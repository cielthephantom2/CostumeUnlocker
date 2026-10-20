using Il2Cpp;
using LastEpochLAN.Core;
using LastEpochLAN.Core.Encounters;
using UnityEngine;

namespace LastEpochLAN.Mod;

internal sealed class NativeHostBloodCrystalAdapter:IEncounterFamilyAdapter
{
    private sealed class Binding
    {
        internal RageOfMorditasLogic Crystal;
        internal EncounterInstance Instance;
        internal EncounterReservation Execution;
        internal bool ObservedTerminal;
    }
    private readonly EncounterHostDriver driver;
    private readonly WorldSynchronizer world;
    private readonly Dictionary<Guid,Binding> bindings=new();
    internal NativeHostBloodCrystalAdapter(EncounterHostDriver driver,WorldSynchronizer world)
    {this.driver=driver;this.world=world;}
    public EncounterKind Kind=>EncounterKind.BloodCrystal;
    internal Guid Register(RageOfMorditasLogic crystal)
    {
        if(crystal==null || crystal.GetState()!=RageOfMorditasLogic.State.NotStarted || driver.Instance==null)
            throw new InvalidOperationException("Only a ready native Blood Crystal may be registered.");
        var existing=bindings.FirstOrDefault(p=>p.Value.Crystal?.Pointer==crystal.Pointer);
        if(existing.Key!=Guid.Empty)return existing.Key;
        var id=driver.Register(Kind,Point(crystal.transform.position));
        bindings.Add(id,new(){Crystal=crystal,Instance=driver.Instance});return id;
    }
    public EncounterNativeDispatch Execute(EncounterExecution execution)
    {
        if(!bindings.TryGetValue(execution.View.Id,out var binding) || binding.Instance!=execution.Instance || binding.Crystal==null ||
            binding.Execution!=null || binding.Crystal.GetState()!=RageOfMorditasLogic.State.NotStarted ||
            execution.Reservation.Action!=EncounterAction.Activate)return EncounterNativeDispatch.RejectedBeforeExecution;
        var host=PlayerFinder.getPlayerActor()?.GetActorSync()?.TryCast<PlayerActorSync>();
        if(host==null)return EncounterNativeDispatch.RejectedBeforeExecution;
        binding.Execution=execution.Reservation;
        // The native owner remains the real Host. No remote account is forged.
        binding.Crystal.Server_OnInteractWithRageOfMorditasStart(host);
        return EncounterNativeDispatch.Started;
    }
    internal void Poll()
    {
        foreach(var pair in bindings)
        {
            var binding=pair.Value;var crystal=binding.Crystal;
            if(binding.ObservedTerminal)continue;
            if(crystal==null){driver.Observe(binding.Instance,pair.Key,false,removed:true);binding.ObservedTerminal=true;continue;}
            var state=crystal.GetState();driver.UpdatePosition(pair.Key,Point(crystal.transform.position));
            if(crystal._encounterTypeConfig!=null)
            {
                var area=new EncounterArea(Point(crystal._gauntletLocation),crystal._encounterTypeConfig.GauntletAreaRadius,state==RageOfMorditasLogic.State.Gauntlet);
                if(area.IsValid())driver.UpdateArea(pair.Key,area);
            }
            var npcs=(crystal._gauntletActors?.ToArray()??Array.Empty<Actor>()).Select(world.EncounterActorId).Where(id=>id>0).Distinct().Take(256).ToArray();
            driver.UpdateWorld(pair.Key,new(world.StatusEpoch,Array.Empty<int>(),npcs));
            if(binding.Execution!=null && state is RageOfMorditasLogic.State.ReadyForReward or RageOfMorditasLogic.State.Finished)
            {
                driver.Observe(binding.Instance,pair.Key,crystal._encounterSuccess,binding.Execution);binding.ObservedTerminal=true;
            }
        }
    }
    private static VisualPoint Point(Vector3 point)=>new(){X=point.x,Y=point.y,Z=point.z};
    public void EndInstance(EncounterInstance instance)
    {foreach(var id in bindings.Where(p=>p.Value.Instance==instance).Select(p=>p.Key).ToArray())bindings.Remove(id);}
}
