namespace GameStore.Core.Errors;

/// <summary>An unknown id, or an event that has already started. Maps to 404.</summary>
public sealed class NotFoundException(string message) : Exception(message);

/// <summary>Field-level validation failures. Maps to 400 with field errors.</summary>
public sealed class ValidationFailedException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("One or more validation errors occurred.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
