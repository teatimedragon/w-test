using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameStore.Api;

/// <summary>Writes times as "HH:mm"; reads "HH:mm" or "HH:mm:ss".</summary>
public sealed class TimeOnlyJsonConverter : JsonConverter<TimeOnly>
{
    private static readonly string[] Formats = ["HH:mm", "HH:mm:ss"];

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString();
        if (TimeOnly.TryParseExact(text, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)) return time;
        throw new JsonException("Expected a time in HH:mm format.");
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(value.Second == 0 ? "HH:mm" : "HH:mm:ss", CultureInfo.InvariantCulture));
}
