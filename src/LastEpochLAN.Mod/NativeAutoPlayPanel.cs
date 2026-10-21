using Il2CppInterop.Runtime;
using LastEpochLAN.Core;
using Il2CppTMPro;
using Il2CppLE.AssetBundles;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace LastEpochLAN.Mod;

internal sealed partial class AutoPlayRuntime
{
    private int tab;
    private NativePanel panelView;
    private sealed class NativePanel : IDisposable
    {
        private const int Width=680,Height=530;
        private readonly AutoPlayRuntime owner;
        private GameObject root,frame,details;
        private TMP_FontAsset font,ownedFont;
        private Texture2D chromeTexture;
        private Sprite chrome;
        private readonly List<Action> updates=new(),detailUpdates=new();
        private readonly Image[] skillImages=new Image[5];
        private readonly LoadRef<Sprite>[] iconAssets=new LoadRef<Sprite>[5];
        private readonly string[] iconKeys=new string[5];
        private Il2Cpp.AbilityBarIcon[] nativeSlots=Array.Empty<Il2Cpp.AbilityBarIcon>();
        private Il2Cpp.HealthPotionChargeIndicator nativePotion;
        private Sprite potionFallback;
        private Image potionImage;
        private float nextIconScan;
        private object profile;
        private string binding="";
        private int selected=-1;
        private Button startButton;
        private LineRenderer radiusRing;
        private Material radiusMaterial;
        private float previewUntil;
        private readonly Color accent=new(0.35f,0.82f,0.75f),text=new(0.91f,0.94f,0.98f),muted=new(0.53f,0.60f,0.69f);
        private readonly Color surface=new(0.075f,0.10f,0.145f),control=new(0.12f,0.16f,0.22f),stroke=new(0.17f,0.22f,0.29f);
        internal NativePanel(AutoPlayRuntime runtime){owner=runtime;}
        internal bool Visible=>root!=null && root.activeInHierarchy;
        internal bool IconsReady=>Enumerable.Range(0,5).All(i=>owner.abilities[i]==null || skillImages[i]?.sprite!=null) && potionImage?.sprite!=null;
        internal object DesignDiagnostics=>new {width=Width,height=Height,font=font?.name,
            equippedIcons=Enumerable.Range(0,5).Where(i=>owner.abilities[i]!=null).Select(i=>new{slot=i,ability=owner.names[i],sprite=skillImages[i]?.sprite?.name}).ToArray(),
            potionSprite=potionImage?.sprite?.name};
        private GameObject Node(string name,Transform parent,float x,float y,float w,float h)
        {
            var go=new GameObject(name,Il2CppType.Of<RectTransform>());go.transform.SetParent(parent,false);
            var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=new(0,1);r.anchoredPosition=new(x,-y);r.sizeDelta=new(w,h);return go;
        }
        private Image Paint(GameObject go,Color color,bool raycast=true,bool rounded=true)
        {
            var image=go.AddComponent<Image>();image.color=color;image.raycastTarget=raycast;
            if(rounded){image.sprite=chrome;image.type=Image.Type.Sliced;}return image;
        }
        private Image Box(Transform parent,string name,float x,float y,float w,float h,Color color,bool rounded=true)=>Paint(Node(name,parent,x,y,w,h),color,false,rounded);
        private TextMeshProUGUI Label(Transform parent,float x,float y,float w,float h,Func<string> value,int size=14,bool secondary=false,List<Action> refresh=null)
        {
            var go=Node("Text",parent,x,y,w,h);var label=go.AddComponent<TextMeshProUGUI>();label.font=font;label.fontSize=size;label.color=secondary?muted:text;
            label.raycastTarget=false;label.enableWordWrapping=false;label.overflowMode=TextOverflowModes.Ellipsis;
            label.text=value();(refresh??updates).Add(()=>{if(label!=null)label.text=value();});return label;
        }
        private Button Button(Transform parent,string name,float x,float y,float w,float h,Func<string> value,Action action,List<Action> refresh=null)
        {
            var go=Node(name,parent,x,y,w,h);var image=Paint(go,control);
            var button=go.AddComponent<Button>();button.targetGraphic=image;
            button.onClick.AddListener((Action)(()=>{action();owner.Changed();}));
            if(value!=null){float pad=w<45?4:9;var label=Label(go.transform,pad,0,w-pad*2,h,value,13,false,refresh);label.alignment=TextAlignmentOptions.Midline;}
            return button;
        }
        private void Toggle(float x,float y,float w,string name,Func<bool> get,Action<bool> set)
        {
            var button=Button(details.transform,name,x,y,w,28,null,()=>set(!get()),detailUpdates);
            Label(button.transform,12,0,w-44,28,()=>name,13,false,detailUpdates).alignment=TextAlignmentOptions.MidlineLeft;
            var dot=Box(button.transform,"Indicator",w-25,10,8,8,muted);
            detailUpdates.Add(()=>{button.targetGraphic.color=get()?new Color(0.105f,0.24f,0.25f):control;dot.color=get()?accent:muted;});
        }
        private void Slider(float x,float y,float w,string name,Func<float> get,Action<float> set,float min,float max,bool whole=false,string unit="%",bool previewRadius=false)
        {
            Label(details.transform,x,y,w-78,20,()=>name+(unit=="%"?" (%)":unit.Trim()=="s"?" (s)":unit.Trim()=="m"?" (m)":""),12,true,detailUpdates);
            // Real native input field: no per-frame text replacement while editing.
            var fieldRoot=Node(name+" value",details.transform,x+w-76,y-2,76,23);var background=Paint(fieldRoot,control);
            var viewport=Node("Viewport",fieldRoot.transform,3,0,70,23);viewport.AddComponent<RectMask2D>();
            var number=Label(viewport.transform,0,0,70,23,()=>"",12,false,new List<Action>());number.alignment=TextAlignmentOptions.MidlineRight;
            var field=fieldRoot.AddComponent<TMP_InputField>();field.targetGraphic=background;field.textViewport=viewport.GetComponent<RectTransform>();field.textComponent=number;
            field.fontAsset=font;field.lineType=TMP_InputField.LineType.SingleLine;field.characterLimit=32;
            field.SetTextWithoutNotify(get().ToString("0.##",System.Globalization.CultureInfo.InvariantCulture));
            var go=Node(name,details.transform,x,y+22,w,16);Paint(go,new Color(0,0,0,0),true,false);
            var slider=go.AddComponent<UnityEngine.UI.Slider>();slider.minValue=min;slider.maxValue=max;slider.wholeNumbers=whole;
            var track=Box(go.transform,"Track",0,6,w,4,control);
            var fill=Node("Fill",track.transform,0,0,0,0);Paint(fill,new Color(0.22f,0.49f,0.49f),false);
            var fillRect=fill.GetComponent<RectTransform>();fillRect.anchorMin=Vector2.zero;fillRect.anchorMax=Vector2.one;fillRect.sizeDelta=Vector2.zero;slider.fillRect=fillRect;
            var area=Node("Handle area",go.transform,5,2,w-10,12);
            var handle=Node("Handle",area.transform,0,0,10,0);Paint(handle,accent);
            handle.GetComponent<RectTransform>().pivot=new(0.5f,0.5f);
            slider.handleRect=handle.GetComponent<RectTransform>();slider.targetGraphic=handle.GetComponent<Image>();
            slider.direction=UnityEngine.UI.Slider.Direction.LeftToRight;slider.SetValueWithoutNotify(get());
            slider.onValueChanged.AddListener((Action<float>)(_=>
            {
                // Read native Slider state directly. Do not trust the float
                // marshalled through the IL2CPP UnityEvent delegate.
                set(slider.value);
                // Rules can round to a step; keep the handle and saved value equal.
                slider.SetValueWithoutNotify(get());
                if(!field.isFocused)field.SetTextWithoutNotify(get().ToString("0.##",System.Globalization.CultureInfo.InvariantCulture));
                if(previewRadius)previewUntil=Time.unscaledTime+1.2f;
                owner.Changed();
            }));
            field.onEndEdit.AddListener((Action<string>)(_=>
            {
                // As with Slider.value, read actual native state rather than the event argument.
                if(LastEpochLAN.Core.AutoPlayRules.TrySliderValue(field.text,min,max,whole,out float value))
                {set(value);owner.Changed();if(previewRadius)previewUntil=Time.unscaledTime+1.2f;}
                field.SetTextWithoutNotify(get().ToString("0.##",System.Globalization.CultureInfo.InvariantCulture));
                slider.SetValueWithoutNotify(get());
            }));
            detailUpdates.Add(()=>
            {
                slider.SetValueWithoutNotify(get());
                if(!field.isFocused)field.SetTextWithoutNotify(get().ToString("0.##",System.Globalization.CultureInfo.InvariantCulture));
            });
            if(previewRadius)detailUpdates.Add(()=>
            {if(RectTransformUtility.RectangleContainsScreenPoint(go.GetComponent<RectTransform>(),Input.mousePosition,null))previewUntil=Time.unscaledTime+0.1f;});
        }
        private void RadiusPreview()
        {
            bool show=Visible && owner.localReady && selected>=0 && selected<5 && Time.unscaledTime<previewUntil;
            var actor=show?Il2Cpp.PlayerFinder.getPlayerActor():null;
            if(actor==null){if(radiusRing!=null)radiusRing.gameObject.SetActive(false);return;}
            if(radiusRing==null)
            {
                var shader=Shader.Find("Sprites/Default");if(shader==null)return;
                radiusMaterial=new Material(shader);
                var go=new GameObject("Combat Assist detection radius");UnityEngine.Object.DontDestroyOnLoad(go);
                radiusRing=go.AddComponent<LineRenderer>();radiusRing.material=radiusMaterial;radiusRing.useWorldSpace=true;
                radiusRing.loop=true;radiusRing.positionCount=96;radiusRing.startWidth=radiusRing.endWidth=0.07f;
                radiusRing.startColor=radiusRing.endColor=accent;radiusRing.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                radiusRing.receiveShadows=false;
            }
            radiusRing.gameObject.SetActive(true);var center=actor.position()+Vector3.up*0.12f;float radius=AutoPlayRules.DetectionRadius(owner.Settings.Skills[selected].Radius);
            for(int i=0;i<96;i++){float a=i*Mathf.PI*2/96;radiusRing.SetPosition(i,center+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));}
        }
        private void Chrome()
        {
            chromeTexture=new Texture2D(32,32,TextureFormat.RGBA32,false);chromeTexture.name="TM UI rounded surface";chromeTexture.wrapMode=TextureWrapMode.Clamp;chromeTexture.filterMode=FilterMode.Bilinear;
            for(int y=0;y<32;y++)for(int x=0;x<32;x++)
            {float dx=Mathf.Max(Mathf.Abs(x-15.5f)-7.5f,0),dy=Mathf.Max(Mathf.Abs(y-15.5f)-7.5f,0);chromeTexture.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(8-Mathf.Sqrt(dx*dx+dy*dy))));}
            chromeTexture.Apply(false,true);chrome=Sprite.Create(chromeTexture,new Rect(0,0,32,32),new Vector2(0.5f,0.5f),100,0,SpriteMeshType.FullRect,new Vector4(8,8,8,8));
        }
        private void Font()
        {
            var fonts=Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            font=fonts.FirstOrDefault(f=>f!=null && new[]{"Inter-","Roboto-","LiberationSans","Arial"}.Any(n=>f.name.StartsWith(n,StringComparison.OrdinalIgnoreCase)));
            if(font==null)
            {
                try
                {
                    ownedFont=TMP_FontAsset.CreateFontAsset("Segoe UI","Regular",48);
                    if(ownedFont==null)ownedFont=TMP_FontAsset.CreateFontAsset(Resources.GetBuiltinResource<UnityEngine.Font>("LegacyRuntime.ttf"));
                    if(ownedFont!=null){ownedFont.name="TM Combat Assist Sans";font=ownedFont;}
                }
                catch(Exception ex){MelonLoader.MelonLogger.Warning("Auto Play font fallback: "+ex.Message);}
            }
            font??=TMP_Settings.defaultFontAsset??fonts.FirstOrDefault(f=>f!=null);
            if(font==null)throw new InvalidOperationException("Game UI font not loaded yet");
        }
        private Image Artwork(Transform parent,float x,float y,float size,Func<Sprite> sprite,List<Action> refresh)
        {
            var image=Node("Skill artwork",parent,x,y,size,size).AddComponent<Image>();image.raycastTarget=false;image.preserveAspect=true;
            refresh.Add(()=>{var asset=sprite();image.sprite=asset;image.enabled=asset!=null;});return image;
        }
        private void ScanIcons()
        {
            if(Time.unscaledTime<nextIconScan)return;nextIconScan=Time.unscaledTime+1;
            nativeSlots=Resources.FindObjectsOfTypeAll<Il2Cpp.AbilityBarIcon>().Where(v=>v!=null && v.gameObject.scene.IsValid() && v.gameObject.scene.isLoaded).ToArray();
            var actor=Il2Cpp.PlayerFinder.getPlayerActor();
            nativePotion=Resources.FindObjectsOfTypeAll<Il2Cpp.HealthPotionChargeIndicator>().FirstOrDefault(p=>p!=null && p.gameObject.scene.IsValid() && p.playerHealthPotion?.Pointer==actor?.GetComponent<Il2Cpp.HealthPotion>()?.Pointer);
            nativePotion??=Resources.FindObjectsOfTypeAll<Il2Cpp.HealthPotionChargeIndicator>().FirstOrDefault(p=>p!=null && p.gameObject.scene.IsValid() && p.gameObject.activeInHierarchy && p.image?.sprite!=null);
            nativePotion??=Resources.FindObjectsOfTypeAll<Il2Cpp.HealthPotionChargeIndicator>().FirstOrDefault(p=>p!=null && (p.image?.sprite!=null || p.sprites?.Length>0));
            if(nativePotion==null && potionFallback==null)
                potionFallback=Resources.FindObjectsOfTypeAll<Sprite>().Where(s=>s!=null && s.name.IndexOf("potion",StringComparison.OrdinalIgnoreCase)>=0)
                    .OrderByDescending(s=>s.name.IndexOf("health",StringComparison.OrdinalIgnoreCase)>=0).FirstOrDefault();
        }
        private Sprite PotionSprite()
        {
            if(nativePotion?.image?.sprite!=null)return nativePotion.image.sprite;
            var sprites=nativePotion?.sprites;
            if(sprites?.Length>0)return sprites[Math.Clamp(owner.potionCharges,0,sprites.Length-1)];
            return potionFallback;
        }
        private Sprite SkillSprite(int slot)
        {
            var ability=owner.abilities[slot];string key=ability?.Pointer.ToString()??"";
            if(iconKeys[slot]!=key){iconAssets[slot]?.Dispose();iconAssets[slot]=null;iconKeys[slot]=key;}
            if(ability==null)return null;
            // HUD abilityNumber is not the UsingAbilityPlayer bar index. Match the
            // equipped ability itself; a slot-only fallback can display a neighbour.
            var native=nativeSlots.FirstOrDefault(v=>v!=null && v.gameObject.activeInHierarchy &&
                v.ability?.Pointer==ability.Pointer && v.icon?.sprite!=null);
            if(native!=null)return native.icon.sprite;
            if(iconAssets[slot]==null && ability.abilitySpriteSoftRef!=null)
                iconAssets[slot]=SoftRefExtensions.CreateLoadRef<Sprite>(ability.abilitySpriteSoftRef,"LastEpochLAN.AutoPlayIcons",0);
            return iconAssets[slot]?.AssetOrNull;
        }
        private string Key(int slot)=>owner.keys[slot].Replace("Right Mouse Button","RMB").Replace("Left Mouse Button","LMB");
        private void IconTile(int slot,float y)
        {
            var tile=Button(frame.transform,"SkillSlot"+slot,22,y,52,52,null,()=>owner.tab=slot);
            var border=tile.GetComponent<Image>();border.color=stroke;
            Box(tile.transform,"Inset",1,1,50,50,surface);
            Box(tile.transform,"Empty marker",21,25,10,2,muted);Box(tile.transform,"Empty marker",25,21,2,10,muted);
            skillImages[slot]=Artwork(tile.transform,3,3,46,()=>SkillSprite(slot),updates);
            var sweep=Box(tile.transform,"Cooldown",3,3,46,46,new Color(0,0,0,0.7f),false);sweep.sprite=chrome;sweep.type=Image.Type.Filled;sweep.fillMethod=Image.FillMethod.Radial360;sweep.fillOrigin=2;sweep.fillClockwise=false;
            var cooldown=Label(tile.transform,2,9,48,24,()=>owner.cooldowns[slot]>0?$"{owner.cooldowns[slot]:0.0}":"",13);cooldown.alignment=TextAlignmentOptions.Midline;
            Box(tile.transform,"Key badge",2,37,30,14,new Color(0.035f,0.05f,0.08f,0.95f));
            Label(tile.transform,3,37,28,14,()=>Key(slot),10).alignment=TextAlignmentOptions.Midline;
            var enabled=Box(tile.transform,"Enabled",42,4,6,6,accent);
            updates.Add(()=>
            {
                border.color=owner.tab==slot?accent:stroke;enabled.enabled=owner.Settings.Skills[slot].Enabled && owner.abilities[slot]!=null;
                sweep.fillAmount=owner.cooldowns[slot]>0?Mathf.Clamp01(owner.cooldowns[slot]/Mathf.Max(0.1f,owner.abilities[slot]?.GetCooldownWithoutStats()??1)):0;
            });
        }
        private void Build()
        {
            Font();Chrome();root=new GameObject("TM Auto Play Canvas",Il2CppType.Of<RectTransform>());UnityEngine.Object.DontDestroyOnLoad(root);
            var canvas=root.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32700;
            var scaler=root.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor=Mathf.Clamp(Mathf.Min(Screen.width/1600f,Screen.height/900f),0.75f,1f);
            root.AddComponent<GraphicRaycaster>();
            var shade=Node("Input shield",root.transform,0,0,0,0);var sr=shade.GetComponent<RectTransform>();sr.anchorMin=Vector2.zero;sr.anchorMax=Vector2.one;sr.sizeDelta=Vector2.zero;Paint(shade,new Color(0,0,0,0.22f),true,false);
            var shadow=Node("Window shadow",root.transform,0,0,Width+14,Height+14);var sh=shadow.GetComponent<RectTransform>();sh.anchorMin=sh.anchorMax=sh.pivot=new(0.5f,0.5f);sh.anchoredPosition=new(0,-5);Paint(shadow,new Color(0,0,0,0.5f),false);
            frame=Node("Combat Assist",root.transform,0,0,Width,Height);var f=frame.GetComponent<RectTransform>();f.anchorMin=f.anchorMax=f.pivot=new(0.5f,0.5f);f.anchoredPosition=Vector2.zero;
            Paint(frame,stroke);Box(frame.transform,"Surface",1,1,Width-2,Height-2,surface);
            Label(frame.transform,22,15,260,27,()=>"Combat Assist",20).fontStyle=FontStyles.Bold;
            Label(frame.transform,23,43,260,18,()=>"AUTOMATION  /  LOCAL CHARACTER",10,true).characterSpacing=1;
            Label(frame.transform,417,19,89,19,()=>$"HP {owner.hp:0}%",12,true);
            Label(frame.transform,522,19,89,19,()=>$"MP {owner.mp:0}%",12,true);
            Box(frame.transform,"HP background",417,44,86,3,control);var hpFill=Box(frame.transform,"HP",417,44,86,3,new Color(0.8f,0.36f,0.40f));
            Box(frame.transform,"MP background",522,44,86,3,control);var mpFill=Box(frame.transform,"MP",522,44,86,3,new Color(0.35f,0.55f,0.85f));
            updates.Add(()=>{hpFill.rectTransform.sizeDelta=new(86*owner.hp/100,3);mpFill.rectTransform.sizeDelta=new(86*owner.mp/100,3);});
            Button(frame.transform,"Close",634,17,25,25,()=>"X",()=>owner.Open=false);
            Box(frame.transform,"Header divider",20,70,640,1,stroke,false);Box(frame.transform,"Rail divider",90,86,1,383,stroke,false);
            for(int i=0;i<5;i++)IconTile(i,88+i*60);
            var potion=Button(frame.transform,"PotionTab",22,405,52,52,null,()=>owner.tab=5);
            potionImage=Artwork(potion.transform,5,5,42,PotionSprite,updates);
            Box(potion.transform,"Key badge",2,37,20,14,surface);Label(potion.transform,2,37,20,14,()=>owner.potionKey,10).alignment=TextAlignmentOptions.Midline;
            Label(potion.transform,24,37,26,14,()=>owner.potionCharges.ToString(),10).alignment=TextAlignmentOptions.MidlineRight;
            updates.Add(()=>potion.targetGraphic.color=owner.tab==5?new Color(0.16f,0.32f,0.32f):control);
            Button(frame.transform,"HelpTab",22,470,52,25,()=>"?",()=>owner.tab=6);
            Box(frame.transform,"Footer divider",104,476,554,1,stroke,false);
            var status=Box(frame.transform,"Status dot",106,497,7,7,muted);
            Label(frame.transform,122,485,418,20,()=>owner.Armed?"Master ON - paused while panel is open":"Master OFF - automation stopped",12);
            Label(frame.transform,122,505,414,15,()=>owner.Error!=""?owner.Error:"Settings saved automatically",10,true);
            updates.Add(()=>status.color=owner.Armed?accent:muted);
            startButton=Button(frame.transform,"StartStop",574,486,84,32,()=>owner.Armed?"Stop":"Start",()=>
            {if(owner.Armed)owner.Armed=false;else if(owner.localReady){owner.Armed=true;owner.schedule.Reset();owner.Status="Armed - close F1 and F4 to run";}});
            updates.Add(()=>startButton.targetGraphic.color=owner.Armed?new Color(0.34f,0.18f,0.23f):new Color(0.12f,0.33f,0.32f));
        }
        internal void Tick()
        {
            if(!owner.Open){if(root!=null)root.SetActive(false);previewUntil=0;RadiusPreview();return;}
            if(root==null)Build();root.SetActive(true);ScanIcons();
            root.GetComponent<CanvasScaler>().scaleFactor=Mathf.Clamp(Mathf.Min(Screen.width/1600f,Screen.height/900f),0.75f,1f);
            string currentBinding=owner.tab<5?owner.contexts[owner.tab].Ability??"":"";
            if(selected!=owner.tab || profile!=owner.Settings || binding!=currentBinding){selected=owner.tab;profile=owner.Settings;binding=currentBinding;Details();}
            foreach(var update in updates)update();foreach(var update in detailUpdates)update();
            RadiusPreview();
        }
        private void Title(string title,string subtitle)
        {
            Label(details.transform,0,2,425,25,()=>title,17,false,detailUpdates).fontStyle=FontStyles.Bold;
            Label(details.transform,0,31,530,19,()=>subtitle,12,true,detailUpdates);
        }
        private void Details()
        {
            previewUntil=0;if(details!=null){details.SetActive(false);UnityEngine.Object.Destroy(details);}detailUpdates.Clear();
            details=Node("Settings",frame.transform,108,87,548,385);
            if(selected==5){Potion();return;}if(selected==6){Help();return;}
            int i=selected;var r=owner.Settings.Skills[i];
            Artwork(details.transform,0,0,40,()=>SkillSprite(i),detailUpdates);
            Label(details.transform,53,0,355,25,()=>owner.abilities[i]==null?"Empty slot":owner.names[i],17,false,detailUpdates).fontStyle=FontStyles.Bold;
            Label(details.transform,53,28,355,20,()=>Key(i)+"  /  "+
                (owner.abilities[i]!=null && (r.Ability=="" || r.Ability!=owner.contexts[i].Ability)?"Confirmation required":owner.slotStatus[i]),12,true,detailUpdates);
            if(owner.abilities[i]==null)
            {Label(details.transform,0,104,530,60,()=>"Equip a skill on your action bar to configure this slot.",14,true,detailUpdates).enableWordWrapping=true;return;}
            if(r.Ability!=owner.contexts[i].Ability || r.Ability=="")
            {
                Label(details.transform,0,95,520,65,()=>"Confirm this skill before enabling automation. Replacing a skill always requires confirmation.",14,true,detailUpdates).enableWordWrapping=true;
                Button(details.transform,"BindSkill",0,180,260,34,()=>"Configure this skill",()=>{owner.Bind(i);Details();},detailUpdates);return;
            }
            var enabled=Button(details.transform,"Enable",431,4,105,28,()=>r.Enabled?"Enabled":"Disabled",()=>r.Enabled=!r.Enabled,detailUpdates);
            detailUpdates.Add(()=>enabled.targetGraphic.color=r.Enabled?new Color(0.12f,0.33f,0.32f):control);
            Label(details.transform,0,66,63,25,()=>"Target",12,true,detailUpdates);
            Button(details.transform,"Target",80,59,456,29,()=>r.SelfTarget?"Self / buff":r.TargetRareBoss?"Nearest rare / boss":"Nearest enemy",()=>
            {if(r.SelfTarget){r.SelfTarget=false;r.TargetRareBoss=false;}else if(!r.TargetRareBoss)r.TargetRareBoss=true;else r.SelfTarget=true;},detailUpdates);
            Label(details.transform,0,109,345,20,()=>"CAST CONDITIONS",11,true,detailUpdates).characterSpacing=0.7f;
            Button(details.transform,"Trigger logic",431,103,105,24,()=>r.RequireAll?"All selected":"Any selected",()=>r.RequireAll=!r.RequireAll,detailUpdates);
            Toggle(0,125,258,"Enemy nearby",()=>r.EnemyNear,v=>r.EnemyNear=v);
            Toggle(278,125,258,"Rare / boss",()=>r.RareBossNear,v=>r.RareBossNear=v);
            Toggle(0,158,258,"Low health",()=>r.LowHealth,v=>r.LowHealth=v);
            Toggle(278,158,258,"Low mana",()=>r.LowMana,v=>r.LowMana=v);
            Toggle(0,191,258,"Low ward",()=>r.LowWard,v=>r.LowWard=v);
            Slider(278,191,258,"Ward threshold",()=>r.WardThreshold,v=>r.WardThreshold=v,0,100000,true,"");
            Slider(0,232,258,"HP threshold",()=>r.HealthPercent,v=>r.HealthPercent=v,1,100,true);
            Slider(278,232,258,"MP threshold",()=>r.ManaPercent,v=>r.ManaPercent=v,1,100,true);
            Slider(0,276,258,"Detection radius",()=>r.Radius,v=>r.Radius=v,1,AutoPlayRules.MaximumDetectionRadius,true," m",previewRadius:true);
            Slider(278,276,258,"MP before cast",()=>r.MinimumManaPercent,v=>r.MinimumManaPercent=v,0,100,true);
            Slider(0,320,258,"Repeat interval",()=>r.Interval,v=>r.Interval=(float)Math.Round(v*4)/4,0,120,false," s");
            Toggle(278,317,258,"Only when buff missing",()=>r.BuffMissing,v=>r.BuffMissing=v);
            Button(details.transform,"Choose buff",278,352,258,22,()=>r.BuffName==""?"Choose detected buff":r.BuffName,()=>
            {var names=new[]{""}.Concat(owner.buffNames).Concat(new[]{r.BuffName}).Distinct().ToArray();r.BuffName=names[(Array.IndexOf(names,r.BuffName)+1)%names.Length];},detailUpdates);
            Label(details.transform,0,364,263,15,()=>"0 s = hold cast while conditions match",10,true,detailUpdates);
        }
        private void Potion()
        {
            var p=owner.Settings.Potion;Artwork(details.transform,0,0,40,PotionSprite,detailUpdates);
            Label(details.transform,54,0,360,25,()=>"Health potion",17,false,detailUpdates).fontStyle=FontStyles.Bold;
            Label(details.transform,54,29,360,20,()=>$"{owner.potionKey}  /  {owner.potionCharges} of {owner.potionMax} charges",12,true,detailUpdates);
            var enabled=Button(details.transform,"Enable potion",431,4,105,28,()=>p.Enabled?"Enabled":"Disabled",()=>p.Enabled=!p.Enabled,detailUpdates);
            detailUpdates.Add(()=>enabled.targetGraphic.color=p.Enabled?new Color(0.12f,0.33f,0.32f):control);
            Label(details.transform,0,81,530,24,()=>"RECOVERY RULES",11,true,detailUpdates).characterSpacing=0.7f;
            Slider(0,121,536,"Drink at or below HP",()=>p.HealthPercent,v=>p.HealthPercent=v,1,100,true);
            Slider(0,190,536,"Time between drinks",()=>p.Interval,v=>p.Interval=(float)Math.Round(v*2)/2,0.5f,30,false," s");
            Slider(0,259,536,"Keep charges in reserve",()=>p.Reserve,v=>p.Reserve=(int)v,0,Math.Max(1,owner.potionMax),true,"");
            Label(details.transform,0,331,536,45,()=>$"Cost: {owner.potionCost} charge(s) per drink. Potion has priority over skills.\nUsed this session: {owner.PotionsUsed}",12,true,detailUpdates).enableWordWrapping=true;
        }
        private void Help()
        {
            Title("Quick setup","Local character only - no LAN room required");
            var guide=Label(details.transform,0,78,530,157,()=>"1  Confirm a skill, enable it, then choose conditions.\n2  Press Start and close F1 / F4 to resume.\n0 s / Hold key: repeat casts or sustain a channel.\nLow ward uses absolute Ward, not a percentage.\nMP required before cast: minimum current MP %.\nCooldown and normal game restrictions still apply.\nManual casting takes priority. Start is not saved.",13,true,detailUpdates);guide.enableWordWrapping=true;
            Label(details.transform,0,245,530,42,()=>$"Enemy: {Distance(owner.enemyDistance)}   /   Rare or boss: {Distance(owner.rareDistance)}\nRequests {owner.Requests}   /   Observed casts {owner.ConfirmedCasts}",12,true,detailUpdates).enableWordWrapping=true;
            var buffs=Label(details.transform,0,310,530,65,()=>"ACTIVE STAT BUFFS\n"+(owner.buffNames.Count>0?string.Join(", ",owner.buffNames.Take(8)):"Cast your buff once to detect its name. Keep toggle auras manual if no active stat-buff name is readable."),12,true,detailUpdates);buffs.enableWordWrapping=true;
        }
        private static string Distance(float d)=>float.IsInfinity(d)?"none":$"{d:0.0} m";
        internal void VerifyClick()
        {
            var events=EventSystem.current;if(events==null)throw new InvalidOperationException("Native EventSystem unavailable");
            var center=startButton.GetComponent<RectTransform>().TransformPoint(new Vector3(42,-16,0));
            var data=new PointerEventData(events){position=RectTransformUtility.WorldToScreenPoint(null,center),button=PointerEventData.InputButton.Left};
            var hits=new Il2CppSystem.Collections.Generic.List<RaycastResult>();events.RaycastAll(data,hits);
            if(hits.Count==0 || hits[0].gameObject.Pointer!=startButton.gameObject.Pointer)throw new InvalidOperationException("Native Auto Play button has no topmost raycast hit");
            bool before=owner.Armed;ExecuteEvents.Execute<IPointerClickHandler>(startButton.gameObject,data,ExecuteEvents.pointerClickHandler);
            if(owner.Armed==before)throw new InvalidOperationException("Native Start/Stop button failed");
            ExecuteEvents.Execute<IPointerClickHandler>(startButton.gameObject,data,ExecuteEvents.pointerClickHandler);
            if(owner.Armed!=before)throw new InvalidOperationException("Native Stop button failed");
        }
        internal void Capture(string path)
        {
            if(!Environment.GetCommandLineArgs().Contains("--lelan-autoplay-smoke"))throw new InvalidOperationException("Private UI capture only");
            var canvas=root.GetComponent<Canvas>();var previousMode=canvas.renderMode;var previousCamera=canvas.worldCamera;
            var scaler=root.GetComponent<CanvasScaler>();float previousScale=scaler.scaleFactor;scaler.scaleFactor=1;
            var previousTexture=RenderTexture.active;
            var cameraRoot=new GameObject("QA Auto Play render camera");var camera=cameraRoot.AddComponent<Camera>();
            var target=new RenderTexture(1000,730,24);Texture2D pixels=null;
            var transforms=root.GetComponentsInChildren<Transform>(true).ToArray();var layers=transforms.Select(t=>t.gameObject.layer).ToArray();
            try
            {
                for(int i=0;i<transforms.Length;i++)transforms[i].gameObject.layer=5;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(0.02f,0.025f,0.035f,1);
                camera.cullingMask=1<<5;camera.orthographic=true;camera.orthographicSize=365;camera.nearClipPlane=0.1f;camera.farClipPlane=100;
                camera.targetTexture=target;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                pixels=new Texture2D(1000,730,TextureFormat.RGBA32,false);pixels.ReadPixels(new Rect(0,0,1000,730),0,0);pixels.Apply();
                File.WriteAllBytes(path,ImageConversion.EncodeToPNG(pixels).ToArray());
            }
            finally
            {
                canvas.renderMode=previousMode;canvas.worldCamera=previousCamera;scaler.scaleFactor=previousScale;RenderTexture.active=previousTexture;
                for(int i=0;i<transforms.Length;i++)if(transforms[i]!=null)transforms[i].gameObject.layer=layers[i];
                UnityEngine.Object.Destroy(cameraRoot);if(pixels!=null)UnityEngine.Object.Destroy(pixels);target.Release();UnityEngine.Object.Destroy(target);
            }
        }


        public void Dispose()
        {
            if(root!=null)UnityEngine.Object.Destroy(root);root=null;updates.Clear();detailUpdates.Clear();
            if(radiusRing!=null)UnityEngine.Object.Destroy(radiusRing.gameObject);if(radiusMaterial!=null)UnityEngine.Object.Destroy(radiusMaterial);
            foreach(var asset in iconAssets)asset?.Dispose();
            if(ownedFont!=null)UnityEngine.Object.Destroy(ownedFont);
            if(chrome!=null)UnityEngine.Object.Destroy(chrome);if(chromeTexture!=null)UnityEngine.Object.Destroy(chromeTexture);
        }
    }
}
