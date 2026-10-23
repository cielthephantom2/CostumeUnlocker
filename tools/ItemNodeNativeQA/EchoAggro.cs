using System.Reflection;
using Il2Cpp;
using UnityEngine;
namespace ItemNodeNativeQA;
public partial class Audit
{
 static bool probingAcquisition;static int activationAttempts;
 public static bool ObserveAcquisition(){if(!probingAcquisition)return true;activationAttempts++;return false;}
 void EchoAggroActor(Actor player)
 {
  EchoAggro();
  var root=new GameObject("Private QA pooled enemy acquisition");root.SetActive(false);
  var zoneRoot=new GameObject("Private QA echo eligibility");zoneRoot.SetActive(false);
  var originalZone=MonolithZoneManager.instance;
  var enemyAlignment=ScriptableObject.CreateInstance<Alignment>();
  var activate=typeof(UsingAbility).GetMethod(nameof(UsingAbility.Activate));
  var observer=new HarmonyLib.Harmony("private.qa.aggro.acquisition");
  try
  {
   // Pooled actors can be persistent while the live echo manager is in the map.
   UnityEngine.Object.DontDestroyOnLoad(root);
   var enemy=root.AddComponent<Actor>();var controller=root.AddComponent<StateController>();
   var alignment=root.AddComponent<AlignmentManager>();var use=root.AddComponent<UsingAbility>();
   enemy.alignmentManager=alignment;enemy.stateController=controller;enemy.tForm=root.transform;
   controller.actor=enemy;use.controller=controller;use.tForm=root.transform;
   enemyAlignment.alignmentID=250;enemyAlignment.isFoe=new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<bool>(256);
   enemyAlignment.isFoe[player.GetAlignment().alignmentID]=true;alignment.alignment=enemyAlignment;
   MonolithZoneManager.instance=zoneRoot.AddComponent<MonolithZoneManager>();
   Check(root.scene.handle!=zoneRoot.scene.handle && (bool)Call("EchoEnemyAggro","Applies",enemy),"Real IL2CPP hostile pooled actor passes echo gate across different scenes");
   observer.Patch(activate,prefix:new HarmonyLib.HarmonyMethod(typeof(Audit).GetMethod(nameof(ObserveAcquisition))));
   float originalModifier=controller.aggroRangeModifier;
   int tested=0,expanded=0;
   foreach(float x in new[]{-30f,-20f,-10f,10f,20f,30f})
   {
    root.transform.position=player.position()+new Vector3(x,0,0);
    bool expected=UsingAbility.DisplacementWithinAggroBox(-x/3,0,1);
    activationAttempts=0;probingAcquisition=true;
    try{use.activateIfEnemyInRange();}finally{probingAcquisition=false;}
    Check((activationAttempts>0)==expected,"Native acquisition selects player using x3 range",new{x,expected,activationAttempts});
    Check(Near(controller.aggroRangeModifier,originalModifier),"Native acquisition restores original controller modifier",new{x});
    if(expected && !UsingAbility.DisplacementWithinAggroBox(-x,0,1))expanded++;
    tested++;
   }
   Check(tested==6 && expanded>0,"Native acquisition reaches player beyond original aggro boundary",new{tested,expanded});
  }
  finally
  {
   probingAcquisition=false;observer.UnpatchSelf();MonolithZoneManager.instance=originalZone;
   UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(zoneRoot);UnityEngine.Object.Destroy(enemyAlignment);
  }
 }
 void EchoAggro()
 {
  var target=typeof(UsingAbility).GetMethod(nameof(UsingAbility.DisplacementWithinAggroBox));
  Check(HarmonyLib.Harmony.GetPatchInfo(target)?.Prefixes.Any(p=>p.PatchMethod.DeclaringType?.Name=="OfflineEchoEnemyAggroBoxPatch")==true,"x3 aggro native box hook installed");
  var current=T("EchoEnemyAggro").GetField("Searching",BindingFlags.NonPublic|BindingFlags.Static);
  bool original=(bool)current.GetValue(null);int broadened=0,compared=0;
  try
  {
   foreach(float originalMultiplier in new[]{1f,1.5f,2f})
   foreach(float x in new[]{-45f,-30f,-15f,0f,15f,30f,45f})
   foreach(float z in new[]{-45f,-30f,-15f,0f,15f,30f,45f})
   {
    current.SetValue(null,false);
    bool baseline=UsingAbility.DisplacementWithinAggroBox(x,z,originalMultiplier);
    bool expected=UsingAbility.DisplacementWithinAggroBox(x/3,z/3,originalMultiplier);
    current.SetValue(null,true);
    bool actual=UsingAbility.DisplacementWithinAggroBox(x,z,originalMultiplier*3);
    if(actual!=expected)throw new Exception("x3 native box mismatch: "+x+"/"+z+"/"+originalMultiplier);
    if(!baseline && actual)broadened++;compared++;
   }
   Check(compared==147 && broadened>0,"147 native geometric comparisons equal exactly x3 with native bonuses retained",new{compared,broadened});
   current.SetValue(null,false);
   Check(!UsingAbility.DisplacementWithinAggroBox(1000,1000,1),"distant targets outside x3 remain outside native box");
  }
  finally{current.SetValue(null,original);}
 }
}
