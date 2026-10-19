using LastEpochLAN.Core;
using UnityEngine;

namespace LastEpochLAN.Mod;

// Runs only in the disposable, private campaign QA process. It exercises real
// native skill creation, the TCP event path and the receiving native VFX path.
internal sealed class NativeVfxProbe : IDisposable
{
    private readonly int[] skills = { 1, 39, 224, 881 };
    private readonly List<object> results = new();
    private LanSession host, guest;
    private Task<LanSession> joining;
    private int index;
    private float next;
    private long sentBefore, playedBefore, hooksBefore, deferredBefore;
    private string skill = "";
    private bool allTransientSkills = true;
    private long screenshotPlayed;
    private float screenshotAt = -1;
    private Camera qaCamera;
    private RenderTexture qaTarget;
    private Texture2D qaPixels;
    private readonly WorldSynchronizer qaWorld = new();
    private readonly List<double> worldCosts = new();
    internal object Result { get; private set; }
    internal bool Finished => Result != null;
    internal void Tick()
    {
        if (Finished) return;
        try
        {
            var local = Il2Cpp.PlayerFinder.getPlayerActor();
            if (local?.stateController?.usingAbility == null || !local.HasMovedToPlayerSpawn) return;
            var scene = Il2Cpp.SceneList.GetCurrentSceneDetails()?.Name ?? "";
            if (host == null)
            {
                var methods = typeof(Il2Cpp.GlobalVFXManager).GetMethods().Where(m => m.Name.StartsWith("RecieveSpawnVfx") || m.Name.StartsWith("RecieveSpawnMutableVfx") || m.Name == "RecieveSpawnSOSTLCF");
                foreach (var method in methods)
                    MelonLoader.MelonLogger.Msg("VFX hook audit " + method.Name + ": prefixes=" + (HarmonyLib.Harmony.GetPatchInfo(method)?.Prefixes.Count ?? 0));
                host = LanSession.Host("VfxQAHost", "VfxQA", "VfxQAOnly", 39029, System.Net.IPAddress.Loopback);
                joining = LanSession.JoinAsync("127.0.0.1", 39029, "VfxQAGuest", "VfxQA", "VfxQAOnly");
                return;
            }
            if (guest == null) { if (!joining.IsCompleted) return; guest = joining.GetAwaiter().GetResult(); }
            var p = local.transform.position;
            host.Publish(new PlayerState { Available = true, Scene = scene, X = p.x, Y = p.y, Z = p.z, Health = 100, MaxHealth = 100 });
            guest.Publish(new PlayerState { Available = true, Scene = scene, X = p.x, Y = p.y, Z = p.z, Health = 100, MaxHealth = 100 });
            SkillCapture.Session = guest;
            var worldStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            qaWorld.Tick(host, scene);
            var worldMs = (System.Diagnostics.Stopwatch.GetTimestamp() - worldStarted) * 1000d / System.Diagnostics.Stopwatch.Frequency;
            if (worldMs > 0.1) worldCosts.Add(worldMs);
            VisualPoint Shift(VisualPoint value) => value == null ? null : value with { X = value.X + 3 };
            while (host.TryTakeSkill(out var command)) if (command?.NativeVisual != null)
            {
                var v = command.NativeVisual;
                NativeVisualEvents.Receive(command with { NativeVisual = v with { Position = Shift(v.Position), Origin = Shift(v.Origin), Start = Shift(v.Start), Target = Shift(v.Target) } });
            }
            NativeVisualEvents.Tick(scene);
            if (screenshotAt >= 0 && Time.unscaledTime >= screenshotAt)
            {
                screenshotAt = -1;
                if (qaCamera == null)
                {
                    qaCamera = new GameObject("VFX QA capture camera").AddComponent<Camera>();
                    qaCamera.enabled = false; qaCamera.orthographic = true; qaCamera.orthographicSize = 6;
                    qaCamera.clearFlags = CameraClearFlags.SolidColor; qaCamera.backgroundColor = Color.black;
                    qaTarget = new RenderTexture(640, 480, 24); qaTarget.Create(); qaCamera.targetTexture = qaTarget;
                    qaPixels = new Texture2D(640, 480, TextureFormat.RGB24, false);
                }
                qaCamera.transform.position = p + new Vector3(1.5f, 8, -8);
                qaCamera.transform.LookAt(p + new Vector3(1.5f, 0.8f, 0));
                qaCamera.Render();
                var previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = qaTarget; qaPixels.ReadPixels(new Rect(0, 0, 640, 480), 0, 0); qaPixels.Apply();
                    File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath), "UserData", "vfx-qa-" + skill + "-" + screenshotPlayed + ".png"), ImageConversion.EncodeToPNG(qaPixels).ToArray());
                }
                finally { RenderTexture.active = previous; }
            }
            if (NativeVisualEvents.Played > screenshotPlayed)
            {
                screenshotPlayed = NativeVisualEvents.Played;
                screenshotAt = Time.unscaledTime + 0.04f;
            }
            if (Time.unscaledTime < next) return;
            if (skill != "")
            {
                if (skill != "Fireball" && (NativeVisualEvents.Sent <= sentBefore || NativeVisualEvents.Played <= playedBefore)) allTransientSkills = false;
                results.Add(new { skill, sent = NativeVisualEvents.Sent - sentBefore, played = NativeVisualEvents.Played - playedBefore,
                    hooks = NativeVisualEvents.HookCalls - hooksBefore, deferred = NativeVisualEvents.DeferredCalls - deferredBefore,
                    NativeVisualEvents.UnownedCalls, NativeVisualEvents.NoPrefabCalls, NativeVisualEvents.LastUnowned, NativeVisualEvents.Error });
            }
            if (index >= skills.Length)
            {
                Result = new { passed = allTransientSkills && NativeVisualEvents.ReplayRenderers > 0 && NativeVisualEvents.Dropped == 0,
                    online = Il2CppEHG.Multiplayer.GameplayEnvironment.IsOnlinePlay,
                    worldStages = qaWorld.Costs,
                    world = new { enemyCount = host.World.Enemies.Length, measuredWorkCalls = worldCosts.Count,
                        averageMs = worldCosts.Count == 0 ? 0 : worldCosts.Average(), maxMs = worldCosts.Count == 0 ? 0 : worldCosts.Max(),
                        p95Ms = worldCosts.Count == 0 ? 0 : worldCosts.OrderBy(v => v).ElementAt((int)((worldCosts.Count - 1) * 0.95)) },
                    results, NativeVisualEvents.Sent, NativeVisualEvents.Received, NativeVisualEvents.Played, NativeVisualEvents.Dropped,
                    NativeVisualEvents.HookCalls, NativeVisualEvents.DeferredCalls, NativeVisualEvents.DeferredOwned, NativeVisualEvents.ReplayRenderers, NativeVisualEvents.ReplayParticles, NativeVisualEvents.LastUnowned, NativeVisualEvents.Error,
                    scope = "Private native campaign, automated skill casts and TCP/native replay counters; pixel appearance and physical LAN not certified" };
                Dispose(); return;
            }
            var ability = Il2Cpp.Ability.getAbility((ushort)skills[index++]);
            if (ability == null) throw new InvalidOperationException("QA skill asset unavailable.");
            skill = ability.name;
            sentBefore = NativeVisualEvents.Sent; playedBefore = NativeVisualEvents.Played;
            hooksBefore = NativeVisualEvents.HookCalls; deferredBefore = NativeVisualEvents.DeferredCalls;
            next = Time.unscaledTime + 5;
            var target = UnityEngine.Object.FindObjectsOfType<Il2Cpp.Actor>().FirstOrDefault(a => a != null && !a.isPlayerActor && a.health != null && a.health.currentHealth > 0);
            var destination = target == null ? p + local.transform.forward * 5 : target.transform.position;
            MelonLoader.MelonLogger.Msg("Native VFX QA casting " + skill);
            local.stateController.usingAbility.UseAbility(ability, destination, false, false, false, 0, true, true, false, false);
        }
        catch (Exception ex) { Result = new { passed = false, skill, results, error = ex.ToString() }; Dispose(); }
    }
    public void Dispose()
    {
        if (SkillCapture.Session == guest) SkillCapture.Session = null; guest?.Dispose(); host?.Dispose();
        qaWorld.Reset();
        if (qaCamera != null) UnityEngine.Object.Destroy(qaCamera.gameObject);
        if (qaTarget != null) { qaTarget.Release(); UnityEngine.Object.Destroy(qaTarget); }
        if (qaPixels != null) UnityEngine.Object.Destroy(qaPixels);
    }
}
