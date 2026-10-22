namespace GameStore.Core.Contracts;

// Durations are whole minutes in the API.

public sealed record StoreDto(Guid StoreId, string StoreName, string TimeZone);

public sealed record EventDto(
    Guid EventId,
    string GameType,
    DateOnly Date,
    TimeOnly Time,
    string Name,
    int Capacity,
    int EventMinimum,
    int Duration,
    string PlayFormats,
    string Details,
    int RegisteredCount);

// Fields are nullable so that a missing value reaches the validator and is reported as a field error.
public sealed record CreateEventRequest(
    string? GameType,
    DateOnly? Date,
    TimeOnly? Time,
    string? Name,
    int? Capacity,
    int? EventMinimum,
    int? Duration,
    string? PlayFormats,
    string? Details);

public sealed record CalendarFileDto(string FileName, string Content);

public sealed record CapacityDto(int RemainingCapacity, int VersionId);

public static class RegisterActions
{
    public const string Add = "ADD";
    public const string Remove = "REMOVE";
}

public sealed record RegisterRequest(
    Guid? EventId,
    string? Action,
    int? VersionId,
    string? Name,
    Guid? RegistrationId);

public sealed record TemplateDto(
    int? TemplateId,
    string? GameType,
    string? Name,
    int? DefaultCapacity,
    int? DefaultEventMinimum,
    int? DefaultDuration,
    string? DefaultPlayFormats,
    string? DefaultDetails);
