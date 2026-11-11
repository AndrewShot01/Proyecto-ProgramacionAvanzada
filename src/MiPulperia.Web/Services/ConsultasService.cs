using Microsoft.EntityFrameworkCore;
using MiPulperia.Web.Data;
using MiPulperia.Web.Models;

namespace MiPulperia.Web.Services;

public sealed class ConsultasService(PulperiaDbContext db, SucursalCircuitos circuitos)
{
    // El directorio central permite mostrar incluso las sucursales temporalmente desconectadas.
    public Task<List<Sucursal>> Directorio(CancellationToken ct) => db.Sucursales.AsNoTracking().OrderBy(s => s.Nombre).ToListAsync(ct);
    public async Task<IReadOnlyList<ExistenciasSucursal>> Existencias(string? q, int? producto, CancellationToken ct)
    {
        var result = new List<ExistenciasSucursal>();
        foreach (var s in await Directorio(ct))
        {
            IReadOnlyList<Existencia> filas = [];
            try
            {
                filas = await circuitos.Ejecutar(s.Id, async token =>
                {
                    var query = db.Existencias.AsNoTracking().Where(x => x.SucursalId == s.Id);
                    if (producto.HasValue) query = query.Where(x => x.ProductoId == producto.Value);
                    if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.Producto.ToLower().Contains(q.ToLower()));
                    return await query.OrderBy(x => x.Producto).ToListAsync(token);
                }, ct);
            }
            catch (Exception ex) when (SucursalCircuitos.NoDisponible(ex)) { }
            result.Add(new(s, circuitos.Estado(s.Id), filas));
        }
        return result;
    }
    public async Task<IReadOnlyList<ReservasSucursal>> Reservas(string? q, int? cliente, CancellationToken ct)
    {
        var result = new List<ReservasSucursal>();
        foreach (var s in await Directorio(ct))
        {
            IReadOnlyList<ReservaConsulta> filas = [];
            try
            {
                filas = await circuitos.Ejecutar(s.Id, async token =>
                {
                    var query = db.Reservas.AsNoTracking().Where(x => x.SucursalId == s.Id);
                    if (cliente.HasValue) query = query.Where(x => x.ClienteId == cliente.Value);
                    if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.Cliente.ToLower().Contains(q.ToLower()));
                    return await query.OrderByDescending(x => x.FechaReserva).ThenByDescending(x => x.Id).ToListAsync(token);
                }, ct);
            }
            catch (Exception ex) when (SucursalCircuitos.NoDisponible(ex)) { }
            result.Add(new(s, circuitos.Estado(s.Id), filas));
        }
        return result;
    }
}
