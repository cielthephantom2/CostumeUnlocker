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
float Level(byte tag)=>stats.stats.Where(s=>s.property==SP.LevelOfSkills && s.specialTag==tag).Sum(s=>s.addedValue);
Reset();var originalMods=definition.mods;var originalDescriptions=definition.tooltipDescriptions;var originalEntries=definition.tooltipEntries;var healthCost=originalMods[1];var damage=originalMods[5];var defense=originalMods[4];SpiritXylem.Configure(definition);
Check(definition.mods.Count==3 && definition.mods[0]==healthCost && definition.mods[1]==damage && definition.mods[2]==defense,"three common effects retain native health-cost/mana-threshold mod objects");
Check(healthCost.value==.1f && healthCost.maxValue==.2f && damage.type==BaseStats.ModType.MORE && defense.value==-.2f && defense.maxValue==-.1f,"native common10to20percent health cost/damage and20to10percent less damage retain rolls/semantics");
Check(definition.tooltipEntries.Select(e=>e.modDisplay).SequenceEqual(new byte[]{0,1,2,128,129}),"native tooltip displays common mods then both conditional paragraphs");Check(definition.tooltipDescriptions[0].description.Contains("[10,20,6]") && definition.tooltipDescriptions[1].description.Contains("+1 Spell Damage per 2 Intelligence"),"new ranges and Intelligence branch included in native tooltip text");
SpiritXylem.Configure(definition);Check(definition.mods.Count==3 && definition.tooltipDescriptions.Count==2,"registry configuration is idempotent");
SpiritXylem.Clear();Check(ReferenceEquals(definition.mods,originalMods) && ReferenceEquals(definition.tooltipDescriptions,originalDescriptions) && ReferenceEquals(definition.tooltipEntries,originalEntries),"cleanup restores exact original registry references");
Reset();var bytes=item.id.ToArray();var rolls=item.uniqueRolls.ToArray();var affixes=item.affixes;SpiritXylem.Apply(stats);
Check(Value(SP.Attunement)==60 && Value(SP.Intelligence)==30,"Attunement branch adds min10 to actual core stat source");Check(Level(11)==3 && Level(9)==0,"Attunement branch grants3 levels only to Attunement-scaled skills");Check(Value(SP.Health)==180 && Value(SP.Mana)==180 && Value(SP.Damage)==0,"Attunement branch health/mana use3 per final60Attunement");Check(item.id.SequenceEqual(bytes) && item.uniqueRolls.SequenceEqual(rolls) && item.legendaryPotential==3 && ReferenceEquals(affixes,item.affixes),"item identity rolls LP and legendary affixes stay byte/reference identical");
for(int n=0;n<20;n++)SpiritXylem.Apply(stats);Check(stats.stats.Count==6 && Value(SP.Attunement)==60 && Value(SP.Health)==180,"repeated recalculation never stacks bonuses or self-locks comparison");
// With former bonus the displayed stats would favor Attunement; naked sources favor INT.
stats.stats[0].addedValue=20;stats.stats[1].addedValue=25;SpiritXylem.Apply(stats);Check(Value(SP.Attunement)==20 && Value(SP.Intelligence)==35 && Level(11)==0 && Level(9)==3,"changing base stats switches branches using attributes before its own bonus");Check(Value(SP.Health)==0 && Value(SP.Mana)==0 && Value(SP.Damage)==17.5f,"switch removes old HP/MP and adds1 spell damage per2 final35Intelligence");
stats.stats[0].addedValue=25;SpiritXylem.Apply(stats);Check(stats.stats.Count==2 && Value(SP.Damage)==0 && Value(SP.Intelligence)==25,"equal underlying attributes receive neither branch");
Reset(50,30,255);SpiritXylem.Apply(stats);Check(Value(SP.Attunement)==70 && Level(11)==4 && Value(SP.Health)==420 && Value(SP.Mana)==420,"maximum Attunement rolls give20attribute4skills6HP/MP per Attunement");
Reset(30,50,255);SpiritXylem.Apply(stats);Check(Value(SP.Intelligence)==70 && Level(9)==4 && Value(SP.Damage)==35 && Value(SP.Health)==0,"maximum Intelligence rolls give20attribute4skills35 spell damage at70INT");
Reset();actor.itemContainersManager.itemEquipManager.equippedItems[0].StatsApplied=false;SpiritXylem.Apply(stats);Check(stats.stats.Count==2,"native StatsApplied=false excludes disabled/faction/primordial-cap equipment");
Reset();actor.itemContainersManager.itemEquipManager.equippedItems[0].Item.uniqueID=444;SpiritXylem.Apply(stats);Check(stats.stats.Count==2,"other uniques unaffected");
Reset();actor.itemContainersManager.itemEquipManager.equippedItems.Clear();SpiritXylem.Apply(stats);Check(stats.stats.Count==2,"inventory/stash copies cannot grant equipped bonuses");
Reset();SpiritXylem.Apply(stats);actor.itemContainersManager.itemEquipManager.equippedItems.Clear();SpiritXylem.Apply(stats);Check(stats.stats.Count==2 && Value(SP.Health)==0,"unequipping removes every conditional stat on next native recalculation");
Reset();SpiritXylem.Apply(stats);var old=stats;var oldActor=actor;actor=PlayerFinder.Player=new();stats=new(){actor=actor};stats.stats.Add(new(SP.Intelligence,0,70,0,0,0,0));actor.itemContainersManager.itemEquipManager.equippedItems.Add(new(){Item=item});SpiritXylem.Apply(stats);Check(old.stats.Count==2 && old.Requests>0 && Value(SP.Intelligence)==80,"actor replacement removes old source before adding to new actor");
Reset();PlayerFinder.Player=new();SpiritXylem.Apply(stats);Check(stats.stats.Count==2,"remote actor never receives local item changes");
Reset();SpiritXylem.Apply(stats);OfflineToolkit.Enabled=false;SpiritXylem.Tick();Check(stats.stats.Count==2 && definition.mods.Count==8,"online transition restores native registry and conditional stats");
Reset();SpiritXylem.Apply(stats);SpiritXylem.Clear();Check(stats.stats.Count==2 && definition.mods.Count==8,"scene cleanup removes source and restores original asset");
Reset();actor.itemContainersManager.itemEquipManager.equippedItems[0].globalStatModifier=.5f;SpiritXylem.Apply(stats);Check(Value(SP.Attunement)==65 && Value(SP.Health)==292.5f,"native increased item-effect multiplier applies to conditional rolls");
Reset();item.uniqueRolls=Array.Empty<byte>();var patch=typeof(OfflineSpiritXylemConditionPatch).GetMethod("Prefix",BindingFlags.NonPublic|BindingFlags.Static);patch.Invoke(null,new object[]{stats});Check(stats.stats.Count==2 && MelonLoader.MelonLogger.Messages.Count>0,"malformed rolls preserve native stat update instead of throwing through Harmony");
Reset();Check(SpiritXylemRules.Choose(float.NaN,0)==SpiritXylemRules.Branch.None && SpiritXylemRules.Choose(10.4f,10.49f)==SpiritXylemRules.Branch.None,"comparison handles nonfinite input and native integer rounding ties");
var localization=typeof(OfflineSpiritXylemLocalizationPatch).GetMethod("Prefix",BindingFlags.NonPublic|BindingFlags.Static);
foreach(var row in new[]{("Unique_Tooltip_0_445",SpiritXylemRules.AttunementText),("Unique_Tooltip_1_445",SpiritXylemRules.IntelligenceText)})
{
 object[] args2={row.Item1,"shipped old localized description"};bool original=(bool)localization.Invoke(null,args2);
 Check(!original && (string)args2[1]==row.Item2,"native localization cannot replace new conditional paragraph with old HP/MP-only text: "+row.Item1);
}
object[] otherKey={"Unique_Tooltip_444_0","unrelated"};Check((bool)localization.Invoke(null,otherKey) && (string)otherKey[1]=="unrelated","other unique localization remains native");
OfflineToolkit.Enabled=false;object[] onlineKey={"Unique_Tooltip_0_445","online"};Check((bool)localization.Invoke(null,onlineKey),"online localized unique description stays native");OfflineToolkit.Enabled=true;
Reset();stats.stats.Add(new(SP.AllAttributes,0,5,0,0,0,0));SpiritXylem.Apply(stats);Check(Value(SP.Health)==195 && Value(SP.Mana)==195,"All Attributes source contributes to final65 Attunement scaling");
Reset(30,50);stats.stats.Add(new(SP.AllAttributes,0,5,0,0,0,0));SpiritXylem.Apply(stats);Check(Value(SP.Damage)==32.5f,"All Attributes source contributes to final65 Intelligence spell scaling");
Check(stats.stats.Single(st=>st.property==SP.Damage).tags==AT.Spell,"flat spell damage uses the native weapon Spell Damage tag");
Check(!SpiritXylemRules.AttunementText.Contains(" to 4)") && !SpiritXylemRules.AttunementText.Contains(" to 20)") && !SpiritXylemRules.AttunementText.Contains(" to 6)") && !SpiritXylemRules.IntelligenceText.Contains(" to 4)"),"conditional tooltips show actual rolls without redundant literal range text");
Directory.CreateDirectory("validation");File.WriteAllText("validation/dev56-spirit-xylem-fixture.json",JsonSerializer.Serialize(new{passed=checks.Count,checks,actualSource=true,nativeGameplayValidated=false},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("PASS "+checks.Count+" actual-source Spirit Xylem checks");
