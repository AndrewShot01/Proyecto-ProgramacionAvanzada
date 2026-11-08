using Npgsql;
namespace MiPulperia.Tests;

public sealed class ConcurrencyTests
{
    [Fact]
    public async Task DosReservasSimultaneasNoSobrevenden()
    {
        await using var db = new NpgsqlConnection(DatabaseTests.Connection); await db.OpenAsync();
        var suffix = Guid.NewGuid().ToString("N")[..12];
        async Task<int> Id(string sql) => (int)(await new NpgsqlCommand(sql, db).ExecuteScalarAsync())!;
        var product = await Id($"INSERT INTO user_07.mp_productos(codigo,nombre,precio,maximo_reserva,horas_reserva,existencia_minima) VALUES('CON-{suffix}','Concurrencia {suffix}',100,5,24,1) RETURNING id");
        var inventory = await Id($"INSERT INTO user_07.mp_inventarios(sucursal_id,producto_id) VALUES(1,{product}) RETURNING id");
        await new NpgsqlCommand($"INSERT INTO user_07.mp_ingresos(inventario_id,cantidad,referencia) VALUES({inventory},5,'Concurrencia')", db).ExecuteNonQueryAsync();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Reserve(int customer)
        {
            await using var connection = new NpgsqlConnection(DatabaseTests.Connection); await connection.OpenAsync();
            await gate.Task;
            try { await new NpgsqlCommand($"INSERT INTO user_07.mp_reservas(inventario_id,cliente_id,cantidad) VALUES({inventory},{customer},3)", connection).ExecuteNonQueryAsync(); return true; }
            catch (PostgresException ex) when (ex.SqlState == "P0001") { return false; }
        }
        var a = Reserve(1); var b = Reserve(2); gate.SetResult();
        var results = await Task.WhenAll(a,b);
        Assert.Single(results, x => x);
        Assert.Equal(2L, await new NpgsqlCommand($"SELECT disponible FROM user_07.mp_v_productos_existencias WHERE inventario_id={inventory}", db).ExecuteScalarAsync());
    }
}
