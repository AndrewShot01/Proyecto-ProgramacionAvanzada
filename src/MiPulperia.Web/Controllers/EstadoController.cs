using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MiPulperia.Web.Data;
using MiPulperia.Web.Models;
using MiPulperia.Web.Services;

namespace MiPulperia.Web.Controllers;
public sealed class EstadoController(PulperiaDbContext db, SucursalCircuitos circuitos, IOptions<CircuitosOpciones> opciones) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(new PanelSucursales(await db.Sucursales.AsNoTracking().OrderBy(x => x.Nombre).ToListAsync(ct), opciones.Value.PermitirSimulacion));
    [HttpGet]
    public IActionResult Actual() => Json(circuitos.Estados());
    [HttpPost]
    public async Task<IActionResult> Simular(int id, int segundos = 30, CancellationToken ct = default)
    {
        if (!opciones.Value.PermitirSimulacion) return NotFound();
        if (!await db.Sucursales.AnyAsync(s => s.Id == id, ct)) return NotFound();
        await circuitos.SimularFallo(id, segundos, ct);
        TempData["Exito"] = "Fallo temporal simulado. Polly probará la recuperación automáticamente.";
        return RedirectToAction(nameof(Index));
    }
}
