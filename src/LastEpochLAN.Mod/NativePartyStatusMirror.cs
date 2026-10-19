using HarmonyLib;
using Il2Cpp;
using LastEpochLAN.Core;

namespace LastEpochLAN.Mod;

// Mirrors only the native ailment contributions. A replica must never carry
// live ActiveAilment instances: OnUpdateTick and death procs would double-write.
internal static class NativePartyStatusMirror
{
    private sealed class Capture
    {
        internal bool Dirty=true;
        internal StatusStat[] Stats=Array.Empty<StatusStat>();
    }
    private sealed record View(Actor Actor,Il2CppSystem.Collections.Generic.List<Stats.Stat> Stats,AilmentCount[] Counts,StatusStat[] Data);
    private static readonly Dictionary<IntPtr,Capture> captures=new();
    private static readonly Dictionary<IntPtr,View> views=new();
    internal static StatusCounters Counters(Actor actor)
    {
        var receiver=actor.GetComponent<AilmentReceiver>();
        return receiver==null?new():new(receiver.numberOfActiveAilmentsThatAreCurses,receiver.numberOfActiveAilmentsThatAreBrands,
            receiver.numberOfActiveMovementAilments,receiver.numberOfNegativeAilments);
    }
    internal static void Dirty(AilmentReceiver receiver)
    {if(receiver?.actor!=null && captures.TryGetValue(receiver.actor.Pointer,out var capture)) capture.Dirty=true;}
    internal static StatusStat[] Read(Actor actor)
    {
        if(!captures.TryGetValue(actor.Pointer,out var entry)) captures[actor.Pointer]=entry=new();
        if(!entry.Dirty) return entry.Stats;
        var result=new List<StatusStat>();
        var receiver=actor.GetComponent<AilmentReceiver>();
        void Add(Il2CppSystem.Collections.Generic.List<Stats.Stat> stats)
        {if(stats!=null) foreach(var stat in stats) if(stat!=null) result.Add(NativeStatusCodec.ReadStat(stat));}
        if(receiver?.ailmentBuffs!=null)
            foreach(var pair in receiver.ailmentBuffs)
            {
                var container=pair.Value;
                var individual=container?.TryCast<AilmentReceiver.IndividualActiveBuffsForStackingAilment>();
                if(individual!=null) {foreach(var list in individual.activeBuffs) Add(list.Value);continue;}
                var single=container?.TryCast<AilmentReceiver.ActiveBuffsForNonStackingAilment>();
                if(single!=null) {Add(single.activeBuffs);continue;}
                var collected=container?.TryCast<AilmentReceiver.CollectedActiveAilmentBuffs>();
                if(collected!=null) {Add(collected.activeBuffs);continue;}
                throw new InvalidOperationException("Unknown native ailment buff container.");
            }
        if(result.Count>4096) throw new InvalidOperationException("Native shared status modifier budget exceeded.");
        entry.Stats=result.ToArray();entry.Dirty=false;return entry.Stats;
    }
    internal static void Apply(Actor actor,EnemyState state)
    {
        if(!NativePartyStatus.Enabled) return;
        var data=state.Dead?Array.Empty<StatusStat>():state.StatusModifiers;
        var counts=state.Dead?Array.Empty<AilmentCount>():state.Ailments;
        var counters=state.Dead?new StatusCounters():state.StatusCounters;
        var native=actor.GetComponent<AilmentReceiver>();
        if(native!=null)
        {
            native.numberOfActiveAilmentsThatAreCurses=counters.Curses;native.numberOfActiveAilmentsThatAreBrands=counters.Brands;
            native.numberOfActiveMovementAilments=counters.Movement;native.numberOfNegativeAilments=counters.Negative;
        }
        views.TryGetValue(actor.Pointer,out var old);
        if(old==null || !old.Counts.SequenceEqual(counts))
        {
            var receiver=actor.GetComponent<AilmentReceiver>();
            if(receiver?.ailmentStacks!=null)
            {receiver.ailmentStacks.Clear();foreach(var count in counts) receiver.ailmentStacks[(AilmentID)count.Id]=checked((int)count.Count);}
        }
        bool same=old!=null && old.Data.Length==data.Length && data.Select((stat,i)=>stat.Same(old.Data[i])).All(v=>v);
        if(same)
        {views[actor.Pointer]=old with {Counts=counts};return;}
        if(actor.stats==null) throw new InvalidOperationException("Replica has no native stats for shared status modifiers.");
        var stats=new Il2CppSystem.Collections.Generic.List<Stats.Stat>();
        foreach(var stat in data) stats.Add(NativeStatusCodec.MakeStat(stat));
        if(old?.Actor?.stats!=null) old.Actor.stats.removeStats(old.Stats,false,true);
        // dontCollapse preserves identity so removal cannot disturb map/base
        // stats or another subsystem's buffs. Replayed snapshots replace.
        actor.stats.addStats(stats,false,true);actor.stats.UpdateStatsNextFrame();
        views[actor.Pointer]=new(actor,stats,counts,data);
    }
    internal static void Forget(Actor actor)
    {
        if(actor==null) return;
        captures.Remove(actor.Pointer);
        if(views.Remove(actor.Pointer,out var view) && actor.stats!=null)
        {
            actor.stats.removeStats(view.Stats,false,true);actor.stats.UpdateStatsNextFrame();
            actor.GetComponent<AilmentReceiver>()?.ailmentStacks?.Clear();
            var receiver=actor.GetComponent<AilmentReceiver>();
            if(receiver!=null) receiver.numberOfActiveAilmentsThatAreCurses=receiver.numberOfActiveAilmentsThatAreBrands=receiver.numberOfActiveMovementAilments=receiver.numberOfNegativeAilments=0;
        }
    }
    internal static void Reset()
    {foreach(var view in views.Values.ToArray()) Forget(view.Actor);views.Clear();captures.Clear();}
}

[HarmonyPatch]
internal static class LanPartyStatusReplicaWriterPatch
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods()=>new[]{"OnUpdateTick","OnDeath","DealDamageForAilment"}
        .Select(name=>AccessTools.Method(typeof(AilmentReceiver),name));
    private static bool Prefix(AilmentReceiver __instance)=>!NativePartyStatus.Enabled || WorldSynchronizer.Active?.Room?.IsHost!=false ||
        WorldSynchronizer.Active.IsReplica(__instance.actor)!=true;
}

[HarmonyPatch]
internal static class LanPartyStatusDirtyPatch
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods()=>new[]{"changeStackCount","removeAilment"}
        .Select(name=>AccessTools.Method(typeof(AilmentReceiver),name));
    private static void Postfix(AilmentReceiver __instance)=>NativePartyStatusMirror.Dirty(__instance);
}
