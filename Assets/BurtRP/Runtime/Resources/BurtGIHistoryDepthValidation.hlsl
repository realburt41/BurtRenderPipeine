#ifndef BURT_GI_HISTORY_DEPTH_VALIDATION_INCLUDED
#define BURT_GI_HISTORY_DEPTH_VALIDATION_INCLUDED

// Previous SceneColor and depth must describe the same surface as the hit.
// Raw device-depth tolerances alone admit metre-scale disocclusions far from
// the near plane. Use the projection belonging to that history capture, not
// current _ZBufferParams: this also handles orthographic and oblique cameras.
bool BurtGIHistoryDepthMatches(
    float2 historyUV, float expectedRawDepth, float sampledRawDepth,
    float relativeThickness, float noise, float4x4 previousInverseProjection)
{
    float tolerance = max(relativeThickness, 0.0001) * lerp(0.5, 2.0, noise);
    // Retain XRender's raw-depth rejection as an additional conservative gate.
    if (abs(sampledRawDepth - expectedRawDepth) >= tolerance)
        return false;

    float2 clipXY = historyUV * 2.0 - 1.0;
#if UNITY_UV_STARTS_AT_TOP
    clipXY.y = -clipXY.y;
#endif
    float2 clipDepth = float2(expectedRawDepth, sampledRawDepth);
#if !defined(UNITY_REVERSED_Z)
    clipDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, clipDepth);
#endif
    float4 expectedVS = mul(previousInverseProjection, float4(clipXY, clipDepth.x, 1.0));
    float4 sampledVS = mul(previousInverseProjection, float4(clipXY, clipDepth.y, 1.0));
    if (abs(expectedVS.w) < 1.0e-8 || abs(sampledVS.w) < 1.0e-8)
        return false;
    float expectedEyeDepth = -expectedVS.z / expectedVS.w;
    float sampledEyeDepth = -sampledVS.z / sampledVS.w;
    if (!all(isfinite(float2(expectedEyeDepth, sampledEyeDepth))) ||
        expectedEyeDepth <= 0.0 || sampledEyeDepth <= 0.0)
        return false;
    return abs(sampledEyeDepth - expectedEyeDepth) < tolerance * expectedEyeDepth;
}

#endif
