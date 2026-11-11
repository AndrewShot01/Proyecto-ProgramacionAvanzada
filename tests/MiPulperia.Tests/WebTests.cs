using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MiPulperia.Tests;

public sealed class AppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development")
        .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:Pulperia"] = DatabaseTests.Connection,
            ["Procesos:BackgroundHabilitado"] = "false",
            ["Circuitos:PermitirSimulacion"] = "true",
            ["Circuitos:PausaSegundos"] = "1",
            ["Procesos:ClaveProgramador"] = "clave-exclusiva-pruebas"
        }));
}
public sealed class WebTests(AppFactory factory) : IClassFixture<AppFactory>
{
    private readonly HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    private static string Field(string html, string name) => WebUtility.HtmlDecode(Regex.Match(html, $"name=\"{name}\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
    private async Task<(string Html, Dictionary<string, string> Fields)> Form(string url)
    {
        var html = await client.GetStringAsync(url);
        return (html, new() { ["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken"), ["Id"] = Field(html, "Id"), ["Version"] = Field(html, "Version") });
    }
    [Theory]
    [InlineData("/")][InlineData("/Sucursales")][InlineData("/Clientes")][InlineData("/Productos")]
    [InlineData("/Consultas/Existencias")][InlineData("/Consultas/Reservas")]
    [InlineData("/Sucursales/Create")][InlineData("/Clientes/Create")][InlineData("/Productos/Create")]
    public async Task PaginasDisponibles(string url) => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode);

    [Fact]
    public async Task PostSinAntiforgerySeRechaza() => Assert.Equal(HttpStatusCode.BadRequest,
        (await client.PostAsync("/Clientes/Create", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode);

    [Theory]
    [InlineData("Sucursales")][InlineData("Clientes")][InlineData("Productos")]
    public async Task CicloCompletoCrearConsultarEditarEliminar(string catalogo)
    {
        var nombre = "Prueba " + Guid.NewGuid().ToString("N")[..10];
        var (_, fields) = await Form($"/{catalogo}/Create");
        fields["Nombre"] = nombre; fields["Direccion"] = "Dirección de prueba"; fields["Telefono"] = "8888-9999";
        fields["Identificacion"] = nombre; fields["Correo"] = Guid.NewGuid().ToString("N") + "@example.test";
        fields["Codigo"] = nombre; fields["Precio"] = "1250.50"; fields["Atributos"] = "{\"color\":\"azul\"}";
        fields["MaximoReserva"] = "3"; fields["HorasReserva"] = "48"; fields["ExistenciaMinima"] = "1";
        var create = await client.PostAsync($"/{catalogo}/Create", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, create.StatusCode);
        var list = await client.GetStringAsync($"/{catalogo}?q={Uri.EscapeDataString(nombre)}");
        Assert.Contains(nombre, list);
        var id = Regex.Match(list, $"/{catalogo}/Edit/(\\d+)").Groups[1].Value;
        Assert.NotEmpty(id);
        var (_, edit) = await Form($"/{catalogo}/Edit/{id}");
        fields["Id"] = id; fields["Version"] = edit["Version"]; fields["__RequestVerificationToken"] = edit["__RequestVerificationToken"];
        fields["Nombre"] = nombre + " editado";
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync($"/{catalogo}/Edit/{id}", new FormUrlEncodedContent(fields))).StatusCode);
        Assert.Contains(nombre + " editado", await client.GetStringAsync($"/{catalogo}/Details/{id}"));
        // La versión anterior no debe sobrescribir una edición más reciente.
        var conflict = await client.PostAsync($"/{catalogo}/Edit/{id}", new FormUrlEncodedContent(fields));
        Assert.Contains("Otro usuario", await conflict.Content.ReadAsStringAsync());
        var (_, delete) = await Form($"/{catalogo}/Delete/{id}");
        delete.Remove("Id"); delete.Remove("Version");
        var deleteHtml = await client.GetStringAsync($"/{catalogo}/Delete/{id}");
        delete["version"] = Field(deleteHtml, "version");
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync($"/{catalogo}/Delete/{id}", new FormUrlEncodedContent(delete))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/{catalogo}/Details/{id}")).StatusCode);
    }
    [Fact]
    public async Task JsonInvalidoNoSeGuarda()
    {
        var (_, fields) = await Form("/Productos/Create");
        fields["Nombre"]="Inválido"; fields["Codigo"]="INVALIDO"; fields["Precio"]="1";
        fields["MaximoReserva"]="1"; fields["HorasReserva"]="1"; fields["ExistenciaMinima"]="0"; fields["Atributos"]="[]";
        var response = await client.PostAsync("/Productos/Create", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("objeto JSON", await response.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task BorradoConRelacionesMuestraError()
    {
        var (html, fields) = await Form("/Clientes/Delete/1"); fields.Remove("Id"); fields.Remove("Version"); fields["version"] = Field(html, "version");
        var response = await client.PostAsync("/Clientes/Delete/1", new FormUrlEncodedContent(fields));
        Assert.Contains("reservas relacionados", await response.Content.ReadAsStringAsync());
    }
    [Theory]
    [InlineData("%")][InlineData("_")][InlineData("\\")]
    public async Task BusquedaTrataComodinesComoTexto(string literal)
    {
        await using var db = new Npgsql.NpgsqlConnection(DatabaseTests.Connection);
        await db.OpenAsync();
        var name = "Literal " + literal + " " + Guid.NewGuid().ToString("N");
        await using var insert = new Npgsql.NpgsqlCommand("INSERT INTO user_07.mp_sucursales(nombre,direccion,telefono) VALUES(@nombre,'Prueba','8888') RETURNING id", db);
        insert.Parameters.AddWithValue("nombre", name);
        var id = (int)(await insert.ExecuteScalarAsync())!;
        try
        {
            var html = await client.GetStringAsync("/Sucursales?q=" + Uri.EscapeDataString(literal));
            Assert.Contains(name, html);
            Assert.DoesNotContain("Sucursal Centro", html);
        }
        finally
        {
            await using var delete = new Npgsql.NpgsqlCommand("DELETE FROM user_07.mp_sucursales WHERE id=@id", db);
            delete.Parameters.AddWithValue("id", id);
            await delete.ExecuteNonQueryAsync();
        }
    }
}
