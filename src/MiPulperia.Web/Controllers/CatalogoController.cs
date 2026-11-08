using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiPulperia.Web.Data;
using MiPulperia.Web.Models;
using Npgsql;

namespace MiPulperia.Web.Controllers;

public abstract class CatalogoController<T>(PulperiaDbContext db) : Controller where T : Catalogo, new()
{
    protected abstract string Titulo { get; }
    protected abstract string Descripcion { get; }
    protected abstract string[] Columnas { get; }
    protected abstract string[] Celdas(T item);
    public async Task<IActionResult> Index(string? q, CancellationToken ct)
    {
        var query = db.Set<T>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            var escaped = q.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            query = query.Where(x => EF.Functions.ILike(x.Nombre, "%" + escaped + "%", "\\"));
        }
        var items = await query.OrderBy(x => x.Nombre).ThenBy(x => x.Id).ToListAsync(ct);
        return View("CatalogoLista", new ListaCatalogo(Titulo, Descripcion, Columnas,
            items.Select(x => new FilaCatalogo(x.Id, Celdas(x))).ToList(), q));
    }
    public IActionResult Create() => Formulario(new T(), "Crear");
    [HttpPost]
    public async Task<IActionResult> Create(T item, CancellationToken ct)
    {
        item.Id = 0; item.Version = 0;
        ValidarNombre(item);
        if (!ModelState.IsValid) return Formulario(item, "Crear");
        db.Add(item);
        if (!await Guardar(ct)) return Formulario(item, "Crear");
        TempData["Exito"] = "Registro creado correctamente.";
        return RedirectToAction(nameof(Index));
    }
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var item = await db.Set<T>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? NotFound() : Formulario(item, "Editar");
    }
    [HttpPost]
    public async Task<IActionResult> Edit(int id, T item, CancellationToken ct)
    {
        if (id != item.Id) return BadRequest();
        ValidarNombre(item);
        if (!ModelState.IsValid) return Formulario(item, "Editar");
        db.Update(item);
        if (!await Guardar(ct)) return Formulario(item, "Editar");
        TempData["Exito"] = "Cambios guardados correctamente.";
        return RedirectToAction(nameof(Index));
    }
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var item = await db.Set<T>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? NotFound() : Formulario(item, "Detalle");
    }
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var item = await db.Set<T>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? NotFound() : View("CatalogoEliminar", new EliminarCatalogo(item.Id, item.Version, item.Nombre));
    }
    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id, uint version, CancellationToken ct)
    {
        var item = await db.Set<T>().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return NotFound();
        db.Entry(item).Property(x => x.Version).OriginalValue = version;
        db.Remove(item);
        if (!await Guardar(ct)) return View("CatalogoEliminar", new EliminarCatalogo(item.Id, version, item.Nombre));
        TempData["Exito"] = "Registro eliminado correctamente.";
        return RedirectToAction(nameof(Index));
    }
    private IActionResult Formulario(T item, string accion)
    {
        ViewData["Title"] = $"{accion} · {Titulo}";
        ViewData["Accion"] = accion; ViewData["Catalogo"] = Titulo;
        return View("Formulario", item);
    }
    private void ValidarNombre(T item)
    {
        if (item is Sucursal && item.Nombre?.Length > 100) ModelState.AddModelError("Nombre", "Use como máximo 100 caracteres.");
    }
    private async Task<bool> Guardar(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateConcurrencyException)
        { ModelState.AddModelError("", "Otro usuario modificó o eliminó este registro. Regrese al listado y abra la versión actual."); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException)
        {
            var error = (PostgresException)e.InnerException!;
            var message = error.SqlState switch
            {
                "23505" => "Ya existe un registro con ese código, identificación, correo o nombre de sucursal.",
                "23503" or "23001" => "No se puede eliminar: el registro tiene inventario o reservas relacionados.",
                "23514" or "23502" or "22001" => "Revise los valores: no cumplen las restricciones de la base de datos.",
                "P0001" => error.MessageText,
                _ => "No fue posible guardar los cambios. Intente nuevamente."
            };
            ModelState.AddModelError("", message);
        }
        return false;
    }
}
