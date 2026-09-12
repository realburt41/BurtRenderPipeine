#ifndef BURT_GI_RADIANCE_CACHE_HASH_GRID_PACKING_INCLUDED
#define BURT_GI_RADIANCE_CACHE_HASH_GRID_PACKING_INCLUDED

// Persistent values use floating-point precision near black, as in XRender.
// UNorm16 over [0,8] can repeatedly round a decaying history value back to
// 8/65535 even when fresh observations are black. Atomic accumulation is a
// separate weighted fixed-point representation and is deliberately unchanged.
// Preserve BRP's existing radiance/confidence ranges and 2-uint lane order.
uint2 BurtGIHashGridPackPersistentRadiance(float3 radiance, float confidence)
{
    uint4 bits = f32tof16(float4(clamp(radiance, 0.0, 8.0), saturate(confidence)));
    return uint2(bits.x | (bits.y << 16), bits.z | (bits.w << 16));
}

float4 BurtGIHashGridUnpackPersistentRadiance(uint2 packed)
{
    return f16tof32(uint4(packed.x & 0xffffu, packed.x >> 16,
                         packed.y & 0xffffu, packed.y >> 16));
}

#endif
