namespace LastEpochLAN.Core.Encounters;

public enum EncounterKind { Nemesis,OmenWindow,BloodCrystal,ExiledMage,RiftBeast,Shrine,Cache,RoamingOmen,Champion,FortuneCache }
public enum EncounterAction { Activate,Challenge,Empower,Banish,SelectReward,AcknowledgeReward }
public enum EncounterPhase { Available,Reserved,Active,Completed,Faulted,Removed }
public sealed record EncounterActors(long WorldEpoch,int[] Bosses,int[] Npcs)
{
 public bool IsValid()=>WorldEpoch>=0 && Bosses is {Length:<=16} && Npcs is {Length:<=256} &&
  Bosses.Concat(Npcs).All(id=>id>0) && Bosses.Concat(Npcs).Distinct().Count()==Bosses.Length+Npcs.Length;
 public EncounterActors Copy()=>this with {Bosses=Bosses.ToArray(),Npcs=Npcs.ToArray()};
}
public sealed record EncounterDestination(string Scene,string RunId)
{
 public bool IsValid()=>Scene is {Length:>0 and <=256} && RunId is {Length:<=128};
}
public sealed record EncounterArea(VisualPoint Center,float Radius,bool Active)
{
 public bool IsValid()=>Center!=null && Center.IsValid() && float.IsFinite(Radius) && Radius is >0 and <=512;
 public bool Contains(VisualPoint position)=>IsValid() && position.IsValid() && Math.Pow(position.X-Center.X,2)+Math.Pow(position.Z-Center.Z,2)<=Radius*Radius;
}
public sealed record EncounterInstance(Guid Id,long Epoch,string Scene,string RunId)
{
 public bool IsValid()=>Id!=Guid.Empty && Epoch>=0 && Scene is {Length:>0 and <=256} && RunId is {Length:<=128};
 public bool Contains(PlayerState p)=>p.Scene==Scene && p.EchoRunId==RunId;
}
public sealed record EncounterView(Guid Id,EncounterKind Kind,long Revision,VisualPoint Position,EncounterPhase Phase,
 EncounterAction[] Actions,Guid Completion,string[] Recipients)
{
 public EncounterArea? Area {get;init;}
 public EncounterActors? Actors {get;init;}
 public EncounterDestination? Destination {get;init;}
 public EncounterView Copy()=>this with {Actions=Actions.ToArray(),Recipients=Recipients.ToArray(),Actors=Actors?.Copy()};
 public bool IsValid()=>Id!=Guid.Empty && Enum.IsDefined(typeof(EncounterKind),Kind) && Revision>0 &&
  Position!=null && Position.IsValid() && (Area==null || Area.IsValid() && Kind is EncounterKind.OmenWindow or EncounterKind.BloodCrystal) &&
  (Actors==null || Actors.IsValid()) && (Destination==null || Kind==EncounterKind.RiftBeast && Destination.IsValid()) && Enum.IsDefined(typeof(EncounterPhase),Phase) &&
  Actions is {Length:<=5} && Actions.All(a=>Enum.IsDefined(typeof(EncounterAction),a)) && Actions.Distinct().Count()==Actions.Length &&
  Recipients is {Length:<=4} && Recipients.All(r=>r is {Length:>0 and <=161}) && Recipients.Distinct(StringComparer.Ordinal).Count()==Recipients.Length &&
  (Phase==EncounterPhase.Completed ? Completion!=Guid.Empty : Completion==Guid.Empty);
}
public sealed record EncounterCommand(Guid Instance,long Epoch,Guid Encounter,long Revision,long Sequence,EncounterAction Action)
{
 public long TurnRevision {get;init;}
 public Guid RewardCompletion {get;init;}
 public bool IsValid()=>Instance!=Guid.Empty && Epoch>=0 && Encounter!=Guid.Empty && Revision>0 && Sequence>0 &&
  TurnRevision>=0 && Enum.IsDefined(typeof(EncounterAction),Action) &&
  (Action==EncounterAction.AcknowledgeReward ? RewardCompletion!=Guid.Empty && TurnRevision==0 : RewardCompletion==Guid.Empty);
}
public sealed record EncounterReservation(Guid Id,Guid Encounter,string PlayerId,EncounterAction Action)
{
 public string CharacterKey {get;init;}="";
}
public sealed record EncounterSnapshot(EncounterInstance Instance,long Revision,EncounterView[] Entries)
{
 public bool IsValid()=>Instance!=null && Instance.IsValid() && Revision>=0 && Entries is {Length:<=EncounterHost.MaximumEntities} &&
  Entries.All(e=>e!=null && e.IsValid() && e.Revision<=Revision) && Entries.Select(e=>e.Id).Distinct().Count()==Entries.Length;
 public EncounterSnapshot Copy()=>this with {Entries=Entries.Select(e=>e.Copy()).ToArray()};
}

// A room-owned managed state machine. It has no Unity/Harmony dependencies and
// grants no loot. LanSession provides dedicated transport; native family
// adapters remain separate work and cannot silently replace solo callbacks.
public sealed partial class EncounterHost
{
 public const int MaximumEntities=256;
 private readonly LanSession room;
 private readonly Dictionary<Guid,EncounterView> entities=new();
 private readonly Dictionary<Guid,EncounterReservation> reservations=new();
 private readonly Dictionary<Guid,string[]> participants=new();
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
  instance=next;entities.Clear();reservations.Clear();participants.Clear();peerSequences.Clear();nemesisTurns.Clear();nemesisExecutions.Clear();revision=0;
 }
 private static EncounterAction[] Allowed(EncounterKind kind)=>kind switch
 {
  EncounterKind.Nemesis=>new[]{EncounterAction.Challenge,EncounterAction.Empower,EncounterAction.Banish},
  EncounterKind.RoamingOmen or EncounterKind.Champion=>Array.Empty<EncounterAction>(),
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
  // Transport supplies its connection's peer ID. All gameplay
  // fields come from that room's accepted state, never from command payloads.
  var authenticated=room.PlayerPositions.FirstOrDefault(p=>p.Id==authenticatedPeerId);
  if(instance==null || command==null || !command.IsValid() || authenticated==null || !authenticated.IsValid() || authenticated.Id=="" || !authenticated.Cooperative || !authenticated.Available || authenticated.Dead ||
   !instance.Contains(authenticated) || command.Instance!=instance.Id || command.Epoch!=instance.Epoch || command.Sequence<=0)return false;
  if(command.Sequence<=peerSequences.GetValueOrDefault(authenticated.Id))return false;
  var currentPeers=room.PlayerPositions.Select(p=>p.Id).ToHashSet(StringComparer.Ordinal);
  foreach(var oldPeer in peerSequences.Keys.Where(id=>!currentPeers.Contains(id)).ToArray())peerSequences.Remove(oldPeer);
  if(peerSequences.Count>=4 && !peerSequences.ContainsKey(authenticated.Id))return false;
  peerSequences[authenticated.Id]=command.Sequence;
  if(!entities.TryGetValue(command.Encounter,out var entry) || !entry.Actions.Contains(command.Action))return false;
  double distance=Math.Pow(authenticated.X-entry.Position.X,2)+Math.Pow(authenticated.Y-entry.Position.Y,2)+Math.Pow(authenticated.Z-entry.Position.Z,2);
  if(distance>64)return false;
  if(entry.Kind==EncounterKind.Nemesis)return TryReserveNemesis(authenticated,command,entry,out result);
  if(entry.Revision!=command.Revision || entry.Phase!=EncounterPhase.Available || command.TurnRevision!=0)return false;
  result=new(Guid.NewGuid(),entry.Id,authenticated.Id,command.Action){CharacterKey=CharacterKey(authenticated)};reservations.Add(result.Id,result);
  entities[entry.Id]=entry with {Phase=EncounterPhase.Reserved,Revision=++revision};return true;
 }
 public bool NativeAccepted(EncounterReservation reservation)
 {
  RequireLive();if(!reservations.TryGetValue(reservation.Id,out var held) || held!=reservation || !entities.TryGetValue(held.Encounter,out var entry))return false;
  if(entry.Kind==EncounterKind.Nemesis)return AcceptNemesis(held,entry);
  if(entry.Phase!=EncounterPhase.Reserved)return false;
  reservations.Remove(reservation.Id);
  participants[entry.Id]=EligibleCharacters();
  entities[entry.Id]=entry with {Phase=EncounterPhase.Active,Revision=++revision};return true;
 }
 public bool CanExecute(EncounterReservation reservation)
 {
  RequireLive();
  if(instance==null || !reservations.TryGetValue(reservation.Id,out var held) || held!=reservation ||
   !entities.TryGetValue(held.Encounter,out var entry))return false;
  var peer=room.PlayerPositions.FirstOrDefault(p=>p.Id==held.PlayerId);
  if(peer==null || !peer.IsValid() || !peer.Cooperative || !peer.Available || peer.Dead ||
   !instance.Contains(peer) || CharacterKey(peer)!=held.CharacterKey)return false;
  if(held.CharacterKey!="" && room.PlayerPositions.Count(p=>CharacterKey(p)==held.CharacterKey)!=1)return false;
  return Math.Pow(peer.X-entry.Position.X,2)+Math.Pow(peer.Y-entry.Position.Y,2)+Math.Pow(peer.Z-entry.Position.Z,2)<=64;
 }
 public bool NativeRejectedBeforeExecution(EncounterReservation reservation)
 {
  RequireLive();if(!reservations.TryGetValue(reservation.Id,out var held) || held!=reservation || !entities.TryGetValue(held.Encounter,out var entry))return false;
  if(entry.Kind==EncounterKind.Nemesis)return RejectNemesis(held);
  if(entry.Phase!=EncounterPhase.Reserved)return false;
  reservations.Remove(reservation.Id);
  entities[entry.Id]=entry with {Phase=EncounterPhase.Available,Revision=++revision};return true;
 }
 public bool NativeStarted(Guid id)
 {
  RequireLive();if(!entities.TryGetValue(id,out var entry) || entry.Phase!=EncounterPhase.Available || entry.Actions.Length!=0)return false;
  participants[id]=EligibleCharacters();
  entities[id]=entry with {Phase=EncounterPhase.Active,Revision=++revision};return true;
 }
 public bool NativePosition(Guid id,VisualPoint position)
 {
  RequireLive();
  if(position==null || !position.IsValid() || !entities.TryGetValue(id,out var entry) || entry.Phase is EncounterPhase.Removed or EncounterPhase.Faulted || entry.Position==position)return false;
  entities[id]=entry with {Position=position,Revision=++revision};return true;
 }
 public bool NativeArea(Guid id,EncounterArea? area)
 {
  RequireLive();
  if(!entities.TryGetValue(id,out var entry) || entry.Kind is not (EncounterKind.OmenWindow or EncounterKind.BloodCrystal) ||
   entry.Phase is EncounterPhase.Removed or EncounterPhase.Faulted || area!=null && !area.IsValid() || entry.Area==area)return false;
  entities[id]=entry with {Area=area,Revision=++revision};return true;
 }
 public bool NativeWorld(Guid id,EncounterActors? actors,EncounterDestination? destination=null)
 {
  RequireLive();
  if(!entities.TryGetValue(id,out var entry) || entry.Phase is EncounterPhase.Removed or EncounterPhase.Faulted ||
   actors!=null && !actors.IsValid() || destination!=null && (entry.Kind!=EncounterKind.RiftBeast || !destination.IsValid()))return false;
  if(entry.Actors?.WorldEpoch==actors?.WorldEpoch && (entry.Actors?.Bosses??Array.Empty<int>()).SequenceEqual(actors?.Bosses??Array.Empty<int>()) &&
   (entry.Actors?.Npcs??Array.Empty<int>()).SequenceEqual(actors?.Npcs??Array.Empty<int>()) && entry.Destination==destination)return false;
  entities[id]=entry with {Actors=actors?.Copy(),Destination=destination,Revision=++revision};return true;
 }
 private string[] EligibleCharacters()=>room.PlayerPositions.Where(p=>p!=null && p.IsValid() && p.Cooperative && p.Available && !p.Dead && instance!.Contains(p))
  .Select(CharacterKey).Where(k=>k!="").GroupBy(k=>k,StringComparer.Ordinal).Where(g=>g.Count()==1).Select(g=>g.Key).Take(4).ToArray();
 public Guid NativeCompleted(Guid id)
 {
  RequireLive();if(instance==null || !entities.TryGetValue(id,out var entry))return Guid.Empty;
  if(entry.Kind==EncounterKind.Nemesis)return Guid.Empty; // A common completion must never complete everybody's private turn.
  if(entry.Phase==EncounterPhase.Completed)return entry.Completion;
  if(entry.Phase!=EncounterPhase.Active)return Guid.Empty;
  // Only participants present at activation and still eligible at completion.
  // A late join/reconnect may see the encounter, but cannot mint a reward turn.
  var recipients=EligibleCharacters().Intersect(participants.GetValueOrDefault(id)??Array.Empty<string>(),StringComparer.Ordinal).ToArray();
  var completion=Guid.NewGuid();entities[id]=entry with {Phase=EncounterPhase.Completed,Revision=++revision,Completion=completion,Recipients=recipients};return completion;
 }
 public bool NativeFailed(Guid id)
 {
  RequireLive();if(!entities.TryGetValue(id,out var entry) || entry.Phase is EncounterPhase.Completed or EncounterPhase.Removed or EncounterPhase.Faulted)return false;
  foreach(var token in reservations.Where(r=>r.Value.Encounter==id).Select(r=>r.Key).ToArray())reservations.Remove(token);
  FaultNemesisTurns(id);
  entities[id]=entry with {Phase=EncounterPhase.Faulted,Revision=++revision};return true;
 }
 public bool NativeRemoved(Guid id)
 {
  RequireLive();if(!entities.TryGetValue(id,out var entry) || entry.Phase==EncounterPhase.Removed)return false;
  foreach(var token in reservations.Where(r=>r.Value.Encounter==id).Select(r=>r.Key).ToArray())reservations.Remove(token);
  FaultNemesisTurns(id);
  entities[id]=entry with {Phase=EncounterPhase.Removed,Revision=++revision,Completion=Guid.Empty};return true;
 }
 public EncounterSnapshot Snapshot()
 {RequireLive();return new(instance??throw new InvalidOperationException("No encounter scene instance."),revision,entities.Values.Select(e=>e.Copy()).ToArray());}
}
