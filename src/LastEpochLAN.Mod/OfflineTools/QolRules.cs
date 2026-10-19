namespace LastEpochLAN.Mod.OfflineTools;

internal static class QolRules
{
    internal static float ClampPickupRadius(float radius)=>float.IsFinite(radius)?Math.Clamp(radius,0.5f,10f):1.5f;
    internal static float RevealRadius(float x,float z)
    {var diagonal=MathF.Sqrt(x*x+z*z);return float.IsFinite(diagonal)&&diagonal>0?Math.Clamp(diagonal+256,1000,10000):10000;}
}
