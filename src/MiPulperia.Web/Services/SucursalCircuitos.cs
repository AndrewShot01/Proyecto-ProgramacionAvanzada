using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using MiPulperia.Web.Models;
using Npgsql;
using Polly;
using Polly.CircuitBreaker;

namespace MiPulperia.Web.Services;

public sealed class CircuitosOpciones
{
    public int PausaSegundos { get; set; } = 20;
    public bool PermitirSimulacion { get; set; }
}
public sealed class SucursalFueraDeLineaException : Exception
{
    public SucursalFueraDeLineaException() : base(SucursalCircuitos.MensajeNoDisponible) { }
}
public sealed class SucursalesHub : Hub
{
    public SucursalesHub(SucursalCircuitos circuitos) => this.circuitos = circuitos;
    private readonly SucursalCircuitos circuitos;
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("EstadosIniciales", circuitos.Estados(), Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }
}
public sealed class SucursalCircuitos
{
    public const string MensajeNoDisponible = "Agency temporarily unavailable. Our system is optimizing connection. Retrying soon.";
    private sealed class Entrada
    {
        public CircuitBreakerStateProvider Estado { get; } = new();
        public ResiliencePipeline Pipeline { get; set; } = null!;
        public long FalloHasta;
        public EstadoSucursal Visible = null!;
    }
    private readonly ConcurrentDictionary<int, Entrada> entradas = new();
    private readonly IHubContext<SucursalesHub> hub;
    private readonly CircuitosOpciones opciones;
    private readonly ILogger<SucursalCircuitos> logger;
    private long version;
    public SucursalCircuitos(IHubContext<SucursalesHub> hub, IOptions<CircuitosOpciones> opciones, ILogger<SucursalCircuitos> logger)
    { this.hub = hub; this.opciones = opciones.Value; this.logger = logger; }

    private Entrada Obtener(int id) => entradas.GetOrAdd(id, Crear);
    private Entrada Crear(int id)
    {
        var e = new Entrada { Visible = new(id, "CERRADO", "Sucursal disponible", null, Interlocked.Increment(ref version)) };
        e.Pipeline = new ResiliencePipelineBuilder().AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5, MinimumThroughput = 2, SamplingDuration = TimeSpan.FromSeconds(30),
            BreakDuration = TimeSpan.FromSeconds(Math.Clamp(opciones.PausaSegundos, 1, 300)),
            StateProvider = e.Estado,
            ShouldHandle = new PredicateBuilder()
                .Handle<SucursalFueraDeLineaException>()
                .Handle<TimeoutException>()
                .Handle<NpgsqlException>(ex => ex is not PostgresException && ex.IsTransient),
            OnOpened = args => Publicar(e, id, "ABIERTO", DateTimeOffset.UtcNow + args.BreakDuration),
            OnHalfOpened = _ => Publicar(e, id, "RECUPERANDO", null),
            OnClosed = _ => Publicar(e, id, "CERRADO", null)
        }).Build();
        return e;
    }
    private async ValueTask Publicar(Entrada e, int id, string estado, DateTimeOffset? reintento)
    {
        var valor = new EstadoSucursal(id, estado, estado == "CERRADO" ? "Sucursal disponible" : MensajeNoDisponible,
            reintento, Interlocked.Increment(ref version));
        Volatile.Write(ref e.Visible, valor);
        // Una falla de notificación nunca debe cambiar el resultado de una operación de datos.
        try { await hub.Clients.All.SendAsync("CircuitoActualizado", valor); }
        catch (Exception ex) { logger.LogWarning(ex, "No se pudo notificar el circuito de {Sucursal}", id); }
    }
    public EstadoSucursal Estado(int id) => Volatile.Read(ref Obtener(id).Visible);
    public IReadOnlyList<EstadoSucursal> Estados() => entradas.Values.Select(e => Volatile.Read(ref e.Visible)).ToArray();
    public int[] Ids() => entradas.Keys.ToArray();
    public async ValueTask<T> Ejecutar<T>(int sucursal, Func<CancellationToken, ValueTask<T>> operacion, CancellationToken ct)
    {
        var e = Obtener(sucursal);
        try
        {
            var result = await e.Pipeline.ExecuteAsync(async token =>
            {
                if (Interlocked.Read(ref e.FalloHasta) > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                    throw new SucursalFueraDeLineaException();
                return await operacion(token);
            }, ct);
            if (e.Estado.CircuitState == CircuitState.Closed && e.Visible.Estado != "CERRADO")
                await Publicar(e, sucursal, "CERRADO", null);
            return result;
        }
        catch (Exception ex) when (NoDisponible(ex))
        {
            if (e.Estado.CircuitState == CircuitState.Closed && e.Visible.Estado != "RECUPERANDO")
                await Publicar(e, sucursal, "RECUPERANDO", null);
            throw;
        }
    }
    public async Task SimularFallo(int id, int segundos, CancellationToken ct)
    {
        if (!opciones.PermitirSimulacion) throw new InvalidOperationException("La simulación no está habilitada.");
        var e = Obtener(id);
        Interlocked.Exchange(ref e.FalloHasta, DateTimeOffset.UtcNow.AddSeconds(Math.Clamp(segundos, 5, 120)).ToUnixTimeMilliseconds());
        // Los fallos atraviesan Polly; no se cambia el estado con una bandera visual.
        // Incluye fallos suficientes aun cuando hubo solicitudes exitosas recientes.
        // Todas estas llamadas fallan antes de acceder a PostgreSQL.
        for (var n = 0; n < 128 && e.Estado.CircuitState == CircuitState.Closed; n++)
            try { await Ejecutar(id, _ => ValueTask.FromResult(0), ct); }
            catch (Exception ex) when (NoDisponible(ex)) { }
    }
    public static bool NoDisponible(Exception ex) =>
        ex is BrokenCircuitException or SucursalFueraDeLineaException or TimeoutException ||
        ex is NpgsqlException np && np is not PostgresException && np.IsTransient;
}
