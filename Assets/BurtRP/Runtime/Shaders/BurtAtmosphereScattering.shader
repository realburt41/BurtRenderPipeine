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
            float4 _BurtAtmosphereHorizonParams;
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
                float rayleighIntensity = saturate(_BurtAtmosphereRayleighIntensity);
                float mieIntensity = saturate(_BurtAtmosphereMieIntensity);
                float3 zenithColor = float3(0.18f, 0.36f, 0.75f) * skyTint;
                float3 horizonColor = lerp(float3(0.48f, 0.66f, 0.92f) * skyTint, float3(0.95f, 0.82f, 0.58f) * lightColor, saturate(1.0f - lightDirWS.y) * 0.35f);
                float horizonIntensity = max(_BurtAtmosphereHorizonParams.x, 0.0f);
                float horizonFalloff = max(_BurtAtmosphereHorizonParams.y, 0.1f);
                float groundContribution = max(_BurtAtmosphereHorizonParams.z, 0.0f);
                float3 baseSky = lerp(horizonColor * horizonIntensity, zenithColor, pow(up01, horizonFalloff));

                float3 rayleighBeta = float3(0.32f, 0.58f, 1.0f) * (rayleighIntensity * 0.45f);
                float3 mieBeta = float3(1.0f, 0.92f, 0.78f) * (mieIntensity * 0.16f);
                float3 transmittance = exp(-(rayleighBeta * rayleighAir + mieBeta * mieAir) * 0.45f);
                float3 inScatter = rayleighBeta * RayleighPhase(cosTheta) * rayleighAir;
                inScatter += mieBeta * MiePhase(cosTheta, _BurtAtmosphereMieAnisotropy) * mieAir;

                float sunDisk = pow(saturate(cosTheta), 384.0f) * mieIntensity;
                float sunHalo = pow(saturate(cosTheta), 12.0f) * mieIntensity * 0.18f;
                float exposureScale = max(_BurtAtmosphereExposureParams.x, 0.0f);
                float exposureSafeSun = min(_BurtAtmosphereSunIntensity, max(_BurtAtmosphereExposureParams.y, 0.1f));
                float3 skyColor = baseSky * (0.16f + rayleighIntensity * 0.18f);
                skyColor += inScatter * skyTint * lightColor * exposureSafeSun;
                skyColor += (sunDisk * 1.2f + sunHalo) * lightColor * exposureSafeSun;

                float groundBlend = smoothstep(-0.02f, -0.20f, viewUp);
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

                if (_BurtAtmosphereDebugMode > 5.5f)
                {
                    return float3(saturate(rayleighAir), saturate(mieAir), saturate(transmittance.b));
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
            float4 _BurtAtmosphereHorizonParams;
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
                float rayleighIntensity = saturate(_BurtAtmosphereRayleighIntensity);
                float mieIntensity = saturate(_BurtAtmosphereMieIntensity);
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
                if (IsSkyPixel(rawDepth) || _BurtAtmosphereAerialPerspectiveParams.w < 0.5f)
                {
                    return float4(sourceColor, 1.0f);
                }

                float3 positionWS = ReconstructPositionWS(input.screenUV, rawDepth);
                float3 cameraToPixel = positionWS - _BurtAtmosphereCameraPositionWS;
                float distanceWS = length(cameraToPixel);
                float distanceKm = distanceWS * 0.001f;
                float3 viewDirWS = SafeNormalize(cameraToPixel, float3(0.0f, 0.0f, 1.0f));

                float intensity = _BurtAtmosphereAerialPerspectiveParams.x;
                float distanceScale = max(_BurtAtmosphereAerialPerspectiveParams.y, 1.0f);
                float heightFalloff = _BurtAtmosphereAerialPerspectiveParams.z;
                float nearFadeStart = _BurtAtmosphereAerialPerspectiveFadeParams.x;
                float nearFadeEnd = max(_BurtAtmosphereAerialPerspectiveFadeParams.y, nearFadeStart + 0.001f);
                float maxOpacity = saturate(_BurtAtmosphereAerialPerspectiveFadeParams.z);
                float distanceFade = saturate(1.0f - exp(-distanceWS / distanceScale));
                float nearFade = smoothstep(nearFadeStart, nearFadeEnd, distanceWS);
                float heightFade = exp(-max(positionWS.y - _BurtAtmosphereCameraPositionWS.y, 0.0f) * heightFalloff);

                float3 rayleighExtinction = float3(0.32f, 0.58f, 1.0f) * (saturate(_BurtAtmosphereRayleighIntensity) * 0.16f);
                float3 mieExtinction = float3(1.0f, 0.92f, 0.78f) * (saturate(_BurtAtmosphereMieIntensity) * 0.06f);
                float opacityGate = saturate(nearFade * maxOpacity);
                float opticalDepth = distanceKm * distanceFade * heightFade * max(intensity, 0.0f) * opacityGate;
                float3 transmittance = exp(-(rayleighExtinction + mieExtinction) * opticalDepth);
                float scatterFade = saturate(distanceFade * intensity) * opacityGate;
                float3 inScatter = EvaluateAerialInscatter(viewDirWS, scatterFade, heightFade);
                float3 color = sourceColor * transmittance + inScatter;

                if (_BurtAtmosphereDebugMode > 3.5f && _BurtAtmosphereDebugMode < 4.5f)
                {
                    return float4(saturate(transmittance), 1.0f);
                }

                if (_BurtAtmosphereDebugMode > 4.5f && _BurtAtmosphereDebugMode < 5.5f)
                {
                    return float4(saturate(inScatter), 1.0f);
                }

                if (_BurtAtmosphereDebugMode > 5.5f)
                {
                    return float4(scatterFade, heightFade, saturate(transmittance.b), 1.0f);
                }

                return float4(max(color, 0.0f), 1.0f);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
