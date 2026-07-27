#ifndef BURT_ATMOSPHERE_LUT_INCLUDED
#define BURT_ATMOSPHERE_LUT_INCLUDED

sampler2D _BurtAtmosphereTransmittanceLut;
sampler2D _BurtAtmosphereMultipleScatteringLut;
sampler2D _BurtAtmosphereHorizontalScatteringLut;
sampler2D _BurtAtmosphereSkyViewLut;
sampler3D _BurtAtmosphereFogLut;
float _BurtAtmosphereUseLuts;
float _BurtAtmosphereCameraAltitude01;
float4 _BurtAtmosphereSkyLuminanceFactor;
float4 _BurtAtmosphereStylizedParams; // x=blend, y=horizon brightness, z=horizon falloff, w=sun glow scale
float4 _BurtAtmosphereStylizedSunRiseParams; // x=min sun elevation, y=max sun elevation
float4 _BurtAtmosphereStylizedBaseSkyColorDay;
float4 _BurtAtmosphereStylizedBaseSkyColorDawnDusk;
float4 _BurtAtmosphereStylizedBaseSkyColorNight;
float4 _BurtAtmosphereStylizedHorizonSkyColorDay;
float4 _BurtAtmosphereStylizedHorizonSkyColorDawnDusk;
float4 _BurtAtmosphereStylizedHorizonSkyColorNight;
float4 _BurtAtmosphereStylizedSunDiskColorScale;
float4 _BurtAtmosphereStylizedSunGlowColor;
sampler2D _BurtAtmosphereMoonSurfaceTexture;
float4 _BurtAtmosphereMoonDirection;
float4 _BurtAtmosphereMoonUp;
float4 _BurtAtmosphereMoonRight;
float4 _BurtAtmosphereMoonSurfaceTint;
float4 _BurtAtmosphereMoonFlareTint;
float4 _BurtAtmosphereMoonGeometry; // x=disk luminance, y=half apex radians, z=flare radians, w=flare falloff
float4 _BurtAtmosphereMoonPhase; // xy=sin/cos phase, zw=sin/cos phase rotation
float4 _BurtAtmosphereMoonVisibility; // x=enabled, y=earthshine, zw=rise min/max

float BurtAtmosphereRaySphereNearest(float3 origin, float3 direction, float radius)
{
    float b = dot(origin, direction);
    float c = dot(origin, origin) - radius * radius;
    float discriminant = b * b - c;
    if (discriminant < 0.0f)
    {
        return -1.0f;
    }

    float root = sqrt(discriminant);
    float nearDistance = -b - root;
    float farDistance = -b + root;
    return nearDistance > 0.0f ? nearDistance : (farDistance > 0.0f ? farDistance : -1.0f);
}

float BurtAtmosphereCameraRadius(float4 planetParams)
{
    float bottomRadius = max(planetParams.x, 1.0f);
    return bottomRadius + max(_BurtAtmosphereCameraAltitude01, 0.0f) * max(planetParams.y, 0.01f);
}

float BurtAtmospherePlanetVisibility(float3 viewDirection, float4 planetParams)
{
    float bottomRadius = max(planetParams.x, 1.0f);
    float3 origin = float3(0.0f, BurtAtmosphereCameraRadius(planetParams), 0.0f);
    return BurtAtmosphereRaySphereNearest(origin, normalize(viewDirection), bottomRadius) >= 0.0f ? 0.0f : 1.0f;
}

float BurtAtmosphereSmoothRange(float edge0, float edge1, float value)
{
    float range = edge1 - edge0;
    float safeRange = abs(range) > 1.0e-5f ? range : (range < 0.0f ? -1.0e-5f : 1.0e-5f);
    float t = saturate((value - edge0) / safeRange);
    return t * t * (3.0f - 2.0f * t);
}

float3 BurtAtmosphereEvaluateMoon(
    float3 viewDirection,
    float3 sunDirection,
    float3 moonDirection,
    float3 moonUp,
    float3 moonRight,
    float3 atmosphereTransmittance,
    float4 planetParams)
{
    if (_BurtAtmosphereMoonVisibility.x < 0.5f || _BurtAtmosphereMoonGeometry.x <= 0.0f)
    {
        return 0.0f;
    }

    const float inversePi = 0.31830988618f;
    float halfApex = max(_BurtAtmosphereMoonGeometry.y, 1.0e-5f);
    float flareSize = max(_BurtAtmosphereMoonGeometry.z, 0.0f);
    float viewDotMoon = dot(normalize(viewDirection), normalize(moonDirection));
    float innerCos = cos(halfApex);
    float outerCos = cos(halfApex + flareSize);
    if (viewDotMoon <= outerCos)
    {
        return 0.0f;
    }

    float riseMin = _BurtAtmosphereMoonVisibility.z;
    float riseMax = max(_BurtAtmosphereMoonVisibility.w, riseMin + 0.001f);
    float riseVisibility = smoothstep(riseMin, riseMax, moonDirection.y);
    // XRender dispatches moon/stars only in its night permutation. BRP derives
    // that permutation continuously from solar elevation to avoid a frame pop.
    float nightVisibility = 1.0f - smoothstep(-0.05f, 0.05f, sunDirection.y);
    float visibility = riseVisibility * nightVisibility
        * BurtAtmospherePlanetVisibility(viewDirection, planetParams);
    if (visibility <= 0.0f)
    {
        return 0.0f;
    }

    float3 moonLuminance = max(atmosphereTransmittance, 0.0f) * max(_BurtAtmosphereMoonGeometry.x, 0.0f);
    if (viewDotMoon > innerCos)
    {
        float2 projected = float2(
            dot(-viewDirection, normalize(moonUp)),
            dot(-viewDirection, normalize(moonRight)));
        float2 angles = float2(asin(clamp(-projected.x, -1.0f, 1.0f)), asin(clamp(projected.y, -1.0f, 1.0f)));
        float2 moonUV = angles / halfApex;
        float3 surface = tex2D(_BurtAtmosphereMoonSurfaceTexture, moonUV * 0.5f + 0.5f).rgb
            * max(_BurtAtmosphereMoonSurfaceTint.rgb, 0.0f);

        float2 rotationX = float2(_BurtAtmosphereMoonPhase.z, -_BurtAtmosphereMoonPhase.w);
        float2 rotationY = float2(_BurtAtmosphereMoonPhase.w, _BurtAtmosphereMoonPhase.z);
        float2 phasedUV = float2(dot(rotationX, moonUV), dot(rotationY, moonUV));
        float sphereDepth = sqrt(saturate(1.0f - dot(phasedUV, phasedUV)));
        float3 normal = float3(sphereDepth, phasedUV.y, phasedUV.x);
        float3 phaseLight = float3(_BurtAtmosphereMoonPhase.y, 0.0f, _BurtAtmosphereMoonPhase.x);
        float phaseFactor = saturate(-dot(normal, phaseLight)) * inversePi
            + max(_BurtAtmosphereMoonVisibility.y, 0.0f);
        moonLuminance *= surface * phaseFactor;
    }
    else
    {
        float angularDistance = acos(clamp(viewDotMoon, -1.0f, 1.0f));
        float flareWeight = flareSize > 0.0f
            ? saturate(1.0f - max(angularDistance - halfApex, 0.0f) / flareSize)
            : 0.0f;
        moonLuminance *= max(_BurtAtmosphereMoonFlareTint.rgb, 0.0f)
            * pow(abs(flareWeight), max(_BurtAtmosphereMoonGeometry.w, 1.0f));
    }

    return min(max(moonLuminance * visibility, 0.0f), 65504.0f);
}

float3 BurtAtmosphereEvaluateStylizedSky(
    float3 viewDirection,
    float3 lightDirection,
    float4 planetParams,
    float3 groundColor,
    float3 groundParams,
    float mainLightOcclusion)
{
    float sunElevation = clamp(lightDirection.y, -1.0f, 1.0f);
    float dayWeight = smoothstep(-0.05f, 0.15f, sunElevation);
    float dawnDuskWeight = 1.0f - smoothstep(0.0f, 0.35f, abs(sunElevation));

    float3 baseSkyColor = lerp(
        _BurtAtmosphereStylizedBaseSkyColorNight.rgb,
        _BurtAtmosphereStylizedBaseSkyColorDay.rgb,
        dayWeight);
    baseSkyColor = lerp(baseSkyColor, _BurtAtmosphereStylizedBaseSkyColorDawnDusk.rgb, dawnDuskWeight);

    float3 horizonSkyColor = lerp(
        _BurtAtmosphereStylizedHorizonSkyColorNight.rgb,
        _BurtAtmosphereStylizedHorizonSkyColorDay.rgb,
        dayWeight);
    horizonSkyColor = lerp(horizonSkyColor, _BurtAtmosphereStylizedHorizonSkyColorDawnDusk.rgb, dawnDuskWeight);

    float horizonWeight = pow(
        saturate(1.0f - abs(viewDirection.y)),
        max(_BurtAtmosphereStylizedParams.z, 0.1f));
    float3 stylizedSky = lerp(
        max(baseSkyColor, 0.0f),
        max(horizonSkyColor, 0.0f) * max(_BurtAtmosphereStylizedParams.y, 0.0f),
        horizonWeight);

    float riseMin = _BurtAtmosphereStylizedSunRiseParams.x;
    float riseMax = max(_BurtAtmosphereStylizedSunRiseParams.y, riseMin + 0.001f);
    float sunRiseWeight = smoothstep(riseMin, riseMax, sunElevation);
    float sunGlowPower = lerp(20.0f, 5.0f, dawnDuskWeight);
    float sunGlow = pow(saturate(dot(viewDirection, lightDirection)), sunGlowPower)
        * max(_BurtAtmosphereStylizedParams.w, 0.0f)
        * sunRiseWeight
        * BurtAtmospherePlanetVisibility(viewDirection, planetParams);
    // The authored base/horizon colors are ambient sky art direction. Only the
    // directional sun-glow lobe participates in XRender main-light occlusion.
    stylizedSky += max(_BurtAtmosphereStylizedSunGlowColor.rgb, 0.0f) * sunGlow * saturate(mainLightOcclusion);

    float groundBlend = BurtAtmosphereSmoothRange(groundParams.y, groundParams.z, viewDirection.y);
    return lerp(stylizedSky, max(groundColor, 0.0f) * max(groundParams.x, 0.0f), groundBlend);
}

float2 BurtAtmosphereMapTransmittanceParamsToUv(float viewRadius, float viewUp, float4 planetParams)
{
    float bottomRadius = max(planetParams.x, 1.0f);
    float topRadius = bottomRadius + max(planetParams.y, 0.01f);
    viewRadius = clamp(viewRadius, bottomRadius, topRadius);
    float h = sqrt(max(topRadius * topRadius - bottomRadius * bottomRadius, 0.0f));
    float rho = sqrt(max(viewRadius * viewRadius - bottomRadius * bottomRadius, 0.0f));
    float discriminant = viewRadius * viewRadius * (viewUp * viewUp - 1.0f) + topRadius * topRadius;
    float distance = max(0.0f, -viewRadius * viewUp + sqrt(max(discriminant, 0.0f)));
    float distanceMin = topRadius - viewRadius;
    float distanceMax = rho + h;
    return float2(saturate((distance - distanceMin) / max(distanceMax - distanceMin, 0.0001f)), saturate(rho / max(h, 0.0001f)));
}

float2 BurtAtmosphereMapSkyViewParamsToUv(float3 viewDirection, float4 planetParams)
{
    const float pi = 3.14159265359f;
    float bottomRadius = max(planetParams.x, 1.0f);
    float viewRadius = BurtAtmosphereCameraRadius(planetParams);
    float3 origin = float3(0.0f, viewRadius, 0.0f);
    float3 direction = normalize(viewDirection);
    float viewUp = clamp(direction.y, -1.0f, 1.0f);
    float horizonDistance = sqrt(max(viewRadius * viewRadius - bottomRadius * bottomRadius, 0.0f));
    float beta = acos(saturate(horizonDistance / max(viewRadius, 0.001f)));
    float zenithHorizonAngle = pi - beta;
    float viewZenithAngle = acos(viewUp);
    bool intersectsGround = BurtAtmosphereRaySphereNearest(origin, direction, bottomRadius) >= 0.0f;
    float v;
    if (intersectsGround)
    {
        float coord = saturate((viewZenithAngle - zenithHorizonAngle) / max(beta, 0.0001f));
        v = sqrt(coord) * 0.5f + 0.5f;
    }
    else
    {
        float coord = saturate(viewZenithAngle / max(zenithHorizonAngle, 0.0001f));
        v = (1.0f - sqrt(1.0f - coord)) * 0.5f;
    }

    float u = (atan2(-direction.x, -direction.z) + pi) / (2.0f * pi);
    // XRender stores endpoint directions by applying FromSubUvsToUnit while
    // generating the LUT, then samples this parameter coordinate directly with
    // a linear-clamp sampler. Do not apply a second forward sub-UV transform.
    return float2(u, v);
}

float3 BurtAtmosphereSampleTransmittance(float viewUp, float4 planetParams)
{
    float bottomRadius = max(planetParams.x, 1.0f);
    float topRadius = bottomRadius + max(planetParams.y, 0.01f);
    float viewRadius = BurtAtmosphereCameraRadius(planetParams);
    float3 direction = float3(sqrt(saturate(1.0f - viewUp * viewUp)), clamp(viewUp, -1.0f, 1.0f), 0.0f);
    float3 origin = float3(0.0f, viewRadius, 0.0f);
    if (viewRadius > topRadius)
    {
        float entryDistance = BurtAtmosphereRaySphereNearest(origin, direction, topRadius);
        if (entryDistance < 0.0f)
        {
            // A ray that remains in space crosses no participating medium.
            return 1.0f;
        }

        float3 cameraUp = normalize(origin);
        origin = origin + direction * entryDistance - cameraUp * 0.005f;
        viewRadius = length(origin);
        viewUp = dot(normalize(origin), direction);
    }

    return tex2D(_BurtAtmosphereTransmittanceLut, BurtAtmosphereMapTransmittanceParamsToUv(viewRadius, viewUp, planetParams)).rgb;
}

float3 BurtAtmosphereSampleSkyView(float3 viewDirection, float4 planetParams)
{
    return tex2D(_BurtAtmosphereSkyViewLut, BurtAtmosphereMapSkyViewParamsToUv(viewDirection, planetParams)).rgb;
}

float3 BurtAtmosphereSampleMultipleScattering(float sunUp)
{
    // Multiple-scattering is defined only inside the atmosphere. Space views
    // use the top layer after their ray has been moved to the atmosphere entry.
    return tex2D(_BurtAtmosphereMultipleScatteringLut, float2(saturate(sunUp * 0.5f + 0.5f), saturate(_BurtAtmosphereCameraAltitude01))).rgb;
}

float3 BurtAtmosphereSampleHorizontalScattering(float component)
{
    float3 scattering = tex2D(_BurtAtmosphereHorizontalScatteringLut, float2((component + 0.5f) / 3.0f, 0.5f)).rgb;
    // XRender applies SkyLuminanceFactor to the phase-independent Rayleigh and
    // Mie terms, but leaves the separately controlled multiple-scattering term
    // untouched. Apply it at sampling time so grading never rebuilds the LUT.
    return component < 1.5f
        ? scattering * max(_BurtAtmosphereSkyLuminanceFactor.rgb, 0.0f)
        : scattering;
}

float BurtAtmosphereHorizontalRayleighPhase(float cosTheta)
{
    const float pi = 3.14159265359f;
    return 3.0f * (1.0f + cosTheta * cosTheta) / (16.0f * pi);
}

float BurtAtmosphereHorizontalSchlickPhase(float anisotropy, float cosTheta)
{
    const float pi = 3.14159265359f;
    float g = clamp(anisotropy, -0.9f, 0.9f);
    float k = 1.55f * g - 0.55f * g * g * g;
    float factor = max(abs(1.0f + k * cosTheta), 1.0e-4f);
    return (1.0f - k * k) / (4.0f * pi * factor * factor);
}

void BurtAtmosphereEvaluateHorizontalFogLightingTerms(
    float lightViewCosine,
    float mieAnisotropy,
    float3 outerSpaceLightColor,
    float3 rayleighTintScale,
    float3 mieTintScale,
    float3 multipleScatteringTintScale,
    out float3 singleScatteringLighting,
    out float3 multipleScatteringLighting)
{
    float3 rayleigh = BurtAtmosphereSampleHorizontalScattering(0.0f)
        * max(rayleighTintScale, 0.0f)
        * BurtAtmosphereHorizontalRayleighPhase(lightViewCosine);
    // XRender's fog convention negates L.V for its Schlick phase function.
    float3 mie = BurtAtmosphereSampleHorizontalScattering(1.0f)
        * max(mieTintScale, 0.0f)
        * BurtAtmosphereHorizontalSchlickPhase(mieAnisotropy, -lightViewCosine);
    float3 multipleScattering = BurtAtmosphereSampleHorizontalScattering(2.0f)
        * max(multipleScatteringTintScale, 0.0f);
    float3 lightColor = max(outerSpaceLightColor, 0.0f);
    singleScatteringLighting = lightColor * (rayleigh + mie);
    multipleScatteringLighting = lightColor * multipleScattering;
}

float3 BurtAtmosphereCombineHorizontalFogLighting(
    float3 singleScatteringLighting,
    float3 multipleScatteringLighting,
    float singleScatteringVisibility,
    float mainLightOcclusion)
{
    // XRender shadows Rayleigh and Mie (single scattering) in volumetric fog,
    // while multiple scattering remains visible. The authored main-light
    // occlusion factor dims the complete main-light contribution.
    return (singleScatteringLighting * saturate(singleScatteringVisibility) + multipleScatteringLighting)
        * saturate(mainLightOcclusion);
}

float3 BurtAtmosphereEvaluateHorizontalFogLighting(
    float lightViewCosine,
    float mieAnisotropy,
    float3 outerSpaceLightColor,
    float3 rayleighTintScale,
    float3 mieTintScale,
    float3 multipleScatteringTintScale,
    float singleScatteringVisibility,
    float mainLightOcclusion)
{
    float3 singleScatteringLighting;
    float3 multipleScatteringLighting;
    BurtAtmosphereEvaluateHorizontalFogLightingTerms(
        lightViewCosine,
        mieAnisotropy,
        outerSpaceLightColor,
        rayleighTintScale,
        mieTintScale,
        multipleScatteringTintScale,
        singleScatteringLighting,
        multipleScatteringLighting);
    return BurtAtmosphereCombineHorizontalFogLighting(
        singleScatteringLighting,
        multipleScatteringLighting,
        singleScatteringVisibility,
        mainLightOcclusion);
}

float4 BurtAtmosphereSampleFog(float2 screenUv, float distanceRatio)
{
    return tex3D(_BurtAtmosphereFogLut, float3(saturate(screenUv), saturate(sqrt(max(distanceRatio, 0.0f)))));
}

#endif
