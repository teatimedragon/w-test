using System.Net.Http.Json;
using GameStore.Core.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace GameStore.IntegrationTests;

/// <summary>The API hosted in-process, with its own in-memory database and a controllable clock.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    // 2026-10-22 12:00 in Los Angeles (PDT, UTC-7).
    public static readonly DateTimeOffset Now = new(2026, 10, 22, 19, 0, 0, TimeSpan.Zero);
    public static readonly DateOnly Today = new(2026, 10, 22);
    public static readonly Guid StoreId = SeedData.DefaultStoreId;

    public FakeTimeProvider Clock { get; } = new(Now);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:Name", $"GameStore-{Guid.NewGuid()}");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }
}

internal static class Api
{
    public static string Events(Guid? storeId = null) => $"/event-manager/stores/{storeId ?? ApiFactory.StoreId}/events";
    public static string Templates(Guid? storeId = null) => $"/event-manager/stores/{storeId ?? ApiFactory.StoreId}/templates";
    public const string Register = "/event-manager/register";

    public static object NewEvent(string name = "Friday Night Magic", int capacity = 30, int daysFromToday = 7, string time = "18:00") => new
    {
        gameType = "Magic: The Gathering",
        date = ApiFactory.Today.AddDays(daysFromToday).ToString("yyyy-MM-dd"),
        time,
        name,
        capacity,
        eventMinimum = 1,
        duration = 180,
        playFormats = "1v1 constructed",
        details = "3 rounds best of 3",
    };

    public static async Task<EventJson> CreateEventAsync(this HttpClient client, object? body = null)
    {
        var response = await client.PostAsJsonAsync(Events(), body ?? NewEvent());
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EventJson>())!;
    }
}

public sealed record EventJson(
    Guid EventId, string GameType, string Date, string Time, string Name, int Capacity, int EventMinimum,
    int Duration, string PlayFormats, string Details, int RegisteredCount);

public sealed record CapacityJson(int RemainingCapacity, int VersionId);

public sealed record AddJson(string Name, Guid RegistrationId, int VersionId, int RemainingCapacity);

public sealed record RemoveJson(Guid RegistrationId, int VersionId, int RemainingCapacity);

public sealed record ConflictJson(int Status, string Reason, int VersionId, int RemainingCapacity);

public sealed record TemplateJson(
    int TemplateId, string GameType, string Name, int DefaultCapacity, int DefaultEventMinimum,
    int DefaultDuration, string DefaultPlayFormats, string DefaultDetails);

public sealed record ValidationJson(int Status, Dictionary<string, string[]> Errors);
