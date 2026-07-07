// BurtRP �?PBR BRDF 工具库，当前实现单主光也能复用，后续多光源会继续调用这里的函数
#ifndef BURT_BRDF_INCLUDED // 开�?include guard，防止同一�?shader 编译单元里重复定�?BRDF 函数
#define BURT_BRDF_INCLUDED // 标记 BurtBRDF.hlsl 已经被包含过，后续重�?include 会被跳过�?
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl" // 引入安全归一化和基础数学保护
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInput.hlsl" // 引入 BurtSurfaceData，用来读�?baseColor、metallic、smoothness 等材质参数
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtShadingModelMacros.hlsl"

// 定义圆周率，PBR 漫反射和 GGX 分布项都会使用它
#define BURT_PI (3.14159265359f)
#define BURT_INV_PI (0.31830988618f)
#define BURT_MIN_PERCEPTUAL_ROUGHNESS (0.045f)
#define BURT_SPECULAR_AA_SCREEN_SPACE_VARIANCE (0.1f)
#define BURT_SPECULAR_AA_THRESHOLD (0.2f)
#define BURT_MATERIAL_MAX_DIELECTRIC_F0 (0.16f)
#define BURT_CLEAR_COAT_F0 (0.04f)
#define BURT_CLEAR_COAT_IOR (1.5f)
#define BURT_CLEAR_COAT_ETA (1.0f / BURT_CLEAR_COAT_IOR)
#define BURT_PARTICIPATING_MEDIA_MIN_MFP_METER (0.000000000001f)
#define BURT_PARTICIPATING_MEDIA_MIN_EXTINCTION (0.000000000001f)
#define BURT_PARTICIPATING_MEDIA_MIN_TRANSMITTANCE (0.000000000001f)
#define BURT_VOLUME_DEFAULT_THICKNESS_M (1.0f)
#define BURT_GGX_DISTRIBUTION_DENOMINATOR_EPSILON (0.000000000001f)

// BurtRP binds the pre-integrated FG LUT here, or falls back to the analytic approximation when disabled.
Texture2D _BurtPreIntegratedFG;
float _BurtPreIntegratedFGEnabled;
#if BURT_ENABLE_SUBSURFACE_SHADING
Texture2DArray _BurtSubsurfacePreIntegratedLut;
float _BurtSubsurfacePreIntegratedLutEnabled;
Texture2DArray _BurtSubsurfaceSHLut;
float _BurtSubsurfaceSHLutEnabled;
float _BurtSubsurfaceProfileCount;
float4 _BurtSubsurfaceProfileDualSpeculars[8];
float4 _BurtSubsurfaceProfileTransmissions[8];
float4 _BurtSubsurfaceProfileTransmissionTints[8];
#if defined(SHADER_TARGET) && SHADER_TARGET < 35
    Texture2D _BurtSubsurfaceProfileParamLut;
    #define BURT_SUBSURFACE_PROFILE_PARAM_LUT_USE_LOAD 0
#else
    Texture2D<float4> _BurtSubsurfaceProfileParamLut;
    #define BURT_SUBSURFACE_PROFILE_PARAM_LUT_USE_LOAD 1
#endif
float _BurtSubsurfaceProfileParamLutEnabled;
float4 _BurtSubsurfaceProfileParamLutSize;
#endif

// 定义预积�?FG LUT 的尺寸；当前资源�?XRender 默认 128x128 生成
#define BURT_PREINTEGRATED_FG_LUT_SIZE (128.0f)
#define BURT_PREINTEGRATED_FG_LUT_INV_SIZE (1.0f / BURT_PREINTEGRATED_FG_LUT_SIZE)
#if BURT_ENABLE_SUBSURFACE_SHADING
#define BURT_SUBSURFACE_PREINTEGRATED_LUT_SIZE (256.0f)
#define BURT_SUBSURFACE_PREINTEGRATED_LUT_INV_SIZE (1.0f / BURT_SUBSURFACE_PREINTEGRATED_LUT_SIZE)
#define BURT_SUBSURFACE_SH_LUT_WIDTH (512.0f)
#define BURT_SUBSURFACE_SH_LUT_INV_WIDTH (1.0f / BURT_SUBSURFACE_SH_LUT_WIDTH)
#define BURT_SUBSURFACE_MAX_DUAL_SPECULAR_ROUGHNESS (2.0f)
#define BURT_SUBSURFACE_EXTINCTION_DECODE_SCALE (100.0f)
#define BURT_SUBSURFACE_PROFILE_PARAM_DUAL_SPECULAR_OFFSET (4.0f)
#define BURT_SUBSURFACE_PROFILE_PARAM_TRANSMISSION_OFFSET (5.0f)
#define BURT_SUBSURFACE_PROFILE_PARAM_TRANSMISSION_PROFILE_OFFSET (6.0f)
#define BURT_SUBSURFACE_PROFILE_PARAM_TRANSMISSION_PROFILE_SIZE (32.0f)
#define BURT_SUBSURFACE_PROFILE_PARAM_KERNEL0_OFFSET (38.0f)
#define BURT_SUBSURFACE_PROFILE_PARAM_TINT_OFFSET (2.0f)
#define BURT_SUBSURFACE_MAX_TRANSMISSION_PROFILE_DISTANCE (10.0f)
float4 BurtFetchSubsurfaceProfileParam(float sampleIndex, float profileIndex)
{
#if BURT_SUBSURFACE_PROFILE_PARAM_LUT_USE_LOAD
    int width = max((int)_BurtSubsurfaceProfileParamLutSize.x, 1);
    int height = max((int)_BurtSubsurfaceProfileParamLutSize.y, 1);
    int sample = clamp((int)floor(sampleIndex + 0.5f), 0, width - 1);
    int profile = clamp((int)floor(BurtClampSubsurfaceProfileIndex(profileIndex) + 0.5f), 0, height - 1);
    return _BurtSubsurfaceProfileParamLut.Load(int3(sample, profile, 0));
#else
    float width = max(_BurtSubsurfaceProfileParamLutSize.x, 1.0f);
    float height = max(_BurtSubsurfaceProfileParamLutSize.y, 1.0f);
    float resolvedProfile = BurtClampSubsurfaceProfileIndex(profileIndex);
    float2 uv = (float2(clamp(sampleIndex, 0.0f, width - 1.0f), resolvedProfile) + 0.5f) / float2(width, height);
    return BURT_SAMPLE_TEXTURE2D_CLAMP(_BurtSubsurfaceProfileParamLut, uv);
#endif
}

bool BurtUseSubsurfaceProfileParamLut()
{
    return _BurtSubsurfaceProfileParamLutEnabled > 0.5f && _BurtSubsurfaceProfileParamLutSize.x > 1.0f && _BurtSubsurfaceProfileParamLutSize.y > 0.5f;
}

float3 BurtSampleSubsurfacePreIntegratedLut(float rawNoL, float curvature, float profileIndex)
{
    float localInvSize = BURT_SUBSURFACE_PREINTEGRATED_LUT_INV_SIZE;
    int profileSlice = clamp((int)floor(BurtClampSubsurfaceProfileIndex(profileIndex) + 0.5f), 0, 7);
    float localU = saturate(rawNoL * 0.5f + 0.5f) * (1.0f - localInvSize) + 0.5f * localInvSize;
    float localV = saturate(curvature) * (1.0f - localInvSize) + 0.5f * localInvSize;
    return max(BURT_SAMPLE_TEXTURE2D_ARRAY_LOD_CLAMP(_BurtSubsurfacePreIntegratedLut, float2(localU, localV), profileSlice, 0.0f).rgb, float3(0.0f, 0.0f, 0.0f));
}

float3 BurtSampleSubsurfaceSHLut(float curvature, float shBand, float profileIndex)
{
    float localU = saturate(curvature) * (1.0f - BURT_SUBSURFACE_SH_LUT_INV_WIDTH) + 0.5f * BURT_SUBSURFACE_SH_LUT_INV_WIDTH;
    float localV = (clamp(shBand, 0.0f, 2.0f) + 0.5f) / 3.0f;
    int profileSlice = clamp((int)floor(BurtClampSubsurfaceProfileIndex(profileIndex) + 0.5f), 0, 7);
    return max(BURT_SAMPLE_TEXTURE2D_ARRAY_LOD_CLAMP(_BurtSubsurfaceSHLut, float2(localU, localV), profileSlice, 0.0f).rgb, float3(0.0f, 0.0f, 0.0f));
}

float4 BurtLoadSubsurfaceProfileDualSpecular(float profileIndex)
{
    if (BurtUseSubsurfaceProfileParamLut())
    {
        return BurtFetchSubsurfaceProfileParam(BURT_SUBSURFACE_PROFILE_PARAM_DUAL_SPECULAR_OFFSET, profileIndex);
    }

    int index = _BurtSubsurfaceProfileCount > 0.5f
        ? clamp((int)floor(profileIndex + 0.5f), 0, 7)
        : 0;
    return _BurtSubsurfaceProfileDualSpeculars[index];
}

int BurtResolveSubsurfaceProfileArrayIndex(float profileIndex)
{
    return _BurtSubsurfaceProfileCount > 0.5f
        ? clamp((int)floor(profileIndex + 0.5f), 0, 7)
        : 0;
}

float4 BurtLoadSubsurfaceProfileTransmission(float profileIndex)
{
    if (BurtUseSubsurfaceProfileParamLut())
    {
        return BurtFetchSubsurfaceProfileParam(BURT_SUBSURFACE_PROFILE_PARAM_TRANSMISSION_OFFSET, profileIndex);
    }

    return _BurtSubsurfaceProfileTransmissions[BurtResolveSubsurfaceProfileArrayIndex(profileIndex)];
}

float4 BurtLoadSubsurfaceProfileTransmissionTint(float profileIndex)
{
    if (BurtUseSubsurfaceProfileParamLut())
    {
        return BurtFetchSubsurfaceProfileParam(BURT_SUBSURFACE_PROFILE_PARAM_TINT_OFFSET, profileIndex);
    }

    return _BurtSubsurfaceProfileTransmissionTints[BurtResolveSubsurfaceProfileArrayIndex(profileIndex)];
}

float BurtDecodeSubsurfaceProfileExtinctionScale(float encodedExtinctionScale)
{
    return max(encodedExtinctionScale * BURT_SUBSURFACE_EXTINCTION_DECODE_SCALE, 0.01f);
}

float BurtDecodeSubsurfaceProfileScatteringDistribution(float encodedScatteringDistribution)
{
    return clamp(encodedScatteringDistribution * 2.0f - 1.0f, -0.99f, 0.99f);
}

float BurtResolveSubsurfaceProfileThickness(float materialThickness)
{
    return lerp(0.1f, 1.0f, saturate(materialThickness));
}

float BurtResolveSubsurfaceProfileThickness(float materialThickness, float resolvedTransmissionThickness)
{
    return resolvedTransmissionThickness >= 0.0f
        ? clamp(resolvedTransmissionThickness * BurtResolveSubsurfaceProfileThickness(materialThickness), 0.0f, BURT_SUBSURFACE_MAX_TRANSMISSION_PROFILE_DISTANCE)
        : BurtResolveSubsurfaceProfileThickness(materialThickness);
}

bool BurtHasResolvedSubsurfaceTransmissionThickness(float resolvedTransmissionThickness)
{
    return resolvedTransmissionThickness >= 0.0f;
}

float BurtResolveSubsurfaceTransmissionLightingThickness(
    float materialThickness,
    float resolvedTransmissionThickness,
    float nDotL)
{
    float thickness = BurtResolveSubsurfaceProfileThickness(materialThickness, resolvedTransmissionThickness);
    float threshold = lerp(0.1f, 0.01f, abs(nDotL));
    return clamp(max(thickness, threshold), 0.001f, BURT_SUBSURFACE_MAX_TRANSMISSION_PROFILE_DISTANCE);
}

float3 BurtSampleSubsurfaceTransmissionProfileByThickness(float profileIndex, float profileThickness)
{
    float4 profileTransmission = BurtLoadSubsurfaceProfileTransmission(profileIndex);
    float extinctionScale = BurtDecodeSubsurfaceProfileExtinctionScale(profileTransmission.x);
    profileThickness = clamp(profileThickness, 0.0f, BURT_SUBSURFACE_MAX_TRANSMISSION_PROFILE_DISTANCE);
    float3 fallback = exp2(-1.442695f * extinctionScale * profileThickness).xxx;
    if (!BurtUseSubsurfaceProfileParamLut())
    {
        return fallback;
    }

    float samplePosition = saturate(profileThickness / BURT_SUBSURFACE_MAX_TRANSMISSION_PROFILE_DISTANCE) * (BURT_SUBSURFACE_PROFILE_PARAM_TRANSMISSION_PROFILE_SIZE - 1.0f);
    float lowerSample = floor(samplePosition);
    float upperSample = min(lowerSample + 1.0f, BURT_SUBSURFACE_PROFILE_PARAM_TRANSMISSION_PROFILE_SIZE - 1.0f);
    float sampleT = samplePosition - lowerSample;
    float3 lower = BurtFetchSubsurfaceProfileParam(BURT_SUBSURFACE_PROFILE_PARAM_TRANSMISSION_PROFILE_OFFSET + lowerSample, profileIndex).rgb;
    float3 upper = BurtFetchSubsurfaceProfileParam(BURT_SUBSURFACE_PROFILE_PARAM_TRANSMISSION_PROFILE_OFFSET + upperSample, profileIndex).rgb;
    return max(lerp(lower, upper, sampleT), float3(0.0f, 0.0f, 0.0f));
}

float3 BurtSampleSubsurfaceTransmissionProfile(float profileIndex, float materialThickness)
{
    return BurtSampleSubsurfaceTransmissionProfileByThickness(profileIndex, BurtResolveSubsurfaceProfileThickness(materialThickness));
}

float3 BurtSampleSubsurfaceTransmissionThroughputByThickness(float profileIndex, float profileThickness)
{
    float3 throughput = BurtSampleSubsurfaceTransmissionProfileByThickness(profileIndex, profileThickness);
    if (!BurtUseSubsurfaceProfileParamLut())
    {
        throughput *= max(BurtLoadSubsurfaceProfileTransmissionTint(profileIndex).rgb, float3(0.0f, 0.0f, 0.0f));
    }

    return max(throughput, float3(0.0f, 0.0f, 0.0f));
}

float3 BurtSampleSubsurfaceTransmissionThroughput(float profileIndex, float materialThickness)
{
    return BurtSampleSubsurfaceTransmissionThroughputByThickness(profileIndex, BurtResolveSubsurfaceProfileThickness(materialThickness));
}

float3 BurtEvaluateSubsurfaceProfileTransmittanceByExtinction(float profileIndex, float thickness)
{
    float4 profileTransmission = BurtLoadSubsurfaceProfileTransmission(profileIndex);
    float extinctionScale = BurtDecodeSubsurfaceProfileExtinctionScale(profileTransmission.x);
    return max(exp2(-1.442695f * extinctionScale * max(thickness, 0.0f)).xxx, float3(0.0f, 0.0f, 0.0f));
}

float BurtEvaluateSubsurfaceProfileIntensity(float3 profileColor)
{
    return max(dot(max(profileColor, float3(0.0f, 0.0f, 0.0f)), float3(0.3f, 0.59f, 0.11f)), 0.0f);
}

float BurtHenyeyGreensteinPhase(float anisotropy, float cosTheta)
{
    float g = clamp(anisotropy, -0.99f, 0.99f);
    float g2 = g * g;
    float denominator = pow(max(1.0f + g2 + 2.0f * g * cosTheta, 0.001f), 1.5f);
    return (1.0f - g2) / max(4.0f * BURT_PI * denominator, BURT_EPSILON);
}

#endif

// 出处：XRender/Shaders/Library/BRDF.hlsl::rcp_safe；用安全倒数保护 BRDF 分母，避�?0 产生 NaN
float rcp_safe(float value)
{
    // XRender 原实现使�?1e-7，这里沿�?BurtRP 通用 epsilon 统一数值保护下限
return rcp(max(value, BURT_EPSILON));
}

// 出处：XRender/Shaders/Library/Math.hlsl::Pow5；Schlick Fresnel �?DFG 近似都会用到五次项
float Pow5(float value)
{
    // 先算平方，减少重复乘法，也避免直�?pow 带来的平台差异
float value2 = value * value;

    // value^5 = value^2 * value^2 * value
return value2 * value2 * value;
}

float Pow4(float value)
{
    float value2 = value * value;
    return value2 * value2;
}

// 出处：XRender/Shaders/Library/CommonColors.hlsl::PerceivedLuminance；Energy Preservation 用感知亮度把 RGB 反射能量压成单通道
float PerceivedLuminance(float3 color)
{
    // XRender 使用 Rec.601 风格权重，和真实亮度 Luminance 区分开，目的是让调试和能量估算更贴近人眼观感
return dot(color, float3(0.3f, 0.59f, 0.11f));
}

// 出处：XRender/Shaders/Library/CommonMaterial.hlsl::DiffuseColorFromBaseColor；金属材质不保留普�?diffuse
float3 DiffuseColorFromBaseColor(float3 baseColor, float metallic)
{
    // metallic 越高，baseColor 越多转移�?specular F0，diffuse 越少
return baseColor * (1.0f - metallic);
}

// 出处：XRender/Shaders/Library/CommonMaterial.hlsl::DielectricReflectanceToF0；Frostbite reflectance �?F0 的映射
float3 DielectricReflectanceToF0(float3 baseColor, float reflectance, float metallic)
{
    // XRender 公式：MATERIAL_MAX_DIELECTRIC_F0 * Reflectance^2 * (1 - Metallic) + BaseColor * Metallic
float dielectricF0 = BURT_MATERIAL_MAX_DIELECTRIC_F0 * saturate(reflectance) * saturate(reflectance);

    // 非金�?F0 �?reflectance 映射得到，金�?F0 来自 baseColor
return dielectricF0 * (1.0f - saturate(metallic)) + baseColor * saturate(metallic);
}

// 出处：XRender/Shaders/Library/CommonMaterial.hlsl::PerceptualSmoothnessToPerceptualRoughness；BurtRP 面板仍暴�?Smoothness
float PerceptualSmoothnessToPerceptualRoughness(float perceptualSmoothness)
{
    // smoothness 越高 roughness 越低，Deferred 后续也应按同一规则�?GBuffer 还原
return 1.0f - saturate(perceptualSmoothness);
}

// 出处：XRender/Shaders/Library/CommonMaterial.hlsl::ClampPerceptualRoughness；避免完全镜面造成数值尖峰
float ClampPerceptualRoughness(float perceptualRoughness)
{
    // BurtRP 当前最小粗糙度略高�?XRender PC 默认值，用来控制�?TAA 阶段的高光稳定性
return clamp(perceptualRoughness, BURT_MIN_PERCEPTUAL_ROUGHNESS, 1.0f);
}

// BurtRP 表面数据适配函数：把面板 smoothness 转成 XRender 语义�?Base.Roughness
float GetSurfacePerceptualRoughness(BurtSurfaceData surfaceData)
{
    // 先做 smoothness -> roughness，再�?XRender �?ClampPerceptualRoughness 语义做下限保护
return ClampPerceptualRoughness(PerceptualSmoothnessToPerceptualRoughness(surfaceData.Smoothness));
}

// 出处：XRender/Shaders/Library/CommonMaterial.hlsl::GeometricNormalVariance；估算屏幕空间法线变化导致的高光方差
float GeometricNormalVariance(float3 geometricNormalWS, float screenSpaceVariance)
{
    // 先安全归一化法线，避免长度误差放大 ddx/ddy 的方差
float3 safeNormalWS = BurtSafeNormalize(geometricNormalWS);

    // 计算当前像素在屏�?x 方向的法线变化，导数越大表示高光越容易闪烁或漏采样
float3 deltaX = ddx(safeNormalWS);

    // 计算当前像素在屏�?y 方向的法线变化，�?x 方向一起描述一个像素覆盖范围内的法线分布
float3 deltaY = ddy(safeNormalWS);

    // 把两个方向的变化量转成方差，并乘以屏幕空间权重
return screenSpaceVariance * (dot(deltaX, deltaX) + dot(deltaY, deltaY));
}

// 出处：XRender/Shaders/Library/CommonMaterial.hlsl::NormalFiltering；把法线方差折算回感知粗糙度
float NormalFiltering(float perceptualRoughness, float variance, float threshold)
{
    // Ref: Geometry into Shading, equation (3)；阈值限制过滤过强导致高光被过度拓宽
float squaredPerceptualRoughness = saturate(perceptualRoughness * perceptualRoughness + min(2.0f * variance, threshold * threshold));

    // 开平方回到感知粗糙度空间，材质滑块和后�?DFG 仍然使用同一层语义
return sqrt(squaredPerceptualRoughness);
}

// 出处：XRender/Shaders/Library/CommonMaterial.hlsl::GeometricNormalFiltering；对几何法线应用 Specular AA
float GeometricNormalFiltering(float perceptualRoughness, float3 geometricNormalWS, float screenSpaceVariance, float threshold)
{
    // 先估算法线方差，再交�?NormalFiltering 转成过滤后的感知粗糙度
float variance = GeometricNormalVariance(geometricNormalWS, screenSpaceVariance);

    // 只允许过滤增加粗糙度，不允许把材质本身变得更光滑
return max(perceptualRoughness, NormalFiltering(perceptualRoughness, variance, threshold));
}

// 保存 Specular AA 中间项，方便 Debug View 同时观察法线方差和粗糙度拓宽幅度
struct BurtSpecularAATerms
{
    // 保存材质原始感知粗糙度，也就是未经过屏幕空间法线过滤�?Base.Roughness
float MaterialPerceptualRoughness;

    // 保存 GeometricNormalVariance 的结果，数值越大表示像素内法线变化越强
float NormalVariance;

    // 保存 Specular AA 后的感知粗糙度，直接高光会使用这个值
float FilteredPerceptualRoughness;

    // 保存 filtered - material 的差值，越大表示 Specular AA 对高光拓宽越明显
float RoughnessDelta;
};

// Returns the full Specular AA terms for rendering and debug views.
BurtSpecularAATerms BurtEvaluateSpecularAATerms(float materialPerceptualRoughness, float3 geometricNormalWS)
{
    BurtSpecularAATerms terms;
    terms.MaterialPerceptualRoughness = materialPerceptualRoughness;
    terms.NormalVariance = GeometricNormalVariance(geometricNormalWS, BURT_SPECULAR_AA_SCREEN_SPACE_VARIANCE);
    terms.FilteredPerceptualRoughness = max(materialPerceptualRoughness, NormalFiltering(materialPerceptualRoughness, terms.NormalVariance, BURT_SPECULAR_AA_THRESHOLD));
    terms.RoughnessDelta = max(terms.FilteredPerceptualRoughness - terms.MaterialPerceptualRoughness, 0.0f);
    return terms;
}

// BurtRP 直接高光适配函数：材质粗糙度 + XRender 几何法线过滤
float GetDirectSpecularPerceptualRoughness(BurtSurfaceData surfaceData, float3 normalWS)
{
    // 先取得材质本身的感知粗糙度，也就�?1 - smoothness 后的结果
float materialRoughness = GetSurfacePerceptualRoughness(surfaceData);

    // 再把屏幕空间法线变化折算进去，避免极光滑材质的高光小到被像素漏采样
return BurtEvaluateSpecularAATerms(materialRoughness, normalWS).FilteredPerceptualRoughness;
}

// 出处：XRender/Shaders/Library/CommonMaterial.hlsl::PerceptualRoughnessToLinearRoughness；感知粗糙度转线性粗糙度
float PerceptualRoughnessToLinearRoughness(float perceptualRoughness)
{
    // 线�?roughness 使用感知 roughness 的平方，让材质滑块在视觉上更均匀
return max(perceptualRoughness * perceptualRoughness, BURT_EPSILON);
}

// 出处：XRender/Shaders/Library/CommonMaterial.hlsl::LinearRoughnessToA2；线性粗糙度�?GGX D 项使用的 A2
float LinearRoughnessToA2(float linearRoughness)
{
    // A2 太小会让高光尖峰过高，所以用 BURT_EPSILON 做最低保护
return max(linearRoughness * linearRoughness, BURT_EPSILON);
}

// 出处：XRender/Shaders/Library/CommonMaterial.hlsl::ApproximateF90；默认掠射角反射端点
float3 ApproximateF90(float3 f0)
{
    // XRender 当前 DefaultLit 路径返回 1；参数保留在签名里，方便后续接入自定�?F90 或薄膜材质
return float3(1.0f, 1.0f, 1.0f);
}

// 保存 PBR 材质侧可�?Forward SurfaceData 或未�?Deferred GBuffer 还原的数据，避免光照函数直接依赖材质面板结构
struct BurtPBRMaterialData
{
    // 保存材质基础�?RGB，Deferred 后续可以�?GBuffer 还原
float3 BaseColor;

    // 保存金属度，既参�?diffuseColor，也参与 reflectance �?F0 的重建
float Metallic;

    float Anisotropy;

    // 保存 XRender 风格 reflectance；不直接暴露 F0，后续统一�?reflectance 还原
float Reflectance;

    // 保存环境遮蔽，用于间接漫反射和间接高光遮蔽
float Occlusion;

    // 保存面板 smoothness，Debug View �?GBuffer 检查仍然需要看原始语义
float Smoothness;

    // 保存 XRender Base.Roughness，也就是 BurtRP smoothness 转换后的感知粗糙度
float PerceptualRoughness;

    // 保存线性粗糙度，供 GGX 可见性、Specular Occlusion 等公式复用
float LinearRoughness;

    // 保存 GGX D 项使用的 a^2，减少直接光中重复计算
float A2;

    // 保存金属度扣除后�?diffuseColor，对�?XRender GenericData.DiffuseColor
float3 DiffuseColor;

    // 保存�?baseColor、reflectance、metallic 重建�?F0，对�?XRender GenericData.F0
float3 F0;

    // 保存默认掠射角端点，对应 XRender GenericData.F90
float3 F90;

    float ClearCoatMask;

    float ClearCoatRoughness;

    float SubsurfaceActive;

    float SubsurfaceThickness;

    float SubsurfacePower;

    float SubsurfaceDistortion;

    float SubsurfaceAmbient;

    float SubsurfaceScatteringMode;

    float Subsurface3SCurvature;

    float SubsurfaceProfileIndex;

    float FabricActive;

    float FabricIsSilk;

    float FabricFuzzWeight;

    float FabricFuzzRoughness;

    float3 FabricFuzzColor;

    float FoliageActive;

    float3 FoliageTransmissionColor;

    float FoliageTransmissionWeight;

    float FoliageThickness;

    float FoliageBackLight;

    float FoliageTransmissionNdotL;

    float FoliageSpecularScale;

    float FoliageUseSpecularColor;

    float FoliageScreenSpaceShadowIntensity;

    float FoliageIsGrass;
};

// Prepares PBR material data from surface inputs.
BurtPBRMaterialData BurtPreparePBRMaterialData(BurtSurfaceData surfaceData)
{
    BurtPBRMaterialData materialData;

#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL || BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    materialData.Metallic = 0.0f;
#elif BURT_ENABLE_SUBSURFACE_SHADING || BURT_ENABLE_FOLIAGE_SHADING
    if (BurtIsActiveSubsurfaceShadingModel(surfaceData.ShadingModelID) || BurtIsActiveFoliageShadingModel(surfaceData.ShadingModelID))
    {
        materialData.Metallic = 0.0f;
    }
    else
    {
        materialData.Metallic = saturate(surfaceData.Metallic);
    }
#else
    materialData.Metallic = saturate(surfaceData.Metallic);
#endif
    materialData.BaseColor = surfaceData.BaseColor.rgb;
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL || BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    materialData.Anisotropy = 0.0f;
#elif BURT_ENABLE_SUBSURFACE_SHADING || BURT_ENABLE_FOLIAGE_SHADING
    materialData.Anisotropy = (BurtIsActiveSubsurfaceShadingModel(surfaceData.ShadingModelID) || BurtIsActiveFoliageShadingModel(surfaceData.ShadingModelID)) ? 0.0f : clamp(surfaceData.Anisotropy, -1.0f, 1.0f);
#else
    materialData.Anisotropy = clamp(surfaceData.Anisotropy, -1.0f, 1.0f);
#endif
#if BURT_ACTIVE_CLEAR_COAT_SHADING_MODEL
    materialData.ClearCoatMask = saturate(surfaceData.ClearCoatMask);
#elif BURT_ENABLE_CLEAR_COAT_SHADING
    materialData.ClearCoatMask = BurtIsActiveClearCoatShadingModel(surfaceData.ShadingModelID) ? saturate(surfaceData.ClearCoatMask) : 0.0f;
#else
    materialData.ClearCoatMask = 0.0f;
#endif
    materialData.ClearCoatRoughness = ClampPerceptualRoughness(surfaceData.ClearCoatRoughness);
    materialData.SubsurfaceActive = BurtIsSubsurfaceShadingModel(surfaceData.ShadingModelID) ? 1.0f : 0.0f;
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    materialData.SubsurfaceThickness = saturate(surfaceData.SubsurfaceThickness);
    materialData.SubsurfacePower = BurtClampSubsurfacePower(surfaceData.SubsurfacePower);
    materialData.SubsurfaceDistortion = saturate(surfaceData.SubsurfaceDistortion);
    materialData.SubsurfaceAmbient = saturate(surfaceData.SubsurfaceAmbient);
    materialData.SubsurfaceScatteringMode = BurtClampSubsurfaceScatteringMode(surfaceData.SubsurfaceScatteringMode);
    materialData.Subsurface3SCurvature = saturate(surfaceData.Subsurface3SCurvature);
    materialData.SubsurfaceProfileIndex = BurtClampSubsurfaceProfileIndex(surfaceData.SubsurfaceProfileIndex);
#elif BURT_ENABLE_SUBSURFACE_SHADING
    if (BurtIsActiveSubsurfaceShadingModel(surfaceData.ShadingModelID))
    {
        materialData.SubsurfaceThickness = saturate(surfaceData.SubsurfaceThickness);
        materialData.SubsurfacePower = BurtClampSubsurfacePower(surfaceData.SubsurfacePower);
        materialData.SubsurfaceDistortion = saturate(surfaceData.SubsurfaceDistortion);
        materialData.SubsurfaceAmbient = saturate(surfaceData.SubsurfaceAmbient);
        materialData.SubsurfaceScatteringMode = BurtClampSubsurfaceScatteringMode(surfaceData.SubsurfaceScatteringMode);
        materialData.Subsurface3SCurvature = saturate(surfaceData.Subsurface3SCurvature);
        materialData.SubsurfaceProfileIndex = BurtClampSubsurfaceProfileIndex(surfaceData.SubsurfaceProfileIndex);
    }
    else
    {
        materialData.SubsurfaceThickness = BURT_SUBSURFACE_DEFAULT_THICKNESS;
        materialData.SubsurfacePower = BURT_SUBSURFACE_DEFAULT_POWER;
        materialData.SubsurfaceDistortion = BURT_SUBSURFACE_DEFAULT_DISTORTION;
        materialData.SubsurfaceAmbient = BURT_SUBSURFACE_DEFAULT_AMBIENT;
        materialData.SubsurfaceScatteringMode = BURT_SUBSURFACE_DEFAULT_SCATTERING_MODE;
        materialData.Subsurface3SCurvature = 1.0f - BURT_SUBSURFACE_DEFAULT_THICKNESS;
        materialData.SubsurfaceProfileIndex = BURT_SUBSURFACE_DEFAULT_PROFILE_INDEX;
    }
#else
    materialData.SubsurfaceThickness = BURT_SUBSURFACE_DEFAULT_THICKNESS;
    materialData.SubsurfacePower = BURT_SUBSURFACE_DEFAULT_POWER;
    materialData.SubsurfaceDistortion = BURT_SUBSURFACE_DEFAULT_DISTORTION;
    materialData.SubsurfaceAmbient = BURT_SUBSURFACE_DEFAULT_AMBIENT;
    materialData.SubsurfaceScatteringMode = BURT_SUBSURFACE_DEFAULT_SCATTERING_MODE;
    materialData.Subsurface3SCurvature = 1.0f - BURT_SUBSURFACE_DEFAULT_THICKNESS;
    materialData.SubsurfaceProfileIndex = BURT_SUBSURFACE_DEFAULT_PROFILE_INDEX;
#endif
    materialData.FabricActive = BurtIsFabricShadingModel(surfaceData.ShadingModelID) ? 1.0f : 0.0f;
    materialData.FabricIsSilk = saturate(surfaceData.FabricIsSilk);
    materialData.FabricFuzzWeight = saturate(surfaceData.FabricFuzzWeight);
    materialData.FabricFuzzRoughness = ClampPerceptualRoughness(surfaceData.FabricFuzzRoughness);
    materialData.FabricFuzzColor = max(surfaceData.FabricFuzzColor, float3(0.0f, 0.0f, 0.0f));
    materialData.FoliageActive = BurtIsFoliageShadingModel(surfaceData.ShadingModelID) ? 1.0f : 0.0f;
    materialData.FoliageTransmissionColor = max(surfaceData.FoliageTransmissionColor, float3(0.0f, 0.0f, 0.0f));
    materialData.FoliageTransmissionWeight = surfaceData.FoliageIsGrass > 0.5f
        ? max(surfaceData.FoliageTransmissionWeight, 0.0f)
        : saturate(surfaceData.FoliageTransmissionWeight);
    materialData.FoliageThickness = saturate(surfaceData.FoliageThickness);
    materialData.FoliageBackLight = saturate(surfaceData.FoliageBackLight);
    materialData.FoliageTransmissionNdotL = saturate(surfaceData.FoliageTransmissionNdotL);
    materialData.FoliageSpecularScale = saturate(surfaceData.FoliageSpecularScale);
    materialData.FoliageUseSpecularColor = saturate(surfaceData.FoliageUseSpecularColor);
    materialData.FoliageScreenSpaceShadowIntensity = max(surfaceData.FoliageScreenSpaceShadowIntensity, 0.0f);
    materialData.FoliageIsGrass = saturate(surfaceData.FoliageIsGrass);
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    materialData.Reflectance = BURT_SUBSURFACE_FIXED_REFLECTANCE;
#elif BURT_ENABLE_SUBSURFACE_SHADING
    materialData.Reflectance = BurtIsActiveSubsurfaceShadingModel(surfaceData.ShadingModelID) ? BURT_SUBSURFACE_FIXED_REFLECTANCE : surfaceData.Reflectance;
#else
    materialData.Reflectance = surfaceData.Reflectance;
#endif
    materialData.Occlusion = surfaceData.Occlusion;
    materialData.Smoothness = surfaceData.Smoothness;

    materialData.PerceptualRoughness = GetSurfacePerceptualRoughness(surfaceData);
    materialData.LinearRoughness = PerceptualRoughnessToLinearRoughness(materialData.PerceptualRoughness);
    materialData.A2 = LinearRoughnessToA2(materialData.LinearRoughness);

#if defined(BURT_SUBSURFACE_DEFERRED_POSTPROCESS_INPUT) && BURT_ENABLE_SUBSURFACE_SHADING
    float3 diffuseBaseColor = BurtIsActiveSubsurfaceShadingModel(surfaceData.ShadingModelID) && !BurtIsSubsurface3SPreIntegratedMode(materialData.SubsurfaceScatteringMode) ? float3(1.0f, 1.0f, 1.0f) : materialData.BaseColor;
#else
    float3 diffuseBaseColor = materialData.BaseColor;
#endif

    materialData.DiffuseColor = DiffuseColorFromBaseColor(diffuseBaseColor, materialData.Metallic);
    materialData.F0 = DielectricReflectanceToF0(materialData.BaseColor, materialData.Reflectance, materialData.Metallic);
    materialData.F90 = ApproximateF90(materialData.F0);
    if (materialData.FoliageActive > 0.5f)
    {
        materialData.F90 = materialData.FoliageIsGrass > 0.5f
            ? saturate((materialData.BaseColor * 0.9f + 0.1f) * materialData.FoliageSpecularScale * 3.0f)
            : saturate(materialData.BaseColor * materialData.FoliageSpecularScale);
    }

    return materialData;
}

float BurtGetSubsurfaceMaterialWeight(BurtPBRMaterialData materialData)
{
    return saturate(materialData.SubsurfaceActive);
}

// 保存 PBR 几何侧数据；Forward 由插值输入生成，Deferred 后续�?GBuffer normal 和重�?view direction 生成
struct BurtPBRGeometryData
{
    // 保存安全归一化后的世界空间法线
float3 NormalWS;

    float3 TangentWS;

    float3 BitangentWS;

    // 保存安全归一化后的世界空间视线方向，约定从表面指向相机
float3 ViewDirectionWS;

    // 保存 NdotV，Energy Term、DFG �?Specular Occlusion 都会复用
float NDotV;

    // 保存环境反射方向，Reflection Probe / Sky Specular 会复用
float3 ReflectionDirectionWS;
};

// 从世界空间法线和视线方向准备 PBR 几何数据，方�?Forward �?Deferred 使用同一套几何约定
float3 BurtCreateFallbackTangentWS(float3 normalWS)
{
    float3 axis = abs(normalWS.y) < 0.95f ? float3(0.0f, 1.0f, 0.0f) : float3(1.0f, 0.0f, 0.0f);
    return BurtSafeNormalize(cross(axis, normalWS));
}

float3 BurtOrthonormalizeTangentWS(float3 normalWS, float3 tangentWS)
{
    float3 tangent = tangentWS - normalWS * dot(normalWS, tangentWS);
    float tangentLengthSquared = dot(tangent, tangent);
    return tangentLengthSquared > 0.0001f ? tangent * rsqrt(tangentLengthSquared) : BurtCreateFallbackTangentWS(normalWS);
}

BurtPBRGeometryData BurtPreparePBRGeometryData(float3 normalWS, float4 tangentWS, float3 viewDirectionWS)
{
    BurtPBRGeometryData geometryData;
    geometryData.NormalWS = BurtSafeNormalize(normalWS);
    geometryData.ViewDirectionWS = BurtSafeNormalize(viewDirectionWS);
    geometryData.TangentWS = BurtOrthonormalizeTangentWS(geometryData.NormalWS, tangentWS.xyz);
    geometryData.BitangentWS = BurtSafeNormalize(cross(geometryData.NormalWS, geometryData.TangentWS) * (tangentWS.w < 0.0f ? -1.0f : 1.0f));
    geometryData.NDotV = saturate(dot(geometryData.NormalWS, geometryData.ViewDirectionWS));
    geometryData.ReflectionDirectionWS = reflect(-geometryData.ViewDirectionWS, geometryData.NormalWS);
    return geometryData;
}

BurtPBRGeometryData BurtPreparePBRGeometryData(float3 normalWS, float3 tangentWS, float3 viewDirectionWS)
{
    return BurtPreparePBRGeometryData(normalWS, float4(tangentWS, 1.0f), viewDirectionWS);
}

BurtPBRGeometryData BurtPreparePBRGeometryData(float3 normalWS, float3 viewDirectionWS)
{
    float3 safeNormalWS = BurtSafeNormalize(normalWS);
    return BurtPreparePBRGeometryData(safeNormalWS, float4(BurtCreateFallbackTangentWS(safeNormalWS), 1.0f), viewDirectionWS);
}

// BurtRP 直接高光适配函数：使用已经准备好的材�?几何数据计算 Specular AA 后的感知粗糙度
float GetDirectSpecularPerceptualRoughness(BurtPBRMaterialData materialData, BurtPBRGeometryData geometryData)
{
    // XRender 几何法线过滤只影响直接高光，不回写材�?Base.Roughness
return BurtEvaluateSpecularAATerms(materialData.PerceptualRoughness, geometryData.NormalWS).FilteredPerceptualRoughness;
}

// Returns Specular AA terms from prepared material and geometry data.
BurtSpecularAATerms BurtEvaluateSpecularAATerms(BurtPBRMaterialData materialData, BurtPBRGeometryData geometryData)
{
    // 复用 float 版本，保证调试值和直接高光实际使用的过滤逻辑完全一致
return BurtEvaluateSpecularAATerms(materialData.PerceptualRoughness, geometryData.NormalWS);
}

// 出处：XRender/Shaders/Library/BRDF.hlsl::Fd_Lambert；标�?Lambert 漫反�?lobe
float Fd_Lambert()
{
    // XRender 返回 INV_PI；BurtRP 使用同一常量，确�?direct/indirect diffuse 的能量尺度一致
return BURT_INV_PI;
}

float Fd_Lambert_Fabric(float perceptualRoughness)
{
    return BURT_INV_PI * lerp(1.0f, 0.5f, saturate(perceptualRoughness));
}

// 出处：XRender/Shaders/Library/BRDF.hlsl::Fd_Diffuse_Burley；Disney/Burley 粗糙漫反�?lobe
float Fd_Diffuse_Burley(float roughness, float noV, float noL, float voH)
{
    // XRender 公式：FD90 = 0.5 + 2 * VoH^2 * Roughness
float fd90 = 0.5f + 2.0f * voH * voH * roughness;

    // 视线侧散射项，掠射角会增强粗糙漫反射
float fdV = 1.0f + (fd90 - 1.0f) * Pow5(1.0f - saturate(noV));

    // 光照侧散射项，和视线侧一起构�?Burley diffuse
float fdL = 1.0f + (fd90 - 1.0f) * Pow5(1.0f - saturate(noL));

    // 返回�?1/PI �?diffuse lobe
return BURT_INV_PI * fdV * fdL;
}

#ifndef BURT_USE_DISNEY_DIFFUSE
#define BURT_USE_DISNEY_DIFFUSE 0
#endif

// 出处：XRender/Shaders/SlabLobes/SL_Diffuse.hlsl::SlabLobe_Diffuse；当前默�?Lambert，保�?Burley 分支给后续材�?质量级别切换
float SlabLobe_Diffuse(BurtPBRMaterialData materialData, float noV, float noL, float voH)
{
#if BURT_USE_DISNEY_DIFFUSE
    // Burley 使用材质 Base.Roughness，不使用 Specular AA 后的直接高光 roughness
return Fd_Diffuse_Burley(materialData.PerceptualRoughness, noV, noL, voH);
#else
    // 默认沿用当前 Lambert 结果，避免本轮整理改变已�?diffuse 观感
return Fd_Lambert();
#endif
}

// 出处：XRender/Shaders/Library/BRDF.hlsl::F_Schlick；标�?Schlick Fresnel，u 对应 VoH/LoH
float3 F_Schlick(float3 f0, float3 f90, float u)
{
    // 返回�?F0 �?F90 的角度相关反射率，掠射角会更接近 F90
return f0 + (f90 - f0) * Pow5(1.0f - saturate(u));
}

// 出处：XRender/Shaders/Library/BRDF.hlsl::F_Schlick；默�?F90 �?1 的重载
float3 F_Schlick(float3 f0, float u)
{
    // 旧接口等价于常见�?f0 -> 1 �?Schlick 近似
return F_Schlick(f0, float3(1.0f, 1.0f, 1.0f), u);
}

float3 F_Schlick_UE(float3 specularColor, float voH)
{
    float fc = Pow5(1.0f - saturate(voH));
    return saturate(50.0f * specularColor.g) * fc + (1.0f - fc) * specularColor;
}

// 出处：XRender/Shaders/Library/BRDF.hlsl::F_Schlick_UE；BurtRP 直接高光使用这个名字便于�?XRender 溯源
float3 F_Schlick_UE(float3 f0, float3 f90, float voH)
{
    // XRender 的三参数版本等价�?F90 * Fc + (1 - Fc) * F0
return F_Schlick(f0, f90, voH);
}

float3 F_Schlick_Fabric(float3 f0, float f90, float u)
{
    return f0 + (f90 - f0) * Pow4(1.0f - saturate(u));
}

float BurtClothEnergyLookup(float roughness, float noV)
{
    float c = saturate(noV);
    float r = saturate(roughness);
    return (0.526422f / ((-0.227114f + r) * (-0.968835f + r) * ((5.38869f - 20.2835f * c) * r) - (-1.18761f - ((2.58744f - c) * c)))) + 0.0615456f;
}

float BurtComputeWrappedDiffuseLighting(float noL, float wrap)
{
    float safeWrap = saturate(wrap);
    float denominator = (1.0f + safeWrap) * (1.0f + safeWrap);
    return saturate((noL + safeWrap) / max(denominator, BURT_EPSILON));
}

float BurtRefractBlendClearCoatApprox(float voH)
{
    float safeVoH = saturate(voH);
    return (0.63f - 0.22f * safeVoH) * safeVoH - 0.745f;
}

float3 BurtClearCoatFresnelTransmission(float3 clearCoatFresnel)
{
    float3 transmission = saturate(1.0f - clearCoatFresnel);
    return transmission * transmission;
}

float3 BurtSimpleClearCoatTransmittance(float noL, float noV, float metallic, float3 baseColor)
{
    float3 transmittance = float3(1.0f, 1.0f, 1.0f);
    float clearCoatCoverage = saturate(metallic);
    if (clearCoatCoverage > 0.0001f)
    {
        const float layerThickness = 1.0f;
        float thinDistance = layerThickness * (rcp_safe(max(noV, 0.001f)) + rcp_safe(max(noL, 0.001f)));
        thinDistance = min(thinDistance, 4.0f);
        float3 transmittanceColor = max(baseColor * Fd_Lambert(), float3(0.001f, 0.001f, 0.001f));
        float3 extinctionCoefficient = -log(transmittanceColor) / (2.0f * layerThickness);
        float3 opticalDepth = extinctionCoefficient * max(thinDistance - 2.0f * layerThickness, 0.0f);
        transmittance = exp(-opticalDepth);
        transmittance = lerp(float3(1.0f, 1.0f, 1.0f), transmittance, clearCoatCoverage);
    }

    return transmittance;
}

float3 BurtSimpleClearCoatTransmittanceFromView(float noV, float metallic, float3 baseColor)
{
    return BurtSimpleClearCoatTransmittance(noV, noV, metallic, baseColor);
}

float3 BurtTransmittanceToExtinction(float3 transmittanceColor, float thicknessInMeters)
{
    return -log(clamp(transmittanceColor, BURT_PARTICIPATING_MEDIA_MIN_TRANSMITTANCE, 1.0f)) / max(BURT_PARTICIPATING_MEDIA_MIN_MFP_METER, thicknessInMeters);
}

float3 BurtTransmittanceToMeanFreePath(float3 transmittanceColor, float thicknessInMeters)
{
    return 1.0f / max(BURT_PARTICIPATING_MEDIA_MIN_EXTINCTION, BurtTransmittanceToExtinction(transmittanceColor, thicknessInMeters));
}

float3 BurtExtinctionToTransmittance(float3 extinction, float thicknessInMeters)
{
    return exp(-extinction * thicknessInMeters);
}

float3 BurtIsotropicMediumSlabTransmittance(float3 extinctionCoef, float thicknessInMeters, float noV)
{
    float3 safeExtinction = max(float3(0.000001f, 0.000001f, 0.000001f), extinctionCoef);
    float pathLength = thicknessInMeters / max(0.0001f, abs(noV));
    return BurtExtinctionToTransmittance(safeExtinction, pathLength);
}

float3 BurtEvaluateFoliageSlabSubsurfaceColor(float3 foliageTransmissionColor)
{
    float3 meanFreePath = BurtTransmittanceToMeanFreePath(foliageTransmissionColor, BURT_VOLUME_DEFAULT_THICKNESS_M);
    float3 minMeanFreePath = float3(
        BURT_PARTICIPATING_MEDIA_MIN_MFP_METER,
        BURT_PARTICIPATING_MEDIA_MIN_MFP_METER,
        BURT_PARTICIPATING_MEDIA_MIN_MFP_METER);
    float3 extinction = 1.0f / max(minMeanFreePath, meanFreePath);
    return BurtIsotropicMediumSlabTransmittance(extinction, BURT_VOLUME_DEFAULT_THICKNESS_M, 1.0f);
}

// 出处：XRender/Shaders/Library/BRDF.hlsl::D_GGX；Unreal/Frostbite/Filament 使用�?GGX/Trowbridge-Reitz D 项
float D_GGX(float a2, float noH)
{
    // XRender 原式：d = (NoH * A2 - NoH) * NoH + 1
float denom = (noH * a2 - noH) * noH + 1.0f;

    // 分母只做极小保护，避免高 smoothness 时峰值被 1e-6 这类通用 epsilon 截断
return a2 / max(BURT_PI * denom * denom, BURT_GGX_DISTRIBUTION_DENOMINATOR_EPSILON);
}

float D_Charlie(float linearRoughness, float noH)
{
    float invAlpha = rcp_safe(max(linearRoughness, 0.001f));
    float cos2h = noH * noH;
    float sin2h = max(1.0f - cos2h, 0.001f);
    return (2.0f + invAlpha) * pow(sin2h, invAlpha * 0.5f) * (0.5f * BURT_INV_PI);
}

float V_Neubelt(float noV, float noL)
{
    return rcp_safe(4.0f * (saturate(noL) + saturate(noV) - saturate(noL) * saturate(noV)));
}

// 出处：XRender/Shaders/Library/BRDF.hlsl::Vis_SmithJointApprox；返�?G / (4 * NoV * NoL)
float D_GGX_Anisotropic(float ax, float ay, float noH, float xoH, float yoH)
{
    float a2 = max(ax * ay, BURT_EPSILON);
    float3 v = float3(ay * xoH, ax * yoH, a2 * noH);
    float s = max(dot(v, v), BURT_GGX_DISTRIBUTION_DENOMINATOR_EPSILON);
    float a2OverS = a2 / s;
    return BURT_INV_PI * a2 * a2OverS * a2OverS;
}

float Vis_SmithJointAnisotropic(float ax, float ay, float noV, float noL, float xoV, float xoL, float yoV, float yoL)
{
    float visibilityV = noL * length(float3(ax * xoV, ay * yoV, noV));
    float visibilityL = noV * length(float3(ax * xoL, ay * yoL, noL));
    return 0.5f * rcp_safe(visibilityV + visibilityL);
}

void GetAnisotropicRoughness(float linearRoughness, float anisotropy, out float ax, out float ay)
{
    float safeRoughness = max(linearRoughness, 0.001f);
    float safeAnisotropy = clamp(anisotropy, -0.99f, 0.99f);
    ax = max(safeRoughness * (1.0f + safeAnisotropy), 0.001f);
    ay = max(safeRoughness * (1.0f - safeAnisotropy), 0.001f);
}

float3 BurtGetAnisotropicIBLNormalWS(BurtPBRGeometryData geometryData, float anisotropy, float perceptualRoughness)
{
    float safeAnisotropy = clamp(anisotropy, -1.0f, 1.0f);
    float3 grainDirectionWS = safeAnisotropy >= 0.0f ? geometryData.BitangentWS : geometryData.TangentWS;
    float3 viewFacingTangentWS = cross(grainDirectionWS, geometryData.ViewDirectionWS);
    float3 anisotropicNormalCandidateWS = cross(viewFacingTangentWS, grainDirectionWS);
    float anisotropicNormalLengthSquared = dot(anisotropicNormalCandidateWS, anisotropicNormalCandidateWS);
    float3 anisotropicNormalWS = anisotropicNormalLengthSquared > 0.0001f ? anisotropicNormalCandidateWS * rsqrt(anisotropicNormalLengthSquared) : geometryData.NormalWS;
    float stretch = abs(safeAnisotropy) * saturate(5.0f * perceptualRoughness);
    return BurtSafeNormalize(lerp(geometryData.NormalWS, anisotropicNormalWS, stretch));
}

float3 BurtGetSpecularDominantDir(float3 normalWS, float3 reflectionDirectionWS, float linearRoughness, float nDotV)
{
    float safeLinearRoughness = saturate(linearRoughness);
    float dominantBlend = (1.0f - safeLinearRoughness) * (sqrt(saturate(1.0f - safeLinearRoughness)) + safeLinearRoughness);
    return BurtSafeNormalize(lerp(normalWS, reflectionDirectionWS, dominantBlend));
}

float3 BurtRoughnessShiftSpecularDominantDir(float3 dominantReflectionDirectionWS, float3 reflectionDirectionWS, float linearRoughness)
{
    float a2 = LinearRoughnessToA2(linearRoughness);
    return BurtSafeNormalize(lerp(dominantReflectionDirectionWS, reflectionDirectionWS, a2));
}

float3 BurtGetIndirectSpecularReflectionDirectionWS(BurtPBRGeometryData geometryData, float anisotropy, float perceptualRoughness)
{
    float3 iblNormalWS = BurtGetAnisotropicIBLNormalWS(geometryData, anisotropy, perceptualRoughness);
    float3 reflectionDirectionWS = BurtSafeNormalize(reflect(-geometryData.ViewDirectionWS, iblNormalWS));
    float linearRoughness = PerceptualRoughnessToLinearRoughness(perceptualRoughness);
    float3 dominantReflectionDirectionWS = BurtGetSpecularDominantDir(geometryData.NormalWS, reflectionDirectionWS, linearRoughness, geometryData.NDotV);
    return BurtRoughnessShiftSpecularDominantDir(dominantReflectionDirectionWS, reflectionDirectionWS, linearRoughness);
}

float Vis_SmithJointApprox(float linearRoughness, float noV, float noL)
{
    // 计算视线侧遮蔽对光照方向的影响
float visibilityV = noL * (noV * (1.0f - linearRoughness) + linearRoughness);

    // 计算光照侧遮蔽对视线方向的影响，和上面一项组�?joint visibility
float visibilityL = noV * (noL * (1.0f - linearRoughness) + linearRoughness);

    // �?0.5 后再除以两项之和；rcp_safe 负责保护极小分母
return 0.5f * rcp_safe(visibilityV + visibilityL);
}

// 出处：XRender/Shaders/Library/BRDF.hlsl::PrefilteredDFG_Approx；Lazarov 2013 的环�?BRDF 拟合
float2 PrefilteredDFG_Approx(float roughness, float noV)
{
    // c0 是经验拟合系数，XRender �?PrefilteredDFG_Approx 也使用这组值�?    const
float4 c0 = float4(-1.0f, -0.0275f, -0.572f, 0.022f);

    // c1 是经验拟合系数，用来�?roughness 映射�?DFG �?AB 两项�?    const
float4 c1 = float4(1.0f, 0.0425f, 1.04f, -0.04f);

    // 根据 roughness 插值得到中间拟合参数
float4 r = roughness * c0 + c1;

    // a004 负责拟合视角相关的能量变化，NoV 越小掠射角效果越强
float a004 = min(r.x * r.x, exp2(-9.28f * noV)) * r.x + r.y;

    // AB 分别对应 F0 �?F90 的权重，后面会组合成环境 BRDF
return float2(-1.04f, 1.04f) * a004 + r.zw;
}

// 出处：XRender/Shaders/Library/CommonTransform.hlsl::Remap01CoordToHalfTexelCoord；把 0..1 坐标移到�?texel 中心
float2 Remap01CoordToHalfTexelCoord(float2 coord, float2 invSize)
{
    // 保证 128x128 LUT 的首尾采样落�?texel 中心，避免采到边缘外推值
return coord * (1.0f - invSize) + 0.5f * invSize;
}

// 出处：XRender/Shaders/Library/CommonMaterial.hlsl::GetPreIntegratedFG；采样预积分 FG LUT
float3 GetPreIntegratedFG(float clampedNdotV, float perceptualRoughness)
{
    // XRender 使用 float2(NdotV, 1 - PerceptualRoughness) 作为 LUT 坐标
float2 uv = float2(saturate(clampedNdotV), 1.0f - saturate(perceptualRoughness));

    // 对齐 XRender 的半 texel 重映射，避免 LUT 边界采样误差
uv = Remap01CoordToHalfTexelCoord(uv, float2(BURT_PREINTEGRATED_FG_LUT_INV_SIZE, BURT_PREINTEGRATED_FG_LUT_INV_SIZE));

    // RGB 分别保存 DFG.x、DFG.y 和能量补偿使用的单次散射能量 Z
return BURT_SAMPLE_TEXTURE2D_LOD_CLAMP(_BurtPreIntegratedFG, uv, 0.0f).rgb;
}

float3 SanitizePreIntegratedFG(float3 fg, float3 fallbackFG)
{
    float finite = all(fg == fg) ? 1.0f : 0.0f;
    float inRange = step(0.0f, min(min(fg.x, fg.y), fg.z)) * step(max(max(fg.x, fg.y), fg.z), 8.0f);
    float hasEnergy = step(0.0001f, dot(fg, float3(1.0f, 1.0f, 1.0f)));
    return lerp(fallbackFG, fg, finite * inRange * hasEnergy);
}

// BurtRP LUT 适配函数：读取预积分 FG；没有绑�?LUT 时回退到解�?DFG，保证材质仍可渲染
float3 GetPreIntegratedFGOrApprox(float perceptualRoughness, float clampedNdotV)
{
    // 解析近似只提�?DFG.xy，因此用 DFG.x + DFG.y 近似能量补偿项，作为�?LUT 的安�?fallback
float2 approxDFG = PrefilteredDFG_Approx(perceptualRoughness, clampedNdotV);
    float approxEnergy = clamp(approxDFG.x + approxDFG.y, 0.001f, 1.0f);
    float3 approxFG = float3(approxDFG, approxEnergy);

    // �?C# 确认 LUT 已绑定时使用贴图结果，否则完全使用解析近似
float3 lutFG = SanitizePreIntegratedFG(GetPreIntegratedFG(clampedNdotV, perceptualRoughness), approxFG);
    return lerp(approxFG, lutFG, saturate(_BurtPreIntegratedFGEnabled));
}

// BurtRP 适配函数：返回环�?BRDF 使用�?DFG.xy；优先使�?PreintegratedFG.exr，回退到解析近似
float2 GetSpecularDFGTerms(float perceptualRoughness, float clampedNdotV)
{
    // LUT �?xy 对应 XRender 注释里的 ibl brdf 两项，后续会分别�?F0 �?F90
return GetPreIntegratedFGOrApprox(perceptualRoughness, clampedNdotV).xy;
}

// 出处：XRender/Shaders/Library/BRDF.hlsl::EvalSpecularDFG；把 DFG �?AB 两项应用�?F0/F90 上
float3 EvalSpecularDFG(float3 f0, float3 f90, float2 dfg)
{
    // XRender 公式：F0 * DFG.x + F90 * DFG.y
return f0 * dfg.x + f90 * dfg.y;
}

// 出处：XRender/Shaders/Library/CommonMaterial.hlsl::ComputeEnergyCompensation；用 LUT.z 的单次散射能量补多次散射
float3 ComputeEnergyCompensation(float3 f0, float z)
{
    // XRender 公式�? + F0 * (1 / Z - 1)，Z 越低说明单次散射漏能越多，需要越多补偿
return 1.0f + f0 * (rcp_safe(max(z, 0.001f)) - 1.0f);
}

// 出处：XRender/Shaders/Library/CommonMaterial.hlsl::ComputeEnergyPreservation；UE5 ShadingEnergyConservationTemplate 用预积分能量估算反射层占用的能量
float ComputeEnergyPreservation(float3 f0, float3 f90, float3 energy, float3 w)
{
    // Energy.z �?F0 单次散射能量，Energy.y �?F90-F0 权重；w 是多次散射能量补偿
float3 reflectedEnergy = w * (energy.z * f0 + energy.y * (f90 - f0));

    // XRender 用感知亮度把 RGB 反射能量压成单通道，作为底�?diffuse 可保留的比例
return 1.0f - PerceivedLuminance(reflectedEnergy);
}

// 出处：XRender/Shaders/Library/CommonMaterial.hlsl::CalculateEnergyTerm；BurtRP 暂时常开 EnergyCompensation �?EnergyPreservation
void CalculateEnergyTerm(float3 f0, float3 f90, float3 energy, out float3 energyCompensation, out float energyPreservation)
{
    // LUT.z 作为除数时需要极小值保护，避免贴图未绑定或异常 texel 产生过大补偿�?    energy.z = clamp(energy.z, 0.001f, 1.0f);

    // 先补高光多次散射能量，后�?preservation 要用补偿后的反射能量估算底层透过率
energyCompensation = ComputeEnergyCompensation(f0, energy.z);

    // preservation 是给底层 diffuse �?one-minus-reflectance，最终限制到 0..1 防止调试 LUT 过冲
energyPreservation = saturate(ComputeEnergyPreservation(f0, f90, energy, energyCompensation));
}

// BurtRP 适配函数：从预积�?FG 取出 XRender Energy.xyz，并一次性算出补偿和保能项，方便 Forward/Deferred 共用
void GetSpecularEnergyTerms(float3 f0, float3 f90, float perceptualRoughness, float clampedNdotV, out float3 energyCompensation, out float energyPreservation)
{
    // PreIntegratedFG.rgb 对应 XRender �?Energy.xyz；没�?LUT 时由解析近似提供保守 fallback
float3 energy = GetPreIntegratedFGOrApprox(perceptualRoughness, clampedNdotV);

    CalculateEnergyTerm(f0, f90, energy, energyCompensation, energyPreservation);
}

// BurtRP 适配函数：从预积�?FG 中取 Energy.z，再调用 XRender 原名 ComputeEnergyCompensation
float3 GetSpecularEnergyCompensation(float3 f0, float perceptualRoughness, float clampedNdotV)
{
    // 复用统一 energy terms，避免补偿和保能调试读取到两套不同的 LUT 数据
float3 energyCompensation;
    float energyPreservation;
    GetSpecularEnergyTerms(f0, ApproximateF90(f0), perceptualRoughness, clampedNdotV, energyCompensation, energyPreservation);

    // 只返回高光多次散射补偿，保持旧调用点的语义不变
return energyCompensation;
}

// BurtRP 适配函数：返�?XRender EnergyPreservation，也就是 SlabOperator_Layering 给底�?diffuse �?one-minus-reflectance
float GetSpecularEnergyPreservation(float3 f0, float3 f90, float perceptualRoughness, float clampedNdotV)
{
    // 复用统一 energy terms，确�?Debug View 看到�?preservation 和真�?diffuse 缩放一致
float3 energyCompensation;
    float energyPreservation;
    GetSpecularEnergyTerms(f0, f90, perceptualRoughness, clampedNdotV, energyCompensation, energyPreservation);

    // 返回单通道底层透过率，1 表示不压�?diffuse�? 表示 specular 顶层占满能量
return energyPreservation;
}

// 保存 PBR 能量项，集中管理 XRender EnergyCompensation_GGX �?EnergyPreservation，方�?Deferred 一次性准备
struct BurtPBREnergyTerms
{
    // 保存直接高光能量补偿；使�?Specular AA 后的 roughness，保持直接高光和已调准结果一致
float3 DirectSpecularEnergyCompensation;

    // 保存间接高光能量补偿；使用材�?Base.Roughness，对�?XRender Sky/EnvProbe Specular
float3 IndirectSpecularEnergyCompensation;

    // 保存底层 diffuse 保能比例；使用材�?Base.Roughness，对�?XRender GenericData.EnergyPreservation
float EnergyPreservation;
};

// Prepares shared PBR energy terms for direct and indirect lighting.
BurtPBREnergyTerms BurtPreparePBREnergyTerms(BurtPBRMaterialData materialData, BurtPBRGeometryData geometryData, float directSpecularPerceptualRoughness)
{
    BurtPBREnergyTerms energyTerms;

    // 直接高光补偿继续使用 AA �?roughness，避免把之前校准好的 direct specular 峰值改掉
float unusedDirectEnergyPreservation;
    GetSpecularEnergyTerms(materialData.F0, materialData.F90, directSpecularPerceptualRoughness, geometryData.NDotV, energyTerms.DirectSpecularEnergyCompensation, unusedDirectEnergyPreservation);

    GetSpecularEnergyTerms(materialData.F0, materialData.F90, materialData.PerceptualRoughness, geometryData.NDotV, energyTerms.IndirectSpecularEnergyCompensation, energyTerms.EnergyPreservation);

    if (materialData.FabricActive > 0.0001f)
    {
        energyTerms.DirectSpecularEnergyCompensation = float3(1.0f, 1.0f, 1.0f);
        energyTerms.IndirectSpecularEnergyCompensation = float3(1.0f, 1.0f, 1.0f);
        energyTerms.EnergyPreservation = 1.0f;
    }

    // 返回集中准备好的能量项，后续光照函数只消费结构体，不再重复采�?FG LUT
return energyTerms;
}

// 出处：XRender/Shaders/Library/CommonMaterial.hlsl::GetSpecularOcclusionFromAmbientOcclusion；HDRP/Frostbite AO 高光遮蔽
float GetSpecularOcclusionFromAmbientOcclusion(float noV, float ao, float linearRoughness)
{
    // 粗糙度越高，AO 对高光遮蔽越平滑
float exponent = exp2(-16.0f * saturate(linearRoughness) - 1.0f);
    return saturate(pow(max(saturate(noV) + saturate(ao), BURT_EPSILON), exponent) - 1.0f + saturate(ao));
}

// 出处：XRender/Shaders/Library/CommonMaterial.hlsl::GetSpecularOcclusion；UE ReflectionEnvironmentShared.ush 风格的间接高光遮蔽
float GetSpecularOcclusion(float noV, float ao, float linearRoughness)
{
    // XRender 当前 GenericData.Setup �?SpecularAO 分支里使用这�?UE 近似，roughness 越大 AO 影响越平滑
return saturate(pow(max(saturate(noV) + saturate(ao), BURT_EPSILON), abs(linearRoughness)) - 1.0f + saturate(ao));
}

// BurtRP 适配函数：间接高光遮蔽输入使用感知粗糙度，内部转 XRender �?LinearRoughness
float GetIndirectSpecularOcclusion(float noV, float ao, float perceptualRoughness)
{
    // 对齐 XRender GenericData.Setup.hlsl 当前启用�?UE Approximate，而不�?HDRP/Frostbite 版本
return GetSpecularOcclusion(noV, ao, PerceptualRoughnessToLinearRoughness(perceptualRoughness));
}

// 保存直接�?BRDF 的中间项，方�?Debug View 拆开查看 D / V / F / diffuse lobe
float3 BurtGTAOMultiBounce(float visibility, float3 albedo)
{
    float ao = saturate(visibility);
    float3 safeAlbedo = saturate(albedo);
    float3 a = 2.0404f * safeAlbedo - 0.3324f;
    float3 b = -4.7951f * safeAlbedo + 0.6417f;
    float3 c = 2.7552f * safeAlbedo + 0.6903f;
    return max(ao.xxx, ((ao.xxx * a + b) * ao.xxx + c) * ao.xxx);
}

struct BurtDirectBRDFTerms
{
    // 保存 NdotL，控制直接光受光角度
float NDotL;

    // 保存 NdotV，控�?Fresnel、Smith 可见性和能量项
float NDotV;

    // 保存 NdotH，控�?GGX D 项峰值位置
float NDotH;

    // 保存 VdotH，作�?Schlick Fresnel 输入
float VDotH;

    // 保存直接高光实际使用的感知粗糙度，也就是包含 Specular AA �?roughness
float PerceptualRoughness;

    // 保存直接高光使用的线性粗糙度
float LinearRoughness;

    // 保存直接高光使用�?a^2
float A2;

    // 保存 GGX NDF D 项
float D;

    // 保存 Smith Joint Visibility 项，对应 G / (4NoVNoL)
float Visibility;

    // 保存 Schlick Fresnel 项
float3 Fresnel;

    // 保存 diffuse lobe，默�?Lambert，可�?Burley
float DiffuseLobe;

    // 保存未乘灯光颜色、NdotL 和阴影的 diffuse BRDF
float3 DiffuseBRDF;

    // 保存未乘灯光颜色、NdotL 和阴影的 specular BRDF，已经包�?energy compensation
float3 SpecularBRDF;
};

// Evaluates direct BRDF terms before light color, NdotL, and shadow are applied.
BurtDirectBRDFTerms BurtEvaluateDirectBRDFTerms(
    BurtPBRMaterialData materialData,
    BurtPBRGeometryData geometryData,
    BurtPBREnergyTerms energyTerms,
    float directSpecularPerceptualRoughness,
    float3 lightDirectionWS)
{
    BurtDirectBRDFTerms terms;

    // 复用准备阶段已经安全归一化的法线和视线
float3 n = geometryData.NormalWS;
    float3 v = geometryData.ViewDirectionWS;

    // 归一化灯光方向，当前约定它是从表面指向光源
float3 l = BurtSafeNormalize(lightDirectionWS);

    // 半角向量用于 Fresnel、D 项和高光形状
float3 h = BurtSafeNormalize(l + v);

    terms.NDotL = saturate(dot(n, l));
    terms.NDotV = geometryData.NDotV;
    terms.NDotH = saturate(dot(n, h));
    terms.VDotH = saturate(dot(v, h));

    terms.PerceptualRoughness = directSpecularPerceptualRoughness;
    terms.LinearRoughness = PerceptualRoughnessToLinearRoughness(terms.PerceptualRoughness);
    terms.A2 = LinearRoughnessToA2(terms.LinearRoughness);

    // XRender 的直接高�?lobe 拆项：D / V / F
float xoH = dot(geometryData.TangentWS, h);
    float yoH = dot(geometryData.BitangentWS, h);
    float xoV = dot(geometryData.TangentWS, v);
    float yoV = dot(geometryData.BitangentWS, v);
    float xoL = dot(geometryData.TangentWS, l);
    float yoL = dot(geometryData.BitangentWS, l);
    float ax;
    float ay;
    GetAnisotropicRoughness(terms.LinearRoughness, materialData.Anisotropy, ax, ay);
    terms.D = D_GGX_Anisotropic(ax, ay, terms.NDotH, xoH, yoH);
    terms.Visibility = Vis_SmithJointAnisotropic(ax, ay, terms.NDotV, terms.NDotL, xoV, xoL, yoV, yoL);
    terms.Fresnel = materialData.FabricActive > 0.5f && materialData.FabricIsSilk > 0.5f
        ? F_Schlick_UE(materialData.FabricFuzzColor, terms.VDotH)
        : F_Schlick_UE(materialData.F0, materialData.F90, terms.VDotH);

    terms.DiffuseLobe = SlabLobe_Diffuse(materialData, terms.NDotV, terms.NDotL, terms.VDotH);
    terms.DiffuseBRDF = materialData.DiffuseColor * terms.DiffuseLobe * energyTerms.EnergyPreservation;
    terms.SpecularBRDF = terms.D * terms.Visibility * terms.Fresnel * energyTerms.DirectSpecularEnergyCompensation;
    return terms;
}
// 保存直接 PBR 光照拆分结果，方便正常渲染和 Debug View 共用同一�?BRDF 计算�?
struct BurtDirectPBRComponents
{
    // 保存直接漫反射最终贡献，已经包含灯光颜色、NdotL 和阴影衰减
float3 Diffuse;

    // 保存直接镜面高光最终贡献，已经包含灯光颜色、NdotL 和阴影衰减
float3 Specular;

float3 Transmission;

    // 保存 XRender EnergyPreservation，表�?specular 顶层之后底层 diffuse 还能保留的能量比例
float EnergyPreservation;

float3 TransmissionBRDF;
float3 TransmissionThroughput;
float TransmissionLobe;
float TransmissionPhase;
float TransmissionShadow;
float TransmissionThickness;

    // 保存直接 BRDF 中间项，Debug View 可以拆开查看 D / V / F �?diffuse/specular BRDF�?
    BurtDirectBRDFTerms BrdfTerms;
};

float3 BurtEvaluateSubsurfaceTransmissionBRDFColor(
    BurtPBRMaterialData materialData,
    float3 transmissionThroughput,
    float transmissionPhase)
{
#if defined(BURT_SUBSURFACE_DEFERRED_POSTPROCESS_INPUT)
    if (BurtIsSubsurface3SPreIntegratedMode(materialData.SubsurfaceScatteringMode))
    {
        return max(materialData.BaseColor * transmissionThroughput * transmissionPhase, float3(0.0f, 0.0f, 0.0f));
    }

    return max(transmissionThroughput * transmissionPhase, float3(0.0f, 0.0f, 0.0f));
#else
    return max(materialData.BaseColor * transmissionThroughput * transmissionPhase, float3(0.0f, 0.0f, 0.0f));
#endif
}

float BurtEvaluateSubsurfaceTransmissionWrapLobe(
    BurtPBRGeometryData geometryData,
    float3 lightDirectionWS,
    float scatteringDistribution)
{
    float3 n = geometryData.NormalWS;
    float3 v = geometryData.ViewDirectionWS;
    float3 l = BurtSafeNormalize(lightDirectionWS);
    float nDotL = dot(n, l);
    float opacity = saturate(1.0f - abs(scatteringDistribution));
    float inScatter = pow(saturate(dot(l, -v)), 12.0f) * lerp(3.0f, 0.1f, opacity);
    float wrappedDiffuse = pow(saturate(nDotL * (1.0f / 1.5f) + (0.5f / 1.5f)), 1.5f) * (2.5f / 1.5f);
    float normalContribution = lerp(1.0f, wrappedDiffuse, opacity);
    float backScatter = normalContribution * (0.5f * BURT_INV_PI);
    return max(lerp(backScatter, 1.0f, inScatter), 0.0f);
}

#if BURT_ENABLE_SUBSURFACE_SHADING
float3 BurtEvaluateSubsurfaceProfileTransmissionBRDF(
    BurtPBRMaterialData materialData,
    BurtPBRGeometryData geometryData,
    float3 lightDirectionWS)
{
    float4 profileTransmission = BurtLoadSubsurfaceProfileTransmission(materialData.SubsurfaceProfileIndex);
    float3 transmissionThroughput = BurtSampleSubsurfaceTransmissionThroughput(
        materialData.SubsurfaceProfileIndex,
        materialData.SubsurfaceThickness);
    float oneOverIOR = clamp(profileTransmission.w, 0.01f, 1.0f);
    float scatteringDistribution = BurtDecodeSubsurfaceProfileScatteringDistribution(profileTransmission.z);
    float3 refractedView = refract(geometryData.ViewDirectionWS, -geometryData.NormalWS, oneOverIOR);
    refractedView = dot(refractedView, refractedView) > BURT_EPSILON
        ? BurtSafeNormalize(refractedView)
        : BurtSafeNormalize(geometryData.ViewDirectionWS);
    float phase = BurtHenyeyGreensteinPhase(scatteringDistribution, dot(BurtSafeNormalize(lightDirectionWS), refractedView));
    return BurtEvaluateSubsurfaceTransmissionBRDFColor(materialData, transmissionThroughput, phase);
}

void BurtEvaluateSubsurfaceProfileTransmissionTerms(
    BurtPBRMaterialData materialData,
    BurtPBRGeometryData geometryData,
    float3 lightDirectionWS,
    float resolvedTransmissionThickness,
    float transmissionShadowAttenuation,
    out float3 transmissionBRDF,
    out float3 transmissionThroughput,
    out float transmissionLobe,
    out float transmissionPhase,
    out float transmissionThickness)
{
    float4 profileTransmission = BurtLoadSubsurfaceProfileTransmission(materialData.SubsurfaceProfileIndex);
    float nDotL = dot(geometryData.NormalWS, BurtSafeNormalize(lightDirectionWS));
    bool hasResolvedTransmissionThickness = BurtHasResolvedSubsurfaceTransmissionThickness(resolvedTransmissionThickness);
    transmissionThickness = hasResolvedTransmissionThickness
        ? BurtResolveSubsurfaceTransmissionLightingThickness(materialData.SubsurfaceThickness, resolvedTransmissionThickness, nDotL)
        : BurtResolveSubsurfaceProfileThickness(materialData.SubsurfaceThickness);
    transmissionThroughput = BurtSampleSubsurfaceTransmissionThroughputByThickness(
        materialData.SubsurfaceProfileIndex,
        transmissionThickness);
    float oneOverIOR = clamp(profileTransmission.w, 0.01f, 1.0f);
    float scatteringDistribution = BurtDecodeSubsurfaceProfileScatteringDistribution(profileTransmission.z);
    if (hasResolvedTransmissionThickness)
    {
        float3 refractedView = refract(geometryData.ViewDirectionWS, -geometryData.NormalWS, oneOverIOR);
        refractedView = dot(refractedView, refractedView) > BURT_EPSILON
            ? BurtSafeNormalize(refractedView)
            : BurtSafeNormalize(geometryData.ViewDirectionWS);
        transmissionPhase = BurtHenyeyGreensteinPhase(scatteringDistribution, dot(BurtSafeNormalize(lightDirectionWS), refractedView));
    }
    else
    {
        transmissionPhase = BurtEvaluateSubsurfaceTransmissionWrapLobe(geometryData, lightDirectionWS, scatteringDistribution);
    }

    transmissionBRDF = BurtEvaluateSubsurfaceTransmissionBRDFColor(materialData, transmissionThroughput, transmissionPhase);
    transmissionLobe = BurtEvaluateSubsurfaceProfileIntensity(transmissionBRDF);
}

void BurtEvaluateSubsurfaceProfileTransmissionTerms(
    BurtPBRMaterialData materialData,
    BurtPBRGeometryData geometryData,
    float3 lightDirectionWS,
    out float3 transmissionBRDF,
    out float3 transmissionThroughput,
    out float transmissionLobe,
    out float transmissionPhase,
    out float transmissionThickness)
{
    BurtEvaluateSubsurfaceProfileTransmissionTerms(
        materialData,
        geometryData,
        lightDirectionWS,
        -1.0f,
        1.0f,
        transmissionBRDF,
        transmissionThroughput,
        transmissionLobe,
        transmissionPhase,
        transmissionThickness);
}

void BurtApplySubsurfaceDirectPBR(
    inout BurtDirectPBRComponents components,
    BurtPBRMaterialData materialData,
    BurtPBRGeometryData geometryData,
    float3 lightColor,
    float3 lightDirectionWS,
    float shadowAttenuation,
    float transmissionShadowAttenuation,
    float resolvedTransmissionThickness)
{
    float subsurfaceStrength = BurtGetSubsurfaceMaterialWeight(materialData);
    if (subsurfaceStrength <= 0.0001f)
    {
        return;
    }

    float3 n = geometryData.NormalWS;
    float3 v = geometryData.ViewDirectionWS;
    float3 l = BurtSafeNormalize(lightDirectionWS);
    float rawNoL = dot(n, l);
    float wrappedNoL = saturate((rawNoL + materialData.SubsurfaceDistortion) / (1.0f + materialData.SubsurfaceDistortion));
    float3 h = BurtSafeNormalize(l + v);
    float vDotH = saturate(dot(v, h));
    float wrappedDiffuseLobe = SlabLobe_Diffuse(materialData, geometryData.NDotV, wrappedNoL, vDotH);

    float curvature = saturate(materialData.Subsurface3SCurvature);
    float3 profileTransmissionBRDF;
    float3 profileTransmissionThroughput;
    float profileTransmissionLobe;
    float profileTransmissionPhase;
    float profileTransmissionThickness;
    BurtEvaluateSubsurfaceProfileTransmissionTerms(
        materialData,
        geometryData,
        l,
        resolvedTransmissionThickness,
        transmissionShadowAttenuation,
        profileTransmissionBRDF,
        profileTransmissionThroughput,
        profileTransmissionLobe,
        profileTransmissionPhase,
        profileTransmissionThickness);
    float transmissionShadow = saturate(transmissionShadowAttenuation);
    float transmissionVisibility = BurtHasResolvedSubsurfaceTransmissionThickness(resolvedTransmissionThickness) ? transmissionShadow : 1.0f;
    float3 profileTransmission = profileTransmissionBRDF * lightColor * transmissionVisibility;
    components.Transmission = lerp(components.Transmission, profileTransmission, subsurfaceStrength);
    components.TransmissionBRDF = lerp(components.TransmissionBRDF, profileTransmissionBRDF, subsurfaceStrength);
    components.TransmissionThroughput = lerp(components.TransmissionThroughput, profileTransmissionThroughput, subsurfaceStrength);
    components.TransmissionLobe = lerp(components.TransmissionLobe, profileTransmissionLobe, subsurfaceStrength);
    components.TransmissionPhase = lerp(components.TransmissionPhase, profileTransmissionPhase, subsurfaceStrength);
    components.TransmissionShadow = lerp(components.TransmissionShadow, transmissionShadow, subsurfaceStrength);
    components.TransmissionThickness = lerp(components.TransmissionThickness, profileTransmissionThickness, subsurfaceStrength);
    if (BurtIsSubsurface3SPreIntegratedMode(materialData.SubsurfaceScatteringMode))
    {
        float3 lutScatter = BurtSampleSubsurfacePreIntegratedLut(rawNoL, curvature, materialData.SubsurfaceProfileIndex);
        float lutWeight = saturate(_BurtSubsurfacePreIntegratedLutEnabled);
        float3 preintegratedDiffuseBRDF = materialData.DiffuseColor * lutScatter * BURT_INV_PI;
        float3 preintegratedDiffuse = preintegratedDiffuseBRDF * lightColor * shadowAttenuation;
        float targetDiffuseLobe = BurtEvaluateSubsurfaceProfileIntensity(lutScatter) * BURT_INV_PI;
        float3 resolvedDiffuse = lerp(components.Diffuse, preintegratedDiffuse, lutWeight);
        float3 resolvedDiffuseBRDF = lerp(components.BrdfTerms.DiffuseBRDF, preintegratedDiffuseBRDF, lutWeight);
        float resolvedDiffuseLobe = lerp(components.BrdfTerms.DiffuseLobe, targetDiffuseLobe, lutWeight);

        components.Diffuse = lerp(components.Diffuse, resolvedDiffuse, subsurfaceStrength);
        components.BrdfTerms.DiffuseLobe = lerp(components.BrdfTerms.DiffuseLobe, resolvedDiffuseLobe, subsurfaceStrength);
        components.BrdfTerms.DiffuseBRDF = lerp(components.BrdfTerms.DiffuseBRDF, resolvedDiffuseBRDF, subsurfaceStrength);
        return;
    }

#if defined(BURT_SUBSURFACE_DEFERRED_POSTPROCESS_INPUT)
    float3 diffuseTint = float3(1.0f, 1.0f, 1.0f);
#else
    float3 diffuseTint = max(BurtLoadSubsurfaceProfileTransmissionTint(materialData.SubsurfaceProfileIndex).rgb, float3(0.0f, 0.0f, 0.0f));
#endif
    float4 dualSpecular = BurtLoadSubsurfaceProfileDualSpecular(materialData.SubsurfaceProfileIndex);
    float lobeMix = saturate(dualSpecular.z);
    float lobe0Roughness = ClampPerceptualRoughness(materialData.PerceptualRoughness * max(dualSpecular.x * BURT_SUBSURFACE_MAX_DUAL_SPECULAR_ROUGHNESS, 0.01f));
    float lobe1Roughness = ClampPerceptualRoughness(materialData.PerceptualRoughness * max(dualSpecular.y * BURT_SUBSURFACE_MAX_DUAL_SPECULAR_ROUGHNESS, 0.01f));
    float averageRoughness = ClampPerceptualRoughness(lerp(lobe0Roughness, lobe1Roughness, lobeMix));
    float averageLinearRoughness = PerceptualRoughnessToLinearRoughness(averageRoughness);
    float3 dualEnergyCompensation;
    float dualEnergyPreservation;
    GetSpecularEnergyTerms(materialData.F0, materialData.F90, averageRoughness, components.BrdfTerms.NDotV, dualEnergyCompensation, dualEnergyPreservation);
    float3 lutScatter = BurtSampleSubsurfacePreIntegratedLut(rawNoL, curvature, materialData.SubsurfaceProfileIndex);
    float lutWeight = saturate(_BurtSubsurfacePreIntegratedLutEnabled);
    float scatterIntensity = BurtEvaluateSubsurfaceProfileIntensity(lutScatter);
    float3 wrappedDiffuseBRDF = materialData.DiffuseColor * diffuseTint * lerp(wrappedDiffuseLobe, scatterIntensity * BURT_INV_PI, lutWeight) * dualEnergyPreservation;
    float3 wrappedDiffuse = wrappedDiffuseBRDF * lightColor * lerp(wrappedNoL * shadowAttenuation, shadowAttenuation, lutWeight);

    profileTransmissionBRDF *= dualEnergyPreservation;
    profileTransmission *= dualEnergyPreservation;
    components.Transmission *= dualEnergyPreservation;
    components.TransmissionBRDF *= dualEnergyPreservation;
    profileTransmissionLobe *= dualEnergyPreservation;
    components.TransmissionLobe *= dualEnergyPreservation;

    float lobe0A2 = LinearRoughnessToA2(PerceptualRoughnessToLinearRoughness(lobe0Roughness));
    float lobe1A2 = LinearRoughnessToA2(PerceptualRoughnessToLinearRoughness(lobe1Roughness));
    float lobe0D = D_GGX(lobe0A2, components.BrdfTerms.NDotH);
    float lobe1D = D_GGX(lobe1A2, components.BrdfTerms.NDotH);
    float dualD = lerp(lobe0D, lobe1D, lobeMix);
    float dualVisibility = Vis_SmithJointApprox(averageLinearRoughness, components.BrdfTerms.NDotV, components.BrdfTerms.NDotL);
    float3 dualSpecularBRDF = dualD * dualVisibility * components.BrdfTerms.Fresnel * dualEnergyCompensation;
    float lightVisibility = components.BrdfTerms.NDotL * shadowAttenuation;
    float3 dualSpecularContribution = dualSpecularBRDF * lightColor * lightVisibility;

    float3 targetDiffuse = wrappedDiffuse;
    float targetDiffuseLobe = wrappedDiffuseLobe;
    float3 targetDiffuseBRDF = wrappedDiffuseBRDF;
#if defined(BURT_SUBSURFACE_DEFERRED_POSTPROCESS_INPUT)
    if (!BurtIsSubsurface3SPreIntegratedMode(materialData.SubsurfaceScatteringMode))
    {
        // 5S/4S match XRender's post-process SSS path: direct diffuse stays as
        // ordinary diffuse, while profile diffusion/tint is applied by the
        // screen-space subsurface pass. Profile transmission is a direct-light
        // lobe and stays in the deferred subsurface input.
        targetDiffuse = components.Diffuse;
        targetDiffuseLobe = components.BrdfTerms.DiffuseLobe;
        targetDiffuseBRDF = components.BrdfTerms.DiffuseBRDF;
    }
#endif
    components.Diffuse = lerp(components.Diffuse, targetDiffuse, subsurfaceStrength);
    components.BrdfTerms.DiffuseLobe = lerp(components.BrdfTerms.DiffuseLobe, targetDiffuseLobe, subsurfaceStrength);
    components.BrdfTerms.DiffuseBRDF = lerp(components.BrdfTerms.DiffuseBRDF, targetDiffuseBRDF, subsurfaceStrength);
    components.Specular = lerp(components.Specular, dualSpecularContribution, subsurfaceStrength);
    components.EnergyPreservation = lerp(components.EnergyPreservation, dualEnergyPreservation, subsurfaceStrength);
    components.BrdfTerms.SpecularBRDF = lerp(components.BrdfTerms.SpecularBRDF, dualSpecularBRDF, subsurfaceStrength);
    components.BrdfTerms.PerceptualRoughness = lerp(components.BrdfTerms.PerceptualRoughness, averageRoughness, subsurfaceStrength);
    components.BrdfTerms.LinearRoughness = lerp(components.BrdfTerms.LinearRoughness, averageLinearRoughness, subsurfaceStrength);
    components.BrdfTerms.A2 = lerp(components.BrdfTerms.A2, LinearRoughnessToA2(averageLinearRoughness), subsurfaceStrength);
    components.BrdfTerms.D = lerp(components.BrdfTerms.D, dualD, subsurfaceStrength);
    components.BrdfTerms.Visibility = lerp(components.BrdfTerms.Visibility, dualVisibility, subsurfaceStrength);
}

void BurtApplySubsurfaceDirectPBR(
    inout BurtDirectPBRComponents components,
    BurtPBRMaterialData materialData,
    BurtPBRGeometryData geometryData,
    float3 lightColor,
    float3 lightDirectionWS,
    float shadowAttenuation)
{
    BurtApplySubsurfaceDirectPBR(
        components,
        materialData,
        geometryData,
        lightColor,
        lightDirectionWS,
        shadowAttenuation,
        shadowAttenuation,
        -1.0f);
}
#endif

#if BURT_ENABLE_FABRIC_SHADING
void BurtApplyFabricDirectPBR(
    inout BurtDirectPBRComponents components,
    BurtPBRMaterialData materialData,
    BurtPBRGeometryData geometryData,
    float3 lightColor,
    float3 lightDirectionWS,
    float shadowAttenuation)
{
    if (materialData.FabricActive <= 0.0001f || materialData.FabricIsSilk > 0.5f)
    {
        return;
    }

    float fuzzWeight = saturate(materialData.FabricFuzzWeight);
    if (fuzzWeight <= 0.0001f)
    {
        return;
    }

    float3 n = geometryData.NormalWS;
    float3 v = geometryData.ViewDirectionWS;
    float3 l = BurtSafeNormalize(lightDirectionWS);
    float3 h = BurtSafeNormalize(l + v);
    float noL = saturate(dot(n, l));
    float noV = saturate(geometryData.NDotV);
    float noH = saturate(dot(n, h));
    float loH = saturate(dot(l, h));
    float fuzzRoughness = ClampPerceptualRoughness(materialData.FabricFuzzRoughness);
    float fuzzLinearRoughness = PerceptualRoughnessToLinearRoughness(fuzzRoughness);
    float fuzzD = D_Charlie(fuzzLinearRoughness, noH);
    float fuzzVisibility = V_Neubelt(noV, noL);
    float3 fuzzFresnel = F_Schlick_UE(materialData.FabricFuzzColor, loH);
    float3 fuzzBRDF = fuzzD * fuzzVisibility * fuzzFresnel;
    float3 fuzzSpecular = fuzzBRDF * lightColor * noL * shadowAttenuation;
    float fuzzEnergyPreservation = saturate(1.0f - BurtClothEnergyLookup(fuzzRoughness, noV));
    float baseLayerWeight = lerp(1.0f, fuzzEnergyPreservation, fuzzWeight);

    components.Diffuse *= baseLayerWeight;
    components.Specular = components.Specular * baseLayerWeight + fuzzSpecular * fuzzWeight;
    components.EnergyPreservation = saturate(components.EnergyPreservation * baseLayerWeight);
    components.BrdfTerms.DiffuseBRDF *= baseLayerWeight;
    components.BrdfTerms.SpecularBRDF = components.BrdfTerms.SpecularBRDF * baseLayerWeight + fuzzBRDF * fuzzWeight;
    components.BrdfTerms.PerceptualRoughness = lerp(components.BrdfTerms.PerceptualRoughness, fuzzRoughness, fuzzWeight);
    components.BrdfTerms.LinearRoughness = lerp(components.BrdfTerms.LinearRoughness, fuzzLinearRoughness, fuzzWeight);
    components.BrdfTerms.A2 = lerp(components.BrdfTerms.A2, LinearRoughnessToA2(fuzzLinearRoughness), fuzzWeight);
    components.BrdfTerms.D = lerp(components.BrdfTerms.D, fuzzD, fuzzWeight);
    components.BrdfTerms.Visibility = lerp(components.BrdfTerms.Visibility, fuzzVisibility, fuzzWeight);
    components.BrdfTerms.Fresnel = lerp(components.BrdfTerms.Fresnel, fuzzFresnel, fuzzWeight);
}

void BurtApplySilkWrappedDiffuseDirectPBR(
    inout BurtDirectPBRComponents components,
    BurtPBRMaterialData materialData,
    BurtPBRGeometryData geometryData,
    float3 lightColor,
    float3 lightDirectionWS,
    float shadowAttenuation)
{
    if (materialData.FabricActive <= 0.0001f || materialData.FabricIsSilk <= 0.5f)
    {
        return;
    }

    float3 l = BurtSafeNormalize(lightDirectionWS);
    float rawNoL = dot(geometryData.NormalWS, l);
    float wrappedNoL = BurtComputeWrappedDiffuseLighting(-rawNoL, cos(BURT_PI * (5.0f / 12.0f)));
    float3 wrappedDiffuse = components.BrdfTerms.DiffuseBRDF * lightColor * wrappedNoL * shadowAttenuation;

    components.Diffuse += wrappedDiffuse;
}
#endif

#if BURT_ENABLE_FOLIAGE_SHADING
void BurtApplyFoliageDirectPBR(
    inout BurtDirectPBRComponents components,
    BurtPBRMaterialData materialData,
    BurtPBRGeometryData geometryData,
    float3 lightColor,
    float3 lightDirectionWS,
    float shadowAttenuation,
    float transmissionShadowAttenuation)
{
    if (materialData.FoliageActive <= 0.0001f)
    {
        return;
    }

    float transmissionWeight = materialData.FoliageIsGrass > 0.5f
        ? max(materialData.FoliageTransmissionWeight, 0.0f)
        : saturate(materialData.FoliageTransmissionWeight);
    if (transmissionWeight <= 0.0001f)
    {
        return;
    }

    float3 n = geometryData.NormalWS;
    float3 v = geometryData.ViewDirectionWS;
    float3 l = BurtSafeNormalize(lightDirectionWS);
    float foliageTransmissionShadowAttenuation = saturate(shadowAttenuation);
    if (materialData.FoliageIsGrass > 0.5f)
    {
        float voL = dot(v, l);
        float phase = (-voL) * 0.5f + 0.5f;
        float transLightVoL = saturate(Pow5(phase));
        float average = (materialData.BaseColor.r + materialData.BaseColor.g + materialData.BaseColor.b) * 0.3333f;
        float3 sssColor = saturate((materialData.BaseColor - average.xxx) * 0.35f + materialData.BaseColor);
        float3 sssLight = sssColor * transLightVoL * transmissionWeight * lightColor * foliageTransmissionShadowAttenuation;
        float sssBlend = 0.35f * saturate(transmissionWeight * 10.0f);
        float3 baseDiffuse = components.Diffuse;
        float3 blendedDiffuse = lerp(baseDiffuse, sssLight, sssBlend);
        float3 addedTransmission = max(blendedDiffuse - baseDiffuse, float3(0.0f, 0.0f, 0.0f));

        components.Transmission += addedTransmission;
        components.TransmissionBRDF += sssColor * transLightVoL * transmissionWeight;
        components.TransmissionThroughput = max(components.TransmissionThroughput, sssColor);
        components.TransmissionLobe = max(components.TransmissionLobe, transLightVoL * transmissionWeight);
        components.TransmissionShadow = foliageTransmissionShadowAttenuation;
        components.TransmissionThickness = max(components.TransmissionThickness, saturate(materialData.FoliageThickness));
        components.Diffuse = blendedDiffuse;
        components.BrdfTerms.DiffuseBRDF = lerp(components.BrdfTerms.DiffuseBRDF, sssColor * transLightVoL * transmissionWeight, sssBlend);
        components.BrdfTerms.DiffuseLobe = max(components.BrdfTerms.DiffuseLobe, transLightVoL * transmissionWeight);
        return;
    }

    float voL = dot(v, l);
    float noL = dot(n, l);
    float wrap = saturate(materialData.FoliageTransmissionNdotL);
    float wrapNoL = saturate((-noL + wrap) / max((1.0f + wrap) * (1.0f + wrap), BURT_EPSILON));
    float scatter = D_GGX(0.36f, saturate(-voL));
    float lobe = max(scatter * wrapNoL, 0.0f);
    float3 foliageTransmissionColor = max(materialData.FoliageTransmissionColor, float3(0.0f, 0.0f, 0.0f));
    float3 foliageSlabSubsurfaceColor = BurtEvaluateFoliageSlabSubsurfaceColor(foliageTransmissionColor);
    float3 transmissionBRDF = foliageSlabSubsurfaceColor * transmissionWeight * lobe;
    float3 transmission = transmissionBRDF * lightColor * foliageTransmissionShadowAttenuation;

    components.Transmission += transmission;
    components.TransmissionBRDF += transmissionBRDF;
    components.TransmissionThroughput = max(components.TransmissionThroughput, foliageSlabSubsurfaceColor);
    components.TransmissionLobe = max(components.TransmissionLobe, lobe);
    components.TransmissionShadow = foliageTransmissionShadowAttenuation;
    components.TransmissionThickness = max(components.TransmissionThickness, saturate(materialData.FoliageThickness));
    components.Diffuse += transmission;
    components.BrdfTerms.DiffuseBRDF += transmissionBRDF;
    components.BrdfTerms.DiffuseLobe = max(components.BrdfTerms.DiffuseLobe, lobe * transmissionWeight);
}

void BurtApplyFoliageDirectPBR(
    inout BurtDirectPBRComponents components,
    BurtPBRMaterialData materialData,
    BurtPBRGeometryData geometryData,
    float3 lightColor,
    float3 lightDirectionWS,
    float shadowAttenuation,
    float transmissionShadowAttenuation,
    float resolvedTransmissionThickness)
{
    BurtApplyFoliageDirectPBR(
        components,
        materialData,
        geometryData,
        lightColor,
        lightDirectionWS,
        shadowAttenuation,
        transmissionShadowAttenuation);
}
#endif

// Evaluates a single light from prepared PBR data.
BurtDirectPBRComponents BurtEvaluateDirectPBRComponents(
    BurtPBRMaterialData materialData,
    BurtPBRGeometryData geometryData,
    BurtPBREnergyTerms energyTerms,
    float directSpecularPerceptualRoughness,
    float3 lightColor,
    float3 lightDirectionWS,
    float shadowAttenuation,
    float transmissionShadowAttenuation,
    float resolvedTransmissionThickness)
{
    BurtDirectPBRComponents components;
    components.Diffuse = float3(0.0f, 0.0f, 0.0f);
    components.Specular = float3(0.0f, 0.0f, 0.0f);
    components.Transmission = float3(0.0f, 0.0f, 0.0f);
    components.EnergyPreservation = 1.0f;
    components.TransmissionBRDF = float3(0.0f, 0.0f, 0.0f);
    components.TransmissionThroughput = float3(0.0f, 0.0f, 0.0f);
    components.TransmissionLobe = 0.0f;
    components.TransmissionPhase = 0.0f;
    components.TransmissionShadow = saturate(transmissionShadowAttenuation);
    components.TransmissionThickness = 0.0f;
    components.BrdfTerms = BurtEvaluateDirectBRDFTerms(materialData, geometryData, energyTerms, directSpecularPerceptualRoughness, lightDirectionWS);
    components.EnergyPreservation = energyTerms.EnergyPreservation;

    // 合并灯光可见性；NdotL 控制受光角度，shadowAttenuation 控制阴影
    float lightVisibility = components.BrdfTerms.NDotL * shadowAttenuation;

    components.Diffuse = components.BrdfTerms.DiffuseBRDF * lightColor * lightVisibility;
    components.Specular = components.BrdfTerms.SpecularBRDF * lightColor * lightVisibility;

    #if BURT_ENABLE_FABRIC_SHADING
    BurtApplySilkWrappedDiffuseDirectPBR(components, materialData, geometryData, lightColor, lightDirectionWS, shadowAttenuation);
    #endif
    #if BURT_ENABLE_SUBSURFACE_SHADING
    BurtApplySubsurfaceDirectPBR(components, materialData, geometryData, lightColor, lightDirectionWS, shadowAttenuation, transmissionShadowAttenuation, resolvedTransmissionThickness);
    #endif
    #if BURT_ENABLE_FOLIAGE_SHADING
    BurtApplyFoliageDirectPBR(components, materialData, geometryData, lightColor, lightDirectionWS, shadowAttenuation, transmissionShadowAttenuation, resolvedTransmissionThickness);
    #endif
    #if BURT_ENABLE_FABRIC_SHADING
    BurtApplyFabricDirectPBR(components, materialData, geometryData, lightColor, lightDirectionWS, shadowAttenuation);
    #endif

    // 返回拆分后的直接光结果
    return components;
}

BurtDirectPBRComponents BurtEvaluateDirectPBRComponents(
    BurtPBRMaterialData materialData,
    BurtPBRGeometryData geometryData,
    BurtPBREnergyTerms energyTerms,
    float directSpecularPerceptualRoughness,
    float3 lightColor,
    float3 lightDirectionWS,
    float shadowAttenuation,
    float transmissionShadowAttenuation)
{
    return BurtEvaluateDirectPBRComponents(
        materialData,
        geometryData,
        energyTerms,
        directSpecularPerceptualRoughness,
        lightColor,
        lightDirectionWS,
        shadowAttenuation,
        transmissionShadowAttenuation,
        -1.0f);
}

BurtDirectPBRComponents BurtEvaluateDirectPBRComponents(
    BurtPBRMaterialData materialData,
    BurtPBRGeometryData geometryData,
    BurtPBREnergyTerms energyTerms,
    float directSpecularPerceptualRoughness,
    float3 lightColor,
    float3 lightDirectionWS,
    float shadowAttenuation)
{
    return BurtEvaluateDirectPBRComponents(
        materialData,
        geometryData,
        energyTerms,
        directSpecularPerceptualRoughness,
        lightColor,
        lightDirectionWS,
        shadowAttenuation,
        shadowAttenuation,
        -1.0f);
}

// Evaluates split direct PBR lighting from surface inputs.
BurtDirectPBRComponents BurtEvaluateDirectPBRComponents(
    BurtSurfaceData surfaceData,
    float3 lightColor,
    float3 lightDirectionWS,
    float3 normalWS,
    float3 viewDirectionWS,
    float shadowAttenuation)
{
    BurtPBRMaterialData materialData = BurtPreparePBRMaterialData(surfaceData);

    BurtPBRGeometryData geometryData = BurtPreparePBRGeometryData(normalWS, viewDirectionWS);

    // 直接高光�?roughness 单独包含 Specular AA，不回写材质 Base.Roughness
    float directSpecularPerceptualRoughness = GetDirectSpecularPerceptualRoughness(materialData, geometryData);

    // 一次性准备直�?间接高光补偿�?EnergyPreservation，避免多个光照函数重复查 FG LUT�?
    BurtPBREnergyTerms energyTerms = BurtPreparePBREnergyTerms(materialData, geometryData, directSpecularPerceptualRoughness);
    return BurtEvaluateDirectPBRComponents(materialData, geometryData, energyTerms, directSpecularPerceptualRoughness, lightColor, lightDirectionWS, shadowAttenuation);

    // 复用准备数据版本，保�?Forward 和未�?Deferred 的直接光入口一致
    return BurtEvaluateDirectPBRComponents(materialData, geometryData, energyTerms, directSpecularPerceptualRoughness, lightColor, lightDirectionWS, shadowAttenuation);
}

// 计算单个方向光对当前表面�?PBR 直接光贡献
float3 BurtEvaluateDirectPBR(
    BurtSurfaceData surfaceData,
    float3 lightColor,
    float3 lightDirectionWS,
    float3 normalWS,
    float3 viewDirectionWS,
    float shadowAttenuation)
{
    // 复用拆分版本，确保正常渲染和 Debug View 看到的是同一�?BRDF 结果�?
    BurtDirectPBRComponents components = BurtEvaluateDirectPBRComponents(surfaceData, lightColor, lightDirectionWS, normalWS, viewDirectionWS, shadowAttenuation);

    // 把直接漫反射和直接高光相加，得到旧接口需要的总直接光
    return components.Diffuse + components.Specular;
}

#endif // BURT_BRDF_INCLUDED // 结束 BurtBRDF.hlsl �?include guard�?
