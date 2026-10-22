using System.Net;
using System.Net.Http.Json;

namespace GameStore.IntegrationTests;

public class ConcurrencyTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    /// <summary>
    /// Many players register for a small event at once, each following the client flow from section 4:
    /// read capacity, send ADD, and retry with the returned versionId on VERSION_CONFLICT (up to 3 attempts).
    /// </summary>
    [Fact]
    public async Task Concurrent_registrations_never_oversell_seats()
    {
        const int seats = 10;
        const int players = 60;
        var client = _factory.CreateClient();
        var evt = await client.CreateEventAsync(Api.NewEvent(capacity: seats));

        var outcomes = await Task.WhenAll(Enumerable.Range(0, players).Select(i => Task.Run(async () =>
        {
            var http = _factory.CreateClient();
            var capacity = await http.GetFromJsonAsync<CapacityJson>($"/event-manager/events/{evt.EventId}/capacity");
            var versionId = capacity!.VersionId;
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                var response = await http.PostAsJsonAsync(Api.Register, new { eventId = evt.EventId, action = "ADD", versionId, name = $"Player {i}" });
                if (response.StatusCode == HttpStatusCode.OK) return "registered";
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                var conflict = (await response.Content.ReadFromJsonAsync<ConflictJson>())!;
                if (conflict.Reason == "EVENT_FULL") return "full";
                versionId = conflict.VersionId;
            }
            return "gave up";
        })));

        var registered = outcomes.Count(o => o == "registered");
        Assert.True(registered <= seats, $"{registered} registrations for {seats} seats");

        var listed = await client.GetFromJsonAsync<EventJson>($"/event-manager/events/{evt.EventId}");
        var final = await client.GetFromJsonAsync<CapacityJson>($"/event-manager/events/{evt.EventId}/capacity");
        Assert.Equal(registered, listed!.RegisteredCount);
        Assert.Equal(seats - registered, final!.RemainingCapacity);
        Assert.Equal(1 + registered, final.VersionId);
    }

    [Fact]
    public async Task Concurrent_registrations_with_the_same_versionId_let_exactly_one_through()
    {
        var client = _factory.CreateClient();
        var evt = await client.CreateEventAsync(Api.NewEvent(capacity: 30));

        var statuses = await Task.WhenAll(Enumerable.Range(0, 20).Select(i => Task.Run(async () =>
            (await _factory.CreateClient().PostAsJsonAsync(Api.Register,
                new { eventId = evt.EventId, action = "ADD", versionId = 1, name = $"Player {i}" })).StatusCode)));

        Assert.Equal(1, statuses.Count(s => s == HttpStatusCode.OK));
        Assert.Equal(19, statuses.Count(s => s == HttpStatusCode.Conflict));
    }
}
