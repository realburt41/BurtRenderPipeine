#ifndef BURT_GBUFFER_PACKING_FABRIC_INCLUDED
#define BURT_GBUFFER_PACKING_FABRIC_INCLUDED

#define BURT_FABRIC_ROUGHNESS_SILK_PACK_SCALE (0.499f)
float BurtEncodeFabricRoughnessSilkForGBuffer(float FuzzRoughness, float IsSilk)
{
    float PackedRoughness = saturate(FuzzRoughness) * BURT_FABRIC_ROUGHNESS_SILK_PACK_SCALE;
    return IsSilk > 0.5f ? 0.5f + PackedRoughness : PackedRoughness;
}

void BurtDecodeFabricRoughnessSilkFromGBuffer(float PackedValue, out float FuzzRoughness, out float IsSilk)
{
    float Packed = saturate(PackedValue);
    IsSilk = Packed >= 0.5f ? 1.0f : 0.0f;
    float LocalRoughness = IsSilk > 0.5f ? Packed - 0.5f : Packed;
    FuzzRoughness = ClampPerceptualRoughness(LocalRoughness / BURT_FABRIC_ROUGHNESS_SILK_PACK_SCALE);
}

#endif
