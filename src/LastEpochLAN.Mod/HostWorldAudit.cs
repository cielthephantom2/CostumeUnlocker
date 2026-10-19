using System.Text.Json;
using UnityEngine;

namespace LastEpochLAN.Mod;

/// <summary>
/// Read-only native-state capture for developing a shared-world adapter.
/// Capturing state does not install that state on a guest or grant any item.
/// </summary>
internal static class HostWorldAudit
{
    internal static bool Capture(string destination)
    {
        var player = Il2Cpp.PlayerFinder.getPlayerActor();
        if (player == null) return false;
        var quests = Il2Cpp.PlayerFinder.getLocalPlayerStatefulQuestList();
        var manager = Il2Cpp.GroundItemManager.instance;
        if (quests == null || manager == null) return false;
        // Use native item/quest encodings: a name or rarity alone loses affixes and progress.
        var encodedQuests = quests.serialiseQuests().ToArray();
        var drops = manager.offlineItems;
        if (drops == null) return false;
        var records = new List<object>();
        foreach (var item in drops.items)
        {
            records.Add(new { kind = "item", id = item.id, position = Position(item.location),
                nativeItem = Convert.ToBase64String(item.itemData.GetIDAsArray().ToArray()) });
        }
        foreach (var gold in drops.gold)
            records.Add(new { kind = "gold", id = gold.id, position = Position(gold.location), amount = gold.goldValue });
        foreach (var potion in drops.potions)
            records.Add(new { kind = "potion", id = potion.id, position = Position(potion.location) });
        foreach (var xp in drops.xpTomes)
            records.Add(new { kind = "xp", id = xp.id, position = Position(xp.location), amount = xp.experience });
        foreach (var favor in drops.favorTomes)
            records.Add(new { kind = "favor", id = favor.id, position = Position(favor.location), amount = favor.favor });
        foreach (var bone in drops.ancientBones)
            records.Add(new { kind = "ancient-bone", id = bone.id, position = Position(bone.location), amount = bone.amount });
        var document = new
        {
            utc = DateTimeOffset.UtcNow,
            scene = Il2Cpp.SceneList.GetCurrentSceneDetails()?.Name,
            nativeQuestState = Convert.ToBase64String(encodedQuests),
            groundDrops = records,
            scope = "read-only native capture; shared-world join, pickup transactions and guest installation not implemented"
        };
        File.WriteAllText(destination, JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }));
        return true;
    }

    private static object Position(Vector3 p) => new { x = p.x, y = p.y, z = p.z };
}
