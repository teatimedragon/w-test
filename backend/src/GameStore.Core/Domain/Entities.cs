namespace GameStore.Core.Domain;

public class Store
{
    public Guid StoreId { get; set; }
    public string StoreName { get; set; } = "";
    /// <summary>IANA time zone name. Event dates and times are local to this zone.</summary>
    public string TimeZone { get; set; } = "";
}

public class Event
{
    public Guid EventId { get; set; }
    public Guid StoreId { get; set; }
    public string GameType { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Store-local date.</summary>
    public DateOnly Date { get; set; }
    /// <summary>Store-local start time.</summary>
    public TimeOnly Time { get; set; }
    /// <summary>Length of the event (not a time of day).</summary>
    public TimeSpan Duration { get; set; }
    /// <summary>Total seats.</summary>
    public short Capacity { get; set; }
    /// <summary>Players needed for the event to be held. Informational only.</summary>
    public short EventMinimum { get; set; }
    public string PlayFormats { get; set; } = "";
    public string Details { get; set; } = "";
}

public class Registration
{
    public Guid RegistrationId { get; set; }
    public Guid EventId { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>One row per event, used for optimistic concurrency on registration.</summary>
public class Capacity
{
    public Guid EventId { get; set; }
    /// <summary>Increments on every successful ADD or REMOVE.</summary>
    public short VersionId { get; set; }
    public short RemainingCapacity { get; set; }

    /// <summary>The version that follows <paramref name="current"/>. Wraps back to 1 rather than overflowing SMALLINT.</summary>
    public static short NextVersion(short current) => current >= short.MaxValue ? (short)1 : (short)(current + 1);
}

public class Template
{
    public short TemplateId { get; set; }
    public Guid StoreId { get; set; }
    public string GameType { get; set; } = "";
    public string Name { get; set; } = "";
    public short DefaultCapacity { get; set; }
    public short DefaultEventMinimum { get; set; }
    public TimeSpan DefaultDuration { get; set; }
    public string DefaultPlayFormats { get; set; } = "";
    public string DefaultDetails { get; set; } = "";
}
