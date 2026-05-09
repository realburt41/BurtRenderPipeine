// BurtRP 的材质输入工具库，负责把贴图、颜色、Mask Map 和 alpha 裁剪整理成统一的表面数据。
#ifndef BURT_INPUT_INCLUDED // 开始 include guard，防止多个 shader library 重复包含时产生重定义。
#define BURT_INPUT_INCLUDED // 标记 BurtInput.hlsl 已经被包含过，后续重复 include 会被跳过。

// 声明 Base Map 贴图，BurtLit 和其它 Lit 类型 shader 会用它采样基础颜色。
sampler2D _BaseMap;

// 声明 Mask Map 贴图，Forward PBR 会把 R 当金属度、G 当环境遮蔽、A 当光滑度。
sampler2D _MaskMap;

// 定义 XRender / Frostbite 风格的默认 reflectance，0.5 会映射到常见非金属 F0=0.04。
static const float BURT_INPUT_DEFAULT_REFLECTANCE = 0.5f;

// 保存光照函数需要的材质表面属性。
struct BurtSurfaceData
{
    // 保存材质最终基础颜色，通常来自 Base Map 与 Base Color 的相乘结果。
    float4 baseColor;

    // 单独保存 alpha，方便 Forward pass 在光照后直接把透明度传给输出。
    float alpha;

    // 保存材质介质反射率参数，参考 XRender Reflectance，0.5 会映射到非金属 F0=0.04。
    float reflectance;

    // 保存材质光滑度，数值越高，高光越小越锐利。
    float smoothness;

    // 保存材质金属度，0 表示非金属，1 表示金属。
    float metallic;

    // 保存材质环境遮蔽，1 表示不遮蔽环境光，0 表示完全遮蔽环境光。
    float occlusion;
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

// 按 Unity 的贴图 Tiling / Offset 规则转换 Base Map 使用的 mesh UV0。
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

// 按 Unity 的贴图 Tiling / Offset 规则转换 Mask Map 使用的 mesh UV0。
float2 BurtTransformMaskMapUV(float2 uv0, float4 maskMapST)
{
    // maskMapST.xy 表示 Tiling，maskMapST.zw 表示 Offset。
    return uv0 * maskMapST.xy + maskMapST.zw;
}

// 使用已经转换过的 UV 采样材质 Mask Map。
float4 BurtSampleMaskMap(float2 maskMapUV)
{
    // R 通道约定为 Metallic，G 通道约定为 Occlusion，A 通道约定为 Smoothness，B 通道暂时预留。
    return tex2D(_MaskMap, maskMapUV);
}

// 根据标量参数和 Mask Map 计算最终金属度。
float BurtResolveMetallic(float metallic, float4 maskMap)
{
    // 默认白色 Mask Map 的 R 为 1，所以最终结果会保持 _Metallic 标量原值。
    return saturate(metallic * maskMap.r);
}

// 根据标量参数和 Mask Map 计算最终光滑度。
float BurtResolveSmoothness(float smoothness, float4 maskMap)
{
    // 默认白色 Mask Map 的 A 为 1，所以最终结果会保持 _Smoothness 标量原值。
    return saturate(smoothness * maskMap.a);
}

// 根据 Mask Map 和强度参数计算最终环境遮蔽。
float BurtResolveOcclusion(float4 maskMap, float occlusionStrength)
{
    // G 通道越低表示环境遮蔽越强，强度为 0 时强制回到 1，避免影响旧材质。
    return saturate(lerp(1.0f, maskMap.g, saturate(occlusionStrength)));
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

    // 默认使用 XRender 的 reflectance=0.5，对应常见非金属 F0=0.04。
    surfaceData.reflectance = BURT_INPUT_DEFAULT_REFLECTANCE;

    // 默认光滑度设为 0.5，给后续显式开启高光的路径提供中间值。
    surfaceData.smoothness = 0.5f;

    // 默认金属度设为 0，保持旧材质按非金属介质处理。
    surfaceData.metallic = 0.0f;

    // 默认环境遮蔽设为 1，表示不压暗环境光，保持旧材质亮度不变。
    surfaceData.occlusion = 1.0f;

    // 返回填充完成的表面数据。
    return surfaceData;
}

// 根据基础颜色、reflectance 和光滑度创建 BurtSurfaceData。
BurtSurfaceData BurtCreateSurfaceData(float4 baseColor, float reflectance, float smoothness)
{
    // 创建一个输出结构体，下面逐项填充，避免依赖旧的默认参数。
    BurtSurfaceData surfaceData;

    // 保存当前片元的基础颜色，漫反射和环境光会使用它。
    surfaceData.baseColor = baseColor;

    // 保存 alpha，让 Forward pass 可以保持材质透明度输出。
    surfaceData.alpha = baseColor.a;

    // 保存 XRender 风格 reflectance，而不是直接暴露 F0。
    surfaceData.reflectance = saturate(reflectance);

    // 把光滑度限制到 0 到 1，避免材质面板或脚本传入异常值。
    surfaceData.smoothness = saturate(smoothness);

    // 这个重载不传 metallic，所以默认按非金属处理。
    surfaceData.metallic = 0.0f;

    // 这个重载不传 occlusion，所以默认不遮蔽环境光。
    surfaceData.occlusion = 1.0f;

    // 返回填充完成的表面数据。
    return surfaceData;
}

// 根据基础颜色、reflectance、光滑度和金属度创建 BurtSurfaceData。
BurtSurfaceData BurtCreateSurfaceData(float4 baseColor, float reflectance, float smoothness, float metallic)
{
    // 创建一个输出结构体，下面逐项填充，供 PBR BRDF 使用。
    BurtSurfaceData surfaceData;

    // 保存当前片元的基础颜色，PBR 中它会同时影响漫反射和金属反射颜色。
    surfaceData.baseColor = baseColor;

    // 保存 alpha，让 Forward pass 可以保持材质透明度输出。
    surfaceData.alpha = baseColor.a;

    // 保存 XRender 风格 reflectance，它后续会在 BRDF 内部映射成非金属 F0。
    surfaceData.reflectance = saturate(reflectance);

    // 把光滑度限制到 0 到 1，避免异常材质参数影响 BRDF。
    surfaceData.smoothness = saturate(smoothness);

    // 把金属度限制到 0 到 1，保证非金属到金属的插值范围稳定。
    surfaceData.metallic = saturate(metallic);

    // 没有显式传入 Mask Map 时默认不遮蔽环境光，保持旧 PBR 路径亮度不变。
    surfaceData.occlusion = 1.0f;

    // 返回填充完成的表面数据。
    return surfaceData;
}

// 根据基础颜色、reflectance、标量参数和 Mask Map 创建完整 PBR 用 BurtSurfaceData。
BurtSurfaceData BurtCreateSurfaceData(float4 baseColor, float reflectance, float smoothness, float metallic, float4 maskMap, float occlusionStrength)
{
    // 创建一个输出结构体，下面逐项填充，供 PBR BRDF 和环境遮蔽共同使用。
    BurtSurfaceData surfaceData;

    // 保存当前片元的基础颜色，PBR 中它会同时影响漫反射和金属反射颜色。
    surfaceData.baseColor = baseColor;

    // 保存 alpha，让 Forward pass 可以保持材质透明度输出。
    surfaceData.alpha = baseColor.a;

    // 保存 XRender 风格 reflectance，而不是把 F0 直接暴露给材质。
    surfaceData.reflectance = saturate(reflectance);

    // 标量 Smoothness 与 Mask Map A 通道相乘，得到最终光滑度。
    surfaceData.smoothness = BurtResolveSmoothness(smoothness, maskMap);

    // 标量 Metallic 与 Mask Map R 通道相乘，得到最终金属度。
    surfaceData.metallic = BurtResolveMetallic(metallic, maskMap);

    // Mask Map G 通道经过强度混合后得到最终环境遮蔽，只用于环境光。
    surfaceData.occlusion = BurtResolveOcclusion(maskMap, occlusionStrength);

    // 返回填充完成的表面数据。
    return surfaceData;
}

#endif // BURT_INPUT_INCLUDED // 结束 BurtInput.hlsl 的 include guard。