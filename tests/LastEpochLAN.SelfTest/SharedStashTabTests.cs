using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using LastEpochLAN.Core;

internal static class SharedStashTabTests
{
    private static string Id()=>Guid.NewGuid().ToString("N");
    private static ChestItem Item(string id,int seed,int tab)
    {
        byte[] bytes=BitConverter.GetBytes(seed);
        return new(id,Convert.ToBase64String(bytes),Convert.ToHexString(SHA256.HashData(bytes)),"Shared test "+seed,(uint)(seed+16)){Tab=tab};
    }
    internal static void Run(Action<bool,string> check)
    {
        string root=Path.Combine(Path.GetTempPath(),"SharedTabQA-"+Id());Directory.CreateDirectory(root);
        string path=Path.Combine(root,"host.json"),host=Id();
        var store=new SharedChestStore(path,host);string chest=store.View().ChestId;
        ChestCommand Deposit(int tab,int seed)
        {string id=Id();return new(){Kind="deposit",Tab=tab,ChestId=chest,Operation=id,Item=Item(id,seed,tab)};}
        ChestCommand Edit(string kind,int tab=0)=>new(){Kind=kind,Tab=tab,ChestId=chest,Operation=Id(),ExpectedRevision=store.View(tab).Revision};
        check(store.View().Tabs.Length==6 && Enumerable.Range(0,6).All(t=>store.View(t).Valid()),"Shared stash starts with six valid independent 96-item tabs");
        for(int tab=0;tab<6;tab++)
        {
            for(int slot=0;slot<96;slot++)if(store.Apply("player:"+(tab%3),Deposit(tab,1000+tab*96+slot)).Status!="stored")throw new Exception("Tab filling failed");
            check(store.View(tab).Items.Length==96 && store.View(tab).Items.All(i=>i.Tab==tab),"Three players can fill shared tab "+tab+" without a global 96-item limit");
        }
        store=new(path,host);
        check(Enumerable.Range(0,6).Sum(t=>store.View(t).Items.Length)==576,"576 stored items across all six tabs survive Host restart");
        check(store.Apply("player:0",Deposit(0,9000)).Status=="full","A full tab rejects its 97th item without spilling into another tab");
        var reservedItem=store.View(4).Items[0];
        var withdrawal=new ChestCommand{Kind="withdraw",Tab=4,ChestId=chest,Operation=Id(),ItemId=reservedItem.Id};
        var reserved=store.Apply("player:1",withdrawal);
        check(reserved.Status=="reserved" && reserved.Valid() && reserved.Grant==reservedItem,"Cross-tab withdrawal grants exact item from requested tab only");
        check(store.Apply("player:2",Deposit(4,9001)).Status=="full","Unacknowledged withdrawal keeps capacity reserved in its own tab");
        var forged=withdrawal with{Operation=Id(),Tab=0,ItemId=store.View(5).Items[0].Id};
        check(store.Apply("player:2",forged).Status=="unavailable","A withdrawal cannot access another tab by substituting item ID");
        var request=withdrawal with{Kind="ack"};
        store.Apply("player:1",request);store=new(path,host);
        var redeposit=Deposit(5,9002) with{Ticket=withdrawal.Operation};
        // Open a slot in tab5, then redeposit the exact withdrawn native item.
        var tab5item=store.View(5).Items[0];var free=new ChestCommand{Kind="withdraw",Tab=5,ChestId=chest,Operation=Id(),ItemId=tab5item.Id};
        store.Apply("player:2",free);store.Apply("player:2",free with{Kind="ack"});
        redeposit=redeposit with{Item=reservedItem with{Id=redeposit.Operation,Tab=5}};
        check(store.Apply("player:1",redeposit).Status=="stored","Saved withdrawal custody ticket allows redeposit into a different tab");
        bool rejected=false;
        try{store.Apply("player:2",redeposit with{Operation=Id(),Item=redeposit.Item! with{Id=Id(),Tab=0},Tab=0});}catch(InvalidDataException){rejected=true;}
        check(rejected,"Invalid command/item tab or ID mismatch cannot mutate any tab");
        var add=Edit("add-tab");var added=store.Apply("player:0",add);
        check(added.Status=="tab-added" && added.ChangedTab==6 && added.Tabs.Length==7 && added.Valid(),"Native plus command durably creates a new empty shared tab");
        store=new(path,host);
        check(store.Apply("player:0",add).Tabs.Length==7 && store.View(6).Items.Length==0,"Lost add-tab reply and Host restart cannot create a duplicate tab on retry");
        var simultaneous=Edit("add-tab");
        var nextAdded=store.Apply("player:1",simultaneous);
        var conflictingAdd=simultaneous with{Operation=Id()};
        check(nextAdded.Status=="tab-added" && store.Apply("player:2",conflictingAdd).Status=="conflict" && store.View().Tabs.Length==8,
            "Two players adding tabs from the same revision create one tab and return a refreshable conflict");
        var cloneAttempt=Deposit(6,9500) with{Item=redeposit.Item! with{Id=Id(),Tab=6}};
        cloneAttempt=cloneAttempt with{Operation=cloneAttempt.Item!.Id};
        rejected=false;try{store.Apply("player:1",cloneAttempt);}catch(InvalidDataException){rejected=true;}
        check(rejected && store.View(6).Items.Length==0,"Global native-ID custody prevents depositing the same item into a second tab");
        var settings=new SharedStashTab(6,"Boss drops",3,4);
        var configure=Edit("configure-tab",6) with{Settings=settings};
        var configured=store.Apply("player:1",configure);
        check(configured.Status=="configured" && store.View(6).Tabs[6]==settings,"Configure Tab name, color and icon are stored on Host");
        var stale=configure with{Operation=Id(),Settings=settings with{Name="Stale edit"}};
        check(store.Apply("player:2",stale).Status=="conflict" && store.View(6).Tabs[6]==settings,"Concurrent stale Configure Tab cannot overwrite newer settings");
        rejected=false;try{store.Apply("player:2",configure);}catch(InvalidDataException){rejected=true;}
        check(rejected,"Another player cannot replay someone else's tab-edit operation ID");
        store=new(path,host);
        check(store.Apply("player:1",configure).Status=="configured" && store.View(6).Tabs[6]==settings,"Configure retry remains idempotent after restart");
        var before=store.View(2).Items;
        var order=before.Select(i=>i.Id).Reverse().ToArray();
        var sort=Edit("sort-tab",2) with{Order=order};
        check(store.Apply("player:2",sort).Status=="sorted" && store.View(2).Items.Select(i=>i.Id).SequenceEqual(order),"Shared Sort persists the exact native-projection item order for the selected tab");
        check(store.View(2).Items.OrderBy(i=>i.Id).SequenceEqual(before.OrderBy(i=>i.Id)),"Sorting changes only order: item payload, native ID and tab are conserved");
        store=new(path,host);
        check(store.Apply("player:2",sort).Status=="sorted" && store.View(2).Items.Select(i=>i.Id).SequenceEqual(order),"Sort retry and Host restart do not replay item transfers");
        var badOrder=Edit("sort-tab",2) with{Order=order.Skip(1).ToArray()};
        check(store.Apply("player:2",badOrder).Status=="conflict" && store.View(2).Items.Length==96,"Sort cannot remove an item by omitting it from the permutation");
        var query=store.View(1);
        check(!(query with{Tab=0}).Valid() && !(query with{Items=query.Items.Concat(new[]{query.Items[0]}).ToArray()}).Valid(),"Network validation rejects wrong-tab projections and oversized/duplicate page payloads");
        foreach(int tab in new[]{-1,SharedStashRules.MaxTabs})
            check(!new ChestCommand{Tab=tab}.Valid(),"Out-of-range shared tab rejected: "+tab);
        check(!new SharedStashTab(0,"<color=red>bad").Valid() && !new SharedStashTab(0," ").Valid() && !new SharedStashTab(0,new string('a',33)).Valid(),
            "Tab names reject markup, blanks and oversized labels before networking");
        // Legacy JSON has no Tabs/Edits/Tab properties. Default initialization
        // must preserve existing item IDs, custody history and pending grants.
        var old=JsonNode.Parse(File.ReadAllText(path))!.AsObject();old.Remove("Tabs");old.Remove("Edits");
        var legacyItem=Item(Id(),30000,0);
        old["Items"]=JsonSerializer.SerializeToNode(new[]{legacyItem});
        old["Operations"]=new JsonArray();
        old["Items"]![0]!.AsObject().Remove("Tab");
        string legacyPath=Path.Combine(root,"legacy.json");File.WriteAllText(legacyPath,old.ToJsonString());
        var legacy=new SharedChestStore(legacyPath,host);
        check(legacy.View().Items.Single()==legacyItem && legacy.View().Tabs.Length==6 && legacy.View(5).Items.Length==0,
            "Legacy single-tab ledger migrates nondestructively into tab1 with six tabs available");
        var legacyGrant=Item(Id(),31000,0);string legacyOp=Id();
        var legacyState=new ChestState{Id=chest,Revision=10,Operations=new[]{new ChestOperation(legacyOp,"legacy:owner","withdraw",legacyGrant.Id,legacyGrant,"reserved")}};
        var legacyJson=JsonSerializer.SerializeToNode(legacyState)!.AsObject();legacyJson.Remove("Tabs");legacyJson.Remove("Edits");
        legacyJson["Operations"]![0]!["Item"]!.AsObject().Remove("Tab");
        string heldPath=Path.Combine(root,"legacy-reservation.json");File.WriteAllText(heldPath,legacyJson.ToJsonString());
        var held=new SharedChestStore(heldPath,host);
        var resume=new ChestCommand{Kind="withdraw",ChestId=chest,Operation=legacyOp,ItemId=legacyGrant.Id};
        check(held.View().Items.Length==0 && held.Apply("legacy:owner",resume).Grant==legacyGrant,
            "Legacy unacknowledged withdrawal remains hidden escrow and recovers in tab1 with the original operation ID");
        held.Apply("legacy:owner",resume with{Kind="ack"});
        check(held.Apply("legacy:owner",resume).Grant==null,"Acknowledging a migrated legacy grant cannot regrant or resurrect the item");
        var local=new ChestClientJournal(Path.Combine(root,"client"));
        var pending=new ChestLocalTransaction{Id=Id(),ChestId=chest,Host=host,Tab=4,Kind="withdraw",ItemId=reservedItem.Id,Item=reservedItem,Before=0,Stage="Applying"};
        local.Put(pending);local=new(Path.Combine(root,"client"));
        check(local.Find(pending.Id)!.Tab==4 && ChestClientJournal.Recover(pending,1)=="Added","Pending client escrow retains original tab through restart and native-save recovery");
        rejected=false;try{local.Put(pending with{Tab=5});}catch(InvalidDataException){rejected=true;}
        check(rejected,"Pending withdrawal cannot migrate to another tab during recovery");
        while(store.View().Tabs.Length<SharedStashRules.MaxTabs)store.Apply("player:0",Edit("add-tab"));
        check(store.Apply("player:0",Edit("add-tab")).Status=="full" && store.View().Tabs.Length==24,"Plus button respects bounded24-tab storage without deleting existing tabs/items");
        // Two worst-sized pages fit together in the existing frame budget.
        byte[] maxData=Enumerable.Range(0,1024).Select(i=>(byte)i).ToArray();
        var worst=Enumerable.Range(0,96).Select(i=>new ChestItem(Id(),Convert.ToBase64String(maxData),Convert.ToHexString(SHA256.HashData(maxData)),new string('漢',128),(uint)(i+16))).ToArray();
        var worstReply=store.View(0) with{Items=worst};
        check(worstReply.Valid() && JsonSerializer.SerializeToUtf8Bytes(new Packet{Kind="snapshot",ChestReplies=new[]{worstReply,worstReply}}).Length<600000,
            "96-item page queries keep two maximum-sized snapshots below600KB instead of sending the whole stash");
    }

    internal static async Task Transport(Action<bool,string> check)
    {
        using var host=LanSession.Host("Host","build","tabs-qa",0);
        using var a=await LanSession.JoinAsync("127.0.0.1",host.Port,"Client A","build","tabs-qa",CancellationToken.None);
        using var b=await LanSession.JoinAsync("127.0.0.1",host.Port,"Client B","build","tabs-qa",CancellationToken.None);
        string hostIdentity=Id();
        host.Publish(new(){Available=true,GiftIdentity=hostIdentity,CharacterId="H",GiftGroup="mode"});
        a.Publish(new(){Available=true,GiftIdentity=Id(),CharacterId="A",GiftGroup="mode"});
        b.Publish(new(){Available=true,GiftIdentity=Id(),CharacterId="B",GiftGroup="mode"});
        a.SendChest(new(){Tab=1});b.SendChest(new(){Tab=5});
        var requests=new List<ChestRequest>();var deadline=DateTime.UtcNow.AddSeconds(5);
        while(requests.Count<2 && DateTime.UtcNow<deadline){while(host.TryTakeChestRequest(out var request))requests.Add(request!);await Task.Delay(10);}
        check(requests.Count==2 && requests.Select(r=>r.Command.Tab).OrderBy(t=>t).SequenceEqual(new[]{1,5}),"Host + two real TCP clients query different shared tabs independently");
        foreach(var request in requests)
        {
            int tab=request.Command.Tab;
            host.ReplyChest(request.SenderId,new(){ChestId=Id(),HostIdentity=hostIdentity,Tab=tab,Items=new[]{Item(Id(),40000+tab,tab)}});
        }
        ChestReply? first=null,second=null;deadline=DateTime.UtcNow.AddSeconds(5);
        while((first==null || second==null) && DateTime.UtcNow<deadline)
        {if(a.TryTakeChestReply(out var r))first=r;if(b.TryTakeChestReply(out var r2))second=r2;await Task.Delay(10);}
        check(first?.Tab==1 && first.Items.All(i=>i.Tab==1) && second?.Tab==5 && second.Items.All(i=>i.Tab==5),
            "Paged TCP replies deliver only each client's selected tab without mixing views");
    }
}
