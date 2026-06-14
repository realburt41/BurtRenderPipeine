using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Burt.RenderPipeline
{
    public static class BurtSubsurfaceLutUtility
    {
        public const string TextureShaderName = "_BurtSubsurfacePreIntegratedLut";
        public const string EnabledShaderName = "_BurtSubsurfacePreIntegratedLutEnabled";
        public const string SHLutShaderName = "_BurtSubsurfaceSHLut";
        public const string SHLutEnabledShaderName = "_BurtSubsurfaceSHLutEnabled";
        public const string ProfileParamLutShaderName = "_BurtSubsurfaceProfileParamLut";
        public const string ProfileParamLutEnabledShaderName = "_BurtSubsurfaceProfileParamLutEnabled";
        public const string ProfileParamLutSizeShaderName = "_BurtSubsurfaceProfileParamLutSize";

        public static readonly int TextureId = Shader.PropertyToID(TextureShaderName);
        public static readonly int EnabledId = Shader.PropertyToID(EnabledShaderName);
        public static readonly int SHLutId = Shader.PropertyToID(SHLutShaderName);
        public static readonly int SHLutEnabledId = Shader.PropertyToID(SHLutEnabledShaderName);
        public static readonly int ProfileParamLutId = Shader.PropertyToID(ProfileParamLutShaderName);
        public static readonly int ProfileParamLutEnabledId = Shader.PropertyToID(ProfileParamLutEnabledShaderName);
        public static readonly int ProfileParamLutSizeId = Shader.PropertyToID(ProfileParamLutSizeShaderName);

        public const int PreIntegratedLutSize = 256;
        public const int SHLutWidth = 512;
        public const int SHLutHeight = 3;
        private const int LutSize = PreIntegratedLutSize;
        private const float Pi = 3.14159265359f;
        private const float InvPi = 1f / Pi;
        private const float TwoPi = Pi * 2f;
        private const float FourPi = Pi * 4f;
        private const float HalfPi = Pi * 0.5f;
        private const float InvHalfPi = 1f / HalfPi;
        private const float SHIntegrationStep = 0.005f;
        private const float SurfaceAlbedoBias = 0.009f;
        private const float MeanFreePathBias = 0.009f;
        private const float DiffuseMeanFreePathToMeanFreePathMagicNumber = 0.6f;
        private const float CentimetersToMillimeters = 10f;
        private const float MillimetersToCentimeters = 0.1f;
        private const float EncodeWorldUnitScaleInCentimetersToUnit = 0.02f;
        private const float EncodeDiffuseMeanFreePathInMillimetersToUnit = 0.002f;
        private const float MaxDiffuseMeanFreePathInMillimeters = 500f;
        private const float SubsurfaceRadiusScale = 1024f;
        private const int LutFormulaVersion = 8;
        private const int ProfileSlotCount = BurtSubsurfaceProfilePalette.MaxProfiles;
        public const int ProfileParamLutWidth = 66;
        public const int ProfileParamLutHeight = ProfileSlotCount;
        public const int ProfileParamSurfaceAlbedoOffset = 0;
        public const int ProfileParamMeanFreePathOffset = 1;
        public const int ProfileParamTintOffset = 2;
        public const int ProfileParamBoundaryColorBleedOffset = 3;
        public const int ProfileParamDualSpecularOffset = 4;
        public const int ProfileParamTransmissionParamsOffset = 5;
        public const int ProfileParamTransmissionProfileOffset = 6;
        public const int ProfileParamTransmissionProfileSize = 32;
        public const int ProfileParamKernel0Offset = 38;
        public const int ProfileParamKernel0Size = 13;
        public const int ProfileParamKernel1Offset = 51;
        public const int ProfileParamKernel1Size = 9;
        public const int ProfileParamKernel2Offset = 60;
        public const int ProfileParamKernel2Size = 6;
        public const float MaxTransmissionProfileDistance = 10f;

        private static Texture2DArray preIntegratedLut;
        private static RenderTexture gpuPreIntegratedLut;
        private static Texture2DArray fallbackPreIntegratedLut;
        private static int preIntegratedLutHash;
        private static Texture2DArray shLut;
        private static RenderTexture gpuSHLut;
        private static Texture2DArray fallbackSHLut;
        private static int shLutHash;
        private static Texture2D profileParamLut;
        private static RenderTexture gpuProfileParamLut;
        private static Texture2D fallbackProfileParamLut;
        private static int profileParamLutHash;
        private static readonly int[] preIntegratedProfileHashes = new int[ProfileSlotCount];
        private static readonly int[] shProfileHashes = new int[ProfileSlotCount];
        private static readonly int[] profileParamRowHashes = new int[ProfileSlotCount];
        private static Color[] preIntegratedSlicePixels;
        private static Color[] shSlicePixels;
        private static Color[] profileParamRowPixels;
        private static bool preIntegratedProfileHashesValid;
        private static bool shProfileHashesValid;
        private static bool profileParamRowHashesValid;
        private static int lastPaletteHash;
        private static int lastProfileParamPaletteHash;
        private static bool hasLastPaletteHashes;
        private static ComputeShader lutBakerShader;
        private static ComputeBuffer profileParamSettingsBuffer;
        private static Vector4[] profileParamSettingsBufferData;
        private static bool gpuProfileParamLutRandomWrite;
        private static int profileParamKernel = -1;
        private static int preIntegratedKernel = -1;
        private static int shKernel = -1;
        private static bool lutBakerUnavailable;
        private static bool loggedGpuFallback;
        private const int ProfileParamSettingsVectorCount = 7;
        private static readonly int BakerProfileIndexId = Shader.PropertyToID("_BurtSubsurfaceLutProfileIndex");
        private static readonly int BakerProfileParamSettingsId = Shader.PropertyToID("_BurtSubsurfaceProfileSettings");
        private static readonly int BakerProfileParamSettingsStrideId = Shader.PropertyToID("_BurtSubsurfaceProfileSettingsStride");
        private static readonly int BakerPreIntegratedLutSizeId = Shader.PropertyToID("_BurtSubsurfacePreIntegratedLutSize");
        private static readonly int BakerSHLutSizeId = Shader.PropertyToID("_BurtSubsurfaceSHLutSize");
        private static readonly int BakerProfileParamLutRWId = Shader.PropertyToID("_BurtSubsurfaceProfileParamLutRW");
        private static readonly int BakerPreIntegratedLutRWId = Shader.PropertyToID("_BurtSubsurfacePreIntegratedLutRW");
        private static readonly int BakerSHLutRWId = Shader.PropertyToID("_BurtSubsurfaceSHLutRW");
#if UNITY_EDITOR
        private static double lastInteractiveTime;
        private static bool editorTextureRebuildUpdateRegistered;
        private const double InteractiveRebuildDelaySeconds = 0.35;
#endif

        public static Texture GetOrCreatePreIntegratedLut()
        {
            return GetOrCreatePreIntegratedLut(BurtSubsurfaceProfilePalette.Resolve(BurtSubsurfaceProfileSettings.Default, null));
        }

        public static Texture GetOrCreatePreIntegratedLut(BurtSubsurfaceProfilePalette palette)
        {
            if (gpuPreIntegratedLut != null && gpuPreIntegratedLut.IsCreated())
            {
                if (ShouldDeferTextureRebuild())
                {
                    return gpuPreIntegratedLut;
                }

                var cachedHash = GetCachedPaletteHash(palette);
                if (preIntegratedLutHash == cachedHash)
                {
                    return gpuPreIntegratedLut;
                }
            }
            else if (preIntegratedLut != null)
            {
                if (ShouldDeferTextureRebuild())
                {
                    return preIntegratedLut;
                }

                var cachedHash = GetCachedPaletteHash(palette);
                if (preIntegratedLutHash == cachedHash)
                {
                    return preIntegratedLut;
                }
            }
            else if (ShouldDeferTextureRebuild())
            {
                return null;
            }

            var currentHash = GetCachedPaletteHash(palette);
            if (TryUpdateGpuLutSlices(palette, currentHash, true))
            {
                return gpuPreIntegratedLut;
            }

            DestroyRenderTexture(gpuPreIntegratedLut);
            gpuPreIntegratedLut = null;
            EnsurePreIntegratedLutTexture();
            if (UpdatePreIntegratedLutSlices(palette))
            {
                preIntegratedLut.Apply(false, false);
            }

            preIntegratedLutHash = currentHash;
            return preIntegratedLut;
        }

        public static Texture GetFallbackPreIntegratedLut()
        {
            if (fallbackPreIntegratedLut != null)
            {
                return fallbackPreIntegratedLut;
            }

            fallbackPreIntegratedLut = new Texture2DArray(1, 1, ProfileSlotCount, SelectPreIntegratedLutFormat(), false, true)
            {
                name = "Burt Subsurface PreIntegrated LUT Array Fallback",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            };

            var pixel = new[] { Color.black };
            for (var profileIndex = 0; profileIndex < ProfileSlotCount; profileIndex++)
            {
                fallbackPreIntegratedLut.SetPixels(pixel, profileIndex);
            }

            fallbackPreIntegratedLut.Apply(false, true);
            return fallbackPreIntegratedLut;
        }

        public static Texture GetOrCreateSHLut()
        {
            return GetOrCreateSHLut(BurtSubsurfaceProfilePalette.Resolve(BurtSubsurfaceProfileSettings.Default, null));
        }

        public static Texture GetOrCreateSHLut(BurtSubsurfaceProfilePalette palette)
        {
            if (gpuSHLut != null && gpuSHLut.IsCreated())
            {
                if (ShouldDeferTextureRebuild())
                {
                    return gpuSHLut;
                }

                var cachedHash = GetCachedPaletteHash(palette);
                if (shLutHash == cachedHash)
                {
                    return gpuSHLut;
                }
            }
            else if (shLut != null)
            {
                if (ShouldDeferTextureRebuild())
                {
                    return shLut;
                }

                var cachedHash = GetCachedPaletteHash(palette);
                if (shLutHash == cachedHash)
                {
                    return shLut;
                }
            }
            else if (ShouldDeferTextureRebuild())
            {
                return null;
            }

            var currentHash = GetCachedPaletteHash(palette);
            if (TryUpdateGpuLutSlices(palette, currentHash, false))
            {
                return gpuSHLut;
            }

            DestroyRenderTexture(gpuSHLut);
            gpuSHLut = null;
            EnsureSHLutTexture();
            if (UpdateSHLutSlices(palette))
            {
                shLut.Apply(false, false);
            }

            shLutHash = currentHash;
            return shLut;
        }

        public static Texture GetFallbackSHLut()
        {
            if (fallbackSHLut != null)
            {
                return fallbackSHLut;
            }

            fallbackSHLut = new Texture2DArray(1, SHLutHeight, ProfileSlotCount, SelectPreIntegratedLutFormat(), false, true)
            {
                name = "Burt Subsurface SH LUT Array Fallback",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            };

            var pixels = new[]
            {
                new Color(1f, 1f, 1f, 0f),
                new Color(1f, 1f, 1f, 0f),
                new Color(1f, 1f, 1f, 0f)
            };
            for (var profileIndex = 0; profileIndex < ProfileSlotCount; profileIndex++)
            {
                fallbackSHLut.SetPixels(pixels, profileIndex);
            }

            fallbackSHLut.Apply(false, true);
            return fallbackSHLut;
        }

        public static Texture GetOrCreateProfileParamLut()
        {
            return GetOrCreateProfileParamLut(BurtSubsurfaceProfilePalette.Resolve(BurtSubsurfaceProfileSettings.Default, null));
        }

        public static Texture2D GetFallbackProfileParamLut()
        {
            if (fallbackProfileParamLut != null)
            {
                return fallbackProfileParamLut;
            }

            fallbackProfileParamLut = new Texture2D(1, 1, SelectProfileParamLutFormat(), false, true)
            {
                name = "Burt Subsurface Profile Param LUT Fallback",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                anisoLevel = 0
            };

            fallbackProfileParamLut.SetPixel(0, 0, Color.black);
            fallbackProfileParamLut.Apply(false, true);
            return fallbackProfileParamLut;
        }

        public static Texture GetOrCreateProfileParamLut(BurtSubsurfaceProfilePalette palette)
        {
            if (gpuProfileParamLut != null && gpuProfileParamLut.IsCreated())
            {
                if (ShouldDeferTextureRebuild())
                {
                    return gpuProfileParamLut;
                }

                var cachedHash = GetCachedProfileParamPaletteHash(palette);
                if (profileParamLutHash == cachedHash)
                {
                    return gpuProfileParamLut;
                }
            }
            else if (profileParamLut != null)
            {
                if (ShouldDeferTextureRebuild())
                {
                    return profileParamLut;
                }

                var cachedHash = GetCachedProfileParamPaletteHash(palette);
                if (profileParamLutHash == cachedHash)
                {
                    return TryUploadProfileParamLutToGpu(false) ? gpuProfileParamLut : profileParamLut;
                }
            }
            else if (ShouldDeferTextureRebuild())
            {
                return null;
            }

            var currentHash = GetCachedProfileParamPaletteHash(palette);
            if (TryUpdateGpuProfileParamLut(palette, currentHash))
            {
                return gpuProfileParamLut;
            }

            EnsureProfileParamLutTexture();
            var changed = UpdateProfileParamLutRows(palette);
            if (changed)
            {
                profileParamLut.Apply(false, false);
            }

            if (TryUploadProfileParamLutToGpu(changed))
            {
                profileParamLutHash = currentHash;
                return gpuProfileParamLut;
            }

            DestroyRenderTexture(gpuProfileParamLut);
            gpuProfileParamLut = null;
            profileParamLutHash = currentHash;
            return profileParamLut;
        }

        public static void InvalidateCachedTextures()
        {
            DestroyTexture(preIntegratedLut);
            preIntegratedLut = null;
            DestroyRenderTexture(gpuPreIntegratedLut);
            gpuPreIntegratedLut = null;
            preIntegratedLutHash = 0;
            preIntegratedProfileHashesValid = false;
            DestroyTexture(shLut);
            shLut = null;
            DestroyRenderTexture(gpuSHLut);
            gpuSHLut = null;
            shLutHash = 0;
            shProfileHashesValid = false;
            DestroyTexture(profileParamLut);
            profileParamLut = null;
            DestroyRenderTexture(gpuProfileParamLut);
            gpuProfileParamLut = null;
            gpuProfileParamLutRandomWrite = false;
            profileParamLutHash = 0;
            profileParamRowHashesValid = false;
            ReleaseProfileParamSettingsBuffer();
            hasLastPaletteHashes = false;
        }

        public static void RequestEditorTextureRebuild()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                return;
            }

            MarkEditorInteraction();
            RegisterEditorTextureRebuild();
#endif
        }

        public static void MarkEditorInteraction()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                lastInteractiveTime = EditorApplication.timeSinceStartup;
            }
#endif
        }

        public static Vector4 ProfileParamLutSizeVector => new Vector4(
            ProfileParamLutWidth,
            ProfileParamLutHeight,
            1f / ProfileParamLutWidth,
            1f / ProfileParamLutHeight);

        public static Color EvaluatePreIntegratedLutSample(
            float rawNoL,
            float curvature,
            Vector3 surfaceAlbedo,
            Vector3 diffuseMeanFreePathMm)
        {
            var burleyDiffuse = EvaluateBurleyRingIntegral(
                rawNoL,
                curvature,
                surfaceAlbedo,
                diffuseMeanFreePathMm);

            return new Color(
                Mathf.Max(0f, burleyDiffuse.x),
                Mathf.Max(0f, burleyDiffuse.y),
                Mathf.Max(0f, burleyDiffuse.z),
                0f);
        }

        public static Vector3 GetSurfaceAlbedoForLut(BurtSubsurfaceProfileSettings settings)
        {
            return Clamp(ToRgb(settings.SurfaceAlbedo), SurfaceAlbedoBias, 1f);
        }

        public static Vector3 GetEffectiveDiffuseMeanFreePathForLut(BurtSubsurfaceProfileSettings settings)
        {
            var surfaceAlbedo = GetSurfaceAlbedoForLut(settings);
            var meanFreePathColor = Clamp(ToRgb(settings.MeanFreePathColor), MeanFreePathBias, 1f);
            var meanFreePath = Multiply(meanFreePathColor, settings.MeanFreePathDistance);
            var diffuseMeanFreePath = GetDiffuseMeanFreePathFromMeanFreePath(surfaceAlbedo, meanFreePath);
            var diffuseMeanFreePathInMillimeters = Clamp(
                Multiply(diffuseMeanFreePath, CentimetersToMillimeters / DiffuseMeanFreePathToMeanFreePathMagicNumber),
                0f,
                MaxDiffuseMeanFreePathInMillimeters);
            return Max(Multiply(diffuseMeanFreePathInMillimeters, settings.WorldUnitScale * MillimetersToCentimeters), 0.01f);
        }

        public static float GetMeanFreePathScreenScale(BurtSubsurfaceProfileSettings settings)
        {
            var effectiveDiffuseMeanFreePath = GetEffectiveDiffuseMeanFreePathForLut(settings);
            var dominantEffectiveDistance = Mathf.Max(
                effectiveDiffuseMeanFreePath.x,
                Mathf.Max(effectiveDiffuseMeanFreePath.y, effectiveDiffuseMeanFreePath.z));
            return Mathf.Clamp(dominantEffectiveDistance * 0.5f, 0.05f, 4f);
        }

        private static Vector3 ToRgb(Color color)
        {
            return new Vector3(
                Mathf.Clamp01(color.r),
                Mathf.Clamp01(color.g),
                Mathf.Clamp01(color.b));
        }

        private static Vector3 GetDiffuseMeanFreePathFromMeanFreePath(Vector3 surfaceAlbedo, Vector3 meanFreePath)
        {
            var searchLightScale = GetSearchLightDiffuseScalingFactor(surfaceAlbedo);
            var perpendicularScale = GetPerpendicularScalingFactor(surfaceAlbedo);
            return Multiply(meanFreePath, Divide(searchLightScale, perpendicularScale));
        }

        private static Vector3 GetMeanFreePathFromDiffuseMeanFreePath(Vector3 surfaceAlbedo, Vector3 diffuseMeanFreePath)
        {
            var searchLightScale = GetSearchLightDiffuseScalingFactor(surfaceAlbedo);
            var perpendicularScale = GetPerpendicularScalingFactor(surfaceAlbedo);
            return Multiply(diffuseMeanFreePath, Divide(perpendicularScale, searchLightScale));
        }

        private static void EnsurePreIntegratedLutTexture()
        {
            if (preIntegratedLut != null)
            {
                return;
            }

            preIntegratedLut = new Texture2DArray(LutSize, LutSize, ProfileSlotCount, SelectPreIntegratedLutFormat(), false, true)
            {
                name = "Burt Subsurface PreIntegrated LUT Array",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            };

            preIntegratedProfileHashesValid = false;
        }

        private static void EnsureGpuPreIntegratedLutTexture()
        {
            if (gpuPreIntegratedLut != null && gpuPreIntegratedLut.IsCreated())
            {
                return;
            }

            DestroyRenderTexture(gpuPreIntegratedLut);
            var descriptor = new RenderTextureDescriptor(LutSize, LutSize)
            {
                graphicsFormat = SelectGpuLutFormat(),
                depthBufferBits = 0,
                msaaSamples = 1,
                dimension = TextureDimension.Tex2DArray,
                volumeDepth = ProfileSlotCount,
                mipCount = 1,
                useMipMap = false,
                autoGenerateMips = false,
                enableRandomWrite = true,
                sRGB = false
            };
            gpuPreIntegratedLut = new RenderTexture(descriptor)
            {
                name = "Burt Subsurface PreIntegrated LUT Array GPU",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            };
            gpuPreIntegratedLut.Create();
            preIntegratedProfileHashesValid = false;
        }

        private static void EnsureSHLutTexture()
        {
            if (shLut != null)
            {
                return;
            }

            shLut = new Texture2DArray(SHLutWidth, SHLutHeight, ProfileSlotCount, SelectPreIntegratedLutFormat(), false, true)
            {
                name = "Burt Subsurface SH LUT Array",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            };

            shProfileHashesValid = false;
        }

        private static void EnsureGpuSHLutTexture()
        {
            if (gpuSHLut != null && gpuSHLut.IsCreated())
            {
                return;
            }

            DestroyRenderTexture(gpuSHLut);
            var descriptor = new RenderTextureDescriptor(SHLutWidth, SHLutHeight)
            {
                graphicsFormat = SelectGpuLutFormat(),
                depthBufferBits = 0,
                msaaSamples = 1,
                dimension = TextureDimension.Tex2DArray,
                volumeDepth = ProfileSlotCount,
                mipCount = 1,
                useMipMap = false,
                autoGenerateMips = false,
                enableRandomWrite = true,
                sRGB = false
            };
            gpuSHLut = new RenderTexture(descriptor)
            {
                name = "Burt Subsurface SH LUT Array GPU",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            };
            gpuSHLut.Create();
            shProfileHashesValid = false;
        }

        private static void EnsureProfileParamLutTexture()
        {
            if (profileParamLut != null)
            {
                return;
            }

            profileParamLut = new Texture2D(ProfileParamLutWidth, ProfileParamLutHeight, SelectProfileParamLutFormat(), false, true)
            {
                name = "Burt Subsurface Profile Param LUT",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                anisoLevel = 0
            };

            profileParamRowHashesValid = false;
        }

        private static bool TryUploadProfileParamLutToGpu(bool sourceChanged)
        {
            if (!CanUseGpuProfileParamLut())
            {
                return false;
            }

            var gpuTextureRecreated = EnsureGpuProfileParamLutTexture(false);
            if (!sourceChanged && !gpuTextureRecreated)
            {
                return gpuProfileParamLut != null && gpuProfileParamLut.IsCreated();
            }

            if (profileParamLut == null || gpuProfileParamLut == null || !gpuProfileParamLut.IsCreated())
            {
                return false;
            }

            Graphics.CopyTexture(profileParamLut, 0, 0, gpuProfileParamLut, 0, 0);
            return true;
        }

        private static bool EnsureGpuProfileParamLutTexture(bool randomWrite)
        {
            if (gpuProfileParamLut != null &&
                gpuProfileParamLut.IsCreated() &&
                gpuProfileParamLutRandomWrite == randomWrite)
            {
                return false;
            }

            DestroyRenderTexture(gpuProfileParamLut);
            var descriptor = new RenderTextureDescriptor(ProfileParamLutWidth, ProfileParamLutHeight)
            {
                graphicsFormat = SelectGpuProfileParamLutFormat(),
                depthBufferBits = 0,
                msaaSamples = 1,
                dimension = TextureDimension.Tex2D,
                volumeDepth = 1,
                mipCount = 1,
                useMipMap = false,
                autoGenerateMips = false,
                enableRandomWrite = randomWrite,
                sRGB = false
            };
            gpuProfileParamLut = new RenderTexture(descriptor)
            {
                name = "Burt Subsurface Profile Param LUT GPU",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                anisoLevel = 0
            };
            gpuProfileParamLut.Create();
            gpuProfileParamLutRandomWrite = randomWrite;
            return true;
        }

        private static bool TryUpdateGpuProfileParamLut(BurtSubsurfaceProfilePalette palette, int currentHash)
        {
            if (!CanUseGpuLutBaker() || !CanUseGpuProfileParamLutForRandomWrite())
            {
                return false;
            }

            var shader = GetLutBakerShader();
            if (shader == null || profileParamKernel < 0)
            {
                return false;
            }

            EnsureGpuProfileParamLutTexture(true);
            EnsureProfileParamSettingsBuffer();
            UpdateProfileParamSettingsBuffer(palette);

            var cmd = CommandBufferPool.Get("Burt Subsurface GPU Profile Param LUT");
            cmd.SetComputeBufferParam(shader, profileParamKernel, BakerProfileParamSettingsId, profileParamSettingsBuffer);
            cmd.SetComputeIntParam(shader, BakerProfileParamSettingsStrideId, ProfileParamSettingsVectorCount);
            cmd.SetComputeVectorParam(shader, ProfileParamLutSizeId, ProfileParamLutSizeVector);
            cmd.SetComputeTextureParam(shader, profileParamKernel, BakerProfileParamLutRWId, gpuProfileParamLut);
            cmd.DispatchCompute(
                shader,
                profileParamKernel,
                Mathf.CeilToInt(ProfileParamLutWidth / 8f),
                Mathf.CeilToInt(ProfileParamLutHeight / 8f),
                1);
            Graphics.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            DestroyTexture(profileParamLut);
            profileParamLut = null;
            profileParamLutHash = currentHash;
            profileParamRowHashesValid = false;
            return true;
        }

        private static void EnsureProfileParamSettingsBuffer()
        {
            var count = ProfileSlotCount * ProfileParamSettingsVectorCount;
            if (profileParamSettingsBuffer != null && profileParamSettingsBuffer.count == count && profileParamSettingsBuffer.stride == 16)
            {
                return;
            }

            ReleaseProfileParamSettingsBuffer();
            profileParamSettingsBuffer = new ComputeBuffer(count, 16, ComputeBufferType.Structured)
            {
                name = "Burt Subsurface Profile Param Settings"
            };
        }

        private static void UpdateProfileParamSettingsBuffer(BurtSubsurfaceProfilePalette palette)
        {
            var count = Mathf.Clamp(palette.Count, 1, ProfileSlotCount);
            var fallback = palette.GetSettings(0);
            var vectorCount = ProfileSlotCount * ProfileParamSettingsVectorCount;
            if (profileParamSettingsBufferData == null || profileParamSettingsBufferData.Length != vectorCount)
            {
                profileParamSettingsBufferData = new Vector4[vectorCount];
            }

            for (var profileIndex = 0; profileIndex < ProfileSlotCount; profileIndex++)
            {
                var settings = profileIndex < count ? palette.GetSettings(profileIndex) : fallback;
                var offset = profileIndex * ProfileParamSettingsVectorCount;
                var surfaceAlbedo = GetSurfaceAlbedoForLut(settings);
                var meanFreePathColor = Clamp(ToRgb(settings.MeanFreePathColor), MeanFreePathBias, 1f);
                profileParamSettingsBufferData[offset] = new Vector4(surfaceAlbedo.x, surfaceAlbedo.y, surfaceAlbedo.z, settings.MeanFreePathDistance);
                profileParamSettingsBufferData[offset + 1] = new Vector4(meanFreePathColor.x, meanFreePathColor.y, meanFreePathColor.z, settings.WorldUnitScale);
                profileParamSettingsBufferData[offset + 2] = new Vector4(
                    Mathf.Clamp01(settings.Tint.r),
                    Mathf.Clamp01(settings.Tint.g),
                    Mathf.Clamp01(settings.Tint.b),
                    0f);
                profileParamSettingsBufferData[offset + 3] = new Vector4(
                    settings.BoundaryColorBleed.r,
                    settings.BoundaryColorBleed.g,
                    settings.BoundaryColorBleed.b,
                    0f);
                profileParamSettingsBufferData[offset + 4] = settings.DualSpecularVector;
                profileParamSettingsBufferData[offset + 5] = settings.TransmissionVector;
                profileParamSettingsBufferData[offset + 6] = new Vector4(
                    Mathf.Clamp01(settings.TransmissionTintColor.r),
                    Mathf.Clamp01(settings.TransmissionTintColor.g),
                    Mathf.Clamp01(settings.TransmissionTintColor.b),
                    settings.ExtinctionScale);
            }

            profileParamSettingsBuffer.SetData(profileParamSettingsBufferData);
        }

        private static bool UpdatePreIntegratedLutSlices(BurtSubsurfaceProfilePalette palette)
        {
            var changed = false;
            var count = Mathf.Clamp(palette.Count, 1, ProfileSlotCount);
            var fallback = palette.GetSettings(0);
            EnsurePreIntegratedSlicePixels();
            for (var profileIndex = 0; profileIndex < ProfileSlotCount; profileIndex++)
            {
                var settings = profileIndex < count ? palette.GetSettings(profileIndex) : fallback;
                var profileHash = ComputePreIntegratedProfileHash(settings);
                if (preIntegratedProfileHashesValid && preIntegratedProfileHashes[profileIndex] == profileHash)
                {
                    continue;
                }

                FillPreIntegratedSlice(preIntegratedSlicePixels, settings);
                preIntegratedLut.SetPixels(preIntegratedSlicePixels, profileIndex);
                preIntegratedProfileHashes[profileIndex] = profileHash;
                changed = true;
            }

            preIntegratedProfileHashesValid = true;
            return changed;
        }

        private static bool UpdateSHLutSlices(BurtSubsurfaceProfilePalette palette)
        {
            var changed = false;
            var count = Mathf.Clamp(palette.Count, 1, ProfileSlotCount);
            var fallback = palette.GetSettings(0);
            EnsureSHSlicePixels();
            for (var profileIndex = 0; profileIndex < ProfileSlotCount; profileIndex++)
            {
                var settings = profileIndex < count ? palette.GetSettings(profileIndex) : fallback;
                var profileHash = ComputePreIntegratedProfileHash(settings);
                if (shProfileHashesValid && shProfileHashes[profileIndex] == profileHash)
                {
                    continue;
                }

                FillSHSlice(shSlicePixels, settings);
                shLut.SetPixels(shSlicePixels, profileIndex);
                shProfileHashes[profileIndex] = profileHash;
                changed = true;
            }

            shProfileHashesValid = true;
            return changed;
        }

        private static bool TryUpdateGpuLutSlices(BurtSubsurfaceProfilePalette palette, int currentHash, bool preIntegrated)
        {
            if (!CanUseGpuLutBaker())
            {
                return false;
            }

            var shader = GetLutBakerShader();
            if (shader == null)
            {
                return false;
            }

            var kernel = preIntegrated ? preIntegratedKernel : shKernel;
            if (kernel < 0)
            {
                return false;
            }

            var profileParamTexture = GetOrCreateProfileParamLut(palette);
            if (profileParamTexture == null)
            {
                return false;
            }

            if (preIntegrated)
            {
                EnsureGpuPreIntegratedLutTexture();
            }
            else
            {
                EnsureGpuSHLutTexture();
            }

            var count = Mathf.Clamp(palette.Count, 1, ProfileSlotCount);
            var fallback = palette.GetSettings(0);
            var changed = false;
            var hashes = preIntegrated ? preIntegratedProfileHashes : shProfileHashes;
            var hashesValid = preIntegrated ? preIntegratedProfileHashesValid : shProfileHashesValid;
            var commandName = preIntegrated ? "Burt Subsurface GPU PreIntegrated LUT" : "Burt Subsurface GPU SH LUT";
            var cmd = CommandBufferPool.Get(commandName);
            for (var profileIndex = 0; profileIndex < ProfileSlotCount; profileIndex++)
            {
                var settings = profileIndex < count ? palette.GetSettings(profileIndex) : fallback;
                var profileHash = ComputePreIntegratedProfileHash(settings);
                if (hashesValid && hashes[profileIndex] == profileHash)
                {
                    continue;
                }

                DispatchGpuLutSlice(cmd, shader, kernel, profileParamTexture, profileIndex, preIntegrated);
                hashes[profileIndex] = profileHash;
                changed = true;
            }

            if (changed)
            {
                Graphics.ExecuteCommandBuffer(cmd);
            }

            CommandBufferPool.Release(cmd);
            if (preIntegrated)
            {
                preIntegratedProfileHashesValid = true;
                preIntegratedLutHash = currentHash;
            }
            else
            {
                shProfileHashesValid = true;
                shLutHash = currentHash;
            }

            return true;
        }

        private static void DispatchGpuLutSlice(CommandBuffer cmd, ComputeShader shader, int kernel, Texture profileParamTexture, int profileIndex, bool preIntegrated)
        {
            cmd.SetComputeTextureParam(shader, kernel, ProfileParamLutId, profileParamTexture);
            cmd.SetComputeVectorParam(shader, ProfileParamLutSizeId, ProfileParamLutSizeVector);
            cmd.SetComputeIntParam(shader, BakerProfileIndexId, profileIndex);
            if (preIntegrated)
            {
                cmd.SetComputeTextureParam(shader, kernel, BakerPreIntegratedLutRWId, gpuPreIntegratedLut);
                cmd.SetComputeVectorParam(shader, BakerPreIntegratedLutSizeId, new Vector4(LutSize, LutSize, 1f / LutSize, 1f / LutSize));
                cmd.DispatchCompute(shader, kernel, Mathf.CeilToInt(LutSize / 8f), Mathf.CeilToInt(LutSize / 8f), 1);
            }
            else
            {
                cmd.SetComputeTextureParam(shader, kernel, BakerSHLutRWId, gpuSHLut);
                cmd.SetComputeVectorParam(shader, BakerSHLutSizeId, new Vector4(SHLutWidth, SHLutHeight, 1f / SHLutWidth, 1f / SHLutHeight));
                cmd.DispatchCompute(shader, kernel, Mathf.CeilToInt(SHLutWidth / 8f), Mathf.CeilToInt(SHLutHeight / 8f), 1);
            }
        }

        private static bool UpdateProfileParamLutRows(BurtSubsurfaceProfilePalette palette)
        {
            var changed = false;
            var count = Mathf.Clamp(palette.Count, 1, ProfileSlotCount);
            var fallback = palette.GetSettings(0);
            EnsureProfileParamRowPixels();
            for (var profileIndex = 0; profileIndex < ProfileSlotCount; profileIndex++)
            {
                var settings = profileIndex < count ? palette.GetSettings(profileIndex) : fallback;
                var rowHash = ComputeProfileParamRowHash(settings);
                if (profileParamRowHashesValid && profileParamRowHashes[profileIndex] == rowHash)
                {
                    continue;
                }

                FillProfileParamRow(profileParamRowPixels, 0, settings);
                profileParamLut.SetPixels(0, profileIndex, ProfileParamLutWidth, 1, profileParamRowPixels);
                profileParamRowHashes[profileIndex] = rowHash;
                changed = true;
            }

            profileParamRowHashesValid = true;
            return changed;
        }

        private static void FillPreIntegratedSlice(Color[] pixels, BurtSubsurfaceProfileSettings settings)
        {
            var surfaceAlbedo = GetSurfaceAlbedoForLut(settings);
            var diffuseMeanFreePathMm = GetEffectiveDiffuseMeanFreePathForLut(settings);
            for (var y = 0; y < LutSize; y++)
            {
                var curvature = (y + 0.5f) / LutSize;
                for (var x = 0; x < LutSize; x++)
                {
                    var rawNoL = ((x + 0.5f) / LutSize) * 2f - 1f;
                    pixels[y * LutSize + x] = EvaluatePreIntegratedLutSample(rawNoL, curvature, surfaceAlbedo, diffuseMeanFreePathMm);
                }
            }
        }

        private static void EnsurePreIntegratedSlicePixels()
        {
            var pixelCount = LutSize * LutSize;
            if (preIntegratedSlicePixels == null || preIntegratedSlicePixels.Length != pixelCount)
            {
                preIntegratedSlicePixels = new Color[pixelCount];
            }
        }

        private static void FillSHSlice(Color[] pixels, BurtSubsurfaceProfileSettings settings)
        {
            var surfaceAlbedo = GetSurfaceAlbedoForLut(settings);
            var diffuseMeanFreePathMm = GetEffectiveDiffuseMeanFreePathForLut(settings);
            for (var y = 0; y < SHLutHeight; y++)
            {
                var shBand = ResolveSHBandForRow(y);
                for (var x = 0; x < SHLutWidth; x++)
                {
                    var curvature = (x + 0.5f) / SHLutWidth;
                    pixels[y * SHLutWidth + x] = ToColor(EvaluateSHLutSample(curvature, shBand, surfaceAlbedo, diffuseMeanFreePathMm), 0f);
                }
            }
        }

        private static void EnsureSHSlicePixels()
        {
            var pixelCount = SHLutWidth * SHLutHeight;
            if (shSlicePixels == null || shSlicePixels.Length != pixelCount)
            {
                shSlicePixels = new Color[pixelCount];
            }
        }

        private static void EnsureProfileParamRowPixels()
        {
            if (profileParamRowPixels == null || profileParamRowPixels.Length != ProfileParamLutWidth)
            {
                profileParamRowPixels = new Color[ProfileParamLutWidth];
            }
        }

        private static void FillProfileParamRow(Color[] pixels, int profileIndex, BurtSubsurfaceProfileSettings settings)
        {
            var rowOffset = profileIndex * ProfileParamLutWidth;
            var surfaceAlbedo = GetSurfaceAlbedoForLut(settings);
            var diffuseMeanFreePath = GetDiffuseMeanFreePathInMillimetersForProfileParam(settings, surfaceAlbedo);
            ResolveProfileSamplingComponentsForParamLut(surfaceAlbedo, diffuseMeanFreePath, out var clampedDiffuseMeanFreePath, out var samplingAlbedo, out var samplingDiffuseMeanFreePath);

            pixels[rowOffset + ProfileParamSurfaceAlbedoOffset] = ToColor(surfaceAlbedo, samplingAlbedo);
            pixels[rowOffset + ProfileParamMeanFreePathOffset] = ToColor(
                Multiply(clampedDiffuseMeanFreePath, EncodeDiffuseMeanFreePathInMillimetersToUnit),
                samplingDiffuseMeanFreePath * EncodeDiffuseMeanFreePathInMillimetersToUnit);
            pixels[rowOffset + ProfileParamTintOffset] = new Color(Mathf.Clamp01(settings.Tint.r), Mathf.Clamp01(settings.Tint.g), Mathf.Clamp01(settings.Tint.b), settings.WorldUnitScale * EncodeWorldUnitScaleInCentimetersToUnit);
            pixels[rowOffset + ProfileParamBoundaryColorBleedOffset] = new Color(settings.BoundaryColorBleed.r, settings.BoundaryColorBleed.g, settings.BoundaryColorBleed.b, 0f);
            pixels[rowOffset + ProfileParamDualSpecularOffset] = ToColor(settings.DualSpecularVector);
            pixels[rowOffset + ProfileParamTransmissionParamsOffset] = ToColor(settings.TransmissionVector);

            var worldUnitToMillimeters = 1f / Mathf.Max(settings.WorldUnitScale * CentimetersToMillimeters, 0.0001f);
            var scalingFactor = GetSearchLightDiffuseScalingFactor(surfaceAlbedo);
            for (var i = 0; i < ProfileParamTransmissionProfileSize; i++)
            {
                var normalizedDistance = i / (float)ProfileParamTransmissionProfileSize;
                var distanceInMillimeters = normalizedDistance * MaxTransmissionProfileDistance * worldUnitToMillimeters;
                var radiusOffsetInMillimeters = 0.06f * worldUnitToMillimeters;
                var transmission = EvaluateTransmissionProfile(
                    distanceInMillimeters + radiusOffsetInMillimeters,
                    surfaceAlbedo,
                    scalingFactor,
                    clampedDiffuseMeanFreePath,
                    settings.TransmissionTintColor);
                pixels[rowOffset + ProfileParamTransmissionProfileOffset + i] = ToColor(transmission, Mathf.Exp(-distanceInMillimeters * settings.ExtinctionScale));
            }

            var scatterRadius = Mathf.Max(samplingDiffuseMeanFreePath * MillimetersToCentimeters, 0.1f);
            FillKernel(pixels, rowOffset + ProfileParamKernel0Offset, ProfileParamKernel0Size, settings, surfaceAlbedo, clampedDiffuseMeanFreePath, scatterRadius);
            FillKernel(pixels, rowOffset + ProfileParamKernel1Offset, ProfileParamKernel1Size, settings, surfaceAlbedo, clampedDiffuseMeanFreePath, scatterRadius);
            FillKernel(pixels, rowOffset + ProfileParamKernel2Offset, ProfileParamKernel2Size, settings, surfaceAlbedo, clampedDiffuseMeanFreePath, scatterRadius);
        }

        private static Vector3 GetDiffuseMeanFreePathInMillimetersForProfileParam(BurtSubsurfaceProfileSettings settings, Vector3 surfaceAlbedo)
        {
            var meanFreePathColor = Clamp(ToRgb(settings.MeanFreePathColor), MeanFreePathBias, 1f);
            var meanFreePath = Multiply(meanFreePathColor, settings.MeanFreePathDistance);
            return Multiply(GetDiffuseMeanFreePathFromMeanFreePath(surfaceAlbedo, meanFreePath), CentimetersToMillimeters / DiffuseMeanFreePathToMeanFreePathMagicNumber);
        }

        private static void ResolveProfileSamplingComponentsForParamLut(Vector3 surfaceAlbedo, Vector3 diffuseMeanFreePath, out Vector3 clampedDiffuseMeanFreePath, out float samplingAlbedo, out float samplingDiffuseMeanFreePath)
        {
            var meanFreePath = GetMeanFreePathFromDiffuseMeanFreePath(surfaceAlbedo, diffuseMeanFreePath);
            var maxComponent = Mathf.Max(meanFreePath.x, Mathf.Max(meanFreePath.y, meanFreePath.z));
            clampedDiffuseMeanFreePath = Clamp(diffuseMeanFreePath, 0f, MaxDiffuseMeanFreePathInMillimeters);

            if (Mathf.Abs(meanFreePath.x - maxComponent) < float.Epsilon)
            {
                samplingAlbedo = surfaceAlbedo.x;
                samplingDiffuseMeanFreePath = Mathf.Clamp(diffuseMeanFreePath.x, 0f, MaxDiffuseMeanFreePathInMillimeters);
                return;
            }

            if (Mathf.Abs(meanFreePath.y - maxComponent) < float.Epsilon)
            {
                samplingAlbedo = surfaceAlbedo.y;
                samplingDiffuseMeanFreePath = Mathf.Clamp(diffuseMeanFreePath.y, 0f, MaxDiffuseMeanFreePathInMillimeters);
                return;
            }

            samplingAlbedo = surfaceAlbedo.z;
            samplingDiffuseMeanFreePath = Mathf.Clamp(diffuseMeanFreePath.z, 0f, MaxDiffuseMeanFreePathInMillimeters);
        }

        private static void FillKernel(
            Color[] pixels,
            int startIndex,
            int kernelSize,
            BurtSubsurfaceProfileSettings settings,
            Vector3 surfaceAlbedo,
            Vector3 diffuseMeanFreePath,
            float scatterRadius)
        {
            var kernel = ComputeMirroredKernel(kernelSize, surfaceAlbedo, diffuseMeanFreePath, scatterRadius);
            RemapKernelValue(scatterRadius * settings.WorldUnitScale * CentimetersToMillimeters, kernel);
            for (var i = 0; i < kernelSize; i++)
            {
                pixels[startIndex + i] = kernel[i];
            }
        }

        private static Color[] ComputeMirroredKernel(int kernelSize, Vector3 surfaceAlbedo, Vector3 diffuseMeanFreePath, float scatterRadius)
        {
            var singleEdgeSamples = Mathf.Max(1, kernelSize);
            var totalSamples = singleEdgeSamples * 2 - 1;
            var kernel = new Color[totalSamples];
            var scalingFactor = GetSearchLightDiffuseScalingFactor(surfaceAlbedo);
            var range = totalSamples > 20 ? 3f : 2f;
            var step = 2f * range / Mathf.Max(totalSamples - 1, 1);

            for (var i = 0; i < totalSamples; i++)
            {
                var offset = -range + i * step;
                var sign = offset < 0f ? -1f : 1f;
                var remapped = range * sign * Mathf.Abs(Mathf.Pow(offset, 2f)) / Mathf.Pow(range, 2f);
                kernel[i] = new Color(0f, 0f, 0f, remapped);
            }

            kernel[totalSamples / 2] = Color.clear;

            var spaceScale = scatterRadius * CentimetersToMillimeters;
            for (var i = 0; i < totalSamples; i++)
            {
                var previousWidth = i > 0 ? Mathf.Abs(kernel[i].a - kernel[i - 1].a) : 0f;
                var nextWidth = i < totalSamples - 1 ? Mathf.Abs(kernel[i].a - kernel[i + 1].a) : 0f;
                var area = (previousWidth + nextWidth) * 0.5f;
                var weight = Multiply(EvaluateBurleyScatteringProfile(Mathf.Abs(kernel[i].a) * spaceScale, surfaceAlbedo, scalingFactor, diffuseMeanFreePath), area);
                kernel[i] = ToColor(weight, kernel[i].a * 2f);
            }

            var center = kernel[totalSamples / 2];
            for (var i = totalSamples / 2; i > 0; i--)
            {
                kernel[i] = kernel[i - 1];
            }

            kernel[0] = center;

            var sum = Vector3.zero;
            for (var i = 0; i < totalSamples; i++)
            {
                sum += ToRgb(kernel[i]);
            }

            for (var i = 0; i < totalSamples; i++)
            {
                var weight = Divide(ToRgb(kernel[i]), Max(sum, 0.0001f));
                kernel[i] = ToColor(weight, kernel[i].a);
            }

            var result = new Color[singleEdgeSamples];
            result[0] = kernel[0];
            for (var i = 1; i < singleEdgeSamples; i++)
            {
                result[i] = kernel[i + singleEdgeSamples - 1];
            }

            return result;
        }

        private static void RemapKernelValue(float scatterRadius, Color[] kernel)
        {
            var alphaScale = scatterRadius / (3f * SubsurfaceRadiusScale);
            for (var i = 0; i < kernel.Length; i++)
            {
                kernel[i] = new Color(kernel[i].r, kernel[i].g, kernel[i].b, kernel[i].a * alphaScale);
            }
        }

        private static Vector3 EvaluateTransmissionProfile(
            float radiusInMillimeters,
            Vector3 surfaceAlbedo,
            Vector3 scalingFactor,
            Vector3 diffuseMeanFreePath,
            Color transmissionTint)
        {
            return Multiply(
                new Vector3(
                    EvaluateBurleyTransmissionProfile(radiusInMillimeters, surfaceAlbedo.x, scalingFactor.x, diffuseMeanFreePath.x),
                    EvaluateBurleyTransmissionProfile(radiusInMillimeters, surfaceAlbedo.y, scalingFactor.y, diffuseMeanFreePath.y),
                    EvaluateBurleyTransmissionProfile(radiusInMillimeters, surfaceAlbedo.z, scalingFactor.z, diffuseMeanFreePath.z)),
                ToRgb(transmissionTint));
        }

        private static float EvaluateBurleyTransmissionProfile(float radius, float surfaceAlbedo, float scalingFactor, float diffuseMeanFreePath)
        {
            var safeDistance = Mathf.Max(diffuseMeanFreePath, 0.01f);
            return 0.25f * surfaceAlbedo * (Mathf.Exp(-scalingFactor * radius / safeDistance) + 3f * Mathf.Exp(-scalingFactor * radius / (3f * safeDistance)));
        }

        private static int ResolveSHBandForRow(int row)
        {
            if (row <= 0)
            {
                return 0;
            }

            return row == 1 ? 1 : 2;
        }

        private static Vector3 EvaluateSHLutSample(
            float curvature,
            int shBand,
            Vector3 surfaceAlbedo,
            Vector3 diffuseMeanFreePathMm)
        {
            var integral = Vector3.zero;
            var count = 0;
            for (var theta = 0f; theta < HalfPi; theta += SHIntegrationStep)
            {
                var noL = Mathf.Clamp01(Mathf.Cos(theta));
                var sinTheta = Mathf.Sqrt(Mathf.Max(0f, 1f - noL * noL));
                var skinNoL = EvaluateBurleyRingIntegral(noL, Mathf.Max(curvature, 0.0001f), surfaceAlbedo, diffuseMeanFreePathMm);
                integral += Multiply(skinNoL, EvaluateSHBasisForBand(shBand, noL) * sinTheta);
                count++;
            }

            var averageIntegral = Divide(integral, Mathf.Max(count, 1));
            return Multiply(averageIntegral, GetSHBandScale(shBand));
        }

        private static float EvaluateSHBasisForBand(int shBand, float z)
        {
            switch (shBand)
            {
                case 0:
                    return 0.282094791774f;
                case 1:
                    return 0.488602511903f * z;
                default:
                    return 0.315391565253f * (3f * z * z - 1f);
            }
        }

        private static float GetSHBandScale(int shBand)
        {
            switch (shBand)
            {
                case 0:
                    return Mathf.Sqrt(FourPi) * TwoPi * InvHalfPi;
                case 1:
                    return Mathf.Sqrt(FourPi / 3f) * TwoPi * InvHalfPi;
                default:
                    return Mathf.Sqrt(FourPi / 5f) * TwoPi * InvHalfPi;
            }
        }

        private static TextureFormat SelectProfileParamLutFormat()
        {
            if (SystemInfo.SupportsTextureFormat(TextureFormat.RGBAHalf))
            {
                return TextureFormat.RGBAHalf;
            }

            return SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat)
                ? TextureFormat.RGBAFloat
                : TextureFormat.RGBA32;
        }

        private static TextureFormat SelectPreIntegratedLutFormat()
        {
            return SystemInfo.SupportsTextureFormat(TextureFormat.RGBAHalf)
                ? TextureFormat.RGBAHalf
                : TextureFormat.RGBA32;
        }

        private static GraphicsFormat SelectGpuLutFormat()
        {
            return GraphicsFormat.R16G16B16A16_SFloat;
        }

        private static GraphicsFormat SelectGpuProfileParamLutFormat()
        {
            switch (SelectProfileParamLutFormat())
            {
                case TextureFormat.RGBAHalf:
                    return GraphicsFormat.R16G16B16A16_SFloat;
                case TextureFormat.RGBAFloat:
                    return GraphicsFormat.R32G32B32A32_SFloat;
                default:
                    return GraphicsFormat.R8G8B8A8_UNorm;
            }
        }

        private static bool CanUseGpuProfileParamLut()
        {
            var format = SelectGpuProfileParamLutFormat();
            if (!SystemInfo.IsFormatSupported(format, FormatUsage.Sample) ||
                !SystemInfo.IsFormatSupported(format, FormatUsage.Render))
            {
                LogGpuFallbackOnce("GPU profile-param LUT format is not sampleable.");
                return false;
            }

            return true;
        }

        private static bool CanUseGpuProfileParamLutForRandomWrite()
        {
            if (!CanUseGpuProfileParamLut())
            {
                return false;
            }

            var format = SelectGpuProfileParamLutFormat();
            if (!SystemInfo.IsFormatSupported(format, FormatUsage.LoadStore))
            {
                LogGpuFallbackOnce("GPU profile-param LUT format does not support random write.");
                return false;
            }

            return true;
        }

        private static bool CanUseGpuLutBaker()
        {
            if (lutBakerUnavailable || !SystemInfo.supportsComputeShaders)
            {
                return false;
            }

            if (!SystemInfo.IsFormatSupported(SelectGpuLutFormat(), FormatUsage.LoadStore))
            {
                LogGpuFallbackOnce("GPU LUT format does not support random write.");
                lutBakerUnavailable = true;
                return false;
            }

            return true;
        }

        private static ComputeShader GetLutBakerShader()
        {
            if (lutBakerUnavailable)
            {
                return null;
            }

            if (lutBakerShader != null)
            {
                return lutBakerShader;
            }

            lutBakerShader = Resources.Load<ComputeShader>("BurtSubsurfaceLutBaker");
            if (lutBakerShader == null)
            {
                LogGpuFallbackOnce("BurtSubsurfaceLutBaker compute shader is missing.");
                lutBakerUnavailable = true;
                return null;
            }

            try
            {
                profileParamKernel = lutBakerShader.FindKernel("GenerateProfileParamLut");
                preIntegratedKernel = lutBakerShader.FindKernel("GeneratePreIntegratedLut");
                shKernel = lutBakerShader.FindKernel("GenerateSHLut");
            }
            catch (System.Exception exception)
            {
                LogGpuFallbackOnce("BurtSubsurfaceLutBaker kernel lookup failed: " + exception.Message);
                lutBakerUnavailable = true;
                lutBakerShader = null;
                profileParamKernel = -1;
                preIntegratedKernel = -1;
                shKernel = -1;
                return null;
            }

            return lutBakerShader;
        }

        private static void LogGpuFallbackOnce(string reason)
        {
            if (loggedGpuFallback)
            {
                return;
            }

            loggedGpuFallback = true;
            Debug.LogWarning("BurtRP Subsurface GPU LUT path disabled. Falling back to CPU LUT generation. " + reason);
        }

        private static int ComputePaletteHash(BurtSubsurfaceProfilePalette palette)
        {
            unchecked
            {
                var count = Mathf.Clamp(palette.Count, 1, ProfileSlotCount);
                var fallback = palette.GetSettings(0);
                var hash = 17;
                hash = hash * 31 + LutFormulaVersion;
                hash = hash * 31 + count;
                for (var i = 0; i < ProfileSlotCount; i++)
                {
                    var settings = i < count ? palette.GetSettings(i) : fallback;
                    hash = HashColor(hash, settings.SurfaceAlbedo);
                    hash = HashColor(hash, settings.MeanFreePathColor);
                    hash = HashFloat(hash, settings.MeanFreePathDistance);
                    hash = HashFloat(hash, settings.WorldUnitScale);
                }

                return hash;
            }
        }

        private static int ComputePreIntegratedProfileHash(BurtSubsurfaceProfileSettings settings)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + LutFormulaVersion;
                hash = HashColor(hash, settings.SurfaceAlbedo);
                hash = HashColor(hash, settings.MeanFreePathColor);
                hash = HashFloat(hash, settings.MeanFreePathDistance);
                return HashFloat(hash, settings.WorldUnitScale);
            }
        }

        private static int GetCachedPaletteHash(BurtSubsurfaceProfilePalette palette)
        {
            if (!hasLastPaletteHashes)
            {
                lastPaletteHash = ComputePaletteHash(palette);
                lastProfileParamPaletteHash = ComputeProfileParamPaletteHash(palette);
                hasLastPaletteHashes = true;
            }

            return lastPaletteHash;
        }

        private static int GetCachedProfileParamPaletteHash(BurtSubsurfaceProfilePalette palette)
        {
            if (!hasLastPaletteHashes)
            {
                lastPaletteHash = ComputePaletteHash(palette);
                lastProfileParamPaletteHash = ComputeProfileParamPaletteHash(palette);
                hasLastPaletteHashes = true;
            }

            return lastProfileParamPaletteHash;
        }

        public static void BeginPaletteBinding()
        {
            hasLastPaletteHashes = false;
        }

        private static int ComputeProfileParamPaletteHash(BurtSubsurfaceProfilePalette palette)
        {
            unchecked
            {
                var count = Mathf.Clamp(palette.Count, 1, ProfileSlotCount);
                var fallback = palette.GetSettings(0);
                var hash = 23;
                hash = hash * 31 + LutFormulaVersion;
                hash = hash * 31 + count;
                for (var i = 0; i < ProfileSlotCount; i++)
                {
                    var settings = i < count ? palette.GetSettings(i) : fallback;
                    hash = HashProfileSettings(hash, settings);
                }

                return hash;
            }
        }

        private static int HashProfileSettings(int hash, BurtSubsurfaceProfileSettings settings)
        {
            hash = HashColor(hash, settings.SurfaceAlbedo);
            hash = HashColor(hash, settings.MeanFreePathColor);
            hash = HashFloat(hash, settings.MeanFreePathDistance);
            hash = HashFloat(hash, settings.WorldUnitScale);
            hash = HashColor(hash, settings.Tint);
            hash = HashColor(hash, settings.BoundaryColorBleed);
            hash = HashFloat(hash, settings.ExtinctionScale);
            hash = HashFloat(hash, settings.TransmissionNormalScale);
            hash = HashFloat(hash, settings.ScatteringDistribution);
            hash = HashFloat(hash, settings.IOR);
            hash = HashColor(hash, settings.TransmissionTintColor);
            hash = HashFloat(hash, settings.DualSpecularRoughness0);
            hash = HashFloat(hash, settings.DualSpecularRoughness1);
            hash = HashFloat(hash, settings.DualSpecularLobeMix);
            hash = HashFloat(hash, settings.RadiusPixels);
            hash = HashFloat(hash, settings.DepthSigma);
            hash = HashFloat(hash, settings.NormalSigma);
            hash = HashFloat(hash, settings.Blend);
            hash = HashFloat(hash, settings.DistanceScale);
            hash = HashFloat(hash, settings.BoundaryBleed);
            hash = HashFloat(hash, settings.TintStrength);
            return HashFloat(hash, settings.MinStrength);
        }

        private static int ComputeProfileParamRowHash(BurtSubsurfaceProfileSettings settings)
        {
            unchecked
            {
                var hash = 23;
                hash = hash * 31 + LutFormulaVersion;
                return HashProfileSettings(hash, settings);
            }
        }

        private static int HashColor(int hash, Color value)
        {
            hash = HashFloat(hash, value.r);
            hash = HashFloat(hash, value.g);
            hash = HashFloat(hash, value.b);
            return HashFloat(hash, value.a);
        }

        private static int HashFloat(int hash, float value)
        {
            return hash * 31 + Mathf.RoundToInt(value * 10000f);
        }

        private static void DestroyTexture(Texture texture)
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
        }

        private static void DestroyRenderTexture(RenderTexture texture)
        {
            if (texture == null)
            {
                return;
            }

            if (texture.IsCreated())
            {
                texture.Release();
            }

            DestroyTexture(texture);
        }

        private static void ReleaseProfileParamSettingsBuffer()
        {
            if (profileParamSettingsBuffer == null)
            {
                return;
            }

            profileParamSettingsBuffer.Release();
            profileParamSettingsBuffer = null;
        }

#if UNITY_EDITOR
        private static void RegisterEditorTextureRebuild()
        {
            if (editorTextureRebuildUpdateRegistered)
            {
                return;
            }

            editorTextureRebuildUpdateRegistered = true;
            EditorApplication.update += DelayedEditorTextureRebuild;
        }

        private static void DelayedEditorTextureRebuild()
        {
            if (Application.isPlaying)
            {
                UnregisterEditorTextureRebuild();
                return;
            }

            if (GUIUtility.hotControl != 0 || EditorGUIUtility.editingTextField)
            {
                lastInteractiveTime = EditorApplication.timeSinceStartup;
                return;
            }

            if (EditorApplication.timeSinceStartup - lastInteractiveTime < InteractiveRebuildDelaySeconds)
            {
                return;
            }

            UnregisterEditorTextureRebuild();
            MarkCachedTextureContentsDirty();
            SceneView.RepaintAll();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private static void UnregisterEditorTextureRebuild()
        {
            if (!editorTextureRebuildUpdateRegistered)
            {
                return;
            }

            EditorApplication.update -= DelayedEditorTextureRebuild;
            editorTextureRebuildUpdateRegistered = false;
        }

        private static void MarkCachedTextureContentsDirty()
        {
            preIntegratedLutHash = 0;
            shLutHash = 0;
            profileParamLutHash = 0;
            hasLastPaletteHashes = false;
        }
#endif

        private static bool ShouldDeferTextureRebuild()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                return false;
            }

            if (GUIUtility.hotControl != 0 || EditorGUIUtility.editingTextField)
            {
                MarkEditorInteraction();
                return true;
            }

            return EditorApplication.timeSinceStartup - lastInteractiveTime < InteractiveRebuildDelaySeconds;
#else
            return false;
#endif
        }

        public static Vector3 EvaluateBurleyScatteringProfile(
            float radius,
            Vector3 surfaceAlbedo,
            Vector3 scalingFactor,
            Vector3 diffuseMeanFreePath)
        {
            return new Vector3(
                EvaluateBurleyScatteringProfile(radius, surfaceAlbedo.x, scalingFactor.x, diffuseMeanFreePath.x),
                EvaluateBurleyScatteringProfile(radius, surfaceAlbedo.y, scalingFactor.y, diffuseMeanFreePath.y),
                EvaluateBurleyScatteringProfile(radius, surfaceAlbedo.z, scalingFactor.z, diffuseMeanFreePath.z));
        }

        private static float EvaluateBurleyScatteringProfile(float radius, float surfaceAlbedo, float scalingFactor, float diffuseMeanFreePath)
        {
            var diffuseDistance = Mathf.Max(diffuseMeanFreePath, 0.01f);
            var normalizedRadius = Mathf.Max(radius, 0f) / diffuseDistance;
            var inverseDiffusion = 1f / Mathf.Max(scalingFactor, 0.0001f);
            var exponent = -normalizedRadius / inverseDiffusion;
            var value = surfaceAlbedo * (Mathf.Exp(exponent) + Mathf.Exp(exponent / 3f)) / (inverseDiffusion * diffuseDistance) / (8f * Pi);
            return Mathf.Max(value, 0f);
        }

        private static Vector3 EvaluateBurleyRingIntegral(
            float rawNoL,
            float curvature,
            Vector3 surfaceAlbedo,
            Vector3 diffuseMeanFreePathMm)
        {
            var cosTheta = Mathf.Clamp(rawNoL, -1f, 1f);
            var theta = Mathf.Acos(cosTheta);
            var sinTheta = Mathf.Sin(theta);
            var halfPi = Pi * 0.5f;
            var s3d = GetSearchLightDiffuseScalingFactor(surfaceAlbedo);
            var burleyA = Multiply(Divide(s3d, Multiply(diffuseMeanFreePathMm, curvature)), -2f / Pi);
            var burleyB = burleyA / 3f;
            var integralOnRing = 2f * (
                IntegralBurleyDiffusion(Pi, burleyA, burleyB) -
                IntegralBurleyDiffusion(0f, burleyA, burleyB));

            Vector3 integralOnNegativeAngle;
            Vector3 integralOnPositiveAngle;
            if (theta <= halfPi)
            {
                var angleStart = -(halfPi + theta);
                integralOnNegativeAngle =
                    ApproximationRingIntegralOfBurleyDiffusion(0f, 0f, 1f, theta, cosTheta, sinTheta, -burleyA, -burleyB) -
                    ApproximationRingIntegralOfBurleyDiffusion(angleStart, Mathf.Sin(angleStart), Mathf.Cos(angleStart), theta, cosTheta, sinTheta, -burleyA, -burleyB);

                var angleEnd = halfPi - theta;
                integralOnPositiveAngle =
                    ApproximationRingIntegralOfBurleyDiffusion(angleEnd, Mathf.Sin(angleEnd), Mathf.Cos(angleEnd), theta, cosTheta, sinTheta, burleyA, burleyB) -
                    ApproximationRingIntegralOfBurleyDiffusion(0f, 0f, 1f, theta, cosTheta, sinTheta, burleyA, burleyB);
            }
            else
            {
                var angleEnd = halfPi - theta;
                integralOnNegativeAngle =
                    ApproximationRingIntegralOfBurleyDiffusion(angleEnd, Mathf.Sin(angleEnd), Mathf.Cos(angleEnd), theta, cosTheta, sinTheta, -burleyA, -burleyB) -
                    ApproximationRingIntegralOfBurleyDiffusion(-Pi, 0f, -1f, theta, cosTheta, sinTheta, -burleyA, -burleyB);

                var angleStart = halfPi * 3f - theta;
                integralOnPositiveAngle =
                    ApproximationRingIntegralOfBurleyDiffusion(Pi, 0f, -1f, theta, cosTheta, sinTheta, burleyA, burleyB) -
                    ApproximationRingIntegralOfBurleyDiffusion(angleStart, Mathf.Sin(angleStart), Mathf.Cos(angleStart), theta, cosTheta, sinTheta, burleyA, burleyB);
            }

            return Divide(integralOnNegativeAngle + integralOnPositiveAngle, integralOnRing);
        }

        private static Vector3 ApproximationRingIntegralOfBurleyDiffusion(
            float angle,
            float sinAngle,
            float cosAngle,
            float theta,
            float cosTheta,
            float sinTheta,
            Vector3 burleyA,
            Vector3 burleyB)
        {
            return
                cosTheta * (IntegralCosineWithExponent(angle, sinAngle, cosAngle, burleyA) + IntegralCosineWithExponent(angle, sinAngle, cosAngle, burleyB)) -
                sinTheta * (IntegralSineWithExponent(angle, sinAngle, cosAngle, burleyA) + IntegralSineWithExponent(angle, sinAngle, cosAngle, burleyB));
        }

        private static Vector3 IntegralCosineWithExponent(float angle, float sinAngle, float cosAngle, Vector3 sharpness)
        {
            return Divide(
                Multiply(BurleyUnit(angle, sharpness), Add(Multiply(sharpness, cosAngle), sinAngle)),
                Add(Multiply(sharpness, sharpness), 1f));
        }

        private static Vector3 IntegralSineWithExponent(float angle, float sinAngle, float cosAngle, Vector3 sharpness)
        {
            return Divide(
                Multiply(BurleyUnit(angle, sharpness), Add(Multiply(sharpness, sinAngle), -cosAngle)),
                Add(Multiply(sharpness, sharpness), 1f));
        }

        private static Vector3 IntegralBurleyDiffusion(float angle, Vector3 burleyA, Vector3 burleyB)
        {
            return Divide(BurleyUnit(angle, burleyA), burleyA) + Divide(BurleyUnit(angle, burleyB), burleyB);
        }

        private static Vector3 BurleyUnit(float angle, Vector3 sharpness)
        {
            return new Vector3(
                Mathf.Exp(sharpness.x * angle),
                Mathf.Exp(sharpness.y * angle),
                Mathf.Exp(sharpness.z * angle));
        }

        public static Vector3 GetSearchLightDiffuseScalingFactor(Vector3 surfaceAlbedo)
        {
            var value = surfaceAlbedo - new Vector3(0.33f, 0.33f, 0.33f);
            return new Vector3(3.5f, 3.5f, 3.5f) + 100f * Multiply(Multiply(value, value), Multiply(value, value));
        }

        private static Vector3 GetPerpendicularScalingFactor(Vector3 surfaceAlbedo)
        {
            var value = Abs(surfaceAlbedo - new Vector3(0.8f, 0.8f, 0.8f));
            return new Vector3(1.85f, 1.85f, 1.85f) - surfaceAlbedo + 7f * Multiply(Multiply(value, value), value);
        }

        private static Vector3 Multiply(Vector3 value, float scale)
        {
            return new Vector3(value.x * scale, value.y * scale, value.z * scale);
        }

        private static Color ToColor(Vector4 value)
        {
            return new Color(value.x, value.y, value.z, value.w);
        }

        private static Color ToColor(Vector3 value, float alpha)
        {
            return new Color(value.x, value.y, value.z, alpha);
        }

        private static Vector3 Multiply(Vector3 lhs, Vector3 rhs)
        {
            return new Vector3(lhs.x * rhs.x, lhs.y * rhs.y, lhs.z * rhs.z);
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        private static Vector3 Add(Vector3 value, float scalar)
        {
            return new Vector3(value.x + scalar, value.y + scalar, value.z + scalar);
        }

        private static Vector3 Divide(Vector3 lhs, Vector3 rhs)
        {
            return new Vector3(lhs.x / rhs.x, lhs.y / rhs.y, lhs.z / rhs.z);
        }

        private static Vector3 Divide(Vector3 lhs, float rhs)
        {
            return new Vector3(lhs.x / rhs, lhs.y / rhs, lhs.z / rhs);
        }

        private static Vector3 Max(Vector3 value, float minimum)
        {
            return new Vector3(
                Mathf.Max(value.x, minimum),
                Mathf.Max(value.y, minimum),
                Mathf.Max(value.z, minimum));
        }

        private static Vector3 Clamp(Vector3 value, float minimum, float maximum)
        {
            return new Vector3(
                Mathf.Clamp(value.x, minimum, maximum),
                Mathf.Clamp(value.y, minimum, maximum),
                Mathf.Clamp(value.z, minimum, maximum));
        }
    }
}
