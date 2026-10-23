namespace LastEpochLAN.Mod.OfflineTools;

internal static class SpiritXylemRules
{
    internal const ushort Id=445;
    internal static int Attribute(float value)=>!float.IsFinite(value)?0:
        (int)Math.Clamp(Math.Round((double)value,MidpointRounding.ToEven),int.MinValue,int.MaxValue);
    internal const string EffectsText="+[3,4,5] to All Skills\n"+
        "Your Attunement is equal to your Intelligence\n"+
        "+[10,20,6] Intelligence\n"+
        "+[3,6,2] Health and Mana per Attunement\n"+
        "+1 Spell Damage per Intelligence";
    internal static readonly string[] EffectLines=EffectsText.Split('\n');
    internal const string DetailsText="Attunement is set to your final Intelligence, including Spirit Xylem's Intelligence bonus.";
}
