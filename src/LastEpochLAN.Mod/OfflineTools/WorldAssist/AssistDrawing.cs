using Il2Cpp;
using Il2CppDMM;
using UnityEngine;
using UnityEngine.UI;
using UObj=UnityEngine.Object;
using IconManager=Il2CppLE.UI.Minimap.MinimapIconManager;
using NVec2=System.Numerics.Vector2;

namespace LastEpochLAN.Mod.OfflineTools.WorldAssist;

internal static partial class WorldAssistRuntime
{
    private static readonly Sprite[] sprites=new Sprite[14];
    private static Texture2D atlas;
    private static Sprite transparentSprite;
    private static Texture2D transparentTexture;
    private static readonly GameObject[] probes=new GameObject[3];
    private static readonly DMMapIcon[] probeIcons=new DMMapIcon[3];
    private static readonly Vector3[] probeWorld=new Vector3[3];
    private static readonly Vector3[] rectCorners=new Vector3[4];
    private static readonly Vector2[] probeScreen=new Vector2[3];
    private static IntPtr mapManager;
    private static int mapFrame=-1;
    private static Vector2 mapX,mapZ;
    private static Rect mapRect;
    private static bool tabProjection;
    private static void EnsureSprites()
    {
        if(atlas!=null)return;
        atlas=new Texture2D(14*24,24,TextureFormat.RGBA32,false);
        atlas.name="TM World target symbols";atlas.filterMode=FilterMode.Bilinear;atlas.wrapMode=TextureWrapMode.Clamp;
        for(int kind=0;kind<14;kind++)for(int y=0;y<24;y++)for(int x=0;x<24;x++)
        {
            float dx=x-11.5f,dy=y-11.5f;
            int sides=3+kind%7;float step=Mathf.PI*2/sides;
            float theta=Mathf.Atan2(dy,dx)+Mathf.PI/2;
            float edge=8*Mathf.Cos(Mathf.PI/sides)/Mathf.Cos(Mathf.Repeat(theta+step/2,step)-step/2);
            float radius=Mathf.Sqrt(dx*dx+dy*dy);
            float alpha=Mathf.Clamp01(edge+1-radius);
            if(kind>=7)alpha*=Mathf.Clamp01(radius-(edge-2.8f));
            atlas.SetPixel(kind*24+x,y,new Color(1,1,1,alpha));
        }
        atlas.Apply(false,true);
        for(int i=0;i<14;i++)sprites[i]=Sprite.Create(atlas,new Rect(i*24,0,24,24),new Vector2(.5f,.5f),100);
    }
    internal static Color Colour(string hex,float alpha=1)
    {
        if(!ColorUtility.TryParseHtmlString(hex??"",out var color))color=Color.white;
        color.a=alpha;return color;
    }
    private static void EnsureMapProbes()
    {
        var manager=IconManager.Instance;
        var player=PlayerFinder.getLocalActorSync()?.transform;
        if(manager==null || player==null)return;
        if(mapManager!=manager.Pointer){ClearMapProbes();mapManager=manager.Pointer;}
        if(transparentTexture==null)
        {
            transparentTexture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            for(int y=0;y<2;y++)for(int x=0;x<2;x++)transparentTexture.SetPixel(x,y,Color.clear);
            transparentTexture.Apply(false,true);
            transparentSprite=Sprite.Create(transparentTexture,new Rect(0,0,2,2),new Vector2(.5f,.5f),100);
        }
        for(int i=0;i<3;i++)
        {
            if(probes[i]==null)
            {
                probes[i]=new GameObject("TM Map projection probe "+i);probes[i].SetActive(false);
                probeIcons[i]=probes[i].AddComponent<DMMapIcon>();ConfigureIcon(probeIcons[i],transparentSprite);
                probes[i].SetActive(true);
            }
            // Three tiny invisible native icons calibrate the *current* map
            // transform, including zoom/pan/UI scale. No hardcoded shader layout.
            probeWorld[i]=player.position+(i==1?Vector3.right*2:i==2?Vector3.forward*2:Vector3.zero);
            probes[i].transform.position=probeWorld[i];
        }
    }
    internal static void ReadMapProjection(IconManager manager)
    {
        mapFrame=-1;
        if(!wasReady || !Options.Enabled || !OfflineToolkit.Ready || manager==null || manager.Pointer!=mapManager)return;
        var map=manager.minimap;
        if(map==null || !map.isActiveAndEnabled)return;
        var image=Il2CppDMM.DMMap.Instance?.mapImage;
        if(image==null || !image.isActiveAndEnabled)return;
        var rect=image!=null?image.rectTransform:map.GetComponent<RectTransform>();
        if(rect==null)return;
        var canvas=rect.GetComponentInParent<Canvas>();
        var uiCamera=canvas!=null && canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
        int found=0;
        var icons=manager.icons;
        if(icons==null)return;
        for(int i=0;i<icons.Count;i++)
        {
            var info=icons[i];if(info?.icon==null || info.ui==null)continue;
            for(int j=0;j<3;j++)if(probeIcons[j]!=null && info.icon.Pointer==probeIcons[j].Pointer)
            {
                var point=RectTransformUtility.WorldToScreenPoint(uiCamera,info.ui.transform.position);
                probeScreen[j]=new Vector2(point.x,Screen.height-point.y);
                if(info.ui.MainImage!=null)info.ui.MainImage.raycastTarget=false;
                found++;break;
            }
        }
        if(found!=3)return;
        mapX=(probeScreen[1]-probeScreen[0])/2;mapZ=(probeScreen[2]-probeScreen[0])/2;
        float determinant=mapX.x*mapZ.y-mapX.y*mapZ.x;
        if(!float.IsFinite(determinant) || Mathf.Abs(determinant)<.0001f)return;
        rect.GetWorldCorners(rectCorners);
        float left=float.MaxValue,top=float.MaxValue,right=float.MinValue,bottom=float.MinValue;
        foreach(var p in rectCorners)
        {
            var point=RectTransformUtility.WorldToScreenPoint(uiCamera,p);
            left=Mathf.Min(left,point.x);right=Mathf.Max(right,point.x);
            top=Mathf.Min(top,Screen.height-point.y);bottom=Mathf.Max(bottom,Screen.height-point.y);
        }
        mapRect=Rect.MinMaxRect(Mathf.Max(0,left),Mathf.Max(0,top),Mathf.Min(Screen.width,right),Mathf.Min(Screen.height,bottom));
        tabProjection=UIBase.instance?.overlayMapOpen==true;mapFrame=Time.frameCount;
    }
    private static void ClearMapProbes()
    {
        for(int i=0;i<3;i++)
        {if(probes[i]!=null){probes[i].SetActive(false);UObj.Destroy(probes[i]);}probes[i]=null;probeIcons[i]=null;}
        if(transparentSprite!=null)UObj.Destroy(transparentSprite);transparentSprite=null;
        if(transparentTexture!=null)UObj.Destroy(transparentTexture);transparentTexture=null;
        mapManager=IntPtr.Zero;mapFrame=-1;
    }
    private static Vector2 ProjectMap(Vector3 world)=>probeScreen[0]+mapX*(world.x-probeWorld[0].x)+mapZ*(world.z-probeWorld[0].z);
    private static void Line(Vector2 a,Vector2 b,Color color,float width=1.5f)
    {
        var delta=b-a;float length=delta.magnitude;
        if(!float.IsFinite(length) || length<.2f || length>Screen.width*3)return;
        var matrix=GUI.matrix;var saved=GUI.color;
        try
        {
            GUI.color=color;GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,a);
            GUI.DrawTexture(new Rect(a.x,a.y-width/2,length,width),Texture2D.whiteTexture);
        }
        finally{GUI.matrix=matrix;GUI.color=saved;}
    }
    private static bool Project(Camera camera,Vector3 world,out Vector2 screen)
    {
        var p=camera.WorldToScreenPoint(world);screen=new Vector2(p.x,Screen.height-p.y);
        return p.z>camera.nearClipPlane && float.IsFinite(p.x+p.y);
    }
    private static void WorldLine(Camera camera,Vector3 a,Vector3 b,Color color,float width=1.5f)
    {if(Project(camera,a,out var from) && Project(camera,b,out var to))ClippedLine(from,to,new Rect(0,0,Screen.width,Screen.height),color,width);}
    private static void ClippedLine(Vector2 a,Vector2 b,Rect rect,Color color,float width=1.5f)
    {
        var start=new NVec2(a.x,a.y);var end=new NVec2(b.x,b.y);
        if(AssistRules.Clip(ref start,ref end,rect.xMin,rect.yMin,rect.xMax,rect.yMax))Line(new Vector2(start.X,start.Y),new Vector2(end.X,end.Y),color,width);
    }
    private static void Ring(Camera camera,Vector3 center,float radius,Color color)
    {
        var before=center+Vector3.right*radius;
        for(int i=1;i<=28;i++)
        {
            float a=i*Mathf.PI*2/28;var p=center+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);
            WorldLine(camera,before,p,color);before=p;
        }
    }
    private static void DrawHazard(Camera camera,Hazard h,double now)
    {
        var center=h.Motion.Render(now)+Vector3.up*.08f;
        var color=new Color(1,.22f,.1f,.85f);var spec=h.Spec;
        if(spec.Shape==HazardShape.Ring)Ring(camera,center,spec.Radius,color);
        else if(spec.Shape==HazardShape.Cone)
        {
            if(h.Direction.sqrMagnitude<.01f){Ring(camera,center,spec.Length,color);return;}
            var before=center;var yaw=Mathf.Atan2(h.Direction.z,h.Direction.x);
            for(int i=0;i<=24;i++)
            {
                float angle=yaw+(-spec.Angle/2+spec.Angle*i/24)*Mathf.Deg2Rad;
                var point=center+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*spec.Length;
                WorldLine(camera,before,point,color);before=point;
            }
            WorldLine(camera,before,center,color);
        }
        else
        {
            var end=center+h.Direction*spec.Length;
            var side=new Vector3(-h.Direction.z,0,h.Direction.x)*spec.Radius;
            Ring(camera,center,spec.Radius,color);
            WorldLine(camera,center+side,end+side,color);WorldLine(camera,center-side,end-side,color);
            WorldLine(camera,end-side,end+side,color);
            WorldLine(camera,center,end,new Color(1,.6f,.2f,.65f));
        }
        if(Project(camera,center,out var screen) && screen.x>=0 && screen.x<Screen.width && screen.y>=0 && screen.y<Screen.height)
        {var saved=GUI.color;GUI.color=color;GUI.Label(new Rect(screen.x+8,screen.y-18,230,22),h.Name);GUI.color=saved;}
    }
    internal static void Draw()
    {
        if(Event.current.type!=EventType.Repaint || !wasReady || !Options.Enabled || !OfflineToolkit.Ready)return;
        var color=GUI.color;var matrix=GUI.matrix;bool enabled=GUI.enabled;
        try
        {
            GUI.matrix=Matrix4x4.identity;GUI.enabled=true;
            var camera=Camera.main;if(camera==null)return;
            double now=Time.realtimeSinceStartupAsDouble;
            bool tab=UIBase.instance?.overlayMapOpen==true;
            // The map remains readable while it covers the world view.
            if(!tab && Options.DangerAreas)
            {
                int count=0;
                foreach(var h in visibleHazards)
                {if(!h.Enemy || h.Motion.Time<0)continue;DrawHazard(camera,h,now);if(++count>=64)break;}
            }
            if(!tab && Options.Targets)
            {
                EnsureSprites();
                foreach(var t in targets)
                {
                    var style=Options.Styles[(int)t.Kind];
                    if(t.Transform==null || Options.HideCompleted && t.Completed)continue;
                    var position=t.Motion.Render(now);
                    float distance=(position-playerMotion.Position).magnitude;
                    if(distance>Options.Range)continue;
                    bool front=Project(camera,position+Vector3.up*.5f,out var screen);
                    bool onscreen=front && screen.x>24 && screen.x<Screen.width-24 && screen.y>24 && screen.y<Screen.height-45;
                    var tint=Colour(style.Colour,t.Completed ? .3f : 1);
                    if(onscreen && style.World)
                    {
                        GUI.color=tint;
                        GUI.DrawTextureWithTexCoords(new Rect(screen.x-10,screen.y-10,20,20),atlas,new Rect((int)t.Kind/14f,0,1/14f,1));
                        GUI.Label(new Rect(screen.x+12,screen.y-12,250,24),$"{t.Name} {distance:0}m");
                    }
                    else if(!onscreen && style.Arrow)
                    {
                        var center=new Vector2(Screen.width/2f,Screen.height/2f);
                        var direction=screen-center;if(!front)direction=-direction;
                        if(direction.sqrMagnitude<1)continue;direction.Normalize();
                        float radius=Mathf.Min((Screen.width/2f-34)/Mathf.Max(.001f,Mathf.Abs(direction.x)),(Screen.height/2f-50)/Mathf.Max(.001f,Mathf.Abs(direction.y)));
                        var tip=center+direction*radius;var side=new Vector2(-direction.y,direction.x);
                        Line(tip,tip-direction*13+side*7,tint,2.5f);Line(tip,tip-direction*13-side*7,tint,2.5f);
                        GUI.color=tint;GUI.Label(new Rect(Mathf.Clamp(tip.x-55,0,Screen.width-120),Mathf.Clamp(tip.y+10,0,Screen.height-24),120,24),$"{AssistRules.Labels[(int)t.Kind]} {distance:0}");
                    }
                }
            }
            DrawPath(camera,now,tab);
        }
        finally{GUI.color=color;GUI.matrix=matrix;GUI.enabled=enabled;}
    }
    private static void DrawPath(Camera camera,double now,bool tab)
    {
        if(!pathHeadClear || path.Length<2 || !byId.TryGetValue(pathTarget,out var target) || target.Transform==null || target.Completed)return;
        // Do not show a stale route after teleporting or a moving target leaves it.
        var player=playerMotion.Render(now);
        if(((target.PathTransform!=null?target.PathTransform.position:target.Transform.position)-pathGoal).sqrMagnitude>100)return;
        int start=pathTail;
        if(start<=0 || start>=path.Length)return;
        if((player-U(path[start-1])).sqrMagnitude>400 && (player-U(path[start])).sqrMagnitude>400)return;
        var tint=Colour(Options.Styles[(int)target.Kind].Colour,partialPath ? .35f : .9f);
        var before=player;
        for(int i=start;i<path.Length;i++)
        {
            var point=U(path[i]);
            if(!tab && Options.WorldPath)WorldLine(camera,before+Vector3.up*.12f,point+Vector3.up*.12f,tint,2.2f);
            if(mapFrame==Time.frameCount && tab==tabProjection && (tab?Options.MapPath:Options.MinimapPath))
                ClippedLine(ProjectMap(before),ProjectMap(point),mapRect,tint,2);
            before=point;
        }
    }
}
