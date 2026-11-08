using Npgsql;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace MiPulperia.Tests;

public sealed class DatabaseTests : IAsyncLifetime
{
    internal static string Connection => Environment.GetEnvironmentVariable("PULPERIA_TEST_CONNECTION")
        ?? throw new InvalidOperationException("Defina PULPERIA_TEST_CONNECTION apuntando a una base de pruebas, nunca a la nube del profesor.");
    private NpgsqlConnection db = null!;
    private NpgsqlTransaction transaction = null!;
    public async Task InitializeAsync()
    {
        db = new NpgsqlConnection(Connection); await db.OpenAsync();
        transaction = await db.BeginTransactionAsync();
    }
    public async Task DisposeAsync() { await transaction.RollbackAsync(); await db.DisposeAsync(); }
    private async Task<object?> Scalar(string sql) => await new NpgsqlCommand(sql, db).ExecuteScalarAsync();
    private async Task Run(string sql) => await new NpgsqlCommand(sql, db).ExecuteNonQueryAsync();
    private async Task Reject(string sql, string state = "P0001")
    {
        await transaction.SaveAsync("rechazo");
        var ex = await Assert.ThrowsAsync<PostgresException>(() => Run(sql));
        Assert.Equal(state, ex.SqlState);
        await transaction.RollbackAsync("rechazo");
    }
    [Theory]
    [InlineData("mp_sucursales")][InlineData("mp_clientes")][InlineData("mp_productos")]
    [InlineData("mp_inventarios")][InlineData("mp_ingresos")][InlineData("mp_reservas")]
    public async Task TodasLasTablasTienenDatos(string tabla) => Assert.True((long)(await Scalar($"SELECT count(*) FROM user_07.{tabla}"))! > 0);

    [Theory]
    [InlineData("UPDATE user_07.mp_ingresos SET cantidad=cantidad+1")]
    [InlineData("DELETE FROM user_07.mp_ingresos")]
    [InlineData("TRUNCATE user_07.mp_ingresos")]
    [InlineData("UPDATE user_07.mp_inventarios SET producto_id=producto_id")]
    [InlineData("DELETE FROM user_07.mp_reservas")]
    [InlineData("TRUNCATE user_07.mp_reservas")]
    [InlineData("UPDATE user_07.mp_reservas SET cantidad=cantidad+1 WHERE estado='RESERVADO'")]
    [InlineData("UPDATE user_07.mp_reservas SET estado='RESERVADO' WHERE estado='COMPRADO'")]
    [InlineData("UPDATE user_07.mp_reservas SET estado='CANCELADO' WHERE estado='RESERVADO'")]
    public async Task HistorialRechazaCambiosIndebidos(string sql) => await Reject(sql);

    [Fact]
    public async Task RestriccionesDeCatalogosYReferencias()
    {
        await Reject("INSERT INTO user_07.mp_sucursales(nombre,direccion,telefono) VALUES ('  SUCURSAL CENTRO  ','x','x')", "23505");
        await Reject("UPDATE user_07.mp_productos SET precio=-1", "23514");
        await Reject("UPDATE user_07.mp_productos SET atributos='[]'", "23514");
        await Reject("INSERT INTO user_07.mp_ingresos(inventario_id,cantidad,referencia) VALUES (-1,1,'Prueba')", "23503");
        await transaction.SaveAsync("fk");
        var fk = await Assert.ThrowsAsync<PostgresException>(() => Run("DELETE FROM user_07.mp_clientes WHERE identificacion='DEMO-001'"));
        Assert.Contains(fk.SqlState, new[] { "23503", "23001" });
        await transaction.RollbackAsync("fk");
        await Reject("UPDATE user_07.mp_productos SET maximo_reserva=1 WHERE codigo='ZAP-001'");
        await Reject("UPDATE user_07.mp_productos SET existencia_minima=999 WHERE codigo='ZAP-001'");
    }

    [Fact]
    public async Task IngresoReservaCompraYVistasConcuerdan()
    {
        await Run("INSERT INTO user_07.mp_ingresos(inventario_id,cantidad,referencia) VALUES (1,5,'Prueba transaccional')");
        var antes = (long)(await Scalar("SELECT disponible FROM user_07.mp_v_productos_existencias WHERE inventario_id=1"))!;
        var id = (int)(await Scalar("INSERT INTO user_07.mp_reservas(inventario_id,cliente_id,cantidad) VALUES(1,3,1) RETURNING id"))!;
        Assert.Equal(antes - 1, await Scalar("SELECT disponible FROM user_07.mp_v_productos_existencias WHERE inventario_id=1"));
        await Run($"UPDATE user_07.mp_reservas SET estado='COMPRADO' WHERE id={id}");
        Assert.Equal(antes - 1, await Scalar("SELECT disponible FROM user_07.mp_v_productos_existencias WHERE inventario_id=1"));
        Assert.Equal(true, await Scalar($"SELECT compra_materializada FROM user_07.mp_v_reservas_clientes WHERE id={id}"));
        await Reject($"UPDATE user_07.mp_reservas SET estado='CANCELADO' WHERE id={id}");
    }

    [Fact]
    public async Task LimiteAcumuladoIncluyeOtrasSucursales()
    {
        var id = await Scalar("SELECT i.id FROM user_07.mp_inventarios i JOIN user_07.mp_productos p ON p.id=i.producto_id JOIN user_07.mp_sucursales s ON s.id=i.sucursal_id WHERE p.codigo='ZAP-001' AND s.nombre='Sucursal Heredia'");
        await Reject($"INSERT INTO user_07.mp_reservas(inventario_id,cliente_id,cantidad) VALUES({id},1,2)");
    }

    [Fact]
    public async Task MinimoDeExistenciasNoPuedeReservarse()
    {
        await Run("UPDATE user_07.mp_productos SET maximo_reserva=100 WHERE id=1");
        await Reject("INSERT INTO user_07.mp_reservas(inventario_id,cliente_id,cantidad) VALUES(1,3,19)");
    }

    [Fact]
    public async Task VencimientoDevuelveStockYQuedaInmutable()
    {
        await Run("UPDATE user_07.mp_productos SET horas_reserva=0.000001 WHERE id=(SELECT producto_id FROM user_07.mp_inventarios WHERE id=1)");
        var antes = await Scalar("SELECT disponible FROM user_07.mp_v_productos_existencias WHERE inventario_id=1");
        var id = await Scalar("INSERT INTO user_07.mp_reservas(inventario_id,cliente_id,cantidad) VALUES(1,3,1) RETURNING id");
        await Task.Delay(30);
        await Reject($"UPDATE user_07.mp_reservas SET estado='COMPRADO' WHERE id={id}");
        await Run($"UPDATE user_07.mp_reservas SET estado='CANCELADO' WHERE id={id}");
        Assert.Equal(antes, await Scalar("SELECT disponible FROM user_07.mp_v_productos_existencias WHERE inventario_id=1"));
        await Reject($"UPDATE user_07.mp_reservas SET estado='RESERVADO' WHERE id={id}");
        await Reject($"UPDATE user_07.mp_reservas SET cantidad=1 WHERE id={id}");
    }
}
