using UnityEngine; // 引入 UnityEngine，下面需要使用 Shader.PropertyToID 和 Shader.SetGlobalXXX。

namespace Burt.RenderPipeline // 使用 BurtRP 运行时命名空间，让渲染侧和 shader debug 状态处在同一模块。
{
    // 定义 BurtRP 的 shading debug 模式枚举；数值按分组预留，方便后续继续扩展。
    public enum BurtShadingDebugMode
    {
        None = 0, // 不启用 shading debug，正常渲染 BurtRP 画面。
        Albedo = 100, // 材质调试：显示基础颜色，也就是 BaseMap 与 BaseColor 合成后的结果。
        NormalWS = 101, // 材质调试：显示法线贴图影响后的世界空间法线。
        Smoothness = 102, // 材质调试：显示最终光滑度，包含标量 Smoothness 与 Mask Map A 通道。
        Metallic = 103, // 材质调试：显示最终金属度，包含标量 Metallic 与 Mask Map R 通道。
        Occlusion = 104, // 材质调试：显示环境遮蔽，方便检查 Mask Map 的 G 通道和强度混合结果。
        Reflectance = 105, // 材质调试：显示 XRender 风格 reflectance，0.5 会映射到常见非金属 F0=0.04。
        Roughness = 106, // 材质调试：显示材质感知粗糙度，也就是 1 - Smoothness 后的结果。
        SpecularAARoughness = 107, // 材质调试：显示直接高光实际粗糙度，包含 Specular AA 的拓宽结果。
        SpecularEnergyCompensation = 108, // 材质调试：显示直接高光能量补偿。
        SpecularOcclusion = 109, // 材质调试：显示间接高光遮蔽。
        EnergyPreservation = 110, // 材质调试：显示 XRender EnergyPreservation，也就是底层 diffuse 保能比例。
        IndirectSpecularEnergyCompensation = 111, // 材质调试：显示间接高光能量补偿，也就是 Reflection Probe 高光补回的多次散射能量。
        DiffuseColor = 112, // 材质调试：显示 XRender GenericData.DiffuseColor，方便检查 metallic 是否正确扣除 diffuse。
        Height = 113, // Material debug: Mask Map B height channel.
        DirectBRDFD = 115, // 材质调试：显示直接光 GGX D 项，用来检查高 smoothness 时的 NDF 峰值。
        DirectBRDFVisibility = 116, // 材质调试：显示直接光 Smith Joint Visibility，用来排查几何遮蔽是否压暗高光。
        DirectBRDFFresnel = 117, // 材质调试：显示直接光 Schlick Fresnel，用来检查 F0 和视角输入。
        DirectDiffuseLobe = 118, // 材质调试：显示直接光 diffuse lobe，当前默认 Lambert，后续可切 XRender Burley。
        DirectDiffuseBRDF = 119, // 材质调试：显示未乘灯光颜色、NdotL 和阴影的直接 diffuse BRDF。
        DirectSpecularBRDF = 120, // 材质调试：显示未乘灯光颜色、NdotL 和阴影的直接 specular BRDF。
        SpecularAANormalVariance = 121, // 材质调试：显示 XRender Normal Filtering 估算出的屏幕空间法线方差。
        SpecularAARoughnessDelta = 122, // 材质调试：显示 Specular AA 额外增加的感知粗糙度。
        IndirectSpecularDFG = 123, // 材质调试：显示间接高光使用的 PreIntegratedFG DFG.xy。
        IndirectSpecularEnvBRDF = 124, // 材质调试：显示 F0/F90 套用 DFG 后的环境 BRDF。
        SubsurfaceProfileId = 125, // Material debug: resolved Subsurface profile slot.
        SubsurfaceTransmission = 126, // Material debug: profile-driven Subsurface transmission color.
        SubsurfaceKernelWeight = 127, // Material debug: profile-driven Subsurface kernel center weight.
        SubsurfaceIndirect = 128, // Material debug: SH / ambient Subsurface contribution.
        GBufferBaseColor = 130, // GBuffer 调试：显示按 BurtGBuffer 约定编码再解码后的 BaseColor。
        GBufferNormalWS = 131, // GBuffer 调试：显示按 octahedron 编码再解码后的世界空间向量槽。
        GBufferMetallic = 132, // GBuffer 调试：显示 GBuffer 材质通道；Default Lit=metallic，Hair=scatter。
        GBufferSmoothness = 133, // GBuffer 调试：显示 GBuffer 解码后的 Smoothness，后续 Deferred 再从它还原 Roughness。
        GBufferOcclusion = 134, // GBuffer 调试：显示 GBuffer 解码后的 Ambient Occlusion。
        GBufferReflectance = 135, // GBuffer 调试：显示 GBuffer 解码后的 XRender Reflectance。
        GBufferRoughness = 136, // GBuffer 调试：显示从 GBuffer Smoothness 还原出的 XRender Base.Roughness。
        GBufferDiffuseColor = 137, // GBuffer 调试：显示从 GBuffer 还原 PBRMaterialData 后的 DiffuseColor。
        GBufferHairStrandDirection = 138, // GBuffer 调试：只显示 Hair 像素复用 GBuffer1.rg 存储的 strand direction。
        GBufferHairScatter = 139, // GBuffer 调试：只显示 Hair 像素复用 GBuffer1.b material channel 存储的 scatter。
        GBufferHairShift = 140, // GBuffer 调试：只显示 Hair 像素复用 GBuffer1.b material channel 存储的 longitudinal shift scale。
        GBufferClearCoatMask = 141, // GBuffer debug: Clear Coat mask stored in GBuffer3.b.
        GBufferSubsurfaceStrength = 142, // GBuffer 调试：只显示 Subsurface 像素复用 GBuffer1.b material channel 存储的 strength。
        GBufferClearCoatNormalWS = 143,
        GBufferClearCoatRoughness = 144, // GBuffer debug: Clear Coat top-layer roughness stored in GBuffer3.a.
        GBufferAnisotropy = 145, // GBuffer debug: signed anisotropy stored in GBuffer4.b.
        GBufferTangentWS = 146, // GBuffer debug: base tangent stored in GBuffer4.rg as octahedral direction.
        GBufferSubsurfaceThickness = 147, // GBuffer debug: Subsurface thickness stored in GBuffer4.a.
        GBufferSubsurfaceProfileIndex = 148, // GBuffer debug: Subsurface profile index packed with thickness in GBuffer4.a.
        DetailLighting = 200, // 光照调试：参考 XRender Detail Lighting，用 0.18 中灰 BaseColor 重新计算光照，方便只看明暗细节。
        IndirectLighting = 201, // 光照调试：只显示 PBR 间接光，方便检查 SH 漫反射和 Reflection Probe 镜面反射。
        DirectDiffuse = 202, // 光照调试：只显示直接漫反射，方便检查 NdotL、阴影和 1/PI。
        DirectSpecular = 203, // 光照调试：只显示直接高光，方便排查 smoothness 拉满后的高光宽度。
        IndirectDiffuse = 204, // 光照调试：只显示间接漫反射，方便检查 SH / Light Probe。
        IndirectSpecular = 205, // 光照调试：只显示间接高光，方便检查 Reflection Probe 和 DFG。
        ShadowAttenuation = 206, // 光照调试：只显示主光阴影衰减，白色表示不被阴影遮挡。
        AmbientOcclusion = 207, // 光照调试：只显示当前参与间接光遮蔽的 AO 输入。
        Emission = 208, // 光照调试：只显示自发光贡献，方便确认 GBuffer / Forward 是否正确叠加 emission。
        FinalLighting = 209, // 光照调试：显示写入 CameraColor 前的最终材质光照，包含 PBR 光照和自发光。
        HairPrimaryLobe = 210, // Hair 调试：显示 R/Primary 高光 lobe，非 Hair 材质为黑。
        HairSecondaryLobe = 211, // Hair 调试：显示 TT/Secondary 彩色高光 lobe，非 Hair 材质为黑。
        HairTransmissionLobe = 212, // Hair 调试：显示当前近似 backlit/transmission lobe，非 Hair 材质为黑。
        HairScatter = 213, // Hair 调试：显示参与 lighting 的 scatter 强度，非 Hair 材质为黑。
        ShadowCascadeIndex = 214, // 阴影调试：用颜色显示当前像素命中的主光 CSM cascade。
        ShadowCascadeBlend = 215, // 阴影调试：显示 cascade 边界混合权重，白色表示正在混合到下一级 cascade。
        ShadowDistanceFade = 216, // 阴影调试：显示最后一级 cascade 的远距离 fade，白色表示已经淡出到无阴影。
        ShadowPCSSRadius = 217, // 阴影调试：显示当前像素估算出的 PCSS 半影半径。
        ShadowReceiverDepthDelta = 218, // 阴影调试：0.5 灰表示 receiver/shadow depth 基本对齐，亮表示 acne 压力，暗表示 bias 过强。
        ShadowPCSSBlockerFraction = 225, // 阴影调试：显示 PCSS blocker search 命中的 blocker 样本占比。
        AdditionalLighting = 219, // 光照调试：只显示追加光直接光，不包含主光、间接光和自发光。
        AdditionalDiffuse = 220, // 光照调试：只显示追加光漫反射，方便检查多光源 NdotL 和距离衰减。
        AdditionalSpecular = 221, // 光照调试：只显示追加光高光，方便检查多光源镜面贡献。
        HairAdditionalLighting = 222, // Hair 调试：只显示 Hair 像素的追加光直接光，非 Hair 材质为黑。
        TileLightCount = 223, // Tiled light debug: full-screen per-tile additional light count.
        TileLightOccupancy = 224, // Tiled light debug: per-tile list occupancy versus max lights per tile.
        ClusterLightCount = 232, // Clustered light debug: 3D per-cluster additional light count.
        ClusterLightOccupancy = 233, // Clustered light debug: 3D cluster list occupancy.
        AdditionalShadowAttenuation = 226, // 阴影调试：只显示追加光阴影衰减，白色表示当前追加光未被阴影遮挡。
        AdditionalLightingUnshadowed = 227, // 光照调试：显示追加光不乘 additional shadow attenuation 时的直接光贡献。
        AdditionalShadowFace = 228,
        AdditionalShadowUV = 229,
        AdditionalShadowDepth = 230,
        AdditionalShadowDepthDelta = 231,
        CameraDepth = 300, // 全屏调试：复用 BurtRP 当前已有的 CameraDepth debug pass。
        MainLightShadow = 301, // 全屏调试：复用 BurtRP 当前已有的 MainLightShadow debug pass。
        ScreenSpaceAmbientOcclusionRaw = 302, // SSAO debug: raw visibility before blur and final power/intensity curve.
        ScreenSpaceAmbientOcclusionFinal = 303, // SSAO debug: final power/intensity-curved AO texture consumed by deferred lighting.
        ScreenSpaceAmbientOcclusionOverlay = 304, // SSAO debug: final AO multiplied over the current camera color.
        BloomPrefilter = 305, // Bloom debug: prefilter output before the downsample/blur chain.
        ScreenSpaceAmbientOcclusionHistory = 306, // SSAO debug: previous-frame temporal AO history.
        ScreenSpaceAmbientOcclusionDifference = 307, // SSAO debug: absolute current final AO versus temporal history difference.
        ScreenSpaceAmbientOcclusionDepthValidity = 308, // SSAO debug: temporal reprojection depth validity mask.
        ScreenSpaceReflectionRawHitMask = 309, // SSR debug: raw raymarch hit before resolve filters.
        ScreenSpaceReflectionHitMask = 310, // SSR 调试：显示最终参与 composite 的屏幕空间反射遮罩。
        ScreenSpaceReflectionHitUV = 311, // SSR 调试：显示命中位置的屏幕 UV，方便检查方向和翻转。
        ScreenSpaceReflectionStepCount = 312, // SSR 调试：显示 raymarch 步数，方便观察性能和提前退出。
        ScreenSpaceReflectionColor = 313, // SSR 调试：显示命中后采样到的反射颜色。
        ScreenSpaceReflectionConfidence = 314, // SSR debug: final trace confidence / composite alpha.
        ScreenSpaceReflectionDepthDelta = 315, // SSR debug: ray depth versus scene depth delta.
        ScreenSpaceReflectionWorldError = 316, // SSR debug: world-space hit distance from reflection ray.
        ScreenSpaceReflectionDenoisedColor = 317, // SSR debug: spatial denoise output.
        ScreenSpaceReflectionTemporalColor = 318, // SSR debug: temporal accumulation output.
        ScreenSpaceReflectionResolveAlpha = 319, // SSR debug: final alpha used by composite.
        TemporalAAHistory = 320, // TAA debug: reprojected history color.
        TemporalAAFeedback = 321, // TAA debug: final history feedback weight.
        TemporalAARejection = 322, // TAA debug: luma/clip/depth-normal pass weights; bright means stable/accepted.
        TemporalAAHistoryUV = 323, // TAA debug: history UV and in-bounds state.
        TemporalAADifference = 324, // TAA debug: current frame versus history difference.
        TemporalAAVelocity = 325, // TAA debug: reprojection velocity, gray means zero motion.
        TemporalAAConfidence = 326, // TAA debug: accumulated history confidence.
        TemporalAACurrentDepth = 327, // TAA debug: TAA-owned current depth texture.
        TemporalAADepthHistory = 328, // TAA debug: previous-frame depth history.
        TemporalAADepthDelta = 329, // TAA debug: current/history depth disagreement.
        TemporalAACurrentColor = 330, // TAA debug: jittered current color before resolve.
        TemporalAAResolvedColor = 331, // TAA debug: resolved TAA output before history update.
        TemporalAARawVelocity = 332, // TAA debug: raw camera/object velocity before dilation.
        TemporalAAUpdatedConfidence = 333, // TAA debug: current-frame confidence after validity update.
        TemporalAAStaticRelax = 334, // TAA debug: static edge relaxation applied to rejection.
        TemporalAALumaRejection = 335, // TAA debug: luma rejection contribution.
        TemporalAAClipRejection = 336, // TAA debug: color box / variance clip rejection contribution.
        TemporalAADepthRejection = 337, // TAA debug: depth and depth-range rejection contribution.
        TemporalAANormalRejection = 338, // TAA debug: GBuffer normal edge rejection contribution.
        TemporalAAMotionRejection = 339, // TAA debug: velocity-length rejection contribution.
        TemporalAAConfidenceGate = 340, // TAA debug: history confidence gate and boost.
        TemporalAAVelocitySource = 341, // TAA debug: raw velocity source, object motion vectors highlight in white.
        TemporalAAGBufferNormal = 342, // TAA debug: decoded Deferred GBuffer normal sampled by TAA.
        TemporalAAParallaxRejection = 343, // TAA debug: XRender-style parallax/depth history validity mask.
        TemporalAAAntiFlicker = 344, // TAA debug: persistent anti-flicker luma history.
        TemporalAAHistoryCoverage = 345, // TAA debug: history coverage / PrevUseCount-style reuse validity.
        TemporalAAResponsiveMask = 346, // TAA debug: responsive mask that lowers history feedback.
        ScreenSpaceReflectionVisibilityAlpha = 347, // SSR debug: resolved visibility before material Fresnel/roughness weighting.
        ScreenSpaceReflectionMaterialWeight = 348, // SSR debug: material Fresnel/roughness/intensity weight applied during composite.
        ScreenSpaceReflectionRoughnessMip = 349, // SSR debug: roughness-selected mip level used for reflection color.
        ScreenSpaceReflectionResolvedColor = 350, // SSR debug: final roughness-resolved reflection color before alpha blend.
        ScreenSpaceReflectionDepthQuality = 351, // SSR debug: trace depth quality after softened thickness gate.
        ScreenSpaceReflectionWorldQuality = 352, // SSR debug: trace world-space ray distance quality.
        ScreenSpaceReflectionResolveQuality = 353, // SSR debug: combined trace quality before denoise/temporal resolve.
        ScreenSpaceReflectionSurfaceSupport = 354, // SSR debug: same-surface support around the traced hit point.
        ScreenSpaceReflectionHiZSkipCandidate = 355, // SSR debug: raw HiZ empty-space skip candidate without affecting the trace.
        ScreenSpaceReflectionHiZMipLevel = 356, // SSR debug: HiZ mip level that would be considered for skip diagnostics.
        ScreenSpaceReflectionHiZDivergence = 357, // SSR debug: raw HiZ skip candidate rejected by mip0 guard.
        ScreenSpaceReflectionHiZMissedHits = 358, // SSR debug: red marks stable mip0 hits missed by the candidate HiZ skip trace.
        ScreenSpaceReflectionHiZRawHitMiss = 359, // SSR debug: red marks raw trace hits lost by the candidate HiZ skip trace.
        ScreenSpaceReflectionHiZResolvedHitMiss = 360, // SSR debug: red marks resolved trace hits lost by the candidate HiZ skip trace.
        ScreenSpaceReflectionHiZVisibilityMiss = 361, // SSR debug: red marks visible SSR pixels lost after candidate HiZ visibility weighting.
        ScreenSpaceReflectionHiZSkipUsed = 362, // SSR debug: candidate HiZ trace pixels that actually skipped at least one cell.
        ScreenSpaceReflectionHiZProbeBlocked = 363, // SSR debug: candidate HiZ skip attempts blocked by local mip0 raw-hit proof.
        ScreenSpaceReflectionHiZStepCompare = 364, // SSR debug: green means candidate used fewer normalized steps, red means more, blue means near equal.
        TemporalAARejectionReasons = 365, // TAA debug: RGB rejection causes, with magenta highlighting clamp pressure.
        TemporalAAFeedbackWeight = 367, // TAA debug: heatmap of the final history feedback weight.
        TemporalAAPrevUseCount = 376, // TAA debug: raw PrevUseCount RT, where 1 reuse maps to mid gray.
        ScreenSpaceReflectionHiZWorkCompare = 366, // SSR debug: green means candidate has lower estimated work including proof probes, red means higher.
        ScreenSpaceReflectionHiZStepSaved = 368, // SSR debug: production guarded HiZ saved normalized ray steps versus stable mip0, green means saved and red means regression.
        BloomFinalBloom = 369, // Bloom debug: final bloom texture after the upsample/combine chain.
        BloomMip1 = 370, // Bloom debug: intermediate mip 1.
        BloomMip2 = 371, // Bloom debug: intermediate mip 2.
        BloomMip3 = 372, // Bloom debug: intermediate mip 3.
        BloomMip4 = 373, // Bloom debug: intermediate mip 4.
        BloomMip5 = 374, // Bloom debug: intermediate mip 5.
        BloomAlpha = 375, // Bloom debug: alpha channel generated by the bloom chain.
        BloomThresholdMask = 377, // Bloom debug: threshold and soft-knee mask before the blur chain.
        Atmosphere = 378, // Atmosphere debug: compact Rayleigh/Mie/transmittance channels from the sky pass.
        AtmosphereRayleigh = 379, // Atmosphere debug: sky Rayleigh inscattering.
        AtmosphereMie = 380, // Atmosphere debug: sky Mie forward scattering.
        AtmosphereTransmittance = 381, // Atmosphere debug: sky optical transmittance.
        AtmosphereAerialTransmittance = 382, // Atmosphere debug: aerial perspective transmittance over opaque geometry.
        AtmosphereAerialInscatter = 383, // Atmosphere debug: aerial perspective inscattering over opaque geometry.
        AtmosphereAerialFogAmount = 391, // Atmosphere debug: aerial perspective final fog opacity.
        AtmosphereAerialHeightFade = 392, // Atmosphere debug: aerial perspective height fade.
        AtmosphereAerialSummary = 393, // Atmosphere debug: aerial fog amount, height fade, and extinction summary.
        AtmosphereSunDisk = 403, // Atmosphere debug: compact sun disk mask from the sky pass.
        AtmosphereSunHalo = 404, // Atmosphere debug: wider Mie sun halo from the sky pass.
        AtmosphereHorizon = 405, // Atmosphere debug: horizon blend weight used by the sky pass.
        AtmosphereGroundBlend = 406, // Atmosphere debug: below-horizon ground contribution blend.
        AtmosphereViewDirection = 407, // Atmosphere debug: reconstructed sky view direction encoded as RGB.
        ScreenSpaceSubsurfaceSetup = 408, // 5S debug: setup mask, strength, and profile slice.
        ScreenSpaceSubsurfaceTileMask = 409, // 5S debug: low-resolution tile activity mask.
        ScreenSpaceSubsurfaceBlur = 410, // 5S debug: blurred Subsurface color before combine.
        ScreenSpaceSubsurfaceCombine = 411, // 5S debug: combined color before copying to CameraColor.
        ScreenSpaceSubsurfaceThickness = 412, // 5S debug: decoded thickness from setup / GBuffer data.
        ScreenSpaceSubsurfaceProfileIndex = 413, // 5S debug: resolved subsurface profile slot.
        ScreenSpaceSubsurfaceTransmission = 414, // 5S debug: profile transmission contribution.
        ScreenSpaceSubsurfaceDiffuse = 415, // 5S debug: diffuse lighting source used by the blur.
        ScreenSpaceSubsurfaceSpecular = 416, // 5S debug: non-diffuse source preserved by the combine.
        ScreenSpaceSubsurfaceStability = 417, // 5S debug: depth/normal stability gate used by the blur.
        ScreenSpaceSubsurfaceSampleCount = 418, // 5S debug: adaptive Burley sample count stored in history.
        ScreenSpaceSubsurfaceVariance = 419, // 5S debug: history variance used to choose adaptive samples.
        ScreenSpaceSubsurfaceHistory = 420, // 5S debug: history validity, age, and residual summary.
        ScreenSpaceSubsurfaceMask = 421, // 5S debug: full-resolution stencil-derived surface mask.
        FogAmount = 394, // Fog debug: final screen-space fog opacity.
        FogTransmittance = 395, // Fog debug: screen-space fog transmittance after height and distance gates.
        FogHeight = 396, // Fog debug: reconstructed world height relative to the fog height plane.
        FogDistance = 397, // Fog debug: reconstructed camera-to-surface distance used by the fog pass.
        VolumetricFogScattering = 398, // Volumetric fog debug: accumulated single-scattering contribution.
        VolumetricFogTransmittance = 399, // Volumetric fog debug: raymarched transmittance.
        VolumetricFogDensity = 400, // Volumetric fog debug: average and peak sampled density.
        VolumetricFogDistance = 401, // Volumetric fog debug: raymarch distance versus visible distance.
        VolumetricFogStepCount = 402, // Volumetric fog debug: normalized raymarch step count.
        AutoExposureLuminance = 384, // Auto exposure debug: log luminance heatmap from current CameraColor.
        AutoExposureMeteringWeight = 385, // Auto exposure debug: center-weighted metering mask used by lightweight histogram.
        AutoExposureHistogramRange = 386, // Auto exposure debug: current histogram EV range and out-of-range colors.
        ScreenSpaceAmbientOcclusionSurfaceStability = 387, // SSAO debug: current-frame depth/normal stability used to gate temporal history.
        ScreenSpaceAmbientOcclusionDiagnosticCompare = 388, // SSAO debug: quadrant compare for raw/final/history/difference with temporal gates.
        ScreenSpaceGlobalIlluminationRaw = 389, // XGI debug: raw screen-space diffuse GI before the depth/normal bilateral blur.
        ScreenSpaceGlobalIlluminationFinal = 390, // XGI debug: blurred diffuse GI texture consumed by composite.
        ScreenSpaceGlobalIlluminationHitRatio = 422, // XGI debug: trace hit confidence in alpha, with sky fallback shown as low values.
        ScreenSpaceGlobalIlluminationOverlay = 423, // XGI debug: final diffuse GI added over the current camera color.
        ScreenSpaceGlobalIlluminationComposite = 424 // XGI debug: isolated final composite contribution.
    }

    // 保存 Editor Overlay 和运行时渲染共享的 shading debug 状态。
    public static class BurtShadingDebugSettings
    {
        public const string ModeShaderName = "_BurtShadingDebugMode"; // 定义 shader 侧读取 debug 模式的全局属性名。
        public const string EnabledShaderName = "_BurtShadingDebugEnabled"; // 定义 shader 侧读取 debug 是否开启的全局属性名。
        public const string KeywordName = "BURT_SHADING_DEBUG";

        private static readonly int ModeShaderId = Shader.PropertyToID(ModeShaderName); // 缓存模式属性 ID，避免每帧字符串查找。
        private static readonly int EnabledShaderId = Shader.PropertyToID(EnabledShaderName); // 缓存开关属性 ID，避免每帧字符串查找。
        private static BurtShadingDebugMode currentMode = BurtShadingDebugMode.None; // 保存当前 debug 模式，默认关闭。
        private static BurtShadingDebugMode previousMode = BurtShadingDebugMode.None; // 保存上一个 debug 模式，方便后续做返回上次模式或切换统计。

        public static BurtShadingDebugMode Mode // 暴露当前 debug 模式，Editor UI 和渲染侧都通过它读写状态。
        {
            get => currentMode; // 返回当前保存的 debug 模式。
            set // 设置新的 debug 模式，并立刻同步到 shader 全局参数。
            {
                if (currentMode == value) // 如果模式没有变化，说明只是需要刷新全局 shader 参数。
                {
                    ApplyGlobalShaderProperties(); // 重新上传全局参数，避免域重载或相机切换后 shader 状态丢失。
                    return; // 提前返回，避免重复写 previousMode。
                }

                previousMode = currentMode; // 记录切换前的模式，方便后续扩展返回上一个模式。
                currentMode = value; // 保存新的当前模式。
                ApplyGlobalShaderProperties(); // 把新的模式同步给 shader 全局参数。
            }
        }

        public static BurtShadingDebugMode PreviousMode => previousMode; // 暴露上一个模式，当前最小版本暂时只作为状态记录。

        public static bool IsDebugging => currentMode != BurtShadingDebugMode.None; // 只要不是 None，就认为 debug 已开启。

        public static bool UseTileLightCpuDebugColorTextureFallback { get; set; } // Diagnostic fallback for tile-light debug; StructuredBuffer remains the default path.

        private static int tileLightDebugMaxLightsPerTile = BurtLightingData.MaxAdditionalLights; // Debug-only tile-list capacity override; normal lighting still uses MaxAdditionalLights.

        public static int TileLightDebugMaxLightsPerTile
        {
            get => tileLightDebugMaxLightsPerTile;
            set => tileLightDebugMaxLightsPerTile = Mathf.Clamp(value, 1, BurtLightingData.MaxAdditionalLights);
        }

        public static bool IsMainLightShadowDebugMode(BurtShadingDebugMode mode) // 统一判断主光阴影相关调试模式，方便日志和阴影诊断共享同一套入口。
        {
            return mode == BurtShadingDebugMode.ShadowAttenuation
                || (mode >= BurtShadingDebugMode.ShadowCascadeIndex && mode <= BurtShadingDebugMode.ShadowReceiverDepthDelta)
                || mode == BurtShadingDebugMode.ShadowPCSSBlockerFraction
                || mode == BurtShadingDebugMode.MainLightShadow;
        }

        public static void ApplyGlobalShaderProperties() // 把当前 shading debug 状态上传给 shader。
        {
            Shader.SetGlobalInt(ModeShaderId, (int)currentMode); // 上传整数模式 ID，后续 shader 可以 switch 或 if 判断。
            Shader.SetGlobalFloat(EnabledShaderId, IsDebugging ? 1f : 0f); // 上传 0/1 开关，方便 shader 快速判断是否走调试分支。
            if (IsDebugging)
            {
                Shader.EnableKeyword(KeywordName);
            }
            else
            {
                Shader.DisableKeyword(KeywordName);
            }
        }
    }
}
