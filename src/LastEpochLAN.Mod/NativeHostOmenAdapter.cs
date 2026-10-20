using Il2Cpp;
using Il2CppCysharp.Threading.Tasks;
using LastEpochLAN.Core;
using LastEpochLAN.Core.Encounters;
using UnityEngine;

namespace LastEpochLAN.Mod;

// Host-owned binding, constructed by a room runtime only after its full
// presentation/reward capability gate passes. No Harmony or solo hooks.
// This adapter does not redirect/duplicate native rewards on its own.
internal sealed class NativeHostOmenAdapter:IEncounterFamilyAdapter
{
    private sealed class Binding
    {
        internal ObserverWindowLogic Window;
        internal EncounterInstance Instance;
        internal EncounterReservation Execution;
        internal UniTask.Awaiter Starting;
        internal bool AwaitingStart,ObservedTerminal;
    }
    private readonly EncounterHostDriver driver;
    private readonly WorldSynchronizer world;
    private readonly Dictionary<Guid,Binding> bindings=new();
    internal NativeHostOmenAdapter(EncounterHostDriver driver,WorldSynchronizer world)
    {this.driver=driver;this.world=world;}
    public EncounterKind Kind=>EncounterKind.OmenWindow;
    internal Guid Register(ObserverWindowLogic window)
    {
        if(window==null || !window.initialized || window.state!=ObserverWindowLogic.State.NotStarted || driver.Instance==null)
            throw new InvalidOperationException("Only a ready native Omen may be registered.");
        if(bindings.Any(p=>p.Value.Window?.Pointer==window.Pointer))return bindings.Single(p=>p.Value.Window?.Pointer==window.Pointer).Key;
        var id=driver.Register(Kind,Point(window.transform.position));
        bindings.Add(id,new(){Window=window,Instance=driver.Instance});return id;
    }
    public EncounterNativeDispatch Execute(EncounterExecution execution)
    {
        if(!bindings.TryGetValue(execution.View.Id,out var binding) || binding.Instance!=execution.Instance || binding.Window==null ||
            binding.Execution!=null || binding.Window.state!=ObserverWindowLogic.State.NotStarted || !binding.Window.initialized ||
            execution.Reservation.Action!=EncounterAction.Activate)return EncounterNativeDispatch.RejectedBeforeExecution;
        binding.Execution=execution.Reservation;
        binding.Starting=binding.Window.StartEncounter(Il2CppSystem.Threading.CancellationToken.None).GetAwaiter();
        binding.AwaitingStart=true;return EncounterNativeDispatch.Started;
    }
    internal void Poll()
    {
        foreach(var pair in bindings)
        {
            var binding=pair.Value;var window=binding.Window;
            if(binding.ObservedTerminal)continue;
            if(window==null){driver.Observe(binding.Instance,pair.Key,false,removed:true);binding.ObservedTerminal=true;continue;}
            driver.UpdatePosition(pair.Key,Point(window.transform.position));
            var area=new EncounterArea(Point(window.transform.position),window.radius,window.state==ObserverWindowLogic.State.Fight);
            if(area.IsValid())driver.UpdateArea(pair.Key,area);
            var bosses=Ids(window.omenActors?.ToArray());
            var npcs=Ids(window.encounterWaveActors?.ToArray()).Except(bosses).ToArray();
            driver.UpdateWorld(pair.Key,new(world.StatusEpoch,bosses.Take(16).ToArray(),npcs.Take(256).ToArray()));
            if(binding.Execution==null)continue;
            try
            {
                if(binding.AwaitingStart && binding.Starting.IsCompleted)
                {binding.Starting.GetResult();binding.AwaitingStart=false;}
                if(window.EncounterHasBeenCompleted)
                {
                    driver.Observe(binding.Instance,pair.Key,window.encounterSuccess,binding.Execution);
                    binding.ObservedTerminal=true;
                }
            }
            catch
            {
                driver.Observe(binding.Instance,pair.Key,false,binding.Execution);binding.ObservedTerminal=true;throw;
            }
        }
    }
    private int[] Ids(IEnumerable<Actor> actors)=>(actors??Array.Empty<Actor>()).Select(world.EncounterActorId).Where(id=>id>0).Distinct().ToArray();
    private static VisualPoint Point(Vector3 point)=>new(){X=point.x,Y=point.y,Z=point.z};
    public void EndInstance(EncounterInstance instance)
    {
        foreach(var id in bindings.Where(p=>p.Value.Instance==instance).Select(p=>p.Key).ToArray())bindings.Remove(id);
    }
}
