using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    internal sealed class BurtAllocateScreenSpaceShadowPass : BurtRenderPass
    {
        public override string Name => "Burt Allocate Screen Space Shadow";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceShadowPassUtility.ShouldUseScreenSpaceShadow(builder.Request, builder.Asset))
            {
                return;
            }

            builder.WriteScreenSpaceShadow();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceShadowPassUtility.ShouldUseScreenSpaceShadow(context.Request, context.Asset))
            {
                return;
            }

            var target = context.ScreenSpaceShadowTarget;
            if (!target.IsValid)
            {
                return;
            }

            var camera = context.Request != null ? context.Request.Camera : null;
            var settings = BurtScreenSpaceShadowPassUtility.ResolveScreenSpaceShadowSettings(context.Request, context.Asset);
            var descriptor = BurtRenderTargetDescriptorUtility.CreateScreenSpaceShadowDescriptor(camera, settings.DownsampleFactor);
            BurtScreenSpaceShadowRenderTargetUtility.Allocate(context, Name, BurtRenderGraphResourceRegistry.ScreenSpaceShadowTextureId, target, descriptor, FilterMode.Bilinear);
        }
    }

    internal sealed class BurtScreenSpaceShadowTracePass : BurtRenderPass
    {
        private const string ScreenSpaceShadowShaderName = "Hidden/BurtRP/ScreenSpaceShadow";
        private const int TracePassIndex = 0;

        private static readonly int CameraDepthTextureId = BurtRenderGraphResourceRegistry.CameraDepthTextureId;
        private static readonly int GBuffer0Id = BurtRenderGraphResourceRegistry.GBuffer0Id;
        private static readonly int GBuffer1Id = BurtRenderGraphResourceRegistry.GBuffer1Id;
        private static readonly int GBuffer2Id = BurtRenderGraphResourceRegistry.GBuffer2Id;
        private static readonly int GBuffer3Id = BurtRenderGraphResourceRegistry.GBuffer3Id;
        private static readonly int GBuffer4Id = BurtRenderGraphResourceRegistry.GBuffer4Id;
        private static readonly int GBuffer5Id = BurtRenderGraphResourceRegistry.GBuffer5Id;
        private static readonly int ScreenSpaceShadowTextureId = BurtRenderGraphResourceRegistry.ScreenSpaceShadowTextureId;
        private static readonly int ViewProjectionMatrixId = Shader.PropertyToID("_BurtSSShadowViewProjectionMatrix");
        private static readonly int InverseViewProjectionMatrixId = Shader.PropertyToID("_BurtDeferredInverseViewProjectionMatrix");
        private static readonly int CameraWorldPositionId = Shader.PropertyToID("_BurtDeferredCameraWorldPosition");
        private static readonly int ScreenSizeId = Shader.PropertyToID("_BurtDeferredScreenSize");
        private static readonly int TraceScreenSizeId = Shader.PropertyToID("_BurtSSShadowTraceScreenSize");
        private static readonly int Params0Id = Shader.PropertyToID("_BurtSSShadowParams0");
        private static readonly int Params1Id = Shader.PropertyToID("_BurtSSShadowParams1");
        private static readonly int Params2Id = Shader.PropertyToID("_BurtSSShadowParams2");
        private static readonly int ContrastParamsId = Shader.PropertyToID("_BurtSSShadowContrastParams");

        private Material screenSpaceShadowMaterial;
        private bool hasLoggedMissingShader;
        private bool hasLoggedMissingShaderPass;

        public override string Name => "Burt Screen Space Shadow Trace";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.FullScreen;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceShadowPassUtility.ShouldUseScreenSpaceShadow(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadCameraDepth();
            builder.ReadGBuffer0();
            builder.ReadGBuffer1();
            builder.ReadGBuffer2();
            builder.ReadGBuffer3();
            builder.ReadGBuffer4();
            builder.ReadGBuffer5();
            builder.ReadLightingGlobals();
            builder.WriteScreenSpaceShadow();
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
                    out var gbuffer5Target,
                    out var shadowTarget))
            {
                return;
            }

            var settings = BurtScreenSpaceShadowPassUtility.ResolveScreenSpaceShadowSettings(context.Request, context.Asset);
            if (!settings.Enabled)
            {
                return;
            }

            var material = GetScreenSpaceShadowMaterial();
            if (material == null || !HasRequiredShaderPass(material))
            {
                return;
            }

            var camera = context.Request != null ? context.Request.Camera : null;
            if (camera == null)
            {
                return;
            }

            var fullDescriptor = BurtRenderTargetDescriptorUtility.CreateScreenSpaceShadowDescriptor(camera);
            var traceDescriptor = BurtRenderTargetDescriptorUtility.CreateScreenSpaceShadowDescriptor(camera, settings.DownsampleFactor);
            var cmd = CommandBufferPool.Get(Name);
            cmd.SetRenderTarget(shadowTarget.Identifier);
            cmd.SetViewport(new Rect(0f, 0f, Mathf.Max(1, traceDescriptor.width), Mathf.Max(1, traceDescriptor.height)));
            cmd.SetGlobalTexture(CameraDepthTextureId, cameraDepthTarget.Identifier);
            BurtDeferredStencilTextureUtility.BindGlobal(cmd, cameraDepthTarget, camera);
            cmd.SetGlobalTexture(GBuffer0Id, gbuffer0Target.Identifier);
            cmd.SetGlobalTexture(GBuffer1Id, gbuffer1Target.Identifier);
            cmd.SetGlobalTexture(GBuffer2Id, gbuffer2Target.Identifier);
            cmd.SetGlobalTexture(GBuffer3Id, gbuffer3Target.Identifier);
            cmd.SetGlobalTexture(GBuffer4Id, gbuffer4Target.Identifier);
            cmd.SetGlobalTexture(GBuffer5Id, gbuffer5Target.Identifier);
            UploadCameraGlobals(cmd, camera, fullDescriptor, traceDescriptor);
            UploadSettings(cmd, settings);
            cmd.DrawProcedural(Matrix4x4.identity, material, TracePassIndex, MeshTopology.Triangles, 3, 1);
            cmd.SetGlobalTexture(ScreenSpaceShadowTextureId, shadowTarget.Identifier);
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
            out BurtRenderTargetHandle gbuffer5Target,
            out BurtRenderTargetHandle shadowTarget)
        {
            cameraDepthTarget = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            gbuffer0Target = context != null ? context.GBuffer0Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer0Name);
            gbuffer1Target = context != null ? context.GBuffer1Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer1Name);
            gbuffer2Target = context != null ? context.GBuffer2Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer2Name);
            gbuffer3Target = context != null ? context.GBuffer3Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer3Name);
            gbuffer4Target = context != null ? context.GBuffer4Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer4Name);
            gbuffer5Target = context != null ? context.GBuffer5Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer5Name);
            shadowTarget = context != null ? context.ScreenSpaceShadowTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceShadowName);
            return cameraDepthTarget.IsValid &&
                gbuffer0Target.IsValid &&
                gbuffer1Target.IsValid &&
                gbuffer2Target.IsValid &&
                gbuffer3Target.IsValid &&
                gbuffer4Target.IsValid &&
                gbuffer5Target.IsValid &&
                shadowTarget.IsValid;
        }

        private static void UploadCameraGlobals(CommandBuffer cmd, Camera camera, RenderTextureDescriptor fullDescriptor, RenderTextureDescriptor traceDescriptor)
        {
            var viewMatrix = camera.worldToCameraMatrix;
            var projectionMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
            var viewProjectionMatrix = projectionMatrix * viewMatrix;
            var inverseViewProjectionMatrix = viewProjectionMatrix.inverse;
            var width = Mathf.Max(1, fullDescriptor.width);
            var height = Mathf.Max(1, fullDescriptor.height);
            var traceWidth = Mathf.Max(1, traceDescriptor.width);
            var traceHeight = Mathf.Max(1, traceDescriptor.height);
            var cameraPosition = camera.transform.position;

            cmd.SetGlobalMatrix(ViewProjectionMatrixId, viewProjectionMatrix);
            cmd.SetGlobalMatrix(InverseViewProjectionMatrixId, inverseViewProjectionMatrix);
            cmd.SetGlobalVector(CameraWorldPositionId, new Vector4(cameraPosition.x, cameraPosition.y, cameraPosition.z, 1f));
            cmd.SetGlobalVector(ScreenSizeId, new Vector4(width, height, 1f / width, 1f / height));
            cmd.SetGlobalVector(TraceScreenSizeId, new Vector4(traceWidth, traceHeight, 1f / traceWidth, 1f / traceHeight));
        }

        private static void UploadSettings(CommandBuffer cmd, BurtScreenSpaceShadowSettings settings)
        {
            cmd.SetGlobalVector(Params0Id, new Vector4(settings.DepthOffset, settings.MaxDistance, settings.Thickness, settings.Intensity));
            cmd.SetGlobalVector(Params1Id, new Vector4(settings.SampleCount, settings.FadeDistance, settings.FadeRadius, Time.frameCount & 1023));
            cmd.SetGlobalVector(Params2Id, new Vector4(settings.BilinearThreshold * 0.01f, settings.BilinearSamplingOffset ? 1f : 0f, settings.DownsampleFactor, 0f));
            cmd.SetGlobalVector(ContrastParamsId, new Vector4(settings.GrassContrast, settings.DetailContrast, settings.FoliageContrast, settings.CharacterContrast));
        }

        private Material GetScreenSpaceShadowMaterial()
        {
            if (screenSpaceShadowMaterial != null)
            {
                return screenSpaceShadowMaterial;
            }

            var shader = Shader.Find(ScreenSpaceShadowShaderName);
            if (shader == null)
            {
                if (!hasLoggedMissingShader)
                {
                    Debug.LogWarning("BurtRP could not find shader: " + ScreenSpaceShadowShaderName);
                    hasLoggedMissingShader = true;
                }

                return null;
            }

            screenSpaceShadowMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            return screenSpaceShadowMaterial;
        }

        private bool HasRequiredShaderPass(Material material)
        {
            if (material != null && TracePassIndex >= 0 && TracePassIndex < material.passCount)
            {
                return true;
            }

            if (!hasLoggedMissingShaderPass)
            {
                Debug.LogWarning("BurtRP screen-space shadow shader pass missing: " + ScreenSpaceShadowShaderName + " pass " + TracePassIndex);
                hasLoggedMissingShaderPass = true;
            }

            return false;
        }
    }

    internal sealed class BurtDebugScreenSpaceShadowPass : BurtRenderPass
    {
        private const string ScreenSpaceShadowShaderName = "Hidden/BurtRP/ScreenSpaceShadow";
        private const int DebugPassIndex = 1;

        private static readonly int GBuffer0Id = BurtRenderGraphResourceRegistry.GBuffer0Id;
        private static readonly int GBuffer1Id = BurtRenderGraphResourceRegistry.GBuffer1Id;
        private static readonly int GBuffer2Id = BurtRenderGraphResourceRegistry.GBuffer2Id;
        private static readonly int GBuffer3Id = BurtRenderGraphResourceRegistry.GBuffer3Id;
        private static readonly int GBuffer4Id = BurtRenderGraphResourceRegistry.GBuffer4Id;
        private static readonly int GBuffer5Id = BurtRenderGraphResourceRegistry.GBuffer5Id;
        private static readonly int ScreenSpaceShadowTextureId = BurtRenderGraphResourceRegistry.ScreenSpaceShadowTextureId;
        private static readonly int DebugModeId = Shader.PropertyToID("_BurtSSShadowDebugMode");

        private Material screenSpaceShadowMaterial;
        private bool hasLoggedMissingShader;
        private bool hasLoggedMissingShaderPass;

        public override string Name => "Burt Debug Screen Space Shadow";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.FullScreen;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceShadowPassUtility.ShouldUseScreenSpaceShadowDebugView(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceShadow();
            if (BurtScreenSpaceShadowPassUtility.ShouldUseScreenSpaceShadowFinalMultiplierDebugView())
            {
                builder.ReadGBuffer0();
                builder.ReadGBuffer1();
                builder.ReadGBuffer2();
                builder.ReadGBuffer3();
                builder.ReadGBuffer4();
                builder.ReadGBuffer5();
            }

            builder.WriteCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceShadowPassUtility.ShouldUseScreenSpaceShadowDebugView(context.Request, context.Asset))
            {
                return;
            }

            var cameraColorTarget = context.CameraColorTarget;
            var shadowTarget = context.ScreenSpaceShadowTarget;
            if (!cameraColorTarget.IsValid || !shadowTarget.IsValid)
            {
                return;
            }

            var requiresGBuffer = BurtScreenSpaceShadowPassUtility.ShouldUseScreenSpaceShadowFinalMultiplierDebugView();
            var gbuffer0Target = context.GBuffer0Target;
            var gbuffer1Target = context.GBuffer1Target;
            var gbuffer2Target = context.GBuffer2Target;
            var gbuffer3Target = context.GBuffer3Target;
            var gbuffer4Target = context.GBuffer4Target;
            var gbuffer5Target = context.GBuffer5Target;
            if (requiresGBuffer &&
                (!gbuffer0Target.IsValid ||
                    !gbuffer1Target.IsValid ||
                    !gbuffer2Target.IsValid ||
                    !gbuffer3Target.IsValid ||
                    !gbuffer4Target.IsValid ||
                    !gbuffer5Target.IsValid))
            {
                return;
            }

            var material = GetScreenSpaceShadowMaterial();
            if (material == null || !HasRequiredShaderPass(material))
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            var camera = context.Request != null ? context.Request.Camera : null;
            cmd.SetRenderTarget(cameraColorTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            cmd.SetGlobalTexture(ScreenSpaceShadowTextureId, shadowTarget.Identifier);
            cmd.SetGlobalInt(DebugModeId, BurtScreenSpaceShadowPassUtility.ResolveScreenSpaceShadowShaderDebugMode());
            if (requiresGBuffer)
            {
                cmd.SetGlobalTexture(GBuffer0Id, gbuffer0Target.Identifier);
                cmd.SetGlobalTexture(GBuffer1Id, gbuffer1Target.Identifier);
                cmd.SetGlobalTexture(GBuffer2Id, gbuffer2Target.Identifier);
                cmd.SetGlobalTexture(GBuffer3Id, gbuffer3Target.Identifier);
                cmd.SetGlobalTexture(GBuffer4Id, gbuffer4Target.Identifier);
                cmd.SetGlobalTexture(GBuffer5Id, gbuffer5Target.Identifier);
            }

            cmd.DrawProcedural(Matrix4x4.identity, material, DebugPassIndex, MeshTopology.Triangles, 3, 1);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private Material GetScreenSpaceShadowMaterial()
        {
            if (screenSpaceShadowMaterial != null)
            {
                return screenSpaceShadowMaterial;
            }

            var shader = Shader.Find(ScreenSpaceShadowShaderName);
            if (shader == null)
            {
                if (!hasLoggedMissingShader)
                {
                    Debug.LogWarning("BurtRP could not find shader: " + ScreenSpaceShadowShaderName);
                    hasLoggedMissingShader = true;
                }

                return null;
            }

            screenSpaceShadowMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            return screenSpaceShadowMaterial;
        }

        private bool HasRequiredShaderPass(Material material)
        {
            if (material != null && DebugPassIndex >= 0 && DebugPassIndex < material.passCount)
            {
                return true;
            }

            if (!hasLoggedMissingShaderPass)
            {
                Debug.LogWarning("BurtRP screen-space shadow shader pass missing: " + ScreenSpaceShadowShaderName + " pass " + DebugPassIndex);
                hasLoggedMissingShaderPass = true;
            }

            return false;
        }
    }

    internal sealed class BurtReleaseScreenSpaceShadowPass : BurtRenderPass
    {
        public override string Name => "Burt Release Screen Space Shadow";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceShadowPassUtility.ShouldUseScreenSpaceShadow(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceShadow();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceShadowPassUtility.ShouldUseScreenSpaceShadow(context.Request, context.Asset))
            {
                return;
            }

            var target = context.ScreenSpaceShadowTarget;
            if (!target.IsValid)
            {
                return;
            }

            BurtScreenSpaceShadowRenderTargetUtility.Release(context, Name, BurtRenderGraphResourceRegistry.ScreenSpaceShadowTextureId);
        }
    }

    internal readonly struct BurtScreenSpaceShadowSettings
    {
        public static readonly BurtScreenSpaceShadowSettings Disabled = new BurtScreenSpaceShadowSettings(false, 1f, 32, 5f, 0.5f, 0.8f, 0.8f, 0.8f, 0.8f, 0.5f, 2f, true, 80f, 50f, false, false);

        public BurtScreenSpaceShadowSettings(
            bool enabled,
            float intensity,
            int sampleCount,
            float maxDistance,
            float depthOffset,
            float grassContrast,
            float detailContrast,
            float foliageContrast,
            float characterContrast,
            float thickness,
            float bilinearThreshold,
            bool bilinearSamplingOffset,
            float fadeDistance,
            float fadeRadius,
            bool halfResolution,
            bool quarterResolution)
        {
            Enabled = enabled;
            Intensity = Mathf.Clamp(intensity, 0f, 2f);
            SampleCount = Mathf.Clamp(sampleCount, 1, 64);
            MaxDistance = Mathf.Clamp(maxDistance, 0.01f, 50f);
            DepthOffset = Mathf.Clamp(depthOffset, 0f, 2f);
            GrassContrast = Mathf.Clamp(grassContrast, 0f, 2f);
            DetailContrast = Mathf.Clamp(detailContrast, 0f, 2f);
            FoliageContrast = Mathf.Clamp(foliageContrast, 0f, 2f);
            CharacterContrast = Mathf.Clamp(characterContrast, 0f, 2f);
            Thickness = Mathf.Clamp(thickness, 0f, 10f);
            BilinearThreshold = Mathf.Clamp(bilinearThreshold, 0f, 10f);
            BilinearSamplingOffset = bilinearSamplingOffset;
            FadeDistance = Mathf.Clamp(fadeDistance, 0.01f, 800f);
            FadeRadius = Mathf.Clamp(fadeRadius, 0.01f, 200f);
            QuarterResolution = quarterResolution;
            HalfResolution = halfResolution && !QuarterResolution;
            DownsampleFactor = QuarterResolution ? 4 : (HalfResolution ? 2 : 1);
        }

        public bool Enabled { get; }
        public float Intensity { get; }
        public int SampleCount { get; }
        public float MaxDistance { get; }
        public float DepthOffset { get; }
        public float GrassContrast { get; }
        public float DetailContrast { get; }
        public float FoliageContrast { get; }
        public float CharacterContrast { get; }
        public float Thickness { get; }
        public float BilinearThreshold { get; }
        public bool BilinearSamplingOffset { get; }
        public float FadeDistance { get; }
        public float FadeRadius { get; }
        public bool HalfResolution { get; }
        public bool QuarterResolution { get; }
        public int DownsampleFactor { get; }
    }

    internal static class BurtScreenSpaceShadowPassUtility
    {
        public static bool ShouldUseScreenSpaceShadow(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return ResolveScreenSpaceShadowSettings(request, asset).Enabled;
        }

        public static bool ShouldUseScreenSpaceShadowDebugView(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return IsScreenSpaceShadowDebugMode(BurtShadingDebugSettings.Mode) && ShouldUseScreenSpaceShadow(request, asset);
        }

        public static bool IsScreenSpaceShadowDebugMode(BurtShadingDebugMode mode)
        {
            return mode == BurtShadingDebugMode.ScreenSpaceShadow || mode == BurtShadingDebugMode.ScreenSpaceShadowFinalMultiplier;
        }

        public static bool ShouldUseScreenSpaceShadowFinalMultiplierDebugView()
        {
            return BurtShadingDebugSettings.Mode == BurtShadingDebugMode.ScreenSpaceShadowFinalMultiplier;
        }

        public static int ResolveScreenSpaceShadowShaderDebugMode()
        {
            return ShouldUseScreenSpaceShadowFinalMultiplierDebugView() ? 1 : 0;
        }

        public static string ResolveScreenSpaceShadowDebugModeLabel()
        {
            switch (BurtShadingDebugSettings.Mode)
            {
                case BurtShadingDebugMode.ScreenSpaceShadow:
                    return "ScreenSpaceShadow";
                case BurtShadingDebugMode.ScreenSpaceShadowFinalMultiplier:
                    return "ScreenSpaceShadowFinalMultiplier";
                default:
                    return "Disabled";
            }
        }

        public static BurtScreenSpaceShadowSettings ResolveScreenSpaceShadowSettings(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            if (request == null || !request.IsValid || request.Camera == null)
            {
                return BurtScreenSpaceShadowSettings.Disabled;
            }

            if (request.Type == BurtRenderRequestType.Preview || request.Type == BurtRenderRequestType.Reflection)
            {
                return BurtScreenSpaceShadowSettings.Disabled;
            }

            if (asset == null || asset.RendererMode != BurtRendererMode.Deferred)
            {
                return BurtScreenSpaceShadowSettings.Disabled;
            }

            var lightingData = request.LightingData;
            if (lightingData == null || !lightingData.HasMainLight)
            {
                return BurtScreenSpaceShadowSettings.Disabled;
            }

            var component = GetScreenSpaceShadowVolumeComponent();
            if (component == null || !component.IsEnabled())
            {
                return BurtScreenSpaceShadowSettings.Disabled;
            }

            return new BurtScreenSpaceShadowSettings(
                true,
                component.intensity.value,
                component.sampleCount.value,
                component.maxDistance.value,
                component.depthOffset.value,
                component.grassContrast.value,
                component.detailContrast.value,
                component.foliageContrast.value,
                component.characterContrast.value,
                component.thickness.value,
                component.bilinearThreshold.value,
                component.bilinearSamplingOffset.value,
                component.fadeDistance.value,
                component.fadeRadius.value,
                component.halfResolution.value,
                component.quarterResolution.value);
        }

        private static ScreenSpaceShadowVolumeComponent GetScreenSpaceShadowVolumeComponent()
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

            return stack.GetComponent<ScreenSpaceShadowVolumeComponent>();
        }
    }

    internal static class BurtScreenSpaceShadowRenderTargetUtility
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
            cmd.ClearRenderTarget(false, true, Color.white);
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
