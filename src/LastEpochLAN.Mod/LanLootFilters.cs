using HarmonyLib;
using Il2Cpp;
using Il2CppItemFiltering;
using Il2CppInterop.Runtime;
using LastEpochLAN.Core;
using UnityEngine;

namespace LastEpochLAN.Mod;

/// <summary>Room filter copies are memory-only; never install or bind a peer's filter.</summary>
internal sealed class LanLootFilters
{
    private LanSession room;
    private float nextPoll;
    private readonly Dictionary<string, (string Xml, ItemFilter Filter)> copies = new();
    private readonly Dictionary<string, (string Xml, string Error)> rejected = new();
    private static (string Owner, ItemFilter Filter)[] remote = Array.Empty<(string, ItemFilter)>();
    [ThreadStatic] private static bool evaluating;
    private bool refreshPending;
    private static LanSession activeRoom;
    private static readonly Dictionary<IntPtr, string> labelSuffixes = new();
    public string Status { get; private set; } = "Room filters: offline";

    public void Tick(LanSession session)
    {
        activeRoom = session?.Connected == true ? session : null;
        if (!ReferenceEquals(room, session) || session?.Connected != true)
        {
            bool hadRemote = remote.Length > 0;
            room = session; copies.Clear(); rejected.Clear(); remote = Array.Empty<(string, ItemFilter)>(); nextPoll = 0;
            refreshPending = false;
            labelSuffixes.Clear();
            Status = "Room filters: offline";
            if (hadRemote) RefreshLabels();
        }
        if (session?.Connected != true || Time.unscaledTime < nextPoll) return;
        nextPoll = Time.unscaledTime + 1;
        var manager = ItemFilterManager.Instance;
        if (!manager || ItemFilterManager.serializer == null)
        { Status = "Room filters: waiting for native manager"; return; }
        // Local publishing and remote ingestion have independent failure domains.
        string localStatus = PublishLocal(session, manager);
        var roster = session.LootFilters.Where(f => f.OwnerId != session.LocalId && f.Xml != "").ToArray();
        var owners = roster.Select(f => f.OwnerId).ToHashSet(StringComparer.Ordinal);
        bool changed = refreshPending;
        refreshPending = false;
        foreach (string removed in copies.Keys.Where(id => !owners.Contains(id)).ToArray())
        { copies.Remove(removed); changed = true; }
        foreach (string removed in rejected.Keys.Where(id => !owners.Contains(id)).ToArray()) rejected.Remove(removed);
        foreach (var entry in roster)
        {
            if (copies.TryGetValue(entry.OwnerId, out var cached) && cached.Xml == entry.Xml) continue;
            if (rejected.TryGetValue(entry.OwnerId, out var failed) && failed.Xml == entry.Xml) continue;
            try
            {
                var reader = new Il2CppSystem.IO.StringReader(entry.Xml);
                ItemFilter filter;
                try { filter = ItemFilterManager.serializer.Deserialize(reader).Cast<ItemFilter>(); }
                finally { reader.Close(); }
                if (filter == null) throw new InvalidDataException("Remote filter is null.");
                filter.PostDeserialization();
                filter.Sanitize(false);
                if (filter.rules == null || filter.rules.Count > ItemFilter.MAX_RULES_PER_FILTER)
                    throw new InvalidDataException("Invalid remote rule count.");
                copies[entry.OwnerId] = (entry.Xml, filter);
                rejected.Remove(entry.OwnerId);
                changed = true;
            }
            catch (Exception ex)
            {
                // Drop only this owner's obsolete copy. Never clear valid peers
                // or publish an empty local filter because a remote XML failed.
                changed |= copies.Remove(entry.OwnerId);
                rejected[entry.OwnerId] = (entry.Xml, ex.GetType().Name);
            }
        }
        bool hookReady = copies.Count == 0 || NativeLootFilterMatch.EnsureInstalled();
        if (changed || !hookReady && remote.Length > 0)
        {
            remote = hookReady ? copies.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => (p.Key, p.Value.Filter)).ToArray() : Array.Empty<(string, ItemFilter)>();
            RefreshLabels();
        }
        Status = localStatus + " (" + remote.Length + " remote, " + rejected.Count + " rejected)";
        if (!hookReady) Status += " - native hook unavailable";
        else if (NativeLootFilterMatch.LastError != "") Status += " - native match error";
    }

    private static string PublishLocal(LanSession session, ItemFilterManager manager)
    {
        try
        {
            string xml = "";
            var active = manager.Filter;
            if (active != null)
            {
                // PreSerialization may modify rule backing fields, so operate on a detached copy.
                var clone = active.Clone().Cast<ItemFilter>();
                clone.PreSerialization();
                var writer = new Il2CppSystem.IO.StringWriter();
                try { ItemFilterManager.serializer.Serialize(writer, clone); xml = writer.ToString(); }
                finally { writer.Close(); }
            }
            if (!session.PublishLootFilter(xml))
            {
                session.PublishLootFilter("");
                return "Room filters: local filter exceeds sharing limits";
            }
            return "Room filters: synced";
        }
        catch (Exception ex)
        {
            // A failed LOCAL serialization must not leave a previous character's
            // filter advertised. This path cannot be reached by remote ingestion.
            session.PublishLootFilter("");
            return "Room filters: local " + ex.GetType().Name;
        }
    }

    private static void RefreshLabels()
    {
        try { if (ItemFilterManager.Instance) ItemFilterManager.InvokeLootFilterChanged(ItemFilterManager.ChangeType.Modified); }
        catch { /* A scene teardown can destroy the label manager before the room disconnects. */ }
    }

    [HarmonyPatch(typeof(GroundItemLabel), nameof(GroundItemLabel.SetLabelText))]
    private static class MarkFilterOwners
    {
        private static void Postfix(GroundItemLabel __instance)
        {
            if (!__instance || !__instance.itemText) return;
            try
            {
                string text = __instance.itemText.text ?? "";
                if (labelSuffixes.TryGetValue(__instance.Pointer, out var previous) && text.EndsWith(previous, StringComparison.Ordinal))
                    text = text[..^previous.Length];
                labelSuffixes.Remove(__instance.Pointer);
                var session = activeRoom;
                var manager = ItemFilterManager.Instance;
                if (session?.Connected != true || !manager || manager.ManualFilterDisable)
                { __instance.itemText.text = text; return; }
                var item = __instance.getItemData();
                if (item == null) return;
                var filters = session.LootFilters;
                var people = session.PlayerPositions;
                var names = new List<string>();
                bool wasEvaluating = evaluating;
                evaluating = true;
                try
                {
                    // Evaluate every filter independently: several people can want the same item.
                    foreach (var entry in filters.OrderBy(f => f.OwnerId == session.LocalId ? 0 : 1).ThenBy(f => f.OwnerId, StringComparer.Ordinal))
                    {
                        ItemFilter filter = entry.OwnerId == session.LocalId ? manager.Filter :
                            remote.FirstOrDefault(copy => copy.Owner == entry.OwnerId).Filter;
                        if (filter == null) continue;
                        try { if (MatchNative(filter, item).Outcome == Rule.RuleOutcome.HIDE) continue; }
                        catch (Exception ex)
                        {
                            if (entry.OwnerId != session.LocalId) roomInstance?.RejectRemote(entry.OwnerId, ex);
                            continue;
                        }
                        string name = people.FirstOrDefault(p => p.Id == entry.OwnerId)?.Name ?? entry.OwnerId[..8];
                        name = new string(name.Where(c => !char.IsControl(c) && c is not '<' and not '>' and not '&' and not '[' and not ']').Take(24).ToArray());
                        names.Add(name.Length > 0 ? name : entry.OwnerId[..8]);
                    }
                }
                finally { evaluating = wasEvaluating; }
                string suffix = names.Count == 0 ? "" : " [" + string.Join(", ", names) + "]";
                __instance.itemText.text = text + suffix;
                if (suffix.Length > 0)
                {
                    if (labelSuffixes.Count >= 4096) labelSuffixes.Clear();
                    labelSuffixes[__instance.Pointer] = suffix;
                }
            }
            catch { /* Attribution must never prevent native labels or pickup. */ }
        }
    }

    private static LanLootFilters roomInstance;
    public LanLootFilters() { roomInstance = this; }

    [HarmonyPatch(typeof(GroundItemLabel), nameof(GroundItemLabel.beforePooling))]
    private static class ClearPooledMarker
    {
        private static void Prefix(GroundItemLabel __instance)
        {
            if (__instance) labelSuffixes.Remove(__instance.Pointer);
        }
    }

    internal static Verdict SelectRemoteVerdict(IntPtr owner, IntPtr itemPointer)
    {
        if (evaluating || activeRoom?.Connected != true || remote.Length == 0 || itemPointer == IntPtr.Zero) return null;
        var manager = ItemFilterManager.Instance;
        if (!manager || manager.ManualFilterDisable || manager.Filter?.Pointer != owner) return null;
        var item = new ItemDataUnpacked(itemPointer);
        evaluating = true;
        try
        {
            foreach (var entry in remote)
            {
                try
                {
                    var match = MatchNative(entry.Filter, item);
                    if (match.Outcome == Rule.RuleOutcome.HIDE) continue;
                    return match;
                }
                catch (Exception ex) { roomInstance?.RejectRemote(entry.Owner, ex); }
            }
            return null;
        }
        finally { evaluating = false; GC.KeepAlive(item); }
    }

    private void RejectRemote(string owner, Exception error)
    {
        if (!copies.Remove(owner, out var failed)) return;
        rejected[owner] = (failed.Xml, error.GetType().Name);
        remote = remote.Where(entry => entry.Owner != owner).ToArray();
        // Do not fire label refresh recursively from inside native Match.
        refreshPending = true;
    }

    internal sealed record Verdict(Rule.RuleOutcome Outcome, bool HasColor, int Color,
        bool HasEmphasis, bool Emphasis, int Sound, int MapIcon,
        bool BeamOverride, EpochColor.BeamSize BeamSize, Color BeamColor);

    // Generated Match wrappers incorrectly treat Nullable<T> out bytes as object pointers.
    // Give each native out value its own stack storage instead.
    private static unsafe Verdict MatchNative(ItemFilter filter, ItemDataUnpacked item, bool throughRoom = false, bool guard = false)
    {
        NativeLootFilterMatch.RequireLayout();
        var pointer = throughRoom ? NativeLootFilterMatch.MethodInfo : NativeLootFilterMatch.EvaluationMethodInfo;
        ulong color = 0, emphasis = 0;
        byte* canary = stackalloc byte[48];
        if (guard) for (int i = 0; i < 48; i++) canary[i] = 0xA5;
        void* colorOut = guard ? canary + 8 : &color;
        void* emphasisOut = guard ? canary + 32 : &emphasis;
        int rule = -1, sound = 0, mapIcon = 0, beamSize = 0;
        bool beamOverride = false;
        Color beamColor = default;
        void** args = stackalloc void*[9];
        args[0] = (void*)item.Pointer; args[1] = colorOut; args[2] = emphasisOut;
        args[3] = &rule; args[4] = &sound; args[5] = &mapIcon;
        args[6] = &beamOverride; args[7] = &beamSize; args[8] = &beamColor;
        IntPtr exception = IntPtr.Zero;
        IntPtr result = IL2CPP.il2cpp_runtime_invoke(pointer, filter.Pointer, args, ref exception);
        Il2CppException.RaiseExceptionIfNecessary(exception);
        if (result == IntPtr.Zero) throw new InvalidOperationException("Missing filter verdict.");
        if (guard)
        {
            for (int i = 0; i < 48; i++)
                if (!(i is >= 8 and < 16 or >= 32 and < 34) && canary[i] != 0xA5)
                    throw new InvalidOperationException("Native filter out buffer overflow at byte " + i);
            color = *(ulong*)colorOut; emphasis = *(ushort*)emphasisOut;
        }
        NativeLootFilterMatch.ReadNullableInt(&color, out var hasColor, out var colorValue);
        NativeLootFilterMatch.ReadNullableBool(&emphasis, out var hasEmphasis, out var emphasisValue);
        var outcome = new Verdict(*(Rule.RuleOutcome*)IL2CPP.il2cpp_object_unbox(result),
            hasColor, colorValue, hasEmphasis, emphasisValue,
            sound, mapIcon, beamOverride, (EpochColor.BeamSize)beamSize, beamColor);
        GC.KeepAlive(filter); GC.KeepAlive(item);
        return outcome;
    }

#if LELAN_QA

    internal static object NativeSmoke()
    {
        if (!Environment.GetCommandLineArgs().Contains("--lelan-loot-filter-smoke") ||
            !Environment.GetCommandLineArgs().Contains("--lelan-private-campaign-smoke"))
            throw new InvalidOperationException("Private campaign QA only.");
        var manager = ItemFilterManager.Instance;
        if (!manager || ItemFilterManager.serializer == null) throw new InvalidOperationException("Native filter manager not ready.");
        var oldFilter = manager.Filter;
        bool oldDisable = manager.ManualFilterDisable;
        var oldInstance = roomInstance; var oldRoom = activeRoom; var oldRemote = remote;
        var checks = new List<string>();
        void Check(bool passed, string label)
        { if (!passed) throw new InvalidOperationException(label); checks.Add(label); }
        string Xml(ItemFilter filter)
        {
            var writer = new Il2CppSystem.IO.StringWriter();
            try { var clone = filter.Clone().Cast<ItemFilter>(); clone.PreSerialization(); ItemFilterManager.serializer.Serialize(writer, clone); return writer.ToString(); }
            finally { writer.Close(); }
        }
        void Wait(Func<bool> condition)
        { if (!SpinWait.SpinUntil(condition, 5000)) throw new TimeoutException("Native filter TCP fixture timed out."); }
        try
        {
            activeRoom = null; remote = Array.Empty<(string, ItemFilter)>();
            manager._ManualFilterDisable_k__BackingField = false;
            var showRule = new Rule(Rule.RuleOutcome.SHOW, 2, true) { isEnabled = true, recolor = true, emphasized = true, color = 2 };
            var show = new ItemFilter("Private QA Show"); show.AddRule(showRule);
            var hide = new ItemFilter("Private QA Hide"); hide.AddRule(new Rule(Rule.RuleOutcome.HIDE) { isEnabled = true });
            var data = ItemData.DummyData(9, 0, 2); data.RebuildID();
            var item = new ItemDataUnpacked(data.GetIDAsArray().ToArray());
            var baseline = MatchNative(show, item, guard: true);
            Check(baseline.Outcome != Rule.RuleOutcome.HIDE && baseline.HasColor && baseline.HasEmphasis,
                "native colored/emphasized SHOW and nullable guard bytes before detour: " + baseline + "; " + NativeLootFilterMatch.LayoutSummary + "; ruleMatches=" + showRule.Match(item, 1));
            Check(NativeLootFilterMatch.EnsureInstalled(), "native raw-pointer Match detour installs after layout validation");
            Check(MatchNative(show, item, true, true) == baseline,
                "no-room native verdict/style preserved through detour with intact canaries");
            manager._Filter_k__BackingField = hide;
            using var host = LanSession.Host("FilterQAHost", "filter-native", "filter-native", 0, System.Net.IPAddress.Loopback);
            using var guest = LanSession.JoinAsync("127.0.0.1", host.Port, "FilterQAGuest", "filter-native", "filter-native").GetAwaiter().GetResult();
            using var good = LanSession.JoinAsync("127.0.0.1", host.Port, "FilterQAGood", "filter-native", "filter-native").GetAwaiter().GetResult();
            var controller = new LanLootFilters();
            string showXml = Xml(show), hideXml = Xml(hide);
            guest.PublishLootFilter(showXml);
            Wait(() => host.LootFilters.Any(f => f.OwnerId == guest.LocalId && f.Xml == showXml));
            controller.Tick(host);
            Check(MatchNative(hide, item, true, true) == baseline,
                "native local HIDE plus remote SHOW returns complete remote style with intact canaries");
            manager._ManualFilterDisable_k__BackingField = true;
            Check(MatchNative(hide, item, true, true).Outcome == Rule.RuleOutcome.HIDE,
                "manual disable bypasses room override");
            manager._ManualFilterDisable_k__BackingField = false;
            guest.PublishLootFilter(hideXml);
            Wait(() => host.LootFilters.Any(f => f.OwnerId == guest.LocalId && f.Xml == hideXml));
            controller.nextPoll = 0; controller.Tick(host);
            Check(MatchNative(hide, item, true, true).Outcome == Rule.RuleOutcome.HIDE,
                "live remote filter edit from SHOW to HIDE updates native union");
            const string badXml = "<ItemFilter><lootFilterVersion>invalid-int</lootFilterVersion></ItemFilter>";
            guest.PublishLootFilter(badXml); good.PublishLootFilter(showXml);
            Wait(() => host.LootFilters.Any(f => f.OwnerId == guest.LocalId && f.Xml == badXml) &&
                host.LootFilters.Any(f => f.OwnerId == good.LocalId && f.Xml == showXml));
            controller.nextPoll = 0; controller.Tick(host);
            Check(controller.rejected.ContainsKey(guest.LocalId) && controller.copies.ContainsKey(good.LocalId) &&
                host.LootFilters.Any(f => f.OwnerId == host.LocalId && f.Xml != "") &&
                MatchNative(hide, item, true, true) == baseline,
                "invalid native XML isolates one owner while valid remote and local publication remain active");
            guest.PublishLootFilter(showXml);
            Wait(() => host.LootFilters.Any(f => f.OwnerId == guest.LocalId && f.Xml == showXml));
            controller.nextPoll = 0; controller.Tick(host);
            Check(!controller.rejected.ContainsKey(guest.LocalId) && controller.copies.ContainsKey(guest.LocalId),
                "corrected remote XML rejoins without reconnect");
            controller.Tick(null);
            Check(MatchNative(hide, item, true, true).Outcome == Rule.RuleOutcome.HIDE,
                "leaving room restores native HIDE with intact canaries");
            Check(NativeLootFilterMatch.LastError == "", "native detour recorded no dispatch exception");
            return new { passed = true, nativeLootFilters = checks.ToArray(), scope = "Private native Match ABI, TCP filter updates and per-owner error isolation; not rendered labels or two-PC acceptance" };
        }
        finally
        {
            manager._Filter_k__BackingField = oldFilter;
            manager._ManualFilterDisable_k__BackingField = oldDisable;
            roomInstance = oldInstance; activeRoom = oldRoom; remote = oldRemote;
        }
    }
#endif
}
