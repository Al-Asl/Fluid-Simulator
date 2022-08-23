using System.Collections;
using UnityEngine;

public enum ShapeType
{
    Sphere,
    Box
}

public static class ShapeUtility
{
    public static void UpdateShape(ref IShape shape, ShapeType shapeType, Transform transform)
    {
        shape.localToWorld = transform.localToWorldMatrix;
    }

    public static void SetShape(ref IShape shape, ShapeType shapeType, Transform transform)
    {
        if (shape.GetShapeType() == shapeType)
            return;

        switch (shapeType)
        {
            case ShapeType.Sphere:
                shape = new Sphere(Vector3.zero, 0.5f, transform.localToWorldMatrix);
                break;
            case ShapeType.Box:
                shape = new Box(Vector3.zero, Vector3.one, transform.localToWorldMatrix);
                break;
        }
    }

    public static void ShapeGizmos(Transform transform, IShape shape, Color color)
    {
        var shapeType = shape.GetShapeType();
#if UNITY_EDITOR
        UnityEditor.Handles.matrix = transform.localToWorldMatrix;
        switch (shapeType)
        {
            case ShapeType.Sphere:
                var sphere = (Sphere)shape;
                HandleEx.DrawSphere(sphere.center, sphere.radius, color);
                break;
            case ShapeType.Box:
                var box = (Box)shape;
                HandleEx.DrawBounds(new Bounds(box.center, box.size), color);
                break;
        }
        UnityEditor.Handles.matrix = Matrix4x4.identity;
#endif
    }

    public static Bounds TransformBound(Vector3 center, Vector3 ex, Matrix4x4 localToWorld)
    {
        var b = new Bounds(localToWorld.MultiplyPoint(center + new Vector3(ex.x, ex.y, ex.z)), Vector3.zero);
        b.Encapsulate(localToWorld.MultiplyPoint(center + new Vector3(-ex.x, ex.y, ex.z)));
        b.Encapsulate(localToWorld.MultiplyPoint(center + new Vector3(ex.x, -ex.y, ex.z)));
        b.Encapsulate(localToWorld.MultiplyPoint(center + new Vector3(-ex.x, -ex.y, ex.z)));
        b.Encapsulate(localToWorld.MultiplyPoint(center + new Vector3(ex.x, ex.y, -ex.z)));
        b.Encapsulate(localToWorld.MultiplyPoint(center + new Vector3(-ex.x, ex.y, -ex.z)));
        b.Encapsulate(localToWorld.MultiplyPoint(center + new Vector3(ex.x, -ex.y, -ex.z)));
        b.Encapsulate(localToWorld.MultiplyPoint(center + new Vector3(-ex.x, -ex.y, -ex.z)));
        return b;
    }

    public static ShapeType GetShapeType(this IShape shape)
    {
        if (shape is Sphere)
            return ShapeType.Sphere;
        else if (shape is Box)
            return ShapeType.Box;
        else
            throw new System.Exception("Shape not defined!");
    }
}

public interface IShape
{
    Matrix4x4 localToWorld { get; set; }
    Bounds bounds { get; }
}

[System.Serializable]
public struct Sphere : IShape
{
    public Bounds bounds => ShapeUtility.TransformBound(center, Vector3.one * radius, localToWorld);

    public Matrix4x4 localToWorld { get; set; }
    public float radius;
    public Vector3 center;

    public Sphere(Vector3 center, float radius, Matrix4x4 localToWorld)
    {
        this.localToWorld = localToWorld;
        this.radius = radius;
        this.center = center;
    }

    public Vector4 ToVector4() => new Vector4(center.x, center.y, center.z, radius);
}

[System.Serializable]
public struct Box : IShape
{
    public Bounds bounds => ShapeUtility.TransformBound(center, size * 0.5f, localToWorld);

    public Matrix4x4 localToWorld { get; set; }
    public Vector3 center;
    public Vector3 size;

    public Box(Vector3 center,Vector3 size,Matrix4x4 localToWorld)
    {
        this.size = size;
        this.center = center;
        this.localToWorld = localToWorld;
    }
}