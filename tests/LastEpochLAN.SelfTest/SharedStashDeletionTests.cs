using System.Security.Cryptography;
using System.Text.Json;
using LastEpochLAN.Core;

internal static class SharedStashDeletionTests
{
    private static string Id()=>Guid.NewGuid().ToString("N");
    private static ChestItem Item(int tab,uint native)
    {byte[] bytes=BitConverter.GetBytes(native);return new(Id(),Convert.ToBase64String(bytes),Convert.ToHexString(SHA256.HashData(bytes)),"synthetic",native){Tab=tab};}
    internal static void Run(Action<bool,string> check)
    {
        string root=Path.Combine(Path.GetTempPath(),"stash-delete-test-"+Id());Directory.CreateDirectory(root);
        string path=Path.Combine(root,"host.json"),host=Id();var store=new SharedChestStore(path,host);
        ChestCommand Edit(string kind,int tab=0)=>new(){Kind=kind,Tab=tab,ChestId=store.View().ChestId,Operation=Id(),ExpectedRevision=store.View().Revision};
        ChestCommand Deposit(ChestItem item,int generation=0)=>new(){Kind="deposit",Tab=item.Tab,TabGeneration=generation,ChestId=store.View().ChestId,Operation=item.Id,Item=item};
        var kept=Item(4,100);store.Apply("peer",Deposit(kept));var victim=Item(0,101);var originalDeposit=Deposit(victim);store.Apply("peer",originalDeposit);
        long revision=store.View().Revision;
        check(store.Apply("peer",Edit("delete-tab")).Status=="tab-not-empty" && store.View().Revision==revision && store.View().Items.Single()==victim,
            "deleting nonempty tab rejects atomically without changing item or revision");
        var take=new ChestCommand{Kind="withdraw",Tab=0,ChestId=store.View().ChestId,Operation=Id(),ItemId=victim.Id};var reserved=store.Apply("peer",take);
        check(reserved.Status=="reserved" && store.View().Items.Length==0 && store.Apply("peer",Edit("delete-tab")).Status=="tab-not-empty",
            "pending withdrawal blocks deletion even when visible item list is empty");
        store.Apply("peer",take with{Kind="ack"});var deletion=Edit("delete-tab");var deleted=store.Apply("peer",deletion);
        check(deleted.Status=="tab-deleted" && deleted.Tabs[0].Deleted && deleted.Valid() && deleted.Items.Length==0,
            "empty child tab can be deleted through a valid durable Host reply");
        check(store.View(4).Items.Single()==kept && store.View().Tabs.Select(t=>t.Id).SequenceEqual(Enumerable.Range(0,6)),
            "middle/first deletion never renumbers other tabs or moves their item custody");
        check(store.Apply("peer",deletion).Status=="tab-deleted" && store.View().Revision==deleted.Revision,
            "deletion retry is idempotent and survives its target becoming deleted");
        var staleItem=Item(0,102);var staleDeposit=Deposit(staleItem);
        check(store.Apply("peer",staleDeposit).Status=="tab-replaced" && store.View().Revision==deleted.Revision,
            "unseen delayed deposit into deleted slot is rejected without accepting item custody");
        var added=store.Apply("peer",Edit("add-tab",1));
        check(added.ChangedTab==0 && !added.Tabs[0].Deleted && added.Tabs[0].Generation==1 && added.Tabs.Length==6,
            "plus reuses deleted slot with incremented generation instead of exhausting lifetime tab limit");
        check(store.Apply("peer",staleDeposit).Status=="tab-replaced" && store.View(0).Items.Length==0,
            "delayed old-generation deposit cannot land in unrelated replacement tab");
        check(store.Apply("peer",originalDeposit).Status=="stored" && store.View(0).Items.Length==0 && store.Apply("peer",take with{Kind="ack"}).Status=="done",
            "completed old operations still return receipts after reuse without replaying item creation");
        var replacement=Item(0,103);var currentDeposit=Deposit(replacement,1);
        check(store.Apply("peer",currentDeposit).Status=="stored" && store.View(0).Items.Single()==replacement,
            "new-generation deposit works normally in reused slot");
        var currentTake=new ChestCommand{Kind="withdraw",Tab=0,TabGeneration=1,ChestId=store.View().ChestId,Operation=Id(),ItemId=replacement.Id};
        check(store.Apply("peer",currentTake with{TabGeneration=0}).Status=="tab-replaced" && store.View(0).Items.Single()==replacement,
            "stale generation cannot withdraw from replacement tab even with a current item ID");
        var configured=Edit("configure-tab") with{Settings=store.View().Tabs[0] with{Name="replacement"}};
        check(store.Apply("peer",configured).Status=="configured" && store.View().Tabs[0].Generation==1,
            "renaming reused tab preserves generation and all its item bytes");
        var forged=Edit("configure-tab") with{Settings=store.View().Tabs[0] with{Generation=0}};
        check(store.Apply("peer",forged).Status=="conflict","configure cannot roll a tab generation backward");
        store=new(path,host);check(store.View().Tabs[0].Generation==1 && store.View(4).Items.Single()==kept,
            "tab generations and unaffected items survive Host restart");
        store.Apply("peer",currentTake);store.Apply("peer",currentTake with{Kind="ack"});
        for(int cycle=0;cycle<30;cycle++)
        {
            var d=store.Apply("peer",Edit("delete-tab"));var a=store.Apply("peer",Edit("add-tab",1));
            if(d.Status!="tab-deleted" || a.ChangedTab!=0)throw new Exception("slot reuse cycle failed");
        }
        check(store.View().Tabs[0].Generation==31 && store.View().Tabs.Length==6,
            "more than24 delete/add cycles keep24-slot capacity and strictly increasing generations");
        store.Apply("peer",Edit("delete-tab"));
        var folder=store.Apply("peer",Edit("duplicate-category",1) with{CategorySettings=store.View().Categories[0]});
        check(folder.Status=="category-added" && folder.Tabs.Count(t=>!t.Deleted)==10 && folder.Tabs[0].Generation==32 && folder.Tabs.Where(t=>t.Category==folder.ChangedCategory).All(t=>!t.Deleted),
            "category duplicate ignores deleted tabs and safely reuses their slots for empty new tabs");
        check(!SharedStashRules.ValidTabs(store.View().Tabs.Select(t=>t with{Deleted=true}).ToArray()),"wire and persisted model reject an all-deleted stash");
        var legacy=JsonSerializer.Deserialize<SharedStashTab>("{\"Id\":0,\"Name\":\"Old\"}")!;
        check(!legacy.Deleted && legacy.Generation==0 && legacy.Valid(),"old saved tabs migrate with active generation-zero defaults");
        var journal=new ChestClientJournal(Path.Combine(root,"client"));var tx=new ChestLocalTransaction{Tab=0,TabGeneration=31,Id=replacement.Id,ItemId=replacement.Id,ChestId=store.View().ChestId,Host=host,Kind="deposit",Item=replacement,Stage="Removed",Before=1};
        journal.Put(tx);var reloaded=new ChestClientJournal(Path.Combine(root,"client"));
        check(reloaded.Find(tx.Id)!.TabGeneration==31,"durable local transaction retains generation across restart/retry");
        var lastStore=new SharedChestStore(Path.Combine(root,"last.json"),host);
        for(int tab=1;tab<6;tab++)lastStore.Apply("peer",new(){Kind="delete-tab",Tab=tab,ChestId=lastStore.View().ChestId,Operation=Id(),ExpectedRevision=lastStore.View().Revision});
        check(lastStore.Apply("peer",new(){Kind="delete-tab",ChestId=lastStore.View().ChestId,Operation=Id(),ExpectedRevision=lastStore.View().Revision}).Status=="last-tab",
            "Host always preserves at least one usable shared tab");
    }
    internal static async Task Transport(Action<bool,string> check)
    {
        using var host=LanSession.Host("Host","delete-test","protocol44",0);
        using var a=await LanSession.JoinAsync("127.0.0.1",host.Port,"A","delete-test","protocol44");
        using var b=await LanSession.JoinAsync("127.0.0.1",host.Port,"B","delete-test","protocol44");
        var store=new SharedChestStore(Path.Combine(Path.GetTempPath(),"delete-tcp-"+Id(),"host.json"),Id());
        async Task<ChestReply> Exchange(LanSession peer,ChestCommand command)
        {
            peer.SendChest(command);var deadline=DateTime.UtcNow.AddSeconds(5);
            while(DateTime.UtcNow<deadline)
            {
                while(host.TryTakeChestRequest(out var request))host.ReplyChest(request!.SenderId,store.Apply("peer",request.Command));
                if(peer.TryTakeChestReply(out var reply))return reply!;
                await Task.Delay(10);
            }
            throw new TimeoutException("delete TCP exchange");
        }
        ChestCommand Edit(string kind,int tab)=>new(){Kind=kind,Tab=tab,ChestId=store.View().ChestId,Operation=Id(),ExpectedRevision=store.View().Revision};
        var deletion=await Exchange(a,Edit("delete-tab",2));var observed=await Exchange(b,new(){Tab=2});
        check(deletion.Status=="tab-deleted" && observed.Tabs[2].Deleted && observed.Valid(),"two-client TCP exposes deletion of the other client's selected tab as bounded metadata");
        var reuse=await Exchange(a,Edit("add-tab",0));var item=Item(2,1200);
        var rejected=await Exchange(b,new(){Kind="deposit",Tab=2,ChestId=store.View().ChestId,Operation=item.Id,Item=item});
        check(reuse.ChangedTab==2 && reuse.Tabs[2].Generation==1 && rejected.Status=="tab-replaced" && store.View(2).Items.Length==0,
            "two-client TCP rejects a stale deposit after another client deletes and reuses the slot");
        var fresh=await Exchange(b,new(){Kind="deposit",Tab=2,TabGeneration=1,ChestId=store.View().ChestId,Operation=item.Id,Item=item});
        check(fresh.Status=="stored" && fresh.Items.Single()==item,"fresh generation crosses framed TCP and stores item once");
    }
}
