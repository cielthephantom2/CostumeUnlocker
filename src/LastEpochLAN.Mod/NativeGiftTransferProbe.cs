using LastEpochLAN.Core;
using System.Security.Cryptography;
using System.Text.Json;
using UnityEngine;
using Il2CppCysharp.Threading.Tasks;

namespace LastEpochLAN.Mod;

internal sealed class NativeGiftTransferProbe : IDisposable
{
    private readonly LanGift adapter = new();
    private readonly NativePartyReplica nativeParty = new();
    private bool menuTest, nativeGiftClicked;
    private long noticesBefore;
    private bool emptySlotNoNotice;
    private bool retiredTargetSafe;
    private int deliveredCopies;
    private LanSession host, guest;
    private Task<LanSession> joining;
    private string peerIdentity = Guid.NewGuid().ToString("N");
    private GiftMessage incoming, outgoing;
    private bool stored, sourceReceipt;
    private bool replay;
    private Il2Cpp.ItemData sourceData;
    private UniTask.Awaiter fixtureSave;
    private int stage;
    private float deadline;
    private string Manifest => Path.Combine(Path.GetDirectoryName(Environment.ProcessPath), "UserData", "LastEpochLAN.gift-transfer-manifest.json");
    internal object Result { get; private set; }
    internal bool Finished => Result != null;
    internal void Tick()
    {
        if (Finished) return;
        try
        {
            var actor = Il2Cpp.PlayerFinder.getPlayerActor();
            if (actor == null || !actor.HasMovedToPlayerSpawn) return;
            var manager = Il2Cpp.PlayerFinder.getItemContainersManager();
            if (manager?.giftedItems == null || !manager.IsFullyInitializedAndLoaded) return;
            string scene = Il2Cpp.SceneList.GetCurrentSceneDetails()?.Name ?? "";
            if (host == null)
            {
                menuTest = Environment.GetCommandLineArgs().Contains("--lelan-gift-menu");
                replay = Environment.GetCommandLineArgs().Contains("--lelan-gift-transfer-replay");
                if (replay)
                {
                    incoming = JsonSerializer.Deserialize<GiftMessage>(File.ReadAllText(Manifest));
                    peerIdentity = incoming.FromIdentity;
                }
                host = LanSession.Host("GiftQAHost", "GiftQA", "GiftQAOnly", 39031, System.Net.IPAddress.Loopback);
                joining = LanSession.JoinAsync("127.0.0.1", 39031, "GiftQAPeer", "GiftQA", "GiftQAOnly");
                deadline = Time.unscaledTime + 45;
                MelonLoader.MelonLogger.Msg("Gift transfer QA started."); return;
            }
            if (guest == null) { if (!joining.IsCompleted) return; guest = joining.GetAwaiter().GetResult(); }
            adapter.Tick(host);
            var p = actor.position();
            var local = new PlayerState { Scene = scene, Available = true, X = p.x, Y = p.y, Z = p.z,
                GiftIdentity = adapter.Identity, CharacterId = adapter.Character, GiftGroup = adapter.Group };
            host.Publish(local);
            guest.Publish(local with { GiftIdentity = peerIdentity, CharacterId = "software-peer", MaxHealth = stage >= 12 ? 100 : 0, Health = stage >= 12 ? 100 : 0 });
            while (guest.TryTakeGift(out var message))
            {
                if (message.Kind == "offer") { outgoing = message; guest.PublishGift(message.Reply("stored")); }
                if (incoming != null && message.Kind == "stored" && message.TransferId == incoming.TransferId) stored = true;
            }
            if (adapter.Error != "") throw new InvalidOperationException(adapter.Error);
            if (Time.unscaledTime > deadline) throw new TimeoutException("Gift transfer QA stage " + stage + ": " + adapter.Status);
            switch (stage)
            {
                case 0:
                    if (!host.PlayerPositions.Any(v => v.Id == guest.LocalId && v.GiftIdentity == peerIdentity)) return;
                    if (replay) { stage = 4; break; }
                    noticesBefore = NativeGiftNoticeAudit.Calls;
                    if (menuTest)
                    {
                        nativeParty.Tick(host);
                        var members = Resources.FindObjectsOfTypeAll<Il2CppLE.UI.Party.UnityUI.PartyOverviewMemberPanel>();
                        var member = members.FirstOrDefault(m => m != null && m.IsActive && m.partyMember?.Identity?.MasterAccountID == guest.LocalId);
                        if (member == null) throw new InvalidOperationException("QA native party member not bound.");
                        var opener = member.GetComponent<Il2Cpp.SocialRightClickFunctions>() ?? member.GetComponentsInChildren<Il2Cpp.SocialRightClickFunctions>(true).FirstOrDefault();
                        if (opener == null) throw new InvalidOperationException("Native party right-click component missing.");
                        opener.TestRightClick(); stage = 11; break;
                    }
                    adapter.SelectRecipient(host.PlayerPositions.Single(v => v.Id == guest.LocalId));
                    stage = 1; break;
                case 11:
                    var menu = Il2Cpp.SocialManager.instance?.RightClickMenuInstance;
                    if (menu == null || !menu.IsOpen()) return;
                    var giftButton = menu.buttonHolder.GetComponentsInChildren<Il2Cpp.RightClickBtnRef>(true)
                        .SingleOrDefault(b => b.buttonText?.text == "Gift Item");
                    if (giftButton?.btnFunction == null || !giftButton.btnFunction.interactable) throw new InvalidOperationException("Native Gift menu button absent/disabled.");
                    giftButton.btnFunction.onClick.Invoke(); nativeGiftClicked = true; stage = 1; break;
                case 1:
                    emptySlotNoNotice = NativeGiftNoticeAudit.Calls == noticesBefore && !adapter.SlottedItemRejected();
                    if (!emptySlotNoNotice) throw new InvalidOperationException("Opening an empty LAN Gift slot emitted an item rejection notice.");
                    if (!Il2Cpp.UIBase.instance.IsItemGiftingPanelOpen()) return;
                    sourceData = Il2Cpp.ItemData.DummyData(9, 0, 0); sourceData.RollIndividualID(); sourceData.RebuildID();
                    if (!manager.inventory.TryAddItem(sourceData, 1, Il2Cpp.Context.GIFTING)) throw new InvalidOperationException("Native inventory rejected QA source item.");
                    var character = actor.characterDataTracker.charData;
                    character.SaveItems(manager); character.SetDirty(true);
                    fixtureSave = character.DataStoreCache.SaveDirty(character.UserIdentity).GetAwaiter(); stage = 10; break;
                case 10:
                    if (!fixtureSave.IsCompleted) return;
                    fixtureSave.GetResult();
                    Il2Cpp.ItemContainerEntry sourceEntry = null;
                    foreach (var entry in manager.inventory.GetContent()) if (entry.data.individualID == sourceData.individualID) sourceEntry = entry;
                    if (sourceEntry == null || !manager.inventory.MoveItemTo(sourceEntry, manager.itemToGift.Cast<Il2Cpp.IItemContainer>(),
                        new Il2CppSystem.Nullable<Vector2Int>(), Il2Cpp.Context.DEFAULT)) throw new InvalidOperationException("Native inventory-to-Gift move failed.");
                    if (!adapter.CanConfirm()) throw new InvalidOperationException("Native LAN Gift UI could not confirm staged equipment.");
                    if (adapter.SlottedItemRejected() || NativeGiftNoticeAudit.Calls != noticesBefore) throw new InvalidOperationException("Eligible LAN Gift item emitted an item rejection notice.");
                    adapter.Confirm(); stage = 2; break;
                case 2:
                    if (outgoing == null || adapter.Sent == 0) return;
                    if (manager.itemToGift.GetContent().Count != 0) throw new InvalidOperationException("Sender still owns staged gift.");
                    int sourceCopies = 0;
                    foreach (var item in actor.characterDataTracker.charData.SavedItems)
                        if (item.Data != null && Convert.ToBase64String(item.Data.ToArray()) == outgoing.EncodedItem) sourceCopies += item.Quantity;
                    if (sourceCopies != 0) throw new InvalidOperationException("Sender character save retained gifted item.");
                    sourceReceipt = true; stage = 3; break;
                case 3:
                    if (adapter.Status != "Gift stored by recipient") return;
                    var receiveData = Il2Cpp.ItemData.DummyData(21, 0, 0); receiveData.RollIndividualID(); receiveData.RebuildID();
                    var raw = receiveData.GetIDAsArray().ToArray();
                    incoming = new GiftMessage { TransferId = Guid.NewGuid().ToString("N"), FromIdentity = peerIdentity,
                        FromCharacter = "software-peer", ToIdentity = adapter.Identity, ToCharacter = adapter.Character,
                        EncodedItem = Convert.ToBase64String(raw), Fingerprint = Convert.ToHexString(SHA256.HashData(raw)) };
                    stage = 4; break;
                case 4:
                    if (incoming.ToCharacter != adapter.Character || incoming.ToIdentity != adapter.Identity) throw new InvalidOperationException("Replay recipient identity changed.");
                    for (int n = 0; n < 5; n++) if (!guest.PublishGift(incoming)) throw new InvalidOperationException("QA offer queue rejected.");
                    stage = 5; break;
                case 5:
                    if (!stored || adapter.Busy) return;
                    int copies = 0;
                    foreach (var item in manager.giftedItems.GetContent())
                        if (Convert.ToBase64String(item.data.GetIDAsArray().ToArray()) == incoming.EncodedItem) copies += item.quantity;
                    File.WriteAllText(Manifest, JsonSerializer.Serialize(incoming));
                    if (!replay && outgoing != null) File.WriteAllText(Manifest + ".source.json", JsonSerializer.Serialize(outgoing));
                    if (replay)
                    {
                        var sent = JsonSerializer.Deserialize<GiftMessage>(File.ReadAllText(Manifest + ".source.json"));
                        int oldSourceCopies = 0;
                        foreach (var item in actor.characterDataTracker.charData.SavedItems)
                            if (item.Data != null && Convert.ToBase64String(item.Data.ToArray()) == sent.EncodedItem) oldSourceCopies += item.Quantity;
                        if (oldSourceCopies != 0) throw new InvalidOperationException("Gifted source item returned after restart.");
                    }
                    deliveredCopies = copies;
                    guest.Publish(local with { GiftIdentity = peerIdentity, CharacterId = "software-peer", MaxHealth = 100, Health = 100 });
                    stage = 12; break;
                case 12:
                    if (!host.PlayerPositions.Any(p => p.Id == guest.LocalId && p.MaxHealth == 100)) return;
                    var combat = new NativeGuestCombat();
                    try
                    {
                        combat.Tick(host, scene);
                        var target = combat.FindTarget(guest.LocalId);
                        if (target == null) throw new InvalidOperationException("Native cached-target fixture was not created: " + combat.Error);
                        var before = target.position();
                        combat.Reset();
                        retiredTargetSafe = target.isInvalidTarget(Il2Cpp.LifeState.Alive, false, false) && Vector3.Distance(target.position(), before) < 0.01f;
                        if (!retiredTargetSafe) throw new InvalidOperationException("Retired native target did not remain invalid and position-safe after hooks were removed.");
                    }
                    finally { combat.Reset(); }
                    Result = new { passed = deliveredCopies == 1 && (replay || sourceReceipt) && retiredTargetSafe, phase = replay ? "transfer-replay" : "transfer", copies = deliveredCopies,
                        sourceReceipt, nativePanelOpened = replay ? (bool?)null : true, adapter.Sent, adapter.Received,
                        retiredTargetSafe,
                        nativeGiftClicked, emptySlotNoNotice, falseItemNotices = NativeGiftNoticeAudit.Calls - noticesBefore, NativeLanGiftMenuPatch.Added, NativeLanGiftMenuPatch.Error,
                        scope = "real native sender UI/slot/save and receiver inbox/save with software TCP peer; second physical native inventory not exercised" };
                    Dispose(); break;
            }
        }
        catch (Exception ex) { Result = new { passed = false, stage, error = ex.ToString(), adapter.Status }; Dispose(); }
    }
    public void Dispose() { nativeParty.Reset(); adapter.Disconnect(); guest?.Dispose(); host?.Dispose(); }
}

// Observe the actual native toast call during private Gift QA; never suppress it.
[HarmonyLib.HarmonyPatch(typeof(Il2Cpp.Notifications), nameof(Il2Cpp.Notifications.ItemUngiftable))]
internal static class NativeGiftNoticeAudit
{
    internal static long Calls;
    private static void Prefix()
    {
        if (Environment.GetCommandLineArgs().Contains("--lelan-gift-smoke") && LanGift.Active?.NativePanel == true) Calls++;
    }
}
