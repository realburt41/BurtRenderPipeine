using System.Collections.Generic;
using UnityEngine;

namespace Burt.RenderPipeline
{
    /// <summary>
    /// Scene component used to request one avatar pre-skin PositionMap for RenderDoc and decal setup validation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BurtAvatarDecalPositionMapDebugRequester : MonoBehaviour
    {
        [SerializeField] private SkinnedMeshRenderer targetRenderer;
        [SerializeField] private Transform bodyTransform;
        [SerializeField, Range(BurtAvatarDecalPositionMapManager.MinimumPositionMapSize, BurtAvatarDecalPositionMapManager.MaximumPositionMapSize)]
        private int positionMapSize = 1024;
        [SerializeField] private BurtAvatarDecalPreSkinPositionMode preSkinPositionMode = BurtAvatarDecalPreSkinPositionMode.Auto;
        [SerializeField, Range(0.01f, 16.0f)] private float previewPositionRange = 2.0f;
        [Header("BaseMap Combine Test")]
        [SerializeField] private Texture sourceBaseMap;
        [SerializeField] private Texture decalBaseMap;
        [SerializeField] private Color decalBaseColor = Color.white;
        [SerializeField] private Transform decalTransform;
        [SerializeField, Min(0)] private int targetMaterialSlot;

        private BurtAvatarDecalPositionMapResult result;
        private BurtAvatarDecalCombineResult combineResult;
        private MaterialPropertyBlock originalBaseMapProperties;
        private MaterialPropertyBlock combinedBaseMapProperties;
        private SkinnedMeshRenderer appliedRenderer;
        private int appliedMaterialSlot = -1;

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        public SkinnedMeshRenderer TargetRenderer => targetRenderer;
        public Transform BodyTransform => bodyTransform != null ? bodyTransform : targetRenderer != null ? targetRenderer.transform : null;
        public int PositionMapSize => positionMapSize;
        public BurtAvatarDecalPreSkinPositionMode PreSkinPositionMode => preSkinPositionMode;
        public float PreviewPositionRange => Mathf.Max(0.01f, previewPositionRange);
        public BurtAvatarDecalPositionMapResult Result => result;
        public Texture SourceBaseMap => sourceBaseMap;
        public Texture DecalBaseMap => decalBaseMap;
        public Transform DecalTransform => decalTransform;
        public int TargetMaterialSlot => targetMaterialSlot;
        public BurtAvatarDecalCombineResult CombineResult => combineResult;

        [ContextMenu("Request Position Map")]
        public void RequestPositionMap()
        {
            var pipeline = BurtRenderPipeline.Current;
            if (pipeline == null)
            {
                Debug.LogWarning("[BurtRP][AvatarDecal] No active BurtRenderPipeline is available to execute the PositionMap request.", this);
                return;
            }

            ReleasePositionMap();
            var target = BodyTransform;
            if (target == null)
            {
                Debug.LogWarning("[BurtRP][AvatarDecal] Assign a target SkinnedMeshRenderer before requesting a PositionMap.", this);
                return;
            }

            result = pipeline.AvatarDecalPositionMapManager.RequestPositionMap(
                targetRenderer,
                target.localToWorldMatrix,
                positionMapSize,
                preSkinPositionMode);
        }

        [ContextMenu("Release Position Map")]
        public void ReleasePositionMap()
        {
            if (result == null)
            {
                return;
            }

            BurtRenderPipeline.Current?.AvatarDecalPositionMapManager.Release(result);
            result = null;
        }

        [ContextMenu("Combine Base Map")]
        public void CombineBaseMap()
        {
            var pipeline = BurtRenderPipeline.Current;
            if (pipeline == null)
            {
                Debug.LogWarning("[BurtRP][AvatarDecal] No active BurtRenderPipeline is available to combine the BaseMap.", this);
                return;
            }

            var body = BodyTransform;
            if (body == null || sourceBaseMap == null || decalBaseMap == null || decalTransform == null)
            {
                Debug.LogWarning("[BurtRP][AvatarDecal] Assign Target Renderer, Source Base Map, Decal Base Map, and Decal Transform before combining.", this);
                return;
            }

            ReleaseCombinedBaseMap();
            combineResult = pipeline.AvatarDecalManager.CombineBaseMaps(new BurtAvatarDecalCombineParams
            {
                TargetRenderer = targetRenderer,
                BodyMatrix = body.localToWorldMatrix,
                BaseMap = sourceBaseMap,
                PositionMapSize = positionMapSize,
                PreSkinPositionMode = preSkinPositionMode,
                Layers = new List<BurtAvatarDecalBaseLayer>
                {
                    new()
                    {
                        BaseMap = decalBaseMap,
                        BaseColor = decalBaseColor,
                        Position = decalTransform.position,
                        Rotation = decalTransform.rotation,
                        Scale = decalTransform.lossyScale,
                    },
                },
            });
        }

        [ContextMenu("Release Combined Base Map")]
        public void ReleaseCombinedBaseMap()
        {
            if (combineResult == null)
            {
                return;
            }

            RestoreOriginalBaseMap();
            BurtRenderPipeline.Current?.AvatarDecalManager.Release(combineResult);
            combineResult = null;
        }

        [ContextMenu("Compress Combined Base Map")]
        public void CompressCombinedBaseMap()
        {
            var pipeline = BurtRenderPipeline.Current;
            if (combineResult == null || pipeline == null || !pipeline.AvatarDecalManager.CompressBaseMap(combineResult))
            {
                Debug.LogWarning("[BurtRP][AvatarDecal] BaseMap compression requires a completed combine result with BC7 or DXT5 and compute-shader support.", this);
            }
        }

        [ContextMenu("Apply Combined Base Map")]
        public void ApplyCombinedBaseMap()
        {
            Texture baseMap = combineResult != null && combineResult.Status == BurtAvatarDecalCombineStatus.Compressed
                ? combineResult.CompressedBaseMap
                : combineResult?.CombinedBaseMap;
            if (combineResult == null ||
                (combineResult.Status != BurtAvatarDecalCombineStatus.Combined && combineResult.Status != BurtAvatarDecalCombineStatus.Compressed) ||
                baseMap == null)
            {
                Debug.LogWarning("[BurtRP][AvatarDecal] Combine the BaseMap and wait for its status to become Combined before applying it.", this);
                return;
            }

            var materials = targetRenderer != null ? targetRenderer.sharedMaterials : null;
            if (materials == null || targetMaterialSlot < 0 || targetMaterialSlot >= materials.Length ||
                materials[targetMaterialSlot] == null || !materials[targetMaterialSlot].HasProperty(BaseMapId))
            {
                Debug.LogWarning("[BurtRP][AvatarDecal] Target Material Slot must reference a material with a _BaseMap property.", this);
                return;
            }

            RestoreOriginalBaseMap();
            EnsurePropertyBlocks();
            targetRenderer.GetPropertyBlock(originalBaseMapProperties, targetMaterialSlot);
            targetRenderer.GetPropertyBlock(combinedBaseMapProperties, targetMaterialSlot);
            combinedBaseMapProperties.SetTexture(BaseMapId, baseMap);
            targetRenderer.SetPropertyBlock(combinedBaseMapProperties, targetMaterialSlot);
            appliedRenderer = targetRenderer;
            appliedMaterialSlot = targetMaterialSlot;
        }

        [ContextMenu("Restore Original Base Map")]
        public void RestoreOriginalBaseMap()
        {
            if (appliedRenderer == null || appliedMaterialSlot < 0 || originalBaseMapProperties == null)
            {
                return;
            }

            appliedRenderer.SetPropertyBlock(originalBaseMapProperties, appliedMaterialSlot);
            appliedRenderer = null;
            appliedMaterialSlot = -1;
            originalBaseMapProperties.Clear();
            combinedBaseMapProperties?.Clear();
        }

        private void EnsurePropertyBlocks()
        {
            originalBaseMapProperties ??= new MaterialPropertyBlock();
            combinedBaseMapProperties ??= new MaterialPropertyBlock();
        }

        private void OnDisable()
        {
            ReleasePositionMap();
            ReleaseCombinedBaseMap();
            RestoreOriginalBaseMap();
        }
    }
}
