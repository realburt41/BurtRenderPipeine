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

            UNITY_DECLARE_DEPTH_TEXTURE(_BurtCameraDepthTexture);

            float4 _BurtMainLightDirection;
            float4 _BurtMainLightColor;
            float _BurtAtmosphereRayleighIntensity;
            float _BurtAtmosphereMieIntensity;
            float _BurtAtmosphereMieAnisotropy;
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
            float4x4 _BurtAtmosphereInverseViewProjection;
            float3 _BurtAtmosphereCameraPositionWS;
            float _BurtAtmosphereDebugMode;

            static const float PI = 3.14159265359f;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 screenUV : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(uv * 2.0f - 1.0f, 0.0f, 1.0f);
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0f - uv.y;
                #endif
                output.screenUV = uv;
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
                float3 lightDirWS = SafeNormalize(_BurtAtmosphereSunDirection.xyz, SafeNormalize(_BurtMainLightDirection.xyz, float3(0.0f, 1.0f, 0.0f)));
                float3 lightColor = NormalizeLightColor(max(_BurtMainLightColor.rgb, 0.0f));
                float cosTheta = dot(viewDirWS, lightDirWS);
                float viewUp = viewDirWS.y;
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

                float3 rayleighBeta = float3(0.32f, 0.58f, 1.0f) * (rayleighIntensity * 0.45f);
                float3 mieBeta = float3(1.0f, 0.92f, 0.78f) * (mieIntensity * 0.16f);
                float3 transmittance = exp(-(rayleighBeta * rayleighAir + mieBeta * mieAir) * 0.45f);
                float3 inScatter = rayleighBeta * RayleighPhase(cosTheta) * rayleighAir;
                inScatter += mieBeta * MiePhase(cosTheta, _BurtAtmosphereMieAnisotropy) * mieAir;

                float sunDiskSize = max(_BurtAtmosphereSunParams.x, 0.05f);
                float sunDiskIntensity = max(_BurtAtmosphereSunParams.y, 0.0f);
                float sunHaloSize = max(_BurtAtmosphereSunParams.z, 0.05f);
                float sunHaloIntensity = max(_BurtAtmosphereSunParams.w, 0.0f);
                float sunDiskPower = max(4.0f, 384.0f / sunDiskSize);
                float sunHaloPower = max(1.0f, 12.0f / sunHaloSize);
                float sunDisk = pow(saturate(cosTheta), sunDiskPower) * mieIntensity;
                float sunHalo = pow(saturate(cosTheta), sunHaloPower) * mieIntensity * 0.18f;
                float exposureScale = max(_BurtAtmosphereExposureParams.x, 0.0f);
                float exposureSafeSun = min(_BurtAtmosphereSunIntensity, max(_BurtAtmosphereExposureParams.y, 0.1f));
                float3 skyColor = baseSky * (0.16f + rayleighIntensity * 0.18f);
                skyColor += inScatter * skyTint * lightColor * exposureSafeSun;
                skyColor += (sunDisk * sunDiskIntensity + sunHalo * sunHaloIntensity) * lightColor * exposureSafeSun;

                float groundBlend = SmoothRange(groundBlendStart, groundBlendEnd, viewUp);
                skyColor = lerp(skyColor, groundColor * groundContribution, groundBlend);

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
                    return saturate(transmittance);
                }

                if (_BurtAtmosphereDebugMode > 8.5f)
                {
                    if (_BurtAtmosphereDebugMode < 9.5f)
                    {
                        return float3(saturate(rayleighAir), saturate(mieAir), saturate(transmittance.b));
                    }

                    if (_BurtAtmosphereDebugMode < 10.5f)
                    {
                        float diskDebug = sunDisk * sunDiskIntensity;
                        return float3(saturate(diskDebug * 12.0f), saturate(diskDebug * 2.0f), 0.0f);
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

                    return saturate(viewDirWS * 0.5f + 0.5f);
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
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_BurtCameraDepthTexture, input.screenUV);
                if (!IsSkyPixel(rawDepth))
                {
                    discard;
                }

                float4 clip = BuildClipPosition(input.screenUV);
                float4 farWS = mul(_BurtAtmosphereInverseViewProjection, clip);
                farWS.xyz /= max(abs(farWS.w), 1.0e-6f);
                float3 viewDirWS = SafeNormalize(farWS.xyz - _BurtAtmosphereCameraPositionWS, float3(0.0f, 0.0f, 1.0f));
                return float4(EvaluateAtmosphere(viewDirWS), 1.0f);
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

            sampler2D _BurtCameraColorTexture;
            UNITY_DECLARE_DEPTH_TEXTURE(_BurtCameraDepthTexture);

            float4 _BurtMainLightDirection;
            float4 _BurtMainLightColor;
            float _BurtAtmosphereRayleighIntensity;
            float _BurtAtmosphereMieIntensity;
            float _BurtAtmosphereMieAnisotropy;
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
            float4x4 _BurtAtmosphereInverseViewProjection;
            float3 _BurtAtmosphereCameraPositionWS;
            float _BurtAtmosphereDebugMode;

            static const float PI = 3.14159265359f;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 screenUV : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(uv * 2.0f - 1.0f, 0.0f, 1.0f);
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0f - uv.y;
                #endif
                output.screenUV = uv;
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
                float3 lightColor = NormalizeLightColor(max(_BurtMainLightColor.rgb, 0.0f));
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
                scatter *= min(_BurtAtmosphereSunIntensity, max(_BurtAtmosphereExposureParams.y, 0.1f)) * max(_BurtAtmosphereExposureParams.x, 0.0f) * distanceFade * heightFade;
                return max(scatter, 0.0f);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 sourceColor = tex2D(_BurtCameraColorTexture, input.screenUV).rgb;
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_BurtCameraDepthTexture, input.screenUV);
                float intensity = _BurtAtmosphereAerialPerspectiveParams.x;
                float distanceScale = max(_BurtAtmosphereAerialPerspectiveParams.y, 1.0f);
                float heightFalloff = _BurtAtmosphereAerialPerspectiveParams.z;
                float nearFadeStart = _BurtAtmosphereAerialPerspectiveFadeParams.x;
                float nearFadeEnd = max(_BurtAtmosphereAerialPerspectiveFadeParams.y, nearFadeStart + 0.001f);
                float maxOpacity = saturate(_BurtAtmosphereAerialPerspectiveFadeParams.z);
                float affectsSkyPixels = _BurtAtmosphereAerialPerspectiveFadeParams.w;
                bool skyPixel = IsSkyPixel(rawDepth);
                bool aerialDebug = _BurtAtmosphereDebugMode > 3.5f && _BurtAtmosphereDebugMode < 8.5f;
                if ((!aerialDebug && _BurtAtmosphereDebugMode > 0.5f) || (skyPixel && affectsSkyPixels < 0.5f) || _BurtAtmosphereAerialPerspectiveParams.w < 0.5f)
                {
                    return float4(sourceColor, 1.0f);
                }

                float distanceWS;
                float heightFade;
                float3 viewDirWS;
                if (skyPixel)
                {
                    float4 farWS = mul(_BurtAtmosphereInverseViewProjection, BuildClipPosition(input.screenUV, rawDepth));
                    farWS.xyz /= max(abs(farWS.w), 1.0e-6f);
                    viewDirWS = SafeNormalize(farWS.xyz - _BurtAtmosphereCameraPositionWS, float3(0.0f, 0.0f, 1.0f));
                    distanceWS = distanceScale * 4.0f;
                    heightFade = saturate(0.35f + 0.65f * (1.0f - max(viewDirWS.y, 0.0f)));
                }
                else
                {
                    float3 positionWS = ReconstructPositionWS(input.screenUV, rawDepth);
                    float3 cameraToPixel = positionWS - _BurtAtmosphereCameraPositionWS;
                    distanceWS = length(cameraToPixel);
                    viewDirWS = SafeNormalize(cameraToPixel, float3(0.0f, 0.0f, 1.0f));
                    heightFade = exp(-max(positionWS.y - _BurtAtmosphereCameraPositionWS.y, 0.0f) * heightFalloff);
                }

                float distanceRatio = max(distanceWS, 0.0f) / distanceScale;
                float distanceFade = saturate(1.0f - exp(-distanceRatio * max(intensity, 0.0f) * 4.0f));
                float nearFade = smoothstep(nearFadeStart, nearFadeEnd, distanceWS);

                float opacityGate = saturate(nearFade * maxOpacity);
                float fogAmount = saturate(distanceFade * opacityGate * heightFade);
                float3 rayleighExtinction = float3(0.32f, 0.58f, 1.0f) * (max(_BurtAtmosphereRayleighIntensity, 0.0f) * 0.45f);
                float3 mieExtinction = float3(1.0f, 0.92f, 0.78f) * (max(_BurtAtmosphereMieIntensity, 0.0f) * 0.18f);
                float3 transmittance = exp(-(rayleighExtinction + mieExtinction) * fogAmount);
                float scatterFade = saturate(fogAmount * 1.5f);
                float3 inScatter = EvaluateAerialInscatter(viewDirWS, scatterFade, heightFade);
                float3 aerialTint = max(_BurtAtmosphereAerialPerspectiveTint.rgb, 0.0f) * max(_BurtAtmosphereSkyTint.rgb, 0.0f);
                float3 foggedColor = lerp(sourceColor, aerialTint, fogAmount);
                float3 color = foggedColor * transmittance + inScatter;

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
    }

    Fallback Off
}
