using System.Net;
using Il2Cpp;
using LastEpochLAN.Core;
using UnityEngine;

namespace LastEpochLAN.Mod;

// Actual capture detour, TCP, authority pump and callback path in a private
// native process. Deliberately distinct from a real two-PC gameplay test.
internal sealed class NativePartyStatusTransportSmoke : IDisposable
{
    private const int enemy=9001;
    private const long epoch=900;
    private readonly Actor local,target,replica;
    private readonly WorldSynchronizer previous;
    private LanSession host,guest;
    private Task<LanSession> join;
    private WorldSynchronizer hostWorld,guestWorld;
    private float deadline=Time.unscaledTime+25;
    private int stage,callbacks;
    private long sentBefore,appliedBefore,rejectedBefore;
    internal object Observations;
    internal NativePartyStatusTransportSmoke(Actor player,Actor authoritative,Actor copy)
    {
        if(!Environment.GetCommandLineArgs().Contains("--lelan-offline-tools-smoke")) throw new InvalidOperationException("Private status transport QA required.");
        local=player;target=authoritative;replica=copy;previous=WorldSynchronizer.Active;
    }
    private PlayerState State()=>new() {Available=true,Scene=target.gameObject.scene.name,X=local.position().x,Y=local.position().y,Z=local.position().z};
    internal bool Tick()
    {
        if(Time.unscaledTime>deadline) throw new TimeoutException("Native shared status TCP fixture stage "+stage+": "+NativePartyStatus.Error);
        switch(stage)
        {
            case 0:
                NativePartyStatus.Reset();NativePartyStatus.Enabled=true;
                target.GetComponent<AilmentReceiver>().cleanseNegativeAilments();replica.GetComponent<AilmentReceiver>().cleanseNegativeAilments();
                host=LanSession.Host("NativeStatusHost","native-status-proof","private-status-proof",0,IPAddress.Loopback);
                host.Publish(State());
                host.PublishWorld(new() {Epoch=epoch,Revision=1,Scene=target.gameObject.scene.name,AcceptsResolvedAilments=true,Enemies=new[]{
                    new EnemyState {Id=enemy,Template=target.data.name,Health=100000,MaxHealth=100000,X=target.position().x,Y=target.position().y,Z=target.position().z}}});
                join=LanSession.JoinAsync("127.0.0.1",host.Port,"NativeStatusGuest","native-status-proof","private-status-proof");stage=1;return false;
            case 1:
                if(!join.IsCompleted) return false;
                guest=join.GetAwaiter().GetResult();guest.Publish(State());
                if(!guest.AcceptsResolvedAilments || !host.PlayerPositions.Any(p=>p.Id==guest.LocalId && p.Available)) return false;
                hostWorld=WorldSynchronizer.StatusFixture(host,target,enemy,epoch);
                guestWorld=WorldSynchronizer.StatusFixture(guest,replica,enemy,epoch);
                WorldSynchronizer.Active=guestWorld;
                sentBefore=NativePartyStatus.Sent;appliedBefore=NativePartyStatus.Applied;rejectedBefore=NativePartyStatus.Rejected;
                var stats=new DamageStats {damage=new float[]{32,0,0,0,0,0,0},isHit=false};
                var receiver=replica.GetComponent<AilmentReceiver>();
                var active=receiver.ApplyAilmentWithDamageStats(Ailment.getAilment(AilmentID.Ignite),local,null,null,null,stats,local.GetAlignment(),(UseType)0,
                    2,0,0,0,false,0);
                if(active==null || receiver.ailments.Count!=0) throw new InvalidOperationException("Guest detour failed to return a detached native application.");
                // Mirrors a skill script editing the return value after the
                // native call. LateUpdate must preserve this customization.
                active.moreDamage=1.75f;active.callbackOnExpiration=(Action<Vector3>)(_=>callbacks++);
                NativePartyStatus.LateUpdate();stage=2;return false;
            case 2:
                NativePartyStatus.Tick(hostWorld);
                var hostReceiver=target.GetComponent<AilmentReceiver>();
                if(hostReceiver.getStacks(AilmentID.Ignite)!=2) return false;
                var received=hostReceiver.ailments[0];
                if(received.creator.Pointer==local.Pointer || received.damageStats.damage[0]!=32 || received.moreDamage!=1.75f || NativePartyStatus.Sent!=sentBefore+1)
                    throw new InvalidOperationException("Native TCP lost creator, damage, post-return source override or duplicated the command.");
                float health=target.health.currentHealth;hostReceiver.OnUpdateTick(0.5f);
                if(target.health.currentHealth>=health) throw new InvalidOperationException("Host delegated native status caused no DoT.");
                replica.GetComponent<AilmentReceiver>().whenHit(0,local,(HitEvents)1);NativePartyStatus.LateUpdate();stage=4;return false;
            case 4:
                NativePartyStatus.Tick(hostWorld);
                if(NativePartyStatus.Applied<appliedBefore+2) return false;
                target.GetComponent<AilmentReceiver>().OnUpdateTick(10);stage=3;return false;
            case 3:
                NativePartyStatus.Tick(guestWorld);
                if(callbacks==0) return false;
                if(callbacks!=1 || NativePartyStatus.Applied!=appliedBefore+2 || NativePartyStatus.Rejected!=rejectedBefore)
                    throw new InvalidOperationException("Native TCP applied/callback counts were not exactly once: "+NativePartyStatus.Error);
                Observations=new {actualNativeGuestCaptureDetour=true,detachedGuestReceiver=true,actualTcpTransport=true,hostNativeAuthority=true,
                    sourceProfileReconstructed=true,postReturnDamageMultiplier=1.75f,nativeExpirationCallbackRpc=callbacks,nativeGuestOnHitForwarded=true,
                    hostApplications=NativePartyStatus.Applied-appliedBefore,guestCommands=NativePartyStatus.Sent-sentBefore,twoPcValidated=false};
                return true;
            default:throw new InvalidOperationException("Unknown status TCP fixture stage.");
        }
    }
    public void Dispose()
    {
        WorldSynchronizer.Active=previous;
        guest?.Dispose();host?.Dispose();
        if(guest==null && join!=null) _=join.ContinueWith(t=>{if(t.Status==TaskStatus.RanToCompletion)t.Result.Dispose();});
        NativePartyStatus.Reset();
    }
}
