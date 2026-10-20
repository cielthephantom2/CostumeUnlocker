using System.Xml;
using System.Xml.Linq;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LastEpochLAN.Core;

/// <summary>A detached, bounded native loot filter. Never a filename or character binding.</summary>
[JsonConverter(typeof(RoomLootFilterConverter))]
public sealed record RoomLootFilter(string OwnerId, string Xml)
{
    public const int MaximumCharacters = 262144;
    public const int MaximumCompressedBytes = 131072;
    public bool IsValid() => Guid.TryParse(OwnerId, out _) && ValidXml(Xml) &&
        Compress(Xml).Length <= MaximumCompressedBytes;

    internal static byte[] Compress(string xml)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
        { var bytes = Encoding.UTF8.GetBytes(xml); gzip.Write(bytes); }
        return output.ToArray();
    }

    public static bool ValidXml(string xml)
    {
        if (xml == "") return true; // No selected filter contributes no remote rules.
        if (xml is not { Length: > 0 and <= MaximumCharacters }) return false;
        try
        {
            using var input = new StringReader(xml);
            using var reader = XmlReader.Create(input, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                MaxCharactersInDocument = MaximumCharacters, MaxCharactersFromEntities = 0
            });
            var document = XDocument.Load(reader);
            return document.Root?.Name.LocalName == "ItemFilter" &&
                document.Descendants().Count() <= 4096 &&
                document.Descendants().All(e => e.Ancestors().Take(33).Count() <= 32);
        }
        catch (XmlException) { return false; }
    }
}

// Four compressed filters fit in a separate frame, even at the payload limit.
// Decompression is bounded before any XML reaches native code.
public sealed class RoomLootFilterConverter : JsonConverter<RoomLootFilter>
{
    public override RoomLootFilter Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (!root.TryGetProperty("OwnerId", out var owner) || owner.ValueKind != JsonValueKind.String ||
            !Guid.TryParse(owner.GetString(), out _) || !root.TryGetProperty("Data", out var data) ||
            data.ValueKind != JsonValueKind.String || data.GetString()!.Length > 174764)
            throw new JsonException("Invalid room filter envelope.");
        try
        {
            byte[] packed = data.GetBytesFromBase64();
            if (packed.Length > RoomLootFilter.MaximumCompressedBytes) throw new JsonException("Filter payload too large.");
            using var input = new MemoryStream(packed);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var text = new StreamReader(gzip, new UTF8Encoding(false, true));
            char[] buffer = new char[RoomLootFilter.MaximumCharacters + 1];
            int count = 0, read;
            while (count < buffer.Length && (read = text.Read(buffer, count, buffer.Length - count)) > 0) count += read;
            if (count > RoomLootFilter.MaximumCharacters) throw new JsonException("Expanded filter too large.");
            string xml = new(buffer, 0, count);
            if (!RoomLootFilter.ValidXml(xml)) throw new JsonException("Invalid room filter XML.");
            return new(owner.GetString()!, xml);
        }
        catch (Exception ex) when (ex is InvalidDataException or FormatException or DecoderFallbackException)
        { throw new JsonException("Invalid compressed room filter.", ex); }
    }

    public override void Write(Utf8JsonWriter writer, RoomLootFilter value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("OwnerId", value.OwnerId);
        writer.WriteBase64String("Data", RoomLootFilter.Compress(value.Xml));
        writer.WriteEndObject();
    }
}
