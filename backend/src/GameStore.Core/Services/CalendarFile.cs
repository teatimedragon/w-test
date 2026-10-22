using System.Globalization;
using System.Text;
using GameStore.Core.Domain;

namespace GameStore.Core.Services;

/// <summary>Builds an iCalendar (.ics, RFC 5545) file for one event, importable into Google Calendar and other calendar apps.</summary>
public static class CalendarFile
{
    /// <summary>
    /// The event's store-local start and end are converted to UTC with the store's time zone, so the file
    /// needs no VTIMEZONE block and shows the right time wherever it is opened.
    /// </summary>
    public static string Build(Event evt, Store store, DateTimeOffset now)
    {
        var zone = StoreTime.FindZone(store.TimeZone);
        var start = StoreTime.ToUtc(evt.Date, evt.Time, zone);
        var end = start + evt.Duration;
        var description = string.Join("\n", new[] { evt.GameType, evt.PlayFormats, evt.Details }.Where(s => !string.IsNullOrWhiteSpace(s)));

        var lines = new List<string>
        {
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "PRODID:-//Basic Game Store//Game Store Events//EN",
            "CALSCALE:GREGORIAN",
            "METHOD:PUBLISH",
            "BEGIN:VEVENT",
            $"UID:{evt.EventId}@game-store-events",
            $"DTSTAMP:{Utc(now)}",
            $"DTSTART:{Utc(start)}",
            $"DTEND:{Utc(end)}",
            $"SUMMARY:{Escape(evt.Name)}",
            $"LOCATION:{Escape(store.StoreName)}",
        };
        if (description.Length > 0) lines.Add($"DESCRIPTION:{Escape(description)}");
        lines.Add("END:VEVENT");
        lines.Add("END:VCALENDAR");

        var sb = new StringBuilder();
        foreach (var line in lines) sb.Append(Fold(line)).Append("\r\n");
        return sb.ToString();
    }

    /// <summary>A safe download name based on the event name, e.g. "friday-night-magic.ics".</summary>
    public static string FileName(Event evt)
    {
        var slug = new string(evt.Name.ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray());
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        slug = slug.Trim('-');
        return (slug.Length == 0 ? "event" : slug[..Math.Min(slug.Length, 60)].TrimEnd('-')) + ".ics";
    }

    private static string Utc(DateTimeOffset value) => value.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    /// <summary>TEXT escaping from RFC 5545 §3.3.11.</summary>
    internal static string Escape(string text) => text
        .Replace("\\", "\\\\")
        .Replace(";", "\\;")
        .Replace(",", "\\,")
        .Replace("\r\n", "\n")
        .Replace("\n", "\\n");

    /// <summary>Folds lines longer than 75 octets (RFC 5545 §3.1) without splitting a UTF-8 character.</summary>
    internal static string Fold(string line)
    {
        var sb = new StringBuilder();
        var octets = 0;
        foreach (var rune in line.EnumerateRunes())
        {
            var size = rune.Utf8SequenceLength;
            if (octets + size > 75)
            {
                sb.Append("\r\n ");
                octets = 1;
            }
            sb.Append(rune.ToString());
            octets += size;
        }
        return sb.ToString();
    }
}
