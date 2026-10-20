namespace LastEpochLAN.Core.Encounters;

// Pure presentation state. It cannot call gameplay controllers or grant loot.
// A Unity renderer can consume this after the room authenticates Host delivery.
public sealed class EncounterClientView
{
    private EncounterDelivery? current;
    private long receivedRevision;
    private long sequence;
    private string character="";
    private EncounterSnapshot? watermark;
    public EncounterSnapshot? Snapshot=>current?.Snapshot?.Copy();
    public NemesisTurn[] Turns=>current?.Turns.ToArray()??Array.Empty<NemesisTurn>();
    public bool Apply(EncounterDelivery delivery,PlayerState local)
    {
        if(delivery==null || !delivery.IsValid() || delivery.Revision<=receivedRevision)return false;
        if(delivery.Snapshot is { } incoming && watermark is { } previous &&
            (incoming.Instance.Epoch<previous.Instance.Epoch || incoming.Instance.Epoch==previous.Instance.Epoch &&
            (incoming.Instance!=previous.Instance || incoming.Revision<previous.Revision)))return false;
        string key=EncounterHost.CharacterKey(local);
        if(key=="" || !local.Cooperative || !local.Available || delivery.Snapshot is { } view &&
            (!view.Instance.Contains(local) || delivery.Turns.Any(t=>t.CharacterKey!=key) || view.Entries.Any(e=>e.Recipients.Any(r=>r!=key))))return false;
        receivedRevision=delivery.Revision;character=key;current=delivery.Copy();
        if(delivery.Snapshot!=null)watermark=delivery.Snapshot.Copy();return true;
    }
    public EncounterCommand? Choose(PlayerState local,Guid encounter,EncounterAction action)
    {
        var snapshot=current?.Snapshot;
        if(snapshot==null || EncounterHost.CharacterKey(local)!=character || !local.Cooperative || !local.Available || local.Dead || !snapshot.Instance.Contains(local))return null;
        var entry=snapshot.Entries.SingleOrDefault(e=>e.Id==encounter);
        if(entry==null || !entry.Actions.Contains(action) || entry.Phase is not (EncounterPhase.Available or EncounterPhase.Active))return null;
        if(Math.Pow(local.X-entry.Position.X,2)+Math.Pow(local.Y-entry.Position.Y,2)+Math.Pow(local.Z-entry.Position.Z,2)>64)return null;
        var turn=current!.Turns.SingleOrDefault(t=>t.Encounter==encounter);
        if(entry.Kind==EncounterKind.Nemesis && turn is {Phase:not NemesisTurnPhase.Available})return null;
        if(entry.Kind!=EncounterKind.Nemesis && entry.Phase!=EncounterPhase.Available)return null;
        return new(snapshot.Instance.Id,snapshot.Instance.Epoch,encounter,entry.Revision,++sequence,action){TurnRevision=turn?.Revision??0};
    }
    public void ClearPresentation(){current=null;character="";}
    public EncounterCommand? Acknowledge(EncounterRewardRecord record,PlayerState local)
    {
        if(!record.IsValid() || record.Stage!=EncounterRewardStage.Saved || record.CharacterKey!=EncounterHost.CharacterKey(local))return null;
        return new(record.Instance,record.Epoch,record.Encounter,1,++sequence,EncounterAction.AcknowledgeReward){RewardCompletion=record.Completion};
    }
    public void Disconnected(){ClearPresentation();watermark=null;receivedRevision=sequence=0;}
}
