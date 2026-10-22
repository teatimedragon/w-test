using GameStore.Core.Contracts;
using GameStore.Core.Domain;
using GameStore.Core.Errors;
using GameStore.Core.Services;

namespace GameStore.UnitTests;

public class StoreServiceTests
{
    private readonly StoreService _service = new(TestData.Stores());

    [Fact]
    public async Task Store_details_include_name_and_time_zone() =>
        Assert.Equal(new StoreDto(SeedData.DefaultStoreId, "Basic Game Store", "America/Los_Angeles"),
            await _service.GetStoreAsync(SeedData.DefaultStoreId));

    [Fact]
    public async Task Unknown_store_is_not_found() =>
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetStoreAsync(Guid.CreateVersion7()));
}
