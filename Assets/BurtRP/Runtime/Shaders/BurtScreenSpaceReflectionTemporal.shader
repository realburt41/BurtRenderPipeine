Shader "Hidden/BurtRP/ScreenSpaceReflections/Temporal"
{
    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        Pass
        {
            Name "Burt Screen Space Reflections Temporal"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragTemporal

            #include "UnityCG.cginc"
            #include "ShaderLibrary/BurtDeferred.hlsl"

            sampler2D _BurtScreenSpaceReflectionDenoisedColorTexture;
            sampler2D _BurtSSRHistoryTexture;
            sampler2D _BurtSSRHistoryDepthTexture;
            sampler2D _BurtSSRHistoryNormalRoughnessTexture;
            sampler2D _BurtSSRHistoryMomentTexture;
            float4 _BurtSSRSourceTexelSize;
            float4x4 _BurtSSRPreviousViewMatrix;
            float4x4 _BurtSSRPreviousViewProjectionMatrix;
            float4 _BurtSSRTemporalParams0; // x=feedback, y=historyValid, z=depthRejection, w=clampStrength
            float4 _BurtSSRParams1; // z=debugMode
            static const float BurtSSRHistoryMax = 32.0;

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
                output.positionCS = BurtGetFullScreenTriangleVertexPosition(input.vertexID);
                output.screenUV = BurtGetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            bool BurtSSRTemporalIsSkyDepth(float rawDepth)
            {
                #if defined(UNITY_REVERSED_Z)
                    return rawDepth <= 0.00001;
                #else
                    return rawDepth >= 0.99999;
                #endif
            }

            float BurtSSRTemporalLuminance(float3 color)
            {
                return dot(max(color, 0.0), float3(0.2126, 0.7152, 0.0722));
            }

            float2 BurtSSRTemporalClipToScreenUV(float2 clipXY)
            {
                float2 uv = clipXY * 0.5 + 0.5;
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0 - uv.y;
                #endif
                return uv;
            }

            bool BurtSSRTemporalProjectPrevious(float3 positionWS, out float2 previousUV, out float previousLinearDepth)
            {
                float4 previousClip = mul(_BurtSSRPreviousViewProjectionMatrix, float4(positionWS, 1.0));
                if (previousClip.w <= 0.00001)
                {
                    previousUV = 0.0;
                    previousLinearDepth = 0.0;
                    return false;
                }

                float3 previousNDC = previousClip.xyz / previousClip.w;
                previousUV = BurtSSRTemporalClipToScreenUV(previousNDC.xy);
                float3 previousVS = mul(_BurtSSRPreviousViewMatrix, float4(positionWS, 1.0)).xyz;
                previousLinearDepth = max(-previousVS.z, 0.0);
                return all(previousUV >= 0.0) && all(previousUV <= 1.0);
            }

            void BurtSSRTemporalNeighborhood(
                float2 screenUV,
                float2 texel,
                out float3 neighborhoodMin,
                out float3 neighborhoodMax,
                out float neighborhoodAlpha)
            {
                float4 center = tex2D(_BurtScreenSpaceReflectionDenoisedColorTexture, screenUV);
                float centerAlpha = saturate(center.a);
                neighborhoodMin = center.rgb;
                neighborhoodMax = center.rgb;
                neighborhoodAlpha = centerAlpha;
                float hasColorBounds = smoothstep(0.003, 0.08, centerAlpha);

                [unroll(8)]
                for (int sampleIndex = 0; sampleIndex < 8; sampleIndex++)
                {
                    float2 offset = sampleIndex == 0 ? float2(1.0, 0.0) :
                        sampleIndex == 1 ? float2(-1.0, 0.0) :
                        sampleIndex == 2 ? float2(0.0, 1.0) :
                        sampleIndex == 3 ? float2(0.0, -1.0) :
                        sampleIndex == 4 ? float2(1.0, 1.0) :
                        sampleIndex == 5 ? float2(-1.0, 1.0) :
                        sampleIndex == 6 ? float2(1.0, -1.0) :
                        float2(-1.0, -1.0);
                    float2 sampleUV = screenUV + offset * texel;
                    if (!all(sampleUV >= 0.0) || !all(sampleUV <= 1.0))
                    {
                        continue;
                    }

                    float4 sampleSSR = tex2D(_BurtScreenSpaceReflectionDenoisedColorTexture, sampleUV);
                    float sampleAlpha = saturate(sampleSSR.a);
                    neighborhoodAlpha = max(neighborhoodAlpha, sampleAlpha);
                    float sampleColorWeight = smoothstep(0.003, 0.08, sampleAlpha);
                    if (sampleColorWeight <= 0.0)
                    {
                        continue;
                    }

                    neighborhoodMin = hasColorBounds > 0.0 ? min(neighborhoodMin, sampleSSR.rgb) : sampleSSR.rgb;
                    neighborhoodMax = hasColorBounds > 0.0 ? max(neighborhoodMax, sampleSSR.rgb) : sampleSSR.rgb;
                    hasColorBounds = 1.0;
                }
            }

            float BurtSSRTemporalLoadHistory(
                float2 previousUV,
                float previousLinearDepth,
                float3 currentNormalWS,
                float currentRoughness,
                out float4 historySSR,
                out float4 historyMoment,
                out float depthWeight)
            {
                historySSR = 0.0;
                historyMoment = 0.0;
                depthWeight = 0.0;

                if (_BurtSSRSourceTexelSize.x <= 0.0 || _BurtSSRSourceTexelSize.y <= 0.0)
                {
                    return 0.0;
                }

                float2 texel = _BurtSSRSourceTexelSize.xy;
                float2 previousPixel = previousUV * _BurtSSRSourceTexelSize.zw - 0.5;
                float2 basePixel = floor(previousPixel);
                float2 bilinearFraction = saturate(previousPixel - basePixel);
                float depthTolerance = max(previousLinearDepth * _BurtSSRTemporalParams0.z, 0.02);
                float totalWeight = 0.0;
                float totalColorWeight = 0.0;

                [unroll]
                for (int sampleIndex = 0; sampleIndex < 4; sampleIndex++)
                {
                    float2 offset = float2(0.0, 0.0);
                    if (sampleIndex == 1)
                    {
                        offset = float2(1.0, 0.0);
                    }
                    else if (sampleIndex == 2)
                    {
                        offset = float2(0.0, 1.0);
                    }
                    else if (sampleIndex == 3)
                    {
                        offset = float2(1.0, 1.0);
                    }

                    float2 sampleUV = (basePixel + offset + 0.5) * texel;
                    if (!all(sampleUV >= 0.0) || !all(sampleUV <= 1.0))
                    {
                        continue;
                    }

                    float4 previousMoment = tex2D(_BurtSSRHistoryMomentTexture, sampleUV);
                    if (previousMoment.a <= 0.0)
                    {
                        continue;
                    }

                    float previousRawDepth = tex2D(_BurtSSRHistoryDepthTexture, sampleUV).r;
                    if (BurtSSRTemporalIsSkyDepth(previousRawDepth))
                    {
                        continue;
                    }

                    float previousHistoryLinearDepth = LinearEyeDepth(previousRawDepth);
                    float sampleDepthWeight = exp2(-abs(previousHistoryLinearDepth - previousLinearDepth) / depthTolerance);
                    float4 previousNormalRoughness = tex2D(_BurtSSRHistoryNormalRoughnessTexture, sampleUV);
                    if (dot(previousNormalRoughness.rgb, previousNormalRoughness.rgb) <= 0.0001)
                    {
                        continue;
                    }

                    float3 previousNormalWS = BurtSafeNormalize(previousNormalRoughness.rgb * 2.0 - 1.0);
                    float previousRoughness = saturate(previousNormalRoughness.a);
                    float sampleNormalWeight = smoothstep(0.72, 0.96, dot(currentNormalWS, previousNormalWS));
                    float sampleRoughnessWeight = exp2(-abs(previousRoughness - currentRoughness) * 12.0);
                    float bilinearWeight = (offset.x > 0.5 ? bilinearFraction.x : 1.0 - bilinearFraction.x) *
                        (offset.y > 0.5 ? bilinearFraction.y : 1.0 - bilinearFraction.y);
                    float sampleWeight = bilinearWeight * sampleDepthWeight * sampleNormalWeight * sampleRoughnessWeight;
                    float4 previousSSR = tex2D(_BurtSSRHistoryTexture, sampleUV);
                    float previousConfidence = saturate(previousSSR.a);
                    float colorWeight = sampleWeight * smoothstep(0.003, 0.08, previousConfidence);
                    historySSR.rgb += previousSSR.rgb * previousConfidence * colorWeight;
                    historySSR.a += previousConfidence * sampleWeight;
                    historyMoment += previousMoment * sampleWeight;
                    depthWeight += sampleDepthWeight * sampleNormalWeight * sampleRoughnessWeight * bilinearWeight;
                    totalWeight += sampleWeight;
                    totalColorWeight += previousConfidence * colorWeight;
                }

                if (totalWeight <= 0.01)
                {
                    historySSR = 0.0;
                    historyMoment = 0.0;
                    depthWeight = 0.0;
                    return 0.0;
                }

                historySSR.rgb = totalColorWeight > 0.0001 ? historySSR.rgb / totalColorWeight : float3(0.0, 0.0, 0.0);
                historySSR.a /= totalWeight;
                historyMoment /= totalWeight;
                depthWeight = saturate(depthWeight);
                return 1.0;
            }

            float4 FragTemporal(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenUV;
                float4 currentSSR = tex2D(_BurtScreenSpaceReflectionDenoisedColorTexture, screenUV);
                int debugMode = (int)_BurtSSRParams1.z;
                if ((debugMode > 0 && debugMode <= 8) || (debugMode >= 16 && debugMode <= 31))
                {
                    return currentSSR;
                }

                float historyValid = saturate(_BurtSSRTemporalParams0.y);
                if (historyValid <= 0.0)
                {
                    return currentSSR;
                }

                float currentRawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtSSRTemporalIsSkyDepth(currentRawDepth))
                {
                    return float4(0.0, 0.0, 0.0, 0.0);
                }

                float3 positionWS = BurtReconstructDeferredPositionWS(screenUV, currentRawDepth);
                BurtGBufferData currentGBuffer = BurtDecodeGBuffer(BurtSampleEncodedGBuffer(screenUV));
                if (BurtIsActiveHairShadingModel(currentGBuffer.shadingModelID))
                {
                    return float4(0.0, 0.0, 0.0, 0.0);
                }

                float3 currentNormalWS = BurtGetReflectionNormalWS(currentGBuffer);
                float currentRoughness = BurtGetReflectionRoughness(currentGBuffer);
                float2 previousUV;
                float previousLinearDepth;
                if (!BurtSSRTemporalProjectPrevious(positionWS, previousUV, previousLinearDepth))
                {
                    return currentSSR;
                }

                float4 historySSR;
                float4 historyMoment;
                float depthWeight;
                if (BurtSSRTemporalLoadHistory(previousUV, previousLinearDepth, currentNormalWS, currentRoughness, historySSR, historyMoment, depthWeight) <= 0.0)
                {
                    return currentSSR;
                }

                float3 neighborhoodMin;
                float3 neighborhoodMax;
                float neighborhoodAlpha;
                BurtSSRTemporalNeighborhood(screenUV, _BurtSSRSourceTexelSize.xy, neighborhoodMin, neighborhoodMax, neighborhoodAlpha);
                float3 neighborhoodRange = max(neighborhoodMax - neighborhoodMin, 0.0001);
                float historyLength = min(BurtSSRHistoryMax, max(historyMoment.a * BurtSSRHistoryMax, 0.0) + 1.0);
                float historyVariance = max(historyMoment.g - historyMoment.r * historyMoment.r, historyMoment.b);
                float varianceSigma = sqrt(max(historyVariance, 0.0));
                float varianceConfidence = 1.0 - smoothstep(0.02, 0.25, varianceSigma);
                float shortHistoryGate = 1.0 - smoothstep(4.0, 10.0, historyLength);
                float currentConfidence = saturate(currentSSR.a);
                float currentHitGate = smoothstep(0.02, 0.12, currentConfidence);
                float unstableHistoryGate = max(1.0 - varianceConfidence, shortHistoryGate * currentHitGate);
                float clampExpand = max(_BurtSSRTemporalParams0.w - 1.0, 0.0) * 0.5;
                clampExpand *= lerp(1.0, 0.35, unstableHistoryGate);
                float3 historyColor = clamp(historySSR.rgb, neighborhoodMin - neighborhoodRange * clampExpand, neighborhoodMax + neighborhoodRange * clampExpand);

                float historyConfidence = saturate(historySSR.a) * depthWeight;
                float hitResponsiveWeight = smoothstep(0.006, 0.08, currentConfidence);
                float holeSeed = max(neighborhoodAlpha, min(historyConfidence, neighborhoodAlpha + 0.03));
                float holeSupport = smoothstep(0.015, 0.12, holeSeed) *
                    (1.0 - smoothstep(0.015, 0.08, currentConfidence));
                float historyResponsiveWeight = smoothstep(0.02, 0.12, historyConfidence);
                float responsiveWeight = max(hitResponsiveWeight, holeSupport * historyResponsiveWeight);
                float feedback = saturate(_BurtSSRTemporalParams0.x * depthWeight * responsiveWeight);
                float minimumCurrentWeight = max(1.0 - _BurtSSRTemporalParams0.x, 1.0 / historyLength);
                feedback = min(feedback, 1.0 - minimumCurrentWeight);
                feedback *= lerp(0.4, 1.0, varianceConfidence);
                float currentSupport = max(currentConfidence, neighborhoodAlpha);
                float historyOnlyGate = smoothstep(0.08, 0.24, historyConfidence) *
                    (1.0 - smoothstep(0.01, 0.08, currentSupport));
                feedback *= lerp(1.0, 0.12, historyOnlyGate);
                float alphaDivergence = abs(historyConfidence - currentConfidence) /
                    max(max(historyConfidence, currentConfidence), 0.05);
                float alphaDivergenceGate = smoothstep(0.45, 0.9, alphaDivergence) *
                    smoothstep(0.04, 0.16, max(historyConfidence, currentConfidence)) *
                    (1.0 - holeSupport * 0.75);
                feedback *= lerp(1.0, 0.55, alphaDivergenceGate);
                float currentLuminance = BurtSSRTemporalLuminance(currentSSR.rgb);
                float historyLuminance = BurtSSRTemporalLuminance(historyColor);
                float luminanceDivergence = abs(historyLuminance - currentLuminance) / max(max(historyLuminance, currentLuminance), 0.02);
                float unstableDivergenceGate = smoothstep(0.35, 1.25, luminanceDivergence) * currentHitGate * unstableHistoryGate;
                float confidentDivergenceGate = smoothstep(0.55, 1.35, luminanceDivergence) *
                    currentHitGate * (1.0 - holeSupport * 0.5);
                float divergenceGate = max(unstableDivergenceGate, confidentDivergenceGate * 0.65);
                feedback *= lerp(1.0, 0.4, divergenceGate);
                float3 outputColor = lerp(currentSSR.rgb, historyColor, feedback);
                float outputConfidence = saturate(lerp(currentConfidence, historyConfidence, feedback));
                float holeFillConfidence = min(neighborhoodAlpha, historyConfidence) * holeSupport * 0.55;
                float outputSupport = max(currentConfidence, outputConfidence);
                float outputHoleGate = smoothstep(0.015, 0.08, holeFillConfidence) * (1.0 - smoothstep(0.01, 0.06, outputSupport));
                outputColor = lerp(outputColor, historyColor, outputHoleGate);
                return float4(outputColor, outputConfidence);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Burt Screen Space Reflections Copy Temporal Color"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragCopyTemporalColor

            #include "UnityCG.cginc"
            #include "ShaderLibrary/BurtDeferred.hlsl"

            sampler2D _BurtScreenSpaceReflectionTemporalColorTexture;

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
                output.positionCS = BurtGetFullScreenTriangleVertexPosition(input.vertexID);
                output.screenUV = BurtGetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            float4 FragCopyTemporalColor(Varyings input) : SV_Target
            {
                return tex2D(_BurtScreenSpaceReflectionTemporalColorTexture, input.screenUV);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Burt Screen Space Reflections Copy Depth History"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragCopyDepthHistory

            #include "UnityCG.cginc"
            #include "ShaderLibrary/BurtDeferred.hlsl"

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
                output.positionCS = BurtGetFullScreenTriangleVertexPosition(input.vertexID);
                output.screenUV = BurtGetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            float4 FragCopyDepthHistory(Varyings input) : SV_Target
            {
                float rawDepth = BurtSampleDeferredRawDepth(input.screenUV);
                return float4(rawDepth, rawDepth, rawDepth, rawDepth);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Burt Screen Space Reflections Copy Normal Roughness History"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragCopyNormalRoughnessHistory

            #include "UnityCG.cginc"
            #include "ShaderLibrary/BurtDeferred.hlsl"

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
                output.positionCS = BurtGetFullScreenTriangleVertexPosition(input.vertexID);
                output.screenUV = BurtGetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            float4 FragCopyNormalRoughnessHistory(Varyings input) : SV_Target
            {
                BurtGBufferData gbufferData = BurtDecodeGBuffer(BurtSampleEncodedGBuffer(input.screenUV));
                if (BurtIsActiveHairShadingModel(gbufferData.shadingModelID))
                {
                    return 0.0;
                }

                float3 normalWS = BurtGetReflectionNormalWS(gbufferData);
                return float4(normalWS * 0.5 + 0.5, BurtGetReflectionRoughness(gbufferData));
            }
            ENDHLSL
        }

        Pass
        {
            Name "Burt Screen Space Reflections Copy Moment History"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragCopyMomentHistory

            #include "UnityCG.cginc"
            #include "ShaderLibrary/BurtDeferred.hlsl"

            sampler2D _BurtScreenSpaceReflectionTemporalColorTexture;
            sampler2D _BurtSSRHistoryMomentTexture;
            sampler2D _BurtSSRHistoryDepthTexture;
            sampler2D _BurtSSRHistoryNormalRoughnessTexture;
            float4 _BurtSSRSourceTexelSize;
            float4x4 _BurtSSRPreviousViewMatrix;
            float4x4 _BurtSSRPreviousViewProjectionMatrix;
            float4 _BurtSSRTemporalParams0; // x=feedback, y=historyValid, z=depthRejection
            static const float BurtSSRHistoryMax = 32.0;
            static const float BurtSSRInvHistoryMax = 0.03125;

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
                output.positionCS = BurtGetFullScreenTriangleVertexPosition(input.vertexID);
                output.screenUV = BurtGetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            bool BurtSSRTemporalMomentIsSkyDepth(float rawDepth)
            {
                #if defined(UNITY_REVERSED_Z)
                    return rawDepth <= 0.00001;
                #else
                    return rawDepth >= 0.99999;
                #endif
            }

            float BurtSSRTemporalMomentLuminance(float3 color)
            {
                return dot(max(color, 0.0), float3(0.2126, 0.7152, 0.0722));
            }

            float2 BurtSSRTemporalMomentClipToScreenUV(float2 clipXY)
            {
                float2 uv = clipXY * 0.5 + 0.5;
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0 - uv.y;
                #endif
                return uv;
            }

            bool BurtSSRTemporalMomentProjectPrevious(float3 positionWS, out float2 previousUV, out float previousLinearDepth)
            {
                float4 previousClip = mul(_BurtSSRPreviousViewProjectionMatrix, float4(positionWS, 1.0));
                if (previousClip.w <= 0.00001)
                {
                    previousUV = 0.0;
                    previousLinearDepth = 0.0;
                    return false;
                }

                float3 previousNDC = previousClip.xyz / previousClip.w;
                previousUV = BurtSSRTemporalMomentClipToScreenUV(previousNDC.xy);
                float3 previousVS = mul(_BurtSSRPreviousViewMatrix, float4(positionWS, 1.0)).xyz;
                previousLinearDepth = max(-previousVS.z, 0.0);
                return all(previousUV >= 0.0) && all(previousUV <= 1.0);
            }

            float BurtSSRTemporalMomentLoadPrevious(
                float2 previousUV,
                float previousLinearDepth,
                float3 currentNormalWS,
                float currentRoughness,
                out float4 previousMoment)
            {
                previousMoment = 0.0;
                if (_BurtSSRSourceTexelSize.x <= 0.0 || _BurtSSRSourceTexelSize.y <= 0.0)
                {
                    return 0.0;
                }

                float2 texel = _BurtSSRSourceTexelSize.xy;
                float2 previousPixel = previousUV * _BurtSSRSourceTexelSize.zw - 0.5;
                float2 basePixel = floor(previousPixel);
                float2 bilinearFraction = saturate(previousPixel - basePixel);
                float depthTolerance = max(previousLinearDepth * _BurtSSRTemporalParams0.z, 0.02);
                float totalWeight = 0.0;

                [unroll]
                for (int sampleIndex = 0; sampleIndex < 4; sampleIndex++)
                {
                    float2 offset = float2(0.0, 0.0);
                    if (sampleIndex == 1)
                    {
                        offset = float2(1.0, 0.0);
                    }
                    else if (sampleIndex == 2)
                    {
                        offset = float2(0.0, 1.0);
                    }
                    else if (sampleIndex == 3)
                    {
                        offset = float2(1.0, 1.0);
                    }

                    float2 sampleUV = (basePixel + offset + 0.5) * texel;
                    if (!all(sampleUV >= 0.0) || !all(sampleUV <= 1.0))
                    {
                        continue;
                    }

                    float4 sampleMoment = tex2D(_BurtSSRHistoryMomentTexture, sampleUV);
                    if (sampleMoment.a <= 0.0)
                    {
                        continue;
                    }

                    float previousRawDepth = tex2D(_BurtSSRHistoryDepthTexture, sampleUV).r;
                    if (BurtSSRTemporalMomentIsSkyDepth(previousRawDepth))
                    {
                        continue;
                    }

                    float previousHistoryLinearDepth = LinearEyeDepth(previousRawDepth);
                    float sampleDepthWeight = exp2(-abs(previousHistoryLinearDepth - previousLinearDepth) / depthTolerance);
                    float4 previousNormalRoughness = tex2D(_BurtSSRHistoryNormalRoughnessTexture, sampleUV);
                    if (dot(previousNormalRoughness.rgb, previousNormalRoughness.rgb) <= 0.0001)
                    {
                        continue;
                    }

                    float3 previousNormalWS = BurtSafeNormalize(previousNormalRoughness.rgb * 2.0 - 1.0);
                    float previousRoughness = saturate(previousNormalRoughness.a);
                    float sampleNormalWeight = smoothstep(0.72, 0.96, dot(currentNormalWS, previousNormalWS));
                    float sampleRoughnessWeight = exp2(-abs(previousRoughness - currentRoughness) * 12.0);
                    float bilinearWeight = (offset.x > 0.5 ? bilinearFraction.x : 1.0 - bilinearFraction.x) *
                        (offset.y > 0.5 ? bilinearFraction.y : 1.0 - bilinearFraction.y);
                    float sampleWeight = bilinearWeight * sampleDepthWeight * sampleNormalWeight * sampleRoughnessWeight;
                    previousMoment += sampleMoment * sampleWeight;
                    totalWeight += sampleWeight;
                }

                if (totalWeight <= 0.01)
                {
                    previousMoment = 0.0;
                    return 0.0;
                }

                previousMoment /= totalWeight;
                return 1.0;
            }

            float BurtSSRTemporalMomentSpatialVariance(
                float2 screenUV,
                float centerRawDepth,
                float3 centerNormalWS,
                float centerRoughness,
                float centerLuminance)
            {
                if (_BurtSSRSourceTexelSize.x <= 0.0 || _BurtSSRSourceTexelSize.y <= 0.0)
                {
                    return 0.0;
                }

                float centerLinearDepth = LinearEyeDepth(centerRawDepth);
                float depthTolerance = max(centerLinearDepth * max(_BurtSSRTemporalParams0.z * 2.0, 0.01), 0.03);
                float weightSum = 1.0;
                float moment1 = centerLuminance;
                float moment2 = centerLuminance * centerLuminance;

                [unroll]
                for (int sampleIndex = 0; sampleIndex < 4; sampleIndex++)
                {
                    float2 offset = sampleIndex == 0 ? float2(1.0, 0.0) :
                        sampleIndex == 1 ? float2(-1.0, 0.0) :
                        sampleIndex == 2 ? float2(0.0, 1.0) :
                        float2(0.0, -1.0);
                    float2 sampleUV = screenUV + offset * _BurtSSRSourceTexelSize.xy;
                    if (!all(sampleUV >= 0.0) || !all(sampleUV <= 1.0))
                    {
                        continue;
                    }

                    float sampleRawDepth = BurtSampleDeferredRawDepth(sampleUV);
                    if (BurtSSRTemporalMomentIsSkyDepth(sampleRawDepth))
                    {
                        continue;
                    }

                    float4 sampleSSR = tex2D(_BurtScreenSpaceReflectionTemporalColorTexture, sampleUV);
                    float sampleAlphaWeight = smoothstep(0.0001, 0.08, saturate(sampleSSR.a));
                    if (sampleAlphaWeight <= 0.0)
                    {
                        continue;
                    }

                    BurtGBufferData sampleGBuffer = BurtDecodeGBuffer(BurtSampleEncodedGBuffer(sampleUV));
                    if (BurtIsActiveHairShadingModel(sampleGBuffer.shadingModelID))
                    {
                        continue;
                    }

                    float3 sampleNormalWS = BurtGetReflectionNormalWS(sampleGBuffer);
                    float sampleLinearDepth = LinearEyeDepth(sampleRawDepth);
                    float depthWeight = exp2(-abs(sampleLinearDepth - centerLinearDepth) / depthTolerance);
                    float normalWeight = smoothstep(0.72, 0.96, dot(centerNormalWS, sampleNormalWS));
                    float roughnessWeight = exp2(-abs(BurtGetReflectionRoughness(sampleGBuffer) - centerRoughness) * 10.0);
                    float sampleWeight = sampleAlphaWeight * depthWeight * normalWeight * roughnessWeight;
                    float sampleLuminance = BurtSSRTemporalMomentLuminance(sampleSSR.rgb);
                    moment1 += sampleLuminance * sampleWeight;
                    moment2 += sampleLuminance * sampleLuminance * sampleWeight;
                    weightSum += sampleWeight;
                }

                if (weightSum <= 1.01)
                {
                    return 0.0;
                }

                moment1 /= weightSum;
                moment2 /= weightSum;
                return max(moment2 - moment1 * moment1, 0.0);
            }

            float4 FragCopyMomentHistory(Varyings input) : SV_Target
            {
                float rawDepth = BurtSampleDeferredRawDepth(input.screenUV);
                float4 temporalSSR = tex2D(_BurtScreenSpaceReflectionTemporalColorTexture, input.screenUV);
                float confidence = saturate(temporalSSR.a);
                if (BurtSSRTemporalMomentIsSkyDepth(rawDepth) || confidence <= 0.0001)
                {
                    return 0.0;
                }

                float historyValid = saturate(_BurtSSRTemporalParams0.y);
                BurtGBufferData currentGBuffer = BurtDecodeGBuffer(BurtSampleEncodedGBuffer(input.screenUV));
                if (BurtIsActiveHairShadingModel(currentGBuffer.shadingModelID))
                {
                    return 0.0;
                }

                float3 currentNormalWS = BurtGetReflectionNormalWS(currentGBuffer);
                float currentRoughness = BurtGetReflectionRoughness(currentGBuffer);
                float3 positionWS = BurtReconstructDeferredPositionWS(input.screenUV, rawDepth);
                float2 previousUV;
                float previousLinearDepth;
                float4 previousMomentSample = 0.0;
                float hasPreviousMoment = 0.0;
                if (historyValid > 0.0 && BurtSSRTemporalMomentProjectPrevious(positionWS, previousUV, previousLinearDepth))
                {
                    hasPreviousMoment = BurtSSRTemporalMomentLoadPrevious(previousUV, previousLinearDepth, currentNormalWS, currentRoughness, previousMomentSample);
                }

                float previousHistoryLength = previousMomentSample.a * BurtSSRHistoryMax;
                float confidenceHistoryGate = smoothstep(0.015, 0.12, confidence);
                float historyLength = min(BurtSSRHistoryMax, (max(previousHistoryLength, 0.0) + 1.0) * confidenceHistoryGate);
                float safeHistoryLength = max(historyLength, 1.0);
                float momentAlpha = max(0.2, 1.0 - saturate(_BurtSSRTemporalParams0.x));
                momentAlpha = max(momentAlpha, 1.0 / safeHistoryLength);
                momentAlpha = hasPreviousMoment > 0.0 ? momentAlpha : 1.0;
                momentAlpha = lerp(1.0, momentAlpha, confidenceHistoryGate);
                float luminance = BurtSSRTemporalMomentLuminance(temporalSSR.rgb);
                float2 currentMoment = float2(luminance, luminance * luminance);
                float2 previousMoment = previousMomentSample.rg;
                float2 moment = lerp(previousMoment, currentMoment, saturate(momentAlpha));
                float temporalVariance = max(moment.y - moment.x * moment.x, 0.0);
                float shortHistoryWeight = 1.0 - smoothstep(3.0, 4.0, historyLength);
                float spatialVariance = shortHistoryWeight > 0.0 ?
                    BurtSSRTemporalMomentSpatialVariance(input.screenUV, rawDepth, currentNormalWS, currentRoughness, luminance) :
                    0.0;
                float shortHistoryVariance = spatialVariance * max(1.0, 4.0 / max(historyLength, 1.0));
                float variance = lerp(temporalVariance, max(temporalVariance, shortHistoryVariance), shortHistoryWeight);
                return float4(moment, variance, historyLength * BurtSSRInvHistoryMax);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
