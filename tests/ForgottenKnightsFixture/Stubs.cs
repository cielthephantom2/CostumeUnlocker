namespace HarmonyLib
{[AttributeUsage(AttributeTargets.Class)]public class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string m){}}}
namespace UnityEngine{public static class Time{public static float unscaledTime;}}
namespace Il2Cpp
{
 public class Actor
 {static int next;public IntPtr Pointer=(IntPtr)Interlocked.Increment(ref next);public Il2CppLE.Factions.IFactionInfoProvider FactionInfo;}
 public static class PlayerFinder{public static Actor Player;public static Actor getPlayerActor()=>Player;}
}
namespace Il2CppLE.Factions
{
 public enum FactionID{CircleOfFortune=1,ForgottenKnights=2,TheWeaver=3}
 public interface IFactionInfoProvider{ForgottenKnights FK{get;}}
 public class Provider:IFactionInfoProvider{public ForgottenKnights FK{get;set;}}
 public static class FactionInfoExtensions{public static ForgottenKnights FK(IFactionInfoProvider p)=>p?.FK;}
 public class ForgottenKnights
 {
  public bool IsMember=true;public int Rank;public Il2Cpp.Actor CachedPlayerActor;public Dictionary<int,bool> rankToggles=new();
  public float AdditionalChanceForSwappableNemesisUnique,DoubleStabilityGainChance,AdditionalChanceForHarbingersToDropTemporalKeystone;
  public int BonusMaxForgingPotentialWhenEmpoweringNemesis=6,BonusCorruptionPerGazeOfOrobyss=1;
  public void ToggleRank(int rank,bool enable,bool gameplay)
  {
   rankToggles[rank]=enable;
   switch(rank){case 1:AdditionalChanceForSwappableNemesisUnique=enable?.4f:0;break;case 2:DoubleStabilityGainChance=enable?.1f:0;break;case 5:AdditionalChanceForHarbingersToDropTemporalKeystone=enable?.2f:0;break;}
  }
 }
 public class FactionData{public class RankData{public string GetRankDescription(int rank,FactionID id,bool lessLineBreaks)=>"";}}
}
namespace LastEpochLAN.Mod.OfflineTools
{internal static class OfflineToolkit{internal static bool Enabled=true,Ready=true;}}
