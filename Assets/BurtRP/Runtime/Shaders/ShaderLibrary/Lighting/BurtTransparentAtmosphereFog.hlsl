#ifndef BURT_TRANSPARENT_ATMOSPHERE_FOG_INCLUDED
#define BURT_TRANSPARENT_ATMOSPHERE_FOG_INCLUDED

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/BurtAtmosphereLut.hlsl"

float _BurtTransparentAtmosphereFogEnabled;
float4 _BurtTransparentAtmosphereFogLightColor;
float4 _BurtTransparentAtmosphereFogDistanceParams; // x=world to km, y=sampling scale, z=XRender start depth, w=coverage km

float _BurtTransparentHeightFogEnabled;
float4 _BurtTransparentHeightFogParams; // x=height, y=density, z=height falloff, w=max opacity
float4 _BurtTransparentHeightFogSecondLayerParams; // x=absolute height, y=density, z=height falloff
float4 _BurtTransparentHeightFogDistanceParams; // x=start distance, y=cutoff distance
float4 _BurtTransparentHeightFogAerialParams; // x=interaction, y=aerial fade start, z=aerial fade end
float4 _BurtTransparentHeightFogAlbedo;
float4 _BurtTransparentHeightFogScatteringParams; // x=directional, y=ambient, z=anisotropy, w=use atmosphere horizontal scattering
float4 _BurtTransparentHeightFogRayleighTintScale;
float4 _BurtTransparentHeightFogMieTintScale;
float4 _BurtTransparentHeightFogMultipleScatteringTintScale;
float4 _BurtTransparentHeightFogMainLightDirection;
float4 _BurtTransparentHeightFogLegacyLightColor;
float4 _BurtTransparentHeightFogHorizontalSunDirection;
float4 _BurtTransparentHeightFogHorizontalLightColor;
float _BurtTransparentHeightFogMainLightOcclusion;

sampler3D _BurtVolumetricFogIntegratedLut;
float _BurtVolumetricFogIntegratedEnabled;
float4 _BurtVolumetricFogIntegratedGridZParams; // xyz=(B,O,S), w=total slice count
float4 _BurtVolumetricFogIntegratedSamplingParams; // x=visible slice end, y=visible distance, z=start distance, w=inv slice count

float4 BurtEvaluateTransparentVolumetricFog(float2 screenUV, float3 positionWS)
{
    if (_BurtVolumetricFogIntegratedEnabled < 0.5f)
    {
        return float4(0.0f, 0.0f, 0.0f, 1.0f);
    }

    float viewDepth = max(-mul(UNITY_MATRIX_V, float4(positionWS, 1.0f)).z, 0.0f);
    float b = _BurtVolumetricFogIntegratedGridZParams.x;
    float o = _BurtVolumetricFogIntegratedGridZParams.y;
    float s = max(_BurtVolumetricFogIntegratedGridZParams.z, 1.0e-4f);
    float totalSliceCount = max(_BurtVolumetricFogIntegratedGridZParams.w, 1.0f);
    float zSlice = log2(max(viewDepth * b + o, 1.0e-6f)) * s;
    float lastVisibleSliceCenter = max(_BurtVolumetricFogIntegratedSamplingParams.x - 0.5f, 0.5f);
    zSlice = clamp(zSlice, 0.5f, lastVisibleSliceCenter);
    float normalizedZ = zSlice / totalSliceCount;
    // Explicit LOD is required because XRender's default transparent path
    // evaluates total fog in the vertex shader.
    return tex3Dlod(
        _BurtVolumetricFogIntegratedLut,
        float4(saturate(screenUV), saturate(normalizedZ), 0.0f));
}

float3 BurtTransparentHeightFogSafeNormalize(float3 value, float3 fallback)
{
    float lengthSq = dot(value, value);
    return lengthSq > 1.0e-8f ? value * rsqrt(lengthSq) : fallback;
}

float BurtTransparentHeightFogCalcLineIntegral(float falloff, float rayDeltaY, float mediumDensity)
{
    float scaledFalloff = max(-127.0f, falloff * rayDeltaY);
    float log2Value = log(2.0f);
    if (abs(scaledFalloff) <= 0.01f)
    {
        return mediumDensity * (log2Value - 0.5f * log2Value * log2Value * scaledFalloff);
    }

    return mediumDensity * ((1.0f - exp2(-scaledFalloff)) / scaledFalloff);
}

float3 BurtTransparentHeightFogNormalizeLightColor(float3 lightColor)
{
    float peak = max(max(lightColor.r, lightColor.g), lightColor.b);
    return peak > 0.001f ? lightColor / peak : 1.0f;
}

float BurtTransparentHeightFogPhase(float cosTheta, float anisotropy)
{
    // XRender Height Fog uses Schlick with the PBRT phase convention (-L.V).
    return BurtAtmosphereSchlickPhase(anisotropy, -cosTheta);
}

float4 BurtEvaluateTransparentHeightFog(float3 positionWS)
{
    if (_BurtTransparentHeightFogEnabled < 0.5f)
    {
        return float4(0.0f, 0.0f, 0.0f, 1.0f);
    }

    float3 cameraToPixel = positionWS - _WorldSpaceCameraPos.xyz;
    float viewDistance = length(cameraToPixel);
    if (viewDistance <= 1.0e-4f)
    {
        return float4(0.0f, 0.0f, 0.0f, 1.0f);
    }

    float startDistance = max(_BurtTransparentHeightFogDistanceParams.x, 0.0f);
    float cutoffDistance = max(_BurtTransparentHeightFogDistanceParams.y, 0.0f);
    if (cutoffDistance > 0.0f && viewDistance > cutoffDistance)
    {
        return float4(0.0f, 0.0f, 0.0f, 1.0f);
    }

    float rayLength = max(viewDistance - startDistance, 0.0f);
    if (rayLength <= 1.0e-4f)
    {
        return float4(0.0f, 0.0f, 0.0f, 1.0f);
    }

    float3 viewDirection = cameraToPixel / viewDistance;
    float startRatio = startDistance / viewDistance;
    float startHeight = _WorldSpaceCameraPos.y + cameraToPixel.y * startRatio;
    float rayDeltaY = cameraToPixel.y * (rayLength / viewDistance);
    float fogHeight = _BurtTransparentHeightFogParams.x;
    float fogDensity = max(_BurtTransparentHeightFogParams.y, 0.0f);
    float heightFalloff = max(_BurtTransparentHeightFogParams.z, 0.001f);
    float maxOpacity = saturate(_BurtTransparentHeightFogParams.w);
    float mediumDensity = fogDensity * exp2(-max(-127.0f, heightFalloff * (startHeight - fogHeight)));
    float secondFogHeight = _BurtTransparentHeightFogSecondLayerParams.x;
    float secondFogDensity = max(_BurtTransparentHeightFogSecondLayerParams.y, 0.0f);
    float secondHeightFalloff = max(_BurtTransparentHeightFogSecondLayerParams.z, 0.0f);
    float secondMediumDensity = secondFogDensity * exp2(
        -max(-127.0f, secondHeightFalloff * (startHeight - secondFogHeight)));
    float opticalDepth = (
        BurtTransparentHeightFogCalcLineIntegral(
            heightFalloff,
            rayDeltaY,
            mediumDensity)
        + BurtTransparentHeightFogCalcLineIntegral(
            secondHeightFalloff,
            rayDeltaY,
            secondMediumDensity)) * rayLength;
    // Match AEvaluateGlobalHeightFog: MaxOpacity is a final opacity cap,
    // expressed as a lower bound on transmittance.
    float transmittance = max(
        saturate(exp2(-max(opticalDepth, 0.0f))),
        1.0f - maxOpacity);
    float fogAmount = saturate(1.0f - transmittance);

    float aerialInteraction = _BurtTransparentHeightFogAerialParams.x;
    if (aerialInteraction > 0.5f && aerialInteraction < 1.5f)
    {
        float fadeStart = max(_BurtTransparentHeightFogAerialParams.y, 0.0f);
        float fadeEnd = max(_BurtTransparentHeightFogAerialParams.z, fadeStart + 0.001f);
        fogAmount *= 1.0f - smoothstep(fadeStart, fadeEnd, viewDistance);
        transmittance = 1.0f - fogAmount;
    }

    float3 lightDirection = BurtTransparentHeightFogSafeNormalize(
        _BurtTransparentHeightFogMainLightDirection.xyz,
        float3(0.0f, 1.0f, 0.0f));
    float3 lightColor = BurtTransparentHeightFogNormalizeLightColor(
        max(_BurtTransparentHeightFogLegacyLightColor.rgb, 0.0f));
    float lightViewCosine = dot(lightDirection, viewDirection);
    float phase = BurtTransparentHeightFogPhase(
        lightViewCosine,
        _BurtTransparentHeightFogScatteringParams.z);
    float directional = max(_BurtTransparentHeightFogScatteringParams.x, 0.0f) * phase * 4.0f;
    float ambient = max(_BurtTransparentHeightFogScatteringParams.y, 0.0f);
    float mainLightOcclusion = saturate(_BurtTransparentHeightFogMainLightOcclusion);
    float3 evaluatedFogColor = max(_BurtTransparentHeightFogAlbedo.rgb, 0.0f)
        * (ambient + directional * lightColor * mainLightOcclusion);

    float useAtmosphereHorizontalScattering = _BurtTransparentHeightFogScatteringParams.w
        * _BurtAtmosphereUseLuts;
    [branch]
    if (useAtmosphereHorizontalScattering > 0.5f)
    {
        float3 horizontalSunDirection = BurtTransparentHeightFogSafeNormalize(
            _BurtTransparentHeightFogHorizontalSunDirection.xyz,
            lightDirection);
        float horizontalLightViewCosine = dot(horizontalSunDirection, viewDirection);
        evaluatedFogColor = BurtAtmosphereEvaluateHorizontalFogLighting(
            horizontalLightViewCosine,
            _BurtTransparentHeightFogScatteringParams.z,
            _BurtTransparentHeightFogHorizontalLightColor.rgb,
            _BurtTransparentHeightFogRayleighTintScale.rgb,
            _BurtTransparentHeightFogMieTintScale.rgb,
            _BurtTransparentHeightFogMultipleScatteringTintScale.rgb,
            1.0f,
            mainLightOcclusion);
    }

    return float4(max(evaluatedFogColor, 0.0f) * fogAmount, saturate(transmittance));
}

float4 BurtEvaluateTransparentAtmosphereFog(float2 screenUV, float3 positionWS)
{
    if (_BurtTransparentAtmosphereFogEnabled < 0.5f || _BurtAtmosphereUseLuts < 0.5f)
    {
        return float4(0.0f, 0.0f, 0.0f, 1.0f);
    }

    float3 cameraToPixel = positionWS - _WorldSpaceCameraPos.xyz;
    float distanceWS = length(cameraToPixel);
    float startDepth = max(_BurtTransparentAtmosphereFogDistanceParams.z, 0.0f);
    float worldToKilometers = max(_BurtTransparentAtmosphereFogDistanceParams.x, 0.000001f);
    float distanceKm = distanceWS * worldToKilometers;
    float startDepthKm = startDepth * worldToKilometers;
    float distanceRatio = max(distanceKm - startDepthKm, 0.0f)
        * max(_BurtTransparentAtmosphereFogDistanceParams.y, 0.0f)
        / max(_BurtTransparentAtmosphereFogDistanceParams.w, 0.001f);

    float2 fogScreenUV = saturate(screenUV);
#if UNITY_UV_STARTS_AT_TOP
    fogScreenUV.y = 1.0f - fogScreenUV.y;
#endif
    float4 fogLut = BurtAtmosphereSampleFog(fogScreenUV, distanceRatio);

    float startWeight = BurtAtmosphereFogStartWeight(distanceRatio);
    // Exact AEvaluateAtmosphereFog contract: the physical LUT has only its
    // intrinsic first-froxel fade. Legacy BRP shape controls do not participate.
    float lutWeight = startWeight;
    float transmittance = lerp(1.0f, fogLut.a, lutWeight);
    float3 scattering = max(fogLut.rgb, 0.0f)
        * max(_BurtTransparentAtmosphereFogLightColor.rgb, 0.0f)
        * lutWeight;
    return float4(scattering, saturate(transmittance));
}

float4 BurtEvaluateTransparentFog(float2 screenUV, float3 positionWS)
{
    // Match XRender's near-to-far total-fog accumulation: VF -> HF -> AF.
    float4 volumetricFog = BurtEvaluateTransparentVolumetricFog(screenUV, positionWS);
    float4 heightFog = BurtEvaluateTransparentHeightFog(positionWS);
    float4 atmosphereFog = BurtEvaluateTransparentAtmosphereFog(screenUV, positionWS);
    float3 farFogScattering = heightFog.rgb + heightFog.a * atmosphereFog.rgb;
    float farFogTransmittance = heightFog.a * atmosphereFog.a;
    return float4(
        volumetricFog.rgb + volumetricFog.a * farFogScattering,
        saturate(volumetricFog.a * farFogTransmittance));
}

float3 BurtBlendTransparentFog(float3 surfaceRadiance, float4 fog)
{
    return surfaceRadiance * fog.a + fog.rgb;
}

float3 BurtBlendPremultipliedTransparentFog(
    float3 premultipliedSurfaceRadiance,
    float alpha,
    float4 fog)
{
    return premultipliedSurfaceRadiance * fog.a + fog.rgb * saturate(alpha);
}

float3 BurtBlendAdditiveTransparentFog(float3 additiveRadiance, float4 fog)
{
    return additiveRadiance * fog.a;
}

float3 BurtApplyTransparentFog(float3 surfaceRadiance, float2 screenUV, float3 positionWS)
{
    float4 fog = BurtEvaluateTransparentFog(screenUV, positionWS);
    return BurtBlendTransparentFog(surfaceRadiance, fog);
}

float3 BurtApplyPremultipliedTransparentFog(
    float3 premultipliedSurfaceRadiance,
    float alpha,
    float2 screenUV,
    float3 positionWS)
{
    float4 fog = BurtEvaluateTransparentFog(screenUV, positionWS);
    return BurtBlendPremultipliedTransparentFog(
        premultipliedSurfaceRadiance,
        alpha,
        fog);
}

float3 BurtApplyAdditiveTransparentFog(float3 additiveRadiance, float2 screenUV, float3 positionWS)
{
    float4 fog = BurtEvaluateTransparentFog(screenUV, positionWS);
    return BurtBlendAdditiveTransparentFog(additiveRadiance, fog);
}

// Compatibility wrappers for materials integrated before total transparent fog
// gained its Height Fog and Volumetric Fog stages.
float3 BurtApplyTransparentAtmosphereFog(float3 surfaceRadiance, float2 screenUV, float3 positionWS)
{
    return BurtApplyTransparentFog(surfaceRadiance, screenUV, positionWS);
}

float3 BurtApplyPremultipliedTransparentAtmosphereFog(
    float3 premultipliedSurfaceRadiance,
    float alpha,
    float2 screenUV,
    float3 positionWS)
{
    return BurtApplyPremultipliedTransparentFog(
        premultipliedSurfaceRadiance,
        alpha,
        screenUV,
        positionWS);
}

float3 BurtApplyAdditiveTransparentAtmosphereFog(float3 additiveRadiance, float2 screenUV, float3 positionWS)
{
    return BurtApplyAdditiveTransparentFog(additiveRadiance, screenUV, positionWS);
}

#endif
