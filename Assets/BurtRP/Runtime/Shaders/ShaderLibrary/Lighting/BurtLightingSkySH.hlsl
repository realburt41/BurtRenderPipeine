// Ambient color, spherical harmonics, and sky diffuse sampling for Burt indirect lighting.
#ifndef BURT_LIGHTING_SKY_SH_INCLUDED
#define BURT_LIGHTING_SKY_SH_INCLUDED

float4 _BurtAmbientLightColor;

float4 _BurtAmbientSHAr;
float4 _BurtAmbientSHAg;
float4 _BurtAmbientSHAb;
float4 _BurtAmbientSHBr;
float4 _BurtAmbientSHBg;
float4 _BurtAmbientSHBb;
float4 _BurtAmbientSHC;
float _BurtAmbientSHEnabled;

TextureCube _BurtSkyReflectionSourceTexture;
TextureCube _BurtSkyDiffuseCubemapTexture;
Texture2D _BurtSkyDiffuseSHTexture;

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

#endif
