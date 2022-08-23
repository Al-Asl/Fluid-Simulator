using UnityEngine;

public static class GameObjectUtils
{
    public static Mesh GetPrimitiveMesh(PrimitiveType meshType)
    {
        var go = GameObject.CreatePrimitive(meshType);
        var mesh = go.GetComponent<MeshFilter>().sharedMesh;
        SafeDestroy(go);
        return mesh;
    }

    public static T AddOrGetComponenet<T>(GameObject gameObject) where T : Component
    {
        var comp = gameObject.GetComponent<T>();
        if (comp == null)
            return gameObject.AddComponent<T>();
        else
            return comp;
    }

    public static void SafeDestroy(Object obj)
    {
        if (obj == null)
            return;

#if UNITY_EDITOR
        if (UnityEditor.EditorApplication.isPlaying)
            Object.Destroy(obj);
        else
            Object.DestroyImmediate(obj, false);
#else
            Object.Destroy(obj);
#endif
    }
}