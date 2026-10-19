using System.Net;
using LastEpochLAN.Core;

internal static class SourceStatusTransportTests
{
    internal static async Task Run(Action<bool,string> check)
    {
        using var host=LanSession.Host("StatusHost","status-fixture","private-fixture",0,IPAddress.Loopback);
        host.Publish(new PlayerState {Scene="status-map",Available=true});
        host.PublishWorld(new WorldState {Epoch=1,Revision=1,Scene="status-map",Enemies=new[]{
            new EnemyState {Id=42,Template="target",Health=100,MaxHealth=100},
            new EnemyState {Id=43,Template="target",Health=100,MaxHealth=100}}});
        using var guest=await LanSession.JoinAsync("127.0.0.1",host.Port,"StatusGuest","status-fixture","private-fixture");
        guest.Publish(new PlayerState {Scene="status-map",Available=true});
        async Task Wait(Func<bool> condition)
        {
            var end=Environment.TickCount64+5000;
            while(!condition() && Environment.TickCount64<end) await Task.Delay(10);
            if(!condition()) throw new TimeoutException("Status TCP fixture timed out.");
        }
        await Wait(()=>guest.WorldInfo.Epoch==1 && host.PlayerPositions.Any(p=>p.Id==guest.LocalId && p.Available));
        check(guest.PublishStatusVisuals(1,42,new[]{new StatusVisualGroup(1,3,2),new StatusVisualGroup(7,5,3)}),"source PC queues fire and poison presentation without DoT delegation capability");
        StatusVisualReport? received=null;
        await Wait(()=>received!=null || host.TryTakeStatusVisuals(out received));
        check(received!.Source==guest.LocalId && received.Groups.Length==2,"actual TCP binds visual source to authenticated connection");
        check(!host.TryTakeHit(out _),"status visual transport cannot generate an HP hit or a native ailment application");
        guest.RequestHit(1,42,7);
        HitRequest? hit=null;await Wait(()=>hit!=null || host.TryTakeHit(out hit));
        check(hit!.Damage==7 && hit.Ailment==null && !host.TryTakeHit(out _),"source-calculated damage travels independently and once");
        guest.PublishStatusVisuals(0,42,new[]{new StatusVisualGroup(1,9,2)});
        await Task.Delay(120);
        check(!host.TryTakeStatusVisuals(out _),"stale instance visual report is rejected on real transport");
        check(!guest.PublishStatusVisuals(1,42,new[]{new StatusVisualGroup(1,1,float.NaN)}),"malformed native visual report cannot enter network queue");
        // Coalesced targets are not guaranteed global sequence order. Both must survive.
        guest.PublishStatusVisuals(1,43,new[]{new StatusVisualGroup(7,2,2)});
        guest.PublishStatusVisuals(1,42,Array.Empty<StatusVisualGroup>());
        var reports=new Dictionary<int,StatusVisualReport>();
        await Wait(()=>{while(host.TryTakeStatusVisuals(out var item)) reports[item!.EnemyId]=item;return reports.Count==2;});
        check(reports[42].Groups.Length==0 && reports[43].Groups.Single().Count==2,"coalesced independent targets and explicit cleanse survive their own sequence streams");
        guest.Publish(new PlayerState {Scene="status-map",Available=true,Dead=true});
        await Wait(()=>host.PlayerPositions.Any(p=>p.Id==guest.LocalId && p.Dead));
        guest.RequestHit(1,42,3);
        await Task.Delay(120);
        check(!host.TryTakeHit(out _),"dead creator cannot issue ordinary attacks");
        guest.RequestHit(1,42,2,sourceDot:true);
        HitRequest? survivingDot=null;await Wait(()=>survivingDot!=null || host.TryTakeHit(out survivingDot));
        check(survivingDot!.SourceDot && survivingDot.Damage==2 && survivingDot.Ailment==null,"native DoT which survives creator death still subtracts its source-calculated HP result once");
        check(!new HitRequest {Epoch=1,EnemyId=42,Sequence=1,SourceDot=true,ControlType=1,ControlDuration=1}.IsValid(),
            "DoT flag cannot authorize CC requests from a dead creator");
        guest.Publish(new PlayerState {Scene="other-map",Available=true});
        await Wait(()=>host.PlayerPositions.Any(p=>p.Id==guest.LocalId && p.Scene=="other-map"));
        guest.PublishStatusVisuals(1,42,new[]{new StatusVisualGroup(7,9,2)});
        await Task.Delay(120);
        check(!host.TryTakeStatusVisuals(out _),"different-map actor cannot change target visuals");
        check(host.Connected && guest.Connected,"status validation failures do not break healthy room");
    }
}
