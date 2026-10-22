using UnityEngine;

namespace LastEpochLAN.Mod.OfflineTools.WorldAssist;
internal static partial class WorldAssistRuntime
{
    internal static bool SettingsOpen;
    private static Vector2 settingsScroll,targetScroll;
    private static int settingsPage;
    internal static void DrawSettings()
    {
        var o=Options;o.Normalize();
        float width=Mathf.Min(1060,Screen.width-32),height=Mathf.Min(860,Screen.height-32);
        GUI.Box(new Rect(16,16,width,height),"World Assist");
        if(GUI.Button(new Rect(30,47,120,27),"Display / types"))settingsPage=0;
        if(GUI.Button(new Rect(160,47,140,27),"Choose target"))settingsPage=1;
        if(GUI.Button(new Rect(16+width-135,47,120,27),"Back"))SettingsOpen=false;
        GUI.Label(new Rect(30,80,width-30,24),Status);
        bool previous=GUI.changed;GUI.changed=false;
        if(settingsPage==0)
        {
            settingsScroll=GUI.BeginScrollView(new Rect(28,110,width-24,height-110),settingsScroll,new Rect(0,0,Mathf.Max(900,width-50),865));
            try
            {
                o.Enabled=GUI.Toggle(new Rect(0,0,210,24),o.Enabled,"Enable World Assist");
                o.DangerAreas=GUI.Toggle(new Rect(220,0,210,24),o.DangerAreas,"Enemy danger areas");
                o.Targets=GUI.Toggle(new Rect(440,0,190,24),o.Targets,"World targets");
                o.Smooth=GUI.Toggle(new Rect(0,30,210,24),o.Smooth,"Smooth drawing");
                o.HideCompleted=GUI.Toggle(new Rect(220,30,210,24),o.HideCompleted,"Hide completed targets");
                o.WorldPath=GUI.Toggle(new Rect(0,60,210,24),o.WorldPath,"Path in world");
                o.MapPath=GUI.Toggle(new Rect(220,60,210,24),o.MapPath,"Path on Tab map");
                o.MinimapPath=GUI.Toggle(new Rect(440,60,210,24),o.MinimapPath,"Path on minimap");
                GUI.Label(new Rect(0,90,220,24),$"Target range: {o.Range:0}m");
                o.Range=GUI.HorizontalSlider(new Rect(230,98,360,20),o.Range,20,1000);
                GUI.Label(new Rect(0,126,220,24),"Target type / colour");
                foreach(var item in new[]{("World",350),("Arrow",450),("Tab",550),("Mini",650),("Route",750)})GUI.Label(new Rect(item.Item2,126,90,24),item.Item1);
                for(int i=0;i<14;i++)
                {
                    float y=155+i*29;var style=o.Styles[i];
                    var saved=GUI.color;GUI.color=Colour(style.Colour);
                    GUI.Label(new Rect(0,y,180,25),AssistRules.Labels[i]);GUI.color=saved;
                    style.Colour=GUI.TextField(new Rect(190,y,105,24),style.Colour??"#FFFFFF",9);
                    style.World=GUI.Toggle(new Rect(350,y,85,24),style.World,"");
                    style.Arrow=GUI.Toggle(new Rect(450,y,85,24),style.Arrow,"");
                    style.Map=GUI.Toggle(new Rect(550,y,85,24),style.Map,"");
                    style.Minimap=GUI.Toggle(new Rect(650,y,85,24),style.Minimap,"");
                    style.Path=GUI.Toggle(new Rect(750,y,85,24),style.Path,"");
                }
                GUI.Label(new Rect(0,570,320,24),$"Default area radius: {o.AreaRadius:0.00}m");
                o.AreaRadius=GUI.HorizontalSlider(new Rect(330,578,300,20),o.AreaRadius,.1f,20);
                GUI.Label(new Rect(0,605,320,24),$"Default projectile radius: {o.ProjectileRadius:0.00}m");
                o.ProjectileRadius=GUI.HorizontalSlider(new Rect(330,613,300,20),o.ProjectileRadius,.1f,10);
                GUI.Label(new Rect(0,640,320,24),$"Projectile path length: {o.ProjectileLength:0.0}m");
                o.ProjectileLength=GUI.HorizontalSlider(new Rect(330,648,300,20),o.ProjectileLength,1,40);
                GUI.Label(new Rect(0,678,900,40),"Ability overrides, separated by ; or a new line. Examples: Flaming Ground=4; Ability Name=cone:60:8; Arrow=projectile:0.75:12");
                o.AbilityRadii=GUI.TextArea(new Rect(0,724,Mathf.Min(880,width-65),85),o.AbilityRadii,12000);
                GUI.Label(new Rect(0,815,900,35),badRadii>0?$"{badRadii} invalid radius entries ignored.":"Own and friendly abilities are hidden. Shapes show estimated danger areas.");
            }
            finally{GUI.EndScrollView();}
        }
        else
        {
            if(GUI.Button(new Rect(30,112,270,28),selected==0?"Nearest available target (active)":"Use nearest available target")){selected=0;ClearPath();}
            GUI.Label(new Rect(320,112,width-340,28),"Select any loaded target to show its walkable route.");
            var player=playerMotion.Position;
            var rows=targets.Where(t=>t.Transform!=null && !t.Completed).OrderBy(t=>(t.Transform.position-player).sqrMagnitude).ToArray();
            targetScroll=GUI.BeginScrollView(new Rect(30,152,width-28,height-150),targetScroll,new Rect(0,0,width-55,rows.Length*31+8));
            try
            {
                for(int i=0;i<rows.Length;i++)
                {
                    var t=rows[i];var saved=GUI.color;GUI.color=Colour(Options.Styles[(int)t.Kind].Colour);
                    string label=$"{(t.Id==selected?"> ":"")}{AssistRules.Labels[(int)t.Kind]}: {t.Name} — {(t.Transform.position-player).magnitude:0}m";
                    if(GUI.Button(new Rect(0,i*31,width-65,28),label))
                    {selected=t.Id;Options.Styles[(int)t.Kind].Path=true;ClearPath();}
                    GUI.color=saved;
                }
            }
            finally{GUI.EndScrollView();}
        }
        if(GUI.changed){o.Normalize();OfflineToolkit.ScheduleSave();}
        GUI.changed|=previous;
    }
}
