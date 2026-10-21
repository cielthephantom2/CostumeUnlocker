using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using LastEpochLAN.Core.Encounters;

namespace LastEpochLAN.Core;

/// <summary>LAN transport with host-owned world snapshots and authenticated guest hit requests.</summary>
public sealed partial class LanSession : IDisposable
{
    private readonly CancellationTokenSource stop = new();
    private readonly ConcurrentDictionary<string, Peer> peers = new();
    private readonly ConcurrentDictionary<TcpClient, byte> sockets = new();
    private readonly string build;
    private readonly string code;
    private readonly string name;
    private readonly TcpListener? listener;
    private readonly TcpClient? upstream;
    private PlayerState local;
    private PlayerState[] snapshot = Array.Empty<PlayerState>();
    private string status = "Starting";
    private long sequence;
    private int disposed;
    private int slots;
    private WorldState world = new();
    private PartyEchoState? echo;
    public PartyEchoState? Echo => Volatile.Read(ref echo)?.Copy();
    public long EchoRevision=>Volatile.Read(ref echo)?.Revision??0;
    public void PublishEcho(PartyEchoState value)
    {
        if(!IsHost) throw new InvalidOperationException("Only the Host can publish an Echo.");
        if(!value.IsValid() || value.Revision<=(Volatile.Read(ref echo)?.Revision??0))
            throw new ArgumentException("Invalid or stale Echo state.");
        Volatile.Write(ref echo,value.Copy());
    }
    private readonly EnemyDeathLedger deathLedger = new();
    private readonly ConcurrentQueue<DeathReceipt> outgoingDeathReceipts = new();
    private readonly ConcurrentDictionary<(long Epoch, int EnemyId), byte> claimedRewards = new();
    public bool TryClaimReward(long epoch, int enemyId)
    {
        var current = Volatile.Read(ref world);
        return !IsHost && Connected && current.Epoch == epoch && current.RewardEnemyIds.Contains(enemyId) && claimedRewards.TryAdd((epoch,enemyId),0);
    }
    public bool CanRetireDeath(long epoch, int enemyId) => IsHost && deathLedger.CanRetire(epoch, enemyId, peers.Keys);
    public HashSet<int> RetireAcknowledgedDeaths(long epoch) => IsHost ? deathLedger.RetireAcknowledged(epoch, peers.Keys) : new HashSet<int>();
    public bool ReportDeath(DeathReceipt receipt)
    {
        if (IsHost || !Connected || !receipt.IsValid() || outgoingDeathReceipts.Count >= WorldTransfer.MaximumEnemies) return false;
        claimedRewards.TryAdd((receipt.Epoch,receipt.EnemyId),0);
        outgoingDeathReceipts.Enqueue(receipt); return true;
    }
    private HudConfiguration? hud;
    public HudConfiguration? Hud => Volatile.Read(ref hud)?.Copy();
    public long HudRevision => Volatile.Read(ref hud)?.Revision ?? 0;
    public void PublishHud(HudConfiguration value)
    {
        if (!IsHost) throw new InvalidOperationException("Only the host can publish HUD configuration.");
        if (!value.IsValid() || value.Revision <= (Volatile.Read(ref hud)?.Revision ?? 0)) throw new ArgumentException("Invalid HUD configuration revision or values.");
        Volatile.Write(ref hud, value.Copy());
    }
    private readonly ConcurrentQueue<HitRequest> outgoingHits = new();
    private readonly ConcurrentQueue<HitRequest> incomingCombat = new();
    private readonly ConcurrentQueue<StatusCallback> statusCallbacks = new();
    public bool TryTakeCombat(out HitRequest? command)=>incomingCombat.TryDequeue(out command);
    public bool TryTakeAilment(out HitRequest? application)
    {
        application=null;
        return incomingCombat.TryPeek(out var next) && next.Ailment!=null && incomingCombat.TryDequeue(out application);
    }
    public bool TryTakeStatusCallback(out StatusCallback? callback)=>statusCallbacks.TryDequeue(out callback);
    public bool SendStatusCallback(string recipient,StatusCallback callback)
    {
        if(!IsHost || !callback.IsValid() || !peers.TryGetValue(recipient,out var peer) || peer.StatusCallbacks.Count>=2048) return false;
        peer.StatusCallbacks.Enqueue(callback);return true;
    }
    private long hitSequence;
    private long statusVisualSequence;
    private readonly ConcurrentDictionary<int,StatusVisualReport> outgoingStatusVisuals=new();
    private readonly object statusVisualGate=new();
    private readonly Dictionary<(string Source,int Enemy),StatusVisualReport> incomingStatusVisuals=new();
    private readonly Queue<(string Source,int Enemy)> incomingStatusKeys=new();
    public bool PublishStatusVisuals(long epoch,int enemy,StatusVisualGroup[] groups)
    {
        var report=new StatusVisualReport {Epoch=epoch,EnemyId=enemy,Groups=groups,Sequence=Interlocked.Increment(ref statusVisualSequence)};
        if(IsHost || !Connected || !report.IsValid() || outgoingStatusVisuals.Count>=1024 && !outgoingStatusVisuals.ContainsKey(enemy)) return false;
        outgoingStatusVisuals[enemy]=report.Copy();return true;
    }
    public bool TryTakeStatusVisuals(out StatusVisualReport? report)
    {
        lock(statusVisualGate)
        {
            while(incomingStatusKeys.TryDequeue(out var key))
                if(incomingStatusVisuals.Remove(key,out report)) return true;
            report=null;return false;
        }
    }
    private long skillSequence;
    private long damageSequence;
    private long worldFramesSent, worldFramesOmitted;
    private long sentBytes,receivedBytes,sentFrames,receivedFrames,sendTicks,maxSendTicks;
    public NetworkSummary NetworkStats
    {
        get
        {
            long frames=Interlocked.Read(ref sentFrames);
            return new(Interlocked.Read(ref sentBytes),Interlocked.Read(ref receivedBytes),frames,Interlocked.Read(ref receivedFrames),
                frames==0?0:Interlocked.Read(ref sendTicks)*1000d/System.Diagnostics.Stopwatch.Frequency/frames,
                Interlocked.Read(ref maxSendTicks)*1000d/System.Diagnostics.Stopwatch.Frequency,outgoingHits.Count,incomingCombat.Count);
        }
    }
    private async Task WritePacketAsync(Stream stream,Packet packet,CancellationToken token)
    {
        long started=System.Diagnostics.Stopwatch.GetTimestamp();
        int size=await Wire.WriteAsync(stream,packet,token).ConfigureAwait(false);
        long ticks=System.Diagnostics.Stopwatch.GetTimestamp()-started;
        Interlocked.Add(ref sentBytes,size);Interlocked.Increment(ref sentFrames);Interlocked.Add(ref sendTicks,ticks);
        long old=Interlocked.Read(ref maxSendTicks);
        while(ticks>old) {long seen=Interlocked.CompareExchange(ref maxSendTicks,ticks,old);if(seen==old)break;old=seen;}
    }
    private async Task<Packet> ReadPacketAsync(Stream stream,CancellationToken token)
    {
        var packet=await Wire.ReadAsync(stream,token).ConfigureAwait(false);
        Interlocked.Add(ref receivedBytes,packet.ReceivedBytes+4);Interlocked.Increment(ref receivedFrames);return packet;
    }
    public long WorldFramesSent => Interlocked.Read(ref worldFramesSent);
    public long WorldFramesOmitted => Interlocked.Read(ref worldFramesOmitted);
    private readonly ConcurrentQueue<IncomingDamage> receivedDamage = new();
    private readonly ConcurrentQueue<GiftMessage> outgoingGifts = new();
    private readonly ConcurrentQueue<GiftMessage> receivedGifts = new();
    private readonly ConcurrentQueue<ChestCommand> outgoingChest=new();
    private readonly ConcurrentQueue<ChestRequest> receivedChest=new();
    private readonly ConcurrentQueue<ChestReply> chestReplies=new();
    public bool TryTakeChestRequest(out ChestRequest? request)=>receivedChest.TryDequeue(out request);
    public bool TryTakeChestReply(out ChestReply? reply)=>chestReplies.TryDequeue(out reply);
    public bool SendChest(ChestCommand command)
    {
        if(!Connected || !command.Valid())return false;
        if(IsHost){if(receivedChest.Count>=128)return false;receivedChest.Enqueue(new(LocalId,command));}
        else {if(outgoingChest.Count>=128)return false;outgoingChest.Enqueue(command);}
        return true;
    }
    public bool ReplyChest(string playerId,ChestReply reply)
    {
        if(!IsHost || !Connected || !reply.Valid())return false;
        if(playerId==LocalId){if(chestReplies.Count>=128)return false;chestReplies.Enqueue(reply);return true;}
        if(!peers.TryGetValue(playerId,out var target) || target.ChestReplies.Count>=128)return false;
        target.ChestReplies.Enqueue(reply);return true;
    }
    public bool TryTakeGift(out GiftMessage? message) => receivedGifts.TryDequeue(out message);
    public bool PublishGift(GiftMessage message)
    {
        var player = Volatile.Read(ref local);
        message = message with { FromIdentity = player.GiftIdentity, FromCharacter = player.CharacterId };
        if (!Connected || !message.IsValid()) return false;
        if (IsHost) return RelayGift(message);
        if (outgoingGifts.Count >= 256) return false;
        outgoingGifts.Enqueue(message); return true;
    }
    private bool RelayGift(GiftMessage message)
    {
        var localPlayer = Volatile.Read(ref local);
        var targets = peers.Values.Where(p => p.State.GiftIdentity == message.ToIdentity && p.State.CharacterId == message.ToCharacter).ToArray();
        bool toLocal = localPlayer.GiftIdentity == message.ToIdentity && localPlayer.CharacterId == message.ToCharacter;
        // Duplicated installation identities must never deliver to two characters.
        if (targets.Length + (toLocal ? 1 : 0) != 1) return false;
        if (toLocal)
        {
            if (receivedGifts.Count >= 256) return false;
            receivedGifts.Enqueue(message); return true;
        }
        if (targets[0].Gifts.Count >= 256) return false;
        targets[0].Gifts.Enqueue(message); return true;
    }
    public bool TryTakeDamage(out IncomingDamage? damage) => receivedDamage.TryDequeue(out damage);
    public bool SendDamage(string recipientId, IncomingDamage damage)
    {
        if (!IsHost || !Connected || !peers.TryGetValue(recipientId, out var peer)) return false;
        damage = damage with { Sequence = Interlocked.Increment(ref damageSequence) };
        var player = Volatile.Read(ref peer.State);
        var currentWorld = Volatile.Read(ref world);
        if (!damage.IsValid() || damage.Epoch != currentWorld.Epoch || damage.Scene != currentWorld.Scene ||
            !player.Available || player.Dead || player.Scene != damage.Scene || peer.Damage.Count >= 128) return false;
        peer.Damage.Enqueue(damage.Copy()); return true;
    }
    private readonly ConcurrentQueue<SkillEvent> outgoingSkills = new();
    private readonly ConcurrentQueue<SkillEvent> receivedSkills = new();
    public bool TryTakeSkill(out SkillEvent? skill) => receivedSkills.TryDequeue(out skill);
    public bool PublishSkill(string ability, float x, float y, float z, int abilitySlot = -1, bool zoneAbility = false,
        int animationNumber = -1, float animationSpeed = 1, NativeVisualCommand? nativeVisual = null)
    {
        var player = Volatile.Read(ref local);
        var skill = new SkillEvent { PlayerId = LocalId, Scene = player.Scene, Ability = ability,
            AbilitySlot = abilitySlot, ZoneAbility = zoneAbility, NativeVisual = nativeVisual?.Copy(),
            AnimationNumber = animationNumber, AnimationSpeed = animationSpeed,
            Sequence = Interlocked.Increment(ref skillSequence), X = x, Y = y, Z = z };
        if (!Connected || !player.Available || player.Dead || !skill.IsValid()) return false;
        if (IsHost) RelaySkill(skill);
        else { if (outgoingSkills.Count >= 128) return false; outgoingSkills.Enqueue(skill); }
        return true;
    }
    private void RelaySkill(SkillEvent skill)
    {
        if (receivedSkills.Count < 128 && skill.PlayerId != LocalId) receivedSkills.Enqueue(skill);
        foreach (var peer in peers.Values) if (peer.Id != skill.PlayerId && peer.Skills.Count < 128) peer.Skills.Enqueue(skill);
    }
    public WorldState World => Volatile.Read(ref world).Copy();
    public string WorldEchoRunId=>Volatile.Read(ref world).EchoRunId;
    public bool AcceptsResolvedAilments=>Volatile.Read(ref world).AcceptsResolvedAilments;
    private double worldArrivalSeconds;
    public double WorldArrivalSeconds => Volatile.Read(ref worldArrivalSeconds);
    public static double MonotonicSeconds => System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency;
    public (long Epoch, long Revision, string Scene, int EnemyCount) WorldInfo
    {
        get { var value = Volatile.Read(ref world); return (value.Epoch, value.Revision, value.Scene, value.Enemies.Length); }
    }
    // UI/proximity/targeting only need immutable scalar identity and positions.
    // Do not deep-copy nested VFX/animation/item payloads for those consumers.
    // Host combat/proximity must read freshly authenticated peer input rather
    // than wait for the next outgoing 50ms broadcast to refresh its UI snapshot.
    private IEnumerable<PlayerState> CurrentPositions => IsHost
        ? new[] { Volatile.Read(ref local) }.Concat(peers.Values.Select(peer => Volatile.Read(ref peer.State)))
        : Volatile.Read(ref snapshot);
    public IReadOnlyList<PlayerState> PlayerPositions => CurrentPositions
        .Select(player => player with { Avatar = null, DroppedItems = Array.Empty<DroppedItemState>() }).ToArray();
    public bool TryGetPlayerLocation(string id,out PlayerLocation location)
    {
        PlayerState? state=null;
        if(IsHost)
        {
            if(id==LocalId) state=Volatile.Read(ref local);
            else if(peers.TryGetValue(id,out var peer)) state=Volatile.Read(ref peer.State);
        }
        else foreach(var candidate in Volatile.Read(ref snapshot)) if(candidate.Id==id) {state=candidate;break;}
        location=state==null?default:new(state.Id,state.Scene,state.Available,state.Dead,state.X,state.Y,state.Z);
        return state!=null;
    }
    public IReadOnlyList<PlayerState> PlayerOverview => Volatile.Read(ref snapshot)
        .Select(player => player with { Avatar = player.Avatar == null ? null : new AvatarState { ClassId = player.Avatar.ClassId }, DroppedItems = Array.Empty<DroppedItemState>() }).ToArray();
    public void PublishWorld(WorldState state)
    {
        if (!IsHost) throw new InvalidOperationException("Only the host publishes the world.");
        if (!state.IsValid()) throw new ArgumentException("Invalid world.");
        var previous = Volatile.Read(ref world);
        if (state.Epoch < previous.Epoch || (state.Epoch == previous.Epoch && state.Revision <= previous.Revision))
            throw new ArgumentException("World revision must advance.");
        deathLedger.Observe(state, peers.Values.Select(peer=>Volatile.Read(ref peer.State)));
        Volatile.Write(ref world, state.Copy());
    }
    public bool RequestHit(long epoch, int enemyId, float damage, bool cull = false, float cullPercent = 0, float flatCull = 0,bool sourceDot=false)
    {
        var hit = new HitRequest { Epoch = epoch, EnemyId = enemyId, Damage = damage, SourceDot=sourceDot,Cull = cull, CullPercent = cullPercent,
            FlatCull = flatCull, Sequence = Interlocked.Increment(ref hitSequence) };
        if (IsHost || !Connected || !hit.IsValid() || outgoingHits.Count >= 2048) return false;
        outgoingHits.Enqueue(hit);
        return true;
    }
    public bool TryTakeHit(out HitRequest? hit)
    {
        hit=null;
        return incomingCombat.TryPeek(out var next) && next.Ailment==null && incomingCombat.TryDequeue(out hit);
    }
    private static int EstimateHitBytes(HitRequest hit)
    {
        if(hit.Ailment==null) return 1024;
        int Nodes(StatusValue? value)=>value==null?0:1+value.Fields.Sum(f=>Nodes(f.Value))+value.Items.Sum(v=>Nodes(v));
        var ailment=hit.Ailment;
        return 2048+Math.Min(800000,Nodes(ailment.NativeDamage)*768+Nodes(ailment.Overrides)*768+
            (ailment.SourceProfile?.Stats.Sum(stat=>256+stat.More.Length*24)??0));
    }
    public bool RequestAilment(long epoch,int enemyId,ResolvedAilment application)
    {
        var request=new HitRequest {Epoch=epoch,EnemyId=enemyId,Ailment=application?.Copy(),Sequence=Interlocked.Increment(ref hitSequence)};
        if(IsHost || !Connected || !AcceptsResolvedAilments || !request.IsValid() || outgoingHits.Count>=1792) return false;
        outgoingHits.Enqueue(request);return true;
    }
    public bool RequestControl(long epoch, int enemyId, int type, float duration, float angle = 0)
    {
        var request = new HitRequest { Epoch=epoch, EnemyId=enemyId, ControlType=type, ControlDuration=duration, ControlAngle=angle,
            Sequence=Interlocked.Increment(ref hitSequence) };
        if (IsHost || !Connected || !request.IsValid() || outgoingHits.Count >= 128) return false;
        outgoingHits.Enqueue(request); return true;
    }
    public string LocalId { get; }
    public bool IsHost => listener != null;
    public bool Connected => Volatile.Read(ref disposed) == 0 && (IsHost || status == "Connected");
    public string Status => Volatile.Read(ref status);
    public int Port => listener == null ? 0 : ((IPEndPoint)listener.LocalEndpoint).Port;
    public IReadOnlyList<PlayerState> Players => Volatile.Read(ref snapshot).Select(player => player.Copy()).ToArray();

    private LanSession(string name, string build, string code, string id, TcpListener? listener, TcpClient? upstream)
    {
        this.name = name;
        this.build = build;
        this.code = code;
        this.listener = listener;
        this.upstream = upstream;
        LocalId = id;
        local = new PlayerState { Id = id, Name = name };
    }

    private static void ValidateSettings(string name, string build, string code, int port)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 32) throw new ArgumentException("Name: 1–32 characters.");
        if (string.IsNullOrWhiteSpace(build) || build.Length > 128) throw new ArgumentException("Invalid build fingerprint.");
        if (code.Length < 6 || code.Length > 64) throw new ArgumentException("Room code: 6–64 characters.");
        if (port < 0 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
    }

    public static LanSession Host(string name, string build, string code, int port = 7777, IPAddress? bind = null)
    {
        ValidateSettings(name, build, code, port);
        if (bind != null && bind != IPAddress.Any && !LanNetworks.IsSupported(bind)) throw new ArgumentException("Bind to a local unicast IPv4 adapter, or 0.0.0.0 for all interfaces.");
        var listener = new TcpListener(bind ?? IPAddress.Any, port);
        listener.Start(8);
        var session = new LanSession(name, build, code, Guid.NewGuid().ToString("N"), listener, null);
        session.status = "Hosting";
        _ = session.AcceptLoopAsync();
        _ = session.HostTickAsync();
        return session;
    }

    public static async Task<LanSession> JoinAsync(string ip, int port, string name, string build, string code, CancellationToken cancellation = default)
    {
        ValidateSettings(name, build, code, port);
        if (port == 0 || !IPAddress.TryParse(ip, out var address) || address.AddressFamily != AddressFamily.InterNetwork || !LanNetworks.IsSupported(address))
            throw new ArgumentException("Enter the host public IPv4 address (or a local/VPN IPv4) and a port from 1–65535. Default TCP port: 7777.");
        var client = new TcpClient(AddressFamily.InterNetwork) { NoDelay = true };
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            await client.ConnectAsync(address.MapToIPv4(), port, deadline.Token).ConfigureAwait(false);
            await Wire.WriteAsync(client.GetStream(), new Packet { Kind = "hello", Build = build, Code = code, Name = name }, deadline.Token).ConfigureAwait(false);
            Packet response = await Wire.ReadAsync(client.GetStream(), deadline.Token).ConfigureAwait(false);
            if (response.Kind == "reject") throw new InvalidOperationException(response.Error);
            if (response.Kind != "welcome" || response.Protocol != LanProtocol.Version || response.Build != build || !Guid.TryParse(response.Id, out _))
                throw new InvalidDataException("Invalid host handshake.");
            var session = new LanSession(name, build, code, response.Id, null, client) { status = "Connected" };
            session.sockets.TryAdd(client, 0);
            _ = session.ClientReadAsync();
            _ = session.ClientTickAsync();
            return session;
        }
        catch { client.Dispose(); throw; }
    }

    public void Publish(PlayerState state)
    {
        if (!state.IsValid()) throw new ArgumentException("Invalid player state.");
        Volatile.Write(ref local, state.Copy() with { Id = LocalId, Name = name, Sequence = Interlocked.Increment(ref sequence) });
    }

    private async Task AcceptLoopAsync()
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                TcpClient client = await listener!.AcceptTcpClientAsync(stop.Token).ConfigureAwait(false);
                client.NoDelay = true;
                bool supportedPeer = LanNetworks.IsSupported(((IPEndPoint)client.Client.RemoteEndPoint!).Address);
                if (!supportedPeer || Interlocked.Increment(ref slots) > 3)
                {
                    // Balance slots only when the endpoint check actually reserved one.
                    if (supportedPeer) Interlocked.Decrement(ref slots);
                    client.Dispose();
                    continue;
                }
                sockets.TryAdd(client, 0);
                _ = ServePeerAsync(client);
            }
        }
        catch (Exception ex) when (IsConnectionError(ex))
        {
            if (!stop.IsCancellationRequested) { status = "Listener failed: " + ex.Message; Dispose(); }
        }
    }

    private async Task ServePeerAsync(TcpClient client)
    {
        Peer? peer = null;
        try
        {
            Packet hello = await ReadPacketAsync(client.GetStream(), stop.Token).ConfigureAwait(false);
            string rejection = hello.Kind != "hello" || hello.Protocol != LanProtocol.Version ? $"Protocol mismatch: host requires {LanProtocol.Version}; guest sent {hello.Protocol}. Update BOTH Mods/LastEpochLAN.dll and UserLibs/LastEpochLAN.Core.dll on both PCs, then restart both games." :
                hello.Build != build ? "Game build mismatch." :
                !CodesMatch(hello.Code, code) ? "Incorrect room code." :
                string.IsNullOrWhiteSpace(hello.Name) || hello.Name.Length > 32 ? "Invalid name." : "";
            if (rejection != "")
            {
                await WritePacketAsync(client.GetStream(), new Packet { Kind = "reject", Error = rejection }, stop.Token).ConfigureAwait(false);
                return;
            }
            peer = new Peer(client, hello.Name);
            await WritePacketAsync(client.GetStream(), new Packet { Kind = "welcome", Build = build, Id = peer.Id }, stop.Token).ConfigureAwait(false);
            peers.TryAdd(peer.Id, peer);
            _ = PeerTickAsync(peer);
            long lastSequence = -1;
            long lastHitSequence = 0;
            WorldState? indexedWorld=null;
            var indexedEnemies=new Dictionary<int,EnemyState>();
            var lastStatusSequence=new Dictionary<int,long>();
            long lastSkillSequence = 0;
            long rateWindow = Environment.TickCount64;
            int rateCount = 0;
            while (!stop.IsCancellationRequested)
            {
                Packet packet = await ReadPacketAsync(client.GetStream(), stop.Token).ConfigureAwait(false);
                if (Environment.TickCount64 - rateWindow >= 1000) { rateWindow = Environment.TickCount64; rateCount = 0; }
                if (++rateCount > 80) throw new InvalidDataException("Rate limit exceeded.");
                if (packet.Protocol == LanProtocol.Version && packet.Kind == "loot-filters")
                {
                    if (!LootFilterOnly(packet) || packet.LootFilters is not { Length: 1 } ||
                        packet.LootFilters[0] == null || !packet.LootFilters[0].IsValid())
                        throw new InvalidDataException("Invalid guest loot filter.");
                    // Ownership always comes from the authenticated connection.
                    Volatile.Write(ref peer.LootFilter, packet.LootFilters[0] with { OwnerId = peer.Id });
                    continue;
                }
                if (packet.LootFilters != null) throw new InvalidDataException("Filter requires its own frame.");
                if (packet.Protocol != LanProtocol.Version || packet.Kind != "state" || packet.State == null || !packet.State.IsValid()) throw new InvalidDataException("Invalid state.");
                if (packet.State.Sequence < lastSequence) throw new InvalidDataException("Out-of-order state.");
                lastSequence = packet.State.Sequence;
                Volatile.Write(ref peer.State, packet.State with { Id = peer.Id, Name = peer.Name });
                if (packet.DeathReceipts is { Length: > 32 }) throw new InvalidDataException("Too many death receipts.");
                foreach (var receipt in packet.DeathReceipts ?? Array.Empty<DeathReceipt>())
                {
                    if (receipt == null || !receipt.IsValid()) throw new InvalidDataException("Invalid death receipt.");
                    deathLedger.Record(peer.Id, receipt);
                }
                if (packet.Gifts is { Length: > 8 }) throw new InvalidDataException("Too many gift commands.");
                if(packet.ChestReplies!=null || packet.ChestCommands is {Length:>8})throw new InvalidDataException("Invalid chest authority/budget");
                foreach(var command in packet.ChestCommands??Array.Empty<ChestCommand>())
                {
                    if(command==null || !command.Valid() || receivedChest.Count>=128)throw new InvalidDataException("Invalid chest command");
                    receivedChest.Enqueue(new(peer.Id,command));
                }
                foreach (var gift in packet.Gifts ?? Array.Empty<GiftMessage>())
                {
                    if (gift == null || !gift.IsValid()) throw new InvalidDataException("Invalid gift command.");
                    var player = Volatile.Read(ref peer.State);
                    if (Guid.TryParseExact(player.GiftIdentity, "N", out _) && player.CharacterId != "")
                        RelayGift(gift with { FromIdentity = player.GiftIdentity, FromCharacter = player.CharacterId });
                }
                if (packet.Skills is { Length: > 16 }) throw new InvalidDataException("Too many skill events.");
                foreach (var skill in packet.Skills ?? Array.Empty<SkillEvent>())
                {
                    if (skill == null || !skill.IsValid()) throw new InvalidDataException("Invalid skill event.");
                    if (skill.Sequence <= lastSkillSequence) continue;
                    lastSkillSequence = skill.Sequence;
                    var player = Volatile.Read(ref peer.State);
                    if (player.Available && !player.Dead && skill.Scene == player.Scene)
                        RelaySkill(skill with { PlayerId = peer.Id });
                }
                if (packet.Echo!=null || packet.World != null || packet.WorldChunk != null || packet.Hud != null || packet.IncomingDamage != null || packet.StatusCallbacks!=null || packet.Hits is { Length: > 32 }) throw new InvalidDataException("Guest cannot publish host configuration, a world or host attack.");
                foreach (var hit in packet.Hits ?? Array.Empty<HitRequest>())
                {
                    if (hit == null || !hit.IsValid()) throw new InvalidDataException("Invalid hit.");
                    if (hit.Sequence <= lastHitSequence) continue;
                    lastHitSequence = hit.Sequence;
                    // Apply TCP backpressure instead of silently losing damage
                    // or status commands while the Unity consumer is behind.
                    while(incomingCombat.Count>=2048) await Task.Delay(2,stop.Token).ConfigureAwait(false);
                    WorldState authoritative = Volatile.Read(ref world);
                    if(!ReferenceEquals(indexedWorld,authoritative))
                    {indexedEnemies=authoritative.Enemies.ToDictionary(enemy=>enemy.Id);indexedWorld=authoritative;}
                    PlayerState player = Volatile.Read(ref peer.State);
                    if(hit.Ailment!=null && !authoritative.AcceptsResolvedAilments) continue;
                    indexedEnemies.TryGetValue(hit.EnemyId,out var target);
                    if (hit.Epoch != authoritative.Epoch || !player.Available || player.Dead && !hit.SourceDot && hit.Ailment==null ||
                        player.Scene != authoritative.Scene || player.EchoRunId!=authoritative.EchoRunId || target == null || target.Dead) continue;
                    double distance = Math.Pow(player.X - target.X, 2) + Math.Pow(player.Y - target.Y, 2) + Math.Pow(player.Z - target.Z, 2);
                    if (distance <= 10000)
                    {
                        incomingCombat.Enqueue(hit with {PlayerId=peer.Id});
                    }
                }
                if(packet.StatusVisuals is {Length:>32}) throw new InvalidDataException("Too many status reports.");
                foreach(var report in packet.StatusVisuals ?? Array.Empty<StatusVisualReport>())
                {
                    if(report==null || !report.IsValid()) throw new InvalidDataException("Invalid status report.");
                    if(lastStatusSequence.TryGetValue(report.EnemyId,out var seen) && report.Sequence<=seen) continue;
                    var current=Volatile.Read(ref world);var owner=Volatile.Read(ref peer.State);
                    var target=current.Enemies.FirstOrDefault(e=>e.Id==report.EnemyId);
                    if(report.Epoch!=current.Epoch || !owner.Available || owner.Scene!=current.Scene || target==null || target.Dead) continue;
                    if(Math.Pow(owner.X-target.X,2)+Math.Pow(owner.Y-target.Y,2)+Math.Pow(owner.Z-target.Z,2)>10000) continue;
                    if(lastStatusSequence.Count>=65536 && !lastStatusSequence.ContainsKey(report.EnemyId)) throw new InvalidDataException("Status identity budget exceeded.");
                    lastStatusSequence[report.EnemyId]=report.Sequence;
                    lock(statusVisualGate)
                    {
                        var key=(peer.Id,report.EnemyId);
                        if(incomingStatusVisuals.Count>=1024 && !incomingStatusVisuals.ContainsKey(key)) continue;
                        if(!incomingStatusVisuals.ContainsKey(key)) incomingStatusKeys.Enqueue(key);
                        incomingStatusVisuals[key]=report with {Source=peer.Id};
                    }
                }
                // Expose encounter actions only after the complete state frame
                // has passed direction, budget and payload validation.
                ReceiveEncounterCommands(peer, packet);
            }
        }
        catch (Exception ex) when (IsConnectionError(ex)) { }
        finally
        {
            if (peer != null) peers.TryRemove(peer.Id, out _);
            sockets.TryRemove(client, out _);
            client.Dispose();
            Interlocked.Decrement(ref slots);
        }
    }

    private async Task HostTickAsync()
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                Peer[] current = peers.Values.ToArray();
                PlayerState[] states = new[] { Volatile.Read(ref local) }.Concat(current.Select(p => Volatile.Read(ref p.State))).ToArray();
                Volatile.Write(ref snapshot, states);
                // Each peer owns its writer. A slow receiver must not throttle
                // player/world snapshots for the other connected players.
                await Task.Delay(50, stop.Token).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (IsConnectionError(ex)) { }
    }

    private async Task PeerTickAsync(Peer peer)
    {
        try
        {
            while(!stop.IsCancellationRequested && peers.ContainsKey(peer.Id))
            {
                var states=Volatile.Read(ref snapshot);
                if(states.Length>0) await SendSnapshotAsync(peer,states).ConfigureAwait(false);
                await Task.Delay(50,stop.Token).ConfigureAwait(false);
            }
        }
        catch(Exception ex) when(IsConnectionError(ex)) {peer.Client.Dispose();}
    }

    private async Task SendSnapshotAsync(Peer peer, PlayerState[] states)
    {
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
            deadline.CancelAfter(TimeSpan.FromSeconds(5));
            var filters = CreateLootFilterRoster();
            if (peer.LastLootFilters == null || !filters.SequenceEqual(peer.LastLootFilters))
            {
                await WritePacketAsync(peer.Client.GetStream(), new Packet { Kind = "loot-filters", LootFilters = filters }, deadline.Token).ConfigureAwait(false);
                peer.LastLootFilters = filters;
            }
            var skills = new List<SkillEvent>();
            while (skills.Count < 16 && peer.Skills.TryDequeue(out var skill)) skills.Add(skill);
            var damage = new List<IncomingDamage>();
            while (damage.Count < 32 && peer.Damage.TryDequeue(out var hit)) damage.Add(hit);
            var currentWorld = Volatile.Read(ref world);
            var gifts = new List<GiftMessage>();
            var chest=new List<ChestReply>();
            // Category/priority metadata can be large. Keep one bounded reply
            // per frame instead of combining two near the1MB wire limit.
            while(chest.Count<1 && peer.ChestReplies.TryDequeue(out var chestReply))chest.Add(chestReply);
            while (gifts.Count < 8 && peer.Gifts.TryDequeue(out var gift)) gifts.Add(gift);
            bool changed = !ReferenceEquals(currentWorld, peer.LastWorld);
            var chunks = changed ? peer.WorldWriter.Encode(currentWorld with { RewardEnemyIds = deathLedger.Eligible(peer.Id, currentWorld) },allowDelta:true) : Array.Empty<WorldChunk>();
            var currentHud = Volatile.Read(ref hud);
            var currentEcho=Volatile.Read(ref echo);
            var callbacks=new List<StatusCallback>();
            while(callbacks.Count<64 && peer.StatusCallbacks.TryDequeue(out var callback)) callbacks.Add(callback);
            var encounterAcks = new List<EncounterReply>();
            while (encounterAcks.Count < 8 && peer.EncounterReplies.TryDequeue(out var encounterAck)) encounterAcks.Add(encounterAck);
            var publication = Volatile.Read(ref encounterPublication);
            var encounterPlayer = Volatile.Read(ref peer.State);
            var visibility = (encounterPlayer.Cooperative, encounterPlayer.Available, encounterPlayer.Scene,
                encounterPlayer.EchoRunId, encounterPlayer.GiftIdentity, encounterPlayer.CharacterId);
            if (publication != null && (!ReferenceEquals(publication, peer.LastEncounters) || visibility != peer.LastEncounterVisibility))
            {
                var delivery = CreateEncounterDelivery(publication, encounterPlayer, ++peer.EncounterRevision);
                await WritePacketAsync(peer.Client.GetStream(), new Packet { Kind = "encounters", Encounters = delivery }, deadline.Token).ConfigureAwait(false);
                peer.LastEncounters = publication;
                peer.LastEncounterVisibility = visibility;
            }
            // Echo context travels separately from world chunks so late joins
            // receive it even while their player is still in another map.
            if(!ReferenceEquals(currentEcho,peer.LastEcho) && currentEcho!=null)
            {
                var transfer=currentEcho.Active && peer.LastEcho?.Active==true && currentEcho.RunId==peer.LastEcho.RunId ?
                    currentEcho with {Run=Array.Empty<byte>(),Metadata=Array.Empty<byte>()} : currentEcho;
                await WritePacketAsync(peer.Client.GetStream(),new Packet {Kind="echo",Echo=transfer},deadline.Token).ConfigureAwait(false);
            }
            await WritePacketAsync(peer.Client.GetStream(), new Packet { Kind = "snapshot", Players = states, WorldChunk = chunks.FirstOrDefault(), Hud = ReferenceEquals(currentHud, peer.LastHud) ? null : currentHud, Skills = skills.ToArray(), IncomingDamage = damage.ToArray(), Gifts = gifts.ToArray(),StatusCallbacks=callbacks.ToArray(),ChestReplies=chest.ToArray(),EncounterReplies=encounterAcks.ToArray() }, deadline.Token).ConfigureAwait(false);
            foreach (var chunk in chunks.Skip(1))
            {
                // Bound each stalled write, not the total duration of a valid
                // multi-chunk baseline on a slower Internet connection.
                deadline.CancelAfter(TimeSpan.FromSeconds(5));
                await WritePacketAsync(peer.Client.GetStream(), new Packet { Kind = "world-chunk", WorldChunk = chunk }, deadline.Token).ConfigureAwait(false);
            }
            peer.LastHud = currentHud;
            peer.LastEcho=currentEcho;
            // Commit only after the send succeeds. New/reconnected peers have
            // no baseline and always receive the entire authoritative world.
            peer.LastWorld = currentWorld;
            if (changed) Interlocked.Increment(ref worldFramesSent); else Interlocked.Increment(ref worldFramesOmitted);
        }
        catch (Exception ex) when (IsConnectionError(ex)) { peer.Client.Dispose(); }
    }

    private async Task ClientReadAsync()
    {
        long lastDamageSequence = 0;
        bool hasWorldBaseline = false;
        var worldReader = new WorldChunkReader();
        void AcceptWorldChunk(WorldChunk chunk, int frameBytes)
        {
            var completed = worldReader.Accept(chunk, frameBytes);
            if (completed == null) return;
            if (Volatile.Read(ref world).Epoch != completed.Epoch) claimedRewards.Clear();
            Volatile.Write(ref worldArrivalSeconds, MonotonicSeconds);
            Volatile.Write(ref world, completed); hasWorldBaseline = true;
        }
        try
        {
            while (!stop.IsCancellationRequested)
            {
                Packet packet = await ReadPacketAsync(upstream!.GetStream(), stop.Token).ConfigureAwait(false);
                if (packet.Protocol == LanProtocol.Version && packet.Kind == "loot-filters")
                {
                    if (worldReader.Pending || !LootFilterOnly(packet) || packet.LootFilters is not { Length: <= 4 } ||
                        packet.LootFilters.Any(f => f == null || !f.IsValid()) ||
                        packet.LootFilters.Select(f => f.OwnerId).Distinct().Count() != packet.LootFilters.Length)
                        throw new InvalidDataException("Invalid Host loot filter roster.");
                    Volatile.Write(ref receivedLootFilters, packet.LootFilters.ToArray());
                    continue;
                }
                if (packet.LootFilters != null) throw new InvalidDataException("Filter requires its own frame.");
                if (packet.Protocol == LanProtocol.Version && packet.Kind == "encounters")
                {
                    if (worldReader.Pending || !IsEncounterOnlyFrame(packet) || packet.Encounters == null ||
                        !packet.Encounters.IsValid() || packet.Encounters.Revision <= (Volatile.Read(ref encounterDelivery)?.Revision ?? 0))
                        throw new InvalidDataException("Invalid Host encounter frame.");
                    Volatile.Write(ref encounterDelivery, packet.Encounters.Copy());
                    continue;
                }
                if (packet.Encounters != null || packet.EncounterCommands != null ||
                    packet.Kind != "snapshot" && packet.EncounterReplies != null)
                    throw new InvalidDataException("Invalid encounter frame direction.");
                if(packet.Protocol==LanProtocol.Version && packet.Kind=="echo")
                {
                    if(worldReader.Pending || packet.Echo==null || !packet.Echo.IsValid(requireContext:false) ||
                        packet.Echo.Revision<=(Volatile.Read(ref echo)?.Revision??0) ||
                        packet.World!=null || packet.WorldChunk!=null || packet.Players!=null || packet.State!=null ||
                        packet.Skills!=null || packet.Gifts!=null || packet.Hud!=null || packet.IncomingDamage!=null ||
                        packet.Hits!=null || packet.StatusVisuals!=null || packet.DeathReceipts!=null || packet.StatusCallbacks!=null)
                        throw new InvalidDataException("Invalid Host Echo state.");
                    var nextEcho=packet.Echo;
                    if(nextEcho.Active && nextEcho.Run.Length==0)
                    {
                        var baseline=Volatile.Read(ref echo);
                        if(baseline?.Active!=true || baseline.RunId!=nextEcho.RunId || baseline.Scene!=nextEcho.Scene)
                            throw new InvalidDataException("Missing Echo context baseline.");
                        nextEcho=nextEcho with {Run=baseline.Run,Metadata=baseline.Metadata};
                    }
                    if(!nextEcho.IsValid()) throw new InvalidDataException("Invalid assembled Echo context.");
                    Volatile.Write(ref echo,nextEcho.Copy());continue;
                }
                if(packet.Echo!=null) throw new InvalidDataException("Echo context requires its own frame.");
                if (packet.Protocol == LanProtocol.Version && packet.Kind == "world-chunk")
                {
                    if (packet.WorldChunk == null || !worldReader.Pending || packet.World != null || packet.Players != null ||
                        packet.Skills != null || packet.Gifts != null || packet.Hud != null || packet.IncomingDamage != null || packet.Hits != null || packet.StatusVisuals != null || packet.DeathReceipts != null || packet.StatusCallbacks!=null)
                        throw new InvalidDataException("Invalid chunk continuation.");
                    AcceptWorldChunk(packet.WorldChunk,packet.ReceivedBytes); continue;
                }
                if (packet.Protocol != LanProtocol.Version || packet.Kind != "snapshot" || packet.Players == null || packet.Players.Length is < 1 or > 4 ||
                    packet.Players.Any(s => s == null || !s.IsValid() || !Guid.TryParse(s.Id, out _) || s.Name is not { Length: <= 32 }) ||
                    packet.Players.Select(s => s.Id).Distinct().Count() != packet.Players.Length)
                    throw new InvalidDataException("Invalid snapshot.");
                Volatile.Write(ref snapshot, packet.Players);
                ReceiveEncounterReplies(packet);
                if (packet.Hud != null)
                {
                    if (!packet.Hud.IsValid() || packet.Hud.Revision <= (Volatile.Read(ref hud)?.Revision ?? 0)) throw new InvalidDataException("Invalid or stale host HUD configuration.");
                    Volatile.Write(ref hud, packet.Hud);
                }
                if (packet.Gifts is { Length: > 8 }) throw new InvalidDataException("Too many gift events.");
                if(packet.ChestCommands!=null || packet.ChestReplies is {Length:>2})throw new InvalidDataException("Invalid chest response budget");
                foreach(var reply in packet.ChestReplies??Array.Empty<ChestReply>())
                {
                    if(reply==null || !reply.Valid() || chestReplies.Count>=128)throw new InvalidDataException("Invalid chest response");
                    chestReplies.Enqueue(reply);
                }
                foreach (var gift in packet.Gifts ?? Array.Empty<GiftMessage>())
                {
                    if (gift == null || !gift.IsValid() || receivedGifts.Count >= 256) throw new InvalidDataException("Invalid gift event.");
                    receivedGifts.Enqueue(gift);
                }
                if (packet.StatusVisuals != null) throw new InvalidDataException("Host status reports must be in authoritative world state.");
                if(packet.StatusCallbacks is {Length:>64}) throw new InvalidDataException("Too many status callbacks.");
                foreach(var callback in packet.StatusCallbacks ?? Array.Empty<StatusCallback>())
                {
                    if(callback==null || !callback.IsValid() || statusCallbacks.Count>=2048) throw new InvalidDataException("Invalid status callback.");
                    statusCallbacks.Enqueue(callback);
                }
                if (packet.World != null || worldReader.Pending) throw new InvalidDataException("Invalid world transport.");
                if (packet.WorldChunk != null) AcceptWorldChunk(packet.WorldChunk,packet.ReceivedBytes);
                else if (!hasWorldBaseline) throw new InvalidDataException("Missing initial world baseline.");
                if (packet.IncomingDamage is { Length: > 32 }) throw new InvalidDataException("Too many host attacks.");
                foreach (var damage in packet.IncomingDamage ?? Array.Empty<IncomingDamage>())
                {
                    if (damage == null || !damage.IsValid()) throw new InvalidDataException("Invalid host attack.");
                    if (damage.Sequence <= lastDamageSequence) continue;
                    lastDamageSequence = damage.Sequence;
                    if (receivedDamage.Count < 128) receivedDamage.Enqueue(damage);
                }
                if (packet.Skills is { Length: > 16 }) throw new InvalidDataException("Too many skill events.");
                foreach (var skill in packet.Skills ?? Array.Empty<SkillEvent>())
                {
                    if (skill == null || !skill.IsValid() || !Guid.TryParse(skill.PlayerId, out _)) throw new InvalidDataException("Invalid skill event.");
                    if (skill.PlayerId != LocalId && receivedSkills.Count < 128) receivedSkills.Enqueue(skill);
                }
            }
        }
        catch (Exception ex) when (IsConnectionError(ex))
        {
            if (!stop.IsCancellationRequested) { status = "Disconnected: " + ex.Message; Dispose(); }
        }
    }

    private async Task ClientTickAsync()
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
                deadline.CancelAfter(TimeSpan.FromSeconds(1));
                var hits = new List<HitRequest>();
                int hitBytes=0;
                while(hits.Count<32 && outgoingHits.TryPeek(out var pendingHit))
                {
                    int size=EstimateHitBytes(pendingHit);
                    if(hits.Count>0 && hitBytes+size>512*1024) break;
                    if(!outgoingHits.TryDequeue(out var hit)) continue;
                    hits.Add(hit);hitBytes+=size;
                }
                var statuses=new List<StatusVisualReport>();
                int statusBytes=0;
                foreach(var pending in outgoingStatusVisuals)
                {
                    if(statuses.Count>=32) break;
                    // Conservative JSON size bound; do not let visual traffic
                    // fill a TCP frame or evict queued damage/control commands.
                    int estimate=512+pending.Value.Groups.Length*128;
                    if(statusBytes+estimate>256*1024) continue;
                    if(outgoingStatusVisuals.TryRemove(pending.Key,out var report))
                    {statuses.Add(report);statusBytes+=estimate;}
                }
                var skills = new List<SkillEvent>();
                while (skills.Count < 16 && outgoingSkills.TryDequeue(out var skill)) skills.Add(skill);
                var gifts = new List<GiftMessage>();
                var chest=new List<ChestCommand>();
                while(chest.Count<8 && outgoingChest.TryDequeue(out var chestCommand))chest.Add(chestCommand);
                while (gifts.Count < 8 && outgoingGifts.TryDequeue(out var gift)) gifts.Add(gift);
                var receipts = new List<DeathReceipt>();
                while (receipts.Count < 32 && outgoingDeathReceipts.TryDequeue(out var receipt)) receipts.Add(receipt);
                var encounters = new List<EncounterCommand>();
                while (encounters.Count < 8 && outgoingEncounters.TryDequeue(out var command)) encounters.Add(command);
                var filter = Volatile.Read(ref localLootFilter);
                if (filter != null && !ReferenceEquals(filter, sentLootFilter))
                {
                    await WritePacketAsync(upstream!.GetStream(), new Packet { Kind = "loot-filters", LootFilters = new[] { filter } }, deadline.Token).ConfigureAwait(false);
                    sentLootFilter = filter;
                }
                await WritePacketAsync(upstream!.GetStream(), new Packet { Kind = "state", State = Volatile.Read(ref local), DeathReceipts = receipts.ToArray(), Hits = hits.ToArray(), StatusVisuals=statuses.ToArray(), Skills = skills.ToArray(), Gifts = gifts.ToArray(),ChestCommands=chest.ToArray(),EncounterCommands=encounters.ToArray() }, deadline.Token).ConfigureAwait(false);
                await Task.Delay(50, stop.Token).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (IsConnectionError(ex))
        {
            if (!stop.IsCancellationRequested) { status = "Disconnected: " + ex.Message; Dispose(); }
        }
    }

    private static bool CodesMatch(string? supplied, string expected)
    {
        if (supplied == null || supplied.Length > 64) return false;
        return CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(supplied)), SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
    }

    private static bool IsConnectionError(Exception ex) => ex is IOException or SocketException or OperationCanceledException or
        ObjectDisposedException or System.Text.Json.JsonException or InvalidOperationException;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        stop.Cancel();
        listener?.Stop();
        foreach (TcpClient client in sockets.Keys) client.Dispose();
        peers.Clear();
        Volatile.Write(ref snapshot, Array.Empty<PlayerState>());
        Volatile.Write(ref world, new WorldState());
        Volatile.Write(ref echo,null);
        ResetEncounterTransport();
        while (incomingCombat.TryDequeue(out _)) { }
        while (outgoingHits.TryDequeue(out _)) { }
        while(statusCallbacks.TryDequeue(out _)) { }
        outgoingStatusVisuals.Clear();
        lock(statusVisualGate) {incomingStatusVisuals.Clear();incomingStatusKeys.Clear();}
        while (outgoingSkills.TryDequeue(out _)) { }
        while (receivedSkills.TryDequeue(out _)) { }
        while (receivedGifts.TryDequeue(out _)) { }
        while (outgoingGifts.TryDequeue(out _)) { }
        while(outgoingChest.TryDequeue(out _)){}
        while(receivedChest.TryDequeue(out _)){}
        while(chestReplies.TryDequeue(out _)){}
        while (outgoingDeathReceipts.TryDequeue(out _)) { }
        claimedRewards.Clear();
        if (status is "Connected" or "Hosting" or "Starting") status = "Stopped";
        // Tasks hold this token source until they observe cancellation. Do not dispose it early.
    }

    private sealed class Peer
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public string Name { get; }
        public TcpClient Client { get; }
        public PlayerState State;
        public readonly ConcurrentQueue<SkillEvent> Skills = new();
        public readonly ConcurrentQueue<IncomingDamage> Damage = new();
        public readonly ConcurrentQueue<GiftMessage> Gifts = new();
        public readonly ConcurrentQueue<ChestReply> ChestReplies=new();
        public RoomLootFilter? LootFilter;
        public RoomLootFilter[]? LastLootFilters;
        public readonly ConcurrentQueue<EncounterReply> EncounterReplies = new();
        public EncounterPublication? LastEncounters;
        public long EncounterRevision;
        public (bool Cooperative, bool Available, string Scene, string Run, string Identity, string Character) LastEncounterVisibility;
        public readonly ConcurrentQueue<StatusCallback> StatusCallbacks = new();
        public WorldState? LastWorld;
        public readonly WorldChunkWriter WorldWriter = new();
        public HudConfiguration? LastHud;
        public PartyEchoState? LastEcho;
        public Peer(TcpClient client, string name)
        {
            Client = client;
            Name = name;
            State = new PlayerState { Id = Id, Name = name };
        }
    }
}
