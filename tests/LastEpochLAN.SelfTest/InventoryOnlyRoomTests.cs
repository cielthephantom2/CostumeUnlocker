using System.Net;
using LastEpochLAN.Core;
using LastEpochLAN.Mod;

internal static class InventoryOnlyRoomTests
{
    internal static async Task Wait(Func<bool> condition)
    {
        long deadline = Environment.TickCount64 + 5000;
        while (Environment.TickCount64 < deadline)
        { if (condition()) return; await Task.Delay(10); }
        throw new Exception("Inventory-only transport timed out");
    }
    internal static async Task Run(Action<bool,string> check)
    {
        check(!RoomFeaturePolicy.SharedWorldEnabled, "release shared-world policy remains disabled independently of persisted settings");
        var identity = Guid.NewGuid().ToString("N");
        var presence = RoomFeaturePolicy.Presence(true, identity, "character", "cycle:normal");
        check(presence.IsValid() && presence.StashReady && presence.GiftIdentity == identity && presence.CharacterId == "character" &&
            !presence.Available && !presence.Cooperative && presence.Scene == "" && presence.Avatar == null && presence.Summons.Length == 0 &&
            presence.DroppedItems.Length == 0 && presence.MaxHealth == 0 && presence.EchoRunId == "",
            "inventory presence keeps F2 identity/readiness without world, health, avatar, summons or ground-item data");
        var loading = RoomFeaturePolicy.Presence(false, identity, "old-character", "old-group");
        check(!loading.StashReady && loading.GiftIdentity == "" && loading.CharacterId == "" && loading.GiftGroup == "",
            "loading presence clears previous character identity instead of permitting stale inventory transfers");
        foreach (var patch in new[] {"SharedChestItemLockPatch", "SharedChestManagerLockPatch", "SharedStashNativeBindingPatch",
            "SharedStashNativeInputPatch", "SharedStashProjectionMutationPatch", "LanGiftApplyingLockPatch", "AutoPlayCastObservationPatch"})
            check(RoomFeaturePolicy.AllowsPatch("LastEpochLAN.Mod",patch), "inventory-only release retains " + patch);
        check(RoomFeaturePolicy.AllowsPatch("LastEpochLAN.Mod", "NativeGroundFilterPatch", "LanLootFilters") &&
            RoomFeaturePolicy.AllowsPatch("LastEpochLAN.Mod.OfflineTools.Dps", "DpsActorDamagePatch"),
            "room filter label hooks and local DPS hooks remain enabled");
        foreach (var patch in new[] {"SkillCapture", "HostDeathPatch", "ReplicaAiPatch", "NativeGlobalVisualCapturePatch",
            "SharedEchoCompletionPatch", "NativeLanPartyPortalPatch", "LanAilmentResolvedPatch", "LanInventoryDropCapturePatch"})
            check(!RoomFeaturePolicy.AllowsPatch("LastEpochLAN.Mod", patch), "inventory-only release omits " + patch);
        check(LanNetworks.IsSupported(IPAddress.Parse("8.8.8.8")), "inventory-only release retains direct public IPv4 transport");
        using var host = LanSession.Host("InventoryHost", "inventory-only-fixture", "private", 0, IPAddress.Loopback);
        host.Publish(presence);
        using var guest = await LanSession.JoinAsync("127.0.0.1", host.Port, "InventoryGuest", "inventory-only-fixture", "private");
        guest.Publish(RoomFeaturePolicy.Presence(true, Guid.NewGuid().ToString("N"), "guest-character", "cycle:normal"));
        await Wait(() => host.Players.Count == 2 && host.Players.All(p => p.StashReady) && guest.Players.Count == 2 && guest.Players.All(p => p.StashReady));
        check(guest.Players.All(p => !p.Available && !p.Cooperative) && host.WorldInfo.EnemyCount == 0,
            "authenticated room carries both inventory identities while neither peer publishes a shared world");
        check(guest.SendChest(new()), "inventory-only guest can request F2 without map availability or co-op participation");
        ChestRequest? request = null;
        await Wait(() => host.TryTakeChestRequest(out request));
        check(request?.SenderId == guest.LocalId, "F2 request still binds to authenticated guest identity");
        check(guest.PublishLootFilter("<ItemFilter><name>Inventory</name><rules /></ItemFilter>"), "inventory-only room still publishes selected loot filter");
        await Wait(() => host.LootFilters.Any(f => f.OwnerId == guest.LocalId && f.Xml == "<ItemFilter><name>Inventory</name><rules /></ItemFilter>"));
        check(host.LootFilters.Any(f => f.OwnerId == guest.LocalId), "filter reaches Host while world synchronization remains disabled");
    }
}
