using System.Collections.Concurrent;
using LastEpochLAN.Core.Encounters;

namespace LastEpochLAN.Core;

public sealed partial class LanSession
{
    private const int EncounterQueueLimit = 128;
    private readonly object encounterQueueGate = new();
    private readonly ConcurrentQueue<EncounterCommand> outgoingEncounters = new();
    private readonly ConcurrentQueue<EncounterRequest> incomingEncounters = new();
    private readonly ConcurrentQueue<EncounterReply> encounterReplies = new();
    private EncounterPublication? encounterPublication;
    private EncounterDelivery? encounterDelivery;
    private EncounterSnapshot? encounterWatermark;

    private sealed record EncounterPublication(EncounterSnapshot? Snapshot, NemesisTurn[] Turns);
    public EncounterDelivery? Encounters => Volatile.Read(ref encounterDelivery)?.Copy();

    public bool SendEncounter(EncounterCommand command)
    {
        if (!Connected || command == null || !command.IsValid()) return false;
        lock (encounterQueueGate)
        {
            if (IsHost)
            {
                if (incomingEncounters.Count >= EncounterQueueLimit) return false;
                incomingEncounters.Enqueue(new(LocalId, command));
            }
            else
            {
                if (outgoingEncounters.Count >= EncounterQueueLimit) return false;
                outgoingEncounters.Enqueue(command);
            }
        }
        return true;
    }

    public bool TryTakeEncounterRequest(out EncounterRequest? request) => incomingEncounters.TryDequeue(out request);
    public bool TryTakeEncounterReply(out EncounterReply? reply) => encounterReplies.TryDequeue(out reply);

    public bool ReplyEncounter(string playerId, EncounterReply reply)
    {
        if (!IsHost || !Connected || reply == null || !reply.IsValid()) return false;
        lock (encounterQueueGate)
        {
            var target = playerId == LocalId ? encounterReplies :
                peers.TryGetValue(playerId, out var peer) ? peer.EncounterReplies : null;
            if (target == null || target.Count >= EncounterQueueLimit) return false;
            target.Enqueue(reply);
            return true;
        }
    }

    public void PublishEncounters(EncounterSnapshot value, IEnumerable<NemesisTurn> turns)
    {
        if (!IsHost || !Connected) throw new InvalidOperationException("Only a live Host can publish encounters.");
        var privateTurns = turns.ToArray();
        if (!value.IsValid() || privateTurns.Length > EncounterHost.MaximumEntities * 4 ||
            privateTurns.Any(t => t == null || !t.IsValid() || t.Revision > value.Revision ||
                !value.Entries.Any(e => e.Id == t.Encounter && e.Kind == EncounterKind.Nemesis)) ||
            privateTurns.Select(t => (t.Encounter, t.CharacterKey)).Distinct().Count() != privateTurns.Length ||
            privateTurns.GroupBy(t => t.Encounter).Any(g => g.Count() > 4))
            throw new ArgumentException("Invalid encounter publication.");
        var previous = encounterWatermark;
        if (previous != null && (value.Instance.Epoch < previous.Instance.Epoch ||
            value.Instance.Epoch == previous.Instance.Epoch &&
                (value.Instance != previous.Instance || value.Revision <= previous.Revision)))
            throw new ArgumentException("Stale encounter publication.");
        var immutable = value.Copy();
        encounterWatermark = immutable;
        Volatile.Write(ref encounterPublication, new(immutable, privateTurns));
    }

    public void ClearEncounters()
    {
        if (!IsHost || !Connected) throw new InvalidOperationException("Only a live Host can clear encounters.");
        // A non-null publication with an empty context sends a clear to each connected peer.
        Volatile.Write(ref encounterPublication, new(null, Array.Empty<NemesisTurn>()));
    }

    private EncounterDelivery CreateEncounterDelivery(EncounterPublication publication, PlayerState player, long revision)
    {
        var source = publication.Snapshot;
        if (source == null || !player.Cooperative || !player.Available || !source.Instance.Contains(player))
            return new(revision, null, Array.Empty<NemesisTurn>());
        string key = EncounterHost.CharacterKey(player);
        var visible = source with
        {
            Entries = source.Entries.Select(e => e with
            {
                Actions = e.Actions.ToArray(),
                Recipients = e.Recipients.Where(r => r == key).ToArray()
            }).ToArray()
        };
        return new(revision, visible, publication.Turns.Where(t => t.CharacterKey == key).ToArray());
    }

    private void ReceiveEncounterCommands(Peer peer, Packet packet)
    {
        if (packet.Encounters != null || packet.EncounterReplies != null || packet.EncounterCommands is { Length: > 8 })
            throw new InvalidDataException("Invalid encounter authority or command budget.");
        var commands = packet.EncounterCommands ?? Array.Empty<EncounterCommand>();
        if (commands.Any(command => command == null || !command.IsValid()))
            throw new InvalidDataException("Invalid encounter command.");
        lock (encounterQueueGate)
        {
            if (incomingEncounters.Count + commands.Length > EncounterQueueLimit)
                throw new InvalidDataException("Encounter queue is full.");
            foreach (var command in commands)
                incomingEncounters.Enqueue(new(peer.Id, command));
        }
    }

    private void ReceiveEncounterReplies(Packet packet)
    {
        if (packet.EncounterCommands != null || packet.EncounterReplies is { Length: > 8 })
            throw new InvalidDataException("Invalid Host encounter replies.");
        foreach (var reply in packet.EncounterReplies ?? Array.Empty<EncounterReply>())
        {
            if (reply == null || !reply.IsValid() || encounterReplies.Count >= EncounterQueueLimit)
                throw new InvalidDataException("Invalid encounter reply or queue overflow.");
            encounterReplies.Enqueue(reply);
        }
    }

    private static bool IsEncounterOnlyFrame(Packet p) => p.State == null && p.Players == null &&
        p.World == null && p.WorldChunk == null && p.Echo == null && p.Hud == null &&
        p.Hits == null && p.DeathReceipts == null && p.StatusVisuals == null && p.StatusCallbacks == null &&
        p.Skills == null && p.IncomingDamage == null && p.Gifts == null && p.ChestCommands == null &&
        p.ChestReplies == null && p.EncounterCommands == null && p.EncounterReplies == null;

    private void ResetEncounterTransport()
    {
        Volatile.Write(ref encounterPublication, null);
        Volatile.Write(ref encounterDelivery, null);
        encounterWatermark = null;
        while (outgoingEncounters.TryDequeue(out _)) { }
        while (incomingEncounters.TryDequeue(out _)) { }
        while (encounterReplies.TryDequeue(out _)) { }
    }
}
