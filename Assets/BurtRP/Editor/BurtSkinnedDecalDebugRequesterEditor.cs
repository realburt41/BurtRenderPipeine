using Burt.RenderPipeline;
using UnityEditor;
using UnityEngine;

namespace Burt.RenderPipeline.Editor
{
    [CustomEditor(typeof(BurtSkinnedDecalDebugRequester))]
    public sealed class BurtSkinnedDecalDebugRequesterEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            var debugInputsChanged = EditorGUI.EndChangeCheck();

            var requester = (BurtSkinnedDecalDebugRequester)target;
            if (debugInputsChanged && requester.IsApplied && !Application.isPlaying)
            {
                requester.ApplyDebugDecal();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Edit-mode test only. Apply converts Decal Transform from world space through Target Bone and bind-pose inverse into the pre-skin mesh space consumed by BurtRP/Subsurface. " +
                "A hidden material clone is used, so the source material asset is not changed.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("Apply Skinned Decal Debug"))
                {
                    requester.ApplyDebugDecal();
                }
            }

            if (GUILayout.Button("Restore Original Material and Properties"))
            {
                requester.RestoreOriginalMaterial();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("PreSkin Conversion", EditorStyles.boldLabel);
            EditorGUILayout.Vector3Field("Position", requester.LastPreSkinPosition);
            EditorGUILayout.Vector3Field("Basis X", requester.LastPreSkinBasisX);
            EditorGUILayout.Vector3Field("Basis Y", requester.LastPreSkinBasisY);

            if (!string.IsNullOrEmpty(requester.LastFailureReason))
            {
                EditorGUILayout.HelpBox(requester.LastFailureReason, MessageType.Error);
            }
            else if (requester.IsApplied)
            {
                EditorGUILayout.HelpBox("Applied. Changes to debug inputs now update the temporary material and MPB automatically.", MessageType.Info);
            }
        }
    }
}
