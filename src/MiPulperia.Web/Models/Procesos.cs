using System.ComponentModel.DataAnnotations;

namespace MiPulperia.Web.Models;

public sealed class IngresoFormulario
{
    public Guid SolicitudId { get; set; } = Guid.NewGuid();
    [Range(1, int.MaxValue), Display(Name = "Sucursal")] public int SucursalId { get; set; }
    [Range(1, int.MaxValue), Display(Name = "Producto")] public int ProductoId { get; set; }
    [Range(1, 1000000)] public int Cantidad { get; set; } = 1;
    [Required, StringLength(100), Display(Name = "Referencia del ingreso")] public string Referencia { get; set; } = "";
}
public sealed class ReservaFormulario
{
    public Guid SolicitudId { get; set; } = Guid.NewGuid();
    [Range(1, int.MaxValue), Display(Name = "Sucursal")] public int SucursalId { get; set; }
    [Range(1, int.MaxValue), Display(Name = "Producto")] public int ProductoId { get; set; }
    [Range(1, int.MaxValue), Display(Name = "Cliente")] public int ClienteId { get; set; }
    [Range(1, 1000000)] public int Cantidad { get; set; } = 1;
}
public sealed record EstadoSucursal(int SucursalId, string Estado, string Mensaje, DateTimeOffset? Reintento, long Version);
public sealed record ExistenciasSucursal(Sucursal Sucursal, EstadoSucursal Circuito, IReadOnlyList<Existencia> Filas);
public sealed record ReservasSucursal(Sucursal Sucursal, EstadoSucursal Circuito, IReadOnlyList<ReservaConsulta> Filas);
public sealed record ConsultaExistencias(IReadOnlyList<ExistenciasSucursal> Sucursales, string? Busqueda);
public sealed record ConsultaReservas(IReadOnlyList<ReservasSucursal> Sucursales, string? Busqueda);
public sealed record DetalleCliente(Cliente Cliente, IReadOnlyList<ReservasSucursal> Sucursales);
public sealed record DetalleProducto(Producto Producto, IReadOnlyList<ExistenciasSucursal> Sucursales);
public sealed record PanelSucursales(IReadOnlyList<Sucursal> Sucursales, bool SimulacionHabilitada);
