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

// 声明 BurtRP 预积分 FG LUT；C# 会绑定 Assets/Textures/PreintegratedFG.exr 或关闭开关走解析近似。
sampler2D _BurtPreIntegratedFG;
float _BurtPreIntegratedFGEnabled;

// 定义预积分 FG LUT 的尺寸；当前资源按 XRender 默认 128x128 生成。
static const float BURT_PREINTEGRATED_FG_LUT_SIZE = 128.0f;
static const float BURT_PREINTEGRATED_FG_LUT_INV_SIZE = 1.0f / BURT_PREINTEGRATED_FG_LUT_SIZE;

// 对标 XRender 的 rcp_safe，用一个下限保护倒数，避免 BRDF 分母为 0 产生 NaN。
float BurtRcpSafe(float value)
{
    // 先把输入夹到一个很小的正数以上，再取倒数，保证返回值稳定。
    return rcp(max(value, BURT_EPSILON));
}

// 计算五次方，Schlick Fresnel 和 DFG 近似都会用到 (1 - cos)^5。
float BurtPow5(float value)
{
    // 先算平方，减少重复乘法，也避免直接 pow 带来的平台差异。
    float value2 = value * value;

    // value^5 = value^2 * value^2 * value，正好对应 Schlick 的五次项。
    return value2 * value2 * value;
}

// 返回 PBR 使用的漫反射颜色，金属材质不会保留普通 diffuse。
float3 BurtBRDFDiffuseColor(BurtSurfaceData surfaceData)
{
    // metallic 越高，baseColor 越多转移到 specular F0，diffuse 越少。
    return surfaceData.baseColor.rgb * (1.0f - surfaceData.metallic);
}

// 把 XRender 风格 reflectance 映射成非金属 F0；reflectance=0.5 时得到 0.04。
float BurtDielectricReflectanceToF0(float reflectance)
{
    // XRender 的公式是 MATERIAL_MAX_DIELECTRIC_F0 * Reflectance^2。
    return BURT_MATERIAL_MAX_DIELECTRIC_F0 * saturate(reflectance) * saturate(reflectance);
}

// 返回 PBR 内部使用的 F0 反射颜色；材质面板只暴露 reflectance，不直接暴露 F0。
float3 BurtBRDFSpecularF0(BurtSurfaceData surfaceData)
{
    // 非金属 F0 由 reflectance 映射得到，避免让美术直接编辑 F0 颜色。
    float dielectricF0 = BurtDielectricReflectanceToF0(surfaceData.reflectance);

    // 金属材质的 F0 来自 baseColor，非金属材质的 F0 来自 reflectance 计算结果。
    return lerp(float3(dielectricF0, dielectricF0, dielectricF0), surfaceData.baseColor.rgb, surfaceData.metallic);
}

// 把 smoothness 转成感知 roughness，XRender 的 Base.Roughness 也是这层语义。
float BurtBRDFRoughness(BurtSurfaceData surfaceData)
{
    // smoothness 越高 roughness 越低，并保留一个下限避免除零和过亮高光。
    return max(1.0f - surfaceData.smoothness, BURT_MIN_PERCEPTUAL_ROUGHNESS);
}

// 根据世界空间法线变化估算几何/法线导致的高光方差，参考 XRender CommonMaterial 的 GeometricNormalVariance。
float BurtSpecularAANormalVariance(float3 normalWS)
{
    // 先安全归一化法线，避免长度误差放大 ddx/ddy 的方差。
    float3 safeNormalWS = BurtSafeNormalize(normalWS);

    // 计算当前像素在屏幕 x 方向的法线变化，导数越大表示高光越容易闪烁或漏采样。
    float3 deltaX = ddx(safeNormalWS);

    // 计算当前像素在屏幕 y 方向的法线变化，和 x 方向一起描述一个像素覆盖范围内的法线分布。
    float3 deltaY = ddy(safeNormalWS);

    // 把两个方向的变化量转成方差，并乘以屏幕空间权重，得到 XRender 同类过滤使用的输入量。
    return BURT_SPECULAR_AA_SCREEN_SPACE_VARIANCE * (dot(deltaX, deltaX) + dot(deltaY, deltaY));
}

// 把法线方差折算进感知粗糙度，解决极高 smoothness 时高光太窄导致看起来反而变弱的问题。
float BurtApplySpecularAA(float perceptualRoughness, float3 normalWS)
{
    // 估算法线在一个像素覆盖范围内的变化量，曲面或法线贴图变化越大，这个值越高。
    float variance = BurtSpecularAANormalVariance(normalWS);

    // 先把感知粗糙度平方，再加入受阈值限制的方差项，对齐 XRender NormalFiltering 的思路。
    float squaredRoughness = saturate(perceptualRoughness * perceptualRoughness + min(2.0f * variance, BURT_SPECULAR_AA_THRESHOLD * BURT_SPECULAR_AA_THRESHOLD));

    // 开平方回到感知粗糙度空间，材质滑块和后续 DFG 仍然使用同一层语义。
    float filteredRoughness = sqrt(squaredRoughness);

    // 只允许过滤增加粗糙度，不允许把材质本身变得更光滑。
    return max(perceptualRoughness, filteredRoughness);
}

// 返回直接高光实际使用的感知粗糙度，当前会把材质粗糙度和 Specular AA 合并。
float BurtBRDFDirectSpecularRoughness(BurtSurfaceData surfaceData, float3 normalWS)
{
    // 先取得材质本身的感知粗糙度，也就是 1 - smoothness 后的结果。
    float materialRoughness = BurtBRDFRoughness(surfaceData);

    // 再把屏幕空间法线变化折算进去，避免极光滑材质的高光小到被像素漏采样。
    return BurtApplySpecularAA(materialRoughness, normalWS);
}

// 把感知 roughness 转成线性 roughness，也就是 XRender 里常说的 LinearRoughness。
float BurtBRDFLinearRoughness(float perceptualRoughness)
{
    // 线性 roughness 使用感知 roughness 的平方，让材质滑块在视觉上更均匀。
    return max(perceptualRoughness * perceptualRoughness, BURT_EPSILON);
}

// 把线性 roughness 转成 GGX D 项使用的 A2，等价于感知 roughness 的四次方。
float BurtBRDFA2(float linearRoughness)
{
    // A2 太小会让高光尖峰过高，所以用 BURT_EPSILON 做一个最低保护。
    return max(linearRoughness * linearRoughness, BURT_EPSILON);
}

// 按 XRender / UE 的做法推导 F90，过低的 F0 会被视作阴影而不是真实反射。
float3 BurtBRDFF90(float3 f0)
{
    // XRender 的 F_Schlick_UE 会用 50 * F0.g 限制极低反射率的掠射亮度。
    float f90 = saturate(50.0f * f0.g);

    // 返回 RGB 三通道一致的 F90，方便传给 Schlick Fresnel。
    return float3(f90, f90, f90);
}

// 计算带 F90 的 Schlick Fresnel，和 XRender 的 F_Schlick(F0, F90, u) 对齐。
float3 BurtFresnelSchlickF90(float cosTheta, float3 f0, float3 f90)
{
    // 先把角度项限制到 0 到 1，再转成 1 - cosTheta。
    float oneMinusCos = 1.0f - saturate(cosTheta);

    // 返回从 F0 到 F90 的角度相关反射率，掠射角会更接近 F90。
    return f0 + (f90 - f0) * BurtPow5(oneMinusCos);
}

// 计算 Schlick Fresnel，保留旧调用接口，默认 F90 为 1。
float3 BurtFresnelSchlick(float cosTheta, float3 f0)
{
    // 旧接口等价于常见的 f0 -> 1 的 Schlick 近似。
    return BurtFresnelSchlickF90(cosTheta, f0, float3(1.0f, 1.0f, 1.0f));
}

// 计算 XRender 使用的 GGX/Trowbridge-Reitz 法线分布项 D。
float BurtDistributionGGXFromA2(float a2, float nDotH)
{
    // 这是 XRender D_GGX 的核心分母项：((NoH * A2 - NoH) * NoH + 1)。
    float denom = (nDotH * a2 - nDotH) * nDotH + 1.0f;

    // 返回 D 项，A2 已经由 roughness 预先算好，避免函数内部混淆 roughness 语义。
    return a2 / max(BURT_PI * denom * denom, BURT_EPSILON);
}

// 计算 GGX/Trowbridge-Reitz 法线分布项 D，保留旧接口方便其它代码继续调用。
float BurtDistributionGGX(float nDotH, float roughness)
{
    // 先把感知 roughness 转成线性 roughness，再转成 GGX 的 A2。
    float a2 = BurtBRDFA2(BurtBRDFLinearRoughness(roughness));

    // 复用 XRender 风格的 D_GGX 实现。
    return BurtDistributionGGXFromA2(a2, nDotH);
}

// 计算 XRender 常用的 Smith Joint Approx 可见性项，返回的是 G / (4 * NoV * NoL)。
float BurtVisibilitySmithJointApprox(float linearRoughness, float nDotV, float nDotL)
{
    // 计算视线侧遮蔽对光照方向的影响，公式来自 XRender 的 Vis_SmithJointApprox。
    float visibilityV = nDotL * (nDotV * (1.0f - linearRoughness) + linearRoughness);

    // 计算光照侧遮蔽对视线方向的影响，和上面一项组成 joint visibility。
    float visibilityL = nDotV * (nDotL * (1.0f - linearRoughness) + linearRoughness);

    // 乘 0.5 后再除以两项之和；BurtRcpSafe 负责保护极小分母。
    return 0.5f * BurtRcpSafe(visibilityV + visibilityL);
}

// 计算 XRender / Lazarov 风格的预积分 DFG 近似，用于 IBL 间接高光。
float2 BurtPrefilteredDFGApprox(float roughness, float nDotV)
{
    // c0 是经验拟合系数，XRender 的 PrefilteredDFG_Approx 也使用这组值。
    const float4 c0 = float4(-1.0f, -0.0275f, -0.572f, 0.022f);

    // c1 是经验拟合系数，用来把 roughness 映射到 DFG 的 AB 两项。
    const float4 c1 = float4(1.0f, 0.0425f, 1.04f, -0.04f);

    // 根据 roughness 插值得到中间拟合参数。
    float4 r = roughness * c0 + c1;

    // a004 负责拟合视角相关的能量变化，NoV 越小掠射角效果越强。
    float a004 = min(r.x * r.x, exp2(-9.28f * nDotV)) * r.x + r.y;

    // AB 分别对应 F0 和 F90 的权重，后面会组合成环境 BRDF。
    return float2(-1.04f, 1.04f) * a004 + r.zw;
}

// 把 0..1 的 LUT 坐标移动到半 texel 中心，避免采到边缘外推值。
float2 BurtRemapPreIntegratedFGUV(float2 uv)
{
    // 对齐 XRender Remap01CoordToHalfTexelCoord，保证 128x128 LUT 的首尾采样落在 texel 中心。
    return uv * (1.0f - BURT_PREINTEGRATED_FG_LUT_INV_SIZE) + 0.5f * BURT_PREINTEGRATED_FG_LUT_INV_SIZE;
}

// 采样预积分 FG LUT，RGB 分别保存 DFG.x、DFG.y 和能量补偿使用的单次散射能量 Z。
float3 BurtSamplePreIntegratedFG(float roughness, float nDotV)
{
    // XRender 使用 float2(NdotV, 1 - PerceptualRoughness) 作为 LUT 坐标。
    float2 uv = float2(saturate(nDotV), 1.0f - saturate(roughness));
    uv = BurtRemapPreIntegratedFGUV(uv);
    return tex2D(_BurtPreIntegratedFG, uv).rgb;
}

// 读取预积分 FG；没有绑定 LUT 时回退到已有解析 DFG，保证材质仍可渲染。
float3 BurtEvaluatePreIntegratedFG(float roughness, float nDotV)
{
    // 解析近似只提供 DFG.xy，因此用 DFG.x + DFG.y 近似能量补偿项，作为无 LUT 的安全 fallback。
    float2 approxDFG = BurtPrefilteredDFGApprox(roughness, nDotV);
    float approxEnergy = clamp(approxDFG.x + approxDFG.y, 0.001f, 1.0f);
    float3 approxFG = float3(approxDFG, approxEnergy);

    // 当 C# 确认 LUT 已绑定时使用贴图结果，否则完全使用解析近似。
    float3 lutFG = BurtSamplePreIntegratedFG(roughness, nDotV);
    return lerp(approxFG, lutFG, saturate(_BurtPreIntegratedFGEnabled));
}

// 返回环境 BRDF 使用的 DFG.xy；优先使用 PreintegratedFG.exr，回退到解析近似。
float2 BurtEvaluateSpecularDFGTerms(float roughness, float nDotV)
{
    // LUT 的 xy 对应 XRender 注释里的 ibl brdf 两项，后续会分别乘 F0 和 F90。
    return BurtEvaluatePreIntegratedFG(roughness, nDotV).xy;
}

// 把 DFG 的 AB 两项应用到 F0/F90 上，得到环境镜面反射的 BRDF 权重。
float3 BurtEvaluateSpecularDFG(float3 f0, float3 f90, float2 dfg)
{
    // XRender 的 EvalSpecularDFG 也是 F0 * A + F90 * B。
    return f0 * dfg.x + f90 * dfg.y;
}

// 参考 XRender ComputeEnergyCompensation，用 LUT.z 的单次散射能量计算多次散射补偿。
float3 BurtComputeSpecularEnergyCompensation(float3 f0, float roughness, float nDotV)
{
    // PreintegratedFG.z 对应 XRender Energy.z；没有 LUT 时由解析近似提供保守 fallback。
    float singleScatterEnergy = clamp(BurtEvaluatePreIntegratedFG(roughness, nDotV).z, 0.001f, 1.0f);

    // XRender 公式：1 + F0 * (1 / Z - 1)，Z 越低说明单次散射漏能越多，需要越多补偿。
    return 1.0f + f0 * (BurtRcpSafe(singleScatterEnergy) - 1.0f);
}

// 参考 XRender 的 AO 高光遮蔽公式。
float BurtComputeSpecularOcclusionFromAO(float nDotV, float ao, float linearRoughness)
{
    // 粗糙度越高，AO 对高光遮蔽越平滑。
    float exponent = exp2(-16.0f * saturate(linearRoughness) - 1.0f);
    return saturate(pow(max(saturate(nDotV) + saturate(ao), BURT_EPSILON), exponent) - 1.0f + saturate(ao));
}

// 使用感知粗糙度，内部转线性粗糙度。
float BurtComputeIndirectSpecularOcclusion(float nDotV, float ao, float perceptualRoughness)
{
    // 和 DFG/GGX 的粗糙度语义保持一致。
    return BurtComputeSpecularOcclusionFromAO(nDotV, ao, BurtBRDFLinearRoughness(perceptualRoughness));
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
    float materialRoughness = BurtBRDFRoughness(surfaceData);

    // 对直接高光应用 Specular AA；这会在极光滑材质上适度拓宽高光，避免高光被像素漏采样。
    float roughness = BurtApplySpecularAA(materialRoughness, n);

    // 把感知 roughness 转为线性 roughness，XRender 的 Smith Joint Approx 使用这层参数。
    float linearRoughness = BurtBRDFLinearRoughness(roughness);

    // 计算 GGX D 项使用的 A2，也就是感知 roughness 的四次方。
    float a2 = BurtBRDFA2(linearRoughness);

    // 计算材质漫反射颜色，金属材质会自然削弱或移除 diffuse。
    float3 diffuseColor = BurtBRDFDiffuseColor(surfaceData);

    // 计算材质 F0，非金属来自 0.04 倍率，金属来自 baseColor。
    float3 f0 = BurtBRDFSpecularF0(surfaceData);

    // 计算材质 F90，参考 XRender 的 F_Schlick_UE 处理极低 F0 的方式。
    float3 f90 = BurtBRDFF90(f0);

    // 用 XRender 的 D_GGX 形式计算法线分布项，控制高光形状。
    float d = BurtDistributionGGXFromA2(a2, nDotH);

    // 用 XRender 的 Vis_SmithJointApprox 计算可见性项，它已经包含 4NoVNoL 分母。
    float visibility = BurtVisibilitySmithJointApprox(linearRoughness, nDotV, nDotL);

    // 用带 F90 的 Schlick Fresnel 计算视角相关的高光颜色。
    float3 f = BurtFresnelSchlickF90(vDotH, f0, f90);

    // 根据 Fresnel 得到 specular 能量比例。
    float3 kS = f;

    // 剩余能量给 diffuse；金属度已经在 diffuseColor 中扣除，避免重复乘 (1 - metallic)。
    float3 kD = (1.0f - kS);

    // XRender 的 specular lobe 是 D * V * F，其中 V 已经是 G / (4NoVNoL)。
    float3 specularBRDF = d * visibility * f;

    // 对齐 XRender 直接高光能量补偿，粗糙材质会补回 GGX 单次散射损失的高光能量。
    float3 energyCompensation = BurtComputeSpecularEnergyCompensation(f0, roughness, nDotV);
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
