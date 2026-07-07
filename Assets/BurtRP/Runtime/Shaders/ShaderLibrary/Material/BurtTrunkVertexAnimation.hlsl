// XRender-style trunk vertex animation helpers.
#ifndef BURT_TRUNK_VERTEX_ANIMATION_INCLUDED
#define BURT_TRUNK_VERTEX_ANIMATION_INCLUDED

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"

#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_TRUNK) || defined(BURT_MATERIAL_SHADING_MODEL_TRUNK)
    #define BURT_TRUNK_MATERIAL_ENABLED 1
#endif

#if defined(BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS)
    #define BURT_GRASS_MATERIAL_ENABLED 1
#endif

#if (defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE) || defined(BURT_MATERIAL_SHADING_MODEL_FOLIAGE)) && !defined(BURT_GRASS_MATERIAL_ENABLED)
    #define BURT_FOLIAGE_MATERIAL_ENABLED 1
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

float3 BurtGrassPivotPosOSFromVertexColor(float4 vertexColor)
{
    vertexColor = saturate(vertexColor);
    return float3(-(vertexColor.b - 0.5f) * 4.0f, 0.0f, -(vertexColor.a - 0.5f) * 4.0f);
}

float3 BurtGrassPivotPosWSFromVertexColor(float4 vertexColor, float4x4 objectToWorldMatrix)
{
    return mul(objectToWorldMatrix, float4(BurtGrassPivotPosOSFromVertexColor(vertexColor), 1.0f)).xyz;
}

float BurtGrassHeightFromVertexColor(float4 vertexColor)
{
    return saturate(vertexColor.r);
}

float BurtGrassWindNoiseShape(float2 value)
{
    float2 windUV = abs(frac(value + 0.5f) * 2.0f - 1.0f);
    return length(windUV * (3.0f - 2.0f * windUV) * windUV);
}

float BurtGrassWindNoiseIntensity(float3 pivotWS, float timeSeconds)
{
    float2 windDir = BurtTrunkWindDirectionWS().xz;
    windDir = lerp(float2(1.0f, 0.0f), windDir, step(BURT_EPSILON, dot(windDir, windDir)));
    windDir = normalize(windDir);

    float windTime = timeSeconds * 20.0f;
    float2 windScroll = windDir * windTime;
    float noise1 = BurtGrassWindNoiseShape(pivotWS.xz * 0.2f - 0.7f * windScroll);
    float2 windDir2 = normalize(windDir + float2(0.5f, 0.2f));
    float noise2 = BurtGrassWindNoiseShape(pivotWS.xz * 2.0f - 0.27f * windDir2 * windTime);
    return (noise1 * 1.2f + noise2) * 0.45f;
}

float3 BurtGrassCameraTiltOffsetWS(float4 positionOS, float3 positionWS, float3 pivotWS, float3 normalOS, float4x4 objectToWorldMatrix)
{
#if defined(BURT_GRASS_MATERIAL_ENABLED)
    if (_TiltingStrength <= 0.01f)
    {
        return float3(0.0f, 0.0f, 0.0f);
    }

    float3 normalWS = BurtSafeNormalize(mul((float3x3)objectToWorldMatrix, normalOS));
    float3 objectUpWS = BurtSafeNormalize(float3(objectToWorldMatrix._m01, objectToWorldMatrix._m11, objectToWorldMatrix._m21));
    float3 viewForwardWS = BurtSafeNormalize(float3(UNITY_MATRIX_I_V._m02, UNITY_MATRIX_I_V._m12, UNITY_MATRIX_I_V._m22));
    float3 pivotDirWS = BurtSafeNormalize(positionWS - pivotWS);
    float3 cameraOffsetDir = dot(pivotDirWS, normalWS) * normalWS;
    cameraOffsetDir.y = 0.0f;

    float cameraOffsetLengthSquared = dot(cameraOffsetDir, cameraOffsetDir);
    float valid = step(1e-10f, cameraOffsetLengthSquared);
    cameraOffsetDir *= rsqrt(cameraOffsetLengthSquared + 1e-16f);

    float upDotV = saturate(dot(objectUpWS, viewForwardWS));
    float upperMask = 1.0f - pow(1.0f - upDotV, 0.3f);
    float scaleY = max(length(float3(objectToWorldMatrix._m01, objectToWorldMatrix._m11, objectToWorldMatrix._m21)), BURT_EPSILON);
    return cameraOffsetDir * (_TiltingStrength * 0.2f / scaleY) * upperMask * valid;
#else
    return float3(0.0f, 0.0f, 0.0f);
#endif
}

float3 BurtGrassCalculateOffsetWS(
    float4 positionOS,
    float3 normalOS,
    float4 vertexColor,
    float4x4 objectToWorldMatrix,
    float timeSeconds,
    out float windNoiseIntensity)
{
    windNoiseIntensity = 0.0f;
#if defined(BURT_GRASS_MATERIAL_ENABLED)
    float3 positionWS = mul(objectToWorldMatrix, positionOS).xyz;
    float3 pivotWS = BurtGrassPivotPosWSFromVertexColor(vertexColor, objectToWorldMatrix);
    float heightMask = pow(max(BurtGrassHeightFromVertexColor(vertexColor), 0.0f), max(_WindHeightMask, 0.001f));

    float3 rotationForce = float3(0.0f, 0.0f, 0.0f);
    if (_WindStrength > 0.001f)
    {
        windNoiseIntensity = BurtGrassWindNoiseIntensity(pivotWS, timeSeconds);
        rotationForce += BurtTrunkWindDirectionWS() * windNoiseIntensity * heightMask * _WindStrength * 0.03f;
    }

    rotationForce += BurtGrassCameraTiltOffsetWS(positionOS, positionWS, pivotWS, normalOS, objectToWorldMatrix) * heightMask;

    float forceMagnitude = length(rotationForce);
    if (forceMagnitude <= BURT_EPSILON)
    {
        return float3(0.0f, 0.0f, 0.0f);
    }

    float3 forceDirection = rotationForce / forceMagnitude;
    float3 forceAxis = BurtSafeNormalize(cross(forceDirection, float3(0.0f, -1.0f, 0.0f)));
    return BurtTrunkRotateAboutAxisOffset(forceAxis, forceMagnitude, pivotWS, positionWS);
#else
    return float3(0.0f, 0.0f, 0.0f);
#endif
}

float4 BurtGetGrassAnimatedWorldPosition(float4 positionOS, float3 normalOS, float4 vertexColor, float4x4 objectToWorldMatrix, float timeSeconds)
{
    float windNoiseIntensity;
    float4 positionWS = mul(objectToWorldMatrix, positionOS);
    positionWS.xyz += BurtGrassCalculateOffsetWS(positionOS, normalOS, vertexColor, objectToWorldMatrix, timeSeconds, windNoiseIntensity);
    return positionWS;
}

float4 BurtApplyGrassVertexAnimationObjectSpace(float4 positionOS, float3 normalOS, float4 vertexColor, float timeSeconds)
{
#if defined(BURT_GRASS_MATERIAL_ENABLED)
    float4 animatedWorld = BurtGetGrassAnimatedWorldPosition(positionOS, normalOS, vertexColor, unity_ObjectToWorld, timeSeconds);
    return mul(unity_WorldToObject, animatedWorld);
#else
    return positionOS;
#endif
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

float3 BurtFoliageCalculateWindOffsetWS(float4 positionOS, float4 vertexColor, float4x4 objectToWorldMatrix, float timeSeconds)
{
#if defined(BURT_FOLIAGE_MATERIAL_ENABLED)
    float3 positionWS = mul(objectToWorldMatrix, positionOS).xyz;
    float3 pivotWS = mul(objectToWorldMatrix, float4(0.0f, 0.0f, 0.0f, 1.0f)).xyz;

    float maxBendAngle = max(_MaxBendAngle, 0.0f);
    float swayIntensity = max(_SwayIntensity, 0.0f);
    float flutterFrequency = max(_FlutterTipFrequency, 0.0f);
    float flutterIntensity = max(_FlutterTipIntensity, 0.0f);
    if (maxBendAngle <= BURT_EPSILON && swayIntensity <= BURT_EPSILON && flutterIntensity <= BURT_EPSILON)
    {
        return float3(0.0f, 0.0f, 0.0f);
    }

    float height = max(_TreeHeight, BURT_EPSILON);
    float heightMask = saturate(positionOS.y / height);
    float bendPower = _BendMaskPow > BURT_EPSILON ? _BendMaskPow : 1.0f;
    heightMask = pow(max(heightMask, 0.001f), max(bendPower, 0.001f));

    float trunkOffset = saturate(vertexColor.y);
    float distanceToTrunk = saturate(1.0f - vertexColor.z) * 0.99f + 0.01f;
    distanceToTrunk = pow(distanceToTrunk, max(_ToTrunkMaskPow, 0.001f));
    float leafTipMask = saturate(vertexColor.x);

    float windTime = timeSeconds * 20.0f;
    float3 windDirectionWS = BurtTrunkWindDirectionWS();
    float3 windOffsetWS = float3(0.0f, 0.0f, 0.0f);

    float windNoise = sin(dot(pivotWS.xz, float2(0.011f, 0.017f)) + windTime * 0.1f);
    float3 bendAxis = cross(float3(-windDirectionWS.x, 0.0f, -windDirectionWS.z), float3(0.0f, 1.0f, 0.0f));
    windOffsetWS += BurtTrunkRotateAboutAxisOffset(bendAxis, windNoise * heightMask * 0.0035f * maxBendAngle, pivotWS, positionWS);

    float pivotOffset = (pivotWS.x + pivotWS.y + pivotWS.z) * 70.0f + BurtTrunkHash31(pivotWS) * 6.2831853f;
    float positionOffset = (positionWS.x + positionWS.z) * 0.07f;
    float frequencyA = sin(positionOffset + pivotOffset + windTime * 0.11f + trunkOffset) * 1.5f;
    float frequencyB = cos(positionOffset + pivotOffset + windTime * 0.385f * max(trunkOffset, 0.001f) + trunkOffset);
    float3 branchSwayDirection = float3(windDirectionWS.x, (frequencyA + frequencyB * 0.5f) * 0.3f, windDirectionWS.z);
    float branchSwayWave = (frequencyA + frequencyB) * 0.5f + 0.2f;
    windOffsetWS += branchSwayDirection * saturate(distanceToTrunk) * swayIntensity * branchSwayWave * 0.1f;

    float flutterPhase = dot(positionWS.xz, float2(0.10f, 0.17f)) + windTime * max(flutterFrequency, 0.001f) + trunkOffset * 6.2831853f;
    float flutterNoise = sin(flutterPhase) * cos(flutterPhase * 1.7f + pivotOffset);
    float3 flutterDirectionWS = BurtSafeNormalize(float3(windDirectionWS.x, 3.0f, windDirectionWS.z));
    float flutterMask = (distanceToTrunk * 0.5f + 0.5f) * heightMask * leafTipMask;
    windOffsetWS += flutterDirectionWS * flutterMask * flutterIntensity * flutterNoise * 0.025f;

    return windOffsetWS;
#else
    return float3(0.0f, 0.0f, 0.0f);
#endif
}

float4 BurtGetFoliageAnimatedWorldPosition(float4 positionOS, float4 vertexColor, float4x4 objectToWorldMatrix, float timeSeconds)
{
    float4 positionWS = mul(objectToWorldMatrix, positionOS);
    positionWS.xyz += BurtFoliageCalculateWindOffsetWS(positionOS, vertexColor, objectToWorldMatrix, timeSeconds);
    return positionWS;
}

float4 BurtApplyFoliageVertexAnimationObjectSpace(float4 positionOS, float4 vertexColor, float timeSeconds)
{
#if defined(BURT_FOLIAGE_MATERIAL_ENABLED)
    float4 animatedWorld = BurtGetFoliageAnimatedWorldPosition(positionOS, vertexColor, unity_ObjectToWorld, timeSeconds);
    return mul(unity_WorldToObject, animatedWorld);
#else
    return positionOS;
#endif
}

#endif // BURT_TRUNK_VERTEX_ANIMATION_INCLUDED
