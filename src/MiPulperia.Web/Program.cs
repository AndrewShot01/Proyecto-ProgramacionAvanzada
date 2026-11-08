using Microsoft.EntityFrameworkCore;
using MiPulperia.Web.Data;

var builder = WebApplication.CreateBuilder(args);
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
app.UseAuthorization();
app.MapStaticAssets();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}").WithStaticAssets();
app.Run();
public partial class Program;
