#ifndef BURT_GI_HASH_SURFACE_TABLE_INCLUDED
#define BURT_GI_HASH_SURFACE_TABLE_INCLUDED

// Two uint4 cells per sparse surface entry, sharing the existing value budget:
// even: spatial tile hash, local-cell/surface signature, packed normal, plane
// odd: packed radiance.xy, last-update frame, confidence.
// Clear/purge runs before publication. Query/resolve runs in a later dispatch.
// The two key words are immutable between clear/purge operations. No spinlock
// or assumption about simultaneous thread progress is required.
#ifndef BURT_GI_SURFACE_CACHE_EXTERNAL_RESOURCE
#if defined(BURT_GI_SURFACE_CACHE_READ_ONLY)
StructuredBuffer<uint4> _BurtGISurfaceCacheTable;
#else
RWStructuredBuffer<uint4> _BurtGISurfaceCacheTable;
#endif
uint _BurtGISurfaceCacheSlotCount;
uint _BurtGISurfaceCacheProbeCount;
#endif

float3 BurtGISurfaceCacheNormal(uint packed)
{
    if ((packed & 0x40000000u) == 0u) return 0.0;
    float2 oct = float2(packed & 32767u, (packed >> 15u) & 32767u) * (2.0 / 32767.0) - 1.0;
    float3 n = float3(oct, 1.0 - abs(oct.x) - abs(oct.y));
    float t = saturate(-n.z);
    n.xy += float2(n.x >= 0.0 ? -t : t, n.y >= 0.0 ? -t : t);
    return normalize(n);
}

uint BurtGISurfaceCacheSignature(uint packedNormal, float3 position, float cellSize, bool screenSource)
{
    if ((packedNormal & 0x40000000u) == 0u) return 0u;
    float2 oct = float2(packedNormal & 32767u, (packedNormal >> 15u) & 32767u) / 32767.0;
    uint2 q = (uint2)floor(oct * 4.0 + 0.5);
    float3 n = abs(BurtGISurfaceCacheNormal(packedNormal));
    uint axis = n.x >= n.y && n.x >= n.z ? 0u : (n.y >= n.z ? 1u : 2u);
    float coord = position[axis] / max(cellSize, 0.001);
    uint plane = (uint)floor(frac(coord) * 4.0 + 0.5);
    return 1u + q.x + q.y * 5u + 25u * (plane + 5u * axis + (screenSource ? 15u : 0u));
}

uint2 BurtGISurfaceCacheKey(uint spatialHash, uint localCell, uint signature)
{
    return uint2(spatialHash, signature == 0u ? 0u : ((signature << 6u) | (localCell & 63u)));
}

uint BurtGISurfaceCacheHash(uint x)
{
    x ^= x >> 16u; x *= 0x7feb352du;
    x ^= x >> 15u; x *= 0x846ca68bu;
    return x ^ (x >> 16u);
}

uint BurtGISurfaceCacheStart(uint spatialHash, uint localCell)
{
    // All surfaces of a spatial cell share a search window. A fallback ray can
    // enumerate them without knowing the normal of a not-yet-found surface.
    return BurtGISurfaceCacheHash(spatialHash ^ (localCell * 0x9e3779b9u)) % max(1u, _BurtGISurfaceCacheSlotCount);
}

uint BurtGISurfaceCacheSlot(uint start, uint offset)
{
    return (start + offset) % max(1u, _BurtGISurfaceCacheSlotCount);
}

bool BurtGISurfaceCacheFind(uint2 key, out uint slot)
{
    slot = 0xffffffffu;
    if (key.x == 0u || key.y == 0u) return false;
    uint start = BurtGISurfaceCacheStart(key.x, key.y & 63u);
    uint count = min(_BurtGISurfaceCacheSlotCount, _BurtGISurfaceCacheProbeCount);
    [loop] for (uint i = 0u; i < count; ++i)
    {
        uint candidate = BurtGISurfaceCacheSlot(start, i);
        uint2 stored = _BurtGISurfaceCacheTable[candidate * 2u].xy;
        if (all(stored == key)) { slot = candidate; return true; }
    }
    return false;
}

#if !defined(BURT_GI_SURFACE_CACHE_READ_ONLY)
bool BurtGISurfaceCacheFindOrInsert(uint2 key, out uint slot, out bool isNew)
{
    isNew = false;
    if (BurtGISurfaceCacheFind(key, slot)) return true;
    if (key.x == 0u || key.y == 0u) return false;
    uint start = BurtGISurfaceCacheStart(key.x, key.y & 63u);
    uint count = min(_BurtGISurfaceCacheSlotCount, _BurtGISurfaceCacheProbeCount);
    // Full search above is required: a purged hole must not duplicate an entry
    // already present later in this same bounded window.
    [loop] for (uint i = 0u; i < count; ++i)
    {
        uint candidate = BurtGISurfaceCacheSlot(start, i);
        uint oldSpatial;
        InterlockedCompareExchange(_BurtGISurfaceCacheTable[candidate * 2u].x, 0u, key.x, oldSpatial);
        if (oldSpatial != 0u && oldSpatial != key.x) continue;
        uint oldSurface;
        InterlockedCompareExchange(_BurtGISurfaceCacheTable[candidate * 2u].y, 0u, key.y, oldSurface);
        if (oldSurface == 0u || oldSurface == key.y)
        {
            slot = candidate;
            isNew = oldSurface == 0u;
            return true;
        }
    }
    slot = 0xffffffffu;
    return false;
}
#endif
#endif
