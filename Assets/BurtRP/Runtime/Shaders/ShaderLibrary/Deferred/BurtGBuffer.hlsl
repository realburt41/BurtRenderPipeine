#ifndef BURT_GBUFFER_INCLUDED
#define BURT_GBUFFER_INCLUDED

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtBRDF.hlsl"
// Authoritative BurtRP deferred GBuffer layout contract, aligned to the XRender PC-style fixed slots.
// GBuffer0: normal payload. rgb = RGB888-packed normalWS, a = perceptual roughness. Backed by R8G8B8A8_UNorm.
// GBuffer1: base payload. rgb = baseColor, a = occlusion. Backed by R8G8B8A8_SRGB/UNorm.
// GBuffer2: property payload. r = packed(shadingModelID, material channel), g = metallic, b = smoothness, a = reflectance. Backed by R8G8B8A8_UNorm.
// GBuffer3: low-precision per-model custom payload. Every channel must be 0..1 and safe for R8G8B8A8_UNorm.
//   Default/ClearCoat: clearCoatNormal.xy, clearCoatMask, clearCoatRoughness.
//   Hair/Fur: primarySpecularColor.rgb, pack(secondaryRoughness, shadowFillStrength).
//   Subsurface: geometryNormal.xy, reserved, pack(power, ambient) or 3S curvature.
//   Fabric/Silk: fuzzColor.rgb, fuzzWeight.
//   Foliage/Grass: transmissionColor.rgb, transmissionWeight or grass transmissionWeight * 0.1.
//   Eye: irisNormal.xy, irisMask, reserved.
// GBuffer4: emission.rgb, reserved alpha. Backed by ARGBHalf.
// GBuffer5: higher-precision per-model custom payload. Direction/profile/anisotropy packing lives here until it is proven R8-safe.
//   Default/ClearCoat: tangent.xy, anisotropy, reserved.
//   Hair/Fur: secondarySpecularColor.rgb, pack(primaryShift, secondaryShift, backLight).
//   Subsurface: tangent.xy, pack(distortion, mode), pack(thickness, profileIndex).
//   Fabric/Silk: tangent.xy, anisotropy, pack(fuzzRoughness, isSilk).
//   Foliage/Grass: screenSpaceShadowIntensity, reserved, pack(backLight, transmissionNdotL), thickness.
//   Eye: causticNormal.xy, reserved, reserved.

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBufferData.hlsl"

// Pack a world-space unit vector into two 0..1 channels; BurtRP stores it as RGB888 in GBuffer0.rgb.
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBufferPacking.hlsl"

// Creates semantic GBuffer Data from Material inputs. Hair passes use NormalWS as the stored strand direction.

BurtGBufferData BurtCreateGBufferData(BurtSurfaceData SurfaceData, float3 NormalWS, float4 TangentWS, float3 Emission)
{
    BurtGBufferData Data;

    Data.BaseColor = SurfaceData.BaseColor.rgb;

    // Vector slot stores NormalWS for Lit/ClearCoat/Subsurface and strand direction for Hair.
    Data.NormalWS = BurtSafeNormalize(NormalWS);
    Data.ClearCoatNormalWS = Data.NormalWS;
    Data.TangentWS = BurtOrthonormalizeTangentWS(Data.NormalWS, TangentWS.xyz);
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL || BURT_ACTIVE_FOLIAGE_SHADING_MODEL || BURT_ACTIVE_EYE_SHADING_MODEL
    Data.Anisotropy = 0.0f;
#elif BURT_ENABLE_SUBSURFACE_SHADING || BURT_ENABLE_FOLIAGE_SHADING || BURT_ENABLE_EYE_SHADING
    Data.Anisotropy = (BurtIsActiveSubsurfaceShadingModel(SurfaceData.ShadingModelID) || BurtIsActiveFoliageShadingModel(SurfaceData.ShadingModelID) || BurtIsActiveEyeShadingModel(SurfaceData.ShadingModelID)) ? 0.0f : clamp(SurfaceData.Anisotropy, -1.0f, 1.0f);
#else
    Data.Anisotropy = clamp(SurfaceData.Anisotropy, -1.0f, 1.0f);
#endif

#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL || BURT_ACTIVE_FOLIAGE_SHADING_MODEL || BURT_ACTIVE_EYE_SHADING_MODEL
    Data.Metallic = 0.0f;
#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    Data.MaterialChannel = BurtEncodeFoliageSpecularTypeForGBuffer(SurfaceData.FoliageSpecularScale, SurfaceData.FoliageUseSpecularColor);
#elif BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    Data.MaterialChannel = 1.0f;
#else
    Data.MaterialChannel = 0.0f;
#endif
#elif BURT_ENABLE_SUBSURFACE_SHADING || BURT_ENABLE_FOLIAGE_SHADING || BURT_ENABLE_EYE_SHADING
    bool IsSubsurfaceMaterial = false;
    bool IsFoliageMaterial = false;
    bool IsEyeMaterial = false;
#if BURT_ENABLE_SUBSURFACE_SHADING
    IsSubsurfaceMaterial = BurtIsActiveSubsurfaceShadingModel(SurfaceData.ShadingModelID);
#endif
#if BURT_ENABLE_FOLIAGE_SHADING
    IsFoliageMaterial = BurtIsActiveFoliageShadingModel(SurfaceData.ShadingModelID);
#endif
#if BURT_ENABLE_EYE_SHADING
    IsEyeMaterial = BurtIsActiveEyeShadingModel(SurfaceData.ShadingModelID);
#endif
    if (IsSubsurfaceMaterial || IsFoliageMaterial || IsEyeMaterial)
    {
        Data.Metallic = 0.0f;
#if BURT_ENABLE_FOLIAGE_SHADING
        if (IsFoliageMaterial)
        {
            Data.MaterialChannel = BurtEncodeFoliageSpecularTypeForGBuffer(SurfaceData.FoliageSpecularScale, SurfaceData.FoliageUseSpecularColor);
        }
        else
#endif
        {
            Data.MaterialChannel = IsSubsurfaceMaterial ? 1.0f : 0.0f;
        }
    }
    else
    {
        Data.Metallic = saturate(SurfaceData.Metallic);
        Data.MaterialChannel = Data.Metallic;
    }
#else
    Data.Metallic = saturate(SurfaceData.Metallic);
    Data.MaterialChannel = Data.Metallic;
#endif
    Data.Smoothness = saturate(SurfaceData.Smoothness);
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    Data.Reflectance = BURT_SUBSURFACE_FIXED_REFLECTANCE;
#elif BURT_ENABLE_SUBSURFACE_SHADING
    Data.Reflectance = BurtIsActiveSubsurfaceShadingModel(SurfaceData.ShadingModelID) ? BURT_SUBSURFACE_FIXED_REFLECTANCE : saturate(SurfaceData.Reflectance);
#else
    Data.Reflectance = saturate(SurfaceData.Reflectance);
#endif
    Data.Occlusion = saturate(SurfaceData.Occlusion);

    Data.PerceptualRoughness = ClampPerceptualRoughness(PerceptualSmoothnessToPerceptualRoughness(Data.Smoothness));

    Data.Emission = max(Emission, float3(0.0f, 0.0f, 0.0f));

    Data.ShadingModelID = BurtResolveSurfaceShadingModel(SurfaceData.ShadingModelID);
    Data.ClearCoatMask = 0.0f;
    Data.ClearCoatRoughness = 0.2f;
    Data.SubsurfaceThickness = BURT_SUBSURFACE_DEFAULT_THICKNESS;
    Data.SubsurfacePower = BURT_SUBSURFACE_DEFAULT_POWER;
    Data.SubsurfaceDistortion = BURT_SUBSURFACE_DEFAULT_DISTORTION;
    Data.SubsurfaceAmbient = BURT_SUBSURFACE_DEFAULT_AMBIENT;
    Data.SubsurfaceScatteringMode = BURT_SUBSURFACE_DEFAULT_SCATTERING_MODE;
    Data.Subsurface3SCurvature = 1.0f - BURT_SUBSURFACE_DEFAULT_THICKNESS;
    Data.SubsurfaceProfileIndex = BURT_SUBSURFACE_DEFAULT_PROFILE_INDEX;
    Data.SubsurfaceGeometryNormalWS = Data.NormalWS;
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
    Data.FoliageTransmissionColor = float3(0.55f, 0.85f, 0.35f);
    Data.FoliageTransmissionWeight = 0.0f;
    Data.FoliageThickness = 0.5f;
    Data.FoliageBackLight = 0.5f;
    Data.FoliageTransmissionNdotL = 0.5f;
    Data.FoliageSpecularScale = 1.0f;
    Data.FoliageUseSpecularColor = 0.0f;
    Data.FoliageScreenSpaceShadowIntensity = 0.0f;
    Data.FoliageIsGrass = 0.0f;
    Data.EyeIrisMask = 0.0f;
    Data.EyeIrisNormalWS = Data.NormalWS;
    Data.EyeCausticNormalWS = Data.NormalWS;

#if BURT_ACTIVE_CLEAR_COAT_SHADING_MODEL
    Data.ClearCoatMask = saturate(SurfaceData.ClearCoatMask);
    Data.ClearCoatRoughness = ClampPerceptualRoughness(SurfaceData.ClearCoatRoughness);
#elif BURT_ENABLE_CLEAR_COAT_SHADING
    if (BurtIsActiveClearCoatShadingModel(Data.ShadingModelID))
    {
        Data.ClearCoatMask = saturate(SurfaceData.ClearCoatMask);
        Data.ClearCoatRoughness = ClampPerceptualRoughness(SurfaceData.ClearCoatRoughness);
    }
#endif

#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    Data.SubsurfaceThickness = saturate(SurfaceData.SubsurfaceThickness);
    Data.SubsurfacePower = BurtClampSubsurfacePower(SurfaceData.SubsurfacePower);
    Data.SubsurfaceDistortion = saturate(SurfaceData.SubsurfaceDistortion);
    Data.SubsurfaceAmbient = saturate(SurfaceData.SubsurfaceAmbient);
    Data.SubsurfaceScatteringMode = BurtClampSubsurfaceScatteringMode(SurfaceData.SubsurfaceScatteringMode);
    Data.Subsurface3SCurvature = saturate(SurfaceData.Subsurface3SCurvature);
    Data.SubsurfaceProfileIndex = BurtClampSubsurfaceProfileIndex(SurfaceData.SubsurfaceProfileIndex);
#elif BURT_ENABLE_SUBSURFACE_SHADING
    if (BurtIsActiveSubsurfaceShadingModel(Data.ShadingModelID))
    {
        Data.SubsurfaceThickness = saturate(SurfaceData.SubsurfaceThickness);
        Data.SubsurfacePower = BurtClampSubsurfacePower(SurfaceData.SubsurfacePower);
        Data.SubsurfaceDistortion = saturate(SurfaceData.SubsurfaceDistortion);
        Data.SubsurfaceAmbient = saturate(SurfaceData.SubsurfaceAmbient);
        Data.SubsurfaceScatteringMode = BurtClampSubsurfaceScatteringMode(SurfaceData.SubsurfaceScatteringMode);
        Data.Subsurface3SCurvature = saturate(SurfaceData.Subsurface3SCurvature);
        Data.SubsurfaceProfileIndex = BurtClampSubsurfaceProfileIndex(SurfaceData.SubsurfaceProfileIndex);
    }
#endif

#if BURT_ACTIVE_FABRIC_SHADING_MODEL
    Data.FabricIsSilk = saturate(SurfaceData.FabricIsSilk);
    Data.FabricFuzzWeight = saturate(SurfaceData.FabricFuzzWeight);
    Data.FabricFuzzRoughness = ClampPerceptualRoughness(SurfaceData.FabricFuzzRoughness);
    Data.FabricFuzzColor = max(SurfaceData.FabricFuzzColor, float3(0.0f, 0.0f, 0.0f));
#elif BURT_ENABLE_FABRIC_SHADING
    if (BurtIsActiveFabricShadingModel(Data.ShadingModelID))
    {
        Data.FabricIsSilk = saturate(SurfaceData.FabricIsSilk);
        Data.FabricFuzzWeight = saturate(SurfaceData.FabricFuzzWeight);
        Data.FabricFuzzRoughness = ClampPerceptualRoughness(SurfaceData.FabricFuzzRoughness);
        Data.FabricFuzzColor = max(SurfaceData.FabricFuzzColor, float3(0.0f, 0.0f, 0.0f));
    }
#endif

#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    Data.FoliageTransmissionColor = max(SurfaceData.FoliageTransmissionColor, float3(0.0f, 0.0f, 0.0f));
    Data.FoliageTransmissionWeight = SurfaceData.FoliageIsGrass > 0.5f
        ? max(SurfaceData.FoliageTransmissionWeight, 0.0f)
        : saturate(SurfaceData.FoliageTransmissionWeight);
    Data.FoliageThickness = saturate(SurfaceData.FoliageThickness);
    Data.FoliageBackLight = saturate(SurfaceData.FoliageBackLight);
    Data.FoliageTransmissionNdotL = saturate(SurfaceData.FoliageTransmissionNdotL);
    Data.FoliageSpecularScale = saturate(SurfaceData.FoliageSpecularScale);
    Data.FoliageUseSpecularColor = saturate(SurfaceData.FoliageUseSpecularColor);
    Data.FoliageScreenSpaceShadowIntensity = max(SurfaceData.FoliageScreenSpaceShadowIntensity, 0.0f);
    Data.FoliageIsGrass = saturate(SurfaceData.FoliageIsGrass);
#elif BURT_ENABLE_FOLIAGE_SHADING
    if (BurtIsActiveFoliageShadingModel(Data.ShadingModelID))
    {
        Data.FoliageTransmissionColor = max(SurfaceData.FoliageTransmissionColor, float3(0.0f, 0.0f, 0.0f));
        Data.FoliageTransmissionWeight = SurfaceData.FoliageIsGrass > 0.5f
            ? max(SurfaceData.FoliageTransmissionWeight, 0.0f)
            : saturate(SurfaceData.FoliageTransmissionWeight);
        Data.FoliageThickness = saturate(SurfaceData.FoliageThickness);
        Data.FoliageBackLight = saturate(SurfaceData.FoliageBackLight);
        Data.FoliageTransmissionNdotL = saturate(SurfaceData.FoliageTransmissionNdotL);
        Data.FoliageSpecularScale = saturate(SurfaceData.FoliageSpecularScale);
        Data.FoliageUseSpecularColor = saturate(SurfaceData.FoliageUseSpecularColor);
        Data.FoliageScreenSpaceShadowIntensity = max(SurfaceData.FoliageScreenSpaceShadowIntensity, 0.0f);
        Data.FoliageIsGrass = saturate(SurfaceData.FoliageIsGrass);
    }
#endif

#if BURT_ACTIVE_EYE_SHADING_MODEL
    Data.EyeIrisMask = saturate(SurfaceData.EyeIrisMask);
    Data.EyeIrisNormalWS = BurtSafeNormalize(SurfaceData.EyeIrisNormalWS);
    Data.EyeCausticNormalWS = BurtSafeNormalize(SurfaceData.EyeCausticNormalWS);
#elif BURT_ENABLE_EYE_SHADING
    if (BurtIsActiveEyeShadingModel(Data.ShadingModelID))
    {
        Data.EyeIrisMask = saturate(SurfaceData.EyeIrisMask);
        Data.EyeIrisNormalWS = BurtSafeNormalize(SurfaceData.EyeIrisNormalWS);
        Data.EyeCausticNormalWS = BurtSafeNormalize(SurfaceData.EyeCausticNormalWS);
    }
#endif

    return Data;
}

BurtGBufferData BurtCreateGBufferData(BurtSurfaceData SurfaceData, float3 NormalWS, float3 Emission)
{
    float3 SafeNormalWS = BurtSafeNormalize(NormalWS);
    return BurtCreateGBufferData(SurfaceData, SafeNormalWS, float4(BurtCreateFallbackTangentWS(SafeNormalWS), 1.0f), Emission);
}

#if (BURT_STATIC_SHADING_MODEL == 0) || BURT_ENABLE_HAIR_SHADING
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBufferHair.hlsl"
#endif
#if (BURT_STATIC_SHADING_MODEL == 0) || BURT_ENABLE_FUR_SHADING
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBufferFur.hlsl"
#endif
#if (BURT_STATIC_SHADING_MODEL == 0) || BURT_ENABLE_EYE_SHADING
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBufferEye.hlsl"
#endif
#if (BURT_STATIC_SHADING_MODEL == 0) || BURT_ENABLE_CLEAR_COAT_SHADING
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBufferClearCoat.hlsl"
#endif
#if (BURT_STATIC_SHADING_MODEL == 0) || BURT_ENABLE_SUBSURFACE_SHADING
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBufferSubsurface.hlsl"
#endif
#if (BURT_STATIC_SHADING_MODEL == 0) || BURT_ENABLE_FABRIC_SHADING
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBufferFabric.hlsl"
#endif
#if (BURT_STATIC_SHADING_MODEL == 0) || BURT_ENABLE_FOLIAGE_SHADING
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBufferFoliage.hlsl"
#endif
float3 BurtGetGBufferDirectionWS(BurtGBufferData GBufferData)
{
    return GBufferData.NormalWS;
}


float3 BurtGetDeferredSurfaceNormalWS(BurtGBufferData GBufferData)
{
    return GBufferData.NormalWS;
}

float3 BurtGetDefaultLitNormalWS(BurtGBufferData GBufferData)
{
    return GBufferData.NormalWS;
}


float BurtGetDefaultLitMetallic(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL || BURT_ACTIVE_EYE_SHADING_MODEL
    return 0.0f;
#elif BURT_ENABLE_SUBSURFACE_SHADING || BURT_ENABLE_EYE_SHADING
    return (BurtIsActiveSubsurfaceShadingModel(GBufferData.ShadingModelID) || BurtIsActiveEyeShadingModel(GBufferData.ShadingModelID)) ? 0.0f : saturate(GBufferData.Metallic);
#else
    return saturate(GBufferData.Metallic);
#endif
}


float3 BurtGetReflectionNormalWS(BurtGBufferData GBufferData)
{
#if BURT_ENABLE_CLEAR_COAT_SHADING
    float ClearCoatMask = BurtGetClearCoatMask(GBufferData);
    return BurtSafeNormalize(lerp(BurtGetDeferredSurfaceNormalWS(GBufferData), BurtGetClearCoatNormalWS(GBufferData), ClearCoatMask));
#else
    return BurtGetDeferredSurfaceNormalWS(GBufferData);
#endif
}

float BurtGetReflectionRoughness(BurtGBufferData GBufferData)
{
#if BURT_ENABLE_CLEAR_COAT_SHADING
    float ClearCoatMask = BurtGetClearCoatMask(GBufferData);
    return saturate(lerp(GBufferData.PerceptualRoughness, BurtGetClearCoatRoughness(GBufferData), ClearCoatMask));
#else
    return ClampPerceptualRoughness(GBufferData.PerceptualRoughness);
#endif
}


float BurtGetGBufferMaterialChannel(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_HAIR_SHADING_MODEL
    return BurtGetHairScatter(GBufferData);
#elif BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(GBufferData.ShadingModelID))
    {
        return BurtGetHairScatter(GBufferData);
    }
#endif

#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    return BurtGetSubsurfaceStrength(GBufferData);
#elif BURT_ENABLE_SUBSURFACE_SHADING
    if (BurtIsActiveSubsurfaceShadingModel(GBufferData.ShadingModelID))
    {
        return BurtGetSubsurfaceStrength(GBufferData);
    }
#endif

#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    return BurtGetFoliageSpecularScale(GBufferData);
#elif BURT_ENABLE_FOLIAGE_SHADING
    if (BurtIsActiveFoliageShadingModel(GBufferData.ShadingModelID))
    {
        return BurtGetFoliageSpecularScale(GBufferData);
    }
#endif

    return BurtGetDefaultLitMetallic(GBufferData);
}

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBufferEncode.hlsl"

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBufferDecode.hlsl"
BurtPBRMaterialData BurtPreparePBRMaterialData(BurtGBufferData GBufferData)
{
    BurtPBRMaterialData MaterialData;

    MaterialData.BaseColor = GBufferData.BaseColor;
#if BURT_ACTIVE_HAIR_SHADING_MODEL || BURT_ACTIVE_FOLIAGE_SHADING_MODEL || BURT_ACTIVE_EYE_SHADING_MODEL
    MaterialData.Metallic = 0.0f;
#elif BURT_ENABLE_HAIR_SHADING || BURT_ENABLE_FOLIAGE_SHADING || BURT_ENABLE_EYE_SHADING
    if (BurtIsActiveHairShadingModel(GBufferData.ShadingModelID) || BurtIsActiveFoliageShadingModel(GBufferData.ShadingModelID) || BurtIsActiveEyeShadingModel(GBufferData.ShadingModelID))
    {
        MaterialData.Metallic = 0.0f;
    }
    else
    {
        MaterialData.Metallic = BurtGetDefaultLitMetallic(GBufferData);
    }
#else
    MaterialData.Metallic = BurtGetDefaultLitMetallic(GBufferData);
#endif
#if BURT_ENABLE_CLEAR_COAT_SHADING
    MaterialData.ClearCoatMask = BurtGetClearCoatMask(GBufferData);
    MaterialData.ClearCoatRoughness = BurtGetClearCoatRoughness(GBufferData);
#endif
#if BURT_ENABLE_SUBSURFACE_SHADING
    MaterialData.SubsurfaceActive = BurtIsActiveSubsurfaceShadingModel(GBufferData.ShadingModelID) ? 1.0f : 0.0f;
    MaterialData.SubsurfaceThickness = BurtGetSubsurfaceThickness(GBufferData);
    MaterialData.SubsurfacePower = BurtGetSubsurfacePower(GBufferData);
    MaterialData.SubsurfaceDistortion = BurtGetSubsurfaceDistortion(GBufferData);
    MaterialData.SubsurfaceAmbient = BurtGetSubsurfaceAmbient(GBufferData);
    MaterialData.SubsurfaceScatteringMode = BurtGetSubsurfaceScatteringMode(GBufferData);
    MaterialData.Subsurface3SCurvature = saturate(GBufferData.Subsurface3SCurvature);
    MaterialData.SubsurfaceProfileIndex = BurtGetSubsurfaceProfileIndex(GBufferData);
#endif
#if BURT_ENABLE_FABRIC_SHADING
    MaterialData.FabricActive = BurtIsActiveFabricShadingModel(GBufferData.ShadingModelID) ? 1.0f : 0.0f;
    MaterialData.FabricIsSilk = BurtGetFabricIsSilk(GBufferData);
    MaterialData.FabricFuzzWeight = BurtGetFabricFuzzWeight(GBufferData);
    MaterialData.FabricFuzzRoughness = BurtGetFabricFuzzRoughness(GBufferData);
    MaterialData.FabricFuzzColor = BurtGetFabricFuzzColor(GBufferData);
#endif
#if BURT_ENABLE_FOLIAGE_SHADING
    MaterialData.FoliageActive = BurtIsActiveFoliageShadingModel(GBufferData.ShadingModelID) ? 1.0f : 0.0f;
    MaterialData.FoliageTransmissionColor = BurtGetFoliageTransmissionColor(GBufferData);
    MaterialData.FoliageTransmissionWeight = BurtGetFoliageTransmissionWeight(GBufferData);
    MaterialData.FoliageThickness = BurtGetFoliageThickness(GBufferData);
    MaterialData.FoliageBackLight = BurtGetFoliageBackLight(GBufferData);
    MaterialData.FoliageTransmissionNdotL = BurtGetFoliageTransmissionNdotL(GBufferData);
    MaterialData.FoliageSpecularScale = BurtGetFoliageSpecularScale(GBufferData);
    MaterialData.FoliageUseSpecularColor = BurtGetFoliageUseSpecularColor(GBufferData);
    MaterialData.FoliageScreenSpaceShadowIntensity = BurtGetFoliageScreenSpaceShadowIntensity(GBufferData);
    MaterialData.FoliageIsGrass = BurtGetFoliageIsGrass(GBufferData);
#endif
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    MaterialData.Reflectance = BURT_SUBSURFACE_FIXED_REFLECTANCE;
#elif BURT_ENABLE_SUBSURFACE_SHADING
    MaterialData.Reflectance = BurtIsActiveSubsurfaceShadingModel(GBufferData.ShadingModelID) ? BURT_SUBSURFACE_FIXED_REFLECTANCE : GBufferData.Reflectance;
#else
    MaterialData.Reflectance = GBufferData.Reflectance;
#endif
    MaterialData.Occlusion = GBufferData.Occlusion;
    MaterialData.Smoothness = GBufferData.Smoothness;
#if BURT_ACTIVE_HAIR_SHADING_MODEL || BURT_ACTIVE_SUBSURFACE_SHADING_MODEL || BURT_ACTIVE_FOLIAGE_SHADING_MODEL || BURT_ACTIVE_EYE_SHADING_MODEL
    MaterialData.Anisotropy = 0.0f;
#elif BURT_ENABLE_HAIR_SHADING || BURT_ENABLE_SUBSURFACE_SHADING || BURT_ENABLE_FOLIAGE_SHADING || BURT_ENABLE_EYE_SHADING
    if (BurtIsActiveHairShadingModel(GBufferData.ShadingModelID) || BurtIsActiveSubsurfaceShadingModel(GBufferData.ShadingModelID) || BurtIsActiveFoliageShadingModel(GBufferData.ShadingModelID) || BurtIsActiveEyeShadingModel(GBufferData.ShadingModelID))
    {
        MaterialData.Anisotropy = 0.0f;
    }
    else
    {
        MaterialData.Anisotropy = clamp(GBufferData.Anisotropy, -1.0f, 1.0f);
    }
#else
    MaterialData.Anisotropy = clamp(GBufferData.Anisotropy, -1.0f, 1.0f);
#endif

    MaterialData.PerceptualRoughness = ClampPerceptualRoughness(PerceptualSmoothnessToPerceptualRoughness(MaterialData.Smoothness));
    MaterialData.LinearRoughness = PerceptualRoughnessToLinearRoughness(MaterialData.PerceptualRoughness);
    MaterialData.A2 = LinearRoughnessToA2(MaterialData.LinearRoughness);

#if defined(BURT_SUBSURFACE_DEFERRED_POSTPROCESS_INPUT) && BURT_ENABLE_SUBSURFACE_SHADING
    float3 DiffuseBaseColor = BurtIsActiveSubsurfaceShadingModel(GBufferData.ShadingModelID) && !BurtIsSubsurface3SPreIntegratedMode(MaterialData.SubsurfaceScatteringMode) ? float3(1.0f, 1.0f, 1.0f) : MaterialData.BaseColor;
#else
    float3 DiffuseBaseColor = MaterialData.BaseColor;
#endif

    MaterialData.DiffuseColor = DiffuseColorFromBaseColor(DiffuseBaseColor, MaterialData.Metallic);
    MaterialData.F0 = DielectricReflectanceToF0(MaterialData.BaseColor, MaterialData.Reflectance, MaterialData.Metallic);
    MaterialData.F90 = ApproximateF90(MaterialData.F0);
#if BURT_MODEL_HAS_FOLIAGE
    if (MaterialData.FoliageActive > 0.5f)
    {
        MaterialData.F90 = MaterialData.FoliageUseSpecularColor > 0.5f
            ? saturate(MaterialData.BaseColor * MaterialData.FoliageSpecularScale)
            : saturate((MaterialData.BaseColor * 0.9f + 0.1f) * MaterialData.FoliageSpecularScale * 3.0f);
    }
#endif

    return MaterialData;
}

// Prepares PBR geometry Data from decoded GBuffer Data and reconstructed view direction.
BurtPBRGeometryData BurtPreparePBRGeometryData(BurtGBufferData GBufferData, float3 ViewDirectionWS)
{
    return BurtPreparePBRGeometryData(BurtGetDeferredSurfaceNormalWS(GBufferData), GBufferData.TangentWS, ViewDirectionWS);
}

#endif
