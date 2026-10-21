using Il2Cpp;
using Il2CppLE.UI.PanelSystem;
using Il2CppTMPro;
using LastEpochLAN.Core;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace LastEpochLAN.Mod;

internal sealed partial class NativeSharedStashView
{
    private Transform categoryHolder;
    private GameObject categoryArtwork,addCategoryArtwork;
    private int[] categoryNamePath,categoryIconPath,categoryImagePath,categoryOrnamentPath;
    private int[] modernCategoryTabsPath,modernSelectedPath;
    private Sprite categoryActive,categoryInactive;
    private SimpleLayoutGroup sourceCategoryLayout,categoryLayout;
    private readonly Dictionary<int,GameObject> categoryVisuals=new();
    private readonly Dictionary<int,Transform> categoryTabHolders=new();
    private SharedStashCategory[] renderedCategories=Array.Empty<SharedStashCategory>();
    private int selectedCategory;
    private int shownCategoryTab=-1,layoutCategory=-1;
    private void ScrollCategories(int direction)
    {
        var ids=owner.StashCategories.Select(c=>c.Id).ToArray();
        int index=Array.IndexOf(ids,selectedCategory);
        SelectSharedCategory(ids[Math.Clamp(index+direction,0,ids.Length-1)]);
    }

    private void CaptureLegacyCategories(StashPanel source,StashTabbedUIControls controls)
    {
        categoryHolder=Cloned(source.transform,controls.categoryHolder);
        addCategoryArtwork=Cloned(source.transform,controls.addCategoryButton)?.gameObject;
        sourceCategoryLayout=controls.categoryLayoutGroup;
        var template=source.GetComponentsInChildren<TabbedUITabWithIcon>(true)
            .FirstOrDefault(t=>!t.isTab && !t.vertTab && t.gameObject.activeInHierarchy && t.transform.IsChildOf(controls.categoryHolder))??
            controls.categoryPrefab?.GetComponent<TabbedUITabWithIcon>();
        if(template==null || categoryHolder==null || addCategoryArtwork==null)throw new InvalidOperationException("Native category controls unavailable");
        categoryNamePath=template.tabName!=null?Indices(template.transform,template.tabName.transform):null;
        categoryIconPath=template.icon!=null?Indices(template.transform,template.icon.transform):null;
        categoryImagePath=template.tabImage!=null?Indices(template.transform,template.tabImage.transform):null;
        categoryOrnamentPath=template.activeOrnament!=null?Indices(template.transform,template.activeOrnament.transform):null;
        categoryActive=template.activeTab;categoryInactive=template.inactiveTab;
        categoryArtwork=UnityEngine.Object.Instantiate(template.gameObject,parking.transform,false);
        categoryArtwork.SetActive(false);StripTabScripts(categoryArtwork);
        SharedStashNativeLayout.Restore(template.transform,categoryArtwork.transform);
    }
    private void CaptureModernCategories(StashPanel source,NewStashTabbedUIControls controls)
    {
        categoryHolder=sharedTabHolder;
        var template=source.GetComponentsInChildren<StashFolderUI>(true).FirstOrDefault(f=>f.gameObject.activeInHierarchy)??
            controls._FolderPrefab?.GetComponent<StashFolderUI>();
        if(template==null)throw new InvalidOperationException("Native folder template unavailable");
        categoryNamePath=template._tabName!=null?Indices(template.transform,template._tabName.transform):null;
        categoryIconPath=template._icon!=null?Indices(template.transform,template._icon.transform):null;
        modernSelectedPath=template._selectedBackground!=null?Indices(template.transform,template._selectedBackground.transform):null;
        var liveTab=template._activeTabs?.ToArray().FirstOrDefault(t=>t!=null && t.transform.IsChildOf(template.transform));
        modernCategoryTabsPath=liveTab!=null?Indices(template.transform,liveTab.transform.parent):
            template._tabContentManager!=null?Indices(template.transform,template._tabContentManager.transform):null;
        if(modernCategoryTabsPath==null)throw new InvalidOperationException("Native folder tab holder unavailable");
        categoryArtwork=UnityEngine.Object.Instantiate(template.gameObject,parking.transform,false);
        categoryArtwork.SetActive(false);StripTabScripts(categoryArtwork);
        SharedStashNativeLayout.Restore(template.transform,categoryArtwork.transform);
    }
    private void BuildSharedCategories()
    {
        if(categoryArtwork==null || categoryHolder==null)throw new InvalidOperationException("Shared categories require native artwork");
        bool addInside=addCategoryArtwork!=null && addCategoryArtwork.transform.IsChildOf(categoryHolder);
        if(addInside)addCategoryArtwork.transform.SetParent(parking.transform,false);
        bool tabAddInside=addArtwork!=null && addArtwork.transform.IsChildOf(categoryHolder);
        if(tabAddInside)addArtwork.transform.SetParent(parking.transform,false);
        for(int i=categoryHolder.childCount-1;i>=0;i--)UnityEngine.Object.DestroyImmediate(categoryHolder.GetChild(i).gameObject);
        if(addInside)addCategoryArtwork.transform.SetParent(categoryHolder,false);
        if(tabAddInside)addArtwork.transform.SetParent(categoryHolder,false);
        if(addCategoryArtwork!=null)BindArtwork(addCategoryArtwork,owner.AddCategory);
        if(sourceCategoryLayout!=null)categoryLayout=CloneLayout(categoryHolder,sourceCategoryLayout);
        // In V2 the original + offers both tab/folder choices. Keep that same
        // native control: left adds a tab, context click adds a category.
        if(modernCategoryTabsPath!=null)BindContextAction(addArtwork,owner.AddCategory);
    }
    private void BindContextAction(GameObject visual,Action action)
    {
        var trigger=visual.AddComponent<EventTrigger>();trigger.triggers=new Il2CppSystem.Collections.Generic.List<EventTrigger.Entry>();
        var entry=new EventTrigger.Entry{eventID=EventTriggerType.PointerClick};
        entry.callback.AddListener((Action<BaseEventData>)(data=>
        {
            var pointer=data.TryCast<PointerEventData>();
            if(pointer?.button!=PointerEventData.InputButton.Right || !owner.TabControlsReady || Configuring)return;
            try{action();}catch(Exception ex){owner.Status="Shared Stash: "+ex.Message;MelonLoader.MelonLogger.Warning(owner.Status);}
        }));trigger.triggers.Add(entry);
    }
    internal void SelectSharedCategory(int id)
    {
        if(!owner.TabControlsReady || Configuring)return;
        selectedCategory=id;
        var first=owner.StashTabs.FirstOrDefault(t=>t.Category==id);
        if(first!=null)owner.SelectTab(first.Id);
    }
    private void RefreshSharedCategories(ChestReply snapshot)
    {
        if(!snapshot.Categories.Any(c=>c.Id==selectedCategory))selectedCategory=0;
        if(!renderedCategories.SequenceEqual(snapshot.Categories))
        {
            foreach(var visual in categoryVisuals.Values){visual.SetActive(false);UnityEngine.Object.Destroy(visual);}
            categoryVisuals.Clear();categoryTabHolders.Clear();renderedCategories=snapshot.Categories.ToArray();
            foreach(var category in snapshot.Categories)
            {
                var visual=UnityEngine.Object.Instantiate(categoryArtwork,categoryHolder,false);visual.name="Shared native category "+category.Id;
                SharedStashNativeLayout.Restore(categoryArtwork.transform,visual.transform);
                var label=At(visual.transform,categoryNamePath)?.GetComponent<TMP_Text>();
                if(label!=null)
                {
                    label.gameObject.SetActive(true);label.enabled=true;label.text=category.Name;
                    SharedStashNativeLayout.SizeLabel(label);
                }
                var image=At(visual.transform,categoryImagePath)?.GetComponent<Image>();if(image!=null && category.Color<tabColors.Length)image.color=tabColors[category.Color];
                var icon=At(visual.transform,categoryIconPath)?.GetComponent<Image>();
                if(icon!=null){icon.sprite=category.Icon>0 && category.Icon<tabIcons.Length?tabIcons[category.Icon]:null;icon.enabled=icon.sprite!=null;icon.gameObject.SetActive(icon.enabled);icon.color=Color.white;}
                int id=category.Id;BindArtwork(visual,()=>SelectSharedCategory(id));BindContextAction(visual,()=>ConfigureSharedCategory(id));
                if(modernCategoryTabsPath!=null)
                {
                    var holder=At(visual.transform,modernCategoryTabsPath);
                    for(int i=holder.childCount-1;i>=0;i--)UnityEngine.Object.DestroyImmediate(holder.GetChild(i).gameObject);
                    holder.gameObject.SetActive(true);categoryTabHolders.Add(id,holder);
                }
                visual.SetActive(true);SharedStashNativeLayout.Reflow(visual);categoryVisuals.Add(id,visual);
            }
            if(addCategoryArtwork!=null)addCategoryArtwork.transform.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
            foreach(var visual in categoryVisuals.Values)SharedStashNativeLayout.Reflow(visual);
            if(categoryLayout!=null)
            {
                categoryLayout.Elements.Clear();foreach(var visual in categoryVisuals.Values)categoryLayout.AddElement(visual);
                if(addCategoryArtwork!=null)categoryLayout.AddElement(addCategoryArtwork);categoryLayout.UpdateLayout();
            }
            renderedTabs=Array.Empty<SharedStashTab>();
        }
        foreach(var category in categoryVisuals)
        {
            var image=At(category.Value.transform,categoryImagePath)?.GetComponent<Image>();if(image!=null)image.sprite=category.Key==selectedCategory?categoryActive:categoryInactive;
            At(category.Value.transform,categoryOrnamentPath)?.gameObject.SetActive(category.Key==selectedCategory);
            At(category.Value.transform,modernSelectedPath)?.gameObject.SetActive(category.Key==selectedCategory);
        }
        // An empty category must not show items belonging to a different one.
        if(grid!=null)grid.gameObject.SetActive(snapshot.Tabs[snapshot.Tab].Category==selectedCategory);
    }
    private Transform TabParent(SharedStashTab tab)=>modernCategoryTabsPath==null?sharedTabHolder:categoryTabHolders[tab.Category];
    private void ScrollSharedSelection()
    {
        if(owner.SelectedTab>=sharedTabVisuals.Count || tabScroll.content==null)return;
        var viewport=tabScroll.viewport??tabScroll.GetComponent<RectTransform>();
        var target=sharedTabVisuals[owner.SelectedTab].GetComponent<RectTransform>();
        var bounds=SharedStashGeometry.RelativeRect(viewport,target);
        var visible=viewport.rect;
        if(tabScroll.horizontal)
        {
            float extent=tabScroll.content.rect.width-visible.width;
            float shift=bounds.xMax>visible.xMax?bounds.xMax-visible.xMax:bounds.xMin<visible.xMin?bounds.xMin-visible.xMin:0;
            if(extent>0)tabScroll.horizontalNormalizedPosition=Mathf.Clamp01(tabScroll.horizontalNormalizedPosition+shift/extent);
        }
        if(tabScroll.vertical)
        {
            float extent=tabScroll.content.rect.height-visible.height;
            float shift=bounds.yMax>visible.yMax?bounds.yMax-visible.yMax:bounds.yMin<visible.yMin?bounds.yMin-visible.yMin:0;
            if(extent>0)tabScroll.verticalNormalizedPosition=Mathf.Clamp01(tabScroll.verticalNormalizedPosition+shift/extent);
        }
    }
    private void RefreshModernCategoryLayouts()
    {
        foreach(var holder in categoryTabHolders.Values)
        {
            var layout=holder.GetComponent<SimpleLayoutGroup>();
            if(layout!=null)
            {
                layout.Elements=new Il2CppSystem.Collections.Generic.List<SimpleLayoutGroup.SimpleLayoutElement>();
                foreach(var tab in sharedTabVisuals.Where(v=>v.transform.parent.Pointer==holder.Pointer)){SharedStashNativeLayout.Reflow(tab);layout.AddElement(tab);}
                layout.UpdateLayout();
            }
        }
        var folders=categoryHolder.GetComponent<SimpleLayoutGroup>();
        if(folders!=null)
        {
            folders.Elements=new Il2CppSystem.Collections.Generic.List<SimpleLayoutGroup.SimpleLayoutElement>();
            foreach(var visual in categoryVisuals.Values){SharedStashNativeLayout.Reflow(visual);folders.AddElement(visual);}
            if(addArtwork!=null && addArtwork.transform.parent.Pointer==categoryHolder.Pointer)folders.AddElement(addArtwork);
            folders.UpdateLayout();
        }
    }
    private static SimpleLayoutGroup CloneLayout(Transform holder,SimpleLayoutGroup source)
    {
        return SharedStashNativeLayout.CopySettings(holder,source);
    }
    private void DisposeSharedCategories()
    {
        categoryHolder=null;categoryArtwork=addCategoryArtwork=null;categoryLayout=sourceCategoryLayout=null;
        categoryVisuals.Clear();categoryTabHolders.Clear();renderedCategories=Array.Empty<SharedStashCategory>();
        categoryNamePath=categoryIconPath=categoryImagePath=categoryOrnamentPath=modernSelectedPath=modernCategoryTabsPath=null;
        categoryActive=categoryInactive=null;selectedCategory=0;
        shownCategoryTab=layoutCategory=-1;
    }
}
