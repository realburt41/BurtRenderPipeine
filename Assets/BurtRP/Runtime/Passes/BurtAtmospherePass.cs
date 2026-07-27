using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    internal sealed class BurtDrawAtmospherePass : BurtRenderPass
    {
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
        private static readonly int MainLightOcclusionFactorId = Shader.PropertyToID("_BurtMainLightOcclusionFactor");
        private static readonly int SunDirectionId = Shader.PropertyToID("_BurtAtmosphereSunDirection");
        private static readonly int SunParamsId = Shader.PropertyToID("_BurtAtmosphereSunParams");
        private static readonly int SunDiskLuminanceAndCosHalfApexId = Shader.PropertyToID("_BurtAtmosphereSunDiskLuminanceAndCosHalfApex");
        private static readonly int HorizonColorId = Shader.PropertyToID("_BurtAtmosphereHorizonColor");
        private static readonly int HorizonSunsetColorId = Shader.PropertyToID("_BurtAtmosphereHorizonSunsetColor");
        private static readonly int HorizonParamsId = Shader.PropertyToID("_BurtAtmosphereHorizonParams");
        private static readonly int GroundParamsId = Shader.PropertyToID("_BurtAtmosphereGroundParams");
        private static readonly int ExposureParamsId = Shader.PropertyToID("_BurtAtmosphereExposureParams");
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
        private static readonly int MoonDirectionId = Shader.PropertyToID("_BurtAtmosphereMoonDirection");
        private static readonly int MoonUpId = Shader.PropertyToID("_BurtAtmosphereMoonUp");
        private static readonly int MoonRightId = Shader.PropertyToID("_BurtAtmosphereMoonRight");
        private static readonly int MoonSurfaceTintId = Shader.PropertyToID("_BurtAtmosphereMoonSurfaceTint");
        private static readonly int MoonFlareTintId = Shader.PropertyToID("_BurtAtmosphereMoonFlareTint");
        private static readonly int MoonGeometryId = Shader.PropertyToID("_BurtAtmosphereMoonGeometry");
        private static readonly int MoonPhaseId = Shader.PropertyToID("_BurtAtmosphereMoonPhase");
        private static readonly int MoonVisibilityId = Shader.PropertyToID("_BurtAtmosphereMoonVisibility");
        private static readonly int AerialPerspectiveParamsId = Shader.PropertyToID("_BurtAtmosphereAerialPerspectiveParams");
        private static readonly int AerialPerspectiveTintId = Shader.PropertyToID("_BurtAtmosphereAerialPerspectiveTint");
        private static readonly int AerialPerspectiveFadeParamsId = Shader.PropertyToID("_BurtAtmosphereAerialPerspectiveFadeParams");
        private static readonly int FogLutDistanceParamsId = Shader.PropertyToID("_BurtAtmosphereFogLutDistanceParams");
        private static readonly int InverseViewProjectionId = Shader.PropertyToID("_BurtAtmosphereInverseViewProjection");
        private static readonly int CameraPositionWSId = Shader.PropertyToID("_BurtAtmosphereCameraPositionWS");
        private static readonly int DebugModeId = Shader.PropertyToID("_BurtAtmosphereDebugMode");

        private Material material;
        private bool hasLoggedMissingShader;

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

            var drawMaterial = GetMaterial();
            if (drawMaterial == null)
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

            var cmd = CommandBufferPool.Get(Name);
            var sunDirection4 = ResolveSunDirection(request, settings);
            BurtAtmosphereLutUtility.EnsureLuts(cmd, camera, settings, new Vector3(sunDirection4.x, sunDirection4.y, sunDirection4.z));
            UploadMaterialProperties(drawMaterial, camera, request, settings);
            cmd.SetRenderTarget(cameraColorTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            cmd.SetGlobalTexture(CameraDepthTextureId, cameraDepthTarget.Identifier);
            cmd.DrawProcedural(Matrix4x4.identity, drawMaterial, 0, MeshTopology.Triangles, 3, 1);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        internal static void UploadMaterialProperties(Material targetMaterial, Camera camera, BurtRenderRequest request, BurtAtmosphereSettings settings)
        {
            targetMaterial.SetFloat(RayleighIntensityId, settings.RayleighIntensity);
            targetMaterial.SetFloat(MieIntensityId, settings.MieIntensity);
            targetMaterial.SetFloat(MieAnisotropyId, settings.MieAnisotropy);
            targetMaterial.SetVector(RayleighScatteringCoefficientId, settings.RayleighScatteringCoefficient);
            targetMaterial.SetVector(MieScatteringCoefficientId, settings.MieScatteringCoefficient);
            targetMaterial.SetVector(MieAbsorptionCoefficientId, settings.MieAbsorptionCoefficient);
            targetMaterial.SetVector(OzoneAbsorptionCoefficientId, settings.OzoneAbsorptionCoefficient);
            targetMaterial.SetVector(PlanetParamsId, new Vector4(settings.PlanetRadius, settings.AtmosphereHeight, settings.RayleighScaleHeight, settings.MieScaleHeight));
            targetMaterial.SetColor(GroundColorId, settings.GroundColor);
            targetMaterial.SetColor(SkyTintId, settings.SkyTint);
            targetMaterial.SetColor(SkyLuminanceFactorId, settings.SkyLuminanceFactor);
            targetMaterial.SetFloat(SunIntensityId, settings.SunIntensity);
            targetMaterial.SetFloat(MainLightOcclusionFactorId, settings.MainLightOcclusion);
            targetMaterial.SetVector(SunDirectionId, ResolveSunDirection(request, settings));
            targetMaterial.SetVector(SunParamsId, new Vector4(settings.SunDiskSize, settings.SunDiskIntensity, settings.SunHaloSize, settings.SunHaloIntensity));
            targetMaterial.SetVector(SunDiskLuminanceAndCosHalfApexId, BurtAtmosphereUtility.EvaluateSunDiskLuminanceAndCosHalfApex(request, settings));
            targetMaterial.SetColor(HorizonColorId, settings.HorizonColor);
            targetMaterial.SetColor(HorizonSunsetColorId, settings.HorizonSunsetColor);
            targetMaterial.SetVector(HorizonParamsId, new Vector4(settings.HorizonIntensity, settings.HorizonFalloff, settings.HorizonSunsetInfluence, 0f));
            targetMaterial.SetVector(GroundParamsId, new Vector4(settings.GroundContribution, settings.GroundBlendStart, settings.GroundBlendEnd, 0f));
            targetMaterial.SetVector(ExposureParamsId, new Vector4(Mathf.Pow(2f, settings.ExposureCompensation), settings.TonemapSafeSunIntensity, settings.ExposureCompensation, 0f));
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
            targetMaterial.SetVector(MoonDirectionId, new Vector4(moonDirection.x, moonDirection.y, moonDirection.z, 0f));
            targetMaterial.SetVector(MoonUpId, new Vector4(moonUp.x, moonUp.y, moonUp.z, 0f));
            targetMaterial.SetVector(MoonRightId, new Vector4(moonRight.x, moonRight.y, moonRight.z, 0f));
            targetMaterial.SetColor(MoonSurfaceTintId, moon.SurfaceTint);
            targetMaterial.SetColor(MoonFlareTintId, moon.FlareTint);
            targetMaterial.SetVector(MoonGeometryId, BurtAtmosphereUtility.EvaluateMoonDiskLuminanceAndGeometry(moon));
            targetMaterial.SetVector(MoonPhaseId, new Vector4(
                Mathf.Sin(phaseRadians),
                Mathf.Cos(phaseRadians),
                Mathf.Sin(phaseRotationRadians),
                Mathf.Cos(phaseRotationRadians)));
            targetMaterial.SetVector(MoonVisibilityId, new Vector4(
                moon.Enabled ? 1f : 0f,
                moon.Earthshine,
                moon.RiseBlendMin,
                moon.RiseBlendMax));
            // XY are retained for the analytic fallback only; the physical LUT
            // follows XRender's density/luminance/sampling-scale controls.
            targetMaterial.SetVector(AerialPerspectiveParamsId, new Vector4(settings.AerialPerspectiveIntensity, settings.AerialPerspectiveDistance, settings.AerialPerspectiveHeightFalloff, settings.AerialPerspectiveEnabled ? 1f : 0f));
            targetMaterial.SetColor(AerialPerspectiveTintId, settings.AerialPerspectiveTint);
            // XRender's atmosphere fog LUT has a fixed 96 km physical coverage.
            const float fogLutCoverageKm = 96f;
            targetMaterial.SetVector(FogLutDistanceParamsId, new Vector4(
                settings.WorldToKilometers,
                fogLutCoverageKm,
                settings.AerialPerspectiveSamplingDistanceScale,
                settings.AerialPerspectiveLuminanceScale));
            var affectsSkyPixels = settings.AerialPerspectivePlacement == AtmosphereAerialPerspectivePlacement.AfterSkyBeforeSSR
                || settings.AerialPerspectivePlacement == AtmosphereAerialPerspectivePlacement.BeforeTransparent;
            targetMaterial.SetVector(AerialPerspectiveFadeParamsId, new Vector4(settings.AerialPerspectiveNearFadeStart, settings.AerialPerspectiveNearFadeEnd, settings.AerialPerspectiveMaxOpacity, affectsSkyPixels ? 1f : 0f));
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

        private static Matrix4x4 ResolveInverseViewProjection(Camera camera)
        {
            var view = camera.worldToCameraMatrix;
            var projection = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
            return (projection * view).inverse;
        }

        private Material GetMaterial()
        {
            if (material != null)
            {
                return material;
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

            material = new Material(shader);
            material.hideFlags = HideFlags.HideAndDontSave;
            return material;
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
        private static readonly int AerialSourceColorTextureId = Shader.PropertyToID("_BurtAtmosphereAerialSourceColorTexture");

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

            var cmd = CommandBufferPool.Get(Name);
            var sunDirection4 = BurtDrawAtmospherePass.ResolveSunDirection(request, settings);
            BurtAtmosphereLutUtility.EnsureLuts(cmd, camera, settings, new Vector3(sunDirection4.x, sunDirection4.y, sunDirection4.z));
            BurtDrawAtmospherePass.UploadMaterialProperties(drawMaterial, camera, request, settings);
            var descriptor = BurtRenderTargetDescriptorUtility.CreateCameraColorDescriptor(camera);
            cmd.GetTemporaryRT(AerialSourceColorTextureId, descriptor, FilterMode.Bilinear);
            cmd.Blit(cameraColorTarget.Identifier, new RenderTargetIdentifier(AerialSourceColorTextureId));
            cmd.SetRenderTarget(cameraColorTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            cmd.SetGlobalTexture(CameraColorTextureId, new RenderTargetIdentifier(AerialSourceColorTextureId));
            cmd.SetGlobalTexture(CameraDepthTextureId, cameraDepthTarget.Identifier);
            cmd.DrawProcedural(Matrix4x4.identity, drawMaterial, 1, MeshTopology.Triangles, 3, 1);
            cmd.ReleaseTemporaryRT(AerialSourceColorTextureId);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
