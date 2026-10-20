using Il2Cpp;
using LastEpochLAN.Core;
using Random = UnityEngine.Random;

namespace LastEpochLAN.Mod.OfflineTools;

// Isolated QA items only: exercise the installed native detour and native
// serialization on both sides of every threshold without touching inventory.
internal static class WovenPotentialNativeSmoke
{
    internal static object Run()
    {
        UniqueList.Entry entry=null;
        foreach (var candidate in UniqueList.instance.uniques)
            if (candidate!=null && !candidate.isSetItem && candidate.legendaryType==UniqueList.LegendaryType.LegendaryPotential && candidate.subTypes?.Count>0)
            {entry=candidate;break;}
        if (entry==null) throw new InvalidOperationException("No LP-capable unique for Woven QA.");
        var previousRandom=Random.state;
        var observations=new List<object>();
        try
        {
            foreach (int lp in new[]{1,2,3})
            foreach (int target in new[]{WovenPotentialRules.UpgradePercent(lp)-1,WovenPotentialRules.UpgradePercent(lp)})
            {
                int seed;
                for (seed=0;seed<8192;seed++)
                {
                    Random.InitState(seed);
                    if (Random.Range(0,100)==target) break;
                }
                if (seed==8192) throw new InvalidOperationException("No deterministic native Woven boundary seed found.");
                var source=ItemData.DummyData(entry.baseType,entry.subTypes[0],0);
                source.rarity=7;source.uniqueID=entry.uniqueID;source.legendaryPotential=(byte)lp;
                source.RollIndividualID();source.SetAllImplicitRolls(37);source.SetAllUniqueRolls(73);source.RebuildID();
                var bytes=source.GetIDAsArray().ToArray();
                var actual=new ItemDataUnpacked(bytes);
                var expected=new ItemDataUnpacked(bytes);
                int outcome=WovenPotentialRules.Roll(lp,target);
                expected.legendaryPotential=(byte)outcome;expected.RebuildID();expected.setValuesFromSerialisation();
                Random.InitState(seed);
                actual.RandomizeLegendaryPotential(null);
                var serialized=actual.GetIDAsArray().ToArray();
                var restored=new ItemDataUnpacked(serialized);
                if (actual.legendaryPotential!=outcome || restored.legendaryPotential!=outcome ||
                    !serialized.SequenceEqual(expected.GetIDAsArray().ToArray()) || actual.individualID!=source.individualID)
                    throw new InvalidOperationException($"Native Woven {lp} LP, roll {target}: result/serialization mismatch.");
                observations.Add(new {from=lp,percentile=target,to=outcome,serializedLp=(int)restored.legendaryPotential,otherItemDataPreserved=true});
            }
            return new {passed=true,observations,scope="Actual item detour and native serialization; no live reward panel, crafting-use consumption or inventory save tested"};
        }
        finally {Random.state=previousRandom;}
    }
}
