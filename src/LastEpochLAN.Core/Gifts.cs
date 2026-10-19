using System.Security.Cryptography;
using System.Text.Json;

namespace LastEpochLAN.Core;

public sealed record GiftMessage
{
    public string Kind { get; init; } = "offer";
    public string TransferId { get; init; } = "";
    public string FromIdentity { get; init; } = "";
    public string FromCharacter { get; init; } = "";
    public string ToIdentity { get; init; } = "";
    public string ToCharacter { get; init; } = "";
    public string EncodedItem { get; init; } = "";
    public string Fingerprint { get; init; } = "";
    public int Quantity { get; init; } = 1;
    public bool IsValid()
    {
        if (Kind is not ("offer" or "stored" or "rejected") || !Guid.TryParseExact(TransferId, "N", out _) ||
            !Guid.TryParseExact(FromIdentity, "N", out _) || !Guid.TryParseExact(ToIdentity, "N", out _) ||
            FromCharacter is not { Length: > 0 and <= 128 } || ToCharacter is not { Length: > 0 and <= 128 } ||
            Quantity is < 1 or > 999 || Fingerprint is not { Length: 64 } ||
            EncodedItem is not { Length: > 0 and <= 1368 }) return false;
        try
        {
            var bytes = Convert.FromBase64String(EncodedItem);
            return bytes.Length is > 0 and <= 1024 && Convert.ToHexString(SHA256.HashData(bytes)) == Fingerprint;
        }
        catch (FormatException) { return false; }
    }
    public GiftMessage Reply(string kind) => this with { Kind = kind, FromIdentity = ToIdentity, FromCharacter = ToCharacter,
        ToIdentity = FromIdentity, ToCharacter = FromCharacter };
}

public sealed record GiftRecord
{
    public GiftMessage Message { get; init; } = new();
    public string State { get; init; } = "";
    public string Error { get; init; } = "";
    public bool Outgoing { get; init; }
}

// One atomic record per transfer. The journal never treats a TCP ACK as a native
// inventory save. Recovery of Preparing/Applying is delegated to the game adapter.
public sealed class GiftJournal
{
    private readonly string directory;
    private readonly Dictionary<string, GiftRecord> records = new();
    public IEnumerable<GiftRecord> Records => records.Values.ToArray();
    public GiftJournal(string root, string identity, string character)
    {
        if (!Guid.TryParseExact(identity, "N", out _) || string.IsNullOrEmpty(character)) throw new ArgumentException("Gift identity required.");
        directory = Path.Combine(Path.GetFullPath(root), identity, Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(character))));
        Directory.CreateDirectory(directory);
        foreach (var file in Directory.GetFiles(directory, "*.json"))
        {
            var record = JsonSerializer.Deserialize<GiftRecord>(File.ReadAllText(file)) ?? throw new InvalidDataException("Invalid gift journal.");
            Validate(record);
            if (Path.GetFileNameWithoutExtension(file) != record.Message.TransferId) throw new InvalidDataException("Gift journal identity mismatch.");
            records.Add(record.Message.TransferId, record);
        }
    }
    public GiftRecord? Find(string id) => records.GetValueOrDefault(id);
    private static void Validate(GiftRecord record)
    {
        if (!record.Message.IsValid() || record.State is not ("Preparing" or "Sending" or "Sent" or "Pending" or "Applying" or "Delivered" or "Rejected" or "Cancelled"))
            throw new InvalidDataException("Invalid persisted gift.");
    }
    public void Put(GiftRecord record)
    {
        Validate(record);
        if (records.TryGetValue(record.Message.TransferId, out var old))
        {
            if (old.Message != record.Message || old.Outgoing != record.Outgoing) throw new InvalidDataException("Conflicting gift transfer ID.");
            if (old.State is "Sent" or "Delivered" or "Rejected" or "Cancelled" && old.State != record.State)
                throw new InvalidOperationException("Terminal gift cannot be reopened.");
        }
        string target = Path.Combine(directory, record.Message.TransferId + ".json");
        string temporary = target + ".new";
        using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(file, record); file.Flush(true);
        }
        File.Move(temporary, target, true);
        records[record.Message.TransferId] = record;
    }
}
