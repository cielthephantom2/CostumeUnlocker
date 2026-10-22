using System.Text.Json;
using LastEpochLAN.Core;

internal static class AutoPlayTests
{
    internal static void Run(Action<bool,string> check)
    {
        var settings=new AutoPlaySettings();
        check(settings.Skills.Length==5 && settings.Skills.All(r=>!r.Enabled) && !settings.Potion.Enabled,"autoplay starts with five disabled slots and potion disabled");
        var scanSettings=new AutoPlaySettings();
        scanSettings.Skills[0]=new(){Enabled=true,SelfTarget=false,Radius=10};
        scanSettings.Skills[1]=new(){Enabled=true,SelfTarget=true,EnemyNear=true,Radius=20};
        scanSettings.Skills[2]=new(){Enabled=false,SelfTarget=false,Radius=30};
        scanSettings.Skills[3]=new(){Enabled=true,SelfTarget=true,Radius=30};
        check(AutoPlayRules.TargetScanRadius(scanSettings)==20,"target scan uses largest enabled target-dependent radius, ignoring disabled skills and unconditional self buffs");
        scanSettings.Skills[1].EnemyNear=false;scanSettings.Skills[1].RareBossNear=true;
        check(AutoPlayRules.TargetScanRadius(scanSettings)==20,"rare/boss proximity buffs participate in shared scan radius");
        scanSettings.Skills[0].Radius=60;
        check(AutoPlayRules.TargetScanRadius(scanSettings)==30,"old oversized profile cannot expand target scan beyond 30");
        scanSettings.Normalize();
        check(scanSettings.Skills[0].Radius==30,"profile normalization migrates previous 60-unit radius to 30");
        check(AutoPlayRules.WithinTargetRadiusSquared(900,30) && !AutoPlayRules.WithinTargetRadiusSquared(900.1f,30) &&
            AutoPlayRules.WithinTargetRadiusSquared(100,10) && !AutoPlayRules.WithinTargetRadiusSquared(100.1f,10),
            "squared range includes exact radius boundary and rejects enemies just outside each configured range");
        check(!AutoPlayRules.WithinTargetRadiusSquared(float.NaN,30) && !AutoPlayRules.WithinTargetRadiusSquared(float.PositiveInfinity,30) &&
            !AutoPlayRules.WithinTargetRadiusSquared(-1,30) && !AutoPlayRules.WithinTargetRadiusSquared(0,0),
            "invalid positions and inactive scan radius never admit a target");
        var limitedRule=new AutoSkillRule {Enabled=true,Ability="attack",SelfTarget=false,Radius=10};
        var limitedContext=new AutoSkillContext("attack",100,100,true,15,15,true,true,false);
        check(AutoPlayRules.Blocked(limitedRule,limitedContext)!="" && AutoPlayRules.Blocked(limitedRule,limitedContext with {EnemyDistance=10})=="",
            "shared scan radius cannot expand an individual skill target range");
        limitedRule.Radius=60;
        check(AutoPlayRules.Blocked(limitedRule,limitedContext with {EnemyDistance=31})!="",
            "cast guard also enforces maximum 30 before an oversized rule is normalized");
        foreach(var rule in scanSettings.Skills)rule.Enabled=false;
        scanSettings.Potion.Enabled=true;
        check(AutoPlayRules.TargetScanRadius(scanSettings)==0,"potion-only profile has no target scan radius");
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
        check(schedule.Select(settings,contexts,10.01)==-1,"global dispatch spacing prevents same-frame overlapping casts");
        check(schedule.Select(settings,contexts,10.3)==1,"round robin allows next ready buff instead of starving it");
        schedule.Requested(1,10.3f,1,false);
        check(schedule.Select(settings,contexts,10.4)==-1 && schedule.Select(settings,contexts,10.5)==1,"rejected native requests back off before retrying");
        settings.Skills[1].Enabled=false;
        check(schedule.Select(settings,contexts,11.5)==-1 && schedule.Select(settings,contexts,12.1)==0,"per-skill repeat interval survives no-cooldown abilities");
        schedule.Reset();check(schedule.Select(settings,contexts,1)==0,"new character/session clears previous scheduling timestamps");
        settings.Skills[0].Interval=120;schedule.Reset();schedule.Select(settings,contexts,10);schedule.Requested(0,10,120,true);
        check(schedule.WaitSeconds(0,11)==119,"Assist diagnostics distinguish configured repeat delay from blocked skill conditions");
        settings.Skills[0].Interval=0;
        check(schedule.Select(settings,contexts,11)==0,"changing repeat interval 120s to Hold immediately clears old per-skill timer");
        check(AutoPlayCadence.ReadFrameDue(false,true,10.001,10.025) && AutoPlayCadence.ReadFrameDue(false,true,10.008,10.025),
            "running Assist reads vitals/cooldowns every frame before old25ms deadline");
        check(!AutoPlayCadence.ReadFrameDue(true,false,10.001,10.1) && AutoPlayCadence.ReadFrameDue(true,false,10.1,10.1),
            "stopped F1 retains100ms configuration refresh");
        check(AutoPlayCadence.ReadFrameDue(true,true,10.001,10.1) && AutoPlayCadence.ReadFrameDue(true,true,10.008,10.1),
            "armed F1 reads vitals/cooldowns every frame while its panel stays open");
        check(!AutoPlayCadence.ReadFrameDue(true,true,double.NaN,0) && !AutoPlayCadence.ReadFrameDue(true,true,double.PositiveInfinity,0),
            "armed open panel still rejects invalid frame times");
        check(!AutoPlayCadence.ReadFrameDue(false,true,double.NaN,0) && !AutoPlayCadence.ReadFrameDue(false,true,double.PositiveInfinity,0),
            "invalid frame time never dispatches reads");
        check(AutoPlayCadence.RegistryInterval<.15f && AutoPlayCadence.TargetInterval<.05f,
            "registry and target selection cadences improve without per-frame registry scans");
        var channelSettings=new AutoPlaySettings();
        channelSettings.Skills[0]=new(){Enabled=true,Ability="buff",Interval=0};
        channelSettings.Skills[1]=new(){Enabled=true,Ability="buff",Interval=0};
        channelSettings.Skills[2]=new(){Enabled=true,Ability="buff",Interval=2,LowHealth=true,HealthPercent=50};
        var channelContexts=Enumerable.Repeat(c with {HealthPercent=40},5).ToArray();var channels=new AutoPlaySchedule();
        channels.Requested(0,10,0,true);
        check(channels.SelectWhileHolding(channelSettings,channelContexts,10.01,0)==-1,
            "channel priority preserves native dispatch spacing");
        check(channels.SelectWhileHolding(channelSettings,channelContexts,10.03,0)==2,
            "ready periodic defensive skill can yield an owned channel");
        channelContexts[2]=channelContexts[2] with {CooldownReady=false};
        check(channels.SelectWhileHolding(channelSettings,channelContexts,10.03,0)==-1,
            "cooldown prevents channel interruption and other Hold slots do not compete");
        channelContexts[2]=channelContexts[2] with {CooldownReady=true,HealthPercent=80};
        check(channels.SelectWhileHolding(channelSettings,channelContexts,10.03,0)==-1,
            "unmet defensive conditions do not interrupt channel");
        channelContexts[2]=channelContexts[2] with {HealthPercent=40};
        check(channels.Ready(channelSettings,channelContexts,10.03,2),
            "queued channel priority is revalidated before native cast");
        channels.Requested(2,10.03,2,true);channels.Observed(2,10.04,2);
        check(channels.SelectWhileHolding(channelSettings,channelContexts,11,0)==-1 && channels.SelectWhileHolding(channelSettings,channelContexts,12.05,0)==2,
            "periodic skill respects confirmed repeat timer while channel continues");
        channelSettings.Skills[2].Enabled=false;
        check(!channels.Ready(channelSettings,channelContexts,12.05,2),
            "disabling queued skill discards priority without bypassing user choice");
        check(!channels.Ready(channelSettings,channelContexts,12.05,-1) && !channels.Ready(channelSettings,channelContexts,12.05,5),
            "invalid queued slot is rejected");
        check(channels.Select(channelSettings,channelContexts,12.05)==0,
            "normal scheduler can resume zero-interval channel after periodic cast");
        var recovery=new AutoPlayRecovery();
        check(recovery.Failed(10,false) && recovery.ReadFailures==1 && recovery.RetryAt==10.25,
            "one transient native read failure retries with bounded delay instead of disarming Assist");
        recovery.Reset();
        check(recovery.ReadFailures==0 && recovery.RetryAt==0,"successful read/restart clears transient recovery state");
        check(recovery.Failed(20,false) && recovery.Failed(21,false) && recovery.Failed(22,false) && !recovery.Failed(23,false),
            "persistent native read failures stop after three retries, not an infinite loop");
        recovery.Reset();
        check(!recovery.Failed(30,true) && recovery.ReadFailures==0,
            "native action failure never auto-retries potentially already-executed cast/potion");
        settings.Skills[0].Interval=0;schedule.Reset();schedule.Requested(0,20,0,true);
        check(schedule.Select(settings,contexts,20.03)==0,"zero interval resumes after 25ms dispatch guard without a 200ms pause");
        schedule.Observed(0,20.04,0);
        check(schedule.Select(settings,contexts,20.04)==0,"native confirmation does not add a global gap to zero interval");
        settings.Skills[0].Interval=2;settings.Skills[1].Enabled=true;schedule.Requested(0,30,2,true);schedule.Observed(0,30.03,2);
        check(schedule.Select(settings,contexts,30.04)==1,"confirming one skill does not delay another ready skill");
        settings.Skills[1].Enabled=false;
        check(schedule.Select(settings,contexts,32.02)==-1 && schedule.Select(settings,contexts,32.04)==0,"positive interval still starts at confirmed native cast");
        check(AutoPlayRules.TrySliderValue("0",0,120,false,out var entered) && entered==0,"numeric slider input accepts zero hold interval");
        check(AutoPlayRules.TrySliderValue("1,25",0,120,false,out entered) && entered==1.25f && AutoPlayRules.TrySliderValue("1.25",0,120,false,out entered) && entered==1.25f,"numeric slider accepts decimal dot and comma consistently");
        check(AutoPlayRules.TrySliderValue("999",0,100,true,out entered) && entered==100 && AutoPlayRules.TrySliderValue("-5",0,100,true,out entered) && entered==0,"typed slider values clamp to the actual control bounds");
        check(!AutoPlayRules.TrySliderValue("NaN",0,100,false,out _) && !AutoPlayRules.TrySliderValue("Infinity",0,100,false,out _) && !AutoPlayRules.TrySliderValue("bad",0,100,false,out _) && !AutoPlayRules.TrySliderValue("",0,100,false,out _),"invalid numeric edits never overwrite a setting");
        check(AutoPlayRules.TrySliderValue("42.8",0,100,true,out entered) && entered==43,"whole-number controls round typed decimals");
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
