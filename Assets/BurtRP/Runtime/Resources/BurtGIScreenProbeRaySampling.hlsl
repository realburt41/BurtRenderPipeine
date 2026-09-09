#ifndef BURT_GI_SCREEN_PROBE_RAY_SAMPLING_INCLUDED
#define BURT_GI_SCREEN_PROBE_RAY_SAMPLING_INCLUDED

// A shared sub-texel offset for every stage of a probe's ray chain and its
// composite/rebin step. FixedJitterIndex is resolved by the caller's frame upload.
// Spatial scrambling plus an R2 sequence avoids repeatedly sampling the same
// 8x8 directions while a small emitter moves between their angular footprints.
float2 BurtGIScreenProbeRayTexelCenter(uint2 stableProbeCoord, uint frameIndex)
{
    uint seed = stableProbeCoord.x * 1664525u + stableProbeCoord.y * 1013904223u + 374761393u;
    seed = (seed ^ (seed >> 16u)) * 2246822519u;
    seed = (seed ^ (seed >> 13u)) * 3266489917u;
    seed ^= seed >> 16u;
    float2 scramble = float2(seed & 65535u, seed >> 16u) * (1.0 / 65536.0);
    return frac(scramble + 0.5 + float2(0.754877666, 0.569840291) * (float)(frameIndex & 1023u));
}
#endif
