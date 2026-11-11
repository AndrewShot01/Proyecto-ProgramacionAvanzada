using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiPulperia.Web.Data;
using MiPulperia.Web.Models;
using MiPulperia.Web.Services;

namespace MiPulperia.Web.Controllers;
public sealed class HomeController : Controller
{
    public async Task<IActionResult> Index([FromServices] PulperiaDbContext db, [FromServices] ConsultasService consultas, CancellationToken ct)
    {
        var sucursales = await consultas.Existencias(null, null, ct);
        var existencias = sucursales.SelectMany(x => x.Filas).ToList();
        return View(new Inicio(await db.Sucursales.CountAsync(ct), await db.Clientes.CountAsync(ct),
            await db.Productos.CountAsync(ct), existencias.Sum(x => x.Disponible), existencias.Sum(x => x.Reservada),
            sucursales.Count(x => x.Circuito.Estado != "CERRADO")));
    }
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() { Response.StatusCode = 503; return View(); }
}
