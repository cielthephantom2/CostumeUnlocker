using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppItemFiltering;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.Runtime;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.MethodInfo;
using Il2CppInterop.Runtime.Startup;
using UnityEngine;

namespace LastEpochLAN.Mod;

// Match has native by-ref value types. Never send this signature through the
// generated managed wrapper or Harmony's by-ref object-pointer bridge.
internal static unsafe class NativeLootFilterMatch
{
    private sealed record NullableLayout(int Size, int HasValue, int Value);
    private static NullableLayout intLayout, boolLayout;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int MatchFunction(IntPtr owner, IntPtr item, void* color, void* emphasis,
        int* rule, int* sound, int* mapIcon, byte* beamOverride, int* beamSize, Color* beamColor, IntPtr method);

    private static readonly MatchFunction callback = Match;
    private static readonly Lazy<bool> layout = new(ValidateLayout);
    private static readonly Lazy<IntPtr> methodInfo = new(ResolveMethodInfo);
    private static IDetour detour;
    private static INativeMethodInfoStruct originalInfo;
    private static readonly ManualResetEventSlim installationReady = new(false);
    private static int unityThread;
    internal static string LastError { get; private set; } = "";
    internal static string LayoutSummary => $"int={intLayout}; bool={boolLayout}";

    internal static IntPtr MethodInfo => methodInfo.Value;
    // Detached filter evaluations bypass the room detour entirely. Native
    // failures then propagate to the specific owner's managed ingestion path.
    internal static IntPtr EvaluationMethodInfo => detour != null ? originalInfo.Pointer : MethodInfo;

    private static IntPtr ResolveMethodInfo()
    {
        // Force the generated type initializer before reading its MethodInfo field.
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(ItemFilter).TypeHandle);
        return (IntPtr)typeof(ItemFilter).GetFields(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(f => f.Name.StartsWith("NativeMethodInfoPtr_Match_", StringComparison.Ordinal)).GetValue(null);
    }

    internal static void RequireLayout() { _ = layout.Value; }

    internal static bool EnsureInstalled()
    {
        if (detour != null) return true;
        if (LastError != "") return false;
        IDetour candidate = null;
        IntPtr target = IntPtr.Zero;
        try
        {
            RequireLayout();
            var info = UnityVersionHandler.Wrap((Il2CppMethodInfo*)MethodInfo);
            if (info.MethodPointer == IntPtr.Zero) throw new InvalidOperationException("Native Match pointer is absent.");
            target = info.MethodPointer;
            // Preserve native metadata so the original can be called through
            // runtime_invoke, which captures native exceptions before they cross
            // managed frames. Its MethodPointer must bypass our detour.
            originalInfo = UnityVersionHandler.NewMethod();
            Buffer.MemoryCopy(info.Pointer.ToPointer(), originalInfo.Pointer.ToPointer(), UnityVersionHandler.MethodSize(), UnityVersionHandler.MethodSize());
            candidate = Il2CppInteropRuntime.Instance.DetourProvider.Create(info.MethodPointer, callback);
            if (candidate.OriginalTrampoline == IntPtr.Zero) throw new InvalidOperationException("Native Match trampoline is absent.");
            originalInfo.MethodPointer = candidate.OriginalTrampoline;
            unityThread = Environment.CurrentManagedThreadId;
            candidate.Apply();
            detour = candidate;
            // Retain the detour/delegates for the process lifetime. Removing a
            // trampoline during a native call risks freeing executable memory.
            // Outside an active room this callback only invokes the original.
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.GetType().Name + ": " + ex.Message;
            try { candidate?.Dispose(); }
            finally { if (originalInfo != null && target != IntPtr.Zero) originalInfo.MethodPointer = target; }
            return false;
        }
        finally { installationReady.Set(); }
    }

    private static int Match(IntPtr owner, IntPtr item, void* color, void* emphasis,
        int* rule, int* sound, int* mapIcon, byte* beamOverride, int* beamSize, Color* beamColor, IntPtr method)
    {
        // Execute native exactly once, preserving every native output unless a
        // complete remote verdict has been evaluated successfully.
        // The configured provider attaches during Create. A concurrent caller
        // must wait until its original trampoline has been assigned.
        installationReady.Wait();
        int result;
        try
        {
            if (originalInfo == null) throw new InvalidOperationException("Original native Match metadata is absent.");
            void** args = stackalloc void*[9];
            args[0] = (void*)item; args[1] = color; args[2] = emphasis;
            args[3] = rule; args[4] = sound; args[5] = mapIcon;
            args[6] = beamOverride; args[7] = beamSize; args[8] = beamColor;
            IntPtr exception = IntPtr.Zero;
            var boxed = IL2CPP.il2cpp_runtime_invoke(originalInfo.Pointer, owner, args, ref exception);
            Il2CppInterop.Runtime.Il2CppException.RaiseExceptionIfNecessary(exception);
            if (boxed == IntPtr.Zero) throw new InvalidOperationException("Original native Match returned no verdict.");
            result = *(int*)IL2CPP.il2cpp_object_unbox(boxed);
        }
        catch (Exception ex)
        {
            LastError = ex.GetType().Name + ": " + ex.Message;
            ClearOutputs(color, emphasis, rule, sound, mapIcon, beamOverride, beamSize, beamColor);
            return (int)Rule.RuleOutcome.SHOW; // Filtering failure must not make an item inaccessible.
        }
        if (Environment.CurrentManagedThreadId != unityThread || result != (int)Rule.RuleOutcome.HIDE ||
            color == null || emphasis == null || rule == null || sound == null || mapIcon == null ||
            beamOverride == null || beamSize == null || beamColor == null) return result;
        try
        {
            var match = LanLootFilters.SelectRemoteVerdict(owner, item);
            if (match == null) return result;
            WriteNullable(color, intLayout, match.HasColor, match.Color);
            WriteNullable(emphasis, boolLayout, match.HasEmphasis, match.Emphasis ? 1 : 0);
            *rule = -1; *sound = match.Sound; *mapIcon = match.MapIcon;
            *beamOverride = (byte)(match.BeamOverride ? 1 : 0); *beamSize = (int)match.BeamSize; *beamColor = match.BeamColor;
            return (int)match.Outcome;
        }
        catch (Exception ex)
        {
            LastError = ex.GetType().Name + ": " + ex.Message;
            return result;
        }
    }

    private static void ClearOutputs(void* color, void* emphasis, int* rule, int* sound,
        int* mapIcon, byte* beamOverride, int* beamSize, Color* beamColor)
    {
        if (color != null) new Span<byte>(color, intLayout.Size).Clear();
        if (emphasis != null) new Span<byte>(emphasis, boolLayout.Size).Clear();
        if (rule != null) *rule = -1;
        if (sound != null) *sound = 0;
        if (mapIcon != null) *mapIcon = 0;
        if (beamOverride != null) *beamOverride = 0;
        if (beamSize != null) *beamSize = 0;
        if (beamColor != null) *beamColor = default;
    }

    private static bool ValidateLayout()
    {
        if (!Environment.Is64BitProcess)
            throw new PlatformNotSupportedException("Unexpected native nullable ABI.");
        intLayout = ReadLayout<int>(8, 4);
        boolLayout = ReadLayout<bool>(2, 1);
        return true;
    }

    private static NullableLayout ReadLayout<T>(int expectedSize, int valueSize) where T : struct
    {
        var box = new Il2CppSystem.Nullable<T>();
        // Nullable wrappers can expose boxed T rather than boxed Nullable<T>.
        // Read the declared generic class metadata, never the boxed object's class.
        IntPtr klass = Il2CppClassPointerStore<Il2CppSystem.Nullable<T>>.NativeClassPtr;
        uint alignment = 0;
        int size = IL2CPP.il2cpp_class_value_size(klass, ref alignment);
        IntPtr hasValue = IL2CPP.il2cpp_class_get_field_from_name(klass, "hasValue");
        IntPtr value = IL2CPP.il2cpp_class_get_field_from_name(klass, "value");
        long header = IL2CPP.il2cpp_object_unbox(box.Pointer).ToInt64() - box.Pointer.ToInt64();
        int Offset(IntPtr field)
        {
            if (field == IntPtr.Zero) return -1;
            long offset = IL2CPP.il2cpp_field_get_offset(field);
            return checked((int)(offset >= header && offset < header + size ? offset - header : offset));
        }
        int hasOffset = Offset(hasValue), valueOffset = Offset(value);
        if (size != expectedSize || hasOffset < 0 || hasOffset >= size || valueOffset < 0 ||
            valueOffset + valueSize > size || hasOffset >= valueOffset && hasOffset < valueOffset + valueSize ||
            valueSize == 4 && valueOffset % 4 != 0)
            throw new PlatformNotSupportedException($"Unsupported Nullable<{typeof(T).Name}> size={size}, has={hasOffset}, value={valueOffset}, header={header}.");
        GC.KeepAlive(box);
        return new(size, hasOffset, valueOffset);
    }

    private static void WriteNullable(void* output, NullableLayout metadata, bool hasValue, int value)
    {
        new Span<byte>(output, metadata.Size).Clear();
        *((byte*)output + metadata.HasValue) = (byte)(hasValue ? 1 : 0);
        if (metadata.Size == 8) *(int*)((byte*)output + metadata.Value) = value;
        else *((byte*)output + metadata.Value) = (byte)value;
    }

    internal static void ReadNullableInt(void* source, out bool hasValue, out int value)
    { hasValue = *((byte*)source + intLayout.HasValue) != 0; value = *(int*)((byte*)source + intLayout.Value); }
    internal static void ReadNullableBool(void* source, out bool hasValue, out bool value)
    { hasValue = *((byte*)source + boolLayout.HasValue) != 0; value = *((byte*)source + boolLayout.Value) != 0; }
}
