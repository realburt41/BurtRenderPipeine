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
            var descriptor = BurtRenderTargetDescriptorUtility.CreateScreenSpaceReflectionColorDescriptor(camera);
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
            var descriptor = BurtRenderTargetDescriptorUtility.CreateScreenSpaceReflectionDenoisedColorDescriptor(camera);
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
            var descriptor = BurtRenderTargetDescriptorUtility.CreateScreenSpaceReflectionTemporalColorDescriptor(camera);
            var cmd = CommandBufferPool.Get(Name);
            cmd.GetTemporaryRT(BurtRenderGraphResourceRegistry.ScreenSpaceReflectionTemporalColorTextureId, descriptor, FilterMode.Bilinear);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.ScreenSpaceReflectionTemporalColorTextureId, target.Identifier);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    internal sealed class BurtScreenSpaceReflectionTracePass : BurtRenderPass
    {
        private const string ScreenSpaceReflectionShaderName = "Hidden/BurtRP/ScreenSpaceReflections";
        private const float ScreenSpaceReflectionEdgeFadeWidth = 0.04f;
        private static readonly int CameraColorTextureId = BurtRenderGraphResourceRegistry.CameraColorTextureId;
        private static readonly int CameraDepthTextureId = BurtRenderGraphResourceRegistry.CameraDepthTextureId;
        private static readonly int GBuffer0Id = BurtRenderGraphResourceRegistry.GBuffer0Id;
        private static readonly int GBuffer1Id = BurtRenderGraphResourceRegistry.GBuffer1Id;
        private static readonly int GBuffer2Id = BurtRenderGraphResourceRegistry.GBuffer2Id;
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
            builder.ReadHiZDepth();
            builder.WriteScreenSpaceReflectionColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!TryGetTargets(context, out var cameraColorTarget, out var cameraDepthTarget, out var gbuffer0Target, out var gbuffer1Target, out var gbuffer2Target, out var hiZDepthTarget, out var ssrColorTarget))
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

            var colorDescriptor = BurtRenderTargetDescriptorUtility.CreateScreenSpaceReflectionColorDescriptor(camera);
            var hiZDescriptor = BurtRenderTargetDescriptorUtility.CreateHiZDepthDescriptor(camera);
            var hiZMipCount = BurtRenderTargetDescriptorUtility.CalculateMipCount(hiZDescriptor.width, hiZDescriptor.height);
            var cmd = CommandBufferPool.Get(Name);

            cmd.SetRenderTarget(ssrColorTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            BindInputs(cmd, cameraColorTarget, cameraDepthTarget, gbuffer0Target, gbuffer1Target, gbuffer2Target, hiZDepthTarget);
            UploadCameraGlobals(cmd, camera, colorDescriptor, hiZMipCount);
            UploadSettings(cmd, settings, hiZMipCount);
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
            out BurtRenderTargetHandle hiZDepthTarget,
            out BurtRenderTargetHandle ssrColorTarget)
        {
            cameraColorTarget = context != null ? context.CameraColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraColorName);
            cameraDepthTarget = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            gbuffer0Target = context != null ? context.GBuffer0Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer0Name);
            gbuffer1Target = context != null ? context.GBuffer1Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer1Name);
            gbuffer2Target = context != null ? context.GBuffer2Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer2Name);
            hiZDepthTarget = context != null ? context.HiZDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.HiZDepthName);
            ssrColorTarget = context != null ? context.ScreenSpaceReflectionColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceReflectionColorName);

            return cameraColorTarget.IsValid &&
                cameraDepthTarget.IsValid &&
                gbuffer0Target.IsValid &&
                gbuffer1Target.IsValid &&
                gbuffer2Target.IsValid &&
                hiZDepthTarget.IsValid &&
                ssrColorTarget.IsValid;
        }

        private static void BindInputs(
            CommandBuffer cmd,
            BurtRenderTargetHandle cameraColorTarget,
            BurtRenderTargetHandle cameraDepthTarget,
            BurtRenderTargetHandle gbuffer0Target,
            BurtRenderTargetHandle gbuffer1Target,
            BurtRenderTargetHandle gbuffer2Target,
            BurtRenderTargetHandle hiZDepthTarget)
        {
            cmd.SetGlobalTexture(CameraColorTextureId, cameraColorTarget.Identifier);
            cmd.SetGlobalTexture(SSRSourceColorTextureId, cameraColorTarget.Identifier);
            cmd.SetGlobalTexture(CameraDepthTextureId, cameraDepthTarget.Identifier);
            cmd.SetGlobalTexture(GBuffer0Id, gbuffer0Target.Identifier);
            cmd.SetGlobalTexture(GBuffer1Id, gbuffer1Target.Identifier);
            cmd.SetGlobalTexture(GBuffer2Id, gbuffer2Target.Identifier);
            cmd.SetGlobalTexture(HiZDepthTextureId, hiZDepthTarget.Identifier);
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
            var cameraPosition = camera.transform.position;

            cmd.SetGlobalMatrix(SSRViewMatrixId, viewMatrix);
            cmd.SetGlobalMatrix(SSRViewProjectionMatrixId, viewProjectionMatrix);
            cmd.SetGlobalMatrix(InverseViewProjectionMatrixId, inverseViewProjectionMatrix);
            cmd.SetGlobalVector(CameraWorldPositionId, new Vector4(cameraPosition.x, cameraPosition.y, cameraPosition.z, 1f));
            cmd.SetGlobalVector(DeferredScreenSizeId, new Vector4(width, height, 1f / width, 1f / height));
            cmd.SetGlobalVector(SSRSourceTexelSizeId, new Vector4(1f / width, 1f / height, width, height));
        }

        private static void UploadSettings(CommandBuffer cmd, BurtScreenSpaceReflectionSettings settings, int hiZMipCount)
        {
            var maxMip = Mathf.Max(0, hiZMipCount - 1);
            var debugMode = BurtScreenSpaceReflectionPassUtility.ResolveScreenSpaceReflectionShaderDebugMode();

            cmd.SetGlobalVector(SSRParams0Id, new Vector4(settings.MaxDistance, settings.Thickness, settings.Intensity, settings.RoughnessFade));
            cmd.SetGlobalVector(SSRParams1Id, new Vector4(settings.MaxSteps, maxMip, debugMode, ScreenSpaceReflectionEdgeFadeWidth));
            cmd.SetGlobalVector(SSRParams2Id, new Vector4(Time.frameCount & 7, settings.TemporalAccumulation ? 1f : 0f, 0f, 0f));
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
        private const string ScreenSpaceReflectionShaderName = "Hidden/BurtRP/ScreenSpaceReflections";
        private static readonly int CameraDepthTextureId = BurtRenderGraphResourceRegistry.CameraDepthTextureId;
        private static readonly int GBuffer0Id = BurtRenderGraphResourceRegistry.GBuffer0Id;
        private static readonly int GBuffer1Id = BurtRenderGraphResourceRegistry.GBuffer1Id;
        private static readonly int GBuffer2Id = BurtRenderGraphResourceRegistry.GBuffer2Id;
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
            builder.WriteScreenSpaceReflectionDenoisedColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!TryGetTargets(context, out var cameraDepthTarget, out var gbuffer0Target, out var gbuffer1Target, out var gbuffer2Target, out var ssrColorTarget, out var ssrDenoisedColorTarget))
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

            var descriptor = BurtRenderTargetDescriptorUtility.CreateScreenSpaceReflectionDenoisedColorDescriptor(camera);
            var width = Mathf.Max(1, descriptor.width);
            var height = Mathf.Max(1, descriptor.height);
            var cmd = CommandBufferPool.Get(Name);
            cmd.SetRenderTarget(ssrDenoisedColorTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            cmd.SetGlobalTexture(CameraDepthTextureId, cameraDepthTarget.Identifier);
            cmd.SetGlobalTexture(GBuffer0Id, gbuffer0Target.Identifier);
            cmd.SetGlobalTexture(GBuffer1Id, gbuffer1Target.Identifier);
            cmd.SetGlobalTexture(GBuffer2Id, gbuffer2Target.Identifier);
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
            out BurtRenderTargetHandle ssrColorTarget,
            out BurtRenderTargetHandle ssrDenoisedColorTarget)
        {
            cameraDepthTarget = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            gbuffer0Target = context != null ? context.GBuffer0Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer0Name);
            gbuffer1Target = context != null ? context.GBuffer1Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer1Name);
            gbuffer2Target = context != null ? context.GBuffer2Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer2Name);
            ssrColorTarget = context != null ? context.ScreenSpaceReflectionColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceReflectionColorName);
            ssrDenoisedColorTarget = context != null ? context.ScreenSpaceReflectionDenoisedColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceReflectionDenoisedColorName);

            return cameraDepthTarget.IsValid &&
                gbuffer0Target.IsValid &&
                gbuffer1Target.IsValid &&
                gbuffer2Target.IsValid &&
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
        private const string ScreenSpaceReflectionShaderName = "Hidden/BurtRP/ScreenSpaceReflections";
        private static readonly int CameraDepthTextureId = BurtRenderGraphResourceRegistry.CameraDepthTextureId;
        private static readonly int GBuffer0Id = BurtRenderGraphResourceRegistry.GBuffer0Id;
        private static readonly int GBuffer1Id = BurtRenderGraphResourceRegistry.GBuffer1Id;
        private static readonly int GBuffer2Id = BurtRenderGraphResourceRegistry.GBuffer2Id;
        private static readonly int SSRDenoisedColorTextureId = BurtRenderGraphResourceRegistry.ScreenSpaceReflectionDenoisedColorTextureId;
        private static readonly int SSRTemporalColorTextureId = BurtRenderGraphResourceRegistry.ScreenSpaceReflectionTemporalColorTextureId;
        private static readonly int SSRHistoryTextureId = Shader.PropertyToID("_BurtSSRHistoryTexture");
        private static readonly int SSRHistoryDepthTextureId = Shader.PropertyToID("_BurtSSRHistoryDepthTexture");
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
            builder.WriteScreenSpaceReflectionTemporalColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!TryGetTargets(context, out var cameraDepthTarget, out var gbuffer0Target, out var gbuffer1Target, out var gbuffer2Target, out var ssrDenoisedColorTarget, out var ssrTemporalColorTarget))
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

            var descriptor = BurtRenderTargetDescriptorUtility.CreateScreenSpaceReflectionTemporalColorDescriptor(camera);
            var width = Mathf.Max(1, descriptor.width);
            var height = Mathf.Max(1, descriptor.height);
            var historyValid = false;
            var history = settings.TemporalAccumulation
                ? BurtScreenSpaceReflectionHistoryUtility.EnsureHistoryTextures(context.Request, context.Asset, out historyValid)
                : BurtScreenSpaceReflectionHistoryTextures.CreateInvalid(BurtScreenSpaceReflectionHistoryUtility.CreateCurrentMatrices(context.Request));

            if (!settings.TemporalAccumulation)
            {
                historyValid = false;
                BurtScreenSpaceReflectionHistoryUtility.InvalidateHistory(camera, "TemporalDisabled");
            }

            var cmd = CommandBufferPool.Get(Name);
            BindInputs(cmd, cameraDepthTarget, gbuffer0Target, gbuffer1Target, gbuffer2Target, ssrDenoisedColorTarget);
            UploadTemporalGlobals(cmd, history, settings, width, height, historyValid);

            cmd.SetRenderTarget(ssrTemporalColorTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            cmd.DrawProcedural(Matrix4x4.identity, material, 3, MeshTopology.Triangles, 3, 1);
            cmd.GenerateMips(ssrTemporalColorTarget.Identifier);
            cmd.SetGlobalTexture(SSRTemporalColorTextureId, ssrTemporalColorTarget.Identifier);

            if (settings.TemporalAccumulation && history.Color != null && history.Depth != null)
            {
                cmd.SetRenderTarget(history.Color);
                BurtRenderTargetDescriptorUtility.SetViewport(cmd, width, height);
                cmd.DrawProcedural(Matrix4x4.identity, material, 4, MeshTopology.Triangles, 3, 1);

                cmd.SetRenderTarget(history.Depth);
                BurtRenderTargetDescriptorUtility.SetViewport(cmd, width, height);
                cmd.DrawProcedural(Matrix4x4.identity, material, 5, MeshTopology.Triangles, 3, 1);
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
            out BurtRenderTargetHandle ssrDenoisedColorTarget,
            out BurtRenderTargetHandle ssrTemporalColorTarget)
        {
            cameraDepthTarget = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            gbuffer0Target = context != null ? context.GBuffer0Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer0Name);
            gbuffer1Target = context != null ? context.GBuffer1Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer1Name);
            gbuffer2Target = context != null ? context.GBuffer2Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer2Name);
            ssrDenoisedColorTarget = context != null ? context.ScreenSpaceReflectionDenoisedColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceReflectionDenoisedColorName);
            ssrTemporalColorTarget = context != null ? context.ScreenSpaceReflectionTemporalColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceReflectionTemporalColorName);

            return cameraDepthTarget.IsValid &&
                gbuffer0Target.IsValid &&
                gbuffer1Target.IsValid &&
                gbuffer2Target.IsValid &&
                ssrDenoisedColorTarget.IsValid &&
                ssrTemporalColorTarget.IsValid;
        }

        private static void BindInputs(
            CommandBuffer cmd,
            BurtRenderTargetHandle cameraDepthTarget,
            BurtRenderTargetHandle gbuffer0Target,
            BurtRenderTargetHandle gbuffer1Target,
            BurtRenderTargetHandle gbuffer2Target,
            BurtRenderTargetHandle ssrDenoisedColorTarget)
        {
            cmd.SetGlobalTexture(CameraDepthTextureId, cameraDepthTarget.Identifier);
            cmd.SetGlobalTexture(GBuffer0Id, gbuffer0Target.Identifier);
            cmd.SetGlobalTexture(GBuffer1Id, gbuffer1Target.Identifier);
            cmd.SetGlobalTexture(GBuffer2Id, gbuffer2Target.Identifier);
            cmd.SetGlobalTexture(SSRDenoisedColorTextureId, ssrDenoisedColorTarget.Identifier);
        }

        private static void UploadTemporalGlobals(
            CommandBuffer cmd,
            BurtScreenSpaceReflectionHistoryTextures history,
            BurtScreenSpaceReflectionSettings settings,
            int width,
            int height,
            bool historyValid)
        {
            cmd.SetGlobalTexture(SSRHistoryTextureId, history.Color != null ? (Texture)history.Color : Texture2D.blackTexture);
            cmd.SetGlobalTexture(SSRHistoryDepthTextureId, history.Depth != null ? (Texture)history.Depth : Texture2D.blackTexture);
            cmd.SetGlobalMatrix(SSRPreviousViewMatrixId, history.PreviousViewMatrix);
            cmd.SetGlobalMatrix(SSRPreviousViewProjectionMatrixId, history.PreviousViewProjectionMatrix);
            cmd.SetGlobalMatrix(InverseViewProjectionMatrixId, history.CurrentInverseViewProjectionMatrix);
            cmd.SetGlobalVector(DeferredScreenSizeId, new Vector4(width, height, 1f / width, 1f / height));
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
        private const string ScreenSpaceReflectionShaderName = "Hidden/BurtRP/ScreenSpaceReflections";
        private static readonly int CameraDepthTextureId = BurtRenderGraphResourceRegistry.CameraDepthTextureId;
        private static readonly int GBuffer0Id = BurtRenderGraphResourceRegistry.GBuffer0Id;
        private static readonly int GBuffer1Id = BurtRenderGraphResourceRegistry.GBuffer1Id;
        private static readonly int GBuffer2Id = BurtRenderGraphResourceRegistry.GBuffer2Id;
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
            builder.ReadScreenSpaceReflectionTemporalColor();
            builder.WriteCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!TryGetTargets(context, out var cameraColorTarget, out var cameraDepthTarget, out var gbuffer0Target, out var gbuffer1Target, out var gbuffer2Target, out var ssrTemporalColorTarget))
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

            var descriptor = BurtRenderTargetDescriptorUtility.CreateScreenSpaceReflectionTemporalColorDescriptor(camera);
            var width = Mathf.Max(1, descriptor.width);
            var height = Mathf.Max(1, descriptor.height);
            var maxMip = Mathf.Max(0, descriptor.mipCount - 1);
            var cmd = CommandBufferPool.Get(Name);
            cmd.SetRenderTarget(cameraColorTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            BindInputs(cmd, cameraDepthTarget, gbuffer0Target, gbuffer1Target, gbuffer2Target, ssrTemporalColorTarget);
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
            out BurtRenderTargetHandle ssrTemporalColorTarget)
        {
            cameraColorTarget = context != null ? context.CameraColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraColorName);
            cameraDepthTarget = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            gbuffer0Target = context != null ? context.GBuffer0Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer0Name);
            gbuffer1Target = context != null ? context.GBuffer1Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer1Name);
            gbuffer2Target = context != null ? context.GBuffer2Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer2Name);
            ssrTemporalColorTarget = context != null ? context.ScreenSpaceReflectionTemporalColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceReflectionTemporalColorName);

            return cameraColorTarget.IsValid &&
                cameraDepthTarget.IsValid &&
                gbuffer0Target.IsValid &&
                gbuffer1Target.IsValid &&
                gbuffer2Target.IsValid &&
                ssrTemporalColorTarget.IsValid;
        }

        private static void BindInputs(
            CommandBuffer cmd,
            BurtRenderTargetHandle cameraDepthTarget,
            BurtRenderTargetHandle gbuffer0Target,
            BurtRenderTargetHandle gbuffer1Target,
            BurtRenderTargetHandle gbuffer2Target,
            BurtRenderTargetHandle ssrTemporalColorTarget)
        {
            cmd.SetGlobalTexture(CameraDepthTextureId, cameraDepthTarget.Identifier);
            cmd.SetGlobalTexture(GBuffer0Id, gbuffer0Target.Identifier);
            cmd.SetGlobalTexture(GBuffer1Id, gbuffer1Target.Identifier);
            cmd.SetGlobalTexture(GBuffer2Id, gbuffer2Target.Identifier);
            cmd.SetGlobalTexture(SSRTemporalColorTextureId, ssrTemporalColorTarget.Identifier);
        }

        private static void UploadCameraGlobals(CommandBuffer cmd, Camera camera, int width, int height)
        {
            var viewMatrix = camera.worldToCameraMatrix;
            var projectionMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
            var inverseViewProjectionMatrix = (projectionMatrix * viewMatrix).inverse;
            var cameraPosition = camera.transform.position;

            cmd.SetGlobalMatrix(InverseViewProjectionMatrixId, inverseViewProjectionMatrix);
            cmd.SetGlobalVector(CameraWorldPositionId, new Vector4(cameraPosition.x, cameraPosition.y, cameraPosition.z, 1f));
            cmd.SetGlobalVector(DeferredScreenSizeId, new Vector4(width, height, 1f / width, 1f / height));
        }

        private static void UploadSettings(CommandBuffer cmd, BurtScreenSpaceReflectionSettings settings, int maxMip)
        {
            cmd.SetGlobalVector(SSRParams0Id, new Vector4(settings.MaxDistance, settings.Thickness, settings.Intensity, settings.RoughnessFade));
            cmd.SetGlobalVector(SSRParams1Id, new Vector4(0f, maxMip, BurtScreenSpaceReflectionPassUtility.ResolveScreenSpaceReflectionShaderDebugMode(), 0.1f));
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
            48,
            30f,
            0.35f,
            1f,
            0.6f,
            false,
            0.86f,
            0.02f,
            1f);

        public BurtScreenSpaceReflectionSettings(
            bool enabled,
            int maxSteps,
            float maxDistance,
            float thickness,
            float intensity,
            float roughnessFade,
            bool temporalAccumulation,
            float temporalFeedback,
            float temporalDepthRejection,
            float temporalClamp)
        {
            Enabled = enabled;
            MaxSteps = Mathf.Clamp(maxSteps, 1, 512);
            MaxDistance = Mathf.Max(0.01f, maxDistance);
            Thickness = Mathf.Max(0.0001f, thickness);
            Intensity = Mathf.Clamp01(intensity);
            RoughnessFade = Mathf.Clamp01(roughnessFade);
            TemporalAccumulation = temporalAccumulation;
            TemporalFeedback = Mathf.Clamp(temporalFeedback, 0f, 0.98f);
            TemporalDepthRejection = Mathf.Clamp(temporalDepthRejection, 0.001f, 0.2f);
            TemporalClamp = Mathf.Clamp(temporalClamp, 0.25f, 4f);
        }

        public bool Enabled { get; }

        public int MaxSteps { get; }

        public float MaxDistance { get; }

        public float Thickness { get; }

        public float Intensity { get; }

        public float RoughnessFade { get; }

        public bool TemporalAccumulation { get; }

        public float TemporalFeedback { get; }

        public float TemporalDepthRejection { get; }

        public float TemporalClamp { get; }
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
        public Matrix4x4 PreviousViewMatrix { get; }
        public Matrix4x4 PreviousViewProjectionMatrix { get; }
        public Matrix4x4 CurrentInverseViewProjectionMatrix { get; }

        public BurtScreenSpaceReflectionHistoryTextures(
            RenderTexture color,
            RenderTexture depth,
            Matrix4x4 previousViewMatrix,
            Matrix4x4 previousViewProjectionMatrix,
            Matrix4x4 currentInverseViewProjectionMatrix)
        {
            Color = color;
            Depth = depth;
            PreviousViewMatrix = previousViewMatrix;
            PreviousViewProjectionMatrix = previousViewProjectionMatrix;
            CurrentInverseViewProjectionMatrix = currentInverseViewProjectionMatrix;
        }

        public static BurtScreenSpaceReflectionHistoryTextures CreateInvalid(BurtScreenSpaceReflectionHistoryMatrices matrices)
        {
            return new BurtScreenSpaceReflectionHistoryTextures(null, null, matrices.ViewMatrix, matrices.ViewProjectionMatrix, matrices.InverseViewProjectionMatrix);
        }
    }

    internal readonly struct BurtScreenSpaceReflectionHistoryStatus
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
        public string LastInvalidationReason { get; }

        public BurtScreenSpaceReflectionHistoryStatus(
            bool hasHistory,
            bool descriptorMatches,
            bool hasDepthHistory,
            bool depthDescriptorMatches,
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
        private const int HistoryAlgorithmVersion = 27;
        private const float ProjectionChangeEpsilon = 0.0001f;

        private sealed class CameraState
        {
            public RenderTexture ColorHistory;
            public RenderTexture DepthHistory;
            public RenderTextureDescriptor ColorDescriptor;
            public RenderTextureDescriptor DepthDescriptor;
            public int AlgorithmVersion;
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

        public static BurtScreenSpaceReflectionHistoryTextures EnsureHistoryTextures(
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset,
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
            var descriptorsMatch = colorMatches && depthMatches;
            var rendererMode = asset != null ? asset.RendererMode : BurtRendererMode.Forward;
            GetTargetSize(camera, out var targetWidth, out var targetHeight);
            var invalidationReason = ResolveHistoryInvalidationReason(camera, state, rendererMode, matrices.NonJitteredProjectionMatrix, targetWidth, targetHeight, descriptorsMatch);

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
            CaptureCurrentCameraState(camera, state, targetWidth, targetHeight);

            historyValid = state.HasValidHistory && state.HasPreviousCameraState && state.ColorHistory != null && state.DepthHistory != null;
            var previousViewMatrix = state.HasPreviousCameraState ? state.PreviousViewMatrix : matrices.ViewMatrix;
            var previousViewProjectionMatrix = state.HasPreviousCameraState ? state.PreviousViewProjectionMatrix : matrices.ViewProjectionMatrix;
            return new BurtScreenSpaceReflectionHistoryTextures(
                state.ColorHistory,
                state.DepthHistory,
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
                return new BurtScreenSpaceReflectionHistoryStatus(false, false, false, false, 0, 0, RenderTextureFormat.Default, 0, 0, "NoCameraOrHistory");
            }

            var colorDescriptor = CreateColorHistoryDescriptor(camera);
            var depthDescriptor = CreateDepthHistoryDescriptor(camera);
            var hasColor = state.ColorHistory != null;
            var hasDepth = state.DepthHistory != null;
            var historyAge = state.HasValidHistory && state.FirstValidFrameIndex > 0 ? Mathf.Max(0, state.FrameIndex - state.FirstValidFrameIndex + 1) : 0;
            return new BurtScreenSpaceReflectionHistoryStatus(
                state.HasValidHistory && hasColor,
                hasColor && Matches(state.ColorDescriptor, colorDescriptor),
                hasDepth,
                hasDepth && Matches(state.DepthDescriptor, depthDescriptor),
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

        private static RenderTextureDescriptor CreateColorHistoryDescriptor(Camera camera)
        {
            return BurtRenderTargetDescriptorUtility.CreateScreenSpaceReflectionColorDescriptor(camera);
        }

        private static RenderTextureDescriptor CreateDepthHistoryDescriptor(Camera camera)
        {
            var descriptor = BurtRenderTargetDescriptorUtility.CreateScreenSpaceReflectionTemporalColorDescriptor(camera);
            descriptor.colorFormat = RenderTextureFormat.RFloat;
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
            return left.width == right.width && left.height == right.height && left.colorFormat == right.colorFormat && left.sRGB == right.sRGB;
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
            state.LastInvalidationReason = string.IsNullOrEmpty(reason) ? "Unknown" : reason;
        }

        private static void SetAllocationInvalidationReason(CameraState state, string reason)
        {
            if (state == null)
            {
                return;
            }

            if (!state.HasPreviousCameraState || state.LastInvalidationReason == "NeverAllocated" || state.LastInvalidationReason == "HistoryAllocated" || state.LastInvalidationReason == "DepthHistoryAllocated")
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
            state.ColorHistory = null;
            state.DepthHistory = null;
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
                mode == BurtShadingDebugMode.ScreenSpaceReflectionSurfaceSupport;
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

            return new BurtScreenSpaceReflectionSettings(
                true,
                screenSpaceReflections.maxSteps.value,
                screenSpaceReflections.maxDistance.value,
                screenSpaceReflections.thickness.value,
                screenSpaceReflections.intensity.value,
                screenSpaceReflections.roughnessFade.value,
                screenSpaceReflections.temporalAccumulation.value,
                screenSpaceReflections.temporalFeedback.value,
                screenSpaceReflections.temporalDepthRejection.value,
                screenSpaceReflections.temporalClamp.value);
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
                default:
                    return 0;
            }
        }

        public static string ResolveScreenSpaceReflectionDebugModeLabel()
        {
            return ResolveScreenSpaceReflectionShaderDebugMode() != 0 ? BurtShadingDebugSettings.Mode.ToString() : "Disabled";
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
