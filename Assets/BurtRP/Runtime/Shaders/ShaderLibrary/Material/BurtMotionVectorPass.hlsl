// Shared material motion-vector pass for BurtRP opaque/cutout Lit-family shaders.
#ifndef BURT_MOTION_VECTOR_PASS_INCLUDED
#define BURT_MOTION_VECTOR_PASS_INCLUDED

sampler2D _BaseMap;
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

struct MotionVectorAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv0 : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct MotionVectorVaryings
{
    float4 positionCS : SV_POSITION;
    float4 currentClip : TEXCOORD0;
    float4 currentClipNoJitter : TEXCOORD1;
    float4 previousClipNoJitter : TEXCOORD2;
    float2 baseMapUV : TEXCOORD3;
    float sourceConfidence : TEXCOORD4;
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

    float4 positionOS = BurtApplyMultipassObjectShellOffset(input.positionOS, input.normalOS);
    float4 currentWorld = mul(unity_ObjectToWorld, positionOS);
    float4 previousObjectWorld = mul(unity_MatrixPreviousM, positionOS);

    float forceNoMotion = step(unity_MotionVectorsParams.y, 0.5);
    float cameraMotion = step(unity_MotionVectorsParams.w, 0.5);
    float allowObjectMotion = (1.0 - forceNoMotion) * (1.0 - cameraMotion);
    float3 objectDelta = previousObjectWorld.xyz - currentWorld.xyz;
    float objectMoved = step(1e-8, dot(objectDelta, objectDelta)) * allowObjectMotion;
    float4 previousWorld = lerp(currentWorld, previousObjectWorld, objectMoved);

    MotionVectorVaryings output;
    output.positionCS = mul(_BurtTAACurrentViewProjection, currentWorld);
#if defined(UNITY_REVERSED_Z)
    output.positionCS.z -= unity_MotionVectorsParams.z * output.positionCS.w;
#else
    output.positionCS.z += unity_MotionVectorsParams.z * output.positionCS.w;
#endif

    output.currentClip = output.positionCS;
    output.currentClipNoJitter = mul(_BurtTAACurrentNonJitteredViewProjection, currentWorld);
    output.previousClipNoJitter = mul(_BurtTAAPreviousNonJitteredViewProjection, previousWorld);
    output.baseMapUV = BurtMotionVectorTransformBaseMapUV(input.uv0);
    output.sourceConfidence = objectMoved;
    return output;
}

float4 FragMotionVector(MotionVectorVaryings input) : SV_Target
{
    float4 baseColor = BurtMotionVectorSampleBaseMap(input.baseMapUV) * _BaseColor;
    BurtMotionVectorApplyAlphaClip(baseColor.a);
    clip(input.sourceConfidence - 0.5);

    float currentRawDepth = BurtTaaDeviceDepth(input.currentClip);
    float surfaceValid = BurtTaaValidSurfaceWeight(currentRawDepth);
    float2 currentScreenUv = BurtTaaClipToUv(input.currentClip);
    float2 currentUv = BurtTaaClipToUv(input.currentClipNoJitter);
    float2 previousUv = BurtTaaClipToUv(input.previousClipNoJitter);

    float currentInBounds = step(0.0, currentScreenUv.x) * step(currentScreenUv.x, 1.0) * step(0.0, currentScreenUv.y) * step(currentScreenUv.y, 1.0);
    float motionInBounds = step(0.0, currentUv.x) * step(currentUv.x, 1.0) * step(0.0, currentUv.y) * step(currentUv.y, 1.0);
    float previousAvailable = step(1e-5, input.previousClipNoJitter.w);
    previousAvailable *= step(0.0, previousUv.x) * step(previousUv.x, 1.0) * step(0.0, previousUv.y) * step(previousUv.y, 1.0);

    float sceneRawDepth = tex2D(_BurtTAACurrentDepthTexture, saturate(currentScreenUv)).r;
    float currentEyeDepth = LinearEyeDepth(currentRawDepth);
    float sceneEyeDepth = LinearEyeDepth(sceneRawDepth);
    float depthTolerance = max(currentEyeDepth * 0.018, 0.035);
    float visible = surfaceValid * currentInBounds * motionInBounds * step(abs(currentEyeDepth - sceneEyeDepth), depthTolerance);
    clip(visible - 0.5);

    if (previousAvailable < 0.5)
    {
        return float4(2.0, 2.0, 1.0, 0.0);
    }

    float2 velocity = previousUv - currentUv;
    float2 velocityPixels = abs(velocity * _BurtTAATexelSize.zw);
    float keepVelocity = step(0.02, max(velocityPixels.x, velocityPixels.y));
    clip(keepVelocity - 0.5);
    return float4(velocity, 1.0, 1.0);
}

#endif // BURT_MOTION_VECTOR_PASS_INCLUDED
