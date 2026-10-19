using LastEpochLAN.Core;

internal static class SourceStatusVisualsTests
{
    internal static void Run(Action<bool,string> check)
    {
        var ledger=new SourceStatusVisuals();ledger.BeginEpoch(7);
        StatusVisualReport Report(string source,long sequence,params StatusVisualGroup[] groups)=>
            new() {Source=source,Sequence=sequence,Epoch=7,EnemyId=42,Groups=groups};
        var fire=new StatusVisualGroup(1,3,1);
        var poison=new StatusVisualGroup(2,5,2);
        check(ledger.Observe(Report("A",1,fire),10) && ledger.Observe(Report("B",1,poison),10),"independent fire and poison sources accepted");
        var merged=ledger.Merge(42,new[]{new AilmentCount(3,2)},10.1);
        check(merged.SequenceEqual(new[]{new AilmentCount(1,3),new AilmentCount(2,5),new AilmentCount(3,2)}),"host local effect and both guest effect colours coexist");
        check(!ledger.Observe(Report("A",1,fire),10.2) && ledger.Merge(42,Array.Empty<AilmentCount>(),10.2).First().Count==3,
            "repeated snapshot is not additive and cannot extend its original deadline");
        check(ledger.Merge(42,Array.Empty<AilmentCount>(),11.1).SequenceEqual(new[]{new AilmentCount(2,5)}),"fire expires without removing longer poison");
        check(ledger.Observe(Report("B",2),11.2) && ledger.Merge(42,Array.Empty<AilmentCount>(),11.2).Length==0,"empty source snapshot cleanses its own effects");
        ledger.Observe(Report("A",2,new StatusVisualGroup(1,2,1),new StatusVisualGroup(1,4,2)),20);
        ledger.Observe(Report("B",3,new StatusVisualGroup(1,5,2)),20);
        check(ledger.Merge(42,Array.Empty<AilmentCount>(),20.1).Single().Count==11,"same ailment sums visual stacks from distinct creators and deadlines");
        check(ledger.Merge(42,Array.Empty<AilmentCount>(),21.1).Single().Count==9,"one stack group expires without deleting the same-source or other-source groups");
        ledger.Observe(Report("A",3),21.2);
        check(ledger.Merge(42,Array.Empty<AilmentCount>(),21.3).Single().Count==5,"source-specific cleanse leaves the other player's stacks");
        ledger.Observe(Report("B",4,new StatusVisualGroup(2,7,30)),22);
        check(ledger.Merge(42,Array.Empty<AilmentCount>(),25.1).Length==0,"lost reporting expires by bounded lease instead of leaving permanent visual effects");
        ledger.Observe(Report("B",5,new StatusVisualGroup(2,7,30)),26);
        ledger.Retain(new HashSet<int>{42},new HashSet<string>{"A"});
        check(ledger.Merge(42,Array.Empty<AilmentCount>(),26.1).Length==0,"disconnect or different map removes that player's visual contribution");
        ledger.Observe(Report("A",4,new StatusVisualGroup(1,1,2)),27);
        ledger.Retain(new HashSet<int>(),new HashSet<string>{"A"});
        check(ledger.Merge(42,Array.Empty<AilmentCount>(),27.1).Length==0,"despawn/death removes target presentation state");
        ledger.BeginEpoch(8);
        check(!ledger.Observe(Report("A",5,fire),30) && ledger.Merge(42,Array.Empty<AilmentCount>(),30).Length==0,"same-ID monster in new instance cannot inherit previous map status");
        check(!Report("A",1,new StatusVisualGroup(1,1,float.NaN)).IsValid() && !Report("A",1,new StatusVisualGroup(1,-1,2)).IsValid() &&
            !Report("A",1,new StatusVisualGroup(1,1,86401)).IsValid(),"invalid counts or durations rejected");
        var mutable=new[]{fire};var copied=Report("A",6,mutable).Copy();mutable[0]=poison;
        check(copied.Groups[0]==fire,"queued visual report isolates caller-owned arrays");
        ledger.Observe((Report("A",7,new StatusVisualGroup(1,1,1)) with {Epoch=8}),40);
        ledger.Observe((Report("A",8,new StatusVisualGroup(1,1,2)) with {Epoch=8}),40.5);
        check(ledger.Merge(42,Array.Empty<AilmentCount>(),41.2).Single().Count==1,"refresh replaces the deadline without multiplying stack count");
        check(!ledger.Observe((Report("A",7,new StatusVisualGroup(1,9,30)) with {Epoch=8}),41.3),"reordered report cannot resurrect cleared or replaced contribution");
        var baseline=new WorldState {Epoch=8,Revision=1,Scene="status",Enemies=new[]{new EnemyState {Id=42,Template="target",Health=100,MaxHealth=100,Ailments=ledger.Merge(42,Array.Empty<AilmentCount>(),41.4)}}};
        var writer=new WorldChunkWriter();var reader=new WorldChunkReader();WorldState? received=null;
        foreach(var chunk in writer.Encode(baseline,true)) received=reader.Accept(chunk) ?? received;
        check(received?.Enemies[0].Ailments.Single().Count==1 && received.Enemies[0].Health==100,"late join receives combined presentation without a second damage application");
    }
}
