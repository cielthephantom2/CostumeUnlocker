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
Reset();var originalMods=definition.mods;var originalDescriptions=definition.tooltipDescriptions;var originalEntries=definition.tooltipEntries;var damage=originalMods[5];var defense=originalMods[4];SpiritXylem.Configure(definition);
Check(definition.mods.Count==2 && definition.mods[0]==damage && definition.mods[1]==defense,"retain exact native boss/rare damage and defense objects; remove reduced health cost");
Check(!definition.mods.Any(m=>m.property==SP.AbilityProperty && m.tags==(AT)253 && m.specialTag==2),"reduced spell health cost is absent from equipped native mods");
Check(damage.type==BaseStats.ModType.MORE && defense.value==-.2f && defense.maxValue==-.1f,"native mana threshold damage/defense rolls unchanged");
Check(definition.tooltipEntries.Select(e=>e.modDisplay).SequenceEqual(new byte[]{128,0,1}) && definition.tooltipDescriptions.Count==1,"tooltip has one unconditional effects block followed by two preserved native modifiers");
Check(definition.tooltipDescriptions[0].description==SpiritXylemRules.EffectsText && !SpiritXylemRules.EffectsText.Contains("If ") && SpiritXylemRules.EffectsText.Contains("All Skills") && SpiritXylemRules.EffectsText.Contains("+1 Spell Damage per Intelligence"),"five requested effects replace both old conditional branches");
SpiritXylem.Configure(definition);Check(definition.mods.Count==2 && definition.tooltipDescriptions.Count==1,"registry configuration is idempotent");
SpiritXylem.Clear();Check(ReferenceEquals(definition.mods,originalMods) && ReferenceEquals(definition.tooltipDescriptions,originalDescriptions) && ReferenceEquals(definition.tooltipEntries,originalEntries),"cleanup restores exact original registry references");
Reset();var bytes=item.id.ToArray();var rolls=item.uniqueRolls.ToArray();var affixes=item.affixes;SpiritXylem.Apply(stats);
Check(Value(SP.Intelligence)==40 && Value(SP.Attunement)==40,"INT30 plus min10 INT gives40; ATT50 is set down to final INT40");
Check(Level(0)==3 && !stats.stats.Any(s=>s.property==SP.LevelOfSkills && s.specialTag!=0),"min3 global skill levels use no attribute filter");
Check(Value(SP.Health)==120 && Value(SP.Mana)==120 && Value(SP.Damage)==40,"min3 HP/MP per final40 ATT plus1 spell damage per40 INT apply together");
Check(item.id.SequenceEqual(bytes) && item.uniqueRolls.SequenceEqual(rolls) && item.legendaryPotential==3 && ReferenceEquals(affixes,item.affixes),"item identity rolls LP and legendary affixes remain unchanged");
for(int n=0;n<100;n++)SpiritXylem.Apply(stats);
Check(Value(SP.Intelligence)==40 && Value(SP.Attunement)==40 && Level(0)==3 && Value(SP.Health)==120 && Value(SP.Damage)==40,"100 recalculations never stack equality, attribute, health, mana or damage");
stats.stats[0].addedValue=200;stats.stats[1].addedValue=65;SpiritXylem.Apply(stats);
Check(Value(SP.Attunement)==75 && Value(SP.Intelligence)==75 && Value(SP.Health)==225 && Value(SP.Damage)==75,"changing base INT/ATT refreshes equality and every scaling effect");
foreach(var (att,intel) in new[]{(30f,50f),(50f,30f),(30f,30f)}){
 Reset(att,intel,255);SpiritXylem.Apply(stats);float expected=intel+20;
 Check(Value(SP.Intelligence)==expected && Value(SP.Attunement)==expected && Level(0)==4 && Value(SP.Health)==6*expected && Value(SP.Mana)==6*expected && Value(SP.Damage)==expected,"max rolls all apply regardless of old comparison: "+att+"/"+intel);
}
Reset();actor.itemContainersManager.itemEquipManager.equippedItems[0].StatsApplied=false;SpiritXylem.Apply(stats);Check(stats.stats.Count==2,"StatsApplied=false equipment grants no effects");
Reset();item.uniqueID=444;SpiritXylem.Apply(stats);Check(stats.stats.Count==2,"other uniques unaffected");
Reset();actor.itemContainersManager.itemEquipManager.equippedItems.Clear();SpiritXylem.Apply(stats);Check(stats.stats.Count==2,"inventory or stash copies grant no effects");
Reset();SpiritXylem.Apply(stats);actor.itemContainersManager.itemEquipManager.equippedItems.Clear();SpiritXylem.Apply(stats);Check(stats.stats.Count==2 && Value(SP.Attunement)==50 && Value(SP.Intelligence)==30 && Value(SP.Health)==0 && Value(SP.Damage)==0,"unequip restores original ATT/INT and removes all five effects");
Reset();SpiritXylem.Apply(stats);var old=stats;actor=PlayerFinder.Player=new();stats=new(){actor=actor};stats.stats.Add(new(SP.Intelligence,0,70,0,0,0,0));actor.itemContainersManager.itemEquipManager.equippedItems.Add(new(){Item=item});SpiritXylem.Apply(stats);Check(old.stats.Count==2 && old.Requests>0 && Value(SP.Intelligence)==80 && Value(SP.Attunement)==80,"actor replacement removes old source and applies equality on the new actor");
Reset();PlayerFinder.Player=new();SpiritXylem.Apply(stats);Check(stats.stats.Count==2,"remote actor never receives local item effects");
Reset();SpiritXylem.Apply(stats);OfflineToolkit.Enabled=false;SpiritXylem.Tick();Check(stats.stats.Count==2 && definition.mods.Count==8,"online transition restores native registry and all attribute sources");
Reset();SpiritXylem.Apply(stats);SpiritXylem.Clear();Check(stats.stats.Count==2 && definition.mods.Count==8,"scene cleanup restores native assets and stats");
Reset();actor.itemContainersManager.itemEquipManager.equippedItems[0].globalStatModifier=.5f;SpiritXylem.Apply(stats);Check(Value(SP.Intelligence)==45 && Value(SP.Attunement)==45 && Value(SP.Health)==202.5f && Value(SP.Mana)==202.5f && Value(SP.Damage)==67.5f && Level(0)==4.5f,"increased item effect scales bonuses once while equality remains exact");
Reset();item.uniqueRolls=Array.Empty<byte>();typeof(OfflineSpiritXylemConditionPatch).GetMethod("Prefix",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{stats});Check(stats.stats.Count==2 && MelonLoader.MelonLogger.Messages.Count>0,"malformed rolls fail without leaving partial attribute or skill effects");
Reset();stats.stats.Add(new(SP.AllAttributes,0,5,0,0,0,0));SpiritXylem.Apply(stats);Check(Value(SP.Attunement)+Value(SP.AllAttributes)==45 && Value(SP.Intelligence)+Value(SP.AllAttributes)==45 && Value(SP.Health)==135 && Value(SP.Mana)==135 && Value(SP.Damage)==45,"All Attributes contributes to final INT and ATT equality and HP/MP/spell scaling");
Reset();stats.stats[0].addedValue=float.NaN;SpiritXylem.Apply(stats);Check(stats.stats.Count==2,"nonfinite attributes do not publish invalid stats");
Reset(40,30);SpiritXylem.Apply(stats);Check(Value(SP.Attunement)==40 && Value(SP.Intelligence)==40 && Value(SP.Health)==120,"already-equal final attributes retain all other effects");
var localization=typeof(OfflineSpiritXylemLocalizationPatch).GetMethod("Prefix",BindingFlags.NonPublic|BindingFlags.Static);
foreach(var row in new[]{("Unique_Tooltip_0_445",SpiritXylemRules.EffectsText),("Unique_Tooltip_1_445",""),("Unique_Tooltip_0_445_Alt",SpiritXylemRules.DetailsText)}){
 object[] values={row.Item1,"old condition"};Check(!(bool)localization.Invoke(null,values) && (string)values[1]==row.Item2,"native localization removes old branches for "+row.Item1);
}
object[] other={"Unique_Tooltip_444_0","unrelated"};Check((bool)localization.Invoke(null,other) && (string)other[1]=="unrelated","unrelated localization remains native");
OfflineToolkit.Enabled=false;object[] online={"Unique_Tooltip_0_445","online"};Check((bool)localization.Invoke(null,online),"online tooltip remains native");OfflineToolkit.Enabled=true;
Directory.CreateDirectory("validation");File.WriteAllText("validation/spirit-rework-fixture.json",JsonSerializer.Serialize(new{passed=checks.Count,checks,actualSource=true,nativeGameplayValidated=false},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("PASS "+checks.Count+" actual-source Spirit Xylem rework checks");
