#ifndef BURT_GBUFFER_DECODE_FABRIC_INCLUDED
#define BURT_GBUFFER_DECODE_FABRIC_INCLUDED

BurtGBufferData BurtDecodeGBufferCustom_Fabric(BurtEncodedGBuffer Encoded, BurtGBufferData Data)
{
    Data.FabricFuzzColor = max(Encoded.GBuffer3.rgb, float3(0.0f, 0.0f, 0.0f));
    Data.FabricFuzzWeight = saturate(Encoded.GBuffer3.a);
    BurtDecodeFabricRoughnessSilkFromGBuffer(Encoded.GBuffer5.a, Data.FabricFuzzRoughness, Data.FabricIsSilk);
    return Data;
}

#endif
