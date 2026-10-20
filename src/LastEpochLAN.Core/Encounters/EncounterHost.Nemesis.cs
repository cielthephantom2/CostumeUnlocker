namespace LastEpochLAN.Core.Encounters;

public sealed partial class EncounterHost
{
    private readonly Dictionary<(Guid Encounter, string Character), NemesisTurn> nemesisTurns = new();
    private readonly Dictionary<Guid, EncounterReservation> nemesisExecutions = new();

    public static string CharacterKey(PlayerState player) =>
        Guid.TryParseExact(player.GiftIdentity, "N", out _) && player.CharacterId is { Length: > 0 and <= 128 }
            ? player.GiftIdentity + ":" + player.CharacterId : "";

    public NemesisTurn[] PrivateNemesisTurns()
    {
        RequireLive();
        return nemesisTurns.Values.ToArray();
    }

    private bool TryReserveNemesis(PlayerState player, EncounterCommand command, EncounterView entry,
        out EncounterReservation? result)
    {
        result = null;
        string character = CharacterKey(player);
        if (character == "" || entry.Phase is not (EncounterPhase.Available or EncounterPhase.Active) ||
            command.Revision > entry.Revision ||
            room.PlayerPositions.Count(p => CharacterKey(p) == character) != 1 ||
            reservations.Values.Any(r => r.Encounter == entry.Id)) return false;
        var key = (entry.Id, character);
        if (!nemesisTurns.TryGetValue(key, out var turn))
        {
            if (nemesisTurns.Keys.Count(k => k.Encounter == entry.Id) >= 4) return false;
            turn = new(entry.Id, character, 0, NemesisTurnPhase.Available, null, Guid.Empty);
        }
        // Public state can advance because another character made a choice. Only
        // this character's turn revision decides whether their own choice is stale.
        if (turn.Phase != NemesisTurnPhase.Available || turn.Revision != command.TurnRevision) return false;
        result = new(Guid.NewGuid(), entry.Id, player.Id, command.Action) { CharacterKey = character };
        reservations.Add(result.Id, result);
        nemesisTurns[key] = turn with { Phase = NemesisTurnPhase.Reserved, Choice = command.Action, Revision = ++revision };
        entities[entry.Id] = entry with { Revision = revision };
        return true;
    }

    private bool AcceptNemesis(EncounterReservation held, EncounterView entry)
    {
        var key = (held.Encounter, held.CharacterKey);
        if (!nemesisTurns.TryGetValue(key, out var turn) || turn.Phase != NemesisTurnPhase.Reserved || turn.Choice != held.Action)
            return false;
        reservations.Remove(held.Id);
        nemesisExecutions.Add(held.Id, held);
        nemesisTurns[key] = turn with { Phase = NemesisTurnPhase.Active, Revision = ++revision };
        entities[entry.Id] = entry with { Phase = EncounterPhase.Active, Revision = revision };
        return true;
    }

    private bool RejectNemesis(EncounterReservation held)
    {
        var key = (held.Encounter, held.CharacterKey);
        if (!nemesisTurns.TryGetValue(key, out var turn) || turn.Phase != NemesisTurnPhase.Reserved) return false;
        reservations.Remove(held.Id);
        nemesisTurns[key] = turn with { Phase = NemesisTurnPhase.Available, Choice = null, Revision = ++revision };
        entities[held.Encounter] = entities[held.Encounter] with { Revision = revision };
        return true;
    }

    // Called only by a Host family adapter after observing the native outcome for
    // this particular turn. This records an outcome ID, never an item grant.
    // Banish/Empower completion does not itself entitle a character to dropped loot.
    public Guid NativeNemesisTurnCompleted(EncounterReservation execution)
    {
        RequireLive();
        if (!nemesisExecutions.TryGetValue(execution.Id, out var held) || held != execution) return Guid.Empty;
        var key = (held.Encounter, held.CharacterKey);
        if (!entities.TryGetValue(held.Encounter, out var entry) || entry.Kind != EncounterKind.Nemesis ||
            !nemesisTurns.TryGetValue(key, out var turn)) return Guid.Empty;
        if (turn.Phase == NemesisTurnPhase.Completed) return turn.Completion;
        if (turn.Phase != NemesisTurnPhase.Active || entry.Phase is EncounterPhase.Removed or EncounterPhase.Faulted)
            return Guid.Empty;
        var completion = Guid.NewGuid();
        nemesisTurns[key] = turn with { Phase = NemesisTurnPhase.Completed, Completion = completion, Revision = ++revision };
        entities[held.Encounter] = entry with { Revision = revision };
        return completion;
    }

    private void FaultNemesisTurns(Guid encounter)
    {
        foreach (var pair in nemesisTurns.Where(p => p.Key.Encounter == encounter &&
            p.Value.Phase is NemesisTurnPhase.Reserved or NemesisTurnPhase.Active).ToArray())
            nemesisTurns[pair.Key] = pair.Value with { Phase = NemesisTurnPhase.Faulted, Revision = ++revision };
    }
}
