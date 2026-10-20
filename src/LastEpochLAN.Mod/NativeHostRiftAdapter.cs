using Il2Cpp;
using LastEpochLAN.Core;
using LastEpochLAN.Core.Encounters;
using UnityEngine;

namespace LastEpochLAN.Mod;

internal sealed class NativeHostRiftAdapter:IEncounterFamilyAdapter
{
    private sealed class Binding
    {
        internal TimeBeastRift Rift;
        internal EncounterInstance Instance;
        internal EncounterReservation Execution;
        internal int BossId;
        internal float Started;
        internal bool ObservedBoss,ObservedTerminal;
    }
    private readonly EncounterHostDriver driver;
    private readonly WorldSynchronizer world;
    private readonly Dictionary<Guid,Binding> bindings=new();
    internal NativeHostRiftAdapter(EncounterHostDriver driver,WorldSynchronizer world)
    {this.driver=driver;this.world=world;}
    public EncounterKind Kind=>EncounterKind.RiftBeast;
    internal Guid Register(TimeBeastRift rift)
    {
        if(rift==null || rift.HasBeenTriggered || driver.Instance==null)throw new InvalidOperationException("Only an unopened native Rift may be registered.");
        var previous=bindings.FirstOrDefault(p=>p.Value.Rift?.Pointer==rift.Pointer);
        if(previous.Key!=Guid.Empty)return previous.Key;
        var id=driver.Register(Kind,Point(rift.transform.position));bindings.Add(id,new(){Rift=rift,Instance=driver.Instance});return id;
    }
    public EncounterNativeDispatch Execute(EncounterExecution execution)
    {
        if(!bindings.TryGetValue(execution.View.Id,out var binding) || binding.Instance!=execution.Instance || binding.Rift==null ||
            binding.Rift.HasBeenTriggered || binding.Execution!=null || execution.Reservation.Action!=EncounterAction.Activate)return EncounterNativeDispatch.RejectedBeforeExecution;
        binding.Execution=execution.Reservation;binding.Started=Time.unscaledTime;
        binding.Rift.Open();return EncounterNativeDispatch.Started;
    }
    internal void Poll()
    {
        foreach(var pair in bindings)
        {
            var binding=pair.Value;var rift=binding.Rift;
            if(binding.ObservedTerminal)continue;
            if(rift==null){driver.Observe(binding.Instance,pair.Key,false,removed:true);binding.ObservedTerminal=true;continue;}
            driver.UpdatePosition(pair.Key,Point(rift.transform.position));
            if(binding.Execution==null)continue;
            var dying=rift.timeBeastDyingState;
            if(dying!=null)
            {
                binding.ObservedBoss=true;
                int current=world.EncounterActorId(dying.GetComponent<Actor>());if(current>0)binding.BossId=current;
            }
            var npcs=(rift.subscribedExtraActors?.ToArray()??Array.Empty<Dying>()).Where(d=>d!=null)
                .Select(d=>world.EncounterActorId(d.GetComponent<Actor>())).Where(id=>id>0 && id!=binding.BossId).Distinct().Take(256).ToArray();
            driver.UpdateWorld(pair.Key,new(world.StatusEpoch,binding.BossId>0?new[]{binding.BossId}:Array.Empty<int>(),npcs));
            if(dying!=null && dying.isDying(false))
            {driver.Observe(binding.Instance,pair.Key,true,binding.Execution);binding.ObservedTerminal=true;}
            else if(binding.ObservedBoss && dying==null || !rift.HasBeenTriggered && Time.unscaledTime-binding.Started>10)
            {driver.Observe(binding.Instance,pair.Key,false,binding.Execution);binding.ObservedTerminal=true;}
        }
    }
    private static VisualPoint Point(Vector3 point)=>new(){X=point.x,Y=point.y,Z=point.z};
    public void EndInstance(EncounterInstance instance)
    {foreach(var id in bindings.Where(p=>p.Value.Instance==instance).Select(p=>p.Key).ToArray())bindings.Remove(id);}
}
