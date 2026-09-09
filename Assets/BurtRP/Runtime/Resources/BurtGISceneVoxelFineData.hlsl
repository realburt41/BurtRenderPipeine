#ifndef BURT_GI_SCENE_VOXEL_FINE_DATA_INCLUDED
#define BURT_GI_SCENE_VOXEL_FINE_DATA_INCLUDED

#define BURT_GI_FINE_INVALID_OFFSET 0xffffffffu

// Structured-buffer stride: 24 bytes. Alpha in EACH half4 carries validity;
// occupied black is (0,0,0,1), never an invalid/empty material.
struct BurtGISceneVoxelFineMaterialData
{
    uint2 albedoAndValid;
    uint2 normalAndValid;
    uint2 emissionAndValid;
};

// Structured-buffer stride: 16 bytes. Radiance and geometry validity remain
// independent: zero radiance at a valid opaque surface is still a blocker.
struct BurtGISceneVoxelFineLightingData
{
    uint2 radianceAndValid;
    uint2 normalAndValid;
};

uint2 BurtGIFinePackHalf4(float4 value)
{
    uint4 halfBits = f32tof16(value) & 0xffffu;
    return uint2(halfBits.x | (halfBits.y << 16u),
                 halfBits.z | (halfBits.w << 16u));
}

float4 BurtGIFineUnpackHalf4(uint2 packed)
{
    return f16tof32(uint4(packed.x & 0xffffu, packed.x >> 16u,
                          packed.y & 0xffffu, packed.y >> 16u));
}

// Inputs already use the native voxel producer's albedo boost and normal
// encoding [0,1]. Do not apply either transformation a second time here.
BurtGISceneVoxelFineMaterialData BurtGIFinePackMaterial(
    float3 boostedAlbedo, float3 encodedNormal, float3 emission)
{
    BurtGISceneVoxelFineMaterialData result;
    result.albedoAndValid = BurtGIFinePackHalf4(float4(boostedAlbedo, 1.0));
    result.normalAndValid = BurtGIFinePackHalf4(float4(encodedNormal, 1.0));
    result.emissionAndValid = BurtGIFinePackHalf4(float4(emission, 1.0));
    return result;
}

BurtGISceneVoxelFineLightingData BurtGIFinePackLighting(
    float3 radiance, float3 encodedNormal)
{
    BurtGISceneVoxelFineLightingData result;
    result.radianceAndValid = BurtGIFinePackHalf4(float4(radiance, 1.0));
    result.normalAndValid = BurtGIFinePackHalf4(float4(encodedNormal, 1.0));
    return result;
}

// 10 bits per axis; valid production fine coordinates are [0,4*N), N<=128.
uint BurtGIFinePackCellCoord(uint3 fineCoord)
{
    return (fineCoord.x & 0x3ffu) |
           ((fineCoord.y & 0x3ffu) << 10u) |
           ((fineCoord.z & 0x3ffu) << 20u);
}

uint3 BurtGIFineUnpackCellCoord(uint packed)
{
    return uint3(packed & 0x3ffu, (packed >> 10u) & 0x3ffu,
                 (packed >> 20u) & 0x3ffu);
}

uint BurtGIFineCellBit(uint3 localCoord)
{
    return localCoord.x + localCoord.y * 4u + localCoord.z * 16u;
}

// Exclusive rank of a known bit in the two-word geometry mask. No shift by
// 32 is evaluated at localBit=0, including the low/high boundary at bit32.
uint BurtGIFineMaskRank(uint2 mask, uint bit)
{
    uint localBit = bit & 31u;
    uint preceding = localBit == 0u ? 0u : (0xffffffffu >> (32u - localBit));
    return (bit >= 32u ? countbits(mask.x) : 0u) +
           countbits((bit < 32u ? mask.x : mask.y) & preceding);
}

#endif
