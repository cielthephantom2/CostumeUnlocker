using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;

namespace LastEpochLAN.Mod;

internal static class NativeNullable
{
    private static readonly Dictionary<(IntPtr Class, string Name), IntPtr> fields = new();
    internal static long EmptyFieldReads, ValueFieldReads, GetterFallbacks;
    internal static T? ReadField<T>(Il2CppSystem.Object owner, string name, Func<Il2CppSystem.Nullable<T>> fallback) where T : struct
    {
        if (owner == null) return null;
        var klass = IL2CPP.il2cpp_object_get_class(owner.Pointer);
        var key = (klass, name);
        if (!fields.TryGetValue(key, out var field))
        {
            for (var current = klass; current != IntPtr.Zero && field == IntPtr.Zero; current = IL2CPP.il2cpp_class_get_parent(current))
                field = IL2CPP.il2cpp_class_get_field_from_name(current, name);
            fields[key] = field;
        }
        if (field == IntPtr.Zero) { GetterFallbacks++; return Read(fallback); }
        // Ask IL2CPP to box the field directly. Empty Nullable becomes null;
        // avoid the generated wrapper constructing a GC handle for null and
        // throwing a managed exception on every absent animation/weapon field.
        var pointer = IL2CPP.il2cpp_field_get_value_object(field, owner.Pointer);
        if (pointer == IntPtr.Zero) { EmptyFieldReads++; return null; }
        var box = new Il2CppSystem.Object(pointer);
        var result = ReadBox<T>(box.Pointer); GC.KeepAlive(box); ValueFieldReads++; return result;
    }
    private static T? ReadBox<T>(IntPtr box) where T : struct
    {
        var pointer = IL2CPP.il2cpp_object_unbox(box);
        if (pointer == IntPtr.Zero) return null;
        if (typeof(T) == typeof(bool)) return (T)(object)(Marshal.ReadByte(pointer) != 0);
        if (typeof(T) == typeof(byte)) return (T)(object)Marshal.ReadByte(pointer);
        if (typeof(T).IsEnum && Enum.GetUnderlyingType(typeof(T)) == typeof(byte))
            return (T)Enum.ToObject(typeof(T), Marshal.ReadByte(pointer));
        return Marshal.PtrToStructure<T>(pointer);
    }
    // IL2CPP boxes Nullable<T> as T, or null. The generated Nullable wrapper's
    // HasValue/Value offsets cannot be used on that boxed T object.
    internal static T? Read<T>(Func<Il2CppSystem.Nullable<T>> getter) where T : struct
    {
        try
        {
            var box = getter();
            if (box == null) return null;
            var result = ReadBox<T>(box.Pointer); GC.KeepAlive(box); return result;
        }
        catch (NullReferenceException) { return null; }
    }
}
