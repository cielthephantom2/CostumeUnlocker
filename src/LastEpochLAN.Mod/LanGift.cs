using HarmonyLib;
using LastEpochLAN.Core;
using Il2CppCysharp.Threading.Tasks;
using System.Security.Cryptography;
using System.Text.Json;
using UnityEngine;

namespace LastEpochLAN.Mod;

internal sealed class LanGift
{
    internal static LanGift Active;
    internal static int NativeScope;
    private GiftJournal journal;
    private string characterId = "";
    private LanSession session;
    private PlayerState target;
    private Il2Cpp.ItemGiftingPanelUI panel;
    private UniTask.Awaiter saving;
    private bool savePending;
    private bool saveCompleted, saveFailed;
    private GiftRecord saveRecord;
    private GiftJournal saveJournal;
    private float saveStarted, nextWork, nextUi;
    private string lockedFingerprint = "";
    private readonly Queue<GiftMessage> replies = new();
    internal string Identity { get; private set; } = "";
    internal string Character => characterId;
    internal string Group { get; private set; } = "";
    internal string Status = "Gift ready";
    internal string Error = "";
    internal long Sent, Received;
    internal bool NativePanel => target != null && session?.Connected == true && !Il2CppEHG.Multiplayer.GameplayEnvironment.IsOnlinePlay;
    internal bool Busy => savePending;
    internal PlayerState FindRecipient(Il2CppLE.Services.Models.UserIdentity identity)
    {
        if (identity == null || session?.Connected != true || Il2CppEHG.Multiplayer.GameplayEnvironment.IsOnlinePlay) return null;
        // Party display identities use the authenticated connection ID. Never
        // resolve a recipient by name: two players can have the same display name.
        return session.PlayerPositions.FirstOrDefault(p => p.Id != session.LocalId && p.Id == identity.MasterAccountID &&
            p.GiftIdentity != "" && p.CharacterId != "");
    }
    internal int Pending => journal?.Records.Count(r => r.State is "Preparing" or "Sending" or "Pending" or "Applying") ?? 0;
    private static string Bytes(Il2Cpp.ItemData data) => Convert.ToBase64String(data.GetIDAsArray().ToArray());
    private static string Fingerprint(Il2Cpp.ItemData data) => Convert.ToHexString(SHA256.HashData(data.GetIDAsArray().ToArray()));
    internal static bool Eligible(Il2Cpp.ItemData data) => data != null &&
        (Il2Cpp.ItemList.isEquipment(data.itemType) || Il2Cpp.ItemList.isIdol(data.itemType)) && !data.IsItemExcludedFromTradingAndGifting;
    internal void Tick(LanSession value)
    {
        Active = this; session = value;
        if (Il2CppEHG.Multiplayer.GameplayEnvironment.IsOnlinePlay) return;
        try
        {
            CompleteSave();
            var actor = Il2Cpp.PlayerFinder.getPlayerActor();
            var character = actor?.characterDataTracker?.charData;
            var manager = Il2Cpp.PlayerFinder.getItemContainersManager();
            if (character == null || !actor.HasMovedToPlayerSpawn || manager == null || !manager.IsFullyInitializedAndLoaded) return;
            EnsureIdentity();
            if (character.Id != characterId)
            {
                string nextCharacter = character.Id;
                Group = character.Cycle + ":" + character.Hardcore + ":" + character.SoloChallenge + ":" + character.SoloCharacterChallenge;
                journal = new GiftJournal(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath), "UserData", "LanGift"), Identity, nextCharacter);
                characterId = nextCharacter;
                target = null; panel = null;
                RecoverPreparing(manager);
            }
            for (int n = 0; n < 8 && session != null && session.TryTakeGift(out var message); n++) Receive(message);
            if (NativePanel && panel != null && Time.unscaledTime >= nextUi)
            {
                nextUi = Time.unscaledTime + 0.25f;
                panel.UpdateUI(CanConfirm(), false, false, false);
            }
            if (Time.unscaledTime < nextWork) return;
            nextWork = Time.unscaledTime + 2;
            if (session?.Connected == true)
            {
                for (int n = 0; n < 8 && replies.Count != 0; n++)
                { if (!session.PublishGift(replies.Peek())) break; replies.Dequeue(); }
                foreach (var record in journal.Records.Where(r => r.Outgoing && r.State == "Sending").Take(8)) session.PublishGift(record.Message);
            }
            if (!savePending)
            {
                var pending = journal.Records.FirstOrDefault(r => !r.Outgoing && r.State is "Pending" or "Applying");
                if (pending != null) ApplyIncoming(pending, manager);
            }
        }
        catch (Exception ex)
        {
            if (Error != ex.Message) MelonLoader.MelonLogger.Warning("Gift held: " + ex.Message);
            Error = ex.Message; Status = "Gift held: " + Error;
        }
    }
    private void EnsureIdentity()
    {
        if (Identity != "") return;
        string path = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath), "UserData", "LastEpochLAN.gift-identity.json");
        if (File.Exists(path))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.GetProperty("machine").GetString() == Environment.MachineName &&
                Guid.TryParseExact(document.RootElement.GetProperty("identity").GetString(), "N", out _))
                Identity = document.RootElement.GetProperty("identity").GetString();
        }
        if (Identity == "")
        {
            Identity = Guid.NewGuid().ToString("N");
            File.WriteAllText(path, JsonSerializer.Serialize(new { machine = Environment.MachineName, identity = Identity }));
        }
    }
    private bool Present(Il2Cpp.ItemContainersManager manager, GiftRecord record)
    {
        var character = Il2Cpp.PlayerFinder.getPlayerActor()?.characterDataTracker?.charData;
        character.SaveItems(manager);
        int count = 0;
        foreach (var item in character.SavedItems)
            if (item.Data != null && Convert.ToBase64String(item.Data.ToArray()) == record.Message.EncodedItem) count += item.Quantity;
        return count >= record.Message.Quantity;
    }
    private void RecoverPreparing(Il2Cpp.ItemContainersManager manager)
    {
        foreach (var record in journal.Records.Where(r => r.Outgoing && r.State == "Preparing").ToArray())
        {
            // No offer was sent before sender save completion. If the saved
            // character still owns it, cancellation is safe; otherwise escrow owns it.
            if (Present(manager, record)) journal.Put(record with { State = "Cancelled" });
            else Status = "Interrupted gift held in escrow — recovery required";
        }
    }
    private void BeginSave(GiftRecord record)
    {
        var character = Il2Cpp.PlayerFinder.getPlayerActor().characterDataTracker.charData;
        var manager = Il2Cpp.PlayerFinder.getItemContainersManager();
        if (character.DataStoreCache == null || character.UserIdentity == null) throw new InvalidOperationException("Native character save service unavailable.");
        character.SaveItems(manager); character.SetDirty(true);
        saveRecord = record; saveJournal = journal; saveStarted = Time.unscaledTime;
        saveCompleted = false; saveFailed = false;
        saving = character.DataStoreCache.SaveDirty(character.UserIdentity).GetAwaiter(); savePending = true;
    }
    private void CompleteSave()
    {
        if (!savePending || saveFailed) return;
        if (!saving.IsCompleted)
        {
            if (Time.unscaledTime - saveStarted > 30) Status = "Gift waiting for character save";
            return;
        }
        if (!saveCompleted)
        {
            try { saving.GetResult(); saveCompleted = true; }
            catch { saveFailed = true; throw; }
        }
        var done = saveRecord with { State = saveRecord.Outgoing ? "Sending" : "Delivered" };
        saveJournal.Put(done);
        if (done.Outgoing) { Sent++; Status = "Gift queued for recipient"; }
        else { Received++; Status = "Gift received — open Gifts in inventory"; replies.Enqueue(done.Message.Reply("stored")); }
        savePending = false; saveRecord = null; saveJournal = null; lockedFingerprint = "";
    }
    private void Receive(GiftMessage message)
    {
        if (message.ToIdentity != Identity || message.ToCharacter != characterId) return;
        var old = journal.Find(message.TransferId);
        if (message.Kind != "offer")
        {
            if (old == null || !old.Outgoing || old.State != "Sending" || old.Message.Reply(message.Kind) != message) return;
            // Rejections retain the source escrow. They are never auto-refunded.
            journal.Put(old with { State = message.Kind == "stored" ? "Sent" : "Sending", Error = message.Kind == "rejected" ? "Recipient rejected this item; escrow retained" : "" });
            Status = message.Kind == "stored" ? "Gift stored by recipient" : "Gift held in escrow";
            return;
        }
        if (old != null)
        {
            if (old.Outgoing || old.Message != message) throw new InvalidDataException("Conflicting incoming gift ID.");
            if (old.State == "Delivered") replies.Enqueue(message.Reply("stored"));
            else if (old.State == "Rejected") replies.Enqueue(message.Reply("rejected"));
            return;
        }
        // Own the pending payload durably before touching native inventory.
        journal.Put(new GiftRecord { Message = message, State = "Pending" });
        Status = "Incoming gift pending";
    }
    private void ApplyIncoming(GiftRecord record, Il2Cpp.ItemContainersManager manager)
    {
        if (record.State == "Applying" && Present(manager, record))
        {
            lockedFingerprint = record.Message.Fingerprint;
            BeginSave(record); return;
        }
        // Native tooltips/gameplay require the unpacked implicit/requirement cache.
        var data = new Il2Cpp.ItemDataUnpacked(Convert.FromBase64String(record.Message.EncodedItem));
        if (!Eligible(data) || Bytes(data) != record.Message.EncodedItem)
        {
            journal.Put(record with { State = "Rejected", Error = "Item excluded from LAN gifting" });
            replies.Enqueue(record.Message.Reply("rejected")); return;
        }
        var applying = record with { State = "Applying" };
        journal.Put(applying); lockedFingerprint = record.Message.Fingerprint;
        NativeScope++;
        bool added;
        try { added = manager.giftedItems.TryAddItem(data, record.Message.Quantity, Il2Cpp.Context.GIFTING); }
        finally { NativeScope--; }
        if (!added)
        {
            journal.Put(record with { State = "Pending", Error = "Gift inbox capacity or item requirements" });
            lockedFingerprint = ""; Status = "Gift pending — free space in Gifts / check requirements"; return;
        }
        BeginSave(applying);
    }
    internal bool Protect(Il2Cpp.ItemData data) => NativeScope == 0 && lockedFingerprint != "" && Fingerprint(data) == lockedFingerprint;
    internal void SelectRecipient(PlayerState player)
    {
        try
        {
            if (session?.Connected != true || player.GiftIdentity == "" || player.CharacterId == "" || player.GiftGroup != Group || !player.Available)
                throw new InvalidOperationException("Recipient not ready or different character mode.");
            var character = Il2Cpp.PlayerFinder.getPlayerActor()?.characterDataTracker?.charData;
            if (character?.SoloChallenge == true || character?.SoloCharacterChallenge == true) throw new InvalidOperationException("Solo characters cannot gift.");
            if (Busy) throw new InvalidOperationException("Wait for the current gift save.");
            target = player;
            var identity = new Il2CppLE.Services.Models.UserIdentity(player.Name, player.Id, player.Id, "LAN", "LAN", false);
            Il2Cpp.UIBase.instance.OpenItemGiftingPanel(identity);
        }
        catch (Exception ex) { Status = ex.Message; Error = ex.Message; }
    }
    internal void Opened(Il2Cpp.ItemGiftingPanelUI value)
    {
        panel = value;
        if (panel.giftingPopupNameText != null) panel.giftingPopupNameText.text = target.Name;
        panel.ClearSlottedResonance(true);
        panel.UpdateUI(CanConfirm(), false, false, false);
    }
    internal bool CanConfirm()
    {
        if (!NativePanel || Busy) return false;
        var players = session.PlayerPositions.Where(p => p.GiftIdentity == target.GiftIdentity && p.CharacterId == target.CharacterId).ToArray();
        if (players.Length != 1 || !players[0].Available || players[0].GiftGroup != Group) return false;
        var content = Il2Cpp.PlayerFinder.getItemContainersManager()?.itemToGift?.GetContent();
        return content != null && content.Count == 1 && Eligible(content[0].data) && content[0].quantity == 1;
    }
    internal bool SlottedItemRejected()
    {
        var content = Il2Cpp.PlayerFinder.getItemContainersManager()?.itemToGift?.GetContent();
        // Empty slot, pending save and unavailable recipient disable Confirm,
        // but do not mean the item itself is excluded from gifting.
        return content != null && content.Count > 0 &&
            (content.Count != 1 || !Eligible(content[0].data) || content[0].quantity != 1);
    }
    internal void Confirm()
    {
        try
        {
            if (!CanConfirm()) { Status = "Select one eligible equipment item and a connected recipient"; return; }
            var character = Il2Cpp.PlayerFinder.getPlayerActor().characterDataTracker.charData;
            if (character.DataStoreCache == null || character.UserIdentity == null) throw new InvalidOperationException("Native character save service unavailable.");
            var manager = Il2Cpp.PlayerFinder.getItemContainersManager();
            var item = manager.itemToGift.GetContent()[0];
            item.data.RebuildID();
            var message = new GiftMessage { TransferId = Guid.NewGuid().ToString("N"), FromIdentity = Identity, FromCharacter = characterId,
                ToIdentity = target.GiftIdentity, ToCharacter = target.CharacterId, EncodedItem = Bytes(item.data), Fingerprint = Fingerprint(item.data), Quantity = item.quantity };
            var preparing = new GiftRecord { Message = message, State = "Preparing", Outgoing = true };
            journal.Put(preparing);
            bool removed;
            NativeScope++;
            try { removed = manager.itemToGift.TryRemoveItem(item, item.quantity, Il2Cpp.Context.GIFTING); }
            finally { NativeScope--; }
            if (!removed) { journal.Put(preparing with { State = "Cancelled" }); throw new InvalidOperationException("Native gift slot removal failed."); }
            BeginSave(preparing);
            Status = "Saving outgoing gift";
        }
        catch (Exception ex) { Error = ex.Message; Status = "Gift held: " + Error; }
    }
    internal void Disconnect() { session = null; target = null; panel = null; replies.Clear(); }
}

[HarmonyPatch(typeof(Il2Cpp.ItemGiftingPanelUI), nameof(Il2Cpp.ItemGiftingPanelUI.OnPanelOpen))]
internal static class LanGiftOpenPatch
{
    private static bool Prefix(Il2Cpp.ItemGiftingPanelUI __instance)
    {
        if (LanGift.Active?.NativePanel != true) return true;
        LanGift.Active.Opened(__instance); return false;
    }
}
[HarmonyPatch(typeof(Il2Cpp.ItemGiftingPanelUI), nameof(Il2Cpp.ItemGiftingPanelUI.ConfirmGiftItemButtonPress))]
internal static class LanGiftConfirmPatch
{
    private static bool Prefix()
    {
        if (LanGift.Active?.NativePanel != true) return true;
        LanGift.Active.Confirm(); return false;
    }
}
[HarmonyPatch]
internal static class LanGiftValidatePatch
{
    private static System.Reflection.MethodBase TargetMethod() => AccessTools.Method(typeof(Il2Cpp.ItemContainersManager),
        nameof(Il2Cpp.ItemContainersManager.ValidateGiftItem), Enumerable.Repeat(typeof(bool).MakeByRefType(), 4).ToArray());
    private static bool Prefix(ref bool canGift, ref bool needsResonanceToGift, ref bool mismatchingResonance, ref bool itemExcludedFromGifting)
    {
        if (LanGift.Active?.NativePanel != true) return true;
        canGift = LanGift.Active.CanConfirm(); needsResonanceToGift = false; mismatchingResonance = false;
        itemExcludedFromGifting = LanGift.Active.SlottedItemRejected();
        return false;
    }
}
[HarmonyPatch(typeof(Il2Cpp.GiftedItemsContainer), nameof(Il2Cpp.GiftedItemsContainer.TryRemoveItem))]
internal static class LanGiftApplyingLockPatch
{
    private static bool Prefix(Il2Cpp.ItemContainerEntry entry, ref bool __result)
    {
        if (entry == null || LanGift.Active?.Protect(entry.data) != true) return true;
        __result = false; return false;
    }
}
