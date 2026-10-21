using LastEpochLAN.Core;

internal static class LinearCorruptionTests
{
    internal static void Run(Action<bool, string> check)
    {
        static bool Near(double a, double b) => Math.Abs(a-b) < .0002 * Math.Max(1, Math.Abs(b));
        foreach (int c in new[] {100, 200, 300, 500, 700, 1000, 1001, 10000, 1000000})
        {
            double n = c / 100d;
            check(Near(LinearCorruptionRules.Experience(c), n) &&
                Near(LinearCorruptionRules.PotentialMultiplier(c)-1, (LinearCorruptionRules.PotentialMultiplier(100)-1)*n) &&
                Near(LinearCorruptionRules.WillMultiplier(c)-1, (LinearCorruptionRules.WillMultiplier(100)-1)*n) &&
                Near(LinearCorruptionRules.EchoRarity(c), LinearCorruptionRules.EchoRarity(100)*n),
                $"Corruption {c}: EXP, LP/WW roll bonus and Echo rarity scale linearly without old thresholds");
            float legacy = OfflineBonusRules.GoldMultiplier(true,c);
            float correction = LinearCorruptionRules.GoldMultiplier(c) / LinearCorruptionRules.NativeGoldMultiplier(c);
            check(Near(correction*legacy*LinearCorruptionRules.NativeGoldMultiplier(c), (1+.0012*c)*legacy) && correction>=1,
                $"Corruption {c}: remove native gold diminishing WITHOUT reducing the existing /40 bonus");
            float nativeAmber = 6+Math.Min(c,500)*.01f+Math.Max(c-500,0)*.001f;
            check(Near(nativeAmber*LinearCorruptionRules.AmberCorrection(c,6,.01f,.001f),6+.01*c),
                $"Corruption {c}: Amber retains early slope above 500");
        }
        check(LinearCorruptionRules.Experience(0)==0 && LinearCorruptionRules.EchoRarity(-100)==0 &&
            LinearCorruptionRules.PotentialMultiplier(-1)==1 && LinearCorruptionRules.WillMultiplier(0)==1 &&
            LinearCorruptionRules.GoldMultiplier(0)==1, "zero/negative Corruption adds no linear bonus");
        check(Near(LinearCorruptionRules.PotentialMultiplier(100),1+.166/Math.Log(10)) &&
            Near(LinearCorruptionRules.WillMultiplier(100),1+.2/Math.Log(10)) &&
            Near(LinearCorruptionRules.GoldMultiplier(100),1.12) &&
            Near(LinearCorruptionRules.EchoRarity(100),.89655626), "new linear slopes do not nerf old 100-C rewards");
        bool noNerf=true;
        for(int c=1;c<=10000;c++)
        {
            double x=.5*c;
            double f=x<=100 ? x*(.088055804*Math.Pow(Math.Log(101),4.6)/100) : .088055804*Math.Pow(Math.Log(1+x),4.6);
            double nativeRarity=.0165*Math.Pow(f,.88);
            noNerf &= LinearCorruptionRules.Experience(c)+.00001>=LinearCorruptionRules.NativeExperience(c) &&
                LinearCorruptionRules.GoldMultiplier(c)+.00001>=LinearCorruptionRules.NativeGoldMultiplier(c) &&
                LinearCorruptionRules.PotentialMultiplier(c)+.00001>=1+.166*Math.Log10(1+c/100d) &&
                LinearCorruptionRules.WillMultiplier(c)+.00001>=1+.2*Math.Log10(1+c/100d) &&
                LinearCorruptionRules.EchoRarity(c)+.00001>=nativeRarity;
        }
        check(noNerf,"all integer Corruption 1..10000: EXP/gold/LP/WW/Echo rarity never lower than current formulas");
        check(Near(LinearCorruptionRules.NativeExperience(100),.6) && Near(LinearCorruptionRules.NativeExperience(500),3.36) &&
            Near(LinearCorruptionRules.NativeExperience(1000),4.77), "inlined EXP removal matches pinned native rounded values");
        check(Near(LinearCorruptionRules.NativeGoldMultiplier(201),1.24) &&
            Near(LinearCorruptionRules.NativeGoldMultiplier(501),1.42) &&
            Near(LinearCorruptionRules.NativeGoldMultiplier(1001),1.48), "native gold inverse retains integer truncation at branch edges");
        check(Near((12+500*.04+200*.002)*LinearCorruptionRules.AmberCorrection(700,12,.04f,.002f),12+700*.04),
            "Amber normalization honors actual asset baseline/slopes rather than hardcoding constructor defaults");
        check(LinearCorruptionRules.AmberCorrection(1000,6,.001f,.01f)>=1,
            "Amber never nerfs an asset variant that already has a higher late slope");
        foreach (var fixture in new[] {(54,0f,5),(55,0f,6),(82,39f,7),(82,40f,8)})
            check(LinearCorruptionRules.RollWill(fixture.Item1,0,0,fixture.Item2,_=>false,(a,b)=>a)==fixture.Item3,
                $"WW preserves native level/Corruption minimum gate {fixture.Item1}/{fixture.Item2}");
        var calls = new List<float>();
        int result=LinearCorruptionRules.RollWill(100,0,0,1000,p=>{calls.Add(p);return true;},(a,b)=>b-1);
        check(result==28 && calls.Count==8 && Near(calls[0],1.8) && Near(calls[1],.96*LinearCorruptionRules.WillMultiplier(1000)) &&
            Near(calls[2],.3) && Near(calls[3],.08) && calls.Skip(4).All(x=>x==.1f),
            "WW changes only Corruption promotion roll; preserves RNG order, four bonus rolls and max28");
        check(LinearCorruptionRules.RollWill(1,0,20,0,_=>false,(a,b)=>a)==20,
            "WW retains caller-provided minimum independently of Corruption");
        float modOnly=.75f;
        float current=modOnly+LinearCorruptionRules.NativeExperience(1000);
        float final=current+LinearCorruptionRules.Experience(1000)-LinearCorruptionRules.NativeExperience(1000);
        check(Near(final,10.75), "real EXP correction preserves unrelated monster-mod EXP bonus");
    }
}
