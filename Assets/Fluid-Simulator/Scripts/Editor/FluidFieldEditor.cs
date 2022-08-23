using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(FluidField))]
public class FluidFieldEditor : Editor
{
    public override bool HasPreviewGUI()
    {
        return EditorApplication.isPlaying;
    }

    private GUIStyle m_PreviewLabelStyle;

    protected GUIStyle previewLabelStyle
    {
        get
        {
            if (m_PreviewLabelStyle == null)
            {
                m_PreviewLabelStyle = new GUIStyle("PreOverlayLabel")
                {
                    richText = true,
                    alignment = TextAnchor.UpperLeft,
                    fontStyle = FontStyle.Normal
                };
            }

            return m_PreviewLabelStyle;
        }
    }

    public override bool RequiresConstantRepaint()
    {
        return Application.isPlaying;
    }

    public override void OnPreviewGUI(Rect rect, GUIStyle background)
    {
        if (target == null)
            return;

        GUI.Label(rect, target.ToString(), previewLabelStyle);
    }
}