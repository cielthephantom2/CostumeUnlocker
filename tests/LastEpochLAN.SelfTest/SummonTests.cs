using System.Net;
using System.Text.Json;
using LastEpochLAN.Core;

internal static class SummonTests
{
    internal static async Task Run(Action<bool,string> check)
    {
        string epoch=Guid.NewGuid().ToString("N");
        var summon=new SummonState {Id=1,Template="wolf",Prefab=new() {P0=1,P1=2},
            Animation=new() {Layers=new[]{new AnimationLayer {Hash=123,Time=0.2f,Weight=1}}}};
        var state=new PlayerState {Available=true,Scene="summon-map",SummonEpoch=epoch,SummonSampleTime=10,Summons=new[]{summon}};
        check(state.IsValid(),"summon owner packet validates prefab and animation data");
        check(!(state with {SummonEpoch=""}).IsValid() && !(state with {SummonSampleTime=double.NaN}).IsValid(),"summon packet requires an instance and finite sample time");
        check(!(state with {Summons=new[]{summon,summon}}).IsValid() && !(summon with {Prefab=new()}).IsValid(),"duplicate summon IDs and empty prefab keys are rejected");
        check(!(summon with {Position=new() {X=float.NaN}}).IsValid() && !(summon with {Scale=new() {X=101}}).IsValid(),"summon transforms have finite bounded scale and position");
        var copied=state.Copy();copied.Summons[0].Animation!.Layers[0]=new() {Hash=999};
        check(state.Summons[0].Animation!.Layers[0].Hash==123,"summon animation arrays are detached by player snapshot copy");
        var timeline=new SummonPresentation();
        check(timeline.Receive(epoch,10,new[]{summon},100),"summon presentation accepts initial ownership generation");
        check(timeline.TrySample(1,9.9,out var initial) && initial.State.Template=="wolf","new summon has an immediate visual baseline");
        var moving=summon with {Position=new() {X=1},Yaw=350,AttackSequence=1};
        check(timeline.Receive(epoch,10.1,new[]{moving},100.1),"summon motion and attack share a source timestamp");
        timeline.TrySample(1,10.05,out var between);
        check(between.Pose.Position.X is >0 and <1 && between.State.AttackSequence==0,"summon pose interpolates while a future attack waits for the same cursor");
        timeline.TrySample(1,10.1,out var attack);
        check(attack.State.AttackSequence==1 && Math.Abs((attack.Pose.Yaw-350+540)%360-180)<0.01,"summon attack state advances with its buffered pose");
        check(!timeline.Receive(epoch,10.1,new[]{summon},100.2) && !timeline.Receive(Guid.NewGuid().ToString("N"),9,new[]{summon},100.3),"repeated or stale summon packets cannot rewind presentation or resurrect an old instance");
        timeline.Receive(epoch,10.2,new[]{summon with {Position=new() {X=100}}},100.2);timeline.TrySample(1,10.15,out var teleport);
        check(teleport.Pose.Position.X==100,"summon teleport clears obsolete motion instead of interpolating across the map");
        timeline.Receive(epoch,10.3,Array.Empty<SummonState>(),100.3);
        check(!timeline.States.Any() && !timeline.TrySample(1,10.3,out _),"desummon removes its presentation and buffered attacks");
        timeline.Receive(Guid.NewGuid().ToString("N"),10.4,new[]{summon with {Id=2}},100.4);
        check(!timeline.TrySample(1,10.4,out _) && timeline.TrySample(2,10.4,out _),"new map generation replaces old summon identities");
        using var host=LanSession.Host("Host","fixture","summon-room",0,IPAddress.Loopback);
        host.Publish(state);
        using var guest=await LanSession.JoinAsync("127.0.0.1",host.Port,"Guest","fixture","summon-room");
        guest.Publish(state with {SummonEpoch=Guid.NewGuid().ToString("N"),Summons=new[]{summon with {Template="skeleton",Id=2}}});
        async Task Wait(Func<bool> ready){long until=Environment.TickCount64+5000;while(!ready() && Environment.TickCount64<until)await Task.Delay(10);check(ready(),"summon TCP state arrives before timeout");}
        await Wait(()=>host.Players.Any(p=>p.Id==guest.LocalId && p.Summons.Length==1) && guest.Players.Any(p=>p.Id==host.LocalId && p.Summons.Length==1));
        check(guest.Players.Single(p=>p.Id==host.LocalId).Summons[0].Template=="wolf" && host.Players.Single(p=>p.Id==guest.LocalId).Summons[0].Template=="skeleton",
            "Host and Guest summon rosters travel in both directions over authenticated TCP");
        using var late=await LanSession.JoinAsync("127.0.0.1",host.Port,"Late","fixture","summon-room");
        await Wait(()=>late.Players.Count(p=>p.Summons.Length==1)==2);
        check(late.Players.Single(p=>p.Id==host.LocalId).SummonEpoch==epoch,"late join receives existing summons with the correct owner generation");
        host.Publish(state with {SummonSampleTime=11,Summons=Array.Empty<SummonState>()});
        await Wait(()=>guest.Players.Single(p=>p.Id==host.LocalId).Summons.Length==0);
        check(guest.Players.Single(p=>p.Id==host.LocalId).Available,"removing summons preserves the connected player");
        var maximum=state with {Summons=Enumerable.Range(1,SummonState.MaximumPerPlayer).Select(id=>summon with {Id=id,
            Animation=new() {Layers=Enumerable.Range(0,8).Select(i=>new AnimationLayer {Hash=i+1,Weight=1,Time=99}).ToArray(),
                Parameters=Enumerable.Range(0,24).Select(i=>new AnimationParameter {Hash=i,Type=1,Float=12345}).ToArray()}}).ToArray()};
        check(maximum.IsValid() && JsonSerializer.SerializeToUtf8Bytes(new {players=new[]{maximum,maximum,maximum,maximum}}).Length<1048576,
            "four maximum summon rosters with eight animation layers and 24 parameters fit the framed transport");
        check(!(state with {Summons=Enumerable.Range(1,65).Select(id=>summon with {Id=id}).ToArray()}).IsValid(),"summon roster rejects counts above the explicit resource budget");
    }
}
