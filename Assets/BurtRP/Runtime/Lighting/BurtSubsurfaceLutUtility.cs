using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Burt.RenderPipeline
{
    public static class BurtSubsurfaceLutUtility
    {
        public const string TextureShaderName = "_BurtSubsurfacePreIntegratedLut";
        public const string EnabledShaderName = "_BurtSubsurfacePreIntegratedLutEnabled";
        public const string ProfileParamLutShaderName = "_BurtSubsurfaceProfileParamLut";
        public const string ProfileParamLutEnabledShaderName = "_BurtSubsurfaceProfileParamLutEnabled";
        public const string ProfileParamLutSizeShaderName = "_BurtSubsurfaceProfileParamLutSize";

        public static readonly int TextureId = Shader.PropertyToID(TextureShaderName);
        public static readonly int EnabledId = Shader.PropertyToID(EnabledShaderName);
        public static readonly int ProfileParamLutId = Shader.PropertyToID(ProfileParamLutShaderName);
        public static readonly int ProfileParamLutEnabledId = Shader.PropertyToID(ProfileParamLutEnabledShaderName);
        public static readonly int ProfileParamLutSizeId = Shader.PropertyToID(ProfileParamLutSizeShaderName);

        public const int PreIntegratedLutSize = 256;
        private const int LutSize = PreIntegratedLutSize;
        private const float Pi = 3.14159265359f;
        private const float SurfaceAlbedoBias = 0.009f;
        private const float MeanFreePathBias = 0.009f;
        private const float DiffuseMeanFreePathToMeanFreePathMagicNumber = 0.6f;
        private const float CentimetersToMillimeters = 10f;
        private const float MillimetersToCentimeters = 0.1f;
        private const float MaxDiffuseMeanFreePathInMillimeters = 500f;
        private const int LutFormulaVersion = 2;
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

        private static readonly float[] Kernel0Offsets =
        {
            0f, 0.22f, 0.46f, 0.78f, 1.16f, 1.60f, 2.12f, 2.74f, 3.48f, 4.36f, 5.42f, 6.68f, 8.16f
        };

        private static readonly Vector3[] Kernel0Weights =
        {
            new Vector3(0.204f, 0.236f, 0.290f),
            new Vector3(0.150f, 0.165f, 0.168f),
            new Vector3(0.118f, 0.123f, 0.114f),
            new Vector3(0.090f, 0.088f, 0.074f),
            new Vector3(0.066f, 0.058f, 0.043f),
            new Vector3(0.047f, 0.036f, 0.023f),
            new Vector3(0.032f, 0.021f, 0.012f),
            new Vector3(0.021f, 0.012f, 0.006f),
            new Vector3(0.013f, 0.006f, 0.003f),
            new Vector3(0.008f, 0.003f, 0.0015f),
            new Vector3(0.0045f, 0.0015f, 0.0007f),
            new Vector3(0.0025f, 0.0007f, 0.0003f),
            new Vector3(0.0015f, 0.0003f, 0.0001f)
        };

        private static Texture2DArray preIntegratedLut;
        private static Texture2DArray fallbackPreIntegratedLut;
        private static int preIntegratedLutHash;
        private static Texture2D profileParamLut;
        private static int profileParamLutHash;
        private static readonly int[] preIntegratedProfileHashes = new int[ProfileSlotCount];
        private static readonly int[] profileParamRowHashes = new int[ProfileSlotCount];
        private static Color[] preIntegratedSlicePixels;
        private static Color[] profileParamRowPixels;
        private static bool preIntegratedProfileHashesValid;
        private static bool profileParamRowHashesValid;
        private static int lastPaletteHash;
        private static int lastProfileParamPaletteHash;
        private static bool hasLastPaletteHashes;
#if UNITY_EDITOR
        private static double lastInteractiveTime;
        private const double InteractiveRebuildDelaySeconds = 0.35;
#endif

        public static Texture GetOrCreatePreIntegratedLut()
        {
            return GetOrCreatePreIntegratedLut(BurtSubsurfaceProfilePalette.Resolve(BurtSubsurfaceProfileSettings.Default, null));
        }

        public static Texture GetOrCreatePreIntegratedLut(BurtSubsurfaceProfilePalette palette)
        {
            if (preIntegratedLut != null)
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

        public static Texture2D GetOrCreateProfileParamLut()
        {
            return GetOrCreateProfileParamLut(BurtSubsurfaceProfilePalette.Resolve(BurtSubsurfaceProfileSettings.Default, null));
        }

        public static Texture2D GetOrCreateProfileParamLut(BurtSubsurfaceProfilePalette palette)
        {
            if (profileParamLut != null)
            {
                if (ShouldDeferTextureRebuild())
                {
                    return profileParamLut;
                }

                var cachedHash = GetCachedProfileParamPaletteHash(palette);
                if (profileParamLutHash == cachedHash)
                {
                    return profileParamLut;
                }
            }
            else if (ShouldDeferTextureRebuild())
            {
                return null;
            }

            var currentHash = GetCachedProfileParamPaletteHash(palette);
            EnsureProfileParamLutTexture();
            if (UpdateProfileParamLutRows(palette))
            {
                profileParamLut.Apply(false, false);
            }

            profileParamLutHash = currentHash;
            return profileParamLut;
        }

        public static void InvalidateCachedTextures()
        {
            DestroyTexture(preIntegratedLut);
            preIntegratedLut = null;
            preIntegratedLutHash = 0;
            preIntegratedProfileHashesValid = false;
            DestroyTexture(profileParamLut);
            profileParamLut = null;
            profileParamLutHash = 0;
            profileParamRowHashesValid = false;
            hasLastPaletteHashes = false;
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
            var diffuseMeanFreePath = GetEffectiveDiffuseMeanFreePathForLut(settings);
            var samplingChannel = GetSamplingChannel(diffuseMeanFreePath);

            pixels[rowOffset + ProfileParamSurfaceAlbedoOffset] = ToColor(surfaceAlbedo, samplingChannel);
            pixels[rowOffset + ProfileParamMeanFreePathOffset] = ToColor(diffuseMeanFreePath, settings.MeanFreePathScreenScale);
            pixels[rowOffset + ProfileParamTintOffset] = new Color(settings.Tint.r, settings.Tint.g, settings.Tint.b, settings.WorldUnitScale);
            pixels[rowOffset + ProfileParamBoundaryColorBleedOffset] = new Color(settings.BoundaryColorBleed.r, settings.BoundaryColorBleed.g, settings.BoundaryColorBleed.b, settings.BoundaryColorBleed.a);
            pixels[rowOffset + ProfileParamDualSpecularOffset] = ToColor(settings.DualSpecularVector);
            pixels[rowOffset + ProfileParamTransmissionParamsOffset] = ToColor(settings.TransmissionVector);

            for (var i = 0; i < ProfileParamTransmissionProfileSize; i++)
            {
                var normalizedDistance = i / (float)(ProfileParamTransmissionProfileSize - 1);
                var transmission = EvaluateTransmissionProfile(settings, diffuseMeanFreePath, normalizedDistance);
                pixels[rowOffset + ProfileParamTransmissionProfileOffset + i] = ToColor(transmission, 1f);
            }

            FillKernel(pixels, rowOffset + ProfileParamKernel0Offset, ProfileParamKernel0Size, settings, diffuseMeanFreePath, Kernel0Offsets, Kernel0Weights);
            FillKernel(pixels, rowOffset + ProfileParamKernel1Offset, ProfileParamKernel1Size, settings, diffuseMeanFreePath, null, null);
            FillKernel(pixels, rowOffset + ProfileParamKernel2Offset, ProfileParamKernel2Size, settings, diffuseMeanFreePath, null, null);
        }

        private static void FillKernel(
            Color[] pixels,
            int startIndex,
            int kernelSize,
            BurtSubsurfaceProfileSettings settings,
            Vector3 diffuseMeanFreePath,
            float[] baseOffsets,
            Vector3[] baseWeights)
        {
            var surfaceAlbedo = ToRgb(settings.SurfaceAlbedo);
            var maxMeanFreePath = Mathf.Max(diffuseMeanFreePath.x, Mathf.Max(diffuseMeanFreePath.y, diffuseMeanFreePath.z));
            var normalizedMeanFreePath = Divide(diffuseMeanFreePath, Mathf.Max(maxMeanFreePath, 0.0001f));
            for (var i = 0; i < kernelSize; i++)
            {
                var normalizedIndex = kernelSize > 1 ? i / (float)(kernelSize - 1) : 0f;
                var offset = baseOffsets != null && i < baseOffsets.Length
                    ? baseOffsets[i]
                    : Mathf.Pow(normalizedIndex, 1.35f) * Kernel0Offsets[Kernel0Offsets.Length - 1];
                var baseWeight = baseWeights != null && i < baseWeights.Length
                    ? baseWeights[i]
                    : EvaluateKernelWeight(settings, diffuseMeanFreePath, offset);
                var profileWeight = Multiply(baseWeight, Vector3.Lerp(Vector3.one, normalizedMeanFreePath, 0.65f));
                profileWeight = Multiply(profileWeight, Vector3.Lerp(Vector3.one, surfaceAlbedo, 0.35f));
                pixels[startIndex + i] = ToColor(Max(profileWeight, 0.0001f), offset);
            }
        }

        private static Vector3 EvaluateKernelWeight(
            BurtSubsurfaceProfileSettings settings,
            Vector3 diffuseMeanFreePath,
            float offset)
        {
            var surfaceAlbedo = GetSurfaceAlbedoForLut(settings);
            var searchLightScale = GetSearchLightDiffuseScalingFactor(surfaceAlbedo);
            var distance = Max(diffuseMeanFreePath, 0.0001f);
            var extinctionScale = settings.ExtinctionScale;
            var a = Exp(Multiply(Divide(Multiply(searchLightScale, distance), Mathf.Max(extinctionScale, 0.01f)), -Mathf.Abs(offset)));
            var b = Exp(Multiply(Divide(Multiply(searchLightScale, distance * 3f), Mathf.Max(extinctionScale, 0.01f)), -Mathf.Abs(offset)));
            return Max(Multiply(surfaceAlbedo, a + b) * 0.5f, 0.0001f);
        }

        private static Vector3 EvaluateTransmissionProfile(
            BurtSubsurfaceProfileSettings settings,
            Vector3 diffuseMeanFreePath,
            float normalizedDistance)
        {
            var transmissionTint = ToRgb(settings.TransmissionTintColor);
            var distance = normalizedDistance * MaxTransmissionProfileDistance;
            var extinction = Mathf.Max(settings.ExtinctionScale, 0.01f);
            var opticalDepth = Divide(new Vector3(distance, distance, distance), Max(Multiply(diffuseMeanFreePath, MaxTransmissionProfileDistance), 0.01f));
            return Multiply(Exp(Multiply(opticalDepth, -extinction)), transmissionTint);
        }

        private static float GetSamplingChannel(Vector3 value)
        {
            if (value.y >= value.x && value.y >= value.z)
            {
                return 1f;
            }

            return value.z >= value.x && value.z >= value.y ? 2f : 0f;
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

        private static Vector3 Exp(Vector3 value)
        {
            return new Vector3(
                Mathf.Exp(value.x),
                Mathf.Exp(value.y),
                Mathf.Exp(value.z));
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
