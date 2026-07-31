#ifndef BURT_DEFERRED_LIGHTING_GI_INCLUDED
#define BURT_DEFERRED_LIGHTING_GI_INCLUDED
Texture2D<float> _BurtScreenSpaceAmbientOcclusionTexture;
Texture2D<float> _BurtScreenSpaceShadowTexture;
Texture2D<float4> _BurtGIDiffuseIndirectTexture;
Texture2D<float4> _BurtGIBackfaceDiffuseIndirectTexture;
Texture2D<float4> _BurtGIRoughSpecularIndirectTexture;
Texture3D<float4> _BurtGITranslucencyVolume0;
Texture3D<float4> _BurtGITranslucencyVolume1;
float _BurtScreenSpaceAmbientOcclusionEnabled;
float _BurtScreenSpaceShadowEnabled;
float4 _BurtGIApplyIndirectParams; // x=diffuse enabled, y=diffuse/transmission intensity, z=backface enabled, w=rough-specular enabled.
float4 _BurtGIApplyIndirectParams1; // x=XRender character diffuse intensity, y=reserved/legacy diffuse boost, z=XGI screen ratio, w=ratio speed/debug.
float4 _BurtGIShortRangeAOParams; // x=enabled, y=weight, z=slope tolerance scale, w=radius pixels.
float4 _BurtGITranslucencyVolumeParams; // x=enabled, y=intensity, z=grazing power, w=backface mix.
float4 _BurtGITranslucencyVolumeGridSize; // xyz=volume grid size, w=apply scale.
float4 _BurtGITranslucencyVolumeGridZParams; // x=log scale, y=log bias, z=slice scale.
float4 _BurtGITranslucencyVolumeParams0; // x=near, y=far, z=depth fade power, w=screen-probe blend.

float3 BurtComputeDeferredGITranslucencyVolumeUV(float3 PositionWS)
{
    float4 ClipPosition = mul(_BurtDeferredCurrentNonJitteredViewProjectionMatrix, float4(PositionWS, 1.0f));
    float SafeW = abs(ClipPosition.w) > BURT_EPSILON ? ClipPosition.w : (ClipPosition.w < 0.0f ? -BURT_EPSILON : BURT_EPSILON);
    float2 ClipXY = ClipPosition.xy / SafeW;
    float2 VolumeXY = ClipXY * 0.5f + 0.5f;
#if UNITY_UV_STARTS_AT_TOP
    VolumeXY.y = 1.0f - VolumeXY.y;
#endif
    float Slice = log2(max(SafeW, 0.00001f) * _BurtGITranslucencyVolumeGridZParams.x + _BurtGITranslucencyVolumeGridZParams.y) *
        _BurtGITranslucencyVolumeGridZParams.z / max(_BurtGITranslucencyVolumeGridSize.z, 1.0f);
    return saturate(float3(VolumeXY, Slice));
}

float4 BurtDeferredGITranslucencyVolumeDiffuseTransferSH2(float3 NormalWS)
{
    const float kSHBasis0 = 0.28209479177387814f;
    const float kSHBasis1 = 0.4886025119029199f;
    const float kPi = 3.14159265358979323846f;
    const float l0Scale = kPi;
    const float l1Scale = 2.0f * kPi / 3.0f;
    NormalWS = BurtSafeNormalize(NormalWS);
    return float4(
        kSHBasis0 * l0Scale,
        -kSHBasis1 * NormalWS.y * l1Scale,
        kSHBasis1 * NormalWS.z * l1Scale,
        -kSHBasis1 * NormalWS.x * l1Scale);
}

float3 BurtDecodeDeferredGITranslucencyVolumeDiffuseSH2(float3 AmbientLightingVector, float3 DirectionalLightingVector, float3 NormalWS)
{
    float3 NormalizedAmbientColor = AmbientLightingVector /
        (dot(AmbientLightingVector, float3(0.2126f, 0.7152f, 0.0722f)) + 0.00001f);
    float4 DiffuseTransferSH = BurtDeferredGITranslucencyVolumeDiffuseTransferSH2(NormalWS);
    float3 Diffuse = AmbientLightingVector * DiffuseTransferSH.x +
        NormalizedAmbientColor * dot(DirectionalLightingVector, DiffuseTransferSH.yzw);
    return max(Diffuse * 0.31830988618f, float3(0.0f, 0.0f, 0.0f));
}

float BurtSampleDeferredScreenSpaceAmbientOcclusion(float2 ScreenUV)
{
    if (_BurtScreenSpaceAmbientOcclusionEnabled < 0.5f)
    {
        return 1.0f;
    }

    int2 TextureSize = max((int2)_BurtDeferredScreenSize.xy, int2(1, 1));
    int2 PixelCoord = clamp((int2)floor(ScreenUV * (float2)TextureSize), int2(0, 0), TextureSize - 1);
    float AO = _BurtScreenSpaceAmbientOcclusionTexture.Load(int3(PixelCoord, 0));
    return saturate(AO);
}

float BurtResolveDeferredMaterialScreenSpaceAmbientOcclusion(float2 ScreenUV, BurtGBufferData GBufferData)
{
    float ScreenSpaceAmbientOcclusion = BurtSampleDeferredScreenSpaceAmbientOcclusion(ScreenUV);
#if defined(BURT_DEFERRED_SHADING_MODEL_FOLIAGE)
    return saturate(lerp(1.0f, ScreenSpaceAmbientOcclusion, max(BurtGetFoliageScreenSpaceShadowIntensity(GBufferData), 0.0f)));
#else
    return ScreenSpaceAmbientOcclusion;
#endif
}

float BurtSampleDeferredScreenSpaceShadow(float2 ScreenUV)
{
    if (_BurtScreenSpaceShadowEnabled < 0.5f)
    {
        return 1.0f;
    }

    return saturate(BURT_SAMPLE_TEXTURE2D_LOD_CLAMP(_BurtScreenSpaceShadowTexture, ScreenUV, 0.0f));
}

float BurtResolveDeferredMaterialScreenSpaceShadow(float2 ScreenUV, BurtGBufferData GBufferData)
{
    float ScreenSpaceShadow = BurtSampleDeferredScreenSpaceShadow(ScreenUV);
#if defined(BURT_DEFERRED_SHADING_MODEL_FOLIAGE)
    return saturate(lerp(1.0f, ScreenSpaceShadow, max(BurtGetFoliageScreenSpaceShadowIntensity(GBufferData), 0.0f)));
#else
    return ScreenSpaceShadow;
#endif
}

float BurtResolveDeferredMaterialFoliageMicroShadow(BurtGBufferData GBufferData)
{
#if defined(BURT_DEFERRED_SHADING_MODEL_FOLIAGE)
    return saturate(GBufferData.Occlusion);
#else
    return 1.0f;
#endif
}

float BurtResolveDeferredGIXGIScreenRatioMask(float2 ScreenUV)
{
    float ScreenRatio = saturate(_BurtGIApplyIndirectParams1.z);
    return ScreenUV.x <= ScreenRatio ? 1.0f : 0.0f;
}

float3 BurtSampleDeferredGIDiffuseIndirect(float2 ScreenUV)
{
    float3 ScreenSpaceDiffuse = 0.0f;
    if (_BurtGIApplyIndirectParams.x >= 0.5f && BurtResolveDeferredGIXGIScreenRatioMask(ScreenUV) > 0.5f)
    {
        ScreenSpaceDiffuse = max(BURT_SAMPLE_TEXTURE2D_CLAMP(_BurtGIDiffuseIndirectTexture, ScreenUV).rgb, 0.0f) * max(_BurtGIApplyIndirectParams.y, 0.0f);
    }

    return ScreenSpaceDiffuse;
}

float3 BurtSampleDeferredGIBackfaceDiffuseIndirect(float2 ScreenUV)
{
    if (_BurtGIApplyIndirectParams.z < 0.5f || BurtResolveDeferredGIXGIScreenRatioMask(ScreenUV) <= 0.5f)
    {
        return 0.0f;
    }

    return max(BURT_SAMPLE_TEXTURE2D_CLAMP(_BurtGIBackfaceDiffuseIndirectTexture, ScreenUV).rgb, 0.0f) * max(_BurtGIApplyIndirectParams.y, 0.0f);
}

float3 BurtSampleDeferredGIRoughSpecularIndirect(float2 ScreenUV)
{
    if (_BurtGIApplyIndirectParams.w < 0.5f || BurtResolveDeferredGIXGIScreenRatioMask(ScreenUV) <= 0.5f)
    {
        return 0.0f;
    }

    return max(BURT_SAMPLE_TEXTURE2D_CLAMP(_BurtGIRoughSpecularIndirectTexture, ScreenUV).rgb, 0.0f);
}

float BurtDeferredGIRoughSpecularSmoothReflectionFade(float PerceptualRoughness)
{
    const float SpecularReflectionsRoughnessMask = 0.6f;
    return saturate(PerceptualRoughness * (-2.0f / SpecularReflectionsRoughnessMask) + 2.0f);
}

float BurtResolveDeferredGIXGICharacterIntensity(BurtGBufferData GBufferData)
{
    bool IsCharacterLike =
        BurtIsActiveHairShadingModel(GBufferData.ShadingModelID) ||
        BurtIsActiveSubsurfaceShadingModel(GBufferData.ShadingModelID) ||
        BurtIsActiveFoliageShadingModel(GBufferData.ShadingModelID) ||
        BurtIsActiveEyeShadingModel(GBufferData.ShadingModelID);
    return IsCharacterLike ? max(_BurtGIApplyIndirectParams1.x, 0.0f) : 1.0f;
}

float3 BurtResolveDeferredGIXGIDiffuseColor(float3 DiffuseColor)
{
    return max(DiffuseColor, float3(0.0f, 0.0f, 0.0f));
}

float2 BurtDeferredGIShortRangeAODirection(int Index)
{
    float2 Direction = 0.0f;
    if (Index == 0) Direction = float2(1.0f, 0.0f);
    if (Index == 1) Direction = float2(-1.0f, 0.0f);
    if (Index == 2) Direction = float2(0.0f, 1.0f);
    if (Index == 3) Direction = float2(0.0f, -1.0f);
    if (Index == 4) Direction = float2(1.0f, 1.0f);
    if (Index == 5) Direction = float2(-1.0f, 1.0f);
    if (Index == 6) Direction = float2(1.0f, -1.0f);
    if (Index == 7) Direction = float2(-1.0f, -1.0f);
    return Direction;
}

float BurtResolveDeferredGIShortRangeAOWeight()
{
    return _BurtGIShortRangeAOParams.x >= 0.5f ? saturate(_BurtGIShortRangeAOParams.y) : 0.0f;
}

float3 BurtResolveDeferredGIMaterialShortRangeAO(BurtGBufferData GBufferData)
{
    float Weight = BurtResolveDeferredGIShortRangeAOWeight();
    float3 MaterialAO = BurtGTAOMultiBounce(GBufferData.Occlusion, GBufferData.BaseColor);
    return lerp(float3(1.0f, 1.0f, 1.0f), MaterialAO, Weight);
}

float BurtResolveDeferredGIShortRangeAO(float2 ScreenUV, BurtGBufferData GBufferData)
{
    float Weight = BurtResolveDeferredGIShortRangeAOWeight();
    if (Weight <= 0.0001f)
    {
        return 1.0f;
    }

    float CenterRawDepth = BurtSampleDeferredRawDepth(ScreenUV);
    float CenterLinearDepth = max(LinearEyeDepth(CenterRawDepth), 0.0001f);
    float3 CenterNormalWS = BurtGetDeferredSurfaceNormalWS(GBufferData);
    float2 Texel = max(_BurtDeferredScreenSize.zw, float2(1.0f / 8192.0f, 1.0f / 8192.0f));
    float RadiusPixels = max(_BurtGIShortRangeAOParams.w, 0.5f);
    float SlopeToleranceScale = max(_BurtGIShortRangeAOParams.z, 0.05f);
    float DepthTolerance = max(CenterLinearDepth * 0.0125f * SlopeToleranceScale, 0.015f);
    float OcclusionSum = 0.0f;
    float WeightSum = 0.0f;

    [unroll(8)]
    for (int Index = 0; Index < 8; ++Index)
    {
        float2 Direction = BurtDeferredGIShortRangeAODirection(Index);
        float2 SampleUV = saturate(ScreenUV + Direction * Texel * RadiusPixels);
        float SampleRawDepth = BurtSampleDeferredRawDepth(SampleUV);
        float SampleLinearDepth = LinearEyeDepth(SampleRawDepth);
        float FrontDepth = CenterLinearDepth - SampleLinearDepth;
        float FrontOcclusion = saturate(FrontDepth / DepthTolerance);
        BurtGBufferData SampleGBufferData = BurtDecodeGBuffer(BurtSampleEncodedGBuffer(SampleUV));
        float NormalMismatch = saturate(1.0f - dot(CenterNormalWS, BurtGetDeferredSurfaceNormalWS(SampleGBufferData)));
        float RingWeight = Index < 4 ? 1.0f : 0.75f;
        float SampleOcclusion = FrontOcclusion * lerp(0.35f, 1.0f, sqrt(NormalMismatch));
        OcclusionSum += SampleOcclusion * RingWeight;
        WeightSum += RingWeight;
    }

    float Occlusion = saturate(OcclusionSum / max(WeightSum, 0.0001f));
    return saturate(1.0f - Occlusion * Weight);
}

float BurtDeferredGIBackfaceDiffuseBlend(BurtGBufferData GBufferData)
{
#if defined(BURT_DEFERRED_SHADING_MODEL_SUBSURFACE)
    float Thickness = saturate(BurtGetSubsurfaceThickness(GBufferData));
    float Ambient = saturate(BurtGetSubsurfaceAmbient(GBufferData));
    float Strength = saturate(BurtGetSubsurfaceStrength(GBufferData));
    return saturate(max(Strength, max(Thickness, Ambient)));
#elif defined(BURT_DEFERRED_SHADING_MODEL_FOLIAGE)
    if (BurtGetFoliageIsGrass(GBufferData) > 0.5f)
    {
        return 0.0f;
    }

    float Transmission = max(BurtGetFoliageTransmissionWeight(GBufferData), max(BurtGetFoliageThickness(GBufferData), BurtGetFoliageBackLight(GBufferData)));
    return saturate(Transmission);
#elif defined(BURT_DEFERRED_SHADING_MODEL_HAIR)
    return saturate(max(BurtGetHairBackLight(GBufferData), BurtGetHairScatter(GBufferData) * 0.35f));
#elif defined(BURT_DEFERRED_SHADING_MODEL_FUR)
    return 0.0f;
#else
    return 0.0f;
#endif
}

float BurtDeferredGITranslucencyVolumeBlend(BurtGBufferData GBufferData)
{
#if defined(BURT_DEFERRED_SHADING_MODEL_SUBSURFACE)
    float Thickness = saturate(BurtGetSubsurfaceThickness(GBufferData));
    float Ambient = saturate(BurtGetSubsurfaceAmbient(GBufferData));
    float Strength = saturate(BurtGetSubsurfaceStrength(GBufferData));
    return saturate(max(Strength, max(Thickness, Ambient)));
#elif defined(BURT_DEFERRED_SHADING_MODEL_FOLIAGE)
    float Transmission = max(BurtGetFoliageTransmissionWeight(GBufferData), max(BurtGetFoliageThickness(GBufferData), BurtGetFoliageBackLight(GBufferData)));
    return saturate(Transmission);
#elif defined(BURT_DEFERRED_SHADING_MODEL_HAIR)
    return 0.35f;
#else
    return 0.0f;
#endif
}

float3 BurtDeferredGIBackfaceTransmissionColor(BurtGBufferData GBufferData, BurtPBRShadingComponents Components)
{
#if defined(BURT_DEFERRED_SHADING_MODEL_SUBSURFACE)
    return max(GBufferData.BaseColor, float3(0.0f, 0.0f, 0.0f));
#elif defined(BURT_DEFERRED_SHADING_MODEL_FOLIAGE)
    return max(BurtGetFoliageTransmissionColor(GBufferData), float3(0.0f, 0.0f, 0.0f));
#elif defined(BURT_DEFERRED_SHADING_MODEL_HAIR)
    return max(Components.DiffuseColor, float3(0.0f, 0.0f, 0.0f));
#else
    return float3(0.0f, 0.0f, 0.0f);
#endif
}

float3 BurtResolveDeferredGITranslucencyVolumeDiffuseLite(float2 ScreenUV, BurtGBufferData GBufferData, out float VolumeConfidence)
{
    VolumeConfidence = 0.0f;
    if (_BurtGITranslucencyVolumeParams.x < 0.5f)
    {
        return 0.0f;
    }

    float MaterialWeight = BurtDeferredGITranslucencyVolumeBlend(GBufferData);
    if (MaterialWeight <= 0.0001f)
    {
        return 0.0f;
    }

    float RawDepth = BurtSampleDeferredRawDepth(ScreenUV);
    float3 PositionWS = BurtReconstructDeferredNonJitteredPositionWS(ScreenUV, RawDepth);
    float3 VolumeUV = BurtComputeDeferredGITranslucencyVolumeUV(PositionWS);
    float4 Volume0 = _BurtGITranslucencyVolume0.SampleLevel(sampler_TriLinearClamp, VolumeUV, 0.0f);
    float4 Volume1 = _BurtGITranslucencyVolume1.SampleLevel(sampler_TriLinearClamp, VolumeUV, 0.0f);
    VolumeConfidence = saturate(max(Volume0.a, Volume1.a)) * MaterialWeight;
    float3 NormalWS = BurtGetDeferredSurfaceNormalWS(GBufferData);
    return BurtDecodeDeferredGITranslucencyVolumeDiffuseSH2(Volume0.rgb, Volume1.rgb, NormalWS);
}

float3 BurtResolveDeferredGITranslucencyVolumeLite(float2 ScreenUV, BurtGBufferData GBufferData, float3 ViewDirectionWS)
{
    if (_BurtGITranslucencyVolumeParams.x < 0.5f)
    {
        return 0.0f;
    }

    float MaterialWeight = BurtDeferredGITranslucencyVolumeBlend(GBufferData);
    if (MaterialWeight <= 0.0001f)
    {
        return 0.0f;
    }

    float3 NormalWS = BurtGetDeferredSurfaceNormalWS(GBufferData);
    float NDotV = saturate(abs(dot(NormalWS, BurtSafeNormalize(ViewDirectionWS))));
    float GrazingWrap = pow(saturate(1.0f - NDotV), max(_BurtGITranslucencyVolumeParams.z, 0.05f));
    float ViewWeight = lerp(0.35f, 1.0f, GrazingWrap);
    float3 BackfaceRadiance = max(BURT_SAMPLE_TEXTURE2D_CLAMP(_BurtGIBackfaceDiffuseIndirectTexture, ScreenUV).rgb, 0.0f);
    float RawDepth = BurtSampleDeferredRawDepth(ScreenUV);
    float3 PositionWS = BurtReconstructDeferredNonJitteredPositionWS(ScreenUV, RawDepth);
    float3 VolumeUV = BurtComputeDeferredGITranslucencyVolumeUV(PositionWS);
    float4 Volume0 = _BurtGITranslucencyVolume0.SampleLevel(sampler_TriLinearClamp, VolumeUV, 0.0f);
    float4 Volume1 = _BurtGITranslucencyVolume1.SampleLevel(sampler_TriLinearClamp, VolumeUV, 0.0f);
    float VolumeConfidence = saturate(max(Volume0.a, Volume1.a));
    float3 VolumeRadiance = BurtDecodeDeferredGITranslucencyVolumeDiffuseSH2(Volume0.rgb, Volume1.rgb, NormalWS);
    float3 BackfaceFallbackRadiance = _BurtGIApplyIndirectParams.z >= 0.5f
        ? BackfaceRadiance * max(_BurtGITranslucencyVolumeParams.w, 0.0f)
        : VolumeRadiance;
    float3 SourceRadiance = lerp(BackfaceFallbackRadiance, VolumeRadiance, VolumeConfidence);
    return SourceRadiance * MaterialWeight * ViewWeight * max(_BurtGIApplyIndirectParams.y, 0.0f) * max(_BurtGITranslucencyVolumeParams.y, 0.0f);
}

void BurtApplyDeferredGIIndirect(float2 ScreenUV, BurtGBufferData GBufferData, float3 ViewDirectionWS, inout BurtPBRShadingComponents Components)
{
    float RawDepth = BurtSampleDeferredRawDepth(ScreenUV);
    float3 PositionWS = BurtReconstructDeferredPositionWS(ScreenUV, RawDepth);
    float3 ProbeVolumeIrradiance;
    if (BurtTrySampleGIProbeVolumeIrradiance(PositionWS, BurtGetDeferredSurfaceNormalWS(GBufferData), ViewDirectionWS, ProbeVolumeIrradiance))
    {
        Components.IndirectDiffuse = Components.DiffuseColor * ProbeVolumeIrradiance * BurtGTAOMultiBounce(GBufferData.Occlusion, GBufferData.BaseColor) * saturate(Components.EnergyPreservation);
    }

    float3 DiffuseIndirect = BurtSampleDeferredGIDiffuseIndirect(ScreenUV);
    float3 BackfaceDiffuseIndirect = BurtSampleDeferredGIBackfaceDiffuseIndirect(ScreenUV);
    float3 RoughSpecularIndirect = BurtSampleDeferredGIRoughSpecularIndirect(ScreenUV);
    // XRender parity: Translucency Volume is sampled only by MATERIAL_USE_TRANSPARENT.
    // Deferred lighting shades opaque receivers, so mixing the froxel volume here creates
    // low-frequency light patches on skin, hair and foliage.
    float3 MaterialShortRangeAO = BurtResolveDeferredGIMaterialShortRangeAO(GBufferData);
    float EnergyPreservation = saturate(Components.EnergyPreservation);
    float3 XGIDiffuseColor = BurtResolveDeferredGIXGIDiffuseColor(Components.DiffuseColor);
    DiffuseIndirect *= BurtResolveDeferredGIXGICharacterIntensity(GBufferData);
    DiffuseIndirect *= MaterialShortRangeAO;
    DiffuseIndirect *= XGIDiffuseColor * EnergyPreservation;
    float BackfaceDiffuseBlend = BurtDeferredGIBackfaceDiffuseBlend(GBufferData);
    float3 BackfaceTransmissionIndirect = BackfaceDiffuseIndirect * BackfaceDiffuseBlend;
    BackfaceTransmissionIndirect *= BurtDeferredGIBackfaceTransmissionColor(GBufferData, Components);
    BackfaceTransmissionIndirect *= MaterialShortRangeAO;
    BackfaceTransmissionIndirect *= EnergyPreservation;

    float3 SubsurfaceIndirectTransmission = max(Components.SubsurfaceIndirectTransmission, float3(0.0f, 0.0f, 0.0f));
    float3 SubsurfaceIndirectTransmissionForLighting = SubsurfaceIndirectTransmission;
#if defined(BURT_DEFERRED_SHADING_MODEL_SUBSURFACE)
    if (BurtGetSubsurfaceStrength(GBufferData) > 0.0001f &&
        !BurtIsSubsurface3SPreIntegratedMode(BurtGetSubsurfaceScatteringMode(GBufferData)))
    {
        SubsurfaceIndirectTransmissionForLighting = float3(0.0f, 0.0f, 0.0f);
    }
#endif
    Components.SubsurfaceIndirectTransmission = SubsurfaceIndirectTransmission + BackfaceTransmissionIndirect;
    Components.IndirectDiffuse += DiffuseIndirect;
    if (_BurtGIApplyIndirectParams.w >= 0.5f && any(RoughSpecularIndirect > 0.0001f))
    {
        float SmoothReflectionFade = BurtDeferredGIRoughSpecularSmoothReflectionFade(GBufferData.PerceptualRoughness);
        Components.IndirectSpecular = lerp(RoughSpecularIndirect, Components.IndirectSpecular, SmoothReflectionFade);
    }
    Components.SubsurfaceIndirect = Components.IndirectDiffuse;
    Components.IndirectLighting = Components.IndirectDiffuse + Components.IndirectSpecular + SubsurfaceIndirectTransmissionForLighting + BackfaceTransmissionIndirect;
    Components.Lighting = Components.DirectLighting + Components.IndirectLighting;
}
#endif
