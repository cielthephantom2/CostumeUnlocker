using LastEpochLAN.Core;
using UnityEngine;
using Actor = Il2Cpp.Actor;

namespace LastEpochLAN.Mod;

internal sealed class NativeBossHud
{
    private Il2Cpp.UIActorInfo ui;
    private Il2Cpp.BaseDisplayInformation bound;
    private float retry;
    internal int BoundId;
    internal string Error = "";
    private Il2Cpp.UIActorInfo Find()
    {
        if (ui == null && Time.unscaledTime >= retry)
        {
            retry = Time.unscaledTime + 2;
            ui = Resources.FindObjectsOfTypeAll<Il2Cpp.UIActorInfo>().FirstOrDefault(x => x != null && x.gameObject.scene.IsValid() && x.gameObject.scene.isLoaded);
        }
        return ui;
    }
    internal int Capture(IReadOnlyDictionary<int, Actor> actors, PlayerState[] players, string scene)
    {
        var info = Find()?.ActorOnMouse?.TryCast<Il2Cpp.ActorDisplayInformation>();
        if (info != null)
            foreach (var pair in actors)
                if (IsAliveBoss(pair.Value) && (info.actor?.Pointer == pair.Value.Pointer || info.actorSync?.Pointer == pair.Value.GetActorSync()?.Pointer)) return pair.Key;
        // Boss encounters remain visible for the party even if only a guest is
        // beside the boss. Selection is decided by the host, then sent by ID.
        return actors.Where(pair => IsAliveBoss(pair.Value) && players.Any(p => p.Available && !p.Dead && p.Scene == scene &&
            Vector3.SqrMagnitude(pair.Value.position() - new Vector3(p.X, p.Y, p.Z)) <= 1600))
            .OrderBy(pair => pair.Key).Select(pair => pair.Key).FirstOrDefault();
    }
    private static bool IsAliveBoss(Actor actor) => actor != null && actor.data?.actorType == Il2Cpp.ActorData.Type.Boss &&
        actor.health != null && actor.health.currentHealth > 0 && !actor.isDying(false) && actor.gameObject.activeInHierarchy;
    internal void Apply(WorldState world, IReadOnlyDictionary<int, Actor> actors)
    {
        try
        {
            if (Find() == null) return;
            if (world.BossId == 0 || !actors.TryGetValue(world.BossId, out var actor) || actor == null || !IsAliveBoss(actor)) { Clear(); return; }
            var sync = actor.GetActorSync();
            var display = sync?.actorVisuals?.GetComponent<Il2Cpp.ActorDisplayInformation>() ?? actor.GetComponent<Il2Cpp.ActorDisplayInformation>();
            if (display == null) { Error = "Boss native display information is not ready."; return; }
            display.actorClass = Il2Cpp.DisplayActorClass.Boss;
            if (BoundId != world.BossId || bound == null || bound.Pointer != display.Pointer || ui.ActorOnMouse?.Pointer != display.Pointer)
            {
                ui.setActorOnMouse(display, display.getAlignment(), true, display.ailmentVFXCreator);
                bound = display; BoundId = world.BossId;
            }
            ui.stillHovered();
            var state = world.Enemies.First(e => e.Id == world.BossId);
            ui.bossEnemyBar?.setFill(Mathf.Clamp01(state.Health / state.MaxHealth));
            Error = "";
        }
        catch (Exception ex) { Error = ex.Message; }
    }
    private void Clear()
    {
        if (bound != null && ui != null && ui.ActorOnMouse?.Pointer == bound.Pointer) ui.clearActorOnMouse();
        bound = null; BoundId = 0;
    }
    internal void Reset() { Clear(); ui = null; retry = 0; Error = ""; }
}
