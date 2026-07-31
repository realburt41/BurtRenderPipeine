using UnityEngine; // 引入 UnityEngine，下面需要使用 Shader.PropertyToID 和 Shader.SetGlobalXXX。
using UnityEngine.Rendering;

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
        PreSkinPosition = 114, // Material debug: linear [-16,16] pre-skin object-space position decoded from mesh UV3.
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
        SubsurfaceDirectTransmission = 129, // Material debug: direct-light Subsurface transmission contribution.
        GBufferBaseColor = 130, // GBuffer 调试：显示按 BurtGBuffer 约定编码再解码后的 BaseColor。
        GBufferNormalWS = 131, // GBuffer 调试：显示按 octahedron 编码再解码后的世界空间向量槽。
        GBufferMetallic = 132, // GBuffer 调试：显示 GBuffer 材质通道；Default Lit=metallic，Hair=scatter。
        GBufferSmoothness = 133, // GBuffer 调试：显示 GBuffer 解码后的 Smoothness，后续 Deferred 再从它还原 Roughness。
        GBufferOcclusion = 134, // GBuffer 调试：显示 GBuffer 解码后的 Ambient Occlusion。
        GBufferReflectance = 135, // GBuffer 调试：显示 GBuffer 解码后的 XRender Reflectance。
        GBufferRoughness = 136, // GBuffer 调试：显示从 GBuffer Smoothness 还原出的 XRender Base.Roughness。
        GBufferDiffuseColor = 137, // GBuffer 调试：显示从 GBuffer 还原 PBRMaterialData 后的 DiffuseColor。
        GBufferHairStrandDirection = 138, // GBuffer 调试：只显示 Hair 像素复用 GBuffer0.rgb 存储的 strand direction。
        GBufferHairScatter = 139, // GBuffer 调试：只显示 Hair 像素复用 GBuffer2.r material channel 存储的 scatter。
        GBufferHairShift = 140, // GBuffer 调试：只显示 Hair 像素复用 GBuffer2.r material channel 存储的 longitudinal shift scale。
        GBufferClearCoatMask = 141, // GBuffer debug: Clear Coat mask stored in GBuffer3.b.
        GBufferSubsurfaceStrength = 142, // GBuffer 调试：只显示 Subsurface 像素复用 GBuffer2.r material channel 存储的 strength。
        GBufferClearCoatNormalWS = 143,
        GBufferClearCoatRoughness = 144, // GBuffer debug: Clear Coat top-layer roughness stored in GBuffer3.a.
        GBufferAnisotropy = 145, // GBuffer debug: signed anisotropy stored in GBuffer4.b.
        GBufferTangentWS = 146, // GBuffer debug: base tangent stored in GBuffer4.rg as octahedral direction.
        GBufferSubsurfaceThickness = 147, // GBuffer debug: Subsurface thickness stored in GBuffer4.a.
        GBufferSubsurfaceProfileIndex = 148, // GBuffer debug: Subsurface profile index packed with thickness in GBuffer4.a.
        SubsurfaceTransmissionBRDF = 149, // Material debug: unlit Subsurface transmission BRDF.
        SubsurfaceTransmissionShadow = 150, // Material debug: shadow term used by direct Subsurface transmission.
        SubsurfaceTransmissionPhase = 151, // Material debug: HG phase term used by direct Subsurface transmission.
        SubsurfaceTransmissionThickness = 152, // Material debug: profile LUT thickness coordinate used by transmission.
        GBufferFoliageTransmissionColor = 153,
        GBufferFoliageTransmissionWeight = 154,
        GBufferFoliageThickness = 155,
        GBufferFoliageTransmissionNdotL = 156,
        GBufferFoliageSpecularScale = 157,
        GBufferFoliageScreenSpaceShadowIntensity = 158,
        GBufferStencilRaw = 159,
        GBufferStencilShadingModel = 160,
        FoliageTransmission = 161,
        FoliageDirectTransmission = 162,
        FoliageTransmissionBRDF = 163,
        FoliageTransmissionShadow = 164,
        FoliageSpecularBRDF = 165,
        GBufferGrassIsGrass = 166,
        GBufferGrassSSSIntensity = 167,
        GBufferGrassSpecularMultiply = 168,
        GBufferGrassScreenSpaceShadowIntensity = 169,
        GrassTransmission = 170,
        GrassDirectTransmission = 171,
        GrassTransmissionBRDF = 172,
        GrassTransmissionShadow = 173,
        GrassSpecularBRDF = 174,
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
        MainLightShadowReceiverDepth = 234, // Main light shadow debug: receiver depth used as hardware compare reference.
        MainLightShadowRawDepth = 235, // Main light shadow debug: raw depth loaded from the shadow atlas at receiver UV.
        MainLightShadowCompare = 236, // Main light shadow debug: direct hardware compare visibility at receiver UV.
        MainLightShadowProjectionValidity = 237, // Main light shadow debug: valid projection and manual-vs-hardware depth comparison diagnosis.
        CameraDepth = 300, // 全屏调试：复用 BurtRP 当前已有的 CameraDepth debug pass。
        MainLightShadow = 301, // 全屏调试：复用 BurtRP 当前已有的 MainLightShadow debug pass。
        PerObjectShadowAtlas = 475, // Fullscreen debug: per-object shadow atlas.
        PerObjectShadowObjectIndex = 476, // Per-object shadow debug: 1-based receiver object index.
        PerObjectShadowSlice = 477, // Per-object shadow debug: selected atlas slice.
        PerObjectShadowUV = 478, // Per-object shadow debug: projected atlas UV.
        PerObjectShadowDepth = 479, // Per-object shadow debug: receiver depth, stored depth, and compare visibility.
        PerObjectShadowCompare = 480, // Per-object shadow debug: hardware shadow compare visibility.
        PerObjectShadowTransmissionDepth = 481, // Per-object shadow debug: transmission surface depth, stored depth, and depth delta.
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
        TemporalAAConfidence = 326, // TAA debug: history availability after history-valid and UV-in-bounds checks.
        TemporalAACurrentDepth = 327, // TAA debug: TAA-owned current depth texture.
        TemporalAADepthHistory = 328, // TAA debug: previous-frame depth history.
        TemporalAADepthDelta = 329, // TAA debug: current/history depth disagreement.
        TemporalAACurrentColor = 330, // TAA debug: jittered current color before resolve.
        TemporalAAResolvedColor = 331, // TAA debug: resolved TAA output before history update.
        TemporalAARawVelocity = 332, // TAA debug: raw camera/object velocity before dilation.
        TemporalAAUpdatedConfidence = 333, // TAA debug: final history acceptance after parallax and coverage checks.
        TemporalAAStaticRelax = 334, // TAA debug: current/history depth continuity for static geometry.
        TemporalAALumaRejection = 335, // TAA debug: luma rejection contribution.
        TemporalAAClipRejection = 336, // TAA debug: color box / variance clip rejection contribution.
        TemporalAADepthRejection = 337, // TAA debug: depth and depth-range rejection contribution.
        TemporalAANormalRejection = 338, // TAA debug: GBuffer normal edge rejection contribution.
        TemporalAAMotionRejection = 339, // TAA debug: velocity-length rejection contribution.
        TemporalAAConfidenceGate = 340, // TAA debug: current blend, history feedback, and history acceptance.
        TemporalAAVelocitySource = 341, // TAA debug: raw velocity source, object motion vectors highlight in white.
        TemporalAAGBufferNormal = 342, // TAA debug: decoded Deferred GBuffer normal sampled by TAA.
        TemporalAAParallaxRejection = 343, // TAA debug: XRender-style parallax/depth history validity mask.
        TemporalAAAntiFlicker = 344, // TAA debug: stable history acceptance from PrevUseCount and depth continuity.
        TemporalAAHistoryCoverage = 345, // TAA debug: history coverage from PrevUseCount-style reuse validity.
        TemporalAAResponsiveMask = 346, // TAA debug: responsive mask, source strength, and geometry break gate.
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
        TemporalAAMetadata = 489, // TAA debug: metadata RGB, red = trusted object motion, green = responsive, blue = untrusted motion.
        TemporalAAObjectMotionMask = 490, // TAA debug: object motion ownership, red = trusted, green = untrusted, blue = velocity valid.
        TemporalAAUpscaleState = 491, // TAA debug: TAAU state, red = active, green/blue = width/height upscale factor.
        TemporalAAStencilMask = 495, // TAA debug: stencil-derived TAA mask, red = object motion, green = responsive, blue = other stencil bits.
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
        ScreenSpaceSubsurfaceSetup = 408, // SSS debug: setup mask, strength, and profile slice.
        ScreenSpaceSubsurfaceCoarseMask = 409, // SSS debug: coarse 8x8 activity mask.
        ScreenSpaceSubsurfaceBlur = 410, // SSS debug: blurred Subsurface color before combine.
        ScreenSpaceSubsurfaceCombine = 411, // SSS debug: combined color before copying to CameraColor.
        ScreenSpaceSubsurfaceThickness = 412, // SSS debug: decoded thickness from setup / GBuffer data.
        ScreenSpaceSubsurfaceProfileIndex = 413, // SSS debug: resolved subsurface profile slot.
        ScreenSpaceSubsurfaceProfileTint = 446, // SSS debug: profile tint color selected by the material profile slot.
        ScreenSpaceSubsurfaceProfileTintRaw = 467, // SSS debug: raw profile tint selected by the material profile slot, without setup/mask modulation.
        ScreenSpaceSubsurfaceProfileSurfaceAlbedo = 447, // SSS debug: profile surface albedo color selected by the material profile slot.
        ScreenSpaceSubsurfaceProfileMeanFreePath = 448, // SSS debug: profile mean free path color selected by the material profile slot.
        ScreenSpaceSubsurfaceProfileTintedLighting = 449, // SSS debug: XRender-style profile-tinted diffuse lighting before base color is reapplied.
        ScreenSpaceSubsurfaceProfileTintedFinal = 450, // SSS debug: XRender-style profile-tinted diffuse after base color is reapplied.
        ScreenSpaceSubsurfaceBlurAlpha = 451, // SSS debug: alpha stored in the final Subsurface blur texture; 4S stores a valid-pixel mask, 5S stores Burley weight.
        ScreenSpaceSubsurfaceBlurRadius = 452, // SSS debug: estimated 4S separable maximum blur offset in screen pixels.
        ScreenSpaceSubsurfaceBlurDelta = 453, // SSS debug: amplified absolute difference between 4S blurred diffuse and original diffuse lighting.
        ScreenSpaceSubsurfaceSeparableSampleGate = 454, // SSS debug: 4S separable sample gate, red = profile/type hits, green = depth weight, blue = radius.
        ScreenSpaceSubsurfaceSeparableHorizontalDelta = 455, // SSS debug: amplified absolute difference between 4S horizontal temp and original diffuse lighting.
        ScreenSpaceSubsurfaceBlurNormalized = 456, // SSS debug: blur texture normalized by alpha, matching the SSSColor consumed by combine.
        ScreenSpaceSubsurfaceBlurSignedDelta = 457, // SSS debug: signed normalized blur versus diffuse lighting, green = red-channel gain, red = red-channel loss, blue = blue-channel change.
        ScreenSpaceSubsurfaceSetupDiffuse = 458, // SSS debug: setup-stage diffuse lighting stored before 4S/5S blur.
        ScreenSpaceSubsurfaceSeparableHorizontal = 459, // SSS debug: raw 4S separable horizontal output stored in the temp texture.
        ScreenSpaceSubsurfaceSeparableHorizontalDepth = 460, // SSS debug: scene depth alpha preserved by the 4S horizontal output.
        ScreenSpaceSubsurfaceXRenderCombineFactors = 461, // SSS debug: red = diffuse factor, green = max profile tint, blue = SSSColor delta.
        ScreenSpaceSubsurfaceProfileKernel = 462, // SSS debug: red = center kernel weight, green = max kernel offset, blue = radius pixels.
        ScreenSpaceSubsurfaceSSSColorDelta = 463, // SSS debug: amplified relative SSSColor versus diffuse light delta before profile tint.
        ScreenSpaceSubsurfaceProfileKernelColor = 464, // SSS debug: normalized RGB spread of the 4S profile kernel.
        ScreenSpaceSubsurfaceFinalDelta = 465, // SSS debug: signed final color delta after XRender-style combine.
        ScreenSpaceSubsurfaceFinalDiffuseDelta = 466, // SSS debug: signed diffuse-only final delta after base color is reapplied.
        ScreenSpaceSubsurfaceSSSColorSignedDelta = 468, // SSS debug: signed SSSColor versus diffuse light delta before profile tint/base color.
        ScreenSpaceSubsurfaceProfileTintedDelta = 469, // SSS debug: signed profile-tinted diffuse delta before base color is reapplied.
        ScreenSpaceSubsurfaceSeparableValidity = 470, // SSS debug: 4S validity, red = setup mask, green = separable profile type, blue = source depth.
        ScreenSpaceSubsurfaceSeparableIO = 471, // SSS debug: 4S pass IO, red = horizontal depth, green = vertical alpha, blue = vertical delta.
        ScreenSpaceSubsurfaceSeparableStages = 472, // SSS debug: 4S stage diagnostics, red = setup/type/depth validity, green = horizontal activity, blue = vertical activity.
        ScreenSpaceSubsurfaceSeparableChain = 473, // SSS debug: 4S chain diagnostics, red = material forward encoding, green = GBuffer subsurface, blue = setup/profile/pass writes.
        ScreenSpaceSubsurfaceXRenderCombineTriplet = 474, // SSS debug: repeated local stripes of XRender combine inputs; profile tint, SSSColor, and subsurface lighting.
        ScreenSpaceSubsurfaceTransmission = 414, // SSS debug: profile transmission contribution.
        ScreenSpaceSubsurfaceDiffuse = 415, // SSS debug: diffuse lighting source used by the blur.
        ScreenSpaceSubsurfaceSpecular = 416, // SSS debug: non-diffuse source preserved by the combine.
        ScreenSpaceSubsurfaceStability = 417, // SSS debug: depth/normal stability gate used by the blur.
        ScreenSpaceSubsurfaceSampleCount = 418, // SSS debug: adaptive Burley sample count stored in history.
        ScreenSpaceSubsurfaceVariance = 419, // SSS debug: history variance used to choose adaptive samples.
        ScreenSpaceSubsurfaceHistory = 420, // SSS debug: history validity, age, and residual summary.
        ScreenSpaceSubsurfaceMask = 421, // SSS debug: full-resolution stencil-derived surface mask.
        ScreenSpaceSubsurfaceSourceColor = 433, // SSS debug: copied lit source color before SSS rewrites CameraColor.
        ScreenSpaceSubsurfaceSourceAlpha = 434, // SSS debug: source alpha carrying diffuse luminance from deferred lighting.
        ScreenSpaceSubsurfaceBaseColor = 435, // SSS debug: base color captured by the SSS forward pass.
        ScreenSpaceSubsurfaceEmission = 436, // SSS debug: emission captured by the SSS forward pass.
        ScreenSpaceSubsurfaceDiffuseWithBaseColor = 437, // SSS debug: decoded diffuse lighting before profile tint/base-color recombine; legacy enum name is kept for compatibility.
        ScreenSpaceSubsurfaceSpecularRaw = 438, // SSS debug: raw specular residual before display exposure.
        ScreenSpaceSubsurfaceCombineDelta = 439, // SSS debug: absolute difference between source and combined SSS color.
        FogAmount = 394, // Fog debug: final screen-space fog opacity.
        FogTransmittance = 395, // Fog debug: screen-space fog transmittance after height and distance gates.
        FogHeight = 396, // Fog debug: reconstructed world height relative to the fog height plane.
        FogDistance = 397, // Fog debug: reconstructed camera-to-surface distance used by the fog pass.
        VolumetricFogScattering = 398, // Volumetric fog debug: shared integrated scattering contribution.
        VolumetricFogTransmittance = 399, // Volumetric fog debug: shared integrated transmittance.
        VolumetricFogDensity = 400, // Volumetric fog debug: legacy fallback average and peak sampled density.
        VolumetricFogDistance = 401, // Volumetric fog debug: legacy fallback distance versus visible distance.
        VolumetricFogStepCount = 402, // Volumetric fog debug: normalized legacy fallback step count.
        AutoExposureLuminance = 384, // Auto exposure debug: log luminance heatmap from current CameraColor.
        AutoExposureMeteringWeight = 385, // Auto exposure debug: center-weighted metering mask used by lightweight histogram.
        AutoExposureHistogramRange = 386, // Auto exposure debug: current histogram EV range and out-of-range colors.
        ScreenSpaceAmbientOcclusionSurfaceStability = 387, // SSAO debug: current-frame depth/normal stability used to gate temporal history.
        ScreenSpaceAmbientOcclusionDiagnosticCompare = 388, // SSAO debug: quadrant compare for raw/final/history/difference with temporal gates.
        ScreenSpaceGlobalIlluminationRaw = 389, // BurtGI debug: raw screen-space diffuse GI before the depth/normal bilateral blur.
        ScreenSpaceGlobalIlluminationFinal = 390, // BurtGI debug: blurred diffuse GI texture consumed by composite.
        ScreenSpaceGlobalIlluminationHitRatio = 422, // BurtGI debug: trace hit confidence in alpha, with sky fallback shown as low values.
        ScreenSpaceGlobalIlluminationOverlay = 423, // BurtGI debug: final diffuse GI added over the current camera color.
        ScreenSpaceGlobalIlluminationComposite = 424, // BurtGI debug: isolated final composite contribution.
        ScreenSpaceGlobalIlluminationTemporalConfidence = 425, // BurtGI debug: v1 temporal history acceptance confidence after depth/normal/color gates.
        ScreenSpaceGlobalIlluminationTemporalRejection = 426, // BurtGI debug: v1 temporal disocclusion/rejection mask, with blue carrying current hit ratio.
        ScreenSpaceGlobalIlluminationHistory = 427, // BurtGI debug: previous resolved diffuse GI history used by temporal accumulation.
        ScreenSpaceGlobalIlluminationDifference = 428, // BurtGI debug: amplified absolute difference between current final GI and previous history.
        ScreenSpaceGlobalIlluminationLeakGuard = 429, // BurtGI debug: edge, hit and sky-fallback risk used by the v2 leak guard.
        ScreenSpaceGlobalIlluminationDiagnosticCompare = 430, // BurtGI debug: quadrant compare for raw/final/hit/leak-guard diagnostics.
        ScreenSpaceSubsurfaceAlgorithm = 431, // SSS debug: material-selected algorithm, red = 5S Burley, green = 4S Separable.
        ScreenSpaceGlobalIlluminationConfidence = 432, // BurtGI debug: quadrant compare for hit ratio, surface validity, edge risk, and sky-fallback risk.
        ScreenSpaceReflectionTemporalAlpha = 440, // SSR debug: temporal accumulation alpha before final material/composite weighting.
        ScreenSpaceReflectionRoughnessMipAlpha = 441, // SSR debug: alpha in the roughness-selected temporal mip.
        ScreenSpaceReflectionReceiverContinuity = 442, // SSR debug: final receiver same-surface continuity gate.
        ScreenSpaceReflectionFallbackSpecular = 443, // SSR debug: camera IBL fallback specular that SSR replaces.
        ScreenSpaceReflectionCompositeDelta = 444, // SSR debug: red = darkening delta, green = brightening delta, blue = final alpha.
        ScreenSpaceReflectionCameraColor = 445, // SSR debug: camera color copied at the start of SSR composite.
        FurBlurDirection = 482, // Fur blur debug: encoded blur direction from the multipass fur property target.
        FurBlurPropertyDepth = 483, // Fur blur debug: device-depth payload stored with the fur blur property.
        FurBlurCurrent = 484, // Fur blur debug: current-frame anisotropic fur blur before temporal resolve.
        FurBlurTemporal = 485, // Fur blur debug: fur blur after temporal resolve.
        FurBlurHistory = 486, // Fur blur debug: previous fur blur history texture.
        FurBlurDiagnostic = 487, // Fur blur debug: red = valid property, green = temporal alpha, blue = history age.
        FurBlurReprojection = 488, // Fur blur debug: red = reprojected, green = property-compatible, blue = property history valid.
        ScreenSpaceShadow = 492, // SS Shadow debug: screen-space main-light visibility texture consumed by deferred lighting.
        PerObjectShadowTransmissionThickness = 493, // Per-object shadow debug: transmission object index, resolved thickness, and validity state.
        ScreenSpaceShadowFinalMultiplier = 494, // SS Shadow debug: final deferred main-light multiplier after material-specific weighting.
        ScreenSpaceGlobalIlluminationHashGridDebug = 496, // BurtGI debug: Radiance Cache HashGrid debug buffer heatmap.
        ScreenSpaceGlobalIlluminationRadianceCacheSkyAO = 497, // BurtGI debug: Radiance Cache ProbeSkyAO atlas sampled through the active clipmap.
        ScreenSpaceGlobalIlluminationRadianceCacheStats = 498, // BurtGI debug: Radiance Cache priority/update/probe allocation stats texture.
        ScreenSpaceGlobalIlluminationRadianceCacheVisualize = 499, // BurtGI debug: Radiance Cache probe irradiance/sky AO visualization for the surface probe selected by the active clipmap.
        ScreenSpaceGlobalIlluminationRadianceCacheStatus = 500, // BurtGI debug: Radiance Cache probe last-used/last-traced state for the surface probe selected by the active clipmap.
        ScreenSpaceGlobalIlluminationScreenProbePlacement = 501, // BurtGI debug: XGI ScreenProbe placement depth/normal/position validity.
        ScreenSpaceGlobalIlluminationScreenProbeTraceVisualize = 502, // BurtGI debug: XGI ScreenProbe trace atlas radiance and hit visualization.
        ScreenSpaceGlobalIlluminationSceneVoxelOccupancy = 506, // BurtGI debug: XGI SceneVoxel occupancy mip sampled at the current surface position.
        GIProbeIrradiance = 503, // XGIProbe debug: per-pixel probe volume irradiance after virtual/direct probe sampling.
        GIProbeValidity = 504, // XGIProbe debug: baked virtual validity, or the direct volume sample mask for non-virtual probe data.
        GIProbeSkyVisibility = 505, // XGIProbe debug: baked sky visibility evaluated from the active virtual probe, or white for direct probe data.
        GIProbeRuntimeInfo = 507, // XGIProbe debug: runtime virtual probe streaming and memory overlay.
        AtmosphereLutSkyView = 508, // Atmosphere debug: raw physical SkyView LUT lookup before artistic fallback blending.
        AtmosphereLutMultipleScattering = 509, // Atmosphere debug: converged physical multiple-scattering LUT lookup.
        AtmosphereLutHorizontalScattering = 510, // Atmosphere debug: XRender-style phase-independent horizon scattering triplet.
        ScreenSpaceGlobalIlluminationCombinedIndirect = 511, // BurtGI debug: isolated diffuse, backface-diffuse and rough-specular channels combined.
        ScreenSpaceGlobalIlluminationDiffuseIndirect = 512, // BurtGI debug: isolated filtered diffuse irradiance channel used by deferred lighting.
        ScreenSpaceGlobalIlluminationBackfaceDiffuseIndirect = 513, // BurtGI debug: isolated backface diffuse/transmission channel.
        ScreenSpaceGlobalIlluminationRoughSpecularIndirect = 514 // BurtGI debug: isolated rough-specular channel.
    }

    // 保存 Editor Overlay 和运行时渲染共享的 shading debug 状态。
    public static class BurtShadingDebugSettings
    {
        private enum ForwardShadingDebugCategory
        {
            None,
            Lighting,
            Brdf,
            Shadow,
            Transmission
        }

        public const string ModeShaderName = "_BurtShadingDebugMode"; // 定义 shader 侧读取 debug 模式的全局属性名。
        public const string EnabledShaderName = "_BurtShadingDebugEnabled"; // 定义 shader 侧读取 debug 是否开启的全局属性名。
        public const string KeywordName = "BURT_SHADING_DEBUG";
        public const string ForwardKeywordName = "BURT_USE_DEBUG_MODE_FORWARD";
        private const string LegacyForwardDebugLightingKeywordName = "BURT_FORWARD_SHADING_DEBUG_LIGHTING";
        private const string LegacyForwardDebugBrdfKeywordName = "BURT_FORWARD_SHADING_DEBUG_BRDF";
        private const string LegacyForwardDebugShadowKeywordName = "BURT_FORWARD_SHADING_DEBUG_SHADOW";
        private const string LegacyForwardDebugTransmissionKeywordName = "BURT_FORWARD_SHADING_DEBUG_TRANSMISSION";
        private static readonly int ModeShaderId = Shader.PropertyToID(ModeShaderName); // 缓存模式属性 ID，避免每帧字符串查找。
        private static readonly int EnabledShaderId = Shader.PropertyToID(EnabledShaderName); // 缓存开关属性 ID，避免每帧字符串查找。
        private static BurtShadingDebugMode currentMode = BurtShadingDebugMode.None; // 保存当前 debug 模式，默认关闭。
        private static BurtShadingDebugMode appliedMode;
        private static bool hasAppliedGlobalShaderProperties;

        public static BurtShadingDebugMode Mode // 暴露当前 debug 模式，Editor UI 和渲染侧都通过它读写状态。
        {
            get => currentMode; // 返回当前保存的 debug 模式。
            set // 设置新的 debug 模式，并立刻同步到 shader 全局参数。
            {
                var normalizedMode = NormalizeMode(value);
                if (currentMode == normalizedMode)
                {
                    return;
                }

                currentMode = normalizedMode; // 保存新的当前模式；相机级 Prepare Pass 会按命令流同步到 shader。
                hasAppliedGlobalShaderProperties = false;
            }
        }

        public static bool IsDebugging => currentMode != BurtShadingDebugMode.None; // 只要不是 None，就认为 debug 已开启。

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetGlobalShaderPropertyCache()
        {
            hasAppliedGlobalShaderProperties = false;
        }

        public static bool UseTileLightCpuDebugColorTextureFallback { get; set; } // Diagnostic fallback for tile-light debug; StructuredBuffer remains the default path.

        private static int tileLightDebugMaxLightsPerTile = BurtLightingData.MaxAdditionalLights; // Debug-only tile-list capacity override; normal lighting still uses MaxAdditionalLights.

        public static int TileLightDebugMaxLightsPerTile
        {
            get => tileLightDebugMaxLightsPerTile;
            set => tileLightDebugMaxLightsPerTile = Mathf.Clamp(value, 1, BurtLightingData.MaxAdditionalLights);
        }

        public static BurtShadingDebugMode NormalizeMode(BurtShadingDebugMode mode)
        {
            return System.Enum.IsDefined(typeof(BurtShadingDebugMode), mode) ? mode : BurtShadingDebugMode.None;
        }

        public static bool IsMainLightShadowDebugMode(BurtShadingDebugMode mode) // 统一判断主光阴影相关调试模式，方便日志和阴影诊断共享同一套入口。
        {
            return mode == BurtShadingDebugMode.ShadowAttenuation
                || (mode >= BurtShadingDebugMode.ShadowCascadeIndex && mode <= BurtShadingDebugMode.ShadowReceiverDepthDelta)
                || mode == BurtShadingDebugMode.ShadowPCSSBlockerFraction
                || (mode >= BurtShadingDebugMode.MainLightShadowReceiverDepth && mode <= BurtShadingDebugMode.MainLightShadowProjectionValidity)
                || mode == BurtShadingDebugMode.MainLightShadow
                || mode == BurtShadingDebugMode.PerObjectShadowAtlas
                || (mode >= BurtShadingDebugMode.PerObjectShadowObjectIndex && mode <= BurtShadingDebugMode.PerObjectShadowTransmissionDepth)
                || mode == BurtShadingDebugMode.PerObjectShadowTransmissionThickness;
        }

        public static bool IsSceneEffectDebugMode(BurtShadingDebugMode mode)
        {
            return IsAtmosphereDebugMode(mode) || IsFogDebugMode(mode) || IsVolumetricFogDebugMode(mode);
        }

        public static bool IsAtmosphereDebugMode(BurtShadingDebugMode mode)
        {
            switch (mode)
            {
                case BurtShadingDebugMode.Atmosphere:
                case BurtShadingDebugMode.AtmosphereRayleigh:
                case BurtShadingDebugMode.AtmosphereMie:
                case BurtShadingDebugMode.AtmosphereTransmittance:
                case BurtShadingDebugMode.AtmosphereAerialTransmittance:
                case BurtShadingDebugMode.AtmosphereAerialInscatter:
                case BurtShadingDebugMode.AtmosphereAerialFogAmount:
                case BurtShadingDebugMode.AtmosphereAerialHeightFade:
                case BurtShadingDebugMode.AtmosphereAerialSummary:
                case BurtShadingDebugMode.AtmosphereSunDisk:
                case BurtShadingDebugMode.AtmosphereSunHalo:
                case BurtShadingDebugMode.AtmosphereHorizon:
                case BurtShadingDebugMode.AtmosphereGroundBlend:
                case BurtShadingDebugMode.AtmosphereViewDirection:
                case BurtShadingDebugMode.AtmosphereLutSkyView:
                case BurtShadingDebugMode.AtmosphereLutMultipleScattering:
                case BurtShadingDebugMode.AtmosphereLutHorizontalScattering:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsFogDebugMode(BurtShadingDebugMode mode)
        {
            switch (mode)
            {
                case BurtShadingDebugMode.FogAmount:
                case BurtShadingDebugMode.FogTransmittance:
                case BurtShadingDebugMode.FogHeight:
                case BurtShadingDebugMode.FogDistance:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsVolumetricFogDebugMode(BurtShadingDebugMode mode)
        {
            switch (mode)
            {
                case BurtShadingDebugMode.VolumetricFogScattering:
                case BurtShadingDebugMode.VolumetricFogTransmittance:
                case BurtShadingDebugMode.VolumetricFogDensity:
                case BurtShadingDebugMode.VolumetricFogDistance:
                case BurtShadingDebugMode.VolumetricFogStepCount:
                    return true;
                default:
                    return false;
            }
        }

        private static ForwardShadingDebugCategory ResolveForwardShadingDebugCategory(BurtShadingDebugMode mode)
        {
            switch (mode)
            {
                case BurtShadingDebugMode.Albedo:
                case BurtShadingDebugMode.NormalWS:
                case BurtShadingDebugMode.Smoothness:
                case BurtShadingDebugMode.Metallic:
                case BurtShadingDebugMode.Occlusion:
                case BurtShadingDebugMode.Reflectance:
                case BurtShadingDebugMode.Roughness:
                case BurtShadingDebugMode.DiffuseColor:
                case BurtShadingDebugMode.Height:
                case BurtShadingDebugMode.PreSkinPosition:
                case BurtShadingDebugMode.GBufferBaseColor:
                case BurtShadingDebugMode.GBufferNormalWS:
                case BurtShadingDebugMode.GBufferMetallic:
                case BurtShadingDebugMode.GBufferSmoothness:
                case BurtShadingDebugMode.GBufferOcclusion:
                case BurtShadingDebugMode.GBufferReflectance:
                case BurtShadingDebugMode.GBufferRoughness:
                case BurtShadingDebugMode.GBufferDiffuseColor:
                case BurtShadingDebugMode.GBufferClearCoatMask:
                case BurtShadingDebugMode.GBufferSubsurfaceStrength:
                case BurtShadingDebugMode.GBufferClearCoatNormalWS:
                case BurtShadingDebugMode.GBufferClearCoatRoughness:
                case BurtShadingDebugMode.GBufferAnisotropy:
                case BurtShadingDebugMode.GBufferTangentWS:
                case BurtShadingDebugMode.GBufferSubsurfaceThickness:
                case BurtShadingDebugMode.GBufferSubsurfaceProfileIndex:
                case BurtShadingDebugMode.SpecularAARoughness:
                case BurtShadingDebugMode.SpecularEnergyCompensation:
                case BurtShadingDebugMode.SpecularOcclusion:
                case BurtShadingDebugMode.EnergyPreservation:
                case BurtShadingDebugMode.IndirectSpecularEnergyCompensation:
                case BurtShadingDebugMode.DirectBRDFD:
                case BurtShadingDebugMode.DirectBRDFVisibility:
                case BurtShadingDebugMode.DirectBRDFFresnel:
                case BurtShadingDebugMode.DirectDiffuseLobe:
                case BurtShadingDebugMode.DirectDiffuseBRDF:
                case BurtShadingDebugMode.DirectSpecularBRDF:
                case BurtShadingDebugMode.SpecularAANormalVariance:
                case BurtShadingDebugMode.SpecularAARoughnessDelta:
                case BurtShadingDebugMode.IndirectSpecularDFG:
                case BurtShadingDebugMode.IndirectSpecularEnvBRDF:
                    return ForwardShadingDebugCategory.Brdf;

                case BurtShadingDebugMode.SubsurfaceProfileId:
                case BurtShadingDebugMode.SubsurfaceTransmission:
                case BurtShadingDebugMode.SubsurfaceKernelWeight:
                case BurtShadingDebugMode.SubsurfaceIndirect:
                case BurtShadingDebugMode.SubsurfaceDirectTransmission:
                case BurtShadingDebugMode.SubsurfaceTransmissionBRDF:
                case BurtShadingDebugMode.SubsurfaceTransmissionShadow:
                case BurtShadingDebugMode.SubsurfaceTransmissionPhase:
                case BurtShadingDebugMode.SubsurfaceTransmissionThickness:
                case BurtShadingDebugMode.FoliageTransmission:
                case BurtShadingDebugMode.FoliageDirectTransmission:
                case BurtShadingDebugMode.FoliageTransmissionBRDF:
                case BurtShadingDebugMode.FoliageTransmissionShadow:
                case BurtShadingDebugMode.FoliageSpecularBRDF:
                case BurtShadingDebugMode.GrassTransmission:
                case BurtShadingDebugMode.GrassDirectTransmission:
                case BurtShadingDebugMode.GrassTransmissionBRDF:
                case BurtShadingDebugMode.GrassTransmissionShadow:
                case BurtShadingDebugMode.GrassSpecularBRDF:
                case BurtShadingDebugMode.HairPrimaryLobe:
                case BurtShadingDebugMode.HairSecondaryLobe:
                case BurtShadingDebugMode.HairTransmissionLobe:
                case BurtShadingDebugMode.HairScatter:
                    return ForwardShadingDebugCategory.Transmission;

                case BurtShadingDebugMode.ShadowAttenuation:
                case BurtShadingDebugMode.ShadowCascadeIndex:
                case BurtShadingDebugMode.ShadowCascadeBlend:
                case BurtShadingDebugMode.ShadowDistanceFade:
                case BurtShadingDebugMode.ShadowPCSSRadius:
                case BurtShadingDebugMode.ShadowReceiverDepthDelta:
                case BurtShadingDebugMode.ShadowPCSSBlockerFraction:
                case BurtShadingDebugMode.AdditionalShadowAttenuation:
                case BurtShadingDebugMode.AdditionalShadowFace:
                case BurtShadingDebugMode.AdditionalShadowUV:
                case BurtShadingDebugMode.AdditionalShadowDepth:
                case BurtShadingDebugMode.AdditionalShadowDepthDelta:
                case BurtShadingDebugMode.MainLightShadowReceiverDepth:
                case BurtShadingDebugMode.MainLightShadowRawDepth:
                case BurtShadingDebugMode.MainLightShadowCompare:
                case BurtShadingDebugMode.MainLightShadowProjectionValidity:
                case BurtShadingDebugMode.PerObjectShadowObjectIndex:
                case BurtShadingDebugMode.PerObjectShadowSlice:
                case BurtShadingDebugMode.PerObjectShadowUV:
                case BurtShadingDebugMode.PerObjectShadowDepth:
                case BurtShadingDebugMode.PerObjectShadowCompare:
                case BurtShadingDebugMode.PerObjectShadowTransmissionDepth:
                case BurtShadingDebugMode.PerObjectShadowTransmissionThickness:
                    return ForwardShadingDebugCategory.Shadow;

                case BurtShadingDebugMode.DetailLighting:
                case BurtShadingDebugMode.IndirectLighting:
                case BurtShadingDebugMode.DirectDiffuse:
                case BurtShadingDebugMode.DirectSpecular:
                case BurtShadingDebugMode.IndirectDiffuse:
                case BurtShadingDebugMode.IndirectSpecular:
                case BurtShadingDebugMode.AmbientOcclusion:
                case BurtShadingDebugMode.Emission:
                case BurtShadingDebugMode.FinalLighting:
                case BurtShadingDebugMode.AdditionalLighting:
                case BurtShadingDebugMode.AdditionalDiffuse:
                case BurtShadingDebugMode.AdditionalSpecular:
                case BurtShadingDebugMode.HairAdditionalLighting:
                case BurtShadingDebugMode.AdditionalLightingUnshadowed:
                case BurtShadingDebugMode.GIProbeIrradiance:
                case BurtShadingDebugMode.GIProbeValidity:
                case BurtShadingDebugMode.GIProbeSkyVisibility:
                    return ForwardShadingDebugCategory.Lighting;

                default:
                    return ForwardShadingDebugCategory.None;
            }
        }

        public static bool IsForwardMaterialDebugMode(BurtShadingDebugMode mode)
        {
            return mode != BurtShadingDebugMode.None &&
                ResolveForwardShadingDebugCategory(mode) != ForwardShadingDebugCategory.None;
        }

        public static bool IsShadingDebugAllowedForRequest(BurtRenderRequest request)
        {
            if (request == null)
            {
                return true;
            }

            // Keep material/shading debug scoped to the two user-facing scene cameras.
            // Preview, reflection, UI and unknown utility requests must not inherit the
            // global editor selection and compile/run the Forward debug permutation.
            switch (request.Type)
            {
                case BurtRenderRequestType.BaseCamera:
                case BurtRenderRequestType.OverlayCamera:
                case BurtRenderRequestType.SceneView:
                    return request.Camera == null ||
                        (request.Camera.cameraType != CameraType.Preview &&
                         request.Camera.cameraType != CameraType.Reflection);
                default:
                    return false;
            }
        }

        public static void RecordGlobalShaderProperties(CommandBuffer cmd)
        {
            RecordGlobalShaderProperties(cmd, null);
        }

        public static void RecordGlobalShaderProperties(CommandBuffer cmd, BurtRenderRequest request)
        {
            if (cmd == null)
            {
                return;
            }

            var requestAllowsDebug = IsShadingDebugAllowedForRequest(request);
            var requestMode = requestAllowsDebug ? currentMode : BurtShadingDebugMode.None;
            var enableForwardMaterialDebug = IsForwardMaterialDebugMode(requestMode);
            cmd.SetGlobalInt(ModeShaderId, (int)requestMode);
            cmd.SetGlobalFloat(EnabledShaderId, requestMode != BurtShadingDebugMode.None ? 1f : 0f);
            SetKeyword(cmd, ForwardKeywordName, enableForwardMaterialDebug);
            cmd.DisableShaderKeyword(KeywordName);

            // Always clear the four retired category keywords so domain reloads and old
            // editor sessions cannot leak a stale variant into a later camera.
            cmd.DisableShaderKeyword(LegacyForwardDebugLightingKeywordName);
            cmd.DisableShaderKeyword(LegacyForwardDebugBrdfKeywordName);
            cmd.DisableShaderKeyword(LegacyForwardDebugShadowKeywordName);
            cmd.DisableShaderKeyword(LegacyForwardDebugTransmissionKeywordName);
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

        public static void ApplyGlobalShaderProperties() // 把当前 shading debug 状态上传给 shader。
        {
            if (hasAppliedGlobalShaderProperties && appliedMode == currentMode)
            {
                return;
            }

            var enableForwardMaterialDebug = IsForwardMaterialDebugMode(currentMode);
            Shader.SetGlobalInt(ModeShaderId, (int)currentMode); // 上传整数模式 ID，后续 shader 可以 switch 或 if 判断。
            Shader.SetGlobalFloat(EnabledShaderId, IsDebugging ? 1f : 0f); // 上传 0/1 开关，方便 shader 快速判断是否走调试分支。
            Shader.DisableKeyword(LegacyForwardDebugLightingKeywordName);
            Shader.DisableKeyword(LegacyForwardDebugBrdfKeywordName);
            Shader.DisableKeyword(LegacyForwardDebugShadowKeywordName);
            Shader.DisableKeyword(LegacyForwardDebugTransmissionKeywordName);
            if (enableForwardMaterialDebug)
            {
                Shader.EnableKeyword(ForwardKeywordName);
            }
            else
            {
                Shader.DisableKeyword(ForwardKeywordName);
            }
            Shader.DisableKeyword(KeywordName);

            appliedMode = currentMode;
            hasAppliedGlobalShaderProperties = true;
        }
    }
}
