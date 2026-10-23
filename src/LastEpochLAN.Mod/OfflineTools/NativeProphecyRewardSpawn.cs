using System.Reflection;
using System.Runtime.InteropServices;
using Il2Cpp;
using Il2CppLE.Factions;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.Runtime;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.MethodInfo;
using Il2CppInterop.Runtime.Startup;
using Lens=Il2CppSystem.Nullable<Il2CppLE.Factions.ProphecySlot.LensType>;

namespace LastEpochLAN.Mod.OfflineTools;

// Nullable<enum> occupies an eight-byte register in this pinned Win64 binary.
// Harmony treats it as a pointer and dereferences the register contents when
// called by native code. Use the same checked ABI approach as loot-filter Match.
internal static unsafe class NativeProphecyRewardSpawn
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void Spawn(IntPtr reward,IntPtr player,ulong lens,IntPtr method);
    private static readonly Spawn callback=Receive;
    private static readonly ManualResetEventSlim ready=new(false);
    private static IDetour detour;
    private static INativeMethodInfoStruct original;
    private static int unityThread;
    internal static string Error {get;private set;}="";
    internal static bool EnsureInstalled()
    {
        if(detour!=null)return true;
        if(Error.Length>0)return false;
        IDetour candidate=null;
        try
        {
            if(!Environment.Is64BitProcess)throw new PlatformNotSupportedException("Prophecy requires verified Win64 ABI");
            _=new Lens();uint alignment=0;
            var klass=Il2CppClassPointerStore<Lens>.NativeClassPtr;
            int size=IL2CPP.il2cpp_class_value_size(klass,ref alignment);
            var has=IL2CPP.il2cpp_class_get_field_from_name(klass,"hasValue");
            var value=IL2CPP.il2cpp_class_get_field_from_name(klass,"value");
            var box=new Lens();long header=IL2CPP.il2cpp_object_unbox(box.Pointer).ToInt64()-box.Pointer.ToInt64();
            long Offset(IntPtr field){long n=IL2CPP.il2cpp_field_get_offset(field);return n>=header?n-header:n;}
            if(size!=8 || has==IntPtr.Zero || value==IntPtr.Zero || Offset(has)!=0 || Offset(value)!=4)
                throw new PlatformNotSupportedException("Unexpected native Prophecy lens layout");
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(ProphecySlotReward).TypeHandle);
            var infoPtr=(IntPtr)typeof(ProphecySlotReward).GetFields(BindingFlags.NonPublic|BindingFlags.Static)
                .Single(f=>f.Name.StartsWith("NativeMethodInfoPtr_SpawnRewardForPlayer_",StringComparison.Ordinal)).GetValue(null);
            var info=UnityVersionHandler.Wrap((Il2CppMethodInfo*)infoPtr);
            original=UnityVersionHandler.NewMethod();
            Buffer.MemoryCopy(info.Pointer.ToPointer(),original.Pointer.ToPointer(),UnityVersionHandler.MethodSize(),UnityVersionHandler.MethodSize());
            candidate=Il2CppInteropRuntime.Instance.DetourProvider.Create(info.MethodPointer,callback);
            if(candidate.OriginalTrampoline==IntPtr.Zero)throw new InvalidOperationException("Prophecy trampoline absent");
            original.MethodPointer=candidate.OriginalTrampoline;
            unityThread=Environment.CurrentManagedThreadId;candidate.Apply();detour=candidate;
            return true;
        }
        catch(Exception ex){Error=ex.ToString();candidate?.Dispose();MelonLoader.MelonLogger.Error("Prophecy native hook disabled: "+Error);return false;}
        finally{ready.Set();}
    }
    private static void Receive(IntPtr reward,IntPtr player,ulong rawLens,IntPtr method)
    {
        ready.Wait();
        try
        {
            if(Environment.CurrentManagedThreadId==unityThread && reward!=IntPtr.Zero && player!=IntPtr.Zero)
            {
                var lens=(rawLens & 255)!=0 ? new Lens((ProphecySlot.LensType)(int)(rawLens>>32)) : new Lens();
                if(ProphecyRewards.Defer(new ProphecySlotReward(reward),new Actor(player),lens))return;
            }
            InvokeOriginal(reward,player,rawLens);
        }
        catch(Exception ex)
        {
            // Never unwind a CLR exception through an unmanaged callback or
            // retry a potentially partial native spawn.
            Error=ex.ToString();MelonLoader.MelonLogger.Error("Prophecy native delivery failed, not retried: "+Error);
        }
    }
    private static void InvokeOriginal(IntPtr reward,IntPtr player,ulong lens)
    {
        void** args=stackalloc void*[2];args[0]=(void*)player;args[1]=&lens;
        IntPtr exception=IntPtr.Zero;
        IL2CPP.il2cpp_runtime_invoke(original.Pointer,reward,args,ref exception);
        Il2CppInterop.Runtime.Il2CppException.RaiseExceptionIfNecessary(exception);
    }
    internal static void Deliver(ProphecySlotReward reward,Actor player,Lens lens)
    {
        ulong raw=lens.HasValue ? 1ul|((ulong)(uint)(int)lens.Value<<32) : 0;
        InvokeOriginal(reward.Pointer,player.Pointer,raw);
    }
}
