// Eye-specific deferred/forward lighting helpers. Included after the common PBR core.
#ifndef BURT_LIGHTING_EYE_INCLUDED
#define BURT_LIGHTING_EYE_INCLUDED

float BurtEvaluateEyeDiffuseVisibility(float3 SurfaceNormalWS, float3 IrisNormalWS, float3 CausticNormalWS, float IrisMask, float3 LightDirectionWS)
{
    float3 L = BurtSafeNormalize(LightDirectionWS);
    float IrisNoL = saturate(dot(BurtSafeNormalize(IrisNormalWS), L));
    float Power = lerp(12.0f, 1.0f, IrisNoL);
    float CausticNoL = saturate(dot(BurtSafeNormalize(CausticNormalWS), L));
    float Caustic = 0.2f * (Power + 1.0f) * pow(CausticNoL, Power);
    float Iris = IrisNoL * Caustic;
    float Sclera = saturate(dot(BurtSafeNormalize(SurfaceNormalWS), L));
    return lerp(Sclera, Iris, saturate(IrisMask));
}

BurtDirectPBRComponents BurtEvaluateEyePBRDirectFromCore(BurtPBRShadingCoreData CoreData, BurtGBufferData GBufferData, BurtLight Light)
{
    BurtDirectPBRComponents Components = BurtEvaluatePBRDirectFromCore(CoreData, Light);
    float EyeNoL = BurtEvaluateEyeDiffuseVisibility(
        BurtGetDeferredSurfaceNormalWS(GBufferData),
        BurtGetEyeIrisNormalWS(GBufferData),
        BurtGetEyeCausticNormalWS(GBufferData),
        BurtGetEyeIrisMask(GBufferData),
        Light.DirectionWS);

    float3 DiffuseBRDF = CoreData.MaterialData.DiffuseColor * Components.BrdfTerms.DiffuseLobe * CoreData.EnergyTerms.EnergyPreservation;
    Components.Diffuse = DiffuseBRDF * Light.Color * EyeNoL * Light.ShadowAttenuation;
    Components.BrdfTerms.DiffuseBRDF = DiffuseBRDF;
    Components.BrdfTerms.NDotL = EyeNoL;
    return Components;
}

BurtDirectPBRComponents BurtEvaluateEyeAdditionalDirectLightingFromCore(BurtPBRShadingCoreData CoreData, BurtGBufferData GBufferData, float3 PositionWS)
{
    BurtDirectPBRComponents AdditionalDirectComponents = BurtCreateZeroPBRDirectComponents();
    int AdditionalLightCount = BurtGetAdditionalLightCount();
    if (AdditionalLightCount <= 0)
    {
        return AdditionalDirectComponents;
    }

    [loop]
    for (int LightIndex = 0; LightIndex < BURT_MAX_ADDITIONAL_LIGHTS; LightIndex++)
    {
        if (LightIndex >= AdditionalLightCount)
        {
            break;
        }

        BurtLight AdditionalLight = BurtCreateAdditionalLight(LightIndex, PositionWS, CoreData.GeometryData.NormalWS);
        AdditionalDirectComponents = BurtAddPBRDirectComponents(AdditionalDirectComponents, BurtEvaluateEyePBRDirectFromCore(CoreData, GBufferData, AdditionalLight));
    }

    return AdditionalDirectComponents;
}

BurtDirectPBRComponents BurtEvaluateEyeAdditionalDirectLightingFromCore(BurtPBRShadingCoreData CoreData, BurtGBufferData GBufferData, float3 PositionWS, float3 ShadowPositionWS)
{
    BurtDirectPBRComponents AdditionalDirectComponents = BurtCreateZeroPBRDirectComponents();
    int AdditionalLightCount = BurtGetAdditionalLightCount();
    if (AdditionalLightCount <= 0)
    {
        return AdditionalDirectComponents;
    }

    [loop]
    for (int LightIndex = 0; LightIndex < BURT_MAX_ADDITIONAL_LIGHTS; LightIndex++)
    {
        if (LightIndex >= AdditionalLightCount)
        {
            break;
        }

        BurtLight AdditionalLight = BurtCreateAdditionalLight(LightIndex, PositionWS, CoreData.GeometryData.NormalWS, ShadowPositionWS);
        AdditionalDirectComponents = BurtAddPBRDirectComponents(AdditionalDirectComponents, BurtEvaluateEyePBRDirectFromCore(CoreData, GBufferData, AdditionalLight));
    }

    return AdditionalDirectComponents;
}

BurtDirectPBRComponents BurtEvaluateEyeAdditionalDirectLightingFromCore(BurtPBRShadingCoreData CoreData, BurtGBufferData GBufferData, float3 PositionWS, float2 ScreenUV)
{
    if (!BurtHasAdditionalLights())
    {
        return BurtCreateZeroPBRDirectComponents();
    }

#if defined(BURT_USE_TILED_LIGHTING)
    uint2 Range = uint2(0u, 0u);
    uint UseClusterLightList = 0u;
    if (!BurtTryGetAdditionalLightListRange(ScreenUV, PositionWS, Range, UseClusterLightList))
    {
        return BurtEvaluateEyeAdditionalDirectLightingFromCore(CoreData, GBufferData, PositionWS);
    }

    BurtDirectPBRComponents AdditionalDirectComponents = BurtCreateZeroPBRDirectComponents();
    int AdditionalLightCount = BurtGetAdditionalLightCount();

    [loop]
    for (uint LocalLightIndex = 0u; LocalLightIndex < BURT_MAX_ADDITIONAL_LIGHTS; LocalLightIndex++)
    {
        if (LocalLightIndex >= Range.y)
        {
            break;
        }

        uint StoredLightIndex = BurtReadAdditionalLightListIndex(Range.x + LocalLightIndex, UseClusterLightList);
        if (StoredLightIndex >= (uint)AdditionalLightCount)
        {
            continue;
        }

        BurtLight AdditionalLight = BurtCreateAdditionalLight((int)StoredLightIndex, PositionWS, CoreData.GeometryData.NormalWS);
        AdditionalDirectComponents = BurtAddPBRDirectComponents(AdditionalDirectComponents, BurtEvaluateEyePBRDirectFromCore(CoreData, GBufferData, AdditionalLight));
    }

    return AdditionalDirectComponents;
#else
    return BurtEvaluateEyeAdditionalDirectLightingFromCore(CoreData, GBufferData, PositionWS);
#endif
}

BurtDirectPBRComponents BurtEvaluateEyeAdditionalDirectLightingFromCore(BurtPBRShadingCoreData CoreData, BurtGBufferData GBufferData, float3 PositionWS, float3 ShadowPositionWS, float2 ScreenUV)
{
    if (!BurtHasAdditionalLights())
    {
        return BurtCreateZeroPBRDirectComponents();
    }

#if defined(BURT_USE_TILED_LIGHTING)
    uint2 Range = uint2(0u, 0u);
    uint UseClusterLightList = 0u;
    if (!BurtTryGetAdditionalLightListRange(ScreenUV, PositionWS, Range, UseClusterLightList))
    {
        return BurtEvaluateEyeAdditionalDirectLightingFromCore(CoreData, GBufferData, PositionWS, ShadowPositionWS);
    }

    BurtDirectPBRComponents AdditionalDirectComponents = BurtCreateZeroPBRDirectComponents();
    int AdditionalLightCount = BurtGetAdditionalLightCount();

    [loop]
    for (uint LocalLightIndex = 0u; LocalLightIndex < BURT_MAX_ADDITIONAL_LIGHTS; LocalLightIndex++)
    {
        if (LocalLightIndex >= Range.y)
        {
            break;
        }

        uint StoredLightIndex = BurtReadAdditionalLightListIndex(Range.x + LocalLightIndex, UseClusterLightList);
        if (StoredLightIndex >= (uint)AdditionalLightCount)
        {
            continue;
        }

        BurtLight AdditionalLight = BurtCreateAdditionalLight((int)StoredLightIndex, PositionWS, CoreData.GeometryData.NormalWS, ShadowPositionWS);
        AdditionalDirectComponents = BurtAddPBRDirectComponents(AdditionalDirectComponents, BurtEvaluateEyePBRDirectFromCore(CoreData, GBufferData, AdditionalLight));
    }

    return AdditionalDirectComponents;
#else
    return BurtEvaluateEyeAdditionalDirectLightingFromCore(CoreData, GBufferData, PositionWS, ShadowPositionWS);
#endif
}

BurtPBRShadingComponents BurtEvaluateEyeShadingComponentsFromGBuffer(BurtGBufferData GBufferData, BurtLight MainLight, float3 ViewDirectionWS)
{
    BurtPBRShadingCoreData CoreData = BurtPreparePBRShadingCoreData(GBufferData, ViewDirectionWS);
    BurtDirectPBRComponents DirectComponents = BurtEvaluateEyePBRDirectFromCore(CoreData, GBufferData, MainLight);
    BurtIndirectPBRComponents IndirectComponents = BurtEvaluatePBRIndirectFromCore(CoreData, MainLight);
    return BurtComposePBRShadingComponents(CoreData, DirectComponents, IndirectComponents);
}

BurtPBRShadingComponents BurtEvaluateEyeShadingComponentsFromGBuffer(BurtGBufferData GBufferData, BurtLight MainLight, float3 ViewDirectionWS, float3 PositionWS)
{
    BurtPBRShadingCoreData CoreData = BurtPreparePBRShadingCoreData(GBufferData, ViewDirectionWS);
    BurtDirectPBRComponents MainDirectComponents = BurtEvaluateEyePBRDirectFromCore(CoreData, GBufferData, MainLight);
    BurtDirectPBRComponents AdditionalDirectComponents = BurtEvaluateEyeAdditionalDirectLightingFromCore(CoreData, GBufferData, PositionWS);
    BurtDirectPBRComponents DirectComponents = BurtAddPBRDirectComponents(MainDirectComponents, AdditionalDirectComponents);
    BurtIndirectPBRComponents IndirectComponents = BurtEvaluatePBRIndirectFromCore(CoreData, MainLight);
    return BurtComposePBRShadingComponentsWithAdditional(CoreData, DirectComponents, IndirectComponents, AdditionalDirectComponents);
}

BurtPBRShadingComponents BurtEvaluateEyeShadingComponentsFromGBuffer(BurtGBufferData GBufferData, BurtLight MainLight, float3 ViewDirectionWS, float3 PositionWS, float2 ScreenUV)
{
    BurtPBRShadingCoreData CoreData = BurtPreparePBRShadingCoreData(GBufferData, ViewDirectionWS);
    BurtDirectPBRComponents MainDirectComponents = BurtEvaluateEyePBRDirectFromCore(CoreData, GBufferData, MainLight);
    BurtDirectPBRComponents AdditionalDirectComponents = BurtEvaluateEyeAdditionalDirectLightingFromCore(CoreData, GBufferData, PositionWS, ScreenUV);
    BurtDirectPBRComponents DirectComponents = BurtAddPBRDirectComponents(MainDirectComponents, AdditionalDirectComponents);
    BurtIndirectPBRComponents IndirectComponents = BurtEvaluatePBRIndirectFromCore(CoreData, MainLight);
    return BurtComposePBRShadingComponentsWithAdditional(CoreData, DirectComponents, IndirectComponents, AdditionalDirectComponents);
}

BurtPBRShadingComponents BurtEvaluateEyeShadingComponentsFromGBuffer(BurtGBufferData GBufferData, BurtLight MainLight, float3 ViewDirectionWS, float3 PositionWS, float3 ShadowPositionWS, float2 ScreenUV)
{
    BurtPBRShadingCoreData CoreData = BurtPreparePBRShadingCoreData(GBufferData, ViewDirectionWS);
    BurtDirectPBRComponents MainDirectComponents = BurtEvaluateEyePBRDirectFromCore(CoreData, GBufferData, MainLight);
    BurtDirectPBRComponents AdditionalDirectComponents = BurtEvaluateEyeAdditionalDirectLightingFromCore(CoreData, GBufferData, PositionWS, ShadowPositionWS, ScreenUV);
    BurtDirectPBRComponents DirectComponents = BurtAddPBRDirectComponents(MainDirectComponents, AdditionalDirectComponents);
    BurtIndirectPBRComponents IndirectComponents = BurtEvaluatePBRIndirectFromCore(CoreData, MainLight);
    return BurtComposePBRShadingComponentsWithAdditional(CoreData, DirectComponents, IndirectComponents, AdditionalDirectComponents);
}

#endif
