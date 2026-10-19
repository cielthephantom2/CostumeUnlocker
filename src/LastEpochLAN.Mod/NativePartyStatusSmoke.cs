using Il2Cpp;
using LastEpochLAN.Core;
using UnityEngine;

namespace LastEpochLAN.Mod;

internal sealed class NativePartyStatusSmoke : IDisposable
{
    private readonly Actor local,target,replica;
    private readonly float deadline=Time.unscaledTime+25;
    internal object Observations;
    internal bool Complete;
    private NativePartyStatusTransportSmoke transport;
    private object nativeObservations;
    internal NativePartyStatusSmoke(Actor player)
    {
        if(!Environment.GetCommandLineArgs().Contains("--lelan-offline-tools-smoke")) throw new InvalidOperationException("Private status QA flag required.");
        local=player;ActorData template=null;
        foreach(var data in ActorDataList.instance().data)
            if(data!=null && data.actorType==ActorData.Type.Normal) {template=data;break;}
        if(template==null) throw new InvalidOperationException("Missing private status target template.");
        Actor Spawn(float x)=>template.spawn(local.position()+new Vector3(x,0,5),0,null,AbilitySync.PositionSyncType.Absolute,null,"",true,0,
            new Il2CppSystem.Nullable<UnityEngine.SceneManagement.Scene>(local.gameObject.scene),true);
        target=Spawn(6);replica=Spawn(9);
    }
    internal bool Tick()
    {
        if(Complete) return true;
        if(transport!=null)
        {
            if(!transport.Tick()) return false;
            Observations=new {native=nativeObservations,transport=transport.Observations};
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),"UserData","LastEpochLAN.shared-status-qa.json"),System.Text.Json.JsonSerializer.Serialize(Observations));
            transport.Dispose();transport=null;Complete=true;return true;
        }
        if(Time.unscaledTime>deadline) throw new TimeoutException("Shared native status targets did not initialize.");
        foreach(var actor in new[]{target,replica})
        {
            if(actor?.health==null || actor.health.maxHealth<=0 || actor.protection==null || actor.stats==null || actor.GetComponent<AilmentReceiver>()==null) return false;
            actor.health.maxHealth=100000;actor.health.currentHealth=100000;actor.health.damageable=true;
            if(actor.stateController!=null) actor.stateController.enabled=false;
            if(actor.navMeshAgent!=null) actor.navMeshAgent.enabled=false;
        }
        NativePartyStatus.Enabled=true;
        var receiver=target.GetComponent<AilmentReceiver>();var replicaReceiver=replica.GetComponent<AilmentReceiver>();
        receiver.cleanseNegativeAilments();replicaReceiver.cleanseNegativeAilments();
        var stats=new DamageStats {damage=new float[]{8,16,0,0,0,0,0},isHit=false,
            penetration=new DamageStats.Penetration(new float[]{1,2,3,4,5,6,7})};
        stats.trackerStatStorage=new DamageStats.TrackerStatStorage();
        stats.trackerStatStorage.addedValues[TrackerPropertyID.CrystalWindStacksUsedOnCast]=3;
        stats.conditionalEffects=new Il2CppSystem.Collections.Generic.List<DamageConditionalEffect>();
        stats.conditionalEffects.Add(new DamageConditionalEffect(new AilmentStacksConditional(AilmentID.Poison,2),new DamageEffectMoreDamagerPerCurrentHealth(0.5f,0.25f,0.5f),AT.None));
        var context=new NativeStatusCodec.Context(local,target,local);
        var encoded=NativeStatusCodec.Encode(stats,context);
        var roundtrip=NativeStatusCodec.Damage(encoded,context);
        if(!encoded.IsValid() || roundtrip.damage[0]!=8 || roundtrip.damage[1]!=16 || roundtrip.getPenetration(6)!=7 ||
            roundtrip.trackerStatStorage.addedValues[TrackerPropertyID.CrystalWindStacksUsedOnCast]!=3 ||
            roundtrip.conditionalEffects.Count!=1 || roundtrip.conditionalEffects[0].conditional.TryCast<AilmentStacksConditional>()==null ||
            roundtrip.conditionalEffects[0].effect.TryCast<DamageEffectMoreDamagerPerCurrentHealth>()==null)
            throw new InvalidOperationException("Native resolved DamageStats codec lost damage or penetration.");
        var source=NativePartyStatus.CreatorForProof(local);
        var first=new ResolvedAilment {Id=(int)AilmentID.Ignite,Stacks=2,Duration=4,Damage=new float[]{8,0,0,0,0,0,0}};
        var second=first with {Stacks=3,Duration=6,Damage=new float[]{24,0,0,0,0,0,0}};
        var a=NativePartyStatus.ApplyAuthority(target,local,first);
        var b=NativePartyStatus.ApplyAuthority(target,source,second);
        var poison=NativePartyStatus.ApplyAuthority(target,source,second with {Id=(int)AilmentID.Poison,Stacks=2});
        if(a==null || b==null || poison==null || a.creator.Pointer==b.creator.Pointer || receiver.getStacks(AilmentID.Ignite)!=5 || receiver.getStacks(AilmentID.Poison)!=2)
            throw new InvalidOperationException("Two native sources did not share one stack receiver.");
        if(a.damageStats.damage[0]!=8 || b.damageStats.damage[0]!=24 || a.totalDuration==b.totalDuration)
            throw new InvalidOperationException("Shared native receiver collapsed different source damage/durations.");
        float before=target.health.currentHealth;receiver.OnUpdateTick(0.5f);float damage=before-target.health.currentHealth;
        if(damage<=0) throw new InvalidOperationException("Shared Host native DoT did not cause damage.");
        var sharedIds=new List<string>();
        foreach(string name in new[]{"Bleed","Blind","Chill","Doom","Electrify","Frostbite","Slow","Shock","MarkedForDeath","ArmourShred","Frailty","TimeRot",
            "SpreadingFlames","BlindingPoison","Plague","Possession","Root","Stagger","Fear","Pestilence","SpiritPlague","PhysicalResistanceShred","FireResistanceShred","ColdResistanceShred","LightningResistanceShred","NecroticResistanceShred","PoisonResistanceShred","VoidResistanceShred"})
        {
            if(!Enum.TryParse<AilmentID>(name,true,out var id) || !AilmentList.TryGetAilment(id,out var definition) || definition==null) continue;
            var application=NativePartyStatus.ApplyAuthority(target,source,first with {Id=(int)id,Stacks=1});
            if(application!=null) sharedIds.Add(name);
        }
        NativePartyStatusMirror.Dirty(receiver);NativeAilmentSynchronization.Dirty(receiver);
        var modifiers=NativePartyStatusMirror.Read(target);
        var counts=NativeAilmentSynchronization.Read(target);
        var snapshot=new EnemyState {Id=1,Template="native-status-proof",Health=100000,MaxHealth=100000,Ailments=counts,StatusModifiers=modifiers,StatusCounters=NativePartyStatusMirror.Counters(target)};
        if(modifiers.Length==0) throw new InvalidOperationException("Native debuff capture produced no modifiers.");
        NativePartyStatusMirror.Apply(replica,snapshot);int countAfterFirst=replica.stats.stats.Count;
        NativePartyStatusMirror.Apply(replica,snapshot);
        if(replica.stats.stats.Count!=countAfterFirst || replicaReceiver.getStacks(AilmentID.Ignite)!=5 || replicaReceiver.ailments.Count!=0)
            throw new InvalidOperationException("Native Guest mirror duplicated stats, lost aggregate counts or created a second DoT writer.");
        foreach(var stat in modifiers)
        {
            float host=target.stats.GetTotalAdded((SP)stat.Property,(AT)stat.Tags,stat.SpecialTag,stat.ExtraTag);
            float guest=replica.stats.GetTotalAdded((SP)stat.Property,(AT)stat.Tags,stat.SpecialTag,stat.ExtraTag);
            if(Math.Abs(host-guest)>0.001f) throw new InvalidOperationException("Native mirrored debuff potency mismatch for "+(SP)stat.Property+": "+host+" vs "+guest);
        }
        float replicaBefore=replica.health.currentHealth;replicaReceiver.OnUpdateTick(0.5f);
        if(replicaBefore!=replica.health.currentHealth) throw new InvalidOperationException("Guest mirrored status wrote duplicate native damage.");
        receiver.cleanseNegativeAilments();NativePartyStatusMirror.Dirty(receiver);NativeAilmentSynchronization.Dirty(receiver);
        NativePartyStatusMirror.Apply(replica,snapshot with {Ailments=NativeAilmentSynchronization.Read(target),StatusModifiers=NativePartyStatusMirror.Read(target),StatusCounters=NativePartyStatusMirror.Counters(target)});
        if(replicaReceiver.getStacks(AilmentID.Ignite)!=0) throw new InvalidOperationException("Native cleanse left stale Guest status counts.");
        bool immunity=receiver.immuneToAllNegativeAilmentsFromStats;receiver.immuneToAllNegativeAilmentsFromStats=true;
        try {if(NativePartyStatus.ApplyAuthority(target,source,first)!=null) throw new InvalidOperationException("Host native immunity was bypassed.");}
        finally {receiver.immuneToAllNegativeAilmentsFromStats=immunity;}
        int expirations=0;
        NativePartyStatus.ApplyAuthority(target,local,first with {Stacks=1,Duration=0.1f,ExpirationCallback=true},_=>expirations++);
        receiver.OnUpdateTick(1);
        if(expirations!=1) throw new InvalidOperationException("Native expiration callback was not exactly once: "+expirations);
        nativeObservations=new {nativeDamageCodec=true,twoNativeCreators=true,sharedIgniteStacks=5,sharedPoisonStacks=2,
            sourcePotencyAndDurationPreserved=true,hostDotDamage=damage,guestDotDamage=replicaBefore-replica.health.currentHealth,
            modifiers=modifiers.Length,catalogueDebuffs=sharedIds.ToArray(),nativeImmunity=true,nativeCleanse=true,nativeExpirationCallbacks=expirations,
            idempotentGuestModifierReplay=true,twoPcValidated=false,advancedProcAndMinionBuildParityValidated=false};
        NativePartyStatusMirror.Forget(replica);
        transport=new(local,target,replica);return false;
    }
    public void Dispose()
    {
        NativePartyStatus.Reset();NativePartyStatus.Enabled=false;
        transport?.Dispose();transport=null;
        if(target!=null) HostEnemyReplicas.DestroyActor(target);
        if(replica!=null) HostEnemyReplicas.DestroyActor(replica);
    }
}
