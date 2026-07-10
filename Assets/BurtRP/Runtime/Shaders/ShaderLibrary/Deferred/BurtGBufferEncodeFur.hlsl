#ifndef BURT_GBUFFER_ENCODE_FUR_INCLUDED
#define BURT_GBUFFER_ENCODE_FUR_INCLUDED

float4 BurtEncodeGBuffer3_Fur(BurtGBufferData Data)
{
    return BurtEncodeClearCoatOrDefaultGBuffer3(Data);
}

float4 BurtEncodeGBuffer4_Fur(BurtGBufferData Data)
{
    return BurtEncodeDefaultOrClearCoatGBuffer4(Data);
}

#endif
