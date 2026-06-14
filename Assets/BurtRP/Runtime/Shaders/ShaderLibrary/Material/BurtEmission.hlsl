// BurtRP 的自发光工具库，负责采样 Emission Map 并输出不受灯光影响的颜色。
#ifndef BURT_EMISSION_INCLUDED // 开始 include guard，防止同一个 shader 编译单元里重复定义自发光函数。
#define BURT_EMISSION_INCLUDED // 标记 BurtEmission.hlsl 已经被包含过，后续重复 include 会被跳过。

// 声明自发光贴图，BurtLit 的 Forward pass 会用 mesh UV0 对它进行采样。
Texture2D _EmissionMap;

// 按 Unity 的贴图 Tiling / Offset 规则转换自发光贴图 UV。
float2 BurtTransformEmissionMapUV(float2 uv0, float4 emissionMapST)
{
    // emissionMapST.xy 表示 Tiling，emissionMapST.zw 表示 Offset。
    return uv0 * emissionMapST.xy + emissionMapST.zw;
}

// 使用已经转换过的 UV 采样自发光贴图。
float4 BurtSampleEmissionMap(float2 emissionMapUV)
{
    // 返回自发光贴图颜色，后续会和材质 Emission Color 相乘。
    return BURT_SAMPLE_TEXTURE2D_REPEAT(_EmissionMap, emissionMapUV);
}

// 计算最终自发光颜色。
float3 BurtEvaluateEmission(float2 emissionMapUV, float3 emissionColor)
{
    // 采样自发光贴图 RGB，贴图默认黑色时不会增加任何亮度。
    float3 emissionMapColor = BurtSampleEmissionMap(emissionMapUV).rgb;

    // 把贴图颜色和材质颜色相乘，得到最终要叠加到 lit color 上的自发光。
    return emissionMapColor * emissionColor;
}

#endif // BURT_EMISSION_INCLUDED // 结束 BurtEmission.hlsl 的 include guard。
