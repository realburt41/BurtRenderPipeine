using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    internal sealed class BurtAllocateScreenSpaceAmbientOcclusionRawPass : BurtRenderPass
    {
        public override string Name => "Burt Allocate Screen Space Ambient Occlusion Raw";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceAmbientOcclusionPassUtility.ShouldUseScreenSpaceAmbientOcclusion(builder.Request, builder.Asset))
            {
                return;
            }

            builder.WriteScreenSpaceAmbientOcclusionRaw();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceAmbientOcclusionPassUtility.ShouldUseScreenSpaceAmbientOcclusion(context.Request, context.Asset))
            {
                return;
            }

            var target = context.ScreenSpaceAmbientOcclusionRawTarget;
            if (!target.IsValid)
            {
                return;
            }

            var camera = context.Request != null ? context.Request.Camera : null;
            var descriptor = BurtRenderTargetDescriptorUtility.CreateScreenSpaceAmbientOcclusionDescriptor(camera);
            BurtScreenSpaceAmbientOcclusionRenderTargetUtility.Allocate(context, Name, BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionRawTextureId, target, descriptor, FilterMode.Bilinear);
        }
    }

    internal sealed class BurtAllocateScreenSpaceAmbientOcclusionPass : BurtRenderPass
    {
        public override string Name => "Burt Allocate Screen Space Ambient Occlusion";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceAmbientOcclusionPassUtility.ShouldUseScreenSpaceAmbientOcclusion(builder.Request, builder.Asset))
            {
                return;
            }

            builder.WriteScreenSpaceAmbientOcclusion();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceAmbientOcclusionPassUtility.ShouldUseScreenSpaceAmbientOcclusion(context.Request, context.Asset))
            {
                return;
            }

            var target = context.ScreenSpaceAmbientOcclusionTarget;
            if (!target.IsValid)
            {
                return;
            }

            var camera = context.Request != null ? context.Request.Camera : null;
            var descriptor = BurtRenderTargetDescriptorUtility.CreateScreenSpaceAmbientOcclusionDescriptor(camera);
            BurtScreenSpaceAmbientOcclusionRenderTargetUtility.Allocate(context, Name, BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionTextureId, target, descriptor, FilterMode.Bilinear);
        }
    }

    internal sealed class BurtScreenSpaceAmbientOcclusionTracePass : BurtRenderPass
    {
        private const string ScreenSpaceAmbientOcclusionShaderName = "Hidden/BurtRP/ScreenSpaceAmbientOcclusion";
        private const int TracePassIndex = 0;
        private const int DownsampleDepthNormalPassIndex = 4;
        private const int HalfTracePassIndex = 5;
        private const int UpsampleRawPassIndex = 6;
        private static readonly int CameraDepthTextureId = BurtRenderGraphResourceRegistry.CameraDepthTextureId;
        private static readonly int GBuffer0Id = BurtRenderGraphResourceRegistry.GBuffer0Id;
        private static readonly int AORawTextureId = BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionRawTextureId;
        private static readonly int HalfDepthNormalTextureId = Shader.PropertyToID("_BurtSSAOHalfDepthNormalTexture");
        private static readonly int HalfAmbientOcclusionTextureId = Shader.PropertyToID("_BurtSSAOHalfAmbientOcclusionTexture");
        private static readonly int ViewProjectionMatrixId = Shader.PropertyToID("_BurtSSAOViewProjectionMatrix");
        private static readonly int InverseViewProjectionMatrixId = Shader.PropertyToID("_BurtDeferredInverseViewProjectionMatrix");
        private static readonly int CameraWorldPositionId = Shader.PropertyToID("_BurtDeferredCameraWorldPosition");
        private static readonly int DeferredScreenSizeId = Shader.PropertyToID("_BurtDeferredScreenSize");
        private static readonly int SSAOFullScreenSizeId = Shader.PropertyToID("_BurtSSAOFullScreenSize");
        private static readonly int SSAOHalfScreenSizeId = Shader.PropertyToID("_BurtSSAOHalfScreenSize");
        private static readonly int SSAOParams0Id = Shader.PropertyToID("_BurtSSAOParams0");
        private static readonly int SSAOParams1Id = Shader.PropertyToID("_BurtSSAOParams1");
        private static readonly int SSAOParams2Id = Shader.PropertyToID("_BurtSSAOParams2");
        private static readonly int SSAOParams3Id = Shader.PropertyToID("_BurtSSAOParams3");
        private static readonly int SSAOParams4Id = Shader.PropertyToID("_BurtSSAOParams4");

        private Material screenSpaceAmbientOcclusionMaterial;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Screen Space Ambient Occlusion Trace";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceAmbientOcclusionPassUtility.ShouldUseScreenSpaceAmbientOcclusion(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadCameraDepth();
            builder.ReadGBuffer0();
            builder.WriteScreenSpaceAmbientOcclusionRaw();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!TryGetTargets(context, out var cameraDepthTarget, out var gbuffer0Target, out var aoRawTarget))
            {
                return;
            }

            var settings = BurtScreenSpaceAmbientOcclusionPassUtility.ResolveScreenSpaceAmbientOcclusionSettings(context.Request, context.Asset);
            if (!settings.Enabled)
            {
                return;
            }

            var material = GetScreenSpaceAmbientOcclusionMaterial();
            if (material == null)
            {
                return;
            }

            var camera = context.Request != null ? context.Request.Camera : null;
            if (camera == null)
            {
                return;
            }

            var descriptor = BurtRenderTargetDescriptorUtility.CreateScreenSpaceAmbientOcclusionDescriptor(camera);
            var cmd = context.AcquireCommandBuffer(Name);
            cmd.SetGlobalTexture(CameraDepthTextureId, cameraDepthTarget.Identifier);
            cmd.SetGlobalTexture(GBuffer0Id, gbuffer0Target.Identifier);
            UploadCameraGlobals(cmd, camera, descriptor);
            UploadSettings(cmd, settings, camera, descriptor);
            if (settings.HalfResolution)
            {
                DrawHalfResolutionTrace(cmd, material, camera, descriptor, aoRawTarget.Identifier);
            }
            else
            {
                cmd.SetRenderTarget(aoRawTarget.Identifier);
                BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
                cmd.DrawProcedural(Matrix4x4.identity, material, TracePassIndex, MeshTopology.Triangles, 3, 1);
            }

            cmd.SetGlobalTexture(AORawTextureId, aoRawTarget.Identifier);
            context.ExecuteAndReleaseCommandBuffer(cmd);
        }

        private static void DrawHalfResolutionTrace(
            CommandBuffer cmd,
            Material material,
            Camera camera,
            RenderTextureDescriptor fullDescriptor,
            RenderTargetIdentifier fullResolutionRawTarget)
        {
            var halfDepthNormalDescriptor = CreateHalfDepthNormalDescriptor(fullDescriptor);
            var halfAmbientOcclusionDescriptor = CreateHalfAmbientOcclusionDescriptor(fullDescriptor);
            var halfDepthNormalIdentifier = new RenderTargetIdentifier(HalfDepthNormalTextureId);
            var halfAmbientOcclusionIdentifier = new RenderTargetIdentifier(HalfAmbientOcclusionTextureId);

            cmd.GetTemporaryRT(HalfDepthNormalTextureId, halfDepthNormalDescriptor, FilterMode.Point);
            cmd.GetTemporaryRT(HalfAmbientOcclusionTextureId, halfAmbientOcclusionDescriptor, FilterMode.Bilinear);
            cmd.SetGlobalVector(SSAOFullScreenSizeId, CreateScreenSizeVector(fullDescriptor));
            cmd.SetGlobalVector(SSAOHalfScreenSizeId, CreateScreenSizeVector(halfDepthNormalDescriptor));

            DrawProceduralToTarget(cmd, material, halfDepthNormalIdentifier, halfDepthNormalDescriptor, DownsampleDepthNormalPassIndex);
            cmd.SetGlobalTexture(HalfDepthNormalTextureId, halfDepthNormalIdentifier);

            DrawProceduralToTarget(cmd, material, halfAmbientOcclusionIdentifier, halfAmbientOcclusionDescriptor, HalfTracePassIndex);
            cmd.SetGlobalTexture(HalfAmbientOcclusionTextureId, halfAmbientOcclusionIdentifier);

            DrawProceduralToTarget(cmd, material, fullResolutionRawTarget, fullDescriptor, UpsampleRawPassIndex);
            cmd.ReleaseTemporaryRT(HalfAmbientOcclusionTextureId);
            cmd.ReleaseTemporaryRT(HalfDepthNormalTextureId);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
        }

        private static void DrawProceduralToTarget(
            CommandBuffer cmd,
            Material material,
            RenderTargetIdentifier target,
            RenderTextureDescriptor descriptor,
            int passIndex)
        {
            cmd.SetRenderTarget(target);
            cmd.SetViewport(new Rect(0f, 0f, Mathf.Max(1, descriptor.width), Mathf.Max(1, descriptor.height)));
            cmd.DrawProcedural(Matrix4x4.identity, material, passIndex, MeshTopology.Triangles, 3, 1);
        }

        private static RenderTextureDescriptor CreateHalfDepthNormalDescriptor(RenderTextureDescriptor fullDescriptor)
        {
            var descriptor = CreateHalfDescriptor(fullDescriptor);
            descriptor.colorFormat = RenderTextureFormat.ARGBHalf;
            return descriptor;
        }

        private static RenderTextureDescriptor CreateHalfAmbientOcclusionDescriptor(RenderTextureDescriptor fullDescriptor)
        {
            var descriptor = CreateHalfDescriptor(fullDescriptor);
            descriptor.colorFormat = RenderTextureFormat.R8;
            return descriptor;
        }

        private static RenderTextureDescriptor CreateHalfDescriptor(RenderTextureDescriptor fullDescriptor)
        {
            var descriptor = fullDescriptor;
            descriptor.width = Mathf.Max(1, (fullDescriptor.width + 1) / 2);
            descriptor.height = Mathf.Max(1, (fullDescriptor.height + 1) / 2);
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            return descriptor;
        }

        private static Vector4 CreateScreenSizeVector(RenderTextureDescriptor descriptor)
        {
            var width = Mathf.Max(1, descriptor.width);
            var height = Mathf.Max(1, descriptor.height);
            return new Vector4(width, height, 1f / width, 1f / height);
        }

        private static bool TryGetTargets(
            BurtRenderGraphContext context,
            out BurtRenderTargetHandle cameraDepthTarget,
            out BurtRenderTargetHandle gbuffer0Target,
            out BurtRenderTargetHandle aoRawTarget)
        {
            cameraDepthTarget = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            gbuffer0Target = context != null ? context.GBuffer0Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer0Name);
            aoRawTarget = context != null ? context.ScreenSpaceAmbientOcclusionRawTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionRawName);
            return cameraDepthTarget.IsValid && gbuffer0Target.IsValid && aoRawTarget.IsValid;
        }

        private static void UploadCameraGlobals(CommandBuffer cmd, Camera camera, RenderTextureDescriptor descriptor)
        {
            var viewMatrix = camera.worldToCameraMatrix;
            var projectionMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
            var viewProjectionMatrix = projectionMatrix * viewMatrix;
            var inverseViewProjectionMatrix = viewProjectionMatrix.inverse;
            var width = Mathf.Max(1, descriptor.width);
            var height = Mathf.Max(1, descriptor.height);
            var cameraPosition = camera.transform.position;

            cmd.SetGlobalMatrix(ViewProjectionMatrixId, viewProjectionMatrix);
            cmd.SetGlobalMatrix(InverseViewProjectionMatrixId, inverseViewProjectionMatrix);
            cmd.SetGlobalVector(CameraWorldPositionId, new Vector4(cameraPosition.x, cameraPosition.y, cameraPosition.z, 1f));
            var fullScreenSize = new Vector4(width, height, 1f / width, 1f / height);
            cmd.SetGlobalVector(DeferredScreenSizeId, fullScreenSize);
            cmd.SetGlobalVector(SSAOFullScreenSizeId, fullScreenSize);
        }

        private static void UploadSettings(
            CommandBuffer cmd,
            BurtScreenSpaceAmbientOcclusionSettings settings,
            Camera camera,
            RenderTextureDescriptor descriptor)
        {
            cmd.SetGlobalVector(SSAOParams0Id, new Vector4(settings.Radius, settings.Intensity, settings.SampleCount, settings.Bias));
            cmd.SetGlobalVector(SSAOParams1Id, new Vector4(settings.Power, settings.Blur ? 1f : 0f, Time.frameCount & 1023, 0f));
            cmd.SetGlobalVector(SSAOParams2Id, new Vector4(settings.FadeDistance, settings.FadeRadius, settings.Thickness, CalculateProjectionRadiusScale(camera, descriptor)));
            cmd.SetGlobalVector(SSAOParams3Id, new Vector4(settings.HorizonSearch ? 1f : 0f, settings.DirectionCount, settings.BlurSharpness, settings.SpatialDenoise ? 1f : 0f));
            cmd.SetGlobalVector(SSAOParams4Id, new Vector4((float)settings.Algorithm, settings.GTAOStrength, settings.HBAOStrength, 0f));
        }

        private static float CalculateProjectionRadiusScale(Camera camera, RenderTextureDescriptor descriptor)
        {
            var height = Mathf.Max(1, descriptor.height);
            var projection = camera != null ? camera.projectionMatrix : Matrix4x4.identity;
            return Mathf.Max(1f, Mathf.Abs(projection.m11) * height * 0.5f);
        }

        private Material GetScreenSpaceAmbientOcclusionMaterial()
        {
            if (screenSpaceAmbientOcclusionMaterial != null)
            {
                return screenSpaceAmbientOcclusionMaterial;
            }

            var shader = Shader.Find(ScreenSpaceAmbientOcclusionShaderName);
            if (shader == null)
            {
                if (!hasLoggedMissingShader)
                {
                    Debug.LogWarning("BurtRP could not find shader: " + ScreenSpaceAmbientOcclusionShaderName);
                    hasLoggedMissingShader = true;
                }

                return null;
            }

            screenSpaceAmbientOcclusionMaterial = new Material(shader);
            screenSpaceAmbientOcclusionMaterial.hideFlags = HideFlags.HideAndDontSave;
            return screenSpaceAmbientOcclusionMaterial;
        }
    }

    internal sealed class BurtScreenSpaceAmbientOcclusionBlurPass : BurtRenderPass
    {
        private const string ScreenSpaceAmbientOcclusionShaderName = "Hidden/BurtRP/ScreenSpaceAmbientOcclusion";
        private const int BlurPassIndex = 1;
        private const int TemporalPassIndex = 7;
        private const int CopyCurrentDepthPassIndex = 8;
        private const int CopyTemporalFinalPassIndex = 10;
        private static readonly int CameraDepthTextureId = BurtRenderGraphResourceRegistry.CameraDepthTextureId;
        private static readonly int GBuffer0Id = BurtRenderGraphResourceRegistry.GBuffer0Id;
        private static readonly int AORawTextureId = BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionRawTextureId;
        private static readonly int AOTextureId = BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionTextureId;
        private static readonly int AOBlurTempTextureId = Shader.PropertyToID("_BurtScreenSpaceAmbientOcclusionBlurTempTexture");
        private static readonly int AOSpatialFinalTextureId = Shader.PropertyToID("_BurtScreenSpaceAmbientOcclusionSpatialFinalTexture");
        private static readonly int AOSpatialFinalInputTextureId = Shader.PropertyToID("_BurtSSAOSpatialFinalTexture");
        private static readonly int AOTemporalFinalTextureId = Shader.PropertyToID("_BurtScreenSpaceAmbientOcclusionTemporalFinalTexture");
        private static readonly int AOTemporalFinalInputTextureId = Shader.PropertyToID("_BurtSSAOTemporalFinalTexture");
        private static readonly int AOCurrentDepthTextureId = Shader.PropertyToID("_BurtScreenSpaceAmbientOcclusionCurrentDepthTexture");
        private static readonly int AOBlurSourceTextureId = Shader.PropertyToID("_BurtSSAOBlurSourceTexture");
        private static readonly int AOBlurDirectionId = Shader.PropertyToID("_BurtSSAOBlurDirection");
        private static readonly int DeferredScreenSizeId = Shader.PropertyToID("_BurtDeferredScreenSize");
        private static readonly int ViewProjectionMatrixId = Shader.PropertyToID("_BurtSSAOViewProjectionMatrix");
        private static readonly int PreviousViewProjectionMatrixId = Shader.PropertyToID("_BurtSSAOPreviousViewProjectionMatrix");
        private static readonly int InverseViewProjectionMatrixId = Shader.PropertyToID("_BurtDeferredInverseViewProjectionMatrix");
        private static readonly int SSAOFullScreenSizeId = Shader.PropertyToID("_BurtSSAOFullScreenSize");
        private static readonly int SSAOParams0Id = Shader.PropertyToID("_BurtSSAOParams0");
        private static readonly int SSAOParams1Id = Shader.PropertyToID("_BurtSSAOParams1");
        private static readonly int SSAOParams2Id = Shader.PropertyToID("_BurtSSAOParams2");
        private static readonly int SSAOParams3Id = Shader.PropertyToID("_BurtSSAOParams3");
        private static readonly int SSAOParams4Id = Shader.PropertyToID("_BurtSSAOParams4");
        private static readonly int SSAOTemporalParamsId = Shader.PropertyToID("_BurtSSAOTemporalParams");
        private static readonly int SSAOHistoryTextureId = Shader.PropertyToID("_BurtSSAOHistoryTexture");
        private static readonly int SSAOHistoryDepthTextureId = Shader.PropertyToID("_BurtSSAOHistoryDepthTexture");
        private static readonly int SSAOPreviousHistoryTextureId = Shader.PropertyToID("_BurtSSAOPreviousHistoryTexture");
        private static readonly int SSAOPreviousHistoryDepthTextureId = Shader.PropertyToID("_BurtSSAOPreviousHistoryDepthTexture");
        private static readonly int SSAOEnabledId = Shader.PropertyToID("_BurtScreenSpaceAmbientOcclusionEnabled");

        private Material screenSpaceAmbientOcclusionMaterial;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Screen Space Ambient Occlusion Blur";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceAmbientOcclusionPassUtility.ShouldUseScreenSpaceAmbientOcclusion(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceAmbientOcclusionRaw();
            builder.ReadCameraDepth();
            builder.ReadGBuffer0();
            builder.WriteScreenSpaceAmbientOcclusion();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!TryGetTargets(context, out var cameraDepthTarget, out var gbuffer0Target, out var aoRawTarget, out var aoTarget))
            {
                return;
            }

            var settings = BurtScreenSpaceAmbientOcclusionPassUtility.ResolveScreenSpaceAmbientOcclusionSettings(context.Request, context.Asset);
            if (!settings.Enabled)
            {
                return;
            }

            var material = GetScreenSpaceAmbientOcclusionMaterial();
            if (material == null)
            {
                return;
            }

            var camera = context.Request != null ? context.Request.Camera : null;
            if (camera == null)
            {
                return;
            }

            var descriptor = BurtRenderTargetDescriptorUtility.CreateScreenSpaceAmbientOcclusionDescriptor(camera);
            var blurTempIdentifier = new RenderTargetIdentifier(AOBlurTempTextureId);
            var spatialFinalIdentifier = new RenderTargetIdentifier(AOSpatialFinalTextureId);
            var temporalFinalIdentifier = new RenderTargetIdentifier(AOTemporalFinalTextureId);
            var currentDepthIdentifier = new RenderTargetIdentifier(AOCurrentDepthTextureId);
            var cmd = context.AcquireCommandBuffer(Name);
            cmd.SetGlobalTexture(CameraDepthTextureId, cameraDepthTarget.Identifier);
            cmd.SetGlobalTexture(GBuffer0Id, gbuffer0Target.Identifier);
            cmd.SetGlobalTexture(AORawTextureId, aoRawTarget.Identifier);
            UploadResolveGlobals(cmd, context.Request, settings, camera, descriptor);
            var outputTarget = aoTarget.Identifier;
            var shouldUseTemporal = settings.TemporalAccumulation;
            if (shouldUseTemporal)
            {
                cmd.GetTemporaryRT(AOSpatialFinalTextureId, descriptor, FilterMode.Bilinear);
                cmd.GetTemporaryRT(AOTemporalFinalTextureId, BurtScreenSpaceAmbientOcclusionHistoryUtility.CreateColorHistoryDescriptor(camera), FilterMode.Bilinear);
                outputTarget = spatialFinalIdentifier;
            }
            else
            {
                BurtScreenSpaceAmbientOcclusionHistoryUtility.InvalidateHistory(camera, "TemporalDisabled");
            }

            if (settings.Blur)
            {
                cmd.GetTemporaryRT(AOBlurTempTextureId, descriptor, FilterMode.Bilinear);
                DrawBlur(cmd, material, aoRawTarget.Identifier, blurTempIdentifier, camera, Vector2.right, false);
                DrawBlur(cmd, material, blurTempIdentifier, outputTarget, camera, Vector2.up, true);
                cmd.ReleaseTemporaryRT(AOBlurTempTextureId);
            }
            else
            {
                DrawBlur(cmd, material, aoRawTarget.Identifier, outputTarget, camera, Vector2.zero, true);
            }

            if (shouldUseTemporal)
            {
                ResolveTemporal(cmd, material, context.Request, context.Asset, settings, camera, descriptor, spatialFinalIdentifier, temporalFinalIdentifier, aoTarget.Identifier, currentDepthIdentifier);
                cmd.ReleaseTemporaryRT(AOTemporalFinalTextureId);
                cmd.ReleaseTemporaryRT(AOSpatialFinalTextureId);
            }

            cmd.SetGlobalTexture(AOTextureId, aoTarget.Identifier);
            cmd.SetGlobalFloat(SSAOEnabledId, 1f);
            context.ExecuteAndReleaseCommandBuffer(cmd);
        }

        private static void DrawBlur(
            CommandBuffer cmd,
            Material material,
            RenderTargetIdentifier source,
            RenderTargetIdentifier destination,
            Camera camera,
            Vector2 direction,
            bool resolveFinal)
        {
            cmd.SetRenderTarget(destination);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            cmd.SetGlobalTexture(AOBlurSourceTextureId, source);
            cmd.SetGlobalVector(AOBlurDirectionId, new Vector4(direction.x, direction.y, resolveFinal ? 1f : 0f, 0f));
            cmd.DrawProcedural(Matrix4x4.identity, material, BlurPassIndex, MeshTopology.Triangles, 3, 1);
        }

        private static void ResolveTemporal(
            CommandBuffer cmd,
            Material material,
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset,
            BurtScreenSpaceAmbientOcclusionSettings settings,
            Camera camera,
            RenderTextureDescriptor descriptor,
            RenderTargetIdentifier spatialFinal,
            RenderTargetIdentifier temporalFinalTarget,
            RenderTargetIdentifier finalTarget,
            RenderTargetIdentifier currentDepthTarget)
        {
            var history = BurtScreenSpaceAmbientOcclusionHistoryUtility.EnsureHistoryTextures(request, asset, settings, out var historyValid);
            var width = Mathf.Max(1, descriptor.width);
            var height = Mathf.Max(1, descriptor.height);
            cmd.SetGlobalTexture(AOSpatialFinalInputTextureId, spatialFinal);
            cmd.SetGlobalTexture(SSAOHistoryTextureId, history.Color != null ? (Texture)history.Color : Texture2D.whiteTexture);
            cmd.SetGlobalTexture(SSAOHistoryDepthTextureId, history.Depth != null ? (Texture)history.Depth : Texture2D.blackTexture);
            cmd.SetGlobalTexture(SSAOPreviousHistoryTextureId, history.Color != null ? (Texture)history.Color : Texture2D.whiteTexture);
            cmd.SetGlobalTexture(SSAOPreviousHistoryDepthTextureId, history.Depth != null ? (Texture)history.Depth : Texture2D.blackTexture);
            cmd.SetGlobalMatrix(PreviousViewProjectionMatrixId, history.PreviousViewProjectionMatrix);
            cmd.SetGlobalMatrix(InverseViewProjectionMatrixId, history.CurrentInverseViewProjectionMatrix);
            cmd.SetGlobalVector(DeferredScreenSizeId, new Vector4(width, height, 1f / width, 1f / height));
            cmd.SetGlobalVector(SSAOFullScreenSizeId, new Vector4(width, height, 1f / width, 1f / height));
            cmd.SetGlobalVector(SSAOTemporalParamsId, new Vector4(settings.TemporalFeedback, historyValid ? 1f : 0f, settings.TemporalDepthRejection, settings.TemporalClamp));

            cmd.SetRenderTarget(temporalFinalTarget);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            cmd.DrawProcedural(Matrix4x4.identity, material, TemporalPassIndex, MeshTopology.Triangles, 3, 1);

            cmd.SetGlobalTexture(AOTemporalFinalInputTextureId, temporalFinalTarget);
            cmd.SetRenderTarget(finalTarget);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            cmd.DrawProcedural(Matrix4x4.identity, material, CopyTemporalFinalPassIndex, MeshTopology.Triangles, 3, 1);

            if (history.Color == null || history.Depth == null)
            {
                return;
            }

            BurtScreenSpaceAmbientOcclusionHistoryUtility.CopyHistoryToDebugSnapshot(cmd, camera);
            cmd.CopyTexture(temporalFinalTarget, new RenderTargetIdentifier(history.Color));
            var depthDescriptor = BurtScreenSpaceAmbientOcclusionHistoryUtility.CreateDepthHistoryDescriptor(camera);
            cmd.GetTemporaryRT(AOCurrentDepthTextureId, depthDescriptor, FilterMode.Point);
            cmd.SetRenderTarget(currentDepthTarget);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, width, height);
            cmd.DrawProcedural(Matrix4x4.identity, material, CopyCurrentDepthPassIndex, MeshTopology.Triangles, 3, 1);
            cmd.CopyTexture(currentDepthTarget, new RenderTargetIdentifier(history.Depth));
            cmd.ReleaseTemporaryRT(AOCurrentDepthTextureId);
            BurtScreenSpaceAmbientOcclusionHistoryUtility.MarkHistoryValid(camera);
        }

        private static void UploadResolveGlobals(
            CommandBuffer cmd,
            BurtRenderRequest request,
            BurtScreenSpaceAmbientOcclusionSettings settings,
            Camera camera,
            RenderTextureDescriptor descriptor)
        {
            var matrices = BurtScreenSpaceAmbientOcclusionHistoryUtility.CreateCurrentMatrices(request);
            var width = Mathf.Max(1, descriptor.width);
            var height = Mathf.Max(1, descriptor.height);
            cmd.SetGlobalMatrix(ViewProjectionMatrixId, matrices.ViewProjectionMatrix);
            cmd.SetGlobalMatrix(InverseViewProjectionMatrixId, matrices.InverseViewProjectionMatrix);
            cmd.SetGlobalVector(DeferredScreenSizeId, new Vector4(width, height, 1f / width, 1f / height));
            cmd.SetGlobalVector(SSAOFullScreenSizeId, new Vector4(width, height, 1f / width, 1f / height));
            cmd.SetGlobalVector(SSAOParams0Id, new Vector4(settings.Radius, settings.Intensity, settings.SampleCount, settings.Bias));
            cmd.SetGlobalVector(SSAOParams1Id, new Vector4(settings.Power, settings.Blur ? 1f : 0f, Time.frameCount & 1023, 0f));
            cmd.SetGlobalVector(SSAOParams2Id, new Vector4(settings.FadeDistance, settings.FadeRadius, settings.Thickness, 1f));
            cmd.SetGlobalVector(SSAOParams3Id, new Vector4(settings.HorizonSearch ? 1f : 0f, settings.DirectionCount, settings.BlurSharpness, settings.SpatialDenoise ? 1f : 0f));
            cmd.SetGlobalVector(SSAOParams4Id, new Vector4((float)settings.Algorithm, settings.GTAOStrength, settings.HBAOStrength, 0f));
        }

        private static bool TryGetTargets(
            BurtRenderGraphContext context,
            out BurtRenderTargetHandle cameraDepthTarget,
            out BurtRenderTargetHandle gbuffer0Target,
            out BurtRenderTargetHandle aoRawTarget,
            out BurtRenderTargetHandle aoTarget)
        {
            cameraDepthTarget = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            gbuffer0Target = context != null ? context.GBuffer0Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer0Name);
            aoRawTarget = context != null ? context.ScreenSpaceAmbientOcclusionRawTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionRawName);
            aoTarget = context != null ? context.ScreenSpaceAmbientOcclusionTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionName);
            return cameraDepthTarget.IsValid && gbuffer0Target.IsValid && aoRawTarget.IsValid && aoTarget.IsValid;
        }

        private Material GetScreenSpaceAmbientOcclusionMaterial()
        {
            if (screenSpaceAmbientOcclusionMaterial != null)
            {
                return screenSpaceAmbientOcclusionMaterial;
            }

            var shader = Shader.Find(ScreenSpaceAmbientOcclusionShaderName);
            if (shader == null)
            {
                if (!hasLoggedMissingShader)
                {
                    Debug.LogWarning("BurtRP could not find shader: " + ScreenSpaceAmbientOcclusionShaderName);
                    hasLoggedMissingShader = true;
                }

                return null;
            }

            screenSpaceAmbientOcclusionMaterial = new Material(shader);
            screenSpaceAmbientOcclusionMaterial.hideFlags = HideFlags.HideAndDontSave;
            return screenSpaceAmbientOcclusionMaterial;
        }
    }

    internal sealed class BurtDebugScreenSpaceAmbientOcclusionPass : BurtRenderPass
    {
        private const string ScreenSpaceAmbientOcclusionShaderName = "Hidden/BurtRP/ScreenSpaceAmbientOcclusion";
        private static readonly int CameraDepthTextureId = BurtRenderGraphResourceRegistry.CameraDepthTextureId;
        private static readonly int GBuffer0Id = BurtRenderGraphResourceRegistry.GBuffer0Id;
        private static readonly int AORawTextureId = BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionRawTextureId;
        private static readonly int AOTextureId = BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionTextureId;
        private static readonly int DebugCameraColorTextureId = Shader.PropertyToID("_BurtSSAODebugCameraColorTexture");
        private static readonly int DebugCameraColorCopyTextureId = Shader.PropertyToID("_BurtSSAODebugCameraColorCopyTexture");
        private static readonly int SSAODebugModeId = Shader.PropertyToID("_BurtSSAODebugMode");
        private static readonly int DeferredScreenSizeId = Shader.PropertyToID("_BurtDeferredScreenSize");
        private static readonly int SSAOFullScreenSizeId = Shader.PropertyToID("_BurtSSAOFullScreenSize");
        private static readonly int SSAOTemporalParamsId = Shader.PropertyToID("_BurtSSAOTemporalParams");
        private static readonly int SSAOHistoryTextureId = Shader.PropertyToID("_BurtSSAOHistoryTexture");
        private static readonly int SSAOHistoryDepthTextureId = Shader.PropertyToID("_BurtSSAOHistoryDepthTexture");
        private static readonly int SSAOPreviousHistoryTextureId = Shader.PropertyToID("_BurtSSAOPreviousHistoryTexture");
        private static readonly int SSAOPreviousHistoryDepthTextureId = Shader.PropertyToID("_BurtSSAOPreviousHistoryDepthTexture");
        private static readonly int PreviousViewProjectionMatrixId = Shader.PropertyToID("_BurtSSAOPreviousViewProjectionMatrix");
        private static readonly int InverseViewProjectionMatrixId = Shader.PropertyToID("_BurtDeferredInverseViewProjectionMatrix");

        private Material screenSpaceAmbientOcclusionMaterial;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Debug Screen Space Ambient Occlusion";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceAmbientOcclusionPassUtility.ShouldUseScreenSpaceAmbientOcclusionDebugView(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceAmbientOcclusionRaw();
            builder.ReadScreenSpaceAmbientOcclusion();
            if (BurtScreenSpaceAmbientOcclusionPassUtility.IsScreenSpaceAmbientOcclusionDepthDiagnosticDebugMode(BurtShadingDebugSettings.Mode))
            {
                builder.ReadCameraDepth();
                builder.ReadGBuffer0();
            }

            if (BurtScreenSpaceAmbientOcclusionPassUtility.IsScreenSpaceAmbientOcclusionOverlayDebugMode(BurtShadingDebugSettings.Mode))
            {
                builder.ReadCameraColor();
            }

            builder.WriteCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceAmbientOcclusionPassUtility.ShouldUseScreenSpaceAmbientOcclusionDebugView(context.Request, context.Asset))
            {
                return;
            }

            var cameraColorTarget = context.CameraColorTarget;
            var cameraDepthTarget = context.CameraDepthTarget;
            var gbuffer0Target = context.GBuffer0Target;
            var aoRawTarget = context.ScreenSpaceAmbientOcclusionRawTarget;
            var aoTarget = context.ScreenSpaceAmbientOcclusionTarget;
            if (!cameraColorTarget.IsValid || !aoRawTarget.IsValid || !aoTarget.IsValid)
            {
                return;
            }

            var isDepthDiagnostic = BurtScreenSpaceAmbientOcclusionPassUtility.IsScreenSpaceAmbientOcclusionDepthDiagnosticDebugMode(BurtShadingDebugSettings.Mode);
            if (isDepthDiagnostic && (!cameraDepthTarget.IsValid || !gbuffer0Target.IsValid))
            {
                return;
            }

            var material = GetScreenSpaceAmbientOcclusionMaterial();
            if (material == null)
            {
                return;
            }

            var cmd = context.AcquireCommandBuffer(Name);
            var camera = context.Request != null ? context.Request.Camera : null;
            var isOverlay = BurtScreenSpaceAmbientOcclusionPassUtility.IsScreenSpaceAmbientOcclusionOverlayDebugMode(BurtShadingDebugSettings.Mode);
            if (isOverlay)
            {
                var descriptor = BurtRenderTargetDescriptorUtility.CreateCameraColorDescriptor(camera);
                cmd.GetTemporaryRT(DebugCameraColorCopyTextureId, descriptor, FilterMode.Bilinear);
                cmd.Blit(cameraColorTarget.Identifier, new RenderTargetIdentifier(DebugCameraColorCopyTextureId));
                cmd.SetGlobalTexture(DebugCameraColorTextureId, new RenderTargetIdentifier(DebugCameraColorCopyTextureId));
            }

            cmd.SetRenderTarget(cameraColorTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            cmd.SetGlobalTexture(AORawTextureId, aoRawTarget.Identifier);
            cmd.SetGlobalTexture(AOTextureId, aoTarget.Identifier);
            if (isDepthDiagnostic)
            {
                cmd.SetGlobalTexture(CameraDepthTextureId, cameraDepthTarget.Identifier);
                cmd.SetGlobalTexture(GBuffer0Id, gbuffer0Target.Identifier);
            }

            UploadTemporalDebugGlobals(cmd, context.Request, context.Asset);
            cmd.SetGlobalFloat(SSAODebugModeId, BurtScreenSpaceAmbientOcclusionPassUtility.ResolveScreenSpaceAmbientOcclusionShaderDebugMode());
            cmd.DrawProcedural(Matrix4x4.identity, material, BurtScreenSpaceAmbientOcclusionPassUtility.ResolveScreenSpaceAmbientOcclusionDebugPassIndex(), MeshTopology.Triangles, 3, 1);
            if (isOverlay)
            {
                cmd.ReleaseTemporaryRT(DebugCameraColorCopyTextureId);
            }

            context.ExecuteAndReleaseCommandBuffer(cmd);
        }

        private static void UploadTemporalDebugGlobals(CommandBuffer cmd, BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            var camera = request != null ? request.Camera : null;
            var settings = BurtScreenSpaceAmbientOcclusionPassUtility.ResolveScreenSpaceAmbientOcclusionSettings(request, asset);
            var history = BurtScreenSpaceAmbientOcclusionHistoryUtility.GetDebugSnapshotTextures(request);
            var status = BurtScreenSpaceAmbientOcclusionHistoryUtility.GetHistoryStatus(camera);
            var descriptor = BurtRenderTargetDescriptorUtility.CreateScreenSpaceAmbientOcclusionDescriptor(camera);
            var width = Mathf.Max(1, descriptor.width);
            var height = Mathf.Max(1, descriptor.height);
            cmd.SetGlobalTexture(SSAOHistoryTextureId, history.Color != null ? (Texture)history.Color : Texture2D.whiteTexture);
            cmd.SetGlobalTexture(SSAOHistoryDepthTextureId, history.Depth != null ? (Texture)history.Depth : Texture2D.blackTexture);
            cmd.SetGlobalTexture(SSAOPreviousHistoryTextureId, history.Color != null ? (Texture)history.Color : Texture2D.whiteTexture);
            cmd.SetGlobalTexture(SSAOPreviousHistoryDepthTextureId, history.Depth != null ? (Texture)history.Depth : Texture2D.blackTexture);
            cmd.SetGlobalMatrix(PreviousViewProjectionMatrixId, history.PreviousViewProjectionMatrix);
            cmd.SetGlobalMatrix(InverseViewProjectionMatrixId, history.CurrentInverseViewProjectionMatrix);
            cmd.SetGlobalVector(DeferredScreenSizeId, new Vector4(width, height, 1f / width, 1f / height));
            cmd.SetGlobalVector(SSAOFullScreenSizeId, new Vector4(width, height, 1f / width, 1f / height));
            cmd.SetGlobalVector(SSAOTemporalParamsId, new Vector4(settings.TemporalFeedback, status.HasHistory ? 1f : 0f, settings.TemporalDepthRejection, settings.TemporalClamp));
        }

        private Material GetScreenSpaceAmbientOcclusionMaterial()
        {
            if (screenSpaceAmbientOcclusionMaterial != null)
            {
                return screenSpaceAmbientOcclusionMaterial;
            }

            var shader = Shader.Find(ScreenSpaceAmbientOcclusionShaderName);
            if (shader == null)
            {
                if (!hasLoggedMissingShader)
                {
                    Debug.LogWarning("BurtRP could not find shader: " + ScreenSpaceAmbientOcclusionShaderName);
                    hasLoggedMissingShader = true;
                }

                return null;
            }

            screenSpaceAmbientOcclusionMaterial = new Material(shader);
            screenSpaceAmbientOcclusionMaterial.hideFlags = HideFlags.HideAndDontSave;
            return screenSpaceAmbientOcclusionMaterial;
        }
    }

    internal sealed class BurtReleaseScreenSpaceAmbientOcclusionRawPass : BurtRenderPass
    {
        public override string Name => "Burt Release Screen Space Ambient Occlusion Raw";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceAmbientOcclusionPassUtility.ShouldUseScreenSpaceAmbientOcclusion(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceAmbientOcclusionRaw();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceAmbientOcclusionPassUtility.ShouldUseScreenSpaceAmbientOcclusion(context.Request, context.Asset))
            {
                return;
            }

            var target = context.ScreenSpaceAmbientOcclusionRawTarget;
            if (!target.IsValid)
            {
                return;
            }

            BurtScreenSpaceAmbientOcclusionRenderTargetUtility.Release(context, Name, BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionRawTextureId);
        }
    }

    internal sealed class BurtReleaseScreenSpaceAmbientOcclusionPass : BurtRenderPass
    {
        public override string Name => "Burt Release Screen Space Ambient Occlusion";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceAmbientOcclusionPassUtility.ShouldUseScreenSpaceAmbientOcclusion(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceAmbientOcclusion();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceAmbientOcclusionPassUtility.ShouldUseScreenSpaceAmbientOcclusion(context.Request, context.Asset))
            {
                return;
            }

            var target = context.ScreenSpaceAmbientOcclusionTarget;
            if (!target.IsValid)
            {
                return;
            }

            BurtScreenSpaceAmbientOcclusionRenderTargetUtility.Release(context, Name, BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionTextureId);
        }
    }

    internal readonly struct BurtScreenSpaceAmbientOcclusionSettings
    {
        public static readonly BurtScreenSpaceAmbientOcclusionSettings Disabled = new BurtScreenSpaceAmbientOcclusionSettings(false, ScreenSpaceAmbientOcclusionQuality.Medium, ScreenSpaceAmbientOcclusionAlgorithm.SSAO, 1f, 1f, 0.5f, 2f, 16, true, 2, 0.003f, 2f, true, true, true, true, 0.78f, 0.012f, 0.75f, 0.12f, 0.5f, 50f, 80f);
        public static readonly BurtScreenSpaceAmbientOcclusionSettings DebugDefault = new BurtScreenSpaceAmbientOcclusionSettings(true, ScreenSpaceAmbientOcclusionQuality.Medium, ScreenSpaceAmbientOcclusionAlgorithm.SSAO, 1f, 1f, 0.5f, 2f, 16, true, 2, 0.003f, 2f, true, true, true, true, 0.78f, 0.012f, 0.75f, 0.12f, 0.5f, 50f, 80f);

        public BurtScreenSpaceAmbientOcclusionSettings(
            bool enabled,
            ScreenSpaceAmbientOcclusionQuality quality,
            ScreenSpaceAmbientOcclusionAlgorithm algorithm,
            float gtaoStrength,
            float hbaoStrength,
            float intensity,
            float radius,
            int sampleCount,
            bool horizonSearch,
            int directionCount,
            float bias,
            float power,
            bool halfResolution,
            bool blur,
            bool spatialDenoise,
            bool temporalAccumulation,
            float temporalFeedback,
            float temporalDepthRejection,
            float temporalClamp,
            float blurSharpness,
            float thickness,
            float fadeRadius,
            float fadeDistance)
        {
            Enabled = enabled;
            Quality = NormalizeQuality(quality);
            Algorithm = NormalizeAlgorithm(algorithm);
            GTAOStrength = Mathf.Clamp(gtaoStrength, 0f, 2f);
            HBAOStrength = Mathf.Clamp(hbaoStrength, 0f, 2f);
            Intensity = Mathf.Clamp(intensity, 0f, 4f);
            Radius = Mathf.Clamp(radius, 0.01f, 8f);
            SampleCount = Mathf.Clamp(sampleCount, 1, 32);
            HorizonSearch = horizonSearch;
            DirectionCount = Mathf.Clamp(directionCount, 1, 8);
            Bias = Mathf.Clamp(bias, 0f, 0.2f);
            Power = Mathf.Clamp(power, 0.1f, 16f);
            HalfResolution = halfResolution;
            Blur = blur;
            SpatialDenoise = spatialDenoise;
            TemporalAccumulation = temporalAccumulation;
            TemporalFeedback = Mathf.Clamp(temporalFeedback, 0f, 0.98f);
            TemporalDepthRejection = Mathf.Clamp(temporalDepthRejection, 0.001f, 0.2f);
            TemporalClamp = Mathf.Clamp(temporalClamp, 0f, 4f);
            BlurSharpness = Mathf.Clamp01(blurSharpness);
            Thickness = Mathf.Clamp01(thickness);
            FadeRadius = Mathf.Clamp(fadeRadius, 0.01f, 200f);
            FadeDistance = Mathf.Clamp(fadeDistance, 0.01f, 800f);
        }

        public bool Enabled { get; }

        public ScreenSpaceAmbientOcclusionQuality Quality { get; }

        public ScreenSpaceAmbientOcclusionAlgorithm Algorithm { get; }

        public float GTAOStrength { get; }

        public float HBAOStrength { get; }

        public float Intensity { get; }

        public float Radius { get; }

        public int SampleCount { get; }

        public bool HorizonSearch { get; }

        public int DirectionCount { get; }

        public float Bias { get; }

        public float Power { get; }

        public bool Blur { get; }

        public bool HalfResolution { get; }

        public bool SpatialDenoise { get; }

        public bool TemporalAccumulation { get; }

        public float TemporalFeedback { get; }

        public float TemporalDepthRejection { get; }

        public float TemporalClamp { get; }

        public float BlurSharpness { get; }

        public float Thickness { get; }

        public float FadeRadius { get; }

        public float FadeDistance { get; }

        public int CreateHistorySignature()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + BoolToInt(Enabled);
                hash = hash * 31 + (int)Quality;
                hash = hash * 31 + (int)Algorithm;
                hash = hash * 31 + Quantize(GTAOStrength, 1000f);
                hash = hash * 31 + Quantize(HBAOStrength, 1000f);
                hash = hash * 31 + Quantize(Intensity, 1000f);
                hash = hash * 31 + Quantize(Radius, 1000f);
                hash = hash * 31 + SampleCount;
                hash = hash * 31 + BoolToInt(HorizonSearch);
                hash = hash * 31 + DirectionCount;
                hash = hash * 31 + Quantize(Bias, 100000f);
                hash = hash * 31 + Quantize(Power, 1000f);
                hash = hash * 31 + BoolToInt(HalfResolution);
                hash = hash * 31 + BoolToInt(Blur);
                hash = hash * 31 + BoolToInt(SpatialDenoise);
                hash = hash * 31 + BoolToInt(TemporalAccumulation);
                hash = hash * 31 + Quantize(TemporalFeedback, 1000f);
                hash = hash * 31 + Quantize(TemporalDepthRejection, 10000f);
                hash = hash * 31 + Quantize(TemporalClamp, 1000f);
                hash = hash * 31 + Quantize(BlurSharpness, 1000f);
                hash = hash * 31 + Quantize(Thickness, 1000f);
                hash = hash * 31 + Quantize(FadeRadius, 100f);
                hash = hash * 31 + Quantize(FadeDistance, 100f);
                return hash;
            }
        }

        private static int BoolToInt(bool value)
        {
            return value ? 1 : 0;
        }

        private static int Quantize(float value, float scale)
        {
            return Mathf.RoundToInt(value * scale);
        }

        private static ScreenSpaceAmbientOcclusionQuality NormalizeQuality(ScreenSpaceAmbientOcclusionQuality quality)
        {
            switch (quality)
            {
                case ScreenSpaceAmbientOcclusionQuality.Low:
                case ScreenSpaceAmbientOcclusionQuality.Medium:
                case ScreenSpaceAmbientOcclusionQuality.High:
                    return quality;
                default:
                    return ScreenSpaceAmbientOcclusionQuality.Custom;
            }
        }

        private static ScreenSpaceAmbientOcclusionAlgorithm NormalizeAlgorithm(ScreenSpaceAmbientOcclusionAlgorithm algorithm)
        {
            switch (algorithm)
            {
                case ScreenSpaceAmbientOcclusionAlgorithm.GTAO:
                case ScreenSpaceAmbientOcclusionAlgorithm.HBAO:
                    return algorithm;
                default:
                    return ScreenSpaceAmbientOcclusionAlgorithm.SSAO;
            }
        }
    }

    internal readonly struct BurtScreenSpaceAmbientOcclusionHistoryMatrices
    {
        public Matrix4x4 ViewProjectionMatrix { get; }
        public Matrix4x4 InverseViewProjectionMatrix { get; }
        public Matrix4x4 NonJitteredProjectionMatrix { get; }

        public BurtScreenSpaceAmbientOcclusionHistoryMatrices(
            Matrix4x4 viewProjectionMatrix,
            Matrix4x4 inverseViewProjectionMatrix,
            Matrix4x4 nonJitteredProjectionMatrix)
        {
            ViewProjectionMatrix = viewProjectionMatrix;
            InverseViewProjectionMatrix = inverseViewProjectionMatrix;
            NonJitteredProjectionMatrix = nonJitteredProjectionMatrix;
        }
    }

    internal readonly struct BurtScreenSpaceAmbientOcclusionHistoryTextures
    {
        public RenderTexture Color { get; }
        public RenderTexture Depth { get; }
        public Matrix4x4 PreviousViewProjectionMatrix { get; }
        public Matrix4x4 CurrentInverseViewProjectionMatrix { get; }

        public BurtScreenSpaceAmbientOcclusionHistoryTextures(
            RenderTexture color,
            RenderTexture depth,
            Matrix4x4 previousViewProjectionMatrix,
            Matrix4x4 currentInverseViewProjectionMatrix)
        {
            Color = color;
            Depth = depth;
            PreviousViewProjectionMatrix = previousViewProjectionMatrix;
            CurrentInverseViewProjectionMatrix = currentInverseViewProjectionMatrix;
        }

        public static BurtScreenSpaceAmbientOcclusionHistoryTextures CreateInvalid(BurtScreenSpaceAmbientOcclusionHistoryMatrices matrices)
        {
            return new BurtScreenSpaceAmbientOcclusionHistoryTextures(null, null, matrices.ViewProjectionMatrix, matrices.InverseViewProjectionMatrix);
        }
    }

    internal readonly struct BurtScreenSpaceAmbientOcclusionHistoryStatus
    {
        public bool HasHistory { get; }
        public bool DescriptorMatches { get; }
        public bool HasDepthHistory { get; }
        public bool DepthDescriptorMatches { get; }
        public int Width { get; }
        public int Height { get; }
        public RenderTextureFormat Format { get; }
        public int FrameIndex { get; }
        public int HistoryAge { get; }
        public int FirstValidFrameIndex { get; }
        public int LastInvalidationFrameIndex { get; }
        public string LastInvalidationReason { get; }

        public BurtScreenSpaceAmbientOcclusionHistoryStatus(
            bool hasHistory,
            bool descriptorMatches,
            bool hasDepthHistory,
            bool depthDescriptorMatches,
            int width,
            int height,
            RenderTextureFormat format,
            int frameIndex,
            int historyAge,
            int firstValidFrameIndex,
            int lastInvalidationFrameIndex,
            string lastInvalidationReason)
        {
            HasHistory = hasHistory;
            DescriptorMatches = descriptorMatches;
            HasDepthHistory = hasDepthHistory;
            DepthDescriptorMatches = depthDescriptorMatches;
            Width = width;
            Height = height;
            Format = format;
            FrameIndex = frameIndex;
            HistoryAge = historyAge;
            FirstValidFrameIndex = firstValidFrameIndex;
            LastInvalidationFrameIndex = lastInvalidationFrameIndex;
            LastInvalidationReason = lastInvalidationReason;
        }
    }

    internal static class BurtScreenSpaceAmbientOcclusionHistoryUtility
    {
        private const int HistoryAlgorithmVersion = 2;
        private const int CameraStatePruneInterval = 128;
        private const float ProjectionChangeEpsilon = 0.0001f;

        private sealed class CameraState
        {
            public Camera Camera;
            public RenderTexture ColorHistory;
            public RenderTexture DepthHistory;
            public RenderTexture DebugPreviousColorHistory;
            public RenderTexture DebugPreviousDepthHistory;
            public RenderTextureDescriptor ColorDescriptor;
            public RenderTextureDescriptor DepthDescriptor;
            public RenderTextureDescriptor DebugPreviousColorDescriptor;
            public RenderTextureDescriptor DebugPreviousDepthDescriptor;
            public int AlgorithmVersion;
            public bool HasValidHistory;
            public bool HasPreviousCameraState;
            public int FrameIndex;
            public int FirstValidFrameIndex;
            public int LastInvalidationFrameIndex;
            public Matrix4x4 CurrentViewProjectionMatrix = Matrix4x4.identity;
            public Matrix4x4 CurrentInverseViewProjectionMatrix = Matrix4x4.identity;
            public Matrix4x4 CurrentNonJitteredProjectionMatrix = Matrix4x4.identity;
            public Matrix4x4 PreviousViewProjectionMatrix = Matrix4x4.identity;
            public Matrix4x4 PreviousNonJitteredProjectionMatrix = Matrix4x4.identity;
            public Matrix4x4 DebugPreviousViewProjectionMatrix = Matrix4x4.identity;
            public BurtRendererMode CurrentRendererMode = BurtRendererMode.Forward;
            public BurtRendererMode PreviousRendererMode = BurtRendererMode.Forward;
            public Vector3 CurrentCameraPosition;
            public Vector3 PreviousCameraPosition;
            public Quaternion CurrentCameraRotation = Quaternion.identity;
            public Quaternion PreviousCameraRotation = Quaternion.identity;
            public bool CurrentOrthographic;
            public bool PreviousOrthographic;
            public float CurrentFieldOfView;
            public float PreviousFieldOfView;
            public float CurrentOrthographicSize;
            public float PreviousOrthographicSize;
            public float CurrentNearClipPlane;
            public float PreviousNearClipPlane;
            public float CurrentFarClipPlane;
            public float PreviousFarClipPlane;
            public int CurrentTargetTextureId;
            public int PreviousTargetTextureId;
            public int CurrentTargetWidth;
            public int CurrentTargetHeight;
            public int PreviousTargetWidth;
            public int PreviousTargetHeight;
            public int SettingsSignature;
            public bool HasSettingsSignature;
            public string LastInvalidationReason = "NeverAllocated";
        }

        private static readonly System.Collections.Generic.Dictionary<int, CameraState> CameraStates = new System.Collections.Generic.Dictionary<int, CameraState>();
        private static readonly System.Collections.Generic.List<int> CameraStateRemovalKeys = new System.Collections.Generic.List<int>();
        private static int cameraStatePruneCounter;

        public static BurtScreenSpaceAmbientOcclusionHistoryTextures EnsureHistoryTextures(
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset,
            BurtScreenSpaceAmbientOcclusionSettings settings,
            out bool historyValid)
        {
            historyValid = false;
            var camera = request != null ? request.Camera : null;
            var matrices = CreateCurrentMatrices(request);
            if (camera == null)
            {
                return BurtScreenSpaceAmbientOcclusionHistoryTextures.CreateInvalid(matrices);
            }

            var state = GetOrCreateState(camera.GetInstanceID());
            state.Camera = camera;
            PruneDisposedCameraStates();
            if (state.AlgorithmVersion != HistoryAlgorithmVersion)
            {
                ReleaseHistory(state);
                state.AlgorithmVersion = HistoryAlgorithmVersion;
                SetAllocationInvalidationReason(state, "AlgorithmChanged");
            }

            var colorDescriptor = CreateColorHistoryDescriptor(camera);
            var depthDescriptor = CreateDepthHistoryDescriptor(camera);
            var colorMatches = state.ColorHistory != null && Matches(state.ColorDescriptor, colorDescriptor);
            var depthMatches = state.DepthHistory != null && Matches(state.DepthDescriptor, depthDescriptor);
            var debugColorMatches = state.DebugPreviousColorHistory != null && Matches(state.DebugPreviousColorDescriptor, colorDescriptor);
            var debugDepthMatches = state.DebugPreviousDepthHistory != null && Matches(state.DebugPreviousDepthDescriptor, depthDescriptor);
            var descriptorsMatch = colorMatches && depthMatches;
            GetTargetSize(camera, out var targetWidth, out var targetHeight);
            var rendererMode = asset != null ? asset.RendererMode : BurtRendererMode.Forward;
            var invalidationReason = ResolveHistoryInvalidationReason(camera, state, rendererMode, matrices.NonJitteredProjectionMatrix, targetWidth, targetHeight, descriptorsMatch);
            var settingsSignature = settings.CreateHistorySignature();
            if (string.IsNullOrEmpty(invalidationReason) && state.HasSettingsSignature && state.SettingsSignature != settingsSignature)
            {
                invalidationReason = "SettingsChanged";
            }

            if (!descriptorsMatch)
            {
                ReleaseHistory(state);
            }

            if (!debugColorMatches)
            {
                ReleaseTexture(state.DebugPreviousColorHistory);
                state.DebugPreviousColorHistory = null;
            }

            if (!debugDepthMatches)
            {
                ReleaseTexture(state.DebugPreviousDepthHistory);
                state.DebugPreviousDepthHistory = null;
            }

            if (state.ColorHistory == null)
            {
                state.ColorDescriptor = colorDescriptor;
                state.ColorHistory = CreateHistoryTexture(colorDescriptor, "Burt SSAO History " + camera.GetInstanceID(), FilterMode.Bilinear);
                SetAllocationInvalidationReason(state, "HistoryAllocated");
            }

            if (state.DepthHistory == null)
            {
                state.DepthDescriptor = depthDescriptor;
                state.DepthHistory = CreateHistoryTexture(depthDescriptor, "Burt SSAO Depth History " + camera.GetInstanceID(), FilterMode.Point);
                SetAllocationInvalidationReason(state, "DepthHistoryAllocated");
            }

            if (state.DebugPreviousColorHistory == null)
            {
                state.DebugPreviousColorDescriptor = colorDescriptor;
                state.DebugPreviousColorHistory = CreateHistoryTexture(colorDescriptor, "Burt SSAO Previous History Debug " + camera.GetInstanceID(), FilterMode.Bilinear);
            }

            if (state.DebugPreviousDepthHistory == null)
            {
                state.DebugPreviousDepthDescriptor = depthDescriptor;
                state.DebugPreviousDepthHistory = CreateHistoryTexture(depthDescriptor, "Burt SSAO Previous Depth Debug " + camera.GetInstanceID(), FilterMode.Point);
            }

            if (!string.IsNullOrEmpty(invalidationReason))
            {
                InvalidateState(state, invalidationReason);
            }

            state.FrameIndex++;
            state.CurrentRendererMode = rendererMode;
            state.CurrentViewProjectionMatrix = matrices.ViewProjectionMatrix;
            state.CurrentInverseViewProjectionMatrix = matrices.InverseViewProjectionMatrix;
            state.CurrentNonJitteredProjectionMatrix = matrices.NonJitteredProjectionMatrix;
            state.SettingsSignature = settingsSignature;
            state.HasSettingsSignature = true;
            CaptureCurrentCameraState(camera, state, targetWidth, targetHeight);

            historyValid = state.HasValidHistory && state.HasPreviousCameraState && state.ColorHistory != null && state.DepthHistory != null;
            var previousViewProjectionMatrix = state.HasPreviousCameraState ? state.PreviousViewProjectionMatrix : matrices.ViewProjectionMatrix;
            return new BurtScreenSpaceAmbientOcclusionHistoryTextures(state.ColorHistory, state.DepthHistory, previousViewProjectionMatrix, matrices.InverseViewProjectionMatrix);
        }

        public static BurtScreenSpaceAmbientOcclusionHistoryMatrices CreateCurrentMatrices(BurtRenderRequest request)
        {
            var camera = request != null ? request.Camera : null;
            return CreateCurrentMatrices(camera, request != null ? request.TemporalAA : null);
        }

        private static BurtScreenSpaceAmbientOcclusionHistoryMatrices CreateCurrentMatrices(Camera camera)
        {
            return CreateCurrentMatrices(camera, null);
        }

        private static BurtScreenSpaceAmbientOcclusionHistoryMatrices CreateCurrentMatrices(Camera camera, BurtTemporalAARequestState temporalAA)
        {
            if (camera == null)
            {
                return new BurtScreenSpaceAmbientOcclusionHistoryMatrices(Matrix4x4.identity, Matrix4x4.identity, Matrix4x4.identity);
            }

            var viewMatrix = temporalAA != null ? temporalAA.ViewMatrix : camera.worldToCameraMatrix;
            var projectionMatrix = temporalAA != null ? temporalAA.JitteredProjectionMatrix : camera.projectionMatrix;
            var nonJitteredProjectionMatrix = temporalAA != null ? temporalAA.NonJitteredProjectionMatrix : camera.projectionMatrix;
            var viewProjectionMatrix = GL.GetGPUProjectionMatrix(projectionMatrix, true) * viewMatrix;
            return new BurtScreenSpaceAmbientOcclusionHistoryMatrices(viewProjectionMatrix, viewProjectionMatrix.inverse, nonJitteredProjectionMatrix);
        }

        public static void MarkHistoryValid(Camera camera)
        {
            if (camera == null || !CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
            {
                return;
            }

            if (!state.HasValidHistory)
            {
                state.FirstValidFrameIndex = state.FrameIndex;
            }

            state.HasValidHistory = true;
            state.PreviousRendererMode = state.CurrentRendererMode;
            state.PreviousViewProjectionMatrix = state.CurrentViewProjectionMatrix;
            state.PreviousNonJitteredProjectionMatrix = state.CurrentNonJitteredProjectionMatrix;
            state.PreviousCameraPosition = state.CurrentCameraPosition;
            state.PreviousCameraRotation = state.CurrentCameraRotation;
            state.PreviousOrthographic = state.CurrentOrthographic;
            state.PreviousFieldOfView = state.CurrentFieldOfView;
            state.PreviousOrthographicSize = state.CurrentOrthographicSize;
            state.PreviousNearClipPlane = state.CurrentNearClipPlane;
            state.PreviousFarClipPlane = state.CurrentFarClipPlane;
            state.PreviousTargetTextureId = state.CurrentTargetTextureId;
            state.PreviousTargetWidth = state.CurrentTargetWidth;
            state.PreviousTargetHeight = state.CurrentTargetHeight;
            state.HasPreviousCameraState = true;
        }

        public static void InvalidateHistory(Camera camera, string reason)
        {
            if (camera == null || !CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
            {
                return;
            }

            InvalidateState(state, string.IsNullOrEmpty(reason) ? "Manual" : reason);
        }

        public static BurtScreenSpaceAmbientOcclusionHistoryStatus GetHistoryStatus(Camera camera)
        {
            if (camera == null || !CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
            {
                return new BurtScreenSpaceAmbientOcclusionHistoryStatus(false, false, false, false, 0, 0, RenderTextureFormat.Default, 0, 0, 0, 0, "NoCameraOrHistory");
            }

            var colorDescriptor = CreateColorHistoryDescriptor(camera);
            var depthDescriptor = CreateDepthHistoryDescriptor(camera);
            var hasColor = state.ColorHistory != null;
            var hasDepth = state.DepthHistory != null;
            var historyAge = state.HasValidHistory && state.FirstValidFrameIndex > 0 ? Mathf.Max(0, state.FrameIndex - state.FirstValidFrameIndex + 1) : 0;
            return new BurtScreenSpaceAmbientOcclusionHistoryStatus(
                state.HasValidHistory && hasColor && hasDepth,
                hasColor && Matches(state.ColorDescriptor, colorDescriptor),
                hasDepth,
                hasDepth && Matches(state.DepthDescriptor, depthDescriptor),
                hasColor ? state.ColorHistory.width : 0,
                hasColor ? state.ColorHistory.height : 0,
                hasColor ? state.ColorHistory.format : RenderTextureFormat.Default,
                state.FrameIndex,
                historyAge,
                state.FirstValidFrameIndex,
                state.LastInvalidationFrameIndex,
                state.LastInvalidationReason);
        }

        public static void CopyHistoryToDebugSnapshot(CommandBuffer cmd, Camera camera)
        {
            if (cmd == null || camera == null || !CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
            {
                return;
            }

            if (state.ColorHistory == null || state.DepthHistory == null || state.DebugPreviousColorHistory == null || state.DebugPreviousDepthHistory == null)
            {
                return;
            }

            cmd.CopyTexture(new RenderTargetIdentifier(state.ColorHistory), new RenderTargetIdentifier(state.DebugPreviousColorHistory));
            cmd.CopyTexture(new RenderTargetIdentifier(state.DepthHistory), new RenderTargetIdentifier(state.DebugPreviousDepthHistory));
            state.DebugPreviousViewProjectionMatrix = state.HasPreviousCameraState ? state.PreviousViewProjectionMatrix : state.CurrentViewProjectionMatrix;
        }

        public static BurtScreenSpaceAmbientOcclusionHistoryTextures GetDebugSnapshotTextures(BurtRenderRequest request)
        {
            var matrices = CreateCurrentMatrices(request);
            var camera = request != null ? request.Camera : null;
            if (camera == null || !CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
            {
                return BurtScreenSpaceAmbientOcclusionHistoryTextures.CreateInvalid(matrices);
            }

            var previousViewProjectionMatrix = state.DebugPreviousColorHistory != null && state.DebugPreviousDepthHistory != null
                ? state.DebugPreviousViewProjectionMatrix
                : (state.HasPreviousCameraState ? state.PreviousViewProjectionMatrix : matrices.ViewProjectionMatrix);
            return new BurtScreenSpaceAmbientOcclusionHistoryTextures(state.DebugPreviousColorHistory, state.DebugPreviousDepthHistory, previousViewProjectionMatrix, matrices.InverseViewProjectionMatrix);
        }

        public static RenderTextureDescriptor CreateDepthHistoryDescriptor(Camera camera)
        {
            var descriptor = BurtRenderTargetDescriptorUtility.CreateScreenSpaceAmbientOcclusionDescriptor(camera);
            descriptor.colorFormat = RenderTextureFormat.RFloat;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.useMipMap = false;
            descriptor.mipCount = 1;
            descriptor.autoGenerateMips = false;
            descriptor.sRGB = false;
            return descriptor;
        }

        public static RenderTextureDescriptor CreateColorHistoryDescriptor(Camera camera)
        {
            var descriptor = BurtRenderTargetDescriptorUtility.CreateScreenSpaceAmbientOcclusionDescriptor(camera);
            descriptor.colorFormat = RenderTextureFormat.RHalf;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.mipCount = 1;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            descriptor.sRGB = false;
            return descriptor;
        }

        private static CameraState GetOrCreateState(int cameraId)
        {
            if (!CameraStates.TryGetValue(cameraId, out var state))
            {
                state = new CameraState();
                CameraStates.Add(cameraId, state);
            }

            return state;
        }

        private static void PruneDisposedCameraStates()
        {
            cameraStatePruneCounter++;
            if (cameraStatePruneCounter < CameraStatePruneInterval)
            {
                return;
            }

            cameraStatePruneCounter = 0;
            CameraStateRemovalKeys.Clear();
            foreach (var pair in CameraStates)
            {
                if (pair.Value.Camera != null)
                {
                    continue;
                }

                ReleaseHistory(pair.Value);
                CameraStateRemovalKeys.Add(pair.Key);
            }

            for (var i = 0; i < CameraStateRemovalKeys.Count; i++)
            {
                CameraStates.Remove(CameraStateRemovalKeys[i]);
            }

            CameraStateRemovalKeys.Clear();
        }

        private static RenderTexture CreateHistoryTexture(RenderTextureDescriptor descriptor, string name, FilterMode filterMode)
        {
            var texture = new RenderTexture(descriptor)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = filterMode,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.Create();
            return texture;
        }

        private static bool Matches(RenderTextureDescriptor left, RenderTextureDescriptor right)
        {
            return left.width == right.width &&
                left.height == right.height &&
                left.colorFormat == right.colorFormat &&
                left.depthBufferBits == right.depthBufferBits &&
                left.msaaSamples == right.msaaSamples &&
                left.useMipMap == right.useMipMap &&
                left.autoGenerateMips == right.autoGenerateMips &&
                left.mipCount == right.mipCount &&
                left.sRGB == right.sRGB;
        }

        private static string ResolveHistoryInvalidationReason(
            Camera camera,
            CameraState state,
            BurtRendererMode rendererMode,
            Matrix4x4 nonJitteredProjectionMatrix,
            int targetWidth,
            int targetHeight,
            bool descriptorsMatch)
        {
            if (state == null)
            {
                return "NoCameraState";
            }

            if (!state.HasPreviousCameraState)
            {
                return descriptorsMatch ? null : "DescriptorChanged";
            }

            if (rendererMode != state.PreviousRendererMode)
            {
                return "RendererModeChanged";
            }

            if (GetTargetTextureId(camera) != state.PreviousTargetTextureId)
            {
                return "TargetTextureChanged";
            }

            if (targetWidth != state.PreviousTargetWidth || targetHeight != state.PreviousTargetHeight)
            {
                return GetTargetTextureId(camera) != 0 ? "TargetTextureSizeChanged" : "CameraResolutionChanged";
            }

            if (!descriptorsMatch)
            {
                return "DescriptorChanged";
            }

            if (camera != null)
            {
                if (camera.orthographic != state.PreviousOrthographic)
                {
                    return "ProjectionModeChanged";
                }

                if (camera.orthographic)
                {
                    if (FloatChanged(camera.orthographicSize, state.PreviousOrthographicSize, 0.0001f))
                    {
                        return "OrthographicSizeChanged";
                    }
                }
                else if (FloatChanged(camera.fieldOfView, state.PreviousFieldOfView, 0.0001f))
                {
                    return "FOVChanged";
                }

                if (FloatChanged(camera.nearClipPlane, state.PreviousNearClipPlane, 0.0001f))
                {
                    return "NearClipChanged";
                }

                if (FloatChanged(camera.farClipPlane, state.PreviousFarClipPlane, 0.001f))
                {
                    return "FarClipChanged";
                }
            }

            if (ProjectionChanged(nonJitteredProjectionMatrix, state.PreviousNonJitteredProjectionMatrix))
            {
                return "ProjectionChanged";
            }

            return CameraCutDetected(camera, state) ? "CameraCut" : null;
        }

        private static void CaptureCurrentCameraState(Camera camera, CameraState state, int targetWidth, int targetHeight)
        {
            if (camera == null || state == null)
            {
                return;
            }

            state.CurrentCameraPosition = camera.transform.position;
            state.CurrentCameraRotation = camera.transform.rotation;
            state.CurrentOrthographic = camera.orthographic;
            state.CurrentFieldOfView = camera.fieldOfView;
            state.CurrentOrthographicSize = camera.orthographicSize;
            state.CurrentNearClipPlane = camera.nearClipPlane;
            state.CurrentFarClipPlane = camera.farClipPlane;
            state.CurrentTargetTextureId = GetTargetTextureId(camera);
            state.CurrentTargetWidth = targetWidth;
            state.CurrentTargetHeight = targetHeight;
        }

        private static void InvalidateState(CameraState state, string reason)
        {
            if (state == null)
            {
                return;
            }

            state.HasValidHistory = false;
            state.FirstValidFrameIndex = 0;
            state.LastInvalidationFrameIndex = state.FrameIndex;
            state.LastInvalidationReason = string.IsNullOrEmpty(reason) ? "Unknown" : reason;
        }

        private static void SetAllocationInvalidationReason(CameraState state, string reason)
        {
            if (state == null)
            {
                return;
            }

            if (!state.HasPreviousCameraState ||
                state.LastInvalidationReason == "NeverAllocated" ||
                state.LastInvalidationReason == "HistoryAllocated" ||
                state.LastInvalidationReason == "DepthHistoryAllocated")
            {
                state.LastInvalidationReason = reason;
            }
        }

        private static int GetTargetTextureId(Camera camera)
        {
            return camera != null && camera.targetTexture != null ? camera.targetTexture.GetInstanceID() : 0;
        }

        private static void GetTargetSize(Camera camera, out int width, out int height)
        {
            if (camera != null && camera.targetTexture != null)
            {
                width = Mathf.Max(1, camera.targetTexture.width);
                height = Mathf.Max(1, camera.targetTexture.height);
                return;
            }

            width = Mathf.Max(1, camera != null ? camera.pixelWidth : 1);
            height = Mathf.Max(1, camera != null ? camera.pixelHeight : 1);
        }

        private static bool FloatChanged(float current, float previous, float epsilon)
        {
            return Mathf.Abs(current - previous) > epsilon;
        }

        private static bool ProjectionChanged(Matrix4x4 current, Matrix4x4 previous)
        {
            for (var i = 0; i < 16; i++)
            {
                if (Mathf.Abs(current[i] - previous[i]) > ProjectionChangeEpsilon)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CameraCutDetected(Camera camera, CameraState state)
        {
            if (camera == null || state == null)
            {
                return false;
            }

            var positionDelta = camera.transform.position - state.PreviousCameraPosition;
            var rotationDelta = Quaternion.Angle(camera.transform.rotation, state.PreviousCameraRotation);
            var farClip = Mathf.Max(camera.farClipPlane, 1f);
            var cutDistance = Mathf.Clamp(farClip * 0.25f, 25f, 250f);
            return positionDelta.sqrMagnitude > cutDistance * cutDistance || rotationDelta > 60f;
        }

        private static void ReleaseHistory(CameraState state)
        {
            if (state == null)
            {
                return;
            }

            ReleaseTexture(state.ColorHistory);
            ReleaseTexture(state.DepthHistory);
            ReleaseTexture(state.DebugPreviousColorHistory);
            ReleaseTexture(state.DebugPreviousDepthHistory);
            state.ColorHistory = null;
            state.DepthHistory = null;
            state.DebugPreviousColorHistory = null;
            state.DebugPreviousDepthHistory = null;
            state.HasValidHistory = false;
            state.HasSettingsSignature = false;
            state.FirstValidFrameIndex = 0;
        }

        private static void ReleaseTexture(RenderTexture texture)
        {
            if (texture == null)
            {
                return;
            }

            texture.Release();
            Object.DestroyImmediate(texture);
        }
    }

    internal static class BurtScreenSpaceAmbientOcclusionPassUtility
    {
        public static bool ShouldUseScreenSpaceAmbientOcclusion(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return ResolveScreenSpaceAmbientOcclusionSettings(request, asset).Enabled;
        }

        public static bool ShouldUseScreenSpaceAmbientOcclusionDebugView(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return IsScreenSpaceAmbientOcclusionDebugMode(BurtShadingDebugSettings.Mode) && ShouldUseScreenSpaceAmbientOcclusion(request, asset);
        }

        public static bool IsScreenSpaceAmbientOcclusionDebugMode(BurtShadingDebugMode mode)
        {
            return mode == BurtShadingDebugMode.ScreenSpaceAmbientOcclusionRaw ||
                mode == BurtShadingDebugMode.ScreenSpaceAmbientOcclusionFinal ||
                IsScreenSpaceAmbientOcclusionOverlayDebugMode(mode) ||
                IsScreenSpaceAmbientOcclusionTemporalDebugMode(mode);
        }

        public static bool IsScreenSpaceAmbientOcclusionOverlayDebugMode(BurtShadingDebugMode mode)
        {
            return mode == BurtShadingDebugMode.ScreenSpaceAmbientOcclusionOverlay;
        }

        public static bool IsScreenSpaceAmbientOcclusionTemporalDebugMode(BurtShadingDebugMode mode)
        {
            return mode == BurtShadingDebugMode.ScreenSpaceAmbientOcclusionHistory ||
                mode == BurtShadingDebugMode.ScreenSpaceAmbientOcclusionDifference ||
                mode == BurtShadingDebugMode.ScreenSpaceAmbientOcclusionDepthValidity ||
                mode == BurtShadingDebugMode.ScreenSpaceAmbientOcclusionSurfaceStability ||
                mode == BurtShadingDebugMode.ScreenSpaceAmbientOcclusionDiagnosticCompare;
        }

        public static bool IsScreenSpaceAmbientOcclusionDepthValidityDebugMode(BurtShadingDebugMode mode)
        {
            return mode == BurtShadingDebugMode.ScreenSpaceAmbientOcclusionDepthValidity;
        }

        public static bool IsScreenSpaceAmbientOcclusionDepthDiagnosticDebugMode(BurtShadingDebugMode mode)
        {
            return mode == BurtShadingDebugMode.ScreenSpaceAmbientOcclusionDepthValidity ||
                mode == BurtShadingDebugMode.ScreenSpaceAmbientOcclusionSurfaceStability ||
                mode == BurtShadingDebugMode.ScreenSpaceAmbientOcclusionDiagnosticCompare;
        }

        public static int ResolveScreenSpaceAmbientOcclusionShaderDebugMode()
        {
            switch (BurtShadingDebugSettings.Mode)
            {
                case BurtShadingDebugMode.ScreenSpaceAmbientOcclusionRaw:
                    return 1;
                case BurtShadingDebugMode.ScreenSpaceAmbientOcclusionFinal:
                    return 2;
                case BurtShadingDebugMode.ScreenSpaceAmbientOcclusionOverlay:
                    return 3;
                case BurtShadingDebugMode.ScreenSpaceAmbientOcclusionHistory:
                    return 4;
                case BurtShadingDebugMode.ScreenSpaceAmbientOcclusionDifference:
                    return 5;
                case BurtShadingDebugMode.ScreenSpaceAmbientOcclusionDepthValidity:
                    return 6;
                case BurtShadingDebugMode.ScreenSpaceAmbientOcclusionSurfaceStability:
                    return 7;
                case BurtShadingDebugMode.ScreenSpaceAmbientOcclusionDiagnosticCompare:
                    return 8;
                default:
                    return 0;
            }
        }

        public static int ResolveScreenSpaceAmbientOcclusionDebugPassIndex()
        {
            if (IsScreenSpaceAmbientOcclusionOverlayDebugMode(BurtShadingDebugSettings.Mode))
            {
                return 3;
            }

            return IsScreenSpaceAmbientOcclusionDepthValidityDebugMode(BurtShadingDebugSettings.Mode) ||
                BurtShadingDebugSettings.Mode == BurtShadingDebugMode.ScreenSpaceAmbientOcclusionSurfaceStability ||
                BurtShadingDebugSettings.Mode == BurtShadingDebugMode.ScreenSpaceAmbientOcclusionDiagnosticCompare ? 9 : 2;
        }

        public static string ResolveScreenSpaceAmbientOcclusionDebugModeLabel()
        {
            switch (BurtShadingDebugSettings.Mode)
            {
                case BurtShadingDebugMode.ScreenSpaceAmbientOcclusionRaw:
                    return "Raw";
                case BurtShadingDebugMode.ScreenSpaceAmbientOcclusionFinal:
                    return "Final";
                case BurtShadingDebugMode.ScreenSpaceAmbientOcclusionOverlay:
                    return "Overlay";
                case BurtShadingDebugMode.ScreenSpaceAmbientOcclusionHistory:
                    return "History";
                case BurtShadingDebugMode.ScreenSpaceAmbientOcclusionDifference:
                    return "TemporalDifference";
                case BurtShadingDebugMode.ScreenSpaceAmbientOcclusionDepthValidity:
                    return "DepthValidity";
                case BurtShadingDebugMode.ScreenSpaceAmbientOcclusionSurfaceStability:
                    return "SurfaceStability";
                case BurtShadingDebugMode.ScreenSpaceAmbientOcclusionDiagnosticCompare:
                    return "DiagnosticCompare";
                default:
                    return "Disabled";
            }
        }

        public static BurtScreenSpaceAmbientOcclusionSettings ResolveScreenSpaceAmbientOcclusionSettings(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            if (request == null || !request.IsValid || request.Camera == null)
            {
                return BurtScreenSpaceAmbientOcclusionSettings.Disabled;
            }

            if (request.Type == BurtRenderRequestType.Preview || request.Type == BurtRenderRequestType.Reflection)
            {
                return DisableScreenSpaceAmbientOcclusion(request, "RequestTypeDisabled");
            }

            if (asset == null || asset.RendererMode != BurtRendererMode.Deferred)
            {
                return DisableScreenSpaceAmbientOcclusion(request, "RendererDisabled");
            }

            var debugRequested = IsScreenSpaceAmbientOcclusionDebugMode(BurtShadingDebugSettings.Mode);
            var component = GetScreenSpaceAmbientOcclusionVolumeComponent();
            if (component == null || (debugRequested && !component.IsEnabled()))
            {
                return debugRequested ? BurtScreenSpaceAmbientOcclusionSettings.DebugDefault : DisableScreenSpaceAmbientOcclusion(request, "VolumeMissing");
            }

            if (!component.IsEnabled() && !debugRequested)
            {
                return DisableScreenSpaceAmbientOcclusion(request, "VolumeDisabled");
            }

            var quality = component.quality.value;
            var algorithm = component.algorithm.value;
            var gtaoStrength = component.gtaoStrength.value;
            var hbaoStrength = component.hbaoStrength.value;
            var sampleCount = component.sampleCount.value;
            var horizonSearch = component.horizonSearch.value;
            var directionCount = component.directionCount.value;
            var halfResolution = component.halfResolution.value;
            var blur = component.blur.value;
            var spatialDenoise = component.spatialDenoise.value;
            var temporalAccumulation = component.temporalAccumulation.value;
            var temporalFeedback = component.temporalFeedback.value;
            var temporalDepthRejection = component.temporalDepthRejection.value;
            var temporalClamp = component.temporalClamp.value;
            var blurSharpness = component.blurSharpness.value;
            ApplyScreenSpaceAmbientOcclusionQualityPreset(
                quality,
                algorithm,
                ref gtaoStrength,
                ref hbaoStrength,
                ref sampleCount,
                ref horizonSearch,
                ref directionCount,
                ref halfResolution,
                ref blur,
                ref spatialDenoise,
                ref temporalAccumulation,
                ref temporalFeedback,
                ref temporalDepthRejection,
                ref temporalClamp,
                ref blurSharpness);

            return new BurtScreenSpaceAmbientOcclusionSettings(
                true,
                quality,
                algorithm,
                gtaoStrength,
                hbaoStrength,
                component.intensity.value,
                component.radius.value,
                sampleCount,
                horizonSearch,
                directionCount,
                component.bias.value,
                component.power.value,
                halfResolution,
                blur,
                spatialDenoise,
                temporalAccumulation,
                temporalFeedback,
                temporalDepthRejection,
                temporalClamp,
                blurSharpness,
                component.thickness.value,
                component.fadeRadius.value,
                component.fadeDistance.value);
        }

        private static void ApplyScreenSpaceAmbientOcclusionQualityPreset(
            ScreenSpaceAmbientOcclusionQuality quality,
            ScreenSpaceAmbientOcclusionAlgorithm algorithm,
            ref float gtaoStrength,
            ref float hbaoStrength,
            ref int sampleCount,
            ref bool horizonSearch,
            ref int directionCount,
            ref bool halfResolution,
            ref bool blur,
            ref bool spatialDenoise,
            ref bool temporalAccumulation,
            ref float temporalFeedback,
            ref float temporalDepthRejection,
            ref float temporalClamp,
            ref float blurSharpness)
        {
            if (quality == ScreenSpaceAmbientOcclusionQuality.Custom)
            {
                return;
            }

            algorithm = NormalizeScreenSpaceAmbientOcclusionAlgorithm(algorithm);
            switch (algorithm)
            {
                case ScreenSpaceAmbientOcclusionAlgorithm.GTAO:
                    ApplyGTAOQualityPreset(
                        quality,
                        ref gtaoStrength,
                        ref hbaoStrength,
                        ref sampleCount,
                        ref horizonSearch,
                        ref directionCount,
                        ref halfResolution,
                        ref blur,
                        ref spatialDenoise,
                        ref temporalAccumulation,
                        ref temporalFeedback,
                        ref temporalDepthRejection,
                        ref temporalClamp,
                        ref blurSharpness);
                    break;
                case ScreenSpaceAmbientOcclusionAlgorithm.HBAO:
                    ApplyHBAOQualityPreset(
                        quality,
                        ref gtaoStrength,
                        ref hbaoStrength,
                        ref sampleCount,
                        ref horizonSearch,
                        ref directionCount,
                        ref halfResolution,
                        ref blur,
                        ref spatialDenoise,
                        ref temporalAccumulation,
                        ref temporalFeedback,
                        ref temporalDepthRejection,
                        ref temporalClamp,
                        ref blurSharpness);
                    break;
                default:
                    ApplySSAOQualityPreset(
                        quality,
                        ref gtaoStrength,
                        ref hbaoStrength,
                        ref sampleCount,
                        ref horizonSearch,
                        ref directionCount,
                        ref halfResolution,
                        ref blur,
                        ref spatialDenoise,
                        ref temporalAccumulation,
                        ref temporalFeedback,
                        ref temporalDepthRejection,
                        ref temporalClamp,
                        ref blurSharpness);
                    break;
            }
        }

        private static void ApplySSAOQualityPreset(
            ScreenSpaceAmbientOcclusionQuality quality,
            ref float gtaoStrength,
            ref float hbaoStrength,
            ref int sampleCount,
            ref bool horizonSearch,
            ref int directionCount,
            ref bool halfResolution,
            ref bool blur,
            ref bool spatialDenoise,
            ref bool temporalAccumulation,
            ref float temporalFeedback,
            ref float temporalDepthRejection,
            ref float temporalClamp,
            ref float blurSharpness)
        {
            gtaoStrength = 1f;
            hbaoStrength = 1f;
            switch (quality)
            {
                case ScreenSpaceAmbientOcclusionQuality.Low:
                    sampleCount = 8;
                    horizonSearch = true;
                    directionCount = 1;
                    halfResolution = true;
                    blur = true;
                    spatialDenoise = true;
                    temporalAccumulation = true;
                    temporalFeedback = 0.72f;
                    temporalDepthRejection = 0.018f;
                    temporalClamp = 0.55f;
                    blurSharpness = 0.08f;
                    break;
                case ScreenSpaceAmbientOcclusionQuality.Medium:
                    sampleCount = 16;
                    horizonSearch = true;
                    directionCount = 2;
                    halfResolution = true;
                    blur = true;
                    spatialDenoise = true;
                    temporalAccumulation = true;
                    temporalFeedback = 0.78f;
                    temporalDepthRejection = 0.012f;
                    temporalClamp = 0.75f;
                    blurSharpness = 0.12f;
                    break;
                case ScreenSpaceAmbientOcclusionQuality.High:
                    sampleCount = 24;
                    horizonSearch = true;
                    directionCount = 4;
                    halfResolution = false;
                    blur = true;
                    spatialDenoise = true;
                    temporalAccumulation = true;
                    temporalFeedback = 0.82f;
                    temporalDepthRejection = 0.01f;
                    temporalClamp = 0.9f;
                    blurSharpness = 0.18f;
                    break;
            }
        }

        private static void ApplyGTAOQualityPreset(
            ScreenSpaceAmbientOcclusionQuality quality,
            ref float gtaoStrength,
            ref float hbaoStrength,
            ref int sampleCount,
            ref bool horizonSearch,
            ref int directionCount,
            ref bool halfResolution,
            ref bool blur,
            ref bool spatialDenoise,
            ref bool temporalAccumulation,
            ref float temporalFeedback,
            ref float temporalDepthRejection,
            ref float temporalClamp,
            ref float blurSharpness)
        {
            hbaoStrength = 1f;
            horizonSearch = true;
            blur = true;
            spatialDenoise = true;
            temporalAccumulation = true;
            switch (quality)
            {
                case ScreenSpaceAmbientOcclusionQuality.Low:
                    gtaoStrength = 0.85f;
                    sampleCount = 8;
                    directionCount = 2;
                    halfResolution = true;
                    temporalFeedback = 0.74f;
                    temporalDepthRejection = 0.016f;
                    temporalClamp = 0.6f;
                    blurSharpness = 0.1f;
                    break;
                case ScreenSpaceAmbientOcclusionQuality.Medium:
                    gtaoStrength = 0.95f;
                    sampleCount = 16;
                    directionCount = 3;
                    halfResolution = true;
                    temporalFeedback = 0.8f;
                    temporalDepthRejection = 0.012f;
                    temporalClamp = 0.78f;
                    blurSharpness = 0.14f;
                    break;
                case ScreenSpaceAmbientOcclusionQuality.High:
                    gtaoStrength = 1f;
                    sampleCount = 24;
                    directionCount = 4;
                    halfResolution = false;
                    temporalFeedback = 0.84f;
                    temporalDepthRejection = 0.009f;
                    temporalClamp = 0.95f;
                    blurSharpness = 0.2f;
                    break;
            }
        }

        private static void ApplyHBAOQualityPreset(
            ScreenSpaceAmbientOcclusionQuality quality,
            ref float gtaoStrength,
            ref float hbaoStrength,
            ref int sampleCount,
            ref bool horizonSearch,
            ref int directionCount,
            ref bool halfResolution,
            ref bool blur,
            ref bool spatialDenoise,
            ref bool temporalAccumulation,
            ref float temporalFeedback,
            ref float temporalDepthRejection,
            ref float temporalClamp,
            ref float blurSharpness)
        {
            gtaoStrength = 1f;
            horizonSearch = true;
            blur = true;
            spatialDenoise = true;
            temporalAccumulation = true;
            switch (quality)
            {
                case ScreenSpaceAmbientOcclusionQuality.Low:
                    hbaoStrength = 0.75f;
                    sampleCount = 8;
                    directionCount = 2;
                    halfResolution = true;
                    temporalFeedback = 0.7f;
                    temporalDepthRejection = 0.02f;
                    temporalClamp = 0.5f;
                    blurSharpness = 0.08f;
                    break;
                case ScreenSpaceAmbientOcclusionQuality.Medium:
                    hbaoStrength = 0.85f;
                    sampleCount = 16;
                    directionCount = 3;
                    halfResolution = true;
                    temporalFeedback = 0.76f;
                    temporalDepthRejection = 0.014f;
                    temporalClamp = 0.68f;
                    blurSharpness = 0.12f;
                    break;
                case ScreenSpaceAmbientOcclusionQuality.High:
                    hbaoStrength = 0.95f;
                    sampleCount = 24;
                    directionCount = 4;
                    halfResolution = false;
                    temporalFeedback = 0.8f;
                    temporalDepthRejection = 0.011f;
                    temporalClamp = 0.82f;
                    blurSharpness = 0.18f;
                    break;
            }
        }

        private static ScreenSpaceAmbientOcclusionAlgorithm NormalizeScreenSpaceAmbientOcclusionAlgorithm(ScreenSpaceAmbientOcclusionAlgorithm algorithm)
        {
            switch (algorithm)
            {
                case ScreenSpaceAmbientOcclusionAlgorithm.GTAO:
                case ScreenSpaceAmbientOcclusionAlgorithm.HBAO:
                    return algorithm;
                default:
                    return ScreenSpaceAmbientOcclusionAlgorithm.SSAO;
            }
        }

        private static BurtScreenSpaceAmbientOcclusionSettings DisableScreenSpaceAmbientOcclusion(BurtRenderRequest request, string reason)
        {
            BurtScreenSpaceAmbientOcclusionHistoryUtility.InvalidateHistory(request != null ? request.Camera : null, reason);
            return BurtScreenSpaceAmbientOcclusionSettings.Disabled;
        }

        private static ScreenSpaceAmbientOcclusionVolumeComponent GetScreenSpaceAmbientOcclusionVolumeComponent()
        {
            var volumeManager = VolumeManager.instance;
            if (volumeManager == null)
            {
                return null;
            }

            var stack = volumeManager.stack;
            if (stack == null)
            {
                return null;
            }

            return stack.GetComponent<ScreenSpaceAmbientOcclusionVolumeComponent>();
        }
    }

    internal static class BurtScreenSpaceAmbientOcclusionRenderTargetUtility
    {
        public static void Allocate(
            BurtRenderGraphContext context,
            string passName,
            int textureId,
            BurtRenderTargetHandle target,
            RenderTextureDescriptor descriptor,
            FilterMode filterMode)
        {
            if (context == null)
            {
                return;
            }

            var cmd = context.AcquireCommandBuffer(passName);
            cmd.GetTemporaryRT(textureId, descriptor, filterMode);
            cmd.SetRenderTarget(target.Identifier);
            cmd.ClearRenderTarget(false, true, Color.white);
            cmd.SetGlobalTexture(textureId, target.Identifier);
            context.ExecuteAndReleaseCommandBuffer(cmd);
        }

        public static void Release(BurtRenderGraphContext context, string passName, int textureId)
        {
            if (context == null)
            {
                return;
            }

            var cmd = context.AcquireCommandBuffer(passName);
            cmd.ReleaseTemporaryRT(textureId);
            context.ExecuteAndReleaseCommandBuffer(cmd);
        }
    }
}
