using UnityEngine;
using UObject = UnityEngine.Object;

namespace LastEpochLAN.Mod;

// Reuses the class's serialized native animation driver and Animator controller.
// Persistence, input and other prefab scripts are removed before activation.
internal sealed class NativeClassAnimator : IDisposable
{
    internal GameObject Root { get; private set; }
    internal Il2Cpp.AnimationManagerWithAnimator Driver { get; private set; }
    internal Animator Animator => Driver.GetAnimator();

    internal NativeClassAnimator(int classId, bool retainEquipmentSlots = false)
    {
        var characterClass = Il2Cpp.CharacterClassList.getClass(classId);
        if (characterClass == null) throw new InvalidOperationException("Class asset is unavailable.");
        Root = new GameObject("LAN native class animation " + classId); Root.SetActive(false);
        try
        {
            var model = Il2CppLE.AssetBundles.SoftRefExtensions.Instantiate(characterClass.visualsPrefabSoftRef,
                Root.transform, false, "LastEpochLAN.NativeClassAnimator", 0);
            foreach (var script in model.GetComponentsInChildren<MonoBehaviour>(true))
            {
                var animation = script.TryCast<Il2Cpp.AnimationManagerWithAnimator>();
                if (animation != null)
                {
                    animation.enabled = false;
                    Driver ??= animation;
                    continue;
                }
                if (script.TryCast<Il2CppLE.AssetBundles.LoadRefComponent>() != null) continue;
                if (script.TryCast<Il2CppAnimationLoader.AnimationLoader>() != null)
                { script.enabled = true; continue; }
                if (retainEquipmentSlots && (script.TryCast<Il2Cpp.IMSlotManager>() != null ||
                    script.TryCast<Il2Cpp.IMSlot>() != null || script.TryCast<Il2Cpp.BodyHiding>() != null ||
                    script.TryCast<Il2Cpp.WeaponJoint>() != null || script.TryCast<Il2Cpp.EquipTypeAngles>() != null ||
                    script.TryCast<Il2Cpp.WeaponJointDirectionPointer>() != null))
                { script.enabled = false; continue; }
                UObject.DestroyImmediate(script);
            }
            if (Driver == null) throw new InvalidOperationException("Class has no native Animator driver.");
            foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var body in model.GetComponentsInChildren<Rigidbody>(true)) UObject.DestroyImmediate(body);
            foreach (var animator in model.GetComponentsInChildren<Animator>(true))
            { animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
            model.SetActive(true); Root.SetActive(true);
            if (Animator == null || Animator.runtimeAnimatorController == null)
                throw new InvalidOperationException("Native class Animator controller is unavailable.");
            Animator.enabled = true;
            Animator.speed = 1;
            Animator.Rebind(); Animator.Update(0);
            Driver.moveAnimationSpeedMultiplier = 1;
            Driver.attackAnimationSpeedMultiplier = 1;
            Driver.Start(); Driver.stopAllAnimations(); Driver.scaleMoveSpeed(1);
        }
        catch { Dispose(); throw; }
    }

    internal static object Smoke()
    {
        var reports = new List<object>();
        for (int classId = 0; classId < 5; classId++)
        {
            using var visual = new NativeClassAnimator(classId);
            var animator = visual.Animator;
            animator.Rebind(); animator.Update(0);
            int idle = animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
            string idleClip = DominantClip(animator);
            visual.Driver.scaleMoveSpeed(1);
            visual.Driver.startMoving();
            int moveHash = Il2Cpp.AnimationManagerWithAnimator.animationHashMove;
            if (!animator.GetBool(moveHash)) throw new InvalidOperationException("Native movement did not update the Animator parameter.");
            for (int i = 0; i < 10; i++) animator.Update(0.1f);
            var moving = animator.GetCurrentAnimatorStateInfo(0);
            string movingClip = DominantClip(animator);
            var snapshot = NativeAnimation.Capture(animator);
            if(snapshot==null) throw new InvalidOperationException($"Class {classId}: native controller snapshot invalid; speed={animator.speed}; layers={animator.layerCount}.");
            var peerRoot = ClassVisualReplica.Create(classId);
            string peerClip;
            try
            {
                var peer = peerRoot.GetComponentsInChildren<Il2Cpp.AnimationManagerWithAnimator>(true).First().GetAnimator();
                peer.Rebind(); peer.Update(0);
                // Exercise the actual gameplay replica path, including a JSON wire
                // round trip; a state hash alone cannot reproduce a blend tree.
                var wire = System.Text.Json.JsonSerializer.Serialize(snapshot);
                var received = System.Text.Json.JsonSerializer.Deserialize<LastEpochLAN.Core.AnimationSnapshot>(wire);
                NativeAnimation.Apply(peer, received);
                // Animator transitions/blend trees need render time to settle;
                // a zero-delta evaluation is not equivalent to a received frame.
                for (int frame=0;frame<10;frame++) peer.Update(0.1f);
                peerClip = DominantClip(peer);
                if (!peer.GetBool(moveHash) || peerClip != movingClip)
                    throw new InvalidOperationException($"Class {classId}: replica diverged: host {movingClip}, peer {peerClip}; move={peer.GetBool(moveHash)}; parameters={snapshot.Parameters.Length}/{animator.parameters.Length}; moveInSnapshot={snapshot.Parameters.Any(p=>p.Hash==moveHash)}.");
            }
            finally { UObject.Destroy(peerRoot); }
            visual.Driver.stopMoving();
            if (animator.GetBool(moveHash)) throw new InvalidOperationException("Native stop movement did not clear the Animator parameter.");
            if ((moving.fullPathHash == idle && movingClip == idleClip) || moving.normalizedTime <= 0 || movingClip == "")
                throw new InvalidOperationException($"Class {classId}: native movement clip did not advance; idle {idle}/{idleClip}, moving {moving.fullPathHash}/{movingClip}, time {moving.normalizedTime}, active {animator.isActiveAndEnabled}, speed {animator.speed}.");
            for (int i = 0; i < 10; i++) animator.Update(0.1f);
            visual.Driver.startAttacking(0, 1);
            if (!visual.Driver.Cast<Il2Cpp.ComplexAnimationManager>().IsAttacking)
                throw new InvalidOperationException("Native attack driver did not enter attacking state.");
            for (int i = 0; i < 3; i++) animator.Update(0.1f);
            string attackingClip = DominantClip(animator);
            if (attackingClip == "" || attackingClip == idleClip)
                throw new InvalidOperationException($"Class {classId}: native attack did not select an attack clip ({attackingClip}).");
            visual.Driver.stopAttacking();
            if (visual.Driver.Cast<Il2Cpp.ComplexAnimationManager>().IsAttacking)
                throw new InvalidOperationException("Native stop attack did not clear attacking state.");
            reports.Add(new { classId, controller = animator.runtimeAnimatorController.name,
                nativeDriver = visual.Driver.GetIl2CppType().FullName, idleState = idle,
                movingState = moving.fullPathHash, idleClip, movingClip, attackingClip, movingTime = moving.normalizedTime,
                movementParameterCleared = true, attackStateCleared = true, gameplayReplicaClip = peerClip,
                gameplaySnapshotParameters = snapshot.Parameters.Length });
        }
        return reports;
    }

    internal static string DominantClip(Animator animator)
    {
        var clips = animator.GetCurrentAnimatorClipInfo(0);
        return clips.OrderByDescending(clip => clip.weight).FirstOrDefault().clip?.name ?? "";
    }

    public void Dispose() { if (Root != null) UObject.Destroy(Root); Root = null; Driver = null; }
}
