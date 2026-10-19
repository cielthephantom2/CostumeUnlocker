namespace LastEpochLAN.Core;

// Data only: native pointers, delegates and executable type names never cross
// the wire. The native adapter must additionally validate its fixed data schema.
public sealed record StatusValue
{
    public string Kind {get;init;}="null";
    public string Type {get;init;}="";
    public double Number {get;init;}
    public StatusField[] Fields {get;init;}=Array.Empty<StatusField>();
    public StatusValue[] Items {get;init;}=Array.Empty<StatusValue>();
    public bool IsValid(int depth=0) => depth<=8 && Type is {Length:<=220} && double.IsFinite(Number) && Math.Abs(Number)<=1e12 &&
        Fields is {Length:<=128} && Items is {Length:<=1024} &&
        (Kind switch {
            "null"=>Type=="" && Number==0 && Fields.Length==0 && Items.Length==0,
            "number"=>Type=="" && Fields.Length==0 && Items.Length==0,
            "ability" or "ailment"=>Type=="" && Number is >=0 and <=65535 && Number==Math.Truncate(Number) && Fields.Length==0 && Items.Length==0,
            "source" or "target" or "owner"=>Type=="" && Number==0 && Fields.Length==0 && Items.Length==0,
            "vector"=>Type=="" && Number==0 && Fields.Length==0 && Items.Length==3 && Items.All(v=>v!=null && v.Kind=="number" && v.IsValid(depth+1)),
            "object"=>Type.Length>0 && Number==0 && Items.Length==0 && Fields.All(f=>f!=null && f.Name is {Length:>0 and <=100} && f.Value!=null && f.Value.IsValid(depth+1)) && Fields.Select(f=>f.Name).Distinct().Count()==Fields.Length,
            "list" or "map"=>Type=="" && Number==0 && Fields.Length==0 && Items.All(v=>v!=null && v.IsValid(depth+1)) && (Kind!="map" || Items.Length%2==0),
            _=>false});
    public StatusValue Copy()=>this with {Fields=Fields.Select(f=>f with {Value=f.Value.Copy()}).ToArray(),Items=Items.Select(v=>v.Copy()).ToArray()};
}
public sealed record StatusField(string Name,StatusValue Value);
public sealed record StatusCounters(int Curses=0,int Brands=0,int Movement=0,int Negative=0)
{
    public bool IsValid()=>Curses is >=0 and <=1000000 && Brands is >=0 and <=1000000 && Movement is >=0 and <=1000000 && Negative is >=0 and <=1000000;
}

public sealed record StatusStat
{
    public int Property {get;init;}
    public int Tags {get;init;}
    public byte SpecialTag {get;init;}
    public int ExtraTag {get;init;}
    public float Added {get;init;}
    public float Increased {get;init;}
    public bool DontCollapse {get;init;}
    public float[] More {get;init;}=Array.Empty<float>();
    public bool IsValid()=>Property is >=0 and <=65535 && float.IsFinite(Added) && Math.Abs(Added)<=1e9 &&
        float.IsFinite(Increased) && Math.Abs(Increased)<=1e9 && More is {Length:<=128} && More.All(v=>float.IsFinite(v) && Math.Abs(v)<=1e9);
    public StatusStat Copy()=>this with {More=More.ToArray()};
    public bool Same(StatusStat other)=>other!=null && Property==other.Property && Tags==other.Tags && SpecialTag==other.SpecialTag &&
        ExtraTag==other.ExtraTag && Added==other.Added && Increased==other.Increased && DontCollapse==other.DontCollapse && More.SequenceEqual(other.More);
}
public sealed record StatusSourceProfile
{
    public long Revision {get;init;}
    public StatusStat[] Stats {get;init;}=Array.Empty<StatusStat>();
    public bool IsValid()=>Revision>0 && Stats is {Length:<=4096} && Stats.All(v=>v!=null && v.IsValid());
    public StatusSourceProfile Copy()=>this with {Stats=Stats.Select(v=>v.Copy()).ToArray()};
}

public sealed record StatusCallback
{
    // 0 accepted, 1 expiration callback, 2 rejected. Only Host may send these.
    public int Kind {get;init;}
    public long Epoch {get;init;}
    public long Application {get;init;}
    public VisualPoint Position {get;init;}=new();
    public bool IsValid()=>Kind is >=0 and <=2 && Epoch>=0 && Application>0 && Position!=null && Position.IsValid();
}
