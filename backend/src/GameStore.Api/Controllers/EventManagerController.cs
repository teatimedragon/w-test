using GameStore.Core.Contracts;
using GameStore.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace GameStore.Api.Controllers;

[ApiController]
[Route("event-manager")]
public sealed class EventManagerController(
    IEventService events,
    IRegistrationService registrations,
    ITemplateService templates) : ControllerBase
{
    [HttpGet("stores/{storeId:guid}/events")]
    public Task<IReadOnlyList<EventDto>> ListEvents(Guid storeId, CancellationToken ct) => events.ListUpcomingAsync(storeId, ct);

    [HttpPost("stores/{storeId:guid}/events")]
    public async Task<ActionResult<EventDto>> CreateEvent(Guid storeId, CreateEventRequest request, CancellationToken ct)
    {
        var created = await events.CreateAsync(storeId, request, ct);
        return CreatedAtAction(nameof(GetEvent), new { eventId = created.EventId }, created);
    }

    [HttpGet("events/{eventId:guid}")]
    public Task<EventDto> GetEvent(Guid eventId, CancellationToken ct) => events.GetUpcomingAsync(eventId, ct);

    [HttpGet("events/{eventId:guid}/capacity")]
    public Task<CapacityDto> GetCapacity(Guid eventId, CancellationToken ct) => events.GetCapacityAsync(eventId, ct);

    /// <summary>ADD registers a player; REMOVE drops a registration. 409 Problem Details on a version conflict or a full event.</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await registrations.RegisterAsync(request, ct);
        return result switch
        {
            RegisterAdded added => Ok(added),
            RegisterRemoved removed => Ok(removed),
            RegisterConflict conflict => ConflictProblem(conflict),
            _ => throw new InvalidOperationException($"Unexpected result {result.GetType().Name}."),
        };
    }

    [HttpGet("stores/{storeId:guid}/templates")]
    public Task<IReadOnlyList<TemplateDto>> ListTemplates(Guid storeId, CancellationToken ct) => templates.ListAsync(storeId, ct);

    [HttpPut("stores/{storeId:guid}/templates")]
    public Task<TemplateDto> PutTemplate(Guid storeId, TemplateDto request, CancellationToken ct) => templates.UpsertAsync(storeId, request, ct);

    private ObjectResult ConflictProblem(RegisterConflict conflict)
    {
        var problem = ProblemDetailsFactory.CreateProblemDetails(
            HttpContext,
            statusCode: StatusCodes.Status409Conflict,
            title: conflict.Reason == ConflictReasons.EventFull ? "Event full" : "Version conflict",
            detail: conflict.Reason == ConflictReasons.EventFull
                ? "No seats are left for this event."
                : "The versionId is stale. Retry with the returned versionId.");
        problem.Extensions["reason"] = conflict.Reason;
        problem.Extensions["versionId"] = conflict.VersionId;
        problem.Extensions["remainingCapacity"] = conflict.RemainingCapacity;
        return new ObjectResult(problem) { StatusCode = StatusCodes.Status409Conflict, ContentTypes = { "application/problem+json" } };
    }
}
