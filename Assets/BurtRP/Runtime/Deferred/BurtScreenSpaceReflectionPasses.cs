using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    internal sealed class BurtAllocateScreenSpaceReflectionColorPass : BurtRenderPass
    {
        public override string Name => "Burt Allocate Screen Space Reflection Color";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceReflectionPassUtility.ShouldUseScreenSpaceReflections(builder.Request, builder.Asset))
            {
                return;
            }

            builder.WriteScreenSpaceReflectionColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceReflectionPassUtility.ShouldUseScreenSpaceReflections(context.Request, context.Asset))
            {
                return;
            }

            var target = context.ScreenSpaceReflectionColorTarget;
            if (!target.IsValid)
            {
                return;
            }

            var camera = context.Request != null ? context.Request.Camera : null;
            var settings = BurtScreenSpaceReflectionPassUtility.ResolveScreenSpaceReflectionSettings(context.Request, context.Asset);
            var descriptor = BurtScreenSpaceReflectionPassUtility.CreateScreenSpaceReflectionColorDescriptor(camera, settings);
            var cmd = CommandBufferPool.Get(Name);
            cmd.GetTemporaryRT(BurtRenderGraphResourceRegistry.ScreenSpaceReflectionColorTextureId, descriptor, FilterMode.Bilinear);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.ScreenSpaceReflectionColorTextureId, target.Identifier);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    internal sealed class BurtAllocateScreenSpaceReflectionDenoisedColorPass : BurtRenderPass
    {
        public override string Name => "Burt Allocate Screen Space Reflection Denoised Color";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceReflectionPassUtility.ShouldUseScreenSpaceReflections(builder.Request, builder.Asset))
            {
                return;
            }

            builder.WriteScreenSpaceReflectionDenoisedColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceReflectionPassUtility.ShouldUseScreenSpaceReflections(context.Request, context.Asset))
            {
                return;
            }

            var target = context.ScreenSpaceReflectionDenoisedColorTarget;
            if (!target.IsValid)
            {
                return;
            }

            var camera = context.Request != null ? context.Request.Camera : null;
            var settings = BurtScreenSpaceReflectionPassUtility.ResolveScreenSpaceReflectionSettings(context.Request, context.Asset);
            var descriptor = BurtScreenSpaceReflectionPassUtility.CreateScreenSpaceReflectionDenoisedColorDescriptor(camera, settings);
            var cmd = CommandBufferPool.Get(Name);
            cmd.GetTemporaryRT(BurtRenderGraphResourceRegistry.ScreenSpaceReflectionDenoisedColorTextureId, descriptor, FilterMode.Bilinear);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.ScreenSpaceReflectionDenoisedColorTextureId, target.Identifier);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    internal sealed class BurtAllocateScreenSpaceReflectionTemporalColorPass : BurtRenderPass
    {
        public override string Name => "Burt Allocate Screen Space Reflection Temporal Color";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceReflectionPassUtility.ShouldUseScreenSpaceReflections(builder.Request, builder.Asset))
            {
                return;
            }

            builder.WriteScreenSpaceReflectionTemporalColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceReflectionPassUtility.ShouldUseScreenSpaceReflections(context.Request, context.Asset))
            {
                return;
            }

            var target = context.ScreenSpaceReflectionTemporalColorTarget;
            if (!target.IsValid)
            {
                return;
            }

            var camera = context.Request != null ? context.Request.Camera : null;
            var settings = BurtScreenSpaceReflectionPassUtility.ResolveScreenSpaceReflectionSettings(context.Request, context.Asset);
            var descriptor = BurtScreenSpaceReflectionPassUtility.CreateScreenSpaceReflectionTemporalColorDescriptor(camera, settings);
            var cmd = CommandBufferPool.Get(Name);
            cmd.GetTemporaryRT(BurtRenderGraphResourceRegistry.ScreenSpaceReflectionTemporalColorTextureId, descriptor, FilterMode.Bilinear);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.ScreenSpaceReflectionTemporalColorTextureId, target.Identifier);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    internal sealed class BurtScreenSpaceReflectionTracePass : BurtRenderPass
    {
        private const string ScreenSpaceReflectionShaderName = BurtScreenSpaceReflectionPassUtility.ScreenSpaceReflectionShaderName;
        private const string ScreenSpaceReflectionHiZTraceKeyword = "BURT_SSR_HIZ_TRACE";
        private const float ScreenSpaceReflectionEdgeFadeWidth = 0.04f;
        private static readonly int CameraColorTextureId = BurtRenderGraphResourceRegistry.CameraColorTextureId;
        private static readonly int CameraDepthTextureId = BurtRenderGraphResourceRegistry.CameraDepthTextureId;
        private static readonly int GBuffer0Id = BurtRenderGraphResourceRegistry.GBuffer0Id;
        private static readonly int GBuffer1Id = BurtRenderGraphResourceRegistry.GBuffer1Id;
        private static readonly int GBuffer2Id = BurtRenderGraphResourceRegistry.GBuffer2Id;
        private static readonly int GBuffer3Id = BurtRenderGraphResourceRegistry.GBuffer3Id;
        private static readonly int GBuffer4Id = BurtRenderGraphResourceRegistry.GBuffer4Id;
        private static readonly int HiZDepthTextureId = BurtRenderGraphResourceRegistry.HiZDepthTextureId;
        private static readonly int SSRSourceColorTextureId = Shader.PropertyToID("_BurtSSRSourceColorTexture");
        private static readonly int SSRSourceTexelSizeId = Shader.PropertyToID("_BurtSSRSourceTexelSize");
        private static readonly int SSRViewMatrixId = Shader.PropertyToID("_BurtSSRViewMatrix");
        private static readonly int SSRViewProjectionMatrixId = Shader.PropertyToID("_BurtSSRViewProjectionMatrix");
        private static readonly int SSRParams0Id = Shader.PropertyToID("_BurtSSRParams0");
        private static readonly int SSRParams1Id = Shader.PropertyToID("_BurtSSRParams1");
        private static readonly int SSRParams2Id = Shader.PropertyToID("_BurtSSRParams2");
        private static readonly int InverseViewProjectionMatrixId = Shader.PropertyToID("_BurtDeferredInverseViewProjectionMatrix");
        private static readonly int CameraWorldPositionId = Shader.PropertyToID("_BurtDeferredCameraWorldPosition");
        private static readonly int DeferredScreenSizeId = Shader.PropertyToID("_BurtDeferredScreenSize");

        private Material screenSpaceReflectionMaterial;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Screen Space Reflections Trace";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceReflectionPassUtility.ShouldUseScreenSpaceReflections(builder.Request, builder.Asset))
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
            if (BurtScreenSpaceReflectionPassUtility.ShouldUseScreenSpaceReflectionHiZTrace(builder.Request, builder.Asset))
            {
                builder.ReadHiZDepth();
            }
            builder.WriteScreenSpaceReflectionColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!TryGetTargets(context, out var cameraColorTarget, out var cameraDepthTarget, out var gbuffer0Target, out var gbuffer1Target, out var gbuffer2Target, out var gbuffer3Target, out var gbuffer4Target, out var hiZDepthTarget, out var ssrColorTarget))
            {
                return;
            }

            var settings = BurtScreenSpaceReflectionPassUtility.ResolveScreenSpaceReflectionSettings(context.Request, context.Asset);
            if (!settings.Enabled)
            {
                return;
            }

            var material = GetScreenSpaceReflectionMaterial();
            if (material == null)
            {
                return;
            }

            var camera = context.Request != null ? context.Request.Camera : null;
            if (camera == null)
            {
                return;
            }

            var colorDescriptor = BurtScreenSpaceReflectionPassUtility.CreateScreenSpaceReflectionColorDescriptor(camera, settings);
            var hiZDescriptor = BurtRenderTargetDescriptorUtility.CreateHiZDepthDescriptor(camera);
            var hiZMipCount = BurtRenderTargetDescriptorUtility.CalculateMipCount(hiZDescriptor.width, hiZDescriptor.height);
            var hasUsableHiZDepth = hiZDepthTarget.IsValid &&
                hiZMipCount > 1 &&
                BurtHiZDepthPassUtility.IsHiZDepthShaderAvailable();
            var hiZTraceForShader = ShouldEnableHiZTraceVariant(settings, hasUsableHiZDepth);
            SetKeyword(material, ScreenSpaceReflectionHiZTraceKeyword, hiZTraceForShader);
            var cmd = CommandBufferPool.Get(Name);

            cmd.SetRenderTarget(ssrColorTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, colorDescriptor.width, colorDescriptor.height);
            BindInputs(cmd, cameraColorTarget, cameraDepthTarget, gbuffer0Target, gbuffer1Target, gbuffer2Target, gbuffer3Target, gbuffer4Target, hiZDepthTarget);
            UploadCameraGlobals(cmd, camera, colorDescriptor, hiZMipCount);
            UploadSettings(cmd, settings, hiZMipCount, hasUsableHiZDepth, hiZTraceForShader);
            cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1);

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
            out BurtRenderTargetHandle hiZDepthTarget,
            out BurtRenderTargetHandle ssrColorTarget)
        {
            cameraColorTarget = context != null ? context.CameraColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraColorName);
            cameraDepthTarget = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            gbuffer0Target = context != null ? context.GBuffer0Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer0Name);
            gbuffer1Target = context != null ? context.GBuffer1Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer1Name);
            gbuffer2Target = context != null ? context.GBuffer2Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer2Name);
            gbuffer3Target = context != null ? context.GBuffer3Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer3Name);
            gbuffer4Target = context != null ? context.GBuffer4Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer4Name);
            hiZDepthTarget = context != null ? context.HiZDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.HiZDepthName);
            ssrColorTarget = context != null ? context.ScreenSpaceReflectionColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceReflectionColorName);

            return cameraColorTarget.IsValid &&
                cameraDepthTarget.IsValid &&
                gbuffer0Target.IsValid &&
                gbuffer1Target.IsValid &&
                gbuffer2Target.IsValid &&
                gbuffer3Target.IsValid &&
                gbuffer4Target.IsValid &&
                ssrColorTarget.IsValid;
        }

        private static void BindInputs(
            CommandBuffer cmd,
            BurtRenderTargetHandle cameraColorTarget,
            BurtRenderTargetHandle cameraDepthTarget,
            BurtRenderTargetHandle gbuffer0Target,
            BurtRenderTargetHandle gbuffer1Target,
            BurtRenderTargetHandle gbuffer2Target,
            BurtRenderTargetHandle gbuffer3Target,
            BurtRenderTargetHandle gbuffer4Target,
            BurtRenderTargetHandle hiZDepthTarget)
        {
            cmd.SetGlobalTexture(CameraColorTextureId, cameraColorTarget.Identifier);
            cmd.SetGlobalTexture(SSRSourceColorTextureId, cameraColorTarget.Identifier);
            cmd.SetGlobalTexture(CameraDepthTextureId, cameraDepthTarget.Identifier);
            cmd.SetGlobalTexture(GBuffer0Id, gbuffer0Target.Identifier);
            cmd.SetGlobalTexture(GBuffer1Id, gbuffer1Target.Identifier);
            cmd.SetGlobalTexture(GBuffer2Id, gbuffer2Target.Identifier);
            cmd.SetGlobalTexture(GBuffer3Id, gbuffer3Target.Identifier);
            cmd.SetGlobalTexture(GBuffer4Id, gbuffer4Target.Identifier);
            cmd.SetGlobalTexture(HiZDepthTextureId, hiZDepthTarget.IsValid ? hiZDepthTarget.Identifier : cameraDepthTarget.Identifier);
        }

        private static void UploadCameraGlobals(
            CommandBuffer cmd,
            Camera camera,
            RenderTextureDescriptor colorDescriptor,
            int hiZMipCount)
        {
            var viewMatrix = camera.worldToCameraMatrix;
            var projectionMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
            var viewProjectionMatrix = projectionMatrix * viewMatrix;
            var inverseViewProjectionMatrix = viewProjectionMatrix.inverse;
            var width = Mathf.Max(1, colorDescriptor.width);
            var height = Mathf.Max(1, colorDescriptor.height);
            BurtScreenSpaceReflectionPassUtility.GetCameraTargetSize(camera, out var cameraWidth, out var cameraHeight);
            var cameraPosition = camera.transform.position;

            cmd.SetGlobalMatrix(SSRViewMatrixId, viewMatrix);
            cmd.SetGlobalMatrix(SSRViewProjectionMatrixId, viewProjectionMatrix);
            cmd.SetGlobalMatrix(InverseViewProjectionMatrixId, inverseViewProjectionMatrix);
            cmd.SetGlobalVector(CameraWorldPositionId, new Vector4(cameraPosition.x, cameraPosition.y, cameraPosition.z, 1f));
            cmd.SetGlobalVector(DeferredScreenSizeId, new Vector4(cameraWidth, cameraHeight, 1f / cameraWidth, 1f / cameraHeight));
            cmd.SetGlobalVector(SSRSourceTexelSizeId, new Vector4(1f / width, 1f / height, width, height));
        }

        private static void UploadSettings(
            CommandBuffer cmd,
            BurtScreenSpaceReflectionSettings settings,
            int hiZMipCount,
            bool hasUsableHiZDepth,
            bool hiZTraceForShader)
        {
            var maxMip = Mathf.Max(0, hiZMipCount - 1);
            var debugMode = BurtScreenSpaceReflectionPassUtility.ResolveScreenSpaceReflectionShaderDebugMode();

            cmd.SetGlobalVector(SSRParams0Id, new Vector4(settings.MaxDistance, settings.Thickness, settings.Intensity, settings.RoughnessFade));
            cmd.SetGlobalVector(SSRParams1Id, new Vector4(settings.MaxSteps, maxMip, debugMode, ScreenSpaceReflectionEdgeFadeWidth));
            cmd.SetGlobalVector(SSRParams2Id, new Vector4(
                Time.frameCount & 7,
                settings.TemporalAccumulation ? 1f : 0f,
                hiZTraceForShader ? 1f : 0f,
                hasUsableHiZDepth ? 1f : 0f));
        }

        private static bool ShouldEnableHiZTraceVariant(BurtScreenSpaceReflectionSettings settings, bool hasUsableHiZDepth)
        {
            return hasUsableHiZDepth && (settings.ExperimentalHiZTrace ||
                BurtScreenSpaceReflectionPassUtility.IsScreenSpaceReflectionHiZStepSavedDebugMode(BurtShadingDebugSettings.Mode));
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
            {
                material.EnableKeyword(keyword);
            }
            else
            {
                material.DisableKeyword(keyword);
            }
        }

        private Material GetScreenSpaceReflectionMaterial()
        {
            if (screenSpaceReflectionMaterial != null)
            {
                return screenSpaceReflectionMaterial;
            }

            var shader = Shader.Find(ScreenSpaceReflectionShaderName);
            if (shader == null)
            {
                if (!hasLoggedMissingShader)
                {
                    Debug.LogWarning("BurtRP could not find shader: " + ScreenSpaceReflectionShaderName);
                    hasLoggedMissingShader = true;
                }

                return null;
            }

            screenSpaceReflectionMaterial = new Material(shader);
            screenSpaceReflectionMaterial.hideFlags = HideFlags.HideAndDontSave;
            return screenSpaceReflectionMaterial;
        }
    }

    internal sealed class BurtScreenSpaceReflectionDenoisePass : BurtRenderPass
    {
        private const string ScreenSpaceReflectionShaderName = BurtScreenSpaceReflectionPassUtility.ScreenSpaceReflectionShaderName;
        private static readonly int CameraDepthTextureId = BurtRenderGraphResourceRegistry.CameraDepthTextureId;
        private static readonly int GBuffer0Id = BurtRenderGraphResourceRegistry.GBuffer0Id;
        private static readonly int GBuffer1Id = BurtRenderGraphResourceRegistry.GBuffer1Id;
        private static readonly int GBuffer2Id = BurtRenderGraphResourceRegistry.GBuffer2Id;
        private static readonly int GBuffer3Id = BurtRenderGraphResourceRegistry.GBuffer3Id;
        private static readonly int GBuffer4Id = BurtRenderGraphResourceRegistry.GBuffer4Id;
        private static readonly int SSRColorTextureId = BurtRenderGraphResourceRegistry.ScreenSpaceReflectionColorTextureId;
        private static readonly int SSRDenoisedColorTextureId = BurtRenderGraphResourceRegistry.ScreenSpaceReflectionDenoisedColorTextureId;
        private static readonly int SSRSourceTexelSizeId = Shader.PropertyToID("_BurtSSRSourceTexelSize");
        private static readonly int SSRParams1Id = Shader.PropertyToID("_BurtSSRParams1");

        private Material screenSpaceReflectionMaterial;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Screen Space Reflections Denoise";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceReflectionPassUtility.ShouldUseScreenSpaceReflections(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceReflectionColor();
            builder.ReadCameraDepth();
            builder.ReadGBuffer0();
            builder.ReadGBuffer1();
            builder.ReadGBuffer2();
            builder.ReadGBuffer3();
            builder.ReadGBuffer4();
            builder.WriteScreenSpaceReflectionDenoisedColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!TryGetTargets(context, out var cameraDepthTarget, out var gbuffer0Target, out var gbuffer1Target, out var gbuffer2Target, out var gbuffer3Target, out var gbuffer4Target, out var ssrColorTarget, out var ssrDenoisedColorTarget))
            {
                return;
            }

            if (!BurtScreenSpaceReflectionPassUtility.ShouldUseScreenSpaceReflections(context.Request, context.Asset))
            {
                return;
            }

            var material = GetScreenSpaceReflectionMaterial();
            if (material == null)
            {
                return;
            }

            var camera = context.Request != null ? context.Request.Camera : null;
            if (camera == null)
            {
                return;
            }

            var settings = BurtScreenSpaceReflectionPassUtility.ResolveScreenSpaceReflectionSettings(context.Request, context.Asset);
            if (!settings.Enabled)
            {
                return;
            }

            var descriptor = BurtScreenSpaceReflectionPassUtility.CreateScreenSpaceReflectionDenoisedColorDescriptor(camera, settings);
            var width = Mathf.Max(1, descriptor.width);
            var height = Mathf.Max(1, descriptor.height);
            var cmd = CommandBufferPool.Get(Name);
            cmd.SetRenderTarget(ssrDenoisedColorTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, width, height);
            cmd.SetGlobalTexture(CameraDepthTextureId, cameraDepthTarget.Identifier);
            cmd.SetGlobalTexture(GBuffer0Id, gbuffer0Target.Identifier);
            cmd.SetGlobalTexture(GBuffer1Id, gbuffer1Target.Identifier);
            cmd.SetGlobalTexture(GBuffer2Id, gbuffer2Target.Identifier);
            cmd.SetGlobalTexture(GBuffer3Id, gbuffer3Target.Identifier);
            cmd.SetGlobalTexture(GBuffer4Id, gbuffer4Target.Identifier);
            cmd.SetGlobalTexture(SSRColorTextureId, ssrColorTarget.Identifier);
            cmd.SetGlobalVector(SSRSourceTexelSizeId, new Vector4(1f / width, 1f / height, width, height));
            cmd.SetGlobalVector(SSRParams1Id, new Vector4(0f, 0f, BurtScreenSpaceReflectionPassUtility.ResolveScreenSpaceReflectionShaderDebugMode(), 0.1f));
            cmd.DrawProcedural(Matrix4x4.identity, material, 1, MeshTopology.Triangles, 3, 1);
            cmd.SetGlobalTexture(SSRDenoisedColorTextureId, ssrDenoisedColorTarget.Identifier);
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
            out BurtRenderTargetHandle ssrColorTarget,
            out BurtRenderTargetHandle ssrDenoisedColorTarget)
        {
            cameraDepthTarget = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            gbuffer0Target = context != null ? context.GBuffer0Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer0Name);
            gbuffer1Target = context != null ? context.GBuffer1Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer1Name);
            gbuffer2Target = context != null ? context.GBuffer2Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer2Name);
            gbuffer3Target = context != null ? context.GBuffer3Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer3Name);
            gbuffer4Target = context != null ? context.GBuffer4Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer4Name);
            ssrColorTarget = context != null ? context.ScreenSpaceReflectionColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceReflectionColorName);
            ssrDenoisedColorTarget = context != null ? context.ScreenSpaceReflectionDenoisedColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceReflectionDenoisedColorName);

            return cameraDepthTarget.IsValid &&
                gbuffer0Target.IsValid &&
                gbuffer1Target.IsValid &&
                gbuffer2Target.IsValid &&
                gbuffer3Target.IsValid &&
                gbuffer4Target.IsValid &&
                ssrColorTarget.IsValid &&
                ssrDenoisedColorTarget.IsValid;
        }

        private Material GetScreenSpaceReflectionMaterial()
        {
            if (screenSpaceReflectionMaterial != null)
            {
                return screenSpaceReflectionMaterial;
            }

            var shader = Shader.Find(ScreenSpaceReflectionShaderName);
            if (shader == null)
            {
                if (!hasLoggedMissingShader)
                {
                    Debug.LogWarning("BurtRP could not find shader: " + ScreenSpaceReflectionShaderName);
                    hasLoggedMissingShader = true;
                }

                return null;
            }

            screenSpaceReflectionMaterial = new Material(shader);
            screenSpaceReflectionMaterial.hideFlags = HideFlags.HideAndDontSave;
            return screenSpaceReflectionMaterial;
        }
    }

    internal sealed class BurtScreenSpaceReflectionTemporalPass : BurtRenderPass
    {
        private const string ScreenSpaceReflectionShaderName = BurtScreenSpaceReflectionPassUtility.ScreenSpaceReflectionShaderName;
        private static readonly int CameraDepthTextureId = BurtRenderGraphResourceRegistry.CameraDepthTextureId;
        private static readonly int GBuffer0Id = BurtRenderGraphResourceRegistry.GBuffer0Id;
        private static readonly int GBuffer1Id = BurtRenderGraphResourceRegistry.GBuffer1Id;
        private static readonly int GBuffer2Id = BurtRenderGraphResourceRegistry.GBuffer2Id;
        private static readonly int GBuffer3Id = BurtRenderGraphResourceRegistry.GBuffer3Id;
        private static readonly int GBuffer4Id = BurtRenderGraphResourceRegistry.GBuffer4Id;
        private static readonly int SSRDenoisedColorTextureId = BurtRenderGraphResourceRegistry.ScreenSpaceReflectionDenoisedColorTextureId;
        private static readonly int SSRTemporalColorTextureId = BurtRenderGraphResourceRegistry.ScreenSpaceReflectionTemporalColorTextureId;
        private static readonly int SSRHistoryTextureId = Shader.PropertyToID("_BurtSSRHistoryTexture");
        private static readonly int SSRHistoryDepthTextureId = Shader.PropertyToID("_BurtSSRHistoryDepthTexture");
        private static readonly int SSRHistoryNormalRoughnessTextureId = Shader.PropertyToID("_BurtSSRHistoryNormalRoughnessTexture");
        private static readonly int SSRHistoryMomentTextureId = Shader.PropertyToID("_BurtSSRHistoryMomentTexture");
        private static readonly int SSRCurrentMomentTextureId = Shader.PropertyToID("_BurtSSRCurrentMomentTexture");
        private static readonly int SSRSourceTexelSizeId = Shader.PropertyToID("_BurtSSRSourceTexelSize");
        private static readonly int SSRPreviousViewMatrixId = Shader.PropertyToID("_BurtSSRPreviousViewMatrix");
        private static readonly int SSRPreviousViewProjectionMatrixId = Shader.PropertyToID("_BurtSSRPreviousViewProjectionMatrix");
        private static readonly int SSRTemporalParams0Id = Shader.PropertyToID("_BurtSSRTemporalParams0");
        private static readonly int SSRParams1Id = Shader.PropertyToID("_BurtSSRParams1");
        private static readonly int InverseViewProjectionMatrixId = Shader.PropertyToID("_BurtDeferredInverseViewProjectionMatrix");
        private static readonly int DeferredScreenSizeId = Shader.PropertyToID("_BurtDeferredScreenSize");

        private Material screenSpaceReflectionMaterial;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Screen Space Reflections Temporal";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceReflectionPassUtility.ShouldUseScreenSpaceReflections(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceReflectionDenoisedColor();
            builder.ReadCameraDepth();
            builder.ReadGBuffer0();
            builder.ReadGBuffer1();
            builder.ReadGBuffer2();
            builder.ReadGBuffer3();
            builder.ReadGBuffer4();
            builder.WriteScreenSpaceReflectionTemporalColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!TryGetTargets(context, out var cameraDepthTarget, out var gbuffer0Target, out var gbuffer1Target, out var gbuffer2Target, out var gbuffer3Target, out var gbuffer4Target, out var ssrDenoisedColorTarget, out var ssrTemporalColorTarget))
            {
                return;
            }

            var settings = BurtScreenSpaceReflectionPassUtility.ResolveScreenSpaceReflectionSettings(context.Request, context.Asset);
            if (!settings.Enabled)
            {
                return;
            }

            var material = GetScreenSpaceReflectionMaterial();
            if (material == null)
            {
                return;
            }

            var camera = context.Request != null ? context.Request.Camera : null;
            if (camera == null)
            {
                return;
            }

            var descriptor = BurtScreenSpaceReflectionPassUtility.CreateScreenSpaceReflectionTemporalColorDescriptor(camera, settings);
            var width = Mathf.Max(1, descriptor.width);
            var height = Mathf.Max(1, descriptor.height);
            var historyValid = false;
            var history = settings.TemporalAccumulation
                ? BurtScreenSpaceReflectionHistoryUtility.EnsureHistoryTextures(context.Request, context.Asset, settings, out historyValid)
                : BurtScreenSpaceReflectionHistoryTextures.CreateInvalid(BurtScreenSpaceReflectionHistoryUtility.CreateCurrentMatrices(context.Request));

            if (!settings.TemporalAccumulation)
            {
                historyValid = false;
                BurtScreenSpaceReflectionHistoryUtility.InvalidateHistory(camera, "TemporalDisabled");
            }

            var cmd = CommandBufferPool.Get(Name);
            BindInputs(cmd, cameraDepthTarget, gbuffer0Target, gbuffer1Target, gbuffer2Target, gbuffer3Target, gbuffer4Target, ssrDenoisedColorTarget);
            UploadTemporalGlobals(cmd, camera, history, settings, width, height, historyValid);

            cmd.SetRenderTarget(ssrTemporalColorTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, width, height);
            cmd.DrawProcedural(Matrix4x4.identity, material, 3, MeshTopology.Triangles, 3, 1);
            cmd.GenerateMips(ssrTemporalColorTarget.Identifier);
            cmd.SetGlobalTexture(SSRTemporalColorTextureId, ssrTemporalColorTarget.Identifier);

            if (settings.TemporalAccumulation && history.Color != null && history.Depth != null && history.NormalRoughness != null && history.Moment != null)
            {
                var momentDescriptor = BurtScreenSpaceReflectionHistoryUtility.CreateMomentHistoryDescriptor(camera, settings);
                cmd.GetTemporaryRT(SSRCurrentMomentTextureId, momentDescriptor, FilterMode.Point);
                var currentMomentTarget = new RenderTargetIdentifier(SSRCurrentMomentTextureId);
                cmd.SetRenderTarget(currentMomentTarget);
                BurtRenderTargetDescriptorUtility.SetViewport(cmd, width, height);
                cmd.DrawProcedural(Matrix4x4.identity, material, 7, MeshTopology.Triangles, 3, 1);
                cmd.CopyTexture(currentMomentTarget, new RenderTargetIdentifier(history.Moment));
                cmd.ReleaseTemporaryRT(SSRCurrentMomentTextureId);

                cmd.SetRenderTarget(history.Color);
                BurtRenderTargetDescriptorUtility.SetViewport(cmd, width, height);
                cmd.DrawProcedural(Matrix4x4.identity, material, 4, MeshTopology.Triangles, 3, 1);

                cmd.SetRenderTarget(history.Depth);
                BurtRenderTargetDescriptorUtility.SetViewport(cmd, width, height);
                cmd.DrawProcedural(Matrix4x4.identity, material, 5, MeshTopology.Triangles, 3, 1);

                cmd.SetRenderTarget(history.NormalRoughness);
                BurtRenderTargetDescriptorUtility.SetViewport(cmd, width, height);
                cmd.DrawProcedural(Matrix4x4.identity, material, 6, MeshTopology.Triangles, 3, 1);
                BurtScreenSpaceReflectionHistoryUtility.MarkHistoryValid(camera);
            }

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
            out BurtRenderTargetHandle ssrDenoisedColorTarget,
            out BurtRenderTargetHandle ssrTemporalColorTarget)
        {
            cameraDepthTarget = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            gbuffer0Target = context != null ? context.GBuffer0Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer0Name);
            gbuffer1Target = context != null ? context.GBuffer1Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer1Name);
            gbuffer2Target = context != null ? context.GBuffer2Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer2Name);
            gbuffer3Target = context != null ? context.GBuffer3Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer3Name);
            gbuffer4Target = context != null ? context.GBuffer4Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer4Name);
            ssrDenoisedColorTarget = context != null ? context.ScreenSpaceReflectionDenoisedColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceReflectionDenoisedColorName);
            ssrTemporalColorTarget = context != null ? context.ScreenSpaceReflectionTemporalColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceReflectionTemporalColorName);

            return cameraDepthTarget.IsValid &&
                gbuffer0Target.IsValid &&
                gbuffer1Target.IsValid &&
                gbuffer2Target.IsValid &&
                gbuffer3Target.IsValid &&
                gbuffer4Target.IsValid &&
                ssrDenoisedColorTarget.IsValid &&
                ssrTemporalColorTarget.IsValid;
        }

        private static void BindInputs(
            CommandBuffer cmd,
            BurtRenderTargetHandle cameraDepthTarget,
            BurtRenderTargetHandle gbuffer0Target,
            BurtRenderTargetHandle gbuffer1Target,
            BurtRenderTargetHandle gbuffer2Target,
            BurtRenderTargetHandle gbuffer3Target,
            BurtRenderTargetHandle gbuffer4Target,
            BurtRenderTargetHandle ssrDenoisedColorTarget)
        {
            cmd.SetGlobalTexture(CameraDepthTextureId, cameraDepthTarget.Identifier);
            cmd.SetGlobalTexture(GBuffer0Id, gbuffer0Target.Identifier);
            cmd.SetGlobalTexture(GBuffer1Id, gbuffer1Target.Identifier);
            cmd.SetGlobalTexture(GBuffer2Id, gbuffer2Target.Identifier);
            cmd.SetGlobalTexture(GBuffer3Id, gbuffer3Target.Identifier);
            cmd.SetGlobalTexture(GBuffer4Id, gbuffer4Target.Identifier);
            cmd.SetGlobalTexture(SSRDenoisedColorTextureId, ssrDenoisedColorTarget.Identifier);
        }

        private static void UploadTemporalGlobals(
            CommandBuffer cmd,
            Camera camera,
            BurtScreenSpaceReflectionHistoryTextures history,
            BurtScreenSpaceReflectionSettings settings,
            int width,
            int height,
            bool historyValid)
        {
            BurtScreenSpaceReflectionPassUtility.GetCameraTargetSize(camera, out var cameraWidth, out var cameraHeight);
            cmd.SetGlobalTexture(SSRHistoryTextureId, history.Color != null ? (Texture)history.Color : Texture2D.blackTexture);
            cmd.SetGlobalTexture(SSRHistoryDepthTextureId, history.Depth != null ? (Texture)history.Depth : Texture2D.blackTexture);
            cmd.SetGlobalTexture(SSRHistoryNormalRoughnessTextureId, history.NormalRoughness != null ? (Texture)history.NormalRoughness : Texture2D.blackTexture);
            cmd.SetGlobalTexture(SSRHistoryMomentTextureId, history.Moment != null ? (Texture)history.Moment : Texture2D.blackTexture);
            cmd.SetGlobalMatrix(SSRPreviousViewMatrixId, history.PreviousViewMatrix);
            cmd.SetGlobalMatrix(SSRPreviousViewProjectionMatrixId, history.PreviousViewProjectionMatrix);
            cmd.SetGlobalMatrix(InverseViewProjectionMatrixId, history.CurrentInverseViewProjectionMatrix);
            cmd.SetGlobalVector(DeferredScreenSizeId, new Vector4(cameraWidth, cameraHeight, 1f / cameraWidth, 1f / cameraHeight));
            cmd.SetGlobalVector(SSRSourceTexelSizeId, new Vector4(1f / width, 1f / height, width, height));
            cmd.SetGlobalVector(SSRTemporalParams0Id, new Vector4(settings.TemporalFeedback, historyValid ? 1f : 0f, settings.TemporalDepthRejection, settings.TemporalClamp));
            cmd.SetGlobalVector(SSRParams1Id, new Vector4(0f, 0f, BurtScreenSpaceReflectionPassUtility.ResolveScreenSpaceReflectionShaderDebugMode(), 0.1f));
        }

        private Material GetScreenSpaceReflectionMaterial()
        {
            if (screenSpaceReflectionMaterial != null)
            {
                return screenSpaceReflectionMaterial;
            }

            var shader = Shader.Find(ScreenSpaceReflectionShaderName);
            if (shader == null)
            {
                if (!hasLoggedMissingShader)
                {
                    Debug.LogWarning("BurtRP could not find shader: " + ScreenSpaceReflectionShaderName);
                    hasLoggedMissingShader = true;
                }

                return null;
            }

            screenSpaceReflectionMaterial = new Material(shader);
            screenSpaceReflectionMaterial.hideFlags = HideFlags.HideAndDontSave;
            return screenSpaceReflectionMaterial;
        }
    }

    internal sealed class BurtScreenSpaceReflectionCompositePass : BurtRenderPass
    {
        private const string ScreenSpaceReflectionShaderName = BurtScreenSpaceReflectionPassUtility.ScreenSpaceReflectionShaderName;
        private const float ScreenSpaceReflectionEdgeFadeWidth = 0.04f;
        private static readonly int CameraDepthTextureId = BurtRenderGraphResourceRegistry.CameraDepthTextureId;
        private static readonly int GBuffer0Id = BurtRenderGraphResourceRegistry.GBuffer0Id;
        private static readonly int GBuffer1Id = BurtRenderGraphResourceRegistry.GBuffer1Id;
        private static readonly int GBuffer2Id = BurtRenderGraphResourceRegistry.GBuffer2Id;
        private static readonly int GBuffer3Id = BurtRenderGraphResourceRegistry.GBuffer3Id;
        private static readonly int GBuffer4Id = BurtRenderGraphResourceRegistry.GBuffer4Id;
        private static readonly int SSRTemporalColorTextureId = BurtRenderGraphResourceRegistry.ScreenSpaceReflectionTemporalColorTextureId;
        private static readonly int SSRSourceTexelSizeId = Shader.PropertyToID("_BurtSSRSourceTexelSize");
        private static readonly int SSRParams0Id = Shader.PropertyToID("_BurtSSRParams0");
        private static readonly int SSRParams1Id = Shader.PropertyToID("_BurtSSRParams1");
        private static readonly int InverseViewProjectionMatrixId = Shader.PropertyToID("_BurtDeferredInverseViewProjectionMatrix");
        private static readonly int CameraWorldPositionId = Shader.PropertyToID("_BurtDeferredCameraWorldPosition");
        private static readonly int DeferredScreenSizeId = Shader.PropertyToID("_BurtDeferredScreenSize");

        private Material screenSpaceReflectionMaterial;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Screen Space Reflections Composite";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceReflectionPassUtility.ShouldUseScreenSpaceReflections(builder.Request, builder.Asset))
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
            builder.ReadScreenSpaceReflectionTemporalColor();
            builder.WriteCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!TryGetTargets(context, out var cameraColorTarget, out var cameraDepthTarget, out var gbuffer0Target, out var gbuffer1Target, out var gbuffer2Target, out var gbuffer3Target, out var gbuffer4Target, out var ssrTemporalColorTarget))
            {
                return;
            }

            var settings = BurtScreenSpaceReflectionPassUtility.ResolveScreenSpaceReflectionSettings(context.Request, context.Asset);
            if (!settings.Enabled)
            {
                return;
            }

            var material = GetScreenSpaceReflectionMaterial();
            if (material == null)
            {
                return;
            }

            var camera = context.Request != null ? context.Request.Camera : null;
            if (camera == null)
            {
                return;
            }

            var descriptor = BurtScreenSpaceReflectionPassUtility.CreateScreenSpaceReflectionTemporalColorDescriptor(camera, settings);
            var width = Mathf.Max(1, descriptor.width);
            var height = Mathf.Max(1, descriptor.height);
            var maxMip = Mathf.Max(0, descriptor.mipCount - 1);
            var cmd = CommandBufferPool.Get(Name);
            cmd.SetRenderTarget(cameraColorTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            BindInputs(cmd, cameraDepthTarget, gbuffer0Target, gbuffer1Target, gbuffer2Target, gbuffer3Target, gbuffer4Target, ssrTemporalColorTarget);
            UploadCameraGlobals(cmd, camera, width, height);
            UploadSettings(cmd, settings, maxMip);
            cmd.SetGlobalVector(SSRSourceTexelSizeId, new Vector4(1f / width, 1f / height, width, height));
            cmd.DrawProcedural(Matrix4x4.identity, material, 2, MeshTopology.Triangles, 3, 1);
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
            out BurtRenderTargetHandle ssrTemporalColorTarget)
        {
            cameraColorTarget = context != null ? context.CameraColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraColorName);
            cameraDepthTarget = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            gbuffer0Target = context != null ? context.GBuffer0Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer0Name);
            gbuffer1Target = context != null ? context.GBuffer1Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer1Name);
            gbuffer2Target = context != null ? context.GBuffer2Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer2Name);
            gbuffer3Target = context != null ? context.GBuffer3Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer3Name);
            gbuffer4Target = context != null ? context.GBuffer4Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer4Name);
            ssrTemporalColorTarget = context != null ? context.ScreenSpaceReflectionTemporalColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceReflectionTemporalColorName);

            return cameraColorTarget.IsValid &&
                cameraDepthTarget.IsValid &&
                gbuffer0Target.IsValid &&
                gbuffer1Target.IsValid &&
                gbuffer2Target.IsValid &&
                gbuffer3Target.IsValid &&
                gbuffer4Target.IsValid &&
                ssrTemporalColorTarget.IsValid;
        }

        private static void BindInputs(
            CommandBuffer cmd,
            BurtRenderTargetHandle cameraDepthTarget,
            BurtRenderTargetHandle gbuffer0Target,
            BurtRenderTargetHandle gbuffer1Target,
            BurtRenderTargetHandle gbuffer2Target,
            BurtRenderTargetHandle gbuffer3Target,
            BurtRenderTargetHandle gbuffer4Target,
            BurtRenderTargetHandle ssrTemporalColorTarget)
        {
            cmd.SetGlobalTexture(CameraDepthTextureId, cameraDepthTarget.Identifier);
            cmd.SetGlobalTexture(GBuffer0Id, gbuffer0Target.Identifier);
            cmd.SetGlobalTexture(GBuffer1Id, gbuffer1Target.Identifier);
            cmd.SetGlobalTexture(GBuffer2Id, gbuffer2Target.Identifier);
            cmd.SetGlobalTexture(GBuffer3Id, gbuffer3Target.Identifier);
            cmd.SetGlobalTexture(GBuffer4Id, gbuffer4Target.Identifier);
            cmd.SetGlobalTexture(SSRTemporalColorTextureId, ssrTemporalColorTarget.Identifier);
        }

        private static void UploadCameraGlobals(CommandBuffer cmd, Camera camera, int width, int height)
        {
            var viewMatrix = camera.worldToCameraMatrix;
            var projectionMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
            var inverseViewProjectionMatrix = (projectionMatrix * viewMatrix).inverse;
            var cameraPosition = camera.transform.position;
            BurtScreenSpaceReflectionPassUtility.GetCameraTargetSize(camera, out var cameraWidth, out var cameraHeight);

            cmd.SetGlobalMatrix(InverseViewProjectionMatrixId, inverseViewProjectionMatrix);
            cmd.SetGlobalVector(CameraWorldPositionId, new Vector4(cameraPosition.x, cameraPosition.y, cameraPosition.z, 1f));
            cmd.SetGlobalVector(DeferredScreenSizeId, new Vector4(cameraWidth, cameraHeight, 1f / cameraWidth, 1f / cameraHeight));
        }

        private static void UploadSettings(CommandBuffer cmd, BurtScreenSpaceReflectionSettings settings, int maxMip)
        {
            cmd.SetGlobalVector(SSRParams0Id, new Vector4(settings.MaxDistance, settings.Thickness, settings.Intensity, settings.RoughnessFade));
            cmd.SetGlobalVector(SSRParams1Id, new Vector4(0f, maxMip, BurtScreenSpaceReflectionPassUtility.ResolveScreenSpaceReflectionShaderDebugMode(), ScreenSpaceReflectionEdgeFadeWidth));
        }

        private Material GetScreenSpaceReflectionMaterial()
        {
            if (screenSpaceReflectionMaterial != null)
            {
                return screenSpaceReflectionMaterial;
            }

            var shader = Shader.Find(ScreenSpaceReflectionShaderName);
            if (shader == null)
            {
                if (!hasLoggedMissingShader)
                {
                    Debug.LogWarning("BurtRP could not find shader: " + ScreenSpaceReflectionShaderName);
                    hasLoggedMissingShader = true;
                }

                return null;
            }

            screenSpaceReflectionMaterial = new Material(shader);
            screenSpaceReflectionMaterial.hideFlags = HideFlags.HideAndDontSave;
            return screenSpaceReflectionMaterial;
        }
    }

    internal sealed class BurtScreenSpaceReflectionHiZDiagnosticsPass : BurtRenderPass
    {
        private const string HiZDiagnosticsShaderName = BurtScreenSpaceReflectionPassUtility.ScreenSpaceReflectionHiZDiagnosticsShaderName;
        private static readonly int CameraDepthTextureId = BurtRenderGraphResourceRegistry.CameraDepthTextureId;
        private static readonly int GBuffer0Id = BurtRenderGraphResourceRegistry.GBuffer0Id;
        private static readonly int GBuffer1Id = BurtRenderGraphResourceRegistry.GBuffer1Id;
        private static readonly int GBuffer2Id = BurtRenderGraphResourceRegistry.GBuffer2Id;
        private static readonly int GBuffer3Id = BurtRenderGraphResourceRegistry.GBuffer3Id;
        private static readonly int GBuffer4Id = BurtRenderGraphResourceRegistry.GBuffer4Id;
        private static readonly int HiZDepthTextureId = BurtRenderGraphResourceRegistry.HiZDepthTextureId;
        private static readonly int SSRHiZDiagnosticsParamsId = Shader.PropertyToID("_BurtSSRHiZDiagnosticsParams");
        private static readonly int SSRHiZTraceParams0Id = Shader.PropertyToID("_BurtSSRHiZTraceParams0");
        private static readonly int SSRHiZViewProjectionMatrixId = Shader.PropertyToID("_BurtSSRHiZViewProjectionMatrix");
        private static readonly int InverseViewProjectionMatrixId = Shader.PropertyToID("_BurtDeferredInverseViewProjectionMatrix");
        private static readonly int CameraWorldPositionId = Shader.PropertyToID("_BurtDeferredCameraWorldPosition");
        private static readonly int DeferredScreenSizeId = Shader.PropertyToID("_BurtDeferredScreenSize");

        private Material hiZDiagnosticsMaterial;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Screen Space Reflections HiZ Diagnostics";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceReflectionPassUtility.ShouldUseScreenSpaceReflectionHiZDiagnosticView(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadCameraDepth();
            builder.ReadGBuffer0();
            builder.ReadGBuffer1();
            builder.ReadGBuffer2();
            builder.ReadGBuffer3();
            builder.ReadGBuffer4();
            builder.ReadHiZDepth();
            builder.WriteCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceReflectionPassUtility.ShouldUseScreenSpaceReflectionHiZDiagnosticView(context.Request, context.Asset))
            {
                return;
            }

            var cameraColorTarget = context.CameraColorTarget;
            var cameraDepthTarget = context.CameraDepthTarget;
            var gbuffer0Target = context.GBuffer0Target;
            var gbuffer1Target = context.GBuffer1Target;
            var gbuffer2Target = context.GBuffer2Target;
            var gbuffer3Target = context.GBuffer3Target;
            var gbuffer4Target = context.GBuffer4Target;
            var hiZDepthTarget = context.HiZDepthTarget;
            if (!cameraColorTarget.IsValid || !cameraDepthTarget.IsValid || !gbuffer0Target.IsValid || !gbuffer1Target.IsValid || !gbuffer2Target.IsValid || !gbuffer3Target.IsValid || !gbuffer4Target.IsValid || !hiZDepthTarget.IsValid)
            {
                return;
            }

            var material = GetHiZDiagnosticsMaterial();
            if (material == null)
            {
                return;
            }

            var settings = BurtScreenSpaceReflectionPassUtility.ResolveScreenSpaceReflectionSettings(context.Request, context.Asset);
            if (!settings.Enabled)
            {
                return;
            }

            var camera = context.Request != null ? context.Request.Camera : null;
            if (camera == null)
            {
                return;
            }

            var colorDescriptor = BurtRenderTargetDescriptorUtility.CreateCameraColorDescriptor(camera);
            var hiZDescriptor = BurtRenderTargetDescriptorUtility.CreateHiZDepthDescriptor(camera);
            var maxMip = Mathf.Max(0, BurtRenderTargetDescriptorUtility.CalculateMipCount(hiZDescriptor.width, hiZDescriptor.height) - 1);
            var diagnosticMip = Mathf.Clamp(maxMip >= 3 ? 3 : maxMip, 0, maxMip);
            var debugMode = BurtScreenSpaceReflectionPassUtility.ResolveScreenSpaceReflectionShaderDebugMode();
            var cmd = CommandBufferPool.Get(Name);

            cmd.SetRenderTarget(cameraColorTarget.Identifier);
            cmd.SetViewport(new Rect(0f, 0f, Mathf.Max(1, colorDescriptor.width), Mathf.Max(1, colorDescriptor.height)));
            cmd.SetGlobalTexture(CameraDepthTextureId, cameraDepthTarget.Identifier);
            cmd.SetGlobalTexture(GBuffer0Id, gbuffer0Target.Identifier);
            cmd.SetGlobalTexture(GBuffer1Id, gbuffer1Target.Identifier);
            cmd.SetGlobalTexture(GBuffer2Id, gbuffer2Target.Identifier);
            cmd.SetGlobalTexture(GBuffer3Id, gbuffer3Target.Identifier);
            cmd.SetGlobalTexture(GBuffer4Id, gbuffer4Target.Identifier);
            cmd.SetGlobalTexture(HiZDepthTextureId, hiZDepthTarget.Identifier);
            cmd.SetGlobalVector(SSRHiZDiagnosticsParamsId, new Vector4(debugMode, maxMip, diagnosticMip, 512f));
            UploadCameraGlobals(cmd, camera, colorDescriptor);
            cmd.SetGlobalVector(SSRHiZTraceParams0Id, new Vector4(settings.MaxDistance, settings.Thickness, settings.MaxSteps, settings.RoughnessFade));
            cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1);

            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private static void UploadCameraGlobals(CommandBuffer cmd, Camera camera, RenderTextureDescriptor colorDescriptor)
        {
            var viewMatrix = camera.worldToCameraMatrix;
            var projectionMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
            var viewProjectionMatrix = projectionMatrix * viewMatrix;
            var inverseViewProjectionMatrix = viewProjectionMatrix.inverse;
            var cameraPosition = camera.transform.position;
            var width = Mathf.Max(1, colorDescriptor.width);
            var height = Mathf.Max(1, colorDescriptor.height);

            cmd.SetGlobalMatrix(SSRHiZViewProjectionMatrixId, viewProjectionMatrix);
            cmd.SetGlobalMatrix(InverseViewProjectionMatrixId, inverseViewProjectionMatrix);
            cmd.SetGlobalVector(CameraWorldPositionId, new Vector4(cameraPosition.x, cameraPosition.y, cameraPosition.z, 1f));
            cmd.SetGlobalVector(DeferredScreenSizeId, new Vector4(width, height, 1f / width, 1f / height));
        }

        private Material GetHiZDiagnosticsMaterial()
        {
            if (hiZDiagnosticsMaterial != null)
            {
                return hiZDiagnosticsMaterial;
            }

            var shader = Shader.Find(HiZDiagnosticsShaderName);
            if (shader == null)
            {
                if (!hasLoggedMissingShader)
                {
                    Debug.LogWarning("BurtRP could not find shader: " + HiZDiagnosticsShaderName);
                    hasLoggedMissingShader = true;
                }

                return null;
            }

            hiZDiagnosticsMaterial = new Material(shader);
            hiZDiagnosticsMaterial.hideFlags = HideFlags.HideAndDontSave;
            return hiZDiagnosticsMaterial;
        }
    }

    internal sealed class BurtReleaseScreenSpaceReflectionColorPass : BurtRenderPass
    {
        public override string Name => "Burt Release Screen Space Reflection Color";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceReflectionPassUtility.ShouldUseScreenSpaceReflections(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceReflectionColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceReflectionPassUtility.ShouldUseScreenSpaceReflections(context.Request, context.Asset))
            {
                return;
            }

            var target = context.ScreenSpaceReflectionColorTarget;
            if (!target.IsValid)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            cmd.ReleaseTemporaryRT(BurtRenderGraphResourceRegistry.ScreenSpaceReflectionColorTextureId);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    internal sealed class BurtReleaseScreenSpaceReflectionDenoisedColorPass : BurtRenderPass
    {
        public override string Name => "Burt Release Screen Space Reflection Denoised Color";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceReflectionPassUtility.ShouldUseScreenSpaceReflections(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceReflectionDenoisedColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceReflectionPassUtility.ShouldUseScreenSpaceReflections(context.Request, context.Asset))
            {
                return;
            }

            var target = context.ScreenSpaceReflectionDenoisedColorTarget;
            if (!target.IsValid)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            cmd.ReleaseTemporaryRT(BurtRenderGraphResourceRegistry.ScreenSpaceReflectionDenoisedColorTextureId);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    internal sealed class BurtReleaseScreenSpaceReflectionTemporalColorPass : BurtRenderPass
    {
        public override string Name => "Burt Release Screen Space Reflection Temporal Color";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceReflectionPassUtility.ShouldUseScreenSpaceReflections(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceReflectionTemporalColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceReflectionPassUtility.ShouldUseScreenSpaceReflections(context.Request, context.Asset))
            {
                return;
            }

            var target = context.ScreenSpaceReflectionTemporalColorTarget;
            if (!target.IsValid)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            cmd.ReleaseTemporaryRT(BurtRenderGraphResourceRegistry.ScreenSpaceReflectionTemporalColorTextureId);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    internal readonly struct BurtScreenSpaceReflectionSettings
    {
        public static readonly BurtScreenSpaceReflectionSettings Disabled = new BurtScreenSpaceReflectionSettings(
            false,
            BurtScreenSpaceReflectionQuality.Custom,
            BurtScreenSpaceReflectionResolution.Full,
            48,
            30f,
            0.35f,
            1f,
            0.6f,
            false,
            false,
            0.86f,
            0.02f,
            1f);

        public BurtScreenSpaceReflectionSettings(
            bool enabled,
            BurtScreenSpaceReflectionQuality quality,
            BurtScreenSpaceReflectionResolution resolution,
            int maxSteps,
            float maxDistance,
            float thickness,
            float intensity,
            float roughnessFade,
            bool experimentalHiZTrace,
            bool temporalAccumulation,
            float temporalFeedback,
            float temporalDepthRejection,
            float temporalClamp)
        {
            Enabled = enabled;
            Quality = NormalizeQuality(quality);
            Resolution = NormalizeResolution(resolution);
            MaxSteps = Mathf.Clamp(maxSteps, 1, 512);
            MaxDistance = Mathf.Max(0.01f, maxDistance);
            Thickness = Mathf.Max(0.0001f, thickness);
            Intensity = Mathf.Clamp01(intensity);
            RoughnessFade = Mathf.Clamp01(roughnessFade);
            ExperimentalHiZTrace = experimentalHiZTrace;
            TemporalAccumulation = temporalAccumulation;
            TemporalFeedback = Mathf.Clamp(temporalFeedback, 0f, 0.98f);
            TemporalDepthRejection = Mathf.Clamp(temporalDepthRejection, 0.001f, 0.2f);
            TemporalClamp = Mathf.Clamp(temporalClamp, 0.25f, 4f);
        }

        public bool Enabled { get; }

        public BurtScreenSpaceReflectionQuality Quality { get; }

        public BurtScreenSpaceReflectionResolution Resolution { get; }

        public int MaxSteps { get; }

        public float MaxDistance { get; }

        public float Thickness { get; }

        public float Intensity { get; }

        public float RoughnessFade { get; }

        public bool ExperimentalHiZTrace { get; }

        public bool TemporalAccumulation { get; }

        public float TemporalFeedback { get; }

        public float TemporalDepthRejection { get; }

        public float TemporalClamp { get; }

        private static BurtScreenSpaceReflectionQuality NormalizeQuality(BurtScreenSpaceReflectionQuality quality)
        {
            switch (quality)
            {
                case BurtScreenSpaceReflectionQuality.Low:
                case BurtScreenSpaceReflectionQuality.Medium:
                case BurtScreenSpaceReflectionQuality.High:
                    return quality;
                default:
                    return BurtScreenSpaceReflectionQuality.Custom;
            }
        }

        private static BurtScreenSpaceReflectionResolution NormalizeResolution(BurtScreenSpaceReflectionResolution resolution)
        {
            return resolution == BurtScreenSpaceReflectionResolution.Half ? BurtScreenSpaceReflectionResolution.Half : BurtScreenSpaceReflectionResolution.Full;
        }
    }

    internal readonly struct BurtScreenSpaceReflectionHistoryMatrices
    {
        public Matrix4x4 ViewMatrix { get; }
        public Matrix4x4 ViewProjectionMatrix { get; }
        public Matrix4x4 InverseViewProjectionMatrix { get; }
        public Matrix4x4 NonJitteredProjectionMatrix { get; }

        public BurtScreenSpaceReflectionHistoryMatrices(
            Matrix4x4 viewMatrix,
            Matrix4x4 viewProjectionMatrix,
            Matrix4x4 inverseViewProjectionMatrix,
            Matrix4x4 nonJitteredProjectionMatrix)
        {
            ViewMatrix = viewMatrix;
            ViewProjectionMatrix = viewProjectionMatrix;
            InverseViewProjectionMatrix = inverseViewProjectionMatrix;
            NonJitteredProjectionMatrix = nonJitteredProjectionMatrix;
        }
    }

    internal readonly struct BurtScreenSpaceReflectionHistoryTextures
    {
        public RenderTexture Color { get; }
        public RenderTexture Depth { get; }
        public RenderTexture NormalRoughness { get; }
        public RenderTexture Moment { get; }
        public Matrix4x4 PreviousViewMatrix { get; }
        public Matrix4x4 PreviousViewProjectionMatrix { get; }
        public Matrix4x4 CurrentInverseViewProjectionMatrix { get; }

        public BurtScreenSpaceReflectionHistoryTextures(
            RenderTexture color,
            RenderTexture depth,
            RenderTexture normalRoughness,
            RenderTexture moment,
            Matrix4x4 previousViewMatrix,
            Matrix4x4 previousViewProjectionMatrix,
            Matrix4x4 currentInverseViewProjectionMatrix)
        {
            Color = color;
            Depth = depth;
            NormalRoughness = normalRoughness;
            Moment = moment;
            PreviousViewMatrix = previousViewMatrix;
            PreviousViewProjectionMatrix = previousViewProjectionMatrix;
            CurrentInverseViewProjectionMatrix = currentInverseViewProjectionMatrix;
        }

        public static BurtScreenSpaceReflectionHistoryTextures CreateInvalid(BurtScreenSpaceReflectionHistoryMatrices matrices)
        {
            return new BurtScreenSpaceReflectionHistoryTextures(null, null, null, null, matrices.ViewMatrix, matrices.ViewProjectionMatrix, matrices.InverseViewProjectionMatrix);
        }
    }

    internal readonly struct BurtScreenSpaceReflectionHistoryStatus
    {
        public bool HasHistory { get; }
        public bool DescriptorMatches { get; }
        public bool HasDepthHistory { get; }
        public bool DepthDescriptorMatches { get; }
        public bool HasNormalRoughnessHistory { get; }
        public bool NormalRoughnessDescriptorMatches { get; }
        public bool HasMomentHistory { get; }
        public bool MomentDescriptorMatches { get; }
        public int Width { get; }
        public int Height { get; }
        public RenderTextureFormat Format { get; }
        public int FrameIndex { get; }
        public int HistoryAge { get; }
        public string LastInvalidationReason { get; }

        public BurtScreenSpaceReflectionHistoryStatus(
            bool hasHistory,
            bool descriptorMatches,
            bool hasDepthHistory,
            bool depthDescriptorMatches,
            bool hasNormalRoughnessHistory,
            bool normalRoughnessDescriptorMatches,
            bool hasMomentHistory,
            bool momentDescriptorMatches,
            int width,
            int height,
            RenderTextureFormat format,
            int frameIndex,
            int historyAge,
            string lastInvalidationReason)
        {
            HasHistory = hasHistory;
            DescriptorMatches = descriptorMatches;
            HasDepthHistory = hasDepthHistory;
            DepthDescriptorMatches = depthDescriptorMatches;
            HasNormalRoughnessHistory = hasNormalRoughnessHistory;
            NormalRoughnessDescriptorMatches = normalRoughnessDescriptorMatches;
            HasMomentHistory = hasMomentHistory;
            MomentDescriptorMatches = momentDescriptorMatches;
            Width = width;
            Height = height;
            Format = format;
            FrameIndex = frameIndex;
            HistoryAge = historyAge;
            LastInvalidationReason = lastInvalidationReason;
        }
    }

    internal static class BurtScreenSpaceReflectionHistoryUtility
    {
        private const int HistoryAlgorithmVersion = 41;
        private const int CameraStatePruneInterval = 128;
        private const float ProjectionChangeEpsilon = 0.0001f;

        private sealed class CameraState
        {
            public Camera Camera;
            public RenderTexture ColorHistory;
            public RenderTexture DepthHistory;
            public RenderTexture NormalRoughnessHistory;
            public RenderTexture MomentHistory;
            public RenderTextureDescriptor ColorDescriptor;
            public RenderTextureDescriptor DepthDescriptor;
            public RenderTextureDescriptor NormalRoughnessDescriptor;
            public RenderTextureDescriptor MomentDescriptor;
            public int AlgorithmVersion;
            public Vector4 CurrentSettingsSignature0;
            public Vector4 PreviousSettingsSignature0;
            public Vector4 CurrentSettingsSignature1;
            public Vector4 PreviousSettingsSignature1;
            public Vector4 CurrentSettingsSignature2;
            public Vector4 PreviousSettingsSignature2;
            public bool HasValidHistory;
            public bool HasPreviousCameraState;
            public int FrameIndex;
            public int FirstValidFrameIndex;
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
            public Matrix4x4 CurrentViewMatrix = Matrix4x4.identity;
            public Matrix4x4 CurrentViewProjectionMatrix = Matrix4x4.identity;
            public Matrix4x4 CurrentInverseViewProjectionMatrix = Matrix4x4.identity;
            public Matrix4x4 CurrentNonJitteredProjectionMatrix = Matrix4x4.identity;
            public Matrix4x4 PreviousViewMatrix = Matrix4x4.identity;
            public Matrix4x4 PreviousViewProjectionMatrix = Matrix4x4.identity;
            public Matrix4x4 PreviousNonJitteredProjectionMatrix = Matrix4x4.identity;
            public string LastInvalidationReason = "NeverAllocated";
        }

        private static readonly Dictionary<int, CameraState> CameraStates = new Dictionary<int, CameraState>();
        private static readonly List<int> CameraStateRemovalKeys = new List<int>();
        private static int cameraStatePruneCounter;

        public static BurtScreenSpaceReflectionHistoryTextures EnsureHistoryTextures(
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset,
            BurtScreenSpaceReflectionSettings settings,
            out bool historyValid)
        {
            historyValid = false;
            var camera = request != null ? request.Camera : null;
            var matrices = CreateCurrentMatrices(request);
            if (camera == null)
            {
                return BurtScreenSpaceReflectionHistoryTextures.CreateInvalid(matrices);
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
            var depthDescriptor = CreateDepthHistoryDescriptor(camera, settings);
            var normalRoughnessDescriptor = CreateNormalRoughnessHistoryDescriptor(camera, settings);
            var momentDescriptor = CreateMomentHistoryDescriptor(camera, settings);
            var colorMatches = state.ColorHistory != null && Matches(state.ColorDescriptor, colorDescriptor);
            var depthMatches = state.DepthHistory != null && Matches(state.DepthDescriptor, depthDescriptor);
            var normalRoughnessMatches = state.NormalRoughnessHistory != null && Matches(state.NormalRoughnessDescriptor, normalRoughnessDescriptor);
            var momentMatches = state.MomentHistory != null && Matches(state.MomentDescriptor, momentDescriptor);
            var descriptorsMatch = colorMatches && depthMatches && normalRoughnessMatches && momentMatches;
            var rendererMode = asset != null ? asset.RendererMode : BurtRendererMode.Forward;
            GetTargetSize(camera, out var targetWidth, out var targetHeight);
            var settingsSignature0 = CreateSettingsSignature0(settings);
            var settingsSignature1 = CreateSettingsSignature1(settings);
            var settingsSignature2 = CreateSettingsSignature2(settings);
            var invalidationReason = ResolveHistoryInvalidationReason(camera, state, rendererMode, matrices.NonJitteredProjectionMatrix, targetWidth, targetHeight, descriptorsMatch, settingsSignature0, settingsSignature1, settingsSignature2);

            if (!descriptorsMatch)
            {
                ReleaseHistory(state);
            }

            if (state.ColorHistory == null)
            {
                state.ColorDescriptor = colorDescriptor;
                state.ColorHistory = CreateHistoryTexture(colorDescriptor, "Burt SSR Color History " + camera.GetInstanceID(), FilterMode.Bilinear);
                SetAllocationInvalidationReason(state, "HistoryAllocated");
            }

            if (state.DepthHistory == null)
            {
                state.DepthDescriptor = depthDescriptor;
                state.DepthHistory = CreateHistoryTexture(depthDescriptor, "Burt SSR Depth History " + camera.GetInstanceID(), FilterMode.Point);
                SetAllocationInvalidationReason(state, "DepthHistoryAllocated");
            }

            if (state.NormalRoughnessHistory == null)
            {
                state.NormalRoughnessDescriptor = normalRoughnessDescriptor;
                state.NormalRoughnessHistory = CreateHistoryTexture(normalRoughnessDescriptor, "Burt SSR Normal Roughness History " + camera.GetInstanceID(), FilterMode.Point);
                SetAllocationInvalidationReason(state, "NormalRoughnessHistoryAllocated");
            }

            if (state.MomentHistory == null)
            {
                state.MomentDescriptor = momentDescriptor;
                state.MomentHistory = CreateHistoryTexture(momentDescriptor, "Burt SSR Moment History " + camera.GetInstanceID(), FilterMode.Point);
                SetAllocationInvalidationReason(state, "MomentHistoryAllocated");
            }

            if (!string.IsNullOrEmpty(invalidationReason))
            {
                InvalidateState(state, invalidationReason);
            }

            state.FrameIndex++;
            state.CurrentRendererMode = rendererMode;
            state.CurrentViewMatrix = matrices.ViewMatrix;
            state.CurrentViewProjectionMatrix = matrices.ViewProjectionMatrix;
            state.CurrentInverseViewProjectionMatrix = matrices.InverseViewProjectionMatrix;
            state.CurrentNonJitteredProjectionMatrix = matrices.NonJitteredProjectionMatrix;
            state.CurrentSettingsSignature0 = settingsSignature0;
            state.CurrentSettingsSignature1 = settingsSignature1;
            state.CurrentSettingsSignature2 = settingsSignature2;
            CaptureCurrentCameraState(camera, state, targetWidth, targetHeight);

            historyValid = state.HasValidHistory && state.HasPreviousCameraState && state.ColorHistory != null && state.DepthHistory != null && state.NormalRoughnessHistory != null && state.MomentHistory != null;
            var previousViewMatrix = state.HasPreviousCameraState ? state.PreviousViewMatrix : matrices.ViewMatrix;
            var previousViewProjectionMatrix = state.HasPreviousCameraState ? state.PreviousViewProjectionMatrix : matrices.ViewProjectionMatrix;
            return new BurtScreenSpaceReflectionHistoryTextures(
                state.ColorHistory,
                state.DepthHistory,
                state.NormalRoughnessHistory,
                state.MomentHistory,
                previousViewMatrix,
                previousViewProjectionMatrix,
                matrices.InverseViewProjectionMatrix);
        }

        public static BurtScreenSpaceReflectionHistoryMatrices CreateCurrentMatrices(BurtRenderRequest request)
        {
            var camera = request != null ? request.Camera : null;
            if (camera == null)
            {
                return new BurtScreenSpaceReflectionHistoryMatrices(Matrix4x4.identity, Matrix4x4.identity, Matrix4x4.identity, Matrix4x4.identity);
            }

            var temporalAA = request.TemporalAA;
            var viewMatrix = temporalAA != null ? temporalAA.ViewMatrix : camera.worldToCameraMatrix;
            var nonJitteredProjectionMatrix = temporalAA != null ? temporalAA.NonJitteredProjectionMatrix : camera.projectionMatrix;
            var viewProjectionMatrix = temporalAA != null ? temporalAA.CurrentViewProjectionMatrix : GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * viewMatrix;
            var inverseViewProjectionMatrix = temporalAA != null ? temporalAA.InverseCurrentViewProjectionMatrix : viewProjectionMatrix.inverse;
            return new BurtScreenSpaceReflectionHistoryMatrices(viewMatrix, viewProjectionMatrix, inverseViewProjectionMatrix, nonJitteredProjectionMatrix);
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
            state.PreviousViewMatrix = state.CurrentViewMatrix;
            state.PreviousViewProjectionMatrix = state.CurrentViewProjectionMatrix;
            state.PreviousNonJitteredProjectionMatrix = state.CurrentNonJitteredProjectionMatrix;
            state.PreviousSettingsSignature0 = state.CurrentSettingsSignature0;
            state.PreviousSettingsSignature1 = state.CurrentSettingsSignature1;
            state.PreviousSettingsSignature2 = state.CurrentSettingsSignature2;
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

        public static BurtScreenSpaceReflectionHistoryStatus GetHistoryStatus(Camera camera)
        {
            if (camera == null || !CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
            {
                return new BurtScreenSpaceReflectionHistoryStatus(false, false, false, false, false, false, false, false, 0, 0, RenderTextureFormat.Default, 0, 0, "NoCameraOrHistory");
            }

            var settings = BurtScreenSpaceReflectionPassUtility.ResolveScreenSpaceReflectionHistorySettings(camera);
            var colorDescriptor = CreateColorHistoryDescriptor(camera, settings);
            var depthDescriptor = CreateDepthHistoryDescriptor(camera, settings);
            var normalRoughnessDescriptor = CreateNormalRoughnessHistoryDescriptor(camera, settings);
            var momentDescriptor = CreateMomentHistoryDescriptor(camera, settings);
            var hasColor = state.ColorHistory != null;
            var hasDepth = state.DepthHistory != null;
            var hasNormalRoughness = state.NormalRoughnessHistory != null;
            var hasMoment = state.MomentHistory != null;
            var historyAge = state.HasValidHistory && state.FirstValidFrameIndex > 0 ? Mathf.Max(0, state.FrameIndex - state.FirstValidFrameIndex + 1) : 0;
            return new BurtScreenSpaceReflectionHistoryStatus(
                state.HasValidHistory && hasColor && hasDepth && hasNormalRoughness && hasMoment,
                hasColor && Matches(state.ColorDescriptor, colorDescriptor),
                hasDepth,
                hasDepth && Matches(state.DepthDescriptor, depthDescriptor),
                hasNormalRoughness,
                hasNormalRoughness && Matches(state.NormalRoughnessDescriptor, normalRoughnessDescriptor),
                hasMoment,
                hasMoment && Matches(state.MomentDescriptor, momentDescriptor),
                hasColor ? state.ColorHistory.width : 0,
                hasColor ? state.ColorHistory.height : 0,
                hasColor ? state.ColorHistory.format : RenderTextureFormat.Default,
                state.FrameIndex,
                historyAge,
                state.LastInvalidationReason);
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

        private static RenderTextureDescriptor CreateColorHistoryDescriptor(Camera camera, BurtScreenSpaceReflectionSettings settings)
        {
            return BurtScreenSpaceReflectionPassUtility.CreateScreenSpaceReflectionColorDescriptor(camera, settings);
        }

        private static RenderTextureDescriptor CreateDepthHistoryDescriptor(Camera camera, BurtScreenSpaceReflectionSettings settings)
        {
            var descriptor = BurtScreenSpaceReflectionPassUtility.CreateScreenSpaceReflectionTemporalColorDescriptor(camera, settings);
            descriptor.colorFormat = RenderTextureFormat.RFloat;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.useMipMap = false;
            descriptor.mipCount = 1;
            descriptor.autoGenerateMips = false;
            descriptor.sRGB = false;
            return descriptor;
        }

        private static RenderTextureDescriptor CreateNormalRoughnessHistoryDescriptor(Camera camera, BurtScreenSpaceReflectionSettings settings)
        {
            var descriptor = BurtScreenSpaceReflectionPassUtility.CreateScreenSpaceReflectionTemporalColorDescriptor(camera, settings);
            descriptor.colorFormat = RenderTextureFormat.ARGBHalf;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.useMipMap = false;
            descriptor.mipCount = 1;
            descriptor.autoGenerateMips = false;
            descriptor.sRGB = false;
            return descriptor;
        }

        public static RenderTextureDescriptor CreateMomentHistoryDescriptor(Camera camera, BurtScreenSpaceReflectionSettings settings)
        {
            var descriptor = BurtScreenSpaceReflectionPassUtility.CreateScreenSpaceReflectionTemporalColorDescriptor(camera, settings);
            descriptor.colorFormat = RenderTextureFormat.ARGBHalf;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.useMipMap = false;
            descriptor.mipCount = 1;
            descriptor.autoGenerateMips = false;
            descriptor.sRGB = false;
            return descriptor;
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
            bool descriptorsMatch,
            Vector4 settingsSignature0,
            Vector4 settingsSignature1,
            Vector4 settingsSignature2)
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

            if (VectorChanged(settingsSignature0, state.PreviousSettingsSignature0, ProjectionChangeEpsilon) ||
                VectorChanged(settingsSignature1, state.PreviousSettingsSignature1, ProjectionChangeEpsilon) ||
                VectorChanged(settingsSignature2, state.PreviousSettingsSignature2, ProjectionChangeEpsilon))
            {
                return "SettingsChanged";
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
                state.LastInvalidationReason == "DepthHistoryAllocated" ||
                state.LastInvalidationReason == "NormalRoughnessHistoryAllocated" ||
                state.LastInvalidationReason == "MomentHistoryAllocated")
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
            BurtScreenSpaceReflectionPassUtility.GetCameraTargetSize(camera, out width, out height);
        }

        private static bool FloatChanged(float current, float previous, float epsilon)
        {
            return Mathf.Abs(current - previous) > epsilon;
        }

        private static bool VectorChanged(Vector4 current, Vector4 previous, float epsilon)
        {
            return FloatChanged(current.x, previous.x, epsilon) ||
                FloatChanged(current.y, previous.y, epsilon) ||
                FloatChanged(current.z, previous.z, epsilon) ||
                FloatChanged(current.w, previous.w, epsilon);
        }

        private static Vector4 CreateSettingsSignature0(BurtScreenSpaceReflectionSettings settings)
        {
            return new Vector4(
                (float)settings.Quality,
                (float)settings.Resolution,
                settings.MaxSteps,
                settings.MaxDistance);
        }

        private static Vector4 CreateSettingsSignature1(BurtScreenSpaceReflectionSettings settings)
        {
            return new Vector4(
                settings.Thickness,
                settings.RoughnessFade,
                settings.TemporalFeedback,
                settings.TemporalDepthRejection);
        }

        private static Vector4 CreateSettingsSignature2(BurtScreenSpaceReflectionSettings settings)
        {
            return new Vector4(
                settings.Intensity,
                settings.TemporalClamp,
                settings.TemporalAccumulation ? 1f : 0f,
                settings.ExperimentalHiZTrace ? 1f : 0f);
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
            ReleaseTexture(state.NormalRoughnessHistory);
            ReleaseTexture(state.MomentHistory);
            state.ColorHistory = null;
            state.DepthHistory = null;
            state.NormalRoughnessHistory = null;
            state.MomentHistory = null;
            state.HasValidHistory = false;
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

    internal static class BurtScreenSpaceReflectionPassUtility
    {
        public const string ScreenSpaceReflectionShaderName = "Hidden/BurtRP/ScreenSpaceReflections";
        public const string ScreenSpaceReflectionHiZDiagnosticsShaderName = "Hidden/BurtRP/ScreenSpaceReflectionHiZDiagnostics";
        private static readonly bool EnableScreenSpaceReflectionHiZDiagnostics = true;
        private static int shaderAvailabilityFrame = -1;
        private static bool shaderAvailable;
        private static int hiZDiagnosticsShaderAvailabilityFrame = -1;
        private static bool hiZDiagnosticsShaderAvailable;

        public static RenderTextureDescriptor CreateScreenSpaceReflectionColorDescriptor(Camera camera, BurtScreenSpaceReflectionSettings settings)
        {
            var descriptor = BurtRenderTargetDescriptorUtility.CreateScreenSpaceReflectionColorDescriptor(camera);
            ApplyScreenSpaceReflectionResolution(ref descriptor, settings);
            return descriptor;
        }

        public static RenderTextureDescriptor CreateScreenSpaceReflectionDenoisedColorDescriptor(Camera camera, BurtScreenSpaceReflectionSettings settings)
        {
            return CreateScreenSpaceReflectionColorDescriptor(camera, settings);
        }

        public static RenderTextureDescriptor CreateScreenSpaceReflectionTemporalColorDescriptor(Camera camera, BurtScreenSpaceReflectionSettings settings)
        {
            var descriptor = BurtRenderTargetDescriptorUtility.CreateScreenSpaceReflectionTemporalColorDescriptor(camera);
            ApplyScreenSpaceReflectionResolution(ref descriptor, settings);
            if (descriptor.useMipMap)
            {
                descriptor.mipCount = BurtRenderTargetDescriptorUtility.CalculateMipCount(descriptor.width, descriptor.height);
            }

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

            width = Mathf.Max(1, camera != null ? camera.pixelWidth : 1);
            height = Mathf.Max(1, camera != null ? camera.pixelHeight : 1);
        }

        public static BurtScreenSpaceReflectionSettings ResolveScreenSpaceReflectionHistorySettings(Camera camera)
        {
            var component = GetScreenSpaceReflectionVolumeComponent();
            if (component != null)
            {
                return CreateScreenSpaceReflectionSettings(component);
            }

            return new BurtScreenSpaceReflectionSettings(
                true,
                BurtScreenSpaceReflectionQuality.Custom,
                BurtScreenSpaceReflectionResolution.Full,
                48,
                30f,
                0.35f,
                1f,
                0.6f,
                false,
                true,
                0.86f,
                0.02f,
                1f);
        }

        private static void ApplyScreenSpaceReflectionResolution(ref RenderTextureDescriptor descriptor, BurtScreenSpaceReflectionSettings settings)
        {
            if (settings.Resolution != BurtScreenSpaceReflectionResolution.Half)
            {
                return;
            }

            descriptor.width = Mathf.Max(1, (descriptor.width + 1) / 2);
            descriptor.height = Mathf.Max(1, (descriptor.height + 1) / 2);
        }

        public static bool IsScreenSpaceReflectionShaderAvailable()
        {
            var frame = Time.frameCount;
            if (shaderAvailabilityFrame == frame)
            {
                return shaderAvailable;
            }

            shaderAvailabilityFrame = frame;
            shaderAvailable = Shader.Find(ScreenSpaceReflectionShaderName) != null;
            return shaderAvailable;
        }

        public static string ResolveScreenSpaceReflectionShaderStatusLabel()
        {
            return IsScreenSpaceReflectionShaderAvailable() ? "Ready" : "Missing(" + ScreenSpaceReflectionShaderName + ")";
        }

        public static bool IsScreenSpaceReflectionHiZDiagnosticsShaderAvailable()
        {
            var frame = Time.frameCount;
            if (hiZDiagnosticsShaderAvailabilityFrame == frame)
            {
                return hiZDiagnosticsShaderAvailable;
            }

            hiZDiagnosticsShaderAvailabilityFrame = frame;
            hiZDiagnosticsShaderAvailable = Shader.Find(ScreenSpaceReflectionHiZDiagnosticsShaderName) != null;
            return hiZDiagnosticsShaderAvailable;
        }

        public static bool ShouldUseScreenSpaceReflectionHiZDiagnostics()
        {
            return EnableScreenSpaceReflectionHiZDiagnostics &&
                BurtShadingDebugSettings.IsDebugging &&
                IsScreenSpaceReflectionHiZDiagnosticMode(BurtShadingDebugSettings.Mode) &&
                IsScreenSpaceReflectionHiZDiagnosticsShaderAvailable();
        }

        public static bool ShouldUseScreenSpaceReflectionHiZTrace(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            var settings = ResolveScreenSpaceReflectionSettings(request, asset);
            return settings.Enabled && (settings.ExperimentalHiZTrace || IsScreenSpaceReflectionHiZStepSavedDebugMode(BurtShadingDebugSettings.Mode));
        }

        public static bool ShouldUseScreenSpaceReflectionHiZDiagnosticView(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return ShouldUseScreenSpaceReflections(request, asset) &&
                ShouldUseScreenSpaceReflectionHiZDiagnostics();
        }

        public static string ResolveScreenSpaceReflectionHiZDiagnosticsStatusLabel()
        {
            if (!BurtShadingDebugSettings.IsDebugging ||
                !IsScreenSpaceReflectionHiZDiagnosticMode(BurtShadingDebugSettings.Mode))
            {
                return "Inactive";
            }

            if (!EnableScreenSpaceReflectionHiZDiagnostics)
            {
                return "Stubbed";
            }

            return IsScreenSpaceReflectionHiZDiagnosticsShaderAvailable() ? "IsolatedTraceCompare" : "Missing(" + ScreenSpaceReflectionHiZDiagnosticsShaderName + ")";
        }

        private static bool IsScreenSpaceReflectionHiZDiagnosticMode(BurtShadingDebugMode mode)
        {
            return mode == BurtShadingDebugMode.ScreenSpaceReflectionHiZSkipCandidate ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionHiZMipLevel ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionHiZDivergence ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionHiZMissedHits ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionHiZRawHitMiss ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionHiZResolvedHitMiss ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionHiZVisibilityMiss ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionHiZSkipUsed ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionHiZProbeBlocked ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionHiZStepCompare ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionHiZWorkCompare;
        }

        public static bool IsScreenSpaceReflectionHiZStepSavedDebugMode(BurtShadingDebugMode mode)
        {
            return BurtShadingDebugSettings.IsDebugging &&
                mode == BurtShadingDebugMode.ScreenSpaceReflectionHiZStepSaved;
        }

        public static string ResolveScreenSpaceReflectionTraceModeLabel(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            var hiZTrace = ShouldUseScreenSpaceReflectionHiZTrace(request, asset);
            var hiZDiagnostics = ShouldUseScreenSpaceReflectionHiZDiagnostics();
            var hiZStepSavedDebug = IsScreenSpaceReflectionHiZStepSavedDebugMode(BurtShadingDebugSettings.Mode);

            if (hiZTrace && hiZDiagnostics)
            {
                return "HiZExperimentalGuarded+HiZDiagnostics";
            }

            if (hiZDiagnostics)
            {
                return "StableMip0+HiZDiagnostics";
            }

            if (BurtShadingDebugSettings.IsDebugging &&
                IsScreenSpaceReflectionHiZDiagnosticMode(BurtShadingDebugSettings.Mode))
            {
                if (!EnableScreenSpaceReflectionHiZDiagnostics)
                {
                    return "StableMip0(HiZDiagnosticsStubbed)";
                }

                return IsScreenSpaceReflectionHiZDiagnosticsShaderAvailable() ? "StableMip0+HiZDiagnostics" : "StableMip0(HiZDiagnosticsMissing)";
            }

            if (hiZStepSavedDebug && hiZTrace)
            {
                return "StableMip0+HiZStepSavedDebug";
            }

            return hiZTrace ? "HiZExperimentalGuarded" : "StableMip0";
        }

        public static bool IsScreenSpaceReflectionSuppressedByShadingDebug()
        {
            return BurtShadingDebugSettings.IsDebugging && !IsScreenSpaceReflectionDebugMode(BurtShadingDebugSettings.Mode);
        }

        public static bool IsScreenSpaceReflectionDebugMode(BurtShadingDebugMode mode)
        {
            return mode == BurtShadingDebugMode.ScreenSpaceReflectionRawHitMask ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionHitMask ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionHitUV ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionStepCount ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionColor ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionConfidence ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionDepthDelta ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionWorldError ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionDenoisedColor ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionTemporalColor ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionResolveAlpha ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionVisibilityAlpha ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionMaterialWeight ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionRoughnessMip ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionResolvedColor ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionDepthQuality ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionWorldQuality ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionResolveQuality ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionSurfaceSupport ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionHiZSkipCandidate ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionHiZMipLevel ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionHiZDivergence ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionHiZMissedHits ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionHiZRawHitMiss ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionHiZResolvedHitMiss ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionHiZVisibilityMiss ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionHiZSkipUsed ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionHiZProbeBlocked ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionHiZStepCompare ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionHiZWorkCompare ||
                mode == BurtShadingDebugMode.ScreenSpaceReflectionHiZStepSaved;
        }

        public static bool ShouldUseScreenSpaceReflections(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return ResolveScreenSpaceReflectionSettings(request, asset).Enabled;
        }

        public static BurtScreenSpaceReflectionSettings ResolveScreenSpaceReflectionSettings(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            if (request == null || !request.IsValid || request.Camera == null)
            {
                return BurtScreenSpaceReflectionSettings.Disabled;
            }

            if (request.Type == BurtRenderRequestType.Preview || request.Type == BurtRenderRequestType.Reflection)
            {
                return DisableAndInvalidateHistory(request, "PreviewOrReflection");
            }

            if (IsScreenSpaceReflectionSuppressedByShadingDebug())
            {
                return DisableAndInvalidateHistory(request, "ShadingDebug");
            }

            if (asset == null || asset.RendererMode != BurtRendererMode.Deferred)
            {
                return DisableAndInvalidateHistory(request, "RendererNotDeferred");
            }

            var screenSpaceReflections = GetScreenSpaceReflectionVolumeComponent();
            if (screenSpaceReflections == null || !screenSpaceReflections.IsEnabled())
            {
                return DisableAndInvalidateHistory(request, "SSRDisabled");
            }

            if (!IsScreenSpaceReflectionShaderAvailable())
            {
                return DisableAndInvalidateHistory(request, "ShaderMissing");
            }

            return CreateScreenSpaceReflectionSettings(screenSpaceReflections);
        }

        private static BurtScreenSpaceReflectionSettings CreateScreenSpaceReflectionSettings(BurtScreenSpaceReflectionVolumeComponent screenSpaceReflections)
        {
            var quality = screenSpaceReflections.quality.value;
            var maxSteps = screenSpaceReflections.maxSteps.value;
            var maxDistance = screenSpaceReflections.maxDistance.value;
            var thickness = screenSpaceReflections.thickness.value;
            var roughnessFade = screenSpaceReflections.roughnessFade.value;
            var temporalFeedback = screenSpaceReflections.temporalFeedback.value;
            var temporalDepthRejection = screenSpaceReflections.temporalDepthRejection.value;
            var temporalClamp = screenSpaceReflections.temporalClamp.value;
            var experimentalHiZTrace = screenSpaceReflections.experimentalHiZTrace.value;
            var temporalAccumulation = screenSpaceReflections.temporalAccumulation.value;
            var resolution = screenSpaceReflections.resolution.value;
            ApplyScreenSpaceReflectionQualityPreset(
                quality,
                ref resolution,
                ref maxSteps,
                ref maxDistance,
                ref thickness,
                ref roughnessFade,
                ref temporalFeedback,
                ref temporalDepthRejection,
                ref temporalClamp,
                ref temporalAccumulation);

            return new BurtScreenSpaceReflectionSettings(
                true,
                quality,
                resolution,
                maxSteps,
                maxDistance,
                thickness,
                screenSpaceReflections.intensity.value,
                roughnessFade,
                experimentalHiZTrace,
                temporalAccumulation,
                temporalFeedback,
                temporalDepthRejection,
                temporalClamp);
        }

        private static void ApplyScreenSpaceReflectionQualityPreset(
            BurtScreenSpaceReflectionQuality quality,
            ref BurtScreenSpaceReflectionResolution resolution,
            ref int maxSteps,
            ref float maxDistance,
            ref float thickness,
            ref float roughnessFade,
            ref float temporalFeedback,
            ref float temporalDepthRejection,
            ref float temporalClamp,
            ref bool temporalAccumulation)
        {
            switch (quality)
            {
                case BurtScreenSpaceReflectionQuality.Low:
                    resolution = BurtScreenSpaceReflectionResolution.Half;
                    maxSteps = 28;
                    maxDistance = 22f;
                    thickness = 0.45f;
                    roughnessFade = 0.45f;
                    temporalFeedback = 0.8f;
                    temporalDepthRejection = 0.03f;
                    temporalClamp = 0.85f;
                    temporalAccumulation = true;
                    break;
                case BurtScreenSpaceReflectionQuality.Medium:
                    resolution = BurtScreenSpaceReflectionResolution.Half;
                    maxSteps = 40;
                    maxDistance = 30f;
                    thickness = 0.35f;
                    roughnessFade = 0.6f;
                    temporalFeedback = 0.86f;
                    temporalDepthRejection = 0.02f;
                    temporalClamp = 1f;
                    temporalAccumulation = true;
                    break;
                case BurtScreenSpaceReflectionQuality.High:
                    resolution = BurtScreenSpaceReflectionResolution.Full;
                    maxSteps = 72;
                    maxDistance = 45f;
                    thickness = 0.28f;
                    roughnessFade = 0.72f;
                    temporalFeedback = 0.9f;
                    temporalDepthRejection = 0.016f;
                    temporalClamp = 1.25f;
                    temporalAccumulation = true;
                    break;
            }
        }

        private static BurtScreenSpaceReflectionSettings DisableAndInvalidateHistory(BurtRenderRequest request, string reason)
        {
            BurtScreenSpaceReflectionHistoryUtility.InvalidateHistory(request != null ? request.Camera : null, reason);
            return BurtScreenSpaceReflectionSettings.Disabled;
        }

        public static int ResolveScreenSpaceReflectionShaderDebugMode()
        {
            if (!BurtShadingDebugSettings.IsDebugging)
            {
                return 0;
            }

            switch (BurtShadingDebugSettings.Mode)
            {
                case BurtShadingDebugMode.ScreenSpaceReflectionRawHitMask:
                    return 1;
                case BurtShadingDebugMode.ScreenSpaceReflectionHitMask:
                    return 11;
                case BurtShadingDebugMode.ScreenSpaceReflectionHitUV:
                    return 2;
                case BurtShadingDebugMode.ScreenSpaceReflectionStepCount:
                    return 3;
                case BurtShadingDebugMode.ScreenSpaceReflectionColor:
                    return 4;
                case BurtShadingDebugMode.ScreenSpaceReflectionConfidence:
                    return 5;
                case BurtShadingDebugMode.ScreenSpaceReflectionDepthDelta:
                    return 6;
                case BurtShadingDebugMode.ScreenSpaceReflectionWorldError:
                    return 7;
                case BurtShadingDebugMode.ScreenSpaceReflectionDenoisedColor:
                    return 8;
                case BurtShadingDebugMode.ScreenSpaceReflectionTemporalColor:
                    return 9;
                case BurtShadingDebugMode.ScreenSpaceReflectionResolveAlpha:
                    return 10;
                case BurtShadingDebugMode.ScreenSpaceReflectionVisibilityAlpha:
                    return 12;
                case BurtShadingDebugMode.ScreenSpaceReflectionMaterialWeight:
                    return 13;
                case BurtShadingDebugMode.ScreenSpaceReflectionRoughnessMip:
                    return 14;
                case BurtShadingDebugMode.ScreenSpaceReflectionResolvedColor:
                    return 15;
                case BurtShadingDebugMode.ScreenSpaceReflectionDepthQuality:
                    return 16;
                case BurtShadingDebugMode.ScreenSpaceReflectionWorldQuality:
                    return 17;
                case BurtShadingDebugMode.ScreenSpaceReflectionResolveQuality:
                    return 18;
                case BurtShadingDebugMode.ScreenSpaceReflectionSurfaceSupport:
                    return 19;
                case BurtShadingDebugMode.ScreenSpaceReflectionHiZSkipCandidate:
                    return 20;
                case BurtShadingDebugMode.ScreenSpaceReflectionHiZMipLevel:
                    return 21;
                case BurtShadingDebugMode.ScreenSpaceReflectionHiZDivergence:
                    return 22;
                case BurtShadingDebugMode.ScreenSpaceReflectionHiZMissedHits:
                    return 23;
                case BurtShadingDebugMode.ScreenSpaceReflectionHiZRawHitMiss:
                    return 24;
                case BurtShadingDebugMode.ScreenSpaceReflectionHiZResolvedHitMiss:
                    return 25;
                case BurtShadingDebugMode.ScreenSpaceReflectionHiZVisibilityMiss:
                    return 26;
                case BurtShadingDebugMode.ScreenSpaceReflectionHiZSkipUsed:
                    return 27;
                case BurtShadingDebugMode.ScreenSpaceReflectionHiZProbeBlocked:
                    return 28;
                case BurtShadingDebugMode.ScreenSpaceReflectionHiZStepCompare:
                    return 29;
                case BurtShadingDebugMode.ScreenSpaceReflectionHiZWorkCompare:
                    return 30;
                case BurtShadingDebugMode.ScreenSpaceReflectionHiZStepSaved:
                    return 31;
                default:
                    return 0;
            }
        }

        public static string ResolveScreenSpaceReflectionDebugModeLabel()
        {
            if (ResolveScreenSpaceReflectionShaderDebugMode() == 0)
            {
                return "Disabled";
            }

            var mode = BurtShadingDebugSettings.Mode;
            if (IsScreenSpaceReflectionHiZDiagnosticMode(mode) && !EnableScreenSpaceReflectionHiZDiagnostics)
            {
                return mode + "(Stubbed)";
            }

            return mode.ToString();
        }

        private static BurtScreenSpaceReflectionVolumeComponent GetScreenSpaceReflectionVolumeComponent()
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

            return stack.GetComponent<BurtScreenSpaceReflectionVolumeComponent>();
        }
    }
}
