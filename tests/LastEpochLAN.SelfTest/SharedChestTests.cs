using LastEpochLAN.Core;
using System.Security.Cryptography;
using System.Text.Json;

internal static class SharedChestTests
{
    private static string Id()=>Guid.NewGuid().ToString("N");
    private static ChestItem Item(string id,int seed=1)
    {var bytes=BitConverter.GetBytes(seed);return new(id,Convert.ToBase64String(bytes),Convert.ToHexString(SHA256.HashData(bytes)),"QA item "+seed,(uint)(seed+16));}
    internal static void Run(Action<bool,string> check)
    {
        string root=Path.Combine(Path.GetTempPath(),"ChestQA-"+Id());Directory.CreateDirectory(root);
        string path=Path.Combine(root,"host.json"),host=Id();
        var store=new SharedChestStore(path,host);string chest=store.View().ChestId;
        var deposit=new ChestCommand{Kind="deposit",ChestId=chest,Operation=Id()};deposit=deposit with{Item=Item(deposit.Operation)};
        check(store.Apply("alice:A",deposit).Status=="stored" && store.View().Items.Length==1,"chest deposit durably enters Host custody");
        for(int n=0;n<100;n++)store.Apply("alice:A",deposit);
        store=new(path,host);
        check(store.View().Items.Length==1,"100 duplicate deposit packets and Host restart preserve exactly one stored item");
        bool failed=false;try{store.Apply("bob:B",deposit);}catch(InvalidDataException){failed=true;}
        check(failed,"different character cannot reuse a deposit operation ID");
        failed=false;try{store.Apply("alice:A",deposit with{Item=Item(deposit.Operation,2)});}catch(InvalidDataException){failed=true;}
        check(failed,"same operation cannot substitute another serialized item");
        failed=false;try{store.Apply("alice:A",deposit with{ChestId=Id()});}catch(InvalidDataException){failed=true;}
        check(failed,"stale/different Host chest cannot accept a pending transaction");
        var takeA=new ChestCommand{Kind="withdraw",ChestId=chest,Operation=Id(),ItemId=deposit.Operation};
        var takeB=takeA with{Operation=Id()};
        var replies=new ChestReply[2];
        Parallel.Invoke(()=>replies[0]=store.Apply("alice:A",takeA),()=>replies[1]=store.Apply("bob:B",takeB));
        check(replies.Count(r=>r.Status=="reserved")==1 && replies.Count(r=>r.Status=="unavailable")==1 && store.View().Items.Length==0,
            "two concurrent users withdrawing same slot produce one reservation and one rejection");
        int winner=replies[0].Status=="reserved"?0:1;string owner=winner==0?"alice:A":"bob:B";var take=winner==0?takeA:takeB;
        store=new(path,host);
        check(store.View().Items.Length==0 && store.Apply(owner,take).Grant==deposit.Item,"disconnect/restart never releases a withdrawal reservation; same owner recovers exact payload");
        for(int n=0;n<100;n++)check(store.Apply(owner,take).Status=="reserved","duplicate grant retry retains one reservation #"+n);
        failed=false;try{store.Apply("other:character",take with{Kind="ack"});}catch(InvalidDataException){failed=true;}
        check(failed,"another character cannot acknowledge or steal reserved withdrawal");
        var ack=take with{Kind="ack"};
        check(store.Apply(owner,ack).Status=="done","receiver native-save acknowledgment finalizes reservation");
        store=new(path,host);
        check(store.Apply(owner,ack).Status=="done" && store.Apply(owner,take).Grant==null && store.View().Items.Length==0,
            "lost ACK and old withdrawal replay after restart cannot regrant a completed item");
        check(store.Apply("alice:A",deposit).Status=="stored" && store.View().Items.Length==0,"old deposit retry after withdrawal does not resurrect chest item");
        string replayId=Id();var staleDeposit=new ChestCommand{Kind="deposit",ChestId=chest,Operation=replayId,Item=deposit.Item! with{Id=replayId}};
        failed=false;try{store.Apply(owner,staleDeposit);}catch(InvalidDataException){failed=true;}
        check(failed,"new operation ID cannot replay an old deposit generation after item has already been withdrawn");
        check(store.Apply(owner,staleDeposit with{Ticket=take.Operation}).Status=="stored","current saved recipient may redeposit using its completed withdrawal custody ticket");
        var finishReplay=new ChestCommand{Kind="withdraw",ChestId=chest,Operation=Id(),ItemId=replayId};store.Apply(owner,finishReplay);store.Apply(owner,finishReplay with{Kind="ack"});
        var secondStore=new SharedChestStore(path,host);
        string multi=Id();var multiDeposit=new ChestCommand{Kind="deposit",ChestId=chest,Operation=multi,Item=Item(multi,400)};
        store.Apply(owner,multiDeposit);
        var multiTake=new ChestCommand{Kind="withdraw",ChestId=chest,Operation=Id(),ItemId=multi};
        var firstGrant=store.Apply(owner,multiTake);
        var secondGrant=secondStore.Apply("bob:B",multiTake with{Operation=Id()});
        check(firstGrant.Status=="reserved" && secondGrant.Status=="unavailable","independent Host store instances reread custody under OS file lock; stale process cannot grant same item");
        store.Apply(owner,multiTake with{Kind="ack"});
        var other=new ChestCommand{Kind="deposit",ChestId=chest,Operation=Id()};other=other with{Item=Item(other.Operation,7)};
        store.Apply(owner,other);var snapshot=store.View();snapshot.Items[0]=Item(Id(),9);
        check(store.View().Items[0]==other.Item,"mutating a UI snapshot cannot change Host ledger");
        var local=new ChestClientJournal(Path.Combine(root,"client"));
        var tx=new ChestLocalTransaction{Id=Id(),ChestId=chest,Host=host,Kind="withdraw",ItemId=other.Operation,Stage="Waiting"};
        local.Put(tx);tx=tx with{Item=other.Item,Before=0,Stage="Applying"};local.Put(tx);
        local=new(Path.Combine(root,"client"));
        check(local.Find(tx.Id)==tx,"client persists payload and applying intent before native inventory add");
        check(ChestClientJournal.Recover(tx,0)=="Waiting" && ChestClientJournal.Recover(tx,1)=="Added" && ChestClientJournal.Recover(tx,2)=="Held",
            "crash before/after incoming native save retries only absent item, acknowledges present item, holds ambiguous count");
        var outgoing=new ChestLocalTransaction{Id=other.Operation,ChestId=chest,Host=host,Kind="deposit",ItemId=other.Operation,Item=other.Item,Before=1,Stage="Preparing"};
        check(ChestClientJournal.Recover(outgoing,1)=="Cancelled" && ChestClientJournal.Recover(outgoing,0)=="Removed" && ChestClientJournal.Recover(outgoing,2)=="Held",
            "crash before/after outgoing native save cancels or resumes escrow from exact durable inventory count");
        local.Put(tx with{Stage="Added"});local.Put(tx with{Stage="Done"});
        failed=false;try{local.Put(tx);}catch(InvalidDataException){failed=true;}
        check(failed,"terminal client receipt cannot reopen into another native item add");
        var journalConflict=tx with{Id=Id(),Stage="Waiting"};local.Put(journalConflict);
        failed=false;try{local.Put(journalConflict with{Host=Id()});}catch(InvalidDataException){failed=true;}
        check(failed,"pending client escrow cannot silently migrate to another Host");
        string corrupt=Path.Combine(root,"corrupt.json");File.WriteAllText(corrupt,"{broken");
        failed=false;try{_=new SharedChestStore(corrupt,host);}catch(JsonException){failed=true;}
        check(failed,"corrupt Host ledger fails closed rather than rebuilding an empty chest");
        string failingPath=Path.Combine(root,"disk-failure.json");var failStore=new SharedChestStore(failingPath,host);
        Directory.CreateDirectory(failingPath+".new");
        string failId=Id();failed=false;
        try{failStore.Apply(owner,new(){Kind="deposit",ChestId=failStore.View().ChestId,Operation=failId,Item=Item(failId,44)});}catch(IOException){failed=true;}catch(UnauthorizedAccessException){failed=true;}
        check(failed && failStore.View().Items.Length==0 && new SharedChestStore(failingPath,host).View().Items.Length==0,
            "failed Host disk write never publishes or acknowledges an in-memory item absent from durable ledger");
        var full=new SharedChestStore(Path.Combine(root,"full.json"),host);string fullId=full.View().ChestId;
        for(int i=0;i<96;i++){string id=Id();full.Apply(owner,new(){Kind="deposit",ChestId=fullId,Operation=id,Item=Item(id,100+i)});}
        string overflow=Id();var rejected=full.Apply(owner,new(){Kind="deposit",ChestId=fullId,Operation=overflow,Item=Item(overflow,999)});
        check(rejected.Status=="full" && full.View().Items.Length==96,"full chest rejects new transfer without deleting any stored item");
        var reserved=full.View().Items[0];full.Apply(owner,new(){Kind="withdraw",ChestId=fullId,Operation=Id(),ItemId=reserved.Id});
        check(full.Apply(owner,new(){Kind="deposit",ChestId=fullId,Operation=overflow,Item=Item(overflow,999)}).Status=="full",
            "in-flight reserved item still consumes capacity, preventing overflow on ambiguous recovery");
        // Deterministic network-fault simulation: sender save, dropped offer,
        // duplicate grants, receiver crash, dropped ACK, Host restart.
        int delivered=0;
        var sim=new SharedChestStore(Path.Combine(root,"simulation.json"),host);string simId=sim.View().ChestId;
        for(int round=0;round<150;round++)
        {
            string id=Id();var item=Item(id,1000+round);var offer=new ChestCommand{Kind="deposit",ChestId=simId,Operation=id,Item=item};
            int sender=0,receiver=0;
            sim.Apply(owner,offer);if(round%2==0)sim=new(Path.Combine(root,"simulation.json"),host);
            sim.Apply(owner,offer);
            var withdrawal=new ChestCommand{Kind="withdraw",ChestId=simId,Operation=Id(),ItemId=id};
            var grant=sim.Apply(owner,withdrawal);
            var client=new ChestLocalTransaction{Id=withdrawal.Operation,ChestId=simId,Host=host,Kind="withdraw",ItemId=id,Item=grant.Grant,Stage="Applying",Before=0};
            if(round%3==0){check(ChestClientJournal.Recover(client,receiver)=="Waiting","simulated receiver crash before save resumes only absent item");}
            receiver=1;
            sim=new(Path.Combine(root,"simulation.json"),host);
            sim.Apply(owner,withdrawal); // lost grant ACK, retries must not add again
            if(ChestClientJournal.Recover(client,receiver)!="Added")throw new Exception("Unsafe recovery");
            sim.Apply(owner,withdrawal with{Kind="ack"});sim.Apply(owner,withdrawal with{Kind="ack"});
            if(sender+receiver+sim.View().Items.Length!=1 || sim.Apply(owner,withdrawal).Grant!=null)throw new Exception("Lost/duplicate custody");
            delivered++;
        }
        check(delivered==150,"150 disconnect/restart/replay/crash simulations conserve exactly one item per transfer");
        var participation=new PartyParticipation();
        check(!participation.CanShareWorld(false,"map","map",false,false),"Join alone cannot take over local world, even in same map as Host");
        participation.TeleportRequested();
        check(participation.CanShareWorld(false,"map","map",false,false) && !participation.CanShareWorld(false,"map","other",false,false),
            "explicit Teleport enables shared world only in matching map");
        participation.Disconnect();check(!participation.Enabled,"disconnect clears explicit co-op participation");
    }
    internal static async Task Transport(Action<bool,string> check)
    {
        using var host=LanSession.Host("Host","build","chest-qa",0);
        using var guest=await LanSession.JoinAsync("127.0.0.1",host.Port,"Client","build","chest-qa",CancellationToken.None);
        host.Publish(new(){Available=true,Scene="host-map",GiftIdentity=Id(),CharacterId="H",GiftGroup="mode"});
        guest.Publish(new(){Available=true,Scene="guest-map",GiftIdentity=Id(),CharacterId="G",GiftGroup="mode"});
        ChestRequest? request=null;
        guest.SendChest(new());
        var deadline=DateTime.UtcNow.AddSeconds(5);
        while(DateTime.UtcNow<deadline && !host.TryTakeChestRequest(out request))await Task.Delay(10);
        check(request!=null && request.SenderId==guest.LocalId && request.Command.Kind=="query","real TCP chest query gets authenticated connection sender while players remain in different maps");
        var reply=new ChestReply{ChestId=Id(),HostIdentity=host.PlayerPositions.First().GiftIdentity};
        host.ReplyChest(guest.LocalId,reply);ChestReply? received=null;deadline=DateTime.UtcNow.AddSeconds(5);
        while(DateTime.UtcNow<deadline && !guest.TryTakeChestReply(out received))await Task.Delay(10);
        check(received?.ChestId==reply.ChestId && received.Valid(),"Host chest projection crosses framed TCP independently of world scene");
        check(!guest.ReplyChest(host.LocalId,reply),"Guest cannot send authoritative chest projection");
    }
}
