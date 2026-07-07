using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
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
            float aerialFadeEnd)
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
        }

        public static BurtFogSettings Disabled => new BurtFogSettings(false, 0f, 0f, 0.2f, 0f, 0f, 0f, Color.white, 0f, 0f, 0f, AtmosphereFogInteraction.Additive, AtmosphereFogInteraction.Additive, 0f, 1f);
    }

    internal static class BurtFogUtility
    {
        public const string ShaderName = "Hidden/BurtRP/Fog";
        public const string FormulaName = "XRenderGlobalHeightFogLite";

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
                aerialFadeEnd);
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
                " AerialInteraction=", settings.RequestedAerialInteraction,
                " EffectiveAerialInteraction=", settings.AerialInteraction,
                " SuppressedByAerial=", settings.RequestedAerialInteraction == AtmosphereFogInteraction.AerialOnly,
                " AerialFadeOut=", Format(settings.AerialFadeStart), "/", Format(settings.AerialFadeEnd),
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
                " AerialInteraction=", settings.RequestedAerialInteraction,
                " EffectiveAerialInteraction=", settings.AerialInteraction,
                " SuppressedByAerial=", settings.RequestedAerialInteraction == AtmosphereFogInteraction.AerialOnly,
                " AerialFadeOut=", Format(settings.AerialFadeStart), "/", Format(settings.AerialFadeEnd),
                " Formula=", FormulaName);
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
