using HarmonyLib;
using Il2Cpp;
using LastEpochLAN.Core;
using UnityEngine;
using ActiveAilment = Il2Cpp.AilmentReceiver.ActiveAilment;

namespace LastEpochLAN.Mod;

// One native receiver on Host is the only writer of shared monster ailments.
// Guest keeps detached return objects so skill scripts can finish customizing
// an application before LateUpdate sends it. No native pointers cross the wire.
internal static class NativePartyStatus
{
    private sealed record Pending(long Id,long Epoch,int Enemy,Actor Target,Actor Source,Actor Owner,
        ActiveAilment Active,StatusValue Baseline,bool Spreading,float Elapsed)
    {
        internal ResolvedAilment Encoded;
        internal HitRequest Direct;
    }
    private sealed class Profile
    {
        internal Actor Actor;
        internal string Key;
        internal float NextRead;
        internal StatusSourceProfile Data;
    }
    private sealed record Expiration(long Epoch,Il2CppSystem.Action<Vector3> Callback,float Deadline);
    private sealed record ProfileAck(string Key,long Revision,float Deadline);
    internal sealed record Replay(ResolvedAilment Data,NativeStatusCodec.Context Context,Actor Target)
    {
        internal bool Injected;
    }
    private static readonly Queue<Pending> pending=new();
    private static readonly Dictionary<IntPtr,Profile> profiles=new();
    private static readonly Dictionary<long,Expiration> callbacks=new();
    private static readonly Dictionary<long,ProfileAck> profileAcks=new();
    private static readonly Dictionary<string,long> confirmedProfiles=new();
    private static readonly Dictionary<IntPtr,(int Frame,float Damage)> forwardedDamage=new();
    private static readonly Dictionary<(string Peer,string Key),Actor> creators=new();
    private static readonly Dictionary<(string Peer,string Key),long> creatorRevisions=new();
    private static readonly Dictionary<string,long> appliedApplications=new();
    private static readonly List<GameObject> roots=new();
    private static long nextApplication,nextProfile,nextCreator;
    private static float nextCallbackPrune;
    [ThreadStatic] internal static Replay Replaying;
    internal static long Sent,Applied,Rejected,Expired;
    internal static string Error="";
    // This development candidate advertises protocol 28 only. Both peers must
    // run the matching adapter; it cannot silently mix source-owned DoT peers.
    internal static bool Enabled=true;
    internal static object Diagnostics=>new {enabled=Enabled,pending=pending.Count,Sent,Applied,Rejected,Expired,Error};

    private static Actor Owner(Actor creator)
    {
        var local=PlayerFinder.getPlayerActor();
        for(int depth=0;creator!=null && depth<8;depth++)
        {
            if(creator.Pointer==local?.Pointer) return local;
            var parent=creator.GetCreator();if(parent==null || parent.Pointer==creator.Pointer) break;creator=parent;
        }
        return null;
    }
    internal static bool Capture(AilmentReceiver receiver,Ailment ailment,Actor creator,Ability ability,Ability original,Ability primary,
        DamageStats damage,Alignment alignment,UseType use,float stacks,float duration,float effect,float healing,bool spreading,float elapsed,
        ref ActiveAilment result)
    {
        var world=WorldSynchronizer.Active;
        if(!Enabled || Replaying!=null || world?.Room?.IsHost!=false || !world.Room.AcceptsResolvedAilments ||
            receiver?.actor==null || !world.TryGetReplicaId(receiver.actor,out int enemy)) return true;
        var owner=Owner(creator);
        // Replica-owned procs are never another damage source on Guest.
        if(owner==null) {result=null;return false;}
        if(pending.Count>=1792) {Fail("Shared status application queue is full.");result=null;return false;}
        try
        {
            result=Detached(ailment,creator,ability,original,primary,damage,alignment,use,stacks,duration,effect,healing,elapsed);
            var context=new NativeStatusCodec.Context(creator,receiver.actor,owner);
            var baseline=NativeStatusCodec.Overrides(result,context);
            MutateSource(receiver,result,spreading);
            pending.Enqueue(new(++nextApplication,world.GuestWorld.Epoch,enemy,receiver.actor,creator,owner,result,baseline,spreading,elapsed));
        }
        catch(Exception ex) {Fail("Capture: "+ex.GetBaseException().Message);result=null;}
        return false;
    }
    internal static bool QueueDirect(WorldSynchronizer world,Actor target,int enemy,float damage,bool cull,float percent,float flat,bool dot)
    {
        if(pending.Count>=2048) {Fail("Shared combat command queue is full.");return false;}
        var local=PlayerFinder.getPlayerActor();
        pending.Enqueue(new(++nextApplication,world.GuestWorld.Epoch,enemy,target,local,local,null,null,false,0)
        {Direct=new() {Damage=damage,Cull=cull,CullPercent=percent,FlatCull=flat,SourceDot=dot}});
        if(!dot) forwardedDamage[target.Pointer]=(Time.frameCount,damage);
        return true;
    }
    internal static bool CaptureHit(AilmentReceiver receiver,float damage,Actor attacker,HitEvents events)
    {
        var world=WorldSynchronizer.Active;
        if(!Enabled || world?.Room?.IsHost!=false || !world.Room.AcceptsResolvedAilments || receiver?.actor==null ||
            !world.TryGetReplicaId(receiver.actor,out var enemy)) return true;
        var owner=Owner(attacker);if(owner==null) return false;
        if(pending.Count>=1792) {Fail("Shared native on-hit queue is full.");return false;}
        try
        {
            if(damage<=0 && forwardedDamage.Remove(receiver.actor.Pointer,out var last) && last.Frame==Time.frameCount) damage=last.Damage;
            var profile=ReadProfile(attacker,owner);long id=++nextApplication;
            var data=new ResolvedAilment {Operation="hit",HitDamage=damage,HitEvents=(int)events,Application=id,
                CreatorKey=profile.Key,SourceRevision=profile.Data.Revision,SourceProfile=profile.Data,Stacks=1,Duration=1};
            pending.Enqueue(new(id,world.GuestWorld.Epoch,enemy,receiver.actor,attacker,owner,null,null,false,0) {Encoded=data});
        }
        catch(Exception ex) {Fail("On hit: "+ex.GetBaseException().Message);}
        return false;
    }
    internal static ActiveAilment Detached(Ailment ailment,Actor creator,Ability ability,Ability original,Ability primary,DamageStats damage,
        Alignment alignment,UseType use,float stacks,float duration,float effect,float healing,float elapsed)
    {
        float total=ailment.duration*(1+duration);
        return new ActiveAilment {ailment=ailment,creator=creator,ability=ability,originalAbility=original,primaryAbility=primary,
            damageStats=damage,alignment=alignment,useType=use,stacksRepresented=stacks,increasedDuration=duration,
            totalDuration=total,remainingDuration=total-elapsed,increasedEffect=effect,increasedHealing=healing};
    }
    private static void MutateSource(AilmentReceiver receiver,ActiveAilment active,bool spreading)
    {
        var definition=active.ailment;var creator=active.creator;
        if(definition.mutationType==Ailment.MutationType.CharacterMutator && creator.characterMutator!=null)
        {
            float added=CharacterAilmentMutator.GetAilmentDamageModifier(active,creator.characterMutator,receiver.actor,spreading);
            active.moreDamage*=1+added;
            CharacterAilmentMutator.MutateAilment(active,creator.characterMutator,receiver.actor,spreading);
        }
        else if(definition.mutationType==Ailment.MutationType.AbilityMutator)
        {
            var manager=creator.GetMutatorManager();var ability=definition.getAbilityForMutation();
            var mutator=manager?.getMutator(ability)?.TryCast<AilmentMutator>();
            if(mutator!=null)
            {
                active.moreDamage*=1+mutator.GetAilmentDamageModifier(definition,active,spreading,receiver);
                var buffs=new Il2CppSystem.Collections.Generic.List<Stats.Stat>();
                if(definition.buffs!=null) foreach(var stat in definition.buffs) buffs.Add(NativeStatusCodec.MakeStat(NativeStatusCodec.ReadStat(stat)));
                mutator.mutateAilmentInstance(definition,active,buffs,spreading,receiver);
                // The resulting list belongs to this source instance; native
                // receiver mutation is applied separately on the authority.
                active.mutationSetStats=buffs;
            }
        }
    }
    private static Profile ReadProfile(Actor actor,Actor owner)
    {
        if(!profiles.TryGetValue(actor.Pointer,out var profile))
            profiles[actor.Pointer]=profile=new() {Actor=actor,Key=actor.Pointer==owner.Pointer?"player":"minion-"+(++nextCreator)};
        if(profile.Data==null || Time.unscaledTime>=profile.NextRead)
        {
            profile.NextRead=Time.unscaledTime+2;
            var list=new List<StatusStat>();
            if(actor.stats?.stats!=null) foreach(var stat in actor.stats.stats) if(stat!=null) list.Add(NativeStatusCodec.ReadStat(stat));
            var stats=list.ToArray();
            if(profile.Data==null || stats.Length!=profile.Data.Stats.Length || stats.Where((s,i)=>!s.Same(profile.Data.Stats[i])).Any())
                profile.Data=new() {Revision=++nextProfile,Stats=stats};
        }
        return profile;
    }
    private static int Id(Ability ability)=>ability!=null && ability.TryGetAbilityID(out var id)?(int)id:-1;
    private static ResolvedAilment Encode(Pending p)
    {
        var active=p.Active;var context=new NativeStatusCodec.Context(p.Source,p.Target,p.Owner);
        var profile=ReadProfile(p.Source,p.Owner);
        var data=new ResolvedAilment {Application=p.Id,CreatorKey=profile.Key,SourceRevision=profile.Data.Revision,SourceProfile=profile.Data,
            Id=(int)active.ailment.id,Ability=Id(active.ability),OriginalAbility=Id(active.originalAbility),PrimaryAbility=Id(active.primaryAbility),
            UseType=(int)active.useType,Stacks=active.stacksRepresented,Duration=active.ailment.duration<=0?86400:active.totalDuration,
            IncreasedEffect=active.increasedEffect,IncreasedHealing=active.increasedHealing,AppliedBySpreading=p.Spreading,ElapsedDuration=p.Elapsed,
            NativeDamage=NativeStatusCodec.Encode(active.damageStats,context),
            Overrides=NativeStatusCodec.ChangedOverrides(p.Baseline,NativeStatusCodec.Overrides(active,context)),ExpirationCallback=active.callbackOnExpiration!=null};
        if(!data.IsValid()) throw new InvalidOperationException("Resolved native application is outside the wire schema.");
        return data;
    }
    internal static void LateUpdate()
    {
        var world=WorldSynchronizer.Active;var room=world?.Room;
        if(room==null || room.IsHost || !Enabled) return;
        for(int budget=Math.Min(64,pending.Count);budget>0;budget--)
        {
            var p=pending.Peek();
            if(p.Epoch!=world.GuestWorld?.Epoch || p.Target==null || p.Source==null) {pending.Dequeue();continue;}
            try
            {
                if(p.Direct is {} direct)
                {
                    if(!room.RequestHit(p.Epoch,p.Enemy,direct.Damage,direct.Cull,direct.CullPercent,direct.FlatCull,direct.SourceDot)) break;
                    pending.Dequeue();continue;
                }
                p.Encoded??=Encode(p);
                var wire=confirmedProfiles.GetValueOrDefault(p.Encoded.CreatorKey)==p.Encoded.SourceRevision?p.Encoded with {SourceProfile=null}:p.Encoded;
                if(!room.RequestAilment(p.Epoch,p.Enemy,wire)) break; // Retry without losing caller modifications.
                pending.Dequeue();Sent++;
                if(wire.SourceProfile!=null) profileAcks[p.Id]=new(wire.CreatorKey,wire.SourceRevision,Time.unscaledTime+30);
                if(p.Active?.callbackOnExpiration!=null)
                    callbacks[p.Id]=new(p.Epoch,p.Active.callbackOnExpiration,Time.unscaledTime+Math.Min(86400,p.Encoded.Duration)+30);
            }
            catch(Exception ex) {pending.Dequeue();Fail("Encode: "+ex.GetBaseException().Message);}
        }
    }
    internal static void Tick(WorldSynchronizer world)
    {
        if(!Enabled) return;
        var room=world.Room;if(room==null) return;
        if(!room.IsHost)
        {
            for(int i=0;i<64 && room.TryTakeStatusCallback(out var callback);i++)
            {
                if(profileAcks.Remove(callback.Application,out var profile) && callback.Kind==0)
                    confirmedProfiles[profile.Key]=profile.Revision;
                if(callback.Kind==0) continue;
                if(callbacks.Remove(callback.Application,out var entry) && entry.Epoch==callback.Epoch && callback.Kind==1)
                {entry.Callback.Invoke(new(callback.Position.X,callback.Position.Y,callback.Position.Z));Expired++;}
            }
            if(Time.unscaledTime>=nextCallbackPrune)
            {
                nextCallbackPrune=Time.unscaledTime+1;
                foreach(var id in callbacks.Where(p=>p.Value.Epoch!=world.GuestWorld?.Epoch || p.Value.Deadline<Time.unscaledTime).Select(p=>p.Key).ToArray()) callbacks.Remove(id);
                foreach(var id in profileAcks.Where(p=>p.Value.Deadline<Time.unscaledTime).Select(p=>p.Key).ToArray()) profileAcks.Remove(id);
            }
            return;
        }
        long start=System.Diagnostics.Stopwatch.GetTimestamp();
        for(int i=0;i<64 && room.TryTakeCombat(out var request);i++)
        {
            if(request.Ailment==null)
            {
                world.ApplyHitRequest(request);
                if((System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000d/System.Diagnostics.Stopwatch.Frequency>=1.5) break;
                continue;
            }
            int outcome=2;
            try
            {
                var data=request.Ailment;var target=world.FindEnemy(request.EnemyId);
                bool hasPlayer=room.TryGetPlayerLocation(request.PlayerId,out var player);
                if(request.Epoch!=world.StatusEpoch || target==null || target.health?.currentHealth<=0 ||
                    !hasPlayer || !player.Available || player.Scene!=target.gameObject.scene.name ||
                    Vector3.Distance(target.position(),new(player.X,player.Y,player.Z))>100) continue;
                // Each peer assigns monotonic application IDs. Old/repeated
                // requests cannot run native callbacks or award procs twice.
                if(data.Application<=appliedApplications.GetValueOrDefault(request.PlayerId)) {outcome=0;continue;}
                appliedApplications[request.PlayerId]=data.Application;
                var creator=Creator(request.PlayerId,data,PlayerFinder.getPlayerActor());
                creator.transform.position=new(player.X,player.Y,player.Z);
                string recipient=request.PlayerId;long requestEpoch=request.Epoch,application=data.Application;
                if(data.Operation=="hit") target.GetComponent<AilmentReceiver>().whenHit(data.HitDamage,creator,(HitEvents)data.HitEvents);
                else ApplyAuthority(target,creator,data,position=>room.SendStatusCallback(recipient,new() {Kind=1,Epoch=requestEpoch,Application=application,
                    Position=new() {X=position.x,Y=position.y,Z=position.z}}));
                outcome=0;Applied++;
            }
            catch(Exception ex) {Fail("Authority: "+ex.GetBaseException().Message);}
            finally {room.SendStatusCallback(request.PlayerId,new() {Kind=outcome,Epoch=request.Epoch,Application=request.Ailment.Application});}
            if((System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000d/System.Diagnostics.Stopwatch.Frequency>=1.5) break;
        }
    }
    private static Actor Creator(string peer,ResolvedAilment data,Actor local)
    {
        var key=(peer,data.CreatorKey);
        if(!creators.TryGetValue(key,out var actor) || actor==null)
        {
            if(creators.Count>=128) throw new InvalidOperationException("Too many native status creators in this instance.");
            var root=new GameObject("LAN status source "+peer+"/"+data.CreatorKey);root.SetActive(false);roots.Add(root);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,local.gameObject.scene);
            actor=root.AddComponent<Actor>();actor.enabled=false;actor.tForm=root.transform;actor.data=local.data;
            actor.initialised=true;actor.isPlayerActor=data.CreatorKey=="player";actor.neverValidTarget=true;
            actor.alignmentManager=root.AddComponent<AlignmentManager>();actor.alignmentManager.alignment=local.GetAlignment();
            actor.stats=root.AddComponent<BaseStats>();actor.stats.actor=actor;actor.stats.enabled=false;
            actor.health=root.AddComponent<BaseHealth>();actor.health.actor=actor;actor.health.tForm=root.transform;actor.health.enabled=false;
            actor.health.currentHealth=actor.health.maxHealth=100000;actor.health.canDie=false;actor.health.damageable=false;
            actor.protection=root.AddComponent<ProtectionClass>();actor.protection.actor=actor;actor.protection.enabled=false;
            actor.dying=root.AddComponent<Dying>();actor.dying.enabled=false;
            creators[key]=actor;
        }
        if(!creatorRevisions.TryGetValue(key,out long revision) || revision!=data.SourceRevision)
        {
            var profile=data.SourceProfile??throw new InvalidOperationException("Native source profile is missing.");
            var stats=new Il2CppSystem.Collections.Generic.List<Stats.Stat>();foreach(var stat in profile.Stats) stats.Add(NativeStatusCodec.MakeStat(stat));
            actor.stats.stats=stats;creatorRevisions[key]=profile.Revision;
        }
        return actor;
    }
    internal static Actor CreatorForProof(Actor local)=>Creator("private-status-proof",new ResolvedAilment
    {CreatorKey="player",SourceRevision=1,SourceProfile=new() {Revision=1,Stats=new[]{new StatusStat {Property=(int)SP.Damage,Increased=0.5f}}}},local);
    internal static ActiveAilment ApplyAuthority(Actor target,Actor creator,ResolvedAilment data,Action<Vector3> expiration=null)
    {
        if((int)(AilmentID)data.Id!=data.Id || !AilmentList.TryGetAilment((AilmentID)data.Id,out var definition) || definition==null) throw new InvalidOperationException("Unknown native ailment ID.");
        var receiver=target.GetComponent<AilmentReceiver>()??throw new InvalidOperationException("Missing native target receiver.");
        if(receiver.ailmentImmunityType==AilmentReceiver.AilmentImmunityType.All ||
            !definition.positive && (receiver.immuneToAllNegativeAilmentsFromStats || receiver.ailmentImmunityType==AilmentReceiver.AilmentImmunityType.Negative) ||
            receiver.ImmuneTo(definition.id)) return null;
        var context=new NativeStatusCodec.Context(creator,target,creator);
        var damage=NativeStatusCodec.Damage(data.NativeDamage,context)??new DamageStats {damage=data.Damage,penetration=new DamageStats.Penetration(data.Penetration),isHit=false,otherTags=(AT)data.Tags};
        if(damage.damage==null || damage.damage.Length!=7 || damage.damage.Any(v=>!float.IsFinite(v) || Math.Abs(v)>1e8))
            throw new InvalidOperationException("Native status damage must contain exactly seven finite values.");
        var prior=Replaying;Replaying=new(data,context,target);
        try
        {
            Ability Ability(int id)=>id<0?null:Il2Cpp.Ability.getAbility((ushort)id);
            var active=receiver.ApplyAilmentWithDamageStats(definition,creator,Ability(data.Ability),Ability(data.OriginalAbility),Ability(data.PrimaryAbility),
                damage,creator.GetAlignment(),(UseType)data.UseType,data.Stacks,definition.duration>0?data.Duration/definition.duration-1:0,
                data.IncreasedEffect,data.IncreasedHealing,data.AppliedBySpreading,data.ElapsedDuration);
            if(active!=null)
            {
                Inject(active);
                if(data.ExpirationCallback && expiration!=null) active.callbackOnExpiration=expiration;
            }
            NativeAilmentSynchronization.Dirty(receiver);NativePartyStatusMirror.Dirty(receiver);
            return active;
        }
        finally {Replaying=prior;}
    }
    internal static void Inject(ActiveAilment active)
    {
        var replay=Replaying;
        if(replay==null || replay.Injected || active==null || (int)active.ailment.id!=replay.Data.Id || active.creator?.Pointer!=replay.Context.Source?.Pointer) return;
        NativeStatusCodec.ApplyOverrides(active,replay.Data.Overrides,replay.Context);replay.Injected=true;
    }
    internal static void Fail(string message)
    {Rejected++;if(Error!=message) {Error=message;MelonLoader.MelonLogger.Warning("Shared status: "+message);}}
    internal static void Reset()
    {
        pending.Clear();profiles.Clear();callbacks.Clear();profileAcks.Clear();confirmedProfiles.Clear();forwardedDamage.Clear();appliedApplications.Clear();creatorRevisions.Clear();creators.Clear();
        // Native receivers can retain creators until their scene is destroyed.
        // Let scene unload own destruction; never invalidate pointers mid-tick.
        roots.RemoveAll(root=>root==null);
        NativePartyStatusMirror.Reset();
    }
}

[HarmonyPatch(typeof(AilmentReceiver),nameof(AilmentReceiver.whenHit))]
internal static class LanPartyStatusHitPatch
{
    private static bool Prefix(AilmentReceiver __instance,float damage,Actor attacker,HitEvents hitEvent)
        =>NativePartyStatus.CaptureHit(__instance,damage,attacker,hitEvent);
}

[HarmonyPatch(typeof(AilmentReceiver),nameof(AilmentReceiver.ApplyAilmentWithDamageStats))]
internal static class LanPartyStatusCapturePatch
{
    private static bool Prefix(AilmentReceiver __instance,Ailment ailment,Actor creator,Ability ability,Ability originalAbility,Ability primaryAbility,
        DamageStats damageStats,Alignment alignment,UseType useType,float stacks,float increasedDuration,float increasedEffect,float increasedHealing,
        bool appliedBySpreading,float elapsedDuration,ref ActiveAilment __result)
        =>NativePartyStatus.Capture(__instance,ailment,creator,ability,originalAbility,primaryAbility,damageStats,alignment,useType,stacks,
            increasedDuration,increasedEffect,increasedHealing,appliedBySpreading,elapsedDuration,ref __result);
}
[HarmonyPatch(typeof(AilmentReceiver),nameof(AilmentReceiver.addBuffsModifiedByStacksAndIncreasedEffect))]
internal static class LanPartyStatusOverridesPatch
{
    private static void Prefix(ActiveAilment activeAilment)=>NativePartyStatus.Inject(activeAilment);
    private static void Postfix(AilmentReceiver __instance)=>NativePartyStatusMirror.Dirty(__instance);
}
[HarmonyPatch(typeof(ActiveAilment),nameof(ActiveAilment.GetApproximateRemainingDamage))]
internal static class LanPartyStatusStrengthPatch
{
    private static void Prefix(ActiveAilment __instance)=>NativePartyStatus.Inject(__instance);
}
