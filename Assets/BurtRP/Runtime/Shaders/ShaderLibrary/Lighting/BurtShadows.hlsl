// BurtRP 的主光阴影接收工具库，负责把世界坐标转换到 shadow map 并采样阴影。
#ifndef BURT_SHADOWS_INCLUDED // 开始 include guard，防止同一个 shader 编译单元里重复定义阴影函数。
#define BURT_SHADOWS_INCLUDED // 标记 BurtShadows.hlsl 已经被包含过，后续重复 include 会被跳过。

// 声明主光 shadow map 深度纹理，它由 BurtRP 的主光阴影 pass 上传。
UNITY_DECLARE_SHADOWMAP(_BurtMainLightShadowMap);

// 保存世界空间到主光 shadow map 空间的矩阵，由 C# 阴影 pass 计算并上传。
float4x4 _BurtMainLightWorldToShadow;
float4 _BurtMainLightWorldToShadowRow0;
float4 _BurtMainLightWorldToShadowRow1;
float4 _BurtMainLightWorldToShadowRow2;
float4 _BurtMainLightWorldToShadowRow3;

// 保存主光阴影强度，用来控制阴影对直接光照的影响程度。
float _BurtMainLightShadowStrength;

// 保存 shadow map 的 texel 信息，后续 PCF 和调试采样会用到。
float4 _BurtMainLightShadowTexelSize;

// 保存接收端深度比较 bias，用来减少基础 self-shadow acne。
float _BurtMainLightShadowSampleBias;

// 保存软阴影开关，后续添加 PCF 时可以用它选择采样路径。
float _BurtMainLightShadowSoftness;

// 保存未来主光阴影采样路径可能需要的一组输入。
struct BurtMainLightShadowInput
{
    // 保存透视除法前的齐次 shadow 坐标。
    float4 shadowCoord;

    // 保存本次采样应该使用的阴影强度。
    float strength;
};

// 把世界空间位置转换到主光 shadow map 空间。
float4 BurtTransformWorldToMainLightShadow(float4 positionWS)
{
    // Use explicit rows instead of relying only on Unity's matrix packing for globals/material overrides.
    return float4(
        dot(_BurtMainLightWorldToShadowRow0, positionWS),
        dot(_BurtMainLightWorldToShadowRow1, positionWS),
        dot(_BurtMainLightWorldToShadowRow2, positionWS),
        dot(_BurtMainLightWorldToShadowRow3, positionWS));
}

// 判断投影后的 shadow 坐标是否落在 0 到 1 的有效 shadow map 范围内。
bool BurtIsInsideMainLightShadowMap(float3 projectedShadowCoord)
{
    // 检查 UV 和深度是否超出当前 shadow map 的有效范围。
    bool outsideShadowMap = projectedShadowCoord.x <= 0.0f || projectedShadowCoord.x >= 1.0f || projectedShadowCoord.y <= 0.0f || projectedShadowCoord.y >= 1.0f || projectedShadowCoord.z <= 0.0f || projectedShadowCoord.z >= 1.0f;

    // 只有坐标在有效范围内时才允许采样 shadow map。
    return !outsideShadowMap;
}

// 根据 Unity 当前深度方向，对接收端 shadow depth 应用 bias。
float BurtApplyMainLightReceiverBias(float projectedDepth)
{
    float receiverBias = max(0.0f, _BurtMainLightShadowSampleBias);

    // Unity's shadow compare path applies receiver-plane bias by adding it to the comparison depth.
    // Keep the same sign here; subtracting pushes BurtRP receivers into self-shadow on D3D/reversed-Z.
    return saturate(projectedDepth + receiverBias);
}

// Explicitly compare the main light shadow map depth.
float BurtSampleMainLightShadowCompare(float3 projectedShadowCoord)
{
    // Use Unity's shadow compare sampler for RenderTextureFormat.Shadowmap.
    // Sampling it as a regular depth texture can return the clear value on D3D-like backends, which makes attenuation stay white.
    return UNITY_SAMPLE_SHADOW(_BurtMainLightShadowMap, projectedShadowCoord);
}

// Apply light shadow strength to the raw visibility result.
float BurtApplyShadowStrength(float rawShadow, float strength)
{
    // strength 为 0 时返回完全受光，strength 为 1 时返回原始 shadow map 结果。
    return lerp(1.0f, rawShadow, saturate(strength));
}

// 采样 BurtRP 当前主光阴影，并返回最终阴影衰减。
// Sample BurtRP current main light shadow and return final attenuation.
float BurtSampleMainLightShadow(float4 shadowCoord)
{
    // 如果 C# 为当前 request 关闭了主光阴影，就直接跳过所有 shadow map 采样。
    if (_BurtMainLightShadowStrength <= 0.0001f)
    {
        // 返回完全受光，因为当前没有启用有效的主光阴影。
        return 1.0f;
    }

    // 对齐次 shadow 坐标做透视除法，得到 0 到 1 范围内的 UV 和深度。
    float safeW = abs(shadowCoord.w) > 0.00001f ? shadowCoord.w : (shadowCoord.w < 0.0f ? -0.00001f : 0.00001f);
    float3 projectedShadowCoord = shadowCoord.xyz / safeW;

    // 如果坐标不在 shadow map 内，就不要采样，避免得到未定义或误导性的阴影值。
    if (!BurtIsInsideMainLightShadowMap(projectedShadowCoord))
    {
        // 当前像素在主光阴影投影范围外时按完全受光处理。
        return 1.0f;
    }

    // 在比较采样前应用接收端 bias，减少阴影 acne。
    projectedShadowCoord.z = BurtApplyMainLightReceiverBias(projectedShadowCoord.z);

    // 从比较 shadow map 中读取原始阴影可见性。
    float rawShadow = BurtSampleMainLightShadowCompare(projectedShadowCoord);

    // 应用灯光阴影强度，返回最终给光照使用的衰减值。
    return BurtApplyShadowStrength(rawShadow, _BurtMainLightShadowStrength);
}


#endif // BURT_SHADOWS_INCLUDED // 结束 BurtShadows.hlsl 的 include guard。
