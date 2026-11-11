using MiPulperia.Web.Models;
using Npgsql;

namespace MiPulperia.Web.Services;

public sealed class OperacionesService(NpgsqlDataSource data, SucursalCircuitos circuitos)
{
    public ValueTask<int> Ingresar(IngresoFormulario f, CancellationToken ct) =>
        circuitos.Ejecutar(f.SucursalId, async token =>
        {
            await using var db = await data.OpenConnectionAsync(token);
            await using var tx = await db.BeginTransactionAsync(token);
            await BloquearSolicitud(db, f.SolicitudId, token);
            await using var previo = new NpgsqlCommand("""
                SELECT e.id, i.sucursal_id, i.producto_id, e.cantidad, e.referencia
                FROM user_07.mp_ingresos e JOIN user_07.mp_inventarios i ON i.id=e.inventario_id
                WHERE e.solicitud_id=@solicitud
                """, db, tx);
            previo.Parameters.AddWithValue("solicitud", f.SolicitudId);
            int? existente = null;
            await using (var r = await previo.ExecuteReaderAsync(token))
                if (await r.ReadAsync(token))
                {
                    if (r.GetInt32(1) != f.SucursalId || r.GetInt32(2) != f.ProductoId || r.GetInt32(3) != f.Cantidad || r.GetString(4) != f.Referencia.Trim())
                        throw new InvalidOperationException("El formulario ya se utilizó con otros valores. Abra uno nuevo.");
                    existente = r.GetInt32(0);
                }
            if (existente.HasValue) { await tx.CommitAsync(token); return existente.Value; }
            await using var inventario = new NpgsqlCommand("""
                INSERT INTO user_07.mp_inventarios(sucursal_id,producto_id) VALUES(@sucursal,@producto)
                ON CONFLICT(sucursal_id,producto_id) DO NOTHING;
                SELECT id FROM user_07.mp_inventarios WHERE sucursal_id=@sucursal AND producto_id=@producto;
                """, db, tx);
            inventario.Parameters.AddWithValue("sucursal", f.SucursalId);
            inventario.Parameters.AddWithValue("producto", f.ProductoId);
            var inv = (int)(await inventario.ExecuteScalarAsync(token))!;
            await using var insert = new NpgsqlCommand("""
                INSERT INTO user_07.mp_ingresos(inventario_id,cantidad,referencia,solicitud_id)
                VALUES(@inventario,@cantidad,@referencia,@solicitud) RETURNING id
                """, db, tx);
            insert.Parameters.AddWithValue("inventario", inv);
            insert.Parameters.AddWithValue("cantidad", f.Cantidad);
            insert.Parameters.AddWithValue("referencia", f.Referencia.Trim());
            insert.Parameters.AddWithValue("solicitud", f.SolicitudId);
            var id = (int)(await insert.ExecuteScalarAsync(token))!;
            await tx.CommitAsync(token);
            return id;
        }, ct);

    public ValueTask<int> Reservar(ReservaFormulario f, CancellationToken ct) =>
        circuitos.Ejecutar(f.SucursalId, async token =>
        {
            await using var db = await data.OpenConnectionAsync(token);
            await using var tx = await db.BeginTransactionAsync(token);
            await BloquearSolicitud(db, f.SolicitudId, token);
            await using var previo = new NpgsqlCommand("""
                SELECT r.id,i.sucursal_id,i.producto_id,r.cliente_id,r.cantidad
                FROM user_07.mp_reservas r JOIN user_07.mp_inventarios i ON i.id=r.inventario_id
                WHERE r.solicitud_id=@solicitud
                """, db, tx);
            previo.Parameters.AddWithValue("solicitud", f.SolicitudId);
            int? existente = null;
            await using (var r = await previo.ExecuteReaderAsync(token))
                if (await r.ReadAsync(token))
                {
                    if (r.GetInt32(1) != f.SucursalId || r.GetInt32(2) != f.ProductoId || r.GetInt32(3) != f.ClienteId || r.GetInt32(4) != f.Cantidad)
                        throw new InvalidOperationException("El formulario ya se utilizó con otros valores. Abra uno nuevo.");
                    existente = r.GetInt32(0);
                }
            if (existente.HasValue) { await tx.CommitAsync(token); return existente.Value; }
            await using var insert = new NpgsqlCommand("""
                INSERT INTO user_07.mp_reservas(inventario_id,cliente_id,cantidad,solicitud_id)
                SELECT id,@cliente,@cantidad,@solicitud FROM user_07.mp_inventarios
                WHERE sucursal_id=@sucursal AND producto_id=@producto RETURNING id
                """, db, tx);
            insert.Parameters.AddWithValue("sucursal", f.SucursalId);
            insert.Parameters.AddWithValue("producto", f.ProductoId);
            insert.Parameters.AddWithValue("cliente", f.ClienteId);
            insert.Parameters.AddWithValue("cantidad", f.Cantidad);
            insert.Parameters.AddWithValue("solicitud", f.SolicitudId);
            var result = await insert.ExecuteScalarAsync(token);
            if (result is not int id) throw new InvalidOperationException("Este producto aún no tiene inventario en la sucursal.");
            await tx.CommitAsync(token);
            return id;
        }, ct);

    public ValueTask<bool> Comprar(int sucursalId, int reservaId, CancellationToken ct) =>
        circuitos.Ejecutar(sucursalId, async token =>
        {
            await using var db = await data.OpenConnectionAsync(token);
            await using var cmd = new NpgsqlCommand("""
                UPDATE user_07.mp_reservas r SET estado='COMPRADO'
                FROM user_07.mp_inventarios i
                WHERE r.inventario_id=i.id AND i.sucursal_id=@sucursal AND r.id=@id
                  AND r.estado='RESERVADO' AND r.fecha_vencimiento > statement_timestamp()
                """, db);
            cmd.Parameters.AddWithValue("sucursal", sucursalId);
            cmd.Parameters.AddWithValue("id", reservaId);
            return await cmd.ExecuteNonQueryAsync(token) == 1;
        }, ct);

    private static async Task BloquearSolicitud(NpgsqlConnection db, Guid solicitud, CancellationToken ct)
    {
        if (solicitud == Guid.Empty) throw new InvalidOperationException("Abra un formulario nuevo para registrar la operación.");
        await using var cmd = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@solicitud,7107))", db);
        cmd.Parameters.AddWithValue("solicitud", solicitud.ToString());
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
