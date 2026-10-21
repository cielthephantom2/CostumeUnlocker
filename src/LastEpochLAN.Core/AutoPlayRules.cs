namespace LastEpochLAN.Core;

public sealed class AutoPlaySettings
{
    public AutoSkillRule[] Skills {get;set;}=Enumerable.Range(0,5).Select(_=>new AutoSkillRule()).ToArray();
    public AutoPotionRule Potion {get;set;}=new();
    public void Normalize()
    {
        Skills=Enumerable.Range(0,5).Select(i=>Skills!=null && i<Skills.Length ? Skills[i]??new() : new AutoSkillRule()).ToArray();
        foreach(var rule in Skills)rule.Normalize();
        Potion??=new();Potion.Normalize();
    }
}

public sealed class AutoSkillRule
{
    public bool Enabled {get;set;}
    public string Ability {get;set;}="";
    public bool SelfTarget {get;set;}=true;
    public bool TargetRareBoss {get;set;}
    public bool EnemyNear {get;set;}
    public bool RareBossNear {get;set;}
    public bool LowHealth {get;set;}
    public bool LowMana {get;set;}
    public bool LowWard {get;set;}
    public float WardThreshold {get;set;}=500;
    public bool RequireAll {get;set;}
    public float HealthPercent {get;set;}=50;
    public float ManaPercent {get;set;}=30;
    public float MinimumManaPercent {get;set;}
    public float Radius {get;set;}=20;
    public float Interval {get;set;}=1;
    public bool BuffMissing {get;set;}
    public string BuffName {get;set;}="";
    public void Normalize()
    {
        Ability=Trim(Ability);BuffName=Trim(BuffName);
        HealthPercent=Clamp(HealthPercent,1,100,50);ManaPercent=Clamp(ManaPercent,1,100,30);
        MinimumManaPercent=Clamp(MinimumManaPercent,0,100,0);Radius=Clamp(Radius,1,60,20);Interval=Clamp(Interval,0,120,1);
        WardThreshold=Clamp(WardThreshold,0,100000,500);
    }
    internal static string Trim(string? s)=>(s??"").Length>160?(s??"")[..160]:(s??"");
    internal static float Clamp(float v,float min,float max,float fallback)=>float.IsFinite(v)?Math.Clamp(v,min,max):fallback;
}

public sealed class AutoPotionRule
{
    public bool Enabled {get;set;}
    public float HealthPercent {get;set;}=45;
    public float Interval {get;set;}=2;
    public int Reserve {get;set;}
    public void Normalize()
    {
        HealthPercent=AutoSkillRule.Clamp(HealthPercent,1,100,45);
        Interval=AutoSkillRule.Clamp(Interval,0.5f,30,2);Reserve=Math.Clamp(Reserve,0,100);
    }
}

public readonly record struct AutoSkillContext(string Ability,float HealthPercent,float ManaPercent,bool ManaKnown,
    float EnemyDistance,float RareBossDistance,bool CooldownReady,bool BuffKnown,bool BuffActive,
    float Ward=0,bool WardKnown=false);

public static class AutoPlayRules
{
    // A running native channel already consumed its charge; its own cooldown
    // must not cancel the hold. All resource/target/trigger guards still apply.
    public static bool ContinueHold(AutoSkillRule r,AutoSkillContext c)=>
        r.Interval==0 && Blocked(r,c with {CooldownReady=true})=="";
    public static string Blocked(AutoSkillRule r,AutoSkillContext c)
    {
        if(!r.Enabled)return "Disabled";
        if(r.Ability.Length==0 || r.Ability!=c.Ability)return "Skill changed - bind this skill";
        if(!c.CooldownReady)return "Cooldown / no charges";
        if(!float.IsFinite(c.HealthPercent) || c.HealthPercent<=0)return "Character unavailable";
        if(r.MinimumManaPercent>0 && (!c.ManaKnown || !float.IsFinite(c.ManaPercent) || c.ManaPercent<r.MinimumManaPercent))return "Mana reserve";
        if(!r.SelfTarget && !((r.TargetRareBoss?c.RareBossDistance:c.EnemyDistance)<=r.Radius))return "No target in range";
        if(r.BuffMissing && (string.IsNullOrWhiteSpace(r.BuffName) || !c.BuffKnown))return "Select a readable buff name";
        if(r.BuffMissing && c.BuffActive)return "Buff already active";
        int count=0,matched=0;
        void Condition(bool enabled,bool match){if(enabled){count++;if(match)matched++;}}
        Condition(r.EnemyNear,c.EnemyDistance<=r.Radius);
        Condition(r.RareBossNear,c.RareBossDistance<=r.Radius);
        Condition(r.LowHealth,c.HealthPercent<=r.HealthPercent);
        Condition(r.LowMana,c.ManaKnown && float.IsFinite(c.ManaPercent) && c.ManaPercent<=r.ManaPercent);
        Condition(r.LowWard,c.WardKnown && float.IsFinite(c.Ward) && c.Ward>=0 && c.Ward<=r.WardThreshold);
        if(count>0 && (r.RequireAll?matched!=count:matched==0))return "Waiting for conditions";
        return "";
    }
    public static bool Drink(AutoPotionRule r,bool ready,float healthPercent,int charges,int chargeCost,double now,double next)=>
        r.Enabled && ready && float.IsFinite(healthPercent) && healthPercent>0 && healthPercent<=r.HealthPercent &&
        chargeCost>0 && charges>=chargeCost && charges-chargeCost>=r.Reserve && double.IsFinite(now) && now>=next;
}

// One request at a time; round robin prevents a spammable slot starving buffs.
public sealed class AutoPlaySchedule
{
    private readonly double[] next=new double[5];
    private readonly string[] identities=new string[5];
    private int cursor;
    private double globalNext;
    public void Reset(){Array.Clear(next,0,next.Length);Array.Clear(identities,0,identities.Length);cursor=0;globalNext=0;}
    public int Select(AutoPlaySettings settings,IReadOnlyList<AutoSkillContext> contexts,double now)
    {
        if(!double.IsFinite(now) || now<globalNext)return -1;
        for(int n=0;n<5;n++)
        {
            int i=(cursor+n)%5;
            if(i>=settings.Skills.Length || i>=contexts.Count)continue;
            if(identities[i]!=contexts[i].Ability){identities[i]=contexts[i].Ability;next[i]=0;}
            if(now>=next[i] && AutoPlayRules.Blocked(settings.Skills[i],contexts[i])=="")return i;
        }
        return -1;
    }
    public void Requested(int slot,double now,float interval,bool accepted)
    {
        next[slot]=now+(accepted?Math.Max(0,interval):0.75);globalNext=now+0.2;cursor=(slot+1)%5;
    }
}
