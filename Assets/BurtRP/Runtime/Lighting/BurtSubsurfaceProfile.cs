using Sirenix.OdinInspector;
using System.Collections.Generic;
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
        [SerializeField, Min(0.01f), LabelText("World Unit Scale")] private float worldUnitScale = 0.1f;

        [TitleGroup("Burley Normalized")]
        [SerializeField, LabelText("Tint")] private Color tint = Color.white;

        [TitleGroup("Burley Normalized")]
        [SerializeField, LabelText("Boundary Color Bleed")] private Color boundaryColorBleed = Color.white;

        [TitleGroup("Transmission")]
        [SerializeField, Range(0.01f, 100f), LabelText("Extinction Scale")] private float extinctionScale = BurtSubsurfaceProfileSettings.DefaultExtinctionScale;

        [TitleGroup("Transmission")]
        [SerializeField, Range(0.01f, 0.99f), LabelText("Normal Scale")] private float transmissionNormalScale = BurtSubsurfaceProfileSettings.DefaultTransmissionNormalScale;

        [TitleGroup("Transmission")]
        [SerializeField, Range(0.01f, 0.99f), LabelText("Scattering Distribution")] private float scatteringDistribution = BurtSubsurfaceProfileSettings.DefaultScatteringDistribution;

        [TitleGroup("Transmission")]
        [SerializeField, Range(0.01f, 3f), LabelText("IOR")] private float ior = BurtSubsurfaceProfileSettings.DefaultIOR;

        [TitleGroup("Transmission")]
        [SerializeField, LabelText("Transmission Tint")] private Color transmissionTintColor = Color.white;

        [TitleGroup("Dual Specular")]
        [SerializeField, Range(0.01f, BurtSubsurfaceProfileSettings.MaxDualSpecularRoughness), LabelText("Lobe 0 Roughness")] private float dualSpecularRoughness0 = BurtSubsurfaceProfileSettings.DefaultDualSpecularRoughness0;

        [TitleGroup("Dual Specular")]
        [SerializeField, Range(0.01f, BurtSubsurfaceProfileSettings.MaxDualSpecularRoughness), LabelText("Lobe 1 Roughness")] private float dualSpecularRoughness1 = BurtSubsurfaceProfileSettings.DefaultDualSpecularRoughness1;

        [TitleGroup("Dual Specular")]
        [SerializeField, Range(0f, 1f), LabelText("Lobe Mix")] private float dualSpecularLobeMix = BurtSubsurfaceProfileSettings.DefaultDualSpecularLobeMix;

        [TitleGroup("Screen Space SSS")]
        [SerializeField, Min(0.01f), LabelText("Radius Pixels")] private float radiusPixels = 3.25f;

        [TitleGroup("Screen Space SSS")]
        [SerializeField, Min(0.0001f), LabelText("Depth Sigma")] private float depthSigma = 0.08f;

        [TitleGroup("Screen Space SSS")]
        [SerializeField, Range(0.01f, 1f), LabelText("Normal Sigma")] private float normalSigma = 0.72f;

        [TitleGroup("Screen Space SSS")]
        [SerializeField, Range(0f, 1f), LabelText("Blend")] private float blend = 0.85f;

        [TitleGroup("Screen Space SSS")]
        [SerializeField, Min(0.01f), LabelText("Distance Scale")] private float distanceScale = 2f;

        [TitleGroup("Screen Space SSS")]
        [SerializeField, Range(0f, 1f), LabelText("Boundary Bleed")] private float boundaryBleed = 0.25f;

        [TitleGroup("Screen Space SSS")]
        [SerializeField, Range(0f, 1f), LabelText("Tint Strength")] private float tintStrength = 0.35f;

        [TitleGroup("Screen Space SSS")]
        [SerializeField, Range(0f, 0.2f), LabelText("Min Strength")] private float minStrength = 0.012f;

        public string ProfileName => string.IsNullOrEmpty(profileName) ? name : profileName;

        public Color SurfaceAlbedo => Clamp01Color(surfaceAlbedo);

        public Color MeanFreePathColor => ClampPositiveColor(meanFreePathColor, 0.0001f);

        public float MeanFreePathDistance => Mathf.Max(0.01f, meanFreePathDistance);

        public float WorldUnitScale => Mathf.Max(0.01f, worldUnitScale);

        public Color Tint => ClampPositiveColor(tint, 0f);

        public Color BoundaryColorBleed => ClampPositiveColor(boundaryColorBleed, 0f);

        public float ExtinctionScale => Mathf.Clamp(extinctionScale, 0.01f, 100f);

        public float TransmissionNormalScale => Mathf.Clamp(transmissionNormalScale, 0.01f, 0.99f);

        public float ScatteringDistribution => Mathf.Clamp(scatteringDistribution, 0.01f, 0.99f);

        public float IOR => Mathf.Clamp(ior, 0.01f, 3f);

        public Color TransmissionTintColor => ClampPositiveColor(transmissionTintColor, 0f);

        public float DualSpecularRoughness0 => dualSpecularRoughness0 > 0f
            ? Mathf.Clamp(dualSpecularRoughness0, 0.01f, BurtSubsurfaceProfileSettings.MaxDualSpecularRoughness)
            : BurtSubsurfaceProfileSettings.DefaultDualSpecularRoughness0;

        public float DualSpecularRoughness1 => dualSpecularRoughness1 > 0f
            ? Mathf.Clamp(dualSpecularRoughness1, 0.01f, BurtSubsurfaceProfileSettings.MaxDualSpecularRoughness)
            : BurtSubsurfaceProfileSettings.DefaultDualSpecularRoughness1;

        public float DualSpecularLobeMix => HasSerializedDualSpecularValues()
            ? Mathf.Clamp01(dualSpecularLobeMix)
            : BurtSubsurfaceProfileSettings.DefaultDualSpecularLobeMix;

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
                ExtinctionScale,
                TransmissionNormalScale,
                ScatteringDistribution,
                IOR,
                TransmissionTintColor,
                DualSpecularRoughness0,
                DualSpecularRoughness1,
                DualSpecularLobeMix,
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
            extinctionScale = Mathf.Clamp(extinctionScale, 0.01f, 100f);
            transmissionNormalScale = Mathf.Clamp(transmissionNormalScale, 0.01f, 0.99f);
            scatteringDistribution = Mathf.Clamp(scatteringDistribution, 0.01f, 0.99f);
            ior = Mathf.Clamp(ior, 0.01f, 3f);
            transmissionTintColor = ClampPositiveColor(transmissionTintColor, 0f);
            ValidateDualSpecular();
            radiusPixels = Mathf.Max(0.01f, radiusPixels);
            depthSigma = Mathf.Max(0.0001f, depthSigma);
            normalSigma = Mathf.Clamp(normalSigma, 0.01f, 1f);
            blend = Mathf.Clamp01(blend);
            distanceScale = Mathf.Max(0.01f, distanceScale);
            boundaryBleed = Mathf.Clamp01(boundaryBleed);
            tintStrength = Mathf.Clamp01(tintStrength);
            minStrength = Mathf.Clamp(minStrength, 0f, 0.2f);
#if UNITY_EDITOR
            BurtSubsurfaceLutUtility.RequestEditorTextureRebuild();
#endif
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

        private bool HasSerializedDualSpecularValues()
        {
            return dualSpecularRoughness0 > 0f || dualSpecularRoughness1 > 0f;
        }

        private void ValidateDualSpecular()
        {
            if (!HasSerializedDualSpecularValues())
            {
                dualSpecularRoughness0 = BurtSubsurfaceProfileSettings.DefaultDualSpecularRoughness0;
                dualSpecularRoughness1 = BurtSubsurfaceProfileSettings.DefaultDualSpecularRoughness1;
                dualSpecularLobeMix = BurtSubsurfaceProfileSettings.DefaultDualSpecularLobeMix;
                return;
            }

            dualSpecularRoughness0 = dualSpecularRoughness0 > 0f
                ? Mathf.Clamp(dualSpecularRoughness0, 0.01f, BurtSubsurfaceProfileSettings.MaxDualSpecularRoughness)
                : BurtSubsurfaceProfileSettings.DefaultDualSpecularRoughness0;
            dualSpecularRoughness1 = dualSpecularRoughness1 > 0f
                ? Mathf.Clamp(dualSpecularRoughness1, 0.01f, BurtSubsurfaceProfileSettings.MaxDualSpecularRoughness)
                : BurtSubsurfaceProfileSettings.DefaultDualSpecularRoughness1;
            dualSpecularLobeMix = Mathf.Clamp01(dualSpecularLobeMix);
        }
    }

    public readonly struct BurtSubsurfaceProfileSettings
    {
        public const float MaxDualSpecularRoughness = 2f;
        public const float DefaultDualSpecularRoughness0 = 0.75f;
        public const float DefaultDualSpecularRoughness1 = 1.3f;
        public const float DefaultDualSpecularLobeMix = 0.85f;
        public const float DefaultExtinctionScale = 1f;
        public const float DefaultTransmissionNormalScale = 0.08f;
        public const float DefaultScatteringDistribution = 0.93f;
        public const float DefaultIOR = 1.55f;

        public BurtSubsurfaceProfileSettings(
            bool usesProfile,
            string profileName,
            Color surfaceAlbedo,
            Color meanFreePathColor,
            float meanFreePathDistance,
            float worldUnitScale,
            Color tint,
            Color boundaryColorBleed,
            float extinctionScale,
            float transmissionNormalScale,
            float scatteringDistribution,
            float ior,
            Color transmissionTintColor,
            float dualSpecularRoughness0,
            float dualSpecularRoughness1,
            float dualSpecularLobeMix,
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
            ExtinctionScale = Mathf.Clamp(extinctionScale, 0.01f, 100f);
            TransmissionNormalScale = Mathf.Clamp(transmissionNormalScale, 0.01f, 0.99f);
            ScatteringDistribution = Mathf.Clamp(scatteringDistribution, 0.01f, 0.99f);
            IOR = Mathf.Clamp(ior, 0.01f, 3f);
            TransmissionTintColor = transmissionTintColor;
            DualSpecularRoughness0 = Mathf.Clamp(dualSpecularRoughness0, 0.01f, MaxDualSpecularRoughness);
            DualSpecularRoughness1 = Mathf.Clamp(dualSpecularRoughness1, 0.01f, MaxDualSpecularRoughness);
            DualSpecularLobeMix = Mathf.Clamp01(dualSpecularLobeMix);
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

        public float ExtinctionScale { get; }

        public float TransmissionNormalScale { get; }

        public float ScatteringDistribution { get; }

        public float IOR { get; }

        public Color TransmissionTintColor { get; }

        public float DualSpecularRoughness0 { get; }

        public float DualSpecularRoughness1 { get; }

        public float DualSpecularLobeMix { get; }

        public float RadiusPixels { get; }

        public float DepthSigma { get; }

        public float NormalSigma { get; }

        public float Blend { get; }

        public float DistanceScale { get; }

        public float BoundaryBleed { get; }

        public float TintStrength { get; }

        public float MinStrength { get; }

        public float MeanFreePathScreenScale => BurtSubsurfaceLutUtility.GetMeanFreePathScreenScale(this);

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

        public Vector4 TransmissionVector => new Vector4(
            Mathf.Clamp01(ExtinctionScale * 0.01f),
            TransmissionNormalScale,
            Mathf.Clamp01((ScatteringDistribution + 1f) * 0.5f),
            1f / Mathf.Max(IOR, 0.01f));

        public Vector4 TransmissionTintVector => ToVector(TransmissionTintColor);

        public Vector4 DualSpecularVector
        {
            get
            {
                var averageRoughness = Mathf.Lerp(DualSpecularRoughness0, DualSpecularRoughness1, DualSpecularLobeMix);
                return new Vector4(
                    Mathf.Clamp01(DualSpecularRoughness0 / MaxDualSpecularRoughness),
                    Mathf.Clamp01(DualSpecularRoughness1 / MaxDualSpecularRoughness),
                    DualSpecularLobeMix,
                    Mathf.Clamp01(averageRoughness / MaxDualSpecularRoughness));
            }
        }

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
                0.1f,
                1f,
                Color.white,
                Color.white,
                DefaultExtinctionScale,
                DefaultTransmissionNormalScale,
                DefaultScatteringDistribution,
                DefaultIOR,
                Color.white,
                DefaultDualSpecularRoughness0,
                DefaultDualSpecularRoughness1,
                DefaultDualSpecularLobeMix,
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

    public readonly struct BurtSubsurfaceProfilePalette
    {
        public const int MaxProfiles = 8;

        private static readonly BurtSubsurfaceProfileSettings[] DefaultSettings =
        {
            BurtSubsurfaceProfileSettings.Default
        };

        private readonly BurtSubsurfaceProfileSettings[] settings;
        private readonly int count;

        private BurtSubsurfaceProfilePalette(BurtSubsurfaceProfileSettings[] settings, int count)
        {
            this.settings = settings;
            this.count = Mathf.Clamp(count, 1, MaxProfiles);
        }

        public int Count => settings == null ? 1 : Mathf.Clamp(count, 1, MaxProfiles);

        public IReadOnlyList<BurtSubsurfaceProfileSettings> Settings => settings ?? DefaultSettings;

        public BurtSubsurfaceProfileSettings GetSettings(int index)
        {
            var values = settings ?? DefaultSettings;
            var safeIndex = Mathf.Clamp(index, 0, values.Length - 1);
            return values[safeIndex];
        }

        public string GetName(int index)
        {
            return GetSettings(index).ProfileName;
        }

        public static BurtSubsurfaceProfilePalette Resolve(
            BurtSubsurfaceProfileSettings defaultSettings,
            IReadOnlyList<BurtSubsurfaceProfile> additionalProfiles)
        {
            var resolved = new BurtSubsurfaceProfileSettings[MaxProfiles];
            for (var i = 0; i < resolved.Length; i++)
            {
                resolved[i] = defaultSettings;
            }

            var resolvedCount = 1;
            if (additionalProfiles != null)
            {
                var additionalCount = Mathf.Min(additionalProfiles.Count, MaxProfiles - 1);
                for (var i = 0; i < additionalCount; i++)
                {
                    var slot = i + 1;
                    var profile = additionalProfiles[i];
                    resolved[slot] = profile != null ? profile.CreateSettings() : defaultSettings;
                    resolvedCount = slot + 1;
                }
            }

            return new BurtSubsurfaceProfilePalette(resolved, resolvedCount);
        }
    }

    internal static class BurtSubsurfaceProfileShaderUtility
    {
        public const string ProfileCountShaderName = "_BurtSubsurfaceProfileCount";
        public const string ProfileDualSpecularsShaderName = "_BurtSubsurfaceProfileDualSpeculars";
        public const string ProfileTransmissionsShaderName = "_BurtSubsurfaceProfileTransmissions";
        public const string ProfileTransmissionTintsShaderName = "_BurtSubsurfaceProfileTransmissionTints";
        public const string ProfileParamLutShaderName = BurtSubsurfaceLutUtility.ProfileParamLutShaderName;
        public const string ProfileParamLutEnabledShaderName = BurtSubsurfaceLutUtility.ProfileParamLutEnabledShaderName;
        public const string ProfileParamLutSizeShaderName = BurtSubsurfaceLutUtility.ProfileParamLutSizeShaderName;

        public static readonly int ProfileCountId = Shader.PropertyToID(ProfileCountShaderName);
        public static readonly int ProfileDualSpecularsId = Shader.PropertyToID(ProfileDualSpecularsShaderName);
        public static readonly int ProfileTransmissionsId = Shader.PropertyToID(ProfileTransmissionsShaderName);
        public static readonly int ProfileTransmissionTintsId = Shader.PropertyToID(ProfileTransmissionTintsShaderName);
        public static readonly int ProfileParamLutId = BurtSubsurfaceLutUtility.ProfileParamLutId;
        public static readonly int ProfileParamLutEnabledId = BurtSubsurfaceLutUtility.ProfileParamLutEnabledId;
        public static readonly int ProfileParamLutSizeId = BurtSubsurfaceLutUtility.ProfileParamLutSizeId;

        private static readonly Vector4[] ProfileDualSpeculars = new Vector4[BurtSubsurfaceProfilePalette.MaxProfiles];
        private static readonly Vector4[] ProfileTransmissions = new Vector4[BurtSubsurfaceProfilePalette.MaxProfiles];
        private static readonly Vector4[] ProfileTransmissionTints = new Vector4[BurtSubsurfaceProfilePalette.MaxProfiles];

        public static void BindGlobals(BurtRenderPipelineAsset asset)
        {
            BurtSubsurfaceLutUtility.BeginPaletteBinding();
            var palette = asset != null
                ? asset.ScreenSpaceSubsurfaceProfilePalette
                : BurtSubsurfaceProfilePalette.Resolve(BurtSubsurfaceProfileSettings.Default, null);

            var count = Mathf.Clamp(palette.Count, 1, BurtSubsurfaceProfilePalette.MaxProfiles);
            var fallback = palette.GetSettings(0);
            for (var i = 0; i < BurtSubsurfaceProfilePalette.MaxProfiles; i++)
            {
                var profile = i < count ? palette.GetSettings(i) : fallback;
                ProfileDualSpeculars[i] = profile.DualSpecularVector;
                ProfileTransmissions[i] = profile.TransmissionVector;
                ProfileTransmissionTints[i] = profile.TransmissionTintVector;
            }

            Shader.SetGlobalFloat(ProfileCountId, count);
            Shader.SetGlobalVectorArray(ProfileDualSpecularsId, ProfileDualSpeculars);
            Shader.SetGlobalVectorArray(ProfileTransmissionsId, ProfileTransmissions);
            Shader.SetGlobalVectorArray(ProfileTransmissionTintsId, ProfileTransmissionTints);

            var profileParamLut = BurtSubsurfaceLutUtility.GetOrCreateProfileParamLut(palette);
            Shader.SetGlobalTexture(ProfileParamLutId, profileParamLut != null ? profileParamLut : BurtSubsurfaceLutUtility.GetFallbackProfileParamLut());
            Shader.SetGlobalFloat(ProfileParamLutEnabledId, profileParamLut != null ? 1f : 0f);
            Shader.SetGlobalVector(ProfileParamLutSizeId, BurtSubsurfaceLutUtility.ProfileParamLutSizeVector);
        }
    }
}
