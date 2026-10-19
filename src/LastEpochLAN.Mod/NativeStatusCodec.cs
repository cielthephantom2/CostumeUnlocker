using System.Reflection;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes;
using LastEpochLAN.Core;

namespace LastEpochLAN.Mod;

// Restricted native-data serializer, never an arbitrary object serializer. Only
// these data families may be reconstructed; no MonoBehaviour, delegate or method
// invocation is accepted from the network. IDs resolve against native catalogues.
internal static class NativeStatusCodec
{
    internal readonly record struct Context(Actor Source,Actor Target,Actor Owner);
    private static readonly Dictionary<string,Type> types=new(StringComparer.Ordinal);
    private static readonly Dictionary<Type,PropertyInfo[]> properties=new();
    private static bool initialized;
    private static void Initialize()
    {
        if(initialized) return;
        foreach(var type in typeof(DamageStats).Assembly.GetTypes())
            if(type==typeof(DamageStats) || type==typeof(DamageStats.Penetration) || type==typeof(DamageStats.TrackerStatStorage) ||
                type==typeof(Stats.Stat) || type==typeof(AilmentApplication) || type==typeof(DamageConditionalEffect) ||
                type!=typeof(DamageConditional) && typeof(DamageConditional).IsAssignableFrom(type) ||
                type!=typeof(DamageEffect) && typeof(DamageEffect).IsAssignableFrom(type))
            {types[type.FullName]=type;types[type.FullName.Replace('+','/')]=type;}
        initialized=true;
    }
    private static PropertyInfo[] Fields(Type type)
    {
        if(properties.TryGetValue(type,out var result)) return result;
        result=type.GetProperties(BindingFlags.Instance|BindingFlags.Public)
            .Where(p=>p.CanRead && p.GetIndexParameters().Length==0 &&
                type.GetField("NativeFieldInfoPtr_"+p.Name,BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.FlattenHierarchy)!=null)
            .OrderBy(p=>p.Name,StringComparer.Ordinal).ToArray();
        properties[type]=result;return result;
    }
    private static object Actual(object value)
    {
        if(value is not Il2CppObjectBase native) return value;
        Initialize();
        var objectValue=new Il2CppSystem.Object(native.Pointer);
        string name="Il2Cpp."+objectValue.GetIl2CppType().FullName.Replace('+','/');
        if(types.TryGetValue(name,out var type) && type!=value.GetType())
            return Activator.CreateInstance(type,native.Pointer);
        return value;
    }
    internal static StatusValue Encode(object value,Context context,int depth=0)
    {
        if(depth>8) throw new InvalidOperationException("Status data nesting exceeds schema.");
        if(value==null || value is UnityEngine.Object unity && unity==null) return new();
        var type=value.GetType();
        if(type.IsEnum || value is IConvertible && value is not string)
            return new() {Kind="number",Number=Convert.ToDouble(value,System.Globalization.CultureInfo.InvariantCulture)};
        if(value is UnityEngine.Vector3 vector) return new() {Kind="vector",Items=new[]{Number(vector.x),Number(vector.y),Number(vector.z)}};
        if(value is Actor actor)
        {
            string kind=actor.Pointer==context.Source?.Pointer?"source":actor.Pointer==context.Target?.Pointer?"target":actor.Pointer==context.Owner?.Pointer?"owner":null;
            if(kind==null) throw new InvalidOperationException("Status references an unmapped Actor.");
            return new() {Kind=kind};
        }
        if(value is Ability ability)
        {
            if(!ability.TryGetAbilityID(out var id)) throw new InvalidOperationException("Status references a non-catalogue Ability: "+ability.name);
            return new() {Kind="ability",Number=(int)id};
        }
        if(value is Ailment ailment) return new() {Kind="ailment",Number=(int)ailment.id};
        string name=type.FullName??"";
        if(name.StartsWith("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray`",StringComparison.Ordinal) ||
            name.StartsWith("Il2CppSystem.Collections.Generic.List`",StringComparison.Ordinal))
        {
            int count=Convert.ToInt32((type.GetProperty("Count")??type.GetProperty("Length")).GetValue(value));
            if(count>1024) throw new InvalidOperationException("Status data list exceeds schema.");
            var item=type.GetProperty("Item");
            return new() {Kind="list",Items=Enumerable.Range(0,count).Select(i=>Encode(item.GetValue(value,new object[]{i}),context,depth+1)).ToArray()};
        }
        if(name.StartsWith("Il2CppSystem.Collections.Generic.Dictionary`",StringComparison.Ordinal))
        {
            var items=new List<StatusValue>();
            var enumerator=type.GetMethod("GetEnumerator",Type.EmptyTypes).Invoke(value,null);
            var enumeratorType=enumerator.GetType();
            var move=enumeratorType.GetMethod("MoveNext");var current=enumeratorType.GetProperty("Current");
            while((bool)move.Invoke(enumerator,null))
            {
                if(items.Count>=1024) throw new InvalidOperationException("Status tracker map exceeds schema.");
                var pair=current.GetValue(enumerator);var pairType=pair.GetType();
                items.Add(Encode(pairType.GetProperty("Key").GetValue(pair),context,depth+1));
                items.Add(Encode(pairType.GetProperty("Value").GetValue(pair),context,depth+1));
            }
            return new() {Kind="map",Items=items.ToArray()};
        }
        value=Actual(value);type=value.GetType();Initialize();
        if(!types.ContainsKey(type.FullName)) throw new InvalidOperationException("Unsupported native status data: "+type.FullName);
        return new() {Kind="object",Type=type.FullName,Fields=Fields(type).Select(p=>new StatusField(p.Name,Encode(p.GetValue(value),context,depth+1))).ToArray()};
    }
    internal static object Decode(StatusValue value,Type expected,Context context,int depth=0)
    {
        if(depth>8 || !value.IsValid()) throw new InvalidOperationException("Invalid native status data.");
        if(value.Kind=="null") return null;
        if(value.Kind=="number") return expected.IsEnum?Enum.ToObject(expected,checked((long)value.Number)):
            Convert.ChangeType(value.Number,expected,System.Globalization.CultureInfo.InvariantCulture);
        if(value.Kind=="vector")
        {if(expected!=typeof(UnityEngine.Vector3)) throw new InvalidOperationException("Status vector type mismatch.");return new UnityEngine.Vector3((float)value.Items[0].Number,(float)value.Items[1].Number,(float)value.Items[2].Number);}
        if(value.Kind=="source" || value.Kind=="target" || value.Kind=="owner")
        {
            if(!typeof(Actor).IsAssignableFrom(expected)) throw new InvalidOperationException("Status Actor type mismatch.");
            return value.Kind=="source"?context.Source:value.Kind=="target"?context.Target:context.Owner;
        }
        if(value.Kind=="ability")
        {if(expected!=typeof(Ability)) throw new InvalidOperationException("Status Ability type mismatch.");return Ability.getAbility((ushort)value.Number);}
        if(value.Kind=="ailment")
        {
            if(expected!=typeof(Ailment) || !AilmentList.TryGetAilment((AilmentID)(int)value.Number,out var ailment)) throw new InvalidOperationException("Unknown native ailment.");
            return ailment;
        }
        if(value.Kind=="list")
        {
            if(!expected.IsGenericType || !(expected.FullName.StartsWith("Il2CppSystem.Collections.Generic.List`",StringComparison.Ordinal) ||
                expected.FullName.StartsWith("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray`",StringComparison.Ordinal)))
                throw new InvalidOperationException("Native status list type mismatch.");
            var element=expected.GetGenericArguments()[0];
            bool array=expected.FullName.StartsWith("Il2CppInterop",StringComparison.Ordinal);
            object list=array?Activator.CreateInstance(expected,(long)value.Items.Length):Activator.CreateInstance(expected);
            var index=expected.GetProperty("Item");var add=expected.GetMethod("Add");
            for(int i=0;i<value.Items.Length;i++)
            {
                var item=Decode(value.Items[i],element,context,depth+1);
                if(array) index.SetValue(list,item,new object[]{i});else add.Invoke(list,new[]{item});
            }
            return list;
        }
        if(value.Kind=="map")
        {
            if(!expected.IsGenericType || !expected.FullName.StartsWith("Il2CppSystem.Collections.Generic.Dictionary`",StringComparison.Ordinal)) throw new InvalidOperationException("Native status map type mismatch.");
            var generic=expected.GetGenericArguments();var result=Activator.CreateInstance(expected);var add=expected.GetMethod("Add");
            for(int i=0;i<value.Items.Length;i+=2) add.Invoke(result,new[]{Decode(value.Items[i],generic[0],context,depth+1),Decode(value.Items[i+1],generic[1],context,depth+1)});
            return result;
        }
        Initialize();
        if(value.Kind!="object" || !types.TryGetValue(value.Type,out var actual) || !expected.IsAssignableFrom(actual)) throw new InvalidOperationException("Native status object type is not in the data schema.");
        object instance;
        if(actual==typeof(Stats.Stat)) instance=new Stats.Stat(SP.Damage,AT.None,0,0,new Il2CppSystem.Collections.Generic.List<float>(),0,0);
        else if(actual==typeof(DamageStats.Penetration)) instance=new DamageStats.Penetration(new float[7]);
        else if(actual.GetConstructor(Type.EmptyTypes)!=null) instance=Activator.CreateInstance(actual);
        else
        {
            // Data-only conditional subclasses sometimes have no zero-argument
            // constructor. Allocate their exact native class, then restore all
            // schema fields. No behaviour or network-chosen method is invoked.
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(actual.TypeHandle);
            var pointer=Il2CppInterop.Runtime.IL2CPP.il2cpp_object_new(Il2CppInterop.Runtime.Il2CppClassPointerStore.GetNativeClassPointer(actual));
            instance=Activator.CreateInstance(actual,pointer);
        }
        ApplyFields(instance,value,context,depth);
        return instance;
    }
    private static void ApplyFields(object instance,StatusValue value,Context context,int depth)
    {
        var allowed=Fields(instance.GetType()).ToDictionary(p=>p.Name,StringComparer.Ordinal);
        foreach(var field in value.Fields)
        {
            if(!allowed.TryGetValue(field.Name,out var property)) throw new InvalidOperationException("Unknown status data field: "+field.Name);
            var decoded=Decode(field.Value,property.PropertyType,context,depth+1);
            if(property.CanWrite) property.SetValue(instance,decoded);
            else
            {
                // Generated interop exposes some readonly native data fields
                // as getter-only properties (conditionals and tracker maps).
                // They still belong to the fixed schema; never omit them.
                var nativeField=instance.GetType().GetField("NativeFieldInfoPtr_"+property.Name,BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.FlattenHierarchy);
                if(instance is not Il2CppObjectBase native || nativeField==null)
                    throw new InvalidOperationException("Unsupported readonly native status field: "+property.Name);
                var fieldPointer=(IntPtr)nativeField.GetValue(null);
                if(decoded==null || decoded is Il2CppObjectBase)
                    Il2CppInterop.Runtime.IL2CPP.il2cpp_field_set_value_object(native.Pointer,fieldPointer,(decoded as Il2CppObjectBase)?.Pointer??IntPtr.Zero);
                else WriteValue(native.Pointer,fieldPointer,decoded);
            }
        }
    }
    private static unsafe void WriteValue(IntPtr instance,IntPtr field,object value)
    {
        var type=value.GetType();
        if(type.IsEnum) {type=Enum.GetUnderlyingType(type);value=Convert.ChangeType(value,type);}
        if(!type.IsPrimitive && type!=typeof(UnityEngine.Vector3)) throw new InvalidOperationException("Unsupported native status value field.");
        var buffer=System.Runtime.InteropServices.Marshal.AllocHGlobal(Math.Max(16,System.Runtime.InteropServices.Marshal.SizeOf(type)));
        try
        {
            System.Runtime.InteropServices.Marshal.StructureToPtr(value,buffer,false);
            Il2CppInterop.Runtime.IL2CPP.il2cpp_field_set_value(instance,field,buffer.ToPointer());
        }
        finally {System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer);}
    }
    internal static StatusStat ReadStat(Stats.Stat stat)=>new() {Property=(int)stat.property,Tags=(int)stat.tags,SpecialTag=stat.specialTag,
        ExtraTag=stat.extraTag,Added=stat.addedValue,Increased=stat.increasedValue,DontCollapse=stat.dontCollapse,More=stat.moreValues?.ToArray()??Array.Empty<float>()};
    internal static Stats.Stat MakeStat(StatusStat stat)=>new((SP)stat.Property,(AT)stat.Tags,stat.Added,stat.Increased,List(stat.More),stat.SpecialTag,stat.ExtraTag) {dontCollapse=stat.DontCollapse};
    private static Il2CppSystem.Collections.Generic.List<float> List(IEnumerable<float> values)
    {var list=new Il2CppSystem.Collections.Generic.List<float>();foreach(float value in values) list.Add(value);return list;}
    private static StatusValue Number(float value)=>new() {Kind="number",Number=value};

    private static readonly HashSet<string> overrideNames=new(StringComparer.Ordinal)
    {
        "moreDamage","overrideBlinds","mutationSetStats","overrideSpreads","addedSpreadRange","increasedSpreadRange",
        "additionalSpreadTargets","addedSpreadTargetsPerCurse","addedSpreadDelay","increasedSpreadDelay",
        "overrideSpreadIfDiedBeforeDelay","spreadIfDiedBeforeDelayIfOverridden","abilityToProcOnExpiration","offsetForExpirationAbility",
        "overrideDealsDamageWhenHit","stopsWhenHit","hitsRemainingBeforeStopping","requireSpecificActorHit","requiredActorToStop",
        "requiresSpecificStopHitEven","requiredHitEventToStop","moreDamageOnStopFromSpecificHitEvent",
        "expiresOnCreatorDeathOrDestruction","appliesAilmentsWhenHit","ailmentsWhenHit","procsAbilitiesOnDeath",
        "abilitiesToProcOnDeath","abilityToProcOnDeathChances","numberOfAbilitiesToProcOnDeath"
    };
    private static readonly PropertyInfo[] overrideFields=typeof(AilmentReceiver.ActiveAilment).GetProperties(BindingFlags.Instance|BindingFlags.Public)
        .Where(p=>overrideNames.Contains(p.Name) && p.CanRead && p.CanWrite).OrderBy(p=>p.Name,StringComparer.Ordinal).ToArray();
    internal static StatusValue Overrides(AilmentReceiver.ActiveAilment active,Context context)=>new()
    {Kind="object",Type="active-overrides",Fields=overrideFields.Select(p=>new StatusField(p.Name,Encode(p.GetValue(active),context))).ToArray()};
    internal static StatusValue ChangedOverrides(StatusValue before,StatusValue after)=>after with
    {Fields=after.Fields.Where(f=>!Same(f.Value,before.Fields.First(b=>b.Name==f.Name).Value)).ToArray()};
    private static bool Same(StatusValue a,StatusValue b)=>a.Kind==b.Kind && a.Type==b.Type && a.Number==b.Number &&
        a.Fields.Length==b.Fields.Length && a.Items.Length==b.Items.Length &&
        a.Fields.Select((f,i)=>f.Name==b.Fields[i].Name && Same(f.Value,b.Fields[i].Value)).All(v=>v) &&
        a.Items.Select((v,i)=>Same(v,b.Items[i])).All(v=>v);
    internal static void ApplyOverrides(AilmentReceiver.ActiveAilment active,StatusValue overrides,Context context)
    {
        if(overrides==null) return;
        if(!overrides.IsValid() || overrides.Kind!="object" || overrides.Type!="active-overrides") throw new InvalidOperationException("Invalid active ailment override schema.");
        foreach(var field in overrides.Fields)
        {
            var property=overrideFields.FirstOrDefault(p=>p.Name==field.Name) ?? throw new InvalidOperationException("Unknown ailment override: "+field.Name);
            property.SetValue(active,Decode(field.Value,property.PropertyType,context));
        }
    }
    internal static DamageStats Damage(StatusValue value,Context context)=>value==null?null:(DamageStats)Decode(value,typeof(DamageStats),context);
}
