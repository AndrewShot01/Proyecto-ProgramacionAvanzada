using MiPulperia.Web.Data;
using MiPulperia.Web.Models;
namespace MiPulperia.Web.Controllers;
public sealed class SucursalesController(PulperiaDbContext db) : CatalogoController<Sucursal>(db)
{
    protected override string Titulo => "Sucursales";
    protected override string Descripcion => "Los puntos de encuentro de tu pulpería con el barrio.";
    protected override string[] Columnas => ["Nombre", "Dirección", "Teléfono"];
    protected override string[] Celdas(Sucursal x) => [x.Nombre, x.Direccion, x.Telefono];
}
