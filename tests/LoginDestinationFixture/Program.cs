using System.Reflection;
using System.Text.Json;
using Il2CppLE.Data;
using LastEpochLAN.Mod.OfflineTools;
var checks=new List<string>();void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);}
string Select(CharacterData character,string scene){object[] values={character,scene};typeof(OfflineLoginDestinationPatch).GetMethod("Postfix",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,values);return (string)values[1];}
var data=new CharacterData{IsOffline=true,ReachedTown=true,LastVisitedTownScene="Observatory"};
foreach(string saved in new[]{"Observatory","EoT","Bazaar","M_Rest"})Check(Select(data,saved)=="M_Rest","established offline login selects Rest from: "+saved);
Check(data.LastVisitedTownScene=="Observatory" && data.ReachedTown,"destination override does not rewrite save or campaign progression");
Check(Select(new(){IsOffline=true,ReachedTown=false},"C0_StartingScene")=="C0_StartingScene","fresh character keeps native introduction");
Check(Select(new(){IsOffline=false,ReachedTown=true},"Observatory")=="Observatory","online character never redirected");
OfflineToolkit.Enabled=false;Check(Select(data,"Observatory")=="Observatory","online environment retains native selection");OfflineToolkit.Enabled=true;
Check(Select(null,"native")=="native" && Select(data,null)==null && Select(data,"")=="","missing character or failed native destination preserved");
Directory.CreateDirectory("validation");File.WriteAllText("validation/login-destination-fixture.json",JsonSerializer.Serialize(new{passed=checks.Count,checks,actualSource=true,nativeGameplayValidated=false},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("PASS "+checks.Count+" actual-source login checks; native login pending");
namespace HarmonyLib{public class HarmonyPatch:Attribute{public HarmonyPatch(params object[] args){}}}
namespace Il2CppLE.Data{public class CharacterData{public bool IsOffline,ReachedTown;public string LastVisitedTownScene;}public static class CharacterDataExtensions{public static string GetInitialScene(CharacterData characterData,object gameVersion)=>"native";}}
namespace LastEpochLAN.Mod.OfflineTools{public static class OfflineToolkit{public static bool Enabled=true;}}
