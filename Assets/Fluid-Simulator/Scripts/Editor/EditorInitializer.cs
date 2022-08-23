using UnityEngine;
using UnityEditor;

public static class EditorInitializer
{
    [MenuItem("GameObject/Fluid/Field", priority = 14)]
    public static void CreateField()
    {
        GameObject go = new GameObject("Fluid Field");
        go.AddComponent<FluidField>();
        Undo.RegisterCreatedObjectUndo(go, "Create Field");
        Selection.activeObject = go;
    }

    [MenuItem("GameObject/Fluid/Injector", priority = 15)]
    public static void CreateInjector()
    {
        FluidField field = GetField();
        GameObject go = new GameObject("Injector");
        if (field != null)
            go.transform.SetParent(field.transform, false);
        go.AddComponent<FluidInjector>();
        Undo.RegisterCreatedObjectUndo(go, "Create Injector");
        Selection.activeObject = go;
    }

    [MenuItem("GameObject/Fluid/Collider", priority = 15)]
    public static void CreateCollider()
    {
        FluidField field = GetField();
        GameObject go = new GameObject("Collider");
        if (field != null)
            go.transform.SetParent(field.transform, false);
        go.AddComponent<FluidCollider>();
        Undo.RegisterCreatedObjectUndo(go, "Create Collider");
        Selection.activeObject = go;
    }

    [MenuItem("GameObject/Fluid/Motor", priority = 15)]
    public static void CreateMotor()
    {
        FluidField field = GetField();
        GameObject go = new GameObject("Motor");
        if (field != null)
            go.transform.SetParent(field.transform, false);
        go.AddComponent<FluidMotor>();
        Undo.RegisterCreatedObjectUndo(go, "Create Motor");
        Selection.activeObject = go;
    }

    private static FluidField GetField()
    {
        FluidField field;
        if (Selection.activeGameObject != null)
            field = FluidField.GetFeild(Selection.activeGameObject);
        else
            field = Object.FindObjectOfType<FluidField>();
        return field;
    }
}