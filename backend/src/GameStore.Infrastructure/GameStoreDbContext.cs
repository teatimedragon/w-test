using GameStore.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace GameStore.Infrastructure;

/// <summary>Application state, held in the EF Core in-memory database. Resets on restart.</summary>
public sealed class GameStoreDbContext(DbContextOptions<GameStoreDbContext> options) : DbContext(options)
{
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Registration> Registrations => Set<Registration>();
    public DbSet<Capacity> Capacities => Set<Capacity>();
    public DbSet<Template> Templates => Set<Template>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Store>(e =>
        {
            e.HasKey(x => x.StoreId);
            e.Property(x => x.StoreName).HasMaxLength(100);
        });

        model.Entity<Event>(e =>
        {
            e.HasKey(x => x.EventId);
            e.HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId);
            e.Property(x => x.GameType).HasMaxLength(100);
            e.Property(x => x.Name).HasMaxLength(250);
            e.Property(x => x.PlayFormats).HasMaxLength(250);
            e.Property(x => x.Details).HasMaxLength(250);
        });

        model.Entity<Registration>(e =>
        {
            e.HasKey(x => x.RegistrationId);
            e.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId);
            e.Property(x => x.Name).HasMaxLength(100);
        });

        model.Entity<Capacity>(e =>
        {
            e.HasKey(x => x.EventId);
            e.HasOne<Event>().WithOne().HasForeignKey<Capacity>(x => x.EventId);
            e.Property(x => x.VersionId).IsConcurrencyToken();
        });

        model.Entity<Template>(e =>
        {
            e.HasKey(x => x.TemplateId);
            e.Property(x => x.TemplateId).ValueGeneratedNever();
            e.HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId);
            e.Property(x => x.GameType).HasMaxLength(100);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.DefaultPlayFormats).HasMaxLength(250);
            e.Property(x => x.DefaultDetails).HasMaxLength(250);
        });
    }
}
