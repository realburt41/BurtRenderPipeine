Shader "Hidden/BurtRP/AtmosphereScattering"
{
    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" "RenderType" = "Opaque" }
        LOD 100

        Pass
        {
            Name "Burt Atmosphere Scattering"

            Cull Off
            ZWrite Off
            ZTest LEqual
            // Exact XRender PhysicalSky blend: add the sky term to RGB using
            // source alpha as the destination factor, while preserving target alpha.
            Blend One SrcAlpha, Zero One

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _ATMOSPHERE_COMBINE_IS_SKY_CAPTURE
            #pragma multi_compile _ _PHYSICAL_SKY_IS_NIGHT

            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtPreExposure.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/BurtAtmosphereLut.hlsl"

            // UNITY_RAW_FAR_CLIP_VALUE belongs to SRP Core's API headers and is
            // not provided by UnityCG.cginc in stock Unity 2022. Preserve the
            // same platform convention without pulling the full Core include
            // stack into this otherwise UnityCG-based pass.
            #ifndef UNITY_RAW_FAR_CLIP_VALUE
                #if defined(UNITY_REVERSED_Z)
                    #define UNITY_RAW_FAR_CLIP_VALUE 0.0f
                #else
                    #define UNITY_RAW_FAR_CLIP_VALUE 1.0f
                #endif
            #endif

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
            float4 _BurtAtmosphereAerialPerspectiveFadeParams; // x=XRender start depth, y=optional smooth-start end, z=max opacity, w=affects sky
            float4 _BurtAtmosphereFogLutDistanceParams;
            float4x4 _BurtAtmosphereInverseViewProjection;
            float4x4 _BurtAtmosphereSkyMeshViewProjection;
            float _BurtAtmosphereProceduralSky;
            float3 _BurtAtmosphereCameraPositionWS;
            float4x4 _BurtAtmosphereWorldToSkyViewLocal;
            float _BurtAtmosphereDebugMode;

            static const float PI = 3.14159265359f;

            struct Attributes
            {
                float3 PositionOS : POSITION;
                float2 MeshUv0 : TEXCOORD0;
                float2 MeshUv1 : TEXCOORD1;
                uint VertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 PositionCS : SV_POSITION;
                float3 PositionWS : TEXCOORD0;
                float2 MeshUv0 : TEXCOORD1;
                float2 MeshUv1 : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                if (_BurtAtmosphereProceduralSky > 0.5f)
                {
                    float2 triangleUv = float2(
                        (input.VertexID << 1) & 2,
                        input.VertexID & 2);
                    output.PositionCS = float4(
                        triangleUv * 2.0f - 1.0f,
                        UNITY_RAW_FAR_CLIP_VALUE,
                        1.0f);
                    float4 farPositionWS = mul(
                        _BurtAtmosphereInverseViewProjection,
                        output.PositionCS);
                    farPositionWS.xyz /= max(abs(farPositionWS.w), 1.0e-6f);
                    float3 cameraToFar = farPositionWS.xyz - _BurtAtmosphereCameraPositionWS;
                    cameraToFar *= rsqrt(max(dot(cameraToFar, cameraToFar), 1.0e-8f));
                    // XRender's authored default sky mesh is a 19930-unit sky
                    // shell. Preserve that world-position scale so its star
                    // horizon formula remains valid in the procedural fallback.
                    output.PositionWS = _BurtAtmosphereCameraPositionWS
                        + cameraToFar * 19930.0f;
                    // DrawProcedural supplies no mesh vertex streams in
                    // XRender, so the fallback intentionally starts at zero UV.
                    output.MeshUv0 = 0.0f;
                    output.MeshUv1 = 0.0f;
                    return output;
                }

                output.PositionWS = mul(unity_ObjectToWorld, float4(input.PositionOS, 1.0f)).xyz;
                output.PositionCS = mul(
                    _BurtAtmosphereSkyMeshViewProjection,
                    float4(output.PositionWS, 1.0f));
                output.PositionCS.z = UNITY_RAW_FAR_CLIP_VALUE * output.PositionCS.w;
                output.MeshUv0 = input.MeshUv0;
                output.MeshUv1 = input.MeshUv1;
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
                return BurtAtmosphereHenyeyGreensteinPhase(cosTheta, g);
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

            float3 EvaluateAtmosphere(
                float3 viewDirWS,
                float3 positionWS,
                float2 meshUv0,
                float2 meshUv1)
            {
                float3 viewDirAtmosphere = mul((float3x3)_BurtAtmosphereWorldToSkyViewLocal, viewDirWS);
                float3 lightDirWS = SafeNormalize(mul((float3x3)_BurtAtmosphereWorldToSkyViewLocal, _BurtAtmosphereSunDirection.xyz), float3(0.0f, 1.0f, 0.0f));
                float3 lightColor = NormalizeLightColor(max(_BurtMainLightColorOuterSpace.rgb, 0.0f));
                float mainLightOcclusion = saturate(_BurtMainLightOcclusionFactor);
                // XRender's AtmosphereCommon applies outer-space main-light
                // illuminance and occlusion to SkyView. The legacy BRP
                // SunIntensity/SkyTint controls belong to the analytic fallback
                // and direct celestial terms, not the physical LUT.
                float3 atmosphereLight = _BurtMainLightColorOuterSpace.rgb;
                float cosTheta = dot(viewDirAtmosphere, lightDirWS);
                // PhysicalSky.hlsl evaluates its strict disk boundary directly
                // in world space. Keep this independent from the SkyView basis
                // transform so the edge follows the source comparison exactly.
                float physicalSkyViewDotLight = dot(
                    viewDirWS,
                    SafeNormalize(_BurtAtmosphereSunDirection.xyz, float3(0.0f, 1.0f, 0.0f)));
                float viewUp = viewDirAtmosphere.y;
                float up01 = saturate(viewUp * 0.5f + 0.5f);

                float atmosphereHeight = max(_BurtAtmospherePlanetParams.y, 0.1f);
                float rayleighScaleHeight = max(_BurtAtmospherePlanetParams.z, 0.001f);
                float mieScaleHeight = max(_BurtAtmospherePlanetParams.w, 0.001f);
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

                float3 rayleighBeta = max(_BurtAtmosphereRayleighScatteringCoefficient.rgb, 0.0f) * 13.6f;
                float3 mieBeta = max(_BurtAtmosphereMieScatteringCoefficient.rgb, 0.0f) * 4.8f;
                float3 ozoneExtinction = max(_BurtAtmosphereOzoneAbsorptionCoefficient.rgb, 0.0f) * 10.0f;
                float3 transmittance = exp(-(rayleighBeta * rayleighAir + (mieBeta + max(_BurtAtmosphereMieAbsorptionCoefficient.rgb, 0.0f) * 4.8f) * mieAir + ozoneExtinction * saturate(mieAir * 0.35f)) * 0.45f);
                float3 inScatter = rayleighBeta * RayleighPhase(cosTheta) * rayleighAir;
                inScatter += mieBeta * MiePhase(cosTheta, _BurtAtmosphereMieAnisotropy) * mieAir;

                float sunHaloSize = max(_BurtAtmosphereSunParams.z, 0.05f);
                float sunHaloIntensity = max(_BurtAtmosphereSunParams.w, 0.0f);
                float sunHaloPower = max(1.0f, 12.0f / sunHaloSize);
                // The package AtmosphereCommon analytic fallback softens the
                // outer half of the solid-angle-normalized disk. The project's
                // active PhysicalSky override uses physicalSunDiskMask below.
                float sunDiskCosHalfApex = saturate(_BurtAtmosphereSunDiskLuminanceAndCosHalfApex.w);
                float sunDiskCosRange = max(1.0f - sunDiskCosHalfApex, 1.0e-7f);
                float sunDisk = cosTheta > sunDiskCosHalfApex
                    ? saturate(2.0f * (cosTheta - sunDiskCosHalfApex) / sunDiskCosRange)
                    : 0.0f;
                float sunHalo = pow(saturate(cosTheta), sunHaloPower) * mieIntensity * 0.18f;
                // Preserve package AtmosphereCommon planet rejection for the
                // analytic fallback. PhysicalSky's active LUT path deliberately
                // bypasses this value.
                float planetVisibility = BurtAtmospherePlanetVisibility(viewDirAtmosphere, _BurtAtmospherePlanetParams);
                sunDisk *= planetVisibility;
                sunHalo *= planetVisibility;
                float exposureScale = max(_BurtAtmosphereExposureParams.x, 0.0f);
                float exposureSafeSun = min(_BurtAtmosphereSunIntensity, max(_BurtAtmosphereExposureParams.y, 0.1f));
                float3 sunDiskContribution = min(
                    sunDisk
                        * max(_BurtAtmosphereSunDiskLuminanceAndCosHalfApex.rgb, 0.0f)
                        * max(_BurtAtmosphereSunIntensity, 0.0f)
                        * max(_BurtAtmosphereSunParams.y, 0.0f),
                    64000.0f) * mainLightOcclusion;
                sunDiskContribution *= BurtAtmosphereWeatherSunVisibility();
                float3 sunHaloContribution = sunHalo * sunHaloIntensity
                    * max(_BurtAtmosphereStylizedSunDiskColorScale.rgb, 0.0f)
                    * lightColor * exposureSafeSun * mainLightOcclusion;
                float3 physicalSkyBackground = baseSky * (0.16f + rayleighIntensity * 0.18f);
                physicalSkyBackground += inScatter * skyTint * lightColor * exposureSafeSun * mainLightOcclusion;
                float3 directSunContribution = sunDiskContribution + sunHaloContribution;
                // The project's PhysicalSky override uses a constant-luminance
                // hard disk. It deliberately omits atmosphere transmittance,
                // planet testing, limb darkening and main-light occlusion.
                float3 physicalSunDiskContribution =
                    BurtAtmosphereEvaluatePhysicalSkySunDisk(
                        physicalSkyViewDotLight,
                        _BurtAtmosphereSunDiskLuminanceAndCosHalfApex,
                        _BurtInvPreExposure);

                float groundBlend = SmoothRange(groundBlendStart, groundBlendEnd, viewUp);
                physicalSkyBackground = lerp(physicalSkyBackground, groundColor * groundContribution, groundBlend);
                // XRender grades integrated sky scattering independently from the
                // direct solar term. Preserve the analytic path's previous ground
                // attenuation while applying the RGB factor only to the background.
                float3 skyColor = physicalSkyBackground
                    * max(_BurtAtmosphereSkyLuminanceFactor.rgb, 0.0f);
                float3 skyDirectSunContribution = directSunContribution * (1.0f - groundBlend);
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
                    // PhysicalSky compiles this term out of sky capture, while
                    // the main-camera combine adds it without transmittance.
                    directSunContribution = physicalSunDiskContribution;
                    skyDirectSunContribution = directSunContribution;
                    skyColor = lutSky
                        * atmosphereLight
                        * _BurtAtmosphereSkyLuminanceFactor.rgb
                        * mainLightOcclusion;
                    transmittance = 1.0f;
                    // XRender's project PhysicalSky has no BRP exposure
                    // compensation control. It performs only frame
                    // pre-exposure after composing the LUT sky and hard disk.
                    exposureScale = 1.0f;
                }
                else
                {
                    // The project's active PhysicalSky.hlsl receives XRender's
                    // stylized constants but never references them. Retain BRP's
                    // historical authored-sky blend only for the analytic fallback.
                    float3 stylizedSky = BurtAtmosphereEvaluateStylizedSky(
                        viewDirAtmosphere,
                        lightDirWS,
                        _BurtAtmospherePlanetParams,
                        groundColor,
                        _BurtAtmosphereGroundParams.xyz,
                        mainLightOcclusion);
                    float stylizedSkyBlend = saturate(_BurtAtmosphereStylizedParams.x);
                    skyColor = lerp(skyColor, stylizedSky, stylizedSkyBlend);
                    skyDirectSunContribution = lerp(
                        skyDirectSunContribution,
                        directSunContribution,
                        stylizedSkyBlend);
                }

                skyColor += BurtAtmosphereEvaluateWeatherSkyClouds(
                    viewDirAtmosphere,
                    lightDirWS,
                    meshUv0);
                // XRender desaturates only the atmosphere + weather-cloud term
                // here. Sun, moon, stars and panoramic clouds remain separate.
                skyColor = BurtAtmosphereApplyPhysicalSkyDesaturation(skyColor);
                #if defined(_ATMOSPHERE_COMBINE_IS_SKY_CAPTURE)
                skyDirectSunContribution = 0.0f;
                #endif
                skyColor += skyDirectSunContribution;
                // XRender compiles both expensive celestial paths only in its
                // _PHYSICAL_SKY_IS_NIGHT permutation (_TodCurve > 0.5).
                #if defined(_PHYSICAL_SKY_IS_NIGHT) && !defined(_ATMOSPHERE_COMBINE_IS_SKY_CAPTURE)
                    skyColor += BurtAtmosphereEvaluateMoon(
                        viewDirWS,
                        _BurtAtmosphereSunDirection.xyz,
                        _BurtAtmosphereMoonUp.xyz,
                        _BurtAtmosphereMoonRight.xyz,
                        meshUv0);
                    // The sky-capture pass below deliberately has no equivalent
                    // moon/star calls, matching _ATMOSPHERE_COMBINE_IS_SKY_CAPTURE.
                    skyColor += BurtAtmosphereEvaluateStars(
                        viewDirWS,
                        positionWS,
                        _BurtAtmosphereMoonUp.xyz,
                        _BurtAtmosphereMoonRight.xyz,
                        meshUv0);
                #endif
                skyColor += BurtAtmosphereEvaluatePanoramicClouds(
                    mainLightOcclusion,
                    meshUv1);

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
                        float diskDebug = _BurtAtmosphereUseLuts > 0.5f
                            ? BurtAtmospherePhysicalSkySunDiskVisibility(
                                physicalSkyViewDotLight,
                                sunDiskCosHalfApex)
                            : sunDisk;
                        return saturate(diskDebug.xxx * 12.0f);
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
                            ? max(
                                lutSky
                                    * atmosphereLight
                                    * _BurtAtmosphereSkyLuminanceFactor.rgb
                                    * mainLightOcclusion,
                                0.0f)
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

                return skyColor * transmittance * exposureScale;
            }

            bool IsSkyPixel(float rawDepth)
            {
                #if defined(UNITY_REVERSED_Z)
                    // Match XRender's DeviceDepth == FarDepthValue contract exactly.
                    return rawDepth == 0.0f;
                #else
                    return rawDepth == 1.0f;
                #endif
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.PositionCS.xy / _ScreenParams.xy;
                #if defined(_ATMOSPHERE_COMBINE_IS_SKY_CAPTURE)
                    #if defined(UNITY_REVERSED_Z)
                    float rawDepth = 0.0f;
                    #else
                    float rawDepth = 1.0f;
                    #endif
                #else
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_BurtCameraDepthTexture, screenUV);
                #endif
                if (!IsSkyPixel(rawDepth))
                {
                    // XRender returns black with alpha one. Its RGB destination
                    // factor is SrcAlpha, so existing scene color is preserved.
                    return float4(0.0f, 0.0f, 0.0f, 1.0f);
                }

                float3 viewDirWS = SafeNormalize(
                    input.PositionWS - _BurtAtmosphereCameraPositionWS,
                    float3(0.0f, 0.0f, 1.0f));
                float3 skyLuminance = EvaluateAtmosphere(
                    viewDirWS,
                    input.PositionWS,
                    input.MeshUv0,
                    input.MeshUv1);
                #if defined(_ATMOSPHERE_COMBINE_IS_SKY_CAPTURE)
                return float4(skyLuminance, 1.0f);
                #else
                return float4(
                    BurtApplyPreExposure(skyLuminance),
                    1.0f);
                #endif
            }
            ENDHLSL
        }

        Pass
        {
            Name "Burt Atmosphere Aerial Perspective"

            Cull Off
            ZWrite Off
            ZTest Always
            // Preserve the source camera alpha already present in the destination.
            ColorMask RGB

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtPreExposure.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/BurtAtmosphereLut.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLightShaftOcclusion.hlsl"

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
            float4 _BurtAtmosphereAerialPerspectiveFadeParams; // x=XRender start depth, y=optional smooth-start end, z=max opacity, w=affects sky
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
                return BurtAtmosphereHenyeyGreensteinPhase(cosTheta, g);
            }

            float3 NormalizeLightColor(float3 lightColor)
            {
                float peak = max(max(lightColor.r, lightColor.g), lightColor.b);
                return peak > 0.001f ? lightColor / peak : 1.0f;
            }

            bool IsSkyPixel(float rawDepth)
            {
                #if defined(UNITY_REVERSED_Z)
                    return rawDepth == 0.0f;
                #else
                    return rawDepth == 1.0f;
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
                float startDepth = _BurtAtmosphereAerialPerspectiveFadeParams.x;
                float smoothStartEnd = max(_BurtAtmosphereAerialPerspectiveFadeParams.y, startDepth + 0.001f);
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
                    || _BurtAtmosphereAerialPerspectiveParams.w < 0.5f
                    // AEvaluateAtmosphereFog treats a zero luminance scale as
                    // disabling both scattering and extinction.
                    || (_BurtAtmosphereUseLuts > 0.5f && luminanceScale <= 0.0f))
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
                float nearFade = smoothstep(startDepth, smoothStartEnd, distanceWS);

                float opacityGate = saturate(nearFade * maxOpacity);
                float fogAmount = saturate(distanceFade * opacityGate * heightFade);
                float3 rayleighExtinction = max(_BurtAtmosphereRayleighScatteringCoefficient.rgb, 0.0f) * 13.6f;
                float3 mieExtinction = (max(_BurtAtmosphereMieScatteringCoefficient.rgb, 0.0f) + max(_BurtAtmosphereMieAbsorptionCoefficient.rgb, 0.0f)) * 4.8f;
                float3 ozoneExtinction = max(_BurtAtmosphereOzoneAbsorptionCoefficient.rgb, 0.0f) * 10.0f;
                float3 transmittance = exp(-(rayleighExtinction + mieExtinction + ozoneExtinction) * fogAmount);
                float scatterFade = saturate(fogAmount * 1.5f);
                float3 inScatter = EvaluateAerialInscatter(viewDirWS, scatterFade, heightFade) * luminanceScale;
                float3 aerialTint = max(_BurtAtmosphereAerialPerspectiveTint.rgb, 0.0f) * max(_BurtAtmosphereSkyTint.rgb, 0.0f);
                float lightShaftOcclusion = BurtSampleLightShaftOcclusion(input.ScreenUV);
                float3 color;
                if (_BurtAtmosphereUseLuts > 0.5f)
                {
                    // The LUT represents only the ray segment after XRender's
                    // Atmosphere Fog Start Distance.
                    float worldToKilometers = max(_BurtAtmosphereFogLutDistanceParams.x, 0.000001f);
                    float fogDistanceKm = distanceWS * worldToKilometers;
                    float startDepthKm = startDepth * worldToKilometers;
                    float fogDistanceRatio = max(fogDistanceKm - startDepthKm, 0.0f)
                        * samplingDistanceScale
                        / max(_BurtAtmosphereFogLutDistanceParams.y, 0.001f);
                    float2 fogScreenUv = input.ScreenUV;
                    #if UNITY_UV_STARTS_AT_TOP
                        fogScreenUv.y = 1.0f - fogScreenUv.y;
                    #endif
                    float4 fogLut = BurtAtmosphereSampleFog(fogScreenUv, fogDistanceRatio);
                    // XRender fades the first half froxel to zero. Without this
                    // intrinsic LUT weight, zero-distance opaque pixels can pick up
                    // the first 3D-LUT texel before Start Depth.
                    float fogLutStartWeight = BurtAtmosphereFogStartWeight(fogDistanceRatio);
                    // XRender's physical consumer applies only the intrinsic
                    // first-froxel fade. Smooth start, opacity cap and height
                    // fade belong to BRP's analytic compatibility path.
                    float lutWeight = fogLutStartWeight;
                    fogAmount = lutWeight;
                    transmittance = lerp(1.0f.xxx, fogLut.aaa, lutWeight);
                    float3 atmosphereLight = max(_BurtMainLightColorOuterSpace.rgb, 0.0f)
                        * saturate(_BurtMainLightOcclusionFactor);
                    // Fog RGB is integrated for unit illuminance. XRender adds
                    // the outer-space main light, environment occlusion and
                    // fog luminance scale exactly once at lookup time.
                    inScatter = fogLut.rgb * atmosphereLight * luminanceScale * lutWeight;
                    // Physical aerial perspective is a direct extinction-plus-inscattering composite.
                    color = sourceColor * transmittance
                        + BurtApplyPreExposure(inScatter) * lightShaftOcclusion;
                    // The intrinsic first-froxel weight is only the LUT ramp-in.
                    // The final physical fog opacity is extinction derived from
                    // the sampled transmittance, which is what the Fog Amount
                    // and Summary debug views promise to visualize.
                    fogAmount = saturate(1.0f - dot(transmittance, (1.0f / 3.0f).xxx));
                }
                else
                {
                    float3 sourceContribution =
                        sourceColor * (1.0f - fogAmount) * transmittance;
                    float3 scatteringContribution =
                        aerialTint * fogAmount * transmittance
                        + BurtApplyPreExposure(inScatter);
                    color = sourceContribution
                        + scatteringContribution * lightShaftOcclusion;
                }

                if (_BurtAtmosphereDebugMode > 3.5f && _BurtAtmosphereDebugMode < 4.5f)
                {
                    return float4(saturate(transmittance), 1.0f);
                }

                if (_BurtAtmosphereDebugMode > 4.5f && _BurtAtmosphereDebugMode < 5.5f)
                {
                    float3 debugInscatter = _BurtAtmosphereUseLuts > 0.5f
                        ? inScatter
                        : fogAmount * aerialTint + inScatter;
                    return float4(saturate(debugInscatter), 1.0f);
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
            ZTest LEqual
            Blend One SrcAlpha, Zero One

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _ATMOSPHERE_COMBINE_IS_SKY_CAPTURE
            #pragma multi_compile _ _PHYSICAL_SKY_IS_NIGHT

            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/BurtAtmosphereLut.hlsl"

            #ifndef UNITY_RAW_FAR_CLIP_VALUE
                #if defined(UNITY_REVERSED_Z)
                    #define UNITY_RAW_FAR_CLIP_VALUE 0.0f
                #else
                    #define UNITY_RAW_FAR_CLIP_VALUE 1.0f
                #endif
            #endif

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
            float4x4 _BurtAtmosphereInverseViewProjection;
            float4x4 _BurtAtmosphereSkyMeshViewProjection;
            float _BurtAtmosphereProceduralSky;
            float3 _BurtAtmosphereCameraPositionWS;
            float _BurtAtmosphereDebugMode;

            static const float PI = 3.14159265359f;

            struct Attributes
            {
                float3 PositionOS : POSITION;
                float2 MeshUv0 : TEXCOORD0;
                float2 MeshUv1 : TEXCOORD1;
                uint VertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 PositionCS : SV_POSITION;
                float3 PositionWS : TEXCOORD0;
                float2 MeshUv0 : TEXCOORD1;
                float2 MeshUv1 : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                if (_BurtAtmosphereProceduralSky > 0.5f)
                {
                    float2 triangleUv = float2(
                        (input.VertexID << 1) & 2,
                        input.VertexID & 2);
                    output.PositionCS = float4(
                        triangleUv * 2.0f - 1.0f,
                        UNITY_RAW_FAR_CLIP_VALUE,
                        1.0f);
                    float4 farPositionWS = mul(
                        _BurtAtmosphereInverseViewProjection,
                        output.PositionCS);
                    farPositionWS.xyz /= max(abs(farPositionWS.w), 1.0e-6f);
                    float3 cameraToFar = farPositionWS.xyz - _BurtAtmosphereCameraPositionWS;
                    cameraToFar *= rsqrt(max(dot(cameraToFar, cameraToFar), 1.0e-8f));
                    output.PositionWS = _BurtAtmosphereCameraPositionWS
                        + cameraToFar * 19930.0f;
                    output.MeshUv0 = 0.0f;
                    output.MeshUv1 = 0.0f;
                    return output;
                }

                output.PositionWS = mul(unity_ObjectToWorld, float4(input.PositionOS, 1.0f)).xyz;
                output.PositionCS = mul(
                    _BurtAtmosphereSkyMeshViewProjection,
                    float4(output.PositionWS, 1.0f));
                output.PositionCS.z = UNITY_RAW_FAR_CLIP_VALUE * output.PositionCS.w;
                output.MeshUv0 = input.MeshUv0;
                output.MeshUv1 = input.MeshUv1;
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
                return BurtAtmosphereHenyeyGreensteinPhase(cosTheta, g);
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

            float3 EvaluateAtmosphere(
                float3 viewDirWS,
                float2 meshUv0,
                float2 meshUv1)
            {
                float3 viewDirAtmosphere = mul((float3x3)_BurtAtmosphereWorldToSkyViewLocal, viewDirWS);
                float3 lightDirWS = SafeNormalize(mul((float3x3)_BurtAtmosphereWorldToSkyViewLocal, _BurtAtmosphereSunDirection.xyz), float3(0.0f, 1.0f, 0.0f));
                float3 lightColor = NormalizeLightColor(max(_BurtMainLightColorOuterSpace.rgb, 0.0f));
                float mainLightOcclusion = saturate(_BurtMainLightOcclusionFactor);
                float3 atmosphereLight = _BurtMainLightColorOuterSpace.rgb;
                float cosTheta = dot(viewDirAtmosphere, lightDirWS);
                float viewUp = viewDirAtmosphere.y;
                float up01 = saturate(viewUp * 0.5f + 0.5f);

                float atmosphereHeight = max(_BurtAtmospherePlanetParams.y, 0.1f);
                float rayleighScaleHeight = max(_BurtAtmospherePlanetParams.z, 0.001f);
                float mieScaleHeight = max(_BurtAtmospherePlanetParams.w, 0.001f);
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

                float3 rayleighBeta = max(_BurtAtmosphereRayleighScatteringCoefficient.rgb, 0.0f) * 13.6f;
                float3 mieBeta = max(_BurtAtmosphereMieScatteringCoefficient.rgb, 0.0f) * 4.8f;
                float3 ozoneExtinction = max(_BurtAtmosphereOzoneAbsorptionCoefficient.rgb, 0.0f) * 10.0f;
                float3 transmittance = exp(-(rayleighBeta * rayleighAir + (mieBeta + max(_BurtAtmosphereMieAbsorptionCoefficient.rgb, 0.0f) * 4.8f) * mieAir + ozoneExtinction * saturate(mieAir * 0.35f)) * 0.45f);
                float3 inScatter = rayleighBeta * RayleighPhase(cosTheta) * rayleighAir;
                inScatter += mieBeta * MiePhase(cosTheta, _BurtAtmosphereMieAnisotropy) * mieAir;

                float exposureScale = max(_BurtAtmosphereExposureParams.x, 0.0f);
                float exposureSafeSun = min(_BurtAtmosphereSunIntensity, max(_BurtAtmosphereExposureParams.y, 0.1f));
                float3 skyColor = baseSky * (0.16f + rayleighIntensity * 0.18f);
                skyColor += inScatter * skyTint * lightColor * exposureSafeSun * mainLightOcclusion;

                float groundBlend = SmoothRange(groundBlendStart, groundBlendEnd, viewUp);
                skyColor = lerp(skyColor, groundColor * groundContribution, groundBlend);
                skyColor *= max(_BurtAtmosphereSkyLuminanceFactor.rgb, 0.0f);
                if (_BurtAtmosphereUseLuts > 0.5f)
                {
                    float3 lutSky = BurtAtmosphereSampleSkyView(viewDirAtmosphere, _BurtAtmospherePlanetParams);
                    // XRender's sky-capture permutation excludes the direct sun disk.
                    // Preserve only the integrated SkyView radiance for IBL; direct solar
                    // lighting already arrives through the main-light path.
                    skyColor = lutSky
                        * atmosphereLight
                        * _BurtAtmosphereSkyLuminanceFactor.rgb
                        * mainLightOcclusion;
                    transmittance = 1.0f;
                }
                else
                {
                    // XRender's project PhysicalSky capture does not consume its
                    // uploaded stylized-sky fields. This remains an analytic-only
                    // compatibility control in BRP.
                    float3 stylizedSky = BurtAtmosphereEvaluateStylizedSky(
                        viewDirAtmosphere,
                        lightDirWS,
                        _BurtAtmospherePlanetParams,
                        groundColor,
                        _BurtAtmosphereGroundParams.xyz,
                        mainLightOcclusion);
                    skyColor = lerp(
                        skyColor,
                        stylizedSky,
                        saturate(_BurtAtmosphereStylizedParams.x));
                }
                skyColor += BurtAtmosphereEvaluateWeatherSkyClouds(
                    viewDirAtmosphere,
                    lightDirWS,
                    meshUv0);
                skyColor = BurtAtmosphereApplyPhysicalSkyDesaturation(skyColor);
                // XRender's sky-capture permutation compiles out the sun disk,
                // moon and stars. Only integrated sky, weather clouds and
                // panoramic clouds are allowed to feed diffuse/specular IBL.
                skyColor += BurtAtmosphereEvaluatePanoramicClouds(
                    mainLightOcclusion,
                    meshUv1);
                // This pass never applies Burt pre-exposure. ARGBHalf therefore
                // stores the same scene-linear domain produced by XRender after
                // its explicit GetOneOverPreExposureValue cancellation.
                return skyColor * transmittance * exposureScale;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 viewDirWS = SafeNormalize(
                    input.PositionWS - _BurtAtmosphereCameraPositionWS,
                    float3(0.0f, 0.0f, 1.0f));
                return float4(
                    EvaluateAtmosphere(
                        viewDirWS,
                        input.MeshUv0,
                        input.MeshUv1),
                    1.0f);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
