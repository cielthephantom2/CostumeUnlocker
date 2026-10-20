using HarmonyLib;
using Il2Cpp;
using Il2CppCysharp.Threading.Tasks;
using LastEpochLAN.Core;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Il2CppLE.AssetBundles;
using Guid=System.Guid;

namespace LastEpochLAN.Mod;

internal sealed class LanSharedChest
{
    internal static LanSharedChest Active;
    private LanSession session;
    private readonly LanGift identity;
    private ChestClientJournal journal;
    private SharedChestStore hostStore;
    private string character="",group="";
    private ChestReply view;
    private float nextPoll;
    private long hostRequests,transferAttempts;
    private string lastHostBlock="",hostError="";
    private readonly Dictionary<string,string> hostBlocks=new();
    private UniTask.Awaiter saving;
    private bool savePending,failed;
    private ChestLocalTransaction saveTx;
    private ChestClientJournal saveJournal;
    private readonly HashSet<uint> locks=new();
    private static int nativeScope;
    internal bool Open;
    internal bool Busy=>savePending || failed || journal?.Records.Any(t=>t.Stage is not ("Done" or "Cancelled"))==true;
    private bool CanOperate=>!identity.Busy && !savePending && !failed && journal!=null && !journal.Records.Any(t=>t.Stage is "Preparing" or "Applying" or "Waiting" or "Held");
    internal string Status="F2 - Shared Stash (stored on Host)";
    private Vector2 inventoryScroll,chestScroll;
    private ItemContainerEntry selected;
    private string dragged="";
    private readonly Dictionary<string,ItemDataUnpacked> previews=new();
    private readonly SharedChestSkin skin=new();
    private NativeSharedStashView nativeView;
    private readonly Dictionary<string,LoadRef<Sprite>> icons=new();
    private ItemContainerEntry[] qaInventory;
    internal LanSharedChest(LanGift gift){identity=gift;}
    private static string Root=>Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),"UserData","LastEpochLAN","SharedChest");
    private static string Hash(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static string Raw(ItemData data)=>Convert.ToBase64String(data.GetIDAsArray().ToArray());
    private static string Fingerprint(ItemData data)=>Convert.ToHexString(SHA256.HashData(data.GetIDAsArray().ToArray()));
    private static bool Eligible(ItemData data)=>LanGift.Eligible(data) && data.individualID>=ItemData.minIndividualID;
    internal void Toggle(){Open=!Open;selected=null;dragged="";nextPoll=0;if(!Open)ReleaseView();}
    internal void Show(){Open=true;nextPoll=0;}
    internal bool Protect(ItemData data)=>nativeScope==0 && locks.Count>0 && data!=null && locks.Contains(data.individualID);
    private static ItemContainersManager Manager=>PlayerFinder.getItemContainersManager();
    private bool Ready=>OfflineTools.OfflineToolkit.Ready && Manager?.IsFullyInitializedAndLoaded==true &&
        PlayerFinder.getPlayerActor()?.HasMovedToPlayerSpawn==true && !Il2CppLE.UI.LoadingScreen.IsVisible;
    internal bool NativeReady=>Ready && qaInventory==null;
    internal NativeSharedStashView NativeView=>nativeView;
    internal object Diagnostics
    {
        get
        {
            try { return new {Open,Status,characterReady=Ready,inventoryReady=Manager?.IsFullyInitializedAndLoaded==true,
                identityReady=identity.Identity!="" && identity.Character!="",journalReady=journal!=null,
                connected=session?.Connected==true,viewReceived=view!=null,nativeVisible=nativeView?.Visible==true,
                nativeError=nativeView?.Error??"",sourcePanel=nativeView?.SourcePanelKind??"",
                hostRequests,transferAttempts,lastHostBlock,savePending,failed,nativeInteractions=nativeView?.Interactions??0,
                transactionStages=journal?.Records.GroupBy(t=>t.Stage).ToDictionary(g=>g.Key,g=>g.Count()),
                localAccessBlock=AccessBlock(session?.PlayerPositions.FirstOrDefault(p=>p.Id==session.LocalId))}; }
            catch(Exception ex) { return new {Open,Status,diagnosticError=ex.GetType().Name}; }
        }
    }
    internal void NativeDepositCursor(ItemContainerEntry entry)=>Deposit(entry,true);
    internal void NativeDepositInventory(ItemContainerEntry entry)=>Deposit(entry);
    internal void NativeWithdraw(ChestItem item)=>Withdraw(item);
    private static int Count(ChestItem item)
    {
        var actor=PlayerFinder.getPlayerActor();var data=actor?.characterDataTracker?.charData;
        if(data==null || Manager==null)throw new InvalidOperationException("Character inventory unavailable");
        // Snapshot only. This call does not claim that the async disk save succeeded.
        data.SaveItems(Manager);int count=0;
        foreach(var saved in data.SavedItems)if(saved.Data!=null && new ItemDataUnpacked(saved.Data).individualID==item.NativeId)count+=saved.Quantity;
        return count;
    }
    private void Put(ChestLocalTransaction tx){journal.Put(tx);RefreshLocks();}
    private void RefreshLocks()
    {
        locks.Clear();if(journal==null)return;
        foreach(var tx in journal.Records.Where(t=>t.Stage is not ("Done" or "Cancelled")))if(tx.Item!=null)locks.Add(tx.Item.NativeId);
    }
    internal void Tick(LanSession room)
    {
        Active=this;session=room;
        if(Il2CppEHG.Multiplayer.GameplayEnvironment.IsOnlinePlay)return;
        try
        {
            CompleteSave();
            if(!Open)nativeView?.Dispose();
            if(!Ready || identity.Identity=="" || identity.Character=="")
            {
                if(Open && !Busy)Status=!Ready ? "Waiting for local character and inventory to finish loading" : "Waiting for local character identity";
                nativeView?.Dispose();return;
            }
            if(character!=identity.Character || group!=identity.Group)
            {
                if(savePending || failed)return;
                var nextJournal=new ChestClientJournal(Path.Combine(Root,"Client",identity.Identity,Hash(identity.Character)));
                foreach(var tx in nextJournal.Records.Where(t=>t.Stage is "Preparing" or "Applying").ToArray())
                    nextJournal.Put(tx with{Stage=ChestClientJournal.Recover(tx,Count(tx.Item!))});
                character=identity.Character;group=identity.Group;journal=nextJournal;hostStore=null;view=null;selected=null;previews.Clear();
                RefreshLocks();
                if(journal.Records.Any(t=>t.Stage=="Held"))Status="Unverified transaction held safely. Journal review required; no automatic refund.";
            }
            if(session?.Connected!=true){if(Open && !Busy)Status="Join a party before opening Shared Stash";view=null;nativeView?.Dispose();return;}
            if(room.IsHost)
            {
                hostStore??=new SharedChestStore(Path.Combine(Root,"Host",identity.Identity,Hash(group)+".json"),identity.Identity);
                for(int n=0;n<8 && room.TryTakeChestRequest(out var request);n++)Handle(request);
            }
            for(int n=0;n<8 && room.TryTakeChestReply(out var reply);n++)Receive(reply);
            if(Open && view==null && !Busy)
            {
                var host=room.PlayerPositions.FirstOrDefault();
                Status=hostError!="" ? "Host: "+hostError :
                    host==null || !(host.StashReady || host.Available) ? "Waiting for Host character to finish loading" :
                    host.GiftGroup!=identity.Group ? "Shared Stash requires the same cycle and character mode as Host" :
                    "Waiting for Host stash data...";
            }
            if(Open && Ready){nativeView??=new(this);nativeView.Tick(view);}
            if(Time.unscaledTime<nextPoll)return;
            nextPoll=Time.unscaledTime+1;
            if(Open || Busy)room.SendChest(new());
            if(savePending || failed)return;
            foreach(var tx in journal.Records.Where(t=>t.Stage is "Removed" or "Waiting" or "Added").Take(4))
            {
                if(view?.HostIdentity!=tx.Host || view.ChestId!=tx.ChestId){Status="Item held: reconnect to the original Host to finish";continue;}
                if(tx.Kind=="deposit")room.SendChest(new(){Kind="deposit",ChestId=tx.ChestId,Operation=tx.Id,Item=tx.Item,Ticket=tx.Ticket});
                else if(tx.Stage=="Added")room.SendChest(new(){Kind="ack",ChestId=tx.ChestId,Operation=tx.Id,ItemId=tx.ItemId});
                else room.SendChest(new(){Kind="withdraw",ChestId=tx.ChestId,Operation=tx.Id,ItemId=tx.ItemId});
            }
        }
        catch(Exception ex){Status="Stash transaction held: "+ex.Message;MelonLoader.MelonLogger.Warning(Status);}
    }
    private string AccessBlock(PlayerState player)
    {
        if(player==null)return "Waiting for party character state";
        if(!(player.StashReady || player.Available))return "Inventory not ready; finish loading and update all PCs";
        if(player.GiftGroup!=identity.Group)return "Shared Stash requires the same cycle and character mode";
        if(!Guid.TryParseExact(player.GiftIdentity,"N",out _) || player.CharacterId=="")return "Waiting for character identity; update all PCs";
        if(session.PlayerPositions.Count(p=>p.GiftIdentity==player.GiftIdentity && p.CharacterId==player.CharacterId)!=1)
            return "Duplicate character identity in party; transfer blocked";
        var data=PlayerFinder.getPlayerActor()?.characterDataTracker?.charData;
        if(data==null)return "Waiting for local character";
        if(data.SoloChallenge || data.SoloCharacterChallenge)return "Solo character mode cannot use Shared Stash";
        return "";
    }
    private bool BeginTransfer()
    {
        transferAttempts++;
        string reason=!Ready?"Waiting for local character and inventory":
            session?.Connected!=true?"Join a party before transferring":
            failed?"Character save failed; transfer held for review":
            savePending || identity.Busy?"Waiting for character save":
            journal==null?"Waiting for transaction journal":
            !CanOperate?"A transfer is pending or held; finish it before starting another":
            view==null?"Waiting for Host stash data":
            AccessBlock(session.PlayerPositions.FirstOrDefault(p=>p.Id==session.LocalId));
        if(reason=="")
        {
            var host=session.PlayerPositions.FirstOrDefault();
            reason=AccessBlock(host);
            if(reason=="" && view.HostIdentity!=host.GiftIdentity)reason="Waiting for current Host stash data";
        }
        if(reason=="")return true;
        Status=reason;return false;
    }
    private void Handle(ChestRequest request)
    {
        try
        {
            hostRequests++;
            var peer=session.PlayerPositions.SingleOrDefault(p=>p.Id==request.SenderId);
            string block=AccessBlock(peer);
            if(block!="")
            {
                lastHostBlock=block;
                if(!hostBlocks.TryGetValue(request.SenderId,out var previous) || previous!=block)
                    MelonLoader.MelonLogger.Warning("Shared Stash request blocked: "+block);
                if(hostBlocks.Count>=4)hostBlocks.Clear();
                hostBlocks[request.SenderId]=block;
                // Report the denial without exposing a different cycle's items.
                session.ReplyChest(request.SenderId,hostStore.View() with
                    {Operation=request.Command.Operation,Error=block,Items=Array.Empty<ChestItem>()});
                return;
            }
            hostBlocks.Remove(request.SenderId);
            if(request.Command.Kind=="deposit")
            {
                var data=new ItemDataUnpacked(Convert.FromBase64String(request.Command.Item.Data));
                if(!Eligible(data) || data.individualID!=request.Command.Item.NativeId || Raw(data)!=request.Command.Item.Data)throw new InvalidDataException("Unsupported item or non-canonical native item data");
            }
            var reply=hostStore.Apply(peer.GiftIdentity+":"+peer.CharacterId,request.Command);
            session.ReplyChest(request.SenderId,reply);
        }
        catch(Exception ex){Status=lastHostBlock="Host transaction held: "+ex.Message;MelonLoader.MelonLogger.Warning(Status);}
    }
    private void Receive(ChestReply reply)
    {
        var host=session.PlayerPositions.FirstOrDefault();
        if(host==null || reply.HostIdentity!=host.GiftIdentity)return;
        if(reply.Error!=""){hostError=reply.Error;Status="Host: "+hostError;return;}
        hostError="";
        if(view==null || view.ChestId!=reply.ChestId || reply.Revision>=view.Revision)view=reply;
        if(reply.Operation=="" || journal==null)return;
        var tx=journal.Find(reply.Operation);
        if(tx==null || tx.ChestId!=reply.ChestId || tx.Host!=reply.HostIdentity || tx.Stage is "Done" or "Cancelled")return;
        if(reply.Status=="stored" && tx.Kind=="deposit" && tx.Stage=="Removed")
        {Put(tx with{Stage="Done"});Status="Item stored on Host";return;}
        if(reply.Status=="done" && tx.Kind=="withdraw" && tx.Stage=="Added")
        {Put(tx with{Stage="Done",CompletedRevision=reply.Revision});Status="Item received and saved";return;}
        if(reply.Status=="unavailable" && tx.Kind=="withdraw" && tx.Stage=="Waiting" && tx.Item==null)
        {Put(tx with{Stage="Cancelled"});Status="Another player already took this item";return;}
        if(reply.Status=="full"){Status="Host stash/journal full; item held in escrow, no automatic refund";return;}
        if(reply.Status!="reserved" || tx.Kind!="withdraw" || tx.Stage!="Waiting" || savePending || identity.Busy || failed)return;
        if(reply.Grant==null || reply.Grant.Id!=tx.ItemId || tx.Item!=null && tx.Item!=reply.Grant)throw new InvalidDataException("Conflicting chest grant");
        var item=new ItemDataUnpacked(Convert.FromBase64String(reply.Grant.Data));
        if(!Eligible(item) || item.individualID!=reply.Grant.NativeId || Raw(item)!=reply.Grant.Data)throw new InvalidDataException("Invalid native chest item");
        if(tx.Item==null){tx=tx with{Item=reply.Grant,Before=Count(reply.Grant)};Put(tx);}
        if(Count(tx.Item)!=tx.Before || tx.Before!=0){Put(tx with{Stage="Held"});Status="Duplicate item detected in inventory; transaction held for review";return;}
        if(!Manager.inventory.CanReceiveItem(item,1)){Status="Inventory full - item reserved on Host; retrying";return;}
        tx=tx with{Stage="Applying"};Put(tx);
        nativeScope++;bool added;
        try{added=Manager.inventory.TryAddItem(item,1,Context.GIFTING);}finally{nativeScope--;}
        if(!added){Put(tx with{Stage="Waiting"});return;}
        BeginSave(tx);
    }
    private void BeginSave(ChestLocalTransaction tx)
    {
        var data=PlayerFinder.getPlayerActor().characterDataTracker.charData;
        if(data.Id!=character || data.DataStoreCache==null || data.UserIdentity==null)throw new InvalidOperationException("Native save service unavailable; transaction held");
        data.SaveItems(Manager);data.SetDirty(true);saveTx=tx;saveJournal=journal;
        savePending=true;failed=false;
        try{saving=data.DataStoreCache.SaveDirty(data.UserIdentity).GetAwaiter();}
        catch{failed=true;throw;}
        Status="Saving character - item temporarily locked";
    }
    private void CompleteSave()
    {
        if(!savePending || failed || !saving.IsCompleted)return;
        try
        {
            saving.GetResult();
            var done=saveTx with{Stage=saveTx.Kind=="deposit"?"Removed":"Added"};
            saveJournal.Put(done);savePending=false;saveTx=null;saveJournal=null;nextPoll=0;RefreshLocks();
        }
        catch{failed=true;Status="Character save failed: transaction held; restart the game to verify";throw;}
    }
    private void Deposit(ItemContainerEntry entry,bool cursor=false)
    {
        if(!BeginTransfer())return;
        if(entry?.quantity!=1 || !Eligible(entry.data))
        {Status="Only tradable equipment / Idols with a unique ID and quantity 1 can be stored";return;}
        var data=PlayerFinder.getPlayerActor().characterDataTracker.charData;
        if(data.DataStoreCache==null || data.UserIdentity==null){Status="Native character save service is not ready";return;}
        entry.data.RebuildID();string raw=Raw(entry.data);
        var item=new ChestItem(Guid.NewGuid().ToString("N"),raw,Fingerprint(entry.data),Name(entry.data),entry.data.individualID);int count=Count(item);
        if(count!=1){Status="Only eligible equipment/Idols with a unique ID and one character copy are accepted";return;}
        if(view.Items.Length>=96){Status="Stash full";return;}
        string id=item.Id;
        string ticket=journal.Records.Where(t=>t.Kind=="withdraw" && t.Stage=="Done" && t.ChestId==view.ChestId && t.Item?.NativeId==item.NativeId).OrderBy(t=>t.CompletedRevision).LastOrDefault()?.Id??"";
        var tx=new ChestLocalTransaction{Id=id,ChestId=view.ChestId,Host=view.HostIdentity,Kind="deposit",ItemId=id,Item=item,Stage="Preparing",Before=count,Ticket=ticket};
        Put(tx);nativeScope++;bool removed;
        try{removed=cursor?Manager.cursor.TryRemoveItem(entry,1,Context.GIFTING):Manager.inventory.TryRemoveItem(entry,1,Context.GIFTING);}finally{nativeScope--;}
        if(!removed){Put(tx with{Stage="Cancelled"});Status="Could not remove item from inventory";return;}
        selected=null;BeginSave(tx);
    }
    private void Withdraw(ChestItem item)
    {
        if(!BeginTransfer())return;
        var tx=new ChestLocalTransaction{Id=Guid.NewGuid().ToString("N"),ChestId=view.ChestId,Host=view.HostIdentity,Kind="withdraw",ItemId=item.Id,Stage="Waiting"};
        Put(tx);nextPoll=0;Status="Reserving item on Host";
    }
    internal ChestReply QaView=>view;
    internal int QaWithdrawRequests=>journal?.Records.Count(t=>t.Kind=="withdraw")??0;
    internal void QaPreview(LanSession room,ChestReply projection,ItemContainerEntry[] inventory)
    {
        if(!Environment.GetCommandLineArgs().Contains("--lelan-chest-ui-smoke"))throw new InvalidOperationException("Chest UI QA flag required");
        session=room;view=projection;qaInventory=inventory;Open=true;
    }
    internal void QaDeposit(ItemContainerEntry entry)
    {if(!Environment.GetCommandLineArgs().Contains("--lelan-shared-chest-smoke"))throw new InvalidOperationException("Private chest QA flag required");Deposit(entry);}
    internal void QaWithdraw(ChestItem item)
    {if(!Environment.GetCommandLineArgs().Contains("--lelan-shared-chest-smoke"))throw new InvalidOperationException("Private chest QA flag required");Withdraw(item);}
    private static string Name(ItemData item)
    {
        var value=item.TryCast<ItemDataUnpacked>()??new ItemDataUnpacked(item);
        return (value.FullName??"Item").Length>128?value.FullName.Substring(0,128):value.FullName??"Item";
    }
    private void ReleaseView(){nativeView?.Dispose();skin.Close();foreach(var asset in icons.Values)asset.Dispose();icons.Clear();previews.Clear();}
    internal void Disconnect(){session=null;view=null;Open=false;selected=null;dragged="";hostBlocks.Clear();hostError="";ReleaseView();}
    private void Icon(Rect rect,string key,ItemData item)
    {
        if(!icons.TryGetValue(key,out var asset))
        {
            if(icons.Count>=256)return;
            var soft=ItemData.GetItemSprite(item.itemType,item.subType,item.isUniqueSetOrLegendary(),item.uniqueID,ItemUIContext.Default);
            if(soft==null)return;
            icons[key]=asset=SoftRefExtensions.CreateLoadRef<Sprite>(soft,"LastEpochLAN.SharedChest",0);
        }
        var sprite=asset.AssetOrNull;
        if(sprite==null || sprite.texture==null)return;
        if(sprite.packed && sprite.packingMode==SpritePackingMode.Tight)return;
        var uv=sprite.textureRect;var texture=sprite.texture;
        GUI.DrawTextureWithTexCoords(rect,texture,new Rect(uv.x/texture.width,uv.y/texture.height,uv.width/texture.width,uv.height/texture.height));
    }
    internal void Draw()
    {
        if(!Open)return;
        if(qaInventory==null)
        {
            if(nativeView?.Visible!=true)
            {
                GUI.Box(new Rect(18,Screen.height-105,650,82),"Shared Stash - F2 to open/close");
                GUI.Label(new Rect(30,Screen.height-75,620,48),!string.IsNullOrWhiteSpace(nativeView?.Error)?nativeView.Error:Status);
            }
            return;
        }
        try{DrawInternal();}catch(Exception ex){Status="Stash: "+ex.Message;GUI.enabled=true;}
    }
    private void DrawInternal()
    {
        float width=Mathf.Min(Screen.width-24,1050),height=Mathf.Min(Screen.height-24,760);
        var area=new Rect((Screen.width-width)/2,(Screen.height-height)/2,width,height);
        skin.Draw(area);
        GUI.Box(area,"SHARED STASH - HOST | F2 to open/close");
        if(GUI.Button(new Rect(area.xMax-40,area.y+5,30,25),"X")){Toggle();return;}
        GUI.Label(new Rect(area.x+14,area.y+30,width-28,42),session?.Connected==true?"Works across maps. Select an item to deposit/withdraw, or drag between lists.":"Host or Join a party to use Shared Stash.");
        GUI.Label(new Rect(area.x+14,area.yMax-65,width-28,55),Status);
        if((!Ready && qaInventory==null) || session?.Connected!=true || view==null){GUI.Label(new Rect(area.x+16,area.y+82,width-32,50),"Waiting for character / Host data...");return;}
        float half=(width-42)/2;
        var left=new Rect(area.x+14,area.y+82,half,height-165);
        var right=new Rect(area.x+28+half,area.y+82,half,height-165);
        GUI.Box(left,"INVENTORY - equipment / Idols, one per item");GUI.Box(right,$"HOST STASH ({view.Items.Length}/96)");
        var items=qaInventory??Manager.inventory.GetContent().ToArray().Where(e=>e.quantity==1 && Eligible(e.data)).ToArray();
        inventoryScroll=GUI.BeginScrollView(new Rect(left.x+6,left.y+27,left.width-12,left.height-70),inventoryScroll,new Rect(0,0,left.width-32,items.Length*39));
        for(int i=0;i<items.Length;i++)
        {
            var entry=items[i];var row=new Rect(0,i*39,left.width-36,35);
            GUI.enabled=CanOperate;
            if(Event.current.rawType==EventType.MouseDown && row.Contains(Event.current.mousePosition) && CanOperate){selected=entry;dragged="deposit";}
            if(GUI.Button(row,Name(entry.data)))selected=entry;
            Icon(new Rect(2,i*39+2,30,30),Fingerprint(entry.data),entry.data);
        }
        GUI.enabled=true;GUI.EndScrollView();
        GUI.enabled=CanOperate && selected!=null;
        if(GUI.Button(new Rect(left.x+8,left.yMax-36,left.width-16,28),"Deposit selected item >"))Deposit(selected);
        GUI.enabled=true;
        chestScroll=GUI.BeginScrollView(new Rect(right.x+6,right.y+27,right.width-12,right.height-35),chestScroll,new Rect(0,0,right.width-32,Mathf.Ceil(view.Items.Length/4f)*87));
        for(int i=0;i<view.Items.Length;i++)
        {
            var item=view.Items[i];float cell=(right.width-38)/4;var rect=new Rect(i%4*cell,i/4*87,cell-4,82);
            GUI.enabled=CanOperate;
            if(Event.current.rawType==EventType.MouseDown && rect.Contains(Event.current.mousePosition) && CanOperate)dragged=item.Id;
            if(GUI.Button(rect,item.Name+"\n[Withdraw]"))Withdraw(item);
            if(!previews.TryGetValue(item.Id,out var preview))previews[item.Id]=preview=new ItemDataUnpacked(Convert.FromBase64String(item.Data));
            Icon(new Rect(rect.x+2,rect.y+2,26,26),item.Hash,preview);
        }
        GUI.enabled=true;GUI.EndScrollView();
        if(Event.current.rawType==EventType.MouseUp && dragged!="")
        {
            string action=dragged;dragged="";
            if(action=="deposit" && right.Contains(Event.current.mousePosition))Deposit(selected);
            else if(left.Contains(Event.current.mousePosition)){var item=view.Items.FirstOrDefault(i=>i.Id==action);if(item!=null)Withdraw(item);}
        }
        var cursorItems=qaInventory==null?Manager.cursor.GetContent():null;
        if(cursorItems?.Count==1 && Event.current.type==EventType.MouseUp && right.Contains(Event.current.mousePosition) && CanOperate)Deposit(cursorItems[0],true);
    }
}

// Generic native movement checks: no projected chest item is a native inventory
// entry. Only real sender/receiver items pending durable save are locked here.
[HarmonyPatch(typeof(ItemContainer),nameof(ItemContainer.CanRemoveItemPreCheck))]
internal static class SharedChestItemLockPatch
{
    private static bool Prefix(ItemContainerEntry item,ref bool __result)
    {if(item==null || LanSharedChest.Active?.Protect(item.data)!=true)return true;__result=false;return false;}
}

[HarmonyPatch(typeof(ItemContainer),nameof(ItemContainer.TryRemoveItem))]
internal static class SharedChestManagerLockPatch
{
    private static bool Prefix(ItemContainerEntry __0,ref bool __result)
    {if(__0==null || LanSharedChest.Active?.Protect(__0.data)!=true)return true;__result=false;return false;}
}

// Copy only the game's stash artwork into our renderer. No Stash controller,
// container reference, event handler, tab purchase or native save owner is cloned.
internal sealed class SharedChestSkin
{
    private Sprite frame;
    private GameObject inputRoot;
    private UnityEngine.UI.Image blocker;
    private float retry;
    internal void Close(){if(inputRoot!=null)UnityEngine.Object.Destroy(inputRoot);inputRoot=null;blocker=null;}
    internal void Draw(Rect area)
    {
        if(inputRoot==null)
        {
            inputRoot=new GameObject("LAN Shared Chest - isolated input",Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
            var canvas=inputRoot.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32000;
            inputRoot.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var panel=new GameObject("Chest mouse blocker",Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
            panel.transform.SetParent(inputRoot.transform,false);blocker=panel.AddComponent<UnityEngine.UI.Image>();
            blocker.color=new Color(0,0,0,0);blocker.raycastTarget=true;
        }
        var bounds=blocker.rectTransform;bounds.anchorMin=bounds.anchorMax=new Vector2(0,1);bounds.pivot=new Vector2(0,1);
        bounds.anchoredPosition=new Vector2(area.x,-area.y);bounds.sizeDelta=new Vector2(area.width,area.height);
        if(frame==null && Time.unscaledTime>=retry)
        {
            retry=Time.unscaledTime+3;
            var source=StashPanelUI.Instance;
            if(source!=null)
            {
                float size=0;
                foreach(var image in source.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                {
                    float current=image.rectTransform.rect.width*image.rectTransform.rect.height;
                    if(image.sprite!=null && current>size){size=current;frame=image.sprite;}
                }
            }
        }
        if(frame!=null && frame.texture!=null && (!frame.packed || frame.packingMode!=SpritePackingMode.Tight))
        {
            var rect=frame.textureRect;var texture=frame.texture;
            GUI.DrawTextureWithTexCoords(area,texture,new Rect(rect.x/texture.width,rect.y/texture.height,rect.width/texture.width,rect.height/texture.height));
        }
    }
}
