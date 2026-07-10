#ifndef BURT_GI_RAY_TRACING_FUR_INCLUDED
#define BURT_GI_RAY_TRACING_FUR_INCLUDED

#include "UnityRayTracingMeshUtils.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtGIRayTracingShared.hlsl"

Texture2D _BaseMap;
SamplerState sampler_BaseMap;
Texture2D _EmissiveMap;
SamplerState sampler_EmissiveMap;

float3 BurtGIFurInterpolateNormalOS(BurtGIRayAttributeData attributeData)
{
    uint3 triangleIndices = UnityRayTracingFetchTriangleIndices(PrimitiveIndex());
    float3 barycentric = float3(1.0 - attributeData.barycentrics.x - attributeData.barycentrics.y, attributeData.barycentrics.x, attributeData.barycentrics.y);
    float3 normal0 = UnityRayTracingFetchVertexAttribute3(triangleIndices.x, kVertexAttributeNormal);
    float3 normal1 = UnityRayTracingFetchVertexAttribute3(triangleIndices.y, kVertexAttributeNormal);
    float3 normal2 = UnityRayTracingFetchVertexAttribute3(triangleIndices.z, kVertexAttributeNormal);
    return normalize(normal0 * barycentric.x + normal1 * barycentric.y + normal2 * barycentric.z);
}

float2 BurtGIFurInterpolateUV0(BurtGIRayAttributeData attributeData)
{
    uint3 triangleIndices = UnityRayTracingFetchTriangleIndices(PrimitiveIndex());
    float3 barycentric = float3(1.0 - attributeData.barycentrics.x - attributeData.barycentrics.y, attributeData.barycentrics.x, attributeData.barycentrics.y);
    float2 uv0 = UnityRayTracingFetchVertexAttribute2(triangleIndices.x, kVertexAttributeTexCoord0);
    float2 uv1 = UnityRayTracingFetchVertexAttribute2(triangleIndices.y, kVertexAttributeTexCoord0);
    float2 uv2 = UnityRayTracingFetchVertexAttribute2(triangleIndices.z, kVertexAttributeTexCoord0);
    return uv0 * barycentric.x + uv1 * barycentric.y + uv2 * barycentric.z;
}

float4 BurtGIFurSampleBaseColor(float2 uv)
{
    return _BaseMap.SampleLevel(sampler_BaseMap, uv * _BaseMap_ST.xy + _BaseMap_ST.zw, 0.0) * _BaseColor;
}

float3 BurtGIFurSampleEmission(float2 uv)
{
    return _EmissiveMap.SampleLevel(sampler_EmissiveMap, uv * _EmissiveMap_ST.xy + _EmissiveMap_ST.zw, 0.0).rgb * _EmissiveColor.rgb;
}

[shader("closesthit")]
void BurtGIFurClosestHit(inout BurtGIRayPayload payload : SV_RayPayload, BurtGIRayAttributeData attributeData : SV_IntersectionAttributes)
{
    float2 uv = BurtGIFurInterpolateUV0(attributeData);
    float4 baseColor = BurtGIFurSampleBaseColor(uv);
    payload.Radiance = 0.0;
    payload.HitDistance = RayTCurrent();
    payload.Albedo = max(baseColor.rgb, 0.0);
    payload.Emission = max(BurtGIFurSampleEmission(uv), 0.0);
    payload.NormalWS = normalize(UnityObjectToWorldNormal(BurtGIFurInterpolateNormalOS(attributeData)));
    payload.Hit = 1.0;
}

[shader("anyhit")]
void BurtGIFurAnyHit(inout BurtGIRayPayload payload : SV_RayPayload, BurtGIRayAttributeData attributeData : SV_IntersectionAttributes)
{
#if defined(BURT_ALPHA_CLIP)
    if (BurtGIFurSampleBaseColor(BurtGIFurInterpolateUV0(attributeData)).a < _Cutoff)
    {
        IgnoreHit();
    }
#endif
}

#endif
