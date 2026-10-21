using UnityEngine;
using LastEpochLAN.Mod;
using System.Text.Json;
int count=0;
void Check(Rect rect,float x,float y,float w,float h,string label){if(Math.Abs(rect.xMin-x)>.001 || Math.Abs(rect.yMin-y)>.001 || Math.Abs(rect.width-w)>.001 || Math.Abs(rect.height-h)>.001)throw new Exception(label);count++;Console.WriteLine("PASS: "+label);}
var viewport=new Transform();var tab=new RectTransform{rect=new(-5,-2,10,4),x=15,y=20};
Check(SharedStashGeometry.RelativeRect(viewport,tab),10,18,10,4,"tab translation is measured relative to viewport");
tab.Angle=90;Check(SharedStashGeometry.RelativeRect(viewport,tab),13,15,4,10,"all four corners account for rotated native tab transforms");
tab.Angle=0;tab.ScaleX=-2;tab.ScaleY=3;Check(SharedStashGeometry.RelativeRect(viewport,tab),5,14,20,12,"negative and nonuniform scale retain correct min/max");
tab.ScaleX=tab.ScaleY=1;viewport.x=5;viewport.y=10;viewport.ScaleX=viewport.ScaleY=2;
Check(SharedStashGeometry.RelativeRect(viewport,tab),2.5f,4,5,2,"viewport transform is inverted rather than using world coordinates");
var folder=new Transform{x=100,y=50};tab.Parent=folder;viewport.x=viewport.y=0;viewport.ScaleX=viewport.ScaleY=1;
Check(SharedStashGeometry.RelativeRect(viewport,tab),110,68,10,4,"nested category holder offsets are included");
Console.WriteLine($"All {count} actual-source geometry stub checks passed; Unity rendering not exercised.");
if(args.Length>0)File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=count,nativeGameplayVerified=false}));

namespace UnityEngine
{
 public struct Vector3 {public float x,y,z;public Vector3(float px,float py,float pz){x=px;y=py;z=pz;}}
 public struct Rect {public float xMin,yMin,width,height;public Rect(float x,float y,float w,float h){xMin=x;yMin=y;width=w;height=h;}public float xMax=>xMin+width;public float yMax=>yMin+height;}
 public static class Mathf{public static float Min(float a,float b)=>Math.Min(a,b);public static float Max(float a,float b)=>Math.Max(a,b);}
 public class Transform
 {
  public float x,y,Angle,ScaleX=1,ScaleY=1;public Transform? Parent;
  public Vector3 TransformPoint(Vector3 p){float a=Angle*MathF.PI/180,c=MathF.Cos(a),s=MathF.Sin(a);var result=new Vector3(x+c*p.x*ScaleX-s*p.y*ScaleY,y+s*p.x*ScaleX+c*p.y*ScaleY,p.z);return Parent?.TransformPoint(result)??result;}
  public Vector3 InverseTransformPoint(Vector3 p){if(Parent!=null)p=Parent.InverseTransformPoint(p);float a=Angle*MathF.PI/180,c=MathF.Cos(a),s=MathF.Sin(a),dx=p.x-x,dy=p.y-y;return new((c*dx+s*dy)/ScaleX,(-s*dx+c*dy)/ScaleY,p.z);}
 }
 public class RectTransform:Transform{public Rect rect;}
}
