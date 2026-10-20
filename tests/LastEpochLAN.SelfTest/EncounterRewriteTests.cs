using System.Net;
using System.Text.Json;
using LastEpochLAN.Core;
using LastEpochLAN.Core.Encounters;

internal static class EncounterRewriteTests
{
 internal static async Task Run(Action<bool,string> check)
 {
  using var host=LanSession.Host("RewriteHost","rewrite-test","rewrite",0,IPAddress.Loopback);
  host.Publish(new(){Scene="map",Available=true,Cooperative=true,GiftIdentity=Guid.NewGuid().ToString("N"),CharacterId="host"});
  using var guest=await LanSession.JoinAsync("127.0.0.1",host.Port,"RewriteGuest","rewrite-test","rewrite");
  guest.Publish(new(){Scene="map",Available=true,Cooperative=true,GiftIdentity=Guid.NewGuid().ToString("N"),CharacterId="guest"});
  var until=Environment.TickCount64+5000;
  while(!host.PlayerPositions.Any(p=>p.Id==guest.LocalId && p.CharacterId=="guest") && Environment.TickCount64<until)await Task.Delay(10);
  check(host.PlayerPositions.Any(p=>p.Id==guest.LocalId && p.CharacterId=="guest"),"rewrite fixture has authenticated peer state from real TCP");
  bool refused=false;try{_ = new EncounterHost(guest);}catch(InvalidOperationException){refused=true;}
  check(refused,"Guest cannot construct a Host encounter authority");
  var authority=new EncounterHost(host);var scene=new EncounterInstance(Guid.NewGuid(),1,"map","");authority.Begin(scene);
  var id=authority.Register(EncounterKind.BloodCrystal,new());
  EncounterCommand Command(long sequence,EncounterAction action=EncounterAction.Activate)=>new(scene.Id,scene.Epoch,id,authority.Snapshot().Entries.Single(e=>e.Id==id).Revision,sequence,action);
  check(!authority.TryReserve("forged-peer",Command(1),out _),"unknown peer cannot activate an encounter");
  check(!authority.TryReserve(guest.LocalId,null!,out _),"null command cannot reach a native adapter");
  check(!authority.TryReserve(guest.LocalId,Command(1) with {Epoch=0},out _),"stale scene epoch rejected");
  check(!authority.TryReserve(guest.LocalId,Command(1) with {Instance=Guid.NewGuid()},out _),"foreign scene instance rejected");
  check(!authority.TryReserve(guest.LocalId,Command(1,EncounterAction.SelectReward),out _),"typed family action whitelist rejects arbitrary reward selection");
  check(!authority.TryReserve(guest.LocalId,Command(2) with {Revision=999},out _),"command based on a stale view cannot reserve a changed entity");
  var guestState=host.PlayerPositions.First(p=>p.Id==guest.LocalId);
  guest.Publish(guestState with {X=100});until=Environment.TickCount64+5000;
  while(host.PlayerPositions.First(p=>p.Id==guest.LocalId).X!=100 && Environment.TickCount64<until)await Task.Delay(10);
  check(!authority.TryReserve(guest.LocalId,Command(3),out _),"range uses the peer's actual accepted TCP position");
  guest.Publish(guestState);until=Environment.TickCount64+5000;
  while(host.PlayerPositions.First(p=>p.Id==guest.LocalId).X!=0 && Environment.TickCount64<until)await Task.Delay(10);
  var command=Command(4);
  check(authority.TryReserve(guest.LocalId,command,out var held),"authenticated nearby Guest reserves one native action");
  check(!authority.TryReserve(guest.LocalId,command,out _),"replayed command cannot execute twice");
  check(!authority.NativeAccepted(held! with {PlayerId="forged"}) && authority.NativeAccepted(held!),"forged reservation cannot consume the legitimate native acceptance token");
  check(!authority.NativeAccepted(held!),"native acceptance token is single use");
  var complete=authority.NativeCompleted(id);var snapshot=authority.Snapshot();
  check(complete!=Guid.Empty && snapshot.Entries.Single().Recipients.Length==2,"native completion snapshots only actual room participants");
  check(authority.NativeCompleted(id)==complete,"duplicate native completion returns the existing event without another grant");
  snapshot.Entries[0].Recipients[0]="mutated";
  check(!authority.Snapshot().Entries[0].Recipients.Contains("mutated"),"caller cannot mutate authoritative recipient storage");
  var copy=JsonSerializer.Deserialize<EncounterSnapshot>(JsonSerializer.Serialize(authority.Snapshot()));
  check(copy?.Entries.Single().Completion==complete,"typed snapshot preserves completion identity through JSON");
  var omen=authority.Register(EncounterKind.OmenWindow,new());
  var omenView=authority.Snapshot().Entries.Single(e=>e.Id==omen);
  check(!authority.NativeStarted(omen) && omenView.Actions.SequenceEqual(new[]{EncounterAction.Activate}),"Omen exposes explicit interaction instead of bypassing activation authorization");
  check(authority.TryReserve(guest.LocalId,new(scene.Id,scene.Epoch,omen,omenView.Revision,5,EncounterAction.Activate),out var omenHeld) && authority.NativeAccepted(omenHeld!),"Guest Omen activation uses authenticated reservation and native confirmation");
  check(authority.NativeFailed(omen) && authority.NativeCompleted(omen)==Guid.Empty,"partially failed native encounter cannot automatically generate a reward");
  check(authority.NativeRemoved(omen) && !authority.NativeRemoved(omen),"removed encounter retains a stable tombstone");
  var nemesis=authority.Register(EncounterKind.Nemesis,new());
  var nemesisRevision=authority.Snapshot().Entries.Single(e=>e.Id==nemesis).Revision;
  var guestChoice=new EncounterCommand(scene.Id,scene.Epoch,nemesis,nemesisRevision,6,EncounterAction.Challenge);
  var hostChoice=guestChoice with {Sequence=1,Action=EncounterAction.Empower};
  check(authority.TryReserve(guest.LocalId,guestChoice,out var guestTurn) && authority.NativeAccepted(guestTurn!),"Nemesis Guest reserves its own Challenge turn");
  check(authority.TryReserve(host.LocalId,hostChoice,out var hostTurn) && authority.NativeAccepted(hostTurn!),"Nemesis Host independently chooses Empower using its own unchanged turn revision");
  check(authority.NativeCompleted(nemesis)==Guid.Empty,"shared Nemesis completion cannot finish or reward all private turns");
  var guestOutcome=authority.NativeNemesisTurnCompleted(guestTurn!);
  check(guestOutcome!=Guid.Empty && authority.PrivateNemesisTurns().Single(t=>t.CharacterKey==hostTurn!.CharacterKey).Phase==NemesisTurnPhase.Active,"Guest Nemesis outcome does not complete the Host turn");
  check(authority.NativeNemesisTurnCompleted(guestTurn! with {CharacterKey=hostTurn!.CharacterKey})==Guid.Empty,"Nemesis native outcome cannot be redirected to another character");
  var hostOutcome=authority.NativeNemesisTurnCompleted(hostTurn!);
  check(hostOutcome!=Guid.Empty && hostOutcome!=guestOutcome && authority.NativeNemesisTurnCompleted(guestTurn!)==guestOutcome,"private Nemesis outcomes have distinct stable IDs and duplicate callbacks are idempotent");
  check(!authority.TryReserve(guest.LocalId,guestChoice with {Sequence=7},out _),"completed private Nemesis choice cannot be replayed as a fresh turn");
  await Transport(host,guest,authority,scene,nemesis,check);
  authority.Begin(scene with {Id=Guid.NewGuid(),Epoch=2});
  check(authority.Snapshot().Entries.Length==0 && !authority.NativeAccepted(held!),"map travel clears entities and rejects old native callback tokens");
  refused=false;try{authority.Begin(scene);}catch(InvalidOperationException){refused=true;}
  check(refused,"old scene instance cannot be reopened");
  host.Dispose();refused=false;try{authority.Register(EncounterKind.Cache,new());}catch(InvalidOperationException){refused=true;}
  check(refused,"disconnect invalidates the managed authority before any native adapter can act");
 }

 private static async Task Transport(LanSession host,LanSession guest,EncounterHost authority,EncounterInstance scene,Guid nemesis,Action<bool,string> check)
 {
  async Task Until(Func<bool> ready)
  {
   var deadline=Environment.TickCount64+5000;
   while(!ready() && Environment.TickCount64<deadline)await Task.Delay(10);
  }
  host.PublishEncounters(authority.Snapshot(),authority.PrivateNemesisTurns());
  await Until(()=>guest.Encounters?.Snapshot?.Entries.Any(e=>e.Id==nemesis)==true);
  var delivery=guest.Encounters;
  check(delivery?.IsValid()==true && delivery.Turns.Length==1 && delivery.Turns[0].Choice==EncounterAction.Challenge,"real TCP encounter frame contains only the receiving character's Nemesis turn");
  check(delivery?.Snapshot?.Entries.Single(e=>e.Kind==EncounterKind.BloodCrystal).Recipients.Length==1,"private encounter delivery excludes another character's reward recipients");
  var entry=authority.Snapshot().Entries.Single(e=>e.Id==nemesis);
  var command=new EncounterCommand(scene.Id,scene.Epoch,nemesis,entry.Revision,8,EncounterAction.Banish);
  check(guest.SendEncounter(command),"encounter commands enter a dedicated outgoing queue");
  EncounterRequest? request=null;
  await Until(()=>host.TryTakeEncounterRequest(out request));
  check(request?.PlayerId==guest.LocalId && request.Command==command,"real TCP encounter command binds its sender to the authenticated connection");
  var reply=new EncounterReply(scene.Id,scene.Epoch,nemesis,command.Sequence,EncounterReplyCode.Rejected,entry.Revision);
  check(host.ReplyEncounter(guest.LocalId,reply) && !guest.ReplyEncounter(host.LocalId,reply),"only Host sends encounter acknowledgments");
  EncounterReply? received=null;
  await Until(()=>guest.TryTakeEncounterReply(out received));
  check(received==reply,"dedicated encounter acknowledgment returns through real TCP");
  var state=host.PlayerPositions.Single(p=>p.Id==guest.LocalId);
  guest.Publish(state with {Cooperative=false});
  await Until(()=>guest.Encounters?.Snapshot==null);
  check(guest.Encounters is {Snapshot:null,Turns.Length:0},"party-only transition clears encounter presentation and private turns without requiring another Host publication");
  guest.Publish(state);
  await Until(()=>guest.Encounters?.Snapshot!=null);
  check(guest.Encounters?.Turns.Length==1,"returning participant receives current private baseline");
  host.ClearEncounters();
  await Until(()=>guest.Encounters?.Snapshot==null);
  check(guest.Encounters is {Snapshot:null,Turns.Length:0},"Host scene clear reaches Client over the encounter channel");
 }
}
