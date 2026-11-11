using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using MiPulperia.Web.Models;
using MiPulperia.Web.Services;
using Npgsql;
using Polly.CircuitBreaker;

namespace MiPulperia.Tests;

public sealed class ProcesosTests(AppFactory factory) : IClassFixture<AppFactory>
{
    private readonly HttpClient client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    private static string Field(string html, string name) => WebUtility.HtmlDecode(Regex.Match(html, $"name=\"{name}\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
    private async Task<Dictionary<string,string>> Form(string path)
    {
        var html = await client.GetStringAsync(path);
        return new() { ["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken"), ["SolicitudId"] = Field(html,"SolicitudId") };
    }
    private static async Task<(int Sucursal, int Producto, int Cliente)> Datos(decimal horas = 1)
    {
        await using var db = new NpgsqlConnection(DatabaseTests.Connection); await db.OpenAsync();
        var sufijo = Guid.NewGuid().ToString("N")[..20];
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO user_07.mp_sucursales(nombre,direccion,telefono) VALUES(@nombre,'Prueba aislada','8888') RETURNING id;
            INSERT INTO user_07.mp_productos(nombre,codigo,precio,atributos,maximo_reserva,horas_reserva,existencia_minima)
              VALUES(@nombre,@nombre,100,'{}',5,@horas,1) RETURNING id;
            INSERT INTO user_07.mp_clientes(nombre,identificacion,correo,telefono)
              VALUES(@nombre,@nombre,@correo,'8888') RETURNING id;
            """, db);
        cmd.Parameters.AddWithValue("nombre", "Proceso "+sufijo); cmd.Parameters.AddWithValue("correo",sufijo+"@example.test"); cmd.Parameters.AddWithValue("horas",horas);
        await using var r = await cmd.ExecuteReaderAsync();
        await r.ReadAsync(); var s = r.GetInt32(0); await r.NextResultAsync(); await r.ReadAsync(); var p = r.GetInt32(0);
        await r.NextResultAsync(); await r.ReadAsync(); var c = r.GetInt32(0);
        return(s,p,c);
    }
    private static async Task<object?> Scalar(string sql, int id)
    {
        await using var db = new NpgsqlConnection(DatabaseTests.Connection); await db.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql,db); cmd.Parameters.AddWithValue("id",id); return await cmd.ExecuteScalarAsync();
    }
    [Theory]
    [InlineData("/Procesos/Ingreso")][InlineData("/Procesos/Reserva")][InlineData("/Estado")]
    [InlineData("/Consultas/Cliente/1")][InlineData("/Consultas/Producto/1")]
    public async Task PantallasDeSegundoAvanceDisponibles(string path) => Assert.Equal(HttpStatusCode.OK,(await client.GetAsync(path)).StatusCode);

    [Theory]
    [InlineData("/Procesos/Ingreso")][InlineData("/Procesos/Reserva")][InlineData("/Procesos/Comprar/1?sucursalId=1")][InlineData("/Estado/Simular/1")]
    public async Task OperacionesExigenAntiforgery(string path) =>
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync(path,new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode);

    [Fact]
    public async Task DobleEnvioConcurrenteNoDuplicaIngresoNiReservaYCompraEsDefinitiva()
    {
        var f = await Datos();
        using var scope = factory.Services.CreateScope(); var servicio = scope.ServiceProvider.GetRequiredService<OperacionesService>();
        var ingreso = new IngresoFormulario { SucursalId=f.Sucursal, ProductoId=f.Producto, Cantidad=10, Referencia="Entrega" };
        var ingresos = await Task.WhenAll(servicio.Ingresar(ingreso,default).AsTask(),servicio.Ingresar(ingreso,default).AsTask());
        Assert.Equal(ingresos[0],ingresos[1]);
        var reserva = new ReservaFormulario { SucursalId=f.Sucursal, ProductoId=f.Producto, ClienteId=f.Cliente, Cantidad=2 };
        var reservas = await Task.WhenAll(servicio.Reservar(reserva,default).AsTask(),servicio.Reservar(reserva,default).AsTask());
        Assert.Equal(reservas[0],reservas[1]);
        Assert.Equal(8L,await Scalar("SELECT disponible FROM user_07.mp_v_productos_existencias WHERE producto_id=@id",f.Producto));
        var compras = await Task.WhenAll(servicio.Comprar(f.Sucursal,reservas[0],default).AsTask(),servicio.Comprar(f.Sucursal,reservas[0],default).AsTask());
        Assert.Single(compras, x => x);
        Assert.Equal("COMPRADO",await Scalar("SELECT estado FROM user_07.mp_reservas WHERE id=@id",reservas[0]));
        reserva.Cantidad=3;
        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.Reservar(reserva,default).AsTask());
    }
    [Fact]
    public async Task FormulariosRegistranIngresoReservaYCompra()
    {
        var f=await Datos();
        var ingreso=await Form("/Procesos/Ingreso"); ingreso["SucursalId"]=f.Sucursal.ToString(); ingreso["ProductoId"]=f.Producto.ToString(); ingreso["Cantidad"]="10"; ingreso["Referencia"]="Formulario HTTP";
        Assert.Equal(HttpStatusCode.Redirect,(await client.PostAsync("/Procesos/Ingreso",new FormUrlEncodedContent(ingreso))).StatusCode);
        var reserva=await Form("/Procesos/Reserva"); reserva["SucursalId"]=f.Sucursal.ToString(); reserva["ProductoId"]=f.Producto.ToString(); reserva["ClienteId"]=f.Cliente.ToString(); reserva["Cantidad"]="2";
        Assert.Equal(HttpStatusCode.Redirect,(await client.PostAsync("/Procesos/Reserva",new FormUrlEncodedContent(reserva))).StatusCode);
        var id=(int)(await Scalar("SELECT id FROM user_07.mp_reservas WHERE cliente_id=@id",f.Cliente))!;
        var compra=await Form($"/Procesos/Comprar/{id}?sucursalId={f.Sucursal}"); compra["sucursalId"]=f.Sucursal.ToString();
        Assert.Equal(HttpStatusCode.Redirect,(await client.PostAsync($"/Procesos/Comprar/{id}",new FormUrlEncodedContent(compra))).StatusCode);
        Assert.Equal("COMPRADO",await Scalar("SELECT estado FROM user_07.mp_reservas WHERE id=@id",id));
        Assert.Contains("Compra materializada",await client.GetStringAsync($"/Consultas/Cliente/{f.Cliente}"));
    }
    [Fact]
    public async Task ReservaExcesivaYValoresInvalidosNoDescuentanInventario()
    {
        var f=await Datos(); using var scope=factory.Services.CreateScope();
        var servicio=scope.ServiceProvider.GetRequiredService<OperacionesService>();
        await servicio.Ingresar(new() {SucursalId=f.Sucursal,ProductoId=f.Producto,Cantidad=3,Referencia="Stock limitado"},default);
        var campos=await Form("/Procesos/Reserva"); campos["SucursalId"]=f.Sucursal.ToString();campos["ProductoId"]=f.Producto.ToString();campos["ClienteId"]=f.Cliente.ToString();campos["Cantidad"]="3";
        var error=await client.PostAsync("/Procesos/Reserva",new FormUrlEncodedContent(campos));
        Assert.Equal(HttpStatusCode.OK,error.StatusCode);
        Assert.Contains("Inventario insuficiente",WebUtility.HtmlDecode(await error.Content.ReadAsStringAsync()));
        Assert.Equal(3L,await Scalar("SELECT disponible FROM user_07.mp_v_productos_existencias WHERE producto_id=@id",f.Producto));
        campos["Cantidad"]="-1";
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsync("/Procesos/Reserva",new FormUrlEncodedContent(campos))).StatusCode);
        Assert.Equal(0L,await Scalar("SELECT count(*) FROM user_07.mp_reservas WHERE cliente_id=@id",f.Cliente));
    }
    [Fact]
    public async Task TareaAutenticadaCancelaSoloVencidasYLiberaSaldo()
    {
        var f=await Datos(0.00005m); using var scope=factory.Services.CreateScope();
        var servicio=scope.ServiceProvider.GetRequiredService<OperacionesService>();
        await servicio.Ingresar(new(){SucursalId=f.Sucursal,ProductoId=f.Producto,Cantidad=10,Referencia="Prueba vencimiento"},default);
        var id=await servicio.Reservar(new(){SucursalId=f.Sucursal,ProductoId=f.Producto,ClienteId=f.Cliente,Cantidad=2},default);
        var vigente = await Datos(1);
        await servicio.Ingresar(new(){SucursalId=vigente.Sucursal,ProductoId=vigente.Producto,Cantidad=10,Referencia="Reserva vigente"},default);
        var vigenteId = await servicio.Reservar(new(){SucursalId=vigente.Sucursal,ProductoId=vigente.Producto,ClienteId=vigente.Cliente,Cantidad=1},default);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsync("/tareas/vencimientos",null)).StatusCode);
        await Task.Delay(250);
        var req=new HttpRequestMessage(HttpMethod.Post,"/tareas/vencimientos"); req.Headers.Add("X-Pulperia-Task-Key","clave-exclusiva-pruebas");
        Assert.Equal(HttpStatusCode.OK,(await client.SendAsync(req)).StatusCode);
        Assert.Equal("CANCELADO",await Scalar("SELECT estado FROM user_07.mp_reservas WHERE id=@id",id));
        Assert.Equal("RESERVADO",await Scalar("SELECT estado FROM user_07.mp_reservas WHERE id=@id",vigenteId));
        Assert.Equal(10L,await Scalar("SELECT disponible FROM user_07.mp_v_productos_existencias WHERE producto_id=@id",f.Producto));
        Assert.False(await servicio.Comprar(f.Sucursal,id,default));
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/Procesos/Cancelar/1")).StatusCode);
    }
    [Fact]
    public async Task PollyAislaUnaSucursalSinEjecutarDelegadoYSeRecupera()
    {
        var circuitos=factory.Services.GetRequiredService<SucursalCircuitos>();
        const int sucursal=900001;
        for(var i=0;i<2;i++) await Assert.ThrowsAsync<TimeoutException>(()=>circuitos.Ejecutar<int>(sucursal,_=>throw new TimeoutException(),default).AsTask());
        Assert.Equal("ABIERTO",circuitos.Estado(sucursal).Estado);
        var llamadas=0;
        await Assert.ThrowsAsync<BrokenCircuitException>(()=>circuitos.Ejecutar(sucursal,_=>{llamadas++;return ValueTask.FromResult(1);},default).AsTask());
        Assert.Equal(0,llamadas);
        Assert.Equal(1,await circuitos.Ejecutar(sucursal+1,_=>ValueTask.FromResult(1),default));
        await Task.Delay(1100);
        Assert.Equal(1,await circuitos.Ejecutar(sucursal,_=>{llamadas++;return ValueTask.FromResult(1);},default));
        Assert.Equal(1,llamadas); Assert.Equal("CERRADO",circuitos.Estado(sucursal).Estado);
    }
    [Fact]
    public async Task SimulacionAbreCircuitoConTraficoExitosoReciente()
    {
        var circuitos=factory.Services.GetRequiredService<SucursalCircuitos>();
        for(var i=0;i<20;i++) await circuitos.Ejecutar(900004,_=>ValueTask.FromResult(1),default);
        await circuitos.SimularFallo(900004,5,default);
        Assert.Equal("ABIERTO",circuitos.Estado(900004).Estado);
    }
    [Fact]
    public async Task ErroresDeNegocioNoAbrenCircuito()
    {
        var circuitos=factory.Services.GetRequiredService<SucursalCircuitos>();
        for(var i=0;i<3;i++) await Assert.ThrowsAsync<PostgresException>(()=>circuitos.Ejecutar<int>(900003,_=>throw new PostgresException("Inventario insuficiente","ERROR","ERROR","P0001"),default).AsTask());
        Assert.Equal("CERRADO",circuitos.Estado(900003).Estado);
    }
}
