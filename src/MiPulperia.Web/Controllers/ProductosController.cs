using MiPulperia.Web.Data;
using MiPulperia.Web.Models;
namespace MiPulperia.Web.Controllers;
public sealed class ProductosController(PulperiaDbContext db) : CatalogoController<Producto>(db)
{
    protected override string Titulo => "Productos";
    protected override string Descripcion => "Cada producto, sus atributos y sus condiciones de reserva.";
    protected override string[] Columnas => ["Nombre", "Código", "Precio", "Máximo / cliente", "Reserva (h)", "Mínimo"];
    protected override string[] Celdas(Producto x) => [x.Nombre, x.Codigo, x.Precio.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("es-CR")), x.MaximoReserva.ToString(), x.HorasReserva.ToString("0.######"), x.ExistenciaMinima.ToString()];
}
