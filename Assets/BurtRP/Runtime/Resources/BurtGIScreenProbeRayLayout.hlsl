#ifndef BURT_GI_SCREEN_PROBE_RAY_LAYOUT_INCLUDED
#define BURT_GI_SCREEN_PROBE_RAY_LAYOUT_INCLUDED
#include "BurtGIScreenProbeRaySampling.hlsl"

// Ray identity is shared by trace consumers and cache publishers. An adaptive
// atlas slot is storage, not a uniform placement or a stable random seed.
void BurtGIResolveScreenProbeRayLayout(
    uint2 traceTexelCoord, uint traceResolution, bool isAdaptiveProbe,
    uint packedRayInfo, uint2 uniformProbeCoord, uint2 adaptiveSourcePixel,
    uint frameIndex, out uint2 sourceTraceTexelCoord, out uint sourceTraceLevel,
    out uint sourceMipSize, out float2 octUV)
{
    traceResolution = max(1u, traceResolution);
    sourceTraceTexelCoord = traceTexelCoord;
    sourceTraceLevel = 0u;
    if (!isAdaptiveProbe)
    {
        sourceTraceTexelCoord = uint2(packedRayInfo & 0x3fu, (packedRayInfo >> 6u) & 0x3fu);
        sourceTraceLevel = (packedRayInfo >> 12u) & 0xfu;
    }
    uint maxImportanceTraceResolution = min(64u, max(traceResolution, traceResolution << 1u));
    sourceMipSize = isAdaptiveProbe ? traceResolution
        : max(1u, maxImportanceTraceResolution >> min(sourceTraceLevel, 5u));
    if (sourceTraceTexelCoord.x >= sourceMipSize || sourceTraceTexelCoord.y >= sourceMipSize)
    {
        sourceTraceTexelCoord = traceTexelCoord;
        sourceTraceLevel = 0u;
        sourceMipSize = traceResolution;
    }
    uint2 seedCoord = isAdaptiveProbe
        ? (adaptiveSourcePixel ^ uint2(0x80000000u, 0u)) : uniformProbeCoord;
    octUV = (float2(sourceTraceTexelCoord) + BurtGIScreenProbeRayTexelCenter(seedCoord, frameIndex)) / (float)sourceMipSize;
}
#endif
