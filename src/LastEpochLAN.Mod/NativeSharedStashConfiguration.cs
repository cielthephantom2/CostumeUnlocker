using Il2Cpp;
using Il2CppLE.UI.PanelSystem;
using UnityEngine;
using UnityEngine.UI;
using LastEpochLAN.Core;

namespace LastEpochLAN.Mod;

internal sealed partial class NativeSharedStashView
{
    private int configureCategory=-1;
    private int configureGeneration;
    private StashConfigureResult? forcedModalAction;
    private readonly List<(UnityEngine.UI.Button Button,UnityEngine.UI.Button.ButtonClickedEvent Event)> modalEvents=new();

    private void OpenSharedConfiguration(StashConfigureTabModalOpenData data)
    {
        forcedModalAction=null;constrainedModal=IntPtr.Zero;
        legacyConfiguration=NativeControlsKind=="StashTabbedUIControls";
        Configuring=true;
        try{configureAwaiter=legacyConfiguration?
            UIBase.instance.PanelSystem.OpenModalAsync<StashConfigureTabModal,StashConfigureTabModalOpenData,StashConfigureTabModalResult>(data,new Il2CppSystem.Nullable<PanelLocation>()).GetAwaiter():
            UIBase.instance.PanelSystem.OpenModalAsync<StashConfigureTabModalV2,StashConfigureTabModalOpenData,StashConfigureTabModalResult>(data,new Il2CppSystem.Nullable<PanelLocation>()).GetAwaiter();}
        catch{Configuring=false;throw;}
    }

    private void ConfigureSharedCategory(int categoryId)
    {
        if(!owner.TabControlsReady || Configuring)return;
        var category=owner.StashCategories.Single(c=>c.Id==categoryId);
        var names=new Il2CppSystem.Collections.Generic.List<string>();var ids=new Il2CppSystem.Collections.Generic.List<int>();
        var tabs=new Il2CppSystem.Collections.Generic.List<string>();var tabIds=new Il2CppSystem.Collections.Generic.List<int>();
        foreach(var tab in owner.StashTabs.Where(t=>!t.Deleted)){tabs.Add(tab.Name);tabIds.Add(tab.Id);}
        var data=new StashConfigureTabModalOpenData(category.Icon,category.Color,category.Name,category.Id,-1,names,ids,category.Id!=0,
            SharedStashPriorityCodec.Decode(category.Priority),false,false,new StashPriority(),tabs,tabIds);
        configureCategory=categoryId;configureTab=owner.SelectedTab;
        configureChestId=owner.QaView?.ChestId??"";OpenSharedConfiguration(data);
    }

    private void DuplicateCategory()=>owner.DuplicateCategory(owner.StashCategories.Single(c=>c.Id==configureCategory));

    private void BindSharedModalActions()
    {
        if(legacyConfiguration)
        {
            var modal=UIBase.instance?.PanelSystem?.GetPanelIfOpen<StashConfigureTabModal>();
            if(modal==null || constrainedModal==modal.Pointer)return;
            var controls=modal._configureTabUI;
            if(controls==null)return;
            constrainedModal=modal.Pointer;
            RebindModalButton(controls.duplicateTabButton,StashConfigureResult.Duplicate,()=>UIBase.instance.PanelSystem.ClosePanel(modal),true);
            RebindModalButton(controls.deleteCategoryButton,StashConfigureResult.Delete,()=>UIBase.instance.PanelSystem.ClosePanel(modal),configureCategory>0);
        }
        else
        {
            var modal=UIBase.instance?.PanelSystem?.GetPanelIfOpen<StashConfigureTabModalV2>();
            if(modal==null || constrainedModal==modal.Pointer)return;
            var controls=modal._configureTabUI;if(controls==null)return;
            constrainedModal=modal.Pointer;
            RebindModalButton(controls._duplicateTabButton?.GetComponent<UnityEngine.UI.Button>(),StashConfigureResult.Duplicate,
                ()=>UIBase.instance.PanelSystem.ClosePanel(modal),true);
            RebindModalButton(controls._deleteCategoryButton?.GetComponent<UnityEngine.UI.Button>(),StashConfigureResult.Delete,
                ()=>UIBase.instance.PanelSystem.ClosePanel(modal),configureCategory>0);
        }
    }
    private void RebindModalButton(UnityEngine.UI.Button button,StashConfigureResult action,Action close,bool visible)
    {
        if(button==null)return;
        hiddenModalWidgets.Add((button.gameObject,button.gameObject.activeSelf));button.gameObject.SetActive(visible);
        modalEvents.Add((button,button.onClick));button.onClick=new UnityEngine.UI.Button.ButtonClickedEvent();
        button.onClick.AddListener((Action)(()=>{forcedModalAction=action;close();}));
    }
    private void RestoreSharedModalActions()
    {
        foreach(var (button,original) in modalEvents)if(button!=null)button.onClick=original;
        modalEvents.Clear();
    }
}
