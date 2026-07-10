#ifndef BURT_GBUFFER_DECODE_EYE_INCLUDED
#define BURT_GBUFFER_DECODE_EYE_INCLUDED

BurtGBufferData BurtDecodeGBufferCustom_Eye(BurtEncodedGBuffer Encoded, BurtGBufferData Data)
{
    Data.EyeIrisNormalWS = BurtDecodeNormalWSFromGBuffer(Encoded.GBuffer3.rg);
    Data.EyeIrisMask = saturate(Encoded.GBuffer3.b);
    Data.EyeCausticNormalWS = BurtDecodeNormalWSFromGBuffer(Encoded.GBuffer5.rg);
    return Data;
}

#endif
