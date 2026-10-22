using GameStore.Core.Abstractions;
using GameStore.Core.Contracts;
using GameStore.Core.Domain;
using GameStore.Core.Errors;
using GameStore.Core.Validation;

namespace GameStore.Core.Services;

public sealed class EventService(
    IStoreRepository stores,
    IEventRepository events,
    IRegistrationRepository registrations,
    ICapacityRepository capacities,
    TimeProvider clock) : IEventService
{
    public async Task<IReadOnlyList<EventDto>> ListUpcomingAsync(Guid storeId, CancellationToken ct = default)
    {
        var store = await StoreService.GetRequiredAsync(stores, storeId, ct);
        var zone = StoreTime.FindZone(store.TimeZone);
        var now = clock.GetUtcNow();

        var upcoming = (await events.ListByStoreAsync(storeId, ct))
            .Where(e => StoreTime.IsUpcoming(e.Date, e.Time, zone, now))
            .OrderBy(e => e.Date)
            .ThenBy(e => e.Time)
            .ToList();

        var counts = await registrations.CountByEventAsync(upcoming.Select(e => e.EventId).ToList(), ct);
        return upcoming.Select(e => ToDto(e, counts.GetValueOrDefault(e.EventId))).ToList();
    }

    public async Task<EventDto> GetUpcomingAsync(Guid eventId, CancellationToken ct = default)
    {
        var evt = await GetUpcomingEventAsync(stores, events, clock, eventId, ct);
        var counts = await registrations.CountByEventAsync([eventId], ct);
        return ToDto(evt, counts.GetValueOrDefault(eventId));
    }

    public async Task<CapacityDto> GetCapacityAsync(Guid eventId, CancellationToken ct = default)
    {
        await GetUpcomingEventAsync(stores, events, clock, eventId, ct);
        var capacity = await capacities.GetAsync(eventId, ct)
            ?? throw new NotFoundException($"Event {eventId} was not found.");
        return new CapacityDto(capacity.RemainingCapacity, capacity.VersionId);
    }

    public async Task<CalendarFileDto> GetCalendarFileAsync(Guid eventId, CancellationToken ct = default)
    {
        var evt = await GetUpcomingEventAsync(stores, events, clock, eventId, ct);
        var store = await StoreService.GetRequiredAsync(stores, evt.StoreId, ct);
        return new CalendarFileDto(CalendarFile.FileName(evt), CalendarFile.Build(evt, store, clock.GetUtcNow()));
    }

    public async Task<EventDto> CreateAsync(Guid storeId, CreateEventRequest request, CancellationToken ct = default)
    {
        var store = await StoreService.GetRequiredAsync(stores, storeId, ct);
        var zone = StoreTime.FindZone(store.TimeZone);
        var now = clock.GetUtcNow();

        Validators.ThrowIfAny(Validators.ValidateEvent(request, (d, t) => !StoreTime.IsUpcoming(d, t, zone, now)));

        var evt = new Event
        {
            EventId = Guid.CreateVersion7(),
            StoreId = storeId,
            GameType = request.GameType!.Trim(),
            Name = request.Name!.Trim(),
            Date = request.Date!.Value,
            Time = request.Time!.Value,
            Duration = TimeSpan.FromMinutes(request.Duration!.Value),
            Capacity = (short)request.Capacity!.Value,
            EventMinimum = (short)request.EventMinimum!.Value,
            PlayFormats = request.PlayFormats?.Trim() ?? "",
            Details = request.Details?.Trim() ?? "",
        };
        var capacity = new Capacity { EventId = evt.EventId, RemainingCapacity = evt.Capacity, VersionId = 1 };

        await events.AddAsync(evt, capacity, ct);
        return ToDto(evt, 0);
    }

    /// <summary>The event, if it exists and hasn't started yet; otherwise NotFoundException.</summary>
    internal static async Task<Event> GetUpcomingEventAsync(
        IStoreRepository stores, IEventRepository events, TimeProvider clock, Guid eventId, CancellationToken ct)
    {
        var evt = await events.GetAsync(eventId, ct);
        if (evt is not null)
        {
            var store = await stores.GetAsync(evt.StoreId, ct);
            if (store is not null && StoreTime.IsUpcoming(evt.Date, evt.Time, StoreTime.FindZone(store.TimeZone), clock.GetUtcNow()))
                return evt;
        }
        throw new NotFoundException($"Event {eventId} was not found.");
    }

    internal static EventDto ToDto(Event e, int registeredCount) => new(
        e.EventId, e.GameType, e.Date, e.Time, e.Name, e.Capacity, e.EventMinimum,
        (int)e.Duration.TotalMinutes, e.PlayFormats, e.Details, registeredCount);
}
