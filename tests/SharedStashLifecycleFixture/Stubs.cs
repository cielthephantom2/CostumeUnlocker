namespace HarmonyLib{[AttributeUsage(AttributeTargets.Class)]public sealed class HarmonyPatch:Attribute{}}
namespace UnityEngine
{
    public class Component
    {
        private static int sequence;public IntPtr Pointer=(IntPtr)Interlocked.Increment(ref sequence);
        public GameObject gameObject;public Transform transform=>gameObject.transform;
        public T GetComponent<T>()where T:Component=>gameObject.GetComponent<T>();
        public T[] GetComponentsInChildren<T>(bool inactive)where T:Component=>gameObject.All().SelectMany(g=>g.components).OfType<T>().ToArray();
    }
    public class Transform:Component{}
    public sealed class GameObject
    {
        public readonly List<Component> components=new();public readonly List<GameObject> children=new();public readonly Transform transform;
        public bool activeSelf=true;public bool activeInHierarchy=>activeSelf;
        public GameObject(){transform=AddComponent<Transform>();}
        public T AddComponent<T>()where T:Component,new(){var result=new T{gameObject=this};components.Add(result);return result;}
        public T GetComponent<T>()where T:Component=>components.OfType<T>().FirstOrDefault();
        public void SetActive(bool active)=>activeSelf=active;
        public IEnumerable<GameObject> All()=>new[]{this}.Concat(children.SelectMany(c=>c.All()));
    }
    public sealed class CanvasGroup:Component{public float alpha=1;public bool blocksRaycasts=true,interactable=true,ignoreParentGroups;}
    public sealed class Canvas:Component{public bool enabled=true;}
}
namespace Il2CppLE.UI.PanelSystem
{
    public class StashPanel:UnityEngine.Component{public void OnOpen(){}public void OnOpenWithArgs(){}}
    public sealed class StashPanelV2:StashPanel{public new void OnOpen(){}}
    public sealed class PanelSystem{public int Closed;public void ClosePanel(StashPanel panel){Closed++;panel.gameObject.SetActive(false);}}
}
namespace Il2Cpp
{
    public sealed class UIBase
    {
        public static UIBase instance=new();public Il2CppLE.UI.PanelSystem.PanelSystem PanelSystem=new();
    }
}
namespace LastEpochLAN.Mod
{
    using UnityEngine;using Il2CppLE.UI.PanelSystem;
    internal sealed class LanSharedChest{internal bool Open=true;}
    internal sealed partial class NativeSharedStashView
    {
        internal static NativeSharedStashView Active;
        private readonly LanSharedChest owner=new();private GameObject root;private float loadStarted=-1;private bool loadFailed;private float retry;
        private readonly Dictionary<IntPtr,Transform> clones=new();
        private Transform Cloned(Transform source,Transform original)=>clones[original.Pointer];
        internal void Open(bool open)=>owner.Open=open;
        internal void Root(GameObject value)=>root=value;
        internal float LoadStarted=>loadStarted;
        internal void Map(GameObject original,GameObject clone)=>clones[original.transform.Pointer]=clone.transform;
        internal void Restore(bool clone)=>RestoreStagingVisibility(clone);
        internal int Recorded=>sourceVisibility.Count+sourceCanvases.Count;
        internal void StartLoad()=>loadStarted=10;
        internal void FailLoad(){loadFailed=true;retry=20;}
        internal bool RetryReady=>!loadFailed && retry==0;
    }
}
