using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using Il2CppLE.Core;
using Il2CppLE.Factions;
using UnityEngine;
using Lens = Il2CppSystem.Nullable<Il2CppLE.Factions.ProphecySlot.LensType>;

namespace LastEpochLAN.Mod.OfflineTools;

// Only defer the native spawn callback. TryTriggerReward still owns charge use,
// progress, double rewards and fulfillment events, and is never replayed.
internal static class ProphecyRewards
{
    private sealed class Work
    {
        internal ProphecySlotReward Reward;
        internal Actor Player;
        internal string Character;
        internal Lens Lens;
        internal RngElement LensRng;
        internal uint LensState;
        internal int Remaining,CreatedFrame;
    }
    private static readonly Queue<Work> pending=new();
    private static bool replaying;
    private static int lastFrame=-1;
    private static long queued,completed,failed,flushes;
    private static string error="";

    internal static bool Defer(ProphecySlotReward reward,Actor actor,Lens lens)
    {
        if(replaying || !OfflineToolkit.Ready || actor==null || reward==null ||
            actor.Pointer!=PlayerFinder.getPlayerActor()?.Pointer || reward.itemsDropped<=0)return false;
        // Snapshot before acknowledging the callback. Never modify shared reward
        // catalog assets: the player can change slot/lens while drops are pending.
        ProphecySlotReward copy=null;
        var rng=ProphecySlotReward.rng;
        uint state=rng.State;
        try
        {
            copy=UnityEngine.Object.Instantiate(reward);
            copy.hideFlags=HideFlags.HideAndDontSave;
            var savedLens=lens.HasValue ? new Lens(lens.Value) : new Lens();
            bool replacement=false;
            var kind=lens.HasValue ? (int)lens.Value : -1;
            // Native rolls these once per SpawnReward call, not per item.
            // Reserve exactly those rolls now. Each chunk gets a private RNG
            // reset to that state; the native lens code then sees the same roll.
            // Native SpawnReward uses this RNG only for these lens decisions.
            if(kind==1)rng.Roll(ProphecyData.Data.duplicationLensChance);
            if(kind==9)
            {
                rng.Roll(ProphecyData.Data.celestialScalesDuplicationChance);
                replacement=rng.Roll(ProphecyData.Data.celestialScalesGroleEggReplacementChance);
            }
            bool split=!replacement && (copy.itemGenerationType==ProphecySlotReward.ItemGenerationType.GenericEquipment ||
                copy.itemGenerationType==ProphecySlotReward.ItemGenerationType.SpecificEquipment);
            int units=split ? copy.itemsDropped : 1;
            if(split)copy.itemsDropped=1;
            pending.Enqueue(new Work{Reward=copy,Player=actor,Character=actor.characterDataTracker.charData.Id,
                Lens=savedLens,LensRng=kind is 1 or 9 ? new RngElement(state) : null,LensState=state,
                Remaining=units,CreatedFrame=Time.frameCount});
            queued+=units;
            return true;
        }
        catch(Exception ex)
        {
            // The native callback has not been suppressed yet; restore the RNG
            // reservation and let the original spawn deliver the full reward.
            rng.State=state;
            if(copy!=null)UnityEngine.Object.Destroy(copy);
            error="Prophecy queue preparation: "+ex.GetBaseException().Message;
            MelonLoader.MelonLogger.Warning(error);
            return false;
        }
    }

    internal static void Tick()
    {
        if(pending.Count==0 || lastFrame==Time.frameCount)return;
        var work=pending.Peek();
        if(work.CreatedFrame==Time.frameCount || !OfflineToolkit.Ready)return;
        lastFrame=Time.frameCount;
        RunOne();
    }
    private static void RunOne()
    {
        var work=pending.Peek();
        if(work.Player==null || PlayerFinder.getPlayerActor()?.Pointer!=work.Player.Pointer ||
            work.Player.characterDataTracker?.charData?.Id!=work.Character)
            throw new InvalidOperationException("Pending Prophecy reward belongs to a different/unloaded character");
        var global=ProphecySlotReward.rng;
        // A callback can throw after dropping something. Consume this unit first
        // and never automatically replay an uncertain partial drop.
        work.Remaining--;
        try
        {
            replaying=true;
            if(work.LensRng!=null){work.LensRng.State=work.LensState;ProphecySlotReward.rng=work.LensRng;}
            NativeProphecyRewardSpawn.Deliver(work.Reward,work.Player,work.Lens);
            completed++;
        }
        catch(Exception ex)
        {
            failed++;error="Prophecy native spawn failed; unit not retried: "+ex.GetBaseException().Message;
            MelonLoader.MelonLogger.Error(error);
            throw;
        }
        finally
        {
            ProphecySlotReward.rng=global;replaying=false;
            if(work.Remaining==0){pending.Dequeue();UnityEngine.Object.Destroy(work.Reward);}
        }
    }
    // Finish before native ground-item caching/unload or character save/exit.
    // This deliberate synchronous fallback can stall a transition, but prevents
    // moving the reward to another character or discarding uncreated units.
    internal static void Flush()
    {
        if(replaying || pending.Count==0)return;
        flushes++;
        while(pending.Count>0)RunOne();
    }
    internal static object Read()=>new{pendingJobs=pending.Count,pendingUnits=pending.Sum(w=>w.Remaining),
        queuedUnits=queued,completedUnits=completed,failedUnits=failed,transitionFlushes=flushes,error,
        nativeHookError=NativeProphecyRewardSpawn.Error,
        policy="one native equipment unit per frame; whole native non-equipment/boss reward per frame; charge processing unchanged"};
}

[HarmonyPatch]
internal static class OfflineProphecyTransitionPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(GroundItemManager),nameof(GroundItemManager.BeforeSceneUnloaded));
        yield return AccessTools.Method(typeof(UIBase),nameof(UIBase.ExitToCharacterSelect));
        yield return AccessTools.Method(typeof(UIBase),nameof(UIBase.OnApplicationQuit));
        yield return AccessTools.Method(typeof(ClientSceneService),nameof(ClientSceneService.LoadSceneAsync));
    }
    private static void Prefix()=>ProphecyRewards.Flush();
}
