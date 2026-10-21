using LastEpochLAN.Core;

internal static class GlyphCraftingTests
{
    internal static void Run(Action<bool,string> check)
    {
        var expected=new[]{1f,0.70f,0.50f,0.33f};
        for(int tier=0;tier<4;tier++)
            check(GlyphCraftingRules.DespairChance(tier,0.01f)==expected[tier] &&
                GlyphCraftingRules.DespairChance(tier,0.95f)==expected[tier],
                $"Despair T{tier+1} uses requested fixed rate independently of native rarity/Exalted chance");
        check(GlyphCraftingRules.DespairChance(-1,0)==0 && GlyphCraftingRules.DespairChance(4,0)==0 &&
            GlyphCraftingRules.DespairChance(5,0)==0 && GlyphCraftingRules.DespairChance(6,0)==0,
            "Despair never opens sealing for T5/T6/T7 or invalid tiers");
        check(GlyphCraftingRules.Description(0,"").Contains("33%") &&
            GlyphCraftingRules.Description(3,"").Contains("T4 33%") && GlyphCraftingRules.Description(1,"Chaos")=="Chaos",
            "glyph descriptions match custom rates without changing other glyph text");
        float chance=0.25f;
        var rng=new IntPtr(100);
        using(var scope=new CraftingHopeScope(rng,true))
        {
            scope.Adjust(new IntPtr(101),ref chance);
            check(chance==0.25f,"Hope cannot change another RNG even during a Forge");
            float critical=0.12f;scope.Adjust(rng,ref critical);
            check(critical==0.12f,"Hope leaves native critical success and unrelated rolls unchanged");
            using(var nested=new CraftingHopeScope(rng,false))
            {
                CraftingHopeScope.Current!.Adjust(rng,ref chance);
                check(chance==0.25f,"non-Hope or nonlocal nested Forge masks an outer Hope scope");
            }
            check(ReferenceEquals(CraftingHopeScope.Current,scope),"nested Forge restores the caller's pending Hope roll");
            CraftingHopeScope.Current!.Adjust(rng,ref chance);
            check(chance==0.33f,"native local Hope probability becomes exactly 33 percent");
            chance=0.25f;scope.Adjust(rng,ref chance);
            check(chance==0.25f,"Hope replacement is consumed exactly once per Forge");
        }
        check(CraftingHopeScope.Current==null,"Forge finalization releases Hope scope");
        try {using var scope=new CraftingHopeScope(rng,true);throw new InvalidOperationException("fixture");}
        catch(InvalidOperationException){}
        check(CraftingHopeScope.Current==null,"exception cleanup cannot leave a Hope override active after crafting");
        using(var scope=new CraftingHopeScope(rng,true))
        {
            bool isolated=false;var worker=new Thread(()=>isolated=CraftingHopeScope.Current==null);worker.Start();worker.Join();
            check(isolated,"Hope scope does not leak to another thread");
        }
        using(var scope=new CraftingHopeScope(IntPtr.Zero,true))
        {
            chance=0.25f;scope.Adjust(IntPtr.Zero,ref chance);
            check(chance==0.25f,"missing crafting RNG cannot enable a broad Hope override");
        }
    }
}
