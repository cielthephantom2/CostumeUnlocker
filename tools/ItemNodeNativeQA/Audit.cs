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
public class Audit:MelonMod
{
 static readonly List<string> localizationKeys=new();
 public static void ObserveKey(string key){if(key?.Contains("445")==true && !localizationKeys.Contains(key))localizationKeys.Add(key);}
 public override void OnInitializeMelon(){HarmonyInstance.Patch(typeof(Localization).GetMethod("GetText",new[]{typeof(string),typeof(string)}),prefix:new HarmonyMethod(typeof(Audit).GetMethod(nameof(ObserveKey))));}
 bool done;float started;readonly List<object> checks=new();
 static Type T(string name)=>AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="LastEpochLAN").GetType("LastEpochLAN.Mod.OfflineTools."+name,true);
 static object Call(string name,string method,params object[] args)=>T(name).GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public).Invoke(null,args);
 void Check(bool ok,string label,object values=null){checks.Add(new{passed=ok,label,values});if(!ok)throw new Exception(label+": "+JsonSerializer.Serialize(values));}
 static bool Near(float a,float b)=>Math.Abs(a-b)<.01f;
 public override void OnUpdate()
 {
  if(done)return;if(started==0)started=Time.unscaledTime;
  var actor=PlayerFinder.getPlayerActor();
  if(actor?.HasMovedToPlayerSpawn!=true){if(Time.unscaledTime-started>150)Finish(new TimeoutException("Private actor bootstrap timed out"));return;}
  done=true;try{Run(actor);Finish(null);}catch(Exception ex){Finish(ex);}
 }
 void Finish(Exception ex)
 {
  done=true;File.WriteAllText(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),"UserData","item-node-native-qa.json"),JsonSerializer.Serialize(new{passed=ex==null,error=ex?.ToString(),gameAssembly=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),"GameAssembly.dll")))),checks,localizationKeys,scope="Real IL2CPP stat, attribute, tooltip parser and Meteor temp-stat consumers on disposable private character; no rendered tooltip or monster-hit measurement"},new JsonSerializerOptions{WriteIndented=true}));Application.Quit();
 }
 ItemDataUnpacked Item(UniqueList.Entry entry,byte roll)
 {
  var data=ItemData.DummyData(entry.baseType,entry.subTypes[0],7);data.uniqueID=445;data.SetAllUniqueRolls(roll);data.RollIndividualID();data.RebuildID();return new ItemDataUnpacked(data.GetIDAsArray());
 }
 float Value(Stats stats,SP property,byte special=0)=>stats.GetStatValue(property,AT.None,0,0,0,special,0,true,false);
 void Run(Actor actor)
 {
  string expected=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),"UserData","GuestProfile"));
  Check(Il2CppLE.Core.PlatformFileManager.ApplicationSavePath==expected && actor.characterDataTracker.charData.CharacterName.StartsWith("LANQA"),"Verified private profile and disposable QA character");
  var stats=actor.GetComponent<CharacterStats>();var equip=actor.itemContainersManager.itemEquipManager;var savedStats=stats.stats;var savedItems=equip.equippedItems;
  var definition=UniqueList.getUnique(445);
  try
  {
   foreach(byte roll in new byte[]{0,255})
   {
    var item=Item(definition,roll);
    var tooltip=new List<string>();foreach(int index in new[]{0,1})tooltip.Add(ItemTooltipContentBuilder.GetUniqueModifierInfoFromTooltipDescription(item,definition,index,(TooltipItemManager.SlotType)0).GetModifierString(false));
    Check(tooltip[0].Contains("If Attunement") && tooltip[0].Contains("Attunement Skills") && tooltip[0].Contains("Health and Mana per Attunement") && tooltip[1].Contains("Spell Damage per 2 Intelligence"),"Native localization + tooltip parser renders both complete branches",new{roll,tooltip});
    Call("SpiritXylem","Clear");UniqueList.getUnique(445);stats.stats=new();equip.equippedItems=new();
    equip.UpdateStats(item,true,false,0,0,0,0);
    var nativeMods=new List<object>();
    foreach(var stat in stats.stats)nativeMods.Add(new{property=stat.property.ToString(),tags=(int)stat.tags,stat.specialTag,stat.addedValue,stat.increasedValue,more=stat.moreValues?.ToArray()});
    bool Matches(SP property,int tags,int special,float added,float more)
    {
     foreach(var st in stats.stats)if(st.property==property && (int)st.tags==tags && st.specialTag==special && Near(st.addedValue,added) && (more==0 || st.moreValues.ToArray().Any(v=>Near(v,more))))return true;
     return false;
    }
    Check(Matches(SP.AbilityProperty,253,2,roll==0?.1f:.2f,0),"Native ItemEquipManager applies reduced spell health cost",nativeMods);
    Check(Matches(SP.GlobalConditionalDamage,0,40,0,roll==0?.1f:.2f),"Native ItemEquipManager applies boss/rare more damage above half mana",nativeMods);
    Check(Matches(SP.PlayerProperty,598,0,0,roll==0?-.2f:-.1f),"Native ItemEquipManager applies boss/rare less damage below half mana",nativeMods);
    foreach(bool att in new[]{true,false})
    foreach(int all in new[]{0,5})
    {
     Call("SpiritXylem","Clear");UniqueList.getUnique(445);
     stats.stats=new();stats.stats.Add(new Stats.Stat(SP.Attunement,0,att?50:30,0,0,0,0));stats.stats.Add(new Stats.Stat(SP.Intelligence,0,att?30:50,0,0,0,0));stats.stats.Add(new Stats.Stat(SP.AllAttributes,0,all,0,0,0,0));
     equip.equippedItems=new();equip.equippedItems.Add(new ItemEquipManager.EquippedItem(item,true,(ItemEquipManager.GearType)0));
     stats.ApplyCoreAttributeModifiers();
     int final=(roll==0?60:70)+all;int level=roll==0?3:4;float hp=(roll==0?3:6)*final;
     Check(stats.GetAttributeValue(att?CoreAttribute.Attribute.Attunement:CoreAttribute.Attribute.Intelligence)==final,"Native core attribute consumes conditional attribute",new{roll,att,expected=final});
     Check(Near(Value(stats,SP.LevelOfSkills,(byte)(att?11:9)),level),"Native stat consumer sees correct tagged skill levels",new{roll,att,actual=Value(stats,SP.LevelOfSkills,(byte)(att?11:9)),level});
     var nativeTrees=actor.localTreeData.specialisedSkillTrees;
     try
     {
      actor.localTreeData.specialisedSkillTrees=new();
      var attAbility=Ability.getAbility(AbilityID.summonThornTotem);var intAbility=Ability.getAbility(AbilityID.fireball);
      var attTree=new LocalTreeData.SkillTreeData(attAbility.playerAbilityID,0){level=20};var intTree=new LocalTreeData.SkillTreeData(intAbility.playerAbilityID,1){level=20};
      actor.localTreeData.specialisedSkillTrees.Add(attTree);actor.localTreeData.specialisedSkillTrees.Add(intTree);
      actor.localTreeData.setAdditionalMaxPointsFromStatsOnServerOrInSingleplayer(stats.stats,actor.GetMutatorManager(),false);
      Check(attTree.additionalMaxPointsFromStats==(att?level:0) && intTree.additionalMaxPointsFromStats==(att?0:level),"Native skill maximum points apply only to the matching attribute skill",new{roll,att,attPoints=attTree.additionalMaxPointsFromStats,intPoints=intTree.additionalMaxPointsFromStats});
     }
     finally{actor.localTreeData.specialisedSkillTrees=nativeTrees;}
     var own=(List<Stats.Stat>)T("SpiritXylem").GetField("injected",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
     float Contribution(SP property)
     {
      float with=Value(stats,property);var held=new List<Stats.Stat>();
      for(int index=stats.stats.Count-1;index>=0;index--)if(own.Any(st=>st.Pointer==stats.stats[index].Pointer) && stats.stats[index].property==property){held.Add(stats.stats[index]);stats.stats.RemoveAt(index);}
      float without=Value(stats,property);foreach(var st in held)stats.stats.Add(st);return with-without;
     }
     Check(att?Near(Contribution(SP.Health),hp)&&Near(Contribution(SP.Mana),hp):Near(Contribution(SP.AdaptiveSpellDamage),final*.5f),"Native stat consumer applies exact extra HP/MP or spell source alongside native attribute effects",new{roll,att,all,health=Value(stats,SP.Health),mana=Value(stats,SP.Mana),healthContribution=Contribution(SP.Health),manaContribution=Contribution(SP.Mana),spellContribution=Contribution(SP.AdaptiveSpellDamage)});
     float beforeRefresh=Value(stats,att?SP.Health:SP.AdaptiveSpellDamage);
     for(int repeat=0;repeat<20;repeat++)stats.ApplyCoreAttributeModifiers();
     Check(Near(Value(stats,att?SP.Health:SP.AdaptiveSpellDamage),beforeRefresh),"Twenty native attribute recalculations do not stack",new{roll,att,all});
     equip.equippedItems.Clear();stats.ApplyCoreAttributeModifiers();
     Check(own.Count==0 && Near(Value(stats,SP.LevelOfSkills,(byte)(att?11:9)),0) && stats.GetAttributeValue(att?CoreAttribute.Attribute.Attunement:CoreAttribute.Attribute.Intelligence)==50+all,"Native unequip removes the complete conditional source and restores attributes");
    }
   }
  }
  finally{Call("SpiritXylem","Clear");stats.stats=savedStats;equip.equippedItems=savedItems;stats.ApplyCoreAttributeModifiers();}
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
