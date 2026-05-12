Shader "Hidden/BurtRP/ScreenSpaceReflections"
{
    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        Pass
        {
            Name "Burt Screen Space Reflections Trace"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragSSR

            #include "UnityCG.cginc"
            #include "ShaderLibrary/BurtDeferred.hlsl"

            sampler2D _BurtSSRSourceColorTexture;
            sampler2D _BurtHiZDepthTexture;
            float4 _BurtSSRSourceTexelSize;
            float4x4 _BurtSSRViewMatrix;
            float4x4 _BurtSSRViewProjectionMatrix;
            float4 _BurtSSRParams0; // x=maxDistance, y=thickness, z=intensity, w=roughnessFade
            float4 _BurtSSRParams1; // x=maxSteps, y=maxMip, z=debugMode, w=edgeFadeWidth
            float4 _BurtSSRParams2; // x=frameIndexMod8, y=temporalAccumulation

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 screenUV : TEXCOORD0;
            };

            struct BurtSSRHit
            {
                float hit;
                float2 uv;
                float steps;
                float depthDelta;
                float distance;
                float worldError;
                float surfaceSupport;
            };

            BurtSSRHit BurtSSRCreateEmptyHit()
            {
                BurtSSRHit result;
                result.hit = 0.0;
                result.uv = 0.0;
                result.steps = 0.0;
                result.depthDelta = 0.0;
                result.distance = 0.0;
                result.worldError = 0.0;
                result.surfaceSupport = 0.0;
                return result;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = BurtGetFullScreenTriangleVertexPosition(input.vertexID);
                output.screenUV = BurtGetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            bool BurtSSRIsSkyDepth(float rawDepth)
            {
                #if defined(UNITY_REVERSED_Z)
                    return rawDepth <= 0.00001;
                #else
                    return rawDepth >= 0.99999;
                #endif
            }

            float BurtSSRRawDepthFromClip(float clipZ)
            {
                #if defined(UNITY_REVERSED_Z)
                    return saturate(clipZ);
                #else
                    return saturate((clipZ - UNITY_NEAR_CLIP_VALUE) / max(1.0 - UNITY_NEAR_CLIP_VALUE, 0.00001));
                #endif
            }

            float BurtSSRRawDepthFromClipUnbounded(float clipZ)
            {
                #if defined(UNITY_REVERSED_Z)
                    return clipZ;
                #else
                    return (clipZ - UNITY_NEAR_CLIP_VALUE) / max(1.0 - UNITY_NEAR_CLIP_VALUE, 0.00001);
                #endif
            }

            float BurtSSRInterleavedGradientNoise(float2 pixelPosition, float frameIndex)
            {
                float2 frameOffset = float2(47.13, 17.0) * frameIndex;
                return frac(52.9829189 * frac(dot(pixelPosition + frameOffset, float2(0.06711056, 0.00583715))));
            }

            float BurtSSRLinearEyeDepthWS(float3 positionWS)
            {
                float3 positionVS = mul(_BurtSSRViewMatrix, float4(positionWS, 1.0)).xyz;
                return max(-positionVS.z, 0.0);
            }

            float2 BurtSSRClipToScreenUV(float2 clipXY)
            {
                float2 uv = clipXY * 0.5 + 0.5;
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0 - uv.y;
                #endif
                return uv;
            }

            bool BurtSSRProjectPosition(float3 positionWS, out float2 screenUV, out float rawDepth, out float linearDepth)
            {
                float4 clip = mul(_BurtSSRViewProjectionMatrix, float4(positionWS, 1.0));
                if (clip.w <= 0.00001)
                {
                    screenUV = 0.0;
                    rawDepth = 0.0;
                    linearDepth = 0.0;
                    return false;
                }

                float3 ndc = clip.xyz / clip.w;
                screenUV = BurtSSRClipToScreenUV(ndc.xy);
                rawDepth = BurtSSRRawDepthFromClip(ndc.z);
                linearDepth = BurtSSRLinearEyeDepthWS(positionWS);
                return all(screenUV >= 0.0) && all(screenUV <= 1.0) && rawDepth >= 0.0 && rawDepth <= 1.0;
            }

            bool BurtSSRProjectPositionUnbounded(float3 positionWS, out float2 screenUV, out float rawDepth)
            {
                float4 clip = mul(_BurtSSRViewProjectionMatrix, float4(positionWS, 1.0));
                if (clip.w <= 0.00001)
                {
                    screenUV = 0.0;
                    rawDepth = 0.0;
                    return false;
                }

                float3 ndc = clip.xyz / clip.w;
                screenUV = BurtSSRClipToScreenUV(ndc.xy);
                rawDepth = BurtSSRRawDepthFromClipUnbounded(ndc.z);
                return true;
            }

            bool BurtSSRProjectPositionUnboundedDetailed(
                float3 positionWS,
                out float2 screenUV,
                out float rawDepth,
                out float inverseW,
                out float3 weightedPositionWS)
            {
                float4 clip = mul(_BurtSSRViewProjectionMatrix, float4(positionWS, 1.0));
                if (clip.w <= 0.00001)
                {
                    screenUV = 0.0;
                    rawDepth = 0.0;
                    inverseW = 0.0;
                    weightedPositionWS = 0.0;
                    return false;
                }

                inverseW = rcp(clip.w);
                float3 ndc = clip.xyz * inverseW;
                screenUV = BurtSSRClipToScreenUV(ndc.xy);
                rawDepth = BurtSSRRawDepthFromClipUnbounded(ndc.z);
                weightedPositionWS = positionWS * inverseW;
                return true;
            }

            float3 BurtSSRInterpolateProjectedRayPosition(
                float rayTime,
                float3 weightedStartWS,
                float3 weightedEndWS,
                float inverseWStart,
                float inverseWEnd)
            {
                float inverseW = lerp(inverseWStart, inverseWEnd, rayTime);
                float safeInverseW = abs(inverseW) > 0.000001 ? inverseW : (inverseW < 0.0 ? -0.000001 : 0.000001);
                return lerp(weightedStartWS, weightedEndWS, rayTime) / safeInverseW;
            }

            float BurtSSRSelectHiZMip(float2 previousUV, float2 currentUV)
            {
                // Until the marcher can skip across HiZ cells hierarchically, resolve against mip0 depth.
                // Sampling reduced mips as if they were exact depth over-accepts large blocks and smears hits.
                return 0.0;
            }

            float BurtSSRAdaptiveThickness(float rayLinearDepth, float travelDistance)
            {
                float baseThickness = max(_BurtSSRParams0.y, 0.0001);
                float maxDistance = max(_BurtSSRParams0.x, 0.01);
                float distanceScale = max(saturate(travelDistance / maxDistance), saturate(rayLinearDepth / max(maxDistance * 2.0, 0.01)));
                return baseThickness * lerp(0.25, 1.25, distanceScale) + rayLinearDepth * 0.0015;
            }

            bool BurtSSRTrySampleDepthDelta(float3 rayPositionWS, float2 rayUV, float hiZMip, out float depthDelta)
            {
                float sceneRawDepth = tex2Dlod(_BurtHiZDepthTexture, float4(rayUV, 0.0, hiZMip)).r;
                if (BurtSSRIsSkyDepth(sceneRawDepth))
                {
                    depthDelta = 0.0;
                    return false;
                }

                float3 scenePositionWS = BurtReconstructDeferredPositionWS(rayUV, sceneRawDepth);
                float rayLinearDepth = BurtSSRLinearEyeDepthWS(rayPositionWS);
                float sceneLinearDepth = BurtSSRLinearEyeDepthWS(scenePositionWS);
                depthDelta = rayLinearDepth - sceneLinearDepth;
                return true;
            }

            bool BurtSSRIsBehindSurface(float3 rayPositionWS, float2 rayUV, float hiZMip, float thickness, out float depthDelta)
            {
                if (!BurtSSRTrySampleDepthDelta(rayPositionWS, rayUV, hiZMip, depthDelta))
                {
                    return false;
                }

                return depthDelta >= 0.0 && depthDelta <= max(thickness, 0.0001);
            }

            bool BurtSSRRefineHit(
                float3 originWS,
                float3 reflectionDirectionWS,
                float missTravel,
                float hitTravel,
                out float refinedTravel,
                out float2 refinedUV,
                out float refinedDepthDelta)
            {
                refinedTravel = hitTravel;
                refinedUV = 0.0;
                refinedDepthDelta = 0.0;
                bool foundHit = false;

                [unroll]
                for (int refineIndex = 0; refineIndex < 5; refineIndex++)
                {
                    float midTravel = 0.5 * (missTravel + hitTravel);
                    float3 rayPositionWS = originWS + reflectionDirectionWS * midTravel;
                    float2 rayUV;
                    float rayRawDepth;
                    float rayLinearDepth;

                    if (!BurtSSRProjectPosition(rayPositionWS, rayUV, rayRawDepth, rayLinearDepth))
                    {
                        missTravel = midTravel;
                        continue;
                    }

                    float thickness = BurtSSRAdaptiveThickness(rayLinearDepth, midTravel);
                    float depthDelta;
                    if (BurtSSRIsBehindSurface(rayPositionWS, rayUV, 0.0, thickness, depthDelta))
                    {
                        hitTravel = midTravel;
                        refinedTravel = midTravel;
                        refinedUV = rayUV;
                        refinedDepthDelta = depthDelta;
                        foundHit = true;
                    }
                    else
                    {
                        missTravel = midTravel;
                    }
                }

                if (!foundHit)
                {
                    float3 rayPositionWS = originWS + reflectionDirectionWS * hitTravel;
                    float2 rayUV;
                    float rayRawDepth;
                    float rayLinearDepth;
                    if (BurtSSRProjectPosition(rayPositionWS, rayUV, rayRawDepth, rayLinearDepth))
                    {
                        float thickness = BurtSSRAdaptiveThickness(rayLinearDepth, hitTravel);
                        float depthDelta;
                        if (BurtSSRIsBehindSurface(rayPositionWS, rayUV, 0.0, thickness, depthDelta))
                        {
                            refinedTravel = hitTravel;
                            refinedUV = rayUV;
                            refinedDepthDelta = depthDelta;
                            foundHit = true;
                        }
                    }
                }

                return foundHit;
            }

            float BurtSSRHitNormalWeight(float2 hitUV, float3 reflectionDirectionWS)
            {
                BurtGBufferData hitGBufferData = BurtDecodeGBuffer(BurtSampleEncodedGBuffer(hitUV));
                float3 hitNormalWS = BurtSafeNormalize(hitGBufferData.normalWS);
                float frontFaceWeight = smoothstep(-0.15, 0.15, dot(-reflectionDirectionWS, hitNormalWS));
                return lerp(0.35, 1.0, frontFaceWeight);
            }

            bool BurtSSRIsValidHitUV(float2 hitUV)
            {
                return all(hitUV >= 0.0) && all(hitUV <= 1.0);
            }

            float BurtSSRClipScreenRay(float2 startUV, float2 deltaUV, float startRawDepth, float deltaRawDepth)
            {
                float rayScale = 1.0;

                if (deltaUV.x > 0.000001)
                {
                    rayScale = min(rayScale, (1.0 - startUV.x) / deltaUV.x);
                }
                else if (deltaUV.x < -0.000001)
                {
                    rayScale = min(rayScale, -startUV.x / deltaUV.x);
                }

                if (deltaUV.y > 0.000001)
                {
                    rayScale = min(rayScale, (1.0 - startUV.y) / deltaUV.y);
                }
                else if (deltaUV.y < -0.000001)
                {
                    rayScale = min(rayScale, -startUV.y / deltaUV.y);
                }

                if (deltaRawDepth > 0.000001)
                {
                    rayScale = min(rayScale, (1.0 - startRawDepth) / deltaRawDepth);
                }
                else if (deltaRawDepth < -0.000001)
                {
                    rayScale = min(rayScale, -startRawDepth / deltaRawDepth);
                }

                return saturate(rayScale);
            }

            bool BurtSSRTrySampleSceneRawDepth(float2 rayUV, float hiZMip, out float sceneRawDepth)
            {
                sceneRawDepth = hiZMip <= 0.5 ?
                    BurtSampleDeferredRawDepth(rayUV) :
                    tex2Dlod(_BurtHiZDepthTexture, float4(rayUV, 0.0, hiZMip)).r;

                return !BurtSSRIsSkyDepth(sceneRawDepth);
            }

            bool BurtSSRTrySampleRayMarchDepth(
                float2 rayUV,
                float2 rayDeltaUV,
                float rayLinearDepth,
                float thickness,
                float frontTolerance,
                out float sceneRawDepth)
            {
                sceneRawDepth = 0.0;
                float centerRawDepth = 0.0;
                bool centerValid = false;
                bool foundNearSurface = false;
                float bestScore = 999999.0;

                float2 rayPixels = rayDeltaUV * _BurtSSRSourceTexelSize.zw;
                float rayPixelLength = length(rayPixels);
                float2 alongUV = rayPixelLength > 0.0001 ? rayDeltaUV / rayPixelLength : float2(_BurtSSRSourceTexelSize.x, 0.0);
                float2 sideUV = float2(-alongUV.y, alongUV.x);

                [unroll]
                for (int sampleIndex = 0; sampleIndex < 9; sampleIndex++)
                {
                    float2 offset = sampleIndex == 0 ? float2(0.0, 0.0) :
                        sampleIndex == 1 ? sideUV :
                        sampleIndex == 2 ? -sideUV :
                        sampleIndex == 3 ? alongUV :
                        sampleIndex == 4 ? -alongUV :
                        sampleIndex == 5 ? sideUV * 2.0 :
                        sampleIndex == 6 ? -sideUV * 2.0 :
                        sampleIndex == 7 ? alongUV + sideUV :
                        alongUV - sideUV;
                    float2 sampleUV = rayUV + offset;
                    if (!BurtSSRIsValidHitUV(sampleUV))
                    {
                        continue;
                    }

                    float sampleRawDepth;
                    if (!BurtSSRTrySampleSceneRawDepth(sampleUV, 0.0, sampleRawDepth))
                    {
                        continue;
                    }

                    if (sampleIndex == 0)
                    {
                        centerRawDepth = sampleRawDepth;
                        centerValid = true;
                    }

                    float depthDelta = rayLinearDepth - LinearEyeDepth(sampleRawDepth);
                    if (depthDelta < -frontTolerance || depthDelta > thickness + frontTolerance)
                    {
                        continue;
                    }

                    float distancePenalty = sampleIndex == 0 ? 0.0 : sampleIndex < 5 ? 0.04 : 0.08;
                    float candidateScore = abs(depthDelta) / max(thickness + frontTolerance, 0.0001) + distancePenalty;
                    if (candidateScore < bestScore)
                    {
                        bestScore = candidateScore;
                        sceneRawDepth = sampleRawDepth;
                        foundNearSurface = true;
                    }
                }

                if (foundNearSurface)
                {
                    return true;
                }

                sceneRawDepth = centerRawDepth;
                return centerValid;
            }

            float2 BurtSSRPushHitUVInsideSilhouette(float2 hitUV, float2 rayDeltaUV)
            {
                float2 rayPixels = rayDeltaUV * _BurtSSRSourceTexelSize.zw;
                float rayPixelLength = length(rayPixels);
                if (rayPixelLength <= 0.0001)
                {
                    return hitUV;
                }

                float2 stepUV = rayDeltaUV / max(rayPixelLength, 0.0001);
                float centerRawDepth;
                if (!BurtSSRTrySampleSceneRawDepth(hitUV, 0.0, centerRawDepth))
                {
                    return hitUV;
                }

                float centerLinearDepth = LinearEyeDepth(centerRawDepth);
                float depthTolerance = max(centerLinearDepth * 0.04, 0.05);
                float backRawDepth = centerRawDepth;
                bool backValid = BurtSSRIsValidHitUV(hitUV - stepUV) && BurtSSRTrySampleSceneRawDepth(hitUV - stepUV, 0.0, backRawDepth);
                bool backDiscontinuous = !backValid || abs(LinearEyeDepth(backRawDepth) - centerLinearDepth) > depthTolerance;
                float2 sideStepUV = float2(-stepUV.y, stepUV.x);
                float sideRawDepthA = centerRawDepth;
                float sideRawDepthB = centerRawDepth;
                bool sideValidA = BurtSSRIsValidHitUV(hitUV + sideStepUV) && BurtSSRTrySampleSceneRawDepth(hitUV + sideStepUV, 0.0, sideRawDepthA);
                bool sideValidB = BurtSSRIsValidHitUV(hitUV - sideStepUV) && BurtSSRTrySampleSceneRawDepth(hitUV - sideStepUV, 0.0, sideRawDepthB);
                bool sideDiscontinuous =
                    !sideValidA || !sideValidB ||
                    abs(LinearEyeDepth(sideRawDepthA) - centerLinearDepth) > depthTolerance ||
                    abs(LinearEyeDepth(sideRawDepthB) - centerLinearDepth) > depthTolerance;
                if (!backDiscontinuous && !sideDiscontinuous)
                {
                    return hitUV;
                }

                float2 bestUV = hitUV;
                [unroll]
                for (int sampleIndex = 0; sampleIndex < 3; sampleIndex++)
                {
                    float candidateOffset = 0.5 + (float)sampleIndex * 0.5;
                    float2 candidateUV = hitUV + stepUV * candidateOffset;
                    if (!BurtSSRIsValidHitUV(candidateUV))
                    {
                        break;
                    }

                    float candidateRawDepth;
                    if (BurtSSRTrySampleSceneRawDepth(candidateUV, 0.0, candidateRawDepth))
                    {
                        bestUV = candidateUV;
                    }
                    else
                    {
                        break;
                    }
                }

                return bestUV;
            }

            bool BurtSSRIsValidResolvedHitCandidate(
                float2 hitUV,
                float3 originWS,
                float3 reflectionDirectionWS,
                float minRayDistance,
                float maxRayDistance,
                out float hitDistance,
                out float normalizedWorldError)
            {
                hitDistance = 0.0;
                normalizedWorldError = 1.0;
                if (!BurtSSRIsValidHitUV(hitUV))
                {
                    return false;
                }

                float sceneRawDepth;
                if (!BurtSSRTrySampleSceneRawDepth(hitUV, 0.0, sceneRawDepth))
                {
                    return false;
                }

                float3 scenePositionWS = BurtReconstructDeferredPositionWS(hitUV, sceneRawDepth);
                float3 originToHit = scenePositionWS - originWS;
                float rayDistance = dot(originToHit, reflectionDirectionWS);
                if (rayDistance < minRayDistance || rayDistance > maxRayDistance + max(_BurtSSRParams0.y, 0.01))
                {
                    return false;
                }

                float3 closestRayPositionWS = originWS + reflectionDirectionWS * rayDistance;
                float rayError = length(scenePositionWS - closestRayPositionWS);
                float distanceScale = saturate(rayDistance / max(_BurtSSRParams0.x, 0.01));
                float worldTolerance = max(_BurtSSRParams0.y * lerp(0.35, 1.5, distanceScale), rayDistance * 0.006);
                hitDistance = rayDistance;
                normalizedWorldError = rayError / max(worldTolerance, 0.0001);
                return rayError <= worldTolerance * 1.6;
            }

            float BurtSSRSameSurfaceSupport(float2 sampleUV, float centerLinearDepth, float3 centerNormal, float depthTolerance)
            {
                if (!BurtSSRIsValidHitUV(sampleUV))
                {
                    return 0.0;
                }

                float sampleRawDepth;
                if (!BurtSSRTrySampleSceneRawDepth(sampleUV, 0.0, sampleRawDepth))
                {
                    return 0.0;
                }

                float sampleLinearDepth = LinearEyeDepth(sampleRawDepth);
                BurtGBufferData sampleGBuffer = BurtDecodeGBuffer(BurtSampleEncodedGBuffer(sampleUV));
                float normalSupport = saturate(dot(centerNormal, BurtSafeNormalize(sampleGBuffer.normalWS)));
                normalSupport *= normalSupport;
                float depthSupport = 1.0 - smoothstep(depthTolerance * 0.5, depthTolerance, abs(sampleLinearDepth - centerLinearDepth));
                return normalSupport * depthSupport;
            }

            float BurtSSREstimateHitSurfaceSupport(float2 hitUV, float2 rayDeltaUV)
            {
                float centerRawDepth;
                if (!BurtSSRTrySampleSceneRawDepth(hitUV, 0.0, centerRawDepth))
                {
                    return 0.0;
                }

                float centerLinearDepth = LinearEyeDepth(centerRawDepth);
                BurtGBufferData centerGBuffer = BurtDecodeGBuffer(BurtSampleEncodedGBuffer(hitUV));
                float3 centerNormal = BurtSafeNormalize(centerGBuffer.normalWS);
                float depthTolerance = max(centerLinearDepth * 0.025, 0.035);
                float2 rayPixels = rayDeltaUV * _BurtSSRSourceTexelSize.zw;
                float rayPixelLength = length(rayPixels);
                float2 alongUV = rayPixelLength > 0.0001 ? rayDeltaUV / rayPixelLength : float2(_BurtSSRSourceTexelSize.x, 0.0);
                float2 sideUV = float2(-alongUV.y, alongUV.x);

                float sideA = BurtSSRSameSurfaceSupport(hitUV + sideUV, centerLinearDepth, centerNormal, depthTolerance);
                float sideB = BurtSSRSameSurfaceSupport(hitUV - sideUV, centerLinearDepth, centerNormal, depthTolerance);
                float alongF = BurtSSRSameSurfaceSupport(hitUV + alongUV, centerLinearDepth, centerNormal, depthTolerance);
                float alongB = BurtSSRSameSurfaceSupport(hitUV - alongUV, centerLinearDepth, centerNormal, depthTolerance);
                float sideA2 = BurtSSRSameSurfaceSupport(hitUV + sideUV * 2.0, centerLinearDepth, centerNormal, depthTolerance) * 0.6;
                float sideB2 = BurtSSRSameSurfaceSupport(hitUV - sideUV * 2.0, centerLinearDepth, centerNormal, depthTolerance) * 0.6;
                float diagonalA = BurtSSRSameSurfaceSupport(hitUV + alongUV + sideUV, centerLinearDepth, centerNormal, depthTolerance) * 0.5;
                float diagonalB = BurtSSRSameSurfaceSupport(hitUV + alongUV - sideUV, centerLinearDepth, centerNormal, depthTolerance) * 0.5;
                float pairedSupport = max(max(min(sideA, sideB), min(alongF, alongB)), max(diagonalA, diagonalB));
                float totalSupport = sideA + sideB + alongF + alongB + sideA2 + sideB2 + diagonalA + diagonalB;
                return saturate(max(totalSupport / 3.0, pairedSupport));
            }

            bool BurtSSRFindBestResolvedHitCandidate(
                float2 baseHitUV,
                float2 rayDeltaUV,
                float3 originWS,
                float3 reflectionDirectionWS,
                float minRayDistance,
                float maxRayDistance,
                out float2 resolvedHitUV,
                out float hitDistance,
                out float normalizedWorldError,
                out float surfaceSupport)
            {
                resolvedHitUV = baseHitUV;
                hitDistance = 0.0;
                normalizedWorldError = 999.0;
                surfaceSupport = 0.0;

                float2 rayPixels = rayDeltaUV * _BurtSSRSourceTexelSize.zw;
                float rayPixelLength = length(rayPixels);
                float2 alongUV = rayPixelLength > 0.0001 ? rayDeltaUV / rayPixelLength : float2(_BurtSSRSourceTexelSize.x, 0.0);
                float2 sideUV = float2(-alongUV.y, alongUV.x);
                bool foundCandidate = false;
                float bestScore = 999.0;

                [unroll]
                for (int sampleIndex = 0; sampleIndex < 13; sampleIndex++)
                {
                    float2 offset = sampleIndex == 0 ? float2(0.0, 0.0) :
                        sampleIndex == 1 ? sideUV :
                        sampleIndex == 2 ? -sideUV :
                        sampleIndex == 3 ? alongUV :
                        sampleIndex == 4 ? -alongUV :
                        sampleIndex == 5 ? sideUV * 2.0 :
                        sampleIndex == 6 ? -sideUV * 2.0 :
                        sampleIndex == 7 ? alongUV + sideUV :
                        sampleIndex == 8 ? alongUV - sideUV :
                        sampleIndex == 9 ? -alongUV + sideUV :
                        sampleIndex == 10 ? -alongUV - sideUV :
                        sampleIndex == 11 ? alongUV * 2.0 :
                        -alongUV * 2.0;
                    float2 candidateUV = baseHitUV + offset;
                    float candidateDistance;
                    float candidateWorldError;
                    if (BurtSSRIsValidResolvedHitCandidate(candidateUV, originWS, reflectionDirectionWS, minRayDistance, maxRayDistance, candidateDistance, candidateWorldError))
                    {
                        float candidateSupport = BurtSSREstimateHitSurfaceSupport(candidateUV, rayDeltaUV);
                        float candidateScore = candidateWorldError - candidateSupport * 0.35;
                        if (candidateScore >= bestScore)
                        {
                            continue;
                        }

                        resolvedHitUV = candidateUV;
                        hitDistance = candidateDistance;
                        normalizedWorldError = candidateWorldError;
                        surfaceSupport = candidateSupport;
                        bestScore = candidateScore;
                        foundCandidate = true;
                    }
                }

                return foundCandidate;
            }

            float BurtSSRRawDepthDelta(float rayRawDepth, float sceneRawDepth)
            {
                return LinearEyeDepth(rayRawDepth) - LinearEyeDepth(sceneRawDepth);
            }

            float BurtSSRClipDistanceBeforeCamera(float3 originWS, float3 reflectionDirectionWS, float maxDistance)
            {
                float3 originVS = mul(_BurtSSRViewMatrix, float4(originWS, 1.0)).xyz;
                float3 directionVS = mul(_BurtSSRViewMatrix, float4(reflectionDirectionWS, 0.0)).xyz;
                float originViewDepth = max(-originVS.z, 0.0);
                if (directionVS.z > 0.00001)
                {
                    return min(maxDistance, max(originViewDepth * 0.95 / directionVS.z, 0.01));
                }

                return maxDistance;
            }

            bool BurtSSRRefineScreenHit(
                float2 startUV,
                float2 deltaUV,
                float3 weightedStartWS,
                float3 weightedEndWS,
                float inverseWStart,
                float inverseWEnd,
                float missTime,
                float hitTime,
                out float refinedTime,
                out float2 refinedUV,
                out float refinedDepthDelta)
            {
                refinedTime = hitTime;
                refinedUV = startUV + deltaUV * hitTime;
                refinedDepthDelta = 0.0;
                bool foundCrossing = false;

                [unroll]
                for (int refineIndex = 0; refineIndex < 6; refineIndex++)
                {
                    float midTime = 0.5 * (missTime + hitTime);
                    float2 rayUV = startUV + deltaUV * midTime;
                    float3 rayPositionWS = BurtSSRInterpolateProjectedRayPosition(midTime, weightedStartWS, weightedEndWS, inverseWStart, inverseWEnd);

                    float sceneRawDepth;
                    if (!BurtSSRTrySampleSceneRawDepth(rayUV, 0.0, sceneRawDepth))
                    {
                        missTime = midTime;
                        continue;
                    }

                    float depthDelta = BurtSSRLinearEyeDepthWS(rayPositionWS) - LinearEyeDepth(sceneRawDepth);
                    if (depthDelta >= 0.0)
                    {
                        hitTime = midTime;
                        refinedTime = midTime;
                        refinedUV = rayUV;
                        refinedDepthDelta = depthDelta;
                        foundCrossing = true;
                    }
                    else
                    {
                        missTime = midTime;
                    }
                }

                return foundCrossing;
            }

            BurtSSRHit BurtSSRMarch(float3 originWS, float3 reflectionDirectionWS)
            {
                BurtSSRHit result = BurtSSRCreateEmptyHit();

                const int traceStepLimit = 512;
                int maxSteps = min(max((int)_BurtSSRParams1.x, 1), traceStepLimit);
                float maxDistance = BurtSSRClipDistanceBeforeCamera(originWS, reflectionDirectionWS, max(_BurtSSRParams0.x, 0.01));
                float2 startUV;
                float startRawDepth;
                float inverseWStart;
                float3 weightedStartWS;
                if (!BurtSSRProjectPositionUnboundedDetailed(originWS, startUV, startRawDepth, inverseWStart, weightedStartWS))
                {
                    return result;
                }

                if (!all(startUV >= 0.0) || !all(startUV <= 1.0) || startRawDepth < 0.0 || startRawDepth > 1.0)
                {
                    return result;
                }

                float2 endUV;
                float endRawDepth;
                float inverseWEnd;
                float3 weightedEndWS;
                if (!BurtSSRProjectPositionUnboundedDetailed(originWS + reflectionDirectionWS * maxDistance, endUV, endRawDepth, inverseWEnd, weightedEndWS))
                {
                    return result;
                }

                float2 fullDeltaUV = endUV - startUV;
                float fullDeltaRawDepth = endRawDepth - startRawDepth;
                float rayScale = BurtSSRClipScreenRay(startUV, fullDeltaUV, startRawDepth, fullDeltaRawDepth);
                if (rayScale <= 0.0001)
                {
                    return result;
                }

                float2 deltaUV = fullDeltaUV * rayScale;
                weightedEndWS = lerp(weightedStartWS, weightedEndWS, rayScale);
                inverseWEnd = lerp(inverseWStart, inverseWEnd, rayScale);
                float3 clippedEndWS = BurtSSRInterpolateProjectedRayPosition(1.0, weightedStartWS, weightedEndWS, inverseWStart, inverseWEnd);
                float clippedDistance = max(length(clippedEndWS - originWS), 0.01);
                float minTraceDistance = min(max(_BurtSSRParams0.y * 0.2, 0.025), clippedDistance * 0.25);
                float screenPixelSpan = length(deltaUV * _BurtSSRSourceTexelSize.zw);
                maxSteps = min(traceStepLimit, max(maxSteps, (int)ceil(screenPixelSpan)));

                float stepTime = 1.0 / max((float)maxSteps, 1.0);
                float noise = BurtSSRInterleavedGradientNoise(startUV * _BurtSSRSourceTexelSize.zw, _BurtSSRParams2.x);
                float stepJitter = _BurtSSRParams2.y > 0.5 ? lerp(0.15, 0.95, noise) : 1.0;
                float previousTime = 0.0;
                float previousDepthDelta = -max(_BurtSSRParams0.y, 0.0001);
                bool hasPreviousDepthDelta = true;

                [loop]
                for (int stepIndex = 1; stepIndex <= traceStepLimit; stepIndex++)
                {
                    if (stepIndex > maxSteps)
                    {
                        break;
                    }

                    float rayTime = stepIndex == maxSteps ? 1.0 : stepTime * ((float)stepIndex - 1.0 + stepJitter);
                    float2 rayUV = startUV + deltaUV * rayTime;
                    float3 rayPositionWS = BurtSSRInterpolateProjectedRayPosition(rayTime, weightedStartWS, weightedEndWS, inverseWStart, inverseWEnd);
                    float rayLinearDepth = BurtSSRLinearEyeDepthWS(rayPositionWS);
                    if (!all(rayUV >= 0.0) || !all(rayUV <= 1.0) || rayLinearDepth <= 0.0)
                    {
                        break;
                    }

                    float travelDistance = length(rayPositionWS - originWS);
                    float thickness = BurtSSRAdaptiveThickness(rayLinearDepth, travelDistance);
                    float frontTolerance = max(rayLinearDepth * 0.001, 0.02);
                    float sceneRawDepth;
                    if (!BurtSSRTrySampleRayMarchDepth(rayUV, deltaUV, rayLinearDepth, thickness, frontTolerance, sceneRawDepth))
                    {
                        previousTime = rayTime;
                        hasPreviousDepthDelta = false;
                        continue;
                    }

                    float depthDelta;
                    depthDelta = rayLinearDepth - LinearEyeDepth(sceneRawDepth);
                    bool farEnoughFromOrigin = travelDistance >= minTraceDistance;
                    bool nearSurface = farEnoughFromOrigin && depthDelta >= -frontTolerance && depthDelta <= thickness + frontTolerance;

                    bool crossedFromKnownMiss = hasPreviousDepthDelta && previousDepthDelta < -frontTolerance && depthDelta >= -frontTolerance;
                    bool crossedFromSkyOrOffscreen = !hasPreviousDepthDelta && depthDelta >= -frontTolerance;
                    bool crossedSurface = farEnoughFromOrigin && (crossedFromKnownMiss || crossedFromSkyOrOffscreen);
                    if (crossedSurface)
                    {
                        float refinedTime;
                        float2 refinedUV;
                        float refinedDepthDelta;
                        bool refined = BurtSSRRefineScreenHit(startUV, deltaUV, weightedStartWS, weightedEndWS, inverseWStart, inverseWEnd, previousTime, rayTime, refinedTime, refinedUV, refinedDepthDelta);
                        if (crossedFromSkyOrOffscreen && !refined && !nearSurface)
                        {
                            previousTime = rayTime;
                            previousDepthDelta = depthDelta;
                            hasPreviousDepthDelta = true;
                            continue;
                        }

                        float2 baseCandidateUV = BurtSSRPushHitUVInsideSilhouette(refined ? refinedUV : rayUV, deltaUV);
                        float2 candidateUV;
                        float candidateDistance;
                        float candidateWorldError;
                        float candidateSurfaceSupport;
                        if (!BurtSSRFindBestResolvedHitCandidate(baseCandidateUV, deltaUV, originWS, reflectionDirectionWS, minTraceDistance, clippedDistance, candidateUV, candidateDistance, candidateWorldError, candidateSurfaceSupport))
                        {
                            previousTime = rayTime;
                            previousDepthDelta = -max(_BurtSSRParams0.y, 0.0001);
                            hasPreviousDepthDelta = true;
                            continue;
                        }

                        result.hit = 1.0;
                        result.uv = candidateUV;
                        result.steps = (float)stepIndex / max((float)maxSteps, 1.0);
                        result.depthDelta = refined ? refinedDepthDelta : depthDelta;
                        result.distance = candidateDistance;
                        result.worldError = candidateWorldError;
                        result.surfaceSupport = candidateSurfaceSupport;
                        break;
                    }

                    if (nearSurface)
                    {
                        float candidateDistance;
                        float candidateWorldError;
                        float2 baseCandidateUV = BurtSSRPushHitUVInsideSilhouette(rayUV, deltaUV);
                        float2 candidateUV;
                        float candidateSurfaceSupport;
                        if (!BurtSSRFindBestResolvedHitCandidate(baseCandidateUV, deltaUV, originWS, reflectionDirectionWS, minTraceDistance, clippedDistance, candidateUV, candidateDistance, candidateWorldError, candidateSurfaceSupport))
                        {
                            previousTime = rayTime;
                            previousDepthDelta = -max(_BurtSSRParams0.y, 0.0001);
                            hasPreviousDepthDelta = true;
                            continue;
                        }

                        result.hit = 1.0;
                        result.uv = candidateUV;
                        result.steps = (float)stepIndex / max((float)maxSteps, 1.0);
                        result.depthDelta = depthDelta;
                        result.distance = candidateDistance;
                        result.worldError = candidateWorldError;
                        result.surfaceSupport = candidateSurfaceSupport;
                        break;
                    }

                    previousTime = rayTime;
                    previousDepthDelta = depthDelta;
                    hasPreviousDepthDelta = true;
                }

                return result;
            }

            float BurtSSREdgeFade(float2 uv)
            {
                float2 edgeDistance = min(uv, 1.0 - uv);
                float edgeFadeWidth = max(_BurtSSRParams1.w, 0.0001);
                return saturate(min(edgeDistance.x, edgeDistance.y) / edgeFadeWidth);
            }

            float4 FragSSR(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenUV;
                int debugMode = (int)_BurtSSRParams1.z;
                float rawDepth = BurtSampleDeferredRawDepth(screenUV);

                if (BurtSSRIsSkyDepth(rawDepth))
                {
                    return debugMode != 0 ? float4(0.0, 0.0, 0.0, 1.0) : float4(0.0, 0.0, 0.0, 0.0);
                }

                BurtGBufferData gbufferData = BurtDecodeGBuffer(BurtSampleEncodedGBuffer(screenUV));
                float3 positionWS = BurtReconstructDeferredPositionWS(screenUV, rawDepth);
                float3 viewDirectionWS = BurtSafeNormalize(_BurtDeferredCameraWorldPosition.xyz - positionWS);
                float3 normalWS = BurtSafeNormalize(gbufferData.normalWS);
                float3 reflectionDirectionWS = BurtSafeNormalize(reflect(-viewDirectionWS, normalWS));
                float nDotV = saturate(dot(normalWS, viewDirectionWS));

                float roughnessFade = saturate((_BurtSSRParams0.w - gbufferData.perceptualRoughness) / max(_BurtSSRParams0.w, 0.0001));

                float thickness = max(_BurtSSRParams0.y, 0.0001);
                float originBias = min(thickness * 0.08, 0.025);
                float3 originWS = positionWS + normalWS * originBias + reflectionDirectionWS * originBias;
                BurtSSRHit hit = BurtSSRCreateEmptyHit();
                if (roughnessFade * _BurtSSRParams0.z > 0.0001)
                {
                    hit = BurtSSRMarch(originWS, reflectionDirectionWS);
                }

                float edgeFade = BurtSSREdgeFade(hit.uv);
                float hitNormalWeight = hit.hit > 0.0 ? BurtSSRHitNormalWeight(hit.uv, reflectionDirectionWS) : 0.0;
                float3 reflectionDirectionVS = BurtSafeNormalize(mul(_BurtSSRViewMatrix, float4(reflectionDirectionWS, 0.0)).xyz);
                float screenParallelWeight = lerp(0.35, 1.0, smoothstep(0.005, 0.08, abs(reflectionDirectionVS.z)));
                float grazingWeight = smoothstep(0.01, 0.06, nDotV);
                float distanceFade = hit.hit > 0.0 ? saturate(1.0 - hit.distance / max(_BurtSSRParams0.x, 0.01)) : 0.0;
                distanceFade *= distanceFade;
                float depthError = hit.hit > 0.0 ? abs(hit.depthDelta) / max(thickness * 1.25, 0.0001) : 999.0;
                float depthQuality = hit.hit > 0.0 ? 1.0 - smoothstep(0.85, 1.35, depthError) : 0.0;
                float worldQuality = hit.hit > 0.0 ? 1.0 - smoothstep(0.8, 1.6, hit.worldError) : 0.0;
                float surfaceSupportWeight = hit.hit > 0.0 ? lerp(0.45, 1.0, smoothstep(0.15, 0.85, hit.surfaceSupport)) : 0.0;
                float resolveQuality = depthQuality * worldQuality;
                float validHit = saturate(hit.hit * hitNormalWeight * screenParallelWeight * grazingWeight * distanceFade * resolveQuality * surfaceSupportWeight);
                float visibilityWeight = saturate(validHit * edgeFade);
                float3 reflectionColor = tex2D(_BurtSSRSourceColorTexture, hit.uv).rgb;

                if (debugMode == 1)
                {
                    float rawHitMask = saturate(hit.hit);
                    return float4(rawHitMask, rawHitMask, rawHitMask, 1.0);
                }

                if (debugMode == 2)
                {
                    return float4(hit.uv * validHit, 0.0, 1.0);
                }

                if (debugMode == 3)
                {
                    float visibleSteps = hit.steps * hit.hit;
                    return float4(visibleSteps, visibleSteps, visibleSteps, 1.0);
                }

                if (debugMode == 4)
                {
                    return float4(reflectionColor * validHit, 1.0);
                }

                if (debugMode == 5)
                {
                    return float4(visibilityWeight, visibilityWeight, visibilityWeight, 1.0);
                }

                if (debugMode == 6)
                {
                    if (hit.hit <= 0.0)
                    {
                        return float4(0.0, 0.0, 0.0, 1.0);
                    }

                    float deltaDebug = saturate(abs(hit.depthDelta) / max(thickness * 1.25, 0.0001));
                    return float4(deltaDebug, 1.0 - deltaDebug, 0.0, 1.0);
                }

                if (debugMode == 7)
                {
                    if (hit.hit <= 0.0)
                    {
                        return float4(0.0, 0.0, 0.0, 1.0);
                    }

                    float worldErrorDebug = saturate(hit.worldError);
                    return float4(worldErrorDebug, 1.0 - worldErrorDebug, 0.0, 1.0);
                }

                if (debugMode == 16)
                {
                    return float4(depthQuality, depthQuality, depthQuality, 1.0);
                }

                if (debugMode == 17)
                {
                    return float4(worldQuality, worldQuality, worldQuality, 1.0);
                }

                if (debugMode == 18)
                {
                    return float4(resolveQuality, resolveQuality, resolveQuality, 1.0);
                }

                if (debugMode == 19)
                {
                    return float4(hit.surfaceSupport, hit.surfaceSupport, hit.surfaceSupport, 1.0);
                }

                return float4(reflectionColor * hit.hit, visibilityWeight);
            }
            ENDHLSL
        }

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
                float3 centerNormal = BurtSafeNormalize(centerGBuffer.normalWS);

                int debugMode = (int)_BurtSSRParams1.z;
                if ((debugMode > 0 && debugMode <= 7) || (debugMode >= 16 && debugMode <= 19))
                {
                    return centerSSR;
                }

                float2 texel = _BurtSSRSourceTexelSize.xy;
                float centerConfidence = saturate(centerSSR.a);
                float3 accumulatedColor = centerSSR.rgb * centerConfidence;
                float accumulatedConfidence = centerConfidence;
                float totalWeight = max(centerConfidence, 0.0001);

                float baseSampleRadius = lerp(1.0, 2.25, saturate(centerGBuffer.perceptualRoughness * 2.0));
                float sampleRadius = lerp(1.0, baseSampleRadius, saturate(centerConfidence * 4.0));
                float fillSupport = 0.0;
                float4 axialSupport = 0.0;
                float4 diagonalSupport = 0.0;
                float4 axialSupport2 = 0.0;
                float4 diagonalSupport2 = 0.0;

                [unroll]
                for (int sampleIndex = 0; sampleIndex < 16; sampleIndex++)
                {
                    float2 offset = sampleIndex == 0 ? float2(1.0, 0.0) :
                        sampleIndex == 1 ? float2(-1.0, 0.0) :
                        sampleIndex == 2 ? float2(0.0, 1.0) :
                        sampleIndex == 3 ? float2(0.0, -1.0) :
                        sampleIndex == 4 ? float2(1.0, 1.0) :
                        sampleIndex == 5 ? float2(-1.0, 1.0) :
                        sampleIndex == 6 ? float2(1.0, -1.0) :
                        sampleIndex == 7 ? float2(-1.0, -1.0) :
                        sampleIndex == 8 ? float2(2.0, 0.0) :
                        sampleIndex == 9 ? float2(-2.0, 0.0) :
                        sampleIndex == 10 ? float2(0.0, 2.0) :
                        sampleIndex == 11 ? float2(0.0, -2.0) :
                        sampleIndex == 12 ? float2(2.0, 2.0) :
                        sampleIndex == 13 ? float2(-2.0, 2.0) :
                        sampleIndex == 14 ? float2(2.0, -2.0) :
                        float2(-2.0, -2.0);
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
                    float normalWeight = saturate(dot(centerNormal, BurtSafeNormalize(sampleGBuffer.normalWS)));
                    normalWeight *= normalWeight * normalWeight;
                    float depthTolerance = max(centerLinearDepth * 0.015, 0.01);
                    float depthWeight = exp2(-abs(sampleLinearDepth - centerLinearDepth) / depthTolerance);
                    depthWeight *= depthWeight;
                    float roughnessWeight = exp2(-abs(sampleGBuffer.perceptualRoughness - centerGBuffer.perceptualRoughness) * 10.0);
                    float sampleConfidence = saturate(sampleSSR.a);
                    float confidenceGate = smoothstep(0.02, 0.18, sampleConfidence);
                    float alphaWeight = centerConfidence > 0.05 ?
                        smoothstep(0.0, 0.35, saturate(1.0 - abs(sampleConfidence - centerConfidence) * 2.5)) :
                        confidenceGate;
                    float tapWeight = sampleIndex < 4 ? 1.0 : sampleIndex < 8 ? 0.7071 : sampleIndex < 12 ? 0.5 : 0.3535;
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
                    axialSupport2.x += sampleIndex == 8 ? support : 0.0;
                    axialSupport2.y += sampleIndex == 9 ? support : 0.0;
                    axialSupport2.z += sampleIndex == 10 ? support : 0.0;
                    axialSupport2.w += sampleIndex == 11 ? support : 0.0;
                    diagonalSupport2.x += sampleIndex == 12 ? support : 0.0;
                    diagonalSupport2.y += sampleIndex == 13 ? support : 0.0;
                    diagonalSupport2.z += sampleIndex == 14 ? support : 0.0;
                    diagonalSupport2.w += sampleIndex == 15 ? support : 0.0;
                    accumulatedColor += sampleSSR.rgb * sampleConfidence * weight;
                    accumulatedConfidence += sampleConfidence * weight;
                    totalWeight += weight;
                }

                float outputConfidence = saturate(accumulatedConfidence / max(totalWeight, 0.0001));
                float3 outputColor = accumulatedConfidence > 0.0001 ? accumulatedColor / accumulatedConfidence : float3(0.0, 0.0, 0.0);
                float surroundedSupport = min(max(axialSupport.x, axialSupport.y), max(axialSupport.z, axialSupport.w));
                float pairedAxialSupport = max(min(axialSupport.x, axialSupport.y), min(axialSupport.z, axialSupport.w));
                float pairedDiagonalSupport = max(min(diagonalSupport.x, diagonalSupport.w), min(diagonalSupport.y, diagonalSupport.z));
                float surroundedSupport2 = min(max(axialSupport2.x, axialSupport2.y), max(axialSupport2.z, axialSupport2.w));
                float pairedAxialSupport2 = max(min(axialSupport2.x, axialSupport2.y), min(axialSupport2.z, axialSupport2.w));
                float pairedDiagonalSupport2 = max(min(diagonalSupport2.x, diagonalSupport2.w), min(diagonalSupport2.y, diagonalSupport2.z));
                float pairedSupport2 = max(pairedAxialSupport2, pairedDiagonalSupport2);
                float twoDimensionalSupport2 = max(max(surroundedSupport2, pairedDiagonalSupport2), pairedAxialSupport2 * 0.5);
                float pairedSupport = max(max(pairedAxialSupport, pairedDiagonalSupport), pairedSupport2 * 0.75);
                float twoDimensionalSupport = max(max(surroundedSupport, pairedDiagonalSupport), twoDimensionalSupport2 * 0.65);
                float primaryFillGate =
                    smoothstep(1.0, 1.8, fillSupport) *
                    smoothstep(0.08, 0.22, twoDimensionalSupport) *
                    smoothstep(0.04, 0.14, pairedSupport);
                float longFillGate =
                    smoothstep(0.18, 0.5, pairedSupport2) *
                    smoothstep(0.12, 0.4, twoDimensionalSupport2);
                float fillGate = max(primaryFillGate, longFillGate * 0.55);
                float centerHitReliability = smoothstep(0.003, 0.012, centerConfidence);
                float weakCenterBlend = 1.0 - centerHitReliability;
                float fillEnergy = max(fillSupport, pairedSupport2 * 2.0 + twoDimensionalSupport2);
                float fillConfidence = min(outputConfidence * fillGate * saturate((fillEnergy - 0.35) * 0.45) * saturate(twoDimensionalSupport * 4.0), 0.55);
                float centerLock = smoothstep(0.006, 0.06, centerConfidence);
                float stableConfidence = lerp(outputConfidence, centerConfidence, centerLock * 0.35);
                outputColor = lerp(outputColor, centerSSR.rgb, centerLock * 0.65);
                outputConfidence = lerp(stableConfidence, fillConfidence, weakCenterBlend);

                return float4(outputColor, outputConfidence);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Burt Screen Space Reflections Composite"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha, Zero One

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragComposite

            #include "UnityCG.cginc"
            #include "ShaderLibrary/BurtDeferred.hlsl"

            sampler2D _BurtScreenSpaceReflectionTemporalColorTexture;
            float4 _BurtSSRSourceTexelSize;
            float4 _BurtSSRParams0; // z=intensity, w=roughnessFade
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

            bool BurtSSRCompositeIsSkyDepth(float rawDepth)
            {
                #if defined(UNITY_REVERSED_Z)
                    return rawDepth <= 0.00001;
                #else
                    return rawDepth >= 0.99999;
                #endif
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
                float normalWeight = saturate(dot(centerNormal, BurtSafeNormalize(sampleGBuffer.normalWS)));
                normalWeight *= normalWeight * normalWeight;
                float sampleLinearDepth = LinearEyeDepth(sampleRawDepth);
                float depthTolerance = max(centerLinearDepth * 0.012, 0.01);
                float depthWeight = exp2(-abs(sampleLinearDepth - centerLinearDepth) / depthTolerance);
                float roughnessWeight = exp2(-abs(sampleGBuffer.perceptualRoughness - centerRoughness) * 10.0);
                return saturate(normalWeight * depthWeight * depthWeight * roughnessWeight);
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

            float2 BurtSSRCompositeTapOffset(int sampleIndex, float roughnessRadius)
            {
                float radius = sampleIndex < 8 ? 1.0 : roughnessRadius;
                int localIndex = sampleIndex < 8 ? sampleIndex : sampleIndex - 8;
                return localIndex == 0 ? float2(1.0, 0.0) * radius :
                    localIndex == 1 ? float2(-1.0, 0.0) * radius :
                    localIndex == 2 ? float2(0.0, 1.0) * radius :
                    localIndex == 3 ? float2(0.0, -1.0) * radius :
                    localIndex == 4 ? float2(1.0, 1.0) * radius :
                    localIndex == 5 ? float2(-1.0, 1.0) * radius :
                    localIndex == 6 ? float2(1.0, -1.0) * radius :
                    float2(-1.0, -1.0) * radius;
            }

            float BurtSSRComputeRoughnessMipFromRoughness(float perceptualRoughness)
            {
                float pureSpecularRoughness = 0.06;
                float roughnessRange = max(_BurtSSRParams0.w - pureSpecularRoughness, 0.0001);
                return saturate((perceptualRoughness - pureSpecularRoughness) / roughnessRange) * min(_BurtSSRParams1.y, 4.0);
            }

            float BurtSSRComputeRoughnessMip(float2 screenUV)
            {
                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtSSRCompositeIsSkyDepth(rawDepth))
                {
                    return 0.0;
                }

                BurtGBufferData gbufferData = BurtDecodeGBuffer(BurtSampleEncodedGBuffer(screenUV));
                return BurtSSRComputeRoughnessMipFromRoughness(gbufferData.perceptualRoughness);
            }

            float3 BurtSSRResolveCompositeColor(float2 screenUV, float4 centerSSR, float resolvedVisibility)
            {
                float centerRawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtSSRCompositeIsSkyDepth(centerRawDepth))
                {
                    return centerSSR.rgb;
                }

                BurtGBufferData centerGBuffer = BurtDecodeGBuffer(BurtSampleEncodedGBuffer(screenUV));
                float centerLinearDepth = LinearEyeDepth(centerRawDepth);
                float3 centerNormal = BurtSafeNormalize(centerGBuffer.normalWS);
                float centerRoughness = centerGBuffer.perceptualRoughness;
                float centerAlpha = saturate(centerSSR.a);
                float pureSpecularRoughness = 0.06;
                float roughnessGate = smoothstep(pureSpecularRoughness, max(_BurtSSRParams0.w, pureSpecularRoughness + 0.0001), centerRoughness);
                float roughnessMip = BurtSSRComputeRoughnessMipFromRoughness(centerRoughness);
                float3 mipColor = tex2Dlod(_BurtScreenSpaceReflectionTemporalColorTexture, float4(screenUV, 0.0, roughnessMip)).rgb;
                float holeGate = smoothstep(0.01, 0.08, saturate(resolvedVisibility - centerAlpha));
                float tapGate = max(holeGate, roughnessGate);
                if (tapGate <= 0.0001)
                {
                    return centerSSR.rgb;
                }

                float roughnessRadius = max(lerp(1.5, 5.0, roughnessGate * roughnessGate), lerp(1.0, 2.0, holeGate));
                float centerWeight = lerp(max(centerAlpha, 0.02), max(centerAlpha, 0.35), 1.0 - holeGate);
                float3 accumulatedColor = centerSSR.rgb * centerWeight;
                float totalWeight = centerWeight;
                float2 texel = _BurtSSRSourceTexelSize.xy;

                [unroll]
                for (int sampleIndex = 0; sampleIndex < 16; sampleIndex++)
                {
                    float roughnessTapGate = sampleIndex < 8 ? 1.0 : max(roughnessGate, holeGate * 0.75);
                    float2 sampleUV = screenUV + BurtSSRCompositeTapOffset(sampleIndex, roughnessRadius) * texel;
                    if (!all(sampleUV >= 0.0) || !all(sampleUV <= 1.0))
                    {
                        continue;
                    }

                    float4 sampleSSR = tex2D(_BurtScreenSpaceReflectionTemporalColorTexture, sampleUV);
                    float surfaceWeight = BurtSSRCompositeSurfaceWeight(sampleUV, centerLinearDepth, centerNormal, centerRoughness);
                    float sampleAlpha = saturate(sampleSSR.a);
                    float alphaWeight = smoothstep(0.003, 0.08, sampleAlpha);
                    float diagonalWeight = (sampleIndex % 8) < 4 ? 1.0 : 0.7071;
                    float weight = tapGate * roughnessTapGate * diagonalWeight * surfaceWeight * alphaWeight * max(sampleAlpha, 0.02);
                    accumulatedColor += sampleSSR.rgb * weight;
                    totalWeight += weight;
                }

                float3 filteredColor = accumulatedColor / max(totalWeight, 0.0001);
                float mipBlend = roughnessGate * smoothstep(0.04, 0.18, resolvedVisibility) * (1.0 - holeGate * 0.65);
                filteredColor = lerp(filteredColor, mipColor, mipBlend * 0.55);
                float mirrorLock = (1.0 - roughnessGate) * smoothstep(0.05, 0.2, centerAlpha) * (1.0 - holeGate);
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
                float3 centerNormal = BurtSafeNormalize(centerGBuffer.normalWS);
                float centerRoughness = centerGBuffer.perceptualRoughness;
                float2 texel = _BurtSSRSourceTexelSize.xy;
                float alphaRight = BurtSSRCompositeNeighborAlpha(screenUV + float2(texel.x, 0.0), centerLinearDepth, centerNormal, centerRoughness);
                float alphaLeft = BurtSSRCompositeNeighborAlpha(screenUV - float2(texel.x, 0.0), centerLinearDepth, centerNormal, centerRoughness);
                float alphaUp = BurtSSRCompositeNeighborAlpha(screenUV + float2(0.0, texel.y), centerLinearDepth, centerNormal, centerRoughness);
                float alphaDown = BurtSSRCompositeNeighborAlpha(screenUV - float2(0.0, texel.y), centerLinearDepth, centerNormal, centerRoughness);
                float alphaNE = BurtSSRCompositeNeighborAlpha(screenUV + float2(texel.x, texel.y), centerLinearDepth, centerNormal, centerRoughness);
                float alphaNW = BurtSSRCompositeNeighborAlpha(screenUV + float2(-texel.x, texel.y), centerLinearDepth, centerNormal, centerRoughness);
                float alphaSE = BurtSSRCompositeNeighborAlpha(screenUV + float2(texel.x, -texel.y), centerLinearDepth, centerNormal, centerRoughness);
                float alphaSW = BurtSSRCompositeNeighborAlpha(screenUV + float2(-texel.x, -texel.y), centerLinearDepth, centerNormal, centerRoughness);
                float2 texel2 = texel * 2.0;
                float alphaRight2 = BurtSSRCompositeNeighborAlpha(screenUV + float2(texel2.x, 0.0), centerLinearDepth, centerNormal, centerRoughness);
                float alphaLeft2 = BurtSSRCompositeNeighborAlpha(screenUV - float2(texel2.x, 0.0), centerLinearDepth, centerNormal, centerRoughness);
                float alphaUp2 = BurtSSRCompositeNeighborAlpha(screenUV + float2(0.0, texel2.y), centerLinearDepth, centerNormal, centerRoughness);
                float alphaDown2 = BurtSSRCompositeNeighborAlpha(screenUV - float2(0.0, texel2.y), centerLinearDepth, centerNormal, centerRoughness);
                float alphaNE2 = BurtSSRCompositeNeighborAlpha(screenUV + texel2, centerLinearDepth, centerNormal, centerRoughness);
                float alphaNW2 = BurtSSRCompositeNeighborAlpha(screenUV + float2(-texel2.x, texel2.y), centerLinearDepth, centerNormal, centerRoughness);
                float alphaSE2 = BurtSSRCompositeNeighborAlpha(screenUV + float2(texel2.x, -texel2.y), centerLinearDepth, centerNormal, centerRoughness);
                float alphaSW2 = BurtSSRCompositeNeighborAlpha(screenUV - texel2, centerLinearDepth, centerNormal, centerRoughness);

                float horizontalSupport = max(alphaLeft, alphaRight);
                float verticalSupport = max(alphaUp, alphaDown);
                float surroundedSupport = min(horizontalSupport, verticalSupport);
                float diagonalSupport = max(min(alphaNE, alphaSW), min(alphaNW, alphaSE));
                float longHorizontalSupport = min(alphaLeft2, alphaRight2);
                float longVerticalSupport = min(alphaUp2, alphaDown2);
                float longDiagonalSupport = max(min(alphaNE2, alphaSW2), min(alphaNW2, alphaSE2));
                float longBridgeSupport = max(max(longHorizontalSupport, longVerticalSupport), longDiagonalSupport);
                float twoDimensionalSupport = max(max(surroundedSupport, diagonalSupport), longBridgeSupport * 0.5);
                float axialSupport = max(horizontalSupport, verticalSupport);
                float bridgeSupport = max(max(max(min(alphaLeft, alphaRight), min(alphaUp, alphaDown)), diagonalSupport), longBridgeSupport * 0.75);
                float bridgeGate = smoothstep(0.04, 0.16, bridgeSupport) * (1.0 - smoothstep(0.015, 0.08, centerAlpha));
                float resolvedCenterAlpha = max(centerAlpha, bridgeSupport * bridgeGate);
                float strongAlphaGate = smoothstep(0.04, 0.16, resolvedCenterAlpha);
                float support = lerp(twoDimensionalSupport, max(twoDimensionalSupport, axialSupport * 0.6), strongAlphaGate);
                float supportGate = smoothstep(0.004, 0.04, support);
                float lowAlphaGate = smoothstep(0.001, 0.006, resolvedCenterAlpha);
                float isolatedFade = lerp(0.25 + supportGate * 0.75, 1.0, strongAlphaGate);
                return saturate(resolvedCenterAlpha * lowAlphaGate * isolatedFade);
            }

            float BurtSSRComputeMaterialWeight(float2 screenUV)
            {
                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtSSRCompositeIsSkyDepth(rawDepth))
                {
                    return 0.0;
                }

                BurtGBufferData gbufferData = BurtDecodeGBuffer(BurtSampleEncodedGBuffer(screenUV));
                float roughnessFade = saturate((_BurtSSRParams0.w - gbufferData.perceptualRoughness) / max(_BurtSSRParams0.w, 0.0001));
                if (roughnessFade <= 0.0 || _BurtSSRParams0.z <= 0.0)
                {
                    return 0.0;
                }

                float3 positionWS = BurtReconstructDeferredPositionWS(screenUV, rawDepth);
                float3 viewDirectionWS = BurtSafeNormalize(_BurtDeferredCameraWorldPosition.xyz - positionWS);
                float3 normalWS = BurtSafeNormalize(gbufferData.normalWS);
                float nDotV = saturate(dot(normalWS, viewDirectionWS));
                BurtPBRMaterialData materialData = BurtPreparePBRMaterialData(gbufferData);
                float3 fresnel = F_Schlick(materialData.f0, materialData.f90, nDotV);
                return saturate(max(max(fresnel.r, fresnel.g), fresnel.b) * roughnessFade * _BurtSSRParams0.z);
            }

            float4 FragComposite(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenUV;
                float4 ssrColor = tex2D(_BurtScreenSpaceReflectionTemporalColorTexture, screenUV);
                int debugMode = (int)_BurtSSRParams1.z;
                float resolvedVisibility = BurtSSRResolveCompositeAlpha(screenUV, saturate(ssrColor.a));
                float3 resolvedColor = BurtSSRResolveCompositeColor(screenUV, ssrColor, resolvedVisibility);
                float materialWeight = BurtSSRComputeMaterialWeight(screenUV);
                float resolvedAlpha = saturate(resolvedVisibility * materialWeight);

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

                if (debugMode == 15)
                {
                    return float4(resolvedColor, 1.0);
                }

                if (debugMode != 0)
                {
                    return float4(ssrColor.rgb, 1.0);
                }

                return float4(resolvedColor, resolvedAlpha);
            }
            ENDHLSL
        }

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
            float4 _BurtSSRSourceTexelSize;
            float4x4 _BurtSSRPreviousViewMatrix;
            float4x4 _BurtSSRPreviousViewProjectionMatrix;
            float4 _BurtSSRTemporalParams0; // x=feedback, y=historyValid, z=depthRejection, w=clampStrength
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

            bool BurtSSRTemporalIsSkyDepth(float rawDepth)
            {
                #if defined(UNITY_REVERSED_Z)
                    return rawDepth <= 0.00001;
                #else
                    return rawDepth >= 0.99999;
                #endif
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
                neighborhoodMin = center.rgb;
                neighborhoodMax = center.rgb;
                neighborhoodAlpha = saturate(center.a);

                [unroll]
                for (int sampleIndex = 0; sampleIndex < 16; sampleIndex++)
                {
                    float2 offset = sampleIndex == 0 ? float2(1.0, 0.0) :
                        sampleIndex == 1 ? float2(-1.0, 0.0) :
                        sampleIndex == 2 ? float2(0.0, 1.0) :
                        sampleIndex == 3 ? float2(0.0, -1.0) :
                        sampleIndex == 4 ? float2(1.0, 1.0) :
                        sampleIndex == 5 ? float2(-1.0, 1.0) :
                        sampleIndex == 6 ? float2(1.0, -1.0) :
                        sampleIndex == 7 ? float2(-1.0, -1.0) :
                        sampleIndex == 8 ? float2(2.0, 0.0) :
                        sampleIndex == 9 ? float2(-2.0, 0.0) :
                        sampleIndex == 10 ? float2(0.0, 2.0) :
                        sampleIndex == 11 ? float2(0.0, -2.0) :
                        sampleIndex == 12 ? float2(2.0, 2.0) :
                        sampleIndex == 13 ? float2(-2.0, 2.0) :
                        sampleIndex == 14 ? float2(2.0, -2.0) :
                        float2(-2.0, -2.0);
                    float4 sampleSSR = tex2D(_BurtScreenSpaceReflectionDenoisedColorTexture, saturate(screenUV + offset * texel));
                    float alphaSupportWeight = sampleIndex < 8 ? 1.0 : 0.65;
                    neighborhoodAlpha = max(neighborhoodAlpha, saturate(sampleSSR.a) * alphaSupportWeight);
                    if (sampleIndex < 8)
                    {
                        neighborhoodMin = min(neighborhoodMin, sampleSSR.rgb);
                        neighborhoodMax = max(neighborhoodMax, sampleSSR.rgb);
                    }
                }
            }

            float4 FragTemporal(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenUV;
                float4 currentSSR = tex2D(_BurtScreenSpaceReflectionDenoisedColorTexture, screenUV);
                int debugMode = (int)_BurtSSRParams1.z;
                if ((debugMode > 0 && debugMode <= 8) || (debugMode >= 16 && debugMode <= 19))
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
                float2 previousUV;
                float previousLinearDepth;
                if (!BurtSSRTemporalProjectPrevious(positionWS, previousUV, previousLinearDepth))
                {
                    return currentSSR;
                }

                float previousRawDepth = tex2D(_BurtSSRHistoryDepthTexture, previousUV).r;
                if (BurtSSRTemporalIsSkyDepth(previousRawDepth))
                {
                    return currentSSR;
                }

                float previousHistoryLinearDepth = LinearEyeDepth(previousRawDepth);
                float depthTolerance = max(previousLinearDepth * _BurtSSRTemporalParams0.z, 0.02);
                float depthWeight = exp2(-abs(previousHistoryLinearDepth - previousLinearDepth) / depthTolerance);
                float4 historySSR = tex2D(_BurtSSRHistoryTexture, previousUV);

                float3 neighborhoodMin;
                float3 neighborhoodMax;
                float neighborhoodAlpha;
                BurtSSRTemporalNeighborhood(screenUV, _BurtSSRSourceTexelSize.xy, neighborhoodMin, neighborhoodMax, neighborhoodAlpha);
                float3 neighborhoodRange = max(neighborhoodMax - neighborhoodMin, 0.0001);
                float clampExpand = max(_BurtSSRTemporalParams0.w - 1.0, 0.0) * 0.5;
                float3 historyColor = clamp(historySSR.rgb, neighborhoodMin - neighborhoodRange * clampExpand, neighborhoodMax + neighborhoodRange * clampExpand);

                float currentConfidence = saturate(currentSSR.a);
                float historyConfidence = saturate(historySSR.a) * depthWeight;
                float hitResponsiveWeight = smoothstep(0.006, 0.08, currentConfidence);
                float holeSeed = max(neighborhoodAlpha, min(historyConfidence, neighborhoodAlpha + 0.03));
                float holeSupport = smoothstep(0.015, 0.12, holeSeed) *
                    (1.0 - smoothstep(0.015, 0.08, currentConfidence));
                float historyResponsiveWeight = smoothstep(0.02, 0.12, historyConfidence);
                float responsiveWeight = max(hitResponsiveWeight, holeSupport * historyResponsiveWeight);
                float feedback = saturate(_BurtSSRTemporalParams0.x * depthWeight * responsiveWeight);
                float3 outputColor = lerp(currentSSR.rgb, historyColor, feedback);
                float outputConfidence = saturate(lerp(currentConfidence, historyConfidence, feedback));
                outputConfidence = max(outputConfidence, min(neighborhoodAlpha, historyConfidence) * holeSupport * 0.55);
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
    }

    Fallback Off
}
