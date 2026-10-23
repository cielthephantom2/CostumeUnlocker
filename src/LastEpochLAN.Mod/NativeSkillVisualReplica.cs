using LastEpochLAN.Core;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace LastEpochLAN.Mod;

internal sealed partial class NativeSkillVisualReplica : IDisposable
{
    private sealed record Visual(GameObject Root, GameObject PrefabRoot, LineRenderer[] Lines, Il2Cpp.SubObjectsStretchToLocationCreatedFrom[] Stretchers, float PlayUntil, ParticleSystem[] Particles)
    {
        internal Vector3 SourcePosition, Velocity;
        internal Quaternion SourceRotation;
        internal float SourceAge = -1, ReceivedAt;
    }
    private readonly bool smoothMotion;
    private int renderedFrame = -1;
    internal NativeSkillVisualReplica(bool smoothMotion = false) { this.smoothMotion = smoothMotion; }
    private static readonly Dictionary<string, Material> materials = new();
    private readonly Dictionary<long, Visual> objects = new();
    private readonly Dictionary<long, float> retryAt = new();
    internal string Error { get; private set; } = "";
    internal long Created { get; private set; }
    internal long Received { get; private set; }
    internal long Failed { get; private set; }
    internal long ParticleFramesApplied { get; private set; }
    internal long ParticleValuesApplied { get; private set; }
    internal long PresentationScriptsRetained { get; private set; }
    internal string LastCreated { get; private set; } = "";
    internal string LastSnapshot { get; private set; } = "";
    private HashSet<long> activeIds = new();
    private readonly List<long> removals=new();

    internal void Tick(bool render = true)
    {
        if (objects.Count == 0) return;
        bool advance=smoothMotion && render && renderedFrame!=Time.frameCount;
        if(advance) renderedFrame=Time.frameCount;
        float blend=advance ? 1-Mathf.Exp(-40*Time.unscaledDeltaTime) : 0;
        removals.Clear();
        foreach(var pair in objects)
        {
            var visual=pair.Value;
            // Unity can destroy the native object while its managed wrapper is
            // still in our dictionary (scene unload or a self-ending prefab).
            if(!(bool)visual.Root || !(bool)visual.PrefabRoot ||
                (!activeIds.Contains(pair.Key) && (smoothMotion || Time.unscaledTime>=visual.PlayUntil)))
            {removals.Add(pair.Key);continue;}
            if(!advance || !activeIds.Contains(pair.Key) || visual.SourceAge<0) continue;
            try
            {
                var target = visual.SourcePosition + visual.Velocity * Mathf.Clamp(Time.unscaledTime - visual.ReceivedAt, 0, 0.08f);
                visual.Root.transform.position = Vector3.Lerp(visual.Root.transform.position, target, blend);
                visual.Root.transform.rotation = Quaternion.Slerp(visual.Root.transform.rotation, visual.SourceRotation, blend);
            }
            catch(Exception ex) {RecordFailure(ex);removals.Add(pair.Key);}
        }
        foreach (var id in removals)
        {
            if(objects.Remove(id,out var visual)) Release(visual);
            if(activeIds.Contains(id)) retryAt[id]=Time.unscaledTime+1;
            else retryAt.Remove(id);
        }
    }

    private void RecordFailure(Exception ex) {Failed++;Error=ex.ToString();}
    private void Release(Visual visual)
    {
        try {if((bool)visual.Root) visual.Root.SetActive(false);}
        catch(Exception ex) {RecordFailure(ex);}
        // Remove tracking before releasing; a failed deactivate must not leave
        // a poisoned entry that breaks the next snapshot's cleanup as well.
        try {if((bool)visual.Root) UObject.Destroy(visual.Root);}
        catch(Exception ex) {RecordFailure(ex);}
    }

#if LELAN_QA

    internal static void NativeMotionSmoke()
    {
        var root = new GameObject("LAN projectile smoothing QA"); root.SetActive(false);
        try
        {
            var replica = new NativeSkillVisualReplica(true);
            replica.activeIds.Add(1);
            replica.objects.Add(1, new Visual(root, root, Array.Empty<LineRenderer>(), Array.Empty<Il2Cpp.SubObjectsStretchToLocationCreatedFrom>(), Time.unscaledTime + 10, Array.Empty<ParticleSystem>())
            { SourcePosition = Vector3.right, SourceRotation = Quaternion.identity, SourceAge = 0, ReceivedAt = Time.unscaledTime });
            replica.Tick(false);
            if (root.transform.position != Vector3.zero) throw new InvalidOperationException("Snapshot cleanup advanced projectile presentation.");
            replica.Tick(); var first = root.transform.position;
            replica.Tick();
            if (root.transform.position != first || first.x < 0 || first.x > 1)
                throw new InvalidOperationException("Projectile presentation advanced twice within one frame.");
            var tracked=replica.objects[1];tracked.SourceAge=1;tracked.SourcePosition=Vector3.right;tracked.SourceRotation=Quaternion.identity;
            tracked.ReceivedAt=Time.unscaledTime-0.05f;float received=tracked.ReceivedAt;
            replica.Apply(new[]{new SkillVisualState {Id=1,AbilityKey=1,Age=1,Position=new() {X=1}}});
            if(tracked.ReceivedAt!=received) throw new InvalidOperationException("Repeated projectile sample reset its prediction clock.");
            var ended = new SkillVisualState { Id = 1, AbilityKey = 1, Ended = true };
            replica.Apply(new[] { ended });
            if (root.activeSelf || replica.objects.Count != 0)
                throw new InvalidOperationException("Ended projectile retained its minimum display lifetime.");
            replica.Apply(new[] { ended });
            if (replica.Created != 0 || replica.retryAt.Count != 0)
                throw new InvalidOperationException("Repeated terminal snapshot recreated a projectile.");
            var missing = new GameObject("LAN absent projectile QA"); missing.SetActive(false);
            replica.objects.Add(2, new Visual(missing, missing, Array.Empty<LineRenderer>(), Array.Empty<Il2Cpp.SubObjectsStretchToLocationCreatedFrom>(), Time.unscaledTime + 10, Array.Empty<ParticleSystem>()));
            replica.Apply(Array.Empty<SkillVisualState>());
            if (replica.objects.Count != 0)
                throw new InvalidOperationException("Absent NPC projectile retained its minimum display lifetime.");
            var destroyed=new GameObject("LAN destroyed projectile QA");
            replica.objects.Add(3,new Visual(destroyed,destroyed,Array.Empty<LineRenderer>(),Array.Empty<Il2Cpp.SubObjectsStretchToLocationCreatedFrom>(),Time.unscaledTime+10,Array.Empty<ParticleSystem>()));
            UObject.DestroyImmediate(destroyed);
            replica.Apply(new[]{new SkillVisualState {Id=3,AbilityKey=1,Ended=true}});
            replica.Tick();replica.Dispose();replica.Dispose();
            if(replica.objects.Count!=0 || replica.Failed!=0)
                throw new InvalidOperationException("Destroyed native projectile wrapper broke terminal cleanup.");
        }
        finally { UObject.Destroy(root); }
    }
#endif

    internal void Apply(SkillVisualState[] states)
    {
        Received += states.Length;
        foreach (var state in states.Where(state => state.Ended))
        {
            if (objects.Remove(state.Id, out var ended))
                Release(ended);
            retryAt.Remove(state.Id);
        }
        var ids = states.Where(state => !state.Ended).Select(state => state.Id).ToHashSet();
        activeIds = ids; Tick(false);
        foreach (var id in retryAt.Keys.Where(id => !ids.Contains(id)).ToArray()) retryAt.Remove(id);
        foreach (var state in states)
        {
            if (state.Ended) continue;
            try
            {
                if (!objects.TryGetValue(state.Id, out var visual))
                {
                    if (Time.unscaledTime < retryAt.GetValueOrDefault(state.Id)) continue;
                    LastSnapshot = $"ability={state.AbilityKey}; prefab={state.PrefabGuid0:X}:{state.PrefabGuid1:X}; casting={state.CastingVfxKey}; poses={state.Poses.Length}; lines={state.Lines.Length}; scale={state.Scale.X},{state.Scale.Y},{state.Scale.Z}";
                    retryAt[state.Id] = Time.unscaledTime + 1;
                    visual = Create(state); objects[state.Id] = visual; retryAt.Remove(state.Id); Created++;
                    LastCreated = $"ability={state.AbilityKey}; layer={state.Layer}; renderers={visual.Root.GetComponentsInChildren<Renderer>(true).Length}; particleSystems={visual.Particles.Length}; pos={state.Position.X},{state.Position.Y},{state.Position.Z}";
                }
                var position = NativeSkillVisualCapture.Vector(state.Position);
                var rotation = Quaternion.Euler(NativeSkillVisualCapture.Vector(state.Rotation));
                if (!smoothMotion || visual.SourceAge < 0 || (position - visual.SourcePosition).sqrMagnitude > 400)
                { visual.Root.transform.position = position; visual.Root.transform.rotation = rotation; visual.Velocity = Vector3.zero; }
                else if (state.Age > visual.SourceAge)
                    visual.Velocity = Vector3.ClampMagnitude((position - visual.SourcePosition) / (state.Age - visual.SourceAge), 200);
                // An unchanged delta/retry carries the old sample age. It
                // must not rewind the extrapolation clock on every retry.
                if(state.Age>visual.SourceAge || position!=visual.SourcePosition || rotation!=visual.SourceRotation)
                {visual.SourcePosition = position; visual.SourceRotation = rotation;visual.SourceAge = state.Age; visual.ReceivedAt = Time.unscaledTime;}
                visual.Root.transform.localScale = NativeSkillVisualCapture.Vector(state.Scale);
                visual.PrefabRoot.layer = state.Layer;
                ApplyPoses(visual.PrefabRoot, state.Poses);
                foreach (var frame in state.Particles)
                {
                    if (frame.Index >= visual.Particles.Length || !(bool)visual.Particles[frame.Index]) continue;
                    var system = visual.Particles[frame.Index]; var main = system.main;
                    main.startSizeMultiplier = frame.Size; main.startSpeedMultiplier = frame.Speed; main.startLifetimeMultiplier = frame.Lifetime;
                    var shape = system.shape;
                    shape.scale = NativeSkillVisualCapture.Vector(frame.ShapeScale);
                    shape.position = NativeSkillVisualCapture.Vector(frame.ShapePosition);
                    shape.rotation = NativeSkillVisualCapture.Vector(frame.ShapeRotation);
                    int emit = Math.Clamp(frame.Count - system.particleCount, 0, 32);
                    if (emit > 0) { system.Emit(emit); ParticleValuesApplied += emit; }
                    ParticleFramesApplied++;
                }
                if (state.StretchOrigin != null)
                    foreach (var stretcher in visual.Stretchers)
                    {
                        if(!(bool)stretcher) continue;
                        stretcher.useTransformInsteadOfLocationCreatedFrom = null;
                        stretcher.setLocation(NativeSkillVisualCapture.Vector(state.StretchOrigin));
                        stretcher.OnUpdateTick(Time.deltaTime);
                    }
                foreach (var line in state.Lines)
                {
                    if (line.Index >= visual.Lines.Length) continue;
                    var renderer = visual.Lines[line.Index]; if(!(bool)renderer) continue;
                    renderer.useWorldSpace = true;
                    renderer.enabled = true; renderer.gameObject.SetActive(true);
                    renderer.startWidth = renderer.endWidth = line.Width;
                    renderer.startColor = renderer.endColor = new Color(line.Color.X, line.Color.Y, line.Color.Z, 1);
                    if (renderer.sharedMaterial == null) renderer.sharedMaterial = MaterialFor(line.Material);
                    renderer.positionCount = line.Points.Length;
                    for (int i = 0; i < line.Points.Length; i++) renderer.SetPosition(i, NativeSkillVisualCapture.Vector(line.Points[i]));
                }
            }
            catch (Exception ex)
            {
                RecordFailure(ex);
                if(objects.Remove(state.Id,out var broken)) Release(broken);
                retryAt[state.Id]=Time.unscaledTime+1;
            }
        }
    }

    public void Dispose()
    {
        foreach (var visual in objects.Values) Release(visual);
        objects.Clear(); retryAt.Clear(); activeIds.Clear();
    }
}
