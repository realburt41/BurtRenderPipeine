#ifndef BURT_GBUFFER_ENCODE_HAIR_INCLUDED
#define BURT_GBUFFER_ENCODE_HAIR_INCLUDED

float4 BurtEncodeGBuffer3_Hair(BurtGBufferData Data)
{
    return float4(
        max(Data.HairSpecularColor, float3(0.0f, 0.0f, 0.0f)),
        BurtEncodeHairRoughnessFillForGBuffer(Data.HairSecondaryRoughness, Data.HairShadowFillStrength));
}

float4 BurtEncodeGBuffer4_Hair(BurtGBufferData Data)
{
    return float4(
        max(Data.HairSecondarySpecularColor, float3(0.0f, 0.0f, 0.0f)),
        BurtEncodeHairShiftBackLightForGBuffer(Data.HairSpecularShift, Data.HairSecondarySpecularShift, Data.HairBackLight));
}

#endif
