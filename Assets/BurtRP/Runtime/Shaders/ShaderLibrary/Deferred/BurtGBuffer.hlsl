// BurtRP Deferred GBuffer 约定草案：只定义 shader 侧数据布局和编解码，不绑定 RenderTarget 生命周期
#ifndef BURT_GBUFFER_INCLUDED // 开�?include guard，防止同一�?shader 编译单元里重复定�?GBuffer 工具
#define BURT_GBUFFER_INCLUDED // 标记 BurtGBuffer.hlsl 已经被包含过，后续重�?include 会被跳过�?
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtBRDF.hlsl" // 引入 BurtSurfaceData、PBR 准备结构�?XRender 风格 reflectance/F0/roughness 工具�?
// GBuffer0 约定：rgb = baseColor，a = occlusion；baseColor 保持材质基础色，不预乘灯光或能量项�?// GBuffer1 约定：rg = octahedron directionWS，b = packed(shadingModelID, material channel)，a = smoothness；Default Lit �?normal，Hair �?strand direction�?// GBuffer2 约定：rgb = emission，a = reflectance；emission 建议使用 HDR RT，reflectance 继续�?XRender 语义重建 F0，不直接�?F0�?// GBuffer3 stores Clear Coat top-layer normalWS/mask/roughness, or Subsurface tint.rgb + packed power/distortion.
// GBuffer4 stores base tangentWS in rg, signed anisotropy in b, and packed Subsurface thickness/profile index in a.

// 保存 Deferred 解码后的材质数据；字段只覆盖 PBR shading 所需的最小集合，方便后续替换真实 GBuffer RT
struct BurtGBufferData
{
    // 保存材质基础色，�?Forward �?surfaceData.baseColor.rgb 保持一致
float3 baseColor;

    // 保存世界空间方向，解码后必须是单位向量；Default Lit=normalWS，Hair=strandDirectionWS
float3 normalWS;

    float3 clearCoatNormalWS;

    float3 tangentWS;

    float anisotropy;

    // 保存材质通道；Default Lit=metallic，Hair=packed(scatter, shift)。访问时优先使用下面的语�?helper，避免混用
float metallic;

    float materialChannel;

    // 保存光滑度，GBuffer 保留面板语义，后续统一�?smoothness -> perceptual roughness
float smoothness;

    // 保存感知粗糙度，解码后立即计算出来，方便 Debug 或后�?shading 直接读取
float perceptualRoughness;

    // 保存 XRender 风格 reflectance，避免把 F0 暴露成材质输入或直接写入 GBuffer
float reflectance;

    // 保存环境遮蔽，用于间接漫反射和间接高光遮蔽
float occlusion;

    // 保存自发光颜色；如果后续需�?HDR emission，GBuffer2 �?RT 格式要配合选择
float3 emission;

    // 保存 shading model id�?=Default Lit�?=Hair。它决定 vector/material 两个复用槽的语义
float shadingModelID;

    float clearCoatMask;

    float clearCoatRoughness;

    float subsurfaceStrength;

    float subsurfaceThickness;

    float subsurfacePower;

    float subsurfaceDistortion;

    float subsurfaceAmbient;

    float3 subsurfaceTint;

    float subsurfaceProfileIndex;
};

// 保存实际写入 RenderTarget 的五�?GBuffer 颜色；这里只定义编码结果，不负责 RT 创建或生命周期
struct BurtEncodedGBuffer
{
    // GBuffer0：baseColor.rgb + occlusion
float4 gbuffer0;

    // GBuffer1：octa directionWS.rg + packed(shadingModelID, material channel) + smoothness
float4 gbuffer1;

    // GBuffer2：emission.rgb + reflectance
float4 gbuffer2;

    float4 gbuffer3;

    float4 gbuffer4;
};

// Octahedron normal 编码的折叠函数；把背半球折回二维平面，节�?GBuffer normal 通道
float2 BurtWrapOctahedronNormal(float2 value)
{
    // 分量符号单独计算，避�?HLSL 向量三目表达式在不同后端产生兼容性问题
float2 signNotZero = float2(value.x >= 0.0f ? 1.0f : -1.0f, value.y >= 0.0f ? 1.0f : -1.0f);

    // 背半球折叠公式：�?1 - abs(yx) 保留边界连续性，再乘回原始符号
return (1.0f - abs(value.yx)) * signNotZero;
}

// 把世界空间单位向量编码成两个 0..1 通道；Deferred GBuffer1.rg 使用这个结果
float2 BurtEncodeNormalWSForGBuffer(float3 normalWS)
{
    // 先安全归一化，避免法线贴图或插值误差影�?octahedron 投影
float3 n = BurtSafeNormalize(normalWS);

    // 投影�?L1 单位八面体；分母做保护，避免异常零法线产�?NaN
float invL1 = rcp(max(abs(n.x) + abs(n.y) + abs(n.z), BURT_EPSILON));
    float2 encoded = n.xy * invL1;

    // 背半球需要折叠回二维平面，保证两个通道能恢复完整方向
if (n.z < 0.0f)
    {
        encoded = BurtWrapOctahedronNormal(encoded);
    }

    // �?[-1, 1] 映射�?[0, 1]，方便写入常规颜�?RT
return encoded * 0.5f + 0.5f;
}

// �?GBuffer1.rg 解码世界空间单位向量；Default Lit 把它�?normal，Hair 把它�?strand direction
float3 BurtDecodeNormalWSFromGBuffer(float2 encodedNormal)
{
    // �?[0, 1] 还原�?octahedron 平面上的 [-1, 1]
float2 f = encodedNormal * 2.0f - 1.0f;

    // 先按前半球重�?z，再通过下面的修正处理背半球折叠
float3 n = float3(f.x, f.y, 1.0f - abs(f.x) - abs(f.y));

    // z 为负表示来自折叠区域，需要把 xy 沿符号方向推回去
float t = saturate(-n.z);
    n.x += n.x >= 0.0f ? -t : t;
    n.y += n.y >= 0.0f ? -t : t;

    // 最后安全归一化，抵消 RT 量化和插值带来的长度误差
return BurtSafeNormalize(n);
}

// GBuffer1.b 复用一个半精度通道保存 shading model �?material scalar；Hair 再把 scatter/shift 压到 material scalar 内
static const float BURT_GBUFFER_SHADING_MODEL_PACK_COUNT = 4.0f;
static const float BURT_GBUFFER_SHADING_MODEL_PACK_SCALE = 0.999f;

float BurtEncodeMetallicAndShadingModelForGBuffer(float metallicOrScatter, float shadingModelID)
{
    // Point-sampled ARGBHalf GBuffer1 can safely store four model buckets while keeping useful 0..1 material precision.
    float modelID = clamp(BurtResolveSurfaceShadingModel(shadingModelID), 0.0f, BURT_GBUFFER_SHADING_MODEL_PACK_COUNT - 1.0f);
    return (modelID + saturate(metallicOrScatter) * BURT_GBUFFER_SHADING_MODEL_PACK_SCALE) / BURT_GBUFFER_SHADING_MODEL_PACK_COUNT;
}

float BurtDecodeMetallicAndShadingModelFromGBuffer(float packedValue, out float shadingModelID)
{
    // The 0.999 encode scale prevents metallic=1 from spilling into the next shading model bucket.
    float scaled = saturate(packedValue) * BURT_GBUFFER_SHADING_MODEL_PACK_COUNT;
    shadingModelID = floor(min(scaled, BURT_GBUFFER_SHADING_MODEL_PACK_COUNT - BURT_EPSILON));
    return saturate((scaled - shadingModelID) / BURT_GBUFFER_SHADING_MODEL_PACK_SCALE);
}

static const float BURT_HAIR_SCATTER_PACK_DIMENSION = 32.0f;
static const float BURT_HAIR_SHIFT_PACK_DIMENSION = 16.0f;
static const float BURT_HAIR_SCATTER_PACK_MAX_BUCKET = BURT_HAIR_SCATTER_PACK_DIMENSION - 1.0f;
static const float BURT_HAIR_SHIFT_PACK_MAX_BUCKET = BURT_HAIR_SHIFT_PACK_DIMENSION - 1.0f;
static const float BURT_HAIR_MATERIAL_PACK_MAX_VALUE = BURT_HAIR_SCATTER_PACK_DIMENSION * BURT_HAIR_SHIFT_PACK_DIMENSION - 1.0f;

float BurtQuantizeHairMaterialValue(float value, float maxBucket)
{
    return floor(saturate(value) * maxBucket + 0.5f);
}

float BurtEncodeHairMaterialChannel(float hairScatter, float hairShiftScale)
{
    // Hair only has one material scalar inside GBuffer1.b; pack scatter and the longitudinal lobe shift scale together.
    float scatterBucket = BurtQuantizeHairMaterialValue(hairScatter, BURT_HAIR_SCATTER_PACK_MAX_BUCKET);
    float shiftBucket = BurtQuantizeHairMaterialValue(hairShiftScale, BURT_HAIR_SHIFT_PACK_MAX_BUCKET);
    return (shiftBucket * BURT_HAIR_SCATTER_PACK_DIMENSION + scatterBucket) / BURT_HAIR_MATERIAL_PACK_MAX_VALUE;
}

void BurtDecodeHairMaterialChannel(float packedHairMaterial, out float hairScatter, out float hairShiftScale)
{
    float packedBucket = floor(saturate(packedHairMaterial) * BURT_HAIR_MATERIAL_PACK_MAX_VALUE + 0.5f);
    float shiftBucket = floor(packedBucket / BURT_HAIR_SCATTER_PACK_DIMENSION);
    float scatterBucket = packedBucket - shiftBucket * BURT_HAIR_SCATTER_PACK_DIMENSION;

    hairScatter = saturate(scatterBucket / BURT_HAIR_SCATTER_PACK_MAX_BUCKET);
    hairShiftScale = saturate(shiftBucket / BURT_HAIR_SHIFT_PACK_MAX_BUCKET);
}

float BurtEncodeSubsurfacePowerForGBuffer(float power)
{
    return saturate((BurtClampSubsurfacePower(power) - BURT_SUBSURFACE_POWER_MIN) / max(BURT_SUBSURFACE_POWER_MAX - BURT_SUBSURFACE_POWER_MIN, BURT_EPSILON));
}

float BurtDecodeSubsurfacePowerFromGBuffer(float encodedPower)
{
    return BurtClampSubsurfacePower(lerp(BURT_SUBSURFACE_POWER_MIN, BURT_SUBSURFACE_POWER_MAX, saturate(encodedPower)));
}

static const float BURT_SUBSURFACE_CONTROL_PACK_DIMENSION = 32.0f;
static const float BURT_SUBSURFACE_CONTROL_PACK_MAX_BUCKET = BURT_SUBSURFACE_CONTROL_PACK_DIMENSION - 1.0f;
static const float BURT_SUBSURFACE_CONTROL_PACK_MAX_VALUE = BURT_SUBSURFACE_CONTROL_PACK_DIMENSION * BURT_SUBSURFACE_CONTROL_PACK_DIMENSION - 1.0f;
static const float BURT_SUBSURFACE_THICKNESS_PACK_DIMENSION = 64.0f;
static const float BURT_SUBSURFACE_THICKNESS_PACK_MAX_BUCKET = BURT_SUBSURFACE_THICKNESS_PACK_DIMENSION - 1.0f;
static const float BURT_SUBSURFACE_PROFILE_PACK_DIMENSION = BURT_SUBSURFACE_PROFILE_COUNT;
static const float BURT_SUBSURFACE_PROFILE_PACK_MAX_BUCKET = BURT_SUBSURFACE_PROFILE_PACK_DIMENSION - 1.0f;
static const float BURT_SUBSURFACE_THICKNESS_PROFILE_PACK_MAX_VALUE = BURT_SUBSURFACE_THICKNESS_PACK_DIMENSION * BURT_SUBSURFACE_PROFILE_PACK_DIMENSION - 1.0f;

float BurtQuantizeSubsurfaceControlValue(float value)
{
    return floor(saturate(value) * BURT_SUBSURFACE_CONTROL_PACK_MAX_BUCKET + 0.5f);
}

float BurtEncodeSubsurfacePowerAmbientForGBuffer(float power, float ambient)
{
    float powerBucket = BurtQuantizeSubsurfaceControlValue(BurtEncodeSubsurfacePowerForGBuffer(power));
    float ambientBucket = BurtQuantizeSubsurfaceControlValue(ambient);
    return (ambientBucket * BURT_SUBSURFACE_CONTROL_PACK_DIMENSION + powerBucket) / BURT_SUBSURFACE_CONTROL_PACK_MAX_VALUE;
}

void BurtDecodeSubsurfacePowerAmbientFromGBuffer(float packedControl, out float power, out float ambient)
{
    float packedBucket = floor(saturate(packedControl) * BURT_SUBSURFACE_CONTROL_PACK_MAX_VALUE + 0.5f);
    float ambientBucket = floor(packedBucket / BURT_SUBSURFACE_CONTROL_PACK_DIMENSION);
    float powerBucket = packedBucket - ambientBucket * BURT_SUBSURFACE_CONTROL_PACK_DIMENSION;
    power = BurtDecodeSubsurfacePowerFromGBuffer(powerBucket / BURT_SUBSURFACE_CONTROL_PACK_MAX_BUCKET);
    ambient = saturate(ambientBucket / BURT_SUBSURFACE_CONTROL_PACK_MAX_BUCKET);
}

float BurtEncodeSubsurfaceThicknessProfileForGBuffer(float thickness, float profileIndex)
{
    float thicknessBucket = floor(saturate(thickness) * BURT_SUBSURFACE_THICKNESS_PACK_MAX_BUCKET + 0.5f);
    float profileBucket = BurtClampSubsurfaceProfileIndex(profileIndex);
    return (profileBucket * BURT_SUBSURFACE_THICKNESS_PACK_DIMENSION + thicknessBucket) / BURT_SUBSURFACE_THICKNESS_PROFILE_PACK_MAX_VALUE;
}

void BurtDecodeSubsurfaceThicknessProfileFromGBuffer(float packedValue, out float thickness, out float profileIndex)
{
    float packedBucket = floor(saturate(packedValue) * BURT_SUBSURFACE_THICKNESS_PROFILE_PACK_MAX_VALUE + 0.5f);
    float profileBucket = floor(packedBucket / BURT_SUBSURFACE_THICKNESS_PACK_DIMENSION);
    float thicknessBucket = packedBucket - profileBucket * BURT_SUBSURFACE_THICKNESS_PACK_DIMENSION;
    thickness = saturate(thicknessBucket / BURT_SUBSURFACE_THICKNESS_PACK_MAX_BUCKET);
    profileIndex = BurtClampSubsurfaceProfileIndex(profileBucket);
}

// Creates semantic GBuffer data from material inputs. Hair passes use normalWS as the stored strand direction.
BurtGBufferData BurtCreateGBufferData(BurtSurfaceData surfaceData, float3 normalWS, float4 tangentWS, float3 emission)
{
    BurtGBufferData data;

    data.baseColor = surfaceData.baseColor.rgb;

    // Vector slot stores normalWS for Lit/ClearCoat/Subsurface and strand direction for Hair.
    data.normalWS = BurtSafeNormalize(normalWS);
    data.clearCoatNormalWS = data.normalWS;
    data.tangentWS = BurtOrthonormalizeTangentWS(data.normalWS, tangentWS.xyz);
    data.anisotropy = clamp(surfaceData.anisotropy, -1.0f, 1.0f);

#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    data.metallic = 0.0f;
    data.materialChannel = saturate(surfaceData.subsurfaceStrength);
#elif BURT_ENABLE_SUBSURFACE_SHADING
    if (BurtIsActiveSubsurfaceShadingModel(surfaceData.shadingModelID))
    {
        data.metallic = 0.0f;
        data.materialChannel = saturate(surfaceData.subsurfaceStrength);
    }
    else
    {
        data.metallic = saturate(surfaceData.metallic);
        data.materialChannel = data.metallic;
    }
#else
    data.metallic = saturate(surfaceData.metallic);
    data.materialChannel = data.metallic;
#endif
    data.smoothness = saturate(surfaceData.smoothness);
    data.reflectance = saturate(surfaceData.reflectance);
    data.occlusion = saturate(surfaceData.occlusion);

    data.perceptualRoughness = ClampPerceptualRoughness(PerceptualSmoothnessToPerceptualRoughness(data.smoothness));

    data.emission = max(emission, float3(0.0f, 0.0f, 0.0f));

    data.shadingModelID = BurtResolveSurfaceShadingModel(surfaceData.shadingModelID);
    data.clearCoatMask = 0.0f;
    data.clearCoatRoughness = 0.2f;
    data.subsurfaceStrength = 0.0f;
    data.subsurfaceThickness = BURT_SUBSURFACE_DEFAULT_THICKNESS;
    data.subsurfacePower = BURT_SUBSURFACE_DEFAULT_POWER;
    data.subsurfaceDistortion = BURT_SUBSURFACE_DEFAULT_DISTORTION;
    data.subsurfaceAmbient = BURT_SUBSURFACE_DEFAULT_AMBIENT;
    data.subsurfaceTint = BURT_SUBSURFACE_DEFAULT_TINT;
    data.subsurfaceProfileIndex = BURT_SUBSURFACE_DEFAULT_PROFILE_INDEX;

#if BURT_ACTIVE_CLEAR_COAT_SHADING_MODEL
    data.clearCoatMask = saturate(surfaceData.clearCoatMask);
    data.clearCoatRoughness = ClampPerceptualRoughness(surfaceData.clearCoatRoughness);
#elif BURT_ENABLE_CLEAR_COAT_SHADING
    if (BurtIsActiveClearCoatShadingModel(data.shadingModelID))
    {
        data.clearCoatMask = saturate(surfaceData.clearCoatMask);
        data.clearCoatRoughness = ClampPerceptualRoughness(surfaceData.clearCoatRoughness);
    }
#endif

#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    data.subsurfaceStrength = saturate(data.materialChannel);
    data.subsurfaceThickness = saturate(surfaceData.subsurfaceThickness);
    data.subsurfacePower = BurtClampSubsurfacePower(surfaceData.subsurfacePower);
    data.subsurfaceDistortion = saturate(surfaceData.subsurfaceDistortion);
    data.subsurfaceAmbient = saturate(surfaceData.subsurfaceAmbient);
    data.subsurfaceTint = max(surfaceData.subsurfaceTint, float3(0.0f, 0.0f, 0.0f));
    data.subsurfaceProfileIndex = BurtClampSubsurfaceProfileIndex(surfaceData.subsurfaceProfileIndex);
#elif BURT_ENABLE_SUBSURFACE_SHADING
    if (BurtIsActiveSubsurfaceShadingModel(data.shadingModelID))
    {
        data.subsurfaceStrength = saturate(data.materialChannel);
        data.subsurfaceThickness = saturate(surfaceData.subsurfaceThickness);
        data.subsurfacePower = BurtClampSubsurfacePower(surfaceData.subsurfacePower);
        data.subsurfaceDistortion = saturate(surfaceData.subsurfaceDistortion);
        data.subsurfaceAmbient = saturate(surfaceData.subsurfaceAmbient);
        data.subsurfaceTint = max(surfaceData.subsurfaceTint, float3(0.0f, 0.0f, 0.0f));
        data.subsurfaceProfileIndex = BurtClampSubsurfaceProfileIndex(surfaceData.subsurfaceProfileIndex);
    }
#endif

    return data;
}

// Hair GBuffer keeps one scalar material channel: packed(scatter, lobe shift scale).
BurtSurfaceData BurtApplyHairGBufferSurfaceSemantics(BurtSurfaceData surfaceData, float hairScatter, float hairShiftScale)
{
    surfaceData.shadingModelID = BURT_SHADING_MODEL_HAIR;
    surfaceData.metallic = BurtEncodeHairMaterialChannel(hairScatter, hairShiftScale);
    return surfaceData;
}

BurtSurfaceData BurtApplyHairGBufferSurfaceSemantics(BurtSurfaceData surfaceData, float hairScatter)
{
    return BurtApplyHairGBufferSurfaceSemantics(surfaceData, hairScatter, 1.0f);
}

BurtGBufferData BurtCreateGBufferData(BurtSurfaceData surfaceData, float3 normalWS, float3 emission)
{
    float3 safeNormalWS = BurtSafeNormalize(normalWS);
    return BurtCreateGBufferData(surfaceData, safeNormalWS, float4(BurtCreateFallbackTangentWS(safeNormalWS), 1.0f), emission);
}

BurtGBufferData BurtCreateHairGBufferData(BurtSurfaceData surfaceData, float3 strandDirectionWS, float3 emission)
{
    surfaceData.shadingModelID = BURT_SHADING_MODEL_HAIR;
    return BurtCreateGBufferData(surfaceData, strandDirectionWS, emission);
}

BurtGBufferData BurtCreateClearCoatGBufferData(BurtSurfaceData surfaceData, float3 normalWS, float4 tangentWS, float3 clearCoatNormalWS, float3 emission)
{
    surfaceData.shadingModelID = BURT_SHADING_MODEL_CLEAR_COAT;
    BurtGBufferData data = BurtCreateGBufferData(surfaceData, normalWS, tangentWS, emission);
    data.clearCoatNormalWS = BurtSafeNormalize(clearCoatNormalWS);
    return data;
}

BurtGBufferData BurtCreateClearCoatGBufferData(BurtSurfaceData surfaceData, float3 normalWS, float3 clearCoatNormalWS, float3 emission)
{
    surfaceData.shadingModelID = BURT_SHADING_MODEL_CLEAR_COAT;
    BurtGBufferData data = BurtCreateGBufferData(surfaceData, normalWS, emission);
    data.clearCoatNormalWS = BurtSafeNormalize(clearCoatNormalWS);
    return data;
}

BurtGBufferData BurtCreateClearCoatGBufferData(BurtSurfaceData surfaceData, float3 normalWS, float3 emission)
{
    return BurtCreateClearCoatGBufferData(surfaceData, normalWS, normalWS, emission);
}

BurtGBufferData BurtCreateSubsurfaceGBufferData(BurtSurfaceData surfaceData, float3 normalWS, float3 emission)
{
    surfaceData.shadingModelID = BURT_SHADING_MODEL_SUBSURFACE;
    return BurtCreateGBufferData(surfaceData, normalWS, emission);
}

BurtGBufferData BurtCreateSubsurfaceGBufferData(BurtSurfaceData surfaceData, float3 normalWS, float4 tangentWS, float3 emission)
{
    surfaceData.shadingModelID = BURT_SHADING_MODEL_SUBSURFACE;
    return BurtCreateGBufferData(surfaceData, normalWS, tangentWS, emission);
}

float3 BurtGetGBufferDirectionWS(BurtGBufferData gbufferData)
{
    return gbufferData.normalWS;
}

float3 BurtGetDefaultLitNormalWS(BurtGBufferData gbufferData)
{
    return gbufferData.normalWS;
}

float3 BurtGetClearCoatNormalWS(BurtGBufferData gbufferData)
{
#if BURT_ACTIVE_CLEAR_COAT_SHADING_MODEL
    return gbufferData.clearCoatNormalWS;
#elif BURT_ENABLE_CLEAR_COAT_SHADING
    return BurtIsActiveClearCoatShadingModel(gbufferData.shadingModelID) ? gbufferData.clearCoatNormalWS : gbufferData.normalWS;
#else
    return gbufferData.normalWS;
#endif
}

float3 BurtGetHairStrandDirectionWS(BurtGBufferData gbufferData)
{
    return gbufferData.normalWS;
}

float BurtGetDefaultLitMetallic(BurtGBufferData gbufferData)
{
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    return 0.0f;
#elif BURT_ENABLE_SUBSURFACE_SHADING
    return BurtIsActiveSubsurfaceShadingModel(gbufferData.shadingModelID) ? 0.0f : saturate(gbufferData.metallic);
#else
    return saturate(gbufferData.metallic);
#endif
}

float BurtGetClearCoatMask(BurtGBufferData gbufferData)
{
#if BURT_ACTIVE_CLEAR_COAT_SHADING_MODEL
    return saturate(gbufferData.clearCoatMask);
#elif BURT_ENABLE_CLEAR_COAT_SHADING
    return BurtIsActiveClearCoatShadingModel(gbufferData.shadingModelID) ? saturate(gbufferData.clearCoatMask) : 0.0f;
#else
    return 0.0f;
#endif
}

float BurtGetClearCoatRoughness(BurtGBufferData gbufferData)
{
#if BURT_ACTIVE_CLEAR_COAT_SHADING_MODEL
    return ClampPerceptualRoughness(gbufferData.clearCoatRoughness);
#elif BURT_ENABLE_CLEAR_COAT_SHADING
    return BurtIsActiveClearCoatShadingModel(gbufferData.shadingModelID) ? ClampPerceptualRoughness(gbufferData.clearCoatRoughness) : 0.2f;
#else
    return 0.2f;
#endif
}

float3 BurtGetReflectionNormalWS(BurtGBufferData gbufferData)
{
    float clearCoatMask = BurtGetClearCoatMask(gbufferData);
    return BurtSafeNormalize(lerp(gbufferData.normalWS, BurtGetClearCoatNormalWS(gbufferData), clearCoatMask));
}

float BurtGetReflectionRoughness(BurtGBufferData gbufferData)
{
    float clearCoatMask = BurtGetClearCoatMask(gbufferData);
    return saturate(lerp(gbufferData.perceptualRoughness, BurtGetClearCoatRoughness(gbufferData), clearCoatMask));
}

float BurtGetSubsurfaceStrength(BurtGBufferData gbufferData)
{
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    return saturate(gbufferData.subsurfaceStrength);
#elif BURT_ENABLE_SUBSURFACE_SHADING
    return BurtIsActiveSubsurfaceShadingModel(gbufferData.shadingModelID) ? saturate(gbufferData.subsurfaceStrength) : 0.0f;
#else
    return 0.0f;
#endif
}

float BurtGetSubsurfaceThickness(BurtGBufferData gbufferData)
{
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    return saturate(gbufferData.subsurfaceThickness);
#elif BURT_ENABLE_SUBSURFACE_SHADING
    return BurtIsActiveSubsurfaceShadingModel(gbufferData.shadingModelID) ? saturate(gbufferData.subsurfaceThickness) : BURT_SUBSURFACE_DEFAULT_THICKNESS;
#else
    return BURT_SUBSURFACE_DEFAULT_THICKNESS;
#endif
}

float BurtGetSubsurfacePower(BurtGBufferData gbufferData)
{
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    return BurtClampSubsurfacePower(gbufferData.subsurfacePower);
#elif BURT_ENABLE_SUBSURFACE_SHADING
    return BurtIsActiveSubsurfaceShadingModel(gbufferData.shadingModelID) ? BurtClampSubsurfacePower(gbufferData.subsurfacePower) : BURT_SUBSURFACE_DEFAULT_POWER;
#else
    return BURT_SUBSURFACE_DEFAULT_POWER;
#endif
}

float BurtGetSubsurfaceDistortion(BurtGBufferData gbufferData)
{
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    return saturate(gbufferData.subsurfaceDistortion);
#elif BURT_ENABLE_SUBSURFACE_SHADING
    return BurtIsActiveSubsurfaceShadingModel(gbufferData.shadingModelID) ? saturate(gbufferData.subsurfaceDistortion) : BURT_SUBSURFACE_DEFAULT_DISTORTION;
#else
    return BURT_SUBSURFACE_DEFAULT_DISTORTION;
#endif
}

float BurtGetSubsurfaceAmbient(BurtGBufferData gbufferData)
{
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    return saturate(gbufferData.subsurfaceAmbient);
#elif BURT_ENABLE_SUBSURFACE_SHADING
    return BurtIsActiveSubsurfaceShadingModel(gbufferData.shadingModelID) ? saturate(gbufferData.subsurfaceAmbient) : BURT_SUBSURFACE_DEFAULT_AMBIENT;
#else
    return BURT_SUBSURFACE_DEFAULT_AMBIENT;
#endif
}

float3 BurtGetSubsurfaceTint(BurtGBufferData gbufferData)
{
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    return max(gbufferData.subsurfaceTint, float3(0.0f, 0.0f, 0.0f));
#elif BURT_ENABLE_SUBSURFACE_SHADING
    return BurtIsActiveSubsurfaceShadingModel(gbufferData.shadingModelID) ? max(gbufferData.subsurfaceTint, float3(0.0f, 0.0f, 0.0f)) : BURT_SUBSURFACE_DEFAULT_TINT;
#else
    return BURT_SUBSURFACE_DEFAULT_TINT;
#endif
}

float BurtGetSubsurfaceProfileIndex(BurtGBufferData gbufferData)
{
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    return BurtClampSubsurfaceProfileIndex(gbufferData.subsurfaceProfileIndex);
#elif BURT_ENABLE_SUBSURFACE_SHADING
    return BurtIsActiveSubsurfaceShadingModel(gbufferData.shadingModelID) ? BurtClampSubsurfaceProfileIndex(gbufferData.subsurfaceProfileIndex) : BURT_SUBSURFACE_DEFAULT_PROFILE_INDEX;
#else
    return BURT_SUBSURFACE_DEFAULT_PROFILE_INDEX;
#endif
}

float BurtGetHairScatter(BurtGBufferData gbufferData)
{
    float hairScatter;
    float hairShiftScale;
    BurtDecodeHairMaterialChannel(gbufferData.metallic, hairScatter, hairShiftScale);
    return hairScatter;
}

float BurtGetHairLongitudinalShiftScale(BurtGBufferData gbufferData)
{
    float hairScatter;
    float hairShiftScale;
    BurtDecodeHairMaterialChannel(gbufferData.metallic, hairScatter, hairShiftScale);
    return hairShiftScale;
}

float BurtGetGBufferMaterialChannel(BurtGBufferData gbufferData)
{
#if BURT_ACTIVE_HAIR_SHADING_MODEL
    return BurtGetHairScatter(gbufferData);
#elif BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(gbufferData.shadingModelID))
    {
        return BurtGetHairScatter(gbufferData);
    }
#endif

#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    return BurtGetSubsurfaceStrength(gbufferData);
#elif BURT_ENABLE_SUBSURFACE_SHADING
    if (BurtIsActiveSubsurfaceShadingModel(gbufferData.shadingModelID))
    {
        return BurtGetSubsurfaceStrength(gbufferData);
    }
#endif

    return BurtGetDefaultLitMetallic(gbufferData);
}

float4 BurtEncodeClearCoatOrDefaultGBuffer3(BurtGBufferData data)
{
    float2 encodedClearCoatNormalWS = BurtEncodeNormalWSForGBuffer(data.clearCoatNormalWS);

#if BURT_ACTIVE_CLEAR_COAT_SHADING_MODEL
    return float4(
        encodedClearCoatNormalWS,
        saturate(data.clearCoatMask),
        ClampPerceptualRoughness(data.clearCoatRoughness));
#elif BURT_ENABLE_CLEAR_COAT_SHADING
    if (BurtIsActiveClearCoatShadingModel(data.shadingModelID))
    {
        return float4(
            encodedClearCoatNormalWS,
            saturate(data.clearCoatMask),
            ClampPerceptualRoughness(data.clearCoatRoughness));
    }
#endif

    return float4(encodedClearCoatNormalWS, 0.0f, 0.0f);
}

float4 BurtEncodeSubsurfaceGBuffer3(BurtGBufferData data)
{
    return float4(
        max(data.subsurfaceTint, float3(0.0f, 0.0f, 0.0f)),
        BurtEncodeSubsurfacePowerAmbientForGBuffer(data.subsurfacePower, data.subsurfaceAmbient));
}

float4 BurtEncodeGBuffer3(BurtGBufferData data)
{
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    return BurtEncodeSubsurfaceGBuffer3(data);
#elif BURT_ENABLE_SUBSURFACE_SHADING
    if (BurtIsActiveSubsurfaceShadingModel(data.shadingModelID))
    {
        return BurtEncodeSubsurfaceGBuffer3(data);
    }
#endif

    return BurtEncodeClearCoatOrDefaultGBuffer3(data);
}

float4 BurtEncodeGBuffer4(BurtGBufferData data)
{
    float2 encodedTangentWS = BurtEncodeNormalWSForGBuffer(data.tangentWS);

#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    return float4(
        encodedTangentWS,
        saturate(data.subsurfaceDistortion),
        BurtEncodeSubsurfaceThicknessProfileForGBuffer(data.subsurfaceThickness, data.subsurfaceProfileIndex));
#elif BURT_ENABLE_SUBSURFACE_SHADING
    if (BurtIsActiveSubsurfaceShadingModel(data.shadingModelID))
    {
        return float4(
            encodedTangentWS,
            saturate(data.subsurfaceDistortion),
            BurtEncodeSubsurfaceThicknessProfileForGBuffer(data.subsurfaceThickness, data.subsurfaceProfileIndex));
    }
#endif

    return float4(
        encodedTangentWS,
        clamp(data.anisotropy, -1.0f, 1.0f) * 0.5f + 0.5f,
        0.0f);
}

// Encodes semantic GBuffer data into the five MRT payloads.
BurtEncodedGBuffer BurtEncodeGBuffer(BurtGBufferData data)
{
    BurtEncodedGBuffer encoded;

    encoded.gbuffer0 = float4(saturate(data.baseColor), saturate(data.occlusion));

    encoded.gbuffer1 = float4(BurtEncodeNormalWSForGBuffer(data.normalWS), BurtEncodeMetallicAndShadingModelForGBuffer(data.materialChannel, data.shadingModelID), saturate(data.smoothness));

    encoded.gbuffer2 = float4(max(data.emission, float3(0.0f, 0.0f, 0.0f)), saturate(data.reflectance));

    encoded.gbuffer3 = BurtEncodeGBuffer3(data);
    encoded.gbuffer4 = BurtEncodeGBuffer4(data);

    return encoded;
}

// Decodes the five MRT payloads back into semantic GBuffer data.
BurtGBufferData BurtDecodeGBuffer(BurtEncodedGBuffer encoded)
{
    BurtGBufferData data;

    data.baseColor = saturate(encoded.gbuffer0.rgb);
    data.occlusion = saturate(encoded.gbuffer0.a);

    data.normalWS = BurtDecodeNormalWSFromGBuffer(encoded.gbuffer1.rg);
    data.materialChannel = BurtDecodeMetallicAndShadingModelFromGBuffer(encoded.gbuffer1.b, data.shadingModelID);
#if BURT_ACTIVE_CLEAR_COAT_SHADING_MODEL
    data.clearCoatNormalWS = BurtDecodeNormalWSFromGBuffer(encoded.gbuffer3.rg);
#elif BURT_ENABLE_CLEAR_COAT_SHADING
    if (BurtIsActiveClearCoatShadingModel(data.shadingModelID))
    {
        data.clearCoatNormalWS = BurtDecodeNormalWSFromGBuffer(encoded.gbuffer3.rg);
    }
    else
    {
        data.clearCoatNormalWS = data.normalWS;
    }
#else
    data.clearCoatNormalWS = data.normalWS;
#endif
    data.tangentWS = BurtOrthonormalizeTangentWS(data.normalWS, BurtDecodeNormalWSFromGBuffer(encoded.gbuffer4.rg));
#if BURT_ACTIVE_HAIR_SHADING_MODEL || BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    data.anisotropy = 0.0f;
#elif BURT_ENABLE_HAIR_SHADING || BURT_ENABLE_SUBSURFACE_SHADING
    if (BurtIsActiveHairShadingModel(data.shadingModelID) || BurtIsActiveSubsurfaceShadingModel(data.shadingModelID))
    {
        data.anisotropy = 0.0f;
    }
    else
    {
        data.anisotropy = clamp(encoded.gbuffer4.b * 2.0f - 1.0f, -1.0f, 1.0f);
    }
#else
    data.anisotropy = clamp(encoded.gbuffer4.b * 2.0f - 1.0f, -1.0f, 1.0f);
#endif
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    data.metallic = 0.0f;
#elif BURT_ENABLE_SUBSURFACE_SHADING
    if (BurtIsActiveSubsurfaceShadingModel(data.shadingModelID))
    {
        data.metallic = 0.0f;
    }
    else
    {
        data.metallic = data.materialChannel;
    }
#else
    data.metallic = data.materialChannel;
#endif
    data.clearCoatMask = 0.0f;
    data.clearCoatRoughness = 0.2f;
    data.subsurfaceStrength = 0.0f;
    data.subsurfaceThickness = BURT_SUBSURFACE_DEFAULT_THICKNESS;
    data.subsurfacePower = BURT_SUBSURFACE_DEFAULT_POWER;
    data.subsurfaceDistortion = BURT_SUBSURFACE_DEFAULT_DISTORTION;
    data.subsurfaceAmbient = BURT_SUBSURFACE_DEFAULT_AMBIENT;
    data.subsurfaceTint = BURT_SUBSURFACE_DEFAULT_TINT;
    data.subsurfaceProfileIndex = BURT_SUBSURFACE_DEFAULT_PROFILE_INDEX;

#if BURT_ACTIVE_CLEAR_COAT_SHADING_MODEL
    data.clearCoatMask = saturate(encoded.gbuffer3.b);
    data.clearCoatRoughness = ClampPerceptualRoughness(encoded.gbuffer3.a);
#elif BURT_ENABLE_CLEAR_COAT_SHADING
    if (BurtIsActiveClearCoatShadingModel(data.shadingModelID))
    {
        data.clearCoatMask = saturate(encoded.gbuffer3.b);
        data.clearCoatRoughness = ClampPerceptualRoughness(encoded.gbuffer3.a);
    }
#endif

#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    data.subsurfaceStrength = saturate(data.materialChannel);
    data.subsurfaceTint = max(encoded.gbuffer3.rgb, float3(0.0f, 0.0f, 0.0f));
    BurtDecodeSubsurfacePowerAmbientFromGBuffer(encoded.gbuffer3.a, data.subsurfacePower, data.subsurfaceAmbient);
    data.subsurfaceDistortion = saturate(encoded.gbuffer4.b);
    BurtDecodeSubsurfaceThicknessProfileFromGBuffer(encoded.gbuffer4.a, data.subsurfaceThickness, data.subsurfaceProfileIndex);
#elif BURT_ENABLE_SUBSURFACE_SHADING
    if (BurtIsActiveSubsurfaceShadingModel(data.shadingModelID))
    {
        data.subsurfaceStrength = saturate(data.materialChannel);
        data.subsurfaceTint = max(encoded.gbuffer3.rgb, float3(0.0f, 0.0f, 0.0f));
        BurtDecodeSubsurfacePowerAmbientFromGBuffer(encoded.gbuffer3.a, data.subsurfacePower, data.subsurfaceAmbient);
        data.subsurfaceDistortion = saturate(encoded.gbuffer4.b);
        BurtDecodeSubsurfaceThicknessProfileFromGBuffer(encoded.gbuffer4.a, data.subsurfaceThickness, data.subsurfaceProfileIndex);
    }
#endif
    data.smoothness = saturate(encoded.gbuffer1.a);

    data.perceptualRoughness = ClampPerceptualRoughness(PerceptualSmoothnessToPerceptualRoughness(data.smoothness));

    data.emission = max(encoded.gbuffer2.rgb, float3(0.0f, 0.0f, 0.0f));
    data.reflectance = saturate(encoded.gbuffer2.a);

    return data;
}

// Prepares PBR material data from decoded GBuffer data.
BurtPBRMaterialData BurtPreparePBRMaterialData(BurtGBufferData gbufferData)
{
    BurtPBRMaterialData materialData;

    materialData.baseColor = gbufferData.baseColor;
#if BURT_ACTIVE_HAIR_SHADING_MODEL
    materialData.metallic = 0.0f;
#elif BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(gbufferData.shadingModelID))
    {
        materialData.metallic = 0.0f;
    }
    else
    {
        materialData.metallic = BurtGetDefaultLitMetallic(gbufferData);
    }
#else
    materialData.metallic = BurtGetDefaultLitMetallic(gbufferData);
#endif
    materialData.clearCoatMask = BurtGetClearCoatMask(gbufferData);
    materialData.clearCoatRoughness = BurtGetClearCoatRoughness(gbufferData);
    materialData.subsurfaceStrength = BurtGetSubsurfaceStrength(gbufferData);
    materialData.subsurfaceThickness = BurtGetSubsurfaceThickness(gbufferData);
    materialData.subsurfacePower = BurtGetSubsurfacePower(gbufferData);
    materialData.subsurfaceDistortion = BurtGetSubsurfaceDistortion(gbufferData);
    materialData.subsurfaceAmbient = BurtGetSubsurfaceAmbient(gbufferData);
    materialData.subsurfaceTint = BurtGetSubsurfaceTint(gbufferData);
    materialData.subsurfaceProfileIndex = BurtGetSubsurfaceProfileIndex(gbufferData);
    materialData.reflectance = gbufferData.reflectance;
    materialData.occlusion = gbufferData.occlusion;
    materialData.smoothness = gbufferData.smoothness;
#if BURT_ACTIVE_HAIR_SHADING_MODEL
    materialData.anisotropy = 0.0f;
#elif BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(gbufferData.shadingModelID))
    {
        materialData.anisotropy = 0.0f;
    }
    else
    {
        materialData.anisotropy = clamp(gbufferData.anisotropy, -1.0f, 1.0f);
    }
#else
    materialData.anisotropy = clamp(gbufferData.anisotropy, -1.0f, 1.0f);
#endif

    materialData.perceptualRoughness = ClampPerceptualRoughness(PerceptualSmoothnessToPerceptualRoughness(materialData.smoothness));
    materialData.linearRoughness = PerceptualRoughnessToLinearRoughness(materialData.perceptualRoughness);
    materialData.a2 = LinearRoughnessToA2(materialData.linearRoughness);

#if defined(BURT_SUBSURFACE_DEFERRED_POSTPROCESS_INPUT) && BURT_ENABLE_SUBSURFACE_SHADING
    float3 diffuseBaseColor = BurtIsActiveSubsurfaceShadingModel(gbufferData.shadingModelID) ? float3(1.0f, 1.0f, 1.0f) : materialData.baseColor;
#else
    float3 diffuseBaseColor = materialData.baseColor;
#endif
    materialData.diffuseColor = DiffuseColorFromBaseColor(diffuseBaseColor, materialData.metallic);
    materialData.f0 = DielectricReflectanceToF0(materialData.baseColor, materialData.reflectance, materialData.metallic);
    materialData.f90 = ApproximateF90(materialData.f0);

    return materialData;
}

// Prepares PBR geometry data from decoded GBuffer data and reconstructed view direction.
BurtPBRGeometryData BurtPreparePBRGeometryData(BurtGBufferData gbufferData, float3 viewDirectionWS)
{
    return BurtPreparePBRGeometryData(BurtGetDefaultLitNormalWS(gbufferData), gbufferData.tangentWS, viewDirectionWS);
}

#endif // BURT_GBUFFER_INCLUDED // 结束 BurtGBuffer.hlsl �?include guard�?
