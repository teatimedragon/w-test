using GameStore.Core.Abstractions;
using GameStore.Core.Contracts;
using GameStore.Core.Domain;
using GameStore.Core.Errors;
using GameStore.Core.Validation;

namespace GameStore.Core.Services;

/// <summary>
/// ADD and REMOVE with optimistic concurrency on the event's Capacity row: a write succeeds only
/// if the caller's versionId matches the current one.
/// </summary>
public sealed class RegistrationService(
    IStoreRepository stores,
    IEventRepository events,
    IRegistrationRepository registrations,
    ICapacityRepository capacities,
    TimeProvider clock) : IRegistrationService
{
    public async Task<RegisterResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        Validators.ThrowIfAny(Validators.ValidateRegister(request));
        var eventId = request.EventId!.Value;
        var versionId = request.VersionId!.Value;

        await EventService.GetUpcomingEventAsync(stores, events, clock, eventId, ct);
        var capacity = await GetCapacityAsync(eventId, ct);

        return request.Action == RegisterActions.Add
            ? await AddAsync(eventId, versionId, request.Name!.Trim(), capacity, ct)
            : await RemoveAsync(eventId, versionId, request.RegistrationId!.Value, capacity, ct);
    }

    private async Task<RegisterResult> AddAsync(Guid eventId, int versionId, string name, Capacity capacity, CancellationToken ct)
    {
        // EVENT_FULL takes precedence over a stale version: retrying can't help when no seats are left.
        if (capacity.RemainingCapacity <= 0) return Conflict(ConflictReasons.EventFull, capacity);
        if (capacity.VersionId != versionId) return Conflict(ConflictReasons.VersionConflict, capacity);

        var registration = new Registration { RegistrationId = Guid.CreateVersion7(), EventId = eventId, Name = name };
        var change = new CapacityChange(
            eventId,
            ExpectedVersionId: capacity.VersionId,
            NewVersionId: Capacity.NextVersion(capacity.VersionId),
            NewRemainingCapacity: (short)(capacity.RemainingCapacity - 1),
            AddRegistration: registration);

        if (!await capacities.TryApplyAsync(change, ct))
            return await ConflictAfterLostRaceAsync(eventId, checkFull: true, ct);

        return new RegisterAdded(name, registration.RegistrationId, change.NewVersionId, change.NewRemainingCapacity);
    }

    private async Task<RegisterResult> RemoveAsync(Guid eventId, int versionId, Guid registrationId, Capacity capacity, CancellationToken ct)
    {
        var registration = await registrations.GetAsync(registrationId, ct);
        if (registration is null || registration.EventId != eventId)
            throw new NotFoundException($"Registration {registrationId} was not found for event {eventId}.");

        if (capacity.VersionId != versionId) return Conflict(ConflictReasons.VersionConflict, capacity);

        var change = new CapacityChange(
            eventId,
            ExpectedVersionId: capacity.VersionId,
            NewVersionId: Capacity.NextVersion(capacity.VersionId),
            NewRemainingCapacity: (short)(capacity.RemainingCapacity + 1),
            RemoveRegistrationId: registrationId);

        if (!await capacities.TryApplyAsync(change, ct))
            return await ConflictAfterLostRaceAsync(eventId, checkFull: false, ct);

        return new RegisterRemoved(registrationId, change.NewVersionId, change.NewRemainingCapacity);
    }

    /// <summary>Another write landed between our read and our write; report the state it left behind.</summary>
    private async Task<RegisterResult> ConflictAfterLostRaceAsync(Guid eventId, bool checkFull, CancellationToken ct)
    {
        var current = await GetCapacityAsync(eventId, ct);
        var reason = checkFull && current.RemainingCapacity <= 0 ? ConflictReasons.EventFull : ConflictReasons.VersionConflict;
        return Conflict(reason, current);
    }

    private async Task<Capacity> GetCapacityAsync(Guid eventId, CancellationToken ct) =>
        await capacities.GetAsync(eventId, ct) ?? throw new NotFoundException($"Event {eventId} was not found.");

    private static RegisterConflict Conflict(string reason, Capacity capacity) =>
        new(reason, capacity.VersionId, capacity.RemainingCapacity);
}
