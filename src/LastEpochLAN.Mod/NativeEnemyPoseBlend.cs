using LastEpochLAN.Core;
using UnityEngine;
using NumericsVector = System.Numerics.Vector3;

namespace LastEpochLAN.Mod;

internal sealed class NativeEnemyPoseBlend
{
    private sealed class Pose
    {
        internal Transform Gameplay, Sync, Visual;
        internal readonly BufferedMotion Buffer = new();
        internal readonly BufferedMotion VisualBuffer = new();
        internal Vector3 Written;
        internal float WrittenYaw;
        internal Vector3 VisualWritten;
        internal float VisualYaw;
        internal bool Moving;
    }
    private readonly Dictionary<int, Pose> entries = new();
    private readonly List<int> removed = new();
    private readonly SnapshotPresentationClock clock = new(true);
    private double sampleTime;
    internal int Count => entries.Count;
    internal double SampleTime => sampleTime;
    internal double Delay=>clock.Delay;
    internal double RenderTime { get; private set; }
    internal bool IsMoving(int id)=>entries.TryGetValue(id,out var pose) && pose.Moving;
    internal void Observe(double time, double arrival)
    {
        if (time <= 0) time = arrival > 0 ? arrival : LanSession.MonotonicSeconds;
        if (arrival <= 0) arrival = LanSession.MonotonicSeconds;
        if (clock.Observe(time, arrival)) sampleTime = time;
    }
    internal void Receive(int id, Il2Cpp.Actor actor, Vector3 target, Quaternion rotation, bool first, Vector3? visualPosition = null)
    {
        if (sampleTime <= 0) Observe(0, 0);
        if (!entries.TryGetValue(id, out var pose))
        {
            var sync = actor.GetActorSync();
            pose = new Pose { Gameplay = actor.transform, Sync = sync?.transform,
                Visual = sync?.actorVisuals?.transform, Written = actor.transform.position,
                WrittenYaw = actor.transform.eulerAngles.y };
            entries.Add(id, pose); first = true;
        }
        float yaw = rotation.eulerAngles.y;
        if (first || (target - pose.Written).sqrMagnitude > 64)
        {
            pose.Buffer.Clear();
            pose.VisualBuffer.Clear();
            Write(pose, target, yaw);
        }
        pose.Buffer.Push(new MotionSample(sampleTime, new NumericsVector(target.x, target.y, target.z), yaw));
        var visual = visualPosition ?? target;
        pose.VisualBuffer.Push(new MotionSample(sampleTime, new NumericsVector(visual.x, visual.y, visual.z), yaw));
    }
    internal void Tick()
    {
        double renderTime = RenderTime = clock.RenderTime(LanSession.MonotonicSeconds);
        removed.Clear();
        foreach (var pair in entries)
        {
            var pose = pair.Value;
            if (!(bool)pose.Gameplay) { removed.Add(pair.Key); continue; }
            var value = pose.Buffer.Sample(renderTime);
            var position = new Vector3(value.Position.X, value.Position.Y, value.Position.Z);
            pose.Moving=(position-pose.Written).sqrMagnitude>0.00000025f;
            if ((position - pose.Written).sqrMagnitude > 0.000001f || Mathf.Abs(Mathf.DeltaAngle(pose.WrittenYaw, value.Yaw)) > 0.01f)
                Write(pose, position, value.Yaw);
            if ((bool)pose.Visual && pose.Visual.Pointer != pose.Gameplay.Pointer && pose.Visual.Pointer != pose.Sync?.Pointer)
            {
                var visual = pose.VisualBuffer.Sample(renderTime);
                var positionVisual=new Vector3(visual.Position.X, visual.Position.Y, visual.Position.Z);
                if((positionVisual-pose.VisualWritten).sqrMagnitude>0.000001f || Mathf.Abs(Mathf.DeltaAngle(pose.VisualYaw,visual.Yaw))>0.01f)
                {pose.Visual.SetPositionAndRotation(positionVisual,Quaternion.Euler(0,visual.Yaw,0));pose.VisualWritten=positionVisual;pose.VisualYaw=visual.Yaw;}
            }
        }
        foreach (var id in removed) entries.Remove(id);
    }
    private static void Write(Pose pose, Vector3 position, float yaw)
    {
        var rotation = Quaternion.Euler(0, yaw, 0);
        pose.Gameplay.SetPositionAndRotation(position, rotation);
        if ((bool)pose.Sync && pose.Sync.Pointer != pose.Gameplay.Pointer) pose.Sync.SetPositionAndRotation(position, rotation);
        if ((bool)pose.Visual && pose.Visual.Pointer != pose.Gameplay.Pointer && pose.Visual.Pointer != pose.Sync?.Pointer)
            pose.Visual.SetPositionAndRotation(position, rotation);
        pose.Written = position; pose.WrittenYaw = yaw;
        pose.VisualWritten=position;pose.VisualYaw=yaw;
    }
    internal void Remove(int id) => entries.Remove(id);
    internal void Clear() { entries.Clear(); removed.Clear(); clock.Clear(); sampleTime = 0; }
    internal static void NativeSmoke()
    {
        var root = new GameObject("LAN buffered enemy pose QA"); root.SetActive(false);
        try
        {
            var actor = root.AddComponent<Il2Cpp.Actor>(); actor.tForm = root.transform;
            var blend = new NativeEnemyPoseBlend();
            double now = LanSession.MonotonicSeconds;
            blend.Observe(100, now); blend.Receive(1, actor, Vector3.zero, Quaternion.identity, true);
            blend.Observe(100.1, now + 0.1); blend.Receive(1, actor, Vector3.right, Quaternion.identity, false);
            if (actor.transform.position != Vector3.zero || blend.Count != 1) throw new InvalidOperationException("Enemy pose jumped at packet arrival.");
            blend.Tick();
            if (actor.transform.position.x >= 0.5f) throw new InvalidOperationException("Enemy pose bypassed the presentation buffer.");
            blend.Receive(1, actor, Vector3.right * 20, Quaternion.identity, false);
            if (actor.transform.position.x != 20 || blend.entries[1].Buffer.Count != 1) throw new InvalidOperationException("Enemy teleport did not reset buffered samples.");
        }
        finally { UnityEngine.Object.Destroy(root); }
    }
}
