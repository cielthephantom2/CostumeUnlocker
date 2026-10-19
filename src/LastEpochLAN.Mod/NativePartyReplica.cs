using LastEpochLAN.Core;
using HarmonyLib;
using UnityEngine;
using Panel = Il2CppLE.UI.Party.UnityUI.PartyOverviewPanel;
using Member = Il2CppLE.UI.Party.UnityUI.PartyOverviewMemberPanel;

namespace LastEpochLAN.Mod;

// Binds LAN membership to the game's existing party widgets, without RTM calls.
internal sealed class NativePartyReplica
{
    private static readonly HashSet<IntPtr> lanMembers = new();
    internal static bool Owns(Member member) => member != null && lanMembers.Contains(member.Pointer);
    private Panel panel;
    private Member[] members = Array.Empty<Member>();
    private readonly Dictionary<IntPtr, bool> enabledBefore = new();
    private bool activeBefore;
    private readonly Dictionary<IntPtr, (bool Active, bool Selectable)> before = new();
    private string scene = "";
    private readonly Dictionary<string, GameObject> icons = new();
    private float retry;
    private readonly Dictionary<IntPtr, (string Id, string Name, int Class, bool Leader)> boundIdentity = new();
    private readonly Dictionary<IntPtr, bool> deathState = new();
    private readonly Dictionary<IntPtr, Il2CppSystem.Action> portalActions = new();
    private readonly Dictionary<IntPtr, bool> portalVisible = new();
    private readonly Dictionary<IntPtr, bool> portalInteractable = new();
    internal string Error = "";
    internal int Bound;
    internal void Tick(LanSession session)
    {
        if (Time.unscaledTime < retry) return;
        retry = Time.unscaledTime + 0.1f;
        try
        {
            if (panel == null)
            {
                panel = Resources.FindObjectsOfTypeAll<Panel>().FirstOrDefault(candidate => candidate != null &&
                    candidate.gameObject.scene.IsValid() && candidate.gameObject.scene.isLoaded &&
                    candidate.GetComponentsInChildren<Member>(true).Length >= 3);
                if (panel == null) { Error = "Native PartyOverviewPanel has not loaded."; retry = Time.unscaledTime + 2; return; }
                activeBefore = panel.gameObject.activeSelf;
                members = panel.GetComponentsInChildren<Member>(true);
                foreach (var member in members) enabledBefore[member.Pointer] = member.enabled;
                foreach (var member in members) before[member.Pointer] = (member.IsActive,
                    member.partyOverviewSelectableAdapter?.interactable ?? false);
                foreach (var member in members)
                {
                    portalActions[member.Pointer] = member.RequestPortalButtonPressed;
                    portalVisible[member.Pointer] = member.requestPortalButton?.gameObject.activeSelf ?? false;
                    portalInteractable[member.Pointer] = member.requestPortalButton?.interactable ?? false;
                }
            }
            string currentScene = Il2Cpp.SceneList.GetCurrentSceneDetails()?.Name ?? "";
            if (scene != currentScene)
            {
                foreach (var icon in icons.Values) if (icon != null) UnityEngine.Object.Destroy(icon);
                icons.Clear(); scene = currentScene;
            }
            panel.gameObject.SetActive(true);
            var allPlayers = session.PlayerOverview;
            var players = allPlayers.Where(player => player.Id != session.LocalId).Take(members.Length).ToArray();
            var ids = players.Select(player => player.Id).ToHashSet();
            foreach (var id in icons.Keys.Where(id => !ids.Contains(id)).ToArray())
            { UnityEngine.Object.Destroy(icons[id]); icons.Remove(id); }
            for (int index = 0; index < members.Length; index++)
            {
                var member = members[index]; member.enabled = false;
                lanMembers.Add(member.Pointer);
                member.IsActive = index < players.Length;
                if (index >= players.Length) continue;
                var player = players[index];
                int classId = player.Avatar?.ClassId ?? 0;
                var signature = (player.Id, player.Name, classId, allPlayers.FirstOrDefault()?.Id == player.Id);
                if (!boundIdentity.TryGetValue(member.Pointer, out var old) || old != signature)
                {
                    var identity = new Il2CppLE.Services.Models.UserIdentity(player.Name, player.Id, player.Id, "LAN", "LAN", false);
                    member.partyMember = new Il2CppLE.Services.Models.PartyMember_Full(identity, signature.Item4,
                        new Il2CppSystem.Nullable<Il2Cpp.CharacterClassID>((Il2Cpp.CharacterClassID)classId));
                    if (member.accountNameText != null) member.accountNameText.text = player.Name;
                    if (member.leaderTransform != null) member.leaderTransform.SetActive(signature.Item4);
                    if (member.portrait != null && member.classPortraits != null && classId < member.classPortraits.Length)
                        member.portrait.sprite = member.classPortraits[classId];
                    boundIdentity[member.Pointer] = signature; deathState.Remove(member.Pointer);
                    string peerId = player.Id;
                    member.RequestPortalButtonPressed = new Action(() => LanPartyTravel.Active?.Request(peerId));
                }
                if (member.partyOverviewSelectableAdapter != null) member.partyOverviewSelectableAdapter.interactable = player.GiftIdentity != "";
                if (!deathState.TryGetValue(member.Pointer, out var dead) || dead != player.Dead)
                { if (player.Dead) member.ApplyDeadState(); else member.ApplyAliveState(); deathState[member.Pointer] = player.Dead; }
                // Native managed ticks are scheduled separately from Behaviour.enabled.
                // Apply LAN bars after native alive/dead presentation, and suppress only
                // the bound LAN members' backend PlayerActorSync update callback.
                if (member.barHolder != null) member.barHolder.SetActive(player.Available && player.Scene == currentScene && player.MaxHealth > 0);
                if (member.healthBar != null) { member.healthBar.gameObject.SetActive(true); member.healthBar.fillAmount = player.MaxHealth > 0 ? Mathf.Clamp01(player.Health / player.MaxHealth) : 0; }
                if (member.wardBar != null) member.wardBar.fillAmount = player.MaxHealth > 0 ? Mathf.Clamp01(player.Ward / player.MaxHealth) : 0;
                if (member.requestPortalButton != null)
                {
                    member.requestPortalButton.gameObject.SetActive(true);
                    member.requestPortalButton.interactable = player.Available && !player.Dead && LanPartyTravel.Active?.Busy != true;
                }
                bool sameScene = player.Available && player.Scene == Il2Cpp.SceneList.GetCurrentSceneDetails()?.Name;
                if (!icons.TryGetValue(player.Id, out var iconRoot) && panel.playerMinimapIcons != null && index < panel.playerMinimapIcons.Count)
                {
                    var template = Il2Cpp.PlayerFinder.getPlayerActor()?.actorSync?.actorVisuals?.minimapIcon;
                    if (template != null)
                    {
                        iconRoot = new GameObject("LAN native party map icon " + player.Id); iconRoot.SetActive(false);
                        var icon = iconRoot.AddComponent<Il2CppDMM.DMMapIcon>();
                        icon.icon = icon.sprite = panel.playerMinimapIcons[index];
                        icon.tint = template.tint; icon.layer = template.layer;
                        icon.scaleMultiplier = template.scaleMultiplier; icon.scaleWithZoom = template.scaleWithZoom;
                        icon.initialiseOnStart = true;
                        icons[player.Id] = iconRoot;
                    }
                }
                if (iconRoot != null)
                {
                    iconRoot.transform.position = new Vector3(player.X, player.Y, player.Z);
                    iconRoot.SetActive(sameScene && !player.Dead);
                }
            }
            Bound = players.Length; Error = "";
        }
        catch (Exception ex) { Error = ex.ToString(); }
    }
    internal void Reset()
    {
        foreach (var member in members) if (member != null) lanMembers.Remove(member.Pointer);
        foreach (var icon in icons.Values) if (icon != null) UnityEngine.Object.Destroy(icon);
        icons.Clear();
        foreach (var member in members)
            if (member != null)
            {
                member.IsActive = before.GetValueOrDefault(member.Pointer).Active;
                member.enabled = enabledBefore.GetValueOrDefault(member.Pointer);
                member.RequestPortalButtonPressed = portalActions.GetValueOrDefault(member.Pointer);
                if (member.requestPortalButton != null)
                {
                    member.requestPortalButton.gameObject.SetActive(portalVisible.GetValueOrDefault(member.Pointer));
                    member.requestPortalButton.interactable = portalInteractable.GetValueOrDefault(member.Pointer);
                }
                if (member.partyOverviewSelectableAdapter != null)
                    member.partyOverviewSelectableAdapter.interactable = before.GetValueOrDefault(member.Pointer).Selectable;
            }
        if (panel != null) panel.gameObject.SetActive(activeBefore);
        panel = null; members = Array.Empty<Member>(); enabledBefore.Clear(); before.Clear(); boundIdentity.Clear(); deathState.Clear();
        portalActions.Clear(); portalVisible.Clear(); portalInteractable.Clear(); retry = 0; Bound = 0;
    }
}

[HarmonyPatch(typeof(Member), nameof(Member.OnUpdateTick))]
internal static class LanPartyHealthTickPatch
{
    private static bool Prefix(Member __instance) => !NativePartyReplica.Owns(__instance);
}
