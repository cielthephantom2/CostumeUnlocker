using UnityEngine;

namespace LastEpochLAN.Mod;

// RectTransformUtility.CalculateRelativeRectTransformBounds is an unstripping
// stub in Unity6000.4.8. Transform four rect corners through native bindings.
internal static class SharedStashGeometry
{
    internal static Rect RelativeRect(Transform relativeTo, RectTransform target)
    {
        var rect=target.rect;
        float minX=float.PositiveInfinity,minY=float.PositiveInfinity,maxX=float.NegativeInfinity,maxY=float.NegativeInfinity;
        for(int i=0;i<4;i++)
        {
            var point=relativeTo.InverseTransformPoint(target.TransformPoint(new Vector3(
                (i&1)==0?rect.xMin:rect.xMax,(i&2)==0?rect.yMin:rect.yMax,0)));
            minX=Mathf.Min(minX,point.x);minY=Mathf.Min(minY,point.y);
            maxX=Mathf.Max(maxX,point.x);maxY=Mathf.Max(maxY,point.y);
        }
        return new Rect(minX,minY,maxX-minX,maxY-minY);
    }
}
