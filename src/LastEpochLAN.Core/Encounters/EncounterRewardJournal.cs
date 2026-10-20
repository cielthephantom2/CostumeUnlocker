using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LastEpochLAN.Core.Encounters;

public enum EncounterRewardStage { Offered, Applying, Saved, Acknowledged, Uncertain }
public sealed record EncounterRewardRecord(Guid Completion,Guid Instance,long Epoch,Guid Encounter,
    EncounterKind Kind,string CharacterKey,EncounterRewardStage Stage)
{
    public bool IsValid()=>Completion!=Guid.Empty && Instance!=Guid.Empty && Epoch>=0 && Encounter!=Guid.Empty &&
        Kind is EncounterKind.Nemesis or EncounterKind.RiftBeast or EncounterKind.OmenWindow or EncounterKind.BloodCrystal &&
        CharacterKey is {Length:>0 and <=161} && Enum.IsDefined(typeof(EncounterRewardStage),Stage);
}

// Character-scoped local receipts. It stores no Host items/eggs or inventory.
// Applying survives a crash as Uncertain: native effects cannot be rolled back
// by replaying the reward. Acknowledgment follows a confirmed character save.
public sealed class EncounterRewardJournal:IDisposable
{
    private readonly string directory,characterKey;
    private readonly Dictionary<Guid,EncounterRewardRecord> records=new();
    private readonly FileStream lease;
    private bool disposed;
    public EncounterRewardRecord[] Records {get {RequireOpen();return records.Values.ToArray();}}
    public EncounterRewardJournal(string root,string characterKey)
    {
        var split=characterKey.IndexOf(':');
        if(split!=32 || !Guid.TryParseExact(characterKey[..split],"N",out _) || characterKey.Length is <=33 or >161)
            throw new ArgumentException("A stable encounter character identity is required.");
        this.characterKey=characterKey;
        string name=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(characterKey)));
        directory=Path.Combine(root,"encounter-receipts",name);Directory.CreateDirectory(directory);
        lease=new FileStream(Path.Combine(directory,"writer.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        try
        {
        foreach(var path in Directory.EnumerateFiles(directory,"*.json"))
        {
            if(new FileInfo(path).Length>4096 || records.Count>=8192)throw new InvalidDataException("Encounter receipt budget exceeded.");
            var record=JsonSerializer.Deserialize<EncounterRewardRecord>(File.ReadAllText(path));
            if(record==null || !record.IsValid() || record.CharacterKey!=characterKey || Path.GetFileNameWithoutExtension(path)!=record.Completion.ToString("N") || !records.TryAdd(record.Completion,record))
                throw new InvalidDataException("Invalid encounter receipt; manual reconciliation required.");
        }
        foreach(var record in Records.Where(r=>r.Stage==EncounterRewardStage.Applying))Write(record with {Stage=EncounterRewardStage.Uncertain});
        }
        catch{lease.Dispose();throw;}
    }

    public EncounterRewardRecord? Offer(EncounterDelivery delivery,Guid encounter)
    {
        RequireOpen();
        if(!delivery.IsValid() || delivery.Snapshot is not { } snapshot)return null;
        var view=snapshot.Entries.SingleOrDefault(e=>e.Id==encounter);
        if(view==null)return null;
        Guid completion;
        if(view.Kind==EncounterKind.Nemesis)
        {
            var turn=delivery.Turns.SingleOrDefault(t=>t.Encounter==encounter && t.CharacterKey==characterKey);
            if(turn is not {Phase:NemesisTurnPhase.Completed,Choice:EncounterAction.Challenge})return null;
            completion=turn.Completion;
        }
        else
        {
            if(view.Phase!=EncounterPhase.Completed || !view.Recipients.Contains(characterKey,StringComparer.Ordinal))return null;
            completion=view.Completion;
        }
        var proposed=new EncounterRewardRecord(completion,snapshot.Instance.Id,snapshot.Instance.Epoch,encounter,view.Kind,characterKey,EncounterRewardStage.Offered);
        if(!proposed.IsValid())return null;
        if(records.TryGetValue(completion,out var old))
        {
            if(old with {Stage=EncounterRewardStage.Offered}!=proposed)throw new InvalidDataException("Conflicting encounter completion receipt.");
            return old;
        }
        if(records.Count>=8192)throw new InvalidOperationException("Encounter receipt budget exhausted; preserve existing receipts.");
        Write(proposed);return proposed;
    }

    public bool Transition(Guid completion,EncounterRewardStage next)
    {
        RequireOpen();
        if(!records.TryGetValue(completion,out var old))return false;
        bool allowed=(old.Stage,next) is (EncounterRewardStage.Offered,EncounterRewardStage.Applying) or
            (EncounterRewardStage.Applying,EncounterRewardStage.Saved) or
            (EncounterRewardStage.Applying,EncounterRewardStage.Uncertain) or
            (EncounterRewardStage.Saved,EncounterRewardStage.Acknowledged);
        if(!allowed)return false;
        Write(old with {Stage=next});return true;
    }
    private void Write(EncounterRewardRecord record)
    {
        string target=Path.Combine(directory,record.Completion.ToString("N")+".json"),temporary=target+".new";
        using(var stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))
        {JsonSerializer.Serialize(stream,record);stream.Flush(true);}
        File.Move(temporary,target,true);records[record.Completion]=record;
    }
    private void RequireOpen(){if(disposed)throw new ObjectDisposedException(nameof(EncounterRewardJournal));}
    public void Dispose(){if(disposed)return;disposed=true;lease.Dispose();}
}

public enum EncounterRewardApplyResult { RejectedBeforeMutation, AwaitingSave, Uncertain }
public interface IEncounterPrivateRewardExecutor
{
    EncounterKind Kind {get;}
    EncounterRewardApplyResult Apply(EncounterRewardRecord record);
    bool IsCharacterSaveConfirmed(EncounterRewardRecord record);
}

// Called only with an authenticated Host delivery and a local character journal.
// Native executors must keep their own async work handle for save confirmation.
public sealed class EncounterRewardPump
{
    private readonly EncounterRewardJournal journal;
    private readonly Dictionary<EncounterKind,IEncounterPrivateRewardExecutor> executors;
    public string LastError {get;private set;}="";
    public EncounterRewardPump(EncounterRewardJournal journal,IEnumerable<IEncounterPrivateRewardExecutor> executors)
    {this.journal=journal;this.executors=executors.ToDictionary(e=>e.Kind);}
    public void Pump()
    {
        foreach(var record in journal.Records.Where(r=>r.Stage is EncounterRewardStage.Offered or EncounterRewardStage.Applying).Take(8))
        {
            if(!executors.TryGetValue(record.Kind,out var executor))continue;
            try
            {
                if(record.Stage==EncounterRewardStage.Offered)
                {
                    if(!journal.Transition(record.Completion,EncounterRewardStage.Applying))continue;
                    var result=executor.Apply(record with {Stage=EncounterRewardStage.Applying});
                    // Even a native rejection remains terminal here: reissuing
                    // requires separate reconciliation, not an automatic retry.
                    if(result!=EncounterRewardApplyResult.AwaitingSave)
                    {journal.Transition(record.Completion,EncounterRewardStage.Uncertain);continue;}
                }
                if(executor.IsCharacterSaveConfirmed(record with {Stage=EncounterRewardStage.Applying}))journal.Transition(record.Completion,EncounterRewardStage.Saved);
            }
            catch(Exception ex)
            {
                LastError=ex.ToString();journal.Transition(record.Completion,EncounterRewardStage.Uncertain);
            }
        }
    }
}
