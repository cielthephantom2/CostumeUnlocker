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
using Il2CppLE.UI.PanelSystem;

namespace LastEpochLAN.Mod;

// Native graphics + a NEW searchable ItemContainerUI. Never instantiate a second
// live Stash controller, bind manager.currentStash, or share its pool/delegates.
internal sealed partial class NativeSharedStashView : IDisposable
{
    internal static NativeSharedStashView Active;
    private readonly LanSharedChest owner;
    private GameObject parking,root;
    private ItemContainerUI grid;
    private readonly List<ItemContainer> pages=new();
    private readonly Dictionary<uint,ChestItem> displayed=new();
    private readonly Dictionary<IntPtr,uint> itemButtons=new();
    internal int ItemButtonCount=>itemButtons.Count;
    internal long Interactions {get;private set;}
    internal int InputRaycasterCount=>root==null?0:root.GetComponentsInChildren<GraphicRaycaster>(true).Length;
    private ItemContainersManager manager;
    private string chestId="";
    private long revision=-1;
    private ChestItem[] projectedItems=Array.Empty<ChestItem>();
    private int page;
    private Vector2Int dimensions;
    private float retry;
    private float loadStarted=-1;
    private bool loadFailed;
    internal string SourcePanelKind {get;private set;}="";
    internal bool LegacySingletonMissing {get;private set;}
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
        PollTabConfiguration();
        if(!owner.Open){Hide();return;}
        if(!owner.NativeReady){Dispose();return;}
        if(snapshot==null)
        {
            if(!awaitingTab && grid!=null){grid.ResetInternal(true,true);displayed.Clear();itemButtons.Clear();projectedItems=Array.Empty<ChestItem>();awaitingTab=true;}
            return;
        }
        awaitingTab=false;
        if(!Configuring && Input.GetKeyDown(KeyCode.Escape)){owner.Toggle();return;}
        try
        {
            if(manager!=null && manager.Pointer!=PlayerFinder.getItemContainersManager()?.Pointer)Dispose();
            if(root==null)
            {
                if(Time.unscaledTime<retry)return;
                retry=Time.unscaledTime+1;
                Active=this;
                if(!Build())return;
            }
            if(!root.activeSelf)root.SetActive(true);
            if(snapshot.Tab!=shownTab || snapshot.ChestId!=chestId || snapshot.Revision!=revision)
            {
                if(snapshot.Tab!=shownTab || snapshot.ChestId!=chestId || !projectedItems.SequenceEqual(snapshot.Items))
                {Rebuild(snapshot);projectedItems=snapshot.Items.ToArray();}
                chestId=snapshot.ChestId;revision=snapshot.Revision;shownTab=snapshot.Tab;
            }
            RefreshSharedTabs(snapshot);
            BindItemActions();
            PollNativeOverflow();
        }
        catch(Exception ex)
        {
            Error=ex.GetBaseException().Message;owner.Status="Stash native: "+Error;
            MelonLoader.MelonLogger.Warning(owner.Status+"\n"+ex);
            try{Dispose();}finally{loadFailed=true;}
        }
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
        if(loadFailed)return false;
        var ui=UIBase.instance;
        var panels=ui?.PanelSystem;
        // StashPanelUI is a legacy notification component, not the loader or a
        // singleton guaranteed to exist in Stash V2. Resolve the real panel.
        StashPanel source=panels?.GetPanelIfOpen<StashPanelV2>();
        source??=panels?.GetPanelIfOpen<StashPanel>();
        if(source==null || !source.gameObject.activeInHierarchy)
        {
            if(ui==null || panels==null)throw new InvalidOperationException("Panel system is not ready; close and reopen F2 to retry");
            if(loadStarted<0)
            {
                loadStarted=Time.unscaledTime;
                var args=Environment.GetCommandLineArgs();
                if(args.Contains("--lelan-shared-chest-smoke") && args.Contains("--lelan-stash-v2"))
                    panels.OpenPanelWithArgs<StashPanelV2,StashPanelOpenData>(new(){searchString=""},new Il2CppSystem.Nullable<PanelLocation>());
                else if(args.Contains("--lelan-shared-chest-smoke") && args.Contains("--lelan-stash-v1"))
                    panels.OpenPanelWithArgs<StashPanel,StashPanelOpenData>(new(){searchString=""},new Il2CppSystem.Nullable<PanelLocation>());
                else ui.openStash(false,false,false,"",false);
            }
            if(Time.unscaledTime-loadStarted>20)throw new TimeoutException("Stash did not open within 20 seconds; close and reopen F2 to retry");
            owner.Status="Opening native Stash panel...";return false;
        }
        SourcePanelKind=source.TryCast<StashPanelV2>()!=null?"StashPanelV2":"StashPanel";
        LegacySingletonMissing=StashPanelUI.Instance==null;
        var sourceGrid=source._stash??source.GetComponentsInChildren<ItemContainerUI>(true).FirstOrDefault(g=>g.identifier==ContainerID.STASH);
        if(sourceGrid==null)throw new InvalidOperationException("Native stash prefab has no item grid");
        var path=Indices(source.transform,sourceGrid.transform);
        dimensions=sourceGrid.Container?.GetSize()??new Vector2Int(12,20);
        if(dimensions.x<4 || dimensions.y<4 || dimensions.x>32 || dimensions.y>40)dimensions=new(12,20);
        // Inactive parent prevents cloned native Awake/OnEnable. Remove all
        // gameplay/controller/navigation callbacks before activating any child.
        parking=new GameObject("Shared Stash inactive staging");parking.SetActive(false);
        root=UnityEngine.Object.Instantiate(source.gameObject,parking.transform,false);root.name="LAN Shared Stash - independent native view";
        RestoreStagingVisibility(true);
        CaptureNativeTabArtwork(source);
        // Preserve native button colors/sprites/transitions, remove only their
        // personal-stash callbacks. Selected shared actions are rebound below.
        foreach(var button in root.GetComponentsInChildren<UnityEngine.UI.Button>(true))
        {button.onClick=new UnityEngine.UI.Button.ButtonClickedEvent();button.interactable=false;}
        var gridTransform=root.transform;foreach(int index in path)gridTransform=gridTransform.GetChild(index);
        foreach(var item in root.GetComponentsInChildren<InventoryItemUI>(true))UnityEngine.Object.DestroyImmediate(item.gameObject);
        foreach(var script in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            bool graphic=script.TryCast<Graphic>()!=null;
            bool layout=script.TryCast<LayoutGroup>()!=null || script.TryCast<ContentSizeFitter>()!=null || script.TryCast<LayoutElement>()!=null || script.TryCast<Mask>()!=null || script.TryCast<RectMask2D>()!=null || script.TryCast<TMP_InputField>()!=null || script.TryCast<ScrollRect>()!=null || script.TryCast<Scrollbar>()!=null;
            if(!graphic && !layout && script.TryCast<UnityEngine.UI.Button>()==null)UnityEngine.Object.DestroyImmediate(script);
        }
        // Artwork kept from the prefab must not intercept item clicks after its
        // original controllers have been removed. Preserve hidden child groups.
        foreach(var graphic in root.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=false;
        SharedStashNativeLayout.Restore(source.transform,root.transform);
        var rootGroup=root.GetComponent<CanvasGroup>()??root.AddComponent<CanvasGroup>();
        rootGroup.alpha=1;rootGroup.interactable=true;rootGroup.blocksRaycasts=true;rootGroup.ignoreParentGroups=true;
        // Raycasters are MonoBehaviours too: stripping the source controllers
        // removed them. Each retained Canvas needs its own new input raycaster.
        var canvases=root.GetComponentsInChildren<Canvas>(true).ToArray();
        if(canvases.Length==0)canvases=new[]{root.AddComponent<Canvas>()};
        foreach(var canvas in canvases)
        {
            canvas.enabled=true;
            if(canvas.GetComponent<GraphicRaycaster>()==null)canvas.gameObject.AddComponent<GraphicRaycaster>();
        }
        foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))
            if(text.text?.Trim().Equals("STASH",StringComparison.OrdinalIgnoreCase)==true)text.text="SHARED STASH";
        var searchGrid=gridTransform.gameObject.AddComponent<ItemContainerUIWithSearch>();
        grid=searchGrid;
        var sourceHighlight=sourceGrid.TryCast<ItemContainerUIWithHighlight>();
        if(sourceHighlight!=null)
        {
            searchGrid.hilightPrefab=sourceHighlight.hilightPrefab;
            searchGrid._highlightContainer=Cloned(source.transform,sourceHighlight._highlightContainer)?.GetComponent<RectTransform>();
        }
        grid.identifier=ContainerID.STASH;grid.interactable=true;grid.forceFillRect=sourceGrid.forceFillRect;
        grid.disableItemBackground=sourceGrid.disableItemBackground;grid.slotSize=sourceGrid.slotSize;
        grid.initialiseOnEnableBehaviour=ItemContainerUI.InitialiseOnEnableBehaviour.Default;
        grid.ShouldSelectOnInitialization=false;
        var gridGraphic=gridTransform.GetComponent<Graphic>();
        if(gridGraphic==null){var input=gridTransform.gameObject.AddComponent<Image>();input.color=new Color(0,0,0,0.001f);gridGraphic=input;}
        gridGraphic.raycastTarget=true;
        pages.Add(NewPage());Active=this;
        root.SetActive(false);root.transform.SetParent(source.transform.parent,false);
        BuildSharedTabControls();
        if(source.gameObject.activeInHierarchy)
        {
            // The public API pops the panel stack before native focus cleanup.
            // Calling ClosePanelInternal directly leaves a stale focused panel.
            var v2=source.TryCast<StashPanelV2>();
            if(v2!=null)panels.ClosePanel(v2);else panels.ClosePanel(source);
        }
        RestoreStagingVisibility(false);
        root.SetActive(true);grid.Initialize();
        UIBase.instance.openInventory(false,false,false,0,false);
        if(!owner.Busy)owner.Status="Drop items to deposit | Click to withdraw | F2 / Esc to close";
        Error="";return true;
    }
    private ItemContainer NewPage()=>new(manager,dimensions,ContainerType.STASH,ContainerID.STASH,manager.inventory.getCharacterInfoProvider(),null,(Rarity)127);
    private void Rebuild(ChestReply snapshot)
    {
        if(grid==null)return;
        grid.ResetInternal(true,true);pages.Clear();displayed.Clear();itemButtons.Clear();Populating=true;
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
        grid.TryCast<ItemContainerUIWithSearch>()?.itemStringCache?.Clear();ApplyNativeSearch();
        if(BoundToPrivateStash)throw new InvalidOperationException("Shared projection accidentally bound to personal stash");
    }
    private void Flip(int delta)
    {if(grid==null || pages.Count==0)return;grid.ResetInternal(true,true);itemButtons.Clear();page=(page+delta+pages.Count)%pages.Count;grid.Initialize();}
    private void BindItemActions()
    {
        if(grid?.currentlyShown==null)return;
        foreach(var itemUi in grid.currentlyShown)
        {
            var entry=itemUi?.EntryRef;if(entry?.data==null)continue;
            uint id=entry.data.individualID;
            if(!displayed.ContainsKey(id) || itemButtons.TryGetValue(itemUi.Pointer,out uint bound) && bound==id)continue;
            itemUi.SetInteractionStatus(true);
            // Native item icons deliberately let input pass through to the
            // game's original grid. Our per-item buttons need real hit targets.
            var group=itemUi.GetComponent<CanvasGroup>();
            if(group!=null){group.interactable=true;group.blocksRaycasts=true;}
            foreach(var canvas in itemUi.GetComponentsInChildren<Canvas>(true))
                if(canvas.GetComponent<GraphicRaycaster>()==null)canvas.gameObject.AddComponent<GraphicRaycaster>();
            var graphic=itemUi.backgroundImage;
            if(graphic==null)throw new InvalidOperationException("Native item has no clickable background");
            graphic.raycastTarget=true;
            var button=itemUi.GetComponent<UnityEngine.UI.Button>()??itemUi.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic=graphic;button.interactable=true;button.transition=Selectable.Transition.None;
            button.onClick.RemoveAllListeners();button.onClick.AddListener((Action)(()=>WithdrawById(id)));
            itemButtons[itemUi.Pointer]=id;
        }
    }
    private void WithdrawById(uint id)
    {
        Interactions++;
        if(!Visible || !displayed.TryGetValue(id,out var item) || !pages[page].GetContent().ToArray().Any(e=>e.data.individualID==id))
        {owner.Status="Stash view changed; reopen F2 to refresh";return;}
        if(manager.cursor.GetContent()?.Count>0){owner.Status="Put the held item into inventory before withdrawing";return;}
        owner.NativeWithdraw(item);
    }
    internal string QaClickItem(uint id)
    {
        if(!Environment.GetCommandLineArgs().Contains("--lelan-shared-chest-smoke"))throw new InvalidOperationException("Private chest QA flag required");
        BindItemActions();Canvas.ForceUpdateCanvases();
        var ui=grid.currentlyShown.ToArray().Single(i=>i.EntryRef?.data?.individualID==id);
        var canvas=ui.GetComponentInParent<Canvas>().rootCanvas;
        var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
        var position=RectTransformUtility.WorldToScreenPoint(camera,ui.RectTransform.TransformPoint(ui.RectTransform.rect.center));
        var data=new PointerEventData(EventSystem.current){position=position,button=PointerEventData.InputButton.Left,eligibleForClick=true};
        var hits=new Il2CppSystem.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),"UserData","shared-stash-click-diagnostics.json"),System.Text.Json.JsonSerializer.Serialize(new{
            screen=new{Screen.width,Screen.height},position=new{position.x,position.y},item=ui.name,active=ui.gameObject.activeInHierarchy,
            rect=new{ui.RectTransform.rect.x,ui.RectTransform.rect.y,ui.RectTransform.rect.width,ui.RectTransform.rect.height},
            background=new{ui.backgroundImage.enabled,ui.backgroundImage.raycastTarget,ui.backgroundImage.canvasRenderer.cull,ui.backgroundImage.depth,
                valid=ui.backgroundImage.Raycast(position,camera),inside=RectTransformUtility.RectangleContainsScreenPoint(ui.backgroundImage.rectTransform,position,camera),forward=ui.backgroundImage.transform.forward.z},
            canvases=ui.GetComponentsInParent<Canvas>(true).ToArray().Select(c=>new{c.name,c.enabled,mode=c.renderMode.ToString(),camera=c.worldCamera?.name,c.sortingOrder}),
            groups=ui.GetComponentsInParent<CanvasGroup>(true).ToArray().Select(g=>new{g.name,g.alpha,g.interactable,g.blocksRaycasts,g.ignoreParentGroups}),
            raycasters=root.GetComponentsInChildren<GraphicRaycaster>(true).ToArray().Select(r=>new{r.name,r.enabled,active=r.gameObject.activeInHierarchy,camera=r.eventCamera?.name}),
            hits=hits.ToArray().Select(h=>new{name=h.gameObject?.name,module=h.module?.GetIl2CppType()?.Name})}));
        if(hits.Count==0)throw new InvalidOperationException("Native item screen position has no UI raycast hit");
        var hit=hits[0];data.pointerCurrentRaycast=hit;data.pointerPressRaycast=hit;data.pointerEnter=hit.gameObject;
        var receiver=ExecuteEvents.ExecuteHierarchy<IPointerClickHandler>(hit.gameObject,data,ExecuteEvents.pointerClickHandler);
        if(receiver==null || receiver.Pointer!=ui.gameObject.Pointer)throw new InvalidOperationException("Item click intercepted by "+hit.gameObject.name+" / "+receiver?.name);
        return receiver.name;
    }
    internal void Click(ItemContainerUI ui,PointerEventData data)
    {
        Interactions++;
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
        RestoreStagingVisibility(false);
        if(root!=null)root.SetActive(false);
        if(grid!=null){try{grid.ResetInternal(true,true);}catch{}}
        if(root!=null)UnityEngine.Object.Destroy(root);if(parking!=null)UnityEngine.Object.Destroy(parking);
        DisposeSharedTabs();
        root=null;parking=null;grid=null;manager=null;pages.Clear();displayed.Clear();itemButtons.Clear();revision=-1;projectedItems=Array.Empty<ChestItem>();chestId="";loadStarted=-1;loadFailed=false;
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
