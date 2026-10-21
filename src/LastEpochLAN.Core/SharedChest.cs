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
    public long ExpectedRevision {get;init;}=-1;
    public SharedStashTab? Settings {get;init;}
    public string[] Order {get;init;}=Array.Empty<string>();
    public string Kind {get;init;}="query";
    public string ChestId {get;init;}="";
    public string Operation {get;init;}="";
    public string ItemId {get;init;}="";
    public ChestItem? Item {get;init;}
    public string Ticket {get;init;}="";
    public bool Valid()=>SharedStashRules.ValidTabIndex(Tab) && (Kind=="query" ? ChestId.Length<=32 && Operation=="" && Item==null && Settings==null && Order.Length==0 :
        Kind is "add-tab" or "configure-tab" or "sort-tab" ? Guid.TryParseExact(ChestId,"N",out _) && Guid.TryParseExact(Operation,"N",out _) &&
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
    public SharedStashTab[] Tabs {get;init;}=SharedStashRules.InitialTabs();
    public string Error {get;init;}="";
    public string ChestId {get;init;}="";
    public string HostIdentity {get;init;}="";
    public long Revision {get;init;}
    public string Operation {get;init;}="";
    public string Status {get;init;}="view";
    public ChestItem? Grant {get;init;}
    public ChestItem[] Items {get;init;}=Array.Empty<ChestItem>();
    public bool Valid()=>SharedStashRules.ValidTabs(Tabs) && Tab>=0 && Tab<Tabs.Length && ChangedTab>=-1 && ChangedTab<Tabs.Length && Error is {Length:<=256} && (Error=="" || Status=="view" && Grant==null && Items is {Length:0}) &&
        Guid.TryParseExact(ChestId,"N",out _) && Guid.TryParseExact(HostIdentity,"N",out _) && Revision>=0 &&
        (Operation=="" || Guid.TryParseExact(Operation,"N",out _)) && Status is "view" or "stored" or "reserved" or "done" or "unavailable" or "full" or "tab-added" or "configured" or "sorted" or "conflict" &&
        (Grant==null || Grant.Valid() && Grant.Tab==Tab) && Items is {Length:<=SharedStashRules.ItemsPerTab} && Items.All(i=>i!=null && i.Valid() && i.Tab==Tab) && Items.Select(i=>i.Id).Distinct().Count()==Items.Length;
}
public sealed record ChestRequest(string SenderId,ChestCommand Command);
public sealed record ChestOperation(string Id,string Owner,string Kind,string ItemId,ChestItem? Item,string State,string Ticket="");
public sealed record ChestState
{
    public SharedStashTab[] Tabs {get;init;}=SharedStashRules.InitialTabs();
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
            !SharedStashRules.ValidTabs(value.Tabs) || value.Edits.Length>4096 || value.Edits.Any(e=>!e.Valid(value.Tabs.Length)) ||
            value.Edits.Select(e=>e.Id).Distinct().Count()!=value.Edits.Length ||
            value.Items.Concat(value.Operations.Where(o=>o.State=="reserved").Select(o=>o.Item!)).GroupBy(i=>i.Tab).Any(g=>g.Key>=value.Tabs.Length || g.Count()>SharedStashRules.ItemsPerTab) ||
            value.Items.Any(i=>!i.Valid()) || value.Items.Select(i=>i.Id).Distinct().Count()!=value.Items.Length || value.Items.Select(i=>i.NativeId).Distinct().Count()!=value.Items.Length ||
            value.Operations.Select(o=>o.Id).Distinct().Count()!=value.Operations.Length ||
            value.Operations.Any(o=>!Guid.TryParseExact(o.Id,"N",out _) || o.Owner.Length is <1 or >256 ||
                o.Kind is not ("deposit" or "withdraw") || o.State is not ("stored" or "reserved" or "done") || o.Item?.Valid()!=true || o.ItemId!=o.Item.Id || o.Item.Tab>=value.Tabs.Length))
            throw new InvalidDataException("Chest journal invalid; automatic recovery disabled");
        foreach(var op in value.Operations.Where(o=>o.State=="reserved"))
            if(value.Items.Any(i=>i.Id==op.ItemId))throw new InvalidDataException("Reserved item also visible");
    }
    private void Commit(ChestState next)
    {Validate(next);DurableJson.Write(path,next);state=next;}
    private void Reload()
    {var current=JsonSerializer.Deserialize<ChestState>(File.ReadAllText(path))??throw new InvalidDataException("Empty chest");Validate(current);if(current.Id!=state.Id)throw new InvalidDataException("Chest identity replaced during session");state=current;}
    private ChestReply ViewCore(int tab=0)=>new(){Tab=tab,Tabs=state.Tabs.ToArray(),ChestId=state.Id,HostIdentity=host,Revision=state.Revision,Items=state.Items.Where(i=>i.Tab==tab).ToArray()};
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
        if(command.Kind is "add-tab" or "configure-tab" or "sort-tab")return EditTab(owner,command);
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
        ChestReply Reply(string status,int changed=-1)=>ViewCore(command.Tab) with{Operation=command.Operation,Status=status,ChangedTab=changed};
        var previous=state.Edits.FirstOrDefault(e=>e.Id==command.Operation);
        if(previous!=null)
        {
            if(previous.Owner!=owner || previous.Payload!=payload)throw new InvalidDataException("Tab edit operation ID reused");
            return Reply(previous.Status,previous.ChangedTab);
        }
        if(state.Operations.Any(o=>o.Id==command.Operation))throw new InvalidDataException("Operation ID already used by item transfer");
        if(command.ExpectedRevision!=state.Revision)return Reply("conflict");
        if(state.Edits.Length>=4096)return Reply("full");
        int changed=command.Tab;
        var tabs=state.Tabs;
        var items=state.Items;
        string status;
        if(command.Kind=="add-tab")
        {
            if(tabs.Length>=SharedStashRules.MaxTabs)return Reply("full");
            changed=tabs.Length;tabs=tabs.Append(new SharedStashTab(changed,"Shared "+(changed+1))).ToArray();status="tab-added";
        }
        else if(command.Kind=="configure-tab")
        {tabs=tabs.Select(t=>t.Id==command.Tab?command.Settings!:t).ToArray();status="configured";}
        else
        {
            var current=items.Where(i=>i.Tab==command.Tab).ToDictionary(i=>i.Id);
            if(current.Count!=command.Order.Length || command.Order.Any(id=>!current.ContainsKey(id)))return Reply("conflict");
            items=items.Where(i=>i.Tab!=command.Tab).Concat(command.Order.Select(id=>current[id])).ToArray();status="sorted";
        }
        var edit=new SharedStashEdit(command.Operation,owner,payload,status,changed);
        Commit(state with{Revision=state.Revision+1,Tabs=tabs,Items=items,Edits=state.Edits.Append(edit).ToArray()});
        return Reply(status,changed);
    }

}

public sealed record ChestLocalTransaction
{
    public int Tab {get;init;}
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
        if(!SharedStashRules.ValidTabIndex(value.Tab) || value.Item!=null && value.Item.Tab!=value.Tab || !Guid.TryParseExact(value.Id,"N",out _) || !Guid.TryParseExact(value.ChestId,"N",out _) || !Guid.TryParseExact(value.Host,"N",out _) ||
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
