using GameStore.Core.Abstractions;
using GameStore.Core.Contracts;
using GameStore.Core.Domain;
using GameStore.Core.Errors;

namespace GameStore.Core.Services;

public sealed class StoreService(IStoreRepository stores) : IStoreService
{
    public async Task<StoreDto> GetStoreAsync(Guid storeId, CancellationToken ct = default)
    {
        var store = await GetRequiredAsync(stores, storeId, ct);
        return new StoreDto(store.StoreId, store.StoreName, store.TimeZone);
    }

    internal static async Task<Store> GetRequiredAsync(IStoreRepository stores, Guid storeId, CancellationToken ct) =>
        await stores.GetAsync(storeId, ct) ?? throw new NotFoundException($"Store {storeId} was not found.");
}
