// BurtRP 的材质输入工具库，负责把贴图、颜色和 alpha 裁剪整理成统一的表面数据。
#ifndef BURT_INPUT_INCLUDED // 开始 include guard，防止多个 shader library 重复包含时产生重定义。
#define BURT_INPUT_INCLUDED // 标记 BurtInput.hlsl 已经被包含过，后续重复 include 会被跳过。

// 声明 Base Map 贴图，BurtLit 和其它 Lit 类型 shader 会用它采样基础颜色。
sampler2D _BaseMap;

// 保存光照函数需要的材质表面属性。
struct BurtSurfaceData
{
    // 保存材质最终基础颜色，通常来自 Base Map 与 Base Color 的相乘结果。
    float4 baseColor;

    // 单独保存 alpha，方便 Forward pass 在光照后直接把透明度传给输出。
    float alpha;
};

// 保存片元级几何输入，后续高光、雾效、附加光等功能会继续扩展这个结构。
struct BurtInputData
{
    // 保存当前片元的世界空间位置，未来距离衰减、雾效和更多阴影计算会用到。
    float3 positionWS;

    // 保存当前片元的世界空间法线，Lambert 或未来 BRDF 光照会用到。
    float3 normalWS;

    // 保存从片元指向相机的世界空间方向，未来高光和 Fresnel 会用到。
    float3 viewDirectionWS;
};

// 按 Unity 的贴图 Tiling / Offset 规则转换 mesh UV0。
float2 BurtTransformBaseMapUV(float2 uv0, float4 baseMapST)
{
    // baseMapST.xy 表示 Tiling，baseMapST.zw 表示 Offset。
    return uv0 * baseMapST.xy + baseMapST.zw;
}

// 使用已经转换过的 UV 采样材质 Base Map。
float4 BurtSampleBaseMap(float2 baseMapUV)
{
    // 使用 sampler2D + tex2D，是为了兼容当前 BurtLit.shader 仍在使用的 UnityCG.cginc 写法。
    return tex2D(_BaseMap, baseMapUV);
}

// 应用 BurtRP 统一的 alpha clip 规则，让 Forward、DepthOnly、ShadowCaster 使用同一套镂空判定。
void BurtApplyAlphaClip(float alpha, float alphaClip, float cutoff)
{
    // 当 _AlphaClip 大于等于 0.5 时认为开启裁剪，这和 Unity Toggle 类型 float 的常见用法一致。
    float clipEnabled = step(0.5f, alphaClip);

    // 未开启裁剪时使用 -1 作为阈值，让 0 到 1 范围内的 alpha 永远通过测试。
    float activeCutoff = lerp(-1.0f, cutoff, clipEnabled);

    // 当 alpha 小于有效阈值时丢弃当前片元，从而阻止颜色、深度或阴影写入。
    clip(alpha - activeCutoff);
}

// 根据已经合并好的基础颜色创建 BurtSurfaceData。
BurtSurfaceData BurtCreateSurfaceData(float4 baseColor)
{
    // 创建一个输出结构体，下面逐项填充，方便后续继续扩展字段。
    BurtSurfaceData surfaceData;

    // 保存当前片元的基础颜色，光照阶段会把它当成 albedo 使用。
    surfaceData.baseColor = baseColor;

    // 把 alpha 拆出来单独保存，避免后续代码反复从 baseColor.a 里取值。
    surfaceData.alpha = baseColor.a;

    // 返回填充完成的表面数据。
    return surfaceData;
}

#endif // BURT_INPUT_INCLUDED // 结束 BurtInput.hlsl 的 include guard。
