#ifndef BURT_GBUFFER_DECODE_HAIR_INCLUDED
#define BURT_GBUFFER_DECODE_HAIR_INCLUDED

BurtGBufferData BurtDecodeGBufferCustom_Hair(BurtEncodedGBuffer Encoded, BurtGBufferData Data)
{
    Data.HairSpecularColor = max(Encoded.GBuffer3.rgb, float3(0.0f, 0.0f, 0.0f));
    BurtDecodeHairRoughnessFillFromGBuffer(Encoded.GBuffer3.a, Data.HairSecondaryRoughness, Data.HairShadowFillStrength);
    Data.HairSecondarySpecularColor = max(Encoded.GBuffer5.rgb, float3(0.0f, 0.0f, 0.0f));
    BurtDecodeHairShiftBackLightFromGBuffer(Encoded.GBuffer5.a, Data.HairSpecularShift, Data.HairSecondarySpecularShift, Data.HairBackLight);
    return Data;
}

#endif
