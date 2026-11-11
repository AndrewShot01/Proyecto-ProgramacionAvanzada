using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MiPulperia.Tests;

public sealed class AccesoTests
{
    [Fact]
    public async Task AccesoProtegeDatosValidaClaveYRechazaRedireccionExterna()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseEnvironment("Development").ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string,string?>
            {
                ["ConnectionStrings:Pulperia"] = DatabaseTests.Connection,
                ["Procesos:BackgroundHabilitado"] = "false",
                ["Acceso:Habilitado"] = "true", ["Acceso:Usuario"] = "pruebas",
                ["Acceso:ClaveHash"] = new PasswordHasher<string>().HashPassword("pruebas", "clave-solo-prueba")
            })));
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var anonimo = await client.GetAsync("/Clientes");
        Assert.Equal(HttpStatusCode.Redirect, anonimo.StatusCode);
        Assert.Contains("/Acceso/Entrar", anonimo.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/css/site.css")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Acceso/Entrar", new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode);
        var html = await client.GetStringAsync("/Acceso/Entrar");
        var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
        var form = new Dictionary<string,string> { ["__RequestVerificationToken"] = token, ["Usuario"] = "pruebas", ["Clave"] = "incorrecta", ["ReturnUrl"] = "https://example.test" };
        Assert.Contains("incorrectos", await (await client.PostAsync("/Acceso/Entrar", new FormUrlEncodedContent(form))).Content.ReadAsStringAsync());
        form["Clave"] = "clave-solo-prueba";
        var login = await client.PostAsync("/Acceso/Entrar", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/", login.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Clientes")).StatusCode);
    }
}
