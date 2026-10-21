using System.Text.Json;
using LastEpochLAN.Core;

internal static class AutoPlayTests
{
    internal static void Run(Action<bool,string> check)
    {
        var settings=new AutoPlaySettings();
        check(settings.Skills.Length==5 && settings.Skills.All(r=>!r.Enabled) && !settings.Potion.Enabled,"autoplay starts with five disabled slots and potion disabled");
        var r=new AutoSkillRule {Enabled=true,Ability="buff"};
        var c=new AutoSkillContext("buff",80,80,true,15,30,true,true,false);
        check(AutoPlayRules.Blocked(r,c)=="","ready self skill without triggers repeats on readiness");
        check(AutoPlayRules.Blocked(r,c with {CooldownReady=false})!="","autoplay never bypasses native cooldown/charges");
        check(AutoPlayRules.Blocked(r,c with {Ability="other"})!="","swapping a skill never inherits an enabled slot rule");
        check(AutoPlayRules.Blocked(r,c with {HealthPercent=0})!="" && AutoPlayRules.Blocked(r,c with {HealthPercent=float.NaN})!="","dead or invalid HP cannot cast");
        r.EnemyNear=true;r.Radius=20;
        check(AutoPlayRules.Blocked(r,c)=="" && AutoPlayRules.Blocked(r,c with {EnemyDistance=21})!="","enemy trigger uses configured radius");
        r.EnemyNear=false;r.RareBossNear=true;
        check(AutoPlayRules.Blocked(r,c)!="" && AutoPlayRules.Blocked(r,c with {RareBossDistance=20})=="","normal nearby mobs do not satisfy rare/boss trigger");
        r.LowHealth=true;r.HealthPercent=50;
        check(AutoPlayRules.Blocked(r,c with {HealthPercent=45})=="","ANY allows emergency HP without nearby boss");
        r.RequireAll=true;
        check(AutoPlayRules.Blocked(r,c with {HealthPercent=45})!="" && AutoPlayRules.Blocked(r,c with {HealthPercent=45,RareBossDistance=10})=="","ALL requires HP and nearby boss together");
        r.RareBossNear=false;r.LowHealth=false;r.LowMana=true;r.ManaPercent=30;
        check(AutoPlayRules.Blocked(r,c with {ManaPercent=20})=="" && AutoPlayRules.Blocked(r,c with {ManaPercent=20,ManaKnown=false})!="","low MP trigger requires readable native MP");
        r.MinimumManaPercent=25;
        check(AutoPlayRules.Blocked(r,c with {ManaPercent=20})!="","minimum mana reserve overrides low MP trigger");
        r.MinimumManaPercent=0;r.LowMana=false;r.SelfTarget=false;
        check(AutoPlayRules.Blocked(r,c with {EnemyDistance=float.PositiveInfinity})!="","targeted skill requires actual enemy in range");
        r.TargetRareBoss=true;
        check(AutoPlayRules.Blocked(r,c)!="" && AutoPlayRules.Blocked(r,c with {RareBossDistance=5})=="","rare/boss target cannot fall back to nearby normal enemy");
        r.SelfTarget=true;r.BuffMissing=true;r.BuffName="WardBuff";
        check(AutoPlayRules.Blocked(r,c)=="" && AutoPlayRules.Blocked(r,c with {BuffActive=true})!="","named active buff prevents wasteful refresh");
        check(AutoPlayRules.Blocked(r,c with {BuffKnown=false})!="","unreadable buff state cannot be assumed missing");
        r.BuffName="";check(AutoPlayRules.Blocked(r,c)!="","empty buff name cannot enable a missing-buff loop");
        var p=new AutoPotionRule {Enabled=true,HealthPercent=45,Reserve=1};
        check(AutoPlayRules.Drink(p,true,45,3,1,10,10),"potion triggers at HP threshold with real available charges");
        check(!AutoPlayRules.Drink(p,true,46,3,1,10,10) && !AutoPlayRules.Drink(p,true,0,3,1,10,10),"potion does not drink above threshold or on dead character");
        check(!AutoPlayRules.Drink(p,true,20,1,1,10,10) && !AutoPlayRules.Drink(p,true,20,0,1,10,10),"potion respects reserve and empty slot");
        check(!AutoPlayRules.Drink(p,true,20,3,2,10,11) && !AutoPlayRules.Drink(p,false,20,3,1,10,0),"potion interval and readiness block repeated/menu use");
        check(!AutoPlayRules.Drink(p,true,float.NaN,3,1,10,0) && !AutoPlayRules.Drink(p,true,20,3,0,10,0),"invalid health and potion charge cost are rejected");
        p.Reserve=2;check(!AutoPlayRules.Drink(p,true,20,3,2,10,0),"multi-charge potions preserve configured remaining charges");
        settings.Skills[0]=new(){Enabled=true,Ability="buff",Interval=2};settings.Skills[1]=new(){Enabled=true,Ability="buff",Interval=1};
        var contexts=Enumerable.Repeat(c,5).ToArray();var schedule=new AutoPlaySchedule();
        check(schedule.Select(settings,contexts,10)==0,"autoplay selects first ready slot");
        schedule.Requested(0,10,2,true);
        check(schedule.Select(settings,contexts,10.1)==-1,"global dispatch spacing prevents same-frame overlapping casts");
        check(schedule.Select(settings,contexts,10.3)==1,"round robin allows next ready buff instead of starving it");
        schedule.Requested(1,10.3f,1,false);
        check(schedule.Select(settings,contexts,10.8)==-1 && schedule.Select(settings,contexts,11.1)==1,"rejected native requests back off before retrying");
        settings.Skills[1].Enabled=false;
        check(schedule.Select(settings,contexts,11.5)==-1 && schedule.Select(settings,contexts,12.1)==0,"per-skill repeat interval survives no-cooldown abilities");
        schedule.Reset();check(schedule.Select(settings,contexts,1)==0,"new character/session clears previous scheduling timestamps");
        settings.Skills=new[]{new AutoSkillRule {Radius=float.NaN,Interval=-10,Ability=new string('a',999)}};
        settings.Potion.Interval=float.PositiveInfinity;settings.Potion.Reserve=-5;settings.Normalize();
        check(settings.Skills.Length==5 && settings.Skills[0].Radius==20 && settings.Skills[0].Interval==0 && settings.Skills[0].Ability.Length==160 && settings.Potion.Interval==2 && settings.Potion.Reserve==0,"profile normalization bounds invalid values and restores five slots");
        var wardRule=new AutoSkillRule{Enabled=true,Ability="buff",LowWard=true,WardThreshold=500,Interval=0};
        check(AutoPlayRules.Blocked(wardRule,c with {Ward=500,WardKnown=true})=="" && AutoPlayRules.Blocked(wardRule,c with {Ward=501,WardKnown=true})!="","low ward includes exact absolute threshold and excludes higher ward");
        check(AutoPlayRules.Blocked(wardRule,c)!="" && AutoPlayRules.Blocked(wardRule,c with {Ward=float.NaN,WardKnown=true})!="","unknown or invalid ward never satisfies low ward");
        wardRule.LowHealth=true;wardRule.RequireAll=true;
        check(AutoPlayRules.Blocked(wardRule,c with {Ward=100,WardKnown=true})!="" && AutoPlayRules.Blocked(wardRule,c with {Ward=100,WardKnown=true,HealthPercent=40})=="","ALL applies to ward and HP together");
        wardRule.RequireAll=false;
        check(AutoPlayRules.Blocked(wardRule,c with {Ward=100,WardKnown=true})=="","ANY allows low ward with healthy HP");
        check(AutoPlayRules.ContinueHold(wardRule,c with {Ward=100,WardKnown=true,CooldownReady=false}),"held channel ignores only its already-consumed charge cooldown");
        check(!AutoPlayRules.ContinueHold(wardRule,c with {Ward=700,WardKnown=true}) && !AutoPlayRules.ContinueHold(wardRule,c with {Ward=100,WardKnown=true,Ability="other"}),"hold releases when conditions fail or equipped skill changes");
        wardRule.Interval=1;
        check(!AutoPlayRules.ContinueHold(wardRule,c with {Ward=100,WardKnown=true}),"positive repeat interval cannot sustain a hold");
        wardRule.Interval=0;wardRule.MinimumManaPercent=30;
        check(!AutoPlayRules.ContinueHold(wardRule,c with {Ward=100,WardKnown=true,ManaPercent=20}),"hold respects minimum current MP");
        var wardCopy=JsonSerializer.Deserialize<AutoSkillRule>(JsonSerializer.Serialize(wardRule))!;wardCopy.Normalize();
        check(wardCopy.Interval==0 && wardCopy.LowWard && wardCopy.WardThreshold==500,"zero hold interval and ward settings survive profile serialization");
        var copy=JsonSerializer.Deserialize<AutoPlaySettings>(JsonSerializer.Serialize(settings))!;copy.Normalize();
        check(copy.Skills[0].Ability==settings.Skills[0].Ability && !JsonSerializer.Serialize(settings).Contains("Armed"),"saved settings roundtrip without persisting START state");
    }
}
