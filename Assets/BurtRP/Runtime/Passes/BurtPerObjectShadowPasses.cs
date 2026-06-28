using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    internal static class BurtPerObjectShadowUtility
    {
        public const int MaxSlices = 8;
        public const int DefaultSliceResolution = 1024;

        private const string CastingPunctualLightShadowKeyword = "_CASTING_PUNCTUAL_LIGHT_SHADOW";
        private const string ShadowCasterPassName = "ShadowCaster";

        private static readonly int PerObjectShadowRows0Id = Shader.PropertyToID("_BurtPerObjectShadowRows0");
        private static readonly int PerObjectShadowRows1Id = Shader.PropertyToID("_BurtPerObjectShadowRows1");
        private static readonly int PerObjectShadowRows2Id = Shader.PropertyToID("_BurtPerObjectShadowRows2");
        private static readonly int PerObjectShadowRows3Id = Shader.PropertyToID("_BurtPerObjectShadowRows3");
        private static readonly int PerObjectShadowAtlasRectsId = Shader.PropertyToID("_BurtPerObjectShadowAtlasRects");
        private static readonly int PerObjectShadowSliceParamsId = Shader.PropertyToID("_BurtPerObjectShadowSliceParams");
        private static readonly int PerObjectShadowSliceDepthParamsId = Shader.PropertyToID("_BurtPerObjectShadowSliceDepthParams");
        private static readonly int PerObjectShadowParamsId = Shader.PropertyToID("_BurtPerObjectShadowParams");
        private static readonly int PerObjectShadowTexelSizeId = Shader.PropertyToID("_BurtPerObjectShadowTexelSize");
        internal static readonly int PerObjectShadowObjectIndexId = Shader.PropertyToID("_BurtPerObjectShadowObjectIndex");
        private static readonly int MainLightDirectionId = Shader.PropertyToID("_BurtMainLightDirection");
        private static readonly int ShadowCasterLightPositionId = Shader.PropertyToID("_BurtShadowCasterLightPosition");
        private static readonly int CastingPunctualLightShadowId = Shader.PropertyToID("_BurtCastingPunctualLightShadow");
        private static readonly int MainLightShadowDepthBiasId = Shader.PropertyToID("_BurtMainLightShadowDepthBias");
        private static readonly int MainLightShadowNormalBiasId = Shader.PropertyToID("_BurtMainLightShadowNormalBias");
        private static readonly int UnityLightDirectionId = Shader.PropertyToID("_LightDirection");
        private static readonly int UnityLightPositionId = Shader.PropertyToID("_LightPosition");
        private static readonly int UnityShadowBiasId = Shader.PropertyToID("_ShadowBias");
        private static readonly int UnityWorldToCameraId = Shader.PropertyToID("unity_WorldToCamera");
        private static readonly int UnityCameraToWorldId = Shader.PropertyToID("unity_CameraToWorld");

        private static readonly Matrix4x4[] DisabledWorldToShadowMatrices = CreateIdentityMatrixArray();
        private static readonly Vector4[] DisabledAtlasRects = CreateDefaultAtlasRectArray();
        private static readonly Vector4[] DisabledSliceParams = new Vector4[MaxSlices];
        private static readonly Vector4[] DisabledSliceDepthParams = new Vector4[MaxSlices];
        private static readonly Vector4[] WorldToShadowRows0 = new Vector4[MaxSlices];
        private static readonly Vector4[] WorldToShadowRows1 = new Vector4[MaxSlices];
        private static readonly Vector4[] WorldToShadowRows2 = new Vector4[MaxSlices];
        private static readonly Vector4[] WorldToShadowRows3 = new Vector4[MaxSlices];
        private static readonly Vector3[] BoundsCorners = new Vector3[8];
        private static readonly ShaderTagId LightModeTag = new ShaderTagId("LightMode");
        private static readonly ShaderTagId ShadowCasterLightModeTag = new ShaderTagId(ShadowCasterPassName);

        public static System.IDisposable BeginDirectionalLightRenderingLayerMaskOverrideForCulling()
        {
            if (BurtPerObjectShadowRegistry.ActiveCount <= 0)
            {
                return null;
            }

            var lights = Object.FindObjectsOfType<Light>();
            DirectionalLightRenderingLayerMaskScope scope = null;
            for (var lightIndex = 0; lightIndex < lights.Length; lightIndex++)
            {
                var light = lights[lightIndex];
                if (light == null || !light.isActiveAndEnabled || light.type != LightType.Directional)
                {
                    continue;
                }

                var originalMask = light.renderingLayerMask;
                var maskedLightMask = originalMask & ~(int)BurtPerObjectShadow.PerObjectShadowRenderingLayerMask;
                if (maskedLightMask == originalMask)
                {
                    continue;
                }

                if (scope == null)
                {
                    scope = new DirectionalLightRenderingLayerMaskScope();
                }

                scope.Add(light, originalMask);
                light.renderingLayerMask = maskedLightMask;
            }

            return scope;
        }

        public static bool ShouldUsePerObjectShadow(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return request != null &&
                request.IsValid &&
                request.Camera != null &&
                BurtPerObjectShadowRegistry.ActiveCount > 0;
        }

        public static PerObjectShadowPrepareData Prepare(BurtRenderRequest request)
        {
            var preparedData = new PerObjectShadowPrepareData();
            if (request == null || request.Camera == null)
            {
                return preparedData;
            }

            BurtPerObjectShadowRegistry.CollectActive(preparedData.Components, int.MaxValue, request.Camera);
            if (preparedData.Components.Count <= 0)
            {
                return preparedData;
            }

            var mainLightDirection = ResolveMainLightDirection(request);
            var lightRotation = ResolveLightRotation(mainLightDirection);
            var validSliceCount = 0;
            var atlasHeight = 0;
            for (var componentIndex = 0; componentIndex < preparedData.Components.Count && validSliceCount < MaxSlices; componentIndex++)
            {
                var component = preparedData.Components[componentIndex];
                if (component == null || !component.IsRenderable)
                {
                    continue;
                }

                var slice = preparedData.Slices[validSliceCount];
                if (!TryPrepareSlice(component, lightRotation, validSliceCount, request.Camera, slice))
                {
                    continue;
                }

                preparedData.Slices[validSliceCount] = slice;
                atlasHeight = Mathf.Max(atlasHeight, slice.SliceResolution);
                validSliceCount++;
            }

            if (validSliceCount <= 0)
            {
                return preparedData;
            }

            preparedData.SliceCount = validSliceCount;
            preparedData.AtlasHeight = Mathf.Max(1, atlasHeight);
            preparedData.AtlasWidth = Mathf.Max(1, preparedData.AtlasHeight * validSliceCount);

            for (var sliceIndex = 0; sliceIndex < preparedData.SliceCount; sliceIndex++)
            {
                var slice = preparedData.Slices[sliceIndex];
                var sliceSize = Mathf.Max(1, Mathf.Min(slice.SliceResolution, preparedData.AtlasHeight));
                slice.Viewport = new Rect(sliceIndex * preparedData.AtlasHeight, 0f, sliceSize, sliceSize);
                slice.AtlasRect = new Vector4(
                    slice.Viewport.x / preparedData.AtlasWidth,
                    0f,
                    (slice.Viewport.x + slice.Viewport.width) / preparedData.AtlasWidth,
                    slice.Viewport.height / preparedData.AtlasHeight);
                slice.WorldToShadowMatrix = CreateWorldToPerObjectShadowMatrix(slice.ViewMatrix, slice.ProjectionMatrix, slice.AtlasRect);
                preparedData.Slices[sliceIndex] = slice;
            }

            return preparedData;
        }

        public static RenderTextureDescriptor CreateAtlasDescriptor(PerObjectShadowPrepareData preparedData)
        {
            return BurtRenderTargetDescriptorUtility.CreatePerObjectShadowAtlasDescriptor(
                preparedData != null && preparedData.AtlasWidth > 0 ? preparedData.AtlasWidth : DefaultSliceResolution,
                preparedData != null && preparedData.AtlasHeight > 0 ? preparedData.AtlasHeight : DefaultSliceResolution);
        }

        public static void BindPerObjectShadowAtlasIfValid(CommandBuffer cmd, BurtRenderTargetHandle atlasTarget)
        {
            if (cmd == null || !atlasTarget.IsValid)
            {
                return;
            }

            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.PerObjectShadowAtlasId, atlasTarget.Identifier);
        }

        public static void BindPerObjectShadowAtlasIfValid(CommandBuffer cmd, Material material, BurtRenderTargetHandle atlasTarget)
        {
            BindPerObjectShadowAtlasIfValid(cmd, atlasTarget);
        }

        public static void UploadPerObjectShadowReceiverGlobals(
            CommandBuffer cmd,
            Material material,
            BurtRenderTargetHandle atlasTarget,
            PerObjectShadowPrepareData preparedData)
        {
            ClearObjectIndexesForPreparedComponents(preparedData);

            if (preparedData == null || preparedData.SliceCount <= 0 || !atlasTarget.IsValid)
            {
                ClearPerObjectShadowReceiverGlobals(cmd, material);
                return;
            }

            BindPerObjectShadowAtlasIfValid(cmd, material, atlasTarget);

            var matrices = DisabledWorldToShadowMatrices;
            var atlasRects = DisabledAtlasRects;
            var sliceParams = DisabledSliceParams;
            var sliceDepthParams = DisabledSliceDepthParams;
            for (var sliceIndex = 0; sliceIndex < MaxSlices; sliceIndex++)
            {
                matrices[sliceIndex] = Matrix4x4.identity;
                atlasRects[sliceIndex] = new Vector4(0f, 0f, 1f, 1f);
                sliceParams[sliceIndex] = Vector4.zero;
                sliceDepthParams[sliceIndex] = Vector4.zero;
            }

            for (var sliceIndex = 0; sliceIndex < Mathf.Min(preparedData.SliceCount, MaxSlices); sliceIndex++)
            {
                var slice = preparedData.Slices[sliceIndex];
                if (slice.Component != null)
                {
                    slice.Component.SetObjectIndexForRenderers(sliceIndex + 1);
                }

                matrices[sliceIndex] = slice.WorldToShadowMatrix;
                atlasRects[sliceIndex] = slice.AtlasRect;
                sliceParams[sliceIndex] = new Vector4(
                    Mathf.Clamp01(slice.Strength),
                    Mathf.Max(0f, slice.ReceiverDepthBias),
                    Mathf.Max(0f, slice.ReceiverNormalBias),
                    Mathf.Max(0f, slice.WorldTexelSize));
                sliceDepthParams[sliceIndex] = new Vector4(
                    Mathf.Max(0.001f, slice.DepthRangeWorld),
                    Mathf.Max(0f, slice.WorldTexelSize),
                    0f,
                    1f);
            }

            UploadPerObjectShadowArrays(
                cmd,
                material,
                matrices,
                atlasRects,
                sliceParams,
                sliceDepthParams,
                new Vector4(Mathf.Clamp(preparedData.SliceCount, 0, MaxSlices), preparedData.AtlasWidth, preparedData.AtlasHeight, 0f),
                new Vector4(1f / Mathf.Max(1, preparedData.AtlasWidth), 1f / Mathf.Max(1, preparedData.AtlasHeight), Mathf.Max(1, preparedData.AtlasWidth), Mathf.Max(1, preparedData.AtlasHeight)));
        }

        private static void ClearObjectIndexesForPreparedComponents(PerObjectShadowPrepareData preparedData)
        {
            if (preparedData == null || preparedData.Components == null)
            {
                return;
            }

            for (var componentIndex = 0; componentIndex < preparedData.Components.Count; componentIndex++)
            {
                preparedData.Components[componentIndex]?.ClearObjectIndexForRenderers();
            }
        }

        public static void ClearPerObjectShadowReceiverGlobals(CommandBuffer cmd)
        {
            ClearPerObjectShadowReceiverGlobals(cmd, null);
        }

        public static void ClearPerObjectShadowReceiverGlobals(CommandBuffer cmd, Material material)
        {
            UploadPerObjectShadowArrays(
                cmd,
                material,
                DisabledWorldToShadowMatrices,
                DisabledAtlasRects,
                DisabledSliceParams,
                DisabledSliceDepthParams,
                Vector4.zero,
                Vector4.zero);
        }

        public static void SetupDirectionalShadowCasterState(
            CommandBuffer cmd,
            Vector3 mainLightDirection,
            Matrix4x4 viewMatrix,
            float depthBias,
            float normalBias)
        {
            if (cmd == null)
            {
                return;
            }

            cmd.SetGlobalVector(MainLightDirectionId, new Vector4(mainLightDirection.x, mainLightDirection.y, mainLightDirection.z, 0f));
            cmd.SetGlobalVector(UnityLightDirectionId, new Vector4(mainLightDirection.x, mainLightDirection.y, mainLightDirection.z, 0f));
            cmd.SetGlobalVector(UnityLightPositionId, Vector4.zero);
            cmd.SetGlobalVector(ShadowCasterLightPositionId, Vector4.zero);
            cmd.SetGlobalFloat(CastingPunctualLightShadowId, 0f);
            cmd.SetGlobalFloat(MainLightShadowDepthBiasId, depthBias);
            cmd.SetGlobalFloat(MainLightShadowNormalBiasId, normalBias);
            cmd.SetGlobalVector(UnityShadowBiasId, new Vector4(depthBias, normalBias, 0f, 0f));
            cmd.SetGlobalDepthBias(0f, 0f);
            SetWorldToCameraAndCameraToWorldMatrices(cmd, viewMatrix);
            SetKeyword(cmd, CastingPunctualLightShadowKeyword, false);
        }

        public static void ResetShadowCasterState(BurtRenderGraphContext context, ScriptableRenderContext renderContext, Camera camera)
        {
            var cmd = CommandBufferPool.Get("Burt Reset Per Object Shadow Caster State");
            cmd.SetGlobalDepthBias(0f, 0f);
            cmd.SetGlobalFloat(CastingPunctualLightShadowId, 0f);
            cmd.SetGlobalFloat(MainLightShadowDepthBiasId, 0f);
            cmd.SetGlobalFloat(MainLightShadowNormalBiasId, 0f);
            cmd.SetGlobalVector(UnityLightDirectionId, Vector4.zero);
            cmd.SetGlobalVector(UnityLightPositionId, Vector4.zero);
            cmd.SetGlobalVector(ShadowCasterLightPositionId, Vector4.zero);
            cmd.SetGlobalVector(UnityShadowBiasId, Vector4.zero);
            SetKeyword(cmd, CastingPunctualLightShadowKeyword, false);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            BurtDrawingSettingsUtility.RestoreCameraMatricesForMainDraw(context, cmd);
            renderContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            if (camera != null && !BurtDrawingSettingsUtility.IsTemporalAAEnabled(context))
            {
                renderContext.SetupCameraProperties(camera);
            }
        }

        public static Vector3 ResolveMainLightDirection(BurtRenderRequest request)
        {
            var lightingData = request != null ? request.LightingData : null;
            var direction = lightingData != null ? lightingData.MainLightDirection : Vector3.forward;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return Vector3.forward;
            }

            return direction.normalized;
        }

        private static bool TryPrepareSlice(
            BurtPerObjectShadow component,
            Quaternion lightRotation,
            int sliceIndex,
            Camera camera,
            PerObjectShadowSlice slice)
        {
            slice.Reset();
            component.CollectRenderers(slice.Renderers);
            component.CollectMultipassRenderers(slice.MultipassRenderers);
            if (!TryCalculateRenderableBounds(slice.Renderers, camera, out var boundsWS))
            {
                return false;
            }

            var boundsViewMatrix = CreateWorldToShadowViewMatrix(boundsWS.center, lightRotation);
            var shadowViewToWorldForBounds = boundsViewMatrix.inverse;
            var boundsSVS = EncapsulateBoundsInSpace(boundsWS, boundsViewMatrix);
            if (boundsSVS.size.x <= 0.0001f || boundsSVS.size.y <= 0.0001f || boundsSVS.size.z <= 0.0001f)
            {
                return false;
            }

            var paddedExtents = boundsSVS.extents + new Vector3(component.Padding, component.Padding, 0f);
            var shadowCasterDepth = Mathf.Max(0.001f, boundsSVS.extents.z * 2f);
            var receiverDepth = shadowCasterDepth + component.ReceiverDistance;
            var projectionExtents = new Vector3(paddedExtents.x, paddedExtents.y, receiverDepth * 0.5f);
            var nearPlaneCenterWS = shadowViewToWorldForBounds.MultiplyPoint3x4(boundsSVS.center + Vector3.back * boundsSVS.extents.z);
            slice.ViewMatrix = CreateWorldToShadowViewMatrix(nearPlaneCenterWS, lightRotation);
            slice.ProjectionMatrix = CreateShadowViewToClipMatrix(projectionExtents);
            slice.SliceResolution = component.SliceResolution;
            slice.Strength = component.Strength;
            slice.ReceiverDepthBias = component.ReceiverDepthBias;
            slice.ReceiverDistance = component.ReceiverDistance;
            slice.ReceiverNormalBias = component.NormalBias * CalculateWorldTexelSize(projectionExtents, component.SliceResolution);
            slice.WorldTexelSize = CalculateWorldTexelSize(projectionExtents, component.SliceResolution);
            slice.DepthRangeWorld = receiverDepth;
            slice.Index = sliceIndex;
            slice.Component = component;
            return true;
        }

        private static bool TryCalculateRenderableBounds(List<Renderer> renderers, Camera camera, out Bounds bounds)
        {
            bounds = default;
            var hasBounds = false;
            if (renderers == null)
            {
                return false;
            }

            for (var rendererIndex = 0; rendererIndex < renderers.Count; rendererIndex++)
            {
                var renderer = renderers[rendererIndex];
                if (!IsRenderableShadowCaster(renderer, camera))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            return hasBounds && bounds.size.sqrMagnitude > 0.000001f;
        }

        public static bool IsRenderableShadowCaster(Renderer renderer, Camera camera)
        {
            if (renderer == null ||
                !renderer.enabled ||
                renderer.shadowCastingMode == ShadowCastingMode.Off ||
                renderer.gameObject == null ||
                !renderer.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (camera != null && (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0)
            {
                return false;
            }

            return true;
        }

        public static int FindShadowCasterPass(Material material)
        {
            if (material == null)
            {
                return -1;
            }

            var shader = material.shader;
            if (shader == null)
            {
                return -1;
            }

            for (var passIndex = 0; passIndex < shader.passCount; passIndex++)
            {
                var lightMode = shader.FindPassTagValue(passIndex, LightModeTag);
                if (lightMode.Equals(ShadowCasterLightModeTag))
                {
                    return passIndex;
                }
            }

            return material.FindPass(ShadowCasterPassName);
        }

        private static Matrix4x4 CreateWorldToShadowViewMatrix(Vector3 centerWS, Quaternion lightRotation)
        {
            return Matrix4x4.Rotate(Quaternion.Inverse(lightRotation)) * Matrix4x4.Translate(-centerWS);
        }

        private static Matrix4x4 CreateShadowViewToClipMatrix(Vector3 extents)
        {
            var safeExtents = new Vector3(Mathf.Max(0.001f, extents.x), Mathf.Max(0.001f, extents.y), Mathf.Max(0.001f, extents.z));
            var ortho = Matrix4x4.Ortho(-safeExtents.x, safeExtents.x, -safeExtents.y, safeExtents.y, 0f, safeExtents.z * 2f);
            ortho.m02 *= -1f;
            ortho.m12 *= -1f;
            ortho.m22 *= -1f;
            ortho.m32 *= -1f;
            return ortho;
        }

        private static Matrix4x4 CreateWorldToPerObjectShadowMatrix(Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix, Vector4 atlasRect)
        {
            var worldToShadow = GL.GetGPUProjectionMatrix(projectionMatrix, false) * viewMatrix;

            var textureScaleAndBias = Matrix4x4.identity;
            textureScaleAndBias.m00 = 0.5f;
            textureScaleAndBias.m11 = 0.5f;
            textureScaleAndBias.m03 = 0.5f;
            textureScaleAndBias.m13 = 0.5f;

            return BurtMainLightShadowMatrixUtility.CreateAtlasSliceTransform(atlasRect) * textureScaleAndBias * worldToShadow;
        }

        private static Quaternion ResolveLightRotation(Vector3 mainLightDirection)
        {
            var forward = mainLightDirection.sqrMagnitude > 0.0001f ? -mainLightDirection.normalized : Vector3.forward;
            var up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.98f ? Vector3.right : Vector3.up;
            return Quaternion.LookRotation(forward, up);
        }

        private static Bounds EncapsulateBoundsInSpace(Bounds bounds, Matrix4x4 transformMatrix)
        {
            FillBoundsCorners(bounds, BoundsCorners);
            var min = transformMatrix.MultiplyPoint3x4(BoundsCorners[0]);
            var max = min;
            for (var cornerIndex = 1; cornerIndex < BoundsCorners.Length; cornerIndex++)
            {
                var corner = transformMatrix.MultiplyPoint3x4(BoundsCorners[cornerIndex]);
                min = Vector3.Min(min, corner);
                max = Vector3.Max(max, corner);
            }

            return new Bounds((min + max) * 0.5f, max - min);
        }

        private static void FillBoundsCorners(Bounds bounds, Vector3[] corners)
        {
            var min = bounds.min;
            var max = bounds.max;
            corners[0] = new Vector3(max.x, max.y, max.z);
            corners[1] = new Vector3(min.x, min.y, min.z);
            corners[2] = new Vector3(min.x, max.y, min.z);
            corners[3] = new Vector3(min.x, max.y, max.z);
            corners[4] = new Vector3(max.x, max.y, min.z);
            corners[5] = new Vector3(max.x, min.y, max.z);
            corners[6] = new Vector3(min.x, min.y, max.z);
            corners[7] = new Vector3(max.x, min.y, min.z);
        }

        private static float CalculateWorldTexelSize(Vector3 projectionExtents, int sliceResolution)
        {
            var projectionWidth = Mathf.Max(0.001f, projectionExtents.x * 2f);
            var projectionHeight = Mathf.Max(0.001f, projectionExtents.y * 2f);
            return Mathf.Max(projectionWidth, projectionHeight) / Mathf.Max(1, sliceResolution);
        }

        private static void UploadPerObjectShadowArrays(
            CommandBuffer cmd,
            Material material,
            Matrix4x4[] worldToShadowMatrices,
            Vector4[] atlasRects,
            Vector4[] sliceParams,
            Vector4[] sliceDepthParams,
            Vector4 shadowParams,
            Vector4 texelSize)
        {
            FillWorldToShadowRows(worldToShadowMatrices);
            if (material != null)
            {
                material.SetVectorArray(PerObjectShadowRows0Id, WorldToShadowRows0);
                material.SetVectorArray(PerObjectShadowRows1Id, WorldToShadowRows1);
                material.SetVectorArray(PerObjectShadowRows2Id, WorldToShadowRows2);
                material.SetVectorArray(PerObjectShadowRows3Id, WorldToShadowRows3);
                material.SetVectorArray(PerObjectShadowAtlasRectsId, atlasRects);
                material.SetVectorArray(PerObjectShadowSliceParamsId, sliceParams);
                material.SetVectorArray(PerObjectShadowSliceDepthParamsId, sliceDepthParams);
                material.SetVector(PerObjectShadowParamsId, shadowParams);
                material.SetVector(PerObjectShadowTexelSizeId, texelSize);
            }

            if (cmd != null)
            {
                cmd.SetGlobalVectorArray(PerObjectShadowRows0Id, WorldToShadowRows0);
                cmd.SetGlobalVectorArray(PerObjectShadowRows1Id, WorldToShadowRows1);
                cmd.SetGlobalVectorArray(PerObjectShadowRows2Id, WorldToShadowRows2);
                cmd.SetGlobalVectorArray(PerObjectShadowRows3Id, WorldToShadowRows3);
                cmd.SetGlobalVectorArray(PerObjectShadowAtlasRectsId, atlasRects);
                cmd.SetGlobalVectorArray(PerObjectShadowSliceParamsId, sliceParams);
                cmd.SetGlobalVectorArray(PerObjectShadowSliceDepthParamsId, sliceDepthParams);
                cmd.SetGlobalVector(PerObjectShadowParamsId, shadowParams);
                cmd.SetGlobalVector(PerObjectShadowTexelSizeId, texelSize);
            }
        }

        private static void FillWorldToShadowRows(Matrix4x4[] matrices)
        {
            for (var sliceIndex = 0; sliceIndex < MaxSlices; sliceIndex++)
            {
                var matrix = matrices != null && matrices.Length > sliceIndex ? matrices[sliceIndex] : Matrix4x4.identity;
                WorldToShadowRows0[sliceIndex] = matrix.GetRow(0);
                WorldToShadowRows1[sliceIndex] = matrix.GetRow(1);
                WorldToShadowRows2[sliceIndex] = matrix.GetRow(2);
                WorldToShadowRows3[sliceIndex] = matrix.GetRow(3);
            }
        }

        private static void SetWorldToCameraAndCameraToWorldMatrices(CommandBuffer cmd, Matrix4x4 viewMatrix)
        {
            var worldToCameraMatrix = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * viewMatrix;
            cmd.SetGlobalMatrix(UnityWorldToCameraId, worldToCameraMatrix);
            cmd.SetGlobalMatrix(UnityCameraToWorldId, worldToCameraMatrix.inverse);
        }

        private static void SetKeyword(CommandBuffer cmd, string keyword, bool enabled)
        {
            if (cmd == null || string.IsNullOrEmpty(keyword))
            {
                return;
            }

            if (enabled)
            {
                cmd.EnableShaderKeyword(keyword);
            }
            else
            {
                cmd.DisableShaderKeyword(keyword);
            }
        }

        private static Matrix4x4[] CreateIdentityMatrixArray()
        {
            var matrices = new Matrix4x4[MaxSlices];
            for (var sliceIndex = 0; sliceIndex < matrices.Length; sliceIndex++)
            {
                matrices[sliceIndex] = Matrix4x4.identity;
            }

            return matrices;
        }

        private static Vector4[] CreateDefaultAtlasRectArray()
        {
            var rects = new Vector4[MaxSlices];
            for (var sliceIndex = 0; sliceIndex < rects.Length; sliceIndex++)
            {
                rects[sliceIndex] = new Vector4(0f, 0f, 1f, 1f);
            }

            return rects;
        }

        private sealed class DirectionalLightRenderingLayerMaskScope : System.IDisposable
        {
            private readonly List<Light> lights = new List<Light>();
            private readonly List<int> renderingLayerMasks = new List<int>();

            public void Add(Light light, int renderingLayerMask)
            {
                lights.Add(light);
                renderingLayerMasks.Add(renderingLayerMask);
            }

            public void Dispose()
            {
                for (var lightIndex = 0; lightIndex < lights.Count; lightIndex++)
                {
                    var light = lights[lightIndex];
                    if (light != null)
                    {
                        light.renderingLayerMask = renderingLayerMasks[lightIndex];
                    }
                }

                lights.Clear();
                renderingLayerMasks.Clear();
            }
        }
    }

    internal sealed class PerObjectShadowPrepareData
    {
        public readonly List<BurtPerObjectShadow> Components = new List<BurtPerObjectShadow>(BurtPerObjectShadowUtility.MaxSlices);
        public readonly PerObjectShadowSlice[] Slices = CreateSlices();

        public int SliceCount;
        public int AtlasWidth;
        public int AtlasHeight;

        private static PerObjectShadowSlice[] CreateSlices()
        {
            var slices = new PerObjectShadowSlice[BurtPerObjectShadowUtility.MaxSlices];
            for (var sliceIndex = 0; sliceIndex < slices.Length; sliceIndex++)
            {
                slices[sliceIndex] = new PerObjectShadowSlice();
            }

            return slices;
        }
    }

    internal sealed class PerObjectShadowSlice
    {
        public readonly List<Renderer> Renderers = new List<Renderer>(16);
        public readonly List<BurtMultipassRenderer> MultipassRenderers = new List<BurtMultipassRenderer>(4);

        public int Index;
        public int SliceResolution;
        public float Strength;
        public float ReceiverDepthBias;
        public float ReceiverDistance;
        public float ReceiverNormalBias;
        public float WorldTexelSize;
        public float DepthRangeWorld;
        public Rect Viewport;
        public Vector4 AtlasRect;
        public Matrix4x4 ViewMatrix;
        public Matrix4x4 ProjectionMatrix;
        public Matrix4x4 WorldToShadowMatrix;
        public BurtPerObjectShadow Component;

        public void Reset()
        {
            Renderers.Clear();
            MultipassRenderers.Clear();
            Index = 0;
            SliceResolution = BurtPerObjectShadowUtility.DefaultSliceResolution;
            Strength = 0f;
            ReceiverDepthBias = 0f;
            ReceiverDistance = 0f;
            ReceiverNormalBias = 0f;
            WorldTexelSize = 0f;
            DepthRangeWorld = 0f;
            Viewport = default;
            AtlasRect = new Vector4(0f, 0f, 1f, 1f);
            ViewMatrix = Matrix4x4.identity;
            ProjectionMatrix = Matrix4x4.identity;
            WorldToShadowMatrix = Matrix4x4.identity;
            Component = null;
        }
    }

    internal sealed class BurtAllocatePerObjectShadowAtlasPass : BurtRenderPass
    {
        public override string Name => "Burt Allocate Per Object Shadow Atlas";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtPerObjectShadowUtility.ShouldUsePerObjectShadow(builder.Request, builder.Asset))
            {
                return;
            }

            builder.WritePerObjectShadowAtlas();
            builder.WriteShadowGlobals();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!BurtPerObjectShadowUtility.ShouldUsePerObjectShadow(context.Request, context.Asset))
            {
                return;
            }

            var atlasTarget = context.PerObjectShadowAtlasTarget;
            if (!atlasTarget.IsValid)
            {
                return;
            }

            var preparedData = BurtPerObjectShadowUtility.Prepare(context.Request);
            if (preparedData.SliceCount <= 0)
            {
                var clearCmd = CommandBufferPool.Get(Name + " Clear Globals");
                BurtPerObjectShadowUtility.UploadPerObjectShadowReceiverGlobals(clearCmd, null, atlasTarget, preparedData);
                context.ScriptableContext.ExecuteCommandBuffer(clearCmd);
                CommandBufferPool.Release(clearCmd);
                return;
            }

            var descriptor = BurtPerObjectShadowUtility.CreateAtlasDescriptor(preparedData);
            var cmd = CommandBufferPool.Get(Name);
            cmd.GetTemporaryRT(BurtRenderGraphResourceRegistry.PerObjectShadowAtlasId, descriptor, FilterMode.Bilinear);
            cmd.SetRenderTarget(atlasTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, descriptor.width, descriptor.height);
            cmd.ClearRenderTarget(true, false, Color.clear, BurtShadowRenderTargetUtility.ResolveMainLightShadowClearDepth());
            BurtPerObjectShadowUtility.BindPerObjectShadowAtlasIfValid(cmd, atlasTarget);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    internal sealed class BurtDrawPerObjectShadowCasterPass : BurtRenderPass
    {
        public override string Name => "Burt Draw Per Object Shadow Caster";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtPerObjectShadowUtility.ShouldUsePerObjectShadow(builder.Request, builder.Asset))
            {
                return;
            }

            builder.WritePerObjectShadowAtlas();
            builder.WriteShadowGlobals();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!BurtPerObjectShadowUtility.ShouldUsePerObjectShadow(context.Request, context.Asset))
            {
                return;
            }

            var request = context.Request;
            var camera = request.Camera;
            var atlasTarget = context.PerObjectShadowAtlasTarget;
            if (camera == null || !atlasTarget.IsValid)
            {
                DisablePerObjectShadowReceiverGlobals(context.ScriptableContext);
                return;
            }

            var preparedData = BurtPerObjectShadowUtility.Prepare(request);
            if (preparedData.SliceCount <= 0)
            {
                DisablePerObjectShadowReceiverGlobals(context.ScriptableContext, atlasTarget, preparedData);
                return;
            }

            var mainLightDirection = BurtPerObjectShadowUtility.ResolveMainLightDirection(request);
            var cmd = CommandBufferPool.Get(Name);
            try
            {
                cmd.SetGlobalInt(BurtPerObjectShadowUtility.PerObjectShadowObjectIndexId, 0);
                cmd.SetRenderTarget(atlasTarget.Identifier);
                BurtRenderTargetDescriptorUtility.SetViewport(cmd, preparedData.AtlasWidth, preparedData.AtlasHeight);
                cmd.ClearRenderTarget(true, false, Color.clear, BurtShadowRenderTargetUtility.ResolveMainLightShadowClearDepth());
                BurtPerObjectShadowUtility.UploadPerObjectShadowReceiverGlobals(cmd, null, atlasTarget, preparedData);
                context.ScriptableContext.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                for (var sliceIndex = 0; sliceIndex < preparedData.SliceCount; sliceIndex++)
                {
                    var slice = preparedData.Slices[sliceIndex];
                    cmd.SetRenderTarget(atlasTarget.Identifier);
                    cmd.SetViewport(slice.Viewport);
                    cmd.EnableScissorRect(slice.Viewport);
                    cmd.SetViewProjectionMatrices(slice.ViewMatrix, slice.ProjectionMatrix);
                    BurtPerObjectShadowUtility.SetupDirectionalShadowCasterState(
                        cmd,
                        mainLightDirection,
                        slice.ViewMatrix,
                        0f,
                        0f);
                    context.ScriptableContext.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    DrawSliceRenderers(cmd, context.ScriptableContext, slice, camera);

                    cmd.DisableScissorRect();
                    context.ScriptableContext.ExecuteCommandBuffer(cmd);
                    cmd.Clear();
                }
            }
            finally
            {
                CommandBufferPool.Release(cmd);
                BurtPerObjectShadowUtility.ResetShadowCasterState(context, context.ScriptableContext, camera);
            }

            UploadPerObjectShadowReceiverGlobals(context.ScriptableContext, atlasTarget, preparedData);
        }

        private static void DrawSliceRenderers(CommandBuffer cmd, ScriptableRenderContext renderContext, PerObjectShadowSlice slice, Camera camera)
        {
            if (slice == null)
            {
                return;
            }

            for (var rendererIndex = 0; rendererIndex < slice.Renderers.Count; rendererIndex++)
            {
                var renderer = slice.Renderers[rendererIndex];
                if (!BurtPerObjectShadowUtility.IsRenderableShadowCaster(renderer, camera))
                {
                    continue;
                }

                var sharedMaterials = renderer.sharedMaterials;
                if (sharedMaterials == null || sharedMaterials.Length <= 0)
                {
                    continue;
                }

                for (var submeshIndex = 0; submeshIndex < sharedMaterials.Length; submeshIndex++)
                {
                    var material = sharedMaterials[submeshIndex];
                    if (material == null)
                    {
                        continue;
                    }

                    var passIndex = BurtPerObjectShadowUtility.FindShadowCasterPass(material);
                    if (passIndex < 0)
                    {
                        continue;
                    }

                    cmd.DrawRenderer(renderer, material, submeshIndex, passIndex);
                }
            }

            for (var rendererIndex = 0; rendererIndex < slice.MultipassRenderers.Count; rendererIndex++)
            {
                BurtMultipassRenderer.DrawOne(
                    cmd,
                    slice.MultipassRenderers[rendererIndex],
                    camera,
                    BurtMultipassShaderPass.ShadowCaster,
                    RenderQueueRange.opaque,
                    (int)BurtPerObjectShadow.PerObjectShadowRenderingLayerMask,
                    true);
            }

            renderContext.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        private static void UploadPerObjectShadowReceiverGlobals(
            ScriptableRenderContext renderContext,
            BurtRenderTargetHandle atlasTarget,
            PerObjectShadowPrepareData preparedData)
        {
            if (preparedData == null || preparedData.SliceCount <= 0 || !atlasTarget.IsValid)
            {
                DisablePerObjectShadowReceiverGlobals(renderContext);
                return;
            }

            var cmd = CommandBufferPool.Get("Burt Upload Per Object Shadow Receiver");
            BurtPerObjectShadowUtility.UploadPerObjectShadowReceiverGlobals(cmd, null, atlasTarget, preparedData);
            renderContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private static void DisablePerObjectShadowReceiverGlobals(ScriptableRenderContext renderContext)
        {
            var cmd = CommandBufferPool.Get("Burt Disable Per Object Shadow Receiver");
            BurtPerObjectShadowUtility.ClearPerObjectShadowReceiverGlobals(cmd);
            renderContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private static void DisablePerObjectShadowReceiverGlobals(
            ScriptableRenderContext renderContext,
            BurtRenderTargetHandle atlasTarget,
            PerObjectShadowPrepareData preparedData)
        {
            var cmd = CommandBufferPool.Get("Burt Disable Per Object Shadow Receiver");
            BurtPerObjectShadowUtility.UploadPerObjectShadowReceiverGlobals(cmd, null, atlasTarget, preparedData);
            renderContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    internal sealed class BurtDebugPerObjectShadowAtlasPass : BurtRenderPass
    {
        private const string DebugShadowShaderName = "Hidden/BurtRP/DebugPerObjectShadowAtlas";
        private static readonly int ShadowDebugExposureId = Shader.PropertyToID("_BurtPerObjectShadowDebugExposure");
        private static readonly int ShadowDebugYFlipId = Shader.PropertyToID("_BurtPerObjectShadowDebugYFlip");

        private Material debugShadowMaterial;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Debug Per Object Shadow Atlas";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.Debug;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtPerObjectShadowUtility.ShouldUsePerObjectShadow(builder.Request, builder.Asset))
            {
                return;
            }

            if (BurtShadingDebugSettings.Mode != BurtShadingDebugMode.PerObjectShadowAtlas)
            {
                return;
            }

            builder.ReadPerObjectShadowAtlas();
            builder.WriteCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!BurtPerObjectShadowUtility.ShouldUsePerObjectShadow(context.Request, context.Asset))
            {
                return;
            }

            var cameraColorTarget = context.CameraColorTarget;
            var atlasTarget = context.PerObjectShadowAtlasTarget;
            if (!cameraColorTarget.IsValid || !atlasTarget.IsValid)
            {
                return;
            }

            var material = GetDebugShadowMaterial();
            if (material == null)
            {
                return;
            }

            material.SetFloat(ShadowDebugExposureId, 1f);
            material.SetFloat(ShadowDebugYFlipId, BurtFinalBlitUtility.ResolveFinalBlitYFlip(context.Request));

            var cmd = CommandBufferPool.Get(Name);
            cmd.SetRenderTarget(cameraColorTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, context.Request != null ? context.Request.Camera : null);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.PerObjectShadowAtlasId, atlasTarget.Identifier);
            cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private Material GetDebugShadowMaterial()
        {
            if (debugShadowMaterial != null)
            {
                return debugShadowMaterial;
            }

            var shader = Shader.Find(DebugShadowShaderName);
            if (shader == null)
            {
                if (!hasLoggedMissingShader)
                {
                    Debug.LogWarning("BurtRP could not find shader: " + DebugShadowShaderName);
                    hasLoggedMissingShader = true;
                }

                return null;
            }

            debugShadowMaterial = new Material(shader);
            debugShadowMaterial.hideFlags = HideFlags.HideAndDontSave;
            return debugShadowMaterial;
        }
    }

    internal sealed class BurtReleasePerObjectShadowAtlasPass : BurtRenderPass
    {
        public override string Name => "Burt Release Per Object Shadow Atlas";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtPerObjectShadowUtility.ShouldUsePerObjectShadow(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadPerObjectShadowAtlas();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!BurtPerObjectShadowUtility.ShouldUsePerObjectShadow(context.Request, context.Asset))
            {
                return;
            }

            var atlasTarget = context.PerObjectShadowAtlasTarget;
            if (!atlasTarget.IsValid)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            cmd.ReleaseTemporaryRT(BurtRenderGraphResourceRegistry.PerObjectShadowAtlasId);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
