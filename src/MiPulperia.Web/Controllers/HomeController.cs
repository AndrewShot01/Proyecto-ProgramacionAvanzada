using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiPulperia.Web.Data;
using MiPulperia.Web.Models;

namespace MiPulperia.Web.Controllers;
public sealed class HomeController : Controller
{
    public async Task<IActionResult> Index([FromServices] PulperiaDbContext db, CancellationToken ct)
    {
        var existencias = await db.Existencias.AsNoTracking().ToListAsync(ct);
        return View(new Inicio(await db.Sucursales.CountAsync(ct), await db.Clientes.CountAsync(ct),
            await db.Productos.CountAsync(ct), existencias.Sum(x => x.Disponible), existencias.Sum(x => x.Reservada)));
    }
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() { Response.StatusCode = 503; return View(); }
}
