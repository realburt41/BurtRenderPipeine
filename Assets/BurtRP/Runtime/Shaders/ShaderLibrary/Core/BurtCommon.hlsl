// BurtRP 的通用 HLSL 工具库，所有 BurtRP shader 都可以安全包含这个文件。
#ifndef BURT_COMMON_INCLUDED // 开始 include guard，防止同一个 shader 编译单元里重复定义这些工具函数。
#define BURT_COMMON_INCLUDED // 标记 BurtCommon.hlsl 已经被包含过，后续重复 include 会被跳过。

// 定义一个很小的正数，用来避免除以 0 或 rsqrt 输入 0 导致 NaN。
static const float BURT_EPSILON = 0.000001f;

// 保存 BurtRP 各类 pass 常用的位置数据。
struct BurtPositionInputs
{
    // 保存世界空间位置，后续光照、阴影、雾效和调试视图都会用到。
    float3 positionWS;

    // 保存裁剪空间位置，最终会写入 SV_POSITION 让 GPU 做光栅化。
    float4 positionCS;
};

// 保存 BurtRP 光照当前使用的法线数据。
struct BurtNormalInputs
{
    // 保存世界空间法线，Lambert 光照、法线贴图和阴影 bias 计算都会用到。
    float3 normalWS;
};

// 对 float3 做安全归一化，避免零向量导致非法结果。
float3 BurtSafeNormalize(float3 value)
{
    // 先计算向量长度平方，并用 BURT_EPSILON 做下限保护，避免 rsqrt(0)。
    float lengthSquared = max(dot(value, value), BURT_EPSILON);

    // 使用平方根倒数把输入向量缩放成单位长度方向。
    return value * rsqrt(lengthSquared);
}

// 对 float4 的 xyz 分量做安全归一化，同时保留原本的 w 分量。
float4 BurtSafeNormalizeDirection(float4 value)
{
    // 返回归一化后的 xyz，并把原来的 w 原样传回去。
    return float4(BurtSafeNormalize(value.xyz), value.w);
}

#endif // BURT_COMMON_INCLUDED // 结束 BurtCommon.hlsl 的 include guard。
