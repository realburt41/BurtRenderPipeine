using Burt.RenderPipeline;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BurtMultipassRenderer))]
internal sealed class BurtMultipassRendererEditor : Editor
{
    private SerializedProperty layerCount;
    private SerializedProperty renderingLayerMask;

    private void OnEnable()
    {
        layerCount = serializedObject.FindProperty("m_LayerCount");
        renderingLayerMask = serializedObject.FindProperty("m_RenderingLayerMask");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(layerCount, new GUIContent("Layer Count"));
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
}
