using GameStore.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GameStore.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGameStoreServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IStoreService, StoreService>();
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<ITemplateService, TemplateService>();
        return services;
    }
}
