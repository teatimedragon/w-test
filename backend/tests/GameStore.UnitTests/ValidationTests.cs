using GameStore.Core.Contracts;
using GameStore.Core.Validation;

namespace GameStore.UnitTests;

public class ValidationTests
{
    private static RegisterRequest Add(string? name) => new(Guid.CreateVersion7(), RegisterActions.Add, 1, name, null);

    // Player name: required, trimmed, 1–100 characters.

    [Fact]
    public void Player_name_of_1_character_is_valid() => Assert.Empty(Validators.ValidateRegister(Add("A")));

    [Fact]
    public void Player_name_of_100_characters_is_valid() => Assert.Empty(Validators.ValidateRegister(Add(new string('a', 100))));

    [Fact]
    public void Player_name_is_measured_after_trimming() =>
        Assert.Empty(Validators.ValidateRegister(Add("  " + new string('a', 100) + "  ")));

    [Fact]
    public void Player_name_over_100_characters_is_invalid() =>
        Assert.Contains("name", Validators.ValidateRegister(Add(new string('a', 101))).Keys);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public void Player_name_is_required(string? name) => Assert.Contains("name", Validators.ValidateRegister(Add(name)).Keys);

    [Fact]
    public void Register_requires_a_known_action() =>
        Assert.Contains("action", Validators.ValidateRegister(new RegisterRequest(Guid.CreateVersion7(), "UPSERT", 1, "A", null)).Keys);

    [Fact]
    public void Register_requires_versionId_and_eventId()
    {
        var errors = Validators.ValidateRegister(new RegisterRequest(null, RegisterActions.Add, null, "A", null));
        Assert.Contains("versionId", errors.Keys);
        Assert.Contains("eventId", errors.Keys);
    }

    [Fact]
    public void Remove_requires_registrationId() =>
        Assert.Contains("registrationId",
            Validators.ValidateRegister(new RegisterRequest(Guid.CreateVersion7(), RegisterActions.Remove, 1, null, null)).Keys);

    // Event: capacity > 0; 0 < eventMinimum ≤ capacity; date/time not in the past; duration > 0; text within column lengths.

    private static CreateEventRequest ValidEvent() => new(
        "Magic: The Gathering", new DateOnly(2030, 1, 1), new TimeOnly(18, 0), "FNM", 30, 3, 180, "1v1", "3 rounds");

    private static IReadOnlyDictionary<string, string[]> ValidateEvent(CreateEventRequest r, bool inPast = false) =>
        Validators.ValidateEvent(r, (_, _) => inPast);

    [Fact]
    public void Valid_event_has_no_errors() => Assert.Empty(ValidateEvent(ValidEvent()));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(32768)]
    public void Event_capacity_must_be_positive_and_fit_smallint(int capacity) =>
        Assert.Contains("capacity", ValidateEvent(ValidEvent() with { Capacity = capacity, EventMinimum = 1 }).Keys);

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(31)]
    public void Event_minimum_must_be_between_1_and_capacity(int minimum) =>
        Assert.Contains("eventMinimum", ValidateEvent(ValidEvent() with { Capacity = 30, EventMinimum = minimum }).Keys);

    [Fact]
    public void Event_minimum_equal_to_capacity_is_valid() =>
        Assert.Empty(ValidateEvent(ValidEvent() with { Capacity = 30, EventMinimum = 30 }));

    [Theory]
    [InlineData(0)]
    [InlineData(-60)]
    public void Event_duration_must_be_positive(int duration) =>
        Assert.Contains("duration", ValidateEvent(ValidEvent() with { Duration = duration }).Keys);

    [Fact]
    public void Event_date_and_time_must_not_be_in_the_past() =>
        Assert.Contains("date", ValidateEvent(ValidEvent(), inPast: true).Keys);

    [Fact]
    public void Event_date_and_time_are_required()
    {
        var errors = ValidateEvent(ValidEvent() with { Date = null, Time = null });
        Assert.Contains("date", errors.Keys);
        Assert.Contains("time", errors.Keys);
    }

    [Fact]
    public void Event_text_fields_must_fit_column_lengths()
    {
        var errors = ValidateEvent(ValidEvent() with
        {
            Name = new string('n', 251),
            GameType = new string('g', 101),
            PlayFormats = new string('p', 251),
            Details = new string('d', 251),
        });
        Assert.Equal(["details", "gameType", "name", "playFormats"], errors.Keys.Order());
    }

    [Fact]
    public void Event_text_fields_at_column_lengths_are_valid() =>
        Assert.Empty(ValidateEvent(ValidEvent() with
        {
            Name = new string('n', 250),
            GameType = new string('g', 100),
            PlayFormats = new string('p', 250),
            Details = new string('d', 250),
        }));

    [Fact]
    public void Event_name_and_gameType_are_required()
    {
        var errors = ValidateEvent(ValidEvent() with { Name = " ", GameType = null });
        Assert.Contains("name", errors.Keys);
        Assert.Contains("gameType", errors.Keys);
    }

    // Template: same numeric rules as events; name required.

    private static TemplateDto ValidTemplate() => new(null, "Magic: The Gathering", "Draft", 30, 3, 180, "1v1", "3 rounds");

    [Fact]
    public void Valid_template_has_no_errors() => Assert.Empty(Validators.ValidateTemplate(ValidTemplate()));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Template_name_is_required(string? name) =>
        Assert.Contains("name", Validators.ValidateTemplate(ValidTemplate() with { Name = name }).Keys);

    [Fact]
    public void Template_name_must_fit_100_characters() =>
        Assert.Contains("name", Validators.ValidateTemplate(ValidTemplate() with { Name = new string('t', 101) }).Keys);

    [Fact]
    public void Template_numeric_rules_match_events()
    {
        Assert.Contains("defaultCapacity", Validators.ValidateTemplate(ValidTemplate() with { DefaultCapacity = 0 }).Keys);
        Assert.Contains("defaultEventMinimum", Validators.ValidateTemplate(ValidTemplate() with { DefaultEventMinimum = 0 }).Keys);
        Assert.Contains("defaultEventMinimum", Validators.ValidateTemplate(ValidTemplate() with { DefaultCapacity = 5, DefaultEventMinimum = 6 }).Keys);
        Assert.Contains("defaultDuration", Validators.ValidateTemplate(ValidTemplate() with { DefaultDuration = 0 }).Keys);
    }
}
