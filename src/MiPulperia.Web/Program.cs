using Microsoft.EntityFrameworkCore;
using MiPulperia.Web.Data;
using MiPulperia.Web.Services;
using Npgsql;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.LoginPath = "/Acceso/Entrar";
    options.Cookie.Name = "Pulperia.Acceso";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(4);
    options.SlidingExpiration = true;
});
builder.Services.AddSingleton<IPasswordHasher<string>, PasswordHasher<string>>();
builder.Services.AddAuthorization(options =>
{
    if (builder.Configuration.GetValue("Acceso:Habilitado", !builder.Environment.IsDevelopment()))
        options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("acceso", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "desconocido",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddSignalR();
builder.Services.Configure<CircuitosOpciones>(builder.Configuration.GetSection("Circuitos"));
builder.Services.AddSingleton<SucursalCircuitos>();
builder.Services.AddSingleton(sp => NpgsqlDataSource.Create(
    sp.GetRequiredService<IConfiguration>().GetConnectionString("Pulperia")
    ?? throw new InvalidOperationException("Configure ConnectionStrings:Pulperia.")));
builder.Services.AddScoped<OperacionesService>();
builder.Services.AddScoped<ConsultasService>();
builder.Services.AddScoped<VencimientosService>();
builder.Services.AddHostedService<ProcesosBackground>();
builder.Services.AddControllersWithViews(options => options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddDbContext<PulperiaDbContext>(options =>
{
    var connection = builder.Configuration.GetConnectionString("Pulperia");
    if (string.IsNullOrWhiteSpace(connection))
        throw new InvalidOperationException("Configure ConnectionStrings:Pulperia usando scripts/configurar-conexion.ps1.");
    options.UseNpgsql(connection);
});
var app = builder.Build();
app.UseRequestLocalization("en-US");
app.UseExceptionHandler("/Home/Error");
if (!app.Environment.IsDevelopment()) { app.UseHsts(); app.UseHttpsRedirection(); }
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets().AllowAnonymous();
app.MapHub<SucursalesHub>("/hubs/sucursales");
app.MapGet("/health", () => Results.Ok(new { estado = "Disponible" })).AllowAnonymous();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}").WithStaticAssets();
app.Run();
public partial class Program;
