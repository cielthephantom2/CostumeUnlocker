using System.Text.Json;
using HarmonyLib;
using MelonLoader;
using Il2CppLE.Core;

[assembly: MelonInfo(typeof(LastEpochLAN.GuestProfile.ProfileMod), "LAN Local Guest Profile", "0.1.0", "Local testing")]
[assembly: MelonGame("Eleventh Hour Games", "Last Epoch")]
[assembly: MelonPriority(-10000)]

namespace LastEpochLAN.GuestProfile;

public sealed class ProfileMod : MelonMod
{
    internal static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, "UserData", "GuestProfile"));
    private bool verified;
    internal static long BlockedWrites;
    public override void OnInitializeMelon()
    {
        Directory.CreateDirectory(Root);
        // This helper exists only in disposable QA copies. Set the supported
        // native option before GameClientLauncher.Start reads the cached parser
        // result; the extra QA/Unity switches can otherwise invalidate parsing.
        CommandLine.Options.OfflineMode=true;
        LoggerInstance.Msg("Guest platform save/data paths redirected to " + Root);
    }

    public override void OnUpdate()
    {
        if (verified) return;
        verified = true;
        string reportPath = Path.Combine(Path.GetDirectoryName(Root)!, "LastEpochLAN.guest-profile-report.json");
        try
        {
            var win = new PlatformFileManagerWin();
            string save = win.GetApplicationSavePath(), data = win.GetApplicationDataPath();
            string nativeSave = PlatformFileManager.ApplicationSavePath;
            string nativeData = Paths.GetDataPath();
            if (save != Root || data != Root || nativeSave != Root || !Inside(nativeData))
                throw new InvalidOperationException("Native profile path verification failed: " + nativeData);
            Directory.CreateDirectory(nativeData);
            string probe = Path.Combine(nativeData, "lelan-profile-sentinel.txt");
            PlatformFileManager.WriteAllText(probe, "LAN guest native save isolation probe");
            if (!File.Exists(probe) || File.ReadAllText(probe) != "LAN guest native save isolation probe")
                throw new InvalidOperationException("Native profile write verification failed.");
            string deniedProbe = Path.Combine(Path.GetDirectoryName(Root)!, "profile-outside-" + Guid.NewGuid().ToString("N") + ".txt");
            long blockedBefore = BlockedWrites;
            PlatformFileManager.WriteAllText(deniedProbe, "This write must be blocked.");
            if (File.Exists(deniedProbe) || BlockedWrites <= blockedBefore)
                throw new InvalidOperationException("Native writes outside the guest profile were not blocked.");
            File.WriteAllText(reportPath, JsonSerializer.Serialize(new { passed = true, utc = DateTimeOffset.UtcNow,
                root = Root, nativeSave, nativeData, probe, outsideWriteBlocked = true,
                scope = "Native Windows platform paths and save-file write; campaign character/progression not yet tested" },
                new JsonSerializerOptions { WriteIndented = true }));
            LoggerInstance.Msg("PASS: native guest save paths and native file write are isolated.");
        }
        catch (Exception ex)
        {
            File.WriteAllText(reportPath, JsonSerializer.Serialize(new { passed = false, error = ex.ToString(), root = Root }));
            LoggerInstance.Error("Guest save isolation not verified; do not enter campaign: " + ex);
        }
    }

    internal static bool Inside(string path)
    {
        string absolute = Path.GetFullPath(path);
        return absolute.Equals(Root, StringComparison.OrdinalIgnoreCase) || absolute.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}

[HarmonyPatch(typeof(IPlatformFileManager), nameof(IPlatformFileManager.PlatformWriteSaveDataFile))]
internal static class SaveWriteBoundaryPatch
{
    private static bool Prefix(string path)
    {
        if (ProfileMod.Inside(path)) return true;
        Interlocked.Increment(ref ProfileMod.BlockedWrites);
        MelonLogger.Warning("Guest profile blocked a native save write outside its private directory.");
        return false;
    }
}
[HarmonyPatch(typeof(IPlatformFileManager), nameof(IPlatformFileManager.PlatformDeleteSaveDataFile))]
internal static class SaveDeleteBoundaryPatch
{
    private static bool Prefix(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray paths) => paths.All(ProfileMod.Inside);
}
[HarmonyPatch(typeof(IPlatformFileManager), nameof(IPlatformFileManager.PlatformDeleteSaveDataDirectory))]
internal static class SaveDirectoryDeleteBoundaryPatch
{
    private static bool Prefix(string dir) => ProfileMod.Inside(dir);
}

[HarmonyPatch(typeof(PlatformFileManagerWin), nameof(PlatformFileManagerWin.GetApplicationSavePath))]
internal static class SavePathPatch
{
    private static bool Prefix(ref string __result) { __result = ProfileMod.Root; return false; }
}
[HarmonyPatch(typeof(PlatformFileManagerWin), nameof(PlatformFileManagerWin.GetApplicationDataPath))]
internal static class DataPathPatch
{
    private static bool Prefix(ref string __result) { __result = ProfileMod.Root; return false; }
}
[HarmonyPatch(typeof(PlatformFileManagerWin), nameof(PlatformFileManagerWin.GetApplicationTempPath))]
internal static class TempPathPatch
{
    private static bool Prefix(ref string __result)
    { __result = Path.Combine(ProfileMod.Root, "Temp"); Directory.CreateDirectory(__result); return false; }
}

[HarmonyPatch(typeof(CommandLine), "GetCommandLineOptions")]
internal static class PrivateOfflineOptionsPatch
{
    private static void Postfix(CommandLineOptions __result)
    {
        if(__result!=null)__result.OfflineMode=true;
    }
}
