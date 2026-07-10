#ifndef BURT_GBUFFER_HAIR_INCLUDED
#define BURT_GBUFFER_HAIR_INCLUDED

BurtSurfaceData BurtApplyHairGBufferSurfaceSemantics(BurtSurfaceData SurfaceData, float HairScatter, float HairShiftScale)
{
    SurfaceData.ShadingModelID = BURT_SHADING_MODEL_HAIR;
    SurfaceData.Metallic = BurtEncodeHairMaterialChannel(HairScatter, HairShiftScale);
    return SurfaceData;
}

BurtSurfaceData BurtApplyHairGBufferSurfaceSemantics(BurtSurfaceData SurfaceData, float HairScatter)
{
    return BurtApplyHairGBufferSurfaceSemantics(SurfaceData, HairScatter, 1.0f);
}

BurtGBufferData BurtCreateHairGBufferData(BurtSurfaceData SurfaceData, float3 StrandDirectionWS, float3 HairNormalWS, float3 HairGeometryNormalWS, float3 Emission)
{
    SurfaceData.ShadingModelID = BURT_SHADING_MODEL_HAIR;
    BurtGBufferData Data = BurtCreateGBufferData(SurfaceData, StrandDirectionWS, Emission);
    Data.ClearCoatNormalWS = BurtSafeNormalize(HairNormalWS);
    Data.HairGeometryNormalWS = BurtSafeNormalize(HairGeometryNormalWS);
    Data.HairSecondaryRoughness = ClampPerceptualRoughness(SurfaceData.HairSecondaryRoughness);
    Data.HairBackLight = saturate(SurfaceData.HairBackLight);
    Data.HairShadowFillStrength = saturate(SurfaceData.HairShadowFillStrength);
    Data.HairSpecularShift = clamp(SurfaceData.HairSpecularShift, BURT_HAIR_PRIMARY_SPECULAR_SHIFT_MIN, BURT_HAIR_PRIMARY_SPECULAR_SHIFT_MAX);
    Data.HairSecondarySpecularShift = clamp(SurfaceData.HairSecondarySpecularShift, BURT_HAIR_SECONDARY_SPECULAR_SHIFT_MIN, BURT_HAIR_SECONDARY_SPECULAR_SHIFT_MAX);
    Data.HairSpecularColor = max(SurfaceData.HairSpecularColor, float3(0.0f, 0.0f, 0.0f));
    Data.HairSecondarySpecularColor = max(SurfaceData.HairSecondarySpecularColor, float3(0.0f, 0.0f, 0.0f));
    return Data;
}

BurtGBufferData BurtCreateHairGBufferData(BurtSurfaceData SurfaceData, float3 StrandDirectionWS, float3 Emission)
{
    return BurtCreateHairGBufferData(SurfaceData, StrandDirectionWS, StrandDirectionWS, StrandDirectionWS, Emission);
}

float3 BurtGetHairStrandDirectionWS(BurtGBufferData GBufferData)
{
    return GBufferData.NormalWS;
}

float3 BurtGetHairShadingNormalWS(BurtGBufferData GBufferData)
{
    return BurtSafeNormalize(GBufferData.ClearCoatNormalWS);
}

float3 BurtGetHairGeometryNormalWS(BurtGBufferData GBufferData)
{
    return BurtSafeNormalize(GBufferData.HairGeometryNormalWS);
}

float BurtGetHairScatter(BurtGBufferData GBufferData)
{
    float HairScatter;
    float HairShiftScale;
    BurtDecodeHairMaterialChannel(GBufferData.MaterialChannel, HairScatter, HairShiftScale);
    return HairScatter;
}

float BurtGetHairLongitudinalShiftScale(BurtGBufferData GBufferData)
{
    float HairScatter;
    float HairShiftScale;
    BurtDecodeHairMaterialChannel(GBufferData.MaterialChannel, HairScatter, HairShiftScale);
    return HairShiftScale;
}

float BurtGetHairSecondaryRoughness(BurtGBufferData GBufferData)
{
    return ClampPerceptualRoughness(GBufferData.HairSecondaryRoughness);
}

float BurtGetHairBackLight(BurtGBufferData GBufferData)
{
    return saturate(GBufferData.HairBackLight);
}

float BurtGetHairShadowFillStrength(BurtGBufferData GBufferData)
{
    return saturate(GBufferData.HairShadowFillStrength);
}

float BurtGetHairSpecularShift(BurtGBufferData GBufferData)
{
    return clamp(GBufferData.HairSpecularShift, BURT_HAIR_PRIMARY_SPECULAR_SHIFT_MIN, BURT_HAIR_PRIMARY_SPECULAR_SHIFT_MAX);
}

float BurtGetHairSecondarySpecularShift(BurtGBufferData GBufferData)
{
    return clamp(GBufferData.HairSecondarySpecularShift, BURT_HAIR_SECONDARY_SPECULAR_SHIFT_MIN, BURT_HAIR_SECONDARY_SPECULAR_SHIFT_MAX);
}

float3 BurtGetHairSpecularColor(BurtGBufferData GBufferData)
{
    return max(GBufferData.HairSpecularColor, float3(0.0f, 0.0f, 0.0f));
}

float3 BurtGetHairSecondarySpecularColor(BurtGBufferData GBufferData)
{
    return max(GBufferData.HairSecondarySpecularColor, float3(0.0f, 0.0f, 0.0f));
}

#endif
