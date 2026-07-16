using System;
using UnityEngine;

namespace Burt.RenderPipeline
{
    /// <summary>
    /// Edit-mode-only helper for validating one Subsurface skinned decal against pre-skin mesh space.
    /// It mirrors XRender's hit-bone -> bind-pose inverse conversion, while keeping the source material unchanged.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BurtSkinnedDecalDebugRequester : MonoBehaviour
    {
        private const int MaxDecalLayers = 5;

        [Header("Target")]
        [SerializeField] private SkinnedMeshRenderer targetRenderer;
        [SerializeField, Min(0)] private int targetMaterialSlot;
        [Tooltip("XRender-compatible conversion source. Assign the character/body SkinnedMeshRenderer that owns the hit bone; leave empty to use Target Renderer.")]
        [SerializeField] private SkinnedMeshRenderer coordinateRenderer;
        [Tooltip("The bone which received the hit. It must be one of Coordinate Renderer > Bones.")]
        [SerializeField] private Transform targetBone;

        [Header("World-space Decal Input")]
        [Tooltip("Position and rotation are converted through Target Bone and its bind pose into pre-skin mesh space.")]
        [SerializeField] private Transform decalTransform;
        [SerializeField, Range(0, MaxDecalLayers - 1)] private int debugLayerIndex;
        [SerializeField, Min(0.001f), Tooltip("Projection width/height in decimeters. The shader converts it to meters by multiplying by 0.1.")]
        private float decalSizeDecimeters = 1.0f;
        [SerializeField, Range(0, 1)] private float albedoMultiply;
        [SerializeField, Min(0)] private float normalScale = 1.0f;
        [SerializeField] private Color decalTint = Color.white;
        [Tooltip("Ignore the decal texture and alpha; paint the current pre-skin projection rectangle magenta.")]
        [SerializeField] private bool showProjectionMask;
        [Tooltip("Paint the whole target magenta from inside the Skinned Decal shader function. It ignores all projection data and validates that the shader branch is reached.")]
        [SerializeField] private bool showShaderEntryMarker;
        [Tooltip("Ignore the decal projection and paint the entire target renderer magenta. Use this first to verify Target Renderer is visible in the current scene.")]
        [SerializeField] private bool showTargetRendererOverride;
        [Tooltip("Use the mesh POSITION attribute instead of UV3 pre-skin data. Diagnostic only: if this makes the projection appear, the mesh has no usable UV3 pre-skin position.")]
        [SerializeField] private bool useMeshPositionFallback;
        [Tooltip("Bypass hit-bone bindpose conversion and transform the decal directly into Target Renderer local space. Use this to diagnose renderer/root transform mismatches.")]
        [SerializeField] private bool useRendererLocalCoordinates;

        [Header("Textures (optional when already assigned on the material)")]
        [SerializeField] private Texture2D decalAlbedo;
        [SerializeField] private Texture2D decalNormal;
        [SerializeField] private Texture2D decalMohr;

        private MaterialPropertyBlock originalProperties;
        private MaterialPropertyBlock debugProperties;
        private SkinnedMeshRenderer appliedRenderer;
        private Material appliedOriginalMaterial;
        private Material appliedDebugMaterial;
        private int appliedMaterialSlot = -1;
        private Vector3 lastPreSkinPosition;
        private Vector3 lastPreSkinBasisX;
        private Vector3 lastPreSkinBasisY;
        private string lastFailureReason;

        private static readonly int SkinnedDecalEnabledId = Shader.PropertyToID("_SkinnedDecalEnabled");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ProjectionDebugId = Shader.PropertyToID("_BurtSkinnedDecalProjectionDebug");
        private static readonly int EntryDebugId = Shader.PropertyToID("_BurtSkinnedDecalEntryDebug");
        private static readonly int UseMeshPositionId = Shader.PropertyToID("_BurtSkinnedDecalUseMeshPosition");
        private static readonly int DecalCountId = Shader.PropertyToID("_SkinnedDecalPluginModel_DecalCount");
        private static readonly int DecalAlbedoId = Shader.PropertyToID("_SkinnedDecalPluginModel_DecalAlbedo");
        private static readonly int DecalNormalId = Shader.PropertyToID("_SkinnedDecalPluginModel_DecalNormal");
        private static readonly int DecalMohrId = Shader.PropertyToID("_SkinnedDecalPluginModel_DecalMOHR");
        private static readonly int[] DecalArrayIndexSizeIds =
        {
            Shader.PropertyToID("_SkinnedDecalPluginModel_DecalArrayIndexSize1"),
            Shader.PropertyToID("_SkinnedDecalPluginModel_DecalArraySizeIndex2"),
            Shader.PropertyToID("_SkinnedDecalPluginModel_DecalArraySizeIndex3"),
            Shader.PropertyToID("_SkinnedDecalPluginModel_DecalArraySizeIndex4"),
            Shader.PropertyToID("_SkinnedDecalPluginModel_DecalArraySizeIndex5"),
        };
        private static readonly int[] DecalTintIds = CreateLayerPropertyIds("_SkinnedDecalPluginModel_DecalTint");
        private static readonly int[] DecalPositionIds = CreateLayerPropertyIds("_SkinnedDecalPluginModel_DecalPosition");
        private static readonly int[] DecalBasisXIds = CreateLayerPropertyIds("_SkinnedDecalPluginModel_DecalBasisX");
        private static readonly int[] DecalBasisYIds = CreateLayerPropertyIds("_SkinnedDecalPluginModel_DecalBasisY");

        public SkinnedMeshRenderer TargetRenderer => targetRenderer;
        public SkinnedMeshRenderer CoordinateRenderer => coordinateRenderer != null ? coordinateRenderer : targetRenderer;
        public Transform TargetBone => targetBone;
        public Transform DecalTransform => decalTransform;
        public Vector3 LastPreSkinPosition => lastPreSkinPosition;
        public Vector3 LastPreSkinBasisX => lastPreSkinBasisX;
        public Vector3 LastPreSkinBasisY => lastPreSkinBasisY;
        public string LastFailureReason => lastFailureReason;
        public bool IsApplied => appliedRenderer != null;

        [ContextMenu("Apply Skinned Decal Debug")]
        public void ApplyDebugDecal()
        {
            lastFailureReason = null;
            if (Application.isPlaying)
            {
                Fail("This component is an edit-mode debug tool. Use it outside Play Mode.");
                return;
            }

            if (!TryCalculatePreSkinDecal(out var preSkinPosition, out var preSkinBasisX, out var preSkinBasisY, out var failureReason))
            {
                Fail(failureReason);
                return;
            }

            var isUpdatingCurrentTarget = appliedRenderer == targetRenderer &&
                                          appliedMaterialSlot == targetMaterialSlot &&
                                          appliedOriginalMaterial != null;
            Material sourceMaterial;
            if (isUpdatingCurrentTarget)
            {
                sourceMaterial = appliedOriginalMaterial;
            }
            else if (!TryGetTargetMaterial(out sourceMaterial, out failureReason))
            {
                Fail(failureReason);
                return;
            }

            if (appliedRenderer != null && (appliedRenderer != targetRenderer || appliedMaterialSlot != targetMaterialSlot))
            {
                RestoreOriginalMaterial();
            }

            EnsureDebugMaterial(sourceMaterial);
            ConfigureDebugMaterial(sourceMaterial);
            EnsurePropertyBlocks();
            if (appliedRenderer == null)
            {
                targetRenderer.GetPropertyBlock(originalProperties, targetMaterialSlot);
            }

            ApplyDebugMaterial();
            targetRenderer.GetPropertyBlock(debugProperties, targetMaterialSlot);
            ApplyDebugProperties(debugProperties, preSkinPosition, preSkinBasisX, preSkinBasisY);
            targetRenderer.SetPropertyBlock(debugProperties, targetMaterialSlot);
            ApplyDebugMaterialProperties(preSkinPosition, preSkinBasisX, preSkinBasisY);

            lastPreSkinPosition = preSkinPosition;
            lastPreSkinBasisX = preSkinBasisX;
            lastPreSkinBasisY = preSkinBasisY;
        }

        [ContextMenu("Restore Original Skinned Decal Material")]
        public void RestoreOriginalMaterial()
        {
            if (appliedRenderer == null)
            {
                return;
            }

            if (originalProperties != null && appliedMaterialSlot >= 0)
            {
                appliedRenderer.SetPropertyBlock(originalProperties, appliedMaterialSlot);
            }

            var materials = appliedRenderer.sharedMaterials;
            if (materials != null && appliedMaterialSlot >= 0 && appliedMaterialSlot < materials.Length)
            {
                materials[appliedMaterialSlot] = appliedOriginalMaterial;
                appliedRenderer.sharedMaterials = materials;
            }

            DestroyDebugMaterial();
            appliedRenderer = null;
            appliedOriginalMaterial = null;
            appliedMaterialSlot = -1;
            originalProperties?.Clear();
            debugProperties?.Clear();
        }

        public bool TryCalculatePreSkinDecal(
            out Vector3 preSkinPosition,
            out Vector3 preSkinBasisX,
            out Vector3 preSkinBasisY,
            out string failureReason)
        {
            preSkinPosition = default;
            preSkinBasisX = default;
            preSkinBasisY = default;
            failureReason = null;

            if (targetRenderer == null || targetRenderer.sharedMesh == null)
            {
                failureReason = "Assign a Target Renderer with a shared mesh.";
                return false;
            }

            var coordinateSource = CoordinateRenderer;
            if (coordinateSource == null || coordinateSource.sharedMesh == null)
            {
                failureReason = "Assign a Coordinate Renderer with a shared mesh, or leave it empty to use Target Renderer.";
                return false;
            }

            if (decalTransform == null)
            {
                failureReason = "Assign a Decal Transform.";
                return false;
            }

            if (useRendererLocalCoordinates)
            {
                var worldToRenderer = coordinateSource.transform.worldToLocalMatrix;
                preSkinPosition = worldToRenderer.MultiplyPoint(decalTransform.position);
                preSkinBasisX = worldToRenderer.MultiplyVector(decalTransform.right).normalized;
                preSkinBasisY = worldToRenderer.MultiplyVector(decalTransform.up).normalized;
                return true;
            }

            if (targetBone == null)
            {
                failureReason = "Assign a Target Bone, or enable Use Renderer Local Coordinates.";
                return false;
            }

            var bones = coordinateSource.bones;
            var boneIndex = Array.IndexOf(bones, targetBone);
            if (boneIndex < 0)
            {
                failureReason = "Target Bone must be present in Coordinate Renderer > Bones.";
                return false;
            }

            var bindPoses = coordinateSource.sharedMesh.bindposes;
            if (boneIndex >= bindPoses.Length)
            {
                failureReason = "The target mesh has no bind pose for the selected Target Bone.";
                return false;
            }

            // This is the same coordinate chain as XRender ShaderView.SpawnDecal:
            // world -> hit-bone local -> bind-pose inverse (pre-skin mesh local).
            var boneLocalPosition = targetBone.worldToLocalMatrix.MultiplyPoint(decalTransform.position);
            var boneLocalRotation = targetBone.worldToLocalMatrix.rotation * decalTransform.rotation;
            var boneToMesh = bindPoses[boneIndex].inverse;
            var decalRotation = boneToMesh.rotation * boneLocalRotation;

            preSkinPosition = boneToMesh.MultiplyPoint(boneLocalPosition);
            preSkinBasisX = decalRotation * Vector3.right;
            preSkinBasisY = decalRotation * Vector3.up;
            return true;
        }

        private bool TryGetTargetMaterial(out Material sourceMaterial, out string failureReason)
        {
            sourceMaterial = null;
            failureReason = null;
            var materials = targetRenderer != null ? targetRenderer.sharedMaterials : null;
            if (materials == null || targetMaterialSlot < 0 || targetMaterialSlot >= materials.Length)
            {
                failureReason = "Target Material Slot is outside Target Renderer > Materials.";
                return false;
            }

            sourceMaterial = materials[targetMaterialSlot];
            if (sourceMaterial == null || !sourceMaterial.HasProperty(SkinnedDecalEnabledId) || !sourceMaterial.HasProperty(DecalCountId))
            {
                failureReason = "Target Material Slot must use BurtRP/Subsurface with Skinned Decal properties.";
                return false;
            }

            return true;
        }

        private void ApplyDebugProperties(MaterialPropertyBlock properties, Vector3 preSkinPosition, Vector3 preSkinBasisX, Vector3 preSkinBasisY)
        {
            properties.SetFloat(ProjectionDebugId, showProjectionMask ? 1.0f : 0.0f);
            properties.SetFloat(EntryDebugId, showShaderEntryMarker ? 1.0f : 0.0f);
            properties.SetFloat(UseMeshPositionId, useMeshPositionFallback ? 1.0f : 0.0f);
            properties.SetFloat(DecalCountId, debugLayerIndex + 1);
            if (decalAlbedo != null)
            {
                properties.SetTexture(DecalAlbedoId, decalAlbedo);
            }

            if (decalNormal != null)
            {
                properties.SetTexture(DecalNormalId, decalNormal);
            }

            if (decalMohr != null)
            {
                properties.SetTexture(DecalMohrId, decalMohr);
            }

            for (var layer = 0; layer < MaxDecalLayers; ++layer)
            {
                var enabledLayer = layer == debugLayerIndex;
                properties.SetVector(DecalArrayIndexSizeIds[layer], enabledLayer
                    ? new Vector4(0.0f, decalSizeDecimeters, albedoMultiply, normalScale)
                    : Vector4.zero);
                properties.SetColor(DecalTintIds[layer], enabledLayer ? decalTint : Color.clear);
                properties.SetVector(DecalPositionIds[layer], enabledLayer ? preSkinPosition : Vector3.zero);
                properties.SetVector(DecalBasisXIds[layer], enabledLayer ? preSkinBasisX : Vector3.zero);
                properties.SetVector(DecalBasisYIds[layer], enabledLayer ? preSkinBasisY : Vector3.zero);
            }
        }

        private void ApplyDebugMaterialProperties(Vector3 preSkinPosition, Vector3 preSkinBasisX, Vector3 preSkinBasisY)
        {
            // The debug material is unique to this component. Mirroring the MPB values here makes
            // Inspector/RenderDoc validation unambiguous without changing the shared source material.
            appliedDebugMaterial.SetFloat(ProjectionDebugId, showProjectionMask ? 1.0f : 0.0f);
            appliedDebugMaterial.SetFloat(EntryDebugId, showShaderEntryMarker ? 1.0f : 0.0f);
            appliedDebugMaterial.SetFloat(UseMeshPositionId, useMeshPositionFallback ? 1.0f : 0.0f);
            appliedDebugMaterial.SetFloat(DecalCountId, debugLayerIndex + 1);
            if (decalAlbedo != null)
            {
                appliedDebugMaterial.SetTexture(DecalAlbedoId, decalAlbedo);
            }

            if (decalNormal != null)
            {
                appliedDebugMaterial.SetTexture(DecalNormalId, decalNormal);
            }

            if (decalMohr != null)
            {
                appliedDebugMaterial.SetTexture(DecalMohrId, decalMohr);
            }

            for (var layer = 0; layer < MaxDecalLayers; ++layer)
            {
                var enabledLayer = layer == debugLayerIndex;
                appliedDebugMaterial.SetVector(DecalArrayIndexSizeIds[layer], enabledLayer
                    ? new Vector4(0.0f, decalSizeDecimeters, albedoMultiply, normalScale)
                    : Vector4.zero);
                appliedDebugMaterial.SetColor(DecalTintIds[layer], enabledLayer ? decalTint : Color.clear);
                appliedDebugMaterial.SetVector(DecalPositionIds[layer], enabledLayer ? preSkinPosition : Vector3.zero);
                appliedDebugMaterial.SetVector(DecalBasisXIds[layer], enabledLayer ? preSkinBasisX : Vector3.zero);
                appliedDebugMaterial.SetVector(DecalBasisYIds[layer], enabledLayer ? preSkinBasisY : Vector3.zero);
            }
        }

        private void EnsureDebugMaterial(Material sourceMaterial)
        {
            if (appliedDebugMaterial != null && appliedOriginalMaterial == sourceMaterial)
            {
                return;
            }

            DestroyDebugMaterial();
            appliedOriginalMaterial = sourceMaterial;
            appliedDebugMaterial = new Material(sourceMaterial)
            {
                name = sourceMaterial.name + " (Burt Skinned Decal Debug)",
                hideFlags = HideFlags.HideAndDontSave,
            };
            appliedDebugMaterial.SetFloat(SkinnedDecalEnabledId, 1.0f);
            appliedDebugMaterial.EnableKeyword("BURT_SKINNED_DECAL");
        }

        private void ConfigureDebugMaterial(Material sourceMaterial)
        {
            if (showTargetRendererOverride)
            {
                appliedDebugMaterial.SetTexture(BaseMapId, Texture2D.whiteTexture);
                appliedDebugMaterial.SetColor(BaseColorId, Color.magenta);
                return;
            }

            if (sourceMaterial.HasProperty(BaseMapId))
            {
                appliedDebugMaterial.SetTexture(BaseMapId, sourceMaterial.GetTexture(BaseMapId));
            }

            if (sourceMaterial.HasProperty(BaseColorId))
            {
                appliedDebugMaterial.SetColor(BaseColorId, sourceMaterial.GetColor(BaseColorId));
            }
        }

        private void ApplyDebugMaterial()
        {
            var materials = targetRenderer.sharedMaterials;
            materials[targetMaterialSlot] = appliedDebugMaterial;
            targetRenderer.sharedMaterials = materials;
            appliedRenderer = targetRenderer;
            appliedMaterialSlot = targetMaterialSlot;
        }

        private void EnsurePropertyBlocks()
        {
            originalProperties ??= new MaterialPropertyBlock();
            debugProperties ??= new MaterialPropertyBlock();
        }

        private void DestroyDebugMaterial()
        {
            if (appliedDebugMaterial == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(appliedDebugMaterial);
            }
            else
            {
                DestroyImmediate(appliedDebugMaterial);
            }

            appliedDebugMaterial = null;
        }

        private void Fail(string failureReason)
        {
            lastFailureReason = failureReason;
            Debug.LogWarning("[BurtRP][SkinnedDecal] " + failureReason, this);
        }

        private static int[] CreateLayerPropertyIds(string propertyPrefix)
        {
            var ids = new int[MaxDecalLayers];
            for (var layer = 0; layer < MaxDecalLayers; ++layer)
            {
                ids[layer] = Shader.PropertyToID(propertyPrefix + (layer + 1));
            }

            return ids;
        }

        private void OnDisable()
        {
            RestoreOriginalMaterial();
        }
    }
}
