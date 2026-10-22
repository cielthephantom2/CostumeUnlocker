namespace UnityEngine
{
 public enum KeyCode{None=0,Backspace=8,Escape=27,Space=32,A=97,B=98,C=99,D=100,E=101,Delete=127,F1=282,F2=283,F3=284,F4=285,LeftShift=304,RightShift=303,LeftControl=306,RightControl=305,LeftAlt=308,RightAlt=307,Mouse0=323}
 public static class Input
 {public static HashSet<KeyCode> Down=new(),Held=new();public static bool GetKeyDown(KeyCode k)=>Down.Contains(k);public static bool GetKey(KeyCode k)=>Held.Contains(k);}
 public static class Time{public static float unscaledTime,timeScale=1;}
 public static class Application{public static bool isFocused=true;}
 public struct Rect{public Rect(float x,float y,float w,float h){}public bool Contains(object p)=>false;}
 public enum EventType{Repaint,MouseDown}
 public class Event{public static Event current=new();public EventType type;public object mousePosition;public void Use(){}}
 public static class GUI{public static bool enabled=true;public static string Click="";public static void Box(Rect r,string s){}public static void Label(Rect r,string s){}public static bool Button(Rect r,string s){if(Click!=s || !enabled)return false;Click="";return true;}}
}
namespace Il2Cpp
{
 public class EpochInputManager
 {public static EpochInputManager instance=new();static int next;public IntPtr Pointer=(IntPtr)(++next);public bool forceDisableInput,cinematicActive,IsInputFieldActive,ButtonPressBlocked;}
 public class Actor{public GameObject gameObject=new();public bool HasMovedToPlayerSpawn=true,Dying;public Health health=new();public Tracker characterDataTracker=new();public Sync Sync=new();public Sync GetActorSync()=>Sync;public bool isDying(bool b)=>Dying;}
 public class GameObject{public bool activeInHierarchy=true;}
 public class Health{public float currentHealth=100;}
 public class Tracker{public CharData charData=new();}
 public class CharData{public object UserIdentity=new();}
 public class Sync{public Visual actorVisuals=new();}
 public class Visual{public bool HasBeenInitialized=true;}
 public static class PlayerFinder{public static Actor Player=new();public static Actor getPlayerActor()=>Player;}
 public class UIBase
 {
  public static UIBase instance=new();public bool menuOpen,IsChatInputFocused,Panel;public string Last="";public int Calls;
  public bool AnyPanelOpened()=>Panel;
  public void OpenObservatoryPanel(Il2CppLE.Factions.ProphecyRegion region,bool close,bool tutorial){Last="telescope:"+region;Calls++;}
  public void openMasteryPanel(bool close,bool respec){Last="mastery:"+respec;Calls++;}
  public void OpenPassivePanel(bool respec){Last="points:"+respec;Calls++;}
  public void openStash(bool close,bool sound,bool button,string search,bool clear){Last="personal";Calls++;}
 }
 public class SceneDetails{public string Name="ArenaLobby",DisplayName="Champions' Gate";public object arenaInformation;}
 public static class SceneList{public static SceneDetails Scene=new();public static SceneDetails FindSceneDetails(string name)=>Scene;}
}
namespace Il2CppLE.UI{public static class LoadingScreen{public static bool IsVisible;}}
namespace Il2CppLE.Factions{public enum ProphecyRegion{South}}
namespace Il2CppLE.Services.Transitions{public enum TransitionFadeType{ToBlack}public enum TransitionError{None,Failed}}
namespace Il2CppOperationResult{public class Status<T>{public bool IsSuccess=true;public T Error;}}
namespace Il2CppLE.Services.Models{}
namespace Il2CppCysharp.Threading.Tasks
{
 public class Completion<T>{public bool Done;public T Result;public int Reads;}
 public class UniTask<T>{public Completion<T> State=new();public Awaiter GetAwaiter()=>new(){State=State};public struct Awaiter{public Completion<T> State;public bool IsCompleted=>State.Done;public T GetResult(){State.Reads++;return State.Result;}}}
}
namespace Il2CppLE.Services
{
 using Il2CppCysharp.Threading.Tasks;
 using Il2CppLE.Services.Transitions;
 using Il2CppOperationResult;
 public static class ServiceProvider{public static Transition OfflineTransitionService=new();}
 public class Transition
 {
  public bool Busy;public int Calls;public string Scene;public byte Gate;
  public UniTask<Status<TransitionError>> Task=new(){State=new(){Result=new()}};
  public bool IsTransitioning(object id)=>Busy;
  public UniTask<Status<TransitionError>> Waypoint(object identity,TransitionFadeType fade,string scene,byte gate){Calls++;Scene=scene;Gate=gate;return Task;}
 }
}
namespace LastEpochLAN.Mod.OfflineTools
{public static class OfflineToolkit{public static bool Enabled=true;}public static class NewCharacterBoost{public static bool BlocksRoom;}}
namespace LastEpochLAN.Mod
{
 public class LanSharedChest{public static LanSharedChest Active=new();public bool Open,Busy;}
 public class LanGift{public static LanGift Active=new();public bool Busy;}
 public class LanMonolithParty{public static LanMonolithParty Active=new();public bool Busy;}
}
namespace MelonLoader{public static class MelonLogger{public static void Msg(string s){}public static void Warning(string s){}}}
