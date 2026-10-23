namespace HarmonyLib { public class HarmonyPatch:Attribute{public HarmonyPatch(params object[] a){}} }
namespace UnityEngine {}
namespace Il2CppSystem.Collections.Generic {public class List<T>:System.Collections.Generic.List<T>{} }
namespace Il2Cpp {
 public static class Localization{public static string GetText(string key,string fallback)=>fallback;}
 public class NativeObject{private static int next;public IntPtr Pointer{get;}=(IntPtr)(++next);public T TryCast<T>()where T:class=>this as T;}
 public enum SP{AllAttributes=46,Damage=0,Health=7,Mana=8,Intelligence=21,Attunement=23,AdaptiveSpellDamage=41,AbilityProperty=58,ManaCost=66,LevelOfSkills=88,PlayerProperty=98,GlobalConditionalDamage=117}
 public enum AT{None=0,Spell=256}
 public static class Tags{public enum SpecificityType{TagsIntelligence=9,TagsAttunement=11}}
 public class Stats:NativeObject{
  public Il2CppSystem.Collections.Generic.List<Stat> stats=new();
  public class Stat:NativeObject{public SP property;public AT tags;public float addedValue,increasedValue;public byte specialTag;public int extraTag;public bool dontCollapse;public float more;
   public Stat(SP p,AT t,float a,float inc,float m,byte st,int extra){property=p;tags=t;addedValue=a;increasedValue=inc;more=m;specialTag=st;extraTag=extra;}}
 }
 public class BaseStats:Stats{public Actor actor;public enum ModType{ADDED,INCREASED,MORE}public int Requests;public void UpdateStatsInternal(){}public void UpdateStatsNextFrame(){Requests++;}}
 public class CharacterStats:BaseStats{public void ApplyCoreAttributeModifiers(){}}
 public class Actor:NativeObject{public ItemContainersManager itemContainersManager=new();}
 public static class PlayerFinder{public static Actor Player;public static Actor getPlayerActor()=>Player;}
 public class ItemContainersManager{public ItemEquipManager itemEquipManager=new();}
 public class ItemEquipManager{public Il2CppSystem.Collections.Generic.List<EquippedItem> equippedItems=new();public class EquippedItem{public bool StatsApplied=true;public ItemData Item;public float globalStatModifier;}}
 public class ItemData:NativeObject{public ushort uniqueID;public byte[] uniqueRolls=new byte[8];public byte legendaryPotential=3;public readonly byte[] id={1,2,3,4};public readonly object affixes=new();}
 public class UniqueItemMod:NativeObject{public SP property;public AT tags;public byte specialTag,rollID;public int extraTag;public BaseStats.ModType type;public float value,maxValue;public bool canRoll,hideInTooltip;
  public float getValue(byte roll){float result=value+(maxValue-value)*roll/255f;return property is SP.LevelOfSkills or SP.Attunement?(float)Math.Round(result):result;}}
 public class ItemTooltipDescription{public string description,altText;}
 public class UniqueModDisplayListEntry{public byte modDisplay;public UniqueModDisplayListEntry(byte value){modDisplay=value;}}
 public class UniqueList:NativeObject{public static UniqueList instance=new();public Il2CppSystem.Collections.Generic.List<Entry> uniques=new();public class Entry:NativeObject{public ushort uniqueID;public string name;public byte baseType=15;public byte subType=8;public int levelRequirement=50;public bool isPrimordialItem=true;public Il2CppSystem.Collections.Generic.List<UniqueItemMod> mods=new();public Il2CppSystem.Collections.Generic.List<ItemTooltipDescription> tooltipDescriptions=new();public Il2CppSystem.Collections.Generic.List<UniqueModDisplayListEntry> tooltipEntries=new();}public static Entry getUnique(ushort id)=>instance.uniques.FirstOrDefault(e=>e.uniqueID==id);public static Il2CppSystem.Collections.Generic.List<UniqueItemMod> getMods(ushort id)=>getUnique(id)?.mods;}
}
namespace LastEpochLAN.Mod.OfflineTools{public static class OfflineToolkit{public static bool Enabled=true;}}
namespace MelonLoader{public static class MelonLogger{public static List<string> Messages=new();public static void Warning(string message)=>Messages.Add(message);}}
