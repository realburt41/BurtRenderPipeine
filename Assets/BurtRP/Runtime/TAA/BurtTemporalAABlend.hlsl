#ifndef BURT_TEMPORAL_AA_BLEND_INCLUDED
#define BURT_TEMPORAL_AA_BLEND_INCLUDED

// Native TAA only. Preserve XRender's static luma-contrast blend; under
// motion, refresh high-contrast history sooner to limit resampling blur.
// Shared by compute, raster fallback and Feedback diagnostics.
float BurtTaaMotionAwareCurrentBlend(float staticBlend, float motionPixels)
{
    return lerp(staticBlend, max(staticBlend, 0.2), saturate(motionPixels));
}

// Native TAA extension to XRender's perceptual variance clip. Its offset
// YCoCg chroma is divided by (1 + Y), so a black/white neighborhood spans a
// wide chroma AABB even though it contains no color. Constrain history in
// uncompressed YCoCg as well, retaining the existing clipped luminance.
float3 BurtTaaConstrainHistoryChroma(float3 perceptualHistory, float2 chromaMin, float2 chromaMax)
{
    float inversePerception = rcp(max(1.0 - perceptualHistory.x, 6.103515625e-5));
    float2 chroma = perceptualHistory.yz * inversePerception;
    chroma = clamp(chroma, chromaMin - 6.103515625e-5, chromaMax + 6.103515625e-5);
    perceptualHistory.yz = chroma / inversePerception;
    return perceptualHistory;
}

#endif
