using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    internal sealed class BurtApplyFogPass : BurtRenderPass
    {
        private static readonly int CameraDepthTextureId = BurtRenderGraphResourceRegistry.CameraDepthTextureId;
        private static readonly int CameraColorTextureId = BurtRenderGraphResourceRegistry.CameraColorTextureId;
        private static readonly int FogSourceColorTextureId = Shader.PropertyToID("_BurtFogSourceColorTexture");
        private static readonly int FogParamsId = Shader.PropertyToID("_BurtFogParams");
        private static readonly int FogDistanceParamsId = Shader.PropertyToID("_BurtFogDistanceParams");
        private static readonly int FogAlbedoId = Shader.PropertyToID("_BurtFogAlbedo");
        private static readonly int FogScatteringParamsId = Shader.PropertyToID("_BurtFogScatteringParams");
        private static readonly int FogAtmosphereRayleighTintScaleId = Shader.PropertyToID("_BurtFogAtmosphereRayleighTintScale");
        private static readonly int FogAtmosphereMieTintScaleId = Shader.PropertyToID("_BurtFogAtmosphereMieTintScale");
        private static readonly int FogAtmosphereMultipleScatteringTintScaleId = Shader.PropertyToID("_BurtFogAtmosphereMultipleScatteringTintScale");
        private static readonly int FogAerialInteractionParamsId = Shader.PropertyToID("_BurtFogAerialInteractionParams");
        private static readonly int FogDebugModeId = Shader.PropertyToID("_BurtFogDebugMode");
        private static readonly int InverseViewProjectionId = Shader.PropertyToID("_BurtFogInverseViewProjection");
        private static readonly int CameraPositionWSId = Shader.PropertyToID("_BurtFogCameraPositionWS");
        private static readonly int MainLightDirectionId = Shader.PropertyToID("_BurtMainLightDirection");
        private static readonly int MainLightColorId = Shader.PropertyToID("_BurtMainLightColor");

        private Material material;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Fog";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.FullScreen;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtFogUtility.ShouldUseFog(builder.Request))
            {
                return;
            }

            builder.ReadCameraColor();
            builder.ReadCameraDepth();
            if (BurtLightShaftOcclusionUtility.ShouldUseLightShaftOcclusion(builder.Request))
            {
                builder.ReadRenderTarget(BurtRenderGraphResourceRegistry.LightShaftOcclusionName);
            }
            builder.WriteCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtFogUtility.ShouldUseFog(context.Request))
            {
                return;
            }

            var cameraColorTarget = context.CameraColorTarget;
            var cameraDepthTarget = context.CameraDepthTarget;
            if (!cameraColorTarget.IsValid || !cameraDepthTarget.IsValid)
            {
                return;
            }

            var request = context.Request;
            var camera = request != null ? request.Camera : null;
            if (camera == null)
            {
                return;
            }

            var settings = BurtFogUtility.ResolveSettings(request);
            if (!settings.Enabled)
            {
                return;
            }

            var drawMaterial = GetMaterial();
            if (drawMaterial == null)
            {
                return;
            }

            UploadMaterialProperties(drawMaterial, camera, request, settings);

            var cmd = CommandBufferPool.Get(Name);
            BurtLightShaftOcclusionUtility.BindForOpaqueFog(cmd, context);
            BurtAtmosphereLutUtility.EnsureAndBindForFog(cmd, drawMaterial, camera, request);
            var descriptor = BurtRenderTargetDescriptorUtility.CreateCameraColorDescriptor(camera);
            cmd.GetTemporaryRT(FogSourceColorTextureId, descriptor, FilterMode.Bilinear);
            cmd.Blit(cameraColorTarget.Identifier, new RenderTargetIdentifier(FogSourceColorTextureId));
            cmd.SetRenderTarget(cameraColorTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            cmd.SetGlobalTexture(CameraColorTextureId, new RenderTargetIdentifier(FogSourceColorTextureId));
            cmd.SetGlobalTexture(CameraDepthTextureId, cameraDepthTarget.Identifier);
            cmd.DrawProcedural(Matrix4x4.identity, drawMaterial, 0, MeshTopology.Triangles, 3, 1);
            cmd.ReleaseTemporaryRT(FogSourceColorTextureId);
            context.ExecuteLegacyCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private static void UploadMaterialProperties(Material targetMaterial, Camera camera, BurtRenderRequest request, BurtFogSettings settings)
        {
            var effectiveStartDistance = BurtFogUtility.ResolveEffectiveStartDistance(
                request,
                camera,
                settings);
            targetMaterial.SetVector(FogParamsId, new Vector4(settings.Height, settings.Density, settings.HeightFalloff, settings.MaxOpacity));
            targetMaterial.SetVector(FogDistanceParamsId, new Vector4(effectiveStartDistance, settings.CutoffDistance, 0f, 0f));
            targetMaterial.SetColor(FogAlbedoId, settings.Albedo);
            targetMaterial.SetVector(FogScatteringParamsId, new Vector4(
                settings.DirectionalIntensity,
                settings.AmbientIntensity,
                settings.Anisotropy,
                settings.HorizontalScattering.Enabled ? 1f : 0f));
            targetMaterial.SetVector(FogAtmosphereRayleighTintScaleId, ToTintScaleVector(settings.HorizontalScattering.RayleighTint, settings.HorizontalScattering.RayleighScale));
            targetMaterial.SetVector(FogAtmosphereMieTintScaleId, ToTintScaleVector(settings.HorizontalScattering.MieTint, settings.HorizontalScattering.MieScale));
            targetMaterial.SetVector(FogAtmosphereMultipleScatteringTintScaleId, ToTintScaleVector(settings.HorizontalScattering.MultipleScatteringTint, settings.HorizontalScattering.MultipleScatteringScale));
            targetMaterial.SetVector(FogAerialInteractionParamsId, new Vector4((float)settings.AerialInteraction, settings.AerialFadeStart, settings.AerialFadeEnd, 0f));
            targetMaterial.SetFloat(FogDebugModeId, ResolveDebugMode());
            targetMaterial.SetMatrix(InverseViewProjectionId, ResolveInverseViewProjection(camera));
            targetMaterial.SetVector(CameraPositionWSId, camera.transform.position);

            var lightingData = request != null ? request.LightingData : null;
            var lightDirection = lightingData != null ? lightingData.MainLightDirection : Vector3.up;
            if (lightDirection.sqrMagnitude <= 0.0001f)
            {
                lightDirection = Vector3.up;
            }

            lightDirection.Normalize();
            targetMaterial.SetVector(MainLightDirectionId, new Vector4(lightDirection.x, lightDirection.y, lightDirection.z, 0f));
            targetMaterial.SetVector(MainLightColorId, ResolveMainLightColor(lightingData));
        }

        private static Vector4 ToTintScaleVector(Color tint, float scale)
        {
            var linearTint = tint.linear;
            var safeScale = Mathf.Max(0f, scale);
            return new Vector4(linearTint.r * safeScale, linearTint.g * safeScale, linearTint.b * safeScale, safeScale);
        }

        private static float ResolveDebugMode()
        {
            switch (BurtShadingDebugSettings.Mode)
            {
                case BurtShadingDebugMode.FogAmount:
                    return 1f;
                case BurtShadingDebugMode.FogTransmittance:
                    return 2f;
                case BurtShadingDebugMode.FogHeight:
                    return 3f;
                case BurtShadingDebugMode.FogDistance:
                    return 4f;
                default:
                    return 0f;
            }
        }

        private static Vector4 ResolveMainLightColor(BurtLightingData lightingData)
        {
            if (lightingData == null || !lightingData.HasMainLight)
            {
                return Vector4.zero;
            }

            var color = lightingData.MainLightColor;
            return new Vector4(color.r, color.g, color.b, 1f);
        }

        private static Matrix4x4 ResolveInverseViewProjection(Camera camera)
        {
            var view = camera.worldToCameraMatrix;
            var projection = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
            return (projection * view).inverse;
        }

        private Material GetMaterial()
        {
            if (material != null)
            {
                return material;
            }

            var shader = Shader.Find(BurtFogUtility.ShaderName);
            if (shader == null ||
                !shader.isSupported ||
                shader.passCount < 1)
            {
                if (!hasLoggedMissingShader)
                {
                    Debug.LogWarning(
                        "BurtRP cannot use shader: " +
                        BurtFogUtility.ShaderName);
                    hasLoggedMissingShader = true;
                }

                return null;
            }

            material = new Material(shader);
            material.hideFlags = HideFlags.HideAndDontSave;
            return material;
        }
    }
}
