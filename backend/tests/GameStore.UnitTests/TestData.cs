using GameStore.Core.Abstractions;
using GameStore.Core.Domain;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace GameStore.UnitTests;

internal static class TestData
{
    public static readonly Guid StoreId = SeedData.DefaultStoreId;

    // 2026-10-22 12:00 in Los Angeles (PDT, UTC-7).
    public static readonly DateTimeOffset Now = new(2026, 10, 22, 19, 0, 0, TimeSpan.Zero);
    public static readonly DateOnly Today = new(2026, 10, 22);

    public static FakeTimeProvider Clock() => new(Now);

    public static IStoreRepository Stores()
    {
        var stores = Substitute.For<IStoreRepository>();
        stores.GetAsync(StoreId, Arg.Any<CancellationToken>()).Returns(SeedData.Store());
        return stores;
    }

    public static Event Event(DateOnly? date = null, TimeOnly? time = null, short capacity = 30, string name = "Friday Night Magic") => new()
    {
        EventId = Guid.CreateVersion7(),
        StoreId = StoreId,
        GameType = "Magic: The Gathering",
        Name = name,
        Date = date ?? Today.AddDays(7),
        Time = time ?? new TimeOnly(18, 0),
        Duration = TimeSpan.FromHours(3),
        Capacity = capacity,
        EventMinimum = 3,
        PlayFormats = "1v1 constructed",
        Details = "3 rounds best of 3",
    };
}
