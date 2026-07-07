// BurtRP 的材质输入工具库，负责把贴图、颜色、Mask Map 和 alpha 裁剪整理成统一的表面数据。
#ifndef BURT_INPUT_INCLUDED // 开始 include guard，防止多个 shader library 重复包含时产生重定义。
#define BURT_INPUT_INCLUDED // 标记 BurtInput.hlsl 已经被包含过，后续重复 include 会被跳过。

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtShadingModelIds.hlsl"

// 声明 Base Map 贴图，BurtLit 和其它 Lit 类型 shader 会用它采样基础颜色。
Texture2D _BaseMap;

// 声明 Mask Map 贴图，Forward PBR 会把 R 当金属度、G 当环境遮蔽、A 当光滑度。
Texture2D _MaskMap;
Texture2D _SubsurfaceThicknessMap;
Texture2D _AlphaMap;
Texture2D _TintPalette;
Texture2D _LocalTintPalette;
Texture2D _NoiseMap;
Texture2D _IDMap;
Texture2D _GradientMap;
Texture2D _FuzzMap;
Texture2D _FuzzMask;

#if !defined(SAMPLE_TEXTURE2D_BIAS)
    #define SAMPLE_TEXTURE2D_BIAS(textureName, samplerName, coord2, bias) textureName.SampleBias(samplerName, coord2, bias)
#endif

// 定义 XRender / Frostbite 风格的默认 reflectance，0.5 会映射到常见非金属 F0=0.04。
#define BURT_INPUT_DEFAULT_REFLECTANCE (0.5f)
#define BURT_SUBSURFACE_FIXED_REFLECTANCE (0.42f)

#define BURT_SUBSURFACE_POWER_MIN (0.5f)
#define BURT_SUBSURFACE_POWER_MAX (8.0f)
#define BURT_SUBSURFACE_DEFAULT_THICKNESS (0.5f)
#define BURT_SUBSURFACE_DEFAULT_POWER (3.0f)
#define BURT_SUBSURFACE_DEFAULT_DISTORTION (0.35f)
#define BURT_SUBSURFACE_DEFAULT_AMBIENT (0.35f)
#define BURT_SUBSURFACE_PROFILE_COUNT (8.0f)
#define BURT_SUBSURFACE_DEFAULT_PROFILE_INDEX (0.0f)
#define BURT_SUBSURFACE_SCATTERING_MODE_5S_BURLEY (0.0f)
#define BURT_SUBSURFACE_SCATTERING_MODE_4S_SEPARABLE (1.0f)
#define BURT_SUBSURFACE_SCATTERING_MODE_3S_PREINTEGRATED (2.0f)
#define BURT_SUBSURFACE_DEFAULT_SCATTERING_MODE (BURT_SUBSURFACE_SCATTERING_MODE_5S_BURLEY)
float BurtResolveSurfaceShadingModel(float shadingModelID)
{
    // Keep the value integral before GBuffer packing so material sliders cannot land between lighting branches.
    return clamp(floor(shadingModelID + 0.5f), BURT_SHADING_MODEL_DEFAULT_LIT, BURT_SHADING_MODEL_MAX_ENCODED);
}

bool BurtIsHairShadingModel(float shadingModelID)
{
    return abs(BurtResolveSurfaceShadingModel(shadingModelID) - BURT_SHADING_MODEL_HAIR) < 0.5f;
}

bool BurtIsClearCoatShadingModel(float shadingModelID)
{
    return abs(BurtResolveSurfaceShadingModel(shadingModelID) - BURT_SHADING_MODEL_CLEAR_COAT) < 0.5f;
}

bool BurtIsSubsurfaceShadingModel(float shadingModelID)
{
    return abs(BurtResolveSurfaceShadingModel(shadingModelID) - BURT_SHADING_MODEL_SUBSURFACE) < 0.5f;
}

bool BurtIsFabricShadingModel(float shadingModelID)
{
    return abs(BurtResolveSurfaceShadingModel(shadingModelID) - BURT_SHADING_MODEL_FABRIC) < 0.5f;
}

bool BurtIsFoliageShadingModel(float shadingModelID)
{
    return abs(BurtResolveSurfaceShadingModel(shadingModelID) - BURT_SHADING_MODEL_FOLIAGE) < 0.5f;
}

bool BurtIsFurShadingModel(float shadingModelID)
{
    return abs(BurtResolveSurfaceShadingModel(shadingModelID) - BURT_SHADING_MODEL_FUR) < 0.5f;
}

bool BurtIsEyeShadingModel(float shadingModelID)
{
    return abs(BurtResolveSurfaceShadingModel(shadingModelID) - BURT_SHADING_MODEL_EYE) < 0.5f;
}

// 保存光照函数需要的材质表面属性。
struct BurtSurfaceData
{
    // 保存材质最终基础颜色，通常来自 Base Map 与 Base Color 的相乘结果。
    float4 BaseColor;

    // 单独保存 alpha，方便 Forward pass 在光照后直接把透明度传给输出。
    float Alpha;

    // 保存材质介质反射率参数，参考 XRender Reflectance，0.5 会映射到非金属 F0=0.04。
    float Reflectance;

    // 保存材质光滑度，数值越高，高光越小越锐利。
    float Smoothness;

    // 保存材质金属度，0 表示非金属，1 表示金属。
    float Metallic;

    float Anisotropy;

    // 保存材质环境遮蔽，1 表示不遮蔽环境光，0 表示完全遮蔽环境光。
    float Occlusion;

    // Stores Mask Map B as the material height debug channel. 0.5 is neutral for materials without a height map.
    float Height;

    // 保存 Deferred shading model，当前 0=Default Lit、1=Hair；Forward 默认也按这个字段选择实验分支。
    float ShadingModelID;

    float ClearCoatMask;

    float ClearCoatRoughness;

    float SubsurfaceThickness;

    float SubsurfacePower;

    float SubsurfaceDistortion;

    float SubsurfaceAmbient;

    float SubsurfaceScatteringMode;

    float Subsurface3SCurvature;

    float SubsurfaceProfileIndex;

    float HairSecondaryRoughness;

    float HairBackLight;

    float HairShadowFillStrength;

    float HairSpecularShift;

    float HairSecondarySpecularShift;

    float3 HairSpecularColor;

    float3 HairSecondarySpecularColor;

    float FabricIsSilk;

    float FabricFuzzWeight;

    float FabricFuzzRoughness;

    float3 FabricFuzzColor;

    float3 FabricFacingColor;

    float3 FoliageTransmissionColor;

    float FoliageTransmissionWeight;

    float FoliageThickness;

    float FoliageBackLight;

    float FoliageTransmissionNdotL;

    float FoliageSpecularScale;

    float FoliageUseSpecularColor;

    float FoliageScreenSpaceShadowIntensity;

    float FoliageIsGrass;

    float EyeIrisMask;

    float3 EyeIrisNormalWS;

    float3 EyeCausticNormalWS;
};

float BurtClampSubsurfacePower(float power)
{
    return clamp(power, BURT_SUBSURFACE_POWER_MIN, BURT_SUBSURFACE_POWER_MAX);
}

float BurtClampSubsurfaceProfileIndex(float profileIndex)
{
    return clamp(floor(profileIndex + 0.5f), 0.0f, BURT_SUBSURFACE_PROFILE_COUNT - 1.0f);
}

float BurtClampSubsurfaceScatteringMode(float scatteringMode)
{
    return clamp(floor(scatteringMode + 0.5f), BURT_SUBSURFACE_SCATTERING_MODE_5S_BURLEY, BURT_SUBSURFACE_SCATTERING_MODE_3S_PREINTEGRATED);
}

bool BurtIsSubsurface3SPreIntegratedMode(float scatteringMode)
{
    return abs(BurtClampSubsurfaceScatteringMode(scatteringMode) - BURT_SUBSURFACE_SCATTERING_MODE_3S_PREINTEGRATED) < 0.5f;
}

bool BurtIsSubsurface4SSeparableMode(float scatteringMode)
{
    return abs(BurtClampSubsurfaceScatteringMode(scatteringMode) - BURT_SUBSURFACE_SCATTERING_MODE_4S_SEPARABLE) < 0.5f;
}

bool BurtIsSubsurface5SBurleyMode(float scatteringMode)
{
    return abs(BurtClampSubsurfaceScatteringMode(scatteringMode) - BURT_SUBSURFACE_SCATTERING_MODE_5S_BURLEY) < 0.5f;
}

void BurtInitializeSubsurfaceSurfaceData(inout BurtSurfaceData surfaceData)
{
    surfaceData.SubsurfaceThickness = BURT_SUBSURFACE_DEFAULT_THICKNESS;
    surfaceData.SubsurfacePower = BURT_SUBSURFACE_DEFAULT_POWER;
    surfaceData.SubsurfaceDistortion = BURT_SUBSURFACE_DEFAULT_DISTORTION;
    surfaceData.SubsurfaceAmbient = BURT_SUBSURFACE_DEFAULT_AMBIENT;
    surfaceData.SubsurfaceScatteringMode = BURT_SUBSURFACE_DEFAULT_SCATTERING_MODE;
    surfaceData.Subsurface3SCurvature = 1.0f - BURT_SUBSURFACE_DEFAULT_THICKNESS;
    surfaceData.SubsurfaceProfileIndex = BURT_SUBSURFACE_DEFAULT_PROFILE_INDEX;
    surfaceData.HairSecondaryRoughness = 0.5f;
    surfaceData.HairBackLight = 0.0f;
    surfaceData.HairShadowFillStrength = 0.0f;
    surfaceData.HairSpecularShift = 0.0f;
    surfaceData.HairSecondarySpecularShift = 0.0f;
    surfaceData.HairSpecularColor = float3(1.0f, 1.0f, 1.0f);
    surfaceData.HairSecondarySpecularColor = float3(1.0f, 1.0f, 1.0f);
    surfaceData.FabricIsSilk = 0.0f;
    surfaceData.FabricFuzzWeight = 0.0f;
    surfaceData.FabricFuzzRoughness = 0.75f;
    surfaceData.FabricFuzzColor = float3(1.0f, 1.0f, 1.0f);
    surfaceData.FabricFacingColor = float3(1.0f, 1.0f, 1.0f);
    surfaceData.FoliageTransmissionColor = float3(0.55f, 0.85f, 0.35f);
    surfaceData.FoliageTransmissionWeight = 0.45f;
    surfaceData.FoliageThickness = 0.5f;
    surfaceData.FoliageBackLight = 0.5f;
    surfaceData.FoliageTransmissionNdotL = 0.5f;
    surfaceData.FoliageSpecularScale = 1.0f;
    surfaceData.FoliageUseSpecularColor = 0.0f;
    surfaceData.FoliageScreenSpaceShadowIntensity = 0.0f;
    surfaceData.FoliageIsGrass = 0.0f;
    surfaceData.EyeIrisMask = 0.0f;
    surfaceData.EyeIrisNormalWS = float3(0.0f, 0.0f, 1.0f);
    surfaceData.EyeCausticNormalWS = float3(0.0f, 0.0f, 1.0f);
}

// 保存片元级几何输入，后续高光、雾效、附加光等功能会继续扩展这个结构。
struct BurtInputData
{
    // 保存当前片元的世界空间位置，未来距离衰减、雾效和更多阴影计算会用到。
    float3 PositionWS;

    // 保存当前片元的世界空间法线，Lambert 或未来 BRDF 光照会用到。
    float3 NormalWS;

    // 保存从片元指向相机的世界空间方向，未来高光和 Fresnel 会用到。
    float3 ViewDirectionWS;
};

// 按 Unity 的贴图 Tiling / Offset 规则转换 Base Map 使用的 mesh UV0。
float2 BurtTransformBaseMapUV(float2 uv0, float4 baseMapST)
{
    // baseMapST.xy 表示 Tiling，baseMapST.zw 表示 Offset。
    return uv0 * baseMapST.xy + baseMapST.zw;
}

// 使用已经转换过的 UV 采样材质 Base Map。
float4 BurtSampleBaseMap(float2 baseMapUV)
{
    return SAMPLE_TEXTURE2D(_BaseMap, sampler_LinearRepeat, baseMapUV);
}

// 按 Unity 的贴图 Tiling / Offset 规则转换 Mask Map 使用的 mesh UV0。
float2 BurtTransformMaskMapUV(float2 uv0, float4 maskMapST)
{
    // maskMapST.xy 表示 Tiling，maskMapST.zw 表示 Offset。
    return uv0 * maskMapST.xy + maskMapST.zw;
}

// 使用已经转换过的 UV 采样材质 Mask Map。
float4 BurtSampleMaskMap(float2 maskMapUV)
{
    // R 通道约定为 Metallic，G 通道约定为 Occlusion，A 通道约定为 Smoothness，B 通道暂时预留。
    return SAMPLE_TEXTURE2D(_MaskMap, sampler_LinearRepeat, maskMapUV);
}

// 根据标量参数和 Mask Map 计算最终金属度。
float BurtResolveMetallic(float metallic, float4 maskMap)
{
    // 默认白色 Mask Map 的 R 为 1，所以最终结果会保持 _Metallic 标量原值。
    return saturate(metallic * maskMap.r);
}

// 根据标量参数和 Mask Map 计算最终光滑度。
float BurtResolveSmoothness(float smoothness, float4 maskMap)
{
    // 默认白色 Mask Map 的 A 为 1，所以最终结果会保持 _Smoothness 标量原值。
    return saturate(smoothness * maskMap.a);
}

float BurtSampleSubsurfaceThicknessMap(float2 baseMapUV)
{
    return SAMPLE_TEXTURE2D(_SubsurfaceThicknessMap, sampler_LinearRepeat, baseMapUV).r;
}

float BurtResolveFabricRoughness(float roughness, float4 maskMap)
{
    return clamp(saturate(roughness * maskMap.a), 0.045f, 1.0f);
}

// 根据 Mask Map 和强度参数计算最终环境遮蔽。
float BurtResolveOcclusion(float4 maskMap, float occlusionStrength)
{
    // G 通道越低表示环境遮蔽越强，强度为 0 时强制回到 1，避免影响旧材质。
    return saturate(lerp(1.0f, maskMap.g, saturate(occlusionStrength)));
}

// 应用 BurtRP 统一的 alpha clip 规则，让 Forward、DepthOnly、ShadowCaster 使用同一套镂空判定。
void BurtApplyAlphaClip(float alpha, float alphaClip, float cutoff)
{
#if defined(BURT_ALPHA_CLIP)
    clip(alpha - cutoff);
#endif
}

// 根据已经合并好的基础颜色创建 BurtSurfaceData。
BurtSurfaceData BurtCreateSurfaceData(float4 baseColor)
{
    // 创建一个输出结构体，下面逐项填充，方便后续继续扩展字段。
    BurtSurfaceData surfaceData;

    // 保存当前片元的基础颜色，光照阶段会把它当成 albedo 使用。
    surfaceData.BaseColor = baseColor;

    // 把 alpha 拆出来单独保存，避免后续代码反复从 baseColor.a 里取值。
    surfaceData.Alpha = baseColor.a;

    // 默认使用 XRender 的 reflectance=0.5，对应常见非金属 F0=0.04。
    surfaceData.Reflectance = BURT_INPUT_DEFAULT_REFLECTANCE;

    // 默认光滑度设为 0.5，给后续显式开启高光的路径提供中间值。
    surfaceData.Smoothness = 0.5f;

    // 默认金属度设为 0，保持旧材质按非金属介质处理。
    surfaceData.Metallic = 0.0f;
    surfaceData.Anisotropy = 0.0f;

    // 默认环境遮蔽设为 1，表示不压暗环境光，保持旧材质亮度不变。
    surfaceData.Occlusion = 1.0f;

    // 默认使用 Default Lit，保持所有旧材质和透明 Lit 路径不变。
    surfaceData.ShadingModelID = BURT_SHADING_MODEL_DEFAULT_LIT;
    surfaceData.Height = 0.5f;
    surfaceData.ClearCoatMask = 0.0f;
    surfaceData.ClearCoatRoughness = 0.2f;
    BurtInitializeSubsurfaceSurfaceData(surfaceData);

    // 返回填充完成的表面数据。
    return surfaceData;
}

// 根据基础颜色、reflectance 和光滑度创建 BurtSurfaceData。
BurtSurfaceData BurtCreateSurfaceData(float4 baseColor, float reflectance, float smoothness)
{
    // 创建一个输出结构体，下面逐项填充，避免依赖旧的默认参数。
    BurtSurfaceData surfaceData;

    // 保存当前片元的基础颜色，漫反射和环境光会使用它。
    surfaceData.BaseColor = baseColor;

    // 保存 alpha，让 Forward pass 可以保持材质透明度输出。
    surfaceData.Alpha = baseColor.a;

    // 保存 XRender 风格 reflectance，而不是直接暴露 F0。
    surfaceData.Reflectance = saturate(reflectance);

    // 把光滑度限制到 0 到 1，避免材质面板或脚本传入异常值。
    surfaceData.Smoothness = saturate(smoothness);

    // 这个重载不传 metallic，所以默认按非金属处理。
    surfaceData.Metallic = 0.0f;
    surfaceData.Anisotropy = 0.0f;

    // 这个重载不传 occlusion，所以默认不遮蔽环境光。
    surfaceData.Occlusion = 1.0f;

    // 默认使用 Default Lit；独立 BurtRP/Hair shader 会覆盖为 Hair。
    surfaceData.ShadingModelID = BURT_SHADING_MODEL_DEFAULT_LIT;
    surfaceData.Height = 0.5f;
    surfaceData.ClearCoatMask = 0.0f;
    surfaceData.ClearCoatRoughness = 0.2f;
    BurtInitializeSubsurfaceSurfaceData(surfaceData);

    // 返回填充完成的表面数据。
    return surfaceData;
}

// 根据基础颜色、reflectance、光滑度和金属度创建 BurtSurfaceData。
BurtSurfaceData BurtCreateSurfaceData(float4 baseColor, float reflectance, float smoothness, float metallic)
{
    // 创建一个输出结构体，下面逐项填充，供 PBR BRDF 使用。
    BurtSurfaceData surfaceData;

    // 保存当前片元的基础颜色，PBR 中它会同时影响漫反射和金属反射颜色。
    surfaceData.BaseColor = baseColor;

    // 保存 alpha，让 Forward pass 可以保持材质透明度输出。
    surfaceData.Alpha = baseColor.a;

    // 保存 XRender 风格 reflectance，它后续会在 BRDF 内部映射成非金属 F0。
    surfaceData.Reflectance = saturate(reflectance);

    // 把光滑度限制到 0 到 1，避免异常材质参数影响 BRDF。
    surfaceData.Smoothness = saturate(smoothness);

    // 把金属度限制到 0 到 1，保证非金属到金属的插值范围稳定。
    surfaceData.Metallic = saturate(metallic);
    surfaceData.Anisotropy = 0.0f;

    // 没有显式传入 Mask Map 时默认不遮蔽环境光，保持旧 PBR 路径亮度不变。
    surfaceData.Occlusion = 1.0f;

    // 默认使用 Default Lit；独立 BurtRP/Hair shader 会覆盖为 Hair。
    surfaceData.ShadingModelID = BURT_SHADING_MODEL_DEFAULT_LIT;
    surfaceData.Height = 0.5f;
    surfaceData.ClearCoatMask = 0.0f;
    surfaceData.ClearCoatRoughness = 0.2f;
    BurtInitializeSubsurfaceSurfaceData(surfaceData);

    // 返回填充完成的表面数据。
    return surfaceData;
}

// 根据基础颜色、reflectance、标量参数和 Mask Map 创建完整 PBR 用 BurtSurfaceData。
BurtSurfaceData BurtApplyClearCoatSurfaceSemantics(BurtSurfaceData surfaceData, float clearCoatMask, float clearCoatRoughness)
{
    surfaceData.ClearCoatMask = saturate(clearCoatMask);
    surfaceData.ClearCoatRoughness = saturate(clearCoatRoughness);
    surfaceData.ShadingModelID = BURT_SHADING_MODEL_CLEAR_COAT;
    return surfaceData;
}

BurtSurfaceData BurtApplyClearCoatSurfaceSemantics(BurtSurfaceData surfaceData, float clearCoatMask)
{
    return BurtApplyClearCoatSurfaceSemantics(surfaceData, clearCoatMask, 0.2f);
}

BurtSurfaceData BurtApplySubsurfaceSurfaceSemantics(
    BurtSurfaceData surfaceData,
    float subsurfaceThickness,
    float subsurfacePower,
    float subsurfaceDistortion,
    float subsurfaceAmbient,
    float subsurface3SCurvature,
    float subsurfaceProfileIndex,
    float subsurfaceScatteringMode)
{
    surfaceData.SubsurfaceThickness = saturate(subsurfaceThickness);
    surfaceData.SubsurfacePower = BurtClampSubsurfacePower(subsurfacePower);
    surfaceData.SubsurfaceDistortion = saturate(subsurfaceDistortion);
    surfaceData.SubsurfaceAmbient = saturate(subsurfaceAmbient);
    surfaceData.Subsurface3SCurvature = saturate(subsurface3SCurvature);
    surfaceData.SubsurfaceProfileIndex = BurtClampSubsurfaceProfileIndex(subsurfaceProfileIndex);
    surfaceData.SubsurfaceScatteringMode = BurtClampSubsurfaceScatteringMode(subsurfaceScatteringMode);
    surfaceData.Reflectance = BURT_SUBSURFACE_FIXED_REFLECTANCE;
    surfaceData.ShadingModelID = BURT_SHADING_MODEL_SUBSURFACE;
    return surfaceData;
}

BurtSurfaceData BurtApplySubsurfaceSurfaceSemantics(
    BurtSurfaceData surfaceData,
    float subsurfaceThickness,
    float subsurfacePower,
    float subsurfaceDistortion,
    float subsurfaceAmbient,
    float subsurfaceProfileIndex)
{
    return BurtApplySubsurfaceSurfaceSemantics(
        surfaceData,
        subsurfaceThickness,
        subsurfacePower,
        subsurfaceDistortion,
        subsurfaceAmbient,
        1.0f - saturate(subsurfaceThickness),
        subsurfaceProfileIndex,
        BURT_SUBSURFACE_DEFAULT_SCATTERING_MODE);
}

BurtSurfaceData BurtApplySubsurfaceSurfaceSemantics(
    BurtSurfaceData surfaceData,
    float subsurfaceThickness,
    float subsurfacePower,
    float subsurfaceDistortion,
    float subsurfaceAmbient)
{
    return BurtApplySubsurfaceSurfaceSemantics(
        surfaceData,
        subsurfaceThickness,
        subsurfacePower,
        subsurfaceDistortion,
        subsurfaceAmbient,
        1.0f - saturate(subsurfaceThickness),
        BURT_SUBSURFACE_DEFAULT_PROFILE_INDEX,
        BURT_SUBSURFACE_DEFAULT_SCATTERING_MODE);
}

BurtSurfaceData BurtApplySubsurfaceSurfaceSemantics(BurtSurfaceData surfaceData)
{
    return BurtApplySubsurfaceSurfaceSemantics(
        surfaceData,
        BURT_SUBSURFACE_DEFAULT_THICKNESS,
        BURT_SUBSURFACE_DEFAULT_POWER,
        BURT_SUBSURFACE_DEFAULT_DISTORTION,
        BURT_SUBSURFACE_DEFAULT_AMBIENT,
        1.0f - BURT_SUBSURFACE_DEFAULT_THICKNESS,
        BURT_SUBSURFACE_DEFAULT_PROFILE_INDEX,
        BURT_SUBSURFACE_DEFAULT_SCATTERING_MODE);
}

BurtSurfaceData BurtApplyAnisotropySurfaceSemantics(BurtSurfaceData surfaceData, float anisotropy)
{
    surfaceData.Anisotropy = clamp(anisotropy, -1.0f, 1.0f);
    return surfaceData;
}

BurtSurfaceData BurtApplyFabricSurfaceSemantics(BurtSurfaceData surfaceData, float fuzzWeight, float3 fuzzColor, float fuzzRoughness)
{
    surfaceData.FabricIsSilk = 0.0f;
    surfaceData.FabricFuzzWeight = saturate(fuzzWeight);
    surfaceData.FabricFuzzColor = max(fuzzColor, float3(0.0f, 0.0f, 0.0f));
    surfaceData.FabricFuzzRoughness = saturate(fuzzRoughness);
    surfaceData.ShadingModelID = BURT_SHADING_MODEL_FABRIC;
    return surfaceData;
}

BurtSurfaceData BurtApplyFoliageSurfaceSemantics(
    BurtSurfaceData surfaceData,
    float3 transmissionColor,
    float transmissionWeight,
    float thickness,
    float backLight,
    float transmissionNdotL,
    float specularScale,
    float useSpecularColor)
{
    surfaceData.Metallic = 0.0f;
    surfaceData.Anisotropy = 0.0f;
    surfaceData.FoliageTransmissionColor = max(transmissionColor, float3(0.0f, 0.0f, 0.0f));
    surfaceData.FoliageTransmissionWeight = saturate(transmissionWeight);
    surfaceData.FoliageThickness = saturate(thickness);
    surfaceData.FoliageBackLight = saturate(backLight);
    surfaceData.FoliageTransmissionNdotL = saturate(transmissionNdotL);
    surfaceData.FoliageSpecularScale = saturate(specularScale);
    surfaceData.FoliageUseSpecularColor = saturate(useSpecularColor);
    surfaceData.FoliageScreenSpaceShadowIntensity = 0.0f;
    surfaceData.FoliageIsGrass = 0.0f;
    surfaceData.ShadingModelID = BURT_SHADING_MODEL_FOLIAGE;
    return surfaceData;
}

BurtSurfaceData BurtApplyFoliageSurfaceSemantics(
    BurtSurfaceData surfaceData,
    float3 transmissionColor,
    float transmissionWeight,
    float thickness,
    float backLight)
{
    return BurtApplyFoliageSurfaceSemantics(surfaceData, transmissionColor, transmissionWeight, thickness, backLight, 0.5f, 1.0f, 0.0f);
}

BurtSurfaceData BurtApplySilkSurfaceSemantics(
    BurtSurfaceData surfaceData,
    float anisotropy,
    float3 facingColor)
{
    surfaceData.Anisotropy = clamp(anisotropy, -1.0f, 1.0f);
    surfaceData.FabricIsSilk = 1.0f;
    surfaceData.FabricFuzzWeight = 0.0f;
    surfaceData.FabricFuzzColor = max(facingColor, float3(0.0f, 0.0f, 0.0f));
    surfaceData.FabricFacingColor = max(facingColor, float3(0.0f, 0.0f, 0.0f));
    surfaceData.ShadingModelID = BURT_SHADING_MODEL_FABRIC;
    return surfaceData;
}

BurtSurfaceData BurtApplyEyeSurfaceSemantics(
    BurtSurfaceData surfaceData,
    float irisMask,
    float3 irisNormalWS,
    float3 causticNormalWS)
{
    surfaceData.Metallic = 0.0f;
    surfaceData.Anisotropy = 0.0f;
    surfaceData.EyeIrisMask = saturate(irisMask);
    surfaceData.EyeIrisNormalWS = BurtSafeNormalize(irisNormalWS);
    surfaceData.EyeCausticNormalWS = BurtSafeNormalize(causticNormalWS);
    surfaceData.ShadingModelID = BURT_SHADING_MODEL_EYE;
    return surfaceData;
}

BurtSurfaceData BurtCreateSurfaceData(float4 baseColor, float reflectance, float smoothness, float metallic, float4 maskMap, float occlusionStrength)
{
    // 创建一个输出结构体，下面逐项填充，供 PBR BRDF 和环境遮蔽共同使用。
    BurtSurfaceData surfaceData;

    // 保存当前片元的基础颜色，PBR 中它会同时影响漫反射和金属反射颜色。
    surfaceData.BaseColor = baseColor;

    // 保存 alpha，让 Forward pass 可以保持材质透明度输出。
    surfaceData.Alpha = baseColor.a;

    // 保存 XRender 风格 reflectance，而不是把 F0 直接暴露给材质。
    surfaceData.Reflectance = saturate(reflectance);

    // 标量 Smoothness 与 Mask Map A 通道相乘，得到最终光滑度。
    surfaceData.Smoothness = BurtResolveSmoothness(smoothness, maskMap);

    // 标量 Metallic 与 Mask Map R 通道相乘，得到最终金属度。
    surfaceData.Metallic = BurtResolveMetallic(metallic, maskMap);
    surfaceData.Anisotropy = 0.0f;

    // Mask Map G 通道经过强度混合后得到最终环境遮蔽，只用于环境光。
    surfaceData.Occlusion = BurtResolveOcclusion(maskMap, occlusionStrength);

    // 默认使用 Default Lit；独立 BurtRP/Hair shader 会覆盖为 Hair。
    surfaceData.ShadingModelID = BURT_SHADING_MODEL_DEFAULT_LIT;
    surfaceData.Height = saturate(maskMap.b);
    surfaceData.ClearCoatMask = 0.0f;
    surfaceData.ClearCoatRoughness = 0.2f;
    BurtInitializeSubsurfaceSurfaceData(surfaceData);

    // 返回填充完成的表面数据。
    return surfaceData;
}

BurtSurfaceData BurtCreateFabricSurfaceData(float4 baseColor, float reflectance, float roughness, float metallic, float4 maskMap, float occlusionStrength)
{
    BurtSurfaceData surfaceData = BurtCreateSurfaceData(baseColor, reflectance, 1.0f, metallic, maskMap, occlusionStrength);
    surfaceData.Smoothness = saturate(1.0f - BurtResolveFabricRoughness(roughness, maskMap));
    surfaceData.ShadingModelID = BURT_SHADING_MODEL_FABRIC;
    return surfaceData;
}

#endif // BURT_INPUT_INCLUDED // 结束 BurtInput.hlsl 的 include guard。
