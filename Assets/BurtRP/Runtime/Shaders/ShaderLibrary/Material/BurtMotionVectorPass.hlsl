// Shared material motion-vector pass for BurtRP opaque/cutout Lit-family shaders.
#ifndef BURT_MOTION_VECTOR_PASS_INCLUDED
#define BURT_MOTION_VECTOR_PASS_INCLUDED

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtTrunkVertexAnimation.hlsl"

#if defined(BURT_MATERIAL_SHADING_MODEL_HAIR) || defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
// BurtHairDither normally consumes the material-pass alpha-clip helper.  Motion
// vectors are deliberately standalone, so provide the same contract locally
// while importing the one shared dither implementation used by depth/GBuffer.
void BurtMotionVectorHairApplyAlphaClip(float alpha, float alphaClip, float cutoff)
{
    #if defined(BURT_ALPHA_CLIP)
        clip(alpha - cutoff);
    #else
        if (alphaClip > 0.5)
        {
            clip(alpha - cutoff);
        }
    #endif
}
#define BurtApplyAlphaClip BurtMotionVectorHairApplyAlphaClip
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtHairDither.hlsl"
#undef BurtApplyAlphaClip
#endif

sampler2D _BaseMap;
sampler2D _AlphaMap;
sampler2D _BurtTAACurrentDepthTexture;
float4x4 _BurtTAACurrentViewProjection;
float4x4 _BurtTAACurrentNonJitteredViewProjection;
float4x4 _BurtTAAPreviousNonJitteredViewProjection;
float4x4 _BurtTAAClipToPreviousClip;
float4x4 unity_MatrixPreviousM;
float4 unity_MotionVectorsParams;
float _BurtTAAPreviousRenderDeltaTime;

float4x4 BurtGetMotionVectorPreviousObjectToWorldMatrix()
{
#if defined(UNITY_INSTANCING_ENABLED)
    return UNITY_ACCESS_INSTANCED_PROP(unity_Builtins3, unity_PrevObjectToWorldArray);
#else
    return unity_MatrixPreviousM;
#endif
}
float4 _BurtTAATexelSize;
float4 _BurtTAAJitter;

float2 BurtMotionVectorTransformBaseMapUV(float2 uv0)
{
#if defined(BURT_MATERIAL_SHADING_MODEL_HAIR) || defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    // AvatarHair samples its opacity from the untransformed UV in the visible,
    // depth and shadow passes.  Motion coverage must use that exact footprint.
    return uv0;
#else
    return uv0 * _BaseMap_ST.xy + _BaseMap_ST.zw;
#endif
}

float4 BurtMotionVectorSampleBaseMap(float2 uv)
{
    return tex2D(_BaseMap, uv);
}

void BurtMotionVectorApplyAlphaClip(float alpha, float4 positionCS)
{
#if defined(BURT_MATERIAL_SHADING_MODEL_HAIR) || defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    BurtApplyHairDitherAlphaClip(alpha, _AlphaClip, _Cutoff, positionCS);
#else
    #if defined(BURT_ALPHA_CLIP)
        clip(alpha - _Cutoff);
    #else
        if (_AlphaClip > 0.5)
        {
            clip(alpha - _Cutoff);
        }
    #endif
#endif
}

float BurtMotionVectorEvaluateOpacity(float alpha, float2 baseMapUV, float3 positionWS)
{
#if defined(BURT_MATERIAL_SHADING_MODEL_FOLIAGE) || defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
    #if defined(BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS)
        // Match the visible/depth Grass pass.  Omitting its fixed negative mip
        // bias can discard motion on pixels that still contribute color/depth.
        float alphaMap = tex2Dbias(_AlphaMap, float4(baseMapUV, 0.0f, -1.0f)).r;
    #else
        float alphaMap = tex2D(_AlphaMap, baseMapUV).r;
    #endif
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
    // Unity/XRender populate UV4 with the previous skinned position when deformation motion vectors are enabled.
    float3 PreviousPositionOS : TEXCOORD4;
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
    float3 PositionWS : TEXCOORD4;
};

float2 BurtTaaClipToUv(float4 clipPosition)
{
    float safeW = abs(clipPosition.w) > 1e-6
        ? clipPosition.w
        : (clipPosition.w < 0.0 ? -1e-6 : 1e-6);
    float2 ndc = clipPosition.xy / safeW;
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
    float4x4 previousObjectToWorld = BurtGetMotionVectorPreviousObjectToWorldMatrix();
    float hasDeformation = step(1e-6, unity_MotionVectorsParams.x);
    float4 previousPositionOS = float4(lerp(positionOS.xyz, input.PreviousPositionOS, hasDeformation), positionOS.w);
    #if defined(BURT_MATERIAL_SHADING_MODEL_TRUNK) || defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_TRUNK)
        // Use this camera's actual previous render interval, equivalent to
        // XRender's _LastTimeParameters.x, rather than the global frame delta.
        float previousTimeSeconds = max(_Time.y - _BurtTAAPreviousRenderDeltaTime, 0.0f);
        float4 currentWorld = BurtGetTrunkAnimatedWorldPosition(positionOS, input.Color, unity_ObjectToWorld, _Time.y);
        float4 previousObjectWorld = BurtGetTrunkAnimatedWorldPosition(previousPositionOS, input.Color, previousObjectToWorld, previousTimeSeconds);
    #elif defined(BURT_MATERIAL_SHADING_MODEL_FOLIAGE) || defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
        float previousTimeSeconds = max(_Time.y - _BurtTAAPreviousRenderDeltaTime, 0.0f);
        #if defined(BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS)
        float4 currentWorld = BurtGetGrassAnimatedWorldPosition(positionOS, input.NormalOS, input.Color, unity_ObjectToWorld, _Time.y);
        float4 previousObjectWorld = BurtGetGrassAnimatedWorldPosition(previousPositionOS, input.NormalOS, input.Color, previousObjectToWorld, previousTimeSeconds);
        #else
        float4 currentWorld = BurtGetFoliageAnimatedWorldPosition(positionOS, input.Color, unity_ObjectToWorld, _Time.y);
        float4 previousObjectWorld = BurtGetFoliageAnimatedWorldPosition(previousPositionOS, input.Color, previousObjectToWorld, previousTimeSeconds);
        #endif
    #else
        float4 currentWorld = mul(unity_ObjectToWorld, positionOS);
        float4 previousObjectWorld = mul(previousObjectToWorld, previousPositionOS);
    #endif

    // Use the same motion-source contract as XRender's GBuffer path.
    float cameraMotion = 1.0 - step(1e-6, abs(unity_MotionVectorsParams.w));
    float4 previousWorld = lerp(previousObjectWorld, currentWorld, cameraMotion);

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
    output.PositionWS = currentWorld.xyz;
    return output;
}

float4 FragMotionVector(MotionVectorVaryings input) : SV_Target
{
    float4 baseColor = BurtMotionVectorSampleBaseMap(input.BaseMapUV) * _BaseColor;
    float alpha = BurtMotionVectorEvaluateOpacity(baseColor.a, input.BaseMapUV, input.PositionWS);
    BurtMotionVectorApplyAlphaClip(alpha, input.PositionCS);

    // XRender stamps its motion-vector stencil bit on every visible opaque
    // surface.  Static renderers still own a valid camera-motion vector; they
    // must not be clipped merely because their object transform did not move.
    // Raster coverage plus ZTest Equal already provide the current-surface
    // validity that this auxiliary pass needs.
    float2 currentRasterUv = input.PositionCS.xy * _BurtTAATexelSize.xy;
    float2 currentClipXY = currentRasterUv * 2.0 - 1.0;
#if UNITY_UV_STARTS_AT_TOP
    currentClipXY.y = -currentClipXY.y;
#endif
    float2 currentJitter = _BurtTAAJitter.xy;
#if UNITY_UV_STARTS_AT_TOP
    currentJitter.y = -currentJitter.y;
#endif
    currentClipXY -= currentJitter;
    float2 currentUv = BurtTaaClipToUv(float4(currentClipXY, 0.0, 1.0));
    // Match XRender CommonPass: static/camera motion is reconstructed from the
    // current pixel's device depth. Only real object/deformation motion uses
    // the interpolated previous-object endpoint.
    float4 cameraPreviousClip = mul(
        _BurtTAAClipToPreviousClip,
        float4(currentClipXY, input.PositionCS.z, 1.0));
    float cameraMotion = 1.0 - step(1e-6, abs(unity_MotionVectorsParams.w));
    float4 previousClip = lerp(input.PreviousClipNoJitter, cameraPreviousClip, cameraMotion);
    float2 previousUv = BurtTaaClipToUv(previousClip);

    float2 velocity = currentUv - previousUv;
    float2 velocityPixels = abs(velocity * _BurtTAATexelSize.zw);
    velocity *= step(float2(0.02, 0.02), velocityPixels);
    return float4(velocity, 1.0, 1.0);
}

// Responsive AA is an independent history-control signal.  It must survive
// platforms where the native depth-stencil buffer cannot be sampled, so the
// dedicated material pass writes a separate binary mask which TAA merges with
// its object-motion bit.  Keeping this binary avoids corrupting bit values on
// overlapping transparent geometry or multipass shells.
#if defined(BURT_MOTION_VECTOR_RESPONSIVE_AA_MASK)
float4 FragResponsiveAAMask(MotionVectorVaryings input) : SV_Target
{
    float4 baseColor = BurtMotionVectorSampleBaseMap(input.BaseMapUV) * _BaseColor;
    float alpha = BurtMotionVectorEvaluateOpacity(baseColor.a, input.BaseMapUV, input.PositionWS);
    BurtMotionVectorApplyAlphaClip(alpha, input.PositionCS);
    float responsiveAA = _ResponsiveAA;
#if defined(BURT_DEFAULT_LIT_FORCE_TRANSPARENT_RESPONSIVE_AA)
    responsiveAA = max(responsiveAA, step(0.5, _Surface));
#endif
    clip(responsiveAA - 0.5);
    return float4(1.0, 0.0, 0.0, 0.0);
}
#endif

#endif // BURT_MOTION_VECTOR_PASS_INCLUDED
