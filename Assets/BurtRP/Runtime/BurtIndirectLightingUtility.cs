using System; // 引入 ArgumentException，用来安全兜住 Lighting 自定义反射引用异常。
using System.Text; // 引入 StringBuilder，用来把间接光数据源状态写进 RenderGraph Debug 文本。
using UnityEngine; // 引入 UnityEngine，用来读取 RenderSettings、ReflectionProbe、Texture、Vector4 和 Shader.PropertyToID。
using UnityEngine.Rendering; // 引入 UnityEngine.Rendering，用来使用 CommandBuffer 和 SphericalHarmonicsL2。

namespace Burt.RenderPipeline // 定义 BurtRP 运行时命名空间，让 SetupLightingPass 和 DebugUtility 都能访问这个工具。
{
    internal static class BurtIndirectLightingUtility // 定义 BurtRP 自己的全局间接光数据入口，避免 Deferred Lighting 依赖 Forward DrawRenderers 的副作用。
    {
        private static readonly int AmbientSHArId = Shader.PropertyToID("_BurtAmbientSHAr"); // 缓存 BurtRP 自有 SH R 通道 L0/L1 常量属性 ID。
        private static readonly int AmbientSHAgId = Shader.PropertyToID("_BurtAmbientSHAg"); // 缓存 BurtRP 自有 SH G 通道 L0/L1 常量属性 ID。
        private static readonly int AmbientSHAbId = Shader.PropertyToID("_BurtAmbientSHAb"); // 缓存 BurtRP 自有 SH B 通道 L0/L1 常量属性 ID。
        private static readonly int AmbientSHBrId = Shader.PropertyToID("_BurtAmbientSHBr"); // 缓存 BurtRP 自有 SH R 通道 L2 常量属性 ID。
        private static readonly int AmbientSHBgId = Shader.PropertyToID("_BurtAmbientSHBg"); // 缓存 BurtRP 自有 SH G 通道 L2 常量属性 ID。
        private static readonly int AmbientSHBbId = Shader.PropertyToID("_BurtAmbientSHBb"); // 缓存 BurtRP 自有 SH B 通道 L2 常量属性 ID。
        private static readonly int AmbientSHCId = Shader.PropertyToID("_BurtAmbientSHC"); // 缓存 BurtRP 自有 SH C 项常量属性 ID。
        private static readonly int AmbientSHEnabledId = Shader.PropertyToID("_BurtAmbientSHEnabled"); // 缓存 BurtRP 自有 SH 是否有效的开关属性 ID。
        private static readonly int AmbientLightColorId = Shader.PropertyToID("_BurtAmbientLightColor"); // 缓存 SimpleLit 和 SH fallback 使用的环境光颜色属性 ID。
        private static readonly int SkyReflectionTextureId = Shader.PropertyToID("_BurtSkyReflectionTexture"); // 缓存 BurtRP 全局天空反射 cubemap 属性 ID。
        private static readonly int SkyReflectionHDRId = Shader.PropertyToID("_BurtSkyReflectionHDR"); // 缓存 BurtRP 天空反射 HDR 解码参数属性 ID。
        private static readonly int SkyReflectionIntensityId = Shader.PropertyToID("_BurtSkyReflectionIntensity"); // 缓存 BurtRP 天空反射强度属性 ID。
        private static readonly int SkyReflectionTintId = Shader.PropertyToID("_BurtSkyReflectionTint");
        private static readonly int SkyReflectionEnabledId = Shader.PropertyToID("_BurtSkyReflectionEnabled"); // 缓存 BurtRP 天空反射是否有效的开关属性 ID。
        private static readonly int SkyReflectionOverrideId = Shader.PropertyToID("_BurtSkyReflectionOverride"); // 标记 SkyLight 是否显式接管 specular，避免关闭时回落到 Unity legacy probe。
        private static readonly int SkyReflectionMaxMipId = Shader.PropertyToID("_BurtSkyReflectionMaxMip"); // 缓存 BurtRP 全局天空反射真实最大 mip 属性 ID，shader 会再限制到反射预过滤有效范围。
        private static readonly int SkyReflectionRotationId = Shader.PropertyToID("_BurtSkyReflectionRotation"); // 缓存 SkyLight 指定 cubemap 的水平旋转参数，xy 分别是 cos/sin。
        private static readonly int SkyReflectionSourceTextureId = Shader.PropertyToID("_BurtSkyReflectionSourceTexture");
        private static readonly int SkyReflectionSourceHDRId = Shader.PropertyToID("_BurtSkyReflectionSourceHDR");
        private static readonly int SkyReflectionSourceEnabledId = Shader.PropertyToID("_BurtSkyReflectionSourceEnabled");
        private static readonly int SkyReflectionSourceMaxMipId = Shader.PropertyToID("_BurtSkyReflectionSourceMaxMip");
        private static readonly int SkyDiffuseCubemapTextureId = Shader.PropertyToID("_BurtSkyDiffuseCubemapTexture");
        private static readonly int SkyDiffuseCubemapHDRId = Shader.PropertyToID("_BurtSkyDiffuseCubemapHDR");
        private static readonly int SkyDiffuseCubemapEnabledId = Shader.PropertyToID("_BurtSkyDiffuseCubemapEnabled"); // 缓存 SpecifiedCubemap 是否同时驱动 diffuse 环境光。
        private static readonly int SkyDiffuseCubemapIntensityId = Shader.PropertyToID("_BurtSkyDiffuseCubemapIntensity"); // 缓存 diffuse cubemap 近似采样强度。
        private static readonly int SkyDiffuseCubemapTintId = Shader.PropertyToID("_BurtSkyDiffuseCubemapTint"); // 缓存 diffuse cubemap tint。
        private static readonly int SkyDiffuseCubemapMipId = Shader.PropertyToID("_BurtSkyDiffuseCubemapMip"); // 缓存 diffuse cubemap 近似采样 mip。
        private static readonly int SkyDiffuseSHTextureId = Shader.PropertyToID("_BurtSkyDiffuseSHTexture");
        private static readonly int SkyDiffuseSHEnabledId = Shader.PropertyToID("_BurtSkyDiffuseSHEnabled");
        private static readonly int SkyLowerHemisphereEnabledId = Shader.PropertyToID("_BurtSkyLowerHemisphereEnabled"); // 缓存 SkyLight 下半球覆盖开关。
        private static readonly int SkyLowerHemisphereDiffuseColorId = Shader.PropertyToID("_BurtSkyLowerHemisphereDiffuseColor"); // 缓存下半球 diffuse 覆盖颜色。
        private static readonly int SkyLowerHemisphereSpecularColorId = Shader.PropertyToID("_BurtSkyLowerHemisphereSpecularColor"); // 缓存下半球 specular 覆盖颜色。
        private static readonly int UnitySHArId = Shader.PropertyToID("unity_SHAr"); // 缓存 Unity 内置 SH R 通道 L0/L1 属性 ID，保留给仍调用 ShadeSH9 的 shader 兼容。
        private static readonly int UnitySHAgId = Shader.PropertyToID("unity_SHAg"); // 缓存 Unity 内置 SH G 通道 L0/L1 属性 ID，保留给仍调用 ShadeSH9 的 shader 兼容。
        private static readonly int UnitySHAbId = Shader.PropertyToID("unity_SHAb"); // 缓存 Unity 内置 SH B 通道 L0/L1 属性 ID，保留给仍调用 ShadeSH9 的 shader 兼容。
        private static readonly int UnitySHBrId = Shader.PropertyToID("unity_SHBr"); // 缓存 Unity 内置 SH R 通道 L2 属性 ID，保留给仍调用 ShadeSH9 的 shader 兼容。
        private static readonly int UnitySHBgId = Shader.PropertyToID("unity_SHBg"); // 缓存 Unity 内置 SH G 通道 L2 属性 ID，保留给仍调用 ShadeSH9 的 shader 兼容。
        private static readonly int UnitySHBbId = Shader.PropertyToID("unity_SHBb"); // 缓存 Unity 内置 SH B 通道 L2 属性 ID，保留给仍调用 ShadeSH9 的 shader 兼容。
        private static readonly int UnitySHCId = Shader.PropertyToID("unity_SHC"); // 缓存 Unity 内置 SH C 项属性 ID，保留给仍调用 ShadeSH9 的 shader 兼容。
        private const float ReflectionCaptureSpecularMipMax = 8f; // 和 shader 里的 XRender mip 输入上限保持一致，256 sky 的 mip count 9 对应 max index 8。
        private static readonly Vector4 DefaultSkyReflectionHDR = new Vector4(1f, 1f, 0f, 0f); // 定义默认 HDR 解码参数，表示按原始 cubemap RGB 直接使用。
        private static readonly Vector4 DefaultSkyReflectionRotation = new Vector4(1f, 0f, 0f, 0f); // 默认不旋转 cubemap 采样方向。

        private struct ResolvedSkyReflection // 保存一次全局反射源解析结果，避免纹理、HDR 解码和 Debug 来源三套逻辑互相漂移。
        {
            public Texture Texture; // 当前要上传给 shader 的 cubemap；为空时 shader 会走环境色兜底。
            public Vector4 HDRDecodeValues; // 当前 cubemap 对应的 HDR 解码参数，ReflectionProbe 纹理必须使用它还原亮度。
            public float IntensityMultiplier; // 反射源自身强度，Scene ReflectionProbe 会把 probe.intensity 合并进最终强度。
            public string Source; // Debug 中显示的数据源名称，方便判断使用的是自定义、场景 Probe 还是默认天空反射。
            public Color Tint;
            public bool ForceOverride; // true 表示 SkyLight 已显式接管 specular，即使无纹理也不回退 unity_SpecCube0。
            public Vector4 Rotation; // xy 保存绕世界 Y 轴旋转 cubemap 方向所需的 cos/sin。
        }

        private struct ResolvedLowerHemisphere // 保存 SkyLight 下半球覆盖结果，diffuse/specular 分开预乘各自强度。
        {
            public bool Enabled;
            public Color DiffuseColor;
            public Color SpecularColor;
            public Color BakeColor;
            public string Source;
        }

        private struct ResolvedSkyDiffuseCubemap // 保存 SpecifiedCubemap 驱动 diffuse 的近似采样参数。
        {
            public bool Enabled;
            public Texture Texture;
            public Texture SHTexture;
            public Vector4 HDRDecodeValues;
            public float Intensity;
            public Color Tint;
            public float Mip;
            public string Source;
        }

        private readonly struct IBLDiagnosticSnapshot
        {
            public readonly bool SkyLightActive;
            public readonly string SkyLightSource;
            public readonly string SkyLightName;
            public readonly int SkyLightPriority;
            public readonly bool AffectDiffuse;
            public readonly bool AffectSpecular;
            public readonly float EffectiveDiffuseIntensity;
            public readonly float EffectiveSpecularIntensity;
            public readonly Color Tint;
            public readonly float CubemapAngle;
            public readonly ResolvedSkyReflection SkyReflection;
            public readonly BurtImageBasedFilterResult ImageBasedFilter;
            public readonly ResolvedSkyDiffuseCubemap SkyDiffuseCubemap;
            public readonly ResolvedLowerHemisphere LowerHemisphere;
            public readonly float RenderSettingsReflectionIntensity;
            public readonly string EffectiveDiffuseSource;
            public readonly int SkyReflectionMipCount;

            public IBLDiagnosticSnapshot(
                bool skyLightActive,
                string skyLightSource,
                string skyLightName,
                int skyLightPriority,
                bool affectDiffuse,
                bool affectSpecular,
                float effectiveDiffuseIntensity,
                float effectiveSpecularIntensity,
                Color tint,
                float cubemapAngle,
                ResolvedSkyReflection skyReflection,
                BurtImageBasedFilterResult imageBasedFilter,
                ResolvedSkyDiffuseCubemap skyDiffuseCubemap,
                ResolvedLowerHemisphere lowerHemisphere,
                float renderSettingsReflectionIntensity,
                string effectiveDiffuseSource,
                int skyReflectionMipCount)
            {
                SkyLightActive = skyLightActive;
                SkyLightSource = skyLightSource;
                SkyLightName = skyLightName;
                SkyLightPriority = skyLightPriority;
                AffectDiffuse = affectDiffuse;
                AffectSpecular = affectSpecular;
                EffectiveDiffuseIntensity = effectiveDiffuseIntensity;
                EffectiveSpecularIntensity = effectiveSpecularIntensity;
                Tint = tint;
                CubemapAngle = cubemapAngle;
                SkyReflection = skyReflection;
                ImageBasedFilter = imageBasedFilter;
                SkyDiffuseCubemap = skyDiffuseCubemap;
                LowerHemisphere = lowerHemisphere;
                RenderSettingsReflectionIntensity = renderSettingsReflectionIntensity;
                EffectiveDiffuseSource = effectiveDiffuseSource;
                SkyReflectionMipCount = skyReflectionMipCount;
            }
        }

        public static void UploadGlobalIndirectLighting(CommandBuffer cmd) // 保留旧入口，旧调用没有相机上下文时只能解析全局自定义/默认反射。
        {
            UploadGlobalIndirectLighting(cmd, (Camera)null); // 转发到新入口，让所有上传逻辑保持同一套实现。
        }

        public static void UploadGlobalIndirectLighting(CommandBuffer cmd, Camera camera) // 上传当前帧/当前 request 使用的全局间接光数据。
        {
            UploadGlobalIndirectLighting(cmd, null, camera);
        }

        public static void UploadGlobalIndirectLighting(CommandBuffer cmd, BurtRenderRequest request) // 上传当前 request 使用的全局间接光数据；带 request 时可解析 Burt fullscreen 天空反射源。
        {
            UploadGlobalIndirectLighting(cmd, request, request != null ? request.Camera : null);
        }

        private static void UploadGlobalIndirectLighting(CommandBuffer cmd, BurtRenderRequest request, Camera camera)
        {
            if (cmd == null) // 如果命令缓冲为空，就没有可写入的 GPU 状态。
            {
                return; // 直接返回，避免工具影响渲染主流程。
            }

            var skyLightActive = TryResolveActiveSkyLight(camera, out var skyLight);
            var ambientProbe = skyLightActive ? ResolveSkyLightAmbientProbe(skyLight) : RenderSettings.ambientProbe;
            var skyReflection = skyLightActive ? ResolveSkyLightReflection(cmd, request, camera, skyLight) : ResolveSkyReflection(cmd, request, camera);
            var lowerHemisphere = skyLightActive ? ResolveSkyLightLowerHemisphere(skyLight) : CreateDisabledLowerHemisphere();
            var renderSettingsReflectionIntensity = IsPreviewCamera(camera) ? 1f : Mathf.Max(0f, RenderSettings.reflectionIntensity);
            var skyReflectionIntensity = skyLightActive ? skyReflection.IntensityMultiplier : renderSettingsReflectionIntensity * Mathf.Max(0f, skyReflection.IntensityMultiplier);
            var skyDiffuseIntensity = skyLightActive && skyLight != null && skyLight.affectDiffuse ? skyLight.EffectiveDiffuseIntensity : 0f;
            var imageBasedFilterBakeIntensity = skyLightActive ? Mathf.Max(skyReflectionIntensity, skyDiffuseIntensity) : skyReflectionIntensity;
            var bakeSettings = CreateImageBasedFilterBakeSettings(skyReflection, imageBasedFilterBakeIntensity, lowerHemisphere);
            var imageBasedFilter = BurtImageBasedFilterUtility.Filter(cmd, skyReflection.Texture, skyReflection.HDRDecodeValues, skyReflection.Source, bakeSettings);
            var skyDiffuseCubemap = skyLightActive ? ResolveSkyLightDiffuseCubemap(skyLight, skyReflection, imageBasedFilter) : CreateDisabledSkyDiffuseCubemap();
            var runtimeLowerHemisphere = imageBasedFilter.Filtered ? CreateDisabledLowerHemisphere() : lowerHemisphere;
            var runtimeSkyReflectionIntensity = imageBasedFilter.Filtered ? ResolveBakedIntensityScale(skyReflectionIntensity, imageBasedFilter.BakedIntensity) : skyReflectionIntensity;
            var runtimeSkyReflectionTint = imageBasedFilter.Filtered ? Color.white : skyReflection.Tint;
            var runtimeSkyReflectionRotation = imageBasedFilter.Filtered ? DefaultSkyReflectionRotation : skyReflection.Rotation;

            UploadAmbientProbe(cmd, ambientProbe);
            if (skyLightActive)
            {
                cmd.SetGlobalColor(AmbientLightColorId, ResolveSkyLightAmbientColor(skyLight));
            }

            UploadSkyLowerHemisphere(cmd, runtimeLowerHemisphere);
            UploadSkyDiffuseCubemap(cmd, skyDiffuseCubemap);
            UploadSkyReflection(
                cmd,
                imageBasedFilter.SpecularTexture,
                imageBasedFilter.SpecularHDRDecodeValues,
                imageBasedFilter.SpecularMaxMip,
                skyReflection.Texture,
                skyReflection.HDRDecodeValues,
                CalculateSkyReflectionSpecularMipMax(skyReflection.Texture),
                runtimeSkyReflectionIntensity,
                runtimeSkyReflectionTint,
                skyReflection.ForceOverride,
                runtimeSkyReflectionRotation);
        }

        public static void AppendDebugState(StringBuilder builder) // 保留旧入口，旧调用没有相机上下文时只输出全局自定义/默认反射。
        {
            AppendDebugState(builder, (Camera)null); // 转发到新入口，避免 Debug 文本和上传逻辑分叉。
        }

        public static void AppendDebugState(StringBuilder builder, Camera camera) // 把当前间接光数据源状态写入 RenderGraph Debug。
        {
            AppendDebugState(builder, null, camera);
        }

        public static void AppendDebugState(StringBuilder builder, BurtRenderRequest request) // 把当前 request 的间接光数据源状态写入 RenderGraph Debug。
        {
            AppendDebugState(builder, request, request != null ? request.Camera : null);
        }

        private static void AppendDebugState(StringBuilder builder, BurtRenderRequest request, Camera camera)
        {
            if (builder == null) // 如果没有字符串构建器，就没有安全输出目标。
            {
                return; // 直接返回，保持 Debug 工具空值安全。
            }

            var snapshot = CreateIBLDiagnosticSnapshot(request, camera);

            builder.Append("  SkyLightActive=").Append(snapshot.SkyLightActive);
            builder.Append(" Source=").Append(snapshot.SkyLightSource);
            builder.Append(" Name=").Append(snapshot.SkyLightName);
            builder.Append(" Priority=").Append(snapshot.SkyLightActive ? snapshot.SkyLightPriority.ToString() : "<none>");
            builder.Append(" AffectDiffuse=").Append(snapshot.AffectDiffuse);
            builder.Append(" AffectSpecular=").Append(snapshot.AffectSpecular);
            builder.Append(" DiffuseIntensity=").Append(snapshot.EffectiveDiffuseIntensity.ToString("0.###"));
            builder.Append(" SpecularIntensity=").Append(snapshot.EffectiveSpecularIntensity.ToString("0.###"));
            builder.Append(" Tint=").Append(FormatColor(snapshot.Tint));
            builder.Append(" Cubemap=").Append(snapshot.SkyReflection.Texture != null ? snapshot.SkyReflection.Texture.name : "<none>");
            builder.Append(" CubemapMipCount=").Append(snapshot.SkyReflectionMipCount);
            builder.Append(" CubemapMipHealth=").Append(ResolveSkyReflectionMipHealth(snapshot.SkyReflection.Texture));
            builder.Append(" MaxMip=").Append(CalculateSkyReflectionMaxMip(snapshot.SkyReflection.Texture).ToString("0.###"));
            builder.Append(" CubemapAngle=").Append(snapshot.CubemapAngle.ToString("0.###"));
            builder.Append(" SpecularOverride=").Append(snapshot.SkyReflection.ForceOverride);
            builder.AppendLine();

            builder.Append("  IndirectDiffuseSource=").Append(snapshot.EffectiveDiffuseSource);
            builder.Append(" DiffuseCubemap=").Append(snapshot.SkyDiffuseCubemap.Source);
            builder.Append(" DiffuseCubemapMip=").Append(snapshot.SkyDiffuseCubemap.Mip.ToString("0.###"));
            builder.Append(" DiffuseSH=").Append(snapshot.SkyDiffuseCubemap.Enabled && snapshot.ImageBasedFilter.DiffuseSHTexture != null);
            builder.Append(" DiffuseSHFormat=").Append(snapshot.ImageBasedFilter.DiffuseSHFormat);
            builder.Append(" DiffuseSHSize=").Append(snapshot.ImageBasedFilter.DiffuseSHTexture != null ? snapshot.ImageBasedFilter.DiffuseSHTexture.width + "x" + snapshot.ImageBasedFilter.DiffuseSHTexture.height : "<none>");
            builder.Append(" DiffuseSHLayout=").Append(BurtImageBasedFilterUtility.DiffuseSHLayout);
            builder.Append(" DiffuseSHSamples=").Append(BurtImageBasedFilterUtility.DiffuseSHDiagnosticSampleCount);
            builder.Append(" DiffuseSHParity=").Append(BurtImageBasedFilterUtility.DiffuseSHParity);
            builder.Append(" DiffuseSHValidation=").Append(snapshot.ImageBasedFilter.DiffuseSHTexture != null ? BurtImageBasedFilterUtility.DiffuseSHValidationStatus : "Unavailable");
            builder.Append(" IBLNumericValidation=").Append(BurtImageBasedFilterUtility.NumericValidationStatus);
            builder.Append(" IndirectSpecularSource=").Append(snapshot.SkyReflection.Source);
            builder.Append(" LowerHemisphere=").Append(snapshot.LowerHemisphere.Source);
            builder.Append(" LowerDiffuse=").Append(FormatColor(snapshot.LowerHemisphere.DiffuseColor));
            builder.Append(" LowerSpecular=").Append(FormatColor(snapshot.LowerHemisphere.SpecularColor));
            builder.Append(" ReflectionIntensity=").Append((snapshot.SkyLightActive ? 1f : snapshot.RenderSettingsReflectionIntensity).ToString("0.###"));
            builder.Append(" SourceIntensity=").Append(Mathf.Max(0f, snapshot.SkyReflection.IntensityMultiplier).ToString("0.###"));
            builder.Append(" SkyReflectionMaxMip=").Append(CalculateSkyReflectionMaxMip(snapshot.SkyReflection.Texture).ToString("0.###"));
            builder.Append(" SpecularMipMax=").Append(snapshot.ImageBasedFilter.SpecularMaxMip.ToString("0.###"));
            builder.Append(" ImageBasedFilter=").Append(snapshot.ImageBasedFilter.Status);
            builder.AppendLine();
        }

        public static void AppendXRenderComparisonReport(StringBuilder builder, BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            AppendXRenderComparisonReport(builder, request, request != null ? request.Camera : null, asset);
        }

        public static void AppendXRenderComparisonReport(StringBuilder builder, Camera camera, BurtRenderPipelineAsset asset)
        {
            AppendXRenderComparisonReport(builder, null, camera, asset);
        }

        private static void AppendXRenderComparisonReport(StringBuilder builder, BurtRenderRequest request, Camera camera, BurtRenderPipelineAsset asset)
        {
            if (builder == null)
            {
                return;
            }

            var snapshot = CreateIBLDiagnosticSnapshot(request, camera);
            var fgLut = asset != null ? asset.PreintegratedFGLut : null;
            var sourceName = snapshot.SkyReflection.Texture != null ? snapshot.SkyReflection.Texture.name : "<none>";
            builder.AppendLine("  IBL/XRender Compare:");
            AppendComparisonRow(builder, "SourceCube", sourceName + " mips=" + snapshot.SkyReflectionMipCount + " maxMip=" + FormatFloat(CalculateSkyReflectionMaxMip(snapshot.SkyReflection.Texture)), "Cube source with valid mip chain", ResolveSourceCubeStatus(snapshot.SkyReflection.Texture), "XRender expects cubemap source before LD/SH filtering.");
            AppendComparisonRow(builder, "SpecularLD", FormatTextureSummary(snapshot.ImageBasedFilter.SpecularTexture, snapshot.ImageBasedFilter.SpecularMaxMip, snapshot.ImageBasedFilter.Status), "BakeEnvironment -> Filtered GGX LD cube 256 mips 0..8", snapshot.ImageBasedFilter.Filtered && snapshot.ImageBasedFilter.SpecularTexture != null ? "OK" : "Fallback", "XRender bake rotation/tint/intensity/lower before LD, mip0 CopyTexture, Smith GGX weighted samples 21/34/55/89, runtime samples filtered LD directly.");
            AppendComparisonRow(builder, "DiffuseSH9", FormatDiffuseSummary(snapshot), "UnitySH9 layout from cubemap convolution", ResolveDiffuseStatus(snapshot), "Diffuse path uses SH texture when filtered; otherwise ambientProbe/fallback is visible here.");
            AppendComparisonRow(builder, "PreIntegratedFG", FormatFGLutSummary(fgLut), BurtImageBasedFilterUtility.PreIntegratedFGParity, fgLut != null ? "OK" : "FallbackApprox", BurtImageBasedFilterUtility.ValidatePreIntegratedFGLut(fgLut));
            AppendComparisonRow(builder, "LowerHemisphere", snapshot.LowerHemisphere.Source + " diffuse=" + FormatColor(snapshot.LowerHemisphere.DiffuseColor) + " specular=" + FormatColor(snapshot.LowerHemisphere.SpecularColor), "Preserve/Black/Color split diffuse+specular", snapshot.LowerHemisphere.Enabled ? "Override" : "Preserve", "Burt keeps diffuse/specular lower hemisphere separately.");
            AppendComparisonRow(builder, "IntensityTint", "diffuse=" + FormatFloat(snapshot.EffectiveDiffuseIntensity) + " specular=" + FormatFloat(snapshot.EffectiveSpecularIntensity), "SkyLight intensity/tint baked before convolution", ResolveIntensityStatus(snapshot), "Filtered path bakes tint/intensity into LD/SH; RenderSettings reflection intensity is only used on legacy fallback.");
            AppendComparisonRow(builder, "Numeric", BurtImageBasedFilterUtility.NumericValidationStatus, "Spec/Diffuse/SH/FG constants match XRender policy", "Info", "Use this row as the golden signature when comparing dumps.");
        }

        private static IBLDiagnosticSnapshot CreateIBLDiagnosticSnapshot(BurtRenderRequest request, Camera camera)
        {
            var skyLightActive = TryResolveActiveSkyLight(camera, out var skyLight);
            var skyReflection = skyLightActive ? ResolveSkyLightReflection(null, request, camera, skyLight) : ResolveSkyReflection(null, request, camera);
            var lowerHemisphere = skyLightActive ? ResolveSkyLightLowerHemisphere(skyLight) : CreateDisabledLowerHemisphere();
            var renderSettingsReflectionIntensity = IsPreviewCamera(camera) ? 1f : Mathf.Max(0f, RenderSettings.reflectionIntensity);
            var skyReflectionIntensity = skyLightActive ? skyReflection.IntensityMultiplier : renderSettingsReflectionIntensity * Mathf.Max(0f, skyReflection.IntensityMultiplier);
            var skyDiffuseIntensity = skyLightActive && skyLight != null && skyLight.affectDiffuse ? skyLight.EffectiveDiffuseIntensity : 0f;
            var imageBasedFilterBakeIntensity = skyLightActive ? Mathf.Max(skyReflectionIntensity, skyDiffuseIntensity) : skyReflectionIntensity;
            var imageBasedFilter = CreateDebugImageBasedFilterResult(skyReflection, imageBasedFilterBakeIntensity, lowerHemisphere);
            var skyDiffuseCubemap = skyLightActive ? ResolveSkyLightDiffuseCubemap(skyLight, skyReflection, imageBasedFilter) : CreateDisabledSkyDiffuseCubemap();
            var effectiveDiffuseIntensity = skyLightActive && skyLight.affectDiffuse ? skyLight.EffectiveDiffuseIntensity : 1f;
            var effectiveSpecularIntensity = skyLightActive && skyLight.affectSpecular ? skyLight.EffectiveSpecularIntensity : Mathf.Max(0f, skyReflection.IntensityMultiplier);
            var skyReflectionMipCount = skyReflection.Texture != null ? skyReflection.Texture.mipmapCount : 0;
            var effectiveDiffuseSource = skyDiffuseCubemap.Enabled ? skyDiffuseCubemap.Source : skyLightActive ? ResolveSkyLightDiffuseSource(skyLight) : "RenderSettings.ambientProbe";
            return new IBLDiagnosticSnapshot(
                skyLightActive,
                skyLightActive ? skyLight.sourceType.ToString() : "LegacyRenderSettings",
                skyLightActive ? skyLight.name : "<none>",
                skyLightActive ? skyLight.priority : 0,
                skyLightActive && skyLight.affectDiffuse,
                skyLightActive && skyLight.affectSpecular,
                effectiveDiffuseIntensity,
                effectiveSpecularIntensity,
                skyLightActive ? skyLight.SafeTint : Color.white,
                skyLightActive ? skyLight.cubemapAngle : 0f,
                skyReflection,
                imageBasedFilter,
                skyDiffuseCubemap,
                lowerHemisphere,
                renderSettingsReflectionIntensity,
                effectiveDiffuseSource,
                skyReflectionMipCount);
        }

        private static void AppendComparisonRow(StringBuilder builder, string item, string burt, string xrender, string status, string delta)
        {
            builder.Append("    ").Append(item);
            builder.Append(" | Burt=").Append(burt);
            builder.Append(" | XRender=").Append(xrender);
            builder.Append(" | Status=").Append(status);
            builder.Append(" | Delta=").Append(delta);
            builder.AppendLine();
        }

        private static string FormatTextureSummary(Texture texture, float maxMip, string status)
        {
            if (texture == null)
            {
                return "<none> status=" + status;
            }

            return texture.name + " " + texture.width + "x" + texture.height + " mips=" + texture.mipmapCount + " maxMip=" + FormatFloat(maxMip) + " status=" + status;
        }

        private static string FormatDiffuseSummary(IBLDiagnosticSnapshot snapshot)
        {
            var sh = snapshot.ImageBasedFilter.DiffuseSHTexture;
            var shSize = sh != null ? sh.width + "x" + sh.height : "<none>";
            return snapshot.EffectiveDiffuseSource + " enabled=" + snapshot.SkyDiffuseCubemap.Enabled + " cube=" + FormatTextureName(snapshot.SkyDiffuseCubemap.Texture) + " sh=" + shSize + " layout=" + BurtImageBasedFilterUtility.DiffuseSHLayout + " readback=" + (sh != null ? BurtImageBasedFilterUtility.DiffuseSHValidationStatus : "Unavailable");
        }

        private static string FormatFGLutSummary(Texture2D lut)
        {
            if (lut == null)
            {
                return "FallbackApprox";
            }

            return lut.name + " " + lut.width + "x" + lut.height + " format=" + lut.format + " readable=" + lut.isReadable;
        }

        private static string ResolveSourceCubeStatus(Texture texture)
        {
            if (texture == null)
            {
                return "Missing";
            }

            if (texture.mipmapCount <= 1)
            {
                return "NoMips";
            }

            return texture.mipmapCount - 1 < ReflectionCaptureSpecularMipMax ? "LimitedMips" : "OK";
        }

        private static string ResolveDiffuseStatus(IBLDiagnosticSnapshot snapshot)
        {
            if (!snapshot.SkyDiffuseCubemap.Enabled)
            {
                return snapshot.SkyLightActive && snapshot.AffectDiffuse ? "DisabledByIntensityOrSource" : "Fallback";
            }

            return snapshot.ImageBasedFilter.DiffuseSHTexture != null ? "OK" : "CubeFallback";
        }

        private static string ResolveIntensityStatus(IBLDiagnosticSnapshot snapshot)
        {
            if (snapshot.EffectiveDiffuseIntensity <= 0f && snapshot.EffectiveSpecularIntensity <= 0f)
            {
                return "Zero";
            }

            return "OK";
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string FormatTextureName(Texture texture)
        {
            return texture != null ? texture.name : "<none>";
        }

        private static void UploadAmbientProbe(CommandBuffer cmd, SphericalHarmonicsL2 ambientProbe) // 上传 ambient probe 的 SH 常量。
        {
            var shAr = CreateUnitySHA(ambientProbe, 0); // 计算 R 通道 L0/L1 打包结果。
            var shAg = CreateUnitySHA(ambientProbe, 1); // 计算 G 通道 L0/L1 打包结果。
            var shAb = CreateUnitySHA(ambientProbe, 2); // 计算 B 通道 L0/L1 打包结果。
            var shBr = CreateUnitySHB(ambientProbe, 0); // 计算 R 通道 L2 打包结果。
            var shBg = CreateUnitySHB(ambientProbe, 1); // 计算 G 通道 L2 打包结果。
            var shBb = CreateUnitySHB(ambientProbe, 2); // 计算 B 通道 L2 打包结果。
            var shC = CreateUnitySHC(ambientProbe); // 计算 RGB 三通道共用的 L2 C 打包结果。

            cmd.SetGlobalVector(AmbientSHArId, shAr); // 上传 BurtRP 自有 R 通道 L0/L1 SH。
            cmd.SetGlobalVector(AmbientSHAgId, shAg); // 上传 BurtRP 自有 G 通道 L0/L1 SH。
            cmd.SetGlobalVector(AmbientSHAbId, shAb); // 上传 BurtRP 自有 B 通道 L0/L1 SH。
            cmd.SetGlobalVector(AmbientSHBrId, shBr); // 上传 BurtRP 自有 R 通道 L2 SH。
            cmd.SetGlobalVector(AmbientSHBgId, shBg); // 上传 BurtRP 自有 G 通道 L2 SH。
            cmd.SetGlobalVector(AmbientSHBbId, shBb); // 上传 BurtRP 自有 B 通道 L2 SH。
            cmd.SetGlobalVector(AmbientSHCId, shC); // 上传 BurtRP 自有 SH C 项。
            cmd.SetGlobalFloat(AmbientSHEnabledId, 1f); // 标记 BurtRP 自有 ambient SH 已经上传，可以被 Deferred fullscreen pass 稳定读取。
            cmd.SetGlobalVector(UnitySHArId, shAr); // 同步 Unity 兼容 R 通道 SH，避免仍使用 ShadeSH9 的现有 shader 读到旧值。
            cmd.SetGlobalVector(UnitySHAgId, shAg); // 同步 Unity 兼容 G 通道 SH。
            cmd.SetGlobalVector(UnitySHAbId, shAb); // 同步 Unity 兼容 B 通道 SH。
            cmd.SetGlobalVector(UnitySHBrId, shBr); // 同步 Unity 兼容 R 通道二阶 SH。
            cmd.SetGlobalVector(UnitySHBgId, shBg); // 同步 Unity 兼容 G 通道二阶 SH。
            cmd.SetGlobalVector(UnitySHBbId, shBb); // 同步 Unity 兼容 B 通道二阶 SH。
            cmd.SetGlobalVector(UnitySHCId, shC); // 同步 Unity 兼容 SH C 项。
        }

        private static void UploadSkyLowerHemisphere(CommandBuffer cmd, ResolvedLowerHemisphere lowerHemisphere) // 上传 SkyLight 下半球覆盖参数。
        {
            cmd.SetGlobalFloat(SkyLowerHemisphereEnabledId, lowerHemisphere.Enabled ? 1f : 0f);
            cmd.SetGlobalVector(SkyLowerHemisphereDiffuseColorId, SanitizeColorVector(lowerHemisphere.DiffuseColor));
            cmd.SetGlobalVector(SkyLowerHemisphereSpecularColorId, SanitizeColorVector(lowerHemisphere.SpecularColor));
        }

        private static void UploadSkyDiffuseCubemap(CommandBuffer cmd, ResolvedSkyDiffuseCubemap skyDiffuseCubemap) // 上传 SpecifiedCubemap diffuse 近似采样参数。
        {
            if (skyDiffuseCubemap.Texture != null)
            {
                cmd.SetGlobalTexture(SkyDiffuseCubemapTextureId, skyDiffuseCubemap.Texture);
            }

            if (skyDiffuseCubemap.SHTexture != null)
            {
                cmd.SetGlobalTexture(SkyDiffuseSHTextureId, skyDiffuseCubemap.SHTexture);
            }

            cmd.SetGlobalVector(SkyDiffuseCubemapHDRId, skyDiffuseCubemap.HDRDecodeValues);
            cmd.SetGlobalFloat(SkyDiffuseCubemapEnabledId, skyDiffuseCubemap.Enabled ? 1f : 0f);
            cmd.SetGlobalFloat(SkyDiffuseSHEnabledId, skyDiffuseCubemap.Enabled && skyDiffuseCubemap.SHTexture != null ? 1f : 0f);
            cmd.SetGlobalFloat(SkyDiffuseCubemapIntensityId, Mathf.Max(0f, skyDiffuseCubemap.Intensity));
            cmd.SetGlobalVector(SkyDiffuseCubemapTintId, SanitizeTint(skyDiffuseCubemap.Tint));
            cmd.SetGlobalFloat(SkyDiffuseCubemapMipId, Mathf.Max(0f, skyDiffuseCubemap.Mip));
        }

        private static void UploadSkyReflection(
            CommandBuffer cmd,
            Texture skyReflection,
            Vector4 skyReflectionHDR,
            float skyReflectionMaxMip,
            Texture sourceSkyReflection,
            Vector4 sourceSkyReflectionHDR,
            float sourceSkyReflectionMaxMip,
            float intensity,
            Color tint,
            bool forceOverride,
            Vector4 rotation) // 上传全局天空/场景反射纹理。
        {
            var safeRotation = SanitizeSkyReflectionRotation(rotation); // 没有显式旋转时使用 identity，避免默认 Vector4.zero 把方向压扁。
            UploadSkyReflectionSourceFallback(cmd, sourceSkyReflection, sourceSkyReflectionHDR, sourceSkyReflectionMaxMip);
            if (skyReflection == null) // 如果项目没有可用的天空反射纹理。
            {
                cmd.SetGlobalFloat(SkyReflectionEnabledId, 0f); // 标记天空反射无效，shader 会回退到环境色兜底。
                cmd.SetGlobalFloat(SkyReflectionIntensityId, 0f); // 清零强度，避免 shader 误用上一帧状态。
                cmd.SetGlobalFloat(SkyReflectionMaxMipId, 0f); // 清零 mip 上限，避免 shader 在无 cubemap 时沿用上一帧的 mip 数。
                cmd.SetGlobalVector(SkyReflectionHDRId, DefaultSkyReflectionHDR); // 仍上传默认解码参数，保持 shader 变量稳定。
                cmd.SetGlobalVector(SkyReflectionTintId, Color.white);
                cmd.SetGlobalFloat(SkyReflectionOverrideId, forceOverride ? 1f : 0f);
                cmd.SetGlobalVector(SkyReflectionRotationId, safeRotation);
                return; // 没有纹理时不绑定 samplerCUBE，避免 2D fallback 误绑定到 cube 采样器。
            }

            cmd.SetGlobalTexture(SkyReflectionTextureId, skyReflection); // 绑定当前全局 sky/custom/scene probe reflection cubemap。
            cmd.SetGlobalVector(SkyReflectionHDRId, skyReflectionHDR); // 上传 HDR 解码参数，ReflectionProbe 纹理需要用它还原真实亮度。
            cmd.SetGlobalFloat(SkyReflectionIntensityId, intensity); // 上传反射强度，让 Lighting 面板的 Reflection Intensity 影响 Deferred IBL。
            cmd.SetGlobalVector(SkyReflectionTintId, SanitizeTint(tint));
            cmd.SetGlobalFloat(SkyReflectionEnabledId, intensity > 0f ? 1f : 0f); // 只有有 cubemap 且强度大于 0 时才启用天空反射采样。
            cmd.SetGlobalFloat(SkyReflectionOverrideId, forceOverride ? 1f : 0f);
            cmd.SetGlobalFloat(SkyReflectionMaxMipId, skyReflectionMaxMip); // 上传真实 mip 上限；shader 内部会限制到 reflection capture 的有效预过滤 LOD，避免镜面端变糊。
            cmd.SetGlobalVector(SkyReflectionRotationId, safeRotation);
        }

        private static void UploadSkyReflectionSourceFallback(CommandBuffer cmd, Texture sourceSkyReflection, Vector4 sourceSkyReflectionHDR, float sourceSkyReflectionMaxMip)
        {
            if (sourceSkyReflection != null)
            {
                cmd.SetGlobalTexture(SkyReflectionSourceTextureId, sourceSkyReflection);
                cmd.SetGlobalFloat(SkyReflectionSourceEnabledId, 1f);
                cmd.SetGlobalVector(SkyReflectionSourceHDRId, sourceSkyReflectionHDR);
                cmd.SetGlobalFloat(SkyReflectionSourceMaxMipId, Mathf.Max(0f, sourceSkyReflectionMaxMip));
                return;
            }

            cmd.SetGlobalFloat(SkyReflectionSourceEnabledId, 0f);
            cmd.SetGlobalVector(SkyReflectionSourceHDRId, DefaultSkyReflectionHDR);
            cmd.SetGlobalFloat(SkyReflectionSourceMaxMipId, 0f);
        }


        private static bool TryResolveActiveSkyLight(Camera camera, out BurtSkyLight skyLight)
        {
            skyLight = null;
            if (IsPreviewCamera(camera))
            {
                return false;
            }

            return BurtSkyLight.TryGetActive(out skyLight);
        }

        private static string ResolveSkyLightDiffuseSource(BurtSkyLight skyLight)
        {
            if (skyLight == null || !skyLight.affectDiffuse)
            {
                return "SkyLight.DiffuseDisabled";
            }

            if (skyLight.sourceType == BurtSkyLightSourceType.ConstantColor)
            {
                return "SkyLight.constantColor";
            }

            return skyLight.sourceType == BurtSkyLightSourceType.SpecifiedCubemap && skyLight.cubemap != null ? "SkyLight.SpecifiedCubemapApprox" : "RenderSettings.ambientProbe";
        }

        private static SphericalHarmonicsL2 ResolveSkyLightAmbientProbe(BurtSkyLight skyLight)
        {
            if (skyLight == null || !skyLight.affectDiffuse)
            {
                return default;
            }

            if (skyLight.sourceType == BurtSkyLightSourceType.ConstantColor)
            {
                var sh = new SphericalHarmonicsL2();
                sh.AddAmbientLight(MultiplyColor(skyLight.constantColor, skyLight.SafeTint, skyLight.EffectiveDiffuseIntensity));
                return sh;
            }

            return ScaleSphericalHarmonics(RenderSettings.ambientProbe, skyLight.SafeTint, skyLight.EffectiveDiffuseIntensity);
        }

        private static Color ResolveSkyLightAmbientColor(BurtSkyLight skyLight)
        {
            if (skyLight == null || !skyLight.affectDiffuse)
            {
                return Color.black;
            }

            var sourceColor = skyLight.sourceType == BurtSkyLightSourceType.ConstantColor ? skyLight.constantColor : RenderSettings.ambientLight;
            return MultiplyColor(sourceColor, skyLight.SafeTint, skyLight.EffectiveDiffuseIntensity);
        }

        private static ResolvedSkyReflection ResolveSkyLightReflection(CommandBuffer cmd, BurtRenderRequest request, Camera camera, BurtSkyLight skyLight)
        {
            if (skyLight == null)
            {
                return CreateDisabledSkyReflection(skyLight, "SkyLight.SpecularDisabled");
            }

            switch (skyLight.sourceType)
            {
                case BurtSkyLightSourceType.SpecifiedCubemap:
                    return new ResolvedSkyReflection
                    {
                        Texture = skyLight.cubemap,
                        HDRDecodeValues = DefaultSkyReflectionHDR,
                        IntensityMultiplier = skyLight.cubemap != null && skyLight.affectSpecular ? skyLight.EffectiveSpecularIntensity : 0f,
                        Source = skyLight.cubemap != null ? (skyLight.affectSpecular ? "SkyLight.SpecifiedCubemap" : "SkyLight.SpecifiedCubemapDiffuseOnly") : "SkyLight.SpecifiedCubemapMissing",
                        Tint = skyLight.SafeTint,
                        ForceOverride = true,
                        Rotation = ResolveSkyReflectionRotation(skyLight)
                    };
                case BurtSkyLightSourceType.ConstantColor:
                    return CreateDisabledSkyReflection(skyLight, "SkyLight.ConstantColorSpecularDisabled");
                case BurtSkyLightSourceType.CapturedScene:
                    if (!skyLight.affectSpecular)
                    {
                        return CreateDisabledSkyReflection(skyLight, "SkyLight.SpecularDisabled");
                    }

                    var capturedFallback = ResolveSkyReflection(cmd, request, camera);
                    capturedFallback.Source = "SkyLight.CapturedSceneUnimplemented->" + capturedFallback.Source;
                    capturedFallback.IntensityMultiplier *= skyLight.EffectiveSpecularIntensity;
                    capturedFallback.Tint = skyLight.SafeTint;
                    capturedFallback.ForceOverride = true;
                    capturedFallback.Rotation = DefaultSkyReflectionRotation;
                    return capturedFallback;
                default:
                    if (!skyLight.affectSpecular)
                    {
                        return CreateDisabledSkyReflection(skyLight, "SkyLight.SpecularDisabled");
                    }

                    var renderSettingsReflection = ResolveSkyReflection(cmd, request, camera);
                    renderSettingsReflection.Source = "SkyLight.RenderSettings->" + renderSettingsReflection.Source;
                    renderSettingsReflection.IntensityMultiplier *= skyLight.EffectiveSpecularIntensity;
                    renderSettingsReflection.Tint = skyLight.SafeTint;
                    renderSettingsReflection.ForceOverride = true;
                    renderSettingsReflection.Rotation = DefaultSkyReflectionRotation;
                    return renderSettingsReflection;
            }
        }

        private static ResolvedLowerHemisphere ResolveSkyLightLowerHemisphere(BurtSkyLight skyLight)
        {
            if (skyLight == null || skyLight.lowerHemisphereMode == BurtSkyLightLowerHemisphereMode.Preserve)
            {
                return CreateDisabledLowerHemisphere();
            }

            var sourceColor = skyLight.lowerHemisphereMode == BurtSkyLightLowerHemisphereMode.Black ? Color.black : skyLight.lowerHemisphereColor;
            var blend = skyLight.lowerHemisphereMode == BurtSkyLightLowerHemisphereMode.Black ? 1f : Mathf.Clamp01(sourceColor.a);
            var diffuseIntensity = skyLight.affectDiffuse ? skyLight.EffectiveDiffuseIntensity : 0f;
            var specularIntensity = skyLight.affectSpecular ? skyLight.EffectiveSpecularIntensity : 0f;
            var diffuseBlend = skyLight.affectDiffuse ? blend : 0f;
            var specularBlend = skyLight.affectSpecular ? blend : 0f;

            return new ResolvedLowerHemisphere
            {
                Enabled = diffuseBlend > 0f || specularBlend > 0f,
                DiffuseColor = MultiplyColor(sourceColor, skyLight.SafeTint, diffuseIntensity, diffuseBlend),
                SpecularColor = MultiplyColor(sourceColor, skyLight.SafeTint, specularIntensity, specularBlend),
                BakeColor = new Color(Mathf.Max(0f, sourceColor.r), Mathf.Max(0f, sourceColor.g), Mathf.Max(0f, sourceColor.b), blend),
                Source = skyLight.lowerHemisphereMode.ToString()
            };
        }

        private static ResolvedLowerHemisphere CreateDisabledLowerHemisphere()
        {
            return new ResolvedLowerHemisphere
            {
                Enabled = false,
                DiffuseColor = Color.clear,
                SpecularColor = Color.clear,
                BakeColor = Color.clear,
                Source = "Preserve"
            };
        }

        private static ResolvedSkyDiffuseCubemap ResolveSkyLightDiffuseCubemap(BurtSkyLight skyLight, ResolvedSkyReflection skyReflection, BurtImageBasedFilterResult imageBasedFilter)
        {
            if (skyLight == null || !skyLight.affectDiffuse || imageBasedFilter.DiffuseTexture == null)
            {
                return CreateDisabledSkyDiffuseCubemap();
            }

            var source = ResolveSkyLightDiffuseCubemapSource(skyLight, skyReflection, imageBasedFilter);
            if (string.IsNullOrEmpty(source))
            {
                return CreateDisabledSkyDiffuseCubemap();
            }

            return new ResolvedSkyDiffuseCubemap
            {
                Enabled = skyLight.EffectiveDiffuseIntensity > 0f,
                Texture = imageBasedFilter.DiffuseTexture,
                SHTexture = imageBasedFilter.DiffuseSHTexture,
                HDRDecodeValues = imageBasedFilter.DiffuseHDRDecodeValues,
                Intensity = imageBasedFilter.Filtered ? ResolveBakedIntensityScale(skyLight.EffectiveDiffuseIntensity, imageBasedFilter.BakedIntensity) : skyLight.EffectiveDiffuseIntensity,
                Tint = imageBasedFilter.Filtered ? Color.white : skyLight.SafeTint,
                Mip = imageBasedFilter.DiffuseMip,
                Source = source
            };
        }

        private static string ResolveSkyLightDiffuseCubemapSource(BurtSkyLight skyLight, ResolvedSkyReflection skyReflection, BurtImageBasedFilterResult imageBasedFilter)
        {
            if (skyLight == null)
            {
                return null;
            }

            var suffix = imageBasedFilter.Filtered ? (imageBasedFilter.DiffuseSHTexture != null ? "FilteredIrradianceSH9" : "FilteredIrradiance") : "Approx";
            if (skyLight.sourceType == BurtSkyLightSourceType.SpecifiedCubemap && skyLight.cubemap != null)
            {
                return "SkyLight.SpecifiedCubemap" + suffix;
            }

            if (IsAtmosphereReflectionSource(skyReflection.Source))
            {
                return "BurtAtmosphere" + suffix;
            }

            return null;
        }

        private static bool IsAtmosphereReflectionSource(string source)
        {
            return !string.IsNullOrEmpty(source) && source.IndexOf("BurtAtmosphereCubemap", StringComparison.Ordinal) >= 0;
        }

        private static ResolvedSkyDiffuseCubemap CreateDisabledSkyDiffuseCubemap()
        {
            return new ResolvedSkyDiffuseCubemap
            {
                Enabled = false,
                Texture = null,
                SHTexture = null,
                HDRDecodeValues = DefaultSkyReflectionHDR,
                Intensity = 0f,
                Tint = Color.white,
                Mip = 0f,
                Source = "Disabled"
            };
        }

        private static ResolvedSkyReflection CreateDisabledSkyReflection(BurtSkyLight skyLight, string source)
        {
            return new ResolvedSkyReflection
            {
                Texture = null,
                HDRDecodeValues = DefaultSkyReflectionHDR,
                IntensityMultiplier = 0f,
                Source = source,
                Tint = skyLight != null ? skyLight.SafeTint : Color.white,
                ForceOverride = true,
                Rotation = DefaultSkyReflectionRotation
            };
        }

        private static Color MultiplyColor(Color color, Color tint, float intensity)
        {
            return new Color(Mathf.Max(0f, color.r * tint.r * intensity), Mathf.Max(0f, color.g * tint.g * intensity), Mathf.Max(0f, color.b * tint.b * intensity), 1f);
        }

        private static Color MultiplyColor(Color color, Color tint, float intensity, float alpha)
        {
            return new Color(Mathf.Max(0f, color.r * tint.r * intensity), Mathf.Max(0f, color.g * tint.g * intensity), Mathf.Max(0f, color.b * tint.b * intensity), Mathf.Clamp01(alpha));
        }

        private static BurtImageBasedFilterBakeSettings CreateImageBasedFilterBakeSettings(ResolvedSkyReflection skyReflection, float intensity, ResolvedLowerHemisphere lowerHemisphere)
        {
            var lowerColor = lowerHemisphere.Enabled ? lowerHemisphere.BakeColor : Color.clear;
            return new BurtImageBasedFilterBakeSettings(
                skyReflection.Rotation,
                skyReflection.Tint,
                intensity,
                lowerHemisphere.Enabled,
                lowerColor);
        }

        private static float ResolveBakedIntensityScale(float targetIntensity, float bakedIntensity)
        {
            if (targetIntensity <= 0f || bakedIntensity <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp(targetIntensity / bakedIntensity, 0f, 1024f);
        }

        private static Vector4 SanitizeTint(Color tint)
        {
            return new Vector4(Mathf.Max(0f, tint.r), Mathf.Max(0f, tint.g), Mathf.Max(0f, tint.b), 1f);
        }

        private static Vector4 SanitizeColorVector(Color color)
        {
            return new Vector4(Mathf.Max(0f, color.r), Mathf.Max(0f, color.g), Mathf.Max(0f, color.b), Mathf.Clamp01(color.a));
        }

        private static Vector4 ResolveSkyReflectionRotation(BurtSkyLight skyLight)
        {
            if (skyLight == null || skyLight.sourceType != BurtSkyLightSourceType.SpecifiedCubemap)
            {
                return DefaultSkyReflectionRotation;
            }

            var angleRadians = -Mathf.Deg2Rad * skyLight.cubemapAngle;
            return new Vector4(Mathf.Cos(angleRadians), Mathf.Sin(angleRadians), 0f, 0f);
        }

        private static Vector4 SanitizeSkyReflectionRotation(Vector4 rotation)
        {
            if (Mathf.Abs(rotation.x) < 0.0001f && Mathf.Abs(rotation.y) < 0.0001f)
            {
                return DefaultSkyReflectionRotation;
            }

            var length = Mathf.Sqrt(rotation.x * rotation.x + rotation.y * rotation.y);
            return length > 0.0001f ? new Vector4(rotation.x / length, rotation.y / length, 0f, 0f) : DefaultSkyReflectionRotation;
        }

        private static SphericalHarmonicsL2 ScaleSphericalHarmonics(SphericalHarmonicsL2 source, Color tint, float intensity)
        {
            var result = new SphericalHarmonicsL2();
            var scales = new[] { Mathf.Max(0f, tint.r * intensity), Mathf.Max(0f, tint.g * intensity), Mathf.Max(0f, tint.b * intensity) };
            for (var channel = 0; channel < 3; channel++)
            {
                for (var coefficient = 0; coefficient < 9; coefficient++)
                {
                    result[channel, coefficient] = source[channel, coefficient] * scales[channel];
                }
            }

            return result;
        }

        private static ResolvedSkyReflection ResolveSkyReflection(CommandBuffer cmd, BurtRenderRequest request, Camera camera) // 解析当前 BurtRP 全局反射纹理和 HDR 解码参数。
        {
            if (IsPreviewCamera(camera)) // Inspector 的 Cubemap / ReflectionProbe 预览没有场景语义，不能被当前场景 Probe 或 Lighting Custom Reflection 污染。
            {
                return ResolvePreviewReflectionFallback(); // 预览窗口只使用 Unity 默认反射兜底，避免资产预览被场景 IBL 覆盖。
            }

            if (cmd != null && BurtAtmosphereReflectionUtility.TryGetReflection(cmd, request, out var atmosphereReflection, out var atmosphereHDR, out var atmosphereSource))
            {
                return new ResolvedSkyReflection
                {
                    Texture = atmosphereReflection,
                    HDRDecodeValues = atmosphereHDR,
                    IntensityMultiplier = 1f,
                    Tint = Color.white,
                    Source = atmosphereSource,
                    Rotation = DefaultSkyReflectionRotation
                };
            }

            if (cmd == null && BurtAtmosphereReflectionUtility.HasReadyReflection(request, out var readyAtmosphereReflection, out var readyAtmosphereHDR, out var readyAtmosphereSource))
            {
                return new ResolvedSkyReflection
                {
                    Texture = readyAtmosphereReflection,
                    HDRDecodeValues = readyAtmosphereHDR,
                    IntensityMultiplier = 1f,
                    Tint = Color.white,
                    Source = readyAtmosphereSource,
                    Rotation = DefaultSkyReflectionRotation
                };
            }

            if (camera != null && TryResolveSceneReflectionProbe(camera, out var sceneProbe, out var sceneReflectionTexture)) // ReflectionProbe 是 per-object 覆盖源，Deferred 先选一盏场景 Probe 做全局近似。
            {
                return new ResolvedSkyReflection
                {
                    Texture = sceneReflectionTexture,
                    HDRDecodeValues = sceneProbe.textureHDRDecodeValues,
                    IntensityMultiplier = Mathf.Max(0f, sceneProbe.intensity),
                    Tint = Color.white,
                    Source = "SceneReflectionProbe(" + sceneProbe.name + ")",
                    Rotation = DefaultSkyReflectionRotation
                };
            }

            if (TryResolveCustomReflection(out var customReflection)) // 如果没有可用场景 Probe，再使用 Lighting 面板的 Custom Reflection 作为全局兜底。
            {
                return new ResolvedSkyReflection
                {
                    Texture = customReflection,
                    HDRDecodeValues = DefaultSkyReflectionHDR,
                    IntensityMultiplier = 1f,
                    Tint = Color.white,
                    Source = "RenderSettings.customReflection",
                    Rotation = DefaultSkyReflectionRotation
                };
            }

            if (ReflectionProbe.defaultTexture != null) // 如果 Unity 提供了默认反射纹理。
            {
                return new ResolvedSkyReflection
                {
                    Texture = ReflectionProbe.defaultTexture,
                    HDRDecodeValues = ReflectionProbe.defaultTextureHDRDecodeValues,
                    IntensityMultiplier = 1f,
                    Tint = Color.white,
                    Source = RenderSettings.defaultReflectionMode == DefaultReflectionMode.Custom ? "InvalidCustomReflectionFallback->ReflectionProbe.defaultTexture" : "ReflectionProbe.defaultTexture",
                    Rotation = DefaultSkyReflectionRotation
                }; // 没有自定义或场景 Probe 时使用 Unity 公开的默认反射纹理，避免访问当前版本不存在的 RenderSettings 默认反射属性。
            }

            return new ResolvedSkyReflection
            {
                Texture = null,
                HDRDecodeValues = DefaultSkyReflectionHDR,
                IntensityMultiplier = 0f,
                Tint = Color.white,
                Source = RenderSettings.defaultReflectionMode == DefaultReflectionMode.Custom ? "InvalidCustomReflectionFallback->AmbientColorFallback" : "AmbientColorFallback",
                Rotation = DefaultSkyReflectionRotation
            }; // 没有 cubemap 时写出环境色兜底来源。
        }

        private static bool IsPreviewCamera(Camera camera) // 判断当前 request 是否来自 Unity 编辑器的 Inspector/Asset Preview 相机。
        {
            return camera != null && camera.cameraType == CameraType.Preview; // Preview 相机不应该参与场景 ReflectionProbe 选择，否则会破坏 Cubemap/ReflectionProbe 自身预览。
        }

        private static ResolvedSkyReflection ResolvePreviewReflectionFallback() // 给编辑器预览窗口解析稳定的反射兜底源。
        {
            if (ReflectionProbe.defaultTexture != null) // Unity 默认反射纹理存在时，预览窗口使用它作为低风险 IBL。
            {
                return new ResolvedSkyReflection
                {
                    Texture = ReflectionProbe.defaultTexture,
                    HDRDecodeValues = ReflectionProbe.defaultTextureHDRDecodeValues,
                    IntensityMultiplier = 1f,
                    Tint = Color.white,
                    Source = "PreviewFallback->ReflectionProbe.defaultTexture",
                    Rotation = DefaultSkyReflectionRotation
                };
            }

            return new ResolvedSkyReflection
            {
                Texture = null,
                HDRDecodeValues = DefaultSkyReflectionHDR,
                IntensityMultiplier = 0f,
                Tint = Color.white,
                Source = "PreviewFallback->AmbientColorFallback",
                Rotation = DefaultSkyReflectionRotation
            }; // 没有默认反射纹理时，预览窗口退回环境色，至少不采样场景 Probe。
        }

        private static bool TryResolveCustomReflection(out Texture customReflection) // 安全读取 Lighting 面板里的 Custom Reflection 引用。
        {
            customReflection = null; // 先把输出清空，保证失败路径不会把上一帧数据误传出去。

            if (RenderSettings.defaultReflectionMode != DefaultReflectionMode.Custom) // 只有 Reflection Source 真的选择 Custom 时，才允许访问 customReflection。
            {
                return false; // Skybox 模式下不读取 customReflection，避免 Unity 在引用不是 Cubemap 时抛 ArgumentException。
            }

            try // Unity 在 customReflection 引用非 Cubemap 时会从 getter 直接抛异常，所以必须保护读取。
            {
                customReflection = RenderSettings.customReflectionTexture; // 读取 Lighting 面板中用户配置的自定义反射纹理，避免使用已废弃的 customReflection。
            }
            catch (ArgumentException) // 如果引用不是 Cubemap，说明 Lighting 面板配置无效。
            {
                customReflection = null; // 清空输出，让调用方安全回退到 ReflectionProbe.defaultTexture 或环境色兜底。

                return false; // 返回失败，避免异常继续中断整条渲染管线。
            }

            return customReflection != null; // 只有真正拿到 Cubemap 时才告诉调用方可以使用自定义反射。
        }

        private static bool TryResolveSceneReflectionProbe(Camera camera, out ReflectionProbe selectedProbe, out Texture reflectionTexture) // 从场景中选择一盏可用 ReflectionProbe 作为 Deferred 全局反射兜底。
        {
            selectedProbe = null; // 默认没有选中任何场景 Probe。
            reflectionTexture = null; // 默认没有可上传的 Probe 纹理。

            var probes = UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Exclude, FindObjectsSortMode.None); // 只扫描当前已加载且激活的 ReflectionProbe。
            if (probes == null || probes.Length == 0) // 场景里没有激活 Probe 时直接失败。
            {
                return false; // 交给默认天空反射或环境色兜底。
            }

            var hasCamera = camera != null; // 有相机时可以按相机位置估算最近的 Probe。
            var cameraPosition = hasCamera ? camera.transform.position : Vector3.zero; // 没有相机时只按 importance 排序。
            var bestScore = float.NegativeInfinity; // 分数越高表示越适合作为当前 request 的全局反射源。

            for (var i = 0; i < probes.Length; i++) // 遍历所有激活 Probe，选择 importance 高且靠近当前相机的一盏。
            {
                var probe = probes[i]; // 读取当前候选 Probe。
                if (probe == null || !probe.enabled || probe.gameObject == null || !probe.gameObject.activeInHierarchy) // 跳过禁用或无效 Probe。
                {
                    continue; // 当前 Probe 不参与本次选择。
                }

                var probeTexture = probe.texture; // 读取 Unity 为这盏 Probe 生成或绑定的最终 cubemap。
                if (probeTexture == null) // 没烘焙、没刷新或实时 Probe 尚未产出纹理时不能上传。
                {
                    continue; // 继续检查下一盏 Probe。
                }

                var score = probe.importance * 100000f; // importance 是 Unity Probe blending 的首要意图，这里用大权重优先满足用户配置。
                if (hasCamera) // 有相机上下文时再用距离做次级排序。
                {
                    var probeCenterWS = probe.transform.TransformPoint(probe.center); // 把 Probe 本地中心转换到世界空间。
                    var probeBounds = new Bounds(probeCenterWS, probe.size); // 用 AABB 近似 Probe 影响范围，足够作为全局兜底选择依据。
                    var closestPoint = probeBounds.ClosestPoint(cameraPosition); // 找到相机到 Probe 包围盒的最近点。
                    var distance = Vector3.Distance(cameraPosition, closestPoint); // 相机在盒内时距离为 0。
                    score += probeBounds.Contains(cameraPosition) ? 10000f : 0f; // 相机位于 Probe 内时优先级更高。
                    score -= distance; // 越近越优先。
                }

                if (score <= bestScore) // 如果当前候选不比已有候选更好。
                {
                    continue; // 保留已有选择。
                }

                bestScore = score; // 更新当前最佳分数。
                selectedProbe = probe; // 记录当前最佳 Probe。
                reflectionTexture = probeTexture; // 记录当前最佳 Probe 的 cubemap。
            }

            return selectedProbe != null && reflectionTexture != null; // 只有选到有效 Probe 和纹理时才返回成功。
        }

        private static float CalculateSkyReflectionMaxMip(Texture skyReflection) // 根据纹理资源计算 shader 可用的最大 mip。
        {
            if (skyReflection == null) // 如果没有可用纹理，就没有合法的 mip 链。
            {
                return 0f; // 返回 0，表示 shader 只能走环境色兜底或 legacy 路径。
            }

            return Mathf.Max(0f, skyReflection.mipmapCount - 1f); // Unity 的 mipmapCount 是数量，采样 LOD 上限需要转成最后一个 mip 的索引。
        }

        private static float CalculateSkyReflectionSpecularMipMax(Texture skyReflection) // 根据纹理真实 mip 链推导 shader 实际使用的反射高光 LOD 上限。
        {
            return Mathf.Min(CalculateSkyReflectionMaxMip(skyReflection), ReflectionCaptureSpecularMipMax); // XRender/Unity reflection capture 的预过滤范围按 0..8 处理，不直接使用 512/1024 cubemap 的全部 mip。
        }

        private static BurtImageBasedFilterResult CreateDebugImageBasedFilterResult(ResolvedSkyReflection skyReflection, float intensity, ResolvedLowerHemisphere lowerHemisphere)
        {
            var bakeSettings = CreateImageBasedFilterBakeSettings(skyReflection, intensity, lowerHemisphere);
            return BurtImageBasedFilterUtility.CreateDebugResult(skyReflection.Texture, skyReflection.HDRDecodeValues, skyReflection.Source, bakeSettings);
        }

        private static string ResolveSkyReflectionMipHealth(Texture skyReflection)
        {
            if (skyReflection == null)
            {
                return "<none>";
            }

            if (skyReflection.mipmapCount <= 1)
            {
                return "NoMips";
            }

            return skyReflection.mipmapCount - 1 < ReflectionCaptureSpecularMipMax ? "LimitedMips" : "OK";
        }

        private static string FormatColor(Color color)
        {
            return "(" + color.r.ToString("0.###") + "," + color.g.ToString("0.###") + "," + color.b.ToString("0.###") + "," + color.a.ToString("0.###") + ")";
        }

        private static Vector4 CreateUnitySHA(SphericalHarmonicsL2 sh, int channel) // 创建 UnityCG.cginc 中 unity_SHA* 的打包结果。
        {
            return new Vector4(sh[channel, 3], sh[channel, 1], sh[channel, 2], sh[channel, 0] - sh[channel, 6]); // xyz 对应 L1，w 把 L0 和 L2 常数项合并，匹配 Unity 的 ShadeSH9 约定。
        }

        private static Vector4 CreateUnitySHB(SphericalHarmonicsL2 sh, int channel) // 创建 UnityCG.cginc 中 unity_SHB* 的打包结果。
        {
            return new Vector4(sh[channel, 4], sh[channel, 5], sh[channel, 6] * 3f, sh[channel, 7]); // z 乘 3 是 Unity 内置二阶 SH 打包要求，保证 ShadeSH9 还原方向项正确。
        }

        private static Vector4 CreateUnitySHC(SphericalHarmonicsL2 sh) // 创建 UnityCG.cginc 中 unity_SHC 的打包结果。
        {
            return new Vector4(sh[0, 8], sh[1, 8], sh[2, 8], 1f); // rgb 保存三个颜色通道的第 8 个二阶系数，w 暂未使用但保持为 1。
        }
    }
}
