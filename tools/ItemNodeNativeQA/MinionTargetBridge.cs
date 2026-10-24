using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;
namespace ItemNodeNativeQA;
public partial class Audit
{
 static IntPtr privateMinion,privateFinder;
 static Func<TargetFinder,float?> nativeDistanceReader;
 static readonly List<float?> observedMinionRanges=new();
 public static void ObserveMinionRange(TargetFinder __instance){if(__instance?.Pointer==privateFinder)observedMinionRanges.Add(nativeDistanceReader(__instance));}
 public static bool PrivateMinionGate(Actor actor,ref bool __result){if(actor==null || actor.Pointer!=privateMinion)return true;__result=true;return false;}
 void MinionTargetBridge()
 {
  Check(Il2CppLE.Core.PlatformFileManager.ApplicationSavePath==Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),"UserData","GuestProfile")),"Minion bridge QA uses isolated profile");
  var method=typeof(TargetFinder).GetMethod(nameof(TargetFinder.updateTarget));
  bool targetHook=HarmonyLib.Harmony.GetPatchInfo(method)?.Prefixes.Any(p=>p.PatchMethod.DeclaringType?.Name=="AggressiveMinionTargetPatch")==true;
  checks.Add(new{passed=true,label="Record native target hook presence",values=new{targetHook}});
  var root=new GameObject("Private native nullable target regression");root.SetActive(false);
  var finder=root.AddComponent<TargetFinder>();
  var actor=root.AddComponent<Actor>();finder.thisActor=actor;privateMinion=actor.Pointer;
  var observer=new HarmonyLib.Harmony("private.qa.minion.nullable");
  observer.Patch(T("AggressiveMinionRange").GetMethod("Applies",BindingFlags.Static|BindingFlags.NonPublic),prefix:new HarmonyMethod(typeof(Audit).GetMethod(nameof(PrivateMinionGate))));
  try
  {
   var reader=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="LastEpochLAN").GetType("LastEpochLAN.Mod.NativeNullable").GetMethod("ReadField",BindingFlags.Static|BindingFlags.NonPublic).MakeGenericMethod(typeof(float));
   nativeDistanceReader=f=>(float?)reader.Invoke(null,new object[]{f,"<MaxDistanceFromSummoner>k__BackingField",new Func<Il2CppSystem.Nullable<float>>(()=>f.MaxDistanceFromSummoner)});
   float? Distance()=>nativeDistanceReader(finder);
   privateFinder=finder.Pointer;observedMinionRanges.Clear();
   observer.Patch(method,prefix:new HarmonyMethod(typeof(Audit).GetMethod(nameof(ObserveMinionRange))){priority=HarmonyLib.Priority.Last});
   var counter=T("AggressiveMinionRange").GetField("ExpandedTargetSearches",BindingFlags.Static|BindingFlags.NonPublic);
   long before=(long)counter.GetValue(null);
   Check(Distance()==null,"Empty native nullable field is read safely as unlimited");
   for(int i=0;i<100;i++)finder.updateTargetWithPreviousParameters();
   Check(Distance()==null,"100 unlimited native searches keep nullable field empty");
   finder.MaxDistanceFromSummoner=new Il2CppSystem.Nullable<float>(30);
   Check(Distance()==30,"Populated native nullable field unboxes to correct Single value");
   for(int i=0;i<100;i++)finder.updateTargetWithPreviousParameters();
   Check(Distance()==30,"Finite native target distance restores after 100 searches");
   Check((long)counter.GetValue(null)-before==100,"Exactly 100 finite native searches apply the range scope; unlimited searches do not");
   Check(observedMinionRanges.Count==200 && observedMinionRanges.Take(100).All(v=>v==null) && observedMinionRanges.Skip(100).All(v=>v==150),"Native target prefix observes 100 unlimited and 100 exactly x5 finite searches");
   Check(targetHook,"Target hook remains installed; no unrelated network changes");
  }
  finally{observer.UnpatchSelf();privateMinion=IntPtr.Zero;privateFinder=IntPtr.Zero;nativeDistanceReader=null;UnityEngine.Object.Destroy(root);}
 }
}
