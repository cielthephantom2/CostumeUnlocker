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
Check(!types.Any(t=>t.Name.Contains("Disintegrate") || t.Name.Contains("Ascendance")) &&
 !Walk(core.MainModule.Types).Any(t=>t.Name=="ChannelLocomotionRules"),
 "Disintegrate movement, animation, root overrides and related Core helpers are absent from binary");
var skillOverrides=types.SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions)
 .Select(i=>i.Operand).OfType<MemberReference>().Where(m=>m.DeclaringType!=null &&
  (m.DeclaringType.Name.Contains("Disintegrate") || m.DeclaringType.Name.Contains("ArcaneAscendance") || m.DeclaringType.Name=="ModifyDamageOnRotation")).ToArray();
Check(skillOverrides.Length==0,"compiled mod has no direct references to Disintegrate/Ascendance or rotation-penalty override APIs");
Check(mod.CustomAttributes.Any(a=>a.AttributeType.Name=="HarmonyDontPatchAllAttribute"),
 "Melon automatic PatchAll is disabled; release installs only selected inventory/local hooks");
var policy=types.Single(t=>t.Name=="RoomFeaturePolicy");
var enabled=policy.Methods.Single(m=>m.Name=="get_SharedWorldEnabled").Body.Instructions;
Check(enabled.Count==2 && enabled[0].OpCode.Code==Mono.Cecil.Cil.Code.Ldc_I4_0 && enabled[1].OpCode.Code==Mono.Cecil.Cil.Code.Ret,
 "compiled release shared-world policy is false with no configuration or command-line override");
string ReflectionNamespace(TypeDefinition type)=>type.IsNested ? ReflectionNamespace(type.DeclaringType) : type.Namespace;
var installed=patches.Where(t=>LastEpochLAN.Mod.RoomFeaturePolicy.AllowsPatch(ReflectionNamespace(t),t.Name,t.DeclaringType?.Name??"")).ToArray();
var skipped=patches.Except(installed).ToArray();
Check(installed.Any(t=>t.Name=="SharedChestItemLockPatch") && installed.Any(t=>t.Name=="SharedStashProjectionMutationPatch") &&
 installed.Any(t=>t.DeclaringType?.Name=="LanLootFilters") && installed.Any(t=>t.Name=="AutoPlayCastObservationPatch") &&
 installed.Any(t=>t.Name=="DpsActorDamagePatch"), "selected compiled hooks retain F2 custody/projection, Loot Filter labels, local Assist and DPS");
Check(skipped.Any(t=>t.Name=="NativeGlobalVisualCapturePatch") && skipped.Any(t=>t.Name=="ReplicaAiPatch") &&
 skipped.Any(t=>t.Name=="SkillCapture") && skipped.Any(t=>t.Name=="SharedEchoCompletionPatch") &&
 !installed.Any(t=>t.Namespace=="LastEpochLAN.Mod" && t.Name.Contains("Visual")),
 "network VFX, skill capture, enemy AI and shared Monolith hooks are not selected for installation");
var installer=types.Single(t=>t.Name=="RoomPatches").Methods.Single(m=>m.Name=="Install");
Check(installer.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.DeclaringType.Name=="RoomFeaturePolicy" && m.Name=="AllowsPatch") &&
 installer.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="CreateClassProcessor") &&
 !types.SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is MethodReference m && m.Name=="PatchAll"),
 "runtime patch installation uses the release allowlist without a PatchAll bypass");
IEnumerable<Mono.Cecil.Cil.Instruction> Reachable(MethodDefinition method)
{
 var pending=new Stack<Mono.Cecil.Cil.Instruction>();var visited=new HashSet<Mono.Cecil.Cil.Instruction>();pending.Push(method.Body.Instructions[0]);
 while(pending.TryPop(out var instruction))
 {
  if(!visited.Add(instruction))continue;
  yield return instruction;
  if(instruction.Operand is MethodReference target && target.DeclaringType.Name=="RoomFeaturePolicy" && target.Name=="get_SharedWorldEnabled")
  {
   var branch=instruction.Next;
   if(branch?.OpCode.Code is Mono.Cecil.Cil.Code.Brtrue or Mono.Cecil.Cil.Code.Brtrue_S)
   {if(branch.Next!=null)pending.Push(branch.Next);continue;}
   if(branch?.OpCode.Code is Mono.Cecil.Cil.Code.Brfalse or Mono.Cecil.Cil.Code.Brfalse_S)
   {pending.Push((Mono.Cecil.Cil.Instruction)branch.Operand);continue;}
   throw new InvalidOperationException("Unrecognized shared-world guard IL; inspect before releasing.");
  }
  if(instruction.OpCode.FlowControl is Mono.Cecil.Cil.FlowControl.Return or Mono.Cecil.Cil.FlowControl.Throw)continue;
  if(instruction.Operand is Mono.Cecil.Cil.Instruction jump)pending.Push(jump);
  if(instruction.Operand is Mono.Cecil.Cil.Instruction[] jumps)foreach(var jumpTarget in jumps)pending.Push(jumpTarget);
  if(instruction.OpCode.FlowControl!=Mono.Cecil.Cil.FlowControl.Branch && instruction.Next!=null)pending.Push(instruction.Next);
 }
}
var lan=types.Single(t=>t.Name=="LanMod");
var reachable=Reachable(lan.Methods.Single(m=>m.Name=="OnUpdate")).Select(i=>i.Operand).OfType<MethodReference>().ToArray();
Check(!reachable.Any(m=>m.Name=="Tick" && m.DeclaringType.Name is "WorldSynchronizer" or "NativeGuestCombat" or "LanPartyTravel" or "LanMonolithParty" or "NativeVisualEvents" or "NativePartyReplica" or "NativeEncounterCatalogue") &&
 !reachable.Any(m=>m.Name=="UpdateMarkers"), "compiled OnUpdate cannot reach shared-world/combat/map/encounter/remote rendering updates under release policy");
Check(reachable.Any(m=>m.Name=="PublishInventoryPresence") && reachable.Any(m=>m.Name=="Tick" && m.DeclaringType.Name=="LanSharedChest") &&
 reachable.Any(m=>m.Name=="Tick" && m.DeclaringType.Name=="LanLootFilters"), "compiled OnUpdate still processes inventory presence, F2 and room filters");
Check(!Reachable(lan.Methods.Single(m=>m.Name=="OnLateUpdate")).Any(i=>i.Operand is MethodReference m && m.DeclaringType.Name!="RoomFeaturePolicy"),
 "compiled OnLateUpdate returns before native party status, cosmetics, summon and enemy pose work");
var presenceCalls=lan.Methods.Single(m=>m.Name=="PublishInventoryPresence").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().ToArray();
Check(!presenceCalls.Any(m=>m.DeclaringType.Name is "ClassVisualReplica" or "NativeSummonCapture" or "LanDroppedItems" or "NativeSkillVisualCapture"),
 "inventory presence does not capture avatar, summons, ground items or VFX");
File.WriteAllText(args[2],JsonSerializer.Serialize(new{passed=checks.Count,patchesInspected=patches.Length,
 installedPatches=installed.Select(t=>t.FullName),skippedPatches=skipped.Select(t=>t.FullName),
 scope="compiled metadata and IL audit; not gameplay acceptance",checks},new JsonSerializerOptions{WriteIndented=true}));
