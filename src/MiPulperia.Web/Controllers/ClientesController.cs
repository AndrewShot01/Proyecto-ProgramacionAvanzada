using MiPulperia.Web.Data;
using MiPulperia.Web.Models;
namespace MiPulperia.Web.Controllers;
public sealed class ClientesController(PulperiaDbContext db) : CatalogoController<Cliente>(db)
{
    protected override string Titulo => "Clientes";
    protected override string Descripcion => "Encuentra y administra a las personas que compran con nosotros.";
    protected override string[] Columnas => ["Nombre", "Identificación", "Correo", "Teléfono"];
    protected override string[] Celdas(Cliente x) => [x.Nombre, x.Identificacion, x.Correo, x.Telefono];
}
