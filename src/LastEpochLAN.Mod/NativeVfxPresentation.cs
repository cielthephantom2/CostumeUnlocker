using UnityEngine;

namespace LastEpochLAN.Mod;

// Retain visual material/texture/scale controllers; no actor, collision,
// damage, projectile movement or arbitrary event scripts are allowed here.
internal static class NativeVfxPresentation
{
    internal static bool Keep(MonoBehaviour script)
    {
        var scalar = script.TryCast<Il2Cpp.RFX4_ShaderFloatCurve>();
        if (scalar != null) { scalar.UseSharedMaterial = false; return true; }
        var color = script.TryCast<Il2Cpp.RFX4_ShaderColorGradient>();
        if (color != null) { color.UseSharedMaterial = false; return true; }
        return script.TryCast<Il2Cpp.RFX4_UVAnimation>() != null ||
            script.TryCast<Il2Cpp.RFX4_UVScroll>() != null ||
            script.TryCast<Il2Cpp.RFX4_ScaleCurves>() != null ||
            script.TryCast<Il2Cpp.RFX4_MaterialQueue>() != null ||
            script.TryCast<Il2Cpp.RFX4_LightCurves>() != null;
    }
}
