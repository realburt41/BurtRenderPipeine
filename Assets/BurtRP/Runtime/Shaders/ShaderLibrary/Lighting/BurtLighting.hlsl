// BurtRP lighting facade.
// Includes shared lighting modules and dispatches active shading models for forward/deferred paths.

#ifndef BURT_LIGHTING_INCLUDED
#define BURT_LIGHTING_INCLUDED
#include "UnityCG.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInput.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtBRDF.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBuffer.hlsl"

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLightingAdditionalLights.hlsl"
#if !defined(BURT_PBR_DIRECT_ONLY)
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLightingIndirect.hlsl"
#endif
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLightingPBRCore.hlsl"
#if !defined(BURT_PBR_DIRECT_ONLY) && BURT_ENABLE_FUR_SHADING && (!defined(BURT_DEFERRED_LIGHTING_PRUNE_MODEL_HELPERS) || defined(BURT_DEFERRED_SHADING_MODEL_FUR))
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLightingFur.hlsl"
#endif

#if BURT_ENABLE_EYE_SHADING && (!defined(BURT_DEFERRED_LIGHTING_PRUNE_MODEL_HELPERS) || defined(BURT_DEFERRED_SHADING_MODEL_DEFAULT_LIT))
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLightingEye.hlsl"
#endif

#if BURT_ENABLE_HAIR_SHADING && (!defined(BURT_DEFERRED_LIGHTING_PRUNE_MODEL_HELPERS) || defined(BURT_DEFERRED_SHADING_MODEL_HAIR))
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLightingHair.hlsl"
#endif

#if !defined(BURT_DEFERRED_LIGHTING_SINGLE_SHADING_MODEL) && !defined(BURT_FORWARD_SINGLE_SHADING_MODEL)
// Shading-model dispatch used by Forward and Deferred. More models can join this switch without changing pass wiring.
BurtPBRShadingComponents BurtEvaluateShadingModelComponents(BurtSurfaceData SurfaceData, BurtLight MainLight, float3 NormalWS, float3 ViewDirectionWS)
{
#if BURT_ENABLE_FUR_SHADING
    if (BurtIsActiveFurShadingModel(SurfaceData.ShadingModelID))
    {
        return BurtEvaluateFurShadingComponents(SurfaceData, MainLight, NormalWS, ViewDirectionWS);
    }
#endif

#if BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(SurfaceData.ShadingModelID))
    {
        BurtGBufferData HairGBufferData = BurtCreateHairGBufferData(SurfaceData, NormalWS, float3(0.0f, 0.0f, 0.0f));
        return BurtEvaluateHairShadingComponentsFromGBuffer(HairGBufferData, MainLight, ViewDirectionWS);
    }
#endif

#if BURT_ENABLE_EYE_SHADING
    if (BurtIsActiveEyeShadingModel(SurfaceData.ShadingModelID))
    {
        float3 SafeNormalWS = BurtSafeNormalize(NormalWS);
        BurtGBufferData EyeGBufferData = BurtCreateEyeGBufferData(SurfaceData, SafeNormalWS, float4(BurtCreateFallbackTangentWS(SafeNormalWS), 1.0f), float3(0.0f, 0.0f, 0.0f));
        return BurtEvaluateEyeShadingComponentsFromGBuffer(EyeGBufferData, MainLight, ViewDirectionWS);
    }
#endif

    return BurtEvaluatePBRShadingComponents(SurfaceData, MainLight, NormalWS, ViewDirectionWS);
}

BurtPBRShadingComponents BurtEvaluateShadingModelComponents(BurtSurfaceData SurfaceData, BurtLight MainLight, float3 NormalWS, float3 ViewDirectionWS, float3 PositionWS)
{
#if BURT_ENABLE_FUR_SHADING
    if (BurtIsActiveFurShadingModel(SurfaceData.ShadingModelID))
    {
        return BurtEvaluateFurShadingComponents(SurfaceData, MainLight, NormalWS, ViewDirectionWS, PositionWS);
    }
#endif

#if BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(SurfaceData.ShadingModelID))
    {
        BurtGBufferData HairGBufferData = BurtCreateHairGBufferData(SurfaceData, NormalWS, float3(0.0f, 0.0f, 0.0f));
        return BurtEvaluateHairShadingComponentsFromGBuffer(HairGBufferData, MainLight, ViewDirectionWS, PositionWS);
    }
#endif

#if BURT_ENABLE_EYE_SHADING
    if (BurtIsActiveEyeShadingModel(SurfaceData.ShadingModelID))
    {
        float3 SafeNormalWS = BurtSafeNormalize(NormalWS);
        BurtGBufferData EyeGBufferData = BurtCreateEyeGBufferData(SurfaceData, SafeNormalWS, float4(BurtCreateFallbackTangentWS(SafeNormalWS), 1.0f), float3(0.0f, 0.0f, 0.0f));
        return BurtEvaluateEyeShadingComponentsFromGBuffer(EyeGBufferData, MainLight, ViewDirectionWS, PositionWS);
    }
#endif

    return BurtEvaluatePBRShadingComponents(SurfaceData, MainLight, NormalWS, ViewDirectionWS, PositionWS);
}

BurtPBRShadingComponents BurtEvaluateShadingModelComponents(BurtSurfaceData SurfaceData, BurtLight MainLight, float3 NormalWS, float3 ViewDirectionWS, float3 PositionWS, float2 ScreenUV)
{
#if BURT_ENABLE_FUR_SHADING
    if (BurtIsActiveFurShadingModel(SurfaceData.ShadingModelID))
    {
        return BurtEvaluateFurShadingComponents(SurfaceData, MainLight, NormalWS, ViewDirectionWS, PositionWS, ScreenUV);
    }
#endif

#if BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(SurfaceData.ShadingModelID))
    {
        BurtGBufferData HairGBufferData = BurtCreateHairGBufferData(SurfaceData, NormalWS, float3(0.0f, 0.0f, 0.0f));
        return BurtEvaluateHairShadingComponentsFromGBuffer(HairGBufferData, MainLight, ViewDirectionWS, PositionWS, ScreenUV);
    }
#endif

#if BURT_ENABLE_EYE_SHADING
    if (BurtIsActiveEyeShadingModel(SurfaceData.ShadingModelID))
    {
        float3 SafeNormalWS = BurtSafeNormalize(NormalWS);
        BurtGBufferData EyeGBufferData = BurtCreateEyeGBufferData(SurfaceData, SafeNormalWS, float4(BurtCreateFallbackTangentWS(SafeNormalWS), 1.0f), float3(0.0f, 0.0f, 0.0f));
        return BurtEvaluateEyeShadingComponentsFromGBuffer(EyeGBufferData, MainLight, ViewDirectionWS, PositionWS, ScreenUV);
    }
#endif

    return BurtEvaluatePBRShadingComponents(SurfaceData, MainLight, NormalWS, ViewDirectionWS, PositionWS, ScreenUV);
}

float3 BurtEvaluateAdditionalLightingUnshadowedDebug(BurtSurfaceData SurfaceData, float3 NormalWS, float3 ViewDirectionWS, float3 PositionWS)
{
#if BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(SurfaceData.ShadingModelID))
    {
        BurtGBufferData HairGBufferData = BurtCreateHairGBufferData(SurfaceData, NormalWS, float3(0.0f, 0.0f, 0.0f));
        BurtPBRGeometryData HairGeometryData = BurtPrepareHairGeometryData(HairGBufferData, ViewDirectionWS);
        BurtHairDirectComponents HairAdditional = BurtEvaluateHairAdditionalDirectLightingUnshadowedComponents(HairGBufferData, HairGeometryData, PositionWS);
        return HairAdditional.Diffuse + HairAdditional.Specular;
    }
#endif

    BurtPBRShadingCoreData CoreData = BurtPreparePBRShadingCoreData(SurfaceData, NormalWS, ViewDirectionWS);
    BurtDirectPBRComponents Additional = BurtEvaluatePBRAdditionalDirectLightingUnshadowedFromCore(CoreData, PositionWS);
    return Additional.Diffuse + Additional.Specular;
}

float3 BurtEvaluateAdditionalLightingUnshadowedDebug(BurtSurfaceData SurfaceData, float3 NormalWS, float3 ViewDirectionWS, float3 PositionWS, float2 ScreenUV)
{
#if BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(SurfaceData.ShadingModelID))
    {
        BurtGBufferData HairGBufferData = BurtCreateHairGBufferData(SurfaceData, NormalWS, float3(0.0f, 0.0f, 0.0f));
        BurtPBRGeometryData HairGeometryData = BurtPrepareHairGeometryData(HairGBufferData, ViewDirectionWS);
        BurtHairDirectComponents HairAdditional = BurtEvaluateHairAdditionalDirectLightingUnshadowedComponents(HairGBufferData, HairGeometryData, PositionWS, ScreenUV);
        return HairAdditional.Diffuse + HairAdditional.Specular;
    }
#endif

    BurtPBRShadingCoreData CoreData = BurtPreparePBRShadingCoreData(SurfaceData, NormalWS, ViewDirectionWS);
    BurtDirectPBRComponents Additional = BurtEvaluatePBRAdditionalDirectLightingUnshadowedFromCore(CoreData, PositionWS, ScreenUV);
    return Additional.Diffuse + Additional.Specular;
}

float3 BurtEvaluateAdditionalLightingUnshadowedDebugFromGBuffer(BurtGBufferData GBufferData, float3 ViewDirectionWS, float3 PositionWS)
{
#if BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(GBufferData.ShadingModelID))
    {
        GBufferData = BurtResolveHairDeferredGeometryData(GBufferData, ViewDirectionWS, PositionWS);
        BurtPBRGeometryData HairGeometryData = BurtPrepareHairGeometryData(GBufferData, ViewDirectionWS);
        BurtHairDirectComponents HairAdditional = BurtEvaluateHairAdditionalDirectLightingUnshadowedComponents(GBufferData, HairGeometryData, PositionWS);
        return HairAdditional.Diffuse + HairAdditional.Specular;
    }
#endif

    BurtPBRShadingCoreData CoreData = BurtPreparePBRShadingCoreData(GBufferData, ViewDirectionWS);
    BurtDirectPBRComponents Additional = BurtEvaluatePBRAdditionalDirectLightingUnshadowedFromCore(CoreData, PositionWS);
    return Additional.Diffuse + Additional.Specular;
}

float3 BurtEvaluateAdditionalLightingUnshadowedDebugFromGBuffer(BurtGBufferData GBufferData, float3 ViewDirectionWS, float3 PositionWS, float2 ScreenUV)
{
#if BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(GBufferData.ShadingModelID))
    {
        GBufferData = BurtResolveHairDeferredGeometryData(GBufferData, ViewDirectionWS, PositionWS);
        BurtPBRGeometryData HairGeometryData = BurtPrepareHairGeometryData(GBufferData, ViewDirectionWS);
        BurtHairDirectComponents HairAdditional = BurtEvaluateHairAdditionalDirectLightingUnshadowedComponents(GBufferData, HairGeometryData, PositionWS, ScreenUV);
        return HairAdditional.Diffuse + HairAdditional.Specular;
    }
#endif

    BurtPBRShadingCoreData CoreData = BurtPreparePBRShadingCoreData(GBufferData, ViewDirectionWS);
    BurtDirectPBRComponents Additional = BurtEvaluatePBRAdditionalDirectLightingUnshadowedFromCore(CoreData, PositionWS, ScreenUV);
    return Additional.Diffuse + Additional.Specular;
}

BurtPBRShadingComponents BurtEvaluateShadingModelComponentsFromGBuffer(BurtGBufferData GBufferData, BurtLight MainLight, float3 ViewDirectionWS)
{
#if BURT_ENABLE_FUR_SHADING
    if (BurtIsActiveFurShadingModel(GBufferData.ShadingModelID))
    {
        return BurtEvaluateFurShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS);
    }
#endif

#if BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(GBufferData.ShadingModelID))
    {
        return BurtEvaluateHairShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS);
    }
#endif

#if BURT_ENABLE_EYE_SHADING
    if (BurtIsActiveEyeShadingModel(GBufferData.ShadingModelID))
    {
        return BurtEvaluateEyeShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS);
    }
#endif

    return BurtEvaluatePBRShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS);
}

BurtPBRShadingComponents BurtEvaluateShadingModelComponentsFromGBuffer(BurtGBufferData GBufferData, BurtLight MainLight, float3 ViewDirectionWS, float3 PositionWS)
{
#if BURT_ENABLE_FUR_SHADING
    if (BurtIsActiveFurShadingModel(GBufferData.ShadingModelID))
    {
        return BurtEvaluateFurShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS, PositionWS);
    }
#endif

#if BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(GBufferData.ShadingModelID))
    {
        return BurtEvaluateHairShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS, PositionWS);
    }
#endif

#if BURT_ENABLE_EYE_SHADING
    if (BurtIsActiveEyeShadingModel(GBufferData.ShadingModelID))
    {
        return BurtEvaluateEyeShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS, PositionWS);
    }
#endif

    return BurtEvaluatePBRShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS, PositionWS);
}

BurtPBRShadingComponents BurtEvaluateShadingModelComponentsFromGBuffer(BurtGBufferData GBufferData, BurtLight MainLight, float3 ViewDirectionWS, float3 PositionWS, float2 ScreenUV)
{
#if BURT_ENABLE_FUR_SHADING
    if (BurtIsActiveFurShadingModel(GBufferData.ShadingModelID))
    {
        return BurtEvaluateFurShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS, PositionWS, ScreenUV);
    }
#endif

#if BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(GBufferData.ShadingModelID))
    {
        return BurtEvaluateHairShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS, PositionWS, ScreenUV);
    }
#endif

#if BURT_ENABLE_EYE_SHADING
    if (BurtIsActiveEyeShadingModel(GBufferData.ShadingModelID))
    {
        return BurtEvaluateEyeShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS, PositionWS, ScreenUV);
    }
#endif

    return BurtEvaluatePBRShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS, PositionWS, ScreenUV);
}

BurtPBRShadingComponents BurtEvaluateShadingModelComponentsFromGBuffer(BurtGBufferData GBufferData, BurtLight MainLight, float3 ViewDirectionWS, float3 PositionWS, float3 ShadowPositionWS, float2 ScreenUV)
{
#if BURT_ENABLE_FUR_SHADING
    if (BurtIsActiveFurShadingModel(GBufferData.ShadingModelID))
    {
        return BurtEvaluateFurShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS, PositionWS, ShadowPositionWS, ScreenUV);
    }
#endif

#if BURT_ENABLE_HAIR_SHADING
    if (BurtIsActiveHairShadingModel(GBufferData.ShadingModelID))
    {
        return BurtEvaluateHairShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS, PositionWS, ShadowPositionWS, ScreenUV);
    }
#endif

#if BURT_ENABLE_EYE_SHADING
    if (BurtIsActiveEyeShadingModel(GBufferData.ShadingModelID))
    {
        return BurtEvaluateEyeShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS, PositionWS, ShadowPositionWS, ScreenUV);
    }
#endif

    return BurtEvaluatePBRShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS, PositionWS, ShadowPositionWS, ScreenUV);
}

BurtPBRShadingComponents BurtEvaluateShadingModelComponentsFromGBuffer(BurtEncodedGBuffer EncodedGBuffer, BurtLight MainLight, float3 ViewDirectionWS)
{
    BurtGBufferData GBufferData = BurtDecodeGBuffer(EncodedGBuffer);
    return BurtEvaluateShadingModelComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS);
}

BurtPBRShadingComponents BurtEvaluateShadingModelComponentsFromGBuffer(BurtEncodedGBuffer EncodedGBuffer, BurtLight MainLight, float3 ViewDirectionWS, float3 PositionWS)
{
    BurtGBufferData GBufferData = BurtDecodeGBuffer(EncodedGBuffer);
    return BurtEvaluateShadingModelComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS, PositionWS);
}

BurtPBRShadingComponents BurtEvaluateShadingModelComponentsFromGBuffer(BurtEncodedGBuffer EncodedGBuffer, BurtLight MainLight, float3 ViewDirectionWS, float3 PositionWS, float2 ScreenUV)
{
    BurtGBufferData GBufferData = BurtDecodeGBuffer(EncodedGBuffer);
    return BurtEvaluateShadingModelComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS, PositionWS, ScreenUV);
}
#endif

#if !defined(BURT_DEFERRED_LIGHTING_SINGLE_SHADING_MODEL)
float3 BurtEvaluateSpecular(BurtSurfaceData SurfaceData, BurtLight Light, float3 NormalWS, float3 ViewDirectionWS)
{
    float DiffuseVisibility = BurtLambert(NormalWS, Light.DirectionWS);

    float3 HalfDirectionWS = BurtSafeNormalize(Light.DirectionWS + ViewDirectionWS);

    float SpecularNdotH = saturate(dot(NormalWS, HalfDirectionWS));

    float SpecularPower = lerp(8.0f, 256.0f, SurfaceData.Smoothness);

    float SpecularTerm = pow(SpecularNdotH, SpecularPower);

    return DielectricReflectanceToF0(SurfaceData.BaseColor.rgb, SurfaceData.Reflectance, SurfaceData.Metallic) * Light.Color * SpecularTerm * DiffuseVisibility * Light.ShadowAttenuation;
}

float3 BurtEvaluateAdditionalDiffuseLights(BurtSurfaceData SurfaceData, float3 NormalWS, float3 PositionWS)
{
    float3 DiffuseLighting = float3(0.0f, 0.0f, 0.0f);
    int AdditionalLightCount = BurtGetAdditionalLightCount();

    [loop]
    for (int LightIndex = 0; LightIndex < BURT_MAX_ADDITIONAL_LIGHTS; LightIndex++)
    {
        if (LightIndex >= AdditionalLightCount)
        {
            break;
        }

        DiffuseLighting += BurtEvaluateDiffuse(SurfaceData.BaseColor.rgb, BurtCreateAdditionalLight(LightIndex, PositionWS, NormalWS), NormalWS);
    }

    return DiffuseLighting;
}

float3 BurtEvaluateAdditionalSpecularLights(BurtSurfaceData SurfaceData, float3 NormalWS, float3 ViewDirectionWS, float3 PositionWS)
{
    float3 SpecularLighting = float3(0.0f, 0.0f, 0.0f);
    int AdditionalLightCount = BurtGetAdditionalLightCount();

    [loop]
    for (int LightIndex = 0; LightIndex < BURT_MAX_ADDITIONAL_LIGHTS; LightIndex++)
    {
        if (LightIndex >= AdditionalLightCount)
        {
            break;
        }

        SpecularLighting += BurtEvaluateSpecular(SurfaceData, BurtCreateAdditionalLight(LightIndex, PositionWS, NormalWS), NormalWS, ViewDirectionWS);
    }

    return SpecularLighting;
}

float3 BurtEvaluateSimpleLit(BurtSurfaceData SurfaceData, BurtLight MainLight, float3 NormalWS)
{
    float3 AmbientIrradiance = BurtSampleIndirectDiffuseIrradiance(NormalWS);
    float3 AmbientColor = BurtEvaluateAmbient(SurfaceData.BaseColor.rgb, AmbientIrradiance);

    float3 DiffuseColor = BurtEvaluateDiffuse(SurfaceData.BaseColor.rgb, MainLight, NormalWS);

    return AmbientColor + DiffuseColor;
}

float3 BurtEvaluateSimpleLit(BurtSurfaceData SurfaceData, BurtLight MainLight, float3 NormalWS, float3 PositionWS)
{
    float3 Lighting = BurtEvaluateSimpleLit(SurfaceData, MainLight, NormalWS);
    Lighting += BurtEvaluateAdditionalDiffuseLights(SurfaceData, NormalWS, PositionWS);
    return Lighting;
}

float3 BurtEvaluateSimpleLitSpecular(BurtSurfaceData SurfaceData, BurtLight MainLight, float3 NormalWS, float3 ViewDirectionWS)
{
    float3 BaseLighting = BurtEvaluateSimpleLit(SurfaceData, MainLight, NormalWS);

    float3 SpecularLighting = BurtEvaluateSpecular(SurfaceData, MainLight, NormalWS, ViewDirectionWS);

    return BaseLighting + SpecularLighting;
}

float3 BurtEvaluateSimpleLitSpecular(BurtSurfaceData SurfaceData, BurtLight MainLight, float3 NormalWS, float3 ViewDirectionWS, float3 PositionWS)
{
    float3 Lighting = BurtEvaluateSimpleLitSpecular(SurfaceData, MainLight, NormalWS, ViewDirectionWS);
    Lighting += BurtEvaluateAdditionalDiffuseLights(SurfaceData, NormalWS, PositionWS);
    Lighting += BurtEvaluateAdditionalSpecularLights(SurfaceData, NormalWS, ViewDirectionWS, PositionWS);
    return Lighting;
}

float3 BurtEvaluateSimpleLitPBR(BurtSurfaceData SurfaceData, BurtLight MainLight, float3 NormalWS, float3 ViewDirectionWS)
{
    BurtPBRShadingComponents Components = BurtEvaluatePBRShadingComponents(SurfaceData, MainLight, NormalWS, ViewDirectionWS);

    return Components.Lighting;
}

float3 BurtEvaluateSimpleLitPBR(BurtSurfaceData SurfaceData, BurtLight MainLight, float3 NormalWS, float3 ViewDirectionWS, float3 PositionWS)
{
    BurtPBRShadingComponents Components = BurtEvaluatePBRShadingComponents(SurfaceData, MainLight, NormalWS, ViewDirectionWS, PositionWS);
    return Components.Lighting;
}
#endif

#endif // BURT_LIGHTING_INCLUDED
