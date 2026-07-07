// Shared material motion-vector pass for BurtRP opaque/cutout Lit-family shaders.
#ifndef BURT_MOTION_VECTOR_PASS_INCLUDED
#define BURT_MOTION_VECTOR_PASS_INCLUDED

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtTrunkVertexAnimation.hlsl"

sampler2D _BaseMap;
sampler2D _AlphaMap;
sampler2D _BurtTAACurrentDepthTexture;
float4x4 _BurtTAACurrentViewProjection;
float4x4 _BurtTAACurrentNonJitteredViewProjection;
float4x4 _BurtTAAPreviousNonJitteredViewProjection;
float4x4 unity_MatrixPreviousM;
float4 unity_MotionVectorsParams;
float4 _BurtTAATexelSize;

float2 BurtMotionVectorTransformBaseMapUV(float2 uv0)
{
    return uv0 * _BaseMap_ST.xy + _BaseMap_ST.zw;
}

float4 BurtMotionVectorSampleBaseMap(float2 uv)
{
    return tex2D(_BaseMap, uv);
}

void BurtMotionVectorApplyAlphaClip(float alpha)
{
    #if defined(BURT_ALPHA_CLIP)
        clip(alpha - _Cutoff);
    #else
        if (_AlphaClip > 0.5)
        {
            clip(alpha - _Cutoff);
        }
    #endif
}

float BurtMotionVectorEvaluateOpacity(float alpha, float2 baseMapUV, float3 positionWS)
{
#if defined(BURT_MATERIAL_SHADING_MODEL_FOLIAGE) || defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
    float alphaMap = tex2D(_AlphaMap, baseMapUV).r;
    float distanceToCamera = distance(_WorldSpaceCameraPos.xyz, positionWS);
    #if defined(BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS)
        float distanceFactor = saturate(distanceToCamera / 150.0f);
    #else
        float distanceFactor = saturate((distanceToCamera - 20.0f) / 200.0f);
    #endif
    return saturate(alphaMap + alphaMap * distanceFactor * max(_AlphaIncrease, 0.0f));
#else
    return alpha;
#endif
}

struct MotionVectorAttributes
{
    float4 PositionOS : POSITION;
    float3 NormalOS : NORMAL;
    float2 UV0 : TEXCOORD0;
    #if defined(BURT_MATERIAL_SHADING_MODEL_TRUNK) || defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_TRUNK) || defined(BURT_MATERIAL_SHADING_MODEL_FOLIAGE) || defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
        float4 Color : COLOR;
    #endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct MotionVectorVaryings
{
    float4 PositionCS : SV_POSITION;
    float4 CurrentClip : TEXCOORD0;
    float4 CurrentClipNoJitter : TEXCOORD1;
    float4 PreviousClipNoJitter : TEXCOORD2;
    float2 BaseMapUV : TEXCOORD3;
    float SourceConfidence : TEXCOORD4;
    float3 PositionWS : TEXCOORD5;
};

float2 BurtTaaClipToUv(float4 clipPosition)
{
    float2 ndc = clipPosition.xy / max(abs(clipPosition.w), 1e-6);
    float2 uv = ndc * 0.5 + 0.5;
#if UNITY_UV_STARTS_AT_TOP
    uv.y = 1.0 - uv.y;
#endif

    return uv;
}

float BurtTaaValidSurfaceWeight(float rawDepth)
{
#if defined(UNITY_REVERSED_Z)
    return step(1e-6, rawDepth);
#else
    return 1.0 - step(1.0 - 1e-6, rawDepth);
#endif
}

float BurtTaaDeviceDepth(float4 clipPosition)
{
    return clipPosition.z / max(abs(clipPosition.w), 1e-6);
}

MotionVectorVaryings VertMotionVector(MotionVectorAttributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);

    float4 positionOS = BurtApplyMultipassObjectShellOffset(input.PositionOS, input.NormalOS);
    #if defined(BURT_MATERIAL_SHADING_MODEL_TRUNK) || defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_TRUNK)
        float previousTimeSeconds = max(_Time.y - unity_DeltaTime.x, 0.0f);
        float4 currentWorld = BurtGetTrunkAnimatedWorldPosition(positionOS, input.Color, unity_ObjectToWorld, _Time.y);
        float4 previousObjectWorld = BurtGetTrunkAnimatedWorldPosition(positionOS, input.Color, unity_MatrixPreviousM, previousTimeSeconds);
    #elif defined(BURT_MATERIAL_SHADING_MODEL_FOLIAGE) || defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
        float previousTimeSeconds = max(_Time.y - unity_DeltaTime.x, 0.0f);
        #if defined(BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS)
        float4 currentWorld = BurtGetGrassAnimatedWorldPosition(positionOS, input.NormalOS, input.Color, unity_ObjectToWorld, _Time.y);
        float4 previousObjectWorld = BurtGetGrassAnimatedWorldPosition(positionOS, input.NormalOS, input.Color, unity_MatrixPreviousM, previousTimeSeconds);
        #else
        float4 currentWorld = BurtGetFoliageAnimatedWorldPosition(positionOS, input.Color, unity_ObjectToWorld, _Time.y);
        float4 previousObjectWorld = BurtGetFoliageAnimatedWorldPosition(positionOS, input.Color, unity_MatrixPreviousM, previousTimeSeconds);
        #endif
    #else
        float4 currentWorld = mul(unity_ObjectToWorld, positionOS);
        float4 previousObjectWorld = mul(unity_MatrixPreviousM, positionOS);
    #endif

    float materialVertexAnimation = 0.0;
    #if defined(BURT_MATERIAL_SHADING_MODEL_TRUNK) || defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_TRUNK)
        materialVertexAnimation = step(BURT_EPSILON, max(_MaxBendAngle, _SwayIntensity));
    #elif defined(BURT_MATERIAL_SHADING_MODEL_FOLIAGE) || defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
        #if defined(BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS)
        materialVertexAnimation = step(BURT_EPSILON, max(_WindStrength, _TiltingStrength));
        #else
        materialVertexAnimation = step(BURT_EPSILON, max(max(_MaxBendAngle, _SwayIntensity), _FlutterTipIntensity));
        #endif
    #endif

    float forceNoMotion = step(unity_MotionVectorsParams.y, 0.5);
    float cameraMotion = step(unity_MotionVectorsParams.w, 0.5);
    float allowObjectMotion = max((1.0 - forceNoMotion) * (1.0 - cameraMotion), materialVertexAnimation);
    float3 objectDelta = previousObjectWorld.xyz - currentWorld.xyz;
    float objectMoved = step(1e-8, dot(objectDelta, objectDelta)) * allowObjectMotion;
    float4 previousWorld = lerp(currentWorld, previousObjectWorld, objectMoved);

    MotionVectorVaryings output;
    output.PositionCS = mul(_BurtTAACurrentViewProjection, currentWorld);
#if defined(UNITY_REVERSED_Z)
    output.PositionCS.z -= unity_MotionVectorsParams.z * output.PositionCS.w;
#else
    output.PositionCS.z += unity_MotionVectorsParams.z * output.PositionCS.w;
#endif

    output.CurrentClip = output.PositionCS;
    output.CurrentClipNoJitter = mul(_BurtTAACurrentNonJitteredViewProjection, currentWorld);
    output.PreviousClipNoJitter = mul(_BurtTAAPreviousNonJitteredViewProjection, previousWorld);
    output.BaseMapUV = BurtMotionVectorTransformBaseMapUV(input.UV0);
    output.SourceConfidence = objectMoved;
    output.PositionWS = currentWorld.xyz;
    return output;
}

float4 FragMotionVector(MotionVectorVaryings input) : SV_Target
{
    float4 baseColor = BurtMotionVectorSampleBaseMap(input.BaseMapUV) * _BaseColor;
    float alpha = BurtMotionVectorEvaluateOpacity(baseColor.a, input.BaseMapUV, input.PositionWS);
    BurtMotionVectorApplyAlphaClip(alpha);
    clip(input.SourceConfidence - 0.5);

    float currentRawDepth = BurtTaaDeviceDepth(input.CurrentClip);
    float surfaceValid = BurtTaaValidSurfaceWeight(currentRawDepth);
    float2 currentScreenUv = BurtTaaClipToUv(input.CurrentClip);
    float2 currentUv = BurtTaaClipToUv(input.CurrentClipNoJitter);
    float2 previousUv = BurtTaaClipToUv(input.PreviousClipNoJitter);

    float currentInBounds = step(0.0, currentScreenUv.x) * step(currentScreenUv.x, 1.0) * step(0.0, currentScreenUv.y) * step(currentScreenUv.y, 1.0);
    float motionInBounds = step(0.0, currentUv.x) * step(currentUv.x, 1.0) * step(0.0, currentUv.y) * step(currentUv.y, 1.0);
    float previousAvailable = step(1e-5, input.PreviousClipNoJitter.w);
    previousAvailable *= step(0.0, previousUv.x) * step(previousUv.x, 1.0) * step(0.0, previousUv.y) * step(previousUv.y, 1.0);

    float sceneRawDepth = tex2D(_BurtTAACurrentDepthTexture, saturate(currentScreenUv)).r;
    float currentEyeDepth = LinearEyeDepth(currentRawDepth);
    float sceneEyeDepth = LinearEyeDepth(sceneRawDepth);
    float depthTolerance = max(currentEyeDepth * 0.018, 0.035);
    float visible = surfaceValid * currentInBounds * motionInBounds * step(abs(currentEyeDepth - sceneEyeDepth), depthTolerance);
    clip(visible - 0.5);

    clip(previousAvailable - 0.5);

    float2 velocity = currentUv - previousUv;
    float2 velocityPixels = abs(velocity * _BurtTAATexelSize.zw);
    velocity *= step(float2(0.02, 0.02), velocityPixels);
    clip(max(abs(velocity.x), abs(velocity.y)) - 1e-8);
    return float4(velocity, 1.0, 1.0);
}

#endif // BURT_MOTION_VECTOR_PASS_INCLUDED
