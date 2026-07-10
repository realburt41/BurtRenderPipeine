#ifndef BURT_GBUFFER_ENCODE_CLEAR_COAT_INCLUDED
#define BURT_GBUFFER_ENCODE_CLEAR_COAT_INCLUDED

float4 BurtEncodeGBuffer3_ClearCoat(BurtGBufferData Data)
{
    return BurtEncodeClearCoatOrDefaultGBuffer3(Data);
}

float4 BurtEncodeGBuffer4_ClearCoat(BurtGBufferData Data)
{
    return BurtEncodeDefaultOrClearCoatGBuffer4(Data);
}

#endif
