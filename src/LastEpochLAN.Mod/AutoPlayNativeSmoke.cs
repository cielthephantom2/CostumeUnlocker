using Il2Cpp;
using UnityEngine;

namespace LastEpochLAN.Mod;

internal sealed partial class AutoPlayRuntime
{
    private int qaStage,qaSlot;
    private float qaDeadline,qaCaptureAt;
    private long qaBeforeCast;
    private bool qaCaptured;
    private Actor qaEnemy;
    private float qaNextAttempt;
    private string QaImage=>Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),"UserData","autoplay-panel-qa-"+Environment.ProcessId+".png");
    // Called only after NativeAssetProbe verifies the disposable sandbox profile.
    internal object NativeSmoke(Actor actor)
    {
        if(!Environment.GetCommandLineArgs().Contains("--lelan-autoplay-smoke"))throw new InvalidOperationException("Private QA switch required");
        if(qaStage==0)
        {
            if(actor.GetActorSync()?.actorVisuals?.HasBeenInitialized!=true || actor.chargeManager==null ||
                actor.stateController?.usingAbility?.TryCast<UsingAbilityPlayer>()==null || UIBase.instance==null)return null;
            Profile(actor.characterDataTracker.charData.Id);Scan();Read(actor,EpochInputManager.instance);
            qaSlot=Array.FindIndex(abilities,a=>a!=null && !actor.chargeManager.onCoooldown(a) && Ability.PlayersCanUseAbilityInCurrentScene(actor.gameObject,a));
            if(qaSlot<0)throw new InvalidOperationException("No native equipped ability ready in private character");
            var p=actor.GetComponent<HealthPotion>();if(p==null || p.maxCharges<=0 || p.chargeCost<=0)throw new InvalidOperationException("Native potion unavailable");
            float beforeHp=actor.health.currentHealth;int beforeCharges=p.currentCharges;
            try
            {
                actor.health.currentHealth=actor.health.maxHealth*0.25f;p.currentCharges=p.maxCharges;
                Settings.Potion.Enabled=true;Settings.Potion.HealthPercent=45;Settings.Potion.Reserve=0;
                localReady=true;Read(actor,EpochInputManager.instance);int count=p.currentCharges;
                if(!TryPotion(p,Time.unscaledTime) || p.currentCharges>=count || actor.health.currentHealth<=actor.health.maxHealth*0.25f)
                    throw new InvalidOperationException("Native automatic potion did not consume/heal");
                if(TryPotion(p,Time.unscaledTime))throw new InvalidOperationException("Potion repeat interval failed");
            }
            finally{actor.health.currentHealth=beforeHp;p.currentCharges=beforeCharges;Settings.Potion.Enabled=false;}
            Bind(qaSlot);Settings.Skills[qaSlot].Enabled=true;Settings.Skills[qaSlot].Interval=2;
            Settings.Skills[qaSlot].SelfTarget=false;Settings.Skills[qaSlot].EnemyNear=true;Settings.Skills[qaSlot].Radius=40;
            foreach(var data in ActorDataList.instance().data)
                if(data!=null && data.actorType==ActorData.Type.Normal)
                {
                    qaEnemy=data.spawn(actor.position()+actor.transform.forward*5,0,null,AbilitySync.PositionSyncType.Absolute,null,"",true,0,
                        new Il2CppSystem.Nullable<UnityEngine.SceneManagement.Scene>(actor.gameObject.scene),true);break;
                }
            if(qaEnemy==null)throw new InvalidOperationException("Native QA enemy could not spawn");
            qaBeforeCast=ConfirmedCasts;qaDeadline=Time.unscaledTime+30;qaStage=1;return null;
        }
        if(Time.unscaledTime>qaDeadline)throw new TimeoutException("AutoPlay native QA stage "+qaStage+": "+Status+" / "+Error+"; nativeVisible="+panelView?.Visible+" open="+Open+" captured="+qaCaptured+" exists="+File.Exists(QaImage)+" design="+System.Text.Json.JsonSerializer.Serialize(panelView?.DesignDiagnostics));
        if(qaStage==1)
        {
            if(ConfirmedCasts>qaBeforeCast){Open=true;tab=qaSlot;qaStage=2;return null;}
            if(Time.unscaledTime<qaNextAttempt)return null;
            if(!actor.stateController.IsIdle && !actor.stateController.IsMoving)return null;
            Scan();Read(actor,EpochInputManager.instance);
            if(nearest==null)return null;
            qaNextAttempt=Time.unscaledTime+2;
            if(!TryCast(actor,actor.stateController.usingAbility.TryCast<UsingAbilityPlayer>(),qaSlot,Time.unscaledTime))
                throw new InvalidOperationException("Native game rejected QA bar skill command: "+Status);
            return null;
        }
        if(qaStage==2)
        {
            if(panelView?.Visible!=true || !panelView.IconsReady)return null;
            if(!qaCaptured)
            {
                Canvas.ForceUpdateCanvases();panelView.VerifyClick();
                panelView.Capture(QaImage);qaCaptured=true;qaCaptureAt=Time.unscaledTime+2;tab=5;return null;
            }
            if(!qaCaptured || Time.unscaledTime<qaCaptureAt || !File.Exists(QaImage))return null;
            if(blockedInput!=null)throw new InvalidOperationException("F1 panel acquired a global gameplay input block");
            panelView.Capture(QaImage.Replace(".png","-potion.png"));
            Open=false;qaStage=3;return null;
        }
        if(blockedInput!=null)throw new InvalidOperationException("Closing F1 retained an Assist input block");
        Armed=false;
        if(qaEnemy!=null){HostEnemyReplicas.DestroyActor(qaEnemy);qaEnemy=null;}
        return new {passed=true,nativeAutoPlay=new {equippedSlotsRead=true,nativeCooldownRead=true,nativeManaHealthRead=true,
            potionChargesDetected=true,nativePotionConsumedAndHealed=true,potionIntervalBlockedRepeat=true,nativeSkillCommandAccepted=true,
            nativeLocalCastObserved=true,panelDoesNotBlockGameplayInput=true,nativeStartStopRaycast=true,skillAndPotionIconsLoaded=true,design=panelView.DesignDiagnostics,panelScreenshot=QaImage,potionScreenshot=QaImage.Replace(".png","-potion.png"),
            detectedKeys=keys,skill=names[qaSlot],Requests,ConfirmedCasts},
            scope="Disposable native Mage character: live bar/cooldown/potion reads, actual game cast and potion, panel input ownership and screenshot. Multi-class buffs and physical two-PC combat remain unverified."};
    }
}
