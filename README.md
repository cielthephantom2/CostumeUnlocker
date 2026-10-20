# TM Last Epoch Co-op Mod — 1.1.0-dev.8

Development release, protocol **33**. Close the game and run `update.bat` from the game folder (or pass that folder as its first argument). Both Host and Client must update. The updater verifies the exact DLL pair and keeps backups. Use **Play Offline**.

Joining only creates party membership: the Client stays in its own map and solo gameplay until it explicitly selects Teleport. Each new Host Echo requires another Teleport request. Party-only clients do not take over the Host world or count toward Host NPC scaling.

**F2 opens the independent Shared Chest**, persisted on the Host and usable across maps in the same character cycle/mode. It currently accepts eligible Gift equipment/Idols, one item per entry, with a native individual ID. It has 96 logical slots. The frontend now clones native Stash graphics and uses a new native item grid for icons and hover/tooltips. Its detached Host projection never binds personal Stash data; original controllers are stripped before activation. Cursor drop or inventory right/shift-click deposits; clicking a chest item receives it into inventory after Host/save confirmation. Pagination follows native item sizes. Shared Stash labels/help/status use English. Stash loading resolves the actual legacy/V2 panel with a bounded timeout. Item canvases have independent raycasters and native buttons keyed to Host item identity, fixing clicks passing through the chest into the world. Deposit/withdraw use durable client journals, Host reservations, save acknowledgments, operation deduplication and custody tickets. Uncertain operations stay held rather than being automatically refunded or regranted. Legacy encounter synchronization remains unsupported.

**Legacy encounter synchronization has been removed. Its replacement is not wired into gameplay yet.** This release is published at the user's request so both PCs can obtain the same cleanup build. Encounter interactions/rewards and generic map interactions are not synchronized. It does not provide shared Nemesis, Omen Window, Blood Crystal, Exiled Mage or Rift Beast gameplay. Native solo encounter behavior needs complete in-game regression testing.

Echo of a World / Shade of Orobyss now gives **x5 positive Corruption gain**, including native Gaze and catch-up bonuses. For example, +10 becomes +50. Existing Corruption is not multiplied; the native timeline cap and non-positive outcomes are preserved. Preview and completion use the same patched native calculator. The game still owns progress persistence.

Woven Potential now uses: 1 LP → 2 LP (50%) / 0 LP (50%); 2 LP → 3 LP (50%) / 1 LP (50%); 3 LP → 4 LP (25%) / 1 LP (75%). The dropped-item minimum of 1 LP does not clamp Woven failures to 0 LP. Other input LP values keep their native behavior. Crafting eligibility and use consumption remain in the native caller.

NPC movement, animation, VFX, minions, combat and party travel code is retained. That is not a two-PC acceptance claim; doors, quests and map interactions need regression after generic replay removal. The new typed Host encounter state machine is a managed foundation only; dedicated transport, per-family native adapters, presentation and durable private rewards remain unfinished.

See [Vietnamese release report](RELEASE-1.1.0-dev8-VI.md) and [historical rewrite milestone](ENCOUNTER-REWRITE-VI.md).

## Validation

- Release build: zero warnings/errors.
- 696 managed checks pass, including actual cross-map TCP and 150 failure/custody simulations.
- Compiled audit: 7 checks pass across 110 Harmony classes; no legacy encounter or generic interaction replay hooks. CoF Rank 7's offline quantity bonus is explicitly retained.
- Six native Woven threshold observations pass, including serialized 0 LP and exact preservation of other item bytes. This does not validate live panel use consumption or inventory saving.
- Basic native adapter smoke passes on the distributed DLL pair. Private native chest observations pass: remove/save → Host custody → add/save, exactly one final native copy with the same item bytes; cross-map remote queries; no native Monolith transition from joining alone. Native Stash item UI creation, independent binding, direct mutation/move guards and SaveItems exclusion pass; a second cursor deposit via the native drop handler also passes. A rendered private sandbox additionally verifies actual EventSystem raycast/click dispatch to an item button, successful Host withdrawal/save and exactly one request after a double click.
- The raw full rendered campaign gate remains FAIL with sandbox initialization errors, including duplicate native service initialization. These additional failures are not claimed as an accepted baseline. Raw failures remain separate from passing raycast/click/transfer observations.
- Full visual layout/hover/drag acceptance, real crash/save reload and physical two-PC native inventories remain unverified. Screenshot output was black even though the final native raycast/click/withdraw observations completed; these limits are separate from passing custody simulations.

Evidence lives in `validation/`. Earlier milestone reports refer to their own builds, not the current DLL hashes.

## Build

Native game/loader references are private and not distributed. The tested runtime uses Unity 6000.4.8f1 and MelonLoader 0.7.3.

```powershell
dotnet build src/LastEpochLAN.Mod/LastEpochLAN.Mod.csproj -c Release -p:RuntimeRoot="<runtime>"
dotnet run --project tests/LastEpochLAN.SelfTest -c Release -- validation/managed-shared-chest.json
dotnet run --project tools/EncounterPatchAudit -c Release -p:RuntimeRoot="<runtime>" -- src/LastEpochLAN.Mod/bin/Release/net6.0/LastEpochLAN.dll src/LastEpochLAN.Core/bin/Release/net6.0/LastEpochLAN.Core.dll validation/patch-audit-shared-chest.json "<runtime>"
```

Native QA requires the game closed and an explicitly prepared private runtime/sandbox. Inspect `Test-NativeAdapter.ps1` first. `-NativeEncounters` is intentionally rejected; `-OmenRewards` only tests isolated native reward methods. Full campaign errors remain failures even when individual observations pass. `tests/Test-PublicUpdater.ps1` is the current updater test; `Test-Updater.ps1` is a historical test for the older release installer.
