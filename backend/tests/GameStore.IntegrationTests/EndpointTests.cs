using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace GameStore.IntegrationTests;

public class EndpointTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public EndpointTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    // GET /store-manager/stores/{storeId}

    [Fact]
    public async Task Get_store_returns_camelCase_store_details()
    {
        var json = await _client.GetStringAsync($"/store-manager/stores/{ApiFactory.StoreId}");

        Assert.Equal("""{"storeId":"0199f0a0-0000-7000-8000-000000000001","storeName":"Basic Game Store","timeZone":"America/Los_Angeles"}""", json);
    }

    [Fact]
    public async Task Get_unknown_store_returns_404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/store-manager/stores/{Guid.CreateVersion7()}")).StatusCode);

    // GET /event-manager/stores/{storeId}/events

    [Fact]
    public async Task List_events_returns_upcoming_events_sorted_with_registeredCount()
    {
        var later = await _client.CreateEventAsync(Api.NewEvent("later", daysFromToday: 2));
        var sooner = await _client.CreateEventAsync(Api.NewEvent("sooner", daysFromToday: 1));
        await _client.PostAsJsonAsync(Api.Register, new { eventId = sooner.EventId, action = "ADD", versionId = 1, name = "Alice" });

        var events = await _client.GetFromJsonAsync<List<EventJson>>(Api.Events());

        Assert.Equal(["sooner", "later"], events!.Select(e => e.Name));
        Assert.Equal([1, 0], events.Select(e => e.RegisteredCount));
        Assert.Equal(later.EventId, events[1].EventId);
    }

    [Fact]
    public async Task List_events_hides_events_once_they_start()
    {
        await _client.CreateEventAsync(Api.NewEvent("today 13:00", daysFromToday: 0, time: "13:00"));

        Assert.Single((await _client.GetFromJsonAsync<List<EventJson>>(Api.Events()))!);
        _factory.Clock.Advance(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1));
        Assert.Empty((await _client.GetFromJsonAsync<List<EventJson>>(Api.Events()))!);
    }

    [Fact]
    public async Task List_events_for_unknown_store_returns_404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(Api.Events(Guid.CreateVersion7()))).StatusCode);

    // POST /event-manager/stores/{storeId}/events

    [Fact]
    public async Task Create_event_returns_201_with_the_created_event_and_initializes_capacity()
    {
        var response = await _client.PostAsJsonAsync(Api.Events(), Api.NewEvent("FNM", capacity: 12));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<EventJson>())!;
        Assert.Equal(new EventJson(created.EventId, "Magic: The Gathering", "2026-10-29", "18:00", "FNM", 12, 1, 180,
            "1v1 constructed", "3 rounds best of 3", 0), created);
        Assert.Equal($"/event-manager/events/{created.EventId}", response.Headers.Location!.AbsolutePath);

        var capacity = await _client.GetFromJsonAsync<CapacityJson>($"/event-manager/events/{created.EventId}/capacity");
        Assert.Equal(new CapacityJson(12, 1), capacity);
    }

    [Fact]
    public async Task Create_event_with_invalid_fields_returns_400_with_field_errors()
    {
        var response = await _client.PostAsJsonAsync(Api.Events(), new
        {
            gameType = "MTG", date = "2026-10-22", time = "11:00", name = "", capacity = 5, eventMinimum = 6, duration = 0,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = (await response.Content.ReadFromJsonAsync<ValidationJson>())!;
        Assert.Equal(["date", "duration", "eventMinimum", "name"], problem.Errors.Keys.Order());
    }

    [Fact]
    public async Task Create_event_for_unknown_store_returns_404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsJsonAsync(Api.Events(Guid.CreateVersion7()), Api.NewEvent())).StatusCode);

    // GET /event-manager/events/{eventId}

    [Fact]
    public async Task Get_event_returns_the_event()
    {
        var created = await _client.CreateEventAsync();

        Assert.Equal(created, await _client.GetFromJsonAsync<EventJson>($"/event-manager/events/{created.EventId}"));
    }

    [Fact]
    public async Task Get_unknown_event_returns_404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/event-manager/events/{Guid.CreateVersion7()}")).StatusCode);

    [Fact]
    public async Task Get_event_that_has_started_returns_404()
    {
        var created = await _client.CreateEventAsync(Api.NewEvent(daysFromToday: 0, time: "12:30"));
        _factory.Clock.Advance(TimeSpan.FromMinutes(31));

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/event-manager/events/{created.EventId}")).StatusCode);
    }

    // GET /event-manager/events/{eventId}/capacity

    [Fact]
    public async Task Get_capacity_for_unknown_event_returns_404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/event-manager/events/{Guid.CreateVersion7()}/capacity")).StatusCode);

    // GET /event-manager/events/{eventId}/calendar.ics

    [Fact]
    public async Task Get_calendar_file_downloads_an_ics_with_name_times_and_store_location()
    {
        var created = await _client.CreateEventAsync(Api.NewEvent("Friday Night Magic", daysFromToday: 7, time: "18:00"));

        var response = await _client.GetAsync($"/event-manager/events/{created.EventId}/calendar.ics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/calendar", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("friday-night-magic.ics", response.Content.Headers.ContentDisposition!.FileName);
        var ics = await response.Content.ReadAsStringAsync();
        Assert.Contains("SUMMARY:Friday Night Magic\r\n", ics);
        Assert.Contains("DTSTART:20261030T010000Z\r\n", ics); // 2026-10-29 18:00 PDT
        Assert.Contains("DTEND:20261030T040000Z\r\n", ics);
        Assert.Contains("LOCATION:Basic Game Store\r\n", ics);
    }

    [Fact]
    public async Task Get_calendar_file_for_unknown_event_returns_404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/event-manager/events/{Guid.CreateVersion7()}/calendar.ics")).StatusCode);

    // POST /event-manager/register

    [Fact]
    public async Task Register_ADD_returns_200_with_registration_and_new_capacity()
    {
        var evt = await _client.CreateEventAsync(Api.NewEvent(capacity: 3));

        var response = await _client.PostAsJsonAsync(Api.Register, new { eventId = evt.EventId, action = "ADD", versionId = 1, name = " Alice " });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var added = (await response.Content.ReadFromJsonAsync<AddJson>())!;
        Assert.Equal(("Alice", 2, 2), (added.Name, added.VersionId, added.RemainingCapacity));
        Assert.Equal(7, added.RegistrationId.Version);
    }

    [Fact]
    public async Task Register_REMOVE_returns_200_and_frees_the_seat()
    {
        var evt = await _client.CreateEventAsync(Api.NewEvent(capacity: 3));
        var added = await (await _client.PostAsJsonAsync(Api.Register, new { eventId = evt.EventId, action = "ADD", versionId = 1, name = "Alice" }))
            .Content.ReadFromJsonAsync<AddJson>();

        var response = await _client.PostAsJsonAsync(Api.Register,
            new { eventId = evt.EventId, action = "REMOVE", versionId = added!.VersionId, registrationId = added.RegistrationId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new RemoveJson(added.RegistrationId, 3, 3), await response.Content.ReadFromJsonAsync<RemoveJson>());
        var listed = await _client.GetFromJsonAsync<EventJson>($"/event-manager/events/{evt.EventId}");
        Assert.Equal(0, listed!.RegisteredCount);
    }

    [Fact]
    public async Task Register_with_stale_versionId_returns_409_problem_details_VERSION_CONFLICT()
    {
        var evt = await _client.CreateEventAsync(Api.NewEvent(capacity: 3));
        await _client.PostAsJsonAsync(Api.Register, new { eventId = evt.EventId, action = "ADD", versionId = 1, name = "Alice" });

        var response = await _client.PostAsJsonAsync(Api.Register, new { eventId = evt.EventId, action = "ADD", versionId = 1, name = "Bob" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(new ConflictJson(409, "VERSION_CONFLICT", 2, 2), await response.Content.ReadFromJsonAsync<ConflictJson>());
    }

    [Fact]
    public async Task Register_for_full_event_returns_409_problem_details_EVENT_FULL()
    {
        var evt = await _client.CreateEventAsync(Api.NewEvent(capacity: 1));
        await _client.PostAsJsonAsync(Api.Register, new { eventId = evt.EventId, action = "ADD", versionId = 1, name = "Alice" });

        var response = await _client.PostAsJsonAsync(Api.Register, new { eventId = evt.EventId, action = "ADD", versionId = 2, name = "Bob" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(new ConflictJson(409, "EVENT_FULL", 2, 0), await response.Content.ReadFromJsonAsync<ConflictJson>());
    }

    [Fact]
    public async Task Register_with_invalid_name_returns_400_with_field_errors()
    {
        var evt = await _client.CreateEventAsync();

        var response = await _client.PostAsJsonAsync(Api.Register, new { eventId = evt.EventId, action = "ADD", versionId = 1, name = new string('x', 101) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("name", (await response.Content.ReadFromJsonAsync<ValidationJson>())!.Errors.Keys);
    }

    [Fact]
    public async Task Register_for_unknown_event_returns_404()
    {
        var response = await _client.PostAsJsonAsync(Api.Register, new { eventId = Guid.CreateVersion7(), action = "ADD", versionId = 1, name = "Alice" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Register_REMOVE_for_unknown_registration_returns_404()
    {
        var evt = await _client.CreateEventAsync();

        var response = await _client.PostAsJsonAsync(Api.Register,
            new { eventId = evt.EventId, action = "REMOVE", versionId = 1, registrationId = Guid.CreateVersion7() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // GET /event-manager/stores/{storeId}/templates

    [Fact]
    public async Task List_templates_returns_the_four_seeded_templates()
    {
        var templates = await _client.GetFromJsonAsync<List<TemplateJson>>(Api.Templates());

        Assert.Equal([0, 1, 2, 3], templates!.Select(t => t.TemplateId));
        Assert.Equal(new TemplateJson(0, "Magic: The Gathering", "Default", 30, 3, 180, "1v1 constructed", "3 rounds best of 3"), templates[0]);
        Assert.Equal(new TemplateJson(3, "Galactic Empires CCG", "Galactic Empires", 30, 7, 240, "7v7",
            "winner takes all swiss final if more than one pod"), templates[3]);
    }

    [Fact]
    public async Task List_templates_for_unknown_store_returns_404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(Api.Templates(Guid.CreateVersion7()))).StatusCode);

    // PUT /event-manager/stores/{storeId}/templates

    [Fact]
    public async Task Put_template_without_templateId_creates_highest_plus_1()
    {
        var response = await _client.PutAsJsonAsync(Api.Templates(), new
        {
            gameType = "Pokémon TCG", name = "League", defaultCapacity = 16, defaultEventMinimum = 4, defaultDuration = 120,
            defaultPlayFormats = "Standard", defaultDetails = "Bring a deck",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new TemplateJson(4, "Pokémon TCG", "League", 16, 4, 120, "Standard", "Bring a deck"),
            await response.Content.ReadFromJsonAsync<TemplateJson>());
        Assert.Equal(5, (await _client.GetFromJsonAsync<List<TemplateJson>>(Api.Templates()))!.Count);
    }

    [Fact]
    public async Task Put_template_with_existing_templateId_updates_it()
    {
        var body = new TemplateJson(2, "Magic: The Gathering Commander", "Commander Night", 24, 4, 200, "pods of 4", "casual");

        var response = await _client.PutAsJsonAsync(Api.Templates(), body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(body, await response.Content.ReadFromJsonAsync<TemplateJson>());
        var templates = await _client.GetFromJsonAsync<List<TemplateJson>>(Api.Templates());
        Assert.Equal(4, templates!.Count);
        Assert.Equal(body, templates[2]);
    }

    [Fact]
    public async Task Put_template_with_invalid_fields_returns_400_with_field_errors()
    {
        var response = await _client.PutAsJsonAsync(Api.Templates(), new
        {
            gameType = "X", name = "", defaultCapacity = 0, defaultEventMinimum = 1, defaultDuration = 60,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["defaultCapacity", "name"], (await response.Content.ReadFromJsonAsync<ValidationJson>())!.Errors.Keys.Order());
    }

    [Fact]
    public async Task Put_template_for_unknown_store_returns_404()
    {
        var response = await _client.PutAsJsonAsync(Api.Templates(Guid.CreateVersion7()), new
        {
            gameType = "X", name = "Y", defaultCapacity = 2, defaultEventMinimum = 1, defaultDuration = 60,
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Malformed_json_returns_400()
    {
        var response = await _client.PostAsync(Api.Register, new StringContent("{not json", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
