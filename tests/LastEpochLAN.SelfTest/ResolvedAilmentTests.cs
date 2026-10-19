using LastEpochLAN.Core;

internal static class ResolvedAilmentTests
{
    internal static void Run(Action<bool,string> check)
    {
        var host=new ResolvedAilment {Id=1,Stacks=3,Duration=4,Damage=new float[]{100,0,0,0,0,0,0}};
        var guest=host with {Stacks=5,Duration=6,Damage=new float[]{250,0,0,0,0,0,0}};
        check(host.IsValid() && guest.IsValid() && host.Damage[0]==100 && guest.Damage[0]==250 && host.Duration!=guest.Duration,
            "resolved DoT applications preserve separate source potency, stacks and durations");
        var copy=guest.Copy();guest.Damage[0]=999;
        check(copy.Damage[0]==250,"resolved ailment queues isolate damage arrays from caller mutation");
        var request=new HitRequest {Epoch=1,EnemyId=1,Sequence=1,Ailment=copy};
        check(request.IsValid() && !(request with {Damage=10}).IsValid() && !(request with {ControlType=1,ControlDuration=1}).IsValid(),
            "one request cannot apply both an ailment and an ordinary HP/control result");
        check(!(copy with {Duration=float.NaN}).IsValid() && !(copy with {Stacks=0}).IsValid() &&
            !(copy with {Damage=new float[6]}).IsValid() && !(copy with {Penetration=new[]{float.PositiveInfinity,0,0,0,0,0,0}}).IsValid(),
            "nonfinite or malformed native ailment payloads are rejected");
        var enemy=new EnemyState {Id=1,Template="status-target",MaxHealth=100,Health=100,Ailments=new[]{new AilmentCount(1,8)}};
        check(enemy.IsValid() && !(enemy with {Ailments=new[]{new AilmentCount(1,3),new AilmentCount(1,5)}}).IsValid(),
            "shared status presentation has one aggregate count per native ID");
        var world=new WorldState {Epoch=1,Revision=1,Scene="status-map",AcceptsResolvedAilments=true,Enemies=new[]{enemy}};
        var writer=new WorldChunkWriter();var reader=new WorldChunkReader();WorldState? applied=null;
        foreach(var chunk in writer.Encode(world,true)) applied=reader.Accept(chunk) ?? applied;
        check(applied?.AcceptsResolvedAilments==true && applied.Enemies[0].Ailments[0].Count==8,
            "native status counts and delegation capability survive the actual baseline chunk codec");
        var changed=world with {Revision=2,Enemies=new[]{enemy with {Ailments=new[]{new AilmentCount(1,5),new AilmentCount(2,1)}}}};
        check(!WorldContent.Same(world,changed),"status count changes invalidate the authoritative world delta comparison");
        foreach(var chunk in writer.Encode(changed,true)) applied=reader.Accept(chunk) ?? applied;
        check(applied?.Enemies[0].Ailments.SequenceEqual(changed.Enemies[0].Ailments)==true,
            "status changes replace counts over deltas rather than incrementing on replay");
        foreach(var chunk in writer.Encode(changed with {Revision=3,Enemies=new[]{enemy with {Ailments=Array.Empty<AilmentCount>()}}},true)) applied=reader.Accept(chunk) ?? applied;
        check(applied?.Enemies[0].Ailments.Length==0,"cleanse/expiry clears stale mirrored native status counts through deltas");
    }
}
