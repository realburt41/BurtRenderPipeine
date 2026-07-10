#ifndef BURT_GI_RAY_TRACING_SHARED_INCLUDED
#define BURT_GI_RAY_TRACING_SHARED_INCLUDED

struct BurtGIRayPayload
{
    float3 Radiance;
    float HitDistance;
    float3 Albedo;
    float Hit;
    float3 Emission;
    float Padding;
    float3 NormalWS;
};

struct BurtGIRayAttributeData
{
    float2 barycentrics;
};

#endif
