#ifndef BURT_GBUFFER_PACKING_COMMON_INCLUDED
#define BURT_GBUFFER_PACKING_COMMON_INCLUDED

float2 BurtEncodeNormalWSForGBuffer(float3 NormalWS)
{
    float3 N = BurtSafeNormalize(NormalWS);

    float InvL1 = rcp(max(abs(N.x) + abs(N.y) + abs(N.z), BURT_EPSILON));
    float2 Encoded = N.xy * InvL1;

    if (N.z < 0.0f)
    {
        Encoded = BurtWrapOctahedronNormal(Encoded);
    }

    return Encoded * 0.5f + 0.5f;
}

// Decode the octahedral unit vector used by the GBuffer normal/custom direction payloads.
float3 BurtDecodeNormalWSFromGBuffer(float2 EncodedNormal)
{
    float2 F = EncodedNormal * 2.0f - 1.0f;

    float3 N = float3(F.x, F.y, 1.0f - abs(F.x) - abs(F.y));

    float T = saturate(-N.z);
    N.x += N.x >= 0.0f ? -T : T;
    N.y += N.y >= 0.0f ? -T : T;

    return BurtSafeNormalize(N);
}

uint3 BurtPackFloat2To888UInt(float2 Value)
{
    uint2 Quantized = (uint2)(saturate(Value) * 4095.5f);
    uint2 Hi = Quantized >> 8;
    uint2 Lo = Quantized & 255u;
    return uint3(Lo, Hi.x | (Hi.y << 4));
}

float3 BurtPackFloat2To888(float2 Value)
{
    return (float3)BurtPackFloat2To888UInt(Value) / 255.0f;
}

float2 BurtUnpack888UIntToFloat2(uint3 Value)
{
    uint Hi = Value.z >> 4;
    uint Lo = Value.z & 15u;
    uint2 Packed = Value.xy | uint2(Lo << 8, Hi << 8);
    return (float2)Packed / 4095.0f;
}

float2 BurtUnpack888ToFloat2(float3 Value)
{
    uint3 Quantized = (uint3)(saturate(Value) * 255.5f);
    return BurtUnpack888UIntToFloat2(Quantized);
}

float3 BurtEncodeNormalWS888ForGBuffer(float3 NormalWS)
{
    float3 N = BurtSafeNormalize(NormalWS);
    float Z = max(abs(N.z), 1.0f / 1024.0f);
    N.z = N.z < 0.0f ? -Z : Z;
    return BurtPackFloat2To888(BurtEncodeNormalWSForGBuffer(N));
}

float3 BurtDecodeNormalWS888FromGBuffer(float3 EncodedNormal)
{
    return BurtDecodeNormalWSFromGBuffer(BurtUnpack888ToFloat2(EncodedNormal));
}

// Deferred lighting uses XRender-style high stencil bits for the authoritative
// shading model id. GBuffer2.r keeps this compatibility pack for fullscreen
// consumers that still need to branch per pixel.
// Keep each model bucket away from both edges: Fabric/Silk at metallic=0
// otherwise lands on the 4/5 boundary, and half/UNorm RT quantization can
// decode it as the previous shading model.
float BurtEncodeMetallicAndShadingModelForGBuffer(float MetallicOrScatter, float ShadingModelID)
{
    float ModelID = clamp(BurtResolveSurfaceShadingModel(ShadingModelID), 0.0f, BURT_GBUFFER_SHADING_MODEL_PACK_COUNT - 1.0f);
    float Material = BURT_GBUFFER_SHADING_MODEL_PACK_BIAS + saturate(MetallicOrScatter) * BURT_GBUFFER_SHADING_MODEL_PACK_SCALE;
    return (ModelID + Material) / BURT_GBUFFER_SHADING_MODEL_PACK_COUNT;
}

float BurtDecodeMetallicAndShadingModelFromGBuffer(float PackedValue, out float ShadingModelID)
{
    float Scaled = saturate(PackedValue) * BURT_GBUFFER_SHADING_MODEL_PACK_COUNT;
    ShadingModelID = floor(min(Scaled, BURT_GBUFFER_SHADING_MODEL_PACK_COUNT - BURT_EPSILON));
    return saturate((Scaled - ShadingModelID - BURT_GBUFFER_SHADING_MODEL_PACK_BIAS) / BURT_GBUFFER_SHADING_MODEL_PACK_SCALE);
}

#endif
