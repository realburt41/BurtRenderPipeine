// Ambient, sky/SH, probe-volume GI, and image-based indirect lighting.
#ifndef BURT_LIGHTING_INDIRECT_INCLUDED
#define BURT_LIGHTING_INDIRECT_INCLUDED

#ifndef BURT_SUBSURFACE_3S_SH_IRRADIANCE_WEIGHT
#define BURT_SUBSURFACE_3S_SH_IRRADIANCE_WEIGHT (0.0f)
#endif
float4 _BurtAmbientLightColor;
Texture3D<float4> _BurtGIProbeVolumeIrradianceTexture;
float4 _BurtGIProbeVolumeCenterExtent;
float4 _BurtGIProbeVolumeParams;
StructuredBuffer<uint> _BurtGIProbeVolumeVirtualPageTable;
StructuredBuffer<uint3> _BurtGIProbeVolumeVirtualIndirection;
Texture3D<float4> _BurtGIProbeVolumeVirtualL0L1Rx;
Texture3D<float4> _BurtGIProbeVolumeVirtualL1GL1Ry;
Texture3D<float4> _BurtGIProbeVolumeVirtualL1BL1Rz;
Texture3D<float4> _BurtGIProbeVolumeVirtualL20;
Texture3D<float4> _BurtGIProbeVolumeVirtualL21;
Texture3D<float4> _BurtGIProbeVolumeVirtualL22;
Texture3D<float4> _BurtGIProbeVolumeVirtualL23;
Texture3D<float4> _BurtGIProbeVolumeVirtualSkyVisibilityL0L1;
Texture3D<float4> _BurtGIProbeVolumeVirtualSkyShadingDirectionIndices;
StructuredBuffer<float3> _BurtGIProbeVolumeVirtualSkyShadingDirections;
float4 _BurtGIProbeVolumeVirtualPosOffsetMinBrickSize;
float4 _BurtGIProbeVolumeVirtualIndirectionDimensions;
float4 _BurtGIProbeVolumeVirtualMinLoadedEntry;
float4 _BurtGIProbeVolumeVirtualMaxLoadedEntry;
float4 _BurtGIProbeVolumeVirtualMinEntryIndexEntrySize;
float4 _BurtGIProbeVolumeVirtualPhysicalPoolDimensions;
float4 _BurtGIProbeVolumeVirtualPhysicalPoolDimensionsRcp;
float4 _BurtGIProbeVolumeVirtualBiasL2;
float4 _BurtGIProbeVolumeVirtualSkyVisibilityParams;
float _BurtGIProbeVolumeVirtualSkyShadingDirectionEnabled;
float4 _BurtGIProbeVolumeVirtualBufferCounts;

float4 _BurtAmbientSHAr;

float4 _BurtAmbientSHAg;

float4 _BurtAmbientSHAb;

float4 _BurtAmbientSHBr;

float4 _BurtAmbientSHBg;

float4 _BurtAmbientSHBb;

float4 _BurtAmbientSHC;

float _BurtAmbientSHEnabled;

TextureCube _BurtSkyReflectionTexture;
TextureCube _BurtSkyReflectionSourceTexture;
TextureCube _BurtSkyDiffuseCubemapTexture;
Texture2D _BurtSkyDiffuseSHTexture;

float4 _BurtSkyReflectionHDR;

float _BurtSkyReflectionIntensity;

// Optional tint supplied by BurtSkyLight; legacy RenderSettings path uploads white.
float4 _BurtSkyReflectionTint;

float _BurtSkyReflectionEnabled;

float _BurtSkyReflectionOverride;

float _BurtSkyReflectionMaxMip;
float4 _BurtSkyReflectionSourceHDR;
float _BurtSkyReflectionSourceEnabled;
float _BurtSkyReflectionSourceMaxMip;

float4 _BurtSkyReflectionRotation;

float4 _BurtSkyDiffuseCubemapHDR;
float _BurtSkyDiffuseCubemapEnabled;
float _BurtSkyDiffuseSHEnabled;
float _BurtSkyDiffuseCubemapIntensity;
float4 _BurtSkyDiffuseCubemapTint;
float _BurtSkyDiffuseCubemapMip;

float _BurtSkyLowerHemisphereEnabled;
float4 _BurtSkyLowerHemisphereDiffuseColor;
float4 _BurtSkyLowerHemisphereSpecularColor;

float3 BurtGetAmbientLightColor()
{
    return max(_BurtAmbientLightColor.rgb, float3(0.0f, 0.0f, 0.0f));
}

float BurtLuminanceForIndirectFallback(float3 color)
{
    float3 safeColor = max(color, float3(0.0f, 0.0f, 0.0f));
    return dot(safeColor, float3(0.2126f, 0.7152f, 0.0722f));
}

float3 BurtSelectIndirectFallbackIfBlack(float3 sampledColor, float3 fallbackColor)
{
    float sampledLuminance = BurtLuminanceForIndirectFallback(sampledColor);
    float useSampledColor = step(0.0001f, sampledLuminance);
    return lerp(max(fallbackColor, float3(0.0f, 0.0f, 0.0f)), max(sampledColor, float3(0.0f, 0.0f, 0.0f)), useSampledColor);
}

float3 BurtApplySkyLowerHemisphere(float3 sourceColor, float3 directionWS, float4 lowerHemisphereColor)
{
    float lowerBlend = (_BurtSkyLowerHemisphereEnabled > 0.5f && BurtSafeNormalize(directionWS).y < 0.0f) ? saturate(lowerHemisphereColor.a) : 0.0f;
    return lerp(max(sourceColor, float3(0.0f, 0.0f, 0.0f)), max(lowerHemisphereColor.rgb, float3(0.0f, 0.0f, 0.0f)), lowerBlend);
}

float3 BurtRotateSkyReflectionDirection(float3 directionWS)
{
    float3 SafeDirectionWS = BurtSafeNormalize(directionWS);
    float CosPhi = _BurtSkyReflectionRotation.x;
    float SinPhi = _BurtSkyReflectionRotation.y;
    float3 RotDirX = float3(CosPhi, 0.0f, -SinPhi);
    float3 RotDirZ = float3(SinPhi, 0.0f, CosPhi);
    return BurtSafeNormalize(float3(dot(RotDirX, SafeDirectionWS), SafeDirectionWS.y, dot(RotDirZ, SafeDirectionWS)));
}

float3 BurtSampleSkyDiffuseCubemap(float3 normalWS)
{
    float3 SafeNormalWS = BurtSafeNormalize(normalWS);
    float3 SkySampleDirectionWS = BurtRotateSkyReflectionDirection(SafeNormalWS);
    float4 EncodedSkyDiffuse = BURT_SAMPLE_TEXTURECUBE_LOD_CLAMP(_BurtSkyDiffuseCubemapTexture, SkySampleDirectionWS, max(_BurtSkyDiffuseCubemapMip, 0.0f));
    float3 SkyDiffuse = DecodeHDR(EncodedSkyDiffuse, _BurtSkyDiffuseCubemapHDR) * max(_BurtSkyDiffuseCubemapTint.rgb, float3(0.0f, 0.0f, 0.0f)) * max(_BurtSkyDiffuseCubemapIntensity, 0.0f);
    if (_BurtSkyReflectionSourceEnabled > 0.5f)
    {
        float SourceDiffuseMip = max(_BurtSkyReflectionSourceMaxMip, 0.0f);
        float4 EncodedSourceDiffuse = BURT_SAMPLE_TEXTURECUBE_LOD_CLAMP(_BurtSkyReflectionSourceTexture, SkySampleDirectionWS, SourceDiffuseMip);
        float3 SourceDiffuse = DecodeHDR(EncodedSourceDiffuse, _BurtSkyReflectionSourceHDR) * max(_BurtSkyDiffuseCubemapTint.rgb, float3(0.0f, 0.0f, 0.0f)) * max(_BurtSkyDiffuseCubemapIntensity, 0.0f);
        SkyDiffuse = BurtSelectIndirectFallbackIfBlack(SkyDiffuse, SourceDiffuse);
    }

    SkyDiffuse = BurtApplySkyLowerHemisphere(SkyDiffuse, SafeNormalWS, _BurtSkyLowerHemisphereDiffuseColor);
    return max(SkyDiffuse, float3(0.0f, 0.0f, 0.0f));
}

float4 BurtSampleSkyDiffuseSHPacked(float index)
{
    float u = (index + 0.5f) / 7.0f;
    return BURT_SAMPLE_TEXTURE2D_LOD_CLAMP(_BurtSkyDiffuseSHTexture, float2(u, 0.5f), 0.0f);
}

float3 BurtEvaluateSkyDiffuseSH9(float3 normalWS)
{
    float3 SafeNormalWS = BurtSafeNormalize(normalWS);
    float3 SkySampleDirectionWS = BurtRotateSkyReflectionDirection(SafeNormalWS);
    float4 SHAr = BurtSampleSkyDiffuseSHPacked(0.0f);
    float4 SHAg = BurtSampleSkyDiffuseSHPacked(1.0f);
    float4 SHAb = BurtSampleSkyDiffuseSHPacked(2.0f);
    float4 SHBr = BurtSampleSkyDiffuseSHPacked(3.0f);
    float4 SHBg = BurtSampleSkyDiffuseSHPacked(4.0f);
    float4 SHBb = BurtSampleSkyDiffuseSHPacked(5.0f);
    float4 SHC = BurtSampleSkyDiffuseSHPacked(6.0f);
    float4 SHNormal = float4(SkySampleDirectionWS, 1.0f);

    float3 LinearL0L1;
    LinearL0L1.r = dot(SHAr, SHNormal);
    LinearL0L1.g = dot(SHAg, SHNormal);
    LinearL0L1.b = dot(SHAb, SHNormal);

    float4 VB = SHNormal.xyzz * SHNormal.yzzx;
    float3 LinearL2;
    LinearL2.r = dot(SHBr, VB);
    LinearL2.g = dot(SHBg, VB);
    LinearL2.b = dot(SHBb, VB);

    float VC = SkySampleDirectionWS.x * SkySampleDirectionWS.x - SkySampleDirectionWS.y * SkySampleDirectionWS.y;
    float3 SHIrradiance = (LinearL0L1 + LinearL2 + SHC.rgb * VC) * max(_BurtSkyDiffuseCubemapTint.rgb, float3(0.0f, 0.0f, 0.0f)) * max(_BurtSkyDiffuseCubemapIntensity, 0.0f);
    SHIrradiance = BurtApplySkyLowerHemisphere(SHIrradiance, SafeNormalWS, _BurtSkyLowerHemisphereDiffuseColor);
    return max(SHIrradiance, float3(0.0f, 0.0f, 0.0f));
}

float3 BurtEvaluateAmbientSH9(float3 normalWS)
{
    float3 SafeNormalWS = BurtSafeNormalize(normalWS);
    float4 SHNormal = float4(SafeNormalWS, 1.0f);

    float3 LinearL0L1;
    LinearL0L1.r = dot(_BurtAmbientSHAr, SHNormal);
    LinearL0L1.g = dot(_BurtAmbientSHAg, SHNormal);
    LinearL0L1.b = dot(_BurtAmbientSHAb, SHNormal);

    float4 VB = SHNormal.xyzz * SHNormal.yzzx;
    float3 LinearL2;
    LinearL2.r = dot(_BurtAmbientSHBr, VB);
    LinearL2.g = dot(_BurtAmbientSHBg, VB);
    LinearL2.b = dot(_BurtAmbientSHBb, VB);

    float VC = SafeNormalWS.x * SafeNormalWS.x - SafeNormalWS.y * SafeNormalWS.y;
    float3 SHIrradiance = LinearL0L1 + LinearL2 + _BurtAmbientSHC.rgb * VC;
    SHIrradiance = BurtApplySkyLowerHemisphere(SHIrradiance, SafeNormalWS, _BurtSkyLowerHemisphereDiffuseColor);

#ifdef UNITY_COLORSPACE_GAMMA
    SHIrradiance = pow(max(SHIrradiance, float3(0.0f, 0.0f, 0.0f)), 1.0f / 2.2f);
#endif

    return max(SHIrradiance, float3(0.0f, 0.0f, 0.0f));
}

float BurtLambert(float3 NormalWS, float3 LightDirectionWS)
{
    return saturate(dot(NormalWS, LightDirectionWS));
}

float3 BurtEvaluateDiffuse(float3 BaseColor, BurtLight Light, float3 NormalWS)
{
    float DiffuseTerm = BurtLambert(NormalWS, Light.DirectionWS);

    return BaseColor * Light.Color * DiffuseTerm * Light.ShadowAttenuation;
}

float3 BurtEvaluateAmbientOccluded(float3 BaseColor, float3 AmbientColor, float Occlusion)
{
    return BaseColor * AmbientColor * saturate(Occlusion);
}

float3 BurtEvaluateAmbient(float3 BaseColor, float3 AmbientColor)
{
    return BurtEvaluateAmbientOccluded(BaseColor, AmbientColor, 1.0f);
}

float3 BurtSampleIndirectDiffuseIrradiance(float3 NormalWS)
{
    if (_BurtSkyDiffuseSHEnabled > 0.5f)
    {
        return BurtEvaluateSkyDiffuseSH9(NormalWS);
    }

    if (_BurtSkyDiffuseCubemapEnabled > 0.5f)
    {
        return BurtSampleSkyDiffuseCubemap(NormalWS);
    }

    if (_BurtAmbientSHEnabled > 0.5f)
    {
        return BurtEvaluateAmbientSH9(NormalWS);
    }

    float3 SafeNormalWS = BurtSafeNormalize(NormalWS);
    float4 SHNormal = float4(SafeNormalWS, 1.0f);
    float3 SHIrradiance = ShadeSH9(SHNormal);
SHIrradiance = BurtApplySkyLowerHemisphere(SHIrradiance, SafeNormalWS, _BurtSkyLowerHemisphereDiffuseColor);
    return BurtSelectIndirectFallbackIfBlack(SHIrradiance, BurtGetAmbientLightColor());
}


#ifndef BURT_REFLECTION_CAPTURE_SPECULAR_MIP_MAX_INDEX
#define BURT_REFLECTION_CAPTURE_SPECULAR_MIP_MAX_INDEX (8.0f)
#endif

float ComputeReflectionCaptureMipFromRoughness(float PerceptualRoughness, float CubemapMaxMipIndex)
{
float SpecularMipMaxIndex = min(max(CubemapMaxMipIndex, 0.0f), BURT_REFLECTION_CAPTURE_SPECULAR_MIP_MAX_INDEX);

    float SafeRoughness = max(saturate(PerceptualRoughness), BURT_MIN_PERCEPTUAL_ROUGHNESS);

float LevelFrom1x1 = 1.0f - 1.2f * log2(SafeRoughness);

return clamp(SpecularMipMaxIndex - 1.0f - LevelFrom1x1, 0.0f, SpecularMipMaxIndex);
}

float3 BurtSkyCubeFaceUVToDirection(float Face, float2 UV)
{
    float2 St = UV * 2.0f - 1.0f;
    if (Face < 0.5f) return BurtSafeNormalize(float3(1.0f, -St.y, -St.x));
    if (Face < 1.5f) return BurtSafeNormalize(float3(-1.0f, -St.y, St.x));
    if (Face < 2.5f) return BurtSafeNormalize(float3(St.x, 1.0f, St.y));
    if (Face < 3.5f) return BurtSafeNormalize(float3(St.x, -1.0f, -St.y));
    if (Face < 4.5f) return BurtSafeNormalize(float3(St.x, -St.y, 1.0f));
    return BurtSafeNormalize(float3(-St.x, -St.y, -1.0f));
}

void BurtSkyDirectionToCubeFaceUV(float3 DirectionWS, out float Face, out float2 UV)
{
    float3 Dir = BurtSafeNormalize(DirectionWS);
    float3 AbsDir = abs(Dir);

    if (AbsDir.x >= AbsDir.y && AbsDir.x >= AbsDir.z)
    {
        float InvAxis = rcp(max(AbsDir.x, BURT_EPSILON));
        if (Dir.x >= 0.0f)
        {
            Face = 0.0f;
            UV = float2(-Dir.z, -Dir.y) * InvAxis;
        }
        else
        {
            Face = 1.0f;
            UV = float2(Dir.z, -Dir.y) * InvAxis;
        }
    }
    else if (AbsDir.y >= AbsDir.z)
    {
        float InvAxis = rcp(max(AbsDir.y, BURT_EPSILON));
        if (Dir.y >= 0.0f)
        {
            Face = 2.0f;
            UV = float2(Dir.x, Dir.z) * InvAxis;
        }
        else
        {
            Face = 3.0f;
            UV = float2(Dir.x, -Dir.z) * InvAxis;
        }
    }
    else
    {
        float InvAxis = rcp(max(AbsDir.z, BURT_EPSILON));
        if (Dir.z >= 0.0f)
        {
            Face = 4.0f;
            UV = float2(Dir.x, -Dir.y) * InvAxis;
        }
        else
        {
            Face = 5.0f;
            UV = float2(-Dir.x, -Dir.y) * InvAxis;
        }
    }

    UV = UV * 0.5f + 0.5f;
}

float3 BurtApplySkyReflectionMipSeamScale(float3 DirectionWS, float MipLevel, float MaxMipIndex)
{
    float SafeMaxMip = max(MaxMipIndex, 0.0f);
    if (SafeMaxMip <= 0.5f)
    {
        return BurtSafeNormalize(DirectionWS);
    }

    float MipSize = exp2(max(SafeMaxMip - floor(max(MipLevel, 0.0f)), 0.0f));
    float MipScale = saturate((MipSize - 2.0f) / max(MipSize, 1.0f));
    float Face;
    float2 UV;
    BurtSkyDirectionToCubeFaceUV(DirectionWS, Face, UV);
    UV = (UV - 0.5f) * MipScale + 0.5f;
    return BurtSkyCubeFaceUVToDirection(Face, UV);
}

float3 SampleIndirectSpecularRadiance(float3 ReflectionDirectionWS, float Roughness)
{
    float3 SafeReflectionDirectionWS = BurtSafeNormalize(ReflectionDirectionWS);

    if (_BurtSkyReflectionEnabled > 0.5f)
    {
        float3 SkySampleDirectionWS = BurtRotateSkyReflectionDirection(SafeReflectionDirectionWS);
        float SkyReflectionMaxMip = max(_BurtSkyReflectionMaxMip, 0.0f);
        float SkyReflectionMipLevel = ComputeReflectionCaptureMipFromRoughness(Roughness, SkyReflectionMaxMip);
        float4 EncodedSkyReflection = BURT_SAMPLE_TEXTURECUBE_LOD_CLAMP(_BurtSkyReflectionTexture, SkySampleDirectionWS, SkyReflectionMipLevel);
        float3 SkyReflectionRadiance = DecodeHDR(EncodedSkyReflection, _BurtSkyReflectionHDR) * max(_BurtSkyReflectionTint.rgb, float3(0.0f, 0.0f, 0.0f)) * max(0.0f, _BurtSkyReflectionIntensity);
        return max(SkyReflectionRadiance, float3(0.0f, 0.0f, 0.0f));
    }

    if (_BurtSkyReflectionOverride > 0.5f)
    {
        return BurtApplySkyLowerHemisphere(float3(0.0f, 0.0f, 0.0f), SafeReflectionDirectionWS, _BurtSkyLowerHemisphereSpecularColor);
    }

    const float LegacyUnitySpecCubeMaxMip = 6.0f;
    float LegacyUnitySpecCubeMipLevel = ComputeReflectionCaptureMipFromRoughness(Roughness, LegacyUnitySpecCubeMaxMip);
float4 EncodedSpecular = BURT_SAMPLE_TEXTURECUBE_LOD_CLAMP(unity_SpecCube0, SafeReflectionDirectionWS, LegacyUnitySpecCubeMipLevel);

    float3 SpecularRadiance = DecodeHDR(EncodedSpecular, unity_SpecCube0_HDR);

    return BurtSelectIndirectFallbackIfBlack(SpecularRadiance, BurtGetAmbientLightColor());
}

float3 BurtEvaluateIndirectDiffusePBR(BurtPBRMaterialData MaterialData, float3 NormalWS, float EnergyPreservation)
{
    float3 DiffuseIrradiance = BurtSampleIndirectDiffuseIrradiance(NormalWS);

    return MaterialData.DiffuseColor * DiffuseIrradiance * BurtGTAOMultiBounce(MaterialData.Occlusion, MaterialData.BaseColor) * saturate(EnergyPreservation);
}

bool BurtTrySampleGIProbeVolumeVirtualIrradiance(float3 PositionWS, float3 NormalWS, float3 ViewDirectionWS, out float3 Irradiance)
{
    Irradiance = 0.0f;
    if (_BurtGIProbeVolumeParams.w < 1.5f)
    {
        return false;
    }

    float3 BiasedPositionWS = PositionWS - _BurtGIProbeVolumeVirtualPosOffsetMinBrickSize.xyz;
    BiasedPositionWS += NormalWS * _BurtGIProbeVolumeVirtualBiasL2.x;
    BiasedPositionWS += ViewDirectionWS * _BurtGIProbeVolumeVirtualBiasL2.y;
    float EntrySizeWS = max(_BurtGIProbeVolumeVirtualMinEntryIndexEntrySize.w, 0.0001f);
    int3 EntryIndexWS = (int3)floor(BiasedPositionWS / EntrySizeWS);
    int3 MinLoadedEntry = (int3)_BurtGIProbeVolumeVirtualMinLoadedEntry.xyz;
    int3 MaxLoadedEntry = (int3)_BurtGIProbeVolumeVirtualMaxLoadedEntry.xyz;
    if (any(EntryIndexWS < MinLoadedEntry) || any(EntryIndexWS > MaxLoadedEntry))
    {
        return false;
    }

    int3 EntryIndexES = EntryIndexWS - (int3)_BurtGIProbeVolumeVirtualMinEntryIndexEntrySize.xyz;
    int3 IndirectionDimensions = max((int3)_BurtGIProbeVolumeVirtualIndirectionDimensions.xyz, int3(1, 1, 1));
    if (any(EntryIndexES < 0) || any(EntryIndexES >= IndirectionDimensions))
    {
        return false;
    }

    uint EntryFlatIndex = (uint)(EntryIndexES.x + EntryIndexES.y * IndirectionDimensions.x + EntryIndexES.z * IndirectionDimensions.x * IndirectionDimensions.y);
    if (EntryFlatIndex >= (uint)max(_BurtGIProbeVolumeVirtualBufferCounts.x, 0.0f))
    {
        return false;
    }

    uint3 MetaData = _BurtGIProbeVolumeVirtualIndirection[EntryFlatIndex];
    if (MetaData.x == 0xffffffffu)
    {
        return false;
    }

    uint ChunkIndex = MetaData.x & 0x1fffffffu;
    int BrickSize = (int)round(pow(3.0f, (float)((MetaData.x >> 29) & 0x7u)));
    int3 MinRelativeIndex = int3(MetaData.y & 0x3ffu, (MetaData.y >> 10) & 0x3ffu, (MetaData.y >> 20) & 0x3ffu);
    int3 MaxRelativeIndexPlusOne = int3(MetaData.z & 0x3ffu, (MetaData.z >> 10) & 0x3ffu, (MetaData.z >> 20) & 0x3ffu);
    float3 EntryCornerPositionWS = (float3)EntryIndexWS * EntrySizeWS;
    int3 BrickIndexES = (int3)floor((BiasedPositionWS - EntryCornerPositionWS) / max(_BurtGIProbeVolumeVirtualPosOffsetMinBrickSize.w * BrickSize, 0.0001f));
    BrickIndexES = min(BrickIndexES, int3(26, 26, 26));
    if (any(BrickIndexES < MinRelativeIndex) || any(BrickIndexES >= MaxRelativeIndexPlusOne))
    {
        return false;
    }

    int3 ValidBrickDimensions = MaxRelativeIndexPlusOne - MinRelativeIndex;
    int3 BrickIndexVS = BrickIndexES - MinRelativeIndex;
    uint BrickFlatIndex = (uint)(BrickIndexVS.x * ValidBrickDimensions.y + BrickIndexVS.y + BrickIndexVS.z * ValidBrickDimensions.x * ValidBrickDimensions.y);
    uint PageTableIndex = ChunkIndex * 243u + BrickFlatIndex;
    if (PageTableIndex >= (uint)max(_BurtGIProbeVolumeVirtualBufferCounts.y, 0.0f))
    {
        return false;
    }

    uint PhysicalLocationPacked = _BurtGIProbeVolumeVirtualPageTable[PageTableIndex];
    if (PhysicalLocationPacked == 0xffffffffu)
    {
        return false;
    }

    uint Subdivision = (PhysicalLocationPacked >> 28) & 0xfu;
    uint PhysicalLocation = PhysicalLocationPacked & 0x0fffffffu;
    uint3 PoolDimensions = (uint3)max((int3)_BurtGIProbeVolumeVirtualPhysicalPoolDimensions.xyz, int3(1, 1, 1));
    uint PhysicalPoolElementCount = PoolDimensions.x * PoolDimensions.y * PoolDimensions.z;
    if (PhysicalLocation >= PhysicalPoolElementCount)
    {
        return false;
    }

    uint3 PoolIndex;
    PoolIndex.z = PhysicalLocation / (PoolDimensions.x * PoolDimensions.y);
    PhysicalLocation -= PoolIndex.z * PoolDimensions.x * PoolDimensions.y;
    PoolIndex.y = PhysicalLocation / PoolDimensions.x;
    PoolIndex.x = PhysicalLocation - PoolIndex.y * PoolDimensions.x;

    float BrickSizeWS = pow(3.0f, (float)Subdivision) * _BurtGIProbeVolumeVirtualPosOffsetMinBrickSize.w;
    float3 BrickOffset = frac(BiasedPositionWS / max(BrickSizeWS, 0.0001f)) * 3.0f + 0.5f;
    float3 PoolUV = saturate(((float3)PoolIndex + BrickOffset) * _BurtGIProbeVolumeVirtualPhysicalPoolDimensionsRcp.xyz);

    float4 L0L1Rx = _BurtGIProbeVolumeVirtualL0L1Rx.SampleLevel(sampler_LinearClamp, PoolUV, 0.0f);
    float4 L1GL1Ry = _BurtGIProbeVolumeVirtualL1GL1Ry.SampleLevel(sampler_LinearClamp, PoolUV, 0.0f);
    float4 L1BL1Rz = _BurtGIProbeVolumeVirtualL1BL1Rz.SampleLevel(sampler_LinearClamp, PoolUV, 0.0f);
    float3 L0 = L0L1Rx.rgb;
    float3 L1R = (float3(L0L1Rx.a, L1GL1Ry.a, L1BL1Rz.a) - 0.5f) * (4.0f * L0.r);
    float3 L1G = (L1GL1Ry.rgb - 0.5f) * (4.0f * L0.g);
    float3 L1B = (L1BL1Rz.rgb - 0.5f) * (4.0f * L0.b);
    float3 SafeNormalWS = normalize(NormalWS);

    // XRender evaluates encoded XGI SH in its zxy coordinate convention.
    const float BurtXGIShBasis0 = 0.28209479177387814f;
    const float BurtXGIShBasis1 = 0.4886025119029199f;
    const float BurtXGIShBasis2 = 1.092548430592079f;
    const float BurtXGIShBasis3 = 0.31539156525252f;
    const float BurtXGIShBasis4 = 0.5462742152960395f;
    float3 XGISHAxis = SafeNormalWS.zxy;
    float3 XGISHAxisSquared = XGISHAxis * XGISHAxis;
    float4 SHBasisL0L1 = float4(
        BurtXGIShBasis0,
        -BurtXGIShBasis1 * XGISHAxis.y,
        BurtXGIShBasis1 * XGISHAxis.z,
        -BurtXGIShBasis1 * XGISHAxis.x);
    Irradiance = float3(dot(float4(L0.r, L1R.x, L1R.y, L1R.z), SHBasisL0L1),
                       dot(float4(L0.g, L1G.x, L1G.y, L1G.z), SHBasisL0L1),
                       dot(float4(L0.b, L1B.x, L1B.y, L1B.z), SHBasisL0L1));

    if (_BurtGIProbeVolumeVirtualBiasL2.w > 0.5f)
    {
        float4 SkyVisibilityL0L1 = _BurtGIProbeVolumeVirtualSkyVisibilityL0L1.SampleLevel(sampler_LinearClamp, PoolUV, 0.0f);
        float4 SkyVisibilityBasis = float4(
            BurtXGIShBasis0,
            BurtXGIShBasis1 * SafeNormalWS.x,
            BurtXGIShBasis1 * SafeNormalWS.y,
            BurtXGIShBasis1 * SafeNormalWS.z);
        float SkyVisibility = lerp(1.0f, dot(SkyVisibilityBasis, SkyVisibilityL0L1), _BurtGIProbeVolumeVirtualSkyVisibilityParams.w);
        float3 SkyShadingNormalWS = SafeNormalWS;
        if (_BurtGIProbeVolumeVirtualSkyShadingDirectionEnabled > 0.5f)
        {
            int3 PhysicalPoolDimensions = max((int3)_BurtGIProbeVolumeVirtualPhysicalPoolDimensions.xyz, 1);
            int3 PoolTexel = clamp((int3)floor(PoolUV * PhysicalPoolDimensions), 0, PhysicalPoolDimensions - 1);
            uint SkyDirectionIndex = (uint)round(saturate(_BurtGIProbeVolumeVirtualSkyShadingDirectionIndices.Load(int4(PoolTexel, 0)).r) * 255.0f);
            if (SkyDirectionIndex != 255u)
            {
                float3 SkyDirection = _BurtGIProbeVolumeVirtualSkyShadingDirections[SkyDirectionIndex];
                if (dot(SkyDirection, SkyDirection) > 0.0001f)
                {
                    SkyShadingNormalWS = normalize(SkyDirection);
                }
            }
        }

        float3 OccludedSkyIrradiance = BurtSampleIndirectDiffuseIrradiance(SkyShadingNormalWS) * SkyVisibility * max(_BurtGIProbeVolumeVirtualSkyVisibilityParams.rgb, 0.0f);
        Irradiance += max(OccludedSkyIrradiance, 0.0f);
    }

    if (_BurtGIProbeVolumeVirtualBiasL2.z > 0.5f)
    {
        float4 L2R = (_BurtGIProbeVolumeVirtualL20.SampleLevel(sampler_LinearClamp, PoolUV, 0.0f) - 0.5f) * (7.1554176f * L0.r);
        float4 L2G = (_BurtGIProbeVolumeVirtualL21.SampleLevel(sampler_LinearClamp, PoolUV, 0.0f) - 0.5f) * (7.1554176f * L0.g);
        float4 L2B = (_BurtGIProbeVolumeVirtualL22.SampleLevel(sampler_LinearClamp, PoolUV, 0.0f) - 0.5f) * (7.1554176f * L0.b);
        float3 L2C = (_BurtGIProbeVolumeVirtualL23.SampleLevel(sampler_LinearClamp, PoolUV, 0.0f).rgb - 0.5f) * (7.1554176f * L0);
        float4 SHBasisL2 = float4(
            BurtXGIShBasis2 * XGISHAxis.x * XGISHAxis.y,
            -BurtXGIShBasis2 * XGISHAxis.y * XGISHAxis.z,
            BurtXGIShBasis3 * (3.0f * XGISHAxisSquared.z - 1.0f),
            -BurtXGIShBasis2 * XGISHAxis.x * XGISHAxis.z);
        float SHBasisL2C = BurtXGIShBasis4 * (XGISHAxisSquared.x - XGISHAxisSquared.y);
        Irradiance += float3(dot(L2R, SHBasisL2) + L2C.x * SHBasisL2C,
                             dot(L2G, SHBasisL2) + L2C.y * SHBasisL2C,
                             dot(L2B, SHBasisL2) + L2C.z * SHBasisL2C);
    }

    Irradiance = max(Irradiance, 0.0f);
    return true;
}

bool BurtTrySampleGIProbeVolumeIrradiance(float3 PositionWS, float3 NormalWS, float3 ViewDirectionWS, out float3 Irradiance)
{
    Irradiance = 0.0f;
    if (_BurtGIProbeVolumeParams.x < 0.5f)
    {
        return false;
    }

    if (BurtTrySampleGIProbeVolumeVirtualIrradiance(PositionWS, NormalWS, ViewDirectionWS, Irradiance))
    {
        Irradiance *= _BurtGIProbeVolumeParams.y;
        return true;
    }

    if (_BurtGIProbeVolumeParams.w >= 1.5f)
    {
        return false;
    }

    if (_BurtGIProbeVolumeCenterExtent.w <= 0.0001f)
    {
        return false;
    }

    float3 LocalPosition = (PositionWS - _BurtGIProbeVolumeCenterExtent.xyz) / _BurtGIProbeVolumeCenterExtent.www;
    float3 AbsoluteLocalPosition = abs(LocalPosition);
    if (any(AbsoluteLocalPosition >= 1.0f))
    {
        return false;
    }

    float EdgeDistance = min(min(1.0f - AbsoluteLocalPosition.x, 1.0f - AbsoluteLocalPosition.y), 1.0f - AbsoluteLocalPosition.z);
    float EdgeWeight = smoothstep(0.0f, 1.0f, EdgeDistance * _BurtGIProbeVolumeParams.z);
    float3 VolumeUV = LocalPosition * 0.5f + 0.5f;
    Irradiance = max(_BurtGIProbeVolumeIrradianceTexture.SampleLevel(sampler_LinearClamp, VolumeUV, 0.0f).rgb, 0.0f);
    Irradiance *= _BurtGIProbeVolumeParams.y * EdgeWeight;
    return true;
}

float3 BurtEvaluateGIProbeVolumeIrradiance(float3 PositionWS, float3 NormalWS, float3 ViewDirectionWS)
{
    float3 Irradiance;
    return BurtTrySampleGIProbeVolumeIrradiance(PositionWS, NormalWS, ViewDirectionWS, Irradiance) ? Irradiance : 0.0f;
}

bool BurtTryEvaluateGIProbeVolumeIndirectDiffuse(
    BurtPBRMaterialData MaterialData,
    float3 PositionWS,
    float3 NormalWS,
    float3 ViewDirectionWS,
    float EnergyPreservation,
    out float3 Diffuse)
{
    float3 Irradiance;
    if (!BurtTrySampleGIProbeVolumeIrradiance(PositionWS, NormalWS, ViewDirectionWS, Irradiance))
    {
        Diffuse = 0.0f;
        return false;
    }

    Diffuse = MaterialData.DiffuseColor * Irradiance * BurtGTAOMultiBounce(MaterialData.Occlusion, MaterialData.BaseColor) * saturate(EnergyPreservation);
    return true;
}

    float3 BurtEvaluateIndirectDiffusePBR(BurtSurfaceData SurfaceData, float3 NormalWS, float EnergyPreservation)
{
    BurtPBRMaterialData MaterialData = BurtPreparePBRMaterialData(SurfaceData);

    return BurtEvaluateIndirectDiffusePBR(MaterialData, NormalWS, EnergyPreservation);
}

float3 GetIndirectSpecularEnergyCompensation(BurtPBRMaterialData MaterialData, BurtPBRGeometryData GeometryData, BurtPBREnergyTerms EnergyTerms)
{
return EnergyTerms.IndirectSpecularEnergyCompensation;
}

float3 GetIndirectSpecularEnergyCompensation(BurtSurfaceData SurfaceData, float3 NormalWS, float3 ViewDirectionWS)
{
    BurtPBRMaterialData MaterialData = BurtPreparePBRMaterialData(SurfaceData);

    BurtPBRGeometryData GeometryData = BurtPreparePBRGeometryData(NormalWS, ViewDirectionWS);

float DirectSpecularPerceptualRoughness = GetDirectSpecularPerceptualRoughness(MaterialData, GeometryData);

    BurtPBREnergyTerms EnergyTerms = BurtPreparePBREnergyTerms(MaterialData, GeometryData, DirectSpecularPerceptualRoughness);

return GetIndirectSpecularEnergyCompensation(MaterialData, GeometryData, EnergyTerms);
}

float3 BurtEvaluateIndirectSpecularPBR(BurtPBRMaterialData MaterialData, BurtPBRGeometryData GeometryData, float3 IndirectSpecularEnergyCompensation)
{
float Roughness = MaterialData.PerceptualRoughness;

float3 ReflectionDirectionWS = BurtGetIndirectSpecularReflectionDirectionWS(GeometryData, MaterialData.Anisotropy, Roughness);

    float3 SpecularRadiance = SampleIndirectSpecularRadiance(ReflectionDirectionWS, Roughness);

    float2 Dfg = GetSpecularDFGTerms(Roughness, GeometryData.NDotV);

float3 EnvBRDF = EvalSpecularDFG(MaterialData.F0, MaterialData.F90, Dfg);


    float SpecularOcclusion = GetIndirectSpecularOcclusion(GeometryData.NDotV, MaterialData.Occlusion, Roughness);

    return SpecularRadiance * EnvBRDF * IndirectSpecularEnergyCompensation * SpecularOcclusion;
}

float3 BurtEvaluateIndirectSpecularPBR(BurtSurfaceData SurfaceData, float3 NormalWS, float3 ViewDirectionWS, float3 IndirectSpecularEnergyCompensation)
{
    BurtPBRMaterialData MaterialData = BurtPreparePBRMaterialData(SurfaceData);

    BurtPBRGeometryData GeometryData = BurtPreparePBRGeometryData(NormalWS, ViewDirectionWS);

    return BurtEvaluateIndirectSpecularPBR(MaterialData, GeometryData, IndirectSpecularEnergyCompensation);
}

float3 BurtEvaluateIndirectSpecularPBR(BurtSurfaceData SurfaceData, float3 NormalWS, float3 ViewDirectionWS)
{
float3 IndirectSpecularEnergyCompensation = GetIndirectSpecularEnergyCompensation(SurfaceData, NormalWS, ViewDirectionWS);

    return BurtEvaluateIndirectSpecularPBR(SurfaceData, NormalWS, ViewDirectionWS, IndirectSpecularEnergyCompensation);
}

struct BurtIndirectPBRComponents

{
float3 Diffuse;

float3 Specular;

    float3 SpecularEnergyCompensation;

    float3 SubsurfaceIndirect;

    float3 SubsurfaceIndirectTransmission;
};

#if BURT_ENABLE_SUBSURFACE_SHADING && (!defined(BURT_DEFERRED_LIGHTING_PRUNE_MODEL_HELPERS) || defined(BURT_DEFERRED_SHADING_MODEL_SUBSURFACE))
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLightingSubsurface.hlsl"
#else
BurtIndirectPBRComponents BurtApplySubsurfaceIndirectTransmissionFromLight(
    BurtIndirectPBRComponents Components,
    BurtPBRMaterialData MaterialData,
    BurtPBRGeometryData GeometryData,
    BurtLight MainLight)
{
    return Components;
}

BurtIndirectPBRComponents BurtApplySubsurfaceIndirectPBRComponents(
    BurtIndirectPBRComponents Components,
    BurtPBRMaterialData MaterialData,
    BurtPBRGeometryData GeometryData)
{
    return Components;
}
#endif

#if BURT_ENABLE_FABRIC_SHADING && (!defined(BURT_DEFERRED_LIGHTING_PRUNE_MODEL_HELPERS) || defined(BURT_DEFERRED_SHADING_MODEL_FABRIC))
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLightingFabric.hlsl"
#else
BurtIndirectPBRComponents BurtApplyFabricIndirectPBRComponents(
    BurtIndirectPBRComponents Components,
    BurtPBRMaterialData MaterialData,
    BurtPBRGeometryData GeometryData)
{
    return Components;
}
#endif

BurtPBRMaterialData BurtCreateClearCoatMaterialData(BurtPBRMaterialData BaseMaterialData);

#if BURT_ENABLE_CLEAR_COAT_SHADING && (!defined(BURT_DEFERRED_LIGHTING_PRUNE_MODEL_HELPERS) || defined(BURT_DEFERRED_SHADING_MODEL_CLEAR_COAT))
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLightingClearCoat.hlsl"
#else
BurtIndirectPBRComponents BurtApplyClearCoatIndirectPBRComponents(
    BurtIndirectPBRComponents Components,
    BurtPBRMaterialData MaterialData,
    BurtPBRGeometryData GeometryData,
    BurtPBRGeometryData ClearCoatGeometryData)
{
    return Components;
}
#endif


// Evaluates split indirect PBR lighting from prepared material/geometry data.
BurtIndirectPBRComponents BurtEvaluateIndirectPBRComponents(BurtPBRMaterialData MaterialData, BurtPBRGeometryData GeometryData, BurtPBRGeometryData ClearCoatGeometryData, BurtPBREnergyTerms EnergyTerms)
{
    BurtIndirectPBRComponents Components;
    Components.Diffuse = BurtEvaluateIndirectDiffusePBR(MaterialData, GeometryData.NormalWS, EnergyTerms.EnergyPreservation);
    Components.SpecularEnergyCompensation = GetIndirectSpecularEnergyCompensation(MaterialData, GeometryData, EnergyTerms);
    Components.Specular = BurtEvaluateIndirectSpecularPBR(MaterialData, GeometryData, Components.SpecularEnergyCompensation);
    Components.SubsurfaceIndirect = float3(0.0f, 0.0f, 0.0f);
    Components.SubsurfaceIndirectTransmission = float3(0.0f, 0.0f, 0.0f);

    Components = BurtApplyFabricIndirectPBRComponents(Components, MaterialData, GeometryData);
    Components = BurtApplySubsurfaceIndirectPBRComponents(Components, MaterialData, GeometryData);
    Components = BurtApplyClearCoatIndirectPBRComponents(Components, MaterialData, GeometryData, ClearCoatGeometryData);

    return Components;
}

BurtIndirectPBRComponents BurtEvaluateIndirectPBRComponents(BurtPBRMaterialData MaterialData, BurtPBRGeometryData GeometryData, BurtPBREnergyTerms EnergyTerms)
{
    return BurtEvaluateIndirectPBRComponents(MaterialData, GeometryData, GeometryData, EnergyTerms);
}


// Compatibility overload that prepares material/geometry/energy terms internally.
BurtIndirectPBRComponents BurtEvaluateIndirectPBRComponents(BurtSurfaceData SurfaceData, float3 NormalWS, float3 ViewDirectionWS, float EnergyPreservation)
{
    BurtPBRMaterialData MaterialData = BurtPreparePBRMaterialData(SurfaceData);

    BurtPBRGeometryData GeometryData = BurtPreparePBRGeometryData(NormalWS, ViewDirectionWS);

    float DirectSpecularPerceptualRoughness = GetDirectSpecularPerceptualRoughness(MaterialData, GeometryData);
    BurtPBREnergyTerms EnergyTerms = BurtPreparePBREnergyTerms(MaterialData, GeometryData, DirectSpecularPerceptualRoughness);
    EnergyTerms.EnergyPreservation = EnergyPreservation;

    return BurtEvaluateIndirectPBRComponents(MaterialData, GeometryData, EnergyTerms);
}

float3 BurtEvaluateIndirectPBR(BurtSurfaceData SurfaceData, float3 NormalWS, float3 ViewDirectionWS)
{
    BurtPBRMaterialData MaterialData = BurtPreparePBRMaterialData(SurfaceData);

    BurtPBRGeometryData GeometryData = BurtPreparePBRGeometryData(NormalWS, ViewDirectionWS);

    float DirectSpecularPerceptualRoughness = GetDirectSpecularPerceptualRoughness(MaterialData, GeometryData);

    BurtPBREnergyTerms EnergyTerms = BurtPreparePBREnergyTerms(MaterialData, GeometryData, DirectSpecularPerceptualRoughness);

    BurtIndirectPBRComponents Components = BurtEvaluateIndirectPBRComponents(MaterialData, GeometryData, EnergyTerms);

    return Components.Diffuse + Components.Specular;
}

#endif
