namespace LastEpochLAN.Core;

// Presentation only. No damage, penetration, build or gameplay receiver data.
public sealed record StatusVisualGroup(int Id,float Count,float Remaining)
{
    public bool IsValid()=>Id is >=0 and <=65535 && float.IsFinite(Count) && Count is >0 and <=1000000 &&
        float.IsFinite(Remaining) && Remaining is >0 and <=86400;
}
public sealed record StatusVisualReport
{
    public string Source {get;init;}="";
    public long Sequence {get;init;}
    public long Epoch {get;init;}
    public int EnemyId {get;init;}
    public StatusVisualGroup[] Groups {get;init;}=Array.Empty<StatusVisualGroup>();
    public bool IsValid()=>Source is {Length:<=64} && Sequence>0 && Epoch>=0 && EnemyId>0 &&
        Groups is {Length:<=1024} && Groups.All(group=>group!=null && group.IsValid());
    public StatusVisualReport Copy()=>this with {Groups=Groups.ToArray()};
}

// Unity-thread owned ledger. Each source replaces its own complete target view;
// refresh, expiry and cleanse never remove another source's contribution.
public sealed class SourceStatusVisuals
{
    public const double LeaseSeconds=3;
    private sealed record Contribution(long Sequence,(int Id,float Count,double Until)[] Groups,double LastSeen);
    private readonly Dictionary<int,Dictionary<string,Contribution>> targets=new();
    private long epoch=-1;
    public void BeginEpoch(long value)
    {if(epoch==value) return;epoch=value;targets.Clear();}
    public bool Observe(StatusVisualReport report,double now)
    {
        if(!double.IsFinite(now) || !report.IsValid() || report.Epoch!=epoch || string.IsNullOrEmpty(report.Source)) return false;
        if(!targets.TryGetValue(report.EnemyId,out var sources)) targets[report.EnemyId]=sources=new();
        if(sources.TryGetValue(report.Source,out var old) && old.Sequence>=report.Sequence) return false;
        sources[report.Source]=new(report.Sequence,report.Groups.Select(g=>(g.Id,g.Count,now+Math.Min(g.Remaining,LeaseSeconds))).ToArray(),now);
        return true;
    }
    public AilmentCount[] Merge(int enemyId,IReadOnlyList<AilmentCount> local,double now)
    {
        if(!targets.TryGetValue(enemyId,out var sources) || !sources.Values.Any(source=>now-source.LastSeen<LeaseSeconds && source.Groups.Any(group=>group.Until>now)))
            return local as AilmentCount[] ?? local.ToArray();
        var counts=local.ToDictionary(a=>a.Id,a=>(double)a.Count);
        foreach(var source in sources.Values)
                if(now-source.LastSeen<LeaseSeconds)
                    foreach(var group in source.Groups)
                        if(group.Until>now) counts[group.Id]=counts.GetValueOrDefault(group.Id)+group.Count;
        return counts.Where(p=>p.Value>0).OrderBy(p=>p.Key).Select(p=>new AilmentCount(p.Key,(float)Math.Min(1000000,p.Value))).ToArray();
    }
    public void Retain(ISet<int> liveTargets,ISet<string> presentSources)
        =>Retain(liveTargets.Contains,presentSources);
    public void Retain(Func<int,bool> isLive,ISet<string> presentSources)
    {
        foreach(var id in targets.Keys.Where(id=>!isLive(id)).ToArray()) targets.Remove(id);
        foreach(var sources in targets.Values)
            foreach(var source in sources.Keys.Where(source=>!presentSources.Contains(source)).ToArray()) sources.Remove(source);
    }
}
