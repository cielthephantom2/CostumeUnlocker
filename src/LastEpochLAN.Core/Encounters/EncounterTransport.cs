namespace LastEpochLAN.Core.Encounters;

public enum EncounterReplyCode { Reserved, Rejected, Busy, NativeAccepted, NativeRejected, Faulted,RewardAcknowledged }

// The sender is assigned by the receiving connection, never deserialized from a command.
public sealed record EncounterRequest(string PlayerId, EncounterCommand Command);
public sealed record EncounterReply(Guid Instance, long Epoch, Guid Encounter, long Sequence,
    EncounterReplyCode Code, long Revision)
{
    public bool IsValid() => Instance != Guid.Empty && Epoch >= 0 && Encounter != Guid.Empty &&
        Sequence > 0 && Revision >= 0 && Enum.IsDefined(typeof(EncounterReplyCode), Code);
}

public enum NemesisTurnPhase { Available, Reserved, Active, Completed, Faulted }

// No inventory, egg or Host reward payload belongs in this public transport.
// Native item custody will be handled by the family adapter and its durable journal.
public sealed record NemesisTurn(Guid Encounter, string CharacterKey, long Revision,
    NemesisTurnPhase Phase, EncounterAction? Choice, Guid Completion)
{
    public bool IsValid() => Encounter != Guid.Empty && CharacterKey is { Length: > 0 and <= 161 } &&
        Revision >= 0 && Enum.IsDefined(typeof(NemesisTurnPhase), Phase) &&
        (Choice == null || Choice is EncounterAction.Challenge or EncounterAction.Empower or EncounterAction.Banish) &&
        (Phase == NemesisTurnPhase.Available ? Choice == null : Choice != null) &&
        (Phase == NemesisTurnPhase.Completed ? Completion != Guid.Empty : Completion == Guid.Empty);
}

// Revision is connection-wide, including clears and participation changes. Snapshot revisions
// are scoped to an instance and may restart at zero after travel.
public sealed record EncounterDelivery(long Revision, EncounterSnapshot? Snapshot, NemesisTurn[] Turns)
{
    public bool IsValid() => Revision > 0 && Turns is { Length: <= EncounterHost.MaximumEntities } &&
        (Snapshot == null ? Turns.Length == 0 : Snapshot.IsValid() &&
            Turns.All(t => t != null && t.IsValid() && t.Revision <= Snapshot.Revision && Snapshot.Entries.Any(e => e.Id == t.Encounter && e.Kind == EncounterKind.Nemesis)) &&
            Turns.Select(t => t.Encounter).Distinct().Count() == Turns.Length &&
            Turns.Select(t => t.CharacterKey).Distinct().Count() <= 1);
    public EncounterDelivery Copy() => this with { Snapshot = Snapshot?.Copy(), Turns = Turns.ToArray() };
}
