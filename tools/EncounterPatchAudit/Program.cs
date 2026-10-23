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
Check(!types.Any(t=>t.Name is "RuntimePerformance" or "DeathHitchTrace" or "OfflineDeathHitchTracePatch" or "OfflineEchoEnemyAggroActivationTracePatch"),
 "temporary frame/allocation/death/loot and aggro-activation measuring code is absent from normal binary");
var saveRuntime=types.Single(t=>t.Name=="LanMod").Methods.Single(m=>m.Name=="SaveRuntimeReport");
Check(!saveRuntime.Body.Instructions.Any(i=>i.Operand is MethodReference),
 "release runtime report has no capture/serialization/writer calls");
Check(!types.Any(t=>t.Name.Contains("Smoke",StringComparison.Ordinal) || t.Name.Contains("Probe",StringComparison.Ordinal) || t.Name=="HostWorldAudit") &&
 !types.SelectMany(t=>t.Methods).Any(m=>m.Name=="Smoke" || m.Name.EndsWith("Smoke",StringComparison.Ordinal)),
 "release excludes QA probe/smoke classes and inline smoke entry points: "+string.Join(",",types.Where(t=>t.Name.Contains("Smoke") || t.Name.Contains("Probe") || t.Name=="HostWorldAudit").Select(t=>t.FullName).Concat(types.SelectMany(t=>t.Methods).Where(m=>m.Name=="Smoke" || m.Name.EndsWith("Smoke")).Select(m=>m.FullName))));
Check(!Walk(core.MainModule.Types).Any(t=>t.Name=="LatestReportWriter"),"release Core excludes temporary runtime report writer");
Check(!types.Any(t=>legacy.IsMatch(t.FullName)),"legacy encounter adapters and global Nemesis/Omen/shrine classes absent from binary");
Check(!types.Any(t=>t.Name is "LanMapTriggers" or "NativeSceneEvents" or "NativeMapObjectReplica"),"generic callback/state-graph/object-state replay engines absent");
Check(!types.Any(t=>t.Name.Contains("DefaultMeteor",StringComparison.Ordinal) || t.Name=="OfflineRemovedMeteorRepairPatch"),"retired Default Meteor and save-repair hook absent from release binary");
Check(!types.Any(t=>t.Name is "QolRules" or "NativeQaNaturalBoot") && !types.Single(t=>t.Name=="MeteorNodeRules").Methods.Any(m=>m.Name=="LargeMeteorChance"),"unused pickup/reveal/QA boot and old Meteor chance helpers removed");
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
Check(skillOverrides.All(m=>m.DeclaringType.Name is "ArcaneAscendanceTree" or "ArcaneAscendanceMutator"),
 "skill overrides exclude Disintegrate and rotation penalties; Ascendance references are restricted to its tree and mutator");
var tranquilityHooks=new[]{"OfflineArcaneTranquilityTreePatch","OfflineArcaneTranquilityCastPatch","OfflineArcaneTranquilityDescriptionPatch","OfflineArcaneTranquilityLegacyTooltipPatch","OfflineArcaneTranquilityTooltipPatch"};
Check(tranquilityHooks.All(n=>patches.Any(t=>t.Name==n)),"Tranquility actual-stat, description and both tooltip hooks are compiled");
Check(mod.CustomAttributes.Any(a=>a.AttributeType.Name=="HarmonyDontPatchAllAttribute"),
 "Melon automatic PatchAll is disabled; release installs only selected inventory/local hooks");
var policy=types.Single(t=>t.Name=="RoomFeaturePolicy");
var enabled=policy.Methods.Single(m=>m.Name=="get_SharedWorldEnabled").Body.Instructions;
Check(enabled.Count==2 && enabled[0].OpCode.Code==Mono.Cecil.Cil.Code.Ldc_I4_0 && enabled[1].OpCode.Code==Mono.Cecil.Cil.Code.Ret,
 "compiled release shared-world policy is false with no configuration or command-line override");
string ReflectionNamespace(TypeDefinition type)=>type.IsNested ? ReflectionNamespace(type.DeclaringType) : type.Namespace;
var installed=patches.Where(t=>LastEpochLAN.Mod.RoomFeaturePolicy.AllowsPatch(ReflectionNamespace(t),t.Name,t.DeclaringType?.Name??"")).ToArray();
Check(new[]{"OfflineEchoSpawnEnqueuePatch","OfflineEchoSpawnDrainPatch"}.All(n=>installed.Any(t=>t.Name==n)),
 "dense echo native enqueue routing and post-load drain guard both install");
Check(types.Single(t=>t.Name=="OfflineEchoSpawnEnqueuePatch").Methods.Any(m=>m.Name=="Finalizer") &&
 !types.Single(t=>t.Name=="OfflineEchoSpawnQueue").Methods.SelectMany(m=>m.HasBody?m.Body.Instructions:Enumerable.Empty<Mono.Cecil.Cil.Instruction>()).Any(i=>i.Operand is MethodReference m && m.Name is "set_numberToSpawn" or "set_totalSpawnCount" or "set_spawnsRemaining"),
 "echo queue routing restores pooled spawner flag and does not reduce generation counts");
Check(!types.Any(t=>t.Name.Contains("Vilatria",StringComparison.Ordinal)) &&
 !types.Single(t=>t.Name=="ToolkitSettings").Properties.Any(p=>p.Name=="IgnoreVilatriaMeteorVisuals"),
 "Vilatria visual override hooks and setting are completely removed");
var newMeteorHooks=new[]{"OfflineSpiritXylemLocalizationPatch","OfflineMeteorStardustTreePatch","OfflineMeteorStardustHasStatsPatch","OfflineMeteorStardustStatsPatch","OfflineMeteorStardustLimitPatch","OfflineMeteorStardustNodePatch","OfflineMeteorStardustTextPatch","OfflineMeteorStardustDescriptionPatch","OfflineMeteorStardustAltPatch","OfflineMeteorStardustLabelPatch"};
var classMenuHooks=new[]{"OfflineClassRespecMenuPatch","OfflineClassRespecClickPatch","OfflineClassRespecClosePatch","OfflineClassRespecUnityMenuPatch","OfflineClassRespecUnityClickPatch","OfflineClassRespecUnityClosePatch"};
Check(newMeteorHooks.All(n=>installed.Any(t=>t.Name==n)) && installed.Any(t=>t.Name=="OfflineClassRespecLoadGatePatch") && !types.Any(t=>classMenuHooks.Contains(t.Name)),"Stardust and class save load gate install; NPC class hooks removed");
var classRuntime=types.Single(t=>t.Name=="ClassRespecialization");
Check(types.Single(t=>t.Name=="OfflineToolkit").Methods.Single(m=>m.Name=="Draw").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.DeclaringType.Name=="ClassRespecialization" && m.Name=="Draw") && classRuntime.Methods.Any(m=>m.Name=="Confirm"),"F4 Mod Settings draws Reset Class with explicit confirmation");
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
var updateCalls=lan.Methods.Single(m=>m.Name=="OnUpdate").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().ToArray();
int assistOrder=Array.FindIndex(updateCalls,m=>m.DeclaringType.Name=="AutoPlayRuntime" && m.Name=="Tick");
Check(assistOrder>=0 && assistOrder<Array.FindIndex(updateCalls,m=>m.DeclaringType.Name=="LanGift" && m.Name=="Tick") && assistOrder<Array.FindIndex(updateCalls,m=>m.DeclaringType.Name=="LanSharedChest" && m.Name=="Tick"),
 "local Assist ticks before room gift/stash polling can fail");
var stopBody=lan.Methods.Single(m=>m.Name=="StopRoom").Body.Instructions;
Check(stopBody.Any(i=>i.Operand is MethodReference m && m.DeclaringType.Name=="AutoPlayRuntime" && m.Name=="RoomDisconnected"),
 "room disconnect refreshes local Assist without Suspend/Dispose");
Check(!stopBody.Any(i=>i.Operand is MethodReference m && m.DeclaringType.Name=="AutoPlayRuntime" && m.Name is "Suspend" or "Dispose"),
 "room disconnect never suspends or disposes local Assist");
var refresh=types.Single(t=>t.Name=="AutoPlayRuntime").Methods.Single(m=>m.Name=="RoomDisconnected");
Check(!refresh.Body.Instructions.Any(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Stfld && i.Operand is FieldReference f && f.Name is "Armed" or "Open" or "Settings" or "character" or "profilePath"),
 "disconnect preserves armed/configuration/profile state");
Check(stopBody.Any(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Stfld && i.Operand is FieldReference f && f.Name=="panel") && stopBody.Any(i=>i.Operand is FieldReference f && f.Name=="PanelOpen"),
 "disconnect closes F4 root and its remembered Mod Settings page");
var lateCalls=Reachable(lan.Methods.Single(m=>m.Name=="OnLateUpdate")).Select(i=>i.Operand).OfType<MethodReference>().ToArray();
Check(lateCalls.All(m=>m.DeclaringType.Name=="RoomFeaturePolicy"),
 "compiled OnLateUpdate stops at the release policy guard");
Check(!types.Any(t=>t.Namespace.Contains("WorldAssist") || t.Name is "AssistAbilityEnabledPatch" or "AssistMapFramePatch"),
 "World Assist runtime, drawing, settings, routes and hooks are absent from the binary");
var balanceHooks=new[]{"OfflineWovenRankPatch","OfflineWovenRankDescriptionPatch","OfflineNativeAutoPickupGatePatch","OfflineAutoPickupItemFilterPatch","OfflineDelayedPickupInteractionPatch","OfflineSpecialPickupPatch"};
Check(balanceHooks.All(n=>installed.Any(t=>t.Name==n)),"Woven and delayed map-wide pickup hooks are selected for installation");
Check(types.Single(t=>t.Name=="OfflineGoldPickupPatch").Methods.Single(m=>m.Name=="Prefix").ReturnType.FullName=="System.Void",
 "gold bonus scales once before native ground placement");
var pickup=types.Single(t=>t.Name=="OfflinePickup");
Check(pickup.Methods.Any(m=>m.Name=="Tick") && !pickup.Methods.Any(m=>m.Name is "Moving" or "Attach" or "Configure" or "CanAutoPick") &&
 !types.Any(t=>t.Name is "OfflineNativeItemTriggerPatch" or "OfflineNativePickupRadiusPatch" or "OfflineNativeItemPickupPatch"),
 "drop-driven delayed pickup removes proximity bridges, movement gates and radius checks");
Check(!types.Any(t=>t.Name=="OfflineAutoPickupVacuumGatePatch"),"delayed pickup retains native primary stackable vacuum/storage dispatch");
var itemFilter=types.Single(t=>t.Name=="OfflineAutoPickupItemFilterPatch");
Check(itemFilter.CustomAttributes.Any(a=>a.AttributeType.FullName=="HarmonyLib.HarmonyPatch" && a.ConstructorArguments.Any(c=>c.Value is TypeReference t && t.Name=="ItemList")),
 "automatic item scope prevents native vacuum from collecting unapproved/newer drops before deadline");
var localPickupCalls=pickup.Methods.Where(m=>m.Name is "Local" or "Owned").SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>().ToArray();
Check(localPickupCalls.Any(m=>m.Name=="get_Enabled") && localPickupCalls.Any(m=>m.Name=="get_itemContainersManager") && !localPickupCalls.Any(m=>m.Name=="get_Ready"),
 "pickup requires offline local storage without model/network visual readiness");
var pickupTickCalls=pickup.Methods.Single(m=>m.Name=="Tick").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().ToArray();
var pickupTransactionCalls=pickup.Methods.Where(m=>m.Name is "Tick" or "PickupGold").SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>().ToArray();
Check(new[]{"pickupItem","pickupGold","pickupPotion","pickupAncientBone"}.All(name=>pickupTransactionCalls.Any(m=>m.Name==name)) &&
 !pickupTransactionCalls.Any(m=>m.Name is "modifyGold" or "modifyAncientBones" or "set_currentCharges"),
 "all delayed pickups use native storage/currency methods without direct grants");
var pickupPolicy=types.Single(t=>t.Name=="PickupRules");
Check((float)pickupPolicy.Fields.Single(f=>f.Name=="Delay").Constant==.5f && !pickupPolicy.Fields.Any(f=>f.Name is "Radius" or "SweepInterval" or "MaximumVisits"),
 "fixed500ms per-drop delay replaces radius and polling/count limits");
Check(pickupTickCalls.Any(m=>m.Name=="TryPeek") && pickupTickCalls.Any(m=>m.Name=="Dequeue") &&
 types.Single(t=>t.Name=="OfflineToolkit").Methods.Single(m=>m.Name=="Tick").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.DeclaringType.Name=="OfflinePickup" && m.Name=="Tick"),
 "Unity callback processes the deadline queue, without per-drop tasks or new native trigger objects");
var prepareCalls=pickup.Methods.Single(m=>m.Name=="Prepare").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().ToArray();
Check(prepareCalls.Any(m=>m.Name=="position") && prepareCalls.Any(m=>m.Name=="Special") &&
 pickup.Methods.Single(m=>m.Name=="Dropped").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="get_itemData"),
 "old special-drop placement is restored and actual native ground IDs/data are queued after drop");
var cleanupHooks=new[]{"OfflineNativePickupScenePatch","OfflineNativePickupZonePatch","OfflineNativePickupDisposePatch"};
Check(cleanupHooks.All(n=>installed.Any(t=>t.Name==n)),"native pre-unload/zone/dispose cleanup cancels queued work before ground IDs can be reused");
Check(!types.Single(t=>t.Name=="OfflineToolkit").Methods.Single(m=>m.Name=="SceneChanged").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.DeclaringType.Name=="OfflinePickup" && m.Name=="Clear"),
 "additive scene-loaded notifications cannot cancel a live pickup delay");
var nativePickupGate=types.Single(t=>t.Name=="OfflineNativeAutoPickupGatePatch");
Check(nativePickupGate.Methods.Single(m=>m.Name=="Prefix").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="AllowNative"),
 "native automatic interaction action checks its queued deadline");
Check(!pickup.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is MethodReference m && m.DeclaringType.Name=="MelonLogger" && m.Name is "Msg" or "Log"),
 "pickup emits no per-drop diagnostic logs; native errors remain observable");
Check(pickup.Methods.Single(m=>m.Name=="Special").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="isWovenEcho") && types.Single(t=>t.Name=="ToolkitSettings").Properties.Any(p=>p.Name=="WovenEchoes"),
 "Woven Echo eligibility and independent toggle remain");
var pickupLabels=types.Single(t=>t.Name=="OfflineToolkit").Methods.Single(m=>m.Name=="Draw").Body.Instructions.Select(i=>i.Operand).OfType<string>().ToArray();
Check(pickupLabels.Count(value=>value.Contains("all map, 0.5 s"))==6 && !pickupLabels.Any(value=>value.Contains("Auto ") && value.Contains("radius 5")),
 "all six pickup labels show map-wide500ms behavior rather than obsolete radius5");
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
Check(!types.Any(t=>t.Name.StartsWith("OfflineProphecy",StringComparison.Ordinal) &&
 t.Name is not ("OfflineProphecySpawnPatch" or "OfflineProphecyTransitionPatch")) &&
 !patches.SelectMany(t=>t.CustomAttributes.Where(a=>a.AttributeType.Name=="HarmonyPatch")).SelectMany(a=>a.ConstructorArguments)
 .Any(a=>a.Value is TypeReference t && t.Name=="ProphecySlot"),
 "custom prophecy charge runtime, transaction/UI hooks and notification overrides are absent; native charge behavior retained");
var prophecyStream=types.Single(t=>t.Name=="ProphecyRewards");
var prophecyCalls=Walk(new[]{prophecyStream}).SelectMany(t=>t.Methods).Where(m=>m.HasBody)
 .SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>().ToArray();
Check(!prophecyCalls.Any(m=>m.DeclaringType.Name=="ProphecySlot") &&
 !prophecyCalls.Any(m=>m.Name.Contains("TryTriggerReward") || m.Name.Contains("CompletedCharges") || m.Name.Contains("ChargeInProgress")),
 "deferred Prophecy delivery never replays trigger events or reads/writes charge/progress counters");
Check(prophecyCalls.Count(m=>m.DeclaringType.Name=="NativeProphecyRewardSpawn" && m.Name=="Deliver")==1 &&
 prophecyCalls.Any(m=>m.Name=="Instantiate" && m.DeclaringType.FullName=="UnityEngine.Object") &&
 prophecyStream.Methods.Single(m=>m.Name=="Tick").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="get_frameCount"),
 "Prophecy delivery uses isolated reward snapshots and a frame-limited native spawn consumer");
var prophecyAbi=types.Single(t=>t.Name=="NativeProphecyRewardSpawn");
Check(prophecyAbi.Methods.Single(m=>m.Name=="Receive").Parameters[2].ParameterType.FullName=="System.UInt64" &&
 !patches.SelectMany(t=>t.CustomAttributes.Where(a=>a.AttributeType.Name=="HarmonyPatch")).SelectMany(a=>a.ConstructorArguments)
 .Any(a=>a.Value is TypeReference t && t.Name=="ProphecySlotReward"),
 "native Prophecy lens uses eight-byte value ABI; no Harmony Nullable lens argument bridge");
var assistRuntime=types.Single(t=>t.Name=="AutoPlayRuntime");
var assistTick=assistRuntime.Methods.Single(m=>m.Name=="Tick");
Check(assistTick.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.DeclaringType.Name=="AutoPlayRecovery" && m.Name=="Failed") &&
 assistRuntime.Fields.Any(f=>f.Name=="LastRunStatus") && assistRuntime.Fields.Any(f=>f.Name=="pauseReason"),
 "Assist retains bounded native-read recovery and observable last-run/pause reason without new global hooks");
var assistCalls=assistTick.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().ToArray();
int vitalsAt=Array.FindIndex(assistCalls,m=>m.Name=="ReadVitals"),potionAt=Array.FindIndex(assistCalls,m=>m.Name=="TryPotion");
Check(vitalsAt>=0 && vitalsAt<potionAt && potionAt<Array.FindIndex(assistCalls,m=>m.Name=="Scan") && potionAt<Array.FindIndex(assistCalls,m=>m.Name=="ReadSkills"),
 "Assist checks native potion before target registry walks and five-slot reads");
Check(assistCalls.Any(m=>m.DeclaringType.Name=="AutoPlayCadence" && m.Name=="ReadFrameDue"),
 "running Assist uses per-frame read policy including open configuration UI");
Check(assistCalls.Any(m=>m.DeclaringType.Name=="AutoPlaySchedule" && m.Name=="SelectWhileHolding") && assistCalls.Any(m=>m.DeclaringType.Name=="AutoPlaySchedule" && m.Name=="Ready"),
 "owned channel yields to eligible periodic slot and revalidates queued conditions");
Check(!assistCalls.Any(m=>m.DeclaringType.Namespace.StartsWith("System.Threading") || m.Name=="StartCoroutine"),
 "Assist native reads and actions remain on its single Unity callback thread");
var assistSchedule=Walk(core.MainModule.Types).Single(t=>t.Name=="AutoPlaySchedule");
Check(assistSchedule.Fields.Any(f=>f.Name=="intervals") && assistSchedule.Methods.Any(m=>m.Name=="WaitSeconds") &&
 types.Single(t=>t.Name=="LanMod").Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Count(i=>
 i.Operand is MethodReference call && call.DeclaringType.Name=="AutoPlayRuntime" && call.Name=="Tick")==1,
 "Assist timer diagnostics/config-change tracking keep a single local scheduler tick");
var modUpdate=types.Single(t=>t.Name=="LanMod").Methods.Single(m=>m.Name=="OnUpdate");
Check(assistTick.Parameters.Count==1 && assistTick.Parameters[0].Name=="transition" &&
 modUpdate.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.DeclaringType.Name=="AutoPlayRuntime" && m.Name=="Tick"),
 "Assist tick takes transition state only; F4/Shared Stash visibility is never passed as a pause flag");
Check(!assistCalls.Any(m=>m.DeclaringType.Name=="UIBase" || m.Name is "get_forceDisableInput" or "get_ButtonPressBlocked") &&
 !assistTick.Body.Instructions.Any(i=>i.Operand is FieldReference f && f.Name is "forceDisableInput" or "ButtonPressBlocked"),
 "Assist neither pauses nor releases held channel for menu/chat/panels or UI input-block flags");
Check(assistTick.Body.Instructions.Where(i=>i.Operand is MethodReference m && m.Name=="InputBlock")
 .All(i=>i.Previous.OpCode.Code==Mono.Cecil.Cil.Code.Ldc_I4_0),
 "Assist releases owned input blocks only; opening F1 never acquires a global input block");
var enemyDots=types.Single(t=>t.Name=="EnemyMapDots").Methods.Single(m=>m.Name=="Tick");
var enemyCalls=enemyDots.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().ToArray();
Check(enemyCalls.Any(m=>m.DeclaringType.Name=="MinimapIconManager" && m.Name=="Add") &&
 enemyCalls.Any(m=>m.DeclaringType.Name=="MinimapIconManager" && m.Name=="Exists") &&
 !enemyCalls.Any(m=>m.Name=="ChangeIcon"),"Enemy dots register sprite changes with native LE minimap manager and recover missing registrations");
var woven=installed.Single(t=>t.Name=="OfflineWovenPotentialPatch");
var wovenPrefix=woven.Methods.Single(m=>m.Name=="Prefix");
Check(wovenPrefix.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.DeclaringType.Name=="OfflineToolkit" && m.Name=="get_Enabled") &&
 wovenPrefix.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.DeclaringType.Name=="WovenPotentialRules" && m.Name=="AppliesTo") &&
 wovenPrefix.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.DeclaringType.Name=="WovenPotentialRules" && m.Name=="Roll") &&
 wovenPrefix.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="RebuildID") &&
 wovenPrefix.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="setValuesFromSerialisation"),
 "Woven reroll remains offline-only, guarded by Core LP range, and rebuilds native item serialization");
Check(!installed.SelectMany(t=>t.CustomAttributes.Where(a=>a.AttributeType.Name=="HarmonyPatch"))
 .SelectMany(a=>a.ConstructorArguments).Any(a=>a.Value is string target &&
 (target.Contains("CanRandomizeLegendaryPotential") || target=="CraftRandomizeLegendaryPotential")),
 "Woven native machine eligibility and craft caller are unpatched; 0 LP remains rejected, use consumption stays native");
Check(!installed.SelectMany(t=>t.CustomAttributes.Where(a=>a.AttributeType.Name=="HarmonyPatch"))
 .SelectMany(a=>a.ConstructorArguments).Any(a=>a.Value is TypeReference target &&
 target.Name is "RandomizeLegendaryPotentialItemContainer" or "RandomizeLegendaryPotentialCraftingUI" or "ZoneRestrictedCraftingManager"),
 "Woven input container, crafting UI and zone/use gate remain entirely native");
var imprintPatches=installed.Where(t=>t.Name.StartsWith("OfflineWeaver",StringComparison.Ordinal)).ToArray();
Check(imprintPatches.Length==11,"all eleven narrowly selected Weaver Imprint gameplay/legacy/V2 tooltip patch classes are installed");
var pointImprints=imprintPatches.Single(t=>t.Name=="OfflineWeaverImprintPointRatesPatch");
Check(pointImprints.Methods.Single(m=>m.Name=="Postfix").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>()
 .Where(m=>m.Name.StartsWith("set_",StringComparison.Ordinal)).Select(m=>m.Name).OrderBy(n=>n).SequenceEqual(new[]{
 "set_ImprintContainerChampionsChanceToDropMoreItemsOfType","set_OnCacheOpenedChanceForSimilarItems"}),
 "Imprint point refresh writes only Sealed/Warlord derived rate fields, not shared assets, points or unlock flags");
Check(pointImprints.Methods.Single(m=>m.Name=="Postfix").Parameters.Any(p=>p.Name=="__1" && p.ParameterType.FullName=="System.Int32") &&
 pointImprints.Methods.Single(m=>m.Name=="Postfix").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="SealedChance"),
 "Sealed uses actual allocated-point argument and the explicit6%-to12% rate, not legacy .029 doubled to .058");
var commonerImprints=imprintPatches.Single(t=>t.Name=="OfflineWeaverCommonerDropPatch");
Check(commonerImprints.Methods.Single(m=>m.Name=="Finalizer").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="set_totalSpawners") &&
 commonerImprints.Fields.Any(f=>f.CustomAttributes.Any(a=>a.AttributeType.Name=="ThreadStaticAttribute")),
 "Commoner inlined probability has exception-safe numerator restoration and thread-local reentrancy guard");
var wovenImprints=imprintPatches.Single(t=>t.Name=="OfflineWeaverWovenImprintScopePatch");
var wovenTargets=Walk(new[]{wovenImprints}).SelectMany(t=>t.Methods).Where(m=>m.HasBody)
 .SelectMany(m=>m.Body.Instructions).Where(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Ldstr).Select(i=>i.Operand as string).ToArray();
Check(new[]{"getIslandTypeForNewIsland","SetNaturalSpawnedWovenEchoIslandData","OverrideEchoTypes","CanSpawnSpecialEchoesNaturally"}.All(wovenTargets.Contains) &&
 !wovenTargets.Contains("IncreaseChanceForSpecificEchoTypes"),
 "Woven/Loomed scopes cover both outer island probability and final native weighted choice");
Check(!imprintPatches.SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>()
 .Any(m=>m.DeclaringType.Name=="RngElement" || m.Name.Contains("DropAfterDelay") || m.Name=="RebuildID"),
 "Imprint multiplier patches never roll/replay rewards or generate/mutate items directly");
var sharedView=types.Single(t=>t.Name=="NativeSharedStashView");
var sharedSort=sharedView.Methods.Single(m=>m.Name=="SortSharedTab");
Check(sharedSort.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.DeclaringType.Name=="ItemContainer" && m.Name=="TrySort") &&
 sharedSort.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.DeclaringType.Name=="LanSharedChest" && m.Name=="SortTab") &&
 !sharedSort.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name is "get_stash" or "SaveItems" or "SetDirty"),
 "Shared Sort uses native algorithm on isolated projection containers and submits only order to Host, never personal stash save");
Check(sharedView.Methods.Single(m=>m.Name=="OpenSharedConfiguration").Body.Instructions.Any(i=>i.Operand is GenericInstanceMethod m &&
 m.Name=="OpenModalAsync" && m.GenericArguments.Any(a=>a.Name=="StashConfigureTabModalV2")) &&
 sharedView.Methods.Single(m=>m.Name=="PollTabConfiguration").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="ConfigureTab" && m.DeclaringType.Name=="LanSharedChest"),
 "Configure Tab reuses native V2 modal and saves result through authoritative shared Host command");
Check(sharedView.Methods.Any(m=>m.Name=="CaptureNativeTabArtwork") && sharedView.Methods.Any(m=>m.Name=="BuildSharedTabControls") &&
 types.Where(t=>t.FullName.StartsWith(sharedView.FullName)).SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is MethodReference m && m.Name=="AddTabInCategory" && m.DeclaringType.Name=="LanSharedChest"),
 "Shared tab navigation reuses native tab/control artwork with a dedicated add-tab action");
var sharedClient=types.Single(t=>t.Name=="LanSharedChest");
Check(sharedClient.Methods.Single(m=>m.Name=="Receive").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.DeclaringType.Name=="ChestReply" && m.Name=="get_Tab") &&
 sharedClient.Methods.Single(m=>m.Name=="SelectTab").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="get_TabControlsReady"),
 "Shared tab switching is blocked during custody operations and selected view checks reply tab identity");
Check(!sharedClient.Methods.Any(m=>m.Name=="DeleteTab") &&
 !sharedView.Methods.Any(m=>m.Name is "RebindDeleteButton" or "MaintainSharedDeleteButton" or "CanDeleteSharedTab") &&
 !types.Any(t=>t.Name=="SharedStashModalLayout") &&
 !sharedView.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>Equals(i.Operand,"DELETE TAB")),
 "removed child-tab Delete UI, layout helper and client action are absent from the compiled mod");
Check(sharedClient.Methods.Single(m=>m.Name=="Tick").Body.Instructions.Count(i=>i.Operand is MethodReference m && m.Name=="set_TabGeneration")>=3 &&
 sharedClient.Methods.Single(m=>m.Name=="Receive").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="get_Deleted") &&
 sharedView.Methods.Single(m=>m.Name=="RestoreSharedModalActions").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="set_onClick"),
 "all deposit/withdraw/ack retries retain tab generation; deleted selection falls back and native modal state is restored");
Check(core.MainModule.Types.Single(t=>t.Name=="LanProtocol").Fields.Single(f=>f.Name=="Version").Constant is int sharedProtocol && sharedProtocol==44 &&
 core.MainModule.Types.Single(t=>t.Name=="SharedStashRules").Fields.Single(f=>f.Name=="ItemsPerTab").Constant is int perTab && perTab==96,
 "Protocol44 requires matching peers; per-tab snapshots retain the96-item transport limit");
Check(sharedView.Methods.Single(m=>m.Name=="BindArtwork").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="set_raycastTarget") &&
 sharedView.Methods.Single(m=>m.Name=="BuildSharedTabControls").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="SetPersistentListenerState"),
 "Native shared-tab buttons create stable root hit targets; copied scroll controls disable serialized private callbacks");
Check(sharedView.Methods.Single(m=>m.Name=="Tick").Body.Instructions.Any(i=>i.Operand is FieldReference f && f.Name=="projectedItems") &&
 sharedClient.Methods.Single(m=>m.Name=="Tick").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="PollTabConfiguration"),
 "Other-tab revisions do not rebuild unchanged item projections, and native Configure modal completion is polled even after F2 closes");
var legacyCapture=sharedView.Methods.Single(m=>m.Name=="CaptureLegacyTabArtwork");
Check(legacyCapture.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.Name=="get_addTabButton") &&
 legacyCapture.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.Name=="get_tabPrefab") &&
 legacyCapture.Body.Instructions.Select(i=>i.Operand).OfType<GenericInstanceMethod>().Any(m=>m.GenericArguments.Any(a=>a.Name=="StashSortConfigureSearchButtonsNavigable")),
 "Legacy Stash captures its real tab prefab, add button and Sort/Configure controls instead of falling back to fabricated UI");
Check(sharedView.Methods.All(m=>m.Name is not ("Label" or "Button" or "AddControls")) &&
 sharedView.Methods.Where(m=>m.HasBody && m.Name!="Build").SelectMany(m=>m.Body.Instructions)
 .All(i=>!(i.OpCode.Code==Mono.Cecil.Cil.Code.Newobj && i.Operand is MethodReference m && m.DeclaringType.FullName=="UnityEngine.GameObject")),
 "Shared view creates no replacement button, tab strip, page label or status overlay GameObjects");
Check(sharedView.Methods.Single(m=>m.Name=="BindArtwork").Body.Instructions.Any(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Newobj &&
 i.Operand is MethodReference m && m.DeclaringType.Name=="ButtonClickedEvent"),
 "Native button rebinding replaces serialized events so private-stash persistent callbacks cannot survive");
Check(sharedView.Methods.Single(m=>m.Name=="CaptureSearch").Body.Instructions.Count(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Newobj)>=5 &&
 sharedView.Methods.Single(m=>m.Name=="ApplyNativeSearch").Body.Instructions.Any(i=>i.Operand is MethodReference m &&
 m.DeclaringType.Name=="ItemContainerUIWithSearch" && m.Name=="PerformSearch"),
 "Original search field uses fresh events and native search on the isolated shared projection");
Check(sharedView.Methods.Single(m=>m.Name=="RestoreNativeTabLayout").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>()
 .Any(m=>m.DeclaringType.Name=="SharedStashNativeLayout" && m.Name=="CopySettings") &&
 types.Single(t=>t.Name=="SharedStashNativeLayout").Methods.Single(m=>m.Name=="CopySettings").Body.Instructions.Select(i=>i.Operand).OfType<GenericInstanceMethod>()
 .Any(m=>m.Name=="AddComponent" && m.GenericArguments.Any(a=>a.Name=="SimpleLayoutGroup")) &&
 sharedView.Methods.Single(m=>m.Name=="RefreshNativeTabLayout").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.Name=="UpdateLayout"),
 "Legacy tabs reuse the native SimpleLayoutGroup with fresh element records and original layout settings");
Check(!sharedView.Methods.Single(m=>m.Name=="Tick").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>()
 .Any(m=>m.Name=="RefreshNativeTabLayout"),
 "Native tab layout rebuild is driven by changed tab metadata, not an unconditional per-frame rebuild");
var configureModal=sharedView.Methods.Single(m=>m.Name=="OpenSharedConfiguration");
Check(configureModal.Body.Instructions.Select(i=>i.Operand).OfType<GenericInstanceMethod>().Where(m=>m.Name=="OpenModalAsync")
 .SelectMany(m=>m.GenericArguments).Any(t=>t.Name=="StashConfigureTabModal") &&
 configureModal.Body.Instructions.Select(i=>i.Operand).OfType<GenericInstanceMethod>().Where(m=>m.Name=="OpenModalAsync")
 .SelectMany(m=>m.GenericArguments).Any(t=>t.Name=="StashConfigureTabModalV2") &&
 configureModal.Body.Instructions.Any(i=>i.Operand is FieldReference f && f.Name=="legacyConfiguration"),
 "Configure uses the original legacy or V2 modal matching the captured native Stash controls");
Check(sharedView.Methods.Single(m=>m.Name=="BindTabConfigurationClick").Body.Instructions.Select(i=>i.Operand).OfType<GenericInstanceMethod>()
 .Any(m=>m.Name=="AddComponent" && m.GenericArguments.Any(t=>t.Name=="EventTrigger")) &&
 sharedView.Methods.Single(m=>m.Name=="RefreshSharedTabs").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="BindTabConfigurationClick") &&
 sharedView.Methods.Single(m=>m.Name=="RefreshSharedTabs").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="ConfigureSharedTab"),
 "Every native shared tab gets a context-click handler and configuration waits for the selected Host tab snapshot");
Check(sharedView.Methods.Single(m=>m.Name=="RefreshSharedTabs").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="ApplyLegacyTabColor") &&
 sharedView.Methods.Single(m=>m.Name=="ApplyLegacyTabColor").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="set_color"),
 "Legacy tab tint is applied to its native background, separately from the icon");
Check(sharedView.Methods.Single(m=>m.Name=="ConfigureSharedTab").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>()
 .Any(m=>m.DeclaringType.Name=="SharedStashPriorityCodec" && m.Name=="Decode") &&
 sharedView.Methods.Single(m=>m.Name=="PollTabConfiguration").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>()
 .Any(m=>m.DeclaringType.Name=="SharedStashPriorityCodec" && m.Name=="Encode"),
 "Native modal priority values are initialized from Host metadata and serialized on confirm, without hiding priority widgets");
Check(sharedView.Methods.Single(m=>m.Name=="ConfigureSharedCategory").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.Name=="OpenSharedConfiguration") &&
 sharedView.Methods.Single(m=>m.Name=="PollTabConfiguration").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.Name=="ConfigureCategory"),
 "Category editor reuses native modal and submits settings to the authoritative Host");
Check(sharedView.Methods.Single(m=>m.Name=="BuildSharedCategories").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.Name=="AddCategory") &&
 sharedView.Methods.Single(m=>m.Name=="CaptureLegacyCategories").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.Name=="get_categoryPrefab"),
 "Shared categories use native category artwork and add control instead of custom UI");
Check(sharedView.Methods.Single(m=>m.Name=="BindSharedModalActions").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.Name=="RebindModalButton") &&
 sharedView.Methods.Single(m=>m.Name=="RestoreSharedModalActions").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.Name=="set_onClick"),
 "Native duplicate/delete callbacks are scoped to shared modal and original callback objects are restored afterwards");
Check(!types.Any(t=>t.Name is "SharedStashLegacyConfigurationPatch" or "SharedStashConfigurationPatch"),
 "removed child-tab Delete lifecycle hooks are absent");
Check(installed.Any(t=>t.Name=="SharedStashPreparationVisibilityPatch") &&
 sharedView.Methods.Single(m=>m.Name=="MaskNativePreparation").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.Name=="set_alpha") &&
 sharedView.Methods.Single(m=>m.Name=="RestoreStagingVisibility").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.Name=="set_alpha"),
 "Scoped native panel-open hook masks the source before rendering and restores its visual state");
Check(sharedClient.Methods.Single(m=>m.Name=="ReleaseView").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.Name=="Hide") &&
 !sharedClient.Methods.Single(m=>m.Name=="ReleaseView").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.Name=="Dispose" && m.DeclaringType.Name=="NativeSharedStashView"),
 "Closing F2 hides cached projection instead of reopening/cloning the personal stash on every toggle");
var routing=types.Single(t=>t.Name=="SharedStashPriorityRouting");
Check(types.Where(t=>t.FullName.StartsWith(routing.FullName)).SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions)
 .Any(i=>i.Operand is MethodReference m && m.Name=="Match" && m.DeclaringType.Name=="StashPriority") &&
 sharedClient.Methods.Single(m=>m.Name=="Deposit").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="Choose" && m.DeclaringType.Name=="SharedStashPriorityRouting"),
 "Shared deposits use native priority matching and route before starting the durable custody transaction");
var buildGate=types.Single(t=>t.Name=="LanMod").Methods.Single(m=>m.Name=="IsSupportedGameAssembly");
var geometry=types.Single(t=>t.Name=="SharedStashGeometry");
Check(sharedView.Methods.Single(m=>m.Name=="ScrollSharedSelection").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.DeclaringType.Name=="SharedStashGeometry") &&
 geometry.Methods.Single(m=>m.Name=="RelativeRect").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.Name=="TransformPoint") &&
 geometry.Methods.Single(m=>m.Name=="RelativeRect").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.Name=="InverseTransformPoint"),
 "shared tab scrolling measures four corners through native transform bindings instead of stripped Unity bounds helper");
var strippedCalls=new List<string>();
foreach(var source in types.Where(t=>t.Name is "NativeSharedStashView" or "SharedStashGeometry" or "SharedStashNativeLayout" or "SharedStashModalLayout").SelectMany(t=>t.Methods).Where(m=>m.HasBody))
foreach(var call in source.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Where(m=>m.DeclaringType.Namespace.StartsWith("UnityEngine",StringComparison.Ordinal) || m.DeclaringType.Namespace=="Il2CppTMPro"))
{
 var target=call.Resolve();
 if(target?.HasBody==true && target.Body.Instructions.Any(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Ldstr && i.Operand is string text && text.Contains("unstripping",StringComparison.OrdinalIgnoreCase)))
  strippedCalls.Add(source.Name+" -> "+call.FullName);
}
Check(strippedCalls.Count==0,"shared native view and geometry have no direct Unity/TMP unstripping-failure stubs: "+string.Join(",",strippedCalls));
var nativeLayout=types.Single(t=>t.Name=="SharedStashNativeLayout");
Check(nativeLayout.Methods.Single(m=>m.Name=="Restore").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.Name=="set__excludeIfInactive") &&
 nativeLayout.Methods.Single(m=>m.Name=="CopySettings").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.Name=="set__updateMode"),
 "native layout restore preserves update mode, inactive-element policy and freshly mapped clone element records");
Check(sharedView.Methods.Single(m=>m.Name=="BindArtwork").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.Name=="PreserveHitSurface") &&
 nativeLayout.Methods.Single(m=>m.Name=="PreserveHitSurface").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.Name=="set_transition"),
 "synthetic input hit surfaces remain transparent instead of becoming flat default Button backgrounds");
Check(!sharedView.Fields.Any(f=>f.Name is "nativeTabNamePadding" or "nativeCategoryNamePadding") &&
 sharedView.Methods.Single(m=>m.Name=="RefreshNativeTabLayout").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.Name=="Reflow"),
 "tab/category sizing uses restored native layout rather than a fixed copied width or text-padding estimate");
Check(new[]{"RefreshSharedTabs","RefreshSharedCategories"}.All(name=>sharedView.Methods.Single(m=>m.Name==name).Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="SizeLabel")) &&
 nativeLayout.Methods.Single(m=>m.Name=="SizeLabel").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="GetPreferredValues") &&
 nativeLayout.Methods.Single(m=>m.Name=="SizeLabel").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="SetSizeWithCurrentAnchors"),
 "both shared tab/category labels resize their rect from TMP preferred text metrics before native parent layout");
Check(Walk(new[]{sharedView}).SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Ldstr && Equals(i.Operand,"CONFIGURE CATEGORY")) &&
 sharedView.Methods.Single(m=>m.Name=="CaptureNativeNavigation").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="SetActive") &&
 sharedView.Methods.Single(m=>m.Name=="RefreshSharedCategories").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="BindContextAction"),
 "extra Configure Category footer control is explicitly suppressed while category context actions remain available");
Check(new[]{"OfflineWeaverImprintTooltipStatPatch","OfflineWeaverImprintTooltipInfoPatch"}.All(name=>installed.Any(t=>t.Name==name)) &&
 types.Single(t=>t.Name=="OfflineWeaverImprintTooltipStatPatch").Methods.Single(m=>m.Name=="Postfix").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.Name=="DoubleTooltipPercent") &&
 !types.Single(t=>t.Name=="OfflineWeaverImprintTooltipInfoPatch").Methods.SelectMany(m=>m.HasBody?m.Body.Instructions:Enumerable.Empty<Mono.Cecil.Cil.Instruction>()).Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.Name=="set_pointsAllocated"),
 "offline Imprint tooltip updates fresh native display values without granting or editing node points");
Check(new[]{"OfflineWeaverImprintLegacyValuePatch","OfflineWeaverImprintLegacyInfoPatch"}.All(name=>installed.Any(t=>t.Name==name)) &&
 types.Single(t=>t.Name=="OfflineWeaverImprintLegacyValuePatch").Methods.Single(m=>m.Name=="Prefix").Parameters.Any(p=>p.Name=="valueText" && p.ParameterType.IsByReference) &&
 types.Single(t=>t.Name=="OfflineWeaverImprintLegacyValuePatch").Methods.Single(m=>m.Name=="Prefix").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.Name=="DoubleTooltipPercent"),
 "legacy NodeTooltipManager path scales only the fresh valueText before native coloring");
Check(types.Single(t=>t.Name=="OfflineWeaverImprintLegacyInfoPatch").Methods.Single(m=>m.Name=="Postfix").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.Name=="set_requireLayoutUpdate") &&
 !types.Single(t=>t.Name=="OfflineWeaverImprintLegacyValuePatch").Methods.Single(m=>m.Name=="Prefix").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.Name=="set_value" || m.Name=="set_pointsAllocated"),
 "legacy tooltip note requests native layout refresh without editing serialized node stats or allocated points");
var ladder=types.Single(t=>t.Name=="NativeRoomLadder");
var ladderHooks=new[]{"RoomLadderOpenPatch","RoomLadderClosePatch","RoomLadderSessionPatch","RoomLadderSelfRowPatch","RoomLadderFilterPatch"};
var meteorHooks=new[]{"OfflineMeteorTreePatch","OfflineMeteorCastPatch","OfflineMeteorImpactScopePatch","OfflineMeteorManaCostPatch","OfflineMeteorTooltipValuePatch","OfflineMeteorLegacyValuePatch","OfflineMeteorTooltipInfoPatch","OfflineMeteorLegacyInfoPatch"};
Check(meteorHooks.All(name=>installed.Any(t=>t.Name==name)),"all eight Meteor gameplay/legacy/V2 tooltip hooks are selected");
var meteorApply=types.Single(t=>t.Name=="MeteorNodeRuntime").Methods.Single(m=>m.Name=="Apply");
Check(meteorApply.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>()
 .Where(m=>m.Name.StartsWith("set_",StringComparison.Ordinal)).Select(m=>m.Name).OrderBy(n=>n).SequenceEqual(new[]{"set_chanceFor100increasedArea","set_frostClawCastsOnImpact"}),
 "Meteor allocation application changes only large-Meteor chance and Heat Flux count, not crit/damage stats or allocated points");
var castCalls=types.Single(t=>t.Name=="OfflineMeteorCastPatch").Methods.Single(m=>m.Name=="Prefix").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().ToArray();
Check(castCalls.Any(m=>m.Name=="Local") && castCalls.Any(m=>m.Name=="getSkillTreeData") && castCalls.Any(m=>m.Name=="Apply"),
 "Meteor cast refresh reads local allocated tree before native object creation even if loading skipped the tree hook");
Check(tranquilityHooks.All(n=>installed.Any(t=>t.Name==n)),"all Tranquility gameplay and legacy/V2 tooltip hooks are selected for installation");
Check(new[]{"OfflineArcaneTranquilityLegacyTooltipPatch","OfflineArcaneTranquilityTooltipPatch"}.All(n=>
 types.Single(t=>t.Name==n).Methods.Any(m=>m.Name=="Finalizer")),"Tranquility tooltip list changes have native exception cleanup");
var fkHooks=new[]{"OfflineForgottenKnightsRankPatch","OfflineForgottenKnightsDescriptionPatch"};
Check(fkHooks.All(n=>installed.Any(t=>t.Name==n)),"Forgotten Knights rank and description hooks are selected for installation");
var fkCalls=types.Single(t=>t.Name=="OfflineForgottenKnights").Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>().ToArray();
Check(new[]{"set_AdditionalChanceForSwappableNemesisUnique","set_DoubleStabilityGainChance","set_AdditionalChanceForHarbingersToDropTemporalKeystone"}.All(n=>fkCalls.Any(m=>m.Name==n)) &&
 !fkCalls.Any(m=>m.Name is "AddStability" or "Roll" or "SetRank" or "Join"),
 "Forgotten Knights changes three native rank fields without replaying rewards, replacing RNG or unlocking ranks");
Check(fkCalls.Any(m=>m.Name=="get_IsMember") && fkCalls.Any(m=>m.Name=="getPlayerActor") && fkCalls.Any(m=>m.Name=="RankEnabled"),
 "Forgotten Knights changes respect local membership, earned ranks and native toggle state");
var stabilityHooks=new[]{"OfflineEchoStabilityPatch","OfflineEchoBonusStabilityScopePatch","OfflineTemporalKeystoneScopePatch","OfflineScopedStabilityAwardPatch","OfflineTemporalKeystoneRangePatch","OfflineEchoStabilityTooltipPatch","OfflineEchoStabilityBarPatch","OfflineEchoBonusStabilityDisplayPatch"};
Check(stabilityHooks.All(n=>installed.Any(t=>t.Name==n)),"Echo reward, bonus, Keystone and matching native UI hooks are installed");
Check(types.Single(t=>t.Name=="StabilityAwardScope").Fields.Any(f=>f.CustomAttributes.Any(a=>a.AttributeType.Name=="ThreadStaticAttribute")) &&
 new[]{"OfflineEchoBonusStabilityScopePatch","OfflineTemporalKeystoneScopePatch"}.All(n=>types.Single(t=>t.Name==n).Methods.Any(m=>m.Name=="Finalizer")),
 "bonus/Keystone award scaling has thread-local scopes and native exception cleanup");
var stabilityRefs=stabilityHooks.Select(n=>types.Single(t=>t.Name==n)).SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>().ToArray();
Check(!stabilityRefs.Any(m=>m.Name is "AddStability" or "NextInt" or "Roll" or "TryGetKeyEntry"),
 "Stability corrections do not replay awards, replace native RNG or consume extra Keystones");
var arcane=types.Single(t=>t.Name=="OfflineArcaneMovementPatch");
var arcaneCalls=arcane.Methods.Single(m=>m.Name=="Prefix").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().ToArray();
Check(installed.Contains(arcane) && arcaneCalls.Any(m=>m.Name=="Local") && arcaneCalls.Any(m=>m.Name=="Instantiate") && arcaneCalls.Any(m=>m.Name=="set_roots") &&
 !arcaneCalls.Any(m=>m.Name is "set_numberOfRoots" or "DecrementNumberOfRoots"),
 "local Arcane uses a private root-free ailment definition without clearing unrelated root counters");
Check(!types.Any(t=>t.Name.StartsWith("UiShortcut") || t.Name is "ShortcutAction" or "ShortcutBinding"),
 "removed Shortcut runtime, rules, actions and bindings are absent from the release assembly");
Check(!types.Single(t=>t.Name=="LanMod").Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions)
 .Any(i=>i.Operand is string s && s=="UI Shortcut"),
 "F4 no longer advertises the removed UI Shortcut feature");
var archmageHooks=new[]{"OfflineArchmageCastPatch","OfflineArchmageRefundPatch","OfflineArchmageDescriptionPatch","OfflineArchmageStatNamePatch","OfflineArchmageTooltipValuePatch","OfflineArchmageLegacyValuePatch"};
Check(archmageHooks.All(n=>installed.Any(t=>t.Name==n)),"all Archmage native, description, stat-label and legacy/V2 value hooks are installed");
Check(types.Single(t=>t.Name=="OfflineArchmageCastPatch").Methods.Any(m=>m.Name=="Finalizer") &&
 types.Single(t=>t.Name=="ArchmageCastScope").Fields.Any(f=>f.CustomAttributes.Any(a=>a.AttributeType.Name=="ThreadStaticAttribute")),
 "Archmage correction uses a thread-local scope with native exception cleanup");
var archmageRefund=types.Single(t=>t.Name=="OfflineArchmageRefundPatch").Methods.Single(m=>m.Name=="Prefix");
Check(archmageRefund.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.Name=="get_CurrentManaRefundCastID") &&
 !archmageHooks.Select(n=>types.Single(t=>t.Name==n)).SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions)
 .Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.Name is "gainMana" or "reduceMana" or "RefundManaFromMostRecentCast"),
 "Archmage adjusts the native refund argument for its original cast ID without replaying mana effects");
Check(types.Single(t=>t.Name=="OfflineMeteorImpactScopePatch").Methods.Any(m=>m.Name=="Finalizer") &&
 types.Single(t=>t.Name=="MeteorManaScope").Fields.Any(f=>f.CustomAttributes.Any(a=>a.AttributeType.Name=="ThreadStaticAttribute")) &&
 !meteorHooks.Select(name=>types.Single(t=>t.Name==name)).SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is MethodReference m && m.Name is "reduceMana" or "UseAbility" or "CastAfterDelay"),
 "Meteor mana correction is a thread-local finalizer scope and never refunds/replays a cast");
Check(ladderHooks.All(name=>installed.Any(t=>t.Name==name)),"all five offline native ladder hooks are selected by the release allowlist");
Check(!patches.Any(t=>t.Name=="RoomLadderSeasonPatch") &&
 !patches.SelectMany(t=>t.CustomAttributes).Where(a=>a.AttributeType.FullName=="HarmonyLib.HarmonyPatch").SelectMany(a=>a.ConstructorArguments).Any(a=>Equals(a.Value,"HandleSeasonEventUI")),
 "shared no-op native HandleSeasonEventUI body is never detoured; season visuals are changed only while opening the concrete Ladder view");
using(var nativeMetadata=AssemblyDefinition.ReadAssembly(Path.Combine(args[3],"MelonLoader","Dependencies","Il2CppAssemblyGenerator","Cpp2IL","cpp2il_out","LE.dll")))
{
 var nativeTypes=Walk(nativeMetadata.MainModule.Types).ToDictionary(t=>t.FullName);
 var image=File.ReadAllBytes(Path.Combine(args[3],"GameAssembly.dll"));
 byte[] NativeBody(string type,string name)
 {
  var method=nativeTypes[type].Methods.Single(m=>m.Name==name);
  var address=method.CustomAttributes.Single(a=>a.AttributeType.Name=="AddressAttribute");
  int Read(string field)=>Convert.ToInt32(address.Fields.Single(f=>f.Name==field).Argument.Value.ToString()!.Replace("0x",""),16);
  return image.AsSpan(Read("Offset"),Read("Length")).ToArray();
 }
 ulong NativeRva(string type,string name)
 {
  var address=nativeTypes[type].Methods.Single(m=>m.Name==name).CustomAttributes.Single(a=>a.AttributeType.Name=="AddressAttribute");
  return Convert.ToUInt64(address.Fields.Single(f=>f.Name=="RVA").Argument.Value.ToString()!.Replace("0x",""),16);
 }
 bool CallsNative(string owner,string method,string targetOwner,string targetMethod)
 {
  var body=NativeBody(owner,method);long start=(long)NativeRva(owner,method),target=(long)NativeRva(targetOwner,targetMethod);
  for(int i=0;i+5<=body.Length;i++)if(body[i]==0xE8 && start+i+5+BitConverter.ToInt32(body,i+1)==target)return true;
  return false;
 }
 Check(nativeTypes["EchoWeb"].Methods.Single(m=>m.Name=="getIslandTypeForNewIsland").Parameters.All(p=>p.ParameterType is not ByReferenceType) &&
  CallsNative("EchoWeb","getIslandTypeForNewIsland","LE.Factions.WeaverTreeNodesExtensions","IncreaseChanceForSpecificEchoTypes") &&
  CallsNative("LE.Factions.WeaverTreeNodesExtensions","IncreaseChanceForSpecificEchoTypes","WovenEchoData","GetTotalNaturalSpawnWeighting"),
  "replacement Woven scope has no by-ref parameters and encloses the actual native Imprint weighting helper");
 Check(NativeBody("ArcaneAscendanceTree","updateMutator").AsSpan().IndexOf(new byte[]{0xB2,0x12,0x48,0x8B,0xC8})>=0 &&
  NativeBody("ArcaneAscendanceTree","updateMutator").AsSpan().IndexOf(new byte[]{0x4C,0x89,0xB3,0x28,0x01,0,0})>=0 &&
  NativeBody("ArcaneAscendanceMutator","Mutate").AsSpan().IndexOf(new byte[]{0x48,0x8B,0x97,0x28,0x01,0,0})>=0,
  "native Tranquility creates ManaRegen stat18 in statsWhileActive and actual activation consumes that same list");
 Check(CallsNative("MonolithRestZoneManager","Initialize","MonolithRun","AddStability") &&
  CallsNative("MonolithRun","TryUseTemporalKeystoneItemAndIncreaseStability","MonolithRun","AddStability") &&
  !CallsNative("MonolithRun","TryUseTemporalKeystoneItemAndIncreaseStability","MonolithRun","GetStabilityRangeFromTemporalKeystone"),
  "actual bonus and Keystone awards reach the scoped consumer; Keystone UI range is not reused by its native roll");
 Check(CallsNative("MonolithTimelinePanelManager","SelectEcho","StabilityBar","SetCurrentStabilityGainMax") &&
  CallsNative("TemporalKeystoneUI","OnTemporalKeystonePointerEntered","StabilityBar","SetCurrentStabilityMinMax"),
  "Echo and Keystone previews use distinct native bar paths to avoid double-scaling Keystone ranges");
 Check(NativeBody("MonolithZoneManager","ApplyHostWeaverModifiers").AsSpan().IndexOf(new byte[]{0x8B,0x81,0x20,0x01,0,0,0x89,0x83,0x04,0x03,0,0})>=0,
  "native Echo setup copies faction cache chance at0x120 into the zone roll field at0x304");
 Check(NativeBody("OneShotCacheGameplayObject/<OpenAfterDelay>d__26","MoveNext").AsSpan().IndexOf(new byte[]{0xF3,0x0F,0x10,0x88,0x04,0x03,0,0})>=0,
  "actual cache coroutine reads zone chance at0x304 for its native RNG roll, rather than only reading tooltip stats");
 Check(NativeBody("MeteorMutator","Mutate").AsSpan().IndexOf(new byte[]{0x8B,0x8F,0x50,0x01,0,0,0x89,0x88,0x38,0x01,0,0})>=0,
  "native Meteor propagates Heat Flux count from its0x150 field into impact0x138");
 Check(NativeBody("MeteorMutator","Mutate").AsSpan().IndexOf(new byte[]{0xF3,0x0F,0x10,0x8F,0x78,0x01,0,0})>=0,
  "actual large-Meteor native RNG reads chanceFor100increasedArea at0x178");
 Check(NativeBody("MeteorAoEMutator","Mutate").AsSpan().IndexOf(new byte[]{0xBA,0xC4,0x02,0,0})>=0 &&
  NativeBody("MeteorAoEMutator","Mutate").AsSpan().IndexOf(new byte[]{0x0F,0x28,0xF8,0xF3,0x0F,0x59,0x3D})>=0,
  "Heat Flux native consumer queries Frost Claw ID708 and applies its inlined mana factor to that result");
 foreach(var hook in installed.Where(t=>newMeteorHooks.Contains(t.Name) || classMenuHooks.Contains(t.Name) || ladderHooks.Contains(t.Name) || meteorHooks.Contains(t.Name) || t.Name is "SharedStashLegacyConfigurationPatch" or "SharedStashConfigurationPatch" or "AggressiveMinionAcquirePatch" or "AggressiveMinionTargetPatch" or "OfflineWeaverImprintTooltipStatPatch" or "OfflineWeaverImprintTooltipInfoPatch" or "OfflineWeaverImprintLegacyValuePatch" or "OfflineWeaverImprintLegacyInfoPatch"))
 {
  var attribute=hook.CustomAttributes.Single(a=>a.AttributeType.FullName=="HarmonyLib.HarmonyPatch");
  var type=(TypeReference)attribute.ConstructorArguments[0].Value;
  string name=type.FullName.StartsWith("Il2Cpp.",StringComparison.Ordinal)?type.FullName[7..]:type.FullName[6..];
  string methodName=(string)attribute.ConstructorArguments[1].Value;
  string NativeType(TypeReference t)=>t.FullName.StartsWith("Il2Cpp.",StringComparison.Ordinal)?t.FullName[7..]:
   t.FullName.StartsWith("Il2CppLE.",StringComparison.Ordinal)?t.FullName[6..]:t.FullName;
  var candidates=nativeTypes[name].Methods.Where(m=>m.Name==methodName);
  if(attribute.ConstructorArguments.Count>=3 && attribute.ConstructorArguments[2].Value is CustomAttributeArgument[] overload)
   candidates=candidates.Where(m=>m.Parameters.Select(p=>p.ParameterType.FullName).SequenceEqual(overload.Select(a=>NativeType((TypeReference)a.Value))));
  var method=candidates.Single();
  var address=method.CustomAttributes.Single(a=>a.AttributeType.Name=="AddressAttribute");
  int offset=Convert.ToInt32(address.Fields.Single(f=>f.Name=="Offset").Argument.Value.ToString()!.Replace("0x",""),16);
  Check(image[offset] is not (0xC3 or 0xC2 or 0xCC),"native hook targets a non-noop implementation: "+name+"::"+methodName);
 }
}
Check(presenceCalls.Any(m=>m.DeclaringType.Name=="NativeRoomLadder" && m.Name=="Capture") &&
 reachable.Any(m=>m.DeclaringType.Name=="NativeRoomLadder" && m.Name=="Tick"),
 "inventory-only room publishes bounded character ladder data and refreshes native table");
var ladderCalls=Walk(new[]{ladder}).SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>().ToArray();
Check(!ladderCalls.Any(m=>m.DeclaringType.Name is "LadderOnlineClientService" or "ServiceProvider" or "HttpClient") &&
 ladderCalls.Any(m=>m.Name=="ShowSelectedLadder") && ladderCalls.Any(m=>m.Name=="SetRecords"),
 "room ladder supplies native sessions/table without online services or a custom overlay");
Check(ladder.Methods.Single(m=>m.Name=="Filter").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.Name=="get_Cycle") &&
 !ladder.Methods.Single(m=>m.Name=="Filter").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.DeclaringType.Name=="ServiceProvider"),
 "offline class/mode filters use loaded character cycle rather than online cycle service");
Check(ladderCalls.Any(m=>m.Name=="remove_LadderSelectionChanged") && ladderCalls.Any(m=>m.Name=="remove_LocaleChanged") &&
 ladderCalls.Any(m=>m.Name=="RemoveListener"),"offline ladder detaches its owned filter/locale/scroll subscriptions");
var minion=types.Single(t=>t.Name=="AggressiveMinionRange");
var minionCalls=minion.Methods.Single(m=>m.Name=="Applies").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().ToArray();
Check(minionCalls.Any(m=>m.DeclaringType.Name=="OfflineToolkit" && m.Name=="get_Enabled") &&
 minionCalls.Any(m=>m.Name=="getPlayerActor") && minionCalls.Any(m=>m.Name=="GetPlayerMinionStance") && minionCalls.Any(m=>m.Name=="get_creator"),
 "aggressive minion gate checks offline environment, stance and bounded local ownership chain");
Check(new[]{"AggressiveMinionAcquirePatch","AggressiveMinionTargetPatch"}.All(name=>installed.Any(t=>t.Name==name) &&
 types.Single(t=>t.Name==name).Methods.Any(m=>m.Name=="Finalizer")),
 "both x2 minion acquisition hooks install with finalizers restoring temporary native fields");
var acquisition=types.Single(t=>t.Name=="AggressiveMinionAcquirePatch");
Check(acquisition.Methods.SelectMany(m=>m.HasBody?m.Body.Instructions:Enumerable.Empty<Mono.Cecil.Cil.Instruction>()).Select(i=>i.Operand).OfType<MethodReference>()
 .All(m=>m.Name is not "set_maximumAggroRange" and not "set_maxHorizontalOffsetForAggro" and not "UseAbility"),
 "minion acquisition never mutates global aggro constants or executes an extra attack");
Check(buildGate.Body.Instructions.Where(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Ldstr).Select(i=>(string)i.Operand).OrderBy(s=>s)
 .SequenceEqual(new[]{"D5F9FB458F0E9878E697A92B590A586A52FD77E645F8BF87BB4F2B74C6C56567","323E9183F3BEE600B6C9E1C469218B9DE926E9044F2CA3D44FAE8A0EC9A91464"}.OrderBy(s=>s)) &&
 buildGate.Body.Instructions.Count(i=>i.Operand is MethodReference m && m.DeclaringType.FullName=="System.String" && m.Name=="op_Equality")==2,
 "Game compatibility accepts only the old pinned SHA and inspected Steam26087601 SHA, without a wildcard bypass");
File.WriteAllText(args[2],JsonSerializer.Serialize(new{passed=checks.Count,patchesInspected=patches.Length,
 installedPatches=installed.Select(t=>t.FullName),skippedPatches=skipped.Select(t=>t.FullName),
 scope="compiled metadata and IL audit; not gameplay acceptance",checks},new JsonSerializerOptions{WriteIndented=true}));
