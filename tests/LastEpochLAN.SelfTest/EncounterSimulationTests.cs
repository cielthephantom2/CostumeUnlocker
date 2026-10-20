using System.Net;
using LastEpochLAN.Core;
using LastEpochLAN.Core.Encounters;

internal static class EncounterSimulationTests
{
    internal static async Task Run(Action<bool,string> check)
    {
        using var host=LanSession.Host("Encounter simulator host","encounter-simulation","simulation",0,IPAddress.Loopback);
        using var client1=await LanSession.JoinAsync("127.0.0.1",host.Port,"Client1","encounter-simulation","simulation");
        using var client2=await LanSession.JoinAsync("127.0.0.1",host.Port,"Client2","encounter-simulation","simulation");
        PlayerState State(string character)=>new(){Scene="encounter-map",Available=true,Cooperative=true,GiftIdentity=Guid.NewGuid().ToString("N"),CharacterId=character};
        var hs=State("host");var cs1=State("client1");var cs2=State("client2");
        host.Publish(hs);client1.Publish(cs1);client2.Publish(cs2);
        await Until(()=>host.PlayerPositions.Count(p=>p.CharacterId!="")==3);
        string root=Path.Combine(Path.GetTempPath(),"LastEpochEncounterSimulation-"+Guid.NewGuid().ToString("N"));
        using var hostReceipts=new EncounterRewardAuthority(root);
        var driver=new EncounterHostDriver(host,hostReceipts);
        var adapters=new[]{EncounterKind.Nemesis,EncounterKind.RiftBeast,EncounterKind.OmenWindow,EncounterKind.BloodCrystal}
            .Select(k=>new SimulatedFamily(k,driver)).ToArray();
        foreach(var adapter in adapters)driver.Attach(adapter);
        var instance=new EncounterInstance(Guid.NewGuid(),1,"encounter-map","");driver.Begin(instance);
        var ids=adapters.ToDictionary(a=>a.Kind,a=>driver.Register(a.Kind,new()));driver.Pump();
        var area=new EncounterArea(new(){X=2,Z=3},12,true);
        var actorLinks=new EncounterActors(1,new[]{11},new[]{12,13});
        check(driver.UpdateWorld(ids[EncounterKind.RiftBeast],actorLinks,new("rift-map","rift-run")),"simulator: Rift map and stable Host actor IDs accepted");
        actorLinks.Bosses[0]=999;
        check(driver.Snapshot!.Entries.Single(e=>e.Id==ids[EncounterKind.RiftBeast]).Actors!.Bosses[0]==11,"simulator: actor link input cannot mutate authority after publication");
        check(!driver.UpdateWorld(ids[EncounterKind.Nemesis],new(1,new[]{11},new[]{11})) &&
            !driver.UpdateWorld(ids[EncounterKind.Nemesis],null,new("rift-map","rift-run")),"simulator: duplicate actor IDs and foreign-family travel rejected");
        check(driver.UpdateArea(ids[EncounterKind.BloodCrystal],area) && driver.UpdateArea(ids[EncounterKind.OmenWindow],area),"simulator: native trigger area accepted for Blood Crystal and Omen");
        check(!driver.UpdateArea(ids[EncounterKind.Nemesis],area) && !driver.UpdateArea(ids[EncounterKind.BloodCrystal],area with {Radius=float.NaN}),"simulator: invalid or unrelated trigger area refused");
        driver.Pump();
        await Until(()=>client1.Encounters?.Snapshot?.Entries.Length==4 && client2.Encounters?.Snapshot?.Entries.Length==4);
        await Until(()=>client1.Encounters?.Snapshot?.Entries.Single(e=>e.Id==ids[EncounterKind.BloodCrystal]).Area==area);
        check(client1.Encounters!.Snapshot!.Entries.Single(e=>e.Id==ids[EncounterKind.BloodCrystal]).Area==area,"simulator: trigger center/radius/active state propagated over TCP");
        var presentation=new EncounterClientView();
        check(presentation.Apply(client1.Encounters!,cs1),"simulator: client presentation accepts its own baseline");
        check(!presentation.Apply(client1.Encounters!,cs1),"simulator: client refuses duplicate/out-of-order delivery");
        var selected=presentation.Choose(cs1,ids[EncounterKind.Nemesis],EncounterAction.Challenge);
        check(selected is {TurnRevision:0,Action:EncounterAction.Challenge},"simulator: private client action references current map and turn revision");
        check(presentation.Choose(cs1 with {X=100},ids[EncounterKind.Nemesis],EncounterAction.Challenge)==null,"simulator: distant client cannot interact through presentation");
        check(presentation.Choose(cs1 with {CharacterId="other"},ids[EncounterKind.Nemesis],EncounterAction.Challenge)==null,"simulator: character swap invalidates previous presentation actions");
        check(client1.Encounters!.Snapshot!.Entries.Length==4,"simulator: all four families discovered through actual TCP baseline");
        using var j1=new EncounterRewardJournal(root,EncounterHost.CharacterKey(cs1));
        using var j2=new EncounterRewardJournal(root,EncounterHost.CharacterKey(cs2));
        bool competingRefused=false;
        try{using var competing=new EncounterRewardJournal(root,EncounterHost.CharacterKey(cs1));}catch(IOException){competingRefused=true;}
        check(competingRefused,"simulator: exclusive character receipt writer prevents two executors granting the same reward");
        long seq1=0,seq2=0,seqHost=0;
        async Task Send(LanSession room,EncounterKind kind,EncounterAction action,long sequence)
        {
            var entry=driver.Snapshot!.Entries.Single(e=>e.Id==ids[kind]);
            check(room.SendEncounter(new(instance.Id,instance.Epoch,entry.Id,entry.Revision,sequence,action)),"simulator: command queued "+kind+"/"+action);
            await Until(()=>adapters.Single(a=>a.Kind==kind).Executions.Any(e=>e.Reservation.PlayerId==room.LocalId),()=>driver.Pump());
        }
        foreach(var kind in new[]{EncounterKind.RiftBeast,EncounterKind.OmenWindow,EncounterKind.BloodCrystal})
        {
            await Send(client1,kind,EncounterAction.Activate,++seq1);
            await Until(()=>client1.Encounters?.Snapshot?.Entries.Single(e=>e.Id==ids[kind]).Phase==EncounterPhase.Completed &&
                client2.Encounters?.Snapshot?.Entries.Single(e=>e.Id==ids[kind]).Phase==EncounterPhase.Completed,()=>driver.Pump());
            var a=adapters.Single(a=>a.Kind==kind);
            check(a.Executions.Count==1,"simulator: synchronous "+kind+" completion deferred until native acceptance");
            check(j1.Offer(client1.Encounters!,ids[kind])!=null && j2.Offer(client2.Encounters!,ids[kind])!=null,"simulator: separate reward offers "+kind);
            check(client1.Encounters!.Snapshot!.Entries.Single(e=>e.Id==ids[kind]).Recipients.SequenceEqual(new[]{EncounterHost.CharacterKey(cs1)}),"simulator: private recipients do not leak "+kind);
            var original=driver.Snapshot!.Entries.Single(e=>e.Id==ids[kind]).Completion;
            driver.Observe(instance,ids[kind],true,a.Executions[0].Reservation);driver.Pump();
            check(driver.Snapshot!.Entries.Single(e=>e.Id==ids[kind]).Completion==original,"simulator: duplicate completion stable "+kind);
        }
        await Send(client1,EncounterKind.Nemesis,EncounterAction.Challenge,++seq1);
        await Send(client2,EncounterKind.Nemesis,EncounterAction.Empower,++seq2);
        await Send(host,EncounterKind.Nemesis,EncounterAction.Banish,++seqHost);
        await Until(()=>client1.Encounters?.Turns.SingleOrDefault()?.Phase==NemesisTurnPhase.Completed && client2.Encounters?.Turns.SingleOrDefault()?.Phase==NemesisTurnPhase.Completed,()=>driver.Pump());
        check(client1.Encounters!.Turns.Single().Choice==EncounterAction.Challenge && client2.Encounters!.Turns.Single().Choice==EncounterAction.Empower,"simulator: 1 Host + 2 Clients choose Nemesis independently");
        check(j1.Offer(client1.Encounters!,ids[EncounterKind.Nemesis])!=null && j2.Offer(client2.Encounters!,ids[EncounterKind.Nemesis])==null,"simulator: Challenge reward offered; Empower does not mint loot");
        check(j2.Offer(client1.Encounters!,ids[EncounterKind.Nemesis])==null,"simulator: private turn cannot transfer rewards to another character");
        var battleScheduler=new NemesisBattleScheduler();
        var nemesisExecutions=adapters.Single(a=>a.Kind==EncounterKind.Nemesis).Executions;
        check(battleScheduler.Add(nemesisExecutions[0]),"simulator: independent Challenge admitted to shared native combat plan");
        var battle=battleScheduler.Start(ids[EncounterKind.Nemesis])!;
        check(battle!=null && battleScheduler.Start(ids[EncounterKind.Nemesis])==null,"simulator: exactly one native combat start for a Nemesis group");
        check(battleScheduler.Add(nemesisExecutions[1]) && !battleScheduler.Add(nemesisExecutions[2]),"simulator: Empower may join running fight; Banish does not spawn combat");
        check(!battleScheduler.Add(nemesisExecutions[0]),"simulator: replayed private turn cannot join combat twice");
        var completedTurns=battleScheduler.Complete(battle!);
        check(completedTurns.Length==2 && completedTurns.Select(t=>t.Action).Distinct().Count()==2,"simulator: shared death keeps separate Challenge/Empower outcomes");
        check(battleScheduler.Complete(battle!).Length==0,"simulator: duplicate shared death cannot complete private turns again");
        var rewards=adapters.Select(a=>new SimulatedRewards(a.Kind)).ToArray();
        var pump=new EncounterRewardPump(j1,rewards);pump.Pump();pump.Pump();
        check(j1.Records.All(r=>r.Stage==EncounterRewardStage.Applying) && rewards.Sum(r=>r.Applied)==4,"simulator: native rewards run once, await save before receipt");
        foreach(var reward in rewards)reward.SaveConfirmed=true;
        pump.Pump();pump.Pump();
        check(j1.Records.All(r=>r.Stage==EncounterRewardStage.Saved) && rewards.Sum(r=>r.Applied)==4,"simulator: save confirmation gates saved receipts without regrant");
        foreach(var record in j1.Records)
        {
            var ack=presentation.Acknowledge(record,cs1)!;
            check(ack!=null && client1.SendEncounter(ack),"simulator: saved reward receipt sent to Host through encounter channel");
            await Until(()=>hostReceipts.IsAcknowledged(record.CharacterKey,record.Completion),()=>driver.Pump());
            EncounterReply? receiptReply=null;
            await Until(()=>
            {
                while(client1.TryTakeEncounterReply(out var reply))
                    if(reply!.Sequence==ack!.Sequence && reply.Encounter==ack.Encounter && reply.Code==EncounterReplyCode.RewardAcknowledged)receiptReply=reply;
                return receiptReply!=null;
            });
            check(j1.Transition(record.Completion,EncounterRewardStage.Acknowledged),"simulator: local receipt acknowledged only after durable Host acknowledgment");
            check(!hostReceipts.Acknowledge(cs2 with {CharacterId="unentitled"},ack!) && !hostReceipts.Acknowledge(cs1,ack! with {RewardCompletion=Guid.NewGuid()}),"simulator: unentitled character or unissued reward receipt refused");
        }
        j1.Dispose();
        using var reopened=new EncounterRewardJournal(root,EncounterHost.CharacterKey(cs1));
        new EncounterRewardPump(reopened,rewards).Pump();
        check(reopened.Records.All(r=>r.Stage==EncounterRewardStage.Acknowledged) && rewards.Sum(r=>r.Applied)==4,"simulator: reconnect does not regrant saved rewards");
        var crashRecord=j2.Records.First();j2.Transition(crashRecord.Completion,EncounterRewardStage.Applying);
        j2.Dispose();
        using var crashReopen=new EncounterRewardJournal(root,EncounterHost.CharacterKey(cs2));
        check(crashReopen.Records.Single(r=>r.Completion==crashRecord.Completion).Stage==EncounterRewardStage.Uncertain,"simulator: crash during native grant quarantines receipt instead of replay");
        check(!crashReopen.Transition(crashRecord.Completion,EncounterRewardStage.Applying),"simulator: uncertain reward cannot automatically restart");
        var partialReward=new SimulatedRewards(EncounterKind.OmenWindow){ThrowAfterMutation=true};
        var partialPump=new EncounterRewardPump(crashReopen,new[]{partialReward});
        partialPump.Pump();partialPump.Pump();
        check(partialReward.Applied==1 && partialPump.LastError!="" && crashReopen.Records.Any(r=>r.Kind==EncounterKind.OmenWindow && r.Stage==EncounterRewardStage.Uncertain),"simulator: exception after mutation quarantines reward without retry");
        var oldExecution=adapters[0].Executions[0];
        driver.EndInstance();
        check(!driver.Observe(instance,oldExecution.View.Id,true,oldExecution.Reservation),"simulator: callback after map disposal rejected");
        instance=instance with {Id=Guid.NewGuid(),Epoch=2};driver.Begin(instance);
        check(!driver.Observe(oldExecution.Instance,oldExecution.View.Id,true,oldExecution.Reservation),"simulator: callback from previous map cannot mutate new instance");
        check(driver.Snapshot!.Entries.Length==0,"simulator: map transition clears encounter registrations");
        await Until(()=>client1.Encounters?.Snapshot?.Instance.Epoch==2);
        check(client1.Encounters!.Snapshot!.Entries.Length==0,"simulator: map transition delivered to client");
        var acknowledgedRecord=reopened.Records.First();
        var lateAck=new EncounterCommand(acknowledgedRecord.Instance,acknowledgedRecord.Epoch,acknowledgedRecord.Encounter,1,999,EncounterAction.AcknowledgeReward){RewardCompletion=acknowledgedRecord.Completion};
        check(hostReceipts.Acknowledge(cs1,lateAck),"simulator: receipt retransmission remains idempotent after map change");
        presentation.ClearPresentation();
        check(presentation.Choose(cs1,ids[EncounterKind.Nemesis],EncounterAction.Challenge)==null,"simulator: cleared context cannot send old interaction");
        foreach(var adapter in adapters)adapter.SynchronousCompletion=false;
        var activeId=driver.Register(EncounterKind.BloodCrystal,new());driver.Pump();
        var entry=driver.Snapshot!.Entries.Single(e=>e.Id==activeId);
        check(client1.SendEncounter(new(instance.Id,instance.Epoch,activeId,entry.Revision,++seq1,EncounterAction.Activate)),"simulator: reserve active encounter for departure/late-join scenario");
        await Until(()=>driver.Snapshot!.Entries.Single(e=>e.Id==activeId).Phase==EncounterPhase.Active,()=>driver.Pump());
        client2.Publish(cs2 with {Available=false});
        await Until(()=>host.PlayerPositions.Single(p=>p.Id==client2.LocalId).Available==false);
        // Client2 was present at activation but left before completion.
        driver.Observe(instance,activeId,true);driver.Pump();
        check(!driver.Snapshot!.Entries.Single(e=>e.Id==activeId).Recipients.Contains(EncounterHost.CharacterKey(cs2)),"simulator: departed participant is not rewarded at completion");
        var lateId=driver.Register(EncounterKind.OmenWindow,new());driver.Pump();
        entry=driver.Snapshot!.Entries.Single(e=>e.Id==lateId);
        client1.SendEncounter(new(instance.Id,instance.Epoch,lateId,entry.Revision,++seq1,EncounterAction.Activate));
        await Until(()=>driver.Snapshot!.Entries.Single(e=>e.Id==lateId).Phase==EncounterPhase.Active,()=>driver.Pump());
        client2.Publish(cs2);
        await Until(()=>host.PlayerPositions.Single(p=>p.Id==client2.LocalId).Available);
        driver.Observe(instance,lateId,true);driver.Pump();
        check(!driver.Snapshot!.Entries.Single(e=>e.Id==lateId).Recipients.Contains(EncounterHost.CharacterKey(cs2)),"simulator: late arrival sees state but does not acquire an earlier reward turn");
        var omenAdapter=adapters.Single(a=>a.Kind==EncounterKind.OmenWindow);
        omenAdapter.SynchronousCompletion=true;omenAdapter.NextDispatch=EncounterNativeDispatch.RejectedBeforeExecution;
        var rejectedId=driver.Register(EncounterKind.OmenWindow,new());
        entry=driver.Snapshot!.Entries.Single(e=>e.Id==rejectedId);int beforeReject=omenAdapter.Executions.Count;
        client1.SendEncounter(new(instance.Id,instance.Epoch,rejectedId,entry.Revision,++seq1,EncounterAction.Activate));
        await Until(()=>omenAdapter.Executions.Count>beforeReject,()=>driver.Pump());
        check(driver.Snapshot!.Entries.Single(e=>e.Id==rejectedId).Phase==EncounterPhase.Available,"simulator: rejected native dispatch restores available state; premature completion ignored");
        omenAdapter.ThrowDuringDispatch=true;
        entry=driver.Snapshot!.Entries.Single(e=>e.Id==rejectedId);beforeReject=omenAdapter.Executions.Count;
        client1.SendEncounter(new(instance.Id,instance.Epoch,rejectedId,entry.Revision,++seq1,EncounterAction.Activate));
        await Until(()=>omenAdapter.Executions.Count>beforeReject,()=>driver.Pump());
        check(driver.Snapshot!.Entries.Single(e=>e.Id==rejectedId).Phase==EncounterPhase.Faulted && driver.LastDispatchError!="","simulator: uncertain native dispatch faults encounter instead of completing/retrying");
        var receiptRecord=crashReopen.Records.First(r=>r.Stage==EncounterRewardStage.Offered);
        crashReopen.Transition(receiptRecord.Completion,EncounterRewardStage.Applying);crashReopen.Transition(receiptRecord.Completion,EncounterRewardStage.Saved);
        var acknowledgments=new EncounterRewardAcknowledgments(crashReopen,new EncounterClientView());
        acknowledgments.SendSaved(client2,cs2,100);
        await Until(()=>hostReceipts.IsAcknowledged(receiptRecord.CharacterKey,receiptRecord.Completion),()=>driver.Pump());
        EncounterReply? ackReply=null;
        await Until(()=>
        {
            while(client2.TryTakeEncounterReply(out var reply))
                if(reply!.Code==EncounterReplyCode.RewardAcknowledged)ackReply=reply;
            return ackReply!=null;
        });
        check(!acknowledgments.Accept("old-connection",ackReply!),"simulator: receipt from another connection cannot close a local save");
        check(acknowledgments.Accept(client2.LocalId,ackReply!) && acknowledgments.Pending==0,"simulator: tracked receipt closes only after matching Host reply");
        driver.EndInstance();
        check(adapters.All(a=>a.EndedInstances==2),"simulator: all adapter bindings released once on each instance end");
        hostReceipts.Dispose();
        using var reopenedHost=new EncounterRewardAuthority(root);
        check(reopenedHost.IsAcknowledged(acknowledgedRecord.CharacterKey,acknowledgedRecord.Completion),"simulator: Host receipt survives process/room restart");
        File.WriteAllText(Path.Combine(root,"simulation-result.txt"),"PASS: managed simulation only; no Last Epoch process/save used");
        Console.WriteLine("Simulation receipts retained: "+root);
    }
    private static async Task Until(Func<bool> ready,Action? tick=null)
    {
        long deadline=Environment.TickCount64+7000;
        while(!ready() && Environment.TickCount64<deadline){tick?.Invoke();await Task.Delay(10);}
        if(!ready())throw new TimeoutException("Encounter simulation synchronization timed out.");
    }
    private sealed class SimulatedFamily(EncounterKind kind,EncounterHostDriver driver):IEncounterFamilyAdapter
    {
        public EncounterKind Kind=>kind;
        public readonly List<EncounterExecution> Executions=new();
        public bool SynchronousCompletion=true;
        public EncounterNativeDispatch NextDispatch=EncounterNativeDispatch.Started;
        public bool ThrowDuringDispatch;
        public int EndedInstances;
        public void EndInstance(EncounterInstance instance){EndedInstances++;}
        public EncounterNativeDispatch Execute(EncounterExecution execution)
        {
            Executions.Add(execution);
            if(SynchronousCompletion)driver.Observe(execution.Instance,execution.View.Id,true,execution.Reservation);
            if(ThrowDuringDispatch)throw new InvalidOperationException("Simulated native dispatch fault");
            return NextDispatch;
        }
    }
    private sealed class SimulatedRewards(EncounterKind kind):IEncounterPrivateRewardExecutor
    {
        public EncounterKind Kind=>kind;
        public int Applied;
        public bool SaveConfirmed;
        public bool ThrowAfterMutation;
        public EncounterRewardApplyResult Apply(EncounterRewardRecord record){Applied++;if(ThrowAfterMutation)throw new InvalidOperationException("Simulated native partial mutation");return EncounterRewardApplyResult.AwaitingSave;}
        public bool IsCharacterSaveConfirmed(EncounterRewardRecord record)=>SaveConfirmed;
    }
}
