using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    internal sealed class BurtPrepareAtmosphereLutsAsyncPass : BurtRenderPass
    {
        public override string Name => "Burt Prepare Atmosphere LUTs Async";

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null ||
                !BurtAtmosphereUtility.ShouldUseAtmosphereAsyncCompute(context.Request, context.Asset))
            {
                return;
            }

            var request = context.Request;
            var settings = BurtAtmosphereUtility.ResolveSettings();
            var sunDirection4 = BurtDrawAtmospherePass.ResolveSunDirection(request, settings);
            BurtAtmosphereLutUtility.DispatchAsync(
                context,
                request.Camera,
                settings,
                new Vector3(sunDirection4.x, sunDirection4.y, sunDirection4.z));
        }
    }

    internal sealed class BurtPrepareAtmosphereCombineMobilePass : BurtRenderPass
    {
        public override string Name => "Burt Atmosphere Combine Mobile Prepare";

        public override void Execute(BurtRenderGraphContext context)
        {
            BurtDrawAtmospherePass.PrepareMobile(context);
        }
    }

    internal sealed class BurtDrawAtmospherePass : BurtRenderPass
    {
        private const string DefaultSkyMeshResourcePath = "BurtAtmosphereDefaultSkyMesh";
        private const string SkyCaptureKeyword = "_ATMOSPHERE_COMBINE_IS_SKY_CAPTURE";
        private const string PhysicalSkyNightKeyword = "_PHYSICAL_SKY_IS_NIGHT";

        private static readonly int CameraDepthTextureId = BurtRenderGraphResourceRegistry.CameraDepthTextureId;
        private static readonly int RayleighIntensityId = Shader.PropertyToID("_BurtAtmosphereRayleighIntensity");
        private static readonly int MieIntensityId = Shader.PropertyToID("_BurtAtmosphereMieIntensity");
        private static readonly int MieAnisotropyId = Shader.PropertyToID("_BurtAtmosphereMieAnisotropy");
        private static readonly int RayleighScatteringCoefficientId = Shader.PropertyToID("_BurtAtmosphereRayleighScatteringCoefficient");
        private static readonly int MieScatteringCoefficientId = Shader.PropertyToID("_BurtAtmosphereMieScatteringCoefficient");
        private static readonly int MieAbsorptionCoefficientId = Shader.PropertyToID("_BurtAtmosphereMieAbsorptionCoefficient");
        private static readonly int OzoneAbsorptionCoefficientId = Shader.PropertyToID("_BurtAtmosphereOzoneAbsorptionCoefficient");
        private static readonly int PlanetParamsId = Shader.PropertyToID("_BurtAtmospherePlanetParams");
        private static readonly int GroundColorId = Shader.PropertyToID("_BurtAtmosphereGroundColor");
        private static readonly int SkyTintId = Shader.PropertyToID("_BurtAtmosphereSkyTint");
        private static readonly int SkyLuminanceFactorId = Shader.PropertyToID("_BurtAtmosphereSkyLuminanceFactor");
        private static readonly int SunIntensityId = Shader.PropertyToID("_BurtAtmosphereSunIntensity");
        private static readonly int MainLightColorOuterSpaceId = Shader.PropertyToID("_BurtMainLightColorOuterSpace");
        private static readonly int MainLightOcclusionFactorId = Shader.PropertyToID("_BurtMainLightOcclusionFactor");
        private static readonly int SunDirectionId = Shader.PropertyToID("_BurtAtmosphereSunDirection");
        private static readonly int SunParamsId = Shader.PropertyToID("_BurtAtmosphereSunParams");
        private static readonly int SunDiskLuminanceAndCosHalfApexId = Shader.PropertyToID("_BurtAtmosphereSunDiskLuminanceAndCosHalfApex");
        private static readonly int HorizonColorId = Shader.PropertyToID("_BurtAtmosphereHorizonColor");
        private static readonly int HorizonSunsetColorId = Shader.PropertyToID("_BurtAtmosphereHorizonSunsetColor");
        private static readonly int HorizonParamsId = Shader.PropertyToID("_BurtAtmosphereHorizonParams");
        private static readonly int GroundParamsId = Shader.PropertyToID("_BurtAtmosphereGroundParams");
        private static readonly int ExposureParamsId = Shader.PropertyToID("_BurtAtmosphereExposureParams");
        private static readonly int PhysicalSkyTimeOfDayCurveId = Shader.PropertyToID("_BurtAtmospherePhysicalSkyTimeOfDayCurve");
        private static readonly int PhysicalSkyTimeId = Shader.PropertyToID("_BurtAtmospherePhysicalSkyTime");
        private static readonly int StylizedParamsId = Shader.PropertyToID("_BurtAtmosphereStylizedParams");
        private static readonly int StylizedSunRiseParamsId = Shader.PropertyToID("_BurtAtmosphereStylizedSunRiseParams");
        private static readonly int StylizedBaseSkyColorDayId = Shader.PropertyToID("_BurtAtmosphereStylizedBaseSkyColorDay");
        private static readonly int StylizedBaseSkyColorDawnDuskId = Shader.PropertyToID("_BurtAtmosphereStylizedBaseSkyColorDawnDusk");
        private static readonly int StylizedBaseSkyColorNightId = Shader.PropertyToID("_BurtAtmosphereStylizedBaseSkyColorNight");
        private static readonly int StylizedHorizonSkyColorDayId = Shader.PropertyToID("_BurtAtmosphereStylizedHorizonSkyColorDay");
        private static readonly int StylizedHorizonSkyColorDawnDuskId = Shader.PropertyToID("_BurtAtmosphereStylizedHorizonSkyColorDawnDusk");
        private static readonly int StylizedHorizonSkyColorNightId = Shader.PropertyToID("_BurtAtmosphereStylizedHorizonSkyColorNight");
        private static readonly int StylizedSunDiskColorScaleId = Shader.PropertyToID("_BurtAtmosphereStylizedSunDiskColorScale");
        private static readonly int StylizedSunGlowColorId = Shader.PropertyToID("_BurtAtmosphereStylizedSunGlowColor");
        private static readonly int MoonSurfaceTextureId = Shader.PropertyToID("_BurtAtmosphereMoonSurfaceTexture");
        private static readonly int MoonPhaseNormalTextureId = Shader.PropertyToID("_BurtAtmosphereMoonPhaseNormalTexture");
        private static readonly int MoonDirectionId = Shader.PropertyToID("_BurtAtmosphereMoonDirection");
        private static readonly int MoonUpId = Shader.PropertyToID("_BurtAtmosphereMoonUp");
        private static readonly int MoonRightId = Shader.PropertyToID("_BurtAtmosphereMoonRight");
        private static readonly int MoonSurfaceTintId = Shader.PropertyToID("_BurtAtmosphereMoonSurfaceTint");
        private static readonly int MoonAdditionalTintId = Shader.PropertyToID("_BurtAtmosphereMoonAdditionalTint");
        private static readonly int MoonFlareTintId = Shader.PropertyToID("_BurtAtmosphereMoonFlareTint");
        private static readonly int MoonGeometryId = Shader.PropertyToID("_BurtAtmosphereMoonGeometry");
        private static readonly int MoonPhaseId = Shader.PropertyToID("_BurtAtmosphereMoonPhase");
        private static readonly int MoonPhysicalParamsId = Shader.PropertyToID("_BurtAtmosphereMoonPhysicalParams");
        private static readonly int MoonBloomParamsId = Shader.PropertyToID("_BurtAtmosphereMoonBloomParams");
        private static readonly int MoonVisibilityId = Shader.PropertyToID("_BurtAtmosphereMoonVisibility");
        private static readonly int StarsTextureId = Shader.PropertyToID("_BurtAtmosphereStarsTexture");
        private static readonly int StarsTintColorTextureId = Shader.PropertyToID("_BurtAtmosphereStarsTintColorTexture");
        private static readonly int AreaStarsTextureId = Shader.PropertyToID("_BurtAtmosphereAreaStarsTexture");
        private static readonly int GalaxyCloudTextureId = Shader.PropertyToID("_BurtAtmosphereGalaxyCloudTexture");
        private static readonly int StarsControlId = Shader.PropertyToID("_BurtAtmosphereStarsControl");
        private static readonly int StarsTintColorId = Shader.PropertyToID("_BurtAtmosphereStarsTintColor");
        private static readonly int StarsTintTransformId = Shader.PropertyToID("_BurtAtmosphereStarsTintTransform");
        private static readonly int StarsLayerHeightsRotationId = Shader.PropertyToID("_BurtAtmosphereStarsLayerHeightsRotation");
        private static readonly int StarsLayerAnimationId = Shader.PropertyToID("_BurtAtmosphereStarsLayerAnimation");
        private static readonly int StarsLayerFalloffsId = Shader.PropertyToID("_BurtAtmosphereStarsLayerFalloffs");
        private static readonly int AreaStarsParamsId = Shader.PropertyToID("_BurtAtmosphereAreaStarsParams");
        private static readonly int AreaStarsMaskTransformId = Shader.PropertyToID("_BurtAtmosphereAreaStarsMaskTransform");
        private static readonly int AreaStarsFalloffsId = Shader.PropertyToID("_BurtAtmosphereAreaStarsFalloffs");
        private static readonly int GalaxyTransformId = Shader.PropertyToID("_BurtAtmosphereGalaxyTransform");
        private static readonly int GalaxyParamsId = Shader.PropertyToID("_BurtAtmosphereGalaxyParams");
        private static readonly int GalaxyStarParamsId = Shader.PropertyToID("_BurtAtmosphereGalaxyStarParams");
        private static readonly int CustomStarTextureId = Shader.PropertyToID("_BurtAtmosphereCustomStarTexture");
        private static readonly int CustomStarTextureTransformId = Shader.PropertyToID("_BurtAtmosphereCustomStarTextureTransform");
        private static readonly int CustomStarControlId = Shader.PropertyToID("_BurtAtmosphereCustomStarControl");
        private static readonly int CustomStarIntensityMaxId = Shader.PropertyToID("_BurtAtmosphereCustomStarIntensityMax");
        private static readonly int CustomStarIntensityMinId = Shader.PropertyToID("_BurtAtmosphereCustomStarIntensityMin");
        private static readonly int CustomStarScatterIntervalId = Shader.PropertyToID("_BurtAtmosphereCustomStarScatterInterval");
        private static readonly int PanoramicCloudDefaultTextureId = Shader.PropertyToID("_BurtAtmospherePanoramicCloudDefaultTexture");
        private static readonly int PanoramicCloudPreviousTextureId = Shader.PropertyToID("_BurtAtmospherePanoramicCloudPreviousTexture");
        private static readonly int PanoramicCloudCurrentTextureId = Shader.PropertyToID("_BurtAtmospherePanoramicCloudCurrentTexture");
        private static readonly int PanoramicCloudControlId = Shader.PropertyToID("_BurtAtmospherePanoramicCloudControl");
        private static readonly int PanoramicCloudUvParamsId = Shader.PropertyToID("_BurtAtmospherePanoramicCloudUvParams");
        private static readonly int PanoramicCloudLuminanceParamsId = Shader.PropertyToID("_BurtAtmospherePanoramicCloudLuminanceParams");
        private static readonly int PanoramicCloudBaseColorId = Shader.PropertyToID("_BurtAtmospherePanoramicCloudBaseColor");
        private static readonly int PanoramicCloudDetailSpecularId = Shader.PropertyToID("_BurtAtmospherePanoramicCloudDetailSpecular");
        private static readonly int PhysicalSkyDesaturationParamsId = Shader.PropertyToID("_BurtAtmospherePhysicalSkyDesaturationParams");
        private static readonly int PhysicalSkyDesaturationColorId = Shader.PropertyToID("_BurtAtmospherePhysicalSkyDesaturationColor");
        private static readonly int WeatherCoverageTextureId = Shader.PropertyToID("_BurtAtmosphereWeatherCoverageTexture");
        private static readonly int WeatherWeightsId = Shader.PropertyToID("_BurtAtmosphereWeatherWeights");
        private static readonly int WeatherShadowParamsId = Shader.PropertyToID("_BurtAtmosphereWeatherShadowParams");
        private static readonly int WeatherShadowBrightId = Shader.PropertyToID("_BurtAtmosphereWeatherShadowBright");
        private static readonly int WeatherShadowDarkId = Shader.PropertyToID("_BurtAtmosphereWeatherShadowDark");
        private static readonly int AerialPerspectiveParamsId = Shader.PropertyToID("_BurtAtmosphereAerialPerspectiveParams");
        private static readonly int AerialPerspectiveTintId = Shader.PropertyToID("_BurtAtmosphereAerialPerspectiveTint");
        private static readonly int AerialPerspectiveFadeParamsId = Shader.PropertyToID("_BurtAtmosphereAerialPerspectiveFadeParams");
        private static readonly int FogLutDistanceParamsId = Shader.PropertyToID("_BurtAtmosphereFogLutDistanceParams");
        private static readonly int InverseViewProjectionId = Shader.PropertyToID("_BurtAtmosphereInverseViewProjection");
        private static readonly int SkyMeshViewProjectionId = Shader.PropertyToID("_BurtAtmosphereSkyMeshViewProjection");
        private static readonly int ProceduralSkyId = Shader.PropertyToID("_BurtAtmosphereProceduralSky");
        private static readonly int CameraPositionWSId = Shader.PropertyToID("_BurtAtmosphereCameraPositionWS");
        private static readonly int DebugModeId = Shader.PropertyToID("_BurtAtmosphereDebugMode");

        private static readonly int[] MobilePreparedFloatPropertyIds =
        {
            RayleighIntensityId,
            MieIntensityId,
            MieAnisotropyId,
            SunIntensityId,
            MainLightOcclusionFactorId,
            PhysicalSkyTimeOfDayCurveId,
            CustomStarScatterIntervalId,
            DebugModeId
        };

        private static readonly int[] MobilePreparedVectorPropertyIds =
        {
            RayleighScatteringCoefficientId,
            MieScatteringCoefficientId,
            MieAbsorptionCoefficientId,
            OzoneAbsorptionCoefficientId,
            PlanetParamsId,
            GroundColorId,
            SkyTintId,
            SkyLuminanceFactorId,
            MainLightColorOuterSpaceId,
            SunDirectionId,
            SunParamsId,
            SunDiskLuminanceAndCosHalfApexId,
            HorizonColorId,
            HorizonSunsetColorId,
            HorizonParamsId,
            GroundParamsId,
            ExposureParamsId,
            PhysicalSkyTimeId,
            StylizedParamsId,
            StylizedSunRiseParamsId,
            StylizedBaseSkyColorDayId,
            StylizedBaseSkyColorDawnDuskId,
            StylizedBaseSkyColorNightId,
            StylizedHorizonSkyColorDayId,
            StylizedHorizonSkyColorDawnDuskId,
            StylizedHorizonSkyColorNightId,
            StylizedSunDiskColorScaleId,
            StylizedSunGlowColorId,
            MoonDirectionId,
            MoonUpId,
            MoonRightId,
            MoonSurfaceTintId,
            MoonAdditionalTintId,
            MoonFlareTintId,
            MoonGeometryId,
            MoonPhaseId,
            MoonPhysicalParamsId,
            MoonBloomParamsId,
            MoonVisibilityId,
            StarsControlId,
            StarsTintColorId,
            StarsTintTransformId,
            StarsLayerHeightsRotationId,
            StarsLayerAnimationId,
            StarsLayerFalloffsId,
            AreaStarsParamsId,
            AreaStarsMaskTransformId,
            AreaStarsFalloffsId,
            GalaxyTransformId,
            GalaxyParamsId,
            GalaxyStarParamsId,
            CustomStarTextureTransformId,
            CustomStarControlId,
            CustomStarIntensityMaxId,
            CustomStarIntensityMinId,
            PanoramicCloudControlId,
            PanoramicCloudUvParamsId,
            PanoramicCloudLuminanceParamsId,
            PanoramicCloudBaseColorId,
            PanoramicCloudDetailSpecularId,
            PhysicalSkyDesaturationParamsId,
            PhysicalSkyDesaturationColorId,
            WeatherWeightsId,
            WeatherShadowParamsId,
            WeatherShadowBrightId,
            WeatherShadowDarkId,
            AerialPerspectiveParamsId,
            AerialPerspectiveTintId,
            AerialPerspectiveFadeParamsId,
            FogLutDistanceParamsId,
            CameraPositionWSId
        };

        private static readonly int[] MobilePreparedTexturePropertyIds =
        {
            MoonSurfaceTextureId,
            MoonPhaseNormalTextureId,
            StarsTextureId,
            StarsTintColorTextureId,
            AreaStarsTextureId,
            GalaxyCloudTextureId,
            CustomStarTextureId,
            PanoramicCloudDefaultTextureId,
            PanoramicCloudPreviousTextureId,
            PanoramicCloudCurrentTextureId,
            WeatherCoverageTextureId
        };

        // XRender's atmosphere/nature parameter tables bind a concrete dummy
        // resource for every texture slot on every draw. Keep the fallback table
        // parallel to MobilePreparedTexturePropertyIds so a custom material that
        // does not expose Burt's properties cannot leak a previous camera/frame's
        // global texture into the prepared mobile draw.
        private static readonly Texture2D[] MobilePreparedTextureFallbacks =
        {
            Texture2D.whiteTexture, // Moon surface.
            Texture2D.whiteTexture, // Moon phase normal (XRender intentionally uses white, not a flat-normal texture).
            Texture2D.blackTexture, // Stars.
            Texture2D.whiteTexture, // Star tint.
            Texture2D.blackTexture, // Area stars.
            Texture2D.blackTexture, // Galaxy cloud.
            Texture2D.blackTexture, // Custom star material property.
            Texture2D.blackTexture, // Default panoramic cloud.
            Texture2D.blackTexture, // Previous panoramic weather cloud.
            Texture2D.blackTexture, // Current panoramic weather cloud.
            Texture2D.whiteTexture  // Weather coverage material property.
        };

        private static Mesh defaultSkyMesh;
        private static bool hasLoggedMissingSkyMesh;

        private static Material material;
        private static Material mobileDrawMaterial;
        private MaterialPropertyBlock skyMeshProperties;
        private static bool hasLoggedMissingShader;
        private static int mobilePrepareVersion;
        private static int mobileConsumedVersion;
        private static int mobilePrepareCount;
        private static int mobileFallbackCount;
        private static int lastMobilePrepareFrame = -1;
        private static int lastMobilePrepareCameraId;
        private static int permutationApplyCount;
        private static bool lastPermutationWasSkyCapture;
        private static bool lastPermutationWasNight;
        private static int proceduralFallbackDrawCount;
        private static bool lastDrawUsedProceduralFallback;
        private static bool lastDrawUsedCustomMaterial;

        public override string Name => "Burt Draw Atmosphere";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.FullScreen;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtAtmosphereUtility.ShouldUseAtmosphere(builder.Request))
            {
                return;
            }

            builder.ReadCameraDepth();
            builder.WriteCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtAtmosphereUtility.ShouldUseAtmosphere(context.Request))
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
            var camera = request.Camera;
            if (camera == null)
            {
                return;
            }

            var settings = BurtAtmosphereUtility.ResolveSettings();
            if (!settings.Enabled)
            {
                return;
            }

            var skyMesh = ResolveSkyMesh(settings);
            var useMobilePreparedState =
                BurtAtmosphereUtility.IsMobileAtmospherePlatform &&
                mobilePrepareVersion != mobileConsumedVersion &&
                lastMobilePrepareFrame == Time.frameCount &&
                lastMobilePrepareCameraId == camera.GetInstanceID();
            var drawMaterial = ResolveSkyMaterial(settings, useMobilePreparedState);
            if (drawMaterial == null)
            {
                return;
            }

            var cmd = context.AcquireCommandBuffer(Name);
            PreparePermutation(cmd, settings, false);
            if (useMobilePreparedState)
            {
                mobileConsumedVersion = mobilePrepareVersion;
            }
            else
            {
                var sunDirection4 = ResolveSunDirection(request, settings);
                BurtAtmosphereLutUtility.EnsureLuts(
                    cmd,
                    camera,
                    settings,
                    new Vector3(sunDirection4.x, sunDirection4.y, sunDirection4.z));
                UploadMaterialProperties(drawMaterial, camera, request, settings);
                if (BurtAtmosphereUtility.IsMobileAtmospherePlatform ||
                    settings.PhysicalSkyMaterial != null)
                {
                    UploadPreparedMaterialGlobals(cmd, drawMaterial, camera);
                    if (BurtAtmosphereUtility.IsMobileAtmospherePlatform)
                    {
                        mobileFallbackCount++;
                    }
                }
            }

            // XRender's generic scene target carries both color and depth.
            // Bind BRP's depth attachment as well so PhysicalSky ZTest LEqual
            // has the same early-depth contract; ZWrite remains disabled.
            cmd.SetRenderTarget(cameraColorTarget.Identifier, cameraDepthTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            cmd.SetGlobalTexture(CameraDepthTextureId, cameraDepthTarget.Identifier);
            if (!useMobilePreparedState && skyMeshProperties == null)
            {
                skyMeshProperties = new MaterialPropertyBlock();
            }

            if (!useMobilePreparedState)
            {
                skyMeshProperties.Clear();
                skyMeshProperties.SetMatrix(SkyMeshViewProjectionId, ResolveSkyMeshViewProjection(camera));
            }

            var useProceduralFallback = skyMesh == null;
            cmd.SetGlobalFloat(ProceduralSkyId, useProceduralFallback ? 1f : 0f);
            if (useProceduralFallback)
            {
                if (!useMobilePreparedState)
                {
                    skyMeshProperties.SetFloat(ProceduralSkyId, 1f);
                    skyMeshProperties.SetMatrix(InverseViewProjectionId, ResolveInverseViewProjection(camera));
                }

                cmd.DrawProcedural(
                    Matrix4x4.identity,
                    drawMaterial,
                    0,
                    MeshTopology.Triangles,
                    3,
                    1,
                    useMobilePreparedState ? null : skyMeshProperties);
                proceduralFallbackDrawCount++;
            }
            else
            {
                if (!useMobilePreparedState)
                {
                    skyMeshProperties.SetFloat(ProceduralSkyId, 0f);
                }

                cmd.DrawMesh(
                    skyMesh,
                    ResolveSkyMeshLocalToWorld(settings),
                    drawMaterial,
                    0,
                    0,
                    useMobilePreparedState ? null : skyMeshProperties);
            }

            lastDrawUsedProceduralFallback = useProceduralFallback;
            lastDrawUsedCustomMaterial = settings.PhysicalSkyMaterial != null;
            context.ExecuteLegacyCommandBuffer(cmd);
            context.ReleaseCommandBuffer(cmd);
        }

        internal static void PrepareMobile(BurtRenderGraphContext context)
        {
            if (context == null ||
                !BurtAtmosphereUtility.IsMobileAtmospherePlatform ||
                !BurtAtmosphereUtility.ShouldUseAtmosphere(context.Request))
            {
                return;
            }

            var request = context.Request;
            var camera = request.Camera;
            if (camera == null)
            {
                return;
            }

            var settings = BurtAtmosphereUtility.ResolveSettings();
            if (!settings.Enabled)
            {
                return;
            }

            var drawMaterial = settings.PhysicalSkyMaterial != null
                ? settings.PhysicalSkyMaterial
                : GetMaterial();
            if (drawMaterial == null)
            {
                return;
            }

            var cmd = context.AcquireCommandBuffer("Burt Atmosphere Combine Mobile Prepare");
            var sunDirection4 = ResolveSunDirection(request, settings);
            BurtAtmosphereLutUtility.EnsureLuts(
                cmd,
                camera,
                settings,
                new Vector3(sunDirection4.x, sunDirection4.y, sunDirection4.z));
            UploadMaterialProperties(drawMaterial, camera, request, settings);
            UploadPreparedMaterialGlobals(cmd, drawMaterial, camera);
            context.ExecuteLegacyCommandBuffer(cmd);
            context.ReleaseCommandBuffer(cmd);

            unchecked
            {
                mobilePrepareVersion++;
                if (mobilePrepareVersion == 0)
                {
                    mobilePrepareVersion = 1;
                    mobileConsumedVersion = 0;
                }
            }

            lastMobilePrepareFrame = Time.frameCount;
            lastMobilePrepareCameraId = camera.GetInstanceID();
            mobilePrepareCount++;
        }

        internal static string FormatCombineTopologyState()
        {
            return string.Concat(
                "Platform=", BurtAtmosphereUtility.IsMobileAtmospherePlatform ? "Mobile" : "PC",
                " Topology=", BurtAtmosphereUtility.IsMobileAtmospherePlatform
                    ? "PrepareThenTileDraw"
                    : "SinglePass",
                " Contract=", BurtAtmosphereUtility.AtmosphereCombineTopologyFormulaName,
                " MobilePrepareCount=", mobilePrepareCount,
                " MobileFallbackCount=", mobileFallbackCount,
                " MobilePreparePending=", mobilePrepareVersion != mobileConsumedVersion,
                " LastMobilePrepareFrame=", lastMobilePrepareFrame,
                " LastMobilePrepareCamera=", lastMobilePrepareCameraId,
                " PermutationApplyCount=", permutationApplyCount,
                " LastCapture=", lastPermutationWasSkyCapture,
                " LastNight=", lastPermutationWasNight,
                " LastDraw=", lastDrawUsedProceduralFallback ? "ProceduralTriangle" : "SkyMesh",
                " LastMaterial=", lastDrawUsedCustomMaterial ? "CustomPass0" : "BuiltInPass0",
                " ProceduralFallbackDrawCount=", proceduralFallbackDrawCount);
        }

        internal static void PreparePermutation(
            CommandBuffer cmd,
            BurtAtmosphereSettings settings,
            bool isSkyCapture)
        {
            if (cmd == null)
            {
                return;
            }

            var isNight = !isSkyCapture && settings.PhysicalSkyTimeOfDayCurve > 0.5f;
            SetKeyword(cmd, SkyCaptureKeyword, isSkyCapture);
            SetKeyword(cmd, PhysicalSkyNightKeyword, isNight);
            lastPermutationWasSkyCapture = isSkyCapture;
            lastPermutationWasNight = isNight;
            permutationApplyCount++;
        }

        private static void SetKeyword(CommandBuffer cmd, string keyword, bool enabled)
        {
            if (enabled)
            {
                cmd.EnableShaderKeyword(keyword);
            }
            else
            {
                cmd.DisableShaderKeyword(keyword);
            }
        }

        private static void UploadPreparedMaterialGlobals(
            CommandBuffer cmd,
            Material sourceMaterial,
            Camera camera)
        {
            if (cmd == null || sourceMaterial == null || camera == null)
            {
                return;
            }

            for (var i = 0; i < MobilePreparedFloatPropertyIds.Length; i++)
            {
                var propertyId = MobilePreparedFloatPropertyIds[i];
                cmd.SetGlobalFloat(propertyId, sourceMaterial.GetFloat(propertyId));
            }

            for (var i = 0; i < MobilePreparedVectorPropertyIds.Length; i++)
            {
                var propertyId = MobilePreparedVectorPropertyIds[i];
                cmd.SetGlobalVector(propertyId, sourceMaterial.GetVector(propertyId));
            }

            for (var i = 0; i < MobilePreparedTexturePropertyIds.Length; i++)
            {
                var propertyId = MobilePreparedTexturePropertyIds[i];
                var texture = sourceMaterial.GetTexture(propertyId);
                cmd.SetGlobalTexture(
                    propertyId,
                    texture != null ? texture : MobilePreparedTextureFallbacks[i]);
            }

            cmd.SetGlobalMatrix(
                InverseViewProjectionId,
                sourceMaterial.GetMatrix(InverseViewProjectionId));
            cmd.SetGlobalMatrix(
                SkyMeshViewProjectionId,
                ResolveSkyMeshViewProjection(camera));
        }

        internal static void UploadMaterialProperties(Material targetMaterial, Camera camera, BurtRenderRequest request, BurtAtmosphereSettings settings)
        {
            var effectiveCoefficients = BurtAtmosphereUtility.ResolveEffectiveCoefficients(settings);
            // Coefficient globals match XRender RenderProxy and are already in
            // effective km^-1 units. The legacy intensity globals remain only
            // for BRP's non-physical analytic styling/debug controls.
            targetMaterial.SetFloat(RayleighIntensityId, settings.RayleighIntensity);
            targetMaterial.SetFloat(MieIntensityId, settings.MieIntensity);
            targetMaterial.SetFloat(MieAnisotropyId, settings.MieAnisotropy);
            targetMaterial.SetVector(
                RayleighScatteringCoefficientId,
                effectiveCoefficients.RayleighScattering);
            targetMaterial.SetVector(
                MieScatteringCoefficientId,
                effectiveCoefficients.MieScattering);
            targetMaterial.SetVector(
                MieAbsorptionCoefficientId,
                effectiveCoefficients.MieAbsorption);
            targetMaterial.SetVector(
                OzoneAbsorptionCoefficientId,
                effectiveCoefficients.OzoneAbsorption);
            targetMaterial.SetVector(PlanetParamsId, new Vector4(settings.PlanetRadius, settings.AtmosphereHeight, settings.RayleighScaleHeight, settings.MieScaleHeight));
            targetMaterial.SetColor(GroundColorId, settings.GroundColor);
            targetMaterial.SetColor(SkyTintId, settings.SkyTint);
            targetMaterial.SetColor(SkyLuminanceFactorId, settings.SkyLuminanceFactor);
            targetMaterial.SetFloat(SunIntensityId, settings.SunIntensity);
            var lightingData = request != null ? request.LightingData : null;
            targetMaterial.SetColor(
                MainLightColorOuterSpaceId,
                lightingData != null && lightingData.HasMainLight
                    ? lightingData.MainLightColorOuterSpace
                    : Color.clear);
            targetMaterial.SetFloat(MainLightOcclusionFactorId, settings.MainLightOcclusion);
            targetMaterial.SetVector(SunDirectionId, ResolveSunDirection(request, settings));
            targetMaterial.SetVector(SunParamsId, new Vector4(settings.SunDiskSize, settings.SunDiskIntensity, settings.SunHaloSize, settings.SunHaloIntensity));
            targetMaterial.SetVector(SunDiskLuminanceAndCosHalfApexId, BurtAtmosphereUtility.EvaluateSunDiskLuminanceAndCosHalfApex(request, settings));
            targetMaterial.SetColor(HorizonColorId, settings.HorizonColor);
            targetMaterial.SetColor(HorizonSunsetColorId, settings.HorizonSunsetColor);
            targetMaterial.SetVector(HorizonParamsId, new Vector4(settings.HorizonIntensity, settings.HorizonFalloff, settings.HorizonSunsetInfluence, 0f));
            targetMaterial.SetVector(GroundParamsId, new Vector4(settings.GroundContribution, settings.GroundBlendStart, settings.GroundBlendEnd, 0f));
            targetMaterial.SetVector(ExposureParamsId, new Vector4(Mathf.Pow(2f, settings.ExposureCompensation), settings.TonemapSafeSunIntensity, settings.ExposureCompensation, 0f));
            targetMaterial.SetFloat(PhysicalSkyTimeOfDayCurveId, settings.PhysicalSkyTimeOfDayCurve);
            targetMaterial.SetVector(PhysicalSkyTimeId, ResolvePhysicalSkyTime());
            var stylized = settings.StylizedSky;
            targetMaterial.SetVector(StylizedParamsId, new Vector4(stylized.Blend, stylized.HorizonBrightness, stylized.HorizonFalloff, stylized.SunGlowScale));
            targetMaterial.SetVector(StylizedSunRiseParamsId, new Vector4(stylized.SunRiseBlendMin, stylized.SunRiseBlendMax, 0f, 0f));
            targetMaterial.SetColor(StylizedBaseSkyColorDayId, stylized.BaseSkyColorDay);
            targetMaterial.SetColor(StylizedBaseSkyColorDawnDuskId, stylized.BaseSkyColorDawnDusk);
            targetMaterial.SetColor(StylizedBaseSkyColorNightId, stylized.BaseSkyColorNight);
            targetMaterial.SetColor(StylizedHorizonSkyColorDayId, stylized.HorizonSkyColorDay);
            targetMaterial.SetColor(StylizedHorizonSkyColorDawnDuskId, stylized.HorizonSkyColorDawnDusk);
            targetMaterial.SetColor(StylizedHorizonSkyColorNightId, stylized.HorizonSkyColorNight);
            targetMaterial.SetColor(StylizedSunDiskColorScaleId, stylized.SunDiskColorScale);
            targetMaterial.SetColor(StylizedSunGlowColorId, stylized.SunGlowColor);
            var moon = settings.Moon;
            BurtAtmosphereUtility.ResolveMoonBasis(moon, out var moonDirection, out var moonUp, out var moonRight);
            var phaseRadians = moon.Phase * 2f * Mathf.PI;
            var phaseRotationRadians = moon.PhaseRotation * Mathf.Deg2Rad;
            targetMaterial.SetTexture(MoonSurfaceTextureId, moon.SurfaceTexture != null ? moon.SurfaceTexture : Texture2D.whiteTexture);
            targetMaterial.SetTexture(MoonPhaseNormalTextureId, moon.PhaseNormalTexture != null ? moon.PhaseNormalTexture : Texture2D.whiteTexture);
            targetMaterial.SetVector(MoonDirectionId, new Vector4(moonDirection.x, moonDirection.y, moonDirection.z, 0f));
            targetMaterial.SetVector(MoonUpId, new Vector4(moonUp.x, moonUp.y, moonUp.z, 0f));
            targetMaterial.SetVector(MoonRightId, new Vector4(moonRight.x, moonRight.y, moonRight.z, 0f));
            targetMaterial.SetColor(MoonSurfaceTintId, moon.SurfaceTint);
            targetMaterial.SetColor(MoonAdditionalTintId, moon.AdditionalTint);
            targetMaterial.SetColor(MoonFlareTintId, moon.FlareTint);
            targetMaterial.SetVector(MoonGeometryId, BurtAtmosphereUtility.EvaluateMoonDiskLuminanceAndGeometry(moon));
            targetMaterial.SetVector(MoonPhaseId, new Vector4(
                Mathf.Sin(phaseRadians),
                Mathf.Cos(phaseRadians),
                Mathf.Sin(phaseRotationRadians),
                Mathf.Cos(phaseRotationRadians)));
            targetMaterial.SetVector(MoonPhysicalParamsId, new Vector4(
                moon.PhaseSharpness,
                moon.LightBloomIntensity,
                moon.LightBloomSize,
                moon.LightBloomFalloff));
            targetMaterial.SetVector(MoonBloomParamsId, new Vector4(
                moon.LightBloomEdgeAlpha,
                1f,
                0f,
                0f));
            targetMaterial.SetVector(MoonVisibilityId, new Vector4(
                moon.Enabled ? 1f : 0f,
                moon.Earthshine,
                moon.RiseBlendMin,
                moon.RiseBlendMax));
            var stars = settings.Stars;
            targetMaterial.SetTexture(StarsTextureId, stars.StarsTexture != null ? stars.StarsTexture : Texture2D.blackTexture);
            targetMaterial.SetTexture(StarsTintColorTextureId, stars.TintColorTexture != null ? stars.TintColorTexture : Texture2D.whiteTexture);
            targetMaterial.SetTexture(AreaStarsTextureId, stars.AreaTexture != null ? stars.AreaTexture : Texture2D.blackTexture);
            targetMaterial.SetTexture(GalaxyCloudTextureId, stars.GalaxyCloudTexture != null ? stars.GalaxyCloudTexture : Texture2D.blackTexture);
            targetMaterial.SetVector(StarsControlId, new Vector4(stars.Enabled ? 1f : 0f, stars.Intensity, stars.HorizonFalloff, 0f));
            targetMaterial.SetVector(StarsTintColorId, new Vector4(stars.TintColor.r, stars.TintColor.g, stars.TintColor.b, stars.TintColorSaturation));
            targetMaterial.SetVector(StarsTintTransformId, new Vector4(
                stars.TintColorTextureTiling.x,
                stars.TintColorTextureTiling.y,
                stars.TintColorTextureOffset.x,
                stars.TintColorTextureOffset.y));
            targetMaterial.SetVector(StarsLayerHeightsRotationId, new Vector4(
                stars.LayerHeights.x,
                stars.LayerHeights.y,
                stars.LayerHeights.z,
                stars.Rotation));
            targetMaterial.SetVector(StarsLayerAnimationId, new Vector4(
                stars.LayerSpeed / 10f,
                stars.TwinkleStrength,
                stars.TwinkleSpeed / 50f,
                0f));
            targetMaterial.SetVector(StarsLayerFalloffsId, new Vector4(
                stars.LayerFalloffs.x,
                stars.LayerFalloffs.y,
                stars.LayerFalloffs.z,
                0f));
            targetMaterial.SetVector(AreaStarsParamsId, new Vector4(
                stars.AreaIntensity,
                stars.AreaDensityMinMax.x,
                stars.AreaDensityMinMax.y,
                stars.AreaSpeed));
            targetMaterial.SetVector(AreaStarsMaskTransformId, new Vector4(
                stars.AreaMaskTiling.x,
                stars.AreaMaskTiling.y,
                stars.AreaMaskOffset.x,
                stars.AreaMaskOffset.y));
            targetMaterial.SetVector(AreaStarsFalloffsId, new Vector4(stars.AreaFalloff, stars.AreaMaskFalloff, 0f, 0f));
            targetMaterial.SetVector(GalaxyTransformId, new Vector4(
                stars.GalaxyCloudTiling.x,
                stars.GalaxyCloudTiling.y,
                stars.GalaxyCloudOffset.x,
                stars.GalaxyCloudOffset.y));
            targetMaterial.SetVector(GalaxyParamsId, new Vector4(
                stars.GalaxyCloudRotation * Mathf.Deg2Rad,
                stars.GalaxyCloudIntensity,
                stars.GalaxyCloudFalloff,
                stars.GalaxyStarIntensity));
            targetMaterial.SetVector(GalaxyStarParamsId, new Vector4(
                stars.GalaxyStarFalloff,
                stars.GalaxyStarHeight,
                stars.GalaxyStarSpeed / 10f,
                0f));
            targetMaterial.SetTexture(CustomStarTextureId, stars.CustomStarTexture != null ? stars.CustomStarTexture : Texture2D.blackTexture);
            targetMaterial.SetVector(CustomStarTextureTransformId, new Vector4(
                stars.CustomStarTextureScale.x,
                stars.CustomStarTextureScale.y,
                stars.CustomStarTextureOffset.x,
                stars.CustomStarTextureOffset.y));
            targetMaterial.SetVector(CustomStarControlId, new Vector4(
                stars.CustomStarTexture != null ? 1f : 0f,
                stars.CustomStarRotation,
                stars.CustomStarScaleMin,
                stars.CustomStarScatterSpeed));
            targetMaterial.SetVector(CustomStarIntensityMaxId, stars.CustomStarIntensityMax);
            targetMaterial.SetVector(CustomStarIntensityMinId, stars.CustomStarIntensityMin);
            targetMaterial.SetFloat(CustomStarScatterIntervalId, stars.CustomStarScatterInterval);
            var panoramicClouds = settings.PanoramicClouds;
            targetMaterial.SetTexture(
                PanoramicCloudDefaultTextureId,
                panoramicClouds.DefaultTexture != null ? panoramicClouds.DefaultTexture : Texture2D.blackTexture);
            targetMaterial.SetTexture(
                PanoramicCloudPreviousTextureId,
                panoramicClouds.PreviousWeatherTexture != null ? panoramicClouds.PreviousWeatherTexture : Texture2D.blackTexture);
            targetMaterial.SetTexture(
                PanoramicCloudCurrentTextureId,
                panoramicClouds.CurrentWeatherTexture != null ? panoramicClouds.CurrentWeatherTexture : Texture2D.blackTexture);
            targetMaterial.SetVector(PanoramicCloudControlId, new Vector4(
                panoramicClouds.Enabled ? 1f : 0f,
                panoramicClouds.UseDefaultTexture ? 1f : 0f,
                panoramicClouds.TextureInTransition ? 1f : 0f,
                panoramicClouds.TextureTransition));
            targetMaterial.SetVector(PanoramicCloudUvParamsId, new Vector4(
                panoramicClouds.DayUvOffset,
                panoramicClouds.NightUvOffset,
                panoramicClouds.RotationSpeed,
                panoramicClouds.IgnoreTimeOfDayColors ? 1f : 0f));
            targetMaterial.SetVector(PanoramicCloudLuminanceParamsId, new Vector4(
                panoramicClouds.SunnyLuminance,
                panoramicClouds.NightLuminance,
                panoramicClouds.Alpha,
                0f));
            targetMaterial.SetColor(PanoramicCloudBaseColorId, panoramicClouds.BaseColor);
            targetMaterial.SetColor(PanoramicCloudDetailSpecularId, panoramicClouds.DetailSpecular);
            var desaturation = settings.PhysicalSkyDesaturation;
            targetMaterial.SetVector(PhysicalSkyDesaturationParamsId, new Vector4(
                desaturation.Blend,
                desaturation.SkyIntensity,
                desaturation.CloudIntensity,
                desaturation.ForceEnabled ? 1f : 0f));
            targetMaterial.SetColor(PhysicalSkyDesaturationColorId, desaturation.Color);
            var weather = settings.Weather;
            targetMaterial.SetTexture(
                WeatherCoverageTextureId,
                weather.CoverageTexture != null ? weather.CoverageTexture : Texture2D.whiteTexture);
            targetMaterial.SetVector(WeatherWeightsId, new Vector4(
                weather.RainIntensity,
                weather.RainWetCoverage,
                weather.SnowIntensity,
                weather.SnowCoverage));
            targetMaterial.SetVector(WeatherShadowParamsId, new Vector4(
                weather.CloudShadowMarchDistance,
                weather.Enabled ? 1f : 0f,
                0f,
                0f));
            targetMaterial.SetColor(WeatherShadowBrightId, weather.CloudShadowBright);
            targetMaterial.SetColor(WeatherShadowDarkId, weather.CloudShadowDark);
            // XY are retained for the analytic fallback only; the physical LUT
            // follows XRender's density/luminance/sampling-scale controls.
            targetMaterial.SetVector(AerialPerspectiveParamsId, new Vector4(settings.AerialPerspectiveIntensity, settings.AerialPerspectiveDistance, settings.AerialPerspectiveHeightFalloff, settings.AerialPerspectiveEnabled ? 1f : 0f));
            targetMaterial.SetColor(AerialPerspectiveTintId, settings.AerialPerspectiveTint);
            targetMaterial.SetVector(FogLutDistanceParamsId, new Vector4(
                settings.WorldToKilometers,
                BurtAtmosphereLutUtility.FogLutCoverageKm,
                settings.AerialPerspectiveSamplingDistanceScale,
                settings.AerialPerspectiveLuminanceScale));
            var affectsSkyPixels = settings.AerialPerspectivePlacement == AtmosphereAerialPerspectivePlacement.AfterSkyBeforeSSR
                || settings.AerialPerspectivePlacement == AtmosphereAerialPerspectivePlacement.BeforeTransparent;
            targetMaterial.SetVector(AerialPerspectiveFadeParamsId, new Vector4(settings.AerialPerspectiveStartDepth, settings.AerialPerspectiveNearFadeEnd, settings.AerialPerspectiveMaxOpacity, affectsSkyPixels ? 1f : 0f));
            targetMaterial.SetMatrix(InverseViewProjectionId, ResolveInverseViewProjection(camera));
            targetMaterial.SetVector(CameraPositionWSId, camera.transform.position);
            targetMaterial.SetFloat(DebugModeId, ResolveDebugMode());
            BurtAtmosphereLutUtility.BindToMaterial(targetMaterial);
        }

        internal static Vector4 ResolveSunDirection(BurtRenderRequest request, BurtAtmosphereSettings settings)
        {
            var direction = settings.SunSource == AtmosphereSunSource.CustomDirection
                ? settings.CustomSunDirection
                : ResolveMainLightDirection(request);

            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = settings.CustomSunDirection;
            }

            direction.Normalize();
            return new Vector4(direction.x, direction.y, direction.z, 0f);
        }

        private static Vector4 ResolvePhysicalSkyTime()
        {
#if UNITY_EDITOR
            var elapsedSeconds = Application.isPlaying
                ? Time.timeAsDouble
                : Time.realtimeSinceStartupAsDouble;
#else
            var elapsedSeconds = Time.timeAsDouble;
#endif
            // XRender stores elapsedSeconds / 20 as a 128-unit block index and
            // remainder in _Time.zw. Its PhysicalSky multiplies this legacy
            // time back by 20 only for raw elapsed-time animation, while stable
            // star phases consume the split pair directly.
            var legacyTime = elapsedSeconds * 0.05;
            var blockIndex = System.Math.Floor(legacyTime / 128.0);
            var blockRemainder = legacyTime - blockIndex * 128.0;
            return new Vector4(
                (float)blockIndex,
                (float)blockRemainder,
                0f,
                0f);
        }

        private static Vector3 ResolveMainLightDirection(BurtRenderRequest request)
        {
            var lightingData = request != null ? request.LightingData : null;
            if (lightingData == null)
            {
                return Vector3.zero;
            }

            return lightingData.MainLightDirection;
        }

        private static float ResolveDebugMode()
        {
            switch (BurtShadingDebugSettings.Mode)
            {
                case BurtShadingDebugMode.AtmosphereRayleigh:
                    return 1f;
                case BurtShadingDebugMode.AtmosphereMie:
                    return 2f;
                case BurtShadingDebugMode.AtmosphereTransmittance:
                    return 3f;
                case BurtShadingDebugMode.AtmosphereAerialTransmittance:
                    return 4f;
                case BurtShadingDebugMode.AtmosphereAerialInscatter:
                    return 5f;
                case BurtShadingDebugMode.AtmosphereAerialFogAmount:
                    return 6f;
                case BurtShadingDebugMode.AtmosphereAerialHeightFade:
                    return 7f;
                case BurtShadingDebugMode.AtmosphereAerialSummary:
                    return 8f;
                case BurtShadingDebugMode.Atmosphere:
                    return 9f;
                case BurtShadingDebugMode.AtmosphereSunDisk:
                    return 10f;
                case BurtShadingDebugMode.AtmosphereSunHalo:
                    return 11f;
                case BurtShadingDebugMode.AtmosphereHorizon:
                    return 12f;
                case BurtShadingDebugMode.AtmosphereGroundBlend:
                    return 13f;
                case BurtShadingDebugMode.AtmosphereViewDirection:
                    return 14f;
                case BurtShadingDebugMode.AtmosphereLutSkyView:
                    return 15f;
                case BurtShadingDebugMode.AtmosphereLutMultipleScattering:
                    return 16f;
                case BurtShadingDebugMode.AtmosphereLutHorizontalScattering:
                    return 17f;
                default:
                    return 0f;
            }
        }

        internal static Mesh ResolveSkyMesh(BurtAtmosphereSettings settings)
        {
            if (settings.PhysicalSkyMesh != null)
            {
                return settings.PhysicalSkyMesh;
            }

            if (defaultSkyMesh == null)
            {
                var meshes = Resources.LoadAll<Mesh>(DefaultSkyMeshResourcePath);
                if (meshes != null && meshes.Length > 0)
                {
                    defaultSkyMesh = meshes[0];
                }
            }

            if (defaultSkyMesh == null && !hasLoggedMissingSkyMesh)
            {
                Debug.LogWarning(
                    "BurtRP could not load the XRender PhysicalSky mesh resource: "
                    + DefaultSkyMeshResourcePath
                    + ". Falling back to XRender's three-vertex procedural draw.");
                hasLoggedMissingSkyMesh = true;
            }

            return defaultSkyMesh;
        }

        internal static Material ResolveSkyMaterial(
            BurtAtmosphereSettings settings,
            bool useMobilePreparedState = false)
        {
            // XRender's PrepareDrawState selects any non-null custom material;
            // its legacy m_UsePhysicalSkyMaterial flag does not gate the draw.
            if (settings.PhysicalSkyMaterial != null)
            {
                return IsMaterialSupported(
                        settings.PhysicalSkyMaterial,
                        1)
                    ? settings.PhysicalSkyMaterial
                    : null;
            }

            return useMobilePreparedState
                ? GetMobileDrawMaterial()
                : GetMaterial();
        }

        internal static bool IsMaterialSupported(
            Material candidate,
            int requiredPassCount)
        {
            return candidate != null &&
                candidate.shader != null &&
                candidate.shader.isSupported &&
                candidate.passCount >= Mathf.Max(1, requiredPassCount);
        }

        internal static Matrix4x4 ResolveSkyMeshLocalToWorld(BurtAtmosphereSettings settings)
        {
            // XRender preserves only the AtmosphereComponent translation and
            // explicitly resets the sky mesh rotation and scale to identity.
            return Matrix4x4.Translate(settings.PhysicalSkyMeshWorldPosition);
        }

        internal static Matrix4x4 ResolveSkyMeshViewProjection(Camera camera)
        {
            var view = camera.worldToCameraMatrix;
            var projection = GL.GetGPUProjectionMatrix(camera.nonJitteredProjectionMatrix, true);
            return projection * view;
        }

        private static Matrix4x4 ResolveInverseViewProjection(Camera camera)
        {
            var view = camera.worldToCameraMatrix;
            var projection = GL.GetGPUProjectionMatrix(camera.nonJitteredProjectionMatrix, true);
            return (projection * view).inverse;
        }

        private static Material GetMaterial()
        {
            if (material != null)
            {
                return material;
            }

            var shader = Shader.Find(BurtAtmosphereUtility.ShaderName);
            if (shader == null ||
                !shader.isSupported ||
                shader.passCount < 3)
            {
                if (!hasLoggedMissingShader)
                {
                    Debug.LogWarning(
                        "BurtRP cannot use shader: " +
                        BurtAtmosphereUtility.ShaderName);
                    hasLoggedMissingShader = true;
                }

                return null;
            }

            material = new Material(shader);
            material.hideFlags = HideFlags.HideAndDontSave;
            return material;
        }

        private static Material GetMobileDrawMaterial()
        {
            return CreateMaterial(ref mobileDrawMaterial, ref hasLoggedMissingShader);
        }

        internal static Material CreateMaterial(ref Material cachedMaterial, ref bool hasLoggedMissingShader)
        {
            if (cachedMaterial != null)
            {
                return cachedMaterial;
            }

            var shader = Shader.Find(BurtAtmosphereUtility.ShaderName);
            if (shader == null)
            {
                if (!hasLoggedMissingShader)
                {
                    Debug.LogWarning("BurtRP could not find shader: " + BurtAtmosphereUtility.ShaderName);
                    hasLoggedMissingShader = true;
                }

                return null;
            }

            cachedMaterial = new Material(shader);
            cachedMaterial.hideFlags = HideFlags.HideAndDontSave;
            return cachedMaterial;
        }
    }

    internal sealed class BurtApplyAtmosphereAerialPerspectivePass : BurtRenderPass
    {
        private static readonly int CameraDepthTextureId = BurtRenderGraphResourceRegistry.CameraDepthTextureId;
        private static readonly int CameraColorTextureId = BurtRenderGraphResourceRegistry.CameraColorTextureId;

        private Material material;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Atmosphere Aerial Perspective";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.FullScreen;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtAtmosphereUtility.ShouldUseAerialPerspective(builder.Request))
            {
                return;
            }

            builder.ReadCameraColor();
            builder.ReadCameraDepth();
            builder.WriteRenderTarget(BurtRenderGraphResourceRegistry.AtmosphereAerialSourceColorName);
            builder.AllowUnconsumedRenderTargetWrite(BurtRenderGraphResourceRegistry.AtmosphereAerialSourceColorName);
            if (BurtLightShaftOcclusionUtility.ShouldUseLightShaftOcclusion(builder.Request))
            {
                builder.ReadRenderTarget(BurtRenderGraphResourceRegistry.LightShaftOcclusionName);
            }
            builder.WriteCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtAtmosphereUtility.ShouldUseAerialPerspective(context.Request))
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

            var settings = BurtAtmosphereUtility.ResolveSettings();
            if (!settings.AerialPerspectiveEnabled)
            {
                return;
            }

            var drawMaterial = BurtDrawAtmospherePass.CreateMaterial(ref material, ref hasLoggedMissingShader);
            if (drawMaterial == null)
            {
                return;
            }

            var cmd = context.AcquireCommandBuffer(Name);
            BurtLightShaftOcclusionUtility.BindForOpaqueFog(cmd, context);
            var sunDirection4 = BurtDrawAtmospherePass.ResolveSunDirection(request, settings);
            BurtAtmosphereLutUtility.EnsureLuts(cmd, camera, settings, new Vector3(sunDirection4.x, sunDirection4.y, sunDirection4.z));
            BurtDrawAtmospherePass.UploadMaterialProperties(drawMaterial, camera, request, settings);
            var descriptor = BurtRenderTargetDescriptorUtility.CreateCameraColorDescriptor(camera);
            context.ResourceRegistry.SetRenderTargetDescriptor(
                BurtRenderGraphResourceRegistry.AtmosphereAerialSourceColorName,
                descriptor,
                FilterMode.Bilinear,
                "Burt Atmosphere Aerial Source Color");
            var aerialSourceTarget = context.ResourceRegistry.AllocateRenderTarget(
                BurtRenderGraphResourceRegistry.AtmosphereAerialSourceColorName);
            if (!aerialSourceTarget.IsValid)
            {
                context.ReleaseCommandBuffer(cmd);
                return;
            }

            cmd.Blit(cameraColorTarget.Identifier, aerialSourceTarget.Identifier);
            cmd.SetRenderTarget(cameraColorTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            cmd.SetGlobalTexture(CameraColorTextureId, aerialSourceTarget.Identifier);
            cmd.SetGlobalTexture(CameraDepthTextureId, cameraDepthTarget.Identifier);
            cmd.DrawProcedural(Matrix4x4.identity, drawMaterial, 1, MeshTopology.Triangles, 3, 1);
            context.ExecuteLegacyCommandBuffer(cmd);
            context.ReleaseCommandBuffer(cmd);
            context.ResourceRegistry.ReleaseRenderTarget(BurtRenderGraphResourceRegistry.AtmosphereAerialSourceColorName);
        }
    }
}
