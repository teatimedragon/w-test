using GameStore.Core.Abstractions;
using GameStore.Core.Contracts;
using GameStore.Core.Domain;
using GameStore.Core.Errors;
using GameStore.Core.Services;
using NSubstitute;

namespace GameStore.UnitTests;

public class RegistrationServiceTests
{
    private readonly IEventRepository _events = Substitute.For<IEventRepository>();
    private readonly IRegistrationRepository _registrations = Substitute.For<IRegistrationRepository>();
    private readonly ICapacityRepository _capacities = Substitute.For<ICapacityRepository>();
    private readonly Event _event = TestData.Event();
    private readonly RegistrationService _service;

    public RegistrationServiceTests()
    {
        _events.GetAsync(_event.EventId, Arg.Any<CancellationToken>()).Returns(_event);
        _capacities.TryApplyAsync(Arg.Any<CapacityChange>(), Arg.Any<CancellationToken>()).Returns(true);
        _service = new RegistrationService(TestData.Stores(), _events, _registrations, _capacities, TestData.Clock());
    }

    private void CapacityIs(short remaining, short version) =>
        _capacities.GetAsync(_event.EventId, Arg.Any<CancellationToken>())
            .Returns(new Capacity { EventId = _event.EventId, RemainingCapacity = remaining, VersionId = version });

    private RegisterRequest Add(int versionId, string? name = "Alice") => new(_event.EventId, RegisterActions.Add, versionId, name, null);

    private RegisterRequest Remove(int versionId, Guid registrationId) =>
        new(_event.EventId, RegisterActions.Remove, versionId, null, registrationId);

    private Registration ExistingRegistration(Guid? eventId = null)
    {
        var registration = new Registration { RegistrationId = Guid.CreateVersion7(), EventId = eventId ?? _event.EventId, Name = "Bob" };
        _registrations.GetAsync(registration.RegistrationId, Arg.Any<CancellationToken>()).Returns(registration);
        return registration;
    }

    [Fact]
    public async Task Add_succeeds_when_versionId_matches_and_seats_remain()
    {
        CapacityIs(remaining: 5, version: 3);

        var result = await _service.RegisterAsync(Add(3, "  Alice  "));

        var added = Assert.IsType<RegisterAdded>(result);
        Assert.Equal("Alice", added.Name);
        Assert.Equal(4, added.VersionId);
        Assert.Equal(4, added.RemainingCapacity);
        await _capacities.Received(1).TryApplyAsync(
            Arg.Is<CapacityChange>(c => c.ExpectedVersionId == 3 && c.NewVersionId == 4 && c.NewRemainingCapacity == 4
                && c.AddRegistration!.Name == "Alice" && c.AddRegistration.EventId == _event.EventId
                && c.AddRegistration.RegistrationId == added.RegistrationId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Add_with_stale_versionId_returns_VERSION_CONFLICT_with_current_versionId_and_remainingCapacity()
    {
        CapacityIs(remaining: 5, version: 7);

        var result = await _service.RegisterAsync(Add(6));

        Assert.Equal(new RegisterConflict(ConflictReasons.VersionConflict, 7, 5), result);
        await _capacities.DidNotReceive().TryApplyAsync(Arg.Any<CapacityChange>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Add_to_full_event_returns_EVENT_FULL()
    {
        CapacityIs(remaining: 0, version: 31);

        var result = await _service.RegisterAsync(Add(31));

        Assert.Equal(new RegisterConflict(ConflictReasons.EventFull, 31, 0), result);
        await _capacities.DidNotReceive().TryApplyAsync(Arg.Any<CapacityChange>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Add_to_full_event_returns_EVENT_FULL_even_when_versionId_is_also_stale()
    {
        CapacityIs(remaining: 0, version: 31);

        var result = await _service.RegisterAsync(Add(2));

        Assert.Equal(ConflictReasons.EventFull, Assert.IsType<RegisterConflict>(result).Reason);
    }

    [Fact]
    public async Task Add_that_loses_a_race_at_write_time_returns_VERSION_CONFLICT_with_fresh_state()
    {
        _capacities.GetAsync(_event.EventId, Arg.Any<CancellationToken>()).Returns(
            new Capacity { EventId = _event.EventId, RemainingCapacity = 5, VersionId = 3 },
            new Capacity { EventId = _event.EventId, RemainingCapacity = 4, VersionId = 4 });
        _capacities.TryApplyAsync(Arg.Any<CapacityChange>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await _service.RegisterAsync(Add(3));

        Assert.Equal(new RegisterConflict(ConflictReasons.VersionConflict, 4, 4), result);
    }

    [Fact]
    public async Task Add_that_loses_a_race_for_the_last_seat_returns_EVENT_FULL()
    {
        _capacities.GetAsync(_event.EventId, Arg.Any<CancellationToken>()).Returns(
            new Capacity { EventId = _event.EventId, RemainingCapacity = 1, VersionId = 3 },
            new Capacity { EventId = _event.EventId, RemainingCapacity = 0, VersionId = 4 });
        _capacities.TryApplyAsync(Arg.Any<CapacityChange>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await _service.RegisterAsync(Add(3));

        Assert.Equal(new RegisterConflict(ConflictReasons.EventFull, 4, 0), result);
    }

    [Fact]
    public async Task Remove_frees_a_seat_and_bumps_the_version()
    {
        var registration = ExistingRegistration();
        CapacityIs(remaining: 0, version: 31);

        var result = await _service.RegisterAsync(Remove(31, registration.RegistrationId));

        Assert.Equal(new RegisterRemoved(registration.RegistrationId, 32, 1), result);
        await _capacities.Received(1).TryApplyAsync(
            Arg.Is<CapacityChange>(c => c.RemoveRegistrationId == registration.RegistrationId && c.ExpectedVersionId == 31
                && c.NewVersionId == 32 && c.NewRemainingCapacity == 1 && c.AddRegistration == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Remove_with_stale_versionId_returns_VERSION_CONFLICT()
    {
        var registration = ExistingRegistration();
        CapacityIs(remaining: 3, version: 10);

        var result = await _service.RegisterAsync(Remove(9, registration.RegistrationId));

        Assert.Equal(new RegisterConflict(ConflictReasons.VersionConflict, 10, 3), result);
    }

    [Fact]
    public async Task Remove_of_unknown_registration_is_not_found()
    {
        CapacityIs(remaining: 3, version: 10);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.RegisterAsync(Remove(10, Guid.CreateVersion7())));
    }

    [Fact]
    public async Task Remove_of_registration_belonging_to_another_event_is_not_found()
    {
        var registration = ExistingRegistration(eventId: Guid.CreateVersion7());
        CapacityIs(remaining: 3, version: 10);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.RegisterAsync(Remove(10, registration.RegistrationId)));
    }

    [Fact]
    public async Task Register_for_unknown_event_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.RegisterAsync(new RegisterRequest(Guid.CreateVersion7(), RegisterActions.Add, 1, "Alice", null)));
    }

    [Fact]
    public async Task Register_for_event_that_already_started_is_not_found()
    {
        var started = TestData.Event(date: TestData.Today, time: new TimeOnly(11, 59));
        _events.GetAsync(started.EventId, Arg.Any<CancellationToken>()).Returns(started);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.RegisterAsync(new RegisterRequest(started.EventId, RegisterActions.Add, 1, "Alice", null)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    public async Task Add_rejects_missing_player_name(string? name)
    {
        CapacityIs(remaining: 5, version: 1);

        var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _service.RegisterAsync(Add(1, name)));
        Assert.Contains("name", ex.Errors.Keys);
    }

    [Fact]
    public async Task Version_wraps_to_1_after_smallint_max()
    {
        CapacityIs(remaining: 5, version: short.MaxValue);

        var result = await _service.RegisterAsync(Add(short.MaxValue));

        Assert.Equal(1, Assert.IsType<RegisterAdded>(result).VersionId);
    }
}
