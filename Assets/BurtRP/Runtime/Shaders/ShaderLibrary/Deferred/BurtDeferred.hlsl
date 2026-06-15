// BurtRP Deferred shader 公共工具：集中放置 GBuffer 采样、深度重建和全屏三角形函数，不负责 C# RenderTarget 生命周期。
#ifndef BURT_DEFERRED_INCLUDED // 开始 include guard，防止 DeferredLighting 和 GBuffer Debug 重复包含。
#define BURT_DEFERRED_INCLUDED // 标记 BurtDeferred.hlsl 已经被包含过。

#include "UnityCG.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBuffer.hlsl" // 引入 BurtEncodedGBuffer / BurtGBufferData，以及 GBuffer 编解码函数。

// 主 Agent 分配并绑定的五张 GBuffer，全局名来自 BurtRenderGraphResourceRegistry 的 _BurtGBuffer0/1/2/3/4 约定。
Texture2D _BurtGBuffer0;
Texture2D _BurtGBuffer1;
Texture2D _BurtGBuffer2;
Texture2D _BurtGBuffer3;
Texture2D _BurtGBuffer4;
Texture2D _BurtGBufferObjectIndex;

// 主 Agent 绑定的 CameraDepth；来源可以是 DepthPrepass，也可以是 GBuffer pass 写入的共享深度。
UNITY_DECLARE_DEPTH_TEXTURE(_BurtCameraDepthTexture);

// 主 Agent 在 Deferred Lighting Pass 上传的逆 ViewProjection；C# 侧使用 GL.GetGPUProjectionMatrix(..., true) 对齐渲染到 RT 的投影。
float4x4 _BurtDeferredInverseViewProjectionMatrix;

float4x4 _BurtDeferredInverseNonJitteredViewProjectionMatrix;

// 主 Agent 上传的相机世界坐标；Deferred 用它和重建 positionWS 计算 viewDirectionWS。
float4 _BurtDeferredCameraWorldPosition;

// 主 Agent 上传的近远裁剪面参数；当前仅作为 Debug / 后续线性深度扩展预留。
float4 _BurtDeferredCameraClipPlanes;

// 主 Agent 上传的屏幕尺寸，xy = width/height，zw = 1/width/1/height。
float4 _BurtDeferredScreenSize;

// 生成不带平台翻转的全屏三角形坐标；这个函数只给 SV_POSITION 使用，不能拿来采样 RenderTexture。
float2 BurtGetFullScreenTriangleRawCoord(uint vertexID)
{
    // 根据 SV_VertexID 生成 (0,0)、(2,0)、(0,2) 三个点，覆盖整个屏幕。
    return float2((vertexID << 1) & 2, vertexID & 2);
}

// 出处：XRender/Shaders/Library/CommonTransform.hlsl::GetFullScreenTriangleTexCoord；通过 SV_VertexID 生成采样 GBuffer/Depth 使用的 UV。
float2 BurtGetFullScreenTriangleTexCoord(uint vertexID)
{
    // 先生成未翻转的全屏三角形 UV，后面只改变采样坐标，不改变顶点位置。
    float2 uv = BurtGetFullScreenTriangleRawCoord(vertexID);

    #if UNITY_UV_STARTS_AT_TOP
        // 对齐 XRender 的 GetFullScreenTriangleTexCoord：D3D 类平台采样 RT 时需要把源纹理 Y 方向翻过来。
        uv.y = 1.0f - uv.y;
    #endif

    // 返回平台相关的屏幕采样 UV；NDC 重建时仍会按 XRender 的 TransformScreenUVToPositionNDC 再处理一次。
    return uv;
}

float4 BurtGetFullScreenTriangleVertexPosition(uint vertexID)
{
    // 顶点位置必须使用未翻转坐标；否则采样 UV 的平台翻转会被顶点位置反向抵消，导致 Deferred 看起来像投影矩阵错位。
    float2 uv = BurtGetFullScreenTriangleRawCoord(vertexID);

    // z 固定为 0；Deferred Lighting 使用 ZTest Always，不依赖全屏三角形自身深度。
    return float4(uv * 2.0f - 1.0f, 0.0f, 1.0f);
}

// 出处：XRender/Shaders/Library/CommonTransform.hlsl::TransformScreenUVToPositionNDC；把屏幕 UV 和硬件深度转换为裁剪空间位置。
float4 BurtBuildDeferredClipPosition(float2 screenUV, float rawDepth)
{
    // xy 从 0..1 屏幕 UV 还原到 -1..1 的 NDC 平面。
    float2 clipXY = screenUV * 2.0f - 1.0f;

    #if UNITY_UV_STARTS_AT_TOP
        // 对齐 XRender 的 PLATFORM_UV_STARTS_AT_TOP 分支：D3D 类平台从屏幕 UV 回到 NDC 时需要翻转 y。
        clipXY.y = -clipXY.y;
    #endif

    #if defined(UNITY_REVERSED_Z)
        // reversed-Z 平台的硬件深度已经和 GPU clip z 方向一致。
        float clipZ = rawDepth;
    #else
        // 非 reversed-Z 平台需要从 0..1 硬件深度映射到当前 API 的 NDC z 范围。
        float clipZ = lerp(UNITY_NEAR_CLIP_VALUE, 1.0f, rawDepth);
    #endif

    // 返回齐次裁剪空间位置，随后乘以逆 ViewProjection 得到世界坐标。
    return float4(clipXY, clipZ, 1.0f);
}

// 出处：XRender/Shaders/Library/CommonTransform.hlsl::ReconstructPositionWS；用屏幕 UV、硬件深度和逆 VP 重建世界坐标。
float3 BurtReconstructDeferredPositionWSWithMatrix(float2 screenUV, float rawDepth, float4x4 inverseViewProjectionMatrix)
{
    // 先构建与当前图形 API 和 UV 原点匹配的裁剪空间位置。
    float4 clipPosition = BurtBuildDeferredClipPosition(screenUV, rawDepth);

    // 乘以主 Agent 上传的逆 ViewProjection，把裁剪空间点还原到世界空间齐次坐标。
    float4 positionWS = mul(inverseViewProjectionMatrix, clipPosition);

    // 对 w 做极小值保护，避免异常深度或未上传矩阵时产生 NaN。
    float safeW = abs(positionWS.w) > BURT_EPSILON ? positionWS.w : (positionWS.w < 0.0f ? -BURT_EPSILON : BURT_EPSILON);

    // 做透视除法，得到最终世界空间坐标。
    return positionWS.xyz / safeW;
}

// 出处：XRender/Shaders/Library/CommonTransform.hlsl::ReconstructPositionWS；用屏幕 UV、硬件深度和逆 VP 重建世界坐标。
float3 BurtReconstructDeferredPositionWS(float2 screenUV, float rawDepth)
{
    return BurtReconstructDeferredPositionWSWithMatrix(screenUV, rawDepth, _BurtDeferredInverseViewProjectionMatrix);
}

float3 BurtReconstructDeferredNonJitteredPositionWS(float2 screenUV, float rawDepth)
{
    return BurtReconstructDeferredPositionWSWithMatrix(screenUV, rawDepth, _BurtDeferredInverseNonJitteredViewProjectionMatrix);
}

// 采样 BurtRP 当前五张 GBuffer RT，并打包成 BurtGBuffer.hlsl 定义的 BurtEncodedGBuffer。
BurtEncodedGBuffer BurtSampleEncodedGBuffer(float2 screenUV)
{
    // 创建编码 GBuffer 输出，字段顺序必须和材质 shader 的 SV_Target0/1/2/3 保持一致。
    BurtEncodedGBuffer encodedGBuffer;

    // GBuffer0：baseColor.rgb + occlusion.a。
    encodedGBuffer.gbuffer0 = BURT_SAMPLE_TEXTURE2D_POINT_CLAMP(_BurtGBuffer0, screenUV);

    // GBuffer1：oct normal.rg + packed shadingModel/material.b + smoothness.a。
    encodedGBuffer.gbuffer1 = BURT_SAMPLE_TEXTURE2D_POINT_CLAMP(_BurtGBuffer1, screenUV);

    // GBuffer2：emission.rgb + reflectance.a。
    encodedGBuffer.gbuffer2 = BURT_SAMPLE_TEXTURE2D_POINT_CLAMP(_BurtGBuffer2, screenUV);

    encodedGBuffer.gbuffer3 = BURT_SAMPLE_TEXTURE2D_POINT_CLAMP(_BurtGBuffer3, screenUV);

    encodedGBuffer.gbuffer4 = BURT_SAMPLE_TEXTURE2D_POINT_CLAMP(_BurtGBuffer4, screenUV);

    // 返回采样结果，让调用方继续 Decode 或做原始 GBuffer Debug。
    return encodedGBuffer;
}

float BurtSampleDeferredShadingModelID(float2 screenUV)
{
    // Fast path for split lighting passes: read only GBuffer1.b to reject the wrong shading model before sampling all GBuffer/depth data.
    float packedShadingModelAndMaterial = BURT_SAMPLE_TEXTURE2D_POINT_CLAMP(_BurtGBuffer1, screenUV).b;
    float shadingModelID;
    BurtDecodeMetallicAndShadingModelFromGBuffer(packedShadingModelAndMaterial, shadingModelID);
    return shadingModelID;
}

int BurtSampleDeferredPerObjectShadowObjectIndex(float2 screenUV)
{
    float encodedObjectIndex = BURT_SAMPLE_TEXTURE2D_POINT_CLAMP(_BurtGBufferObjectIndex, screenUV).r;
    return (int)floor(saturate(encodedObjectIndex) * 255.0f + 0.5f);
}

// 采样 BurtRP CameraDepth 的原始硬件深度，Deferred Lighting 和 GBuffer Debug 共用。
float BurtSampleDeferredRawDepth(float2 screenUV)
{
    // SAMPLE_DEPTH_TEXTURE 使用 Unity 提供的深度纹理采样宏，兼容不同平台的深度纹理声明。
    return SAMPLE_DEPTH_TEXTURE(_BurtCameraDepthTexture, screenUV);
}

// 根据屏幕 UV 准备 Deferred Lighting 需要的视图数据：rawDepth、positionWS 和 viewDirectionWS。
void BurtPrepareDeferredViewDataFromUVs(float2 textureUV, float2 reconstructionUV, out float rawDepth, out float3 positionWS, out float3 viewDirectionWS)
{
    rawDepth = BurtSampleDeferredRawDepth(textureUV);
    positionWS = BurtReconstructDeferredPositionWS(reconstructionUV, rawDepth);
    viewDirectionWS = BurtSafeNormalize(_BurtDeferredCameraWorldPosition.xyz - positionWS);
}

void BurtPrepareDeferredViewData(float2 screenUV, out float rawDepth, out float3 positionWS, out float3 viewDirectionWS)
{
    BurtPrepareDeferredViewDataFromUVs(screenUV, screenUV, rawDepth, positionWS, viewDirectionWS);
}

void BurtPrepareDeferredViewData(float2 screenUV, out float rawDepth, out float3 positionWS, out float3 nonJitteredPositionWS, out float3 viewDirectionWS)
{
    rawDepth = BurtSampleDeferredRawDepth(screenUV);
    positionWS = BurtReconstructDeferredPositionWS(screenUV, rawDepth);
    nonJitteredPositionWS = BurtReconstructDeferredNonJitteredPositionWS(screenUV, rawDepth);
    viewDirectionWS = BurtSafeNormalize(_BurtDeferredCameraWorldPosition.xyz - positionWS);
}

#endif // BURT_DEFERRED_INCLUDED // 结束 BurtDeferred.hlsl 的 include guard。
