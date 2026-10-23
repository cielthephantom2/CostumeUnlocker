using HarmonyLib;
using Il2Cpp;
using LastEpochLAN.Core;
using UnityEngine;

namespace LastEpochLAN.Mod.OfflineTools.Dps;

internal static class DpsRuntime
{
    private static string error;
    internal static void Tick()
    {
        if (OfflineToolkit.Ready) DamageMeterOverlay.OnUpdate();
    }
    internal static void SceneChanged()
    {
        OfflineDamageHooks.Clear();
        DamageMeterOverlay.OnSceneChanged();
    }
    internal static void Draw()
    {
        if (!OfflineToolkit.Ready) return;
        var matrix = GUI.matrix;
        var color = GUI.color;
        bool enabled = GUI.enabled;
        try { GUI.enabled = true; DamageMeterOverlay.OnGUI(); }
        catch (Exception ex)
        {
            if (error != ex.Message) MelonLoader.MelonLogger.Warning("DPS HUD: " + ex.GetBaseException().Message);
            error = ex.Message;
        }
        finally { GUI.matrix = matrix; GUI.color = color; GUI.enabled = enabled; }
    }
    internal static bool CapturesPointer
    {
        get
        {
            if (!DamageMeterOverlay.IsShown || !OfflineToolkit.Ready) return false;
            var mouse = Input.mousePosition;
            return DamageMeterOverlay.IsManipulating || DamageMeterOverlay.LastDrawnBounds.Contains(new Vector2(mouse.x, Screen.height - mouse.y));
        }
    }
}

internal static class DpsSettings
{
    public static bool damageMeterEnabled => OfflineToolkit.Enabled;
    public static bool damageMeterVisible => OfflineToolkit.Settings.DpsVisible;
    public static bool damageMeterShowWhenIdle => true;
    public static float damageMeterScale
    {
        get => DpsRules.Scale(OfflineToolkit.Settings.DpsScale);
        set { value = DpsRules.Scale(value); if (value == OfflineToolkit.Settings.DpsScale) return; OfflineToolkit.Settings.DpsScale = value; OfflineToolkit.ScheduleSave(); }
    }
    public static float damageMeterOffsetX
    {
        get => float.IsFinite(OfflineToolkit.Settings.DpsOffsetX) ? OfflineToolkit.Settings.DpsOffsetX : 0;
        set { OfflineToolkit.Settings.DpsOffsetX = value; OfflineToolkit.ScheduleSave(); }
    }
    public static float TopOffset
    {
        get => float.IsFinite(OfflineToolkit.Settings.DpsTopOffset) ? OfflineToolkit.Settings.DpsTopOffset : 0;
        set { OfflineToolkit.Settings.DpsTopOffset = value; OfflineToolkit.ScheduleSave(); }
    }
    public static float damageMeterVerticalOffset => 0;
    public static float damageMeterOpacity => 0.84f;
    public static int damageMeterSkillRows => 8;
    public static float damageMeterRollingWindowSeconds => 5;
    public static bool damageMeterResetPerMap => true;
    public static bool damageMeterShowSkillBreakdown => true;
    public static bool damageMeterShowRecentFiveSecond => true;
    public static bool damageMeterShowBiggestHit => true;
    public static bool damageMeterShowHitCount => true;
    public static bool damageMeterShowMapLabel => false;
    public static bool damageMeterShowSessionTimer => false;
    public static bool damageMeterShowRowTooltips => true;
    public static bool damageMeterShowDamageTypeMeta => true;
    public static bool damageMeterShowSkillTagMeta => true;
    public static bool damageMeterShowCritMeta => true;
    public static bool damageMeterShowKillMeta => true;
}

internal static class DpsDrawing
{
    public static Texture2D lineTex => Texture2D.whiteTexture;
    public static GUIStyle StringStyle => GUI.skin.label;
}
// No independent menu or hotkeys from the reference DLL are imported.
internal static class DpsMenu
{
    public static bool ShouldCaptureGameInput => false;
    public static Rect windowRect => default;
}

[HarmonyPatch]
internal static class DpsPointerInputPatch
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods() =>
        new[] { "GetLeftMouseButtonHold", "GetLeftMouseButtonDown", "GetLeftMouseButtonUp", "GetInteractMoveButtonDown" }
        .Select(name => AccessTools.Method(typeof(EpochInputManager), name)).Where(method => method != null && method.ReturnType == typeof(bool));
    private static bool Prefix(ref bool __result)
    {
        if (!DpsRuntime.CapturesPointer) return true;
        __result = false; return false;
    }
}
