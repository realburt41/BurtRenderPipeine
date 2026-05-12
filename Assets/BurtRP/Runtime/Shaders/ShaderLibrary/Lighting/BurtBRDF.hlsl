// BurtRP 的 PBR BRDF 工具库，当前实现单主光也能复用，后续多光源会继续调用这里的函数。
#ifndef BURT_BRDF_INCLUDED // 开始 include guard，防止同一个 shader 编译单元里重复定义 BRDF 函数。
#define BURT_BRDF_INCLUDED // 标记 BurtBRDF.hlsl 已经被包含过，后续重复 include 会被跳过。

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl" // 引入安全归一化和基础数学保护。
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInput.hlsl" // 引入 BurtSurfaceData，用来读取 baseColor、metallic、smoothness 等材质参数。

// 定义圆周率，PBR 漫反射和 GGX 分布项都会使用它。
static const float BURT_PI = 3.14159265359f;

// 定义圆周率倒数，Lambert 漫反射用它代替每次做除法。
static const float BURT_INV_PI = 0.31830988618f;

// 定义最小感知粗糙度；0.045 对应当前 BurtRP 的基础高光下限，避免完全镜面造成数值尖峰。
static const float BURT_MIN_PERCEPTUAL_ROUGHNESS = 0.045f;

// 定义屏幕空间法线方差权重；对齐 XRender 默认值 0.1，避免默认过滤过强导致极光滑高光峰值被过度压低。
static const float BURT_SPECULAR_AA_SCREEN_SPACE_VARIANCE = 0.1f;

// 定义高光 AA 能额外增加的最大粗糙度阈值；对齐 XRender 默认值 0.2，限制法线变化过大时的最大拓宽幅度。
static const float BURT_SPECULAR_AA_THRESHOLD = 0.2f;

// 定义 XRender / Frostbite 使用的最大介质 F0，reflectance=1 时非金属 F0 会达到 0.16。
static const float BURT_MATERIAL_MAX_DIELECTRIC_F0 = 0.16f;

// 定义 GGX D 项分母的极小保护值，不能复用 1e-6 的通用 epsilon，否则极光滑时 D 项会被反向压低。
static const float BURT_GGX_DISTRIBUTION_DENOMINATOR_EPSILON = 0.000000000001f;

// 声明 BurtRP 预积分 FG LUT；C# 会绑定 Assets/Textures/PreintegratedFG.exr 或关闭开关走解析近似。
sampler2D _BurtPreIntegratedFG;
float _BurtPreIntegratedFGEnabled;

// 定义预积分 FG LUT 的尺寸；当前资源按 XRender 默认 128x128 生成。
static const float BURT_PREINTEGRATED_FG_LUT_SIZE = 128.0f;
static const float BURT_PREINTEGRATED_FG_LUT_INV_SIZE = 1.0f / BURT_PREINTEGRATED_FG_LUT_SIZE;

// 出处：XRender/Shaders/Library/BRDF.hlsl::rcp_safe；用安全倒数保护 BRDF 分母，避免 0 产生 NaN。
float rcp_safe(float value)
{
    // XRender 原实现使用 1e-7，这里沿用 BurtRP 通用 epsilon 统一数值保护下限。
    return rcp(max(value, BURT_EPSILON));
}

// 出处：XRender/Shaders/Library/Math.hlsl::Pow5；Schlick Fresnel 和 DFG 近似都会用到五次项。
float Pow5(float value)
{
    // 先算平方，减少重复乘法，也避免直接 pow 带来的平台差异。
    float value2 = value * value;

    // value^5 = value^2 * value^2 * value。
    return value2 * value2 * value;
}

// 出处：XRender/Shaders/Library/CommonColors.hlsl::PerceivedLuminance；Energy Preservation 用感知亮度把 RGB 反射能量压成单通道。
float PerceivedLuminance(float3 color)
{
    // XRender 使用 Rec.601 风格权重，和真实亮度 Luminance 区分开，目的是让调试和能量估算更贴近人眼观感。
    return dot(color, float3(0.3f, 0.59f, 0.11f));
}

// 出处：XRender/Shaders/Library/CommonMaterial.hlsl::DiffuseColorFromBaseColor；金属材质不保留普通 diffuse。
float3 DiffuseColorFromBaseColor(float3 baseColor, float metallic)
{
    // metallic 越高，baseColor 越多转移到 specular F0，diffuse 越少。
    return baseColor * (1.0f - metallic);
}

// 出处：XRender/Shaders/Library/CommonMaterial.hlsl::DielectricReflectanceToF0；Frostbite reflectance 到 F0 的映射。
float3 DielectricReflectanceToF0(float3 baseColor, float reflectance, float metallic)
{
    // XRender 公式：MATERIAL_MAX_DIELECTRIC_F0 * Reflectance^2 * (1 - Metallic) + BaseColor * Metallic。
    float dielectricF0 = BURT_MATERIAL_MAX_DIELECTRIC_F0 * saturate(reflectance) * saturate(reflectance);

    // 非金属 F0 由 reflectance 映射得到，金属 F0 来自 baseColor。
    return dielectricF0 * (1.0f - saturate(metallic)) + baseColor * saturate(metallic);
}

// 出处：XRender/Shaders/Library/CommonMaterial.hlsl::PerceptualSmoothnessToPerceptualRoughness；BurtRP 面板仍暴露 Smoothness。
float PerceptualSmoothnessToPerceptualRoughness(float perceptualSmoothness)
{
    // smoothness 越高 roughness 越低，Deferred 后续也应按同一规则从 GBuffer 还原。
    return 1.0f - saturate(perceptualSmoothness);
}

// 出处：XRender/Shaders/Library/CommonMaterial.hlsl::ClampPerceptualRoughness；避免完全镜面造成数值尖峰。
float ClampPerceptualRoughness(float perceptualRoughness)
{
    // BurtRP 当前最小粗糙度略高于 XRender PC 默认值，用来控制无 TAA 阶段的高光稳定性。
    return clamp(perceptualRoughness, BURT_MIN_PERCEPTUAL_ROUGHNESS, 1.0f);
}

// BurtRP 表面数据适配函数：把面板 smoothness 转成 XRender 语义的 Base.Roughness。
float GetSurfacePerceptualRoughness(BurtSurfaceData surfaceData)
{
    // 先做 smoothness -> roughness，再按 XRender 的 ClampPerceptualRoughness 语义做下限保护。
    return ClampPerceptualRoughness(PerceptualSmoothnessToPerceptualRoughness(surfaceData.smoothness));
}

// 出处：XRender/Shaders/Library/CommonMaterial.hlsl::GeometricNormalVariance；估算屏幕空间法线变化导致的高光方差。
float GeometricNormalVariance(float3 geometricNormalWS, float screenSpaceVariance)
{
    // 先安全归一化法线，避免长度误差放大 ddx/ddy 的方差。
    float3 safeNormalWS = BurtSafeNormalize(geometricNormalWS);

    // 计算当前像素在屏幕 x 方向的法线变化，导数越大表示高光越容易闪烁或漏采样。
    float3 deltaX = ddx(safeNormalWS);

    // 计算当前像素在屏幕 y 方向的法线变化，和 x 方向一起描述一个像素覆盖范围内的法线分布。
    float3 deltaY = ddy(safeNormalWS);

    // 把两个方向的变化量转成方差，并乘以屏幕空间权重。
    return screenSpaceVariance * (dot(deltaX, deltaX) + dot(deltaY, deltaY));
}

// 出处：XRender/Shaders/Library/CommonMaterial.hlsl::NormalFiltering；把法线方差折算回感知粗糙度。
float NormalFiltering(float perceptualRoughness, float variance, float threshold)
{
    // Ref: Geometry into Shading, equation (3)；阈值限制过滤过强导致高光被过度拓宽。
    float squaredPerceptualRoughness = saturate(perceptualRoughness * perceptualRoughness + min(2.0f * variance, threshold * threshold));

    // 开平方回到感知粗糙度空间，材质滑块和后续 DFG 仍然使用同一层语义。
    return sqrt(squaredPerceptualRoughness);
}

// 出处：XRender/Shaders/Library/CommonMaterial.hlsl::GeometricNormalFiltering；对几何法线应用 Specular AA。
float GeometricNormalFiltering(float perceptualRoughness, float3 geometricNormalWS, float screenSpaceVariance, float threshold)
{
    // 先估算法线方差，再交给 NormalFiltering 转成过滤后的感知粗糙度。
    float variance = GeometricNormalVariance(geometricNormalWS, screenSpaceVariance);

    // 只允许过滤增加粗糙度，不允许把材质本身变得更光滑。
    return max(perceptualRoughness, NormalFiltering(perceptualRoughness, variance, threshold));
}

// 保存 Specular AA 中间项，方便 Debug View 同时观察法线方差和粗糙度拓宽幅度。
struct BurtSpecularAATerms
{
    // 保存材质原始感知粗糙度，也就是未经过屏幕空间法线过滤的 Base.Roughness。
    float materialPerceptualRoughness;

    // 保存 GeometricNormalVariance 的结果，数值越大表示像素内法线变化越强。
    float normalVariance;

    // 保存 Specular AA 后的感知粗糙度，直接高光会使用这个值。
    float filteredPerceptualRoughness;

    // 保存 filtered - material 的差值，越大表示 Specular AA 对高光拓宽越明显。
    float roughnessDelta;
};

// BurtRP Specular AA 调试入口：一次性拿到法线方差、过滤前 roughness 和过滤后 roughness。
BurtSpecularAATerms BurtEvaluateSpecularAATerms(float materialPerceptualRoughness, float3 geometricNormalWS)
{
    // 创建输出结构体，下面逐项填入 Specular AA 的中间结果。
    BurtSpecularAATerms terms;

    // 先保存材质原始 roughness，Debug View 可以和过滤后值做对比。
    terms.materialPerceptualRoughness = materialPerceptualRoughness;

    // 复用 XRender GeometricNormalVariance 估算像素内法线变化。
    terms.normalVariance = GeometricNormalVariance(geometricNormalWS, BURT_SPECULAR_AA_SCREEN_SPACE_VARIANCE);

    // 把法线方差折算到 roughness，并且只允许粗糙度增加。
    terms.filteredPerceptualRoughness = max(materialPerceptualRoughness, NormalFiltering(materialPerceptualRoughness, terms.normalVariance, BURT_SPECULAR_AA_THRESHOLD));

    // 记录拓宽幅度，后续 debug 可以直接看 Specular AA 对当前像素的影响。
    terms.roughnessDelta = max(terms.filteredPerceptualRoughness - terms.materialPerceptualRoughness, 0.0f);

    // 返回完整中间项。
    return terms;
}

// BurtRP 直接高光适配函数：材质粗糙度 + XRender 几何法线过滤。
float GetDirectSpecularPerceptualRoughness(BurtSurfaceData surfaceData, float3 normalWS)
{
    // 先取得材质本身的感知粗糙度，也就是 1 - smoothness 后的结果。
    float materialRoughness = GetSurfacePerceptualRoughness(surfaceData);

    // 再把屏幕空间法线变化折算进去，避免极光滑材质的高光小到被像素漏采样。
    return BurtEvaluateSpecularAATerms(materialRoughness, normalWS).filteredPerceptualRoughness;
}

// 出处：XRender/Shaders/Library/CommonMaterial.hlsl::PerceptualRoughnessToLinearRoughness；感知粗糙度转线性粗糙度。
float PerceptualRoughnessToLinearRoughness(float perceptualRoughness)
{
    // 线性 roughness 使用感知 roughness 的平方，让材质滑块在视觉上更均匀。
    return max(perceptualRoughness * perceptualRoughness, BURT_EPSILON);
}

// 出处：XRender/Shaders/Library/CommonMaterial.hlsl::LinearRoughnessToA2；线性粗糙度转 GGX D 项使用的 A2。
float LinearRoughnessToA2(float linearRoughness)
{
    // A2 太小会让高光尖峰过高，所以用 BURT_EPSILON 做最低保护。
    return max(linearRoughness * linearRoughness, BURT_EPSILON);
}

// 出处：XRender/Shaders/Library/CommonMaterial.hlsl::ApproximateF90；默认掠射角反射端点。
float3 ApproximateF90(float3 f0)
{
    // XRender 当前 DefaultLit 路径返回 1；参数保留在签名里，方便后续接入自定义 F90 或薄膜材质。
    return float3(1.0f, 1.0f, 1.0f);
}

// 保存 PBR 材质侧可从 Forward SurfaceData 或未来 Deferred GBuffer 还原的数据，避免光照函数直接依赖材质面板结构。
struct BurtPBRMaterialData
{
    // 保存材质基础色 RGB，Deferred 后续可以从 GBuffer 还原。
    float3 baseColor;

    // 保存金属度，既参与 diffuseColor，也参与 reflectance 到 F0 的重建。
    float metallic;

    // 保存 XRender 风格 reflectance；不直接暴露 F0，后续统一从 reflectance 还原。
    float reflectance;

    // 保存环境遮蔽，用于间接漫反射和间接高光遮蔽。
    float occlusion;

    // 保存面板 smoothness，Debug View 和 GBuffer 检查仍然需要看原始语义。
    float smoothness;

    // 保存 XRender Base.Roughness，也就是 BurtRP smoothness 转换后的感知粗糙度。
    float perceptualRoughness;

    // 保存线性粗糙度，供 GGX 可见性、Specular Occlusion 等公式复用。
    float linearRoughness;

    // 保存 GGX D 项使用的 a^2，减少直接光中重复计算。
    float a2;

    // 保存金属度扣除后的 diffuseColor，对应 XRender GenericData.DiffuseColor。
    float3 diffuseColor;

    // 保存从 baseColor、reflectance、metallic 重建的 F0，对应 XRender GenericData.F0。
    float3 f0;

    // 保存默认掠射角端点，对应 XRender GenericData.F90。
    float3 f90;
};

// 从 BurtSurfaceData 准备 PBR 材质数据；Deferred 后续可以做一个从 GBuffer 还原到同一结构的入口。
BurtPBRMaterialData BurtPreparePBRMaterialData(BurtSurfaceData surfaceData)
{
    // 创建输出结构体，下面按 XRender GenericData 的语义逐项填充。
    BurtPBRMaterialData materialData;

    // 记录原始材质输入，避免后续光照函数再次依赖 SurfaceData 的字段布局。
    materialData.baseColor = surfaceData.baseColor.rgb;
    materialData.metallic = surfaceData.metallic;
    materialData.reflectance = surfaceData.reflectance;
    materialData.occlusion = surfaceData.occlusion;
    materialData.smoothness = surfaceData.smoothness;

    // 把 BurtRP smoothness 转成 XRender Base.Roughness，并准备 GGX 常用粗糙度层级。
    materialData.perceptualRoughness = GetSurfacePerceptualRoughness(surfaceData);
    materialData.linearRoughness = PerceptualRoughnessToLinearRoughness(materialData.perceptualRoughness);
    materialData.a2 = LinearRoughnessToA2(materialData.linearRoughness);

    // 准备 diffuseColor、F0 和 F90，Forward 与 Deferred 都应复用同一套 reflectance 映射。
    materialData.diffuseColor = DiffuseColorFromBaseColor(materialData.baseColor, materialData.metallic);
    materialData.f0 = DielectricReflectanceToF0(materialData.baseColor, materialData.reflectance, materialData.metallic);
    materialData.f90 = ApproximateF90(materialData.f0);

    // 返回准备好的材质数据，后续 BRDF 和 IBL 都只读取这个结构。
    return materialData;
}

// 保存 PBR 几何侧数据；Forward 由插值输入生成，Deferred 后续由 GBuffer normal 和重建 view direction 生成。
struct BurtPBRGeometryData
{
    // 保存安全归一化后的世界空间法线。
    float3 normalWS;

    // 保存安全归一化后的世界空间视线方向，约定从表面指向相机。
    float3 viewDirectionWS;

    // 保存 NdotV，Energy Term、DFG 和 Specular Occlusion 都会复用。
    float nDotV;

    // 保存环境反射方向，Reflection Probe / Sky Specular 会复用。
    float3 reflectionDirectionWS;
};

// 从世界空间法线和视线方向准备 PBR 几何数据，方便 Forward 和 Deferred 使用同一套几何约定。
BurtPBRGeometryData BurtPreparePBRGeometryData(float3 normalWS, float3 viewDirectionWS)
{
    // 创建输出结构体，下面逐项写入安全归一化后的几何量。
    BurtPBRGeometryData geometryData;

    // 法线和视线都做安全归一化，避免 Forward 插值或 Deferred 重建误差进入 BRDF。
    geometryData.normalWS = BurtSafeNormalize(normalWS);
    geometryData.viewDirectionWS = BurtSafeNormalize(viewDirectionWS);

    // NdotV 统一夹到 0..1，保持和现有 DFG、Energy Term 输入一致。
    geometryData.nDotV = saturate(dot(geometryData.normalWS, geometryData.viewDirectionWS));

    // reflect 的入射方向需要从相机指向表面，所以使用 -viewDirectionWS。
    geometryData.reflectionDirectionWS = reflect(-geometryData.viewDirectionWS, geometryData.normalWS);

    // 返回准备好的几何数据，后续直接光和间接光都复用同一份。
    return geometryData;
}

// BurtRP 直接高光适配函数：使用已经准备好的材质/几何数据计算 Specular AA 后的感知粗糙度。
float GetDirectSpecularPerceptualRoughness(BurtPBRMaterialData materialData, BurtPBRGeometryData geometryData)
{
    // XRender 几何法线过滤只影响直接高光，不回写材质 Base.Roughness。
    return BurtEvaluateSpecularAATerms(materialData.perceptualRoughness, geometryData.normalWS).filteredPerceptualRoughness;
}

// BurtRP Specular AA 调试入口：使用准备好的材质/几何数据返回完整中间项。
BurtSpecularAATerms BurtEvaluateSpecularAATerms(BurtPBRMaterialData materialData, BurtPBRGeometryData geometryData)
{
    // 复用 float 版本，保证调试值和直接高光实际使用的过滤逻辑完全一致。
    return BurtEvaluateSpecularAATerms(materialData.perceptualRoughness, geometryData.normalWS);
}

// 出处：XRender/Shaders/Library/BRDF.hlsl::Fd_Lambert；标准 Lambert 漫反射 lobe。
float Fd_Lambert()
{
    // XRender 返回 INV_PI；BurtRP 使用同一常量，确保 direct/indirect diffuse 的能量尺度一致。
    return BURT_INV_PI;
}

// 出处：XRender/Shaders/Library/BRDF.hlsl::Fd_Diffuse_Burley；Disney/Burley 粗糙漫反射 lobe。
float Fd_Diffuse_Burley(float roughness, float noV, float noL, float voH)
{
    // XRender 公式：FD90 = 0.5 + 2 * VoH^2 * Roughness。
    float fd90 = 0.5f + 2.0f * voH * voH * roughness;

    // 视线侧散射项，掠射角会增强粗糙漫反射。
    float fdV = 1.0f + (fd90 - 1.0f) * Pow5(1.0f - saturate(noV));

    // 光照侧散射项，和视线侧一起构成 Burley diffuse。
    float fdL = 1.0f + (fd90 - 1.0f) * Pow5(1.0f - saturate(noL));

    // 返回带 1/PI 的 diffuse lobe。
    return BURT_INV_PI * fdV * fdL;
}

#ifndef BURT_USE_DISNEY_DIFFUSE
#define BURT_USE_DISNEY_DIFFUSE 0
#endif

// 出处：XRender/Shaders/SlabLobes/SL_Diffuse.hlsl::SlabLobe_Diffuse；当前默认 Lambert，保留 Burley 分支给后续材质/质量级别切换。
float SlabLobe_Diffuse(BurtPBRMaterialData materialData, float noV, float noL, float voH)
{
#if BURT_USE_DISNEY_DIFFUSE
    // Burley 使用材质 Base.Roughness，不使用 Specular AA 后的直接高光 roughness。
    return Fd_Diffuse_Burley(materialData.perceptualRoughness, noV, noL, voH);
#else
    // 默认沿用当前 Lambert 结果，避免本轮整理改变已有 diffuse 观感。
    return Fd_Lambert();
#endif
}

// 出处：XRender/Shaders/Library/BRDF.hlsl::F_Schlick；标准 Schlick Fresnel，u 对应 VoH/LoH。
float3 F_Schlick(float3 f0, float3 f90, float u)
{
    // 返回从 F0 到 F90 的角度相关反射率，掠射角会更接近 F90。
    return f0 + (f90 - f0) * Pow5(1.0f - saturate(u));
}

// 出处：XRender/Shaders/Library/BRDF.hlsl::F_Schlick；默认 F90 为 1 的重载。
float3 F_Schlick(float3 f0, float u)
{
    // 旧接口等价于常见的 f0 -> 1 的 Schlick 近似。
    return F_Schlick(f0, float3(1.0f, 1.0f, 1.0f), u);
}

// 出处：XRender/Shaders/Library/BRDF.hlsl::F_Schlick_UE；BurtRP 直接高光使用这个名字便于和 XRender 溯源。
float3 F_Schlick_UE(float3 f0, float3 f90, float voH)
{
    // XRender 的三参数版本等价于 F90 * Fc + (1 - Fc) * F0。
    return F_Schlick(f0, f90, voH);
}

// 出处：XRender/Shaders/Library/BRDF.hlsl::D_GGX；Unreal/Frostbite/Filament 使用的 GGX/Trowbridge-Reitz D 项。
float D_GGX(float a2, float noH)
{
    // XRender 原式：d = (NoH * A2 - NoH) * NoH + 1。
    float denom = (noH * a2 - noH) * noH + 1.0f;

    // 分母只做极小保护，避免高 smoothness 时峰值被 1e-6 这类通用 epsilon 截断。
    return a2 / max(BURT_PI * denom * denom, BURT_GGX_DISTRIBUTION_DENOMINATOR_EPSILON);
}

// 出处：XRender/Shaders/Library/BRDF.hlsl::Vis_SmithJointApprox；返回 G / (4 * NoV * NoL)。
float Vis_SmithJointApprox(float linearRoughness, float noV, float noL)
{
    // 计算视线侧遮蔽对光照方向的影响。
    float visibilityV = noL * (noV * (1.0f - linearRoughness) + linearRoughness);

    // 计算光照侧遮蔽对视线方向的影响，和上面一项组成 joint visibility。
    float visibilityL = noV * (noL * (1.0f - linearRoughness) + linearRoughness);

    // 乘 0.5 后再除以两项之和；rcp_safe 负责保护极小分母。
    return 0.5f * rcp_safe(visibilityV + visibilityL);
}

// 出处：XRender/Shaders/Library/BRDF.hlsl::PrefilteredDFG_Approx；Lazarov 2013 的环境 BRDF 拟合。
float2 PrefilteredDFG_Approx(float roughness, float noV)
{
    // c0 是经验拟合系数，XRender 的 PrefilteredDFG_Approx 也使用这组值。
    const float4 c0 = float4(-1.0f, -0.0275f, -0.572f, 0.022f);

    // c1 是经验拟合系数，用来把 roughness 映射到 DFG 的 AB 两项。
    const float4 c1 = float4(1.0f, 0.0425f, 1.04f, -0.04f);

    // 根据 roughness 插值得到中间拟合参数。
    float4 r = roughness * c0 + c1;

    // a004 负责拟合视角相关的能量变化，NoV 越小掠射角效果越强。
    float a004 = min(r.x * r.x, exp2(-9.28f * noV)) * r.x + r.y;

    // AB 分别对应 F0 和 F90 的权重，后面会组合成环境 BRDF。
    return float2(-1.04f, 1.04f) * a004 + r.zw;
}

// 出处：XRender/Shaders/Library/CommonTransform.hlsl::Remap01CoordToHalfTexelCoord；把 0..1 坐标移到半 texel 中心。
float2 Remap01CoordToHalfTexelCoord(float2 coord, float2 invSize)
{
    // 保证 128x128 LUT 的首尾采样落在 texel 中心，避免采到边缘外推值。
    return coord * (1.0f - invSize) + 0.5f * invSize;
}

// 出处：XRender/Shaders/Library/CommonMaterial.hlsl::GetPreIntegratedFG；采样预积分 FG LUT。
float3 GetPreIntegratedFG(float clampedNdotV, float perceptualRoughness)
{
    // XRender 使用 float2(NdotV, 1 - PerceptualRoughness) 作为 LUT 坐标。
    float2 uv = float2(saturate(clampedNdotV), 1.0f - saturate(perceptualRoughness));

    // 对齐 XRender 的半 texel 重映射，避免 LUT 边界采样误差。
    uv = Remap01CoordToHalfTexelCoord(uv, float2(BURT_PREINTEGRATED_FG_LUT_INV_SIZE, BURT_PREINTEGRATED_FG_LUT_INV_SIZE));

    // RGB 分别保存 DFG.x、DFG.y 和能量补偿使用的单次散射能量 Z。
    return tex2D(_BurtPreIntegratedFG, uv).rgb;
}

// BurtRP LUT 适配函数：读取预积分 FG；没有绑定 LUT 时回退到解析 DFG，保证材质仍可渲染。
float3 GetPreIntegratedFGOrApprox(float perceptualRoughness, float clampedNdotV)
{
    // 解析近似只提供 DFG.xy，因此用 DFG.x + DFG.y 近似能量补偿项，作为无 LUT 的安全 fallback。
    float2 approxDFG = PrefilteredDFG_Approx(perceptualRoughness, clampedNdotV);
    float approxEnergy = clamp(approxDFG.x + approxDFG.y, 0.001f, 1.0f);
    float3 approxFG = float3(approxDFG, approxEnergy);

    // 当 C# 确认 LUT 已绑定时使用贴图结果，否则完全使用解析近似。
    float3 lutFG = GetPreIntegratedFG(clampedNdotV, perceptualRoughness);
    return lerp(approxFG, lutFG, saturate(_BurtPreIntegratedFGEnabled));
}

// BurtRP 适配函数：返回环境 BRDF 使用的 DFG.xy；优先使用 PreintegratedFG.exr，回退到解析近似。
float2 GetSpecularDFGTerms(float perceptualRoughness, float clampedNdotV)
{
    // LUT 的 xy 对应 XRender 注释里的 ibl brdf 两项，后续会分别乘 F0 和 F90。
    return GetPreIntegratedFGOrApprox(perceptualRoughness, clampedNdotV).xy;
}

// 出处：XRender/Shaders/Library/BRDF.hlsl::EvalSpecularDFG；把 DFG 的 AB 两项应用到 F0/F90 上。
float3 EvalSpecularDFG(float3 f0, float3 f90, float2 dfg)
{
    // XRender 公式：F0 * DFG.x + F90 * DFG.y。
    return f0 * dfg.x + f90 * dfg.y;
}

// 出处：XRender/Shaders/Library/CommonMaterial.hlsl::ComputeEnergyCompensation；用 LUT.z 的单次散射能量补多次散射。
float3 ComputeEnergyCompensation(float3 f0, float z)
{
    // XRender 公式：1 + F0 * (1 / Z - 1)，Z 越低说明单次散射漏能越多，需要越多补偿。
    return 1.0f + f0 * (rcp_safe(max(z, 0.001f)) - 1.0f);
}

// 出处：XRender/Shaders/Library/CommonMaterial.hlsl::ComputeEnergyPreservation；UE5 ShadingEnergyConservationTemplate 用预积分能量估算反射层占用的能量。
float ComputeEnergyPreservation(float3 f0, float3 f90, float3 energy, float3 w)
{
    // Energy.z 是 F0 单次散射能量，Energy.y 是 F90-F0 权重；w 是多次散射能量补偿。
    float3 reflectedEnergy = w * (energy.z * f0 + energy.y * (f90 - f0));

    // XRender 用感知亮度把 RGB 反射能量压成单通道，作为底层 diffuse 可保留的比例。
    return 1.0f - PerceivedLuminance(reflectedEnergy);
}

// 出处：XRender/Shaders/Library/CommonMaterial.hlsl::CalculateEnergyTerm；BurtRP 暂时常开 EnergyCompensation 和 EnergyPreservation。
void CalculateEnergyTerm(float3 f0, float3 f90, float3 energy, out float3 energyCompensation, out float energyPreservation)
{
    // LUT.z 作为除数时需要极小值保护，避免贴图未绑定或异常 texel 产生过大补偿。
    energy.z = clamp(energy.z, 0.001f, 1.0f);

    // 先补高光多次散射能量，后续 preservation 要用补偿后的反射能量估算底层透过率。
    energyCompensation = ComputeEnergyCompensation(f0, energy.z);

    // preservation 是给底层 diffuse 的 one-minus-reflectance，最终限制到 0..1 防止调试 LUT 过冲。
    energyPreservation = saturate(ComputeEnergyPreservation(f0, f90, energy, energyCompensation));
}

// BurtRP 适配函数：从预积分 FG 取出 XRender Energy.xyz，并一次性算出补偿和保能项，方便 Forward/Deferred 共用。
void GetSpecularEnergyTerms(float3 f0, float3 f90, float perceptualRoughness, float clampedNdotV, out float3 energyCompensation, out float energyPreservation)
{
    // PreIntegratedFG.rgb 对应 XRender 的 Energy.xyz；没有 LUT 时由解析近似提供保守 fallback。
    float3 energy = GetPreIntegratedFGOrApprox(perceptualRoughness, clampedNdotV);

    // 复用 XRender 原名 CalculateEnergyTerm，保持 energy compensation / preservation 的公式来源清晰。
    CalculateEnergyTerm(f0, f90, energy, energyCompensation, energyPreservation);
}

// BurtRP 适配函数：从预积分 FG 中取 Energy.z，再调用 XRender 原名 ComputeEnergyCompensation。
float3 GetSpecularEnergyCompensation(float3 f0, float perceptualRoughness, float clampedNdotV)
{
    // 复用统一 energy terms，避免补偿和保能调试读取到两套不同的 LUT 数据。
    float3 energyCompensation;
    float energyPreservation;
    GetSpecularEnergyTerms(f0, ApproximateF90(f0), perceptualRoughness, clampedNdotV, energyCompensation, energyPreservation);

    // 只返回高光多次散射补偿，保持旧调用点的语义不变。
    return energyCompensation;
}

// BurtRP 适配函数：返回 XRender EnergyPreservation，也就是 SlabOperator_Layering 给底层 diffuse 的 one-minus-reflectance。
float GetSpecularEnergyPreservation(float3 f0, float3 f90, float perceptualRoughness, float clampedNdotV)
{
    // 复用统一 energy terms，确保 Debug View 看到的 preservation 和真实 diffuse 缩放一致。
    float3 energyCompensation;
    float energyPreservation;
    GetSpecularEnergyTerms(f0, f90, perceptualRoughness, clampedNdotV, energyCompensation, energyPreservation);

    // 返回单通道底层透过率，1 表示不压暗 diffuse，0 表示 specular 顶层占满能量。
    return energyPreservation;
}

// 保存 PBR 能量项，集中管理 XRender EnergyCompensation_GGX 与 EnergyPreservation，方便 Deferred 一次性准备。
struct BurtPBREnergyTerms
{
    // 保存直接高光能量补偿；使用 Specular AA 后的 roughness，保持直接高光和已调准结果一致。
    float3 directSpecularEnergyCompensation;

    // 保存间接高光能量补偿；使用材质 Base.Roughness，对齐 XRender Sky/EnvProbe Specular。
    float3 indirectSpecularEnergyCompensation;

    // 保存底层 diffuse 保能比例；使用材质 Base.Roughness，对齐 XRender GenericData.EnergyPreservation。
    float energyPreservation;
};

// 统一准备 PBR Energy Terms；Forward 和 Deferred 都应优先走这个入口，避免 direct/indirect 各查一套 FG LUT。
BurtPBREnergyTerms BurtPreparePBREnergyTerms(BurtPBRMaterialData materialData, BurtPBRGeometryData geometryData, float directSpecularPerceptualRoughness)
{
    // 创建输出结构体，下面分别填充直接高光补偿、间接高光补偿和 diffuse 保能。
    BurtPBREnergyTerms energyTerms;

    // 直接高光补偿继续使用 AA 后 roughness，避免把之前校准好的 direct specular 峰值改掉。
    float unusedDirectEnergyPreservation;
    GetSpecularEnergyTerms(materialData.f0, materialData.f90, directSpecularPerceptualRoughness, geometryData.nDotV, energyTerms.directSpecularEnergyCompensation, unusedDirectEnergyPreservation);

    // 间接高光补偿和 EnergyPreservation 都使用材质 Base.Roughness，所以可以共用一次 XRender CalculateEnergyTerm。
    GetSpecularEnergyTerms(materialData.f0, materialData.f90, materialData.perceptualRoughness, geometryData.nDotV, energyTerms.indirectSpecularEnergyCompensation, energyTerms.energyPreservation);

    // 返回集中准备好的能量项，后续光照函数只消费结构体，不再重复采样 FG LUT。
    return energyTerms;
}

// 出处：XRender/Shaders/Library/CommonMaterial.hlsl::GetSpecularOcclusionFromAmbientOcclusion；HDRP/Frostbite AO 高光遮蔽。
float GetSpecularOcclusionFromAmbientOcclusion(float noV, float ao, float linearRoughness)
{
    // 粗糙度越高，AO 对高光遮蔽越平滑。
    float exponent = exp2(-16.0f * saturate(linearRoughness) - 1.0f);
    return saturate(pow(max(saturate(noV) + saturate(ao), BURT_EPSILON), exponent) - 1.0f + saturate(ao));
}

// 出处：XRender/Shaders/Library/CommonMaterial.hlsl::GetSpecularOcclusion；UE ReflectionEnvironmentShared.ush 风格的间接高光遮蔽。
float GetSpecularOcclusion(float noV, float ao, float linearRoughness)
{
    // XRender 当前 GenericData.Setup 在 SpecularAO 分支里使用这个 UE 近似，roughness 越大 AO 影响越平滑。
    return saturate(pow(max(saturate(noV) + saturate(ao), BURT_EPSILON), abs(linearRoughness)) - 1.0f + saturate(ao));
}

// BurtRP 适配函数：间接高光遮蔽输入使用感知粗糙度，内部转 XRender 的 LinearRoughness。
float GetIndirectSpecularOcclusion(float noV, float ao, float perceptualRoughness)
{
    // 对齐 XRender GenericData.Setup.hlsl 当前启用的 UE Approximate，而不是 HDRP/Frostbite 版本。
    return GetSpecularOcclusion(noV, ao, PerceptualRoughnessToLinearRoughness(perceptualRoughness));
}

// 保存直接光 BRDF 的中间项，方便 Debug View 拆开查看 D / V / F / diffuse lobe。
struct BurtDirectBRDFTerms
{
    // 保存 NdotL，控制直接光受光角度。
    float nDotL;

    // 保存 NdotV，控制 Fresnel、Smith 可见性和能量项。
    float nDotV;

    // 保存 NdotH，控制 GGX D 项峰值位置。
    float nDotH;

    // 保存 VdotH，作为 Schlick Fresnel 输入。
    float vDotH;

    // 保存直接高光实际使用的感知粗糙度，也就是包含 Specular AA 的 roughness。
    float perceptualRoughness;

    // 保存直接高光使用的线性粗糙度。
    float linearRoughness;

    // 保存直接高光使用的 a^2。
    float a2;

    // 保存 GGX NDF D 项。
    float d;

    // 保存 Smith Joint Visibility 项，对应 G / (4NoVNoL)。
    float visibility;

    // 保存 Schlick Fresnel 项。
    float3 fresnel;

    // 保存 diffuse lobe，默认 Lambert，可切 Burley。
    float diffuseLobe;

    // 保存未乘灯光颜色、NdotL 和阴影的 diffuse BRDF。
    float3 diffuseBRDF;

    // 保存未乘灯光颜色、NdotL 和阴影的 specular BRDF，已经包含 energy compensation。
    float3 specularBRDF;
};

// 计算直接光 BRDF 中间项；这里只算 BRDF，不乘 lightColor、NdotL 和 shadow，方便 Forward/Deferred/Debug 共用。
BurtDirectBRDFTerms BurtEvaluateDirectBRDFTerms(
    BurtPBRMaterialData materialData,
    BurtPBRGeometryData geometryData,
    BurtPBREnergyTerms energyTerms,
    float directSpecularPerceptualRoughness,
    float3 lightDirectionWS)
{
    // 创建输出结构体，下面逐项写入直接光 BRDF 的中间项。
    BurtDirectBRDFTerms terms;

    // 复用准备阶段已经安全归一化的法线和视线。
    float3 n = geometryData.normalWS;
    float3 v = geometryData.viewDirectionWS;

    // 归一化灯光方向，当前约定它是从表面指向光源。
    float3 l = BurtSafeNormalize(lightDirectionWS);

    // 半角向量用于 Fresnel、D 项和高光形状。
    float3 h = BurtSafeNormalize(l + v);

    // 计算直接光常用的角度项。
    terms.nDotL = saturate(dot(n, l));
    terms.nDotV = geometryData.nDotV;
    terms.nDotH = saturate(dot(n, h));
    terms.vDotH = saturate(dot(v, h));

    // 准备直接高光的粗糙度层级；这里使用 Specular AA 后的 roughness。
    terms.perceptualRoughness = directSpecularPerceptualRoughness;
    terms.linearRoughness = PerceptualRoughnessToLinearRoughness(terms.perceptualRoughness);
    terms.a2 = LinearRoughnessToA2(terms.linearRoughness);

    // XRender 的直接高光 lobe 拆项：D / V / F。
    terms.d = D_GGX(terms.a2, terms.nDotH);
    terms.visibility = Vis_SmithJointApprox(terms.linearRoughness, terms.nDotV, terms.nDotL);
    terms.fresnel = F_Schlick_UE(materialData.f0, materialData.f90, terms.vDotH);

    // XRender 的 diffuse lobe 当前默认 Lambert，后续可切 Burley 分支。
    terms.diffuseLobe = SlabLobe_Diffuse(materialData, terms.nDotV, terms.nDotL, terms.vDotH);

    // XRender layering：底层 diffuse 使用 EnergyPreservation 作为透过率。
    terms.diffuseBRDF = materialData.diffuseColor * terms.diffuseLobe * energyTerms.energyPreservation;

    // XRender specular lobe：D * V * F，再用 EnergyCompensation_GGX 补多次散射损失。
    terms.specularBRDF = terms.d * terms.visibility * terms.fresnel * energyTerms.directSpecularEnergyCompensation;

    // 返回完整中间项，调用方再决定如何乘灯光可见性。
    return terms;
}
// 保存直接 PBR 光照拆分结果，方便正常渲染和 Debug View 共用同一套 BRDF 计算。

struct BurtDirectPBRComponents
{
    // 保存直接漫反射最终贡献，已经包含灯光颜色、NdotL 和阴影衰减。
    float3 diffuse;

    // 保存直接镜面高光最终贡献，已经包含灯光颜色、NdotL 和阴影衰减。
    float3 specular;

    // 保存 XRender EnergyPreservation，表示 specular 顶层之后底层 diffuse 还能保留的能量比例。
    float energyPreservation;

    // 保存直接 BRDF 中间项，Debug View 可以拆开查看 D / V / F 和 diffuse/specular BRDF。
    BurtDirectBRDFTerms brdfTerms;
};

// 使用已经准备好的 PBR 数据计算单个方向光贡献；Deferred 后续可以从 GBuffer 还原数据后直接复用这个入口。
BurtDirectPBRComponents BurtEvaluateDirectPBRComponents(
    BurtPBRMaterialData materialData,
    BurtPBRGeometryData geometryData,
    BurtPBREnergyTerms energyTerms,
    float directSpecularPerceptualRoughness,
    float3 lightColor,
    float3 lightDirectionWS,
    float shadowAttenuation)
{
    // 创建输出结构体，后面会分别写入 diffuse、specular 和 EnergyPreservation。
    BurtDirectPBRComponents components;

    // 先把输出清零，确保背光或异常输入时不会返回未初始化颜色。
    components.diffuse = float3(0.0f, 0.0f, 0.0f);

    // 同样清零镜面高光输出，方便后续 Debug View 单独显示。
    components.specular = float3(0.0f, 0.0f, 0.0f);

    // 默认让底层 diffuse 完整保留，后面会用统一准备好的 EnergyPreservation 覆盖。
    components.energyPreservation = 1.0f;

    // 先拆出 D / V / F / diffuse lobe 等中间项，Debug View 和光照输出共用同一份计算结果。
    components.brdfTerms = BurtEvaluateDirectBRDFTerms(materialData, geometryData, energyTerms, directSpecularPerceptualRoughness, lightDirectionWS);

    // 保存本次 BRDF 实际使用的保能项，Forward Debug 和未来 Deferred 都从同一份结果读取。
    components.energyPreservation = energyTerms.energyPreservation;

    // 合并灯光可见性；NdotL 控制受光角度，shadowAttenuation 控制阴影。
    float lightVisibility = components.brdfTerms.nDotL * shadowAttenuation;

    // 输出直接漫反射贡献，Debug View 可以直接显示这一项。
    components.diffuse = components.brdfTerms.diffuseBRDF * lightColor * lightVisibility;

    // 输出直接镜面高光贡献，Debug View 可以直接显示这一项。
    components.specular = components.brdfTerms.specularBRDF * lightColor * lightVisibility;

    // 返回拆分后的直接光结果。
    return components;
}

// 计算单个方向光对当前表面的 PBR 直接光贡献，并把漫反射和高光拆开返回。
BurtDirectPBRComponents BurtEvaluateDirectPBRComponents(
    BurtSurfaceData surfaceData,
    float3 lightColor,
    float3 lightDirectionWS,
    float3 normalWS,
    float3 viewDirectionWS,
    float shadowAttenuation)
{
    // 从 SurfaceData 准备材质数据；Deferred 后续可以从 GBuffer 还原同一结构。
    BurtPBRMaterialData materialData = BurtPreparePBRMaterialData(surfaceData);

    // 准备几何数据，统一 NdotV 和 reflection direction 的来源。
    BurtPBRGeometryData geometryData = BurtPreparePBRGeometryData(normalWS, viewDirectionWS);

    // 直接高光的 roughness 单独包含 Specular AA，不回写材质 Base.Roughness。
    float directSpecularPerceptualRoughness = GetDirectSpecularPerceptualRoughness(materialData, geometryData);

    // 一次性准备直接/间接高光补偿和 EnergyPreservation，避免多个光照函数重复查 FG LUT。
    BurtPBREnergyTerms energyTerms = BurtPreparePBREnergyTerms(materialData, geometryData, directSpecularPerceptualRoughness);

    // 复用准备数据版本，保证 Forward 和未来 Deferred 的直接光入口一致。
    return BurtEvaluateDirectPBRComponents(materialData, geometryData, energyTerms, directSpecularPerceptualRoughness, lightColor, lightDirectionWS, shadowAttenuation);
}

// 计算单个方向光对当前表面的 PBR 直接光贡献。
float3 BurtEvaluateDirectPBR(
    BurtSurfaceData surfaceData,
    float3 lightColor,
    float3 lightDirectionWS,
    float3 normalWS,
    float3 viewDirectionWS,
    float shadowAttenuation)
{
    // 复用拆分版本，确保正常渲染和 Debug View 看到的是同一套 BRDF 结果。
    BurtDirectPBRComponents components = BurtEvaluateDirectPBRComponents(surfaceData, lightColor, lightDirectionWS, normalWS, viewDirectionWS, shadowAttenuation);

    // 把直接漫反射和直接高光相加，得到旧接口需要的总直接光。
    return components.diffuse + components.specular;
}

#endif // BURT_BRDF_INCLUDED // 结束 BurtBRDF.hlsl 的 include guard。
