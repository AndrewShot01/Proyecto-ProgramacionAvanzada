using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using MiPulperia.Web.Services;

namespace MiPulperia.Web.Controllers;
public sealed class TareasController(IConfiguration config, VencimientosService vencimientos) : ControllerBase
{
    [HttpPost("/tareas/vencimientos"), IgnoreAntiforgeryToken, Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> Vencimientos(CancellationToken ct)
    {
        var secreto = config["Procesos:ClaveProgramador"];
        var recibido = Request.Headers["X-Pulperia-Task-Key"].ToString();
        if (string.IsNullOrWhiteSpace(secreto) || string.IsNullOrWhiteSpace(recibido) ||
            !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(secreto)), SHA256.HashData(Encoding.UTF8.GetBytes(recibido))))
            return Unauthorized();
        var total = 0;
        for (var lote = 0; lote < 5; lote++)
        {
            var n = await vencimientos.Ejecutar(ct); total += n;
            if (n < 200) break;
        }
        return Ok(new { canceladas = total });
    }
}
