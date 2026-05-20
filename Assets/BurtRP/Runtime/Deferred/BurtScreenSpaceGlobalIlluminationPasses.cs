using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    internal sealed class BurtAllocateScreenSpaceGlobalIlluminationRawPass : BurtRenderPass
    {
        public override string Name => "Burt Allocate Screen Space Global Illumination Raw";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIllumination(builder.Request, builder.Asset))
            {
                return;
            }

            builder.WriteScreenSpaceGlobalIlluminationRaw();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIllumination(context.Request, context.Asset))
            {
                return;
            }

            var target = context.ScreenSpaceGlobalIlluminationRawTarget;
            if (!target.IsValid)
            {
                return;
            }

            var camera = context.Request != null ? context.Request.Camera : null;
            var settings = BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationSettings(context.Request, context.Asset);
            var descriptor = BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationDescriptor(camera, settings);
            BurtScreenSpaceGlobalIlluminationRenderTargetUtility.Allocate(context, Name, BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationRawTextureId, target, descriptor, FilterMode.Bilinear);
        }
    }

    internal sealed class BurtAllocateScreenSpaceGlobalIlluminationPass : BurtRenderPass
    {
        public override string Name => "Burt Allocate Screen Space Global Illumination";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIllumination(builder.Request, builder.Asset))
            {
                return;
            }

            builder.WriteScreenSpaceGlobalIllumination();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIllumination(context.Request, context.Asset))
            {
                return;
            }

            var target = context.ScreenSpaceGlobalIlluminationTarget;
            if (!target.IsValid)
            {
                return;
            }

            var camera = context.Request != null ? context.Request.Camera : null;
            var settings = BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationSettings(context.Request, context.Asset);
            var descriptor = BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationDescriptor(camera, settings);
            BurtScreenSpaceGlobalIlluminationRenderTargetUtility.Allocate(context, Name, BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationTextureId, target, descriptor, FilterMode.Bilinear);
        }
    }

    internal abstract class BurtScreenSpaceGlobalIlluminationPass : BurtRenderPass
    {
        protected const string ScreenSpaceGlobalIlluminationShaderName = BurtScreenSpaceGlobalIlluminationPassUtility.ScreenSpaceGlobalIlluminationShaderName;
        protected static readonly int CameraColorTextureId = BurtRenderGraphResourceRegistry.CameraColorTextureId;
        protected static readonly int CameraDepthTextureId = BurtRenderGraphResourceRegistry.CameraDepthTextureId;
        protected static readonly int GBuffer0Id = BurtRenderGraphResourceRegistry.GBuffer0Id;
        protected static readonly int GBuffer1Id = BurtRenderGraphResourceRegistry.GBuffer1Id;
        protected static readonly int GBuffer2Id = BurtRenderGraphResourceRegistry.GBuffer2Id;
        protected static readonly int GBuffer3Id = BurtRenderGraphResourceRegistry.GBuffer3Id;
        protected static readonly int GBuffer4Id = BurtRenderGraphResourceRegistry.GBuffer4Id;
        protected static readonly int XGIRawTextureId = BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationRawTextureId;
        protected static readonly int XGITextureId = BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationTextureId;
        protected static readonly int XGISourceColorTextureId = Shader.PropertyToID("_BurtXGISourceColorTexture");
        protected static readonly int XGICameraColorCopyTextureId = Shader.PropertyToID("_BurtXGICameraColorCopyTexture");
        protected static readonly int XGIDebugCameraColorTextureId = Shader.PropertyToID("_BurtXGIDebugCameraColorTexture");
        protected static readonly int XGIDebugCameraColorCopyTextureId = Shader.PropertyToID("_BurtXGIDebugCameraColorCopyTexture");
        protected static readonly int XGISourceTexelSizeId = Shader.PropertyToID("_BurtXGISourceTexelSize");
        protected static readonly int XGIViewMatrixId = Shader.PropertyToID("_BurtXGIViewMatrix");
        protected static readonly int XGIViewProjectionMatrixId = Shader.PropertyToID("_BurtXGIViewProjectionMatrix");
        protected static readonly int XGIParams0Id = Shader.PropertyToID("_BurtXGIParams0");
        protected static readonly int XGIParams1Id = Shader.PropertyToID("_BurtXGIParams1");
        protected static readonly int XGIParams2Id = Shader.PropertyToID("_BurtXGIParams2");
        protected static readonly int XGIDebugModeId = Shader.PropertyToID("_BurtXGIDebugMode");
        protected static readonly int InverseViewProjectionMatrixId = Shader.PropertyToID("_BurtDeferredInverseViewProjectionMatrix");
        protected static readonly int CameraWorldPositionId = Shader.PropertyToID("_BurtDeferredCameraWorldPosition");
        protected static readonly int DeferredScreenSizeId = Shader.PropertyToID("_BurtDeferredScreenSize");

        private Material screenSpaceGlobalIlluminationMaterial;
        private bool hasLoggedMissingShader;

        protected Material GetScreenSpaceGlobalIlluminationMaterial()
        {
            if (screenSpaceGlobalIlluminationMaterial != null)
            {
                return screenSpaceGlobalIlluminationMaterial;
            }

            var shader = Shader.Find(ScreenSpaceGlobalIlluminationShaderName);
            if (shader == null)
            {
                if (!hasLoggedMissingShader)
                {
                    Debug.LogWarning("BurtRP could not find shader: " + ScreenSpaceGlobalIlluminationShaderName);
                    hasLoggedMissingShader = true;
                }

                return null;
            }

            screenSpaceGlobalIlluminationMaterial = new Material(shader);
            screenSpaceGlobalIlluminationMaterial.hideFlags = HideFlags.HideAndDontSave;
            return screenSpaceGlobalIlluminationMaterial;
        }

        protected static void BindGBufferInputs(
            CommandBuffer cmd,
            BurtRenderTargetHandle gbuffer0Target,
            BurtRenderTargetHandle gbuffer1Target,
            BurtRenderTargetHandle gbuffer2Target,
            BurtRenderTargetHandle gbuffer3Target,
            BurtRenderTargetHandle gbuffer4Target)
        {
            cmd.SetGlobalTexture(GBuffer0Id, gbuffer0Target.Identifier);
            cmd.SetGlobalTexture(GBuffer1Id, gbuffer1Target.Identifier);
            cmd.SetGlobalTexture(GBuffer2Id, gbuffer2Target.Identifier);
            cmd.SetGlobalTexture(GBuffer3Id, gbuffer3Target.Identifier);
            cmd.SetGlobalTexture(GBuffer4Id, gbuffer4Target.Identifier);
        }

        protected static void UploadCameraGlobals(CommandBuffer cmd, Camera camera, RenderTextureDescriptor descriptor)
        {
            var viewMatrix = camera.worldToCameraMatrix;
            var projectionMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
            var viewProjectionMatrix = projectionMatrix * viewMatrix;
            var inverseViewProjectionMatrix = viewProjectionMatrix.inverse;
            var width = Mathf.Max(1, descriptor.width);
            var height = Mathf.Max(1, descriptor.height);
            var cameraPosition = camera.transform.position;

            BurtScreenSpaceGlobalIlluminationPassUtility.GetCameraTargetSize(camera, out var cameraWidth, out var cameraHeight);
            cmd.SetGlobalMatrix(XGIViewMatrixId, viewMatrix);
            cmd.SetGlobalMatrix(XGIViewProjectionMatrixId, viewProjectionMatrix);
            cmd.SetGlobalMatrix(InverseViewProjectionMatrixId, inverseViewProjectionMatrix);
            cmd.SetGlobalVector(CameraWorldPositionId, new Vector4(cameraPosition.x, cameraPosition.y, cameraPosition.z, 1f));
            cmd.SetGlobalVector(DeferredScreenSizeId, new Vector4(cameraWidth, cameraHeight, 1f / cameraWidth, 1f / cameraHeight));
            cmd.SetGlobalVector(XGISourceTexelSizeId, new Vector4(1f / width, 1f / height, width, height));
        }

        protected static void UploadSettings(CommandBuffer cmd, BurtScreenSpaceGlobalIlluminationSettings settings)
        {
            cmd.SetGlobalVector(XGIParams0Id, new Vector4(settings.Radius, settings.SampleCount, settings.MaxSteps, settings.Thickness));
            cmd.SetGlobalVector(XGIParams1Id, new Vector4(settings.Intensity, settings.SkyFallback, settings.Blur ? 1f : 0f, settings.BlurSharpness));
            cmd.SetGlobalVector(XGIParams2Id, new Vector4(Time.frameCount & 1023, settings.NormalWeight, settings.DistanceFade, settings.RadianceClamp));
        }
    }

    internal sealed class BurtScreenSpaceGlobalIlluminationTracePass : BurtScreenSpaceGlobalIlluminationPass
    {
        public override string Name => "Burt Screen Space Global Illumination Trace";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIllumination(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadCameraColor();
            builder.ReadCameraDepth();
            builder.ReadGBuffer0();
            builder.ReadGBuffer1();
            builder.ReadGBuffer2();
            builder.ReadGBuffer3();
            builder.ReadGBuffer4();
            builder.WriteScreenSpaceGlobalIlluminationRaw();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!TryGetTargets(context, out var cameraColorTarget, out var cameraDepthTarget, out var gbuffer0Target, out var gbuffer1Target, out var gbuffer2Target, out var gbuffer3Target, out var gbuffer4Target, out var rawTarget))
            {
                return;
            }

            var settings = BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationSettings(context.Request, context.Asset);
            if (!settings.Enabled)
            {
                return;
            }

            var material = GetScreenSpaceGlobalIlluminationMaterial();
            var camera = context.Request != null ? context.Request.Camera : null;
            if (material == null || camera == null)
            {
                return;
            }

            var descriptor = BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationDescriptor(camera, settings);
            var cmd = CommandBufferPool.Get(Name);
            cmd.SetRenderTarget(rawTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, descriptor.width, descriptor.height);
            cmd.SetGlobalTexture(CameraColorTextureId, cameraColorTarget.Identifier);
            cmd.SetGlobalTexture(XGISourceColorTextureId, cameraColorTarget.Identifier);
            cmd.SetGlobalTexture(CameraDepthTextureId, cameraDepthTarget.Identifier);
            BindGBufferInputs(cmd, gbuffer0Target, gbuffer1Target, gbuffer2Target, gbuffer3Target, gbuffer4Target);
            UploadCameraGlobals(cmd, camera, descriptor);
            UploadSettings(cmd, settings);
            cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1);
            cmd.SetGlobalTexture(XGIRawTextureId, rawTarget.Identifier);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private static bool TryGetTargets(
            BurtRenderGraphContext context,
            out BurtRenderTargetHandle cameraColorTarget,
            out BurtRenderTargetHandle cameraDepthTarget,
            out BurtRenderTargetHandle gbuffer0Target,
            out BurtRenderTargetHandle gbuffer1Target,
            out BurtRenderTargetHandle gbuffer2Target,
            out BurtRenderTargetHandle gbuffer3Target,
            out BurtRenderTargetHandle gbuffer4Target,
            out BurtRenderTargetHandle rawTarget)
        {
            cameraColorTarget = context != null ? context.CameraColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraColorName);
            cameraDepthTarget = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            gbuffer0Target = context != null ? context.GBuffer0Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer0Name);
            gbuffer1Target = context != null ? context.GBuffer1Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer1Name);
            gbuffer2Target = context != null ? context.GBuffer2Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer2Name);
            gbuffer3Target = context != null ? context.GBuffer3Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer3Name);
            gbuffer4Target = context != null ? context.GBuffer4Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer4Name);
            rawTarget = context != null ? context.ScreenSpaceGlobalIlluminationRawTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationRawName);

            return cameraColorTarget.IsValid &&
                cameraDepthTarget.IsValid &&
                gbuffer0Target.IsValid &&
                gbuffer1Target.IsValid &&
                gbuffer2Target.IsValid &&
                gbuffer3Target.IsValid &&
                gbuffer4Target.IsValid &&
                rawTarget.IsValid;
        }
    }

    internal sealed class BurtScreenSpaceGlobalIlluminationBlurPass : BurtScreenSpaceGlobalIlluminationPass
    {
        private const int BlurPassIndex = 1;
        private const int TemporalPassIndex = 4;
        private const int CopyDepthNormalPassIndex = 5;
        private const int CopyTemporalFinalPassIndex = 6;
        private static readonly int XGISpatialFinalTextureId = Shader.PropertyToID("_BurtScreenSpaceGlobalIlluminationSpatialFinalTexture");
        private static readonly int XGISpatialFinalInputTextureId = Shader.PropertyToID("_BurtXGISpatialFinalTexture");
        private static readonly int XGITemporalFinalTextureId = Shader.PropertyToID("_BurtScreenSpaceGlobalIlluminationTemporalFinalTexture");
        private static readonly int XGITemporalFinalInputTextureId = Shader.PropertyToID("_BurtXGITemporalFinalTexture");
        private static readonly int XGIDepthNormalTextureId = Shader.PropertyToID("_BurtXGIDepthNormalTexture");
        private static readonly int XGIHistoryTextureId = Shader.PropertyToID("_BurtXGIHistoryTexture");
        private static readonly int XGIHistoryDepthNormalTextureId = Shader.PropertyToID("_BurtXGIHistoryDepthNormalTexture");
        private static readonly int XGIPreviousViewProjectionMatrixId = Shader.PropertyToID("_BurtXGIPreviousViewProjectionMatrix");
        private static readonly int XGITemporalParamsId = Shader.PropertyToID("_BurtXGITemporalParams");
        private static readonly int XGITemporalParams1Id = Shader.PropertyToID("_BurtXGITemporalParams1");

        public override string Name => "Burt Screen Space Global Illumination Blur";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIllumination(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceGlobalIlluminationRaw();
            builder.ReadCameraDepth();
            builder.ReadGBuffer1();
            builder.WriteScreenSpaceGlobalIllumination();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!TryGetTargets(context, out var rawTarget, out var cameraDepthTarget, out var gbuffer1Target, out var target))
            {
                return;
            }

            var settings = BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationSettings(context.Request, context.Asset);
            if (!settings.Enabled)
            {
                return;
            }

            var material = GetScreenSpaceGlobalIlluminationMaterial();
            var camera = context.Request != null ? context.Request.Camera : null;
            if (material == null || camera == null)
            {
                return;
            }

            var descriptor = BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationDescriptor(camera, settings);
            var outputTarget = target.Identifier;
            var spatialFinalIdentifier = new RenderTargetIdentifier(XGISpatialFinalTextureId);
            var temporalFinalIdentifier = new RenderTargetIdentifier(XGITemporalFinalTextureId);
            var depthNormalIdentifier = new RenderTargetIdentifier(XGIDepthNormalTextureId);
            var cmd = CommandBufferPool.Get(Name);
            cmd.SetGlobalTexture(XGIRawTextureId, rawTarget.Identifier);
            cmd.SetGlobalTexture(CameraDepthTextureId, cameraDepthTarget.Identifier);
            cmd.SetGlobalTexture(GBuffer1Id, gbuffer1Target.Identifier);
            UploadCameraGlobals(cmd, camera, descriptor);
            UploadSettings(cmd, settings);

            var shouldUseTemporal = settings.TemporalAccumulation;
            if (shouldUseTemporal)
            {
                cmd.GetTemporaryRT(XGISpatialFinalTextureId, descriptor, FilterMode.Bilinear);
                cmd.GetTemporaryRT(XGITemporalFinalTextureId, BurtScreenSpaceGlobalIlluminationHistoryUtility.CreateColorHistoryDescriptor(camera, settings), FilterMode.Bilinear);
                outputTarget = spatialFinalIdentifier;
            }
            else
            {
                BurtScreenSpaceGlobalIlluminationHistoryUtility.InvalidateHistory(camera, "TemporalDisabled");
            }

            cmd.SetRenderTarget(outputTarget);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, descriptor.width, descriptor.height);
            cmd.DrawProcedural(Matrix4x4.identity, material, BlurPassIndex, MeshTopology.Triangles, 3, 1);

            if (shouldUseTemporal)
            {
                ResolveTemporal(cmd, material, context.Request, context.Asset, settings, camera, descriptor, spatialFinalIdentifier, temporalFinalIdentifier, target.Identifier, depthNormalIdentifier);
                cmd.ReleaseTemporaryRT(XGITemporalFinalTextureId);
                cmd.ReleaseTemporaryRT(XGISpatialFinalTextureId);
            }

            cmd.SetGlobalTexture(XGITextureId, target.Identifier);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private static void ResolveTemporal(
            CommandBuffer cmd,
            Material material,
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset,
            BurtScreenSpaceGlobalIlluminationSettings settings,
            Camera camera,
            RenderTextureDescriptor descriptor,
            RenderTargetIdentifier spatialFinal,
            RenderTargetIdentifier temporalFinalTarget,
            RenderTargetIdentifier finalTarget,
            RenderTargetIdentifier depthNormalTarget)
        {
            var history = BurtScreenSpaceGlobalIlluminationHistoryUtility.EnsureHistoryTextures(request, asset, settings, out var historyValid);
            var width = Mathf.Max(1, descriptor.width);
            var height = Mathf.Max(1, descriptor.height);
            cmd.SetGlobalTexture(XGISpatialFinalInputTextureId, spatialFinal);
            cmd.SetGlobalTexture(XGIHistoryTextureId, history.Color != null ? (Texture)history.Color : Texture2D.blackTexture);
            cmd.SetGlobalTexture(XGIHistoryDepthNormalTextureId, history.DepthNormal != null ? (Texture)history.DepthNormal : Texture2D.blackTexture);
            cmd.SetGlobalMatrix(XGIPreviousViewProjectionMatrixId, history.PreviousViewProjectionMatrix);
            cmd.SetGlobalMatrix(InverseViewProjectionMatrixId, history.CurrentInverseViewProjectionMatrix);
            cmd.SetGlobalVector(XGISourceTexelSizeId, new Vector4(1f / width, 1f / height, width, height));
            cmd.SetGlobalVector(XGITemporalParamsId, new Vector4(settings.TemporalFeedback, historyValid ? 1f : 0f, settings.TemporalDepthRejection, settings.TemporalNormalRejection));
            cmd.SetGlobalVector(XGITemporalParams1Id, new Vector4(settings.TemporalClamp, 0f, 0f, 0f));

            cmd.SetRenderTarget(temporalFinalTarget);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, width, height);
            cmd.DrawProcedural(Matrix4x4.identity, material, TemporalPassIndex, MeshTopology.Triangles, 3, 1);

            cmd.SetGlobalTexture(XGITemporalFinalInputTextureId, temporalFinalTarget);
            cmd.SetRenderTarget(finalTarget);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, width, height);
            cmd.DrawProcedural(Matrix4x4.identity, material, CopyTemporalFinalPassIndex, MeshTopology.Triangles, 3, 1);

            if (history.Color == null || history.DepthNormal == null)
            {
                return;
            }

            cmd.CopyTexture(temporalFinalTarget, new RenderTargetIdentifier(history.Color));
            var depthNormalDescriptor = BurtScreenSpaceGlobalIlluminationHistoryUtility.CreateDepthNormalHistoryDescriptor(camera, settings);
            cmd.GetTemporaryRT(XGIDepthNormalTextureId, depthNormalDescriptor, FilterMode.Point);
            cmd.SetRenderTarget(depthNormalTarget);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, width, height);
            cmd.DrawProcedural(Matrix4x4.identity, material, CopyDepthNormalPassIndex, MeshTopology.Triangles, 3, 1);
            cmd.CopyTexture(depthNormalTarget, new RenderTargetIdentifier(history.DepthNormal));
            cmd.ReleaseTemporaryRT(XGIDepthNormalTextureId);
            BurtScreenSpaceGlobalIlluminationHistoryUtility.MarkHistoryValid(camera);
        }

        private static bool TryGetTargets(
            BurtRenderGraphContext context,
            out BurtRenderTargetHandle rawTarget,
            out BurtRenderTargetHandle cameraDepthTarget,
            out BurtRenderTargetHandle gbuffer1Target,
            out BurtRenderTargetHandle target)
        {
            rawTarget = context != null ? context.ScreenSpaceGlobalIlluminationRawTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationRawName);
            cameraDepthTarget = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            gbuffer1Target = context != null ? context.GBuffer1Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer1Name);
            target = context != null ? context.ScreenSpaceGlobalIlluminationTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationName);

            return rawTarget.IsValid && cameraDepthTarget.IsValid && gbuffer1Target.IsValid && target.IsValid;
        }
    }

    internal sealed class BurtScreenSpaceGlobalIlluminationCompositePass : BurtScreenSpaceGlobalIlluminationPass
    {
        public override string Name => "Burt Screen Space Global Illumination Composite";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIllumination(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadCameraColor();
            builder.ReadCameraDepth();
            builder.ReadScreenSpaceGlobalIllumination();
            builder.WriteCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!TryGetTargets(context, out var cameraColorTarget, out var cameraDepthTarget, out var xgiTarget))
            {
                return;
            }

            var settings = BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationSettings(context.Request, context.Asset);
            if (!settings.Enabled)
            {
                return;
            }

            var material = GetScreenSpaceGlobalIlluminationMaterial();
            var camera = context.Request != null ? context.Request.Camera : null;
            if (material == null || camera == null)
            {
                return;
            }

            var xgiDescriptor = BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationDescriptor(camera, settings);
            var cameraColorDescriptor = BurtRenderTargetDescriptorUtility.CreateCameraColorDescriptor(camera);
            var cmd = CommandBufferPool.Get(Name);
            cmd.GetTemporaryRT(XGICameraColorCopyTextureId, cameraColorDescriptor, FilterMode.Bilinear);
            cmd.Blit(cameraColorTarget.Identifier, new RenderTargetIdentifier(XGICameraColorCopyTextureId));
            cmd.SetRenderTarget(cameraColorTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            cmd.SetGlobalTexture(XGICameraColorCopyTextureId, new RenderTargetIdentifier(XGICameraColorCopyTextureId));
            cmd.SetGlobalTexture(CameraDepthTextureId, cameraDepthTarget.Identifier);
            cmd.SetGlobalTexture(XGITextureId, xgiTarget.Identifier);
            UploadCameraGlobals(cmd, camera, xgiDescriptor);
            UploadSettings(cmd, settings);
            cmd.DrawProcedural(Matrix4x4.identity, material, 2, MeshTopology.Triangles, 3, 1);
            cmd.ReleaseTemporaryRT(XGICameraColorCopyTextureId);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private static bool TryGetTargets(
            BurtRenderGraphContext context,
            out BurtRenderTargetHandle cameraColorTarget,
            out BurtRenderTargetHandle cameraDepthTarget,
            out BurtRenderTargetHandle xgiTarget)
        {
            cameraColorTarget = context != null ? context.CameraColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraColorName);
            cameraDepthTarget = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            xgiTarget = context != null ? context.ScreenSpaceGlobalIlluminationTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationName);
            return cameraColorTarget.IsValid && cameraDepthTarget.IsValid && xgiTarget.IsValid;
        }
    }

    internal sealed class BurtDebugScreenSpaceGlobalIlluminationPass : BurtScreenSpaceGlobalIlluminationPass
    {
        private const int DebugPassIndex = 3;

        public override string Name => "Burt Debug Screen Space Global Illumination";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIlluminationDebugView(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceGlobalIlluminationRaw();
            builder.ReadScreenSpaceGlobalIllumination();
            if (BurtScreenSpaceGlobalIlluminationPassUtility.IsScreenSpaceGlobalIlluminationOverlayDebugMode(BurtShadingDebugSettings.Mode))
            {
                builder.ReadCameraColor();
            }

            builder.WriteCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIlluminationDebugView(context.Request, context.Asset))
            {
                return;
            }

            var cameraColorTarget = context.CameraColorTarget;
            var rawTarget = context.ScreenSpaceGlobalIlluminationRawTarget;
            var xgiTarget = context.ScreenSpaceGlobalIlluminationTarget;
            if (!cameraColorTarget.IsValid || !rawTarget.IsValid || !xgiTarget.IsValid)
            {
                return;
            }

            var material = GetScreenSpaceGlobalIlluminationMaterial();
            var camera = context.Request != null ? context.Request.Camera : null;
            if (material == null || camera == null)
            {
                return;
            }

            var settings = BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationSettings(context.Request, context.Asset);
            if (!settings.Enabled)
            {
                return;
            }

            var descriptor = BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationDescriptor(camera, settings);
            var cmd = CommandBufferPool.Get(Name);
            var isOverlay = BurtScreenSpaceGlobalIlluminationPassUtility.IsScreenSpaceGlobalIlluminationOverlayDebugMode(BurtShadingDebugSettings.Mode);
            if (isOverlay)
            {
                var cameraColorDescriptor = BurtRenderTargetDescriptorUtility.CreateCameraColorDescriptor(camera);
                cmd.GetTemporaryRT(XGIDebugCameraColorCopyTextureId, cameraColorDescriptor, FilterMode.Bilinear);
                cmd.Blit(cameraColorTarget.Identifier, new RenderTargetIdentifier(XGIDebugCameraColorCopyTextureId));
                cmd.SetGlobalTexture(XGIDebugCameraColorTextureId, new RenderTargetIdentifier(XGIDebugCameraColorCopyTextureId));
            }

            cmd.SetRenderTarget(cameraColorTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            cmd.SetGlobalTexture(XGIRawTextureId, rawTarget.Identifier);
            cmd.SetGlobalTexture(XGITextureId, xgiTarget.Identifier);
            cmd.SetGlobalFloat(XGIDebugModeId, BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationShaderDebugMode());
            UploadCameraGlobals(cmd, camera, descriptor);
            UploadSettings(cmd, settings);
            cmd.DrawProcedural(Matrix4x4.identity, material, DebugPassIndex, MeshTopology.Triangles, 3, 1);
            if (isOverlay)
            {
                cmd.ReleaseTemporaryRT(XGIDebugCameraColorCopyTextureId);
            }

            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    internal sealed class BurtReleaseScreenSpaceGlobalIlluminationRawPass : BurtRenderPass
    {
        public override string Name => "Burt Release Screen Space Global Illumination Raw";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIllumination(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceGlobalIlluminationRaw();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIllumination(context.Request, context.Asset))
            {
                return;
            }

            var target = context.ScreenSpaceGlobalIlluminationRawTarget;
            if (!target.IsValid)
            {
                return;
            }

            BurtScreenSpaceGlobalIlluminationRenderTargetUtility.Release(context, Name, BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationRawTextureId);
        }
    }

    internal sealed class BurtReleaseScreenSpaceGlobalIlluminationPass : BurtRenderPass
    {
        public override string Name => "Burt Release Screen Space Global Illumination";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIllumination(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceGlobalIllumination();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIllumination(context.Request, context.Asset))
            {
                return;
            }

            var target = context.ScreenSpaceGlobalIlluminationTarget;
            if (!target.IsValid)
            {
                return;
            }

            BurtScreenSpaceGlobalIlluminationRenderTargetUtility.Release(context, Name, BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationTextureId);
        }
    }

    internal readonly struct BurtScreenSpaceGlobalIlluminationSettings
    {
        public static readonly BurtScreenSpaceGlobalIlluminationSettings Disabled = new BurtScreenSpaceGlobalIlluminationSettings(false, BurtScreenSpaceGlobalIlluminationQuality.Medium, BurtScreenSpaceGlobalIlluminationResolution.Half, 0.6f, 2f, 12, 8, 0.35f, 1f, 8f, 0.65f, 80f, true, 0.18f, true, 0.86f, 0.02f, 0.65f, 1f);

        public BurtScreenSpaceGlobalIlluminationSettings(
            bool enabled,
            BurtScreenSpaceGlobalIlluminationQuality quality,
            BurtScreenSpaceGlobalIlluminationResolution resolution,
            float intensity,
            float radius,
            int sampleCount,
            int maxSteps,
            float thickness,
            float skyFallback,
            float radianceClamp,
            float normalWeight,
            float distanceFade,
            bool blur,
            float blurSharpness,
            bool temporalAccumulation,
            float temporalFeedback,
            float temporalDepthRejection,
            float temporalNormalRejection,
            float temporalClamp)
        {
            Enabled = enabled;
            Quality = NormalizeQuality(quality);
            Resolution = NormalizeResolution(resolution);
            Intensity = Mathf.Clamp(intensity, 0f, 4f);
            Radius = Mathf.Clamp(radius, 0.05f, 20f);
            SampleCount = Mathf.Clamp(sampleCount, 1, 32);
            MaxSteps = Mathf.Clamp(maxSteps, 1, 64);
            Thickness = Mathf.Clamp(thickness, 0.01f, 3f);
            SkyFallback = Mathf.Clamp(skyFallback, 0f, 2f);
            RadianceClamp = Mathf.Clamp(radianceClamp, 0.1f, 64f);
            NormalWeight = Mathf.Clamp01(normalWeight);
            DistanceFade = Mathf.Clamp(distanceFade, 1f, 800f);
            Blur = blur;
            BlurSharpness = Mathf.Clamp01(blurSharpness);
            TemporalAccumulation = temporalAccumulation;
            TemporalFeedback = Mathf.Clamp(temporalFeedback, 0f, 0.98f);
            TemporalDepthRejection = Mathf.Clamp(temporalDepthRejection, 0.001f, 0.2f);
            TemporalNormalRejection = Mathf.Clamp01(temporalNormalRejection);
            TemporalClamp = Mathf.Clamp(temporalClamp, 0.25f, 4f);
        }

        public bool Enabled { get; }
        public BurtScreenSpaceGlobalIlluminationQuality Quality { get; }
        public BurtScreenSpaceGlobalIlluminationResolution Resolution { get; }
        public float Intensity { get; }
        public float Radius { get; }
        public int SampleCount { get; }
        public int MaxSteps { get; }
        public float Thickness { get; }
        public float SkyFallback { get; }
        public float RadianceClamp { get; }
        public float NormalWeight { get; }
        public float DistanceFade { get; }
        public bool Blur { get; }
        public float BlurSharpness { get; }
        public bool TemporalAccumulation { get; }
        public float TemporalFeedback { get; }
        public float TemporalDepthRejection { get; }
        public float TemporalNormalRejection { get; }
        public float TemporalClamp { get; }

        public int CreateHistorySignature()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + (Enabled ? 1 : 0);
                hash = hash * 31 + (int)Quality;
                hash = hash * 31 + (int)Resolution;
                hash = hash * 31 + Quantize(Intensity, 1000f);
                hash = hash * 31 + Quantize(Radius, 1000f);
                hash = hash * 31 + SampleCount;
                hash = hash * 31 + MaxSteps;
                hash = hash * 31 + Quantize(Thickness, 1000f);
                hash = hash * 31 + Quantize(SkyFallback, 1000f);
                hash = hash * 31 + Quantize(RadianceClamp, 1000f);
                hash = hash * 31 + Quantize(NormalWeight, 1000f);
                hash = hash * 31 + Quantize(DistanceFade, 100f);
                hash = hash * 31 + (Blur ? 1 : 0);
                hash = hash * 31 + Quantize(BlurSharpness, 1000f);
                hash = hash * 31 + (TemporalAccumulation ? 1 : 0);
                hash = hash * 31 + Quantize(TemporalFeedback, 1000f);
                hash = hash * 31 + Quantize(TemporalDepthRejection, 10000f);
                hash = hash * 31 + Quantize(TemporalNormalRejection, 1000f);
                hash = hash * 31 + Quantize(TemporalClamp, 1000f);
                return hash;
            }
        }

        private static int Quantize(float value, float scale)
        {
            return Mathf.RoundToInt(value * scale);
        }

        private static BurtScreenSpaceGlobalIlluminationQuality NormalizeQuality(BurtScreenSpaceGlobalIlluminationQuality quality)
        {
            switch (quality)
            {
                case BurtScreenSpaceGlobalIlluminationQuality.Custom:
                case BurtScreenSpaceGlobalIlluminationQuality.Low:
                case BurtScreenSpaceGlobalIlluminationQuality.Medium:
                case BurtScreenSpaceGlobalIlluminationQuality.High:
                    return quality;
                default:
                    return BurtScreenSpaceGlobalIlluminationQuality.Medium;
            }
        }

        private static BurtScreenSpaceGlobalIlluminationResolution NormalizeResolution(BurtScreenSpaceGlobalIlluminationResolution resolution)
        {
            return resolution == BurtScreenSpaceGlobalIlluminationResolution.Full ? BurtScreenSpaceGlobalIlluminationResolution.Full : BurtScreenSpaceGlobalIlluminationResolution.Half;
        }
    }

    internal readonly struct BurtScreenSpaceGlobalIlluminationHistoryMatrices
    {
        public Matrix4x4 ViewProjectionMatrix { get; }
        public Matrix4x4 InverseViewProjectionMatrix { get; }
        public Matrix4x4 NonJitteredProjectionMatrix { get; }

        public BurtScreenSpaceGlobalIlluminationHistoryMatrices(
            Matrix4x4 viewProjectionMatrix,
            Matrix4x4 inverseViewProjectionMatrix,
            Matrix4x4 nonJitteredProjectionMatrix)
        {
            ViewProjectionMatrix = viewProjectionMatrix;
            InverseViewProjectionMatrix = inverseViewProjectionMatrix;
            NonJitteredProjectionMatrix = nonJitteredProjectionMatrix;
        }
    }

    internal readonly struct BurtScreenSpaceGlobalIlluminationHistoryTextures
    {
        public RenderTexture Color { get; }
        public RenderTexture DepthNormal { get; }
        public Matrix4x4 PreviousViewProjectionMatrix { get; }
        public Matrix4x4 CurrentInverseViewProjectionMatrix { get; }

        public BurtScreenSpaceGlobalIlluminationHistoryTextures(
            RenderTexture color,
            RenderTexture depthNormal,
            Matrix4x4 previousViewProjectionMatrix,
            Matrix4x4 currentInverseViewProjectionMatrix)
        {
            Color = color;
            DepthNormal = depthNormal;
            PreviousViewProjectionMatrix = previousViewProjectionMatrix;
            CurrentInverseViewProjectionMatrix = currentInverseViewProjectionMatrix;
        }

        public static BurtScreenSpaceGlobalIlluminationHistoryTextures CreateInvalid(BurtScreenSpaceGlobalIlluminationHistoryMatrices matrices)
        {
            return new BurtScreenSpaceGlobalIlluminationHistoryTextures(null, null, matrices.ViewProjectionMatrix, matrices.InverseViewProjectionMatrix);
        }
    }

    internal readonly struct BurtScreenSpaceGlobalIlluminationHistoryStatus
    {
        public bool HasHistory { get; }
        public bool DescriptorMatches { get; }
        public bool HasDepthNormalHistory { get; }
        public bool DepthNormalDescriptorMatches { get; }
        public int Width { get; }
        public int Height { get; }
        public RenderTextureFormat Format { get; }
        public int FrameIndex { get; }
        public int HistoryAge { get; }
        public int FirstValidFrameIndex { get; }
        public int LastInvalidationFrameIndex { get; }
        public string LastInvalidationReason { get; }

        public BurtScreenSpaceGlobalIlluminationHistoryStatus(
            bool hasHistory,
            bool descriptorMatches,
            bool hasDepthNormalHistory,
            bool depthNormalDescriptorMatches,
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
            HasDepthNormalHistory = hasDepthNormalHistory;
            DepthNormalDescriptorMatches = depthNormalDescriptorMatches;
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

    internal static class BurtScreenSpaceGlobalIlluminationHistoryUtility
    {
        private const int HistoryAlgorithmVersion = 1;
        private const int CameraStatePruneInterval = 128;
        private const float ProjectionChangeEpsilon = 0.0001f;

        private sealed class CameraState
        {
            public Camera Camera;
            public RenderTexture ColorHistory;
            public RenderTexture DepthNormalHistory;
            public RenderTextureDescriptor ColorDescriptor;
            public RenderTextureDescriptor DepthNormalDescriptor;
            public int AlgorithmVersion;
            public bool HasValidHistory;
            public bool HasPreviousCameraState;
            public bool HasSettingsSignature;
            public int SettingsSignature;
            public int FrameIndex;
            public int FirstValidFrameIndex;
            public int LastInvalidationFrameIndex;
            public Matrix4x4 CurrentViewProjectionMatrix = Matrix4x4.identity;
            public Matrix4x4 CurrentInverseViewProjectionMatrix = Matrix4x4.identity;
            public Matrix4x4 CurrentNonJitteredProjectionMatrix = Matrix4x4.identity;
            public Matrix4x4 PreviousViewProjectionMatrix = Matrix4x4.identity;
            public Matrix4x4 PreviousNonJitteredProjectionMatrix = Matrix4x4.identity;
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
            public string LastInvalidationReason = "NeverAllocated";
        }

        private static readonly System.Collections.Generic.Dictionary<int, CameraState> CameraStates = new System.Collections.Generic.Dictionary<int, CameraState>();
        private static readonly System.Collections.Generic.List<int> CameraStateRemovalKeys = new System.Collections.Generic.List<int>();
        private static int cameraStatePruneCounter;

        public static BurtScreenSpaceGlobalIlluminationHistoryTextures EnsureHistoryTextures(
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset,
            BurtScreenSpaceGlobalIlluminationSettings settings,
            out bool historyValid)
        {
            historyValid = false;
            var camera = request != null ? request.Camera : null;
            var matrices = CreateCurrentMatrices(request);
            if (camera == null)
            {
                return BurtScreenSpaceGlobalIlluminationHistoryTextures.CreateInvalid(matrices);
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

            var colorDescriptor = CreateColorHistoryDescriptor(camera, settings);
            var depthNormalDescriptor = CreateDepthNormalHistoryDescriptor(camera, settings);
            var colorMatches = state.ColorHistory != null && Matches(state.ColorDescriptor, colorDescriptor);
            var depthNormalMatches = state.DepthNormalHistory != null && Matches(state.DepthNormalDescriptor, depthNormalDescriptor);
            var descriptorsMatch = colorMatches && depthNormalMatches;
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

            if (state.ColorHistory == null)
            {
                state.ColorDescriptor = colorDescriptor;
                state.ColorHistory = CreateHistoryTexture(colorDescriptor, "Burt XGI Color History " + camera.GetInstanceID(), FilterMode.Bilinear);
                SetAllocationInvalidationReason(state, "HistoryAllocated");
            }

            if (state.DepthNormalHistory == null)
            {
                state.DepthNormalDescriptor = depthNormalDescriptor;
                state.DepthNormalHistory = CreateHistoryTexture(depthNormalDescriptor, "Burt XGI Depth Normal History " + camera.GetInstanceID(), FilterMode.Point);
                SetAllocationInvalidationReason(state, "DepthNormalHistoryAllocated");
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

            historyValid = state.HasValidHistory && state.HasPreviousCameraState && state.ColorHistory != null && state.DepthNormalHistory != null;
            var previousViewProjectionMatrix = state.HasPreviousCameraState ? state.PreviousViewProjectionMatrix : matrices.ViewProjectionMatrix;
            return new BurtScreenSpaceGlobalIlluminationHistoryTextures(state.ColorHistory, state.DepthNormalHistory, previousViewProjectionMatrix, matrices.InverseViewProjectionMatrix);
        }

        public static BurtScreenSpaceGlobalIlluminationHistoryMatrices CreateCurrentMatrices(BurtRenderRequest request)
        {
            var camera = request != null ? request.Camera : null;
            if (camera == null)
            {
                return new BurtScreenSpaceGlobalIlluminationHistoryMatrices(Matrix4x4.identity, Matrix4x4.identity, Matrix4x4.identity);
            }

            var temporalAA = request.TemporalAA;
            var viewMatrix = temporalAA != null ? temporalAA.ViewMatrix : camera.worldToCameraMatrix;
            var projectionMatrix = temporalAA != null ? temporalAA.JitteredProjectionMatrix : camera.projectionMatrix;
            var nonJitteredProjectionMatrix = temporalAA != null ? temporalAA.NonJitteredProjectionMatrix : camera.projectionMatrix;
            var viewProjectionMatrix = temporalAA != null ? temporalAA.CurrentViewProjectionMatrix : GL.GetGPUProjectionMatrix(projectionMatrix, true) * viewMatrix;
            var inverseViewProjectionMatrix = temporalAA != null ? temporalAA.InverseCurrentViewProjectionMatrix : viewProjectionMatrix.inverse;
            return new BurtScreenSpaceGlobalIlluminationHistoryMatrices(viewProjectionMatrix, inverseViewProjectionMatrix, nonJitteredProjectionMatrix);
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

        public static BurtScreenSpaceGlobalIlluminationHistoryStatus GetHistoryStatus(Camera camera)
        {
            if (camera == null || !CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
            {
                return new BurtScreenSpaceGlobalIlluminationHistoryStatus(false, false, false, false, 0, 0, RenderTextureFormat.Default, 0, 0, 0, 0, "NoCameraOrHistory");
            }

            var settings = BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationHistorySettings(camera);
            var colorDescriptor = CreateColorHistoryDescriptor(camera, settings);
            var depthNormalDescriptor = CreateDepthNormalHistoryDescriptor(camera, settings);
            var hasColor = state.ColorHistory != null;
            var hasDepthNormal = state.DepthNormalHistory != null;
            var historyAge = state.HasValidHistory && state.FirstValidFrameIndex > 0 ? Mathf.Max(0, state.FrameIndex - state.FirstValidFrameIndex + 1) : 0;
            return new BurtScreenSpaceGlobalIlluminationHistoryStatus(
                state.HasValidHistory && hasColor && hasDepthNormal,
                hasColor && Matches(state.ColorDescriptor, colorDescriptor),
                hasDepthNormal,
                hasDepthNormal && Matches(state.DepthNormalDescriptor, depthNormalDescriptor),
                hasColor ? state.ColorHistory.width : 0,
                hasColor ? state.ColorHistory.height : 0,
                hasColor ? state.ColorHistory.format : RenderTextureFormat.Default,
                state.FrameIndex,
                historyAge,
                state.FirstValidFrameIndex,
                state.LastInvalidationFrameIndex,
                state.LastInvalidationReason);
        }

        public static RenderTextureDescriptor CreateColorHistoryDescriptor(Camera camera, BurtScreenSpaceGlobalIlluminationSettings settings)
        {
            var descriptor = BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationDescriptor(camera, settings);
            descriptor.colorFormat = RenderTextureFormat.ARGBHalf;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.mipCount = 1;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            descriptor.sRGB = false;
            return descriptor;
        }

        public static RenderTextureDescriptor CreateDepthNormalHistoryDescriptor(Camera camera, BurtScreenSpaceGlobalIlluminationSettings settings)
        {
            var descriptor = BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationDescriptor(camera, settings);
            descriptor.colorFormat = RenderTextureFormat.ARGBHalf;
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
            if (!descriptorsMatch)
            {
                return "DescriptorChanged";
            }

            if (!state.HasPreviousCameraState)
            {
                return state.HasValidHistory ? "MissingPreviousCameraState" : null;
            }

            if (state.PreviousRendererMode != rendererMode)
            {
                return "RendererModeChanged";
            }

            if (state.PreviousTargetTextureId != GetTargetTextureId(camera) ||
                state.PreviousTargetWidth != targetWidth ||
                state.PreviousTargetHeight != targetHeight)
            {
                return "TargetChanged";
            }

            if (state.PreviousOrthographic != camera.orthographic)
            {
                return "ProjectionModeChanged";
            }

            if (camera.orthographic)
            {
                if (Mathf.Abs(state.PreviousOrthographicSize - camera.orthographicSize) > ProjectionChangeEpsilon)
                {
                    return "OrthographicSizeChanged";
                }
            }
            else if (Mathf.Abs(state.PreviousFieldOfView - camera.fieldOfView) > ProjectionChangeEpsilon)
            {
                return "FieldOfViewChanged";
            }

            if (Mathf.Abs(state.PreviousNearClipPlane - camera.nearClipPlane) > ProjectionChangeEpsilon)
            {
                return "NearClipChanged";
            }

            if (Mathf.Abs(state.PreviousFarClipPlane - camera.farClipPlane) > ProjectionChangeEpsilon)
            {
                return "FarClipChanged";
            }

            if (!Approximately(state.PreviousNonJitteredProjectionMatrix, nonJitteredProjectionMatrix))
            {
                return "ProjectionMatrixChanged";
            }

            return null;
        }

        private static void CaptureCurrentCameraState(Camera camera, CameraState state, int targetWidth, int targetHeight)
        {
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

        private static bool Approximately(Matrix4x4 a, Matrix4x4 b)
        {
            for (var i = 0; i < 16; i++)
            {
                if (Mathf.Abs(a[i] - b[i]) > ProjectionChangeEpsilon)
                {
                    return false;
                }
            }

            return true;
        }

        private static void GetTargetSize(Camera camera, out int width, out int height)
        {
            BurtScreenSpaceGlobalIlluminationPassUtility.GetCameraTargetSize(camera, out width, out height);
        }

        private static int GetTargetTextureId(Camera camera)
        {
            return camera != null && camera.targetTexture != null ? camera.targetTexture.GetInstanceID() : 0;
        }

        private static void SetAllocationInvalidationReason(CameraState state, string reason)
        {
            if (state == null || state.HasValidHistory)
            {
                return;
            }

            state.LastInvalidationReason = reason;
        }

        private static void InvalidateState(CameraState state, string reason)
        {
            if (state == null)
            {
                return;
            }

            state.HasValidHistory = false;
            state.LastInvalidationReason = string.IsNullOrEmpty(reason) ? "Manual" : reason;
            state.LastInvalidationFrameIndex = state.FrameIndex;
        }

        private static void ReleaseHistory(CameraState state)
        {
            if (state == null)
            {
                return;
            }

            ReleaseTexture(state.ColorHistory);
            ReleaseTexture(state.DepthNormalHistory);
            state.ColorHistory = null;
            state.DepthNormalHistory = null;
            state.HasValidHistory = false;
            state.HasSettingsSignature = false;
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

    internal static class BurtScreenSpaceGlobalIlluminationPassUtility
    {
        public const string ScreenSpaceGlobalIlluminationShaderName = "Hidden/BurtRP/ScreenSpaceGlobalIllumination";
        private static int shaderAvailabilityFrame = -1;
        private static bool shaderAvailable;

        public static bool ShouldUseScreenSpaceGlobalIllumination(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return ResolveScreenSpaceGlobalIlluminationSettings(request, asset).Enabled;
        }

        public static bool ShouldUseScreenSpaceGlobalIlluminationDebugView(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return IsScreenSpaceGlobalIlluminationDebugMode(BurtShadingDebugSettings.Mode) && ShouldUseScreenSpaceGlobalIllumination(request, asset);
        }

        public static bool IsScreenSpaceGlobalIlluminationDebugMode(BurtShadingDebugMode mode)
        {
            return mode == BurtShadingDebugMode.ScreenSpaceGlobalIlluminationRaw ||
                mode == BurtShadingDebugMode.ScreenSpaceGlobalIlluminationFinal ||
                mode == BurtShadingDebugMode.ScreenSpaceGlobalIlluminationHitRatio ||
                mode == BurtShadingDebugMode.ScreenSpaceGlobalIlluminationOverlay ||
                mode == BurtShadingDebugMode.ScreenSpaceGlobalIlluminationComposite;
        }

        public static bool IsScreenSpaceGlobalIlluminationOverlayDebugMode(BurtShadingDebugMode mode)
        {
            return mode == BurtShadingDebugMode.ScreenSpaceGlobalIlluminationOverlay;
        }

        public static BurtScreenSpaceGlobalIlluminationSettings ResolveScreenSpaceGlobalIlluminationSettings(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            if (request == null || !request.IsValid || request.Camera == null)
            {
                return BurtScreenSpaceGlobalIlluminationSettings.Disabled;
            }

            if (request.Type == BurtRenderRequestType.Preview || request.Type == BurtRenderRequestType.Reflection)
            {
                return BurtScreenSpaceGlobalIlluminationSettings.Disabled;
            }

            if (asset == null || asset.RendererMode != BurtRendererMode.Deferred)
            {
                return BurtScreenSpaceGlobalIlluminationSettings.Disabled;
            }

            if (IsScreenSpaceGlobalIlluminationSuppressedByShadingDebug())
            {
                return BurtScreenSpaceGlobalIlluminationSettings.Disabled;
            }

            var component = GetScreenSpaceGlobalIlluminationVolumeComponent();
            if (component == null || !component.IsEnabled())
            {
                return BurtScreenSpaceGlobalIlluminationSettings.Disabled;
            }

            if (!IsScreenSpaceGlobalIlluminationShaderAvailable())
            {
                return BurtScreenSpaceGlobalIlluminationSettings.Disabled;
            }

            return CreateScreenSpaceGlobalIlluminationSettings(component);
        }

        public static RenderTextureDescriptor CreateScreenSpaceGlobalIlluminationDescriptor(Camera camera, BurtScreenSpaceGlobalIlluminationSettings settings)
        {
            var descriptor = BurtRenderTargetDescriptorUtility.CreateScreenSpaceGlobalIlluminationDescriptor(camera);
            ApplyScreenSpaceGlobalIlluminationResolution(ref descriptor, settings);
            return descriptor;
        }

        public static void GetCameraTargetSize(Camera camera, out int width, out int height)
        {
            if (camera != null && camera.targetTexture != null)
            {
                width = Mathf.Max(1, camera.targetTexture.width);
                height = Mathf.Max(1, camera.targetTexture.height);
                return;
            }

            width = camera != null ? Mathf.Max(1, camera.pixelWidth) : 1;
            height = camera != null ? Mathf.Max(1, camera.pixelHeight) : 1;
        }

        public static bool IsScreenSpaceGlobalIlluminationShaderAvailable()
        {
            var frame = Time.frameCount;
            if (shaderAvailabilityFrame == frame)
            {
                return shaderAvailable;
            }

            shaderAvailabilityFrame = frame;
            shaderAvailable = Shader.Find(ScreenSpaceGlobalIlluminationShaderName) != null;
            return shaderAvailable;
        }

        public static string ResolveScreenSpaceGlobalIlluminationShaderStatusLabel()
        {
            return IsScreenSpaceGlobalIlluminationShaderAvailable() ? "Ready" : "Missing(" + ScreenSpaceGlobalIlluminationShaderName + ")";
        }

        public static bool IsScreenSpaceGlobalIlluminationSuppressedByShadingDebug()
        {
            return BurtShadingDebugSettings.IsDebugging && !IsScreenSpaceGlobalIlluminationDebugMode(BurtShadingDebugSettings.Mode);
        }

        public static string ResolveScreenSpaceGlobalIlluminationTraceModeLabel(BurtScreenSpaceGlobalIlluminationSettings settings)
        {
            return settings.Enabled
                ? "ScreenSpaceDiffuseBounce+DiffuseSourceFiltered+SkySHFallback+DepthNormalBilateral+TemporalOptional"
                : "Disabled";
        }

        public static BurtScreenSpaceGlobalIlluminationSettings ResolveScreenSpaceGlobalIlluminationHistorySettings(Camera camera)
        {
            var component = GetScreenSpaceGlobalIlluminationVolumeComponent();
            if (component != null)
            {
                return CreateScreenSpaceGlobalIlluminationSettings(component);
            }

            return new BurtScreenSpaceGlobalIlluminationSettings(
                true,
                BurtScreenSpaceGlobalIlluminationQuality.Custom,
                BurtScreenSpaceGlobalIlluminationResolution.Full,
                0.6f,
                2f,
                12,
                8,
                0.35f,
                1f,
                8f,
                0.65f,
                80f,
                true,
                0.18f,
                true,
                0.86f,
                0.02f,
                0.65f,
                1f);
        }

        public static int ResolveScreenSpaceGlobalIlluminationShaderDebugMode()
        {
            switch (BurtShadingDebugSettings.Mode)
            {
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationRaw:
                    return 1;
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationFinal:
                    return 2;
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationHitRatio:
                    return 3;
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationOverlay:
                    return 4;
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationComposite:
                    return 5;
                default:
                    return 0;
            }
        }

        public static string ResolveScreenSpaceGlobalIlluminationDebugModeLabel()
        {
            switch (BurtShadingDebugSettings.Mode)
            {
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationRaw:
                    return "Raw";
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationFinal:
                    return "Final";
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationHitRatio:
                    return "HitRatio";
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationOverlay:
                    return "Overlay";
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationComposite:
                    return "Composite";
                default:
                    return "Disabled";
            }
        }

        private static BurtScreenSpaceGlobalIlluminationSettings CreateScreenSpaceGlobalIlluminationSettings(BurtScreenSpaceGlobalIlluminationVolumeComponent component)
        {
            var quality = component.quality.value;
            var resolution = component.resolution.value;
            var sampleCount = component.sampleCount.value;
            var maxSteps = component.maxSteps.value;
            var radius = component.radius.value;
            var thickness = component.thickness.value;
            var blur = component.blur.value;
            var blurSharpness = component.blurSharpness.value;
            ApplyScreenSpaceGlobalIlluminationQualityPreset(
                quality,
                ref resolution,
                ref sampleCount,
                ref maxSteps,
                ref radius,
                ref thickness,
                ref blur,
                ref blurSharpness);

            return new BurtScreenSpaceGlobalIlluminationSettings(
                true,
                quality,
                resolution,
                component.intensity.value,
                radius,
                sampleCount,
                maxSteps,
                thickness,
                component.skyFallback.value,
                component.radianceClamp.value,
                component.normalWeight.value,
                component.distanceFade.value,
                blur,
                blurSharpness,
                component.temporalAccumulation.value,
                component.temporalFeedback.value,
                component.temporalDepthRejection.value,
                component.temporalNormalRejection.value,
                component.temporalClamp.value);
        }

        private static void ApplyScreenSpaceGlobalIlluminationQualityPreset(
            BurtScreenSpaceGlobalIlluminationQuality quality,
            ref BurtScreenSpaceGlobalIlluminationResolution resolution,
            ref int sampleCount,
            ref int maxSteps,
            ref float radius,
            ref float thickness,
            ref bool blur,
            ref float blurSharpness)
        {
            switch (quality)
            {
                case BurtScreenSpaceGlobalIlluminationQuality.Low:
                    resolution = BurtScreenSpaceGlobalIlluminationResolution.Half;
                    sampleCount = 8;
                    maxSteps = 6;
                    radius = Mathf.Min(radius, 1.5f);
                    thickness = Mathf.Max(thickness, 0.45f);
                    blur = true;
                    blurSharpness = 0.12f;
                    break;
                case BurtScreenSpaceGlobalIlluminationQuality.Medium:
                    resolution = BurtScreenSpaceGlobalIlluminationResolution.Half;
                    sampleCount = 12;
                    maxSteps = 8;
                    radius = Mathf.Max(radius, 2f);
                    thickness = Mathf.Max(thickness, 0.35f);
                    blur = true;
                    blurSharpness = 0.18f;
                    break;
                case BurtScreenSpaceGlobalIlluminationQuality.High:
                    resolution = BurtScreenSpaceGlobalIlluminationResolution.Full;
                    sampleCount = 20;
                    maxSteps = 12;
                    radius = Mathf.Max(radius, 3f);
                    thickness = Mathf.Max(thickness, 0.28f);
                    blur = true;
                    blurSharpness = 0.24f;
                    break;
            }
        }

        private static void ApplyScreenSpaceGlobalIlluminationResolution(ref RenderTextureDescriptor descriptor, BurtScreenSpaceGlobalIlluminationSettings settings)
        {
            if (settings.Resolution != BurtScreenSpaceGlobalIlluminationResolution.Half)
            {
                return;
            }

            descriptor.width = Mathf.Max(1, (descriptor.width + 1) / 2);
            descriptor.height = Mathf.Max(1, (descriptor.height + 1) / 2);
        }

        private static BurtScreenSpaceGlobalIlluminationVolumeComponent GetScreenSpaceGlobalIlluminationVolumeComponent()
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

            return stack.GetComponent<BurtScreenSpaceGlobalIlluminationVolumeComponent>();
        }
    }

    internal static class BurtScreenSpaceGlobalIlluminationRenderTargetUtility
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

            var cmd = CommandBufferPool.Get(passName);
            cmd.GetTemporaryRT(textureId, descriptor, filterMode);
            cmd.SetRenderTarget(target.Identifier);
            cmd.ClearRenderTarget(false, true, Color.clear);
            cmd.SetGlobalTexture(textureId, target.Identifier);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public static void Release(BurtRenderGraphContext context, string passName, int textureId)
        {
            if (context == null)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(passName);
            cmd.ReleaseTemporaryRT(textureId);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
