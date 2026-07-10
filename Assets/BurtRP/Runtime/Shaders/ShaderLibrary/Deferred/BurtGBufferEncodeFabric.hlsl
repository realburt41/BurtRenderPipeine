#ifndef BURT_GBUFFER_ENCODE_FABRIC_INCLUDED
#define BURT_GBUFFER_ENCODE_FABRIC_INCLUDED

float4 BurtEncodeGBuffer3_Fabric(BurtGBufferData Data)
{
    return float4(max(Data.FabricFuzzColor, float3(0.0f, 0.0f, 0.0f)), saturate(Data.FabricFuzzWeight));
}

float4 BurtEncodeGBuffer4_Fabric(BurtGBufferData Data)
{
    float2 EncodedTangentWS = BurtEncodeNormalWSForGBuffer(Data.TangentWS);
    return float4(
        EncodedTangentWS,
        clamp(Data.Anisotropy, -1.0f, 1.0f) * 0.5f + 0.5f,
        BurtEncodeFabricRoughnessSilkForGBuffer(Data.FabricFuzzRoughness, Data.FabricIsSilk));
}

#endif
