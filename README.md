# Encounter rewrite — source-only development branch

This branch removes the legacy co-op encounter implementation and starts a replacement. It is **not a playable encounter release**. There are no downloadable game DLLs, update manifest or updater on this branch. The automatic updater on main still distributes dev.27, which has reported encounter problems.

Experimental source version: `1.1.0-encounter-rewrite.1`, protocol `32`.

The old Nemesis/Omen/shrine/random encounter adapters, generic interaction callback replay, scene graph/object replay and legacy encounter wire path are removed. Solo encounter callbacks are left to the native game. Offline bonuses remain. NPC movement, animation, VFX, combat and party travel code is retained; doors, quests and map interactions need regression testing because generic replay has been removed.

A new room-owned, typed Host state machine is in `src/LastEpochLAN.Core/Encounters/EncounterHost.cs`. It has no Unity/Harmony dependencies and does not grant rewards. Dedicated encounter transport, per-family native adapters, client presentation and durable per-player reward execution are **not implemented yet**. It is not wired into gameplay.

See [Vietnamese progress, evidence and acceptance gates](ENCOUNTER-REWRITE-VI.md).

## Validation

- Release build: zero warnings/errors.
- 500 managed checks pass, including 23 new authority checks using accepted peer state from actual loopback TCP. Encounter commands themselves are not yet transported.
- Compiled audit: 7 checks pass across 103 Harmony classes; no legacy encounter or generic callback replay hooks remain. The explicitly retained CoF Rank 7 offline quantity bonus is excluded from that prohibition.
- Private native Omen isolation observations: both item-only and full native reward paths create ground items, including 4 Corrupted items each. This does not test a complete encounter.
- Full native campaign QA **fails** with five sandbox bootstrap errors; observations are recorded separately. No two-PC acceptance claimed.

No real saves or production log dumps are included. Production game DLLs/saves were not modified.

## Build and check

Native game/loader references are not distributed. Supply a matching private runtime directory (the tested build uses Unity 6000.4.8f1 and MelonLoader 0.7.3).

```powershell
dotnet build src/LastEpochLAN.Mod/LastEpochLAN.Mod.csproj -c Release -p:RuntimeRoot="<runtime>"
dotnet run --project tests/LastEpochLAN.SelfTest -c Release -- validation/managed-rewrite.json
dotnet run --project tools/EncounterPatchAudit -c Release -p:RuntimeRoot="<runtime>" -- src/LastEpochLAN.Mod/bin/Release/net6.0/LastEpochLAN.dll src/LastEpochLAN.Core/bin/Release/net6.0/LastEpochLAN.Core.dll validation/patch-audit.json "<runtime>"
```

Native QA is optional and requires the game to be closed. It creates a disposable sandbox/character rather than using existing saves. Inspect `Test-NativeAdapter.ps1` and build `tools/GuestProfile` against the same private runtime before the Omen isolation test. `-NativeEncounters` is intentionally rejected because the legacy test was removed. `-OmenRewards` runs the isolation test; any Unity/runtime error makes the whole native QA fail.
