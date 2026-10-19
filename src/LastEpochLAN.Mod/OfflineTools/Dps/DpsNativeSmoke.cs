using Il2Cpp;
using LastEpochLAN.Core;
using UnityEngine;

namespace LastEpochLAN.Mod.OfflineTools.Dps;

// Private QA only. Uses a disposable native monster and actual damage entry
// points; never targets the user's character or changes their installed mods.
internal sealed class DpsNativeSmoke : IDisposable
{
    private readonly Actor local, target;
    private readonly float deadline=Time.unscaledTime+20;
    private GameObject sourceRoot;
    private Ability firstAbility, secondAbility;
    internal bool Complete;
    internal object Observations;
    internal DpsNativeSmoke(Actor player)
    {
        if(!Environment.GetCommandLineArgs().Contains("--lelan-offline-tools-smoke")) throw new InvalidOperationException("Private DPS QA flag required.");
        local=player;
        ActorData template=null;
        foreach(var data in ActorDataList.instance().data)
            if(data!=null && data.actorType==ActorData.Type.Normal) {template=data;break;}
        if(template==null) throw new InvalidOperationException("No native DPS QA target template.");
        target=template.spawn(local.position()+new Vector3(5,0,5),0,null,AbilitySync.PositionSyncType.Absolute,null,"",true,0,
            new Il2CppSystem.Nullable<UnityEngine.SceneManagement.Scene>(local.gameObject.scene),true);
    }
    internal bool Tick()
    {
        if(Complete) return true;
        if(Time.unscaledTime>deadline) throw new TimeoutException("DPS native target did not initialize.");
        if(target?.health==null || target.health.maxHealth<=0 || target.protection==null) return false;
        if(target.stateController!=null) target.stateController.enabled=false;
        if(target.navMeshAgent!=null) target.navMeshAgent.enabled=false;
        target.health.maxHealth=100000;target.health.currentHealth=100000;target.health.damageable=true;
        sourceRoot=new GameObject("Private native DPS source");sourceRoot.SetActive(false);
        var source=sourceRoot.AddComponent<DamageEnemiesWithBeam>();
        source.refs=sourceRoot.AddComponent<CreationReferences>();source.refs.creator=local;
        source.refs.locationCreatedFrom=local.position();
        firstAbility=ScriptableObject.CreateInstance<Ability>();firstAbility.name="DPS QA First";firstAbility.playerAbilityID="DpsQA1";
        secondAbility=ScriptableObject.CreateInstance<Ability>();secondAbility.name="DPS QA Second";secondAbility.playerAbilityID="DpsQA2";
        source.refs.thisAbility=firstAbility;source.refs.originalAbility=firstAbility;
        source.damageStats=new DamageStats {damage=new float[]{16,0,0,0,0,0,0},isHit=false,critChance=0,critMultiplier=1};
        source.damageModifier=1;
        DamageMeterOverlay.ResetCombatData();
        float before=target.health.currentHealth;
        source.applyDamage(target);
        float first=DpsRules.HealthLoss(before,target.health.currentHealth);
        if(first<=0) throw new InvalidOperationException("Native DPS first hit caused no HP damage.");
        if(DamageMeterOverlay.Readout.Hits!=1 || Math.Abs(DamageMeterOverlay.Readout.Total-first)>0.01)
            throw new InvalidOperationException("Native Actor damage was missed or double counted: "+DamageMeterOverlay.Readout);
        source.refs.thisAbility=secondAbility;source.refs.originalAbility=secondAbility;
        before=target.health.currentHealth;source.applyDamage(target.protection,1);
        float second=DpsRules.HealthLoss(before,target.health.currentHealth);
        var read=DamageMeterOverlay.Readout;
        if(second<=0 || read.Hits!=2 || read.Skills!=2 || Math.Abs(read.Total-first-second)>0.01)
            throw new InvalidOperationException("Native protection path/skill attribution failed: "+read);
        NativeAilmentSynchronization.ApplyNativeFixture(target,local,new ResolvedAilment {Id=(int)AilmentID.Ignite,Stacks=1,Duration=4,Damage=new float[]{1,0,0,0,0,0,0}});
        var receiver=target.GetComponent<AilmentReceiver>();
        before=target.health.currentHealth;double meterBefore=read.Total;
        receiver.OnUpdateTick(0.5f);
        float dot=DpsRules.HealthLoss(before,target.health.currentHealth);
        if(dot<=0 || Math.Abs(DamageMeterOverlay.Readout.Total-meterBefore-dot)>0.01)
            throw new InvalidOperationException("Native DoT HP loss not captured by DPS: loss="+dot+", meterDelta="+(DamageMeterOverlay.Readout.Total-meterBefore)+", "+OfflineDamageHooks.DotDiagnostic);
        receiver.cleanseNegativeAilments();
        Observations=new {nativeActorDamage=first,nativeProtectionDamage=second,nativeDotDamage=dot,
            total=DamageMeterOverlay.Readout.Total,hits=DamageMeterOverlay.Readout.Hits,skills=DamageMeterOverlay.Readout.Skills,
            actualHpLoss=true,noDuplicateOverloadCount=true,offlineOnly=true,coOpMeterValidated=false};
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),"UserData","LastEpochLAN.dps-combat-qa.json"),System.Text.Json.JsonSerializer.Serialize(Observations));
        Complete=true;return true;
    }
    public void Dispose()
    {
        if(target!=null) HostEnemyReplicas.DestroyActor(target);
        if(sourceRoot!=null) UnityEngine.Object.Destroy(sourceRoot);
        if(firstAbility!=null) UnityEngine.Object.Destroy(firstAbility);
        if(secondAbility!=null) UnityEngine.Object.Destroy(secondAbility);
    }
}
