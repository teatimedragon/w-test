using GameStore.Core.Contracts;

namespace GameStore.Core.Services;

public interface IStoreService
{
    Task<StoreDto> GetStoreAsync(Guid storeId, CancellationToken ct = default);
}

public interface IEventService
{
    /// <summary>Upcoming events only, sorted by date then start time, each with its registeredCount.</summary>
    Task<IReadOnlyList<EventDto>> ListUpcomingAsync(Guid storeId, CancellationToken ct = default);
    /// <summary>Throws NotFoundException when the event doesn't exist or has already started.</summary>
    Task<EventDto> GetUpcomingAsync(Guid eventId, CancellationToken ct = default);
    Task<CapacityDto> GetCapacityAsync(Guid eventId, CancellationToken ct = default);
    Task<EventDto> CreateAsync(Guid storeId, CreateEventRequest request, CancellationToken ct = default);
}

public interface IRegistrationService
{
    Task<RegisterResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
}

public interface ITemplateService
{
    Task<IReadOnlyList<TemplateDto>> ListAsync(Guid storeId, CancellationToken ct = default);
    /// <summary>An existing templateId updates it; otherwise creates one with highest templateId + 1.</summary>
    Task<TemplateDto> UpsertAsync(Guid storeId, TemplateDto request, CancellationToken ct = default);
}
