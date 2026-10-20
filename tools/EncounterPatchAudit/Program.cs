using Mono.Cecil;
using System.Text.Json;
using System.Text.RegularExpressions;

var resolver=new DefaultAssemblyResolver();
resolver.AddSearchDirectory(Path.Combine(args[3],"MelonLoader","net6"));
resolver.AddSearchDirectory(Path.Combine(args[3],"MelonLoader","Il2CppAssemblies"));
resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location));
using var mod=AssemblyDefinition.ReadAssembly(args[0],new ReaderParameters{AssemblyResolver=resolver});
using var core=AssemblyDefinition.ReadAssembly(args[1],new ReaderParameters{AssemblyResolver=resolver});
IEnumerable<TypeDefinition> Walk(IEnumerable<TypeDefinition> types)=>types.SelectMany(t=>new[]{t}.Concat(Walk(t.NestedTypes)));
var legacy=new Regex("^LastEpochLAN.Mod.Native(EncounterJournal|EncounterReplica|Nemesis|HostNemesis|OmenRewards|Shrine)");
var banned=new Regex("Nemesis|ObserverWindow|RageOfMorditas|RunePrison|TimeBeast|Shrine|RandomEncounter|ConditionHandler|WorldEventTriggerArea|SceneStateGraph|PlayerDistanceCondition|PlayerNotInRangeCondition");
var checks=new List<string>();
void Check(bool good,string text){if(!good)throw new InvalidOperationException(text);checks.Add(text);Console.WriteLine("PASS: "+text);}
var types=Walk(mod.MainModule.Types).ToArray();
Check(!types.Any(t=>legacy.IsMatch(t.FullName)),"legacy encounter adapters and global Nemesis/Omen/shrine classes absent from binary");
Check(!types.Any(t=>t.Name is "LanMapTriggers" or "NativeSceneEvents" or "NativeMapObjectReplica"),"generic callback/state-graph/object-state replay engines absent");
var patches=types.Where(t=>t.CustomAttributes.Any(a=>a.AttributeType.FullName=="HarmonyLib.HarmonyPatch")).ToArray();
var violations=new List<string>();
foreach(var patch in patches)
{
 // CoF Rank7 offline quantity bonus is an explicitly retained gameplay bonus.
 if(patch.FullName=="LastEpochLAN.Mod.OfflineTools.FortuneMageLootPatch")continue;
 foreach(var attribute in patch.CustomAttributes.Where(a=>a.AttributeType.FullName=="HarmonyLib.HarmonyPatch"))
 foreach(var value in attribute.ConstructorArguments)
  if(value.Value is TypeReference type && banned.IsMatch(type.FullName))violations.Add(patch.FullName+" -> "+type.FullName);
 foreach(var owner in Walk(new[]{patch}))foreach(var method in owner.Methods.Where(m=>m.HasBody))
 foreach(var instruction in method.Body.Instructions)
 {
  var target=instruction.Operand switch {MethodReference m=>m.DeclaringType.FullName,TypeReference t=>t.FullName,_=>""};
  if(banned.IsMatch(target))violations.Add(patch.FullName+" -> "+target);
 }
}
Check(violations.Count==0,"remaining Harmony patches do not target encounter or generic interaction types: "+string.Join(",",violations.Distinct()));
var oldTypes=new[]{"EncounterState","EncounterAuthority","EncounterVisualPayload","NemesisRounds","NemesisCommand","OmenItemReward","WorldInteractionRequest"};
Check(!Walk(core.MainModule.Types).Any(t=>oldTypes.Contains(t.Name)),"legacy encounter payloads and skill-RPC model absent from Core");
Check(core.MainModule.Types.First(t=>t.Name=="WorldState").Properties.All(p=>p.Name!="Encounters"),"NPC world snapshots have no legacy encounter side channel");
Check(core.MainModule.Types.First(t=>t.Name=="SkillEvent").Properties.All(p=>p.Name!="MapRequest"),"skill visual channel cannot carry legacy interaction or reward acknowledgements");
Check(types.Any(t=>t.Name=="NativePlayerAnimationReplica") && types.Any(t=>t.Name=="NativeSkillVisualReplica") && types.Any(t=>t.Name=="HostEnemyReplicas"),"NPC animation, remote VFX and enemy replicas remain compiled");
var catalogue=types.Single(t=>t.Name=="NativeEncounterCatalogue");
var catalogueCalls=Walk(new[]{catalogue}).SelectMany(t=>t.Methods).Where(m=>m.HasBody)
 .SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>().ToArray();
Check(!catalogue.CustomAttributes.Any(a=>a.AttributeType.FullName=="HarmonyLib.HarmonyPatch") &&
 !catalogueCalls.Any(m=>((m.DeclaringType.Namespace=="Il2Cpp" || m.DeclaringType.Namespace.StartsWith("Il2CppLE.")) && !m.Name.StartsWith("get_") &&
  !(m.DeclaringType.FullName=="Il2Cpp.RageOfMorditasLogic" && m.Name=="GetState")) ||
  m.DeclaringType.FullName=="UnityEngine.Object" && m.Name is "Destroy" or "Instantiate"),
 "development encounter catalogue has no Harmony patch or native gameplay mutation calls");
Check(catalogue.Fields.Any(f=>f.Name=="Enabled" && f.IsStatic && f.IsInitOnly) &&
 catalogue.Methods.Single(m=>m.Name==".cctor").Body.Instructions.Any(i=>Equals(i.Operand,"--lelan-encounter-development")),
 "native encounter discovery requires explicit development switch");
File.WriteAllText(args[2],JsonSerializer.Serialize(new{passed=checks.Count,patchesInspected=patches.Length,scope="compiled metadata and IL audit; not gameplay acceptance",checks},new JsonSerializerOptions{WriteIndented=true}));
