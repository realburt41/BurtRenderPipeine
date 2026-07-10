#ifndef BURT_GBUFFER_DECODE_FOLIAGE_INCLUDED
#define BURT_GBUFFER_DECODE_FOLIAGE_INCLUDED

BurtGBufferData BurtDecodeGBufferCustom_Foliage(BurtEncodedGBuffer Encoded, BurtGBufferData Data)
{
    Data.FoliageTransmissionColor = max(Encoded.GBuffer3.rgb, float3(0.0f, 0.0f, 0.0f));
    BurtDecodeFoliageSpecularTypeFromGBuffer(Data.MaterialChannel, Data.FoliageSpecularScale, Data.FoliageUseSpecularColor);
    Data.FoliageIsGrass = 1.0f - saturate(Data.FoliageUseSpecularColor);
    Data.FoliageTransmissionWeight = Data.FoliageIsGrass > 0.5f
        ? max(Encoded.GBuffer3.a * 10.0f, 0.0f)
        : saturate(Encoded.GBuffer3.a);
    Data.FoliageScreenSpaceShadowIntensity = max(Encoded.GBuffer5.r, 0.0f);
    Data.TangentWS = BurtCreateFallbackTangentWS(Data.NormalWS);
    BurtDecodeFoliageBackLightNdotLFromGBuffer(Encoded.GBuffer5.b, Data.FoliageBackLight, Data.FoliageTransmissionNdotL);
    Data.FoliageThickness = saturate(Encoded.GBuffer5.a);
    return Data;
}

#endif
