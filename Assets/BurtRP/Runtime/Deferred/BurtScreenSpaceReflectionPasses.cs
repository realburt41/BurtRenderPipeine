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

    internal sealed class BurtScreenSpaceReflectionPass : BurtRenderPass
    {
        private const string ScreenSpaceReflectionShaderName = "Hidden/BurtRP/ScreenSpaceReflections";
        private static readonly int CameraColorTextureId = BurtRenderGraphResourceRegistry.CameraColorTextureId;
        private static readonly int CameraDepthTextureId = BurtRenderGraphResourceRegistry.CameraDepthTextureId;
        private static readonly int GBuffer0Id = BurtRenderGraphResourceRegistry.GBuffer0Id;
        private static readonly int GBuffer1Id = BurtRenderGraphResourceRegistry.GBuffer1Id;
        private static readonly int GBuffer2Id = BurtRenderGraphResourceRegistry.GBuffer2Id;
        private static readonly int HiZDepthTextureId = BurtRenderGraphResourceRegistry.HiZDepthTextureId;
        private static readonly int SSRColorTextureId = BurtRenderGraphResourceRegistry.ScreenSpaceReflectionColorTextureId;
        private static readonly int SSRSourceColorTextureId = Shader.PropertyToID("_BurtSSRSourceColorTexture");
        private static readonly int SSRSourceTexelSizeId = Shader.PropertyToID("_BurtSSRSourceTexelSize");
        private static readonly int SSRViewMatrixId = Shader.PropertyToID("_BurtSSRViewMatrix");
        private static readonly int SSRViewProjectionMatrixId = Shader.PropertyToID("_BurtSSRViewProjectionMatrix");
        private static readonly int SSRParams0Id = Shader.PropertyToID("_BurtSSRParams0");
        private static readonly int SSRParams1Id = Shader.PropertyToID("_BurtSSRParams1");
        private static readonly int InverseViewProjectionMatrixId = Shader.PropertyToID("_BurtDeferredInverseViewProjectionMatrix");
        private static readonly int CameraWorldPositionId = Shader.PropertyToID("_BurtDeferredCameraWorldPosition");
        private static readonly int DeferredScreenSizeId = Shader.PropertyToID("_BurtDeferredScreenSize");

        private Material screenSpaceReflectionMaterial;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Screen Space Reflections";

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
            builder.WriteCameraColor();
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

            cmd.SetRenderTarget(cameraColorTarget.Identifier, cameraDepthTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            cmd.SetGlobalTexture(SSRColorTextureId, ssrColorTarget.Identifier);
            cmd.DrawProcedural(Matrix4x4.identity, material, 1, MeshTopology.Triangles, 3, 1);

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
            cmd.SetGlobalVector(SSRParams1Id, new Vector4(settings.MaxSteps, maxMip, debugMode, 0.1f));
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

    internal readonly struct BurtScreenSpaceReflectionSettings
    {
        public static readonly BurtScreenSpaceReflectionSettings Disabled = new BurtScreenSpaceReflectionSettings(
            false,
            48,
            30f,
            0.35f,
            1f,
            0.6f);

        public BurtScreenSpaceReflectionSettings(
            bool enabled,
            int maxSteps,
            float maxDistance,
            float thickness,
            float intensity,
            float roughnessFade)
        {
            Enabled = enabled;
            MaxSteps = Mathf.Clamp(maxSteps, 1, 128);
            MaxDistance = Mathf.Max(0.01f, maxDistance);
            Thickness = Mathf.Max(0.0001f, thickness);
            Intensity = Mathf.Clamp01(intensity);
            RoughnessFade = Mathf.Clamp01(roughnessFade);
        }

        public bool Enabled { get; }

        public int MaxSteps { get; }

        public float MaxDistance { get; }

        public float Thickness { get; }

        public float Intensity { get; }

        public float RoughnessFade { get; }
    }

    internal static class BurtScreenSpaceReflectionPassUtility
    {
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
                return BurtScreenSpaceReflectionSettings.Disabled;
            }

            if (asset == null || asset.RendererMode != BurtRendererMode.Deferred)
            {
                return BurtScreenSpaceReflectionSettings.Disabled;
            }

            var screenSpaceReflections = GetScreenSpaceReflectionVolumeComponent();
            if (screenSpaceReflections == null || !screenSpaceReflections.IsEnabled())
            {
                return BurtScreenSpaceReflectionSettings.Disabled;
            }

            return new BurtScreenSpaceReflectionSettings(
                true,
                screenSpaceReflections.maxSteps.value,
                screenSpaceReflections.maxDistance.value,
                screenSpaceReflections.thickness.value,
                screenSpaceReflections.intensity.value,
                screenSpaceReflections.roughnessFade.value);
        }

        public static int ResolveScreenSpaceReflectionShaderDebugMode()
        {
            if (!BurtShadingDebugSettings.IsDebugging)
            {
                return 0;
            }

            switch (BurtShadingDebugSettings.Mode)
            {
                case BurtShadingDebugMode.ScreenSpaceReflectionHitMask:
                    return 1;
                case BurtShadingDebugMode.ScreenSpaceReflectionHitUV:
                    return 2;
                case BurtShadingDebugMode.ScreenSpaceReflectionStepCount:
                    return 3;
                case BurtShadingDebugMode.ScreenSpaceReflectionColor:
                    return 4;
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
