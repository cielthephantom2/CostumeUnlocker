using System.Globalization;
using System.Numerics;

namespace LastEpochLAN.Mod.OfflineTools.WorldAssist;

internal enum TargetKind { Shrine, BloodCrystal, Nemesis, RiftBeastNest, RunePrison, OmenWindow, Container, Chest, Cache, TombEntrance, TimeRift, Quest, Transition, Other }
internal enum HazardShape { Ring, Cone, Projectile }
internal readonly record struct HazardSpec(HazardShape Shape, float Radius, float Angle=0, float Length=0);
internal sealed class TargetStyle
{
    public bool World {get;set;}=true;
    public bool Arrow {get;set;}=true;
    public bool Map {get;set;}=true;
    public bool Minimap {get;set;}=true;
    public bool Path {get;set;}=true;
    public string Colour {get;set;}="#FFFFFF";
}
internal sealed class WorldAssistSettings
{
    public bool Enabled {get;set;}=true;
    public bool DangerAreas {get;set;}=true;
    public bool Targets {get;set;}=true;
    public bool Smooth {get;set;}=true;
    public bool WorldPath {get;set;}=true;
    public bool MapPath {get;set;}=true;
    public bool MinimapPath {get;set;}=true;
    public bool HideCompleted {get;set;}=true;
    public float Range {get;set;}=500;
    public float ProjectileRadius {get;set;}=0.75f;
    public float AreaRadius {get;set;}=2;
    public float ProjectileLength {get;set;}=10;
    public string AbilityRadii {get;set;}="Flaming Ground=4";
    public TargetStyle[] Styles {get;set;}=AssistRules.DefaultStyles();
    internal void Normalize()
    {
        Range=AssistRules.Bounded(Range,500,20,1000);
        ProjectileRadius=AssistRules.Bounded(ProjectileRadius,.75f,.1f,40);
        AreaRadius=AssistRules.Bounded(AreaRadius,2,.1f,40);
        ProjectileLength=AssistRules.Bounded(ProjectileLength,10,.1f,80);
        var defaults=AssistRules.DefaultStyles();
        if(Styles==null || Styles.Length!=14) Styles=defaults;
        for(int i=0;i<14;i++) Styles[i]??=defaults[i];
        AbilityRadii??="";
    }
}
internal static class AssistRules
{
    internal static readonly string[] Labels={"Shrine","Blood Crystal","Nemesis","Rift Beast Nest","Rune Prison","Omen Window","Container","Chest","Cache","Tomb Entrance","Time Rift","Quest Target","Transition","Other"};
    private static readonly string[] Colours={"#FFC830","#E03030","#66CCFF","#B4E632","#3CDC5A","#A864F0","#A0703C","#FF8C1A","#30D5C8","#9A9A9A","#20E0E0","#FFE640","#80808C","#FFFFFF"};
    internal static TargetStyle[] DefaultStyles()=>Colours.Select((c,i)=>new TargetStyle
    {Colour=c,Arrow=i is 1 or 2 or 11,Map=i!=13,Path=i is not (6 or 13)}).ToArray();
    internal static float Bounded(float value,float fallback,float min,float max)=>float.IsFinite(value)?Math.Clamp(value,min,max):fallback;
    // Specific types precede generic chest/rift/transition names. Keys and prefab
    // names supplement localized display names, so English UI is not required.
    internal static TargetKind Classify(string text,bool transition=false,bool quest=false)
    {
        string n=(text??"").ToLowerInvariant().Replace('_',' ');
        if(Has(n,"blood crystal","blood-filled crystal","bloodfilledcrystal","bloodcrystal")) return TargetKind.BloodCrystal;
        if(Has(n,"nemesis","morditas")) return TargetKind.Nemesis;
        if(Has(n,"timebeastegg","rift beast nest","time beast egg")) return TargetKind.RiftBeastNest;
        if(Has(n,"runeprison","rune prison")) return TargetKind.RunePrison;
        if(Has(n,"observerwindow","omen window","observer window")) return TargetKind.OmenWindow;
        if(Has(n,"shrine")) return TargetKind.Shrine;
        if(Has(n,"tomb entrance","tombentrance","opentombentrance","cemetery entrance")) return TargetKind.TombEntrance;
        if(Has(n,"timebeastrift","torn rift","time rift","mysterious rift","icon rift")) return TargetKind.TimeRift;
        if(Has(n,"cache","eternity")) return TargetKind.Cache;
        if(Has(n,"suitcase","sturdy","standard chest","breakable","container")) return TargetKind.Container;
        if(Has(n,"chest")) return TargetKind.Chest;
        if(quest || Has(n,"questtarget","quest target","questobjective","quest objective","queststart","mainquest","main quest","sidequest","side quest","quest haupt","quest neben")) return TargetKind.Quest;
        if(transition || Has(n,"transition","portal","waypoint","tomb exit","icon weaver")) return TargetKind.Transition;
        return TargetKind.Other;
    }
    private static bool Has(string text,params string[] values)=>values.Any(text.Contains);
    internal static Dictionary<string,HazardSpec> ParseRadii(string text,out int invalid)
    {
        var result=new Dictionary<string,HazardSpec>(StringComparer.OrdinalIgnoreCase);invalid=0;
        foreach(string row in (text??"").Split(new[]{';','\n','\r'},StringSplitOptions.RemoveEmptyEntries))
        {
            int split=row.IndexOf('=');
            if(split<=0){invalid++;continue;}
            string name=row[..split].Trim(), value=row[(split+1)..].Trim();
            var parts=value.Split(':');
            bool Number(string s,float max,out float v)=>float.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out v) && float.IsFinite(v) && v>0 && v<=max;
            HazardSpec spec;
            if(parts.Length==1 && Number(value,40,out var radius)) spec=new(HazardShape.Ring,radius);
            else if(parts.Length==3 && (parts[0].Equals("cone",StringComparison.OrdinalIgnoreCase) || parts[0].Equals("kegel",StringComparison.OrdinalIgnoreCase)) && Number(parts[1],360,out var angle) && Number(parts[2],80,out var length)) spec=new(HazardShape.Cone,length,angle,length);
            else if(parts.Length==3 && parts[0].Equals("projectile",StringComparison.OrdinalIgnoreCase) && Number(parts[1],40,out radius) && Number(parts[2],80,out length)) spec=new(HazardShape.Projectile,radius,0,length);
            else {invalid++;continue;}
            result[name]=spec;
        }
        return result;
    }
    // Extrapolate only a small gap after the latest visual sample. Large jumps
    // reset velocity; stale snapshots never drift indefinitely after unloading.
    internal static Vector3 Predict(Vector3 position,Vector3 velocity,double sampledAt,double now,bool smooth)
    {
        if(!smooth || !Finite(position) || !Finite(velocity)) return position;
        double age=now-sampledAt;
        return age>=0 && age<=.1 ? position+velocity*(float)Math.Min(age,.04) : position;
    }
    internal static Vector3 Velocity(Vector3 before,Vector3 after,double dt)
    {
        if(dt<.001 || dt>.2 || !Finite(before) || !Finite(after) || Vector3.DistanceSquared(before,after)>144) return Vector3.Zero;
        var v=(after-before)/(float)dt;
        return v.LengthSquared()<=40000?v:Vector3.Zero;
    }
    internal static bool Finite(Vector3 p)=>float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z);
    // Trim already walked corners; never draw a straight shortcut through walls.
    internal static int TailStart(Vector3 player,IReadOnlyList<Vector3> corners)
    {
        if(corners.Count<2)return 0;
        float best=float.MaxValue;int end=1;
        for(int i=1;i<corners.Count;i++)
        {
            var a=corners[i-1];var delta=corners[i]-a;float length=delta.LengthSquared();
            float t=length>.0001f?Math.Clamp(Vector3.Dot(player-a,delta)/length,0,1):0;
            float distance=Vector3.DistanceSquared(player,a+delta*t);
            if(distance<best){best=distance;end=i;}
        }
        return end;
    }
    // Clip screen-space map paths so off-map segments never cross other HUDs.
    internal static bool Clip(ref Vector2 a,ref Vector2 b,float left,float top,float right,float bottom)
    {
        if(!float.IsFinite(a.X+a.Y+b.X+b.Y) || right<=left || bottom<=top)return false;
        var d=b-a;float lo=0,hi=1;
        bool Edge(float p,float q)
        {
            if(MathF.Abs(p)<.00001f)return q>=0;
            float r=q/p;
            if(p<0){if(r>hi)return false;lo=Math.Max(lo,r);}else{if(r<lo)return false;hi=Math.Min(hi,r);}return true;
        }
        if(!Edge(-d.X,a.X-left) || !Edge(d.X,right-a.X) || !Edge(-d.Y,a.Y-top) || !Edge(d.Y,bottom-a.Y))return false;
        b=a+d*hi;a+=d*lo;return true;
    }
}
