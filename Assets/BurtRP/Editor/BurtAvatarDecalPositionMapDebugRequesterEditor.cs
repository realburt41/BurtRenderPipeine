using Burt.RenderPipeline;
using UnityEditor;
using UnityEngine;

namespace Burt.RenderPipeline.Editor
{
    [CustomEditor(typeof(BurtAvatarDecalPositionMapDebugRequester))]
    public sealed class BurtAvatarDecalPositionMapDebugRequesterEditor : UnityEditor.Editor
    {
        private const string PositionMapPreviewShaderName = "Hidden/BurtRP/AvatarDecal/PositionMapPreview";
        private static readonly int PositionMapPreviewRangeId = Shader.PropertyToID("_BurtAvatarDecalPreviewRange");
        private static Material positionMapPreviewMaterial;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var requester = (BurtAvatarDecalPositionMapDebugRequester)target;
            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(BurtRenderPipeline.Current == null || requester.TargetRenderer == null))
            {
                if (GUILayout.Button("Request Position Map"))
                {
                    requester.RequestPositionMap();
                }
            }

            if (GUILayout.Button("Release Position Map"))
            {
                requester.ReleasePositionMap();
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(
                       BurtRenderPipeline.Current == null ||
                       requester.TargetRenderer == null ||
                       requester.SourceBaseMap == null ||
                       requester.DecalBaseMap == null ||
                       requester.DecalTransform == null))
            {
                if (GUILayout.Button("Combine Base Map"))
                {
                    requester.CombineBaseMap();
                }
            }

            if (GUILayout.Button("Release Combined Base Map"))
            {
                requester.ReleaseCombinedBaseMap();
            }

            using (new EditorGUI.DisabledScope(
                       requester.CombineResult == null ||
                       requester.CombineResult.Status != BurtAvatarDecalCombineStatus.Combined))
            {
                if (GUILayout.Button("Compress Combined Base Map (BC7; BC3 Fallback)"))
                {
                    requester.CompressCombinedBaseMap();
                }
            }

            using (new EditorGUI.DisabledScope(
                       requester.CombineResult == null ||
                       (requester.CombineResult.Status != BurtAvatarDecalCombineStatus.Combined &&
                        requester.CombineResult.Status != BurtAvatarDecalCombineStatus.Compressed) ||
                       (requester.CombineResult.Status == BurtAvatarDecalCombineStatus.Compressed
                           ? requester.CombineResult.CompressedBaseMap == null
                           : requester.CombineResult.CombinedBaseMap == null)))
            {
                if (GUILayout.Button("Apply Combined Base Map To Target"))
                {
                    requester.ApplyCombinedBaseMap();
                }
            }

            if (GUILayout.Button("Restore Original Base Map"))
            {
                requester.RestoreOriginalBaseMap();
            }

            var result = requester.Result;
            if (result == null)
            {
                EditorGUILayout.HelpBox("No PositionMap has been requested.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("PositionMap Status", result.Status.ToString());
            if (!string.IsNullOrEmpty(result.FailureReason))
            {
                EditorGUILayout.HelpBox(result.FailureReason, MessageType.Error);
            }
            else if (result.Status == BurtAvatarDecalPositionMapStatus.Pending)
            {
                EditorGUILayout.HelpBox("The request executes at the beginning of the next BurtRP frame. Capture that frame in RenderDoc.", MessageType.Info);
            }
            else if (result.Status == BurtAvatarDecalPositionMapStatus.Ready)
            {
                EditorGUILayout.HelpBox("Search RenderDoc Event Browser for: Burt Avatar Decal / Generate PositionMap", MessageType.Info);
                EditorGUILayout.ObjectField("Position Map", result.PositionMap, typeof(RenderTexture), false);
                DrawPositionMapPreview(result.PositionMap, requester.PreviewPositionRange);
            }

            DrawBaseMapCombineResult(requester.CombineResult);
        }

        private static void DrawBaseMapCombineResult(BurtAvatarDecalCombineResult combineResult)
        {
            if (combineResult == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("BaseMap Combine", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Combine Status", combineResult.Status.ToString());
            if (!string.IsNullOrEmpty(combineResult.FailureReason))
            {
                EditorGUILayout.HelpBox(combineResult.FailureReason, MessageType.Error);
                return;
            }

            if (combineResult.Status == BurtAvatarDecalCombineStatus.PendingCompression)
            {
                EditorGUILayout.HelpBox("The BC7 compression request executes at the beginning of the next BurtRP frame; unsupported platforms fall back to BC3/DXT5.", MessageType.Info);
                return;
            }

            if ((combineResult.Status != BurtAvatarDecalCombineStatus.Combined && combineResult.Status != BurtAvatarDecalCombineStatus.Compressed) || combineResult.CombinedBaseMap == null)
            {
                EditorGUILayout.HelpBox("The combine request executes at the beginning of the next BurtRP frame.", MessageType.Info);
                return;
            }

            EditorGUILayout.ObjectField("Combined Base Map", combineResult.CombinedBaseMap, typeof(RenderTexture), false);
            var previewRect = GUILayoutUtility.GetRect(1.0f, 240.0f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawPreviewTexture(previewRect, combineResult.CombinedBaseMap, null, ScaleMode.ScaleToFit, 1.0f);
            if (combineResult.CompressedBaseMap != null)
            {
                EditorGUILayout.ObjectField("Compressed Base Map (" + combineResult.CompressionFormat + ")", combineResult.CompressedBaseMap, typeof(Texture2D), false);
                var compressedPreviewRect = GUILayoutUtility.GetRect(1.0f, 240.0f, GUILayout.ExpandWidth(true));
                EditorGUI.DrawPreviewTexture(compressedPreviewRect, combineResult.CompressedBaseMap, null, ScaleMode.ScaleToFit, 1.0f);
            }
            EditorGUILayout.ObjectField("Projection Debug Map", combineResult.ProjectionDebugMap, typeof(RenderTexture), false);
            var projectionDebugRect = GUILayoutUtility.GetRect(1.0f, 160.0f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawPreviewTexture(projectionDebugRect, combineResult.ProjectionDebugMap, null, ScaleMode.ScaleToFit, 1.0f);
            EditorGUILayout.HelpBox("Projection Debug: black = outside the decal box; red = inside the box but decal alpha is 0; yellow = inside the box with decal alpha above 0.", MessageType.Info);
            EditorGUILayout.HelpBox("Search RenderDoc Event Browser for: Burt Avatar Decal / Combine BaseMap", MessageType.Info);
        }

        private static void DrawPositionMapPreview(RenderTexture positionMap, float previewPositionRange)
        {
            if (positionMap == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Position Map Preview", EditorStyles.boldLabel);
            var previewRect = GUILayoutUtility.GetRect(1.0f, 240.0f, GUILayout.ExpandWidth(true));
            var previewMaterial = GetPositionMapPreviewMaterial();
            if (previewMaterial != null)
            {
                previewMaterial.SetFloat(PositionMapPreviewRangeId, Mathf.Max(0.01f, previewPositionRange));
            }
            EditorGUI.DrawPreviewTexture(
                previewRect,
                positionMap,
                previewMaterial,
                ScaleMode.ScaleToFit,
                1.0f);
            EditorGUILayout.HelpBox(
                "Black = invalid UV texel (PositionMap alpha is 0). " +
                "Valid texel color is saturate((PreSkinPositionOS / " + previewPositionRange.ToString("0.##") + " + 1) / 2): R=X, G=Y, B=Z.",
                MessageType.None);
        }

        private static Material GetPositionMapPreviewMaterial()
        {
            if (positionMapPreviewMaterial != null)
            {
                return positionMapPreviewMaterial;
            }

            var shader = Shader.Find(PositionMapPreviewShaderName);
            if (shader == null)
            {
                return null;
            }

            positionMapPreviewMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            return positionMapPreviewMaterial;
        }
    }
}
