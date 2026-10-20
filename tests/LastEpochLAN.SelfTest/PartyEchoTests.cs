using System.Net;
using System.Net.Sockets;
using LastEpochLAN.Core;

internal static class PartyEchoTests
{
    internal static async Task Run(Action<bool,string> check)
    {
        var state=new PartyEchoState {Revision=1,Active=true,RunId=Guid.NewGuid().ToString("N"),Scene="echo-A",HostScene="echo-A",
            Run=new byte[]{1,2,3},Metadata=new byte[]{4,5},Objectives=new[]{new EchoObjective(0,1,1,2,3,0,false)}};
        check(state.IsValid(),"Host Echo context validates before native import");
        check(PartyEchoRules.LocalGameplayReady(true,false,false,false),
            "party-only Guest remains locally ready while Host Echo is active without borrowing or matching its context");
        check(PartyEchoRules.LocalGameplayReady(false,false,false,false),"ordinary Host/solo local character remains ready");
        check(!PartyEchoRules.SharedWorldReady(true,true,true,false),
            "party-only Guest cannot enter active Host Echo world even with sticky participation and identical scene");
        check(PartyEchoRules.SharedWorldReady(true,true,true,true) && !PartyEchoRules.SharedWorldReady(false,true,true,true),
            "borrowed Host world requires matching local readiness");
        check(PartyEchoRules.SharedWorldReady(true,true,false,false) && PartyEchoRules.SharedWorldReady(true,false,true,false),
            "ordinary shared maps and Host retain world readiness");
        check(!PartyEchoRules.LocalGameplayReady(true,false,true,false) && !PartyEchoRules.LocalGameplayReady(true,true,true,true),
            "local availability stays blocked during native Echo transitions");
        check(!PartyEchoRules.LocalGameplayReady(true,true,false,false) && PartyEchoRules.LocalGameplayReady(true,true,false,true) &&
            !PartyEchoRules.LocalGameplayReady(false,true,false,true),"borrowed Echo still requires live Guest context parity");
        check(!(state with {Run=Array.Empty<byte>()}).IsValid() && !(state with {Metadata=Array.Empty<byte>()}).IsValid() &&
            !(state with {RunId="scene-name"}).IsValid(),"active Echo requires both native contexts and a generation identity");
        check(!(state with {Run=new byte[262145]}).IsValid() && !(state with {Metadata=new byte[8193]}).IsValid() &&
            !(state with {Objectives=new EchoObjective[33]}).IsValid() && !(state with {PortalTarget=3}).IsValid(),
            "Echo transfer rejects oversized contexts, excessive objectives and invalid portal destination");
        check(PartyEchoRules.NeedsEntry(state,"",state.Scene) && !PartyEchoRules.NeedsEntry(state,state.RunId,state.Scene) &&
            PartyEchoRules.NeedsEntry(state,state.RunId,"rest"),"same-scene first entry and respawn require the Host generation; matching joined generation stays put");
        check(!PartyEchoRules.NeedsEntry(state with {HostScene="rest"},"","rest") &&
            !PartyEchoRules.NeedsEntry(state with {Active=false},"","rest"),"Guest does not enter before Host spawn or after Echo retirement");
        check(!PartyEchoRules.Matches(state,Guid.NewGuid().ToString("N"),state.Scene),"old Echo generation cannot pass same-map instance matching");
        var echoWorld=new WorldState {EchoRunId=state.RunId,Scene=state.Scene,Epoch=1,Revision=1};
        var writer=new WorldChunkWriter();var reader=new WorldChunkReader();WorldState? decoded=null;
        foreach(var chunk in writer.Encode(echoWorld,true)) decoded=reader.Accept(chunk)??decoded;
        check(decoded?.EchoRunId==state.RunId && !WorldContent.Same(echoWorld,echoWorld with {EchoRunId=Guid.NewGuid().ToString("N")}),
            "world codec and change detection retain Echo generation independently from scene name");
        var copy=state.Copy();copy.Run[0]=99;copy.Metadata[0]=99;
        check(state.Run[0]==1 && state.Metadata[0]==4,"Echo context copies isolate mutable native serialization buffers");
        using var host=LanSession.Host("EchoHost","echo-build","echo-room",0,IPAddress.Loopback);
        host.Publish(new PlayerState {Available=true,Scene=state.Scene});host.PublishEcho(state);
        using var guest=await LanSession.JoinAsync("127.0.0.1",host.Port,"EchoGuest","echo-build","echo-room");
        async Task Wait(Func<bool> condition)
        {
            long end=Environment.TickCount64+5000;
            while(!condition() && Environment.TickCount64<end) await Task.Delay(10);
            if(!condition()) throw new TimeoutException("Echo TCP test timed out.");
        }
        await Wait(()=>guest.Echo?.Revision==1);
        check(guest.Echo!.Run.SequenceEqual(state.Run) && guest.Echo.Metadata.SequenceEqual(state.Metadata) && guest.Echo.RunId==state.RunId,
            "real Host TCP delivers native run and metadata to a Guest still outside the map");
        var returned=guest.Echo!;returned.Run[0]=77;
        check(guest.Echo!.Run[0]==1,"network Echo reads cannot mutate the stored context");
        bool denied=false;try{guest.PublishEcho(state with {Revision=2});}catch(InvalidOperationException){denied=true;}
        check(denied,"Guest cannot publish an authoritative Echo");
        denied=false;try{host.PublishEcho(state);}catch(ArgumentException){denied=true;}
        check(denied,"Host cannot publish stale Echo revision");
        host.PublishEcho(state with {Revision=2,Completed=true,PortalTarget=2,BonusStability=17,MaxBonusStability=50,
            Objectives=new[]{state.Objectives[0] with {Completed=true,Data=9}}});
        await Wait(()=>guest.Echo?.Revision==2);
        check(guest.Echo is {Completed:true,PortalTarget:2,BonusStability:17} && guest.Echo.Objectives[0].Data==9,
            "Host objective progress and completion travel independently from enemy world revisions");
        using var late=await LanSession.JoinAsync("127.0.0.1",host.Port,"LateEchoGuest","echo-build","echo-room");
        await Wait(()=>late.Echo?.Revision==2);
        check(late.Echo!.Completed && late.Echo.RunId==state.RunId,"late join receives full current Echo and completion baseline");
        var next=state with {Revision=3,RunId=Guid.NewGuid().ToString("N")};host.PublishEcho(next);
        await Wait(()=>guest.Echo?.Revision==3);
        check(PartyEchoRules.NeedsEntry(guest.Echo!,state.RunId,state.Scene),"consecutive Host Echoes using one scene trigger a new Guest entry over real TCP");
        host.PublishEcho(new PartyEchoState {Revision=4,HostScene="rest"});
        await Wait(()=>guest.Echo?.Revision==4);
        check(guest.Echo is {Active:false,HostScene:"rest"},"Host leaving retires the shared Echo for every Guest");
        guest.Dispose();check(guest.Echo==null,"disconnect clears the network Echo baseline before reconnect");
        var journalDirectory=Path.Combine(Path.GetTempPath(),"LELAN-Echo-Ledger-"+Guid.NewGuid().ToString("N"));
        var ledger=new EchoRewardLedger(journalDirectory);
        check(!ledger.TryClaim(state.RunId,"guest-A"),"late arrival after completion cannot claim an Echo without recorded participation");
        ledger.Participate(state.RunId,"guest-A");
        check(ledger.TryClaim(state.RunId,"guest-A") && !ledger.TryClaim(state.RunId,"guest-A"),"participating character claims a native completion grant at most once");
        check(!new EchoRewardLedger(journalDirectory).TryClaim(state.RunId,"guest-A"),"write-ahead Echo claim survives a new ledger instance and restart");
        check(!ledger.TryClaim(state.RunId,"guest-B"),"one character cannot reuse another character's Echo participation");
        check(!(state with {Reward=new EchoCompletion(float.NaN,1,1,true,0,true,1,2,1),Completed=true}).IsValid(),
            "non-finite native reward payload cannot reach a Guest grant");
        using var raw=new TcpClient();await raw.ConnectAsync(IPAddress.Loopback,host.Port);
        await Wire.WriteAsync(raw.GetStream(),new Packet {Kind="hello",Name="EchoWireProbe",Build="echo-build",Code="echo-room"},CancellationToken.None);
        var welcome=await Wire.ReadAsync(raw.GetStream(),CancellationToken.None);
        await Wait(()=>host.Players.Any(p=>p.Id==welcome.Id));
        async Task<PartyEchoState> ReadEcho(long rev)
        {
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while(true){var packet=await Wire.ReadAsync(raw.GetStream(),timeout.Token);if(packet.Kind=="echo" && packet.Echo?.Revision==rev) return packet.Echo;}
        }
        var large=state with {Revision=5,Run=new byte[100000]};host.PublishEcho(large);
        check((await ReadEcho(5)).Run.Length==100000,"new Echo transfers its full bounded native context on the actual wire");
        host.PublishEcho(large with {Revision=6,Completed=true,Reward=new EchoCompletion(10,5,1,true,0,true,1,2,1)});
        var progress=await ReadEcho(6);
        check(progress.Run.Length==0 && progress.Metadata.Length==0 && progress.Reward?.Chests==1,
            "objective/reward progress omits unchanged native run blobs from shared TCP traffic");
        await Wait(()=>late.Echo?.Revision==6);
        check(late.Echo!.Run.Length==100000 && late.Echo.Reward?.BonusStability==5,"Guest reconstructs progress from its matching full context baseline");
        await Wire.WriteAsync(raw.GetStream(),new Packet {Kind="state",State=new PlayerState {Sequence=1},Echo=large},CancellationToken.None);
        await Wait(()=>!host.Players.Any(p=>p.Id==welcome.Id));
        check(true,"server disconnects a raw Guest attempting to forge authoritative Echo context");
        string combatRun=Guid.NewGuid().ToString("N");
        host.PublishWorld(new WorldState {Epoch=9,Revision=1,Scene=state.Scene,EchoRunId=combatRun,
            Enemies=new[]{new EnemyState {Id=1,Template="echo-target",Health=100,MaxHealth=100}}});
        late.Publish(new PlayerState {Available=true,Scene=state.Scene,EchoRunId=state.RunId});
        await Wait(()=>late.WorldInfo.Epoch==9 && host.PlayerPositions.Any(p=>p.Id==late.LocalId && p.EchoRunId==state.RunId));
        late.RequestHit(9,1,5);await Task.Delay(150);
        check(!host.TryTakeHit(out _),"same-scene Guest from the previous Echo cannot damage the Host's new generation");
        late.Publish(new PlayerState {Available=true,Scene=state.Scene,EchoRunId=combatRun});
        await Wait(()=>host.PlayerPositions.Any(p=>p.Id==late.LocalId && p.EchoRunId==combatRun));
        late.RequestHit(9,1,5);HitRequest? accepted=null;
        await Wait(()=>accepted!=null || host.TryTakeHit(out accepted));
        check(accepted?.PlayerId==late.LocalId && accepted.Damage==5,"matching Echo generation enables authenticated Guest combat again");
    }
}
