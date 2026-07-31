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
        private const int HorizontalScatteringElementCount = 3;
        private const int HorizontalScatteringElementStride = 16;
        private const int HorizontalScatteringBufferSize =
            HorizontalScatteringElementCount * HorizontalScatteringElementStride;
        private const int SkyViewWidth = 192;
        private const int SkyViewHeight = 104;
        internal const int FogLutWidth = 32;
        internal const int FogLutHeight = 32;
        internal const int FogLutDepth = 16;
        internal const int FogLutSamplesPerSlice = 2;
        internal const float FogLutCoverageKm = 96f;
        private const GraphicsFormat PackedScatteringFormat = GraphicsFormat.B10G11R11_UFloatPack32;
        private const GraphicsFormat HighPrecisionFormat = GraphicsFormat.R16G16B16A16_SFloat;
        // XRender offsets planet-surface evaluations by 5m to avoid unstable sphere intersections at the ground boundary.
        private const float GroundRadiusOffsetKm = 0.005f;
        // XRender reconstructs its per-camera frustum vectors at this linear depth.
        // Perspective vectors are scale-invariant; orthographic vectors retain the
        // projection-plane offset before normalization.
        private const float FrustumRayReconstructionDepth = 10000f;
        private const float MobileSkySunDirectionThresholdDegrees = 10f;
        private const float MobileSkyAltitudeThresholdWorldUnits = 100f;

        private static readonly int TransmittanceLutId = Shader.PropertyToID("_BurtAtmosphereTransmittanceLut");
        private static readonly int MultipleScatteringLutId = Shader.PropertyToID("_BurtAtmosphereMultipleScatteringLut");
        private static readonly int HorizontalScatteringConstantBufferId = Shader.PropertyToID("_BurtAtmosphereHorizontalScatteringCB");
        private static readonly int SkyViewLutId = Shader.PropertyToID("_BurtAtmosphereSkyViewLut");
        private static readonly int FogLutId = Shader.PropertyToID("_BurtAtmosphereFogLut");
        private static readonly int UseLutsId = Shader.PropertyToID("_BurtAtmosphereUseLuts");
        private static readonly int TransparentFogEnabledId = Shader.PropertyToID("_BurtTransparentAtmosphereFogEnabled");
        private static readonly int TransparentFogLightColorId = Shader.PropertyToID("_BurtTransparentAtmosphereFogLightColor");
        private static readonly int TransparentFogDistanceParamsId = Shader.PropertyToID("_BurtTransparentAtmosphereFogDistanceParams");
        private static readonly int MieAnisotropyId = Shader.PropertyToID("_BurtAtmosphereMieAnisotropy");
        private static readonly int RayleighScatteringCoefficientId = Shader.PropertyToID("_BurtAtmosphereRayleighScatteringCoefficient");
        private static readonly int MieScatteringCoefficientId = Shader.PropertyToID("_BurtAtmosphereMieScatteringCoefficient");
        private static readonly int MieAbsorptionCoefficientId = Shader.PropertyToID("_BurtAtmosphereMieAbsorptionCoefficient");
        private static readonly int MieExtinctionCoefficientId = Shader.PropertyToID("_BurtAtmosphereMieExtinctionCoefficient");
        private static readonly int OzoneAbsorptionCoefficientId = Shader.PropertyToID("_BurtAtmosphereOzoneAbsorptionCoefficient");
        private static readonly int DensityProfileParamsId = Shader.PropertyToID("_BurtAtmosphereDensityProfileParams");
        private static readonly int OzoneDensityProfileParamsId = Shader.PropertyToID("_BurtAtmosphereOzoneDensityProfileParams");
        private static readonly int GroundAlbedoId = Shader.PropertyToID("_BurtAtmosphereGroundAlbedo");
        private static readonly int MultipleScatteringIntensityId = Shader.PropertyToID("_BurtAtmosphereMultipleScatteringIntensity");
        private static readonly int TraceSampleCountScaleId = Shader.PropertyToID("_BurtAtmosphereTraceSampleCountScale");
        private static readonly int PlanetParamsId = Shader.PropertyToID("_BurtAtmospherePlanetParams");
        private static readonly int SunDirectionId = Shader.PropertyToID("_BurtAtmosphereSunDirection");
        // Property names predate platform-UV parity. Their values are shader UV
        // corners (00, 10, 01, 11), which are not always physical bottom/top.
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
        private static readonly int HorizontalScatteringOutputId = Shader.PropertyToID("_BurtAtmosphereHorizontalScatteringLutUav");
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
        private static GraphicsBuffer horizontalScatteringStructuredBuffer;
        private static GraphicsBuffer horizontalScatteringConstantBuffer;
        private static RenderTexture skyViewLut;
        private static RenderTexture fogLut;
        private static Texture3D fogLutDummy;
        private static GraphicsFormat transmittanceFormat = GraphicsFormat.None;
        private static GraphicsFormat multipleScatteringFormat = GraphicsFormat.None;
        private static GraphicsFormat skyViewFormat = GraphicsFormat.None;
        private static GraphicsFormat fogFormat = GraphicsFormat.None;
        private static PhysicalStateKey lastPhysicalState;
        private static SkyStateKey lastSkyState;
        private static FogStateKey lastFogState;
        private static int lastPhysicalStateHash;
        private static int lastSkyStateHash;
        private static int lastFogStateHash;
        private static int lastEnsureFrame = -1;
        private static int lastEnsureCameraId;
        private static int lastSkyProducerCameraId;
        private static int lastFogProducerCameraId;
        private static int physicalDispatchCount;
        private static int skyDispatchCount;
        private static int fogDispatchCount;
        private static GraphicsFence pendingAsyncFence;
        private static bool hasPendingAsyncFence;
        private static bool pendingHorizontalPublish;
        private static int asyncDispatchCount;
        private static int asyncCompletionCount;
        private static int asyncAttemptCount;
        private static int asyncCleanSkipCount;
        private static int asyncResourceRecoveryDeferralCount;
        private static int pendingAsyncProducerCameraId;
        private static int pendingAsyncProducerFrame = -1;
        private static int lastCompletedAsyncProducerCameraId;
        private static int lastCompletedAsyncProducerFrame = -1;
        private static string lastAsyncStatus = "NeverAttempted";
        private static int kernelResourceBindCount;
        private static int kernelResourceBindFailureCount;
        private static string lastKernelResourceBinding = "NeverBound";
        private static float cameraAltitudeRatio;
        private static Matrix4x4 worldToSkyViewLocal = Matrix4x4.identity;
        private static bool hasPhysicalState;
        private static bool hasSkyState;
        private static bool hasFogState;
        private static bool initializationFailed;

        // These exact keys are the cache authority. Their hash codes are only
        // diagnostics: using a 32-bit hash as the equality test could silently
        // reuse another camera's LUT after a collision.
        private readonly struct PhysicalStateKey : System.IEquatable<PhysicalStateKey>
        {
            private readonly Color rayleighScatteringCoefficient;
            private readonly Color mieScatteringCoefficient;
            private readonly Color mieAbsorptionCoefficient;
            private readonly Color ozoneAbsorptionCoefficient;
            private readonly float ozoneLayerCenter;
            private readonly float ozoneLayerThickness;
            private readonly float multipleScatteringIntensity;
            private readonly float traceSampleCountScale;
            private readonly float planetRadius;
            private readonly float atmosphereHeight;
            private readonly float rayleighScaleHeight;
            private readonly float mieScaleHeight;
            private readonly Color groundAlbedo;

            public PhysicalStateKey(BurtAtmosphereSettings settings)
            {
                var effectiveCoefficients = BurtAtmosphereUtility.ResolveEffectiveCoefficients(settings);
                rayleighScatteringCoefficient = effectiveCoefficients.RayleighScattering;
                mieScatteringCoefficient = effectiveCoefficients.MieScattering;
                mieAbsorptionCoefficient = effectiveCoefficients.MieAbsorption;
                ozoneAbsorptionCoefficient = effectiveCoefficients.OzoneAbsorption;
                ozoneLayerCenter = settings.OzoneLayerCenter;
                ozoneLayerThickness = settings.OzoneLayerThickness;
                multipleScatteringIntensity = settings.MultipleScatteringIntensity;
                traceSampleCountScale = settings.TraceSampleCountScale;
                planetRadius = settings.PlanetRadius;
                atmosphereHeight = settings.AtmosphereHeight;
                rayleighScaleHeight = settings.RayleighScaleHeight;
                mieScaleHeight = settings.MieScaleHeight;
                groundAlbedo = settings.GroundAlbedo;
            }

            public bool Equals(PhysicalStateKey other)
            {
                return rayleighScatteringCoefficient.Equals(other.rayleighScatteringCoefficient)
                    && mieScatteringCoefficient.Equals(other.mieScatteringCoefficient)
                    && mieAbsorptionCoefficient.Equals(other.mieAbsorptionCoefficient)
                    && ozoneAbsorptionCoefficient.Equals(other.ozoneAbsorptionCoefficient)
                    && ozoneLayerCenter.Equals(other.ozoneLayerCenter)
                    && ozoneLayerThickness.Equals(other.ozoneLayerThickness)
                    && multipleScatteringIntensity.Equals(other.multipleScatteringIntensity)
                    && traceSampleCountScale.Equals(other.traceSampleCountScale)
                    && planetRadius.Equals(other.planetRadius)
                    && atmosphereHeight.Equals(other.atmosphereHeight)
                    && rayleighScaleHeight.Equals(other.rayleighScaleHeight)
                    && mieScaleHeight.Equals(other.mieScaleHeight)
                    && groundAlbedo.Equals(other.groundAlbedo);
            }

            public override bool Equals(object obj)
            {
                return obj is PhysicalStateKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = 17;
                    hash = hash * 31 + rayleighScatteringCoefficient.GetHashCode();
                    hash = hash * 31 + mieScatteringCoefficient.GetHashCode();
                    hash = hash * 31 + mieAbsorptionCoefficient.GetHashCode();
                    hash = hash * 31 + ozoneAbsorptionCoefficient.GetHashCode();
                    hash = hash * 31 + ozoneLayerCenter.GetHashCode();
                    hash = hash * 31 + ozoneLayerThickness.GetHashCode();
                    hash = hash * 31 + multipleScatteringIntensity.GetHashCode();
                    hash = hash * 31 + traceSampleCountScale.GetHashCode();
                    hash = hash * 31 + planetRadius.GetHashCode();
                    hash = hash * 31 + atmosphereHeight.GetHashCode();
                    hash = hash * 31 + rayleighScaleHeight.GetHashCode();
                    hash = hash * 31 + mieScaleHeight.GetHashCode();
                    return hash * 31 + groundAlbedo.GetHashCode();
                }
            }
        }

        private readonly struct SkyStateKey : System.IEquatable<SkyStateKey>
        {
            private readonly float mieAnisotropy;
            private readonly Color skyLuminanceFactor;
            private readonly Vector3 sunDirection;
            private readonly float cameraAltitudeKm;

            public SkyStateKey(
                BurtAtmosphereSettings settings,
                Vector3 localSunDirection,
                float altitudeKm)
            {
                mieAnisotropy = settings.MieAnisotropy;
                skyLuminanceFactor = settings.SkyLuminanceFactor;
                sunDirection = localSunDirection;
                cameraAltitudeKm = altitudeKm;
            }

            public bool Equals(SkyStateKey other)
            {
                return mieAnisotropy.Equals(other.mieAnisotropy)
                    && skyLuminanceFactor.Equals(other.skyLuminanceFactor)
                    && sunDirection.Equals(other.sunDirection)
                    && cameraAltitudeKm.Equals(other.cameraAltitudeKm);
            }

            public bool RequiresMobileRefresh(SkyStateKey other, float altitudeThresholdKm)
            {
                return !mieAnisotropy.Equals(other.mieAnisotropy)
                    || !skyLuminanceFactor.Equals(other.skyLuminanceFactor)
                    || Vector3.Angle(sunDirection, other.sunDirection) > MobileSkySunDirectionThresholdDegrees
                    || Mathf.Abs(cameraAltitudeKm - other.cameraAltitudeKm) > altitudeThresholdKm;
            }

            public override bool Equals(object obj)
            {
                return obj is SkyStateKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = 17;
                    hash = hash * 31 + mieAnisotropy.GetHashCode();
                    hash = hash * 31 + skyLuminanceFactor.GetHashCode();
                    hash = hash * 31 + sunDirection.GetHashCode();
                    return hash * 31 + cameraAltitudeKm.GetHashCode();
                }
            }
        }

        private readonly struct FogStateKey : System.IEquatable<FogStateKey>
        {
            private readonly float mieAnisotropy;
            private readonly float densityScale;
            private readonly float startDepthKm;
            private readonly Vector3 sunDirection;
            private readonly Vector3 viewRayUv00;
            private readonly Vector3 viewRayUv10;
            private readonly Vector3 viewRayUv01;
            private readonly Vector3 viewRayUv11;
            private readonly float cameraAltitudeKm;

            public FogStateKey(
                BurtAtmosphereSettings settings,
                Vector3 localSunDirection,
                float altitudeKm,
                Vector3 fogViewRayUv00,
                Vector3 fogViewRayUv10,
                Vector3 fogViewRayUv01,
                Vector3 fogViewRayUv11)
            {
                mieAnisotropy = settings.MieAnisotropy;
                densityScale = settings.AerialPerspectiveDensityScale;
                startDepthKm = settings.AerialPerspectiveStartDepth * settings.WorldToKilometers;
                sunDirection = localSunDirection;
                viewRayUv00 = fogViewRayUv00;
                viewRayUv10 = fogViewRayUv10;
                viewRayUv01 = fogViewRayUv01;
                viewRayUv11 = fogViewRayUv11;
                cameraAltitudeKm = altitudeKm;
            }

            public bool Equals(FogStateKey other)
            {
                return mieAnisotropy.Equals(other.mieAnisotropy)
                    && densityScale.Equals(other.densityScale)
                    && startDepthKm.Equals(other.startDepthKm)
                    && sunDirection.Equals(other.sunDirection)
                    && viewRayUv00.Equals(other.viewRayUv00)
                    && viewRayUv10.Equals(other.viewRayUv10)
                    && viewRayUv01.Equals(other.viewRayUv01)
                    && viewRayUv11.Equals(other.viewRayUv11)
                    && cameraAltitudeKm.Equals(other.cameraAltitudeKm);
            }

            public override bool Equals(object obj)
            {
                return obj is FogStateKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = 17;
                    hash = hash * 31 + mieAnisotropy.GetHashCode();
                    hash = hash * 31 + densityScale.GetHashCode();
                    hash = hash * 31 + startDepthKm.GetHashCode();
                    hash = hash * 31 + sunDirection.GetHashCode();
                    hash = hash * 31 + viewRayUv00.GetHashCode();
                    hash = hash * 31 + viewRayUv10.GetHashCode();
                    hash = hash * 31 + viewRayUv01.GetHashCode();
                    hash = hash * 31 + viewRayUv11.GetHashCode();
                    return hash * 31 + cameraAltitudeKm.GetHashCode();
                }
            }
        }

        private readonly struct LutUpdateState
        {
            public readonly float CameraAltitudeKm;
            public readonly Matrix4x4 WorldToSkyViewLocal;
            public readonly Vector3 LocalSunDirection;
            public readonly Vector3 FogViewRayUv00;
            public readonly Vector3 FogViewRayUv10;
            public readonly Vector3 FogViewRayUv01;
            public readonly Vector3 FogViewRayUv11;
            public readonly PhysicalStateKey PhysicalState;
            public readonly SkyStateKey SkyState;
            public readonly FogStateKey FogState;
            public readonly bool PhysicalDirty;
            public readonly bool SkyDirty;
            public readonly bool FogDirty;

            public bool HasUpdates => PhysicalDirty || SkyDirty || FogDirty;

            public LutUpdateState(
                float cameraAltitudeKm,
                Matrix4x4 worldToSkyViewLocal,
                Vector3 localSunDirection,
                Vector3 fogViewRayUv00,
                Vector3 fogViewRayUv10,
                Vector3 fogViewRayUv01,
                Vector3 fogViewRayUv11,
                PhysicalStateKey physicalState,
                SkyStateKey skyState,
                FogStateKey fogState,
                bool physicalDirty,
                bool skyDirty,
                bool fogDirty)
            {
                CameraAltitudeKm = cameraAltitudeKm;
                WorldToSkyViewLocal = worldToSkyViewLocal;
                LocalSunDirection = localSunDirection;
                FogViewRayUv00 = fogViewRayUv00;
                FogViewRayUv10 = fogViewRayUv10;
                FogViewRayUv01 = fogViewRayUv01;
                FogViewRayUv11 = fogViewRayUv11;
                PhysicalState = physicalState;
                SkyState = skyState;
                FogState = fogState;
                PhysicalDirty = physicalDirty;
                SkyDirty = skyDirty;
                FogDirty = fogDirty;
            }
        }

        public static void EnsureLuts(CommandBuffer cmd, Camera camera, BurtAtmosphereSettings settings, Vector3 sunDirection)
        {
            if (cmd == null)
            {
                return;
            }

            CompleteAsync(cmd);
            RecordLutUpdates(cmd, camera, settings, sunDirection, true);
            BindGlobals(cmd);
        }

        public static void DispatchAsync(
            ScriptableRenderContext renderContext,
            Camera camera,
            BurtAtmosphereSettings settings,
            Vector3 sunDirection)
        {
            asyncAttemptCount++;
            if (camera == null ||
                !SystemInfo.supportsComputeShaders ||
                !SystemInfo.supportsAsyncCompute ||
                !SystemInfo.supportsGraphicsFence)
            {
                lastAsyncStatus = "SkippedUnsupported";
                return;
            }

            // TryResolveLutUpdateState may recreate a lost persistent texture or
            // buffer. Never let that CPU-side recovery run while an earlier
            // async command can still reference the old resource generation.
            // The request's SetupLighting pass will consume the fence first;
            // a later synchronous EnsureLuts call can then rebuild safely.
            if (hasPendingAsyncFence && !AreRequiredLutsReady())
            {
                asyncResourceRecoveryDeferralCount++;
                lastAsyncStatus = "DeferredResourceRecoveryUntilGraphicsFence";
                return;
            }

            if (!TryResolveLutUpdateState(camera, settings, sunDirection, out var updateState))
            {
                lastAsyncStatus = initializationFailed ? "SkippedInitializationFailed" : "SkippedUnavailable";
                return;
            }

            if (!updateState.HasUpdates)
            {
                asyncCleanSkipCount++;
                lastAsyncStatus = "SkippedClean";
                return;
            }

            // Serialize any previous async update and every earlier graphics
            // consumer before the persistent LUTs may be overwritten.
            var graphicsCmd = CommandBufferPool.Get("Burt Atmosphere Async Begin");
            CompleteAsync(graphicsCmd);
            var graphicsToComputeFence = graphicsCmd.CreateAsyncGraphicsFence();
            renderContext.ExecuteCommandBuffer(graphicsCmd);
            CommandBufferPool.Release(graphicsCmd);

            var asyncCmd = CommandBufferPool.Get("Burt Atmosphere LUT Async Compute");
            asyncCmd.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
            asyncCmd.WaitOnAsyncGraphicsFence(graphicsToComputeFence);
            var recordedDispatch = RecordResolvedLutUpdates(
                asyncCmd,
                camera,
                settings,
                updateState,
                false);
            if (recordedDispatch)
            {
                pendingAsyncFence = asyncCmd.CreateAsyncGraphicsFence();
                hasPendingAsyncFence = true;
                pendingAsyncProducerCameraId = camera.GetInstanceID();
                pendingAsyncProducerFrame = Time.frameCount;
                asyncDispatchCount++;
                lastAsyncStatus = "Submitted";
                renderContext.ExecuteCommandBufferAsync(asyncCmd, ComputeQueueType.Background);
            }
            else
            {
                lastAsyncStatus = "SkippedRecordFailure";
            }

            CommandBufferPool.Release(asyncCmd);
        }

        public static void CompleteAsync(CommandBuffer cmd)
        {
            if (cmd == null || !hasPendingAsyncFence)
            {
                return;
            }

            cmd.WaitOnAsyncGraphicsFence(pendingAsyncFence);
            hasPendingAsyncFence = false;
            lastCompletedAsyncProducerCameraId = pendingAsyncProducerCameraId;
            lastCompletedAsyncProducerFrame = pendingAsyncProducerFrame;
            pendingAsyncProducerCameraId = 0;
            pendingAsyncProducerFrame = -1;
            asyncCompletionCount++;
            if (pendingHorizontalPublish)
            {
                PublishHorizontalScattering(cmd);
                pendingHorizontalPublish = false;
            }

            BindHorizontalScatteringConstantBuffer(cmd);
        }

        private static bool RecordLutUpdates(
            CommandBuffer cmd,
            Camera camera,
            BurtAtmosphereSettings settings,
            Vector3 sunDirection,
            bool publishHorizontalImmediately)
        {
            if (cmd == null ||
                !TryResolveLutUpdateState(camera, settings, sunDirection, out var updateState))
            {
                return false;
            }

            return RecordResolvedLutUpdates(
                cmd,
                camera,
                settings,
                updateState,
                publishHorizontalImmediately);
        }

        private static bool RecordResolvedLutUpdates(
            CommandBuffer cmd,
            Camera camera,
            BurtAtmosphereSettings settings,
            LutUpdateState updateState,
            bool publishHorizontalImmediately)
        {
            // XRender publishes the CS-written triplet through a constant-buffer
            // mirror. Binding the mirror every request keeps all material draws
            // valid even when no LUT dispatch is required for this camera.
            if (publishHorizontalImmediately)
            {
                BindHorizontalScatteringConstantBuffer(cmd);
            }

            worldToSkyViewLocal = updateState.WorldToSkyViewLocal;
            // Sampling uniforms remain current even when the mobile SkyView LUT
            // stays inside XRender's coarse refresh thresholds. Keep the ratio
            // unclamped so cameras in space retain their real geocentric radius.
            cameraAltitudeRatio =
                updateState.CameraAltitudeKm / Mathf.Max(settings.AtmosphereHeight, 0.001f);
            lastEnsureFrame = Time.frameCount;
            lastEnsureCameraId = camera != null ? camera.GetInstanceID() : 0;
            if (!updateState.HasUpdates)
            {
                return false;
            }

            UploadParameters(
                cmd,
                settings,
                updateState.LocalSunDirection,
                updateState.CameraAltitudeKm,
                updateState.FogViewRayUv00,
                updateState.FogViewRayUv10,
                updateState.FogViewRayUv01,
                updateState.FogViewRayUv11);
            var recordedDispatch = false;
            var deferHorizontalPublish = false;
            if (updateState.PhysicalDirty)
            {
                if (!Dispatch(cmd, transmittanceKernel, TransmittanceWidth, TransmittanceHeight, 1))
                {
                    return false;
                }

                recordedDispatch = true;
                if (!Dispatch(cmd, multipleScatteringKernel, MultipleScatteringSize, MultipleScatteringSize, 1))
                {
                    return false;
                }

                lastPhysicalState = updateState.PhysicalState;
                lastPhysicalStateHash = updateState.PhysicalState.GetHashCode();
                hasPhysicalState = true;
                physicalDispatchCount++;
            }

            if (updateState.SkyDirty)
            {
                // Match XRender's dependency order: SkyView first, then the
                // independent horizontal triplet and its GPU-side CB mirror.
                if (!Dispatch(cmd, skyViewKernel, SkyViewWidth, SkyViewHeight, 1))
                {
                    return false;
                }

                recordedDispatch = true;
                if (!Dispatch(cmd, horizontalScatteringKernel, 1, 1, 1))
                {
                    return false;
                }

                if (publishHorizontalImmediately)
                {
                    if (!PublishHorizontalScattering(cmd))
                    {
                        return false;
                    }
                }
                else
                {
                    // CopyBuffer and global constant-buffer publication remain on
                    // graphics after the compute-to-graphics fence.
                    deferHorizontalPublish = true;
                }

                lastSkyState = updateState.SkyState;
                lastSkyStateHash = updateState.SkyState.GetHashCode();
                hasSkyState = true;
                lastSkyProducerCameraId = lastEnsureCameraId;
                skyDispatchCount++;
            }

            if (updateState.FogDirty)
            {
                if (!Dispatch(cmd, fogKernel, FogLutWidth, FogLutHeight, FogLutDepth))
                {
                    return false;
                }

                recordedDispatch = true;
                lastFogState = updateState.FogState;
                lastFogStateHash = updateState.FogState.GetHashCode();
                hasFogState = true;
                lastFogProducerCameraId = lastEnsureCameraId;
                fogDispatchCount++;
            }

            // Commit the async publication flag only after the complete update
            // batch was recorded. A rejected partial batch is never submitted.
            pendingHorizontalPublish |= deferHorizontalPublish;
            return recordedDispatch;
        }

        private static bool TryResolveLutUpdateState(
            Camera camera,
            BurtAtmosphereSettings settings,
            Vector3 sunDirection,
            out LutUpdateState updateState)
        {
            updateState = default;
            if (camera == null ||
                !SystemInfo.supportsComputeShaders ||
                !EnsureResources())
            {
                return false;
            }

            ResolveCameraFrame(
                camera,
                settings,
                out var cameraAltitudeKm,
                out var resolvedWorldToSkyViewLocal);
            var localSunDirection =
                resolvedWorldToSkyViewLocal.MultiplyVector(sunDirection).normalized;
            var fogViewRayUv00 = Vector3.forward;
            var fogViewRayUv10 = Vector3.forward;
            var fogViewRayUv01 = Vector3.forward;
            var fogViewRayUv11 = Vector3.forward;
            var supportsFogLut = BurtAtmosphereUtility.SupportsAtmosphereFogLut;
            if (supportsFogLut)
            {
                ResolveFogViewRays(
                    camera,
                    resolvedWorldToSkyViewLocal,
                    out fogViewRayUv00,
                    out fogViewRayUv10,
                    out fogViewRayUv01,
                    out fogViewRayUv11);
            }

            var physicalState = new PhysicalStateKey(settings);
            var skyState = new SkyStateKey(settings, localSunDirection, cameraAltitudeKm);
            var fogState = supportsFogLut
                ? new FogStateKey(
                    settings,
                    localSunDirection,
                    cameraAltitudeKm,
                    fogViewRayUv00,
                    fogViewRayUv10,
                    fogViewRayUv01,
                    fogViewRayUv11)
                : default;
            var physicalDirty = !hasPhysicalState || !physicalState.Equals(lastPhysicalState);
            var mobileAltitudeThresholdKm =
                MobileSkyAltitudeThresholdWorldUnits *
                Mathf.Max(settings.WorldToKilometers, 0.000001f);
            var skyDirty = physicalDirty ||
                !hasSkyState ||
                (BurtAtmosphereUtility.IsMobileAtmospherePlatform
                    ? skyState.RequiresMobileRefresh(lastSkyState, mobileAltitudeThresholdKm)
                    : !skyState.Equals(lastSkyState));
            var fogDirty = supportsFogLut &&
                (physicalDirty || !hasFogState || !fogState.Equals(lastFogState));
            updateState = new LutUpdateState(
                cameraAltitudeKm,
                resolvedWorldToSkyViewLocal,
                localSunDirection,
                fogViewRayUv00,
                fogViewRayUv10,
                fogViewRayUv01,
                fogViewRayUv11,
                physicalState,
                skyState,
                fogState,
                physicalDirty,
                skyDirty,
                fogDirty);
            return true;
        }

        public static void BindToMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            var available = AreRequiredLutsReady();
            material.SetFloat(UseLutsId, available ? 1f : 0f);
            material.SetFloat(CameraAltitude01Id, cameraAltitudeRatio);
            material.SetMatrix(WorldToSkyViewLocalId, worldToSkyViewLocal);
            if (!available)
            {
                return;
            }

            material.SetTexture(TransmittanceLutId, transmittanceLut);
            material.SetTexture(MultipleScatteringLutId, multipleScatteringLut);
            material.SetConstantBuffer(
                HorizontalScatteringConstantBufferId,
                horizontalScatteringConstantBuffer,
                0,
                HorizontalScatteringBufferSize);
            material.SetTexture(SkyViewLutId, skyViewLut);
            material.SetTexture(FogLutId, ResolveFogLutBinding());
        }

        public static void BindGlobals(CommandBuffer cmd)
        {
            if (cmd == null)
            {
                return;
            }

            var available = AreRequiredLutsReady();
            cmd.SetGlobalFloat(UseLutsId, available ? 1f : 0f);
            cmd.SetGlobalFloat(CameraAltitude01Id, cameraAltitudeRatio);
            cmd.SetGlobalMatrix(WorldToSkyViewLocalId, worldToSkyViewLocal);
            if (!available)
            {
                return;
            }

            cmd.SetGlobalTexture(TransmittanceLutId, transmittanceLut);
            cmd.SetGlobalTexture(MultipleScatteringLutId, multipleScatteringLut);
            BindHorizontalScatteringConstantBuffer(cmd);
            cmd.SetGlobalTexture(SkyViewLutId, skyViewLut);
            cmd.SetGlobalTexture(FogLutId, ResolveFogLutBinding());
        }

        public static void ResetFallbackGlobals(CommandBuffer cmd)
        {
            if (cmd == null)
            {
                return;
            }

            cmd.SetGlobalFloat(UseLutsId, 0f);
            cmd.SetGlobalFloat(TransparentFogEnabledId, 0f);
        }

        public static bool BindHorizontalScatteringToCompute(
            CommandBuffer cmd,
            ComputeShader shader,
            int kernel)
        {
            if (cmd == null || shader == null || kernel < 0 || !AreCoreLutsReady())
            {
                return false;
            }

            cmd.SetComputeConstantBufferParam(
                shader,
                HorizontalScatteringConstantBufferId,
                horizontalScatteringConstantBuffer,
                0,
                HorizontalScatteringBufferSize);
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
            var atmosphereResourcesAvailable = BurtAtmosphereUtility.ShouldUseAtmosphereResources(request);
            var aerialEnabled = camera != null &&
                BurtAtmosphereUtility.ShouldUseAerialPerspective(request);
            var fogSettings = BurtFogUtility.ResolveSettings(request);
            var heightFogNeedsAtmosphereLuts = camera != null &&
                request != null &&
                BurtFogUtility.ShouldUseFog(request) &&
                fogSettings.HorizontalScattering.Enabled;
            var needsAtmosphereLuts = atmosphereResourcesAvailable &&
                (aerialEnabled || heightFogNeedsAtmosphereLuts);
            if (!needsAtmosphereLuts)
            {
                cmd.SetGlobalFloat(TransparentFogEnabledId, 0f);
                cmd.SetGlobalFloat(UseLutsId, 0f);
                return;
            }

            var sunDirection4 = BurtDrawAtmospherePass.ResolveSunDirection(request, settings);
            var sunDirection = new Vector3(sunDirection4.x, sunDirection4.y, sunDirection4.z);
            EnsureLuts(cmd, camera, settings, sunDirection);

            var available = AreCoreLutsReady();
            var fogAvailable = IsFogLutReady();
            // XRender's AEvaluateAtmosphereFog returns (0, 0, 0, 1) when the
            // luminance scale is zero. Disable the transparent consumer as well
            // so it cannot retain LUT extinction after RGB has been zeroed.
            var transparentFogEnabled = aerialEnabled &&
                settings.AerialPerspectiveLuminanceScale > 0f &&
                fogAvailable;
            cmd.SetGlobalFloat(TransparentFogEnabledId, transparentFogEnabled ? 1f : 0f);
            cmd.SetGlobalFloat(UseLutsId, available ? 1f : 0f);
            if (!available)
            {
                return;
            }

            BindHorizontalScatteringConstantBuffer(cmd);
            cmd.SetGlobalColor(SkyLuminanceFactorId, settings.SkyLuminanceFactor);

            var lightingData = request.LightingData;
            var outerSpaceLight = lightingData != null && lightingData.HasMainLight
                ? lightingData.MainLightColorOuterSpace
                : Color.clear;
            var lightScale = Mathf.Clamp01(settings.MainLightOcclusion) *
                Mathf.Max(0f, settings.AerialPerspectiveLuminanceScale);
            cmd.SetGlobalVector(TransparentFogLightColorId, new Vector4(
                Mathf.Max(0f, outerSpaceLight.r) * lightScale,
                Mathf.Max(0f, outerSpaceLight.g) * lightScale,
                Mathf.Max(0f, outerSpaceLight.b) * lightScale,
                0f));
            cmd.SetGlobalVector(TransparentFogDistanceParamsId, new Vector4(
                settings.WorldToKilometers,
                settings.AerialPerspectiveSamplingDistanceScale,
                settings.AerialPerspectiveStartDepth,
                FogLutCoverageKm));
            cmd.SetGlobalTexture(FogLutId, ResolveFogLutBinding());
        }

        public static void EnsureAndBindForFog(CommandBuffer cmd, Material material, Camera camera, BurtRenderRequest request)
        {
            if (material == null)
            {
                return;
            }

            var settings = BurtAtmosphereUtility.ResolveSettings();
            if (camera == null || !BurtAtmosphereUtility.ShouldUseAtmosphereResources(request))
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
                " PhysicalState=", hasPhysicalState,
                " PhysicalHash=", lastPhysicalStateHash,
                " SkyState=", hasSkyState,
                " SkyHash=", lastSkyStateHash,
                " FogState=", hasFogState,
                " FogHash=", lastFogStateHash,
                " CacheEquality=ExactKeys",
                " Mobile=", BurtAtmosphereUtility.IsMobileAtmospherePlatform,
                " PlatformContract=", BurtAtmosphereUtility.AtmospherePlatformFormulaName,
                " ResourceContract=", BurtAtmosphereUtility.AtmosphereLutResourceFormulaName,
                " AsyncContract=", BurtAtmosphereUtility.AtmosphereAsyncComputeFormulaName,
                " FogLutPolicy=", BurtAtmosphereUtility.SupportsAtmosphereFogLut ? "PCFull" : "MobileDummyNoAerial",
                " LastEnsureFrame=", lastEnsureFrame,
                " LastEnsureCamera=", lastEnsureCameraId,
                " SkyProducerCamera=", lastSkyProducerCameraId,
                " FogProducerCamera=", lastFogProducerCameraId,
                " DispatchCounts=", physicalDispatchCount, "/", skyDispatchCount, "/", fogDispatchCount,
                " AsyncPending=", hasPendingAsyncFence,
                " AsyncHorizontalPublishPending=", pendingHorizontalPublish,
                " AsyncStatus=", lastAsyncStatus,
                " AsyncAttempts=", asyncAttemptCount,
                " AsyncCleanSkips=", asyncCleanSkipCount,
                " AsyncResourceRecoveryDeferrals=", asyncResourceRecoveryDeferralCount,
                " AsyncDispatches=", asyncDispatchCount,
                " AsyncCompletions=", asyncCompletionCount,
                " AsyncFenceBalance=", asyncDispatchCount - asyncCompletionCount,
                " AsyncPendingProducer=", pendingAsyncProducerCameraId, "@", pendingAsyncProducerFrame,
                " AsyncLastCompletedProducer=", lastCompletedAsyncProducerCameraId, "@", lastCompletedAsyncProducerFrame,
                " KernelResourceBindings=", kernelResourceBindCount,
                " KernelResourceBindingFailures=", kernelResourceBindFailureCount,
                " LastKernelResourceBinding=", lastKernelResourceBinding,
                " RadianceContract=UnitLightLut*OuterLight*Occlusion*FogLuminanceScale",
                " FogCoordinates=CameraScreenFrustum",
                " FogProjection=", BurtAtmosphereUtility.AerialProjectionFormulaName,
                " FogReprojection=", BurtAtmosphereUtility.AtmosphereFogReprojectionFormulaName,
                " FogConsumer=", BurtAtmosphereUtility.AtmosphereFogConsumerFormulaName,
                " GraphicsUvStartsAtTop=", SystemInfo.graphicsUVStartsAtTop,
                " CameraAltitudeRatio=", cameraAltitudeRatio.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                " PackedScatteringSupported=", SupportsWritableSampledFormat(PackedScatteringFormat),
                " Transmittance=", FormatTextureState(transmittanceLut, TransmittanceWidth, TransmittanceHeight, 1, transmittanceFormat),
                " Multi=", FormatTextureState(multipleScatteringLut, MultipleScatteringSize, MultipleScatteringSize, 1, multipleScatteringFormat),
                " Horizontal=", FormatHorizontalScatteringBufferState(),
                " SkyView=", FormatTextureState(skyViewLut, SkyViewWidth, SkyViewHeight, 1, skyViewFormat),
                " Fog=", BurtAtmosphereUtility.SupportsAtmosphereFogLut
                    ? FormatTextureState(fogLut, FogLutWidth, FogLutHeight, FogLutDepth, fogFormat)
                    : "NotAllocated:Dummy=" + (fogLutDummy != null ? "Ready" : "Null"),
                " FogTopology=", FogLutWidth, "x", FogLutHeight, "x", FogLutDepth,
                "x", FogLutSamplesPerSlice, "@", FogLutCoverageKm, "km",
                " TotalLutMemory=", FormatMemoryBytes(
                    CalculateTextureMemoryBytes(transmittanceLut)
                    + CalculateTextureMemoryBytes(multipleScatteringLut)
                    + CalculateBufferMemoryBytes(horizontalScatteringStructuredBuffer)
                    + CalculateBufferMemoryBytes(horizontalScatteringConstantBuffer)
                    + CalculateTextureMemoryBytes(skyViewLut)
                    + CalculateTextureMemoryBytes(fogLut)));
        }

        public static void Release()
        {
            ReleaseTexture(ref transmittanceLut);
            ReleaseTexture(ref multipleScatteringLut);
            ReleaseBuffer(ref horizontalScatteringStructuredBuffer);
            ReleaseBuffer(ref horizontalScatteringConstantBuffer);
            ReleaseTexture(ref skyViewLut);
            ReleaseTexture(ref fogLut);
            ReleaseTexture(ref fogLutDummy);
            computeShader = null;
            transmittanceKernel = -1;
            multipleScatteringKernel = -1;
            horizontalScatteringKernel = -1;
            skyViewKernel = -1;
            fogKernel = -1;
            hasPhysicalState = false;
            hasSkyState = false;
            hasFogState = false;
            lastPhysicalStateHash = 0;
            lastSkyStateHash = 0;
            lastFogStateHash = 0;
            lastPhysicalState = default;
            lastSkyState = default;
            lastFogState = default;
            lastEnsureFrame = -1;
            lastEnsureCameraId = 0;
            lastSkyProducerCameraId = 0;
            lastFogProducerCameraId = 0;
            physicalDispatchCount = 0;
            skyDispatchCount = 0;
            fogDispatchCount = 0;
            pendingAsyncFence = default;
            hasPendingAsyncFence = false;
            pendingHorizontalPublish = false;
            asyncDispatchCount = 0;
            asyncCompletionCount = 0;
            asyncAttemptCount = 0;
            asyncCleanSkipCount = 0;
            asyncResourceRecoveryDeferralCount = 0;
            pendingAsyncProducerCameraId = 0;
            pendingAsyncProducerFrame = -1;
            lastCompletedAsyncProducerCameraId = 0;
            lastCompletedAsyncProducerFrame = -1;
            lastAsyncStatus = "NeverAttempted";
            kernelResourceBindCount = 0;
            kernelResourceBindFailureCount = 0;
            lastKernelResourceBinding = "NeverBound";
            cameraAltitudeRatio = 0f;
            worldToSkyViewLocal = Matrix4x4.identity;
            transmittanceFormat = GraphicsFormat.None;
            multipleScatteringFormat = GraphicsFormat.None;
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

            var packedScatteringSupported =
                SupportsWritableSampledFormat(PackedScatteringFormat);
            var highPrecisionSupported =
                SupportsWritableSampledFormat(HighPrecisionFormat);
            if ((!packedScatteringSupported ||
                    BurtAtmosphereUtility.SupportsAtmosphereFogLut) &&
                !highPrecisionSupported)
            {
                return FailInitialization(
                    "BurtRP atmosphere LUTs require either XRender's sampled " +
                    "B10G11R11_UFloatPack32 LoadStore format or the explicit " +
                    "R16G16B16A16_SFloat stock-Unity compatibility format; " +
                    "the analytic atmosphere fallback remains active.");
            }

            // XRender uses packed R11G11B10 unconditionally. Stock Unity 2022
            // D3D11 can report that format as sampleable but not typed-UAV
            // writable, while XRender's custom player supports it. Keep the
            // exact format wherever available and expose an explicit RGBA16F
            // compatibility path so the port remains functional in BRP's
            // stock editor instead of silently disabling the entire LUT chain.
            var scatteringFormat = packedScatteringSupported
                ? PackedScatteringFormat
                : HighPrecisionFormat;
            transmittanceFormat = scatteringFormat;
            multipleScatteringFormat = scatteringFormat;
            skyViewFormat = scatteringFormat;
            // Aerial perspective stores RGB in-scattering plus scalar transmittance in A.
            fogFormat = BurtAtmosphereUtility.SupportsAtmosphereFogLut
                ? HighPrecisionFormat
                : GraphicsFormat.None;

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
            if (!EnsureHorizontalScatteringBuffers(out var horizontalScatteringRecreated))
            {
                return false;
            }
            skyViewLut = EnsureTexture(skyViewLut, SkyViewWidth, SkyViewHeight, 1, TextureDimension.Tex2D, skyViewFormat, "Burt Atmosphere Sky View LUT", out var skyViewRecreated);
            if (BurtAtmosphereUtility.SupportsAtmosphereFogLut)
            {
                fogLut = EnsureTexture(fogLut, FogLutWidth, FogLutHeight, FogLutDepth, TextureDimension.Tex3D, fogFormat, "Burt Atmosphere Fog LUT", out fogResourcesRecreated);
            }
            else
            {
                fogResourcesRecreated = fogLut != null;
                ReleaseTexture(ref fogLut);
                if (!EnsureFogLutDummy())
                {
                    return false;
                }

                hasFogState = false;
            }

            physicalResourcesRecreated = transmittanceRecreated || multipleScatteringRecreated;
            skyResourcesRecreated = horizontalScatteringRecreated || skyViewRecreated;
            if (physicalResourcesRecreated)
            {
                hasPhysicalState = false;
                hasSkyState = false;
                hasFogState = false;
            }
            else
            {
                hasSkyState &= !skyResourcesRecreated;
                hasFogState &= !fogResourcesRecreated;
            }
            if (!AreRequiredLutsReady())
            {
                return FailInitialization("BurtRP could not create one or more atmosphere LUT textures; the analytic atmosphere fallback remains active.");
            }

            // Match XRender's ParamTable contract: SRV/UAV state belongs to the
            // individual dispatch, not to ambient mutable ComputeShader state.
            // BindKernelResources is therefore the sole resource-binding path.
            return true;
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

        private static bool EnsureHorizontalScatteringBuffers(out bool recreated)
        {
            if (AreHorizontalScatteringBuffersReady())
            {
                recreated = false;
                return true;
            }

            recreated = true;
            ReleaseBuffer(ref horizontalScatteringStructuredBuffer);
            ReleaseBuffer(ref horizontalScatteringConstantBuffer);
            try
            {
                horizontalScatteringStructuredBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured | GraphicsBuffer.Target.CopySource,
                    HorizontalScatteringElementCount,
                    HorizontalScatteringElementStride)
                {
                    name = "Burt Atmosphere Horizontal Scattering LUT"
                };
                horizontalScatteringConstantBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Constant | GraphicsBuffer.Target.CopyDestination,
                    HorizontalScatteringElementCount,
                    HorizontalScatteringElementStride)
                {
                    name = "Burt Atmosphere Horizontal Scattering LUT CB"
                };
            }
            catch (System.Exception exception)
            {
                return FailInitialization(
                    "BurtRP could not create the paired atmosphere horizontal-scattering buffers; " +
                    "the analytic atmosphere fallback remains active. " + exception.Message);
            }

            return AreHorizontalScatteringBuffersReady() ||
                FailInitialization(
                    "BurtRP created invalid atmosphere horizontal-scattering buffers; " +
                    "the analytic atmosphere fallback remains active.");
        }

        private static void UploadParameters(
            CommandBuffer cmd,
            BurtAtmosphereSettings settings,
            Vector3 sunDirection,
            float cameraAltitudeKm,
            Vector3 fogViewRayUv00,
            Vector3 fogViewRayUv10,
            Vector3 fogViewRayUv01,
            Vector3 fogViewRayUv11)
        {
            cmd.SetComputeFloatParam(computeShader, MieAnisotropyId, settings.MieAnisotropy);
            var effectiveCoefficients = BurtAtmosphereUtility.ResolveEffectiveCoefficients(settings);
            cmd.SetComputeVectorParam(
                computeShader,
                RayleighScatteringCoefficientId,
                effectiveCoefficients.RayleighScattering);
            cmd.SetComputeVectorParam(
                computeShader,
                MieScatteringCoefficientId,
                effectiveCoefficients.MieScattering);
            cmd.SetComputeVectorParam(
                computeShader,
                MieAbsorptionCoefficientId,
                effectiveCoefficients.MieAbsorption);
            cmd.SetComputeVectorParam(
                computeShader,
                MieExtinctionCoefficientId,
                effectiveCoefficients.MieExtinction);
            cmd.SetComputeVectorParam(
                computeShader,
                OzoneAbsorptionCoefficientId,
                effectiveCoefficients.OzoneAbsorption);
            var densityProfile = BurtAtmosphereUtility.ResolveDensityProfile(settings);
            cmd.SetComputeVectorParam(
                computeShader,
                DensityProfileParamsId,
                new Vector4(
                    densityProfile.RayleighDensityExpScale,
                    densityProfile.MieDensityExpScale,
                    densityProfile.OzoneLayerSplitAltitude,
                    0f));
            cmd.SetComputeVectorParam(
                computeShader,
                OzoneDensityProfileParamsId,
                new Vector4(
                    densityProfile.OzoneDensity0LinearTerm,
                    densityProfile.OzoneDensity0ConstantTerm,
                    densityProfile.OzoneDensity1LinearTerm,
                    densityProfile.OzoneDensity1ConstantTerm));
            // ResolveSettings mirrors XRender's RenderProxy and converts the
            // authored ground albedo to linear exactly once, before it also
            // participates in LUT cache keys.
            cmd.SetComputeVectorParam(computeShader, GroundAlbedoId, settings.GroundAlbedo);
            cmd.SetComputeFloatParam(computeShader, MultipleScatteringIntensityId, settings.MultipleScatteringIntensity);
            cmd.SetComputeFloatParam(computeShader, TraceSampleCountScaleId, settings.TraceSampleCountScale);
            cmd.SetComputeVectorParam(computeShader, SkyLuminanceFactorId, settings.SkyLuminanceFactor);
            cmd.SetComputeVectorParam(computeShader, PlanetParamsId, new Vector4(settings.PlanetRadius, settings.AtmosphereHeight, settings.RayleighScaleHeight, settings.MieScaleHeight));
            cmd.SetComputeVectorParam(computeShader, SunDirectionId, sunDirection.normalized);
            cmd.SetComputeVectorParam(computeShader, FogViewRayBottomLeftId, fogViewRayUv00);
            cmd.SetComputeVectorParam(computeShader, FogViewRayBottomRightId, fogViewRayUv10);
            cmd.SetComputeVectorParam(computeShader, FogViewRayTopLeftId, fogViewRayUv01);
            cmd.SetComputeVectorParam(computeShader, FogViewRayTopRightId, fogViewRayUv11);
            // Compute-space fog distances are physical kilometers; material-space aerial parameters remain world units.
            cmd.SetComputeVectorParam(computeShader, AerialLutParamsId, new Vector4(
                settings.AerialPerspectiveDensityScale,
                FogLutCoverageKm,
                FogLutSamplesPerSlice,
                settings.AerialPerspectiveStartDepth * settings.WorldToKilometers));
            cmd.SetComputeFloatParam(computeShader, CameraAltitudeKmId, cameraAltitudeKm);
        }

        private static bool Dispatch(CommandBuffer cmd, int kernel, int width, int height, int depth)
        {
            if (cmd == null || computeShader == null || kernel < 0)
            {
                return FailInitialization("BurtRP tried to dispatch an invalid atmosphere LUT kernel; the analytic atmosphere fallback remains active.");
            }

            var kernelName = ResolveKernelName(kernel);
            try
            {
                if (!BindKernelResources(cmd, kernel, out var bindingFailure))
                {
                    kernelResourceBindFailureCount++;
                    lastKernelResourceBinding = kernelName + ":Missing:" + bindingFailure;
                    return FailInitialization(
                        "BurtRP atmosphere LUT kernel '" + kernelName +
                        "' is missing required compute resource '" + bindingFailure +
                        "' before dispatch; the analytic atmosphere fallback remains active.");
                }
            }
            catch (System.Exception exception)
            {
                kernelResourceBindFailureCount++;
                lastKernelResourceBinding = kernelName + ":BindException:" + exception.GetType().Name;
                return FailInitialization(
                    "BurtRP could not bind resources for atmosphere LUT kernel '" +
                    kernelName + "'; the analytic atmosphere fallback remains active. " +
                    exception.Message);
            }

            kernelResourceBindCount++;
            lastKernelResourceBinding = kernelName + ":Complete";
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
                return FailInitialization(
                    "BurtRP could not dispatch atmosphere LUT kernel '" + kernelName +
                    "'; the analytic atmosphere fallback remains active. " +
                    exception.Message);
            }
        }

        private static bool BindKernelResources(
            CommandBuffer cmd,
            int kernel,
            out string missingResource)
        {
            missingResource = null;
            if (kernel == transmittanceKernel)
            {
                if (!IsTextureReady(transmittanceLut))
                {
                    missingResource = "_BurtAtmosphereTransmittanceLut";
                    return false;
                }

                cmd.SetComputeTextureParam(computeShader, kernel, TransmittanceOutputId, transmittanceLut);
                return true;
            }

            if (kernel == multipleScatteringKernel)
            {
                if (!IsTextureReady(multipleScatteringLut))
                {
                    missingResource = "_BurtAtmosphereMultipleScatteringLut";
                    return false;
                }

                if (!IsTextureReady(transmittanceLut))
                {
                    missingResource = "_BurtAtmosphereTransmittanceLutInput";
                    return false;
                }

                cmd.SetComputeTextureParam(computeShader, kernel, MultipleScatteringOutputId, multipleScatteringLut);
                cmd.SetComputeTextureParam(computeShader, kernel, TransmittanceInputId, transmittanceLut);
                return true;
            }

            if (kernel == skyViewKernel)
            {
                if (!IsTextureReady(skyViewLut))
                {
                    missingResource = "_BurtAtmosphereSkyViewLut";
                    return false;
                }

                if (!IsTextureReady(transmittanceLut))
                {
                    missingResource = "_BurtAtmosphereTransmittanceLutInput";
                    return false;
                }

                if (!IsTextureReady(multipleScatteringLut))
                {
                    missingResource = "_BurtAtmosphereMultipleScatteringLutInput";
                    return false;
                }

                cmd.SetComputeTextureParam(computeShader, kernel, SkyViewOutputId, skyViewLut);
                cmd.SetComputeTextureParam(computeShader, kernel, TransmittanceInputId, transmittanceLut);
                cmd.SetComputeTextureParam(computeShader, kernel, MultipleScatteringInputId, multipleScatteringLut);
                return true;
            }

            if (kernel == horizontalScatteringKernel)
            {
                if (horizontalScatteringStructuredBuffer == null ||
                    !horizontalScatteringStructuredBuffer.IsValid())
                {
                    missingResource = "_BurtAtmosphereHorizontalScatteringLutUav";
                    return false;
                }

                if (!IsTextureReady(transmittanceLut))
                {
                    missingResource = "_BurtAtmosphereTransmittanceLutInput";
                    return false;
                }

                if (!IsTextureReady(multipleScatteringLut))
                {
                    missingResource = "_BurtAtmosphereMultipleScatteringLutInput";
                    return false;
                }

                cmd.SetComputeBufferParam(computeShader, kernel, HorizontalScatteringOutputId, horizontalScatteringStructuredBuffer);
                cmd.SetComputeTextureParam(computeShader, kernel, TransmittanceInputId, transmittanceLut);
                cmd.SetComputeTextureParam(computeShader, kernel, MultipleScatteringInputId, multipleScatteringLut);
                return true;
            }

            if (kernel == fogKernel)
            {
                if (!IsTextureReady(fogLut))
                {
                    missingResource = "_BurtAtmosphereFogLut";
                    return false;
                }

                if (!IsTextureReady(transmittanceLut))
                {
                    missingResource = "_BurtAtmosphereTransmittanceLutInput";
                    return false;
                }

                if (!IsTextureReady(multipleScatteringLut))
                {
                    missingResource = "_BurtAtmosphereMultipleScatteringLutInput";
                    return false;
                }

                cmd.SetComputeTextureParam(computeShader, kernel, FogOutputId, fogLut);
                cmd.SetComputeTextureParam(computeShader, kernel, TransmittanceInputId, transmittanceLut);
                cmd.SetComputeTextureParam(computeShader, kernel, MultipleScatteringInputId, multipleScatteringLut);
                return true;
            }

            missingResource = "UnknownKernel";
            return false;
        }

        private static bool IsTextureReady(RenderTexture texture)
        {
            return texture != null && texture.IsCreated();
        }

        private static string ResolveKernelName(int kernel)
        {
            if (kernel == transmittanceKernel)
            {
                return "BuildTransmittanceLut";
            }

            if (kernel == multipleScatteringKernel)
            {
                return "BuildMultipleScatteringLut";
            }

            if (kernel == horizontalScatteringKernel)
            {
                return "BuildHorizontalScatteringLut";
            }

            if (kernel == skyViewKernel)
            {
                return "BuildSkyViewLut";
            }

            if (kernel == fogKernel)
            {
                return "BuildFogLut";
            }

            return "Kernel#" + kernel;
        }

        private static bool PublishHorizontalScattering(CommandBuffer cmd)
        {
            if (cmd == null || !AreHorizontalScatteringBuffersReady())
            {
                return FailInitialization(
                    "BurtRP could not publish the atmosphere horizontal-scattering buffer; " +
                    "the analytic atmosphere fallback remains active.");
            }

            try
            {
                cmd.CopyBuffer(horizontalScatteringStructuredBuffer, horizontalScatteringConstantBuffer);
                BindHorizontalScatteringConstantBuffer(cmd);
                return true;
            }
            catch (System.Exception exception)
            {
                return FailInitialization(
                    "BurtRP could not synchronize the atmosphere horizontal-scattering buffers; " +
                    "the analytic atmosphere fallback remains active. " + exception.Message);
            }
        }

        private static void BindHorizontalScatteringConstantBuffer(CommandBuffer cmd)
        {
            if (cmd == null || !AreHorizontalScatteringBuffersReady())
            {
                return;
            }

            cmd.SetGlobalConstantBuffer(
                horizontalScatteringConstantBuffer,
                HorizontalScatteringConstantBufferId,
                0,
                HorizontalScatteringBufferSize);
        }

        private static bool AreHorizontalScatteringBuffersReady()
        {
            return horizontalScatteringStructuredBuffer != null
                && horizontalScatteringStructuredBuffer.IsValid()
                && horizontalScatteringStructuredBuffer.count == HorizontalScatteringElementCount
                && horizontalScatteringStructuredBuffer.stride == HorizontalScatteringElementStride
                && horizontalScatteringConstantBuffer != null
                && horizontalScatteringConstantBuffer.IsValid()
                && horizontalScatteringConstantBuffer.count == HorizontalScatteringElementCount
                && horizontalScatteringConstantBuffer.stride == HorizontalScatteringElementStride;
        }

        private static bool AreCoreLutsReady()
        {
            return !initializationFailed
                && transmittanceLut != null && transmittanceLut.IsCreated()
                && multipleScatteringLut != null && multipleScatteringLut.IsCreated()
                && AreHorizontalScatteringBuffersReady()
                && skyViewLut != null && skyViewLut.IsCreated();
        }

        private static bool IsFogLutReady()
        {
            return !initializationFailed &&
                fogLut != null &&
                fogLut.IsCreated();
        }

        private static bool AreRequiredLutsReady()
        {
            return AreCoreLutsReady() &&
                (BurtAtmosphereUtility.SupportsAtmosphereFogLut
                    ? IsFogLutReady()
                    : fogLutDummy != null);
        }

        private static Texture ResolveFogLutBinding()
        {
            if (IsFogLutReady())
            {
                return fogLut;
            }

            return EnsureFogLutDummy() ? fogLutDummy : null;
        }

        private static bool EnsureFogLutDummy()
        {
            if (fogLutDummy != null)
            {
                return true;
            }

            try
            {
                fogLutDummy = new Texture3D(1, 1, 1, TextureFormat.RGBA32, false, true)
                {
                    name = "Burt Atmosphere Fog LUT Dummy",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave
                };
                // XRender's mobile dummy represents zero in-scattering and
                // full transmittance.
                fogLutDummy.SetPixel(0, 0, 0, new Color(0f, 0f, 0f, 1f));
                fogLutDummy.Apply(false, true);
                return true;
            }
            catch (System.Exception exception)
            {
                return FailInitialization(
                    "BurtRP could not create the mobile atmosphere Fog LUT dummy. " +
                    exception.Message);
            }
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

            // A failure can happen after earlier dispatches were already recorded
            // into the caller's CommandBuffer. Releasing persistent resources
            // here would invalidate those queued references. Keep this resource
            // generation alive but disabled; the pipeline's Release path owns
            // final destruction after submitted work has drained.
            hasPhysicalState = false;
            hasSkyState = false;
            hasFogState = false;
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

        private static string FormatHorizontalScatteringBufferState()
        {
            return string.Concat(
                "Structured=",
                FormatBufferState(horizontalScatteringStructuredBuffer),
                ",Constant=",
                FormatBufferState(horizontalScatteringConstantBuffer),
                ",Sync=CopyBuffer");
        }

        private static string FormatBufferState(GraphicsBuffer buffer)
        {
            if (buffer == null)
            {
                return "Null";
            }

            return string.Concat(
                buffer.IsValid() ? "Ready" : "Invalid",
                ":", buffer.count, "x", buffer.stride,
                ":", FormatMemoryBytes(CalculateBufferMemoryBytes(buffer)));
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

        private static long CalculateBufferMemoryBytes(GraphicsBuffer buffer)
        {
            return buffer != null && buffer.IsValid()
                ? (long)buffer.count * buffer.stride
                : 0L;
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

        private static void ResolveFogViewRays(
            Camera camera,
            Matrix4x4 worldToLocal,
            out Vector3 uv00,
            out Vector3 uv10,
            out Vector3 uv01,
            out Vector3 uv11)
        {
            if (camera == null)
            {
                uv00 = uv10 = uv01 = uv11 = Vector3.forward;
                return;
            }

            // Match XRender's _FrustumRay0/1/2 construction. In particular, use
            // the non-jittered GPU projection so TAA sub-pixel offsets do not
            // invalidate a 32x32x16 physical fog volume every frame.
            var cpuProjection = camera.nonJitteredProjectionMatrix;
            if (cpuProjection == Matrix4x4.zero)
            {
                cpuProjection = camera.projectionMatrix;
            }

            var projection = GL.GetGPUProjectionMatrix(cpuProjection, true);
            var cameraToWorld = camera.cameraToWorldMatrix;
            var uvStartsAtTop = SystemInfo.graphicsUVStartsAtTop;
            var ndcYAtUv0 = uvStartsAtTop ? 1f : -1f;
            var ndcYAtUv1 = uvStartsAtTop ? -1f : 1f;
            var projectionX = Mathf.Abs(projection[0, 0]) > 0.000001f ? projection[0, 0] : 1f;
            var projectionY = Mathf.Abs(projection[1, 1]) > 0.000001f ? projection[1, 1] : 1f;
            Vector3 view00;
            Vector3 view10;
            Vector3 view01;
            Vector3 view11;

            if (!camera.orthographic)
            {
                // proj[0,2]/proj[1,2] preserve lens shift and off-center frusta.
                var centerX = projection[0, 2];
                var centerY = projection[1, 2];
                var inverseX = 1f / projectionX;
                var inverseY = 1f / projectionY;
                view00 = new Vector3((-1f + centerX) * inverseX, (ndcYAtUv0 + centerY) * inverseY, -1f);
                view10 = new Vector3((1f + centerX) * inverseX, (ndcYAtUv0 + centerY) * inverseY, -1f);
                view01 = new Vector3((-1f + centerX) * inverseX, (ndcYAtUv1 + centerY) * inverseY, -1f);
                view11 = new Vector3((1f + centerX) * inverseX, (ndcYAtUv1 + centerY) * inverseY, -1f);
            }
            else
            {
                // XRender keeps the orthographic plane offset and adds a long
                // forward vector before normalization instead of collapsing all
                // pixels to an identical camera-forward direction.
                var halfWidth = 1f / projectionX;
                var halfHeight = 1f / projectionY;
                view00 = new Vector3(-halfWidth, ndcYAtUv0 * halfHeight, -FrustumRayReconstructionDepth);
                view10 = new Vector3(halfWidth, ndcYAtUv0 * halfHeight, -FrustumRayReconstructionDepth);
                view01 = new Vector3(-halfWidth, ndcYAtUv1 * halfHeight, -FrustumRayReconstructionDepth);
                view11 = new Vector3(halfWidth, ndcYAtUv1 * halfHeight, -FrustumRayReconstructionDepth);
            }

            uv00 = worldToLocal.MultiplyVector(cameraToWorld.MultiplyVector(view00));
            uv10 = worldToLocal.MultiplyVector(cameraToWorld.MultiplyVector(view10));
            uv01 = worldToLocal.MultiplyVector(cameraToWorld.MultiplyVector(view01));
            uv11 = worldToLocal.MultiplyVector(cameraToWorld.MultiplyVector(view11));
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

            // XRender's cached mobile SkyView referential is world-fixed so
            // rotating the camera does not invalidate SkyView/Horizontal.
            var referenceForward = BurtAtmosphereUtility.IsMobileAtmospherePlatform
                ? Vector3.forward
                : (camera != null ? camera.transform.forward : Vector3.forward);
            var left = Vector3.Cross(referenceForward, up);
            left.Normalize();
            Vector3 forward;
            if (Mathf.Abs(Vector3.Dot(up, referenceForward)) > 0.999f)
            {
                // Match XRender's Duff et al. orthonormal-basis fallback. Using
                // an arbitrary world axis here rotates the SkyView LUT around
                // the zenith as the camera crosses the near-parallel threshold.
                var sign = up.z >= 0f ? 1f : -1f;
                var a = -1f / (sign + up.z);
                var b = up.x * up.y * a;
                forward = new Vector3(
                    1f + sign * a * up.x * up.x,
                    sign * b,
                    -sign * up.x);
                left = new Vector3(
                    b,
                    sign + a * up.y * up.y,
                    -up.y);
            }
            else
            {
                forward = Vector3.Cross(up, left);
                forward.Normalize();
            }

            worldToLocal = Matrix4x4.identity;
            worldToLocal.SetRow(0, new Vector4(left.x, left.y, left.z, 0f));
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

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
            {
                return;
            }

            buffer.Release();
            buffer = null;
        }

        private static void ReleaseTexture(ref Texture3D texture)
        {
            if (texture == null)
            {
                return;
            }

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
