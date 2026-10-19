namespace LastEpochLAN.Core.Encounters;

public enum EncounterKind { Nemesis,OmenWindow,BloodCrystal,ExiledMage,RiftBeast,Shrine,Cache,RoamingOmen,Champion,FortuneCache }
public enum EncounterAction { Activate,Challenge,Empower,Banish,SelectReward }
public enum EncounterPhase { Available,Reserved,Active,Completed,Faulted,Removed }
public sealed record EncounterInstance(Guid Id,long Epoch,string Scene,string RunId)
{
 public bool IsValid()=>Id!=Guid.Empty && Epoch>=0 && Scene is {Length:>0 and <=256} && RunId is {Length:<=128};
 public bool Contains(PlayerState p)=>p.Scene==Scene && p.EchoRunId==RunId;
}
public sealed record EncounterView(Guid Id,EncounterKind Kind,long Revision,VisualPoint Position,EncounterPhase Phase,
 EncounterAction[] Actions,Guid Completion,string[] Recipients)
{
 public EncounterView Copy()=>this with {Actions=Actions.ToArray(),Recipients=Recipients.ToArray()};
}
public sealed record EncounterCommand(Guid Instance,long Epoch,Guid Encounter,long Revision,long Sequence,EncounterAction Action);
public sealed record EncounterReservation(Guid Id,Guid Encounter,string PlayerId,EncounterAction Action);
public sealed record EncounterSnapshot(EncounterInstance Instance,long Revision,EncounterView[] Entries);

// A room-owned managed state machine. It has no Unity/Harmony dependencies and
// grants no loot. Native family adapters and dedicated transport are separate
// work; this core cannot silently replace any native solo callback.
public sealed class EncounterHost
{
 public const int MaximumEntities=256;
 private readonly LanSession room;
 private readonly Dictionary<Guid,EncounterView> entities=new();
 private readonly Dictionary<Guid,EncounterReservation> reservations=new();
 private readonly Dictionary<string,long> peerSequences=new(StringComparer.Ordinal);
 private EncounterInstance? instance;
 private long revision;
 public EncounterHost(LanSession room)
 {
  if(!room.Connected || !room.IsHost)throw new InvalidOperationException("A live Host room is required.");
  this.room=room;
 }
 private void RequireLive(){if(!room.Connected || !room.IsHost)throw new InvalidOperationException("Encounter room has ended.");}
 public void Begin(EncounterInstance next)
 {
  RequireLive();if(!next.IsValid())throw new ArgumentException("Invalid encounter scene instance.");
  if(instance==next)return;
  if(instance!=null && next.Epoch<=instance.Epoch)throw new InvalidOperationException("Scene instance must advance its epoch.");
  instance=next;entities.Clear();reservations.Clear();peerSequences.Clear();revision=0;
 }
 private static EncounterAction[] Allowed(EncounterKind kind)=>kind switch
 {
  EncounterKind.Nemesis=>new[]{EncounterAction.Challenge,EncounterAction.Empower,EncounterAction.Banish},
  EncounterKind.OmenWindow or EncounterKind.RoamingOmen or EncounterKind.Champion=>Array.Empty<EncounterAction>(),
  _=>new[]{EncounterAction.Activate}
 };
 public Guid Register(EncounterKind kind,VisualPoint position)
 {
  RequireLive();if(instance==null || !Enum.IsDefined(typeof(EncounterKind),kind) || position==null || !position.IsValid())throw new ArgumentException("Invalid native registration.");
  if(entities.Count>=MaximumEntities)throw new InvalidOperationException("Encounter scene budget exceeded.");
  var id=Guid.NewGuid();entities.Add(id,new(id,kind,++revision,position,EncounterPhase.Available,Allowed(kind),Guid.Empty,Array.Empty<string>()));return id;
 }
 public bool TryReserve(string authenticatedPeerId,EncounterCommand command,out EncounterReservation? result)
 {
  result=null;RequireLive();
  // The future transport supplies its connection's peer ID. All gameplay
  // fields come from that room's accepted state, never from command payloads.
  var authenticated=room.PlayerPositions.FirstOrDefault(p=>p.Id==authenticatedPeerId);
  if(instance==null || command==null || authenticated==null || !authenticated.IsValid() || authenticated.Id=="" || !authenticated.Available || authenticated.Dead ||
   !instance.Contains(authenticated) || command.Instance!=instance.Id || command.Epoch!=instance.Epoch || command.Sequence<=0)return false;
  if(command.Sequence<=peerSequences.GetValueOrDefault(authenticated.Id))return false;
  var currentPeers=room.PlayerPositions.Select(p=>p.Id).ToHashSet(StringComparer.Ordinal);
  foreach(var oldPeer in peerSequences.Keys.Where(id=>!currentPeers.Contains(id)).ToArray())peerSequences.Remove(oldPeer);
  if(peerSequences.Count>=4 && !peerSequences.ContainsKey(authenticated.Id))return false;
  peerSequences[authenticated.Id]=command.Sequence;
  if(!entities.TryGetValue(command.Encounter,out var entry) || entry.Revision!=command.Revision || entry.Phase!=EncounterPhase.Available || !entry.Actions.Contains(command.Action))return false;
  double distance=Math.Pow(authenticated.X-entry.Position.X,2)+Math.Pow(authenticated.Y-entry.Position.Y,2)+Math.Pow(authenticated.Z-entry.Position.Z,2);
  if(distance>64)return false;
  result=new(Guid.NewGuid(),entry.Id,authenticated.Id,command.Action);reservations.Add(result.Id,result);
  entities[entry.Id]=entry with {Phase=EncounterPhase.Reserved,Revision=++revision};return true;
 }
 public bool NativeAccepted(EncounterReservation reservation)
 {
  RequireLive();if(!reservations.TryGetValue(reservation.Id,out var held) || held!=reservation || !entities.TryGetValue(held.Encounter,out var entry) || entry.Phase!=EncounterPhase.Reserved)return false;
  reservations.Remove(reservation.Id);
  entities[entry.Id]=entry with {Phase=EncounterPhase.Active,Revision=++revision};return true;
 }
 public bool NativeRejectedBeforeExecution(EncounterReservation reservation)
 {
  RequireLive();if(!reservations.TryGetValue(reservation.Id,out var held) || held!=reservation || !entities.TryGetValue(held.Encounter,out var entry) || entry.Phase!=EncounterPhase.Reserved)return false;
  reservations.Remove(reservation.Id);
  entities[entry.Id]=entry with {Phase=EncounterPhase.Available,Revision=++revision};return true;
 }
 public bool NativeStarted(Guid id)
 {
  RequireLive();if(!entities.TryGetValue(id,out var entry) || entry.Phase!=EncounterPhase.Available || entry.Actions.Length!=0)return false;
  entities[id]=entry with {Phase=EncounterPhase.Active,Revision=++revision};return true;
 }
 public Guid NativeCompleted(Guid id)
 {
  RequireLive();if(instance==null || !entities.TryGetValue(id,out var entry))return Guid.Empty;
  if(entry.Phase==EncounterPhase.Completed)return entry.Completion;
  if(entry.Phase!=EncounterPhase.Active)return Guid.Empty;
  var recipients=room.PlayerPositions.Where(p=>p!=null && p.IsValid() && p.Available && !p.Dead && instance.Contains(p) && p.GiftIdentity!="" && p.CharacterId!="")
   .Select(p=>p.GiftIdentity+":"+p.CharacterId).Distinct(StringComparer.Ordinal).Take(4).ToArray();
  var completion=Guid.NewGuid();entities[id]=entry with {Phase=EncounterPhase.Completed,Revision=++revision,Completion=completion,Recipients=recipients};return completion;
 }
 public bool NativeFailed(Guid id)
 {
  RequireLive();if(!entities.TryGetValue(id,out var entry) || entry.Phase is EncounterPhase.Completed or EncounterPhase.Removed or EncounterPhase.Faulted)return false;
  foreach(var token in reservations.Where(r=>r.Value.Encounter==id).Select(r=>r.Key).ToArray())reservations.Remove(token);
  entities[id]=entry with {Phase=EncounterPhase.Faulted,Revision=++revision};return true;
 }
 public bool NativeRemoved(Guid id)
 {
  RequireLive();if(!entities.TryGetValue(id,out var entry) || entry.Phase==EncounterPhase.Removed)return false;
  foreach(var token in reservations.Where(r=>r.Value.Encounter==id).Select(r=>r.Key).ToArray())reservations.Remove(token);
  entities[id]=entry with {Phase=EncounterPhase.Removed,Revision=++revision};return true;
 }
 public EncounterSnapshot Snapshot()
 {RequireLive();return new(instance??throw new InvalidOperationException("No encounter scene instance."),revision,entities.Values.Select(e=>e.Copy()).ToArray());}
}
