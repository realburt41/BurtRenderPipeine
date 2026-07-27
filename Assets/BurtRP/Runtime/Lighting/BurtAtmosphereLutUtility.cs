using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    // XRender atmosphere parity cache: the LUT topology mirrors its transmittance,
    // multiple-scattering, sky-view and aerial-fog stages while keeping ownership local to BRP.
    internal static class BurtAtmosphereLutUtility
    {
        private const string ComputeResourcePath = "BurtAtmosphereLut";
        private const int TransmittanceWidth = 256;
        private const int TransmittanceHeight = 64;
        private const int MultipleScatteringSize = 32;
        private const int HorizontalScatteringWidth = 3;
        private const int HorizontalScatteringHeight = 1;
        private const int SkyViewWidth = 192;
        private const int SkyViewHeight = 104;
        private const int FogWidth = 32;
        private const int FogHeight = 32;
        private const int FogDepth = 16;
        private const GraphicsFormat PackedScatteringFormat = GraphicsFormat.B10G11R11_UFloatPack32;
        private const GraphicsFormat HighPrecisionFormat = GraphicsFormat.R16G16B16A16_SFloat;
        // XRender offsets planet-surface evaluations by 5m to avoid unstable sphere intersections at the ground boundary.
        private const float GroundRadiusOffsetKm = 0.005f;

        private static readonly int TransmittanceLutId = Shader.PropertyToID("_BurtAtmosphereTransmittanceLut");
        private static readonly int MultipleScatteringLutId = Shader.PropertyToID("_BurtAtmosphereMultipleScatteringLut");
        private static readonly int HorizontalScatteringLutId = Shader.PropertyToID("_BurtAtmosphereHorizontalScatteringLut");
        private static readonly int SkyViewLutId = Shader.PropertyToID("_BurtAtmosphereSkyViewLut");
        private static readonly int FogLutId = Shader.PropertyToID("_BurtAtmosphereFogLut");
        private static readonly int UseLutsId = Shader.PropertyToID("_BurtAtmosphereUseLuts");
        private static readonly int TransparentFogEnabledId = Shader.PropertyToID("_BurtTransparentAtmosphereFogEnabled");
        private static readonly int TransparentFogLightColorId = Shader.PropertyToID("_BurtTransparentAtmosphereFogLightColor");
        private static readonly int TransparentFogDistanceParamsId = Shader.PropertyToID("_BurtTransparentAtmosphereFogDistanceParams");
        private static readonly int TransparentFogFadeParamsId = Shader.PropertyToID("_BurtTransparentAtmosphereFogFadeParams");
        private static readonly int TransparentFogWorldToLocalId = Shader.PropertyToID("_BurtTransparentAtmosphereWorldToLocal");
        private static readonly int RayleighIntensityId = Shader.PropertyToID("_BurtAtmosphereRayleighIntensity");
        private static readonly int MieIntensityId = Shader.PropertyToID("_BurtAtmosphereMieIntensity");
        private static readonly int MieAnisotropyId = Shader.PropertyToID("_BurtAtmosphereMieAnisotropy");
        private static readonly int RayleighScatteringCoefficientId = Shader.PropertyToID("_BurtAtmosphereRayleighScatteringCoefficient");
        private static readonly int MieScatteringCoefficientId = Shader.PropertyToID("_BurtAtmosphereMieScatteringCoefficient");
        private static readonly int MieAbsorptionCoefficientId = Shader.PropertyToID("_BurtAtmosphereMieAbsorptionCoefficient");
        private static readonly int OzoneParamsId = Shader.PropertyToID("_BurtAtmosphereOzoneParams");
        private static readonly int OzoneAbsorptionCoefficientId = Shader.PropertyToID("_BurtAtmosphereOzoneAbsorptionCoefficient");
        private static readonly int GroundAlbedoId = Shader.PropertyToID("_BurtAtmosphereGroundAlbedo");
        private static readonly int MultipleScatteringIntensityId = Shader.PropertyToID("_BurtAtmosphereMultipleScatteringIntensity");
        private static readonly int TraceSampleCountScaleId = Shader.PropertyToID("_BurtAtmosphereTraceSampleCountScale");
        private static readonly int PlanetParamsId = Shader.PropertyToID("_BurtAtmospherePlanetParams");
        private static readonly int SunDirectionId = Shader.PropertyToID("_BurtAtmosphereSunDirection");
        private static readonly int FogViewRayBottomLeftId = Shader.PropertyToID("_BurtAtmosphereFogViewRayBottomLeft");
        private static readonly int FogViewRayBottomRightId = Shader.PropertyToID("_BurtAtmosphereFogViewRayBottomRight");
        private static readonly int FogViewRayTopLeftId = Shader.PropertyToID("_BurtAtmosphereFogViewRayTopLeft");
        private static readonly int FogViewRayTopRightId = Shader.PropertyToID("_BurtAtmosphereFogViewRayTopRight");
        private static readonly int HorizontalFogSunDirectionId = Shader.PropertyToID("_BurtAtmosphereHorizontalFogSunDirection");
        private static readonly int HorizontalFogLightColorId = Shader.PropertyToID("_BurtAtmosphereHorizontalFogLightColor");
        private static readonly int MainLightOcclusionFactorId = Shader.PropertyToID("_BurtMainLightOcclusionFactor");
        private static readonly int HorizontalFogUsesMainLightId = Shader.PropertyToID("_BurtAtmosphereHorizontalFogUsesMainLight");
        private static readonly int SkyLuminanceFactorId = Shader.PropertyToID("_BurtAtmosphereSkyLuminanceFactor");
        private static readonly int AerialLutParamsId = Shader.PropertyToID("_BurtAtmosphereAerialLutParams");
        private static readonly int CameraAltitudeKmId = Shader.PropertyToID("_BurtAtmosphereCameraAltitudeKm");
        private static readonly int CameraAltitude01Id = Shader.PropertyToID("_BurtAtmosphereCameraAltitude01");
        private static readonly int WorldToSkyViewLocalId = Shader.PropertyToID("_BurtAtmosphereWorldToSkyViewLocal");
        private static readonly int TransmittanceOutputId = Shader.PropertyToID("_BurtAtmosphereTransmittanceLut");
        private static readonly int MultipleScatteringOutputId = Shader.PropertyToID("_BurtAtmosphereMultipleScatteringLut");
        private static readonly int HorizontalScatteringOutputId = Shader.PropertyToID("_BurtAtmosphereHorizontalScatteringLut");
        private static readonly int SkyViewOutputId = Shader.PropertyToID("_BurtAtmosphereSkyViewLut");
        private static readonly int FogOutputId = Shader.PropertyToID("_BurtAtmosphereFogLut");
        private static readonly int TransmittanceInputId = Shader.PropertyToID("_BurtAtmosphereTransmittanceLutInput");
        private static readonly int MultipleScatteringInputId = Shader.PropertyToID("_BurtAtmosphereMultipleScatteringLutInput");

        private static ComputeShader computeShader;
        private static int transmittanceKernel = -1;
        private static int multipleScatteringKernel = -1;
        private static int horizontalScatteringKernel = -1;
        private static int skyViewKernel = -1;
        private static int fogKernel = -1;
        private static RenderTexture transmittanceLut;
        private static RenderTexture multipleScatteringLut;
        private static RenderTexture horizontalScatteringLut;
        private static RenderTexture skyViewLut;
        private static RenderTexture fogLut;
        private static GraphicsFormat transmittanceFormat = GraphicsFormat.None;
        private static GraphicsFormat multipleScatteringFormat = GraphicsFormat.None;
        private static GraphicsFormat horizontalScatteringFormat = GraphicsFormat.None;
        private static GraphicsFormat skyViewFormat = GraphicsFormat.None;
        private static GraphicsFormat fogFormat = GraphicsFormat.None;
        private static int lastPhysicalStateHash;
        private static int lastSkyStateHash;
        private static int lastFogStateHash;
        private static float cameraAltitudeRatio;
        private static Matrix4x4 worldToSkyViewLocal = Matrix4x4.identity;
        private static bool hasPhysicalStateHash;
        private static bool hasSkyStateHash;
        private static bool hasFogStateHash;
        private static bool initializationFailed;

        public static void EnsureLuts(CommandBuffer cmd, Camera camera, BurtAtmosphereSettings settings, Vector3 sunDirection)
        {
            if (cmd == null || !SystemInfo.supportsComputeShaders || !EnsureResources())
            {
                return;
            }

            ResolveCameraFrame(camera, settings, out var cameraAltitudeKm, out worldToSkyViewLocal);
            var localSunDirection = worldToSkyViewLocal.MultiplyVector(sunDirection).normalized;
            ResolveFogViewRays(
                camera,
                worldToSkyViewLocal,
                out var fogViewRayBottomLeft,
                out var fogViewRayBottomRight,
                out var fogViewRayTopLeft,
                out var fogViewRayTopRight);
            var physicalStateHash = CalculatePhysicalStateHash(settings);
            var skyStateHash = CalculateSkyStateHash(settings, localSunDirection, cameraAltitudeKm);
            var fogStateHash = CalculateFogStateHash(
                settings,
                localSunDirection,
                cameraAltitudeKm,
                fogViewRayBottomLeft,
                fogViewRayBottomRight,
                fogViewRayTopLeft,
                fogViewRayTopRight);
            var physicalDirty = !hasPhysicalStateHash || physicalStateHash != lastPhysicalStateHash;
            var skyDirty = physicalDirty || !hasSkyStateHash || skyStateHash != lastSkyStateHash;
            var fogDirty = physicalDirty || !hasFogStateHash || fogStateHash != lastFogStateHash;
            if (!physicalDirty && !skyDirty && !fogDirty)
            {
                return;
            }

            UploadParameters(
                settings,
                localSunDirection,
                cameraAltitudeKm,
                fogViewRayBottomLeft,
                fogViewRayBottomRight,
                fogViewRayTopLeft,
                fogViewRayTopRight);
            if (physicalDirty)
            {
                if (!Dispatch(cmd, transmittanceKernel, TransmittanceWidth, TransmittanceHeight, 1)
                    || !Dispatch(cmd, multipleScatteringKernel, MultipleScatteringSize, MultipleScatteringSize, 1))
                {
                    return;
                }

                lastPhysicalStateHash = physicalStateHash;
                hasPhysicalStateHash = true;
            }

            if (skyDirty)
            {
                if (!Dispatch(cmd, horizontalScatteringKernel, 1, 1, 1)
                    || !Dispatch(cmd, skyViewKernel, SkyViewWidth, SkyViewHeight, 1))
                {
                    return;
                }

                lastSkyStateHash = skyStateHash;
                hasSkyStateHash = true;
            }

            if (fogDirty)
            {
                if (!Dispatch(cmd, fogKernel, FogWidth, FogHeight, FogDepth))
                {
                    return;
                }

                lastFogStateHash = fogStateHash;
                hasFogStateHash = true;
            }
        }

        public static void BindToMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            var available = AreLutsReady();
            material.SetFloat(UseLutsId, available ? 1f : 0f);
            material.SetFloat(CameraAltitude01Id, cameraAltitudeRatio);
            material.SetMatrix(WorldToSkyViewLocalId, worldToSkyViewLocal);
            if (!available)
            {
                return;
            }

            material.SetTexture(TransmittanceLutId, transmittanceLut);
            material.SetTexture(MultipleScatteringLutId, multipleScatteringLut);
            material.SetTexture(HorizontalScatteringLutId, horizontalScatteringLut);
            material.SetTexture(SkyViewLutId, skyViewLut);
            material.SetTexture(FogLutId, fogLut);
        }

        public static bool BindHorizontalScatteringToCompute(
            CommandBuffer cmd,
            ComputeShader shader,
            int kernel,
            int texturePropertyId)
        {
            if (cmd == null || shader == null || kernel < 0 || !AreLutsReady())
            {
                return false;
            }

            cmd.SetComputeTextureParam(shader, kernel, texturePropertyId, horizontalScatteringLut);
            return true;
        }

        public static void EnsureAndBindForTransparentFog(CommandBuffer cmd, Camera camera, BurtRenderRequest request)
        {
            if (cmd == null)
            {
                return;
            }

            BurtFogUtility.UploadTransparentGlobals(cmd, request);
            var settings = BurtAtmosphereUtility.ResolveSettings();
            var aerialEnabled = camera != null &&
                request != null &&
                BurtAtmosphereUtility.ShouldUseAerialPerspective(request);
            var fogSettings = BurtFogUtility.ResolveSettings(request);
            var heightFogNeedsAtmosphereLuts = camera != null &&
                request != null &&
                BurtFogUtility.ShouldUseFog(request) &&
                fogSettings.HorizontalScattering.Enabled;
            var needsAtmosphereLuts = settings.Enabled && (aerialEnabled || heightFogNeedsAtmosphereLuts);
            if (!needsAtmosphereLuts)
            {
                cmd.SetGlobalFloat(TransparentFogEnabledId, 0f);
                cmd.SetGlobalFloat(UseLutsId, 0f);
                return;
            }

            var sunDirection4 = BurtDrawAtmospherePass.ResolveSunDirection(request, settings);
            var sunDirection = new Vector3(sunDirection4.x, sunDirection4.y, sunDirection4.z);
            EnsureLuts(cmd, camera, settings, sunDirection);

            var available = AreLutsReady();
            cmd.SetGlobalFloat(TransparentFogEnabledId, aerialEnabled && available ? 1f : 0f);
            cmd.SetGlobalFloat(UseLutsId, available ? 1f : 0f);
            if (!available)
            {
                return;
            }

            cmd.SetGlobalTexture(HorizontalScatteringLutId, horizontalScatteringLut);
            cmd.SetGlobalColor(SkyLuminanceFactorId, settings.SkyLuminanceFactor);

            const float fogLutCoverageKm = 96f;
            var lightingData = request.LightingData;
            var outerSpaceLight = lightingData != null ? lightingData.MainLightColorOuterSpace : Color.white;
            var lightScale = Mathf.Max(0f, settings.SunIntensity) *
                Mathf.Clamp01(settings.MainLightOcclusion) *
                Mathf.Max(0f, settings.AerialPerspectiveLuminanceScale);
            var tint = settings.AerialPerspectiveTint;
            cmd.SetGlobalVector(TransparentFogLightColorId, new Vector4(
                Mathf.Max(0f, outerSpaceLight.r) * Mathf.Max(0f, tint.r) * lightScale,
                Mathf.Max(0f, outerSpaceLight.g) * Mathf.Max(0f, tint.g) * lightScale,
                Mathf.Max(0f, outerSpaceLight.b) * Mathf.Max(0f, tint.b) * lightScale,
                0f));
            cmd.SetGlobalVector(TransparentFogDistanceParamsId, new Vector4(
                settings.WorldToKilometers,
                settings.AerialPerspectiveSamplingDistanceScale,
                settings.AerialPerspectiveNearFadeStart,
                fogLutCoverageKm));
            cmd.SetGlobalVector(TransparentFogFadeParamsId, new Vector4(
                settings.AerialPerspectiveNearFadeEnd,
                settings.AerialPerspectiveMaxOpacity,
                settings.AerialPerspectiveHeightFalloff,
                0f));
            cmd.SetGlobalMatrix(TransparentFogWorldToLocalId, worldToSkyViewLocal);
            cmd.SetGlobalTexture(FogLutId, fogLut);
        }

        public static void EnsureAndBindForFog(CommandBuffer cmd, Material material, Camera camera, BurtRenderRequest request)
        {
            if (material == null)
            {
                return;
            }

            var settings = BurtAtmosphereUtility.ResolveSettings();
            if (!settings.Enabled || camera == null || request == null)
            {
                material.SetFloat(UseLutsId, 0f);
                material.SetVector(HorizontalFogSunDirectionId, Vector4.zero);
                material.SetVector(HorizontalFogLightColorId, Vector4.zero);
                material.SetFloat(MainLightOcclusionFactorId, 1f);
                material.SetFloat(HorizontalFogUsesMainLightId, 0f);
                return;
            }

            var sunDirection4 = BurtDrawAtmospherePass.ResolveSunDirection(request, settings);
            var sunDirection = new Vector3(sunDirection4.x, sunDirection4.y, sunDirection4.z);
            var lightingData = request.LightingData;
            var outerSpaceLightColor = lightingData != null ? lightingData.MainLightColorOuterSpace : Color.white;
            var sunColor = outerSpaceLightColor * settings.SunIntensity;
            EnsureLuts(cmd, camera, settings, sunDirection);
            BindToMaterial(material);

            var volumetricScale = lightingData != null && lightingData.HasMainLight
                ? Mathf.Max(0f, lightingData.MainLightVolumetricScatteringIntensityScale)
                : 1f;
            material.SetVector(HorizontalFogSunDirectionId, sunDirection4);
            material.SetVector(HorizontalFogLightColorId, new Vector4(sunColor.r, sunColor.g, sunColor.b, volumetricScale));
            material.SetFloat(MainLightOcclusionFactorId, settings.MainLightOcclusion);
            material.SetFloat(HorizontalFogUsesMainLightId, settings.SunSource == AtmosphereSunSource.MainLight ? 1f : 0f);
            material.SetColor(SkyLuminanceFactorId, settings.SkyLuminanceFactor);
        }

        public static string FormatDebugState()
        {
            return string.Concat(
                "ComputeSupported=", SystemInfo.supportsComputeShaders,
                " InitFailed=", initializationFailed,
                " PhysicalState=", hasPhysicalStateHash,
                " PhysicalHash=", lastPhysicalStateHash,
                " SkyState=", hasSkyStateHash,
                " SkyHash=", lastSkyStateHash,
                " FogState=", hasFogStateHash,
                " FogHash=", lastFogStateHash,
                " RadianceContract=NormalizedConsumerMainLight",
                " FogCoordinates=CameraScreenFrustum",
                " CameraAltitudeRatio=", cameraAltitudeRatio.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                " PackedScatteringSupported=", SupportsWritableSampledFormat(PackedScatteringFormat),
                " Transmittance=", FormatTextureState(transmittanceLut, TransmittanceWidth, TransmittanceHeight, 1, transmittanceFormat),
                " Multi=", FormatTextureState(multipleScatteringLut, MultipleScatteringSize, MultipleScatteringSize, 1, multipleScatteringFormat),
                " Horizontal=", FormatTextureState(horizontalScatteringLut, HorizontalScatteringWidth, HorizontalScatteringHeight, 1, horizontalScatteringFormat),
                " SkyView=", FormatTextureState(skyViewLut, SkyViewWidth, SkyViewHeight, 1, skyViewFormat),
                " Fog=", FormatTextureState(fogLut, FogWidth, FogHeight, FogDepth, fogFormat),
                " TotalLutMemory=", FormatMemoryBytes(
                    CalculateTextureMemoryBytes(transmittanceLut)
                    + CalculateTextureMemoryBytes(multipleScatteringLut)
                    + CalculateTextureMemoryBytes(horizontalScatteringLut)
                    + CalculateTextureMemoryBytes(skyViewLut)
                    + CalculateTextureMemoryBytes(fogLut)));
        }

        public static void Release()
        {
            ReleaseTexture(ref transmittanceLut);
            ReleaseTexture(ref multipleScatteringLut);
            ReleaseTexture(ref horizontalScatteringLut);
            ReleaseTexture(ref skyViewLut);
            ReleaseTexture(ref fogLut);
            computeShader = null;
            transmittanceKernel = -1;
            multipleScatteringKernel = -1;
            horizontalScatteringKernel = -1;
            skyViewKernel = -1;
            fogKernel = -1;
            hasPhysicalStateHash = false;
            hasSkyStateHash = false;
            hasFogStateHash = false;
            lastPhysicalStateHash = 0;
            lastSkyStateHash = 0;
            lastFogStateHash = 0;
            cameraAltitudeRatio = 0f;
            worldToSkyViewLocal = Matrix4x4.identity;
            transmittanceFormat = GraphicsFormat.None;
            multipleScatteringFormat = GraphicsFormat.None;
            horizontalScatteringFormat = GraphicsFormat.None;
            skyViewFormat = GraphicsFormat.None;
            fogFormat = GraphicsFormat.None;
            initializationFailed = false;
        }

        private static bool EnsureResources()
        {
            if (initializationFailed)
            {
                return false;
            }

            if (!SupportsWritableSampledFormat(HighPrecisionFormat))
            {
                return FailInitialization("BurtRP atmosphere LUTs require sampled R16G16B16A16_SFloat LoadStore support; the analytic atmosphere fallback remains active.");
            }

            // XRender stores RGB-only scattering LUTs in packed 32-bit float textures.
            // Use the same layout when typed UAV writes and filtered sampling are both
            // supported; otherwise retain the existing 64-bit RGBA fallback per device.
            var packedScatteringSupported = SupportsWritableSampledFormat(PackedScatteringFormat);
            var selectedScatteringFormat = packedScatteringSupported ? PackedScatteringFormat : HighPrecisionFormat;
            transmittanceFormat = selectedScatteringFormat;
            multipleScatteringFormat = selectedScatteringFormat;
            skyViewFormat = selectedScatteringFormat;
            // Horizontal scattering is three float4 values in XRender's paired buffers,
            // while BRP represents it as a 3x1 texture. Preserve all four channels here.
            horizontalScatteringFormat = HighPrecisionFormat;
            // Aerial perspective stores RGB in-scattering plus scalar transmittance in A.
            fogFormat = HighPrecisionFormat;

            if (computeShader == null)
            {
                computeShader = Resources.Load<ComputeShader>(ComputeResourcePath);
                if (computeShader == null)
                {
                    return FailInitialization("BurtRP could not find atmosphere LUT compute shader: " + ComputeResourcePath);
                }

                if (!TryFindKernel("BuildTransmittanceLut", out transmittanceKernel)
                    || !TryFindKernel("BuildMultipleScatteringLut", out multipleScatteringKernel)
                    || !TryFindKernel("BuildHorizontalScatteringLut", out horizontalScatteringKernel)
                    || !TryFindKernel("BuildSkyViewLut", out skyViewKernel)
                    || !TryFindKernel("BuildFogLut", out fogKernel))
                {
                    return false;
                }
            }

            var physicalResourcesRecreated = false;
            var skyResourcesRecreated = false;
            var fogResourcesRecreated = false;
            transmittanceLut = EnsureTexture(transmittanceLut, TransmittanceWidth, TransmittanceHeight, 1, TextureDimension.Tex2D, transmittanceFormat, "Burt Atmosphere Transmittance LUT", out var transmittanceRecreated);
            multipleScatteringLut = EnsureTexture(multipleScatteringLut, MultipleScatteringSize, MultipleScatteringSize, 1, TextureDimension.Tex2D, multipleScatteringFormat, "Burt Atmosphere Multiple Scattering LUT", out var multipleScatteringRecreated);
            horizontalScatteringLut = EnsureTexture(horizontalScatteringLut, HorizontalScatteringWidth, HorizontalScatteringHeight, 1, TextureDimension.Tex2D, horizontalScatteringFormat, "Burt Atmosphere Horizontal Scattering LUT", out var horizontalScatteringRecreated);
            skyViewLut = EnsureTexture(skyViewLut, SkyViewWidth, SkyViewHeight, 1, TextureDimension.Tex2D, skyViewFormat, "Burt Atmosphere Sky View LUT", out var skyViewRecreated);
            fogLut = EnsureTexture(fogLut, FogWidth, FogHeight, FogDepth, TextureDimension.Tex3D, fogFormat, "Burt Atmosphere Fog LUT", out fogResourcesRecreated);
            physicalResourcesRecreated = transmittanceRecreated || multipleScatteringRecreated;
            skyResourcesRecreated = horizontalScatteringRecreated || skyViewRecreated;
            if (physicalResourcesRecreated)
            {
                hasPhysicalStateHash = false;
                hasSkyStateHash = false;
                hasFogStateHash = false;
            }
            else
            {
                hasSkyStateHash &= !skyResourcesRecreated;
                hasFogStateHash &= !fogResourcesRecreated;
            }
            if (!AreLutsReady())
            {
                return FailInitialization("BurtRP could not create one or more atmosphere LUT textures; the analytic atmosphere fallback remains active.");
            }

            try
            {
                computeShader.SetTexture(transmittanceKernel, TransmittanceOutputId, transmittanceLut);
                computeShader.SetTexture(multipleScatteringKernel, MultipleScatteringOutputId, multipleScatteringLut);
                computeShader.SetTexture(multipleScatteringKernel, TransmittanceInputId, transmittanceLut);
                computeShader.SetTexture(horizontalScatteringKernel, HorizontalScatteringOutputId, horizontalScatteringLut);
                computeShader.SetTexture(skyViewKernel, SkyViewOutputId, skyViewLut);
                computeShader.SetTexture(fogKernel, FogOutputId, fogLut);
                computeShader.SetTexture(skyViewKernel, TransmittanceInputId, transmittanceLut);
                computeShader.SetTexture(skyViewKernel, MultipleScatteringInputId, multipleScatteringLut);
                computeShader.SetTexture(horizontalScatteringKernel, TransmittanceInputId, transmittanceLut);
                computeShader.SetTexture(horizontalScatteringKernel, MultipleScatteringInputId, multipleScatteringLut);
                computeShader.SetTexture(fogKernel, TransmittanceInputId, transmittanceLut);
                computeShader.SetTexture(fogKernel, MultipleScatteringInputId, multipleScatteringLut);
                return true;
            }
            catch (System.Exception exception)
            {
                return FailInitialization("BurtRP could not bind atmosphere LUT compute resources; the analytic atmosphere fallback remains active. " + exception.Message);
            }
        }

        private static RenderTexture EnsureTexture(RenderTexture texture, int width, int height, int depth, TextureDimension dimension, GraphicsFormat format, string name, out bool recreated)
        {
            if (texture != null
                && texture.IsCreated()
                && texture.width == width
                && texture.height == height
                && texture.volumeDepth == depth
                && texture.dimension == dimension
                && texture.graphicsFormat == format)
            {
                recreated = false;
                return texture;
            }

            recreated = true;
            ReleaseTexture(ref texture);
            var descriptor = new RenderTextureDescriptor(width, height, format, 0)
            {
                dimension = dimension,
                volumeDepth = depth,
                enableRandomWrite = true,
                msaaSamples = 1,
                sRGB = false,
                useMipMap = false,
                autoGenerateMips = false
            };
            texture = new RenderTexture(descriptor)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.Create();
            return texture;
        }

        private static void UploadParameters(
            BurtAtmosphereSettings settings,
            Vector3 sunDirection,
            float cameraAltitudeKm,
            Vector3 fogViewRayBottomLeft,
            Vector3 fogViewRayBottomRight,
            Vector3 fogViewRayTopLeft,
            Vector3 fogViewRayTopRight)
        {
            computeShader.SetFloat(RayleighIntensityId, settings.RayleighIntensity);
            computeShader.SetFloat(MieIntensityId, settings.MieIntensity);
            computeShader.SetFloat(MieAnisotropyId, settings.MieAnisotropy);
            computeShader.SetVector(RayleighScatteringCoefficientId, settings.RayleighScatteringCoefficient);
            computeShader.SetVector(MieScatteringCoefficientId, settings.MieScatteringCoefficient);
            computeShader.SetVector(MieAbsorptionCoefficientId, settings.MieAbsorptionCoefficient);
            computeShader.SetVector(OzoneParamsId, new Vector4(settings.OzoneAbsorptionIntensity, settings.OzoneLayerCenter, settings.OzoneLayerThickness, 0f));
            computeShader.SetVector(OzoneAbsorptionCoefficientId, settings.OzoneAbsorptionCoefficient);
            computeShader.SetVector(GroundAlbedoId, settings.GroundAlbedo.linear);
            computeShader.SetFloat(MultipleScatteringIntensityId, settings.MultipleScatteringIntensity);
            computeShader.SetFloat(TraceSampleCountScaleId, settings.TraceSampleCountScale);
            computeShader.SetVector(PlanetParamsId, new Vector4(settings.PlanetRadius, settings.AtmosphereHeight, settings.RayleighScaleHeight, settings.MieScaleHeight));
            computeShader.SetVector(SunDirectionId, sunDirection.normalized);
            computeShader.SetVector(FogViewRayBottomLeftId, fogViewRayBottomLeft);
            computeShader.SetVector(FogViewRayBottomRightId, fogViewRayBottomRight);
            computeShader.SetVector(FogViewRayTopLeftId, fogViewRayTopLeft);
            computeShader.SetVector(FogViewRayTopRightId, fogViewRayTopRight);
            // Compute-space fog distances are physical kilometers; material-space aerial parameters remain world units.
            computeShader.SetVector(AerialLutParamsId, new Vector4(
                settings.AerialPerspectiveDensityScale,
                96f,
                settings.AerialPerspectiveHeightFalloff,
                settings.AerialPerspectiveNearFadeStart * settings.WorldToKilometers));
            computeShader.SetFloat(CameraAltitudeKmId, cameraAltitudeKm);
            // Keep the unclamped ratio. XRender preserves the real geocentric camera radius
            // so SkyView/Fog rays can enter the atmosphere from space instead of pretending
            // every camera above the top boundary is still just inside it.
            cameraAltitudeRatio = cameraAltitudeKm / Mathf.Max(settings.AtmosphereHeight, 0.001f);
        }

        private static bool Dispatch(CommandBuffer cmd, int kernel, int width, int height, int depth)
        {
            if (cmd == null || computeShader == null || kernel < 0)
            {
                return FailInitialization("BurtRP tried to dispatch an invalid atmosphere LUT kernel; the analytic atmosphere fallback remains active.");
            }

            try
            {
                computeShader.GetKernelThreadGroupSizes(kernel, out var threadX, out var threadY, out var threadZ);
                if (threadX == 0 || threadY == 0 || threadZ == 0)
                {
                    return FailInitialization("BurtRP atmosphere LUT kernel has an invalid thread-group size; the analytic atmosphere fallback remains active.");
                }

                cmd.DispatchCompute(computeShader, kernel,
                    Mathf.CeilToInt(width / (float)threadX),
                    Mathf.CeilToInt(height / (float)threadY),
                    Mathf.CeilToInt(depth / (float)threadZ));
                return true;
            }
            catch (System.Exception exception)
            {
                return FailInitialization("BurtRP could not dispatch an atmosphere LUT kernel; the analytic atmosphere fallback remains active. " + exception.Message);
            }
        }

        private static bool AreLutsReady()
        {
            return !initializationFailed
                && transmittanceLut != null && transmittanceLut.IsCreated()
                && multipleScatteringLut != null && multipleScatteringLut.IsCreated()
                && horizontalScatteringLut != null && horizontalScatteringLut.IsCreated()
                && skyViewLut != null && skyViewLut.IsCreated()
                && fogLut != null && fogLut.IsCreated();
        }

        private static bool TryFindKernel(string kernelName, out int kernel)
        {
            kernel = -1;
            try
            {
                kernel = computeShader.FindKernel(kernelName);
                computeShader.GetKernelThreadGroupSizes(kernel, out var threadX, out var threadY, out var threadZ);
                if (threadX == 0 || threadY == 0 || threadZ == 0)
                {
                    return FailInitialization("BurtRP atmosphere LUT kernel has an invalid thread-group size: " + kernelName + ". The analytic atmosphere fallback remains active.");
                }

                return true;
            }
            catch (System.Exception exception)
            {
                return FailInitialization("BurtRP could not load atmosphere LUT kernel '" + kernelName + "'; the analytic atmosphere fallback remains active. " + exception.Message);
            }
        }

        private static bool FailInitialization(string message)
        {
            if (!initializationFailed)
            {
                Debug.LogWarning(message);
            }

            ReleaseTexture(ref transmittanceLut);
            ReleaseTexture(ref multipleScatteringLut);
            ReleaseTexture(ref horizontalScatteringLut);
            ReleaseTexture(ref skyViewLut);
            ReleaseTexture(ref fogLut);
            hasPhysicalStateHash = false;
            hasSkyStateHash = false;
            hasFogStateHash = false;
            initializationFailed = true;
            return false;
        }

        private static string FormatTextureState(RenderTexture texture, int width, int height, int depth, GraphicsFormat selectedFormat)
        {
            var expected = string.Concat(width, "x", height, depth > 1 ? "x" + depth : string.Empty);
            if (texture == null)
            {
                return string.Concat(expected, "@", FormatGraphicsFormat(selectedFormat), ":Null");
            }

            return string.Concat(
                expected,
                "@", texture.graphicsFormat,
                texture.graphicsFormat == selectedFormat ? string.Empty : "(Expected=" + FormatGraphicsFormat(selectedFormat) + ")",
                texture.IsCreated() ? ":Ready:" : ":NotCreated:",
                FormatMemoryBytes(CalculateTextureMemoryBytes(texture)));
        }

        private static bool SupportsWritableSampledFormat(GraphicsFormat format)
        {
            return SystemInfo.IsFormatSupported(format, FormatUsage.LoadStore)
                && SystemInfo.IsFormatSupported(format, FormatUsage.Sample);
        }

        private static string FormatGraphicsFormat(GraphicsFormat format)
        {
            return format == GraphicsFormat.None ? "Unselected" : format.ToString();
        }

        private static long CalculateTextureMemoryBytes(RenderTexture texture)
        {
            if (texture == null)
            {
                return 0L;
            }

            return (long)texture.width
                * texture.height
                * Mathf.Max(texture.volumeDepth, 1)
                * GraphicsFormatUtility.GetBlockSize(texture.graphicsFormat);
        }

        private static string FormatMemoryBytes(long bytes)
        {
            if (bytes < 1024L)
            {
                return bytes.ToString(System.Globalization.CultureInfo.InvariantCulture) + "B";
            }

            if (bytes < 1024L * 1024L)
            {
                return (bytes / 1024.0).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "KiB";
            }

            return (bytes / (1024.0 * 1024.0)).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "MiB";
        }

        private static int CalculatePhysicalStateHash(BurtAtmosphereSettings settings)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + settings.RayleighIntensity.GetHashCode();
                hash = hash * 31 + settings.MieIntensity.GetHashCode();
                hash = hash * 31 + settings.RayleighScatteringCoefficient.GetHashCode();
                hash = hash * 31 + settings.MieScatteringCoefficient.GetHashCode();
                hash = hash * 31 + settings.MieAbsorptionCoefficient.GetHashCode();
                hash = hash * 31 + settings.OzoneAbsorptionIntensity.GetHashCode();
                hash = hash * 31 + settings.OzoneAbsorptionCoefficient.GetHashCode();
                hash = hash * 31 + settings.OzoneLayerCenter.GetHashCode();
                hash = hash * 31 + settings.OzoneLayerThickness.GetHashCode();
                hash = hash * 31 + settings.MultipleScatteringIntensity.GetHashCode();
                hash = hash * 31 + settings.TraceSampleCountScale.GetHashCode();
                hash = hash * 31 + settings.PlanetRadius.GetHashCode();
                hash = hash * 31 + settings.AtmosphereHeight.GetHashCode();
                hash = hash * 31 + settings.RayleighScaleHeight.GetHashCode();
                hash = hash * 31 + settings.MieScaleHeight.GetHashCode();
                hash = hash * 31 + settings.GroundAlbedo.GetHashCode();
                return hash;
            }
        }

        private static int CalculateSkyStateHash(BurtAtmosphereSettings settings, Vector3 sunDirection, float cameraAltitudeKm)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + settings.MieAnisotropy.GetHashCode();
                hash = hash * 31 + sunDirection.GetHashCode();
                return hash * 31 + cameraAltitudeKm.GetHashCode();
            }
        }

        private static int CalculateFogStateHash(
            BurtAtmosphereSettings settings,
            Vector3 sunDirection,
            float cameraAltitudeKm,
            Vector3 fogViewRayBottomLeft,
            Vector3 fogViewRayBottomRight,
            Vector3 fogViewRayTopLeft,
            Vector3 fogViewRayTopRight)
        {
            unchecked
            {
                // Hash the values consumed by BuildFogLut after their CPU-side unit conversion.
                // Height falloff, tint, intensity and sampling/luminance scales are applied while
                // compositing the LUT and therefore do not require a 3D texture rebuild.
                var startDepthKm = settings.AerialPerspectiveNearFadeStart * settings.WorldToKilometers;
                var hash = 17;
                hash = hash * 31 + settings.MieAnisotropy.GetHashCode();
                hash = hash * 31 + settings.AerialPerspectiveDensityScale.GetHashCode();
                hash = hash * 31 + startDepthKm.GetHashCode();
                hash = hash * 31 + sunDirection.GetHashCode();
                hash = hash * 31 + fogViewRayBottomLeft.GetHashCode();
                hash = hash * 31 + fogViewRayBottomRight.GetHashCode();
                hash = hash * 31 + fogViewRayTopLeft.GetHashCode();
                hash = hash * 31 + fogViewRayTopRight.GetHashCode();
                return hash * 31 + cameraAltitudeKm.GetHashCode();
            }
        }

        private static void ResolveFogViewRays(
            Camera camera,
            Matrix4x4 worldToLocal,
            out Vector3 bottomLeft,
            out Vector3 bottomRight,
            out Vector3 topLeft,
            out Vector3 topRight)
        {
            if (camera == null)
            {
                bottomLeft = bottomRight = topLeft = topRight = Vector3.forward;
                return;
            }

            if (camera.orthographic)
            {
                var localForward = worldToLocal.MultiplyVector(camera.transform.forward).normalized;
                bottomLeft = bottomRight = topLeft = topRight = localForward;
                return;
            }

            // Keep the unnormalized perspective rays. Bilinear interpolation is
            // exact in this projective representation, including asymmetric frusta;
            // the compute kernel normalizes only after interpolation.
            bottomLeft = ResolvePerspectiveViewRay(camera, worldToLocal, 0f, 0f);
            bottomRight = ResolvePerspectiveViewRay(camera, worldToLocal, 1f, 0f);
            topLeft = ResolvePerspectiveViewRay(camera, worldToLocal, 0f, 1f);
            topRight = ResolvePerspectiveViewRay(camera, worldToLocal, 1f, 1f);
        }

        private static Vector3 ResolvePerspectiveViewRay(Camera camera, Matrix4x4 worldToLocal, float viewportX, float viewportY)
        {
            var viewportPointWorld = camera.ViewportToWorldPoint(new Vector3(viewportX, viewportY, 1f));
            return worldToLocal.MultiplyVector(viewportPointWorld - camera.transform.position);
        }

        private static void ResolveCameraFrame(Camera camera, BurtAtmosphereSettings settings, out float altitudeKm, out Matrix4x4 worldToLocal)
        {
            var worldToKm = Mathf.Max(settings.WorldToKilometers, 0.000001f);
            var cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
            var planetToCameraWorld = cameraPosition - settings.PlanetCenterWorld;
            if (planetToCameraWorld.sqrMagnitude <= 0.000001f)
            {
                planetToCameraWorld = Vector3.up;
            }

            var up = planetToCameraWorld.normalized;
            var radiusKm = planetToCameraWorld.magnitude * worldToKm;
            // Only protect the lower planet boundary. Do not clamp against AtmosphereHeight:
            // the compute kernels need the true altitude to reproduce XRender's
            // MoveToTopAtmosphere path for cameras outside the atmosphere.
            altitudeKm = Mathf.Max(radiusKm - settings.PlanetRadius, GroundRadiusOffsetKm);

            var referenceForward = camera != null ? camera.transform.forward : Vector3.forward;
            var right = Vector3.Cross(referenceForward, up);
            if (right.sqrMagnitude <= 0.000001f)
            {
                right = Vector3.Cross(Vector3.forward, up);
            }

            if (right.sqrMagnitude <= 0.000001f)
            {
                right = Vector3.right;
            }

            right.Normalize();
            var forward = Vector3.Cross(up, right).normalized;
            worldToLocal = Matrix4x4.identity;
            worldToLocal.SetRow(0, new Vector4(right.x, right.y, right.z, 0f));
            worldToLocal.SetRow(1, new Vector4(up.x, up.y, up.z, 0f));
            worldToLocal.SetRow(2, new Vector4(forward.x, forward.y, forward.z, 0f));
        }

        private static void ReleaseTexture(ref RenderTexture texture)
        {
            if (texture == null)
            {
                return;
            }

            texture.Release();
            if (Application.isPlaying)
            {
                Object.Destroy(texture);
            }
            else
            {
                Object.DestroyImmediate(texture);
            }

            texture = null;
        }
    }
}
