#ifndef BURT_GBUFFER_ENCODE_INCLUDED
#define BURT_GBUFFER_ENCODE_INCLUDED

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBufferEncodeCommon.hlsl"

#if (BURT_STATIC_SHADING_MODEL == 0) || BURT_ENABLE_DEFAULT_LIT_SHADING
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBufferEncodeDefaultLit.hlsl"
#endif
#if (BURT_STATIC_SHADING_MODEL == 0) || BURT_ENABLE_HAIR_SHADING
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBufferEncodeHair.hlsl"
#endif
#if (BURT_STATIC_SHADING_MODEL == 0) || BURT_ENABLE_CLEAR_COAT_SHADING
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBufferEncodeClearCoat.hlsl"
#endif
#if (BURT_STATIC_SHADING_MODEL == 0) || BURT_ENABLE_SUBSURFACE_SHADING
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBufferEncodeSubsurface.hlsl"
#endif
#if (BURT_STATIC_SHADING_MODEL == 0) || BURT_ENABLE_FABRIC_SHADING
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBufferEncodeFabric.hlsl"
#endif
#if (BURT_STATIC_SHADING_MODEL == 0) || BURT_ENABLE_FOLIAGE_SHADING
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBufferEncodeFoliage.hlsl"
#endif
#if (BURT_STATIC_SHADING_MODEL == 0) || BURT_ENABLE_FUR_SHADING
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBufferEncodeFur.hlsl"
#endif
#if (BURT_STATIC_SHADING_MODEL == 0) || BURT_ENABLE_EYE_SHADING
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBufferEncodeEye.hlsl"
#endif

float4 BurtEncodeGBuffer3(BurtGBufferData Data)
{
#if BURT_STATIC_SHADING_MODEL
    return BURT_ENCODE_GBUFFER3_SHADING_MODEL(BURT_STATIC_SHADING_MODEL_NAME, Data);
#else
#if BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(Data.ShadingModelID))
    {
        return BurtEncodeGBuffer3_Hair(Data);
    }
#endif

#if BURT_ENABLE_SUBSURFACE_SHADING
    if (BurtIsActiveSubsurfaceShadingModel(Data.ShadingModelID))
    {
        return BurtEncodeGBuffer3_Subsurface(Data);
    }
#endif

#if BURT_ENABLE_FABRIC_SHADING
    if (BurtIsActiveFabricShadingModel(Data.ShadingModelID))
    {
        return BurtEncodeGBuffer3_Fabric(Data);
    }
#endif

#if BURT_ENABLE_FOLIAGE_SHADING
    if (BurtIsActiveFoliageShadingModel(Data.ShadingModelID))
    {
        return BurtEncodeGBuffer3_Foliage(Data);
    }
#endif

#if BURT_ENABLE_EYE_SHADING
    if (BurtIsActiveEyeShadingModel(Data.ShadingModelID))
    {
        return BurtEncodeGBuffer3_Eye(Data);
    }
#endif

    return BurtEncodeGBuffer3_DefaultLit(Data);
#endif
}

float4 BurtEncodeGBuffer4(BurtGBufferData Data)
{
#if BURT_STATIC_SHADING_MODEL
    return BURT_ENCODE_GBUFFER4_SHADING_MODEL(BURT_STATIC_SHADING_MODEL_NAME, Data);
#else
#if BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(Data.ShadingModelID))
    {
        return BurtEncodeGBuffer4_Hair(Data);
    }
#endif

#if BURT_ENABLE_SUBSURFACE_SHADING
    if (BurtIsActiveSubsurfaceShadingModel(Data.ShadingModelID))
    {
        return BurtEncodeGBuffer4_Subsurface(Data);
    }
#endif

#if BURT_ENABLE_FABRIC_SHADING
    if (BurtIsActiveFabricShadingModel(Data.ShadingModelID))
    {
        return BurtEncodeGBuffer4_Fabric(Data);
    }
#endif

#if BURT_ENABLE_FOLIAGE_SHADING
    if (BurtIsActiveFoliageShadingModel(Data.ShadingModelID))
    {
        return BurtEncodeGBuffer4_Foliage(Data);
    }
#endif

#if BURT_ENABLE_EYE_SHADING
    if (BurtIsActiveEyeShadingModel(Data.ShadingModelID))
    {
        return BurtEncodeGBuffer4_Eye(Data);
    }
#endif

    return BurtEncodeGBuffer4_DefaultLit(Data);
#endif
}

BurtEncodedGBuffer BurtEncodeGBuffer(BurtGBufferData Data)
{
    BurtEncodedGBuffer Encoded;

    Encoded.GBuffer0 = float4(BurtEncodeNormalWS888ForGBuffer(Data.NormalWS), ClampPerceptualRoughness(Data.PerceptualRoughness));

    Encoded.GBuffer1 = float4(saturate(Data.BaseColor), saturate(Data.Occlusion));

    Encoded.GBuffer2 = float4(
        BurtEncodeMetallicAndShadingModelForGBuffer(Data.MaterialChannel, Data.ShadingModelID),
        saturate(Data.Metallic),
        saturate(Data.Smoothness),
        saturate(Data.Reflectance));

    Encoded.GBuffer3 = BurtClampGBuffer3LowPrecisionPayload(BurtEncodeGBuffer3(Data));
    Encoded.GBuffer4 = float4(max(Data.Emission, float3(0.0f, 0.0f, 0.0f)), 0.0f);
    Encoded.GBuffer5 = BurtEncodeGBuffer4(Data);

    return Encoded;
}

#endif
