using Burt.RenderPipeline;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BurtMultipassRenderer))]
internal sealed class BurtMultipassRendererEditor : Editor
{
    private SerializedProperty layerCount;
    private SerializedProperty supportDifferentPassCount;
    private SerializedProperty layerCountList;
    private SerializedProperty distanceFadeCurve;
    private SerializedProperty renderingLayerMask;

    private void OnEnable()
    {
        layerCount = serializedObject.FindProperty("m_LayerCount");
        supportDifferentPassCount = serializedObject.FindProperty("m_SupportDifferentPassCount");
        layerCountList = serializedObject.FindProperty("m_LayerCountList");
        distanceFadeCurve = serializedObject.FindProperty("m_DistanceFadeCurve");
        renderingLayerMask = serializedObject.FindProperty("m_RenderingLayerMask");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(layerCount, new GUIContent("Layer Count"));
        EditorGUILayout.PropertyField(supportDifferentPassCount, new GUIContent("Different Pass Count"));
        if (supportDifferentPassCount.boolValue)
        {
            EditorGUILayout.PropertyField(layerCountList, new GUIContent("Layer Count List"), true);
        }

        DrawShaderSupportWarning();
        EditorGUILayout.PropertyField(distanceFadeCurve, new GUIContent("Distance Fade Curve"));
        EditorGUILayout.PropertyField(renderingLayerMask, new GUIContent("Rendering Layer Mask"));

        serializedObject.ApplyModifiedProperties();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Sync Renderer"))
            {
                foreach (var targetObject in targets)
                {
                    var renderer = targetObject as BurtMultipassRenderer;
                    if (renderer == null)
                    {
                        continue;
                    }

                    Undo.RecordObject(renderer, "Sync Multipass Renderer");
                    renderer.OnMeshChanged();
                    EditorUtility.SetDirty(renderer);
                }
            }

            if (GUILayout.Button("Refresh Pass Cache"))
            {
                foreach (var targetObject in targets)
                {
                    var renderer = targetObject as BurtMultipassRenderer;
                    if (renderer != null)
                    {
                        renderer.OnMaterialChanged();
                    }
                }
            }
        }
    }

    private void DrawShaderSupportWarning()
    {
        for (var targetIndex = 0; targetIndex < targets.Length; targetIndex++)
        {
            var multipassRenderer = targets[targetIndex] as BurtMultipassRenderer;
            var unsupportedMaterial = FindFirstUnsupportedMaterial(multipassRenderer);
            if (unsupportedMaterial == null)
            {
                continue;
            }

            var shaderName = unsupportedMaterial.shader != null ? unsupportedMaterial.shader.name : "<none>";
            EditorGUILayout.HelpBox(
                $"Only materials using shader \"{BurtMultipassRenderer.SupportedShaderName}\" are drawn by this component. " +
                $"\"{unsupportedMaterial.name}\" uses \"{shaderName}\" and will be ignored.",
                MessageType.Warning);
            return;
        }

        EditorGUILayout.HelpBox(
            $"Supported shader: \"{BurtMultipassRenderer.SupportedShaderName}\".",
            MessageType.Info);
    }

    private static Material FindFirstUnsupportedMaterial(BurtMultipassRenderer multipassRenderer)
    {
        if (multipassRenderer == null)
        {
            return null;
        }

        var renderer = multipassRenderer.GetComponent<Renderer>();
        var sharedMaterials = renderer != null ? renderer.sharedMaterials : null;
        var materialCount = sharedMaterials != null ? sharedMaterials.Length : 0;
        for (var materialIndex = 0; materialIndex < materialCount; materialIndex++)
        {
            var material = sharedMaterials[materialIndex];
            if (material != null && !BurtMultipassRenderer.IsSupportedMaterial(material))
            {
                return material;
            }
        }

        return null;
    }
}
