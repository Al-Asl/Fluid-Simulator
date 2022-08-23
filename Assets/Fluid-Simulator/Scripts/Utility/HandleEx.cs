#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class HandleEx
{
    public static void DrawBounds(Bounds bounds,Color color)
    {
        var min = bounds.min;
        var max = bounds.max;
        Handles.color = color;
        Handles.zTest = CompareFunction.LessEqual;

        Handles.DrawAAPolyLine(2f,
            new Vector3(min.x, min.y, min.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, min.y, min.z));
        Handles.DrawAAPolyLine(2f,
            new Vector3(min.x, min.y, max.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, max.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(min.x, min.y, max.z));
        Handles.DrawAAPolyLine(2f,
           new Vector3(min.x, min.y, min.z),
           new Vector3(min.x, min.y, max.z),
           new Vector3(max.x, min.y, max.z),
           new Vector3(max.x, min.y, min.z));
        Handles.DrawAAPolyLine(2f,
           new Vector3(min.x, max.y, min.z),
           new Vector3(min.x, max.y, max.z),
           new Vector3(max.x, max.y, max.z),
           new Vector3(max.x, max.y, min.z));
    }

    public static void DrawSphere(Vector3 center, float radius,Color color)
    {
        UnityEditor.Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
        UnityEditor.Handles.color = color;
        UnityEditor.Handles.DrawWireDisc(center, Vector3.right, radius);
        UnityEditor.Handles.DrawWireDisc(center, Vector3.up, radius);
        UnityEditor.Handles.DrawWireDisc(center, Vector3.forward, radius);
    }
}

#endif