namespace LastEpochLAN.Core.Encounters;

public enum EncounterNativeDispatch { RejectedBeforeExecution, Started, Uncertain }
public sealed record EncounterExecution(EncounterInstance Instance, EncounterView View, EncounterReservation Reservation);

// Implemented by an instance-owned native family adapter on the Unity thread.
// Started only confirms dispatch. Completion/reward callbacks are separate.
// Queue native completion observations until Execute has returned and the
// driver has confirmed NativeAccepted; do not complete synchronously inside Execute.
public interface IEncounterFamilyAdapter
{
    EncounterKind Kind { get; }
    EncounterNativeDispatch Execute(EncounterExecution execution);
}

/// <summary>
/// A single-threaded Host pump joining authenticated transport to typed adapters.
/// Constructing it installs no hooks, discovers no objects and grants no rewards.
/// </summary>
public sealed class EncounterHostDriver
{
    private readonly LanSession room;
    private readonly EncounterHost authority;
    private readonly Dictionary<EncounterKind, IEncounterFamilyAdapter> adapters = new();
    private EncounterInstance? instance;
    private long publishedRevision = -1;
    private long endedEpoch = -1;
    public string LastDispatchError { get; private set; } = "";

    public EncounterHostDriver(LanSession room)
    {
        this.room = room;
        authority = new(room);
    }

    public void Attach(IEncounterFamilyAdapter adapter)
    {
        if (adapter.Kind is not (EncounterKind.Nemesis or EncounterKind.RiftBeast or EncounterKind.OmenWindow or EncounterKind.BloodCrystal))
            throw new ArgumentException("Family is outside the requested co-op scope.");
        if (!adapters.TryAdd(adapter.Kind, adapter)) throw new InvalidOperationException("Family adapter already attached.");
    }

    public void Begin(EncounterInstance next)
    {
        if (next.Epoch <= endedEpoch) throw new InvalidOperationException("An ended instance cannot be reopened.");
        authority.Begin(next);
        if (instance == next) return;
        instance = next;
        publishedRevision = -1;
        Publish();
    }

    public Guid Register(EncounterKind kind, VisualPoint position)
    {
        if (instance == null) throw new InvalidOperationException("No active encounter instance.");
        if (!adapters.ContainsKey(kind)) throw new InvalidOperationException("No owned native adapter for this encounter family.");
        return authority.Register(kind, position);
    }

    private bool Active => room.Connected && instance != null;
    public bool Completed(Guid encounter) => Active && authority.NativeCompleted(encounter) != Guid.Empty;
    public Guid NemesisTurnCompleted(EncounterReservation execution) => Active ? authority.NativeNemesisTurnCompleted(execution) : Guid.Empty;
    public bool Failed(Guid encounter) => Active && authority.NativeFailed(encounter);
    public bool Removed(Guid encounter) => Active && authority.NativeRemoved(encounter);

    public void EndInstance()
    {
        if (instance != null) endedEpoch = Math.Max(endedEpoch, instance.Epoch);
        if (Active)
        {
            foreach (var entry in authority.Snapshot().Entries) authority.NativeRemoved(entry.Id);
            room.ClearEncounters();
        }
        instance = null;
        publishedRevision = -1;
    }

    public void Pump()
    {
        if (!room.Connected || !room.IsHost) return;
        for (int i = 0; i < 8 && room.TryTakeEncounterRequest(out var request); i++)
        {
            if (request == null) continue;
            var command = request.Command;
            if (instance == null || !authority.TryReserve(request.PlayerId, command, out var reservation))
            {
                Reply(request, EncounterReplyCode.Rejected);
                continue;
            }
            var view = authority.Snapshot().Entries.Single(e => e.Id == reservation!.Encounter);
            if (!adapters.TryGetValue(view.Kind, out var adapter) || !authority.CanExecute(reservation!))
            {
                authority.NativeRejectedBeforeExecution(reservation!);
                Reply(request, EncounterReplyCode.NativeRejected);
                continue;
            }
            // Do not start a native mutation when the requester cannot even be
            // told its operation was reserved. A later snapshot also carries state.
            if (!Reply(request, EncounterReplyCode.Reserved))
            {
                authority.NativeRejectedBeforeExecution(reservation!);
                continue;
            }
            EncounterNativeDispatch dispatched;
            try { dispatched = adapter.Execute(new(instance, view, reservation!)); }
            catch (Exception ex)
            {
                // An exception cannot prove that native execution did nothing.
                // Keep the encounter faulted; never retry or grant a reward here.
                LastDispatchError = ex.ToString();
                dispatched = EncounterNativeDispatch.Uncertain;
            }
            if (dispatched == EncounterNativeDispatch.RejectedBeforeExecution)
            {
                authority.NativeRejectedBeforeExecution(reservation!);
                Reply(request, EncounterReplyCode.NativeRejected);
            }
            else if (dispatched == EncounterNativeDispatch.Started && authority.NativeAccepted(reservation!))
                Reply(request, EncounterReplyCode.NativeAccepted);
            else
            {
                authority.NativeFailed(view.Id);
                Reply(request, EncounterReplyCode.Faulted);
            }
        }
        Publish();
    }

    private bool Reply(EncounterRequest request, EncounterReplyCode code)
    {
        long current = instance == null ? 0 : authority.Snapshot().Revision;
        var c = request.Command;
        return room.ReplyEncounter(request.PlayerId, new(c.Instance, c.Epoch, c.Encounter, c.Sequence, code, current));
    }

    private void Publish()
    {
        if (instance == null) return;
        var snapshot = authority.Snapshot();
        if (snapshot.Revision == publishedRevision) return;
        room.PublishEncounters(snapshot, authority.PrivateNemesisTurns());
        publishedRevision = snapshot.Revision;
    }
}
