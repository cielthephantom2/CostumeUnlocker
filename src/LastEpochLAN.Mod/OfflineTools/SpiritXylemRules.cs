namespace LastEpochLAN.Mod.OfflineTools;

internal static class SpiritXylemRules
{
    internal const ushort Id=445;
    internal const float SpellDamagePerMaximumMana=1f/25f;
    internal static int Attribute(float value)=>!float.IsFinite(value)?0:
        (int)Math.Clamp(Math.Round((double)value,MidpointRounding.ToEven),int.MinValue,int.MaxValue);
    internal const string EffectsText="+[3,4,5] to All Skills\n"+
        "+[10,16,6] Attunement\n"+
        "+[3,6,2] Health and Mana per Attunement\n"+
        "+1 Spell Damage per 25 Maximum Mana";
    internal static readonly string[] EffectLines=EffectsText.Split('\n');
    internal static readonly string[] EffectDetails={
        "Range: 3 to 4",
        "Range: 10 to 16",
        "Range: 3 to 6. Mana from this item stacks with the native +2 Mana per Attunement.",
        "Scales with your full maximum Mana, not your current Mana. 1000 maximum Mana grants +40 Spell Damage."
    };
}
