using System.Net;
using System.Text.Json;
using LastEpochLAN.Mod;
using LastEpochLAN.Core;
var checks=new List<string>();
void Check(bool ok,string name){if(!ok)throw new Exception(name);checks.Add(name);}
var policy=new AutomaticRoom();
Check(AutomaticRoom.Address=="27.70.239.115" && AutomaticRoom.Port==7777,"Fixed requested IPv4/port");
Check(!policy.Due(0,false,false),"No connection before a named character logs in");
Check(policy.Observe("a","Doomsday") && policy.IsHost && policy.Name=="Doomsday" && policy.Due(0,false,false),"Doomsday character hosts immediately on login");
Check(!policy.Observe("a","Doomsday"),"Stable character does not reset retry timer every frame");
Check(!policy.Due(0,true,false) && !policy.Due(0,false,true),"No parallel attempt while connected or joining");
policy.Attempt(10);policy.Failed(15);
Check(!policy.Due(44.999,false,false) && policy.Due(45,false,false),"Failed join retries exactly 30 seconds after completion");
policy.Attempt(45);policy.Failed(50);
Check(!policy.Due(79.999,false,false) && policy.Due(80,false,false),"Repeated failures each retain 30-second cadence");
policy.Pause();Check(!policy.Due(500,false,false),"Manual Disconnect pauses automatic retries");
policy.Resume();Check(policy.Due(500,false,false),"Reconnect resumes immediately");
policy.Pause();Check(policy.Observe("b","Sorcerer") && !policy.IsHost && policy.Due(0,false,false),"Changing character cancels prior login state, chooses Guest and resets manual pause");
Check(policy.Observe("b","doomsday") && !policy.IsHost,"Only exact requested Doomsday name selects Host");
Check(policy.Observe("","") && !policy.Due(1000,false,false),"Character selection/logout clears automatic connection identity");
policy.Observe("b","Sorcerer");Check(policy.Due(0,false,false),"Relogging same character starts a fresh connection");
using(var host=LanSession.Host("Doomsday","automatic-room-fixture","",0,IPAddress.Loopback))
{
 using var guest=await LanSession.JoinAsync("127.0.0.1",host.Port,"Sorcerer","automatic-room-fixture","");
 Check(host.Connected && host.IsHost && guest.Connected && !guest.IsHost,"Real loopback host/guest handshake works with no room code");
}
using(var host=LanSession.Host("Host","automatic-room-fixture","secret-room",0,IPAddress.Loopback))
{
 bool rejected=false;
 try{using var wrong=await LanSession.JoinAsync("127.0.0.1",host.Port,"Guest","automatic-room-fixture","");}catch(InvalidOperationException){rejected=true;}
 Check(rejected,"Explicit coded sessions still reject mismatched code; empty-room support does not bypass handshake");
}
Directory.CreateDirectory("validation");File.WriteAllText("validation/automatic-room-fixture.json",JsonSerializer.Serialize(new{passed=checks.Count,checks,scope="Actual production policy and real TCP loopback handshake; no public endpoint contacted"},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("PASS "+checks.Count+" automatic-room policy/transport checks");
