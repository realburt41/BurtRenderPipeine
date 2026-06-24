Shader "Hidden/BurtRP/ScreenSpaceReflections/Composite"
{
    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        Pass
        {
            Name "Burt Screen Space Reflections Composite"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend One Zero

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragComposite

            #include "UnityCG.cginc"
            #include "ShaderLibrary/BurtDeferred.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLighting.hlsl"

            sampler2D _BurtSSRCameraColorCopyTexture;
            sampler2D _BurtScreenSpaceReflectionTemporalColorTexture;
            sampler2D _BurtSSRHistoryMomentTexture;
            float4 _BurtSSRSourceTexelSize;
            float4 _BurtSSRParams0; // z=intensity, w=roughnessFade
            float4 _BurtSSRParams1; // y=maxMip, z=debugMode, w=edgeFadeWidth
            static const float BurtSSRCompositeHistoryMax = 32.0;

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

            bool BurtSSRCompositeIsSkyDepth(float rawDepth)
            {
                #if defined(UNITY_REVERSED_Z)
                    return rawDepth <= 0.00001;
                #else
                    return rawDepth >= 0.99999;
                #endif
            }

            float BurtSSRCompositeLuminance(float3 color)
            {
                return dot(max(color, 0.0), float3(0.2126, 0.7152, 0.0722));
            }

            float BurtSSRCompositeLuminanceWeight(
                float centerLuminance,
                float sampleLuminance,
                float centerAlpha,
                float roughnessGate,
                float holeGate,
                float varianceGate)
            {
                float edgeStopStrength = max(roughnessGate, varianceGate) *
                    smoothstep(0.035, 0.16, centerAlpha) *
                    (1.0 - holeGate * 0.7);
                if (edgeStopStrength <= 0.0001)
                {
                    return 1.0;
                }

                float luminanceScale = max(max(centerLuminance, sampleLuminance), 0.02);
                float luminanceTolerance = max(0.045, luminanceScale * lerp(0.32, 0.58, roughnessGate));
                luminanceTolerance *= lerp(1.0, 1.35, varianceGate);
                float edgeWeight = exp2(-abs(sampleLuminance - centerLuminance) / luminanceTolerance);
                return lerp(1.0, edgeWeight, saturate(edgeStopStrength));
            }

            float BurtSSRCompositeSurfaceWeight(
                float2 sampleUV,
                float centerLinearDepth,
                float3 centerNormal,
                float centerRoughness)
            {
                float sampleRawDepth = BurtSampleDeferredRawDepth(sampleUV);
                if (BurtSSRCompositeIsSkyDepth(sampleRawDepth))
                {
                    return 0.0;
                }

                BurtGBufferData sampleGBuffer = BurtDecodeGBuffer(BurtSampleEncodedGBuffer(sampleUV));
                if (BurtIsActiveHairShadingModel(sampleGBuffer.shadingModelID))
                {
                    return 0.0;
                }

                float normalWeight = saturate(dot(centerNormal, BurtGetReflectionNormalWS(sampleGBuffer)));
                normalWeight *= normalWeight * normalWeight;
                float sampleLinearDepth = LinearEyeDepth(sampleRawDepth);
                float depthTolerance = max(centerLinearDepth * 0.012, 0.01);
                float depthWeight = exp2(-abs(sampleLinearDepth - centerLinearDepth) / depthTolerance);
                float roughnessWeight = exp2(-abs(BurtGetReflectionRoughness(sampleGBuffer) - centerRoughness) * 10.0);
                return saturate(normalWeight * depthWeight * depthWeight * roughnessWeight);
            }

            float BurtSSRCompositeSameSurfaceSupport(
                float2 sampleUV,
                float centerLinearDepth,
                float3 centerNormal,
                float depthTolerance)
            {
                if (!all(sampleUV >= 0.0) || !all(sampleUV <= 1.0))
                {
                    return 0.0;
                }

                float sampleRawDepth = BurtSampleDeferredRawDepth(sampleUV);
                if (BurtSSRCompositeIsSkyDepth(sampleRawDepth))
                {
                    return 0.0;
                }

                BurtGBufferData sampleGBuffer = BurtDecodeGBuffer(BurtSampleEncodedGBuffer(sampleUV));
                if (BurtIsActiveHairShadingModel(sampleGBuffer.shadingModelID))
                {
                    return 0.0;
                }

                float normalSupport = saturate(dot(centerNormal, BurtGetReflectionNormalWS(sampleGBuffer)));
                normalSupport *= normalSupport;
                float sampleLinearDepth = LinearEyeDepth(sampleRawDepth);
                float depthSupport = 1.0 - smoothstep(depthTolerance * 0.45, depthTolerance, abs(sampleLinearDepth - centerLinearDepth));
                return normalSupport * depthSupport;
            }

            float BurtSSRCompositeReceiverContinuityWeight(float2 screenUV)
            {
                float centerRawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtSSRCompositeIsSkyDepth(centerRawDepth))
                {
                    return 0.0;
                }

                float centerLinearDepth = LinearEyeDepth(centerRawDepth);
                BurtGBufferData centerGBuffer = BurtDecodeGBuffer(BurtSampleEncodedGBuffer(screenUV));
                if (BurtIsActiveHairShadingModel(centerGBuffer.shadingModelID))
                {
                    return 0.0;
                }

                float3 centerNormal = BurtGetReflectionNormalWS(centerGBuffer);
                float depthTolerance = max(centerLinearDepth * 0.014, 0.018);
                float2 texel = _BurtDeferredScreenSize.zw;
                float supportLeft = BurtSSRCompositeSameSurfaceSupport(screenUV - float2(texel.x, 0.0), centerLinearDepth, centerNormal, depthTolerance);
                float supportRight = BurtSSRCompositeSameSurfaceSupport(screenUV + float2(texel.x, 0.0), centerLinearDepth, centerNormal, depthTolerance);
                float supportDown = BurtSSRCompositeSameSurfaceSupport(screenUV - float2(0.0, texel.y), centerLinearDepth, centerNormal, depthTolerance);
                float supportUp = BurtSSRCompositeSameSurfaceSupport(screenUV + float2(0.0, texel.y), centerLinearDepth, centerNormal, depthTolerance);
                float supportNE = BurtSSRCompositeSameSurfaceSupport(screenUV + texel, centerLinearDepth, centerNormal, depthTolerance);
                float supportNW = BurtSSRCompositeSameSurfaceSupport(screenUV + float2(-texel.x, texel.y), centerLinearDepth, centerNormal, depthTolerance);
                float supportSE = BurtSSRCompositeSameSurfaceSupport(screenUV + float2(texel.x, -texel.y), centerLinearDepth, centerNormal, depthTolerance);
                float supportSW = BurtSSRCompositeSameSurfaceSupport(screenUV - texel, centerLinearDepth, centerNormal, depthTolerance);
                float axialCoverage = (supportLeft + supportRight + supportDown + supportUp) * 0.25;
                float fullCoverage = (supportLeft + supportRight + supportDown + supportUp + supportNE + supportNW + supportSE + supportSW) * 0.125;
                float pairedAxialSupport = max(min(supportLeft, supportRight), min(supportDown, supportUp));
                float pairedDiagonalSupport = max(min(supportNE, supportSW), min(supportNW, supportSE));
                float pairedSupport = max(pairedAxialSupport, pairedDiagonalSupport);
                float receiverCoverage = max(axialCoverage, fullCoverage);
                float coverageGate = smoothstep(0.34, 0.82, receiverCoverage);
                float pairedGate = smoothstep(0.12, 0.58, pairedSupport);
                float isolatedReject = smoothstep(0.18, 0.52, max(receiverCoverage, pairedSupport));
                return saturate(coverageGate * isolatedReject * lerp(0.45, 1.0, pairedGate));
            }

            float BurtSSRCompositeNeighborAlpha(
                float2 sampleUV,
                float centerLinearDepth,
                float3 centerNormal,
                float centerRoughness)
            {
                if (!all(sampleUV >= 0.0) || !all(sampleUV <= 1.0))
                {
                    return 0.0;
                }

                float alpha = tex2D(_BurtScreenSpaceReflectionTemporalColorTexture, sampleUV).a;
                return alpha * BurtSSRCompositeSurfaceWeight(sampleUV, centerLinearDepth, centerNormal, centerRoughness);
            }

            float2 BurtSSRCompositeTapOffset(int sampleIndex, float radius)
            {
                return sampleIndex == 0 ? float2(1.0, 0.0) * radius :
                    sampleIndex == 1 ? float2(-1.0, 0.0) * radius :
                    sampleIndex == 2 ? float2(0.0, 1.0) * radius :
                    sampleIndex == 3 ? float2(0.0, -1.0) * radius :
                    sampleIndex == 4 ? float2(1.0, 1.0) * radius :
                    sampleIndex == 5 ? float2(-1.0, 1.0) * radius :
                    sampleIndex == 6 ? float2(1.0, -1.0) * radius :
                    float2(-1.0, -1.0) * radius;
            }

            float BurtSSRComputeRoughnessMipFromRoughness(float perceptualRoughness)
            {
                float pureSpecularRoughness = 0.06;
                float roughnessRange = max(_BurtSSRParams0.w - pureSpecularRoughness, 0.0001);
                float maxMip = max(min(_BurtSSRParams1.y, 4.0), 0.0);
                float roughnessMip = perceptualRoughness < pureSpecularRoughness ? 0.0 : saturate((perceptualRoughness - pureSpecularRoughness) / roughnessRange) * maxMip;
                return clamp(roughnessMip, 0.0, maxMip);
            }

            float BurtSSRComputeRoughnessMip(float2 screenUV)
            {
                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtSSRCompositeIsSkyDepth(rawDepth))
                {
                    return 0.0;
                }

                BurtGBufferData gbufferData = BurtDecodeGBuffer(BurtSampleEncodedGBuffer(screenUV));
                if (BurtIsActiveHairShadingModel(gbufferData.shadingModelID))
                {
                    return 0.0;
                }

                return BurtSSRComputeRoughnessMipFromRoughness(BurtGetReflectionRoughness(gbufferData));
            }

            float BurtSSRCompositeVarianceGate(float2 screenUV)
            {
                float4 moment = tex2D(_BurtSSRHistoryMomentTexture, screenUV);
                float historyLength = moment.a * BurtSSRCompositeHistoryMax;
                float variance = max(moment.b, moment.g - moment.r * moment.r);
                float varianceSigma = sqrt(max(variance, 0.0));
                float highVarianceGate = smoothstep(0.025, 0.22, varianceSigma);
                float shortHistoryGate = 1.0 - smoothstep(4.0, 10.0, historyLength);
                return highVarianceGate * lerp(0.35, 1.0, shortHistoryGate);
            }

            float BurtSSRCompositeEdgeFade(float2 uv)
            {
                float2 edgeDistance = min(uv, 1.0 - uv);
                float edgeFadeWidth = max(_BurtSSRParams1.w, 0.0001);
                float fade = saturate(min(edgeDistance.x, edgeDistance.y) / edgeFadeWidth);
                return fade * fade * (3.0 - 2.0 * fade);
            }

            float4 BurtSSRCompositeSampleTemporalUpscaled(float2 screenUV)
            {
                float4 centerSSR = tex2D(_BurtScreenSpaceReflectionTemporalColorTexture, screenUV);
                if (_BurtSSRSourceTexelSize.z >= _BurtDeferredScreenSize.x * 0.75 ||
                    _BurtSSRSourceTexelSize.w >= _BurtDeferredScreenSize.y * 0.75)
                {
                    return centerSSR;
                }

                float fullRawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtSSRCompositeIsSkyDepth(fullRawDepth))
                {
                    return centerSSR;
                }

                float fullLinearDepth = LinearEyeDepth(fullRawDepth);
                float2 halfPixel = screenUV * _BurtSSRSourceTexelSize.zw - 0.5;
                float2 baseHalfPixel = floor(halfPixel);
                float2 fractionalHalfPixel = saturate(halfPixel - baseHalfPixel);
                float4 spatialWeights = float4(
                    (1.0 - fractionalHalfPixel.x) * (1.0 - fractionalHalfPixel.y),
                    fractionalHalfPixel.x * (1.0 - fractionalHalfPixel.y),
                    (1.0 - fractionalHalfPixel.x) * fractionalHalfPixel.y,
                    fractionalHalfPixel.x * fractionalHalfPixel.y);
                float2 halfMaxPixel = max(_BurtSSRSourceTexelSize.zw - 1.0, 0.0);
                float depthTolerance = max(fullLinearDepth * 0.018, 0.035);
                float3 accumulatedColor = 0.0;
                float accumulatedAlpha = 0.0;
                float totalWeight = 0.0;

                [unroll]
                for (int sampleIndex = 0; sampleIndex < 4; sampleIndex++)
                {
                    float2 sampleOffset = float2(sampleIndex == 1 || sampleIndex == 3 ? 1.0 : 0.0, sampleIndex >= 2 ? 1.0 : 0.0);
                    float2 samplePixel = clamp(baseHalfPixel + sampleOffset, 0.0, halfMaxPixel);
                    float2 sampleUV = (samplePixel + 0.5) * _BurtSSRSourceTexelSize.xy;
                    float sampleRawDepth = BurtSampleDeferredRawDepth(sampleUV);
                    if (BurtSSRCompositeIsSkyDepth(sampleRawDepth))
                    {
                        continue;
                    }

                    float sampleLinearDepth = LinearEyeDepth(sampleRawDepth);
                    float depthWeight = rcp(abs(sampleLinearDepth - fullLinearDepth) + depthTolerance);
                    float spatialWeight = sampleIndex == 0 ? spatialWeights.x :
                        sampleIndex == 1 ? spatialWeights.y :
                        sampleIndex == 2 ? spatialWeights.z :
                        spatialWeights.w;
                    float4 sampleSSR = tex2Dlod(_BurtScreenSpaceReflectionTemporalColorTexture, float4(sampleUV, 0.0, 0.0));
                    float sampleAlpha = saturate(sampleSSR.a);
                    float confidenceWeight = smoothstep(0.002, 0.08, sampleAlpha);
                    float weight = spatialWeight * depthWeight * confidenceWeight;
                    accumulatedColor += sampleSSR.rgb * sampleAlpha * weight;
                    accumulatedAlpha += sampleAlpha * weight;
                    totalWeight += weight;
                }

                if (totalWeight <= 0.0001)
                {
                    return centerSSR;
                }

                float3 upscaledColor = accumulatedAlpha > 0.0001 ? accumulatedColor / accumulatedAlpha : centerSSR.rgb;
                float centerLock = smoothstep(0.12, 0.35, saturate(centerSSR.a));
                float3 resolvedColor = lerp(upscaledColor, centerSSR.rgb, centerLock * 0.35);
                return float4(resolvedColor, centerSSR.a);
            }

            float3 BurtSSRResolveCompositeColor(float2 screenUV, float4 centerSSR, float resolvedVisibility)
            {
                float centerRawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtSSRCompositeIsSkyDepth(centerRawDepth))
                {
                    return centerSSR.rgb;
                }

                BurtGBufferData centerGBuffer = BurtDecodeGBuffer(BurtSampleEncodedGBuffer(screenUV));
                if (BurtIsActiveHairShadingModel(centerGBuffer.shadingModelID))
                {
                    return centerSSR.rgb;
                }

                float centerLinearDepth = LinearEyeDepth(centerRawDepth);
                float3 centerNormal = BurtGetReflectionNormalWS(centerGBuffer);
                float centerRoughness = BurtGetReflectionRoughness(centerGBuffer);
                float centerAlpha = saturate(centerSSR.a);
                float centerLuminance = BurtSSRCompositeLuminance(centerSSR.rgb);
                float pureSpecularRoughness = 0.06;
                float roughnessGate = smoothstep(pureSpecularRoughness, max(_BurtSSRParams0.w, pureSpecularRoughness + 0.0001), centerRoughness);
                float roughnessMip = BurtSSRComputeRoughnessMipFromRoughness(centerRoughness);
                float4 mipSSR = tex2Dlod(_BurtScreenSpaceReflectionTemporalColorTexture, float4(screenUV, 0.0, roughnessMip));
                float mipAlpha = saturate(mipSSR.a);
                float holeGate = smoothstep(0.01, 0.08, saturate(resolvedVisibility - centerAlpha));
                float varianceGate = BurtSSRCompositeVarianceGate(screenUV) * smoothstep(0.02, 0.16, resolvedVisibility);
                float tapGate = max(max(holeGate, roughnessGate), varianceGate);
                if (tapGate <= 0.0001)
                {
                    return centerSSR.rgb;
                }

                float roughnessRadius = max(
                    max(lerp(1.5, 5.0, roughnessGate * roughnessGate), lerp(1.0, 2.0, holeGate)),
                    lerp(1.0, 2.75, varianceGate));
                float centerReliability = smoothstep(0.015, 0.12, centerAlpha);
                float centerWeight = lerp(centerAlpha, max(centerAlpha, 0.35), (1.0 - holeGate) * centerReliability);
                centerWeight *= lerp(1.0, 0.65, varianceGate * max(roughnessGate, 0.35));
                float3 accumulatedColor = centerSSR.rgb * centerWeight;
                float totalWeight = centerWeight;
                float2 texel = _BurtSSRSourceTexelSize.xy;
                float sampleRadius = lerp(1.0, roughnessRadius, saturate(tapGate));

                [unroll(8)]
                for (int sampleIndex = 0; sampleIndex < 8; sampleIndex++)
                {
                    float2 sampleUV = screenUV + BurtSSRCompositeTapOffset(sampleIndex, sampleRadius) * texel;
                    if (!all(sampleUV >= 0.0) || !all(sampleUV <= 1.0))
                    {
                        continue;
                    }

                    float4 sampleSSR = tex2D(_BurtScreenSpaceReflectionTemporalColorTexture, sampleUV);
                    float surfaceWeight = BurtSSRCompositeSurfaceWeight(sampleUV, centerLinearDepth, centerNormal, centerRoughness);
                    float sampleAlpha = saturate(sampleSSR.a);
                    if (sampleAlpha <= 0.002)
                    {
                        continue;
                    }

                    float alphaWeight = smoothstep(0.003, 0.08, sampleAlpha);
                    float luminanceWeight = BurtSSRCompositeLuminanceWeight(
                        centerLuminance,
                        BurtSSRCompositeLuminance(sampleSSR.rgb),
                        centerAlpha,
                        roughnessGate,
                        holeGate,
                        varianceGate);
                    float diagonalWeight = (sampleIndex % 8) < 4 ? 1.0 : 0.7071;
                    float weight = tapGate * diagonalWeight * surfaceWeight * luminanceWeight * alphaWeight * sampleAlpha;
                    accumulatedColor += sampleSSR.rgb * weight;
                    totalWeight += weight;
                }

                float3 filteredColor = accumulatedColor / max(totalWeight, 0.0001);
                float mipBlend = roughnessGate * smoothstep(0.04, 0.18, resolvedVisibility) * (1.0 - holeGate * 0.65);
                float mipCoverage = mipAlpha / max(resolvedVisibility, 0.05);
                float mipValidity = smoothstep(0.16, 0.45, mipAlpha) * smoothstep(0.55, 0.92, mipCoverage);
                filteredColor = lerp(filteredColor, mipSSR.rgb, mipBlend * 0.75 * mipValidity);
                float mirrorLock = (1.0 - roughnessGate) * smoothstep(0.05, 0.2, centerAlpha) * (1.0 - holeGate);
                mirrorLock *= 1.0 - varianceGate * roughnessGate * 0.5;
                return lerp(filteredColor, centerSSR.rgb, mirrorLock);
            }

            float BurtSSRResolveCompositeAlpha(float2 screenUV, float centerAlpha)
            {
                float centerRawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtSSRCompositeIsSkyDepth(centerRawDepth))
                {
                    return 0.0;
                }

                BurtGBufferData centerGBuffer = BurtDecodeGBuffer(BurtSampleEncodedGBuffer(screenUV));
                float centerLinearDepth = LinearEyeDepth(centerRawDepth);
                float3 centerNormal = BurtGetReflectionNormalWS(centerGBuffer);
                float centerRoughness = BurtGetReflectionRoughness(centerGBuffer);
                float2 texel = _BurtSSRSourceTexelSize.xy;
                float alphaRight = BurtSSRCompositeNeighborAlpha(screenUV + float2(texel.x, 0.0), centerLinearDepth, centerNormal, centerRoughness);
                float alphaLeft = BurtSSRCompositeNeighborAlpha(screenUV - float2(texel.x, 0.0), centerLinearDepth, centerNormal, centerRoughness);
                float alphaUp = BurtSSRCompositeNeighborAlpha(screenUV + float2(0.0, texel.y), centerLinearDepth, centerNormal, centerRoughness);
                float alphaDown = BurtSSRCompositeNeighborAlpha(screenUV - float2(0.0, texel.y), centerLinearDepth, centerNormal, centerRoughness);
                float alphaNE = BurtSSRCompositeNeighborAlpha(screenUV + float2(texel.x, texel.y), centerLinearDepth, centerNormal, centerRoughness);
                float alphaNW = BurtSSRCompositeNeighborAlpha(screenUV + float2(-texel.x, texel.y), centerLinearDepth, centerNormal, centerRoughness);
                float alphaSE = BurtSSRCompositeNeighborAlpha(screenUV + float2(texel.x, -texel.y), centerLinearDepth, centerNormal, centerRoughness);
                float alphaSW = BurtSSRCompositeNeighborAlpha(screenUV + float2(-texel.x, -texel.y), centerLinearDepth, centerNormal, centerRoughness);
                float horizontalSupport = max(alphaLeft, alphaRight);
                float verticalSupport = max(alphaUp, alphaDown);
                float surroundedSupport = min(horizontalSupport, verticalSupport);
                float diagonalSupport = max(min(alphaNE, alphaSW), min(alphaNW, alphaSE));
                float twoDimensionalSupport = max(surroundedSupport, diagonalSupport);
                float axialSupport = max(horizontalSupport, verticalSupport);
                float bridgeSupport = max(max(min(alphaLeft, alphaRight), min(alphaUp, alphaDown)), diagonalSupport);
                float varianceGate = BurtSSRCompositeVarianceGate(screenUV);
                float edgeFade = BurtSSRCompositeEdgeFade(screenUV);
                float bridgeGate = smoothstep(0.012, 0.11, bridgeSupport) * (1.0 - smoothstep(0.025, 0.14, centerAlpha));
                bridgeGate *= lerp(1.0, 0.55, varianceGate);
                bridgeGate *= smoothstep(0.24, 0.85, edgeFade);
                float resolvedCenterAlpha = max(centerAlpha, bridgeSupport * bridgeGate * 0.82);
                float strongAlphaGate = smoothstep(0.04, 0.16, resolvedCenterAlpha);
                float support = lerp(twoDimensionalSupport, max(twoDimensionalSupport, axialSupport * 0.6), strongAlphaGate);
                float supportGate = smoothstep(0.004, 0.04, support);
                float lowAlphaGate = smoothstep(0.001, 0.006, resolvedCenterAlpha);
                float isolatedFade = lerp(0.25 + supportGate * 0.75, 1.0, strongAlphaGate);
                isolatedFade *= lerp(1.0, 0.85, varianceGate * (1.0 - strongAlphaGate));
                float centerHitGate = smoothstep(0.02, 0.12, centerAlpha);
                float edgeAlphaFade = lerp(edgeFade * edgeFade, edgeFade, centerHitGate);
                return saturate(resolvedCenterAlpha * lowAlphaGate * isolatedFade * edgeAlphaFade);
            }

            float BurtSSRComputeMaterialVisibilityWeight(float reflectionRoughness)
            {
                float roughnessFade = saturate((_BurtSSRParams0.w - reflectionRoughness) / max(_BurtSSRParams0.w, 0.0001));
                return roughnessFade * saturate(_BurtSSRParams0.z);
            }

            float BurtSSRComputeCompositeFallbackAlphaScale(float2 screenUV, float resolvedVisibility, float centerAlpha, float materialWeight)
            {
                float edgeFade = BurtSSRCompositeEdgeFade(screenUV);
                float roughnessMip = BurtSSRComputeRoughnessMip(screenUV);
                float roughnessMipMax = max(min(_BurtSSRParams1.y, 4.0), 0.0001);
                float roughnessFallback = smoothstep(0.65, 1.0, roughnessMip / roughnessMipMax);
                float lowValidityFallback = 1.0 - smoothstep(0.03, 0.18, resolvedVisibility);
                float centerHoleFallback = smoothstep(0.01, 0.08, saturate(resolvedVisibility - centerAlpha));
                centerHoleFallback *= (1.0 - smoothstep(0.04, 0.16, centerAlpha)) * 0.35;
                float edgeFallback = 1.0 - smoothstep(0.18, 0.85, edgeFade);
                float materialFallback = 1.0 - saturate(materialWeight);
                float fallback = saturate(max(max(roughnessFallback, lowValidityFallback), max(max(centerHoleFallback, edgeFallback), materialFallback)));
                float confidence = saturate(1.0 - fallback);
                return confidence * confidence * (3.0 - 2.0 * confidence);
            }

            float BurtSSRCompositeDarkHaloGate(
                float2 screenUV,
                float4 centerSSR,
                float3 resolvedColor,
                float3 fallbackSpecular,
                float3 materialSpecularScale,
                float resolvedAlpha,
                float receiverContinuityWeight)
            {
                float strongestDarkDelta = max(max(fallbackSpecular.r - resolvedColor.r, fallbackSpecular.g - resolvedColor.g), fallbackSpecular.b - resolvedColor.b);
                float darkReplaceGate = smoothstep(0.006, 0.06, strongestDarkDelta) * smoothstep(0.015, 0.14, resolvedAlpha);
                if (darkReplaceGate <= 0.0001)
                {
                    return 1.0;
                }

                float centerRawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtSSRCompositeIsSkyDepth(centerRawDepth))
                {
                    return 1.0;
                }

                BurtGBufferData centerGBuffer = BurtDecodeGBuffer(BurtSampleEncodedGBuffer(screenUV));
                float centerLinearDepth = LinearEyeDepth(centerRawDepth);
                float3 centerNormal = BurtGetReflectionNormalWS(centerGBuffer);
                float centerRoughness = BurtGetReflectionRoughness(centerGBuffer);
                float centerLuminance = BurtSSRCompositeLuminance(resolvedColor);
                float neighborMaxLuminance = centerLuminance;
                float brightNeighborWeight = 0.0;
                float alphaSurfaceSupport = 0.0;
                float2 texel = _BurtSSRSourceTexelSize.xy;

                [unroll(8)]
                for (int sampleIndex = 0; sampleIndex < 8; sampleIndex++)
                {
                    float2 sampleUV = screenUV + BurtSSRCompositeTapOffset(sampleIndex, 1.0) * texel;
                    if (!all(sampleUV >= 0.0) || !all(sampleUV <= 1.0))
                    {
                        continue;
                    }

                    float surfaceWeight = BurtSSRCompositeSurfaceWeight(sampleUV, centerLinearDepth, centerNormal, centerRoughness);
                    float4 sampleSSR = tex2D(_BurtScreenSpaceReflectionTemporalColorTexture, sampleUV);
                    float sampleAlphaWeight = smoothstep(0.015, 0.12, saturate(sampleSSR.a));
                    float sampleLuminance = BurtSSRCompositeLuminance(sampleSSR.rgb * materialSpecularScale);
                    float brighterWeight = smoothstep(0.025, 0.16, sampleLuminance - centerLuminance);
                    float sampleWeight = surfaceWeight * sampleAlphaWeight * (sampleIndex < 4 ? 1.0 : 0.7071);
                    neighborMaxLuminance = max(neighborMaxLuminance, lerp(centerLuminance, sampleLuminance, sampleWeight));
                    brightNeighborWeight += sampleWeight * brighterWeight;
                    alphaSurfaceSupport += sampleWeight;
                }

                float relativeDarkness = smoothstep(0.08, 0.38, (neighborMaxLuminance - centerLuminance) / max(neighborMaxLuminance, 0.035));
                float brightCoverage = smoothstep(0.08, 0.75, brightNeighborWeight);
                float supportedReceiver = smoothstep(0.32, 0.82, receiverContinuityWeight) * smoothstep(0.18, 1.2, alphaSurfaceSupport);
                float haloRisk = saturate(darkReplaceGate * relativeDarkness * brightCoverage * supportedReceiver);
                return 1.0 - haloRisk;
            }

            float BurtSSRCompositeDarkSilhouetteSuppress(
                float2 screenUV,
                inout float3 resolvedColor,
                float3 materialSpecularScale,
                float resolvedAlpha,
                float materialWeight,
                float receiverContinuityWeight)
            {
                if (resolvedAlpha <= 0.002 || materialWeight <= 0.0001)
                {
                    return 0.0;
                }

                float centerRawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtSSRCompositeIsSkyDepth(centerRawDepth))
                {
                    return 0.0;
                }

                BurtGBufferData centerGBuffer = BurtDecodeGBuffer(BurtSampleEncodedGBuffer(screenUV));
                float centerRoughness = BurtGetReflectionRoughness(centerGBuffer);
                float receiverMaterialGate = smoothstep(0.04, 0.28, materialWeight) * (1.0 - smoothstep(0.42, 0.82, centerRoughness));
                if (receiverMaterialGate <= 0.0001)
                {
                    return 0.0;
                }

                float centerLinearDepth = LinearEyeDepth(centerRawDepth);
                float3 centerNormal = BurtGetReflectionNormalWS(centerGBuffer);
                float centerLuminance = BurtSSRCompositeLuminance(resolvedColor);
                float neighborMaxLuminance = centerLuminance;
                float3 brightColorSum = 0.0;
                float brightNeighborWeight = 0.0;
                float alphaSupport = 0.0;
                float2 texel = _BurtSSRSourceTexelSize.xy;

                [unroll]
                for (int sampleIndex = 0; sampleIndex < 12; sampleIndex++)
                {
                    float radius = sampleIndex < 8 ? 1.0 : 2.0;
                    float2 sampleUV = screenUV + BurtSSRCompositeTapOffset(sampleIndex % 8, radius) * texel;
                    if (!all(sampleUV >= 0.0) || !all(sampleUV <= 1.0))
                    {
                        continue;
                    }

                    float surfaceWeight = BurtSSRCompositeSurfaceWeight(sampleUV, centerLinearDepth, centerNormal, centerRoughness);
                    if (surfaceWeight <= 0.015)
                    {
                        continue;
                    }

                    float4 sampleSSR = tex2D(_BurtScreenSpaceReflectionTemporalColorTexture, sampleUV);
                    float sampleAlphaWeight = smoothstep(0.01, 0.10, saturate(sampleSSR.a));
                    float sampleWeight = surfaceWeight * sampleAlphaWeight * (sampleIndex < 4 ? 1.0 : sampleIndex < 8 ? 0.72 : 0.42);
                    float sampleLuminance = BurtSSRCompositeLuminance(sampleSSR.rgb * materialSpecularScale);
                    float brighterWeight = smoothstep(0.025, 0.18, sampleLuminance - centerLuminance);
                    neighborMaxLuminance = max(neighborMaxLuminance, lerp(centerLuminance, sampleLuminance, sampleWeight));
                    brightColorSum += sampleSSR.rgb * materialSpecularScale * sampleWeight * brighterWeight;
                    brightNeighborWeight += sampleWeight * brighterWeight;
                    alphaSupport += sampleWeight;
                }

                float relativeDarkHole = smoothstep(0.035, 0.24, (neighborMaxLuminance - centerLuminance) / max(neighborMaxLuminance, 0.035));
                float darkCore = 1.0 - smoothstep(0.35, 0.82, centerLuminance / max(neighborMaxLuminance, 0.035));
                float brightCoverage = smoothstep(0.018, 0.22, brightNeighborWeight);
                float alphaGate = smoothstep(0.004, 0.06, resolvedAlpha);
                float supportGate = smoothstep(0.015, 0.38, alphaSupport);
                float continuityGate = lerp(0.85, 1.0, smoothstep(0.05, 0.45, receiverContinuityWeight));
                float suppress = saturate(relativeDarkHole * darkCore * brightCoverage * alphaGate * supportGate * receiverMaterialGate * continuityGate);
                if (brightNeighborWeight > 0.0001)
                {
                    float3 brightFillColor = brightColorSum / max(brightNeighborWeight, 0.0001);
                    resolvedColor = lerp(resolvedColor, max(resolvedColor, brightFillColor * 0.98), suppress);
                }

                return suppress;
            }

            float BurtSSRComputeMaterialWeight(float2 screenUV)
            {
                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtSSRCompositeIsSkyDepth(rawDepth))
                {
                    return 0.0;
                }

                BurtGBufferData gbufferData = BurtDecodeGBuffer(BurtSampleEncodedGBuffer(screenUV));
                if (BurtIsActiveHairShadingModel(gbufferData.shadingModelID))
                {
                    return 0.0;
                }

                float reflectionRoughness = BurtGetReflectionRoughness(gbufferData);
                return BurtSSRComputeMaterialVisibilityWeight(reflectionRoughness);
            }

            float3 BurtSSRComputeBaseMaterialSpecularScale(BurtPBRMaterialData materialData, float reflectionRoughness, float nDotV)
            {
                float2 dfg = GetSpecularDFGTerms(reflectionRoughness, nDotV);
                float3 envBRDF = EvalSpecularDFG(materialData.f0, materialData.f90, dfg);
                float3 energyCompensation = GetSpecularEnergyCompensation(materialData.f0, reflectionRoughness, nDotV);
                float specularOcclusion = GetIndirectSpecularOcclusion(nDotV, materialData.occlusion, reflectionRoughness);
                return envBRDF * energyCompensation * specularOcclusion;
            }

            float3 BurtSSRComputeMaterialSpecularScale(float2 screenUV)
            {
                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtSSRCompositeIsSkyDepth(rawDepth))
                {
                    return float3(0.0, 0.0, 0.0);
                }

                BurtGBufferData gbufferData = BurtDecodeGBuffer(BurtSampleEncodedGBuffer(screenUV));
                if (BurtIsActiveHairShadingModel(gbufferData.shadingModelID))
                {
                    return float3(0.0, 0.0, 0.0);
                }

                float reflectionRoughness = BurtGetReflectionRoughness(gbufferData);
                if (BurtSSRComputeMaterialVisibilityWeight(reflectionRoughness) <= 0.0)
                {
                    return float3(0.0, 0.0, 0.0);
                }

                float3 positionWS = BurtReconstructDeferredPositionWS(screenUV, rawDepth);
                float3 viewDirectionWS = BurtSafeNormalize(_BurtDeferredCameraWorldPosition.xyz - positionWS);
                float3 baseNormalWS = BurtSafeNormalize(gbufferData.normalWS);
                float baseRoughness = ClampPerceptualRoughness(gbufferData.perceptualRoughness);
                float baseNoV = saturate(dot(baseNormalWS, viewDirectionWS));
                BurtPBRMaterialData materialData = BurtPreparePBRMaterialData(gbufferData);
                float3 baseSpecularScale = BurtSSRComputeBaseMaterialSpecularScale(materialData, baseRoughness, baseNoV);

                #if BURT_ENABLE_CLEAR_COAT_SHADING
                    float clearCoatMask = saturate(materialData.clearCoatMask);
                    if (clearCoatMask > 0.0001)
                    {
                        BurtPBRMaterialData clearCoatMaterialData = materialData;
                        clearCoatMaterialData.baseColor = float3(1.0, 1.0, 1.0);
                        clearCoatMaterialData.metallic = 0.0;
                        clearCoatMaterialData.anisotropy = 0.0;
                        clearCoatMaterialData.reflectance = BURT_INPUT_DEFAULT_REFLECTANCE;
                        clearCoatMaterialData.diffuseColor = float3(0.0, 0.0, 0.0);
                        clearCoatMaterialData.f0 = float3(BURT_CLEAR_COAT_F0, BURT_CLEAR_COAT_F0, BURT_CLEAR_COAT_F0);
                        clearCoatMaterialData.f90 = float3(1.0, 1.0, 1.0);
                        clearCoatMaterialData.perceptualRoughness = ClampPerceptualRoughness(materialData.clearCoatRoughness);
                        clearCoatMaterialData.linearRoughness = PerceptualRoughnessToLinearRoughness(clearCoatMaterialData.perceptualRoughness);
                        clearCoatMaterialData.a2 = LinearRoughnessToA2(clearCoatMaterialData.linearRoughness);
                        clearCoatMaterialData.clearCoatMask = 0.0;
                        float3 clearCoatNormalWS = BurtGetClearCoatNormalWS(gbufferData);
                        float clearCoatNoV = saturate(dot(clearCoatNormalWS, viewDirectionWS));
                        float2 clearCoatDFG = GetSpecularDFGTerms(clearCoatMaterialData.perceptualRoughness, clearCoatNoV);
                        float3 clearCoatEnvBRDF = EvalSpecularDFG(clearCoatMaterialData.f0, clearCoatMaterialData.f90, clearCoatDFG);
                        float3 clearCoatEnergyCompensation = GetSpecularEnergyCompensation(clearCoatMaterialData.f0, clearCoatMaterialData.perceptualRoughness, clearCoatNoV);
                        float clearCoatSpecularOcclusion = GetIndirectSpecularOcclusion(clearCoatNoV, clearCoatMaterialData.occlusion, clearCoatMaterialData.perceptualRoughness);
                        float3 clearCoatSpecularScale = clearCoatEnvBRDF * clearCoatEnergyCompensation * clearCoatSpecularOcclusion;
                        float3 layerTransmission = BurtClearCoatFresnelTransmission(clearCoatEnvBRDF) * BurtSimpleClearCoatTransmittanceFromView(clearCoatNoV, materialData.metallic, materialData.baseColor);
                        return lerp(baseSpecularScale, baseSpecularScale * layerTransmission + clearCoatSpecularScale, clearCoatMask);
                    }
                #endif

                return baseSpecularScale;
            }

            float3 BurtSSRComputeCameraIBLSpecularFallback(float2 screenUV)
            {
                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtSSRCompositeIsSkyDepth(rawDepth))
                {
                    return float3(0.0, 0.0, 0.0);
                }

                BurtGBufferData gbufferData = BurtDecodeGBuffer(BurtSampleEncodedGBuffer(screenUV));
                float3 positionWS = BurtReconstructDeferredPositionWS(screenUV, rawDepth);
                float3 viewDirectionWS = BurtSafeNormalize(_BurtDeferredCameraWorldPosition.xyz - positionWS);
                BurtPBRShadingCoreData coreData = BurtPreparePBRShadingCoreData(gbufferData, viewDirectionWS);
                BurtIndirectPBRComponents indirectComponents = BurtEvaluatePBRIndirectFromCore(coreData);
                return max(indirectComponents.specular, float3(0.0, 0.0, 0.0));
            }

            float3 BurtSSRCompositeFillPositiveHole(
                float2 screenUV,
                float3 compositeDelta,
                float materialWeight,
                float receiverContinuityWeight)
            {
                if (materialWeight <= 0.0001)
                {
                    return compositeDelta;
                }

                float centerRawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtSSRCompositeIsSkyDepth(centerRawDepth))
                {
                    return compositeDelta;
                }

                BurtGBufferData centerGBuffer = BurtDecodeGBuffer(BurtSampleEncodedGBuffer(screenUV));
                if (BurtIsActiveHairShadingModel(centerGBuffer.shadingModelID))
                {
                    return compositeDelta;
                }

                float centerRoughness = BurtGetReflectionRoughness(centerGBuffer);
                float receiverMaterialGate = smoothstep(0.03, 0.22, materialWeight) * (1.0 - smoothstep(0.38, 0.78, centerRoughness));
                if (receiverMaterialGate <= 0.0001)
                {
                    return compositeDelta;
                }

                float centerLinearDepth = LinearEyeDepth(centerRawDepth);
                float3 centerNormal = BurtGetReflectionNormalWS(centerGBuffer);
                float centerLuminance = BurtSSRCompositeLuminance(compositeDelta);
                float3 brightDeltaSum = 0.0;
                float brightWeightSum = 0.0;
                float strongestNeighborLuminance = centerLuminance;
                float surfaceSupportSum = 0.0;
                float2 texel = _BurtSSRSourceTexelSize.xy;

                [loop]
                for (int sampleIndex = 0; sampleIndex < 8; sampleIndex++)
                {
                    float2 sampleUV = screenUV + BurtSSRCompositeTapOffset(sampleIndex, 1.0) * texel;
                    if (!all(sampleUV >= 0.0) || !all(sampleUV <= 1.0))
                    {
                        continue;
                    }

                    float surfaceWeight = BurtSSRCompositeSurfaceWeight(sampleUV, centerLinearDepth, centerNormal, centerRoughness);
                    if (surfaceWeight <= 0.012)
                    {
                        continue;
                    }

                    float4 sampleSSR = BurtSSRCompositeSampleTemporalUpscaled(sampleUV);
                    float sampleReceiverContinuity = BurtSSRCompositeReceiverContinuityWeight(sampleUV);
                    float sampleReceiverAlphaGate = lerp(0.78, 1.0, smoothstep(0.04, 0.42, sampleReceiverContinuity));
                    float sampleCenterAlpha = saturate(sampleSSR.a) * sampleReceiverAlphaGate;
                    float sampleResolvedVisibility = BurtSSRResolveCompositeAlpha(sampleUV, sampleCenterAlpha) * sampleReceiverAlphaGate;
                    float3 sampleSpecularScale = BurtSSRComputeMaterialSpecularScale(sampleUV);
                    float3 sampleResolvedColor = BurtSSRResolveCompositeColor(sampleUV, sampleSSR, sampleResolvedVisibility) * sampleSpecularScale;
                    float3 sampleFallbackSpecular = BurtSSRComputeCameraIBLSpecularFallback(sampleUV);
                    float sampleMaterialWeight = BurtSSRComputeMaterialWeight(sampleUV);
                    float sampleFallbackAlphaScale = BurtSSRComputeCompositeFallbackAlphaScale(sampleUV, sampleResolvedVisibility, sampleCenterAlpha, sampleMaterialWeight);
                    float sampleResolvedAlpha = saturate(sampleResolvedVisibility * sampleMaterialWeight * sampleFallbackAlphaScale);
                    float3 sampleDelta = max(sampleResolvedColor - sampleFallbackSpecular, 0.0) * sampleResolvedAlpha;
                    float sampleLuminance = BurtSSRCompositeLuminance(sampleDelta);
                    float brightWeight = smoothstep(0.006, 0.065, sampleLuminance - centerLuminance) * surfaceWeight * (sampleIndex < 4 ? 1.0 : 0.68);

                    brightDeltaSum += sampleDelta * brightWeight;
                    brightWeightSum += brightWeight;
                    strongestNeighborLuminance = max(strongestNeighborLuminance, lerp(centerLuminance, sampleLuminance, surfaceWeight));
                    surfaceSupportSum += surfaceWeight;
                }

                if (brightWeightSum <= 0.0001)
                {
                    return compositeDelta;
                }

                float relativeHole = smoothstep(0.025, 0.16, (strongestNeighborLuminance - centerLuminance) / max(strongestNeighborLuminance, 0.025));
                float weakCenter = 1.0 - smoothstep(0.22, 0.78, centerLuminance / max(strongestNeighborLuminance, 0.025));
                float supportGate = smoothstep(0.12, 0.9, surfaceSupportSum);
                float continuityGate = lerp(0.7, 1.0, smoothstep(0.04, 0.42, receiverContinuityWeight));
                float fillGate = saturate(relativeHole * weakCenter * smoothstep(0.018, 0.28, brightWeightSum) * supportGate * receiverMaterialGate * continuityGate) * 0.45;
                float3 fillDelta = brightDeltaSum / max(brightWeightSum, 0.0001);
                return lerp(compositeDelta, max(compositeDelta, fillDelta * 0.72), fillGate);
            }

            float3 BurtSSRProtectCompositeOutputDarkSeam(
                float2 screenUV,
                float3 sourceColor,
                float3 finalColor,
                float3 ssrDelta,
                float resolvedAlpha,
                float materialWeight,
                float receiverContinuityWeight)
            {
                if (materialWeight <= 0.0001)
                {
                    return finalColor;
                }

                float sourceLuminance = BurtSSRCompositeLuminance(sourceColor);
                float finalLuminance = BurtSSRCompositeLuminance(finalColor);
                float sourceDarkening = sourceLuminance - finalLuminance;
                float darkFromComposite =
                    smoothstep(0.006, 0.055, sourceDarkening) *
                    smoothstep(0.08, 0.32, sourceDarkening / max(sourceLuminance, 0.035));
                if (darkFromComposite <= 0.0001)
                {
                    return finalColor;
                }

                float centerRawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtSSRCompositeIsSkyDepth(centerRawDepth))
                {
                    return finalColor;
                }

                BurtGBufferData centerGBuffer = BurtDecodeGBuffer(BurtSampleEncodedGBuffer(screenUV));
                float centerMetallic = saturate(centerGBuffer.metallic);
                float centerRoughness = BurtGetReflectionRoughness(centerGBuffer);
                float lowRoughnessReceiverGate = saturate(materialWeight) * (1.0 - smoothstep(0.34, 0.78, centerRoughness));
                float metallicReceiverGate = smoothstep(0.18, 0.72, centerMetallic) * (1.0 - smoothstep(0.42, 0.82, centerRoughness));
                float repairMaterialGate = max(lowRoughnessReceiverGate, metallicReceiverGate);
                if (repairMaterialGate <= 0.0001)
                {
                    return finalColor;
                }

                float ssrMagnitude = max(max(abs(ssrDelta.r), abs(ssrDelta.g)), abs(ssrDelta.b));
                float lowConfidenceGate = 1.0 - smoothstep(0.10, 0.42, max(resolvedAlpha, ssrMagnitude));
                float receiverEdgeGate = 1.0 - smoothstep(0.06, 0.38, receiverContinuityWeight);
                float directProtectGate = darkFromComposite * repairMaterialGate * max(
                    max(receiverEdgeGate, lowConfidenceGate * 0.75),
                    0.22);

                // Receiver edges can turn a valid specular replacement into a black outline.
                float3 sourceFloorColor = max(finalColor, sourceColor);
                finalColor = directProtectGate > 0.0001 ? sourceFloorColor : finalColor;

                float centerLinearDepth = LinearEyeDepth(centerRawDepth);
                float3 centerNormal = BurtGetReflectionNormalWS(centerGBuffer);
                float2 texel = _BurtSSRSourceTexelSize.xy;
                float3 brightColorSum = 0.0;
                float brightWeightSum = 0.0;
                float strongestNeighborLuminance = sourceLuminance;

                [unroll]
                for (int sampleIndex = 0; sampleIndex < 12; sampleIndex++)
                {
                    float radius = sampleIndex < 8 ? 1.0 : 2.0;
                    float2 sampleUV = screenUV + BurtSSRCompositeTapOffset(sampleIndex % 8, radius) * texel;
                    if (!all(sampleUV >= 0.0) || !all(sampleUV <= 1.0))
                    {
                        continue;
                    }

                    float surfaceWeight = BurtSSRCompositeSurfaceWeight(sampleUV, centerLinearDepth, centerNormal, centerRoughness);
                    if (surfaceWeight <= 0.02)
                    {
                        continue;
                    }

                    float3 sampleColor = tex2D(_BurtSSRCameraColorCopyTexture, sampleUV).rgb;
                    float sampleLuminance = BurtSSRCompositeLuminance(sampleColor);
                    float brighterWeight = smoothstep(0.015, 0.12, sampleLuminance - finalLuminance);
                    float sampleWeight = surfaceWeight * brighterWeight * (sampleIndex < 4 ? 1.0 : sampleIndex < 8 ? 0.72 : 0.45);
                    brightColorSum += sampleColor * sampleWeight;
                    brightWeightSum += sampleWeight;
                    strongestNeighborLuminance = max(strongestNeighborLuminance, lerp(sourceLuminance, sampleLuminance, surfaceWeight));
                }

                float3 neighborColor = brightWeightSum > 0.0001 ? brightColorSum / brightWeightSum : sourceColor;
                float relativeDarkSeam = smoothstep(0.08, 0.38, (strongestNeighborLuminance - finalLuminance) / max(strongestNeighborLuminance, 0.035));
                float supportGate = max(smoothstep(0.06, 0.55, brightWeightSum), smoothstep(0.04, 0.18, sourceLuminance - finalLuminance));
                float continuitySupport = lerp(0.45, 1.0, smoothstep(0.08, 0.62, receiverContinuityWeight));
                float seamGate = darkFromComposite * relativeDarkSeam * supportGate * repairMaterialGate * continuitySupport;
                seamGate *= lerp(1.0, 0.55, smoothstep(0.08, 0.45, resolvedAlpha)) * lerp(0.65, 1.0, lowConfidenceGate);
                float3 protectedColor = max(finalColor, max(sourceColor, neighborColor * 0.85));
                return lerp(finalColor, protectedColor, saturate(seamGate));
            }

            float BurtSSRCompositeBrightOutlierSuppress(
                float2 screenUV,
                float3 compositeDelta,
                float resolvedAlpha,
                float materialWeight,
                float receiverContinuityWeight)
            {
                if (resolvedAlpha <= 0.0001 || materialWeight <= 0.0001)
                {
                    return 0.0;
                }

                float centerDeltaLuminance = BurtSSRCompositeLuminance(max(compositeDelta, 0.0));
                if (centerDeltaLuminance <= 0.0001)
                {
                    return 0.0;
                }

                float centerRawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtSSRCompositeIsSkyDepth(centerRawDepth))
                {
                    return 0.0;
                }

                BurtGBufferData centerGBuffer = BurtDecodeGBuffer(BurtSampleEncodedGBuffer(screenUV));
                float centerLinearDepth = LinearEyeDepth(centerRawDepth);
                float3 centerNormal = BurtGetReflectionNormalWS(centerGBuffer);
                float centerRoughness = BurtGetReflectionRoughness(centerGBuffer);
                float roughReceiverGate = smoothstep(0.08, 0.46, centerRoughness);
                if (roughReceiverGate <= 0.0001)
                {
                    return 0.0;
                }

                float2 texel = _BurtSSRSourceTexelSize.xy;
                float neighborDeltaLuminance = 0.0;
                float neighborWeightSum = 0.0;
                float alphaSupport = 0.0;

                [unroll(8)]
                for (int sampleIndex = 0; sampleIndex < 8; sampleIndex++)
                {
                    float2 sampleUV = screenUV + BurtSSRCompositeTapOffset(sampleIndex, 1.0) * texel;
                    if (!all(sampleUV >= 0.0) || !all(sampleUV <= 1.0))
                    {
                        continue;
                    }

                    float surfaceWeight = BurtSSRCompositeSurfaceWeight(sampleUV, centerLinearDepth, centerNormal, centerRoughness);
                    if (surfaceWeight <= 0.01)
                    {
                        continue;
                    }

                    float4 sampleSSR = BurtSSRCompositeSampleTemporalUpscaled(sampleUV);
                    float sampleAlpha = saturate(sampleSSR.a);
                    float sampleAlphaWeight = smoothstep(0.01, 0.12, sampleAlpha);
                    float3 sampleSpecularScale = BurtSSRComputeMaterialSpecularScale(sampleUV);
                    float3 sampleFallbackSpecular = BurtSSRComputeCameraIBLSpecularFallback(sampleUV);
                    float3 sampleDelta = max(sampleSSR.rgb * sampleSpecularScale - sampleFallbackSpecular, 0.0) * sampleAlpha;
                    float sampleWeight = surfaceWeight * sampleAlphaWeight * (sampleIndex < 4 ? 1.0 : 0.7071);
                    neighborDeltaLuminance += BurtSSRCompositeLuminance(sampleDelta) * sampleWeight;
                    neighborWeightSum += sampleWeight;
                    alphaSupport += sampleWeight * smoothstep(0.015, 0.14, sampleAlpha);
                }

                float averageNeighborLuminance = neighborWeightSum > 0.0001 ? neighborDeltaLuminance / neighborWeightSum : 0.0;
                float relativeSpike = (centerDeltaLuminance - averageNeighborLuminance) / max(centerDeltaLuminance, 0.035);
                float isolatedAlpha = 1.0 - smoothstep(0.08, 0.55, alphaSupport);
                float continuityRisk = 1.0 - smoothstep(0.18, 0.72, receiverContinuityWeight);
                float spikeGate = smoothstep(0.34, 0.78, relativeSpike);
                return saturate(spikeGate * isolatedAlpha * roughReceiverGate * lerp(0.45, 1.0, continuityRisk));
            }

            float4 FragComposite(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenUV;
                int debugMode = (int)_BurtSSRParams1.z;

                bool compositeDebugMode = (debugMode >= 10 && debugMode <= 15) || (debugMode >= 32 && debugMode <= 37);
                if (debugMode != 0 && !compositeDebugMode)
                {
                    float4 debugSSRColor = tex2D(_BurtScreenSpaceReflectionTemporalColorTexture, screenUV);
                    return float4(debugSSRColor.rgb, 1.0);
                }

                float4 sourceColor = tex2D(_BurtSSRCameraColorCopyTexture, screenUV);

                float materialWeight = BurtSSRComputeMaterialWeight(screenUV);

                if (debugMode == 37)
                {
                    return float4(sourceColor.rgb, 1.0);
                }

                if (debugMode == 13)
                {
                    return float4(materialWeight, materialWeight, materialWeight, 1.0);
                }

                if (debugMode == 14)
                {
                    float roughnessMip = BurtSSRComputeRoughnessMip(screenUV);
                    float mipDebug = _BurtSSRParams1.y > 0.0 ? roughnessMip / max(min(_BurtSSRParams1.y, 4.0), 0.0001) : 0.0;
                    return float4(mipDebug, mipDebug, mipDebug, 1.0);
                }

                if (debugMode == 0 && materialWeight <= 0.0001)
                {
                    return sourceColor;
                }

                float4 ssrColor = BurtSSRCompositeSampleTemporalUpscaled(screenUV);
                float temporalAlpha = saturate(ssrColor.a);
                float centerAlpha = temporalAlpha;
                float receiverContinuityWeight = BurtSSRCompositeReceiverContinuityWeight(screenUV);
                float receiverAlphaGate = lerp(0.78, 1.0, smoothstep(0.04, 0.42, receiverContinuityWeight));
                centerAlpha *= receiverAlphaGate;
                ssrColor.a = centerAlpha;
                float resolvedVisibility = BurtSSRResolveCompositeAlpha(screenUV, centerAlpha) * receiverAlphaGate;
                float3 materialSpecularScale = BurtSSRComputeMaterialSpecularScale(screenUV);
                float3 resolvedColor = BurtSSRResolveCompositeColor(screenUV, ssrColor, resolvedVisibility) * materialSpecularScale;
                float3 fallbackSpecular = BurtSSRComputeCameraIBLSpecularFallback(screenUV);
                float fallbackAlphaScale = BurtSSRComputeCompositeFallbackAlphaScale(screenUV, resolvedVisibility, centerAlpha, materialWeight);
                float directReplacementConfidence = smoothstep(0.025, 0.12, min(centerAlpha, resolvedVisibility));
                float holeReplacementConfidence = smoothstep(0.025, 0.16, resolvedVisibility) * (1.0 - smoothstep(0.02, 0.12, centerAlpha));
                float replacementConfidence = max(directReplacementConfidence, holeReplacementConfidence * lerp(0.75, 1.0, smoothstep(0.32, 0.78, receiverContinuityWeight)));
                float resolvedAlpha = saturate(resolvedVisibility * materialWeight * fallbackAlphaScale * replacementConfidence);
                float darkSilhouetteSuppress = BurtSSRCompositeDarkSilhouetteSuppress(screenUV, resolvedColor, materialSpecularScale, resolvedAlpha, materialWeight, receiverContinuityWeight);
                resolvedAlpha *= 1.0 - darkSilhouetteSuppress * 0.96;
                float3 ssrDelta = resolvedColor - fallbackSpecular;
                float fallbackLuminance = BurtSSRCompositeLuminance(fallbackSpecular);
                float resolvedLuminance = BurtSSRCompositeLuminance(resolvedColor);
                float darkReplaceRisk = smoothstep(0.08, 0.35, (fallbackLuminance - resolvedLuminance) / max(fallbackLuminance, 0.03));
                float strongDarkReplace = smoothstep(0.48, 0.9, min(centerAlpha, resolvedVisibility));
                strongDarkReplace *= smoothstep(0.5, 0.95, receiverContinuityWeight);
                strongDarkReplace *= smoothstep(0.18, 0.55, resolvedAlpha);
                float darkDeltaConfidence = smoothstep(0.2, 0.62, min(centerAlpha, resolvedVisibility));
                darkDeltaConfidence *= smoothstep(0.45, 0.92, receiverContinuityWeight);
                darkDeltaConfidence *= smoothstep(0.16, 0.52, resolvedAlpha);
                float darkHaloGate = BurtSSRCompositeDarkHaloGate(screenUV, ssrColor, resolvedColor, fallbackSpecular, materialSpecularScale, resolvedAlpha, receiverContinuityWeight);
                float darkDeltaGate = darkDeltaConfidence * lerp(1.0, strongDarkReplace, darkReplaceRisk) * darkHaloGate;
                darkDeltaGate *= 1.0 - darkSilhouetteSuppress;
                float3 compositeDelta = lerp(max(ssrDelta, 0.0), ssrDelta, darkDeltaGate) * resolvedAlpha;
                compositeDelta = BurtSSRCompositeFillPositiveHole(screenUV, compositeDelta, materialWeight, receiverContinuityWeight);
                float brightOutlierSuppress = BurtSSRCompositeBrightOutlierSuppress(screenUV, compositeDelta, resolvedAlpha, materialWeight, receiverContinuityWeight);
                compositeDelta *= 1.0 - brightOutlierSuppress * 0.92;
                resolvedAlpha *= 1.0 - brightOutlierSuppress * 0.72;

                if (debugMode == 32)
                {
                    return float4(temporalAlpha, temporalAlpha, temporalAlpha, 1.0);
                }

                if (debugMode == 33)
                {
                    float roughnessMip = BurtSSRComputeRoughnessMip(screenUV);
                    float mipAlpha = tex2Dlod(_BurtScreenSpaceReflectionTemporalColorTexture, float4(screenUV, 0.0, roughnessMip)).a;
                    return float4(mipAlpha, mipAlpha, mipAlpha, 1.0);
                }

                if (debugMode == 34)
                {
                    return float4(receiverContinuityWeight, receiverContinuityWeight, receiverContinuityWeight, 1.0);
                }

                if (debugMode == 35)
                {
                    return float4(fallbackSpecular, 1.0);
                }

                if (debugMode == 36)
                {
                    float darken = saturate(max(max(-compositeDelta.r, -compositeDelta.g), -compositeDelta.b) * 12.0);
                    float brighten = saturate(max(max(compositeDelta.r, compositeDelta.g), compositeDelta.b) * 8.0);
                    return float4(darken, brighten, resolvedAlpha, 1.0);
                }

                if (debugMode == 10)
                {
                    return float4(resolvedAlpha, resolvedAlpha, resolvedAlpha, 1.0);
                }

                if (debugMode == 11)
                {
                    float finalMask = resolvedAlpha > 0.002 ? 1.0 : 0.0;
                    return float4(finalMask, finalMask, finalMask, 1.0);
                }

                if (debugMode == 12)
                {
                    return float4(resolvedVisibility, resolvedVisibility, resolvedVisibility, 1.0);
                }

                if (debugMode == 15)
                {
                    return float4(resolvedColor, 1.0);
                }

                float3 finalColor = sourceColor.rgb + compositeDelta;
                finalColor = BurtSSRProtectCompositeOutputDarkSeam(screenUV, sourceColor.rgb, finalColor, ssrDelta, resolvedAlpha, materialWeight, receiverContinuityWeight);
                return float4(max(finalColor, float3(0.0, 0.0, 0.0)), sourceColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
