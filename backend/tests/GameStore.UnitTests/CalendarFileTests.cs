using GameStore.Core.Abstractions;
using GameStore.Core.Domain;
using GameStore.Core.Errors;
using GameStore.Core.Services;
using NSubstitute;

namespace GameStore.UnitTests;

public class CalendarFileTests
{
    private static string[] Lines(string ics) => ics.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void Calendar_file_has_event_name_store_location_and_UTC_start_and_end_from_the_store_time_zone()
    {
        // 18:00 for 3 h in Los Angeles on 2026-10-29 (PDT, UTC-7) is 01:00–04:00 UTC on 2026-10-30.
        var evt = TestData.Event(date: new DateOnly(2026, 10, 29), time: new TimeOnly(18, 0), name: "Friday Night Magic");

        var lines = Lines(CalendarFile.Build(evt, SeedData.Store(), TestData.Now));

        Assert.Equal("BEGIN:VCALENDAR", lines[0]);
        Assert.Equal("END:VCALENDAR", lines[^1]);
        Assert.Contains("SUMMARY:Friday Night Magic", lines);
        Assert.Contains("DTSTART:20261030T010000Z", lines);
        Assert.Contains("DTEND:20261030T040000Z", lines);
        Assert.Contains("LOCATION:Basic Game Store", lines);
        Assert.Contains($"UID:{evt.EventId}@game-store-events", lines);
        Assert.Contains("DTSTAMP:20261022T190000Z", lines);
        Assert.Contains(@"DESCRIPTION:Magic: The Gathering\n1v1 constructed\n3 rounds best of 3", lines);
    }

    [Fact]
    public void Calendar_file_uses_standard_time_offset_in_winter()
    {
        // 18:00 PST (UTC-8) on 2026-12-04 is 02:00 UTC on 2026-12-05.
        var evt = TestData.Event(date: new DateOnly(2026, 12, 4), time: new TimeOnly(18, 0));

        Assert.Contains("DTSTART:20261205T020000Z", Lines(CalendarFile.Build(evt, SeedData.Store(), TestData.Now)));
    }

    [Fact]
    public void Calendar_file_escapes_text_and_folds_long_lines()
    {
        var evt = TestData.Event(name: "Draft; Sealed, and \\ more " + new string('x', 80));

        var ics = CalendarFile.Build(evt, SeedData.Store(), TestData.Now);

        Assert.Contains(@"SUMMARY:Draft\; Sealed\, and \\ more ", ics);
        Assert.All(ics.Split("\r\n"), line => Assert.True(System.Text.Encoding.UTF8.GetByteCount(line) <= 75, line));
        Assert.Contains("\r\n x", ics);
    }

    [Theory]
    [InlineData("Friday Night Magic", "friday-night-magic.ics")]
    [InlineData("Magic: The Gathering — Commander!", "magic-the-gathering-commander.ics")]
    [InlineData("???", "event.ics")]
    public void Calendar_file_name_is_a_slug_of_the_event_name(string name, string expected) =>
        Assert.Equal(expected, CalendarFile.FileName(TestData.Event(name: name)));

    [Fact]
    public async Task Calendar_file_for_event_that_already_started_is_not_found()
    {
        var events = Substitute.For<IEventRepository>();
        var started = TestData.Event(date: TestData.Today, time: new TimeOnly(9, 0));
        events.GetAsync(started.EventId, Arg.Any<CancellationToken>()).Returns(started);
        var service = new EventService(TestData.Stores(), events, Substitute.For<IRegistrationRepository>(),
            Substitute.For<ICapacityRepository>(), TestData.Clock());

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetCalendarFileAsync(started.EventId));
    }
}
