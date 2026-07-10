#ifndef BURT_GI_RAY_TRACING_LIT_INCLUDED
#define BURT_GI_RAY_TRACING_LIT_INCLUDED

#include "UnityRayTracingMeshUtils.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtGIRayTracingShared.hlsl"

Texture2D _BaseMap;
SamplerState sampler_BaseMap;
Texture2D _EmissionMap;
SamplerState sampler_EmissionMap;

float3 BurtGIInterpolateNormalOS(BurtGIRayAttributeData attributeData)
{
    uint3 triangleIndices = UnityRayTracingFetchTriangleIndices(PrimitiveIndex());
    float3 barycentric = float3(1.0 - attributeData.barycentrics.x - attributeData.barycentrics.y, attributeData.barycentrics.x, attributeData.barycentrics.y);
    float3 normal0 = UnityRayTracingFetchVertexAttribute3(triangleIndices.x, kVertexAttributeNormal);
    float3 normal1 = UnityRayTracingFetchVertexAttribute3(triangleIndices.y, kVertexAttributeNormal);
    float3 normal2 = UnityRayTracingFetchVertexAttribute3(triangleIndices.z, kVertexAttributeNormal);
    return normalize(normal0 * barycentric.x + normal1 * barycentric.y + normal2 * barycentric.z);
}

float2 BurtGIInterpolateUV0(BurtGIRayAttributeData attributeData)
{
    uint3 triangleIndices = UnityRayTracingFetchTriangleIndices(PrimitiveIndex());
    float3 barycentric = float3(1.0 - attributeData.barycentrics.x - attributeData.barycentrics.y, attributeData.barycentrics.x, attributeData.barycentrics.y);
    float2 uv0 = UnityRayTracingFetchVertexAttribute2(triangleIndices.x, kVertexAttributeTexCoord0);
    float2 uv1 = UnityRayTracingFetchVertexAttribute2(triangleIndices.y, kVertexAttributeTexCoord0);
    float2 uv2 = UnityRayTracingFetchVertexAttribute2(triangleIndices.z, kVertexAttributeTexCoord0);
    return uv0 * barycentric.x + uv1 * barycentric.y + uv2 * barycentric.z;
}

float4 BurtGISampleRayTracingBaseColor(float2 uv)
{
    float2 transformedUV = uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
    return _BaseMap.SampleLevel(sampler_BaseMap, transformedUV, 0.0) * _BaseColor;
}

float3 BurtGISampleRayTracingEmission(float2 uv)
{
    float2 transformedUV = uv * _EmissionMap_ST.xy + _EmissionMap_ST.zw;
    return _EmissionMap.SampleLevel(sampler_EmissionMap, transformedUV, 0.0).rgb * _EmissionColor.rgb;
}

[shader("closesthit")]
void BurtGIClosestHit(inout BurtGIRayPayload payload : SV_RayPayload, BurtGIRayAttributeData attributeData : SV_IntersectionAttributes)
{
    float2 uv = BurtGIInterpolateUV0(attributeData);
    float4 baseColor = BurtGISampleRayTracingBaseColor(uv);
    float3 normalWS = normalize(UnityObjectToWorldNormal(BurtGIInterpolateNormalOS(attributeData)));
    payload.Radiance = 0.0;
    payload.HitDistance = RayTCurrent();
    payload.Albedo = max(baseColor.rgb, 0.0);
    payload.Emission = max(BurtGISampleRayTracingEmission(uv), 0.0);
    payload.NormalWS = normalWS;
    payload.Hit = 1.0;
}

[shader("anyhit")]
void BurtGIAnyHit(inout BurtGIRayPayload payload : SV_RayPayload, BurtGIRayAttributeData attributeData : SV_IntersectionAttributes)
{
#if defined(BURT_ALPHA_CLIP)
    if (BurtGISampleRayTracingBaseColor(BurtGIInterpolateUV0(attributeData)).a < _Cutoff)
    {
        IgnoreHit();
    }
#endif
}

#endif
