# Danh sách tính năng và rà soát code — dev.55 / protocol44

Danh sách đối chiếu source và DLL hiện tại, không lấy changelog cũ làm bằng chứng rằng tính năng còn hoạt động. Chi tiết từng source/hook nằm trong validation/dev55-mod-inventory.json. Runtime chỉ cài các hook được RoomFeaturePolicy cho phép; SharedWorldEnabled=false.

## A. Những thay đổi tự áp dụng khi chơi offline

| Nhóm | Giá trị / hành vi đang áp dụng | Source chính |
|---|---|---|
| Di chuyển | +25% increased movement speed; một buff, không stack khi đổi map/reconnect | OfflineBonuses.cs, OfflineBonusRules.cs |
| Diện tích | +50% area cho area skills; một nguồn untagged | OfflineBonuses.cs |
| Regen | x2 phần base HP/MP regen tại SetInitialValues; gear/passive flat và increased/more vẫn tính tiếp. Không x2 max HP/MP. Base MP regen8→16; base HP regen6 và phần tăng theo level cũng được x2 | OfflineBaseRegeneration.cs |
| LP tối thiểu | Native LP roll được chặn dưới tại1, giữ phân bố roll cao; không áp cho set/Weaver's Will | OfflineBonuses.cs |
| Quái | Ngoài Monolith (campaign/Dungeon): mật độ gốc, không override. Trong Monolith: max(1, Corruption/100). Không nhân lại target lưu sau mỗi roll | OfflineDensity.cs, DensityRules.cs |
| Minion | Tầm acquisition/search x2 khi minion thuộc local player ở aggressive/Assassinate; không tăng damage hay range toàn cục | AggressiveMinionRange.cs |
| Shade | Phần corruption kiếm được và Gaze x5; catch-up không x5, bám timeline cao nhất để tránh chạy vượt và tăng vô hạn | OfflineCorruption.cs, OfflineBonusRules.cs |
| Corruption XP | Bỏ diminishing: +1% increased XP mỗi corruption | OfflineLinearCorruption.cs |
| Corruption LP | Hệ số 1 + (0.166/ln(10))×Corruption/100 | LinearCorruptionRules.cs |
| Corruption Weaver's Will | Hệ số 1 + (0.2/ln(10))×Corruption/100; giữ gate level và native roll tiers, cap28 | LinearCorruptionRules.cs |
| Corruption Echo rarity | Tuyến tính theo độ dốc native tại bước corruption đầu; bỏ diminishing | OfflineLinearCorruption.cs |
| Corruption Amber | Dùng độ dốc lớn hơn giữa phần trước/sau500 cho toàn corruption; không hạ biến thể asset đã tốt hơn | LinearCorruptionRules.cs |
| Gold Monolith | Sửa native gold-corruption contribution thành 1+0.0012×Corruption; thêm bonus riêng max(1, Corruption/40). Đây là hai bước nhân, còn modifier khác của game | OfflineLinearCorruption.cs, OfflinePickup.cs |
| Timeline Stability | x2 Stability cơ bản và bonus trong Echo Rewards mọi type; x2 Temporal Keystone. Giữ Forgotten Knights RNG, không nhân các nguồn khác hay chi phí | OfflineTimelineStability.cs |
| Bloodrage / Morditas | x2 charge kiếm được và balance thưởng khởi tạo của hunt, đồng bộ numerator/denominator UI; không nhân mỗi lần mở lại/consume, standalone reward giữ gốc | OfflineBloodrageCharges.cs |
| Prophecy | Số item thưởng cơ sở mỗi loại x2, gồm upgraded rewards; charge tiers và thông báo dùng game gốc. Không ép mọi loại thành20 item | OfflineFortune.cs |
| Glyph of Hope | 33% tránh tốn Forging Potential cho craft eligible | OfflineGlyphCrafting.cs |
| Glyph of Despair | T1=100%, T2=70%, T3=50%, T4=33%; Rare/Exalted cùng rate. Không mở sealing T5+ | OfflineGlyphCrafting.cs |
| Woven Potential reroll | LP1:50% lên2, còn lại xuống0. LP2:25% lên3, fail về1. LP3:25% lên4, fail về1. LP4:25% giữ4, còn lại xuống3; giữ native tiêu hao/eligibility | OfflineWovenPotential.cs |

## B. Faction và Weaver Tree

Các bonus rank yêu cầu membership, rank đạt và rank toggle đang bật. Không tự cấp rank.

| Faction/rank | Override |
|---|---|
| Circle of Fortune1 | 100% cơ hội enemy double item rewards |
| CoF3 | Idol rơi:50% cơ hội thêm2 Idol |
| CoF5 | 100% cơ hội double item rewards từ Echo |
| CoF6 | Exalted chance multiplier2, text +100% |
| CoF7 | Exiled Mages x15 Experimental items, thay hệ số x2 gốc |
| CoF8 | LP roll chance multiplier20, trước clamp minimumLP1; không có nghĩa bảo đảm LP cao |
| CoF10 | +300% boss-specific loot drop rate |
| CoF11 | Rare có50% cơ hội thành Exalted |
| Forgotten Knights1 | Additional chance cho swappable Nemesis unique/Egg of the Forgotten:100% |
| Forgotten Knights2 | Double Stability chance25%; cộng hưởng với x2 nguồn Stability ở mục A |
| Forgotten Knights5 | Additional chance Harbinger rơi Temporal Keystone50% |
| The Woven7 | +30% more likely second Weaver affix khi reforge Weaver Idol |
| The Woven9 | +50% more Memory Amber trong Echo |

Weaver Tree / Imprint (cần node và imprinted item đúng loại):

- Cache/Sealed:12% mỗi điểm (tooltip gốc6%).
- Champions: cơ hội item pile x2, tương ứng6% mỗi điểm theo base hiện tại.
- Commoners/enemies: trigger chance x2; giữ scaling enemy/spawner và corruption.
- Echo completion: chance/weight x2, xác suất cuối bị chặn100%; không bảo đảm mọi Echo đều rơi.
- Imprinted Woven Echo natural spawn: weighting x2; giữ eligibility/unlock/corruption gate. Đây là trọng số, không phải cộng2 điểm phần trăm.
- Scope đặt ở caller chọn Echo để tránh bridge cũ gây lỗi khi Empowered→Normal.

## C. Skill và Spirit Xylem

| Nội dung | Override |
|---|---|
| Meteor Extinction | 33% cơ hội large Meteor mỗi điểm, cap100%. Crit chance riêng của node giữ nguyên |
| Meteor Heat Flux | 2 Frost Claw mỗi điểm; impact trả100% native Frost Claw mana cost, thay120%. Không sửa manual Frost Claw |
| Meteor Stardust | Max1 điểm; Maximum Mana×0.001 MORE Damage mọi hệ. 500→x1.5,1000→x2,2000→x3. Nhân với modifier khác; không dùng mana hiện tại. Bỏ cả bonus non-fire và reciprocal fire gốc. Old4points→1, hoàn3 điểm |
| Arcane Ascendance | Bỏ root/prevent movement riêng của ailment người chơi; vẫn giữ mana drain và các hiệu ứng khác |
| Tranquility | Bỏ đúng stat -100% more ManaRegen trong active stats; giữ mana efficiency20% mỗi điểm, không mở regen toàn cục |
| Archmage tại5 điểm | Dưới300 max mana: +5 spell,35% refund20%. Từ300: +10,70% refund40%. Từ1000: +15,100% refund60%. Damage/chance scale theo điểm, chance cap100% |

Spirit Xylem (unique445, thay modifiers cũ; implicit/base item, ID, rolls, LP, legendary affix giữ nguyên):

- +3–4 All Skills (không lọc thuộc tính).
- Attunement bằng Intelligence cuối, sau khi cộng +10–20 Intelligence của item; ATT gốc có thể tăng hoặc giảm để bằng INT.
- +10–20 Intelligence, gồm All Attributes trong giá trị cuối.
- +3–6 Health và Mana mỗi Attunement; Mana cộng thêm vào +2 Mana/ATT gốc, thành +5–8 Mana/ATT trước modifier khác.
- +1 flat Spell Damage mỗi Intelligence, AT.Spell; không chia2 và không áp sang melee.
- Giữ More Damage to Bosses/Rares10–20% khi trên nửa mana và Less Damage Taken from Bosses/Rares20–10% khi dưới nửa mana.
- Bỏ reduced health cost và hai điều kiện so sánh thuộc tính; giữ rollID/implicit/LP/affix. Native localization thay bằng một paragraph5 dòng, không lặp range dư.

## D. F1/F4, minimap và tiện ích

| Tính năng | Cách hoạt động / mặc định |
|---|---|
| Combat Assist F1 | Theo dõi5 slot QWER/LMB và potion trên Unity main thread. Kiểm tra cooldown/vitals mỗi frame; potion trước target scan. Cached target25ms, registry refresh100ms; nhường channel khi slot khác đến hạn. Cấu hình từng nhân vật, từng slot/interval/range/HP threshold |
| Assist + UI | Vẫn hoạt động khi mở inventory/stash/skill/F1/F4/chat. Vẫn tôn trọng death/loading/focus/cinematic/native cast gate |
| Assist disconnect | Giữ armed/Start khi room đóng/disconnect; bỏ reference room/actor cũ rồi lấy local actor lại. Chuyển nhân vật vẫn nạp profile riêng |
| Auto Pickup | Gold/potion/key/material/Ancient Bone/Woven Echo toàn map; từng loại có tick, mặc định bật. Mỗi drop chờ0.5s từ lần đầu thấy; không yêu cầu di chuyển, không radius/count cap; native grant/storage/currency, không log mỗi món. Full potion không nhặt |
| Range Pickup | Tick mặc định bật: click loot từ xa không đi tới; khác với auto pickup. Không tự loot vũ khí/giáp chỉ vì range được mở |
| Cosmetics | Unlock native cosmetic selection offline, mặc định bật; lọc entry rỗng/junk của catalog, giữ item/skill slot eligibility; không còn Default Meteor tự tạo hoặc Vilatria override |
| New character | Mặc định bật: native-created character mới lên58, campaign quest và đến End of Time; không boost lại nhân vật cũ. Có journal tiếp tục job dở, không grant lặp |
| Respecialize Class | F4 → Mod Settings → Reset Class:5 class, confirm rồi về character select. Giữ level/items/progress; reset mastery/passive/skill/bar; backup Saves trước mutation; đợi native flush. Không dùng NPC dialogue |
| Reveal objectives | Mặc định bật: Monolith objective reveal threshold; Dungeon delayed pulse sau0.5s |
| Enemy minimap dots | Mặc định bật; giữ sprite qua unload asset, khôi phục registration thiếu, quản lý marker quái local và dọn marker khi đổi scene |
| Player minimap dot | Xanh dương, đường kính3×NPC dot (gấp đôi bản chấm xanh trước); chỉ minimap, Tab overlay giữ marker gốc |
| Gray unexplored map | Mặc định bật: thêm terrain preview màu xám, opacity mặc định0.2, không ghi khám phá thật vào save |
| Extended minimap zoom | Mặc định bật, mở clamp tối thiểu tới2.5 và tối đa ít nhất150; nút +/- gốc vẫn xử lý step/save |
| Camera zoom | Mặc định bật,0.5–5×, bước0.1 bằng dấu phẩy/chấm; giữ camera interpolation/input gốc |
| DPS | HUD damage/skill breakdown, reset/pause/hide, scale/position lưu riêng; theo actual HP loss để tránh double count, có ailment/DoT hooks |
| Offline enforced | Chặn luồng chuyển sang Online ở landing/mode/character UI |
| Changelog/update | Changelog bonus offline; kiểm tra release/update status. update.bat ghim manifest+hash, từ chối khi game đang chạy, backup cặp DLL |
| Diagnostics | Bộ đo600 frame, runtime report; khi Assist/room hoạt động có thể ghi report mỗi2s. Đây là code đang dùng, có thể gây allocation/I/O; chưa thay đổi trong lần rà soát này |

## E. Room được bật trong bản phát hành

- Kết nối Internet/public IP host/join room, protocol44.
- F2 Shared Stash: category/tab native, custody/lock/transaction và persist; không có Delete Tab con tùy biến (Delete Category native vẫn còn).
- Chia sẻ Loot Filters và native matching, lọc theo owner.
- Room Ladder: dữ liệu nhân vật trong room, native table/class/mode filters, không dùng online ladder service.
- Local character presence cần cho các tính năng inventory trên. Disconnect cleanup cố gắng tách lỗi teardown khỏi Assist.

## F. Code bị khóa và phần cần dọn tiếp

- SharedWorldEnabled=false: shared enemies/combat/world/travel/Monolith party/remote character-equipment-animation-skill VFX/summon/reward synchronization không hoạt động như một tính năng co-op trong release. Một phần source và type vẫn compile, một số object còn được tạo trong LanMod. 66 Harmony patch classes bị policy bỏ qua; không đồng nghĩa66 tính năng độc lập.
- Các adapter, packet schemas, ledger và native probes phục vụ nhánh shared-world cũ còn trong Core/Mod. Chưa xóa hàng loạt vì có dependency chung với stash/filter/ladder và QA. Đây là code ứng viên tách khỏi release, không phải lý do tự bật lại shared-world.
- QA/Smoke/Probe files thường chỉ chạy bằng command-line riêng; không nên gọi tất cả là junk. Đợt kiểm tra này có helper ItemNodeNativeQA riêng trong tools và chỉ cài trong runtime QA, không đưa vào Mods của Steam.
- Danh sách no-direct-caller trong JSON chỉ là ứng viên: Harmony reflection, delegate, generic method hoặc CLI QA có thể không hiện thành direct IL call. Ví dụ OfflineHopeRollHook.Prefix được Harmony.Patch thủ công nên vẫn dùng.
- Đã gỡ QolRules (radius/reveal helpers), MeteorNodeRules.LargeMeteorChance sau khi xác nhận không còn caller runtime; gỡ NativeQaNaturalBoot vốn bị release policy chặn; gỡ thêm constant Name không dùng của Stardust. Không xóa các helper đang được native/Harmony gọi.
- Default Meteor save repair: đã xác nhận log Collection was modified phát từ hook này; xóa source/hook và fixture cũ theo yêu cầu.
- World Assist/Danger Areas/Paths, UI Shortcuts, custom Prophecy charge rewards, Vilatria VFX toggle: đã gỡ; không được tính là tính năng đang bật.

## Bằng chứng và giới hạn

- validation/dev55-mod-inventory.json:202 source files, 1034 compiled types, 144 hook classes cài /66 hook classes bỏ qua, 13 no-direct-caller candidates còn lại sau đợt dọn7 helper.
- Rà static không chứng minh mọi nhánh gameplay của toàn mod chạy đúng. Không có tuyên bố rằng toàn bộ tính năng ở bảng đã được đo ingame trong đợt này.
- Kiểm tra engine riêng cho Spirit Xylem/Stardust ghi ở validation/dev55-item-node-native-qa.json sau khi runner hoàn tất; báo cáo ghi rõ phạm vi thực tế, không đồng nhất với ảnh tooltip render hay combat-hit measurement.

## Kết quả review20 ứng viên ban đầu

Đã kiểm tra20 ứng viên ban đầu:7 helper không có caller đã gỡ;1 hook Hope được cài thủ công vẫn giữ;12 hàm thuộc nhánh shared-world đang khóa được giữ để tránh đứt dependency. Không có20 lỗi gameplay.

| Hàm | Source | Đánh giá |
|---|---|---|
| `System.Void LastEpochLAN.Mod.LanSharedChest::AddTab()` | `src/LastEpochLAN.Mod/LanSharedChest.cs` | ĐÃ GỠ: không có caller source/IL/registration |
| `T LastEpochLAN.Mod.NativeEquipmentReplica::ReadOptional(System.Func`1<T>)` | `src/LastEpochLAN.Mod/NativeEquipmentReplica.cs` | ĐÃ GỠ: không có caller source/IL/registration |
| `System.Collections.Generic.IEnumerable`1<System.Reflection.MethodBase> LastEpochLAN.Mod.NativeGuestActorLifecyclePatch::TargetMethods()` | `src/LastEpochLAN.Mod/NativeGuestCombat.cs` | Nhánh shared-world/guest cũ; đang bị policy chặn hoặc chỉ QA |
| `System.Boolean LastEpochLAN.Mod.NativeGuestActorLifecyclePatch::Prefix(Il2Cpp.Actor,System.Reflection.MethodBase)` | `src/LastEpochLAN.Mod/NativeGuestCombat.cs` | Nhánh shared-world/guest cũ; đang bị policy chặn hoặc chỉ QA |
| `System.Boolean LastEpochLAN.Mod.NativeGuestTargetValidityPatch::Prefix(Il2Cpp.Actor,Il2Cpp.LifeState,System.Boolean&)` | `src/LastEpochLAN.Mod/NativeGuestCombat.cs` | Nhánh shared-world/guest cũ; đang bị policy chặn hoặc chỉ QA |
| `System.Boolean LastEpochLAN.Mod.NativeGuestTargetInterfacePatch::Prefix(Il2Cpp.Actor,System.Boolean&)` | `src/LastEpochLAN.Mod/NativeGuestCombat.cs` | Nhánh shared-world/guest cũ; đang bị policy chặn hoặc chỉ QA |
| `System.Boolean LastEpochLAN.Mod.NativeGuestProtectionAwakePatch::Prefix(Il2Cpp.ProtectionClass)` | `src/LastEpochLAN.Mod/NativeGuestCombat.cs` | Nhánh shared-world/guest cũ; đang bị policy chặn hoặc chỉ QA |
| `System.Boolean LastEpochLAN.Mod.NativeGuestTargetPriorityPatch::Prefix(Il2Cpp.Actor,System.Single,System.Single&)` | `src/LastEpochLAN.Mod/NativeGuestCombat.cs` | Nhánh shared-world/guest cũ; đang bị policy chặn hoặc chỉ QA |
| `System.Boolean LastEpochLAN.Mod.NativeGuestHolderDamagePatch::Prefix(Il2Cpp.DamageStatsHolder,Il2Cpp.Actor)` | `src/LastEpochLAN.Mod/NativeGuestCombat.cs` | Nhánh shared-world/guest cũ; đang bị policy chặn hoặc chỉ QA |
| `System.Boolean LastEpochLAN.Mod.NativeGuestHolderProtectionDamagePatch::Prefix(Il2Cpp.DamageStatsHolder,Il2Cpp.ProtectionClass,System.Single)` | `src/LastEpochLAN.Mod/NativeGuestCombat.cs` | Nhánh shared-world/guest cũ; đang bị policy chặn hoặc chỉ QA |
| `System.Guid LastEpochLAN.Mod.NativeHostBloodCrystalAdapter::Register(Il2Cpp.RageOfMorditasLogic)` | `src/LastEpochLAN.Mod/NativeHostBloodCrystalAdapter.cs` | Nhánh shared-world/guest cũ; đang bị policy chặn hoặc chỉ QA |
| `System.Void LastEpochLAN.Mod.NativeHostBloodCrystalAdapter::Poll()` | `src/LastEpochLAN.Mod/NativeHostBloodCrystalAdapter.cs` | Nhánh shared-world/guest cũ; đang bị policy chặn hoặc chỉ QA |
| `System.Guid LastEpochLAN.Mod.NativeHostRiftAdapter::Register(Il2Cpp.TimeBeastRift)` | `src/LastEpochLAN.Mod/NativeHostRiftAdapter.cs` | Nhánh shared-world/guest cũ; đang bị policy chặn hoặc chỉ QA |
| `System.Void LastEpochLAN.Mod.NativeHostRiftAdapter::Poll()` | `src/LastEpochLAN.Mod/NativeHostRiftAdapter.cs` | Nhánh shared-world/guest cũ; đang bị policy chặn hoặc chỉ QA |
| `T LastEpochLAN.Mod.NativePlayerAnimationReplica::Read(System.Func`1<Il2CppSystem.Nullable`1<T>>,T)` | `src/LastEpochLAN.Mod/NativePlayerAnimationReplica.cs` | ĐÃ GỠ: không có caller source/IL/registration |
| `System.Void LastEpochLAN.Mod.NativeRewardCompletion::Reset()` | `src/LastEpochLAN.Mod/NativeRewardCompletion.cs` | ĐÃ GỠ: không có caller source/IL/registration |
| `System.Void LastEpochLAN.Mod.OfflineTools.MapFog::ApplyCurrent()` | `src/LastEpochLAN.Mod/OfflineTools/MapFog.cs` | ĐÃ GỠ: không có caller source/IL/registration |
| `System.Void LastEpochLAN.Mod.OfflineTools.OfflineHopeRollHook::Prefix(Il2CppLE.Core.RngElement,System.Single&)` | `src/LastEpochLAN.Mod/OfflineTools/OfflineGlyphCrafting.cs` | ĐANG DÙNG: được cài bằng Harmony.Patch thủ công |
| `System.String LastEpochLAN.Mod.OfflineTools.Dps.DamageMeterOverlay::GetMapLabel()` | `src/LastEpochLAN.Mod/OfflineTools/Dps/DamageMeterOverlay.cs` | ĐÃ GỠ: không có caller source/IL/registration |
| `System.String LastEpochLAN.Mod.OfflineTools.Dps.DamageMeterOverlay::FormatDuration(System.Single)` | `src/LastEpochLAN.Mod/OfflineTools/Dps/DamageMeterOverlay.cs` | ĐÃ GỠ: không có caller source/IL/registration |

## Kết quả xác minh engine của bản này

79/79 native IL2CPP checks đạt trên GameAssembly Steam26087601 (SHA323E9183…). Profile private đã được engine xác minh; character LANQA dùng riêng. Cặp DLL được ghi hash vào validation/dev55-item-node-native-qa.json.

- Tooltip đi qua Localization.GetText và UniqueModParser thật: đủ Attunement và Intelligence ở min/max rolls. Khóa thật là Unique_Tooltip_0_445 / Unique_Tooltip_1_445 và các khóa _Alt.
- ItemEquipManager.UpdateStats thật nhận đúng reduced health cost, more boss/rare damage và less boss/rare damage ở cả hai đầu roll. Các điều kiện mana vẫn là native modifier gốc.
- CharacterStats.ApplyCoreAttributeModifiers thật nhận đúng attribute, cả nguồn All Attributes. Native skill max-points consumer cấp3–4 điểm chỉ cho skill scale theo attribute đúng (Thorn Totem/Fireball).
- Stats.GetStatValue thật nhận đúng phần thêm HP/MP/spell; tính tách khỏi các bonus thuộc tính gốc. 20 lần recalc không stack; native unequip gỡ source và hoàn attribute về baseline.
- Stardust chạy MeteorSkillTree.updateMutator, MeteorMutator.addsTempStats/getTempStats và Stats.GetStatValue thật. Trên Fire/Cold/Physical, baseline có sẵn25% more là125; max mana500/1000/2000 cho187.5/250/375. Mana0 giữ125. Cache native không tích stat dư; old4points→1 hoàn3 điểm qua native getUnspentPoints.
- Runner lấy được Meteor component native có sẵn qua LocalTreeData/mutator manager (createdForBaseMage=false); không cần tạo component và không giả lập công thức trả về. Chưa đo HP loss của monster sau cast, chưa chụp tooltip render. Không coi79 checks này là gameplay acceptance của mọi tính năng mod.
- Build0 warnings/errors; Core1053, Spirit33, Meteor/Arcane106, Archmage94, class/regen47, pickup39, Forgotten Knights59, Fortune40, stability45 và compiled/native audit189 đều đạt. Native log QA không còn cảnh báo Default Meteor save repair.

## Tiếp tục sau khi hết token — dọn helper

Đã gỡ7 hàm không dùng: LanSharedChest.AddTab (UI đang dùng AddTabInCategory), NativeEquipmentReplica.ReadOptional (đã thay bằng NativeNullable.ReadField), NativePlayerAnimationReplica.Read (đã thay bằng reader hiện tại), NativeRewardCompletion.Reset, MapFog.ApplyCurrent (Tick đang được toolkit gọi), DPS.GetMapLabel và DPS.FormatDuration.

Giữ nguyên AddTabInCategory, routing/transaction stash, MapFog.Tick, DPS runtime, reader Nullable hiện tại và144 hook đang cài.66 hook bị khóa không được tự bật lại. Không xóa các adapter shared-world/interface methods đang có dependency chỉ để giảm số dòng code. Danh sách13 ứng viên còn lại nằm trong JSON và bảng review phía trên; chúng đã được phân loại, không còn để trạng thái chưa đọc.

Tool ModInventory cũng bỏ qua nested helper của Harmony patch owner khi tìm no-direct-caller candidates, tránh gắn nhãn nhầm hàm được reflection sử dụng.

So sánh DLL trước/sau dọn: chỉ7 method đã nêu bị xóa,0 method có tên khác được thêm hoặc đổi IL sau khi chuẩn hóa ordinal lambda/display-class do compiler đánh số lại.4.443 method giữ nguyên IL theo phép so sánh này;30 helper compiler có tên ordinal mới. Shared Stash native priority fixture12/12, class47/47, Spirit33/33, Meteor106/106 và audit189/189 đạt. Bằng chứng: validation/dev55-helper-cleanup-il-equivalence.json.

DLL sau dọn đã chạy lại79/79 engine checks trong profile riêng và không có cảnh báo Default Meteor repair. Bằng chứng theo hash mới: validation/dev55-helper-cleanup-native-qa.json. Native combat-hit và tooltip render vẫn chưa được đo/chụp.
