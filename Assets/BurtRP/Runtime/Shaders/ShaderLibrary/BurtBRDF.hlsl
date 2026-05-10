// BurtRP 的 PBR BRDF 工具库，当前实现单主光也能复用，后续多光源会继续调用这里的函数。
#ifndef BURT_BRDF_INCLUDED // 开始 include guard，防止同一个 shader 编译单元里重复定义 BRDF 函数。
#define BURT_BRDF_INCLUDED // 标记 BurtBRDF.hlsl 已经被包含过，后续重复 include 会被跳过。

#include "BurtCommon.hlsl" // 引入安全归一化和基础数学保护。
#include "BurtInput.hlsl" // 引入 BurtSurfaceData，用来读取 baseColor、metallic、smoothness 等材质参数。

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

// BurtRP 直接高光适配函数：材质粗糙度 + XRender 几何法线过滤。
float GetDirectSpecularPerceptualRoughness(BurtSurfaceData surfaceData, float3 normalWS)
{
    // 先取得材质本身的感知粗糙度，也就是 1 - smoothness 后的结果。
    float materialRoughness = GetSurfacePerceptualRoughness(surfaceData);

    // 再把屏幕空间法线变化折算进去，避免极光滑材质的高光小到被像素漏采样。
    return GeometricNormalFiltering(materialRoughness, normalWS, BURT_SPECULAR_AA_SCREEN_SPACE_VARIANCE, BURT_SPECULAR_AA_THRESHOLD);
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

// BurtRP 适配函数：从预积分 FG 中取 Energy.z，再调用 XRender 原名 ComputeEnergyCompensation。
float3 GetSpecularEnergyCompensation(float3 f0, float perceptualRoughness, float clampedNdotV)
{
    // PreintegratedFG.z 对应 XRender Energy.z；没有 LUT 时由解析近似提供保守 fallback。
    float singleScatterEnergy = clamp(GetPreIntegratedFGOrApprox(perceptualRoughness, clampedNdotV).z, 0.001f, 1.0f);

    // 使用 XRender 原函数名计算多次散射补偿，方便从调用点溯源。
    return ComputeEnergyCompensation(f0, singleScatterEnergy);
}

// 出处：XRender/Shaders/Library/CommonMaterial.hlsl::GetSpecularOcclusionFromAmbientOcclusion；HDRP/Frostbite AO 高光遮蔽。
float GetSpecularOcclusionFromAmbientOcclusion(float noV, float ao, float linearRoughness)
{
    // 粗糙度越高，AO 对高光遮蔽越平滑。
    float exponent = exp2(-16.0f * saturate(linearRoughness) - 1.0f);
    return saturate(pow(max(saturate(noV) + saturate(ao), BURT_EPSILON), exponent) - 1.0f + saturate(ao));
}

// BurtRP 适配函数：间接高光遮蔽输入使用感知粗糙度，内部转 XRender 的 LinearRoughness。
float GetIndirectSpecularOcclusion(float noV, float ao, float perceptualRoughness)
{
    // 和 DFG/GGX 的粗糙度语义保持一致。
    return GetSpecularOcclusionFromAmbientOcclusion(noV, ao, PerceptualRoughnessToLinearRoughness(perceptualRoughness));
}
// 保存直接 PBR 光照拆分结果，方便正常渲染和 Debug View 共用同一套 BRDF 计算。

struct BurtDirectPBRComponents
{
    // 保存直接漫反射最终贡献，已经包含灯光颜色、NdotL 和阴影衰减。
    float3 diffuse;

    // 保存直接镜面高光最终贡献，已经包含灯光颜色、NdotL 和阴影衰减。
    float3 specular;
};

// 计算单个方向光对当前表面的 PBR 直接光贡献，并把漫反射和高光拆开返回。
BurtDirectPBRComponents BurtEvaluateDirectPBRComponents(
    BurtSurfaceData surfaceData,
    float3 lightColor,
    float3 lightDirectionWS,
    float3 normalWS,
    float3 viewDirectionWS,
    float shadowAttenuation)
{
    // 创建输出结构体，后面会分别写入 diffuse 和 specular。
    BurtDirectPBRComponents components;

    // 先把输出清零，确保背光或异常输入时不会返回未初始化颜色。
    components.diffuse = float3(0.0f, 0.0f, 0.0f);

    // 同样清零镜面高光输出，方便后续 Debug View 单独显示。
    components.specular = float3(0.0f, 0.0f, 0.0f);

    // 归一化所有方向，避免插值或上传误差影响 BRDF。
    float3 n = BurtSafeNormalize(normalWS);

    // 归一化灯光方向，当前约定它是从表面指向光源。
    float3 l = BurtSafeNormalize(lightDirectionWS);

    // 归一化视线方向，当前约定它是从表面指向相机。
    float3 v = BurtSafeNormalize(viewDirectionWS);

    // 半角向量用于 Fresnel、D 项和高光形状。
    float3 h = BurtSafeNormalize(l + v);

    // 计算光照方向和法线夹角，控制直接光是否照到表面。
    float nDotL = saturate(dot(n, l));

    // 计算视线方向和法线夹角，控制 Fresnel 和 Smith 可见性。
    float nDotV = saturate(dot(n, v));

    // 计算半角向量和法线夹角，控制 GGX 高光峰值位置。
    float nDotH = saturate(dot(n, h));

    // 计算视线方向和半角向量夹角，作为 Schlick Fresnel 的输入。
    float vDotH = saturate(dot(v, h));

    // 计算材质原始感知 roughness，Debug View 会用它检查 smoothness 到 roughness 的转换。
    float materialRoughness = GetSurfacePerceptualRoughness(surfaceData);

    // 对直接高光应用 Specular AA；这会在极光滑材质上适度拓宽高光，避免高光被像素漏采样。
    float roughness = GeometricNormalFiltering(materialRoughness, n, BURT_SPECULAR_AA_SCREEN_SPACE_VARIANCE, BURT_SPECULAR_AA_THRESHOLD);

    // 把感知 roughness 转为线性 roughness，XRender 的 Smith Joint Approx 使用这层参数。
    float linearRoughness = PerceptualRoughnessToLinearRoughness(roughness);

    // 计算 GGX D 项使用的 A2，也就是感知 roughness 的四次方。
    float a2 = LinearRoughnessToA2(linearRoughness);

    // 计算材质漫反射颜色，金属材质会自然削弱或移除 diffuse。
    float3 diffuseColor = DiffuseColorFromBaseColor(surfaceData.baseColor.rgb, surfaceData.metallic);

    // 计算材质 F0，非金属来自 0.04 倍率，金属来自 baseColor。
    float3 f0 = DielectricReflectanceToF0(surfaceData.baseColor.rgb, surfaceData.reflectance, surfaceData.metallic);

    // 计算材质 F90，使用 XRender CommonMaterial.hlsl::ApproximateF90 的默认掠射角端点。
    float3 f90 = ApproximateF90(f0);

    // 用 XRender 的 D_GGX 形式计算法线分布项，控制高光形状。
    float d = D_GGX(a2, nDotH);

    // 用 XRender 的 Vis_SmithJointApprox 计算可见性项，它已经包含 4NoVNoL 分母。
    float visibility = Vis_SmithJointApprox(linearRoughness, nDotV, nDotL);

    // 用带 F90 的 Schlick Fresnel 计算视角相关的高光颜色。
    float3 f = F_Schlick_UE(f0, f90, vDotH);

    // 根据 Fresnel 得到 specular 能量比例。
    float3 kS = f;

    // 剩余能量给 diffuse；金属度已经在 diffuseColor 中扣除，避免重复乘 (1 - metallic)。
    float3 kD = (1.0f - kS);

    // XRender 的 specular lobe 是 D * V * F，其中 V 已经是 G / (4NoVNoL)。
    float3 specularBRDF = d * visibility * f;

    // 对齐 XRender 直接高光能量补偿，粗糙材质会补回 GGX 单次散射损失的高光能量。
    float3 energyCompensation = GetSpecularEnergyCompensation(f0, roughness, nDotV);
    specularBRDF *= energyCompensation;

    // 计算 Lambert diffuse BRDF，使用预先定义好的 PI 倒数减少一次除法。
    float3 diffuseBRDF = kD * diffuseColor * BURT_INV_PI;

    // 合并灯光可见性；NdotL 控制受光角度，shadowAttenuation 控制阴影。
    float lightVisibility = nDotL * shadowAttenuation;

    // 输出直接漫反射贡献，Debug View 可以直接显示这一项。
    components.diffuse = diffuseBRDF * lightColor * lightVisibility;

    // 输出直接镜面高光贡献，Debug View 可以直接显示这一项。
    components.specular = specularBRDF * lightColor * lightVisibility;

    // 返回拆分后的直接光结果。
    return components;
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
