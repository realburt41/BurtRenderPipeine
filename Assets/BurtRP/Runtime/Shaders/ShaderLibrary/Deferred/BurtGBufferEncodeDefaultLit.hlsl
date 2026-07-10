#ifndef BURT_GBUFFER_ENCODE_DEFAULT_LIT_INCLUDED
#define BURT_GBUFFER_ENCODE_DEFAULT_LIT_INCLUDED

float4 BurtEncodeGBuffer3_DefaultLit(BurtGBufferData Data)
{
    return BurtEncodeClearCoatOrDefaultGBuffer3(Data);
}

float4 BurtEncodeGBuffer4_DefaultLit(BurtGBufferData Data)
{
    return BurtEncodeDefaultOrClearCoatGBuffer4(Data);
}

#endif
