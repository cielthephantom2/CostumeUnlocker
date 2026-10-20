using HarmonyLib;

namespace LastEpochLAN.Mod;

// Only the private QA copy may alter its native boot mode. The normal game
// never receives this switch, and an unisolated copy fails before boot.
[HarmonyPatch(typeof(Il2Cpp.GameLoader),nameof(Il2Cpp.GameLoader.InitializeAsync))]
internal static class NativeQaNaturalBoot
{
    private static readonly bool Enabled=Environment.GetCommandLineArgs().Contains("--lelan-natural-shell-qa");
    private static void Prefix()
    {
        if(!Enabled)return;
        string root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),"UserData","GuestProfile"));
        if(!string.Equals(Il2CppLE.Core.PlatformFileManager.ApplicationSavePath,root,StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Natural QA boot requires verified private platform paths.");
        Il2CppLE.Core.CommandLine.Options.OfflineMode=true;
        UnityEngine.Application.targetFrameRate=20;
        UnityEngine.Application.runInBackground=true;
    }
}
