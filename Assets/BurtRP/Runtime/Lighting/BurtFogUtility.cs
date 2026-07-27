using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    internal readonly struct BurtAtmosphereHorizontalFogSettings
    {
        public readonly bool Enabled;
        public readonly Color RayleighTint;
        public readonly float RayleighScale;
        public readonly Color MieTint;
        public readonly float MieScale;
        public readonly Color MultipleScatteringTint;
        public readonly float MultipleScatteringScale;

        public BurtAtmosphereHorizontalFogSettings(
            bool enabled,
            Color rayleighTint,
            float rayleighScale,
            Color mieTint,
            float mieScale,
            Color multipleScatteringTint,
            float multipleScatteringScale)
        {
            Enabled = enabled;
            RayleighTint = rayleighTint;
            RayleighScale = Mathf.Clamp(rayleighScale, 0f, 10f);
            MieTint = mieTint;
            MieScale = Mathf.Clamp(mieScale, 0f, 10f);
            MultipleScatteringTint = multipleScatteringTint;
            MultipleScatteringScale = Mathf.Clamp(multipleScatteringScale, 0f, 10f);
        }

        public static BurtAtmosphereHorizontalFogSettings Disabled => new BurtAtmosphereHorizontalFogSettings(
            false, Color.white, 1f, Color.white, 1f, Color.white, 1f);
    }

    public readonly struct BurtFogDebugSnapshot
    {
        public readonly bool Enabled;
        public readonly float Height;
        public readonly float Density;
        public readonly float HeightFalloff;
        public readonly float StartDistance;
        public readonly float CutoffDistance;
        public readonly float MaxOpacity;
        public readonly Color Albedo;
        public readonly float DirectionalIntensity;
        public readonly float AmbientIntensity;
        public readonly float Anisotropy;
        public readonly string Formula;

        public BurtFogDebugSnapshot(
            bool enabled,
            float height,
            float density,
            float heightFalloff,
            float startDistance,
            float cutoffDistance,
            float maxOpacity,
            Color albedo,
            float directionalIntensity,
            float ambientIntensity,
            float anisotropy,
            string formula)
        {
            Enabled = enabled;
            Height = height;
            Density = density;
            HeightFalloff = heightFalloff;
            StartDistance = startDistance;
            CutoffDistance = cutoffDistance;
            MaxOpacity = maxOpacity;
            Albedo = albedo;
            DirectionalIntensity = directionalIntensity;
            AmbientIntensity = ambientIntensity;
            Anisotropy = anisotropy;
            Formula = string.IsNullOrEmpty(formula) ? "XRenderGlobalHeightFogLite" : formula;
        }
    }

    public static class BurtFogDebugUtility
    {
        public static BurtFogDebugSnapshot GetSnapshot()
        {
            var settings = BurtFogUtility.ResolveSettings();
            return new BurtFogDebugSnapshot(
                settings.Enabled,
                settings.Height,
                settings.Density,
                settings.HeightFalloff,
                settings.StartDistance,
                settings.CutoffDistance,
                settings.MaxOpacity,
                settings.Albedo,
                settings.DirectionalIntensity,
                settings.AmbientIntensity,
                settings.Anisotropy,
                BurtFogUtility.FormulaName);
        }

        public static string FormatDebugState()
        {
            return BurtFogUtility.FormatDebugState();
        }
    }

    internal readonly struct BurtFogSettings
    {
        public readonly bool Enabled;
        public readonly float Height;
        public readonly float Density;
        public readonly float HeightFalloff;
        public readonly float StartDistance;
        public readonly float CutoffDistance;
        public readonly float MaxOpacity;
        public readonly Color Albedo;
        public readonly float DirectionalIntensity;
        public readonly float AmbientIntensity;
        public readonly float Anisotropy;
        public readonly AtmosphereFogInteraction RequestedAerialInteraction;
        public readonly AtmosphereFogInteraction AerialInteraction;
        public readonly float AerialFadeStart;
        public readonly float AerialFadeEnd;
        public readonly BurtAtmosphereHorizontalFogSettings HorizontalScattering;

        public BurtFogSettings(
            bool enabled,
            float height,
            float density,
            float heightFalloff,
            float startDistance,
            float cutoffDistance,
            float maxOpacity,
            Color albedo,
            float directionalIntensity,
            float ambientIntensity,
            float anisotropy,
            AtmosphereFogInteraction requestedAerialInteraction,
            AtmosphereFogInteraction aerialInteraction,
            float aerialFadeStart,
            float aerialFadeEnd,
            BurtAtmosphereHorizontalFogSettings horizontalScattering)
        {
            Enabled = enabled && aerialInteraction != AtmosphereFogInteraction.AerialOnly && density > 0.000001f && maxOpacity > 0.000001f;
            Height = height;
            Density = Mathf.Clamp(density, 0f, 0.5f);
            HeightFalloff = Mathf.Clamp(heightFalloff, 0.001f, 4f);
            StartDistance = Mathf.Max(0f, startDistance);
            CutoffDistance = Mathf.Max(0f, cutoffDistance);
            MaxOpacity = Mathf.Clamp01(maxOpacity);
            Albedo = albedo;
            DirectionalIntensity = Mathf.Max(0f, directionalIntensity);
            AmbientIntensity = Mathf.Max(0f, ambientIntensity);
            Anisotropy = Mathf.Clamp(anisotropy, -0.9f, 0.9f);
            RequestedAerialInteraction = requestedAerialInteraction;
            AerialInteraction = aerialInteraction;
            AerialFadeStart = Mathf.Max(0f, aerialFadeStart);
            AerialFadeEnd = Mathf.Max(AerialFadeStart + 0.001f, aerialFadeEnd);
            HorizontalScattering = horizontalScattering;
        }

        public static BurtFogSettings Disabled => new BurtFogSettings(false, 0f, 0f, 0.2f, 0f, 0f, 0f, Color.white, 0f, 0f, 0f, AtmosphereFogInteraction.Additive, AtmosphereFogInteraction.Additive, 0f, 1f, BurtAtmosphereHorizontalFogSettings.Disabled);
    }

    internal static class BurtFogUtility
    {
        private static readonly int TransparentHeightFogEnabledId = Shader.PropertyToID("_BurtTransparentHeightFogEnabled");
        private static readonly int TransparentHeightFogParamsId = Shader.PropertyToID("_BurtTransparentHeightFogParams");
        private static readonly int TransparentHeightFogDistanceParamsId = Shader.PropertyToID("_BurtTransparentHeightFogDistanceParams");
        private static readonly int TransparentHeightFogAerialParamsId = Shader.PropertyToID("_BurtTransparentHeightFogAerialParams");
        private static readonly int TransparentHeightFogAlbedoId = Shader.PropertyToID("_BurtTransparentHeightFogAlbedo");
        private static readonly int TransparentHeightFogScatteringParamsId = Shader.PropertyToID("_BurtTransparentHeightFogScatteringParams");
        private static readonly int TransparentHeightFogRayleighTintScaleId = Shader.PropertyToID("_BurtTransparentHeightFogRayleighTintScale");
        private static readonly int TransparentHeightFogMieTintScaleId = Shader.PropertyToID("_BurtTransparentHeightFogMieTintScale");
        private static readonly int TransparentHeightFogMultipleScatteringTintScaleId = Shader.PropertyToID("_BurtTransparentHeightFogMultipleScatteringTintScale");
        private static readonly int TransparentHeightFogMainLightDirectionId = Shader.PropertyToID("_BurtTransparentHeightFogMainLightDirection");
        private static readonly int TransparentHeightFogLegacyLightColorId = Shader.PropertyToID("_BurtTransparentHeightFogLegacyLightColor");
        private static readonly int TransparentHeightFogHorizontalSunDirectionId = Shader.PropertyToID("_BurtTransparentHeightFogHorizontalSunDirection");
        private static readonly int TransparentHeightFogHorizontalLightColorId = Shader.PropertyToID("_BurtTransparentHeightFogHorizontalLightColor");
        private static readonly int TransparentHeightFogMainLightOcclusionId = Shader.PropertyToID("_BurtTransparentHeightFogMainLightOcclusion");

        public const string ShaderName = "Hidden/BurtRP/Fog";
        public const string FormulaName = "XRenderGlobalHeightFogLiteTransparentV2";

        public static bool ShouldUseFog(BurtRenderRequest request)
        {
            if (request == null || !request.IsValid || request.Camera == null)
            {
                return false;
            }

            if (request.Type == BurtRenderRequestType.Preview ||
                request.Type == BurtRenderRequestType.Reflection ||
                request.Type == BurtRenderRequestType.UICamera)
            {
                return false;
            }

            return ResolveSettings(request).Enabled;
        }

        public static BurtFogSettings ResolveSettings()
        {
            return ResolveSettings(null);
        }

        public static BurtFogSettings ResolveSettings(BurtRenderRequest request)
        {
            var stack = VolumeManager.instance != null ? VolumeManager.instance.stack : null;
            var fog = stack != null ? stack.GetComponent<FogVolumeComponent>() : null;
            if (fog == null || !fog.IsEnabled())
            {
                return BurtFogSettings.Disabled;
            }

            var atmosphereSettings = BurtAtmosphereUtility.ResolveSettings();
            var requestedInteraction = atmosphereSettings.FogInteraction;
            var interaction = requestedInteraction;
            var aerialCanRun = request == null ? atmosphereSettings.AerialPerspectiveEnabled : BurtAtmosphereUtility.ShouldUseAerialPerspective(request);
            if (!aerialCanRun && interaction != AtmosphereFogInteraction.AerialOnly)
            {
                interaction = AtmosphereFogInteraction.Additive;
            }

            var aerialFadeStart = aerialCanRun ? atmosphereSettings.AerialPerspectiveNearFadeStart : 0f;
            var aerialFadeEnd = aerialCanRun ? atmosphereSettings.AerialPerspectiveNearFadeEnd : 1f;

            return new BurtFogSettings(
                true,
                fog.height.value,
                fog.density.value,
                fog.heightFalloff.value,
                fog.startDistance.value,
                fog.cutoffDistance.value,
                fog.maxOpacity.value,
                fog.albedo.value,
                fog.directionalIntensity.value,
                fog.ambientIntensity.value,
                fog.anisotropy.value,
                requestedInteraction,
                interaction,
                aerialFadeStart,
                aerialFadeEnd,
                new BurtAtmosphereHorizontalFogSettings(
                    fog.useAtmosphereHorizontalScattering.value,
                    fog.atmosphereRayleighTint.value,
                    fog.atmosphereRayleighScale.value,
                    fog.atmosphereMieTint.value,
                    fog.atmosphereMieScale.value,
                    fog.atmosphereMultipleScatteringTint.value,
                    fog.atmosphereMultipleScatteringScale.value));
        }

        public static void UploadTransparentGlobals(CommandBuffer cmd, BurtRenderRequest request)
        {
            if (cmd == null)
            {
                return;
            }

            var settings = ResolveSettings(request);
            var enabled = ShouldUseFog(request) && settings.Enabled;
            cmd.SetGlobalFloat(TransparentHeightFogEnabledId, enabled ? 1f : 0f);
            if (!enabled)
            {
                return;
            }

            cmd.SetGlobalVector(TransparentHeightFogParamsId, new Vector4(
                settings.Height,
                settings.Density,
                settings.HeightFalloff,
                settings.MaxOpacity));
            cmd.SetGlobalVector(TransparentHeightFogDistanceParamsId, new Vector4(
                settings.StartDistance,
                settings.CutoffDistance,
                0f,
                0f));
            cmd.SetGlobalVector(TransparentHeightFogAerialParamsId, new Vector4(
                (float)settings.AerialInteraction,
                settings.AerialFadeStart,
                settings.AerialFadeEnd,
                0f));
            cmd.SetGlobalColor(TransparentHeightFogAlbedoId, settings.Albedo);
            cmd.SetGlobalVector(TransparentHeightFogScatteringParamsId, new Vector4(
                settings.DirectionalIntensity,
                settings.AmbientIntensity,
                settings.Anisotropy,
                settings.HorizontalScattering.Enabled ? 1f : 0f));
            cmd.SetGlobalVector(TransparentHeightFogRayleighTintScaleId, ToTintScaleVector(
                settings.HorizontalScattering.RayleighTint,
                settings.HorizontalScattering.RayleighScale));
            cmd.SetGlobalVector(TransparentHeightFogMieTintScaleId, ToTintScaleVector(
                settings.HorizontalScattering.MieTint,
                settings.HorizontalScattering.MieScale));
            cmd.SetGlobalVector(TransparentHeightFogMultipleScatteringTintScaleId, ToTintScaleVector(
                settings.HorizontalScattering.MultipleScatteringTint,
                settings.HorizontalScattering.MultipleScatteringScale));

            var lightingData = request != null ? request.LightingData : null;
            var mainLightDirection = lightingData != null ? lightingData.MainLightDirection : Vector3.up;
            if (mainLightDirection.sqrMagnitude <= 0.0001f)
            {
                mainLightDirection = Vector3.up;
            }

            mainLightDirection.Normalize();
            var outerSpaceLight = lightingData != null ? lightingData.MainLightColorOuterSpace : Color.white;
            var atmosphereTransmittance = lightingData != null ? lightingData.AtmosphereTransmittance : Color.white;
            var legacyLight = new Color(
                Mathf.Max(0f, outerSpaceLight.r * atmosphereTransmittance.r),
                Mathf.Max(0f, outerSpaceLight.g * atmosphereTransmittance.g),
                Mathf.Max(0f, outerSpaceLight.b * atmosphereTransmittance.b),
                1f);
            var atmosphereSettings = BurtAtmosphereUtility.ResolveSettings();
            var horizontalSunDirection = BurtDrawAtmospherePass.ResolveSunDirection(request, atmosphereSettings);
            var horizontalLightScale = Mathf.Max(0f, atmosphereSettings.SunIntensity);

            cmd.SetGlobalVector(TransparentHeightFogMainLightDirectionId, new Vector4(
                mainLightDirection.x,
                mainLightDirection.y,
                mainLightDirection.z,
                0f));
            cmd.SetGlobalColor(TransparentHeightFogLegacyLightColorId, legacyLight);
            cmd.SetGlobalVector(TransparentHeightFogHorizontalSunDirectionId, horizontalSunDirection);
            cmd.SetGlobalVector(TransparentHeightFogHorizontalLightColorId, new Vector4(
                Mathf.Max(0f, outerSpaceLight.r) * horizontalLightScale,
                Mathf.Max(0f, outerSpaceLight.g) * horizontalLightScale,
                Mathf.Max(0f, outerSpaceLight.b) * horizontalLightScale,
                0f));
            cmd.SetGlobalFloat(TransparentHeightFogMainLightOcclusionId, Mathf.Clamp01(atmosphereSettings.MainLightOcclusion));
        }

        public static string FormatDebugState(BurtRenderRequest request)
        {
            var settings = ResolveSettings(request);
            return string.Concat(
                "Requested=", ShouldUseFog(request),
                " Enabled=", settings.Enabled,
                " Height=", Format(settings.Height),
                " Density=", Format(settings.Density),
                " Falloff=", Format(settings.HeightFalloff),
                " Start=", Format(settings.StartDistance),
                " Cutoff=", Format(settings.CutoffDistance),
                " MaxOpacity=", Format(settings.MaxOpacity),
                " Albedo=", FormatColor(settings.Albedo),
                " Directional=", Format(settings.DirectionalIntensity),
                " Ambient=", Format(settings.AmbientIntensity),
                " Anisotropy=", Format(settings.Anisotropy),
                " AtmosphereHS=", settings.HorizontalScattering.Enabled,
                " HSRayleigh=", FormatColor(settings.HorizontalScattering.RayleighTint), "*", Format(settings.HorizontalScattering.RayleighScale),
                " HSMie=", FormatColor(settings.HorizontalScattering.MieTint), "*", Format(settings.HorizontalScattering.MieScale),
                " HSMultiple=", FormatColor(settings.HorizontalScattering.MultipleScatteringTint), "*", Format(settings.HorizontalScattering.MultipleScatteringScale),
                " AerialInteraction=", settings.RequestedAerialInteraction,
                " EffectiveAerialInteraction=", settings.AerialInteraction,
                " SuppressedByAerial=", settings.RequestedAerialInteraction == AtmosphereFogInteraction.AerialOnly,
                " AerialFadeOut=", Format(settings.AerialFadeStart), "/", Format(settings.AerialFadeEnd),
                " Transparent=True TransparentOrder=HFThenAF",
                " Formula=", FormulaName);
        }

        public static string FormatDebugState()
        {
            var settings = ResolveSettings();
            return string.Concat(
                "Enabled=", settings.Enabled,
                " Height=", Format(settings.Height),
                " Density=", Format(settings.Density),
                " Falloff=", Format(settings.HeightFalloff),
                " Start=", Format(settings.StartDistance),
                " Cutoff=", Format(settings.CutoffDistance),
                " MaxOpacity=", Format(settings.MaxOpacity),
                " Albedo=", FormatColor(settings.Albedo),
                " Directional=", Format(settings.DirectionalIntensity),
                " Ambient=", Format(settings.AmbientIntensity),
                " Anisotropy=", Format(settings.Anisotropy),
                " AtmosphereHS=", settings.HorizontalScattering.Enabled,
                " HSRayleigh=", FormatColor(settings.HorizontalScattering.RayleighTint), "*", Format(settings.HorizontalScattering.RayleighScale),
                " HSMie=", FormatColor(settings.HorizontalScattering.MieTint), "*", Format(settings.HorizontalScattering.MieScale),
                " HSMultiple=", FormatColor(settings.HorizontalScattering.MultipleScatteringTint), "*", Format(settings.HorizontalScattering.MultipleScatteringScale),
                " AerialInteraction=", settings.RequestedAerialInteraction,
                " EffectiveAerialInteraction=", settings.AerialInteraction,
                " SuppressedByAerial=", settings.RequestedAerialInteraction == AtmosphereFogInteraction.AerialOnly,
                " AerialFadeOut=", Format(settings.AerialFadeStart), "/", Format(settings.AerialFadeEnd),
                " Transparent=True TransparentOrder=HFThenAF",
                " Formula=", FormulaName);
        }

        private static Vector4 ToTintScaleVector(Color tint, float scale)
        {
            var linearTint = tint.linear;
            var safeScale = Mathf.Max(0f, scale);
            return new Vector4(
                linearTint.r * safeScale,
                linearTint.g * safeScale,
                linearTint.b * safeScale,
                safeScale);
        }

        private static string Format(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string FormatColor(Color color)
        {
            return string.Concat(
                "(",
                Format(color.r),
                ",",
                Format(color.g),
                ",",
                Format(color.b),
                ")");
        }
    }
}
