using System.Reflection;
using HarmonyLib;
using System.Text.Json;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using Il2CppLE.UI.Tooltips;
using NStats=Il2CppSystem.Collections.Generic.List<Il2Cpp.Stats.Stat>;
[assembly:MelonInfo(typeof(ItemNodeNativeQA.Audit),"Private Item Node QA","0.1.0","Local QA")]
[assembly:MelonGame("Eleventh Hour Games","Last Epoch")]
namespace ItemNodeNativeQA;
public partial class Audit:MelonMod
{
 static readonly List<string> localizationKeys=new();
 public static void ForceOfflineService(){Il2CppLE.Core.CommandLine.Options.OfflineMode=true;}
 public static void UsePrivateOfflineUser(){Il2CppLE.Services.ServiceProvider._UserService=new Il2CppLE.Services.OfflineUserService().Cast<Il2CppLE.Services.IUserService>();}
 public static void ObserveKey(string key){if(key?.Contains("445")==true && !localizationKeys.Contains(key))localizationKeys.Add(key);}
 public override void OnInitializeMelon(){var main=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="LastEpochLAN");releaseBootstrap=main.GetType("LastEpochLAN.Mod.NativeAssetProbe")==null;if(releaseBootstrap)HarmonyInstance.Patch(main.GetType("LastEpochLAN.Mod.LanMod").GetMethod("TickAutomaticRoom",BindingFlags.NonPublic|BindingFlags.Instance),prefix:new HarmonyMethod(typeof(Audit),nameof(BlockPrivateRoom)));HarmonyInstance.Patch(typeof(Il2CppLE.Services.ServiceProvider).GetMethod("InitializeUserService"),prefix:new HarmonyMethod(typeof(Audit),nameof(ForceOfflineService)),postfix:new HarmonyMethod(typeof(Audit),nameof(UsePrivateOfflineUser)));EchoLoadProfile.InstallBoot(HarmonyInstance);HarmonyInstance.Patch(typeof(Localization).GetMethod("GetText",new[]{typeof(string),typeof(string)}),prefix:new HarmonyMethod(typeof(Audit).GetMethod(nameof(ObserveKey))));}
 bool done,migrating;float mapStarted;int mapStage;Il2CppDMM.DMMapIcon removedMapIcon;string migrationId;int migrationClass;float migrationStarted,started;readonly List<object> checks=new();
 static Type T(string name)=>AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="LastEpochLAN").GetType("LastEpochLAN.Mod.OfflineTools."+name,true);
 static object Call(string name,string method,params object[] args)=>T(name).GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public).Invoke(null,args);
 void Check(bool ok,string label,object values=null){checks.Add(new{passed=ok,label,values});if(!ok)throw new Exception(label+": "+JsonSerializer.Serialize(values));}
 static bool Near(float a,float b)=>Math.Abs(a-b)<.01f;
 public override void OnUpdate()
 {
  if(done)return;EchoLoadProfile.BootSample();if(started==0)started=Time.unscaledTime;
  try
  {
   ReleaseBootstrapTick();
   if(Environment.GetCommandLineArgs().Contains("--no-debug-startup-qa"))
   {
    if(Time.unscaledTime-started<10)return;
    var assembly=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="LastEpochLAN");
    var main=assembly.GetType("LastEpochLAN.Mod.LanMod",true);
    Check(!(bool)main.GetField("DiagnosticsRequested",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null),"Normal startup leaves runtime report capture disabled");
    Check(assembly.GetType("LastEpochLAN.Mod.RuntimePerformance")==null && assembly.GetType("LastEpochLAN.Mod.OfflineTools.DeathHitchTrace")==null,"Normal binary contains no frame/death/allocation profiler");
    Finish(null);return;
   }
   if(echo500Stage>0){EchoLoad500(PlayerFinder.getPlayerActor());return;}
   if(Environment.GetCommandLineArgs().Contains("--lelan-echo-aggro-qa")){EchoAggro();Finish(null);return;}
   if(Environment.GetCommandLineArgs().Contains("--lelan-forging-half-qa")){ForgingPotential();Finish(null);return;}
   if(migrating)
   {
    if(Time.unscaledTime-migrationStarted>90)throw new TimeoutException("Native class migration timed out: "+T("ClassRespecialization").GetField("Status",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null));
    if((bool)Call("ClassRespecialization","get_Busy"))return;
    string status=(string)T("ClassRespecialization").GetField("Status",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
    Check(status.StartsWith("Class changed to "),"Native class exit, backup and datastore flush complete",new{status});
    Il2CppLE.Data.CharacterData fresh=null;foreach(var d in LocalCharacterSlots.instance.characterSlots)if(d.Id==migrationId){fresh=d;break;}
    Check(fresh.CharacterClass==migrationClass && fresh.ChosenMastery==0 && fresh.OriginalMastery==0 && fresh.SavedSkillTrees.Count==0 && fresh.AbilityBar.Count==5,"Native offline character list has new class, reset mastery and skill bars");
    Finish(null);return;
   }
   var actor=PlayerFinder.getPlayerActor();
   if(actor?.HasMovedToPlayerSpawn!=true){if(Time.unscaledTime-started>420)Finish(new TimeoutException("Private actor bootstrap timed out"));return;}
   if(Environment.GetCommandLineArgs().Contains("--lelan-echo-load-500-qa")){EchoLoad500(actor);return;}
   if(Environment.GetCommandLineArgs().Contains("--lelan-prophecy-stream-qa")){ProphecyStream(actor);return;}
   if(Environment.GetCommandLineArgs().Contains("--lelan-map-probe"))
   {
    if(mapStarted==0){mapStarted=Time.unscaledTime;Resources.UnloadUnusedAssets();return;}
    if(Time.unscaledTime-mapStarted<(mapStage==0?10:1))return;
    if(mapStage==0)
    {
     MapProbe(actor);
     if(!Environment.GetCommandLineArgs().Contains("--lelan-map-verify")){Finish(null);return;}
     Run(actor);
     if(!releaseBootstrap)Check(Call("WovenPotentialNativeSmoke","Run")!=null,"Native Woven threshold detour and serialization pass");
     var manager=Il2CppLE.UI.Minimap.MinimapIconManager.Instance;
     foreach(var row in manager.icons)if(row.icon?.name=="TM Enemy minimap dot"){removedMapIcon=row.icon;break;}
     Check(removedMapIcon!=null,"Native minimap registers enemy dot with UI");
     manager.Remove(removedMapIcon);
     Check(!manager.Exists(removedMapIcon),"QA removes only its enemy marker to simulate minimap UI rebuild");
     mapStage=1;mapStarted=Time.unscaledTime;return;
    }
    Call("EnemyMapDots","Tick");
    var native=Il2CppLE.UI.Minimap.MinimapIconManager.Instance;
    Check(native.Exists(removedMapIcon),"Enemy dot registration recovers after native UI removal");
    for(int i=0;i<120;i++)Call("EnemyMapDots","Tick");
    int same=0;foreach(var row in native.icons)if(row.icon?.Pointer==removedMapIcon.Pointer)same++;
    Check(same==1,"Repeated enemy updates do not duplicate native icon registration");
    Call("EnemyMapDots","Clear");
    Check(!native.Exists(removedMapIcon) && (int)Call("EnemyMapDots","get_Count")==0,"Enemy cleanup removes native icon and cache");
    Finish(null);return;
   }
   Run(actor);
   if(!releaseBootstrap)Check(Call("WovenPotentialNativeSmoke","Run")!=null,"Native Woven threshold detour and serialization pass");
   if(!Environment.GetCommandLineArgs().Contains("--lelan-class-panel-qa")){Finish(null);return;}
   var data=actor.characterDataTracker.charData;migrationId=data.Id;migrationClass=data.CharacterClass==4?1:4;
   Call("ClassRespecialization","Open");Call("ClassRespecialization","Select",migrationClass);
   Check((int)Call("ClassRespecialization","get_Selected")==migrationClass && !(bool)Call("ClassRespecialization","get_Busy"),"Real offline panel selection waits for confirmation");
   Call("ClassRespecialization","Confirm");
   Check((bool)Call("ClassRespecialization","get_Busy"),"Real offline confirmation starts guarded class transition");
   migrating=true;migrationStarted=Time.unscaledTime;
  }
  catch(Exception ex){Finish(ex);}
 }

 void MapProbe(Actor actor)
 {
  var local=PlayerFinder.getLocalActorSync()?.actorVisuals?.alignment;
  var rows=new List<object>();
  var groups=ActorManager.instance?.visuals;
  if(groups!=null)foreach(var group in groups)
  {
   if(group?.visuals==null)continue;
   for(int i=0;i<group.visuals.Count;i++)
   {
    var v=group.visuals[i];if(v==null)continue;
    rows.Add(new{v.name,active=v.isActiveAndEnabled,v.isPlayer,v.dead,v.isCorpse,alignment=v.alignment?.name,foe=local!=null&&v.alignment!=null&&v.alignment.IsFoe(local),enemy=v.alignment?.IsEnemyOfGood,initialized=v.HasBeenInitialized,originalIcon=v.minimapIcon!=null});
   }
  }
  for(int i=0;i<120;i++)Call("EnemyMapDots","Tick");
  var icons=Il2CppLE.UI.Minimap.MinimapIconManager.Instance;
  var details=new List<object>();
  foreach(var e in (System.Collections.IEnumerable)T("EnemyMapDots").GetField("entries",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null))
  {
   object F(string field)=>e.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(e);
   var icon=(Il2CppDMM.DMMapIcon)F("Icon");var anchor=(GameObject)F("Anchor");
   bool exists=icon!=null && icons.Exists(icon);var sprite=icon!=null?icon.sprite:null;
   details.Add(new{kind=F("Kind"),checkAt=F("CheckAt"),icon=icon?.name,anchor=anchor?.name,active=anchor?.activeInHierarchy,iconEnabled=icon?.enabled,sprite=sprite!=null?sprite.name:"missing/destroyed",exists,artRetained=sprite!=null && (sprite.hideFlags & HideFlags.DontUnloadUnusedAsset)!=0});
  }

  var iconRows=new List<object>();if(icons?.icons!=null)foreach(var row in icons.icons)iconRows.Add(new{icon=row.icon?.name,sprite=row.icon?.sprite?.name,ui=row.ui?.name,active=row.ui?.gameObject?.activeInHierarchy});
  File.WriteAllText(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),"UserData","map-probe.json"),JsonSerializer.Serialize(new{ready=Call("OfflineToolkit","get_Ready"),local=local?.name,groups=groups?.Length,rows,entryCount=Call("EnemyMapDots","get_Count"),details,iconRows},new JsonSerializerOptions{WriteIndented=true}));
 }
 void Finish(Exception ex)
 {
  done=true;EchoLoadProfile.Finish(ex);File.WriteAllText(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),"UserData","item-node-native-qa.json"),JsonSerializer.Serialize(new{passed=ex==null,error=ex?.ToString(),gameAssembly=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),"GameAssembly.dll")))),checks,localizationKeys,scope=Environment.GetCommandLineArgs().Contains("--lelan-echo-load-500-qa") ? "Real detached native empowered echo at requested corruption, native scene loading and player placement; private helper timers, not a rendered multiplayer/loading regression for all maps" : Environment.GetCommandLineArgs().Contains("--lelan-prophecy-stream-qa") ? "Real IL2CPP hostile pooled actor acquisition across scenes, nullable reward detour, native item generation counts/lens outcomes and 80-frame scheduling; ground placement intercepted in private QA; no live pursuit/death-hitch measurement" : Environment.GetCommandLineArgs().Contains("--lelan-echo-aggro-qa") ? "Native IL2CPP acquisition-box detour and scope restoration; echo actor eligibility tested separately by actual-source fixture; no live enemy pursuit measurement" : Environment.GetCommandLineArgs().Contains("--lelan-forging-half-qa") ? "Real IL2CPP FP cost consumers on disposable items; no character/save or rendered UI test" : "Real IL2CPP stat, attribute, tooltip parser and Meteor temp-stat consumers on disposable private character; no rendered tooltip or monster-hit measurement"},new JsonSerializerOptions{WriteIndented=true}));Application.Quit();
 }
 ItemDataUnpacked Item(UniqueList.Entry entry,byte roll)
 {
  var data=ItemData.DummyData(entry.baseType,entry.subTypes[0],7);data.uniqueID=445;data.SetAllUniqueRolls(roll);data.RollIndividualID();data.RebuildID();return new ItemDataUnpacked(data.GetIDAsArray());
 }
 float Value(Stats stats,SP property,byte special=0)=>stats.GetStatValue(property,property==SP.Damage?AT.Spell:AT.None,0,0,0,special,0,true,false);
 void Run(Actor actor)
 {
  string expected=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),"UserData","GuestProfile"));
  Check(Il2CppLE.Core.PlatformFileManager.ApplicationSavePath==expected && actor.characterDataTracker.charData.CharacterName.StartsWith("LANQA"),"Verified private profile and disposable QA character");
  VerifyDisintegrate(actor);
  var stats=actor.GetComponent<CharacterStats>();var equip=actor.itemContainersManager.itemEquipManager;var savedStats=stats.stats;var savedItems=equip.equippedItems;
  var definition=UniqueList.getUnique(445);
  try
  {
   void ResetSources()
   {
    Call("SpiritXylem","Clear");equip.equippedItems=new();stats.stats=new();
    // Zero native cached attributes in a scratch stat list, then discard the
    // resulting delta rows. Each case starts with clean native +2 mana/ATT.
    stats.ApplyCoreAttributeModifiers();stats.stats=new();UniqueList.getUnique(445);
   }
   foreach(byte roll in new byte[]{0,255})
   {
    ResetSources();var item=Item(definition,roll);var identity=item.GetIDAsArray().ToArray();
    string tooltip=string.Join("\n",Enumerable.Range(0,4).Select(index=>ItemTooltipContentBuilder.GetUniqueModifierInfoFromTooltipDescription(item,definition,index,(TooltipItemManager.SlotType)0).GetModifierString(false)));
    Check(tooltip.Contains("All Skills") && tooltip.Contains("Attunement") && !tooltip.Contains("Intelligence") && tooltip.Contains("Health and Mana per Attunement") && tooltip.Contains("+1 Spell Damage per 25 Maximum Mana") && !tooltip.Contains("If "),"Native tooltip parser renders all four custom effects",new{roll,tooltip});
    var notes=(string[])T("SpiritXylemRules").GetField("EffectDetails",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
    for(int index=0;index<4;index++)
    {
     var info=ItemTooltipContentBuilder.GetUniqueModifierInfoFromTooltipDescription(item,definition,index,(TooltipItemManager.SlotType)0);
     string note=info.GetPropertyAltText(false);
     Check(note==notes[index] && !note.Contains("Intelligence"),"Native note matches this effect, without stale INT equality",new{roll,index,note});
    }
    Check(definition.mods.Count==2 && definition.tooltipDescriptions.Count==4,"Native registry retains exactly two mana-threshold mods and four separate effects descriptions");
    equip.UpdateStats(item,true,false,0,0,0,0);
    bool Matches(SP property,int tags,int special,float more)
    {
     foreach(var st in stats.stats)if(st.property==property && (int)st.tags==tags && st.specialTag==special && st.moreValues.ToArray().Any(v=>Near(v,more)))return true;
     return false;
    }
    Check(!stats.stats.ToArray().Any(st=>st.property==SP.AbilityProperty && (int)st.tags==253 && st.specialTag==2),"Native equip does not apply the removed reduced-health-cost modifier");
    Check(Matches(SP.GlobalConditionalDamage,0,40,roll==0?.1f:.2f) && Matches(SP.PlayerProperty,598,0,roll==0?-.2f:-.1f),"Native equip retains both original boss/rare mana-threshold MORE modifiers");
    foreach(var values in new[]{(Att:50,Int:30),(Att:30,Int:50),(Att:30,Int:30)})
    foreach(int all in new[]{0,5})
    {
     ResetSources();
     stats.stats.Add(new Stats.Stat(SP.Attunement,0,values.Att,0,0,0,0));stats.stats.Add(new Stats.Stat(SP.Intelligence,0,values.Int,0,0,0,0));stats.stats.Add(new Stats.Stat(SP.AllAttributes,0,all,0,0,0,0));
     stats.stats.Add(new Stats.Stat(SP.Health,0,1000,0,0,0,0));stats.stats.Add(new Stats.Stat(SP.Mana,0,100,0,0,0,0));
     equip.equippedItems.Add(new ItemEquipManager.EquippedItem(item,true,(ItemEquipManager.GearType)0));
     stats.ApplyCoreAttributeModifiers();
     int final=values.Att+all+(roll==0?10:16);int level=roll==0?3:4;int per=roll==0?3:6;float hp=per*final;
     Check(stats.GetAttributeValue(CoreAttribute.Attribute.Intelligence)==values.Int+all && stats.GetAttributeValue(CoreAttribute.Attribute.Attunement)==final,"Native rolled ATT bonus applies independently from INT",new{roll,values.Att,values.Int,all,expected=final,intelligence=stats.GetAttributeValue(CoreAttribute.Attribute.Intelligence),attunement=stats.GetAttributeValue(CoreAttribute.Attribute.Attunement)});
     Check(Near(Value(stats,SP.LevelOfSkills),level),"Native stat consumer reads global All Skills levels",new{roll,level,actual=Value(stats,SP.LevelOfSkills)});
     var nativeTrees=actor.localTreeData.specialisedSkillTrees;
     try
     {
      actor.localTreeData.specialisedSkillTrees=new();
      var trees=new List<LocalTreeData.SkillTreeData>();
      foreach(var abilityID in new[]{AbilityID.fireball,AbilityID.summonThornTotem,AbilityID.puncture,AbilityID.rive1})
      {
       var ability=Ability.getAbility(abilityID);var tree=new LocalTreeData.SkillTreeData(ability.playerAbilityID,(byte)trees.Count){level=20};trees.Add(tree);actor.localTreeData.specialisedSkillTrees.Add(tree);
      }
      actor.localTreeData.setAdditionalMaxPointsFromStatsOnServerOrInSingleplayer(stats.stats,actor.GetMutatorManager(),false);
      Check(trees.All(t=>t.additionalMaxPointsFromStats==level),"Native skill points apply to INT, ATT, DEX and STR skills",new{roll,level,points=trees.Select(t=>t.additionalMaxPointsFromStats).ToArray()});
     }
     finally{actor.localTreeData.specialisedSkillTrees=nativeTrees;}
     var own=(List<Stats.Stat>)T("SpiritXylem").GetField("injected",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
     float Contribution(SP property)
     {
      float with=Value(stats,property);var held=own.Where(st=>st.property==property).ToArray();
      foreach(var st in held)stats.stats.Remove(st);float without=Value(stats,property);foreach(var st in held)stats.stats.Add(st);return with-without;
     }
     Check(Near(Contribution(SP.Health),hp) && Near(Contribution(SP.Mana),hp),"Native HP and MP consumers both apply rolled per-ATT bonus",new{roll,final,per,health=Value(stats,SP.Health),mana=Value(stats,SP.Mana),healthContribution=Contribution(SP.Health),manaContribution=Contribution(SP.Mana)});
     Check(Near(Value(stats,SP.Mana),100+(2+per)*final) && Near(Value(stats,SP.Health),1000+6*all+hp),"Native +2 mana per ATT stacks with item for exactly +5 to +8 mana per ATT",new{roll,final,baseMana=100,nativeMana=2*final,itemMana=hp,totalMana=Value(stats,SP.Mana),expectedMana=100+(2+per)*final,health=Value(stats,SP.Health),expectedHealth=1000+6*all+hp,nativeVitalityHealth=6*all});
     stats.ApplyExternalStats();
     Check(actor.mana.maxMana==(int)Value(stats,SP.Mana) && Near(Value(stats,SP.Damage),actor.mana.maxMana/25f),"Native external-stat refresh automatically derives maximum Mana and spell bonus",new{maximumMana=actor.mana.maxMana,statMana=Value(stats,SP.Mana),spell=Value(stats,SP.Damage)});
     var baseDamage=new DamageStatsHolder.BaseDamageStats();baseDamage.damage[0]=100;
     float SpellDamage()=>DamageStats.buildDamageStats(baseDamage,AT.Spell,stats,stats.stats,0,0,(DamageType)0).damage.ToArray().Sum();
     float withSpell=SpellDamage();
     float AttackDamage()=>DamageStats.buildDamageStats(baseDamage,AT.Melee,stats,stats.stats,0,0,(DamageType)0).damage.ToArray().Sum();
     float withAttack=AttackDamage();var spellRows=own.Where(st=>st.property==SP.Damage).ToArray();
     foreach(var st in spellRows)stats.stats.Remove(st);float withoutSpell=SpellDamage(),withoutAttack=AttackDamage();foreach(var st in spellRows)stats.stats.Add(st);
     Check(Near(withSpell-withoutSpell,actor.mana.maxMana/25f),"Native damage builder consumes exactly +1 Spell Damage per25 maximum Mana",new{roll,final,withSpell,withoutSpell,delta=withSpell-withoutSpell});
     Check(Near(withAttack,withoutAttack),"Added Spell Damage does not leak into native melee damage",new{withAttack,withoutAttack});
     float health=Value(stats,SP.Health),mana=Value(stats,SP.Mana),spell=Value(stats,SP.Damage);
     for(int repeat=0;repeat<100;repeat++)stats.ApplyCoreAttributeModifiers();
     Check(stats.GetAttributeValue(CoreAttribute.Attribute.Attunement)==final && stats.GetAttributeValue(CoreAttribute.Attribute.Intelligence)==values.Int+all && Near(Value(stats,SP.Health),health) && Near(Value(stats,SP.Mana),mana) && Near(Value(stats,SP.Damage),spell),"100 native refreshes do not stack native mana or item bonuses");
     equip.equippedItems.Clear();stats.ApplyCoreAttributeModifiers();
     Check(own.Count==0 && Near(Value(stats,SP.LevelOfSkills),0) && Near(Value(stats,SP.Damage),0) && stats.GetAttributeValue(CoreAttribute.Attribute.Attunement)==values.Att+all && stats.GetAttributeValue(CoreAttribute.Attribute.Intelligence)==values.Int+all && Near(Value(stats,SP.Mana),100+2*(values.Att+all)),"Native unequip restores original INT/ATT, retains base mana and removes all item effects");
     Check(identity.SequenceEqual(item.GetIDAsArray().ToArray()),"Native recalculation never changes item ID/rolls/LP/affixes");
    }
   }
  }
  finally{Call("SpiritXylem","Clear");stats.stats=savedStats;equip.equippedItems=savedItems;stats.UpdateStatsInternal();}
  var local=actor.localTreeData;var originalTrees=local.specialisedSkillTrees;int originalMana=actor.mana.maxMana;
  var meteor=local.GetComponent<MeteorMutator>()??actor.GetMutatorManager().getMutator(Ability.getAbility(AbilityID.meteor))?.TryCast<MeteorMutator>();
  bool created=meteor==null;
  if(created){meteor=local.gameObject.AddComponent<MeteorMutator>();meteor.actor=actor;meteor.myMana=actor.mana;meteor.mutatorManager=actor.GetMutatorManager();meteor.ability=Ability.getAbility(AbilityID.meteor);}
  Check(meteor!=null,"Native Meteor component resolved for private Sorcerer-skill test",new{createdForBaseMage=created});
  var originalUnconditional=meteor.unconditionalTempStats;var originalConditional=meteor.conditionalTempStats;
  var scratchObject=new GameObject("Private stat consumer QA");scratchObject.SetActive(false);var scratch=scratchObject.AddComponent<Stats>();
  try
  {
   local.specialisedSkillTrees=new();var tree=new LocalTreeData.SkillTreeData("me27",0){level=20};tree.nodes.Add(new LocalTreeData.NodeData(10,4));local.specialisedSkillTrees.Add(tree);
   int before=tree.getUnspentPoints();MeteorSkillTree.updateMutator(local,tree);Check(tree.nodes[0].pointsAllocated==1 && tree.getUnspentPoints()==before+3,"Native skill points refund3 when old Stardust rank4 becomes1",new{before,after=tree.getUnspentPoints()});
   foreach(int mana in new[]{0,500,1000,2000})
   {
    actor.mana.maxMana=mana;tree.nodes[0].pointsAllocated=0;MeteorSkillTree.updateMutator(local,tree);var baseline=meteor.getTempStats();
    tree.nodes[0].pointsAllocated=1;MeteorSkillTree.updateMutator(local,tree);Check(meteor.addsTempStats(),"Native Meteor enables temp stats for Stardust");var modified=meteor.getTempStats();
    foreach(AT tag in new[]{AT.Fire,AT.Cold,AT.Physical})
    {
     float Damage(NStats rows){scratch.stats=new();foreach(var row in rows)scratch.stats.Add(row);scratch.stats.Add(Stats.MoreStat(SP.Damage,0,.25f,0,0));return scratch.GetStatValue(SP.Damage,tag,100,0,0,0,0,true,false);}
     float a=Damage(baseline),b=Damage(modified);Check(Near(b,a*(1+mana*.001f)),"Native Damage MORE multiplies independently for each element",new{mana,tag=tag.ToString(),baseline=a,modified=b});
    }
    Check(meteor.conditionalTempStats.Count==baseline.Count,"Native temp-stat cache has no injected accumulation");
   }
  }
  finally{local.specialisedSkillTrees=originalTrees;actor.mana.maxMana=originalMana;meteor.unconditionalTempStats=originalUnconditional;meteor.conditionalTempStats=originalConditional;UnityEngine.Object.Destroy(scratchObject);}
 }
}
