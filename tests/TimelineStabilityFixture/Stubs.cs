namespace HarmonyLib{[AttributeUsage(AttributeTargets.Class)]public class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string m){}}}
namespace Il2Cpp
{
 public enum TimelineID{First,Second}
 public class NativeObject{static int next;public IntPtr Pointer=(IntPtr)Interlocked.Increment(ref next);}
 public class Actor:NativeObject{}
 public class MonolithRun:NativeObject{public int stability,difficultyIndex,Max=1000;public TimelineID timelineID;public int GetMaxStability()=>Max;public void AddStability(){}public void TryUseTemporalKeystoneItemAndIncreaseStability(){}public void GetStabilityRangeFromTemporalKeystone(){}}
 public class EchoWebIsland{public void GetStabilityRewardAfterFactionModifiers(){}}
 public class EchoCompletionReward{public TimelineID timelineID;public int difficultyIndex,bonusStability;}
 public class MonolithRestZoneManager{public void Initialize(){}}
 public class Text{public string text;}
 public class SoftRef{public string SubPath;}
 public class EchoModListItem{public SoftRef spriteSoftRef;public Text description=new();}
 public class EchoTooltip{public List<EchoModListItem> _echoRewards=new();public void InitEchoRewards(){}}
 public class StabilityBar{public void SetCurrentStabilityGainMax(){}}
 public class QuestTrackerUI{public void ApplyBonusStability(){}}
}
namespace LastEpochLAN.Mod.OfflineTools
{
 internal static class OfflineToolkit{internal static bool Enabled=true;}
 internal static class MeteorNodeRuntime{internal static Il2Cpp.Actor Player=new();internal static bool Local(Il2Cpp.Actor actor)=>OfflineToolkit.Enabled && actor!=null && actor.Pointer==Player.Pointer;}
}
