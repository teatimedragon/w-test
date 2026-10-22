namespace GameStore.Core.Contracts;

public static class ConflictReasons
{
    /// <summary>The caller's versionId is stale; the client may retry with the returned one.</summary>
    public const string VersionConflict = "VERSION_CONFLICT";
    /// <summary>No seats left; the client stops.</summary>
    public const string EventFull = "EVENT_FULL";
}

public abstract record RegisterResult;

public sealed record RegisterAdded(string Name, Guid RegistrationId, int VersionId, int RemainingCapacity) : RegisterResult;

public sealed record RegisterRemoved(Guid RegistrationId, int VersionId, int RemainingCapacity) : RegisterResult;

public sealed record RegisterConflict(string Reason, int VersionId, int RemainingCapacity) : RegisterResult;
