using Microsoft.EntityFrameworkCore;
using MiPulperia.Web.Models;

namespace MiPulperia.Web.Data;

public sealed class PulperiaDbContext(DbContextOptions<PulperiaDbContext> options) : DbContext(options)
{
    public DbSet<Sucursal> Sucursales => Set<Sucursal>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Existencia> Existencias => Set<Existencia>();
    public DbSet<ReservaConsulta> Reservas => Set<ReservaConsulta>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        Configure<Sucursal>(model, "mp_sucursales");
        Configure<Cliente>(model, "mp_clientes");
        Configure<Producto>(model, "mp_productos");
        model.Entity<Sucursal>().Property(x => x.Nombre).HasMaxLength(100);
        model.Entity<Producto>().Property(x => x.Atributos).HasColumnType("jsonb");
        model.Entity<Producto>().Property(x => x.Precio).HasPrecision(12, 2);
        model.Entity<Producto>().Property(x => x.HorasReserva).HasPrecision(12, 6);
        model.Entity<Existencia>().HasNoKey().ToView("mp_v_productos_existencias", "user_07");
        model.Entity<ReservaConsulta>().HasNoKey().ToView("mp_v_reservas_clientes", "user_07");
        model.Entity<Existencia>().Property(x => x.Atributos).HasColumnType("jsonb");
        foreach (var entity in model.Model.GetEntityTypes())
        foreach (var property in entity.GetProperties())
        {
            if (property.Name == nameof(Catalogo.Version)) { property.SetColumnName("xmin"); continue; }
            property.SetColumnName(System.Text.RegularExpressions.Regex.Replace(property.Name, "(?<=[a-z0-9])([A-Z])", "_$1").ToLowerInvariant());
        }
    }

    private static void Configure<T>(ModelBuilder model, string table) where T : Catalogo
    {
        model.Entity<T>().HasBaseType((Type?)null);
        model.Entity<T>().ToTable(table, "user_07");
        model.Entity<T>().HasKey(x => x.Id);
        model.Entity<T>().Property(x => x.Id).UseIdentityAlwaysColumn();
        model.Entity<T>().Property(x => x.Version).IsRowVersion();
    }
}
