using Il2Cpp;
using Il2CppTMPro;
using Il2CppCysharp.Threading.Tasks;
using Il2CppLE.UI.PanelSystem;
using LastEpochLAN.Core;
using UnityEngine;
using UnityEngine.UI;
using Il2CppInterop.Runtime;

namespace LastEpochLAN.Mod;

internal sealed partial class NativeSharedStashView
{
    private Transform sharedTabHolder;
    private ScrollRect tabScroll;
    private int scrollTab=-1;
    private GameObject tabArtwork,sortArtwork,configureArtwork,addArtwork;
    private int[] tabNamePath,tabIconPath,tabSelectionPath;
    private Color[] tabColors=Array.Empty<Color>();
    private Sprite[] tabIcons=Array.Empty<Sprite>();
    private readonly List<GameObject> sharedTabVisuals=new();
    private SharedStashTab[] renderedTabs=Array.Empty<SharedStashTab>();
    private UnityEngine.UI.Button sortButton,configureButton,addButton;
    private TMP_Text sharedTabTitle;
    private Image sharedTabTitleIcon;
    private UniTask<StashConfigureTabModalResult>.Awaiter configureAwaiter;
    internal bool Configuring {get;private set;}
    private int configureTab=-1,shownTab=-1;
    private string configureChestId="";
    private bool awaitingTab;
    private readonly List<(GameObject Widget,bool Active)> hiddenModalWidgets=new();
    private IntPtr constrainedModal;

    private Transform Cloned(Transform sourceRoot,Transform original)
    {
        if(original==null)return null;
        var target=root.transform;
        foreach(int index in Indices(sourceRoot,original))target=target.GetChild(index);
        return target;
    }
    private static Transform At(Transform parent,int[] path)
    {if(path==null)return null;foreach(int index in path)parent=parent.GetChild(index);return parent;}
    private static void StripTabScripts(GameObject artwork)
    {
        foreach(var script in artwork.GetComponentsInChildren<MonoBehaviour>(true))
        {
            bool keep=script.TryCast<Graphic>()!=null || script.TryCast<LayoutGroup>()!=null || script.TryCast<ContentSizeFitter>()!=null ||
                script.TryCast<LayoutElement>()!=null || script.TryCast<Mask>()!=null || script.TryCast<RectMask2D>()!=null;
            if(!keep)UnityEngine.Object.DestroyImmediate(script);
        }
        foreach(var graphic in artwork.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=false;
    }
    private void CaptureNativeTabArtwork(StashPanel source)
    {
        var controls=source.GetComponentInChildren<NewStashTabbedUIControls>(true);
        if(controls==null)return;
        sharedTabHolder=Cloned(source.transform,controls._tabHolder);
        tabScroll=Cloned(source.transform,controls._tabsScrollRect?.transform)?.GetComponent<ScrollRect>();
        sortArtwork=Cloned(source.transform,controls._sortButton?.transform)?.gameObject;
        configureArtwork=Cloned(source.transform,controls._configureButton?.transform)?.gameObject;
        addArtwork=Cloned(source.transform,controls._addButton?.transform)?.gameObject;
        if(controls._tabInfo!=null)
        {
            sharedTabTitle=Cloned(source.transform,controls._tabInfo._label?.transform)?.GetComponent<TMP_Text>();
            sharedTabTitleIcon=Cloned(source.transform,controls._tabInfo._icon?.transform)?.GetComponent<Image>();
            Cloned(source.transform,controls._tabInfo._priorityBanner?.transform)?.gameObject.SetActive(false);
        }
        if(controls._folderInfo!=null)
        {
            var folderLabel=Cloned(source.transform,controls._folderInfo._label?.transform)?.GetComponent<TMP_Text>();
            if(folderLabel!=null)folderLabel.text="Shared Stash";
            Cloned(source.transform,controls._folderInfo._priorityBanner?.transform)?.gameObject.SetActive(false);
        }
        tabColors=controls._tabColors?.ToArray()??Array.Empty<Color>();
        tabIcons=controls._tabIcons?.ToArray()??Array.Empty<Sprite>();
        var template=source.GetComponentsInChildren<StashTabUI>(true).FirstOrDefault(t=>!t._quickViewTab);
        if(template==null)return;
        tabNamePath=template._tabName!=null?Indices(template.transform,template._tabName.transform):null;
        tabIconPath=template._icon!=null?Indices(template.transform,template._icon.transform):null;
        tabSelectionPath=template._selectedBackground!=null?Indices(template.transform,template._selectedBackground.transform):null;
        tabArtwork=UnityEngine.Object.Instantiate(template.gameObject,parking.transform,false);
        tabArtwork.name="Shared native tab artwork";tabArtwork.SetActive(false);StripTabScripts(tabArtwork);
        // A copied personal tab may contain a private item-count label. Shared
        // counts are shown in the selected-tab footer, never copied from it.
        if(template._itemsCount!=null)At(tabArtwork.transform,Indices(template.transform,template._itemsCount.transform))?.gameObject.SetActive(false);
        if(template._priorityBanner!=null)At(tabArtwork.transform,Indices(template.transform,template._priorityBanner.transform))?.gameObject.SetActive(false);
        if(template._breadcrumbPip!=null)At(tabArtwork.transform,Indices(template.transform,template._breadcrumbPip.transform))?.gameObject.SetActive(false);
    }
    private UnityEngine.UI.Button BindArtwork(GameObject artwork,Action action)
    {
        if(artwork==null)return null;
        var graphic=artwork.GetComponent<Graphic>();
        if(graphic==null){var hit=artwork.AddComponent<Image>();hit.color=new(0,0,0,.001f);graphic=hit;}
        artwork.SetActive(true);
        var group=artwork.GetComponent<CanvasGroup>()??artwork.AddComponent<CanvasGroup>();
        group.alpha=1;group.interactable=true;group.blocksRaycasts=true;group.ignoreParentGroups=true;
        graphic.raycastTarget=true;
        var button=artwork.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=graphic;
        button.onClick.RemoveAllListeners();button.onClick.AddListener((Action)(()=>
        {try{action();}catch(Exception ex){owner.Status="Shared Stash: "+ex.Message;MelonLoader.MelonLogger.Warning(owner.Status);}}));
        return button;
    }
    private void BuildSharedTabControls()
    {
        // Keep native button artwork/placement but replace every callback. None
        // of the personal-stash controllers survives the projection clone.
        sortButton=BindArtwork(sortArtwork,SortSharedTab);
        configureButton=BindArtwork(configureArtwork,ConfigureSharedTab);
        addButton=BindArtwork(addArtwork,owner.AddTab);
        if(sharedTabHolder!=null)
        {
            // Keep the native ScrollRect/layout and put a flat list of shared
            // tabs into it. Never retain personal tab names or folder widgets.
            bool addInside=addArtwork!=null && addArtwork.transform.IsChildOf(sharedTabHolder);
            if(addInside)addArtwork.transform.SetParent(parking.transform,false);
            for(int i=sharedTabHolder.childCount-1;i>=0;i--)UnityEngine.Object.DestroyImmediate(sharedTabHolder.GetChild(i).gameObject);
            if(addInside)addArtwork.transform.SetParent(sharedTabHolder,false);
        }
        else
        {
            var row=new GameObject("Shared tab navigation",Il2CppType.Of<RectTransform>());row.transform.SetParent(root.transform,false);
            var rect=row.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new(.5f,1);
            rect.anchoredPosition=new(0,-44);rect.sizeDelta=new(390,34);
            row.AddComponent<Image>().color=new(0,0,0,.001f);row.AddComponent<RectMask2D>();
            var content=new GameObject("Shared tab scroll content",Il2CppType.Of<RectTransform>());content.transform.SetParent(row.transform,false);
            var contentRect=content.GetComponent<RectTransform>();contentRect.anchorMin=contentRect.anchorMax=contentRect.pivot=new(0,.5f);contentRect.sizeDelta=new(390,34);
            var layout=content.AddComponent<HorizontalLayoutGroup>();layout.spacing=3;layout.childForceExpandWidth=false;layout.childControlWidth=false;
            content.AddComponent<ContentSizeFitter>().horizontalFit=ContentSizeFitter.FitMode.PreferredSize;
            tabScroll=row.AddComponent<ScrollRect>();tabScroll.viewport=rect;tabScroll.content=contentRect;tabScroll.horizontal=true;tabScroll.vertical=false;
            tabScroll.movementType=ScrollRect.MovementType.Clamped;sharedTabHolder=content.transform;
        }
        if(tabScroll!=null)
        {
            tabScroll.onValueChanged.RemoveAllListeners();
            for(int i=0;i<tabScroll.onValueChanged.GetPersistentEventCount();i++)
                tabScroll.onValueChanged.SetPersistentListenerState(i,UnityEngine.Events.UnityEventCallState.Off);
            var viewport=tabScroll.viewport??tabScroll.GetComponent<RectTransform>();
            var hit=viewport.GetComponent<Graphic>();
            if(hit==null){var image=viewport.gameObject.AddComponent<Image>();image.color=new(0,0,0,.001f);hit=image;}
            hit.raycastTarget=true;
            foreach(var bar in new[]{tabScroll.horizontalScrollbar,tabScroll.verticalScrollbar})
            {
                if(bar==null)continue;
                // ScrollRect hooks its own scrollbar events when activated.
                // Disable only serialized callbacks to stripped controllers.
                for(int i=0;i<bar.onValueChanged.GetPersistentEventCount();i++)
                    bar.onValueChanged.SetPersistentListenerState(i,UnityEngine.Events.UnityEventCallState.Off);
                if(bar.targetGraphic!=null)bar.targetGraphic.raycastTarget=true;
            }
        }
        if(sortButton==null)Button("Shared sort",new(0,1),new(10,-8),new(65,30),"SORT",SortSharedTab);
        if(configureButton==null)Button("Shared configure",new(.5f,1),new(0,-8),new(150,30),"CONFIGURE TAB",ConfigureSharedTab);
        if(addButton==null)Button("Shared add tab",new(1,1),new(-60,-8),new(38,30),"+",owner.AddTab);
    }
    private void RefreshSharedTabs(ChestReply snapshot)
    {
        var activeTab=snapshot.Tabs[snapshot.Tab];
        if(sharedTabTitle!=null)sharedTabTitle.text=activeTab.Name;
        if(sharedTabTitleIcon!=null)
        {
            if(activeTab.Icon<tabIcons.Length)sharedTabTitleIcon.sprite=tabIcons[activeTab.Icon];
            if(activeTab.Color<tabColors.Length)sharedTabTitleIcon.color=tabColors[activeTab.Color];
        }
        if(!renderedTabs.SequenceEqual(snapshot.Tabs))
        {
            foreach(var visual in sharedTabVisuals){visual.SetActive(false);UnityEngine.Object.Destroy(visual);}
            sharedTabVisuals.Clear();renderedTabs=snapshot.Tabs.ToArray();
            foreach(var tab in renderedTabs)
            {
                var visual=tabArtwork!=null?UnityEngine.Object.Instantiate(tabArtwork,sharedTabHolder,false):
                    new GameObject("Shared tab",Il2CppType.Of<RectTransform>());
                visual.transform.SetParent(sharedTabHolder,false);visual.name="Shared tab "+tab.Id;
                if(tabArtwork==null)
                {
                    var rect=visual.GetComponent<RectTransform>();rect.sizeDelta=new(62,30);
                    visual.AddComponent<Image>().color=new(.12f,.1f,.06f,.95f);
                    var label=Label("Shared tab name",new(.5f,.5f),Vector2.zero,new(60,28),(tab.Id+1).ToString());
                    label.transform.SetParent(visual.transform,false);label.alignment=TextAlignmentOptions.Center;
                }
                else
                {
                    var label=At(visual.transform,tabNamePath)?.GetComponent<TMP_Text>();if(label!=null)label.text=tab.Name;
                    var icon=At(visual.transform,tabIconPath)?.GetComponent<Image>();
                    if(icon!=null)
                    {
                        if(tab.Icon<tabIcons.Length)icon.sprite=tabIcons[tab.Icon];
                        if(tab.Color<tabColors.Length)icon.color=tabColors[tab.Color];
                    }
                }
                int id=tab.Id;BindArtwork(visual,()=>owner.SelectTab(id));visual.SetActive(true);sharedTabVisuals.Add(visual);
            }
        }
        if(addArtwork!=null && addArtwork.transform.parent==sharedTabHolder)addArtwork.transform.SetAsLastSibling();
        for(int i=0;i<sharedTabVisuals.Count;i++)
        {
            var selected=tabArtwork!=null?At(sharedTabVisuals[i].transform,tabSelectionPath)?.gameObject:null;
            if(selected!=null)selected.SetActive(i==owner.SelectedTab);
            var button=sharedTabVisuals[i].GetComponent<UnityEngine.UI.Button>();if(button!=null)button.interactable=owner.TabControlsReady && i!=owner.SelectedTab;
        }
        if(tabScroll!=null && scrollTab!=owner.SelectedTab)
        {
            Canvas.ForceUpdateCanvases();
            if(tabScroll.horizontal)tabScroll.horizontalNormalizedPosition=owner.SelectedTab/(float)Math.Max(1,renderedTabs.Length-1);
            else if(tabScroll.vertical)tabScroll.verticalNormalizedPosition=1-owner.SelectedTab/(float)Math.Max(1,renderedTabs.Length-1);
            scrollTab=owner.SelectedTab;
        }
        if(sortButton!=null)sortButton.interactable=owner.TabControlsReady;
        if(configureButton!=null)configureButton.interactable=owner.TabControlsReady;
        if(addButton!=null)addButton.interactable=owner.TabControlsReady && renderedTabs.Length<SharedStashRules.MaxTabs;
    }
    private void SortSharedTab()
    {
        if(!owner.TabControlsReady){owner.Status="Wait for the current stash operation";return;}
        if(manager.cursor.GetContent()?.Count>0){owner.Status="Put the held item back before sorting";return;}
        if(displayed.Count==0){owner.SortTab(Array.Empty<string>());return;}
        var ordered=new List<string>();
        // Sort all items together, including those on overflow grid pages. A
        // tall isolated buffer fits every item; it is never bound to a player
        // stash or shown by ItemContainerUI, and only the item-ID permutation
        // goes to Host. Real custody and serialized item bytes do not change.
        int height=Math.Max(dimensions.y,pages.SelectMany(p=>p.GetContent().ToArray()).Sum(e=>Math.Max(1,e.size.y)));
        var buffer=new ItemContainer(manager,new Vector2Int(dimensions.x,height),ContainerType.STASH,ContainerID.STASH,
            manager.inventory.getCharacterInfoProvider(),null,(Il2CppItemFiltering.RarityCondition.Rarity)127);
        Populating=true;
        try
        {
            foreach(var item in displayed.Values)
                if(!buffer.TryAddItem(new ItemDataUnpacked(Convert.FromBase64String(item.Data)),1,Context.GIFTING))
                    throw new InvalidOperationException("Native sort buffer cannot fit all shared items");
            if(!buffer.TrySort()){owner.Status="Native sort is not ready; try again";return;}
            foreach(var entry in buffer.GetContent().ToArray().OrderBy(e=>e.Position.y).ThenBy(e=>e.Position.x))
                ordered.Add(displayed[entry.data.individualID].Id);
        }
        finally{Populating=false;}
        owner.SortTab(ordered.ToArray());
    }
    private void ConfigureSharedTab()
    {
        if(!owner.TabControlsReady){owner.Status="Wait for the current stash operation";return;}
        var tab=owner.StashTabs[owner.SelectedTab];
        var names=new Il2CppSystem.Collections.Generic.List<string>();names.Add("Shared Stash");
        var ids=new Il2CppSystem.Collections.Generic.List<int>();ids.Add(0);
        var data=new StashConfigureTabModalOpenData(tab.Icon,tab.Color,tab.Name,tab.Id,0,names,ids,false,new StashPriority(),true,false,
            new StashPriority(),new Il2CppSystem.Collections.Generic.List<string>(),new Il2CppSystem.Collections.Generic.List<int>());
        configureAwaiter=UIBase.instance.PanelSystem.OpenModalAsync<StashConfigureTabModalV2,StashConfigureTabModalOpenData,StashConfigureTabModalResult>(
            data,new Il2CppSystem.Nullable<PanelLocation>()).GetAwaiter();
        configureTab=tab.Id;configureChestId=owner.QaView?.ChestId??"";Configuring=true;
    }
    internal void InvalidateConfiguration()=>configureChestId="";
    internal void PollTabConfiguration()
    {
        if(!Configuring)return;
        var modal=UIBase.instance?.PanelSystem?.GetPanelIfOpen<StashConfigureTabModalV2>();
        if(modal!=null && modal.Pointer!=constrainedModal)
        {
            constrainedModal=modal.Pointer;
            var controls=modal._configureTabUI;
            if(controls!=null)
            {
                foreach(var widget in new[]{controls._duplicateTabButton?.gameObject,controls._deleteCategoryButton?.gameObject,
                    controls._parentSelectDropdown?.gameObject,controls._childTabsDropdown?.gameObject,
                    controls._priorityHeader?.gameObject,controls._ignoreFolderPriority?.gameObject,controls._stashPriorityUI?.gameObject})
                {
                    if(widget==null)continue;
                    hiddenModalWidgets.Add((widget,widget.activeSelf));widget.SetActive(false);
                }
            }
        }
        if(!configureAwaiter.IsCompleted)return;
        try
        {
            var result=configureAwaiter.GetResult();Configuring=false;
            if(result==null || !owner.Open || configureTab!=owner.SelectedTab || owner.QaView?.ChestId!=configureChestId)return;
            if(result.Action!=StashConfigureResult.Save){owner.Status="Shared tabs support name, color and icon configuration";return;}
            owner.ConfigureTab(new(configureTab,result.TabName.Trim(),result.SelectedColorId,result.SelectedIconId));
        }
        catch(Exception ex){Configuring=false;owner.Status="Shared tab configuration: "+ex.Message;}
        finally
        {
            foreach(var (widget,active) in hiddenModalWidgets)if(widget!=null)widget.SetActive(active);
            hiddenModalWidgets.Clear();constrainedModal=IntPtr.Zero;
        }
    }
    private void DisposeSharedTabs()
    {
        sharedTabHolder=null;tabScroll=null;scrollTab=-1;tabArtwork=null;sortArtwork=configureArtwork=addArtwork=null;sharedTabTitle=null;sharedTabTitleIcon=null;
        sharedTabVisuals.Clear();renderedTabs=Array.Empty<SharedStashTab>();sortButton=configureButton=addButton=null;
        shownTab=-1;awaitingTab=false;
        // An open native modal owns its own lifetime. Poll it before the next
        // projection tick; ignore results if F2 was closed or the tab changed.
    }
}
