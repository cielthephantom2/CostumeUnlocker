namespace LastEpochLAN.Core;

// Build-derived values before target mitigation. Host applies the native target
// rules and computes damage there; these must never be post-mitigation HP values.
public sealed record ResolvedAilment
{
    public string Operation {get;init;}="apply";
    public float HitDamage {get;init;}
    public int HitEvents {get;init;}
    public long Application {get;init;}
    public string CreatorKey {get;init;}="player";
    public long SourceRevision {get;init;}
    public StatusSourceProfile? SourceProfile {get;init;}
    public StatusValue? NativeDamage {get;init;}
    public StatusValue? Overrides {get;init;}
    public bool ExpirationCallback {get;init;}
    public bool AppliedBySpreading {get;init;}
    public float ElapsedDuration {get;init;}
    public int Id {get;init;}
    public int Ability {get;init;}=-1;
    public int OriginalAbility {get;init;}=-1;
    public int PrimaryAbility {get;init;}=-1;
    public int UseType {get;init;}
    public float Stacks {get;init;}
    public float Duration {get;init;}
    public float IncreasedEffect {get;init;}
    public float IncreasedHealing {get;init;}
    public float[] Damage {get;init;}=new float[7];
    public float[] Penetration {get;init;}=new float[7];
    public int Tags {get;init;}
    public bool IsValid()=>(Operation=="apply" && HitDamage==0 && HitEvents==0 || Operation=="hit" && Application>0 && NativeDamage==null && Overrides==null && !ExpirationCallback && float.IsFinite(HitDamage) && HitDamage is >=0 and <=100000000) &&
        CreatorKey is {Length:>0 and <=64} && Application>=0 && SourceRevision>=0 && (!ExpirationCallback || Application>0) &&
        (SourceProfile==null || SourceProfile.IsValid() && SourceProfile.Revision==SourceRevision) &&
        (NativeDamage==null || NativeDamage.IsValid()) && (Overrides==null || Overrides.IsValid()) &&
        float.IsFinite(ElapsedDuration) && ElapsedDuration is >=0 and <=86400 &&
        Id is >=0 and <=65535 && Ability is >=-1 and <=65535 && OriginalAbility is >=-1 and <=65535 &&
        PrimaryAbility is >=-1 and <=65535 && UseType is >=0 and <=255 &&
        float.IsFinite(Stacks) && Stacks is >0 and <=1000000 && float.IsFinite(Duration) && Duration is >0 and <=86400 &&
        float.IsFinite(IncreasedEffect) && IncreasedEffect is >=-1 and <=1000000 && float.IsFinite(IncreasedHealing) && IncreasedHealing is >=-1 and <=1000000 &&
        Damage is {Length:7} && Damage.All(v=>float.IsFinite(v) && v is >=0 and <=100000000) &&
        Penetration is {Length:7} && Penetration.All(v=>float.IsFinite(v) && Math.Abs(v)<=1000000);
    public ResolvedAilment Copy()=>this with {Damage=Damage.ToArray(),Penetration=Penetration.ToArray(),
        SourceProfile=SourceProfile?.Copy(),NativeDamage=NativeDamage?.Copy(),Overrides=Overrides?.Copy()};
}
public sealed record AilmentCount(int Id,float Count)
{
    public bool IsValid()=>Id is >=0 and <=65535 && float.IsFinite(Count) && Count is >0 and <=1000000;
}
