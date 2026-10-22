using Il2Cpp;
using Il2CppLE.UI.PanelSystem;
using UnityEngine;
using UnityEngine.UI;
using Il2CppTMPro;
using LastEpochLAN.Core;

namespace LastEpochLAN.Mod;

internal sealed partial class NativeSharedStashView
{
    private int configureCategory=-1;
    private int configureGeneration;
    private StashConfigureResult? forcedModalAction;
    private readonly List<(UnityEngine.UI.Button Button,UnityEngine.UI.Button.ButtonClickedEvent Event)> modalEvents=new();
    private readonly List<(UnityEngine.UI.Button Button,bool Interactable)> modalInteractable=new();
    private readonly List<(TMP_Text Label,string Text)> modalLabels=new();

    private void OpenSharedConfiguration(StashConfigureTabModalOpenData data)
    {
        forcedModalAction=null;constrainedModal=IntPtr.Zero;
        legacyConfiguration=NativeControlsKind=="StashTabbedUIControls";
        configureAwaiter=legacyConfiguration?
            UIBase.instance.PanelSystem.OpenModalAsync<StashConfigureTabModal,StashConfigureTabModalOpenData,StashConfigureTabModalResult>(data,new Il2CppSystem.Nullable<PanelLocation>()).GetAwaiter():
            UIBase.instance.PanelSystem.OpenModalAsync<StashConfigureTabModalV2,StashConfigureTabModalOpenData,StashConfigureTabModalResult>(data,new Il2CppSystem.Nullable<PanelLocation>()).GetAwaiter();
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
        OpenSharedConfiguration(data);configureCategory=categoryId;configureTab=owner.SelectedTab;
        configureChestId=owner.QaView?.ChestId??"";Configuring=true;
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
            RebindDeleteButton(controls.deleteCategoryButton,()=>UIBase.instance.PanelSystem.ClosePanel(modal));
        }
        else
        {
            var modal=UIBase.instance?.PanelSystem?.GetPanelIfOpen<StashConfigureTabModalV2>();
            if(modal==null || constrainedModal==modal.Pointer)return;
            var controls=modal._configureTabUI;if(controls==null)return;
            constrainedModal=modal.Pointer;
            RebindModalButton(controls._duplicateTabButton?.GetComponent<UnityEngine.UI.Button>(),StashConfigureResult.Duplicate,
                ()=>UIBase.instance.PanelSystem.ClosePanel(modal),true);
            RebindDeleteButton(controls._deleteCategoryButton?.GetComponent<UnityEngine.UI.Button>(),()=>UIBase.instance.PanelSystem.ClosePanel(modal));
        }
    }
    private void RebindDeleteButton(UnityEngine.UI.Button button,Action close)
    {
        RebindModalButton(button,StashConfigureResult.Delete,close,configureCategory!=0);
        if(button==null || configureCategory>=0)return;
        // Reuse the native action and artwork for child tabs as well. Host
        // rechecks emptiness/reservations/revision even if this preview is stale.
        modalInteractable.Add((button,button.interactable));
        button.interactable=CanDeleteSharedTab(configureTab);
        foreach(var label in button.GetComponentsInChildren<TMP_Text>(true))
        {modalLabels.Add((label,label.text));label.text="DELETE TAB";}
    }
    private bool CanDeleteSharedTab(int tab)=>owner.StashTabs.Count(t=>!t.Deleted)>1 && owner.QaView!=null &&
        owner.QaView.Tab==tab && owner.QaView.Items.Length==0 && owner.QaView.Counts.Length==owner.StashTabs.Length && owner.QaView.Counts[tab]==0;
    private void RebindModalButton(UnityEngine.UI.Button button,StashConfigureResult action,Action close,bool visible)
    {
        if(button==null)return;
        hiddenModalWidgets.Add((button.gameObject,button.gameObject.activeSelf));button.gameObject.SetActive(visible);
        modalEvents.Add((button,button.onClick));button.onClick=new UnityEngine.UI.Button.ButtonClickedEvent();
        button.onClick.AddListener((Action)(()=>{forcedModalAction=action;close();}));
    }
    private void RestoreSharedModalActions()
    {
        foreach(var (button,active) in modalInteractable)if(button!=null)button.interactable=active;
        modalInteractable.Clear();
        foreach(var (label,text) in modalLabels)if(label!=null)label.text=text;
        modalLabels.Clear();
        foreach(var (button,original) in modalEvents)if(button!=null)button.onClick=original;
        modalEvents.Clear();
    }
}
