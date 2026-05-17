using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    public readonly struct BurtVolumetricFogDebugSnapshot
    {
        public readonly bool Enabled;
        public readonly float VisibleDistance;
        public readonly float StartDistance;
        public readonly int StepCount;
        public readonly bool Jitter;
        public readonly float Height;
        public readonly float Density;
        public readonly float HeightFalloff;
        public readonly float ExtinctionScale;
        public readonly float MaxOpacity;
        public readonly Color Albedo;
        public readonly float Anisotropy;
        public readonly float DirectIntensity;
        public readonly float AmbientIntensity;
        public readonly string Formula;

        internal BurtVolumetricFogDebugSnapshot(BurtVolumetricFogSettings settings, string formula)
        {
            Enabled = settings.Enabled;
            VisibleDistance = settings.VisibleDistance;
            StartDistance = settings.StartDistance;
            StepCount = settings.StepCount;
            Jitter = settings.Jitter;
            Height = settings.Height;
            Density = settings.Density;
            HeightFalloff = settings.HeightFalloff;
            ExtinctionScale = settings.ExtinctionScale;
            MaxOpacity = settings.MaxOpacity;
            Albedo = settings.Albedo;
            Anisotropy = settings.Anisotropy;
            DirectIntensity = settings.DirectIntensity;
            AmbientIntensity = settings.AmbientIntensity;
            Formula = string.IsNullOrEmpty(formula) ? BurtVolumetricFogUtility.FormulaName : formula;
        }
    }

    public static class BurtVolumetricFogDebugUtility
    {
        public static BurtVolumetricFogDebugSnapshot GetSnapshot()
        {
            return new BurtVolumetricFogDebugSnapshot(BurtVolumetricFogUtility.ResolveSettings(), BurtVolumetricFogUtility.FormulaName);
        }

        public static string FormatDebugState()
        {
            return BurtVolumetricFogUtility.FormatDebugState();
        }
    }

    internal readonly struct BurtVolumetricFogSettings
    {
        public readonly bool Enabled;
        public readonly float VisibleDistance;
        public readonly float StartDistance;
        public readonly int StepCount;
        public readonly bool Jitter;
        public readonly float Height;
        public readonly float Density;
        public readonly float HeightFalloff;
        public readonly float ExtinctionScale;
        public readonly float MaxOpacity;
        public readonly Color Albedo;
        public readonly float Anisotropy;
        public readonly float DirectIntensity;
        public readonly float AmbientIntensity;

        public BurtVolumetricFogSettings(
            bool enabled,
            float visibleDistance,
            float startDistance,
            int stepCount,
            bool jitter,
            float height,
            float density,
            float heightFalloff,
            float extinctionScale,
            float maxOpacity,
            Color albedo,
            float anisotropy,
            float directIntensity,
            float ambientIntensity)
        {
            VisibleDistance = Mathf.Max(1f, visibleDistance);
            StartDistance = Mathf.Max(0f, startDistance);
            StepCount = Mathf.Clamp(stepCount, 4, 96);
            Jitter = jitter;
            Height = height;
            Density = Mathf.Clamp(density, 0f, 1f);
            HeightFalloff = Mathf.Clamp(heightFalloff, 0.001f, 4f);
            ExtinctionScale = Mathf.Clamp(extinctionScale, 0.01f, 10f);
            MaxOpacity = Mathf.Clamp01(maxOpacity);
            Albedo = albedo;
            Anisotropy = Mathf.Clamp(anisotropy, -0.9f, 0.9f);
            DirectIntensity = Mathf.Max(0f, directIntensity);
            AmbientIntensity = Mathf.Max(0f, ambientIntensity);
            Enabled = enabled && VisibleDistance > 0.001f && Density > 0.000001f && ExtinctionScale > 0.000001f && MaxOpacity > 0.000001f;
        }

        public static BurtVolumetricFogSettings Disabled => new BurtVolumetricFogSettings(false, 300f, 0f, 24, false, 0f, 0f, 0.15f, 1f, 0f, Color.white, 0f, 0f, 0f);
    }

    internal static class BurtVolumetricFogUtility
    {
        public const string ShaderName = "Hidden/BurtRP/VolumetricFog";
        public const string FormulaName = "ScreenSpaceRaymarchSingleScattering";

        public static bool ShouldUseVolumetricFog(BurtRenderRequest request)
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

            return ResolveSettings().Enabled;
        }

        public static BurtVolumetricFogSettings ResolveSettings()
        {
            var stack = VolumeManager.instance != null ? VolumeManager.instance.stack : null;
            var fog = stack != null ? stack.GetComponent<BurtVolumetricFogVolumeComponent>() : null;
            if (fog == null || !fog.IsEnabled())
            {
                return BurtVolumetricFogSettings.Disabled;
            }

            return new BurtVolumetricFogSettings(
                true,
                fog.visibleDistance.value,
                fog.startDistance.value,
                fog.stepCount.value,
                fog.jitter.value,
                fog.height.value,
                fog.density.value,
                fog.heightFalloff.value,
                fog.extinctionScale.value,
                fog.maxOpacity.value,
                fog.albedo.value,
                fog.anisotropy.value,
                fog.directIntensity.value,
                fog.ambientIntensity.value);
        }

        public static string FormatDebugState(BurtRenderRequest request)
        {
            var settings = ResolveSettings();
            return string.Concat(
                "Requested=", ShouldUseVolumetricFog(request),
                " Enabled=", settings.Enabled,
                " VisibleDistance=", Format(settings.VisibleDistance),
                " Start=", Format(settings.StartDistance),
                " Steps=", settings.StepCount,
                " Jitter=", settings.Jitter,
                " Height=", Format(settings.Height),
                " Density=", Format(settings.Density),
                " Falloff=", Format(settings.HeightFalloff),
                " ExtinctionScale=", Format(settings.ExtinctionScale),
                " MaxOpacity=", Format(settings.MaxOpacity),
                " Albedo=", FormatColor(settings.Albedo),
                " Anisotropy=", Format(settings.Anisotropy),
                " Direct=", Format(settings.DirectIntensity),
                " Ambient=", Format(settings.AmbientIntensity),
                " Formula=", FormulaName);
        }

        public static string FormatDebugState()
        {
            var settings = ResolveSettings();
            return string.Concat(
                "Enabled=", settings.Enabled,
                " VisibleDistance=", Format(settings.VisibleDistance),
                " Start=", Format(settings.StartDistance),
                " Steps=", settings.StepCount,
                " Jitter=", settings.Jitter,
                " Height=", Format(settings.Height),
                " Density=", Format(settings.Density),
                " Falloff=", Format(settings.HeightFalloff),
                " ExtinctionScale=", Format(settings.ExtinctionScale),
                " MaxOpacity=", Format(settings.MaxOpacity),
                " Albedo=", FormatColor(settings.Albedo),
                " Anisotropy=", Format(settings.Anisotropy),
                " Direct=", Format(settings.DirectIntensity),
                " Ambient=", Format(settings.AmbientIntensity),
                " Formula=", FormulaName);
        }

        private static string Format(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string FormatColor(Color color)
        {
            return string.Concat("(", Format(color.r), ",", Format(color.g), ",", Format(color.b), ")");
        }
    }
}
