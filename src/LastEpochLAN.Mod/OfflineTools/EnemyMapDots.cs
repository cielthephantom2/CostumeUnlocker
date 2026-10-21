using Il2Cpp;
using Il2CppDMM;
using UnityEngine;
using IconManager=Il2CppLE.UI.Minimap.MinimapIconManager;
namespace LastEpochLAN.Mod.OfflineTools;

// Incrementally read the game's loaded visual registry. Native minimap icons
// handle movement, projection, clipping and map zoom; no scene-wide scans.
internal static class EnemyMapDots
{
    private sealed class Entry
    {
        internal int Id;
        internal ActorVisuals Visual;
        internal GameObject Anchor;
        internal DMMapIcon Icon;
        internal int Kind=-1;
    }
    private static readonly Dictionary<int,Entry> byId=new();
    private static readonly List<Entry> entries=new();
    private static readonly Sprite[] sprites=new Sprite[3];
    private static Texture2D atlas;
    private static ActorManager registry;
    private static IconManager manager;
    private static int groupIndex,actorIndex,updateIndex;
    internal static int Count=>entries.Count;
    internal static void Tick()
    {
        using var performance = RuntimePerformance.Measure(PerformanceArea.EnemyDots);
        if(!OfflineToolkit.Settings.EnemyMapDots || !OfflineToolkit.Ready)
        {if(entries.Count>0 || registry!=null) Clear();return;}
        var native=IconManager.Instance;
        var actors=ActorManager.instance;
        if(native==null || actors==null) {if(entries.Count>0) Clear();return;}
        if(manager==null || manager.Pointer!=native.Pointer || registry==null || registry.Pointer!=actors.Pointer)
        {Clear();manager=native;registry=actors;}
        EnsureSprites();
        var localAlignment=PlayerFinder.getLocalActorSync()?.actorVisuals?.alignment;
        // At most 12 registry visits and 24 status checks per rendered frame.
        var groups=registry.visuals;
        if(groups!=null && groups.Length>0)
        {
            for(int step=0;step<12;step++)
            {
                if(groupIndex>=groups.Length) {groupIndex=0;actorIndex=0;}
                var list=groups[groupIndex]?.visuals;
                if(list==null || actorIndex>=list.Count) {groupIndex++;actorIndex=0;continue;}
                var visual=list[actorIndex++];
                if(visual==null) continue;
                int id=visual.GetInstanceID();
                if(byId.TryGetValue(id,out var previous))
                {
                    if(previous.Visual!=null && previous.Visual.Pointer==visual.Pointer) continue;
                    Remove(previous);
                }
                if(!Eligible(visual,localAlignment)) continue;
                var entry=new Entry {Id=id,Visual=visual};
                byId[id]=entry;entries.Add(entry);
            }
        }
        int budget=Math.Min(24,entries.Count);
        int creations=0;
        for(int i=0;i<budget && entries.Count>0;i++)
        {
            if(updateIndex>=entries.Count) updateIndex=0;
            var entry=entries[updateIndex];
            if(!Eligible(entry.Visual,localAlignment)) {RemoveAt(updateIndex);continue;}
            if(entry.Anchor==null || entry.Icon==null)
            {
                if(creations>=2) {updateIndex++;continue;}
                Create(entry);creations++;
            }
            var info=entry.Visual.BaseDisplayInformation;
            int kind=info!=null?(int)info.actorClass:0;
            kind=kind>=3?2:kind==2?1:0;
            if(kind!=entry.Kind)
            {
                entry.Kind=kind;
                entry.Icon.sprite=sprites[kind];entry.Icon.icon=sprites[kind];
                entry.Icon.ChangeIcon(sprites[kind]);

            }
            ApplySize(entry);
            if(entry.Icon.img!=null) entry.Icon.img.raycastTarget=false;
            updateIndex++;
        }
    }
    private static bool Eligible(ActorVisuals visual,Alignment local)
    {
        if(visual==null || !visual.isActiveAndEnabled || visual.isPlayer || visual.dead || visual.isCorpse) return false;
        var sync=visual.actorSync;
        if(sync!=null && sync.IsDead) return false;
        var alignment=visual.alignment;
        if(alignment==null) return false;
        return local!=null?alignment.IsFoe(local):alignment.IsEnemyOfGood;
    }
    private static void Create(Entry entry)
    {
        var anchor=new GameObject("TM Enemy minimap dot");
        entry.Anchor=anchor;anchor.SetActive(false);
        anchor.transform.SetParent(entry.Visual.transform,false);
        var icon=anchor.AddComponent<DMMapIcon>();entry.Icon=icon;
        icon.sprite=sprites[0];icon.icon=sprites[0];
        icon.tint=new Color32(255,255,255,255);
        icon.scaleWithZoom=false;
        icon.overrideGlobalScaleMode=true;icon.ignoreGlobalScale=true;
        icon.rotate=false;icon.rotateWithMap=false;icon.useDirectionIndicator=false;
        icon.useCompositeDirectionIndicator=false;icon.WorldScale=false;
        icon.ApplyScaling=true;icon.SinScale=false;ApplySize(entry);
        anchor.SetActive(true);entry.Kind=-1;
    }
    private static void ApplySize(Entry entry)
    {
        // MinimapIconManager.LateUpdate uses min(abs(icon.transform.lossyScale))
        // when ApplyScaling is enabled. BaseDMMapIcon.scaleMultiplier is ignored
        // on this renderer. Normalize the parent so monster prefab size cannot
        // enlarge the dot; the large Tab overlay keeps the original UI scale.
        float desired=UIBase.instance?.overlayMapOpen==true?1f:0.3f;
        var parent=entry.Anchor.transform.parent;
        var inherited=parent!=null?parent.lossyScale:Vector3.one;
        float minimum=Mathf.Min(Mathf.Abs(inherited.x),Mathf.Abs(inherited.y),Mathf.Abs(inherited.z));
        float scale=float.IsFinite(minimum) && minimum>0.0001f?desired/minimum:desired;
        var current=entry.Anchor.transform.localScale;
        if(!Mathf.Approximately(current.x,scale) || !Mathf.Approximately(current.y,scale) || !Mathf.Approximately(current.z,scale))
            entry.Anchor.transform.localScale=Vector3.one*scale;
    }
    private static void EnsureSprites()
    {
        if(atlas!=null) return;
        atlas=new Texture2D(48,16,TextureFormat.RGBA32,false);
        atlas.name="TM Enemy minimap dots";atlas.wrapMode=TextureWrapMode.Clamp;atlas.filterMode=FilterMode.Bilinear;
        var colors=new[]{new Color(1f,0.08f,0.06f),new Color(1f,0.82f,0.05f),new Color(1f,0.38f,0.02f)};
        for(int kind=0;kind<3;kind++)
            for(int y=0;y<16;y++) for(int x=0;x<16;x++)
            {
                float radius=Mathf.Sqrt((x-7.5f)*(x-7.5f)+(y-7.5f)*(y-7.5f));
                // Bake the outline into the same sprite: no extra UI objects.
                var edge=Mathf.SmoothStep(0f,1f,Mathf.Clamp01((radius-4.8f)/1.2f));
                var color=Color.Lerp(colors[kind],Color.black,edge);
                color.a=Mathf.Clamp01(7.5f-radius);
                atlas.SetPixel(kind*16+x,y,color);
            }
        atlas.Apply(false,true);
        for(int kind=0;kind<3;kind++)
        {sprites[kind]=Sprite.Create(atlas,new Rect(kind*16,0,16,16),new Vector2(0.5f,0.5f),100f);sprites[kind].name="TM Enemy dot "+kind;}
    }
    private static void Remove(Entry entry)
    {
        int index=entries.IndexOf(entry);
        if(index>=0) RemoveAt(index);
    }
    private static void RemoveAt(int index)
    {
        var entry=entries[index];byId.Remove(entry.Id);
        if(entry.Anchor!=null) {entry.Anchor.SetActive(false);UnityEngine.Object.Destroy(entry.Anchor);}
        int last=entries.Count-1;
        entries[index]=entries[last];entries.RemoveAt(last);
    }
    internal static void Clear()
    {
        for(int i=entries.Count-1;i>=0;i--) RemoveAt(i);
        manager=null;registry=null;groupIndex=0;actorIndex=0;updateIndex=0;
    }
    internal static void Dispose()
    {
        Clear();
        foreach(var sprite in sprites) if(sprite!=null) UnityEngine.Object.Destroy(sprite);
        Array.Clear(sprites,0,sprites.Length);
        if(atlas!=null) UnityEngine.Object.Destroy(atlas);atlas=null;
    }
}
