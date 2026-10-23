using System.Security.Cryptography;
using System.Text.Json;
using LastEpochLAN.Core;
using MelonLoader;
using UnityEngine;
using UObject = UnityEngine.Object;

[assembly: MelonInfo(typeof(LastEpochLAN.Mod.LanMod), "TM Last Epoch Co-op Mod", "1.1.0-dev.58", "Local research")]
[assembly: HarmonyDontPatchAll]
[assembly: MelonGame("Eleventh Hour Games", "Last Epoch")]

namespace LastEpochLAN.Mod;

public sealed class LanMod : MelonMod
{
    private static readonly string[] StartupArguments=Environment.GetCommandLineArgs();
    private static readonly string ModVersion = (System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(LanMod).Assembly)?.InformationalVersion?.Split('+')[0]) ?? typeof(LanMod).Assembly.GetName().Version?.ToString(3) ?? "unknown";
    private readonly WorldSynchronizer worldSync = new();
    private readonly NativeEncounterCatalogue encounterCatalogue=new();
    private readonly NativeSummonCapture summonCapture=new();
    private readonly NativeSummonReplicas summonReplicas=new();
    private readonly LatestReportWriter reportWriter=new();
    private readonly TimingWindow updateTiming=new(),lateTiming=new();
    private readonly NativePartyReplica nativeParty = new();
    private readonly NativePeerHealthBars peerHealthBars = new();
    private readonly NativeGuestCombat guestCombat = new();
    private readonly LanGift gift = new();
    private LanSharedChest sharedChest;
    private NativeChestUiProbe chestUiProbe;
    private readonly LanPartyTravel partyTravel = new();
    private readonly LanMonolithParty monolithParty=new();
    private readonly LanLootFilters lootFilters = new();
    private readonly AutoPlayRuntime autoPlay=new();
    private LanSession session;
    private Task<LanSession> pendingJoin;
    private CancellationTokenSource pendingCancellation;
    private readonly Dictionary<string, GameObject> markers = new();
    private readonly Dictionary<string, NativeEquipmentReplica> equipmentReplicas = new();
    private readonly Dictionary<string, NativePlayerAnimationReplica> animationReplicas = new();
    private readonly Dictionary<string, NativeSkillVisualReplica> skillVisualReplicas = new();
    private readonly Dictionary<string, float> equipmentRetryAt = new();
    private readonly Dictionary<string, int> markerClasses = new();
    private readonly HashSet<string> classModels = new();
    private PlayerState[] remoteStates = Array.Empty<PlayerState>();
    private float nextRemoteRead;
    private readonly Dictionary<string, long> appliedVisualSequence = new();
    private double frameTotalMs, frameMaxMs;
    private long frameSamples;
    private double gameFrameSeconds;
    private readonly double[] stageTotals = new double[4], stageMax = new double[4];
    private long CostStage(int stage, long started)
    {
        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        double ms = (now - started) * 1000d / System.Diagnostics.Stopwatch.Frequency;
        stageTotals[stage] += ms; stageMax[stage] = Math.Max(stageMax[stage], ms); return now;
    }
    private bool panel = true;
    private bool supported;
    private string fingerprint = "";
    private string displayName = "Traveller";
    private string hostIp = "192.168.1.100";
    private string port = "7777";
    private string roomCode = "";
    private string message = "Idle";
    private string currentScene = "";
    private float nextPublish;
    private float nextGuard;
    private bool offline;
    private string reportFile;
    private string configPath;
    private bool runtimeReported;
    private bool adapterSmokeRequested;
    private bool adapterSmokePassed;
    private bool worldSmokePassed;
    private bool guiReported;
    private readonly ReleaseUpdateChecker updateChecker=new();
    private LanSettings settings;
    private bool autoStartAttempted;
    private float nextSmokeReport;
    private float nextRuntimeReport;
    private long skillsReceived;
    private string lastRemoteSkill = "";
    private bool worldAuditRequested;
    private float nextAudit;
    private bool characterProbeCompleted;
    private bool classSmokeRequested;
    private bool classSmokeCompleted;
    private bool classSmokePassed;
    private bool nativeServerProbeCompleted;
    private NativeSessionProbe nativeSessionProbe;
    private bool nativeSessionReported;
    private NativeAssetProbe nativeAssetProbe;
    private bool nativeAssetsReported;
    private NativeCampaignProbe nativeCampaignProbe;
    private bool nativeCampaignReported;
    private NativeVfxProbe vfxProbe;
    private bool vfxProbeReported;
    private NativeGiftProbe giftProbe;
    private bool giftProbeReported;

    public override void OnInitializeMelon()
    {
        RoomPatches.Install(HarmonyInstance);
        sharedChest=new LanSharedChest(gift);
        string runtime = Path.GetDirectoryName(Environment.ProcessPath)!;
        string assemblyPath = Path.Combine(runtime, "GameAssembly.dll");
        using var stream = File.OpenRead(assemblyPath);
        using var sha256 = SHA256.Create();
        fingerprint = Convert.ToHexString(sha256.ComputeHash(stream));
        // Exact inspected builds only. The 2026-10-22 Steam build26087601
        // passed new-interop compilation and installed all72 selected hooks.
        // This compatibility gate is not native gameplay acceptance.
        supported = IsSupportedGameAssembly(fingerprint);
        roomCode = Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
        adapterSmokeRequested = StartupArguments.Contains("--lelan-adapter-smoke");
        classSmokeRequested = StartupArguments.Contains("--lelan-class-visual-smoke");
        worldAuditRequested = StartupArguments.Contains("--lelan-world-audit");
        reportFile = Path.Combine(runtime, "UserData", "LastEpochLAN.runtime-report.json");
        Directory.CreateDirectory(Path.GetDirectoryName(reportFile)!);
        configPath = Path.Combine(runtime, "UserData", "LastEpochLAN.config.json");
        settings = new LanSettings { RoomCode = roomCode };
        OfflineTools.OfflineToolkit.Initialize();
        updateChecker.Check(ModVersion);
        try
        {
            if (File.Exists(configPath))
            {
                if (new FileInfo(configPath).Length > 4096) throw new InvalidDataException("LAN configuration exceeds 4096 bytes.");
                settings = JsonSerializer.Deserialize<LanSettings>(File.ReadAllText(configPath)) ?? throw new InvalidDataException("Empty LAN configuration.");
                displayName = settings.Name ?? "Traveller";
                hostIp = settings.HostIp ?? "192.168.1.100";
                port = settings.Port.ToString();
                roomCode = settings.RoomCode ?? "";
            }
            else File.WriteAllText(configPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) { settings.AutoStart = false; message = "Configuration error: " + ex.Message; LoggerInstance.Warning(message); }
        settings.ExperimentalClassModels = settings.GuestPrivateRewards = settings.HostOwnedEnemies = settings.ExperimentalGuestCombat = false;
        settings.ExperimentalNativeRewards = settings.ProbeRemoteCharacter = false;
        SaveRuntimeReport("initialized", null);
        LoggerInstance.Msg($"Internet room: F2 Shared Stash, Loot Filters and Room Ladder enabled; offline aggressive minion detection x2; shared world/combat/travel disabled. Patches installed={RoomPatches.Installed}, skipped={RoomPatches.Skipped}. F4 opens the panel.");
        if (!supported) message = "Unsupported GameAssembly build. See README.";
    }

    internal static bool IsSupportedGameAssembly(string hash)=>hash is
        "D5F9FB458F0E9878E697A92B590A586A52FD77E645F8BF87BB4F2B74C6C56567" or
        "323E9183F3BEE600B6C9E1C469218B9DE926E9044F2CA3D44FAE8A0EC9A91464";

    public override void OnUpdate()
    {
        RuntimePerformance.BeginFrame();
        long updateStarted=System.Diagnostics.Stopwatch.GetTimestamp(),allocatedBefore=GC.GetAllocatedBytesForCurrentThread();
        try
        {
            using (RuntimePerformance.Measure(PerformanceArea.Toolkit)) OfflineTools.OfflineToolkit.Tick(panel);
            if (StartupArguments.Contains("--lelan-gift-smoke") && !giftProbeReported && nativeAssetProbe?.Passed == true)
            {
                giftProbe ??= new NativeGiftProbe(); giftProbe.Tick();
                if (giftProbe.Finished)
                {
                    giftProbeReported = true;
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(reportFile), "LastEpochLAN.gift-probe.json"),
                        JsonSerializer.Serialize(giftProbe.Result, new JsonSerializerOptions { WriteIndented = true }));
                }
            }
            if (StartupArguments.Contains("--lelan-vfx-smoke") && !vfxProbeReported && nativeAssetProbe?.Passed == true)
            {
                vfxProbe ??= new NativeVfxProbe(); vfxProbe.Tick();
                if (vfxProbe.Finished)
                {
                    vfxProbeReported = true;
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(reportFile), "LastEpochLAN.vfx-probe.json"),
                        JsonSerializer.Serialize(vfxProbe.Result, new JsonSerializerOptions { WriteIndented = true }));
                }
            }
            if (!nativeCampaignReported && StartupArguments.Contains("--lelan-private-campaign-smoke") && nativeAssetProbe?.Passed == true)
            {
                nativeCampaignProbe ??= new NativeCampaignProbe();
                nativeCampaignProbe.Tick();
                if (nativeCampaignProbe.Finished)
                {
                    nativeCampaignReported = true;
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(reportFile), "LastEpochLAN.native-campaign-probe.json"),
                        JsonSerializer.Serialize(nativeCampaignProbe.Result, new JsonSerializerOptions { WriteIndented = true }));
                }
            }
            if (!nativeAssetsReported && StartupArguments.Contains("--lelan-native-assets-smoke"))
            {
                nativeAssetProbe ??= new NativeAssetProbe();
                nativeAssetProbe.Tick();
                if (nativeAssetProbe.Finished)
                {
                    nativeAssetsReported = true;
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(reportFile), "LastEpochLAN.native-assets-probe.json"),
                        JsonSerializer.Serialize(nativeAssetProbe.Result, new JsonSerializerOptions { WriteIndented = true }));
                }
            }
            if (!nativeSessionReported && StartupArguments.Contains("--lelan-native-session-smoke") &&
                (!StartupArguments.Contains("--lelan-native-assets-smoke") || nativeAssetProbe?.Passed == true))
            {
                nativeSessionProbe ??= new NativeSessionProbe(nativeAssetProbe?.Passed == true);
                nativeSessionProbe.Tick();
                if (nativeSessionProbe.Finished)
                {
                    nativeSessionReported = true;
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(reportFile), "LastEpochLAN.native-session-probe.json"),
                        JsonSerializer.Serialize(nativeSessionProbe.Result, new JsonSerializerOptions { WriteIndented = true }));
                }
            }
            if (!nativeServerProbeCompleted && StartupArguments.Contains("--lelan-native-server-smoke"))
            {
                nativeServerProbeCompleted = true;
                object result;
                try { result = NativeServerProbe.Smoke(); }
                catch (Exception ex) { result = new { passed = false, error = ex.ToString(), scope = "native transport only" }; }
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(reportFile), "LastEpochLAN.native-server-probe.json"),
                    JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            }
            if (Input.GetKeyDown(KeyCode.F4)) panel = !panel;
            if (Time.unscaledTime >= nextGuard)
            {
                nextGuard = Time.unscaledTime + 0.5f;
                offline = !Il2CppEHG.Multiplayer.GameplayEnvironment.IsOnlinePlay;
                if (!runtimeReported)
                {
                    SaveRuntimeReport("unity-update-and-game-interop-ok", offline);
                    runtimeReported = true;
                    LoggerInstance.Msg("Unity update callback and GameplayEnvironment interop verified.");
                }
            }
            if (!offline || !supported)
            {
                autoPlay.Suspend();
                if (session != null || pendingJoin != null) StopRoom();
                return;
            }
            var smokePlayer = classSmokeRequested && !classSmokeCompleted ? Il2Cpp.PlayerFinder.getPlayerActor() : null;
            if (classSmokeRequested && !classSmokeCompleted && smokePlayer != null && smokePlayer.HasMovedToPlayerSpawn)
            {
                classSmokeCompleted = true;
                try
                {
                    var classes = ClassVisualReplica.Smoke();
                    classSmokePassed = true;
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(reportFile), "LastEpochLAN.class-visual-smoke.json"),
                        JsonSerializer.Serialize(new { passed = true, classes, scope = "native base-class prefab creation and visual-only isolation; campaign animation/equipment/combat not verified" },
                            new JsonSerializerOptions { WriteIndented = true }));
                    LoggerInstance.Msg("Native visual-only class prefab smoke passed for all five classes.");
                }
                catch (Exception ex) { LoggerInstance.Error("Class visual smoke failed: " + ex); }
                SaveRuntimeReport("class-visual-smoke", offline);
            }
            if (settings.AutoStart && !autoStartAttempted && !OfflineTools.NewCharacterBoost.BlocksRoom)
            {
                autoStartAttempted = true;
                if (settings.Role == "Host") StartHost();
                else if (settings.Role == "Join") StartJoin();
                else message = "Configuration Role must be Host or Join.";
                LoggerInstance.Msg("LAN auto-start: " + message);
            }
            if (worldAuditRequested && Time.unscaledTime >= nextAudit)
            {
                nextAudit = Time.unscaledTime + 2f;
                try { HostWorldAudit.Capture(Path.Combine(Path.GetDirectoryName(reportFile), "LastEpochLAN.host-world-audit.json")); }
                catch (Exception ex)
                {
                    worldAuditRequested = false;
                    LoggerInstance.Warning("Read-only world audit disabled: " + ex.Message);
                }
            }
            if (settings.ProbeRemoteCharacter && !characterProbeCompleted)
            {
                try
                {
                    characterProbeCompleted = RemoteCharacterProbe.Capture(Path.Combine(Path.GetDirectoryName(reportFile), "LastEpochLAN.character-isolation.json"));
                    if (characterProbeCompleted) LoggerInstance.Msg("Inactive character isolation report saved. This does not establish remote combat.");
                }
                catch (Exception ex)
                {
                    characterProbeCompleted = true;
                    LoggerInstance.Warning("Character isolation probe stopped: " + ex.Message);
                }
            }
            if (adapterSmokeRequested && !adapterSmokePassed)
            {
                worldSmokePassed = WorldSynchronizer.NativeSmoke();
                NativeRewardCompletion.NativeSmoke();
                NativeAilmentSynchronization.NativeSmoke();
                OfflineTools.OfflineToolkitSmoke.VerifyHooks();
                LoggerInstance.Msg("Native async drop state-machine completion detour verified; campaign reward delivery remains unverified.");
                NativePeerHealthBars.NativeSmoke();
                NativeCosmeticAbiSmoke.Run();
                LoggerInstance.Msg("Native cosmetic dictionary ContainsKey/indexer ABI verified with isolated footprint/aura dictionaries.");
                NativeEnemyPoseBlend.NativeSmoke();
                NativeGuestCombat.NativeSmoke();
                WorldSynchronizer.NativeOwnershipSmoke();
                NativeSkillVisualReplica.NativeMotionSmoke();
                LoggerInstance.Msg("Native host NPC VFX ownership, health target registry and once-per-frame projectile presentation verified.");
                LoggerInstance.Msg("Native overhead interface/renderer, enemy interpolation and destroyed travel-target cleanup verified.");
                LoggerInstance.Msg("Native enemy transform/HP application and damage-to-host IL2CPP detour verified.");
                var testMarker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                testMarker.GetComponent<Collider>().enabled = false;
                testMarker.transform.position = new Vector3(1, 2, 3);
                testMarker.transform.rotation = Quaternion.Euler(0, 45, 0);
                testMarker.transform.localScale = new Vector3(0.45f, 0.6f, 0.45f);
                UObject.DontDestroyOnLoad(testMarker);
                UObject.Destroy(testMarker);
                adapterSmokePassed = true;
                SaveRuntimeReport("marker-primitive-smoke-ok", offline);
                LoggerInstance.Msg("Marker creation, transform, collider disabling and cleanup verified.");
            }
            // Local Assist runs before room polling/teardown can throw.
            // UI panels do not participate in the Assist pause policy.
            if(supported) using (RuntimePerformance.Measure(PerformanceArea.AutoAssist)) autoPlay.Tick(
                RoomFeaturePolicy.SharedWorldEnabled && (partyTravel.Busy || monolithParty.Busy));
            if (pendingJoin?.IsCompleted == true)
            {
                try { session = pendingJoin.GetAwaiter().GetResult(); message = "Connected"; }
                catch (Exception ex) { message = ex.Message; LoggerInstance.Warning(message); }
                pendingJoin = null;
                pendingCancellation?.Dispose();
                pendingCancellation = null;
            }
            if ((session!=null || gift.Busy) && !StartupArguments.Contains("--lelan-gift-smoke") && !StartupArguments.Contains("--lelan-shared-chest-smoke")) gift.Tick(session);
            if ((session!=null || sharedChest.Open || sharedChest.Busy) && !StartupArguments.Contains("--lelan-gift-smoke") && !StartupArguments.Contains("--lelan-shared-chest-smoke")) sharedChest.Tick(session);
            if(Input.GetKeyDown(KeyCode.F2))sharedChest.Toggle();
            if (RoomFeaturePolicy.SharedWorldEnabled && !StartupArguments.Contains("--lelan-gift-smoke")) partyTravel.Tick(session);
            if (RoomFeaturePolicy.SharedWorldEnabled && !StartupArguments.Contains("--lelan-gift-smoke")) monolithParty.Tick(session);
            if (!StartupArguments.Contains("--lelan-hud-policy")) HudAuthority.Session = RoomFeaturePolicy.SharedWorldEnabled && (session?.IsHost==true || partyTravel.Participation.Enabled) ? session : null;
            lootFilters.Tick(session);
            NativeRoomLadder.Tick(session);
            if (session == null)
            {
                if((autoPlay.Open || autoPlay.Armed) && Time.unscaledTime>=nextRuntimeReport)
                {nextRuntimeReport=Time.unscaledTime+2;SaveRuntimeReport("local-autoplay-diagnostics",offline);}
                return;
            }
            if (!session.Connected) { message = session.Status; StopRoom(); return; }
            if (!RoomFeaturePolicy.SharedWorldEnabled)
            {
                // Inventory readiness only: no actor/scene graph/animation/VFX,
                // summon/loot capture or native party/combat/world update work.
                if (Time.unscaledTime >= nextPublish)
                {
                    nextPublish = Time.unscaledTime + 0.25f;
                    PublishInventoryPresence();
                }
                if (Time.unscaledTime >= nextRuntimeReport)
                { nextRuntimeReport = Time.unscaledTime + 2; SaveRuntimeReport("inventory-only-room", offline); }
                return;
            }
            bool coopReady=monolithParty.SharedWorldReady && (session.IsHost || partyTravel.Participation.CanShareWorld(false,currentScene,session.PlayerPositions.FirstOrDefault()?.Scene??"",monolithParty.Borrowing,partyTravel.Busy));
            encounterCatalogue.Tick(session,currentScene,coopReady);
            SkillCapture.Session = coopReady ? session : null;
            long lanStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            long stageStarted = lanStarted;
            if (Time.unscaledTime >= nextPublish)
            {
                nextPublish = Time.unscaledTime + 0.05f;
                using (RuntimePerformance.Measure(PerformanceArea.PlayerPublish)) PublishPlayer();
            }
            worldSync.NativeRewardsEnabled = settings.GuestPrivateRewards;
            stageStarted = CostStage(0, stageStarted);
            // Retire scene targets before world capture invokes native AI range checks.
            if (settings.ExperimentalGuestCombat && coopReady) guestCombat.Tick(session, currentScene);
            stageStarted = CostStage(2, stageStarted);
            worldSync.HostOwnedEnemies = settings.HostOwnedEnemies;
            if(coopReady) { using var worldCost = RuntimePerformance.Measure(PerformanceArea.WorldSync); worldSync.Tick(session, currentScene); }
            else {worldSync.Reset();guestCombat.Reset();}
            stageStarted = CostStage(1, stageStarted);
            UpdateMarkers();
            peerHealthBars.Tick(remoteStates, markers);
            nativeParty.Tick(session);
            CostStage(3, stageStarted);
            double elapsedMs = (System.Diagnostics.Stopwatch.GetTimestamp() - lanStarted) * 1000d / System.Diagnostics.Stopwatch.Frequency;
            frameTotalMs += elapsedMs; frameMaxMs = Math.Max(frameMaxMs, elapsedMs); frameSamples++;
            gameFrameSeconds += Time.unscaledDeltaTime;
            while (session.TryTakeSkill(out var skill))
            {
                if (!coopReady || skill.Scene != currentScene) continue;
                if (skill.NativeVisual != null) { NativeVisualEvents.Receive(skill); continue; }
                skillsReceived++; lastRemoteSkill = skill.Ability;
                bool animated = animationReplicas.TryGetValue(skill.PlayerId, out var replica) && replica.ReplaySkill(skill);
                LoggerInstance.Msg($"Remote skill {skill.Ability}: animation={skill.AnimationNumber}, zone={skill.ZoneAbility}, native character animation={animated}; native VFX commands with projectile snapshot fallback; remote gameplay execution remains disabled.");
            }
            NativeVisualEvents.Tick(currentScene);
            if (Time.unscaledTime >= nextRuntimeReport)
            {
                nextRuntimeReport = Time.unscaledTime + 2f;
                SaveRuntimeReport("active-lan-diagnostics", offline);
            }
            if (adapterSmokeRequested && Time.unscaledTime >= nextSmokeReport)
            {
                nextSmokeReport = Time.unscaledTime + 1f;
                SaveRuntimeReport("native-game-lan-smoke", offline);
            }
        }
        catch (Exception ex)
        {
            message = "Adapter error: " + ex.Message;
            LoggerInstance.Error(message + "\n" + ex);
            StopRoom();
        }
        finally {updateTiming.Add(updateStarted,allocatedBefore);}
    }

    private void PublishInventoryPresence()
    {
        using var performance = RuntimePerformance.Measure(PerformanceArea.PlayerPublish);
        var actor = Il2Cpp.PlayerFinder.getPlayerActor();
        bool ready = sharedChest.NativeReady && actor != null && gift.Character == actor.characterDataTracker?.charData?.Id;
        session.Publish(RoomFeaturePolicy.Presence(ready, gift.Identity, gift.Character, gift.Group) with {Ladder = NativeRoomLadder.Capture()});
    }

    private void PublishPlayer()
    {
        var actor = Il2Cpp.PlayerFinder.getPlayerActor();
        if (actor == null || !actor.gameObject.activeInHierarchy || !actor.HasMovedToPlayerSpawn)
        {
            currentScene = "";
            session!.Publish(new PlayerState());
            return;
        }
        bool stashReady=sharedChest.NativeReady && !Il2CppLE.UI.LoadingScreen.IsVisible &&
            !partyTravel.Busy && !monolithParty.Busy && gift.Character==actor.characterDataTracker?.charData?.Id;
        if(!monolithParty.GameplayReady)
        {
            // Keep world/combat unavailable, but retain the loaded inventory's
            // identity so F2 does not depend on the borrowed Echo matching.
            currentScene="";
            session!.Publish(new PlayerState {StashReady=stashReady,
                GiftIdentity=stashReady?gift.Identity:"",CharacterId=stashReady?gift.Character:"",GiftGroup=stashReady?gift.Group:""});
            return;
        }
        Vector3 position = actor.position();
        // A persistent actor can live in DontDestroyOnLoad; use the game's gameplay-zone identity.
        currentScene = Il2Cpp.SceneList.GetCurrentSceneDetails()?.Name ?? "";
        var summons=summonCapture.Capture(session,actor,currentScene);
        session!.Publish(new PlayerState
        {
            StashReady=stashReady, Available = currentScene != "", Dead = actor.isDying(false), Scene = currentScene,
            Cooperative=session.IsHost || partyTravel.Participation.Enabled && monolithParty.SharedWorldReady,
            EchoRunId=monolithParty.LocalRun,
            Health = actor.health != null ? Mathf.Clamp(actor.health.currentHealth, 0, actor.health.maxHealth) : 0,
            MaxHealth = actor.health?.maxHealth ?? 0,
            Ward = Mathf.Clamp(actor.protection?.currentWard ?? 0, 0, 100000000),
            GiftIdentity = gift.Identity, CharacterId = gift.Character, GiftGroup = gift.Group,
            X = position.x, Y = position.y, Z = position.z, Yaw = actor.transform.eulerAngles.y,
            Avatar = ClassVisualReplica.Capture(actor), DroppedItems = LanDroppedItems.Capture(currentScene),
            SummonEpoch=summons.Epoch,SummonSampleTime=summons.Time,Summons=summons.States
        });
    }

    public override void OnLateUpdate()
    {
        if (!RoomFeaturePolicy.SharedWorldEnabled) return;
        long started=System.Diagnostics.Stopwatch.GetTimestamp(),allocated=GC.GetAllocatedBytesForCurrentThread();
        try
        {
        try {NativePartyStatus.LateUpdate();} catch(Exception ex) {NativePartyStatus.Fail(ex.GetBaseException().Message);}
        NativeCosmeticReplica.TickAll();
        summonReplicas.Tick();
        if (session?.Connected == true && !session.IsHost && monolithParty.SharedWorldReady) worldSync.RenderGuestPoses();
        }
        finally {lateTiming.Add(started,allocated);}
    }

    private void UpdateMarkers()
    {
        using var performance = RuntimePerformance.Measure(PerformanceArea.RemotePresentation);
        if (Time.unscaledTime >= nextRemoteRead)
        {
            nextRemoteRead = Time.unscaledTime + 0.05f;
            remoteStates = session!.Players.Where(s => s.Id != session.LocalId && s.Cooperative && (session.IsHost || partyTravel.Participation.Enabled) && s.Available && s.Scene == currentScene && currentScene != "").ToArray();
        }
        PlayerState[] remote = remoteStates;
        summonReplicas.Apply(remote,currentScene);
        var visible = remote.Select(s => s.Id).ToHashSet();
        var connected = session.PlayerPositions.Select(p => p.Id).ToHashSet();
        foreach (string id in markers.Keys.Where(id => !connected.Contains(id)).ToArray())
        {
            UObject.Destroy(markers[id]);
            markers.Remove(id);
            equipmentReplicas.Remove(id);
            animationReplicas.Remove(id);
            if (skillVisualReplicas.Remove(id, out var skillVisuals)) skillVisuals.Dispose();
            equipmentRetryAt.Remove(id);
            markerClasses.Remove(id); classModels.Remove(id);
            appliedVisualSequence.Remove(id);
        }
        // Other-zone members remain party members. Reuse their loaded native
        // model after reunion instead of rebuilding assets on each travel.
        foreach (var marker in markers)
            if (!visible.Contains(marker.Key) && marker.Value != null && marker.Value.activeSelf) marker.Value.SetActive(false);
        foreach (var id in skillVisualReplicas.Keys.Where(id => !visible.Contains(id)).ToArray())
        {
            skillVisualReplicas[id].Dispose(); skillVisualReplicas.Remove(id);
            appliedVisualSequence.Remove(id);
        }
        foreach (PlayerState state in remote)
        {
            if (!skillVisualReplicas.TryGetValue(state.Id, out var skillVisual))
                skillVisualReplicas[state.Id] = skillVisual = new NativeSkillVisualReplica(true);
            skillVisual.Tick();
            if (!appliedVisualSequence.TryGetValue(state.Id, out var sequence) || sequence != state.Sequence)
            {
                skillVisual.Apply(state.Avatar?.Visuals ?? Array.Empty<SkillVisualState>());
                appliedVisualSequence[state.Id] = state.Sequence;
            }
            int desiredClass = settings.ExperimentalClassModels && state.Avatar != null ? state.Avatar.ClassId : -1;
            if (markers.TryGetValue(state.Id, out var previous) && markerClasses.GetValueOrDefault(state.Id, -1) != desiredClass)
            { UObject.Destroy(previous); markers.Remove(state.Id); classModels.Remove(state.Id); equipmentReplicas.Remove(state.Id); animationReplicas.Remove(state.Id); }
            if (!markers.TryGetValue(state.Id, out var marker) || marker == null)
            {
                if (desiredClass >= 0)
                {
                    try { marker = ClassVisualReplica.Create(desiredClass); classModels.Add(state.Id); }
                    catch (Exception ex) { LoggerInstance.Warning("Class model unavailable; capsule fallback: " + ex.Message); }
                }
                markerClasses[state.Id] = desiredClass;
                if (marker == null)
                {
                    marker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    marker.transform.localScale = new Vector3(0.45f, 0.6f, 0.45f);
                    var collider = marker.GetComponent<Collider>();
                    if (collider != null) collider.enabled = false;
                }
                marker.name = "LAN marker - " + state.Name;
                UObject.DontDestroyOnLoad(marker);
                markers[state.Id] = marker;
            }
            bool model = classModels.Contains(state.Id);
            Vector3 target = new(state.X, state.Y + (model ? 0 : 0.65f), state.Z);
            bool moving = Vector3.Distance(marker.transform.position, target) > 0.03f;
            marker.transform.position = !marker.activeSelf || Vector3.Distance(marker.transform.position, target) > 8f ? target :
                Vector3.Lerp(marker.transform.position, target, 1f - Mathf.Exp(-15f * Time.unscaledDeltaTime));
            marker.transform.rotation = Quaternion.Euler(0, state.Yaw, 0);
            marker.SetActive(!state.Dead);
            if (model)
            {
                if (!animationReplicas.TryGetValue(state.Id, out var animationReplica))
                    animationReplicas[state.Id] = animationReplica = new NativePlayerAnimationReplica(marker);
                animationReplica.Apply(state.Avatar?.Motion, moving);
                if (state.Avatar?.Motion?.State is 5 or 9)
                    animationReplica.ApplyCastSnapshot(state.Avatar.Animation, state.Sequence);
                if (state.Avatar?.Motion == null && state.Avatar?.Animation != null)
                    ClassVisualReplica.ApplyAnimation(marker, state.Avatar);
            }
            if (model && state.Avatar != null)
            {
                if (!equipmentReplicas.TryGetValue(state.Id, out var equipment) &&
                    Time.unscaledTime >= equipmentRetryAt.GetValueOrDefault(state.Id))
                {
                    try { equipmentReplicas[state.Id] = equipment = new NativeEquipmentReplica(marker); }
                    catch (Exception ex)
                    {
                        equipmentRetryAt[state.Id] = Time.unscaledTime + 2;
                        LoggerInstance.Warning("Remote equipment renderer: " + ex.Message);
                    }
                }
                equipment?.Tick(state.Avatar);
            }
        }
    }

    public override void OnGUI()
    {
        using var performance = RuntimePerformance.Measure(PerformanceArea.Gui);
        try { DrawCoopUi(); }
        finally
        {
            OfflineTools.Dps.DpsRuntime.Draw();sharedChest?.Draw();nativeCampaignProbe?.DrawChest();
            if(StartupArguments.Contains("--lelan-chest-ui-smoke") && nativeAssetProbe?.Passed==true)
            {chestUiProbe??=new();chestUiProbe.Draw();}
        }
    }
    private void DrawCoopUi()
    {
        if(RoomFeaturePolicy.SharedWorldEnabled && (session?.IsHost==true || partyTravel.Participation.Enabled))LanHud.Draw(session, currentScene, worldSync.GuestWorld);
        if (!panel) return;
        GUI.enabled = true;
        if(OfflineChangelog.Open) {OfflineChangelog.Draw(ModVersion);return;}
        if(OfflineTools.OfflineToolkit.PanelOpen) {OfflineTools.OfflineToolkit.Draw();return;}
        GUI.Box(new Rect(16, 16, 460, 518), "TM Last Epoch Co-op Mod v" + ModVersion + " (F4)");
        GUI.Label(new Rect(30,492,420,24),"Internet room: Shared Stash + Loot Filters + Ladder.");
        if(GUI.Button(new Rect(30,395,420,27),"Mod Settings")) OfflineTools.OfflineToolkit.PanelOpen=true;
        if(GUI.Button(new Rect(30,430,420,27),"Changelog")) OfflineChangelog.Show();
        updateChecker.Check(ModVersion);
        var updateStatus=updateChecker.Status;
        var updateStyle=new GUIStyle(GUI.skin.label);
        if(updateStatus.StartsWith("New update available:",StringComparison.Ordinal))
            updateStyle.normal.textColor=Color.red;
        else if(updateStatus.StartsWith("Latest version:",StringComparison.Ordinal))
            updateStyle.normal.textColor=Color.green;
        GUI.Label(new Rect(30,464,420,24),updateStatus,updateStyle);
        GUI.Label(new Rect(30, 52, 100, 22), "Name");
        displayName = GUI.TextField(new Rect(140, 52, 310, 23), displayName, 32);
        GUI.Label(new Rect(30, 84, 100, 22), "Host IPv4");
        hostIp = GUI.TextField(new Rect(140, 84, 200, 23), hostIp, 45);
        if (GUI.Button(new Rect(350, 84, 100, 23), "Save settings"))
        {
            try { SaveConnectionSettings(); message = "Connection settings saved"; }
            catch (Exception ex) { message = ex.Message; }
        }
        GUI.Label(new Rect(30, 116, 100, 22), "Port");
        port = GUI.TextField(new Rect(140, 116, 310, 23), port, 5);
        GUI.Label(new Rect(30, 148, 100, 22), "Room code");
        roomCode = GUI.TextField(new Rect(140, 148, 310, 23), roomCode, 64);
        GUI.enabled = supported && offline && session == null && pendingJoin == null;
        if (GUI.Button(new Rect(30, 186, 130, 30), "Host Internet")) StartHost();
        if (GUI.Button(new Rect(175, 186, 130, 30), "Join Internet")) StartJoin();
        GUI.enabled = session != null || pendingJoin != null;
        if (GUI.Button(new Rect(320, 186, 130, 30), "Disconnect")) { StopRoom(); message = "Disconnected"; }
        GUI.enabled = true;
        if (!supported) GUI.Label(new Rect(30, 225, 420, 22), "Unsupported game version");
        else if (!offline) GUI.Label(new Rect(30, 225, 420, 22), "Choose Play Offline");
        else GUI.Label(new Rect(30, 225, 420, 22), session == null ? message : "Internet room connected — F2 Shared Stash");
        if (session?.Connected == true)
        {
            int y = 254;
            foreach (var player in session.PlayerPositions)
            {
                GUI.Label(new Rect(30, y, 380, 20), player.Name + (player.StashReady ? " — inventory ready" : " — loading inventory"));
                y += 21;
            }
        }
        GUI.Label(new Rect(30, 360, 420, 22), lootFilters.Status);
        GUI.Label(new Rect(30,335,420,22),"World, combat, VFX and map travel sync paused.");
        if (!guiReported) { guiReported = true; SaveRuntimeReport("imgui-callback-ok", offline); }
    }
    private int ReadPort()
    {
        if (!int.TryParse(port, out int value) || value < 1 || value > 65535) throw new ArgumentException("Port: 1–65535.");
        return value;
    }

    private void StartHost()
    {
        if(monolithParty.Busy || monolithParty.Borrowing){message="Wait for shared Echo cleanup before starting another room.";return;}
        if(OfflineTools.NewCharacterBoost.BlocksRoom) {message="Wait for new character setup and End of Time travel.";return;}
        try { SaveConnectionSettings(); session = LanSession.Host(displayName, fingerprint, roomCode, ReadPort()); message = "Hosting"; }
        catch (Exception ex) { message = ex.Message; }
    }

    private void StartJoin()
    {
        if(monolithParty.Busy || monolithParty.Borrowing){message="Wait for shared Echo cleanup before joining another room.";return;}
        if(OfflineTools.NewCharacterBoost.BlocksRoom) {message="Wait for new character setup and End of Time travel.";return;}
        try
        {
            SaveConnectionSettings();
            pendingCancellation = new CancellationTokenSource();
            pendingJoin = LanSession.JoinAsync(hostIp, ReadPort(), displayName, fingerprint, roomCode, pendingCancellation.Token);
            message = "Connecting...";
        }
        catch (Exception ex) { message = ex.Message; pendingCancellation?.Dispose(); pendingCancellation = null; }
    }

    private void SaveConnectionSettings()
    {
        string address = hostIp.Trim();
        if (!System.Net.IPAddress.TryParse(address, out var parsed) ||
            parsed.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            throw new ArgumentException("Enter a valid host IPv4 address, e.g. 192.168.1.8.");
        int tcpPort = ReadPort();
        settings.HostIp = hostIp = parsed.ToString();
        settings.Port = tcpPort; settings.Name = displayName; settings.RoomCode = roomCode;
        File.WriteAllText(configPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void StopRoom()
    {
        // Detach transport first. A stale native panel must not keep a failed
        // session attached and repeat its exception before local Assist ticks.
        var closingSession=session;session=null;
        panel=false;OfflineTools.OfflineToolkit.PanelOpen=false;
        void Clean(string name,Action cleanup)=>RoomDisconnectCleanup.Run(cleanup,
            ex=>LoggerInstance.Warning("Room disconnect ("+name+"): "+ex.GetBaseException().Message));
        Clean("local Assist",autoPlay.RoomDisconnected);
        var cancellation=pendingCancellation;pendingCancellation=null;
        var joining=pendingJoin;pendingJoin=null;
        Clean("cancel join",()=>cancellation?.Cancel());
        if(joining!=null)
        {
            _=joining.ContinueWith(task=>
            {
                if(task.IsCompletedSuccessfully)task.Result.Dispose();else _=task.Exception;
            },TaskScheduler.Default);
        }
        Clean("join cancellation",()=>cancellation?.Dispose());
        Clean("transport",()=>closingSession?.Dispose());
        HudAuthority.Session=null;SkillCapture.Session=null;
        Clean("gift",gift.Disconnect);
        Clean("shared stash",()=>sharedChest?.Disconnect());
        Clean("travel",partyTravel.Disconnect);
        Clean("monolith",monolithParty.Disconnect);
        Clean("filters",()=>lootFilters.Tick(null));
        Clean("ladder",()=>NativeRoomLadder.Tick(null));
        if(RoomFeaturePolicy.SharedWorldEnabled)
        {
            Clean("world",worldSync.Reset);
            Clean("encounters",encounterCatalogue.Reset);
            Clean("guest combat",guestCombat.Reset);
            Clean("drops",LanDroppedItems.Reset);
            Clean("HUD",LanHud.Reset);
            Clean("party",nativeParty.Reset);
            Clean("health bars",peerHealthBars.Reset);
            Clean("skill visuals",NativeSkillVisualCapture.Reset);
            Clean("visual events",NativeVisualEvents.Reset);
            foreach(var marker in markers.Values)if(marker!=null)Clean("marker",()=>UObject.Destroy(marker));
            foreach(var visuals in skillVisualReplicas.Values)Clean("skill replica",visuals.Dispose);
            Clean("summon replicas",summonReplicas.Dispose);
            Clean("summon capture",summonCapture.Reset);
        }
        remoteStates=Array.Empty<PlayerState>();nextRemoteRead=0;appliedVisualSequence.Clear();
        markers.Clear();equipmentReplicas.Clear();animationReplicas.Clear();skillVisualReplicas.Clear();
        equipmentRetryAt.Clear();markerClasses.Clear();classModels.Clear();
        nextRuntimeReport=0;
    }

    private void SaveRuntimeReport(string phase, bool? offlineMode)
    {
        using var performance = RuntimePerformance.Measure(PerformanceArea.ReportCapture);
        if (reportFile == null) return;
        reportWriter.Publish(reportFile,new
        {
            phase, utc = DateTimeOffset.UtcNow, modVersion = ModVersion, protocolVersion = LanProtocol.Version, supported, gameAssemblySha256 = fingerprint, offlineMode,
            roomMode = "Internet inventory-only: F2 + Loot Filters + Room Ladder",
            sharedWorldEnabled = RoomFeaturePolicy.SharedWorldEnabled,
            modUi = new { f4Visible=panel, settingsPageSelected=OfflineTools.OfflineToolkit.PanelOpen,
                settingsVisible=panel && OfflineTools.OfflineToolkit.PanelOpen, stashVisible=sharedChest.Open,
                dpsVisible=OfflineTools.OfflineToolkit.Settings.DpsVisible },
            runtimePatches = new { RoomPatches.Installed, RoomPatches.Skipped },
            partyTravel = new { partyTravel.Completed, partyTravel.Busy, partyTravel.Message, partyTravel.Error },
            monolithParty=new {monolithParty.Entries,monolithParty.Restores,monolithParty.RewardGrants,monolithParty.Busy,monolithParty.Borrowing,monolithParty.Message,monolithParty.Error,monolithParty.ObjectiveError},
            fortuneSmoke=OfflineTools.OfflineToolkitSmoke.FortuneObservations,
            pickupBoundarySmoke=OfflineTools.OfflineToolkitSmoke.PickupObservations,
            characterSetup = OfflineTools.NewCharacterBoost.Diagnostics,
            partyHealthScaling = false,
            encounterDiagnostics=worldSync.EncounterDiagnostics,encounterDevelopment=encounterCatalogue.Diagnostics,sharedStatus=NativePartyStatus.Diagnostics,
            sourceOwnedStatus=new {NativeAilmentSynchronization.ReportsSent,NativeAilmentSynchronization.ReportsRejected,
                NativeAilmentSynchronization.Error,damagePolicy=NativePartyStatus.Enabled?"shared native Host status/DoT; direct hits use source-calculated damage":"source-owned damage; shared visual experiment only"},
            featurePerformance = RuntimePerformance.Read(),
            callbackPerformance=new {update=updateTiming.Read(),lateUpdate=lateTiming.Read(),reportWriterError=reportWriter.Error,
                gc0=GC.CollectionCount(0),gc1=GC.CollectionCount(1),gc2=GC.CollectionCount(2)},
            cosmeticErrors = equipmentReplicas.Where(pair=>pair.Value.CosmeticError!="").Select(pair=>new {playerId=pair.Key,reason=pair.Value.CosmeticError}).ToArray(),
            nativeGiftMenu = new { NativeLanGiftMenuPatch.Added, NativeLanGiftMenuPatch.Error },
            autoPlay=autoPlay.Diagnostics,
            arcaneMovement=new {OfflineTools.OfflineArcaneMovementPatch.Applications},
            archmage=new {OfflineTools.OfflineArchmageCastPatch.Casts,OfflineTools.OfflineArchmageRefundPatch.Refunds,
                OfflineTools.OfflineArchmageRefundPatch.LastMaximumMana,OfflineTools.OfflineArchmageRefundPatch.LastRequestedRefund},
            lanGift = new { gift.Status, gift.Error, gift.Sent, gift.Received, gift.Pending, gift.Busy },
            sharedChest = sharedChest?.Diagnostics,
            localCharacterReadiness = new { monolithParty.GameplayReady,monolithParty.SharedWorldReady,monolithParty.GuestSession },
            roomLadder = new { NativeRoomLadder.Error },
            weaverImprints = OfflineTools.WeaverImprintDiagnostics.Snapshot(),
            meteorNodes = new { OfflineTools.MeteorNodeRuntime.TreeUpdates, OfflineTools.MeteorNodeRuntime.CastAttempts, OfflineTools.MeteorNodeRuntime.CastUpdates,
                OfflineTools.MeteorNodeRuntime.MissingTrees, OfflineTools.MeteorNodeRuntime.ManaAdjustments,
                OfflineTools.MeteorNodeRuntime.LastLargeMeteorChance, OfflineTools.MeteorNodeRuntime.LastFrostClawCount },
            aggressiveMinions = new { OfflineTools.AggressiveMinionRange.ExpandedAcquisitions, OfflineTools.AggressiveMinionRange.ExpandedTargetSearches },
            avatarCaptureError = ClassVisualReplica.CaptureError,
            summons=new {local=summonCapture.Count,omitted=summonCapture.Omitted,captureError=summonCapture.Error,remote=summonReplicas.Count,renderError=summonReplicas.Error},
            enemyHostSync = worldSync.Status, fullCoop = false, adapterSmokePassed, worldSmokePassed, guiReported, classSmokeCompleted, classSmokePassed,
            worldDiagnostics = new { worldSync.MissingAlive, worldSync.MissingDead, worldSync.ForwardedHits, worldSync.ProcessedHits,
                worldSync.DamageConfirmedHits, worldSync.ConfirmedHealthLoss, worldSync.CapturedDeaths, worldSync.CreatedHostEnemies,
                worldSync.PendingHostEnemies, worldSync.SuppressedLocalEnemies, worldSync.SpawnFailures, hitHealthAudits = worldSync.HitAudits.ToArray() },
            sessionStatus = session?.Status, sessionPort = session?.Port,
            network=session?.NetworkStats,
            localScene = currentScene,
            hostWorld = session == null ? null : new { session.WorldInfo.Scene, session.WorldInfo.Epoch, session.WorldInfo.Revision, enemyCount = session.WorldInfo.EnemyCount },
            participants = session?.Players.Select(player => new { player.Id, player.Name, player.Scene, player.Available,player.StashReady,
                player.Cooperative,player.EchoRunId,player.GiftGroup,hasGiftIdentity=player.GiftIdentity!="",hasCharacter=player.CharacterId!="",
                animationLayers = player.Avatar?.Animation?.Layers.Length ?? 0, animatorParameters = player.Avatar?.Animation?.Parameters.Length ?? 0,
                motion = player.Avatar?.Motion, equipmentSlots = player.Avatar?.Equipment.Length ?? 0,
                equipment = player.Avatar?.Equipment, visuals = player.Avatar?.Visuals.Length ?? 0 }).ToArray(),
            positionMarkers = "base class models or fallback capsules; not combat actors", nativeClassModels = classModels.Count,
            capsuleFallbacks = markers.Count - classModels.Count, equipmentAppearanceSync = true, equipmentNativeGameplayVerified = false, equipmentErrors = equipmentReplicas.Where(pair => pair.Value.Error != "").Select(pair => new { playerId = pair.Key, reason = pair.Value.Error }).ToArray(), skillEventTransport = true,
            skillEventsSent = SkillCapture.Sent, skillEventsReceived = skillsReceived, lastRemoteSkill,
            nativeSkillAnimations = animationReplicas.Values.Sum(replica => replica.SkillAnimations),
            nativeCastSnapshotsApplied = animationReplicas.Values.Sum(replica => replica.CastSnapshotsApplied),
            nativeVisualsCaptured = NativeSkillVisualCapture.Captured, nativeVisualCaptureError = NativeSkillVisualCapture.Error,
            nativeVisualSnapshotsPublished = NativeSkillVisualCapture.SnapshotsPublished,
            nativeVisualInvalidSnapshots = NativeSkillVisualCapture.InvalidSnapshots,
            nativePooledObjectsObserved = NativeSkillVisualCapture.PooledObjectsObserved,
            nativeLocalVisualSnapshotsPublished = NativeSkillVisualCapture.LocalSnapshotsPublished,
            nativeLastLocalVisualSnapshot = NativeSkillVisualCapture.LastLocalSnapshot,
            nativeRenderedPartsCaptured = NativeSkillVisualCapture.RenderedPartsCaptured,
            nativeParticleValuesCaptured = NativeSkillVisualCapture.ParticleValuesCaptured,
            nativeOwnedMinionVisualsCaptured = NativeSkillVisualCapture.MinionVisualsCaptured,
            nativeVisualProfiles = NativeSkillVisualCapture.Profiles,
            nativeVisualCommands = new { NativeVisualEvents.Sent, NativeVisualEvents.Received, NativeVisualEvents.Played, NativeVisualEvents.Dropped, NativeVisualEvents.Error,
                NativeVisualEvents.HookCalls, NativeVisualEvents.UnownedCalls, NativeVisualEvents.NoPrefabCalls, NativeVisualEvents.DeferredCalls, NativeVisualEvents.DeferredOwned },
            worldCostBreakdown = worldSync.Costs,
            nativeAbilityObjectsObserved = NativeSkillVisualCapture.ObjectsObserved,
            nativeGuestTargets = guestCombat.Count, guestCombatHooksInstalled = guestCombat.HooksInstalled,
            retiredGuestTargets = NativeGuestCombat.RetiredCount,
            hudAuthority = new { hostCanEdit = HudAuthority.CanEdit, hostRevision = session?.HudRevision, HudAuthority.AppliedRevision, HudAuthority.Error },
            guestDamageSent = guestCombat.Sent, guestDamageApplied = guestCombat.Applied,
            guestCombatError = guestCombat.Error,
            lanFrameAverageMs = frameSamples == 0 ? 0 : frameTotalMs / frameSamples, lanFrameMaxMs = frameMaxMs,
            lanFrameSamples = frameSamples,
            nativeNullableEmptyFields = NativeNullable.EmptyFieldReads, nativeNullableValueFields = NativeNullable.ValueFieldReads,
            nativeNullableGetterFallbacks = NativeNullable.GetterFallbacks,
            observedGameFps = gameFrameSeconds > 0 ? frameSamples / gameFrameSeconds : 0,
            observedGameFrameAverageMs = frameSamples > 0 ? gameFrameSeconds * 1000 / frameSamples : 0,
            worldFramesSent = session?.WorldFramesSent ?? 0, worldFramesOmitted = session?.WorldFramesOmitted ?? 0,
            lanStages = new[] { "publish", "world", "combat", "remoteRenderingAndParty" }.Select((name, i) =>
                new { name, averageMs = frameSamples == 0 ? 0 : stageTotals[i] / frameSamples, maxMs = stageMax[i] }).ToArray(),
            experimentalGuestCombat = settings.ExperimentalGuestCombat,
            guestTargetScene = guestCombat.TargetScene, guestTargetRegistrationVerified = guestCombat.RegistrationVerified,
            nativeGlobalVisualsObserved = NativeSkillVisualCapture.GlobalObjectsObserved,
            nativeVisualReplicasCreated = skillVisualReplicas.Values.Sum(replica => replica.Created),
            nativeVisualDiagnostics = skillVisualReplicas.Select(pair => new { playerId = pair.Key, pair.Value.Received, pair.Value.Created, pair.Value.Failed, pair.Value.LastSnapshot, pair.Value.LastCreated,
                pair.Value.ParticleFramesApplied, pair.Value.ParticleValuesApplied, pair.Value.PresentationScriptsRetained }).ToArray(),
            nativeVisualErrors = skillVisualReplicas.Where(pair => pair.Value.Error != "").Select(pair => new { playerId = pair.Key, reason = pair.Value.Error }).ToArray(),
            enemyAnimationUpdates = worldSync.EnemyAnimationUpdates, enemyAnimationError = worldSync.EnemyAnimationError,
            enemyPresentation = worldSync.EnemyPresentationDiagnostics,
            enemyVisualsCreated = worldSync.EnemyVisualsCreated,
            sceneEventsApplied = worldSync.SceneEventsApplied, sceneGraphsCaptured = worldSync.SceneGraphsCaptured,
            sceneEventError = worldSync.SceneEventError,
            guestProximityActivations = worldSync.GuestProximityActivations, mapTriggersApplied = worldSync.MapTriggersApplied,
            mapTriggerError = worldSync.MapTriggerError, inventoryDropsObserved = LanDroppedItems.Observed, inventoryDropError = LanDroppedItems.Error,
            nativePartyMembers = nativeParty.Bound, nativePartyError = nativeParty.Error,
            nativePeerHealthBars = peerHealthBars.Count, nativePeerHealthBarError = peerHealthBars.Error,
            mapObjectsCaptured = worldSync.MapObjectsCaptured, mapObjectsApplied = worldSync.MapObjectsApplied, mapObjectError = worldSync.MapObjectError,
            bossHudId = worldSync.BossHudId, bossHudError = worldSync.BossHudError,
            skillNativeReplay = false, privateRewardDispatch = settings.GuestPrivateRewards,
            nativeDeathsDispatched = worldSync.NativeDeathsDispatched, nativeRewardCallbacks = worldSync.NativeRewardCallbacks,
            guestExperienceGranted = worldSync.GuestExperienceGranted, nativeRewardCampaignVerified = false
        });
    }

    public override void OnSceneWasLoaded(int buildIndex,string sceneName) => OfflineTools.OfflineToolkit.SceneChanged();
    public override void OnDeinitializeMelon() { autoPlay.Dispose();updateChecker.Dispose();OfflineTools.OfflineToolkit.Dispose(); nativeSessionProbe?.Dispose(); nativeAssetProbe?.Dispose(); StopRoom();reportWriter.Dispose(); }
}

public sealed class LanSettings
{
    public bool ExperimentalGuestCombat { get; set; }
    public bool HostOwnedEnemies { get; set; }
    public bool ExperimentalClassModels { get; set; }
    public bool ExperimentalNativeRewards { get; set; }
    public bool GuestPrivateRewards { get; set; }
    public bool ProbeRemoteCharacter { get; set; }
    public bool AutoStart { get; set; }
    public string Role { get; set; } = "Host";
    public string Name { get; set; } = "Traveller";
    public string HostIp { get; set; } = "192.168.1.100";
    public int Port { get; set; } = 7777;
    public string RoomCode { get; set; } = "";
}
