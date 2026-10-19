namespace LastEpochLAN.Core;

// Animation phase and effect age advance locally; they do not by themselves
// change world authority. A periodic heartbeat corrects long-term drift.
public static class WorldContent
{
    public static bool Same(WorldState? a, WorldState b)
    {
        if (a == null || a.EchoRunId!=b.EchoRunId || a.AcceptsResolvedAilments!=b.AcceptsResolvedAilments || a.Scene != b.Scene || a.Epoch != b.Epoch || a.BossId != b.BossId || !a.MapObjects.SequenceEqual(b.MapObjects) || a.Enemies.Length != b.Enemies.Length ||
            !a.SceneGraphs.SequenceEqual(b.SceneGraphs) || !a.MapTriggers.SequenceEqual(b.MapTriggers)) return false;
        for (int i = 0; i < a.Enemies.Length; i++)
            if(!EnemySame(a.Enemies[i],b.Enemies[i])) return false;
        return true;
    }
    public static bool EnemySame(EnemyState x,EnemyState y)
    {
            if(!x.Ailments.SequenceEqual(y.Ailments)) return false;
            if(x.StatusCounters!=y.StatusCounters) return false;
            if(x.StatusModifiers.Length!=y.StatusModifiers.Length) return false;
            for(int i=0;i<x.StatusModifiers.Length;i++) if(!x.StatusModifiers[i].Same(y.StatusModifiers[i])) return false;
            if (x.Id != y.Id || x.Template != y.Template || x.X != y.X || x.Y != y.Y || x.Z != y.Z || x.Yaw != y.Yaw ||
                x.FreezeMaterial != y.FreezeMaterial || x.Stunned != y.Stunned || x.VisualPosition != y.VisualPosition ||
                x.SpawnX != y.SpawnX || x.SpawnY != y.SpawnY || x.SpawnZ != y.SpawnZ ||
                x.Health != y.Health || x.MaxHealth != y.MaxHealth || x.Ward != y.Ward || x.Boss != y.Boss || x.Dead != y.Dead || x.Motion != y.Motion ||
                x.AttackSequence != y.AttackSequence || x.Visuals.Length != y.Visuals.Length || !AnimationSame(x.Animation, y.Animation)) return false;
            for (int j = 0; j < x.Visuals.Length; j++)
            {
                var p = x.Visuals[j]; var q = y.Visuals[j];
                if (p.Id != q.Id || p.Ended != q.Ended || p.Layer != q.Layer || p.AbilityKey != q.AbilityKey || p.VisualIndex != q.VisualIndex || p.CastingVfxKey != q.CastingVfxKey ||
                    p.PrefabGuid0 != q.PrefabGuid0 || p.PrefabGuid1 != q.PrefabGuid1 || p.PrefabSubPath != q.PrefabSubPath ||
                    p.Position != q.Position || p.Rotation != q.Rotation || p.Scale != q.Scale || p.StretchOrigin != q.StretchOrigin ||
                    !p.Poses.SequenceEqual(q.Poses) || p.Lines.Length != q.Lines.Length || p.Particles.Length != q.Particles.Length) return false;
                for (int k = 0; k < p.Particles.Length; k++)
                    if (p.Particles[k] != q.Particles[k]) return false;
                for (int k = 0; k < p.Lines.Length; k++)
                {
                    var l = p.Lines[k]; var r = q.Lines[k];
                    if (l.Index != r.Index || l.Width != r.Width || l.Color != r.Color || l.Material != r.Material || !l.Points.SequenceEqual(r.Points)) return false;
                }
            }
        return true;
    }
    private static bool AnimationSame(AnimationSnapshot? a, AnimationSnapshot? b)
    {
        if (a == null || b == null) return a == b;
        if (a.Speed != b.Speed || a.Layers.Length != b.Layers.Length || !a.Parameters.SequenceEqual(b.Parameters)) return false;
        for (int i = 0; i < a.Layers.Length; i++) if (a.Layers[i].Hash != b.Layers[i].Hash || a.Layers[i].Weight != b.Layers[i].Weight) return false;
        return true;
    }
}
