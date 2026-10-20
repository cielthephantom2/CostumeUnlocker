using HarmonyLib;
using Il2Cpp;
using LastEpochLAN.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Il2CppTMPro;
using Il2CppInterop.Runtime;
using System.Reflection;
using Rarity=Il2CppItemFiltering.RarityCondition.Rarity;

namespace LastEpochLAN.Mod;

// Native graphics + a NEW base ItemContainerUI. Never instantiate a second
// live Stash controller, bind manager.currentStash, or share its pool/delegates.
internal sealed class NativeSharedStashView : IDisposable
{
    internal static NativeSharedStashView Active;
    private readonly LanSharedChest owner;
    private GameObject parking,root;
    private ItemContainerUI grid;
    private readonly List<ItemContainer> pages=new();
    private readonly Dictionary<uint,ChestItem> displayed=new();
    private ItemContainersManager manager;
    private string chestId="";
    private long revision=-1;
    private int page;
    private Vector2Int dimensions;
    private TextMeshProUGUI pageLabel,statusLabel;
    private float retry;
    internal static bool Populating;
    internal string Error="";
    internal bool Visible=>root!=null && root.activeInHierarchy;
    internal int NativeItemUis=>grid?.currentlyShown?.Count??0;
    internal int PageCount=>pages.Count;
    internal bool BoundToPrivateStash=>grid?.container!=null && manager?.stash!=null && grid.container.Pointer==manager.stash.Pointer;
    internal bool IsInventory(ItemContainerUI candidate)=>candidate?.Container?.Pointer==manager?.inventory?.Pointer;
    internal NativeSharedStashView(LanSharedChest chest){owner=chest;}
    internal bool Owns(ItemContainerUI candidate)=>candidate!=null && grid!=null && candidate.Pointer==grid.Pointer;
    internal bool Owns(IItemContainer container)=>container!=null && pages.Any(p=>p.Pointer==container.Pointer);
    internal IItemContainer Container=>pages.Count>0?pages[Math.Clamp(page,0,pages.Count-1)].Cast<IItemContainer>():null;
    internal void QaDropCursor()
    {
        if(!Environment.GetCommandLineArgs().Contains("--lelan-shared-chest-smoke"))throw new InvalidOperationException("Private chest QA flag required");
        grid.TryDropFromCursor(null);
    }
    internal void Tick(ChestReply snapshot)
    {
        if(!owner.Open || !owner.NativeReady || snapshot==null){Dispose();return;}
        if(Input.GetKeyDown(KeyCode.Escape)){owner.Toggle();return;}
        try
        {
            if(manager!=null && manager.Pointer!=PlayerFinder.getItemContainersManager()?.Pointer)Dispose();
            if(root==null)
            {
                if(Time.unscaledTime<retry)return;
                retry=Time.unscaledTime+1;
                if(!Build())return;
            }
            if(snapshot.ChestId!=chestId || snapshot.Revision!=revision)
            {
                Rebuild(snapshot);chestId=snapshot.ChestId;revision=snapshot.Revision;
            }
            if(statusLabel!=null)statusLabel.text=owner.Status;
            if(pageLabel!=null)pageLabel.text=$"Hòm Host  {page+1}/{pages.Count}  •  {snapshot.Items.Length}/96 món";
        }
        catch(Exception ex){Error=ex.Message;owner.Status="Stash native: "+Error;Dispose();retry=Time.unscaledTime+3;}
    }
    private static int[] Indices(Transform root,Transform child)
    {
        var indices=new List<int>();
        for(var node=child;node.Pointer!=root.Pointer;node=node.parent)
        {if(node.parent==null)throw new InvalidOperationException("Native stash grid not under panel");indices.Add(node.GetSiblingIndex());}
        indices.Reverse();return indices.ToArray();
    }
    private bool Build()
    {
        manager=PlayerFinder.getItemContainersManager();
        var source=StashPanelUI.Instance;
        if(source==null)
        {
            UIBase.instance?.openStash(false,false,false,"",false);source=StashPanelUI.Instance;
            if(source==null){owner.Status="Đang nạp prefab Stash native...";return false;}
        }
        var sourceGrid=source.GetComponentsInChildren<StashItemContainerUI>(true).FirstOrDefault();
        if(sourceGrid==null)throw new InvalidOperationException("Native stash prefab has no item grid");
        var path=Indices(source.transform,sourceGrid.transform);
        dimensions=sourceGrid.Container?.GetSize()??new Vector2Int(12,20);
        if(dimensions.x<4 || dimensions.y<4 || dimensions.x>32 || dimensions.y>40)dimensions=new(12,20);
        // Inactive parent prevents cloned native Awake/OnEnable. Remove all
        // gameplay/controller/navigation callbacks before activating any child.
        parking=new GameObject("Shared Stash inactive staging");parking.SetActive(false);
        root=UnityEngine.Object.Instantiate(source.gameObject,parking.transform,false);root.name="LAN Shared Stash — independent native view";
        var gridTransform=root.transform;foreach(int index in path)gridTransform=gridTransform.GetChild(index);
        foreach(var item in root.GetComponentsInChildren<InventoryItemUI>(true))UnityEngine.Object.DestroyImmediate(item.gameObject);
        foreach(var script in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            bool graphic=script.TryCast<Graphic>()!=null;
            bool layout=script.TryCast<LayoutGroup>()!=null || script.TryCast<ContentSizeFitter>()!=null || script.TryCast<LayoutElement>()!=null || script.TryCast<Mask>()!=null || script.TryCast<RectMask2D>()!=null;
            if(!graphic && !layout)UnityEngine.Object.DestroyImmediate(script);
        }
        foreach(var group in root.GetComponentsInChildren<CanvasGroup>(true)){group.alpha=1;group.interactable=true;group.blocksRaycasts=true;}
        foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))
            if(text.text?.Trim().Equals("STASH",StringComparison.OrdinalIgnoreCase)==true)text.text="SHARED STASH";
        grid=gridTransform.gameObject.AddComponent<ItemContainerUI>();
        grid.identifier=ContainerID.STASH;grid.interactable=true;grid.forceFillRect=sourceGrid.forceFillRect;
        grid.disableItemBackground=sourceGrid.disableItemBackground;grid.slotSize=sourceGrid.slotSize;
        grid.initialiseOnEnableBehaviour=ItemContainerUI.InitialiseOnEnableBehaviour.Default;
        grid.ShouldSelectOnInitialization=false;
        pages.Add(NewPage());Active=this;
        root.SetActive(false);root.transform.SetParent(source.transform.parent,false);
        AddControls();
        if(source.gameObject.activeInHierarchy)UIBase.instance.closeStash(false);
        root.SetActive(true);grid.Initialize();
        UIBase.instance.openInventory(false,false,false,0,false);
        Error="";return true;
    }
    private ItemContainer NewPage()=>new(manager,dimensions,ContainerType.STASH,ContainerID.STASH,manager.inventory.getCharacterInfoProvider(),null,(Rarity)127);
    private void Rebuild(ChestReply snapshot)
    {
        if(grid==null)return;
        grid.ResetInternal(true,true);pages.Clear();displayed.Clear();Populating=true;
        try
        {
            pages.Add(NewPage());
            foreach(var item in snapshot.Items)
            {
                var data=new ItemDataUnpacked(Convert.FromBase64String(item.Data));
                if(data.individualID!=item.NativeId)throw new InvalidDataException("Projection item identity mismatch");
                if(!pages[^1].TryAddItem(data,1,Context.GIFTING))
                {
                    pages.Add(NewPage());
                    if(!pages[^1].TryAddItem(data,1,Context.GIFTING))throw new InvalidOperationException("Native stash cannot display this item size/type");
                }
                displayed.Add(item.NativeId,item);
            }
        }
        finally{Populating=false;}
        page=Math.Clamp(page,0,pages.Count-1);grid.Initialize();
        if(BoundToPrivateStash)throw new InvalidOperationException("Shared projection accidentally bound to personal stash");
    }
    private void Flip(int delta)
    {if(grid==null || pages.Count==0)return;grid.ResetInternal(true,true);page=(page+delta+pages.Count)%pages.Count;grid.Initialize();}
    private TextMeshProUGUI Label(string name,Vector2 anchor,Vector2 position,Vector2 size,string text)
    {
        var go=new GameObject(name,Il2CppType.Of<RectTransform>());go.transform.SetParent(root.transform,false);
        var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=anchor;rect.pivot=anchor;rect.anchoredPosition=position;rect.sizeDelta=size;
        var label=go.AddComponent<TextMeshProUGUI>();
        var font=root.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.font!=null)?.font;
        if(font!=null)label.font=font;
        label.fontSize=17;label.color=Color.white;label.raycastTarget=false;label.text=text;return label;
    }
    private void Button(string name,Vector2 anchor,Vector2 pos,Vector2 size,string text,Action action)
    {
        var go=new GameObject(name,Il2CppType.Of<RectTransform>());go.transform.SetParent(root.transform,false);
        var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=anchor;rect.pivot=anchor;rect.anchoredPosition=pos;rect.sizeDelta=size;
        var image=go.AddComponent<Image>();image.color=new Color(0.12f,0.1f,0.06f,0.95f);
        var label=Label(name+" text",new(0.5f,0.5f),Vector2.zero,size,text);label.transform.SetParent(go.transform,false);label.alignment=TextAlignmentOptions.Center;
        var button=go.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=image;
        button.onClick.AddListener(action);
    }
    private void AddControls()
    {
        Button("Shared close",new(1,1),new(-8,-8),new(45,34),"X",owner.Toggle);
        Button("Shared previous",new(0,0),new(12,5),new(40,32),"<",()=>Flip(-1));
        Button("Shared next",new(1,0),new(-12,5),new(40,32),">",()=>Flip(1));
        pageLabel=Label("Shared page",new(0.5f,0),new(0,8),new(380,28),"");
        statusLabel=Label("Shared status",new(0.5f,0),new(0,40),new(470,46),owner.Status);
        statusLabel.fontSize=14;statusLabel.alignment=TextAlignmentOptions.Center;
    }
    internal void Click(ItemContainerUI ui,PointerEventData data)
    {
        if(!Visible)return;
        var cursor=manager.cursor.GetContent();
        if(Owns(ui))
        {
            if(cursor?.Count==1){owner.NativeDepositCursor(cursor[0]);return;}
            var position=data!=null?ui.GetCursorPosInContainer(data):ui.GetCursorPosInContainer((Vector2)Input.mousePosition,null);
            var entry=pages[page].GetEntryAt(position);
            if(entry!=null && displayed.TryGetValue(entry.data.individualID,out var item))owner.NativeWithdraw(item);
        }
        else if(ui.Container?.Pointer==manager.inventory.Pointer)
        {
            var position=data!=null?ui.GetCursorPosInContainer(data):ui.GetCursorPosInContainer((Vector2)Input.mousePosition,null);
            var entry=manager.inventory.GetEntryAt(position);if(entry!=null)owner.NativeDepositInventory(entry);
        }
    }
    public void Dispose()
    {
        if(root!=null)root.SetActive(false);
        if(grid!=null){try{grid.ResetInternal(true,true);}catch{}}
        if(root!=null)UnityEngine.Object.Destroy(root);if(parking!=null)UnityEngine.Object.Destroy(parking);
        root=null;parking=null;grid=null;manager=null;pages.Clear();displayed.Clear();revision=-1;chestId="";
        if(Active==this)Active=null;
    }
}

[HarmonyPatch(typeof(ItemContainerUI),nameof(ItemContainerUI.UpdateContainer))]
internal static class SharedStashNativeBindingPatch
{
    private static bool Prefix(ItemContainerUI __instance,ref IItemContainer __result)
    {var view=NativeSharedStashView.Active;if(view?.Owns(__instance)!=true)return true;__result=view.Container;return false;}
}

// Keep native hover/tooltip/compare logic. Swallow every native action that could
// directly materialize a projection item in cursor/inventory or personal stash.
[HarmonyPatch]
internal static class SharedStashNativeInputPatch
{
    private static IEnumerable<MethodBase> TargetMethods()=>new[]{"onPointerClick","onBeginDrag","onEndDrag","TryDropFromCursor","TryQuickMove","TryToHoldItemToCursor","TryToPlaceItem","TryToDropItem","TryToEquipItem","TryQuickEquip","ProcessDefaultSubmit","ProcessDropSubmit","ProcessEquip"}
        .SelectMany(name=>typeof(ItemContainerUI).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Where(m=>m.Name==name));
    private static bool Prefix(ItemContainerUI __instance,MethodBase __originalMethod,object[] __args)
    {
        var view=NativeSharedStashView.Active;if(view?.Visible!=true)return true;
        var data=__args.OfType<PointerEventData>().FirstOrDefault();
        if(view.Owns(__instance))
        {
            if(__originalMethod.Name is "onPointerClick" or "onBeginDrag" or "TryDropFromCursor" or "ProcessDefaultSubmit" or "TryToPlaceItem")view.Click(__instance,data);
            return false;
        }
        if(view.IsInventory(__instance) && (__originalMethod.Name=="TryQuickMove" || __originalMethod.Name=="onPointerClick" && data!=null && (data.button==PointerEventData.InputButton.Right || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))))
        {view.Click(__instance,data);return false;}
        return true;
    }
}

[HarmonyPatch]
internal static class SharedStashProjectionMutationPatch
{
    private static IEnumerable<MethodBase> TargetMethods()=>typeof(ItemContainer).GetMethods(BindingFlags.Public|BindingFlags.Instance).Where(m=>m.Name is "TryAddItem" or "TryRemoveItem" or "MoveItemTo" or "TrySort");
    private static bool Prefix(ItemContainer __instance,object[] __args)
    {
        var view=NativeSharedStashView.Active;if(view==null || NativeSharedStashView.Populating)return true;
        if(view.Owns(__instance.Cast<IItemContainer>()))return false;
        return !__args.OfType<IItemContainer>().Any(view.Owns);
    }
}
