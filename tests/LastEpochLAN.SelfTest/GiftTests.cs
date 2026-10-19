using LastEpochLAN.Core;
using System.Net;
using System.Security.Cryptography;

internal static class GiftTests
{
    internal static async Task Run(Action<bool, string> check)
    {
        string sender = Guid.NewGuid().ToString("N"), recipient = Guid.NewGuid().ToString("N");
        byte[] bytes = { 6, 1, 3, 4 };
        var message = new GiftMessage { TransferId = Guid.NewGuid().ToString("N"), FromIdentity = sender, FromCharacter = "char-a",
            ToIdentity = recipient, ToCharacter = "char-b", EncodedItem = Convert.ToBase64String(bytes), Fingerprint = Convert.ToHexString(SHA256.HashData(bytes)) };
        check(message.IsValid(), "gift includes exact bytes and verified fingerprint");
        check(!(message with { EncodedItem = "AQ==" }).IsValid() && !(message with { Quantity = 0 }).IsValid() &&
            !(message with { TransferId = "../escape" }).IsValid(), "gift rejects tampered bytes, invalid quantity and path IDs");
        string directory = Path.Combine(Path.GetTempPath(), "lelan-gift-test-" + Guid.NewGuid().ToString("N"));
        var journal = new GiftJournal(directory, sender, "char-a");
        var preparing = new GiftRecord { Message = message, Outgoing = true, State = "Preparing" };
        journal.Put(preparing);
        var resumed = new GiftJournal(directory, sender, "char-a");
        check(resumed.Find(message.TransferId)?.State == "Preparing", "sender intent survives journal reopening without becoming a delivered gift");
        resumed.Put(preparing with { State = "Sending" }); resumed.Put(preparing with { State = "Sent" });
        check(new GiftJournal(directory, sender, "char-a").Find(message.TransferId)?.State == "Sent", "completed sender receipt is durable");
        bool rejected = false;
        try { resumed.Put(preparing); } catch (InvalidOperationException) { rejected = true; }
        check(rejected, "terminal gift cannot be reopened by delayed state");
        rejected = false;
        try { resumed.Put(preparing with { Message = message with { Quantity = 2 } }); } catch (InvalidDataException) { rejected = true; }
        check(rejected, "same transfer ID cannot substitute another quantity");
        var inbox = new GiftJournal(directory, recipient, "char-b");
        var pending = new GiftRecord { Message = message, State = "Pending" };
        inbox.Put(pending); inbox.Put(pending); inbox.Put(pending with { State = "Applying" });
        check(new GiftJournal(directory, recipient, "char-b").Records.Count() == 1, "duplicate delivery persists a single inbox record");
        inbox.Put(pending with { State = "Delivered" });
        check(new GiftJournal(directory, recipient, "other-character").Records.Count() == 0, "gift inbox is isolated by character");
        check(new GiftJournal(directory, recipient, "char-b").Find(message.TransferId)?.State == "Delivered", "receiver completion survives journal reopening");

        using var host = LanSession.Host("GiftHost", "GiftQA", "GiftRoom", 0, IPAddress.Loopback);
        using var guest = await LanSession.JoinAsync("127.0.0.1", host.Port, "GiftGuest", "GiftQA", "GiftRoom");
        host.Publish(new PlayerState { Available = true, Scene = "map", GiftIdentity = sender, CharacterId = "char-a" });
        guest.Publish(new PlayerState { Available = true, Scene = "map", GiftIdentity = recipient, CharacterId = "char-b" });
        await Wait(() => host.PlayerPositions.Any(p => p.GiftIdentity == recipient));
        check(host.PublishGift(message), "host routes Gift to authenticated online recipient");
        GiftMessage? received = null;
        await Wait(() => guest.TryTakeGift(out received));
        check(received == message, "native item bytes and character identities survive framed TCP");
        check(guest.PublishGift(received!.Reply("stored") with { FromIdentity = sender, FromCharacter = "forged" }), "guest sends receipt through host");
        GiftMessage? receipt = null;
        await Wait(() => host.TryTakeGift(out receipt));
        check(receipt == message.Reply("stored"), "receipt sender identity is replaced with actual authenticated peer identity");
        check(!host.PublishGift(message with { ToCharacter = "not-loaded" }), "host cannot route Gift to a different loaded character");
        guest.Dispose();
        await Wait(() => host.PlayerPositions.Count == 1);
        check(!host.PublishGift(message), "offline recipient cannot be mistaken for acknowledged delivery");
        using var rejoined = await LanSession.JoinAsync("127.0.0.1", host.Port, "GiftRejoin", "GiftQA", "GiftRoom");
        rejoined.Publish(new PlayerState { Available = true, Scene = "map", GiftIdentity = recipient, CharacterId = "char-b" });
        await Wait(() => host.PlayerPositions.Any(p => p.GiftIdentity == recipient));
        check(host.PublishGift(message), "persisted Gift can be resent after new connection GUID");
        await Wait(() => rejoined.TryTakeGift(out received));
        check(received!.TransferId == message.TransferId, "reconnect preserves transfer ID for recipient deduplication");
        // Cleanup touches only this test's explicitly verified temporary directory.
        string resolved = Path.GetFullPath(directory);
        if (Path.GetDirectoryName(resolved) != Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) ||
            !Path.GetFileName(resolved).StartsWith("lelan-gift-test-")) throw new Exception("Invalid test cleanup path.");
        Directory.Delete(resolved, true);
        static async Task Wait(Func<bool> predicate)
        {
            long end = Environment.TickCount64 + 5000;
            while (!predicate()) { if (Environment.TickCount64 > end) throw new Exception("Gift transport timeout"); await Task.Delay(10); }
        }
    }
}
