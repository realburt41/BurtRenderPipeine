#ifndef BURT_GBUFFER_ENCODE_COMMON_INCLUDED
#define BURT_GBUFFER_ENCODE_COMMON_INCLUDED

float4 BurtEncodeClearCoatOrDefaultGBuffer3(BurtGBufferData Data)
{
    float2 EncodedClearCoatNormalWS = BurtEncodeNormalWSForGBuffer(Data.ClearCoatNormalWS);

#if BURT_ACTIVE_CLEAR_COAT_SHADING_MODEL
    return float4(
        EncodedClearCoatNormalWS,
        saturate(Data.ClearCoatMask),
        ClampPerceptualRoughness(Data.ClearCoatRoughness));
#elif BURT_ENABLE_CLEAR_COAT_SHADING
    if (BurtIsActiveClearCoatShadingModel(Data.ShadingModelID))
    {
        return float4(
            EncodedClearCoatNormalWS,
            saturate(Data.ClearCoatMask),
            ClampPerceptualRoughness(Data.ClearCoatRoughness));
    }
#endif

    return float4(EncodedClearCoatNormalWS, 0.0f, 0.0f);
}

float4 BurtEncodeDefaultOrClearCoatGBuffer4(BurtGBufferData Data)
{
    float2 EncodedTangentWS = BurtEncodeNormalWSForGBuffer(Data.TangentWS);
    return float4(
        EncodedTangentWS,
        clamp(Data.Anisotropy, -1.0f, 1.0f) * 0.5f + 0.5f,
        0.0f);
}

float4 BurtClampGBuffer3LowPrecisionPayload(float4 Payload)
{
    return saturate(Payload);
}

#define BURT_ENCODE_GBUFFER3_SHADING_MODEL(ShadingModelName, Data) \
    BURT_TOKEN_PASTE2(BurtEncodeGBuffer3_, ShadingModelName)(Data)
#define BURT_ENCODE_GBUFFER4_SHADING_MODEL(ShadingModelName, Data) \
    BURT_TOKEN_PASTE2(BurtEncodeGBuffer4_, ShadingModelName)(Data)

#endif
