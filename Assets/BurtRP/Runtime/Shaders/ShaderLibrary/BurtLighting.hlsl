// BurtRP 的基础光照工具库，目前实现 Ambient + 单主光 Lambert 的简单 Lit 模型。
#ifndef BURT_LIGHTING_INCLUDED // 开始 include guard，防止同一个 shader 编译单元里重复定义光照函数。
#define BURT_LIGHTING_INCLUDED // 标记 BurtLighting.hlsl 已经被包含过，后续重复 include 会被跳过。

#include "BurtCommon.hlsl" // 引入安全数学函数，例如 BurtSafeNormalize。
#include "BurtInput.hlsl" // 引入 BurtSurfaceData，用来读取材质基础色和 alpha。

// 保存从当前着色点指向主方向光的世界空间方向，由 Burt Setup Lighting Pass 上传。
float4 _BurtMainLightDirection;

// 保存主方向光颜色，由 Burt Setup Lighting Pass 从 Unity 可见光数据中上传。
float4 _BurtMainLightColor;

// 保存环境光颜色，由 Burt Setup Lighting Pass 从场景环境光中上传。
float4 _BurtAmbientLightColor;

// 保存 BurtRP 当前光照函数需要的一盏灯的数据。
struct BurtLight
{
    // 保存从表面点指向灯光的世界空间单位方向。
    float3 directionWS;

    // 保存灯光 RGB 颜色，用于直接漫反射计算。
    float3 color;

    // 保存阴影可见性，1 表示完全受光，0 表示完全在阴影中。
    float shadowAttenuation;
};

// 根据 BurtRP 的全局 shader 变量创建当前主光数据。
BurtLight BurtCreateMainLight(float shadowAttenuation)
{
    // 创建一个输出光源结构体，下面逐项填充。
    BurtLight light;

    // 对上传的主光方向做安全归一化，避免方向长度影响 Lambert 结果。
    light.directionWS = BurtSafeNormalize(_BurtMainLightDirection.xyz);

    // 拷贝主光颜色的 RGB 部分，忽略 alpha。
    light.color = _BurtMainLightColor.rgb;

    // 保存当前片元采样得到的阴影衰减值。
    light.shadowAttenuation = shadowAttenuation;

    // 返回填充完成的主光数据。
    return light;
}

// 获取 BurtRP 当前简单光照模型使用的环境光颜色。
float3 BurtGetAmbientLightColor()
{
    // 返回 C# 上传的环境光 RGB 值，目前来自场景的 ambient light。
    return _BurtAmbientLightColor.rgb;
}

// 计算经典 Lambert 漫反射项。
float BurtLambert(float3 normalWS, float3 lightDirectionWS)
{
    // 点乘结果限制在 0 到 1，避免背光面产生负光照。
    return saturate(dot(normalWS, lightDirectionWS));
}

// 计算一盏 BurtLight 对表面的直接漫反射贡献。
float3 BurtEvaluateDiffuse(float3 baseColor, BurtLight light, float3 normalWS)
{
    // 计算 N dot L，表示表面朝向光源的程度。
    float diffuseTerm = BurtLambert(normalWS, light.directionWS);

    // 把 albedo、灯光颜色、Lambert 项和阴影衰减相乘，得到直接漫反射颜色。
    return baseColor * light.color * diffuseTerm * light.shadowAttenuation;
}

// 计算当前简单 Lit 模型的环境光部分。
float3 BurtEvaluateAmbient(float3 baseColor, float3 ambientColor)
{
    // 用 albedo 乘环境光颜色，让阴影面或背光面仍然有基础可见度。
    return baseColor * ambientColor;
}

// 计算 BurtRP 当前的完整简单 Lit 模型：环境光 + 一个带阴影的 Lambert 主光。
float3 BurtEvaluateSimpleLit(BurtSurfaceData surfaceData, BurtLight mainLight, float3 normalWS)
{
    // 使用材质基础色和全局环境光计算 ambient 部分。
    float3 ambientColor = BurtEvaluateAmbient(surfaceData.baseColor.rgb, BurtGetAmbientLightColor());

    // 使用材质基础色、主光数据和法线计算 direct diffuse 部分。
    float3 diffuseColor = BurtEvaluateDiffuse(surfaceData.baseColor.rgb, mainLight, normalWS);

    // 返回环境光和直接光相加后的最终 RGB。
    return ambientColor + diffuseColor;
}

#endif // BURT_LIGHTING_INCLUDED // 结束 BurtLighting.hlsl 的 include guard。
