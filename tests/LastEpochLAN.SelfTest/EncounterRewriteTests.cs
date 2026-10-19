using System.Net;
using System.Text.Json;
using LastEpochLAN.Core;
using LastEpochLAN.Core.Encounters;

internal static class EncounterRewriteTests
{
 internal static async Task Run(Action<bool,string> check)
 {
  using var host=LanSession.Host("RewriteHost","rewrite-test","rewrite",0,IPAddress.Loopback);
  host.Publish(new(){Scene="map",Available=true,GiftIdentity=Guid.NewGuid().ToString("N"),CharacterId="host"});
  using var guest=await LanSession.JoinAsync("127.0.0.1",host.Port,"RewriteGuest","rewrite-test","rewrite");
  guest.Publish(new(){Scene="map",Available=true,GiftIdentity=Guid.NewGuid().ToString("N"),CharacterId="guest"});
  var until=Environment.TickCount64+5000;
  while(!host.PlayerPositions.Any(p=>p.Id==guest.LocalId && p.CharacterId=="guest") && Environment.TickCount64<until)await Task.Delay(10);
  check(host.PlayerPositions.Any(p=>p.Id==guest.LocalId && p.CharacterId=="guest"),"rewrite fixture has authenticated peer state from real TCP");
  bool refused=false;try{_ = new EncounterHost(guest);}catch(InvalidOperationException){refused=true;}
  check(refused,"Guest cannot construct a Host encounter authority");
  var authority=new EncounterHost(host);var scene=new EncounterInstance(Guid.NewGuid(),1,"map","");authority.Begin(scene);
  var id=authority.Register(EncounterKind.Nemesis,new());
  EncounterCommand Command(long sequence,EncounterAction action=EncounterAction.Challenge)=>new(scene.Id,scene.Epoch,id,authority.Snapshot().Entries.Single(e=>e.Id==id).Revision,sequence,action);
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
  check(authority.NativeStarted(omen) && !authority.NativeStarted(omen),"Host-native automatic Omen activation is single use");
  check(authority.NativeFailed(omen) && authority.NativeCompleted(omen)==Guid.Empty,"partially failed native encounter cannot automatically generate a reward");
  check(authority.NativeRemoved(omen) && !authority.NativeRemoved(omen),"removed encounter retains a stable tombstone");
  authority.Begin(scene with {Id=Guid.NewGuid(),Epoch=2});
  check(authority.Snapshot().Entries.Length==0 && !authority.NativeAccepted(held!),"map travel clears entities and rejects old native callback tokens");
  refused=false;try{authority.Begin(scene);}catch(InvalidOperationException){refused=true;}
  check(refused,"old scene instance cannot be reopened");
  host.Dispose();refused=false;try{authority.Register(EncounterKind.Cache,new());}catch(InvalidOperationException){refused=true;}
  check(refused,"disconnect invalidates the managed authority before any native adapter can act");
 }
}
