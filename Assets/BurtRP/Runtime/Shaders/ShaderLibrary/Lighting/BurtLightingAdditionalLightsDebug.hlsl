// Additional-light shadow diagnostics kept out of the runtime light creation layer.
#ifndef BURT_LIGHTING_ADDITIONAL_LIGHTS_DEBUG_INCLUDED
#define BURT_LIGHTING_ADDITIONAL_LIGHTS_DEBUG_INCLUDED

bool BurtGetAdditionalLightShadowProjectionDebug(
    int lightIndex,
    float3 positionWS,
    float3 lightPositionWS,
    float3 lightDirectionWS,
    float3 normalWS,
    out float3 faceColor,
    out float3 uvColor,
    out float3 depthColor,
    out float3 depthDeltaColor);

float BurtEvaluateAdditionalShadowAttenuationDebug(float3 positionWS, float3 normalWS)
{
    float Attenuation = 1.0f;
    int AdditionalLightCount = BurtGetAdditionalLightCount();

    [loop]
    for (int LightIndex = 0; LightIndex < BURT_MAX_ADDITIONAL_LIGHTS; LightIndex++)
    {
        if (LightIndex >= AdditionalLightCount)
        {
            break;
        }

        BurtLight AdditionalLight = BurtCreateAdditionalLight(LightIndex, positionWS, normalWS);
        if (dot(AdditionalLight.Color, float3(0.2126f, 0.7152f, 0.0722f)) <= 0.0001f)
        {
            continue;
        }

        Attenuation = min(Attenuation, saturate(AdditionalLight.ShadowAttenuation));
    }

    return Attenuation;
}

float BurtEvaluateAdditionalShadowAttenuationDebug(float3 positionWS)
{
    return BurtEvaluateAdditionalShadowAttenuationDebug(positionWS, float3(0.0f, 0.0f, 0.0f));
}

float BurtEvaluateAdditionalShadowAttenuationDebug(float3 positionWS, float3 normalWS, float2 screenUV)
{
#if defined(BURT_USE_TILED_LIGHTING)
    uint2 Range = uint2(0u, 0u);
    uint UseClusterLightList = 0u;
    if (!BurtTryGetAdditionalLightListRange(screenUV, positionWS, Range, UseClusterLightList))
    {
        return BurtEvaluateAdditionalShadowAttenuationDebug(positionWS, normalWS);
    }

    float Attenuation = 1.0f;
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

        BurtLight AdditionalLight = BurtCreateAdditionalLight((int)StoredLightIndex, positionWS, normalWS);
        if (dot(AdditionalLight.Color, float3(0.2126f, 0.7152f, 0.0722f)) <= 0.0001f)
        {
            continue;
        }

        Attenuation = min(Attenuation, saturate(AdditionalLight.ShadowAttenuation));
    }

    return Attenuation;
#else
    return BurtEvaluateAdditionalShadowAttenuationDebug(positionWS, normalWS);
#endif
}

float BurtEvaluateAdditionalShadowAttenuationDebug(float3 positionWS, float2 screenUV)
{
    return BurtEvaluateAdditionalShadowAttenuationDebug(positionWS, float3(0.0f, 0.0f, 0.0f), screenUV);
}

bool BurtTryFillAdditionalLightShadowProjectionDebugForLight(
    int lightIndex,
    float3 positionWS,
    float3 normalWS,
    out float3 faceColor,
    out float3 uvColor,
    out float3 depthColor,
    out float3 depthDeltaColor)
{
    faceColor = float3(0.0f, 0.0f, 0.0f);
    uvColor = float3(0.0f, 0.0f, 0.0f);
    depthColor = float3(0.0f, 0.0f, 0.0f);
    depthDeltaColor = float3(0.0f, 0.0f, 0.0f);

    float4 PositionAndRange = BurtReadAdditionalLightPositionAndRange(lightIndex);
    BurtLight AdditionalLight = BurtCreateAdditionalLightUnshadowed(lightIndex, positionWS, normalWS);
    if (dot(AdditionalLight.Color, float3(0.2126f, 0.7152f, 0.0722f)) <= 0.0001f)
    {
        return false;
    }

    return BurtGetAdditionalLightShadowProjectionDebug(
        lightIndex,
        positionWS,
        PositionAndRange.xyz,
        AdditionalLight.DirectionWS,
        normalWS,
        faceColor,
        uvColor,
        depthColor,
        depthDeltaColor);
}

void BurtFillAdditionalLightShadowProjectionDebugData(
    float3 positionWS,
    float3 normalWS,
    out float3 faceColor,
    out float3 uvColor,
    out float3 depthColor,
    out float3 depthDeltaColor)
{
    faceColor = float3(0.0f, 0.0f, 0.0f);
    uvColor = float3(0.0f, 0.0f, 0.0f);
    depthColor = float3(0.0f, 0.0f, 0.0f);
    depthDeltaColor = float3(0.0f, 0.0f, 0.0f);

    int AdditionalLightCount = BurtGetAdditionalLightCount();
    [loop]
    for (int LightIndex = 0; LightIndex < BURT_MAX_ADDITIONAL_LIGHTS; LightIndex++)
    {
        if (LightIndex >= AdditionalLightCount)
        {
            break;
        }

        if (BurtTryFillAdditionalLightShadowProjectionDebugForLight(LightIndex, positionWS, normalWS, faceColor, uvColor, depthColor, depthDeltaColor))
        {
            return;
        }
    }
}

void BurtFillAdditionalLightShadowProjectionDebugData(
    float3 positionWS,
    float3 normalWS,
    float2 screenUV,
    out float3 faceColor,
    out float3 uvColor,
    out float3 depthColor,
    out float3 depthDeltaColor)
{
#if defined(BURT_USE_TILED_LIGHTING)
    uint2 Range = uint2(0u, 0u);
    uint UseClusterLightList = 0u;
    if (!BurtTryGetAdditionalLightListRange(screenUV, positionWS, Range, UseClusterLightList))
    {
        BurtFillAdditionalLightShadowProjectionDebugData(positionWS, normalWS, faceColor, uvColor, depthColor, depthDeltaColor);
        return;
    }

    faceColor = float3(0.0f, 0.0f, 0.0f);
    uvColor = float3(0.0f, 0.0f, 0.0f);
    depthColor = float3(0.0f, 0.0f, 0.0f);
    depthDeltaColor = float3(0.0f, 0.0f, 0.0f);

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

        if (BurtTryFillAdditionalLightShadowProjectionDebugForLight((int)StoredLightIndex, positionWS, normalWS, faceColor, uvColor, depthColor, depthDeltaColor))
        {
            return;
        }
    }
#else
    BurtFillAdditionalLightShadowProjectionDebugData(positionWS, normalWS, faceColor, uvColor, depthColor, depthDeltaColor);
#endif
}

#endif
