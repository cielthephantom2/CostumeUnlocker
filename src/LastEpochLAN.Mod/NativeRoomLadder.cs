using HarmonyLib;
using Il2Cpp;
using Il2CppLE.Services.Ladders;
using LastEpochLAN.Core;
using UnityEngine;
using NativeList = Il2CppSystem.Collections.Generic.List<Il2CppLE.Services.Ladders.RankedItem>;

namespace LastEpochLAN.Mod;

internal static class NativeRoomLadder
{
    internal static LanSession Room;
    internal static string Error = "";
    internal static bool Enabled => OfflineTools.OfflineToolkit.Enabled;
    private static LadderUIManager view;
    private static Il2CppSystem.Action changed;
    private static UnityEngine.Events.UnityAction<float> scrolled;
    private static float nextRefresh;
    private static string signature = "";
    internal static RoomLadderCharacter Capture()
    {
        if (!Enabled || Il2CppLE.UI.LoadingScreen.IsVisible) return null;
        var actor = PlayerFinder.getPlayerActor();
        var data = actor?.characterDataTracker?.charData;
        if (data == null || !actor.HasMovedToPlayerSpawn) return null;
        try
        {
            var skills = LadderUtils.BuildSpecializedSkillList(data);
            var skillIds = new List<string>();
            if (skills != null) foreach (var id in skills) if (!string.IsNullOrEmpty(id)) skillIds.Add(id);
            var characterClass = CharacterClassList.getClass(data.CharacterClass);
            string mastery = "";
            if (data.ChosenMastery > 0 && characterClass?.masteries != null && data.ChosenMastery <= characterClass.masteries.Length)
                mastery = characterClass.masteries[data.ChosenMastery - 1]?.name ?? "";
            var result = new RoomLadderCharacter {
                CharacterId = data.Id, CharacterName = data.CharacterName, Class = data.CharacterClass,
                Mastery = mastery, Level = actor.experienceTracker?.CurrentLevel ?? data.Level,
                Experience = actor.experienceTracker?.CurrentExperience ?? data.CurrentExp,
                ArenaWave = data.MaxWave, Corruption = data.MaxCorruption, OmenKills = data.OmensKilled,
                OmenWindows = data.OmenWindowsCompleted, Hardcore = data.Hardcore, Deaths = data.Deaths,
                Cycle = (int)data.Cycle, Skills = skillIds.Take(5).ToArray()};
            return result.IsValid() ? result : null;
        }
        catch (Exception ex) { Fail(ex); return null; }
    }
    private static void Fail(Exception ex)
    {
        string message = ex.GetBaseException().Message;
        if (Error != message) MelonLoader.MelonLogger.Warning("Room ladder: " + message);
        Error = message;
    }
    internal static void Open(LadderUIManager manager)
    {
        if (!Enabled) return;
        if (view != null) Close(view);
        view = manager;
        try
        {
            if (manager.activableElements != null) foreach (var element in manager.activableElements)
                if (element != null) element.gameObject.SetActive(true);
            manager.disabledGroup?.gameObject.SetActive(false);
            // HandleSeasonEventUI is a shared native ret stub on this build:
            // never detour it. Configure only this concrete Ladder view here.
            manager.seasonEventMessageTab?.gameObject.SetActive(false);
            manager.seasonEventTabButton?.gameObject.SetActive(true);
            manager.seasonEventBanner?.gameObject.SetActive(false);
            // Offline OnEnable skips these native subscriptions. Keep our one owned delegate.
            changed = (Il2CppSystem.Action)(Action)(() => Refresh(true));
            manager.filterPanel?.add_LadderSelectionChanged(changed);
            Localization.add_LocaleChanged(changed);
            scrolled = (UnityEngine.Events.UnityAction<float>)(Action<float>)(value => manager.OnScrollBarChange(value));
            manager.scrollBar?.onValueChanged.AddListener(scrolled);
            var local = Capture();
            if (local != null && !manager.firstTimeInitializationCompleted)
                manager.filterPanel?.Set(new LadderFilter(LadderType.Arena,
                    local.Hardcore ? LadderMode.Hardcore : LadderMode.Softcore, LadderGroup.NoParty,
                    (Il2CppLE.Data.Cycle)local.Cycle, LadderClassFilter.All));
            manager.firstTimeInitializationCompleted = true;
            Refresh(true);
        }
        catch (Exception ex) { Fail(ex); }
    }
    internal static void Close(LadderUIManager manager)
    {
        if (view == null || manager == null || view.Pointer != manager.Pointer) return;
        if (changed != null && view.filterPanel != null) view.filterPanel.remove_LadderSelectionChanged(changed);
        if (changed != null) Localization.remove_LocaleChanged(changed);
        if (scrolled != null) view.scrollBar?.onValueChanged.RemoveListener(scrolled);
        scrolled = null;
        changed = null; view = null; signature = ""; nextRefresh = 0;
    }
    internal static void Tick(LanSession room)
    {
        Room = room?.Connected == true ? room : null;
        if (!Enabled || view == null || !view.isActiveAndEnabled || Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 1;
        try { Refresh(false); } catch (Exception ex) { Fail(ex); }
    }
    private static PlayerState[] Members()
    {
        var local = Capture();
        var room = Room;
        if (room == null) return local == null ? Array.Empty<PlayerState>() : new[] {
            new PlayerState {Id = "local", Name = local.CharacterName, Ladder = local}};
        // Local capture is immediate, while peers arrive in the authenticated room snapshot.
        var members = room.PlayerPositions.Where(p => p.Id != room.LocalId).ToList();
        if (local != null) members.Add(new PlayerState {Id = room.LocalId, Name = local.CharacterName, Ladder = local});
        return members.ToArray();
    }
    internal static LadderSession CreateSession(LadderFilter filter)
    {
        var session = new LadderSession(filter);
        var records = new NativeList();
        bool party = filter.Group is LadderGroup.PartyOf2 or LadderGroup.PartyOf3 or LadderGroup.PartyOf4;
        var rows = party ? Array.Empty<RoomLadderRow>() : RoomLadder.Select(Members(),
            (RoomLadderMetric)(int)filter.Type, filter.Mode == LadderMode.Hardcore, (int)filter.Cycle,
            filter.ClassFilter == LadderClassFilter.All ? -1 : (int)filter.ClassFilter - 1);
        foreach (var row in rows)
        {
            var native = Convert(row, filter.Type);
            records.Add(native);
            if (row.PlayerId == (Room?.LocalId ?? "local")) session.PlayerRecord = native;
        }
        session.SetRecords(records.Cast<Il2CppSystem.Collections.Generic.IEnumerable<RankedItem>>());
        session.UpdateTime = Il2CppSystem.DateTime.UtcNow;
        session.State = LadderSession.LoadingState.Complete;
        return session;
    }
    internal static LadderFilter Filter(LadderUIFilterPanel panel)
    {
        // Native getter asks the online CycleStatusService. Room scope follows the loaded offline character.
        var local = PlayerFinder.getPlayerActor()?.characterDataTracker?.charData;
        var cycle = local?.Cycle ?? Il2CppLE.Data.Cycle.Legacy;
        var group = panel.ladderType == LadderType.Arena ? (LadderGroup)(panel.tabController?.SelectedTabId ?? 0) : LadderGroup.NoParty;
        return new LadderFilter(panel.ladderType, panel.ladderMode, group, cycle,
            (LadderClassFilter)(panel.classDropdown?.value ?? 0));
    }
    private static void Refresh(bool force)
    {
        if (view == null || view.filterPanel == null) return;
        var filter = view.filterPanel.CurrentSelection;
        string value = System.Text.Json.JsonSerializer.Serialize(new {filter = filter.AsPartitionKey,
            members = Members().OrderBy(p => p.Id, StringComparer.Ordinal).Select(p => new {p.Id, p.Name, p.Ladder}), connected = Room != null});
        if (!force && value == signature) return;
        signature = value;
        float scroll = view.scrollRect?.verticalNormalizedPosition ?? 1;
        view.ShowSelectedLadder(); // Native headings, layout, rows, icons, search and scroll.
        if (!force && view.scrollRect != null) view.scrollRect.verticalNormalizedPosition = scroll;
        if (view.filterPanel.header != null)
            view.filterPanel.header.text = (Room == null ? "LOCAL LADDER" : "ROOM LADDER") + " · " + filter.Type.ToString().ToUpperInvariant();
    }
    private static RankedItem Convert(RoomLadderRow row, LadderType type)
    {
        var c = row.Character;
        var metadata = new Il2CppSystem.Collections.Generic.Dictionary<string,string>();
        metadata.Add(LadderConstants.LadderEntryMetadataKeys.HasDied, (c.Hardcore && c.Deaths > 0).ToString());
        var skills = new Il2CppSystem.Collections.Generic.List<string>();
        foreach (var id in c.Skills) skills.Add(id);
        string cls = ((CharacterClassID)c.Class).ToString();
        string composite = row.PlayerId + ":" + c.CharacterId;
        LadderEntry entry;
        RankedItem ranked;
        switch (type)
        {
            case LadderType.Level:
                var level = new LevelLadderEntry {PlayerEntry = new LevelLadderEntry.LevelPlayerEntry {
                    PlayerId=row.PlayerId, PlayerName=row.AccountName, CharacterId=c.CharacterId, PlayerCharacterId=composite,
                    CharacterName=c.CharacterName, CharacterClass=cls, CharacterMastery=c.Mastery,
                    CharacterLevel=c.Level, CharacterTotalExp=c.Experience, CharacterDeaths=c.Deaths, CharacterSkills=skills}};
                entry=level; ranked=new RankedLadderEntry<LevelLadderEntry>{LadderEntry=level, Rank=row.Rank,Metadata=metadata}; break;
            case LadderType.Corruption:
                var corruption=new CorruptionLadderEntry {PlayerEntry=new CorruptionLadderEntry.CorruptionPlayerEntry {
                    PlayerId=row.PlayerId,PlayerName=row.AccountName,CharacterId=c.CharacterId,PlayerCharacterId=composite,
                    CharacterName=c.CharacterName,CharacterClass=cls,CharacterMastery=c.Mastery,CharacterLevel=c.Level,
                    MaxCorruption=c.Corruption, CharacterSkills=skills, TimelineId=0}};
                entry=corruption;ranked=new RankedLadderEntry<CorruptionLadderEntry>{LadderEntry=corruption,Rank=row.Rank,Metadata=metadata};break;
            case LadderType.SeasonEvent:
                var omen=new OmenLadderEntry {PlayerEntry=new OmenLadderEntry.OmenPlayerEntry {
                    PlayerId=row.PlayerId,PlayerName=row.AccountName,CharacterId=c.CharacterId,PlayerCharacterId=composite,
                    CharacterName=c.CharacterName,CharacterClass=cls,CharacterMastery=c.Mastery,CharacterLevel=c.Level,
                    OmenKills=c.OmenKills,OmenWindowsCompleted=c.OmenWindows,CharacterSkills=skills}};
                entry=omen;ranked=new RankedLadderEntry<OmenLadderEntry>{LadderEntry=omen,Rank=row.Rank,Metadata=metadata};break;
            default:
                var players=new Il2CppSystem.Collections.Generic.List<ArenaLadderEntry.ArenaPlayerEntry>();
                players.Add(new ArenaLadderEntry.ArenaPlayerEntry {
                    PlayerId=row.PlayerId,PlayerName=row.AccountName,CharacterId=c.CharacterId,PlayerCharacterId=composite,
                    CharacterName=c.CharacterName,CharacterClass=cls,CharacterMastery=c.Mastery,CharacterLevel=c.Level,CharacterSkills=skills});
                var arena=new ArenaLadderEntry{PlayerEntries=players,Wave=c.ArenaWave};
                entry=arena;ranked=new RankedLadderEntry<ArenaLadderEntry>{LadderEntry=arena,Rank=row.Rank,Metadata=metadata};break;
        }
        entry.LadderId="room";entry.LadderEntryId=composite;entry.LadderType=type.ToString();
        entry.TimeStamp=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        entry.SortingScore=type switch {LadderType.Level=>c.Experience,LadderType.Corruption=>c.Corruption,
            LadderType.SeasonEvent=>c.OmenKills,_=>c.ArenaWave};
        return ranked;
    }
}

[HarmonyPatch(typeof(LadderUIManager), "OnEnable")]
internal static class RoomLadderOpenPatch
{ private static void Postfix(LadderUIManager __instance) => NativeRoomLadder.Open(__instance); }
[HarmonyPatch(typeof(LadderUIManager), "OnDisable")]
internal static class RoomLadderClosePatch
{ private static void Prefix(LadderUIManager __instance) => NativeRoomLadder.Close(__instance); }
[HarmonyPatch(typeof(LadderSessionManager), nameof(LadderSessionManager.GetSession))]
internal static class RoomLadderSessionPatch
{
    private static bool Prefix(LadderFilter ladderFilter, ref LadderSession __result)
    {
        if (!NativeRoomLadder.Enabled) return true;
        __result=NativeRoomLadder.CreateSession(ladderFilter);return false;
    }
}
[HarmonyPatch(typeof(LadderUIManager), nameof(LadderUIManager.UpdatePlayerRowElement))]
internal static class RoomLadderSelfRowPatch
{
    private static bool Prefix(LadderUIManager __instance)
    {
        if (!NativeRoomLadder.Enabled) return true;
        // Local character already appears in the ranked table. Native self row needs an online account.
        __instance._selfLadderRowPlayer?.gameObject.SetActive(false);return false;
    }
}
[HarmonyPatch(typeof(LadderUIFilterPanel), "get_CurrentSelection")]
internal static class RoomLadderFilterPatch
{
    private static bool Prefix(LadderUIFilterPanel __instance, ref LadderFilter __result)
    {
        if (!NativeRoomLadder.Enabled) return true;
        __result=NativeRoomLadder.Filter(__instance);return false;
    }
}
