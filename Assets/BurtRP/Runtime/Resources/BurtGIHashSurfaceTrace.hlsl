#ifndef BURT_GI_HASH_SURFACE_TRACE_INCLUDED
#define BURT_GI_HASH_SURFACE_TRACE_INCLUDED

// Shared sparse-surface reader for Screen Probe and ClipMap fallback.
void BurtGISurfaceCacheTraceCell(BurtGIHashSurfaceAddress address, float3 samplePoint,
    float3 direction, float intervalLength, inout float closest, inout float4 radiance)
{
    uint local = address.cell.x + address.cell.y * (uint)_BurtGIRadianceCacheHashGridParams0.y;
    uint start = BurtGISurfaceCacheStart(address.hash, local);
    float tolerance = max(1.0e-5, address.cellSize * 1.0e-4);
    [loop] for (uint i = 0u; i < min(_BurtGISurfaceCacheSlotCount, _BurtGISurfaceCacheProbeCount); ++i)
    {
        uint slot = BurtGISurfaceCacheSlot(start, i);
        uint4 metadata = _BurtGISurfaceCacheTable[slot * 2u];
        if (metadata.x != address.hash || metadata.y == 0u || (metadata.y & 63u) != local ||
            (metadata.z & 0x40000000u) == 0u) continue;
        float3 normal = BurtGISurfaceCacheNormal(metadata.z);
        float denominator = dot(normal, direction);
        // Oct15 decoding perturbs an exactly tangent normal by roughly a code
        // step. Do not manufacture a plane crossing by dividing by that error.
        // Include unfolding/normalization in the conservative angular bound.
        if (abs(denominator) <= 8.0 / 32767.0) continue;
        float delta = (asfloat(metadata.w) * address.cellSize - dot(normal, samplePoint - address.cellCenter)) / denominator;
        if (!isfinite(delta) || delta < -tolerance || delta > intervalLength + tolerance || delta >= closest) continue;
        float3 intersection = samplePoint + direction * max(delta, 0.0);
        if (any(abs(intersection - address.cellCenter) > address.cellSize * 0.5 + tolerance)) continue;
        uint4 packed = _BurtGISurfaceCacheTable[slot * 2u + 1u];
        float4 value = BurtGIRadianceCacheHashGridUnpackRadiance(packed);
        value.a *= BurtGIRadianceCacheHashGridAgeConfidenceScale(packed);
        if (value.a <= 0.0001 || !all(isfinite(value))) continue;
        closest = max(delta, 0.0);
        radiance = value; // Valid black is opaque, exactly like a positive hit.
    }
}

// A perspective footprint changes on camera-centered spheres, not only on
// Cartesian cell faces. Visit both sides before accepting any farther plane.
float BurtGISurfaceCacheLevelInterval(float3 samplePoint, float3 direction, float cellSize, float interval)
{
    if (_BurtGIRadianceCacheHashGridAddressParams.z > 0.5 ||
        _BurtGIRadianceCacheHashGridAddressParams.x <= 0.0) return interval;
    float3 relative = samplePoint - _BurtDeferredCameraWorldPosition.xyz;
    float a = dot(direction, direction);
    if (a <= 1.0e-12) return interval;
    float b = dot(relative, direction);
    [unroll] for (uint boundary = 0u; boundary < 2u; ++boundary)
    {
        if (boundary == 0u && cellSize <= 0.001001) continue;
        float radius = cellSize * (boundary == 0u ? 1.0 : 2.0) /
            _BurtGIRadianceCacheHashGridAddressParams.x;
        float c = dot(relative, relative) - radius * radius;
        float discriminant = b * b - a * c;
        if (discriminant <= 0.0) continue;
        float root = sqrt(discriminant);
        float enter = (-b - root) / a;
        float leave = (-b + root) / a;
        if (enter > 1.0e-6) interval = min(interval, enter);
        if (leave > 1.0e-6) interval = min(interval, leave);
    }
    // The address classifier includes float length/log2 rounding. Its actual
    // transition can straddle the analytic sphere, especially at grazing angles.
    // Bracket the first observed change within this sphere/cell interval so a
    // rounded boundary sample cannot skip the next level's occupied cell.
    float halfInterval = interval * 0.5;
    float halfSize = BurtGIHashSurfaceCellSize(_BurtDeferredCameraWorldPosition.xyz,
        samplePoint + direction * halfInterval);
    float endSize = BurtGIHashSurfaceCellSize(_BurtDeferredCameraWorldPosition.xyz,
        samplePoint + direction * interval);
    if (halfSize != cellSize || endSize != cellSize)
    {
        float low = 0.0;
        float high = halfSize != cellSize ? halfInterval : interval;
        [loop] for (uint refine = 0u; refine < 16u; ++refine)
        {
            float middle = (low + high) * 0.5;
            float size = BurtGIHashSurfaceCellSize(_BurtDeferredCameraWorldPosition.xyz,
                samplePoint + direction * middle);
            if (size == cellSize) low = middle; else high = middle;
        }
        interval = min(interval, high);
    }
    return interval;
}

bool BurtGISurfaceCacheTrace(float3 origin, float3 direction, float minDistance, float maxDistance,
    out float3 radiance, out float confidence, out float hitDistance)
{
    radiance = 0.0; confidence = 0.0; hitDistance = maxDistance;
    float t = max(minDistance, 0.0);
    [loop] for (uint step = 0u; step < 50u && t <= maxDistance; ++step)
    {
        float3 samplePoint = origin + direction * t;
        BurtGIHashSurfaceAddress address = BurtGIGetHashSurfaceAddress(_BurtDeferredCameraWorldPosition.xyz,
            samplePoint, direction, t, (uint)_BurtGIRadianceCacheHashGridParams0.y);
        float3 boundary = address.cellCenter + float3(direction.x >= 0.0 ? 0.5 : -0.5,
            direction.y >= 0.0 ? 0.5 : -0.5, direction.z >= 0.0 ? 0.5 : -0.5) * address.cellSize;
        float3 exitDistance = float3(
            abs(direction.x) > 1.0e-6 ? max((boundary.x - samplePoint.x) / direction.x, 0.0) : 1.0e20,
            abs(direction.y) > 1.0e-6 ? max((boundary.y - samplePoint.y) / direction.y, 0.0) : 1.0e20,
            abs(direction.z) > 1.0e-6 ? max((boundary.z - samplePoint.z) / direction.z, 0.0) : 1.0e20);
        float interval = min(maxDistance - t, min(exitDistance.x, min(exitDistance.y, exitDistance.z)));
        // Split at the proximity-key boundary too: a near key must not be
        // tested against an intersection belonging to the far interval.
        float tileSize = address.cellSize * _BurtGIRadianceCacheHashGridParams0.y;
        if (t < tileSize) interval = min(interval, tileSize - t);
        interval = BurtGISurfaceCacheLevelInterval(samplePoint, direction, address.cellSize, interval);
        float closest = 1.0e20; float4 value = 0.0;
        BurtGISurfaceCacheTraceCell(address, samplePoint, direction, interval, closest, value);
        if (value.a > 0.0001)
        {
            radiance = value.rgb;
            hitDistance = t + closest;
            confidence = saturate(value.a * (1.0 - saturate((t + closest) / max(maxDistance, 0.001)) * 0.35));
            return true;
        }
        t += max(interval, 0.0) + max(1.0e-5, address.cellSize * 1.0e-4);
    }
    return false;
}
#endif
