// Image-based specular reflection sampling for Burt indirect lighting.
#ifndef BURT_LIGHTING_IBL_INCLUDED
#define BURT_LIGHTING_IBL_INCLUDED

TextureCube _BurtSkyReflectionTexture;
float4 _BurtSkyReflectionHDR;
float _BurtSkyReflectionIntensity;

// Optional tint supplied by BurtSkyLight; legacy RenderSettings path uploads white.
float4 _BurtSkyReflectionTint;

float _BurtSkyReflectionEnabled;
float _BurtSkyReflectionOverride;
float _BurtSkyReflectionMaxMip;

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

#endif
