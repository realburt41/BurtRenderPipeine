#ifndef BURT_DEFERRED_ADDITIONAL_LIGHTING_PASS_INCLUDED
#define BURT_DEFERRED_ADDITIONAL_LIGHTING_PASS_INCLUDED

// XRender DeferredPunctual equivalent. This pass intentionally owns only the
// additional-light loop; main light, indirect light, GI and emission stay in
// the base deferred stage.
#define BURT_DEFERRED_LIGHTING_SINGLE_SHADING_MODEL 1
#define BURT_USE_ADDITIONAL_LIGHT_BUFFER 1
#define BURT_USE_TILED_LIGHTING 1
#define BURT_PBR_DIRECT_ONLY 1
#define BURT_SHADOWS_ADDITIONAL_ONLY 1
#define BURT_PBR_SHADING_COMPONENTS_INCLUDE_BRDF_DEBUG 0
#define BURT_PBR_SHADING_COMPONENTS_INCLUDE_TRANSMISSION_DEBUG 0

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtPreExposure.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInput.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtShadingModelMacros.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtDeferred.hlsl"
#define BURT_DEFERRED_LIGHTING_PRUNE_MODEL_HELPERS 1
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLighting.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtShadows.hlsl"

#if defined(BURT_DEFERRED_ADDITIONAL_LIGHTING_DEBUG)
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtShadingDebugModes.hlsl"
#endif

float _BurtDeferredSubsurfaceDiffuseLuminanceOutputEnabled;
StructuredBuffer<uint> _BurtPunctualTileIds;
float _BurtPunctualTileDrawEnabled;
uint _BurtPunctualTileIdOffset;

struct BurtDeferredAdditionalLightingComponents
{
    float3 Diffuse;
    float3 Specular;
    float3 Transmission;
};

struct BurtDeferredAdditionalAttributes
{
    uint VertexID : SV_VertexID;
    uint InstanceID : SV_InstanceID;
};

struct BurtDeferredAdditionalVaryings
{
    float4 PositionCS : SV_POSITION;
    float2 ScreenUV : TEXCOORD0;
};

BurtDeferredAdditionalVaryings Vert(BurtDeferredAdditionalAttributes Input)
{
    BurtDeferredAdditionalVaryings Output;
    if (_BurtPunctualTileDrawEnabled <= 0.5f)
    {
        Output.PositionCS = BurtGetFullScreenTriangleVertexPosition(Input.VertexID);
        Output.ScreenUV = BurtGetFullScreenTriangleTexCoord(Input.VertexID);
        return Output;
    }

    uint packedTileId = _BurtPunctualTileIds[_BurtPunctualTileIdOffset + Input.InstanceID];
    uint tileX = packedTileId & 0xffffu;
    uint tileY = (packedTileId >> 16) & 0xffffu;
    const float2 tileCorners[6] =
    {
        float2(0.0f, 0.0f),
        float2(1.0f, 0.0f),
        float2(1.0f, 1.0f),
        float2(0.0f, 0.0f),
        float2(1.0f, 1.0f),
        float2(0.0f, 1.0f)
    };

    float2 tileCount = max(_BurtClusterLightGridParams.xy, 1.0f);
    float2 clusterUV = (float2(tileX, tileY) + tileCorners[Input.VertexID]) / tileCount;
    Output.PositionCS = float4(clusterUV * 2.0f - 1.0f, 0.0f, 1.0f);
    Output.ScreenUV = clusterUV;
#if UNITY_UV_STARTS_AT_TOP
    Output.ScreenUV.y = 1.0f - Output.ScreenUV.y;
#endif
    return Output;
}

BurtDeferredAdditionalLightingComponents BurtEvaluateDeferredAdditionalLighting(
    BurtGBufferData GBufferData,
    float3 ViewDirectionWS,
    float3 PositionWS,
    float3 ShadowPositionWS,
    float2 ScreenUV)
{
    BurtDeferredAdditionalLightingComponents Components = (BurtDeferredAdditionalLightingComponents)0;

#if defined(BURT_DEFERRED_SHADING_MODEL_HAIR)
    GBufferData = BurtResolveHairDeferredGeometryData(GBufferData, ViewDirectionWS, PositionWS);
    BurtPBRGeometryData HairGeometryData = BurtPrepareHairGeometryData(GBufferData, ViewDirectionWS);
    BurtHairDirectComponents HairAdditional = BurtEvaluateHairAdditionalDirectLightingComponents(
        GBufferData,
        HairGeometryData,
        PositionWS,
        ShadowPositionWS,
        ScreenUV);
    Components.Diffuse = HairAdditional.Diffuse;
    Components.Specular = HairAdditional.Specular;
#else
    BurtPBRShadingCoreData CoreData = BurtPreparePBRShadingCoreData(GBufferData, ViewDirectionWS);
    BurtDirectPBRComponents AdditionalDirect;

    #if defined(BURT_DEFERRED_SHADING_MODEL_DEFAULT_LIT) && BURT_ENABLE_EYE_SHADING
    if (BurtIsActiveEyeShadingModel(GBufferData.ShadingModelID))
    {
        AdditionalDirect = BurtEvaluateEyeAdditionalDirectLightingFromCore(
            CoreData,
            GBufferData,
            PositionWS,
            ShadowPositionWS,
            ScreenUV);
    }
    else
    #endif
    {
        AdditionalDirect = BurtEvaluatePBRAdditionalDirectLightingFromCore(
            CoreData,
            PositionWS,
            ShadowPositionWS,
            ScreenUV);
    }

    Components.Diffuse = AdditionalDirect.Diffuse;
    Components.Specular = AdditionalDirect.Specular;
#if BURT_MODEL_HAS_TRANSMISSION
    Components.Transmission = AdditionalDirect.Transmission;
#endif
#endif

    return Components;
}

#if defined(BURT_DEFERRED_ADDITIONAL_LIGHTING_DEBUG)
float3 BurtEvaluateDeferredAdditionalLightingUnshadowed(
    BurtGBufferData GBufferData,
    float3 ViewDirectionWS,
    float3 PositionWS,
    float2 ScreenUV)
{
#if defined(BURT_DEFERRED_SHADING_MODEL_HAIR)
    GBufferData = BurtResolveHairDeferredGeometryData(GBufferData, ViewDirectionWS, PositionWS);
    BurtPBRGeometryData HairGeometryData = BurtPrepareHairGeometryData(GBufferData, ViewDirectionWS);
    BurtHairDirectComponents HairAdditional = BurtEvaluateHairAdditionalDirectLightingUnshadowedComponents(
        GBufferData,
        HairGeometryData,
        PositionWS,
        ScreenUV);
    return HairAdditional.Diffuse + HairAdditional.Specular;
#else
    // This matches the existing deferred debug semantics: Eye uses the generic
    // unshadowed diagnostic path, while its shadowed result keeps the Eye BRDF.
    BurtPBRShadingCoreData CoreData = BurtPreparePBRShadingCoreData(GBufferData, ViewDirectionWS);
    BurtDirectPBRComponents AdditionalDirect =
        BurtEvaluatePBRAdditionalDirectLightingUnshadowedFromCore(CoreData, PositionWS, ScreenUV);
    return AdditionalDirect.Diffuse + AdditionalDirect.Specular;
#endif
}

bool BurtIsDeferredAdditionalLightingDebugMode(float ExpectedMode)
{
    return abs(_BurtShadingDebugMode - ExpectedMode) < 0.5f;
}

float3 BurtResolveDeferredAdditionalLightingDebugColor(
    BurtDeferredAdditionalLightingComponents Components,
    float3 ResolvedDiffuse,
    float3 ResolvedTransmission,
    BurtGBufferData GBufferData,
    float3 ViewDirectionWS,
    float3 PositionWS,
    float2 ScreenUV)
{
    if (BurtIsDeferredAdditionalLightingDebugMode(BURT_SHADING_DEBUG_MODE_DETAIL_LIGHTING) ||
        BurtIsDeferredAdditionalLightingDebugMode(BURT_SHADING_DEBUG_MODE_FINAL_LIGHTING))
    {
        return max(ResolvedDiffuse + Components.Specular + ResolvedTransmission, 0.0f);
    }

    if (BurtIsDeferredAdditionalLightingDebugMode(BURT_SHADING_DEBUG_MODE_DIRECT_DIFFUSE))
    {
        return max(ResolvedDiffuse, 0.0f);
    }

    if (BurtIsDeferredAdditionalLightingDebugMode(BURT_SHADING_DEBUG_MODE_DIRECT_SPECULAR))
    {
        return max(Components.Specular, 0.0f);
    }

    if (BurtIsDeferredAdditionalLightingDebugMode(BURT_SHADING_DEBUG_MODE_ADDITIONAL_DIFFUSE))
    {
        return max(Components.Diffuse, 0.0f);
    }

    if (BurtIsDeferredAdditionalLightingDebugMode(BURT_SHADING_DEBUG_MODE_ADDITIONAL_SPECULAR))
    {
        return max(Components.Specular, 0.0f);
    }

    if (BurtIsDeferredAdditionalLightingDebugMode(BURT_SHADING_DEBUG_MODE_ADDITIONAL_LIGHTING_UNSHADOWED))
    {
        return max(
            BurtEvaluateDeferredAdditionalLightingUnshadowed(
                GBufferData,
                ViewDirectionWS,
                PositionWS,
                ScreenUV),
            0.0f);
    }

    if (BurtIsDeferredAdditionalLightingDebugMode(BURT_SHADING_DEBUG_MODE_HAIR_ADDITIONAL_LIGHTING))
    {
#if defined(BURT_DEFERRED_SHADING_MODEL_HAIR)
        return max(Components.Diffuse + Components.Specular, 0.0f);
#else
        return 0.0f;
#endif
    }

    return max(Components.Diffuse + Components.Specular, 0.0f);
}
#endif

float4 Frag(BurtDeferredAdditionalVaryings Input) : SV_Target
{
    float2 ScreenUV = Input.ScreenUV;
    BurtGBufferData GBufferData = BurtDecodeGBuffer(BurtSampleEncodedGBuffer(ScreenUV));

#if defined(BURT_DEFERRED_SHADING_MODEL_DEFAULT_LIT) && BURT_ENABLE_EYE_SHADING
    if (!BurtIsEyeShadingModel(GBufferData.ShadingModelID))
    {
        GBufferData.ShadingModelID = BURT_DEFERRED_LIGHTING_SHADING_MODEL_ID;
    }
#else
    GBufferData.ShadingModelID = BURT_DEFERRED_LIGHTING_SHADING_MODEL_ID;
#endif

    float RawDepth;
    float3 PositionWS;
    float3 ShadowPositionWS;
    float3 ViewDirectionWS;
    BurtPrepareDeferredViewData(ScreenUV, RawDepth, PositionWS, ShadowPositionWS, ViewDirectionWS);

#if defined(BURT_DEFERRED_ADDITIONAL_LIGHTING_DEBUG)
    if (BurtIsDeferredAdditionalLightingDebugMode(BURT_SHADING_DEBUG_MODE_DETAIL_LIGHTING))
    {
        GBufferData.BaseColor = float3(0.18f, 0.18f, 0.18f);
    }
#endif

    BurtDeferredAdditionalLightingComponents Components =
        BurtEvaluateDeferredAdditionalLighting(
            GBufferData,
            ViewDirectionWS,
            PositionWS,
            ShadowPositionWS,
            ScreenUV);

    float3 ResolvedDiffuse = Components.Diffuse;
    float3 ResolvedTransmission = Components.Transmission;
#if defined(BURT_DEFERRED_SHADING_MODEL_SUBSURFACE)
    float3 ZeroIndirectDiffuse = 0.0f;
    float3 ZeroIndirectTransmission = 0.0f;
    BurtPBRShadingCoreData CoreData = BurtPreparePBRShadingCoreData(GBufferData, ViewDirectionWS);
    BurtResolveSubsurfaceDeferredPostprocessTransmission(
        CoreData.MaterialData,
        ResolvedDiffuse,
        ResolvedTransmission,
        ZeroIndirectDiffuse,
        ZeroIndirectTransmission);
#endif

#if defined(BURT_DEFERRED_ADDITIONAL_LIGHTING_DEBUG)
    return float4(
        BurtResolveDeferredAdditionalLightingDebugColor(
            Components,
            ResolvedDiffuse,
            ResolvedTransmission,
            GBufferData,
            ViewDirectionWS,
            PositionWS,
            ScreenUV),
        1.0f);
#else
    float3 PreExposedLighting =
        BurtApplyPreExposure(ResolvedDiffuse + Components.Specular + ResolvedTransmission);
    float OutputAlpha = 0.0f;
#if defined(BURT_DEFERRED_SHADING_MODEL_SUBSURFACE)
    if (_BurtDeferredSubsurfaceDiffuseLuminanceOutputEnabled >= 0.5f)
    {
        OutputAlpha = dot(
            BurtApplyPreExposure(ResolvedDiffuse),
            float3(0.3f, 0.59f, 0.11f));
    }
#endif
    return float4(PreExposedLighting, OutputAlpha);
#endif
}

#endif
