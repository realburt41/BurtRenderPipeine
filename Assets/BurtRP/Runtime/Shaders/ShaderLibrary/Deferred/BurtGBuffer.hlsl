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

    float subsurfaceThickness;

    float subsurfacePower;

    float subsurfaceDistortion;

    float subsurfaceAmbient;

    float subsurfaceScatteringMode;

    float subsurface3SCurvature;

    float subsurfaceProfileIndex;

    float hairSecondaryRoughness;

    float hairBackLight;

    float hairShadowFillStrength;

    float3 hairGeometryNormalWS;

    float hairSpecularShift;

    float hairSecondarySpecularShift;

    float3 hairSpecularColor;

    float3 hairSecondarySpecularColor;

    float fabricIsSilk;

    float fabricFuzzWeight;

    float fabricFuzzRoughness;

    float3 fabricFuzzColor;

    float3 foliageTransmissionColor;

    float foliageTransmissionWeight;

    float foliageThickness;

    float foliageBackLight;

    float foliageTransmissionNdotL;

    float foliageSpecularScale;

    float foliageUseSpecularColor;

    float foliageScreenSpaceShadowIntensity;

    float foliageIsGrass;
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

// GBuffer1.b stores shading model + material scalar in one half channel. Keep
// each model bucket away from both edges: Fabric/Silk at metallic=0 otherwise
// lands on the 4/5 boundary, and half/UNorm RT quantization can decode it as
// the previous shading model.
static const float BURT_GBUFFER_SHADING_MODEL_PACK_COUNT = 7.0f;
static const float BURT_GBUFFER_SHADING_MODEL_PACK_BIAS = 0.02f;
static const float BURT_GBUFFER_SHADING_MODEL_PACK_SCALE = 1.0f - 2.0f * BURT_GBUFFER_SHADING_MODEL_PACK_BIAS;

float BurtEncodeMetallicAndShadingModelForGBuffer(float metallicOrScatter, float shadingModelID)
{
    float modelID = clamp(BurtResolveSurfaceShadingModel(shadingModelID), 0.0f, BURT_GBUFFER_SHADING_MODEL_PACK_COUNT - 1.0f);
    float material = BURT_GBUFFER_SHADING_MODEL_PACK_BIAS + saturate(metallicOrScatter) * BURT_GBUFFER_SHADING_MODEL_PACK_SCALE;
    return (modelID + material) / BURT_GBUFFER_SHADING_MODEL_PACK_COUNT;
}

float BurtDecodeMetallicAndShadingModelFromGBuffer(float packedValue, out float shadingModelID)
{
    float scaled = saturate(packedValue) * BURT_GBUFFER_SHADING_MODEL_PACK_COUNT;
    shadingModelID = floor(min(scaled, BURT_GBUFFER_SHADING_MODEL_PACK_COUNT - BURT_EPSILON));
    return saturate((scaled - shadingModelID - BURT_GBUFFER_SHADING_MODEL_PACK_BIAS) / BURT_GBUFFER_SHADING_MODEL_PACK_SCALE);
}

static const float BURT_HAIR_SCATTER_PACK_DIMENSION = 32.0f;
static const float BURT_HAIR_SHIFT_PACK_DIMENSION = 16.0f;
static const float BURT_HAIR_SCATTER_PACK_MAX_BUCKET = BURT_HAIR_SCATTER_PACK_DIMENSION - 1.0f;
static const float BURT_HAIR_SHIFT_PACK_MAX_BUCKET = BURT_HAIR_SHIFT_PACK_DIMENSION - 1.0f;
static const float BURT_HAIR_MATERIAL_PACK_MAX_VALUE = BURT_HAIR_SCATTER_PACK_DIMENSION * BURT_HAIR_SHIFT_PACK_DIMENSION - 1.0f;
static const float BURT_HAIR_CONTROL_PACK_DIMENSION = 64.0f;
static const float BURT_HAIR_CONTROL_PACK_MAX_BUCKET = BURT_HAIR_CONTROL_PACK_DIMENSION - 1.0f;
static const float BURT_HAIR_CONTROL_PACK_MAX_VALUE = BURT_HAIR_CONTROL_PACK_DIMENSION * BURT_HAIR_CONTROL_PACK_DIMENSION - 1.0f;
static const float BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION = 16.0f;
static const float BURT_HAIR_SHIFT_CONTROL_PACK_MAX_BUCKET = BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION - 1.0f;
static const float BURT_HAIR_SHIFT_CONTROL_PACK_MAX_VALUE = BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION * BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION * BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION - 1.0f;
static const float BURT_HAIR_PRIMARY_SPECULAR_SHIFT_MIN = -2.60f;
static const float BURT_HAIR_PRIMARY_SPECULAR_SHIFT_MAX = 5.32f;
static const float BURT_HAIR_SECONDARY_SPECULAR_SHIFT_MIN = -5.10f;
static const float BURT_HAIR_SECONDARY_SPECULAR_SHIFT_MAX = 8.22f;

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

float BurtEncodeHairRoughnessFillForGBuffer(float secondaryRoughness, float shadowFillStrength)
{
    float roughnessBucket = BurtQuantizeHairMaterialValue(secondaryRoughness, BURT_HAIR_CONTROL_PACK_MAX_BUCKET);
    float fillBucket = BurtQuantizeHairMaterialValue(shadowFillStrength, BURT_HAIR_CONTROL_PACK_MAX_BUCKET);
    return (fillBucket * BURT_HAIR_CONTROL_PACK_DIMENSION + roughnessBucket) / BURT_HAIR_CONTROL_PACK_MAX_VALUE;
}

void BurtDecodeHairRoughnessFillFromGBuffer(float packedValue, out float secondaryRoughness, out float shadowFillStrength)
{
    float packedBucket = floor(saturate(packedValue) * BURT_HAIR_CONTROL_PACK_MAX_VALUE + 0.5f);
    float fillBucket = floor(packedBucket / BURT_HAIR_CONTROL_PACK_DIMENSION);
    float roughnessBucket = packedBucket - fillBucket * BURT_HAIR_CONTROL_PACK_DIMENSION;
    secondaryRoughness = saturate(roughnessBucket / BURT_HAIR_CONTROL_PACK_MAX_BUCKET);
    shadowFillStrength = saturate(fillBucket / BURT_HAIR_CONTROL_PACK_MAX_BUCKET);
}

float BurtEncodeHairShiftBackLightForGBuffer(float specularShift, float secondarySpecularShift, float backLight)
{
    float primaryBucket = floor(saturate((specularShift - BURT_HAIR_PRIMARY_SPECULAR_SHIFT_MIN) / max(BURT_HAIR_PRIMARY_SPECULAR_SHIFT_MAX - BURT_HAIR_PRIMARY_SPECULAR_SHIFT_MIN, BURT_EPSILON)) * BURT_HAIR_SHIFT_CONTROL_PACK_MAX_BUCKET + 0.5f);
    float secondaryBucket = floor(saturate((secondarySpecularShift - BURT_HAIR_SECONDARY_SPECULAR_SHIFT_MIN) / max(BURT_HAIR_SECONDARY_SPECULAR_SHIFT_MAX - BURT_HAIR_SECONDARY_SPECULAR_SHIFT_MIN, BURT_EPSILON)) * BURT_HAIR_SHIFT_CONTROL_PACK_MAX_BUCKET + 0.5f);
    float backLightBucket = BurtQuantizeHairMaterialValue(backLight, BURT_HAIR_SHIFT_CONTROL_PACK_MAX_BUCKET);
    return (backLightBucket * BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION * BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION + secondaryBucket * BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION + primaryBucket) / BURT_HAIR_SHIFT_CONTROL_PACK_MAX_VALUE;
}

void BurtDecodeHairShiftBackLightFromGBuffer(float packedValue, out float specularShift, out float secondarySpecularShift, out float backLight)
{
    float packedBucket = floor(saturate(packedValue) * BURT_HAIR_SHIFT_CONTROL_PACK_MAX_VALUE + 0.5f);
    float backLightBucket = floor(packedBucket / (BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION * BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION));
    float remainingBucket = packedBucket - backLightBucket * BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION * BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION;
    float secondaryBucket = floor(remainingBucket / BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION);
    float primaryBucket = remainingBucket - secondaryBucket * BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION;
    specularShift = lerp(BURT_HAIR_PRIMARY_SPECULAR_SHIFT_MIN, BURT_HAIR_PRIMARY_SPECULAR_SHIFT_MAX, primaryBucket / BURT_HAIR_SHIFT_CONTROL_PACK_MAX_BUCKET);
    secondarySpecularShift = lerp(BURT_HAIR_SECONDARY_SPECULAR_SHIFT_MIN, BURT_HAIR_SECONDARY_SPECULAR_SHIFT_MAX, secondaryBucket / BURT_HAIR_SHIFT_CONTROL_PACK_MAX_BUCKET);
    backLight = saturate(backLightBucket / BURT_HAIR_SHIFT_CONTROL_PACK_MAX_BUCKET);
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
static const float BURT_SUBSURFACE_DISTORTION_MODE_PACK_COUNT = 3.0f;
static const float BURT_SUBSURFACE_DISTORTION_MODE_PACK_SCALE = 0.999f;

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

float BurtEncodeSubsurfaceDistortionModeForGBuffer(float distortion, float scatteringMode)
{
    float mode = BurtClampSubsurfaceScatteringMode(scatteringMode);
    return (mode + saturate(distortion) * BURT_SUBSURFACE_DISTORTION_MODE_PACK_SCALE) / BURT_SUBSURFACE_DISTORTION_MODE_PACK_COUNT;
}

void BurtDecodeSubsurfaceDistortionModeFromGBuffer(float packedValue, out float distortion, out float scatteringMode)
{
    float scaled = saturate(packedValue) * BURT_SUBSURFACE_DISTORTION_MODE_PACK_COUNT;
    scatteringMode = floor(min(scaled, BURT_SUBSURFACE_DISTORTION_MODE_PACK_COUNT - BURT_EPSILON));
    distortion = saturate((scaled - scatteringMode) / BURT_SUBSURFACE_DISTORTION_MODE_PACK_SCALE);
    scatteringMode = BurtClampSubsurfaceScatteringMode(scatteringMode);
}

static const float BURT_FABRIC_ROUGHNESS_SILK_PACK_SCALE = 0.499f;

float BurtEncodeFabricRoughnessSilkForGBuffer(float fuzzRoughness, float isSilk)
{
    float packedRoughness = saturate(fuzzRoughness) * BURT_FABRIC_ROUGHNESS_SILK_PACK_SCALE;
    return isSilk > 0.5f ? 0.5f + packedRoughness : packedRoughness;
}

void BurtDecodeFabricRoughnessSilkFromGBuffer(float packedValue, out float fuzzRoughness, out float isSilk)
{
    float packed = saturate(packedValue);
    isSilk = packed >= 0.5f ? 1.0f : 0.0f;
    float localRoughness = isSilk > 0.5f ? packed - 0.5f : packed;
    fuzzRoughness = ClampPerceptualRoughness(localRoughness / BURT_FABRIC_ROUGHNESS_SILK_PACK_SCALE);
}

static const float BURT_FOLIAGE_SPECULAR_PACK_SCALE = 0.499f;

float BurtEncodeFoliageSpecularTypeForGBuffer(float specularScale, float useSpecularColor)
{
    float packedSpecular = saturate(specularScale) * BURT_FOLIAGE_SPECULAR_PACK_SCALE;
    return useSpecularColor > 0.5f ? 0.5f + packedSpecular : packedSpecular;
}

void BurtDecodeFoliageSpecularTypeFromGBuffer(float packedValue, out float specularScale, out float useSpecularColor)
{
    float packed = saturate(packedValue);
    useSpecularColor = packed >= 0.5f ? 1.0f : 0.0f;
    float localSpecular = useSpecularColor > 0.5f ? packed - 0.5f : packed;
    specularScale = saturate(localSpecular / BURT_FOLIAGE_SPECULAR_PACK_SCALE);
}

static const float BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_DIMENSION = 32.0f;
static const float BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_MAX_BUCKET = BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_DIMENSION - 1.0f;
static const float BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_MAX_VALUE =
    BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_DIMENSION * BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_DIMENSION - 1.0f;

float BurtEncodeFoliageBackLightNdotLForGBuffer(float backLight, float transmissionNdotL)
{
    float backLightBucket = floor(saturate(backLight) * BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_MAX_BUCKET + 0.5f);
    float ndotlBucket = floor(saturate(transmissionNdotL) * BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_MAX_BUCKET + 0.5f);
    return (ndotlBucket * BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_DIMENSION + backLightBucket) / BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_MAX_VALUE;
}

void BurtDecodeFoliageBackLightNdotLFromGBuffer(float packedValue, out float backLight, out float transmissionNdotL)
{
    float packedBucket = floor(saturate(packedValue) * BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_MAX_VALUE + 0.5f);
    float ndotlBucket = floor(packedBucket / BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_DIMENSION);
    float backLightBucket = packedBucket - ndotlBucket * BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_DIMENSION;
    backLight = saturate(backLightBucket / BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_MAX_BUCKET);
    transmissionNdotL = saturate(ndotlBucket / BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_MAX_BUCKET);
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
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL || BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    data.anisotropy = 0.0f;
#elif BURT_ENABLE_SUBSURFACE_SHADING || BURT_ENABLE_FOLIAGE_SHADING
    data.anisotropy = (BurtIsActiveSubsurfaceShadingModel(surfaceData.shadingModelID) || BurtIsActiveFoliageShadingModel(surfaceData.shadingModelID)) ? 0.0f : clamp(surfaceData.anisotropy, -1.0f, 1.0f);
#else
    data.anisotropy = clamp(surfaceData.anisotropy, -1.0f, 1.0f);
#endif

#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL || BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    data.metallic = 0.0f;
    data.materialChannel = BURT_ACTIVE_FOLIAGE_SHADING_MODEL
        ? BurtEncodeFoliageSpecularTypeForGBuffer(surfaceData.foliageSpecularScale, surfaceData.foliageUseSpecularColor)
        : 1.0f;
#elif BURT_ENABLE_SUBSURFACE_SHADING || BURT_ENABLE_FOLIAGE_SHADING
    if (BurtIsActiveSubsurfaceShadingModel(surfaceData.shadingModelID) || BurtIsActiveFoliageShadingModel(surfaceData.shadingModelID))
    {
        data.metallic = 0.0f;
        data.materialChannel = BurtIsActiveFoliageShadingModel(surfaceData.shadingModelID)
            ? BurtEncodeFoliageSpecularTypeForGBuffer(surfaceData.foliageSpecularScale, surfaceData.foliageUseSpecularColor)
            : 1.0f;
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
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    data.reflectance = BURT_SUBSURFACE_FIXED_REFLECTANCE;
#elif BURT_ENABLE_SUBSURFACE_SHADING
    data.reflectance = BurtIsActiveSubsurfaceShadingModel(surfaceData.shadingModelID) ? BURT_SUBSURFACE_FIXED_REFLECTANCE : saturate(surfaceData.reflectance);
#else
    data.reflectance = saturate(surfaceData.reflectance);
#endif
    data.occlusion = saturate(surfaceData.occlusion);

    data.perceptualRoughness = ClampPerceptualRoughness(PerceptualSmoothnessToPerceptualRoughness(data.smoothness));

    data.emission = max(emission, float3(0.0f, 0.0f, 0.0f));

    data.shadingModelID = BurtResolveSurfaceShadingModel(surfaceData.shadingModelID);
    data.clearCoatMask = 0.0f;
    data.clearCoatRoughness = 0.2f;
    data.subsurfaceThickness = BURT_SUBSURFACE_DEFAULT_THICKNESS;
    data.subsurfacePower = BURT_SUBSURFACE_DEFAULT_POWER;
    data.subsurfaceDistortion = BURT_SUBSURFACE_DEFAULT_DISTORTION;
    data.subsurfaceAmbient = BURT_SUBSURFACE_DEFAULT_AMBIENT;
    data.subsurfaceScatteringMode = BURT_SUBSURFACE_DEFAULT_SCATTERING_MODE;
    data.subsurface3SCurvature = 1.0f - BURT_SUBSURFACE_DEFAULT_THICKNESS;
    data.subsurfaceProfileIndex = BURT_SUBSURFACE_DEFAULT_PROFILE_INDEX;
    data.hairSecondaryRoughness = 0.5f;
    data.hairBackLight = 0.0f;
    data.hairShadowFillStrength = 0.0f;
    data.hairGeometryNormalWS = data.normalWS;
    data.hairSpecularShift = 0.0f;
    data.hairSecondarySpecularShift = 0.0f;
    data.hairSpecularColor = float3(1.0f, 1.0f, 1.0f);
    data.hairSecondarySpecularColor = float3(1.0f, 1.0f, 1.0f);
    data.fabricIsSilk = 0.0f;
    data.fabricFuzzWeight = 0.0f;
    data.fabricFuzzRoughness = 0.75f;
    data.fabricFuzzColor = float3(1.0f, 1.0f, 1.0f);
    data.foliageTransmissionColor = float3(0.55f, 0.85f, 0.35f);
    data.foliageTransmissionWeight = 0.0f;
    data.foliageThickness = 0.5f;
    data.foliageBackLight = 0.5f;
    data.foliageTransmissionNdotL = 0.5f;
    data.foliageSpecularScale = 1.0f;
    data.foliageUseSpecularColor = 0.0f;
    data.foliageScreenSpaceShadowIntensity = 0.0f;
    data.foliageIsGrass = 0.0f;

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
    data.subsurfaceThickness = saturate(surfaceData.subsurfaceThickness);
    data.subsurfacePower = BurtClampSubsurfacePower(surfaceData.subsurfacePower);
    data.subsurfaceDistortion = saturate(surfaceData.subsurfaceDistortion);
    data.subsurfaceAmbient = saturate(surfaceData.subsurfaceAmbient);
    data.subsurfaceScatteringMode = BurtClampSubsurfaceScatteringMode(surfaceData.subsurfaceScatteringMode);
    data.subsurface3SCurvature = saturate(surfaceData.subsurface3SCurvature);
    data.subsurfaceProfileIndex = BurtClampSubsurfaceProfileIndex(surfaceData.subsurfaceProfileIndex);
#elif BURT_ENABLE_SUBSURFACE_SHADING
    if (BurtIsActiveSubsurfaceShadingModel(data.shadingModelID))
    {
        data.subsurfaceThickness = saturate(surfaceData.subsurfaceThickness);
        data.subsurfacePower = BurtClampSubsurfacePower(surfaceData.subsurfacePower);
        data.subsurfaceDistortion = saturate(surfaceData.subsurfaceDistortion);
        data.subsurfaceAmbient = saturate(surfaceData.subsurfaceAmbient);
        data.subsurfaceScatteringMode = BurtClampSubsurfaceScatteringMode(surfaceData.subsurfaceScatteringMode);
        data.subsurface3SCurvature = saturate(surfaceData.subsurface3SCurvature);
        data.subsurfaceProfileIndex = BurtClampSubsurfaceProfileIndex(surfaceData.subsurfaceProfileIndex);
    }
#endif

#if BURT_ACTIVE_FABRIC_SHADING_MODEL
    data.fabricIsSilk = saturate(surfaceData.fabricIsSilk);
    data.fabricFuzzWeight = saturate(surfaceData.fabricFuzzWeight);
    data.fabricFuzzRoughness = ClampPerceptualRoughness(surfaceData.fabricFuzzRoughness);
    data.fabricFuzzColor = max(surfaceData.fabricFuzzColor, float3(0.0f, 0.0f, 0.0f));
#elif BURT_ENABLE_FABRIC_SHADING
    if (BurtIsActiveFabricShadingModel(data.shadingModelID))
    {
        data.fabricIsSilk = saturate(surfaceData.fabricIsSilk);
        data.fabricFuzzWeight = saturate(surfaceData.fabricFuzzWeight);
        data.fabricFuzzRoughness = ClampPerceptualRoughness(surfaceData.fabricFuzzRoughness);
        data.fabricFuzzColor = max(surfaceData.fabricFuzzColor, float3(0.0f, 0.0f, 0.0f));
    }
#endif

#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    data.foliageTransmissionColor = max(surfaceData.foliageTransmissionColor, float3(0.0f, 0.0f, 0.0f));
    data.foliageTransmissionWeight = surfaceData.foliageIsGrass > 0.5f
        ? max(surfaceData.foliageTransmissionWeight, 0.0f)
        : saturate(surfaceData.foliageTransmissionWeight);
    data.foliageThickness = saturate(surfaceData.foliageThickness);
    data.foliageBackLight = saturate(surfaceData.foliageBackLight);
    data.foliageTransmissionNdotL = saturate(surfaceData.foliageTransmissionNdotL);
    data.foliageSpecularScale = saturate(surfaceData.foliageSpecularScale);
    data.foliageUseSpecularColor = saturate(surfaceData.foliageUseSpecularColor);
    data.foliageScreenSpaceShadowIntensity = max(surfaceData.foliageScreenSpaceShadowIntensity, 0.0f);
    data.foliageIsGrass = saturate(surfaceData.foliageIsGrass);
#elif BURT_ENABLE_FOLIAGE_SHADING
    if (BurtIsActiveFoliageShadingModel(data.shadingModelID))
    {
        data.foliageTransmissionColor = max(surfaceData.foliageTransmissionColor, float3(0.0f, 0.0f, 0.0f));
        data.foliageTransmissionWeight = surfaceData.foliageIsGrass > 0.5f
            ? max(surfaceData.foliageTransmissionWeight, 0.0f)
            : saturate(surfaceData.foliageTransmissionWeight);
        data.foliageThickness = saturate(surfaceData.foliageThickness);
        data.foliageBackLight = saturate(surfaceData.foliageBackLight);
        data.foliageTransmissionNdotL = saturate(surfaceData.foliageTransmissionNdotL);
        data.foliageSpecularScale = saturate(surfaceData.foliageSpecularScale);
        data.foliageUseSpecularColor = saturate(surfaceData.foliageUseSpecularColor);
        data.foliageScreenSpaceShadowIntensity = max(surfaceData.foliageScreenSpaceShadowIntensity, 0.0f);
        data.foliageIsGrass = saturate(surfaceData.foliageIsGrass);
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

BurtSurfaceData BurtApplyFurGBufferSurfaceSemantics(BurtSurfaceData surfaceData)
{
    surfaceData.shadingModelID = BURT_SHADING_MODEL_FUR;
    surfaceData.metallic = 0.0f;
    return surfaceData;
}

BurtGBufferData BurtCreateGBufferData(BurtSurfaceData surfaceData, float3 normalWS, float3 emission)
{
    float3 safeNormalWS = BurtSafeNormalize(normalWS);
    return BurtCreateGBufferData(surfaceData, safeNormalWS, float4(BurtCreateFallbackTangentWS(safeNormalWS), 1.0f), emission);
}

BurtGBufferData BurtCreateHairGBufferData(BurtSurfaceData surfaceData, float3 strandDirectionWS, float3 hairNormalWS, float3 hairGeometryNormalWS, float3 emission)
{
    surfaceData.shadingModelID = BURT_SHADING_MODEL_HAIR;
    BurtGBufferData data = BurtCreateGBufferData(surfaceData, strandDirectionWS, emission);
    data.clearCoatNormalWS = BurtSafeNormalize(hairNormalWS);
    data.hairGeometryNormalWS = BurtSafeNormalize(hairGeometryNormalWS);
    data.hairSecondaryRoughness = ClampPerceptualRoughness(surfaceData.hairSecondaryRoughness);
    data.hairBackLight = saturate(surfaceData.hairBackLight);
    data.hairShadowFillStrength = saturate(surfaceData.hairShadowFillStrength);
    data.hairSpecularShift = clamp(surfaceData.hairSpecularShift, BURT_HAIR_PRIMARY_SPECULAR_SHIFT_MIN, BURT_HAIR_PRIMARY_SPECULAR_SHIFT_MAX);
    data.hairSecondarySpecularShift = clamp(surfaceData.hairSecondarySpecularShift, BURT_HAIR_SECONDARY_SPECULAR_SHIFT_MIN, BURT_HAIR_SECONDARY_SPECULAR_SHIFT_MAX);
    data.hairSpecularColor = max(surfaceData.hairSpecularColor, float3(0.0f, 0.0f, 0.0f));
    data.hairSecondarySpecularColor = max(surfaceData.hairSecondarySpecularColor, float3(0.0f, 0.0f, 0.0f));
    return data;
}

BurtGBufferData BurtCreateHairGBufferData(BurtSurfaceData surfaceData, float3 strandDirectionWS, float3 emission)
{
    return BurtCreateHairGBufferData(surfaceData, strandDirectionWS, strandDirectionWS, strandDirectionWS, emission);
}

BurtGBufferData BurtCreateFurGBufferData(BurtSurfaceData surfaceData, float3 normalWS, float4 tangentWS, float3 emission)
{
    surfaceData = BurtApplyFurGBufferSurfaceSemantics(surfaceData);
    return BurtCreateGBufferData(surfaceData, normalWS, tangentWS, emission);
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

BurtGBufferData BurtCreateFabricGBufferData(BurtSurfaceData surfaceData, float3 normalWS, float4 tangentWS, float3 emission)
{
    surfaceData.shadingModelID = BURT_SHADING_MODEL_FABRIC;
    return BurtCreateGBufferData(surfaceData, normalWS, tangentWS, emission);
}

BurtGBufferData BurtCreateFoliageGBufferData(BurtSurfaceData surfaceData, float3 normalWS, float4 tangentWS, float3 emission)
{
    surfaceData.shadingModelID = BURT_SHADING_MODEL_FOLIAGE;
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

float3 BurtGetHairShadingNormalWS(BurtGBufferData gbufferData)
{
    return BurtSafeNormalize(gbufferData.clearCoatNormalWS);
}

float3 BurtGetHairGeometryNormalWS(BurtGBufferData gbufferData)
{
    return BurtSafeNormalize(gbufferData.hairGeometryNormalWS);
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
    return 1.0f;
#elif BURT_ENABLE_SUBSURFACE_SHADING
    return BurtIsActiveSubsurfaceShadingModel(gbufferData.shadingModelID) ? 1.0f : 0.0f;
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

float BurtGetSubsurfaceScatteringMode(BurtGBufferData gbufferData)
{
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    return BurtClampSubsurfaceScatteringMode(gbufferData.subsurfaceScatteringMode);
#elif BURT_ENABLE_SUBSURFACE_SHADING
    return BurtIsActiveSubsurfaceShadingModel(gbufferData.shadingModelID) ? BurtClampSubsurfaceScatteringMode(gbufferData.subsurfaceScatteringMode) : BURT_SUBSURFACE_DEFAULT_SCATTERING_MODE;
#else
    return BURT_SUBSURFACE_DEFAULT_SCATTERING_MODE;
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

float BurtGetHairSecondaryRoughness(BurtGBufferData gbufferData)
{
    return ClampPerceptualRoughness(gbufferData.hairSecondaryRoughness);
}

float BurtGetHairBackLight(BurtGBufferData gbufferData)
{
    return saturate(gbufferData.hairBackLight);
}

float BurtGetHairShadowFillStrength(BurtGBufferData gbufferData)
{
    return saturate(gbufferData.hairShadowFillStrength);
}

float BurtGetHairSpecularShift(BurtGBufferData gbufferData)
{
    return clamp(gbufferData.hairSpecularShift, BURT_HAIR_PRIMARY_SPECULAR_SHIFT_MIN, BURT_HAIR_PRIMARY_SPECULAR_SHIFT_MAX);
}

float BurtGetHairSecondarySpecularShift(BurtGBufferData gbufferData)
{
    return clamp(gbufferData.hairSecondarySpecularShift, BURT_HAIR_SECONDARY_SPECULAR_SHIFT_MIN, BURT_HAIR_SECONDARY_SPECULAR_SHIFT_MAX);
}

float3 BurtGetHairSpecularColor(BurtGBufferData gbufferData)
{
    return max(gbufferData.hairSpecularColor, float3(0.0f, 0.0f, 0.0f));
}

float3 BurtGetHairSecondarySpecularColor(BurtGBufferData gbufferData)
{
    return max(gbufferData.hairSecondarySpecularColor, float3(0.0f, 0.0f, 0.0f));
}

float BurtGetFabricFuzzWeight(BurtGBufferData gbufferData)
{
#if BURT_ACTIVE_FABRIC_SHADING_MODEL
    return saturate(gbufferData.fabricFuzzWeight);
#elif BURT_ENABLE_FABRIC_SHADING
    return BurtIsActiveFabricShadingModel(gbufferData.shadingModelID) ? saturate(gbufferData.fabricFuzzWeight) : 0.0f;
#else
    return 0.0f;
#endif
}

float BurtGetFabricFuzzRoughness(BurtGBufferData gbufferData)
{
#if BURT_ACTIVE_FABRIC_SHADING_MODEL
    return ClampPerceptualRoughness(gbufferData.fabricFuzzRoughness);
#elif BURT_ENABLE_FABRIC_SHADING
    return BurtIsActiveFabricShadingModel(gbufferData.shadingModelID) ? ClampPerceptualRoughness(gbufferData.fabricFuzzRoughness) : 0.75f;
#else
    return 0.75f;
#endif
}

float3 BurtGetFabricFuzzColor(BurtGBufferData gbufferData)
{
#if BURT_ACTIVE_FABRIC_SHADING_MODEL
    return max(gbufferData.fabricFuzzColor, float3(0.0f, 0.0f, 0.0f));
#elif BURT_ENABLE_FABRIC_SHADING
    return BurtIsActiveFabricShadingModel(gbufferData.shadingModelID) ? max(gbufferData.fabricFuzzColor, float3(0.0f, 0.0f, 0.0f)) : float3(1.0f, 1.0f, 1.0f);
#else
    return float3(1.0f, 1.0f, 1.0f);
#endif
}

float BurtGetFabricIsSilk(BurtGBufferData gbufferData)
{
#if BURT_ACTIVE_FABRIC_SHADING_MODEL
    return saturate(gbufferData.fabricIsSilk);
#elif BURT_ENABLE_FABRIC_SHADING
    return BurtIsActiveFabricShadingModel(gbufferData.shadingModelID) ? saturate(gbufferData.fabricIsSilk) : 0.0f;
#else
    return 0.0f;
#endif
}

float3 BurtGetFoliageTransmissionColor(BurtGBufferData gbufferData)
{
#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    return max(gbufferData.foliageTransmissionColor, float3(0.0f, 0.0f, 0.0f));
#elif BURT_ENABLE_FOLIAGE_SHADING
    return BurtIsActiveFoliageShadingModel(gbufferData.shadingModelID) ? max(gbufferData.foliageTransmissionColor, float3(0.0f, 0.0f, 0.0f)) : float3(0.0f, 0.0f, 0.0f);
#else
    return float3(0.0f, 0.0f, 0.0f);
#endif
}

float BurtGetFoliageTransmissionWeight(BurtGBufferData gbufferData)
{
#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    return gbufferData.foliageIsGrass > 0.5f ? max(gbufferData.foliageTransmissionWeight, 0.0f) : saturate(gbufferData.foliageTransmissionWeight);
#elif BURT_ENABLE_FOLIAGE_SHADING
    return BurtIsActiveFoliageShadingModel(gbufferData.shadingModelID)
        ? (gbufferData.foliageIsGrass > 0.5f ? max(gbufferData.foliageTransmissionWeight, 0.0f) : saturate(gbufferData.foliageTransmissionWeight))
        : 0.0f;
#else
    return 0.0f;
#endif
}

float BurtGetFoliageThickness(BurtGBufferData gbufferData)
{
#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    return saturate(gbufferData.foliageThickness);
#elif BURT_ENABLE_FOLIAGE_SHADING
    return BurtIsActiveFoliageShadingModel(gbufferData.shadingModelID) ? saturate(gbufferData.foliageThickness) : 0.5f;
#else
    return 0.5f;
#endif
}

float BurtGetFoliageBackLight(BurtGBufferData gbufferData)
{
#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    return saturate(gbufferData.foliageBackLight);
#elif BURT_ENABLE_FOLIAGE_SHADING
    return BurtIsActiveFoliageShadingModel(gbufferData.shadingModelID) ? saturate(gbufferData.foliageBackLight) : 0.0f;
#else
    return 0.0f;
#endif
}

float BurtGetFoliageTransmissionNdotL(BurtGBufferData gbufferData)
{
#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    return saturate(gbufferData.foliageTransmissionNdotL);
#elif BURT_ENABLE_FOLIAGE_SHADING
    return BurtIsActiveFoliageShadingModel(gbufferData.shadingModelID) ? saturate(gbufferData.foliageTransmissionNdotL) : 0.5f;
#else
    return 0.5f;
#endif
}

float BurtGetFoliageSpecularScale(BurtGBufferData gbufferData)
{
#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    return saturate(gbufferData.foliageSpecularScale);
#elif BURT_ENABLE_FOLIAGE_SHADING
    return BurtIsActiveFoliageShadingModel(gbufferData.shadingModelID) ? saturate(gbufferData.foliageSpecularScale) : 0.0f;
#else
    return 0.0f;
#endif
}

float BurtGetFoliageUseSpecularColor(BurtGBufferData gbufferData)
{
#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    return saturate(gbufferData.foliageUseSpecularColor);
#elif BURT_ENABLE_FOLIAGE_SHADING
    return BurtIsActiveFoliageShadingModel(gbufferData.shadingModelID) ? saturate(gbufferData.foliageUseSpecularColor) : 0.0f;
#else
    return 0.0f;
#endif
}

float BurtGetFoliageIsGrass(BurtGBufferData gbufferData)
{
#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    return saturate(gbufferData.foliageIsGrass);
#elif BURT_ENABLE_FOLIAGE_SHADING
    return BurtIsActiveFoliageShadingModel(gbufferData.shadingModelID) ? saturate(gbufferData.foliageIsGrass) : 0.0f;
#else
    return 0.0f;
#endif
}

float BurtGetFoliageScreenSpaceShadowIntensity(BurtGBufferData gbufferData)
{
#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    return max(gbufferData.foliageScreenSpaceShadowIntensity, 0.0f);
#elif BURT_ENABLE_FOLIAGE_SHADING
    return BurtIsActiveFoliageShadingModel(gbufferData.shadingModelID) ? max(gbufferData.foliageScreenSpaceShadowIntensity, 0.0f) : 0.0f;
#else
    return 0.0f;
#endif
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

#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    return BurtGetFoliageSpecularScale(gbufferData);
#elif BURT_ENABLE_FOLIAGE_SHADING
    if (BurtIsActiveFoliageShadingModel(gbufferData.shadingModelID))
    {
        return BurtGetFoliageSpecularScale(gbufferData);
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
    float subsurfaceControl = BurtIsSubsurface3SPreIntegratedMode(data.subsurfaceScatteringMode)
        ? saturate(data.subsurface3SCurvature)
        : BurtEncodeSubsurfacePowerAmbientForGBuffer(data.subsurfacePower, data.subsurfaceAmbient);
    return float4(1.0f, 1.0f, 1.0f, subsurfaceControl);
}

float4 BurtEncodeGBuffer3(BurtGBufferData data)
{
#if BURT_ACTIVE_HAIR_SHADING_MODEL
    return float4(
        max(data.hairSpecularColor, float3(0.0f, 0.0f, 0.0f)),
        BurtEncodeHairRoughnessFillForGBuffer(data.hairSecondaryRoughness, data.hairShadowFillStrength));
#elif BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(data.shadingModelID))
    {
        return float4(
            max(data.hairSpecularColor, float3(0.0f, 0.0f, 0.0f)),
            BurtEncodeHairRoughnessFillForGBuffer(data.hairSecondaryRoughness, data.hairShadowFillStrength));
    }
#endif

#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    return BurtEncodeSubsurfaceGBuffer3(data);
#elif BURT_ENABLE_SUBSURFACE_SHADING
    if (BurtIsActiveSubsurfaceShadingModel(data.shadingModelID))
    {
        return BurtEncodeSubsurfaceGBuffer3(data);
    }
#endif

#if BURT_ACTIVE_FABRIC_SHADING_MODEL
    return float4(max(data.fabricFuzzColor, float3(0.0f, 0.0f, 0.0f)), saturate(data.fabricFuzzWeight));
#elif BURT_ENABLE_FABRIC_SHADING
    if (BurtIsActiveFabricShadingModel(data.shadingModelID))
    {
        return float4(max(data.fabricFuzzColor, float3(0.0f, 0.0f, 0.0f)), saturate(data.fabricFuzzWeight));
    }
#endif

#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    float encodedFoliageWeight = data.foliageIsGrass > 0.5f
        ? saturate(data.foliageTransmissionWeight * 0.1f)
        : saturate(data.foliageTransmissionWeight);
    return float4(max(data.foliageTransmissionColor, float3(0.0f, 0.0f, 0.0f)), encodedFoliageWeight);
#elif BURT_ENABLE_FOLIAGE_SHADING
    if (BurtIsActiveFoliageShadingModel(data.shadingModelID))
    {
        float encodedFoliageWeight = data.foliageIsGrass > 0.5f
            ? saturate(data.foliageTransmissionWeight * 0.1f)
            : saturate(data.foliageTransmissionWeight);
        return float4(max(data.foliageTransmissionColor, float3(0.0f, 0.0f, 0.0f)), encodedFoliageWeight);
    }
#endif

    return BurtEncodeClearCoatOrDefaultGBuffer3(data);
}

float4 BurtEncodeGBuffer4(BurtGBufferData data)
{
    float2 encodedTangentWS = BurtEncodeNormalWSForGBuffer(data.tangentWS);

#if BURT_ACTIVE_HAIR_SHADING_MODEL
    return float4(
        max(data.hairSecondarySpecularColor, float3(0.0f, 0.0f, 0.0f)),
        BurtEncodeHairShiftBackLightForGBuffer(data.hairSpecularShift, data.hairSecondarySpecularShift, data.hairBackLight));
#elif BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(data.shadingModelID))
    {
        return float4(
            max(data.hairSecondarySpecularColor, float3(0.0f, 0.0f, 0.0f)),
            BurtEncodeHairShiftBackLightForGBuffer(data.hairSpecularShift, data.hairSecondarySpecularShift, data.hairBackLight));
    }
#endif

#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    return float4(
        encodedTangentWS,
        BurtEncodeSubsurfaceDistortionModeForGBuffer(data.subsurfaceDistortion, data.subsurfaceScatteringMode),
        BurtEncodeSubsurfaceThicknessProfileForGBuffer(data.subsurfaceThickness, data.subsurfaceProfileIndex));
#elif BURT_ENABLE_SUBSURFACE_SHADING
    if (BurtIsActiveSubsurfaceShadingModel(data.shadingModelID))
    {
        return float4(
            encodedTangentWS,
            BurtEncodeSubsurfaceDistortionModeForGBuffer(data.subsurfaceDistortion, data.subsurfaceScatteringMode),
            BurtEncodeSubsurfaceThicknessProfileForGBuffer(data.subsurfaceThickness, data.subsurfaceProfileIndex));
    }
#endif

#if BURT_ACTIVE_FABRIC_SHADING_MODEL
    return float4(
        encodedTangentWS,
        clamp(data.anisotropy, -1.0f, 1.0f) * 0.5f + 0.5f,
        BurtEncodeFabricRoughnessSilkForGBuffer(data.fabricFuzzRoughness, data.fabricIsSilk));
#elif BURT_ENABLE_FABRIC_SHADING
    if (BurtIsActiveFabricShadingModel(data.shadingModelID))
    {
        return float4(
            encodedTangentWS,
            clamp(data.anisotropy, -1.0f, 1.0f) * 0.5f + 0.5f,
            BurtEncodeFabricRoughnessSilkForGBuffer(data.fabricFuzzRoughness, data.fabricIsSilk));
    }
#endif

#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    return float4(
        max(data.foliageScreenSpaceShadowIntensity, 0.0f),
        0.0f,
        BurtEncodeFoliageBackLightNdotLForGBuffer(data.foliageBackLight, data.foliageTransmissionNdotL),
        saturate(data.foliageThickness));
#elif BURT_ENABLE_FOLIAGE_SHADING
    if (BurtIsActiveFoliageShadingModel(data.shadingModelID))
    {
        return float4(
            max(data.foliageScreenSpaceShadowIntensity, 0.0f),
            0.0f,
            BurtEncodeFoliageBackLightNdotLForGBuffer(data.foliageBackLight, data.foliageTransmissionNdotL),
            saturate(data.foliageThickness));
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

    encoded.gbuffer0 = float4(max(data.baseColor, float3(0.0f, 0.0f, 0.0f)), saturate(data.occlusion));

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

    data.baseColor = max(encoded.gbuffer0.rgb, float3(0.0f, 0.0f, 0.0f));
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
#if BURT_ACTIVE_HAIR_SHADING_MODEL || BURT_ACTIVE_SUBSURFACE_SHADING_MODEL || BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    data.anisotropy = 0.0f;
#elif BURT_ENABLE_HAIR_SHADING || BURT_ENABLE_SUBSURFACE_SHADING || BURT_ENABLE_FOLIAGE_SHADING
    if (BurtIsActiveHairShadingModel(data.shadingModelID) || BurtIsActiveSubsurfaceShadingModel(data.shadingModelID) || BurtIsActiveFoliageShadingModel(data.shadingModelID))
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
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL || BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    data.metallic = 0.0f;
#elif BURT_ENABLE_SUBSURFACE_SHADING || BURT_ENABLE_FOLIAGE_SHADING
    if (BurtIsActiveSubsurfaceShadingModel(data.shadingModelID) || BurtIsActiveFoliageShadingModel(data.shadingModelID))
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
    data.subsurfaceThickness = BURT_SUBSURFACE_DEFAULT_THICKNESS;
    data.subsurfacePower = BURT_SUBSURFACE_DEFAULT_POWER;
    data.subsurfaceDistortion = BURT_SUBSURFACE_DEFAULT_DISTORTION;
    data.subsurfaceAmbient = BURT_SUBSURFACE_DEFAULT_AMBIENT;
    data.subsurfaceScatteringMode = BURT_SUBSURFACE_DEFAULT_SCATTERING_MODE;
    data.subsurface3SCurvature = 1.0f - BURT_SUBSURFACE_DEFAULT_THICKNESS;
    data.subsurfaceProfileIndex = BURT_SUBSURFACE_DEFAULT_PROFILE_INDEX;
    data.hairSecondaryRoughness = 0.5f;
    data.hairBackLight = 0.0f;
    data.hairShadowFillStrength = 0.0f;
    data.hairGeometryNormalWS = data.normalWS;
    data.hairSpecularShift = 0.0f;
    data.hairSecondarySpecularShift = 0.0f;
    data.hairSpecularColor = float3(1.0f, 1.0f, 1.0f);
    data.hairSecondarySpecularColor = float3(1.0f, 1.0f, 1.0f);
    data.fabricIsSilk = 0.0f;
    data.fabricFuzzWeight = 0.0f;
    data.fabricFuzzRoughness = 0.75f;
    data.fabricFuzzColor = float3(1.0f, 1.0f, 1.0f);
    data.foliageTransmissionColor = float3(0.0f, 0.0f, 0.0f);
    data.foliageTransmissionWeight = 0.0f;
    data.foliageThickness = 0.5f;
    data.foliageBackLight = 0.0f;
    data.foliageTransmissionNdotL = 0.5f;
    data.foliageSpecularScale = 0.0f;
    data.foliageUseSpecularColor = 0.0f;
    data.foliageScreenSpaceShadowIntensity = 0.0f;
    data.foliageIsGrass = 0.0f;

#if BURT_ACTIVE_HAIR_SHADING_MODEL
    data.hairSpecularColor = max(encoded.gbuffer3.rgb, float3(0.0f, 0.0f, 0.0f));
    BurtDecodeHairRoughnessFillFromGBuffer(encoded.gbuffer3.a, data.hairSecondaryRoughness, data.hairShadowFillStrength);
    data.hairSecondarySpecularColor = max(encoded.gbuffer4.rgb, float3(0.0f, 0.0f, 0.0f));
    BurtDecodeHairShiftBackLightFromGBuffer(encoded.gbuffer4.a, data.hairSpecularShift, data.hairSecondarySpecularShift, data.hairBackLight);
#elif BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(data.shadingModelID))
    {
        data.hairSpecularColor = max(encoded.gbuffer3.rgb, float3(0.0f, 0.0f, 0.0f));
        BurtDecodeHairRoughnessFillFromGBuffer(encoded.gbuffer3.a, data.hairSecondaryRoughness, data.hairShadowFillStrength);
        data.hairSecondarySpecularColor = max(encoded.gbuffer4.rgb, float3(0.0f, 0.0f, 0.0f));
        BurtDecodeHairShiftBackLightFromGBuffer(encoded.gbuffer4.a, data.hairSpecularShift, data.hairSecondarySpecularShift, data.hairBackLight);
    }
#endif

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
    BurtDecodeSubsurfacePowerAmbientFromGBuffer(encoded.gbuffer3.a, data.subsurfacePower, data.subsurfaceAmbient);
    BurtDecodeSubsurfaceDistortionModeFromGBuffer(encoded.gbuffer4.b, data.subsurfaceDistortion, data.subsurfaceScatteringMode);
    BurtDecodeSubsurfaceThicknessProfileFromGBuffer(encoded.gbuffer4.a, data.subsurfaceThickness, data.subsurfaceProfileIndex);
    data.subsurface3SCurvature = BurtIsSubsurface3SPreIntegratedMode(data.subsurfaceScatteringMode)
        ? saturate(encoded.gbuffer3.a)
        : saturate(1.0f - data.subsurfaceThickness);
#elif BURT_ENABLE_SUBSURFACE_SHADING
    if (BurtIsActiveSubsurfaceShadingModel(data.shadingModelID))
    {
        BurtDecodeSubsurfacePowerAmbientFromGBuffer(encoded.gbuffer3.a, data.subsurfacePower, data.subsurfaceAmbient);
        BurtDecodeSubsurfaceDistortionModeFromGBuffer(encoded.gbuffer4.b, data.subsurfaceDistortion, data.subsurfaceScatteringMode);
        BurtDecodeSubsurfaceThicknessProfileFromGBuffer(encoded.gbuffer4.a, data.subsurfaceThickness, data.subsurfaceProfileIndex);
        data.subsurface3SCurvature = BurtIsSubsurface3SPreIntegratedMode(data.subsurfaceScatteringMode)
            ? saturate(encoded.gbuffer3.a)
            : saturate(1.0f - data.subsurfaceThickness);
    }
#endif

#if BURT_ACTIVE_FABRIC_SHADING_MODEL
    data.fabricFuzzColor = max(encoded.gbuffer3.rgb, float3(0.0f, 0.0f, 0.0f));
    data.fabricFuzzWeight = saturate(encoded.gbuffer3.a);
    BurtDecodeFabricRoughnessSilkFromGBuffer(encoded.gbuffer4.a, data.fabricFuzzRoughness, data.fabricIsSilk);
#elif BURT_ENABLE_FABRIC_SHADING
    if (BurtIsActiveFabricShadingModel(data.shadingModelID))
    {
        data.fabricFuzzColor = max(encoded.gbuffer3.rgb, float3(0.0f, 0.0f, 0.0f));
        data.fabricFuzzWeight = saturate(encoded.gbuffer3.a);
        BurtDecodeFabricRoughnessSilkFromGBuffer(encoded.gbuffer4.a, data.fabricFuzzRoughness, data.fabricIsSilk);
    }
#endif

#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    data.foliageTransmissionColor = max(encoded.gbuffer3.rgb, float3(0.0f, 0.0f, 0.0f));
    BurtDecodeFoliageSpecularTypeFromGBuffer(data.materialChannel, data.foliageSpecularScale, data.foliageUseSpecularColor);
    data.foliageIsGrass = 1.0f - saturate(data.foliageUseSpecularColor);
    data.foliageTransmissionWeight = data.foliageIsGrass > 0.5f
        ? max(encoded.gbuffer3.a * 10.0f, 0.0f)
        : saturate(encoded.gbuffer3.a);
    data.foliageScreenSpaceShadowIntensity = max(encoded.gbuffer4.r, 0.0f);
    data.tangentWS = BurtCreateFallbackTangentWS(data.normalWS);
    BurtDecodeFoliageBackLightNdotLFromGBuffer(encoded.gbuffer4.b, data.foliageBackLight, data.foliageTransmissionNdotL);
    data.foliageThickness = saturate(encoded.gbuffer4.a);
#elif BURT_ENABLE_FOLIAGE_SHADING
    if (BurtIsActiveFoliageShadingModel(data.shadingModelID))
    {
        data.foliageTransmissionColor = max(encoded.gbuffer3.rgb, float3(0.0f, 0.0f, 0.0f));
        BurtDecodeFoliageSpecularTypeFromGBuffer(data.materialChannel, data.foliageSpecularScale, data.foliageUseSpecularColor);
        data.foliageIsGrass = 1.0f - saturate(data.foliageUseSpecularColor);
        data.foliageTransmissionWeight = data.foliageIsGrass > 0.5f
            ? max(encoded.gbuffer3.a * 10.0f, 0.0f)
            : saturate(encoded.gbuffer3.a);
        data.foliageScreenSpaceShadowIntensity = max(encoded.gbuffer4.r, 0.0f);
        data.tangentWS = BurtCreateFallbackTangentWS(data.normalWS);
        BurtDecodeFoliageBackLightNdotLFromGBuffer(encoded.gbuffer4.b, data.foliageBackLight, data.foliageTransmissionNdotL);
        data.foliageThickness = saturate(encoded.gbuffer4.a);
    }
#endif
    data.smoothness = saturate(encoded.gbuffer1.a);

    data.perceptualRoughness = ClampPerceptualRoughness(PerceptualSmoothnessToPerceptualRoughness(data.smoothness));

    data.emission = max(encoded.gbuffer2.rgb, float3(0.0f, 0.0f, 0.0f));
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    data.reflectance = BURT_SUBSURFACE_FIXED_REFLECTANCE;
#elif BURT_ENABLE_SUBSURFACE_SHADING
    data.reflectance = BurtIsActiveSubsurfaceShadingModel(data.shadingModelID) ? BURT_SUBSURFACE_FIXED_REFLECTANCE : saturate(encoded.gbuffer2.a);
#else
    data.reflectance = saturate(encoded.gbuffer2.a);
#endif

    return data;
}

// Prepares PBR material data from decoded GBuffer data.
BurtPBRMaterialData BurtPreparePBRMaterialData(BurtGBufferData gbufferData)
{
    BurtPBRMaterialData materialData;

    materialData.baseColor = gbufferData.baseColor;
#if BURT_ACTIVE_HAIR_SHADING_MODEL || BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    materialData.metallic = 0.0f;
#elif BURT_ENABLE_HAIR_SHADING || BURT_ENABLE_FOLIAGE_SHADING
    if (BurtIsActiveHairShadingModel(gbufferData.shadingModelID) || BurtIsActiveFoliageShadingModel(gbufferData.shadingModelID))
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
    materialData.subsurfaceActive = BurtIsSubsurfaceShadingModel(gbufferData.shadingModelID) ? 1.0f : 0.0f;
    materialData.subsurfaceThickness = BurtGetSubsurfaceThickness(gbufferData);
    materialData.subsurfacePower = BurtGetSubsurfacePower(gbufferData);
    materialData.subsurfaceDistortion = BurtGetSubsurfaceDistortion(gbufferData);
    materialData.subsurfaceAmbient = BurtGetSubsurfaceAmbient(gbufferData);
    materialData.subsurfaceScatteringMode = BurtGetSubsurfaceScatteringMode(gbufferData);
    materialData.subsurface3SCurvature = saturate(gbufferData.subsurface3SCurvature);
    materialData.subsurfaceProfileIndex = BurtGetSubsurfaceProfileIndex(gbufferData);
    materialData.fabricActive = BurtIsFabricShadingModel(gbufferData.shadingModelID) ? 1.0f : 0.0f;
    materialData.fabricIsSilk = BurtGetFabricIsSilk(gbufferData);
    materialData.fabricFuzzWeight = BurtGetFabricFuzzWeight(gbufferData);
    materialData.fabricFuzzRoughness = BurtGetFabricFuzzRoughness(gbufferData);
    materialData.fabricFuzzColor = BurtGetFabricFuzzColor(gbufferData);
    materialData.foliageActive = BurtIsFoliageShadingModel(gbufferData.shadingModelID) ? 1.0f : 0.0f;
    materialData.foliageTransmissionColor = BurtGetFoliageTransmissionColor(gbufferData);
    materialData.foliageTransmissionWeight = BurtGetFoliageTransmissionWeight(gbufferData);
    materialData.foliageThickness = BurtGetFoliageThickness(gbufferData);
    materialData.foliageBackLight = BurtGetFoliageBackLight(gbufferData);
    materialData.foliageTransmissionNdotL = BurtGetFoliageTransmissionNdotL(gbufferData);
    materialData.foliageSpecularScale = BurtGetFoliageSpecularScale(gbufferData);
    materialData.foliageUseSpecularColor = BurtGetFoliageUseSpecularColor(gbufferData);
    materialData.foliageScreenSpaceShadowIntensity = BurtGetFoliageScreenSpaceShadowIntensity(gbufferData);
    materialData.foliageIsGrass = BurtGetFoliageIsGrass(gbufferData);
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    materialData.reflectance = BURT_SUBSURFACE_FIXED_REFLECTANCE;
#elif BURT_ENABLE_SUBSURFACE_SHADING
    materialData.reflectance = BurtIsActiveSubsurfaceShadingModel(gbufferData.shadingModelID) ? BURT_SUBSURFACE_FIXED_REFLECTANCE : gbufferData.reflectance;
#else
    materialData.reflectance = gbufferData.reflectance;
#endif
    materialData.occlusion = gbufferData.occlusion;
    materialData.smoothness = gbufferData.smoothness;
#if BURT_ACTIVE_HAIR_SHADING_MODEL || BURT_ACTIVE_SUBSURFACE_SHADING_MODEL || BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    materialData.anisotropy = 0.0f;
#elif BURT_ENABLE_HAIR_SHADING || BURT_ENABLE_SUBSURFACE_SHADING || BURT_ENABLE_FOLIAGE_SHADING
    if (BurtIsActiveHairShadingModel(gbufferData.shadingModelID) || BurtIsActiveSubsurfaceShadingModel(gbufferData.shadingModelID) || BurtIsActiveFoliageShadingModel(gbufferData.shadingModelID))
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
    float3 diffuseBaseColor = BurtIsActiveSubsurfaceShadingModel(gbufferData.shadingModelID) && !BurtIsSubsurface3SPreIntegratedMode(materialData.subsurfaceScatteringMode) ? float3(1.0f, 1.0f, 1.0f) : materialData.baseColor;
#else
    float3 diffuseBaseColor = materialData.baseColor;
#endif

    materialData.diffuseColor = DiffuseColorFromBaseColor(diffuseBaseColor, materialData.metallic);
    materialData.f0 = DielectricReflectanceToF0(materialData.baseColor, materialData.reflectance, materialData.metallic);
    materialData.f90 = ApproximateF90(materialData.f0);
    if (materialData.foliageActive > 0.5f)
    {
        materialData.f90 = materialData.foliageUseSpecularColor > 0.5f
            ? saturate(materialData.baseColor * materialData.foliageSpecularScale)
            : saturate((materialData.baseColor * 0.9f + 0.1f) * materialData.foliageSpecularScale * 3.0f);
    }

    return materialData;
}

// Prepares PBR geometry data from decoded GBuffer data and reconstructed view direction.
BurtPBRGeometryData BurtPreparePBRGeometryData(BurtGBufferData gbufferData, float3 viewDirectionWS)
{
    return BurtPreparePBRGeometryData(BurtGetDefaultLitNormalWS(gbufferData), gbufferData.tangentWS, viewDirectionWS);
}

#endif // BURT_GBUFFER_INCLUDED // 结束 BurtGBuffer.hlsl �?include guard�?
