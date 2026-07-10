#ifndef BURT_GBUFFER_ENCODE_FOLIAGE_INCLUDED
#define BURT_GBUFFER_ENCODE_FOLIAGE_INCLUDED

float4 BurtEncodeGBuffer3_Foliage(BurtGBufferData Data)
{
    float EncodedFoliageWeight = Data.FoliageIsGrass > 0.5f
        ? saturate(Data.FoliageTransmissionWeight * 0.1f)
        : saturate(Data.FoliageTransmissionWeight);
    return float4(max(Data.FoliageTransmissionColor, float3(0.0f, 0.0f, 0.0f)), EncodedFoliageWeight);
}

float4 BurtEncodeGBuffer4_Foliage(BurtGBufferData Data)
{
    return float4(
        max(Data.FoliageScreenSpaceShadowIntensity, 0.0f),
        0.0f,
        BurtEncodeFoliageBackLightNdotLForGBuffer(Data.FoliageBackLight, Data.FoliageTransmissionNdotL),
        saturate(Data.FoliageThickness));
}

#endif
