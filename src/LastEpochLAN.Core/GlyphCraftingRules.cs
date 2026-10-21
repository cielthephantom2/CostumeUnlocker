namespace LastEpochLAN.Core;

public static class GlyphCraftingRules
{
    public const float NativeHopeChance=0.25f, HopeChance=0.33f;
    // Native affix tiers are zero-based. Keep unsupported tiers native so this
    // never enables sealing T5/T6/T7 or changes crafting eligibility.
    public static float DespairChance(int nativeTier,float nativeChance)=>nativeTier switch
    {0=>1f,1=>0.70f,2=>0.50f,3=>0.33f,_=>nativeChance};
    public static string Description(int glyphSubtype,string original)=>glyphSubtype switch
    {
        0=>"Grants this craft a 33% chance to have no Forging Potential cost.",
        3=>"Chance to seal instead of upgrading: T1 100%, T2 70%, T3 50%, T4 33%. Same rates for Rare and Exalted items. Cannot seal T5 or higher.",
        _=>original
    };
}

// One native Hope Roll per local Forge, scoped to the crafting RNG. An inactive
// nested Forge masks its caller; finalizers restore the previous context.
public sealed class CraftingHopeScope : IDisposable
{
    [ThreadStatic] public static CraftingHopeScope? Current;
    private readonly CraftingHopeScope? previous;
    private readonly IntPtr rng;
    private bool pending,disposed;
    public CraftingHopeScope(IntPtr rng,bool eligible)
    {previous=Current;this.rng=rng;pending=eligible && rng!=IntPtr.Zero;Current=this;}
    public void Adjust(IntPtr source,ref float chance)
    {
        if(disposed || !pending || source!=rng || chance!=GlyphCraftingRules.NativeHopeChance)return;
        pending=false;chance=GlyphCraftingRules.HopeChance;
    }
    public void Dispose(){if(disposed)return;disposed=true;Current=previous;}
}
