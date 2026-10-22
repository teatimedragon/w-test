using GameStore.Core.Abstractions;
using GameStore.Core.Contracts;
using GameStore.Core.Domain;
using GameStore.Core.Errors;
using GameStore.Core.Services;
using NSubstitute;

namespace GameStore.UnitTests;

public class EventServiceTests
{
    private readonly IEventRepository _events = Substitute.For<IEventRepository>();
    private readonly IRegistrationRepository _registrations = Substitute.For<IRegistrationRepository>();
    private readonly ICapacityRepository _capacities = Substitute.For<ICapacityRepository>();
    private readonly EventService _service;

    public EventServiceTests()
    {
        _registrations.CountByEventAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, int>());
        _service = new EventService(TestData.Stores(), _events, _registrations, _capacities, TestData.Clock());
    }

    private void StoreHas(params Event[] events) =>
        _events.ListByStoreAsync(TestData.StoreId, Arg.Any<CancellationToken>()).Returns(events);

    [Fact]
    public async Task Upcoming_filter_excludes_past_events_using_the_store_time_zone()
    {
        // Now is 12:00 in Los Angeles but 19:00 UTC. An 18:00 event today is upcoming in LA even
        // though 18:00 UTC has already passed; an 11:00 event today has started.
        var laterToday = TestData.Event(date: TestData.Today, time: new TimeOnly(18, 0), name: "later today");
        var earlierToday = TestData.Event(date: TestData.Today, time: new TimeOnly(11, 0), name: "earlier today");
        var yesterday = TestData.Event(date: TestData.Today.AddDays(-1), name: "yesterday");
        StoreHas(laterToday, earlierToday, yesterday);

        var result = await _service.ListUpcomingAsync(TestData.StoreId);

        Assert.Equal(["later today"], result.Select(e => e.Name));
    }

    [Fact]
    public async Task Upcoming_includes_an_event_starting_exactly_now()
    {
        StoreHas(TestData.Event(date: TestData.Today, time: new TimeOnly(12, 0), name: "now"));

        var result = await _service.ListUpcomingAsync(TestData.StoreId);

        Assert.Equal(["now"], result.Select(e => e.Name));
    }

    [Fact]
    public async Task Event_list_is_sorted_by_date_then_start_time()
    {
        var day1Late = TestData.Event(date: TestData.Today.AddDays(1), time: new TimeOnly(19, 0), name: "d1 19:00");
        var day2 = TestData.Event(date: TestData.Today.AddDays(2), time: new TimeOnly(9, 0), name: "d2 09:00");
        var day1Early = TestData.Event(date: TestData.Today.AddDays(1), time: new TimeOnly(10, 0), name: "d1 10:00");
        StoreHas(day2, day1Late, day1Early);

        var result = await _service.ListUpcomingAsync(TestData.StoreId);

        Assert.Equal(["d1 10:00", "d1 19:00", "d2 09:00"], result.Select(e => e.Name));
    }

    [Fact]
    public async Task Event_list_includes_registeredCount_looked_up_per_event()
    {
        var a = TestData.Event(name: "a");
        var b = TestData.Event(date: TestData.Today.AddDays(8), name: "b");
        StoreHas(a, b);
        _registrations.CountByEventAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, int> { [a.EventId] = 12 });

        var result = await _service.ListUpcomingAsync(TestData.StoreId);

        Assert.Equal([12, 0], result.Select(e => e.RegisteredCount));
    }

    [Fact]
    public async Task Event_list_for_unknown_store_is_not_found() =>
        await Assert.ThrowsAsync<NotFoundException>(() => _service.ListUpcomingAsync(Guid.CreateVersion7()));

    [Fact]
    public async Task Get_event_that_already_started_is_not_found()
    {
        var started = TestData.Event(date: TestData.Today, time: new TimeOnly(9, 0));
        _events.GetAsync(started.EventId, Arg.Any<CancellationToken>()).Returns(started);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetUpcomingAsync(started.EventId));
    }

    [Fact]
    public async Task Get_capacity_returns_remainingCapacity_and_versionId()
    {
        var evt = TestData.Event();
        _events.GetAsync(evt.EventId, Arg.Any<CancellationToken>()).Returns(evt);
        _capacities.GetAsync(evt.EventId, Arg.Any<CancellationToken>())
            .Returns(new Capacity { EventId = evt.EventId, RemainingCapacity = 17, VersionId = 14 });

        Assert.Equal(new CapacityDto(17, 14), await _service.GetCapacityAsync(evt.EventId));
    }

    [Fact]
    public async Task Event_creation_from_template_defaults_initializes_the_Capacity_row()
    {
        // The admin wizard prefills a new event from a template; the values it submits are the template's defaults.
        var template = SeedData.Templates().Single(t => t.TemplateId == 2);
        var request = new CreateEventRequest(
            template.GameType, TestData.Today.AddDays(3), new TimeOnly(18, 30), template.Name,
            template.DefaultCapacity, template.DefaultEventMinimum, (int)template.DefaultDuration.TotalMinutes,
            template.DefaultPlayFormats, template.DefaultDetails);

        var created = await _service.CreateAsync(TestData.StoreId, request);

        Assert.Equal(7, created.EventId.Version);
        Assert.Equal(template.GameType, created.GameType);
        Assert.Equal(30, created.Capacity);
        Assert.Equal(6, created.EventMinimum);
        Assert.Equal(240, created.Duration);
        Assert.Equal("pods", created.PlayFormats);
        Assert.Equal("swiss pairing", created.Details);
        Assert.Equal(0, created.RegisteredCount);
        await _events.Received(1).AddAsync(
            Arg.Is<Event>(e => e.EventId == created.EventId && e.StoreId == TestData.StoreId && e.Duration == TimeSpan.FromHours(4)),
            Arg.Is<Capacity>(c => c.EventId == created.EventId && c.RemainingCapacity == 30 && c.VersionId == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Event_creation_rejects_a_start_in_the_past_in_the_store_time_zone()
    {
        // 11:30 today is in the past in Los Angeles (now is 12:00 there).
        var request = new CreateEventRequest("MTG", TestData.Today, new TimeOnly(11, 30), "Too late", 10, 2, 60, null, null);

        var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _service.CreateAsync(TestData.StoreId, request));
        Assert.Contains("date", ex.Errors.Keys);
        await _events.DidNotReceive().AddAsync(Arg.Any<Event>(), Arg.Any<Capacity>(), Arg.Any<CancellationToken>());
    }
}
