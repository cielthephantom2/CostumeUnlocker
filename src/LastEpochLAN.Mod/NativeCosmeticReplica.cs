using LastEpochLAN.Core;
using UnityEngine;
using Il2CppLE.AssetBundles;

namespace LastEpochLAN.Mod;

internal sealed class NativeCosmeticReplica
{
    private readonly GameObject owner;
    private GameObject aura;
    private CosmeticPrefab auraKey, footKey;
    private readonly List<(GameObject Root, float Until)> prints = new();
    private Vector3 lastStep;
    private float retryAt;
    internal string Error { get; private set; } = "";
    private bool right;
    private static readonly List<NativeCosmeticReplica> all = new();
    internal NativeCosmeticReplica(GameObject root) {owner=root; lastStep=root.transform.position; all.Add(this);}
    internal static (CosmeticPrefab Foot, CosmeticPrefab Aura) Capture(Il2Cpp.Actor actor)
        =>NativeCosmeticCapture.Capture(actor);
    private static GameObject Create(CosmeticPrefab key, Transform parent)
    {
        var root=new GameObject("LAN cosmetic VFX"); root.SetActive(false);
        if (parent != null) root.transform.SetParent(parent,false);
        try
        {
            var prefab=new SoftRef<GameObject>(new Il2CppLE.AssetBundles.Guid(key.P0,key.P1),key.SubPath);
            SoftRefExtensions.Instantiate(prefab,root.transform,false,"LastEpochLAN.Cosmetic",0);
            foreach (var script in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (script.TryCast<Il2CppLE.AssetBundles.LoadRefComponent>() == null && !NativeVfxPresentation.Keep(script)) UnityEngine.Object.DestroyImmediate(script);
            foreach (var collider in root.GetComponentsInChildren<Collider>(true)) collider.enabled=false;
            foreach (var body in root.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
            root.SetActive(true); return root;
        }
        catch {UnityEngine.Object.Destroy(root); throw;}
    }
    internal void Tick(AvatarState state)
    {
        if (Time.unscaledTime < retryAt || state == null) return;
        try
        {
            if (auraKey != state.Aura)
            {
                if ((bool)aura) {aura.SetActive(false); UnityEngine.Object.Destroy(aura); aura=null;}
                if (state.Aura != null) aura=Create(state.Aura,owner.transform);
                auraKey=state.Aura;
            }
            if (footKey != state.Footprint)
            {
                foreach(var oldPrint in prints) UnityEngine.Object.Destroy(oldPrint.Root);
                prints.Clear(); footKey=state.Footprint; lastStep=owner.transform.position;
            }
            var position=owner.transform.position;
            Error="";
            float distance=(position-lastStep).sqrMagnitude;
            if (distance > 64) {lastStep=position;return;}
            if (footKey == null || distance < 0.25f) return;
            lastStep=position; right=!right;
            int reuse=prints.FindIndex(p=>p.Until <= Time.unscaledTime);
            if (reuse < 0 && prints.Count >= 16) return;
            var root=reuse >= 0 && (bool)prints[reuse].Root ? prints[reuse].Root : Create(footKey,null);
            root.SetActive(false);
            root.transform.SetPositionAndRotation(position+owner.transform.right*(right ? 0.12f : -0.12f),owner.transform.rotation);
            root.SetActive(true);
            foreach(var particle in root.GetComponentsInChildren<ParticleSystem>(true)) particle.Play(true);
            var entry=(root,Time.unscaledTime+2f);
            if (reuse >= 0) prints[reuse]=entry; else prints.Add(entry);
        }
        catch (Exception ex) {Error=ex.Message; retryAt=Time.unscaledTime+2;}
    }
    internal static void TickAll()
    {
        for(int i=all.Count-1;i>=0;i--)
        {
            var replica=all[i];
            if (!(bool)replica.owner)
            {
                foreach(var print in replica.prints) if ((bool)print.Root) UnityEngine.Object.Destroy(print.Root);
                all.RemoveAt(i); continue;
            }
            foreach(var print in replica.prints)
                if ((bool)print.Root && print.Root.activeSelf && (print.Until <= Time.unscaledTime || !replica.owner.activeInHierarchy)) print.Root.SetActive(false);
            replica.prints.RemoveAll(print=>!(bool)print.Root);
        }
    }
}
