using Il2Cpp;
using Il2CppTMPro;
using Il2CppInterop.Runtime;
using Il2CppLE.UI.PanelSystem;
using UnityEngine;
using UnityEngine.UI;

namespace LastEpochLAN.Mod;

internal sealed partial class NativeSharedStashView
{
    private GameObject closeArtwork;
    private TMP_InputField nativeSearch;
    private readonly List<(GameObject Artwork,Action Action)> nativeNavigation=new();
    private SimpleLayoutGroup sourceTabLayout,nativeTabLayout;
    private int[] legacyTabImagePath,legacyOrnamentPath;
    private Sprite legacyActiveTab,legacyInactiveTab;
    private string sharedSearch="";
    internal string NativeControlsKind {get;private set;}="";

    private void CaptureSearch(StashPanel source,TMP_InputField input)
    {
        nativeSearch=Cloned(source.transform,input?.transform)?.GetComponent<TMP_InputField>();
        if(nativeSearch==null)return;
        // Replace the event objects, including serialized persistent callbacks.
        // RemoveAllListeners alone does not remove Inspector callbacks.
        nativeSearch.onValueChanged=new TMP_InputField.OnChangeEvent();
        nativeSearch.onEndEdit=new TMP_InputField.SubmitEvent();
        nativeSearch.onSubmit=new TMP_InputField.SubmitEvent();
        nativeSearch.onSelect=new TMP_InputField.SelectionEvent();
        nativeSearch.onDeselect=new TMP_InputField.SelectionEvent();
        nativeSearch.SetTextWithoutNotify("");
    }

    private void CaptureLegacyTabArtwork(StashPanel source)
    {
        var controls=source._stashTabbedUIControls??source.GetComponentInChildren<StashTabbedUIControls>(true);
        if(controls==null)throw new InvalidOperationException("Native Stash tab controller is not ready; reopen F2");
        NativeControlsKind="StashTabbedUIControls";
        sharedTabHolder=Cloned(source.transform,controls.tabHolder);
        tabScroll=Cloned(source.transform,controls.tabsScrollRect?.transform)?.GetComponent<ScrollRect>();
        addArtwork=Cloned(source.transform,controls.addTabButton)?.gameObject;
        tabColors=controls.tabColors?.ToArray()??Array.Empty<Color>();
        tabIcons=controls.tabIcons?.ToArray()??Array.Empty<Sprite>();
        sourceTabLayout=controls.tabLayoutGroup;
        var navigation=source.GetComponentInChildren<StashSortConfigureSearchButtonsNavigable>(true);
        sortArtwork=Cloned(source.transform,navigation?.sortBtn?.transform)?.gameObject;
        configureArtwork=Cloned(source.transform,navigation?.configureBtn?.transform)?.gameObject;
        CaptureSearch(source,controls.primarySearchBar);
        CaptureNativeNavigation(source);
        // The horizontal shared tab strip replaces personal tabs. Keep just
        // one native category decoration; there is no shared folder protocol.
        HideCloned(source,controls.addCategoryButton);
        HideCloned(source,controls.verticalTabWrapper?.transform);
        HideCloned(source,controls.cycleDropdown?.transform);
        HideCloned(source,controls.cycleMergeButton?.transform);
        HideCloned(source,controls.gridBlocker?.transform);
        HideCloned(source,controls.removeOnlyTitle?.transform);
        var categories=Cloned(source.transform,controls.categoryHolder);
        if(categories!=null)
        {
            var first=categories.GetComponentsInChildren<TabbedUITabWithIcon>(true).FirstOrDefault(t=>t.gameObject.activeSelf);
            foreach(var tab in categories.GetComponentsInChildren<TabbedUITabWithIcon>(true))
            {
                var label=tab.tabName;
                if(tab!=first)tab.gameObject.SetActive(false);
                else if(label!=null)label.text="Shared Stash";
            }
        }
        var template=controls.tabPrefab?.GetComponent<TabbedUITabWithIcon>()??
            source.GetComponentsInChildren<TabbedUITabWithIcon>(true).FirstOrDefault(t=>t.isTab && !t.vertTab);
        if(template==null)throw new InvalidOperationException("Native Stash tab prefab is unavailable");
        tabNamePath=template.tabName!=null?Indices(template.transform,template.tabName.transform):null;
        tabIconPath=template.icon!=null?Indices(template.transform,template.icon.transform):null;
        legacyTabImagePath=template.tabImage!=null?Indices(template.transform,template.tabImage.transform):null;
        legacyOrnamentPath=template.activeOrnament!=null?Indices(template.transform,template.activeOrnament.transform):null;
        legacyActiveTab=template.activeTab;legacyInactiveTab=template.inactiveTab;
        tabArtwork=UnityEngine.Object.Instantiate(template.gameObject,parking.transform,false);
        tabArtwork.name="Shared native tab artwork";tabArtwork.SetActive(false);StripTabScripts(tabArtwork);
    }

    private void HideCloned(StashPanel source,Transform widget)
        =>Cloned(source.transform,widget)?.gameObject.SetActive(false);

    private void CaptureNativeNavigation(StashPanel source)
    {
        if(NativeControlsKind=="")NativeControlsKind="NewStashTabbedUIControls";
        foreach(var button in source.GetComponentsInChildren<UnityEngine.UI.Button>(true))
        {
            var artwork=Cloned(source.transform,button.transform)?.gameObject;
            for(int i=0;i<button.onClick.GetPersistentEventCount();i++)
            {
                var method=button.onClick.GetPersistentMethodName(i);
                switch(method)
                {
                    case "Sort":case "SortItems":case "OnSortClicked":sortArtwork??=artwork;break;
                    case "ConfigureActiveTab":case "OnConfigureClicked":configureArtwork??=artwork;break;
                    case "ScrollTabsLeft":nativeNavigation.Add((artwork,()=>ScrollNativeTabs(-1)));break;
                    case "ScrollTabsRight":nativeNavigation.Add((artwork,()=>ScrollNativeTabs(1)));break;
                    case "ScrollCategoryLeft":case "ScrollCategoryRight":
                    case "AddNewCategory":case "ConfigureActiveCategory":
                        artwork?.SetActive(false);break;
                    case "ClearSearchField":case "ClearSearch":
                        nativeNavigation.Add((artwork,()=>{if(nativeSearch!=null)nativeSearch.text="";}));break;
                    case "Close":case "ClosePanel":case "OnCloseClicked":case "CloseStash":
                        closeArtwork??=artwork;break;
                }
            }
            // Some panel prefabs wire the close action at runtime in Awake.
            if(closeArtwork==null && button.name.Contains("close",StringComparison.OrdinalIgnoreCase))closeArtwork=artwork;
        }
    }

    private void ScrollNativeTabs(int direction)
    {
        if(tabScroll==null)return;
        if(tabScroll.horizontal)tabScroll.horizontalNormalizedPosition=Mathf.Clamp01(tabScroll.horizontalNormalizedPosition+direction*.25f);
        else if(tabScroll.vertical)tabScroll.verticalNormalizedPosition=Mathf.Clamp01(tabScroll.verticalNormalizedPosition-direction*.25f);
    }

    private void RestoreNativeTabLayout()
    {
        if(sourceTabLayout==null)return;
        // Recreate only the native layout engine, with fresh element records.
        // The copied engine's records still point at destroyed personal tabs.
        nativeTabLayout=sharedTabHolder.gameObject.AddComponent<SimpleLayoutGroup>();
        nativeTabLayout._rectTransform=sharedTabHolder.GetComponent<RectTransform>();
        nativeTabLayout._layoutMode=sourceTabLayout._layoutMode;
        nativeTabLayout._padding=sourceTabLayout._padding;
        nativeTabLayout._spacing=sourceTabLayout._spacing;
        nativeTabLayout._childAlignment=sourceTabLayout._childAlignment;
        nativeTabLayout._adaptHorizontalSize=sourceTabLayout._adaptHorizontalSize;
        nativeTabLayout._adaptVerticalSize=sourceTabLayout._adaptVerticalSize;
        nativeTabLayout._minWidth=sourceTabLayout._minWidth;
        nativeTabLayout._minHeight=sourceTabLayout._minHeight;
        nativeTabLayout._preferredWidth=sourceTabLayout._preferredWidth;
        nativeTabLayout._preferredHeight=sourceTabLayout._preferredHeight;
    }

    private void RefreshNativeTabLayout()
    {
        if(nativeTabLayout==null)return;
        nativeTabLayout.Elements.Clear();
        foreach(var visual in sharedTabVisuals)nativeTabLayout.AddElement(visual);
        if(addArtwork.transform.parent==sharedTabHolder)nativeTabLayout.AddElement(addArtwork);
        nativeTabLayout.UpdateLayout();
    }

    private void RefreshLegacyTabSelection(GameObject tab,bool selected)
    {
        var image=At(tab.transform,legacyTabImagePath)?.GetComponent<Image>();
        if(image!=null)image.sprite=selected?legacyActiveTab:legacyInactiveTab;
        var ornament=At(tab.transform,legacyOrnamentPath)?.gameObject;
        if(ornament!=null)ornament.SetActive(selected);
    }

    private void BindNativeSearch()
    {
        if(nativeSearch==null)return;
        nativeSearch.interactable=true;
        foreach(var graphic in nativeSearch.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=true;
        nativeSearch.onValueChanged.AddListener((Action<string>)(value=>
        {
            sharedSearch=value??"";ApplyNativeSearch();
        }));
    }

    private void ApplyNativeSearch()
    {
        var search=grid?.TryCast<ItemContainerUIWithSearch>();
        if(search==null)return;
        search.lastSearch=sharedSearch;
        search.PerformSearch(sharedSearch);
    }

    private void PollNativeOverflow()
    {
        if(Configuring || pages.Count<2 || nativeSearch?.isFocused==true || manager.cursor.GetContent()?.Count>0)return;
        if(!RectTransformUtility.RectangleContainsScreenPoint(grid.GetComponent<RectTransform>(),Input.mousePosition,null))return;
        float wheel=Input.mouseScrollDelta.y;
        if(wheel!=0){Flip(wheel<0?1:-1);ApplyNativeSearch();}
    }

    private void DisposeNativeControls()
    {
        nativeNavigation.Clear();closeArtwork=null;nativeSearch=null;nativeTabLayout=null;sourceTabLayout=null;
        legacyTabImagePath=legacyOrnamentPath=null;legacyActiveTab=legacyInactiveTab=null;
        sharedSearch="";NativeControlsKind="";
    }
}
