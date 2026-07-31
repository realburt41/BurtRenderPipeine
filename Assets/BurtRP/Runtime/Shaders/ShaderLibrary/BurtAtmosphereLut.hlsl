#ifndef BURT_ATMOSPHERE_LUT_INCLUDED
#define BURT_ATMOSPHERE_LUT_INCLUDED

#pragma warning(disable : 3571)

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/BurtAtmosphereFogLutConfig.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/BurtAtmosphereHorizontalScattering.hlsl"

sampler2D _BurtAtmosphereTransmittanceLut;
sampler2D _BurtAtmosphereMultipleScatteringLut;
sampler2D _BurtAtmosphereSkyViewLut;
sampler3D _BurtAtmosphereFogLut;
float _BurtAtmosphereUseLuts;
float _BurtAtmosphereCameraAltitude01;
float _BurtAtmospherePhysicalSkyTimeOfDayCurve;
float4 _BurtAtmospherePhysicalSkyTime; // xy=XRender legacy time block index/remainder (elapsed seconds / 20, block size 128)
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
Texture2D _BurtAtmosphereMoonSurfaceTexture;
Texture2D _BurtAtmosphereMoonPhaseNormalTexture;
float4 _BurtAtmosphereMoonDirection;
float4 _BurtAtmosphereMoonUp;
float4 _BurtAtmosphereMoonRight;
float4 _BurtAtmosphereMoonSurfaceTint;
float4 _BurtAtmosphereMoonAdditionalTint;
float4 _BurtAtmosphereMoonFlareTint;
float4 _BurtAtmosphereMoonGeometry; // x=disk luminance, y=half apex radians, z=XRender radial flare scale, w=flare falloff
float4 _BurtAtmosphereMoonPhase; // xy=sin/cos phase, zw=sin/cos phase rotation
float4 _BurtAtmosphereMoonPhysicalParams; // x=phase sharpness, y=bloom intensity, z=bloom size, w=bloom falloff
float4 _BurtAtmosphereMoonBloomParams; // x=bloom edge alpha, y=XRender phase-normal binding active
float4 _BurtAtmosphereMoonVisibility; // x=enabled, y=earthshine, zw=legacy uploaded rise interval (project PhysicalSky leaves it unused)
Texture2D _BurtAtmosphereStarsTexture;
Texture2D _BurtAtmosphereStarsTintColorTexture;
Texture2D _BurtAtmosphereAreaStarsTexture;
Texture2D _BurtAtmosphereGalaxyCloudTexture;
Texture2D _BurtAtmosphereCustomStarTexture;
float4 _BurtAtmosphereStarsControl; // x=enabled, y=PC intensity, z=PC horizon falloff (mobile fixes both)
float4 _BurtAtmosphereStarsTintColor; // rgb=tint, a=desaturation
float4 _BurtAtmosphereStarsTintTransform; // xy=tiling, zw=offset
float4 _BurtAtmosphereStarsLayerHeightsRotation; // xyz=layer heights, w=XRender raw-radian rotation
float4 _BurtAtmosphereStarsLayerAnimation; // x=layer speed/10, y=twinkle strength, z=twinkle speed/50
float4 _BurtAtmosphereStarsLayerFalloffs;
float4 _BurtAtmosphereAreaStarsParams; // x=intensity, yz=density min/max, w=speed
float4 _BurtAtmosphereAreaStarsMaskTransform; // xy=tiling, zw=offset
float4 _BurtAtmosphereAreaStarsFalloffs; // x=stars, y=mask
float4 _BurtAtmosphereGalaxyTransform; // xy=tiling, zw=offset
float4 _BurtAtmosphereGalaxyParams; // x=rotation radians, y=cloud intensity, z=cloud falloff, w=star intensity
float4 _BurtAtmosphereGalaxyStarParams; // x=falloff, y=height, z=speed/10
float4 _BurtAtmosphereCustomStarTextureTransform; // xy=scale, zw=offset
float4 _BurtAtmosphereCustomStarControl; // x=texture enabled, y=rotation speed, z=minimum scale, w=scatter speed
float4 _BurtAtmosphereCustomStarIntensityMax;
float4 _BurtAtmosphereCustomStarIntensityMin;
float _BurtAtmosphereCustomStarScatterInterval;
Texture2D _BurtAtmospherePanoramicCloudDefaultTexture;
Texture2D _BurtAtmospherePanoramicCloudPreviousTexture;
Texture2D _BurtAtmospherePanoramicCloudCurrentTexture;
float4 _BurtAtmospherePanoramicCloudControl; // x=enabled, y=default texture, z=transition active, w=transition
float4 _BurtAtmospherePanoramicCloudUvParams; // x=day offset, y=night offset, z=rotation speed, w=ignore TOD colors
float4 _BurtAtmospherePanoramicCloudLuminanceParams; // x=sunny, y=night, z=authored alpha 0..100
float4 _BurtAtmospherePanoramicCloudBaseColor;
float4 _BurtAtmospherePanoramicCloudDetailSpecular;
float4 _BurtAtmospherePhysicalSkyDesaturationParams; // x=shared blend, y=sky intensity, z=cloud intensity, w=force enabled
float4 _BurtAtmospherePhysicalSkyDesaturationColor;
Texture2D _BurtAtmosphereWeatherCoverageTexture;
float4 _BurtAtmosphereWeatherWeights; // x=rain intensity, y=rain wet coverage, z=snow intensity, w=snow coverage
float4 _BurtAtmosphereWeatherShadowParams; // x=march distance, y=enabled
float4 _BurtAtmosphereWeatherShadowBright;
float4 _BurtAtmosphereWeatherShadowDark;

// Unity recognizes the filter/address tokens in these inline sampler names.
// XRender's PhysicalSky uses these fixed states instead of texture-importer
// sampling state for all celestial, weather and panoramic-cloud resources.
SamplerState BurtAtmosphere_linear_clamp_sampler;
SamplerState BurtAtmosphere_linear_repeat_sampler;

float BurtAtmosphereHenyeyGreensteinPhase(float cosTheta, float anisotropy)
{
    float g2 = anisotropy * anisotropy;
    float denominatorBase = 1.0f + g2 - 2.0f * anisotropy * cosTheta;
    return (1.0f - g2)
        / (4.0f * 3.14159265359f * denominatorBase * sqrt(denominatorBase));
}

float BurtAtmosphereSchlickPhase(float anisotropy, float cosTheta)
{
    float k = 1.55f * anisotropy
        - 0.55f * anisotropy * anisotropy * anisotropy;
    float factor = 1.0f + k * cosTheta;
    return (1.0f - k * k)
        / (4.0f * 3.14159265359f * factor * factor);
}

float BurtAtmosphereRaySphereNearest(float3 origin, float3 direction, float radius)
{
    float a = dot(direction, direction);
    float b = 2.0f * dot(origin, direction);
    float c = dot(origin, origin) - radius * radius;
    float discriminant = b * b - 4.0f * a * c;
    if (discriminant < 0.0f)
    {
        return -1.0f;
    }

    float root = sqrt(discriminant);
    float2 intersections = (-b + float2(-1.0f, 1.0f) * root) / (2.0f * a);
    float nearDistance = intersections.x;
    float farDistance = intersections.y;
    if (nearDistance < 0.0f && farDistance < 0.0f)
    {
        return -1.0f;
    }

    if (nearDistance < 0.0f)
    {
        return max(0.0f, farDistance);
    }

    if (farDistance < 0.0f)
    {
        return max(0.0f, nearDistance);
    }

    return max(0.0f, min(nearDistance, farDistance));
}

float BurtAtmosphereCameraRadius(float4 planetParams)
{
    float bottomRadius = max(planetParams.x, 0.1f);
    return bottomRadius + max(_BurtAtmosphereCameraAltitude01, 0.0f) * max(planetParams.y, 0.1f);
}

float BurtAtmospherePlanetVisibility(float3 viewDirection, float4 planetParams)
{
    float bottomRadius = max(planetParams.x, 0.1f);
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

float2 BurtAtmosphereRotateUv(float2 uv, float2 center, float angle)
{
    float sine;
    float cosine;
    sincos(angle, sine, cosine);
    float2 centered = uv - center;
    return center + float2(
        centered.x * cosine - centered.y * sine,
        centered.x * sine + centered.y * cosine);
}

float BurtAtmosphereFastAcosPositive(float value)
{
    float x = abs(value);
    float result = (0.0468878f * x - 0.203471f) * x + 1.570796f;
    return result * sqrt(saturate(1.0f - x));
}

float BurtAtmosphereFastAcos(float value)
{
    float clampedValue = clamp(value, -1.0f, 1.0f);
    float result = BurtAtmosphereFastAcosPositive(clampedValue);
    return clampedValue >= 0.0f ? result : 3.14159265359f - result;
}

float BurtAtmosphereFastAsin(float value)
{
    return 1.57079632679f - BurtAtmosphereFastAcos(value);
}

float BurtAtmosphereStableTimePhase(float frequency)
{
    float blockPhase = frac(
        _BurtAtmospherePhysicalSkyTime.x
        * frac(128.0f * frequency));
    float remainderPhase = frac(
        _BurtAtmospherePhysicalSkyTime.y * frequency);
    return frac(blockPhase + remainderPhase);
}

float BurtAtmosphereElapsedTime()
{
    return (
        128.0f * _BurtAtmospherePhysicalSkyTime.x
        + _BurtAtmospherePhysicalSkyTime.y) * 20.0f;
}

float BurtAtmosphereStarLuminance(float intensity)
{
    return intensity / 0.00023924444374124696f;
}

float BurtAtmosphereCheapContrast(float grayscale, float contrast)
{
    return saturate(lerp(-contrast, contrast + 1.0f, grayscale));
}

float3 BurtAtmosphereAdjustSaturation(float3 color, float saturation)
{
    float grey = dot(color, float3(0.299f, 0.587f, 0.114f));
    return lerp(grey.xxx, color, saturation);
}

float3 BurtAtmosphereApplyPhysicalSkyDesaturation(float3 skyLuminance)
{
    float grey = dot(skyLuminance, float3(0.299f, 0.587f, 0.114f));
    float3 desaturated = max(_BurtAtmospherePhysicalSkyDesaturationColor.rgb, 0.0f)
        * grey.xxx
        * max(_BurtAtmospherePhysicalSkyDesaturationParams.y, 0.0f);
    return lerp(
        skyLuminance,
        desaturated,
        saturate(_BurtAtmospherePhysicalSkyDesaturationParams.x));
}

float3 BurtAtmosphereApplyPhysicalSkyCloudDesaturation(float3 cloudLuminance)
{
    float grey = dot(cloudLuminance, float3(0.299f, 0.587f, 0.114f));
    float3 desaturated = grey.xxx
        * _BurtAtmospherePhysicalSkyDesaturationParams.z;
    return lerp(
        cloudLuminance,
        desaturated,
        _BurtAtmospherePhysicalSkyDesaturationParams.x);
}

float BurtAtmosphereWeatherWetWeight()
{
    return max(_BurtAtmosphereWeatherWeights.x, _BurtAtmosphereWeatherWeights.y);
}

float BurtAtmosphereWeatherSnowWeight()
{
    return max(_BurtAtmosphereWeatherWeights.z, _BurtAtmosphereWeatherWeights.w);
}

float BurtAtmosphereWeatherCoverage()
{
    if (_BurtAtmosphereWeatherShadowParams.y < 0.5f)
    {
        return 0.0f;
    }

    return max(
        BurtAtmosphereWeatherWetWeight(),
        BurtAtmosphereWeatherSnowWeight());
}

float2 BurtAtmosphereWeatherCloudUv(float2 meshUv0)
{
    return meshUv0 * 4.0f
        + float2(BurtAtmosphereElapsedTime() * 0.015f, 0.0f);
}

float BurtAtmosphereWeatherCelestialMask(float2 meshUv0, float maskScale)
{
    float coverage = BurtAtmosphereWeatherCoverage();
    float coverageSample = _BurtAtmosphereWeatherCoverageTexture.Sample(
        BurtAtmosphere_linear_repeat_sampler,
        BurtAtmosphereWeatherCloudUv(meshUv0)).r;
    float squared = coverageSample * coverageSample;
    float fourthPower = squared * squared;
    return lerp(1.0f, saturate(fourthPower * maskScale), coverage);
}

float BurtAtmosphereWeatherSunVisibility()
{
    float coverage = BurtAtmosphereWeatherCoverage();
    float effectAmount = saturate(coverage * (2.3f - coverage));
    return 1.0f - effectAmount;
}

float BurtAtmospherePhysicalSkyTimeOfDayCurve()
{
    return saturate(_BurtAtmospherePhysicalSkyTimeOfDayCurve);
}

float BurtAtmospherePhysicalSkyNightPermutation()
{
    // XRender selects _PHYSICAL_SKY_IS_NIGHT only when _TodCurve > 0.5.
    // Keep this independent from main-light elevation: its EnvSystem moves
    // the active directional light along a moon trajectory during the night.
    return BurtAtmospherePhysicalSkyTimeOfDayCurve() > 0.5f ? 1.0f : 0.0f;
}

float BurtAtmospherePhysicalSkySunDiskVisibility(
    float viewLightCosine,
    float sunDiskCosHalfApex)
{
    float diskMask = viewLightCosine > sunDiskCosHalfApex ? 1.0f : 0.0f;
    float dayVisibility = BurtAtmospherePhysicalSkyTimeOfDayCurve() > 0.0f ? 0.0f : 1.0f;
    return diskMask
        * dayVisibility
        * BurtAtmosphereWeatherSunVisibility();
}

float3 BurtAtmosphereEvaluatePhysicalSkySunDisk(
    float viewLightCosine,
    float4 sunDiskLuminanceAndCosHalfApex,
    float inversePreExposure)
{
    float visibility = BurtAtmospherePhysicalSkySunDiskVisibility(
        viewLightCosine,
        sunDiskLuminanceAndCosHalfApex.w);
    // PhysicalSky clamps after pre-exposure. Convert that ceiling back to the
    // scene-linear domain because Burt applies pre-exposure to the combined sky.
    float sceneLinearCeiling = 64000.0f * max(inversePreExposure, 0.0f);
    return min(
        sunDiskLuminanceAndCosHalfApex.rgb,
        sceneLinearCeiling) * visibility;
}

float3 BurtAtmosphereEvaluateWeatherSkyClouds(
    float3 viewDirection,
    float3 sunDirection,
    float2 meshUv0)
{
#if defined(SHADER_API_MOBILE)
    // Project PhysicalSky mobile returns the integrated atmosphere sky
    // directly and compiles out the PC weather-coverage overlay.
    return 0.0f;
#else
    float coverage = BurtAtmosphereWeatherCoverage();
    float2 cloudUv = BurtAtmosphereWeatherCloudUv(meshUv0);
    float3 cloudMaskTexture = _BurtAtmosphereWeatherCoverageTexture.Sample(
        BurtAtmosphere_linear_repeat_sampler,
        cloudUv).rgb;
    float2 cloudShadowOffset = sunDirection.xz
        * _BurtAtmosphereWeatherShadowParams.x;
    float3 cloudShadowTexture = _BurtAtmosphereWeatherCoverageTexture.Sample(
        BurtAtmosphere_linear_repeat_sampler,
        cloudUv + cloudShadowOffset).rgb;
    float3 cloudShadowColor = lerp(
        _BurtAtmosphereWeatherShadowDark.rgb,
        _BurtAtmosphereWeatherShadowBright.rgb,
        1.0f - cloudShadowTexture.r);

    // XRender's MF_SunCenteredGradient uses a constant effective shape of
    // three for the day/night values supplied by PhysicalSky.
    float sunHeight = (viewDirection.y - sunDirection.y) * 3.0f + sunDirection.y;
    float3 gradientDirection = float3(viewDirection.x, sunHeight, viewDirection.z);
    gradientDirection *= rsqrt(max(dot(gradientDirection, gradientDirection), 1.0e-6f));
    float sunCenteredMask = saturate(
        (dot(gradientDirection, normalize(sunDirection)) + 1.0f) * 0.5f);
    float3 weatherCloud = saturate(cloudMaskTexture * cloudShadowColor)
        * sunCenteredMask
        * coverage;
    float snowBrightness = lerp(
        1.0f,
        5.0f,
        ceil(BurtAtmosphereWeatherSnowWeight()));
    float isNight = BurtAtmospherePhysicalSkyNightPermutation();
    float cloudLuminance = lerp(
        _BurtAtmospherePanoramicCloudLuminanceParams.x * snowBrightness,
        _BurtAtmospherePanoramicCloudLuminanceParams.y,
        isNight);
    return weatherCloud * cloudLuminance;
#endif
}

float3 BurtAtmosphereEvaluateStars(
    float3 viewDirection,
    float3 positionWS,
    float3 moonUp,
    float3 moonRight,
    float2 meshUv0)
{
    if (_BurtAtmosphereStarsControl.x < 0.5f)
    {
        return 0.0f;
    }

    const float legacyTimeScale = 20.0f;
    const float layer2SpeedScale = 1.5f;
    const float layer3SpeedScale = 2.0f;
    float2 skyUv = meshUv0;
    float layerSpeed = _BurtAtmosphereStarsLayerAnimation.x;

#if defined(SHADER_API_MOBILE)
    // Project PhysicalSky mobile permutation: two fixed star layers, a fixed
    // galaxy-cloud transform and no authored rotation/area/twinkle/tint/full
    // galaxy-star controls. Only the shared layer speed remains dynamic.
    float layer1Phase = BurtAtmosphereStableTimePhase(
        legacyTimeScale * layerSpeed * 15.0f);
    float layer2Phase = BurtAtmosphereStableTimePhase(
        legacyTimeScale * (layerSpeed / layer2SpeedScale) * 10.0f);
    float3 layer1 = _BurtAtmosphereStarsTexture.Sample(
        BurtAtmosphere_linear_repeat_sampler,
        skyUv * 15.0f + float2(3.75f, layer1Phase)).rgb;
    float3 layer2 = _BurtAtmosphereStarsTexture.Sample(
        BurtAtmosphere_linear_repeat_sampler,
        skyUv * 10.0f + float2(0.0f, layer2Phase)).rgb;

    // XRender fixes intensity to 0.025 and approximates pow(x, 2.4) as
    // x^2 * (0.5342793 + 0.4657207*x), with luminance/1.5 baked below.
    float3 layer1Squared = layer1 * layer1;
    float3 layer2Squared = layer2 * layer2;
    float3 stars =
        layer1Squared * (37.2199030f + 32.4438534f * layer1)
        + layer2Squared * (37.2199030f + 32.4438534f * layer2);
    stars *= saturate(positionWS.y / 19930.0f);

    float2 galaxyUv = skyUv * 2.0f - 1.0f;
    const float2 galaxyCenter = float2(0.14f, 0.128f);
    const float galaxySin115 = 0.90630778703665f;
    const float galaxyCos115 = -0.42261826174070f;
    float2 galaxyCentered = galaxyUv - galaxyCenter;
    galaxyUv = galaxyCenter + float2(
        galaxyCentered.x * galaxyCos115 - galaxyCentered.y * galaxySin115,
        galaxyCentered.x * galaxySin115 + galaxyCentered.y * galaxyCos115);
    galaxyUv = galaxyUv * float2(2.5f, 3.0f) + float2(0.15f, 0.116f);
    float3 galaxyCloud = _BurtAtmosphereGalaxyCloudTexture.Sample(
        BurtAtmosphere_linear_clamp_sampler,
        galaxyUv).rgb;
    float3 galaxyCloudPow = galaxyCloud
        * (0.3892773f + 0.6107227f * galaxyCloud);
    stars += galaxyCloudPow * 5.0157905f;
#else
    float2 rotatedUv = BurtAtmosphereRotateUv(
        skyUv * 2.0f - 1.0f,
        0.0f,
        _BurtAtmosphereStarsLayerHeightsRotation.w);
    float twinkleStrength = _BurtAtmosphereStarsLayerAnimation.y;
    float twinkleSpeed = _BurtAtmosphereStarsLayerAnimation.z;
    float3 layerHeights = _BurtAtmosphereStarsLayerHeightsRotation.xyz;
    float layer1Phase = BurtAtmosphereStableTimePhase(legacyTimeScale * layerSpeed * layerHeights.x);
    float twinklePhase = BurtAtmosphereStableTimePhase(legacyTimeScale * twinkleSpeed * 10.0f);
    float2 layer1Uv = rotatedUv * layerHeights.x + float2(0.0f, layer1Phase);
    float twinkleMask = _BurtAtmosphereStarsTexture.Sample(
        BurtAtmosphere_linear_repeat_sampler,
        layer1Uv + float2(0.0f, twinklePhase)).a;
    twinkleMask = lerp(
        1.0f,
        pow(twinkleMask, 6.0f) * BurtAtmosphereStarLuminance(twinkleStrength),
        saturate(twinkleStrength));

    float layer2Phase = BurtAtmosphereStableTimePhase(
        legacyTimeScale * (layerSpeed / layer2SpeedScale) * layerHeights.y);
    float layer3Phase = BurtAtmosphereStableTimePhase(
        legacyTimeScale * (layerSpeed / layer3SpeedScale) * layerHeights.z);
    float2 layer2Uv = rotatedUv * layerHeights.y + float2(0.0f, layer2Phase);
    float2 layer3Uv = rotatedUv * layerHeights.z + float2(0.0f, layer3Phase);
    float3 layer1 = _BurtAtmosphereStarsTexture.Sample(
        BurtAtmosphere_linear_repeat_sampler,
        layer1Uv).rgb;
    float3 layer2 = _BurtAtmosphereStarsTexture.Sample(
        BurtAtmosphere_linear_repeat_sampler,
        layer2Uv).rgb;
    float3 layer3 = _BurtAtmosphereStarsTexture.Sample(
        BurtAtmosphere_linear_repeat_sampler,
        layer3Uv).rgb;
    float starIntensity = BurtAtmosphereStarLuminance(_BurtAtmosphereStarsControl.y);
    float3 stars = pow(layer1, _BurtAtmosphereStarsLayerFalloffs.x) * starIntensity
        + pow(layer2, _BurtAtmosphereStarsLayerFalloffs.y) * (starIntensity / layer2SpeedScale)
        + pow(layer3, _BurtAtmosphereStarsLayerFalloffs.z) * (starIntensity / layer3SpeedScale);

    float areaSpeed = _BurtAtmosphereAreaStarsParams.w;
    float2 areaMaskUv = (rotatedUv + _BurtAtmosphereAreaStarsMaskTransform.zw)
        * _BurtAtmosphereAreaStarsMaskTransform.xy;
    float areaMaskPhase = BurtAtmosphereStableTimePhase(legacyTimeScale * areaSpeed * 0.025f);
    float areaMask = _BurtAtmosphereAreaStarsTexture.Sample(
        BurtAtmosphere_linear_repeat_sampler,
        areaMaskUv + float2(0.0f, -areaMaskPhase)).a;
    areaMask = BurtAtmosphereCheapContrast(
        areaMask / _BurtAtmosphereAreaStarsFalloffs.y,
        0.15f);
    float areaPhase = BurtAtmosphereStableTimePhase(legacyTimeScale * areaSpeed);
    float densityMin = _BurtAtmosphereAreaStarsParams.y;
    float densityMax = _BurtAtmosphereAreaStarsParams.z;
    float areaStars = _BurtAtmosphereAreaStarsTexture.Sample(
        BurtAtmosphere_linear_repeat_sampler,
        rotatedUv * densityMin - 100.0f + float2(0.0f, areaPhase)).r;
    areaStars += _BurtAtmosphereAreaStarsTexture.Sample(
        BurtAtmosphere_linear_repeat_sampler,
        rotatedUv * lerp(densityMin, densityMax, 0.5f) + float2(0.0f, areaPhase)).g;
    areaStars += _BurtAtmosphereAreaStarsTexture.Sample(
        BurtAtmosphere_linear_repeat_sampler,
        rotatedUv * densityMax + 100.0f + float2(0.0f, areaPhase)).b;
    float areaLuminance = pow(areaStars, _BurtAtmosphereAreaStarsFalloffs.x)
        * BurtAtmosphereStarLuminance(_BurtAtmosphereAreaStarsParams.x)
        * areaMask;
    stars += areaLuminance;

    // Preserve the original PhysicalSky operation, including its self-clamp.
    stars *= min(stars, twinkleMask);
    float2 tintUv = skyUv * _BurtAtmosphereStarsTintTransform.xy
        + _BurtAtmosphereStarsTintTransform.zw;
    float3 randomTint = _BurtAtmosphereStarsTintColorTexture.SampleLevel(
        BurtAtmosphere_linear_repeat_sampler,
        tintUv,
        0.0f).rgb;
    stars *= randomTint * _BurtAtmosphereStarsTintColor.rgb;
    stars = BurtAtmosphereAdjustSaturation(
        stars,
        1.0f - _BurtAtmosphereStarsTintColor.a);
    float horizonMask = pow(
        saturate(positionWS.y / 19930.0f),
        max(_BurtAtmosphereStarsControl.z, 1.175494351e-38f));
    stars *= horizonMask;

    float2 galaxyUv = skyUv * 2.0f - 1.0f;
    float2 galaxyCenter = -_BurtAtmosphereGalaxyTransform.zw;
    galaxyUv = BurtAtmosphereRotateUv(galaxyUv, galaxyCenter, _BurtAtmosphereGalaxyParams.x);
    galaxyUv = (galaxyUv + _BurtAtmosphereGalaxyTransform.zw)
        * _BurtAtmosphereGalaxyTransform.xy;
    galaxyUv = (galaxyUv + 1.0f) * 0.5f;
    float3 galaxyCloud = _BurtAtmosphereGalaxyCloudTexture.Sample(
        BurtAtmosphere_linear_clamp_sampler,
        galaxyUv).rgb;
    float3 finalGalaxyCloud = pow(
        galaxyCloud,
        _BurtAtmosphereGalaxyParams.z) * BurtAtmosphereStarLuminance(_BurtAtmosphereGalaxyParams.y);
    float galaxyStarPhase = BurtAtmosphereStableTimePhase(
        legacyTimeScale
        * _BurtAtmosphereGalaxyStarParams.z
        * _BurtAtmosphereGalaxyStarParams.y);
    float2 galaxyStarUv = skyUv * _BurtAtmosphereGalaxyStarParams.y
        + float2(0.0f, galaxyStarPhase);
    float3 galaxyStars = _BurtAtmosphereStarsTexture.Sample(
        BurtAtmosphere_linear_repeat_sampler,
        galaxyStarUv).rgb;
    float3 finalGalaxyStars = pow(
        galaxyStars,
        _BurtAtmosphereGalaxyStarParams.x) * BurtAtmosphereStarLuminance(_BurtAtmosphereGalaxyParams.w);
    finalGalaxyStars *= min(finalGalaxyStars, twinkleMask + 0.25f);
    finalGalaxyStars *= finalGalaxyCloud;
    stars += finalGalaxyStars + finalGalaxyCloud;
#endif

    float moonHalfApex = _BurtAtmosphereMoonGeometry.y;
    float2 moonUv = float2(
        dot(-viewDirection, normalize(moonUp)),
        dot(-viewDirection, normalize(moonRight))) / moonHalfApex;
#if !defined(SHADER_API_MOBILE)
    // PhysicalSky removes the procedural star field under the authored moon
    // position even when the separate moon-disk intensity is zero.
    float moonMask = 1.0f - saturate((1.0f - length(moonUv)) * 70.0f);
    stars *= moonMask;
#endif

    if (_BurtAtmosphereCustomStarControl.x > 0.5f)
    {
        float elapsed = BurtAtmosphereElapsedTime();
        float2 customStarUv = moonUv + _BurtAtmosphereCustomStarTextureTransform.zw;
#if !defined(SHADER_API_MOBILE)
        const float degreesToRadians = 0.017453292f;
        float rotationTime = elapsed * _BurtAtmosphereCustomStarControl.y;
        float rotation = (rotationTime + sin(rotationTime) * 5.0f) * degreesToRadians;
        float2 rotationSinCos = float2(sin(rotation), cos(rotation));
        float2 rotationDirectionX = float2(rotationSinCos.x, -rotationSinCos.y);
        float2 rotationDirectionY = float2(rotationSinCos.y, rotationSinCos.x);
        customStarUv = float2(
            dot(rotationDirectionX, customStarUv),
            dot(rotationDirectionY, customStarUv));
#endif

        float scatterSpeed = _BurtAtmosphereCustomStarControl.w;
        float scatterInterval = _BurtAtmosphereCustomStarScatterInterval;
        float modTime = fmod(elapsed, scatterSpeed + scatterInterval);
#if defined(SHADER_API_MOBILE)
        float scatterPhase = saturate((modTime - scatterInterval) / scatterSpeed);
        float customStarTime1 = 1.0f - abs(scatterPhase * 2.0f - 1.0f);
        float customStarPhase2 = frac(
            _BurtAtmosphereCustomStarIntensityMax.w * elapsed * 0.5f);
        float customStarTri2 = 1.0f
            - abs(customStarPhase2 * 2.0f - 1.0f);
        float customStarTime2 =
            _BurtAtmosphereCustomStarIntensityMin.w * customStarTri2;
#else
        const float twoPi = 6.28318530718f;
        const float pi = 3.14159265359f;
        float scatterPhase = max((modTime - scatterInterval) / scatterSpeed, 0.0f);
        float customStarTime1 = 0.5f * (1.0f - cos(twoPi * scatterPhase));
        float customStarTime2 = _BurtAtmosphereCustomStarIntensityMin.w
            * (1.0f - cos(_BurtAtmosphereCustomStarIntensityMax.w * pi * elapsed));
#endif
        float customStarTime = customStarTime1 + customStarTime2;
        float2 customStarScale = lerp(
            _BurtAtmosphereCustomStarTextureTransform.xy
                / _BurtAtmosphereCustomStarControl.z,
            _BurtAtmosphereCustomStarTextureTransform.xy,
            customStarTime);
        customStarUv *= customStarScale;
        float3 customStar = _BurtAtmosphereCustomStarTexture.SampleLevel(
            BurtAtmosphere_linear_clamp_sampler,
            customStarUv * 0.5f + 0.5f,
            0.0f).rgb;
        float3 customStarIntensity = lerp(
            _BurtAtmosphereCustomStarIntensityMin.rgb,
            _BurtAtmosphereCustomStarIntensityMax.rgb,
            customStarTime);
        stars += customStar * customStarIntensity;
    }

#if !defined(SHADER_API_MOBILE)
    stars *= BurtAtmosphereWeatherCelestialMask(meshUv0, 10.0f);
#endif
    return stars;
}

float3 BurtAtmosphereEvaluatePanoramicClouds(
    float mainLightOcclusion,
    float2 meshUv1)
{
    if (_BurtAtmospherePanoramicCloudControl.x < 0.5f)
    {
        return 0.0f;
    }

    float2 skyUv = meshUv1;
    float isNight = BurtAtmospherePhysicalSkyNightPermutation();
    float verticalTiling = lerp(2.1f, 1.0f, isNight);
    float uvOffset = lerp(
        _BurtAtmospherePanoramicCloudUvParams.x,
        _BurtAtmospherePanoramicCloudUvParams.y,
        isNight);
    float2 cloudUv = skyUv * float2(-1.0f, verticalTiling)
        + float2(
            uvOffset
                + _BurtAtmospherePanoramicCloudUvParams.z
                * BurtAtmosphereElapsedTime(),
            0.0f);
    float3 cloudData;
    if (_BurtAtmospherePanoramicCloudControl.y > 0.5f)
    {
        cloudData = _BurtAtmospherePanoramicCloudDefaultTexture.Sample(
            BurtAtmosphere_linear_repeat_sampler,
            cloudUv).rgb;
    }
    else if (_BurtAtmospherePanoramicCloudControl.z > 0.5f)
    {
        float3 previousData = _BurtAtmospherePanoramicCloudPreviousTexture.Sample(
            BurtAtmosphere_linear_repeat_sampler,
            cloudUv).rgb;
        float3 currentData = _BurtAtmospherePanoramicCloudCurrentTexture.Sample(
            BurtAtmosphere_linear_repeat_sampler,
            cloudUv).rgb;
        float transition = saturate(_BurtAtmospherePanoramicCloudControl.w);
        float radialWeight;
        if (transition <= 0.0f)
        {
            radialWeight = 0.0f;
        }
        else if (transition >= 1.0f)
        {
            radialWeight = 1.0f;
        }
        else
        {
            const float feather = 0.02f;
            float radius = transition * 0.5f;
            float distanceFromCenter = abs(skyUv.x - 0.5f);
            radialWeight = 1.0f - smoothstep(
                radius - feather,
                radius + feather,
                distanceFromCenter);
        }

        cloudData = lerp(previousData, currentData, radialWeight);
    }
    else
    {
        cloudData = _BurtAtmospherePanoramicCloudCurrentTexture.Sample(
            BurtAtmosphere_linear_repeat_sampler,
            cloudUv).rgb;
    }

    bool ignoreTimeOfDayColors = _BurtAtmospherePanoramicCloudUvParams.w > 0.5f;
    float3 baseColor = ignoreTimeOfDayColors
        ? 1.0f
        : _BurtAtmospherePanoramicCloudBaseColor.rgb;
    float3 detailSpecular = ignoreTimeOfDayColors
        ? 1.0f
        : _BurtAtmospherePanoramicCloudDetailSpecular.rgb;
    float3 cloudColor = baseColor * cloudData.r * cloudData.b
        + baseColor * cloudData.b
        + detailSpecular * cloudData.g;
    float luminance = lerp(
        _BurtAtmospherePanoramicCloudLuminanceParams.x,
        _BurtAtmospherePanoramicCloudLuminanceParams.y,
        isNight);
    cloudColor *= luminance * mainLightOcclusion;
    cloudColor *= ignoreTimeOfDayColors
        ? 1.0f
        : _BurtAtmospherePanoramicCloudLuminanceParams.z / 100.0f;
    cloudColor *= step(0.5f, skyUv.y);

    cloudColor = BurtAtmosphereApplyPhysicalSkyCloudDesaturation(cloudColor);
    return cloudColor;
}

float3 BurtAtmosphereEvaluateMoon(
    float3 viewDirection,
    float3 sunDirection,
    float3 moonUp,
    float3 moonRight,
    float2 meshUv0)
{
    if (_BurtAtmosphereMoonVisibility.x < 0.5f || _BurtAtmosphereMoonGeometry.x <= 0.0f)
    {
        return 0.0f;
    }

    float3 normalizedView = normalize(viewDirection);
    float3 normalizedMoonUp = normalize(moonUp);
    float3 normalizedMoonRight = normalize(moonRight);
    float halfApex = _BurtAtmosphereMoonGeometry.y;

#if defined(SHADER_API_MOBILE)
    // Project PhysicalSky mobile permutation consumes the packed moon texture
    // directly: G=surface, B=glow, A=mask. It intentionally ignores phase
    // normal, earthshine, bloom controls and weather-cloud attenuation.
    float2 mobileProjection = float2(
        dot(normalizedView, normalizedMoonRight),
        -dot(normalizedView, normalizedMoonUp));
    float2 mobileMoonUv = mobileProjection / (halfApex * 1.5f);
    float4 mobilePacked = _BurtAtmosphereMoonSurfaceTexture.SampleLevel(
        BurtAtmosphere_linear_clamp_sampler,
        mobileMoonUv * 0.5f + 0.5f,
        0.0f);
    float3 mobileMoon =
        (mobilePacked.g * _BurtAtmosphereMoonAdditionalTint.rgb
            + mobilePacked.b)
        * mobilePacked.a;

    float mobileFlareSize = _BurtAtmosphereMoonGeometry.z;
    float moonCenteredGradient = (
        dot(normalizedView, normalize(sunDirection)) + 1.0f) * 0.5f;
    float flareWeight = saturate(
        (moonCenteredGradient - (1.0f - mobileFlareSize))
        / mobileFlareSize);
    float flare2 = flareWeight * flareWeight;
    float flare4 = flare2 * flare2;
    float flare8 = flare4 * flare4;
    float flare12 = flare8 * flare4;
    float flareApprox12p5 = flare12 * (0.5f + 0.5f * flareWeight);
    mobileMoon += flareApprox12p5 * _BurtAtmosphereMoonFlareTint.rgb;

    mobileMoon *= _BurtAtmosphereMoonSurfaceTint.rgb
        * _BurtAtmosphereMoonGeometry.x;
    return mobileMoon;
#else
    float2 xrenderProjection = float2(
        dot(normalizedView, normalizedMoonRight),
        -dot(normalizedView, normalizedMoonUp));
    float2 xrenderAngles = float2(
        BurtAtmosphereFastAsin(-xrenderProjection.x),
        BurtAtmosphereFastAsin(xrenderProjection.y));
    float2 moonUv = xrenderAngles / halfApex;
    float moonRadius = length(moonUv);
    float moonAlpha = saturate((1.0f - moonRadius) * 70.0f);

    float2 rotationX = float2(_BurtAtmosphereMoonPhase.z, -_BurtAtmosphereMoonPhase.w);
    float2 rotationY = float2(_BurtAtmosphereMoonPhase.w, _BurtAtmosphereMoonPhase.z);
    float2 phasedUv = float2(dot(rotationX, moonUv), dot(rotationY, moonUv));
    float2 sampleUv = phasedUv * 0.5f + 0.5f;
    float moonSurface = _BurtAtmosphereMoonSurfaceTexture.SampleLevel(
        BurtAtmosphere_linear_clamp_sampler,
        sampleUv,
        0.0f).r;
    float3 phaseNormal = normalize(
        _BurtAtmosphereMoonPhaseNormalTexture.SampleLevel(
            BurtAtmosphere_linear_clamp_sampler,
            sampleUv,
            0.0f).xzy - 0.5f);
    float3 phaseLightDirection = float3(
        0.0f,
        _BurtAtmosphereMoonPhase.y,
        _BurtAtmosphereMoonPhase.x);
    float moonPhaseLight = dot(phaseNormal, phaseLightDirection);
    float phaseFactor = saturate(
        moonPhaseLight * _BurtAtmosphereMoonPhysicalParams.x);
    phaseFactor = saturate(
        phaseFactor + _BurtAtmosphereMoonVisibility.y);
    float3 moonDisk = phaseFactor
        * moonAlpha
        * moonSurface
        * _BurtAtmosphereMoonAdditionalTint.rgb;

    float moonGlow = 1.0f - saturate(
        moonRadius / _BurtAtmosphereMoonPhysicalParams.z);
    moonGlow = pow(
        moonGlow,
        _BurtAtmosphereMoonPhysicalParams.w)
        * _BurtAtmosphereMoonPhysicalParams.y;
    float moonGlowAlpha = saturate(
        1.0f - pow(
            moonRadius,
            _BurtAtmosphereMoonBloomParams.x));
    moonGlow = lerp(moonGlow, 0.0f, moonGlowAlpha);
    moonGlow = lerp(0.0f, moonGlow, moonPhaseLight * 0.5f + 0.5f);

    float moonCenteredGradient = (
        dot(normalizedView, normalize(sunDirection)) + 1.0f) * 0.5f;
    float flareSize = _BurtAtmosphereMoonGeometry.z;
    float flareWeight = saturate(
        (moonCenteredGradient - (1.0f - flareSize)) / flareSize);
    float3 moonFlare = pow(
        flareWeight,
        _BurtAtmosphereMoonGeometry.w)
        * _BurtAtmosphereMoonFlareTint.rgb;

    float3 moonLuminance = (moonDisk + moonGlow.xxx + moonFlare)
        * _BurtAtmosphereMoonSurfaceTint.rgb
        * _BurtAtmosphereMoonGeometry.x;
    moonLuminance *= BurtAtmosphereWeatherCelestialMask(meshUv0, 40.0f);
    return moonLuminance;
#endif
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
    float bottomRadius = max(planetParams.x, 0.1f);
    float topRadius = bottomRadius + max(planetParams.y, 0.1f);
    float h = sqrt(max(topRadius * topRadius - bottomRadius * bottomRadius, 0.0f));
    float rho = sqrt(max(viewRadius * viewRadius - bottomRadius * bottomRadius, 0.0f));
    float discriminant = viewRadius * viewRadius * (viewUp * viewUp - 1.0f) + topRadius * topRadius;
    float distance = max(0.0f, -viewRadius * viewUp + sqrt(discriminant));
    float distanceMin = topRadius - viewRadius;
    float distanceMax = rho + h;
    return float2((distance - distanceMin) / (distanceMax - distanceMin), rho / h);
}

float BurtAtmosphereAcosFast4(float value)
{
    const float pi = 3.14159265359f;
    float x1 = abs(value);
    float x2 = x1 * x1;
    float x3 = x2 * x1;
    float result = -0.2121144f * x1 + 1.5707288f;
    result = 0.0742610f * x2 + result;
    result = -0.0187293f * x3 + result;
    result = sqrt(1.0f - x1) * result;
    return value >= 0.0f ? result : pi - result;
}

float BurtAtmosphereAtan2Fast(float y, float x)
{
    const float pi = 3.14159265359f;
    const float halfPi = 1.57079632679f;
    float t0 = max(abs(x), abs(y));
    float t1 = min(abs(x), abs(y));
    float t3 = t1 / t0;
    float t4 = t3 * t3;
    t0 = 0.0872929f;
    t0 = t0 * t4 - 0.301895f;
    t0 = t0 * t4 + 1.0f;
    t3 = t0 * t3;
    t3 = abs(y) > abs(x) ? halfPi - t3 : t3;
    t3 = x < 0.0f ? pi - t3 : t3;
    t3 = y < 0.0f ? -t3 : t3;
    return t3;
}

float2 BurtAtmosphereMapSkyViewParamsToUv(float3 viewDirection, float4 planetParams)
{
    const float pi = 3.14159265359f;
    float bottomRadius = max(planetParams.x, 0.1f);
    float viewRadius = BurtAtmosphereCameraRadius(planetParams);
    float3 direction = normalize(viewDirection);
    float viewUp = direction.y;
    float horizonDistance = sqrt(viewRadius * viewRadius - bottomRadius * bottomRadius);
    float cosBeta = horizonDistance / viewRadius;
    float beta = BurtAtmosphereAcosFast4(cosBeta);
    float zenithHorizonAngle = pi - beta;
    float viewZenithAngle = BurtAtmosphereAcosFast4(viewUp);
    bool intersectsGround = viewUp <= -cosBeta;
    float v;
    if (!intersectsGround)
    {
        float coord = viewZenithAngle / zenithHorizonAngle;
        coord = 1.0f - coord;
        coord = sqrt(coord);
        coord = 1.0f - coord;
        v = coord * 0.5f;
    }
    else
    {
        float coord = (viewZenithAngle - zenithHorizonAngle) / beta;
        coord = sqrt(coord);
        v = coord * 0.5f + 0.5f;
    }

    float u = (BurtAtmosphereAtan2Fast(-direction.x, -direction.z) + pi) / (2.0f * pi);
    // XRender stores endpoint directions by applying FromSubUvsToUnit while
    // generating the LUT, then samples this parameter coordinate directly with
    // a linear-clamp sampler. Do not apply a second forward sub-UV transform.
    return float2(u, v);
}

float3 BurtAtmosphereSampleTransmittance(float viewUp, float4 planetParams)
{
    float bottomRadius = max(planetParams.x, 0.1f);
    float topRadius = bottomRadius + max(planetParams.y, 0.1f);
    float viewRadius = BurtAtmosphereCameraRadius(planetParams);
    float sampleViewUp = clamp(viewUp, -1.0f, 1.0f);
    float3 direction = float3(
        sqrt(saturate(1.0f - sampleViewUp * sampleViewUp)),
        sampleViewUp,
        0.0f);
    float3 origin = float3(0.0f, viewRadius, 0.0f);
    bool sampleLut = true;
    if (viewRadius > topRadius)
    {
        float entryDistance = BurtAtmosphereRaySphereNearest(origin, direction, topRadius);
        if (entryDistance < 0.0f)
        {
            // A ray that remains in space crosses no participating medium.
            sampleLut = false;
        }
        else
        {
            float3 cameraUp = normalize(origin);
            origin = origin + direction * entryDistance - cameraUp * 0.005f;
            viewRadius = length(origin);
            sampleViewUp = dot(normalize(origin), direction);
        }
    }

    float3 transmittance = float3(1.0f, 1.0f, 1.0f);
    // XRender uses SAMPLE_TEXTURE2D_LOD with SamplerLinearClamp. BRP's
    // runtime LUT is bilinear, clamp and mipless; tex2Dlod preserves the
    // explicit level-zero contract and avoids derivative-dependent sampling.
    if (sampleLut)
    {
        float2 transmittanceUv =
            BurtAtmosphereMapTransmittanceParamsToUv(
                viewRadius,
                sampleViewUp,
                planetParams);
        transmittance = tex2Dlod(
            _BurtAtmosphereTransmittanceLut,
            float4(transmittanceUv, 0.0f, 0.0f)).rgb;
    }

    return transmittance;
}

float3 BurtAtmosphereSampleSkyView(float3 viewDirection, float4 planetParams)
{
    // XRender's AtmosphereCommon only evaluates integrated SkyView radiance
    // while the camera is strictly below the top atmosphere boundary. Weather,
    // celestial and panoramic-cloud terms remain independent of this gate.
    float bottomRadius = max(planetParams.x, 0.1f);
    float topRadius = bottomRadius + max(planetParams.y, 0.1f);
    if (BurtAtmosphereCameraRadius(planetParams) >= topRadius)
    {
        return 0.0f;
    }

    float2 skyViewUv =
        BurtAtmosphereMapSkyViewParamsToUv(viewDirection, planetParams);
    return tex2Dlod(
        _BurtAtmosphereSkyViewLut,
        float4(skyViewUv, 0.0f, 0.0f)).rgb;
}

float3 BurtAtmosphereSampleMultipleScattering(float sunUp)
{
    // Multiple-scattering is defined only inside the atmosphere. Space views
    // use the top layer after their ray has been moved to the atmosphere entry.
    float2 multipleScatteringUv = float2(
        saturate(sunUp * 0.5f + 0.5f),
        saturate(_BurtAtmosphereCameraAltitude01));
    return tex2Dlod(
        _BurtAtmosphereMultipleScatteringLut,
        float4(multipleScatteringUv, 0.0f, 0.0f)).rgb;
}

float3 BurtAtmosphereSampleHorizontalScattering(float component)
{
    // XRender bakes SkyLuminanceFactor into Rayleigh/Mie and applies its
    // multiple-scattering scale again while generating this three-value LUT.
    return BurtAtmosphereLoadHorizontalScattering(component);
}

float BurtAtmosphereHorizontalRayleighPhase(float cosTheta)
{
    const float pi = 3.14159265359f;
    return 3.0f * (1.0f + cosTheta * cosTheta) / (16.0f * pi);
}

float BurtAtmosphereHorizontalSchlickPhase(float anisotropy, float cosTheta)
{
    return BurtAtmosphereSchlickPhase(anisotropy, cosTheta);
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

float BurtAtmosphereFogNonLinearW(float distanceRatio)
{
    return saturate(sqrt(max(distanceRatio, 0.0f)));
}

float BurtAtmosphereFogStartWeight(float distanceRatio)
{
    float nonLinearSlice = BurtAtmosphereFogNonLinearW(distanceRatio)
        * (float)BURT_ATMOSPHERE_FOG_LUT_DEPTH;
    const float halfSliceDepth = 0.70710678118654752440084436210485f;
    if (nonLinearSlice < halfSliceDepth)
    {
        return saturate(nonLinearSlice * nonLinearSlice * 2.0f);
    }

    return 1.0f;
}

float4 BurtAtmosphereSampleFog(float2 screenUv, float distanceRatio)
{
    return tex3Dlod(
        _BurtAtmosphereFogLut,
        float4(
            saturate(screenUv),
            BurtAtmosphereFogNonLinearW(distanceRatio),
            0.0f));
}

#endif
