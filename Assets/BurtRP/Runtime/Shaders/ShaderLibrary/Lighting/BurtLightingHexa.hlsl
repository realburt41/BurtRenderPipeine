// Six-direction scattering lighting used by XRender's HexaLighting materials.
#ifndef BURT_LIGHTING_HEXA_INCLUDED
#define BURT_LIGHTING_HEXA_INCLUDED

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLighting.hlsl"

struct BurtHexaLightingData
{
    float3 BaseColor;
    float3 ScatteringFactorRTBk;
    float3 ScatteringFactorLBtF;
};

float3 BurtHexaGetOrthonormalTangent(float3 normalWS, float3 tangentWS)
{
    float3 projectedTangentWS = tangentWS - normalWS * dot(normalWS, tangentWS);
    if (dot(projectedTangentWS, projectedTangentWS) <= 0.000001f)
    {
        float3 fallbackAxis = abs(normalWS.y) < 0.999f ? float3(0.0f, 1.0f, 0.0f) : float3(1.0f, 0.0f, 0.0f);
        projectedTangentWS = cross(fallbackAxis, normalWS);
    }

    return BurtSafeNormalize(projectedTangentWS);
}

void BurtBuildHexaBasis(
    float3 normalWS,
    float4 tangentWS,
    out float3 rightWS,
    out float3 topWS,
    out float3 backWS)
{
    float3 safeNormalWS = BurtSafeNormalize(normalWS);
    rightWS = BurtHexaGetOrthonormalTangent(safeNormalWS, tangentWS.xyz);
    topWS = BurtSafeNormalize(cross(safeNormalWS, rightWS)) * (tangentWS.w >= 0.0f ? 1.0f : -1.0f);
    backWS = -safeNormalWS;
}

float3 BurtTransformHexaLightDirectionToLocal(float3 lightDirectionWS, float3 rightWS, float3 topWS, float3 backWS)
{
    float3 safeLightDirectionWS = BurtSafeNormalize(lightDirectionWS);
    return float3(
        dot(rightWS, safeLightDirectionWS),
        dot(topWS, safeLightDirectionWS),
        dot(backWS, safeLightDirectionWS));
}

float3 BurtEvaluateHexaDirectionalScattering(BurtHexaLightingData data, float3 localLightDirection)
{
    float3 directionalScattering = lerp(data.ScatteringFactorLBtF, data.ScatteringFactorRTBk, step(0.0f, localLightDirection));
    float transmittance = dot(localLightDirection * localLightDirection, directionalScattering);
    return data.BaseColor * max(transmittance, 0.0f);
}

float3 BurtEvaluateHexaLight(BurtHexaLightingData data, BurtLight light, float3 rightWS, float3 topWS, float3 backWS)
{
    float3 localLightDirection = BurtTransformHexaLightDirectionToLocal(light.DirectionWS, rightWS, topWS, backWS);
    float3 scattering = BurtEvaluateHexaDirectionalScattering(data, localLightDirection);
    return scattering * max(light.Color, float3(0.0f, 0.0f, 0.0f)) * saturate(light.ShadowAttenuation);
}

float3 BurtEvaluateHexaSkyScattering(BurtHexaLightingData data, float3 rightWS, float3 topWS, float3 backWS)
{
    float3 positiveIrradiance =
        data.ScatteringFactorRTBk.x * BurtSampleIndirectDiffuseIrradiance(rightWS) +
        data.ScatteringFactorRTBk.y * BurtSampleIndirectDiffuseIrradiance(topWS) +
        data.ScatteringFactorRTBk.z * BurtSampleIndirectDiffuseIrradiance(backWS);
    float3 negativeIrradiance =
        data.ScatteringFactorLBtF.x * BurtSampleIndirectDiffuseIrradiance(-rightWS) +
        data.ScatteringFactorLBtF.y * BurtSampleIndirectDiffuseIrradiance(-topWS) +
        data.ScatteringFactorLBtF.z * BurtSampleIndirectDiffuseIrradiance(-backWS);

    // XRender estimates the spherical integral from six baked directional lobes.
    return data.BaseColor * (positiveIrradiance + negativeIrradiance) * 2.09439510239f;
}

float3 BurtEvaluateHexaLighting(
    BurtHexaLightingData data,
    BurtLight mainLight,
    float3 positionWS,
    float3 normalWS,
    float4 tangentWS)
{
    float3 rightWS;
    float3 topWS;
    float3 backWS;
    BurtBuildHexaBasis(normalWS, tangentWS, rightWS, topWS, backWS);

    float3 lighting = BurtEvaluateHexaLight(data, mainLight, rightWS, topWS, backWS);

    [loop]
    for (int lightIndex = 0; lightIndex < BURT_MAX_ADDITIONAL_LIGHTS; lightIndex++)
    {
        if (lightIndex >= BurtGetAdditionalLightCount())
        {
            break;
        }

        BurtLight additionalLight = BurtCreateAdditionalLight(lightIndex, positionWS, normalWS);
        lighting += BurtEvaluateHexaLight(data, additionalLight, rightWS, topWS, backWS);
    }

    return max(lighting + BurtEvaluateHexaSkyScattering(data, rightWS, topWS, backWS), float3(0.0f, 0.0f, 0.0f));
}

#endif // BURT_LIGHTING_HEXA_INCLUDED
