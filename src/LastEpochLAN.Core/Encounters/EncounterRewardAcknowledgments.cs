namespace LastEpochLAN.Core.Encounters;

// Resends identifiers only. The local record must already be Saved; a Host
// acknowledgment cannot trigger another native grant. Caller routes encounter
// replies here before presenting unrelated interaction replies to the user.
public sealed class EncounterRewardAcknowledgments
{
    private readonly EncounterRewardJournal journal;
    private readonly EncounterClientView view;
    private readonly Dictionary<long,(Guid Completion,EncounterCommand Command)> pending=new();
    private readonly Dictionary<Guid,double> retryAt=new();
    private string connection="";
    public int Pending=>pending.Count;
    public EncounterRewardAcknowledgments(EncounterRewardJournal journal,EncounterClientView view)
    {this.journal=journal;this.view=view;}
    public void SendSaved(LanSession room,PlayerState local,double now)
    {
        if(!double.IsFinite(now) || room?.Connected!=true)return;
        if(connection!=room.LocalId){connection=room.LocalId;pending.Clear();retryAt.Clear();}
        foreach(var record in journal.Records.Where(r=>r.Stage==EncounterRewardStage.Saved).Take(8))
        {
            if(retryAt.GetValueOrDefault(record.Completion)>now)continue;
            var command=view.Acknowledge(record,local);if(command==null || !room.SendEncounter(command))continue;
            foreach(var previous in pending.Where(p=>p.Value.Completion==record.Completion).Select(p=>p.Key).ToArray())pending.Remove(previous);
            pending[command.Sequence]=(record.Completion,command);retryAt[record.Completion]=now+2;
        }
    }
    public bool Accept(string connectionId,EncounterReply reply)
    {
        if(connectionId!=connection || reply==null || !reply.IsValid() || reply.Code!=EncounterReplyCode.RewardAcknowledged ||
            !pending.TryGetValue(reply.Sequence,out var sent) || reply.Instance!=sent.Command.Instance ||
            reply.Epoch!=sent.Command.Epoch || reply.Encounter!=sent.Command.Encounter)return false;
        bool saved=journal.Transition(sent.Completion,EncounterRewardStage.Acknowledged);
        if(saved){pending.Remove(reply.Sequence);retryAt.Remove(sent.Completion);}return saved;
    }
}
