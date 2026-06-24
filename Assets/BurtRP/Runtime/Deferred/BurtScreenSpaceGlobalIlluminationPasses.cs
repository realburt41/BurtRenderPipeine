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

    internal abstract class BurtAllocateScreenSpaceGlobalIlluminationIndirectPass : BurtRenderPass
    {
        protected abstract int TextureId { get; }
        protected abstract BurtRenderTargetHandle GetTarget(BurtRenderGraphContext context);
        protected abstract void WriteTarget(BurtRenderPassBuilder builder);

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIllumination(builder.Request, builder.Asset))
            {
                return;
            }

            WriteTarget(builder);
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIllumination(context.Request, context.Asset))
            {
                return;
            }

            var target = GetTarget(context);
            if (!target.IsValid)
            {
                return;
            }

            var camera = context.Request != null ? context.Request.Camera : null;
            var settings = BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationSettings(context.Request, context.Asset);
            var descriptor = BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationDescriptor(camera, settings);
            BurtScreenSpaceGlobalIlluminationRenderTargetUtility.Allocate(context, Name, TextureId, target, descriptor, FilterMode.Bilinear);
        }
    }

    internal sealed class BurtAllocateBurtGIBackfaceDiffuseIndirectPass : BurtAllocateScreenSpaceGlobalIlluminationIndirectPass
    {
        public override string Name => "Burt Allocate BurtGI Backface Diffuse Indirect";
        protected override int TextureId => BurtRenderGraphResourceRegistry.BurtGIBackfaceDiffuseIndirectTextureId;

        protected override BurtRenderTargetHandle GetTarget(BurtRenderGraphContext context)
        {
            return context.BurtGIBackfaceDiffuseIndirectTarget;
        }

        protected override void WriteTarget(BurtRenderPassBuilder builder)
        {
            builder.WriteBurtGIBackfaceDiffuseIndirect();
        }
    }

    internal sealed class BurtAllocateBurtGIRoughSpecularIndirectPass : BurtAllocateScreenSpaceGlobalIlluminationIndirectPass
    {
        public override string Name => "Burt Allocate BurtGI Rough Specular Indirect";
        protected override int TextureId => BurtRenderGraphResourceRegistry.BurtGIRoughSpecularIndirectTextureId;

        protected override BurtRenderTargetHandle GetTarget(BurtRenderGraphContext context)
        {
            return context.BurtGIRoughSpecularIndirectTarget;
        }

        protected override void WriteTarget(BurtRenderPassBuilder builder)
        {
            builder.WriteBurtGIRoughSpecularIndirect();
        }
    }

    internal sealed class BurtAllocateBurtGITemporalDiagnosticsPass : BurtRenderPass
    {
        public override string Name => "Burt Allocate BurtGI Temporal Diagnostics";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIlluminationTemporalDiagnostics(builder.Request, builder.Asset))
            {
                return;
            }

            builder.WriteBurtGITemporalDiagnostics();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIlluminationTemporalDiagnostics(context.Request, context.Asset))
            {
                return;
            }

            var target = context.BurtGITemporalDiagnosticsTarget;
            if (!target.IsValid)
            {
                return;
            }

            var camera = context.Request != null ? context.Request.Camera : null;
            var settings = BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationSettings(context.Request, context.Asset);
            var descriptor = BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationDiagnosticsDescriptor(camera, settings);
            BurtScreenSpaceGlobalIlluminationRenderTargetUtility.Allocate(context, Name, BurtRenderGraphResourceRegistry.BurtGITemporalDiagnosticsTextureId, target, descriptor, FilterMode.Bilinear);
        }
    }

    internal abstract class BurtAllocateScreenSpaceGlobalIlluminationScreenProbePass : BurtRenderPass
    {
        protected abstract int TextureId { get; }
        protected abstract BurtRenderTargetHandle GetTarget(BurtRenderGraphContext context);
        protected abstract void WriteTarget(BurtRenderPassBuilder builder);

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIlluminationScreenProbeLite(builder.Request, builder.Asset))
            {
                return;
            }

            WriteTarget(builder);
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIlluminationScreenProbeLite(context.Request, context.Asset))
            {
                return;
            }

            var target = GetTarget(context);
            if (!target.IsValid)
            {
                return;
            }

            var camera = context.Request != null ? context.Request.Camera : null;
            var settings = BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationScreenProbeSettings(context.Request, context.Asset);
            var descriptor = BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationScreenProbeDescriptor(camera, settings);
            BurtScreenSpaceGlobalIlluminationRenderTargetUtility.Allocate(context, Name, TextureId, target, descriptor, FilterMode.Bilinear);
        }
    }

    internal sealed class BurtAllocateScreenSpaceGlobalIlluminationScreenProbeRadiancePass : BurtAllocateScreenSpaceGlobalIlluminationScreenProbePass
    {
        public override string Name => "Burt Allocate ScreenProbe Radiance";
        protected override int TextureId => BurtRenderGraphResourceRegistry.BurtGIScreenProbeRadianceTextureId;

        protected override BurtRenderTargetHandle GetTarget(BurtRenderGraphContext context)
        {
            return context.BurtGIScreenProbeRadianceTarget;
        }

        protected override void WriteTarget(BurtRenderPassBuilder builder)
        {
            builder.WriteBurtGIScreenProbeRadiance();
        }
    }

    internal sealed class BurtAllocateScreenSpaceGlobalIlluminationScreenProbeIrradiancePass : BurtAllocateScreenSpaceGlobalIlluminationScreenProbePass
    {
        public override string Name => "Burt Allocate ScreenProbe Irradiance";
        protected override int TextureId => BurtRenderGraphResourceRegistry.BurtGIScreenProbeIrradianceTextureId;

        protected override BurtRenderTargetHandle GetTarget(BurtRenderGraphContext context)
        {
            return context.BurtGIScreenProbeIrradianceTarget;
        }

        protected override void WriteTarget(BurtRenderPassBuilder builder)
        {
            builder.WriteBurtGIScreenProbeIrradiance();
        }
    }

    internal sealed class BurtAllocateScreenSpaceGlobalIlluminationScreenProbeConfidencePass : BurtAllocateScreenSpaceGlobalIlluminationScreenProbePass
    {
        public override string Name => "Burt Allocate ScreenProbe Confidence";
        protected override int TextureId => BurtRenderGraphResourceRegistry.BurtGIScreenProbeConfidenceTextureId;

        protected override BurtRenderTargetHandle GetTarget(BurtRenderGraphContext context)
        {
            return context.BurtGIScreenProbeConfidenceTarget;
        }

        protected override void WriteTarget(BurtRenderPassBuilder builder)
        {
            builder.WriteBurtGIScreenProbeConfidence();
        }
    }

    internal sealed class BurtScreenSpaceGlobalIlluminationScreenProbePreparePass : BurtRenderPass
    {
        public override string Name => "Burt ScreenProbe Lite Prepare";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIlluminationScreenProbeLite(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceGlobalIlluminationRaw();
            builder.ReadScreenSpaceGlobalIllumination();
            builder.WriteBurtGIScreenProbeRadiance();
            builder.WriteBurtGIScreenProbeIrradiance();
            builder.WriteBurtGIScreenProbeConfidence();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIlluminationScreenProbeLite(context.Request, context.Asset))
            {
                return;
            }

            var radianceTarget = context.BurtGIScreenProbeRadianceTarget;
            var irradianceTarget = context.BurtGIScreenProbeIrradianceTarget;
            var confidenceTarget = context.BurtGIScreenProbeConfidenceTarget;
            if (!radianceTarget.IsValid || !irradianceTarget.IsValid || !confidenceTarget.IsValid)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            cmd.SetRenderTarget(radianceTarget.Identifier);
            cmd.ClearRenderTarget(false, true, Color.clear);
            cmd.SetRenderTarget(irradianceTarget.Identifier);
            cmd.ClearRenderTarget(false, true, Color.clear);
            cmd.SetRenderTarget(confidenceTarget.Identifier);
            cmd.ClearRenderTarget(false, true, Color.clear);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.BurtGIScreenProbeRadianceTextureId, radianceTarget.Identifier);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.BurtGIScreenProbeIrradianceTextureId, irradianceTarget.Identifier);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.BurtGIScreenProbeConfidenceTextureId, confidenceTarget.Identifier);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    internal abstract class BurtScreenSpaceGlobalIlluminationPass : BurtRenderPass
    {
        protected const string ScreenSpaceGlobalIlluminationShaderName = BurtScreenSpaceGlobalIlluminationPassUtility.ScreenSpaceGlobalIlluminationShaderName;
        protected static readonly int CameraDepthTextureId = BurtRenderGraphResourceRegistry.CameraDepthTextureId;
        protected static readonly int GBuffer0Id = BurtRenderGraphResourceRegistry.GBuffer0Id;
        protected static readonly int GBuffer1Id = BurtRenderGraphResourceRegistry.GBuffer1Id;
        protected static readonly int GBuffer2Id = BurtRenderGraphResourceRegistry.GBuffer2Id;
        protected static readonly int GBuffer3Id = BurtRenderGraphResourceRegistry.GBuffer3Id;
        protected static readonly int GBuffer4Id = BurtRenderGraphResourceRegistry.GBuffer4Id;
        protected static readonly int BurtGIRawTextureId = BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationRawTextureId;
        protected static readonly int BurtGITextureId = BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationTextureId;
        protected static readonly int BurtGITemporalDiagnosticsTextureId = BurtRenderGraphResourceRegistry.BurtGITemporalDiagnosticsTextureId;
        protected static readonly int BurtGIApplyIndirectDiffuseTextureId = Shader.PropertyToID("_BurtGIDiffuseIndirectTexture");
        protected static readonly int BurtGIApplyIndirectBackfaceDiffuseTextureId = Shader.PropertyToID("_BurtGIBackfaceDiffuseIndirectTexture");
        protected static readonly int BurtGIApplyIndirectRoughSpecularTextureId = Shader.PropertyToID("_BurtGIRoughSpecularIndirectTexture");
        protected static readonly int BurtGIApplyIndirectParamsId = Shader.PropertyToID("_BurtGIApplyIndirectParams");
        protected static readonly int BurtGICameraColorCopyTextureId = Shader.PropertyToID("_BurtGICameraColorCopyTexture");
        protected static readonly int BurtGIDebugCameraColorTextureId = Shader.PropertyToID("_BurtGIDebugCameraColorTexture");
        protected static readonly int BurtGIDebugCameraColorCopyTextureId = Shader.PropertyToID("_BurtGIDebugCameraColorCopyTexture");
        protected static readonly int BurtGISourceTexelSizeId = Shader.PropertyToID("_BurtGISourceTexelSize");
        protected static readonly int BurtGIViewMatrixId = Shader.PropertyToID("_BurtGIViewMatrix");
        protected static readonly int BurtGIViewProjectionMatrixId = Shader.PropertyToID("_BurtGIViewProjectionMatrix");
        protected static readonly int BurtGIParams0Id = Shader.PropertyToID("_BurtGIParams0");
        protected static readonly int BurtGIParams1Id = Shader.PropertyToID("_BurtGIParams1");
        protected static readonly int BurtGIParams2Id = Shader.PropertyToID("_BurtGIParams2");
        protected static readonly int BurtGIParams3Id = Shader.PropertyToID("_BurtGIParams3");
        protected static readonly int BurtGIParams4Id = Shader.PropertyToID("_BurtGIParams4");
        protected static readonly int BurtGIDebugModeId = Shader.PropertyToID("_BurtGIDebugMode");
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

        protected static void UploadCameraGlobals(CommandBuffer cmd, BurtRenderRequest request, Camera camera, RenderTextureDescriptor descriptor)
        {
            var temporalAA = request != null ? request.TemporalAA : null;
            var useTemporalAA = temporalAA != null && temporalAA.Enabled;
            var viewMatrix = useTemporalAA ? temporalAA.ViewMatrix : camera.worldToCameraMatrix;
            var viewProjectionMatrix = useTemporalAA
                ? temporalAA.CurrentViewProjectionMatrix
                : GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * viewMatrix;
            var inverseViewProjectionMatrix = useTemporalAA
                ? temporalAA.InverseCurrentViewProjectionMatrix
                : viewProjectionMatrix.inverse;
            var width = Mathf.Max(1, descriptor.width);
            var height = Mathf.Max(1, descriptor.height);
            var cameraPosition = camera.transform.position;

            BurtScreenSpaceGlobalIlluminationPassUtility.GetCameraTargetSize(camera, out var cameraWidth, out var cameraHeight);
            cmd.SetGlobalMatrix(BurtGIViewMatrixId, viewMatrix);
            cmd.SetGlobalMatrix(BurtGIViewProjectionMatrixId, viewProjectionMatrix);
            cmd.SetGlobalMatrix(InverseViewProjectionMatrixId, inverseViewProjectionMatrix);
            cmd.SetGlobalVector(CameraWorldPositionId, new Vector4(cameraPosition.x, cameraPosition.y, cameraPosition.z, 1f));
            cmd.SetGlobalVector(DeferredScreenSizeId, new Vector4(cameraWidth, cameraHeight, 1f / cameraWidth, 1f / cameraHeight));
            cmd.SetGlobalVector(BurtGISourceTexelSizeId, new Vector4(1f / width, 1f / height, width, height));
        }

        protected static void UploadSettings(CommandBuffer cmd, BurtScreenSpaceGlobalIlluminationSettings settings)
        {
            cmd.SetGlobalVector(BurtGIParams0Id, new Vector4(settings.Radius, settings.SampleCount, settings.MaxSteps, settings.Thickness));
            cmd.SetGlobalVector(BurtGIParams1Id, new Vector4(settings.Intensity, settings.SkyFallback, settings.Blur ? 1f : 0f, settings.BlurSharpness));
            cmd.SetGlobalVector(BurtGIParams2Id, new Vector4(Time.frameCount & 1023, settings.NormalWeight, settings.DistanceFade, settings.RadianceClamp));
            cmd.SetGlobalVector(BurtGIParams3Id, new Vector4(settings.SpatialDenoiseRadius, settings.SpatialDenoiseStrength, settings.TemporalVarianceClamp, settings.TemporalHitRejection));
            cmd.SetGlobalVector(BurtGIParams4Id, new Vector4(settings.LeakGuardStrength, settings.EdgeFadeStrength, settings.NormalConeTightness, settings.SkyEdgeSuppression));
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
            if (!TryGetTargets(context, out var cameraDepthTarget, out var gbuffer0Target, out var gbuffer1Target, out var gbuffer2Target, out var gbuffer3Target, out var gbuffer4Target, out var rawTarget))
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
            cmd.SetGlobalTexture(CameraDepthTextureId, cameraDepthTarget.Identifier);
            BindGBufferInputs(cmd, gbuffer0Target, gbuffer1Target, gbuffer2Target, gbuffer3Target, gbuffer4Target);
            UploadCameraGlobals(cmd, context.Request, camera, descriptor);
            UploadSettings(cmd, settings);
            cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1);
            cmd.SetGlobalTexture(BurtGIRawTextureId, rawTarget.Identifier);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private static bool TryGetTargets(
            BurtRenderGraphContext context,
            out BurtRenderTargetHandle cameraDepthTarget,
            out BurtRenderTargetHandle gbuffer0Target,
            out BurtRenderTargetHandle gbuffer1Target,
            out BurtRenderTargetHandle gbuffer2Target,
            out BurtRenderTargetHandle gbuffer3Target,
            out BurtRenderTargetHandle gbuffer4Target,
            out BurtRenderTargetHandle rawTarget)
        {
            cameraDepthTarget = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            gbuffer0Target = context != null ? context.GBuffer0Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer0Name);
            gbuffer1Target = context != null ? context.GBuffer1Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer1Name);
            gbuffer2Target = context != null ? context.GBuffer2Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer2Name);
            gbuffer3Target = context != null ? context.GBuffer3Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer3Name);
            gbuffer4Target = context != null ? context.GBuffer4Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer4Name);
            rawTarget = context != null ? context.ScreenSpaceGlobalIlluminationRawTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationRawName);

            return cameraDepthTarget.IsValid &&
                gbuffer0Target.IsValid &&
                gbuffer1Target.IsValid &&
                gbuffer2Target.IsValid &&
                gbuffer3Target.IsValid &&
                gbuffer4Target.IsValid &&
                rawTarget.IsValid;
        }
    }

    internal sealed class BurtScreenSpaceGlobalIlluminationApplyIndirectPass : BurtScreenSpaceGlobalIlluminationPass
    {
        public override string Name => "Burt Screen Space Global Illumination Apply Indirect";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.GlobalState;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIllumination(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceGlobalIllumination();
            builder.ReadBurtGIBackfaceDiffuseIndirect();
            builder.ReadBurtGIRoughSpecularIndirect();
            builder.WriteGlobalResource(BurtRenderGraphResourceRegistry.BurtGIApplyIndirectGlobalsName);
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var enabled = false;
            var intensity = 0f;
            var burtGITarget = context != null
                ? context.ScreenSpaceGlobalIlluminationTarget
                : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationName);
            var backfaceDiffuseTarget = context != null
                ? context.BurtGIBackfaceDiffuseIndirectTarget
                : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIBackfaceDiffuseIndirectName);
            var roughSpecularTarget = context != null
                ? context.BurtGIRoughSpecularIndirectTarget
                : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIRoughSpecularIndirectName);

            if (context != null &&
                burtGITarget.IsValid &&
                backfaceDiffuseTarget.IsValid &&
                roughSpecularTarget.IsValid &&
                BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIllumination(context.Request, context.Asset))
            {
                var settings = BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationSettings(context.Request, context.Asset);
                enabled = settings.Enabled;
                intensity = settings.Intensity;
            }

            var cmd = CommandBufferPool.Get(Name);
            if (enabled)
            {
                cmd.SetGlobalTexture(BurtGIApplyIndirectDiffuseTextureId, burtGITarget.Identifier);
            }
            else
            {
                cmd.SetGlobalTexture(BurtGIApplyIndirectDiffuseTextureId, Texture2D.blackTexture);
            }

            cmd.SetGlobalTexture(BurtGIApplyIndirectBackfaceDiffuseTextureId, enabled ? backfaceDiffuseTarget.Identifier : Texture2D.blackTexture);
            cmd.SetGlobalTexture(BurtGIApplyIndirectRoughSpecularTextureId, enabled ? roughSpecularTarget.Identifier : Texture2D.blackTexture);
            cmd.SetGlobalVector(BurtGIApplyIndirectParamsId, new Vector4(enabled ? 1f : 0f, intensity, enabled ? 1f : 0f, enabled ? 1f : 0f));
            if (context != null)
            {
                context.ScriptableContext.ExecuteCommandBuffer(cmd);
            }

            CommandBufferPool.Release(cmd);
        }
    }

    internal sealed class BurtScreenSpaceGlobalIlluminationResolveIndirectChannelsPass : BurtScreenSpaceGlobalIlluminationPass
    {
        private const int ResolveIndirectChannelsPassIndex = 8;

        public override string Name => "Burt Screen Space Global Illumination Resolve Indirect Channels";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIllumination(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceGlobalIllumination();
            builder.ReadCameraDepth();
            builder.ReadGBuffer0();
            builder.ReadGBuffer1();
            builder.ReadGBuffer2();
            builder.ReadGBuffer3();
            builder.ReadGBuffer4();
            builder.WriteBurtGIBackfaceDiffuseIndirect();
            builder.WriteBurtGIRoughSpecularIndirect();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!TryGetTargets(
                    context,
                    out var cameraDepthTarget,
                    out var gbuffer0Target,
                    out var gbuffer1Target,
                    out var gbuffer2Target,
                    out var gbuffer3Target,
                    out var gbuffer4Target,
                    out var burtGITarget,
                    out var backfaceDiffuseTarget,
                    out var roughSpecularTarget))
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
            cmd.SetRenderTarget(
                new[]
                {
                    backfaceDiffuseTarget.Identifier,
                    roughSpecularTarget.Identifier,
                },
                new RenderTargetIdentifier(BuiltinRenderTextureType.None));
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, descriptor.width, descriptor.height);
            cmd.SetGlobalTexture(CameraDepthTextureId, cameraDepthTarget.Identifier);
            cmd.SetGlobalTexture(BurtGITextureId, burtGITarget.Identifier);
            BindGBufferInputs(cmd, gbuffer0Target, gbuffer1Target, gbuffer2Target, gbuffer3Target, gbuffer4Target);
            UploadCameraGlobals(cmd, context.Request, camera, descriptor);
            UploadSettings(cmd, settings);
            cmd.DrawProcedural(Matrix4x4.identity, material, ResolveIndirectChannelsPassIndex, MeshTopology.Triangles, 3, 1);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.BurtGIBackfaceDiffuseIndirectTextureId, backfaceDiffuseTarget.Identifier);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.BurtGIRoughSpecularIndirectTextureId, roughSpecularTarget.Identifier);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private static bool TryGetTargets(
            BurtRenderGraphContext context,
            out BurtRenderTargetHandle cameraDepthTarget,
            out BurtRenderTargetHandle gbuffer0Target,
            out BurtRenderTargetHandle gbuffer1Target,
            out BurtRenderTargetHandle gbuffer2Target,
            out BurtRenderTargetHandle gbuffer3Target,
            out BurtRenderTargetHandle gbuffer4Target,
            out BurtRenderTargetHandle burtGITarget,
            out BurtRenderTargetHandle backfaceDiffuseTarget,
            out BurtRenderTargetHandle roughSpecularTarget)
        {
            cameraDepthTarget = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            gbuffer0Target = context != null ? context.GBuffer0Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer0Name);
            gbuffer1Target = context != null ? context.GBuffer1Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer1Name);
            gbuffer2Target = context != null ? context.GBuffer2Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer2Name);
            gbuffer3Target = context != null ? context.GBuffer3Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer3Name);
            gbuffer4Target = context != null ? context.GBuffer4Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer4Name);
            burtGITarget = context != null ? context.ScreenSpaceGlobalIlluminationTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationName);
            backfaceDiffuseTarget = context != null ? context.BurtGIBackfaceDiffuseIndirectTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIBackfaceDiffuseIndirectName);
            roughSpecularTarget = context != null ? context.BurtGIRoughSpecularIndirectTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGIRoughSpecularIndirectName);

            return cameraDepthTarget.IsValid &&
                gbuffer0Target.IsValid &&
                gbuffer1Target.IsValid &&
                gbuffer2Target.IsValid &&
                gbuffer3Target.IsValid &&
                gbuffer4Target.IsValid &&
                burtGITarget.IsValid &&
                backfaceDiffuseTarget.IsValid &&
                roughSpecularTarget.IsValid;
        }
    }

    internal sealed class BurtScreenSpaceGlobalIlluminationBlurPass : BurtScreenSpaceGlobalIlluminationPass
    {
        private const int BlurPassIndex = 1;
        private const int TemporalPassIndex = 4;
        private const int CopyDepthNormalPassIndex = 5;
        private const int CopyTemporalFinalPassIndex = 6;
        private const int CopyTemporalDiagnosticsPassIndex = 7;
        private static readonly int BurtGISpatialFinalTextureId = Shader.PropertyToID("_BurtScreenSpaceGlobalIlluminationSpatialFinalTexture");
        private static readonly int BurtGISpatialFinalInputTextureId = Shader.PropertyToID("_BurtGISpatialFinalTexture");
        private static readonly int BurtGITemporalFinalTextureId = Shader.PropertyToID("_BurtScreenSpaceGlobalIlluminationTemporalFinalTexture");
        private static readonly int BurtGITemporalFinalInputTextureId = Shader.PropertyToID("_BurtGITemporalFinalTexture");
        private static readonly int BurtGIDepthNormalTextureId = Shader.PropertyToID("_BurtGIDepthNormalTexture");
        private static readonly int BurtGIHistoryTextureId = Shader.PropertyToID("_BurtGIHistoryTexture");
        private static readonly int BurtGIHistoryDepthNormalTextureId = Shader.PropertyToID("_BurtGIHistoryDepthNormalTexture");
        private static readonly int BurtGIPreviousHistoryTextureId = Shader.PropertyToID("_BurtGIPreviousHistoryTexture");
        private static readonly int BurtGIPreviousHistoryDepthNormalTextureId = Shader.PropertyToID("_BurtGIPreviousHistoryDepthNormalTexture");
        private static readonly int BurtGIPreviousViewProjectionMatrixId = Shader.PropertyToID("_BurtGIPreviousViewProjectionMatrix");
        private static readonly int BurtGITemporalParamsId = Shader.PropertyToID("_BurtGITemporalParams");
        private static readonly int BurtGITemporalParams1Id = Shader.PropertyToID("_BurtGITemporalParams1");

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
            if (BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIlluminationTemporalDiagnostics(builder.Request, builder.Asset))
            {
                builder.WriteBurtGITemporalDiagnostics();
            }
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!TryGetTargets(context, out var rawTarget, out var cameraDepthTarget, out var gbuffer1Target, out var target, out var temporalDiagnosticsTarget))
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
            var spatialFinalIdentifier = new RenderTargetIdentifier(BurtGISpatialFinalTextureId);
            var temporalFinalIdentifier = new RenderTargetIdentifier(BurtGITemporalFinalTextureId);
            var depthNormalIdentifier = new RenderTargetIdentifier(BurtGIDepthNormalTextureId);
            var cmd = CommandBufferPool.Get(Name);
            cmd.SetGlobalTexture(BurtGIRawTextureId, rawTarget.Identifier);
            cmd.SetGlobalTexture(CameraDepthTextureId, cameraDepthTarget.Identifier);
            cmd.SetGlobalTexture(GBuffer1Id, gbuffer1Target.Identifier);
            UploadCameraGlobals(cmd, context.Request, camera, descriptor);
            UploadSettings(cmd, settings);

            var shouldUseTemporal = settings.TemporalAccumulation;
            if (shouldUseTemporal)
            {
                cmd.GetTemporaryRT(BurtGISpatialFinalTextureId, descriptor, FilterMode.Bilinear);
                cmd.GetTemporaryRT(BurtGITemporalFinalTextureId, BurtScreenSpaceGlobalIlluminationHistoryUtility.CreateColorHistoryDescriptor(camera, settings), FilterMode.Bilinear);
                outputTarget = spatialFinalIdentifier;
            }
            else
            {
                BurtScreenSpaceGlobalIlluminationHistoryUtility.InvalidateHistory(camera, BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationTemporalDisabledReason(context.Request, settings));
            }

            cmd.SetRenderTarget(outputTarget);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, descriptor.width, descriptor.height);
            cmd.DrawProcedural(Matrix4x4.identity, material, BlurPassIndex, MeshTopology.Triangles, 3, 1);

            if (shouldUseTemporal)
            {
                ResolveTemporal(cmd, material, context.Request, context.Asset, settings, camera, descriptor, spatialFinalIdentifier, temporalFinalIdentifier, target.Identifier, temporalDiagnosticsTarget, depthNormalIdentifier);
                cmd.ReleaseTemporaryRT(BurtGITemporalFinalTextureId);
                cmd.ReleaseTemporaryRT(BurtGISpatialFinalTextureId);
            }

            cmd.SetGlobalTexture(BurtGITextureId, target.Identifier);
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
            BurtRenderTargetHandle temporalDiagnosticsTarget,
            RenderTargetIdentifier depthNormalTarget)
        {
            var history = BurtScreenSpaceGlobalIlluminationHistoryUtility.EnsureHistoryTextures(request, asset, settings, out var historyValid);
            var width = Mathf.Max(1, descriptor.width);
            var height = Mathf.Max(1, descriptor.height);
            cmd.SetGlobalTexture(BurtGISpatialFinalInputTextureId, spatialFinal);
            cmd.SetGlobalTexture(BurtGIHistoryTextureId, history.Color != null ? (Texture)history.Color : Texture2D.blackTexture);
            cmd.SetGlobalTexture(BurtGIHistoryDepthNormalTextureId, history.DepthNormal != null ? (Texture)history.DepthNormal : Texture2D.blackTexture);
            cmd.SetGlobalTexture(BurtGIPreviousHistoryTextureId, history.Color != null ? (Texture)history.Color : Texture2D.blackTexture);
            cmd.SetGlobalTexture(BurtGIPreviousHistoryDepthNormalTextureId, history.DepthNormal != null ? (Texture)history.DepthNormal : Texture2D.blackTexture);
            cmd.SetGlobalMatrix(BurtGIPreviousViewProjectionMatrixId, history.PreviousViewProjectionMatrix);
            cmd.SetGlobalMatrix(InverseViewProjectionMatrixId, history.CurrentInverseViewProjectionMatrix);
            cmd.SetGlobalVector(BurtGISourceTexelSizeId, new Vector4(1f / width, 1f / height, width, height));
            cmd.SetGlobalVector(BurtGITemporalParamsId, new Vector4(settings.TemporalFeedback, historyValid ? 1f : 0f, settings.TemporalDepthRejection, settings.TemporalNormalRejection));
            cmd.SetGlobalVector(BurtGITemporalParams1Id, new Vector4(settings.TemporalClamp, settings.TemporalVarianceClamp, settings.TemporalHitRejection, settings.SpatialDenoiseStrength));

            cmd.SetRenderTarget(temporalFinalTarget);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, width, height);
            cmd.DrawProcedural(Matrix4x4.identity, material, TemporalPassIndex, MeshTopology.Triangles, 3, 1);

            cmd.SetGlobalTexture(BurtGITemporalFinalInputTextureId, temporalFinalTarget);
            cmd.SetRenderTarget(finalTarget);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, width, height);
            cmd.DrawProcedural(Matrix4x4.identity, material, CopyTemporalFinalPassIndex, MeshTopology.Triangles, 3, 1);

            if (temporalDiagnosticsTarget.IsValid)
            {
                cmd.SetGlobalTexture(BurtGITemporalFinalInputTextureId, temporalFinalTarget);
                cmd.SetRenderTarget(temporalDiagnosticsTarget.Identifier);
                BurtRenderTargetDescriptorUtility.SetViewport(cmd, width, height);
                cmd.DrawProcedural(Matrix4x4.identity, material, CopyTemporalDiagnosticsPassIndex, MeshTopology.Triangles, 3, 1);
                cmd.SetGlobalTexture(BurtGITemporalDiagnosticsTextureId, temporalDiagnosticsTarget.Identifier);
            }

            if (history.Color == null || history.DepthNormal == null)
            {
                return;
            }

            BurtScreenSpaceGlobalIlluminationHistoryUtility.CopyHistoryToDebugSnapshot(cmd, camera);
            cmd.CopyTexture(temporalFinalTarget, new RenderTargetIdentifier(history.Color));
            var depthNormalDescriptor = BurtScreenSpaceGlobalIlluminationHistoryUtility.CreateDepthNormalHistoryDescriptor(camera, settings);
            cmd.GetTemporaryRT(BurtGIDepthNormalTextureId, depthNormalDescriptor, FilterMode.Point);
            cmd.SetRenderTarget(depthNormalTarget);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, width, height);
            cmd.DrawProcedural(Matrix4x4.identity, material, CopyDepthNormalPassIndex, MeshTopology.Triangles, 3, 1);
            cmd.CopyTexture(depthNormalTarget, new RenderTargetIdentifier(history.DepthNormal));
            cmd.ReleaseTemporaryRT(BurtGIDepthNormalTextureId);
            BurtScreenSpaceGlobalIlluminationHistoryUtility.MarkHistoryValid(camera);
        }

        private static bool TryGetTargets(
            BurtRenderGraphContext context,
            out BurtRenderTargetHandle rawTarget,
            out BurtRenderTargetHandle cameraDepthTarget,
            out BurtRenderTargetHandle gbuffer1Target,
            out BurtRenderTargetHandle target,
            out BurtRenderTargetHandle temporalDiagnosticsTarget)
        {
            rawTarget = context != null ? context.ScreenSpaceGlobalIlluminationRawTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationRawName);
            cameraDepthTarget = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            gbuffer1Target = context != null ? context.GBuffer1Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer1Name);
            target = context != null ? context.ScreenSpaceGlobalIlluminationTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationName);
            temporalDiagnosticsTarget = context != null ? context.BurtGITemporalDiagnosticsTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.BurtGITemporalDiagnosticsName);

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
            if (!TryGetTargets(context, out var cameraColorTarget, out var cameraDepthTarget, out var burtGITarget))
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

            var burtGIDescriptor = BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationDescriptor(camera, settings);
            var cameraColorDescriptor = BurtRenderTargetDescriptorUtility.CreateCameraColorDescriptor(camera);
            var cmd = CommandBufferPool.Get(Name);
            cmd.GetTemporaryRT(BurtGICameraColorCopyTextureId, cameraColorDescriptor, FilterMode.Bilinear);
            cmd.Blit(cameraColorTarget.Identifier, new RenderTargetIdentifier(BurtGICameraColorCopyTextureId));
            cmd.SetRenderTarget(cameraColorTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            cmd.SetGlobalTexture(BurtGICameraColorCopyTextureId, new RenderTargetIdentifier(BurtGICameraColorCopyTextureId));
            cmd.SetGlobalTexture(CameraDepthTextureId, cameraDepthTarget.Identifier);
            cmd.SetGlobalTexture(BurtGITextureId, burtGITarget.Identifier);
            UploadCameraGlobals(cmd, context.Request, camera, burtGIDescriptor);
            UploadSettings(cmd, settings);
            cmd.DrawProcedural(Matrix4x4.identity, material, 2, MeshTopology.Triangles, 3, 1);
            cmd.ReleaseTemporaryRT(BurtGICameraColorCopyTextureId);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private static bool TryGetTargets(
            BurtRenderGraphContext context,
            out BurtRenderTargetHandle cameraColorTarget,
            out BurtRenderTargetHandle cameraDepthTarget,
            out BurtRenderTargetHandle burtGITarget)
        {
            cameraColorTarget = context != null ? context.CameraColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraColorName);
            cameraDepthTarget = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            burtGITarget = context != null ? context.ScreenSpaceGlobalIlluminationTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationName);
            return cameraColorTarget.IsValid && cameraDepthTarget.IsValid && burtGITarget.IsValid;
        }
    }

    internal sealed class BurtDebugScreenSpaceGlobalIlluminationPass : BurtScreenSpaceGlobalIlluminationPass
    {
        private const int DebugPassIndex = 3;
        private static readonly int BurtGIPreviousHistoryTextureId = Shader.PropertyToID("_BurtGIPreviousHistoryTexture");
        private static readonly int BurtGIPreviousHistoryDepthNormalTextureId = Shader.PropertyToID("_BurtGIPreviousHistoryDepthNormalTexture");
        private static readonly int BurtGIPreviousViewProjectionMatrixId = Shader.PropertyToID("_BurtGIPreviousViewProjectionMatrix");
        private static readonly int BurtGITemporalParamsId = Shader.PropertyToID("_BurtGITemporalParams");

        public override string Name => "Burt Debug Screen Space Global Illumination";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIlluminationDebugView(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceGlobalIlluminationRaw();
            builder.ReadScreenSpaceGlobalIllumination();
            builder.ReadCameraDepth();
            builder.ReadGBuffer1();
            if (BurtScreenSpaceGlobalIlluminationPassUtility.IsScreenSpaceGlobalIlluminationTemporalDiagnosticDebugMode(BurtShadingDebugSettings.Mode))
            {
                builder.ReadBurtGITemporalDiagnostics();
            }

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
            var cameraDepthTarget = context.CameraDepthTarget;
            var gbuffer1Target = context.GBuffer1Target;
            var rawTarget = context.ScreenSpaceGlobalIlluminationRawTarget;
            var burtGITarget = context.ScreenSpaceGlobalIlluminationTarget;
            var temporalDiagnosticsTarget = context.BurtGITemporalDiagnosticsTarget;
            if (!cameraColorTarget.IsValid || !cameraDepthTarget.IsValid || !gbuffer1Target.IsValid || !rawTarget.IsValid || !burtGITarget.IsValid)
            {
                return;
            }

            if (BurtScreenSpaceGlobalIlluminationPassUtility.IsScreenSpaceGlobalIlluminationTemporalDiagnosticDebugMode(BurtShadingDebugSettings.Mode) && !temporalDiagnosticsTarget.IsValid)
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
                cmd.GetTemporaryRT(BurtGIDebugCameraColorCopyTextureId, cameraColorDescriptor, FilterMode.Bilinear);
                cmd.Blit(cameraColorTarget.Identifier, new RenderTargetIdentifier(BurtGIDebugCameraColorCopyTextureId));
                cmd.SetGlobalTexture(BurtGIDebugCameraColorTextureId, new RenderTargetIdentifier(BurtGIDebugCameraColorCopyTextureId));
            }

            cmd.SetRenderTarget(cameraColorTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            cmd.SetGlobalTexture(CameraDepthTextureId, cameraDepthTarget.Identifier);
            cmd.SetGlobalTexture(GBuffer1Id, gbuffer1Target.Identifier);
            cmd.SetGlobalTexture(BurtGIRawTextureId, rawTarget.Identifier);
            cmd.SetGlobalTexture(BurtGITextureId, burtGITarget.Identifier);
            if (temporalDiagnosticsTarget.IsValid)
            {
                cmd.SetGlobalTexture(BurtGITemporalDiagnosticsTextureId, temporalDiagnosticsTarget.Identifier);
            }
            else
            {
                cmd.SetGlobalTexture(BurtGITemporalDiagnosticsTextureId, Texture2D.blackTexture);
            }

            UploadTemporalDebugGlobals(cmd, context.Request, settings);
            cmd.SetGlobalFloat(BurtGIDebugModeId, BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationShaderDebugMode());
            UploadCameraGlobals(cmd, context.Request, camera, descriptor);
            UploadSettings(cmd, settings);
            cmd.DrawProcedural(Matrix4x4.identity, material, DebugPassIndex, MeshTopology.Triangles, 3, 1);
            if (isOverlay)
            {
                cmd.ReleaseTemporaryRT(BurtGIDebugCameraColorCopyTextureId);
            }

            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private static void UploadTemporalDebugGlobals(CommandBuffer cmd, BurtRenderRequest request, BurtScreenSpaceGlobalIlluminationSettings settings)
        {
            var history = BurtScreenSpaceGlobalIlluminationHistoryUtility.GetDebugSnapshotTextures(request);
            var status = BurtScreenSpaceGlobalIlluminationHistoryUtility.GetHistoryStatus(request, settings);
            cmd.SetGlobalTexture(BurtGIPreviousHistoryTextureId, history.Color != null ? (Texture)history.Color : Texture2D.blackTexture);
            cmd.SetGlobalTexture(BurtGIPreviousHistoryDepthNormalTextureId, history.DepthNormal != null ? (Texture)history.DepthNormal : Texture2D.blackTexture);
            cmd.SetGlobalMatrix(BurtGIPreviousViewProjectionMatrixId, history.PreviousViewProjectionMatrix);
            cmd.SetGlobalVector(BurtGITemporalParamsId, new Vector4(settings.TemporalFeedback, status.HasHistory ? 1f : 0f, settings.TemporalDepthRejection, settings.TemporalNormalRejection));
        }
    }

    internal sealed class BurtReleaseBurtGITemporalDiagnosticsPass : BurtRenderPass
    {
        public override string Name => "Burt Release BurtGI Temporal Diagnostics";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIlluminationTemporalDiagnostics(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadBurtGITemporalDiagnostics();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIlluminationTemporalDiagnostics(context.Request, context.Asset))
            {
                return;
            }

            var target = context.BurtGITemporalDiagnosticsTarget;
            if (!target.IsValid)
            {
                return;
            }

            BurtScreenSpaceGlobalIlluminationRenderTargetUtility.Release(context, Name, BurtRenderGraphResourceRegistry.BurtGITemporalDiagnosticsTextureId);
        }
    }

    internal abstract class BurtReleaseScreenSpaceGlobalIlluminationScreenProbePass : BurtRenderPass
    {
        protected abstract int TextureId { get; }
        protected abstract BurtRenderTargetHandle GetTarget(BurtRenderGraphContext context);
        protected abstract void ReadTarget(BurtRenderPassBuilder builder);

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIlluminationScreenProbeLite(builder.Request, builder.Asset))
            {
                return;
            }

            ReadTarget(builder);
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIlluminationScreenProbeLite(context.Request, context.Asset))
            {
                return;
            }

            var target = GetTarget(context);
            if (!target.IsValid)
            {
                return;
            }

            BurtScreenSpaceGlobalIlluminationRenderTargetUtility.Release(context, Name, TextureId);
        }
    }

    internal sealed class BurtReleaseScreenSpaceGlobalIlluminationScreenProbeConfidencePass : BurtReleaseScreenSpaceGlobalIlluminationScreenProbePass
    {
        public override string Name => "Burt Release ScreenProbe Confidence";
        protected override int TextureId => BurtRenderGraphResourceRegistry.BurtGIScreenProbeConfidenceTextureId;

        protected override BurtRenderTargetHandle GetTarget(BurtRenderGraphContext context)
        {
            return context.BurtGIScreenProbeConfidenceTarget;
        }

        protected override void ReadTarget(BurtRenderPassBuilder builder)
        {
            builder.ReadBurtGIScreenProbeConfidence();
        }
    }

    internal sealed class BurtReleaseScreenSpaceGlobalIlluminationScreenProbeIrradiancePass : BurtReleaseScreenSpaceGlobalIlluminationScreenProbePass
    {
        public override string Name => "Burt Release ScreenProbe Irradiance";
        protected override int TextureId => BurtRenderGraphResourceRegistry.BurtGIScreenProbeIrradianceTextureId;

        protected override BurtRenderTargetHandle GetTarget(BurtRenderGraphContext context)
        {
            return context.BurtGIScreenProbeIrradianceTarget;
        }

        protected override void ReadTarget(BurtRenderPassBuilder builder)
        {
            builder.ReadBurtGIScreenProbeIrradiance();
        }
    }

    internal sealed class BurtReleaseScreenSpaceGlobalIlluminationScreenProbeRadiancePass : BurtReleaseScreenSpaceGlobalIlluminationScreenProbePass
    {
        public override string Name => "Burt Release ScreenProbe Radiance";
        protected override int TextureId => BurtRenderGraphResourceRegistry.BurtGIScreenProbeRadianceTextureId;

        protected override BurtRenderTargetHandle GetTarget(BurtRenderGraphContext context)
        {
            return context.BurtGIScreenProbeRadianceTarget;
        }

        protected override void ReadTarget(BurtRenderPassBuilder builder)
        {
            builder.ReadBurtGIScreenProbeRadiance();
        }
    }

    internal abstract class BurtReleaseScreenSpaceGlobalIlluminationIndirectPass : BurtRenderPass
    {
        protected abstract int TextureId { get; }
        protected abstract BurtRenderTargetHandle GetTarget(BurtRenderGraphContext context);
        protected abstract void ReadTarget(BurtRenderPassBuilder builder);

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIllumination(builder.Request, builder.Asset))
            {
                return;
            }

            ReadTarget(builder);
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIllumination(context.Request, context.Asset))
            {
                return;
            }

            var target = GetTarget(context);
            if (!target.IsValid)
            {
                return;
            }

            BurtScreenSpaceGlobalIlluminationRenderTargetUtility.Release(context, Name, TextureId);
        }
    }

    internal sealed class BurtReleaseBurtGIBackfaceDiffuseIndirectPass : BurtReleaseScreenSpaceGlobalIlluminationIndirectPass
    {
        public override string Name => "Burt Release BurtGI Backface Diffuse Indirect";
        protected override int TextureId => BurtRenderGraphResourceRegistry.BurtGIBackfaceDiffuseIndirectTextureId;

        protected override BurtRenderTargetHandle GetTarget(BurtRenderGraphContext context)
        {
            return context.BurtGIBackfaceDiffuseIndirectTarget;
        }

        protected override void ReadTarget(BurtRenderPassBuilder builder)
        {
            builder.ReadBurtGIBackfaceDiffuseIndirect();
        }
    }

    internal sealed class BurtReleaseBurtGIRoughSpecularIndirectPass : BurtReleaseScreenSpaceGlobalIlluminationIndirectPass
    {
        public override string Name => "Burt Release BurtGI Rough Specular Indirect";
        protected override int TextureId => BurtRenderGraphResourceRegistry.BurtGIRoughSpecularIndirectTextureId;

        protected override BurtRenderTargetHandle GetTarget(BurtRenderGraphContext context)
        {
            return context.BurtGIRoughSpecularIndirectTarget;
        }

        protected override void ReadTarget(BurtRenderPassBuilder builder)
        {
            builder.ReadBurtGIRoughSpecularIndirect();
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
        public static readonly BurtScreenSpaceGlobalIlluminationSettings Disabled = new BurtScreenSpaceGlobalIlluminationSettings(false, BurtScreenSpaceGlobalIlluminationQuality.Medium, BurtScreenSpaceGlobalIlluminationResolution.Half, 0.6f, 2f, 12, 8, 0.35f, 1f, 8f, 0.65f, 80f, true, 0.18f, 1.25f, 0.75f, 0.65f, 0.5f, 0.55f, 0.6f, true, 0.86f, 0.02f, 0.65f, 1f, 1.25f, 0.55f);

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
            float spatialDenoiseRadius,
            float spatialDenoiseStrength,
            float leakGuardStrength,
            float edgeFadeStrength,
            float normalConeTightness,
            float skyEdgeSuppression,
            bool temporalAccumulation,
            float temporalFeedback,
            float temporalDepthRejection,
            float temporalNormalRejection,
            float temporalClamp,
            float temporalVarianceClamp,
            float temporalHitRejection)
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
            SpatialDenoiseRadius = Mathf.Clamp(spatialDenoiseRadius, 0.5f, 3f);
            SpatialDenoiseStrength = Mathf.Clamp01(spatialDenoiseStrength);
            LeakGuardStrength = Mathf.Clamp01(leakGuardStrength);
            EdgeFadeStrength = Mathf.Clamp01(edgeFadeStrength);
            NormalConeTightness = Mathf.Clamp01(normalConeTightness);
            SkyEdgeSuppression = Mathf.Clamp01(skyEdgeSuppression);
            TemporalAccumulation = temporalAccumulation;
            TemporalFeedback = Mathf.Clamp(temporalFeedback, 0f, 0.98f);
            TemporalDepthRejection = Mathf.Clamp(temporalDepthRejection, 0.001f, 0.2f);
            TemporalNormalRejection = Mathf.Clamp01(temporalNormalRejection);
            TemporalClamp = Mathf.Clamp(temporalClamp, 0.25f, 4f);
            TemporalVarianceClamp = Mathf.Clamp(temporalVarianceClamp, 0f, 4f);
            TemporalHitRejection = Mathf.Clamp01(temporalHitRejection);
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
        public float SpatialDenoiseRadius { get; }
        public float SpatialDenoiseStrength { get; }
        public float LeakGuardStrength { get; }
        public float EdgeFadeStrength { get; }
        public float NormalConeTightness { get; }
        public float SkyEdgeSuppression { get; }
        public bool TemporalAccumulation { get; }
        public float TemporalFeedback { get; }
        public float TemporalDepthRejection { get; }
        public float TemporalNormalRejection { get; }
        public float TemporalClamp { get; }
        public float TemporalVarianceClamp { get; }
        public float TemporalHitRejection { get; }

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
                hash = hash * 31 + Quantize(SpatialDenoiseRadius, 1000f);
                hash = hash * 31 + Quantize(SpatialDenoiseStrength, 1000f);
                hash = hash * 31 + Quantize(LeakGuardStrength, 1000f);
                hash = hash * 31 + Quantize(EdgeFadeStrength, 1000f);
                hash = hash * 31 + Quantize(NormalConeTightness, 1000f);
                hash = hash * 31 + Quantize(SkyEdgeSuppression, 1000f);
                hash = hash * 31 + (TemporalAccumulation ? 1 : 0);
                hash = hash * 31 + Quantize(TemporalFeedback, 1000f);
                hash = hash * 31 + Quantize(TemporalDepthRejection, 10000f);
                hash = hash * 31 + Quantize(TemporalNormalRejection, 1000f);
                hash = hash * 31 + Quantize(TemporalClamp, 1000f);
                hash = hash * 31 + Quantize(TemporalVarianceClamp, 1000f);
                hash = hash * 31 + Quantize(TemporalHitRejection, 1000f);
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

    internal readonly struct BurtScreenSpaceGlobalIlluminationScreenProbeSettings
    {
        public static readonly BurtScreenSpaceGlobalIlluminationScreenProbeSettings Disabled = new BurtScreenSpaceGlobalIlluminationScreenProbeSettings(false, 16, 12f, 8, 0.9f, 0f);

        public BurtScreenSpaceGlobalIlluminationScreenProbeSettings(
            bool enabled,
            int spacingPixels,
            float traceDistance,
            int sampleCount,
            float temporalFeedback,
            float applyStrength)
        {
            Enabled = enabled;
            SpacingPixels = Mathf.Clamp(spacingPixels, 4, 64);
            TraceDistance = Mathf.Clamp(traceDistance, 0.5f, 80f);
            SampleCount = Mathf.Clamp(sampleCount, 1, 32);
            TemporalFeedback = Mathf.Clamp(temporalFeedback, 0f, 0.98f);
            ApplyStrength = Mathf.Clamp01(applyStrength);
        }

        public bool Enabled { get; }
        public int SpacingPixels { get; }
        public float TraceDistance { get; }
        public int SampleCount { get; }
        public float TemporalFeedback { get; }
        public float ApplyStrength { get; }
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
        private const int HistoryAlgorithmVersion = 5;
        private const int CameraStatePruneInterval = 128;
        private const float ProjectionChangeEpsilon = 0.0001f;

        private sealed class CameraState
        {
            public Camera Camera;
            public RenderTexture ColorHistory;
            public RenderTexture DepthNormalHistory;
            public RenderTexture DebugPreviousColorHistory;
            public RenderTexture DebugPreviousDepthNormalHistory;
            public RenderTextureDescriptor ColorDescriptor;
            public RenderTextureDescriptor DepthNormalDescriptor;
            public RenderTextureDescriptor DebugPreviousColorDescriptor;
            public RenderTextureDescriptor DebugPreviousDepthNormalDescriptor;
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
            var debugColorMatches = state.DebugPreviousColorHistory != null && Matches(state.DebugPreviousColorDescriptor, colorDescriptor);
            var debugDepthNormalMatches = state.DebugPreviousDepthNormalHistory != null && Matches(state.DebugPreviousDepthNormalDescriptor, depthNormalDescriptor);
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
            else
            {
                if (!debugColorMatches)
                {
                    ReleaseTexture(state.DebugPreviousColorHistory);
                    state.DebugPreviousColorHistory = null;
                }

                if (!debugDepthNormalMatches)
                {
                    ReleaseTexture(state.DebugPreviousDepthNormalHistory);
                    state.DebugPreviousDepthNormalHistory = null;
                }
            }

            if (state.ColorHistory == null)
            {
                state.ColorDescriptor = colorDescriptor;
                state.ColorHistory = CreateHistoryTexture(colorDescriptor, "BurtGI Color History " + camera.GetInstanceID(), FilterMode.Bilinear);
                SetAllocationInvalidationReason(state, "HistoryAllocated");
            }

            if (state.DepthNormalHistory == null)
            {
                state.DepthNormalDescriptor = depthNormalDescriptor;
                state.DepthNormalHistory = CreateHistoryTexture(depthNormalDescriptor, "BurtGI Depth Normal History " + camera.GetInstanceID(), FilterMode.Point);
                SetAllocationInvalidationReason(state, "DepthNormalHistoryAllocated");
            }

            if (state.DebugPreviousColorHistory == null)
            {
                state.DebugPreviousColorDescriptor = colorDescriptor;
                state.DebugPreviousColorHistory = CreateHistoryTexture(colorDescriptor, "BurtGI Previous Color History Debug " + camera.GetInstanceID(), FilterMode.Bilinear);
            }

            if (state.DebugPreviousDepthNormalHistory == null)
            {
                state.DebugPreviousDepthNormalDescriptor = depthNormalDescriptor;
                state.DebugPreviousDepthNormalHistory = CreateHistoryTexture(depthNormalDescriptor, "BurtGI Previous Depth Normal History Debug " + camera.GetInstanceID(), FilterMode.Point);
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
            return GetHistoryStatus(camera, BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationHistorySettings(camera));
        }

        public static BurtScreenSpaceGlobalIlluminationHistoryStatus GetHistoryStatus(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            var settings = BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationSettings(request, asset);
            return GetHistoryStatus(request, settings);
        }

        public static BurtScreenSpaceGlobalIlluminationHistoryStatus GetHistoryStatus(BurtRenderRequest request, BurtScreenSpaceGlobalIlluminationSettings settings)
        {
            if (!settings.Enabled)
            {
                return CreateInactiveHistoryStatus("Disabled");
            }

            if (!settings.TemporalAccumulation)
            {
                return CreateInactiveHistoryStatus(BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationTemporalDisabledReason(request, settings));
            }

            return GetHistoryStatus(request != null ? request.Camera : null, settings);
        }

        public static BurtScreenSpaceGlobalIlluminationHistoryStatus GetHistoryStatus(Camera camera, BurtScreenSpaceGlobalIlluminationSettings settings)
        {
            if (!settings.Enabled)
            {
                return CreateInactiveHistoryStatus("Disabled");
            }

            if (!settings.TemporalAccumulation)
            {
                return CreateInactiveHistoryStatus("TemporalDisabled");
            }

            if (camera == null || !CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
            {
                return new BurtScreenSpaceGlobalIlluminationHistoryStatus(false, false, false, false, 0, 0, RenderTextureFormat.Default, 0, 0, 0, 0, "NoCameraOrHistory");
            }

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

        public static void CopyHistoryToDebugSnapshot(CommandBuffer cmd, Camera camera)
        {
            if (cmd == null || camera == null || !CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
            {
                return;
            }

            if (state.ColorHistory == null || state.DepthNormalHistory == null || state.DebugPreviousColorHistory == null || state.DebugPreviousDepthNormalHistory == null)
            {
                return;
            }

            cmd.CopyTexture(new RenderTargetIdentifier(state.ColorHistory), new RenderTargetIdentifier(state.DebugPreviousColorHistory));
            cmd.CopyTexture(new RenderTargetIdentifier(state.DepthNormalHistory), new RenderTargetIdentifier(state.DebugPreviousDepthNormalHistory));
            state.DebugPreviousViewProjectionMatrix = state.HasPreviousCameraState ? state.PreviousViewProjectionMatrix : state.CurrentViewProjectionMatrix;
        }

        public static BurtScreenSpaceGlobalIlluminationHistoryTextures GetDebugSnapshotTextures(BurtRenderRequest request)
        {
            var matrices = CreateCurrentMatrices(request);
            var camera = request != null ? request.Camera : null;
            if (camera == null || !CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
            {
                return BurtScreenSpaceGlobalIlluminationHistoryTextures.CreateInvalid(matrices);
            }

            var previousViewProjectionMatrix = state.DebugPreviousColorHistory != null && state.DebugPreviousDepthNormalHistory != null
                ? state.DebugPreviousViewProjectionMatrix
                : (state.HasPreviousCameraState ? state.PreviousViewProjectionMatrix : matrices.ViewProjectionMatrix);
            return new BurtScreenSpaceGlobalIlluminationHistoryTextures(state.DebugPreviousColorHistory, state.DebugPreviousDepthNormalHistory, previousViewProjectionMatrix, matrices.InverseViewProjectionMatrix);
        }

        private static BurtScreenSpaceGlobalIlluminationHistoryStatus CreateInactiveHistoryStatus(string reason)
        {
            return new BurtScreenSpaceGlobalIlluminationHistoryStatus(false, false, false, false, 0, 0, RenderTextureFormat.Default, 0, 0, 0, 0, reason);
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
            ReleaseTexture(state.DebugPreviousColorHistory);
            ReleaseTexture(state.DebugPreviousDepthNormalHistory);
            state.ColorHistory = null;
            state.DepthNormalHistory = null;
            state.DebugPreviousColorHistory = null;
            state.DebugPreviousDepthNormalHistory = null;
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
        public const string ScreenSpaceGlobalIlluminationShaderName = "Hidden/BurtRP/BurtGI";
        private static int shaderAvailabilityFrame = -1;
        private static bool shaderAvailable;

        public static bool ShouldUseScreenSpaceGlobalIllumination(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return ResolveScreenSpaceGlobalIlluminationSettings(request, asset).Enabled;
        }

        public static bool ShouldUseScreenSpaceGlobalIlluminationScreenProbeLite(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return ResolveScreenSpaceGlobalIlluminationScreenProbeSettings(request, asset).Enabled;
        }

        public static bool ShouldUseScreenSpaceGlobalIlluminationDebugView(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            if (!IsScreenSpaceGlobalIlluminationDebugMode(BurtShadingDebugSettings.Mode))
            {
                return false;
            }

            if (IsScreenSpaceGlobalIlluminationTemporalDiagnosticDebugMode(BurtShadingDebugSettings.Mode))
            {
                return ShouldUseScreenSpaceGlobalIlluminationTemporalDiagnostics(request, asset);
            }

            return ShouldUseScreenSpaceGlobalIllumination(request, asset);
        }

        public static bool ShouldUseScreenSpaceGlobalIlluminationTemporalDiagnostics(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            if (!IsScreenSpaceGlobalIlluminationTemporalDiagnosticDebugMode(BurtShadingDebugSettings.Mode))
            {
                return false;
            }

            var settings = ResolveScreenSpaceGlobalIlluminationSettings(request, asset);
            return settings.Enabled && settings.TemporalAccumulation;
        }

        public static bool IsScreenSpaceGlobalIlluminationDebugMode(BurtShadingDebugMode mode)
        {
            return mode == BurtShadingDebugMode.ScreenSpaceGlobalIlluminationRaw ||
                mode == BurtShadingDebugMode.ScreenSpaceGlobalIlluminationFinal ||
                mode == BurtShadingDebugMode.ScreenSpaceGlobalIlluminationHitRatio ||
                mode == BurtShadingDebugMode.ScreenSpaceGlobalIlluminationOverlay ||
                mode == BurtShadingDebugMode.ScreenSpaceGlobalIlluminationComposite ||
                mode == BurtShadingDebugMode.ScreenSpaceGlobalIlluminationHistory ||
                mode == BurtShadingDebugMode.ScreenSpaceGlobalIlluminationDifference ||
                mode == BurtShadingDebugMode.ScreenSpaceGlobalIlluminationLeakGuard ||
                mode == BurtShadingDebugMode.ScreenSpaceGlobalIlluminationDiagnosticCompare ||
                mode == BurtShadingDebugMode.ScreenSpaceGlobalIlluminationConfidence ||
                IsScreenSpaceGlobalIlluminationTemporalDiagnosticDebugMode(mode);
        }

        public static bool IsScreenSpaceGlobalIlluminationTemporalDiagnosticDebugMode(BurtShadingDebugMode mode)
        {
            return mode == BurtShadingDebugMode.ScreenSpaceGlobalIlluminationTemporalConfidence ||
                mode == BurtShadingDebugMode.ScreenSpaceGlobalIlluminationTemporalRejection;
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
                return DisableAndInvalidateHistory(request, "PreviewOrReflection");
            }

            if (asset == null || asset.RendererMode != BurtRendererMode.Deferred)
            {
                return DisableAndInvalidateHistory(request, "RendererNotDeferred");
            }

            if (IsScreenSpaceGlobalIlluminationSuppressedByShadingDebug())
            {
                return DisableAndInvalidateHistory(request, "ShadingDebug");
            }

            var component = GetScreenSpaceGlobalIlluminationVolumeComponent();
            if (component == null || !component.IsEnabled())
            {
                return DisableAndInvalidateHistory(request, "BurtGIDisabled");
            }

            if (!IsScreenSpaceGlobalIlluminationShaderAvailable())
            {
                return DisableAndInvalidateHistory(request, "ShaderMissing");
            }

            return ApplyTemporalAACompatibilityOverrides(CreateScreenSpaceGlobalIlluminationSettings(component), request);
        }

        public static BurtScreenSpaceGlobalIlluminationScreenProbeSettings ResolveScreenSpaceGlobalIlluminationScreenProbeSettings(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            if (!ResolveScreenSpaceGlobalIlluminationSettings(request, asset).Enabled)
            {
                return BurtScreenSpaceGlobalIlluminationScreenProbeSettings.Disabled;
            }

            var component = GetScreenSpaceGlobalIlluminationVolumeComponent();
            if (component == null || !component.screenProbeLite.value)
            {
                return BurtScreenSpaceGlobalIlluminationScreenProbeSettings.Disabled;
            }

            return new BurtScreenSpaceGlobalIlluminationScreenProbeSettings(
                true,
                component.screenProbeSpacingPixels.value,
                component.screenProbeTraceDistance.value,
                component.screenProbeSampleCount.value,
                component.screenProbeTemporalFeedback.value,
                component.screenProbeApplyStrength.value);
        }

        private static BurtScreenSpaceGlobalIlluminationSettings DisableAndInvalidateHistory(BurtRenderRequest request, string reason)
        {
            BurtScreenSpaceGlobalIlluminationHistoryUtility.InvalidateHistory(request != null ? request.Camera : null, reason);
            return BurtScreenSpaceGlobalIlluminationSettings.Disabled;
        }

        public static RenderTextureDescriptor CreateScreenSpaceGlobalIlluminationDescriptor(Camera camera, BurtScreenSpaceGlobalIlluminationSettings settings)
        {
            var descriptor = BurtRenderTargetDescriptorUtility.CreateScreenSpaceGlobalIlluminationDescriptor(camera);
            ApplyScreenSpaceGlobalIlluminationResolution(ref descriptor, settings);
            return descriptor;
        }

        public static RenderTextureDescriptor CreateScreenSpaceGlobalIlluminationDiagnosticsDescriptor(Camera camera, BurtScreenSpaceGlobalIlluminationSettings settings)
        {
            var descriptor = CreateScreenSpaceGlobalIlluminationDescriptor(camera, settings);
            descriptor.colorFormat = RenderTextureFormat.ARGB32;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            descriptor.sRGB = false;
            return descriptor;
        }

        public static RenderTextureDescriptor CreateScreenSpaceGlobalIlluminationScreenProbeDescriptor(Camera camera, BurtScreenSpaceGlobalIlluminationScreenProbeSettings settings)
        {
            var descriptor = BurtRenderTargetDescriptorUtility.CreateScreenSpaceGlobalIlluminationDescriptor(camera);
            descriptor.width = Mathf.Max(1, (descriptor.width + settings.SpacingPixels - 1) / settings.SpacingPixels);
            descriptor.height = Mathf.Max(1, (descriptor.height + settings.SpacingPixels - 1) / settings.SpacingPixels);
            descriptor.colorFormat = RenderTextureFormat.ARGBHalf;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            descriptor.sRGB = false;
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
            if (!settings.Enabled)
            {
                return "Disabled";
            }

            var label = "ScreenSpaceDiffuseBounce+DiffuseSourceFiltered+SkySHFallback+V3.2CoplanarGateFix+NearFieldEdgeGuardedColorBleed+NoSilhouetteEnergyFade+LeakGuardEdgeFadeNormalCone+DiffuseOcclusionFloor+EdgeSkyConfidence+HitAwareBlur+StableHitAlpha+ReadableLeakGuardDebug+GrazingPlaneReject+PerSampleJitter";
            if (settings.Resolution == BurtScreenSpaceGlobalIlluminationResolution.Full && !settings.TemporalAccumulation)
            {
                label += "+TAAFullResCurrent+StabilizedLeakGuard";
            }

            return label;
        }

        public static string ResolveScreenSpaceGlobalIlluminationPipelineStageLabel(BurtRenderRequest request, BurtRenderPipelineAsset asset, BurtRequestRenderOptions renderOptions, BurtScreenSpaceGlobalIlluminationSettings settings)
        {
            if (!settings.Enabled)
            {
                return "Disabled";
            }

            var filter = settings.Blur ? "HitAwareSpatialBilateral" : "None";
            var temporal = ShouldExposeScreenSpaceGlobalIlluminationToTemporalAA(request, asset, renderOptions)
                ? "ExternalTAA(CurrentGIAlpha)"
                : (settings.TemporalAccumulation ? "InternalHistory" : "Disabled(" + ResolveScreenSpaceGlobalIlluminationTemporalDisabledReason(request, settings) + ")");

            return "Prepare=GBuffer;Gather=ScreenSpaceDiffuseTrace(" + settings.Resolution + ");Filter=" + filter + ";Apply=ApplyIndirectBeforeDeferredLighting;TAA=" + temporal;
        }

        public static string ResolveScreenSpaceGlobalIlluminationXGIParityLabel(BurtScreenSpaceGlobalIlluminationSettings settings)
        {
            if (!settings.Enabled)
            {
                return "Disabled";
            }

            return "BurtGI=ScreenSpaceDiffuseApprox;XGIRef=Prepare>SceneRepresent>FinalGather>ApplyIndirect;Covered=DiffuseIndirectApply+BackfaceDiffuseApprox+RoughSpecularApprox;Missing=ScreenProbeLite,RadianceCache,TrueBackfaceDiffuse,TrueRoughSpecular,TranslucencyVolume";
        }

        public static string ResolveScreenSpaceGlobalIlluminationScreenProbeStageLabel(BurtScreenSpaceGlobalIlluminationScreenProbeSettings settings)
        {
            if (!settings.Enabled)
            {
                return "Disabled";
            }

            var apply = settings.ApplyStrength > 0f ? "DisabledPlaceholder(strength=" + settings.ApplyStrength.ToString("0.###") + ")" : "DebugOnly";
            return "Prepare=ProbeGrid;Gather=PlaceholderClear;Filter=Pending;Apply=" + apply;
        }

        public static string ResolveScreenSpaceGlobalIlluminationScreenProbeDebugLabel(BurtScreenSpaceGlobalIlluminationScreenProbeSettings settings)
        {
            if (!settings.Enabled)
            {
                return "Disabled";
            }

            return "Radiance=AllocatedClear;Irradiance=AllocatedClear;Confidence=AllocatedClear";
        }

        public static string ResolveScreenSpaceGlobalIlluminationDebugChannelLabel()
        {
            switch (BurtShadingDebugSettings.Mode)
            {
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationRaw:
                    return "RawRGB=TraceDiffuseGI;RawA=HitRatio";
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationFinal:
                    return "FinalRGB=FilteredDiffuseGI;FinalA=HitRatio";
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationHitRatio:
                    return "R=LowHitOrMiss;G=HitRatio;B=0";
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationOverlay:
                    return "RGB=CameraPlusGIContribution";
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationComposite:
                    return "RGB=GIContributionOnly";
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationTemporalConfidence:
                    return "R=TemporalRejection;G=Confidence;B=HitRatio";
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationTemporalRejection:
                    return "R=Rejected;G=Accepted;B=HitRatio";
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationHistory:
                    return "RGB=PreviousGIHistory";
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationDifference:
                    return "RGB=AmplifiedAbsFinalMinusHistory";
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationLeakGuard:
                    return "R=EdgeLeakRisk;G=DimStableSurfaceContext;B=SkyFallbackRisk";
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationDiagnosticCompare:
                    return "Quadrants=Raw,Final,HitRatio,LeakGuard";
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationConfidence:
                    return "Quadrants=HitRatio,SurfaceValidity,EdgeRisk,SkyFallbackRisk";
                default:
                    return "Disabled";
            }
        }

        public static string ResolveScreenSpaceGlobalIlluminationInspectLabel()
        {
            return "RawRGB+RawHitA>FinalRGB+FinalHitA>CompositeAdd>TAACurrentGIAlpha;Debug=HitRatio,LeakGuard,Confidence,DiagnosticCompare";
        }

        public static bool ShouldExposeScreenSpaceGlobalIlluminationToTemporalAA(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return ShouldExposeScreenSpaceGlobalIlluminationToTemporalAA(request, asset, null);
        }

        public static bool ShouldExposeScreenSpaceGlobalIlluminationToTemporalAA(BurtRenderRequest request, BurtRenderPipelineAsset asset, BurtRequestRenderOptions renderOptions)
        {
            var temporalAA = request != null ? request.TemporalAA : null;
            return temporalAA != null &&
                temporalAA.Enabled &&
                BurtTemporalAAUtility.ShouldUseTemporalAA(request, asset, renderOptions) &&
                ShouldUseScreenSpaceGlobalIllumination(request, asset);
        }

        public static string ResolveScreenSpaceGlobalIlluminationTemporalStatusLabel(BurtRenderRequest request, BurtScreenSpaceGlobalIlluminationSettings settings)
        {
            if (!settings.Enabled)
            {
                return "Disabled";
            }

            return settings.TemporalAccumulation
                ? "Enabled"
                : "Disabled(" + ResolveScreenSpaceGlobalIlluminationTemporalDisabledReason(request, settings) + ")";
        }

        public static string ResolveScreenSpaceGlobalIlluminationTemporalDisabledReason(BurtRenderRequest request, BurtScreenSpaceGlobalIlluminationSettings settings)
        {
            if (!settings.Enabled)
            {
                return "Disabled";
            }

            var component = GetScreenSpaceGlobalIlluminationVolumeComponent();
            if (component != null && component.temporalAccumulation.value && ShouldUseTemporalAACompatibility(request))
            {
                return "TAACompatibilityFullResCurrent";
            }

            return "TemporalDisabled";
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
                1.25f,
                0.75f,
                0.65f,
                0.5f,
                0.55f,
                0.6f,
                true,
                0.86f,
                0.02f,
                0.65f,
                1f,
                1.25f,
                0.55f);
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
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationTemporalConfidence:
                    return 6;
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationTemporalRejection:
                    return 7;
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationHistory:
                    return 8;
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationDifference:
                    return 9;
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationLeakGuard:
                    return 10;
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationDiagnosticCompare:
                    return 11;
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationConfidence:
                    return 12;
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
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationTemporalConfidence:
                    return "TemporalConfidence";
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationTemporalRejection:
                    return "TemporalRejection";
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationHistory:
                    return "History";
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationDifference:
                    return "Difference";
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationLeakGuard:
                    return "LeakGuard";
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationDiagnosticCompare:
                    return "DiagnosticCompare";
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationConfidence:
                    return "Confidence";
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
            var spatialDenoiseRadius = component.spatialDenoiseRadius.value;
            var spatialDenoiseStrength = component.spatialDenoiseStrength.value;
            var leakGuardStrength = component.leakGuardStrength.value;
            var edgeFadeStrength = component.edgeFadeStrength.value;
            var normalConeTightness = component.normalConeTightness.value;
            var skyEdgeSuppression = component.skyEdgeSuppression.value;
            var temporalVarianceClamp = component.temporalVarianceClamp.value;
            var temporalHitRejection = component.temporalHitRejection.value;
            ApplyScreenSpaceGlobalIlluminationQualityPreset(
                quality,
                ref resolution,
                ref sampleCount,
                ref maxSteps,
                ref radius,
                ref thickness,
                ref blur,
                ref blurSharpness,
                ref spatialDenoiseRadius,
                ref spatialDenoiseStrength,
                ref leakGuardStrength,
                ref edgeFadeStrength,
                ref normalConeTightness,
                ref skyEdgeSuppression,
                ref temporalVarianceClamp,
                ref temporalHitRejection);

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
                spatialDenoiseRadius,
                spatialDenoiseStrength,
                leakGuardStrength,
                edgeFadeStrength,
                normalConeTightness,
                skyEdgeSuppression,
                component.temporalAccumulation.value,
                component.temporalFeedback.value,
                component.temporalDepthRejection.value,
                component.temporalNormalRejection.value,
                component.temporalClamp.value,
                temporalVarianceClamp,
                temporalHitRejection);
        }

        private static BurtScreenSpaceGlobalIlluminationSettings ApplyTemporalAACompatibilityOverrides(
            BurtScreenSpaceGlobalIlluminationSettings settings,
            BurtRenderRequest request)
        {
            if (!settings.Enabled || !ShouldUseTemporalAACompatibility(request))
            {
                return settings;
            }

            return new BurtScreenSpaceGlobalIlluminationSettings(
                true,
                settings.Quality,
                BurtScreenSpaceGlobalIlluminationResolution.Full,
                settings.Intensity,
                settings.Radius,
                settings.SampleCount,
                settings.MaxSteps,
                settings.Thickness,
                settings.SkyFallback,
                settings.RadianceClamp,
                settings.NormalWeight,
                settings.DistanceFade,
                settings.Blur,
                Mathf.Max(settings.BlurSharpness, 0.22f),
                settings.SpatialDenoiseRadius,
                Mathf.Max(settings.SpatialDenoiseStrength, 0.82f),
                Mathf.Max(settings.LeakGuardStrength, 0.72f),
                Mathf.Max(settings.EdgeFadeStrength, 0.6f),
                Mathf.Max(settings.NormalConeTightness, 0.62f),
                Mathf.Max(settings.SkyEdgeSuppression, 0.7f),
                false,
                settings.TemporalFeedback,
                settings.TemporalDepthRejection,
                settings.TemporalNormalRejection,
                settings.TemporalClamp,
                settings.TemporalVarianceClamp,
                settings.TemporalHitRejection);
        }

        public static bool ShouldUseTemporalAACompatibility(BurtRenderRequest request)
        {
            var temporalAA = request != null ? request.TemporalAA : null;
            if (temporalAA != null && temporalAA.Enabled)
            {
                return true;
            }

            return IsScreenSpaceGlobalIlluminationDebugMode(BurtShadingDebugSettings.Mode) &&
                !IsScreenSpaceGlobalIlluminationTemporalDiagnosticDebugMode(BurtShadingDebugSettings.Mode) &&
                BurtPostProcessUtility.HasActiveTemporalAASource(request);
        }

        private static void ApplyScreenSpaceGlobalIlluminationQualityPreset(
            BurtScreenSpaceGlobalIlluminationQuality quality,
            ref BurtScreenSpaceGlobalIlluminationResolution resolution,
            ref int sampleCount,
            ref int maxSteps,
            ref float radius,
            ref float thickness,
            ref bool blur,
            ref float blurSharpness,
            ref float spatialDenoiseRadius,
            ref float spatialDenoiseStrength,
            ref float leakGuardStrength,
            ref float edgeFadeStrength,
            ref float normalConeTightness,
            ref float skyEdgeSuppression,
            ref float temporalVarianceClamp,
            ref float temporalHitRejection)
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
                    spatialDenoiseRadius = 1f;
                    spatialDenoiseStrength = 0.65f;
                    leakGuardStrength = 0.55f;
                    edgeFadeStrength = 0.45f;
                    normalConeTightness = 0.45f;
                    skyEdgeSuppression = 0.5f;
                    temporalVarianceClamp = 1f;
                    temporalHitRejection = 0.65f;
                    break;
                case BurtScreenSpaceGlobalIlluminationQuality.Medium:
                    resolution = BurtScreenSpaceGlobalIlluminationResolution.Half;
                    sampleCount = 12;
                    maxSteps = 8;
                    radius = Mathf.Max(radius, 2f);
                    thickness = Mathf.Max(thickness, 0.35f);
                    blur = true;
                    blurSharpness = 0.18f;
                    spatialDenoiseRadius = 1.25f;
                    spatialDenoiseStrength = 0.75f;
                    leakGuardStrength = 0.65f;
                    edgeFadeStrength = 0.5f;
                    normalConeTightness = 0.55f;
                    skyEdgeSuppression = 0.6f;
                    temporalVarianceClamp = 1.25f;
                    temporalHitRejection = 0.55f;
                    break;
                case BurtScreenSpaceGlobalIlluminationQuality.High:
                    resolution = BurtScreenSpaceGlobalIlluminationResolution.Full;
                    sampleCount = 20;
                    maxSteps = 12;
                    radius = Mathf.Max(radius, 3f);
                    thickness = Mathf.Max(thickness, 0.28f);
                    blur = true;
                    blurSharpness = 0.24f;
                    spatialDenoiseRadius = 1.5f;
                    spatialDenoiseStrength = 0.85f;
                    leakGuardStrength = 0.75f;
                    edgeFadeStrength = 0.6f;
                    normalConeTightness = 0.68f;
                    skyEdgeSuppression = 0.72f;
                    temporalVarianceClamp = 1.5f;
                    temporalHitRejection = 0.45f;
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
