using HarmonyLib;
using LastEpochLAN.Core;
using Il2CppCysharp.Threading.Tasks;
using Il2CppLE.Services;
using Il2CppLE.Services.Models;
using Il2CppLE.Services.Transitions;
using Il2CppOperationResult;
using UnityEngine;
using UnityEngine.AI;

namespace LastEpochLAN.Mod;

internal sealed class LanPartyTravel
{
    internal static LanPartyTravel Active;
    private LanSession session;
    private string targetId = "", destinationScene = "";
    private float cooldown, started;
    private bool awaitingPlacement;
    private float placementDeadline;
    private LanSession travellingSession;
    private UniTask<Status<TransitionError>>.Awaiter loading;
    internal bool Busy { get; private set; }
    internal string Message = "Party travel ready";
    internal string Error = "";
    internal long Completed;
    internal void Tick(LanSession value)
    {
        Active = this; session = value;
        if (!Busy) return;
        try
        {
            if(value!=travellingSession || value?.Connected!=true)
                throw new InvalidOperationException("Party disconnected or changed during travel.");
            if (!awaitingPlacement)
            {
                if (!loading.IsCompleted)
                {
                    if (Time.unscaledTime - started > 60) Message = "Party travel still loading";
                    return;
                }
                var result = loading.GetResult();
                if (!result.IsSuccess) throw new InvalidOperationException("Native map transition failed: " + result.Error);
                awaitingPlacement=true;placementDeadline=Time.unscaledTime+15;
            }
            var target = Find(targetId);
            if (target == null || !PartyTravelRules.SameScene(target.Scene,destinationScene))
                throw new InvalidOperationException("Party member moved or disconnected during travel.");
            var actor=Il2Cpp.PlayerFinder.getPlayerActor();
            if(!PartyTravelRules.ReadyToPlace(actor?.HasMovedToPlayerSpawn==true,
                actor?.GetActorSync()?.actorVisuals?.HasBeenInitialized==true,Il2CppLE.UI.LoadingScreen.IsVisible,
                Il2Cpp.SceneList.GetCurrentSceneDetails()?.Name,destinationScene))
            {
                if(Time.unscaledTime>placementDeadline) throw new TimeoutException("Destination player spawn did not become ready.");
                Message="Map loaded; waiting for player spawn...";return;
            }
            Place(target);Busy=false;awaitingPlacement=false;
        }
        catch (Exception ex) { Busy = false;awaitingPlacement=false; Error = ex.Message; Message = "Party travel: " + Error; MelonLoader.MelonLogger.Warning(Message); }
    }
    private PlayerState Find(string id) => session?.PlayerPositions.FirstOrDefault(p => p.Id == id && p.Id != session.LocalId && p.Available && !p.Dead);
    internal bool Request(Il2CppLE.Services.Models.UserIdentity identity)
    {
        if (identity == null || session?.Connected != true || Il2CppEHG.Multiplayer.GameplayEnvironment.IsOnlinePlay) return false;
        if (!session.PlayerPositions.Any(p => p.Id == identity.MasterAccountID && p.Id != session.LocalId)) return false;
        Request(identity.MasterAccountID); return true;
    }
    internal void Request(string id)
    {
        try
        {
            if (session?.Connected != true || Il2CppEHG.Multiplayer.GameplayEnvironment.IsOnlinePlay) throw new InvalidOperationException("Connect to the LAN party first.");
            if(LanMonolithParty.Active?.RequestHost(id)==true) return;
            if(LanMonolithParty.Active?.Busy==true) return;
            if (Busy || Time.unscaledTime < cooldown) return;
            if (LanGift.Active?.Busy == true) throw new InvalidOperationException("Wait for the Gift save to finish.");
            var actor = Il2Cpp.PlayerFinder.getPlayerActor();
            if (actor == null || !actor.HasMovedToPlayerSpawn || actor.isDying(false)) throw new InvalidOperationException("Character is not ready to travel.");
            var target = Find(id) ?? throw new InvalidOperationException("Party member is unavailable.");
            var service = ServiceProvider.OfflineTransitionService;
            var user = actor.characterDataTracker?.charData?.UserIdentity;
            if (service == null || user == null || service.IsTransitioning(user)) throw new InvalidOperationException("Map transition is already active or unavailable.");
            var current = Il2Cpp.SceneList.GetCurrentSceneDetails();
            var destination = Il2Cpp.SceneList.FindSceneDetails(target.Scene);
            if (destination == null) throw new InvalidOperationException("Unknown party destination.");
            cooldown = Time.unscaledTime + 3;
            if (PartyTravelRules.SameScene(current?.Name,target.Scene)) { Place(target); return; }
            if (!PartyTravelRules.CanUseDefaultDestination((int)destination.sceneType,Il2Cpp.SceneList.IsNonCombatZone(destination.Name)))
                throw new InvalidOperationException("Teleport into an active "+destination.sceneType+" run is not supported yet; its run context must be transferred first.");
            targetId = target.Id; destinationScene = target.Scene; started = Time.unscaledTime;
            Error="";travellingSession=session;awaitingPlacement=false;
            loading = service.Default(user, TransitionFadeType.ToBlack_Short, target.Scene, 0).GetAwaiter();
            Busy = true; Message = "Travelling to " + target.Name;
        }
        catch (Exception ex) { Error = ex.Message; Message = "Party travel: " + Error; MelonLoader.MelonLogger.Warning(Message); }
    }
    private void Place(PlayerState target)
    {
        var actor = Il2Cpp.PlayerFinder.getPlayerActor();
        if (actor == null || !actor.HasMovedToPlayerSpawn || actor.isDying(false) || !PartyTravelRules.SameScene(Il2Cpp.SceneList.GetCurrentSceneDetails()?.Name,target.Scene))
            throw new InvalidOperationException("Character has not reached the party member's map.");
        var center = new Vector3(target.X, target.Y, target.Z);
        var desired = center + Quaternion.Euler(0, target.Yaw, 0) * Vector3.right * 2;
        NavMeshHit hit;
        int areas = actor.navMeshAgent?.areaMask ?? NavMesh.AllAreas;
        if (!NavMesh.SamplePosition(desired, out hit, 3, areas) && !NavMesh.SamplePosition(center, out hit, 3, areas))
            throw new InvalidOperationException("No walkable position near party member.");
        var scene = Il2Cpp.SceneList.activeScene;
        Il2Cpp.PlayerSpawnManager.PlacePlayerAtPosition(actor, hit.position, Quaternion.Euler(0, target.Yaw, 0), hit.position,
            scene, false, 0, false, false, true, new Il2CppSystem.Nullable<UnityEngine.SceneManagement.Scene>(scene), false, false);
        Completed++; Error = ""; Message = "Arrived beside " + target.Name;
    }
    internal void PlaceBeside(string id)
    {
        var target=Find(id);if(target!=null) Place(target);
    }
    internal void Disconnect() { session = null; targetId = "";Busy=false;awaitingPlacement=false;travellingSession=null; }
}

[HarmonyPatch(typeof(Il2CppLE.UI.Party.UnityUI.PartyOverviewPanel), nameof(Il2CppLE.UI.Party.UnityUI.PartyOverviewPanel.RequestPortal))]
internal static class NativeLanPartyPortalPatch
{
    private static bool Prefix(UserIdentity target) => LanPartyTravel.Active?.Request(target) != true;
}
