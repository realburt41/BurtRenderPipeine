Shader "Hidden/BurtRP/ScreenSpaceReflections/Denoise"
{
    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        Pass
        {
            Name "Burt Screen Space Reflections Denoise"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragDenoise

            #include "UnityCG.cginc"
            #include "ShaderLibrary/BurtDeferred.hlsl"

            sampler2D _BurtScreenSpaceReflectionColorTexture;
            float4 _BurtSSRSourceTexelSize;
            float4 _BurtSSRParams1; // z=debugMode

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

            bool BurtSSRDenoiseIsSkyDepth(float rawDepth)
            {
                #if defined(UNITY_REVERSED_Z)
                    return rawDepth <= 0.00001;
                #else
                    return rawDepth >= 0.99999;
                #endif
            }

            float BurtSSRDenoiseLuminance(float3 color)
            {
                return dot(max(color, 0.0), float3(0.2126, 0.7152, 0.0722));
            }

            float4 FragDenoise(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenUV;
                float4 centerSSR = tex2D(_BurtScreenSpaceReflectionColorTexture, screenUV);
                float centerDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtSSRDenoiseIsSkyDepth(centerDepth))
                {
                    return centerSSR;
                }

                float centerLinearDepth = LinearEyeDepth(centerDepth);
                BurtGBufferData centerGBuffer = BurtDecodeGBuffer(BurtSampleEncodedGBuffer(screenUV));

                int debugMode = (int)_BurtSSRParams1.z;
                if (BurtIsActiveHairShadingModel(centerGBuffer.shadingModelID))
                {
                    bool traceDebugMode = (debugMode > 0 && debugMode <= 8) || (debugMode >= 16 && debugMode <= 31);
                    return traceDebugMode ? float4(0.0, 0.0, 0.0, 1.0) : float4(0.0, 0.0, 0.0, 0.0);
                }

                float3 centerNormal = BurtGetReflectionNormalWS(centerGBuffer);
                float centerRoughness = BurtGetReflectionRoughness(centerGBuffer);

                if ((debugMode > 0 && debugMode <= 7) || (debugMode >= 16 && debugMode <= 31))
                {
                    return centerSSR;
                }

                float2 texel = _BurtSSRSourceTexelSize.xy;
                float centerConfidence = saturate(centerSSR.a);
                float3 accumulatedColor = centerSSR.rgb * centerConfidence;
                float accumulatedConfidence = centerConfidence;
                float totalWeight = max(centerConfidence, 0.0001);

                float baseSampleRadius = lerp(1.0, 2.25, saturate(centerRoughness * 2.0));
                float sampleRadius = lerp(1.0, baseSampleRadius, saturate(centerConfidence * 4.0));
                float fillSupport = 0.0;
                float4 axialSupport = 0.0;
                float4 diagonalSupport = 0.0;
                float3 neighborColorSum = 0.0;
                float neighborColorWeight = 0.0;
                float neighborMaxLuminance = 0.0;

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
                    float2 sampleUV = screenUV + offset * texel * sampleRadius;
                    if (!all(sampleUV >= 0.0) || !all(sampleUV <= 1.0))
                    {
                        continue;
                    }

                    float4 sampleSSR = tex2D(_BurtScreenSpaceReflectionColorTexture, sampleUV);
                    float sampleDepth = BurtSampleDeferredRawDepth(sampleUV);
                    if (BurtSSRDenoiseIsSkyDepth(sampleDepth))
                    {
                        continue;
                    }

                    float sampleLinearDepth = LinearEyeDepth(sampleDepth);
                    BurtGBufferData sampleGBuffer = BurtDecodeGBuffer(BurtSampleEncodedGBuffer(sampleUV));
                    float normalWeight = saturate(dot(centerNormal, BurtGetReflectionNormalWS(sampleGBuffer)));
                    normalWeight *= normalWeight * normalWeight;
                    float depthTolerance = max(centerLinearDepth * 0.015, 0.01);
                    float depthWeight = exp2(-abs(sampleLinearDepth - centerLinearDepth) / depthTolerance);
                    depthWeight *= depthWeight;
                    float roughnessWeight = exp2(-abs(BurtGetReflectionRoughness(sampleGBuffer) - centerRoughness) * 10.0);
                    float sampleConfidence = saturate(sampleSSR.a);
                    float confidenceGate = smoothstep(0.02, 0.18, sampleConfidence);
                    float alphaWeight = centerConfidence > 0.05 ?
                        smoothstep(0.0, 0.35, saturate(1.0 - abs(sampleConfidence - centerConfidence) * 2.5)) :
                        confidenceGate;
                    float tapWeight = sampleIndex < 4 ? 1.0 : 0.7071;
                    float weight = tapWeight * normalWeight * depthWeight * roughnessWeight * alphaWeight * confidenceGate;
                    float support = sampleConfidence > 0.08 ? weight : 0.0;
                    fillSupport += support;
                    axialSupport.x += sampleIndex == 0 ? support : 0.0;
                    axialSupport.y += sampleIndex == 1 ? support : 0.0;
                    axialSupport.z += sampleIndex == 2 ? support : 0.0;
                    axialSupport.w += sampleIndex == 3 ? support : 0.0;
                    diagonalSupport.x += sampleIndex == 4 ? support : 0.0;
                    diagonalSupport.y += sampleIndex == 5 ? support : 0.0;
                    diagonalSupport.z += sampleIndex == 6 ? support : 0.0;
                    diagonalSupport.w += sampleIndex == 7 ? support : 0.0;
                    accumulatedColor += sampleSSR.rgb * sampleConfidence * weight;
                    accumulatedConfidence += sampleConfidence * weight;
                    totalWeight += weight;

                    float neighborWeight = weight * sampleConfidence;
                    neighborColorSum += sampleSSR.rgb * neighborWeight;
                    neighborColorWeight += neighborWeight;
                    neighborMaxLuminance = max(neighborMaxLuminance, BurtSSRDenoiseLuminance(sampleSSR.rgb) * smoothstep(0.002, 0.08, neighborWeight));
                }

                float outputConfidence = saturate(accumulatedConfidence / max(totalWeight, 0.0001));
                float3 outputColor = accumulatedConfidence > 0.0001 ? accumulatedColor / accumulatedConfidence : float3(0.0, 0.0, 0.0);
                float surroundedSupport = min(max(axialSupport.x, axialSupport.y), max(axialSupport.z, axialSupport.w));
                float pairedAxialSupport = max(min(axialSupport.x, axialSupport.y), min(axialSupport.z, axialSupport.w));
                float pairedDiagonalSupport = max(min(diagonalSupport.x, diagonalSupport.w), min(diagonalSupport.y, diagonalSupport.z));
                float pairedSupport = max(pairedAxialSupport, pairedDiagonalSupport);
                float twoDimensionalSupport = max(surroundedSupport, pairedDiagonalSupport);
                float primaryFillGate =
                    smoothstep(0.75, 1.45, fillSupport) *
                    smoothstep(0.08, 0.22, twoDimensionalSupport) *
                    smoothstep(0.04, 0.14, pairedSupport);
                float fillGate = primaryFillGate;
                float centerHitReliability = smoothstep(0.003, 0.012, centerConfidence);
                float weakCenterBlend = 1.0 - centerHitReliability;
                float fillEnergy = fillSupport;
                float fillConfidence = min(outputConfidence * fillGate * saturate((fillEnergy - 0.25) * 0.6) * saturate(twoDimensionalSupport * 4.0), 0.55);
                float centerLock = smoothstep(0.006, 0.06, centerConfidence);
                float stableConfidence = lerp(outputConfidence, centerConfidence, centerLock * 0.35);
                outputColor = lerp(outputColor, centerSSR.rgb, centerLock * 0.65);
                outputConfidence = lerp(stableConfidence, fillConfidence, weakCenterBlend);
                float3 neighborColor = neighborColorWeight > 0.0001 ? neighborColorSum / neighborColorWeight : outputColor;
                float centerLuminance = BurtSSRDenoiseLuminance(centerSSR.rgb);
                float neighborLuminance = max(BurtSSRDenoiseLuminance(neighborColor), neighborMaxLuminance);
                float relativeOutlier = (centerLuminance - neighborLuminance) / max(max(centerLuminance, neighborLuminance), 0.035);
                float roughFireflyGate = smoothstep(0.08, 0.42, centerRoughness);
                float isolatedGate = 1.0 - smoothstep(0.08, 0.52, fillSupport);
                float brightOutlierGate = smoothstep(0.18, 0.62, relativeOutlier);
                float fireflyGate = roughFireflyGate * isolatedGate * brightOutlierGate * smoothstep(0.012, 0.12, centerConfidence);
                outputColor = lerp(outputColor, min(outputColor, max(neighborColor, centerSSR.rgb * 0.35)), fireflyGate);
                outputConfidence *= lerp(1.0, 0.12, fireflyGate);

                return float4(outputColor, outputConfidence);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
