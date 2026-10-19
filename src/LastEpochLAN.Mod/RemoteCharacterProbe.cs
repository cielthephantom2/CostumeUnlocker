using System.Text.Json;

namespace LastEpochLAN.Mod;

internal static class RemoteCharacterProbe
{
    internal static bool Capture(string reportPath)
    {
        var local = Il2Cpp.PlayerFinder.getPlayerActor();
        if (local == null || !local.isPlayerActor || local.characterDataTracker?.charData == null) return false;
        string scene = Il2Cpp.SceneList.GetCurrentSceneDetails()?.Name ?? "";
        if (scene == "" || scene.Contains("CharacterSelect", StringComparison.OrdinalIgnoreCase) ||
            scene.Contains("Frontend", StringComparison.OrdinalIgnoreCase) || !local.gameObject.activeInHierarchy ||
            !local.HasMovedToPlayerSpawn) return false;
        using var remote = new NativeRemoteActor("isolation-probe", local.characterDataTracker.charData.CharacterClass);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new
        {
            utc = DateTimeOffset.UtcNow, scene, checks = remote.Isolation,
            source = "fresh native ActorSoftRef asset; no copy of live host character",
            inventoryUsesOwnTracker = remote.Actor.itemContainersManager?.tracker?.Pointer == remote.Tracker.Pointer,
            goldUsesOwnTracker = remote.Actor.goldTracker?.dataTracker?.Pointer == remote.Tracker.Pointer,
            passed = remote.Isolation.Values.All(value => value),
            scope = "inactive native prefab isolation only; activation, player registration, stats/build transfer and combat not verified",
            fullCoop = false
        }, new JsonSerializerOptions { WriteIndented = true }));
        return true;
    }
}
