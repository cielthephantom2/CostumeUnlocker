using System.Buffers.Binary;
using System.Net;
using System.Text.Json;
using LastEpochLAN.Core.Encounters;

namespace LastEpochLAN.Core;

public static class LanProtocol
{
    public const int Version = 41;
}

public sealed record PlayerState
{
    // Inventory transactions can remain ready while a borrowed Echo context is
    // waiting for world synchronization. This does not grant combat authority.
    public bool StashReady {get;init;}
    public bool Cooperative {get;init;}
    public string SummonEpoch {get;init;}="";
    public double SummonSampleTime {get;init;}
    public SummonState[] Summons {get;init;}=Array.Empty<SummonState>();
    public string EchoRunId {get;init;}="";
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Scene { get; init; } = "";
    public string GiftIdentity { get; init; } = "";
    public string CharacterId { get; init; } = "";
    public string GiftGroup { get; init; } = "";
    public long Sequence { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
    public float Yaw { get; init; }
    public bool Available { get; init; }
    public bool Dead { get; init; }
    public float Health { get; init; }
    public int MaxHealth { get; init; }
    public float Ward { get; init; }
    public AvatarState? Avatar { get; init; }
    public DroppedItemState[] DroppedItems { get; init; } = Array.Empty<DroppedItemState>();
    public PlayerState Copy() => this with { Avatar = Avatar?.Copy(), DroppedItems = DroppedItems.ToArray(),Summons=Summons.Select(s=>s.Copy()).ToArray() };

    public bool IsValid() => (SummonEpoch=="" ? Summons is {Length:0} && SummonSampleTime==0 : Guid.TryParseExact(SummonEpoch,"N",out _)) &&
        double.IsFinite(SummonSampleTime) && SummonSampleTime>=0 && Summons is {Length:<=SummonState.MaximumPerPlayer} &&
        Summons.All(s=>s!=null && s.IsValid()) && Summons.Select(s=>s.Id).Distinct().Count()==Summons.Length &&
        (EchoRunId=="" || Guid.TryParseExact(EchoRunId,"N",out _)) && Scene is { Length: <= 256 } && CharacterId is { Length: <= 128 } && GiftGroup is { Length: <= 96 } &&
        (GiftIdentity == "" || Guid.TryParseExact(GiftIdentity, "N", out _)) && Sequence >= 0 &&
        float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z) && float.IsFinite(Yaw) &&
        Math.Abs(X) <= 100000 && Math.Abs(Y) <= 100000 && Math.Abs(Z) <= 100000 &&
        float.IsFinite(Health) && Health >= 0 && MaxHealth is >= 0 and <= 100000000 && Health <= MaxHealth && float.IsFinite(Ward) && Ward is >= 0 and <= 100000000 &&
        (Avatar == null || Avatar.IsValid()) && DroppedItems is { Length: <= 16 } &&
        DroppedItems.All(item => item != null && item.IsValid()) && DroppedItems.Select(item => item.Id).Distinct().Count() == DroppedItems.Length;
}

public sealed record DroppedItemState
{
    public long Id { get; init; }
    public string Name { get; init; } = "";
    public string EncodedItem { get; init; } = "";
    public VisualPoint Position { get; init; } = new();
    public bool IsValid()
    {
        if (Id <= 0 || Name is not { Length: > 0 and <= 128 } || EncodedItem is not { Length: > 0 and <= 1368 } || !Position.IsValid()) return false;
        try { return Convert.FromBase64String(EncodedItem).Length is > 0 and <= 1024; }
        catch (FormatException) { return false; }
    }
}

public sealed record AvatarState
{
    public CosmeticPrefab? Footprint { get; init; }
    public CosmeticPrefab? Aura { get; init; }
    public SkillVisualState[] Visuals { get; init; } = Array.Empty<SkillVisualState>();
    public NativeMotionState? Motion { get; init; }
    public EquipmentAppearance[] Equipment { get; init; } = Array.Empty<EquipmentAppearance>();
    public string BackCosmetic { get; init; } = "";
    public int ClassId { get; init; }
    public int AnimationHash { get; init; }
    public float AnimationTime { get; init; }
    public AnimationSnapshot? Animation { get; init; }
    public AvatarState Copy() => this with { Animation = Animation?.Copy(), Equipment = Equipment.ToArray(), Visuals = Visuals.Select(visual => visual.Copy()).ToArray() };
    public bool IsValid() => (Footprint == null || Footprint.IsValid()) && (Aura == null || Aura.IsValid()) && ClassId is >= 0 and <= 4 && Visuals is { Length: <= 32 } && Visuals.All(visual => visual != null && visual.IsValid()) &&
        Visuals.Select(visual => visual.Id).Distinct().Count() == Visuals.Length && Visuals.Sum(v => v.Particles.Sum(p => p.Count)) <= 128 &&
        (Motion == null || Motion.IsValid()) && Equipment is { Length: <= 6 } && BackCosmetic is { Length: <= 256 } &&
        Equipment.All(item => item != null && item.IsValid()) && Equipment.Select(item => item.Slot).Distinct().Count() == Equipment.Length &&
        float.IsFinite(AnimationTime) && Math.Abs(AnimationTime) <= 100000 &&
        (Animation == null || Animation.IsValid());
}

public sealed record CosmeticPrefab
{
    public ulong P0 { get; init; }
    public ulong P1 { get; init; }
    public string SubPath { get; init; } = "";
    public bool IsValid() => (P0 != 0 || P1 != 0) && SubPath is { Length: <= 256 };
}

public sealed record VisualPoint
{
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
    public bool IsValid() => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z) &&
        Math.Abs(X) <= 100000 && Math.Abs(Y) <= 100000 && Math.Abs(Z) <= 100000;
}

public sealed record VisualLine
{
    public int Index { get; init; }
    public float Width { get; init; } = 0.05f;
    public VisualPoint Color { get; init; } = new() { X = 1, Y = 1, Z = 1 };
    public string Material { get; init; } = "";
    public VisualPoint[] Points { get; init; } = Array.Empty<VisualPoint>();
    public bool IsValid() => Index is >= 0 and < 4 && float.IsFinite(Width) && Width is > 0 and <= 10 &&
        Color != null && Color.IsValid() && Color.X is >= 0 and <= 16 && Color.Y is >= 0 and <= 16 && Color.Z is >= 0 and <= 16 &&
        Material is { Length: <= 128 } && Points is { Length: >= 2 and <= 16 } && Points.All(point => point != null && point.IsValid());
    public VisualLine Copy() => this with { Points = Points.ToArray() };
}

public sealed record VisualPose
{
    public int Layer { get; init; }
    public string Path { get; init; } = "";
    public VisualPoint Position { get; init; } = new();
    public VisualPoint Rotation { get; init; } = new();
    public VisualPoint Scale { get; init; } = new() { X = 1, Y = 1, Z = 1 };
    public bool Active { get; init; }
    public bool IsValid() => Layer is >= 0 and <= 31 && Path is { Length: <= 128 } && (Path == "" || Path.Split('/').All(s => int.TryParse(s, out var i) && i is >= 0 and <= 4096)) &&
        Position != null && Position.IsValid() && Rotation != null && Rotation.IsValid() && Scale != null && Scale.IsValid() &&
        Math.Abs(Scale.X) <= 1000 && Math.Abs(Scale.Y) <= 1000 && Math.Abs(Scale.Z) <= 1000;
}
public sealed record SkillVisualState
{
    public bool Ended { get; init; }
    public int Layer { get; init; }
    public VisualParticleFrame[] Particles { get; init; } = Array.Empty<VisualParticleFrame>();
    public long Id { get; init; }
    public long AbilityKey { get; init; }
    public byte VisualIndex { get; init; }
    public ulong PrefabGuid0 { get; init; }
    public ulong PrefabGuid1 { get; init; }
    public string PrefabSubPath { get; init; } = "";
    public long CastingVfxKey { get; init; }
    public VisualPoint? StretchOrigin { get; init; }
    public VisualPoint Position { get; init; } = new();
    public VisualPoint Rotation { get; init; } = new();
    public VisualPoint Scale { get; init; } = new() { X = 1, Y = 1, Z = 1 };
    public float Age { get; init; }
    public VisualLine[] Lines { get; init; } = Array.Empty<VisualLine>();
    public VisualPose[] Poses { get; init; } = Array.Empty<VisualPose>();
    public bool IsValid() => Layer is >= 0 and <= 31 && Id > 0 && AbilityKey != 0 && VisualIndex <= 63 && PrefabSubPath is { Length: <= 256 } && Position != null && Position.IsValid() &&
        Rotation != null && Rotation.IsValid() && Scale != null && Scale.IsValid() &&
        (StretchOrigin == null || StretchOrigin.IsValid()) &&
        Math.Abs(Scale.X) <= 1000 && Math.Abs(Scale.Y) <= 1000 && Math.Abs(Scale.Z) <= 1000 &&
        float.IsFinite(Age) && Age is >= 0 and <= 30 && Lines is { Length: <= 4 } &&
        Lines.All(line => line != null && line.IsValid()) && Lines.Select(line => line.Index).Distinct().Count() == Lines.Length &&
        Poses is { Length: <= 24 } && Poses.All(p => p != null && p.IsValid()) && Poses.Select(p => p.Path).Distinct().Count() == Poses.Length &&
        Particles is { Length: <= 4 } && Particles.All(p => p != null && p.IsValid()) &&
        Particles.Select(p => p.Index).Distinct().Count() == Particles.Length && Particles.Sum(p => p.Count) <= 64;
    public SkillVisualState Copy() => this with { Lines = Lines.Select(line => line.Copy()).ToArray(), Poses = Poses.ToArray(), Particles = Particles.Select(p => p.Copy()).ToArray() };
}

public sealed record VisualParticleFrame
{
    public int Index { get; init; }
    public int Count { get; init; }
    public VisualPoint ShapeScale { get; init; } = new() { X = 1, Y = 1, Z = 1 };
    public VisualPoint ShapePosition { get; init; } = new();
    public VisualPoint ShapeRotation { get; init; } = new();
    public float Size { get; init; } = 1;
    public float Speed { get; init; } = 1;
    public float Lifetime { get; init; } = 1;
    public bool IsValid() => Index is >= 0 and < 32 && Count is >= 0 and <= 32 &&
        ShapeScale != null && ShapeScale.IsValid() && ShapePosition != null && ShapePosition.IsValid() && ShapeRotation != null && ShapeRotation.IsValid() &&
        Math.Abs(ShapeScale.X) <= 1000 && Math.Abs(ShapeScale.Y) <= 1000 && Math.Abs(ShapeScale.Z) <= 1000 &&
        float.IsFinite(Size) && Size is >= 0 and <= 1000 && float.IsFinite(Speed) && Math.Abs(Speed) <= 1000 &&
        float.IsFinite(Lifetime) && Lifetime is > 0 and <= 120;
    public VisualParticleFrame Copy() => this with { };
}

public sealed record NativeMotionState
{
    public bool TemporaryIdle { get; init; }
    public byte TemporaryAnimation { get; init; }
    public float Duration { get; init; }
    public int State { get; init; }
    public byte AnimationNumber { get; init; }
    public float AnimationSpeed { get; init; } = 1;
    public float MoveSpeed { get; init; } = 1;
    public bool IsValid() => State is >= 0 and <= 9 && AnimationNumber <= 63 && TemporaryAnimation <= 63 &&
        float.IsFinite(Duration) && Duration is >= 0 and <= 30 &&
        float.IsFinite(AnimationSpeed) && AnimationSpeed is > 0 and <= 100 &&
        float.IsFinite(MoveSpeed) && MoveSpeed is >= 0 and <= 100;
}

// Logical slots: helmet/body/gloves/boots/main hand/off hand. No inventory or save data.
public sealed record EquipmentAppearance
{
    public int Slot { get; init; }
    public int EquipmentType { get; init; }
    public int SubType { get; init; }
    public bool Unique { get; init; }
    public ushort UniqueId { get; init; }
    public ushort Variation { get; init; }
    public bool IsValid() => Slot is >= 0 and <= 5 && EquipmentType is >= 0 and <= 64 && SubType is >= 0 and <= 65535;
}

public sealed record AnimationLayer
{
    public int Hash { get; init; }
    public float Time { get; init; }
    public float Weight { get; init; }
    public bool IsValid() => float.IsFinite(Time) && Math.Abs(Time) <= 100000 && float.IsFinite(Weight) && Weight is >= 0 and <= 1;
}

public sealed record AnimationSnapshot
{
    public float Speed { get; init; } = 1;
    public AnimationLayer[] Layers { get; init; } = Array.Empty<AnimationLayer>();
    public AnimationParameter[] Parameters { get; init; } = Array.Empty<AnimationParameter>();
    public bool IsValid() => float.IsFinite(Speed) && Math.Abs(Speed) <= 100 && Layers is { Length: <= 8 } &&
        Layers.All(layer => layer != null && layer.IsValid()) && Parameters is { Length: <= 64 } &&
        Parameters.All(parameter => parameter != null && parameter.IsValid()) &&
        Parameters.Select(parameter => parameter.Hash).Distinct().Count() == Parameters.Length;
    public AnimationSnapshot Copy() => this with { Layers = Layers.ToArray(), Parameters = Parameters.ToArray() };
}

// Unity AnimatorControllerParameterType values; triggers are transient and are
// intentionally excluded from persistent snapshots.
public sealed record AnimationParameter
{
    public int Hash { get; init; }
    public int Type { get; init; }
    public float Float { get; init; }
    public int Int { get; init; }
    public bool Bool { get; init; }
    public bool IsValid() => Type is 1 or 3 or 4 && float.IsFinite(Float) && Math.Abs(Float) <= 100000;
}

internal sealed record Packet
{
    public RoomLootFilter[]? LootFilters { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public int ReceivedBytes { get; init; }
    public HudConfiguration? Hud { get; init; }
    public string Kind { get; init; } = "";
    public int Protocol { get; init; } = LanProtocol.Version;
    public string Build { get; init; } = "";
    public string Code { get; init; } = "";
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Error { get; init; } = "";
    public PlayerState? State { get; init; }
    public PlayerState[]? Players { get; init; }
    public WorldState? World { get; init; }
    public PartyEchoState? Echo {get;init;}
    public WorldChunk? WorldChunk { get; init; }
    public DeathReceipt[]? DeathReceipts { get; init; }
    public HitRequest[]? Hits { get; init; }
    public StatusVisualReport[]? StatusVisuals {get;init;}
    public StatusCallback[]? StatusCallbacks {get;init;}
    public SkillEvent[]? Skills { get; init; }
    public IncomingDamage[]? IncomingDamage { get; init; }
    public GiftMessage[]? Gifts { get; init; }
    public ChestCommand[]? ChestCommands {get;init;}
    public ChestReply[]? ChestReplies {get;init;}
    public EncounterCommand[]? EncounterCommands {get;init;}
    public EncounterReply[]? EncounterReplies {get;init;}
    public EncounterDelivery? Encounters {get;init;}
}

public sealed record SkillEvent
{
    public NativeVisualCommand? NativeVisual { get; init; }
    public int AnimationNumber { get; init; } = -1;
    public float AnimationSpeed { get; init; } = 1;
    // -1 is a legacy visual event; native commands must resolve a known bar slot.
    public int AbilitySlot { get; init; } = -1;
    public bool ZoneAbility { get; init; }
    public string PlayerId { get; init; } = "";
    public string Scene { get; init; } = "";
    public string Ability { get; init; } = "";
    public long Sequence { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
    public bool IsValid() => AnimationNumber is >= -1 and <= 63 && float.IsFinite(AnimationSpeed) && AnimationSpeed is > 0 and <= 100 &&
        AbilitySlot is >= -1 and <= 4 && (!ZoneAbility || AbilitySlot == 0) &&
        Sequence > 0 && Ability is { Length: > 0 and <= 256 } &&
        new PlayerState { Scene = Scene, X = X, Y = Y, Z = Z }.IsValid() && (NativeVisual == null || NativeVisual.IsValid());
}
public sealed record NativeVisualCommand
{
    public ulong Guid0 { get; init; }
    public ulong Guid1 { get; init; }
    public string SubPath { get; init; } = "";
    public long AbilityKey { get; init; }
    public VisualPoint Position { get; init; } = new();
    public VisualPoint Rotation { get; init; } = new();
    public VisualPoint Scale { get; init; } = new() { X = 1, Y = 1, Z = 1 };
    public VisualPoint? Origin { get; init; }
    public VisualPoint? Start { get; init; }
    public VisualPoint TargetOffset { get; init; } = new();
    public VisualPoint? Target { get; init; }
    public bool Mutable { get; init; }
    public bool ApplyMutation { get; init; }
    public bool StretchFromOrigin { get; init; }
    public bool UseAutoPool { get; init; } = true;
    public float[] Modifiers { get; init; } = new float[7];
    public int ToggleEnum { get; init; }
    public ulong ToggleMask { get; init; }
    public ulong TimingMode { get; init; }
    public ulong TimingLo { get; init; }
    public ulong TimingHi { get; init; }
    public bool IsValid() => (Guid0 != 0 || Guid1 != 0) && SubPath is { Length: <= 256 } &&
        Position != null && Position.IsValid() && Rotation != null && Rotation.IsValid() && Scale != null && Scale.IsValid() &&
        Math.Abs(Scale.X) <= 1000 && Math.Abs(Scale.Y) <= 1000 && Math.Abs(Scale.Z) <= 1000 &&
        (Origin == null || Origin.IsValid()) && (Start == null || Start.IsValid()) && (Target == null || Target.IsValid()) &&
        ((Start == null) == (Target == null)) && (!StretchFromOrigin || Origin != null && Target == null && !Mutable) && TargetOffset != null && TargetOffset.IsValid() &&
        Modifiers is { Length: 7 } && Modifiers.All(v => float.IsFinite(v) && v is >= -1 and <= 1000);
    public NativeVisualCommand Copy() => this with { Modifiers = Modifiers.ToArray() };
}

public sealed record EnemyState
{
    public StatusStat[] StatusModifiers {get;init;}=Array.Empty<StatusStat>();
    public StatusCounters StatusCounters {get;init;}=new();
    public AilmentCount[] Ailments {get;init;}=Array.Empty<AilmentCount>();
    public int FreezeMaterial { get; init; }
    public bool Stunned { get; init; }
    public VisualPoint? VisualPosition { get; init; }
    public bool Boss { get; init; }
    public float Ward { get; init; }
    public int Id { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string Template { get; init; } = "";
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
    public float Yaw { get; init; }
    public float Health { get; init; }
    public float SpawnX { get; init; }
    public float SpawnY { get; init; }
    public float SpawnZ { get; init; }
    public int MaxHealth { get; init; }
    public bool Dead { get; init; }
    public AnimationSnapshot? Animation { get; init; }
    public NativeMotionState? Motion { get; init; }
    public SkillVisualState[] Visuals { get; init; } = Array.Empty<SkillVisualState>();
    public long AttackSequence { get; init; }
    public EnemyState Copy() => this with { StatusModifiers=StatusModifiers.Select(v=>v.Copy()).ToArray(),Ailments=Ailments.ToArray(),Animation = Animation?.Copy(), Visuals = Visuals.Select(visual => visual.Copy()).ToArray() };
    public bool IsValid() => FreezeMaterial is >= 0 and <= 3 && (VisualPosition == null || VisualPosition.IsValid()) && Id > 0 && Template is { Length: > 0 and <= 256 } &&
        StateValidation.Position(X,Y,Z) && float.IsFinite(Yaw) && StateValidation.Position(SpawnX,SpawnY,SpawnZ) &&
        float.IsFinite(Health) && Health >= 0 && MaxHealth > 0 && Health <= MaxHealth && float.IsFinite(Ward) && Ward is >= 0 and <= 100000000 && (Animation == null || Animation.IsValid()) &&
        StatusCounters!=null && StatusCounters.IsValid() && StatusModifiers is {Length:<=4096} && StatusModifiers.All(v=>v!=null && v.IsValid()) &&
        Ailments is {Length:<=512} && Ailments.All(a=>a!=null && a.IsValid()) && StateValidation.UniqueAilments(Ailments) &&
        AttackSequence >= 0 && (Motion == null || Motion.IsValid()) && Visuals is { Length: <= 8 } && Visuals.All(visual => visual != null && visual.IsValid()) &&
        StateValidation.UniqueVisuals(Visuals);
}

public sealed record WorldState
{
    public string EchoRunId {get;init;}="";
    public bool AcceptsResolvedAilments {get;init;}
    public int[] RewardEnemyIds { get; init; } = Array.Empty<int>();
    public double SampleTime { get; init; }
    public int BossId { get; init; }
    public MapObjectState[] MapObjects { get; init; } = Array.Empty<MapObjectState>();
    public string Scene { get; init; } = "";
    public long Epoch { get; init; }
    public long Revision { get; init; }
    public EnemyState[] Enemies { get; init; } = Array.Empty<EnemyState>();
    public SceneGraphState[] SceneGraphs { get; init; } = Array.Empty<SceneGraphState>();
    public MapTriggerState[] MapTriggers { get; init; } = Array.Empty<MapTriggerState>();
    public bool IsValid() => (EchoRunId=="" || Guid.TryParseExact(EchoRunId,"N",out _)) && double.IsFinite(SampleTime) && SampleTime is >= 0 and <= 1e12 && Scene is { Length: <= 256 } && Epoch >= 0 && Revision >= 0 && BossId >= 0 &&
        RewardEnemyIds is { Length: <= WorldTransfer.MaximumEnemies } && RewardEnemyIds.All(id=>id>0) && RewardEnemyIds.Distinct().Count() == RewardEnemyIds.Length &&
        MapObjects is { Length: <= 256 } && MapObjects.All(o => o != null && o.IsValid()) && MapObjects.Select(o => o.Path).Distinct().Count() == MapObjects.Length &&
        Enemies is { Length: <= WorldTransfer.MaximumEnemies } && Enemies.All(e => e != null && e.IsValid()) &&
        (BossId == 0 || Enemies.Any(e => e.Id == BossId && e.Boss && !e.Dead)) &&
        Enemies.Select(e => e.Id).Distinct().Count() == Enemies.Length && Enemies.Sum(enemy => enemy.Visuals.Length) <= 64 && SceneGraphs is { Length: <= 128 } &&
        SceneGraphs.All(graph => graph != null && graph.IsValid()) && SceneGraphs.Select(graph => graph.Path).Distinct().Count() == SceneGraphs.Length &&
        MapTriggers is { Length: <= 128 } && MapTriggers.All(trigger => trigger != null && trigger.IsValid()) &&
        MapTriggers.Select(trigger => (trigger.Kind, trigger.Path)).Distinct().Count() == MapTriggers.Length;
    public WorldState Copy() => this with { RewardEnemyIds = RewardEnemyIds.ToArray(), Enemies = Enemies.Select(enemy => enemy.Copy()).ToArray(), SceneGraphs = SceneGraphs.ToArray(), MapTriggers = MapTriggers.ToArray(), MapObjects = MapObjects.ToArray() };
}

public sealed record MapObjectState
{
    public string Path { get; init; } = "";
    public bool Exists { get; init; } = true;
    public bool Active { get; init; }
    // Compact hierarchy digest keeps combined enemy/VFX/event frames bounded.
    public bool IsValid() => Path is { Length: 64 } && Path.All(Uri.IsHexDigit) && (Exists || !Active);
}

public sealed record MapTriggerState
{
    public string Path { get; init; } = "";
    public int Kind { get; init; }
    public long Sequence { get; init; }
    public VisualPoint Position { get; init; } = new();
    public bool IsValid() => Path is { Length: > 0 and <= 1024 } && Kind is 0 or 1 && Sequence > 0 && Position.IsValid();
}

public sealed record SceneGraphState
{
    public string Path { get; init; } = "";
    public string Asset { get; init; } = "";
    public int Node { get; init; }
    public int NodeCount { get; init; }
    public bool IsValid() => Path is { Length: > 0 and <= 1024 } && Asset is { Length: > 0 and <= 256 } &&
        Node is >= 0 and <= 255 && NodeCount is > 0 and <= 256 && Node < NodeCount;
}

public sealed record HitRequest
{
    // Already calculated on the source PC. This is not a request to recreate DoT.
    public bool SourceDot {get;init;}
    public ResolvedAilment? Ailment {get;init;}
    public int ControlType { get; init; } = -1;
    public float ControlDuration { get; init; }
    public float ControlAngle { get; init; }
    public string PlayerId { get; init; } = "";
    public long Sequence { get; init; }
    public long Epoch { get; init; }
    public int EnemyId { get; init; }
    public float Damage { get; init; }
    public bool Cull { get; init; }
    public float CullPercent { get; init; }
    public float FlatCull { get; init; }
    public bool IsValid() => Sequence > 0 && Epoch >= 0 && EnemyId > 0 &&
        (!SourceDot || Ailment==null && ControlType==-1) &&
        float.IsFinite(Damage) && Damage >= 0 && Damage <= 100000000 &&
        ControlType is >= -1 and <= 3 && float.IsFinite(ControlDuration) && ControlDuration is >= 0 and <= 30 && float.IsFinite(ControlAngle) && Math.Abs(ControlAngle) <= 360 &&
        (Ailment!=null ? Ailment.IsValid() && Damage==0 && ControlType==-1 && ControlDuration==0 && !Cull && CullPercent==0 && FlatCull==0 :
            ControlType < 0 ? Damage > 0 && ControlDuration == 0 : Damage == 0 && ControlDuration > 0 && !Cull) &&
        float.IsFinite(CullPercent) && CullPercent >= 0 && CullPercent <= 100 &&
        float.IsFinite(FlatCull) && FlatCull >= 0 && FlatCull <= 100000000;
}

internal static class Wire
{
    internal const int MaximumFrameBytes = 1048576;
    // Packet -> player -> avatar -> visual -> line -> point needs more nesting.
    private static readonly JsonSerializerOptions JsonOptions = new() { MaxDepth = 40 };

    public static async Task<int> WriteAsync(Stream stream, Packet packet, CancellationToken cancellation)
    {
        using var buffer=new PooledFrameBuffer();
        using(var writer=new Utf8JsonWriter(buffer)) JsonSerializer.Serialize(writer,packet,JsonOptions);
        var frame=buffer.Frame();
        await stream.WriteAsync(frame,cancellation).ConfigureAwait(false);
        return frame.Length;
    }

    public static async Task<Packet> ReadAsync(Stream stream, CancellationToken cancellation)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        byte[] header = new byte[4];
        await ReadExactlyAsync(stream, header, deadline.Token).ConfigureAwait(false);
        int size = BinaryPrimitives.ReadInt32BigEndian(header);
        if (size < 2 || size > MaximumFrameBytes) throw new InvalidDataException("Invalid frame size.");
        byte[] payload=System.Buffers.ArrayPool<byte>.Shared.Rent(size);
        try
        {
            await ReadExactlyAsync(stream,payload.AsMemory(0,size),deadline.Token).ConfigureAwait(false);
            return (JsonSerializer.Deserialize<Packet>(payload.AsSpan(0,size),JsonOptions)??throw new InvalidDataException("Empty packet.")) with {ReceivedBytes=size};
        }
        finally {System.Buffers.ArrayPool<byte>.Shared.Return(payload);}
    }

    private static async Task ReadExactlyAsync(Stream stream, Memory<byte> data, CancellationToken cancellation)
    {
        int offset = 0;
        while (offset < data.Length)
        {
            int count = await stream.ReadAsync(data.Slice(offset), cancellation).ConfigureAwait(false);
            if (count == 0) throw new EndOfStreamException("Peer disconnected.");
            offset += count;
        }
    }
}

public static class LanAddress
{
    public static bool IsLocal(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return true;
        byte[] bytes = address.GetAddressBytes();
        return bytes.Length == 4 && (bytes[0] == 10 ||
            (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
            (bytes[0] == 192 && bytes[1] == 168) || (bytes[0] == 169 && bytes[1] == 254));
    }
}
