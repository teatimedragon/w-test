using GameStore.Core.Abstractions;
using GameStore.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameStore.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGameStoreInMemoryData(this IServiceCollection services, string databaseName = "GameStore")
    {
        services.AddDbContextFactory<GameStoreDbContext>(o => o.UseInMemoryDatabase(databaseName));
        services.AddSingleton<CapacityWriteLock>();
        services.AddSingleton<IStoreRepository, StoreRepository>();
        services.AddSingleton<IEventRepository, EventRepository>();
        services.AddSingleton<IRegistrationRepository, RegistrationRepository>();
        services.AddSingleton<ICapacityRepository, CapacityRepository>();
        services.AddSingleton<ITemplateRepository, TemplateRepository>();
        return services;
    }

    /// <summary>Loads the seed data from section 3 into an empty database.</summary>
    public static async Task SeedGameStoreAsync(this IServiceProvider services)
    {
        var factory = services.GetRequiredService<IDbContextFactory<GameStoreDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        if (await db.Stores.AnyAsync()) return;
        db.Stores.Add(SeedData.Store());
        db.Templates.AddRange(SeedData.Templates());
        await db.SaveChangesAsync();
    }
}
