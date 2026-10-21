namespace HarmonyLib {[AttributeUsage(AttributeTargets.Class)]public sealed class HarmonyPatch:Attribute{public HarmonyPatch(Type type,string name){}}}
namespace MelonLoader {public static class MelonLogger{public static void Warning(string text)=>Console.WriteLine(text);}}
namespace LastEpochLAN.Mod.OfflineTools {internal static class OfflineToolkit {internal static bool Enabled=true;}}
namespace UnityEngine {
 public static class Time {public static float unscaledTime;}
 public class GameObject {public bool Active;public void SetActive(bool value)=>Active=value;}
 public class Component {public GameObject gameObject=new();}
 public class RectTransform:Component{}
}
namespace UnityEngine.Events {
 public class UnityAction<T> {public Action<T> Value;public static explicit operator UnityAction<T>(Action<T> action)=>new(){Value=action};}
 public class UnityEvent<T> {public List<UnityAction<T>> Listeners=new();public void AddListener(UnityAction<T> value)=>Listeners.Add(value);public void RemoveListener(UnityAction<T> value)=>Listeners.Remove(value);}
}
namespace UnityEngine.UI {
 public class ScrollRect{public float verticalNormalizedPosition=1;}
 public class Scrollbar {public UnityEngine.Events.UnityEvent<float> onValueChanged=new();}
}
namespace Il2CppTMPro {public class Text{public string text;} public class Dropdown{public int value;}}
namespace Il2CppSystem {
 public class Action {public System.Action Value;public static explicit operator Action(System.Action action)=>new(){Value=action};public void Invoke()=>Value();}
 public struct DateTime {public static DateTime UtcNow=>new();}
}
namespace Il2CppSystem.Collections.Generic {
 public interface IEnumerable<T>:System.Collections.Generic.IEnumerable<T>{}
 public class List<T>:System.Collections.Generic.List<T>,IEnumerable<T>{public TOut Cast<TOut>()=>(TOut)(object)this;}
 public class Dictionary<TKey,TValue>:System.Collections.Generic.Dictionary<TKey,TValue>{}
}
namespace Il2CppLE.UI {public static class LoadingScreen{public static bool IsVisible;}}
namespace Il2CppLE.Data {
 public enum Cycle {Legacy=1,Current=5}
 public class CharacterData {
  public string Id="local-character",CharacterName="Character";
  public int CharacterClass=3,Level=81,MaxWave=100,MaxCorruption=300,OmensKilled=60,OmenWindowsCompleted=5,Deaths;
  public byte ChosenMastery=1;public bool Hardcore;public long CurrentExp=12345;public Cycle Cycle=Cycle.Current;
 }
}
namespace Il2Cpp {
 public enum CharacterClassID{Primalist,Mage,Sentinel,Acolyte,Rogue}
 public enum LadderType{Arena,Level,Corruption,SeasonEvent}
 public enum LadderMode{Softcore,Hardcore}
 public enum LadderGroup{NoParty,PartyOf2,PartyOf3,PartyOf4,Solo}
 public enum LadderClassFilter{All,Primalist,Mage,Sentinel,Acolyte,Rogue}
 public class ExperienceTracker {public int CurrentLevel=81;public long CurrentExperience=12345;}
 public class CharacterDataTracker{public Il2CppLE.Data.CharacterData charData=new();}
 public class Actor {public CharacterDataTracker characterDataTracker=new();public bool HasMovedToPlayerSpawn=true;public ExperienceTracker experienceTracker=new();}
 public static class PlayerFinder{public static Actor Player=new();public static Actor getPlayerActor()=>Player;}
 public class Mastery{public string name="Necromancer";}
 public class CharacterClass{public Mastery[] masteries={new()};}
 public static class CharacterClassList{public static CharacterClass getClass(int id)=>new();}
 public class TabUIController{public int SelectedTabId;}
 public struct LadderFilter {
  public LadderFilter(LadderType type,LadderMode mode,LadderGroup group,Il2CppLE.Data.Cycle cycle,LadderClassFilter classFilter){Type=type;Mode=mode;Group=group;Cycle=cycle;ClassFilter=classFilter;}
  public LadderType Type;public LadderMode Mode;public LadderGroup Group;public Il2CppLE.Data.Cycle Cycle;public LadderClassFilter ClassFilter;
  public string AsPartitionKey=>$"{Type}:{Mode}:{Group}:{Cycle}:{ClassFilter}";
 }
 public class LadderUIFilterPanel {
  public Il2CppTMPro.Text header=new();public LadderType ladderType;public LadderMode ladderMode;
  public TabUIController tabController=new();public Il2CppTMPro.Dropdown classDropdown=new();
  public List<Il2CppSystem.Action> Listeners=new();
  public LadderFilter CurrentSelection=>LastEpochLAN.Mod.NativeRoomLadder.Filter(this);
  public void Set(LadderFilter filter){ladderType=filter.Type;ladderMode=filter.Mode;tabController.SelectedTabId=(int)filter.Group;classDropdown.value=(int)filter.ClassFilter;foreach(var listener in Listeners.ToArray())listener.Invoke();}
  public void add_LadderSelectionChanged(Il2CppSystem.Action value)=>Listeners.Add(value);
  public void remove_LadderSelectionChanged(Il2CppSystem.Action value)=>Listeners.Remove(value);
 }
 public static class Localization {
  public static List<Il2CppSystem.Action> Listeners=new();
  public static void add_LocaleChanged(Il2CppSystem.Action value)=>Listeners.Add(value);
  public static void remove_LocaleChanged(Il2CppSystem.Action value)=>Listeners.Remove(value);
 }
 public class LadderRowPlayerElement:UnityEngine.Component{}
 public class LadderSession {
  public enum LoadingState{None,LoadingRecords,Complete,Error}
  public LadderSession(LadderFilter filter){Filter=filter;}
  public LadderFilter Filter;public Il2CppLE.Services.Ladders.RankedItem PlayerRecord;
  public Il2CppSystem.DateTime UpdateTime;public LoadingState State;
  public List<Il2CppLE.Services.Ladders.RankedItem> Records=new();
  public void SetRecords(Il2CppSystem.Collections.Generic.IEnumerable<Il2CppLE.Services.Ladders.RankedItem> records)=>Records=records.ToList();
 }
 public class LadderSessionManager {public LadderSession GetSession(LadderFilter filter)=>throw new Exception("Online service called");}
 public class LadderUIManager {
  public IntPtr Pointer=(IntPtr)123;public bool isActiveAndEnabled=true,firstTimeInitializationCompleted;
  public List<UnityEngine.RectTransform> activableElements=new(){new()};public UnityEngine.RectTransform disabledGroup=new(),seasonEventMessageTab=new(),seasonEventTabButton=new();
  public UnityEngine.Component seasonEventBanner=new();public LadderUIFilterPanel filterPanel=new();
  public UnityEngine.UI.ScrollRect scrollRect=new();public UnityEngine.UI.Scrollbar scrollBar=new();
  public LadderRowPlayerElement _selfLadderRowPlayer=new();public LadderSession Session;public int RefreshCount;
  public void ShowSelectedLadder(){RefreshCount++;Session=LastEpochLAN.Mod.NativeRoomLadder.CreateSession(filterPanel.CurrentSelection);scrollRect.verticalNormalizedPosition=1;}
  public void OnScrollBarChange(float value){}public void UpdatePlayerRowElement(){}public void HandleSeasonEventUI(bool forceUpdate){}
 }
}
namespace Il2CppLE.Services.Ladders {
 public class RankedItem{public long Rank;}
 public class RankedLadderEntry<T>:RankedItem{public T LadderEntry;public Il2CppSystem.Collections.Generic.Dictionary<string,string> Metadata;}
 public class LadderEntry{public string LadderId,LadderEntryId,LadderType;public long TimeStamp;public double SortingScore;}
 public class PlayerEntry {
  public string PlayerId,PlayerName,CharacterId,PlayerCharacterId,CharacterName,CharacterClass,CharacterMastery;
  public int CharacterLevel,CharacterDeaths,MaxCorruption,TimelineId,OmenKills,OmenWindowsCompleted;public long CharacterTotalExp;
  public Il2CppSystem.Collections.Generic.List<string> CharacterSkills;
 }
 public class ArenaLadderEntry:LadderEntry{public class ArenaPlayerEntry:PlayerEntry{}public Il2CppSystem.Collections.Generic.List<ArenaPlayerEntry> PlayerEntries;public int Wave;}
 public class LevelLadderEntry:LadderEntry{public class LevelPlayerEntry:PlayerEntry{}public LevelPlayerEntry PlayerEntry;}
 public class CorruptionLadderEntry:LadderEntry{public class CorruptionPlayerEntry:PlayerEntry{}public CorruptionPlayerEntry PlayerEntry;}
 public class OmenLadderEntry:LadderEntry{public class OmenPlayerEntry:PlayerEntry{}public OmenPlayerEntry PlayerEntry;}
 public static class LadderConstants{public static class LadderEntryMetadataKeys{public static string HasDied="hasDied";}}
 public static class LadderUtils {
  public static Il2CppSystem.Collections.Generic.List<string> BuildSpecializedSkillList(Il2CppLE.Data.CharacterData data)=>new(){"skill-one","skill-two"};
 }
}
