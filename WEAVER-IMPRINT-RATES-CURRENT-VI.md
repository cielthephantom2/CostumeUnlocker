# Weaver Imprint — tỷ lệ native, 2026-10-21

Native baseline audit of installed game metadata/native code. **Update: dev.34 now applies x2 to these six Imprint effects; see [implementation and validation](WEAVER-IMPRINT-X2-dev34-VI.md). The combined dev.34/protocol41 release is documented in RELEASE-1.1.0-dev34-VI.md.** The table below records original native values before the multiplier.

## Node effects and rates

| Node / effect | Confirmed native rate |
|---|---|
| Commoner's Riches / effect41 `ImprintContainerEnemiesDropMoreItemsOfType` | Boolean unlock. ChanceForEnemiesToDropPileOfSimilarItems = .00125 × totalSpawners / max(totalEnemies,1) × M(C), before item eligibility/selection. Not a fixed per-kill percent independent of map state. |
| Sealed Riches / effect42 `ImprintContainerCachesDropMoreItemsOfType` | OnCacheOpenedChanceForSimilarItems = .029 × points, i.e. 2.9% per allocated point, for the eligible cache mechanism. |
| Echoing Riches / effect43 `ImprintContainerEchoCompletionsDropMoreItemsOfType` | Boolean unlock. Actual reward claim's TryDropSimilarItemsToImprintedWeaverTreeItem rolls .00125 × EchoCompletionReward.enemyScore × M(C). Not a universal fixed 50% proc. |
| Warlord's Riches / effect44 `ImprintContainerChampionsDropMoreItemsOfType` | ImprintContainerChampionsChanceToDropMoreItemsOfType = .03 × points, i.e. 3% per allocated point. |
| Woven Riches / effect45 `ImprintContainerSpawnMoreWovenEchoesOfType` | Boolean unlock, no per-point fixed proc probability. SetNaturalSpawnedWovenEchoIslandData reads the imprinted Woven Echo's GetTotalNaturalSpawnWeighting plus eligibility including minimum Corruption. |
| Loomed Riches / effect46 `ImprintContainerSpawnMoreWovenEchoesOfTypeSecond` | Second imprint slot, same weighting-based mechanism. No universal fixed percentage established. |

Displayed node labels are conventional English names; executable metadata identifies them by the effects above. This audit has not extracted the current localized tree assets or node point caps.

M(C) = 1.2 - .6 × exp(-.6931 × C/100), asymptotically approaches1.2. Approx values C100=.900, C300=1.125, C500=1.181, C700=1.195, C1000=1.199. The mod's dev31 removal of EXP/Gold/Amber/LP/WW/EchoRewardRarity diminishing does not modify this separate curve.

## Evidence

- TheWeaver.UpdateWeaverTreeNode RVA0x20E3E70 jump table at0x20E4824 maps effect41..46 to blocks0x20E4032,0x20E4043,0x20E4060,0x20E4071,0x20E408E,0x20E409F.
- Effect42 multiplier constant at0x4723DAC=.029; effect44 at0x456E2F8=.03. Metadata also supplies named per-point constants.
- MonolithZoneManager.ChanceForEnemiesToDropPileOfSimilarItems RVA0x12FE060 uses fields totalEnemies0x194 and totalSpawners0x198 and constant.00125.
- MonolithItemManager.TryDropSimilarItemsToImprintedWeaverTreeItem RVA0x1D85A80 reads EchoCompletionReward.enemyScore at0x20, corruption at0x1C, calls SimilarItemDropMultiplierFromCorruption and rolls their product with.00125.
- WeaverTreeNodesExtensions.SetNaturalSpawnedWovenEchoIslandData RVA0x20EEC60 checks both imprint-slot flags, WovenEchoData.GetTotalNaturalSpawnWeighting and eligibility. No fixed proc rate asserted for these slots.

These are activation probabilities/weights, not guaranteed chances to receive the exact imprinted Unique or affix configuration. Subsequent item/container eligibility and SimilarItem generation remain separate. No gameplay process was started or stopped.
