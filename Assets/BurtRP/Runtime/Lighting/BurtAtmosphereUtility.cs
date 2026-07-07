using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    public readonly struct BurtAtmosphereDebugSnapshot
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
        public readonly float SunDiskSize;
        public readonly float SunDiskIntensity;
        public readonly float SunHaloSize;
        public readonly float SunHaloIntensity;
        public readonly AtmosphereSunSource SunSource;
        public readonly Vector3 CustomSunDirection;
        public readonly Color HorizonColor;
        public readonly Color HorizonSunsetColor;
        public readonly float HorizonIntensity;
        public readonly float HorizonFalloff;
        public readonly float HorizonSunsetInfluence;
        public readonly float GroundContribution;
        public readonly float GroundBlendStart;
        public readonly float GroundBlendEnd;
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
        public readonly AtmosphereAerialPerspectivePlacement AerialPerspectivePlacement;
        public readonly AtmosphereFogInteraction FogInteraction;
        public readonly string SkyFormula;
        public readonly string AerialFormula;

        internal BurtAtmosphereDebugSnapshot(BurtAtmosphereSettings settings, string skyFormula, string aerialFormula)
        {
            Enabled = settings.Enabled;
            RayleighIntensity = settings.RayleighIntensity;
            MieIntensity = settings.MieIntensity;
            MieAnisotropy = settings.MieAnisotropy;
            PlanetRadius = settings.PlanetRadius;
            AtmosphereHeight = settings.AtmosphereHeight;
            RayleighScaleHeight = settings.RayleighScaleHeight;
            MieScaleHeight = settings.MieScaleHeight;
            GroundColor = settings.GroundColor;
            SkyTint = settings.SkyTint;
            SunIntensity = settings.SunIntensity;
            SunDiskSize = settings.SunDiskSize;
            SunDiskIntensity = settings.SunDiskIntensity;
            SunHaloSize = settings.SunHaloSize;
            SunHaloIntensity = settings.SunHaloIntensity;
            SunSource = settings.SunSource;
            CustomSunDirection = settings.CustomSunDirection;
            HorizonColor = settings.HorizonColor;
            HorizonSunsetColor = settings.HorizonSunsetColor;
            HorizonIntensity = settings.HorizonIntensity;
            HorizonFalloff = settings.HorizonFalloff;
            HorizonSunsetInfluence = settings.HorizonSunsetInfluence;
            GroundContribution = settings.GroundContribution;
            GroundBlendStart = settings.GroundBlendStart;
            GroundBlendEnd = settings.GroundBlendEnd;
            ExposureCompensation = settings.ExposureCompensation;
            TonemapSafeSunIntensity = settings.TonemapSafeSunIntensity;
            AerialPerspectiveEnabled = settings.AerialPerspectiveEnabled;
            AerialPerspectiveIntensity = settings.AerialPerspectiveIntensity;
            AerialPerspectiveDistance = settings.AerialPerspectiveDistance;
            AerialPerspectiveHeightFalloff = settings.AerialPerspectiveHeightFalloff;
            AerialPerspectiveTint = settings.AerialPerspectiveTint;
            AerialPerspectiveNearFadeStart = settings.AerialPerspectiveNearFadeStart;
            AerialPerspectiveNearFadeEnd = settings.AerialPerspectiveNearFadeEnd;
            AerialPerspectiveMaxOpacity = settings.AerialPerspectiveMaxOpacity;
            AerialPerspectivePlacement = settings.AerialPerspectivePlacement;
            FogInteraction = settings.FogInteraction;
            SkyFormula = string.IsNullOrEmpty(skyFormula) ? BurtAtmosphereUtility.SkyFormulaName : skyFormula;
            AerialFormula = string.IsNullOrEmpty(aerialFormula) ? BurtAtmosphereUtility.AerialFormulaName : aerialFormula;
        }
    }

    public static class BurtAtmosphereDebugUtility
    {
        public static BurtAtmosphereDebugSnapshot GetSnapshot()
        {
            return new BurtAtmosphereDebugSnapshot(
                BurtAtmosphereUtility.ResolveSettings(),
                BurtAtmosphereUtility.SkyFormulaName,
                BurtAtmosphereUtility.AerialFormulaName);
        }

        public static string FormatDebugState()
        {
            return BurtAtmosphereUtility.FormatDebugState();
        }
    }

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
        public readonly float SunDiskSize;
        public readonly float SunDiskIntensity;
        public readonly float SunHaloSize;
        public readonly float SunHaloIntensity;
        public readonly AtmosphereSunSource SunSource;
        public readonly Vector3 CustomSunDirection;
        public readonly Color HorizonColor;
        public readonly Color HorizonSunsetColor;
        public readonly float HorizonIntensity;
        public readonly float HorizonFalloff;
        public readonly float HorizonSunsetInfluence;
        public readonly float GroundContribution;
        public readonly float GroundBlendStart;
        public readonly float GroundBlendEnd;
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
        public readonly AtmosphereAerialPerspectivePlacement AerialPerspectivePlacement;
        public readonly AtmosphereFogInteraction FogInteraction;

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
            float sunDiskSize,
            float sunDiskIntensity,
            float sunHaloSize,
            float sunHaloIntensity,
            AtmosphereSunSource sunSource,
            Vector3 customSunDirection,
            Color horizonColor,
            Color horizonSunsetColor,
            float horizonIntensity,
            float horizonFalloff,
            float horizonSunsetInfluence,
            float groundContribution,
            float groundBlendStart,
            float groundBlendEnd,
            float exposureCompensation,
            float tonemapSafeSunIntensity,
            bool aerialPerspectiveEnabled,
            float aerialPerspectiveIntensity,
            float aerialPerspectiveDistance,
            float aerialPerspectiveHeightFalloff,
            Color aerialPerspectiveTint,
            float aerialPerspectiveNearFadeStart,
            float aerialPerspectiveNearFadeEnd,
            float aerialPerspectiveMaxOpacity,
            AtmosphereAerialPerspectivePlacement aerialPerspectivePlacement,
            AtmosphereFogInteraction fogInteraction)
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
            SunDiskSize = Mathf.Max(0.05f, sunDiskSize);
            SunDiskIntensity = Mathf.Max(0f, sunDiskIntensity);
            SunHaloSize = Mathf.Max(0.05f, sunHaloSize);
            SunHaloIntensity = Mathf.Max(0f, sunHaloIntensity);
            SunSource = sunSource;
            CustomSunDirection = customSunDirection.sqrMagnitude > 0.0001f ? customSunDirection.normalized : new Vector3(0.3f, 0.8f, 0.4f).normalized;
            HorizonColor = horizonColor;
            HorizonSunsetColor = horizonSunsetColor;
            HorizonIntensity = Mathf.Max(0f, horizonIntensity);
            HorizonFalloff = Mathf.Max(0.1f, horizonFalloff);
            HorizonSunsetInfluence = Mathf.Clamp01(horizonSunsetInfluence);
            GroundContribution = Mathf.Max(0f, groundContribution);
            GroundBlendStart = groundBlendStart;
            GroundBlendEnd = groundBlendEnd;
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
            AerialPerspectivePlacement = aerialPerspectivePlacement;
            FogInteraction = fogInteraction;
        }

        public static BurtAtmosphereSettings Disabled => new BurtAtmosphereSettings(false, 0f, 0f, 0f, 6371f, 80f, 8f, 1.2f, Color.black, Color.white, 0f, 1f, 1.2f, 1f, 1f, AtmosphereSunSource.MainLight, Vector3.up, new Color(0.48f, 0.66f, 0.92f, 1f), new Color(0.95f, 0.82f, 0.58f, 1f), 1f, 0.65f, 0.35f, 0.22f, -0.02f, -0.20f, 0f, 4f, false, 0f, 250f, 0f, new Color(0.70f, 0.82f, 1.0f, 1f), 0f, 50f, 0.65f, AtmosphereAerialPerspectivePlacement.AfterOpaqueBeforeSky, AtmosphereFogInteraction.Additive);
    }

    internal static class BurtAtmosphereUtility
    {
        public const string ShaderName = "Hidden/BurtRP/AtmosphereScattering";
        public const string SkyFormulaName = "ArtisticSingleScatteringSkyV2";
        public const string AerialFormulaName = "ArtisticMetersV2";

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

            var settings = ResolveSettings();
            return settings.AerialPerspectiveEnabled && settings.FogInteraction != AtmosphereFogInteraction.FogOnly;
        }

        public static bool ShouldApplyAerialPerspectiveAfterOpaqueBeforeSky(BurtRenderRequest request)
        {
            return ShouldUseAerialPerspective(request) && ResolveSettings().AerialPerspectivePlacement == AtmosphereAerialPerspectivePlacement.AfterOpaqueBeforeSky;
        }

        public static bool ShouldApplyAerialPerspectiveAfterSkyBeforeSSR(BurtRenderRequest request)
        {
            return ShouldUseAerialPerspective(request) && ResolveSettings().AerialPerspectivePlacement == AtmosphereAerialPerspectivePlacement.AfterSkyBeforeSSR;
        }

        public static bool ShouldApplyAerialPerspectiveBeforeTransparent(BurtRenderRequest request)
        {
            return ShouldUseAerialPerspective(request) && ResolveSettings().AerialPerspectivePlacement == AtmosphereAerialPerspectivePlacement.BeforeTransparent;
        }

        public static BurtAtmosphereSettings ResolveSettings()
        {
            var stack = VolumeManager.instance != null ? VolumeManager.instance.stack : null;
            var atmosphere = stack != null ? stack.GetComponent<AtmosphereVolumeComponent>() : null;
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
                atmosphere.sunDiskSize.value,
                atmosphere.sunDiskIntensity.value,
                atmosphere.sunHaloSize.value,
                atmosphere.sunHaloIntensity.value,
                atmosphere.sunSource.value,
                atmosphere.customSunDirection.value,
                atmosphere.horizonColor.value,
                atmosphere.horizonSunsetColor.value,
                atmosphere.horizonIntensity.value,
                atmosphere.horizonFalloff.value,
                atmosphere.horizonSunsetInfluence.value,
                atmosphere.groundContribution.value,
                atmosphere.groundBlendStart.value,
                atmosphere.groundBlendEnd.value,
                atmosphere.exposureCompensation.value,
                atmosphere.tonemapSafeSunIntensity.value,
                atmosphere.aerialPerspective.value,
                atmosphere.aerialPerspectiveIntensity.value,
                atmosphere.aerialPerspectiveDistance.value,
                atmosphere.aerialPerspectiveHeightFalloff.value,
                atmosphere.aerialPerspectiveTint.value,
                atmosphere.aerialPerspectiveNearFadeStart.value,
                atmosphere.aerialPerspectiveNearFadeEnd.value,
                atmosphere.aerialPerspectiveMaxOpacity.value,
                atmosphere.aerialPerspectivePlacement.value,
                atmosphere.aerialFogInteraction.value);
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
                " RayleighScaleKm=", Format(settings.RayleighScaleHeight),
                " MieScaleKm=", Format(settings.MieScaleHeight),
                " Sun=", Format(settings.SunIntensity),
                " SunDiskSize=", Format(settings.SunDiskSize),
                " SunDiskIntensity=", Format(settings.SunDiskIntensity),
                " SunHaloSize=", Format(settings.SunHaloSize),
                " SunHaloIntensity=", Format(settings.SunHaloIntensity),
                " SunSource=", settings.SunSource,
                " Horizon=", Format(settings.HorizonIntensity),
                " HorizonFalloff=", Format(settings.HorizonFalloff),
                " HorizonSunsetInfluence=", Format(settings.HorizonSunsetInfluence),
                " Ground=", Format(settings.GroundContribution),
                " GroundBlend=", Format(settings.GroundBlendStart), "/", Format(settings.GroundBlendEnd),
                " ExposureEV=", Format(settings.ExposureCompensation),
                " SunClamp=", Format(settings.TonemapSafeSunIntensity),
                " SkyTint=", FormatColor(settings.SkyTint),
                " HorizonColor=", FormatColor(settings.HorizonColor),
                " HorizonSunsetColor=", FormatColor(settings.HorizonSunsetColor),
                " GroundColor=", FormatColor(settings.GroundColor),
                " Aerial=", settings.AerialPerspectiveEnabled,
                " AerialIntensity=", Format(settings.AerialPerspectiveIntensity),
                " AerialDistance=", Format(settings.AerialPerspectiveDistance),
                " AerialHeightFalloff=", Format(settings.AerialPerspectiveHeightFalloff),
                " AerialNearFade=", Format(settings.AerialPerspectiveNearFadeStart), "/", Format(settings.AerialPerspectiveNearFadeEnd),
                " AerialMaxOpacity=", Format(settings.AerialPerspectiveMaxOpacity),
                " AerialPlacement=", settings.AerialPerspectivePlacement,
                " FogInteraction=", settings.FogInteraction,
                " AerialTint=", FormatColor(settings.AerialPerspectiveTint),
                " Formula=", SkyFormulaName);
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
                " AffectsSkyPixels=", settings.AerialPerspectivePlacement != AtmosphereAerialPerspectivePlacement.AfterOpaqueBeforeSky,
                " Placement=", settings.AerialPerspectivePlacement,
                " FogInteraction=", settings.FogInteraction,
                " Formula=", AerialFormulaName,
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

    internal static class BurtAtmosphereReflectionUtility
    {
        private const int CubemapSize = 256;
        private const int CubemapFaceCount = 6;
        private const int AtmosphereCubemapPass = 2;

        private static readonly int FaceId = Shader.PropertyToID("_BurtAtmosphereCubemapFace");
        private static readonly Vector4 DefaultHDRDecodeValues = new Vector4(1f, 1f, 0f, 0f);

        private static RenderTexture atmosphereCubemap;
        private static Material material;
        private static bool hasLoggedMissingShader;
        private static int lastRenderedFrame = -1;

        public static bool TryGetReflection(CommandBuffer cmd, BurtRenderRequest request, out Texture texture, out Vector4 hdrDecodeValues, out string source)
        {
            texture = null;
            hdrDecodeValues = DefaultHDRDecodeValues;
            source = "BurtAtmosphereReflectionUnavailable";

            if (cmd == null || request == null || !BurtAtmosphereUtility.ShouldUseAtmosphere(request))
            {
                return false;
            }

            var camera = request.Camera;
            if (camera == null)
            {
                return false;
            }

            var settings = BurtAtmosphereUtility.ResolveSettings();
            if (!settings.Enabled)
            {
                return false;
            }

            var drawMaterial = BurtDrawAtmospherePass.CreateMaterial(ref material, ref hasLoggedMissingShader);
            if (drawMaterial == null)
            {
                source = "BurtAtmosphereReflectionMissingShader";
                return false;
            }

            EnsureTexture();
            if (atmosphereCubemap == null)
            {
                source = "BurtAtmosphereReflectionAllocationFailed";
                return false;
            }

            var currentFrame = Time.frameCount;
            if (lastRenderedFrame != currentFrame)
            {
                BurtDrawAtmospherePass.UploadMaterialProperties(drawMaterial, camera, request, settings);
                for (var face = 0; face < CubemapFaceCount; face++)
                {
                    cmd.SetRenderTarget(new RenderTargetIdentifier(atmosphereCubemap, 0, (CubemapFace)face));
                    cmd.SetViewport(new Rect(0f, 0f, CubemapSize, CubemapSize));
                    cmd.SetGlobalFloat(FaceId, face);
                    cmd.DrawProcedural(Matrix4x4.identity, drawMaterial, AtmosphereCubemapPass, MeshTopology.Triangles, 3, 1);
                }

                lastRenderedFrame = currentFrame;
            }

            texture = atmosphereCubemap;
            source = "BurtAtmosphereCubemap";
            return true;
        }

        public static bool HasReadyReflection(BurtRenderRequest request, out Texture texture, out Vector4 hdrDecodeValues, out string source)
        {
            hdrDecodeValues = DefaultHDRDecodeValues;
            source = "BurtAtmosphereReflectionUnavailable";
            texture = BurtAtmosphereUtility.ShouldUseAtmosphere(request) ? atmosphereCubemap : null;
            if (texture == null)
            {
                return false;
            }

            source = "BurtAtmosphereCubemap";
            return true;
        }

        public static void Release()
        {
            ReleaseTexture(atmosphereCubemap);
            atmosphereCubemap = null;
            lastRenderedFrame = -1;

            if (material != null)
            {
                DestroyUnityObject(material);
                material = null;
            }

            hasLoggedMissingShader = false;
        }

        private static void EnsureTexture()
        {
            if (atmosphereCubemap != null && atmosphereCubemap.IsCreated())
            {
                return;
            }

            ReleaseTexture(atmosphereCubemap);
            atmosphereCubemap = new RenderTexture(CubemapSize, CubemapSize, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
            {
                name = "Burt Atmosphere Reflection Cubemap",
                dimension = TextureDimension.Cube,
                volumeDepth = CubemapFaceCount,
                useMipMap = false,
                autoGenerateMips = false,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            atmosphereCubemap.Create();
            lastRenderedFrame = -1;
        }

        private static void ReleaseTexture(RenderTexture texture)
        {
            if (texture == null)
            {
                return;
            }

            texture.Release();
            DestroyUnityObject(texture);
        }

        private static void DestroyUnityObject(Object unityObject)
        {
            if (unityObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(unityObject);
            }
            else
            {
                Object.DestroyImmediate(unityObject);
            }
        }
    }
}
