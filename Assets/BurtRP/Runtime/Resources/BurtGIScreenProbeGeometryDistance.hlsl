#ifndef BURT_GI_SCREEN_PROBE_GEOMETRY_DISTANCE_INCLUDED
#define BURT_GI_SCREEN_PROBE_GEOMETRY_DISTANCE_INCLUDED

// TraceGeometryDistance is RGBA32F: x=nearest geometry/filter-support state,
// y=remaining geometric visibility inside the traced scene range,
// z=fraction not yet supplied by cache/local-environment fallbacks,
// w=fresh Voxel angular coverage within the finite traced range (hit OR valid miss).
// y is NOT a claim of visibility beyond scene coverage. Never derive y/z from
// lighting confidence or emitted brightness. Reconstruction consumes x only.
float4 BurtGIInitialTraceState() { return float4(-1.0f, 1.0f, 1.0f, 0.0f); }
float BurtGISkyRemainingWeight(float4 state) { return saturate(state.y) * saturate(state.z); }
// A valid empty Voxel direction can receive sky beyond the traced range, but
// must not receive the cached version of the same finite-range incident light.
float BurtGICacheRemainingWeight(float4 state)
{
    return min(saturate(state.y), 1.0f - saturate(state.w)) * saturate(state.z);
}

// Independent of TraceHit (confidence) and radiance. Nonnegative values are
// world-space distances from the unbiased probe position to observed geometry.
// Voxel cones retain the nearest observed point across their lanes; this is a
// conservative geometric bound, NOT the confidence-winning lane's distance.
// Negative values never imply that a direction is unoccluded to infinity.
#define BURT_GI_GEOMETRY_UNKNOWN (-1.0f)
#define BURT_GI_GEOMETRY_UNRESOLVED (-2.0f) // attempted trace, no accepted geometry
#define BURT_GI_GEOMETRY_EMPTY (-3.0f) // no ray rebinned into this gather texel
#define BURT_GI_GEOMETRY_RANGE_COMPLETE (-4.0f) // finite cone range sampled inside valid coverage, no geometry
#define BURT_GI_FILTER_SUPPORT_CACHE (-5.0f) // resolved cache lookup, no physical hit distance
#define BURT_GI_FILTER_SUPPORT_SKY (-6.0f) // resolved sky lookup, including valid black
#define BURT_GI_FILTER_SUPPORT_MIXED (-7.0f) // gathered resolved support with different provenance
// RANGE_COMPLETE is not a geometric hit or an assertion of visibility beyond
// the configured trace range. Step exhaustion and coverage holes stay unresolved.

bool BurtGIHasResolvedFilterSupport(float state)
{
    return state == BURT_GI_GEOMETRY_RANGE_COMPLETE || state == BURT_GI_FILTER_SUPPORT_CACHE ||
        state == BURT_GI_FILTER_SUPPORT_SKY || state == BURT_GI_FILTER_SUPPORT_MIXED;
}

// A proxy supports spatial reconstruction only; it must never become geometry
// metadata or an opaque hit. XRender uses its configured maximum trace distance
// for cache depth-unavailable and sky/miss branches.
float BurtGIResolveFilterSupportDistance(float state, float maxTraceDistance)
{
    if (state >= 0.0f && isfinite(state)) return state;
    if (BurtGIHasResolvedFilterSupport(state) && isfinite(maxTraceDistance) && maxTraceDistance > 0.0f)
        // Only Voxel tracing imposes the one-unit minimum. Cache/sky do not;
        // mixed provenance uses the smaller configured support conservatively.
        return state == BURT_GI_GEOMETRY_RANGE_COMPLETE ? max(maxTraceDistance, 1.0f) : maxTraceDistance;
    return BURT_GI_GEOMETRY_UNKNOWN;
}

float BurtGIMergeResolvedFilterSupport(float currentState, float resolvedState)
{
    if (!BurtGIHasResolvedFilterSupport(resolvedState)) return currentState;
    if ((currentState >= 0.0f && isfinite(currentState)) || currentState == BURT_GI_GEOMETRY_RANGE_COMPLETE)
        return currentState;
    return BurtGIHasResolvedFilterSupport(currentState) && currentState != resolvedState
        ? BURT_GI_FILTER_SUPPORT_MIXED : resolvedState;
}

float BurtGIMergeGeometryDistance(float currentDistance, float candidateDistance)
{
    if (candidateDistance >= 0.0f && isfinite(candidateDistance))
        return currentDistance >= 0.0f ? min(currentDistance, candidateDistance) : candidateDistance;
    if (candidateDistance == BURT_GI_GEOMETRY_RANGE_COMPLETE && currentDistance < 0.0f)
        return candidateDistance;
    return currentDistance == BURT_GI_GEOMETRY_UNKNOWN ? candidateDistance : currentDistance;
}
#endif
