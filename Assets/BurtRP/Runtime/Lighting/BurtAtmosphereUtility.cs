using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    internal readonly struct BurtAtmosphereSettings
    {
        public readonly bool Enabled;
        public readonly float RayleighIntensity;
        public readonly float MieIntensity;
        public readonly float MieAnisotropy;
        public readonly float PlanetRadius;
        public readonly float AtmosphereHeight;
        public readonly float RayleighScaleHeight;
        public readonly float MieScaleHeight;
        public readonly Color GroundColor;
        public readonly Color SkyTint;
        public readonly float SunIntensity;
        public readonly BurtAtmosphereSunSource SunSource;
        public readonly Vector3 CustomSunDirection;
        public readonly float HorizonIntensity;
        public readonly float HorizonFalloff;
        public readonly float GroundContribution;
        public readonly float ExposureCompensation;
        public readonly float TonemapSafeSunIntensity;
        public readonly bool AerialPerspectiveEnabled;
        public readonly float AerialPerspectiveIntensity;
        public readonly float AerialPerspectiveDistance;
        public readonly float AerialPerspectiveHeightFalloff;
        public readonly Color AerialPerspectiveTint;
        public readonly float AerialPerspectiveNearFadeStart;
        public readonly float AerialPerspectiveNearFadeEnd;
        public readonly float AerialPerspectiveMaxOpacity;

        public BurtAtmosphereSettings(
            bool enabled,
            float rayleighIntensity,
            float mieIntensity,
            float mieAnisotropy,
            float planetRadius,
            float atmosphereHeight,
            float rayleighScaleHeight,
            float mieScaleHeight,
            Color groundColor,
            Color skyTint,
            float sunIntensity,
            BurtAtmosphereSunSource sunSource,
            Vector3 customSunDirection,
            float horizonIntensity,
            float horizonFalloff,
            float groundContribution,
            float exposureCompensation,
            float tonemapSafeSunIntensity,
            bool aerialPerspectiveEnabled,
            float aerialPerspectiveIntensity,
            float aerialPerspectiveDistance,
            float aerialPerspectiveHeightFalloff,
            Color aerialPerspectiveTint,
            float aerialPerspectiveNearFadeStart,
            float aerialPerspectiveNearFadeEnd,
            float aerialPerspectiveMaxOpacity)
        {
            Enabled = enabled;
            RayleighIntensity = Mathf.Max(0f, rayleighIntensity);
            MieIntensity = Mathf.Max(0f, mieIntensity);
            MieAnisotropy = Mathf.Clamp(mieAnisotropy, -0.95f, 0.95f);
            PlanetRadius = Mathf.Max(100f, planetRadius);
            AtmosphereHeight = Mathf.Max(1f, atmosphereHeight);
            RayleighScaleHeight = Mathf.Max(0.1f, rayleighScaleHeight);
            MieScaleHeight = Mathf.Max(0.1f, mieScaleHeight);
            GroundColor = groundColor;
            SkyTint = skyTint;
            SunIntensity = Mathf.Max(0f, sunIntensity);
            SunSource = sunSource;
            CustomSunDirection = customSunDirection.sqrMagnitude > 0.0001f ? customSunDirection.normalized : new Vector3(0.3f, 0.8f, 0.4f).normalized;
            HorizonIntensity = Mathf.Max(0f, horizonIntensity);
            HorizonFalloff = Mathf.Max(0.1f, horizonFalloff);
            GroundContribution = Mathf.Max(0f, groundContribution);
            ExposureCompensation = Mathf.Clamp(exposureCompensation, -8f, 8f);
            TonemapSafeSunIntensity = Mathf.Max(0.1f, tonemapSafeSunIntensity);
            AerialPerspectiveEnabled = enabled && aerialPerspectiveEnabled && aerialPerspectiveIntensity > 0.0001f && aerialPerspectiveDistance > 0.0001f;
            AerialPerspectiveIntensity = Mathf.Max(0f, aerialPerspectiveIntensity);
            AerialPerspectiveDistance = Mathf.Max(1f, aerialPerspectiveDistance);
            AerialPerspectiveHeightFalloff = Mathf.Max(0f, aerialPerspectiveHeightFalloff);
            AerialPerspectiveTint = aerialPerspectiveTint;
            AerialPerspectiveNearFadeStart = Mathf.Max(0f, aerialPerspectiveNearFadeStart);
            AerialPerspectiveNearFadeEnd = Mathf.Max(AerialPerspectiveNearFadeStart + 0.001f, aerialPerspectiveNearFadeEnd);
            AerialPerspectiveMaxOpacity = Mathf.Clamp01(aerialPerspectiveMaxOpacity);
        }

        public static BurtAtmosphereSettings Disabled => new BurtAtmosphereSettings(false, 0f, 0f, 0f, 6371f, 80f, 8f, 1.2f, Color.black, Color.white, 0f, BurtAtmosphereSunSource.MainLight, Vector3.up, 1f, 0.65f, 0.22f, 0f, 4f, false, 0f, 750f, 0f, new Color(0.70f, 0.82f, 1.0f, 1f), 0f, 50f, 0.65f);
    }

    internal static class BurtAtmosphereUtility
    {
        public const string ShaderName = "Hidden/BurtRP/AtmosphereScattering";

        public static bool ShouldUseAtmosphere(BurtRenderRequest request)
        {
            if (request == null || !request.IsValid || request.Camera == null)
            {
                return false;
            }

            if (request.Type == BurtRenderRequestType.Preview || request.Type == BurtRenderRequestType.Reflection)
            {
                return false;
            }

            if (request.Type == BurtRenderRequestType.UICamera)
            {
                return false;
            }

            if (BurtCameraClearUtility.ResolveClearMode(request) != BurtCameraClearMode.Skybox)
            {
                return false;
            }

            return ResolveSettings().Enabled;
        }

        public static bool ShouldUseAerialPerspective(BurtRenderRequest request)
        {
            if (!ShouldUseAtmosphere(request))
            {
                return false;
            }

            return ResolveSettings().AerialPerspectiveEnabled;
        }

        public static BurtAtmosphereSettings ResolveSettings()
        {
            var stack = VolumeManager.instance != null ? VolumeManager.instance.stack : null;
            var atmosphere = stack != null ? stack.GetComponent<BurtAtmosphereVolumeComponent>() : null;
            if (atmosphere == null || !atmosphere.IsEnabled())
            {
                return BurtAtmosphereSettings.Disabled;
            }

            return new BurtAtmosphereSettings(
                true,
                atmosphere.rayleighIntensity.value,
                atmosphere.mieIntensity.value,
                atmosphere.mieAnisotropy.value,
                atmosphere.planetRadius.value,
                atmosphere.atmosphereHeight.value,
                atmosphere.rayleighScaleHeight.value,
                atmosphere.mieScaleHeight.value,
                atmosphere.groundColor.value,
                atmosphere.skyTint.value,
                atmosphere.sunIntensity.value,
                atmosphere.sunSource.value,
                atmosphere.customSunDirection.value,
                atmosphere.horizonIntensity.value,
                atmosphere.horizonFalloff.value,
                atmosphere.groundContribution.value,
                atmosphere.exposureCompensation.value,
                atmosphere.tonemapSafeSunIntensity.value,
                atmosphere.aerialPerspective.value,
                atmosphere.aerialPerspectiveIntensity.value,
                atmosphere.aerialPerspectiveDistance.value,
                atmosphere.aerialPerspectiveHeightFalloff.value,
                atmosphere.aerialPerspectiveTint.value,
                atmosphere.aerialPerspectiveNearFadeStart.value,
                atmosphere.aerialPerspectiveNearFadeEnd.value,
                atmosphere.aerialPerspectiveMaxOpacity.value);
        }

        public static string FormatDebugState()
        {
            var settings = ResolveSettings();
            return string.Concat(
                "Enabled=", settings.Enabled,
                " Rayleigh=", Format(settings.RayleighIntensity),
                " Mie=", Format(settings.MieIntensity),
                " g=", Format(settings.MieAnisotropy),
                " RadiusKm=", Format(settings.PlanetRadius),
                " HeightKm=", Format(settings.AtmosphereHeight),
                " Sun=", Format(settings.SunIntensity),
                " SunSource=", settings.SunSource,
                " Horizon=", Format(settings.HorizonIntensity),
                " Ground=", Format(settings.GroundContribution),
                " ExposureEV=", Format(settings.ExposureCompensation),
                " SunClamp=", Format(settings.TonemapSafeSunIntensity),
                " Aerial=", settings.AerialPerspectiveEnabled,
                " AerialIntensity=", Format(settings.AerialPerspectiveIntensity),
                " AerialDistance=", Format(settings.AerialPerspectiveDistance),
                " AerialHeightFalloff=", Format(settings.AerialPerspectiveHeightFalloff),
                " AerialNearFade=", Format(settings.AerialPerspectiveNearFadeStart), "/", Format(settings.AerialPerspectiveNearFadeEnd),
                " AerialMaxOpacity=", Format(settings.AerialPerspectiveMaxOpacity),
                " AerialTint=", FormatColor(settings.AerialPerspectiveTint));
        }

        public static string FormatRequestGate(BurtRenderRequest request)
        {
            if (request == null)
            {
                return "Request=null";
            }

            var clearMode = request.IsValid ? BurtCameraClearUtility.ResolveClearMode(request).ToString() : "Invalid";
            return string.Concat(
                "RequestType=", request.Type,
                " ClearMode=", clearMode,
                " SkyAllowed=", ShouldUseAtmosphere(request),
                " AerialAllowed=", ShouldUseAerialPerspective(request));
        }

        public static string FormatAerialPassState(BurtRenderRequest request)
        {
            var settings = ResolveSettings();
            var requested = ShouldUseAerialPerspective(request);
            return string.Concat(
                "Requested=", requested,
                " UsesSourceCopy=", requested,
                " SourceCopy=TemporaryCameraColor",
                " AffectsSkyPixels=False",
                " Placement=AfterOpaqueBeforeSky",
                " Intensity=", Format(settings.AerialPerspectiveIntensity),
                " Distance=", Format(settings.AerialPerspectiveDistance),
                " HeightFalloff=", Format(settings.AerialPerspectiveHeightFalloff),
                " NearFade=", Format(settings.AerialPerspectiveNearFadeStart), "/", Format(settings.AerialPerspectiveNearFadeEnd),
                " MaxOpacity=", Format(settings.AerialPerspectiveMaxOpacity),
                " Tint=", FormatColor(settings.AerialPerspectiveTint));
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
                ",",
                Format(color.a),
                ")");
        }
    }
}
