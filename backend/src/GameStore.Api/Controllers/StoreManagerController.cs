using GameStore.Core.Contracts;
using GameStore.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace GameStore.Api.Controllers;

[ApiController]
[Route("store-manager")]
public sealed class StoreManagerController(IStoreService stores) : ControllerBase
{
    [HttpGet("stores/{storeId:guid}")]
    public Task<StoreDto> GetStore(Guid storeId, CancellationToken ct) => stores.GetStoreAsync(storeId, ct);
}
