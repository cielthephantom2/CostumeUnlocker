using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Il2Cpp;
using LastEpochLAN.Core;
using UnityEngine;

namespace LastEpochLAN.Mod;

internal sealed partial class AutoPlayRuntime
{
    internal static AutoPlayRuntime Active;
    private static readonly bool privateNativeQa=Environment.GetCommandLineArgs().Contains("--lelan-autoplay-smoke");
    internal AutoPlaySettings Settings=new();
    internal bool Open,Armed;
    internal string Status="Stopped - F1 to configure",Error="";
    internal long Requests,ConfirmedCasts,PotionsUsed;
    private readonly AutoPlaySchedule schedule=new();
    private readonly Ability[] abilities=new Ability[5];
    private readonly string[] keys=Enumerable.Repeat("?",5).ToArray();
    private readonly string[] names=Enumerable.Repeat("No skill",5).ToArray();
    private readonly float[] cooldowns=new float[5];
    private readonly int[] charges=new int[5];
    private readonly AutoSkillContext[] contexts=new AutoSkillContext[5];
    private readonly string[] slotStatus=Enumerable.Repeat("Waiting for character",5).ToArray();
    private readonly List<ActorVisuals> enemies=new();
    private readonly List<string> buffNames=new();
    private ActorVisuals nearest,rareBoss;
    private float nextRead,nextScan,nextBindings,nextTargets,nextBuffs,saveAt=-1,enemyDistance=float.PositiveInfinity,rareDistance=float.PositiveInfinity;
    private double nextPotion;
    private string character="",profilePath="",potionKey="1",scene="";
    private float hp,mp,ward;
    private UsingAbilityPlayer heldUse;
    private Ability heldAbility;
    private int heldSlot=-1;
    private float heldRequestedAt;
    private void ReleaseHeld()
    {
        var use=heldUse;var ability=heldAbility;heldUse=null;heldAbility=null;heldSlot=-1;
        // End only the channel we started; never cancel a replacement/manual cast.
        if(use!=null && ability!=null && use.ability?.Pointer==ability.Pointer && use.isUsingChannelledAbility())use.stopUsingAbility(true);
    }
    private int potionCharges,potionMax,potionCost=1;
    private bool localReady;
    private IntPtr actorPointer;
    private EpochInputManager blockedInput;
    private bool previousDisableInput;
    private void InputBlock(EpochInputManager input,bool block)
    {
        if(blockedInput!=null && (!block || input==null || input.Pointer!=blockedInput.Pointer))
        {blockedInput.forceDisableInput=previousDisableInput;blockedInput=null;}
        if(block && input!=null && blockedInput==null)
        {blockedInput=input;previousDisableInput=input.forceDisableInput;input.forceDisableInput=true;}
    }
    internal object Diagnostics=>new {Open,Armed,Status,Error,Requests,ConfirmedCasts,PotionsUsed,hp,mp,potionCharges,potionMax,
        slots=Enumerable.Range(0,5).Select(i=>new {slot=i,key=keys[i],name=names[i],cooldown=cooldowns[i],charges=charges[i],status=slotStatus[i]}).ToArray()};
    private static string Root=>Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),"UserData","LastEpochLAN","AutoPlay");
    private static int ActionId(int i)=>i switch {0=>(int)EpochInputManager.InputActions.Ability1,1=>(int)EpochInputManager.InputActions.Ability2,
        2=>(int)EpochInputManager.InputActions.Ability3,3=>(int)EpochInputManager.InputActions.Ability4,_=>(int)EpochInputManager.InputActions.Ability5};
    internal void Changed()=>saveAt=Time.unscaledTime+0.5f;
    internal void Suspend(){ReleaseHeld();InputBlock(null,false);Open=false;Armed=false;localReady=false;panelView?.Tick();Status="Stopped - offline character required";}
    private void Save()
    {
        if(profilePath=="" || saveAt<0)return;
        Settings.Normalize();Directory.CreateDirectory(Root);
        File.WriteAllText(profilePath+".tmp",JsonSerializer.Serialize(Settings,new JsonSerializerOptions {WriteIndented=true}));
        File.Move(profilePath+".tmp",profilePath,true);saveAt=-1;
    }
    internal void Dispose(){ReleaseHeld();InputBlock(null,false);panelView?.Dispose();try{Save();}catch(Exception ex){MelonLoader.MelonLogger.Warning("Auto Play save: "+ex.Message);}if(Active==this)Active=null;}
    private void Profile(string id)
    {
        if(character==id)return;
        ReleaseHeld();Save();character=id;Armed=false;schedule.Reset();nextPotion=0;
        profilePath=Path.Combine(Root,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id)))+".json");
        Settings=new();
        try
        {
            if(File.Exists(profilePath))
            {
                if(new FileInfo(profilePath).Length>32768)throw new InvalidDataException("Auto Play profile exceeds 32 KiB");
                Settings=JsonSerializer.Deserialize<AutoPlaySettings>(File.ReadAllText(profilePath))??new();
            }
            Settings.Normalize();Error="";
        }
        catch(Exception ex){Settings=new();Error="Profile load failed: "+ex.Message;}
    }
    internal static void CastObserved(UsingAbility state,Ability ability)
    {
        var runtime=Active;if(runtime==null || !runtime.localReady || ability==null)return;
        var local=PlayerFinder.getPlayerActor();
        if(local?.stateController?.usingAbility?.Pointer!=state.Pointer)return;
        runtime.ConfirmedCasts++;
        for(int i=0;i<runtime.abilities.Length;i++)
            if(runtime.abilities[i]?.Pointer==ability.Pointer)
            {runtime.schedule.Observed(i,Time.unscaledTime,runtime.Settings.Skills[i].Interval);break;}
    }
    internal void Tick(bool otherPanel,bool transition)
    {
        Active=this;
        try
        {
            var input=EpochInputManager.instance;
            if(Input.GetKeyDown(KeyCode.F1) && input?.IsInputFieldActive!=true)Open=!Open;
            if(Open && Input.GetKeyDown(KeyCode.Escape)){Open=false;Changed();}
            if(saveAt>=0 && Time.unscaledTime>=saveAt)Save();
            if(!Open && !Armed && !privateNativeQa)
            {
                InputBlock(null,false);localReady=false;
                enemies.Clear();nearest=rareBoss=null;
                Status="Stopped - F1 to configure";
                return;
            }
            var actor=PlayerFinder.getPlayerActor();
            bool offline=!Il2CppEHG.Multiplayer.GameplayEnvironment.IsOnlinePlay;
            localReady=offline && actor!=null && actor.gameObject.activeInHierarchy && actor.HasMovedToPlayerSpawn &&
                actor.health?.currentHealth>0 && !actor.isDying(false) && actor.GetActorSync()?.actorVisuals?.HasBeenInitialized==true &&
                !Il2CppLE.UI.LoadingScreen.IsVisible && !transition;
            InputBlock(input,Open && localReady);
            if(!localReady)
            {
                if(actor==null || !offline){Armed=false;}
                enemies.Clear();nearest=rareBoss=null;Status="Paused - character/loading/transition";return;
            }
            string id=actor.characterDataTracker?.charData?.Id??"";
            if(id==""){Armed=false;Status="Waiting for character identity";return;}
            Profile(id);
            string nowScene=SceneList.GetCurrentSceneDetails()?.Name??"";
            if(actorPointer!=actor.Pointer || scene!=nowScene)
            {ReleaseHeld();actorPointer=actor.Pointer;scene=nowScene;enemies.Clear();nearest=rareBoss=null;nextScan=nextRead=nextTargets=nextBuffs=0;schedule.Reset();}
            if(Time.unscaledTime>=nextScan){Scan();nextScan=Time.unscaledTime+0.15f;}
            if(Time.unscaledTime<nextRead)return;
            nextRead=Time.unscaledTime+(Open?0.1f:0.025f);
            Read(actor,input);
            var ui=UIBase.instance;
            bool paused=Open || otherPanel || !Application.isFocused || Time.timeScale<=0 || input==null ||
                input.forceDisableInput || input.cinematicActive || input.IsInputFieldActive || input.ButtonPressBlocked ||
                ui==null || ui.menuOpen || ui.IsChatInputFocused || ui.AnyPanelOpened();
            if(!Armed){Status="Stopped - configure skills, then Start";return;}
            if(paused){Status="Paused - panel/chat/focus";return;}
            var potion=actor.GetComponent<HealthPotion>();
            double now=Time.unscaledTime;
            if(AutoPlayRules.Drink(Settings.Potion,localReady,hp,potionCharges,potionCost,now,nextPotion) && potion!=null)
            {
                TryPotion(potion,now);
                return;
            }
            if(heldSlot>=0)
            {
                var rule=Settings.Skills[heldSlot];
                bool ownsCast=heldUse!=null && heldAbility!=null && heldUse.ability?.Pointer==heldAbility.Pointer && abilities[heldSlot]?.Pointer==heldAbility.Pointer;
                // Some channels become active only after their initial cast animation.
                // Keep the accepted request through that wind-up instead of losing ownership.
                if(ownsCast && !heldUse.isAbilityUseRestricted && !heldUse.hasCast && !actor.stateController.IsIdle && !actor.stateController.IsMoving &&
                    now-heldRequestedAt<1 && AutoPlayRules.ContinueHold(rule,contexts[heldSlot]) && !input.IsHoldingAbilityButton())
                {Status="Starting: "+names[heldSlot];return;}
                bool same=ownsCast && heldUse.isUsingChannelledAbility();
                if(same && !heldUse.isAbilityUseRestricted && AutoPlayRules.ContinueHold(rule,contexts[heldSlot]) && !input.IsHoldingAbilityButton())
                {
                    var target=rule.TargetRareBoss?rareBoss:nearest;
                    if(rule.SelfTarget || EnemyAlive(target,actor.GetActorSync()?.actorVisuals?.alignment))
                    {heldUse.TryUpdateTargetLocationIfChannelling(rule.SelfTarget?actor.position():target.transform.position);Status="Holding: "+names[heldSlot];return;}
                }
                ReleaseHeld();
            }
            var state=actor.stateController;
            var use=state?.usingAbility?.TryCast<UsingAbilityPlayer>();
            if(use==null || use.isAbilityUseRestricted || (!state.IsIdle && !state.IsMoving) || input.IsHoldingAbilityButton())
            {Status="Waiting - manual skill / cast / control state";return;}
            int slot=schedule.Select(Settings,contexts,now);
            if(slot<0){Status="Running - waiting for skill conditions";return;}
            TryCast(actor,use,slot,now);
        }
        catch(Exception ex)
        {
            InputBlock(null,false);Armed=false;Status="Stopped - native read/action failed";
            string message=ex.GetBaseException().Message;if(Error!=message)MelonLoader.MelonLogger.Warning("Auto Play: "+message);Error=message;
        }
        finally
        {
            try
            {
                var input=EpochInputManager.instance;var ui=UIBase.instance;
                if(!Armed || Open || otherPanel || transition || !localReady || !Application.isFocused || Time.timeScale<=0 ||
                    input==null || input.forceDisableInput || input.cinematicActive || input.IsInputFieldActive || input.ButtonPressBlocked ||
                    ui==null || ui.menuOpen || ui.IsChatInputFocused || ui.AnyPanelOpened())ReleaseHeld();
            }
            catch{heldUse=null;heldAbility=null;heldSlot=-1;}
            try{panelView??=new(this);panelView.Tick();}
            catch(Exception ex){Armed=false;Error="Panel: "+ex.Message;}
        }
    }
    private bool TryPotion(HealthPotion potion,double now)
    {
        if(!AutoPlayRules.Drink(Settings.Potion,localReady,hp,potion.currentCharges,potion.chargeCost,now,nextPotion))return false;
        nextPotion=now+Settings.Potion.Interval;
        if(!potion.UsePotion())return false;
        PotionsUsed++;Status="Potion used";return true;
    }
    private bool TryCast(Actor actor,UsingAbilityPlayer use,int slot,double now)
    {
            var rule=Settings.Skills[slot];var ability=abilities[slot];
            if(ability==null || AutoPlayRules.Blocked(rule,contexts[slot])!="")return false;
            var enemy=rule.TargetRareBoss?rareBoss:nearest;
            if(!rule.SelfTarget && !EnemyAlive(enemy,actor.GetActorSync()?.actorVisuals?.alignment))return false;
            var target=rule.SelfTarget?actor.position():enemy.transform.position;
            var transform=rule.SelfTarget?actor.transform:enemy.transform;
            if(actor.chargeManager.onCoooldown(ability) || !Ability.PlayersCanUseAbilityInCurrentScene(actor.gameObject,ability))return false;
            // Programmatic single press: -1 avoids tying the command's lifetime
            // to a physical Rewired key that is not actually being held.
            bool accepted=use.UseAbilityCommand(ability,slot,target,transform,-1,true,false,false,false);
            if(accepted && rule.Interval==0 && use.ability?.Pointer==ability.Pointer)
            {heldUse=use;heldAbility=ability;heldSlot=slot;heldRequestedAt=Time.unscaledTime;}
            Requests++;schedule.Requested(slot,now,rule.Interval,accepted);
            Status=(accepted?"Cast requested: ":"Game rejected: ")+names[slot];
            return accepted;
    }
    private void Scan()
    {
        enemies.Clear();var groups=ActorManager.instance?.visuals;if(groups==null)return;
        foreach(var group in groups)
        {
            var visuals=group?.visuals;if(visuals==null)continue;
            foreach(var visual in visuals)if(visual!=null && !visual.isPlayer)enemies.Add(visual);
        }
    }
    private static bool EnemyAlive(ActorVisuals v,Alignment local)=>v!=null && v.isActiveAndEnabled && !v.isPlayer && !v.dead && !v.isCorpse &&
        v.actorSync?.IsDead!=true && v.alignment!=null && local!=null && v.alignment.IsFoe(local);
    private void Read(Actor actor,EpochInputManager input)
    {
        hp=100*Mathf.Clamp01(actor.health.currentHealth/actor.health.maxHealth);
        bool manaKnown=actor.mana!=null && actor.mana.maxMana>0;
        mp=manaKnown?100*Mathf.Clamp01(actor.mana.currentMana/actor.mana.maxMana):0;
        var protection=actor.GetComponent<ProtectionClass>();ward=protection?.CurrentWard??0;
        var potion=actor.GetComponent<HealthPotion>();potionCharges=potion?.currentCharges??0;potionMax=potion?.maxCharges??0;potionCost=potion?.chargeCost??1;
        if(Time.unscaledTime>=nextTargets)
        {
            nextTargets=Time.unscaledTime+0.05f;nearest=rareBoss=null;enemyDistance=rareDistance=float.PositiveInfinity;
            var local=actor.GetActorSync()?.actorVisuals?.alignment;var origin=actor.position();
            foreach(var v in enemies)
            {
                if(!EnemyAlive(v,local) || v.gameObject.scene!=actor.gameObject.scene)continue;
                float d=Vector3.Distance(origin,v.transform.position);
                if(d<enemyDistance){enemyDistance=d;nearest=v;}
                var kind=v.BaseDisplayInformation?.actorClass;
                if((kind==DisplayActorClass.Rare || kind==DisplayActorClass.Boss) && d<rareDistance){rareDistance=d;rareBoss=v;}
            }
        }
        var buffs=actor.statBuffs?.buffs;
        if(Time.unscaledTime>=nextBuffs)
        {
            nextBuffs=Time.unscaledTime+0.1f;buffNames.Clear();
            if(buffs!=null)foreach(var buff in buffs)if(buff!=null && buff.remainingDuration>0 && !string.IsNullOrWhiteSpace(buff.name) && !buffNames.Contains(buff.name))buffNames.Add(buff.name);
        }
        var use=actor.stateController?.usingAbility?.TryCast<UsingAbilityPlayer>();
        for(int i=0;i<5;i++)
        {
            var a=use?.GetNonBufferableAbility(BufferableAbilityType.Bar,i);
            if(a!=null && (string.Equals(a.name,"NullAbility",StringComparison.OrdinalIgnoreCase) || string.Equals(a.abilityName,"NullAbility",StringComparison.OrdinalIgnoreCase)))a=null;
            abilities[i]=a;
            names[i]=a==null?"No skill":string.IsNullOrWhiteSpace(a.abilityName)?a.name:a.abilityName;
            cooldowns[i]=a==null?0:actor.chargeManager?.remainingCooldown(a)??0;
            charges[i]=a==null?0:actor.chargeManager?.getCharges(a)??0;
            var rule=Settings.Skills[i];bool buffKnown=buffs!=null;
            bool hasBuff=buffNames.Any(n=>string.Equals(n,rule.BuffName,StringComparison.OrdinalIgnoreCase));
            contexts[i]=new(a?.name??"",hp,mp,manaKnown,enemyDistance,rareDistance,
                a!=null && actor.chargeManager!=null && !actor.chargeManager.onCoooldown(a),buffKnown,hasBuff,ward,protection!=null);
            slotStatus[i]=a==null?"No skill":AutoPlayRules.Blocked(rule,contexts[i]);
            if(slotStatus[i]=="")slotStatus[i]="Ready";
        }
        if(input!=null && Time.unscaledTime>=nextBindings)
        {
            nextBindings=Time.unscaledTime+1;
            for(int i=0;i<5;i++)keys[i]=input.GetButtonNameByActionId(ActionId(i))??"?";
            potionKey=input.GetButtonNameByActionId((int)EpochInputManager.InputActions.UsePotion)??"1";
        }
    }
    internal void Bind(int slot)
    {
        Settings.Skills[slot].Ability=abilities[slot]?.name??"";Settings.Skills[slot].Enabled=false;Changed();
    }
}
