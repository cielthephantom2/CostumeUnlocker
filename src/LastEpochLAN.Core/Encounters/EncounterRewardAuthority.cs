using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LastEpochLAN.Core.Encounters;

// Durable Host entitlement/receipt ledger. It contains identifiers only, never
// inventory or a caller-supplied reward recipe. Retained across map instances.
public sealed class EncounterRewardAuthority:IDisposable
{
    private readonly string directory;
    private readonly FileStream writer;
    private readonly Dictionary<string,EncounterRewardRecord> records=new(StringComparer.Ordinal);
    private bool disposed;
    public EncounterRewardAuthority(string root)
    {
        directory=Path.Combine(root,"host-encounter-entitlements");Directory.CreateDirectory(directory);
        writer=new FileStream(Path.Combine(directory,"writer.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        try
        {
            foreach(var path in Directory.EnumerateFiles(directory,"*.json"))
            {
                if(new FileInfo(path).Length>4096 || records.Count>=32768)throw new InvalidDataException("Host encounter receipt budget exceeded.");
                var record=JsonSerializer.Deserialize<EncounterRewardRecord>(File.ReadAllText(path));
                if(record==null || !record.IsValid() || record.Stage is not (EncounterRewardStage.Offered or EncounterRewardStage.Acknowledged) ||
                    Path.GetFileNameWithoutExtension(path)!=Key(record.CharacterKey,record.Completion) || !records.TryAdd(Key(record.CharacterKey,record.Completion),record))
                    throw new InvalidDataException("Invalid Host encounter entitlement.");
            }
        }
        catch{writer.Dispose();throw;}
    }
    public void Publish(EncounterSnapshot snapshot,NemesisTurn[] turns)
    {
        RequireOpen();
        if(!snapshot.IsValid())throw new ArgumentException("Invalid entitlement snapshot.");
        foreach(var view in snapshot.Entries)
        {
            if(view.Kind==EncounterKind.Nemesis)
            {
                foreach(var turn in turns.Where(t=>t.Encounter==view.Id && t.Phase==NemesisTurnPhase.Completed && t.Choice==EncounterAction.Challenge))
                    Offer(snapshot.Instance,view,turn.CharacterKey,turn.Completion);
            }
            else if(view.Phase==EncounterPhase.Completed)
                foreach(var character in view.Recipients)Offer(snapshot.Instance,view,character,view.Completion);
        }
    }
    private void Offer(EncounterInstance instance,EncounterView view,string character,Guid completion)
    {
        var record=new EncounterRewardRecord(completion,instance.Id,instance.Epoch,view.Id,view.Kind,character,EncounterRewardStage.Offered);
        if(!record.IsValid())throw new InvalidDataException("Invalid Host entitlement.");
        string key=Key(character,completion);
        if(records.TryGetValue(key,out var old))
        {if(old with {Stage=EncounterRewardStage.Offered}!=record)throw new InvalidDataException("Conflicting Host entitlement.");return;}
        if(records.Count>=32768)throw new InvalidOperationException("Host entitlement budget exhausted.");
        Write(key,record);
    }
    public bool Acknowledge(PlayerState authenticated,EncounterCommand command)
    {
        RequireOpen();
        if(command==null || !command.IsValid() || command.Action!=EncounterAction.AcknowledgeReward || authenticated==null || !authenticated.IsValid())return false;
        string key=Key(EncounterHost.CharacterKey(authenticated),command.RewardCompletion);
        if(!records.TryGetValue(key,out var record) || record.Instance!=command.Instance || record.Epoch!=command.Epoch || record.Encounter!=command.Encounter)return false;
        if(record.Stage==EncounterRewardStage.Acknowledged)return true;
        Write(key,record with {Stage=EncounterRewardStage.Acknowledged});return true;
    }
    public bool IsAcknowledged(string character,Guid completion)
    {RequireOpen();return records.TryGetValue(Key(character,completion),out var record) && record.Stage==EncounterRewardStage.Acknowledged;}
    private static string Key(string character,Guid completion)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(character+"/"+completion.ToString("N"))));
    private void Write(string key,EncounterRewardRecord record)
    {
        string target=Path.Combine(directory,key+".json"),temp=target+".new";
        using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(stream,record);stream.Flush(true);}
        File.Move(temp,target,true);records[key]=record;
    }
    private void RequireOpen(){if(disposed)throw new ObjectDisposedException(nameof(EncounterRewardAuthority));}
    public void Dispose(){if(disposed)return;disposed=true;writer.Dispose();}
}
