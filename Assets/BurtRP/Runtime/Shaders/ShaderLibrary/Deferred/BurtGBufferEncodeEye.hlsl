#ifndef BURT_GBUFFER_ENCODE_EYE_INCLUDED
#define BURT_GBUFFER_ENCODE_EYE_INCLUDED

float4 BurtEncodeGBuffer3_Eye(BurtGBufferData Data)
{
    float2 EncodedIrisNormalWS = BurtEncodeNormalWSForGBuffer(Data.EyeIrisNormalWS);
    return float4(EncodedIrisNormalWS, saturate(Data.EyeIrisMask), 0.0f);
}

float4 BurtEncodeGBuffer4_Eye(BurtGBufferData Data)
{
    float2 EncodedCausticNormalWS = BurtEncodeNormalWSForGBuffer(Data.EyeCausticNormalWS);
    return float4(EncodedCausticNormalWS, 0.0f, 0.0f);
}

#endif
