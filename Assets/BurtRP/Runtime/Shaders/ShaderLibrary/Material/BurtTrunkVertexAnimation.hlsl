// XRender-style trunk vertex animation helpers.
#ifndef BURT_TRUNK_VERTEX_ANIMATION_INCLUDED
#define BURT_TRUNK_VERTEX_ANIMATION_INCLUDED

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"

#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_TRUNK) || defined(BURT_MATERIAL_SHADING_MODEL_TRUNK)
    #define BURT_TRUNK_MATERIAL_ENABLED 1
#endif

float BurtTrunkHash31(float3 value)
{
    return frac(sin(dot(value, float3(12.9898f, 78.233f, 37.719f))) * 43758.5453f);
}

float3 BurtTrunkWindDirectionWS()
{
    return BurtSafeNormalize(float3(0.78f, 0.0f, 0.62f));
}

float3 BurtTrunkRotateAboutAxisOffset(float3 axis, float angle, float3 pivotWS, float3 positionWS)
{
    axis = BurtSafeNormalize(axis);
    float sineValue;
    float cosineValue;
    sincos(angle, sineValue, cosineValue);

    float3 relative = positionWS - pivotWS;
    float3 rotated = relative * cosineValue + cross(axis, relative) * sineValue + axis * dot(axis, relative) * (1.0f - cosineValue);
    return pivotWS + rotated - positionWS;
}

float4 BurtGetTrunkAnimatedWorldPosition(float4 positionOS, float4 vertexColor, float4x4 objectToWorldMatrix, float timeSeconds)
{
#if defined(BURT_TRUNK_MATERIAL_ENABLED)
    float3 positionWS = mul(objectToWorldMatrix, positionOS).xyz;
    float3 pivotWS = mul(objectToWorldMatrix, float4(0.0f, 0.0f, 0.0f, 1.0f)).xyz;

    float maxBendAngle = max(_MaxBendAngle, 0.0f);
    float swayIntensity = max(_SwayIntensity, 0.0f);
    if (maxBendAngle <= BURT_EPSILON && swayIntensity <= BURT_EPSILON)
    {
        return float4(positionWS, 1.0f);
    }

    float height = max(_TreeHeight, BURT_EPSILON);
    float heightMask = saturate(positionOS.y / height);
    float bendPower = _BendMaskPow > BURT_EPSILON ? _BendMaskPow : 1.0f;
    heightMask = pow(max(heightMask, 0.001f), max(bendPower, 0.001f));

    float trunkOffset = saturate(vertexColor.y);
    float distanceToTrunk = saturate(1.0f - vertexColor.z) * 0.99f + 0.01f;
    distanceToTrunk = pow(distanceToTrunk, max(_ToTrunkMaskPow, 0.001f));

    float windTime = timeSeconds * 20.0f;
    float3 windDirectionWS = BurtTrunkWindDirectionWS();
    float3 windOffsetWS = float3(0.0f, 0.0f, 0.0f);

    float windNoise = sin(dot(pivotWS.xz, float2(0.011f, 0.017f)) + windTime * 0.1f);
    float3 bendAxis = cross(float3(-windDirectionWS.x, 0.0f, -windDirectionWS.z), float3(0.0f, 1.0f, 0.0f));
    float bendAngle = windNoise * heightMask * 0.0035f * maxBendAngle;
    windOffsetWS += BurtTrunkRotateAboutAxisOffset(bendAxis, bendAngle, pivotWS, positionWS);

    float pivotOffset = (pivotWS.x + pivotWS.y + pivotWS.z) * 70.0f + BurtTrunkHash31(pivotWS) * 6.2831853f;
    float positionOffset = (positionWS.x + positionWS.z) * 0.07f;
    float swayRatio = 0.5f;
    float frequencyA = sin(positionOffset + pivotOffset + (swayRatio + 0.1f) * windTime + trunkOffset) * 1.5f;
    float frequencyB = cos(positionOffset + pivotOffset + (swayRatio + 0.1f) * 3.5f * windTime * max(trunkOffset, 0.001f) + trunkOffset);
    float3 randomSwayDirection = float3(windDirectionWS.x, (frequencyA + frequencyB * 0.5f) * 0.3f, windDirectionWS.z);
    float swayWave = (frequencyA + frequencyB) * 0.5f + 0.2f;
    windOffsetWS += randomSwayDirection * 0.1f * saturate(distanceToTrunk * heightMask) * swayIntensity * swayWave;

    return float4(positionWS + windOffsetWS, 1.0f);
#else
    return mul(objectToWorldMatrix, positionOS);
#endif
}

float4 BurtApplyTrunkVertexAnimationObjectSpace(float4 positionOS, float4 vertexColor, float timeSeconds)
{
#if defined(BURT_TRUNK_MATERIAL_ENABLED)
    float4 animatedWorld = BurtGetTrunkAnimatedWorldPosition(positionOS, vertexColor, unity_ObjectToWorld, timeSeconds);
    return mul(unity_WorldToObject, animatedWorld);
#else
    return positionOS;
#endif
}

#endif // BURT_TRUNK_VERTEX_ANIMATION_INCLUDED
