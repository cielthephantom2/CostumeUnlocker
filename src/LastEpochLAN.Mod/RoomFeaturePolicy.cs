using LastEpochLAN.Core;

namespace LastEpochLAN.Mod;

// Release policy, independent of stale config or development command switches.
// Public-IP/Internet transport, F2 custody and room filters stay enabled.
internal static class RoomFeaturePolicy
{
    internal static bool SharedWorldEnabled => false;
    internal static bool AllowsPatch(string ns, string name, string parent = "") =>
        ns == "LastEpochLAN.Mod.OfflineTools" || ns.StartsWith("LastEpochLAN.Mod.OfflineTools.", StringComparison.Ordinal) ||
        ns == "LastEpochLAN.Mod" && (parent == "LanLootFilters" || name is
            "SharedChestItemLockPatch" or "SharedChestManagerLockPatch" or
            "SharedStashNativeBindingPatch" or "SharedStashNativeInputPatch" or "SharedStashProjectionMutationPatch" or "SharedStashPreparationVisibilityPatch" or
            "LanGiftApplyingLockPatch" or "AutoPlayCastObservationPatch" or
            "RoomLadderOpenPatch" or "RoomLadderClosePatch" or "RoomLadderSessionPatch" or "RoomLadderSelfRowPatch" or
            "RoomLadderFilterPatch");

    internal static PlayerState Presence(bool ready, string identity, string character, string group) => new()
    {
        StashReady = ready,
        GiftIdentity = ready ? identity : "", CharacterId = ready ? character : "", GiftGroup = ready ? group : ""
    };
}
