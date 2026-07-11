// BurtRP shading-debug per-pixel payload and common helpers.
#ifndef BURT_SHADING_DEBUG_DATA_INCLUDED
#define BURT_SHADING_DEBUG_DATA_INCLUDED

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtPreSkinPosition.hlsl"

// Per-pixel debug payload prepared by the shading path.
struct BurtShadingDebugData
{
    // 保存世界空间法线，用于 NormalWS 调试模式。
    float3 NormalWS;

    // Stores mesh UV3 interpreted as XRender-style pre-skin object-space position for the PreSkin Position debug mode.
    float3 PreSkinPositionOS;

    // Stores the already-encoded visible color for paths that can only pass debug color through GBuffer.
    float3 PreSkinPositionDebugColor;

    // Marks whether this pixel actually provided pre-skin position data.
    float PreSkinPositionAvailable;

    // 保存 Detail Lighting 结果，不包含自发光；调用方会参考 XRender 先把 BaseColor 替换成 0.18 中灰再计算。
    float3 DetailLightingColor;

    // 保存直接漫反射贡献，已经包含主光颜色、NdotL 和阴影。
    float3 DirectDiffuseColor;

    // 保存直接镜面高光贡献，已经包含主光颜色、NdotL 和阴影。
    float3 DirectSpecularColor;

    // 保存间接漫反射贡献，主要来自 Unity SH / Light Probe。
    float3 IndirectDiffuseColor;

    // 保存间接镜面贡献，主要来自 Unity Reflection Probe / Sky Reflection。
    float3 IndirectSpecularColor;

    // 保存追加光直接漫反射，不包含主光、间接光和自发光。
    float3 AdditionalDiffuseColor;

    // 保存追加光直接镜面高光，不包含主光、间接光和自发光。
    float3 AdditionalSpecularColor;

    // 保存追加光直接光在不乘 additional shadow attenuation 时的贡献，用来确认 shadow 只减少追加光。
    float3 AdditionalUnshadowedColor;

    // 保存主光阴影衰减，1 表示不被阴影遮挡，0 表示完全落在阴影中。
    float ShadowAttenuation;

    // 保存追加光阴影衰减，1 表示不被追加光阴影遮挡，0 表示完全落在追加光阴影中。
    float AdditionalShadowAttenuation;

    float3 AdditionalShadowFaceColor;
    float3 AdditionalShadowUVColor;
    float3 AdditionalShadowDepthColor;
    float3 AdditionalShadowDepthDeltaColor;

    float3 PerObjectShadowObjectIndexColor;
    float3 PerObjectShadowSliceColor;
    float3 PerObjectShadowUVColor;
    float3 PerObjectShadowDepthColor;
    float3 PerObjectShadowCompareColor;
    float3 PerObjectShadowTransmissionDepthColor;
    float3 PerObjectShadowTransmissionThicknessColor;

    // 保存当前像素命中的 CSM cascade 调试颜色。
    float3 ShadowCascadeColor;

    // 保存当前像素在 cascade 边界处混合到下一级 cascade 的权重。
    float ShadowCascadeBlend;

    // 保存最后一级 cascade 淡出到无阴影的权重。
    float ShadowDistanceFade;

    // 保存当前像素估算出的 PCSS 半影半径，已归一化到 0 到 1。
    float ShadowPCSSRadius;

    // 保存 receiver depth 和 shadow map stored depth 的可视化差值。
    float ShadowReceiverDepthDelta;

    float MainLightShadowReceiverDepth;

    float MainLightShadowRawDepth;

    float MainLightShadowCompare;

    float3 MainLightShadowProjectionValidity;

    // 保存 PCSS blocker search 命中的 blocker 样本占比。
    float ShadowPCSSBlockerFraction;

    // 保存参与间接光遮蔽的 AO 输入，Forward 来自 surfaceData，Deferred 来自 GBuffer 解码。
    float AmbientOcclusion;

    // 保存自发光贡献，Forward 来自 Emission Map，Deferred 来自 GBuffer2.rgb。
    float3 EmissionColor;

    // 保存材质最终写入 CameraColor 前的颜色，包含 PBR 光照和自发光。
    float3 FinalLightingColor;

    // 保存材质 reflectance，用来确认材质面板输入的介质反射率。
    float Reflectance;

    // 保存材质感知粗糙度，也就是 1 - smoothness 后的结果。
    float PerceptualRoughness;

    // 保存直接高光实际使用的粗糙度，包含 Specular AA 对极光滑高光的拓宽。
    float SpecularAARoughness;

    // 保存直接高光多次散射能量补偿，1 表示没有补偿，大于 1 表示 LUT.z 正在补回能量。
    float3 SpecularEnergyCompensation;

    // 保存间接高光多次散射能量补偿，1 表示没有补偿，大于 1 表示反射探针高光正在补回能量。
    float3 IndirectSpecularEnergyCompensation;

    // 保存 XRender EnergyPreservation，1 表示底层 diffuse 完整保留，越小表示被 specular 顶层占用越多能量。
    float EnergyPreservation;

    // 保存间接高光遮蔽项，1 表示不遮蔽。
    float SpecularOcclusion;

    // 保存 XRender GenericData.DiffuseColor，方便观察 metallic 是否正确扣除了 diffuse。
    float3 DiffuseColor;

    // 保存直接 GGX D 项，数值可能很高，Debug View 会缩放显示。
    float DirectBRDFD;

    // 保存直接 Smith Joint Visibility 项。
    float DirectBRDFVisibility;

    // 保存直接 Schlick Fresnel 项。
    float3 DirectBRDFFresnel;

    // 保存直接 diffuse lobe，默认 Lambert 时约为 1 / PI。
    float DirectDiffuseLobe;

    // 保存未乘灯光颜色、NdotL 和阴影的直接 diffuse BRDF。
    float3 DirectDiffuseBRDF;

    // 保存未乘灯光颜色、NdotL 和阴影的直接 specular BRDF。
    float3 DirectSpecularBRDF;

    // 保存 Specular AA 使用的屏幕空间法线方差，通常很小，Debug View 会放大显示。
    float SpecularAANormalVariance;

    // 保存 Specular AA 额外增加的感知粗糙度。
    float SpecularAARoughnessDelta;

    // 保存间接高光 DFG.xy，Debug View 会显示为 R/G 两个通道。
    float2 IndirectSpecularDFG;

    // 保存 DFG 应用到 F0/F90 后的环境 BRDF。
    float3 IndirectSpecularEnvBRDF;

    // 保存 Hair primary/R lobe；Default Lit 固定为 0。
    float HairPrimaryLobe;

    // 保存 Hair secondary/TT lobe；Default Lit 固定为 0。
    float HairSecondaryLobe;

    // 保存 Hair 近似背光透射 lobe；Default Lit 固定为 0。
    float HairTransmissionLobe;

    // 保存 Hair lighting 使用的 scatter；Default Lit 固定为 0。
    float HairScatter;

    // 保存按 BurtGBuffer 约定编码再解码后的 BaseColor，用来提前验证 Deferred 材质还原。
    float3 GBufferBaseColor;

    // 保存按 BurtGBuffer octahedron 编码再解码后的世界空间向量槽。
    float3 GBufferNormalWS;

    // 保存 GBuffer 解码后的材质通道；Default Lit=metallic，Hair=scatter。
    float GBufferMetallic;

    float GBufferClearCoatMask;

    float3 GBufferClearCoatNormalWS;

    float GBufferClearCoatRoughness;

    float GBufferSubsurfaceStrength;

    float GBufferSubsurfaceThickness;

    float GBufferSubsurfaceProfileIndex;

    float GBufferAnisotropy;

    float3 GBufferTangentWS;

    // 保存 GBuffer 解码后的 Smoothness，Deferred 会再由它还原 Roughness。
    float GBufferSmoothness;

    // 保存 GBuffer 解码后的 Ambient Occlusion。
    float GBufferOcclusion;

    // 保存 GBuffer 解码后的 XRender Reflectance。
    float GBufferReflectance;

    // 保存从 GBuffer Smoothness 还原出的 XRender Base.Roughness。
    float GBufferRoughness;

    // 保存从 GBuffer 转成 PBRMaterialData 后的 DiffuseColor。
    float3 GBufferDiffuseColor;

    float SubsurfaceProfileIndex;

    float3 SubsurfaceTransmission;

    float3 SubsurfaceDirectTransmission;

    float3 SubsurfaceTransmissionBRDF;

    float SubsurfaceTransmissionShadow;

    float SubsurfaceTransmissionPhase;

    float SubsurfaceTransmissionThickness;

    float3 SubsurfaceKernelWeight;

    float3 SubsurfaceIndirect;

    float FoliageMask;

    float3 FoliageTransmission;

    float3 FoliageDirectTransmission;

    float3 FoliageTransmissionBRDF;

    float FoliageTransmissionShadow;

    float3 FoliageSpecularBRDF;

};

bool BurtIsShadingDebugEnabled() // 判断当前是否启用了任意 shading debug 模式。
{
    return _BurtShadingDebugEnabled > 0.5f; // 使用 0.5 作为阈值，兼容 C# 上传的 0/1 float 开关。
}

bool BurtIsSameShadingDebugMode(float mode, float expectedMode) // 判断当前模式是否等于指定模式。
{
    return abs(mode - expectedMode) < 0.5f; // 用半个整数范围做比较，避免 float/int 上传转换产生精度边界问题。
}

bool BurtNeedsAdditionalLightingUnshadowedShadingDebug()
{
    return BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_LIGHTING_UNSHADOWED);
}

bool BurtNeedsAdditionalShadowAttenuationShadingDebug()
{
    return BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_ATTENUATION);
}

bool BurtNeedsAdditionalShadowProjectionShadingDebug()
{
    return BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_FACE)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_UV)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_DEPTH)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_DEPTH_DELTA);
}

bool BurtNeedsMainLightShadowProjectionShadingDebug()
{
    return BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_MAIN_LIGHT_SHADOW_RECEIVER_DEPTH)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_MAIN_LIGHT_SHADOW_RAW_DEPTH)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_MAIN_LIGHT_SHADOW_COMPARE)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_MAIN_LIGHT_SHADOW_PROJECTION_VALIDITY);
}

bool BurtNeedsPerObjectShadowProjectionShadingDebug()
{
    return BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_OBJECT_INDEX)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_SLICE)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_UV)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_DEPTH)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_COMPARE);
}

bool BurtNeedsPerObjectShadowTransmissionShadingDebug()
{
    return BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_TRANSMISSION_DEPTH)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_TRANSMISSION_THICKNESS);
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

    data.NormalWS = safeNormalWS;
    data.PreSkinPositionOS = float3(0.0f, 0.0f, 0.0f);
    data.PreSkinPositionDebugColor = float3(0.0f, 0.0f, 0.0f);
    data.PreSkinPositionAvailable = 0.0f;
    data.DetailLightingColor = float3(0.0f, 0.0f, 0.0f);
    data.DirectDiffuseColor = float3(0.0f, 0.0f, 0.0f);
    data.DirectSpecularColor = float3(0.0f, 0.0f, 0.0f);
    data.IndirectDiffuseColor = float3(0.0f, 0.0f, 0.0f);
    data.IndirectSpecularColor = float3(0.0f, 0.0f, 0.0f);
    data.AdditionalDiffuseColor = float3(0.0f, 0.0f, 0.0f);
    data.AdditionalSpecularColor = float3(0.0f, 0.0f, 0.0f);
    data.AdditionalUnshadowedColor = float3(0.0f, 0.0f, 0.0f);
    data.ShadowAttenuation = 1.0f;
    data.AdditionalShadowAttenuation = 1.0f;
    data.AdditionalShadowFaceColor = float3(0.0f, 0.0f, 0.0f);
    data.AdditionalShadowUVColor = float3(0.0f, 0.0f, 0.0f);
    data.AdditionalShadowDepthColor = float3(0.0f, 0.0f, 0.0f);
    data.AdditionalShadowDepthDeltaColor = float3(0.0f, 0.0f, 0.0f);
    data.PerObjectShadowObjectIndexColor = float3(0.0f, 0.0f, 0.0f);
    data.PerObjectShadowSliceColor = float3(0.0f, 0.0f, 0.0f);
    data.PerObjectShadowUVColor = float3(0.0f, 0.0f, 0.0f);
    data.PerObjectShadowDepthColor = float3(0.0f, 0.0f, 0.0f);
    data.PerObjectShadowCompareColor = float3(0.0f, 0.0f, 0.0f);
    data.PerObjectShadowTransmissionDepthColor = float3(0.0f, 0.0f, 0.0f);
    data.PerObjectShadowTransmissionThicknessColor = float3(0.0f, 0.0f, 0.0f);
    data.ShadowCascadeColor = float3(0.0f, 0.0f, 0.0f);
    data.ShadowCascadeBlend = 0.0f;
    data.ShadowDistanceFade = 0.0f;
    data.ShadowPCSSRadius = 0.0f;
    data.ShadowReceiverDepthDelta = 0.0f;
    data.ShadowPCSSBlockerFraction = 0.0f;
    data.AmbientOcclusion = 1.0f;
    data.EmissionColor = float3(0.0f, 0.0f, 0.0f);
    data.FinalLightingColor = float3(0.0f, 0.0f, 0.0f);
    data.Reflectance = BURT_INPUT_DEFAULT_REFLECTANCE;
    data.PerceptualRoughness = 0.5f;
    data.SpecularAARoughness = 0.5f;
    data.SpecularEnergyCompensation = float3(1.0f, 1.0f, 1.0f);
    data.IndirectSpecularEnergyCompensation = float3(1.0f, 1.0f, 1.0f);
    data.EnergyPreservation = 1.0f;
    data.SpecularOcclusion = 1.0f;
    data.DiffuseColor = float3(0.0f, 0.0f, 0.0f);
    data.DirectBRDFD = 0.0f;
    data.DirectBRDFVisibility = 0.0f;
    data.DirectBRDFFresnel = float3(0.0f, 0.0f, 0.0f);
    data.DirectDiffuseLobe = 0.0f;
    data.DirectDiffuseBRDF = float3(0.0f, 0.0f, 0.0f);
    data.DirectSpecularBRDF = float3(0.0f, 0.0f, 0.0f);
    data.SpecularAANormalVariance = 0.0f;
    data.SpecularAARoughnessDelta = 0.0f;
    data.IndirectSpecularDFG = float2(0.0f, 0.0f);
    data.IndirectSpecularEnvBRDF = float3(0.0f, 0.0f, 0.0f);
    data.HairPrimaryLobe = 0.0f;
    data.HairSecondaryLobe = 0.0f;
    data.HairTransmissionLobe = 0.0f;
    data.HairScatter = 0.0f;
    data.GBufferBaseColor = float3(0.0f, 0.0f, 0.0f);
    data.GBufferNormalWS = safeNormalWS;
    data.GBufferMetallic = 0.0f;
    data.GBufferClearCoatMask = 0.0f;
    data.GBufferClearCoatNormalWS = safeNormalWS;
    data.GBufferClearCoatRoughness = 0.0f;
    data.GBufferSubsurfaceStrength = 0.0f;
    data.GBufferSubsurfaceThickness = 0.0f;
    data.GBufferSubsurfaceProfileIndex = 0.0f;
    data.GBufferAnisotropy = 0.0f;
    data.GBufferTangentWS = BurtSafeNormalize(abs(safeNormalWS.y) < 0.999f ? cross(float3(0.0f, 1.0f, 0.0f), safeNormalWS) : cross(float3(1.0f, 0.0f, 0.0f), safeNormalWS));
    data.GBufferSmoothness = 0.5f;
    data.GBufferOcclusion = 1.0f;
    data.GBufferReflectance = BURT_INPUT_DEFAULT_REFLECTANCE;
    data.GBufferRoughness = 0.5f;
    data.GBufferDiffuseColor = float3(0.0f, 0.0f, 0.0f);
    data.SubsurfaceProfileIndex = 0.0f;
    data.SubsurfaceTransmission = float3(0.0f, 0.0f, 0.0f);
    data.SubsurfaceDirectTransmission = float3(0.0f, 0.0f, 0.0f);
    data.SubsurfaceTransmissionBRDF = float3(0.0f, 0.0f, 0.0f);
    data.SubsurfaceTransmissionShadow = 1.0f;
    data.SubsurfaceTransmissionPhase = 0.0f;
    data.SubsurfaceTransmissionThickness = 0.0f;
    data.SubsurfaceKernelWeight = float3(0.0f, 0.0f, 0.0f);
    data.SubsurfaceIndirect = float3(0.0f, 0.0f, 0.0f);
    data.FoliageMask = 0.0f;
    data.FoliageTransmission = float3(0.0f, 0.0f, 0.0f);
    data.FoliageDirectTransmission = float3(0.0f, 0.0f, 0.0f);
    data.FoliageTransmissionBRDF = float3(0.0f, 0.0f, 0.0f);
    data.FoliageTransmissionShadow = 1.0f;
    data.FoliageSpecularBRDF = float3(0.0f, 0.0f, 0.0f);
    return data;
}

#endif // BURT_SHADING_DEBUG_DATA_INCLUDED
