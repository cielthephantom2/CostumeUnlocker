using System.Net;
using LastEpochLAN.Core;

internal static class PartyStatusTests
{
    internal static async Task Run(Action<bool,string> check)
    {
        var number=new StatusValue {Kind="number",Number=3.25};
        var value=new StatusValue {Kind="object",Type="Il2Cpp.DamageStats",Fields=new[]{new StatusField("damage",new() {Kind="list",Items=new[]{number}})}};
        var stat=new StatusStat {Property=1,Added=-12,Increased=-0.2f,More=new[]{0.5f},DontCollapse=true};
        var data=new ResolvedAilment {Application=1,Id=1,Stacks=2,Duration=4,NativeDamage=value,
            SourceRevision=1,SourceProfile=new() {Revision=1,Stats=new[]{stat}},ExpirationCallback=true};
        check(data.IsValid(),"shared status payload accepts resolved native data, negative debuffs, source stats and expiration token");
        var interaction=new ResolvedAilment {Operation="hit",Application=4,Stacks=1,Duration=1,HitDamage=12,HitEvents=1};
        check(interaction.IsValid() && !(interaction with {HitDamage=float.NaN}).IsValid() && !(interaction with {NativeDamage=value}).IsValid(),
            "native on-hit interaction is distinct from a new status application or another HP subtraction");
        check(!(data with {SourceRevision=2}).IsValid() && !(data with {Application=0}).IsValid(),"source revision mismatch and unaddressable expiration callback rejected");
        check(!(value with {Fields=new[]{new StatusField("damage",number),new StatusField("damage",number)}}).IsValid() &&
            !new StatusValue {Kind="map",Items=new[]{number}}.IsValid(),"ambiguous native fields and odd tracker maps are rejected");
        var copy=data.Copy();data.SourceProfile!.Stats[0].More[0]=9;value.Fields[0].Value.Items[0]=number with {Number=99};
        check(copy.SourceProfile!.Stats[0].More[0]==0.5f && copy.NativeDamage!.Fields[0].Value.Items[0].Number==3.25,
            "queued native status data deep-copies nested arrays and creator profiles");
        var enemy=new EnemyState {Id=1,Template="status-target",Health=100,MaxHealth=100,Ailments=new[]{new AilmentCount(1,2)},StatusModifiers=new[]{stat},StatusCounters=new(2,1,0,4)};
        var world=new WorldState {Epoch=8,Revision=1,Scene="status-map",AcceptsResolvedAilments=true,Enemies=new[]{enemy}};
        var writer=new WorldChunkWriter();var reader=new WorldChunkReader();WorldState? decoded=null;
        foreach(var chunk in writer.Encode(world,true)) decoded=reader.Accept(chunk)??decoded;
        check(decoded?.Enemies[0].StatusModifiers[0].Same(stat)==true,"native ailment modifier identity flags survive actual world baseline codec");
        check(decoded?.Enemies[0].StatusCounters==new StatusCounters(2,1,0,4),"curse, brand, movement and negative ailment counters survive late-join baseline codec");
        var next=world with {Revision=2,Enemies=new[]{enemy with {StatusModifiers=new[]{stat with {Added=-24}}}}};
        check(!WorldContent.Same(world,next),"debuff potency changes invalidate shared world even when stack counts stay the same");
        foreach(var chunk in writer.Encode(next,true)) decoded=reader.Accept(chunk)??decoded;
        check(decoded?.Enemies[0].StatusModifiers[0].Added==-24,"debuff potency updates travel over world deltas");
        foreach(var chunk in writer.Encode(next with {Revision=3,Enemies=new[]{enemy with {StatusModifiers=Array.Empty<StatusStat>(),Ailments=Array.Empty<AilmentCount>()}}},true)) decoded=reader.Accept(chunk)??decoded;
        check(decoded?.Enemies[0].StatusModifiers.Length==0 && decoded.Enemies[0].Ailments.Length==0,"cleanse clears both shared modifiers and counts over deltas");
        using var host=LanSession.Host("SharedStatusHost","status-build","status-test",0,IPAddress.Loopback);
        host.Publish(new PlayerState {Available=true,Scene="status-map"});host.PublishWorld(world);
        using var guest=await LanSession.JoinAsync("127.0.0.1",host.Port,"SharedStatusGuest","status-build","status-test");
        guest.Publish(new PlayerState {Available=true,Scene="status-map"});
        async Task Wait(Func<bool> condition)
        {
            long end=Environment.TickCount64+5000;
            while(!condition() && Environment.TickCount64<end) await Task.Delay(10);
            if(!condition()) throw new TimeoutException("Shared status TCP fixture timed out.");
        }
        await Wait(()=>guest.AcceptsResolvedAilments && host.PlayerPositions.Any(p=>p.Id==guest.LocalId && p.Available));
        check(guest.RequestAilment(8,1,copy),"Guest queues resolved status only after Host advertises native authority");
        HitRequest? request=null;await Wait(()=>request!=null || host.TryTakeAilment(out request));
        check(request?.PlayerId==guest.LocalId && request.Ailment!.NativeDamage!.Fields[0].Value.Items[0].Number==3.25 && !host.TryTakeHit(out _),
            "authenticated TCP delivers source-resolved status separately from HP damage");
        check(host.SendStatusCallback(guest.LocalId,new() {Kind=1,Epoch=8,Application=1,Position=new() {X=7}}),"Host queues source expiration notification");
        StatusCallback? callback=null;await Wait(()=>callback!=null || guest.TryTakeStatusCallback(out callback));
        check(callback is {Kind:1,Application:1,Epoch:8} && callback.Position.X==7,"expiration token returns through real Host-to-Guest transport");
        check(!guest.SendStatusCallback(host.LocalId,new() {Kind=1,Epoch=8,Application=1}),"Guest cannot forge Host status callbacks");
        guest.RequestAilment(7,1,copy with {Application=2});await Task.Delay(150);
        check(!host.TryTakeAilment(out _),"status requests from a stale instance cannot reach native receiver");
        guest.RequestHit(8,1,5);guest.RequestAilment(8,1,copy with {Application=4});guest.RequestAilment(8,1,interaction);guest.RequestHit(8,1,6);
        var ordered=new List<HitRequest>();
        await Wait(()=>{while(host.TryTakeCombat(out var item)) ordered.Add(item!);return ordered.Count==4;});
        check(ordered[0].Damage==5 && ordered[1].Ailment?.Operation=="apply" && ordered[2].Ailment?.Operation=="hit" && ordered[3].Damage==6,
            "real transport preserves damage, status application and on-hit processing in one authoritative command order");
        guest.Publish(new PlayerState {Available=true,Scene="status-map",Dead=true});
        await Wait(()=>host.PlayerPositions.Any(p=>p.Id==guest.LocalId && p.Dead));
        guest.RequestAilment(8,1,copy with {Application=3});request=null;
        await Wait(()=>request!=null || host.TryTakeAilment(out request));
        check(request?.Ailment?.Application==3,"already-created effects can finish applying after their source player dies");
    }
}
