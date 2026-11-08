using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiPulperia.Web.Data;
namespace MiPulperia.Web.Controllers;
public sealed class ConsultasController(PulperiaDbContext db) : Controller
{
    public async Task<IActionResult> Existencias(string? q, CancellationToken ct)
    {
        ViewData["Busqueda"] = q;
        var query = db.Existencias.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.Producto.ToLower().Contains(q.ToLower()));
        return View(await query.OrderBy(x => x.Producto).ThenBy(x => x.Sucursal).ToListAsync(ct));
    }
    public async Task<IActionResult> Reservas(string? q, CancellationToken ct)
    {
        ViewData["Busqueda"] = q;
        var query = db.Reservas.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.Cliente.ToLower().Contains(q.ToLower()));
        return View(await query.OrderByDescending(x => x.FechaReserva).ThenByDescending(x => x.Id).ToListAsync(ct));
    }
}
