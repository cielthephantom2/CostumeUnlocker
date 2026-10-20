using LastEpochLAN.Core;
using Il2Cpp;
using Il2CppCysharp.Threading.Tasks;
using UnityEngine;
using System.Security.Cryptography;

namespace LastEpochLAN.Mod;

// Only run after NativeCampaignProbe verifies private platform save paths and
// creates a disposable character. No source/target from real saves is used.
internal sealed class NativeSharedChestProbe : IDisposable
{
    private readonly LanGift identity=new();
    private readonly LanSharedChest chest;
    private LanSession host,guest;
    private Task<LanSession> joining;
    private ItemDataUnpacked item;
    private string raw="",key="";
    private UniTask.Awaiter saving;
    private int stage;
    private readonly string peerIdentity=Guid.NewGuid().ToString("N");
    private float deadline;
    private bool screenshot;
    private float holdUntil;
    private readonly LanMonolithParty partyOnly=new();
    private LanSharedChest clientChest;
    private SharedChestStore clientViewHost;
    private readonly string clientViewHostId=Guid.NewGuid().ToString("N");
    private int hostInputRaycasters;
    private bool otherMapView;
    private string originalScene;
    private float queryAt;
    private IntPtr privateStash;
    private int privateStashCount;
    private bool nativeProjectionVerified;
    private string nativeStashPanel="";
    private bool legacySingletonMissing;
    private string nativeClickReceiver="";
    internal object Result {get;private set;}
    internal bool Finished=>Result!=null;
    internal NativeSharedChestProbe(){chest=new(identity);}
    private static ItemContainersManager Manager=>PlayerFinder.getItemContainersManager();
    private int Count()=>Manager.inventory.GetContent().ToArray().Count(e=>Convert.ToBase64String(e.data.GetIDAsArray().ToArray())==raw);
    internal void Draw()
    {
        if(Finished)return;chest.Draw();clientChest?.Draw();
        if(!screenshot && stage==3 && chest.QaView?.Items.Any(i=>i.Hash==key)==true && Event.current.type==EventType.Repaint)
        {ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),"UserData","shared-chest-qa.png"));screenshot=true;holdUntil=Time.unscaledTime+2;}
    }
    internal void Tick()
    {
        if(Finished)return;
        try
        {
            var actor=PlayerFinder.getPlayerActor();if(actor?.HasMovedToPlayerSpawn!=true || Manager?.IsFullyInitializedAndLoaded!=true)return;
            if(stage>=8){TickClientView(actor);return;}
            if(stage==0)
            {
                if(!Environment.GetCommandLineArgs().Contains("--lelan-shared-chest-smoke"))throw new InvalidOperationException("QA switch required");
                host=LanSession.Host("ChestNativeHost","private-chest","private-chest",0,System.Net.IPAddress.Loopback);
                joining=LanSession.JoinAsync("127.0.0.1",host.Port,"Other map client","private-chest","private-chest");
                deadline=Time.unscaledTime+90;stage=1;
                originalScene=SceneList.GetCurrentSceneDetails()?.Name??"";
                if(Environment.GetCommandLineArgs().Contains("--lelan-stash-v2"))StashPanelUI.Instance=null;
                privateStash=Manager.stash.Pointer;privateStashCount=Manager.stash.GetContent().Count;
                host.PublishEcho(new(){Revision=1,Active=true,RunId=Guid.NewGuid().ToString("N"),Scene="QA-DoNotEnter",HostScene="QA-DoNotEnter",Run=new byte[]{1},Metadata=new byte[]{1}});
            }
            if(Time.unscaledTime>deadline)throw new TimeoutException("Native chest stage "+stage+": "+chest.Status);
            identity.Tick(host);
            host.Publish(new(){Available=true,Scene=SceneList.GetCurrentSceneDetails()?.Name??"",GiftIdentity=identity.Identity,CharacterId=identity.Character,GiftGroup=identity.Group,Cooperative=true});
            if(guest==null){if(!joining.IsCompleted)return;guest=joining.GetAwaiter().GetResult();}
            guest.Publish(new(){Available=true,Scene="DifferentMap-DoNotTravel",GiftIdentity=peerIdentity,CharacterId="QA-peer",GiftGroup=identity.Group});
            partyOnly.Tick(guest);
            if(partyOnly.Busy || partyOnly.Borrowing || !partyOnly.GameplayReady || partyOnly.Entries!=0 || partyOnly.Error!="" || (SceneList.GetCurrentSceneDetails()?.Name??"")!=originalScene)
                throw new InvalidOperationException("Join-only Client attempted native Echo travel without Teleport");
            if(Time.unscaledTime>=queryAt){queryAt=Time.unscaledTime+1;guest.SendChest(new());}
            while(guest.TryTakeChestReply(out var response))if(response.Valid() && response.HostIdentity==identity.Identity)otherMapView=true;
            chest.Tick(host);
            switch(stage)
            {
                case 1:
                    chest.Show();
                    if(chest.QaView==null || host.PlayerPositions.Count<2)return;
                    UniqueList.Entry entry=null;
                    foreach(var candidate in UniqueList.instance.uniques)
                    {
                        if(candidate==null || candidate.isSetItem || candidate.subTypes?.Count<=0)continue;
                        var source=ItemData.DummyData(candidate.baseType,candidate.subTypes[0],0);
                        source.rarity=7;source.uniqueID=candidate.uniqueID;source.legendaryPotential=2;source.RollIndividualID();source.SetAllImplicitRolls(37);source.SetAllUniqueRolls(73);source.RebuildID();
                        item=new ItemDataUnpacked(source.GetIDAsArray());
                        if(LanGift.Eligible(item)){entry=candidate;break;}
                    }
                    if(entry==null || !Manager.inventory.TryAddItem(item,1,Context.GIFTING))throw new InvalidOperationException("Cannot create isolated native chest QA item");
                    raw=Convert.ToBase64String(item.GetIDAsArray().ToArray());key=Convert.ToHexString(SHA256.HashData(item.GetIDAsArray().ToArray()));
                    var data=actor.characterDataTracker.charData;data.SaveItems(Manager);data.SetDirty(true);saving=data.DataStoreCache.SaveDirty(data.UserIdentity).GetAwaiter();stage=2;return;
                case 2:
                    if(!saving.IsCompleted)return;saving.GetResult();
                    if(Count()!=1)throw new InvalidOperationException("Native QA baseline has duplicate/missing item");
                    var entryToSend=Manager.inventory.GetContent().ToArray().Single(e=>Convert.ToBase64String(e.data.GetIDAsArray().ToArray())==raw);
                    chest.QaDeposit(entryToSend);if(Count()!=0 || !chest.Busy)throw new InvalidOperationException("Native deposit failed to remove source and enter save escrow");
                    stage=3;return;
                case 3:
                    if(chest.Busy || chest.QaView?.Items.Any(i=>i.Hash==key)!=true)return;
                    var nativeView=chest.NativeView;
                    if(nativeView?.Visible!=true || nativeView.NativeItemUis<1)return;
                    nativeStashPanel=nativeView.SourcePanelKind;legacySingletonMissing=nativeView.LegacySingletonMissing;
                    if(Environment.GetCommandLineArgs().Contains("--lelan-stash-v2") && nativeStashPanel!="StashPanelV2")throw new InvalidOperationException("QA did not clone requested Stash V2");
                    if(Environment.GetCommandLineArgs().Contains("--lelan-stash-v1") && nativeStashPanel!="StashPanel")throw new InvalidOperationException("QA did not clone requested legacy Stash");
                    if(nativeView.BoundToPrivateStash || Manager.stash.Pointer!=privateStash || Manager.stash.GetContent().Count!=privateStashCount)
                        throw new InvalidOperationException("Native shared projection touched personal stash");
                    var projected=nativeView.Container.GetContent();
                    var projectedItem=projected.ToArray().SingleOrDefault(e=>e.data.individualID==item.individualID);
                    if(projectedItem==null)throw new InvalidOperationException("Native item UI bound to incorrect item");
                    int projectionCount=projected.Count;
                    var projection=nativeView.Container.Cast<ItemContainer>();
                    projection.TryRemoveItem(projectedItem,1,Context.GIFTING);
                    projection.TryAddItem(item,1,Context.GIFTING);
                    projection.MoveItemTo(projectedItem,Manager.inventory.Cast<IItemContainer>(),new Il2CppSystem.Nullable<Vector2Int>(new Vector2Int(0,0)),Context.GIFTING);
                    if(projection.GetContent().Count!=projectionCount || Count()!=0)throw new InvalidOperationException("Native projection mutation was not blocked");
                    var characterData=actor.characterDataTracker.charData;characterData.SaveItems(Manager);
                    if(characterData.SavedItems.ToArray().Any(s=>s.Data!=null && new ItemDataUnpacked(s.Data).individualID==item.individualID))
                        throw new InvalidOperationException("Projection leaked into native character save");
                    nativeProjectionVerified=true;
                    if(Environment.GetCommandLineArgs().Contains("--lelan-render-chest-qa"))
                    {
                        if(!screenshot){ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),"UserData","shared-chest-qa.png"));screenshot=true;holdUntil=Time.unscaledTime+3;}
                        if(Time.unscaledTime<holdUntil)return;
                    }
                    if(Count()!=0)throw new InvalidOperationException("Item remained in native inventory after Host deposit");
                    int beforeRequests=chest.QaWithdrawRequests;
                    nativeClickReceiver=chest.NativeView.QaClickItem(item.individualID);
                    chest.NativeView.QaClickItem(item.individualID);
                    if(!chest.Busy || chest.QaWithdrawRequests!=beforeRequests+1)throw new InvalidOperationException("Native double click did not queue exactly one withdrawal: "+chest.Status);
                    stage=4;return;
                case 4:
                    if(chest.Busy || Count()!=1 || chest.QaView.Items.Any(i=>i.Hash==key) || !otherMapView)return;
                    var cursorSource=Manager.inventory.GetContent().ToArray().Single(e=>e.data.individualID==item.individualID);
                    Manager.inventory.MoveItemTo(cursorSource,Manager.cursor.Cast<IItemContainer>(),new Il2CppSystem.Nullable<Vector2Int>(new Vector2Int(0,0)),Context.GIFTING);
                    if(Manager.cursor.GetContent().Count!=1)throw new InvalidOperationException("Native item could not move to cursor for deposit QA");
                    chest.NativeView.QaDropCursor();
                    if(!chest.Busy || Manager.cursor.GetContent().Count!=0)throw new InvalidOperationException("Native cursor deposit did not enter escrow");
                    stage=6;return;
                case 6:
                    if(chest.Busy || chest.QaView.Items.Any(i=>i.Hash==key)!=true)return;
                    chest.QaWithdraw(chest.QaView.Items.Single(i=>i.Hash==key));stage=7;return;
                case 7:
                    if(chest.Busy || Count()!=1 || chest.QaView.Items.Any(i=>i.Hash==key))return;
                    if(actor.GetActorSync()?.actorVisuals?.HasBeenInitialized!=true)throw new InvalidOperationException("Native player interrupted during chest transfer");
                    hostInputRaycasters=chest.NativeView.InputRaycasterCount;
                    chest.Disconnect();
                    // Reuse only the disposable QA character as a Client. The
                    // Host below is a separate managed ledger fixture, so native
                    // identity is unique and no second real save is impersonated.
                    clientChest=new(identity);
                    clientViewHost=new SharedChestStore(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),"UserData","SharedChestClientViewQA","host.json"),clientViewHostId);
                    clientChest.Toggle();deadline=Time.unscaledTime+60;stage=8;
                    return;
            }
        }
        catch(Exception ex){Result=new{passed=false,error=ex.ToString(),stage,status=chest.Status};}
    }
    private void TickClientView(Actor actor)
    {
        if(Time.unscaledTime>deadline)throw new TimeoutException("Client native stash view: "+clientChest.Status);
        host.Publish(new(){Available=true,Cooperative=true,Scene="QA-DoNotEnter",GiftIdentity=clientViewHostId,CharacterId="QA-view-host",GiftGroup=identity.Group});
        partyOnly.Tick(guest);
        if(!partyOnly.GameplayReady || partyOnly.SharedWorldReady || partyOnly.Busy || partyOnly.Borrowing)throw new InvalidOperationException("Party-only Client readiness or Host world isolation failed");
        guest.Publish(new(){Available=partyOnly.GameplayReady,Scene=originalScene,GiftIdentity=identity.Identity,CharacterId=identity.Character,GiftGroup=identity.Group});
        while(host.TryTakeChestRequest(out var request))
            if(request.SenderId==guest.LocalId)host.ReplyChest(request.SenderId,clientViewHost.Apply(request.SenderId,request.Command));
        clientChest.Tick(guest);
        if(clientChest.QaView?.HostIdentity!=clientViewHostId || clientChest.NativeView?.Visible!=true)return;
        if((SceneList.GetCurrentSceneDetails()?.Name??"")!=originalScene || partyOnly.Entries!=0 ||
            Manager.stash.Pointer!=privateStash || Manager.stash.GetContent().Count!=privateStashCount || Count()!=1)
            throw new InvalidOperationException("Client stash view changed native scene, private stash or inventory custody");
        Result=new{passed=true,nativeRemovedThenSaved=true,hostCustodyBetweenSaves=true,nativeWithdrawAndSave=true,finalNativeCopies=Count(),serializedBytesPreserved=true,
            partyOnlyGuestDifferentMap=true,nativeMonolithJoinNoTransition=true,remoteChestQueryDifferentMap=true,nativeProjectionVerified,nativeCursorDepositAndRedeposit=true,nativeStashPanel,legacySingletonMissing,nativeClickReceiver,nativeRaycastWithdrawal=true,
            inputRaycasters=hostInputRaycasters,nativeDoubleClickSingleRequest=true,clientPartyOnlyGameplayReady=true,
            nativeClientF2VisibleWithHostEcho=true,nativeClientDifferentMap=true,clientHostWorldIsolated=true,clientSourcePanel=clientChest.NativeView.SourcePanelKind,
            scope="Private native Host transfer/click/save regression, then native party-only Client view against a separate loopback managed Host ledger while Host Echo is active; not physical two-PC inventories or full visual acceptance"};
    }
    public void Dispose(){clientChest?.Disconnect();chest.Disconnect();host?.Dispose();guest?.Dispose();}
}
