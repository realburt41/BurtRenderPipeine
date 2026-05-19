// BurtRP 的材质 Shading Debug 工具库，负责把 Editor Overlay 选择的调试模式转换成片元颜色。
#ifndef BURT_SHADING_DEBUG_INCLUDED // 开始 include guard，防止同一个 shader 编译单元里重复定义调试函数。
#define BURT_SHADING_DEBUG_INCLUDED // 标记 BurtShadingDebug.hlsl 已经被包含过，后续重复 include 会被跳过。

#if defined(BURT_SHADING_DEBUG) && !defined(BURT_ENABLE_SHADING_DEBUG)
#define BURT_ENABLE_SHADING_DEBUG 1
#endif

#if defined(BURT_ENABLE_SHADING_DEBUG)

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl" // 引入 BurtSafeNormalize，用来安全归一化世界空间法线。
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInput.hlsl" // 引入 BurtSurfaceData，让调试函数可以读取基础色、光滑度、金属度和环境遮蔽。
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtShadingModelMacros.hlsl"

float _BurtShadingDebugMode; // 保存 C# 侧 BurtShadingDebugSettings 上传的当前调试模式编号。
float _BurtShadingDebugEnabled; // 保存 C# 侧上传的调试开关，0 表示关闭，1 表示开启。

static const float BURT_SHADING_DEBUG_MODE_ALBEDO = 100.0f; // 对应 C# BurtShadingDebugMode.Albedo，用来显示材质基础色。
static const float BURT_SHADING_DEBUG_MODE_NORMAL_WS = 101.0f; // 对应 C# BurtShadingDebugMode.NormalWS，用来显示世界空间法线。
static const float BURT_SHADING_DEBUG_MODE_SMOOTHNESS = 102.0f; // 对应 C# BurtShadingDebugMode.Smoothness，用来显示最终材质光滑度。
static const float BURT_SHADING_DEBUG_MODE_METALLIC = 103.0f; // 对应 C# BurtShadingDebugMode.Metallic，用来显示最终材质金属度。
static const float BURT_SHADING_DEBUG_MODE_OCCLUSION = 104.0f; // 对应 C# BurtShadingDebugMode.Occlusion，用来显示材质环境遮蔽。
static const float BURT_SHADING_DEBUG_MODE_REFLECTANCE = 105.0f; // 对应 C# BurtShadingDebugMode.Reflectance，用来显示 XRender 风格介质反射率。
static const float BURT_SHADING_DEBUG_MODE_ROUGHNESS = 106.0f; // 对应 C# BurtShadingDebugMode.Roughness，用来显示材质感知粗糙度。
static const float BURT_SHADING_DEBUG_MODE_SPECULAR_AA_ROUGHNESS = 107.0f; // 对应 C# BurtShadingDebugMode.SpecularAARoughness，用来显示直接高光实际粗糙度。
static const float BURT_SHADING_DEBUG_MODE_SPECULAR_ENERGY_COMPENSATION = 108.0f; // 对应 C# BurtShadingDebugMode.SpecularEnergyCompensation，用来显示直接高光能量补偿强度。
static const float BURT_SHADING_DEBUG_MODE_SPECULAR_OCCLUSION = 109.0f; // 对应 C# BurtShadingDebugMode.SpecularOcclusion，用来显示间接高光遮蔽。
static const float BURT_SHADING_DEBUG_MODE_ENERGY_PRESERVATION = 110.0f; // 对应 C# BurtShadingDebugMode.EnergyPreservation，用来显示 XRender 底层 diffuse 保能比例。
static const float BURT_SHADING_DEBUG_MODE_INDIRECT_SPECULAR_ENERGY_COMPENSATION = 111.0f; // 对应 C# BurtShadingDebugMode.IndirectSpecularEnergyCompensation，用来显示间接高光能量补偿强度。
static const float BURT_SHADING_DEBUG_MODE_DIFFUSE_COLOR = 112.0f; // 对应 C# BurtShadingDebugMode.DiffuseColor，用来显示 XRender GenericData.DiffuseColor。
static const float BURT_SHADING_DEBUG_MODE_HEIGHT = 113.0f; // Corresponds to C# BurtShadingDebugMode.Height, showing Mask Map B.
static const float BURT_SHADING_DEBUG_MODE_DIRECT_BRDF_D = 115.0f; // 对应 C# BurtShadingDebugMode.DirectBRDFD，用来显示 GGX D 项。
static const float BURT_SHADING_DEBUG_MODE_DIRECT_BRDF_VISIBILITY = 116.0f; // 对应 C# BurtShadingDebugMode.DirectBRDFVisibility，用来显示 Smith Joint Visibility。
static const float BURT_SHADING_DEBUG_MODE_DIRECT_BRDF_FRESNEL = 117.0f; // 对应 C# BurtShadingDebugMode.DirectBRDFFresnel，用来显示 Schlick Fresnel。
static const float BURT_SHADING_DEBUG_MODE_DIRECT_DIFFUSE_LOBE = 118.0f; // 对应 C# BurtShadingDebugMode.DirectDiffuseLobe，用来显示 Lambert / Burley diffuse lobe。
static const float BURT_SHADING_DEBUG_MODE_DIRECT_DIFFUSE_BRDF = 119.0f; // 对应 C# BurtShadingDebugMode.DirectDiffuseBRDF，用来显示未乘灯光的直接 diffuse BRDF。
static const float BURT_SHADING_DEBUG_MODE_DIRECT_SPECULAR_BRDF = 120.0f; // 对应 C# BurtShadingDebugMode.DirectSpecularBRDF，用来显示未乘灯光的直接 specular BRDF。
static const float BURT_SHADING_DEBUG_MODE_SPECULAR_AA_NORMAL_VARIANCE = 121.0f; // 对应 C# BurtShadingDebugMode.SpecularAANormalVariance，用来显示 Normal Filtering 的法线方差。
static const float BURT_SHADING_DEBUG_MODE_SPECULAR_AA_ROUGHNESS_DELTA = 122.0f; // 对应 C# BurtShadingDebugMode.SpecularAARoughnessDelta，用来显示 Specular AA 增加的 roughness。
static const float BURT_SHADING_DEBUG_MODE_INDIRECT_SPECULAR_DFG = 123.0f; // 对应 C# BurtShadingDebugMode.IndirectSpecularDFG，用来显示间接高光 DFG.xy。
static const float BURT_SHADING_DEBUG_MODE_INDIRECT_SPECULAR_ENV_BRDF = 124.0f; // 对应 C# BurtShadingDebugMode.IndirectSpecularEnvBRDF，用来显示 DFG 应用到 F0/F90 后的环境 BRDF。
static const float BURT_SHADING_DEBUG_MODE_SUBSURFACE_PROFILE_ID = 125.0f;
static const float BURT_SHADING_DEBUG_MODE_SUBSURFACE_TRANSMISSION = 126.0f;
static const float BURT_SHADING_DEBUG_MODE_SUBSURFACE_KERNEL_WEIGHT = 127.0f;
static const float BURT_SHADING_DEBUG_MODE_SUBSURFACE_INDIRECT = 128.0f;
static const float BURT_SHADING_DEBUG_MODE_GBUFFER_BASE_COLOR = 130.0f; // 对应 C# BurtShadingDebugMode.GBufferBaseColor，用来显示 GBuffer 解码 BaseColor。
static const float BURT_SHADING_DEBUG_MODE_GBUFFER_NORMAL_WS = 131.0f; // 对应 C# BurtShadingDebugMode.GBufferNormalWS，用来显示 GBuffer 解码向量槽。
static const float BURT_SHADING_DEBUG_MODE_GBUFFER_METALLIC = 132.0f; // 对应 C# BurtShadingDebugMode.GBufferMetallic，用来显示 GBuffer 解码材质通道。
static const float BURT_SHADING_DEBUG_MODE_GBUFFER_SMOOTHNESS = 133.0f; // 对应 C# BurtShadingDebugMode.GBufferSmoothness，用来显示 GBuffer 解码 Smoothness。
static const float BURT_SHADING_DEBUG_MODE_GBUFFER_OCCLUSION = 134.0f; // 对应 C# BurtShadingDebugMode.GBufferOcclusion，用来显示 GBuffer 解码 AO。
static const float BURT_SHADING_DEBUG_MODE_GBUFFER_REFLECTANCE = 135.0f; // 对应 C# BurtShadingDebugMode.GBufferReflectance，用来显示 GBuffer 解码 Reflectance。
static const float BURT_SHADING_DEBUG_MODE_GBUFFER_ROUGHNESS = 136.0f; // 对应 C# BurtShadingDebugMode.GBufferRoughness，用来显示 GBuffer 还原的 Base.Roughness。
static const float BURT_SHADING_DEBUG_MODE_GBUFFER_DIFFUSE_COLOR = 137.0f; // 对应 C# BurtShadingDebugMode.GBufferDiffuseColor，用来显示 GBuffer 还原的 DiffuseColor。
static const float BURT_SHADING_DEBUG_MODE_GBUFFER_CLEAR_COAT_MASK = 141.0f;
static const float BURT_SHADING_DEBUG_MODE_GBUFFER_SUBSURFACE_STRENGTH = 142.0f;
static const float BURT_SHADING_DEBUG_MODE_GBUFFER_CLEAR_COAT_NORMAL_WS = 143.0f;
static const float BURT_SHADING_DEBUG_MODE_GBUFFER_CLEAR_COAT_ROUGHNESS = 144.0f;
static const float BURT_SHADING_DEBUG_MODE_GBUFFER_ANISOTROPY = 145.0f;
static const float BURT_SHADING_DEBUG_MODE_GBUFFER_TANGENT_WS = 146.0f;
static const float BURT_SHADING_DEBUG_MODE_GBUFFER_SUBSURFACE_THICKNESS = 147.0f;
static const float BURT_SHADING_DEBUG_MODE_GBUFFER_SUBSURFACE_PROFILE_INDEX = 148.0f;
static const float BURT_SHADING_DEBUG_MODE_DETAIL_LIGHTING = 200.0f; // 对应 C# BurtShadingDebugMode.DetailLighting，用 0.18 中灰 BaseColor 显示光照细节。
static const float BURT_SHADING_DEBUG_MODE_INDIRECT_LIGHTING = 201.0f; // 对应 C# BurtShadingDebugMode.IndirectLighting，用来显示 PBR 间接光。
static const float BURT_SHADING_DEBUG_MODE_DIRECT_DIFFUSE = 202.0f; // 对应 C# BurtShadingDebugMode.DirectDiffuse，用来显示直接漫反射。
static const float BURT_SHADING_DEBUG_MODE_DIRECT_SPECULAR = 203.0f; // 对应 C# BurtShadingDebugMode.DirectSpecular，用来显示直接高光。
static const float BURT_SHADING_DEBUG_MODE_INDIRECT_DIFFUSE = 204.0f; // 对应 C# BurtShadingDebugMode.IndirectDiffuse，用来显示 SH / Light Probe 漫反射。
static const float BURT_SHADING_DEBUG_MODE_INDIRECT_SPECULAR = 205.0f; // 对应 C# BurtShadingDebugMode.IndirectSpecular，用来显示 Reflection Probe 镜面反射。
static const float BURT_SHADING_DEBUG_MODE_SHADOW_ATTENUATION = 206.0f; // 对应 C# BurtShadingDebugMode.ShadowAttenuation，用来显示主光阴影衰减。
static const float BURT_SHADING_DEBUG_MODE_AMBIENT_OCCLUSION = 207.0f; // 对应 C# BurtShadingDebugMode.AmbientOcclusion，用来显示参与间接光遮蔽的 AO。
static const float BURT_SHADING_DEBUG_MODE_EMISSION = 208.0f; // 对应 C# BurtShadingDebugMode.Emission，用来显示自发光贡献。
static const float BURT_SHADING_DEBUG_MODE_FINAL_LIGHTING = 209.0f; // 对应 C# BurtShadingDebugMode.FinalLighting，用来显示写入 CameraColor 前的最终材质光照。
static const float BURT_SHADING_DEBUG_MODE_HAIR_PRIMARY_LOBE = 210.0f; // 对应 C# BurtShadingDebugMode.HairPrimaryLobe，用来显示 Hair primary lobe。
static const float BURT_SHADING_DEBUG_MODE_HAIR_SECONDARY_LOBE = 211.0f; // 对应 C# BurtShadingDebugMode.HairSecondaryLobe，用来显示 Hair secondary lobe。
static const float BURT_SHADING_DEBUG_MODE_HAIR_TRANSMISSION_LOBE = 212.0f; // 对应 C# BurtShadingDebugMode.HairTransmissionLobe，用来显示 Hair backlit/transmission lobe。
static const float BURT_SHADING_DEBUG_MODE_HAIR_SCATTER = 213.0f; // 对应 C# BurtShadingDebugMode.HairScatter，用来显示 Hair lighting scatter。
static const float BURT_SHADING_DEBUG_MODE_SHADOW_CASCADE_INDEX = 214.0f; // 对应 C# BurtShadingDebugMode.ShadowCascadeIndex，用颜色显示 CSM cascade。
static const float BURT_SHADING_DEBUG_MODE_SHADOW_CASCADE_BLEND = 215.0f; // 对应 C# BurtShadingDebugMode.ShadowCascadeBlend，用来显示 cascade 边界混合。
static const float BURT_SHADING_DEBUG_MODE_SHADOW_DISTANCE_FADE = 216.0f; // 对应 C# BurtShadingDebugMode.ShadowDistanceFade，用来显示远距离淡出。
static const float BURT_SHADING_DEBUG_MODE_SHADOW_PCSS_RADIUS = 217.0f; // 对应 C# BurtShadingDebugMode.ShadowPCSSRadius，用来显示 PCSS 半影半径。
static const float BURT_SHADING_DEBUG_MODE_SHADOW_RECEIVER_DEPTH_DELTA = 218.0f; // 对应 C# BurtShadingDebugMode.ShadowReceiverDepthDelta，用来显示 receiver/shadow depth 差。
static const float BURT_SHADING_DEBUG_MODE_SHADOW_PCSS_BLOCKER_FRACTION = 225.0f; // 对应 C# BurtShadingDebugMode.ShadowPCSSBlockerFraction，用来显示 blocker 搜索命中比例。
static const float BURT_SHADING_DEBUG_MODE_ADDITIONAL_LIGHTING = 219.0f; // 对应 C# BurtShadingDebugMode.AdditionalLighting，只显示追加光直接光。
static const float BURT_SHADING_DEBUG_MODE_ADDITIONAL_DIFFUSE = 220.0f; // 对应 C# BurtShadingDebugMode.AdditionalDiffuse，只显示追加光漫反射。
static const float BURT_SHADING_DEBUG_MODE_ADDITIONAL_SPECULAR = 221.0f; // 对应 C# BurtShadingDebugMode.AdditionalSpecular，只显示追加光高光。
static const float BURT_SHADING_DEBUG_MODE_HAIR_ADDITIONAL_LIGHTING = 222.0f; // 对应 C# BurtShadingDebugMode.HairAdditionalLighting，只显示 Hair 追加光。
static const float BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_ATTENUATION = 226.0f; // 对应 C# BurtShadingDebugMode.AdditionalShadowAttenuation，用来显示追加光阴影衰减。
static const float BURT_SHADING_DEBUG_MODE_ADDITIONAL_LIGHTING_UNSHADOWED = 227.0f; // 对应 C# BurtShadingDebugMode.AdditionalLightingUnshadowed，用来对照追加光未乘阴影时的贡献。
static const float BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_FACE = 228.0f;
static const float BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_UV = 229.0f;
static const float BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_DEPTH = 230.0f;
static const float BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_DEPTH_DELTA = 231.0f;
static const float BURT_SHADING_DEBUG_SUBSURFACE_PROFILE_SLOT_COUNT = 8.0f;

// 保存片元已经算好的调试数据，避免 Debug View 重新计算一套和正常渲染不一致的光照。
struct BurtShadingDebugData
{
    // 保存世界空间法线，用于 NormalWS 调试模式。
    float3 normalWS;

    // 保存 Detail Lighting 结果，不包含自发光；调用方会参考 XRender 先把 BaseColor 替换成 0.18 中灰再计算。
    float3 detailLightingColor;

    // 保存直接漫反射贡献，已经包含主光颜色、NdotL 和阴影。
    float3 directDiffuseColor;

    // 保存直接镜面高光贡献，已经包含主光颜色、NdotL 和阴影。
    float3 directSpecularColor;

    // 保存间接漫反射贡献，主要来自 Unity SH / Light Probe。
    float3 indirectDiffuseColor;

    // 保存间接镜面贡献，主要来自 Unity Reflection Probe / Sky Reflection。
    float3 indirectSpecularColor;

    // 保存追加光直接漫反射，不包含主光、间接光和自发光。
    float3 additionalDiffuseColor;

    // 保存追加光直接镜面高光，不包含主光、间接光和自发光。
    float3 additionalSpecularColor;

    // 保存追加光直接光在不乘 additional shadow attenuation 时的贡献，用来确认 shadow 只减少追加光。
    float3 additionalUnshadowedColor;

    // 保存主光阴影衰减，1 表示不被阴影遮挡，0 表示完全落在阴影中。
    float shadowAttenuation;

    // 保存追加光阴影衰减，1 表示不被追加光阴影遮挡，0 表示完全落在追加光阴影中。
    float additionalShadowAttenuation;

    float3 additionalShadowFaceColor;
    float3 additionalShadowUVColor;
    float3 additionalShadowDepthColor;
    float3 additionalShadowDepthDeltaColor;

    // 保存当前像素命中的 CSM cascade 调试颜色。
    float3 shadowCascadeColor;

    // 保存当前像素在 cascade 边界处混合到下一级 cascade 的权重。
    float shadowCascadeBlend;

    // 保存最后一级 cascade 淡出到无阴影的权重。
    float shadowDistanceFade;

    // 保存当前像素估算出的 PCSS 半影半径，已归一化到 0 到 1。
    float shadowPCSSRadius;

    // 保存 receiver depth 和 shadow map stored depth 的可视化差值。
    float shadowReceiverDepthDelta;

    // 保存 PCSS blocker search 命中的 blocker 样本占比。
    float shadowPCSSBlockerFraction;

    // 保存参与间接光遮蔽的 AO 输入，Forward 来自 surfaceData，Deferred 来自 GBuffer 解码。
    float ambientOcclusion;

    // 保存自发光贡献，Forward 来自 Emission Map，Deferred 来自 GBuffer2.rgb。
    float3 emissionColor;

    // 保存材质最终写入 CameraColor 前的颜色，包含 PBR 光照和自发光。
    float3 finalLightingColor;

    // 保存材质 reflectance，用来确认材质面板输入的介质反射率。
    float reflectance;

    // 保存材质感知粗糙度，也就是 1 - smoothness 后的结果。
    float perceptualRoughness;

    // 保存直接高光实际使用的粗糙度，包含 Specular AA 对极光滑高光的拓宽。
    float specularAARoughness;

    // 保存直接高光多次散射能量补偿，1 表示没有补偿，大于 1 表示 LUT.z 正在补回能量。
    float3 specularEnergyCompensation;

    // 保存间接高光多次散射能量补偿，1 表示没有补偿，大于 1 表示反射探针高光正在补回能量。
    float3 indirectSpecularEnergyCompensation;

    // 保存 XRender EnergyPreservation，1 表示底层 diffuse 完整保留，越小表示被 specular 顶层占用越多能量。
    float energyPreservation;

    // 保存间接高光遮蔽项，1 表示不遮蔽。
    float specularOcclusion;

    // 保存 XRender GenericData.DiffuseColor，方便观察 metallic 是否正确扣除了 diffuse。
    float3 diffuseColor;

    // 保存直接 GGX D 项，数值可能很高，Debug View 会缩放显示。
    float directBRDFD;

    // 保存直接 Smith Joint Visibility 项。
    float directBRDFVisibility;

    // 保存直接 Schlick Fresnel 项。
    float3 directBRDFFresnel;

    // 保存直接 diffuse lobe，默认 Lambert 时约为 1 / PI。
    float directDiffuseLobe;

    // 保存未乘灯光颜色、NdotL 和阴影的直接 diffuse BRDF。
    float3 directDiffuseBRDF;

    // 保存未乘灯光颜色、NdotL 和阴影的直接 specular BRDF。
    float3 directSpecularBRDF;

    // 保存 Specular AA 使用的屏幕空间法线方差，通常很小，Debug View 会放大显示。
    float specularAANormalVariance;

    // 保存 Specular AA 额外增加的感知粗糙度。
    float specularAARoughnessDelta;

    // 保存间接高光 DFG.xy，Debug View 会显示为 R/G 两个通道。
    float2 indirectSpecularDFG;

    // 保存 DFG 应用到 F0/F90 后的环境 BRDF。
    float3 indirectSpecularEnvBRDF;

    // 保存 Hair primary/R lobe；Default Lit 固定为 0。
    float hairPrimaryLobe;

    // 保存 Hair secondary/TT lobe；Default Lit 固定为 0。
    float hairSecondaryLobe;

    // 保存 Hair 近似背光透射 lobe；Default Lit 固定为 0。
    float hairTransmissionLobe;

    // 保存 Hair lighting 使用的 scatter；Default Lit 固定为 0。
    float hairScatter;

    // 保存按 BurtGBuffer 约定编码再解码后的 BaseColor，用来提前验证 Deferred 材质还原。
    float3 gbufferBaseColor;

    // 保存按 BurtGBuffer octahedron 编码再解码后的世界空间向量槽。
    float3 gbufferNormalWS;

    // 保存 GBuffer 解码后的材质通道；Default Lit=metallic，Hair=scatter。
    float gbufferMetallic;

    float gbufferClearCoatMask;

    float3 gbufferClearCoatNormalWS;

    float gbufferClearCoatRoughness;

    float gbufferSubsurfaceStrength;

    float gbufferSubsurfaceThickness;

    float gbufferSubsurfaceProfileIndex;

    float gbufferAnisotropy;

    float3 gbufferTangentWS;

    // 保存 GBuffer 解码后的 Smoothness，Deferred 会再由它还原 Roughness。
    float gbufferSmoothness;

    // 保存 GBuffer 解码后的 Ambient Occlusion。
    float gbufferOcclusion;

    // 保存 GBuffer 解码后的 XRender Reflectance。
    float gbufferReflectance;

    // 保存从 GBuffer Smoothness 还原出的 XRender Base.Roughness。
    float gbufferRoughness;

    // 保存从 GBuffer 转成 PBRMaterialData 后的 DiffuseColor。
    float3 gbufferDiffuseColor;

    float subsurfaceProfileIndex;

    float3 subsurfaceTransmission;

    float3 subsurfaceKernelWeight;

    float3 subsurfaceIndirect;

};

bool BurtIsShadingDebugEnabled() // 判断当前是否启用了任意 shading debug 模式。
{
    return _BurtShadingDebugEnabled > 0.5f; // 使用 0.5 作为阈值，兼容 C# 上传的 0/1 float 开关。
}

bool BurtIsSameShadingDebugMode(float mode, float expectedMode) // 判断当前模式是否等于指定模式。
{
    return abs(mode - expectedMode) < 0.5f; // 用半个整数范围做比较，避免 float/int 上传转换产生精度边界问题。
}

float3 BurtEncodeNormalWSForDebug(float3 normalWS) // 把世界空间法线编码成可以显示到屏幕上的 RGB 颜色。
{
    float3 safeNormalWS = BurtSafeNormalize(normalWS); // 先安全归一化，避免插值或贴图采样造成长度偏差。
    return safeNormalWS * 0.5f + 0.5f; // 把 [-1, 1] 的法线范围映射到 [0, 1] 的颜色范围。
}

BurtShadingDebugData BurtCreateDefaultShadingDebugData(float3 normalWS) // 为不走完整 PBR 路径的 shader 生成一份可安全覆盖的调试数据默认值。
{
    BurtShadingDebugData data = (BurtShadingDebugData)0;
    float3 safeNormalWS = BurtSafeNormalize(normalWS);

    data.normalWS = safeNormalWS;
    data.detailLightingColor = float3(0.0f, 0.0f, 0.0f);
    data.directDiffuseColor = float3(0.0f, 0.0f, 0.0f);
    data.directSpecularColor = float3(0.0f, 0.0f, 0.0f);
    data.indirectDiffuseColor = float3(0.0f, 0.0f, 0.0f);
    data.indirectSpecularColor = float3(0.0f, 0.0f, 0.0f);
    data.additionalDiffuseColor = float3(0.0f, 0.0f, 0.0f);
    data.additionalSpecularColor = float3(0.0f, 0.0f, 0.0f);
    data.additionalUnshadowedColor = float3(0.0f, 0.0f, 0.0f);
    data.shadowAttenuation = 1.0f;
    data.additionalShadowAttenuation = 1.0f;
    data.additionalShadowFaceColor = float3(0.0f, 0.0f, 0.0f);
    data.additionalShadowUVColor = float3(0.0f, 0.0f, 0.0f);
    data.additionalShadowDepthColor = float3(0.0f, 0.0f, 0.0f);
    data.additionalShadowDepthDeltaColor = float3(0.0f, 0.0f, 0.0f);
    data.shadowCascadeColor = float3(0.0f, 0.0f, 0.0f);
    data.shadowCascadeBlend = 0.0f;
    data.shadowDistanceFade = 0.0f;
    data.shadowPCSSRadius = 0.0f;
    data.shadowReceiverDepthDelta = 0.0f;
    data.shadowPCSSBlockerFraction = 0.0f;
    data.ambientOcclusion = 1.0f;
    data.emissionColor = float3(0.0f, 0.0f, 0.0f);
    data.finalLightingColor = float3(0.0f, 0.0f, 0.0f);
    data.reflectance = BURT_INPUT_DEFAULT_REFLECTANCE;
    data.perceptualRoughness = 0.5f;
    data.specularAARoughness = 0.5f;
    data.specularEnergyCompensation = float3(1.0f, 1.0f, 1.0f);
    data.indirectSpecularEnergyCompensation = float3(1.0f, 1.0f, 1.0f);
    data.energyPreservation = 1.0f;
    data.specularOcclusion = 1.0f;
    data.diffuseColor = float3(0.0f, 0.0f, 0.0f);
    data.directBRDFD = 0.0f;
    data.directBRDFVisibility = 0.0f;
    data.directBRDFFresnel = float3(0.0f, 0.0f, 0.0f);
    data.directDiffuseLobe = 0.0f;
    data.directDiffuseBRDF = float3(0.0f, 0.0f, 0.0f);
    data.directSpecularBRDF = float3(0.0f, 0.0f, 0.0f);
    data.specularAANormalVariance = 0.0f;
    data.specularAARoughnessDelta = 0.0f;
    data.indirectSpecularDFG = float2(0.0f, 0.0f);
    data.indirectSpecularEnvBRDF = float3(0.0f, 0.0f, 0.0f);
    data.hairPrimaryLobe = 0.0f;
    data.hairSecondaryLobe = 0.0f;
    data.hairTransmissionLobe = 0.0f;
    data.hairScatter = 0.0f;
    data.gbufferBaseColor = float3(0.0f, 0.0f, 0.0f);
    data.gbufferNormalWS = safeNormalWS;
    data.gbufferMetallic = 0.0f;
    data.gbufferClearCoatMask = 0.0f;
    data.gbufferClearCoatNormalWS = safeNormalWS;
    data.gbufferClearCoatRoughness = 0.0f;
    data.gbufferSubsurfaceStrength = 0.0f;
    data.gbufferSubsurfaceThickness = 0.0f;
    data.gbufferSubsurfaceProfileIndex = 0.0f;
    data.gbufferAnisotropy = 0.0f;
    data.gbufferTangentWS = BurtSafeNormalize(abs(safeNormalWS.y) < 0.999f ? cross(float3(0.0f, 1.0f, 0.0f), safeNormalWS) : cross(float3(1.0f, 0.0f, 0.0f), safeNormalWS));
    data.gbufferSmoothness = 0.5f;
    data.gbufferOcclusion = 1.0f;
    data.gbufferReflectance = BURT_INPUT_DEFAULT_REFLECTANCE;
    data.gbufferRoughness = 0.5f;
    data.gbufferDiffuseColor = float3(0.0f, 0.0f, 0.0f);
    data.subsurfaceProfileIndex = 0.0f;
    data.subsurfaceTransmission = float3(0.0f, 0.0f, 0.0f);
    data.subsurfaceKernelWeight = float3(0.0f, 0.0f, 0.0f);
    data.subsurfaceIndirect = float3(0.0f, 0.0f, 0.0f);
    return data;
}

void BurtFillMainLightShadowShadingDebugData(
    float3 positionWS,
    float3 normalWS,
    out float3 shadowCascadeColor,
    out float shadowCascadeBlend,
    out float shadowDistanceFade,
    out float shadowPCSSRadius,
    out float shadowReceiverDepthDelta,
    out float shadowPCSSBlockerFraction)
{
    shadowCascadeColor = float3(0.0f, 0.0f, 0.0f);
    shadowCascadeBlend = 0.0f;
    shadowDistanceFade = 0.0f;
    shadowPCSSRadius = 0.0f;
    shadowReceiverDepthDelta = 0.0f;
    shadowPCSSBlockerFraction = 0.0f;

    if (!BurtIsShadingDebugEnabled())
    {
        return;
    }

    bool needsShadowDebug = BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_CASCADE_INDEX)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_CASCADE_BLEND)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_DISTANCE_FADE)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_PCSS_RADIUS)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_RECEIVER_DEPTH_DELTA)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_PCSS_BLOCKER_FRACTION);
    if (!needsShadowDebug)
    {
        return;
    }

    shadowCascadeColor = BurtGetMainLightShadowCascadeDebugColor(positionWS);
    shadowCascadeBlend = BurtGetMainLightShadowCascadeBlendDebug(positionWS);
    shadowDistanceFade = BurtGetMainLightShadowDistanceFadeDebug(positionWS);
    shadowPCSSRadius = BurtGetMainLightShadowPCSSRadiusDebug(positionWS);
    shadowReceiverDepthDelta = BurtGetMainLightShadowReceiverDepthDeltaDebug(positionWS, normalWS);
    shadowPCSSBlockerFraction = BurtGetMainLightShadowPCSSBlockerFractionDebug(positionWS);
}

bool BurtTryEvaluateMaterialShadingDebug(BurtSurfaceData surfaceData, BurtShadingDebugData data, out float3 debugColor) // 尝试根据当前模式生成材质调试颜色。
{
    debugColor = float3(0.0f, 0.0f, 0.0f); // 先清空输出颜色，保证未命中任何模式时不会返回未初始化值。

    if (!BurtIsShadingDebugEnabled()) // 如果全局 debug 开关关闭，就不接管材质输出。
    {
        return false; // 返回 false，告诉调用方继续走正常 Lit 渲染路径。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ALBEDO)) // Albedo 模式显示贴图和 BaseColor 合成后的基础色。
    {
        debugColor = saturate(surfaceData.baseColor.rgb); // 把基础色限制到 0 到 1，避免普通 LDR 调试视图过曝。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_NORMAL_WS)) // NormalWS 模式显示法线贴图影响后的世界空间法线。
    {
        debugColor = BurtEncodeNormalWSForDebug(data.normalWS); // 把世界法线从方向值编码成可视化颜色。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SMOOTHNESS)) // Smoothness 模式显示当前材质光滑度。
    {
        debugColor = float3(surfaceData.smoothness, surfaceData.smoothness, surfaceData.smoothness); // 把单通道光滑度复制到 RGB，形成灰度图。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_METALLIC)) // Metallic 模式显示当前材质金属度。
    {
        debugColor = float3(surfaceData.metallic, surfaceData.metallic, surfaceData.metallic); // 把单通道金属度复制到 RGB，形成灰度图。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_OCCLUSION)) // Occlusion 模式显示当前材质环境遮蔽。
    {
        debugColor = float3(surfaceData.occlusion, surfaceData.occlusion, surfaceData.occlusion); // 把单通道环境遮蔽复制到 RGB，形成灰度图。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_HEIGHT))
    {
        debugColor = float3(surfaceData.height, surfaceData.height, surfaceData.height);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_REFLECTANCE)) // Reflectance 模式显示材质介质反射率。
    {
        debugColor = float3(data.reflectance, data.reflectance, data.reflectance); // 直接显示 reflectance，0.5 对应常见非金属 F0=0.04。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ROUGHNESS)) // Roughness 模式显示材质感知粗糙度。
    {
        debugColor = float3(data.perceptualRoughness, data.perceptualRoughness, data.perceptualRoughness); // 把粗糙度复制到 RGB，越黑表示越光滑。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SPECULAR_AA_ROUGHNESS)) // SpecularAARoughness 模式显示高光实际粗糙度。
    {
        debugColor = float3(data.specularAARoughness, data.specularAARoughness, data.specularAARoughness); // 越亮表示 Specular AA 把高光拓得越宽。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SPECULAR_ENERGY_COMPENSATION)) // SpecularEnergyCompensation 模式显示直接高光能量补偿。
    {
        debugColor = saturate((data.specularEnergyCompensation - 1.0f) * 0.5f); // 黑色表示无补偿，越亮表示多次散射补偿越强，2 倍补偿约为 0.5 灰。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_INDIRECT_SPECULAR_ENERGY_COMPENSATION)) // IndirectSpecularEnergyCompensation 模式显示间接高光能量补偿。
    {
        debugColor = saturate((data.indirectSpecularEnergyCompensation - 1.0f) * 0.5f); // 黑色表示无补偿，越亮表示 Reflection Probe 高光补偿越强，2 倍补偿约为 0.5 灰。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ENERGY_PRESERVATION)) // EnergyPreservation 模式显示底层 diffuse 保能比例。
    {
        debugColor = float3(data.energyPreservation, data.energyPreservation, data.energyPreservation); // 越亮表示 diffuse 保留越多，越暗表示 specular 顶层占用能量越多。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SPECULAR_OCCLUSION)) // SpecularOcclusion 模式显示间接高光遮蔽。
    {
        debugColor = float3(data.specularOcclusion, data.specularOcclusion, data.specularOcclusion); // 越亮表示反射探针被保留越多。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIFFUSE_COLOR)) // DiffuseColor 模式显示 XRender GenericData.DiffuseColor。
    {
        debugColor = saturate(data.diffuseColor); // 金属度越高 diffuse 越暗，方便检查 metallic 是否正确转移到 F0。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIRECT_BRDF_D)) // DirectBRDFD 模式显示 GGX D 项。
    {
        float visibleD = saturate(data.directBRDFD * 0.05f); // D 项在极光滑时会很高，所以缩放 0.05 让普通范围更容易观察。
        debugColor = float3(visibleD, visibleD, visibleD); // 把缩放后的 D 项复制到 RGB 形成灰度调试图。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIRECT_BRDF_VISIBILITY)) // DirectBRDFVisibility 模式显示 Smith Joint Visibility。
    {
        float visibleVisibility = saturate(data.directBRDFVisibility); // 越亮表示几何遮蔽越少，掠射角可能接近白色。
        debugColor = float3(visibleVisibility, visibleVisibility, visibleVisibility); // 把单通道 visibility 复制到 RGB。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIRECT_BRDF_FRESNEL)) // DirectBRDFFresnel 模式显示 Schlick Fresnel。
    {
        debugColor = saturate(data.directBRDFFresnel); // 可以直接观察 reflectance、metallic 和视角对 F 的影响。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIRECT_DIFFUSE_LOBE)) // DirectDiffuseLobe 模式显示 Lambert / Burley diffuse lobe。
    {
        float visibleDiffuseLobe = saturate(data.directDiffuseLobe); // Lambert 默认约为 0.318，切 Burley 后会随角度和 roughness 改变。
        debugColor = float3(visibleDiffuseLobe, visibleDiffuseLobe, visibleDiffuseLobe); // 把 diffuse lobe 复制到 RGB 形成灰度调试图。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIRECT_DIFFUSE_BRDF)) // DirectDiffuseBRDF 模式显示未乘灯光的 diffuse BRDF。
    {
        debugColor = saturate(data.directDiffuseBRDF); // 只看材质、diffuse lobe 和 EnergyPreservation，不含灯光颜色、NdotL 或阴影。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIRECT_SPECULAR_BRDF)) // DirectSpecularBRDF 模式显示未乘灯光的 specular BRDF。
    {
        debugColor = saturate(data.directSpecularBRDF); // 只看 D/V/F 和 EnergyCompensation，不含灯光颜色、NdotL 或阴影。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_HAIR_PRIMARY_LOBE)) // HairPrimaryLobe 模式显示 Hair R/primary 高光 lobe。
    {
        float visiblePrimary = saturate(data.hairPrimaryLobe * 0.05f); // Hair lobe 峰值可能很高，沿用 DirectBRDFD 的缩放口径。
        debugColor = float3(visiblePrimary, visiblePrimary, visiblePrimary);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_HAIR_SECONDARY_LOBE)) // HairSecondaryLobe 模式显示 Hair TT/secondary 高光 lobe。
    {
        float visibleSecondary = saturate(data.hairSecondaryLobe * 0.25f); // secondary lobe 通常低于 primary，给更高可见度。
        debugColor = float3(visibleSecondary, visibleSecondary, visibleSecondary);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_HAIR_TRANSMISSION_LOBE)) // HairTransmissionLobe 模式显示 Hair 背光透射近似。
    {
        float visibleTransmission = saturate(data.hairTransmissionLobe);
        debugColor = float3(visibleTransmission, visibleTransmission, visibleTransmission);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_HAIR_SCATTER)) // HairScatter 模式显示参与 Hair lighting 的 scatter。
    {
        debugColor = float3(data.hairScatter, data.hairScatter, data.hairScatter);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SPECULAR_AA_NORMAL_VARIANCE)) // SpecularAANormalVariance 模式显示 Normal Filtering 的法线方差。
    {
        float visibleVariance = saturate(data.specularAANormalVariance * 100.0f); // 方差通常很小，放大 100 倍后更容易看出法线贴图引起的拓宽区域。
        debugColor = float3(visibleVariance, visibleVariance, visibleVariance); // 把放大后的法线方差复制到 RGB。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SPECULAR_AA_ROUGHNESS_DELTA)) // SpecularAARoughnessDelta 模式显示 Specular AA 增加的 roughness。
    {
        float visibleRoughnessDelta = saturate(data.specularAARoughnessDelta * 5.0f); // delta 最大约受 threshold 限制，放大 5 倍能让 0.2 附近接近白色。
        debugColor = float3(visibleRoughnessDelta, visibleRoughnessDelta, visibleRoughnessDelta); // 把放大后的 roughness delta 复制到 RGB。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_INDIRECT_SPECULAR_DFG)) // IndirectSpecularDFG 模式显示间接高光预积分 DFG.xy。
    {
        debugColor = saturate(float3(data.indirectSpecularDFG.x, data.indirectSpecularDFG.y, 0.0f)); // R/G 分别显示 DFG.x/y，B 保持 0 便于区分。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_INDIRECT_SPECULAR_ENV_BRDF)) // IndirectSpecularEnvBRDF 模式显示 F0/F90 套用 DFG 后的环境 BRDF。
    {
        debugColor = saturate(data.indirectSpecularEnvBRDF); // Reflection Probe 会乘这个项，可用来确认 DFG 和 F0 是否合理。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SUBSURFACE_PROFILE_ID))
    {
        float visibleProfileIndex = saturate(data.subsurfaceProfileIndex / max(BURT_SHADING_DEBUG_SUBSURFACE_PROFILE_SLOT_COUNT - 1.0f, 1.0f));
        debugColor = float3(visibleProfileIndex, visibleProfileIndex, visibleProfileIndex);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SUBSURFACE_TRANSMISSION))
    {
        debugColor = saturate(data.subsurfaceTransmission);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SUBSURFACE_KERNEL_WEIGHT))
    {
        debugColor = saturate(data.subsurfaceKernelWeight * 4.0f);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SUBSURFACE_INDIRECT))
    {
        debugColor = max(data.subsurfaceIndirect, float3(0.0f, 0.0f, 0.0f));
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_BASE_COLOR)) // GBufferBaseColor 模式显示编码再解码后的 BaseColor。
    {
        debugColor = saturate(data.gbufferBaseColor); // 观察 GBuffer0.rgb 是否能还原材质基础色。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_NORMAL_WS)) // GBufferNormalWS 模式显示编码再解码后的向量槽。
    {
        debugColor = BurtEncodeNormalWSForDebug(data.gbufferNormalWS); // 把解码法线从方向值编码成屏幕可读颜色。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_METALLIC)) // GBufferMetallic 模式显示解码后的材质通道。
    {
        debugColor = float3(data.gbufferMetallic, data.gbufferMetallic, data.gbufferMetallic); // 把单通道材质值复制到 RGB。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_CLEAR_COAT_MASK))
    {
        debugColor = float3(data.gbufferClearCoatMask, data.gbufferClearCoatMask, data.gbufferClearCoatMask);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_CLEAR_COAT_NORMAL_WS))
    {
        debugColor = BurtEncodeNormalWSForDebug(data.gbufferClearCoatNormalWS);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_CLEAR_COAT_ROUGHNESS))
    {
        debugColor = float3(data.gbufferClearCoatRoughness, data.gbufferClearCoatRoughness, data.gbufferClearCoatRoughness);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_SUBSURFACE_STRENGTH))
    {
        debugColor = float3(data.gbufferSubsurfaceStrength, data.gbufferSubsurfaceStrength, data.gbufferSubsurfaceStrength);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_SUBSURFACE_THICKNESS))
    {
        debugColor = float3(data.gbufferSubsurfaceThickness, data.gbufferSubsurfaceThickness, data.gbufferSubsurfaceThickness);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_SUBSURFACE_PROFILE_INDEX))
    {
        float visibleProfileIndex = saturate(data.gbufferSubsurfaceProfileIndex / max(BURT_SHADING_DEBUG_SUBSURFACE_PROFILE_SLOT_COUNT - 1.0f, 1.0f));
        debugColor = float3(visibleProfileIndex, visibleProfileIndex, visibleProfileIndex);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_ANISOTROPY))
    {
        float encodedAnisotropy = saturate(data.gbufferAnisotropy * 0.5f + 0.5f);
        debugColor = float3(encodedAnisotropy, encodedAnisotropy, encodedAnisotropy);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_TANGENT_WS))
    {
        debugColor = BurtEncodeNormalWSForDebug(data.gbufferTangentWS);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_SMOOTHNESS)) // GBufferSmoothness 模式显示解码后的光滑度。
    {
        debugColor = float3(data.gbufferSmoothness, data.gbufferSmoothness, data.gbufferSmoothness); // Smoothness 越亮表示材质越光滑。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_OCCLUSION)) // GBufferOcclusion 模式显示解码后的 AO。
    {
        debugColor = float3(data.gbufferOcclusion, data.gbufferOcclusion, data.gbufferOcclusion); // AO 越暗表示间接光被遮蔽越强。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_REFLECTANCE)) // GBufferReflectance 模式显示解码后的 XRender reflectance。
    {
        debugColor = float3(data.gbufferReflectance, data.gbufferReflectance, data.gbufferReflectance); // Reflectance 仍按 XRender 输入语义显示，不显示 F0。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_ROUGHNESS)) // GBufferRoughness 模式显示解码后还原出的 XRender Base.Roughness。
    {
        debugColor = float3(data.gbufferRoughness, data.gbufferRoughness, data.gbufferRoughness); // Roughness 越黑表示越光滑，口径和 Forward Roughness 一致。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_DIFFUSE_COLOR)) // GBufferDiffuseColor 模式显示 GBuffer 还原后的 DiffuseColor。
    {
        debugColor = saturate(data.gbufferDiffuseColor); // 观察 metallic 扣除 diffuse 后是否和 Forward DiffuseColor 一致。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DETAIL_LIGHTING)) // DetailLighting 模式显示中灰 BaseColor 下的 PBR 光照。
    {
        debugColor = max(data.detailLightingColor, float3(0.0f, 0.0f, 0.0f)); // 保留 HDR 光照强度但去掉负值，便于观察阴影、高光和间接光分布。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_INDIRECT_LIGHTING)) // IndirectLighting 模式只显示 PBR 间接光。
    {
        debugColor = max(data.indirectDiffuseColor + data.indirectSpecularColor, float3(0.0f, 0.0f, 0.0f)); // 把间接漫反射和间接高光相加后显示。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIRECT_DIFFUSE)) // DirectDiffuse 模式只显示直接漫反射。
    {
        debugColor = max(data.directDiffuseColor, float3(0.0f, 0.0f, 0.0f)); // 显示主光漫反射项，方便排查 1/PI、NdotL 和阴影。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIRECT_SPECULAR)) // DirectSpecular 模式只显示直接高光。
    {
        debugColor = max(data.directSpecularColor, float3(0.0f, 0.0f, 0.0f)); // 显示主光 GGX 高光项，方便排查 smoothness 拉满后高光是否过窄。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_LIGHTING)) // AdditionalLighting 模式只显示追加光直接光。
    {
        debugColor = max(data.additionalDiffuseColor + data.additionalSpecularColor, float3(0.0f, 0.0f, 0.0f));
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_DIFFUSE)) // AdditionalDiffuse 模式只显示追加光漫反射。
    {
        debugColor = max(data.additionalDiffuseColor, float3(0.0f, 0.0f, 0.0f));
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_SPECULAR)) // AdditionalSpecular 模式只显示追加光高光。
    {
        debugColor = max(data.additionalSpecularColor, float3(0.0f, 0.0f, 0.0f));
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_LIGHTING_UNSHADOWED)) // AdditionalLightingUnshadowed 模式显示追加光未乘阴影的贡献。
    {
        debugColor = max(data.additionalUnshadowedColor, float3(0.0f, 0.0f, 0.0f));
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_HAIR_ADDITIONAL_LIGHTING)) // HairAdditionalLighting 模式只显示 Hair 追加光。
    {
#if BURT_ACTIVE_HAIR_SHADING_MODEL
        debugColor = max(data.additionalDiffuseColor + data.additionalSpecularColor, float3(0.0f, 0.0f, 0.0f));
#elif BURT_ENABLE_HAIR_SHADING
        float3 hairAdditionalLighting = max(data.additionalDiffuseColor + data.additionalSpecularColor, float3(0.0f, 0.0f, 0.0f));
        debugColor = BurtIsActiveHairShadingModel(surfaceData.shadingModelID) ? hairAdditionalLighting : float3(0.0f, 0.0f, 0.0f);
#else
        debugColor = float3(0.0f, 0.0f, 0.0f);
#endif
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_INDIRECT_DIFFUSE)) // IndirectDiffuse 模式只显示间接漫反射。
    {
        debugColor = max(data.indirectDiffuseColor, float3(0.0f, 0.0f, 0.0f)); // 显示 Unity SH / Light Probe 漫反射，方便检查间接漫反射是否存在。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_INDIRECT_SPECULAR)) // IndirectSpecular 模式只显示间接高光。
    {
        debugColor = max(data.indirectSpecularColor, float3(0.0f, 0.0f, 0.0f)); // 显示 Reflection Probe 镜面项，方便检查探针和 DFG 是否生效。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_ATTENUATION)) // ShadowAttenuation 模式显示主光阴影衰减。
    {
        debugColor = float3(data.shadowAttenuation, data.shadowAttenuation, data.shadowAttenuation); // 白色表示无阴影，黑色表示完全被遮挡。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_ATTENUATION)) // AdditionalShadowAttenuation 模式显示追加光阴影衰减。
    {
        debugColor = float3(data.additionalShadowAttenuation, data.additionalShadowAttenuation, data.additionalShadowAttenuation); // 白色表示追加光未被阴影遮挡，黑色表示完全被遮挡。
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_FACE))
    {
        debugColor = saturate(data.additionalShadowFaceColor);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_UV))
    {
        debugColor = saturate(data.additionalShadowUVColor);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_DEPTH))
    {
        debugColor = saturate(data.additionalShadowDepthColor);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_DEPTH_DELTA))
    {
        debugColor = saturate(data.additionalShadowDepthDeltaColor);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_CASCADE_INDEX)) // ShadowCascadeIndex 模式用颜色显示当前 CSM cascade。
    {
        debugColor = saturate(data.shadowCascadeColor); // 不同 cascade 使用固定调试颜色，黑色表示没有命中任何 cascade。
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_CASCADE_BLEND)) // ShadowCascadeBlend 模式显示 cascade 边界混合权重。
    {
        debugColor = float3(data.shadowCascadeBlend, data.shadowCascadeBlend, data.shadowCascadeBlend); // 白色表示正在强混合到下一级 cascade。
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_DISTANCE_FADE)) // ShadowDistanceFade 模式显示远距离阴影淡出。
    {
        debugColor = float3(data.shadowDistanceFade, data.shadowDistanceFade, data.shadowDistanceFade); // 白色表示已经淡出到无阴影。
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_PCSS_RADIUS)) // ShadowPCSSRadius 模式显示当前 PCSS 半影半径。
    {
        debugColor = float3(data.shadowPCSSRadius, data.shadowPCSSRadius, data.shadowPCSSRadius); // 白色表示接近当前 PCSS 最大过滤半径。
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_RECEIVER_DEPTH_DELTA)) // ShadowReceiverDepthDelta 模式显示 receiver 与 shadow map 深度差。
    {
        debugColor = float3(data.shadowReceiverDepthDelta, data.shadowReceiverDepthDelta, data.shadowReceiverDepthDelta); // 0.5 灰表示基本对齐，越亮表示 acne 压力越大，越暗表示 bias 过强。
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_PCSS_BLOCKER_FRACTION)) // ShadowPCSSBlockerFraction 模式显示 blocker 搜索命中比例。
    {
        debugColor = float3(data.shadowPCSSBlockerFraction, data.shadowPCSSBlockerFraction, data.shadowPCSSBlockerFraction); // 越白表示 blocker search 半径里越多采样落在遮挡体上。
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_AMBIENT_OCCLUSION)) // AmbientOcclusion 模式显示参与间接光遮蔽的 AO。
    {
        debugColor = float3(data.ambientOcclusion, data.ambientOcclusion, data.ambientOcclusion); // AO 越暗表示间接漫反射和间接高光越容易被压暗。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_EMISSION)) // Emission 模式只显示自发光贡献。
    {
        debugColor = max(data.emissionColor, float3(0.0f, 0.0f, 0.0f)); // 保留 HDR 自发光强度但去掉负值，方便检查贴图和颜色是否进入最终颜色。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_FINAL_LIGHTING)) // FinalLighting 模式显示写入 CameraColor 前的材质最终颜色。
    {
        debugColor = max(data.finalLightingColor, float3(0.0f, 0.0f, 0.0f)); // 显示 PBR 光照加自发光后的最终材质输出，用来对比后处理前后的差异。
        return true; // 返回 true，告诉调用方使用 debugColor 作为最终输出。
    }

    return false; // Depth、Shadow、SSR、TAA 等全屏/后处理 debug 由各自 pass 接管，材质 shader 不在这里输出。
}

#endif // BURT_ENABLE_SHADING_DEBUG
#endif // BURT_SHADING_DEBUG_INCLUDED // 结束 BurtShadingDebug.hlsl 的 include guard。
