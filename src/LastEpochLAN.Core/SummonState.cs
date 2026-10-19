using System.Numerics;

namespace LastEpochLAN.Core;

public sealed record SummonState
{
    public const int MaximumPerPlayer=64;
    public int Id {get;init;}
    public string Template {get;init;}="";
    public CosmeticPrefab Prefab {get;init;}=new();
    public VisualPoint Position {get;init;}=new();
    public VisualPoint Scale {get;init;}=new() {X=1,Y=1,Z=1};
    public float Yaw {get;init;}
    public bool Visible {get;init;}=true;
    public NativeMotionState? Motion {get;init;}
    public AnimationSnapshot? Animation {get;init;}
    public long AttackSequence {get;init;}
    public byte AttackAnimation {get;init;}
    public float AttackSpeed {get;init;}=1;
    public bool IsValid()=>Id>0 && Template is {Length:<=128} && Prefab!=null && Prefab.IsValid() &&
        Position!=null && Position.IsValid() && Scale!=null && Scale.IsValid() &&
        Math.Abs(Scale.X)<=100 && Math.Abs(Scale.Y)<=100 && Math.Abs(Scale.Z)<=100 &&
        float.IsFinite(Yaw) && Math.Abs(Yaw)<=360 && (Motion==null || Motion.IsValid()) &&
        (Animation==null || Animation.IsValid() && Animation.Parameters.Length<=24) &&
        AttackSequence>=0 && AttackAnimation<=63 && float.IsFinite(AttackSpeed) && AttackSpeed is >0 and <=100;
    public SummonState Copy()=>this with {Animation=Animation?.Copy()};
}

public readonly record struct SummonFrame(MotionSample Pose,SummonState State);

// Presentation-only lifetime and timeline. The player packet's authenticated
// owner remains the authority; these entities never become gameplay Actors.
public sealed class SummonPresentation
{
    private sealed class Entry
    {
        internal readonly BufferedMotion Motion=new();
        internal readonly List<(double Time,SummonState State)> Pending=new();
        internal SummonState Latest=null!,Shown=null!;
    }
    private readonly Dictionary<int,Entry> entries=new();
    private readonly SnapshotPresentationClock clock=new(true);
    private string epoch="";
    private double latest=-1;
    public IEnumerable<SummonState> States=>entries.Values.Select(e=>e.Latest);
    public bool Receive(string generation,double sampleTime,SummonState[] states,double arrivalTime)
    {
        if(!Guid.TryParseExact(generation,"N",out _) || !double.IsFinite(sampleTime) || sampleTime<0 || sampleTime<=latest ||
            !double.IsFinite(arrivalTime) || arrivalTime<0 || states==null || states.Length>SummonState.MaximumPerPlayer ||
            states.Any(s=>s==null || !s.IsValid()) || states.Select(s=>s.Id).Distinct().Count()!=states.Length)return false;
        if(epoch!=generation){entries.Clear();clock.Clear();epoch=generation;}
        latest=sampleTime;clock.Observe(sampleTime,arrivalTime);
        var ids=states.Select(s=>s.Id).ToHashSet();
        foreach(var id in entries.Keys.Where(id=>!ids.Contains(id)).ToArray())entries.Remove(id);
        foreach(var state in states)
        {
            if(!entries.TryGetValue(state.Id,out var entry))entries.Add(state.Id,entry=new());
            var p=new Vector3(state.Position.X,state.Position.Y,state.Position.Z);
            bool reset=entry.Latest!=null && (entry.Latest.Prefab!=state.Prefab ||
                Vector3.DistanceSquared(p,new(entry.Latest.Position.X,entry.Latest.Position.Y,entry.Latest.Position.Z))>400);
            if(reset){entry.Motion.Clear();entry.Pending.Clear();entry.Shown=null!;}
            entry.Latest=state.Copy();entry.Motion.Push(new(sampleTime,p,state.Yaw));
            if(entry.Pending.Count==8)entry.Pending.RemoveAt(0);
            entry.Pending.Add((sampleTime,entry.Latest));
        }
        return true;
    }
    public double RenderTime(double now)=>clock.RenderTime(now);
    public bool TrySample(int id,double cursor,out SummonFrame frame)
    {
        frame=default;
        if(!double.IsFinite(cursor) || !entries.TryGetValue(id,out var entry))return false;
        int due=0;while(due<entry.Pending.Count && entry.Pending[due].Time<=cursor)due++;
        if(entry.Shown==null && entry.Pending.Count>0 && due==0)due=1;
        if(due>0){entry.Shown=entry.Pending[due-1].State;entry.Pending.RemoveRange(0,due);}
        if(entry.Shown==null || entry.Motion.Count==0)return false;
        frame=new(entry.Motion.Sample(cursor),entry.Shown);return true;
    }
}
