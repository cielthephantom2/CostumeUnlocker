using Il2Cpp;
using UnityEngine;

namespace LastEpochLAN.Mod;

// Invoked only after NativeAssetProbe verifies the disposable sandbox profile.
internal sealed class NativeDisintegrateProbe
{
    internal static readonly bool Enabled=Environment.GetCommandLineArgs().Contains("--lelan-disintegrate-smoke");
    internal static bool Running,IgnoreAimFilter;
    internal static ModifyDamageOnRotation LastRotation;
    private int stage;
    private float until,deadline,nextAim,nextSample;
    private Ability ability;
    private Vector3 origin,aim,moveOrigin,stationaryOrigin;
    private float baselineDrift,stationaryDrift,movedDistance,manaBefore,manaAfter;
    private bool accepted,channelObserved,moveAccepted,channelDuringMove,beamAimUpdated;
    private int keyboardIndex;
    private Vector3 keyboardOrigin,keyboardDirection,releaseOrigin;
    private readonly List<object> keyboardSamples=new();
    private bool keyboardPassed=true;
    private float releaseDrift;
    private bool rotationPenaltyRemoved;
    private float originalRotationPenalty;
    private ArcaneAscendanceMutator ascendance;
    private bool ascendanceAccepted,ascendanceRootObserved,ascendanceChannelPreserved;
    private float ascendanceDrift;
    private Vector3 ascendanceOrigin;
    private bool movementResumed;
    private static readonly Vector2[] KeyboardDirections={Vector2.up,Vector2.down,Vector2.left,Vector2.right,new(1,1),new(-1,-1)};
    private readonly List<object> samples=new();
    internal object Tick(Actor actor)
    {
        if(!Enabled)throw new InvalidOperationException("Private Disintegrate QA switch required");
        var use=actor.stateController?.usingAbility?.TryCast<UsingAbilityPlayer>();
        if(use==null || actor.GetActorSync()?.actorVisuals?.HasBeenInitialized!=true)return null;
        if(stage==0)
        {
            ability=AbilityManager.get().playerAbilities.ToArray().FirstOrDefault(a=>a!=null &&
                string.Equals(a.name,"Disintegrate",StringComparison.OrdinalIgnoreCase));
            if(ability==null)throw new InvalidOperationException("Disintegrate player asset missing");
            while(actor.experienceTracker.CurrentLevel<60)actor.experienceTracker.LevelUp(true);
            use.abilityList.setAbility(ability,4,true,true);
            actor.mana.maxMana=10000;actor.mana.currentMana=10000;
            var input=EpochInputManager.instance??throw new InvalidOperationException("QA input manager missing");
            // The disposable bootstrap does not always bind the shell input
            // manager to the newly spawned actor. Bind only this QA actor.
            var thumb=actor.stateController.states.ToArray().Select(s=>s.TryCast<MovingPlayerThumbstick>()).FirstOrDefault(s=>s!=null)
                ??actor.GetComponent<MovingPlayerThumbstick>();
            if(thumb==null)throw new InvalidOperationException("QA actor keyboard movement state missing");
            input.thumbstickPlayerMovement=thumb.Cast<IThumbstickPlayerMovement>();
            input.movementProcessor??=new MovementProcessor();
            input.movementProcessor.OnPlayerChanged(actor.gameObject);
            Running=true;
            until=Time.unscaledTime+2;deadline=Time.unscaledTime+70;stage=1;return null;
        }
        if(Time.unscaledTime>deadline)return Finish(use,"Timed out");
        if(stage==1)
        {
            if(Time.unscaledTime<until)return null;
            origin=actor.position();aim=origin+actor.transform.forward*7;
            manaBefore=actor.mana.currentMana;
            accepted=use.UseAbilityCommand(ability,4,aim,null,-1,true,false,false,false);
            // A/B control: reproduce dev.18 by letting target updates steer
            // movement, then enable the new filter in the same native channel.
            IgnoreAimFilter=true;stage=2;until=Time.unscaledTime+4;
        }
        if(stage is 2 or 3 or 4 or 5 or 7 or 8 or 9 or 10 or 11 or 12)
        {
            channelObserved|=use.channelling;
            if(use.channelling && Time.unscaledTime>=nextAim)
            {
                nextAim=Time.unscaledTime+0.1f;
                use.TryUpdateTargetLocationIfChannelling(aim);
                use.UpdateTargetLocation(aim);
                beamAimUpdated|=Vector3.Distance(use.targetLocation,origin)>1;
            }
            Sample(actor,use);
        }
        if(stage==2)
        {
            if(LastRotation!=null)
            {
                originalRotationPenalty=OfflineTools.DisintegrateRotationPenaltyPatch.LastOriginalPenalty;
                rotationPenaltyRemoved=originalRotationPenalty<0 && LastRotation.damageModifierPerDegree==0;
            }
            baselineDrift=Math.Max(baselineDrift,Vector3.Distance(origin,actor.position()));
            if(Time.unscaledTime<until)return null;
            IgnoreAimFilter=false;
            use.MovementFromAbility.UpdateDestination(actor.position());
            until=Time.unscaledTime+1;stage=3;return null;
        }
        if(stage==3)
        {
            if(Time.unscaledTime<until)return null;
            stationaryOrigin=actor.position();
            aim=stationaryOrigin-actor.transform.right*6;
            until=Time.unscaledTime+4;stage=4;return null;
        }
        if(stage==4)
        {
            stationaryDrift=Math.Max(stationaryDrift,Vector3.Distance(stationaryOrigin,actor.position()));
            if(Time.unscaledTime<until)return null;
            manaAfter=actor.mana.currentMana;
            moveOrigin=actor.position();
            moveAccepted=use.movingPlayer.MoveToPoint(moveOrigin+actor.transform.right*3,false,false);
            until=Time.unscaledTime+3;stage=5;return null;
        }
        if(stage==5)
        {
            movedDistance=Math.Max(movedDistance,Vector3.Distance(moveOrigin,actor.position()));
            channelDuringMove|=use.channelling && movedDistance>0.25f;
            if(Time.unscaledTime<until)return null;
            BeginKeyboard(actor);return null;
        }
        if(stage==7)
        {
            EpochInputManager.instance.movementProcessor.ProcessKeyboardMovement(KeyboardDirections[keyboardIndex],true);
            var thumb=EpochInputManager.instance.thumbstickPlayerMovement;
            keyboardDirection=thumb.WorldMovementDir;
            if(Time.unscaledTime<until)return null;
            var travel=actor.position()-keyboardOrigin;
            bool pass=Vector3.Dot(travel,keyboardDirection)>0.25f && use.channelling;
            keyboardPassed&=pass;
            keyboardSamples.Add(new {keyboardIndex,input=KeyboardDirections[keyboardIndex].ToString(),distance=travel.magnitude,
                projection=Vector3.Dot(travel,keyboardDirection),channel=use.channelling,pass});
            EpochInputManager.instance.movementProcessor.ProcessKeyboardMovement(Vector2.zero,false);
            until=Time.unscaledTime+0.2f;stage=8;return null;
        }
        if(stage==8 || stage==9)
        {
            EpochInputManager.instance.movementProcessor.ProcessKeyboardMovement(Vector2.zero,false);
            if(stage==8)
            {
                if(Time.unscaledTime<until)return null;
                releaseOrigin=actor.position();releaseDrift=0;until=Time.unscaledTime+0.6f;stage=9;return null;
            }
            releaseDrift=Math.Max(releaseDrift,Vector3.Distance(releaseOrigin,actor.position()));
            if(Time.unscaledTime<until)return null;
            bool pass=releaseDrift<0.15f && use.channelling;
            keyboardPassed&=pass;
            keyboardSamples.Add(new {keyboardIndex,releaseDrift,channel=use.channelling,pass});
            keyboardIndex++;
            if(keyboardIndex<KeyboardDirections.Length){BeginKeyboard(actor);return null;}
            var aa=AbilityManager.get().playerAbilities.ToArray().FirstOrDefault(a=>a!=null &&
                string.Equals(a.name,"ArcaneAscendance",StringComparison.OrdinalIgnoreCase));
            if(aa==null)throw new InvalidOperationException("QA Arcane Ascendance asset missing");
            use.abilityList.setAbility(aa,3,true,true);
            // Seed an in-flight destination before activating the real buff.
            EpochInputManager.instance.movementProcessor.ProcessKeyboardMovement(Vector2.up,true);
            ascendance=use.mutatorManager.getMutator(aa)?.TryCast<ArcaneAscendanceMutator>();
            // The disposable Mage has no selected mastery, so the player cast
            // command rejects this Sorcerer skill. Construct the actual buff
            // through the native constructor/mutator to test root precedence.
            var buff=ascendance?.aoc?.constructAbilityObject(aa,actor.position(),aim,UseType.InstantDirect);
            ascendanceAccepted=buff!=null && ascendance.activeAbilityObject?.Pointer==buff.Pointer;
            until=Time.unscaledTime+0.4f;stage=10;return null;
        }
        if(stage==10)
        {
            if(Time.unscaledTime<until)return null;
            ascendanceRootObserved=ascendance?.activeAbilityObject!=null && actor.stateController.numberOfRoots>0;
            ascendanceOrigin=actor.position();ascendanceChannelPreserved=use.channelling;
            until=Time.unscaledTime+1.5f;stage=11;return null;
        }
        if(stage==11)
        {
            EpochInputManager.instance.movementProcessor.ProcessKeyboardMovement(Vector2.up,true);
            ascendanceDrift=Math.Max(ascendanceDrift,Vector3.Distance(ascendanceOrigin,actor.position()));
            ascendanceChannelPreserved&=use.channelling;
            if(Time.unscaledTime<until)return null;
            ascendance?.EndAbility();until=Time.unscaledTime+0.4f;stage=12;return null;
        }
        if(stage==12)
        {
            if(Time.unscaledTime<until)return null;
            if(actor.stateController.numberOfRoots>0)throw new InvalidOperationException("Ascendance root did not expire");
            EpochInputManager.instance.movementProcessor.ProcessKeyboardMovement(Vector2.up,true);
            movementResumed=Vector3.Distance(ascendanceOrigin,actor.position())>0.25f && use.channelling;
            if(!movementResumed && Time.unscaledTime<until+1.5f)return null;
            EpochInputManager.instance.movementProcessor.ProcessKeyboardMovement(Vector2.zero,false);
            use.stopUsingAbility(true);until=Time.unscaledTime+0.8f;stage=6;return null;
        }
        if(stage==6 && Time.unscaledTime>=until)return Finish(use,"");
        return null;
    }
    private void BeginKeyboard(Actor actor)
    {
        keyboardOrigin=actor.position();until=Time.unscaledTime+1.2f;stage=7;
    }
    private void Sample(Actor actor,UsingAbilityPlayer use)
    {
        if(Time.unscaledTime<nextSample)return;nextSample=Time.unscaledTime+0.4f;
        var movement=use.MovementFromAbility;
        samples.Add(new {stage,IgnoreAimFilter,position=Point(actor.position()),aim=Point(aim),beamTarget=Point(use.targetLocation),
            use.channelling,state=actor.stateController.currentState?.GetIl2CppType()?.Name,
            movementType=movement?.movementType.ToString(),moving=movement?.moving,
            destination=movement==null?null:Point(movement.destination),mouseFollow=movement?.changeDestinationWithMousePosition,
            componentMutator=actor.GetComponent<DisintegrateMutator>()!=null,
            registryMutator=use.mutatorManager?.getMutator(ability)?.GetIl2CppType()?.Name,
            counters=OfflineTools.OfflineDisintegrateMovement.Diagnostics});
    }
    private static object Point(Vector3 p)=>new {p.x,p.y,p.z};
    private object Finish(UsingAbilityPlayer use,string error)
    {
        Running=IgnoreAimFilter=false;
        LastRotation=null;
        return new {passed=error=="" && accepted && channelObserved && baselineDrift>0.5f &&
            stationaryDrift<0.15f && moveAccepted && movedDistance>0.25f && channelDuringMove &&
            beamAimUpdated && manaAfter<manaBefore && !use.channelling && keyboardPassed && keyboardIndex==KeyboardDirections.Length && rotationPenaltyRemoved &&
            ascendanceAccepted && ascendanceRootObserved && ascendanceChannelPreserved && ascendanceDrift<0.15f && movementResumed,
            error,accepted,channelObserved,baselineDrift,stationaryDrift,moveAccepted,movedDistance,
            channelDuringMove,beamAimUpdated,released=!use.channelling,manaBefore,manaAfter,
            focused=Application.isFocused,samples,keyboardPassed,keyboardSamples,rotationPenaltyRemoved,originalRotationPenalty,
            ascendanceAccepted,ascendanceRootObserved,ascendanceChannelPreserved,ascendanceDrift,movementResumed,
            scope="Native disposable Mage A/B: actual channel + native aim updates reproduce dev.18 then filter; native explicit move and stop. No OS mouse automation; hidden-window focus guard bypassed only for private explicit move."};
    }
}
