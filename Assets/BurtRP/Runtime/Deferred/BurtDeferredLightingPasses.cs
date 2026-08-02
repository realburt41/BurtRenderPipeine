using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    internal sealed class BurtAllocateDeferredLightingDepthPass : BurtRenderPass
    {
        public override string Name => "Burt Allocate Deferred Lighting Depth";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            builder.WriteDeferredLightingDepth();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var target = context != null
                ? context.DeferredLightingDepthTarget
                : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.DeferredLightingDepthName);
            if (context == null || !target.IsValid)
            {
                return;
            }

            var camera = context.Request != null ? context.Request.Camera : null;
            var descriptor = BurtRenderTargetDescriptorUtility.CreateDeferredLightingDepthDescriptor(camera);
            var cmd = context.AcquireCommandBuffer(Name);
            cmd.GetTemporaryRT(BurtRenderGraphResourceRegistry.DeferredLightingDepthTextureId, descriptor, FilterMode.Point);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.DeferredLightingDepthTextureId, target.Identifier);
            context.ExecuteAndReleaseCommandBuffer(cmd);
        }
    }

    internal sealed class BurtCopyDeferredLightingDepthPass : BurtRenderPass
    {
        private const int CopyDepthPassIndex = 0;
        private const string DepthCopyShaderName = BurtHiZDepthPassUtility.HiZDepthShaderName;
        private static readonly int CameraDepthId = BurtRenderGraphResourceRegistry.CameraDepthTextureId;
        private Material depthCopyMaterial;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Copy Deferred Lighting Depth";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            builder.ReadCameraDepth();
            builder.WriteDeferredLightingDepth();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null)
            {
                return;
            }

            var cameraDepthTarget = context.CameraDepthTarget;
            var deferredLightingDepthTarget = context.DeferredLightingDepthTarget;
            if (!cameraDepthTarget.IsValid || !deferredLightingDepthTarget.IsValid)
            {
                return;
            }

            var material = GetDepthCopyMaterial();
            if (material == null)
            {
                return;
            }

            var cmd = context.AcquireCommandBuffer(Name);
            cmd.SetRenderTarget(deferredLightingDepthTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, context.Request != null ? context.Request.Camera : null);
            cmd.SetGlobalTexture(CameraDepthId, cameraDepthTarget.Identifier);
            cmd.DrawProcedural(Matrix4x4.identity, material, CopyDepthPassIndex, MeshTopology.Triangles, 3, 1);
            cmd.SetGlobalTexture(CameraDepthId, deferredLightingDepthTarget.Identifier);
            context.ExecuteAndReleaseCommandBuffer(cmd);
        }

        private Material GetDepthCopyMaterial()
        {
            if (depthCopyMaterial != null)
            {
                return depthCopyMaterial;
            }

            var shader = Shader.Find(DepthCopyShaderName);
            if (shader == null)
            {
                if (!hasLoggedMissingShader)
                {
                    Debug.LogWarning("BurtRP could not find shader: " + DepthCopyShaderName);
                    hasLoggedMissingShader = true;
                }

                return null;
            }

            depthCopyMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            return depthCopyMaterial;
        }
    }

    internal sealed class BurtReleaseDeferredLightingDepthPass : BurtRenderPass
    {
        public override string Name => "Burt Release Deferred Lighting Depth";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            builder.ReadDeferredLightingDepth();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var target = context != null
                ? context.DeferredLightingDepthTarget
                : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.DeferredLightingDepthName);
            if (context == null || !target.IsValid)
            {
                return;
            }

            var cmd = context.AcquireCommandBuffer(Name);
            var cameraDepthTarget = context.CameraDepthTarget;
            if (cameraDepthTarget.IsValid)
            {
                cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.CameraDepthTextureId, cameraDepthTarget.Identifier);
            }

            cmd.ReleaseTemporaryRT(BurtRenderGraphResourceRegistry.DeferredLightingDepthTextureId);
            context.ExecuteAndReleaseCommandBuffer(cmd);
        }
    }

    internal sealed class BurtClearDeferredLightingTargetPass : BurtRenderPass
    {
        public override string Name => "Burt Clear Deferred Lighting Target";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.Clear;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            builder.WriteCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var cameraColorTarget = context != null
                ? context.CameraColorTarget
                : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraColorName);
            if (!cameraColorTarget.IsValid || context == null)
            {
                return;
            }

            var cmd = context.AcquireCommandBuffer(Name);
            cmd.SetRenderTarget(cameraColorTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, context.Request != null ? context.Request.Camera : null);
            cmd.ClearRenderTarget(false, true, Color.clear);
            context.ExecuteAndReleaseCommandBuffer(cmd);
        }
    }

    internal abstract class BurtDeferredLightingPass : BurtRenderPass
    {
        private enum DeferredLightingDebugCategory
        {
            None,
            Lighting,
            Probe,
            Brdf,
            Shadow,
            Transmission
        }

        private const string DeferredLightingShaderName = "Hidden/BurtRP/DeferredLighting";
        private const string DeferredAdditionalLightingShaderName = "Hidden/BurtRP/DeferredAdditionalLighting";
        private const string DeferredPunctualTileLightingShaderName = "Hidden/BurtRP/DeferredPunctualTileLighting";
        private const string DeferredLightingDebugProbeShaderName = "Hidden/BurtRP/DeferredLightingDebugProbe";
        private const string DeferredLightingDebugShadowShaderName = "Hidden/BurtRP/DeferredLightingDebugShadow";
        private const string DeferredLightingDebugKeyword = "BURT_USE_DEBUG_MODE_DEFERRED";
        private const string DeferredGIProbeVolumeEvaluateKeyword = "BURT_GI_PROBE_VOLUME_EVALUATE";
        private const string DeferredGISceneVoxelProbeEvaluateKeyword = "BURT_GI_SCENE_VOXEL_PROBE_EVALUATE";
        private const string DeferredGIProbeHybridEvaluateKeyword = "BURT_GI_PROBE_HYBRID_EVALUATE";
        private static readonly string[] PunctualTileBinKeywords =
        {
            "BURT_PUNCTUAL_BIN_1_2",
            "BURT_PUNCTUAL_BIN_3_8"
        };

        private static readonly int GBuffer0Id = BurtRenderGraphResourceRegistry.GBuffer0Id;
        private static readonly int GBuffer1Id = BurtRenderGraphResourceRegistry.GBuffer1Id;
        private static readonly int GBuffer2Id = BurtRenderGraphResourceRegistry.GBuffer2Id;
        private static readonly int GBuffer3Id = BurtRenderGraphResourceRegistry.GBuffer3Id;
        private static readonly int GBuffer4Id = BurtRenderGraphResourceRegistry.GBuffer4Id;
        private static readonly int GBuffer5Id = BurtRenderGraphResourceRegistry.GBuffer5Id;
        private static readonly int GBufferObjectIndexId = BurtRenderGraphResourceRegistry.GBufferObjectIndexId;
        private static readonly int CameraDepthId = BurtRenderGraphResourceRegistry.CameraDepthTextureId;
        private static readonly int ScreenSpaceAmbientOcclusionId = BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionTextureId;
        private static readonly int ScreenSpaceAmbientOcclusionEnabledId = Shader.PropertyToID("_BurtScreenSpaceAmbientOcclusionEnabled");
        private static readonly int ScreenSpaceShadowId = BurtRenderGraphResourceRegistry.ScreenSpaceShadowTextureId;
        private static readonly int ScreenSpaceShadowEnabledId = Shader.PropertyToID("_BurtScreenSpaceShadowEnabled");
        private static readonly int AdditionalLightBufferId = Shader.PropertyToID("_BurtAdditionalLightBuffer");
        private static readonly int AdditionalLightBufferEnabledId = Shader.PropertyToID("_BurtAdditionalLightBufferEnabled");
        private static readonly int InverseViewProjectionMatrixId = Shader.PropertyToID("_BurtDeferredInverseViewProjectionMatrix");
        private static readonly int CurrentNonJitteredViewProjectionMatrixId = Shader.PropertyToID("_BurtDeferredCurrentNonJitteredViewProjectionMatrix");
        private static readonly int InverseNonJitteredViewProjectionMatrixId = Shader.PropertyToID("_BurtDeferredInverseNonJitteredViewProjectionMatrix");
        private static readonly int CameraWorldPositionId = Shader.PropertyToID("_BurtDeferredCameraWorldPosition");
        private static readonly int CameraClipPlanesId = Shader.PropertyToID("_BurtDeferredCameraClipPlanes");
        private static readonly int ScreenSizeId = Shader.PropertyToID("_BurtDeferredScreenSize");
        private static readonly int SubsurfaceDiffuseLuminanceOutputEnabledId = Shader.PropertyToID("_BurtDeferredSubsurfaceDiffuseLuminanceOutputEnabled");
        private static readonly int BurtGIApplyIndirectDiffuseTextureId = Shader.PropertyToID("_BurtGIDiffuseIndirectTexture");
        private static readonly int BurtGIApplyIndirectBackfaceDiffuseTextureId = Shader.PropertyToID("_BurtGIBackfaceDiffuseIndirectTexture");
        private static readonly int BurtGIApplyIndirectRoughSpecularTextureId = Shader.PropertyToID("_BurtGIRoughSpecularIndirectTexture");
        private static readonly int BurtGIApplyIndirectParamsId = Shader.PropertyToID("_BurtGIApplyIndirectParams");
        private static readonly int BurtGIApplyIndirectParams1Id = Shader.PropertyToID("_BurtGIApplyIndirectParams1");
        private static readonly int BurtGIShortRangeAOParamsId = Shader.PropertyToID("_BurtGIShortRangeAOParams");
        private static readonly int BurtGITranslucencyVolumeParamsId = Shader.PropertyToID("_BurtGITranslucencyVolumeParams");
        private static readonly int BurtGITranslucencyVolume0TextureId = BurtRenderGraphResourceRegistry.BurtGITranslucencyVolume0TextureId;
        private static readonly int BurtGITranslucencyVolume1TextureId = BurtRenderGraphResourceRegistry.BurtGITranslucencyVolume1TextureId;
        private static readonly int BurtGITranslucencyVolumeGridSizeId = Shader.PropertyToID("_BurtGITranslucencyVolumeGridSize");
        private static readonly int BurtGITranslucencyVolumeGridZParamsId = Shader.PropertyToID("_BurtGITranslucencyVolumeGridZParams");
        private static readonly int BurtGITranslucencyVolumeParams0Id = Shader.PropertyToID("_BurtGITranslucencyVolumeParams0");

        private readonly string passName;
        private readonly int shaderPassIndex;
        private readonly bool readsExistingCameraColor;
        private readonly bool isAdditionalLightingStage;
        private Material deferredLightingMaterial;
        private Material deferredLightingDebugMaterial;
        private readonly Material[] deferredPunctualTileMaterials = new Material[BurtTiledLightData.PunctualTileBinCount];
        private bool hasLoggedMissingShader;
        private bool hasLoggedMissingDebugShader;
        private bool hasLoggedMissingPunctualTileShader;
        private bool hasLoggedMissingShaderPass;

        protected BurtDeferredLightingPass(
            string passName,
            int shaderPassIndex,
            bool readsExistingCameraColor,
            bool isAdditionalLightingStage = false)
        {
            this.passName = passName;
            this.shaderPassIndex = shaderPassIndex;
            this.readsExistingCameraColor = readsExistingCameraColor;
            this.isAdditionalLightingStage = isAdditionalLightingStage;
        }

        public override string Name => passName;

        public override BurtRenderPassKind Kind => BurtRenderPassKind.FullScreen;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            builder.ReadGBuffer0();
            builder.ReadGBuffer1();
            builder.ReadGBuffer2();
            builder.ReadGBuffer3();
            builder.ReadGBuffer4();
            builder.ReadGBuffer5();
            builder.ReadGBufferObjectIndex();
            builder.ReadCameraDepth();
            builder.ReadDeferredLightingDepth();

            if (!isAdditionalLightingStage)
            {
                if (BurtScreenSpaceAmbientOcclusionPassUtility.ShouldUseScreenSpaceAmbientOcclusion(builder.Request, builder.Asset))
                {
                    builder.ReadScreenSpaceAmbientOcclusion();
                }

                if (BurtScreenSpaceShadowPassUtility.ShouldUseScreenSpaceShadow(builder.Request, builder.Asset))
                {
                    builder.ReadScreenSpaceShadow();
                }

                if (ShouldUseBurtGIApplyIndirect(builder.Request, builder.Asset, builder.ResourceRegistry))
                {
                    builder.ReadScreenSpaceGlobalIllumination();
                    if (builder.ResourceRegistry.ContainsRenderTarget(BurtRenderGraphResourceRegistry.BurtGIBackfaceDiffuseIndirectName))
                    {
                        builder.ReadBurtGIBackfaceDiffuseIndirect();
                    }

                    if (builder.ResourceRegistry.ContainsRenderTarget(BurtRenderGraphResourceRegistry.BurtGIRoughSpecularIndirectName))
                    {
                        builder.ReadBurtGIRoughSpecularIndirect();
                    }

                    if (BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIlluminationBilateralUpsample(builder.Request, builder.Asset))
                    {
                        if (builder.ResourceRegistry.ContainsRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationUpsampledName))
                        {
                            builder.ReadScreenSpaceGlobalIlluminationUpsampled();
                        }

                        if (builder.ResourceRegistry.ContainsRenderTarget(BurtRenderGraphResourceRegistry.BurtGIBackfaceDiffuseIndirectUpsampledName))
                        {
                            builder.ReadBurtGIBackfaceDiffuseIndirectUpsampled();
                        }

                        if (builder.ResourceRegistry.ContainsRenderTarget(BurtRenderGraphResourceRegistry.BurtGIRoughSpecularIndirectUpsampledName))
                        {
                            builder.ReadBurtGIRoughSpecularIndirectUpsampled();
                        }
                    }

                    builder.ReadGlobalResource(BurtRenderGraphResourceRegistry.BurtGIApplyIndirectGlobalsName);
                }
            }

            if (ShouldUseRuntimeTiledLighting(builder.Request, builder.Asset, builder.ResourceRegistry))
            {
                builder.ReadTileLightCountBuffer();
                builder.ReadTileLightListBuffer();
                builder.ReadTileLightOffsetBuffer();
            }

            if (ShouldUseRuntimeClusteredLighting(builder.Request, builder.Asset, builder.ResourceRegistry))
            {
                builder.ReadClusterLightCountBuffer();
                builder.ReadClusterLightListBuffer();
                builder.ReadClusterLightOffsetBuffer();
                if (isAdditionalLightingStage &&
                    builder.ResourceRegistry.ContainsBuffer(BurtRenderGraphResourceRegistry.PunctualTileIdBufferName))
                {
                    builder.ReadPunctualTileIdBuffer();
                }
            }

            builder.ReadLightingGlobals();
            builder.ReadShadowGlobals();

            if (!isAdditionalLightingStage &&
                BurtShadowUtility.ShouldUseMainLightShadow(builder.Request, builder.Asset))
            {
                builder.ReadMainLightShadowMap();
            }

            if (readsExistingCameraColor)
            {
                builder.ReadCameraColor();
            }

            builder.WriteCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (ShouldSkipDeferredLightingStage(context))
            {
                return;
            }

            if (!TryGetRequiredTargets(
                    context,
                    out var cameraColorTarget,
                    out var cameraDepthTarget,
                    out var gbuffer0Target,
                    out var gbuffer1Target,
                    out var gbuffer2Target,
                    out var gbuffer3Target,
                    out var gbuffer4Target,
                    out var gbuffer5Target,
                    out var deferredLightingDepthTarget,
                    out var gbufferObjectIndexTarget))
            {
                return;
            }

            var material = GetDeferredLightingMaterial(context.Asset);
            if (material == null || !HasRequiredShaderPass(material))
            {
                return;
            }

            ConfigureDeferredGIProbeEvaluateKeyword(context, material);

            var cmd = context.AcquireCommandBuffer(Name);
            cmd.SetRenderTarget(cameraColorTarget.Identifier, cameraDepthTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, context.Request != null ? context.Request.Camera : null);
            cmd.SetGlobalTexture(GBuffer0Id, gbuffer0Target.Identifier);
            cmd.SetGlobalTexture(GBuffer1Id, gbuffer1Target.Identifier);
            cmd.SetGlobalTexture(GBuffer2Id, gbuffer2Target.Identifier);
            cmd.SetGlobalTexture(GBuffer3Id, gbuffer3Target.Identifier);
            cmd.SetGlobalTexture(GBuffer4Id, gbuffer4Target.Identifier);
            cmd.SetGlobalTexture(GBuffer5Id, gbuffer5Target.Identifier);
            cmd.SetGlobalTexture(GBufferObjectIndexId, gbufferObjectIndexTarget.Identifier);
            cmd.SetGlobalTexture(CameraDepthId, deferredLightingDepthTarget.Identifier);
            if (!isAdditionalLightingStage)
            {
                BindScreenSpaceAmbientOcclusion(context, cmd, material);
                BindScreenSpaceShadow(context, cmd, material);
                BindBurtGIApplyIndirect(context, cmd, material);
            }
            BindAdditionalLightBuffer(context, cmd, material);
            BindRuntimeTiledLighting(context, cmd);
            if (!isAdditionalLightingStage)
            {
                UploadMainLightShadowReceiverGlobals(context, cmd, material);
            }
            UploadAdditionalLightShadowReceiverGlobals(context, cmd, material);
            if (!isAdditionalLightingStage)
            {
                UploadPerObjectShadowReceiverGlobals(context, cmd, material);
            }
            UploadCameraReconstructionGlobals(context, cmd, material);
            BindSubsurfaceDiffuseLuminanceOutput(context, cmd, material);
            DrawDeferredLighting(context, cmd, material);
            cmd.SetGlobalTexture(CameraDepthId, cameraDepthTarget.Identifier);
            context.ExecuteAndReleaseCommandBuffer(cmd);
        }

        private void DrawDeferredLighting(BurtRenderGraphContext context, CommandBuffer cmd, Material material)
        {
            if (!isAdditionalLightingStage ||
                context == null ||
                context.Request == null ||
                context.Request.LightingData == null)
            {
                cmd.DrawProcedural(Matrix4x4.identity, material, shaderPassIndex, MeshTopology.Triangles, 3, 1);
                return;
            }

            var lightingData = context.Request.LightingData;
            if (!lightingData.ShouldUsePunctualTileDraw ||
                context.ResourceRegistry == null ||
                !context.ResourceRegistry.ContainsBuffer(BurtRenderGraphResourceRegistry.PunctualTileIdBufferName))
            {
                cmd.DrawProcedural(Matrix4x4.identity, material, shaderPassIndex, MeshTopology.Triangles, 3, 1);
                return;
            }

            var tileIdBuffer = context.PunctualTileIdBuffer;
            if (!tileIdBuffer.IsValid ||
                !tileIdBuffer.HasBuffer)
            {
                cmd.DrawProcedural(Matrix4x4.identity, material, shaderPassIndex, MeshTopology.Triangles, 3, 1);
                return;
            }

            cmd.SetGlobalBuffer(BurtTiledLightData.PunctualTileIdBufferId, tileIdBuffer.Buffer);
            for (var binIndex = 0; binIndex < BurtTiledLightData.PunctualTileBinCount; binIndex++)
            {
                var tileCount = lightingData.PunctualTileDrawBinCounts[binIndex];
                if (tileCount <= 0)
                {
                    continue;
                }

                var tileMaterial = GetDeferredPunctualTileMaterial(context.Asset, material, binIndex);
                if (tileMaterial == null || shaderPassIndex < 0 || shaderPassIndex >= tileMaterial.passCount)
                {
                    continue;
                }

                cmd.SetGlobalInt(
                    BurtTiledLightData.PunctualTileIdOffsetId,
                    lightingData.PunctualTileDrawBinOffsets[binIndex]);
                cmd.DrawProcedural(
                    Matrix4x4.identity,
                    tileMaterial,
                    shaderPassIndex,
                    MeshTopology.Triangles,
                    6,
                    tileCount);
            }

            cmd.SetGlobalInt(BurtTiledLightData.PunctualTileIdOffsetId, 0);
        }

        private Material GetDeferredPunctualTileMaterial(BurtRenderPipelineAsset asset, Material fullscreenMaterial, int binIndex)
        {
            if (fullscreenMaterial == null || fullscreenMaterial.shader == null ||
                binIndex < 0 || binIndex >= deferredPunctualTileMaterials.Length)
            {
                return null;
            }

            var tileShader = asset != null && asset.RuntimeResources != null
                ? asset.RuntimeResources.ResolveDeferredShadingDebugShader(DeferredPunctualTileLightingShaderName)
                : null;
            if (tileShader == null)
            {
                tileShader = Shader.Find(DeferredPunctualTileLightingShaderName);
            }
            if (tileShader == null)
            {
                if (!hasLoggedMissingPunctualTileShader)
                {
                    Debug.LogWarning("BurtRP could not find shader: " + DeferredPunctualTileLightingShaderName);
                    hasLoggedMissingPunctualTileShader = true;
                }

                return null;
            }

            var material = deferredPunctualTileMaterials[binIndex];
            if (material == null || material.shader != tileShader)
            {
                if (material != null)
                {
                    CoreUtils.Destroy(material);
                }

                material = new Material(tileShader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                deferredPunctualTileMaterials[binIndex] = material;
                BurtShadingModelIds.ApplyDeferredLightingStencilProperties(material);
            }

            for (var keywordIndex = 0; keywordIndex < PunctualTileBinKeywords.Length; keywordIndex++)
            {
                CoreUtils.SetKeyword(material, PunctualTileBinKeywords[keywordIndex], keywordIndex == binIndex);
            }
            CoreUtils.SetKeyword(
                material,
                DeferredLightingDebugKeyword,
                fullscreenMaterial.IsKeywordEnabled(DeferredLightingDebugKeyword));
            return material;
        }

        private static bool TryGetRequiredTargets(
            BurtRenderGraphContext context,
            out BurtRenderTargetHandle cameraColorTarget,
            out BurtRenderTargetHandle cameraDepthTarget,
            out BurtRenderTargetHandle gbuffer0Target,
            out BurtRenderTargetHandle gbuffer1Target,
            out BurtRenderTargetHandle gbuffer2Target,
            out BurtRenderTargetHandle gbuffer3Target,
            out BurtRenderTargetHandle gbuffer4Target,
            out BurtRenderTargetHandle gbuffer5Target,
            out BurtRenderTargetHandle deferredLightingDepthTarget,
            out BurtRenderTargetHandle gbufferObjectIndexTarget)
        {
            cameraColorTarget = context != null ? context.CameraColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraColorName);
            cameraDepthTarget = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            gbuffer0Target = context != null ? context.GBuffer0Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer0Name);
            gbuffer1Target = context != null ? context.GBuffer1Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer1Name);
            gbuffer2Target = context != null ? context.GBuffer2Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer2Name);
            gbuffer3Target = context != null ? context.GBuffer3Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer3Name);
            gbuffer4Target = context != null ? context.GBuffer4Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer4Name);
            gbuffer5Target = context != null ? context.GBuffer5Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer5Name);
            deferredLightingDepthTarget = context != null ? context.DeferredLightingDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.DeferredLightingDepthName);
            gbufferObjectIndexTarget = context != null ? context.GBufferObjectIndexTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBufferObjectIndexName);

            return cameraColorTarget.IsValid &&
                cameraDepthTarget.IsValid &&
                gbuffer0Target.IsValid &&
                gbuffer1Target.IsValid &&
                gbuffer2Target.IsValid &&
                gbuffer3Target.IsValid &&
                gbuffer4Target.IsValid &&
                gbuffer5Target.IsValid &&
                deferredLightingDepthTarget.IsValid &&
                gbufferObjectIndexTarget.IsValid;
        }

        private static void BindScreenSpaceAmbientOcclusion(BurtRenderGraphContext context, CommandBuffer cmd, Material material)
        {
            var enabled = false;
            var target = context != null
                ? context.ScreenSpaceAmbientOcclusionTarget
                : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionName);

            if (context != null &&
                target.IsValid &&
                BurtScreenSpaceAmbientOcclusionPassUtility.ShouldUseScreenSpaceAmbientOcclusion(context.Request, context.Asset))
            {
                cmd.SetGlobalTexture(ScreenSpaceAmbientOcclusionId, target.Identifier);
                enabled = true;
            }

            cmd.SetGlobalFloat(ScreenSpaceAmbientOcclusionEnabledId, enabled ? 1f : 0f);
            if (material != null)
            {
                material.SetFloat(ScreenSpaceAmbientOcclusionEnabledId, enabled ? 1f : 0f);
            }
        }

        private static void BindScreenSpaceShadow(BurtRenderGraphContext context, CommandBuffer cmd, Material material)
        {
            var enabled = false;
            var target = context != null
                ? context.ScreenSpaceShadowTarget
                : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceShadowName);

            if (context != null &&
                target.IsValid &&
                BurtScreenSpaceShadowPassUtility.ShouldUseScreenSpaceShadow(context.Request, context.Asset))
            {
                cmd.SetGlobalTexture(ScreenSpaceShadowId, target.Identifier);
                enabled = true;
            }
            else
            {
                cmd.SetGlobalTexture(ScreenSpaceShadowId, Texture2D.whiteTexture);
            }

            cmd.SetGlobalFloat(ScreenSpaceShadowEnabledId, enabled ? 1f : 0f);
            if (material != null)
            {
                material.SetFloat(ScreenSpaceShadowEnabledId, enabled ? 1f : 0f);
            }
        }

        private static void BindBurtGIApplyIndirect(BurtRenderGraphContext context, CommandBuffer cmd, Material material)
        {
            var enabled = false;
            var intensity = 0f;
            var backfaceDiffuseEnabled = false;
            var roughSpecularEnabled = false;
            var translucencyVolumeEnabled = false;
            var characterIntensity = 1f;
            var diffuseColorBoost = 1f;
            var xgiScreenRatio = 1f;
            var xgiScreenRatioSpeed = 0.1f;
            var shortRangeAOParams = Vector4.zero;
            var translucencyVolumeParams = Vector4.zero;
            var target = context != null
                ? context.ScreenSpaceGlobalIlluminationTarget
                : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationName);
            var backfaceDiffuseTarget = context != null
                ? context.BurtGIBackfaceDiffuseIndirectTarget
                : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIBackfaceDiffuseIndirectName);
            var roughSpecularTarget = context != null
                ? context.BurtGIRoughSpecularIndirectTarget
                : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIRoughSpecularIndirectName);
            var translucencyVolume0Target = context != null
                ? context.BurtGITranslucencyVolume0Target
                : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGITranslucencyVolume0Name);
            var translucencyVolume1Target = context != null
                ? context.BurtGITranslucencyVolume1Target
                : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGITranslucencyVolume1Name);
            var translucencyVolumeFilter0Target = context != null
                ? context.BurtGITranslucencyVolumeFilter0Target
                : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGITranslucencyVolumeFilter0Name);
            var translucencyVolumeFilter1Target = context != null
                ? context.BurtGITranslucencyVolumeFilter1Target
                : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGITranslucencyVolumeFilter1Name);
            if (BurtGITranslucencyVolumeRuntimeState.HasFilteredVolumeThisFrame &&
                translucencyVolumeFilter0Target.IsValid &&
                translucencyVolumeFilter1Target.IsValid)
            {
                translucencyVolume0Target = translucencyVolumeFilter0Target;
                translucencyVolume1Target = translucencyVolumeFilter1Target;
            }

            if (context != null &&
                BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIlluminationBilateralUpsample(context.Request, context.Asset))
            {
                var upsampledTarget = context.ScreenSpaceGlobalIlluminationUpsampledTarget;
                var backfaceDiffuseUpsampledTarget = context.BurtGIBackfaceDiffuseIndirectUpsampledTarget;
                var roughSpecularUpsampledTarget = context.BurtGIRoughSpecularIndirectUpsampledTarget;
                if (upsampledTarget.IsValid)
                {
                    target = upsampledTarget;
                }

                if (backfaceDiffuseUpsampledTarget.IsValid)
                {
                    backfaceDiffuseTarget = backfaceDiffuseUpsampledTarget;
                }

                if (roughSpecularUpsampledTarget.IsValid)
                {
                    roughSpecularTarget = roughSpecularUpsampledTarget;
                }
            }

            if (context != null &&
                target.IsValid &&
                BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIllumination(context.Request, context.Asset))
            {
                var settings = BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationSettings(context.Request, context.Asset);
                enabled = settings.Enabled;
                intensity = settings.Intensity;
                backfaceDiffuseEnabled = settings.EnableBackfaceDiffuse && backfaceDiffuseTarget.IsValid;
                roughSpecularEnabled = settings.EnableRoughSpecular && roughSpecularTarget.IsValid;
                translucencyVolumeEnabled = settings.UseTranslucencyVolume;
                characterIntensity = settings.XGICharacterIntensity;
                diffuseColorBoost = 1f / Mathf.Clamp(settings.SceneVoxelDiffuseColorBoost, 1f, 4f);
                xgiScreenRatio = settings.XGIScreenRatio;
                xgiScreenRatioSpeed = settings.XGIScreenRatioSpeed;
                shortRangeAOParams = new Vector4(
                    settings.ShortRangeAO ? 1f : 0f,
                    settings.ShortRangeAOApplyWeight,
                    settings.ShortRangeAOSlopeCompareToleranceScale,
                    2f);
                translucencyVolumeParams = new Vector4(1f, BurtScreenSpaceGlobalIlluminationPassUtility.TranslucencyVolumeApplyIntensityScale, 0.55f, 1f);
            }

            if (enabled)
            {
                cmd.SetGlobalTexture(BurtGIApplyIndirectDiffuseTextureId, target.Identifier);
            }
            else
            {
                cmd.SetGlobalTexture(BurtGIApplyIndirectDiffuseTextureId, Texture2D.blackTexture);
            }

            cmd.SetGlobalTexture(BurtGIApplyIndirectBackfaceDiffuseTextureId, enabled && backfaceDiffuseEnabled && backfaceDiffuseTarget.IsValid ? backfaceDiffuseTarget.Identifier : Texture2D.blackTexture);
            cmd.SetGlobalTexture(BurtGIApplyIndirectRoughSpecularTextureId, enabled && roughSpecularEnabled && roughSpecularTarget.IsValid ? roughSpecularTarget.Identifier : Texture2D.blackTexture);
            // XRender parity: the translucency froxel volume belongs to transparent forward
            // materials, never to an opaque deferred lighting pass.
            const bool volumeEnabled = false;
            var fallbackVolumeTexture = BurtGITranslucencyVolumeFallbackUtility.BlackVolumeTexture;
            cmd.SetGlobalTexture(BurtGITranslucencyVolume0TextureId, volumeEnabled ? translucencyVolume0Target.Identifier : fallbackVolumeTexture);
            cmd.SetGlobalTexture(BurtGITranslucencyVolume1TextureId, volumeEnabled ? translucencyVolume1Target.Identifier : fallbackVolumeTexture);
            var applyParams = new Vector4(enabled ? 1f : 0f, intensity, enabled && backfaceDiffuseEnabled ? 1f : 0f, enabled && roughSpecularEnabled ? 1f : 0f);
            var applyParams1 = new Vector4(
                enabled ? characterIntensity : 1f,
                enabled ? diffuseColorBoost : 1f,
                enabled ? xgiScreenRatio : 1f,
                enabled ? xgiScreenRatioSpeed : 0.1f);
            cmd.SetGlobalVector(BurtGIApplyIndirectParamsId, applyParams);
            cmd.SetGlobalVector(BurtGIApplyIndirectParams1Id, applyParams1);
            cmd.SetGlobalVector(BurtGIShortRangeAOParamsId, enabled ? shortRangeAOParams : Vector4.zero);
            cmd.SetGlobalVector(BurtGITranslucencyVolumeParamsId, volumeEnabled ? translucencyVolumeParams : Vector4.zero);
            UploadTranslucencyVolumeGlobals(context, cmd, volumeEnabled);
            if (material != null)
            {
                material.SetVector(BurtGIApplyIndirectParamsId, applyParams);
                material.SetVector(BurtGIApplyIndirectParams1Id, applyParams1);
                material.SetVector(BurtGIShortRangeAOParamsId, enabled ? shortRangeAOParams : Vector4.zero);
                material.SetVector(BurtGITranslucencyVolumeParamsId, volumeEnabled ? translucencyVolumeParams : Vector4.zero);
            }
        }

        private static void UploadTranslucencyVolumeGlobals(BurtRenderGraphContext context, CommandBuffer cmd, bool enabled)
        {
            if (!enabled)
            {
                cmd.SetGlobalVector(BurtGITranslucencyVolumeGridSizeId, Vector4.zero);
                cmd.SetGlobalVector(BurtGITranslucencyVolumeGridZParamsId, Vector4.zero);
                cmd.SetGlobalVector(BurtGITranslucencyVolumeParams0Id, Vector4.zero);
                return;
            }

            var camera = context != null && context.Request != null ? context.Request.Camera : null;
            var settings = BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationScreenProbeSettings(context != null ? context.Request : null, context != null ? context.Asset : null);
            var descriptor = BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationTranslucencyVolumeDescriptor(camera, settings);
            var nearPlane = camera != null ? Mathf.Max(camera.nearClipPlane, 0.01f) : 0.1f;
            var cameraFarPlane = camera != null ? Mathf.Max(camera.farClipPlane, nearPlane + 1f) : 1000f;
            var farPlane = Mathf.Min(cameraFarPlane, nearPlane + settings.TranslucencyVolumeEndDistanceFromCamera);
            cmd.SetGlobalVector(BurtGITranslucencyVolumeGridSizeId, new Vector4(
                descriptor.width,
                descriptor.height,
                descriptor.volumeDepth,
                BurtScreenSpaceGlobalIlluminationPassUtility.TranslucencyVolumeMaterialIntensityScale));
            cmd.SetGlobalVector(BurtGITranslucencyVolumeGridZParamsId, BurtScreenSpaceGlobalIlluminationPassUtility.ResolveTranslucencyVolumeGridZParams(settings));
            cmd.SetGlobalVector(BurtGITranslucencyVolumeParams0Id, new Vector4(nearPlane, farPlane, 0.75f, 0.65f));
        }

        private static void BindAdditionalLightBuffer(BurtRenderGraphContext context, CommandBuffer cmd, Material material)
        {
            var additionalLightBuffer = context != null
                ? context.AdditionalLightBuffer
                : BurtRenderBufferHandle.Invalid(BurtRenderGraphResourceRegistry.AdditionalLightBufferName);
            var enabled = additionalLightBuffer.IsValid && additionalLightBuffer.HasBuffer;
            if (enabled)
            {
                cmd.SetGlobalBuffer(AdditionalLightBufferId, additionalLightBuffer.Buffer);
                if (material != null)
                {
                    material.SetBuffer(AdditionalLightBufferId, additionalLightBuffer.Buffer);
                }
            }

            cmd.SetGlobalFloat(AdditionalLightBufferEnabledId, enabled ? 1f : 0f);
            if (material != null)
            {
                material.SetFloat(AdditionalLightBufferEnabledId, enabled ? 1f : 0f);
            }
        }

        private static void BindSubsurfaceDiffuseLuminanceOutput(BurtRenderGraphContext context, CommandBuffer cmd, Material material)
        {
            var enabled = context != null && BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(context.Request, context.Asset);
            cmd.SetGlobalFloat(SubsurfaceDiffuseLuminanceOutputEnabledId, enabled ? 1f : 0f);
            if (material != null)
            {
                material.SetFloat(SubsurfaceDiffuseLuminanceOutputEnabledId, enabled ? 1f : 0f);
            }
        }

        private static bool ShouldUseRuntimeTiledLighting(
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset,
            BurtRenderGraphResourceRegistry resourceRegistry)
        {
            if (!BurtTiledLightData.ShouldUseRuntimeTiledLightingResources(request, asset, true))
            {
                return false;
            }

            return resourceRegistry != null &&
                resourceRegistry.ContainsBuffer(BurtRenderGraphResourceRegistry.TileLightCountBufferName) &&
                resourceRegistry.ContainsBuffer(BurtRenderGraphResourceRegistry.TileLightListBufferName) &&
                resourceRegistry.ContainsBuffer(BurtRenderGraphResourceRegistry.TileLightOffsetBufferName);
        }

        private static bool ShouldUseBurtGIApplyIndirect(
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset,
            BurtRenderGraphResourceRegistry resourceRegistry)
        {
            return BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIllumination(request, asset) &&
                resourceRegistry != null &&
                resourceRegistry.ContainsRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationName);
        }

        private static bool ShouldUseRuntimeClusteredLighting(
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset,
            BurtRenderGraphResourceRegistry resourceRegistry)
        {
            if (!BurtTiledLightData.ShouldUseRuntimeClusteredLightingResources(request, asset, true))
            {
                return false;
            }

            return resourceRegistry != null &&
                resourceRegistry.ContainsBuffer(BurtRenderGraphResourceRegistry.ClusterLightCountBufferName) &&
                resourceRegistry.ContainsBuffer(BurtRenderGraphResourceRegistry.ClusterLightListBufferName) &&
                resourceRegistry.ContainsBuffer(BurtRenderGraphResourceRegistry.ClusterLightOffsetBufferName);
        }

        private static void BindRuntimeTiledLighting(BurtRenderGraphContext context, CommandBuffer cmd)
        {
            var enabled = false;
            var lightingData = context != null && context.Request != null ? context.Request.LightingData : null;
            var countBuffer = context != null
                ? context.TileLightCountBuffer
                : BurtRenderBufferHandle.Invalid(BurtRenderGraphResourceRegistry.TileLightCountBufferName);
            var listBuffer = context != null
                ? context.TileLightListBuffer
                : BurtRenderBufferHandle.Invalid(BurtRenderGraphResourceRegistry.TileLightListBufferName);
            var offsetBuffer = context != null
                ? context.TileLightOffsetBuffer
                : BurtRenderBufferHandle.Invalid(BurtRenderGraphResourceRegistry.TileLightOffsetBufferName);
            var clusterCountBuffer = context != null
                ? context.ClusterLightCountBuffer
                : BurtRenderBufferHandle.Invalid(BurtRenderGraphResourceRegistry.ClusterLightCountBufferName);
            var clusterListBuffer = context != null
                ? context.ClusterLightListBuffer
                : BurtRenderBufferHandle.Invalid(BurtRenderGraphResourceRegistry.ClusterLightListBufferName);
            var clusterOffsetBuffer = context != null
                ? context.ClusterLightOffsetBuffer
                : BurtRenderBufferHandle.Invalid(BurtRenderGraphResourceRegistry.ClusterLightOffsetBufferName);

            if (context != null &&
                BurtTiledLightData.ShouldUseRuntimeTiledLightingResources(context.Request, context.Asset, true) &&
                lightingData != null &&
                lightingData.TileLightDebugUploaded &&
                countBuffer.IsValid && countBuffer.HasBuffer &&
                listBuffer.IsValid && listBuffer.HasBuffer &&
                offsetBuffer.IsValid && offsetBuffer.HasBuffer)
            {
                var layout = ResolveRuntimeTileLightLayout(context, lightingData);
                var maxLightsPerTile = lightingData.TileLightMaxLightsPerTile > 0
                    ? Mathf.Min(lightingData.TileLightMaxLightsPerTile, BurtTiledLightData.ResolveRuntimeMaxLightsPerTile())
                    : BurtTiledLightData.ResolveRuntimeMaxLightsPerTile();
                cmd.SetGlobalBuffer(BurtTiledLightData.TileLightCountBufferId, countBuffer.Buffer);
                cmd.SetGlobalBuffer(BurtTiledLightData.TileLightListBufferId, listBuffer.Buffer);
                cmd.SetGlobalBuffer(BurtTiledLightData.TileLightOffsetBufferId, offsetBuffer.Buffer);
                cmd.SetGlobalVector(
                    BurtTiledLightData.TileLightGridParamsId,
                    new Vector4(layout.TileCountX, layout.TileCountY, layout.TileSize, maxLightsPerTile));
                enabled = true;
            }

            cmd.SetGlobalFloat(BurtTiledLightData.TileLightCountBufferEnabledId, enabled ? 1f : 0f);
            if (!enabled)
            {
                cmd.SetGlobalVector(BurtTiledLightData.TileLightGridParamsId, Vector4.zero);
            }

            BindRuntimeClusteredLighting(cmd, lightingData, clusterCountBuffer, clusterListBuffer, clusterOffsetBuffer);
        }

        private static void BindRuntimeClusteredLighting(
            CommandBuffer cmd,
            BurtLightingData lightingData,
            BurtRenderBufferHandle countBuffer,
            BurtRenderBufferHandle listBuffer,
            BurtRenderBufferHandle offsetBuffer)
        {
            var enabled = lightingData != null &&
                lightingData.ClusterLightUploaded &&
                lightingData.ClusterLightGridX > 0 &&
                lightingData.ClusterLightGridY > 0 &&
                lightingData.ClusterLightDepthSliceCount > 0 &&
                lightingData.ClusterLightMaxLightsPerCluster > 0 &&
                lightingData.ClusterLightListCapacity > 0 &&
                lightingData.ClusterLightInvDepthRange > 0f &&
                countBuffer.IsValid && countBuffer.HasBuffer &&
                listBuffer.IsValid && listBuffer.HasBuffer &&
                offsetBuffer.IsValid && offsetBuffer.HasBuffer;

            if (enabled)
            {
                cmd.SetGlobalBuffer(BurtTiledLightData.ClusterLightCountBufferId, countBuffer.Buffer);
                cmd.SetGlobalBuffer(BurtTiledLightData.ClusterLightListBufferId, listBuffer.Buffer);
                cmd.SetGlobalBuffer(BurtTiledLightData.ClusterLightOffsetBufferId, offsetBuffer.Buffer);
                cmd.SetGlobalVector(
                    BurtTiledLightData.ClusterLightGridParamsId,
                    new Vector4(
                        lightingData.ClusterLightGridX,
                        lightingData.ClusterLightGridY,
                        lightingData.ClusterLightDepthSliceCount,
                        lightingData.ClusterLightMaxLightsPerCluster));
                cmd.SetGlobalVector(
                    BurtTiledLightData.ClusterLightDepthParamsId,
                    new Vector4(
                        lightingData.ClusterLightNearPlane,
                        lightingData.ClusterLightFarPlane,
                        lightingData.ClusterLightInvDepthRange,
                        lightingData.ClusterLightDepthSliceCount));
                cmd.SetGlobalVector(BurtTiledLightData.ClusterLightWorldToViewZId, lightingData.ClusterLightWorldToViewZ);
            }
            else
            {
                cmd.SetGlobalVector(BurtTiledLightData.ClusterLightGridParamsId, Vector4.zero);
                cmd.SetGlobalVector(BurtTiledLightData.ClusterLightDepthParamsId, Vector4.zero);
                cmd.SetGlobalVector(BurtTiledLightData.ClusterLightWorldToViewZId, Vector4.zero);
            }

            cmd.SetGlobalFloat(BurtTiledLightData.ClusterLightBufferEnabledId, enabled ? 1f : 0f);
        }

        private static BurtTileLightLayout ResolveRuntimeTileLightLayout(BurtRenderGraphContext context, BurtLightingData lightingData)
        {
            if (lightingData != null &&
                lightingData.TileLightGridX > 0 &&
                lightingData.TileLightGridY > 0 &&
                lightingData.TileLightTileSize > 0)
            {
                return new BurtTileLightLayout(
                    lightingData.TileLightGridX * lightingData.TileLightTileSize,
                    lightingData.TileLightGridY * lightingData.TileLightTileSize,
                    lightingData.TileLightTileSize);
            }

            return BurtTiledLightData.CalculateLayout(context != null && context.Request != null ? context.Request.Camera : null);
        }

        private static void UploadCameraReconstructionGlobals(
            BurtRenderGraphContext context,
            CommandBuffer cmd,
            Material material)
        {
            var request = context != null ? context.Request : null;
            var camera = request != null ? request.Camera : null;
            if (camera == null)
            {
                return;
            }

            var viewMatrix = camera.worldToCameraMatrix;
            var projectionMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
            var viewProjectionMatrix = projectionMatrix * viewMatrix;
            var inverseViewProjectionMatrix = viewProjectionMatrix.inverse;
            var currentNonJitteredViewProjectionMatrix = request.TemporalAA != null
                ? request.TemporalAA.CurrentNonJitteredViewProjectionMatrix
                : viewProjectionMatrix;
            var inverseNonJitteredViewProjectionMatrix = request.TemporalAA != null
                ? request.TemporalAA.InverseCurrentNonJitteredViewProjectionMatrix
                : inverseViewProjectionMatrix;
            var pixelWidth = Mathf.Max(1, camera.pixelWidth);
            var pixelHeight = Mathf.Max(1, camera.pixelHeight);
            var screenSize = new Vector4(pixelWidth, pixelHeight, 1f / pixelWidth, 1f / pixelHeight);
            var clipPlanes = new Vector4(
                camera.nearClipPlane,
                camera.farClipPlane,
                1f / Mathf.Max(camera.nearClipPlane, 0.0001f),
                1f / Mathf.Max(camera.farClipPlane, 0.0001f));
            var cameraPosition = camera.transform.position;
            var cameraWorldPosition = new Vector4(cameraPosition.x, cameraPosition.y, cameraPosition.z, 1f);

            if (material != null)
            {
                material.SetMatrix(InverseViewProjectionMatrixId, inverseViewProjectionMatrix);
                material.SetMatrix(CurrentNonJitteredViewProjectionMatrixId, currentNonJitteredViewProjectionMatrix);
                material.SetMatrix(InverseNonJitteredViewProjectionMatrixId, inverseNonJitteredViewProjectionMatrix);
                material.SetVector(CameraWorldPositionId, cameraWorldPosition);
                material.SetVector(CameraClipPlanesId, clipPlanes);
                material.SetVector(ScreenSizeId, screenSize);
            }

            cmd.SetGlobalMatrix(InverseViewProjectionMatrixId, inverseViewProjectionMatrix);
            cmd.SetGlobalMatrix(CurrentNonJitteredViewProjectionMatrixId, currentNonJitteredViewProjectionMatrix);
            cmd.SetGlobalMatrix(InverseNonJitteredViewProjectionMatrixId, inverseNonJitteredViewProjectionMatrix);
            cmd.SetGlobalVector(CameraWorldPositionId, cameraWorldPosition);
            cmd.SetGlobalVector(CameraClipPlanesId, clipPlanes);
            cmd.SetGlobalVector(ScreenSizeId, screenSize);
        }

        private void UploadMainLightShadowReceiverGlobals(BurtRenderGraphContext context, CommandBuffer cmd, Material material)
        {
            if (cmd == null || material == null)
            {
                return;
            }

            if (context == null || !BurtShadowUtility.ShouldUseMainLightShadow(context.Request, context.Asset))
            {
                DisableMainLightShadowReceiverGlobals(cmd, material);
                return;
            }

            var shadowMapTarget = context.MainLightShadowMapTarget;
            if (!shadowMapTarget.IsValid)
            {
                DisableMainLightShadowReceiverGlobals(cmd, material);
                return;
            }

            var shadowData = BurtShadowUtility.ResolveMainLightShadowData(context.Request, context.Asset);
            if (shadowData == null || shadowData.MainLightIndex < 0)
            {
                DisableMainLightShadowReceiverGlobals(cmd, material);
                return;
            }

            if (!BurtMainLightShadowMatrixUtility.TryGetMainLightShadowCascadeCache(context.Request, shadowData, out var cascadeCache))
            {
                DisableMainLightShadowReceiverGlobals(cmd, material);
                return;
            }

            BurtMainLightShadowMatrixUtility.UploadMainLightShadowReceiverGlobals(
                cmd,
                material,
                shadowMapTarget,
                cascadeCache.WorldToShadowMatrices,
                cascadeCache.CascadeSpheres,
                cascadeCache.CascadeAtlasRects,
                cascadeCache.CascadeCount,
                cascadeCache.TileResolution,
                shadowData);
        }

        private static void DisableMainLightShadowReceiverGlobals(CommandBuffer cmd, Material material)
        {
            BurtMainLightShadowMatrixUtility.ClearMainLightShadowReceiverGlobals(cmd, material);
        }

        private Material GetDeferredLightingMaterial(BurtRenderPipelineAsset asset)
        {
            var debugCategory = ResolveDeferredLightingDebugCategory(BurtShadingDebugSettings.Mode);
            if (isAdditionalLightingStage)
            {
                var additionalMaterial = GetDeferredLightingMaterial(
                    DeferredAdditionalLightingShaderName,
                    asset,
                    ref deferredLightingMaterial,
                    ref hasLoggedMissingShader);
                ConfigureDeferredLightingDebugKeyword(
                    additionalMaterial,
                    debugCategory == DeferredLightingDebugCategory.Lighting &&
                    ShouldRunAdditionalLightingDebugStage(BurtShadingDebugSettings.Mode));
                return additionalMaterial;
            }

            if (debugCategory == DeferredLightingDebugCategory.Probe ||
                debugCategory == DeferredLightingDebugCategory.Shadow)
            {
                var debugShaderName = ResolveDeferredLightingDebugShaderName(BurtShadingDebugSettings.Mode);
                return GetDeferredLightingMaterial(
                    debugShaderName,
                    asset,
                    ref deferredLightingDebugMaterial,
                    ref hasLoggedMissingDebugShader);
            }

            var lightingMaterial = GetDeferredLightingMaterial(
                DeferredLightingShaderName,
                asset,
                ref deferredLightingMaterial,
                ref hasLoggedMissingShader);
            ConfigureDeferredLightingDebugKeyword(
                lightingMaterial,
                debugCategory == DeferredLightingDebugCategory.Lighting ||
                debugCategory == DeferredLightingDebugCategory.Brdf ||
                debugCategory == DeferredLightingDebugCategory.Transmission);
            return lightingMaterial;
        }

        private bool ShouldSkipDeferredLightingStage(BurtRenderGraphContext context)
        {
            if (isAdditionalLightingStage)
            {
                var lightingData = context != null && context.Request != null
                    ? context.Request.LightingData
                    : null;
                if (lightingData == null || lightingData.AdditionalLightCount <= 0)
                {
                    return true;
                }

                if (lightingData.PunctualTileDrawUploaded &&
                    lightingData.PunctualTileDrawHitTileCount <= 0)
                {
                    return true;
                }
            }

            if (!ShouldUseDeferredLightingDebug())
            {
                return false;
            }

            var debugCategory = ResolveDeferredLightingDebugCategory(BurtShadingDebugSettings.Mode);
            if (!isAdditionalLightingStage &&
                (debugCategory == DeferredLightingDebugCategory.Probe ||
                 debugCategory == DeferredLightingDebugCategory.Shadow))
            {
                // Probe and shadow diagnostics are single fullscreen views. They do
                // not need seven copies of the material-model lighting pass.
                return shaderPassIndex != 0;
            }

            var isAdditionalLightingDebugMode = IsAdditionalLightingDebugMode(BurtShadingDebugSettings.Mode);
            return isAdditionalLightingStage
                ? !ShouldRunAdditionalLightingDebugStage(BurtShadingDebugSettings.Mode)
                : isAdditionalLightingDebugMode;
        }

        private static bool ShouldRunAdditionalLightingDebugStage(BurtShadingDebugMode mode)
        {
            switch (mode)
            {
                case BurtShadingDebugMode.DetailLighting:
                case BurtShadingDebugMode.DirectDiffuse:
                case BurtShadingDebugMode.DirectSpecular:
                case BurtShadingDebugMode.FinalLighting:
                    return true;
                default:
                    return IsAdditionalLightingDebugMode(mode);
            }
        }

        private static bool IsAdditionalLightingDebugMode(BurtShadingDebugMode mode)
        {
            switch (mode)
            {
                case BurtShadingDebugMode.AdditionalLighting:
                case BurtShadingDebugMode.AdditionalDiffuse:
                case BurtShadingDebugMode.AdditionalSpecular:
                case BurtShadingDebugMode.HairAdditionalLighting:
                case BurtShadingDebugMode.AdditionalLightingUnshadowed:
                    return true;
                default:
                    return false;
            }
        }

        private static bool ShouldUseDeferredLightingDebug()
        {
            if (!BurtShadingDebugSettings.IsDebugging)
            {
                return false;
            }

            if (BurtGBufferDebugViewUtility.ResolveGBufferDebugViewMode(BurtShadingDebugSettings.Mode) != BurtGBufferDebugViewMode.Disabled)
            {
                return false;
            }

            return ResolveDeferredLightingDebugCategory(BurtShadingDebugSettings.Mode) != DeferredLightingDebugCategory.None;
        }

        private static void ConfigureDeferredLightingDebugKeyword(Material material, bool enabled)
        {
            if (material == null)
            {
                return;
            }

            CoreUtils.SetKeyword(material, DeferredLightingDebugKeyword, enabled);
        }

        private void ConfigureDeferredGIProbeEvaluateKeyword(BurtRenderGraphContext context, Material material)
        {
            if (isAdditionalLightingStage || material == null || material.shader == null || material.shader.name != DeferredLightingShaderName)
            {
                return;
            }

            var camera = context != null && context.Request != null ? context.Request.Camera : null;
            var hasProbeVolume = BurtGIProbeVolume.TryGetBestForCamera(camera, out var probeVolume) &&
                (probeVolume.IsVirtualReady || probeVolume.IsDirectIrradianceReady);
            var hasSceneVoxelProbe = BurtGISceneVoxelClipmapStateUtility.HasProbeApplyResources(camera);
            var useHybridProbe = hasProbeVolume && hasSceneVoxelProbe;
            CoreUtils.SetKeyword(material, DeferredGIProbeVolumeEvaluateKeyword, hasProbeVolume && !hasSceneVoxelProbe);
            CoreUtils.SetKeyword(material, DeferredGISceneVoxelProbeEvaluateKeyword, !hasProbeVolume && hasSceneVoxelProbe);
            CoreUtils.SetKeyword(material, DeferredGIProbeHybridEvaluateKeyword, useHybridProbe);
        }

        private static string ResolveDeferredLightingDebugShaderName(BurtShadingDebugMode mode)
        {
            switch (ResolveDeferredLightingDebugCategory(mode))
            {
                case DeferredLightingDebugCategory.Lighting:
                case DeferredLightingDebugCategory.Brdf:
                case DeferredLightingDebugCategory.Transmission:
                    return DeferredLightingShaderName;
                case DeferredLightingDebugCategory.Probe:
                    return DeferredLightingDebugProbeShaderName;
                case DeferredLightingDebugCategory.Shadow:
                    return DeferredLightingDebugShadowShaderName;
                default:
                    return DeferredLightingShaderName;
            }
        }

        private static DeferredLightingDebugCategory ResolveDeferredLightingDebugCategory(BurtShadingDebugMode mode)
        {
            switch (mode)
            {
                case BurtShadingDebugMode.Height:
                case BurtShadingDebugMode.SpecularAARoughness:
                case BurtShadingDebugMode.SpecularEnergyCompensation:
                case BurtShadingDebugMode.SpecularOcclusion:
                case BurtShadingDebugMode.EnergyPreservation:
                case BurtShadingDebugMode.IndirectSpecularEnergyCompensation:
                case BurtShadingDebugMode.DirectBRDFD:
                case BurtShadingDebugMode.DirectBRDFVisibility:
                case BurtShadingDebugMode.DirectBRDFFresnel:
                case BurtShadingDebugMode.DirectDiffuseLobe:
                case BurtShadingDebugMode.DirectDiffuseBRDF:
                case BurtShadingDebugMode.DirectSpecularBRDF:
                case BurtShadingDebugMode.SpecularAANormalVariance:
                case BurtShadingDebugMode.SpecularAARoughnessDelta:
                case BurtShadingDebugMode.IndirectSpecularDFG:
                case BurtShadingDebugMode.IndirectSpecularEnvBRDF:
                    return DeferredLightingDebugCategory.Brdf;

                case BurtShadingDebugMode.SubsurfaceProfileId:
                case BurtShadingDebugMode.SubsurfaceTransmission:
                case BurtShadingDebugMode.SubsurfaceKernelWeight:
                case BurtShadingDebugMode.SubsurfaceIndirect:
                case BurtShadingDebugMode.SubsurfaceDirectTransmission:
                case BurtShadingDebugMode.SubsurfaceTransmissionBRDF:
                case BurtShadingDebugMode.SubsurfaceTransmissionShadow:
                case BurtShadingDebugMode.SubsurfaceTransmissionPhase:
                case BurtShadingDebugMode.SubsurfaceTransmissionThickness:
                case BurtShadingDebugMode.FoliageTransmission:
                case BurtShadingDebugMode.FoliageDirectTransmission:
                case BurtShadingDebugMode.FoliageTransmissionBRDF:
                case BurtShadingDebugMode.FoliageTransmissionShadow:
                case BurtShadingDebugMode.FoliageSpecularBRDF:
                case BurtShadingDebugMode.GrassTransmission:
                case BurtShadingDebugMode.GrassDirectTransmission:
                case BurtShadingDebugMode.GrassTransmissionBRDF:
                case BurtShadingDebugMode.GrassTransmissionShadow:
                case BurtShadingDebugMode.GrassSpecularBRDF:
                case BurtShadingDebugMode.HairPrimaryLobe:
                case BurtShadingDebugMode.HairSecondaryLobe:
                case BurtShadingDebugMode.HairTransmissionLobe:
                case BurtShadingDebugMode.HairScatter:
                    return DeferredLightingDebugCategory.Transmission;

                case BurtShadingDebugMode.ShadowCascadeIndex:
                case BurtShadingDebugMode.ShadowCascadeBlend:
                case BurtShadingDebugMode.ShadowDistanceFade:
                case BurtShadingDebugMode.ShadowPCSSRadius:
                case BurtShadingDebugMode.ShadowReceiverDepthDelta:
                case BurtShadingDebugMode.ShadowPCSSBlockerFraction:
                case BurtShadingDebugMode.AdditionalShadowAttenuation:
                case BurtShadingDebugMode.AdditionalShadowFace:
                case BurtShadingDebugMode.AdditionalShadowUV:
                case BurtShadingDebugMode.AdditionalShadowDepth:
                case BurtShadingDebugMode.AdditionalShadowDepthDelta:
                case BurtShadingDebugMode.MainLightShadowReceiverDepth:
                case BurtShadingDebugMode.MainLightShadowRawDepth:
                case BurtShadingDebugMode.MainLightShadowCompare:
                case BurtShadingDebugMode.MainLightShadowProjectionValidity:
                case BurtShadingDebugMode.PerObjectShadowObjectIndex:
                case BurtShadingDebugMode.PerObjectShadowSlice:
                case BurtShadingDebugMode.PerObjectShadowUV:
                case BurtShadingDebugMode.PerObjectShadowDepth:
                case BurtShadingDebugMode.PerObjectShadowCompare:
                case BurtShadingDebugMode.PerObjectShadowTransmissionDepth:
                case BurtShadingDebugMode.PerObjectShadowTransmissionThickness:
                    return DeferredLightingDebugCategory.Shadow;

                case BurtShadingDebugMode.DetailLighting:
                case BurtShadingDebugMode.ShadowAttenuation:
                case BurtShadingDebugMode.IndirectLighting:
                case BurtShadingDebugMode.DirectDiffuse:
                case BurtShadingDebugMode.DirectSpecular:
                case BurtShadingDebugMode.IndirectDiffuse:
                case BurtShadingDebugMode.IndirectSpecular:
                case BurtShadingDebugMode.AmbientOcclusion:
                case BurtShadingDebugMode.Emission:
                case BurtShadingDebugMode.AdditionalLighting:
                case BurtShadingDebugMode.AdditionalDiffuse:
                case BurtShadingDebugMode.AdditionalSpecular:
                case BurtShadingDebugMode.HairAdditionalLighting:
                case BurtShadingDebugMode.AdditionalLightingUnshadowed:
                case BurtShadingDebugMode.FinalLighting:
                    return DeferredLightingDebugCategory.Lighting;

                case BurtShadingDebugMode.GIProbeIrradiance:
                case BurtShadingDebugMode.GIProbeValidity:
                case BurtShadingDebugMode.GIProbeSkyVisibility:
                    return DeferredLightingDebugCategory.Probe;

                default:
                    return DeferredLightingDebugCategory.None;
            }
        }

        private static Material GetDeferredLightingMaterial(
            string shaderName,
            BurtRenderPipelineAsset asset,
            ref Material material,
            ref bool hasLoggedMissingShader)
        {
            if (material != null)
            {
                if (material.shader != null && material.shader.name == shaderName)
                {
                    BurtShadingModelIds.ApplyDeferredLightingStencilProperties(material);
                    return material;
                }

                CoreUtils.Destroy(material);
                material = null;
            }

            var shader = asset != null && asset.RuntimeResources != null
                ? asset.RuntimeResources.ResolveDeferredShadingDebugShader(shaderName)
                : null;
            if (shader == null)
            {
                shader = Shader.Find(shaderName);
            }
            if (shader == null)
            {
                if (!hasLoggedMissingShader)
                {
                    Debug.LogWarning("BurtRP could not find shader: " + shaderName);
                    hasLoggedMissingShader = true;
                }

                return null;
            }

            material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            BurtShadingModelIds.ApplyDeferredLightingStencilProperties(material);
            return material;
        }

        private bool HasRequiredShaderPass(Material material)
        {
            if (material != null && shaderPassIndex >= 0 && shaderPassIndex < material.passCount)
            {
                return true;
            }

            if (!hasLoggedMissingShaderPass)
            {
                var fallbackShaderName = isAdditionalLightingStage
                    ? DeferredAdditionalLightingShaderName
                    : DeferredLightingShaderName;
                var shaderName = material != null && material.shader != null ? material.shader.name : fallbackShaderName;
                Debug.LogWarning("BurtRP deferred lighting shader pass missing: " + shaderName + " pass " + shaderPassIndex + " for " + Name);
                hasLoggedMissingShaderPass = true;
            }

            return false;
        }

        private void UploadAdditionalLightShadowReceiverGlobals(BurtRenderGraphContext context, CommandBuffer cmd, Material material)
        {
            if (cmd == null || material == null)
            {
                return;
            }

            if (context == null || !BurtAdditionalLightShadowUtility.ShouldUseAdditionalLightShadows(context.Request))
            {
                BurtAdditionalLightShadowUtility.ClearAdditionalLightShadowReceiverGlobals(cmd, material);
                return;
            }

            var atlasTarget = context.AdditionalLightShadowAtlasTarget;
            var lightingData = context.Request != null ? context.Request.LightingData : null;
            BurtAdditionalLightShadowUtility.UploadAdditionalLightShadowReceiverGlobals(cmd, material, atlasTarget, lightingData);
        }

        private void UploadPerObjectShadowReceiverGlobals(BurtRenderGraphContext context, CommandBuffer cmd, Material material)
        {
            if (cmd == null || material == null)
            {
                return;
            }

            if (context == null || !BurtPerObjectShadowUtility.ShouldUsePerObjectShadow(context.Request, context.Asset))
            {
                BurtPerObjectShadowUtility.ClearPerObjectShadowReceiverGlobals(cmd, material);
                return;
            }

            var atlasTarget = context.PerObjectShadowAtlasTarget;
            var preparedData = BurtPerObjectShadowUtility.Prepare(context.Request);
            BurtPerObjectShadowUtility.UploadPerObjectShadowReceiverGlobals(cmd, material, atlasTarget, preparedData);
        }
    }

    internal sealed class BurtDeferredLitLightingPass : BurtDeferredLightingPass
    {
        public BurtDeferredLitLightingPass(bool additionalLightingStage = false)
            : base(
                additionalLightingStage ? "Burt Deferred Lit Additional Lighting" : "Burt Deferred Lit Lighting",
                0,
                additionalLightingStage,
                additionalLightingStage)
        {
        }
    }

    internal sealed class BurtDeferredHairLightingPass : BurtDeferredLightingPass
    {
        public BurtDeferredHairLightingPass(bool additionalLightingStage = false)
            : base(
                additionalLightingStage ? "Burt Deferred Hair Additional Lighting" : "Burt Deferred Hair Lighting",
                1,
                true,
                additionalLightingStage)
        {
        }
    }

    internal sealed class BurtDeferredClearCoatLightingPass : BurtDeferredLightingPass
    {
        public BurtDeferredClearCoatLightingPass(bool additionalLightingStage = false)
            : base(
                additionalLightingStage ? "Burt Deferred Clear Coat Additional Lighting" : "Burt Deferred Clear Coat Lighting",
                2,
                true,
                additionalLightingStage)
        {
        }
    }

    internal sealed class BurtDeferredSubsurfaceLightingPass : BurtDeferredLightingPass
    {
        public BurtDeferredSubsurfaceLightingPass(bool additionalLightingStage = false)
            : base(
                additionalLightingStage ? "Burt Deferred Subsurface Additional Lighting" : "Burt Deferred Subsurface Lighting",
                3,
                true,
                additionalLightingStage)
        {
        }
    }

    internal sealed class BurtDeferredFabricLightingPass : BurtDeferredLightingPass
    {
        public BurtDeferredFabricLightingPass(bool additionalLightingStage = false)
            : base(
                additionalLightingStage ? "Burt Deferred Fabric Additional Lighting" : "Burt Deferred Fabric Lighting",
                4,
                true,
                additionalLightingStage)
        {
        }
    }

    internal sealed class BurtDeferredFoliageLightingPass : BurtDeferredLightingPass
    {
        public BurtDeferredFoliageLightingPass(bool additionalLightingStage = false)
            : base(
                additionalLightingStage ? "Burt Deferred Foliage Additional Lighting" : "Burt Deferred Foliage Lighting",
                5,
                true,
                additionalLightingStage)
        {
        }
    }

    internal sealed class BurtDeferredFurLightingPass : BurtDeferredLightingPass
    {
        public BurtDeferredFurLightingPass(bool additionalLightingStage = false)
            : base(
                additionalLightingStage ? "Burt Deferred Fur Additional Lighting" : "Burt Deferred Fur Lighting",
                6,
                true,
                additionalLightingStage)
        {
        }
    }
}
