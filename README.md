# Offline Co-op 1.0.9-dev.15 / protocol 28

This repository now distributes offline Co-op at the owner's request. CostumeUnlocker.dll has been removed. This replaces the online cosmetic-only mod with an offline development candidate.

Install MelonLoader 0.7.3 x64 for the supported game build. Copy both files, preserving these paths:

```
<game>/Mods/LastEpochLAN.dll
<game>/UserLibs/LastEpochLAN.Core.dll
```

Update BOTH PCs. Both DLLs are required. Remove CostumeUnlocker.dll from Mods to avoid overlapping hooks. Exit the game before installing. Keep saves and presets. Choose Play Offline; F4 opens the panel. Internet hosting uses TCP 7777 and requires port forwarding/firewall configuration.

Download the current `update.bat` and place it in the game folder, then run it after exiting the game. Alternatively pass the game folder as its first argument. It verifies both DLLs against SHA-256 from a single immutable GitHub commit, backs up replaced/conflicting DLLs, updates the pair and starts the game. It never closes your game or deletes saves/presets. Existing old Costume updater copies are incompatible; download this new batch instead.

Client NPC animation update: animation commands wait for loaded controllers/layers and held motion retries each frame; ready Guest NPCs suppress competing local animation queues. NPC buffering adapts to packet jitter (100–250ms). Teleport to normal maps/towns/hubs no longer restricts the origin category and waits for completed player spawn. Entry into active Monolith/Arena/Dungeon/Pinnacle runs still needs run-context transfer and remains unsupported. 461 managed/network checks and 16 source-linked recording-stub adapter checks passed; clean Release build. Native animation and two-PC smoothness remain unverified. The user confirmed Lightning Blast chain already works on the preceding build. See [dev.15 report](NPC-TRAVEL-dev15-VI.md).

Previous density and DPS adjustment: outside-Monolith placement density is now x5; Monolith remains max(1, corruption / 100). Applies to newly generated maps. DPS shows up to eight skill rows and defaults to the top-left. Legacy position is reset there once on upgrade; later drag positions are saved. Activity auto-hide, compact summary and F4 update status are retained. 451 managed/network checks passed; Release build clean. Real map-loading improvement and in-game layout remain unverified. Update both DLLs.

Previous UI update: DPS HUD stays hidden until damage is captured, then auto-hides after five seconds without new damage while preserving totals. DPS / 5S TOTAL / TOTAL / BIGGEST / HITS share one compact row, with up to four skill rows. F4 shows a background release check under Mod Settings: checking, latest version, new update available or unavailable. It checks at most once per five minutes with a six-second timeout; no automatic installation. 451 managed/network checks passed; clean Release build. In-game UI render validation is pending. Update both DLLs.

Previous NPC presentation iteration: animation/control/VFX snapshots now share the buffered pose timeline, disabled complex visual drivers advance each render frame, attack replay avoids duplicate starts, and native spawn admission prioritizes nearby Guest enemies. 437 managed/network checks and 9 source-linked adapter checks with recording API stubs passed. Both DLLs changed and must be updated together. Native visuals, real two-PC smoothness and game FPS remain unverified; Guest DPS remains pending; the user has since confirmed Lightning Blast chain works. See [NPC report](NPC-SYNC-dev12-VI.md).

Previous character-loading hotfix: removed the native creation async MoveNext hook. Campaign setup waits until the new player has spawned, loading has ended, and the scene is stable. End of Time travel is issued once and observed; Retry cannot overlap a pending save or travel. 425 managed/network checks passed, including 9 new lifecycle guards; clean Release build. Real new-character creation still needs in-game verification. This earlier hotfix did not address NPC presentation or Guest DPS.

Previous UI update: simplified Host status and Mod Settings labels; persistent Show/Hide DPS control; character setup retry tooltip. Release build passed with zero warnings/errors. In-game UI validation is pending.

The preceding dev.9 baseline passed 416 managed regression checks. Synthetic managed benchmarks show reduced sparse-world traffic and allocations, not measured two-PC FPS. Selected native observations passed; full isolated campaign QA still reports bootstrap/runtime errors. Real two-PC gameplay validation is pending.

This is a development candidate, not full online parity. Shared statuses and encounters remain incomplete, including advanced consume/spread/proc behavior and some rewards. Host native actor scans and shared-TCP blocking remain potential bottlenecks.
