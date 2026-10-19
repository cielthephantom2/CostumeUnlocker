using HarmonyLib;
using LastEpochLAN.Core;
using UnityEngine;
using Actor = Il2Cpp.Actor;

namespace LastEpochLAN.Mod;

// All native object access happens on the Unity thread. Network workers only handle records.
internal sealed class WorldSynchronizer
{
    internal static WorldSynchronizer Active;
    internal LanSession Room => session;
    internal long StatusEpoch => epoch;
    internal static WorldSynchronizer StatusFixture(LanSession room,Actor actor,int id,long instance)
    {
        if(!Environment.GetCommandLineArgs().Contains("--lelan-offline-tools-smoke")) throw new InvalidOperationException("Private native status fixture required.");
        var world=new WorldSynchronizer {session=room,scene=actor.gameObject.scene.name,epoch=instance,guestWorld=room.World};
        world.actors[id]=actor;
        if(!room.IsHost) {world.replicaPointers.Add(actor.Pointer);world.replicaIds[actor.Pointer]=id;}
        return world;
    }
    internal object EncounterDiagnostics=>new {available=false,implementation="removed; rewrite in development"};
    internal Actor FindEnemy(int id)=>actors.TryGetValue(id,out var actor) ? actor : null;
    private readonly double[] costTotals = new double[5], costMax = new double[5];
    private readonly long[] costCounts = new long[5];
    private long Cost(int stage, long start)
    {
        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        double ms = (now - start) * 1000d / System.Diagnostics.Stopwatch.Frequency;
        costTotals[stage] += ms; costMax[stage] = Math.Max(costMax[stage], ms); costCounts[stage]++;
        return now;
    }
    internal object[] Costs => new[] { "mapTriggers", "actorScan", "actorRead", "sceneGraphs", "publish" }.Select((name, i) => (object)new {
        name, count = costCounts[i], averageMs = costCounts[i] == 0 ? 0 : costTotals[i] / costCounts[i], maxMs = costMax[i] }).ToArray();
    private LanSession session;
    private readonly Dictionary<int, Actor> actors = new();
    private readonly HashSet<IntPtr> replicaPointers = new();
    private readonly Dictionary<IntPtr,int> replicaIds=new();
    private readonly SourceStatusVisuals sourceStatusVisuals=new();
    private readonly HashSet<IntPtr> corpsePointers = new();
    private readonly Dictionary<int, float> animationRetryAt = new();
    private readonly Dictionary<int, EnemyState> appliedEnemyStates = new();
    private readonly Dictionary<int, BufferedEnemyPresentation> enemyPresentation = new();
    private readonly Dictionary<int, EnemyState> presentedEnemyStates = new();
    private readonly HashSet<IntPtr> animationAuthority = new();
    internal long SuppressedNativeAnimationEvents {get;private set;}
    private readonly List<EnemyState> spawnCandidates = new();
    private readonly Dictionary<int, int> hostIds = new();
    private readonly Dictionary<int, Vector3> origins = new();
    private readonly Dictionary<int, bool> navigation = new();
    private readonly Dictionary<int, ReplicaBackup> backups = new();
    private readonly HashSet<int> handledRewards = new();
    private readonly Dictionary<int, DeathReceiptOutcome> rewardResults = new();
    private readonly Dictionary<int, NativeRewardCompletion.Pending> asyncRewards = new();
    private readonly EnemyHistory history = new();
    private readonly HashSet<int> localDeaths = new();
    private readonly HashSet<int> pendingDeaths = new();
    private readonly HostEnemyReplicas hostReplicas = new();
    private readonly NativeBossHud bossHud = new();
    internal int MapObjectsCaptured => 0;
    internal long MapObjectsApplied => 0;
    internal string MapObjectError => "disabled during encounter rewrite";
    internal int BossHudId => bossHud.BoundId;
    internal string BossHudError => bossHud.Error;
    internal long GuestProximityActivations => 0;
    internal long MapTriggersApplied => 0;
    internal string MapTriggerError => "disabled during encounter rewrite";
    private readonly Dictionary<int, NativePlayerAnimationReplica> enemyAnimations = new();
    private readonly Dictionary<int, NativeSkillVisualReplica> enemyVisuals = new();
    private readonly HashSet<int> rewardCallbacks = new();
    private readonly Dictionary<int, (long Sequence, byte Animation, float Speed, float Until, int Frame)> capturedAttacks = new();
    private readonly Dictionary<int, long> playedAttacks = new();
    private long nextAttack;
    internal long NativeRewardCallbacks { get; private set; }
    internal long GuestExperienceGranted { get; private set; }
    internal long EnemyAnimationUpdates { get; private set; }
    internal long EnemyRenderFrames { get; private set; }
    internal long EnemyAnimationManualTicks => enemyAnimations.Values.Sum(driver => driver.ManualTicks);
    internal object EnemyPresentationDiagnostics => new {renderFrames=EnemyRenderFrames,active=enemyPresentation.Count,
        queued=enemyPresentation.Values.Sum(buffer=>buffer.Count),manualDriverTicks=EnemyAnimationManualTicks,
        readyAnimationOwners=animationAuthority.Count,SuppressedNativeAnimationEvents,
        bufferDelayMs=poses.Delay*1000,spawnPolicy="nearest Guest; max two native starts per Apply",nativeGameplayValidated=false};
    internal long EnemyVisualsCreated => enemyVisuals.Values.Sum(replica => replica.Created);
    internal long SceneEventsApplied => 0;
    internal int SceneGraphsCaptured => 0;
    internal string SceneEventError => "disabled during encounter rewrite";
    internal string EnemyAnimationError { get; private set; } = "";
    internal bool HostOwnedEnemies;
    private bool activeHostOwnedEnemies;
    internal int CreatedHostEnemies => hostReplicas.CreatedCount;
    internal int PendingHostEnemies => hostReplicas.PendingCount;
    internal int SuppressedLocalEnemies => hostReplicas.HiddenCount;
    internal object SpawnFailures => hostReplicas.Failures.Select(pair => new { enemyId = pair.Key, reason = pair.Value }).Take(16).ToArray();
    internal bool NativeRewardsEnabled;
    internal long NativeDeathsDispatched { get; private set; }
    internal int MissingAlive { get; private set; }
    internal int MissingDead { get; private set; }
    internal long ForwardedHits { get; private set; }
    internal long ProcessedHits { get; private set; }
    internal long DamageConfirmedHits { get; private set; }
    internal double ConfirmedHealthLoss { get; private set; }
    internal readonly Queue<HitHealthAudit> HitAudits = new();
    internal long CapturedDeaths { get; private set; }
    private long epoch;
    private long revision;
    private long appliedRevision = -1;
    private string scene = "";
    private int sceneHandle;
    private int nextId;
    private float nextTick;
    private float nextScan;
    private Actor[] scanned = Array.Empty<Actor>();
    private WorldState lastPublished;
    private WorldState guestWorld;
    private readonly NativeEnemyPoseBlend poses = new();
    internal WorldState GuestWorld => guestWorld;
    private float nextWorldHeartbeat;
    internal string Status { get; private set; } = "Waiting for gameplay";

    internal void Tick(LanSession room, string zone)
    {
        if (session != room) { Reset(); session = room; Active = this; }
        if (activeHostOwnedEnemies != HostOwnedEnemies)
        { Restore(); actors.Clear(); appliedRevision = -1; activeHostOwnedEnemies = HostOwnedEnemies; }
        var loadedScene=UnityEngine.SceneManagement.SceneManager.GetSceneByName(zone);
        int currentHandle=loadedScene.IsValid() && loadedScene.isLoaded ? loadedScene.handle : 0;
        if (zone != scene || currentHandle!=0 && sceneHandle!=0 && currentHandle!=sceneHandle)
        {
            Restore(); actors.Clear(); hostIds.Clear(); origins.Clear(); history.Clear(); localDeaths.Clear(); pendingDeaths.Clear(); nextId = 0; scene = zone;
            epoch++; appliedRevision = -1;
            sceneHandle=currentHandle;
        }
        else if(currentHandle!=0) sceneHandle=currentHandle;
        bool due = Time.unscaledTime >= nextTick;
        if (due)
        {
            nextTick = Time.unscaledTime + 0.1f;
            long triggerStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            Cost(0, triggerStarted);
        }
        if (room.IsHost) { NativePartyStatus.Tick(this); if (due) Capture(); }
        else
        {
            var info = room.WorldInfo;
            bool changed = guestWorld == null || guestWorld.Epoch != info.Epoch || guestWorld.Revision != info.Revision || guestWorld.Scene != info.Scene;
            if (changed) guestWorld = room.World;
            // Consume arrivals independently of the host's 10 Hz sampling
            // phase, while keeping native asset/spawn retries at 10 Hz.
            if (changed || due) Apply(guestWorld);
            if(due && guestWorld.Scene==scene) NativeAilmentSynchronization.GuestTick(this);
            NativePartyStatus.Tick(this);
        }
    }

    internal void RenderGuestPoses()
    {
        poses.Tick();
        foreach(var pair in enemyPresentation)
        {
            if(!actors.TryGetValue(pair.Key,out var actor) || actor==null || IsNativeCorpse(actor)) continue;
            try
            {
                if(pair.Value.TryTake(poses.RenderTime,out var state))
                {
                    presentedEnemyStates.TryGetValue(pair.Key,out var previous);
                    ApplyEnemyPresentation(actor,state,previous);
                    presentedEnemyStates[pair.Key]=state;
                }
                if(presentedEnemyStates.TryGetValue(pair.Key,out var shown))
                {
                    if(shown.Motion!=null && enemyAnimations.TryGetValue(pair.Key,out var driver))
                    {
                        // Retry the held state after asynchronous controller load;
                        // visual playback must not depend on another network change.
                        driver.Apply(shown.Motion,poses.IsMoving(pair.Key),false);
                        driver.TickPresentation();
                        if(driver.Ready) ClaimAnimation(actor,shown);
                    }
                    else if(shown.Animation!=null && !animationAuthority.Contains(actor.Pointer))
                    {
                        var animator=NativeAnimation.Find(actor);
                        if(animator?.runtimeAnimatorController!=null && animator.layerCount>0)
                        {NativeAnimation.Apply(animator,shown.Animation,false,false);ClaimAnimation(actor,shown);}
                    }
                }
            }
            catch(Exception ex) {EnemyAnimationError=ex.GetBaseException().Message;}
        }
        foreach (var visual in enemyVisuals.Values) visual.Tick();
        EnemyRenderFrames++;
    }

    private void ClaimAnimation(Actor actor,EnemyState shown)
    {
        animationAuthority.Add(actor.Pointer);
        var visual=actor.GetActorSync()?.actorVisuals;
        if(visual!=null && shown.Motion!=null && shown.Motion.State!=(int)Il2Cpp.AnimationStates.Emerging &&
            visual.renderersCurrentlyDisabledAwaitingEmerge)
        {visual.OnStartedEmerging();visual.OnEmerge();}
    }

    internal bool SuppressNativeAnimation(Il2Cpp.ActorSync sync,Il2Cpp.ActorSyncAnimationBuffer buffer)
    {
        if(session?.IsHost!=false || sync==null) return false;
        var actor=sync.GameplayActor;
        if(actor==null || !animationAuthority.Contains(actor.Pointer) || IsNativeCorpse(actor)) return false;
        SuppressedNativeAnimationEvents+=buffer.buffer?.Count ?? 0;
        buffer.Clear();return true;
    }

    private static bool Enemy(Actor actor) => actor != null && actor.health != null && actor.data != null &&
        !actor.isPlayerActor && !actor.IsMinion() &&
        (actor.data.actorType == Il2Cpp.ActorData.Type.Normal || actor.data.actorType == Il2Cpp.ActorData.Type.MiniBoss || actor.data.actorType == Il2Cpp.ActorData.Type.Boss);

    private Actor[] Scan()
    {
        if (Time.unscaledTime >= nextScan)
        {
            var manager = Il2Cpp.ActorManager.instance;
            nextScan = Time.unscaledTime + (manager == null ? 5 : 0.25f);
            if (manager?.actors == null)
                scanned = UnityEngine.Object.FindObjectsOfType<Actor>().Where(Enemy).ToArray();
            else
            {
                // The game already maintains its live actor registry. Avoid an
                // engine-wide object search during every periodic world scan.
                var live = new List<Actor>();
                foreach (var group in manager.actors)
                {
                    var list = group?.actors?._list;
                    if (list == null) continue;
                    for (int i = 0, count = list.Count; i < count; i++)
                    {
                        var actor = list[i];
                        if (Enemy(actor) && actor.gameObject.activeInHierarchy && actor.gameObject.scene.isLoaded) live.Add(actor);
                    }
                }
                scanned = live.ToArray();
            }
        }
        return scanned.Where(actor => actor != null).ToArray();
    }

    private void Capture()
    {
        if (scene == "") { session.PublishWorld(new WorldState { Epoch = epoch, Revision = ++revision }); return; }
        long captureStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        Actor[] found = Scan();
        captureStarted = Cost(1, captureStarted);
        foreach (Actor actor in found)
        {
            if (actor.isDying(false) || actor.health.currentHealth <= 0) continue;
            int nativeId = actor.GetInstanceID();
            if (!hostIds.TryGetValue(nativeId, out int id)) { hostIds[nativeId] = id = ++nextId; origins[id] = actor.position(); }
            actors[id] = actor;
        }
        sourceStatusVisuals.BeginEpoch(epoch);
        var participants=session.PlayerPositions.Where(p=>p.Available && p.Scene==scene).Select(p=>p.Id).ToHashSet();
        sourceStatusVisuals.Retain(actors.ContainsKey,participants);
        while(session.TryTakeStatusVisuals(out var report))
            if(report.Epoch==epoch && participants.Contains(report.Source) && actors.TryGetValue(report.EnemyId,out var target) && target!=null && target.health?.currentHealth>0)
                if(NativeAilmentSynchronization.ValidateCatalogue(report)) sourceStatusVisuals.Observe(report,LanSession.MonotonicSeconds);
        while (!NativePartyStatus.Enabled && session.TryTakeHit(out var hit))
            ApplyHitRequest(hit);
        int visualBudget = 64;
        foreach (var pair in actors.ToArray())
        {
            Actor actor = pair.Value;
            if (actor == null) { actors.Remove(pair.Key); continue; }
            if (actor.health == null || actor.data == null || actor.health.maxHealth <= 0) continue;
            // A scripted despawn may deactivate a live boss without killing it.
            // Stop advertising that actor; native deaths retain their tombstone.
            if (!actor.gameObject.activeInHierarchy && actor.health.currentHealth > 0 && !actor.isDying(false))
            { NativeAilmentSynchronization.Forget(actor);actors.Remove(pair.Key); continue; }
            var state = ReadActor(pair.Key, actor, actor.isDying(false) || actor.health.currentHealth <= 0,Math.Min(8,visualBudget));
            visualBudget -= state.Visuals.Length;
            history.Observe(state);
            if (state.Dead) {NativeAilmentSynchronization.Forget(actor);actors.Remove(pair.Key);}
        }
        var completedDeaths = session.RetireAcknowledgedDeaths(epoch);
        history.RetireDeaths(completedDeaths.Contains);
        var states = history.Snapshot(actors.Where(pair => pair.Value != null).Select(pair => pair.Key));
        captureStarted = Cost(2, captureStarted);
        int bossId = bossHud.Capture(actors, session.PlayerPositions.ToArray(), scene);
        captureStarted = Cost(3, captureStarted);
        var candidate = new WorldState { SampleTime = LanSession.MonotonicSeconds, Scene = scene, Epoch = epoch, Revision = revision + 1, Enemies = states,
            EchoRunId=LanMonolithParty.Active?.HostWorldRun(scene)??"",
            AcceptsResolvedAilments=NativePartyStatus.Enabled,
            BossId = bossId };
        bossHud.Apply(candidate, actors);
        if (Time.unscaledTime >= nextWorldHeartbeat || !WorldContent.Same(lastPublished, candidate))
        {
            revision++; lastPublished = candidate; nextWorldHeartbeat = Time.unscaledTime + 1;
            session.PublishWorld(candidate);
        }
        Status = $"Host world: {states.Length} enemies, revision {revision}";
        Cost(4, captureStarted);
    }

    private EnemyState ReadActor(int id, Actor actor, bool dead,int visualBudget=8)
    {
        if (!dead) NativeGuestCombat.Active?.RefreshActivation(actor);
        Vector3 p = actor.position(), origin = origins[id];
        var motion = dead ? null : NativePlayerAnimationReplica.Capture(actor);
        var animation = !dead && motion == null ? NativeAnimation.Capture(actor, true, 24) : null;
        capturedAttacks.TryGetValue(id, out var attack);
        if (motion == null && animation == null && !dead && attack.Sequence > 0 && Time.unscaledTime < attack.Until)
            motion = new NativeMotionState { State = (int)Il2Cpp.AnimationStates.Attacking,
                AnimationNumber = attack.Animation, AnimationSpeed = attack.Speed };
        var ailments=dead ? Array.Empty<AilmentCount>() : sourceStatusVisuals.Merge(id,NativeAilmentSynchronization.Read(actor),LanSession.MonotonicSeconds);
        NativeAilmentSynchronization.Present(actor,ailments);
        return new EnemyState { Id = id, Template = actor.data.name, X = p.x, Y = p.y, Z = p.z,
            Ailments=ailments,
            StatusModifiers=dead || !NativePartyStatus.Enabled ? Array.Empty<StatusStat>() : NativePartyStatusMirror.Read(actor),
            StatusCounters=dead || !NativePartyStatus.Enabled ? new() : NativePartyStatusMirror.Counters(actor),
            FreezeMaterial = dead ? 0 : NativeEnemyControl.FreezeMaterial(actor), Stunned = !dead && NativeEnemyControl.IsStunned(actor),
            VisualPosition = actor.GetActorSync()?.actorVisuals is { } av ? new VisualPoint {X=av.transform.position.x,Y=av.transform.position.y,Z=av.transform.position.z} : null,
            Boss = actor.data.actorType == Il2Cpp.ActorData.Type.Boss, Ward = Mathf.Clamp(actor.protection?.currentWard ?? 0, 0, 100000000),
            SpawnX = origin.x, SpawnY = origin.y, SpawnZ = origin.z, Yaw = actor.transform.eulerAngles.y,
            MaxHealth = actor.health.maxHealth, Health = dead ? 0 : Mathf.Clamp(actor.health.currentHealth, 0, actor.health.maxHealth), Dead = dead,
            Animation = animation, Motion = motion, AttackSequence = attack.Sequence,
            Visuals = dead ? Array.Empty<SkillVisualState>() : NativeSkillVisualCapture.CaptureFor(actor,visualBudget) };
    }

    internal void ApplyHitRequest(HitRequest hit)
    {
        if (hit.Epoch != epoch || hit.Ailment!=null || !actors.TryGetValue(hit.EnemyId, out var actor) || actor == null || actor.isDying(false) || !actor.health.damageable) return;
        if (!session.TryGetPlayerLocation(hit.PlayerId,out var player) || !player.Available || player.Dead && !hit.SourceDot || player.Scene != scene ||
            Vector3.Distance(actor.position(), new Vector3(player.X, player.Y, player.Z)) > 100) return;
        if (hit.ControlType >= 0) { NativeEnemyControl.ApplyRequest(actor, hit); return; }
        // Direct hit damage is still calculated on its source PC against the
        // mirrored target modifiers; native DoT is calculated only by Host.
        float before = actor.health.currentHealth;
        if (hit.Cull) actor.health.HealthDamageCull(hit.Damage, hit.CullPercent, hit.FlatCull);
        else actor.health.HealthDamage(hit.Damage);
        float after = actor.health.currentHealth;
        ProcessedHits++;
        float loss = Math.Max(0, before - after);
        if (loss > 0) { DamageConfirmedHits++; ConfirmedHealthLoss += loss; }
        HitAudits.Enqueue(new HitHealthAudit(hit.PlayerId, hit.EnemyId, epoch, hit.Damage, before, after));
        while (HitAudits.Count > 32) HitAudits.Dequeue();
        if (actor.health.currentHealth <= 0 && !actor.health.CannotDie() && !actor.isDying(false) && actor.dying != null) actor.dying.die(true);
    }

    internal bool CanCaptureEnemyVisuals(Actor actor) => session != null && session.IsHost && Enemy(actor);

    internal void CaptureEnemyAttack(Actor actor, byte animation, float speed)
    {
        if (!CanCaptureEnemyVisuals(actor) || animation > 63 || !float.IsFinite(speed) || speed <= 0 || speed > 100) return;
        int nativeId = actor.GetInstanceID();
        if (!hostIds.TryGetValue(nativeId, out int id))
        { hostIds[nativeId] = id = ++nextId; origins[id] = actor.position(); }
        if (capturedAttacks.TryGetValue(id, out var previous) && previous.Frame == Time.frameCount && previous.Animation == animation && previous.Speed == speed) return;
        capturedAttacks[id] = (++nextAttack, animation, speed, Time.unscaledTime + 0.8f / speed, Time.frameCount);
    }

    internal void ObserveRewardCallback(Actor actor)
    {
        if (!IsNativeCorpse(actor)) return;
        if (rewardCallbacks.Add(actor.GetInstanceID())) NativeRewardCallbacks++;
    }

    internal void CaptureDeath(Actor actor)
    {
        if (session == null || !session.IsHost || scene == "" || !Enemy(actor) || actor.health.maxHealth <= 0) return;
        int nativeId = actor.GetInstanceID();
        if (!hostIds.TryGetValue(nativeId, out int id))
        { hostIds[nativeId] = id = ++nextId; origins[id] = actor.position(); }
        actors[id] = actor;
        history.Observe(ReadActor(id, actor, true));
        CapturedDeaths++;
    }

    private void Apply(WorldState world)
    {
        if (scene == "" || world.Scene != scene)
        { Restore(); actors.Clear(); appliedRevision = -1; Status = "Enter host zone: " + world.Scene; return; }
        if (epoch != world.Epoch)
        { Restore(); actors.Clear(); localDeaths.Clear(); pendingDeaths.Clear(); handledRewards.Clear(); rewardResults.Clear(); epoch = world.Epoch; appliedRevision = -1; }
        poses.Observe(world.SampleTime, session.WorldArrivalSeconds);
        bossHud.Apply(world, actors);
        // Native components can become ready without a new host packet.
        // Native asset availability and procedural guest spawns change independently
        // of the network revision. Continue suppression and creation retries.
        foreach (var delivery in asyncRewards.ToArray())
        {
            var outcome = delivery.Value.Outcome;
            if (outcome == null && Time.unscaledTime < delivery.Value.Deadline) continue;
            rewardResults[delivery.Key] = outcome ?? DeathReceiptOutcome.Failed;
            if (outcome == null) MelonLoader.MelonLogger.Warning($"Guest reward completion timed out for enemy {delivery.Key}; held without retry.");
            NativeRewardCompletion.Release(delivery.Value); asyncRewards.Remove(delivery.Key);
        }
        foreach (var result in rewardResults.ToArray())
            if (session.ReportDeath(new DeathReceipt(epoch, result.Key, result.Value))) rewardResults.Remove(result.Key);
        if (world.Revision == appliedRevision && pendingDeaths.Count == 0 &&
            (!HostOwnedEnemies || (hostReplicas.PendingCount == 0 && MissingAlive == 0))) return;
        foreach (int id in world.RewardEnemyIds)
        {
            if (handledRewards.Contains(id)) continue;
            if (NativeRewardsEnabled) pendingDeaths.Add(id);
            else { handledRewards.Add(id); rewardResults[id] = DeathReceiptOutcome.Skipped; }
        }
        if (!NativeRewardsEnabled) pendingDeaths.Clear(); // Enabling later must not grant earlier kills.
        var candidates = Scan();
        hostReplicas.ObserveTemplates(candidates);
        hostReplicas.BeginUpdate();
        if (HostOwnedEnemies) hostReplicas.HideLocal(candidates);
        if(HostOwnedEnemies)
        {
            // Admit nearby actors first without changing world identities,
            // reward eligibility or the existing two-spawn native work budget.
            spawnCandidates.Clear();
            foreach(var state in world.Enemies)
                if((!state.Dead || pendingDeaths.Contains(state.Id)) &&
                    (!actors.TryGetValue(state.Id,out var existing) || existing==null)) spawnCandidates.Add(state);
            var local=Il2Cpp.PlayerFinder.getPlayerActor();
            var position=local!=null ? local.position() : Vector3.zero;
            spawnCandidates.Sort((a,b)=>EnemyAdmission.Compare(a,b,position.x,position.y,position.z));
            foreach(var state in spawnCandidates)
            {
                if(!hostReplicas.HasSpawnBudget) break;
                hostReplicas.GetOrCreate(state,pendingDeaths.Contains(state.Id));
            }
        }
        var claimed = actors.Values.Where(a => a != null).Select(a => a.GetInstanceID()).ToHashSet();
        int missing = 0;
        MissingAlive = 0; MissingDead = 0;
        foreach (var state in world.Enemies)
        {
            if (!actors.TryGetValue(state.Id, out var actor) || actor == null)
            {
                Vector3 target = new(state.SpawnX, state.SpawnY, state.SpawnZ);
                actor = HostOwnedEnemies ? hostReplicas.GetOrCreate(state, pendingDeaths.Contains(state.Id)) :
                    candidates.Where(a => !claimed.Contains(a.GetInstanceID()) && a.data.name == state.Template)
                        .OrderBy(a => Vector3.Distance(a.position(), target)).FirstOrDefault();
                if (actor == null) { missing++; if (state.Dead) MissingDead++; else MissingAlive++; continue; }
                actors[state.Id] = actor; claimed.Add(actor.GetInstanceID());
                appliedEnemyStates.Remove(state.Id);
                enemyAnimations.Remove(state.Id); animationRetryAt.Remove(state.Id);
                enemyPresentation.Remove(state.Id); presentedEnemyStates.Remove(state.Id);
                replicaPointers.Add(actor.Pointer);replicaIds[actor.Pointer]=state.Id;
                if (!HostOwnedEnemies)
                    backups[actor.GetInstanceID()] = new ReplicaBackup(HostEnemyReplicas.Root(actor).activeSelf, actor.gameObject.activeSelf, actor.transform.position, actor.transform.rotation,
                        actor.GetActorSync()?.transform.position ?? actor.transform.position,
                        actor.GetActorSync()?.transform.rotation ?? actor.transform.rotation,
                        actor.health.currentHealth, actor.health.maxHealth, actor.health.healthRatio);
                if (actor.navMeshAgent != null)
                { navigation[actor.GetInstanceID()] = actor.navMeshAgent.enabled; actor.navMeshAgent.enabled = false; }
            }
            bool confirmed = state.Dead && pendingDeaths.Contains(state.Id);
            if (confirmed && NativeRewardsEnabled && actor.dying != null && !actor.isDying(false) && !localDeaths.Contains(state.Id) && !session.TryClaimReward(epoch, state.Id))
            {
                // Session-level claims survive local map unload/re-entry. Scene cleanup must not grant the same host death twice.
                pendingDeaths.Remove(state.Id); handledRewards.Add(state.Id); confirmed = false;
            }
            if (state.Dead && enemyVisuals.Remove(state.Id, out var deadVisuals)) deadVisuals.Dispose();
            if (state.Dead)
            {poses.Remove(state.Id);animationAuthority.Remove(actor.Pointer);enemyPresentation.Remove(state.Id);presentedEnemyStates.Remove(state.Id);enemyAnimations.Remove(state.Id);}
            if (confirmed && NativeRewardsEnabled && actor.dying != null && !actor.isDying(false) && localDeaths.Add(state.Id))
            {
                corpsePointers.Add(actor.Pointer);
                // Consume before native callbacks; they must never be retried after a partial grant.
                SetPose(actor, state);
                actor.health.currentHealth = 0;
                actor.health.healthRatio = Il2Cpp.BaseHealth.HealthPercentageToRatio(0);
                HostEnemyReplicas.SetActive(actor, true);
                var xp = Il2Cpp.PlayerFinder.getExperienceTracker();
                long beforeXp = xp?.CurrentExperience ?? 0;
                // Some fresh replica prefabs reach health initialization before
                // DeathItemDrop.Start subscribes. Invoke that callback only if
                // the native death did not already dispatch it.
                var drop = actor.deathItemDrop ?? actor.GetComponent<Il2Cpp.DeathItemDrop>();
                // Fresh replicas may die before Start assigns DeathItemDrop.dying.
                // Its OnDeath reads the field as well as the method argument.
                if (drop != null && drop.dying == null) drop.dying = actor.dying;
                var completion = NativeRewardCompletion.Begin(drop);
                bool rewardFailed = false;
                try
                {
                    actor.dying.die(false);
                    if (drop != null && !rewardCallbacks.Contains(actor.GetInstanceID())) drop.OnDeath(actor.dying);
                }
                catch (Exception ex)
                {
                    rewardFailed = true;
                    // A native reward can partially complete before throwing.
                    // Consume once; never duplicate grants or disconnect the room.
                    MelonLoader.MelonLogger.Warning($"Guest reward held for enemy {state.Id}: {ex.Message}");
                }
                long afterXp = xp?.CurrentExperience ?? beforeXp;
                GuestExperienceGranted += Math.Max(0, afterXp - beforeXp);
                NativeDeathsDispatched++;
                pendingDeaths.Remove(state.Id);
                handledRewards.Add(state.Id);
                if (rewardFailed) Volatile.Write(ref completion.Failed, 1);
                Volatile.Write(ref completion.DispatchReturned, 1);
                asyncRewards[state.Id] = completion;
                MelonLoader.MelonLogger.Msg($"Native guest death dispatched: epoch {epoch}, enemy {state.Id}. Validate local rewards in campaign.");
            }
            // Native dying owns corpse animation/destruction once a confirmed death is dispatched.
            if (!localDeaths.Contains(state.Id)) ApplyActor(actor, state);
        }
        var present = world.Enemies.Select(e => e.Id).ToHashSet();
        if (HostOwnedEnemies) hostReplicas.RemoveAbsent(present);
        foreach (var id in actors.Keys.Where(id => !present.Contains(id)).ToArray())
        {
            if (actors[id] != null) HostEnemyReplicas.SetActive(actors[id], false);
            if(actors.TryGetValue(id,out var removed) && removed!=null)
            {animationAuthority.Remove(removed.Pointer);replicaIds.Remove(removed.Pointer);replicaPointers.Remove(removed.Pointer);NativeAilmentSynchronization.Forget(removed);}
            actors.Remove(id); appliedEnemyStates.Remove(id); enemyAnimations.Remove(id); animationRetryAt.Remove(id);
            enemyPresentation.Remove(id); presentedEnemyStates.Remove(id);
            poses.Remove(id);
            if (enemyVisuals.Remove(id, out var visuals)) visuals.Dispose();
        }
        bossHud.Apply(world, actors);
        // Local procedural spawns are not guaranteed to match the host. Keep this visible.
        appliedRevision = world.Revision;
        int mapped = world.Enemies.Count(e => actors.TryGetValue(e.Id, out var a) && a != null);
        Status = $"Host sync: {mapped}/{world.Enemies.Length}; missing {MissingAlive} alive/{MissingDead} dead; epoch {epoch}; rev {world.Revision}";
    }

    internal bool IsReplica(Actor actor) => session != null && !session.IsHost && actor is not null && replicaPointers.Contains(actor.Pointer);

    internal bool ShouldSuppressAi(Actor actor) => IsReplica(actor) ||
        (session != null && !session.IsHost && HostOwnedEnemies && (hostReplicas.Owns(actor) || hostReplicas.IsHidden(actor)));

    internal void SuppressLocalSpawn(Actor actor)
    {
        if (session == null || session.IsHost || !HostOwnedEnemies || scene == "" ||
            session.WorldInfo.Scene != scene || !Enemy(actor) || hostReplicas.Owns(actor)) return;
        hostReplicas.HideLocal(new[] { actor });
    }

    internal bool IsNativeCorpse(Actor actor) => actor is not null && corpsePointers.Contains(actor.Pointer);

    internal bool ForwardDamage(Il2Cpp.BaseHealth health, float damage, bool cull = false, float cullPercent = 0, float flatCull = 0)
    {
        if (session == null || session.IsHost || health == null) return false;
        var pair = actors.FirstOrDefault(p => p.Value != null && p.Value.health != null && p.Value.health.Pointer == health.Pointer);
        if (pair.Value == null)
            return HostOwnedEnemies && (hostReplicas.Owns(health.actor) || hostReplicas.IsHidden(health.actor));
        if(damage>0)
        {
            bool queued=NativePartyStatus.Enabled && session.AcceptsResolvedAilments && guestWorld!=null ?
                NativePartyStatus.QueueDirect(this,pair.Value,pair.Key,damage,cull,cullPercent,flatCull,NativeAilmentSynchronization.InSourceDot):
                session.RequestHit(epoch,pair.Key,damage,cull,cullPercent,flatCull,NativeAilmentSynchronization.InSourceDot);
            if(queued) ForwardedHits++;
        }
        return true;
    }
    internal bool ForwardControl(Actor actor, int type, float duration, float angle)
    {
        if (session == null || session.IsHost || !IsReplica(actor)) return false;
        var pair = actors.FirstOrDefault(p => p.Value?.Pointer == actor.Pointer);
        if (pair.Value != null) session.RequestControl(epoch, pair.Key, type, Mathf.Clamp(duration, 0.001f, 30), Mathf.Clamp(angle, -360, 360));
        return true;
    }
    internal bool TryGetReplicaId(Actor actor,out int id)
    {
        id=0;
        return session?.IsHost==false && actor!=null && replicaIds.TryGetValue(actor.Pointer,out id) &&
            actors.TryGetValue(id,out var current) && current!=null && current.Pointer==actor.Pointer;
    }

    private void ApplyActor(Actor actor, EnemyState state)
    {
        if (HostOwnedEnemies) hostReplicas.FreezeSimulation(actor);
        appliedEnemyStates.TryGetValue(state.Id, out var previous);
        if (!state.Dead || previous == null || previous.Dead != state.Dead)
        {
            if (state.Dead) { poses.Remove(state.Id); SetPose(actor, state); }
            else poses.Receive(state.Id, actor, new Vector3(state.X, state.Y, state.Z), Quaternion.Euler(0, state.Yaw, 0), previous == null,
                state.VisualPosition == null ? null : NativeSkillVisualCapture.Vector(state.VisualPosition));
        }
        if (previous == null || previous.MaxHealth != state.MaxHealth || previous.Health != state.Health)
        {
            actor.health.maxHealth = state.MaxHealth;
            actor.health.currentHealth = state.Health;
            actor.health.healthRatio = Il2Cpp.BaseHealth.HealthPercentageToRatio(state.Health / state.MaxHealth);
            var sync = actor.GetActorSync();
            if (sync != null) { sync.healthRatio = actor.health.healthRatio; sync.ReceiveSyncMaxHealth(state.MaxHealth); }
        }
        if (previous == null || previous.Ward != state.Ward)
        {
            if (actor.protection != null) actor.protection.ward = state.Ward;
            actor.GetActorSync()?.ReceiveSyncWard(state.Ward);
        }
        try {NativePartyStatusMirror.Apply(actor,state);}
        catch(Exception ex) {NativeAilmentSynchronization.Error=ex.GetBaseException().Message;}
        if(state.Dead)
        {
            enemyPresentation.Remove(state.Id);presentedEnemyStates.Remove(state.Id);enemyAnimations.Remove(state.Id);
            NativeEnemyControl.Present(actor,state,previous);
            NativeAilmentSynchronization.Present(actor,Array.Empty<AilmentCount>());
            if(enemyVisuals.Remove(state.Id,out var obsolete)) obsolete.Dispose();
        }
        else
        {
            if(!enemyPresentation.TryGetValue(state.Id,out var buffer)) enemyPresentation[state.Id]=buffer=new();
            buffer.Push(poses.SampleTime,state);
        }
        // Authoritative HP/death is immediate; only visual actions wait for the pose cursor.
        if (previous == null || previous.Dead != state.Dead) HostEnemyReplicas.SetActive(actor, !state.Dead);
        appliedEnemyStates[state.Id] = state;
    }

    private void ApplyEnemyPresentation(Actor actor,EnemyState state,EnemyState previous)
    {
            if (state.Animation != null)
            {
                NativeAnimation.Apply(NativeAnimation.Find(actor), state.Animation, false, false, previous?.Animation);
                var animator=NativeAnimation.Find(actor);
                if(animator?.runtimeAnimatorController!=null && animator.layerCount>0) ClaimAnimation(actor,state);
                EnemyAnimationUpdates++;
            }
            else if (state.Motion != null)
            {
            try
            {
                if (!enemyAnimations.TryGetValue(state.Id, out var driver) && Time.unscaledTime >= animationRetryAt.GetValueOrDefault(state.Id))
                {
                    animationRetryAt[state.Id] = Time.unscaledTime + 0.25f;
                    enemyAnimations[state.Id] = driver = new NativePlayerAnimationReplica(actor.GetActorSync()?.actorVisuals?.gameObject ?? HostEnemyReplicas.Root(actor));
                    animationRetryAt.Remove(state.Id);
                }
                if (driver != null && state.AttackSequence > playedAttacks.GetValueOrDefault(state.Id) && state.Motion != null)
                {
                    if (state.Motion.State is 5 or 9)
                    {
                        if(driver.ReplayEnemyAttack(state.Motion.AnimationNumber,state.Motion.AnimationSpeed,state.Motion))
                            playedAttacks[state.Id] = state.AttackSequence;
                    }
                }
                if (driver != null) { driver.Apply(state.Motion, poses.IsMoving(state.Id), false); EnemyAnimationUpdates++; }
            }
            catch (Exception ex) { EnemyAnimationError = ex.Message; }
            }
        try {NativeEnemyControl.Present(actor, state, previous);} catch(Exception ex) {EnemyAnimationError="Control presentation: "+ex.Message;}
        try {NativeAilmentSynchronization.Present(actor,state.Ailments);}
        catch(Exception ex) {NativeAilmentSynchronization.Error=ex.GetBaseException().Message;}
        if(state.Dead || state.Visuals.Length==0)
        {if(enemyVisuals.Remove(state.Id,out var obsolete)) obsolete.Dispose();}
        else
        {
            if (!enemyVisuals.TryGetValue(state.Id, out var visuals)) enemyVisuals[state.Id] = visuals = new NativeSkillVisualReplica(true);
            visuals.Apply(state.Visuals);
        }
    }

    private static void SetPose(Actor actor, EnemyState state)
    {
        var position = new Vector3(state.X, state.Y, state.Z);
        var rotation = Quaternion.Euler(0, state.Yaw, 0);
        var sync = actor.GetActorSync();
        // Gameplay collider and separately-owned visual skeleton must share the host pose.
        if (sync != null) { sync.transform.position = position; sync.transform.rotation = rotation; }
        actor.transform.position = position; actor.transform.rotation = rotation;
    }

    internal static void NativeOwnershipSmoke()
    {
        NativeSkillVisualCapture.NativeIndexSmoke();
        using var host = LanSession.Host("EnemyVisualQA", "enemy-visual-qa", "qa-room", 0, System.Net.IPAddress.Loopback);
        using var guest = LanSession.JoinAsync("127.0.0.1", host.Port, "Guest", "enemy-visual-qa", "qa-room").GetAwaiter().GetResult();
        var root = new GameObject("LAN enemy ownership QA"); root.SetActive(false);
        var data = ScriptableObject.CreateInstance<Il2Cpp.ActorData>(); data.actorType = Il2Cpp.ActorData.Type.Normal;
        var oldActive = Active; var oldSession = SkillCapture.Session;
        var actor = root.AddComponent<Actor>(); actor.tForm = root.transform; actor.data = data;
        actor.health = root.AddComponent<Il2Cpp.BaseHealth>();
        try
        {
            Active = new WorldSynchronizer { session = host }; SkillCapture.Session = host;
            if (!NativeVisualEvents.IsOwned(actor)) throw new InvalidOperationException("Host NPC native VFX was excluded.");
            Active = new WorldSynchronizer { session = guest }; SkillCapture.Session = guest;
            if (NativeVisualEvents.IsOwned(actor)) throw new InvalidOperationException("Guest NPC native VFX was permitted to originate.");
            if (!Il2Cpp.BaseHealth.all.Contains(actor.health)) Il2Cpp.BaseHealth.all.Add(actor.health);
            if (!Il2Cpp.BaseHealth.all.Contains(actor.health)) throw new InvalidOperationException("Native health target registry did not retain the target.");
        }
        finally
        {
            Il2Cpp.BaseHealth.all.Remove(actor.health);
            Active = oldActive; SkillCapture.Session = oldSession;
            UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(data);
        }
    }

    internal static bool NativeSmoke()
    {
        using var host = LanSession.Host("AdapterSmoke", "adapter-smoke", "test-room", 0, System.Net.IPAddress.Loopback);
        using var guest = LanSession.JoinAsync("127.0.0.1", host.Port, "SmokeGuest", "adapter-smoke", "test-room").GetAwaiter().GetResult();
        guest.Publish(new PlayerState { Scene = "smoke-zone", Available = true });
        host.PublishWorld(new WorldState { Scene = "smoke-zone", Epoch = 1, Revision = 1,
            Enemies = new[] { new EnemyState { Id = 1, Template = "smoke", MaxHealth = 100, Health = 100 } } });
        var root = new GameObject("LAN temporary inactive native adapter smoke");
        root.SetActive(false);
        var previous = Active;
        var adapter = new WorldSynchronizer { session = guest, epoch = 1 };
        try
        {
            var actor = root.AddComponent<Actor>();
            var health = root.AddComponent<Il2Cpp.BaseHealth>();
            actor.health = health; health.actor = actor;
            var controller = root.AddComponent<Il2Cpp.StateController>(); controller.actor = actor;
            adapter.actors[1] = actor; adapter.replicaPointers.Add(actor.Pointer); Active = adapter;
            adapter.ApplyActor(actor, new EnemyState { Id = 1, Template = "smoke", X = 3, Y = 4, Z = 5,
                MaxHealth = 100, Health = 80, Dead = true });
            if (health.currentHealth != 80 || health.maxHealth != 100 || actor.transform.position.x != 3)
                throw new InvalidOperationException("Native snapshot application failed.");
            Thread.Sleep(200);
            health.HealthDamage(20); // Exercises Harmony's actual IL2CPP native detour.
            Thread.Sleep(200);
            if (health.currentHealth != 80 || !host.TryTakeHit(out var hit) || hit.Damage != 20 || hit.PlayerId != guest.LocalId)
                throw new InvalidOperationException("Native damage detour did not forward the hit to host.");
            health.HealthDamageCull(15, 0.1f, 2);
            controller.update(0.1f);
            health.OnUpdateTick(10);
            Thread.Sleep(200);
            if (health.currentHealth != 80 || !host.TryTakeHit(out var culled) || culled.Damage != 15 || !culled.Cull || culled.CullPercent != 0.1f || culled.FlatCull != 2)
                throw new InvalidOperationException("Native cull/AI/health update detour smoke failed.");
            return true;
        }
        finally { Active = previous; UnityEngine.Object.Destroy(root); }
    }

    private void Restore()
    {
        // Scene unload cannot turn an unfinished native grant into a successful receipt.
        // Report interruption conservatively; the host retains failed delivery records and never retries a partial grant.
        if (session?.IsHost == false)
        {
            foreach (var result in rewardResults) session.ReportDeath(new DeathReceipt(epoch,result.Key,result.Value));
            foreach (var delivery in asyncRewards)
                session.ReportDeath(new DeathReceipt(epoch,delivery.Key,delivery.Value.Outcome ?? DeathReceiptOutcome.Failed));
        }
        poses.Clear();
        NativeEnemyControl.Reset();
        NativePartyStatus.Reset();NativeAilmentSynchronization.Reset();replicaIds.Clear();sourceStatusVisuals.BeginEpoch(-1);
        scanned = Array.Empty<Actor>(); nextScan = 0; lastPublished = null; nextWorldHeartbeat = 0;
        foreach (var replica in enemyVisuals.Values) replica.Dispose();
        enemyVisuals.Clear(); enemyAnimations.Clear(); rewardCallbacks.Clear();
        enemyPresentation.Clear();presentedEnemyStates.Clear();spawnCandidates.Clear();
        animationAuthority.Clear();
        replicaPointers.Clear(); animationRetryAt.Clear();
        appliedEnemyStates.Clear();
        capturedAttacks.Clear(); playedAttacks.Clear();
        handledRewards.Clear(); rewardResults.Clear();
        foreach (var completion in asyncRewards.Values) NativeRewardCompletion.Release(completion);
        asyncRewards.Clear();
        bossHud.Reset();
        if (activeHostOwnedEnemies) { hostReplicas.Clear(); navigation.Clear(); backups.Clear(); corpsePointers.Clear(); return; }
        foreach (var actor in actors.Values)
        {
            if (actor == null) continue;
            if (IsNativeCorpse(actor)) continue; // Never resurrect or restart navigation on a rewarded corpse.
            if (navigation.TryGetValue(actor.GetInstanceID(), out bool enabled) && actor.navMeshAgent != null) actor.navMeshAgent.enabled = enabled;
            if (backups.TryGetValue(actor.GetInstanceID(), out var backup))
            {
                var sync = actor.GetActorSync();
                if (sync != null) { sync.transform.position = backup.SyncPosition; sync.transform.rotation = backup.SyncRotation; }
                actor.transform.position = backup.Position;
                actor.transform.rotation = backup.Rotation;
                actor.health.currentHealth = backup.Health; actor.health.maxHealth = backup.MaxHealth;
                actor.health.healthRatio = backup.Ratio;
                actor.gameObject.SetActive(backup.GameplayActive); HostEnemyReplicas.Root(actor).SetActive(backup.Active);
            }
        }
        corpsePointers.Clear();
        navigation.Clear(); backups.Clear();
    }
    internal void Reset() { Restore(); NativeAilmentSynchronization.Reset();guestWorld = null; actors.Clear(); hostIds.Clear(); origins.Clear(); history.Clear(); localDeaths.Clear(); pendingDeaths.Clear(); session = null; if (Active == this) Active = null; }
    private sealed record ReplicaBackup(bool Active, bool GameplayActive, Vector3 Position, Quaternion Rotation, Vector3 SyncPosition, Quaternion SyncRotation, float Health, int MaxHealth, byte Ratio);
    internal sealed record HitHealthAudit(string PlayerId, int EnemyId, long Epoch, float RequestedDamage, float BeforeHealth, float AfterHealth);
}

[HarmonyPatch(typeof(Il2Cpp.DeathItemDrop), nameof(Il2Cpp.DeathItemDrop.OnDeath))]
internal static class GuestRewardCallbackPatch
{
    private static void Prefix(Il2Cpp.Dying dying)
    {
        try { WorldSynchronizer.Active?.ObserveRewardCallback(dying?.getActor()); }
        catch (Exception ex) { MelonLoader.MelonLogger.Warning("Guest reward callback audit: " + ex.Message); }
    }
}

[HarmonyPatch]
internal static class HostOfflineEnemyAttackPatch
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods() =>
        new[] {
            AccessTools.Method(typeof(Il2Cpp.BasicAnimationManager), nameof(Il2Cpp.BasicAnimationManager.startAttacking)),
            AccessTools.Method(typeof(Il2Cpp.ComplexAnimationManager), nameof(Il2Cpp.ComplexAnimationManager.startAttacking)),
            AccessTools.Method(typeof(Il2Cpp.ComplexAnimationManager), nameof(Il2Cpp.ComplexAnimationManager.instantAttack))
        };
    private static void Postfix(Il2Cpp.AnimationManagerWithAnimator __instance, byte animationNumber, float animationSpeed)
    {
        try
        {
            var actor = __instance.GetComponentInParent<Actor>() ?? __instance.GetComponentInParent<Il2Cpp.ActorVisuals>()?.actorSync?.GameplayActor;
            WorldSynchronizer.Active?.CaptureEnemyAttack(actor, animationNumber, animationSpeed);
        }
        catch { /* Visual owners may still be loading; capture on the next native event. */ }
    }
}

[HarmonyPatch(typeof(Il2Cpp.GameplayRelayAnimationManager), nameof(Il2Cpp.GameplayRelayAnimationManager.startAttacking))]
internal static class HostEnemyAttackPatch
{
    private static void Postfix(Il2Cpp.GameplayRelayAnimationManager __instance, byte animationNumber, float animationSpeed)
    {
        try
        {
            NativePlayerAnimationReplica.RecordLocalAttack(__instance.actor, animationNumber, animationSpeed);
            WorldSynchronizer.Active?.CaptureEnemyAttack(__instance.actor, animationNumber, animationSpeed);
        }
        catch (Exception ex) { MelonLoader.MelonLogger.Warning("Host enemy attack capture: " + ex.Message); }
    }
}

[HarmonyPatch(typeof(Il2Cpp.GameplayRelayAnimationManager), nameof(Il2Cpp.GameplayRelayAnimationManager.instantAttack))]
internal static class HostEnemyInstantAttackPatch
{
    private static void Postfix(Il2Cpp.GameplayRelayAnimationManager __instance, byte animationNumber, float animationSpeed)
    {
        try
        {
            NativePlayerAnimationReplica.RecordLocalAttack(__instance.actor, animationNumber, animationSpeed);
            WorldSynchronizer.Active?.CaptureEnemyAttack(__instance.actor, animationNumber, animationSpeed);
        }
        catch (Exception ex) { MelonLoader.MelonLogger.Warning("Host enemy instant attack capture: " + ex.Message); }
    }
}

[HarmonyPatch(typeof(Il2Cpp.StateController), nameof(Il2Cpp.StateController.update))]
internal static class ReplicaAiPatch
{
    private static bool Prefix(Il2Cpp.StateController __instance) => WorldSynchronizer.Active?.ShouldSuppressAi(__instance.actor) != true || WorldSynchronizer.Active.IsNativeCorpse(__instance.actor);
}

// A native offline replica also generates local idle/move animation events.
// Once Host presentation is ready it is the sole visual writer. Preserve the
// original initialization path until then and all native corpse/death playback.
[HarmonyPatch(typeof(Il2Cpp.ActorSyncAnimationBuffer),nameof(Il2Cpp.ActorSyncAnimationBuffer.Unbuffer))]
internal static class ReplicaAnimationBufferPatch
{
    private static bool Prefix(Il2Cpp.ActorSyncAnimationBuffer __instance,Il2Cpp.ActorSync sync)=>
        WorldSynchronizer.Active?.SuppressNativeAnimation(sync,__instance)!=true;
}

[HarmonyPatch(typeof(Il2Cpp.Actor), nameof(Il2Cpp.Actor.Start))]
internal static class GuestLocalSpawnPatch
{
    private static void Postfix(Il2Cpp.Actor __instance)
    {
        try { WorldSynchronizer.Active?.SuppressLocalSpawn(__instance); }
        catch (Exception ex) { MelonLoader.MelonLogger.Warning("Guest local spawn suppression: " + ex); }
    }
}

[HarmonyPatch(typeof(Il2Cpp.Dying), nameof(Il2Cpp.Dying.onEnter))]
internal static class HostDeathPatch
{
    private static void Postfix(Il2Cpp.Dying __instance)
    {
        try { WorldSynchronizer.Active?.CaptureDeath(__instance.getActor()); }
        catch (Exception ex) { MelonLoader.MelonLogger.Warning("Host death capture: " + ex.Message); }
    }
}

[HarmonyPatch(typeof(Il2Cpp.BaseHealth), nameof(Il2Cpp.BaseHealth.HealthDamage))]
internal static class ReplicaDamagePatch
{
    private static bool Prefix(Il2Cpp.BaseHealth __instance, float damage, ref float __result)
    { if (WorldSynchronizer.Active?.ForwardDamage(__instance, damage) != true) return true; __result = 0; return false; }
}

[HarmonyPatch(typeof(Il2Cpp.BaseHealth), nameof(Il2Cpp.BaseHealth.HealthDamageCull))]
internal static class ReplicaCullPatch
{
    private static bool Prefix(Il2Cpp.BaseHealth __instance, float damage, float cullPercent, float flatCull, ref float __result)
    { if (WorldSynchronizer.Active?.ForwardDamage(__instance, damage, true, cullPercent, flatCull) != true) return true; __result = 0; return false; }
}

[HarmonyPatch(typeof(Il2Cpp.BaseHealth), nameof(Il2Cpp.BaseHealth.OnUpdateTick))]
internal static class ReplicaHealthTickPatch
{
    private static bool Prefix(Il2Cpp.BaseHealth __instance) => WorldSynchronizer.Active?.IsReplica(__instance.actor) != true;
}
