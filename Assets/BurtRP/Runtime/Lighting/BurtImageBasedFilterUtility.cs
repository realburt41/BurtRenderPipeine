using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    internal readonly struct BurtImageBasedFilterResult
    {
        public readonly Texture SourceTexture;
        public readonly Texture SpecularTexture;
        public readonly Texture DiffuseTexture;
        public readonly Texture DiffuseSHTexture;
        public readonly RenderTextureFormat DiffuseSHFormat;
        public readonly Vector4 SpecularHDRDecodeValues;
        public readonly Vector4 DiffuseHDRDecodeValues;
        public readonly float SpecularMaxMip;
        public readonly float DiffuseMip;
        public readonly float BakedIntensity;
        public readonly bool Filtered;
        public readonly string Status;

        public BurtImageBasedFilterResult(
            Texture sourceTexture,
            Texture specularTexture,
            Texture diffuseTexture,
            Texture diffuseSHTexture,
            RenderTextureFormat diffuseSHFormat,
            Vector4 specularHDRDecodeValues,
            Vector4 diffuseHDRDecodeValues,
            float specularMaxMip,
            float diffuseMip,
            float bakedIntensity,
            bool filtered,
            string status)
        {
            SourceTexture = sourceTexture;
            SpecularTexture = specularTexture;
            DiffuseTexture = diffuseTexture;
            DiffuseSHTexture = diffuseSHTexture;
            DiffuseSHFormat = diffuseSHFormat;
            SpecularHDRDecodeValues = specularHDRDecodeValues;
            DiffuseHDRDecodeValues = diffuseHDRDecodeValues;
            SpecularMaxMip = Mathf.Max(0f, specularMaxMip);
            DiffuseMip = Mathf.Max(0f, diffuseMip);
            BakedIntensity = Mathf.Max(0f, bakedIntensity);
            Filtered = filtered;
            Status = status;
        }
    }

    internal readonly struct BurtImageBasedFilterBakeSettings
    {
        public readonly Vector4 Rotation;
        public readonly Color Tint;
        public readonly float Intensity;
        public readonly bool LowerHemisphereEnabled;
        public readonly Color LowerHemisphereColor;

        public BurtImageBasedFilterBakeSettings(Vector4 rotation, Color tint, float intensity, bool lowerHemisphereEnabled, Color lowerHemisphereColor)
        {
            Rotation = SanitizeRotation(rotation);
            Tint = new Color(Mathf.Max(0f, tint.r), Mathf.Max(0f, tint.g), Mathf.Max(0f, tint.b), 1f);
            Intensity = Mathf.Max(0f, intensity);
            LowerHemisphereEnabled = lowerHemisphereEnabled && lowerHemisphereColor.a > 0f;
            LowerHemisphereColor = LowerHemisphereEnabled
                ? new Color(Mathf.Max(0f, lowerHemisphereColor.r), Mathf.Max(0f, lowerHemisphereColor.g), Mathf.Max(0f, lowerHemisphereColor.b), Mathf.Clamp01(lowerHemisphereColor.a))
                : Color.clear;
        }

        public static BurtImageBasedFilterBakeSettings Default => new BurtImageBasedFilterBakeSettings(new Vector4(1f, 0f, 0f, 0f), Color.white, 1f, false, Color.clear);

        private static Vector4 SanitizeRotation(Vector4 rotation)
        {
            if (Mathf.Abs(rotation.x) < 0.0001f && Mathf.Abs(rotation.y) < 0.0001f)
            {
                return new Vector4(1f, 0f, 0f, 0f);
            }

            var length = Mathf.Sqrt(rotation.x * rotation.x + rotation.y * rotation.y);
            return length > 0.0001f ? new Vector4(rotation.x / length, rotation.y / length, 0f, 0f) : new Vector4(1f, 0f, 0f, 0f);
        }
    }

    internal static class BurtImageBasedFilterUtility
    {
        private const int SpecularSize = 256;
        private const int DiffuseSize = 32;
        private const int SpecularMipCount = 9;
        private const int SpecularMaxMip = SpecularMipCount - 1;
        private const int DiffuseSampleCount = 512;
        private const int DiffuseSHWidth = 7;
        private const int DiffuseSHHeight = 1;
        private const int DiffuseSHSampleCount = 256;
        private const int CubemapFaceCount = 6;
        private const int BakeSourcePassIndex = 2;
        private const int DiffuseSHPassIndex = 3;
        private const int FilterVersion = 14;

        public const string DiffuseSHLayout = "UnitySHArAgAbBrBgBbC";
        public const string DiffuseSHParity = "XRenderAmbientConvolveToUnitySH9";
        public const string PreIntegratedFGParity = "XRenderCommonMaterial.GetPreIntegratedFG";
        public static int DiffuseSHDiagnosticWidth => DiffuseSHWidth;
        public static int DiffuseSHDiagnosticHeight => DiffuseSHHeight;
        public static int DiffuseSHDiagnosticSampleCount => DiffuseSHSampleCount;
        public static string DiffuseSHValidationStatus => Cache.GetDiffuseSHValidationStatus();
        public static string NumericValidationStatus => GetNumericValidationStatus();
        public static int SpecularDiagnosticSize => SpecularSize;
        public static int SpecularDiagnosticMipCount => SpecularMipCount;
        public static int SpecularDiagnosticMaxMip => SpecularMaxMip;
        public static int DiffuseDiagnosticSize => DiffuseSize;

        private static readonly int SourceTextureId = Shader.PropertyToID("_BurtIBLFilterSource");
        private static readonly int SourceHDRId = Shader.PropertyToID("_BurtIBLFilterSourceHDR");
        private static readonly int FilterParamsId = Shader.PropertyToID("_BurtIBLFilterParams");
        private static readonly int FilterFaceMipId = Shader.PropertyToID("_BurtIBLFilterFaceMip");
        private static readonly int PixelCoordToViewDirWSId = Shader.PropertyToID("_BurtIBLFilterPixelCoordToViewDirWS");
        private static readonly int BakeRotationId = Shader.PropertyToID("_BurtIBLFilterBakeRotation");
        private static readonly int BakeTintIntensityId = Shader.PropertyToID("_BurtIBLFilterBakeTintIntensity");
        private static readonly int BakeLowerHemisphereId = Shader.PropertyToID("_BurtIBLFilterBakeLowerHemisphere");

        private static readonly Vector4 DefaultHDRDecodeValues = new Vector4(1f, 1f, 0f, 0f);
        private static readonly Vector3[] CubemapLookAtList =
        {
            new Vector3(1f, 0f, 0f),
            new Vector3(-1f, 0f, 0f),
            new Vector3(0f, 1f, 0f),
            new Vector3(0f, -1f, 0f),
            new Vector3(0f, 0f, 1f),
            new Vector3(0f, 0f, -1f)
        };

        private static readonly Vector3[] CubemapUpVectorList =
        {
            new Vector3(0f, 1f, 0f),
            new Vector3(0f, 1f, 0f),
            new Vector3(0f, 0f, -1f),
            new Vector3(0f, 0f, 1f),
            new Vector3(0f, 1f, 0f),
            new Vector3(0f, 1f, 0f)
        };

        private static readonly FilterCache Cache = new FilterCache();
        private static Material filterMaterial;

        public static BurtImageBasedFilterResult Filter(CommandBuffer cmd, Texture sourceTexture, Vector4 sourceHDRDecodeValues, string sourceName)
        {
            return Filter(cmd, sourceTexture, sourceHDRDecodeValues, sourceName, BurtImageBasedFilterBakeSettings.Default, 0);
        }

        public static BurtImageBasedFilterResult Filter(CommandBuffer cmd, Texture sourceTexture, Vector4 sourceHDRDecodeValues, string sourceName, BurtImageBasedFilterBakeSettings bakeSettings, int dynamicSourceVersion = 0)
        {
            if (sourceTexture == null)
            {
                return CreateFallbackResult(null, sourceHDRDecodeValues, "NoSource");
            }

            if (!IsCubeTexture(sourceTexture))
            {
                return CreateFallbackResult(sourceTexture, sourceHDRDecodeValues, "Bypass:SourceIsNotCubemap");
            }

            var material = GetFilterMaterial();
            if (cmd == null || material == null)
            {
                return CreateFallbackResult(sourceTexture, sourceHDRDecodeValues, material == null ? "Bypass:MissingShader" : "Bypass:MissingCommandBuffer");
            }

            var sourceMaxMip = CalculateMaxMip(sourceTexture);
            var sourceId = sourceTexture.GetInstanceID();
            var diffuseSHFormat = SelectDiffuseSHFormat();
            var cubeFormat = SelectFilteredCubeFormat();
            var needsReallocate = Cache.FilterVersion != FilterVersion || Cache.SourceId != sourceId || Cache.SourceWidth != sourceTexture.width || Cache.SourceMipCount != sourceTexture.mipmapCount || Cache.CubeFormat != cubeFormat || Cache.DiffuseSHFormat != diffuseSHFormat || Cache.RuntimeSource == null || Cache.Specular == null || Cache.Diffuse == null || Cache.DiffuseSH == null;
            if (needsReallocate)
            {
                Cache.Release();
                Cache.FilterVersion = FilterVersion;
                Cache.SourceId = sourceId;
                Cache.SourceWidth = sourceTexture.width;
                Cache.SourceMipCount = sourceTexture.mipmapCount;
                Cache.CubeFormat = cubeFormat;
                Cache.RuntimeSource = CreateCubeRenderTexture(SpecularSize, true, cubeFormat, "Burt IBL Runtime Source: " + sourceTexture.name);
                Cache.Specular = CreateCubeRenderTexture(SpecularSize, true, cubeFormat, "Burt IBL Specular LD: " + sourceTexture.name);
                Cache.Diffuse = CreateCubeRenderTexture(DiffuseSize, false, cubeFormat, "Burt IBL Diffuse Irradiance: " + sourceTexture.name);
                Cache.DiffuseSHFormat = diffuseSHFormat;
                Cache.DiffuseSH = CreateTexture2DRenderTexture(DiffuseSHWidth, DiffuseSHHeight, diffuseSHFormat, "Burt IBL Diffuse SH9: " + sourceTexture.name);
                Cache.Ready = false;
                Cache.LastFilteredFrame = -1;
                Cache.LastDynamicSourceVersion = 0;
                Cache.ResetDiffuseSHReadback();
            }

            var sourceIsDynamic = sourceTexture is RenderTexture;
            var currentFrame = Time.frameCount;
            var bakeSettingsChanged = !Cache.MatchesBakeSettings(sourceHDRDecodeValues, bakeSettings);
            // Most dynamic sources refresh once per frame. Atmosphere supplies a monotonically
            // increasing version, so another camera can refresh its own sky in the same frame.
            var dynamicSourceChanged = sourceIsDynamic && (dynamicSourceVersion != 0
                ? Cache.LastDynamicSourceVersion != dynamicSourceVersion
                : Cache.LastFilteredFrame != currentFrame);
            if (!Cache.Ready || bakeSettingsChanged || dynamicSourceChanged)
            {
                Cache.SourceHDRDecodeValues = sourceHDRDecodeValues;
                Cache.BakeSettings = bakeSettings;
                Cache.ResetDiffuseSHReadback();
                ScheduleFilter(cmd, material, sourceTexture, sourceHDRDecodeValues, sourceMaxMip, bakeSettings, Cache);
                Cache.Ready = true;
                Cache.LastFilteredFrame = currentFrame;
                Cache.LastDynamicSourceVersion = dynamicSourceVersion;
            }

            var safeName = string.IsNullOrEmpty(sourceName) ? sourceTexture.name : sourceName;
            return new BurtImageBasedFilterResult(
                sourceTexture,
                Cache.Specular,
                Cache.Diffuse,
                Cache.DiffuseSH,
                Cache.DiffuseSHFormat,
                DefaultHDRDecodeValues,
                DefaultHDRDecodeValues,
                SpecularMaxMip,
                0f,
                bakeSettings.Intensity,
                true,
                GetFilterStatus("FilteredLD") + "(" + safeName + ")");
        }

        public static string Describe(Texture sourceTexture)
        {
            return Describe(sourceTexture, BurtImageBasedFilterBakeSettings.Default);
        }

        public static string Describe(Texture sourceTexture, BurtImageBasedFilterBakeSettings bakeSettings)
        {
            if (sourceTexture == null)
            {
                return "NoSource";
            }

            if (!IsCubeTexture(sourceTexture))
            {
                return "Bypass:SourceIsNotCubemap";
            }

            if (filterMaterial == null && Shader.Find("Hidden/BurtRP/ImageBasedFilter") == null)
            {
                return "Bypass:MissingShader";
            }

            return Cache.FilterVersion == FilterVersion && Cache.SourceId == sourceTexture.GetInstanceID() && Cache.Ready && Cache.MatchesBakeSettings(Cache.SourceHDRDecodeValues, bakeSettings) ? GetFilterStatus("FilteredLDReady") : GetFilterStatus("FilteredLDPending");
        }

        public static BurtImageBasedFilterResult CreateDebugResult(Texture sourceTexture, Vector4 sourceHDRDecodeValues, string sourceName)
        {
            return CreateDebugResult(sourceTexture, sourceHDRDecodeValues, sourceName, BurtImageBasedFilterBakeSettings.Default);
        }

        public static BurtImageBasedFilterResult CreateDebugResult(Texture sourceTexture, Vector4 sourceHDRDecodeValues, string sourceName, BurtImageBasedFilterBakeSettings bakeSettings)
        {
            var status = Describe(sourceTexture, bakeSettings);
            if (sourceTexture != null && Cache.FilterVersion == FilterVersion && Cache.SourceId == sourceTexture.GetInstanceID() && Cache.Ready && Cache.MatchesBakeSettings(sourceHDRDecodeValues, bakeSettings))
            {
                var safeName = string.IsNullOrEmpty(sourceName) ? sourceTexture.name : sourceName;
                return new BurtImageBasedFilterResult(
                    sourceTexture,
                    Cache.Specular,
                    Cache.Diffuse,
                    Cache.DiffuseSH,
                    Cache.DiffuseSHFormat,
                    DefaultHDRDecodeValues,
                    DefaultHDRDecodeValues,
                    SpecularMaxMip,
                    0f,
                    bakeSettings.Intensity,
                    true,
                    status + "(" + safeName + ")");
            }

            return CreateFallbackResult(sourceTexture, sourceHDRDecodeValues, status);
        }

        public static void Release()
        {
            Cache.Release();
            if (filterMaterial != null)
            {
                DestroyUnityObject(filterMaterial);
                filterMaterial = null;
            }
        }

        private static BurtImageBasedFilterResult CreateFallbackResult(Texture sourceTexture, Vector4 sourceHDRDecodeValues, string status)
        {
            var sourceMaxMip = CalculateMaxMip(sourceTexture);
            var diffuseTexture = IsCubeTexture(sourceTexture) ? sourceTexture : null;
            return new BurtImageBasedFilterResult(sourceTexture, sourceTexture, diffuseTexture, null, RenderTextureFormat.Default, sourceHDRDecodeValues, sourceHDRDecodeValues, sourceMaxMip, Mathf.Min(sourceMaxMip, SpecularMaxMip), 1f, false, status);
        }

        private static Material GetFilterMaterial()
        {
            if (filterMaterial != null)
            {
                return filterMaterial;
            }

            var shader = Shader.Find("Hidden/BurtRP/ImageBasedFilter");
            if (shader == null)
            {
                return null;
            }

            filterMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            return filterMaterial;
        }

        private static bool IsCubeTexture(Texture texture)
        {
            return texture != null && texture.dimension == TextureDimension.Cube;
        }

        private static float CalculateMaxMip(Texture texture)
        {
            return texture != null ? Mathf.Max(0f, texture.mipmapCount - 1f) : 0f;
        }

        private static RenderTexture CreateCubeRenderTexture(int size, bool useMipMap, RenderTextureFormat format, string name)
        {
            var texture = new RenderTexture(size, size, 0, format, RenderTextureReadWrite.Linear)
            {
                name = name,
                dimension = TextureDimension.Cube,
                volumeDepth = 6,
                useMipMap = useMipMap,
                autoGenerateMips = false,
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.Create();
            return texture;
        }

        private static RenderTexture CreateTexture2DRenderTexture(int width, int height, RenderTextureFormat format, string name)
        {
            var texture = new RenderTexture(width, height, 0, format, RenderTextureReadWrite.Linear)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.Create();
            return texture;
        }

        private static RenderTextureFormat SelectDiffuseSHFormat()
        {
            return SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat)
                ? RenderTextureFormat.ARGBFloat
                : RenderTextureFormat.ARGBHalf;
        }

        private static RenderTextureFormat SelectFilteredCubeFormat()
        {
            return SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB111110Float)
                ? RenderTextureFormat.RGB111110Float
                : RenderTextureFormat.ARGBHalf;
        }

        private static void ScheduleFilter(CommandBuffer cmd, Material material, Texture sourceTexture, Vector4 sourceHDRDecodeValues, float sourceMaxMip, BurtImageBasedFilterBakeSettings bakeSettings, FilterCache cache)
        {
            BakeRuntimeSourceCubemap(cmd, material, sourceTexture, sourceHDRDecodeValues, sourceMaxMip, bakeSettings, cache);

            var filterSourceTexture = cache.RuntimeSource != null ? (Texture)cache.RuntimeSource : sourceTexture;
            var filterSourceHDRDecodeValues = cache.RuntimeSource != null ? DefaultHDRDecodeValues : sourceHDRDecodeValues;
            var specularSourceMaxMip = cache.RuntimeSource != null ? SpecularMaxMip : sourceMaxMip;
            var diffuseSourceMaxMip = cache.RuntimeSource != null ? SpecularMaxMip : sourceMaxMip;

            cmd.SetGlobalTexture(SourceTextureId, filterSourceTexture);
            cmd.SetGlobalVector(SourceHDRId, filterSourceHDRDecodeValues);

            for (var mip = 0; mip < SpecularMipCount; mip++)
            {
                var mipSize = Mathf.Max(1, SpecularSize >> mip);
                var roughness = RoughnessFromReflectionMip(mip, SpecularMaxMip);
                var sampleCount = SpecularSampleCountForMip(mip);
                for (var face = 0; face < CubemapFaceCount; face++)
                {
                    if (mip == 0 && cache.RuntimeSource != null)
                    {
                        cmd.CopyTexture(cache.RuntimeSource, face, 0, cache.Specular, face, 0);
                        continue;
                    }

                    cmd.SetRenderTarget(new RenderTargetIdentifier(cache.Specular, mip, (CubemapFace)face));
                    cmd.SetViewport(new Rect(0f, 0f, mipSize, mipSize));
                    cmd.SetGlobalVector(FilterParamsId, new Vector4(roughness, specularSourceMaxMip, sampleCount, filterSourceTexture.width));
                    cmd.SetGlobalVector(FilterFaceMipId, new Vector4(face, mip, mipSize, SpecularMaxMip));
                    cmd.SetGlobalMatrix(PixelCoordToViewDirWSId, GetFacePixelCoordToViewDirMatrix(face, mipSize));
                    cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1);
                }
            }

            for (var face = 0; face < CubemapFaceCount; face++)
            {
                cmd.SetRenderTarget(new RenderTargetIdentifier(cache.Diffuse, 0, (CubemapFace)face));
                cmd.SetViewport(new Rect(0f, 0f, DiffuseSize, DiffuseSize));
                cmd.SetGlobalVector(FilterParamsId, new Vector4(1f, diffuseSourceMaxMip, DiffuseSampleCount, filterSourceTexture.width));
                cmd.SetGlobalVector(FilterFaceMipId, new Vector4(face, 0f, DiffuseSize, 0f));
                cmd.SetGlobalMatrix(PixelCoordToViewDirWSId, GetFacePixelCoordToViewDirMatrix(face, DiffuseSize));
                cmd.DrawProcedural(Matrix4x4.identity, material, 1, MeshTopology.Triangles, 3, 1);
            }

            cmd.SetRenderTarget(cache.DiffuseSH);
            cmd.SetViewport(new Rect(0f, 0f, DiffuseSHWidth, DiffuseSHHeight));
            cmd.SetGlobalVector(FilterParamsId, new Vector4(1f, diffuseSourceMaxMip, DiffuseSHSampleCount, filterSourceTexture.width));
            cmd.SetGlobalVector(FilterFaceMipId, new Vector4(0f, 0f, DiffuseSHWidth, 0f));
            cmd.DrawProcedural(Matrix4x4.identity, material, DiffuseSHPassIndex, MeshTopology.Triangles, 3, 1);
            RequestDiffuseSHReadback(cmd, cache);
        }

        private static void BakeRuntimeSourceCubemap(CommandBuffer cmd, Material material, Texture sourceTexture, Vector4 sourceHDRDecodeValues, float sourceMaxMip, BurtImageBasedFilterBakeSettings bakeSettings, FilterCache cache)
        {
            if (cache.RuntimeSource == null)
            {
                return;
            }

            cmd.SetGlobalTexture(SourceTextureId, sourceTexture);
            cmd.SetGlobalVector(SourceHDRId, sourceHDRDecodeValues);
            cmd.SetGlobalVector(BakeRotationId, bakeSettings.Rotation);
            cmd.SetGlobalVector(BakeTintIntensityId, new Vector4(bakeSettings.Tint.r, bakeSettings.Tint.g, bakeSettings.Tint.b, bakeSettings.Intensity));
            cmd.SetGlobalVector(BakeLowerHemisphereId, bakeSettings.LowerHemisphereEnabled ? new Vector4(bakeSettings.LowerHemisphereColor.r, bakeSettings.LowerHemisphereColor.g, bakeSettings.LowerHemisphereColor.b, bakeSettings.LowerHemisphereColor.a) : Vector4.zero);
            cmd.SetGlobalVector(FilterParamsId, new Vector4(0f, sourceMaxMip, 1f, sourceTexture.width));
            for (var face = 0; face < CubemapFaceCount; face++)
            {
                cmd.SetRenderTarget(new RenderTargetIdentifier(cache.RuntimeSource, 0, (CubemapFace)face));
                cmd.SetViewport(new Rect(0f, 0f, SpecularSize, SpecularSize));
                cmd.SetGlobalVector(FilterFaceMipId, new Vector4(face, 0f, SpecularSize, SpecularMaxMip));
                cmd.SetGlobalMatrix(PixelCoordToViewDirWSId, GetFacePixelCoordToViewDirMatrix(face, SpecularSize));
                cmd.DrawProcedural(Matrix4x4.identity, material, BakeSourcePassIndex, MeshTopology.Triangles, 3, 1);
            }

            cmd.GenerateMips(cache.RuntimeSource);
        }

        private static Matrix4x4 GetFacePixelCoordToViewDirMatrix(int face, int textureSize)
        {
            var safeFace = Mathf.Clamp(face, 0, CubemapFaceCount - 1);
            var safeTextureSize = Mathf.Max(textureSize, 1);
            // Matches XRender/HDUtils cubemap bake rays; the shader samples with -ViewDirWS.
            var lookAt = Matrix4x4.LookAt(Vector3.zero, CubemapLookAtList[safeFace], CubemapUpVectorList[safeFace]);
            var worldToView = lookAt * Matrix4x4.Scale(new Vector3(1f, 1f, -1f));
            var screenSize = new Vector4(safeTextureSize, safeTextureSize, 1f / safeTextureSize, 1f / safeTextureSize);
            return ComputePixelCoordToWorldSpaceViewDirectionMatrix(0.5f * Mathf.PI, Vector2.zero, screenSize, worldToView, true);
        }

        private static Matrix4x4 ComputePixelCoordToWorldSpaceViewDirectionMatrix(float verticalFoV, Vector2 lensShift, Vector4 screenSize, Matrix4x4 worldToViewMatrix, bool renderToCubemap)
        {
            var aspectRatio = screenSize.x * screenSize.w;
            var tanHalfVertFoV = Mathf.Tan(0.5f * verticalFoV);
            var m21 = (1f - 2f * lensShift.y) * tanHalfVertFoV;
            var m11 = -2f * screenSize.w * tanHalfVertFoV;
            var m20 = (1f - 2f * lensShift.x) * tanHalfVertFoV * aspectRatio;
            var m00 = -2f * screenSize.z * tanHalfVertFoV * aspectRatio;

            if (renderToCubemap)
            {
                m11 = -m11;
                m21 = -m21;
            }

            var viewSpaceRasterTransform = new Matrix4x4(
                new Vector4(m00, 0f, 0f, 0f),
                new Vector4(0f, m11, 0f, 0f),
                new Vector4(m20, m21, -1f, 0f),
                new Vector4(0f, 0f, 0f, 1f));

            worldToViewMatrix.SetColumn(3, new Vector4(0f, 0f, 0f, 1f));
            worldToViewMatrix.SetRow(2, -worldToViewMatrix.GetRow(2));
            return Matrix4x4.Transpose(worldToViewMatrix.transpose * viewSpaceRasterTransform);
        }

        private static int SpecularSampleCountForMip(int mip)
        {
            if (mip <= 0)
            {
                return 1;
            }

            switch (mip)
            {
                case 1:
                    return 21;
                case 2:
                    return 34;
                case 3:
                    return 55;
                default:
                    return 89;
            }
        }

        private static string GetFilterStatus(string prefix)
        {
            var shFormat = Cache.DiffuseSHFormat == RenderTextureFormat.Default ? SelectDiffuseSHFormat() : Cache.DiffuseSHFormat;
            return prefix + "v" + FilterVersion + "+XRenderBakeEnvironment+XRenderFaceMatrixMinusViewDir+RuntimeSourceMip0GenerateMips+SourceDirectLOD+SpecularXRenderSmithGGX+SpecMip0Copy+SpecRoughnessUnclamped+FilteredLDDirectRuntime+DiffuseMipFiltered" + DiffuseSampleCount + "NoPi+DiffuseSH9XRender" + DiffuseSHSampleCount + "+DiffuseSHSeamSafe+" + DiffuseSHLayout + "+" + shFormat + "+Numeric:" + GetNumericValidationStatus() + "+SHReadback:" + Cache.GetDiffuseSHValidationStatus();
        }

        private static string GetNumericValidationStatus()
        {
            return "SpecMips=" + SpecularMipCount +
                ",SpecSamples=1/21/34/55/89" +
                ",SpecRoughness=UnclampedXRenderMipInverse" +
                ",Bake=XRenderBakeEnvironment" +
                ",Face=XRenderPixelCoordMinusViewDir" +
                ",DiffuseSamples=" + DiffuseSampleCount +
                ",SHSamples=" + DiffuseSHSampleCount +
                ",RuntimeSource=" + SpecularSize +
                ",DiffuseCube=" + DiffuseSize +
                ",SHLayout=" + DiffuseSHLayout +
                ",FGParity=" + PreIntegratedFGParity +
                ",DiffuseParity=" + DiffuseSHParity;
        }

        public static string ValidatePreIntegratedFGLut(Texture2D lut)
        {
            if (lut == null)
            {
                return "Unavailable";
            }

            if (!lut.isReadable)
            {
                return "GPUOnly(NotReadable)";
            }

            try
            {
                var minValue = float.PositiveInfinity;
                var maxValue = float.NegativeInfinity;
                var maxApproxError = 0f;
                var finite = true;
                AccumulatePreIntegratedFGSample(lut, 0.02f, 0.02f, ref minValue, ref maxValue, ref maxApproxError, ref finite);
                AccumulatePreIntegratedFGSample(lut, 0.25f, 0.25f, ref minValue, ref maxValue, ref maxApproxError, ref finite);
                AccumulatePreIntegratedFGSample(lut, 0.5f, 0.5f, ref minValue, ref maxValue, ref maxApproxError, ref finite);
                AccumulatePreIntegratedFGSample(lut, 0.75f, 0.75f, ref minValue, ref maxValue, ref maxApproxError, ref finite);
                AccumulatePreIntegratedFGSample(lut, 0.98f, 0.98f, ref minValue, ref maxValue, ref maxApproxError, ref finite);
                return "ReadableSamples(finite=" + finite + ",min=" + FormatDiagnosticFloat(minValue) + ",max=" + FormatDiagnosticFloat(maxValue) + ",maxApproxErr=" + FormatDiagnosticFloat(maxApproxError) + ")";
            }
            catch (UnityException ex)
            {
                return "ReadError(" + ex.GetType().Name + ")";
            }
        }

        private static void AccumulatePreIntegratedFGSample(Texture2D lut, float noV, float perceptualRoughness, ref float minValue, ref float maxValue, ref float maxApproxError, ref bool finite)
        {
            var uv = new Vector2(Mathf.Clamp01(noV), 1f - Mathf.Clamp01(perceptualRoughness));
            var halfTexel = new Vector2(0.5f / Mathf.Max(lut.width, 1), 0.5f / Mathf.Max(lut.height, 1));
            uv = new Vector2(Mathf.Lerp(halfTexel.x, 1f - halfTexel.x, uv.x), Mathf.Lerp(halfTexel.y, 1f - halfTexel.y, uv.y));
            var sample = lut.GetPixelBilinear(uv.x, uv.y);
            AccumulatePreIntegratedFGValue(sample.r, ref minValue, ref maxValue, ref finite);
            AccumulatePreIntegratedFGValue(sample.g, ref minValue, ref maxValue, ref finite);
            AccumulatePreIntegratedFGValue(sample.b, ref minValue, ref maxValue, ref finite);
            var approx = PrefilteredDFGApprox(perceptualRoughness, noV);
            maxApproxError = Mathf.Max(maxApproxError, Mathf.Abs(sample.r - approx.x), Mathf.Abs(sample.g - approx.y));
        }

        private static void AccumulatePreIntegratedFGValue(float value, ref float minValue, ref float maxValue, ref bool finite)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                finite = false;
                return;
            }

            minValue = Mathf.Min(minValue, value);
            maxValue = Mathf.Max(maxValue, value);
        }

        private static Vector2 PrefilteredDFGApprox(float roughness, float noV)
        {
            var r0 = -1f * roughness + 1f;
            var r1 = -0.0275f * roughness + 0.0425f;
            var r2 = -0.572f * roughness + 1.04f;
            var r3 = 0.022f * roughness - 0.04f;
            var a004 = Mathf.Min(r0 * r0, Mathf.Pow(2f, -9.28f * Mathf.Clamp01(noV))) * r0 + r1;
            return new Vector2(-1.04f * a004 + r2, 1.04f * a004 + r3);
        }

        private static void RequestDiffuseSHReadback(CommandBuffer cmd, FilterCache cache)
        {
            if (cmd == null || cache == null || cache.DiffuseSH == null || !SystemInfo.supportsAsyncGPUReadback || cache.DiffuseSHReadbackPending)
            {
                return;
            }

            cache.DiffuseSHReadbackPending = true;
            cache.DiffuseSHReadbackRequestedFrame = Time.frameCount;
            var sourceId = cache.SourceId;
            var filterVersion = cache.FilterVersion;
            cmd.RequestAsyncReadback(cache.DiffuseSH, request => CompleteDiffuseSHReadback(cache, sourceId, filterVersion, request));
        }

        private static void CompleteDiffuseSHReadback(FilterCache cache, int sourceId, int filterVersion, AsyncGPUReadbackRequest request)
        {
            if (cache == null)
            {
                return;
            }

            if (cache.SourceId != sourceId || cache.FilterVersion != filterVersion)
            {
                return;
            }

            cache.DiffuseSHReadbackPending = false;
            cache.DiffuseSHReadbackCompletedFrame = Time.frameCount;
            if (request.hasError)
            {
                cache.DiffuseSHReadbackStatus = "Error";
                return;
            }

            var data = request.GetData<Vector4>();
            if (data.Length <= 0)
            {
                cache.DiffuseSHReadbackStatus = "Empty";
                return;
            }

            var finite = true;
            var minValue = float.PositiveInfinity;
            var maxValue = float.NegativeInfinity;
            var maxAbs = 0f;
            for (var i = 0; i < data.Length; i++)
            {
                AccumulateDiffuseSHReadbackValue(data[i].x, ref finite, ref minValue, ref maxValue, ref maxAbs);
                AccumulateDiffuseSHReadbackValue(data[i].y, ref finite, ref minValue, ref maxValue, ref maxAbs);
                AccumulateDiffuseSHReadbackValue(data[i].z, ref finite, ref minValue, ref maxValue, ref maxAbs);
                AccumulateDiffuseSHReadbackValue(data[i].w, ref finite, ref minValue, ref maxValue, ref maxAbs);
            }

            cache.DiffuseSHReadbackStatus = "OK(samples=" + data.Length + ",finite=" + finite + ",min=" + FormatDiagnosticFloat(minValue) + ",max=" + FormatDiagnosticFloat(maxValue) + ",maxAbs=" + FormatDiagnosticFloat(maxAbs) + ")";
        }

        private static void AccumulateDiffuseSHReadbackValue(float value, ref bool finite, ref float minValue, ref float maxValue, ref float maxAbs)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                finite = false;
                return;
            }

            minValue = Mathf.Min(minValue, value);
            maxValue = Mathf.Max(maxValue, value);
            maxAbs = Mathf.Max(maxAbs, Mathf.Abs(value));
        }

        private static string FormatDiagnosticFloat(float value)
        {
            return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static float RoughnessFromReflectionMip(int mip, float maxMip)
        {
            if (mip <= 0)
            {
                return 0f;
            }

            return Mathf.Max(0f, Mathf.Pow(2f, (mip - maxMip + 2f) / 1.2f));
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

        private sealed class FilterCache
        {
            public int SourceId;
            public int FilterVersion;
            public int SourceWidth;
            public int SourceMipCount;
            public int LastFilteredFrame = -1;
            public int LastDynamicSourceVersion;
            public bool Ready;
            public Vector4 SourceHDRDecodeValues = DefaultHDRDecodeValues;
            public BurtImageBasedFilterBakeSettings BakeSettings = BurtImageBasedFilterBakeSettings.Default;
            public RenderTexture RuntimeSource;
            public RenderTexture Specular;
            public RenderTexture Diffuse;
            public RenderTexture DiffuseSH;
            public RenderTextureFormat CubeFormat;
            public RenderTextureFormat DiffuseSHFormat;
            public bool DiffuseSHReadbackPending;
            public int DiffuseSHReadbackRequestedFrame = -1;
            public int DiffuseSHReadbackCompletedFrame = -1;
            public string DiffuseSHReadbackStatus = "Pending";

            public void Release()
            {
                ReleaseTexture(RuntimeSource);
                ReleaseTexture(Specular);
                ReleaseTexture(Diffuse);
                ReleaseTexture(DiffuseSH);
                RuntimeSource = null;
                Specular = null;
                Diffuse = null;
                DiffuseSH = null;
                DiffuseSHFormat = RenderTextureFormat.Default;
                SourceId = 0;
                FilterVersion = 0;
                SourceWidth = 0;
                SourceMipCount = 0;
                SourceHDRDecodeValues = DefaultHDRDecodeValues;
                BakeSettings = BurtImageBasedFilterBakeSettings.Default;
                CubeFormat = RenderTextureFormat.Default;
                LastFilteredFrame = -1;
                LastDynamicSourceVersion = 0;
                Ready = false;
                ResetDiffuseSHReadback();
            }

            public bool MatchesBakeSettings(Vector4 sourceHDRDecodeValues, BurtImageBasedFilterBakeSettings bakeSettings)
            {
                return Approximately(SourceHDRDecodeValues, sourceHDRDecodeValues) &&
                    Approximately(BakeSettings.Rotation, bakeSettings.Rotation) &&
                    Approximately(BakeSettings.Intensity, bakeSettings.Intensity) &&
                    Approximately(BakeSettings.Tint, bakeSettings.Tint) &&
                    BakeSettings.LowerHemisphereEnabled == bakeSettings.LowerHemisphereEnabled &&
                    Approximately(BakeSettings.LowerHemisphereColor, bakeSettings.LowerHemisphereColor);
            }

            public void ResetDiffuseSHReadback()
            {
                DiffuseSHReadbackPending = false;
                DiffuseSHReadbackRequestedFrame = -1;
                DiffuseSHReadbackCompletedFrame = -1;
                DiffuseSHReadbackStatus = SystemInfo.supportsAsyncGPUReadback ? "Pending" : "Unsupported";
            }

            public string GetDiffuseSHValidationStatus()
            {
                if (DiffuseSH == null)
                {
                    return "Unavailable";
                }

                if (DiffuseSHReadbackPending)
                {
                    var age = DiffuseSHReadbackRequestedFrame >= 0 ? Mathf.Max(0, Time.frameCount - DiffuseSHReadbackRequestedFrame) : 0;
                    return "ReadbackPending(age=" + age + ")";
                }

                if (!string.IsNullOrEmpty(DiffuseSHReadbackStatus))
                {
                    var age = DiffuseSHReadbackCompletedFrame >= 0 ? Mathf.Max(0, Time.frameCount - DiffuseSHReadbackCompletedFrame) : 0;
                    return DiffuseSHReadbackStatus + "(age=" + age + ")";
                }

                return "Pending";
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

            private static bool Approximately(float left, float right)
            {
                return Mathf.Abs(left - right) <= 0.0001f;
            }

            private static bool Approximately(Vector4 left, Vector4 right)
            {
                return Approximately(left.x, right.x) &&
                    Approximately(left.y, right.y) &&
                    Approximately(left.z, right.z) &&
                    Approximately(left.w, right.w);
            }

            private static bool Approximately(Color left, Color right)
            {
                return Approximately(left.r, right.r) &&
                    Approximately(left.g, right.g) &&
                    Approximately(left.b, right.b) &&
                    Approximately(left.a, right.a);
            }
        }
    }
}
