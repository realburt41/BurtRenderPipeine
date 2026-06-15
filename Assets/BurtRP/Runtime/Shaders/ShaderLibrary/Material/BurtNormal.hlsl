// BurtRP 的法线贴图工具库，负责采样切线空间 normal map 并转换到世界空间。
#ifndef BURT_NORMAL_INCLUDED // 开始 include guard，防止同一个 shader 编译单元里重复定义法线工具函数。
#define BURT_NORMAL_INCLUDED // 标记 BurtNormal.hlsl 已经被包含过，后续重复 include 会被跳过。

#include "UnityCG.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl" // 引入 BurtSafeNormalize，用来安全归一化切线、法线和最终结果。

// 声明材质法线贴图，BurtLit 的 Forward pass 会用 mesh UV0 对它进行采样。
Texture2D _NormalMap;
Texture2D _ClearCoatNormalMap;

// 采样材质法线贴图，当前阶段复用 Base Map 的 UV 和 Tiling / Offset。
float4 BurtSampleNormalMap(float2 normalMapUV)
{
    // 返回 normal map 的原始打包值，后续函数会把它解包成切线空间法线。
    return BURT_SAMPLE_TEXTURE2D_REPEAT(_NormalMap, normalMapUV);
}

float4 BurtSampleClearCoatNormalMap(float2 normalMapUV)
{
    return BURT_SAMPLE_TEXTURE2D_REPEAT(_ClearCoatNormalMap, normalMapUV);
}

// 把 Unity normal map 的打包颜色解包成切线空间法线，并应用强度缩放。
float3 BurtUnpackNormalScale(float4 packedNormal, float normalScale)
{
    // 使用 UnityCG.cginc 提供的 UnpackNormal，兼容 Unity normal map 的平台相关编码。
    float3 normalTS = UnpackNormal(packedNormal);

    // 只缩放 xy 扰动分量，z 分量后面重新计算，避免法线长度因为缩放变得不正确。
    normalTS.xy *= normalScale;

    // 根据缩放后的 xy 重建 z，保证切线空间法线仍然尽量保持单位长度。
    normalTS.z = sqrt(saturate(1.0f - dot(normalTS.xy, normalTS.xy)));

    // 返回缩放后的切线空间法线。
    return normalTS;
}

// 把 mesh 提供的模型空间切线转换到世界空间，并保留副切线方向符号。
float4 BurtObjectToWorldTangent(float4 tangentOS)
{
    // 把模型空间切线方向转换到世界空间方向。
    float3 tangentWS = UnityObjectToWorldDir(tangentOS.xyz);

    // 对世界空间切线做安全归一化，避免非等比缩放影响 TBN 矩阵。
    tangentWS = BurtSafeNormalize(tangentWS);

    // Unity 需要把 tangent.w 和 unity_WorldTransformParams.w 相乘来处理负缩放造成的 handedness 变化。
    float tangentSign = tangentOS.w * unity_WorldTransformParams.w;

    // 返回世界空间切线 xyz，并把副切线方向符号放在 w 中传给片元阶段。
    return float4(tangentWS, tangentSign);
}

// 把切线空间法线通过 TBN 矩阵转换成世界空间法线。
float3 BurtApplyDoubleSidedNormalMode(float3 normalTS, float facing, float4 doubleSidedNormalModeConstants)
{
    return facing < 0.0f ? normalTS * doubleSidedNormalModeConstants.xyz : normalTS;
}

float3 BurtTransformTangentToWorld(float3 normalTS, float3 normalWS, float4 tangentWS)
{
    // 对插值后的世界空间法线做安全归一化，作为 TBN 的 N 轴。
    float3 safeNormalWS = BurtSafeNormalize(normalWS);

    // 对插值后的世界空间切线做安全归一化，先得到一个稳定的切线方向。
    float3 safeTangentWS = BurtSafeNormalize(tangentWS.xyz);

    // 从切线中移除法线方向分量，让 T 轴和 N 轴尽量保持正交。
    safeTangentWS = BurtSafeNormalize(safeTangentWS - safeNormalWS * dot(safeNormalWS, safeTangentWS));

    // 通过 N 和 T 的叉乘得到 B 轴，并用 tangent.w 修正左右手坐标系方向。
    float3 bitangentWS = cross(safeNormalWS, safeTangentWS) * tangentWS.w;

    // 构建从切线空间到世界空间的 TBN 矩阵，三行分别代表 T、B、N。
    float3x3 tangentToWorld = float3x3(safeTangentWS, bitangentWS, safeNormalWS);

    // 把切线空间法线转换到世界空间，并安全归一化后返回给光照函数。
    return BurtSafeNormalize(mul(normalTS, tangentToWorld));
}

// 采样 normal map 并直接输出世界空间法线，供 Forward pass 的光照阶段使用。
float3 BurtSampleNormalWS(float2 normalMapUV, float3 normalWS, float4 tangentWS, float normalScale)
{
    if (normalScale <= 0.0f)
    {
        return BurtSafeNormalize(normalWS);
    }

    // 使用当前片元 UV 采样 normal map 的打包颜色。
    float4 packedNormal = BurtSampleNormalMap(normalMapUV);

    // 把打包颜色解成切线空间法线，并应用材质上的 Normal Scale。
    float3 normalTS = BurtUnpackNormalScale(packedNormal, normalScale);

    // 把切线空间法线转换成世界空间法线，返回给 BurtEvaluateSimpleLit。
    return BurtTransformTangentToWorld(normalTS, normalWS, tangentWS);
}

float3 BurtSampleNormalWS(float2 normalMapUV, float3 normalWS, float4 tangentWS, float normalScale, float facing, float4 doubleSidedNormalModeConstants)
{
    if (normalScale <= 0.0f)
    {
        float3 normalTS = BurtApplyDoubleSidedNormalMode(float3(0.0f, 0.0f, 1.0f), facing, doubleSidedNormalModeConstants);
        return BurtTransformTangentToWorld(normalTS, normalWS, tangentWS);
    }

    float4 packedNormal = BurtSampleNormalMap(normalMapUV);
    float3 normalTS = BurtUnpackNormalScale(packedNormal, normalScale);
    normalTS = BurtApplyDoubleSidedNormalMode(normalTS, facing, doubleSidedNormalModeConstants);
    return BurtTransformTangentToWorld(normalTS, normalWS, tangentWS);
}

float3 BurtSampleClearCoatNormalWS(float2 normalMapUV, float3 normalWS, float4 tangentWS, float normalScale, float facing, float4 doubleSidedNormalModeConstants)
{
    if (normalScale <= 0.0f)
    {
        float3 normalTS = BurtApplyDoubleSidedNormalMode(float3(0.0f, 0.0f, 1.0f), facing, doubleSidedNormalModeConstants);
        return BurtTransformTangentToWorld(normalTS, normalWS, tangentWS);
    }

    float4 packedNormal = BurtSampleClearCoatNormalMap(normalMapUV);
    float3 normalTS = BurtUnpackNormalScale(packedNormal, normalScale);
    normalTS = BurtApplyDoubleSidedNormalMode(normalTS, facing, doubleSidedNormalModeConstants);
    return BurtTransformTangentToWorld(normalTS, normalWS, tangentWS);
}

#endif // BURT_NORMAL_INCLUDED // 结束 BurtNormal.hlsl 的 include guard。
