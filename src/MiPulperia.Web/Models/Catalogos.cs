using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace MiPulperia.Web.Models;

public abstract class Catalogo
{
    public int Id { get; set; }
    public uint Version { get; set; }
    [Required(ErrorMessage = "El nombre es obligatorio."), StringLength(120)]
    public string Nombre { get; set; } = "";
}

public sealed class Sucursal : Catalogo
{
    [Required(ErrorMessage = "La dirección es obligatoria."), StringLength(250), Display(Name = "Dirección")]
    public string Direccion { get; set; } = "";
    [Required(ErrorMessage = "El teléfono es obligatorio."), StringLength(25), Display(Name = "Teléfono")]
    public string Telefono { get; set; } = "";
}

public sealed class Cliente : Catalogo
{
    [Required(ErrorMessage = "La identificación es obligatoria."), StringLength(30), Display(Name = "Identificación")]
    public string Identificacion { get; set; } = "";
    [Required(ErrorMessage = "El correo es obligatorio."), EmailAddress(ErrorMessage = "Ingrese un correo válido."), StringLength(150)]
    public string Correo { get; set; } = "";
    [Required(ErrorMessage = "El teléfono es obligatorio."), StringLength(25), Display(Name = "Teléfono")]
    public string Telefono { get; set; } = "";
}

public sealed class Producto : Catalogo, IValidatableObject
{
    [Required(ErrorMessage = "El código es obligatorio."), StringLength(30), Display(Name = "Código")]
    public string Codigo { get; set; } = "";
    [Range(typeof(decimal), "0.01", "9999999999.99", ErrorMessage = "El precio debe ser mayor que cero."), Display(Name = "Precio (CRC)")]
    public decimal Precio { get; set; }
    [Required(ErrorMessage = "Ingrese un objeto JSON."), Display(Name = "Atributos (JSON)")]
    public string Atributos { get; set; } = "{}";
    [Range(1, 1000000), Display(Name = "Máximo por cliente")]
    public int MaximoReserva { get; set; } = 1;
    [Range(typeof(decimal), "0.000001", "8760"), Display(Name = "Duración de reserva (horas)")]
    public decimal HorasReserva { get; set; } = 24;
    [Range(0, 1000000), Display(Name = "Existencia mínima")]
    public int ExistenciaMinima { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var valid = false;
        try { using var json = JsonDocument.Parse(Atributos ?? ""); valid = json.RootElement.ValueKind == JsonValueKind.Object; }
        catch (JsonException) { }
        if (!valid) yield return new ValidationResult("Los atributos deben ser un objeto JSON válido, por ejemplo: {\"color\":\"azul\"}.", [nameof(Atributos)]);
        if (decimal.Round(Precio, 2) != Precio) yield return new ValidationResult("Use como máximo dos decimales en el precio.", [nameof(Precio)]);
        if (decimal.Round(HorasReserva, 6) != HorasReserva) yield return new ValidationResult("Use como máximo seis decimales en las horas.", [nameof(HorasReserva)]);
    }
}

public sealed record FilaCatalogo(int Id, string[] Celdas);
public sealed record ListaCatalogo(string Titulo, string Descripcion, string[] Columnas, IReadOnlyList<FilaCatalogo> Filas, string? Busqueda);
public sealed record EliminarCatalogo(int Id, uint Version, string Nombre);
public sealed record Inicio(int Sucursales, int Clientes, int Productos, long Disponibles, long Reservadas);

public sealed class Existencia
{
    public int InventarioId { get; set; }
    public string Sucursal { get; set; } = "";
    public string Codigo { get; set; } = "";
    public string Producto { get; set; } = "";
    public decimal Precio { get; set; }
    public string Atributos { get; set; } = "{}";
    public int MaximoReserva { get; set; }
    public decimal HorasReserva { get; set; }
    public int ExistenciaMinima { get; set; }
    public long Disponible { get; set; }
    public long Reservada { get; set; }
    public long Vendida { get; set; }
}
public sealed class ReservaConsulta
{
    public int Id { get; set; }
    public string Cliente { get; set; } = "";
    public string Sucursal { get; set; } = "";
    public string Producto { get; set; } = "";
    public int Cantidad { get; set; }
    public decimal Total { get; set; }
    public DateTime FechaReserva { get; set; }
    public DateTime FechaVencimiento { get; set; }
    public DateTime? FechaCierre { get; set; }
    public string Estado { get; set; } = "";
}
