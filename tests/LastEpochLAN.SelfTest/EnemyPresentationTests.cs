using System.Net;
using LastEpochLAN.Core;

internal static class EnemyPresentationTests
{
    private static EnemyState Enemy(int id,float x=0,long attack=0)=>new()
    {Id=id,Template="npc",X=x,Health=100,MaxHealth=100,AttackSequence=attack};
    internal static async Task Run(Action<bool,string> check)
    {
        var timeline=new BufferedEnemyPresentation();
        timeline.Push(100,Enemy(1));
        check(timeline.TryTake(99.9,out var baseline) && baseline.AttackSequence==0,
            "newly admitted NPC receives a baseline without waiting in an uninitialized animation");
        timeline.Push(100.1,Enemy(1,1,1));
        check(!timeline.TryTake(100.099,out _),"NPC attack waits for the same host-time cursor as its moving pose");
        check(timeline.TryTake(100.1,out var attack) && attack.AttackSequence==1 && attack.X==1,
            "NPC pose and attack transition share their snapshot boundary");
        check(!timeline.TryTake(100.15,out _) && !timeline.Push(100.1,attack),
            "render frames and repeated Apply retries do not restart the same NPC attack");
        check(!timeline.Push(99,attack) && !timeline.Push(double.NaN,attack) && !timeline.TryTake(double.NaN,out _),
            "stale and invalid presentation times cannot rewind the NPC state");
        for(int i=2;i<30;i++) timeline.Push(100+i*0.1,Enemy(1,i,i));
        check(timeline.Count<=8 && timeline.TryTake(200,out var latest) && latest.AttackSequence==29 && !timeline.TryTake(201,out _),
            "long render hitch retains bounded visual work and coalesces obsolete attack states once");

        var stream=new BufferedEnemyPresentation();
        bool aligned=true;int transitions=0;
        for(int frame=0;frame<=120;frame++)
        {
            double now=frame/60d;
            if(frame%6==0) stream.Push(100+now,Enemy(1,frame/6,frame/6));
            if(stream.TryTake(100+now-0.1,out var state))
            {aligned &= frame==0 || state.AttackSequence<=Math.Floor((now-0.1+1e-8)*10);transitions++;}
        }
        check(aligned && transitions>=18 && transitions<=21,
            "60Hz presentation consumes 10Hz action states on the delayed pose timeline instead of packet arrival");
        var replacement=new BufferedEnemyPresentation();
        replacement.Push(200,Enemy(2));
        check(replacement.TryTake(200,out var next) && next.Id==2 && next.AttackSequence==0,
            "new scene presentation starts from a fresh baseline without replaying old actor actions");
        var crowd=Enumerable.Range(1,2400).Select(id=>Enemy(id,2401-id)).ToList();
        crowd.Sort((a,b)=>EnemyAdmission.Compare(a,b,0,0,0));
        check(crowd[0].X==1 && crowd[1].X==2 && crowd.Select(e=>e.Id).Distinct().Count()==2400,
            "2400-NPC admission selects the nearest two while retaining every host identity");
        crowd.Sort((a,b)=>EnemyAdmission.Compare(a,b,2400,0,0));
        check(crowd[0].X==2400 && crowd[1].X==2399,
            "NPC admission follows the actual Guest location at the opposite end of the map");
        check(EnemyAdmission.Compare(Enemy(2,1) with {Boss=true},Enemy(1,-1),0,0,0)<0,
            "equally distant NPC admission prefers the boss with stable identity tie breaking");

        using var host=LanSession.Host("PresentationHost","npc-presentation","private-fixture",0,IPAddress.Loopback);
        var world=new WorldState {Epoch=1,Revision=1,Scene="npc-fixture",SampleTime=100,Enemies=new[]{Enemy(1)}};
        host.PublishWorld(world);
        using var guest=await LanSession.JoinAsync("127.0.0.1",host.Port,"PresentationGuest","npc-presentation","private-fixture");
        async Task WaitRevision(long revision)
        {
            long deadline=Environment.TickCount64+5000;
            while(guest.WorldInfo.Revision<revision && Environment.TickCount64<deadline) await Task.Delay(10);
            if(guest.WorldInfo.Revision<revision) throw new Exception("NPC fixture world delivery timed out.");
        }
        await WaitRevision(1);
        var wire=new BufferedEnemyPresentation();var received=guest.World;
        wire.Push(received.SampleTime,received.Enemies[0]);wire.TryTake(100,out _);
        host.PublishWorld(world with {Revision=2,SampleTime=100.1,Enemies=new[]{Enemy(1,1,7)}});
        await WaitRevision(2);received=guest.World;
        wire.Push(received.SampleTime,received.Enemies[0]);
        check(!wire.TryTake(100.05,out _) && wire.TryTake(100.1,out var wireAttack) && wireAttack.AttackSequence==7 && wireAttack.X==1,
            "actual TCP baseline and delta retain NPC action and pose for aligned render consumption");
    }
}
