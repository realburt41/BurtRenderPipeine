// BurtRP 的 PBR BRDF 工具库，当前实现单主光也能复用，后续多光源会继续调用这里的函数。
#ifndef BURT_BRDF_INCLUDED // 开始 include guard，防止同一个 shader 编译单元里重复定义 BRDF 函数。
#define BURT_BRDF_INCLUDED // 标记 BurtBRDF.hlsl 已经被包含过，后续重复 include 会被跳过。

#include "BurtCommon.hlsl" // 引入安全归一化和基础数学保护。
#include "BurtInput.hlsl" // 引入 BurtSurfaceData，用来读取 baseColor、metallic、smoothness 等材质参数。

// 定义圆周率，PBR 漫反射需要除以 PI。
static const float BURT_PI = 3.14159265359f;

// 定义非金属材质的基础反射率，0.04 是常见介质 F0 近似值。
static const float3 BURT_DIELECTRIC_SPECULAR = float3(0.04f, 0.04f, 0.04f);

// 返回 PBR 使用的漫反射颜色，金属材质不会保留普通 diffuse。
float3 BurtBRDFDiffuseColor(BurtSurfaceData surfaceData)
{
    // metallic 越高，baseColor 越多转移到 specular F0，diffuse 越少。
    return surfaceData.baseColor.rgb * (1.0f - surfaceData.metallic);
}

// 返回 PBR 使用的 F0 反射颜色。
float3 BurtBRDFSpecularF0(BurtSurfaceData surfaceData)
{
    // 使用 Specular Color 作为非金属 F0 的美术倍率，避免旧材质的高光颜色完全失效。
    float3 dielectricF0 = BURT_DIELECTRIC_SPECULAR * max(surfaceData.specularColor, 0.0f);

    // 金属材质的 F0 来自 baseColor，非金属材质的 F0 来自介质反射率。
    return lerp(dielectricF0, surfaceData.baseColor.rgb, surfaceData.metallic);
}

// 把 smoothness 转成 GGX 使用的 roughness。
float BurtBRDFRoughness(BurtSurfaceData surfaceData)
{
    // smoothness 越高 roughness 越低，并保留一个下限避免除零和过亮高光。
    return max(1.0f - surfaceData.smoothness, 0.045f);
}

// 计算 Schlick Fresnel，描述掠射角反射增强。
float3 BurtFresnelSchlick(float cosTheta, float3 f0)
{
    // saturate 保护 cosTheta，pow5 是 Schlick 近似的核心。
    float oneMinusCos = 1.0f - saturate(cosTheta);

    // 返回从 F0 到 1 的角度相关反射率。
    return f0 + (1.0f - f0) * pow(oneMinusCos, 5.0f);
}

// 计算 GGX/Trowbridge-Reitz 法线分布项 D。
float BurtDistributionGGX(float nDotH, float roughness)
{
    // 把 roughness 平方作为 alpha，符合常见 GGX 实现。
    float alpha = roughness * roughness;

    // 预先计算 alpha 平方，减少重复乘法。
    float alpha2 = alpha * alpha;

    // 计算 GGX 分母中的核心项。
    float denom = nDotH * nDotH * (alpha2 - 1.0f) + 1.0f;

    // 返回 D 项，并给分母加一个小值避免除零。
    return alpha2 / max(BURT_PI * denom * denom, 0.000001f);
}

// 计算 Schlick-GGX 的单方向几何遮蔽项。
float BurtGeometrySchlickGGX(float nDotV, float roughness)
{
    // 直接光常用 k = (roughness + 1)^2 / 8。
    float k = (roughness + 1.0f);
    k = (k * k) * 0.125f;

    // 返回当前方向的几何可见性。
    return nDotV / max(nDotV * (1.0f - k) + k, 0.000001f);
}

// 计算 Smith 几何项 G，同时考虑视线方向和光照方向。
float BurtGeometrySmith(float nDotV, float nDotL, float roughness)
{
    // 计算视线方向的遮蔽。
    float ggxV = BurtGeometrySchlickGGX(nDotV, roughness);

    // 计算光照方向的遮蔽。
    float ggxL = BurtGeometrySchlickGGX(nDotL, roughness);

    // 两个方向相乘得到最终 G 项。
    return ggxV * ggxL;
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
    // 归一化所有方向，避免插值或上传误差影响 BRDF。
    float3 n = BurtSafeNormalize(normalWS);
    float3 l = BurtSafeNormalize(lightDirectionWS);
    float3 v = BurtSafeNormalize(viewDirectionWS);

    // 半角向量用于 Fresnel、D 项和高光形状。
    float3 h = BurtSafeNormalize(l + v);

    // 计算 BRDF 常用角度项。
    float nDotL = saturate(dot(n, l));
    float nDotV = saturate(dot(n, v));
    float nDotH = saturate(dot(n, h));
    float vDotH = saturate(dot(v, h));

    // 计算材质 PBR 参数。
    float roughness = BurtBRDFRoughness(surfaceData);
    float3 diffuseColor = BurtBRDFDiffuseColor(surfaceData);
    float3 f0 = BurtBRDFSpecularF0(surfaceData);

    // 计算 Cook-Torrance 的 D、G、F 三项。
    float d = BurtDistributionGGX(nDotH, roughness);
    float g = BurtGeometrySmith(nDotV, nDotL, roughness);
    float3 f = BurtFresnelSchlick(vDotH, f0);

    // 根据 Fresnel 得到 specular 能量比例。
    float3 kS = f;

    // 剩余能量给 diffuse；金属度已经在 diffuseColor 中扣除，避免重复乘 (1 - metallic)。
    float3 kD = (1.0f - kS);

    // 计算 Cook-Torrance specular 分母，并加小值防止除零。
    float specularDenom = max(4.0f * nDotV * nDotL, 0.000001f);

    // 计算 specular BRDF。
    float3 specularBRDF = (d * g * f) / specularDenom;

    // 计算 Lambert diffuse BRDF。
    float3 diffuseBRDF = kD * diffuseColor / BURT_PI;

    // 把 BRDF、灯光颜色、NdotL 和阴影衰减合成最终直接光。
    return (diffuseBRDF + specularBRDF) * lightColor * nDotL * shadowAttenuation;
}

#endif // BURT_BRDF_INCLUDED // 结束 BurtBRDF.hlsl 的 include guard。
