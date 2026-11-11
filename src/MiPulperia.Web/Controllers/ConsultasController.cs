using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiPulperia.Web.Data;
using MiPulperia.Web.Models;
using MiPulperia.Web.Services;
namespace MiPulperia.Web.Controllers;
public sealed class ConsultasController(PulperiaDbContext db, ConsultasService consultas) : Controller
{
    public async Task<IActionResult> Existencias(string? q, CancellationToken ct) =>
        View(new ConsultaExistencias(await consultas.Existencias(q, null, ct), q));
    public async Task<IActionResult> Reservas(string? q, CancellationToken ct) =>
        View(new ConsultaReservas(await consultas.Reservas(q, null, ct), q));
    public async Task<IActionResult> Cliente(int id, CancellationToken ct)
    {
        var cliente = await db.Clientes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return cliente is null ? NotFound() : View(new DetalleCliente(cliente, await consultas.Reservas(null, id, ct)));
    }
    public async Task<IActionResult> Producto(int id, CancellationToken ct)
    {
        var producto = await db.Productos.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return producto is null ? NotFound() : View(new DetalleProducto(producto, await consultas.Existencias(null, id, ct)));
    }
}
