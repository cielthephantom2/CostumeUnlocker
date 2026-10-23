using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace LastEpochLAN.Mod.OfflineTools;

// Fixed history for diagnosing intermittent death stalls. Native calls are
// inclusive boundaries; async methods cover only their synchronous entry work.
// No logging, serialization or disk I/O occurs inside a death/loot callback.
internal static class DeathHitchTrace
{
    private readonly record struct Entry(long UtcTicks,int Frame,string Area,double Milliseconds,
        long ManagedBytes,int Gc0,int Gc1,int Gc2);
    private static readonly Entry[] history=new Entry[64];
    private static int cursor,count,lastFrame=-1;
    private static long previousFrame;
    private static int previous0,previous1,previous2;
    internal static void Frame()
    {
        if(!OfflineToolkit.Ready){previousFrame=0;lastFrame=-1;return;}
        int frame=Time.frameCount;
        if(frame==lastFrame)return;
        long now=Stopwatch.GetTimestamp();
        int c0=GC.CollectionCount(0),c1=GC.CollectionCount(1),c2=GC.CollectionCount(2);
        if(previousFrame!=0)
        {
            double ms=(now-previousFrame)*1000d/Stopwatch.Frequency;
            if(ms>=100)Record(frame,"Frame interval (includes native/render/wait)",ms,0,c0-previous0,c1-previous1,c2-previous2);
        }
        previousFrame=now;lastFrame=frame;previous0=c0;previous1=c1;previous2=c2;
    }
    private static void Record(int frame,string area,double ms,long bytes,int c0,int c1,int c2)
    {
        history[cursor]=new(DateTime.UtcNow.Ticks,frame,area,ms,bytes,c0,c1,c2);
        cursor=(cursor+1)%history.Length;count=Math.Min(count+1,history.Length);
    }
    internal static object Read()
    {
        var entries=new Entry[count];
        for(int i=0;i<count;i++)entries[i]=history[(cursor-count+i+history.Length)%history.Length];
        return new {basis="64 most recent native calls >=2ms and frame intervals >=100ms; inclusive timings, not additive; GC counts refer to managed runtime only; no GPU/native GC measurement",entries};
    }
    internal readonly struct Sample
    {
        private readonly string area;
        private readonly long started,allocated;
        private readonly int frame,c0,c1,c2;
        internal Sample(string name)
        {
            area=name;started=Stopwatch.GetTimestamp();allocated=GC.GetAllocatedBytesForCurrentThread();
            frame=Time.frameCount;c0=GC.CollectionCount(0);c1=GC.CollectionCount(1);c2=GC.CollectionCount(2);
        }
        internal void End()
        {
            if(area==null)return;
            double ms=(Stopwatch.GetTimestamp()-started)*1000d/Stopwatch.Frequency;
            if(ms>=2)Record(frame,area,ms,Math.Max(0,GC.GetAllocatedBytesForCurrentThread()-allocated),
                GC.CollectionCount(0)-c0,GC.CollectionCount(1)-c1,GC.CollectionCount(2)-c2);
        }
    }
}

[HarmonyPatch]
internal static class OfflineDeathHitchTracePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Dying),nameof(Dying.onEnter));
        yield return AccessTools.Method(typeof(DeathItemDrop),nameof(DeathItemDrop.OnDeath));
        foreach(var method in typeof(ItemDrop).GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static))
            if(method.Name==nameof(ItemDrop.DropItem))yield return method;
        yield return AccessTools.Method(typeof(GroundItemManager),nameof(GroundItemManager.dropItemForPlayer));
        yield return AccessTools.Method(typeof(GroundItemManager),nameof(GroundItemManager.pickupItem),
            new[]{typeof(Actor),typeof(uint),typeof(StackableItemFlags)});
    }
    private static void Prefix(MethodBase __originalMethod,out DeathHitchTrace.Sample __state)
        =>__state=OfflineToolkit.Enabled ? new(__originalMethod.Name) : default;
    private static Exception Finalizer(DeathHitchTrace.Sample __state,Exception __exception)
    {__state.End();return __exception;}
}
