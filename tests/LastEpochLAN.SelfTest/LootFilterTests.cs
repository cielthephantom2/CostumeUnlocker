using System.Net;
using System.Text.Json;
using LastEpochLAN.Core;

internal static class LootFilterTests
{
    public static async Task Run(Action<bool, string> check)
    {
        const string a = "<ItemFilter><name>Host</name><rules /></ItemFilter>";
        const string b = "<ItemFilter><name>Guest</name><rules /></ItemFilter>";
        check(RoomLootFilter.ValidXml(a) && RoomLootFilter.ValidXml(""), "filter XML and no-filter state validate");
        check(!RoomLootFilter.ValidXml("<wrong />") && !RoomLootFilter.ValidXml("<ItemFilter>"), "invalid root and malformed XML rejected");
        check(!RoomLootFilter.ValidXml("<!DOCTYPE ItemFilter [<!ENTITY x 'test'>]><ItemFilter>&x;</ItemFilter>"), "DTD cannot reach native deserializer");
        check(!RoomLootFilter.ValidXml("<ItemFilter>" + new string('x', RoomLootFilter.MaximumCharacters) + "</ItemFilter>"), "oversized XML rejected");
        check(!RoomLootFilter.ValidXml("<ItemFilter>" + string.Concat(Enumerable.Repeat("<a>", 34)) + string.Concat(Enumerable.Repeat("</a>", 34)) + "</ItemFilter>"), "deep XML rejected");
        var original = new RoomLootFilter(Guid.NewGuid().ToString(), a);
        string encoded = JsonSerializer.Serialize(original);
        check(JsonSerializer.Deserialize<RoomLootFilter>(encoded) == original && !encoded.Contains("ItemFilter"), "compressed filter round trip preserves exact XML");
        bool rejected = false;
        try { JsonSerializer.Deserialize<RoomLootFilter>("{\"OwnerId\":\"" + original.OwnerId + "\",\"Data\":\"broken\"}"); }
        catch (JsonException) { rejected = true; }
        check(rejected, "corrupt compressed payload rejected");

        using var host = LanSession.Host("Host", "filter-fixture", "filter-room", 0, IPAddress.Loopback);
        check(host.PublishLootFilter(a), "host publishes active filter");
        using var guest = await LanSession.JoinAsync("127.0.0.1", host.Port, "Guest", "filter-fixture", "filter-room");
        check(guest.PublishLootFilter(b), "guest publishes active filter");
        async Task Wait(Func<bool> ready, string label)
        {
            long until = Environment.TickCount64 + 5000;
            while (!ready() && Environment.TickCount64 < until) await Task.Delay(20);
            check(ready(), label);
        }
        await Wait(() => host.LootFilters.Count == 2 && guest.LootFilters.Count == 2, "party-only host and guest receive both filters without map participation");
        check(host.LootFilters.Single(f => f.OwnerId == guest.LocalId).Xml == b &&
            guest.LootFilters.Single(f => f.OwnerId == host.LocalId).Xml == a, "TCP relay preserves authenticated filter ownership and content");
        using var late = await LanSession.JoinAsync("127.0.0.1", host.Port, "Late", "filter-fixture", "filter-room");
        late.PublishLootFilter("");
        await Wait(() => late.LootFilters.Count == 3 && guest.LootFilters.Count == 3, "late join receives full roster including explicit no-filter state");
        guest.PublishLootFilter(a);
        await Wait(() => late.LootFilters.SingleOrDefault(f => f.OwnerId == guest.LocalId)?.Xml == a &&
            host.LootFilters.SingleOrDefault(f => f.OwnerId == guest.LocalId)?.Xml == a, "filter edits propagate to host and other client");
        guest.PublishLootFilter("");
        await Wait(() => late.LootFilters.SingleOrDefault(f => f.OwnerId == guest.LocalId)?.Xml == "", "disabling selected filter removes its remote contribution");
        check(!host.PublishLootFilter("<wrong />") && host.LootFilters.Single(f => f.OwnerId == host.LocalId).Xml == a, "invalid publish cannot replace accepted local filter");
        string large = "<ItemFilter><name>" + new string('x', 200000) + "</name></ItemFilter>";
        host.PublishLootFilter(large);
        await Wait(() => late.LootFilters.SingleOrDefault(f => f.OwnerId == host.LocalId)?.Xml == large, "large filter travels in compressed dedicated frame");
        guest.Dispose();
        await Wait(() => host.LootFilters.Count == 2 && late.LootFilters.Count == 2 &&
            late.LootFilters.All(f => f.OwnerId != guest.LocalId), "departing member disappears from host and client filter roster");
        late.Dispose();
        check(late.LootFilters.Count == 0 && !late.PublishLootFilter(a), "disconnected session exposes no stale filters");
    }
}
