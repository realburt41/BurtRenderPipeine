#ifndef BURT_DEFERRED_LIGHTING_OUTPUT_INCLUDED
#define BURT_DEFERRED_LIGHTING_OUTPUT_INCLUDED
float _BurtDeferredSubsurfaceDiffuseLuminanceOutputEnabled;
float BurtEvaluateDeferredOutputAlpha(BurtPBRShadingComponents Components)
{
#if defined(BURT_DEFERRED_SHADING_MODEL_SUBSURFACE)
    if (_BurtDeferredSubsurfaceDiffuseLuminanceOutputEnabled < 0.5f)
    {
        return 1.0f;
    }

    float3 DiffuseLighting =
        Components.DirectDiffuse +
        Components.IndirectDiffuse;
    return dot(BurtApplyPreExposure(DiffuseLighting), float3(0.3f, 0.59f, 0.11f));
#else
    return 1.0f;
#endif
}
#endif
