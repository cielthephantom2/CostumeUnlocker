using System.Text.Json;
using Il2CppCysharp.Threading.Tasks;
using UnityEngine;

namespace LastEpochLAN.Mod;

// QA-only native global asset bootstrap, gated on verified private platform paths.
// It does not select, create or load a campaign character.
internal sealed class NativeAssetProbe : IDisposable
{
    private Il2Cpp.GameLoader loader;
    private UniTask.Awaiter initialization;
    private readonly bool naturalShell=Environment.GetCommandLineArgs().Contains("--lelan-natural-shell-qa");
    private bool profileVerified;
    private readonly DateTimeOffset started = DateTimeOffset.UtcNow;
    private readonly float deadline = Time.unscaledTime + 60;
    internal object Result { get; private set; }
    internal bool Finished => Result != null;
    internal bool Passed { get; private set; }

    internal void Tick()
    {
        if (Finished) return;
        try
        {
            if (!profileVerified)
            {
                string profileReport = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath), "UserData", "LastEpochLAN.guest-profile-report.json");
                if (!File.Exists(profileReport))
                {
                    if (Time.unscaledTime >= deadline) throw new TimeoutException("Private native save path report was not generated.");
                    return;
                }
                using var doc = JsonDocument.Parse(File.ReadAllText(profileReport));
                if (!doc.RootElement.GetProperty("passed").GetBoolean()) throw new InvalidOperationException("Native asset QA requires passing private save path verification.");
                string expected = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath), "UserData", "GuestProfile"));
                if (doc.RootElement.GetProperty("root").GetString() != expected ||
                    File.GetLastWriteTimeUtc(profileReport) < started.UtcDateTime.AddSeconds(-10))
                    throw new InvalidOperationException("Native profile verification is stale or belongs to another sandbox.");
                // QA adds switches the game's strict command-line parser does not
                // recognize. Select its supported offline mode before platform and
                // service initialization, rather than booting online services and
                // replacing the user service after they have subscribed.
                Il2CppLE.Core.CommandLine.Options.OfflineMode = true;
                profileVerified=true;
                Application.runInBackground=true;Application.targetFrameRate=20;
                if(!naturalShell)
                {
                    loader = new Il2Cpp.GameLoader();
                    initialization = loader.InitializeAsync().GetAwaiter();
                }
                MelonLoader.MelonLogger.Msg("Native asset QA: verified private profile; initializing game assets.");
            }
            bool shellReady=naturalShell && Il2CppClientAppState.ClientStateManager._sceneManager?._isTransitioning==false &&
                Il2CppClientAppState.ClientStateManager.CurrentClientAppStateType is Il2CppClientAppState.ClientAppStateType.Login or Il2CppClientAppState.ClientAppStateType.CharacterSelect;
            if (naturalShell?shellReady:initialization.IsCompleted)
            {
                if(!naturalShell)initialization.GetResult();
                var classes = Il2Cpp.CharacterClassList.get();
                if (classes == null || classes.classes.Count < 5) throw new InvalidOperationException("Native class assets did not initialize.");
                NativeGiftDataSmoke.Run();
                var corruptionCalculator = OfflineTools.CorruptionNativeSmoke.Run();
                var wovenPotential = OfflineTools.WovenPotentialNativeSmoke.Run();
                bool vfxOnly = Environment.GetCommandLineArgs().Contains("--lelan-vfx-smoke") || Environment.GetCommandLineArgs().Contains("--lelan-gift-smoke");
                var models = vfxOnly ? null : ClassVisualReplica.Smoke();
                var animation = vfxOnly ? null : NativeClassAnimator.Smoke();
                Passed = true;
                Result = new { passed = true, giftItemStatsRoundTrip = true, classCount = classes.classes.Count, models, animation, corruptionCalculator, wovenPotential,
                    localActorAbsent = Il2Cpp.PlayerFinder.getPlayerActor() == null,
                    scope = vfxOnly ? "Private native asset bootstrap for feature QA; class model checks not run" :
                        "Native assets, five class visual prefabs and native movement animation drivers in a private save profile; no campaign entry or combat validation" };
            }
            else if (Time.unscaledTime >= deadline) throw new TimeoutException("Native asset bootstrap did not finish in 60 seconds.");
        }
        catch (Exception ex) { Result = new { passed = false, error = ex.ToString(), scope = "native asset bootstrap" }; }
    }

    public void Dispose() { loader?.Dispose(); loader = null; }
}
