using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    internal sealed class BurtApplyVolumetricFogPass : BurtRenderPass
    {
        private static readonly int CameraDepthTextureId = BurtRenderGraphResourceRegistry.CameraDepthTextureId;
        private static readonly int CameraColorTextureId = BurtRenderGraphResourceRegistry.CameraColorTextureId;
        private static readonly int SourceColorTextureId = Shader.PropertyToID("_BurtVolumetricFogSourceColorTexture");
        private static readonly int ParamsId = Shader.PropertyToID("_BurtVolumetricFogParams");
        private static readonly int DensityParamsId = Shader.PropertyToID("_BurtVolumetricFogDensityParams");
        private static readonly int ScatteringParamsId = Shader.PropertyToID("_BurtVolumetricFogScatteringParams");
        private static readonly int AlbedoId = Shader.PropertyToID("_BurtVolumetricFogAlbedo");
        private static readonly int InverseViewProjectionId = Shader.PropertyToID("_BurtVolumetricFogInverseViewProjection");
        private static readonly int CameraPositionWSId = Shader.PropertyToID("_BurtVolumetricFogCameraPositionWS");
        private static readonly int DebugModeId = Shader.PropertyToID("_BurtVolumetricFogDebugMode");
        private static readonly int FrameParamsId = Shader.PropertyToID("_BurtVolumetricFogFrameParams");
        private static readonly int MainLightDirectionId = Shader.PropertyToID("_BurtMainLightDirection");
        private static readonly int MainLightColorId = Shader.PropertyToID("_BurtMainLightColor");
        private static readonly int AdditionalLightBufferId = Shader.PropertyToID("_BurtAdditionalLightBuffer");
        private static readonly int AdditionalLightBufferEnabledId = Shader.PropertyToID("_BurtAdditionalLightBufferEnabled");

        private Material material;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Volumetric Fog";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.FullScreen;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtVolumetricFogUtility.ShouldUseVolumetricFog(builder.Request))
            {
                return;
            }

            builder.ReadCameraColor();
            builder.ReadCameraDepth();
            builder.ReadLightingGlobals();
            builder.WriteCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtVolumetricFogUtility.ShouldUseVolumetricFog(context.Request))
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

            var settings = BurtVolumetricFogUtility.ResolveSettings();
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
            var descriptor = BurtRenderTargetDescriptorUtility.CreateCameraColorDescriptor(camera);
            cmd.GetTemporaryRT(SourceColorTextureId, descriptor, FilterMode.Bilinear);
            cmd.Blit(cameraColorTarget.Identifier, new RenderTargetIdentifier(SourceColorTextureId));
            cmd.SetRenderTarget(cameraColorTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            cmd.SetGlobalTexture(CameraColorTextureId, new RenderTargetIdentifier(SourceColorTextureId));
            cmd.SetGlobalTexture(CameraDepthTextureId, cameraDepthTarget.Identifier);
            BindAdditionalLightBuffer(context, cmd, drawMaterial);
            cmd.DrawProcedural(Matrix4x4.identity, drawMaterial, 0, MeshTopology.Triangles, 3, 1);
            cmd.ReleaseTemporaryRT(SourceColorTextureId);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private static void UploadMaterialProperties(Material targetMaterial, Camera camera, BurtRenderRequest request, BurtVolumetricFogSettings settings)
        {
            targetMaterial.SetVector(ParamsId, new Vector4(settings.VisibleDistance, settings.StartDistance, settings.StepCount, settings.MaxOpacity));
            targetMaterial.SetVector(DensityParamsId, new Vector4(settings.Height, settings.Density, settings.HeightFalloff, settings.ExtinctionScale));
            targetMaterial.SetVector(ScatteringParamsId, new Vector4(settings.Anisotropy, settings.DirectIntensity, settings.AmbientIntensity, settings.Jitter ? 1f : 0f));
            targetMaterial.SetColor(AlbedoId, settings.Albedo);
            targetMaterial.SetMatrix(InverseViewProjectionId, ResolveInverseViewProjection(camera));
            targetMaterial.SetVector(CameraPositionWSId, camera.transform.position);
            targetMaterial.SetFloat(DebugModeId, ResolveDebugMode());
            targetMaterial.SetVector(FrameParamsId, new Vector4(Time.frameCount & 1023, 0f, 0f, 0f));

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

        private static void BindAdditionalLightBuffer(BurtRenderGraphContext context, CommandBuffer cmd, Material targetMaterial)
        {
            var additionalLightBuffer = context != null ? context.AdditionalLightBuffer : BurtRenderBufferHandle.Invalid(BurtRenderGraphResourceRegistry.AdditionalLightBufferName);
            var enabled = additionalLightBuffer.IsValid && additionalLightBuffer.HasBuffer;
            if (enabled)
            {
                cmd.SetGlobalBuffer(AdditionalLightBufferId, additionalLightBuffer.Buffer);
                if (targetMaterial != null)
                {
                    targetMaterial.SetBuffer(AdditionalLightBufferId, additionalLightBuffer.Buffer);
                }
            }

            cmd.SetGlobalFloat(AdditionalLightBufferEnabledId, enabled ? 1f : 0f);
            if (targetMaterial != null)
            {
                targetMaterial.SetFloat(AdditionalLightBufferEnabledId, enabled ? 1f : 0f);
            }
        }

        private static float ResolveDebugMode()
        {
            switch (BurtShadingDebugSettings.Mode)
            {
                case BurtShadingDebugMode.VolumetricFogScattering:
                    return 1f;
                case BurtShadingDebugMode.VolumetricFogTransmittance:
                    return 2f;
                case BurtShadingDebugMode.VolumetricFogDensity:
                    return 3f;
                case BurtShadingDebugMode.VolumetricFogDistance:
                    return 4f;
                case BurtShadingDebugMode.VolumetricFogStepCount:
                    return 5f;
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
            var volumetricScale = Mathf.Max(0f, lightingData.MainLightVolumetricScatteringIntensityScale);
            return new Vector4(color.r, color.g, color.b, volumetricScale);
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

            var shader = Shader.Find(BurtVolumetricFogUtility.ShaderName);
            if (shader == null)
            {
                if (!hasLoggedMissingShader)
                {
                    Debug.LogWarning("BurtRP could not find shader: " + BurtVolumetricFogUtility.ShaderName);
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
