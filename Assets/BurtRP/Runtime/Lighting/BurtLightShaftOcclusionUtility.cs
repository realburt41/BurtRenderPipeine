using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    internal readonly struct BurtLightShaftOcclusionSettings
    {
        public readonly bool Enabled;
        public readonly float MaskDarkness;
        public readonly float DepthRange;

        public BurtLightShaftOcclusionSettings(
            bool enabled,
            float maskDarkness,
            float depthRange)
        {
            Enabled = enabled;
            MaskDarkness = Mathf.Clamp01(maskDarkness);
            DepthRange = Mathf.Clamp(depthRange, 1f, 5000f);
        }

        public static BurtLightShaftOcclusionSettings Disabled =>
            new BurtLightShaftOcclusionSettings(false, 1f, 1000f);
    }

    internal static class BurtLightShaftOcclusionUtility
    {
        public const string ShaderName = "Hidden/BurtRP/LightShaftOcclusion";
        public const int BlurPassCount = 3;
        public const int BlurSampleCount = 6;
        public const float FirstPassDistance = 0.1f;

        private static readonly int LightShaftOcclusionTextureId =
            BurtRenderGraphResourceRegistry.LightShaftOcclusionTextureId;
        private static readonly int LightShaftOcclusionEnabledId =
            Shader.PropertyToID("_BurtLightShaftOcclusionEnabled");
        private static Shader supportedShader;
        private static BurtRenderRequest activeRequest;
        private static BurtRenderRequest producedRequest;

        public static BurtLightShaftOcclusionSettings ResolveSettings()
        {
            var stack = VolumeManager.instance != null ? VolumeManager.instance.stack : null;
            var component = stack != null ? stack.GetComponent<LightShaftVolumeComponent>() : null;
            if (component == null || !component.IsEnabled())
            {
                return BurtLightShaftOcclusionSettings.Disabled;
            }

            return new BurtLightShaftOcclusionSettings(
                true,
                component.occlusionMaskDarkness.value,
                component.occlusionDepthRange.value);
        }

        public static bool ShouldUseLightShaftOcclusion(BurtRenderRequest request)
        {
            if (request == null ||
                !request.IsValid ||
                request.Camera == null ||
                BurtAtmosphereUtility.IsMobileAtmospherePlatform ||
                !IsRenderPathAvailable())
            {
                return false;
            }

            if (request.Type == BurtRenderRequestType.Preview ||
                request.Type == BurtRenderRequestType.UICamera)
            {
                return false;
            }

            var settings = ResolveSettings();
            if (!settings.Enabled || !HasOpaqueFogConsumer(request))
            {
                return false;
            }

            return TryResolveTextureSpaceSunOrigin(request, out _);
        }

        public static bool IsRenderPathAvailable()
        {
            return SystemInfo.IsFormatSupported(
                    GraphicsFormat.R8_UNorm,
                    FormatUsage.Render) &&
                SystemInfo.IsFormatSupported(
                    GraphicsFormat.R8_UNorm,
                    FormatUsage.Sample) &&
                TryGetSupportedShader(out _);
        }

        internal static bool TryGetSupportedShader(out Shader shader)
        {
            if (supportedShader == null)
            {
                supportedShader = Shader.Find(ShaderName);
            }

            shader = supportedShader;
            return shader != null &&
                shader.isSupported &&
                shader.passCount >= 3;
        }

        public static bool TryResolveTextureSpaceSunOrigin(
            BurtRenderRequest request,
            out Vector2 textureSpaceOrigin)
        {
            textureSpaceOrigin = new Vector2(-1f, -1f);
            if (request == null || request.Camera == null)
            {
                return false;
            }

            var lightingData = request.LightingData;
            if (lightingData == null || !lightingData.HasMainLight)
            {
                return false;
            }

            var directionTowardLight = lightingData.MainLightDirection;
            if (directionTowardLight.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            directionTowardLight.Normalize();
            if (!IsFinite(directionTowardLight))
            {
                return false;
            }

            const float worldMax = 2097152f;
            var camera = request.Camera;
            var lightPositionWS = camera.transform.position + directionTowardLight * worldMax;
            var lightPositionSS = camera.WorldToScreenPoint(lightPositionWS);
            if (!IsFinite(lightPositionSS) || lightPositionSS.z < 0f)
            {
                return false;
            }

            textureSpaceOrigin = new Vector2(
                lightPositionSS.x / Mathf.Max(1f, camera.pixelWidth),
                lightPositionSS.y / Mathf.Max(1f, camera.pixelHeight));

            return true;
        }

        public static void BeginCameraRequest(
            CommandBuffer cmd,
            BurtRenderRequest request)
        {
            activeRequest = request;
            producedRequest = null;
            BindFallback(cmd);
        }

        internal static void MarkProduced(BurtRenderRequest request)
        {
            producedRequest = ReferenceEquals(activeRequest, request)
                ? request
                : null;
        }

        internal static bool WasProducedForRequest(BurtRenderRequest request)
        {
            return request != null &&
                ReferenceEquals(activeRequest, request) &&
                ReferenceEquals(producedRequest, request);
        }

        public static void EndCameraRequest(
            CommandBuffer cmd,
            BurtRenderRequest request)
        {
            if (ReferenceEquals(producedRequest, request))
            {
                producedRequest = null;
            }

            if (ReferenceEquals(activeRequest, request))
            {
                activeRequest = null;
            }

            BindFallback(cmd);
        }

        public static void BindForOpaqueFog(
            CommandBuffer cmd,
            BurtRenderGraphContext context)
        {
            if (cmd == null)
            {
                return;
            }

            if (context == null ||
                !ShouldUseLightShaftOcclusion(context.Request) ||
                !WasProducedForRequest(context.Request) ||
                context.ResourceRegistry == null)
            {
                BindFallback(cmd);
                return;
            }

            var target = context.ResourceRegistry.GetRenderTarget(
                BurtRenderGraphResourceRegistry.LightShaftOcclusionName);
            if (!target.IsValid)
            {
                BindFallback(cmd);
                return;
            }

            cmd.SetGlobalTexture(LightShaftOcclusionTextureId, target.Identifier);
            cmd.SetGlobalFloat(LightShaftOcclusionEnabledId, 1f);
        }

        private static void BindFallback(CommandBuffer cmd)
        {
            if (cmd == null)
            {
                return;
            }

            cmd.SetGlobalTexture(
                LightShaftOcclusionTextureId,
                Texture2D.whiteTexture);
            cmd.SetGlobalFloat(LightShaftOcclusionEnabledId, 0f);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) &&
                IsFinite(value.y) &&
                IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool HasOpaqueFogConsumer(BurtRenderRequest request)
        {
            return BurtAtmosphereUtility.ShouldUseAerialPerspective(request) ||
                BurtFogUtility.ShouldUseFog(request) ||
                BurtVolumetricFogUtility.ShouldUseVolumetricFog(request);
        }
    }
}
