# TM Last Epoch Co-op Mod — 1.1.0-dev.9

Development release, protocol **35**. Close the game and run `update.bat` from the game folder (or pass that folder as its first argument). Both Host and Client must update. The updater verifies the exact DLL pair and keeps backups. Use **Play Offline**.

**Room Loot Filters:** each member shares their selected filter in memory. Edits, selection changes and disabling propagate automatically, polled once per second, including across maps. Keep the local result/style when it shows an item; if it hides the item, a remote filter may show it. Ground labels attribute filters that show the item. Remote filters do not overwrite local files or character bindings. Invalid remote filters are isolated by owner. Native Match uses an ABI-aware pointer bridge instead of Harmony out-Nullable marshalling. See [filter implementation and limits](ROOM-LOOT-FILTERS-2026-10-20-VI.md).

Joining only creates party membership: the Client stays in its own map and solo gameplay until it explicitly selects Teleport. Each new Host Echo requires another Teleport request. Party-only clients do not take over the Host world or count toward Host NPC scaling.

**F2 opens the independent Shared Chest**, persisted on the Host and usable across maps in the same character cycle/mode. It currently accepts eligible Gift equipment/Idols, one item per entry, with a native individual ID. It has 96 logical slots. The frontend now clones native Stash graphics and uses a new native item grid for icons and hover/tooltips. Its detached Host projection never binds personal Stash data; original controllers are stripped before activation. Cursor drop or inventory right/shift-click deposits; clicking a chest item receives it into inventory after Host/save confirmation. Pagination follows native item sizes. Shared Stash labels/help/status use English. Stash loading resolves the actual legacy/V2 panel with a bounded timeout. Item canvases have independent raycasters and native buttons keyed to Host item identity, fixing clicks passing through the chest into the world. Deposit/withdraw use durable client journals, Host reservations, save acknowledgments, operation deduplication and custody tickets. Uncertain operations stay held rather than being automatically refunded or regranted. Legacy encounter synchronization remains unsupported.

**Legacy encounter synchronization has been removed. Its replacement is not wired into gameplay yet.** This release is published at the user's request so both PCs can obtain the same cleanup build. Encounter interactions/rewards and generic map interactions are not synchronized. It does not provide shared Nemesis, Omen Window, Blood Crystal, Exiled Mage or Rift Beast gameplay. Native solo encounter behavior needs complete in-game regression testing.

Echo of a World / Shade of Orobyss now gives **x5 positive Corruption gain**, including native Gaze and catch-up bonuses. For example, +10 becomes +50. Existing Corruption is not multiplied; the native timeline cap and non-positive outcomes are preserved. Preview and completion use the same patched native calculator. The game still owns progress persistence.

Woven Potential now uses: 1 LP → 2 LP (50%) / 0 LP (50%); 2 LP → 3 LP (50%) / 1 LP (50%); 3 LP → 4 LP (25%) / 1 LP (75%). The dropped-item minimum of 1 LP does not clamp Woven failures to 0 LP. Other input LP values keep their native behavior. Crafting eligibility and use consumption remain in the native caller.

NPC movement, animation, VFX, minions, combat and party travel code is retained. That is not a two-PC acceptance claim; doors, quests and map interactions need regression after generic replay removal. The new typed Host encounter state machine is a managed foundation only; dedicated transport and private managed Nemesis turns exist, but per-family native adapters, presentation and durable private rewards remain unfinished. Encounter development is paused.

See [Vietnamese release report](RELEASE-1.1.0-dev9-VI.md) and [historical rewrite milestone](ENCOUNTER-REWRITE-VI.md).

## Validation

- Release build: zero warnings/errors.
- 713 managed checks pass, including existing custody simulations and the new managed encounter/transport foundation. This is not encounter gameplay acceptance.
- 18 dedicated LootFilter managed/TCP checks pass: validation/compression, edits, late join, cross-map party-only sharing, clear and disconnect.
- Compiled audit: 7 checks pass; legacy encounter replay hooks remain absent.
- Basic native smoke passes on this DLL pair. Ten native LootFilter observations pass, including nullable buffer canaries, verdict/style preservation, live filter edits and per-owner error isolation. Rendered label layout, hover/pooling and physical two-PC filter acceptance remain separate requirements.
- Earlier Shared Stash, Woven and campaign observations belong to their recorded DLL pair. They are regression context, not a fresh acceptance claim for this build.
- The full rendered native campaign gate remains FAIL with 76 runtime log errors, including duplicate service initialization. Feature observations passed separately; they do not turn that gate into a full pass.

Evidence lives in `validation/`. See [dev.9 release report](RELEASE-1.1.0-dev9-VI.md) for exact native scope and remaining limits.

## Build

Native game/loader references are private and not distributed. The tested runtime uses Unity 6000.4.8f1 and MelonLoader 0.7.3.

```powershell
dotnet build src/LastEpochLAN.Mod/LastEpochLAN.Mod.csproj -c Release -p:RuntimeRoot="<runtime>"
dotnet run --project tests/LastEpochLAN.SelfTest -c Release -- validation/managed-shared-chest.json
dotnet run --project tools/EncounterPatchAudit -c Release -p:RuntimeRoot="<runtime>" -- src/LastEpochLAN.Mod/bin/Release/net6.0/LastEpochLAN.dll src/LastEpochLAN.Core/bin/Release/net6.0/LastEpochLAN.Core.dll validation/patch-audit-shared-chest.json "<runtime>"
```

Native QA requires the game closed and an explicitly prepared private runtime/sandbox. Inspect `Test-NativeAdapter.ps1` first. `-NativeEncounters` is intentionally rejected; `-OmenRewards` only tests isolated native reward methods. Full campaign errors remain failures even when individual observations pass. `tests/Test-PublicUpdater.ps1` is the current updater test; `Test-Updater.ps1` is a historical test for the older release installer.
