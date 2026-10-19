using Il2CppInterop.Runtime.Injection;
using LastEpochLAN.Core;
using UnityEngine;

namespace LastEpochLAN.Mod;

// Feed the current game's native instanced player bar renderer, preserving
// its geometry, colors, shader, ward style and screen scaling.
internal sealed class NativePeerHealthBars
{
    private sealed record Bar(LanOverheadBar Values, Il2Cpp.IOverheadBar Native, Transform Model, GameObject Holder)
    {
        internal Il2Cpp.InstancedOverheadBarRenderer Renderer;
    }
    private readonly Dictionary<string, Bar> bars = new();
    private static bool injected;
    internal string Error = "";
    internal int Count => bars.Values.Count(b => ((bool)b.Renderer));
    private static void EnsureType()
    {
        if (injected) return;
        ClassInjector.RegisterTypeInIl2Cpp<LanOverheadBar>(new RegisterTypeOptions { Interfaces = new[] { typeof(Il2Cpp.IOverheadBar) } });
        injected = true;
    }
    internal void Tick(PlayerState[] peers, IReadOnlyDictionary<string, GameObject> models)
    {
        foreach (var id in bars.Keys.Where(id => !peers.Any(p => p.Id == id && p.Available && !p.Dead && p.MaxHealth > 0) || !models.ContainsKey(id)).ToArray()) Remove(id);
        if (peers.Length == 0) { Error = ""; return; }
        try
        {
            EnsureType();
            Il2Cpp.InstancedOverheadBarRenderer renderer = null;
            Il2Cpp.OverheadBarSystem.barRenderers?.TryGetValue(Il2Cpp.OverheadBarType.Player, out renderer);
            if (!((bool)renderer)) { Error = "Native player overhead renderer is not ready."; return; }
            var offset = Il2Cpp.GlobalPlayerProperties.GetOverheadBarOffset();
            foreach (var peer in peers)
            {
                if (!peer.Available || peer.Dead || peer.MaxHealth <= 0 || !models.TryGetValue(peer.Id, out var model) || !((bool)model)) continue;
                if (bars.TryGetValue(peer.Id, out var old) && old.Model != model.transform) Remove(peer.Id);
                if (!bars.TryGetValue(peer.Id, out var bar))
                {
                    var holder = new GameObject("LAN native overhead values " + peer.Id); holder.SetActive(false);
                    holder.transform.SetParent(model.transform, false);
                    var values = holder.AddComponent<LanOverheadBar>();
                    bar = new Bar(values, values.Cast<Il2Cpp.IOverheadBar>(), model.transform, holder);
                    bars.Add(peer.Id, bar);
                }
                float height = peer.Avatar == null ? 0 : Il2Cpp.CharacterClassList.getClass(peer.Avatar.ClassId)?.healthBarHeightOffset ?? 0;
                bar.Values.Location = model.transform.position + offset + Vector3.up * height;
                bar.Values.Health = Mathf.Clamp01(peer.Health / peer.MaxHealth);
                bar.Values.Ward = Mathf.Clamp01(peer.Ward / peer.MaxHealth);
                bar.Values.Segments = Math.Max(0, peer.Ward / peer.MaxHealth);
                if (bar.Renderer != renderer)
                {
                    Detach(bar);
                    renderer.AddBar(bar.Native);
                    bar.Renderer = renderer;
                }
            }
            Error = "";
        }
        catch (Exception ex) { Error = ex.Message; }
    }
    private static void Detach(Bar bar)
    {
        try { if (((bool)bar.Renderer)) bar.Renderer.RemoveBar(bar.Native); }
        catch { /* A scene-unloaded renderer no longer owns a live bar. */ }
        finally { bar.Renderer = null; }
    }
    private void Remove(string id)
    {
        if (!bars.Remove(id, out var bar)) return;
        Detach(bar);
        if ((bool)bar.Holder) UnityEngine.Object.Destroy(bar.Holder);
    }
    internal void Reset() { foreach (var id in bars.Keys.ToArray()) Remove(id); Error = ""; }
    internal static bool NativeSmoke()
    {
        EnsureType();
        var holder = new GameObject("LAN overhead renderer QA"); holder.SetActive(false);
        try
        {
            var values = holder.AddComponent<LanOverheadBar>();
            values.Location = new Vector3(3, 4, 5); values.Health = 0.75f; values.Ward = 0.2f; values.Segments = 1.25f;
            var native = values.Cast<Il2Cpp.IOverheadBar>();
            if (native.Position != values.Location || native.HealthNormalized != 0.75f || native.WardNormalized != 0.2f || native.WardSegments != 1.25f)
                throw new InvalidOperationException("Native overhead interface values did not round-trip.");
            var renderer = holder.AddComponent<Il2Cpp.InstancedOverheadBarRenderer>();
            renderer.AddBar(native); renderer.ClearNulls();
            if (renderer.barsToRender.Count != 1 || renderer.barMatrices.Count != 1 || !renderer.RemoveBar(native))
                throw new InvalidOperationException("Native overhead renderer registration/pruning/removal failed.");
            return true;
        }
        finally { UnityEngine.Object.Destroy(holder); }
    }
}

// The native renderer roots this provider in barsToRender. No Actor lifecycle,
// custom canvas or global prefab search is needed.
public sealed class LanOverheadBar : MonoBehaviour
{
    internal Vector3 Location;
    internal float Health, Ward, Segments;
    public LanOverheadBar(IntPtr pointer) : base(pointer) { }
    public Vector3 get_Position() => Location;
    public float get_HealthNormalized() => Health;
    public float get_WardNormalized() => Ward;
    public float get_WardSegments() => Segments;
}
