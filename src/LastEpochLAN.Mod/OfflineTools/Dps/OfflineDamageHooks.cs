using HarmonyLib;
using Il2Cpp;
using LastEpochLAN.Core;
using UnityEngine;

namespace LastEpochLAN.Mod.OfflineTools.Dps;

// Adapted from Mod.dll's two offline DamageStatsHolder paths. No online tuple
// detour, hit-event estimate, packet capture, or change to combat is installed.
internal static class OfflineDamageHooks
{
    internal sealed class Scope
    {
        internal IntPtr Holder;
        internal UnitHealth Health;
        internal float Before, Children;
        internal bool Owned;
        internal DamageMeterHitInfo Info;
    }
    [ThreadStatic] private static List<Scope> scopes;
    private static Actor local;
    private static int localFrame = -1;
    private static bool warned;
    private static readonly bool privateQa=Environment.GetCommandLineArgs().Contains("--lelan-offline-tools-smoke");
    internal static bool CaptureEnabled=>privateQa || OfflineToolkit.Settings.DpsVisible && !DamageMeterOverlay.IsPaused;
    internal static string DotDiagnostic="not entered";
    internal static Actor Local
    {
        get { if (localFrame != Time.frameCount) { localFrame = Time.frameCount; local = PlayerFinder.getPlayerActor(); } return local; }
    }
    internal static void Clear() { scopes?.Clear(); local = null; localFrame = -1; }
    internal static bool Owned(Actor actor, int depth = 0)
    {
        if (actor == null || Local == null || depth > 4) return false;
        if (actor.Pointer == Local.Pointer || actor.IsMinionOf(Local) || actor.IsCompanionOf(Local) || actor.IsTotemSummonedBy(Local)) return true;
        var creator = actor.GetCreator();
        return creator != null && creator.Pointer != actor.Pointer && Owned(creator, depth + 1);
    }
    internal static Scope Begin(DamageStatsHolder holder, Actor target)
    {
        if (!CaptureEnabled || !OfflineToolkit.Enabled || holder == null || target == null || target.isPlayerActor || target.summoned?.playerMinion == true) return null;
        try
        {
            var health = target.GetComponent<UnitHealth>();
            if (health == null || health.currentHealth <= 0) return null;
            scopes ??= new();
            if (scopes.Any(scope => scope.Holder == holder.Pointer && scope.Health != null && scope.Health.Pointer == health.Pointer)) return null;
            var references = holder.GetCreationReferences();
            var creator = holder.getCreator();
            bool owned = Owned(creator) || references != null && (Owned(references.creator) || references.HasBeenCreatedByLocalPlayer());
            if (!owned && scopes.Count == 0) return null;
            var scope = new Scope { Holder = holder.Pointer, Health = health, Before = health.currentHealth, Owned = owned };
            if (owned)
            {
                Ability primary=null, original=null, direct=null;
                try { var info = holder.GetDamageSourceInfo(); primary=info?.Item4; original=info?.Item3; direct=info?.Item2; } catch { }
                primary ??= references?.GetPrimaryAbility();
                original ??= references?.GetOriginalAbility();
                var ability = primary ?? original ?? direct;
                string id = ability?.playerAbilityID ?? "";
                string name = ability?.name ?? holder.getAbilityName();
                DamageType? type=null;
                try { var vfx = holder.GetInfoForHitVFX(); if(vfx!=null && vfx.Item1) type=vfx.Item2; } catch { }
                scope.Info = new DamageMeterHitInfo(creator?.name ?? references?.creator?.name ?? "Player",
                    name, primary?.name ?? "", original?.name ?? "", id, holder.damageTags, holder.hitEvents,
                    type);
            }
            scopes.Add(scope);
            return scope;
        }
        catch (Exception ex) { Warn(ex); return null; }
    }
    internal static Scope BeginDot(AilmentReceiver receiver, Il2CppSystem.Collections.Generic.List<AilmentReceiver.ActiveAilment> ailments)
    {
        if(privateQa) DotDiagnostic=$"begin actor={receiver?.actor!=null}, entries={ailments?.Count}";
        if(!CaptureEnabled || !OfflineToolkit.Enabled || receiver?.actor==null || receiver.actor.isPlayerActor || ailments==null || ailments.Count==0) return null;
        try
        {
            var health=receiver.actor.GetComponent<UnitHealth>();
            if(health==null || health.currentHealth<=0) return null;
            AilmentReceiver.ActiveAilment first=null;
            string name=null;
            bool mixed=false;
            foreach(var active in ailments)
            {
                if(active?.ailment==null || !active.ailment.dealsDamage) continue;
                // Mixed-owner ticks cannot be split accurately at this boundary.
                // Never credit the whole batch to the local player in that case.
                if(!Owned(active.creator)) {if(privateQa) DotDiagnostic="not locally owned";return null;}
                string next=active.ailment.name;
                if(first==null) {first=active;name=next;} else if(name!=next) mixed=true;
            }
            if(first==null) {if(privateQa) DotDiagnostic="no damaging ailment";return null;}
            string label=mixed?"Damage over time":name;
            var scope=new Scope {Holder=receiver.Pointer,Health=health,Before=health.currentHealth,Owned=true,
                Info=new DamageMeterHitInfo(first.creator?.name??"Player",label,"","",mixed?"dot":"dot:"+(int)first.ailment.id,AT.None,0,null)};
            scopes ??= new(); scopes.Add(scope);if(privateQa) DotDiagnostic="scope created";return scope;
        }
        catch(Exception ex) {Warn(ex);return null;}
    }
    internal static void End(Scope scope)
    {
        if (scope == null) return;
        try
        {
            scopes.Remove(scope);
            if (scope.Health == null) return;
            float loss = DpsRules.HealthLoss(scope.Before, scope.Health.currentHealth);
            // A nested proc is its own skill; subtract it from its outer hit.
            for (int i = scopes.Count - 1; i >= 0; i--)
                if (scopes[i].Health != null && scopes[i].Health.Pointer == scope.Health.Pointer) { scopes[i].Children += loss; break; }
            float ownLoss = Math.Max(0, loss - scope.Children);
            if (scope.Owned && ownLoss > 0) DamageMeterOverlay.NotifyHit(ownLoss, scope.Info);
        }
        catch (Exception ex) { Warn(ex); }
    }
    private static void Warn(Exception ex)
    {
        if (warned) return; warned = true;
        MelonLoader.MelonLogger.Warning("Offline DPS capture: " + ex.GetBaseException().Message);
    }
}

[HarmonyPatch(typeof(DamageStatsHolder), nameof(DamageStatsHolder.applyDamage), new[] { typeof(Actor) })]
internal static class DpsActorDamagePatch
{
    private static void Prefix(DamageStatsHolder __instance, Actor __0, out OfflineDamageHooks.Scope __state) => __state = OfflineDamageHooks.Begin(__instance, __0);
    private static Exception Finalizer(OfflineDamageHooks.Scope __state, Exception __exception) { OfflineDamageHooks.End(__state); return __exception; }
}
[HarmonyPatch(typeof(DamageStatsHolder), nameof(DamageStatsHolder.applyDamage), new[] { typeof(ProtectionClass), typeof(float) })]
internal static class DpsProtectionDamagePatch
{
    private static void Prefix(DamageStatsHolder __instance, ProtectionClass __0, out OfflineDamageHooks.Scope __state) =>
        __state = !OfflineDamageHooks.CaptureEnabled?null:OfflineDamageHooks.Begin(__instance, __0 == null ? null : __0.GetComponent<Actor>());
    private static Exception Finalizer(OfflineDamageHooks.Scope __state, Exception __exception) { OfflineDamageHooks.End(__state); return __exception; }
}

[HarmonyPatch(typeof(AilmentReceiver),nameof(AilmentReceiver.DealDamageForAilment))]
internal static class DpsAilmentDamagePatch
{
    private static void Prefix(AilmentReceiver __instance,Il2CppSystem.Collections.Generic.List<AilmentReceiver.ActiveAilment> __0,out OfflineDamageHooks.Scope __state) =>
        __state=OfflineDamageHooks.BeginDot(__instance,__0);
    private static Exception Finalizer(OfflineDamageHooks.Scope __state,Exception __exception) {OfflineDamageHooks.End(__state);return __exception;}
}

// Current native OnUpdateTick applies continuous DoT directly through
// ProtectionClass instead of calling DealDamageForAilment. Observe its actual
// HP delta as well; nested scopes subtract their own loss to avoid duplication.
[HarmonyPatch(typeof(AilmentReceiver),nameof(AilmentReceiver.OnUpdateTick))]
internal static class DpsContinuousAilmentPatch
{
    private static void Prefix(AilmentReceiver __instance,out OfflineDamageHooks.Scope __state) =>
        __state=!OfflineDamageHooks.CaptureEnabled?null:OfflineDamageHooks.BeginDot(__instance,__instance.ailments);
    private static Exception Finalizer(OfflineDamageHooks.Scope __state,Exception __exception) {OfflineDamageHooks.End(__state);return __exception;}
}
