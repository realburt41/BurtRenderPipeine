#ifndef BURT_GI_SCREEN_PROBE_TRACE_SURFACE_INCLUDED
#define BURT_GI_SCREEN_PROBE_TRACE_SURFACE_INCLUDED

// Four independent records per atlas texel: Screen, then three Voxel lanes.
// Do not assign the combined directional radiance to a fabricated hit point.
// Kind: 0 unknown, 1 opaque geometry without lighting, 2 lit/valid-black
// opaque sample, 3 completed empty segment, 4 dense/partial aggregate.
// Empty segments store their end position, NOT a surface eligible for caching.
struct BurtGITraceSurface
{
    float3 positionWS;
    uint kind;
    float3 radiance;
    float confidence;
    float3 directionWS;
    float distanceFromOrigin;
    // Radiance stays in the hit's domain. Its receiver contribution is kept
    // separately so filtering a hit never allocates the whole cone to it.
    float3 filteredRadiance;
    float contribution;
    // Actual hit shading normal, not the receiver or its nearest screen probe.
    // Octahedral 2x15-bit + validity bit. Zero means unavailable (not +Y).
    uint packedNormal;
};
RWStructuredBuffer<BurtGITraceSurface> _BurtGIScreenProbeTraceSurfaceBuffer;
uint _BurtGIScreenProbeTraceSurfaceEnabled;

uint BurtGIPackTraceSurfaceNormal(float3 n)
{
    if (!all(isfinite(n)) || dot(n, n) < 1.0e-8) return 0u;
    n /= abs(n.x) + abs(n.y) + abs(n.z);
    float2 oct = n.xy;
    if (n.z < 0.0)
        oct = (1.0 - abs(oct.yx)) * float2(oct.x >= 0.0 ? 1.0 : -1.0, oct.y >= 0.0 ? 1.0 : -1.0);
    uint2 q = (uint2)round(saturate(oct * 0.5 + 0.5) * 32767.0);
    return q.x | (q.y << 15u) | 0x40000000u;
}

float3 BurtGIUnpackTraceSurfaceNormal(uint packed)
{
    if ((packed & 0x40000000u) == 0u) return 0.0;
    float2 oct = float2(packed & 32767u, (packed >> 15u) & 32767u) * (2.0 / 32767.0) - 1.0;
    float3 n = float3(oct, 1.0 - abs(oct.x) - abs(oct.y));
    float t = saturate(-n.z);
    n.xy += float2(n.x >= 0.0 ? -t : t, n.y >= 0.0 ? -t : t);
    return normalize(n);
}

void BurtGISetTraceSurfaceNormal(uint recordIndex, float3 normalWS)
{
    if (_BurtGIScreenProbeTraceSurfaceEnabled == 0u) return;
    BurtGITraceSurface s = _BurtGIScreenProbeTraceSurfaceBuffer[recordIndex];
    s.packedNormal = (s.kind == 1u || s.kind == 2u) ? BurtGIPackTraceSurfaceNormal(normalWS) : 0u;
    _BurtGIScreenProbeTraceSurfaceBuffer[recordIndex] = s;
}

void BurtGIWriteTraceSurface(uint recordIndex, float3 positionWS, uint kind,
    float3 radiance, float confidence, float3 directionWS, float distanceFromOrigin)
{
    if (_BurtGIScreenProbeTraceSurfaceEnabled == 0u) return;
    BurtGITraceSurface s;
    s.positionWS = positionWS;
    s.kind = kind;
    s.radiance = radiance;
    s.confidence = confidence;
    s.directionWS = directionWS;
    s.distanceFromOrigin = distanceFromOrigin;
    s.filteredRadiance = radiance;
    s.contribution = 0.0;
    s.packedNormal = 0u;
    _BurtGIScreenProbeTraceSurfaceBuffer[recordIndex] = s;
}

void BurtGISetTraceSurfaceContribution(uint recordIndex, float contribution)
{
    if (_BurtGIScreenProbeTraceSurfaceEnabled == 0u) return;
    BurtGITraceSurface s = _BurtGIScreenProbeTraceSurfaceBuffer[recordIndex];
    s.contribution = s.kind == 2u ? contribution : 0.0;
    _BurtGIScreenProbeTraceSurfaceBuffer[recordIndex] = s;
}

void BurtGIScaleTraceSurfaceContributions(uint texelIndex, uint firstSlot, float scale)
{
    if (_BurtGIScreenProbeTraceSurfaceEnabled == 0u) return;
    [unroll] for (uint slot = firstSlot; slot < 4u; ++slot)
    {
        uint index = texelIndex * 4u + slot;
        BurtGITraceSurface s = _BurtGIScreenProbeTraceSurfaceBuffer[index];
        s.contribution *= scale;
        _BurtGIScreenProbeTraceSurfaceBuffer[index] = s;
    }
}

void BurtGIClearTraceSurfaces(uint texelIndex)
{
    if (_BurtGIScreenProbeTraceSurfaceEnabled == 0u) return;
    [unroll] for (uint slot = 0u; slot < 4u; ++slot)
        BurtGIWriteTraceSurface(texelIndex * 4u + slot, 0.0, 0u, 0.0, 0.0, 0.0, 0.0);
}
#endif
