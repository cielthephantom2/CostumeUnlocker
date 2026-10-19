using System.Text.Json;

namespace LastEpochLAN.Core;

public sealed record HudValue
{
    public string Key { get; init; } = "";
    public string Json { get; init; } = "";
    public bool IsValid()
    {
        if (Key is not { Length: > 0 and <= 160 } || Json is not { Length: > 0 and <= 160 }) return false;
        try
        {
            using var doc = JsonDocument.Parse(Json);
            var value = doc.RootElement;
            return value.ValueKind is JsonValueKind.True or JsonValueKind.False ||
                value.ValueKind == JsonValueKind.String && value.GetString()!.Length <= 128 ||
                value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double number) && double.IsFinite(number) && Math.Abs(number) <= 100000000;
        }
        catch (JsonException) { return false; }
    }
}
public sealed record HudConfiguration
{
    public string Schema { get; init; } = "tm-hud-1";
    public long Revision { get; init; }
    public HudValue[] Values { get; init; } = Array.Empty<HudValue>();
    public bool IsValid() => Schema == "tm-hud-1" && Revision > 0 && Values is { Length: <= 256 } &&
        Values.All(v => v != null && v.IsValid()) && Values.Select(v => v.Key).Distinct(StringComparer.Ordinal).Count() == Values.Length;
    public HudConfiguration Copy() => this with { Values = Values.ToArray() };
}

// Shared managed bridge: HUD code runs on Unity's thread; network code only
// handles immutable records. No Unity objects cross the transport boundary.
public static class HudAuthority
{
    public static LanSession? Session { get; set; }
    public static bool CanEdit => Session?.Connected == true && Session.IsHost;
    public static bool Connected => Session?.Connected == true;
    public static long AppliedRevision { get; set; }
    public static string Error { get; set; } = "";
}
