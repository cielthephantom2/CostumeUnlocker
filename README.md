# Offline Co-op 1.0.9-dev.11 / protocol 28

This repository now distributes offline Co-op at the owner's request. CostumeUnlocker.dll has been removed. This replaces the online cosmetic-only mod with an offline development candidate.

Install MelonLoader 0.7.3 x64 for the supported game build. Copy both files, preserving these paths:

```
<game>/Mods/LastEpochLAN.dll
<game>/UserLibs/LastEpochLAN.Core.dll
```

Update BOTH PCs. Both DLLs are required. Remove CostumeUnlocker.dll from Mods to avoid overlapping hooks. Exit the game before installing. Keep saves and presets. Choose Play Offline; F4 opens the panel. Internet hosting uses TCP 7777 and requires port forwarding/firewall configuration.

Download the current `update.bat` and place it in the game folder, then run it after exiting the game. Alternatively pass the game folder as its first argument. It verifies both DLLs against SHA-256 from a single immutable GitHub commit, backs up replaced/conflicting DLLs, updates the pair and starts the game. It never closes your game or deletes saves/presets. Existing old Costume updater copies are incompatible; download this new batch instead.

Character-loading hotfix: removed the native creation async MoveNext hook. Campaign setup waits until the new player has spawned, loading has ended, and the scene is stable. End of Time travel is issued once and observed; Retry cannot overlap a pending save or travel. 425 managed/network checks passed, including 9 new lifecycle guards; clean Release build. Real new-character creation still needs in-game verification. This update does not fix Guest NPC stutter, Guest DPS or Lightning Blast chain.

Previous UI update: simplified Host status and Mod Settings labels; persistent Show/Hide DPS control; character setup retry tooltip. Release build passed with zero warnings/errors. In-game UI validation is pending.

The preceding dev.9 baseline passed 416 managed regression checks. Synthetic managed benchmarks show reduced sparse-world traffic and allocations, not measured two-PC FPS. Selected native observations passed; full isolated campaign QA still reports bootstrap/runtime errors. Real two-PC gameplay validation is pending.

This is a development candidate, not full online parity. Shared statuses and encounters remain incomplete, including advanced consume/spread/proc behavior and some rewards. Host native actor scans and shared-TCP blocking remain potential bottlenecks.
