using LastEpochLAN.Core;

internal static class RoomLadderTests
{
    private static RoomLadderCharacter Character(string id="character") => new() {
        CharacterId=id, CharacterName="Test", Class=3, Mastery="Necromancer", Level=81, Experience=12345,
        ArenaWave=100, Corruption=300, Cycle=5, Skills=new[]{"skill-id"}};
    internal static async Task Run(Action<bool,string> check)
    {
        var c=Character();
        check(c.IsValid(),"room ladder accepts bounded offline character statistics");
        check(!(c with{Level=101}).IsValid() && !(c with{Experience=-1}).IsValid() &&
            !(c with{Corruption=int.MaxValue}).IsValid() && !(c with{Class=5}).IsValid() &&
            !(c with{CharacterName="bad\nname"}).IsValid() && !(c with{Skills=new string[6]}).IsValid(),
            "room ladder rejects invalid levels, scores, classes, text and unbounded skills");
        check(!new PlayerState{Ladder=c with{Skills=null!}}.IsValid(),"wire validation rejects malformed ladder nested payload");
        var p=new PlayerState{Id="a",Name="Account A",Ladder=c};var copy=p.Copy();c.Skills[0]="changed";
        check(copy.Ladder!.Skills[0]=="skill-id","PlayerState copy isolates ladder skill arrays");
        PlayerState P(string id,RoomLadderCharacter character)=>new(){Id=id,Name=id,Ladder=character};
        var low=P("low",Character("low") with{Level=80,Experience=999999999,Corruption=400,ArenaWave=50});
        var high=P("high",Character("high") with{Experience=13000,Corruption=100,ArenaWave=300});
        var middle=P("middle",Character("middle"));var players=new[]{low,high,middle};
        RoomLadderRow[] Select(RoomLadderMetric m)=>RoomLadder.Select(players,m,false,5);
        check(Select(RoomLadderMetric.Level).Select(r=>r.PlayerId).SequenceEqual(new[]{"high","middle","low"}),
            "level ladder orders level before within-level XP");
        check(Select(RoomLadderMetric.Corruption).Select(r=>r.PlayerId).SequenceEqual(new[]{"low","middle","high"}),
            "corruption ladder uses each character saved best corruption");
        check(Select(RoomLadderMetric.Arena)[0].PlayerId=="high" && Select(RoomLadderMetric.Arena).Select(r=>r.Rank).SequenceEqual(new long[]{1,2,3}),
            "arena ladder orders best waves with contiguous ranks");
        check(RoomLadder.Select(players,RoomLadderMetric.Level,true,5).Length==0 &&
            RoomLadder.Select(players,RoomLadderMetric.Level,false,4).Length==0 &&
            RoomLadder.Select(players,RoomLadderMetric.Level,false,5,2).Length==0 &&
            RoomLadder.Select(players,RoomLadderMetric.Level,false,5,3).Length==3,
            "room ladder respects hardcore, season and class filters");
        check(RoomLadder.Select(new[]{low,low with{Sequence=2,Ladder=low.Ladder! with{ArenaWave=500}}},RoomLadderMetric.Arena,false,5).Single().Character.ArenaWave==500,
            "repeated presence for the same connection produces only its newest row");
        check(RoomLadder.Select(new[]{middle,high with{Ladder=middle.Ladder! with{CharacterId="a"}}},RoomLadderMetric.Level,false,5)[0].PlayerId=="high",
            "equal scores have stable deterministic ordering across clients");
        var omen=new[]{low with{Ladder=low.Ladder! with{OmenKills=50}},high with{Ladder=high.Ladder! with{OmenKills=60}}};
        check(RoomLadder.Select(omen,RoomLadderMetric.Omen,false,5)[0].PlayerId=="high","Omen ranking uses reported kills");
        using var host=LanSession.Host("Host","ladder-test","room43",0);
        using var a=await LanSession.JoinAsync("127.0.0.1",host.Port,"Peer A","ladder-test","room43");
        using var b=await LanSession.JoinAsync("127.0.0.1",host.Port,"Peer B","ladder-test","room43");
        host.Publish(new(){Ladder=Character("host")});
        a.Publish(new(){Id=host.LocalId,Name="Forged",Ladder=Character("a") with{Corruption=500}});
        b.Publish(new(){Ladder=Character("b") with{Corruption=400}});
        async Task Until(Func<bool> ready)
        {
            var deadline=DateTime.UtcNow.AddSeconds(7);
            while(!ready() && DateTime.UtcNow<deadline)await Task.Delay(20);
            if(!ready())throw new TimeoutException("Room ladder TCP snapshot");
        }
        await Until(()=>a.PlayerPositions.Count(p=>p.Ladder!=null)==3 && b.PlayerPositions.Count(p=>p.Ladder!=null)==3);
        var table=RoomLadder.Select(b.PlayerPositions,RoomLadderMetric.Corruption,false,5);
        check(table.Length==3 && table[0].PlayerId==a.LocalId && table[0].AccountName=="Peer A",
            "Host+2Clients: ladder metadata broadcasts with authenticated player/account IDs while shared world is unavailable");
        a.Publish(new(){Ladder=Character("a2") with{Corruption=600}});
        await Until(()=>b.PlayerPositions.Any(p=>p.Id==a.LocalId && p.Ladder?.CharacterId=="a2"));
        check(RoomLadder.Select(b.PlayerPositions,RoomLadderMetric.Corruption,false,5).Count(r=>r.PlayerId==a.LocalId)==1,
            "character switch replaces previous ladder character without a duplicate row");
        a.Dispose();
        await Until(()=>b.PlayerPositions.All(p=>p.Id!=a.LocalId));
        check(RoomLadder.Select(b.PlayerPositions,RoomLadderMetric.Corruption,false,5).Length==2,
            "departed members disappear from room ladder");
    }
}
