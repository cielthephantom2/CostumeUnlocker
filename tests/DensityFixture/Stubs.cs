namespace HarmonyLib { public class HarmonyPatch:Attribute{public HarmonyPatch(params object[] args){}} public class HarmonyPrefix:Attribute{} public class HarmonyFinalizer:Attribute{} }
namespace Il2Cpp {
 public class SpawnerPlacementRoom {
  public class SpawnerRuntimeConfig{}
  public bool locked;private float modifier=.25f,target=10;public int Writes;
  public float spawnerDensityModifier {get=>modifier;set{Writes++;modifier=value;}}
  public float intendedSpawnerDensity {get=>target;set{Writes++;target=value;}}
 }
 public class MonolithZoneManager {public static MonolithZoneManager instance;public GameObject gameObject=new();}
 public class GameObject{public Scene scene=new();}public class Scene{public bool isLoaded=true;}
}
namespace Il2CppLE.Gameplay.Monolith {public static class MonolithGameplayManager{public static Run ActiveRun;}public class Run{public Web web=new();}public class Web{public int corruption;}}
namespace LastEpochLAN.Mod {public static class HudAuthority{public static Session Session;}public class Session{public bool Connected,IsHost;}}
namespace LastEpochLAN.Mod.OfflineTools {public static class OfflineToolkit{public static bool Enabled=true;}}
namespace MelonLoader {public static class MelonLogger{public static void Warning(string message){}}}
