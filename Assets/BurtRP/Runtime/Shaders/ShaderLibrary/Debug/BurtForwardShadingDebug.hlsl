// Forward-only direct shading-debug evaluation.
// Unlike Deferred's compatibility payload, this path reuses the live surface and
// lighting values and only builds diagnostics required by the selected mode.
#ifndef BURT_FORWARD_SHADING_DEBUG_INCLUDED
#define BURT_FORWARD_SHADING_DEBUG_INCLUDED

bool BurtTryEvaluateForwardSurfaceShadingDebug(
    BurtSurfaceData SurfaceData,
    float3 NormalWS,
    Varyings Input,
    out float3 DebugColor)
{
    DebugColor = 0.0f;

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ALBEDO))
    {
        DebugColor = max(SurfaceData.BaseColor.rgb, 0.0f);
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_NORMAL_WS))
    {
        DebugColor = BurtEncodeNormalWSForDebug(NormalWS);
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SMOOTHNESS))
    {
        DebugColor = SurfaceData.Smoothness.xxx;
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_METALLIC))
    {
        DebugColor = SurfaceData.Metallic.xxx;
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_OCCLUSION))
    {
        DebugColor = SurfaceData.Occlusion.xxx;
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_HEIGHT))
    {
        DebugColor = SurfaceData.Height.xxx;
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_REFLECTANCE))
    {
        DebugColor = SurfaceData.Reflectance.xxx;
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PRESKIN_POSITION))
    {
#if BURT_FORWARD_ENABLE_PRESKIN_POSITION
        DebugColor = saturate(BurtEncodePreSkinPositionForDebug(Input.PreSkinPositionOS));
#else
        DebugColor = 0.0f;
#endif
        return true;
    }

    return false;
}

bool BurtIsForwardGBufferShadingDebugMode()
{
    return BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_BASE_COLOR)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_NORMAL_WS)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_METALLIC)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_CLEAR_COAT_MASK)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_CLEAR_COAT_NORMAL_WS)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_CLEAR_COAT_ROUGHNESS)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_SUBSURFACE_STRENGTH)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_SUBSURFACE_THICKNESS)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_SUBSURFACE_PROFILE_INDEX)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_ANISOTROPY)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_TANGENT_WS)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_SMOOTHNESS)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_OCCLUSION)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_REFLECTANCE)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_ROUGHNESS)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_DIFFUSE_COLOR);
}

BurtGBufferData BurtCreateForwardDebugGBufferData(
    BurtSurfaceData SurfaceData,
    Varyings Input,
    float3 NormalWS,
    float3 ShadingDirectionWS,
    float Facing)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    float3 GeometryNormalWS = BurtGetMaterialPassGeometryNormalWS(Input.NormalWS, Facing);
    return BurtCreateMaterialPassGBufferData(SurfaceData, Input.UV0 * float2(_IDXTilling, 1.0f), GeometryNormalWS, NormalWS, Input.TangentWS, ShadingDirectionWS, Facing, 0.0f);
#else
    return BurtCreateMaterialPassGBufferData(SurfaceData, Input.BaseMapUV, Input.NormalWS, NormalWS, Input.TangentWS, ShadingDirectionWS, Facing, 0.0f);
#endif
}

bool BurtTryEvaluateForwardGBufferShadingDebug(
    BurtSurfaceData SurfaceData,
    Varyings Input,
    float3 NormalWS,
    float3 ShadingDirectionWS,
    float Facing,
    out float3 DebugColor)
{
    DebugColor = 0.0f;
    if (!BurtIsForwardGBufferShadingDebugMode())
    {
        return false;
    }

    // Encode/decode is intentionally inside the GBuffer mode gate. Other Forward
    // debug modes no longer pay for this synthetic deferred-material round trip.
    BurtGBufferData SourceData = BurtCreateForwardDebugGBufferData(SurfaceData, Input, NormalWS, ShadingDirectionWS, Facing);
    BurtGBufferData Data = BurtDecodeGBuffer(BurtEncodeGBuffer(SourceData));

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_BASE_COLOR))
    {
        DebugColor = max(Data.BaseColor, 0.0f);
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_NORMAL_WS))
    {
#if BURT_ENABLE_HAIR_SHADING
        float3 StrandDirectionWS = BurtGetHairStrandDirectionWS(Data);
#else
        float3 StrandDirectionWS = BurtGetDefaultLitNormalWS(Data);
#endif
        DebugColor = BurtEncodeNormalWSForDebug(BurtGetForwardDebugNormalWS(BurtGetDefaultLitNormalWS(Data), StrandDirectionWS));
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_METALLIC))
    {
        DebugColor = BurtGetGBufferMaterialChannel(Data).xxx;
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_CLEAR_COAT_MASK))
    {
#if BURT_ENABLE_CLEAR_COAT_SHADING
        DebugColor = BurtGetClearCoatMask(Data).xxx;
#else
        DebugColor = 0.0f;
#endif
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_CLEAR_COAT_NORMAL_WS))
    {
#if BURT_ENABLE_CLEAR_COAT_SHADING
        DebugColor = BurtEncodeNormalWSForDebug(BurtGetClearCoatNormalWS(Data));
#else
        DebugColor = BurtEncodeNormalWSForDebug(BurtGetDefaultLitNormalWS(Data));
#endif
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_CLEAR_COAT_ROUGHNESS))
    {
#if BURT_ENABLE_CLEAR_COAT_SHADING
        DebugColor = BurtGetClearCoatRoughness(Data).xxx;
#else
        DebugColor = 0.2f;
#endif
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_SUBSURFACE_STRENGTH))
    {
#if BURT_ENABLE_SUBSURFACE_SHADING
        DebugColor = BurtGetSubsurfaceStrength(Data).xxx;
#else
        DebugColor = 0.0f;
#endif
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_SUBSURFACE_THICKNESS))
    {
#if BURT_ENABLE_SUBSURFACE_SHADING
        DebugColor = BurtGetSubsurfaceThickness(Data).xxx;
#else
        DebugColor = BURT_SUBSURFACE_DEFAULT_THICKNESS.xxx;
#endif
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_SUBSURFACE_PROFILE_INDEX))
    {
#if BURT_ENABLE_SUBSURFACE_SHADING
        float ProfileIndex = BurtGetSubsurfaceProfileIndex(Data);
#else
        float ProfileIndex = BURT_SUBSURFACE_DEFAULT_PROFILE_INDEX;
#endif
        DebugColor = saturate(ProfileIndex / max(BURT_SHADING_DEBUG_SUBSURFACE_PROFILE_SLOT_COUNT - 1.0f, 1.0f)).xxx;
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_ANISOTROPY))
    {
        DebugColor = saturate(Data.Anisotropy * 0.5f + 0.5f).xxx;
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_TANGENT_WS))
    {
        DebugColor = BurtEncodeNormalWSForDebug(Data.TangentWS);
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_SMOOTHNESS))
    {
        DebugColor = Data.Smoothness.xxx;
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_OCCLUSION))
    {
        DebugColor = Data.Occlusion.xxx;
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_REFLECTANCE))
    {
        DebugColor = Data.Reflectance.xxx;
        return true;
    }

    BurtPBRMaterialData MaterialData = BurtPreparePBRMaterialData(Data);
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_ROUGHNESS))
    {
        DebugColor = MaterialData.PerceptualRoughness.xxx;
        return true;
    }
    DebugColor = saturate(MaterialData.DiffuseColor);
    return true;
}

bool BurtTryEvaluateForwardShadowShadingDebug(float3 PositionWS, float3 NormalWS, out float3 DebugColor)
{
    DebugColor = 0.0f;

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_ATTENUATION))
    {
        DebugColor = BurtSampleMainLightShadowWithoutPerObject(PositionWS, NormalWS).xxx;
        return true;
    }
    if (BurtNeedsAdditionalShadowAttenuationShadingDebug())
    {
        DebugColor = BurtEvaluateAdditionalShadowAttenuationDebug(PositionWS, NormalWS).xxx;
        return true;
    }
    if (BurtNeedsAdditionalShadowProjectionShadingDebug())
    {
        float3 FaceColor, UVColor, DepthColor, DepthDeltaColor;
        BurtFillAdditionalLightShadowProjectionDebugData(PositionWS, NormalWS, FaceColor, UVColor, DepthColor, DepthDeltaColor);
        if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_FACE)) DebugColor = saturate(FaceColor);
        else if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_UV)) DebugColor = saturate(UVColor);
        else if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_DEPTH)) DebugColor = saturate(DepthColor);
        else DebugColor = saturate(DepthDeltaColor);
        return true;
    }

    bool NeedsMainShadowData = BurtNeedsMainLightShadowProjectionShadingDebug()
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_CASCADE_INDEX)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_CASCADE_BLEND)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_DISTANCE_FADE)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_PCSS_RADIUS)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_RECEIVER_DEPTH_DELTA)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_PCSS_BLOCKER_FRACTION);
    if (NeedsMainShadowData)
    {
        float3 CascadeColor, ProjectionValidity;
        float CascadeBlend, DistanceFade, PCSSRadius, ReceiverDepthDelta;
        float ReceiverDepth, RawDepth, Compare, BlockerFraction;
        BurtFillMainLightShadowShadingDebugData(PositionWS, NormalWS, CascadeColor, CascadeBlend, DistanceFade, PCSSRadius, ReceiverDepthDelta, ReceiverDepth, RawDepth, Compare, ProjectionValidity, BlockerFraction);
        if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_CASCADE_INDEX)) DebugColor = saturate(CascadeColor);
        else if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_CASCADE_BLEND)) DebugColor = CascadeBlend.xxx;
        else if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_DISTANCE_FADE)) DebugColor = DistanceFade.xxx;
        else if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_PCSS_RADIUS)) DebugColor = PCSSRadius.xxx;
        else if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_RECEIVER_DEPTH_DELTA)) DebugColor = ReceiverDepthDelta.xxx;
        else if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_MAIN_LIGHT_SHADOW_RECEIVER_DEPTH)) DebugColor = ReceiverDepth.xxx;
        else if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_MAIN_LIGHT_SHADOW_RAW_DEPTH)) DebugColor = RawDepth.xxx;
        else if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_MAIN_LIGHT_SHADOW_COMPARE)) DebugColor = Compare.xxx;
        else if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_MAIN_LIGHT_SHADOW_PROJECTION_VALIDITY)) DebugColor = ProjectionValidity;
        else DebugColor = BlockerFraction.xxx;
        return true;
    }

    if (BurtNeedsPerObjectShadowProjectionShadingDebug() || BurtNeedsPerObjectShadowTransmissionShadingDebug())
    {
        float3 ObjectIndexColor, SliceColor, UVColor, DepthColor, CompareColor, TransmissionDepthColor, TransmissionThicknessColor;
        BurtFillPerObjectShadowShadingDebugData(PositionWS, NormalWS, _BurtPerObjectShadowObjectIndex, ObjectIndexColor, SliceColor, UVColor, DepthColor, CompareColor, TransmissionDepthColor, TransmissionThicknessColor);
        if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_OBJECT_INDEX)) DebugColor = saturate(ObjectIndexColor);
        else if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_SLICE)) DebugColor = saturate(SliceColor);
        else if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_UV)) DebugColor = saturate(UVColor);
        else if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_DEPTH)) DebugColor = saturate(DepthColor);
        else if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_COMPARE)) DebugColor = saturate(CompareColor);
        else if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_TRANSMISSION_DEPTH)) DebugColor = saturate(TransmissionDepthColor);
        else DebugColor = saturate(TransmissionThicknessColor);
        return true;
    }

    return false;
}

float3 BurtEvaluateForwardAdditionalUnshadowedDebug(
    BurtSurfaceData SurfaceData,
    Varyings Input,
    float3 NormalWS,
    float3 ShadingDirectionWS,
    float3 ViewDirectionWS,
    float Facing)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    float3 GeometryNormalWS = BurtGetMaterialPassGeometryNormalWS(Input.NormalWS, Facing);
    BurtGBufferData HairData = BurtCreateHairGBufferData(SurfaceData, ShadingDirectionWS, NormalWS, GeometryNormalWS, 0.0f);
    BurtPBRGeometryData GeometryData = BurtPrepareHairGeometryData(HairData, ViewDirectionWS);
    BurtHairDirectComponents Additional = BurtEvaluateHairAdditionalDirectLightingUnshadowedComponents(HairData, GeometryData, Input.PositionWS);
    return Additional.Diffuse + Additional.Specular;
#else
    BurtPBRShadingCoreData CoreData = BurtPreparePBRShadingCoreData(SurfaceData, NormalWS, ViewDirectionWS);
    BurtDirectPBRComponents Additional = BurtEvaluatePBRAdditionalDirectLightingUnshadowedFromCore(CoreData, Input.PositionWS);
    return Additional.Diffuse + Additional.Specular;
#endif
}

bool BurtTryEvaluateForwardShadedShadingDebug(
    BurtSurfaceData SurfaceData,
    BurtSurfaceData ShadingSurfaceData,
    BurtPBRShadingComponents Components,
    Varyings Input,
    float3 NormalWS,
    float3 ShadingDirectionWS,
    float3 ViewDirectionWS,
    float Facing,
    float3 EmissionColor,
    float3 FinalColor,
    out float3 DebugColor)
{
    DebugColor = 0.0f;

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ROUGHNESS))
    {
        DebugColor = Components.PerceptualRoughness.xxx;
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIFFUSE_COLOR))
    {
        DebugColor = saturate(Components.DiffuseColor);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DETAIL_LIGHTING))
    {
        DebugColor = max(Components.Lighting, 0.0f);
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIRECT_DIFFUSE))
    {
        DebugColor = max(Components.DirectDiffuse, 0.0f);
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIRECT_SPECULAR))
    {
        DebugColor = max(Components.DirectSpecular, 0.0f);
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_LIGHTING))
    {
        DebugColor = max(Components.AdditionalDiffuse + Components.AdditionalSpecular, 0.0f);
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_DIFFUSE))
    {
        DebugColor = max(Components.AdditionalDiffuse, 0.0f);
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_SPECULAR))
    {
        DebugColor = max(Components.AdditionalSpecular, 0.0f);
        return true;
    }
    if (BurtNeedsAdditionalLightingUnshadowedShadingDebug())
    {
        DebugColor = max(BurtEvaluateForwardAdditionalUnshadowedDebug(ShadingSurfaceData, Input, NormalWS, ShadingDirectionWS, ViewDirectionWS, Facing), 0.0f);
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_HAIR_ADDITIONAL_LIGHTING))
    {
#if BURT_ACTIVE_HAIR_SHADING_MODEL
        DebugColor = max(Components.AdditionalDiffuse + Components.AdditionalSpecular, 0.0f);
#elif BURT_ENABLE_HAIR_SHADING
        DebugColor = BurtIsActiveHairShadingModel(SurfaceData.ShadingModelID)
            ? max(Components.AdditionalDiffuse + Components.AdditionalSpecular, 0.0f)
            : 0.0f;
#else
        DebugColor = 0.0f;
#endif
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_INDIRECT_LIGHTING))
    {
        DebugColor = max(Components.IndirectDiffuse + Components.IndirectSpecular, 0.0f);
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_INDIRECT_DIFFUSE))
    {
        DebugColor = max(Components.IndirectDiffuse, 0.0f);
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_INDIRECT_SPECULAR))
    {
        DebugColor = max(Components.IndirectSpecular, 0.0f);
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_AMBIENT_OCCLUSION))
    {
        DebugColor = SurfaceData.Occlusion.xxx;
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_EMISSION))
    {
        DebugColor = max(EmissionColor, 0.0f);
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_FINAL_LIGHTING))
    {
        DebugColor = max(FinalColor, 0.0f);
        return true;
    }

    bool NeedsGIProbeDebug = BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GI_PROBE_IRRADIANCE)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GI_PROBE_VALIDITY)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GI_PROBE_SKY_VISIBILITY);
    if (NeedsGIProbeDebug)
    {
        float3 Irradiance;
        float Validity, SkyVisibility;
        BurtTrySampleGIProbeVolumeDebugData(Input.PositionWS, NormalWS, ViewDirectionWS, Irradiance, Validity, SkyVisibility);
        if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GI_PROBE_IRRADIANCE)) DebugColor = max(Irradiance, 0.0f);
        else if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GI_PROBE_VALIDITY)) DebugColor = Validity.xxx;
        else DebugColor = SkyVisibility.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SPECULAR_AA_ROUGHNESS))
    {
        DebugColor = Components.SpecularAARoughness.xxx;
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SPECULAR_ENERGY_COMPENSATION))
    {
        DebugColor = saturate((Components.SpecularEnergyCompensation - 1.0f) * 0.5f);
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_INDIRECT_SPECULAR_ENERGY_COMPENSATION))
    {
        DebugColor = saturate((Components.IndirectSpecularEnergyCompensation - 1.0f) * 0.5f);
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ENERGY_PRESERVATION))
    {
        DebugColor = Components.EnergyPreservation.xxx;
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SPECULAR_OCCLUSION))
    {
        DebugColor = Components.SpecularOcclusion.xxx;
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIRECT_BRDF_D))
    {
        DebugColor = saturate(Components.DirectBRDFD * 0.05f).xxx;
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIRECT_BRDF_VISIBILITY))
    {
        DebugColor = saturate(Components.DirectBRDFVisibility).xxx;
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIRECT_BRDF_FRESNEL))
    {
        DebugColor = saturate(Components.DirectBRDFFresnel);
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIRECT_DIFFUSE_LOBE))
    {
        DebugColor = saturate(Components.DirectDiffuseLobe).xxx;
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIRECT_DIFFUSE_BRDF))
    {
        DebugColor = saturate(Components.DirectDiffuseBRDF);
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIRECT_SPECULAR_BRDF))
    {
        DebugColor = saturate(Components.DirectSpecularBRDF);
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SPECULAR_AA_NORMAL_VARIANCE))
    {
        DebugColor = saturate(Components.SpecularAANormalVariance * 100.0f).xxx;
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SPECULAR_AA_ROUGHNESS_DELTA))
    {
        DebugColor = saturate(Components.SpecularAARoughnessDelta * 5.0f).xxx;
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_INDIRECT_SPECULAR_DFG))
    {
        DebugColor = saturate(float3(Components.IndirectSpecularDFG, 0.0f));
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_INDIRECT_SPECULAR_ENV_BRDF))
    {
        DebugColor = saturate(Components.IndirectSpecularEnvBRDF);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SUBSURFACE_PROFILE_ID))
    {
        DebugColor = saturate(Components.SubsurfaceProfileIndex / max(BURT_SHADING_DEBUG_SUBSURFACE_PROFILE_SLOT_COUNT - 1.0f, 1.0f)).xxx;
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SUBSURFACE_TRANSMISSION))
    {
        DebugColor = saturate(Components.SubsurfaceTransmission);
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SUBSURFACE_DIRECT_TRANSMISSION))
    {
        DebugColor = max(Components.SubsurfaceDirectTransmission, 0.0f);
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SUBSURFACE_TRANSMISSION_BRDF))
    {
        DebugColor = saturate(Components.SubsurfaceTransmissionBRDF);
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SUBSURFACE_TRANSMISSION_SHADOW))
    {
        DebugColor = Components.SubsurfaceTransmissionShadow.xxx;
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SUBSURFACE_TRANSMISSION_PHASE))
    {
        DebugColor = saturate(Components.SubsurfaceTransmissionPhase * (4.0f * BURT_PI)).xxx;
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SUBSURFACE_TRANSMISSION_THICKNESS))
    {
        DebugColor = saturate(Components.SubsurfaceTransmissionThickness * 0.1f).xxx;
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SUBSURFACE_KERNEL_WEIGHT))
    {
        DebugColor = saturate(Components.SubsurfaceKernelWeight * 4.0f);
        return true;
    }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SUBSURFACE_INDIRECT))
    {
        DebugColor = max(Components.SubsurfaceIndirect, 0.0f);
        return true;
    }

    float FoliageMask = saturate(max(Components.FoliageMask, BurtIsFoliageShadingModel(SurfaceData.ShadingModelID) ? 1.0f : 0.0f));
    float GrassMask = FoliageMask * saturate(SurfaceData.FoliageIsGrass);
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_FOLIAGE_TRANSMISSION)) { DebugColor = saturate(Components.FoliageTransmission) * FoliageMask; return true; }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_FOLIAGE_DIRECT_TRANSMISSION)) { DebugColor = max(Components.FoliageDirectTransmission, 0.0f) * FoliageMask; return true; }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_FOLIAGE_TRANSMISSION_BRDF)) { DebugColor = saturate(Components.FoliageTransmissionBRDF) * FoliageMask; return true; }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_FOLIAGE_TRANSMISSION_SHADOW)) { DebugColor = Components.FoliageTransmissionShadow.xxx * FoliageMask; return true; }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_FOLIAGE_SPECULAR_BRDF)) { DebugColor = saturate(Components.FoliageSpecularBRDF) * FoliageMask; return true; }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GRASS_TRANSMISSION)) { DebugColor = saturate(Components.FoliageTransmission) * GrassMask; return true; }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GRASS_DIRECT_TRANSMISSION)) { DebugColor = max(Components.FoliageDirectTransmission, 0.0f) * GrassMask; return true; }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GRASS_TRANSMISSION_BRDF)) { DebugColor = saturate(Components.FoliageTransmissionBRDF) * GrassMask; return true; }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GRASS_TRANSMISSION_SHADOW)) { DebugColor = Components.FoliageTransmissionShadow.xxx * GrassMask; return true; }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GRASS_SPECULAR_BRDF)) { DebugColor = saturate(Components.FoliageSpecularBRDF) * GrassMask; return true; }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_HAIR_PRIMARY_LOBE)) { DebugColor = saturate(Components.HairPrimaryLobe * 0.05f).xxx; return true; }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_HAIR_SECONDARY_LOBE)) { DebugColor = saturate(Components.HairSecondaryLobe * 0.25f).xxx; return true; }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_HAIR_TRANSMISSION_LOBE)) { DebugColor = saturate(Components.HairTransmissionLobe).xxx; return true; }
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_HAIR_SCATTER)) { DebugColor = Components.HairScatter.xxx; return true; }

    return false;
}

#endif // BURT_FORWARD_SHADING_DEBUG_INCLUDED
