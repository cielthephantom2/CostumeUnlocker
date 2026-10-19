# TM Last Epoch Co-op Mod — 1.1.0-dev.1

Development release, protocol **32**. Close the game and run `update.bat` from the game folder (or pass that folder as its first argument). Both Host and Client must update. The updater verifies the exact DLL pair and keeps backups. Use **Play Offline**.

**Legacy encounter synchronization has been removed. Its replacement is not wired into gameplay yet.** This release is published at the user's request so both PCs can obtain the same cleanup build. Encounter interactions/rewards and generic map interactions are not synchronized. It does not provide shared Nemesis, Omen Window, Blood Crystal, Exiled Mage or Rift Beast gameplay. Native solo encounter behavior needs complete in-game regression testing.

Echo of a World / Shade of Orobyss now gives **x5 positive Corruption gain**, including native Gaze and catch-up bonuses. For example, +10 becomes +50. Existing Corruption is not multiplied; the native timeline cap and non-positive outcomes are preserved. Preview and completion use the same patched native calculator. The game still owns progress persistence.

NPC movement, animation, VFX, minions, combat and party travel code is retained. That is not a two-PC acceptance claim; doors, quests and map interactions need regression after generic replay removal. The new typed Host encounter state machine is a managed foundation only; dedicated transport, per-family native adapters, presentation and durable private rewards remain unfinished.

See [Vietnamese release report](RELEASE-1.1.0-dev1-VI.md) and [historical rewrite milestone](ENCOUNTER-REWRITE-VI.md).

## Validation

- Release build: zero warnings/errors.
- 505 managed checks pass, including reward delta, component, cap and overflow cases.
- Compiled audit: 7 checks pass across 104 Harmony classes; no legacy encounter or generic interaction replay hooks. CoF Rank 7's offline quantity bonus is explicitly retained.
- Native asset/adapter sandbox QA passes on the distributed DLL pair. Actual patched Corruption calculator: 100 → 130; with two Gaze, 100 → 250; capped timeline 49 → 50; repeated preview is stable. This does not validate a boss kill or saved progression.
- 15 updater fixture checks pass. No production saves or installed DLLs were modified during QA.
- Earlier full native campaign QA remains failed with five sandbox bootstrap errors; no complete solo encounter or two-PC acceptance is claimed.

Evidence lives in `validation/`. Earlier milestone reports refer to their own builds, not the current DLL hashes.

## Build

Native game/loader references are private and not distributed. The tested runtime uses Unity 6000.4.8f1 and MelonLoader 0.7.3.

```powershell
dotnet build src/LastEpochLAN.Mod/LastEpochLAN.Mod.csproj -c Release -p:RuntimeRoot="<runtime>"
dotnet run --project tests/LastEpochLAN.SelfTest -c Release -- validation/managed-release-dev1.json
dotnet run --project tools/EncounterPatchAudit -c Release -p:RuntimeRoot="<runtime>" -- src/LastEpochLAN.Mod/bin/Release/net6.0/LastEpochLAN.dll src/LastEpochLAN.Core/bin/Release/net6.0/LastEpochLAN.Core.dll validation/patch-audit-release-dev1.json "<runtime>"
```

Native QA requires the game closed and an explicitly prepared private runtime/sandbox. Inspect `Test-NativeAdapter.ps1` first. `-NativeEncounters` is intentionally rejected; `-OmenRewards` only tests isolated native reward methods. Full campaign errors remain failures even when individual observations pass. `tests/Test-PublicUpdater.ps1` is the current updater test; `Test-Updater.ps1` is a historical test for the older release installer.
