# Shade / Echo of a World — catch-up runaway hotfix

User reports corruption100 -> approximately71000 after the boss on another Client. The local running game log shows dev30, but is not the affected Client's log; local saves inspected do not contain that transition. Do not claim that inspecting this machine confirms the precise remote incident.

## Confirmed bug

`OfflineBonusRules.ShadeCorruption` previously multiplied the ENTIRE native positive delta by5, including catch-up. Native catch-up is convergence toward a known highest-C target; x5 overshoots that target. Visiting other lower timelines can then use the inflated highest value and magnify progression again.

Native `EchoWeb.GainCorruptionAfterCompletingShadeEcho` uses `SharedMonolithRunsData.highestCorruptionOfAnyRunInStash` for the Echo-of-a-World island path, and the character highest for the other Shade path. Thus the source target can come from stash-wide progression rather than the current timeline alone. The calculator receives the selected target as argument5.

Concrete pure-logic reproduction, not a reading of the remote save:

- Current100, native base gain6, highest15000.
- Native catch-up round((15000-106)*.95)=14149. Native result14255.
- Old mod100+(14255-100)*5 = **70875**.
- Fixed mod scales earned base6->30, then recomputes catch-up from remaining gap15000-130 using native95% float/banker's rounding; result stays below15000. It does not multiply convergence by5.

## Change

- Keep x5 for earned Shade base and Gaze only.
- Recompute catch-up after scaled earned gains against native argument5. Catch-up cannot itself raise the selected target; scaled earned rewards can legitimately exceed a previous highest.
- Preserve native losses/zero gain, difficulty max, clipped breakdowns and integer safety.
- Add completion-only diagnostic line with timeline, before/after, delta, Gaze before, highest character and highest stash. No per-preview/per-frame log or direct save edit.
- Native smoke candidate extended with high-target catch-up case, but NOT executed in this session because the user's game is running. Managed regression exercises the70k reproduction, repeated lower timelines and integer extremes.

## Limits / recovery

The exact remote root cause still needs that Client's log/progression data. Native tier rewards and native catch-up can be large independently; the fix removes the proven x5 catch-up feedback, not every possible large native increase.

This candidate does not lower any already persisted corruption/highest-in-stash value. A contaminated target can still be used by native catch-up until the affected saved progress is reviewed/recovered. Do not blindly divide71000 by5 or overwrite a live save. Request the affected Client's Latest.log and a copied/backed-up relevant character save before any recovery. Preserve inventory/stash and other legitimate timeline progression.

Integrated into dev31 alongside linear reward/Bloodrage work. Release overview: RELEASE-1.1.0-dev31-VI.md. No Steam installation or game launch/stop in this release task. Final build/test evidence: validation/managed-dev31-linear-corruption.json and validation/audit-dev31-linear-corruption.json; package/updater proofs: validation/release-dev31-*.
