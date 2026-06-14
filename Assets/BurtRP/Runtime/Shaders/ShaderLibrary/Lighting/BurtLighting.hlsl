// BurtRP 的基础光照工具库，当前提供 Simple Lit、PBR/Hair 多光源直接光，以�?BurtRP 自有全局 SH / Sky Reflection �?PBR 间接光
#ifndef BURT_LIGHTING_INCLUDED // 开�?include guard，防止同一�?shader 编译单元里重复定义光照函数
#define BURT_LIGHTING_INCLUDED // 标记 BurtLighting.hlsl 已经被包含过，后续重�?include 会被跳过�?
#include "UnityCG.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl" // 引入安全数学函数，例�?BurtSafeNormalize
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInput.hlsl" // 引入 BurtSurfaceData，用来读取材质基础色、金属度、光滑度和环境遮蔽
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtShadingModelMacros.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtBRDF.hlsl" // 引入 PBR BRDF 函数，当前单主光和临时间接高光都会复用这里的 Fresnel/粗糙度工具
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBuffer.hlsl" // 引入 shader-only GBuffer 编解码和 PBRData 桥接函数；这里不采样真实 RT，也不触�?RenderTarget 生命周期�?
// 保存从当前着色点指向主方向光的世界空间方向，�?Burt Setup Lighting Pass 上传
float4 _BurtMainLightDirection;

// 保存主方向光颜色，由 Burt Setup Lighting Pass �?Unity 可见光数据中上传
float4 _BurtMainLightColor;

// 第一版追加光源采用全局数组；和 C# BurtLightingData.MaxAdditionalLights 保持一致
#define BURT_MAX_ADDITIONAL_LIGHTS 32

float _BurtAdditionalLightCount;
float4 _BurtAdditionalLightPositionAndRange[BURT_MAX_ADDITIONAL_LIGHTS];
float4 _BurtAdditionalLightColorAndType[BURT_MAX_ADDITIONAL_LIGHTS];
float4 _BurtAdditionalLightDirectionAndSpot[BURT_MAX_ADDITIONAL_LIGHTS];
float4 _BurtAdditionalLightSpotParams[BURT_MAX_ADDITIONAL_LIGHTS];

#define BURT_ADDITIONAL_LIGHT_BUFFER_ROWS 4

#if defined(BURT_USE_ADDITIONAL_LIGHT_BUFFER)
StructuredBuffer<float4> _BurtAdditionalLightBuffer;
float _BurtAdditionalLightBufferEnabled;
#endif

#if defined(BURT_USE_TILED_LIGHTING)
StructuredBuffer<uint> _BurtTileLightCountBuffer;
StructuredBuffer<uint> _BurtTileLightListBuffer;
StructuredBuffer<uint2> _BurtTileLightOffsetBuffer;
float4 _BurtTileLightGridParams;
float _BurtTileLightCountBufferEnabled;
StructuredBuffer<uint> _BurtClusterLightCountBuffer;
StructuredBuffer<uint> _BurtClusterLightListBuffer;
StructuredBuffer<uint2> _BurtClusterLightOffsetBuffer;
float4 _BurtClusterLightGridParams;
float4 _BurtClusterLightDepthParams;
float4 _BurtClusterLightWorldToViewZ;
float _BurtClusterLightBufferEnabled;
#endif

static const float BURT_LIGHT_TYPE_DIRECTIONAL = 0.0f;
static const float BURT_LIGHT_TYPE_POINT = 1.0f;
static const float BURT_LIGHT_TYPE_SPOT = 2.0f;

float BurtSampleAdditionalLightShadow(int lightIndex, float3 positionWS, float3 lightDirectionWS, float3 normalWS, float3 lightPositionWS);
bool BurtGetAdditionalLightShadowProjectionDebug(
    int lightIndex,
    float3 positionWS,
    float3 lightPositionWS,
    float3 lightDirectionWS,
    float3 normalWS,
    out float3 faceColor,
    out float3 uvColor,
    out float3 depthColor,
    out float3 depthDeltaColor);

// 保存 BurtRP 当前环境光颜色，Simple Lit 会直接使用它，PBR 间接光会把它作为 SH / Sky Reflection 兜底
float4 _BurtAmbientLightColor;

// 保存 BurtRP 自己上传�?ambient probe SH R 通道 L0/L1 常量，Deferred 全屏 Pass 会优先使用它
float4 _BurtAmbientSHAr;

// 保存 BurtRP 自己上传�?ambient probe SH G 通道 L0/L1 常量，Deferred 全屏 Pass 会优先使用它
float4 _BurtAmbientSHAg;

// 保存 BurtRP 自己上传�?ambient probe SH B 通道 L0/L1 常量，Deferred 全屏 Pass 会优先使用它
float4 _BurtAmbientSHAb;

// 保存 BurtRP 自己上传�?ambient probe SH R 通道 L2 常量，匹�?UnityCG ShadeSH9 打包方式
float4 _BurtAmbientSHBr;

// 保存 BurtRP 自己上传�?ambient probe SH G 通道 L2 常量，匹�?UnityCG ShadeSH9 打包方式
float4 _BurtAmbientSHBg;

// 保存 BurtRP 自己上传�?ambient probe SH B 通道 L2 常量，匹�?UnityCG ShadeSH9 打包方式
float4 _BurtAmbientSHBb;

// 保存 BurtRP 自己上传�?ambient probe SH C 项常量，匹配 UnityCG ShadeSH9 打包方式
float4 _BurtAmbientSHC;

// 标记 BurtRP 自己�?ambient SH 是否已经上传�? 表示可用�? 表示回退�?Unity 内置 ShadeSH9
float _BurtAmbientSHEnabled;

// 保存 BurtRP 全局天空反射 cubemap；UNITY_DECLARE_TEXCUBE 会同时声明纹理和 sampler_BurtSkyReflectionTexture，匹�?UNITY_SAMPLE_TEXCUBE_LOD 的宏展开
UNITY_DECLARE_TEXCUBE(_BurtSkyReflectionTexture);
UNITY_DECLARE_TEXCUBE(_BurtSkyReflectionSourceTexture);
UNITY_DECLARE_TEXCUBE(_BurtSkyDiffuseCubemapTexture);
sampler2D _BurtSkyDiffuseSHTexture;

// 保存 BurtRP 全局天空反射 HDR 解码参数，当前第一版默认按原始 RGB 使用
float4 _BurtSkyReflectionHDR;

// 保存 BurtRP 全局天空反射强度，对�?Unity Lighting 面板�?Reflection Intensity
float _BurtSkyReflectionIntensity;

// Optional tint supplied by BurtSkyLight; legacy RenderSettings path uploads white.
float4 _BurtSkyReflectionTint;

// 标记 BurtRP 全局天空反射 cubemap 是否可用�? 表示回退到环境光颜色
float _BurtSkyReflectionEnabled;

// 标记 SkyLight 是否显式接管 specular；启用时�?cubemap/零强度应返回黑色，而不是回退 unity_SpecCube0
float _BurtSkyReflectionOverride;

// 保存 BurtRP 全局天空反射 cubemap 的最�?mip 索引，避免所�?cubemap 都写死按 0..6 采样
float _BurtSkyReflectionMaxMip;
float4 _BurtSkyReflectionSourceHDR;
float _BurtSkyReflectionSourceEnabled;
float _BurtSkyReflectionSourceMaxMip;

// 保存 SkyLight 指定 cubemap 的水平旋转参数，xy 分别�?cos/sin
float4 _BurtSkyReflectionRotation;

// SpecifiedCubemap 可用同一�?cubemap 以高 mip 近似 diffuse 环境光，作为完整 SH/卷积链路前的第一版
float4 _BurtSkyDiffuseCubemapHDR;
float _BurtSkyDiffuseCubemapEnabled;
float _BurtSkyDiffuseSHEnabled;
float _BurtSkyDiffuseCubemapIntensity;
float4 _BurtSkyDiffuseCubemapTint;
float _BurtSkyDiffuseCubemapMip;

// 保存 SkyLight 下半球覆盖参数；diffuse/specular 分开预乘各自强度，alpha 是覆盖混合权重
float _BurtSkyLowerHemisphereEnabled;
float4 _BurtSkyLowerHemisphereDiffuseColor;
float4 _BurtSkyLowerHemisphereSpecularColor;

// 保存 BurtRP 当前光照函数需要的一盏灯的数据
struct BurtLight
{
    // 保存从表面点指向灯光的世界空间单位方向
float3 directionWS;

    // 保存灯光 RGB 颜色，用于直接漫反射和直接高光计算
float3 color;

    // 保存阴影可见性，1 表示完全受光�? 表示完全在阴影中
float shadowAttenuation;
};

// Creates the current main light from BurtRP globals.
BurtLight BurtCreateMainLight(float shadowAttenuation)
{
    BurtLight light;
    light.directionWS = BurtSafeNormalize(_BurtMainLightDirection.xyz);
    light.color = _BurtMainLightColor.rgb;
    light.shadowAttenuation = shadowAttenuation;
    return light;
}

int BurtGetAdditionalLightCount()
{
    return min((int)round(max(_BurtAdditionalLightCount, 0.0f)), BURT_MAX_ADDITIONAL_LIGHTS);
}

bool BurtUseTileLightList()
{
#if defined(BURT_USE_TILED_LIGHTING)
    int additionalLightCount = BurtGetAdditionalLightCount();
    return _BurtTileLightCountBufferEnabled > 0.5f &&
        additionalLightCount > 0 &&
        _BurtTileLightGridParams.x > 0.5f &&
        _BurtTileLightGridParams.y > 0.5f &&
        _BurtTileLightGridParams.w > 0.5f;
#else
    return false;
#endif
}

float2 BurtGetTileLightLookupUV(float2 screenUV)
{
    float2 tileUV = screenUV;
#if UNITY_UV_STARTS_AT_TOP
    tileUV.y = 1.0f - tileUV.y;
#endif
    return saturate(tileUV);
}

uint BurtGetTileLightIndex(float2 screenUV)
{
#if defined(BURT_USE_TILED_LIGHTING)
    float2 tileUV = BurtGetTileLightLookupUV(screenUV);
    uint tileCountX = (uint)max((int)round(_BurtTileLightGridParams.x), 1);
    uint tileCountY = (uint)max((int)round(_BurtTileLightGridParams.y), 1);
    uint tileX = (uint)floor(tileUV.x * (float)tileCountX);
    uint tileY = (uint)floor(tileUV.y * (float)tileCountY);
    tileX = min(tileX, tileCountX - 1u);
    tileY = min(tileY, tileCountY - 1u);
    return tileY * tileCountX + tileX;
#else
    return 0u;
#endif
}

uint2 BurtGetTileLightRange(float2 screenUV)
{
#if defined(BURT_USE_TILED_LIGHTING)
    if (!BurtUseTileLightList())
    {
        return uint2(0u, 0u);
    }

    uint tileIndex = BurtGetTileLightIndex(screenUV);
    uint2 range = _BurtTileLightOffsetBuffer[tileIndex];
    uint maxLightsPerTile = (uint)max((int)round(_BurtTileLightGridParams.w), 1);
    uint tileCountX = (uint)max((int)round(_BurtTileLightGridParams.x), 1);
    uint tileCountY = (uint)max((int)round(_BurtTileLightGridParams.y), 1);
    uint listCapacity = max(tileCountX * tileCountY * maxLightsPerTile, 1u);
    uint additionalLightCount = (uint)BurtGetAdditionalLightCount();
    uint countBufferCount = min(_BurtTileLightCountBuffer[tileIndex], maxLightsPerTile);
    range.x = min(range.x, listCapacity - 1u);
    range.y = min(range.y, min(countBufferCount, min(maxLightsPerTile, additionalLightCount)));
    range.y = min(range.y, listCapacity - range.x);
    return range;
#else
    return uint2(0u, 0u);
#endif
}

bool BurtTileLightListOverflows(float2 screenUV)
{
#if defined(BURT_USE_TILED_LIGHTING)
    if (!BurtUseTileLightList())
    {
        return false;
    }

    uint tileIndex = BurtGetTileLightIndex(screenUV);
    uint maxLightsPerTile = (uint)max((int)round(_BurtTileLightGridParams.w), 1);
    uint additionalLightCount = (uint)BurtGetAdditionalLightCount();
    uint storedCapacity = min(maxLightsPerTile, additionalLightCount);
    return _BurtTileLightCountBuffer[tileIndex] > storedCapacity;
#else
    return false;
#endif
}

bool BurtUseClusterLightList()
{
#if defined(BURT_USE_TILED_LIGHTING)
    int additionalLightCount = BurtGetAdditionalLightCount();
    return _BurtClusterLightBufferEnabled > 0.5f &&
        additionalLightCount > 0 &&
        _BurtClusterLightGridParams.x > 0.5f &&
        _BurtClusterLightGridParams.y > 0.5f &&
        _BurtClusterLightGridParams.z > 0.5f &&
        _BurtClusterLightGridParams.w > 0.5f &&
        _BurtClusterLightDepthParams.z > 0.0f;
#else
    return false;
#endif
}

uint BurtGetClusterLightIndex(float2 screenUV, float3 positionWS)
{
#if defined(BURT_USE_TILED_LIGHTING)
    float2 tileUV = BurtGetTileLightLookupUV(screenUV);
    uint tileCountX = (uint)max((int)round(_BurtClusterLightGridParams.x), 1);
    uint tileCountY = (uint)max((int)round(_BurtClusterLightGridParams.y), 1);
    uint depthSliceCount = (uint)max((int)round(_BurtClusterLightGridParams.z), 1);
    uint tileX = (uint)floor(tileUV.x * (float)tileCountX);
    uint tileY = (uint)floor(tileUV.y * (float)tileCountY);
    tileX = min(tileX, tileCountX - 1u);
    tileY = min(tileY, tileCountY - 1u);

    float viewDepth = max(dot(float4(positionWS, 1.0f), _BurtClusterLightWorldToViewZ), _BurtClusterLightDepthParams.x);
    float depth01 = saturate(log(viewDepth / max(_BurtClusterLightDepthParams.x, 0.0001f)) * _BurtClusterLightDepthParams.z);
    uint depthSlice = (uint)floor(depth01 * (float)depthSliceCount);
    depthSlice = min(depthSlice, depthSliceCount - 1u);
    return depthSlice * tileCountX * tileCountY + tileY * tileCountX + tileX;
#else
    return 0u;
#endif
}

uint2 BurtGetClusterLightRange(float2 screenUV, float3 positionWS)
{
#if defined(BURT_USE_TILED_LIGHTING)
    if (!BurtUseClusterLightList())
    {
        return uint2(0u, 0u);
    }

    uint clusterIndex = BurtGetClusterLightIndex(screenUV, positionWS);
    uint2 range = _BurtClusterLightOffsetBuffer[clusterIndex];
    uint tileCountX = (uint)max((int)round(_BurtClusterLightGridParams.x), 1);
    uint tileCountY = (uint)max((int)round(_BurtClusterLightGridParams.y), 1);
    uint depthSliceCount = (uint)max((int)round(_BurtClusterLightGridParams.z), 1);
    uint additionalLightCount = (uint)BurtGetAdditionalLightCount();
    uint maxLightsPerCluster = (uint)max((int)round(_BurtClusterLightGridParams.w), 1);
    uint maxPackedListCapacity = max(tileCountX * tileCountY * depthSliceCount * min(maxLightsPerCluster, additionalLightCount), 1u);
    uint countBufferCount = min(_BurtClusterLightCountBuffer[clusterIndex], maxLightsPerCluster);
    range.x = min(range.x, maxPackedListCapacity - 1u);
    range.y = min(range.y, min(countBufferCount, min(maxLightsPerCluster, additionalLightCount)));
    range.y = min(range.y, maxPackedListCapacity - range.x);
    return range;
#else
    return uint2(0u, 0u);
#endif
}

bool BurtClusterLightListOverflows(float2 screenUV, float3 positionWS)
{
#if defined(BURT_USE_TILED_LIGHTING)
    if (!BurtUseClusterLightList())
    {
        return false;
    }

    uint clusterIndex = BurtGetClusterLightIndex(screenUV, positionWS);
    uint maxLightsPerCluster = (uint)max((int)round(_BurtClusterLightGridParams.w), 1);
    uint additionalLightCount = (uint)BurtGetAdditionalLightCount();
    uint storedCapacity = min(maxLightsPerCluster, additionalLightCount);
    return _BurtClusterLightCountBuffer[clusterIndex] > storedCapacity;
#else
    return false;
#endif
}

bool BurtTryGetAdditionalLightListRange(float2 screenUV, float3 positionWS, out uint2 range, out uint useClusterLightList)
{
    range = uint2(0u, 0u);
    useClusterLightList = 0u;
#if defined(BURT_USE_TILED_LIGHTING)
    if (BurtUseClusterLightList() && !BurtClusterLightListOverflows(screenUV, positionWS))
    {
        range = BurtGetClusterLightRange(screenUV, positionWS);
        useClusterLightList = 1u;
        return true;
    }

    if (BurtUseTileLightList() && !BurtTileLightListOverflows(screenUV))
    {
        range = BurtGetTileLightRange(screenUV);
        return true;
    }
#endif

    return false;
}

uint BurtReadAdditionalLightListIndex(uint listIndex, uint useClusterLightList)
{
#if defined(BURT_USE_TILED_LIGHTING)
    return useClusterLightList > 0u ? _BurtClusterLightListBuffer[listIndex] : _BurtTileLightListBuffer[listIndex];
#else
    return 0u;
#endif
}

float4 BurtReadAdditionalLightPositionAndRange(int lightIndex)
{
#if defined(BURT_USE_ADDITIONAL_LIGHT_BUFFER)
    if (_BurtAdditionalLightBufferEnabled > 0.5f)
    {
        return _BurtAdditionalLightBuffer[lightIndex * BURT_ADDITIONAL_LIGHT_BUFFER_ROWS];
    }
#endif

    return _BurtAdditionalLightPositionAndRange[lightIndex];
}

float4 BurtReadAdditionalLightColorAndType(int lightIndex)
{
#if defined(BURT_USE_ADDITIONAL_LIGHT_BUFFER)
    if (_BurtAdditionalLightBufferEnabled > 0.5f)
    {
        return _BurtAdditionalLightBuffer[lightIndex * BURT_ADDITIONAL_LIGHT_BUFFER_ROWS + 1];
    }
#endif

    return _BurtAdditionalLightColorAndType[lightIndex];
}

float4 BurtReadAdditionalLightDirectionAndSpot(int lightIndex)
{
#if defined(BURT_USE_ADDITIONAL_LIGHT_BUFFER)
    if (_BurtAdditionalLightBufferEnabled > 0.5f)
    {
        return _BurtAdditionalLightBuffer[lightIndex * BURT_ADDITIONAL_LIGHT_BUFFER_ROWS + 2];
    }
#endif

    return _BurtAdditionalLightDirectionAndSpot[lightIndex];
}

float4 BurtReadAdditionalLightSpotParams(int lightIndex)
{
#if defined(BURT_USE_ADDITIONAL_LIGHT_BUFFER)
    if (_BurtAdditionalLightBufferEnabled > 0.5f)
    {
        return _BurtAdditionalLightBuffer[lightIndex * BURT_ADDITIONAL_LIGHT_BUFFER_ROWS + 3];
    }
#endif

    return _BurtAdditionalLightSpotParams[lightIndex];
}

float BurtEvaluateAdditionalLightDistanceAttenuation(float distanceSquared, float range)
{
    float safeRange = max(range, 0.0001f);
    float rangeFade = saturate(1.0f - distanceSquared / max(safeRange * safeRange, BURT_EPSILON));
    return rangeFade * rangeFade * rcp(max(distanceSquared, 0.25f));
}

BurtLight BurtCreateAdditionalLightInternal(int lightIndex, float3 positionWS, float3 normalWS, bool sampleShadow, float3 shadowPositionWS)
{
    BurtLight light;
    light.directionWS = float3(0.0f, 1.0f, 0.0f);
    light.color = float3(0.0f, 0.0f, 0.0f);
    light.shadowAttenuation = 1.0f;

    float4 colorAndType = BurtReadAdditionalLightColorAndType(lightIndex);
    float lightType = colorAndType.w;

    if (lightType < 0.5f)
    {
        light.directionWS = BurtSafeNormalize(BurtReadAdditionalLightDirectionAndSpot(lightIndex).xyz);
        light.color = max(colorAndType.rgb, float3(0.0f, 0.0f, 0.0f));
        return light;
    }

    float4 positionAndRange = BurtReadAdditionalLightPositionAndRange(lightIndex);
    float3 toLight = positionAndRange.xyz - positionWS;
    float distanceSquared = dot(toLight, toLight);
    light.directionWS = BurtSafeNormalize(toLight);

    float attenuation = BurtEvaluateAdditionalLightDistanceAttenuation(distanceSquared, positionAndRange.w);

    if (lightType > 1.5f)
    {
        float3 spotDirectionWS = BurtSafeNormalize(BurtReadAdditionalLightDirectionAndSpot(lightIndex).xyz);
        float3 fromLightDirectionWS = -light.directionWS;
        float spotCos = dot(fromLightDirectionWS, spotDirectionWS);
        float3 spotParams = BurtReadAdditionalLightSpotParams(lightIndex).xyz;
        float spotFade = saturate((spotCos - spotParams.y) * spotParams.z);
        attenuation *= spotFade * spotFade;
    }

    light.color = max(colorAndType.rgb, float3(0.0f, 0.0f, 0.0f)) * attenuation;
    light.shadowAttenuation = sampleShadow && lightType > 0.5f ? BurtSampleAdditionalLightShadow(lightIndex, shadowPositionWS, light.directionWS, normalWS, positionAndRange.xyz) : 1.0f;
    return light;
}

BurtLight BurtCreateAdditionalLightInternal(int lightIndex, float3 positionWS, float3 normalWS, bool sampleShadow)
{
    return BurtCreateAdditionalLightInternal(lightIndex, positionWS, normalWS, sampleShadow, positionWS);
}

BurtLight BurtCreateAdditionalLight(int lightIndex, float3 positionWS, float3 normalWS)
{
    return BurtCreateAdditionalLightInternal(lightIndex, positionWS, normalWS, true);
}

BurtLight BurtCreateAdditionalLight(int lightIndex, float3 positionWS, float3 normalWS, float3 shadowPositionWS)
{
    return BurtCreateAdditionalLightInternal(lightIndex, positionWS, normalWS, true, shadowPositionWS);
}

BurtLight BurtCreateAdditionalLight(int lightIndex, float3 positionWS)
{
    return BurtCreateAdditionalLightInternal(lightIndex, positionWS, float3(0.0f, 0.0f, 0.0f), true);
}

BurtLight BurtCreateAdditionalLightUnshadowed(int lightIndex, float3 positionWS, float3 normalWS)
{
    return BurtCreateAdditionalLightInternal(lightIndex, positionWS, normalWS, false);
}

BurtLight BurtCreateAdditionalLightUnshadowed(int lightIndex, float3 positionWS)
{
    return BurtCreateAdditionalLightInternal(lightIndex, positionWS, float3(0.0f, 0.0f, 0.0f), false);
}

float BurtEvaluateAdditionalShadowAttenuationDebug(float3 positionWS, float3 normalWS)
{
    float attenuation = 1.0f;
    int additionalLightCount = BurtGetAdditionalLightCount();

    [loop]
    for (int lightIndex = 0; lightIndex < BURT_MAX_ADDITIONAL_LIGHTS; lightIndex++)
    {
        if (lightIndex >= additionalLightCount)
        {
            break;
        }

        BurtLight additionalLight = BurtCreateAdditionalLight(lightIndex, positionWS, normalWS);
        if (dot(additionalLight.color, float3(0.2126f, 0.7152f, 0.0722f)) <= 0.0001f)
        {
            continue;
        }

        attenuation = min(attenuation, saturate(additionalLight.shadowAttenuation));
    }

    return attenuation;
}

float BurtEvaluateAdditionalShadowAttenuationDebug(float3 positionWS)
{
    return BurtEvaluateAdditionalShadowAttenuationDebug(positionWS, float3(0.0f, 0.0f, 0.0f));
}

float BurtEvaluateAdditionalShadowAttenuationDebug(float3 positionWS, float3 normalWS, float2 screenUV)
{
#if defined(BURT_USE_TILED_LIGHTING)
    uint2 range = uint2(0u, 0u);
    uint useClusterLightList = 0u;
    if (!BurtTryGetAdditionalLightListRange(screenUV, positionWS, range, useClusterLightList))
    {
        return BurtEvaluateAdditionalShadowAttenuationDebug(positionWS, normalWS);
    }

    float attenuation = 1.0f;
    int additionalLightCount = BurtGetAdditionalLightCount();

    [loop]
    for (uint localLightIndex = 0u; localLightIndex < BURT_MAX_ADDITIONAL_LIGHTS; localLightIndex++)
    {
        if (localLightIndex >= range.y)
        {
            break;
        }

        uint storedLightIndex = BurtReadAdditionalLightListIndex(range.x + localLightIndex, useClusterLightList);
        if (storedLightIndex >= (uint)additionalLightCount)
        {
            continue;
        }

        BurtLight additionalLight = BurtCreateAdditionalLight((int)storedLightIndex, positionWS, normalWS);
        if (dot(additionalLight.color, float3(0.2126f, 0.7152f, 0.0722f)) <= 0.0001f)
        {
            continue;
        }

        attenuation = min(attenuation, saturate(additionalLight.shadowAttenuation));
    }

    return attenuation;
#else
    return BurtEvaluateAdditionalShadowAttenuationDebug(positionWS, normalWS);
#endif
}

float BurtEvaluateAdditionalShadowAttenuationDebug(float3 positionWS, float2 screenUV)
{
    return BurtEvaluateAdditionalShadowAttenuationDebug(positionWS, float3(0.0f, 0.0f, 0.0f), screenUV);
}

bool BurtTryFillAdditionalLightShadowProjectionDebugForLight(
    int lightIndex,
    float3 positionWS,
    float3 normalWS,
    out float3 faceColor,
    out float3 uvColor,
    out float3 depthColor,
    out float3 depthDeltaColor)
{
    faceColor = float3(0.0f, 0.0f, 0.0f);
    uvColor = float3(0.0f, 0.0f, 0.0f);
    depthColor = float3(0.0f, 0.0f, 0.0f);
    depthDeltaColor = float3(0.0f, 0.0f, 0.0f);

    float4 positionAndRange = BurtReadAdditionalLightPositionAndRange(lightIndex);
    BurtLight additionalLight = BurtCreateAdditionalLightUnshadowed(lightIndex, positionWS, normalWS);
    if (dot(additionalLight.color, float3(0.2126f, 0.7152f, 0.0722f)) <= 0.0001f)
    {
        return false;
    }

    return BurtGetAdditionalLightShadowProjectionDebug(
        lightIndex,
        positionWS,
        positionAndRange.xyz,
        additionalLight.directionWS,
        normalWS,
        faceColor,
        uvColor,
        depthColor,
        depthDeltaColor);
}

void BurtFillAdditionalLightShadowProjectionDebugData(
    float3 positionWS,
    float3 normalWS,
    out float3 faceColor,
    out float3 uvColor,
    out float3 depthColor,
    out float3 depthDeltaColor)
{
    faceColor = float3(0.0f, 0.0f, 0.0f);
    uvColor = float3(0.0f, 0.0f, 0.0f);
    depthColor = float3(0.0f, 0.0f, 0.0f);
    depthDeltaColor = float3(0.0f, 0.0f, 0.0f);

    int additionalLightCount = BurtGetAdditionalLightCount();
    [loop]
    for (int lightIndex = 0; lightIndex < BURT_MAX_ADDITIONAL_LIGHTS; lightIndex++)
    {
        if (lightIndex >= additionalLightCount)
        {
            break;
        }

        if (BurtTryFillAdditionalLightShadowProjectionDebugForLight(lightIndex, positionWS, normalWS, faceColor, uvColor, depthColor, depthDeltaColor))
        {
            return;
        }
    }
}

void BurtFillAdditionalLightShadowProjectionDebugData(
    float3 positionWS,
    float3 normalWS,
    float2 screenUV,
    out float3 faceColor,
    out float3 uvColor,
    out float3 depthColor,
    out float3 depthDeltaColor)
{
#if defined(BURT_USE_TILED_LIGHTING)
    uint2 range = uint2(0u, 0u);
    uint useClusterLightList = 0u;
    if (!BurtTryGetAdditionalLightListRange(screenUV, positionWS, range, useClusterLightList))
    {
        BurtFillAdditionalLightShadowProjectionDebugData(positionWS, normalWS, faceColor, uvColor, depthColor, depthDeltaColor);
        return;
    }

    faceColor = float3(0.0f, 0.0f, 0.0f);
    uvColor = float3(0.0f, 0.0f, 0.0f);
    depthColor = float3(0.0f, 0.0f, 0.0f);
    depthDeltaColor = float3(0.0f, 0.0f, 0.0f);

    int additionalLightCount = BurtGetAdditionalLightCount();

    [loop]
    for (uint localLightIndex = 0u; localLightIndex < BURT_MAX_ADDITIONAL_LIGHTS; localLightIndex++)
    {
        if (localLightIndex >= range.y)
        {
            break;
        }

        uint storedLightIndex = BurtReadAdditionalLightListIndex(range.x + localLightIndex, useClusterLightList);
        if (storedLightIndex >= (uint)additionalLightCount)
        {
            continue;
        }

        if (BurtTryFillAdditionalLightShadowProjectionDebugForLight((int)storedLightIndex, positionWS, normalWS, faceColor, uvColor, depthColor, depthDeltaColor))
        {
            return;
        }
    }
#else
    BurtFillAdditionalLightShadowProjectionDebugData(positionWS, normalWS, faceColor, uvColor, depthColor, depthDeltaColor);
#endif
}

// Returns the ambient color uploaded by BurtRP.
float3 BurtGetAmbientLightColor()
{
    return max(_BurtAmbientLightColor.rgb, float3(0.0f, 0.0f, 0.0f));
}

float BurtLuminanceForIndirectFallback(float3 color)
{
    float3 safeColor = max(color, float3(0.0f, 0.0f, 0.0f));
    return dot(safeColor, float3(0.2126f, 0.7152f, 0.0722f));
}

float3 BurtSelectIndirectFallbackIfBlack(float3 sampledColor, float3 fallbackColor)
{
    float sampledLuminance = BurtLuminanceForIndirectFallback(sampledColor);
    float useSampledColor = step(0.0001f, sampledLuminance);
    return lerp(max(fallbackColor, float3(0.0f, 0.0f, 0.0f)), max(sampledColor, float3(0.0f, 0.0f, 0.0f)), useSampledColor);
}

float3 BurtApplySkyLowerHemisphere(float3 sourceColor, float3 directionWS, float4 lowerHemisphereColor)
{
    float lowerBlend = (_BurtSkyLowerHemisphereEnabled > 0.5f && BurtSafeNormalize(directionWS).y < 0.0f) ? saturate(lowerHemisphereColor.a) : 0.0f;
    return lerp(max(sourceColor, float3(0.0f, 0.0f, 0.0f)), max(lowerHemisphereColor.rgb, float3(0.0f, 0.0f, 0.0f)), lowerBlend);
}

float3 BurtRotateSkyReflectionDirection(float3 directionWS)
{
    float3 safeDirectionWS = BurtSafeNormalize(directionWS);
    float cosPhi = _BurtSkyReflectionRotation.x;
    float sinPhi = _BurtSkyReflectionRotation.y;
    float3 rotDirX = float3(cosPhi, 0.0f, -sinPhi);
    float3 rotDirZ = float3(sinPhi, 0.0f, cosPhi);
    return BurtSafeNormalize(float3(dot(rotDirX, safeDirectionWS), safeDirectionWS.y, dot(rotDirZ, safeDirectionWS)));
}

float3 BurtSampleSkyDiffuseCubemap(float3 normalWS)
{
    float3 safeNormalWS = BurtSafeNormalize(normalWS);
    float3 skySampleDirectionWS = BurtRotateSkyReflectionDirection(safeNormalWS);
    float4 encodedSkyDiffuse = UNITY_SAMPLE_TEXCUBE_LOD(_BurtSkyDiffuseCubemapTexture, skySampleDirectionWS, max(_BurtSkyDiffuseCubemapMip, 0.0f));
    float3 skyDiffuse = DecodeHDR(encodedSkyDiffuse, _BurtSkyDiffuseCubemapHDR) * max(_BurtSkyDiffuseCubemapTint.rgb, float3(0.0f, 0.0f, 0.0f)) * max(_BurtSkyDiffuseCubemapIntensity, 0.0f);
    if (_BurtSkyReflectionSourceEnabled > 0.5f)
    {
        float sourceDiffuseMip = max(_BurtSkyReflectionSourceMaxMip, 0.0f);
        float4 encodedSourceDiffuse = UNITY_SAMPLE_TEXCUBE_LOD(_BurtSkyReflectionSourceTexture, skySampleDirectionWS, sourceDiffuseMip);
        float3 sourceDiffuse = DecodeHDR(encodedSourceDiffuse, _BurtSkyReflectionSourceHDR) * max(_BurtSkyDiffuseCubemapTint.rgb, float3(0.0f, 0.0f, 0.0f)) * max(_BurtSkyDiffuseCubemapIntensity, 0.0f);
        skyDiffuse = BurtSelectIndirectFallbackIfBlack(skyDiffuse, sourceDiffuse);
    }

    skyDiffuse = BurtApplySkyLowerHemisphere(skyDiffuse, safeNormalWS, _BurtSkyLowerHemisphereDiffuseColor);
    return max(skyDiffuse, float3(0.0f, 0.0f, 0.0f));
}

float4 BurtSampleSkyDiffuseSHPacked(float index)
{
    float u = (index + 0.5f) / 7.0f;
    return tex2Dlod(_BurtSkyDiffuseSHTexture, float4(u, 0.5f, 0.0f, 0.0f));
}

float3 BurtEvaluateSkyDiffuseSH9(float3 normalWS)
{
    float3 safeNormalWS = BurtSafeNormalize(normalWS);
    float3 skySampleDirectionWS = BurtRotateSkyReflectionDirection(safeNormalWS);
    float4 shAr = BurtSampleSkyDiffuseSHPacked(0.0f);
    float4 shAg = BurtSampleSkyDiffuseSHPacked(1.0f);
    float4 shAb = BurtSampleSkyDiffuseSHPacked(2.0f);
    float4 shBr = BurtSampleSkyDiffuseSHPacked(3.0f);
    float4 shBg = BurtSampleSkyDiffuseSHPacked(4.0f);
    float4 shBb = BurtSampleSkyDiffuseSHPacked(5.0f);
    float4 shC = BurtSampleSkyDiffuseSHPacked(6.0f);
    float4 shNormal = float4(skySampleDirectionWS, 1.0f);

    float3 linearL0L1;
    linearL0L1.r = dot(shAr, shNormal);
    linearL0L1.g = dot(shAg, shNormal);
    linearL0L1.b = dot(shAb, shNormal);

    float4 vB = shNormal.xyzz * shNormal.yzzx;
    float3 linearL2;
    linearL2.r = dot(shBr, vB);
    linearL2.g = dot(shBg, vB);
    linearL2.b = dot(shBb, vB);

    float vC = skySampleDirectionWS.x * skySampleDirectionWS.x - skySampleDirectionWS.y * skySampleDirectionWS.y;
    float3 shIrradiance = (linearL0L1 + linearL2 + shC.rgb * vC) * max(_BurtSkyDiffuseCubemapTint.rgb, float3(0.0f, 0.0f, 0.0f)) * max(_BurtSkyDiffuseCubemapIntensity, 0.0f);
    shIrradiance = BurtApplySkyLowerHemisphere(shIrradiance, safeNormalWS, _BurtSkyLowerHemisphereDiffuseColor);
    return max(shIrradiance, float3(0.0f, 0.0f, 0.0f));
}

float3 BurtEvaluateAmbientSH9(float3 normalWS)
{
    float3 safeNormalWS = BurtSafeNormalize(normalWS);
    float4 shNormal = float4(safeNormalWS, 1.0f);

    float3 linearL0L1;
    linearL0L1.r = dot(_BurtAmbientSHAr, shNormal);
    linearL0L1.g = dot(_BurtAmbientSHAg, shNormal);
    linearL0L1.b = dot(_BurtAmbientSHAb, shNormal);

    float4 vB = shNormal.xyzz * shNormal.yzzx;
    float3 linearL2;
    linearL2.r = dot(_BurtAmbientSHBr, vB);
    linearL2.g = dot(_BurtAmbientSHBg, vB);
    linearL2.b = dot(_BurtAmbientSHBb, vB);

    float vC = safeNormalWS.x * safeNormalWS.x - safeNormalWS.y * safeNormalWS.y;
    float3 shIrradiance = linearL0L1 + linearL2 + _BurtAmbientSHC.rgb * vC;
    shIrradiance = BurtApplySkyLowerHemisphere(shIrradiance, safeNormalWS, _BurtSkyLowerHemisphereDiffuseColor);

#ifdef UNITY_COLORSPACE_GAMMA
    shIrradiance = pow(max(shIrradiance, float3(0.0f, 0.0f, 0.0f)), 1.0f / 2.2f);
#endif

    return max(shIrradiance, float3(0.0f, 0.0f, 0.0f));
}

#if BURT_ENABLE_SUBSURFACE_SHADING
float3 BurtEvaluateSubsurface3SSH9(float curvature, float profileIndex, float3 normalWS)
{
    float3 safeNormalWS = BurtSafeNormalize(normalWS);
    float3 zh0 = BurtSampleSubsurfaceSHLut(curvature, 0.0f, profileIndex);
    float3 zh1 = BurtSampleSubsurfaceSHLut(curvature, 1.0f, profileIndex);
    float3 zh2 = BurtSampleSubsurfaceSHLut(curvature, 2.0f, profileIndex);

    float4 shAr;
    float4 shAg;
    float4 shAb;
    float4 shBr;
    float4 shBg;
    float4 shBb;
    float4 shC;
    float3 shNormalDirection = safeNormalWS;

    if (_BurtSkyDiffuseSHEnabled > 0.5f)
    {
        shNormalDirection = BurtRotateSkyReflectionDirection(safeNormalWS);
        shAr = BurtSampleSkyDiffuseSHPacked(0.0f);
        shAg = BurtSampleSkyDiffuseSHPacked(1.0f);
        shAb = BurtSampleSkyDiffuseSHPacked(2.0f);
        shBr = BurtSampleSkyDiffuseSHPacked(3.0f);
        shBg = BurtSampleSkyDiffuseSHPacked(4.0f);
        shBb = BurtSampleSkyDiffuseSHPacked(5.0f);
        shC = BurtSampleSkyDiffuseSHPacked(6.0f);
    }
    else if (_BurtAmbientSHEnabled > 0.5f)
    {
        shAr = _BurtAmbientSHAr;
        shAg = _BurtAmbientSHAg;
        shAb = _BurtAmbientSHAb;
        shBr = _BurtAmbientSHBr;
        shBg = _BurtAmbientSHBg;
        shBb = _BurtAmbientSHBb;
        shC = _BurtAmbientSHC;
    }
    else
    {
        shAr = unity_SHAr;
        shAg = unity_SHAg;
        shAb = unity_SHAb;
        shBr = unity_SHBr;
        shBg = unity_SHBg;
        shBb = unity_SHBb;
        shC = unity_SHC;
    }

    shAr.xyz *= zh1.x;
    shAr.w *= zh0.x;
    shAg.xyz *= zh1.y;
    shAg.w *= zh0.y;
    shAb.xyz *= zh1.z;
    shAb.w *= zh0.z;

    float4 shNormal = float4(shNormalDirection, 1.0f);
    float3 linearL0L1;
    linearL0L1.r = dot(shAr, shNormal);
    linearL0L1.g = dot(shAg, shNormal);
    linearL0L1.b = dot(shAb, shNormal);

    shBr *= zh2.x;
    shBg *= zh2.y;
    shBb *= zh2.z;
    shC.xyz *= zh2.xyz;

    float4 vB = shNormal.xyzz * shNormal.yzzx;
    float3 linearL2;
    linearL2.r = dot(shBr, vB);
    linearL2.g = dot(shBg, vB);
    linearL2.b = dot(shBb, vB);

    float vC = shNormalDirection.x * shNormalDirection.x - shNormalDirection.y * shNormalDirection.y;
    float3 shIrradiance = linearL0L1 + linearL2 + shC.rgb * vC;
    if (_BurtSkyDiffuseSHEnabled > 0.5f)
    {
        shIrradiance *= max(_BurtSkyDiffuseCubemapTint.rgb, float3(0.0f, 0.0f, 0.0f)) * max(_BurtSkyDiffuseCubemapIntensity, 0.0f);
    }

    shIrradiance = BurtApplySkyLowerHemisphere(shIrradiance, safeNormalWS, _BurtSkyLowerHemisphereDiffuseColor);

#ifdef UNITY_COLORSPACE_GAMMA
    shIrradiance = pow(max(shIrradiance, float3(0.0f, 0.0f, 0.0f)), 1.0f / 2.2f);
#endif

    return max(shIrradiance, float3(0.0f, 0.0f, 0.0f));
}
#endif

// 计算经典 Lambert 漫反射项
float BurtLambert(float3 normalWS, float3 lightDirectionWS)
{
    // 点乘结果限制�?0 �?1，避免背光面产生负光照
return saturate(dot(normalWS, lightDirectionWS));
}

// 计算一�?BurtLight 对表面的直接漫反射贡献
float3 BurtEvaluateDiffuse(float3 baseColor, BurtLight light, float3 normalWS)
{
    // 计算 N dot L，表示表面朝向光源的程度
float diffuseTerm = BurtLambert(normalWS, light.directionWS);

    // �?albedo、灯光颜色、Lambert 项和阴影衰减相乘，得到直接漫反射颜色
return baseColor * light.color * diffuseTerm * light.shadowAttenuation;
}

// 计算带环境遮蔽的环境光部分
float3 BurtEvaluateAmbientOccluded(float3 baseColor, float3 ambientColor, float occlusion)
{
    // �?albedo、环境光颜色�?AO 相乘，让 Mask Map 只压暗间接环境项，不影响直接光
return baseColor * ambientColor * saturate(occlusion);
}

// 计算当前简�?Lit 模型的环境光部分
float3 BurtEvaluateAmbient(float3 baseColor, float3 ambientColor)
{
    // 旧调用不�?occlusion，所以默认按 1 处理，保持原有环境光亮度
return BurtEvaluateAmbientOccluded(baseColor, ambientColor, 1.0f);
}

// 采样 BurtRP 当前的间接漫反射环境照度
float3 BurtSampleIndirectDiffuseIrradiance(float3 normalWS)
{
    if (_BurtSkyDiffuseSHEnabled > 0.5f)
    {
        return BurtEvaluateSkyDiffuseSH9(normalWS);
    }

    if (_BurtSkyDiffuseCubemapEnabled > 0.5f)
    {
        return BurtSampleSkyDiffuseCubemap(normalWS);
    }

    if (_BurtAmbientSHEnabled > 0.5f)
    {
        return BurtEvaluateAmbientSH9(normalWS);
    }

    float3 safeNormalWS = BurtSafeNormalize(normalWS); // 归一化世界空间法线，�?Unity SH 查询使用稳定方向�?
    float4 shNormal = float4(safeNormalWS, 1.0f); // �?normal 扩展�?float4，因�?Unity �?ShadeSH9 约定 xyz 是方向，w 是常数项�?
    float3 shIrradiance = ShadeSH9(shNormal); // 兼容旧路径：直接读取 Unity 内置 spherical harmonics
shIrradiance = BurtApplySkyLowerHemisphere(shIrradiance, safeNormalWS, _BurtSkyLowerHemisphereDiffuseColor); // �?legacy SH fallback 也遵�?SkyLight 下半球设置�?
    // �?Unity 内置 SH 没有�?DrawRenderers 刷新时，�?_BurtAmbientLightColor 兜底，避免间接漫反射全黑
return BurtSelectIndirectFallbackIfBlack(shIrradiance, BurtGetAmbientLightColor());
}


// XRender SkyLight �?256 cubemap �?9 �?mip 为基准；这里使用 max mip index 8 作为有效输入上限
static const float BURT_REFLECTION_CAPTURE_SPECULAR_MIP_MAX_INDEX = 8.0f;

// 出处：XRender/Shaders/Library/ShadingIBL.hlsl::ComputeReflectionCaptureMipFromRoughness；roughness 到反射探�?mip �?log2 曲线
float ComputeReflectionCaptureMipFromRoughness(float perceptualRoughness, float cubemapMaxMipIndex)
{
    // C# 上传的是真实最�?mip 索引；XRender 原函数输�?mip count 后会先减 1，所以这里直接使�?max index
float specularMipMaxIndex = min(max(cubemapMaxMipIndex, 0.0f), BURT_REFLECTION_CAPTURE_SPECULAR_MIP_MAX_INDEX);

    // log2 曲线不能接受 0，所以使�?BurtRP 的最小感知粗糙度保护镜面端
float safeRoughness = max(saturate(perceptualRoughness), BURT_MIN_PERCEPTUAL_ROUGHNESS);

    // 对齐 XRender / UE 的启发式：粗糙端走高 mip，光滑端尽量贴近 mip0
float levelFrom1x1 = 1.0f - 1.2f * log2(safeRoughness);

    // 对齐 XRender�?56 sky �?max index �?8，roughness=1 会落�?mip6，而不是之前错误的 mip4
return clamp(specularMipMaxIndex - 1.0f - levelFrom1x1, 0.0f, specularMipMaxIndex);
}

// BurtRP 适配函数：采�?BurtRP 全局天空反射 cubemap，旧路径才回退 Unity 当前绑定�?reflection probe
float3 BurtSkyCubeFaceUVToDirection(float face, float2 uv)
{
    float2 st = uv * 2.0f - 1.0f;
    if (face < 0.5f) return BurtSafeNormalize(float3(1.0f, -st.y, -st.x));
    if (face < 1.5f) return BurtSafeNormalize(float3(-1.0f, -st.y, st.x));
    if (face < 2.5f) return BurtSafeNormalize(float3(st.x, 1.0f, st.y));
    if (face < 3.5f) return BurtSafeNormalize(float3(st.x, -1.0f, -st.y));
    if (face < 4.5f) return BurtSafeNormalize(float3(st.x, -st.y, 1.0f));
    return BurtSafeNormalize(float3(-st.x, -st.y, -1.0f));
}

void BurtSkyDirectionToCubeFaceUV(float3 directionWS, out float face, out float2 uv)
{
    float3 dir = BurtSafeNormalize(directionWS);
    float3 absDir = abs(dir);

    if (absDir.x >= absDir.y && absDir.x >= absDir.z)
    {
        float invAxis = rcp(max(absDir.x, BURT_EPSILON));
        if (dir.x >= 0.0f)
        {
            face = 0.0f;
            uv = float2(-dir.z, -dir.y) * invAxis;
        }
        else
        {
            face = 1.0f;
            uv = float2(dir.z, -dir.y) * invAxis;
        }
    }
    else if (absDir.y >= absDir.z)
    {
        float invAxis = rcp(max(absDir.y, BURT_EPSILON));
        if (dir.y >= 0.0f)
        {
            face = 2.0f;
            uv = float2(dir.x, dir.z) * invAxis;
        }
        else
        {
            face = 3.0f;
            uv = float2(dir.x, -dir.z) * invAxis;
        }
    }
    else
    {
        float invAxis = rcp(max(absDir.z, BURT_EPSILON));
        if (dir.z >= 0.0f)
        {
            face = 4.0f;
            uv = float2(dir.x, -dir.y) * invAxis;
        }
        else
        {
            face = 5.0f;
            uv = float2(-dir.x, -dir.y) * invAxis;
        }
    }

    uv = uv * 0.5f + 0.5f;
}

float3 BurtApplySkyReflectionMipSeamScale(float3 directionWS, float mipLevel, float maxMipIndex)
{
    float safeMaxMip = max(maxMipIndex, 0.0f);
    if (safeMaxMip <= 0.5f)
    {
        return BurtSafeNormalize(directionWS);
    }

    float mipSize = exp2(max(safeMaxMip - floor(max(mipLevel, 0.0f)), 0.0f));
    float mipScale = saturate((mipSize - 2.0f) / max(mipSize, 1.0f));
    float face;
    float2 uv;
    BurtSkyDirectionToCubeFaceUV(directionWS, face, uv);
    uv = (uv - 0.5f) * mipScale + 0.5f;
    return BurtSkyCubeFaceUVToDirection(face, uv);
}

float3 SampleIndirectSpecularRadiance(float3 reflectionDirectionWS, float roughness)
{
    // 归一化反射方向，�?cubemap 采样方向稳定
float3 safeReflectionDirectionWS = BurtSafeNormalize(reflectionDirectionWS);

    if (_BurtSkyReflectionEnabled > 0.5f)
    {
        float3 skySampleDirectionWS = BurtRotateSkyReflectionDirection(safeReflectionDirectionWS); // 对指�?cubemap 应用 XRender 风格水平旋转；默认参数为 identity�?
        float skyReflectionMaxMip = max(_BurtSkyReflectionMaxMip, 0.0f); // 使用 C# 上传的实�?mip 上限，避免不同尺�?cubemap 都套用固�?6�?
        float skyReflectionMipLevel = ComputeReflectionCaptureMipFromRoughness(roughness, skyReflectionMaxMip); // 使用 XRender 风格 roughness->mip 曲线计算全局天空反射 LOD�?
        float4 encodedSkyReflection = UNITY_SAMPLE_TEXCUBE_LOD(_BurtSkyReflectionTexture, skySampleDirectionWS, skyReflectionMipLevel);
        float3 skyReflectionRadiance = DecodeHDR(encodedSkyReflection, _BurtSkyReflectionHDR) * max(_BurtSkyReflectionTint.rgb, float3(0.0f, 0.0f, 0.0f)) * max(0.0f, _BurtSkyReflectionIntensity); // 解码 HDR 并乘 Lighting 面板的反射强度
skyReflectionRadiance = BurtApplySkyLowerHemisphere(skyReflectionRadiance, safeReflectionDirectionWS, _BurtSkyLowerHemisphereSpecularColor); // 没有离线 convolve 时，用方向覆盖近�?XRender �?lower hemisphere 输入处理�?
        return max(skyReflectionRadiance, float3(0.0f, 0.0f, 0.0f));
    }

    if (_BurtSkyReflectionOverride > 0.5f)
    {
        return BurtApplySkyLowerHemisphere(float3(0.0f, 0.0f, 0.0f), safeReflectionDirectionWS, _BurtSkyLowerHemisphereSpecularColor);
    }

    const float legacyUnitySpecCubeMaxMip = 6.0f; // Unity 内置 reflection probe legacy 路径暂时保留 0..6 的常�?mip 上限，后续有专用数据源后再替换�?
    float legacyUnitySpecCubeMipLevel = ComputeReflectionCaptureMipFromRoughness(roughness, legacyUnitySpecCubeMaxMip); // �?legacy unity_SpecCube0 路径单独计算 mip，避免误用全局 sky �?mip 上限�?
    // 兼容旧路径：采样 Unity 当前绑定�?reflection probe / sky reflection cubemap
float4 encodedSpecular = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, safeReflectionDirectionWS, legacyUnitySpecCubeMipLevel);

    // 使用 Unity 提供�?HDR 解码参数�?RGBM/编码反射颜色还原为线�?HDR 颜色
float3 specularRadiance = DecodeHDR(encodedSpecular, unity_SpecCube0_HDR);

    // �?Deferred 全屏 Pass 没有 Renderer per-object Reflection Probe 时，用环境色做低频镜面兜底
return BurtSelectIndirectFallbackIfBlack(specularRadiance, BurtGetAmbientLightColor());
}

// 计算 PBR 间接漫反射：环境 irradiance 乘以 XRender EnergyPreservation 后的 diffuseColor
float3 BurtEvaluateIndirectDiffusePBR(BurtPBRMaterialData materialData, float3 normalWS, float energyPreservation)
{
    // 根据世界空间法线采样 Unity SH，得到当前方向的环境漫反射照度
float3 diffuseIrradiance = BurtSampleIndirectDiffuseIrradiance(normalWS);

    // XRender �?SlabOperator_Layering 会用 EnergyPreservation 缩放底层 diffuse，这里让 SH 间接漫反射也走同一保能比例
return materialData.diffuseColor * diffuseIrradiance * BurtGTAOMultiBounce(materialData.occlusion, materialData.baseColor) * saturate(energyPreservation);
}

// 保留 SurfaceData 旧入口：内部准备 PBRMaterialData，避免旧调用绕过 EnergyPreservation
    float3 BurtEvaluateIndirectDiffusePBR(BurtSurfaceData surfaceData, float3 normalWS, float energyPreservation)
{
    BurtPBRMaterialData materialData = BurtPreparePBRMaterialData(surfaceData);

    // 复用准备数据版本，保证旧接口�?Deferred 入口输出一致
return BurtEvaluateIndirectDiffusePBR(materialData, normalWS, energyPreservation);
}

// BurtRP 适配函数：对�?XRender GenericData.EnergyCompensation_GGX，间接高光直接读取统一准备好的 EnergyTerms
float3 GetIndirectSpecularEnergyCompensation(BurtPBRMaterialData materialData, BurtPBRGeometryData geometryData, BurtPBREnergyTerms energyTerms)
{
    // materialData �?geometryData 保留在签名里，方便调用点看出这个值依赖材�?F0、Base.Roughness �?NdotV
return energyTerms.indirectSpecularEnergyCompensation;
}

// 保留 SurfaceData 旧入口：内部统一准备 material / geometry / energy terms 后返回间接高光补偿
float3 GetIndirectSpecularEnergyCompensation(BurtSurfaceData surfaceData, float3 normalWS, float3 viewDirectionWS)
{
    BurtPBRMaterialData materialData = BurtPreparePBRMaterialData(surfaceData);

    BurtPBRGeometryData geometryData = BurtPreparePBRGeometryData(normalWS, viewDirectionWS);

    // 直接高光 roughness 只用于填充完�?energy terms，间接补偿本身仍使用材质 Base.Roughness
float directSpecularPerceptualRoughness = GetDirectSpecularPerceptualRoughness(materialData, geometryData);

    // 一次性准备所�?energy terms，再返回间接高光需要的那一项�?
    BurtPBREnergyTerms energyTerms = BurtPreparePBREnergyTerms(materialData, geometryData, directSpecularPerceptualRoughness);

    // 返回 XRender EnergyCompensation_GGX，对�?Slab_SkySpecular / Slab_EnvProbeSpecular 中的 Fs *= EnergyCompensation_GGX
return GetIndirectSpecularEnergyCompensation(materialData, geometryData, energyTerms);
}

// 计算 PBR 间接镜面反射：Reflection Probe radiance 乘以视角 Fresnel �?XRender EnergyCompensation_GGX
float3 BurtEvaluateIndirectSpecularPBR(BurtPBRMaterialData materialData, BurtPBRGeometryData geometryData, float3 indirectSpecularEnergyCompensation)
{
    // 间接高光使用材质 Base.Roughness，同时也用于选择 reflection probe 的模�?mip
float roughness = materialData.perceptualRoughness;

    // 对齐 XRender/UE 的近似：IBL 用各向异性修改后的法线求反射方向；anisotropy=0 时就是普�?reflect(N)
float3 reflectionDirectionWS = BurtGetIndirectSpecularReflectionDirectionWS(geometryData, materialData.anisotropy, roughness);

    // 采样 Unity 当前 reflection probe / sky reflection，得到已经按 roughness 预过滤过的环境高光
float3 specularRadiance = SampleIndirectSpecularRadiance(reflectionDirectionWS, roughness);

    // 优先�?PreintegratedFG LUT 读取 DFG.xy，未绑定时回退到解析近似
float2 dfg = GetSpecularDFGTerms(roughness, geometryData.nDotV);

    // �?DFG 应用�?F0/F90 上，比单�?Fresnel 更接近预积分环境 BRDF
float3 envBRDF = EvalSpecularDFG(materialData.f0, materialData.f90, dfg);

    // 根据 AO、NdotV 和粗糙度计算间接高光遮蔽；用 Specular Occlusion 替代直接�?AO，保留掠射角高光
float specularOcclusion = GetIndirectSpecularOcclusion(geometryData.nDotV, materialData.occlusion, roughness);

    // 出处：XRender/Shaders/SlabsIndirectLight/Slab_SkySpecular.hlsl �?Slab_EnvProbeSpecular.hlsl；先�?Fs 做能量补偿，再乘 SpecularOcclusion
return specularRadiance * envBRDF * indirectSpecularEnergyCompensation * specularOcclusion;
}

#if BURT_ENABLE_SUBSURFACE_SHADING
float3 BurtEvaluateSubsurfaceIndirectSpecularDualLobe(
    BurtPBRMaterialData materialData,
    BurtPBRGeometryData geometryData,
    float3 fallbackIndirectSpecularEnergyCompensation)
{
    float subsurfaceStrength = saturate(materialData.subsurfaceStrength);
    if (subsurfaceStrength <= 0.0001f)
    {
        return BurtEvaluateIndirectSpecularPBR(materialData, geometryData, fallbackIndirectSpecularEnergyCompensation);
    }

    float4 dualSpecular = BurtLoadSubsurfaceProfileDualSpecular(materialData.subsurfaceProfileIndex);
    float lobeMix = saturate(dualSpecular.z);
    float roughness0 = ClampPerceptualRoughness(materialData.perceptualRoughness * max(dualSpecular.x * BURT_SUBSURFACE_MAX_DUAL_SPECULAR_ROUGHNESS, 0.01f));
    float roughness1 = ClampPerceptualRoughness(materialData.perceptualRoughness * max(dualSpecular.y * BURT_SUBSURFACE_MAX_DUAL_SPECULAR_ROUGHNESS, 0.01f));

    float3 reflection0 = BurtGetIndirectSpecularReflectionDirectionWS(geometryData, materialData.anisotropy, roughness0);
    float3 reflection1 = BurtGetIndirectSpecularReflectionDirectionWS(geometryData, materialData.anisotropy, roughness1);
    float3 radiance0 = SampleIndirectSpecularRadiance(reflection0, roughness0);
    float3 radiance1 = SampleIndirectSpecularRadiance(reflection1, roughness1);

    float2 dfg0 = GetSpecularDFGTerms(roughness0, geometryData.nDotV);
    float2 dfg1 = GetSpecularDFGTerms(roughness1, geometryData.nDotV);
    float3 envBRDF0 = EvalSpecularDFG(materialData.f0, materialData.f90, dfg0);
    float3 envBRDF1 = EvalSpecularDFG(materialData.f0, materialData.f90, dfg1);

    float3 energy0;
    float preservation0;
    float3 energy1;
    float preservation1;
    GetSpecularEnergyTerms(materialData.f0, materialData.f90, roughness0, geometryData.nDotV, energy0, preservation0);
    GetSpecularEnergyTerms(materialData.f0, materialData.f90, roughness1, geometryData.nDotV, energy1, preservation1);

    float specularOcclusion0 = GetIndirectSpecularOcclusion(geometryData.nDotV, materialData.occlusion, roughness0);
    float specularOcclusion1 = GetIndirectSpecularOcclusion(geometryData.nDotV, materialData.occlusion, roughness1);
    float3 lobe0 = radiance0 * envBRDF0 * energy0 * specularOcclusion0;
    float3 lobe1 = radiance1 * envBRDF1 * energy1 * specularOcclusion1;
    float3 dualLobeSpecular = lerp(lobe0, lobe1, lobeMix);
    float3 singleLobeSpecular = BurtEvaluateIndirectSpecularPBR(materialData, geometryData, fallbackIndirectSpecularEnergyCompensation);
    return lerp(singleLobeSpecular, dualLobeSpecular, subsurfaceStrength);
}
#endif

// 保留 SurfaceData 旧入口：调用方不关心拆项时，在函数内部按 XRender 规则计算 indirect specular energy compensation
float3 BurtEvaluateIndirectSpecularPBR(BurtSurfaceData surfaceData, float3 normalWS, float3 viewDirectionWS, float3 indirectSpecularEnergyCompensation)
{
    BurtPBRMaterialData materialData = BurtPreparePBRMaterialData(surfaceData);

    BurtPBRGeometryData geometryData = BurtPreparePBRGeometryData(normalWS, viewDirectionWS);

    // 复用准备数据版本，确保间接高光补偿的应用位置一致
return BurtEvaluateIndirectSpecularPBR(materialData, geometryData, indirectSpecularEnergyCompensation);
}

// 保留 SurfaceData 旧入口：调用方不关心拆项时，在函数内部按 XRender 规则计算 indirect specular energy compensation
float3 BurtEvaluateIndirectSpecularPBR(BurtSurfaceData surfaceData, float3 normalWS, float3 viewDirectionWS)
{
    // 先计算间接高光能量补偿，再交给完整入口，避免旧调用绕�?XRender EnergyCompensation_GGX
float3 indirectSpecularEnergyCompensation = GetIndirectSpecularEnergyCompensation(surfaceData, normalWS, viewDirectionWS);

    // 复用带显式能量补偿的入口，保证旧接口�?Debug 拆项看到同一套结果
return BurtEvaluateIndirectSpecularPBR(surfaceData, normalWS, viewDirectionWS, indirectSpecularEnergyCompensation);
}

// 保存 PBR 间接光拆分结果，Deferred 后续可以复用同一�?SH / Reflection Probe 评估逻辑
struct BurtIndirectPBRComponents
{
    // 保存间接漫反射贡献，数据来源�?Unity SH / Light Probe
float3 diffuse;

    // 保存间接镜面贡献，数据来源是 Unity Reflection Probe / Sky Reflection
float3 specular;

    // 保存间接镜面能量补偿，数据来源是 XRender PreIntegratedFG.z
float3 specularEnergyCompensation;

    float3 subsurfaceIndirect;
};

BurtPBRMaterialData BurtCreateClearCoatMaterialData(BurtPBRMaterialData baseMaterialData);

#if BURT_ENABLE_CLEAR_COAT_SHADING
float3 BurtEvaluateClearCoatLayerTransmission(
    BurtPBRMaterialData baseMaterialData,
    BurtPBRMaterialData clearCoatMaterialData,
    BurtPBRGeometryData clearCoatGeometryData)
{
    float2 clearCoatDFG = GetSpecularDFGTerms(clearCoatMaterialData.perceptualRoughness, clearCoatGeometryData.nDotV);
    float3 clearCoatEnvFresnel = EvalSpecularDFG(clearCoatMaterialData.f0, clearCoatMaterialData.f90, clearCoatDFG);
    return BurtClearCoatFresnelTransmission(clearCoatEnvFresnel) * BurtSimpleClearCoatTransmittanceFromView(clearCoatGeometryData.nDotV, baseMaterialData.metallic, baseMaterialData.baseColor);
}

float3 BurtEvaluateClearCoatLayerEnergyPreservation(
    BurtPBRMaterialData baseMaterialData,
    BurtPBRMaterialData clearCoatMaterialData,
    BurtPBRGeometryData clearCoatGeometryData)
{
    float3 clearCoatEnergyCompensation;
    float clearCoatEnergyPreservation;
    GetSpecularEnergyTerms(clearCoatMaterialData.f0, clearCoatMaterialData.f90, clearCoatMaterialData.perceptualRoughness, clearCoatGeometryData.nDotV, clearCoatEnergyCompensation, clearCoatEnergyPreservation);
    return clearCoatEnergyPreservation * BurtSimpleClearCoatTransmittanceFromView(clearCoatGeometryData.nDotV, baseMaterialData.metallic, baseMaterialData.baseColor);
}

float3 BurtClearCoatLayerCombine(
    BurtPBRMaterialData baseMaterialData,
    BurtPBRGeometryData baseGeometryData,
    float3 baseIndirectSpecularEnergyCompensation,
    BurtPBRMaterialData clearCoatMaterialData,
    BurtPBRGeometryData clearCoatGeometryData,
    float clearCoatMask)
{
    float baseRoughness = baseMaterialData.perceptualRoughness;
    float3 bottomLayerReflectionDirectionWS = BurtGetIndirectSpecularReflectionDirectionWS(baseGeometryData, baseMaterialData.anisotropy, baseRoughness);
    float3 bottomLayerRadiance = SampleIndirectSpecularRadiance(bottomLayerReflectionDirectionWS, baseRoughness);
    float2 bottomLayerDFG = GetSpecularDFGTerms(baseRoughness, baseGeometryData.nDotV);
    float3 bottomLayerEnvBRDF = EvalSpecularDFG(baseMaterialData.f0, baseMaterialData.f90, bottomLayerDFG);
    float bottomLayerSpecularOcclusion = GetIndirectSpecularOcclusion(baseGeometryData.nDotV, baseMaterialData.occlusion, baseRoughness);
    float3 bottomLayerReflections = bottomLayerRadiance * bottomLayerEnvBRDF * baseIndirectSpecularEnergyCompensation * bottomLayerSpecularOcclusion;

    float clearCoatRoughness = clearCoatMaterialData.perceptualRoughness;
    float3 topLayerReflectionDirectionWS = BurtGetIndirectSpecularReflectionDirectionWS(clearCoatGeometryData, 0.0f, clearCoatRoughness);
    float3 topLayerRadiance = SampleIndirectSpecularRadiance(topLayerReflectionDirectionWS, clearCoatRoughness);
    float2 clearCoatDFG = GetSpecularDFGTerms(clearCoatRoughness, clearCoatGeometryData.nDotV);
    float3 clearCoatEnvBRDF = EvalSpecularDFG(clearCoatMaterialData.f0, clearCoatMaterialData.f90, clearCoatDFG);
    float3 clearCoatEnergyCompensation;
    float clearCoatEnergyPreservation;
    GetSpecularEnergyTerms(clearCoatMaterialData.f0, clearCoatMaterialData.f90, clearCoatRoughness, clearCoatGeometryData.nDotV, clearCoatEnergyCompensation, clearCoatEnergyPreservation);
    float clearCoatSpecularOcclusion = GetIndirectSpecularOcclusion(clearCoatGeometryData.nDotV, clearCoatMaterialData.occlusion, clearCoatRoughness);
    float3 topLayerReflections = topLayerRadiance * clearCoatEnvBRDF * clearCoatEnergyCompensation * clearCoatSpecularOcclusion;

    float3 layerTransmission = BurtEvaluateClearCoatLayerTransmission(baseMaterialData, clearCoatMaterialData, clearCoatGeometryData);
    float3 transmittedBottomLayerReflections = bottomLayerReflections * layerTransmission;
    return lerp(bottomLayerReflections, transmittedBottomLayerReflections, clearCoatMask) + topLayerReflections * clearCoatMask;
}

float3 BurtEvaluateClearCoatLayerCombinedSpecular(
    BurtPBRMaterialData baseMaterialData,
    BurtPBRGeometryData baseGeometryData,
    float3 baseIndirectSpecularEnergyCompensation,
    BurtPBRMaterialData clearCoatMaterialData,
    BurtPBRGeometryData clearCoatGeometryData,
    float clearCoatMask)
{
    return BurtClearCoatLayerCombine(baseMaterialData, baseGeometryData, baseIndirectSpecularEnergyCompensation, clearCoatMaterialData, clearCoatGeometryData, clearCoatMask);
}
#endif

#if BURT_ENABLE_SUBSURFACE_SHADING
float3 BurtEvaluateSubsurfaceIndirectProfile(
    BurtPBRMaterialData materialData,
    BurtPBRGeometryData geometryData)
{
    float subsurfaceStrength = saturate(materialData.subsurfaceStrength);
    if (subsurfaceStrength <= 0.0001f)
    {
        return float3(0.0f, 0.0f, 0.0f);
    }

    float thickness = saturate(materialData.subsurfaceThickness);
    if (BurtIsSubsurface3SPreIntegratedMode(materialData.subsurfaceScatteringMode))
    {
        float curvature = saturate(1.0f - thickness);
        float3 referenceSHIrradiance = BurtEvaluateSubsurface3SSH9(curvature, materialData.subsurfaceProfileIndex, geometryData.normalWS);
        float3 lutScatter = BurtSampleSubsurfacePreIntegratedLut(1.0f, curvature, materialData.subsurfaceProfileIndex);
        float3 fallbackIrradiance = lerp(float3(1.0f, 1.0f, 1.0f), lutScatter, saturate(_BurtSubsurfacePreIntegratedLutEnabled)) * BurtSampleIndirectDiffuseIrradiance(geometryData.normalWS);
        float3 subsurfaceIrradiance = lerp(fallbackIrradiance, referenceSHIrradiance, saturate(_BurtSubsurfaceSHLutEnabled));
        return materialData.diffuseColor * subsurfaceIrradiance * BurtGTAOMultiBounce(materialData.occlusion, materialData.baseColor) * subsurfaceStrength;
    }

    float ambientWrap = saturate(materialData.subsurfaceAmbient);
    float3 frontIrradiance = BurtSampleIndirectDiffuseIrradiance(geometryData.normalWS);
    float3 backIrradiance = BurtSampleIndirectDiffuseIrradiance(-geometryData.normalWS);
    float3 tangentIrradiance = BurtSampleIndirectDiffuseIrradiance(geometryData.tangentWS);
    float3 bitangentIrradiance = BurtSampleIndirectDiffuseIrradiance(geometryData.bitangentWS);
    float3 sideIrradiance = (tangentIrradiance + bitangentIrradiance) * 0.5f;
    float3 wrappedIrradiance = lerp(frontIrradiance, backIrradiance, ambientWrap);
    wrappedIrradiance = lerp(wrappedIrradiance, sideIrradiance, saturate(thickness * 0.35f));

    float4 profileTransmission = BurtLoadSubsurfaceProfileTransmission(materialData.subsurfaceProfileIndex);
    float4 profileTint = BurtLoadSubsurfaceProfileTransmissionTint(materialData.subsurfaceProfileIndex);
    float3 profileTransmittance = BurtSampleSubsurfaceTransmissionProfile(materialData.subsurfaceProfileIndex, thickness);
    float extinctionScale = BurtDecodeSubsurfaceProfileExtinctionScale(profileTransmission.x);
    float normalWrap = saturate(profileTransmission.y);
    float scatteringDistribution = saturate(abs(BurtDecodeSubsurfaceProfileScatteringDistribution(profileTransmission.z)));
    float meanFreePathVisibility = saturate(rsqrt(extinctionScale));
    float thicknessVisibility = lerp(0.18f, 1.0f, thickness);
    float wrapVisibility = lerp(0.55f, 1.15f, max(ambientWrap, normalWrap));
    float scatterVisibility = lerp(0.75f, 1.25f, scatteringDistribution);
    float transmissionIntensity = BurtEvaluateSubsurfaceProfileIntensity(profileTransmittance);
#if defined(BURT_SUBSURFACE_DEFERRED_POSTPROCESS_INPUT)
    float3 subsurfaceDiffuse = wrappedIrradiance * transmissionIntensity;
#else
    float3 tint = max(materialData.subsurfaceTint * profileTint.rgb, float3(0.0f, 0.0f, 0.0f));
    float3 subsurfaceDiffuse = materialData.baseColor * tint * transmissionIntensity * wrappedIrradiance;
#endif
    return subsurfaceDiffuse * materialData.occlusion * subsurfaceStrength * thicknessVisibility * wrapVisibility * scatterVisibility * lerp(0.55f, 1.0f, meanFreePathVisibility);
}
#endif

// Evaluates split indirect PBR lighting from prepared material/geometry data.
BurtIndirectPBRComponents BurtEvaluateIndirectPBRComponents(BurtPBRMaterialData materialData, BurtPBRGeometryData geometryData, BurtPBRGeometryData clearCoatGeometryData, BurtPBREnergyTerms energyTerms)
{
    BurtIndirectPBRComponents components;
    components.diffuse = BurtEvaluateIndirectDiffusePBR(materialData, geometryData.normalWS, energyTerms.energyPreservation);
    components.specularEnergyCompensation = GetIndirectSpecularEnergyCompensation(materialData, geometryData, energyTerms);
    components.specular = BurtEvaluateIndirectSpecularPBR(materialData, geometryData, components.specularEnergyCompensation);
    components.subsurfaceIndirect = float3(0.0f, 0.0f, 0.0f);

#if BURT_ENABLE_SUBSURFACE_SHADING
    bool isSubsurface3S = BurtIsSubsurface3SPreIntegratedMode(materialData.subsurfaceScatteringMode);
#if defined(BURT_SUBSURFACE_DEFERRED_POSTPROCESS_INPUT)
    if (isSubsurface3S)
    {
        components.subsurfaceIndirect = BurtEvaluateSubsurfaceIndirectProfile(materialData, geometryData);
        components.diffuse = lerp(components.diffuse, components.subsurfaceIndirect, saturate(materialData.subsurfaceStrength));
    }
    else
    {
        components.subsurfaceIndirect = components.diffuse;
    }
#else
    components.subsurfaceIndirect = BurtEvaluateSubsurfaceIndirectProfile(materialData, geometryData);
    components.diffuse = lerp(components.diffuse, components.subsurfaceIndirect, saturate(materialData.subsurfaceStrength));
#endif
    if (!isSubsurface3S)
    {
        components.specular = BurtEvaluateSubsurfaceIndirectSpecularDualLobe(materialData, geometryData, components.specularEnergyCompensation);
    }
#endif

#if BURT_ENABLE_CLEAR_COAT_SHADING
    float clearCoatMask = saturate(materialData.clearCoatMask);
    if (clearCoatMask > 0.0001f)
    {
        BurtPBRMaterialData clearCoatMaterialData = BurtCreateClearCoatMaterialData(materialData);
        float3 layerTransmission = BurtEvaluateClearCoatLayerEnergyPreservation(materialData, clearCoatMaterialData, clearCoatGeometryData);

        components.diffuse = lerp(components.diffuse, components.diffuse * layerTransmission, clearCoatMask);
        components.specular = BurtEvaluateClearCoatLayerCombinedSpecular(materialData, geometryData, components.specularEnergyCompensation, clearCoatMaterialData, clearCoatGeometryData, clearCoatMask);
    }
#endif

    // 返回拆分后的间接光，Debug View �?Deferred 光照都可以直接读取
return components;
}

BurtIndirectPBRComponents BurtEvaluateIndirectPBRComponents(BurtPBRMaterialData materialData, BurtPBRGeometryData geometryData, BurtPBREnergyTerms energyTerms)
{
    return BurtEvaluateIndirectPBRComponents(materialData, geometryData, geometryData, energyTerms);
}

// Compatibility overload that prepares material/geometry/energy terms internally.
BurtIndirectPBRComponents BurtEvaluateIndirectPBRComponents(BurtSurfaceData surfaceData, float3 normalWS, float3 viewDirectionWS, float energyPreservation)
{
    BurtPBRMaterialData materialData = BurtPreparePBRMaterialData(surfaceData);

    BurtPBRGeometryData geometryData = BurtPreparePBRGeometryData(normalWS, viewDirectionWS);

    // 准备完整 energy terms；随后用调用方传入的 preservation 覆盖，保持旧入口语义不变
float directSpecularPerceptualRoughness = GetDirectSpecularPerceptualRoughness(materialData, geometryData);
    BurtPBREnergyTerms energyTerms = BurtPreparePBREnergyTerms(materialData, geometryData, directSpecularPerceptualRoughness);
    energyTerms.energyPreservation = energyPreservation;

    // 复用准备数据版本，保证旧入口也能拿到间接高光能量补偿
return BurtEvaluateIndirectPBRComponents(materialData, geometryData, energyTerms);
}

// 计算 PBR 间接光总和：间接漫反射 + 间接镜面反射
float3 BurtEvaluateIndirectPBR(BurtSurfaceData surfaceData, float3 normalWS, float3 viewDirectionWS)
{
    BurtPBRMaterialData materialData = BurtPreparePBRMaterialData(surfaceData);

    BurtPBRGeometryData geometryData = BurtPreparePBRGeometryData(normalWS, viewDirectionWS);

    // 直接高光�?roughness 单独包含 Specular AA，不回写材质 Base.Roughness
float directSpecularPerceptualRoughness = GetDirectSpecularPerceptualRoughness(materialData, geometryData);

    // 一次性准备能量项，避�?indirect diffuse / indirect specular 分别�?FG LUT�?
    BurtPBREnergyTerms energyTerms = BurtPreparePBREnergyTerms(materialData, geometryData, directSpecularPerceptualRoughness);

    // 复用拆分版本，保证总和接口�?Debug 拆项使用完全相同的结果�?
    BurtIndirectPBRComponents components = BurtEvaluateIndirectPBRComponents(materialData, geometryData, energyTerms);

    // 返回完整间接光，后续会和主光直接光相加
return components.diffuse + components.specular;
}

// 保存一�?PBR shading 评估前的核心准备数据；Deferred 后续�?GBuffer 还原 material / geometry 后也应先进入这里
struct BurtPBRShadingCoreData
{
    // 保存已经准备好的材质数据，对�?XRender GenericData �?SlabParams 的核心材质语义�?
    BurtPBRMaterialData materialData;

    // 保存已经准备好的几何数据，对�?XRender PosData / Geometry 常用方向项�?
    BurtPBRGeometryData geometryData;

    // 保存直接高光�?Specular AA 中间项，避免 direct shading �?Debug View 各算一遍�?
    BurtSpecularAATerms specularAATerms;

    // 保存直接高光实际使用的感知粗糙度，等�?Specular AA 过滤后的 roughness
float directSpecularPerceptualRoughness;

    // 保存 XRender EnergyCompensation_GGX �?EnergyPreservation，供 direct / indirect / compose 共用�?
    BurtPBREnergyTerms energyTerms;

    float3 clearCoatNormalWS;

    BurtPBRGeometryData clearCoatGeometryData;

    BurtSpecularAATerms clearCoatSpecularAATerms;

    float clearCoatDirectSpecularPerceptualRoughness;

    float3 clearCoatDirectSpecularEnergyCompensation;
};

BurtPBRMaterialData BurtCreateClearCoatMaterialData(BurtPBRMaterialData baseMaterialData)
{
    BurtPBRMaterialData clearCoatMaterialData = baseMaterialData;
    clearCoatMaterialData.baseColor = float3(1.0f, 1.0f, 1.0f);
    clearCoatMaterialData.metallic = 0.0f;
    clearCoatMaterialData.anisotropy = 0.0f;
    clearCoatMaterialData.reflectance = BURT_INPUT_DEFAULT_REFLECTANCE;
    clearCoatMaterialData.diffuseColor = float3(0.0f, 0.0f, 0.0f);
    clearCoatMaterialData.f0 = float3(0.04f, 0.04f, 0.04f);
    clearCoatMaterialData.f90 = float3(1.0f, 1.0f, 1.0f);
    clearCoatMaterialData.perceptualRoughness = ClampPerceptualRoughness(baseMaterialData.clearCoatRoughness);
    clearCoatMaterialData.linearRoughness = PerceptualRoughnessToLinearRoughness(clearCoatMaterialData.perceptualRoughness);
    clearCoatMaterialData.a2 = LinearRoughnessToA2(clearCoatMaterialData.linearRoughness);
    clearCoatMaterialData.clearCoatMask = 0.0f;
    clearCoatMaterialData.subsurfaceStrength = 0.0f;
    clearCoatMaterialData.subsurfaceThickness = BURT_SUBSURFACE_DEFAULT_THICKNESS;
    clearCoatMaterialData.subsurfacePower = BURT_SUBSURFACE_DEFAULT_POWER;
    clearCoatMaterialData.subsurfaceDistortion = BURT_SUBSURFACE_DEFAULT_DISTORTION;
    clearCoatMaterialData.subsurfaceAmbient = BURT_SUBSURFACE_DEFAULT_AMBIENT;
    clearCoatMaterialData.subsurfaceScatteringMode = BURT_SUBSURFACE_DEFAULT_SCATTERING_MODE;
    clearCoatMaterialData.subsurfaceTint = BURT_SUBSURFACE_DEFAULT_TINT;
    clearCoatMaterialData.subsurfaceProfileIndex = BURT_SUBSURFACE_DEFAULT_PROFILE_INDEX;
    return clearCoatMaterialData;
}

BurtPBRShadingCoreData BurtApplyClearCoatTopLayerCoreData(BurtPBRShadingCoreData coreData)
{
    coreData.clearCoatSpecularAATerms = coreData.specularAATerms;
    coreData.clearCoatDirectSpecularPerceptualRoughness = coreData.directSpecularPerceptualRoughness;
    coreData.clearCoatDirectSpecularEnergyCompensation = coreData.energyTerms.directSpecularEnergyCompensation;

#if BURT_ENABLE_CLEAR_COAT_SHADING
    if (saturate(coreData.materialData.clearCoatMask) <= 0.0001f)
    {
        return coreData;
    }

    BurtPBRMaterialData clearCoatMaterialData = BurtCreateClearCoatMaterialData(coreData.materialData);
    coreData.clearCoatSpecularAATerms = BurtEvaluateSpecularAATerms(clearCoatMaterialData, coreData.clearCoatGeometryData);
    coreData.clearCoatDirectSpecularPerceptualRoughness = coreData.clearCoatSpecularAATerms.filteredPerceptualRoughness;

    BurtPBREnergyTerms clearCoatEnergyTerms = BurtPreparePBREnergyTerms(clearCoatMaterialData, coreData.clearCoatGeometryData, coreData.clearCoatDirectSpecularPerceptualRoughness);
    coreData.clearCoatDirectSpecularEnergyCompensation = clearCoatEnergyTerms.directSpecularEnergyCompensation;
#endif
    return coreData;
}

// Prepare stage: shared material, geometry, Specular AA, and energy inputs.
BurtPBRShadingCoreData BurtPreparePBRShadingCoreData(BurtPBRMaterialData materialData, BurtPBRGeometryData geometryData)
{
    BurtPBRShadingCoreData coreData;

    coreData.materialData = materialData;

    coreData.geometryData = geometryData;

    coreData.specularAATerms = BurtEvaluateSpecularAATerms(materialData, geometryData);

    coreData.directSpecularPerceptualRoughness = coreData.specularAATerms.filteredPerceptualRoughness;

    coreData.energyTerms = BurtPreparePBREnergyTerms(materialData, geometryData, coreData.directSpecularPerceptualRoughness);
    coreData.clearCoatNormalWS = geometryData.normalWS;
    coreData.clearCoatGeometryData = geometryData;
    coreData = BurtApplyClearCoatTopLayerCoreData(coreData);

    return coreData;
}

// SurfaceData overload used by Forward paths.
BurtPBRShadingCoreData BurtPreparePBRShadingCoreData(BurtSurfaceData surfaceData, float3 normalWS, float3 viewDirectionWS)
{
    BurtPBRMaterialData materialData = BurtPreparePBRMaterialData(surfaceData);

    BurtPBRGeometryData geometryData = BurtPreparePBRGeometryData(normalWS, viewDirectionWS);

    return BurtPreparePBRShadingCoreData(materialData, geometryData);
}

BurtPBRShadingCoreData BurtPreparePBRShadingCoreData(BurtSurfaceData surfaceData, float3 normalWS, float4 tangentWS, float3 viewDirectionWS)
{
    BurtPBRMaterialData materialData = BurtPreparePBRMaterialData(surfaceData);
    BurtPBRGeometryData geometryData = BurtPreparePBRGeometryData(normalWS, tangentWS, viewDirectionWS);
    return BurtPreparePBRShadingCoreData(materialData, geometryData);
}

BurtPBRShadingCoreData BurtPreparePBRShadingCoreData(BurtSurfaceData surfaceData, float3 normalWS, float3 clearCoatNormalWS, float3 viewDirectionWS)
{
    BurtPBRShadingCoreData coreData = BurtPreparePBRShadingCoreData(surfaceData, normalWS, viewDirectionWS);
    coreData.clearCoatNormalWS = BurtSafeNormalize(clearCoatNormalWS);
    coreData.clearCoatGeometryData = BurtPreparePBRGeometryData(coreData.clearCoatNormalWS, viewDirectionWS);
    coreData = BurtApplyClearCoatTopLayerCoreData(coreData);
    return coreData;
}

BurtPBRShadingCoreData BurtPreparePBRShadingCoreData(BurtSurfaceData surfaceData, float3 normalWS, float4 tangentWS, float3 clearCoatNormalWS, float3 viewDirectionWS)
{
    BurtPBRShadingCoreData coreData = BurtPreparePBRShadingCoreData(surfaceData, normalWS, tangentWS, viewDirectionWS);
    coreData.clearCoatNormalWS = BurtSafeNormalize(clearCoatNormalWS);
    coreData.clearCoatGeometryData = BurtPreparePBRGeometryData(coreData.clearCoatNormalWS, tangentWS, viewDirectionWS);
    coreData = BurtApplyClearCoatTopLayerCoreData(coreData);
    return coreData;
}

// GBufferData overload used by Deferred lighting paths.
BurtPBRShadingCoreData BurtPreparePBRShadingCoreData(BurtGBufferData gbufferData, float3 viewDirectionWS)
{
    BurtPBRMaterialData materialData = BurtPreparePBRMaterialData(gbufferData);

    BurtPBRGeometryData geometryData = BurtPreparePBRGeometryData(gbufferData, viewDirectionWS);

    BurtPBRShadingCoreData coreData = BurtPreparePBRShadingCoreData(materialData, geometryData);
    coreData.clearCoatNormalWS = BurtGetClearCoatNormalWS(gbufferData);
    coreData.clearCoatGeometryData = BurtPreparePBRGeometryData(coreData.clearCoatNormalWS, gbufferData.tangentWS, viewDirectionWS);
    coreData = BurtApplyClearCoatTopLayerCoreData(coreData);
    return coreData;
}

// EncodedGBuffer overload used after sampling MRT payloads.
BurtPBRShadingCoreData BurtPreparePBRShadingCoreData(BurtEncodedGBuffer encodedGBuffer, float3 viewDirectionWS)
{
    BurtGBufferData gbufferData = BurtDecodeGBuffer(encodedGBuffer);

    return BurtPreparePBRShadingCoreData(gbufferData, viewDirectionWS);
}

// 保存一次完�?PBR shading 的可复用拆分结果，Forward 只负责调用，Deferred 后续可从 GBuffer 还原输入后复用
struct BurtPBRShadingComponents
{
    // 保存材质漫反射颜色，对应 XRender GenericData.DiffuseColor，Deferred Debug 可以检�?GBuffer 还原是否正确
float3 diffuseColor;

    // 保存�?reflectance / metallic / baseColor 还原得到�?F0，材质面板不直接暴露 F0，只�?Debug 中溯源查看
float3 f0;

    // 保存默认掠射角反射端点，对应 XRender GenericData.F90
float3 f90;

    // 保存直接漫反射贡献，已经包含主光颜色、NdotL 和阴影
float3 directDiffuse;

    // 保存直接镜面高光贡献，已经包�?GGX、Fresnel、主光颜色、NdotL 和阴影
float3 directSpecular;

    // 保存直接光总和，等�?directDiffuse + directSpecular
float3 directLighting;

    // 保存追加光直接漫反射贡献，不包含主光、间接光和自发光
float3 additionalDiffuse;

    // 保存追加光直接镜面贡献，不包含主光、间接光和自发光
float3 additionalSpecular;

    // 保存追加光直接光总和，等�?additionalDiffuse + additionalSpecular
float3 additionalLighting;

    // 保存间接漫反射贡献，主要来自 Unity SH / Light Probe
float3 indirectDiffuse;

    // 保存间接镜面高光贡献，主要来�?Unity Reflection Probe / Sky Reflection
float3 indirectSpecular;

    // 保存间接光总和，等�?indirectDiffuse + indirectSpecular
float3 indirectLighting;

    // 保存最�?PBR 光照，等�?directLighting + indirectLighting，不包含自发光
float3 lighting;

    // 保存材质感知粗糙度，也就�?1 - smoothness 后并经过最小粗糙度保护的结果
float perceptualRoughness;

    // 保存直接高光实际使用的感知粗糙度，包�?Specular AA 对极光滑高光的拓宽
float specularAARoughness;

    // 保存 Specular AA 估算出的屏幕空间法线方差，用来观察高光是否因为像素内法线变化被拓宽
float specularAANormalVariance;

    // 保存 Specular AA 额外增加的感知粗糙度�? 表示未触发拓宽
float specularAARoughnessDelta;

    // 保存直接高光能量补偿，方�?Debug View �?Deferred 调试确认 LUT.z 的影响
float3 specularEnergyCompensation;

    // 保存间接高光能量补偿，方�?Debug View �?Deferred 调试确认 Reflection Probe 是否也补回多次散射能量
float3 indirectSpecularEnergyCompensation;

    // 保存 XRender EnergyPreservation，表�?specular 顶层之后 diffuse 底层还能保留的能量比例
float energyPreservation;

    // 保存间接高光遮蔽，方�?Debug View �?Deferred 调试确认 AO 对反射探针的影响
float specularOcclusion;

    // 保存直接 GGX D 项，方便拆开检查高 smoothness 时的 NDF 峰值
float directBRDFD;

    // 保存直接 Smith Joint Visibility 项，方便检查几何遮蔽是否压暗高光
float directBRDFVisibility;

    // 保存直接 Schlick Fresnel 项，方便检�?reflectance / metallic �?F0 的映射
float3 directBRDFFresnel;

    // 保存直接 diffuse lobe，默认来�?Lambert，后续可�?XRender Burley
float directDiffuseLobe;

    // 保存未乘灯光颜色、NdotL 和阴影的直接 diffuse BRDF
float3 directDiffuseBRDF;

    // 保存未乘灯光颜色、NdotL 和阴影的直接 specular BRDF
float3 directSpecularBRDF;

    // 保存间接高光 DFG.xy，方便检�?PreIntegratedFG LUT 或解�?fallback
float2 indirectSpecularDFG;

    // 保存 F0/F90 套用 DFG 后的环境 BRDF，Reflection Probe 会乘这一项
float3 indirectSpecularEnvBRDF;

    // 保存 Hair primary/R lobe；Default Lit 固定�?0，Hair Debug 使用
float hairPrimaryLobe;

    // 保存 Hair secondary/TT lobe；Default Lit 固定�?0，Hair Debug 使用
float hairSecondaryLobe;

    // 保存 Hair 背光透射近似 lobe；Default Lit 固定�?0，Hair Debug 使用
float hairTransmissionLobe;

    // 保存 Hair lighting scatter；Default Lit 固定�?0，Hair Debug 使用
float hairScatter;

    float clearCoatMask;

    float subsurfaceProfileIndex;

    float3 subsurfaceTransmission;

    float3 subsurfaceKernelWeight;

    float3 subsurfaceIndirect;
};

// Direct stage shared by main and additional lights.
BurtDirectPBRComponents BurtEvaluatePBRDirectFromCore(BurtPBRShadingCoreData coreData, BurtLight light)
{
    // 复用 BRDF 层的 direct components，确�?Forward / Deferred / Debug 都走同一�?D、V、F �?diffuse lobe�?
    BurtDirectPBRComponents components = BurtEvaluateDirectPBRComponents(
        coreData.materialData,
        coreData.geometryData,
        coreData.energyTerms,
        coreData.directSpecularPerceptualRoughness,
        light.color,
        light.directionWS,
        light.shadowAttenuation);

    float clearCoatMask = saturate(coreData.materialData.clearCoatMask);
#if BURT_ENABLE_CLEAR_COAT_SHADING
    if (clearCoatMask <= 0.0001f)
    {
        return components;
    }

    BurtPBRGeometryData clearCoatGeometryData = coreData.clearCoatGeometryData;
    BurtPBRGeometryData baseGeometryData = coreData.geometryData;
    float3 n = clearCoatGeometryData.normalWS;
    float3 baseN = baseGeometryData.normalWS;
    float3 v = clearCoatGeometryData.viewDirectionWS;
    float3 l = BurtSafeNormalize(light.directionWS);
    float3 h = BurtSafeNormalize(l + v);
    float clearCoatNdotL = saturate(dot(n, l));
    float clearCoatNdotH = saturate(dot(n, h));
    float clearCoatVdotH = saturate(dot(v, h));
    float clearCoatNoV = clearCoatGeometryData.nDotV;
    float clearCoatRoughness = ClampPerceptualRoughness(coreData.clearCoatDirectSpecularPerceptualRoughness);
    float clearCoatLinearRoughness = PerceptualRoughnessToLinearRoughness(clearCoatRoughness);
    float clearCoatA2 = LinearRoughnessToA2(clearCoatLinearRoughness);
    float clearCoatD = D_GGX(clearCoatA2, clearCoatNdotH);
    float clearCoatVisibility = Vis_SmithJointApprox(clearCoatLinearRoughness, clearCoatNoV, clearCoatNdotL);
    float3 clearCoatFresnel = F_Schlick_UE(float3(BURT_CLEAR_COAT_F0, BURT_CLEAR_COAT_F0, BURT_CLEAR_COAT_F0), float3(1.0f, 1.0f, 1.0f), clearCoatVdotH);
    float3 clearCoatSpecularBRDF = clearCoatD * clearCoatVisibility * clearCoatFresnel * coreData.clearCoatDirectSpecularEnergyCompensation;
    float3 clearCoatSpecular = clearCoatSpecularBRDF * light.color * clearCoatNdotL * light.shadowAttenuation;

    float baseNdotL = saturate(dot(baseN, l));
    float baseNdotV = baseGeometryData.nDotV;
    float baseNdotH = saturate(dot(baseN, h));
    float baseXdotH = dot(baseGeometryData.tangentWS, h);
    float baseYdotH = dot(baseGeometryData.bitangentWS, h);
    float baseXdotV = dot(baseGeometryData.tangentWS, v);
    float baseYdotV = dot(baseGeometryData.bitangentWS, v);
    float baseXdotL = dot(baseGeometryData.tangentWS, l);
    float baseYdotL = dot(baseGeometryData.bitangentWS, l);
    float refractionBlend = BurtRefractBlendClearCoatApprox(clearCoatVdotH);
    float refractionProjection = refractionBlend * baseNdotH;
    float refractedNdotV = clamp(BURT_CLEAR_COAT_ETA * baseNdotV - refractionProjection, 0.001f, 1.0f);
    float refractedNdotL = clamp(BURT_CLEAR_COAT_ETA * baseNdotL - refractionProjection, 0.001f, 1.0f);
    float refractedVdotH = saturate(BURT_CLEAR_COAT_ETA * clearCoatVdotH - refractionBlend);
    float3 layerTransmission = BurtClearCoatFresnelTransmission(clearCoatFresnel) * BurtSimpleClearCoatTransmittance(refractedNdotL, refractedNdotV, coreData.materialData.metallic, coreData.materialData.baseColor);
    float bottomLayerLightNoL = clearCoatNdotL;

    float bottomDiffuseLobe = SlabLobe_Diffuse(coreData.materialData, refractedNdotV, refractedNdotL, refractedVdotH);
    float3 refractedDiffuseBRDF = coreData.materialData.diffuseColor * bottomDiffuseLobe * coreData.energyTerms.energyPreservation * layerTransmission;
    float3 refractedDiffuse = refractedDiffuseBRDF * light.color * bottomLayerLightNoL * light.shadowAttenuation;

    float bottomLinearRoughness = PerceptualRoughnessToLinearRoughness(coreData.directSpecularPerceptualRoughness);
    float bottomA2 = LinearRoughnessToA2(bottomLinearRoughness);
    float bottomAx;
    float bottomAy;
    GetAnisotropicRoughness(bottomLinearRoughness, coreData.materialData.anisotropy, bottomAx, bottomAy);
    float bottomD = D_GGX_Anisotropic(bottomAx, bottomAy, baseNdotH, baseXdotH, baseYdotH);
    float bottomVisibility = Vis_SmithJointAnisotropic(bottomAx, bottomAy, refractedNdotV, bottomLayerLightNoL, baseXdotV, baseXdotL, baseYdotV, baseYdotL);
    float3 bottomFresnel = F_Schlick_UE(coreData.materialData.f0, coreData.materialData.f90, refractedVdotH);
    float3 refractedSpecularBRDF = bottomD * bottomVisibility * bottomFresnel * coreData.energyTerms.directSpecularEnergyCompensation * layerTransmission;
    float3 refractedSpecular = refractedSpecularBRDF * light.color * bottomLayerLightNoL * light.shadowAttenuation;

    components.diffuse = lerp(components.diffuse, refractedDiffuse, clearCoatMask);
    components.specular = lerp(components.specular, refractedSpecular, clearCoatMask) + clearCoatSpecular * clearCoatMask;
    components.brdfTerms.diffuseLobe = lerp(components.brdfTerms.diffuseLobe, bottomDiffuseLobe, clearCoatMask);
    components.brdfTerms.diffuseBRDF = lerp(components.brdfTerms.diffuseBRDF, refractedDiffuseBRDF, clearCoatMask);
    components.brdfTerms.specularBRDF = lerp(components.brdfTerms.specularBRDF, refractedSpecularBRDF, clearCoatMask) + clearCoatSpecularBRDF * clearCoatMask;
    components.brdfTerms.nDotL = lerp(components.brdfTerms.nDotL, clearCoatNdotL, clearCoatMask);
    components.brdfTerms.nDotV = lerp(components.brdfTerms.nDotV, clearCoatNoV, clearCoatMask);
    components.brdfTerms.nDotH = lerp(components.brdfTerms.nDotH, clearCoatNdotH, clearCoatMask);
    components.brdfTerms.vDotH = lerp(components.brdfTerms.vDotH, clearCoatVdotH, clearCoatMask);
    components.brdfTerms.perceptualRoughness = lerp(components.brdfTerms.perceptualRoughness, clearCoatRoughness, clearCoatMask);
    components.brdfTerms.linearRoughness = lerp(components.brdfTerms.linearRoughness, clearCoatLinearRoughness, clearCoatMask);
    components.brdfTerms.a2 = lerp(components.brdfTerms.a2, clearCoatA2, clearCoatMask);
    components.brdfTerms.d = lerp(components.brdfTerms.d, clearCoatD, clearCoatMask);
    components.brdfTerms.visibility = lerp(components.brdfTerms.visibility, clearCoatVisibility, clearCoatMask);
    components.brdfTerms.fresnel = lerp(components.brdfTerms.fresnel, clearCoatFresnel, clearCoatMask);
#endif
    return components;
}

BurtDirectPBRComponents BurtCreateZeroPBRDirectComponents()
{
    BurtDirectPBRComponents components;
    components.diffuse = float3(0.0f, 0.0f, 0.0f);
    components.specular = float3(0.0f, 0.0f, 0.0f);
    components.energyPreservation = 1.0f;
    components.brdfTerms.nDotL = 0.0f;
    components.brdfTerms.nDotV = 0.0f;
    components.brdfTerms.nDotH = 0.0f;
    components.brdfTerms.vDotH = 0.0f;
    components.brdfTerms.perceptualRoughness = 1.0f;
    components.brdfTerms.linearRoughness = 1.0f;
    components.brdfTerms.a2 = 1.0f;
    components.brdfTerms.d = 0.0f;
    components.brdfTerms.visibility = 0.0f;
    components.brdfTerms.fresnel = float3(0.0f, 0.0f, 0.0f);
    components.brdfTerms.diffuseLobe = 0.0f;
    components.brdfTerms.diffuseBRDF = float3(0.0f, 0.0f, 0.0f);
    components.brdfTerms.specularBRDF = float3(0.0f, 0.0f, 0.0f);
    return components;
}

BurtDirectPBRComponents BurtAddPBRDirectComponents(BurtDirectPBRComponents baseComponents, BurtDirectPBRComponents addedComponents)
{
    baseComponents.diffuse += addedComponents.diffuse;
    baseComponents.specular += addedComponents.specular;
    return baseComponents;
}

BurtDirectPBRComponents BurtEvaluatePBRAdditionalDirectLightingFromCore(BurtPBRShadingCoreData coreData, float3 positionWS)
{
    BurtDirectPBRComponents additionalDirectComponents = BurtCreateZeroPBRDirectComponents();
    int additionalLightCount = BurtGetAdditionalLightCount();

    [loop]
    for (int lightIndex = 0; lightIndex < BURT_MAX_ADDITIONAL_LIGHTS; lightIndex++)
    {
        if (lightIndex >= additionalLightCount)
        {
            break;
        }

        BurtLight additionalLight = BurtCreateAdditionalLight(lightIndex, positionWS, coreData.geometryData.normalWS);
        additionalDirectComponents = BurtAddPBRDirectComponents(additionalDirectComponents, BurtEvaluatePBRDirectFromCore(coreData, additionalLight));
    }

    return additionalDirectComponents;
}

BurtDirectPBRComponents BurtEvaluatePBRAdditionalDirectLightingFromCore(BurtPBRShadingCoreData coreData, float3 positionWS, float3 shadowPositionWS)
{
    BurtDirectPBRComponents additionalDirectComponents = BurtCreateZeroPBRDirectComponents();
    int additionalLightCount = BurtGetAdditionalLightCount();

    [loop]
    for (int lightIndex = 0; lightIndex < BURT_MAX_ADDITIONAL_LIGHTS; lightIndex++)
    {
        if (lightIndex >= additionalLightCount)
        {
            break;
        }

        BurtLight additionalLight = BurtCreateAdditionalLight(lightIndex, positionWS, coreData.geometryData.normalWS, shadowPositionWS);
        additionalDirectComponents = BurtAddPBRDirectComponents(additionalDirectComponents, BurtEvaluatePBRDirectFromCore(coreData, additionalLight));
    }

    return additionalDirectComponents;
}

BurtDirectPBRComponents BurtEvaluatePBRAdditionalDirectLightingFromCore(BurtPBRShadingCoreData coreData, float3 positionWS, float2 screenUV)
{
#if defined(BURT_USE_TILED_LIGHTING)
    uint2 range = uint2(0u, 0u);
    uint useClusterLightList = 0u;
    if (!BurtTryGetAdditionalLightListRange(screenUV, positionWS, range, useClusterLightList))
    {
        return BurtEvaluatePBRAdditionalDirectLightingFromCore(coreData, positionWS);
    }

    BurtDirectPBRComponents additionalDirectComponents = BurtCreateZeroPBRDirectComponents();
    int additionalLightCount = BurtGetAdditionalLightCount();

    [loop]
    for (uint localLightIndex = 0u; localLightIndex < BURT_MAX_ADDITIONAL_LIGHTS; localLightIndex++)
    {
        if (localLightIndex >= range.y)
        {
            break;
        }

        uint storedLightIndex = BurtReadAdditionalLightListIndex(range.x + localLightIndex, useClusterLightList);
        if (storedLightIndex >= (uint)additionalLightCount)
        {
            continue;
        }

        BurtLight additionalLight = BurtCreateAdditionalLight((int)storedLightIndex, positionWS, coreData.geometryData.normalWS);
        additionalDirectComponents = BurtAddPBRDirectComponents(additionalDirectComponents, BurtEvaluatePBRDirectFromCore(coreData, additionalLight));
    }

    return additionalDirectComponents;
#else
    return BurtEvaluatePBRAdditionalDirectLightingFromCore(coreData, positionWS);
#endif
}

BurtDirectPBRComponents BurtEvaluatePBRAdditionalDirectLightingFromCore(BurtPBRShadingCoreData coreData, float3 positionWS, float3 shadowPositionWS, float2 screenUV)
{
#if defined(BURT_USE_TILED_LIGHTING)
    uint2 range = uint2(0u, 0u);
    uint useClusterLightList = 0u;
    if (!BurtTryGetAdditionalLightListRange(screenUV, positionWS, range, useClusterLightList))
    {
        return BurtEvaluatePBRAdditionalDirectLightingFromCore(coreData, positionWS, shadowPositionWS);
    }

    BurtDirectPBRComponents additionalDirectComponents = BurtCreateZeroPBRDirectComponents();
    int additionalLightCount = BurtGetAdditionalLightCount();

    [loop]
    for (uint localLightIndex = 0u; localLightIndex < BURT_MAX_ADDITIONAL_LIGHTS; localLightIndex++)
    {
        if (localLightIndex >= range.y)
        {
            break;
        }

        uint storedLightIndex = BurtReadAdditionalLightListIndex(range.x + localLightIndex, useClusterLightList);
        if (storedLightIndex >= (uint)additionalLightCount)
        {
            continue;
        }

        BurtLight additionalLight = BurtCreateAdditionalLight((int)storedLightIndex, positionWS, coreData.geometryData.normalWS, shadowPositionWS);
        additionalDirectComponents = BurtAddPBRDirectComponents(additionalDirectComponents, BurtEvaluatePBRDirectFromCore(coreData, additionalLight));
    }

    return additionalDirectComponents;
#else
    return BurtEvaluatePBRAdditionalDirectLightingFromCore(coreData, positionWS, shadowPositionWS);
#endif
}

BurtDirectPBRComponents BurtEvaluatePBRAdditionalDirectLightingUnshadowedFromCore(BurtPBRShadingCoreData coreData, float3 positionWS)
{
    BurtDirectPBRComponents additionalDirectComponents = BurtCreateZeroPBRDirectComponents();
    int additionalLightCount = BurtGetAdditionalLightCount();

    [loop]
    for (int lightIndex = 0; lightIndex < BURT_MAX_ADDITIONAL_LIGHTS; lightIndex++)
    {
        if (lightIndex >= additionalLightCount)
        {
            break;
        }

        BurtLight additionalLight = BurtCreateAdditionalLightUnshadowed(lightIndex, positionWS);
        additionalDirectComponents = BurtAddPBRDirectComponents(additionalDirectComponents, BurtEvaluatePBRDirectFromCore(coreData, additionalLight));
    }

    return additionalDirectComponents;
}

BurtDirectPBRComponents BurtEvaluatePBRAdditionalDirectLightingUnshadowedFromCore(BurtPBRShadingCoreData coreData, float3 positionWS, float2 screenUV)
{
#if defined(BURT_USE_TILED_LIGHTING)
    uint2 range = uint2(0u, 0u);
    uint useClusterLightList = 0u;
    if (!BurtTryGetAdditionalLightListRange(screenUV, positionWS, range, useClusterLightList))
    {
        return BurtEvaluatePBRAdditionalDirectLightingUnshadowedFromCore(coreData, positionWS);
    }

    BurtDirectPBRComponents additionalDirectComponents = BurtCreateZeroPBRDirectComponents();
    int additionalLightCount = BurtGetAdditionalLightCount();

    [loop]
    for (uint localLightIndex = 0u; localLightIndex < BURT_MAX_ADDITIONAL_LIGHTS; localLightIndex++)
    {
        if (localLightIndex >= range.y)
        {
            break;
        }

        uint storedLightIndex = BurtReadAdditionalLightListIndex(range.x + localLightIndex, useClusterLightList);
        if (storedLightIndex >= (uint)additionalLightCount)
        {
            continue;
        }

        BurtLight additionalLight = BurtCreateAdditionalLightUnshadowed((int)storedLightIndex, positionWS);
        additionalDirectComponents = BurtAddPBRDirectComponents(additionalDirectComponents, BurtEvaluatePBRDirectFromCore(coreData, additionalLight));
    }

    return additionalDirectComponents;
#else
    return BurtEvaluatePBRAdditionalDirectLightingUnshadowedFromCore(coreData, positionWS);
#endif
}

BurtDirectPBRComponents BurtEvaluatePBRDirectLightingFromCore(BurtPBRShadingCoreData coreData, BurtLight mainLight, float3 positionWS)
{
    BurtDirectPBRComponents directComponents = BurtEvaluatePBRDirectFromCore(coreData, mainLight);
    return BurtAddPBRDirectComponents(directComponents, BurtEvaluatePBRAdditionalDirectLightingFromCore(coreData, positionWS));
}

BurtDirectPBRComponents BurtEvaluatePBRDirectLightingFromCore(BurtPBRShadingCoreData coreData, BurtLight mainLight, float3 positionWS, float2 screenUV)
{
    BurtDirectPBRComponents directComponents = BurtEvaluatePBRDirectFromCore(coreData, mainLight);
    return BurtAddPBRDirectComponents(directComponents, BurtEvaluatePBRAdditionalDirectLightingFromCore(coreData, positionWS, screenUV));
}

BurtDirectPBRComponents BurtEvaluatePBRDirectLightingFromCore(BurtPBRShadingCoreData coreData, BurtLight mainLight, float3 positionWS, float3 shadowPositionWS, float2 screenUV)
{
    BurtDirectPBRComponents directComponents = BurtEvaluatePBRDirectFromCore(coreData, mainLight);
    return BurtAddPBRDirectComponents(directComponents, BurtEvaluatePBRAdditionalDirectLightingFromCore(coreData, positionWS, shadowPositionWS, screenUV));
}

// Indirect stage: evaluates SH diffuse and reflection-probe/sky specular.
BurtIndirectPBRComponents BurtEvaluatePBRIndirectFromCore(BurtPBRShadingCoreData coreData)
{
    BurtIndirectPBRComponents components = BurtEvaluateIndirectPBRComponents(coreData.materialData, coreData.geometryData, coreData.clearCoatGeometryData, coreData.energyTerms);
    return components;
}

// Compose stage used by rendering and debug views.
BurtPBRShadingComponents BurtComposePBRShadingComponents(BurtPBRShadingCoreData coreData, BurtDirectPBRComponents directComponents, BurtIndirectPBRComponents indirectComponents)
{
    BurtPBRShadingComponents components;

    components.perceptualRoughness = coreData.materialData.perceptualRoughness;
    components.diffuseColor = coreData.materialData.diffuseColor;
    components.f0 = coreData.materialData.f0;
    components.f90 = coreData.materialData.f90;

    float debugSpecularAARoughness = coreData.directSpecularPerceptualRoughness;
    float debugSpecularAANormalVariance = coreData.specularAATerms.normalVariance;
    float debugSpecularAARoughnessDelta = coreData.specularAATerms.roughnessDelta;
    float clearCoatMask = saturate(coreData.materialData.clearCoatMask);
#if BURT_ENABLE_CLEAR_COAT_SHADING
    if (clearCoatMask > 0.0001f)
    {
        debugSpecularAARoughness = lerp(debugSpecularAARoughness, coreData.clearCoatDirectSpecularPerceptualRoughness, clearCoatMask);
        debugSpecularAANormalVariance = lerp(debugSpecularAANormalVariance, coreData.clearCoatSpecularAATerms.normalVariance, clearCoatMask);
        debugSpecularAARoughnessDelta = lerp(debugSpecularAARoughnessDelta, coreData.clearCoatSpecularAATerms.roughnessDelta, clearCoatMask);
    }
#endif

    components.specularAARoughness = debugSpecularAARoughness;
    components.specularAANormalVariance = debugSpecularAANormalVariance;
    components.specularAARoughnessDelta = debugSpecularAARoughnessDelta;

    float3 debugDirectSpecularEnergyCompensation = coreData.energyTerms.directSpecularEnergyCompensation;
    float3 debugIndirectSpecularEnergyCompensation = coreData.energyTerms.indirectSpecularEnergyCompensation;

    components.energyPreservation = coreData.energyTerms.energyPreservation;
    float debugIndirectNoV = coreData.geometryData.nDotV;
    float debugIndirectRoughness = coreData.materialData.perceptualRoughness;
    float2 debugIndirectDFG = GetSpecularDFGTerms(debugIndirectRoughness, debugIndirectNoV);
    float3 debugIndirectEnvBRDF = EvalSpecularDFG(coreData.materialData.f0, coreData.materialData.f90, debugIndirectDFG);
#if BURT_ENABLE_CLEAR_COAT_SHADING
    if (clearCoatMask > 0.0001f)
    {
        BurtPBRMaterialData clearCoatMaterialData = BurtCreateClearCoatMaterialData(coreData.materialData);
        BurtPBREnergyTerms clearCoatEnergyTerms = BurtPreparePBREnergyTerms(clearCoatMaterialData, coreData.clearCoatGeometryData, coreData.clearCoatDirectSpecularPerceptualRoughness);
        float2 clearCoatDFG = GetSpecularDFGTerms(clearCoatMaterialData.perceptualRoughness, coreData.clearCoatGeometryData.nDotV);
        float3 clearCoatEnvBRDF = EvalSpecularDFG(clearCoatMaterialData.f0, clearCoatMaterialData.f90, clearCoatDFG);
        float3 bottomLayerEnvBRDF = EvalSpecularDFG(coreData.materialData.f0, coreData.materialData.f90, debugIndirectDFG);
        float3 clearCoatLayerTransmission = BurtEvaluateClearCoatLayerTransmission(coreData.materialData, clearCoatMaterialData, coreData.clearCoatGeometryData);

        debugDirectSpecularEnergyCompensation = lerp(debugDirectSpecularEnergyCompensation, clearCoatEnergyTerms.directSpecularEnergyCompensation, clearCoatMask);
        debugIndirectSpecularEnergyCompensation = lerp(debugIndirectSpecularEnergyCompensation, clearCoatEnergyTerms.indirectSpecularEnergyCompensation, clearCoatMask);
        debugIndirectNoV = lerp(debugIndirectNoV, coreData.clearCoatGeometryData.nDotV, clearCoatMask);
        debugIndirectRoughness = lerp(debugIndirectRoughness, clearCoatMaterialData.perceptualRoughness, clearCoatMask);
        debugIndirectDFG = lerp(debugIndirectDFG, clearCoatDFG, clearCoatMask);
        debugIndirectEnvBRDF = lerp(debugIndirectEnvBRDF, bottomLayerEnvBRDF * clearCoatLayerTransmission + clearCoatEnvBRDF, clearCoatMask);
    }
#endif

    components.specularEnergyCompensation = debugDirectSpecularEnergyCompensation;
    components.indirectSpecularEnergyCompensation = debugIndirectSpecularEnergyCompensation;
    components.directBRDFD = directComponents.brdfTerms.d;
    components.directBRDFVisibility = directComponents.brdfTerms.visibility;
    components.directBRDFFresnel = directComponents.brdfTerms.fresnel;
    components.directDiffuseLobe = directComponents.brdfTerms.diffuseLobe;
    components.directDiffuseBRDF = directComponents.brdfTerms.diffuseBRDF;
    components.directSpecularBRDF = directComponents.brdfTerms.specularBRDF;
    components.directDiffuse = directComponents.diffuse;
    components.directSpecular = directComponents.specular;
    components.directLighting = components.directDiffuse + components.directSpecular;
    components.additionalDiffuse = float3(0.0f, 0.0f, 0.0f);
    components.additionalSpecular = float3(0.0f, 0.0f, 0.0f);
    components.additionalLighting = float3(0.0f, 0.0f, 0.0f);

    components.indirectDiffuse = indirectComponents.diffuse;
    components.indirectSpecular = indirectComponents.specular;
    components.indirectLighting = components.indirectDiffuse + components.indirectSpecular;
    components.lighting = components.directLighting + components.indirectLighting;
    components.specularOcclusion = GetIndirectSpecularOcclusion(debugIndirectNoV, coreData.materialData.occlusion, debugIndirectRoughness);
    components.indirectSpecularDFG = debugIndirectDFG;
    components.indirectSpecularEnvBRDF = debugIndirectEnvBRDF;
    components.hairPrimaryLobe = 0.0f;
    components.hairSecondaryLobe = 0.0f;
    components.hairTransmissionLobe = 0.0f;
    components.hairScatter = 0.0f;
    components.clearCoatMask = coreData.materialData.clearCoatMask;
    components.subsurfaceProfileIndex = coreData.materialData.subsurfaceProfileIndex;
    components.subsurfaceTransmission = float3(0.0f, 0.0f, 0.0f);
    components.subsurfaceKernelWeight = float3(0.0f, 0.0f, 0.0f);
    components.subsurfaceIndirect = indirectComponents.subsurfaceIndirect;

#if BURT_ENABLE_SUBSURFACE_SHADING
    if (saturate(coreData.materialData.subsurfaceStrength) > 0.0001f)
    {
        components.subsurfaceTransmission = BurtSampleSubsurfaceTransmissionProfile(
            coreData.materialData.subsurfaceProfileIndex,
            coreData.materialData.subsurfaceThickness) * max(BurtLoadSubsurfaceProfileTransmissionTint(coreData.materialData.subsurfaceProfileIndex).rgb, float3(0.0f, 0.0f, 0.0f));
        components.subsurfaceKernelWeight = BurtUseSubsurfaceProfileParamLut()
            ? max(BurtFetchSubsurfaceProfileParam(BURT_SUBSURFACE_PROFILE_PARAM_KERNEL0_OFFSET, coreData.materialData.subsurfaceProfileIndex).rgb, float3(0.0f, 0.0f, 0.0f))
            : float3(0.204f, 0.236f, 0.290f);
    }
#endif

    // 返回完整拆分结果，调用方只需要决定是否叠加自发光或进�?Debug View
return components;
}

BurtPBRShadingComponents BurtComposePBRShadingComponentsWithAdditional(
    BurtPBRShadingCoreData coreData,
    BurtDirectPBRComponents directComponents,
    BurtIndirectPBRComponents indirectComponents,
    BurtDirectPBRComponents additionalDirectComponents)
{
    BurtPBRShadingComponents components = BurtComposePBRShadingComponents(coreData, directComponents, indirectComponents);
    components.additionalDiffuse = additionalDirectComponents.diffuse;
    components.additionalSpecular = additionalDirectComponents.specular;
    components.additionalLighting = components.additionalDiffuse + components.additionalSpecular;
    return components;
}

// Evaluates full PBR shading from prepared material and geometry data.
BurtPBRShadingComponents BurtEvaluatePBRShadingComponents(BurtPBRMaterialData materialData, BurtPBRGeometryData geometryData, BurtLight mainLight)
{
    BurtPBRShadingCoreData coreData = BurtPreparePBRShadingCoreData(materialData, geometryData);

    BurtDirectPBRComponents directComponents = BurtEvaluatePBRDirectFromCore(coreData, mainLight);

    BurtIndirectPBRComponents indirectComponents = BurtEvaluatePBRIndirectFromCore(coreData);

    return BurtComposePBRShadingComponents(coreData, directComponents, indirectComponents);
}

BurtPBRShadingComponents BurtEvaluatePBRShadingComponents(BurtPBRMaterialData materialData, BurtPBRGeometryData geometryData, BurtLight mainLight, float3 positionWS)
{
    BurtPBRShadingCoreData coreData = BurtPreparePBRShadingCoreData(materialData, geometryData);
    BurtDirectPBRComponents mainDirectComponents = BurtEvaluatePBRDirectFromCore(coreData, mainLight);
    BurtDirectPBRComponents additionalDirectComponents = BurtEvaluatePBRAdditionalDirectLightingFromCore(coreData, positionWS);
    BurtDirectPBRComponents directComponents = BurtAddPBRDirectComponents(mainDirectComponents, additionalDirectComponents);
    BurtIndirectPBRComponents indirectComponents = BurtEvaluatePBRIndirectFromCore(coreData);
    return BurtComposePBRShadingComponentsWithAdditional(coreData, directComponents, indirectComponents, additionalDirectComponents);
}

BurtPBRShadingComponents BurtEvaluatePBRShadingComponents(BurtPBRMaterialData materialData, BurtPBRGeometryData geometryData, BurtLight mainLight, float3 positionWS, float2 screenUV)
{
    BurtPBRShadingCoreData coreData = BurtPreparePBRShadingCoreData(materialData, geometryData);
    BurtDirectPBRComponents mainDirectComponents = BurtEvaluatePBRDirectFromCore(coreData, mainLight);
    BurtDirectPBRComponents additionalDirectComponents = BurtEvaluatePBRAdditionalDirectLightingFromCore(coreData, positionWS, screenUV);
    BurtDirectPBRComponents directComponents = BurtAddPBRDirectComponents(mainDirectComponents, additionalDirectComponents);
    BurtIndirectPBRComponents indirectComponents = BurtEvaluatePBRIndirectFromCore(coreData);
    return BurtComposePBRShadingComponentsWithAdditional(coreData, directComponents, indirectComponents, additionalDirectComponents);
}

// Evaluates full PBR shading from SurfaceData.
BurtPBRShadingComponents BurtEvaluatePBRShadingComponents(BurtSurfaceData surfaceData, BurtLight mainLight, float3 normalWS, float3 viewDirectionWS)
{
    BurtPBRShadingCoreData coreData = BurtPreparePBRShadingCoreData(surfaceData, normalWS, viewDirectionWS);

    BurtDirectPBRComponents directComponents = BurtEvaluatePBRDirectFromCore(coreData, mainLight);

    BurtIndirectPBRComponents indirectComponents = BurtEvaluatePBRIndirectFromCore(coreData);

    return BurtComposePBRShadingComponents(coreData, directComponents, indirectComponents);
}

BurtPBRShadingComponents BurtEvaluatePBRShadingComponents(BurtSurfaceData surfaceData, BurtLight mainLight, float3 normalWS, float3 viewDirectionWS, float3 positionWS)
{
    BurtPBRShadingCoreData coreData = BurtPreparePBRShadingCoreData(surfaceData, normalWS, viewDirectionWS);
    BurtDirectPBRComponents mainDirectComponents = BurtEvaluatePBRDirectFromCore(coreData, mainLight);
    BurtDirectPBRComponents additionalDirectComponents = BurtEvaluatePBRAdditionalDirectLightingFromCore(coreData, positionWS);
    BurtDirectPBRComponents directComponents = BurtAddPBRDirectComponents(mainDirectComponents, additionalDirectComponents);
    BurtIndirectPBRComponents indirectComponents = BurtEvaluatePBRIndirectFromCore(coreData);
    return BurtComposePBRShadingComponentsWithAdditional(coreData, directComponents, indirectComponents, additionalDirectComponents);
}

BurtPBRShadingComponents BurtEvaluatePBRShadingComponents(BurtSurfaceData surfaceData, BurtLight mainLight, float3 normalWS, float4 tangentWS, float3 viewDirectionWS, float3 positionWS)
{
    BurtPBRShadingCoreData coreData = BurtPreparePBRShadingCoreData(surfaceData, normalWS, tangentWS, viewDirectionWS);
    BurtDirectPBRComponents mainDirectComponents = BurtEvaluatePBRDirectFromCore(coreData, mainLight);
    BurtDirectPBRComponents additionalDirectComponents = BurtEvaluatePBRAdditionalDirectLightingFromCore(coreData, positionWS);
    BurtDirectPBRComponents directComponents = BurtAddPBRDirectComponents(mainDirectComponents, additionalDirectComponents);
    BurtIndirectPBRComponents indirectComponents = BurtEvaluatePBRIndirectFromCore(coreData);
    return BurtComposePBRShadingComponentsWithAdditional(coreData, directComponents, indirectComponents, additionalDirectComponents);
}

BurtPBRShadingComponents BurtEvaluatePBRShadingComponents(BurtSurfaceData surfaceData, BurtLight mainLight, float3 normalWS, float3 clearCoatNormalWS, float3 viewDirectionWS, float3 positionWS)
{
    BurtPBRShadingCoreData coreData = BurtPreparePBRShadingCoreData(surfaceData, normalWS, clearCoatNormalWS, viewDirectionWS);
    BurtDirectPBRComponents mainDirectComponents = BurtEvaluatePBRDirectFromCore(coreData, mainLight);
    BurtDirectPBRComponents additionalDirectComponents = BurtEvaluatePBRAdditionalDirectLightingFromCore(coreData, positionWS);
    BurtDirectPBRComponents directComponents = BurtAddPBRDirectComponents(mainDirectComponents, additionalDirectComponents);
    BurtIndirectPBRComponents indirectComponents = BurtEvaluatePBRIndirectFromCore(coreData);
    return BurtComposePBRShadingComponentsWithAdditional(coreData, directComponents, indirectComponents, additionalDirectComponents);
}

BurtPBRShadingComponents BurtEvaluatePBRShadingComponents(BurtSurfaceData surfaceData, BurtLight mainLight, float3 normalWS, float4 tangentWS, float3 clearCoatNormalWS, float3 viewDirectionWS, float3 positionWS)
{
    BurtPBRShadingCoreData coreData = BurtPreparePBRShadingCoreData(surfaceData, normalWS, tangentWS, clearCoatNormalWS, viewDirectionWS);
    BurtDirectPBRComponents mainDirectComponents = BurtEvaluatePBRDirectFromCore(coreData, mainLight);
    BurtDirectPBRComponents additionalDirectComponents = BurtEvaluatePBRAdditionalDirectLightingFromCore(coreData, positionWS);
    BurtDirectPBRComponents directComponents = BurtAddPBRDirectComponents(mainDirectComponents, additionalDirectComponents);
    BurtIndirectPBRComponents indirectComponents = BurtEvaluatePBRIndirectFromCore(coreData);
    return BurtComposePBRShadingComponentsWithAdditional(coreData, directComponents, indirectComponents, additionalDirectComponents);
}

BurtPBRShadingComponents BurtEvaluatePBRShadingComponents(BurtSurfaceData surfaceData, BurtLight mainLight, float3 normalWS, float3 viewDirectionWS, float3 positionWS, float2 screenUV)
{
    BurtPBRShadingCoreData coreData = BurtPreparePBRShadingCoreData(surfaceData, normalWS, viewDirectionWS);
    BurtDirectPBRComponents mainDirectComponents = BurtEvaluatePBRDirectFromCore(coreData, mainLight);
    BurtDirectPBRComponents additionalDirectComponents = BurtEvaluatePBRAdditionalDirectLightingFromCore(coreData, positionWS, screenUV);
    BurtDirectPBRComponents directComponents = BurtAddPBRDirectComponents(mainDirectComponents, additionalDirectComponents);
    BurtIndirectPBRComponents indirectComponents = BurtEvaluatePBRIndirectFromCore(coreData);
    return BurtComposePBRShadingComponentsWithAdditional(coreData, directComponents, indirectComponents, additionalDirectComponents);
}

// Evaluates full PBR shading from decoded GBuffer data.
BurtPBRShadingComponents BurtEvaluatePBRShadingComponentsFromGBuffer(BurtGBufferData gbufferData, BurtLight mainLight, float3 viewDirectionWS)
{
    BurtPBRShadingCoreData coreData = BurtPreparePBRShadingCoreData(gbufferData, viewDirectionWS);

    BurtDirectPBRComponents directComponents = BurtEvaluatePBRDirectFromCore(coreData, mainLight);

    BurtIndirectPBRComponents indirectComponents = BurtEvaluatePBRIndirectFromCore(coreData);

    return BurtComposePBRShadingComponents(coreData, directComponents, indirectComponents);
}

BurtPBRShadingComponents BurtEvaluatePBRShadingComponentsFromGBuffer(BurtGBufferData gbufferData, BurtLight mainLight, float3 viewDirectionWS, float3 positionWS)
{
    BurtPBRShadingCoreData coreData = BurtPreparePBRShadingCoreData(gbufferData, viewDirectionWS);
    BurtDirectPBRComponents mainDirectComponents = BurtEvaluatePBRDirectFromCore(coreData, mainLight);
    BurtDirectPBRComponents additionalDirectComponents = BurtEvaluatePBRAdditionalDirectLightingFromCore(coreData, positionWS);
    BurtDirectPBRComponents directComponents = BurtAddPBRDirectComponents(mainDirectComponents, additionalDirectComponents);
    BurtIndirectPBRComponents indirectComponents = BurtEvaluatePBRIndirectFromCore(coreData);
    return BurtComposePBRShadingComponentsWithAdditional(coreData, directComponents, indirectComponents, additionalDirectComponents);
}

BurtPBRShadingComponents BurtEvaluatePBRShadingComponentsFromGBuffer(BurtGBufferData gbufferData, BurtLight mainLight, float3 viewDirectionWS, float3 positionWS, float2 screenUV)
{
    BurtPBRShadingCoreData coreData = BurtPreparePBRShadingCoreData(gbufferData, viewDirectionWS);
    BurtDirectPBRComponents mainDirectComponents = BurtEvaluatePBRDirectFromCore(coreData, mainLight);
    BurtDirectPBRComponents additionalDirectComponents = BurtEvaluatePBRAdditionalDirectLightingFromCore(coreData, positionWS, screenUV);
    BurtDirectPBRComponents directComponents = BurtAddPBRDirectComponents(mainDirectComponents, additionalDirectComponents);
    BurtIndirectPBRComponents indirectComponents = BurtEvaluatePBRIndirectFromCore(coreData);
    return BurtComposePBRShadingComponentsWithAdditional(coreData, directComponents, indirectComponents, additionalDirectComponents);
}

BurtPBRShadingComponents BurtEvaluatePBRShadingComponentsFromGBuffer(BurtGBufferData gbufferData, BurtLight mainLight, float3 viewDirectionWS, float3 positionWS, float3 shadowPositionWS, float2 screenUV)
{
    BurtPBRShadingCoreData coreData = BurtPreparePBRShadingCoreData(gbufferData, viewDirectionWS);
    BurtDirectPBRComponents mainDirectComponents = BurtEvaluatePBRDirectFromCore(coreData, mainLight);
    BurtDirectPBRComponents additionalDirectComponents = BurtEvaluatePBRAdditionalDirectLightingFromCore(coreData, positionWS, shadowPositionWS, screenUV);
    BurtDirectPBRComponents directComponents = BurtAddPBRDirectComponents(mainDirectComponents, additionalDirectComponents);
    BurtIndirectPBRComponents indirectComponents = BurtEvaluatePBRIndirectFromCore(coreData);
    return BurtComposePBRShadingComponentsWithAdditional(coreData, directComponents, indirectComponents, additionalDirectComponents);
}

// Evaluates full PBR shading from encoded GBuffer MRT payloads.
BurtPBRShadingComponents BurtEvaluatePBRShadingComponentsFromGBuffer(BurtEncodedGBuffer encodedGBuffer, BurtLight mainLight, float3 viewDirectionWS)
{
    BurtGBufferData gbufferData = BurtDecodeGBuffer(encodedGBuffer);

    return BurtEvaluatePBRShadingComponentsFromGBuffer(gbufferData, mainLight, viewDirectionWS);
}

BurtPBRShadingComponents BurtEvaluatePBRShadingComponentsFromGBuffer(BurtEncodedGBuffer encodedGBuffer, BurtLight mainLight, float3 viewDirectionWS, float3 positionWS)
{
    BurtGBufferData gbufferData = BurtDecodeGBuffer(encodedGBuffer);
    return BurtEvaluatePBRShadingComponentsFromGBuffer(gbufferData, mainLight, viewDirectionWS, positionWS);
}

BurtPBRShadingComponents BurtEvaluatePBRShadingComponentsFromGBuffer(BurtEncodedGBuffer encodedGBuffer, BurtLight mainLight, float3 viewDirectionWS, float3 positionWS, float2 screenUV)
{
    BurtGBufferData gbufferData = BurtDecodeGBuffer(encodedGBuffer);
    return BurtEvaluatePBRShadingComponentsFromGBuffer(gbufferData, mainLight, viewDirectionWS, positionWS, screenUV);
}

#if BURT_ENABLE_HAIR_SHADING
// Hair 第一版参�?UE5 HairBsdf.ush / ShadingModels.ush：BaseColor 作为毛发吸收色，Reflectance 近似 Specular，Hair material channel 解包�?Scatter/Shift
float BurtHairGaussian(float width, float theta)
{
    float safeWidth = max(width, 0.02f);
    return exp(-0.5f * theta * theta / max(safeWidth * safeWidth, BURT_EPSILON)) / max(sqrt(2.0f * BURT_PI) * safeWidth, BURT_EPSILON);
}

float BurtHairFresnel(float cosTheta)
{
    const float eta = 1.55f;
    const float f0 = ((1.0f - eta) * (1.0f - eta)) / ((1.0f + eta) * (1.0f + eta));
    return f0 + (1.0f - f0) * Pow5(1.0f - saturate(cosTheta));
}

float3 BurtHairAbsorptionTint(float3 baseColor)
{
    // Hair has no dedicated absorption/deep-opacity GBuffer data yet; sqrt(baseColor) is a stable first-order tint proxy.
    return sqrt(saturate(baseColor));
}

float3 BurtHairColorToAbsorption(float3 color)
{
    float3 safeColor = clamp(color, float3(0.0001f, 0.0001f, 0.0001f), float3(1.0f, 1.0f, 1.0f));
    const float b = 0.3f;
    const float b2 = b * b;
    const float b3 = b * b2;
    const float b4 = b2 * b2;
    const float b5 = b * b4;
    const float d = 5.969f - 0.215f * b + 2.532f * b2 - 10.73f * b3 + 5.574f * b4 + 0.245f * b5;
    float3 absorption = log(safeColor) / d;
    return absorption * absorption;
}

float3 BurtHairSpecularF0(BurtGBufferData gbufferData)
{
    // Reflectance already includes _HairSpecularScale before GBuffer packing; keep F0 dielectric and avoid reading Lit metallic.
    float specularScale = saturate(gbufferData.reflectance) * 2.0f;
    float3 specularTint = BurtGetHairSpecularColor(gbufferData);
    float tintScale = PerceivedLuminance(specularTint);
    float3 tintColor = specularTint / max(tintScale, 0.0001f);
    return float3(0.04f, 0.04f, 0.04f) * specularScale * tintScale * tintColor;
}

float BurtHairSpecularScale(BurtGBufferData gbufferData)
{
    // UE multiplies the R lobe by Specular * 2; Burt stores that control in Reflectance after _HairSpecularScale.
    return saturate(gbufferData.reflectance) * 2.0f;
}

float3 BurtHairSafePow(float3 value, float power)
{
    return pow(max(abs(value), float3(0.001f, 0.001f, 0.001f)), max(power, 0.0f));
}

float3 BurtLimitHairSpecularEnergy(float3 specularBRDF, float roughness, float specularScale, float scatter)
{
    // Keep the compact UE-style lobes from producing fireflies at very low roughness without changing normal cases.
    float3 safeSpecularBRDF = max(specularBRDF, float3(0.0f, 0.0f, 0.0f));
    float specularLuminance = dot(safeSpecularBRDF, float3(0.2126f, 0.7152f, 0.0722f));
    float smoothness = 1.0f - saturate(roughness);
    float energyLimit = lerp(4.0f, 18.0f, smoothness) * lerp(0.8f, 1.25f, saturate(specularScale * 0.5f)) * lerp(0.9f, 1.15f, scatter);
    return safeSpecularBRDF * min(1.0f, energyLimit / max(specularLuminance, BURT_EPSILON));
}

float BurtHairRoughnessToBlinnPhongSpecularExponent(float roughness)
{
    return clamp(2.0f * rcp(max(roughness * roughness, BURT_EPSILON)) - 2.0f, BURT_EPSILON, rcp(BURT_EPSILON));
}

float BurtHairKajiyaKayPeakFromLinearRoughness(float linearRoughness)
{
    float specularExponent = BurtHairRoughnessToBlinnPhongSpecularExponent(max(linearRoughness, 1.0f / 255.0f));
    return (specularExponent + 2.0f) * (0.5f * BURT_INV_PI);
}

float BurtHairMarschnerAutoSpecularGain(float legacyPeak, float marschnerPeak, float response, float maxGain)
{
    float gain = legacyPeak / max(marschnerPeak, 0.0001f);
    return clamp(pow(max(gain, 1.0f), response), 1.0f, maxGain);
}

float3 BurtHairKajiyaKayDiffuseAttenuation(float3 baseColor, float scatter, float3 lightDirectionWS, float3 viewDirectionWS, float3 strandDirectionWS, float shadow)
{
    float kajiyaDiffuse = 1.0f - abs(dot(strandDirectionWS, lightDirectionWS));
    float3 fakeNormal = BurtSafeNormalize(viewDirectionWS - strandDirectionWS * dot(viewDirectionWS, strandDirectionWS));
    float wrappedNoL = saturate((dot(fakeNormal, lightDirectionWS) + 1.0f) * 0.25f);
    float diffuseScatter = BURT_INV_PI * lerp(wrappedNoL, kajiyaDiffuse, 0.33f) * scatter;
    float luma = max(PerceivedLuminance(baseColor), BURT_EPSILON);
    float3 scatterTint = pow(max(baseColor / luma, float3(0.001f, 0.001f, 0.001f)), saturate(1.0f - shadow));
    return sqrt(saturate(baseColor)) * diffuseScatter * scatterTint;
}

float3 BurtHairCreateFallbackNormalWS(float3 strandDirectionWS)
{
    float3 fallbackAxis = abs(strandDirectionWS.y) < 0.95f ? float3(0.0f, 1.0f, 0.0f) : float3(1.0f, 0.0f, 0.0f);
    return BurtSafeNormalize(cross(fallbackAxis, strandDirectionWS));
}

float3 BurtHairCreateViewFacingNormalWS(float3 strandDirectionWS, float3 viewDirectionWS)
{
    // Hair GBuffer only stores strand direction today; derive a stable view-facing normal for SH and reflection-probe lookups.
    float3 strand = BurtSafeNormalize(strandDirectionWS);
    float3 viewDirection = BurtSafeNormalize(viewDirectionWS);
    float3 viewNormal = viewDirection - strand * dot(viewDirection, strand);
    float normalLengthSquared = dot(viewNormal, viewNormal);
    return normalLengthSquared > 0.0001f ? viewNormal * rsqrt(normalLengthSquared) : BurtHairCreateFallbackNormalWS(strand);
}

float3 BurtHairReconstructGeometryNormalWS(float3 positionWS, float3 viewDirectionWS)
{
    float3 geometricNormalWS = cross(ddx(positionWS), ddy(positionWS));
    float normalLengthSquared = dot(geometricNormalWS, geometricNormalWS);
    if (normalLengthSquared <= 0.0001f)
    {
        return BurtSafeNormalize(viewDirectionWS);
    }

    geometricNormalWS *= rsqrt(normalLengthSquared);
    return dot(geometricNormalWS, viewDirectionWS) < 0.0f ? -geometricNormalWS : geometricNormalWS;
}

BurtGBufferData BurtResolveHairDeferredGeometryData(BurtGBufferData gbufferData, float3 viewDirectionWS, float3 positionWS)
{
    float3 strandDirectionWS = BurtSafeNormalize(BurtGetHairStrandDirectionWS(gbufferData));
    float3 hairNormalWS = BurtGetHairShadingNormalWS(gbufferData);
    float3 hairGeometryNormalWS = BurtGetHairGeometryNormalWS(gbufferData);
    float3 reconstructedGeometryNormalWS = BurtHairReconstructGeometryNormalWS(positionWS, viewDirectionWS);

    bool decodedHairMissingNormals =
        abs(dot(hairNormalWS, strandDirectionWS)) > 0.98f &&
        abs(dot(hairGeometryNormalWS, strandDirectionWS)) > 0.98f;

    if (decodedHairMissingNormals)
    {
        gbufferData.clearCoatNormalWS = reconstructedGeometryNormalWS;
        gbufferData.hairGeometryNormalWS = reconstructedGeometryNormalWS;
    }

    return gbufferData;
}

BurtPBRGeometryData BurtPrepareHairGeometryData(BurtGBufferData gbufferData, float3 viewDirectionWS)
{
    float3 hairNormalWS = BurtGetHairShadingNormalWS(gbufferData);
    float3 strandDirectionWS = BurtGetHairStrandDirectionWS(gbufferData);
    return BurtPreparePBRGeometryData(hairNormalWS, float4(strandDirectionWS, 1.0f), viewDirectionWS);
}

BurtPBRGeometryData BurtPrepareHairGeometryData(BurtGBufferData gbufferData, float3 viewDirectionWS, float3 positionWS)
{
    gbufferData = BurtResolveHairDeferredGeometryData(gbufferData, viewDirectionWS, positionWS);
    return BurtPrepareHairGeometryData(gbufferData, viewDirectionWS);
}

struct BurtHairDirectComponents
{
    float3 diffuse;
    float3 specular;
    float3 diffuseBRDF;
    float3 specularBRDF;
    float primaryLobe;
    float secondaryLobe;
    float transmissionLobe;
    float scatter;
    float diffuseLobe;
    float3 fresnel;
};

BurtHairDirectComponents BurtEvaluateHairDirectComponents(BurtGBufferData gbufferData, BurtPBRGeometryData geometryData, BurtLight light)
{
    float3 baseColor = saturate(gbufferData.baseColor);
    float3 t = BurtSafeNormalize(BurtGetHairStrandDirectionWS(gbufferData));
    float3 n = geometryData.normalWS;
    float3 geometricN = BurtGetHairGeometryNormalWS(gbufferData);
    float3 v = geometryData.viewDirectionWS;
    float3 l = BurtSafeNormalize(light.directionWS);
    float lightFalloff = saturate(light.shadowAttenuation + BurtGetHairShadowFillStrength(gbufferData));

    float roughness = clamp(gbufferData.perceptualRoughness, 1.0f / 255.0f, 1.0f);
    float secondaryRoughness = clamp(BurtGetHairSecondaryRoughness(gbufferData), 1.0f / 255.0f, 1.0f);
    float backlit = min(BurtGetHairBackLight(gbufferData), 1.0f);
    float scatter = BurtGetHairScatter(gbufferData);
    float specularScale = BurtHairSpecularScale(gbufferData);

    float3 normalSlope = n - geometricN * dot(n, geometricN);
    float3 marschnerT = BurtSafeNormalize(t + normalSlope * 1.5f);
    float sinThetaL = clamp(dot(marschnerT, l), -1.0f, 1.0f);
    float sinThetaV = clamp(dot(marschnerT, v), -1.0f, 1.0f);
    float cosThetaD = max(cos(0.5f * abs(asin(sinThetaV) - asin(sinThetaL))), 0.01f);
    float3 lp = l - sinThetaL * marschnerT;
    float3 vp = v - sinThetaV * marschnerT;
    float cosPhi = clamp(dot(lp, vp) * rsqrt(max(dot(lp, lp) * dot(vp, vp), 0.0001f)), -1.0f, 1.0f);
    float cosHalfPhi = sqrt(saturate(0.5f + 0.5f * cosPhi));
    float voL = dot(v, l);
    float nPrime = 1.19f / cosThetaD + 0.36f * cosThetaD;

    float primaryB = roughness * roughness * 0.75f;
    float ttB = roughness * roughness * 0.5f;
    float secondaryB = max(secondaryRoughness * secondaryRoughness, 0.0001f) * 0.85f;
    float alphaR = -0.07f;
    float alphaTT = 0.035f;
    float alphaTRT = 0.14f;

    float sinAlphaR = sin(alphaR);
    float cosAlphaR = cos(alphaR);
    float shiftR = 2.0f * sinAlphaR * (cosAlphaR * cosHalfPhi * sqrt(saturate(1.0f - sinThetaV * sinThetaV)) + sinAlphaR * sinThetaV);
    shiftR += BurtGetHairSpecularShift(gbufferData);
    float primaryBScale = sqrt(2.0f) * cosHalfPhi;
    float primaryM = BurtHairGaussian(primaryB * primaryBScale, sinThetaL + sinThetaV - shiftR);
    float primaryN = 0.25f * cosHalfPhi;
    float primaryFScalar = BurtHairFresnel(sqrt(saturate(0.5f + 0.5f * voL)));

    float nDotL = saturate(dot(n, l));
    float legacyVisibility = nDotL * saturate(dot(geometricN, v) * 1000000.0f);
    float legacyPrimaryPeak = 0.25f * max(primaryFScalar, 0.0001f) * BurtHairKajiyaKayPeakFromLinearRoughness(PerceptualRoughnessToLinearRoughness(roughness)) * legacyVisibility;
    float marschnerPrimaryPeak = BurtHairGaussian(max(primaryB * sqrt(2.0f), 1.0f / 255.0f), 0.0f) * primaryN * max(primaryFScalar, 0.0001f);
    float primaryAutoGain = BurtHairMarschnerAutoSpecularGain(legacyPrimaryPeak, marschnerPrimaryPeak, 1.0f, 2.0f);
    float3 specularTint = BurtGetHairSpecularColor(gbufferData);
    float tintScale = PerceivedLuminance(specularTint);
    float3 tintColor = specularTint / max(tintScale, 0.0001f);
    float3 rBaseColorTint = lerp(float3(1.0f, 1.0f, 1.0f), sqrt(abs(baseColor)), 0.6f);
    float3 primarySpecular = primaryM * primaryN * primaryFScalar * specularScale * primaryAutoGain * 2.0f * tintScale * tintColor * rBaseColorTint * lerp(1.0f, backlit, saturate(-voL));

    float ttM = BurtHairGaussian(ttB, sinThetaL + sinThetaV - alphaTT);
    float a = rcp(max(nPrime, BURT_EPSILON));
    float h = cosHalfPhi * (1.0f + a * (0.6f - 0.8f * cosPhi));
    float ttF = BurtHairFresnel(cosThetaD * sqrt(saturate(1.0f - h * h)));
    float ttFp = (1.0f - ttF) * (1.0f - ttF);
    float3 ttAbsorption = BurtHairColorToAbsorption(baseColor);
    float3 ttTint = exp(-ttAbsorption * 2.0f * abs(1.0f - (h * a) * (h * a) / cosThetaD));
    float ttN = exp(-3.65f * cosPhi - 3.98f);
    float3 transmissionSpecular = ttM * ttN * ttFp * ttTint * backlit;

    float secondaryShiftRaw = clamp((BurtGetHairSecondarySpecularShift(gbufferData) - 1.56f) / 3.33f, -2.0f, 2.0f);
    float trtM = BurtHairGaussian(secondaryB, sinThetaL + sinThetaV - alphaTRT - secondaryShiftRaw);
    float trtF = BurtHairFresnel(cosThetaD * 0.5f);
    float trtFp = (1.0f - trtF) * (1.0f - trtF) * trtF;
    float3 trtTint = BurtHairSafePow(baseColor, 0.8f / cosThetaD);
    const float trtAzimuthSharpness = 20.0f;
    const float trtAzimuthPeakLog = 0.22f;
    float trtN = exp(trtAzimuthSharpness * cosPhi - (trtAzimuthSharpness - trtAzimuthPeakLog));

    float secondaryLinearRoughness = PerceptualRoughnessToLinearRoughness(secondaryRoughness);
    float legacySecondaryPeak = 0.25f * max(trtFp, 0.0001f) * BurtHairKajiyaKayPeakFromLinearRoughness(secondaryLinearRoughness) * legacyVisibility;
    float trtReferenceN = exp(trtAzimuthPeakLog);
    float3 trtReferenceTint = BurtHairSafePow(baseColor, 0.8f);
    float marschnerSecondaryPeak = BurtHairGaussian(max(secondaryB, 1.0f / 255.0f), 0.0f) * trtReferenceN * max(trtFp, 0.0001f) * sqrt(max(PerceivedLuminance(trtReferenceTint), 0.0001f));
    float secondaryAutoGain = BurtHairMarschnerAutoSpecularGain(legacySecondaryPeak, marschnerSecondaryPeak, 0.75f, 2.1f);
    float3 secondarySpecular = trtM * trtN * trtFp * trtTint * secondaryAutoGain * 0.75f * BurtGetHairSecondarySpecularColor(gbufferData) * specularScale;

    float diffuseLobe = BURT_INV_PI * lerp(saturate((dot(BurtHairCreateViewFacingNormalWS(t, v), l) + 1.0f) * 0.25f), 1.0f - abs(sinThetaL), 0.33f);
    float transmissionLobe = ttM * ttN * ttFp * backlit;
    float3 scatterDiffuseBRDF = max(BurtHairKajiyaKayDiffuseAttenuation(baseColor, scatter, l, v, n, lightFalloff), float3(0.0f, 0.0f, 0.0f));

    BurtHairDirectComponents components;
    components.diffuseBRDF = scatterDiffuseBRDF;
    components.specularBRDF = BurtLimitHairSpecularEnergy(primarySpecular + secondarySpecular, roughness, specularScale, scatter);
    components.primaryLobe = primaryM * primaryN * specularScale;
    components.secondaryLobe = trtM * trtN;
    components.transmissionLobe = transmissionLobe;
    components.scatter = scatter;
    components.diffuseLobe = diffuseLobe;
    components.fresnel = float3(primaryFScalar, primaryFScalar, primaryFScalar);

    components.diffuse = components.diffuseBRDF * light.color;
    components.specular = components.specularBRDF * light.color * lightFalloff * nDotL + transmissionSpecular * light.color * lightFalloff;
    return components;
}

BurtHairDirectComponents BurtCreateZeroHairDirectComponents()
{
    BurtHairDirectComponents components;
    components.diffuse = float3(0.0f, 0.0f, 0.0f);
    components.specular = float3(0.0f, 0.0f, 0.0f);
    components.diffuseBRDF = float3(0.0f, 0.0f, 0.0f);
    components.specularBRDF = float3(0.0f, 0.0f, 0.0f);
    components.primaryLobe = 0.0f;
    components.secondaryLobe = 0.0f;
    components.transmissionLobe = 0.0f;
    components.scatter = 0.0f;
    components.diffuseLobe = 0.0f;
    components.fresnel = float3(0.0f, 0.0f, 0.0f);
    return components;
}

BurtHairDirectComponents BurtAddHairDirectComponents(BurtHairDirectComponents baseComponents, BurtHairDirectComponents addedComponents)
{
    baseComponents.diffuse += addedComponents.diffuse;
    baseComponents.specular += addedComponents.specular;
    baseComponents.diffuseBRDF += addedComponents.diffuseBRDF;
    baseComponents.specularBRDF += addedComponents.specularBRDF;
    baseComponents.primaryLobe += addedComponents.primaryLobe;
    baseComponents.secondaryLobe += addedComponents.secondaryLobe;
    baseComponents.transmissionLobe += addedComponents.transmissionLobe;
    baseComponents.diffuseLobe += addedComponents.diffuseLobe;
    baseComponents.scatter = max(baseComponents.scatter, addedComponents.scatter);
    baseComponents.fresnel = max(baseComponents.fresnel, addedComponents.fresnel);
    return baseComponents;
}

BurtHairDirectComponents BurtEvaluateHairAdditionalDirectLightingComponents(BurtGBufferData gbufferData, BurtPBRGeometryData geometryData, float3 positionWS)
{
    BurtHairDirectComponents additionalDirectComponents = BurtCreateZeroHairDirectComponents();
    int additionalLightCount = BurtGetAdditionalLightCount();

    [loop]
    for (int lightIndex = 0; lightIndex < BURT_MAX_ADDITIONAL_LIGHTS; lightIndex++)
    {
        if (lightIndex >= additionalLightCount)
        {
            break;
        }

        BurtLight additionalLight = BurtCreateAdditionalLight(lightIndex, positionWS, geometryData.normalWS);
        additionalDirectComponents = BurtAddHairDirectComponents(additionalDirectComponents, BurtEvaluateHairDirectComponents(gbufferData, geometryData, additionalLight));
    }

    return additionalDirectComponents;
}

BurtHairDirectComponents BurtEvaluateHairAdditionalDirectLightingComponents(BurtGBufferData gbufferData, BurtPBRGeometryData geometryData, float3 positionWS, float3 shadowPositionWS)
{
    BurtHairDirectComponents additionalDirectComponents = BurtCreateZeroHairDirectComponents();
    int additionalLightCount = BurtGetAdditionalLightCount();

    [loop]
    for (int lightIndex = 0; lightIndex < BURT_MAX_ADDITIONAL_LIGHTS; lightIndex++)
    {
        if (lightIndex >= additionalLightCount)
        {
            break;
        }

        BurtLight additionalLight = BurtCreateAdditionalLight(lightIndex, positionWS, geometryData.normalWS, shadowPositionWS);
        additionalDirectComponents = BurtAddHairDirectComponents(additionalDirectComponents, BurtEvaluateHairDirectComponents(gbufferData, geometryData, additionalLight));
    }

    return additionalDirectComponents;
}

BurtHairDirectComponents BurtEvaluateHairAdditionalDirectLightingComponents(BurtGBufferData gbufferData, BurtPBRGeometryData geometryData, float3 positionWS, float2 screenUV)
{
#if defined(BURT_USE_TILED_LIGHTING)
    uint2 range = uint2(0u, 0u);
    uint useClusterLightList = 0u;
    if (!BurtTryGetAdditionalLightListRange(screenUV, positionWS, range, useClusterLightList))
    {
        return BurtEvaluateHairAdditionalDirectLightingComponents(gbufferData, geometryData, positionWS);
    }

    BurtHairDirectComponents additionalDirectComponents = BurtCreateZeroHairDirectComponents();
    int additionalLightCount = BurtGetAdditionalLightCount();

    [loop]
    for (uint localLightIndex = 0u; localLightIndex < BURT_MAX_ADDITIONAL_LIGHTS; localLightIndex++)
    {
        if (localLightIndex >= range.y)
        {
            break;
        }

        uint storedLightIndex = BurtReadAdditionalLightListIndex(range.x + localLightIndex, useClusterLightList);
        if (storedLightIndex >= (uint)additionalLightCount)
        {
            continue;
        }

        BurtLight additionalLight = BurtCreateAdditionalLight((int)storedLightIndex, positionWS, geometryData.normalWS);
        additionalDirectComponents = BurtAddHairDirectComponents(additionalDirectComponents, BurtEvaluateHairDirectComponents(gbufferData, geometryData, additionalLight));
    }

    return additionalDirectComponents;
#else
    return BurtEvaluateHairAdditionalDirectLightingComponents(gbufferData, geometryData, positionWS);
#endif
}

BurtHairDirectComponents BurtEvaluateHairAdditionalDirectLightingComponents(BurtGBufferData gbufferData, BurtPBRGeometryData geometryData, float3 positionWS, float3 shadowPositionWS, float2 screenUV)
{
#if defined(BURT_USE_TILED_LIGHTING)
    uint2 range = uint2(0u, 0u);
    uint useClusterLightList = 0u;
    if (!BurtTryGetAdditionalLightListRange(screenUV, positionWS, range, useClusterLightList))
    {
        return BurtEvaluateHairAdditionalDirectLightingComponents(gbufferData, geometryData, positionWS, shadowPositionWS);
    }

    BurtHairDirectComponents additionalDirectComponents = BurtCreateZeroHairDirectComponents();
    int additionalLightCount = BurtGetAdditionalLightCount();

    [loop]
    for (uint localLightIndex = 0u; localLightIndex < BURT_MAX_ADDITIONAL_LIGHTS; localLightIndex++)
    {
        if (localLightIndex >= range.y)
        {
            break;
        }

        uint storedLightIndex = BurtReadAdditionalLightListIndex(range.x + localLightIndex, useClusterLightList);
        if (storedLightIndex >= (uint)additionalLightCount)
        {
            continue;
        }

        BurtLight additionalLight = BurtCreateAdditionalLight((int)storedLightIndex, positionWS, geometryData.normalWS, shadowPositionWS);
        additionalDirectComponents = BurtAddHairDirectComponents(additionalDirectComponents, BurtEvaluateHairDirectComponents(gbufferData, geometryData, additionalLight));
    }

    return additionalDirectComponents;
#else
    return BurtEvaluateHairAdditionalDirectLightingComponents(gbufferData, geometryData, positionWS, shadowPositionWS);
#endif
}

BurtHairDirectComponents BurtEvaluateHairAdditionalDirectLightingUnshadowedComponents(BurtGBufferData gbufferData, BurtPBRGeometryData geometryData, float3 positionWS)
{
    BurtHairDirectComponents additionalDirectComponents = BurtCreateZeroHairDirectComponents();
    int additionalLightCount = BurtGetAdditionalLightCount();

    [loop]
    for (int lightIndex = 0; lightIndex < BURT_MAX_ADDITIONAL_LIGHTS; lightIndex++)
    {
        if (lightIndex >= additionalLightCount)
        {
            break;
        }

        BurtLight additionalLight = BurtCreateAdditionalLightUnshadowed(lightIndex, positionWS);
        additionalDirectComponents = BurtAddHairDirectComponents(additionalDirectComponents, BurtEvaluateHairDirectComponents(gbufferData, geometryData, additionalLight));
    }

    return additionalDirectComponents;
}

BurtHairDirectComponents BurtEvaluateHairAdditionalDirectLightingUnshadowedComponents(BurtGBufferData gbufferData, BurtPBRGeometryData geometryData, float3 positionWS, float2 screenUV)
{
#if defined(BURT_USE_TILED_LIGHTING)
    uint2 range = uint2(0u, 0u);
    uint useClusterLightList = 0u;
    if (!BurtTryGetAdditionalLightListRange(screenUV, positionWS, range, useClusterLightList))
    {
        return BurtEvaluateHairAdditionalDirectLightingUnshadowedComponents(gbufferData, geometryData, positionWS);
    }

    BurtHairDirectComponents additionalDirectComponents = BurtCreateZeroHairDirectComponents();
    int additionalLightCount = BurtGetAdditionalLightCount();

    [loop]
    for (uint localLightIndex = 0u; localLightIndex < BURT_MAX_ADDITIONAL_LIGHTS; localLightIndex++)
    {
        if (localLightIndex >= range.y)
        {
            break;
        }

        uint storedLightIndex = BurtReadAdditionalLightListIndex(range.x + localLightIndex, useClusterLightList);
        if (storedLightIndex >= (uint)additionalLightCount)
        {
            continue;
        }

        BurtLight additionalLight = BurtCreateAdditionalLightUnshadowed((int)storedLightIndex, positionWS);
        additionalDirectComponents = BurtAddHairDirectComponents(additionalDirectComponents, BurtEvaluateHairDirectComponents(gbufferData, geometryData, additionalLight));
    }

    return additionalDirectComponents;
#else
    return BurtEvaluateHairAdditionalDirectLightingUnshadowedComponents(gbufferData, geometryData, positionWS);
#endif
}

BurtHairDirectComponents BurtEvaluateHairDirectLightingComponents(BurtGBufferData gbufferData, BurtPBRGeometryData geometryData, BurtLight mainLight, float3 positionWS)
{
    BurtHairDirectComponents directComponents = BurtEvaluateHairDirectComponents(gbufferData, geometryData, mainLight);
    return BurtAddHairDirectComponents(directComponents, BurtEvaluateHairAdditionalDirectLightingComponents(gbufferData, geometryData, positionWS));
}

BurtHairDirectComponents BurtEvaluateHairDirectLightingComponents(BurtGBufferData gbufferData, BurtPBRGeometryData geometryData, BurtLight mainLight, float3 positionWS, float2 screenUV)
{
    BurtHairDirectComponents directComponents = BurtEvaluateHairDirectComponents(gbufferData, geometryData, mainLight);
    return BurtAddHairDirectComponents(directComponents, BurtEvaluateHairAdditionalDirectLightingComponents(gbufferData, geometryData, positionWS, screenUV));
}

float3 BurtEvaluateHairIndirectDiffuse(BurtGBufferData gbufferData, float3 hairNormalWS)
{
    // First-step Hair has no deep opacity/dual scattering data; use view-facing strand normal as a soft colored fill direction.
    float scatter = BurtGetHairScatter(gbufferData);
    float3 frontIrradiance = BurtSampleIndirectDiffuseIrradiance(hairNormalWS);
    float3 backIrradiance = BurtSampleIndirectDiffuseIrradiance(-hairNormalWS);
    float3 irradiance = lerp(frontIrradiance, 0.5f * (frontIrradiance + backIrradiance), scatter * 0.65f);
    float3 absorptionTint = BurtHairAbsorptionTint(gbufferData.baseColor);
    return absorptionTint * irradiance * saturate(gbufferData.occlusion) * lerp(0.35f, 1.0f, scatter);
}

float3 BurtEvaluateHairIndirectSpecular(BurtGBufferData gbufferData, BurtPBRGeometryData geometryData, out float3 envBRDF)
{
    // Keep a small environment rim so Hair is not lit by the DefaultLit GGX path but still responds to reflection probes.
    float3 radiance = SampleIndirectSpecularRadiance(geometryData.reflectionDirectionWS, gbufferData.perceptualRoughness);
    float3 f0 = BurtHairSpecularF0(gbufferData);
    envBRDF = F_Schlick(f0, geometryData.nDotV);
    float scatter = BurtGetHairScatter(gbufferData);
    float grazing = Pow5(1.0f - saturate(geometryData.nDotV));
    float specularOcclusion = GetIndirectSpecularOcclusion(geometryData.nDotV, gbufferData.occlusion, gbufferData.perceptualRoughness);
    float environmentScale = lerp(0.18f, 0.45f, scatter) * lerp(1.0f, 1.35f, grazing);
    return radiance * envBRDF * specularOcclusion * environmentScale;
}

BurtPBRShadingComponents BurtEvaluateHairShadingComponentsFromGBuffer(BurtGBufferData gbufferData, BurtLight mainLight, float3 viewDirectionWS)
{
    // Initialize with the existing PBR component layout so all debug fields stay valid, then overwrite the lighting lobes with Hair results.
    BurtPBRMaterialData materialData = BurtPreparePBRMaterialData(gbufferData);
    BurtPBRGeometryData geometryData = BurtPrepareHairGeometryData(gbufferData, viewDirectionWS);
    float3 hairNormalWS = geometryData.normalWS;
    BurtPBRShadingComponents components = BurtEvaluatePBRShadingComponents(materialData, geometryData, mainLight);

    BurtHairDirectComponents hairDirect = BurtEvaluateHairDirectComponents(gbufferData, geometryData, mainLight);
    float3 hairEnvBRDF;
    float3 hairIndirectDiffuse = BurtEvaluateHairIndirectDiffuse(gbufferData, hairNormalWS);
    float3 hairIndirectSpecular = BurtEvaluateHairIndirectSpecular(gbufferData, geometryData, hairEnvBRDF);

    components.diffuseColor = BurtHairAbsorptionTint(gbufferData.baseColor);
    components.f0 = BurtHairSpecularF0(gbufferData);
    components.f90 = float3(1.0f, 1.0f, 1.0f);
    components.directDiffuse = hairDirect.diffuse;
    components.directSpecular = hairDirect.specular;
    components.directLighting = components.directDiffuse + components.directSpecular;
    components.indirectDiffuse = hairIndirectDiffuse;
    components.indirectSpecular = hairIndirectSpecular;
    components.indirectLighting = components.indirectDiffuse + components.indirectSpecular;
    components.lighting = components.directLighting + components.indirectLighting;

    components.perceptualRoughness = gbufferData.perceptualRoughness;
    components.specularAARoughness = gbufferData.perceptualRoughness;
    components.specularAANormalVariance = 0.0f;
    components.specularAARoughnessDelta = 0.0f;
    components.specularEnergyCompensation = float3(1.0f, 1.0f, 1.0f);
    components.indirectSpecularEnergyCompensation = float3(1.0f, 1.0f, 1.0f);
    components.energyPreservation = 1.0f;
    components.specularOcclusion = saturate(gbufferData.occlusion);
    components.directBRDFD = hairDirect.primaryLobe;
    components.directBRDFVisibility = 1.0f;
    components.directBRDFFresnel = hairDirect.fresnel;
    components.directDiffuseLobe = hairDirect.diffuseLobe;
    components.directDiffuseBRDF = hairDirect.diffuseBRDF;
    components.directSpecularBRDF = hairDirect.specularBRDF;
    components.indirectSpecularDFG = float2(0.0f, 0.0f);
    components.indirectSpecularEnvBRDF = hairEnvBRDF;
    components.hairPrimaryLobe = hairDirect.primaryLobe;
    components.hairSecondaryLobe = hairDirect.secondaryLobe;
    components.hairTransmissionLobe = hairDirect.transmissionLobe;
    components.hairScatter = hairDirect.scatter;
    components.clearCoatMask = 0.0f;
    components.subsurfaceProfileIndex = 0.0f;
    components.subsurfaceTransmission = float3(0.0f, 0.0f, 0.0f);
    components.subsurfaceKernelWeight = float3(0.0f, 0.0f, 0.0f);
    components.subsurfaceIndirect = float3(0.0f, 0.0f, 0.0f);
    return components;
}

BurtPBRShadingComponents BurtEvaluateHairShadingComponentsFromGBuffer(BurtGBufferData gbufferData, BurtLight mainLight, float3 viewDirectionWS, float3 positionWS)
{
    gbufferData = BurtResolveHairDeferredGeometryData(gbufferData, viewDirectionWS, positionWS);
    BurtPBRMaterialData materialData = BurtPreparePBRMaterialData(gbufferData);
    BurtPBRGeometryData geometryData = BurtPrepareHairGeometryData(gbufferData, viewDirectionWS);
    float3 hairNormalWS = geometryData.normalWS;
    BurtPBRShadingComponents components = BurtEvaluatePBRShadingComponents(materialData, geometryData, mainLight);

    BurtHairDirectComponents hairAdditionalDirect = BurtEvaluateHairAdditionalDirectLightingComponents(gbufferData, geometryData, positionWS);
    BurtHairDirectComponents hairDirect = BurtAddHairDirectComponents(BurtEvaluateHairDirectComponents(gbufferData, geometryData, mainLight), hairAdditionalDirect);
    float3 hairEnvBRDF;
    float3 hairIndirectDiffuse = BurtEvaluateHairIndirectDiffuse(gbufferData, hairNormalWS);
    float3 hairIndirectSpecular = BurtEvaluateHairIndirectSpecular(gbufferData, geometryData, hairEnvBRDF);

    components.diffuseColor = BurtHairAbsorptionTint(gbufferData.baseColor);
    components.f0 = BurtHairSpecularF0(gbufferData);
    components.f90 = float3(1.0f, 1.0f, 1.0f);
    components.directDiffuse = hairDirect.diffuse;
    components.directSpecular = hairDirect.specular;
    components.directLighting = components.directDiffuse + components.directSpecular;
    components.additionalDiffuse = hairAdditionalDirect.diffuse;
    components.additionalSpecular = hairAdditionalDirect.specular;
    components.additionalLighting = components.additionalDiffuse + components.additionalSpecular;
    components.indirectDiffuse = hairIndirectDiffuse;
    components.indirectSpecular = hairIndirectSpecular;
    components.indirectLighting = components.indirectDiffuse + components.indirectSpecular;
    components.lighting = components.directLighting + components.indirectLighting;

    components.perceptualRoughness = gbufferData.perceptualRoughness;
    components.specularAARoughness = gbufferData.perceptualRoughness;
    components.specularAANormalVariance = 0.0f;
    components.specularAARoughnessDelta = 0.0f;
    components.specularEnergyCompensation = float3(1.0f, 1.0f, 1.0f);
    components.indirectSpecularEnergyCompensation = float3(1.0f, 1.0f, 1.0f);
    components.energyPreservation = 1.0f;
    components.specularOcclusion = saturate(gbufferData.occlusion);
    components.directBRDFD = hairDirect.primaryLobe;
    components.directBRDFVisibility = 1.0f;
    components.directBRDFFresnel = hairDirect.fresnel;
    components.directDiffuseLobe = hairDirect.diffuseLobe;
    components.directDiffuseBRDF = hairDirect.diffuseBRDF;
    components.directSpecularBRDF = hairDirect.specularBRDF;
    components.indirectSpecularDFG = float2(0.0f, 0.0f);
    components.indirectSpecularEnvBRDF = hairEnvBRDF;
    components.hairPrimaryLobe = hairDirect.primaryLobe;
    components.hairSecondaryLobe = hairDirect.secondaryLobe;
    components.hairTransmissionLobe = hairDirect.transmissionLobe;
    components.hairScatter = hairDirect.scatter;
    components.clearCoatMask = 0.0f;
    components.subsurfaceProfileIndex = 0.0f;
    components.subsurfaceTransmission = float3(0.0f, 0.0f, 0.0f);
    components.subsurfaceKernelWeight = float3(0.0f, 0.0f, 0.0f);
    components.subsurfaceIndirect = float3(0.0f, 0.0f, 0.0f);
    return components;
}

BurtPBRShadingComponents BurtEvaluateHairShadingComponentsFromGBuffer(BurtGBufferData gbufferData, BurtLight mainLight, float3 viewDirectionWS, float3 positionWS, float2 screenUV)
{
    gbufferData = BurtResolveHairDeferredGeometryData(gbufferData, viewDirectionWS, positionWS);
    BurtPBRMaterialData materialData = BurtPreparePBRMaterialData(gbufferData);
    BurtPBRGeometryData geometryData = BurtPrepareHairGeometryData(gbufferData, viewDirectionWS);
    float3 hairNormalWS = geometryData.normalWS;
    BurtPBRShadingComponents components = BurtEvaluatePBRShadingComponents(materialData, geometryData, mainLight);

    BurtHairDirectComponents hairAdditionalDirect = BurtEvaluateHairAdditionalDirectLightingComponents(gbufferData, geometryData, positionWS, screenUV);
    BurtHairDirectComponents hairDirect = BurtAddHairDirectComponents(BurtEvaluateHairDirectComponents(gbufferData, geometryData, mainLight), hairAdditionalDirect);
    float3 hairEnvBRDF;
    float3 hairIndirectDiffuse = BurtEvaluateHairIndirectDiffuse(gbufferData, hairNormalWS);
    float3 hairIndirectSpecular = BurtEvaluateHairIndirectSpecular(gbufferData, geometryData, hairEnvBRDF);

    components.diffuseColor = BurtHairAbsorptionTint(gbufferData.baseColor);
    components.f0 = BurtHairSpecularF0(gbufferData);
    components.f90 = float3(1.0f, 1.0f, 1.0f);
    components.directDiffuse = hairDirect.diffuse;
    components.directSpecular = hairDirect.specular;
    components.additionalDiffuse = hairAdditionalDirect.diffuse;
    components.additionalSpecular = hairAdditionalDirect.specular;
    components.additionalLighting = components.additionalDiffuse + components.additionalSpecular;
    components.directLighting = components.directDiffuse + components.directSpecular;
    components.indirectDiffuse = hairIndirectDiffuse;
    components.indirectSpecular = hairIndirectSpecular;
    components.indirectLighting = components.indirectDiffuse + components.indirectSpecular;
    components.lighting = components.directLighting + components.indirectLighting;
    components.perceptualRoughness = gbufferData.perceptualRoughness;
    components.specularAARoughness = gbufferData.perceptualRoughness;
    components.specularAANormalVariance = 0.0f;
    components.specularAARoughnessDelta = 0.0f;
    components.specularEnergyCompensation = float3(1.0f, 1.0f, 1.0f);
    components.indirectSpecularEnergyCompensation = float3(1.0f, 1.0f, 1.0f);
    components.energyPreservation = 1.0f;
    components.specularOcclusion = saturate(gbufferData.occlusion);
    components.directBRDFD = hairDirect.primaryLobe;
    components.directBRDFVisibility = 1.0f;
    components.directBRDFFresnel = hairDirect.fresnel;
    components.directDiffuseLobe = hairDirect.diffuseLobe;
    components.directDiffuseBRDF = hairDirect.diffuseBRDF;
    components.directSpecularBRDF = hairDirect.specularBRDF;
    components.indirectSpecularDFG = float2(0.0f, 0.0f);
    components.indirectSpecularEnvBRDF = hairEnvBRDF;
    components.hairPrimaryLobe = hairDirect.primaryLobe;
    components.hairSecondaryLobe = hairDirect.secondaryLobe;
    components.hairTransmissionLobe = hairDirect.transmissionLobe;
    components.hairScatter = hairDirect.scatter;
    components.clearCoatMask = 0.0f;
    components.subsurfaceProfileIndex = 0.0f;
    components.subsurfaceTransmission = float3(0.0f, 0.0f, 0.0f);
    components.subsurfaceKernelWeight = float3(0.0f, 0.0f, 0.0f);
    components.subsurfaceIndirect = float3(0.0f, 0.0f, 0.0f);
    return components;
}

BurtPBRShadingComponents BurtEvaluateHairShadingComponentsFromGBuffer(BurtGBufferData gbufferData, BurtLight mainLight, float3 viewDirectionWS, float3 positionWS, float3 shadowPositionWS, float2 screenUV)
{
    gbufferData = BurtResolveHairDeferredGeometryData(gbufferData, viewDirectionWS, positionWS);
    BurtPBRMaterialData materialData = BurtPreparePBRMaterialData(gbufferData);
    BurtPBRGeometryData geometryData = BurtPrepareHairGeometryData(gbufferData, viewDirectionWS);
    float3 hairNormalWS = geometryData.normalWS;
    BurtPBRShadingComponents components = BurtEvaluatePBRShadingComponents(materialData, geometryData, mainLight);

    BurtHairDirectComponents hairAdditionalDirect = BurtEvaluateHairAdditionalDirectLightingComponents(gbufferData, geometryData, positionWS, shadowPositionWS, screenUV);
    BurtHairDirectComponents hairDirect = BurtAddHairDirectComponents(BurtEvaluateHairDirectComponents(gbufferData, geometryData, mainLight), hairAdditionalDirect);
    float3 hairEnvBRDF;
    float3 hairIndirectDiffuse = BurtEvaluateHairIndirectDiffuse(gbufferData, hairNormalWS);
    float3 hairIndirectSpecular = BurtEvaluateHairIndirectSpecular(gbufferData, geometryData, hairEnvBRDF);

    components.diffuseColor = BurtHairAbsorptionTint(gbufferData.baseColor);
    components.f0 = BurtHairSpecularF0(gbufferData);
    components.f90 = float3(1.0f, 1.0f, 1.0f);
    components.directDiffuse = hairDirect.diffuse;
    components.directSpecular = hairDirect.specular;
    components.additionalDiffuse = hairAdditionalDirect.diffuse;
    components.additionalSpecular = hairAdditionalDirect.specular;
    components.additionalLighting = components.additionalDiffuse + components.additionalSpecular;
    components.directLighting = components.directDiffuse + components.directSpecular;
    components.indirectDiffuse = hairIndirectDiffuse;
    components.indirectSpecular = hairIndirectSpecular;
    components.indirectLighting = components.indirectDiffuse + components.indirectSpecular;
    components.lighting = components.directLighting + components.indirectLighting;
    components.perceptualRoughness = gbufferData.perceptualRoughness;
    components.specularAARoughness = gbufferData.perceptualRoughness;
    components.specularAANormalVariance = 0.0f;
    components.specularAARoughnessDelta = 0.0f;
    components.specularEnergyCompensation = float3(1.0f, 1.0f, 1.0f);
    components.indirectSpecularEnergyCompensation = float3(1.0f, 1.0f, 1.0f);
    components.energyPreservation = 1.0f;
    components.specularOcclusion = saturate(gbufferData.occlusion);
    components.directBRDFD = hairDirect.primaryLobe;
    components.directBRDFVisibility = 1.0f;
    components.directBRDFFresnel = hairDirect.fresnel;
    components.directDiffuseLobe = hairDirect.diffuseLobe;
    components.directDiffuseBRDF = hairDirect.diffuseBRDF;
    components.directSpecularBRDF = hairDirect.specularBRDF;
    components.indirectSpecularDFG = float2(0.0f, 0.0f);
    components.indirectSpecularEnvBRDF = hairEnvBRDF;
    components.hairPrimaryLobe = hairDirect.primaryLobe;
    components.hairSecondaryLobe = hairDirect.secondaryLobe;
    components.hairTransmissionLobe = hairDirect.transmissionLobe;
    components.hairScatter = hairDirect.scatter;
    components.clearCoatMask = 0.0f;
    components.subsurfaceProfileIndex = 0.0f;
    components.subsurfaceTransmission = float3(0.0f, 0.0f, 0.0f);
    components.subsurfaceKernelWeight = float3(0.0f, 0.0f, 0.0f);
    components.subsurfaceIndirect = float3(0.0f, 0.0f, 0.0f);
    return components;
}
#endif

#if !defined(BURT_DEFERRED_LIGHTING_SINGLE_SHADING_MODEL) && !defined(BURT_FORWARD_SINGLE_SHADING_MODEL)
// Shading-model dispatch used by Forward and Deferred. More models can join this switch without changing pass wiring.
BurtPBRShadingComponents BurtEvaluateShadingModelComponents(BurtSurfaceData surfaceData, BurtLight mainLight, float3 normalWS, float3 viewDirectionWS)
{
#if BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(surfaceData.shadingModelID))
    {
        BurtGBufferData hairGBufferData = BurtCreateHairGBufferData(surfaceData, normalWS, float3(0.0f, 0.0f, 0.0f));
        return BurtEvaluateHairShadingComponentsFromGBuffer(hairGBufferData, mainLight, viewDirectionWS);
    }
#endif

    return BurtEvaluatePBRShadingComponents(surfaceData, mainLight, normalWS, viewDirectionWS);
}

BurtPBRShadingComponents BurtEvaluateShadingModelComponents(BurtSurfaceData surfaceData, BurtLight mainLight, float3 normalWS, float3 viewDirectionWS, float3 positionWS)
{
#if BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(surfaceData.shadingModelID))
    {
        BurtGBufferData hairGBufferData = BurtCreateHairGBufferData(surfaceData, normalWS, float3(0.0f, 0.0f, 0.0f));
        return BurtEvaluateHairShadingComponentsFromGBuffer(hairGBufferData, mainLight, viewDirectionWS, positionWS);
    }
#endif

    return BurtEvaluatePBRShadingComponents(surfaceData, mainLight, normalWS, viewDirectionWS, positionWS);
}

BurtPBRShadingComponents BurtEvaluateShadingModelComponents(BurtSurfaceData surfaceData, BurtLight mainLight, float3 normalWS, float3 viewDirectionWS, float3 positionWS, float2 screenUV)
{
#if BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(surfaceData.shadingModelID))
    {
        BurtGBufferData hairGBufferData = BurtCreateHairGBufferData(surfaceData, normalWS, float3(0.0f, 0.0f, 0.0f));
        return BurtEvaluateHairShadingComponentsFromGBuffer(hairGBufferData, mainLight, viewDirectionWS, positionWS, screenUV);
    }
#endif

    return BurtEvaluatePBRShadingComponents(surfaceData, mainLight, normalWS, viewDirectionWS, positionWS, screenUV);
}

float3 BurtEvaluateAdditionalLightingUnshadowedDebug(BurtSurfaceData surfaceData, float3 normalWS, float3 viewDirectionWS, float3 positionWS)
{
#if BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(surfaceData.shadingModelID))
    {
        BurtGBufferData hairGBufferData = BurtCreateHairGBufferData(surfaceData, normalWS, float3(0.0f, 0.0f, 0.0f));
        BurtPBRGeometryData hairGeometryData = BurtPrepareHairGeometryData(hairGBufferData, viewDirectionWS);
        BurtHairDirectComponents hairAdditional = BurtEvaluateHairAdditionalDirectLightingUnshadowedComponents(hairGBufferData, hairGeometryData, positionWS);
        return hairAdditional.diffuse + hairAdditional.specular;
    }
#endif

    BurtPBRShadingCoreData coreData = BurtPreparePBRShadingCoreData(surfaceData, normalWS, viewDirectionWS);
    BurtDirectPBRComponents additional = BurtEvaluatePBRAdditionalDirectLightingUnshadowedFromCore(coreData, positionWS);
    return additional.diffuse + additional.specular;
}

float3 BurtEvaluateAdditionalLightingUnshadowedDebug(BurtSurfaceData surfaceData, float3 normalWS, float3 viewDirectionWS, float3 positionWS, float2 screenUV)
{
#if BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(surfaceData.shadingModelID))
    {
        BurtGBufferData hairGBufferData = BurtCreateHairGBufferData(surfaceData, normalWS, float3(0.0f, 0.0f, 0.0f));
        BurtPBRGeometryData hairGeometryData = BurtPrepareHairGeometryData(hairGBufferData, viewDirectionWS);
        BurtHairDirectComponents hairAdditional = BurtEvaluateHairAdditionalDirectLightingUnshadowedComponents(hairGBufferData, hairGeometryData, positionWS, screenUV);
        return hairAdditional.diffuse + hairAdditional.specular;
    }
#endif

    BurtPBRShadingCoreData coreData = BurtPreparePBRShadingCoreData(surfaceData, normalWS, viewDirectionWS);
    BurtDirectPBRComponents additional = BurtEvaluatePBRAdditionalDirectLightingUnshadowedFromCore(coreData, positionWS, screenUV);
    return additional.diffuse + additional.specular;
}

float3 BurtEvaluateAdditionalLightingUnshadowedDebugFromGBuffer(BurtGBufferData gbufferData, float3 viewDirectionWS, float3 positionWS)
{
#if BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(gbufferData.shadingModelID))
    {
        gbufferData = BurtResolveHairDeferredGeometryData(gbufferData, viewDirectionWS, positionWS);
        BurtPBRGeometryData hairGeometryData = BurtPrepareHairGeometryData(gbufferData, viewDirectionWS);
        BurtHairDirectComponents hairAdditional = BurtEvaluateHairAdditionalDirectLightingUnshadowedComponents(gbufferData, hairGeometryData, positionWS);
        return hairAdditional.diffuse + hairAdditional.specular;
    }
#endif

    BurtPBRShadingCoreData coreData = BurtPreparePBRShadingCoreData(gbufferData, viewDirectionWS);
    BurtDirectPBRComponents additional = BurtEvaluatePBRAdditionalDirectLightingUnshadowedFromCore(coreData, positionWS);
    return additional.diffuse + additional.specular;
}

float3 BurtEvaluateAdditionalLightingUnshadowedDebugFromGBuffer(BurtGBufferData gbufferData, float3 viewDirectionWS, float3 positionWS, float2 screenUV)
{
#if BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(gbufferData.shadingModelID))
    {
        gbufferData = BurtResolveHairDeferredGeometryData(gbufferData, viewDirectionWS, positionWS);
        BurtPBRGeometryData hairGeometryData = BurtPrepareHairGeometryData(gbufferData, viewDirectionWS);
        BurtHairDirectComponents hairAdditional = BurtEvaluateHairAdditionalDirectLightingUnshadowedComponents(gbufferData, hairGeometryData, positionWS, screenUV);
        return hairAdditional.diffuse + hairAdditional.specular;
    }
#endif

    BurtPBRShadingCoreData coreData = BurtPreparePBRShadingCoreData(gbufferData, viewDirectionWS);
    BurtDirectPBRComponents additional = BurtEvaluatePBRAdditionalDirectLightingUnshadowedFromCore(coreData, positionWS, screenUV);
    return additional.diffuse + additional.specular;
}

BurtPBRShadingComponents BurtEvaluateShadingModelComponentsFromGBuffer(BurtGBufferData gbufferData, BurtLight mainLight, float3 viewDirectionWS)
{
#if BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(gbufferData.shadingModelID))
    {
        return BurtEvaluateHairShadingComponentsFromGBuffer(gbufferData, mainLight, viewDirectionWS);
    }
#endif

    return BurtEvaluatePBRShadingComponentsFromGBuffer(gbufferData, mainLight, viewDirectionWS);
}

BurtPBRShadingComponents BurtEvaluateShadingModelComponentsFromGBuffer(BurtGBufferData gbufferData, BurtLight mainLight, float3 viewDirectionWS, float3 positionWS)
{
#if BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(gbufferData.shadingModelID))
    {
        return BurtEvaluateHairShadingComponentsFromGBuffer(gbufferData, mainLight, viewDirectionWS, positionWS);
    }
#endif

    return BurtEvaluatePBRShadingComponentsFromGBuffer(gbufferData, mainLight, viewDirectionWS, positionWS);
}

BurtPBRShadingComponents BurtEvaluateShadingModelComponentsFromGBuffer(BurtGBufferData gbufferData, BurtLight mainLight, float3 viewDirectionWS, float3 positionWS, float2 screenUV)
{
#if BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(gbufferData.shadingModelID))
    {
        return BurtEvaluateHairShadingComponentsFromGBuffer(gbufferData, mainLight, viewDirectionWS, positionWS, screenUV);
    }
#endif

    return BurtEvaluatePBRShadingComponentsFromGBuffer(gbufferData, mainLight, viewDirectionWS, positionWS, screenUV);
}

BurtPBRShadingComponents BurtEvaluateShadingModelComponentsFromGBuffer(BurtGBufferData gbufferData, BurtLight mainLight, float3 viewDirectionWS, float3 positionWS, float3 shadowPositionWS, float2 screenUV)
{
#if BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(gbufferData.shadingModelID))
    {
        return BurtEvaluateHairShadingComponentsFromGBuffer(gbufferData, mainLight, viewDirectionWS, positionWS, shadowPositionWS, screenUV);
    }
#endif

    return BurtEvaluatePBRShadingComponentsFromGBuffer(gbufferData, mainLight, viewDirectionWS, positionWS, shadowPositionWS, screenUV);
}

BurtPBRShadingComponents BurtEvaluateShadingModelComponentsFromGBuffer(BurtEncodedGBuffer encodedGBuffer, BurtLight mainLight, float3 viewDirectionWS)
{
    BurtGBufferData gbufferData = BurtDecodeGBuffer(encodedGBuffer);
    return BurtEvaluateShadingModelComponentsFromGBuffer(gbufferData, mainLight, viewDirectionWS);
}

BurtPBRShadingComponents BurtEvaluateShadingModelComponentsFromGBuffer(BurtEncodedGBuffer encodedGBuffer, BurtLight mainLight, float3 viewDirectionWS, float3 positionWS)
{
    BurtGBufferData gbufferData = BurtDecodeGBuffer(encodedGBuffer);
    return BurtEvaluateShadingModelComponentsFromGBuffer(gbufferData, mainLight, viewDirectionWS, positionWS);
}

BurtPBRShadingComponents BurtEvaluateShadingModelComponentsFromGBuffer(BurtEncodedGBuffer encodedGBuffer, BurtLight mainLight, float3 viewDirectionWS, float3 positionWS, float2 screenUV)
{
    BurtGBufferData gbufferData = BurtDecodeGBuffer(encodedGBuffer);
    return BurtEvaluateShadingModelComponentsFromGBuffer(gbufferData, mainLight, viewDirectionWS, positionWS, screenUV);
}
#endif

// 计算 Blinn-Phong 高光项，用来给旧 Simple Lit 路径保留第一�?specular
float3 BurtEvaluateSpecular(BurtSurfaceData surfaceData, BurtLight light, float3 normalWS, float3 viewDirectionWS)
{
    // 计算 N dot L，保证背光面不会产生不合理的高光
float diffuseVisibility = BurtLambert(normalWS, light.directionWS);

    // 把光线方向和视线方向相加并归一化，得到 Blinn-Phong 使用的半角向量
float3 halfDirectionWS = BurtSafeNormalize(light.directionWS + viewDirectionWS);

    // 计算法线和半角向量的夹角，数值越接近 1 表示越接近镜面反射方向
float specularNdotH = saturate(dot(normalWS, halfDirectionWS));

    // �?0 �?1 �?smoothness 映射到高光指数，smoothness 越高高光越集中
float specularPower = lerp(8.0f, 256.0f, surfaceData.smoothness);

    // �?pow 计算 Blinn-Phong 高光强度
float specularTerm = pow(specularNdotH, specularPower);

    // 把内�?F0、灯光颜色、受光可见性和阴影衰减相乘得到最终高光
return DielectricReflectanceToF0(surfaceData.baseColor.rgb, surfaceData.reflectance, surfaceData.metallic) * light.color * specularTerm * diffuseVisibility * light.shadowAttenuation;
}

float3 BurtEvaluateAdditionalDiffuseLights(BurtSurfaceData surfaceData, float3 normalWS, float3 positionWS)
{
    float3 diffuseLighting = float3(0.0f, 0.0f, 0.0f);
    int additionalLightCount = BurtGetAdditionalLightCount();

    [loop]
    for (int lightIndex = 0; lightIndex < BURT_MAX_ADDITIONAL_LIGHTS; lightIndex++)
    {
        if (lightIndex >= additionalLightCount)
        {
            break;
        }

        diffuseLighting += BurtEvaluateDiffuse(surfaceData.baseColor.rgb, BurtCreateAdditionalLight(lightIndex, positionWS, normalWS), normalWS);
    }

    return diffuseLighting;
}

float3 BurtEvaluateAdditionalSpecularLights(BurtSurfaceData surfaceData, float3 normalWS, float3 viewDirectionWS, float3 positionWS)
{
    float3 specularLighting = float3(0.0f, 0.0f, 0.0f);
    int additionalLightCount = BurtGetAdditionalLightCount();

    [loop]
    for (int lightIndex = 0; lightIndex < BURT_MAX_ADDITIONAL_LIGHTS; lightIndex++)
    {
        if (lightIndex >= additionalLightCount)
        {
            break;
        }

        specularLighting += BurtEvaluateSpecular(surfaceData, BurtCreateAdditionalLight(lightIndex, positionWS, normalWS), normalWS, viewDirectionWS);
    }

    return specularLighting;
}

// 计算 BurtRP 当前的完整简�?Lit 模型：环境光 + 一个带阴影�?Lambert 主光
float3 BurtEvaluateSimpleLit(BurtSurfaceData surfaceData, BurtLight mainLight, float3 normalWS)
{
    // 使用材质基础色和全局环境光计�?ambient 部分
float3 ambientIrradiance = BurtSampleIndirectDiffuseIrradiance(normalWS);
    float3 ambientColor = BurtEvaluateAmbient(surfaceData.baseColor.rgb, ambientIrradiance);

    // 使用材质基础色、主光数据和法线计算 direct diffuse 部分
float3 diffuseColor = BurtEvaluateDiffuse(surfaceData.baseColor.rgb, mainLight, normalWS);

    // 返回环境光和直接光相加后的最�?RGB
return ambientColor + diffuseColor;
}

float3 BurtEvaluateSimpleLit(BurtSurfaceData surfaceData, BurtLight mainLight, float3 normalWS, float3 positionWS)
{
    float3 lighting = BurtEvaluateSimpleLit(surfaceData, mainLight, normalWS);
    lighting += BurtEvaluateAdditionalDiffuseLights(surfaceData, normalWS, positionWS);
    return lighting;
}

// 计算带高光的�?Simple Lit 模型：环境光 + 漫反射主�?+ Blinn-Phong 高光
float3 BurtEvaluateSimpleLitSpecular(BurtSurfaceData surfaceData, BurtLight mainLight, float3 normalWS, float3 viewDirectionWS)
{
    // 先复用已有简�?Lit，得到环境光和漫反射直接光
float3 baseLighting = BurtEvaluateSimpleLit(surfaceData, mainLight, normalWS);

    // 再额外计算主光高光项
float3 specularLighting = BurtEvaluateSpecular(surfaceData, mainLight, normalWS, viewDirectionWS);

    // 返回基础光照和高光相加后的结果
return baseLighting + specularLighting;
}

float3 BurtEvaluateSimpleLitSpecular(BurtSurfaceData surfaceData, BurtLight mainLight, float3 normalWS, float3 viewDirectionWS, float3 positionWS)
{
    float3 lighting = BurtEvaluateSimpleLitSpecular(surfaceData, mainLight, normalWS, viewDirectionWS);
    lighting += BurtEvaluateAdditionalDiffuseLights(surfaceData, normalWS, positionWS);
    lighting += BurtEvaluateAdditionalSpecularLights(surfaceData, normalWS, viewDirectionWS, positionWS);
    return lighting;
}

// 计算单主�?PBR 光照：PBR 间接�?+ Cook-Torrance 直接光
float3 BurtEvaluateSimpleLitPBR(BurtSurfaceData surfaceData, BurtLight mainLight, float3 normalWS, float3 viewDirectionWS)
{
    // 复用统一 PBR shading 入口，避�?Forward 和未�?Deferred 维护两套组合逻辑�?
    BurtPBRShadingComponents components = BurtEvaluatePBRShadingComponents(surfaceData, mainLight, normalWS, viewDirectionWS);

    // 返回不含自发光的完整 PBR 光照
return components.lighting;
}

float3 BurtEvaluateSimpleLitPBR(BurtSurfaceData surfaceData, BurtLight mainLight, float3 normalWS, float3 viewDirectionWS, float3 positionWS)
{
    BurtPBRShadingComponents components = BurtEvaluatePBRShadingComponents(surfaceData, mainLight, normalWS, viewDirectionWS, positionWS);
    return components.lighting;
}

#endif // BURT_LIGHTING_INCLUDED // 结束 BurtLighting.hlsl �?include guard�?
