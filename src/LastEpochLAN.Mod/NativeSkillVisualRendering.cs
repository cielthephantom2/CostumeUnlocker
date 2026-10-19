using LastEpochLAN.Core;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace LastEpochLAN.Mod;

internal sealed partial class NativeSkillVisualReplica
{
    private Visual Create(SkillVisualState state)
    {
        bool resolvedPrefab = state.PrefabGuid0 != 0 || state.PrefabGuid1 != 0;
        // An exact source prefab is sufficient: resolving the receiver's ability
        // or cosmetics service first can reject mutated/child ability assets or
        // pick a different local visual before the source prefab is instantiated.
        Il2Cpp.Ability ability = null;
        Il2CppLE.AssetBundles.SoftRef<GameObject> prefab;
        Il2CppSystem.Collections.Generic.List<Il2CppLE.AssetBundles.SoftRef<GameObject>> alternate = null;
        if (resolvedPrefab)
            prefab = new Il2CppLE.AssetBundles.SoftRef<GameObject>(new Il2CppLE.AssetBundles.Guid(state.PrefabGuid0, state.PrefabGuid1), state.PrefabSubPath);
        else if (state.CastingVfxKey != 0)
            prefab = new Il2Cpp.VFXRef(state.CastingVfxKey).VfxSoftRef;
        else
        {
            ability = new Il2Cpp.AbilityRef(state.AbilityKey).GetAbility();
            if (ability == null) throw new InvalidOperationException("Native ability asset is unavailable: " + state.AbilityKey);
            prefab = ability.visualSoftRef;
            alternate = ability.alternateVisualsSoftRefs;
        }
        var root = new GameObject("LAN remote skill VFX " + state.Id); root.SetActive(false);
        try
        {
            root.transform.position = NativeSkillVisualCapture.Vector(state.Position);
            root.transform.rotation = Quaternion.Euler(NativeSkillVisualCapture.Vector(state.Rotation));
            root.transform.localScale = NativeSkillVisualCapture.Vector(state.Scale);
            if (prefab)
                Il2CppLE.AssetBundles.SoftRefExtensions.Instantiate(prefab, root.transform, false, "LastEpochLAN.RemoteVFX", 0);
            // Native ReceiveCreateVisualsInternal attaches an optional additional
            // prefab at the zero-based visualsID; it does not replace the primary.
            if (!resolvedPrefab && state.CastingVfxKey == 0 && alternate != null && state.VisualIndex < alternate.Count && alternate[state.VisualIndex])
                Il2CppLE.AssetBundles.SoftRefExtensions.Instantiate(alternate[state.VisualIndex], root.transform, false, "LastEpochLAN.RemoteVFX.Alternate", 0);
            // Legacy offline skills can keep their renderers in the ability
            // prefab rather than the separate multiplayer visual asset. Clone
            // it under the inactive root, then strip gameplay before activation.
            if (state.CastingVfxKey == 0 && root.GetComponentsInChildren<Renderer>(true).Length == 0)
            {
                ability ??= new Il2Cpp.AbilityRef(state.AbilityKey).GetAbility();
                if (resolvedPrefab && ability != null && ability.visualSoftRef)
                    Il2CppLE.AssetBundles.SoftRefExtensions.Instantiate(ability.visualSoftRef, root.transform, false, "LastEpochLAN.CarrierVisualFallback", 0);
                if (ability != null && root.GetComponentsInChildren<Renderer>(true).Length == 0 && ability.abilityPrefabSoftRef)
                    Il2CppLE.AssetBundles.SoftRefExtensions.Instantiate(ability.abilityPrefabSoftRef, root.transform, false, "LastEpochLAN.LegacyRenderOnly", 0);
            }
            for (int index = 0; index < root.transform.childCount; index++)
            {
                var child = root.transform.GetChild(index);
                child.localPosition = Vector3.zero; child.localRotation = Quaternion.identity; child.localScale = Vector3.one;
            }
            // Rendering primitives are retained. Native ability/gameplay controllers
            // must not run against the receiving PC's offline actor or apply damage.
            var stretchers = root.GetComponentsInChildren<Il2Cpp.SubObjectsStretchToLocationCreatedFrom>(true).ToArray();
            int beforeStrip = root.GetComponentsInChildren<Renderer>(true).Length;
            foreach (var script in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (script.TryCast<Il2Cpp.SubObjectsStretchToLocationCreatedFrom>() != null)
                    script.enabled = false; // Driven below using the transmitted endpoint; never a local actor reference.
                else if (NativeVfxPresentation.Keep(script)) { script.enabled = true; PresentationScriptsRetained++; }
                else if (script.TryCast<Il2CppLE.AssetBundles.LoadRefComponent>() == null) UObject.DestroyImmediate(script);
            }
            foreach (var collider in root.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var body in root.GetComponentsInChildren<Rigidbody>(true)) UObject.DestroyImmediate(body);
            var lines = root.GetComponentsInChildren<LineRenderer>(true).ToList();
            // Some beam renderers are created by gameplay-bound scripts. Rebuild
            // only the captured rendering geometry when those scripts are absent.
            int needed = state.Lines.Length == 0 ? 0 : state.Lines.Max(line => line.Index) + 1;
            while (lines.Count < needed)
            {
                var lineObject = new GameObject("LAN captured beam " + lines.Count);
                lineObject.transform.SetParent(root.transform, false);
                var line = lineObject.AddComponent<LineRenderer>(); line.sharedMaterial = MaterialFor(""); lines.Add(line);
            }
            if (root.GetComponentsInChildren<Renderer>(true).Length == 0)
                throw new InvalidOperationException("Native skill has no renderable visual or captured beam: " + (ability?.name ?? state.AbilityKey.ToString()) + $"; renderersBeforeStrip={beforeStrip}; prefabChildren={root.transform.childCount}");
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = true;
                for (var parent = renderer.transform; parent != null && parent.Pointer != root.transform.Pointer; parent = parent.parent)
                    parent.gameObject.SetActive(true);
            }
            for (int index = 0; index < root.transform.childCount; index++) root.transform.GetChild(index).gameObject.SetActive(true);
            root.SetActive(true);
            var prefabRoot = root;
            for (int i = 0; i < root.transform.childCount; i++)
            {
                var child = root.transform.GetChild(i).gameObject;
                if (child.GetComponentsInChildren<Renderer>(true).Length == 0) continue;
                prefabRoot = child; break;
            }
            prefabRoot.layer = state.Layer;
            ApplyPoses(prefabRoot, state.Poses);
            if (state.StretchOrigin != null)
                foreach (var stretcher in stretchers)
                {
                    stretcher.references = null; stretcher.useTransformInsteadOfLocationCreatedFrom = null;
                    stretcher.offset = Vector3.zero; stretcher.useRelativeOffset = false;
                    stretcher.setLocation(NativeSkillVisualCapture.Vector(state.StretchOrigin));
                    stretcher.OnFirstTick(); stretcher.OnUpdateTick(0);
                }
            float playSeconds = 0.75f;
            foreach (var particles in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                // A short burst can already be over at snapshot arrival. Start it
                // on receipt instead of simulating past its entire visible lifetime.
                particles.Simulate(0, false, true); particles.Play(false);
                var main = particles.main;
                playSeconds = Mathf.Max(playSeconds, Mathf.Clamp(main.startLifetime.constantMax, 0.75f, 2f));
            }
            return new Visual(root, prefabRoot, lines.ToArray(), stretchers, Time.unscaledTime + playSeconds, root.GetComponentsInChildren<ParticleSystem>(true).ToArray());
        }
        catch { UObject.Destroy(root); throw; }
    }

    private static void ApplyPoses(GameObject prefabRoot, VisualPose[] poses)
    {
        foreach (var pose in poses.OrderBy(p => p.Path.Count(c => c == '/')))
        {
            var node = prefabRoot.transform;
            foreach (var part in pose.Path.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                int index = int.Parse(part);
                if (index >= node.childCount) { node = null; break; }
                node = node.GetChild(index);
            }
            if (node == null) continue;
            node.localPosition = NativeSkillVisualCapture.Vector(pose.Position);
            node.localRotation = Quaternion.Euler(NativeSkillVisualCapture.Vector(pose.Rotation));
            node.localScale = NativeSkillVisualCapture.Vector(pose.Scale); node.gameObject.layer = pose.Layer; node.gameObject.SetActive(pose.Active);
        }
    }

    private static Material MaterialFor(string name)
    {
        if (materials.TryGetValue(name, out var cached) && cached != null) return cached;
        var found = name == "" ? null : Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(material => material != null &&
            material.name.Replace(" (Instance)", "") == name);
        if (found == null)
        {
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader == null) throw new InvalidOperationException("No beam rendering shader is available.");
            found = new Material(shader);
        }
        materials[name] = found; return found;
    }

}
