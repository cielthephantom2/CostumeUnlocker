namespace LastEpochLAN.Mod.OfflineTools;

internal static class SpiritXylemRules
{
    internal const ushort Id=445;
    internal enum Branch {None,Attunement,Intelligence}
    // Match the native core-attribute rounding before comparing. The caller
    // removes this item's own conditional source, preventing self-lock/flicker.
    internal static Branch Choose(float attunement,float intelligence)
    {
        int att=Attribute(attunement),intel=Attribute(intelligence);
        return att>intel?Branch.Attunement:intel>att?Branch.Intelligence:Branch.None;
    }
    internal static int Attribute(float value)=>!float.IsFinite(value)?0:
        (int)Math.Clamp(Math.Round((double)value,MidpointRounding.ToEven),int.MinValue,int.MaxValue);
    internal const string AttunementText="If Attunement is greater than Intelligence:\n"+
        "+[3,4,5] to Attunement Skills\n+[10,20,6] Attunement\n+[3,6,2] Health and Mana per Attunement";
    internal const string IntelligenceText="If Intelligence is greater than Attunement:\n"+
        "+[3,4,5] to Intelligence Skills\n+[10,20,6] Intelligence\n+1 Spell Damage per 2 Intelligence";
    internal const string ComparisonText="Attribute comparison excludes Spirit Xylem's own conditional attribute bonus. Neither branch applies when the attributes are equal.";
}
