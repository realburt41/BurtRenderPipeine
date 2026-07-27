Shader "Hidden/BurtRP/Fog"
{
    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        Pass
        {
            Name "Burt Fog"

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

            float4 _BurtFogParams; // x=height, y=density, z=height falloff, w=max opacity
            float4 _BurtFogDistanceParams; // x=start distance, y=cutoff distance
            float4 _BurtFogAlbedo;
            float4 _BurtFogScatteringParams; // x=directional, y=ambient, z=anisotropy, w=use atmosphere horizontal scattering
            float4 _BurtFogAtmosphereRayleighTintScale;
            float4 _BurtFogAtmosphereMieTintScale;
            float4 _BurtFogAtmosphereMultipleScatteringTintScale;
            float4 _BurtFogAerialInteractionParams; // x=interaction, y=aerial fade start, z=aerial fade end
            float _BurtFogDebugMode;
            float4x4 _BurtFogInverseViewProjection;
            float3 _BurtFogCameraPositionWS;
            float4 _BurtMainLightDirection;
            float4 _BurtMainLightColor;
            float4 _BurtMainLightColorOuterSpace;
            float4 _BurtMainLightAtmosphereTransmittance;
            float4 _BurtAtmosphereHorizontalFogSunDirection;
            float4 _BurtAtmosphereHorizontalFogLightColor;
            float _BurtMainLightOcclusionFactor;

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

            bool IsSkyPixel(float rawDepth)
            {
                #if defined(UNITY_REVERSED_Z)
                    return rawDepth <= 0.00001f;
                #else
                    return rawDepth >= 0.99999f;
                #endif
            }

            float3 SafeNormalize(float3 value, float3 fallback)
            {
                float lengthSq = dot(value, value);
                return lengthSq > 1.0e-8f ? value * rsqrt(lengthSq) : fallback;
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
                float4 positionWS = mul(_BurtFogInverseViewProjection, BuildClipPosition(screenUV, rawDepth));
                positionWS.xyz /= max(abs(positionWS.w), 1.0e-6f);
                return positionWS.xyz;
            }

            float HenyeyGreensteinPhase(float cosTheta, float g)
            {
                float g2 = g * g;
                float denom = max(0.05f, pow(abs(1.0f + g2 - 2.0f * g * cosTheta), 1.5f));
                return (1.0f - g2) / (4.0f * PI * denom);
            }

            float CalcLineIntegral(float falloff, float rayDeltaY, float mediumDensity)
            {
                float scaledFalloff = max(-127.0f, falloff * rayDeltaY);
                float log2Value = log(2.0f);
                if (abs(scaledFalloff) <= 0.01f)
                {
                    return mediumDensity * (log2Value - 0.5f * log2Value * log2Value * scaledFalloff);
                }

                return mediumDensity * ((1.0f - exp2(-scaledFalloff)) / scaledFalloff);
            }

            float3 NormalizeLightColor(float3 lightColor)
            {
                float peak = max(max(lightColor.r, lightColor.g), lightColor.b);
                return peak > 0.001f ? lightColor / peak : 1.0f;
            }

            float3 FogDebugDistanceColor(float normalizedDistance)
            {
                normalizedDistance = saturate(normalizedDistance);
                return lerp(float3(0.02f, 0.08f, 0.18f), float3(0.15f, 0.62f, 1.0f), normalizedDistance);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 sourceColor = tex2D(_BurtCameraColorTexture, input.ScreenUV).rgb;
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_BurtCameraDepthTexture, input.ScreenUV);
                bool isDebug = _BurtFogDebugMode > 0.5f;
                if (IsSkyPixel(rawDepth))
                {
                    return isDebug ? float4(0.0f, 0.0f, 0.0f, 1.0f) : float4(sourceColor, 1.0f);
                }

                float3 positionWS = ReconstructPositionWS(input.ScreenUV, rawDepth);
                float3 cameraToPixel = positionWS - _BurtFogCameraPositionWS;
                float viewDistance = length(cameraToPixel);
                if (viewDistance <= 1.0e-4f)
                {
                    return isDebug ? float4(0.0f, 0.0f, 0.0f, 1.0f) : float4(sourceColor, 1.0f);
                }

                float startDistance = max(_BurtFogDistanceParams.x, 0.0f);
                float cutoffDistance = max(_BurtFogDistanceParams.y, 0.0f);
                if (cutoffDistance > 0.0f && viewDistance > cutoffDistance)
                {
                    if (isDebug && _BurtFogDebugMode > 3.5f)
                    {
                        return float4(1.0f, 0.15f, 0.05f, 1.0f);
                    }

                    return isDebug ? float4(0.0f, 0.0f, 0.0f, 1.0f) : float4(sourceColor, 1.0f);
                }

                float rayLength = max(viewDistance - startDistance, 0.0f);
                if (rayLength <= 1.0e-4f)
                {
                    if (isDebug && _BurtFogDebugMode > 3.5f)
                    {
                        return float4(0.08f, 0.08f, 0.08f, 1.0f);
                    }

                    return isDebug ? float4(0.0f, 0.0f, 0.0f, 1.0f) : float4(sourceColor, 1.0f);
                }

                float3 viewDirWS = cameraToPixel / viewDistance;
                float startT = startDistance / viewDistance;
                float startY = _BurtFogCameraPositionWS.y + cameraToPixel.y * startT;
                float rayDeltaY = cameraToPixel.y * (rayLength / viewDistance);

                float fogHeight = _BurtFogParams.x;
                float fogDensity = max(_BurtFogParams.y, 0.0f);
                float heightFalloff = max(_BurtFogParams.z, 0.001f);
                float maxOpacity = saturate(_BurtFogParams.w);
                float mediumDensity = fogDensity * exp2(-max(-127.0f, heightFalloff * (startY - fogHeight)));
                float opticalDepth = CalcLineIntegral(heightFalloff, rayDeltaY, mediumDensity) * rayLength;
                float transmittance = lerp(1.0f, exp2(-max(opticalDepth, 0.0f)), maxOpacity);
                float fogAmount = saturate(1.0f - transmittance);
                float aerialInteraction = _BurtFogAerialInteractionParams.x;
                if (aerialInteraction > 0.5f && aerialInteraction < 1.5f)
                {
                    float fadeStart = max(_BurtFogAerialInteractionParams.y, 0.0f);
                    float fadeEnd = max(_BurtFogAerialInteractionParams.z, fadeStart + 0.001f);
                    float aerialDominance = smoothstep(fadeStart, fadeEnd, viewDistance);
                    fogAmount *= 1.0f - aerialDominance;
                    transmittance = 1.0f - fogAmount;
                }

                if (isDebug)
                {
                    if (_BurtFogDebugMode < 1.5f)
                    {
                        return float4(fogAmount.xxx, 1.0f);
                    }

                    if (_BurtFogDebugMode < 2.5f)
                    {
                        return float4(transmittance.xxx, 1.0f);
                    }

                    if (_BurtFogDebugMode < 3.5f)
                    {
                        float heightDebug = saturate((positionWS.y - fogHeight) * 0.02f + 0.5f);
                        return float4(heightDebug, heightDebug, heightDebug, 1.0f);
                    }

                    float distanceRange = cutoffDistance > startDistance ? cutoffDistance - startDistance : 1000.0f;
                    float distanceDebug = (viewDistance - startDistance) / max(distanceRange, 1.0f);
                    return float4(FogDebugDistanceColor(distanceDebug), 1.0f);
                }

                float3 lightDirWS = SafeNormalize(_BurtMainLightDirection.xyz, float3(0.0f, 1.0f, 0.0f));
                // Use the unoccluded, atmosphere-transmitted light chroma here;
                // environment occlusion is applied explicitly once below.
                float3 lightColor = NormalizeLightColor(max(_BurtMainLightColorOuterSpace.rgb * _BurtMainLightAtmosphereTransmittance.rgb, 0.0f));
                float lDotV = dot(lightDirWS, viewDirWS);
                float phase = HenyeyGreensteinPhase(lDotV, _BurtFogScatteringParams.z);
                float directional = max(_BurtFogScatteringParams.x, 0.0f) * phase * 4.0f;
                float ambient = max(_BurtFogScatteringParams.y, 0.0f);
                float mainLightOcclusion = saturate(_BurtMainLightOcclusionFactor);
                float3 legacyFogColor = max(_BurtFogAlbedo.rgb, 0.0f) * (ambient + directional * lightColor * mainLightOcclusion);

                float useAtmosphereHorizontalScattering = _BurtFogScatteringParams.w * _BurtAtmosphereUseLuts;
                float3 evaluatedFogColor = legacyFogColor;
                [branch]
                if (useAtmosphereHorizontalScattering > 0.5f)
                {
                    float3 horizontalSunDirection = SafeNormalize(_BurtAtmosphereHorizontalFogSunDirection.xyz, lightDirWS);
                    float horizontalLDotV = dot(horizontalSunDirection, viewDirWS);
                    evaluatedFogColor = BurtAtmosphereEvaluateHorizontalFogLighting(
                        horizontalLDotV,
                        _BurtFogScatteringParams.z,
                        _BurtAtmosphereHorizontalFogLightColor.rgb,
                        _BurtFogAtmosphereRayleighTintScale.rgb,
                        _BurtFogAtmosphereMieTintScale.rgb,
                        _BurtFogAtmosphereMultipleScatteringTintScale.rgb,
                        1.0f,
                        mainLightOcclusion);
                }

                float3 fogColor = BurtApplyPreExposure(evaluatedFogColor);

                return float4(lerp(sourceColor, fogColor, fogAmount), 1.0f);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
