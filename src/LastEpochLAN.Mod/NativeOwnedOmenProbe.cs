using Il2Cpp;
using Il2CppCysharp.Threading.Tasks;
using LastEpochLAN.Core;
using LastEpochLAN.Core.Encounters;
using UnityEngine;

namespace LastEpochLAN.Mod;

// Private engine test: real Host Omen lifecycle, simulated TCP client commands.
// Enemy deaths are induced in QA, not representative combat or two-PC testing.
internal sealed class NativeOwnedOmenProbe:IDisposable
{
    private int stage,kills,maxActors,maxBosses;
    private float deadline;
    private LanSession host,client;
    private Task<LanSession> joining;
    private EncounterHostDriver driver;
    private NativeHostOmenAdapter adapter;
    private readonly WorldSynchronizer world=new();
    private ObserverWindowLogic window;
    private UniTask<ObserverWindowLogic>.Awaiter creating;
    private Guid encounter;
    private EncounterInstance instance;
    private bool sent;
    private readonly List<object> samples=new();
    private float nextSample;
    internal object Tick(Actor actor)
    {
        if(!Environment.GetCommandLineArgs().Contains("--lelan-owned-omen-smoke"))throw new InvalidOperationException("Private native Omen QA required.");
        if(stage==0)
        {
            host=LanSession.Host("OmenNativeQA","omen-native-qa","qa-private",0,System.Net.IPAddress.Loopback);
            var position=actor.position()+actor.transform.right*7;
            string scene=SceneList.GetCurrentSceneDetails().Name;
            host.Publish(new(){Scene=scene,X=position.x,Y=position.y,Z=position.z,Available=true,Cooperative=true,GiftIdentity=Guid.NewGuid().ToString("N"),CharacterId="disposable-native-host"});
            joining=LanSession.JoinAsync("127.0.0.1",host.Port,"OmenCommandQA","omen-native-qa","qa-private");
            driver=new(host);adapter=new(driver,world);driver.Attach(adapter);
            instance=new(Guid.NewGuid(),1,scene,"");driver.Begin(instance);
            var config=ObserverWindowLogic.Config.Create(ObserverWindowData.WindowType.Regular,new Il2CppSystem.Nullable<int>(1));
            config.canFail=false; // Isolate lifecycle from unattended player positioning.
            creating=ObserverWindowLogic.Create(position,0,actor.gameObject.scene,config).GetAwaiter();
            deadline=Time.unscaledTime+120;stage=1;return null;
        }
        if(Time.unscaledTime>deadline)return Finish("Native Omen lifecycle timed out");
        if(stage==1)
        {
            if(!creating.IsCompleted || !joining.IsCompleted)return null;
            window=creating.GetResult();client=joining.GetAwaiter().GetResult();
            if(window==null || !window.initialized)return Finish("Native Omen did not initialize");
            client.Publish(host.PlayerPositions.Single(p=>p.Id==host.LocalId) with {Id="",Name="",GiftIdentity=Guid.NewGuid().ToString("N"),CharacterId="simulated-client"});
            if(window.state!=ObserverWindowLogic.State.NotStarted)return Finish("Native Omen activated before owned registration");
            encounter=adapter.Register(window);driver.Pump();stage=2;
        }
        world.Tick(host,instance.Scene);
        adapter.Poll();driver.Pump();
        if(stage==2)
        {
            var entry=client.Encounters?.Snapshot?.Entries.SingleOrDefault(e=>e.Id==encounter);
            if(!sent && entry!=null && host.PlayerPositions.Any(p=>p.Id==client.LocalId && p.CharacterId=="simulated-client"))
                sent=client.SendEncounter(new(instance.Id,instance.Epoch,encounter,entry.Revision,1,EncounterAction.Activate));
            var targets=(window.encounterWaveActors?.ToArray()??Array.Empty<Actor>()).Concat(window.omenActors?.ToArray()??Array.Empty<Actor>())
                .Where(a=>a!=null).GroupBy(a=>a.Pointer).Select(g=>g.First()).ToArray();
            maxActors=Math.Max(maxActors,targets.Length);maxBosses=Math.Max(maxBosses,window.omenActors?.Count??0);
            foreach(var target in targets)
                if(target.health!=null && target.health.currentHealth>0 && !target.isDying(false))
                {target.health.currentHealth=0;target.dying.die(true);kills++;}
            if(Time.unscaledTime>=nextSample)
            {nextSample=Time.unscaledTime+1;samples.Add(new{state=window.state.ToString(),sent,kills,maxActors,maxBosses,phase=driver.Snapshot?.Entries.Single(e=>e.Id==encounter).Phase.ToString()});}
            if(client.Encounters?.Snapshot?.Entries.SingleOrDefault(e=>e.Id==encounter)?.Phase==EncounterPhase.Completed)
                return Finish("");
        }
        return null;
    }
    private object Finish(string error)
    {
        var result=new{passed=error=="" && sent && kills>0 && maxBosses>0,error,sent,kills,maxActors,maxBosses,
            scope="Private native Host Omen + simulated TCP client; induced enemy deaths and canFail=false, not real combat, private reward delivery, or two-PC acceptance",samples};
        Dispose();return result;
    }
    public void Dispose()
    {
        driver?.EndInstance();driver=null;world.Reset();client?.Dispose();host?.Dispose();client=host=null;
        if(window!=null)UnityEngine.Object.Destroy(window.gameObject);window=null;
    }
}
