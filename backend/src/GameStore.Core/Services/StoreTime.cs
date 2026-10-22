namespace GameStore.Core.Services;

/// <summary>Converts store-local event dates and times to instants.</summary>
public static class StoreTime
{
    public static TimeZoneInfo FindZone(string ianaTimeZone) => TimeZoneInfo.FindSystemTimeZoneById(ianaTimeZone);

    /// <summary>
    /// The UTC instant of a store-local wall-clock time. A time skipped by a daylight-saving jump
    /// (e.g. 02:30 on the spring-forward night) is moved forward by the size of the jump.
    /// </summary>
    public static DateTimeOffset ToUtc(DateOnly date, TimeOnly time, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local))
        {
            var rule = zone.GetAdjustmentRules().FirstOrDefault(r => r.DateStart <= local && local <= r.DateEnd);
            local = local + (rule?.DaylightDelta ?? TimeSpan.FromHours(1));
        }
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
    }

    /// <summary>True when the event starts now or later ("upcoming").</summary>
    public static bool IsUpcoming(DateOnly date, TimeOnly time, TimeZoneInfo zone, DateTimeOffset now) =>
        ToUtc(date, time, zone) >= now;
}
