using Il2Cpp;
using UnityEngine;
namespace ItemNodeNativeQA;
public partial class Audit
{
 void VerifyDisintegrate(Actor actor)
 {
  var ability=Ability.getAbility(AbilityID.disintegrate);
  var mutator=actor.GetMutatorManager().getMutator(ability)?.TryCast<DisintegrateMutator>();bool created=mutator==null;
  if(created){mutator=actor.gameObject.AddComponent<DisintegrateMutator>();mutator.actor=actor;mutator.ability=ability;}
  try
  {
   Check(mutator.getMovementDuringUse()==AbilityMovement.Spin && mutator.getChangeDestinationWhileMoving() && mutator.getCanUpdateTargetWhileChannelling() && mutator.getStopMovementOnKeyRelease(),"Native Disintegrate getters select mobile channel with live target updates");
   Check(!mutator.getUseSpeedAffectsMovement() && mutator.getIncreasedMovementSpeedAffectsMovement() && Near(mutator.getBaseMovementDuration(),.3f) && Near(mutator.getMovementDurationPerUnitDistance(),.05f),"Native mobile channel uses normal movement stats and native Focus timing");
   var use=actor.GetComponent<UsingAbility>();var speed=actor.GetComponent<SpeedManager>();
   Check(use!=null && speed!=null && actor.navMeshAgent!=null,"Native channel and speed components available");
   var savedAbility=use.ability;bool savedChannel=use.channelling;
   try
   {
    use.channelling=false;speed.updateSpeed();float normal=actor.navMeshAgent.speed;
    use.ability=ability;use.channelling=true;
    for(int n=0;n<100;n++)speed.updateSpeed();
    Check(normal>0 && Near(actor.navMeshAgent.speed,normal*.8f),"Native speed consumer is exactly80 percent after100 updates",new{normal,channel=actor.navMeshAgent.speed});
    use.channelling=false;speed.updateSpeed();
    Check(Near(actor.navMeshAgent.speed,normal),"Native speed restored after channel flag clears");
   }
   finally{use.ability=savedAbility;use.channelling=savedChannel;speed.updateSpeed();}
  }
  finally{if(created)UnityEngine.Object.Destroy(mutator);}
 }
}
