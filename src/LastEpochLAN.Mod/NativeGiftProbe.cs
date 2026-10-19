using Il2CppCysharp.Threading.Tasks;
using Il2CppLE.Services;
using UnityEngine;
using System.Text.Json;

namespace LastEpochLAN.Mod;

// Only runs after the asset probe verifies the private QA save profile.
internal sealed class NativeGiftProbe
{
    private int stage;
    private float deadline;
    private UniTask.Awaiter saving;
    private string encoded;
    private bool inserted;
    private NativeGiftTransferProbe transfer;
    private NativePartyTravelProbe partyTravel;

    private NativeHudPolicyProbe hudPolicy;
    internal object Result { get; private set; }
    internal bool Finished => Result != null;
    private string Manifest => Path.Combine(Path.GetDirectoryName(Environment.ProcessPath), "UserData", "LastEpochLAN.gift-qa-manifest.json");
    internal void Tick()
    {
        if (Finished) return;
        if (Environment.GetCommandLineArgs().Contains("--lelan-hud-policy"))
        {
            hudPolicy ??= new NativeHudPolicyProbe(); hudPolicy.Tick();
            if (hudPolicy.Finished) Result = hudPolicy.Result;
            return;
        }
        if (Environment.GetCommandLineArgs().Contains("--lelan-party-boss"))
        {
            Result=new {passed=false,error="Legacy generic interaction probe removed; encounter rewrite is not released."};
            return;
        }
        if (Environment.GetCommandLineArgs().Contains("--lelan-party-travel"))
        {
            partyTravel ??= new NativePartyTravelProbe(); partyTravel.Tick();
            if (partyTravel.Finished) Result = partyTravel.Result;
            return;
        }
        if (Environment.GetCommandLineArgs().Contains("--lelan-gift-transfer"))
        {
            transfer ??= new NativeGiftTransferProbe(); transfer.Tick();
            if (transfer.Finished) Result = transfer.Result;
            return;
        }
        try
        {
            var actor = Il2Cpp.PlayerFinder.getPlayerActor();
            var manager = Il2Cpp.PlayerFinder.getItemContainersManager();
            var character = actor?.characterDataTracker?.charData;
            if (actor == null || !actor.HasMovedToPlayerSpawn || character == null || manager?.giftedItems == null || !manager.IsFullyInitializedAndLoaded) return;
            if (stage == 0)
            {
                deadline = Time.unscaledTime + 30;
                bool reload = Environment.GetCommandLineArgs().Contains("--lelan-gift-reload");
                if (reload)
                {
                    using var document = JsonDocument.Parse(File.ReadAllText(Manifest));
                    if (character.Id != document.RootElement.GetProperty("characterId").GetString()) throw new InvalidOperationException("Gift QA loaded wrong character.");
                    encoded = document.RootElement.GetProperty("encoded").GetString();
                    int amount = 0;
                    foreach (var item in manager.giftedItems.GetContent())
                        if (Convert.ToBase64String(item.data.GetIDAsArray().ToArray()) == encoded) amount += item.quantity;
                    Result = new { passed = amount == 1, phase = "reload", amount, characterId = character.Id,
                        scope = "native gift container on the same private character after a separate process restart" };
                    return;
                }
                var data = Il2Cpp.ItemData.DummyData(9, 0, 0); data.RollIndividualID(); data.RebuildID();
                encoded = Convert.ToBase64String(data.GetIDAsArray().ToArray());
                var copy = new Il2Cpp.ItemData(Convert.FromBase64String(encoded));
                if (Convert.ToBase64String(copy.GetIDAsArray().ToArray()) != encoded) throw new InvalidDataException("Gift item round trip mismatch.");
                inserted = manager.giftedItems.TryAddItem(copy, 1, Il2Cpp.Context.GIFTING);
                if (!inserted) throw new InvalidOperationException("Native giftedItems rejected Context.GIFTING offline.");
                character.SaveItems(manager); character.SetDirty(true);
                int serialized = 0;
                foreach (var item in character.SavedItems)
                    if (item.ContainerID == (ushort)Il2Cpp.ContainerID.GIFTED_ITEMS_TAB && item.Data != null &&
                        Convert.ToBase64String(item.Data.ToArray()) == encoded) serialized += item.Quantity;
                if (serialized != 1) throw new InvalidOperationException("Gift inbox missing from native character SavedItems.");
                saving = character.DataStoreCache.SaveDirty(character.UserIdentity).GetAwaiter();
                stage = 1; MelonLoader.MelonLogger.Msg("Gift native QA: inbox insertion and serialization passed; waiting for native save.");
            }
            if (stage == 1 && saving.IsCompleted)
            {
                saving.GetResult();
                File.WriteAllText(Manifest, JsonSerializer.Serialize(new { characterId = character.Id, encoded }));
                Result = new { passed = true, phase = "save", inserted, characterId = character.Id,
                    nativeGiftTabUnlocked = manager.GiftingTabUnlocked, encoded,
                    scope = "native gift inbox insertion, character serialization and awaited SaveDirty in private QA; restart is a separate required check" };
            }
            if (!Finished && Time.unscaledTime > deadline) throw new TimeoutException("Native Gift save did not complete.");
        }
        catch (Exception ex) { Result = new { passed = false, stage, error = ex.ToString(), inserted }; }
    }
}
