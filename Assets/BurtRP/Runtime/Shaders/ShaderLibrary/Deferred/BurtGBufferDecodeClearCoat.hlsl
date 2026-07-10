#ifndef BURT_GBUFFER_DECODE_CLEAR_COAT_INCLUDED
#define BURT_GBUFFER_DECODE_CLEAR_COAT_INCLUDED

BurtGBufferData BurtDecodeGBufferCustom_ClearCoat(BurtEncodedGBuffer Encoded, BurtGBufferData Data)
{
    Data.ClearCoatMask = saturate(Encoded.GBuffer3.b);
    Data.ClearCoatRoughness = ClampPerceptualRoughness(Encoded.GBuffer3.a);
    return Data;
}

#endif
