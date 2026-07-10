#ifndef BURT_GBUFFER_DECODE_INCLUDED
#define BURT_GBUFFER_DECODE_INCLUDED

#if (BURT_STATIC_SHADING_MODEL == 0) || BURT_ENABLE_DEFAULT_LIT_SHADING
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBufferDecodeDefaultLit.hlsl"
#endif
#if (BURT_STATIC_SHADING_MODEL == 0) || BURT_ENABLE_HAIR_SHADING
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBufferDecodeHair.hlsl"
#endif
#if (BURT_STATIC_SHADING_MODEL == 0) || BURT_ENABLE_CLEAR_COAT_SHADING
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBufferDecodeClearCoat.hlsl"
#endif
#if (BURT_STATIC_SHADING_MODEL == 0) || BURT_ENABLE_SUBSURFACE_SHADING
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBufferDecodeSubsurface.hlsl"
#endif
#if (BURT_STATIC_SHADING_MODEL == 0) || BURT_ENABLE_FABRIC_SHADING
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBufferDecodeFabric.hlsl"
#endif
#if (BURT_STATIC_SHADING_MODEL == 0) || BURT_ENABLE_FOLIAGE_SHADING
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBufferDecodeFoliage.hlsl"
#endif
#if (BURT_STATIC_SHADING_MODEL == 0) || BURT_ENABLE_FUR_SHADING
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBufferDecodeFur.hlsl"
#endif
#if (BURT_STATIC_SHADING_MODEL == 0) || BURT_ENABLE_EYE_SHADING
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBufferDecodeEye.hlsl"
#endif

#define BURT_DECODE_GBUFFER_CUSTOM_SHADING_MODEL(ShadingModelName, Encoded, Data) \
    BURT_TOKEN_PASTE2(BurtDecodeGBufferCustom_, ShadingModelName)(Encoded, Data)

// Decodes the five MRT payloads back into semantic GBuffer Data.
BurtGBufferData BurtDecodeGBufferInternal(BurtEncodedGBuffer Encoded, float OverrideShadingModelID, bool UseOverrideShadingModel)
{
    BurtGBufferData Data;

    Data.BaseColor = max(Encoded.GBuffer1.rgb, float3(0.0f, 0.0f, 0.0f));
    Data.Occlusion = saturate(Encoded.GBuffer1.a);

    Data.NormalWS = BurtDecodeNormalWS888FromGBuffer(Encoded.GBuffer0.rgb);
    float DecodedShadingModelID = 0.0f;
    Data.MaterialChannel = BurtDecodeMetallicAndShadingModelFromGBuffer(Encoded.GBuffer2.r, DecodedShadingModelID);
    Data.ShadingModelID = UseOverrideShadingModel ? BurtResolveSurfaceShadingModel(OverrideShadingModelID) : DecodedShadingModelID;
    Data.SubsurfaceGeometryNormalWS = Data.NormalWS;
#if BURT_ACTIVE_CLEAR_COAT_SHADING_MODEL
    Data.ClearCoatNormalWS = BurtDecodeNormalWSFromGBuffer(Encoded.GBuffer3.rg);
#elif BURT_ENABLE_CLEAR_COAT_SHADING
    if (BurtIsActiveClearCoatShadingModel(Data.ShadingModelID))
    {
        Data.ClearCoatNormalWS = BurtDecodeNormalWSFromGBuffer(Encoded.GBuffer3.rg);
    }
    else
    {
        Data.ClearCoatNormalWS = Data.NormalWS;
    }
#else
    Data.ClearCoatNormalWS = Data.NormalWS;
#endif
    Data.TangentWS = BurtOrthonormalizeTangentWS(Data.NormalWS, BurtDecodeNormalWSFromGBuffer(Encoded.GBuffer5.rg));
#if BURT_ACTIVE_HAIR_SHADING_MODEL || BURT_ACTIVE_SUBSURFACE_SHADING_MODEL || BURT_ACTIVE_FOLIAGE_SHADING_MODEL || BURT_ACTIVE_EYE_SHADING_MODEL
    Data.Anisotropy = 0.0f;
#elif BURT_ENABLE_HAIR_SHADING || BURT_ENABLE_SUBSURFACE_SHADING || BURT_ENABLE_FOLIAGE_SHADING || BURT_ENABLE_EYE_SHADING
    if (BurtIsActiveHairShadingModel(Data.ShadingModelID) || BurtIsActiveSubsurfaceShadingModel(Data.ShadingModelID) || BurtIsActiveFoliageShadingModel(Data.ShadingModelID) || BurtIsActiveEyeShadingModel(Data.ShadingModelID))
    {
        Data.Anisotropy = 0.0f;
    }
    else
    {
        Data.Anisotropy = clamp(Encoded.GBuffer5.b * 2.0f - 1.0f, -1.0f, 1.0f);
    }
#else
    Data.Anisotropy = clamp(Encoded.GBuffer5.b * 2.0f - 1.0f, -1.0f, 1.0f);
#endif
#if BURT_ACTIVE_HAIR_SHADING_MODEL || BURT_ACTIVE_SUBSURFACE_SHADING_MODEL || BURT_ACTIVE_FOLIAGE_SHADING_MODEL || BURT_ACTIVE_EYE_SHADING_MODEL
    Data.Metallic = 0.0f;
#elif BURT_ENABLE_HAIR_SHADING || BURT_ENABLE_SUBSURFACE_SHADING || BURT_ENABLE_FOLIAGE_SHADING || BURT_ENABLE_EYE_SHADING
    if (BurtIsActiveHairShadingModel(Data.ShadingModelID) || BurtIsActiveSubsurfaceShadingModel(Data.ShadingModelID) || BurtIsActiveFoliageShadingModel(Data.ShadingModelID) || BurtIsActiveEyeShadingModel(Data.ShadingModelID))
    {
        Data.Metallic = 0.0f;
    }
    else
    {
        Data.Metallic = saturate(Encoded.GBuffer2.g);
    }
#else
    Data.Metallic = saturate(Encoded.GBuffer2.g);
#endif
    Data.ClearCoatMask = 0.0f;
    Data.ClearCoatRoughness = 0.2f;
    Data.SubsurfaceThickness = BURT_SUBSURFACE_DEFAULT_THICKNESS;
    Data.SubsurfacePower = BURT_SUBSURFACE_DEFAULT_POWER;
    Data.SubsurfaceDistortion = BURT_SUBSURFACE_DEFAULT_DISTORTION;
    Data.SubsurfaceAmbient = BURT_SUBSURFACE_DEFAULT_AMBIENT;
    Data.SubsurfaceScatteringMode = BURT_SUBSURFACE_DEFAULT_SCATTERING_MODE;
    Data.Subsurface3SCurvature = 1.0f - BURT_SUBSURFACE_DEFAULT_THICKNESS;
    Data.SubsurfaceProfileIndex = BURT_SUBSURFACE_DEFAULT_PROFILE_INDEX;
    Data.HairSecondaryRoughness = 0.5f;
    Data.HairBackLight = 0.0f;
    Data.HairShadowFillStrength = 0.0f;
    Data.HairGeometryNormalWS = Data.NormalWS;
    Data.HairSpecularShift = 0.0f;
    Data.HairSecondarySpecularShift = 0.0f;
    Data.HairSpecularColor = float3(1.0f, 1.0f, 1.0f);
    Data.HairSecondarySpecularColor = float3(1.0f, 1.0f, 1.0f);
    Data.FabricIsSilk = 0.0f;
    Data.FabricFuzzWeight = 0.0f;
    Data.FabricFuzzRoughness = 0.75f;
    Data.FabricFuzzColor = float3(1.0f, 1.0f, 1.0f);
    Data.FoliageTransmissionColor = float3(0.0f, 0.0f, 0.0f);
    Data.FoliageTransmissionWeight = 0.0f;
    Data.FoliageThickness = 0.5f;
    Data.FoliageBackLight = 0.0f;
    Data.FoliageTransmissionNdotL = 0.5f;
    Data.FoliageSpecularScale = 0.0f;
    Data.FoliageUseSpecularColor = 0.0f;
    Data.FoliageScreenSpaceShadowIntensity = 0.0f;
    Data.FoliageIsGrass = 0.0f;
    Data.EyeIrisMask = 0.0f;
    Data.EyeIrisNormalWS = Data.NormalWS;
    Data.EyeCausticNormalWS = Data.NormalWS;
    Data.Smoothness = saturate(Encoded.GBuffer2.b);
    Data.PerceptualRoughness = ClampPerceptualRoughness(Encoded.GBuffer0.a);
    Data.Emission = max(Encoded.GBuffer4.rgb, float3(0.0f, 0.0f, 0.0f));
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    Data.Reflectance = BURT_SUBSURFACE_FIXED_REFLECTANCE;
#elif BURT_ENABLE_SUBSURFACE_SHADING
    Data.Reflectance = BurtIsActiveSubsurfaceShadingModel(Data.ShadingModelID) ? BURT_SUBSURFACE_FIXED_REFLECTANCE : saturate(Encoded.GBuffer2.a);
#else
    Data.Reflectance = saturate(Encoded.GBuffer2.a);
#endif

#if BURT_STATIC_SHADING_MODEL
    #if defined(BURT_DEFERRED_SHADING_MODEL_DEFAULT_LIT) && BURT_ENABLE_EYE_SHADING
    if (BurtIsEyeShadingModel(Data.ShadingModelID))
    {
        Data = BurtDecodeGBufferCustom_Eye(Encoded, Data);
    }
    else
    {
        Data = BurtDecodeGBufferCustom_DefaultLit(Encoded, Data);
    }
    #else
    Data = BURT_DECODE_GBUFFER_CUSTOM_SHADING_MODEL(BURT_STATIC_SHADING_MODEL_NAME, Encoded, Data);
    #endif
#else
#if BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(Data.ShadingModelID))
    {
        Data = BurtDecodeGBufferCustom_Hair(Encoded, Data);
    }
#endif

#if BURT_ENABLE_CLEAR_COAT_SHADING
    if (BurtIsActiveClearCoatShadingModel(Data.ShadingModelID))
    {
        Data = BurtDecodeGBufferCustom_ClearCoat(Encoded, Data);
    }
#endif

#if BURT_ENABLE_SUBSURFACE_SHADING
    if (BurtIsActiveSubsurfaceShadingModel(Data.ShadingModelID))
    {
        Data = BurtDecodeGBufferCustom_Subsurface(Encoded, Data);
    }
#endif

#if BURT_ENABLE_FABRIC_SHADING
    if (BurtIsActiveFabricShadingModel(Data.ShadingModelID))
    {
        Data = BurtDecodeGBufferCustom_Fabric(Encoded, Data);
    }
#endif

#if BURT_ENABLE_FOLIAGE_SHADING
    if (BurtIsActiveFoliageShadingModel(Data.ShadingModelID))
    {
        Data = BurtDecodeGBufferCustom_Foliage(Encoded, Data);
    }
#endif

#if BURT_ENABLE_EYE_SHADING
    if (BurtIsActiveEyeShadingModel(Data.ShadingModelID))
    {
        Data = BurtDecodeGBufferCustom_Eye(Encoded, Data);
    }
#endif
#endif

    return Data;
}

BurtGBufferData BurtDecodeGBuffer(BurtEncodedGBuffer Encoded)
{
    return BurtDecodeGBufferInternal(Encoded, BURT_SHADING_MODEL_DEFAULT_LIT, false);
}

BurtGBufferData BurtDecodeGBufferWithShadingModel(BurtEncodedGBuffer Encoded, float ShadingModelID)
{
    return BurtDecodeGBufferInternal(Encoded, ShadingModelID, true);
}

#endif
