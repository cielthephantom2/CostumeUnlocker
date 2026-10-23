using LastEpochLAN.Core;
using Il2CppCysharp.Threading.Tasks;
using UnityEngine;

namespace LastEpochLAN.Mod;

// Uses the native preview renderer, never an inventory or a persistent character.
internal sealed class NativeEquipmentReplica
{
    private static readonly Il2Cpp.EquipmentType[] armour = { Il2Cpp.EquipmentType.HELMET,
        Il2Cpp.EquipmentType.BODY_ARMOR, Il2Cpp.EquipmentType.GLOVES, Il2Cpp.EquipmentType.BOOTS };
    private readonly Il2Cpp.DummyEquipmentVisualsManager renderer;
    private readonly Il2Cpp.IMSlotManager slots;
    private readonly Il2Cpp.PlayerAnimationManager animation;
    private readonly NativeCosmeticReplica cosmetics;
    private UniTask.Awaiter request;
    private bool pending;
    private readonly Dictionary<int, EquipmentAppearance> applied = new();
    private string appliedBack = "";
    private AvatarState desired;
    private float retryAt;
    internal string Error { get; private set; } = "";
    internal string CosmeticError => cosmetics?.Error ?? "";

    internal NativeEquipmentReplica(GameObject root)
    {
        slots = root.GetComponentsInChildren<Il2Cpp.IMSlotManager>(true).FirstOrDefault();
        animation = root.GetComponentsInChildren<Il2Cpp.PlayerAnimationManager>(true).FirstOrDefault();
        if (slots == null) throw new InvalidOperationException("Remote class model has no native equipment slots.");
        var control = new GameObject("LAN private equipment renderer");
        control.SetActive(false); control.transform.SetParent(root.transform, false);
        try
        {
            renderer = control.AddComponent<Il2Cpp.DummyEquipmentVisualsManager>();
            renderer.enabled = false; renderer.SlotManager = slots;
            slots.SetIsLocalPlayer(false);
            // InitializeDummy waits for server-side initialization and starts an
            // inactive coroutine using LocalUserIdentity. A remote visual needs
            // explicit class information and already-resolved keys instead.
            renderer.characterInfoProvider = new Il2Cpp.SimpleCharacterInfoProvider((int)slots.characterClassID, 1, 0)
                .Cast<Il2Cpp.CharacterInfoProvider>();
            renderer.equippedGearData ??= new Il2CppSystem.Collections.Generic.Dictionary<Il2Cpp.EquipmentType,
                Il2Cpp.EquipmentVisualsManager.EquippedGearVisualData>();
            renderer.backSlotSelectCanceltoken ??= new Il2CppSystem.Threading.CancellationTokenSource();
            renderer.Initialized = true;
            slots.GatherBones(); slots.GetIMSlotReferences();
            cosmetics=new NativeCosmeticReplica(root);
        }
        catch { UnityEngine.Object.Destroy(control); throw; }
    }

    internal static (EquipmentAppearance[] Equipment, string Back) Capture(Il2Cpp.Actor actor)
    {
        var gear = actor.GetActorSync()?.actorVisuals?.equipmentVisualsManager;
        if (gear == null) return (Array.Empty<EquipmentAppearance>(), "");
        var result = new List<EquipmentAppearance>();
        for (int i = 0; i < armour.Length; i++)
            if (gear.equippedGearData != null && gear.equippedGearData.ContainsKey(armour[i]))
            {
                var data=gear.equippedGearData[armour[i]];
                result.Add(Translate(actor, gear, new EquipmentAppearance { Slot = i, EquipmentType = (int)armour[i],
                    SubType = data.subType, Unique = data.isUnique, UniqueId = data.uniqueID }));
            }
        var mainHand = NativeNullable.ReadField(gear, "mainHandData", () => gear.mainHandData);
        if (mainHand != null && mainHand.HasValue)
        {
            var data = mainHand.Value;
            result.Add(Translate(actor, gear, new EquipmentAppearance { Slot = 4, EquipmentType = data.itemType,
                SubType = data.subType, Unique = data.rarity >= 7, UniqueId = data.uniqueID }));
        }
        var offHand = NativeNullable.ReadField(gear, "offHandData", () => gear.offHandData);
        if (offHand != null && offHand.HasValue)
        {
            var data = offHand.Value;
            result.Add(Translate(actor, gear, new EquipmentAppearance { Slot = 5, EquipmentType = data.itemType,
                SubType = data.subType, Unique = data.rarity >= 7, UniqueId = data.uniqueID }));
        }
        // Offline visual managers can omit weapon cache data. Read only the
        // actual paper-doll slots; do not manufacture a default weapon.
        var equipment = actor.itemContainersManager?.equipment;
        if (equipment != null)
        {
            var main = equipment.weapon?.content;
            var off = equipment.offhand?.content;
            AddWeapon(main?.data, 4);
            AddWeapon(off?.data, 5);
        }
        return (result.ToArray(), gear.currentlyEquipedBackslotKey ?? "");

        void AddWeapon(Il2Cpp.ItemData data, int slot)
        {
            if (data == null) return;
            result.RemoveAll(item => item.Slot == slot);
            result.Add(Translate(actor, gear, new EquipmentAppearance { Slot = slot, EquipmentType = data.itemType,
                SubType = data.subType, Unique = Il2Cpp.Item.rarityIsUniqueSetOrLegendary(data.rarity), UniqueId = data.uniqueID }));
        }
    }

    private static EquipmentAppearance Translate(Il2Cpp.Actor actor, Il2Cpp.EquipmentVisualsManager gear, EquipmentAppearance item)
    {
        var service = Il2CppLE.Services.ServiceProvider.CosmeticKeyOverrideService;
        var identity = gear.userIdentity;
        var network = Il2CppLE.Services.ServiceProvider.CosmeticsNetworkManager;
        // TranslateItemKey dereferences the native cosmetics network manager when
        // externalCosmetics is null. Offline campaign can have no manager/equips.
        if (service == null || identity == null || network == null || string.IsNullOrEmpty(identity.MasterAccountID) ||
            !network.TryGetEquippedCosmeticsForPlayer(identity.MasterAccountID, out var equips) || equips == null)
            return item;
        int classId = actor.characterDataTracker.charData.CharacterClass;
        var key = new Il2CppLE.Services.Models.Items.ItemVisualKey(classId, (Il2Cpp.EquipmentType)item.EquipmentType,
            item.SubType, item.Unique, item.UniqueId, 0);
        service.TranslateItemKey(ref key, identity, item.Slot == 5, null);
        return item with { EquipmentType = (int)key.EquipmentType, SubType = key.SubType,
            Unique = key.IsUnique, UniqueId = key.UniqueID, Variation = key.Variation };
    }

    internal void Tick(AvatarState state)
    {
        desired = state;
        cosmetics.Tick(state);
        try
        {
            if (pending)
            {
                if (!request.IsCompleted) return;
                Complete();
            }
            if (desired == null || Time.unscaledTime < retryAt) return;
            var wanted = desired.Equipment.ToDictionary(item => item.Slot);
            foreach (var removed in applied.Keys.Where(slot => !wanted.ContainsKey(slot)).ToArray())
            {
                if (removed == 4) slots.removeMainHand();
                else if (removed == 5) slots.removeOffHand();
                else slots.RemoveWearableIM(Il2Cpp.EquipmentVisualsManager.GetSlotTypesForItemType((byte)armour[removed]));
                applied.Remove(removed);
            }
            foreach (var item in desired.Equipment)
            {
                if (applied.TryGetValue(item.Slot, out var old) && old == item) continue;
                if (!Enum.IsDefined(typeof(Il2Cpp.EquipmentType), item.EquipmentType) ||
                    (item.Slot < 4 && item.EquipmentType != (int)armour[item.Slot]) ||
                    (item.Slot >= 4 && !Il2Cpp.ItemList.isWeapon(item.EquipmentType) &&
                        !(item.Slot == 5 && Il2Cpp.ItemList.isOffhand(item.EquipmentType))))
                    throw new InvalidDataException($"Remote equipment type does not match slot: slot={item.Slot}, type={item.EquipmentType}, subtype={item.SubType}.");
                var type = (Il2Cpp.EquipmentType)item.EquipmentType;
                request = (item.Slot >= 4 ? renderer.EquipWeaponAsync(type, item.SubType, item.Unique,
                    item.UniqueId, item.Variation, item.Slot == 5) : renderer.EquipGearAsync(type,
                    item.SubType, item.Unique, item.UniqueId, item.Variation)).GetAwaiter();
                // Commit only after the native asynchronous load completes.
                inFlight = item; pending = true; return;
            }
            if (appliedBack != desired.BackCosmetic)
            {
                request = renderer.EquipBackSlotAsync(desired.BackCosmetic).GetAwaiter();
                backInFlight = desired.BackCosmetic; pending = true;
            }
            Error = "";
        }
        catch (Exception ex) { pending = false; inFlight = null; backInFlight = null; Error = ex.Message; retryAt = Time.unscaledTime + 2; }
    }
    private EquipmentAppearance inFlight;
    private string backInFlight;
    internal void Complete()
    {
        if (!pending || !request.IsCompleted) return;
        pending = false;
        try
        {
            request.GetResult();
            if (inFlight != null) applied[inFlight.Slot] = inFlight;
            if (inFlight?.Slot >= 4 && animation != null)
            {
                int main = applied.TryGetValue(4, out var mainItem) ? mainItem.EquipmentType : -1;
                int off = applied.TryGetValue(5, out var offItem) ? offItem.EquipmentType : -1;
                Il2Cpp.WeaponInfoHolder.getAnimationType(main, off, out var basic, out var advanced);
                animation.ReceiveSyncWeaponTypeInfo((int)basic, (int)advanced, 13);
            }
            if (backInFlight != null) appliedBack = backInFlight;
        }
        catch (Exception ex) { Error = ex.Message; retryAt = Time.unscaledTime + 2; }
        finally { inFlight = null; backInFlight = null; }
    }
}
