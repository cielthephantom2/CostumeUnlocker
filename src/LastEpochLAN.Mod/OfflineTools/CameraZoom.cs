using Il2Cpp;
using UnityEngine;

namespace LastEpochLAN.Mod.OfflineTools;

// Adapted from LE HUD Camera_Override: use native distance endpoints so the
// game's zoom input, interpolation and camera angle remain in control.
internal static class CameraZoom
{
    private static CameraManager current;
    private static float originalMin,originalDefault;
    private static bool overridden;
    internal static float Clamp(float value)=>float.IsFinite(value)?Math.Clamp(value,0.5f,5f):1f;

    internal static void InputTick(bool editingPanel)
    {
        if(editingPanel || !OfflineToolkit.Ready) return;
        if(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
           Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) return;
        var selected=UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
        if(selected!=null && (selected.GetComponentInParent<UnityEngine.UI.InputField>()!=null ||
                             selected.GetComponentInParent<Il2CppTMPro.TMP_InputField>()!=null)) return;
        int direction=(Input.GetKeyDown(KeyCode.Period)?1:0)-(Input.GetKeyDown(KeyCode.Comma)?1:0);
        if(direction==0) return;
        var settings=OfflineToolkit.Settings;
        settings.CustomZoom=true;
        settings.ZoomDistance=Clamp(MathF.Round((settings.ZoomDistance+direction*0.1f)*100f)/100f);
        ApplyCurrent();OfflineToolkit.ScheduleSave();
    }

    internal static void ApplyCurrent()
    {
        if(!OfflineToolkit.Ready || !OfflineToolkit.Settings.CustomZoom) {Restore();return;}
        var camera=CameraManager.instance;
        if(camera==null || camera.virtualCamera==null) return;
        if(current==null || current.Pointer!=camera.Pointer)
        {
            Restore();
            current=camera;
            originalMin=camera.zoomMin;
            originalDefault=camera.zoomDefault;
        }
        var factor=Clamp(OfflineToolkit.Settings.ZoomDistance);
        var near=originalMin*factor;
        var far=originalDefault*factor;
        if(camera.zoomMin==near && camera.zoomDefault==far) return;
        camera.zoomMin=near;
        camera.zoomDefault=far;
        overridden=true;
        camera.ApplyZoom();
    }

    internal static void Restore()
    {
        if(current!=null && overridden)
        {
            current.zoomMin=originalMin;
            current.zoomDefault=originalDefault;
            if(current.virtualCamera!=null) current.ApplyZoom();
        }
        current=null;
        overridden=false;
    }
}
