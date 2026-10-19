using UnityEngine;
using UObject = UnityEngine.Object;

namespace LastEpochLAN.Mod;

// An owned, inactive gameplay prefab. Activation is a separate acceptance gate:
// native player registration and saving must be isolated before it can join combat.
internal sealed class NativeRemoteActor : IDisposable
{
    internal GameObject Root { get; private set; }
    internal Il2Cpp.Actor Actor { get; private set; }
    internal Il2Cpp.CharacterDataTracker Tracker { get; private set; }
    internal IReadOnlyDictionary<string, bool> Isolation { get; private set; }

    internal NativeRemoteActor(string peerId, int classId)
    {
        if (string.IsNullOrEmpty(peerId) || classId is < 0 or > 4) throw new ArgumentException("Invalid remote actor identity.");
        var local = Il2Cpp.PlayerFinder.getPlayerActor();
        if (local?.data == null || local.characterDataTracker?.charData == null)
            throw new InvalidOperationException("A loaded native player prefab is required.");
        Root = new GameObject("LAN private gameplay actor " + peerId); Root.SetActive(false);
        try
        {
            // Instantiate the original asset, never the live host character: Unity
            // cloning a live character also copies mutable native lists/delegates.
            Actor = Il2CppLE.AssetBundles.SoftRefExtensions.Instantiate(local.data.ActorSoftRef,
                Root.transform, false, "LastEpochLAN.RemoteActor", 0);
            if (Actor == null || Actor.gameObject.activeInHierarchy)
                throw new InvalidOperationException("Native remote actor prefab failed to stay inactive.");
            Actor.data = local.data;
            var trackerRoot = new GameObject("LAN memory-only character tracker");
            trackerRoot.SetActive(false); trackerRoot.transform.SetParent(Root.transform, false);
            Tracker = trackerRoot.AddComponent<Il2Cpp.CharacterDataTracker>();
            Tracker.actor = Actor;
            Tracker.charData = new Il2CppLE.Data.CharacterData("LAN-" + peerId) { CharacterClass = classId, IsOffline = false };
            Actor.characterDataTracker = Tracker;
            if (Actor.itemContainersManager != null) Actor.itemContainersManager.tracker = Tracker;
            if (Actor.goldTracker != null) Actor.goldTracker.dataTracker = Tracker;
            var playerAbilities = Actor.GetComponent<Il2Cpp.PlayerAbilityList>();
            if (playerAbilities != null) playerAbilities.dataTracker = Tracker;
            var characterStats = Actor.GetComponent<Il2Cpp.CharacterStats>();
            if (characterStats != null) characterStats.myCharacterDataTracker = Tracker;
            Isolation = new Dictionary<string, bool>
            {
                ["actor"] = Actor.Pointer != local.Pointer,
                ["health"] = Actor.health != null && local.health != null && Actor.health.Pointer != local.health.Pointer,
                ["stats"] = Actor.stats != null && local.stats != null && Actor.stats.Pointer != local.stats.Pointer,
                ["stateController"] = Actor.stateController != null && local.stateController != null && Actor.stateController.Pointer != local.stateController.Pointer,
                ["characterData"] = Tracker.charData.Pointer != local.characterDataTracker.charData.Pointer,
                ["localPlayer"] = Il2Cpp.PlayerFinder.getPlayerActor()?.Pointer == local.Pointer,
                ["inactive"] = !Root.activeInHierarchy && !Actor.gameObject.activeInHierarchy
            };
            if (!Isolation["actor"] || !Isolation["characterData"] || !Isolation["localPlayer"] || !Isolation["inactive"])
                throw new InvalidOperationException("Remote actor isolation invariant failed.");
        }
        catch { Dispose(); throw; }
    }

    public void Dispose()
    {
        if (Root != null) UObject.Destroy(Root);
        Root = null; Actor = null; Tracker = null;
    }
}
