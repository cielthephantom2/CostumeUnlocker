using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using LastEpochLAN.Core;

internal static class SharedStashConfigurationTests
{
    private static string Id()=>Guid.NewGuid().ToString("N");
    internal static void Run(Action<bool,string> check)
    {
        string root=Path.Combine(Path.GetTempPath(),"SharedConfig-"+Id());Directory.CreateDirectory(root);
        string path=Path.Combine(root,"ledger.json"),host=Id();var store=new SharedChestStore(path,host);
        ChestCommand Edit(string kind,int tab=0,SharedStashCategory? category=null)=>new(){Kind=kind,Tab=tab,CategorySettings=category,
            ChestId=store.View().ChestId,ExpectedRevision=store.View().Revision,Operation=Id()};
        var payload=new SharedStashPriority{Rarity=24,Class=4,Corruption=1,MinLevel=12,MaxLevel=100,Types=new[]{1,4},
            Subtypes=new[]{5,6},Factions=new[]{1},Uniques=new[]{24,25},Potential=new[]{1,4,0,28,0,100,0,254}};
        string json=JsonSerializer.Serialize(payload);
        check(SharedStashPriority.ValidJson(json),"all eight native priority conditions fit the bounded managed schema");
        check(!SharedStashPriority.ValidJson(JsonSerializer.Serialize(payload with{MaxLevel=3})) &&
            !SharedStashPriority.ValidJson(JsonSerializer.Serialize(payload with{Potential=new[]{255}})) &&
            !SharedStashPriority.ValidJson(JsonSerializer.Serialize(payload with{Uniques=new[]{1,1}})) &&
            !SharedStashPriority.ValidJson(new string('x',8193)),"malformed ranges, duplicate IDs, wrong potential shape and oversized priority JSON rejected");
        check(store.View().Categories.SequenceEqual(SharedStashCategory.Initial()),"new stash starts in General category without changing six existing tab IDs");
        byte[] raw={3,7,9};string itemId=Id();
        var item=new ChestItem(itemId,Convert.ToBase64String(raw),Convert.ToHexString(SHA256.HashData(raw)),"kept",100);
        store.Apply("peer",new(){Kind="deposit",Tab=0,ChestId=store.View().ChestId,Operation=itemId,Item=item});
        var add=Edit("add-category",category:new(0,"Uniques",3,4,json));var created=store.Apply("peer",add);
        check(created.Status=="category-added" && created.ChangedCategory==1 && created.Categories[1].Priority==json && created.Valid(),
            "Host creates durable category with complete native priority and returns exact category ID");
        check(store.Apply("peer",add).ChangedCategory==1 && store.View().Categories.Length==2,"retry of category creation is idempotent after revision changes");
        bool rejected=false;try{store.Apply("other",add);}catch(InvalidDataException){rejected=true;}
        check(rejected,"another peer cannot replay a category operation ID");
        var settings=store.View().Tabs[0] with{Category=1,Priority=json,IgnoreFolderPriority=true};
        var configure=Edit("configure-tab") with{Settings=settings};store.Apply("peer",configure);
        store=new(path,host);
        check(store.View().Tabs[0]==settings && store.View().Categories[1].Priority==json && store.View().Items.Single()==item,
            "category, all priority conditions, inheritance flag and exact item custody survive Host restart");
        var bad=Edit("configure-tab") with{Settings=settings with{Category=23}};
        check(store.Apply("peer",bad).Status=="conflict" && store.View().Tabs[0]==settings,"tab cannot be assigned to a missing category");
        var stale=Edit("configure-category",category:new(1,"New Name"));
        var duplicate=Edit("duplicate-tab");var duplicateReply=store.Apply("peer",duplicate);
        check(duplicateReply.Status=="tab-added" && duplicateReply.Tabs[6]==settings with{Id=6} && store.View(6).Items.Length==0 && store.View(0).Items.Single()==item,
            "duplicate tab copies configuration including priority but never duplicates item bytes or IDs");
        check(store.Apply("peer",stale).Status=="conflict","concurrent stale category edit cannot overwrite a newer revision");
        var change=Edit("configure-category",category:new(1,"Renamed",5,6,json)) with{MoveTabs=new[]{2,3}};
        store.Apply("peer",change);
        check(store.View().Tabs[2].Category==1 && store.View().Tabs[3].Category==1 && store.View().Categories[1].Name=="Renamed",
            "category configuration atomically stores metadata and moves selected tabs without touching contents");
        var duplicateFolder=Edit("duplicate-category",category:store.View().Categories[1]);
        var folderReply=store.Apply("peer",duplicateFolder);
        check(folderReply.Status=="category-added" && folderReply.ChangedCategory==2 && folderReply.Tabs.Count(t=>t.Category==2)==4 &&
            Enumerable.Range(7,4).All(t=>store.View(t).Items.Length==0),"duplicate category atomically copies its tab rules into empty new tabs");
        store=new(path,host);check(store.Apply("peer",duplicateFolder).ChangedCategory==2 && store.View().Tabs.Length==11,
            "category duplicate retry after Host restart cannot duplicate tabs a second time");
        var addToFolder=Edit("add-tab") with{DestinationCategory=2};var newTab=store.Apply("peer",addToFolder);
        check(newTab.Tabs[newTab.ChangedTab].Category==2,"native add-tab button can create a tab in an empty or selected category");
        var delete=Edit("delete-category",category:store.View().Categories[1]);store.Apply("peer",delete);
        check(store.View().Categories.All(c=>c.Id!=1) && store.View().Tabs[0].Category==0 && store.View().Items.Single()==item,
            "deleting category safely moves tabs to General and preserves all items");
        check(store.Apply("peer",Edit("delete-category",category:store.View().Categories[0])).Status=="conflict","General category cannot be deleted");
        var reserveId=Id();store.Apply("peer",new(){Kind="withdraw",Tab=0,ChestId=store.View().ChestId,Operation=reserveId,ItemId=item.Id});
        check(store.View().Counts[0]==1 && store.View().Items.Length==0,"priority destination capacity includes reserved withdrawals");
        var unbounded=Edit("configure-category",category:new(0,"bad")) with{MoveTabs=Enumerable.Range(0,25).ToArray()};
        check(!unbounded.Valid() && !(Edit("configure-tab") with{Settings=settings with{Priority="{bad"}}).Valid(),
            "wire commands reject excessive tab movement and malformed serialized native rules");
        while(store.View().Tabs.Length<24)store.Apply("peer",Edit("add-tab"));
        long revision=store.View().Revision;int categoryCount=store.View().Categories.Length;
        check(store.Apply("peer",Edit("duplicate-category",category:store.View().Categories.Single(c=>c.Id==2))).Status=="full" &&
            store.View().Revision==revision && store.View().Categories.Length==categoryCount,"category duplicate over capacity is rejected atomically without a partial category");
        string legacy=Path.Combine(root,"legacy.json");
        var node=JsonNode.Parse(File.ReadAllText(path))!.AsObject();node.Remove("Categories");
        foreach(var tab in node["Tabs"]!.AsArray()){tab!.AsObject().Remove("Priority");tab.AsObject().Remove("Category");tab.AsObject().Remove("IgnoreFolderPriority");}
        File.WriteAllText(legacy,node.ToJsonString());
        var migrated=new SharedChestStore(legacy,host);
        check(migrated.View().Categories.Length==1 && migrated.View().Tabs.All(t=>t.Category==0 && t.Priority=="") && migrated.View().Counts[0]==1,
            "legacy ledger lacking configuration fields migrates to General and retains reserved custody");
        var wide=payload with{Types=Enumerable.Range(100000,256).ToArray(),Subtypes=Enumerable.Range(200000,256).ToArray(),
            Factions=Enumerable.Range(300000,256).ToArray(),Uniques=Enumerable.Range(400000,256).ToArray()};
        string wideJson=JsonSerializer.Serialize(wide);
        var maxTabs=Enumerable.Range(0,24).Select(i=>new SharedStashTab(i,new string('漢',32)){Category=i,Priority=wideJson}).ToArray();
        var maxCategories=Enumerable.Range(0,24).Select(i=>new SharedStashCategory(i,new string('漢',32),Priority:wideJson)).ToArray();
        byte[] maxBytes=new byte[1024];string hash=Convert.ToHexString(SHA256.HashData(maxBytes));
        var maxItems=Enumerable.Range(0,96).Select(i=>new ChestItem(Id(),Convert.ToBase64String(maxBytes),hash,new string('漢',128),(uint)(i+16))).ToArray();
        var maxReply=new ChestReply{ChestId=Id(),HostIdentity=Id(),Tabs=maxTabs,Categories=maxCategories,Items=maxItems};
        check(maxReply.Valid() && JsonSerializer.SerializeToUtf8Bytes(new Packet{Kind="snapshot",ChestReplies=new[]{maxReply}}).Length<850000,
            "one maximum item snapshot plus24tabs/24categories of large priority rules remains below wire frame limit");
    }
    internal static async Task Transport(Action<bool,string> check)
    {
        string path=Path.Combine(Path.GetTempPath(),"SharedConfigTcp-"+Id(),"ledger.json");
        var store=new SharedChestStore(path,Id());string chest=store.View().ChestId;
        using var host=LanSession.Host("Host","config-fixture","tabs42",0);
        using var a=await LanSession.JoinAsync("127.0.0.1",host.Port,"A","config-fixture","tabs42");
        using var b=await LanSession.JoinAsync("127.0.0.1",host.Port,"B","config-fixture","tabs42");
        async Task<ChestReply> Exchange(LanSession peer,ChestCommand command)
        {
            peer.SendChest(command);DateTime deadline=DateTime.UtcNow.AddSeconds(5);
            while(DateTime.UtcNow<deadline)
            {
                while(host.TryTakeChestRequest(out var request))host.ReplyChest(request!.SenderId,store.Apply("peer:"+request.SenderId,request.Command));
                if(peer.TryTakeChestReply(out var reply))return reply!;
                await Task.Delay(10);
            }
            throw new TimeoutException("Shared configuration TCP exchange");
        }
        ChestCommand Edit(string kind)=>new(){Kind=kind,ChestId=chest,Operation=Id(),ExpectedRevision=store.View().Revision};
        string rule=JsonSerializer.Serialize(new SharedStashPriority{Rarity=8,Types=new[]{1,2}});
        var created=await Exchange(a,Edit("add-category") with{CategorySettings=new(0,"TCP Category",Priority:rule)});
        check(created.Status=="category-added" && created.Categories[1].Priority==rule,"Host+twoClientsTCP: category/native rule metadata survives framed transport");
        var queried=await Exchange(b,new(){Tab=4});
        check(queried.Categories.SequenceEqual(created.Categories) && queried.Tab==4,"second TCP client sees common categories while viewing a different tab");
        var first=Edit("configure-category") with{CategorySettings=new(1,"A edit",Priority:rule)};
        var stale=Edit("configure-category") with{CategorySettings=new(1,"B edit")};
        var applied=await Exchange(a,first);var conflict=await Exchange(b,stale);
        check(applied.Status=="category-configured" && conflict.Status=="conflict" && conflict.Categories[1].Name=="A edit",
            "Host+twoClientsTCP: stale concurrent category edit refreshes without overwriting peer metadata");
        var config=Edit("configure-tab") with{Settings=store.View().Tabs[0] with{Category=1,Priority=rule,IgnoreFolderPriority=true}};
        var saved=await Exchange(a,config);var second=await Exchange(b,new(){Tab=5});
        check(saved.Tabs[0]==second.Tabs[0] && second.Tabs[0].Priority==rule && second.Tabs[0].Category==1 && second.Tabs[0].IgnoreFolderPriority,
            "both TCP clients receive tab category/rules/inheritance without requiring the same selected tab");
        var duplicate=await Exchange(a,Edit("duplicate-category") with{CategorySettings=store.View().Categories[1]});
        var after=await Exchange(b,new(){Tab=5});
        check(duplicate.ChangedCategory==2 && after.Tabs.Length==7 && after.Tabs[6].Priority==rule && after.Categories.Length==3,
            "category duplication creates one shared empty configured tab visible to both TCP clients");
    }
}
