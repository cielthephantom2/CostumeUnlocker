using System.Security.Cryptography;
using System.Text.Json;

namespace LastEpochLAN.Core;

public sealed record ChestItem(string Id,string Data,string Hash,string Name,uint NativeId)
{
    public int Tab {get;init;}
    public bool Valid()
    {
        if(!SharedStashRules.ValidTabIndex(Tab) || NativeId<16 || !Guid.TryParseExact(Id,"N",out _) || Name is not {Length:>0 and <=128} || Data is not {Length:>0 and <=1368}) return false;
        try {var bytes=Convert.FromBase64String(Data);return bytes.Length is >0 and <=1024 && Convert.ToHexString(SHA256.HashData(bytes))==Hash;}
        catch(FormatException){return false;}
    }
}
public sealed record ChestCommand
{
    public int Tab {get;init;}
    public int TabGeneration {get;init;}
    public long ExpectedRevision {get;init;}=-1;
    public int DestinationCategory {get;init;}=-1;
    public SharedStashTab? Settings {get;init;}
    public SharedStashCategory? CategorySettings {get;init;}
    public int[] MoveTabs {get;init;}=Array.Empty<int>();
    public string[] Order {get;init;}=Array.Empty<string>();
    public string Kind {get;init;}="query";
    public string ChestId {get;init;}="";
    public string Operation {get;init;}="";
    public string ItemId {get;init;}="";
    public ChestItem? Item {get;init;}
    public string Ticket {get;init;}="";
    public bool Valid()=> TabGeneration>=0 && (Kind=="add-tab" ? DestinationCategory is >=-1 and <24 : DestinationCategory==-1) && (Kind is "add-category" or "configure-category" or "delete-category" or "duplicate-category" ? CategorySettings?.Valid()==true : CategorySettings==null) && MoveTabs is {Length:<=24} && MoveTabs.Distinct().Count()==MoveTabs.Length && MoveTabs.All(SharedStashRules.ValidTabIndex) && (Kind=="configure-category" || MoveTabs.Length==0) && SharedStashRules.ValidTabIndex(Tab) && (Kind=="query" ? ChestId.Length<=32 && Operation=="" && Item==null && Settings==null && Order.Length==0 :
        Kind is "add-tab" or "delete-tab" or "configure-tab" or "sort-tab" or "duplicate-tab" or "add-category" or "configure-category" or "delete-category" or "duplicate-category" ? Guid.TryParseExact(ChestId,"N",out _) && Guid.TryParseExact(Operation,"N",out _) &&
        ExpectedRevision>=0 && Item==null && ItemId=="" && Ticket=="" &&
        (Kind=="configure-tab" ? Settings?.Valid()==true && Settings.Id==Tab : Settings==null) &&
        (Kind=="sort-tab" ? Order.Length<=SharedStashRules.ItemsPerTab && Order.All(id=>Guid.TryParseExact(id,"N",out _)) && Order.Distinct().Count()==Order.Length : Order.Length==0) :
        Kind is "deposit" or "withdraw" or "ack" && Guid.TryParseExact(ChestId,"N",out _) && Guid.TryParseExact(Operation,"N",out _) &&
        Settings==null && Order.Length==0 && (Ticket=="" || Guid.TryParseExact(Ticket,"N",out _)) &&
        (Kind=="deposit" ? Item?.Valid()==true && Item.Id==Operation && Item.Tab==Tab && ItemId=="" : Item==null && Ticket=="" && Guid.TryParseExact(ItemId,"N",out _)));

}
public sealed record ChestReply
{
    public int Tab {get;init;}
    public int ChangedTab {get;init;}=-1;
    public int ChangedCategory {get;init;}=-1;
    public int[] Counts {get;init;}=Array.Empty<int>();
    public SharedStashTab[] Tabs {get;init;}=SharedStashRules.InitialTabs();
    public SharedStashCategory[] Categories {get;init;}=SharedStashCategory.Initial();
    public string Error {get;init;}="";
    public string ChestId {get;init;}="";
    public string HostIdentity {get;init;}="";
    public long Revision {get;init;}
    public string Operation {get;init;}="";
    public string Status {get;init;}="view";
    public ChestItem? Grant {get;init;}
    public ChestItem[] Items {get;init;}=Array.Empty<ChestItem>();
    public bool Valid()=> (Counts.Length==0 || Counts.Length==Tabs.Length && Counts.All(c=>c is >=0 and <=96)) && ChangedCategory is >=-1 and <24 && SharedStashRules.ValidTabs(Tabs) && SharedStashCategory.ValidAll(Categories,Tabs) && Tab>=0 && Tab<Tabs.Length && ChangedTab>=-1 && ChangedTab<Tabs.Length && Error is {Length:<=256} && (Error=="" || Status=="view" && Grant==null && Items is {Length:0}) &&
        Guid.TryParseExact(ChestId,"N",out _) && Guid.TryParseExact(HostIdentity,"N",out _) && Revision>=0 &&
        (Operation=="" || Guid.TryParseExact(Operation,"N",out _)) && Status is "view" or "stored" or "reserved" or "done" or "unavailable" or "full" or "tab-added" or "tab-deleted" or "tab-not-empty" or "last-tab" or "tab-replaced" or "configured" or "sorted" or "category-added" or "category-configured" or "category-deleted" or "conflict" &&
        (!Tabs[Tab].Deleted || Items.Length==0 && Grant==null) &&
        (Grant==null || Grant.Valid() && Grant.Tab==Tab) && Items is {Length:<=SharedStashRules.ItemsPerTab} && Items.All(i=>i!=null && i.Valid() && i.Tab==Tab) && Items.Select(i=>i.Id).Distinct().Count()==Items.Length;
}
public sealed record ChestRequest(string SenderId,ChestCommand Command);
public sealed record ChestOperation(string Id,string Owner,string Kind,string ItemId,ChestItem? Item,string State,string Ticket="");
public sealed record ChestState
{
    public SharedStashTab[] Tabs {get;init;}=SharedStashRules.InitialTabs();
    public SharedStashCategory[] Categories {get;init;}=SharedStashCategory.Initial();
    public SharedStashEdit[] Edits {get;init;}=Array.Empty<SharedStashEdit>();
    public string Id {get;init;}=Guid.NewGuid().ToString("N");
    public long Revision {get;init;}
    public ChestItem[] Items {get;init;}=Array.Empty<ChestItem>();
    public ChestOperation[] Operations {get;init;}=Array.Empty<ChestOperation>();
}

public static class DurableJson
{
    public static void Write<T>(string path,T data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temp=path+".new";
        using(var file=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))
        {JsonSerializer.Serialize(file,data);file.Flush(true);}
        File.Move(temp,path,true);
    }
}

// Serialized by one Host game-thread owner. No timeout ever makes a reserved
// withdrawal available again: only durable receiver save acknowledgment can
// finalize it. Uncertain transactions remain recoverable escrow, not a refund.
public sealed class SharedChestStore
{
    private readonly string path,host;
    private readonly object gate=new();
    private ChestState state;
    public SharedChestStore(string path,string hostIdentity)
    {
        if(!Guid.TryParseExact(hostIdentity,"N",out _))throw new ArgumentException("Host identity required");
        this.path=path;host=hostIdentity;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var ownership=new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        state=File.Exists(path)?JsonSerializer.Deserialize<ChestState>(File.ReadAllText(path))??throw new InvalidDataException("Empty chest"):new();
        Validate(state);
        if(!File.Exists(path))DurableJson.Write(path,state);
    }
    private static void Validate(ChestState value)
    {
        if(!Guid.TryParseExact(value.Id,"N",out _) || value.Revision<0 || value.Items.Length>SharedStashRules.MaximumItems || value.Operations.Length>16384 ||
            !SharedStashRules.ValidTabs(value.Tabs) || !SharedStashCategory.ValidAll(value.Categories,value.Tabs) || value.Edits.Length>4096 || value.Edits.Any(e=>!e.Valid(value.Tabs.Length)) ||
            value.Edits.Select(e=>e.Id).Distinct().Count()!=value.Edits.Length ||
            value.Items.Concat(value.Operations.Where(o=>o.State=="reserved").Select(o=>o.Item!)).GroupBy(i=>i.Tab).Any(g=>g.Key>=value.Tabs.Length || g.Count()>SharedStashRules.ItemsPerTab) ||
            value.Items.Any(i=>!i.Valid() || i.Tab>=value.Tabs.Length || value.Tabs[i.Tab].Deleted) || value.Items.Select(i=>i.Id).Distinct().Count()!=value.Items.Length || value.Items.Select(i=>i.NativeId).Distinct().Count()!=value.Items.Length ||
            value.Operations.Select(o=>o.Id).Distinct().Count()!=value.Operations.Length ||
            value.Operations.Any(o=>!Guid.TryParseExact(o.Id,"N",out _) || o.Owner.Length is <1 or >256 ||
                o.Kind is not ("deposit" or "withdraw") || o.State is not ("stored" or "reserved" or "done") || o.Item?.Valid()!=true || o.ItemId!=o.Item.Id || o.Item.Tab>=value.Tabs.Length))
            throw new InvalidDataException("Chest journal invalid; automatic recovery disabled");
        foreach(var op in value.Operations.Where(o=>o.State=="reserved"))
            if(value.Tabs[op.Item!.Tab].Deleted || value.Items.Any(i=>i.Id==op.ItemId))throw new InvalidDataException("Reserved item also visible or tab deleted");
    }
    private void Commit(ChestState next)
    {Validate(next);DurableJson.Write(path,next);state=next;}
    private void Reload()
    {var current=JsonSerializer.Deserialize<ChestState>(File.ReadAllText(path))??throw new InvalidDataException("Empty chest");Validate(current);if(current.Id!=state.Id)throw new InvalidDataException("Chest identity replaced during session");state=current;}
    private int[] Occupancy()
    {
        var counts=new int[state.Tabs.Length];
        foreach(var item in state.Items)counts[item.Tab]++;
        foreach(var operation in state.Operations)if(operation.State=="reserved")counts[operation.Item!.Tab]++;
        return counts;
    }
    private ChestReply ViewCore(int tab=0)=>new(){Tab=tab,Tabs=state.Tabs.ToArray(),Categories=state.Categories.ToArray(),Counts=Occupancy(),ChestId=state.Id,HostIdentity=host,Revision=state.Revision,Items=state.Items.Where(i=>i.Tab==tab).ToArray()};
    public ChestReply View(int tab=0)
    {
        lock(gate){using var ownership=new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);Reload();if(tab<0 || tab>=state.Tabs.Length)throw new InvalidDataException("Stash tab unavailable");return ViewCore(tab);}
    }
    public ChestReply Apply(string owner,ChestCommand command)
    {lock(gate){using var ownership=new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);Reload();return ApplyCore(owner,command);}}
    private ChestReply ApplyCore(string owner,ChestCommand command)
    {
        if(owner.Length is <1 or >256 || !command.Valid())throw new InvalidDataException("Invalid chest command");
        if(command.Tab>=state.Tabs.Length)throw new InvalidDataException("Stash tab unavailable");
        if(command.Kind=="query")return ViewCore(command.Tab);
        if(command.ChestId!=state.Id)throw new InvalidDataException("Different Host chest; held transaction cannot migrate");
        ChestReply Reply(string status,ChestItem? grant=null)=>ViewCore(command.Tab) with{Operation=command.Operation,Status=status,Grant=grant};
        if(command.Kind is "add-tab" or "delete-tab" or "configure-tab" or "sort-tab" or "duplicate-tab" or "add-category" or "configure-category" or "delete-category" or "duplicate-category")return EditTab(owner,command);
        if(state.Edits.Any(e=>e.Id==command.Operation))throw new InvalidDataException("Operation ID already used by tab edit");
        var old=state.Operations.FirstOrDefault(o=>o.Id==command.Operation);
        if(old!=null)
        {
            if(old.Owner!=owner || old.Item!.Tab!=command.Tab || (command.Kind=="deposit" ? old.Kind!="deposit" || old.Item!=command.Item || old.Ticket!=command.Ticket : old.Kind!="withdraw" || old.ItemId!=command.ItemId))
                throw new InvalidDataException("Chest operation ID reused by a different owner/payload");
            if(command.Kind=="ack" && old.State=="reserved")
            {Commit(state with{Revision=state.Revision+1,Operations=state.Operations.Select(o=>o.Id==old.Id?o with{State="done"}:o).ToArray()});return Reply("done");}
            return Reply(old.State,old.State=="reserved"?old.Item:null);
        }
        if(command.Kind=="ack")return Reply("unavailable");
        if(state.Tabs[command.Tab].Deleted || state.Tabs[command.Tab].Generation!=command.TabGeneration)return Reply("tab-replaced");
        if(state.Operations.Length>=16384)return Reply("full");
        ChestItem item;
        string result;
        if(command.Kind=="deposit")
        {
            item=command.Item!;
            if(state.Items.Count(i=>i.Tab==command.Tab)+state.Operations.Count(o=>o.State=="reserved" && o.Item!.Tab==command.Tab)>=SharedStashRules.ItemsPerTab)return Reply("full");
            if(state.Items.Any(i=>i.NativeId==item.NativeId) || state.Operations.Any(o=>o.State=="reserved" && o.Item!.NativeId==item.NativeId))
                throw new InvalidDataException("Same item already in chest custody");
            var previous=state.Operations.LastOrDefault(o=>o.Kind=="withdraw" && o.State=="done" && o.Item!.NativeId==item.NativeId);
            if(previous!=null && (previous.Owner!=owner || previous.Id!=command.Ticket))
                throw new InvalidDataException("Old item generation or wrong custodian; deposit held");
            if(previous==null && command.Ticket!="")throw new InvalidDataException("Unknown item custody ticket");
            result="stored";
        }
        else
        {
            item=state.Items.FirstOrDefault(i=>i.Id==command.ItemId)!;
            if(item==null || item.Tab!=command.Tab)return Reply("unavailable");
            result="reserved";
        }
        var operation=new ChestOperation(command.Operation,owner,command.Kind,item.Id,item,result,command.Ticket);
        Commit(state with{Revision=state.Revision+1,Items=result=="stored"?state.Items.Append(item).ToArray():state.Items.Where(i=>i.Id!=item.Id).ToArray(),Operations=state.Operations.Append(operation).ToArray()});
        return Reply(result,result=="reserved"?item:null);
    }
    private ChestReply EditTab(string owner,ChestCommand command)
    {
        string payload=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(command)));
        ChestReply Reply(string status,int changed=-1,int category=-1)=>ViewCore(command.Tab) with{Operation=command.Operation,Status=status,ChangedTab=changed,ChangedCategory=category};
        var previous=state.Edits.FirstOrDefault(e=>e.Id==command.Operation);
        if(previous!=null)
        {
            if(previous.Owner!=owner || previous.Payload!=payload)throw new InvalidDataException("Tab edit operation ID reused");
            return Reply(previous.Status,previous.ChangedTab,previous.ChangedCategory);
        }
        if(state.Operations.Any(o=>o.Id==command.Operation))throw new InvalidDataException("Operation ID already used by item transfer");
        if(command.ExpectedRevision!=state.Revision)return Reply("conflict");
        if(state.Tabs[command.Tab].Deleted)return Reply("conflict");
        if(state.Edits.Length>=4096)return Reply("full");
        int changed=command.Tab;int changedCategory=-1;
        var tabs=state.Tabs;
        var categories=state.Categories;
        var items=state.Items;
        string status;
        int FreeSlot()=>Array.FindIndex(tabs,t=>t.Deleted && t.Generation<int.MaxValue) is var free && free>=0?free:
            tabs.Length<SharedStashRules.MaxTabs?tabs.Length:-1;
        bool Add(SharedStashTab template)
        {
            int id=FreeSlot();if(id<0)return false;
            var added=template with{Id=id,Deleted=false,Generation=id<tabs.Length?tabs[id].Generation+1:0};
            tabs=id==tabs.Length?tabs.Append(added).ToArray():tabs.Select(t=>t.Id==id?added:t).ToArray();changed=id;return true;
        }
        if(command.Kind is "add-tab" or "duplicate-tab")
        {
            if(FreeSlot()<0)return Reply("full");
            if(command.DestinationCategory>=0 && categories.All(c=>c.Id!=command.DestinationCategory))return Reply("conflict");
            var added=command.Kind=="duplicate-tab"?tabs[command.Tab]:new SharedStashTab(0,"Shared "+(FreeSlot()+1)){Category=command.DestinationCategory>=0?command.DestinationCategory:tabs[command.Tab].Category};
            Add(added);status="tab-added";
        }
        else if(command.Kind=="delete-tab")
        {
            if(tabs.Count(t=>!t.Deleted)<=1)return Reply("last-tab");
            if(items.Any(i=>i.Tab==command.Tab) || state.Operations.Any(o=>o.State=="reserved" && o.Item!.Tab==command.Tab))return Reply("tab-not-empty");
            tabs=tabs.Select(t=>t.Id==command.Tab?t with{Deleted=true,Priority="",IgnoreFolderPriority=false}:t).ToArray();status="tab-deleted";
        }
        else if(command.Kind=="duplicate-category")
        {
            var source=categories.FirstOrDefault(c=>c.Id==command.CategorySettings!.Id);
            if(source==null)return Reply("conflict");
            var sourceTabs=tabs.Where(t=>!t.Deleted && t.Category==source.Id).ToArray();
            int capacity=SharedStashRules.MaxTabs-tabs.Length+tabs.Count(t=>t.Deleted && t.Generation<int.MaxValue);
            if(categories.Length>=24 || sourceTabs.Length>capacity)return Reply("full");
            int id=Enumerable.Range(0,24).First(i=>categories.All(c=>c.Id!=i));
            changedCategory=id;categories=categories.Append(source with{Id=id}).ToArray();
            foreach(var sourceTab in sourceTabs)Add(sourceTab with{Category=id});
            changed=command.Tab;status="category-added";
        }
        else if(command.Kind=="add-category")
        {
            if(categories.Length>=24)return Reply("full");
            int id=Enumerable.Range(0,24).First(i=>categories.All(c=>c.Id!=i));
            changedCategory=id;categories=categories.Append(command.CategorySettings! with{Id=id}).ToArray();status="category-added";
        }
        else if(command.Kind is "configure-category" or "delete-category")
        {
            int id=command.CategorySettings!.Id;
            if(categories.All(c=>c.Id!=id) || command.MoveTabs.Any(t=>t>=tabs.Length || tabs[t].Deleted))return Reply("conflict");
            if(command.Kind=="delete-category")
            {
                if(id==0)return Reply("conflict");
                categories=categories.Where(c=>c.Id!=id).ToArray();
                tabs=tabs.Select(t=>t.Category==id?t with{Category=0}:t).ToArray();status="category-deleted";
            }
            else
            {
                categories=categories.Select(c=>c.Id==id?command.CategorySettings!:c).ToArray();
                tabs=tabs.Select(t=>command.MoveTabs.Contains(t.Id)?t with{Category=id}:t).ToArray();status="category-configured";
            }
        }
        else if(command.Kind=="configure-tab")
        {if(categories.All(c=>c.Id!=command.Settings!.Category) || command.Settings!.Deleted || command.Settings.Generation!=tabs[command.Tab].Generation)return Reply("conflict");tabs=tabs.Select(t=>t.Id==command.Tab?command.Settings!:t).ToArray();status="configured";}
        else
        {
            var current=items.Where(i=>i.Tab==command.Tab).ToDictionary(i=>i.Id);
            if(current.Count!=command.Order.Length || command.Order.Any(id=>!current.ContainsKey(id)))return Reply("conflict");
            items=items.Where(i=>i.Tab!=command.Tab).Concat(command.Order.Select(id=>current[id])).ToArray();status="sorted";
        }
        var edit=new SharedStashEdit(command.Operation,owner,payload,status,changed){ChangedCategory=changedCategory};
        Commit(state with{Revision=state.Revision+1,Tabs=tabs,Categories=categories,Items=items,Edits=state.Edits.Append(edit).ToArray()});
        return Reply(status,changed,changedCategory);
    }

}

public sealed record ChestLocalTransaction
{
    public int Tab {get;init;}
    public int TabGeneration {get;init;}
    public string Id {get;init;}="";
    public string ChestId {get;init;}="";
    public string Host {get;init;}="";
    public string Kind {get;init;}="";
    public string ItemId {get;init;}="";
    public ChestItem? Item {get;init;}
    public string Stage {get;init;}="";
    public int Before {get;init;}
    public string Ticket {get;init;}="";
    public long CompletedRevision {get;init;}
}
public sealed class ChestClientJournal
{
    private readonly string root;
    private readonly Dictionary<string,ChestLocalTransaction> records=new();
    public ChestClientJournal(string root)
    {
        this.root=root;Directory.CreateDirectory(root);
        foreach(var file in Directory.GetFiles(root,"*.json"))
        {
            var value=JsonSerializer.Deserialize<ChestLocalTransaction>(File.ReadAllText(file))??throw new InvalidDataException("Invalid client chest journal");
            Validate(value);if(Path.GetFileNameWithoutExtension(file)!=value.Id)throw new InvalidDataException("Chest transaction filename mismatch");records.Add(value.Id,value);
        }
    }
    private static void Validate(ChestLocalTransaction value)
    {
        if(value.TabGeneration<0 || !SharedStashRules.ValidTabIndex(value.Tab) || value.Item!=null && value.Item.Tab!=value.Tab || !Guid.TryParseExact(value.Id,"N",out _) || !Guid.TryParseExact(value.ChestId,"N",out _) || !Guid.TryParseExact(value.Host,"N",out _) ||
            !Guid.TryParseExact(value.ItemId,"N",out _) || value.Kind is not ("deposit" or "withdraw") || value.Before<0 || value.CompletedRevision<0 ||
            value.Stage is not ("Preparing" or "Removed" or "Waiting" or "Applying" or "Added" or "Done" or "Cancelled" or "Held") ||
            (value.Ticket!="" && !Guid.TryParseExact(value.Ticket,"N",out _)) ||
            (value.Item!=null && !value.Item.Valid()) || (value.Kind=="deposit" && (value.Item==null || value.Item.Id!=value.Id)))
            throw new InvalidDataException("Client chest transaction invalid");
    }
    public ChestLocalTransaction[] Records=>records.Values.ToArray();
    public ChestLocalTransaction? Find(string id)=>records.GetValueOrDefault(id);
    public void Put(ChestLocalTransaction value)
    {
        Validate(value);
        if(records.TryGetValue(value.Id,out var old) &&
            (old.Tab!=value.Tab || old.ChestId!=value.ChestId || old.Host!=value.Host || old.Kind!=value.Kind || old.ItemId!=value.ItemId || old.Ticket!=value.Ticket ||
             old.Item!=null && old.Item!=value.Item || old.Stage is "Done" or "Cancelled" && old!=value))
            throw new InvalidDataException("Client chest transaction conflict");
        DurableJson.Write(Path.Combine(root,value.Id+".json"),value);records[value.Id]=value;
    }
    // Loaded character inventory is authoritative after a crash. Exact baseline
    // delta only; any ambiguous count remains held. Never blindly re-add.
    public static string Recover(ChestLocalTransaction tx,int loadedCount)=>tx.Stage switch
    {
        "Preparing"=>loadedCount==tx.Before?"Cancelled":loadedCount==tx.Before-1?"Removed":"Held",
        "Applying"=>loadedCount==tx.Before+1?"Added":loadedCount==tx.Before?"Waiting":"Held",
        _=>tx.Stage
    };
}
