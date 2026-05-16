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
        private static readonly int GBuffer1Id = BurtRenderGraphResourceRegistry.GBuffer1Id;
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
            builder.ReadGBuffer1();
            builder.WriteScreenSpaceAmbientOcclusionRaw();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!TryGetTargets(context, out var cameraDepthTarget, out var gbuffer1Target, out var aoRawTarget))
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
            var cmd = CommandBufferPool.Get(Name);
            cmd.SetGlobalTexture(CameraDepthTextureId, cameraDepthTarget.Identifier);
            cmd.SetGlobalTexture(GBuffer1Id, gbuffer1Target.Identifier);
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
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
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
            out BurtRenderTargetHandle gbuffer1Target,
            out BurtRenderTargetHandle aoRawTarget)
        {
            cameraDepthTarget = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            gbuffer1Target = context != null ? context.GBuffer1Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer1Name);
            aoRawTarget = context != null ? context.ScreenSpaceAmbientOcclusionRawTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionRawName);
            return cameraDepthTarget.IsValid && gbuffer1Target.IsValid && aoRawTarget.IsValid;
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
        private static readonly int CameraDepthTextureId = BurtRenderGraphResourceRegistry.CameraDepthTextureId;
        private static readonly int GBuffer1Id = BurtRenderGraphResourceRegistry.GBuffer1Id;
        private static readonly int AORawTextureId = BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionRawTextureId;
        private static readonly int AOTextureId = BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionTextureId;
        private static readonly int AOBlurTempTextureId = Shader.PropertyToID("_BurtScreenSpaceAmbientOcclusionBlurTempTexture");
        private static readonly int AOBlurSourceTextureId = Shader.PropertyToID("_BurtSSAOBlurSourceTexture");
        private static readonly int AOBlurDirectionId = Shader.PropertyToID("_BurtSSAOBlurDirection");
        private static readonly int DeferredScreenSizeId = Shader.PropertyToID("_BurtDeferredScreenSize");
        private static readonly int SSAOParams0Id = Shader.PropertyToID("_BurtSSAOParams0");
        private static readonly int SSAOParams1Id = Shader.PropertyToID("_BurtSSAOParams1");
        private static readonly int SSAOParams2Id = Shader.PropertyToID("_BurtSSAOParams2");
        private static readonly int SSAOParams3Id = Shader.PropertyToID("_BurtSSAOParams3");
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
            builder.ReadGBuffer1();
            builder.WriteScreenSpaceAmbientOcclusion();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!TryGetTargets(context, out var cameraDepthTarget, out var gbuffer1Target, out var aoRawTarget, out var aoTarget))
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
            var cmd = CommandBufferPool.Get(Name);
            cmd.SetGlobalTexture(CameraDepthTextureId, cameraDepthTarget.Identifier);
            cmd.SetGlobalTexture(GBuffer1Id, gbuffer1Target.Identifier);
            cmd.SetGlobalTexture(AORawTextureId, aoRawTarget.Identifier);
            cmd.SetGlobalVector(DeferredScreenSizeId, new Vector4(descriptor.width, descriptor.height, 1f / descriptor.width, 1f / descriptor.height));
            cmd.SetGlobalVector(SSAOParams0Id, new Vector4(settings.Radius, settings.Intensity, settings.SampleCount, settings.Bias));
            cmd.SetGlobalVector(SSAOParams1Id, new Vector4(settings.Power, settings.Blur ? 1f : 0f, Time.frameCount & 1023, 0f));
            cmd.SetGlobalVector(SSAOParams2Id, new Vector4(settings.FadeDistance, settings.FadeRadius, settings.Thickness, 1f));
            cmd.SetGlobalVector(SSAOParams3Id, new Vector4(settings.HorizonSearch ? 1f : 0f, settings.DirectionCount, settings.BlurSharpness, settings.SpatialDenoise ? 1f : 0f));
            if (settings.Blur)
            {
                cmd.GetTemporaryRT(AOBlurTempTextureId, descriptor, FilterMode.Bilinear);
                DrawBlur(cmd, material, aoRawTarget.Identifier, blurTempIdentifier, camera, Vector2.right, false);
                DrawBlur(cmd, material, blurTempIdentifier, aoTarget.Identifier, camera, Vector2.up, true);
                cmd.ReleaseTemporaryRT(AOBlurTempTextureId);
            }
            else
            {
                DrawBlur(cmd, material, aoRawTarget.Identifier, aoTarget.Identifier, camera, Vector2.zero, true);
            }

            cmd.SetGlobalTexture(AOTextureId, aoTarget.Identifier);
            cmd.SetGlobalFloat(SSAOEnabledId, 1f);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
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
            cmd.DrawProcedural(Matrix4x4.identity, material, 1, MeshTopology.Triangles, 3, 1);
        }

        private static bool TryGetTargets(
            BurtRenderGraphContext context,
            out BurtRenderTargetHandle cameraDepthTarget,
            out BurtRenderTargetHandle gbuffer1Target,
            out BurtRenderTargetHandle aoRawTarget,
            out BurtRenderTargetHandle aoTarget)
        {
            cameraDepthTarget = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            gbuffer1Target = context != null ? context.GBuffer1Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer1Name);
            aoRawTarget = context != null ? context.ScreenSpaceAmbientOcclusionRawTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionRawName);
            aoTarget = context != null ? context.ScreenSpaceAmbientOcclusionTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionName);
            return cameraDepthTarget.IsValid && gbuffer1Target.IsValid && aoRawTarget.IsValid && aoTarget.IsValid;
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
        private static readonly int AORawTextureId = BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionRawTextureId;
        private static readonly int AOTextureId = BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionTextureId;
        private static readonly int DebugCameraColorTextureId = Shader.PropertyToID("_BurtSSAODebugCameraColorTexture");
        private static readonly int DebugCameraColorCopyTextureId = Shader.PropertyToID("_BurtSSAODebugCameraColorCopyTexture");
        private static readonly int SSAODebugModeId = Shader.PropertyToID("_BurtSSAODebugMode");

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
            var aoRawTarget = context.ScreenSpaceAmbientOcclusionRawTarget;
            var aoTarget = context.ScreenSpaceAmbientOcclusionTarget;
            if (!cameraColorTarget.IsValid || !aoRawTarget.IsValid || !aoTarget.IsValid)
            {
                return;
            }

            var material = GetScreenSpaceAmbientOcclusionMaterial();
            if (material == null)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
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
            cmd.SetGlobalFloat(SSAODebugModeId, BurtScreenSpaceAmbientOcclusionPassUtility.ResolveScreenSpaceAmbientOcclusionShaderDebugMode());
            cmd.DrawProcedural(Matrix4x4.identity, material, BurtScreenSpaceAmbientOcclusionPassUtility.ResolveScreenSpaceAmbientOcclusionDebugPassIndex(), MeshTopology.Triangles, 3, 1);
            if (isOverlay)
            {
                cmd.ReleaseTemporaryRT(DebugCameraColorCopyTextureId);
            }

            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
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
        public static readonly BurtScreenSpaceAmbientOcclusionSettings Disabled = new BurtScreenSpaceAmbientOcclusionSettings(false, 0.5f, 2f, 16, true, 2, 0.003f, 2f, true, true, true, 0.12f, 0.5f, 50f, 80f);
        public static readonly BurtScreenSpaceAmbientOcclusionSettings DebugDefault = new BurtScreenSpaceAmbientOcclusionSettings(true, 0.5f, 2f, 16, true, 2, 0.003f, 2f, true, true, true, 0.12f, 0.5f, 50f, 80f);

        public BurtScreenSpaceAmbientOcclusionSettings(
            bool enabled,
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
            float blurSharpness,
            float thickness,
            float fadeRadius,
            float fadeDistance)
        {
            Enabled = enabled;
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
            BlurSharpness = Mathf.Clamp01(blurSharpness);
            Thickness = Mathf.Clamp01(thickness);
            FadeRadius = Mathf.Clamp(fadeRadius, 0.01f, 200f);
            FadeDistance = Mathf.Clamp(fadeDistance, 0.01f, 800f);
        }

        public bool Enabled { get; }

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

        public float BlurSharpness { get; }

        public float Thickness { get; }

        public float FadeRadius { get; }

        public float FadeDistance { get; }
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
                IsScreenSpaceAmbientOcclusionOverlayDebugMode(mode);
        }

        public static bool IsScreenSpaceAmbientOcclusionOverlayDebugMode(BurtShadingDebugMode mode)
        {
            return mode == BurtShadingDebugMode.ScreenSpaceAmbientOcclusionOverlay;
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
                default:
                    return 0;
            }
        }

        public static int ResolveScreenSpaceAmbientOcclusionDebugPassIndex()
        {
            return IsScreenSpaceAmbientOcclusionOverlayDebugMode(BurtShadingDebugSettings.Mode) ? 3 : 2;
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
                return BurtScreenSpaceAmbientOcclusionSettings.Disabled;
            }

            if (asset == null || asset.RendererMode != BurtRendererMode.Deferred)
            {
                return BurtScreenSpaceAmbientOcclusionSettings.Disabled;
            }

            var debugRequested = IsScreenSpaceAmbientOcclusionDebugMode(BurtShadingDebugSettings.Mode);
            var component = GetScreenSpaceAmbientOcclusionVolumeComponent();
            if (component == null || (debugRequested && !component.IsEnabled()))
            {
                return debugRequested ? BurtScreenSpaceAmbientOcclusionSettings.DebugDefault : BurtScreenSpaceAmbientOcclusionSettings.Disabled;
            }

            if (!component.IsEnabled() && !debugRequested)
            {
                return BurtScreenSpaceAmbientOcclusionSettings.Disabled;
            }

            return new BurtScreenSpaceAmbientOcclusionSettings(
                true,
                component.intensity.value,
                component.radius.value,
                component.sampleCount.value,
                component.horizonSearch.value,
                component.directionCount.value,
                component.bias.value,
                component.power.value,
                component.halfResolution.value,
                component.blur.value,
                component.spatialDenoise.value,
                component.blurSharpness.value,
                component.thickness.value,
                component.fadeRadius.value,
                component.fadeDistance.value);
        }

        private static BurtScreenSpaceAmbientOcclusionVolumeComponent GetScreenSpaceAmbientOcclusionVolumeComponent()
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

            return stack.GetComponent<BurtScreenSpaceAmbientOcclusionVolumeComponent>();
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
