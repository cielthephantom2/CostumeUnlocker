using System.Text.Json;
using System.Reflection;
using Il2Cpp;
using LastEpochLAN.Mod.OfflineTools;
var checks=new List<string>();void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);}
UniqueList.Entry definition;CharacterStats stats;Actor actor;ItemData item;
UniqueItemMod Mod(SP p,int tag,byte special,byte roll,float min,float max,BaseStats.ModType type=BaseStats.ModType.ADDED)=>new(){property=p,tags=(AT)tag,specialTag=special,rollID=roll,value=min,maxValue=max,canRoll=true,type=type};
void Reset(float att=50,float intel=30,byte roll=0){
 SpiritXylem.Clear();OfflineToolkit.Enabled=true;UniqueList.instance=new();definition=new(){uniqueID=445,name="Spirit Xylem"};
 definition.mods.Add(Mod(SP.ManaCost,256,0,0,-.2f,-.1f,BaseStats.ModType.INCREASED));definition.mods.Add(Mod(SP.AbilityProperty,253,2,1,.1f,.2f));definition.mods.Add(Mod(SP.PlayerProperty,596,0,2,3,6));definition.mods.Add(Mod(SP.PlayerProperty,597,0,2,3,6));definition.mods.Add(Mod(SP.PlayerProperty,598,0,3,-.2f,-.1f,BaseStats.ModType.MORE));definition.mods.Add(Mod(SP.GlobalConditionalDamage,0,40,4,.1f,.2f,BaseStats.ModType.MORE));definition.mods.Add(Mod(SP.LevelOfSkills,0,11,5,3,4));definition.mods.Add(Mod(SP.Attunement,0,0,6,10,16));UniqueList.instance.uniques.Add(definition);
 actor=PlayerFinder.Player=new();stats=new(){actor=actor};stats.stats.Add(new(SP.Attunement,0,att,0,0,0,0));stats.stats.Add(new(SP.Intelligence,0,intel,0,0,0,0));item=new(){uniqueID=445,uniqueRolls=Enumerable.Repeat(roll,8).ToArray()};actor.itemContainersManager.itemEquipManager.equippedItems.Add(new(){Item=item});
}
float Value(SP p)=>stats.stats.Where(s=>s.property==p).Sum(s=>s.addedValue);
bool Near(float a,float b)=>Math.Abs(a-b)<.001f;
Reset();var originalMods=definition.mods;var originalDescriptions=definition.tooltipDescriptions;var originalEntries=definition.tooltipEntries;var damage=originalMods[5];var defense=originalMods[4];SpiritXylem.Configure(definition);
Check(definition.mods.Count==2 && definition.mods[0]==damage && definition.mods[1]==defense,"retain exact native boss/rare damage and defense; remove health cost reduction");
Check(definition.tooltipEntries.Select(e=>e.modDisplay).SequenceEqual(new byte[]{128,129,130,131,0,1}) && definition.tooltipDescriptions.Count==4,"four custom effects and two native effects in order");
Check(definition.tooltipDescriptions.Select(d=>d.description).SequenceEqual(SpiritXylemRules.EffectLines),"each effect has its own native description row");
Check(definition.tooltipDescriptions.Select(d=>d.altText).SequenceEqual(SpiritXylemRules.EffectDetails) && SpiritXylemRules.EffectDetails.Distinct().Count()==4,"each effect has its own matching note, never a repeated old equality note");
Check(SpiritXylemRules.EffectDetails[0]=="Range: 3 to 4" && SpiritXylemRules.EffectDetails[1]=="Range: 10 to 16" && SpiritXylemRules.EffectDetails[2].StartsWith("Range: 3 to 6") && SpiritXylemRules.EffectDetails[3].Contains("1000 maximum Mana grants +40"),"range and maximum-Mana notes match new values");
Check(!SpiritXylemRules.EffectsText.Contains("Intelligence") && !string.Join(" ",SpiritXylemRules.EffectDetails).Contains("Intelligence"),"old Intelligence bonuses and equality notes absent");
SpiritXylem.Configure(definition);Check(definition.mods.Count==2 && definition.tooltipDescriptions.Count==4,"configuration is idempotent");
SpiritXylem.Clear();Check(ReferenceEquals(definition.mods,originalMods) && ReferenceEquals(definition.tooltipDescriptions,originalDescriptions) && ReferenceEquals(definition.tooltipEntries,originalEntries),"cleanup restores original native definition");
foreach(byte roll in new byte[]{0,255}){
 Reset(50,30,roll);var bytes=item.id.ToArray();var rolls=item.uniqueRolls.ToArray();SpiritXylem.Apply(stats);
 float final=50+(roll==0?10:16),per=roll==0?3:6;
 Check(Value(SP.Attunement)==final && Value(SP.Intelligence)==30,"rolled ATT applies without changing INT: "+roll);
 Check(Value(SP.LevelOfSkills)==(roll==0?3:4) && Value(SP.Health)==per*final && Value(SP.Mana)==per*final,"skills and HP/MP per ATT apply together: "+roll);
 Check(Near(Value(SP.Damage),40) && stats.stats.Single(s=>s.property==SP.Damage).tags==AT.Spell,"1000 maximum Mana grants40 spell-only damage: "+roll);
 for(int n=0;n<100;n++)SpiritXylem.Apply(stats);
 Check(Value(SP.Attunement)==final && Value(SP.Mana)==per*final && Near(Value(SP.Damage),40),"100 recalculations do not accumulate effects: "+roll);
 Check(item.id.SequenceEqual(bytes) && item.uniqueRolls.SequenceEqual(rolls) && item.legendaryPotential==3,"identity, rolls and LP preserved: "+roll);
}
Reset();SpiritXylem.Apply(stats);actor.mana.currentMana=0;SpiritXylem.RefreshMaximumMana(stats);Check(Near(Value(SP.Damage),40),"empty current Mana does not reduce bonus");
actor.mana.maxMana=1025;SpiritXylem.RefreshMaximumMana(stats);Check(Near(Value(SP.Damage),41) && stats.Requests==1,"maximum Mana change refreshes spell source and requests native cache update");
SpiritXylem.RefreshMaximumMana(stats);Check(stats.Requests==1,"unchanged maximum Mana does not schedule endless updates");
actor.mana.maxMana=1001;SpiritXylem.RefreshMaximumMana(stats);Check(Near(Value(SP.Damage),40.04f),"fractional native spell damage preserved per25 maximum Mana");
actor.mana.maxMana=0;SpiritXylem.RefreshMaximumMana(stats);Check(Value(SP.Damage)==0,"zero maximum Mana removes damage without leaving stale bonus");
actor.mana.maxMana=400;SpiritXylem.RefreshMaximumMana(stats);Check(Near(Value(SP.Damage),16),"zero source can recover when maximum Mana increases");
Reset();stats.stats.Add(new(SP.AllAttributes,0,5,0,0,0,0));SpiritXylem.Apply(stats);Check(Value(SP.Attunement)+Value(SP.AllAttributes)==65 && Value(SP.Intelligence)==30 && Value(SP.Health)==195,"All Attributes contributes to ATT scaling without INT equality");
Reset();actor.itemContainersManager.itemEquipManager.equippedItems[0].globalStatModifier=.5f;SpiritXylem.Apply(stats);Check(Value(SP.Attunement)==65 && Value(SP.Health)==292.5f && Value(SP.LevelOfSkills)==4.5f && Near(Value(SP.Damage),60),"increased item effect scales each bonus once");
Reset();actor.itemContainersManager.itemEquipManager.equippedItems.Add(new(){Item=item});SpiritXylem.Apply(stats);Check(Value(SP.Attunement)==70 && Value(SP.Mana)==420 && Near(Value(SP.Damage),80),"multiple sources add independently without stale scaling");
Reset();SpiritXylem.Apply(stats);actor.mana.maxMana=100+2*60+180;SpiritXylem.RefreshMaximumMana(stats);Check(Near(Value(SP.Damage),16),"simulated400 maximum Mana includes native +2/ATT and item +3/ATT before spell scaling");
Reset();actor.itemContainersManager.itemEquipManager.equippedItems[0].StatsApplied=false;SpiritXylem.Apply(stats);Check(stats.stats.Count==2,"unapplied equipment grants nothing");
Reset();item.uniqueID=444;SpiritXylem.Apply(stats);Check(stats.stats.Count==2,"other uniques unaffected");
Reset();SpiritXylem.Apply(stats);actor.itemContainersManager.itemEquipManager.equippedItems.Clear();SpiritXylem.Apply(stats);Check(stats.stats.Count==2 && Value(SP.Attunement)==50 && Value(SP.Intelligence)==30,"unequip removes all synthetic sources");
Reset();SpiritXylem.Apply(stats);var old=stats;actor=PlayerFinder.Player=new();stats=new(){actor=actor};actor.itemContainersManager.itemEquipManager.equippedItems.Add(new(){Item=item});SpiritXylem.Apply(stats);Check(old.stats.Count==2 && old.Requests>0 && Value(SP.Attunement)==10,"actor replacement clears old sources");
Reset();PlayerFinder.Player=new();SpiritXylem.Apply(stats);Check(stats.stats.Count==2,"remote actors unaffected");
Reset();SpiritXylem.Apply(stats);OfflineToolkit.Enabled=false;SpiritXylem.Tick();Check(stats.stats.Count==2 && definition.mods.Count==8,"online transition restores native assets and stats");
Reset();item.uniqueRolls=Array.Empty<byte>();typeof(OfflineSpiritXylemConditionPatch).GetMethod("Prefix",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{stats});Check(stats.stats.Count==2 && MelonLoader.MelonLogger.Messages.Count>0,"malformed rolls leave no partial sources");
Reset();stats.stats[0].addedValue=float.NaN;SpiritXylem.Apply(stats);Check(stats.stats.Count==2,"invalid attribute does not inject invalid stats");
var localization=typeof(OfflineSpiritXylemLocalizationPatch).GetMethod("Prefix",BindingFlags.NonPublic|BindingFlags.Static);
for(int i=0;i<4;i++)foreach(bool alt in new[]{false,true}){
 string key="Unique_Tooltip_"+i+"_445"+(alt?"_Alt":"");object[] localized={key,"old equality note"};
 Check(!(bool)localization.Invoke(null,localized) && (string)localized[1]==(alt?SpiritXylemRules.EffectDetails[i]:SpiritXylemRules.EffectLines[i]),"correct localized effect/note: "+key);
}
object[] other={"Unique_Tooltip_4_445","native"};Check((bool)localization.Invoke(null,other),"obsolete fifth custom row is no longer overridden");
OfflineToolkit.Enabled=false;object[] online={"Unique_Tooltip_0_445","online"};Check((bool)localization.Invoke(null,online),"online tooltip stays native");
Directory.CreateDirectory("validation");File.WriteAllText("validation/xylem-mana-fixture.json",JsonSerializer.Serialize(new{passed=checks.Count,checks,actualSource=true,nativeGameplayValidated=false},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("PASS "+checks.Count+" actual-source Spirit Xylem checks");
