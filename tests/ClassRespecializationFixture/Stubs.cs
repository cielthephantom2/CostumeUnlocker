using Il2CppLE.Data;
namespace HarmonyLib { public class HarmonyPatch:Attribute{public HarmonyPatch(params object[] args){}} }
namespace Il2CppSystem.Collections.Generic { public class List<T>:System.Collections.Generic.List<T>{} }
namespace Il2CppInterop.Runtime.InteropTypes.Arrays {
 public class Il2CppReferenceArray<T>:System.Collections.Generic.List<T>{public int Length=>Count;public Il2CppReferenceArray(int n){for(int i=0;i<n;i++)Add(default);} public static implicit operator Il2CppReferenceArray<T>(T[] a){var r=new Il2CppReferenceArray<T>(0);r.AddRange(a);return r;}}
}
namespace UnityEngine { public static class Application{public static string persistentDataPath;} }
namespace Il2CppClientAppState { public enum ClientAppStateType{None,SystemLoading,Login,CharacterSelect,InGame} public static class ClientStateManager{public static ClientAppStateType CurrentClientAppStateType=ClientAppStateType.InGame;} }
namespace Il2CppCysharp.Threading.Tasks {
 public class Completion{public bool Done=true;public Exception Error;}
 public struct UniTask {public Completion Value;public static UniTask CompletedTask=>new(){Value=new()};public Awaiter GetAwaiter()=>new(){Value=Value};public struct Awaiter{public Completion Value;public bool IsCompleted=>Value.Done;public void GetResult(){if(Value.Error!=null)throw Value.Error;}}}
}
namespace Il2CppLE.Networking {public class NativeObject{private static int next;public IntPtr Pointer{get;}=(IntPtr)(++next);} }
namespace Il2Cpp {
 using Il2CppLE.Networking;
 public enum CharacterClassID:byte{Primalist,Mage,Sentinel,Acolyte,Rogue}
 public enum SP{HealthRegen,ManaRegen,Movespeed}
 public enum AbilityID{meteor,meteorShower,other}
 public class Ability:NativeObject{public string playerAbilityID;public static readonly Dictionary<AbilityID,Ability> All=new(){{AbilityID.meteor,new()},{AbilityID.meteorShower,new()},{AbilityID.other,new()}};public static Ability getAbility(AbilityID id)=>All[id];}
 public class CharacterClass:NativeObject{public string className,treeID;public CharacterClassID classID;public Il2CppSystem.Collections.Generic.List<Ability> defaultAbilities=new();}
 public static class CharacterClassList{public static readonly Dictionary<int,CharacterClass> All=new();public static CharacterClass getClass(int id)=>All.GetValueOrDefault(id);}
 public class Actor:NativeObject{public CharacterDataTracker characterDataTracker=new();public BaseHealth health=new();public AbilityStatsMutatorManager StatsMutator=new();public MeteorMutator Meteor=new();public T GetComponent<T>()where T:class=>typeof(T)==typeof(MeteorMutator)?Meteor as T:StatsMutator as T;}
 public class CharacterDataTracker{public CharacterData charData;}
 public class BaseHealth{public float currentHealth=100;}
 public class BaseStats:NativeObject{public enum ModType{ADDED,INCREASED,MORE}public Actor actor;}
 public class CharacterStats:BaseStats{public void SetInitialValues(){}public void ChangeStatModifier(){}}
 public static class PlayerFinder{public static Actor Actor;public static ActorSync Sync;public static Actor getPlayerActor()=>Actor;public static ActorSync getLocalActorSync()=>Sync;}
 public class ActorSync:NativeObject{}
 public class AbilityStatsMutatorManager{public bool meteorConvertedToLightning;}
 public class MeteorMutator{public enum ConversionType{Default_Fire,Cold,Lightning}public ConversionType passedConversion;public const byte LIGHTNING_CONVERSION_VISUAL_ID=1;}
 public class GlobalTreeData{public class TreeData{public ushort version=5;}public static bool Missing;public static GlobalTreeData get()=>new();public TreeData getTreeData(string id)=>Missing?null:new();}
 public class UIBase{public static UIBase instance=new();public static int Exits;public void ExitToCharacterSelect(){Exits++;}}
 public class CharacterTile{public CharacterData characterData;public void Set(CharacterData data){characterData=data;}}
 public class CharacterSelect{public static CharacterSelect instance=new();public Il2CppSystem.Collections.Generic.List<CharacterTile> availableCharacterTiles=new();}
 public class LocalCharacterSlots{public static LocalCharacterSlots instance=new();public bool CharacterSlotsLoaded=true;public Il2CppSystem.Collections.Generic.List<CharacterData> characterSlots=new();public Il2CppCysharp.Threading.Tasks.UniTask LoadCharacterByData(CharacterData data)=>Il2CppCysharp.Threading.Tasks.UniTask.CompletedTask;}
 public class AbilitySync{public void ReceiveCreateVisualsInternal(){}}
 public class AbilityVisuals{public void HandleMeteorIndicator(){}}
}
namespace Il2CppLE.Data {
 public class SavedSkillTree{public string TreeID;}
 public class SavedCharacterTree{public string TreeID;public ushort Version;public int UnspentPoints;public Il2CppSystem.Collections.Generic.List<byte> NodeIDs,NodePoints;public SavedCharacterTree(string id,ushort v,Il2CppSystem.Collections.Generic.List<byte> nodes,Il2CppSystem.Collections.Generic.List<byte> pts){TreeID=id;Version=v;NodeIDs=nodes;NodePoints=pts;}}
 public class CharacterData {
  public CharacterData(){DataStoreCache=new(this);}
  public FakeCache DataStoreCache;public object UserIdentity=new();public FakeStore IDataStore=new();
  public string Id="character-a",CharacterName="Test";public int CharacterClass=1,Level=90;public byte ChosenMastery=2,OriginalMastery=2;
  public SavedCharacterTree SavedCharacterTree=new("mage",1,new(){1,2},new(){5,7}){UnspentPoints=3};
  public Il2CppSystem.Collections.Generic.List<SavedSkillTree> SavedSkillTrees=new(){new(){TreeID="meteor"}};
  public Il2CppSystem.Collections.Generic.List<string> CharacterTreeNodeProgression=new(){"mage node"},AbilityBar=new(){"old skill"};
  public Il2CppSystem.Collections.Generic.List<int> WerebearAbilityBar=new(){1},SprigganFormAbilityBar=new(){2},SwarmbladeAbilityBar=new(){3};
  public readonly object SavedItems=new(),SavedQuests=new(),MonolithRuns=new(),SavedWeaverTree=new(),Factions=new();public int Gold=98765;public string Location="EoT";public int SaveCalls;public static Il2CppCysharp.Threading.Tasks.Completion NextSave=new();
  public void SetDirty(bool b){}public Il2CppCysharp.Threading.Tasks.UniTask SaveData(){SaveCalls++;return new(){Value=NextSave};}
 }
}
namespace Il2CppLE.Networking.Generated.Static.AbilitySyncService {
 public struct ActorKey{public Il2Cpp.ActorSync GetActorSync()=>Il2Cpp.PlayerFinder.Sync;}
 public struct AbilityIndex{public Il2Cpp.Ability GetAbility()=>Il2Cpp.Ability.getAbility(Il2Cpp.AbilityID.meteor);}
 public struct MessageCreateVisuals{public ActorKey? ActorSyncKey;public AbilityIndex AbilityIndex;public byte VisualsId;}
}
namespace Il2CppLE {public class AbilitySyncService{public void ReceiveCreateVisuals(){}}}
namespace MelonLoader {public static class MelonLogger{public static void Msg(string s){}public static void Warning(string s){}}}
namespace LastEpochLAN.Mod.OfflineTools {
 public class Settings{}
 public static class OfflineToolkit{public static bool Enabled=true;public static bool Ready{get=>Enabled && ready;set=>ready=value;}private static bool ready=true;public static string Error;public static Settings Settings=new();}
}

namespace Il2CppLE.Data { public class FakeStore{public bool IsOffline()=>true;}public class FakeCache{private readonly CharacterData data;public FakeCache(CharacterData d){data=d;}public Il2CppCysharp.Threading.Tasks.UniTask Flush(object identity,bool purge)=>data.SaveData();} }


namespace UnityEngine {
 public struct Rect{public Rect(float x,float y,float width,float height){}}
 public class GUIStyle{public bool wordWrap;public GUIStyle(GUIStyle other){}}
 public class GUISkin{public GUIStyle label=new(null);}
 public static class GUI{public static GUISkin skin=new();public static bool enabled=true;public static void Box(Rect r,string s){}public static void Label(Rect r,string s,GUIStyle style=null){}public static bool Button(Rect r,string s)=>false;}
}

namespace Il2CppLE.Core { public static class Paths{public static string GetDataPath()=>System.IO.Path.Combine(UnityEngine.Application.persistentDataPath,"Saves");} }
