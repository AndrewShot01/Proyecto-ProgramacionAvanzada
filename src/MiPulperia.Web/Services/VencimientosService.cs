using Microsoft.AspNetCore.SignalR;
using Npgsql;

namespace MiPulperia.Web.Services;

public sealed class VencimientosService(NpgsqlDataSource data, IHubContext<SucursalesHub> hub, ILogger<VencimientosService> logger)
{
    public async Task<int> Ejecutar(CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("SELECT user_07.mp_cancelar_reservas_vencidas(200)", db);
        var total = (int)(await command.ExecuteScalarAsync(ct))!;
        if (total > 0)
            try { await hub.Clients.All.SendAsync("ReservasVencidas", total, ct); }
            catch (Exception ex) { logger.LogWarning(ex, "Las reservas vencieron, pero no se pudo notificar a los navegadores."); }
        return total;
    }
}
public sealed class ProcesosBackground(IServiceScopeFactory scopes, SucursalCircuitos circuitos,
    NpgsqlDataSource data, IConfiguration config, ILogger<ProcesosBackground> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("Procesos:BackgroundHabilitado", true)) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<VencimientosService>().Ejecutar(stoppingToken);
                foreach (var id in circuitos.Ids())
                {
                    if (circuitos.Estado(id).Estado == "CERRADO") continue;
                    try
                    {
                        await circuitos.Ejecutar(id, async token =>
                        {
                            await using var db = await data.OpenConnectionAsync(token);
                            await using var probe = new NpgsqlCommand("SELECT 1 FROM user_07.mp_sucursales WHERE id=@id", db);
                            probe.Parameters.AddWithValue("id", id);
                            return await probe.ExecuteScalarAsync(token);
                        }, stoppingToken);
                    }
                    catch (Exception ex) when (SucursalCircuitos.NoDisponible(ex)) { }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning(ex, "No se completó el ciclo de vencimientos; se reintentará."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
