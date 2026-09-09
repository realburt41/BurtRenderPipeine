#ifndef BURT_GI_SCREEN_PROBE_ADAPTIVE_DATA_INCLUDED
#define BURT_GI_SCREEN_PROBE_ADAPTIVE_DATA_INCLUDED

// 48-byte structured-buffer contract. Keep the C# allocation stride in sync.
// Placement samples a single camera depth/GBuffer pixel; consumers must not
// rebuild the surface by interpolating neighbouring uniform probe positions.
struct BurtGIAdaptiveProbeData
{
    uint packedCoord;
    uint placementPayload;
    uint2 sourcePixel;
    float3 positionWS;
    float rawDepth;
    float3 normalWS;
    float valid;
};

#endif
