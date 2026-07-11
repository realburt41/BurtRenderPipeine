// BurtRP 的通用 HLSL 工具库，所有 BurtRP shader 都可以安全包含这个文件。
#ifndef BURT_COMMON_INCLUDED // 开始 include guard，防止同一个 shader 编译单元里重复定义这些工具函数。
#define BURT_COMMON_INCLUDED // 标记 BurtCommon.hlsl 已经被包含过，后续重复 include 会被跳过。

// UnityCG does not consistently expose these helpers in every HLSLPROGRAM path, so keep BurtRP shaders self-contained.
#ifndef CBUFFER_START
    #define CBUFFER_START(name) cbuffer name {
#endif

#ifndef CBUFFER_END
    #define CBUFFER_END };
#endif

#ifndef UNITY_DECLARE_TEXCUBE
    #define UNITY_DECLARE_TEXCUBE(tex) samplerCUBE tex
#endif

#ifndef UNITY_SAMPLE_TEXCUBE_LOD
    #define UNITY_SAMPLE_TEXCUBE_LOD(tex, coord, lod) texCUBElod(tex, float4(coord, lod))
#endif

SamplerState sampler_PointClamp;
SamplerState sampler_LinearClamp;
SamplerState sampler_PointRepeat;
SamplerState sampler_LinearRepeat;
SamplerState sampler_TriLinearClamp;
SamplerState sampler_TriLinearRepeat;
SamplerComparisonState sampler_PointClampCompare;
SamplerComparisonState sampler_LinearClampCompare;

#define SamplerPointClamp sampler_PointClamp
#define SamplerLinearClamp sampler_LinearClamp
#define SamplerPointRepeat sampler_PointRepeat
#define SamplerLinearRepeat sampler_LinearRepeat
#define SamplerTriLinearClamp sampler_TriLinearClamp
#define SamplerTriLinearRepeat sampler_TriLinearRepeat
#define SamplerPointClampCompare sampler_PointClampCompare
#define SamplerLinearClampCompare sampler_LinearClampCompare

#ifndef BURT_PI
#define BURT_PI (3.14159265359f)
#endif

#ifndef BURT_INV_PI
#define BURT_INV_PI (0.31830988618f)
#endif

#ifndef SAMPLE_TEXTURE2D
    #define SAMPLE_TEXTURE2D(textureName, samplerName, coord2) textureName.Sample(samplerName, coord2)
#endif

#ifndef SAMPLE_TEXTURE2D_LOD
    #define SAMPLE_TEXTURE2D_LOD(textureName, samplerName, coord2, lod) textureName.SampleLevel(samplerName, coord2, lod)
#endif

#ifndef SAMPLE_TEXTURE2D_ARRAY_LOD
    #define SAMPLE_TEXTURE2D_ARRAY_LOD(textureName, samplerName, coord2, index, lod) textureName.SampleLevel(samplerName, float3(coord2, index), lod)
#endif

#define BURT_SAMPLE_TEXTURE2D_REPEAT(textureName, uv) textureName.Sample(sampler_LinearRepeat, uv)
#define BURT_SAMPLE_TEXTURE2D_CLAMP(textureName, uv) textureName.Sample(sampler_LinearClamp, uv)
#define BURT_SAMPLE_TEXTURE2D_POINT_CLAMP(textureName, uv) textureName.Sample(sampler_PointClamp, uv)
#define BURT_SAMPLE_TEXTURE2D_LOD_CLAMP(textureName, uv, lod) textureName.SampleLevel(sampler_LinearClamp, uv, lod)
#define BURT_SAMPLE_TEXTURE2D_LOD_POINT_CLAMP(textureName, uv, lod) textureName.SampleLevel(sampler_PointClamp, uv, lod)
#define BURT_SAMPLE_TEXTURE2D_ARRAY_LOD_CLAMP(textureName, uv, slice, lod) textureName.SampleLevel(sampler_LinearClamp, float3(uv, slice), lod)
#define BURT_SAMPLE_TEXTURE2D_ARRAY_LOD_REPEAT(textureName, uv, slice, lod) textureName.SampleLevel(sampler_LinearRepeat, float3(uv, slice), lod)
#define BURT_SAMPLE_TEXTURECUBE_LOD_CLAMP(textureName, direction, lod) textureName.SampleLevel(sampler_LinearClamp, direction, lod)
#define BURT_SAMPLE_TEXTURECUBE_LOD_REPEAT(textureName, direction, lod) textureName.SampleLevel(sampler_LinearRepeat, direction, lod)
#define BURT_SAMPLE_SHADOW_CLAMP(textureName, coord) textureName.SampleCmpLevelZero(sampler_LinearClampCompare, (coord).xy, (coord).z)

#define BURT_MULTIPASS_DEFAULT_SHELL_LENGTH (0.03f)
float4 _FurScale;
float _FurMaxCount;

uint BurtGetCurrentInstanceID()
{
#if defined(UNITY_INSTANCING_ENABLED) || defined(UNITY_PROCEDURAL_INSTANCING_ENABLED) || defined(UNITY_STEREO_INSTANCING_ENABLED)
    return unity_InstanceID;
#else
    return 0u;
#endif
}

float BurtGetMultipassLayerFactor()
{
    float layerCount = max(_FurMaxCount, 1.0f);
    return saturate(BurtGetCurrentInstanceID() / max(layerCount - 1.0f, 1.0f));
}

float4 BurtApplyMultipassObjectShellOffset(float4 positionOS, float3 normalOS)
{
#if defined(UNITY_INSTANCING_ENABLED) || defined(UNITY_PROCEDURAL_INSTANCING_ENABLED) || defined(UNITY_STEREO_INSTANCING_ENABLED)
    if (_FurMaxCount <= 1.0f)
    {
        return positionOS;
    }

    float layerFactor = BurtGetMultipassLayerFactor();
    float3 safeScale = max(abs(_FurScale.xyz), float3(0.0001f, 0.0001f, 0.0001f));
    positionOS.xyz += normalOS / safeScale * (layerFactor * BURT_MULTIPASS_DEFAULT_SHELL_LENGTH);
#endif
    return positionOS;
}

// 定义一个很小的正数，用来避免除以 0 或 rsqrt 输入 0 导致 NaN。
#define BURT_EPSILON (0.000001f)

// Position data shared by BurtRP passes.
struct BurtPositionInputs
{
    // 保存世界空间位置，后续光照、阴影、雾效和调试视图都会用到。
    float3 PositionWS;

    // 保存裁剪空间位置，最终会写入 SV_POSITION 让 GPU 做光栅化。
    float4 PositionCS;
};

// 保存 BurtRP 光照当前使用的法线数据。
struct BurtNormalInputs
{
    // 保存世界空间法线，Lambert 光照、法线贴图和阴影 bias 计算都会用到。
    float3 NormalWS;
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
