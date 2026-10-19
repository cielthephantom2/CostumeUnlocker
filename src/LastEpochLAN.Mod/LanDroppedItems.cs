using HarmonyLib;
using LastEpochLAN.Core;

namespace LastEpochLAN.Mod;

// Only player inventory drops are published. Monster rewards stay private.
internal static class LanDroppedItems
{
    private static readonly HashSet<uint> published = new();
    private static string scene = "";
    internal static string Error = "";
    internal static long Observed;

    internal static HashSet<uint> BeforeDrop()
    {
        var items = Il2Cpp.GroundItemManager.instance?.offlineItems?.items;
        var ids = new HashSet<uint>();
        if (items != null) foreach (var item in items) ids.Add(item.id);
        return ids;
    }
    internal static void AfterDrop(Il2Cpp.Actor actor, bool success, HashSet<uint> before)
    {
        if (!success || before == null || SkillCapture.Session == null || actor == null ||
            actor.Pointer != Il2Cpp.PlayerFinder.getPlayerActor()?.Pointer) return;
        EnsureScene(Il2Cpp.SceneList.GetCurrentSceneDetails()?.Name ?? "");
        var items = Il2Cpp.GroundItemManager.instance?.offlineItems?.items;
        if (items == null) return;
        foreach (var item in items)
            if (!before.Contains(item.id)) { published.Add(item.id); Observed++; }
    }
    internal static DroppedItemState[] Capture(string currentScene)
    {
        EnsureScene(currentScene);
        var result = new List<DroppedItemState>();
        var existing = new HashSet<uint>();
        var items = Il2Cpp.GroundItemManager.instance?.offlineItems?.items;
        if (items == null) return Array.Empty<DroppedItemState>();
        foreach (var item in items)
        {
            existing.Add(item.id);
            if (!published.Contains(item.id) || item.itemData == null) continue;
            try
            {
                var name = item.itemData.getAsUnpacked().FullName;
                var state = new DroppedItemState { Id = (long)item.id + 1,
                    Name = name.Length > 128 ? name.Substring(0, 128) : name,
                    EncodedItem = Convert.ToBase64String(item.itemData.GetIDAsArray().ToArray()),
                    Position = NativeSkillVisualCapture.Point(item.location) };
                if (state.IsValid()) result.Add(state);
            }
            catch (Exception ex) { Error = ex.Message; }
        }
        published.RemoveWhere(id => !existing.Contains(id));
        return result.OrderByDescending(item => item.Id).Take(16).ToArray();
    }
    private static void EnsureScene(string currentScene)
    { if (scene != currentScene) { published.Clear(); scene = currentScene; } }
    internal static void Reset() { published.Clear(); scene = ""; Error = ""; }
}

[HarmonyPatch(typeof(Il2Cpp.ItemContainersManager), nameof(Il2Cpp.ItemContainersManager.DropItem),
    new[] { typeof(Il2Cpp.Actor), typeof(Il2Cpp.ItemData), typeof(int) })]
internal static class LanInventoryDropCapturePatch
{
    private static void Prefix(out HashSet<uint> __state)
    {
        __state = null;
        if (SkillCapture.Session == null) return;
        try { __state = LanDroppedItems.BeforeDrop(); }
        catch (Exception ex) { LanDroppedItems.Error = ex.Message; }
    }
    private static void Postfix(Il2Cpp.Actor actor, bool __result, HashSet<uint> __state)
    {
        try { LanDroppedItems.AfterDrop(actor, __result, __state); }
        catch (Exception ex) { LanDroppedItems.Error = ex.Message; }
    }
}
