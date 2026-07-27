Shader "Hidden/BurtRP/AtmosphereScattering"
{
    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        Pass
        {
            Name "Burt Atmosphere Scattering"

            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtPreExposure.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/BurtAtmosphereLut.hlsl"

            UNITY_DECLARE_DEPTH_TEXTURE(_BurtCameraDepthTexture);

            float4 _BurtMainLightDirection;
            float4 _BurtMainLightColor;
            float4 _BurtMainLightColorOuterSpace;
            float _BurtMainLightOcclusionFactor;
            float _BurtAtmosphereRayleighIntensity;
            float _BurtAtmosphereMieIntensity;
            float _BurtAtmosphereMieAnisotropy;
            float4 _BurtAtmosphereRayleighScatteringCoefficient;
            float4 _BurtAtmosphereMieScatteringCoefficient;
            float4 _BurtAtmosphereMieAbsorptionCoefficient;
            float4 _BurtAtmosphereOzoneAbsorptionCoefficient;
            float4 _BurtAtmospherePlanetParams;
            float4 _BurtAtmosphereGroundColor;
            float4 _BurtAtmosphereSkyTint;
            float _BurtAtmosphereSunIntensity;
            float4 _BurtAtmosphereSunDirection;
            float4 _BurtAtmosphereSunParams;
            float4 _BurtAtmosphereSunDiskLuminanceAndCosHalfApex;
            float4 _BurtAtmosphereHorizonColor;
            float4 _BurtAtmosphereHorizonSunsetColor;
            float4 _BurtAtmosphereHorizonParams;
            float4 _BurtAtmosphereGroundParams;
            float4 _BurtAtmosphereExposureParams;
            float4 _BurtAtmosphereAerialPerspectiveParams;
            float4 _BurtAtmosphereAerialPerspectiveTint;
            float4 _BurtAtmosphereAerialPerspectiveFadeParams;
            float4 _BurtAtmosphereFogLutDistanceParams;
            float4x4 _BurtAtmosphereInverseViewProjection;
            float3 _BurtAtmosphereCameraPositionWS;
            float4x4 _BurtAtmosphereWorldToSkyViewLocal;
            float _BurtAtmosphereDebugMode;

            static const float PI = 3.14159265359f;

            struct Attributes
            {
                uint VertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 PositionCS : SV_POSITION;
                float2 ScreenUV : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 uv = float2((input.VertexID << 1) & 2, input.VertexID & 2);
                output.PositionCS = float4(uv * 2.0f - 1.0f, 0.0f, 1.0f);
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0f - uv.y;
                #endif
                output.ScreenUV = uv;
                return output;
            }

            float3 SafeNormalize(float3 value, float3 fallback)
            {
                float lengthSq = dot(value, value);
                return lengthSq > 1.0e-8f ? value * rsqrt(lengthSq) : fallback;
            }

            float RayleighPhase(float cosTheta)
            {
                return (3.0f / (16.0f * PI)) * (1.0f + cosTheta * cosTheta);
            }

            float MiePhase(float cosTheta, float g)
            {
                float g2 = g * g;
                float denom = max(0.05f, pow(abs(1.0f + g2 - 2.0f * g * cosTheta), 1.5f));
                return (1.0f / (4.0f * PI)) * ((1.0f - g2) / denom);
            }

            float EstimateAirMass(float viewUp, float scaleHeight, float atmosphereHeight)
            {
                float horizonBoost = rcp(max(abs(viewUp) + 0.16f, 0.16f));
                float heightRatio = saturate(atmosphereHeight / max(scaleHeight * 12.0f, 0.001f));
                return saturate(heightRatio * horizonBoost * 0.16f);
            }

            float3 NormalizeLightColor(float3 lightColor)
            {
                float peak = max(max(lightColor.r, lightColor.g), lightColor.b);
                return peak > 0.001f ? lightColor / peak : 1.0f;
            }

            float SmoothRange(float edge0, float edge1, float value)
            {
                float range = edge1 - edge0;
                float safeRange = abs(range) > 1.0e-5f ? range : (range < 0.0f ? -1.0e-5f : 1.0e-5f);
                float t = saturate((value - edge0) / safeRange);
                return t * t * (3.0f - 2.0f * t);
            }

            float3 EvaluateAtmosphere(float3 viewDirWS)
            {
                float3 viewDirAtmosphere = mul((float3x3)_BurtAtmosphereWorldToSkyViewLocal, viewDirWS);
                float3 lightDirWS = SafeNormalize(mul((float3x3)_BurtAtmosphereWorldToSkyViewLocal, _BurtAtmosphereSunDirection.xyz), float3(0.0f, 1.0f, 0.0f));
                float3 lightColor = NormalizeLightColor(max(_BurtMainLightColorOuterSpace.rgb, 0.0f));
                float mainLightOcclusion = saturate(_BurtMainLightOcclusionFactor);
                float3 atmosphereLight = max(_BurtMainLightColorOuterSpace.rgb, 0.0f) * max(_BurtAtmosphereSunIntensity, 0.0f) * mainLightOcclusion;
                float cosTheta = dot(viewDirAtmosphere, lightDirWS);
                float viewUp = viewDirAtmosphere.y;
                float up01 = saturate(viewUp * 0.5f + 0.5f);

                float atmosphereHeight = max(_BurtAtmospherePlanetParams.y, 1.0f);
                float rayleighScaleHeight = max(_BurtAtmospherePlanetParams.z, 0.1f);
                float mieScaleHeight = max(_BurtAtmospherePlanetParams.w, 0.1f);
                float rayleighAir = EstimateAirMass(viewUp, rayleighScaleHeight, atmosphereHeight);
                float mieAir = EstimateAirMass(viewUp, mieScaleHeight, atmosphereHeight);

                float3 skyTint = max(_BurtAtmosphereSkyTint.rgb, 0.0f);
                float3 groundColor = max(_BurtAtmosphereGroundColor.rgb, 0.0f);
                float rayleighIntensity = max(_BurtAtmosphereRayleighIntensity, 0.0f);
                float mieIntensity = max(_BurtAtmosphereMieIntensity, 0.0f);
                float3 zenithColor = float3(0.18f, 0.36f, 0.75f) * skyTint;
                float3 horizonBaseColor = max(_BurtAtmosphereHorizonColor.rgb, 0.0f) * skyTint;
                float3 horizonSunsetColor = max(_BurtAtmosphereHorizonSunsetColor.rgb, 0.0f) * lightColor;
                float horizonIntensity = max(_BurtAtmosphereHorizonParams.x, 0.0f);
                float horizonFalloff = max(_BurtAtmosphereHorizonParams.y, 0.1f);
                float horizonSunsetInfluence = saturate(_BurtAtmosphereHorizonParams.z);
                float groundContribution = max(_BurtAtmosphereGroundParams.x, 0.0f);
                float groundBlendStart = _BurtAtmosphereGroundParams.y;
                float groundBlendEnd = _BurtAtmosphereGroundParams.z;
                float3 horizonColor = lerp(horizonBaseColor, horizonSunsetColor, saturate(1.0f - lightDirWS.y) * horizonSunsetInfluence);
                float3 baseSky = lerp(horizonColor * horizonIntensity, zenithColor, pow(up01, horizonFalloff));

                float3 rayleighBeta = max(_BurtAtmosphereRayleighScatteringCoefficient.rgb, 0.0f) * (rayleighIntensity * 13.6f);
                float3 mieBeta = max(_BurtAtmosphereMieScatteringCoefficient.rgb, 0.0f) * (mieIntensity / 0.12f) * 4.8f;
                float3 ozoneExtinction = max(_BurtAtmosphereOzoneAbsorptionCoefficient.rgb, 0.0f) * 10.0f;
                float3 transmittance = exp(-(rayleighBeta * rayleighAir + (mieBeta + max(_BurtAtmosphereMieAbsorptionCoefficient.rgb, 0.0f) * (mieIntensity / 0.12f) * 4.8f) * mieAir + ozoneExtinction * saturate(mieAir * 0.35f)) * 0.45f);
                float3 inScatter = rayleighBeta * RayleighPhase(cosTheta) * rayleighAir;
                inScatter += mieBeta * MiePhase(cosTheta, _BurtAtmosphereMieAnisotropy) * mieAir;

                float sunHaloSize = max(_BurtAtmosphereSunParams.z, 0.05f);
                float sunHaloIntensity = max(_BurtAtmosphereSunParams.w, 0.0f);
                float sunHaloPower = max(1.0f, 12.0f / sunHaloSize);
                // XRender converts the authored full angular diameter to a half-angle,
                // divides outer-space illuminance by the corresponding cone solid angle,
                // then softens the outer half of the disk to avoid bloom/TAA flicker.
                float sunDiskCosHalfApex = saturate(_BurtAtmosphereSunDiskLuminanceAndCosHalfApex.w);
                float sunDiskCosRange = max(1.0f - sunDiskCosHalfApex, 1.0e-7f);
                float sunDisk = cosTheta > sunDiskCosHalfApex
                    ? saturate(2.0f * (cosTheta - sunDiskCosHalfApex) / sunDiskCosRange)
                    : 0.0f;
                float sunHalo = pow(saturate(cosTheta), sunHaloPower) * mieIntensity * 0.18f;
                // XRender rejects the direct solar term when the view ray intersects
                // the planet. Apply the same visibility before all analytic, LUT and
                // debug branches so the disk and its authored halo cannot shine through
                // the ground or the planet limb, including for cameras in space.
                float planetVisibility = BurtAtmospherePlanetVisibility(viewDirAtmosphere, _BurtAtmospherePlanetParams);
                sunDisk *= planetVisibility;
                sunHalo *= planetVisibility;
                float exposureScale = max(_BurtAtmosphereExposureParams.x, 0.0f);
                float exposureSafeSun = min(_BurtAtmosphereSunIntensity, max(_BurtAtmosphereExposureParams.y, 0.1f));
                float3 sunDiskContribution = min(
                    sunDisk
                        * max(_BurtAtmosphereSunDiskLuminanceAndCosHalfApex.rgb, 0.0f)
                        * max(_BurtAtmosphereStylizedSunDiskColorScale.rgb, 0.0f),
                    64000.0f) * mainLightOcclusion;
                float3 sunHaloContribution = sunHalo * sunHaloIntensity
                    * max(_BurtAtmosphereStylizedSunDiskColorScale.rgb, 0.0f)
                    * lightColor * exposureSafeSun * mainLightOcclusion;
                float3 physicalSkyBackground = baseSky * (0.16f + rayleighIntensity * 0.18f);
                physicalSkyBackground += inScatter * skyTint * lightColor * exposureSafeSun * mainLightOcclusion;
                float3 directSunContribution = sunDiskContribution + sunHaloContribution;

                float groundBlend = SmoothRange(groundBlendStart, groundBlendEnd, viewUp);
                physicalSkyBackground = lerp(physicalSkyBackground, groundColor * groundContribution, groundBlend);
                // XRender grades integrated sky scattering independently from the
                // direct solar term. Preserve the analytic path's previous ground
                // attenuation while applying the RGB factor only to the background.
                float3 skyColor = physicalSkyBackground * max(_BurtAtmosphereSkyLuminanceFactor.rgb, 0.0f)
                    + directSunContribution * (1.0f - groundBlend);
                float3 stylizedSky = BurtAtmosphereEvaluateStylizedSky(
                    viewDirAtmosphere,
                    lightDirWS,
                    _BurtAtmospherePlanetParams,
                    groundColor,
                    _BurtAtmosphereGroundParams.xyz,
                    mainLightOcclusion) + directSunContribution;
                float3 lutSky = 0.0f;
                float3 lutMultiple = 0.0f;
                float3 lutTransmittance = 1.0f;

                if (_BurtAtmosphereUseLuts > 0.5f)
                {
                    lutSky = BurtAtmosphereSampleSkyView(viewDirAtmosphere, _BurtAtmospherePlanetParams);
                    lutTransmittance = BurtAtmosphereSampleTransmittance(viewUp, _BurtAtmospherePlanetParams);
                    lutMultiple = BurtAtmosphereSampleMultipleScattering(lightDirWS.y);
                    // SkyView is the physically integrated sky radiance. Keep it direct rather
                    // than blending the legacy heuristic sky, then add the separate solar term
                    // (the same SkyView + SunDisk composition used by XRender's combine pass).
                    // The disk/halo is attenuated toward the light but SkyView itself is not.
                    float3 sunTransmittance = BurtAtmosphereSampleTransmittance(lightDirWS.y, _BurtAtmospherePlanetParams);
                    directSunContribution = (sunDiskContribution + sunHaloContribution) * sunTransmittance;
                    skyColor = lutSky * atmosphereLight * max(_BurtAtmosphereSkyTint.rgb, 0.0f) * max(_BurtAtmosphereSkyLuminanceFactor.rgb, 0.0f) + directSunContribution;
                    stylizedSky = BurtAtmosphereEvaluateStylizedSky(
                        viewDirAtmosphere,
                        lightDirWS,
                        _BurtAtmospherePlanetParams,
                        groundColor,
                        _BurtAtmosphereGroundParams.xyz,
                        mainLightOcclusion) + directSunContribution;
                    transmittance = 1.0f;
                }

                skyColor = lerp(skyColor, stylizedSky, saturate(_BurtAtmosphereStylizedParams.x));
                float3 moonDirection = SafeNormalize(
                    mul((float3x3)_BurtAtmosphereWorldToSkyViewLocal, _BurtAtmosphereMoonDirection.xyz),
                    float3(0.0f, 0.0f, -1.0f));
                float3 moonUp = SafeNormalize(
                    mul((float3x3)_BurtAtmosphereWorldToSkyViewLocal, _BurtAtmosphereMoonUp.xyz),
                    float3(0.0f, 1.0f, 0.0f));
                float3 moonRight = SafeNormalize(
                    mul((float3x3)_BurtAtmosphereWorldToSkyViewLocal, _BurtAtmosphereMoonRight.xyz),
                    float3(1.0f, 0.0f, 0.0f));
                float3 moonTransmittance = _BurtAtmosphereUseLuts > 0.5f
                    ? BurtAtmosphereSampleTransmittance(viewUp, _BurtAtmospherePlanetParams)
                    : 1.0f;
                skyColor += BurtAtmosphereEvaluateMoon(
                    viewDirAtmosphere,
                    lightDirWS,
                    moonDirection,
                    moonUp,
                    moonRight,
                    moonTransmittance,
                    _BurtAtmospherePlanetParams);

                if (_BurtAtmosphereDebugMode > 0.5f && _BurtAtmosphereDebugMode < 1.5f)
                {
                    return saturate(inScatter * skyTint * 8.0f);
                }

                if (_BurtAtmosphereDebugMode > 1.5f && _BurtAtmosphereDebugMode < 2.5f)
                {
                    float mieDebug = saturate((MiePhase(cosTheta, _BurtAtmosphereMieAnisotropy) * mieAir) * mieIntensity * 8.0f);
                    return float3(mieDebug, mieDebug, mieDebug);
                }

                if (_BurtAtmosphereDebugMode > 2.5f && _BurtAtmosphereDebugMode < 3.5f)
                {
                    return saturate(_BurtAtmosphereUseLuts > 0.5f ? lutTransmittance : transmittance);
                }

                if (_BurtAtmosphereDebugMode > 8.5f)
                {
                    if (_BurtAtmosphereDebugMode < 9.5f)
                    {
                        float debugTransmittance = _BurtAtmosphereUseLuts > 0.5f ? lutTransmittance.b : transmittance.b;
                        return float3(saturate(rayleighAir), saturate(mieAir), saturate(debugTransmittance));
                    }

                    if (_BurtAtmosphereDebugMode < 10.5f)
                    {
                        float3 diskDebug = sunDisk.xxx;
                        if (_BurtAtmosphereUseLuts > 0.5f)
                        {
                            diskDebug *= BurtAtmosphereSampleTransmittance(lightDirWS.y, _BurtAtmospherePlanetParams);
                        }
                        return saturate(diskDebug * 12.0f);
                    }

                    if (_BurtAtmosphereDebugMode < 11.5f)
                    {
                        float haloDebug = sunHalo * sunHaloIntensity;
                        return float3(saturate(haloDebug * 8.0f), saturate(haloDebug * 5.0f), saturate(haloDebug * 2.0f));
                    }

                    if (_BurtAtmosphereDebugMode < 12.5f)
                    {
                        float horizonWeight = saturate(1.0f - pow(up01, horizonFalloff));
                        return float3(horizonWeight.xxx);
                    }

                    if (_BurtAtmosphereDebugMode < 13.5f)
                    {
                        return float3(groundBlend.xxx);
                    }

                    if (_BurtAtmosphereDebugMode < 14.5f)
                    {
                        return saturate(viewDirAtmosphere * 0.5f + 0.5f);
                    }

                    if (_BurtAtmosphereDebugMode < 15.5f)
                    {
                        return _BurtAtmosphereUseLuts > 0.5f
                            ? max(lutSky * atmosphereLight * max(_BurtAtmosphereSkyTint.rgb, 0.0f) * max(_BurtAtmosphereSkyLuminanceFactor.rgb, 0.0f), 0.0f)
                            : 0.0f;
                    }

                    if (_BurtAtmosphereDebugMode < 16.5f)
                    {
                        return _BurtAtmosphereUseLuts > 0.5f ? saturate(lutMultiple * 64.0f) : 0.0f;
                    }

                    if (_BurtAtmosphereUseLuts <= 0.5f)
                    {
                        return 0.0f;
                    }

                    float3 horizontalDebug = BurtAtmosphereSampleHorizontalScattering(0.0f) + BurtAtmosphereSampleHorizontalScattering(1.0f) + BurtAtmosphereSampleHorizontalScattering(2.0f);
                    return max(horizontalDebug, 0.0f);
                }

                return max(skyColor * transmittance * exposureScale, 0.0f);
            }

            bool IsSkyPixel(float rawDepth)
            {
                #if defined(UNITY_REVERSED_Z)
                    return rawDepth <= 0.00001f;
                #else
                    return rawDepth >= 0.99999f;
                #endif
            }

            float4 BuildClipPosition(float2 screenUV)
            {
                float2 clipXY = screenUV * 2.0f - 1.0f;
                #if UNITY_UV_STARTS_AT_TOP
                    clipXY.y = -clipXY.y;
                #endif

                #if defined(UNITY_REVERSED_Z)
                    float clipZ = 0.0f;
                #else
                    float clipZ = 1.0f;
                #endif

                return float4(clipXY, clipZ, 1.0f);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_BurtCameraDepthTexture, input.ScreenUV);
                if (!IsSkyPixel(rawDepth))
                {
                    discard;
                }

                float4 clip = BuildClipPosition(input.ScreenUV);
                float4 farWS = mul(_BurtAtmosphereInverseViewProjection, clip);
                farWS.xyz /= max(abs(farWS.w), 1.0e-6f);
                float3 viewDirWS = SafeNormalize(farWS.xyz - _BurtAtmosphereCameraPositionWS, float3(0.0f, 0.0f, 1.0f));
                return float4(BurtApplyPreExposure(EvaluateAtmosphere(viewDirWS)), 1.0f);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Burt Atmosphere Aerial Perspective"

            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtPreExposure.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/BurtAtmosphereLut.hlsl"

            sampler2D _BurtCameraColorTexture;
            UNITY_DECLARE_DEPTH_TEXTURE(_BurtCameraDepthTexture);

            float4 _BurtMainLightDirection;
            float4 _BurtMainLightColor;
            float4 _BurtMainLightColorOuterSpace;
            float _BurtMainLightOcclusionFactor;
            float _BurtAtmosphereRayleighIntensity;
            float _BurtAtmosphereMieIntensity;
            float _BurtAtmosphereMieAnisotropy;
            float4 _BurtAtmosphereRayleighScatteringCoefficient;
            float4 _BurtAtmosphereMieScatteringCoefficient;
            float4 _BurtAtmosphereMieAbsorptionCoefficient;
            float4 _BurtAtmosphereOzoneAbsorptionCoefficient;
            float4 _BurtAtmospherePlanetParams;
            float4 _BurtAtmosphereGroundColor;
            float4 _BurtAtmosphereSkyTint;
            float _BurtAtmosphereSunIntensity;
            float4 _BurtAtmosphereSunDirection;
            float4 _BurtAtmosphereSunParams;
            float4 _BurtAtmosphereHorizonColor;
            float4 _BurtAtmosphereHorizonSunsetColor;
            float4 _BurtAtmosphereHorizonParams;
            float4 _BurtAtmosphereGroundParams;
            float4 _BurtAtmosphereExposureParams;
            float4 _BurtAtmosphereAerialPerspectiveParams;
            float4 _BurtAtmosphereAerialPerspectiveTint;
            float4 _BurtAtmosphereAerialPerspectiveFadeParams;
            float4 _BurtAtmosphereFogLutDistanceParams;
            float4x4 _BurtAtmosphereInverseViewProjection;
            float3 _BurtAtmosphereCameraPositionWS;
            float4x4 _BurtAtmosphereWorldToSkyViewLocal;
            float _BurtAtmosphereDebugMode;

            static const float PI = 3.14159265359f;

            struct Attributes
            {
                uint VertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 PositionCS : SV_POSITION;
                float2 ScreenUV : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 uv = float2((input.VertexID << 1) & 2, input.VertexID & 2);
                output.PositionCS = float4(uv * 2.0f - 1.0f, 0.0f, 1.0f);
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0f - uv.y;
                #endif
                output.ScreenUV = uv;
                return output;
            }

            float3 SafeNormalize(float3 value, float3 fallback)
            {
                float lengthSq = dot(value, value);
                return lengthSq > 1.0e-8f ? value * rsqrt(lengthSq) : fallback;
            }

            float RayleighPhase(float cosTheta)
            {
                return (3.0f / (16.0f * PI)) * (1.0f + cosTheta * cosTheta);
            }

            float MiePhase(float cosTheta, float g)
            {
                float g2 = g * g;
                float denom = max(0.05f, pow(abs(1.0f + g2 - 2.0f * g * cosTheta), 1.5f));
                return (1.0f / (4.0f * PI)) * ((1.0f - g2) / denom);
            }

            float3 NormalizeLightColor(float3 lightColor)
            {
                float peak = max(max(lightColor.r, lightColor.g), lightColor.b);
                return peak > 0.001f ? lightColor / peak : 1.0f;
            }

            bool IsSkyPixel(float rawDepth)
            {
                #if defined(UNITY_REVERSED_Z)
                    return rawDepth <= 0.00001f;
                #else
                    return rawDepth >= 0.99999f;
                #endif
            }

            float4 BuildClipPosition(float2 screenUV, float rawDepth)
            {
                float2 clipXY = screenUV * 2.0f - 1.0f;
                #if UNITY_UV_STARTS_AT_TOP
                    clipXY.y = -clipXY.y;
                #endif

                #if defined(UNITY_REVERSED_Z)
                    float clipZ = rawDepth;
                #else
                    float clipZ = lerp(UNITY_NEAR_CLIP_VALUE, 1.0f, rawDepth);
                #endif

                return float4(clipXY, clipZ, 1.0f);
            }

            float3 ReconstructPositionWS(float2 screenUV, float rawDepth)
            {
                float4 positionWS = mul(_BurtAtmosphereInverseViewProjection, BuildClipPosition(screenUV, rawDepth));
                positionWS.xyz /= max(abs(positionWS.w), 1.0e-6f);
                return positionWS.xyz;
            }

            float3 EvaluateAerialInscatter(float3 viewDirWS, float distanceFade, float heightFade)
            {
                float3 lightDirWS = SafeNormalize(_BurtAtmosphereSunDirection.xyz, SafeNormalize(_BurtMainLightDirection.xyz, float3(0.0f, 1.0f, 0.0f)));
                float3 lightColor = NormalizeLightColor(max(_BurtMainLightColorOuterSpace.rgb, 0.0f));
                float mainLightOcclusion = saturate(_BurtMainLightOcclusionFactor);
                float3 skyTint = max(_BurtAtmosphereSkyTint.rgb, 0.0f);
                float rayleighIntensity = max(_BurtAtmosphereRayleighIntensity, 0.0f);
                float mieIntensity = max(_BurtAtmosphereMieIntensity, 0.0f);
                float cosTheta = dot(viewDirWS, lightDirWS);

                float rayleigh = RayleighPhase(cosTheta) * rayleighIntensity;
                float mie = MiePhase(cosTheta, _BurtAtmosphereMieAnisotropy) * mieIntensity;
                float horizonTint = saturate(1.0f - abs(viewDirWS.y));
                float3 aerialTint = max(_BurtAtmosphereAerialPerspectiveTint.rgb, 0.0f);
                float3 airColor = lerp(float3(0.34f, 0.52f, 0.88f) * skyTint, float3(0.78f, 0.84f, 0.95f) * skyTint, horizonTint);
                airColor *= aerialTint;
                float3 scatter = airColor * (0.08f + rayleigh * 0.55f) + lightColor * mie * 0.45f;
                scatter *= min(_BurtAtmosphereSunIntensity, max(_BurtAtmosphereExposureParams.y, 0.1f)) * mainLightOcclusion * max(_BurtAtmosphereExposureParams.x, 0.0f) * distanceFade * heightFade;
                return max(scatter, 0.0f);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 sourceColor = tex2D(_BurtCameraColorTexture, input.ScreenUV).rgb;
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_BurtCameraDepthTexture, input.ScreenUV);
                float intensity = _BurtAtmosphereAerialPerspectiveParams.x;
                float distanceScale = max(_BurtAtmosphereAerialPerspectiveParams.y, 1.0f);
                float heightFalloff = _BurtAtmosphereAerialPerspectiveParams.z;
                float samplingDistanceScale = max(_BurtAtmosphereFogLutDistanceParams.z, 0.0f);
                float luminanceScale = max(_BurtAtmosphereFogLutDistanceParams.w, 0.0f);
                float nearFadeStart = _BurtAtmosphereAerialPerspectiveFadeParams.x;
                float nearFadeEnd = max(_BurtAtmosphereAerialPerspectiveFadeParams.y, nearFadeStart + 0.001f);
                float maxOpacity = saturate(_BurtAtmosphereAerialPerspectiveFadeParams.z);
                float affectsSkyPixels = _BurtAtmosphereAerialPerspectiveFadeParams.w;
                bool skyPixel = IsSkyPixel(rawDepth);
                bool cameraInsideAtmosphere = _BurtAtmosphereCameraAltitude01 < 1.0f;
                bool aerialDebug = _BurtAtmosphereDebugMode > 3.5f && _BurtAtmosphereDebugMode < 8.5f;
                // XRender does not apply atmosphere fog to the sky while the
                // camera is inside the atmosphere. From space, sky rays may cross
                // the atmosphere and are evaluated when this pass is placed after sky.
                if ((!aerialDebug && _BurtAtmosphereDebugMode > 0.5f)
                    || (skyPixel && (cameraInsideAtmosphere || affectsSkyPixels < 0.5f))
                    || _BurtAtmosphereAerialPerspectiveParams.w < 0.5f)
                {
                    return float4(sourceColor, 1.0f);
                }

                float distanceWS;
                float heightFade;
                float3 viewDirWS;
                if (skyPixel)
                {
                    float4 farWS = mul(_BurtAtmosphereInverseViewProjection, BuildClipPosition(input.ScreenUV, rawDepth));
                    farWS.xyz /= max(abs(farWS.w), 1.0e-6f);
                    float3 cameraToFarPlane = farWS.xyz - _BurtAtmosphereCameraPositionWS;
                    distanceWS = length(cameraToFarPlane);
                    viewDirWS = SafeNormalize(cameraToFarPlane, float3(0.0f, 0.0f, 1.0f));
                    heightFade = 1.0f;
                }
                else
                {
                    float3 positionWS = ReconstructPositionWS(input.ScreenUV, rawDepth);
                    float3 cameraToPixel = positionWS - _BurtAtmosphereCameraPositionWS;
                    distanceWS = length(cameraToPixel);
                    viewDirWS = SafeNormalize(cameraToPixel, float3(0.0f, 0.0f, 1.0f));
                    float3 atmosphereCameraToPixel = mul((float3x3)_BurtAtmosphereWorldToSkyViewLocal, cameraToPixel);
                    heightFade = exp(-max(atmosphereCameraToPixel.y, 0.0f) * heightFalloff);
                }

                float distanceRatio = max(distanceWS, 0.0f) * samplingDistanceScale / distanceScale;
                float distanceFade = saturate(1.0f - exp(-distanceRatio * max(intensity, 0.0f) * 4.0f));
                float nearFade = smoothstep(nearFadeStart, nearFadeEnd, distanceWS);

                float opacityGate = saturate(nearFade * maxOpacity);
                float fogAmount = saturate(distanceFade * opacityGate * heightFade);
                float3 rayleighExtinction = max(_BurtAtmosphereRayleighScatteringCoefficient.rgb, 0.0f) * (max(_BurtAtmosphereRayleighIntensity, 0.0f) * 13.6f);
                float3 mieExtinction = (max(_BurtAtmosphereMieScatteringCoefficient.rgb, 0.0f) + max(_BurtAtmosphereMieAbsorptionCoefficient.rgb, 0.0f)) * (max(_BurtAtmosphereMieIntensity, 0.0f) / 0.12f) * 4.8f;
                float3 ozoneExtinction = max(_BurtAtmosphereOzoneAbsorptionCoefficient.rgb, 0.0f) * 10.0f;
                float3 transmittance = exp(-(rayleighExtinction + mieExtinction + ozoneExtinction) * fogAmount);
                float scatterFade = saturate(fogAmount * 1.5f);
                float3 inScatter = EvaluateAerialInscatter(viewDirWS, scatterFade, heightFade) * luminanceScale;
                float3 aerialTint = max(_BurtAtmosphereAerialPerspectiveTint.rgb, 0.0f) * max(_BurtAtmosphereSkyTint.rgb, 0.0f);
                float3 color;
                if (_BurtAtmosphereUseLuts > 0.5f)
                {
                    // The LUT represents only the ray segment after NearFadeStart,
                    // matching XRender's atmosphere fog volume convention.
                    float fogDistanceRatio = max(distanceWS - nearFadeStart, 0.0f)
                        * max(_BurtAtmosphereFogLutDistanceParams.x, 0.000001f)
                        * samplingDistanceScale
                        / max(_BurtAtmosphereFogLutDistanceParams.y, 0.001f);
                    float2 fogScreenUv = input.ScreenUV;
                    #if UNITY_UV_STARTS_AT_TOP
                        fogScreenUv.y = 1.0f - fogScreenUv.y;
                    #endif
                    float4 fogLut = BurtAtmosphereSampleFog(fogScreenUv, fogDistanceRatio);
                    // XRender fades the first half froxel to zero. Without this
                    // intrinsic LUT weight, zero-distance opaque pixels can pick up
                    // the first 3D-LUT texel before the artistic Near Fade begins.
                    const float fogLutDepth = 16.0f;
                    float fogLutNonLinearSlice = saturate(sqrt(max(fogDistanceRatio, 0.0f))) * fogLutDepth;
                    float fogLutStartWeight = saturate(fogLutNonLinearSlice * fogLutNonLinearSlice * 2.0f);
                    float lutWeight = saturate(opacityGate * heightFade * fogLutStartWeight);
                    fogAmount = lutWeight;
                    transmittance = lerp(1.0f.xxx, fogLut.aaa, lutWeight);
                    float3 atmosphereLight = max(_BurtMainLightColorOuterSpace.rgb, 0.0f) * max(_BurtAtmosphereSunIntensity, 0.0f) * saturate(_BurtMainLightOcclusionFactor);
                    // The physical LUT follows XRender's parameter isolation:
                    // density is baked into RGB/alpha, sampling scale selects
                    // distance, and luminance scale alone grades in-scattering.
                    // Aerial Tint remains an explicit BRP sampling-side extension.
                    inScatter = fogLut.rgb * atmosphereLight * max(_BurtAtmosphereAerialPerspectiveTint.rgb, 0.0f) * luminanceScale * lutWeight;
                    // Physical aerial perspective is a direct extinction-plus-inscattering composite.
                    color = sourceColor * transmittance + BurtApplyPreExposure(inScatter);
                }
                else
                {
                    float3 foggedColor = lerp(sourceColor, aerialTint, fogAmount);
                    color = foggedColor * transmittance + BurtApplyPreExposure(inScatter);
                }

                if (_BurtAtmosphereDebugMode > 3.5f && _BurtAtmosphereDebugMode < 4.5f)
                {
                    return float4(saturate(transmittance), 1.0f);
                }

                if (_BurtAtmosphereDebugMode > 4.5f && _BurtAtmosphereDebugMode < 5.5f)
                {
                    return float4(saturate(fogAmount * aerialTint + inScatter), 1.0f);
                }

                if (_BurtAtmosphereDebugMode > 5.5f && _BurtAtmosphereDebugMode < 6.5f)
                {
                    return float4(fogAmount.xxx, 1.0f);
                }

                if (_BurtAtmosphereDebugMode > 6.5f && _BurtAtmosphereDebugMode < 7.5f)
                {
                    return float4(heightFade.xxx, 1.0f);
                }

                if (_BurtAtmosphereDebugMode > 7.5f && _BurtAtmosphereDebugMode < 8.5f)
                {
                    return float4(fogAmount, heightFade, saturate(1.0f - transmittance.b), 1.0f);
                }

                return float4(max(color, 0.0f), 1.0f);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Burt Atmosphere Reflection Cubemap"

            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/BurtAtmosphereLut.hlsl"

            float4 _BurtMainLightDirection;
            float4 _BurtMainLightColor;
            float4 _BurtMainLightColorOuterSpace;
            float _BurtMainLightOcclusionFactor;
            float _BurtAtmosphereRayleighIntensity;
            float _BurtAtmosphereMieIntensity;
            float _BurtAtmosphereMieAnisotropy;
            float4 _BurtAtmosphereRayleighScatteringCoefficient;
            float4 _BurtAtmosphereMieScatteringCoefficient;
            float4 _BurtAtmosphereMieAbsorptionCoefficient;
            float4 _BurtAtmosphereOzoneAbsorptionCoefficient;
            float4 _BurtAtmospherePlanetParams;
            float4 _BurtAtmosphereGroundColor;
            float4 _BurtAtmosphereSkyTint;
            float _BurtAtmosphereSunIntensity;
            float4 _BurtAtmosphereSunDirection;
            float4 _BurtAtmosphereSunParams;
            float4 _BurtAtmosphereHorizonColor;
            float4 _BurtAtmosphereHorizonSunsetColor;
            float4 _BurtAtmosphereHorizonParams;
            float4 _BurtAtmosphereGroundParams;
            float4 _BurtAtmosphereExposureParams;
            float4x4 _BurtAtmosphereWorldToSkyViewLocal;
            float _BurtAtmosphereDebugMode;
            float _BurtAtmosphereCubemapFace;

            static const float PI = 3.14159265359f;

            struct Attributes
            {
                uint VertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 PositionCS : SV_POSITION;
                float2 UV : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.PositionCS = float4((input.VertexID == 2 ? 3.0f : -1.0f), (input.VertexID == 1 ? 3.0f : -1.0f), 0.0f, 1.0f);
                output.UV = float2((input.VertexID == 2 ? 2.0f : 0.0f), (input.VertexID == 1 ? 2.0f : 0.0f));
                return output;
            }

            float3 SafeNormalize(float3 value, float3 fallback)
            {
                float lengthSq = dot(value, value);
                return lengthSq > 1.0e-8f ? value * rsqrt(lengthSq) : fallback;
            }

            float RayleighPhase(float cosTheta)
            {
                return (3.0f / (16.0f * PI)) * (1.0f + cosTheta * cosTheta);
            }

            float MiePhase(float cosTheta, float g)
            {
                float g2 = g * g;
                float denom = max(0.05f, pow(abs(1.0f + g2 - 2.0f * g * cosTheta), 1.5f));
                return (1.0f / (4.0f * PI)) * ((1.0f - g2) / denom);
            }

            float EstimateAirMass(float viewUp, float scaleHeight, float atmosphereHeight)
            {
                float horizonBoost = rcp(max(abs(viewUp) + 0.16f, 0.16f));
                float heightRatio = saturate(atmosphereHeight / max(scaleHeight * 12.0f, 0.001f));
                return saturate(heightRatio * horizonBoost * 0.16f);
            }

            float3 NormalizeLightColor(float3 lightColor)
            {
                float peak = max(max(lightColor.r, lightColor.g), lightColor.b);
                return peak > 0.001f ? lightColor / peak : 1.0f;
            }

            float SmoothRange(float edge0, float edge1, float value)
            {
                float range = edge1 - edge0;
                float safeRange = abs(range) > 1.0e-5f ? range : (range < 0.0f ? -1.0e-5f : 1.0e-5f);
                float t = saturate((value - edge0) / safeRange);
                return t * t * (3.0f - 2.0f * t);
            }

            float3 AtmosphereFaceUVToDirection(float face, float2 uv)
            {
                float2 st = uv * 2.0f - 1.0f;
                st.y = -st.y;
                if (face < 0.5f) return SafeNormalize(float3(1.0f, st.y, -st.x), float3(1.0f, 0.0f, 0.0f));
                if (face < 1.5f) return SafeNormalize(float3(-1.0f, st.y, st.x), float3(-1.0f, 0.0f, 0.0f));
                if (face < 2.5f) return SafeNormalize(float3(st.x, 1.0f, -st.y), float3(0.0f, 1.0f, 0.0f));
                if (face < 3.5f) return SafeNormalize(float3(st.x, -1.0f, st.y), float3(0.0f, -1.0f, 0.0f));
                if (face < 4.5f) return SafeNormalize(float3(st.x, st.y, 1.0f), float3(0.0f, 0.0f, 1.0f));
                return SafeNormalize(float3(-st.x, st.y, -1.0f), float3(0.0f, 0.0f, -1.0f));
            }

            float3 EvaluateAtmosphere(float3 viewDirWS)
            {
                float3 viewDirAtmosphere = mul((float3x3)_BurtAtmosphereWorldToSkyViewLocal, viewDirWS);
                float3 lightDirWS = SafeNormalize(mul((float3x3)_BurtAtmosphereWorldToSkyViewLocal, _BurtAtmosphereSunDirection.xyz), float3(0.0f, 1.0f, 0.0f));
                float3 lightColor = NormalizeLightColor(max(_BurtMainLightColorOuterSpace.rgb, 0.0f));
                float mainLightOcclusion = saturate(_BurtMainLightOcclusionFactor);
                float3 atmosphereLight = max(_BurtMainLightColorOuterSpace.rgb, 0.0f) * max(_BurtAtmosphereSunIntensity, 0.0f) * mainLightOcclusion;
                float cosTheta = dot(viewDirAtmosphere, lightDirWS);
                float viewUp = viewDirAtmosphere.y;
                float up01 = saturate(viewUp * 0.5f + 0.5f);

                float atmosphereHeight = max(_BurtAtmospherePlanetParams.y, 1.0f);
                float rayleighScaleHeight = max(_BurtAtmospherePlanetParams.z, 0.1f);
                float mieScaleHeight = max(_BurtAtmospherePlanetParams.w, 0.1f);
                float rayleighAir = EstimateAirMass(viewUp, rayleighScaleHeight, atmosphereHeight);
                float mieAir = EstimateAirMass(viewUp, mieScaleHeight, atmosphereHeight);

                float3 skyTint = max(_BurtAtmosphereSkyTint.rgb, 0.0f);
                float3 groundColor = max(_BurtAtmosphereGroundColor.rgb, 0.0f);
                float rayleighIntensity = max(_BurtAtmosphereRayleighIntensity, 0.0f);
                float mieIntensity = max(_BurtAtmosphereMieIntensity, 0.0f);
                float3 zenithColor = float3(0.18f, 0.36f, 0.75f) * skyTint;
                float3 horizonBaseColor = max(_BurtAtmosphereHorizonColor.rgb, 0.0f) * skyTint;
                float3 horizonSunsetColor = max(_BurtAtmosphereHorizonSunsetColor.rgb, 0.0f) * lightColor;
                float horizonIntensity = max(_BurtAtmosphereHorizonParams.x, 0.0f);
                float horizonFalloff = max(_BurtAtmosphereHorizonParams.y, 0.1f);
                float horizonSunsetInfluence = saturate(_BurtAtmosphereHorizonParams.z);
                float groundContribution = max(_BurtAtmosphereGroundParams.x, 0.0f);
                float groundBlendStart = _BurtAtmosphereGroundParams.y;
                float groundBlendEnd = _BurtAtmosphereGroundParams.z;
                float3 horizonColor = lerp(horizonBaseColor, horizonSunsetColor, saturate(1.0f - lightDirWS.y) * horizonSunsetInfluence);
                float3 baseSky = lerp(horizonColor * horizonIntensity, zenithColor, pow(up01, horizonFalloff));

                float3 rayleighBeta = max(_BurtAtmosphereRayleighScatteringCoefficient.rgb, 0.0f) * (rayleighIntensity * 13.6f);
                float3 mieBeta = max(_BurtAtmosphereMieScatteringCoefficient.rgb, 0.0f) * (mieIntensity / 0.12f) * 4.8f;
                float3 ozoneExtinction = max(_BurtAtmosphereOzoneAbsorptionCoefficient.rgb, 0.0f) * 10.0f;
                float3 transmittance = exp(-(rayleighBeta * rayleighAir + (mieBeta + max(_BurtAtmosphereMieAbsorptionCoefficient.rgb, 0.0f) * (mieIntensity / 0.12f) * 4.8f) * mieAir + ozoneExtinction * saturate(mieAir * 0.35f)) * 0.45f);
                float3 inScatter = rayleighBeta * RayleighPhase(cosTheta) * rayleighAir;
                inScatter += mieBeta * MiePhase(cosTheta, _BurtAtmosphereMieAnisotropy) * mieAir;

                float exposureScale = max(_BurtAtmosphereExposureParams.x, 0.0f);
                float exposureSafeSun = min(_BurtAtmosphereSunIntensity, max(_BurtAtmosphereExposureParams.y, 0.1f));
                float3 skyColor = baseSky * (0.16f + rayleighIntensity * 0.18f);
                skyColor += inScatter * skyTint * lightColor * exposureSafeSun * mainLightOcclusion;

                float groundBlend = SmoothRange(groundBlendStart, groundBlendEnd, viewUp);
                skyColor = lerp(skyColor, groundColor * groundContribution, groundBlend);
                skyColor *= max(_BurtAtmosphereSkyLuminanceFactor.rgb, 0.0f);
                float3 stylizedSky = BurtAtmosphereEvaluateStylizedSky(
                    viewDirAtmosphere,
                    lightDirWS,
                    _BurtAtmospherePlanetParams,
                    groundColor,
                    _BurtAtmosphereGroundParams.xyz,
                    mainLightOcclusion);
                if (_BurtAtmosphereUseLuts > 0.5f)
                {
                    float3 lutSky = BurtAtmosphereSampleSkyView(viewDirAtmosphere, _BurtAtmospherePlanetParams);
                    // XRender's sky-capture permutation excludes the direct sun disk.
                    // Preserve only the integrated SkyView radiance for IBL; direct solar
                    // lighting already arrives through the main-light path.
                    skyColor = lutSky * atmosphereLight * max(_BurtAtmosphereSkyTint.rgb, 0.0f) * max(_BurtAtmosphereSkyLuminanceFactor.rgb, 0.0f);
                    transmittance = 1.0f;
                }
                skyColor = lerp(skyColor, stylizedSky, saturate(_BurtAtmosphereStylizedParams.x));
                float3 moonDirection = SafeNormalize(
                    mul((float3x3)_BurtAtmosphereWorldToSkyViewLocal, _BurtAtmosphereMoonDirection.xyz),
                    float3(0.0f, 0.0f, -1.0f));
                float3 moonUp = SafeNormalize(
                    mul((float3x3)_BurtAtmosphereWorldToSkyViewLocal, _BurtAtmosphereMoonUp.xyz),
                    float3(0.0f, 1.0f, 0.0f));
                float3 moonRight = SafeNormalize(
                    mul((float3x3)_BurtAtmosphereWorldToSkyViewLocal, _BurtAtmosphereMoonRight.xyz),
                    float3(1.0f, 0.0f, 0.0f));
                float3 moonTransmittance = _BurtAtmosphereUseLuts > 0.5f
                    ? BurtAtmosphereSampleTransmittance(viewUp, _BurtAtmospherePlanetParams)
                    : 1.0f;
                skyColor += BurtAtmosphereEvaluateMoon(
                    viewDirAtmosphere,
                    lightDirWS,
                    moonDirection,
                    moonUp,
                    moonRight,
                    moonTransmittance,
                    _BurtAtmospherePlanetParams);
                return max(skyColor * transmittance * exposureScale, 0.0f);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 viewDirWS = AtmosphereFaceUVToDirection(_BurtAtmosphereCubemapFace, input.UV);
                return float4(EvaluateAtmosphere(viewDirWS), 1.0f);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
