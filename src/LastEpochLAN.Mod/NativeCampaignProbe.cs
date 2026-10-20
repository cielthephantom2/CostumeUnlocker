using Il2CppCysharp.Threading.Tasks;
using Il2CppLE.Data;
using Il2CppLE.Services;
using Il2CppLE.Services.Login;
using Il2CppLE.Services.Models;
using Il2CppLE.Networking;
using Il2CppOperationResult;
using UnityEngine;

namespace LastEpochLAN.Mod;

// Only enabled by the QA command line, after NativeAssetProbe has verified the
// private platform save paths. Creates a disposable offline character via the
// game's offline service; never selects an existing user character.
internal sealed class NativeCampaignProbe
{
    private readonly OfflineUserService user = new();
    private UniTask<Result<UserIdentity, UserServiceError>>.Awaiter login;
    private UniTask<IGameplayService>.Awaiter service;
    private UniTask<CharacterData>.Awaiter creation;
    private UniTask<IGlobalDataService>.Awaiter giftGlobal;
    private UniTask.Awaiter startPlay;
    private UniTask.Awaiter initializeServices;
    private UniTask.Awaiter characterSelect;
    private CharacterData character;
    private IGameplayService gameplay;
    private int stage;
    private float deadline;
    private float nextStateReport;
    private Il2Cpp.Actor spawnedEnemy;
    private bool spawnAttempted;
    private NativeStatusCampaignSmoke statusSmoke;
    private NativeEncounterIsolationSmoke isolation;
    private NativeSharedChestProbe sharedChest;
    private NativeDisintegrateProbe disintegrate;
    internal void DrawChest()=>sharedChest?.Draw();
    private bool toolkitCharacterQueued;
    internal object Result { get; private set; }
    internal bool Finished => Result != null;

    internal NativeCampaignProbe()
    {
        try
        {
            if (Il2Cpp.PlayerFinder.getPlayerActor() != null) throw new InvalidOperationException("QA campaign requires an empty sandbox.");
            Application.runInBackground=true;
            initializeServices = ServiceProvider.InitializeServices().GetAwaiter();
            Advance(-1);
        }
        catch (Exception ex) { Fail(ex); }
    }

    internal void Tick()
    {
        if (Finished) return;
        try
        {
            switch (stage)
            {
                case -1:
                    if (initializeServices.IsCompleted)
                    {
                        initializeServices.GetResult();
                        ServiceProvider._UserService = user.Cast<IUserService>();
                        login = user.AutoLoginAsync(Il2CppSystem.Threading.CancellationToken.None).GetAwaiter(); Advance(0);
                    }
                    break;
                case 0:
                    if (login.IsCompleted)
                    {
                        login.GetResult();
                        if (user.CurrentUser == null) throw new InvalidOperationException("Native offline user identity was not initialized.");
                        if (PlayerStore._instance == null) PlayerStore.Initialize();
                        PlayerStore._instance.UserService_CurrentUserChanged(user, user.CurrentUser);
                        if (!ServiceProvider.NetworkServiceGroupAvailable) ServiceProvider.InitializeNetworkServiceEtAl();
                        // StartPlay uses the normal character-select scene's player
                        // container and cosmetic services while preparing the actor.
                        // A rendered sandbox also has the normal shell boot.
                        // Wait for it instead of racing a second scene transition.
                        if(!Application.isBatchMode){Advance(8);break;}
                        characterSelect = Il2CppClientAppState.ClientStateManager.ChangeState(Il2CppClientAppState.ClientAppStateType.CharacterSelect).GetAwaiter(); Advance(6);
                    }
                    break;
                case 8:
                    if(Time.unscaledTime>=nextStateReport)
                    {
                        nextStateReport=Time.unscaledTime+2;
                        File.WriteAllText(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),"UserData","LastEpochLAN.render-shell-state.json"),System.Text.Json.JsonSerializer.Serialize(new{
                            stage,state=Il2CppClientAppState.ClientStateManager.CurrentClientAppStateType.ToString(),
                            transitioning=Il2CppClientAppState.ClientStateManager._sceneManager?._isTransitioning}));
                    }
                    if(Il2CppClientAppState.ClientStateManager._sceneManager?._isTransitioning==true)break;
                    if(Il2CppClientAppState.ClientStateManager.CurrentClientAppStateType==Il2CppClientAppState.ClientAppStateType.CharacterSelect)
                    {service=ServiceProvider.GameplayService.GetAwaiter();Advance(1);}
                    else if(Il2CppClientAppState.ClientStateManager.CurrentClientAppStateType==Il2CppClientAppState.ClientAppStateType.Login)
                    {
                        // Normal shell has finished SystemLoading -> Login.
                        // Enter private offline character select only now.
                        ServiceProvider._UserService=user.Cast<IUserService>();
                        PlayerStore._instance.UserService_CurrentUserChanged(user,user.CurrentUser);
                        characterSelect=Il2CppClientAppState.ClientStateManager.ChangeState(Il2CppClientAppState.ClientAppStateType.CharacterSelect).GetAwaiter();Advance(6);
                    }
                    break;
                case 6:
                    if (characterSelect.IsCompleted)
                    { characterSelect.GetResult(); service = ServiceProvider.GameplayService.GetAwaiter(); Advance(1); }
                    break;
                case 1:
                    if (service.IsCompleted)
                    {
                        gameplay = service.GetResult();
                        if (Environment.GetCommandLineArgs().Contains("--lelan-gift-reload"))
                        { giftGlobal = ServiceProvider.GlobalDataService.GetAwaiter(); Advance(7); break; }
                        var arguments = new CharacterCreateFunctionArguments { CharacterName = "LANQA" + Guid.NewGuid().ToString("N")[..6],
                            ClassId = Il2Cpp.CharacterClassID.Mage, Cycle = Cycle.Legacy, IsHardcore = false,
                            IsSoloChallenge = false, IsSoloCharacterChallenge = false };
                        creation = gameplay.CreateOfflinePlayerCharacter(user.CurrentUser, arguments, Il2CppSystem.Threading.CancellationToken.None).GetAwaiter(); Advance(2);
                    }
                    break;
                case 7:
                    if (giftGlobal.IsCompleted)
                    {
                        string manifest = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath), "UserData", "LastEpochLAN.gift-qa-manifest.json");
                        using var document = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(manifest));
                        string id = document.RootElement.GetProperty("characterId").GetString();
                        var offlineCharacters = new OfflineCharacterService(giftGlobal.GetResult(), ServiceProvider.DataStoreCache);
                        creation = offlineCharacters.GetCharacter(user.CurrentUser, id, Il2CppSystem.Threading.CancellationToken.None).GetAwaiter(); Advance(2);
                    }
                    break;
                case 2:
                    if (creation.IsCompleted)
                    {
                        character = creation.GetResult();
                        if (character == null || !character.IsOffline) throw new InvalidOperationException("QA character was not offline.");
                        Il2CppEHG.Multiplayer.GameplayEnvironment.IsOnlinePlay = false;
                        startPlay = gameplay.StartPlayWithCharacter(character, Il2CppSystem.Threading.CancellationToken.None).GetAwaiter(); Advance(3);
                    }
                    break;
                case 3:
                    if (startPlay.IsCompleted)
                    {
                        startPlay.GetResult();
                        // StartPlay already signals the normal InGame transition.
                        // Reentering it exits/unloads the world that just spawned.
                        Advance(5);
                    }
                    break;
                case 5:
                    var actor = Il2Cpp.PlayerFinder.getPlayerActor();
                    var cinematic = Il2Cpp.CharacterSelectionVideoManager.instance;
                    if (cinematic != null && cinematic.IsPlaying) cinematic.EndOrSkip();
                    if (Time.unscaledTime >= nextStateReport)
                    {
                        nextStateReport = Time.unscaledTime + 2;
                        string statePath = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath), "UserData", "LastEpochLAN.campaign-state.json");
                        System.IO.File.WriteAllText(statePath, System.Text.Json.JsonSerializer.Serialize(new {
                            stage, scene = Il2Cpp.SceneList.GetCurrentSceneDetails()?.Name, actor = actor != null,
                            spawned = actor?.HasMovedToPlayerSpawn, health = actor?.health?.currentHealth,
                            selectedCharacter = actor?.characterDataTracker?.charData?.CharacterName,
                            gameState = Il2CppClientAppState.ClientStateManager.CurrentClientAppStateType.ToString() }));
                    }
                    if (actor?.characterDataTracker?.charData != null && actor.characterDataTracker.charData.CharacterName != character.CharacterName)
                        throw new InvalidOperationException("Campaign QA loaded a different character.");
                    if (actor != null && actor.HasMovedToPlayerSpawn && actor.health != null)
                    {
                        if(Environment.GetCommandLineArgs().Contains("--lelan-disintegrate-smoke"))
                        {
                            disintegrate??=new();Result=disintegrate.Tick(actor);return;
                        }
                        if(Environment.GetCommandLineArgs().Contains("--lelan-autoplay-smoke"))
                        {Result=AutoPlayRuntime.Active?.NativeSmoke(actor);return;}
                        if(Environment.GetCommandLineArgs().Contains("--lelan-loot-filter-smoke"))
                        { Result=LanLootFilters.NativeSmoke(); return; }
                        if(Environment.GetCommandLineArgs().Contains("--lelan-shared-chest-smoke"))
                        {
                            sharedChest??=new();sharedChest.Tick();if(!sharedChest.Finished)return;
                            Result=sharedChest.Result;sharedChest.Dispose();return;
                        }
                        if(Environment.GetCommandLineArgs().Contains("--lelan-omen-reward-smoke"))
                        {
                            isolation ??= new();isolation.Tick();if(!isolation.Complete)return;
                            Result=new {passed=true,nativeEncounterIsolation=isolation.Observations,scope="Private native Omen drops after removal of legacy encounter hooks; no co-op acceptance"};
                            isolation.Dispose();return;
                        }
                        if(Environment.GetCommandLineArgs().Contains("--lelan-offline-tools-smoke"))
                        {
                            if(!toolkitCharacterQueued)
                            {toolkitCharacterQueued=true;OfflineTools.NewCharacterBoost.Created(actor.characterDataTracker.charData);}
                            var boostError=OfflineTools.NewCharacterBoost.ErrorFor(actor.characterDataTracker.charData);
                            if(boostError!="") throw new InvalidOperationException("Offline new-character boost QA: "+boostError);
                            if(!OfflineTools.NewCharacterBoost.CompletedFor(actor.characterDataTracker.charData)) return;
                            var owned=OfflineTools.OfflineCosmeticCatalog.OwnedIds();
                            if(owned==null || owned.Count<100) throw new InvalidOperationException("Offline cosmetic catalogue not populated.");
                            if(!OfflineTools.OfflineToolkitSmoke.VerifyPrivatePickups(actor)) return;
                            OfflineTools.OfflineToolkitSmoke.VerifyPrivateBonuses(actor);
                            if(!OfflineTools.OfflineToolkitSmoke.VerifyPrivateDps(actor)) return;
                            if(!OfflineTools.OfflineToolkitSmoke.VerifyPrivateStatus(actor)) return;
                            Result=new {passed=true,offlineNewCharacterCompletion=true,level=actor.experienceTracker.CurrentLevel,
                                scene=actor.gameObject.scene.name,cosmeticCount=owned.Count,
                                nativeGoldPotionBones=true,nativeKeyMaterialStorage=true,toolkitHookClasses=OfflineTools.OfflineToolkitSmoke.PatchClasses,
                                bonuses=OfflineTools.OfflineToolkitSmoke.BonusObservations,
                                dps=OfflineTools.OfflineToolkitSmoke.DpsObservations,
                                sharedStatus=OfflineTools.OfflineToolkitSmoke.SharedStatusObservations,
                                scope="Private character native campaign completion, level/save/EoT, cosmetic catalog and all five pickup categories; not two-PC, actual creation UI, range click or objective validation"};
                            return;
                        }
                        if(Environment.GetCommandLineArgs().Contains("--lelan-status-smoke"))
                        {
                            statusSmoke ??= new NativeStatusCampaignSmoke();statusSmoke.Tick();
                            if(!statusSmoke.Complete) return;
                            Result=new {passed=true,nativeSourceOwnedDot=true,nativeRemoteStatusVisualsOnly=true,nativeCombinedIgnitePoison=true,nativeAggregateVfxReplacementAndCleanse=true,
                                scene=actor.gameObject.scene.name,scope="Private campaign source-owned native DoT, combined remote fire/poison presentation without gameplay duplication, expiry/cleanse; actual two-PC combat remains unverified"};
                            statusSmoke.Dispose();return;
                        }
                        var target = spawnedEnemy ?? Resources.FindObjectsOfTypeAll<Il2Cpp.Actor>().FirstOrDefault(candidate => candidate != null &&
                            !candidate.isPlayerActor && candidate.data != null && candidate.data.actorType == Il2Cpp.ActorData.Type.Normal &&
                            candidate.health != null && candidate.health.currentHealth > 20 && candidate.health.maxHealth > 20 &&
                            candidate.gameObject.scene.IsValid() && candidate.gameObject.activeInHierarchy);
                        if (target == null && !spawnAttempted)
                        {
                            spawnAttempted = true;
                            Il2Cpp.ActorData template = null;
                            foreach (var data in Il2Cpp.ActorDataList.instance().data)
                                if (data != null && data.actorType == Il2Cpp.ActorData.Type.Normal) { template = data; break; }
                            if (template == null) throw new InvalidOperationException("QA has no native normal enemy asset.");
                            spawnedEnemy = template.spawn(actor.transform.position + actor.transform.forward * 3, 0, null,
                                Il2Cpp.AbilitySync.PositionSyncType.Absolute, null, "", true, 0,
                                new Il2CppSystem.Nullable<UnityEngine.SceneManagement.Scene>(actor.gameObject.scene), true);
                            if (spawnedEnemy == null) throw new InvalidOperationException("QA native enemy creation failed.");
                            // Let native components initialize through the regular Unity lifecycle.
                            deadline = Time.unscaledTime + 15; return;
                        }
                        if (target?.health == null || target.health.maxHealth <= 0)
                        { if (Time.unscaledTime >= deadline) throw new TimeoutException("QA enemy health did not initialize."); return; }
                        object hostDamage = null;
                        if (target != null)
                        {
                            var registry = new Il2CppLE.Networking.Core.NetObjectRegistry();
                            using var projectedWorld = new NativeOfflineHostWorld(registry);
                            var projected = projectedWorld.Track(target);
                            float before = target.health.currentHealth;
                            target.health.HealthDamage(5);
                            float after = target.health.currentHealth;
                            projectedWorld.Tick();
                            byte expectedRatio = (byte)Math.Clamp((int)MathF.Round(after / target.health.maxHealth * 255), 0, 255);
                            if (after >= before || projected.healthRatio != expectedRatio)
                                throw new InvalidOperationException("Native host damage did not reduce actual HP and project the new ratio.");
                            hostDamage = new { tested = true, template = target.data.name, before, after, projectedHealthRatio = projected.healthRatio,
                                scope = "Direct native host damage and detached native projection, not a guest skill hit" };
                        }
                        Result = new { passed = true, scene = Il2Cpp.SceneList.GetCurrentSceneDetails()?.Name,
                            character = character.CharacterName, health = actor.health.currentHealth,
                            nativeGameplayActor = true, hostDamage,
                            scope = "Disposable native offline campaign bootstrap in a verified private save profile; co-op combat not yet validated" };

                    }
                    break;
            }
            if (!Finished && Time.unscaledTime >= deadline) throw new TimeoutException("Native campaign QA timed out at stage " + stage);
        }
        catch (Exception ex) { Fail(ex); }
    }

    private void Advance(int next) { stage = next; deadline = Time.unscaledTime + 90; MelonLoader.MelonLogger.Msg("Native campaign QA stage " + next); }
    private void Fail(Exception ex) { statusSmoke?.Dispose();sharedChest?.Dispose();Result = new { passed = false, stage, error = ex.ToString(), scope = "private native campaign bootstrap" }; }
}
