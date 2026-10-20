using LastEpochLAN.Core;
using Il2Cpp;
using UnityEngine;
using System.Security.Cryptography;
using System.Text.Json;

namespace LastEpochLAN.Mod;

// Layout-only fixture: no native inventory, actor, journal or character save.
// Actual deposit/withdraw native behavior is exercised by NativeSharedChestProbe.
internal sealed class NativeChestUiProbe
{
    private readonly LanSharedChest chest=new(new LanGift());
    private LanSession room;
    private float captureAt,finishAt;
    private bool captured,finished;
    internal void Draw()
    {
        if(finished)return;
        string root=Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),"UserData");
        try
        {
            if(room==null)
            {
                room=LanSession.Host("UI QA","private-ui","private-ui",0,System.Net.IPAddress.Loopback);
                string identity=Guid.NewGuid().ToString("N");room.Publish(new(){Available=true,GiftIdentity=identity,CharacterId="UI fixture"});
                var items=new List<ChestItem>();
                foreach(var unique in UniqueList.instance.uniques)
                {
                    if(unique==null || unique.isSetItem || unique.subTypes?.Count<=0)continue;
                    var item=ItemData.DummyData(unique.baseType,unique.subTypes[0],0);item.rarity=7;item.uniqueID=unique.uniqueID;item.RollIndividualID();item.SetAllImplicitRolls(37);item.SetAllUniqueRolls(73);item.RebuildID();
                    var unpacked=new ItemDataUnpacked(item.GetIDAsArray());
                    items.Add(new(Guid.NewGuid().ToString("N"),Convert.ToBase64String(item.GetIDAsArray().ToArray()),Convert.ToHexString(SHA256.HashData(item.GetIDAsArray().ToArray())),unpacked.FullName,item.individualID));
                    if(items.Count==8)break;
                }
                chest.QaPreview(room,new(){ChestId=Guid.NewGuid().ToString("N"),HostIdentity=identity,Items=items.ToArray()},Array.Empty<ItemContainerEntry>());
                captureAt=Time.unscaledTime+4;
            }
            chest.Draw();
            if(!captured && Time.unscaledTime>=captureAt && Event.current.type==EventType.Repaint)
            {ScreenCapture.CaptureScreenshot(Path.Combine(root,"shared-chest-ui-qa.png"));captured=true;finishAt=Time.unscaledTime+2;}
            if(captured && Time.unscaledTime>=finishAt && File.Exists(Path.Combine(root,"shared-chest-ui-qa.png")))
            {
                File.WriteAllText(Path.Combine(root,"shared-chest-ui-qa.json"),JsonSerializer.Serialize(new{passed=true,scope="Native item icons and independent Shared Chest UI layout only; fixture projection, not live inventory or drag/save acceptance"}));
                finished=true;chest.Disconnect();room.Dispose();
            }
        }
        catch(Exception ex)
        {
            File.WriteAllText(Path.Combine(root,"shared-chest-ui-qa.json"),JsonSerializer.Serialize(new{passed=false,error=ex.ToString()}));finished=true;chest.Disconnect();room?.Dispose();
        }
    }
}
