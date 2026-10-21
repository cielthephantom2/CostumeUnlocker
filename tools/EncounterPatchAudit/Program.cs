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
var bloodrageTargets=new Dictionary<string,(string Type,string Method)>
{
 ["OfflineBloodrageChargeAnnouncementPatch"]=("Il2Cpp.RageOfMorditasSync","SendMorditasChargeGainedMessage"),
 ["OfflineBloodrageChargeCountPatch"]=("Il2Cpp.RageOfMorditasSync","GetBloodrageChargeCount"),
 ["OfflineBloodrageInitialRewardsPatch"]=("Il2Cpp.RageOfMorditasRewardLogic","Initialize")
};
var bloodrageUiTargets=new Dictionary<string,(string Type,string Method)>
{
 ["OfflineBloodrageHuntProgressTextPatch"]=("Il2Cpp.RageOfMorditasHuntProgress","SetBloodrageCharges"),
 ["OfflineBloodrageQuestProgressTextPatch"]=("Il2Cpp.RageOfMorditasQuestTrackerUI","UpdatePhaseVisuals")
};
foreach(var patch in patches)
{
 // CoF Rank7 offline quantity bonus is an explicitly retained gameplay bonus.
 if(patch.FullName=="LastEpochLAN.Mod.OfflineTools.FortuneMageLootPatch")continue;
 foreach(var attribute in patch.CustomAttributes.Where(a=>a.AttributeType.FullName=="HarmonyLib.HarmonyPatch"))
 foreach(var value in attribute.ConstructorArguments)
  if(value.Value is TypeReference type && banned.IsMatch(type.FullName))
  {
   bool authorizedCharge=patch.Namespace=="LastEpochLAN.Mod.OfflineTools" && bloodrageTargets.TryGetValue(patch.Name,out var charge) &&
    type.FullName==charge.Type && attribute.ConstructorArguments.Any(v=>Equals(v.Value,charge.Method));
   bool authorizedUi=patch.Namespace=="LastEpochLAN.Mod.OfflineTools" && bloodrageUiTargets.TryGetValue(patch.Name,out var ui) &&
    type.FullName==ui.Type && attribute.ConstructorArguments.Any(v=>Equals(v.Value,ui.Method));
   if(!authorizedCharge && !authorizedUi)violations.Add(patch.FullName+" -> "+type.FullName);
  }
 foreach(var owner in Walk(new[]{patch}))foreach(var method in owner.Methods.Where(m=>m.HasBody))
 foreach(var instruction in method.Body.Instructions)
 {
  var target=instruction.Operand switch {MethodReference m=>m.DeclaringType.FullName,TypeReference t=>t.FullName,_=>""};
  if(bloodrageUiTargets.TryGetValue(patch.Name,out var uiRead) && instruction.Operand is MethodReference uiGetter &&
    uiGetter.DeclaringType.FullName==uiRead.Type && uiGetter.Name is "get_chargesText" or "get_bloodrageChargesText" or "get__currentBloodChargeCount")continue;
  // Amber scaling reads its serialized base/slopes from the encounter data
  // asset. This exact read is allowed; interaction/reward execution stays banned.
  if(patch.Name=="LinearCorruptionAmberPatch" && instruction.Operand is MethodReference amberRead &&
     amberRead.DeclaringType.FullName=="Il2Cpp.RandomEncounterData" &&
     amberRead.Name is "get" or "get_silkenCocoonData")continue;
  if(banned.IsMatch(target))violations.Add(patch.FullName+" -> "+target);
 }
}
Check(violations.Count==0,"remaining Harmony patches exclude encounter interaction/replay except explicitly authorized local Bloodrage charge boundaries: "+string.Join(",",violations.Distinct()));
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
var glyphNames=new[]{"OfflineHopeForgeScopePatch","OfflineDespairChancePatch","OfflineGlyphDescriptionPatch"};
Check(glyphNames.All(name=>installed.Any(t=>t.Name==name)),"local glyph Forge/seal/description hooks are selected by inventory-only release policy");
var hopeForge=types.Single(t=>t.Name=="OfflineHopeForgeScopePatch");
var hopePrefixCalls=hopeForge.Methods.Single(m=>m.Name=="Prefix").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().ToArray();
Check(hopePrefixCalls.Any(m=>m.DeclaringType.Name=="OfflineToolkit" && m.Name=="get_Enabled") &&
 hopePrefixCalls.Any(m=>m.DeclaringType.Name=="PlayerFinder" && m.Name=="getPlayerActor") &&
 hopePrefixCalls.Any(m=>m.Name=="getItem") && hopePrefixCalls.Any(m=>m.DeclaringType.Name=="HopeForgeState" && m.Name==".ctor") &&
 hopeForge.Methods.Single(m=>m.Name=="Finalizer").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="Dispose"),
 "Hope Forge hook checks offline local actor/slotted glyph and cleans up its scoped override in a finalizer");
var hopeRollType=types.Single(t=>t.Name=="OfflineHopeRollHook");
var hopeRoll=hopeRollType.Methods.Single(m=>m.Name=="Prefix");
Check(hopeRoll.ReturnType.FullName=="System.Void" && hopeRoll.Parameters.Any(p=>p.Name=="__0" && p.ParameterType.FullName=="System.Single&") &&
 hopeRoll.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.DeclaringType.Name=="CraftingHopeScope" && m.Name=="Adjust") &&
 !hopeRoll.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="Roll"),
 "Hope edits only the scoped float probability, preserving native random roll and original craft execution");
Check(!patches.Contains(hopeRollType) && hopeRollType.Methods.Single(m=>m.Name=="Release").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="UnpatchSelf") &&
 types.Single(t=>t.Name=="HopeForgeState").Methods.Single(m=>m.Name=="Dispose").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.DeclaringType.Name=="OfflineHopeRollHook" && m.Name=="Release"),
 "Hope RNG hook is installed only for Forge leases and removed by finalization, with no permanent native Roll hook while mapping");
var seal=types.Single(t=>t.Name=="OfflineDespairChancePatch");
Check(seal.CustomAttributes.Any(a=>a.ConstructorArguments.Any(v=>Equals(v.Value,"getChanceToSealAffix"))) &&
 seal.Methods.Single(m=>m.Name=="Postfix").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.DeclaringType.Name=="GlyphCraftingRules" && m.Name=="DespairChance") &&
 !seal.Methods.SelectMany(m=>m.HasBody?m.Body.Instructions:Enumerable.Empty<Mono.Cecil.Cil.Instruction>()).Any(i=>i.Operand is MethodReference m && m.Name is "SealAffix" or "AddAffixTier" or "RebuildID"),
 "Despair patches its native chance getter rather than executing a separate seal/upgrade or changing item identity");
Check(core.MainModule.Types.Single(t=>t.Name=="CraftingHopeScope").Fields.Single(f=>f.Name=="Current").CustomAttributes.Any(a=>a.AttributeType.Name=="ThreadStaticAttribute"),
 "compiled Hope override uses thread-local crafting scope instead of a process-wide RNG replacement");
var linear=installed.Where(t=>t.Name.StartsWith("LinearCorruption",StringComparison.Ordinal)).ToArray();
Check(linear.Length==9 && linear.Any(t=>t.Name=="LinearCorruptionRunExperiencePatch") &&
 linear.Any(t=>t.Name=="LinearCorruptionEchoRarityPatch") && linear.Any(t=>t.Name=="LinearCorruptionPotentialRollPatch"),
 "linear Corruption installs real inlined EXP/rarity/LP consumers in addition to public preview helpers");
var linearTargets=linear.SelectMany(t=>t.CustomAttributes.Where(a=>a.AttributeType.Name=="HarmonyPatch"))
 .SelectMany(a=>a.ConstructorArguments).Where(a=>a.Value is TypeReference).Select(a=>((TypeReference)a.Value).FullName).ToArray();
Check(!linearTargets.Any(t=>t.Contains("RngElement") || t.Contains("Actor") || t.Contains("Monster")),
 "linear Corruption does not install global RNG/combat hooks or change monster power");
var lpLinear=types.Single(t=>t.Name=="LinearCorruptionPotentialRollPatch").Methods.Single(m=>m.Name=="Prefix");
Check(lpLinear.Parameters.Select(p=>p.Name).SequenceEqual(new[]{"__3","__6"}) &&
 !lpLinear.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name.Contains("Roll")),
 "LP linearization changes Corruption/non-CoF inputs only; native RNG, CoF attribution and max4 remain native");
var amberLinear=types.Single(t=>t.Name=="LinearCorruptionAmberPatch");
Check(amberLinear.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>()
 .Where(m=>m.DeclaringType.Name=="SilkenCocoonData" || m.DeclaringType.Name=="RandomEncounterData")
 .All(m=>m.Name=="get" || m.Name.StartsWith("get_",StringComparison.Ordinal)),
 "Amber hook only reads serialized asset values; no encounter state writes or additional drop execution");
var shadePatch=installed.Single(t=>t.Name=="OfflineShadeCorruptionPatch").Methods.Single(m=>m.Name=="Postfix");
Check(shadePatch.Parameters.Any(p=>p.Name=="__5" && p.ParameterType.FullName=="System.Int32") &&
 shadePatch.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="ShadeCorruption" && m.Parameters.Count==6),
 "Shade calculator supplies native highest-C input so catchup converges instead of x5 overshoot");
var shadeTrace=installed.Single(t=>t.Name=="OfflineShadeCorruptionCompletionTracePatch");
Check(!shadeTrace.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>()
 .Any(m=>m.Name.StartsWith("set_",StringComparison.Ordinal)),
 "Shade completion trace reads progress only and never changes corruption/save state");
var chargePatches=installed.Where(t=>bloodrageTargets.ContainsKey(t.Name)).ToArray();
Check(chargePatches.Length==3 && chargePatches.SelectMany(t=>t.Methods).Where(m=>m.HasBody).All(m=>
 m.Body.Instructions.Any(i=>i.Operand is MethodReference call && call.DeclaringType.Name=="OfflineToolkit" && call.Name=="get_Enabled") &&
 m.Body.Instructions.Any(i=>i.Operand is MethodReference call && call.DeclaringType.Name=="BloodrageChargeRules") &&
 !m.Body.Instructions.Any(i=>i.Operand is MethodReference call && (call.DeclaringType.Name.Contains("RageOfMorditas") || call.Name.StartsWith("set_")))),
 "Bloodrage doubles only offline earned/display/initial-currency boundaries, without native counter writes or extra reward execution");
Check(!chargePatches.SelectMany(t=>t.CustomAttributes).SelectMany(a=>a.ConstructorArguments).Any(a=>
 Equals(a.Value,"ConsumeReward") || Equals(a.Value,"FixedUpdate_Hunt") || Equals(a.Value,"GrantBloodrageCharge")),
 "Bloodrage native threshold progression, per-charge spending and timer stay unpatched");
var progressText=types.Single(t=>t.Name=="OfflineBloodrageProgressText").Methods.Single(m=>m.Name=="Refresh");
Check(installed.Count(t=>bloodrageUiTargets.ContainsKey(t.Name))==2 &&
 progressText.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.DeclaringType.Name=="OfflineToolkit" && m.Name=="get_Enabled") &&
 progressText.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.DeclaringType.Name=="BloodrageChargeRules" && m.Name=="Earned") &&
 progressText.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Where(m=>m.Name.StartsWith("set_",StringComparison.Ordinal))
 .All(m=>m.Name=="set_text"),
 "Bloodrage hunt/quest counters scale only the UI maximum; helper writes text only, no threshold/currency/save mutations");
var assistRuntime=types.Single(t=>t.Name=="AutoPlayRuntime");
var assistTick=assistRuntime.Methods.Single(m=>m.Name=="Tick");
Check(assistTick.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.DeclaringType.Name=="AutoPlayRecovery" && m.Name=="Failed") &&
 assistRuntime.Fields.Any(f=>f.Name=="LastRunStatus") && assistRuntime.Fields.Any(f=>f.Name=="pauseReason"),
 "Assist retains bounded native-read recovery and observable last-run/pause reason without new global hooks");
var assistSchedule=Walk(core.MainModule.Types).Single(t=>t.Name=="AutoPlaySchedule");
Check(assistSchedule.Fields.Any(f=>f.Name=="intervals") && assistSchedule.Methods.Any(m=>m.Name=="WaitSeconds") &&
 types.Single(t=>t.Name=="LanMod").Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Count(i=>
 i.Operand is MethodReference call && call.DeclaringType.Name=="AutoPlayRuntime" && call.Name=="Tick")==1,
 "Assist timer diagnostics/config-change tracking keep a single local scheduler tick");
var modUpdate=types.Single(t=>t.Name=="LanMod").Methods.Single(m=>m.Name=="OnUpdate");
Check(!modUpdate.Body.Instructions.Any(i=>i.Operand is FieldReference f && f.DeclaringType.Name=="OfflineToolkit" && f.Name=="PanelOpen") &&
 modUpdate.Body.Instructions.Any(i=>i.Operand is FieldReference f && f.DeclaringType.Name=="LanMod" && f.Name=="panel") &&
 modUpdate.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.DeclaringType.Name=="AutoPlayRuntime" && m.Name=="Tick"),
 "DPS Settings -> close F4 regression: Assist reads visible root panel, never stale hidden Settings page flag");
File.WriteAllText(args[2],JsonSerializer.Serialize(new{passed=checks.Count,patchesInspected=patches.Length,
 installedPatches=installed.Select(t=>t.FullName),skippedPatches=skipped.Select(t=>t.FullName),
 scope="compiled metadata and IL audit; not gameplay acceptance",checks},new JsonSerializerOptions{WriteIndented=true}));
