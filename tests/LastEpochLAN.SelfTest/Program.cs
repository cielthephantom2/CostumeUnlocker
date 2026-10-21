using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using LastEpochLAN.Core;

if(args.Length>0 && args[0]=="--encounter-tests")
{
    int encounterCheckCount=0;
    void Verify(bool ok,string label){if(!ok)throw new Exception("FAIL: "+label);encounterCheckCount++;Console.WriteLine("PASS: "+label);}
    await EncounterRewriteTests.Run(Verify);
    await EncounterSimulationTests.Run(Verify);
    Console.WriteLine($"{encounterCheckCount} encounter checks passed; managed 1 Host + 2 Clients simulation, no game process used.");return;
}

if (args.Length > 0 && args[0] == "--loot-filter-tests")
{
    int count = 0;
    await LootFilterTests.Run((passed, label) =>
    {
        if (!passed) throw new Exception("FAIL: " + label);
        count++; Console.WriteLine("PASS: " + label);
    });
    Console.WriteLine($"{count} loot filter managed checks passed; no game process used.");
    return;
}

if(args.Length>0 && args[0]=="--check-release")
{
    using var checker=new LastEpochLAN.Mod.ReleaseUpdateChecker();
    checker.Check(args[1]);await checker.Work;Console.WriteLine(checker.Status);return;
}

if (args.Length > 0 && args[0] == "--audit-lan")
{
    using var audit = await LanSession.JoinAsync(args[1], int.Parse(args[2]), "LAN-Audit",
        "D5F9FB458F0E9878E697A92B590A586A52FD77E645F8BF87BB4F2B74C6C56567", args[3]);
    await Task.Delay(1000);
    var world = audit.World;
    File.WriteAllText(args[4], JsonSerializer.Serialize(new { utc = DateTimeOffset.UtcNow,
        scope = "read-only authoritative snapshot from actual LAN host; not combat validation",
        players = audit.Players, world, alive = world.Enemies.Count(enemy => !enemy.Dead), dead = world.Enemies.Count(enemy => enemy.Dead) },
        new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Host scene {world.Scene}: {world.Enemies.Length} records; {world.Enemies.Count(enemy => enemy.Dead)} dead.");
    return;
}

if (args.Length > 0 && args[0] == "--probe-native-host")
{
    using var gameGuest = await LanSession.JoinAsync("127.0.0.1", int.Parse(args[1]), "NativeHostProbe",
        "D5F9FB458F0E9878E697A92B590A586A52FD77E645F8BF87BB4F2B74C6C56567", args[2]);
    gameGuest.Publish(new PlayerState { Scene = "probe-zone", X = 123, Available = true });
    long end = Environment.TickCount64 + 5000;
    while (Environment.TickCount64 < end && !gameGuest.Players.Any(p => p.Name == "EngineSmokeHost")) await Task.Delay(50);
    if (!gameGuest.Players.Any(p => p.Name == "EngineSmokeHost")) throw new Exception("Native host did not relay its state.");
    await Task.Delay(2500);
    File.WriteAllText(args[3], JsonSerializer.Serialize(new { passed = true, kind = "native Last Epoch process hosts managed LAN transport",
        participants = gameGuest.Players, combat = false }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine("PASS: connected to LAN host inside actual Last Epoch process; native host snapshot relayed.");
    return;
}

const string build = "selftest-build-25816319";
const string code = "test-room";
var checks = new List<string>();
void Check(bool condition, string label)
{
    if (!condition) throw new Exception("FAIL: " + label);
    checks.Add(label);
    Console.WriteLine("PASS: " + label);
}
async Task Until(Func<bool> predicate, string label, int timeout = 3000)
{
    long end = Environment.TickCount64 + timeout;
    while (!predicate() && Environment.TickCount64 < end) await Task.Delay(20);
    Check(predicate(), label);
}
async Task Reject(Func<Task> operation, string text, string label)
{
    try { await operation(); throw new Exception("Unexpected success: " + label); }
    catch (InvalidOperationException ex) { Check(ex.Message.Contains(text), label); }
}

MotionTests.Run(Check);
await SummonTests.Run(Check);
DensityTests.Run(Check);
OfflineBonusTests.Run(Check);
LinearCorruptionTests.Run(Check);
BloodrageChargeTests.Run(Check);
WovenPotentialTests.Run(Check);
GlyphCraftingTests.Run(Check);
SharedChestTests.Run(Check);
await SharedChestTests.Transport(Check);
FortuneTests.Run(Check);
DpsTests.Run(Check);
CharacterSetupTests.Run(Check);
PartyTravelTests.Run(Check);
await PartyEchoTests.Run(Check);
AutoPlayTests.Run(Check);
await EnemyPresentationTests.Run(Check);
await ReleaseUpdateTests.Run(Check);
WorldTransferTests.Run(Check);
await EncounterRewriteTests.Run(Check);
ResolvedAilmentTests.Run(Check);
await PartyStatusTests.Run(Check);
await PerformanceRegressionTests.Run(Check);
await InventoryOnlyRoomTests.Run(Check);
SourceStatusVisualsTests.Run(Check);
await SourceStatusTransportTests.Run(Check);
var longHistory = new EnemyHistory();
for (int id = 1; id <= 2000; id++)
{
    longHistory.Observe(new EnemyState { Id=id, Template="long-map", Health=10, MaxHealth=10 });
    longHistory.Observe(new EnemyState { Id=id, Template="long-map", Dead=true, MaxHealth=10 });
    longHistory.Snapshot(Array.Empty<int>());
}
Check(longHistory.Snapshot(Array.Empty<int>()).Length == 2000, "2000 pending death transitions are retained until acknowledgement without competing with live identity capacity");
longHistory.RetireDeaths(id => id <= 1900);
Check(longHistory.Snapshot(Array.Empty<int>()).Length == 100, "acknowledged deaths release full snapshot records while pending deliveries remain");
longHistory.Observe(new EnemyState {Id=1,Template="long-map",Health=10,MaxHealth=10});
Check(!longHistory.Snapshot(new[]{1}).Any(e=>e.Id==1), "evicted terminal enemy cannot resurrect from a stale live sample");
var bossHistory=new EnemyHistory();
bossHistory.Observe(new EnemyState{Id=1,Template="boss",Boss=true,Health=10,MaxHealth=10});
for(int id=2;id<300;id++) {bossHistory.Observe(new EnemyState{Id=id,Template="wave",Dead=true,MaxHealth=10});bossHistory.Snapshot(new[]{1});}
bossHistory.Observe(new EnemyState{Id=1,Template="boss",Boss=true,Dead=true,MaxHealth=10});
Check(bossHistory.Snapshot(Array.Empty<int>()).Any(e=>e.Id==1 && e.Dead), "old-spawn boss death retained until acknowledgement rather than displaced by newer spawn IDs");
Check(!new EnemyState{Id=1,Template="control",MaxHealth=10,FreezeMaterial=4}.IsValid(), "unknown freeze material rejected");
var cosmeticFixture=new AvatarState{Aura=new CosmeticPrefab{P0=1,P1=2,SubPath="fx"},Footprint=new CosmeticPrefab{P0=3,P1=4}};
Check(JsonSerializer.Deserialize<AvatarState>(JsonSerializer.Serialize(cosmeticFixture))!.Copy().Aura==cosmeticFixture.Aura && cosmeticFixture.IsValid(),
    "cosmetic asset identities survive avatar wire/copy validation");

var visualLive = new SkillVisualState { Id = 1, AbilityKey = 1 };
var visualEnded = visualLive with { Ended = true };
var visualWorld = new WorldState { Scene = "QA", Enemies = new[] { new EnemyState { Id = 1, Visuals = new[] { visualLive } } } };
Check(!WorldContent.Same(visualWorld, visualWorld with { Enemies = new[] { visualWorld.Enemies[0] with { Visuals = new[] { visualEnded } } } }),
    "projectile termination forces a world update even when its position did not change");
Check(JsonSerializer.Deserialize<SkillVisualState>(JsonSerializer.Serialize(visualEnded))!.Copy().Ended,
    "projectile terminal state survives wire serialization and snapshot copy");
var denseAnimations = new WorldState { Scene = "QA", Enemies = Enumerable.Range(1, 512).Select(id => new EnemyState {
    Id = id, Template = "Dense-animation-fixture", MaxHealth = 100, Health = 100,
    Animation = new AnimationSnapshot { Layers = new[] { new AnimationLayer { Hash = 123, Weight = 1 } },
        Parameters = Enumerable.Range(1, 24).Select(hash => new AnimationParameter { Hash = hash, Type = 4, Bool = true }).ToArray() }
}).ToArray() };
Check(JsonSerializer.SerializeToUtf8Bytes(denseAnimations).Length < 1048576,
    "512 NPC snapshots with bounded native animation parameters fit the transport frame without VFX");

Check(LanAddress.IsLocal(IPAddress.Parse("192.168.1.2")) && LanAddress.IsLocal(IPAddress.Parse("10.3.2.1")) &&
    !LanAddress.IsLocal(IPAddress.Parse("8.8.8.8")), "private LAN address validation");
Check(LanNetworks.IsSupported(IPAddress.Parse("8.8.8.8")) && LanNetworks.IsSupported(IPAddress.Parse("26.77.127.216")),
    "Internet and VPN endpoints are accepted without matching any installed adapter subnet");
Check(LanNetworks.IsSupported(IPAddress.Parse("192.168.1.8")) && LanNetworks.IsSupported(IPAddress.Loopback),
    "direct Internet mode preserves local host/guest and loopback QA connections");
Check(!LanNetworks.IsSupported(IPAddress.Any) && !LanNetworks.IsSupported(IPAddress.Broadcast) &&
    !LanNetworks.IsSupported(IPAddress.Parse("224.0.0.1")) && !LanNetworks.IsSupported(IPAddress.IPv6Loopback),
    "unspecified, broadcast, multicast and IPv6-only peer endpoints remain invalid");
Check(LanNetworks.IsSameSubnet(IPAddress.Parse("26.77.127.216"), IPAddress.Parse("26.9.8.7"), IPAddress.Parse("255.0.0.0")), "VPN 26 peer on configured subnet accepted");
Check(!LanNetworks.IsSameSubnet(IPAddress.Parse("26.77.127.216"), IPAddress.Parse("192.168.1.7"), IPAddress.Parse("255.255.255.0")), "VPN peer requires matching adapter subnet");
Check(!LanNetworks.IsSameSubnet(IPAddress.Parse("8.8.8.8"), IPAddress.Parse("26.9.8.7"), IPAddress.Parse("255.0.0.0")), "unrelated public destination rejected");
Check(!LanNetworks.IsSameSubnet(IPAddress.Parse("26.77.127.216"), IPAddress.Parse("26.9.8.7"), IPAddress.Any) &&
    !LanNetworks.IsSameSubnet(IPAddress.Parse("26.77.127.216"), IPAddress.Parse("26.9.8.7"), IPAddress.Parse("255.0.255.0")), "zero and discontinuous subnet masks rejected");
Check(!new PlayerState { X = float.NaN }.IsValid() && !new PlayerState { Scene = new string('x', 257) }.IsValid(), "non-finite and oversized state rejection");
Check(!new PlayerState { Health = 101, MaxHealth = 100 }.IsValid() &&
    !new PlayerState { Health = float.NaN, MaxHealth = 100 }.IsValid(), "invalid native party health rejected");
var recentDamage = new RecentEnemyDamage();
var damageEnemy = new EnemyState { Id = 1, Template = "damage-test", Health = 100, MaxHealth = 100 };
recentDamage.Observe(new WorldState { Scene = "damage-zone", Epoch = 1, Revision = 1, Enemies = new[] { damageEnemy } }, 10);
Check(!recentDamage.IsVisible(1, 10), "enemy HP bar remains hidden before damage");
recentDamage.Observe(new WorldState { Scene = "damage-zone", Epoch = 1, Revision = 2, Enemies = new[] { damageEnemy with { Health = 80 } } }, 11);
Check(recentDamage.IsVisible(1, 11), "enemy HP bar becomes visible after health loss");
recentDamage.Observe(new WorldState { Scene = "damage-zone", Epoch = 1, Revision = 2, Enemies = new[] { damageEnemy with { Health = 80 } } }, 13);
Check(!recentDamage.IsVisible(1, 14), "duplicate snapshots do not keep damaged HP bar permanently visible");
recentDamage.Observe(new WorldState { Scene = "damage-zone", Epoch = 2, Revision = 3, Enemies = new[] { damageEnemy with { Health = 80 } } }, 15);
Check(!recentDamage.IsVisible(1, 15), "new map instance does not inherit HP bar damage history");
Check(!new PlayerState { Avatar = new AvatarState { ClassId = 5 } }.IsValid() &&
    !new PlayerState { Avatar = new AvatarState { AnimationTime = float.NaN } }.IsValid(), "invalid native class and animation payload rejection");
Check(!new NativeMotionState { State = 10 }.IsValid() && !new NativeMotionState { AnimationSpeed = float.NaN }.IsValid() &&
    !new NativeMotionState { MoveSpeed = -1 }.IsValid(), "invalid native relay animation state and speed rejected");
Check(!new SkillVisualState { Id = 1, AbilityKey = 1, Age = float.NaN }.IsValid() &&
    !new SkillVisualState { Id = 1, AbilityKey = 0 }.IsValid() &&
    !new AvatarState { Visuals = Enumerable.Repeat(new SkillVisualState { Id = 1, AbilityKey = 1 }, 2).ToArray() }.IsValid(),
    "invalid native VFX identity, age and duplicate IDs rejected");
Check(!new SkillVisualState { Id = 1, AbilityKey = 1, Lines = new[] { new VisualLine { Points =
    new[] { new VisualPoint(), new VisualPoint { X = float.PositiveInfinity } } } } }.IsValid(), "non-finite native beam geometry rejected");
Check(!new AvatarState { Equipment = new[] { new EquipmentAppearance { Slot = 6 } } }.IsValid() &&
    !new AvatarState { Equipment = new[] { new EquipmentAppearance { SubType = -1 } } }.IsValid() &&
    !new AvatarState { Equipment = new[] { new EquipmentAppearance(), new EquipmentAppearance() } }.IsValid() &&
    !new AvatarState { BackCosmetic = new string('x', 257) }.IsValid(), "invalid and duplicate equipment slots rejected");
var animation = new AnimationSnapshot { Speed = 1.5f, Layers = new[] {
    new AnimationLayer { Hash = -123456, Time = 0.25f, Weight = 1 },
    new AnimationLayer { Hash = 987654, Time = 0.75f, Weight = 0.5f } }, Parameters = new[] {
        new AnimationParameter { Hash = 123, Type = 4, Bool = true },
        new AnimationParameter { Hash = 456, Type = 1, Float = 0.75f },
        new AnimationParameter { Hash = 789, Type = 3, Int = 2 } } };
Check(!new AnimationSnapshot { Parameters = new[] { new AnimationParameter { Type = 9 } } }.IsValid() &&
    !new AnimationSnapshot { Parameters = new[] { new AnimationParameter { Type = 1, Float = float.NaN } } }.IsValid() &&
    !new AnimationSnapshot { Parameters = Enumerable.Repeat(new AnimationParameter { Hash = 1, Type = 4 }, 2).ToArray() }.IsValid(),
    "invalid animation parameter types, non-finite values and duplicate hashes rejected");
Check(!new AnimationSnapshot { Speed = float.NaN }.IsValid() &&
    !new AnimationSnapshot { Layers = new[] { new AnimationLayer { Weight = 2 } } }.IsValid() &&
    !new AnimationSnapshot { Layers = Enumerable.Repeat(new AnimationLayer(), 9).ToArray() }.IsValid(),
    "invalid speed, layer weight and excessive animation layers rejected");
var deathFilter = new ConfirmedDeaths();
string deathHost = Guid.NewGuid().ToString("N");
var liveEnemy = new EnemyState { Id = 1, Template = "enemy", MaxHealth = 100, Health = 100 };
var corpse = liveEnemy with { Health = 0, Dead = true };
var history = new EnemyHistory();
history.Observe(liveEnemy);
Check(history.Snapshot(Array.Empty<int>()).Length == 0, "alive despawn does not become a death");
history.Observe(corpse);
Check(history.Snapshot(Array.Empty<int>()).Single().Dead, "destroyed host corpse remains in authoritative snapshot");
history.Observe(liveEnemy);
Check(history.Snapshot(new[] { 1 }).Single().Dead, "stale live sample cannot resurrect terminal enemy identity");
history.Clear();
history.Observe(liveEnemy);
Check(!history.Snapshot(new[] { 1 }).Single().Dead, "new instance clears corpse history");
history.Observe(corpse);
var destroyedFilter = new ConfirmedDeaths();
destroyedFilter.Observe(deathHost, new WorldState { Scene = "campaign", Epoch = 1, Revision = 1, Enemies = new[] { liveEnemy } });
Check(destroyedFilter.Observe(deathHost, new WorldState { Scene = "campaign", Epoch = 1, Revision = 2,
    Enemies = history.Snapshot(Array.Empty<int>()) }).Count == 1, "death dispatch survives native corpse destruction between samples");
WorldState DeathWorld(long epoch, long revision, params EnemyState[] enemies) =>
    new() { Scene = "campaign", Epoch = epoch, Revision = revision, Enemies = enemies };
Check(deathFilter.Observe(deathHost, DeathWorld(1, 1, liveEnemy)).Count == 0, "alive baseline grants no local rewards");
Check(deathFilter.Observe(deathHost, DeathWorld(1, 2, corpse)).Count == 1, "host alive-to-dead transition dispatched once");
Check(deathFilter.Observe(deathHost, DeathWorld(1, 3, corpse)).Count == 0 &&
    deathFilter.Observe(deathHost, DeathWorld(1, 2, corpse)).Count == 0, "duplicate and stale deaths cannot replay rewards");
Check(deathFilter.Observe(deathHost, DeathWorld(2, 4, corpse)).Count == 0, "zone entry ignores existing corpses");
Check(new ConfirmedDeaths().Observe(deathHost, DeathWorld(2, 4, corpse)).Count == 0, "late join ignores historical kills");
Check(deathFilter.Observe(deathHost, DeathWorld(1, 99, liveEnemy)).Count == 0 &&
    deathFilter.Observe(deathHost, DeathWorld(1, 100, corpse)).Count == 0, "old instance cannot reset death filter");
Check(deathFilter.Observe(deathHost, DeathWorld(2, 5, liveEnemy)).Count == 0 &&
    deathFilter.Observe(deathHost, DeathWorld(2, 6, corpse)).Count == 0, "reused dead identity cannot grant again");
using var host = LanSession.Host("Host", build, code, 0, IPAddress.Loopback);
host.Publish(new PlayerState { Scene = "test-zone", X = 3, Available = true });
using (var oldVersion = new TcpClient())
{
    await oldVersion.ConnectAsync(IPAddress.Loopback, host.Port);
    await Wire.WriteAsync(oldVersion.GetStream(), new Packet { Kind = "hello", Protocol = 4, Build = build, Code = code, Name = "OldVersion" }, CancellationToken.None);
    var rejected = await Wire.ReadAsync(oldVersion.GetStream(), CancellationToken.None);
    Check(rejected.Kind == "reject" && rejected.Error.Contains($"host requires {LanProtocol.Version}; guest sent 4") && rejected.Error.Contains("BOTH"),
        "old PC 2 protocol receives actionable update error");
}
await Reject(async () => { using var bad = await LanSession.JoinAsync("127.0.0.1", host.Port, "Bad", "another-build", code); },
    "build mismatch", "reject mismatched game build");
await Reject(async () => { using var bad = await LanSession.JoinAsync("127.0.0.1", host.Port, "Bad", build, "incorrect"); },
    "room code", "reject incorrect room code");
using var guest1 = await LanSession.JoinAsync("127.0.0.1", host.Port, "Guest1", build, code);
using var guest2 = await LanSession.JoinAsync("127.0.0.1", host.Port, "Guest2", build, code);
var hudFixture = new HudConfiguration { Revision = 1, Values = new[] { new HudValue { Key = "Legacy.Character.Cheats.DensityMultiplier", Json = "2.5" }, new HudValue { Key = "Groups.Difficulty.EnemyHealthMult.Enabled", Json = "true" } } };
Check(hudFixture.IsValid() && !(hudFixture with { Values = new[] { new HudValue { Key = "x", Json = "{\"code\":1}" } } }).IsValid() &&
    !(hudFixture with { Values = new[] { hudFixture.Values[0], hudFixture.Values[0] } }).IsValid(), "HUD configuration only accepts bounded unique scalar values");
host.PublishHud(hudFixture);
hudFixture.Values[0] = hudFixture.Values[0] with { Json = "999" };
await Until(() => guest1.HudRevision == 1 && guest2.HudRevision == 1, "host HUD configuration reaches every guest over TCP");
Check(guest1.Hud!.Values[0].Json == "2.5", "HUD publication is isolated from caller mutation");
var hudCopy = guest1.Hud!; hudCopy.Values[0] = hudCopy.Values[0] with { Json = "999" };
Check(guest1.Hud!.Values[0].Json == "2.5", "guest HUD diagnostics cannot mutate received authority");
try { guest1.PublishHud(hudFixture with { Revision = 2 }); throw new Exception("Guest HUD publication accepted"); }
catch (InvalidOperationException) { Check(true, "guest cannot publish host HUD settings"); }
try { host.PublishHud(hudFixture); throw new Exception("Stale HUD revision accepted"); }
catch (ArgumentException) { Check(true, "stale host HUD revisions are rejected"); }
var appearance = new[] { new EquipmentAppearance { Slot = 0, EquipmentType = 0, SubType = 42,
    Unique = true, UniqueId = 321, Variation = 17 } };
var particleFixture = new VisualParticleFrame { Index = 0, Count = 12, ShapePosition = new VisualPoint { X = 7, Y = 3 },
    ShapeScale = new VisualPoint { X = 1, Y = 2, Z = 3 }, ShapeRotation = new VisualPoint { Y = 90 }, Size = 2, Speed = 3, Lifetime = 2 };
var visualFixture = new[] { new SkillVisualState { Id = 1, AbilityKey = -1234567, VisualIndex = 2, Layer = 17,
    Particles = new[] { particleFixture },
    PrefabGuid0 = ulong.MaxValue, PrefabGuid1 = 1234, PrefabSubPath = "actual/visual", CastingVfxKey = -55,
    StretchOrigin = new VisualPoint { X = 17, Y = 2, Z = -5 },
    Poses = new[] { new VisualPose { Path = "0/1", Position = new VisualPoint { X = 4 }, Active = true, Layer = 18 } },
    Position = new VisualPoint { X = 3 }, Age = 0.1f, Lines = new[] { new VisualLine { Points =
        new[] { new VisualPoint { X = 3 }, new VisualPoint { X = 12 } } } } } };
var dropsFixture = new[] { new DroppedItemState { Id = 12, Name = "Dropped sword",
    EncodedItem = Convert.ToBase64String(new byte[] { 1, 2, 3 }), Position = new VisualPoint { X = 9 } } };
Check(!(dropsFixture[0] with { EncodedItem = "not base64" }).IsValid() &&
    !new PlayerState { DroppedItems = new[] { dropsFixture[0], dropsFixture[0] } }.IsValid(),
    "malformed serialized drops and duplicate drop identities rejected");
guest1.Publish(new PlayerState { Scene = "test-zone", X = 12, Y = 2, Z = 4, Available = true,
    Health = 123, MaxHealth = 200,
    DroppedItems = dropsFixture,
    Avatar = new AvatarState { ClassId = 1, AnimationHash = -123456, AnimationTime = 0.25f, Animation = animation,
        Equipment = appearance, BackCosmetic = "back-cosmetic-fixture", Visuals = visualFixture,
        Motion = new NativeMotionState { State = 5, AnimationNumber = 2, AnimationSpeed = 1.5f, MoveSpeed = 0.75f,
            TemporaryIdle = true, TemporaryAnimation = 32, Duration = 0.4f } } });
visualFixture[0].Lines[0].Points[0] = new VisualPoint { X = 999 };
visualFixture[0].Particles[0] = particleFixture with { Count = 1 };
dropsFixture[0] = dropsFixture[0] with { Name = "Mutated" };
appearance[0] = appearance[0] with { SubType = 999 };
animation.Layers[0] = animation.Layers[0] with { Hash = 999 };
animation.Parameters[0] = animation.Parameters[0] with { Bool = false };
guest2.Publish(new PlayerState { Scene = "other-zone", X = 34, Available = true });
await Until(() => host.Players.Any(p => p.Id == guest1.LocalId && p.X == 12), "guest state reaches host");
Check(host.Players.First(p => p.Id == guest1.LocalId).Health == 123 && host.Players.First(p => p.Id == guest1.LocalId).MaxHealth == 200,
    "native party current and maximum health reach host through actual transport");
Check(host.Players.First(p => p.Id == guest1.LocalId).DroppedItems[0].Name == "Dropped sword",
    "inventory drop identity, serialized data and position survive caller mutation");
await Until(() => guest2.Players.Any(p => p.Id == guest1.LocalId && p.DroppedItems.Length == 1), "inventory drops relay to the other guest");
Check(guest2.Players.First(p => p.Id == guest1.LocalId).DroppedItems[0].Position.X == 9,
    "remote dropped-item location survives actual transport");
Check(host.Players.First(p => p.Id == guest1.LocalId).Avatar!.Visuals[0].AbilityKey == -1234567 &&
    host.Players.First(p => p.Id == guest1.LocalId).Avatar!.Visuals[0].Lines[0].Points[0].X == 3,
    "native skill prefab identity and beam endpoints survive wire and caller mutation");
Check(host.Players.First(p => p.Id == guest1.LocalId).Avatar!.Visuals[0] is
    { PrefabGuid0: ulong.MaxValue, PrefabGuid1: 1234, PrefabSubPath: "actual/visual", CastingVfxKey: -55 },
    "actual prefab GUID, subasset path and casting VFX key survive framed transport");
Check(host.Players.First(p => p.Id == guest1.LocalId).Avatar!.Visuals[0].StretchOrigin is { X: 17, Y: 2, Z: -5 },
    "native connected VFX stretch origin survives actual TCP transport");
Check(!new SkillVisualState { Id = 1, AbilityKey = 1, StretchOrigin = new VisualPoint { X = float.NaN } }.IsValid(),
    "non-finite connected VFX endpoint rejected");
Check(host.Players.First(p => p.Id == guest1.LocalId).Avatar!.Visuals[0].Poses[0].Path == "0/1" &&
    host.Players.First(p => p.Id == guest1.LocalId).Avatar!.Visuals[0].Poses[0].Position.X == 4,
    "native visual child pose survives TCP transport");
Check(host.Players.First(p => p.Id == guest1.LocalId).Avatar!.Visuals[0].Particles[0] == particleFixture,
    "emitter count, shape, size, speed and lifetime survive TCP and caller mutation");
Check(guest2.Players.First(p => p.Id == guest1.LocalId).Avatar!.Visuals[0].Particles[0] == particleFixture,
    "emitter state relays to the other guest");
Check(host.Players.First(p => p.Id == guest1.LocalId).Avatar!.Visuals[0] is { Layer: 17 } &&
    host.Players.First(p => p.Id == guest1.LocalId).Avatar!.Visuals[0].Poses[0].Layer == 18,
    "native effect root and child render layers survive transport");
Check(!(particleFixture with { Lifetime = float.NaN }).IsValid() &&
    !(particleFixture with { ShapePosition = new VisualPoint { X = float.PositiveInfinity } }).IsValid() &&
    !(particleFixture with { Count = -1 }).IsValid(), "malformed emitter lifetimes, positions and counts rejected");
Check(!new VisualParticleFrame { Index = 32 }.IsValid() && new VisualParticleFrame { Index = 31 }.IsValid() &&
    !new VisualParticleFrame { Count = 33 }.IsValid() &&
    !new VisualPose { Layer = 32 }.IsValid(), "emitter count and render layer bounds enforced");
var fullParticleVisual = new SkillVisualState { Id = 1, AbilityKey = 1, Particles = new[] {
    particleFixture with { Index = 0, Count = 32 }, particleFixture with { Index = 1, Count = 32 } } };
Check(!new AvatarState { Visuals = new[] { fullParticleVisual, fullParticleVisual with { Id = 2 }, fullParticleVisual with { Id = 3 } } }.IsValid(),
    "aggregate player particle payload capped at 128 particles");
Check(!new VisualPose { Path = "../0" }.IsValid() && !new VisualPose { Path = "-1" }.IsValid(),
    "invalid visual hierarchy indices rejected");
Check(!new VisualLine { Width = float.NaN, Points = new[] { new VisualPoint(), new VisualPoint() } }.IsValid() &&
    !new VisualLine { Width = 0, Points = new[] { new VisualPoint(), new VisualPoint() } }.IsValid() &&
    !new SkillVisualState { Id = 1, AbilityKey = 1, PrefabSubPath = new string('x', 257) }.IsValid(),
    "invalid beam width and excessive prefab subasset path rejected");
var exposedVfx = host.Players.First(p => p.Id == guest1.LocalId).Avatar!.Visuals;
exposedVfx[0].Lines[0].Points[0] = new VisualPoint { X = 888 };
Check(host.Players.First(p => p.Id == guest1.LocalId).Avatar!.Visuals[0].Lines[0].Points[0].X == 3,
    "native VFX diagnostics cannot mutate authoritative nested beam geometry");
Check(host.Players.First(p => p.Id == guest1.LocalId).Avatar!.Motion is { TemporaryIdle: true, TemporaryAnimation: 32, Duration: 0.4f },
    "native temporary idle and evade timing survive transport");
Check(host.Players.First(p => p.Id == guest1.LocalId).Avatar!.Motion is { State: 5, AnimationNumber: 2, AnimationSpeed: 1.5f, MoveSpeed: 0.75f },
    "native relay animation command survives guest to host transport");
Check(host.Players.First(p => p.Id == guest1.LocalId).Avatar!.Equipment[0] is { SubType: 42, UniqueId: 321, Variation: 17 } &&
    host.Players.First(p => p.Id == guest1.LocalId).Avatar!.BackCosmetic == "back-cosmetic-fixture",
    "native equipment identifiers and back cosmetic survive wire and caller mutation");
var exposedAppearance = host.Players.First(p => p.Id == guest1.LocalId).Avatar!;
exposedAppearance.Equipment[0] = exposedAppearance.Equipment[0] with { SubType = 888 };
Check(host.Players.First(p => p.Id == guest1.LocalId).Avatar!.Equipment[0].SubType == 42,
    "equipment diagnostics cannot mutate authoritative appearance");
Check(host.Players.First(p => p.Id == guest1.LocalId).Avatar is { ClassId: 1, AnimationHash: -123456, AnimationTime: 0.25f },
    "native class and animation state reaches host unchanged");
Check(host.Players.First(p => p.Id == guest1.LocalId).Avatar!.Animation!.Layers[0].Hash == -123456 &&
    host.Players.First(p => p.Id == guest1.LocalId).Avatar!.Animation!.Layers[1].Weight == 0.5f,
    "all animation layers reach host and publish isolates caller arrays");
Check(host.Players.First(p => p.Id == guest1.LocalId).Avatar!.Animation!.Parameters[0].Bool &&
    host.Players.First(p => p.Id == guest1.LocalId).Avatar!.Animation!.Parameters[1].Float == 0.75f &&
    host.Players.First(p => p.Id == guest1.LocalId).Avatar!.Animation!.Parameters[2].Int == 2,
    "bool/float/int Animator parameters survive wire transfer and caller mutation");
var exposedPlayer = host.Players.First(p => p.Id == guest1.LocalId);
exposedPlayer.Avatar!.Animation!.Layers[0] = new AnimationLayer();
exposedPlayer.Avatar!.Animation!.Parameters[0] = exposedPlayer.Avatar.Animation.Parameters[0] with { Bool = false };
Check(host.Players.First(p => p.Id == guest1.LocalId).Avatar!.Animation!.Layers[0].Hash == -123456,
    "player diagnostics cannot mutate animation state in transport");
Check(host.Players.First(p => p.Id == guest1.LocalId).Avatar!.Animation!.Parameters[0].Bool,
    "player diagnostics cannot mutate authoritative animation parameter arrays");
await Until(() => guest2.Players.Any(p => p.Id == guest1.LocalId && p.X == 12), "host relays state to another guest");
Check(guest2.Players.First(p => p.Id == guest1.LocalId).Avatar!.Visuals[0].Lines[0].Points[1].X == 12,
    "host relays actual native beam endpoint to another guest");
Check(guest2.Players.First(p => p.Id == guest1.LocalId).Avatar!.Equipment[0].Variation == 17,
    "host relays remote cosmetic variation to other guest");
await Until(() => guest1.Players.Any(p => p.Id == host.LocalId && p.X == 3), "host state reaches guest");
await Until(() => guest1.Players.Any(p => p.Id == guest2.LocalId && p.Scene == "other-zone"), "scene identities preserved");
Check(guest1.PublishSkill("Fireball", 15, 2, 4, 3, false, 0, 1.5f), "guest publishes bounded skill event");
SkillEvent? hostSkill = null;
await Until(() => { if (host.TryTakeSkill(out var got)) hostSkill = got; return hostSkill != null; }, "guest skill reaches host");
Check(hostSkill!.AbilitySlot == 3 && !hostSkill.ZoneAbility, "native ability bar slot survives framed transport");
Check(hostSkill.AnimationNumber == 0 && hostSkill.AnimationSpeed == 1.5f, "native skill cast animation survives framed transport");
Check(hostSkill!.PlayerId == guest1.LocalId && hostSkill.Ability == "Fireball", "skill identity assigned by host");
SkillEvent? otherSkill = null;
await Until(() => { if (guest2.TryTakeSkill(out var got)) otherSkill = got; return otherSkill != null; }, "host relays skill to another guest");
Check(!guest1.TryTakeSkill(out _), "skill is not echoed to its source");
Check(host.PublishSkill("Melee", 12, 2, 4), "host publishes skill event");
SkillEvent? fromHost = null;
await Until(() => { if (guest1.TryTakeSkill(out var got)) fromHost = got; return fromHost != null; }, "host skill reaches guest");
Check(fromHost!.PlayerId == host.LocalId, "host skill retains identity");
Check(!guest1.PublishSkill("", 0, 0, 0) && !guest1.PublishSkill("Bad", float.NaN, 0, 0), "invalid skill payload rejected");
Check(!guest1.PublishSkill("BadSlot", 0, 0, 0, 5) && !guest1.PublishSkill("BadSlot", 0, 0, 0, -2) &&
    !guest1.PublishSkill("BadZone", 0, 0, 0, 1, true), "invalid native ability slots rejected before enqueue");
Check(new SkillEvent { Ability = "Zone", Sequence = 1, AbilitySlot = 0, ZoneAbility = true }.IsValid(),
    "native zone ability has its own explicit command identity");
var nativeCommand = new NativeVisualCommand { Guid0 = 123, Guid1 = 456, SubPath = "SlashVariant", AbilityKey = 789,
    Position = new VisualPoint { X = 15 }, Rotation = new VisualPoint { Y = 90 }, Origin = new VisualPoint { X = 12 },
    Start = new VisualPoint { X = 12 }, Target = new VisualPoint { X = 18 }, TargetOffset = new VisualPoint { Y = 1 },
    Mutable = true, ApplyMutation = false, UseAutoPool = false, Modifiers = new float[] { 1, 2, 3, 4, 5, 6, 7 },
    ToggleEnum = 2, ToggleMask = ulong.MaxValue, TimingMode = 4, TimingLo = 5, TimingHi = 6 };
Check(guest1.PublishSkill("native-vfx", 15, 0, 0, nativeVisual: nativeCommand), "guest queues native visual command");
nativeCommand.Modifiers[0] = 99;
SkillEvent? nativeAtHost = null;
await Until(() => { if (host.TryTakeSkill(out var got) && got?.NativeVisual != null) nativeAtHost = got; return nativeAtHost != null; },
    "native spawn command reaches host through reliable transport");
var receivedNative = nativeAtHost!.NativeVisual!;
Check(nativeAtHost.PlayerId == guest1.LocalId && receivedNative.Guid0 == 123 && receivedNative.Guid1 == 456 &&
    receivedNative.SubPath == "SlashVariant" && receivedNative.AbilityKey == 789 && receivedNative.Modifiers[0] == 1,
    "native asset identity and publication copy survive transport");
Check(receivedNative.Start!.X == 12 && receivedNative.Target!.X == 18 && receivedNative.TargetOffset.Y == 1 &&
    receivedNative.Origin!.X == 12 && receivedNative.Rotation.Y == 90 && receivedNative.Mutable && !receivedNative.ApplyMutation &&
    !receivedNative.UseAutoPool && receivedNative.ToggleMask == ulong.MaxValue && receivedNative.TimingHi == 6,
    "native visual endpoint mutation toggle and lifecycle arguments survive transport");
SkillEvent? nativeAtOther = null;
await Until(() => { if (guest2.TryTakeSkill(out var got) && got?.NativeVisual != null) nativeAtOther = got; return nativeAtOther != null; },
    "host relays native VFX command to another guest");
Check(!guest1.TryTakeSkill(out _), "native VFX command is not echoed to its source");
Check(host.PublishSkill("native-vfx", 3, 0, 0, nativeVisual: nativeCommand with { Mutable = false, Start = null, Target = null, StretchFromOrigin = true }),
    "host queues native stretch effect command");
SkillEvent? nativeFromHost = null;
await Until(() => { if (guest1.TryTakeSkill(out var got) && got?.NativeVisual != null) nativeFromHost = got; return nativeFromHost != null; },
    "host native stretch command reaches guest");
Check(nativeFromHost!.NativeVisual!.StretchFromOrigin && nativeFromHost.PlayerId == host.LocalId, "native stretch mode retains host identity");
Check(!(nativeCommand with { Guid0 = 0, Guid1 = 0 }).IsValid() && !(nativeCommand with { Modifiers = new[] { float.NaN, 0, 0, 0, 0, 0, 0 } }).IsValid() &&
    !(nativeCommand with { Modifiers = new float[6] }).IsValid() && !(nativeCommand with { Start = null }).IsValid() &&
    !(nativeCommand with { SubPath = new string('x', 257) }).IsValid() && !(nativeCommand with { StretchFromOrigin = true }).IsValid(),
    "invalid native visual assets endpoints modes and mutations are rejected");
Check(!guest1.PublishSkill("native-vfx", 0, 0, 0, nativeVisual: nativeCommand with { Guid0 = 0, Guid1 = 0 }),
    "invalid native visual command rejected before enqueue");
var enemyVisual = visualFixture.Select(visual => visual.Copy()).ToArray();
while(host.TryTakeSkill(out _)) { }
guest2.Publish(new PlayerState {Scene="other-zone",Available=true,Dead=true,X=34});
await Until(()=>host.PlayerPositions.Any(p=>p.Id==guest2.LocalId && p.Dead),"dead peer state reaches authority");
Check(!guest2.PublishSkill("dead-body-skill",34,0,0),"dead character cannot enqueue an ordinary combat skill");
guest2.Publish(new PlayerState {Scene="other-zone",Available=true,X=34});
await Until(()=>host.PlayerPositions.Any(p=>p.Id==guest2.LocalId && !p.Dead),"alive peer state restored");
var graphFixture = new[] { new SceneGraphState { Path = "Map[0]/Door[1]", Asset = "CampaignDoor", Node = 1, NodeCount = 2 } };
var triggerFixture = new[] { new MapTriggerState { Path = "Map[0]/Ambush[0]", Kind = 1, Sequence = 2,
    Position = new VisualPoint { X = 9 } } };
Check(!(triggerFixture[0] with { Kind = 2 }).IsValid() && !(triggerFixture[0] with { Sequence = 0 }).IsValid(),
    "invalid map trigger kinds and sequence rejected");
var enemy = new EnemyState { Id = 1, Template = "test-enemy", X = 12, MaxHealth = 100, Health = 100, Animation = animation.Copy(),
    Motion = new NativeMotionState { State = 5, AnimationNumber = 3, AnimationSpeed = 1.25f }, AttackSequence = 7, Visuals = enemyVisual };
Check(!new WorldState { SceneGraphs = new[] { graphFixture[0] with { Node = 2 } } }.IsValid() &&
    !new WorldState { SceneGraphs = new[] { graphFixture[0], graphFixture[0] } }.IsValid() &&
    !(enemy with { AttackSequence = -1 }).IsValid(), "invalid graph layouts, duplicate graph paths and enemy attack sequence rejected");
var objectFixture = new[] { new MapObjectState { Path = new string('A', 64), Exists = false, Active = false } };
Check(!new WorldState { BossId = 1 }.IsValid() && !new WorldState { Enemies = new[] { enemy }, BossId = 1 }.IsValid(), "boss HUD cannot reference a missing or non-boss actor");
Check(!(enemy with { Ward = float.NaN }).IsValid() && !new PlayerState { Ward = -1 }.IsValid(), "party and boss ward values reject invalid health data");
Check(!(objectFixture[0] with { Active = true }).IsValid() && !new WorldState { MapObjects = new[] { objectFixture[0], objectFixture[0] } }.IsValid(), "map tombstones reject active destroyed objects and duplicate paths");
host.PublishWorld(new WorldState { SampleTime = 123.456, Scene = "test-zone", Epoch = 1, Revision = 1, Enemies = new[] { enemy with { Boss = true, Ward = 25 } }, BossId = 1, SceneGraphs = graphFixture, MapTriggers = triggerFixture, MapObjects = objectFixture });
var contentFixture = new WorldState { Scene = "test-zone", Epoch = 1, Enemies = new[] { enemy } };
Check(WorldContent.Same(contentFixture, contentFixture with { Revision = 99 }), "network revision alone does not change authoritative content");
Check(WorldContent.Same(contentFixture, contentFixture with { SampleTime = 999 }), "clock advance alone does not resend unchanged worlds");
await Until(() => guest1.World.SampleTime == 123.456 && guest1.WorldArrivalSeconds > 0, "host timestamp and monotonic local arrival survive actual framed TCP");
Check(!WorldContent.Same(contentFixture, contentFixture with { Enemies = new[] { enemy with { Health = 99 } } }) &&
    !WorldContent.Same(contentFixture, contentFixture with { Enemies = new[] { enemy with { AttackSequence = 8 } } }),
    "damage and attack events force world delivery immediately");
Check(!WorldContent.Same(contentFixture, contentFixture with { Epoch = 2 }) &&
    !WorldContent.Same(contentFixture, contentFixture with { Enemies = Array.Empty<EnemyState>() }),
    "zone transitions and despawns force world delivery");
Check(!WorldContent.Same(contentFixture, contentFixture with { MapObjects = objectFixture }) && !WorldContent.Same(contentFixture, contentFixture with { Enemies = new[] { enemy with { Ward = 1 } } }), "map activation/destruction and boss ward changes force delivery");
triggerFixture[0] = triggerFixture[0] with { Sequence = 999 };
graphFixture[0] = graphFixture[0] with { Node = 0 };
enemyVisual[0].Lines[0].Points[0] = new VisualPoint { X = 555 };
await Until(() => guest1.World.Epoch == 1 && guest1.World.Enemies.Length == 1, "host enemy world reaches guest");
Check(guest1.World.BossId == 1 && guest1.World.Enemies[0].Ward == 25 && guest1.World.MapObjects[0].Exists == false, "boss HUD selection ward and summoner tombstone survive real TCP framing");
var objectCopy = guest1.World; objectCopy.MapObjects[0] = objectCopy.MapObjects[0] with { Exists = true };
Check(!guest1.World.MapObjects[0].Exists, "diagnostics cannot mutate authoritative summoner tombstones");
long omittedBefore = host.WorldFramesOmitted;
await Until(() => host.WorldFramesOmitted > omittedBefore, "unchanged world is omitted from subsequent TCP snapshots");
Check(guest1.Connected && guest1.WorldInfo is { Epoch: 1, Revision: 1, EnemyCount: 1 },
    "guest retains authoritative baseline when subsequent packet omits world payload");
Check(host.PlayerPositions.First(p => p.Id == guest1.LocalId) is { Avatar: null, X: 12, Health: 123 } &&
    host.PlayerPositions.First(p => p.Id == guest1.LocalId).DroppedItems.Length == 0,
    "lightweight proximity snapshot retains identity and health without copying nested rendering payloads");
Check(guest1.WorldInfo is { Epoch: 1, Revision: 1, Scene: "test-zone", EnemyCount: 1 },
    "world metadata read preserves revision without copying enemy arrays");
var attackFixture = new IncomingDamage { Epoch = 1, Scene = "test-zone", IsHit = true, Damage = new float[] { 3, 0, 0, 12, 0, 0, 0 }, Penetration = new float[7] };
Check(!guest1.SendDamage(guest2.LocalId, attackFixture), "guest cannot authorize damage to another guest");
Check(!host.SendDamage(guest2.LocalId, attackFixture) && !host.SendDamage(guest1.LocalId, attackFixture with { Epoch = 0 }),
    "host attacks reject wrong zone and stale world epoch");
Check(!host.SendDamage(guest1.LocalId, attackFixture with { Damage = new[] { float.NaN } }), "malformed native damage vector rejected");
Check(host.SendDamage(guest1.LocalId, attackFixture), "host authorizes native attack to selected guest");
attackFixture.Damage[0] = 999;
IncomingDamage? nativeAttack = null;
await Until(() => { if (guest1.TryTakeDamage(out var hit)) nativeAttack = hit; return nativeAttack != null; }, "host native attack reaches recipient");
Check(nativeAttack!.Damage[0] == 3 && nativeAttack.Damage[3] == 12 && nativeAttack.Sequence > 0, "native attack types are isolated from source mutation");
Check(!guest2.TryTakeDamage(out _), "native attack is not delivered to other party members");
Check(guest1.World.MapTriggers[0].Sequence == 2 && guest1.World.MapTriggers[0].Position.X == 9,
    "host map trigger activation and source position survive framed transport and caller mutation");
try { guest1.PublishWorld(new WorldState()); throw new Exception("Guest published world"); }
catch (InvalidOperationException) { Check(true, "guest cannot publish authoritative world"); }
var copied = guest1.World; copied.Enemies[0] = enemy with { Health = 0 };
Check(guest1.World.Enemies[0].Health == 100, "world arrays cannot mutate session state");
Check(guest1.World.Enemies[0].Animation!.Layers[1].Hash == 987654, "host enemy animation layers reach guest");
Check(guest1.World.Enemies[0].Motion!.AnimationNumber == 3 && guest1.World.Enemies[0].AttackSequence == 7,
    "host enemy attack animation and sequence reach guest");
Check(guest1.World.SceneGraphs[0].Node == 1 && guest1.World.Enemies[0].Visuals[0].Lines[0].Points[0].X == 999,
    "host scene events and enemy beam geometry survive transport and caller mutation");
var exposedWorld = guest1.World;
exposedWorld.Enemies[0].Animation!.Layers[1] = new AnimationLayer();
exposedWorld.SceneGraphs[0] = exposedWorld.SceneGraphs[0] with { Node = 0 };
exposedWorld.Enemies[0].Visuals[0].Lines[0].Points[0] = new VisualPoint { X = 777 };
Check(guest1.World.SceneGraphs[0].Node == 1 && guest1.World.Enemies[0].Visuals[0].Lines[0].Points[0].X == 999,
    "guest diagnostics cannot mutate event state or enemy VFX");
Check(guest1.World.Enemies[0].Animation!.Layers[1].Hash == 987654,
    "world diagnostics cannot mutate nested enemy animation arrays");
guest1.RequestHit(1, 1, 20);
HitRequest? received = null;
await Until(() => { if (host.TryTakeHit(out var hit)) received = hit; return received != null; }, "guest hit reaches host command queue");
Check(received!.PlayerId == guest1.LocalId && received.Damage == 20, "host binds hit to authenticated player");
Check(guest1.RequestControl(1,1,1,1.25f,45), "guest can submit bounded freeze request");
HitRequest? control=null;
await Until(()=>{if(host.TryTakeHit(out var value)) control=value; return control!=null;}, "freeze request reaches authoritative host over TCP");
Check(control!.ControlType==1 && control.ControlDuration==1.25f && control.ControlAngle==45 && control.PlayerId==guest1.LocalId && control.Damage==0,
    "control request retains native parameters and authenticated sender without fabricating damage");
Check(!new HitRequest{Sequence=1,EnemyId=1,ControlType=4,ControlDuration=1}.IsValid() && !new HitRequest{Sequence=1,EnemyId=1,ControlType=1,ControlDuration=31}.IsValid(),
    "invalid control type and excessive duration rejected");
guest2.RequestHit(1, 1, 25);
guest1.RequestHit(0, 1, 25);
await Task.Delay(150);
Check(!host.TryTakeHit(out _), "wrong scene and stale epoch hits ignored");
host.PublishWorld(new WorldState { Scene = "test-zone", Epoch = 1, Revision = 2, Enemies = new[] { enemy with { Health = 80 } } });
await Until(() => guest1.World.Enemies[0].Health == 80, "host health correction reaches guest");
host.PublishWorld(new WorldState { Scene = "test-zone", Epoch = 1, Revision = 3, Enemies = new[] { enemy with { Health = 0, Dead = true } } });
await Until(() => guest1.World.Enemies[0].Dead, "host death state reaches guest");
Check(guest1.World.RewardEnemyIds.SequenceEqual(new[]{1}) && guest2.World.RewardEnemyIds.Length == 0,
    "death reward eligibility is limited to guests present in the host instance at death");
Check(guest1.TryClaimReward(1,1) && !guest1.TryClaimReward(1,1) && !guest2.TryClaimReward(1,1),
    "session reward claim prevents duplicate dispatch after local scene recreation and rejects ineligible guests");
Check(!host.CanRetireDeath(1,1), "host retains dead record until the eligible guest reports reward completion");
Check(guest2.ReportDeath(new DeathReceipt(1,1,DeathReceiptOutcome.Completed)), "wrong-zone guest can send a receipt but cannot authorize retirement");
await Task.Delay(100);
Check(!host.CanRetireDeath(1,1), "authenticated receipt from ineligible guest is ignored");
Check(guest1.ReportDeath(new DeathReceipt(1,1,DeathReceiptOutcome.Completed)), "guest queues native reward completion receipt");
await Until(()=>host.CanRetireDeath(1,1), "native reward completion acknowledgement reaches host over real TCP");
try { host.PublishWorld(new WorldState { Epoch = 0, Revision = 100 }); throw new Exception("Old world accepted"); }
catch (ArgumentException) { Check(true, "host rejects backwards world epoch"); }
guest2.Dispose();
await Until(() => host.Players.All(p => p.Id != guest2.LocalId), "disconnect removes stale peer");
using var rejoined = await LanSession.JoinAsync("127.0.0.1", host.Port, "Rejoined", build, code);
await Until(() => rejoined.Players.Any(p => p.Id == guest1.LocalId), "late join receives existing peers");
await Until(() => rejoined.World.Enemies.Length == 1 && rejoined.World.Enemies[0].Dead, "late join receives current host death state");
Check(rejoined.World.RewardEnemyIds.Length == 0, "late join cannot claim retrospective rewards from existing corpses");
await Until(() => rejoined.HudRevision == 1, "late join receives the current full HUD configuration");
rejoined.Dispose();
await Until(() => host.Players.All(p => p.Id != rejoined.LocalId), "reconnected peer can leave cleanly");

// TCP may split a frame into arbitrary chunks. Exercise the actual parser with one-byte writes.
using (var raw = new TcpClient())
{
    await raw.ConnectAsync(IPAddress.Loopback, host.Port);
    byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new Packet { Kind = "hello", Build = build, Code = code, Name = "Fragmented" });
    byte[] frame = new byte[payload.Length + 4];
    BinaryPrimitives.WriteInt32BigEndian(frame, payload.Length);
    payload.CopyTo(frame, 4);
    foreach (byte value in frame) await raw.GetStream().WriteAsync(new[] { value });
    Packet welcome = await Wire.ReadAsync(raw.GetStream(), CancellationToken.None);
    Check(welcome.Kind == "welcome", "fragmented TCP frame handshake");
    await Wire.WriteAsync(raw.GetStream(), new Packet { Kind = "state", State = new PlayerState { Sequence = 20, Id = host.LocalId, Name = "Forged", X = 99, Available = true, Scene = "test-zone" } }, CancellationToken.None);
    await Until(() => host.Players.Any(p => p.Id == welcome.Id && p.X == 99 && p.Name == "Fragmented"), "host assigns peer identity and ignores forged identity");
    await Wire.WriteAsync(raw.GetStream(), new Packet { Kind = "state", State = new PlayerState { Sequence = 19 } }, CancellationToken.None);
    await Until(() => host.Players.All(p => p.Id != welcome.Id), "out-of-order state disconnects only offending peer");
}
await Until(() => host.Players.Count == 2, "normal peer remains after invalid peer disconnect");
using (var forged = new TcpClient())
{
    await forged.ConnectAsync(IPAddress.Loopback, host.Port);
    await Wire.WriteAsync(forged.GetStream(), new Packet { Kind = "hello", Build = build, Code = code, Name = "ForgedWorld" }, CancellationToken.None);
    var welcome = await Wire.ReadAsync(forged.GetStream(), CancellationToken.None);
    await Wire.WriteAsync(forged.GetStream(), new Packet { Kind = "state", State = new PlayerState(), Hud = host.Hud }, CancellationToken.None);
    await Task.Delay(150);
    Check(host.Players.All(p => p.Id != welcome.Id) && host.World.Enemies[0].Dead && host.HudRevision == 1, "forged guest HUD configuration rejected without changing authority");
}
host.PublishWorld(new WorldState { Scene = "new-zone", Epoch = 2, Revision = 4 });
host.PublishHud(new HudConfiguration { Revision = 2, Values = new[] { new HudValue { Key = "Legacy.Character.Cheats.DensityMultiplier", Json = "3.5" } } });
await Until(() => guest1.HudRevision == 2 && guest1.Hud!.Values[0].Json == "3.5", "later host slider changes update the guest configuration");
using (var hudLate = await LanSession.JoinAsync("127.0.0.1", host.Port, "HudLate", build, code))
    await Until(() => hudLate.HudRevision == 2 && hudLate.Hud!.Values[0].Json == "3.5", "late join receives the latest changed configuration rather than defaults");
await Until(() => guest1.World.Scene == "new-zone" && guest1.World.Enemies.Length == 0, "zone epoch clears old enemies");
var maximumAnimation = new AnimationSnapshot { Speed = 100, Layers = Enumerable.Range(0, 8)
    .Select(_ => new AnimationLayer { Hash = int.MinValue, Time = 99999.999f, Weight = 0.99999994f }).ToArray() };
var crowded = Enumerable.Range(1, 512).Select(i => enemy with { Id = i, Template = new string('x', 256), Animation = maximumAnimation,
    Visuals = i <= 64 ? new[] { new SkillVisualState { Id = i, AbilityKey = 1, Lines = Enumerable.Range(0, 4).Select(line =>
        new VisualLine { Index = line, Points = Enumerable.Range(0, 16).Select(point =>
            new VisualPoint { X = 99999.999f, Y = -99999.999f, Z = 99999.999f }).ToArray() }).ToArray() } } : Array.Empty<SkillVisualState>() }).ToArray();
var denseEvents = Enumerable.Range(0, 128).Select(index => new SceneGraphState { Path = index + new string('x', 1000),
    Asset = new string('y', 256), Node = 255, NodeCount = 256 }).ToArray();
var denseObjects = Enumerable.Range(0, 256).Select(i => new MapObjectState { Path = i.ToString("X64"), Exists = true, Active = true }).ToArray();
host.PublishWorld(new WorldState { Scene = "test-zone", Epoch = 3, Revision = 5, Enemies = crowded, SceneGraphs = denseEvents, MapObjects = denseObjects });
await Until(() => guest1.World.Enemies.Length == 512, "bounded full snapshot supports 512 enemies");
Check(guest1.World.Enemies.All(e => e.Animation!.Layers.Length == 8),
    "512 enemies with eight animation layers fit the actual framed transport");
Check(guest1.World.SceneGraphs.Length == 128 && guest1.World.Enemies.Sum(e => e.Visuals.Length) == 64,
    "512 enemies, 64 beam effects and 128 scene events fit combined framed transport");
Check(guest1.World.MapObjects.Length == 256, "maximum map object snapshot fits alongside 512 enemies 64 effects and 128 scene graphs");
host.PublishWorld(new WorldState { Scene="test-zone", Epoch=3, Revision=6, Enemies=Enumerable.Range(1,2400).Select(id=>enemy with {Id=id,Visuals=Array.Empty<SkillVisualState>()}).ToArray() });
await Until(()=>guest1.World.Enemies.Length == 2400, "2400 live identities arrive intact through actual chunked TCP transport", 6000);
host.PublishWorld(host.World with {Revision=7,Enemies=host.World.Enemies.Where(e=>e.Id!=2400).Select(e=>e.Id==1 ? e with {Health=e.Health/2} : e).ToArray()});
await Until(()=>guest1.World.Revision==7 && guest1.World.Enemies.Length==2399,"sparse damage/removal delta commits through actual loopback TCP",6000);
Check(guest1.World.Enemies.Single(e=>e.Id==1).Health==enemy.Health/2 && guest1.World.Enemies.All(e=>e.Id!=2400),
    "real transport preserves unchanged enemies while applying HP and removal from the matching baseline");
using (var hitter = new TcpClient())
{
    await hitter.ConnectAsync(IPAddress.Loopback, host.Port);
    await Wire.WriteAsync(hitter.GetStream(), new Packet { Kind = "hello", Build = build, Code = code, Name = "HitReplay" }, CancellationToken.None);
    var welcome = await Wire.ReadAsync(hitter.GetStream(), CancellationToken.None);
    var hit = new HitRequest { PlayerId = host.LocalId, Epoch = 3, EnemyId = 1, Sequence = 1, Damage = 7 };
    await Wire.WriteAsync(hitter.GetStream(), new Packet { Kind = "state", State = new PlayerState { Available = true, Scene = "test-zone" }, Hits = new[] { hit, hit } }, CancellationToken.None);
    HitRequest? command = null;
    await Until(() => { if (host.TryTakeHit(out var got)) command = got; return command != null; }, "raw damage command delivered");
    Check(command!.PlayerId == welcome.Id && !host.TryTakeHit(out _), "forged hit identity replaced and duplicate damage ignored");
    await Wire.WriteAsync(hitter.GetStream(), new Packet { Kind = "state", State = new PlayerState { Available = true, Scene = "test-zone", X = 1000 }, Hits = new[] { hit with { Sequence = 2 } } }, CancellationToken.None);
    await Task.Delay(150);
    Check(!host.TryTakeHit(out _), "out-of-range guest damage ignored");
}
Check(!guest1.RequestHit(3, 1, float.NaN) && !guest1.RequestHit(3, 1, 0), "non-finite and zero hit rejected");
using (var malformed = new TcpClient())
{
    await malformed.ConnectAsync(IPAddress.Loopback, host.Port);
    byte[] header = new byte[4];
    BinaryPrimitives.WriteInt32BigEndian(header, Wire.MaximumFrameBytes + 1);
    await malformed.GetStream().WriteAsync(header);
    using var deadline = new CancellationTokenSource(3000);
    Check(await malformed.GetStream().ReadAsync(new byte[1], deadline.Token) == 0, "oversized frame rejected before allocating payload");
}
using var guest3 = await LanSession.JoinAsync("127.0.0.1", host.Port, "Guest3", build, code);
await Until(()=>guest3.World.Revision==7 && guest3.World.Enemies.Length==2399,"late join receives a complete baseline after existing peers have consumed deltas",6000);
using var guest4 = await LanSession.JoinAsync("127.0.0.1", host.Port, "Guest4", build, code);
await Until(() => host.Players.Count == 4, "host supports three LAN guests");
try
{
    using var excess = await LanSession.JoinAsync("127.0.0.1", host.Port, "Excess", build, code);
    throw new Exception("Unexpected fifth player accepted.");
}
catch (IOException) { Check(true, "fifth player rejected"); }
host.Dispose();
await Until(() => !guest1.Connected && guest1.Players.Count == 0, "host shutdown clears guest state");

await GiftTests.Run(Check);
string output = args.Length > 0 ? args[0] : "selftest-result.json";
File.WriteAllText(output, JsonSerializer.Serialize(new { utc = DateTimeOffset.UtcNow, passed = checks.Count, checks,
    motion = MotionTests.Metrics,
    scope = "TCP transport on loopback; no claim of game combat or two-machine LAN validation" }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"All {checks.Count} checks passed.");
