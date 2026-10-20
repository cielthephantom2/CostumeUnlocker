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
    void Accepted(EncounterExecution execution){}
    void EndInstance(EncounterInstance instance){}
}

/// <summary>
/// A single-threaded Host pump joining authenticated transport to typed adapters.
/// Constructing it installs no hooks, discovers no objects and grants no rewards.
/// </summary>
public sealed class EncounterHostDriver
{
    private readonly LanSession room;
    private readonly EncounterHost authority;
    private readonly EncounterRewardAuthority? rewards;
    private readonly Dictionary<EncounterKind, IEncounterFamilyAdapter> adapters = new();
    private readonly Queue<Observation> observations = new();
    private readonly Dictionary<Guid,EncounterReservation> executions = new();
    private sealed record Observation(EncounterInstance Instance,Guid Encounter,EncounterReservation? Execution,bool Success,bool Removed);
    private EncounterInstance? instance;
    private long publishedRevision = -1;
    private long endedEpoch = -1;
    public string LastDispatchError { get; private set; } = "";
    public EncounterInstance? Instance => instance;
    public EncounterSnapshot? Snapshot => Active ? authority.Snapshot() : null;
    public int PendingObservations => observations.Count;

    public EncounterHostDriver(LanSession room,EncounterRewardAuthority? rewards=null)
    {
        this.room = room;
        this.rewards=rewards;
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
        if(next==null || !next.IsValid())throw new ArgumentException("Invalid instance.");
        if (next.Epoch <= endedEpoch) throw new InvalidOperationException("An ended instance cannot be reopened.");
        if(instance!=null && instance!=next)
        {
            if(next.Epoch<=instance.Epoch)throw new InvalidOperationException("Instance epoch must advance.");
            ReleaseAdapters(instance);
        }
        authority.Begin(next);
        if (instance == next) return;
        observations.Clear();executions.Clear();
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
    // Capture the instance when binding native callbacks, never read the current
    // instance from an old object's callback after travel. Synchronous callbacks
    // are deferred until Execute returned and NativeAccepted was confirmed.
    public bool Observe(EncounterInstance source,Guid encounter,bool success,EncounterReservation? execution=null,bool removed=false)
    {
        if(!Active || instance!=source || observations.Count>=1024)return false;
        observations.Enqueue(new(source,encounter,execution,success,removed));return true;
    }

    public bool UpdatePosition(Guid encounter,VisualPoint point)=>Active && authority.NativePosition(encounter,point);
    public bool UpdateArea(Guid encounter,EncounterArea? area)=>Active && authority.NativeArea(encounter,area);
    public bool UpdateWorld(Guid encounter,EncounterActors? actors,EncounterDestination? destination=null)=>Active && authority.NativeWorld(encounter,actors,destination);

    public void EndInstance()
    {
        if(instance!=null)ReleaseAdapters(instance);
        if (instance != null) endedEpoch = Math.Max(endedEpoch, instance.Epoch);
        if (Active)
        {
            foreach (var entry in authority.Snapshot().Entries) authority.NativeRemoved(entry.Id);
            room.ClearEncounters();
        }
        instance = null;
        observations.Clear();executions.Clear();
        publishedRevision = -1;
    }
    private void ReleaseAdapters(EncounterInstance source)
    {
        foreach(var adapter in adapters.Values)
            try{adapter.EndInstance(source);}catch(Exception ex){LastDispatchError=ex.ToString();}
    }

    public void Pump()
    {
        if (!room.Connected || !room.IsHost) return;
        for (int i = 0; i < 8 && room.TryTakeEncounterRequest(out var request); i++)
        {
            if (request == null) continue;
            var command = request.Command;
            if(command.Action==EncounterAction.AcknowledgeReward)
            {
                var peer=room.PlayerPositions.FirstOrDefault(p=>p.Id==request.PlayerId);
                try{Reply(request,rewards!=null && peer!=null && rewards.Acknowledge(peer,command)?EncounterReplyCode.RewardAcknowledged:EncounterReplyCode.Rejected);}
                catch(Exception ex){LastDispatchError=ex.ToString();Reply(request,EncounterReplyCode.Faulted);}
                continue;
            }
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
            {
                executions[reservation!.Id]=reservation;
                try{adapter.Accepted(new(instance,view,reservation));}
                catch(Exception ex)
                {LastDispatchError=ex.ToString();executions.Remove(reservation.Id);authority.NativeFailed(view.Id);Reply(request,EncounterReplyCode.Faulted);continue;}
                Reply(request, EncounterReplyCode.NativeAccepted);
            }
            else
            {
                authority.NativeFailed(view.Id);
                Reply(request, EncounterReplyCode.Faulted);
            }
        }
        for(int i=0;i<128 && observations.TryDequeue(out var observation);i++)
        {
            if(instance!=observation.Instance)continue;
            if(observation.Execution is { } execution &&
                (!executions.TryGetValue(execution.Id,out var accepted) || accepted!=execution || execution.Encounter!=observation.Encounter))continue;
            if(observation.Removed)authority.NativeRemoved(observation.Encounter);
            else if(!observation.Success)authority.NativeFailed(observation.Encounter);
            else if(observation.Execution is { } turn && authority.Snapshot().Entries.Any(e=>e.Id==turn.Encounter && e.Kind==EncounterKind.Nemesis))authority.NativeNemesisTurnCompleted(turn);
            else authority.NativeCompleted(observation.Encounter);
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
        var turns=authority.PrivateNemesisTurns();
        // A delivery cannot advertise a reward before its durable Host record.
        rewards?.Publish(snapshot,turns);
        room.PublishEncounters(snapshot,turns);
        publishedRevision = snapshot.Revision;
    }
}
