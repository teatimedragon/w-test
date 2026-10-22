using GameStore.Core.Contracts;

namespace GameStore.Core.Validation;

/// <summary>Collects field errors keyed by camelCase field name.</summary>
internal sealed class ErrorBag
{
    private readonly Dictionary<string, List<string>> _errors = new();

    public void Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var list)) _errors[field] = list = [];
        list.Add(message);
    }

    public IReadOnlyDictionary<string, string[]> ToDictionary() => _errors.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());

    public void RequiredText(string field, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) Add(field, $"{field} is required.");
        else if (value.Trim().Length > maxLength) Add(field, $"{field} must be at most {maxLength} characters.");
    }

    public void OptionalText(string field, string? value, int maxLength)
    {
        if (value is not null && value.Trim().Length > maxLength) Add(field, $"{field} must be at most {maxLength} characters.");
    }

    /// <summary>capacity > 0; 0 &lt; minimum ≤ capacity; duration > 0. Values must fit SMALLINT where stored as one.</summary>
    public void Numbers(string capacityField, int? capacity, string minimumField, int? minimum, string durationField, int? duration)
    {
        if (capacity is null) Add(capacityField, $"{capacityField} is required.");
        else if (capacity <= 0) Add(capacityField, $"{capacityField} must be greater than 0.");
        else if (capacity > short.MaxValue) Add(capacityField, $"{capacityField} must be at most {short.MaxValue}.");

        if (minimum is null) Add(minimumField, $"{minimumField} is required.");
        else if (minimum <= 0) Add(minimumField, $"{minimumField} must be greater than 0.");
        else if (capacity is > 0 && minimum > capacity) Add(minimumField, $"{minimumField} must not exceed {capacityField}.");

        if (duration is null) Add(durationField, $"{durationField} is required.");
        else if (duration <= 0) Add(durationField, $"{durationField} must be greater than 0 minutes.");
    }
}

public static class FieldLimits
{
    public const int PlayerName = 100;
    public const int GameType = 100;
    public const int EventName = 250;
    public const int TemplateName = 100;
    public const int PlayFormats = 250;
    public const int Details = 250;
}

public static class Validators
{
    /// <summary>Player name: required, trimmed, 1–100 characters.</summary>
    public static IReadOnlyDictionary<string, string[]> ValidateRegister(RegisterRequest request)
    {
        var errors = new ErrorBag();
        if (request.EventId is null || request.EventId == Guid.Empty) errors.Add("eventId", "eventId is required.");
        if (request.VersionId is null) errors.Add("versionId", "versionId is required.");

        switch (request.Action)
        {
            case RegisterActions.Add:
                errors.RequiredText("name", request.Name, FieldLimits.PlayerName);
                break;
            case RegisterActions.Remove:
                if (request.RegistrationId is null || request.RegistrationId == Guid.Empty)
                    errors.Add("registrationId", "registrationId is required.");
                break;
            default:
                errors.Add("action", "action must be ADD or REMOVE.");
                break;
        }
        return errors.ToDictionary();
    }

    /// <summary>
    /// Event: capacity > 0; 0 &lt; eventMinimum ≤ capacity; date/time not in the past (store time zone);
    /// duration > 0; text fields within column lengths. Name and gameType are required.
    /// </summary>
    public static IReadOnlyDictionary<string, string[]> ValidateEvent(CreateEventRequest request, Func<DateOnly, TimeOnly, bool> isInPast)
    {
        var errors = new ErrorBag();
        errors.RequiredText("name", request.Name, FieldLimits.EventName);
        errors.RequiredText("gameType", request.GameType, FieldLimits.GameType);
        errors.OptionalText("playFormats", request.PlayFormats, FieldLimits.PlayFormats);
        errors.OptionalText("details", request.Details, FieldLimits.Details);
        errors.Numbers("capacity", request.Capacity, "eventMinimum", request.EventMinimum, "duration", request.Duration);

        if (request.Date is null) errors.Add("date", "date is required.");
        if (request.Time is null) errors.Add("time", "time is required.");
        if (request.Date is { } date && request.Time is { } time && isInPast(date, time))
            errors.Add("date", "The event date and time must not be in the past.");

        return errors.ToDictionary();
    }

    /// <summary>Template: same numeric rules as events; name (and gameType) required.</summary>
    public static IReadOnlyDictionary<string, string[]> ValidateTemplate(TemplateDto request)
    {
        var errors = new ErrorBag();
        if (request.TemplateId is < 0 or > short.MaxValue)
            errors.Add("templateId", $"templateId must be between 0 and {short.MaxValue}.");
        errors.RequiredText("name", request.Name, FieldLimits.TemplateName);
        errors.RequiredText("gameType", request.GameType, FieldLimits.GameType);
        errors.OptionalText("defaultPlayFormats", request.DefaultPlayFormats, FieldLimits.PlayFormats);
        errors.OptionalText("defaultDetails", request.DefaultDetails, FieldLimits.Details);
        errors.Numbers("defaultCapacity", request.DefaultCapacity, "defaultEventMinimum", request.DefaultEventMinimum, "defaultDuration", request.DefaultDuration);
        return errors.ToDictionary();
    }

    internal static void ThrowIfAny(IReadOnlyDictionary<string, string[]> errors)
    {
        if (errors.Count > 0) throw new Errors.ValidationFailedException(errors);
    }
}
