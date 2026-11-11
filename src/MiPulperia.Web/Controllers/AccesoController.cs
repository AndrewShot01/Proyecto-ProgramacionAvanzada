using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MiPulperia.Web.Controllers;

public sealed class AccesoFormulario
{
    [Required, StringLength(100)] public string Usuario { get; set; } = "";
    [Required, StringLength(200), DataType(DataType.Password)] public string Clave { get; set; } = "";
    public string? ReturnUrl { get; set; }
}
public sealed class AccesoController(IConfiguration config, IPasswordHasher<string> hasher) : Controller
{
    [HttpGet, AllowAnonymous]
    public IActionResult Entrar(string? returnUrl) => View(new AccesoFormulario { ReturnUrl = returnUrl });

    [HttpPost, AllowAnonymous, EnableRateLimiting("acceso")]
    public async Task<IActionResult> Entrar(AccesoFormulario form)
    {
        var usuario = config["Acceso:Usuario"];
        var hash = config["Acceso:ClaveHash"];
        var valida = false;
        if (ModelState.IsValid && !string.IsNullOrEmpty(usuario) && !string.IsNullOrEmpty(hash))
        {
            try { valida = hasher.VerifyHashedPassword(usuario, hash, form.Clave) != PasswordVerificationResult.Failed
                    && string.Equals(form.Usuario, usuario, StringComparison.OrdinalIgnoreCase); }
            catch (FormatException) { }
        }
        if (!valida)
        {
            ModelState.AddModelError("", "Usuario o contraseña incorrectos.");
            form.Clave = ""; ModelState.Remove(nameof(form.Clave));
            return View(form);
        }
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, usuario!)], CookieAuthenticationDefaults.AuthenticationScheme)));
        return LocalRedirect(Url.IsLocalUrl(form.ReturnUrl) ? form.ReturnUrl! : "/");
    }

    [HttpPost]
    public async Task<IActionResult> Salir()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Entrar));
    }
}
