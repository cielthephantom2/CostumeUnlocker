namespace LastEpochLAN.Mod;

internal static class NativeGiftDataSmoke
{
    internal static void Run()
    {
        var source=Il2Cpp.ItemData.DummyData(9,0,2);
        source.RollIndividualID();
        source.affixes.Add(new Il2Cpp.ItemAffix(1,2,3,9,(Il2Cpp.SealedAffixType)0));
        source.SetAllImplicitRolls(3); source.RebuildID();
        var raw=source.GetIDAsArray().ToArray();
        var received=new Il2Cpp.ItemDataUnpacked(raw);
        if (!received.GetIDAsArray().ToArray().SequenceEqual(raw) || received.affixes.Count != source.affixes.Count ||
            received.affixes[0].affixId != 1 || received.affixes[0].affixTier != 2 || received.affixes[0].affixRoll != 3 ||
            received.getImplictRoll(0) != 3 || received.Implicits == null || received.Implicits.Count == 0)
            throw new InvalidOperationException($"Gift round trip: raw={received.GetIDAsArray().ToArray().SequenceEqual(raw)}; affixes={received.affixes.Count}/{source.affixes.Count}; implicitRoll={received.getImplictRoll(0)}; implicitCount={received.Implicits?.Count}; affix={(received.affixes.Count>0?$"{received.affixes[0].affixId}/{received.affixes[0].affixTier}/{received.affixes[0].affixRoll}":"none")}");
        MelonLoader.MelonLogger.Msg("Native Gift data QA: unpacked implicit values and affix id/tier/roll preserved.");
    }
}
