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
    private bool otherMapView;
    private string originalScene;
    private float queryAt;
    internal object Result {get;private set;}
    internal bool Finished=>Result!=null;
    internal NativeSharedChestProbe(){chest=new(identity);}
    private static ItemContainersManager Manager=>PlayerFinder.getItemContainersManager();
    private int Count()=>Manager.inventory.GetContent().ToArray().Count(e=>Convert.ToBase64String(e.data.GetIDAsArray().ToArray())==raw);
    internal void Draw()
    {
        if(Finished)return;chest.Draw();
        if(!screenshot && stage==3 && chest.QaView?.Items.Any(i=>i.Hash==key)==true && Event.current.type==EventType.Repaint)
        {ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),"UserData","shared-chest-qa.png"));screenshot=true;holdUntil=Time.unscaledTime+2;}
    }
    internal void Tick()
    {
        if(Finished)return;
        try
        {
            var actor=PlayerFinder.getPlayerActor();if(actor?.HasMovedToPlayerSpawn!=true || Manager?.IsFullyInitializedAndLoaded!=true)return;
            if(stage==0)
            {
                if(!Environment.GetCommandLineArgs().Contains("--lelan-shared-chest-smoke"))throw new InvalidOperationException("QA switch required");
                host=LanSession.Host("ChestNativeHost","private-chest","private-chest",0,System.Net.IPAddress.Loopback);
                joining=LanSession.JoinAsync("127.0.0.1",host.Port,"Other map client","private-chest","private-chest");
                deadline=Time.unscaledTime+90;stage=1;
                originalScene=SceneList.GetCurrentSceneDetails()?.Name??"";
                host.PublishEcho(new(){Revision=1,Active=true,RunId=Guid.NewGuid().ToString("N"),Scene="QA-DoNotEnter",HostScene="QA-DoNotEnter",Run=new byte[]{1},Metadata=new byte[]{1}});
            }
            if(Time.unscaledTime>deadline)throw new TimeoutException("Native chest stage "+stage+": "+chest.Status);
            identity.Tick(host);
            host.Publish(new(){Available=true,Scene=SceneList.GetCurrentSceneDetails()?.Name??"",GiftIdentity=identity.Identity,CharacterId=identity.Character,GiftGroup=identity.Group,Cooperative=true});
            if(guest==null){if(!joining.IsCompleted)return;guest=joining.GetAwaiter().GetResult();}
            guest.Publish(new(){Available=true,Scene="DifferentMap-DoNotTravel",GiftIdentity=peerIdentity,CharacterId="QA-peer",GiftGroup=identity.Group});
            partyOnly.Tick(guest);
            if(partyOnly.Busy || partyOnly.Borrowing || partyOnly.Entries!=0 || partyOnly.Error!="" || (SceneList.GetCurrentSceneDetails()?.Name??"")!=originalScene)
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
                    if(Environment.GetCommandLineArgs().Contains("--lelan-render-chest-qa") && (!screenshot || Time.unscaledTime<holdUntil))return;
                    if(Count()!=0)throw new InvalidOperationException("Item remained in native inventory after Host deposit");
                    chest.QaWithdraw(chest.QaView.Items.Single(i=>i.Hash==key));stage=4;return;
                case 4:
                    if(chest.Busy || Count()!=1 || chest.QaView.Items.Any(i=>i.Hash==key) || !otherMapView)return;
                    if(actor.GetActorSync()?.actorVisuals?.HasBeenInitialized!=true)throw new InvalidOperationException("Native player interrupted during chest transfer");
                    Result=new{passed=true,nativeRemovedThenSaved=true,hostCustodyBetweenSaves=true,nativeWithdrawAndSave=true,finalNativeCopies=Count(),serializedBytesPreserved=true,
                        partyOnlyGuestDifferentMap=true,nativeMonolithJoinNoTransition=true,remoteChestQueryDifferentMap=true,scope="Private native Host character deposit/save/Host ledger/withdraw/save over loopback room; no physical two-PC native inventories or crash/save-reload acceptance"};
                    return;
            }
        }
        catch(Exception ex){Result=new{passed=false,error=ex.ToString(),stage,status=chest.Status};}
    }
    public void Dispose(){chest.Disconnect();host?.Dispose();guest?.Dispose();}
}
