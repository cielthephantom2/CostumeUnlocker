using LastEpochLAN.Core;
using UnityEngine;

namespace LastEpochLAN.Mod;

// Drives the class's native visual animation controller. No gameplay casts/damage.
internal sealed class NativePlayerAnimationReplica
{
    private static (int Frame, byte Animation, float Speed)? localAttack;
    private static readonly Dictionary<IntPtr, (int State, float Next, NativeMotionState Motion)> captureCache = new();
    internal static void ClearLocalAttack() => localAttack = null;
    internal static void RecordLocalAttack(Il2Cpp.Actor actor, byte animation, float speed)
    {
        NativeSummonCapture.RecordAttack(actor,animation,speed);
        if (actor != null && actor.Pointer == Il2Cpp.PlayerFinder.getPlayerActor()?.Pointer)
            localAttack = (Time.frameCount, animation, speed);
    }
    internal static bool ReadLocalAttack(out byte animation, out float speed)
    {
        animation = 0; speed = 1;
        if (localAttack is not { } attack || attack.Frame != Time.frameCount) return false;
        animation = attack.Animation; speed = attack.Speed; return true;
    }
    private readonly Il2Cpp.AnimationManagerWithAnimator driver;
    private readonly Il2Cpp.PlayerAnimationManager playerDriver;
    private readonly Il2Cpp.ComplexAnimationManager complexDriver;
    private NativeMotionState previous;
    private float attackUntil;
    private IntPtr controllerPointer;
    private readonly Il2CppAnimationLoader.AnimationLoader[] loaders;
    private readonly HashSet<IntPtr> requestedLoaders = new();
    internal bool Ready => driver.animator?.runtimeAnimatorController != null && driver.animator.layerCount>0;
    private bool RefreshController()
    {
        foreach (var loader in loaders)
        {
            if (loader == null || requestedLoaders.Contains(loader.Pointer) || loader.animationLayerLoader == null || loader.layers == null) continue;
            for (int layer = 0; layer < loader.layers.Count; layer++) loader.SetLoad(layer, true);
            requestedLoaders.Add(loader.Pointer);
        }
        var controller = driver.animator?.runtimeAnimatorController;
        if (controller == null || driver.animator.layerCount==0) {previous=null;return false;}
        if (controller.Pointer == controllerPointer) return true;
        controllerPointer = controller.Pointer;
        previous = null;
        driver.animator.enabled = true;
        // Movement authority comes from the replicated pose. Native root
        // motion must not pull the visual skeleton away between snapshots.
        driver.animator.applyRootMotion = false;
        driver.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        driver.capabilitiesInitialised = false;
        driver.initialiseCapabilities();
        return true;
    }
    internal long SkillAnimations { get; private set; }
    internal long CastSnapshotsApplied { get; private set; }
    private long snapshotSequence = -1;

    internal void ApplyCastSnapshot(AnimationSnapshot snapshot, long sequence)
    {
        if (snapshot == null || snapshotSequence == sequence) return;
        if(!RefreshController()) return;
        NativeAnimation.Apply(driver.animator, snapshot);
        snapshotSequence = sequence; CastSnapshotsApplied++;
    }

    internal NativePlayerAnimationReplica(GameObject root)
    {
        driver = root.GetComponentsInChildren<Il2Cpp.AnimationManagerWithAnimator>(true).FirstOrDefault();
        loaders = root.GetComponentsInChildren<Il2CppAnimationLoader.AnimationLoader>(true);
        if (driver == null) throw new InvalidOperationException("Remote native animation driver is missing.");
        playerDriver = driver.TryCast<Il2Cpp.PlayerAnimationManager>();
        complexDriver = driver.TryCast<Il2Cpp.ComplexAnimationManager>();
    }

    internal static NativeMotionState Capture(Il2Cpp.Actor actor)
    {
        var relay = actor?.animationManager?.TryCast<Il2Cpp.GameplayRelayAnimationManager>();
        if (relay == null) return null;
        var state = relay.currentAnimationEvent;
        int current = (int)state.State;
        // Missing native nullable fields throw while boxing. Idle/move events
        // need not repeat that work for every actor on each network sample.
        if (current is not (5 or 9) && captureCache.TryGetValue(actor.Pointer, out var cached) &&
            cached.State == current && Time.unscaledTime < cached.Next) return cached.Motion;
        var result = new NativeMotionState { State = (int)state.State,
            AnimationNumber = NativeNullable.ReadField(state, "AnimationNumber", () => state.AnimationNumber) ?? 0,
            TemporaryIdle = NativeNullable.ReadField(state, "AbilityUsesTempIdleState", () => state.AbilityUsesTempIdleState) ?? false,
            TemporaryAnimation = (byte)(NativeNullable.ReadField(state, "Animation", () => state.Animation) ?? Il2Cpp.AbilityAnimation.Cast),
            Duration = NativeNullable.ReadField(state, "Duration", () => state.Duration) ?? 0,
            AnimationSpeed = NativeNullable.ReadField(state, "AnimationSpeed", () => state.AnimationSpeed) ?? 1,
            MoveSpeed = NativeNullable.ReadField(state, "MoveSpeedScale", () => state.MoveSpeedScale) ?? 1 };
        if (!result.IsValid()) return null;
        if (captureCache.Count > 1024) captureCache.Clear();
        captureCache[actor.Pointer] = (current, Time.unscaledTime + 0.4f + (uint)actor.Pointer.ToInt64() % 113 / 1000f, result);
        return result;
    }

    private int tickedFrame = -1;
    internal long ManualTicks { get; private set; }
    internal void TickPresentation()
    {
        if (tickedFrame == Time.frameCount) return;
        tickedFrame = Time.frameCount;
        if (driver.enabled || complexDriver == null) return;
        if(!RefreshController()) return;
        // Never tick an enabled driver twice. Disabled replicas must advance
        // temporary-idle/attack timers at render cadence, not network cadence.
        complexDriver.OnUpdateTick(Time.deltaTime); ManualTicks++;
    }

    internal void Apply(NativeMotionState motion, bool movingFallback, bool tickPresentation = true)
    {
        if(!RefreshController()) return;
        // Live monster visual drivers already receive the native update tick.
        // Only stripped/disabled replica drivers need our manual tick.
        if (tickPresentation) TickPresentation();
        motion ??= new NativeMotionState { State = (int)(movingFallback ? Il2Cpp.AnimationStates.Moving : Il2Cpp.AnimationStates.Idle) };
        if (movingFallback && motion.State == (int)Il2Cpp.AnimationStates.Idle && Time.unscaledTime >= attackUntil)
            motion = motion with { State = (int)Il2Cpp.AnimationStates.Moving };
        if (!Enum.IsDefined(typeof(Il2Cpp.AnimationStates), (byte)motion.State)) return;
        if (previous == motion) return;
        var state = (Il2Cpp.AnimationStates)motion.State;
        if (Time.unscaledTime < attackUntil && state != Il2Cpp.AnimationStates.Dying &&
            state != Il2Cpp.AnimationStates.Stunned) return;
        if (previous == null || previous.MoveSpeed != motion.MoveSpeed)
            driver.scaleMoveSpeed(motion.MoveSpeed > 0 ? motion.MoveSpeed : 1);
        if (Enum.IsDefined(typeof(Il2Cpp.AbilityAnimation), motion.TemporaryAnimation))
            driver.decideTemporaryIdleState(motion.TemporaryIdle, motion.Duration, (Il2Cpp.AbilityAnimation)motion.TemporaryAnimation);
        if (state == Il2Cpp.AnimationStates.Moving)
        { if (previous?.State != motion.State) { driver.stopAttacking(); driver.startMoving(); } }
        else if (state == Il2Cpp.AnimationStates.Attacking || state == Il2Cpp.AnimationStates.InstantAttacking)
        {
            if (!Enum.IsDefined(typeof(Il2Cpp.AbilityAnimation), motion.AnimationNumber)) return;
            if (previous?.State != motion.State || previous.AnimationNumber != motion.AnimationNumber || previous.AnimationSpeed != motion.AnimationSpeed)
            { driver.stopMoving(); driver.startAttacking(motion.AnimationNumber, motion.AnimationSpeed); }
        }
        else if (state == Il2Cpp.AnimationStates.Dying) driver.startDying();
        else if (state == Il2Cpp.AnimationStates.Stunned) driver.startStunned();
        else
        { driver.stopMoving(); driver.stopAttacking(); driver.stopStunned(); }
        previous = motion;
    }

    internal bool ReplayEnemyAttack(byte animation, float speed, NativeMotionState motion = null)
    {
        if(!RefreshController()) return false;
        if (driver.animator?.runtimeAnimatorController == null || animation > 63 || speed <= 0) return false;
        if(motion!=null)
        {
            driver.scaleMoveSpeed(motion.MoveSpeed>0 ? motion.MoveSpeed : 1);
            if(Enum.IsDefined(typeof(Il2Cpp.AbilityAnimation),motion.TemporaryAnimation))
                driver.decideTemporaryIdleState(motion.TemporaryIdle,motion.Duration,(Il2Cpp.AbilityAnimation)motion.TemporaryAnimation);
        }
        driver.stopMoving();
        if(motion?.State==(int)Il2Cpp.AnimationStates.InstantAttacking) driver.instantAttack(animation,speed);
        else driver.startAttacking(animation,speed);
        attackUntil = 0;
        previous = motion ?? new NativeMotionState { State = (int)Il2Cpp.AnimationStates.Attacking,
            AnimationNumber = animation, AnimationSpeed = speed };
        SkillAnimations++; return true;
    }

    internal bool ReplaySkill(SkillEvent skill)
    {
        if(!RefreshController()) return false;
        if (skill.AnimationNumber < 0 || !Enum.IsDefined(typeof(Il2Cpp.AbilityAnimation), (byte)skill.AnimationNumber)) return false;
        driver.stopMoving(); driver.instantAttack((byte)skill.AnimationNumber, skill.AnimationSpeed);
        // The player override also adjusts native animation layer weights.
        // A trigger alone bypasses that path and can leave casting invisible.
        if (playerDriver != null) playerDriver.startAttacking((byte)skill.AnimationNumber, skill.AnimationSpeed);
        else driver.startAttacking((byte)skill.AnimationNumber, skill.AnimationSpeed);
        attackUntil = Time.unscaledTime + (skill.ZoneAbility ? 0.65f : 0.45f) / skill.AnimationSpeed;
        previous = null; SkillAnimations++; return true;
    }
}
