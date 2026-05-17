using Sirenix.OdinInspector;
using UnityEngine;

namespace Burt.RenderPipeline
{
    [CreateAssetMenu(menuName = "Rendering/BurtRP/Subsurface Profile", fileName = "BurtSubsurfaceProfile")]
    public sealed class BurtSubsurfaceProfile : ScriptableObject
    {
        [TitleGroup("Burley Normalized")]
        [SerializeField, LabelText("Profile Name")] private string profileName = "Default Skin";

        [TitleGroup("Burley Normalized")]
        [SerializeField, LabelText("Surface Albedo")] private Color surfaceAlbedo = new Color(0.78f, 0.52f, 0.42f, 1f);

        [TitleGroup("Burley Normalized")]
        [SerializeField, LabelText("Mean Free Path Color")] private Color meanFreePathColor = new Color(1f, 0.42f, 0.24f, 1f);

        [TitleGroup("Burley Normalized")]
        [SerializeField, Min(0.01f), LabelText("Mean Free Path Distance")] private float meanFreePathDistance = 1f;

        [TitleGroup("Burley Normalized")]
        [SerializeField, Min(0.01f), LabelText("World Unit Scale")] private float worldUnitScale = 1f;

        [TitleGroup("Burley Normalized")]
        [SerializeField, LabelText("Tint")] private Color tint = Color.white;

        [TitleGroup("Burley Normalized")]
        [SerializeField, LabelText("Boundary Color Bleed")] private Color boundaryColorBleed = Color.white;

        [TitleGroup("Screen Space 5S")]
        [SerializeField, Min(0.01f), LabelText("Radius Pixels")] private float radiusPixels = 3.25f;

        [TitleGroup("Screen Space 5S")]
        [SerializeField, Min(0.0001f), LabelText("Depth Sigma")] private float depthSigma = 0.08f;

        [TitleGroup("Screen Space 5S")]
        [SerializeField, Range(0.01f, 1f), LabelText("Normal Sigma")] private float normalSigma = 0.72f;

        [TitleGroup("Screen Space 5S")]
        [SerializeField, Range(0f, 1f), LabelText("Blend")] private float blend = 0.85f;

        [TitleGroup("Screen Space 5S")]
        [SerializeField, Min(0.01f), LabelText("Distance Scale")] private float distanceScale = 2f;

        [TitleGroup("Screen Space 5S")]
        [SerializeField, Range(0f, 1f), LabelText("Boundary Bleed")] private float boundaryBleed = 0.25f;

        [TitleGroup("Screen Space 5S")]
        [SerializeField, Range(0f, 1f), LabelText("Tint Strength")] private float tintStrength = 0.35f;

        [TitleGroup("Screen Space 5S")]
        [SerializeField, Range(0f, 0.2f), LabelText("Min Strength")] private float minStrength = 0.012f;

        public string ProfileName => string.IsNullOrEmpty(profileName) ? name : profileName;

        public Color SurfaceAlbedo => Clamp01Color(surfaceAlbedo);

        public Color MeanFreePathColor => ClampPositiveColor(meanFreePathColor, 0.0001f);

        public float MeanFreePathDistance => Mathf.Max(0.01f, meanFreePathDistance);

        public float WorldUnitScale => Mathf.Max(0.01f, worldUnitScale);

        public Color Tint => ClampPositiveColor(tint, 0f);

        public Color BoundaryColorBleed => ClampPositiveColor(boundaryColorBleed, 0f);

        public float RadiusPixels => Mathf.Max(0.01f, radiusPixels);

        public float DepthSigma => Mathf.Max(0.0001f, depthSigma);

        public float NormalSigma => Mathf.Clamp(normalSigma, 0.01f, 1f);

        public float Blend => Mathf.Clamp01(blend);

        public float DistanceScale => Mathf.Max(0.01f, distanceScale);

        public float BoundaryBleed => Mathf.Clamp01(boundaryBleed);

        public float TintStrength => Mathf.Clamp01(tintStrength);

        public float MinStrength => Mathf.Clamp(minStrength, 0f, 0.2f);

        public BurtSubsurfaceProfileSettings CreateSettings()
        {
            return new BurtSubsurfaceProfileSettings(
                true,
                ProfileName,
                SurfaceAlbedo,
                MeanFreePathColor,
                MeanFreePathDistance,
                WorldUnitScale,
                Tint,
                BoundaryColorBleed,
                RadiusPixels,
                DepthSigma,
                NormalSigma,
                Blend,
                DistanceScale,
                BoundaryBleed,
                TintStrength,
                MinStrength);
        }

        private void OnValidate()
        {
            surfaceAlbedo = Clamp01Color(surfaceAlbedo);
            meanFreePathColor = ClampPositiveColor(meanFreePathColor, 0.0001f);
            meanFreePathDistance = Mathf.Max(0.01f, meanFreePathDistance);
            worldUnitScale = Mathf.Max(0.01f, worldUnitScale);
            tint = ClampPositiveColor(tint, 0f);
            boundaryColorBleed = ClampPositiveColor(boundaryColorBleed, 0f);
            radiusPixels = Mathf.Max(0.01f, radiusPixels);
            depthSigma = Mathf.Max(0.0001f, depthSigma);
            normalSigma = Mathf.Clamp(normalSigma, 0.01f, 1f);
            blend = Mathf.Clamp01(blend);
            distanceScale = Mathf.Max(0.01f, distanceScale);
            boundaryBleed = Mathf.Clamp01(boundaryBleed);
            tintStrength = Mathf.Clamp01(tintStrength);
            minStrength = Mathf.Clamp(minStrength, 0f, 0.2f);
        }

        private static Color Clamp01Color(Color value)
        {
            return new Color(
                Mathf.Clamp01(value.r),
                Mathf.Clamp01(value.g),
                Mathf.Clamp01(value.b),
                Mathf.Clamp01(value.a));
        }

        private static Color ClampPositiveColor(Color value, float minimum)
        {
            return new Color(
                Mathf.Max(minimum, value.r),
                Mathf.Max(minimum, value.g),
                Mathf.Max(minimum, value.b),
                Mathf.Max(0f, value.a));
        }
    }

    public readonly struct BurtSubsurfaceProfileSettings
    {
        public BurtSubsurfaceProfileSettings(
            bool usesProfile,
            string profileName,
            Color surfaceAlbedo,
            Color meanFreePathColor,
            float meanFreePathDistance,
            float worldUnitScale,
            Color tint,
            Color boundaryColorBleed,
            float radiusPixels,
            float depthSigma,
            float normalSigma,
            float blend,
            float distanceScale,
            float boundaryBleed,
            float tintStrength,
            float minStrength)
        {
            UsesProfile = usesProfile;
            ProfileName = string.IsNullOrEmpty(profileName) ? "<unnamed>" : profileName;
            SurfaceAlbedo = surfaceAlbedo;
            MeanFreePathColor = meanFreePathColor;
            MeanFreePathDistance = Mathf.Max(0.01f, meanFreePathDistance);
            WorldUnitScale = Mathf.Max(0.01f, worldUnitScale);
            Tint = tint;
            BoundaryColorBleed = boundaryColorBleed;
            RadiusPixels = Mathf.Max(0.01f, radiusPixels);
            DepthSigma = Mathf.Max(0.0001f, depthSigma);
            NormalSigma = Mathf.Clamp(normalSigma, 0.01f, 1f);
            Blend = Mathf.Clamp01(blend);
            DistanceScale = Mathf.Max(0.01f, distanceScale);
            BoundaryBleed = Mathf.Clamp01(boundaryBleed);
            TintStrength = Mathf.Clamp01(tintStrength);
            MinStrength = Mathf.Clamp(minStrength, 0f, 0.2f);
        }

        public bool UsesProfile { get; }

        public string ProfileName { get; }

        public Color SurfaceAlbedo { get; }

        public Color MeanFreePathColor { get; }

        public float MeanFreePathDistance { get; }

        public float WorldUnitScale { get; }

        public Color Tint { get; }

        public Color BoundaryColorBleed { get; }

        public float RadiusPixels { get; }

        public float DepthSigma { get; }

        public float NormalSigma { get; }

        public float Blend { get; }

        public float DistanceScale { get; }

        public float BoundaryBleed { get; }

        public float TintStrength { get; }

        public float MinStrength { get; }

        public float MeanFreePathScreenScale => Mathf.Clamp(MeanFreePathDistance * WorldUnitScale, 0.05f, 4f);

        public Vector4 Params => new Vector4(RadiusPixels, DepthSigma, NormalSigma, MinStrength);

        public Vector4 Params2 => new Vector4(Blend, DistanceScale, BoundaryBleed, TintStrength);

        public Vector4 SurfaceAlbedoVector => ToVector(SurfaceAlbedo);

        public Vector4 MeanFreePathVector => new Vector4(
            Mathf.Max(0.0001f, MeanFreePathColor.r),
            Mathf.Max(0.0001f, MeanFreePathColor.g),
            Mathf.Max(0.0001f, MeanFreePathColor.b),
            MeanFreePathScreenScale);

        public Vector4 TintVector => ToVector(Tint);

        public Vector4 BoundaryColorBleedVector => ToVector(BoundaryColorBleed);

        public static BurtSubsurfaceProfileSettings Default => CreateFallback(
            3.25f,
            0.08f,
            0.72f,
            0.85f,
            2f,
            0.25f,
            0.35f,
            0.012f);

        public static BurtSubsurfaceProfileSettings Resolve(
            BurtSubsurfaceProfile profile,
            float fallbackRadiusPixels,
            float fallbackDepthSigma,
            float fallbackNormalSigma,
            float fallbackBlend,
            float fallbackDistanceScale,
            float fallbackBoundaryBleed,
            float fallbackTintStrength,
            float fallbackMinStrength)
        {
            return profile != null
                ? profile.CreateSettings()
                : CreateFallback(
                    fallbackRadiusPixels,
                    fallbackDepthSigma,
                    fallbackNormalSigma,
                    fallbackBlend,
                    fallbackDistanceScale,
                    fallbackBoundaryBleed,
                    fallbackTintStrength,
                    fallbackMinStrength);
        }

        private static BurtSubsurfaceProfileSettings CreateFallback(
            float radiusPixels,
            float depthSigma,
            float normalSigma,
            float blend,
            float distanceScale,
            float boundaryBleed,
            float tintStrength,
            float minStrength)
        {
            return new BurtSubsurfaceProfileSettings(
                false,
                "InlineFallback",
                new Color(0.78f, 0.52f, 0.42f, 1f),
                new Color(1f, 0.42f, 0.24f, 1f),
                1f,
                1f,
                Color.white,
                Color.white,
                radiusPixels,
                depthSigma,
                normalSigma,
                blend,
                distanceScale,
                boundaryBleed,
                tintStrength,
                minStrength);
        }

        private static Vector4 ToVector(Color value)
        {
            return new Vector4(value.r, value.g, value.b, value.a);
        }
    }
}
