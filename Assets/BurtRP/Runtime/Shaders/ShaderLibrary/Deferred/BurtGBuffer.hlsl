// BurtRP Deferred GBuffer 约定草案：只定义 shader 侧数据布局和编解码，不绑定 RenderTarget 生命周期。
#ifndef BURT_GBUFFER_INCLUDED // 开始 include guard，防止同一个 shader 编译单元里重复定义 GBuffer 工具。
#define BURT_GBUFFER_INCLUDED // 标记 BurtGBuffer.hlsl 已经被包含过，后续重复 include 会被跳过。

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtBRDF.hlsl" // 引入 BurtSurfaceData、PBR 准备结构和 XRender 风格 reflectance/F0/roughness 工具。

// GBuffer0 约定：rgb = baseColor，a = occlusion；baseColor 保持材质基础色，不预乘灯光或能量项。
// GBuffer1 约定：rg = octahedron directionWS，b = packed(shadingModelID, metallic/scatter)，a = smoothness；Default Lit 存 normal，Hair 存 strand direction。
// GBuffer2 约定：rgb = emission，a = reflectance；emission 建议使用 HDR RT，reflectance 继续按 XRender 语义重建 F0，不直接存 F0。

// 保存 Deferred 解码后的材质数据；字段只覆盖 PBR shading 所需的最小集合，方便后续替换真实 GBuffer RT。
struct BurtGBufferData
{
    // 保存材质基础色，和 Forward 的 surfaceData.baseColor.rgb 保持一致。
    float3 baseColor;

    // 保存世界空间方向，解码后必须是单位向量；Default Lit=normalWS，Hair=strandDirectionWS。
    float3 normalWS;

    // 保存材质通道；Default Lit=metallic，Hair=scatter。访问时优先使用下面的语义 helper，避免混用。
    float metallic;

    // 保存光滑度，GBuffer 保留面板语义，后续统一走 smoothness -> perceptual roughness。
    float smoothness;

    // 保存感知粗糙度，解码后立即计算出来，方便 Debug 或后续 shading 直接读取。
    float perceptualRoughness;

    // 保存 XRender 风格 reflectance，避免把 F0 暴露成材质输入或直接写入 GBuffer。
    float reflectance;

    // 保存环境遮蔽，用于间接漫反射和间接高光遮蔽。
    float occlusion;

    // 保存自发光颜色；如果后续需要 HDR emission，GBuffer2 的 RT 格式要配合选择。
    float3 emission;

    // 保存 shading model id；0=Default Lit，1=Hair。它决定 vector/material 两个复用槽的语义。
    float shadingModelID;
};

// 保存实际写入 RenderTarget 的三张 GBuffer 颜色；这里只定义编码结果，不负责 RT 创建或生命周期。
struct BurtEncodedGBuffer
{
    // GBuffer0：baseColor.rgb + occlusion。
    float4 gbuffer0;

    // GBuffer1：octa directionWS.rg + packed(shadingModelID, metallic/scatter) + smoothness。
    float4 gbuffer1;

    // GBuffer2：emission.rgb + reflectance。
    float4 gbuffer2;
};

// Octahedron normal 编码的折叠函数；把背半球折回二维平面，节省 GBuffer normal 通道。
float2 BurtWrapOctahedronNormal(float2 value)
{
    // 分量符号单独计算，避免 HLSL 向量三目表达式在不同后端产生兼容性问题。
    float2 signNotZero = float2(value.x >= 0.0f ? 1.0f : -1.0f, value.y >= 0.0f ? 1.0f : -1.0f);

    // 背半球折叠公式：用 1 - abs(yx) 保留边界连续性，再乘回原始符号。
    return (1.0f - abs(value.yx)) * signNotZero;
}

// 把世界空间单位向量编码成两个 0..1 通道；Deferred GBuffer1.rg 使用这个结果。
float2 BurtEncodeNormalWSForGBuffer(float3 normalWS)
{
    // 先安全归一化，避免法线贴图或插值误差影响 octahedron 投影。
    float3 n = BurtSafeNormalize(normalWS);

    // 投影到 L1 单位八面体；分母做保护，避免异常零法线产生 NaN。
    float invL1 = rcp(max(abs(n.x) + abs(n.y) + abs(n.z), BURT_EPSILON));
    float2 encoded = n.xy * invL1;

    // 背半球需要折叠回二维平面，保证两个通道能恢复完整方向。
    if (n.z < 0.0f)
    {
        encoded = BurtWrapOctahedronNormal(encoded);
    }

    // 从 [-1, 1] 映射到 [0, 1]，方便写入常规颜色 RT。
    return encoded * 0.5f + 0.5f;
}

// 从 GBuffer1.rg 解码世界空间单位向量；Default Lit 把它当 normal，Hair 把它当 strand direction。
float3 BurtDecodeNormalWSFromGBuffer(float2 encodedNormal)
{
    // 从 [0, 1] 还原到 octahedron 平面上的 [-1, 1]。
    float2 f = encodedNormal * 2.0f - 1.0f;

    // 先按前半球重建 z，再通过下面的修正处理背半球折叠。
    float3 n = float3(f.x, f.y, 1.0f - abs(f.x) - abs(f.y));

    // z 为负表示来自折叠区域，需要把 xy 沿符号方向推回去。
    float t = saturate(-n.z);
    n.x += n.x >= 0.0f ? -t : t;
    n.y += n.y >= 0.0f ? -t : t;

    // 最后安全归一化，抵消 RT 量化和插值带来的长度误差。
    return BurtSafeNormalize(n);
}

// GBuffer1.b 复用一个半精度通道保存 shading model 和 metallic/scatter。
static const float BURT_GBUFFER_SHADING_MODEL_PACK_COUNT = 4.0f;
static const float BURT_GBUFFER_SHADING_MODEL_PACK_SCALE = 0.999f;

float BurtEncodeMetallicAndShadingModelForGBuffer(float metallicOrScatter, float shadingModelID)
{
    // Point-sampled ARGBHalf GBuffer1 can safely store four model buckets while keeping useful 0..1 material precision.
    float modelID = clamp(BurtResolveSurfaceShadingModel(shadingModelID), 0.0f, BURT_GBUFFER_SHADING_MODEL_PACK_COUNT - 1.0f);
    return (modelID + saturate(metallicOrScatter) * BURT_GBUFFER_SHADING_MODEL_PACK_SCALE) / BURT_GBUFFER_SHADING_MODEL_PACK_COUNT;
}

float BurtDecodeMetallicAndShadingModelFromGBuffer(float packedValue, out float shadingModelID)
{
    // The 0.999 encode scale prevents metallic=1 from spilling into the next shading model bucket.
    float scaled = saturate(packedValue) * BURT_GBUFFER_SHADING_MODEL_PACK_COUNT;
    shadingModelID = floor(min(scaled, BURT_GBUFFER_SHADING_MODEL_PACK_COUNT - BURT_EPSILON));
    return saturate((scaled - shadingModelID) / BURT_GBUFFER_SHADING_MODEL_PACK_SCALE);
}

// 从 Forward 已经算好的 SurfaceData / direction / emission 生成待编码的 GBuffer 数据；Hair 会把 direction 当 strand。
BurtGBufferData BurtCreateGBufferData(BurtSurfaceData surfaceData, float3 normalWS, float3 emission)
{
    // 创建输出结构体，下面逐项填入 Deferred 所需的最小材质字段。
    BurtGBufferData data;

    // baseColor 不包含灯光和能量项，后续 Deferred shading 再统一计算。
    data.baseColor = surfaceData.baseColor.rgb;

    // 向量槽保存世界空间方向，编码前先做安全归一化；Hair shader 会传入 strand direction。
    data.normalWS = BurtSafeNormalize(normalWS);

    // 保留材质输入语义，避免 Deferred 阶段直接依赖 F0 或临时 BRDF 结果。
    data.metallic = saturate(surfaceData.metallic);
    data.smoothness = saturate(surfaceData.smoothness);
    data.reflectance = saturate(surfaceData.reflectance);
    data.occlusion = saturate(surfaceData.occlusion);

    // 解码侧也会重新计算，这里提前写入方便写入前的 Debug 或断点检查。
    data.perceptualRoughness = ClampPerceptualRoughness(PerceptualSmoothnessToPerceptualRoughness(data.smoothness));

    // 自发光允许 HDR，编码函数不会 saturate rgb，具体 RT 格式后续由主渲染流程决定。
    data.emission = max(emission, float3(0.0f, 0.0f, 0.0f));

    // 记录材质 shading model；Hair 第一版不增加 GBuffer 数量，复用向量槽和 material channel。
    data.shadingModelID = BurtResolveSurfaceShadingModel(surfaceData.shadingModelID);

    // 返回未编码的 GBuffer 数据，调用方可选择直接调试或继续编码到 RT。
    return data;
}

// Hair GBuffer 第一版不扩 RT：vector slot= strand direction，material channel= scatter。
BurtSurfaceData BurtApplyHairGBufferSurfaceSemantics(BurtSurfaceData surfaceData, float hairScatter)
{
    surfaceData.shadingModelID = BURT_SHADING_MODEL_HAIR;
    surfaceData.metallic = saturate(hairScatter);
    return surfaceData;
}

BurtGBufferData BurtCreateHairGBufferData(BurtSurfaceData surfaceData, float3 strandDirectionWS, float3 emission)
{
    surfaceData.shadingModelID = BURT_SHADING_MODEL_HAIR;
    return BurtCreateGBufferData(surfaceData, strandDirectionWS, emission);
}

float3 BurtGetGBufferDirectionWS(BurtGBufferData gbufferData)
{
    return gbufferData.normalWS;
}

float3 BurtGetDefaultLitNormalWS(BurtGBufferData gbufferData)
{
    return gbufferData.normalWS;
}

float3 BurtGetHairStrandDirectionWS(BurtGBufferData gbufferData)
{
    return gbufferData.normalWS;
}

float BurtGetGBufferMaterialChannel(BurtGBufferData gbufferData)
{
    return saturate(gbufferData.metallic);
}

float BurtGetDefaultLitMetallic(BurtGBufferData gbufferData)
{
    return saturate(gbufferData.metallic);
}

float BurtGetHairScatter(BurtGBufferData gbufferData)
{
    return saturate(gbufferData.metallic);
}

// 把语义化 GBuffer 数据编码成三张 RT 的 float4 输出。
BurtEncodedGBuffer BurtEncodeGBuffer(BurtGBufferData data)
{
    // 创建输出结构体，下面按顶部约定写入三个 GBuffer。
    BurtEncodedGBuffer encoded;

    // GBuffer0 保存基础色和 AO；baseColor/AO 都限制到 0..1，避免材质贴图异常污染后续光照。
    encoded.gbuffer0 = float4(saturate(data.baseColor), saturate(data.occlusion));

    // GBuffer1 保存压缩法线、packed shading model/material channel 和光滑度；Hair 时 material channel 解释为 scatter。
    encoded.gbuffer1 = float4(BurtEncodeNormalWSForGBuffer(data.normalWS), BurtEncodeMetallicAndShadingModelForGBuffer(data.metallic, data.shadingModelID), saturate(data.smoothness));

    // GBuffer2 保存自发光和 reflectance；emission 不做 saturate，reflectance 保持 XRender 0..1 输入范围。
    encoded.gbuffer2 = float4(max(data.emission, float3(0.0f, 0.0f, 0.0f)), saturate(data.reflectance));

    // 返回三张 RT 的编码结果。
    return encoded;
}

// 从三张 RT 的 float4 结果解码回语义化 GBuffer 数据。
BurtGBufferData BurtDecodeGBuffer(BurtEncodedGBuffer encoded)
{
    // 创建输出结构体，下面按顶部约定从三个 GBuffer 还原字段。
    BurtGBufferData data;

    // GBuffer0：还原基础色和 AO。
    data.baseColor = saturate(encoded.gbuffer0.rgb);
    data.occlusion = saturate(encoded.gbuffer0.a);

    // GBuffer1：还原世界空间向量槽、shading model、材质通道和光滑度。
    data.normalWS = BurtDecodeNormalWSFromGBuffer(encoded.gbuffer1.rg);
    data.metallic = BurtDecodeMetallicAndShadingModelFromGBuffer(encoded.gbuffer1.b, data.shadingModelID);
    data.smoothness = saturate(encoded.gbuffer1.a);

    // smoothness 仍然是 GBuffer 中的主存储语义，perceptual roughness 在解码后统一计算。
    data.perceptualRoughness = ClampPerceptualRoughness(PerceptualSmoothnessToPerceptualRoughness(data.smoothness));

    // GBuffer2：还原 emission 和 reflectance。
    data.emission = max(encoded.gbuffer2.rgb, float3(0.0f, 0.0f, 0.0f));
    data.reflectance = saturate(encoded.gbuffer2.a);

    // 返回解码后的语义数据，Deferred lighting pass 再转换为 PBRMaterialData/PBRGeometryData。
    return data;
}

// 从 GBufferData 准备 PBRMaterialData；这是 Deferred 从 GBuffer 进入当前 PBR shading core 的主要桥接函数。
BurtPBRMaterialData BurtPreparePBRMaterialData(BurtGBufferData gbufferData)
{
    // 创建输出结构体，字段填充规则必须和 SurfaceData 版本保持一致。
    BurtPBRMaterialData materialData;

    // 拷贝 GBuffer 保存的材质输入语义；Hair 的 material channel 是 scatter，不参与 PBR metallic 重建。
    materialData.baseColor = gbufferData.baseColor;
    materialData.metallic = BurtIsHairShadingModel(gbufferData.shadingModelID) ? 0.0f : BurtGetDefaultLitMetallic(gbufferData);
    materialData.reflectance = gbufferData.reflectance;
    materialData.occlusion = gbufferData.occlusion;
    materialData.smoothness = gbufferData.smoothness;

    // 从 smoothness 统一还原 roughness，并准备 GGX 常用层级。
    materialData.perceptualRoughness = ClampPerceptualRoughness(PerceptualSmoothnessToPerceptualRoughness(materialData.smoothness));
    materialData.linearRoughness = PerceptualRoughnessToLinearRoughness(materialData.perceptualRoughness);
    materialData.a2 = LinearRoughnessToA2(materialData.linearRoughness);

    // Deferred 不存 F0；继续用 baseColor + metallic + reflectance 重建，保持 XRender reflectance 思路。
    materialData.diffuseColor = DiffuseColorFromBaseColor(materialData.baseColor, materialData.metallic);
    materialData.f0 = DielectricReflectanceToF0(materialData.baseColor, materialData.reflectance, materialData.metallic);
    materialData.f90 = ApproximateF90(materialData.f0);

    // 返回当前 PBR shading core 可直接使用的材质数据。
    return materialData;
}

// 从 GBufferData 和 Deferred 重建出的 viewDirectionWS 准备 PBRGeometryData。
BurtPBRGeometryData BurtPreparePBRGeometryData(BurtGBufferData gbufferData, float3 viewDirectionWS)
{
    // 复用现有几何准备函数，保证 Forward 和 Deferred 的 NdotV / reflection direction 约定一致。
    return BurtPreparePBRGeometryData(BurtGetDefaultLitNormalWS(gbufferData), viewDirectionWS);
}

#endif // BURT_GBUFFER_INCLUDED // 结束 BurtGBuffer.hlsl 的 include guard。
