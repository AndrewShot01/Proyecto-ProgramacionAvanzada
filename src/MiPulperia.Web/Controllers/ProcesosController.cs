using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MiPulperia.Web.Data;
using MiPulperia.Web.Models;
using MiPulperia.Web.Services;
using Npgsql;

namespace MiPulperia.Web.Controllers;
public sealed class ProcesosController(PulperiaDbContext db, OperacionesService operaciones, SucursalCircuitos circuitos) : Controller
{
    public async Task<IActionResult> Ingreso(int? sucursalId, int? productoId, CancellationToken ct)
    { await Opciones(ct); return View(new IngresoFormulario { SucursalId = sucursalId ?? 0, ProductoId = productoId ?? 0 }); }
    [HttpPost]
    public async Task<IActionResult> Ingreso(IngresoFormulario form, CancellationToken ct)
    {
        if (ModelState.IsValid)
            try
            {
                var id = await operaciones.Ingresar(form, ct);
                TempData["Exito"] = $"Ingreso {id} registrado. El historial no se puede modificar ni borrar.";
                return RedirectToAction("Existencias", "Consultas");
            }
            catch (Exception ex) when (MostrarError(ex)) { }
        await Opciones(ct); return View(form);
    }
    public async Task<IActionResult> Reserva(int? sucursalId, int? productoId, int? clienteId, CancellationToken ct)
    {
        await Opciones(ct);
        return View(new ReservaFormulario { SucursalId = sucursalId ?? 0, ProductoId = productoId ?? 0, ClienteId = clienteId ?? 0 });
    }
    [HttpPost]
    public async Task<IActionResult> Reserva(ReservaFormulario form, CancellationToken ct)
    {
        if (ModelState.IsValid)
            try
            {
                var id = await operaciones.Reservar(form, ct);
                TempData["Exito"] = $"Reserva {id} registrada correctamente.";
                return RedirectToAction("Reservas", "Consultas");
            }
            catch (Exception ex) when (MostrarError(ex)) { }
        await Opciones(ct); return View(form);
    }
    public async Task<IActionResult> Comprar(int id, int sucursalId, CancellationToken ct)
    {
        try
        {
            var reserva = await circuitos.Ejecutar(sucursalId, async token =>
                await db.Reservas.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.SucursalId == sucursalId, token), ct);
            return reserva is null ? NotFound() : View(reserva);
        }
        catch (Exception ex) when (SucursalCircuitos.NoDisponible(ex))
        { Response.StatusCode = 503; return View("NoDisponible", circuitos.Estado(sucursalId)); }
    }
    [HttpPost, ActionName("Comprar")]
    public async Task<IActionResult> ConfirmarCompra(int id, int sucursalId, CancellationToken ct)
    {
        try
        {
            var comprado = await operaciones.Comprar(sucursalId, id, ct);
            TempData[comprado ? "Exito" : "Error"] = comprado
                ? "Compra registrada correctamente. La reserva ya no puede modificarse."
                : "No se registró la compra: la reserva venció, ya fue finalizada o no pertenece a esta sucursal.";
        }
        catch (Exception ex) when (SucursalCircuitos.NoDisponible(ex))
        { TempData["Error"] = SucursalCircuitos.MensajeNoDisponible; }
        catch (PostgresException ex) when (ex.SqlState == "P0001")
        { TempData["Error"] = ex.MessageText; }
        return RedirectToAction("Reservas", "Consultas");
    }
    private async Task Opciones(CancellationToken ct)
    {
        ViewBag.Sucursales = new SelectList(await db.Sucursales.AsNoTracking().OrderBy(x => x.Nombre).ToListAsync(ct), "Id", "Nombre");
        ViewBag.Productos = new SelectList(await db.Productos.AsNoTracking().OrderBy(x => x.Nombre).ToListAsync(ct), "Id", "Nombre");
        ViewBag.Clientes = new SelectList(await db.Clientes.AsNoTracking().OrderBy(x => x.Nombre).ToListAsync(ct), "Id", "Nombre");
    }
    private bool MostrarError(Exception ex)
    {
        var mensaje = ex switch
        {
            _ when SucursalCircuitos.NoDisponible(ex) => SucursalCircuitos.MensajeNoDisponible,
            PostgresException { SqlState: "P0001" } p => p.MessageText,
            PostgresException { SqlState: "23503" } => "Seleccione una sucursal, producto y cliente existentes.",
            PostgresException { SqlState: "23514" or "23502" or "22001" } => "Revise la cantidad y los datos del formulario.",
            InvalidOperationException i => i.Message,
            _ => null
        };
        if (mensaje is null) return false;
        ModelState.AddModelError("", mensaje); return true;
    }
}
