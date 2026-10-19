using UnityEngine;
using LastEpochLAN.Core;

namespace LastEpochLAN.Mod;

// Real local receiver ticks; remote data is visual-only and cannot create DoT.
internal sealed class NativeStatusCampaignSmoke : IDisposable
{
    private readonly Il2Cpp.Actor local;
    private readonly Il2Cpp.Actor target;
    private readonly float deadline=Time.unscaledTime+20;
    internal bool Complete {get;private set;}
    internal NativeStatusCampaignSmoke()
    {
        local=Il2Cpp.PlayerFinder.getPlayerActor() ?? throw new InvalidOperationException("Status QA requires private campaign actor.");
        Il2Cpp.ActorData template=null;
        foreach(var data in Il2Cpp.ActorDataList.instance().data)
            if(data!=null && data.actorType==Il2Cpp.ActorData.Type.Normal) {template=data;break;}
        if(template==null) throw new InvalidOperationException("No native NPC template for status QA.");
        target=template.spawn(local.position()+new Vector3(130,0,130),0,null,Il2Cpp.AbilitySync.PositionSyncType.Absolute,null,"",true,0,
            new Il2CppSystem.Nullable<UnityEngine.SceneManagement.Scene>(local.gameObject.scene),true);
    }
    internal void Tick()
    {
        if(Complete) return;
        if(Time.unscaledTime>deadline) throw new TimeoutException("Native source-owned status QA timed out.");
        var receiver=target?.GetComponent<Il2Cpp.AilmentReceiver>();
        if(receiver==null || target.health==null || target.health.maxHealth<=0 || target.GetActorSync()?.actorVisuals?.ailmentVFXCreator==null) return;
        NativeAilmentSynchronization.ApplyNativeFixture(target,local,new ResolvedAilment {Id=(int)Il2Cpp.AilmentID.Ignite,Stacks=1,Duration=4,
            Damage=new float[]{1,0,0,0,0,0,0}});
        int originalInstances=receiver.ailments.Count;
        var own=NativeAilmentSynchronization.Read(target);
        if(!own.Any(a=>a.Id==(int)Il2Cpp.AilmentID.Ignite && a.Count==1)) throw new InvalidOperationException("Local ignite source was removed or delegated.");
        var ledger=new SourceStatusVisuals();ledger.BeginEpoch(1);
        ledger.Observe(new StatusVisualReport {Source="remote",Sequence=1,Epoch=1,EnemyId=1,Groups=new[]{
            new StatusVisualGroup((int)Il2Cpp.AilmentID.Poison,5,2),new StatusVisualGroup((int)Il2Cpp.AilmentID.Ignite,2,3)}},10);
        float before=target.health.currentHealth;
        var merged=ledger.Merge(1,own,10);
        NativeAilmentSynchronization.Present(target,merged);NativeAilmentSynchronization.Present(target,merged);
        var vfx=target.GetActorSync().actorVisuals.ailmentVFXCreator;
        if(vfx.getInstanceCount(Il2Cpp.AilmentID.Ignite)!=3 || vfx.getInstanceCount(Il2Cpp.AilmentID.Poison)!=5)
            throw new InvalidOperationException("Combined native fire/poison VFX counts were not applied.");
        // A native local change must not overwrite the combined party view.
        vfx.setInstanceCount(Il2Cpp.AilmentID.Ignite,1,false);
        if(vfx.getInstanceCount(Il2Cpp.AilmentID.Ignite)!=3) throw new InvalidOperationException("Local native VFX overwrote party aggregate.");
        if(target.health.currentHealth!=before || receiver.ailments.Count!=originalInstances || receiver.ailmentStacks.ContainsKey(Il2Cpp.AilmentID.Poison))
            throw new InvalidOperationException("Remote visual snapshot created a second native damage/status receiver instance.");
        receiver.OnUpdateTick(0.5f);
        if(target.health.currentHealth>=before || receiver.ailments.Count!=originalInstances)
            throw new InvalidOperationException("Source-owned native DoT did not keep ticking.");
        NativeAilmentSynchronization.Present(target,ledger.Merge(1,NativeAilmentSynchronization.Read(target),12.1));
        if(vfx.getInstanceCount(Il2Cpp.AilmentID.Poison)!=0 || vfx.getInstanceCount(Il2Cpp.AilmentID.Ignite)!=3)
            throw new InvalidOperationException("Independent native poison expiry removed the surviving fire visual.");
        receiver.cleanseNegativeAilments();NativeAilmentSynchronization.Dirty(receiver);
        ledger.Observe(new StatusVisualReport {Source="remote",Sequence=2,Epoch=1,EnemyId=1},12.2);
        NativeAilmentSynchronization.Present(target,ledger.Merge(1,NativeAilmentSynchronization.Read(target),12.2));
        if(vfx.getInstanceCount(Il2Cpp.AilmentID.Ignite)!=0 || vfx.getInstanceCount(Il2Cpp.AilmentID.Poison)!=0)
            throw new InvalidOperationException("Native cleanse left stale combined VFX.");
        Complete=true;
    }
    public void Dispose()
    {
        NativeAilmentSynchronization.Reset();
        if(target!=null) HostEnemyReplicas.DestroyActor(target);
    }
}
