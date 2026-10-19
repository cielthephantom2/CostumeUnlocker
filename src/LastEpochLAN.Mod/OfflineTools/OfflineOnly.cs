using HarmonyLib;
using Il2Cpp;
using Il2CppLE.UI.Login.UnityUI;

namespace LastEpochLAN.Mod.OfflineTools;

internal static class OfflineOnly
{
    internal static LandingZonePanel Landing;
    internal static OnlineOfflineSwitch Switch;
    internal static void Tick()
    {
        if(Landing!=null && Landing.isActiveAndEnabled && Landing.playOnlineButton!=null)
            Landing.playOnlineButton.gameObject.SetActive(false);
        if(Switch!=null && Switch.isActiveAndEnabled && Switch.characterSelect!=null && !Switch.characterSelect.isOnlineTabShowing)
            Switch.gameObject.SetActive(false);
    }
}
[HarmonyPatch(typeof(LandingZonePanel),"OnOnEnable")]
internal static class OfflineLandingPatch
{private static void Postfix(LandingZonePanel __instance)=>OfflineOnly.Landing=__instance;}
[HarmonyPatch(typeof(LandingZonePanel),"OnPlayOnlineClicked")]
internal static class BlockOnlinePlayPatch
{private static bool Prefix()=>false;}
[HarmonyPatch(typeof(OnlineOfflineSwitch),"Awake")]
internal static class OfflineModeSwitchPatch
{private static void Postfix(OnlineOfflineSwitch __instance)=>OfflineOnly.Switch=__instance;}
[HarmonyPatch(typeof(CharacterSelect),"SetIsOnlineTabShowing")]
internal static class OfflineCharacterTabPatch
{private static bool Prefix(bool playOnline)=>!playOnline;}
[HarmonyPatch(typeof(OnlineOfflineSwitch),"OnButtonPressed")]
internal static class BlockOnlineSwitchPatch
{private static bool Prefix(OnlineOfflineSwitch __instance)=>__instance.characterSelect?.isOnlineTabShowing==true;}
[HarmonyPatch(typeof(CharacterSelect),"SwitchOnlineOffline")]
internal static class BlockCharacterOnlineSwitchPatch
{private static bool Prefix(CharacterSelect __instance)=>__instance.isOnlineTabShowing;}
