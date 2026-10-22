using GameStore.Core.Domain;

namespace GameStore.Core.Abstractions;

public interface IStoreRepository
{
    Task<Store?> GetAsync(Guid storeId, CancellationToken ct = default);
}

public interface IEventRepository
{
    Task<IReadOnlyList<Event>> ListByStoreAsync(Guid storeId, CancellationToken ct = default);
    Task<Event?> GetAsync(Guid eventId, CancellationToken ct = default);
    /// <summary>Saves the event and its Capacity row together.</summary>
    Task AddAsync(Event evt, Capacity capacity, CancellationToken ct = default);
}

public interface IRegistrationRepository
{
    Task<Registration?> GetAsync(Guid registrationId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, int>> CountByEventAsync(IReadOnlyCollection<Guid> eventIds, CancellationToken ct = default);
}

/// <summary>
/// A registration change to apply to an event's Capacity row, only if the row's versionId
/// still equals <see cref="ExpectedVersionId"/> (compare-and-swap).
/// </summary>
public sealed record CapacityChange(
    Guid EventId,
    short ExpectedVersionId,
    short NewVersionId,
    short NewRemainingCapacity,
    Registration? AddRegistration = null,
    Guid? RemoveRegistrationId = null);

public interface ICapacityRepository
{
    Task<Capacity?> GetAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>
    /// Atomically applies <paramref name="change"/> if the stored versionId matches the expected one.
    /// Returns false, changing nothing, when it does not.
    /// </summary>
    Task<bool> TryApplyAsync(CapacityChange change, CancellationToken ct = default);
}

public interface ITemplateRepository
{
    Task<IReadOnlyList<Template>> ListByStoreAsync(Guid storeId, CancellationToken ct = default);
    Task<Template?> GetAsync(short templateId, CancellationToken ct = default);
    /// <summary>Highest templateId across all templates, or null when there are none.</summary>
    Task<short?> GetMaxTemplateIdAsync(CancellationToken ct = default);
    /// <summary>Adds the template. Returns false if its templateId is already taken.</summary>
    Task<bool> TryAddAsync(Template template, CancellationToken ct = default);
    Task UpdateAsync(Template template, CancellationToken ct = default);
}
