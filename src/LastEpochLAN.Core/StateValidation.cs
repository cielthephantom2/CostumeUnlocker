namespace LastEpochLAN.Core;

internal static class StateValidation
{
    internal static bool Position(float x,float y,float z)=>float.IsFinite(x) && float.IsFinite(y) && float.IsFinite(z) &&
        Math.Abs(x)<=100000 && Math.Abs(y)<=100000 && Math.Abs(z)<=100000;
    internal static bool UniqueAilments(AilmentCount[] values)
    {
        if(values.Length>16) return values.Select(v=>v.Id).Distinct().Count()==values.Length;
        for(int i=0;i<values.Length;i++) for(int j=0;j<i;j++) if(values[i].Id==values[j].Id)return false;
        return true;
    }
    internal static bool UniqueVisuals(SkillVisualState[] values)
    {
        for(int i=0;i<values.Length;i++) for(int j=0;j<i;j++) if(values[i].Id==values[j].Id)return false;
        return true;
    }
}
