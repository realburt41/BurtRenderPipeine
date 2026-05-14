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
            float4 _BurtSSRParams2; // x=frameIndexMod8, y=temporalAccumulation, z=experimentalHiZTrace, w=hiZTextureUsable

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
                float rawHit;
                float2 uv;
                float steps;
                float depthDelta;
                float distance;
                float worldError;
                float surfaceSupport;
                float hiZSkipCandidate;
                float hiZMipLevel;
                float hiZDivergence;
                float hiZSkipUsed;
                float hiZProbeBlocked;
                float workCost;
            };

            BurtSSRHit BurtSSRCreateEmptyHit()
            {
                BurtSSRHit result;
                result.hit = 0.0;
                result.rawHit = 0.0;
                result.uv = 0.0;
                result.steps = 0.0;
                result.depthDelta = 0.0;
                result.distance = 0.0;
                result.worldError = 0.0;
                result.surfaceSupport = 0.0;
                result.hiZSkipCandidate = 0.0;
                result.hiZMipLevel = 0.0;
                result.hiZDivergence = 0.0;
                result.hiZSkipUsed = 0.0;
                result.hiZProbeBlocked = 0.0;
                result.workCost = 0.0;
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
                float2 pixelDelta = abs((currentUV - previousUV) * _BurtSSRSourceTexelSize.zw);
                float pixelSpan = max(max(pixelDelta.x, pixelDelta.y), 1.0);
                return clamp(floor(log2(pixelSpan)) - 1.0, 0.0, min(_BurtSSRParams1.y, 6.0));
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
                bool canSampleHiZMip = _BurtSSRParams2.w > 0.5 && hiZMip > 0.5;
                float sceneRawDepth = canSampleHiZMip ?
                    tex2Dlod(_BurtHiZDepthTexture, float4(rayUV, 0.0, hiZMip)).r :
                    BurtSampleDeferredRawDepth(rayUV);
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

            float BurtSSRComputeCellExitTime(float2 startUV, float2 deltaUV, float currentTime, float mipLevel)
            {
                float2 screenSize = _BurtSSRSourceTexelSize.zw;
                float2 startPixel = startUV * screenSize;
                float2 deltaPixel = deltaUV * screenSize;
                float2 currentPixel = startPixel + deltaPixel * currentTime;
                float cellSize = exp2(mipLevel);
                float2 directionSign = float2(deltaPixel.x >= 0.0 ? 1.0 : -1.0, deltaPixel.y >= 0.0 ? 1.0 : -1.0);
                float2 biasedPixel = currentPixel + directionSign * 0.001;
                float2 cellCoord = floor(biasedPixel / cellSize);
                float2 nextBoundary = float2(
                    deltaPixel.x >= 0.0 ? (cellCoord.x + 1.0) * cellSize : cellCoord.x * cellSize,
                    deltaPixel.y >= 0.0 ? (cellCoord.y + 1.0) * cellSize : cellCoord.y * cellSize);
                float2 boundaryTime = 999999.0;

                if (abs(deltaPixel.x) > 0.00001)
                {
                    boundaryTime.x = (nextBoundary.x - startPixel.x) / deltaPixel.x;
                }

                if (abs(deltaPixel.y) > 0.00001)
                {
                    boundaryTime.y = (nextBoundary.y - startPixel.y) / deltaPixel.y;
                }

                return clamp(min(boundaryTime.x, boundaryTime.y), currentTime, 1.0);
            }

            float BurtSSRAdvanceTime(float currentTime, float nextTime, float minTimeStep)
            {
                return min(1.0, max(nextTime, currentTime + minTimeStep));
            }

            float2 BurtSSRComputeCellCenterUV(float2 startUV, float2 deltaUV, float sampleTime, float mipLevel)
            {
                float2 screenSize = _BurtSSRSourceTexelSize.zw;
                float2 samplePixel = (startUV + deltaUV * sampleTime) * screenSize;
                float cellSize = exp2(mipLevel);
                float2 cellCenterPixel = (floor(samplePixel / cellSize) + 0.5) * cellSize;
                return saturate(cellCenterPixel / screenSize);
            }

            bool BurtSSRTrySampleSceneRawDepth(float2 rayUV, float hiZMip, out float sceneRawDepth)
            {
                bool canSampleHiZMip = _BurtSSRParams2.w > 0.5 && hiZMip > 0.5;
                sceneRawDepth = !canSampleHiZMip ?
                    BurtSampleDeferredRawDepth(rayUV) :
                    tex2Dlod(_BurtHiZDepthTexture, float4(rayUV, 0.0, hiZMip)).r;

                return !BurtSSRIsSkyDepth(sceneRawDepth);
            }

            float BurtSSRClosestRawDepth(float rawDepthA, float rawDepthB)
            {
                #if defined(UNITY_REVERSED_Z)
                    return max(rawDepthA, rawDepthB);
                #else
                    return min(rawDepthA, rawDepthB);
                #endif
            }

            bool BurtSSRTrySampleHiZNeighborhoodClosestRawDepth(float2 sampleUV, float mipLevel, out float sceneRawDepth)
            {
                if (mipLevel <= 0.5)
                {
                    return BurtSSRTrySampleSceneRawDepth(sampleUV, 0.0, sceneRawDepth);
                }

                sceneRawDepth = 0.0;
                bool foundDepth = false;
                float2 mipTexelUV = _BurtSSRSourceTexelSize.xy * exp2(mipLevel);

                [unroll]
                for (int sampleIndex = 0; sampleIndex < 9; sampleIndex++)
                {
                    float2 offset = sampleIndex == 0 ? float2(0.0, 0.0) :
                        sampleIndex == 1 ? float2(1.0, 0.0) :
                        sampleIndex == 2 ? float2(-1.0, 0.0) :
                        sampleIndex == 3 ? float2(0.0, 1.0) :
                        sampleIndex == 4 ? float2(0.0, -1.0) :
                        sampleIndex == 5 ? float2(1.0, 1.0) :
                        sampleIndex == 6 ? float2(-1.0, 1.0) :
                        sampleIndex == 7 ? float2(1.0, -1.0) :
                        float2(-1.0, -1.0);
                    float2 neighborUV = sampleUV + offset * mipTexelUV;
                    if (!BurtSSRIsValidHitUV(neighborUV))
                    {
                        continue;
                    }

                    float neighborRawDepth;
                    if (!BurtSSRTrySampleSceneRawDepth(neighborUV, mipLevel, neighborRawDepth))
                    {
                        continue;
                    }

                    sceneRawDepth = foundDepth ? BurtSSRClosestRawDepth(sceneRawDepth, neighborRawDepth) : neighborRawDepth;
                    foundDepth = true;
                }

                return foundDepth;
            }

            bool BurtSSRIsMip0FrontMiss(
                float2 startUV,
                float2 deltaUV,
                float3 weightedStartWS,
                float3 weightedEndWS,
                float inverseWStart,
                float inverseWEnd,
                float rayTime)
            {
                float2 rayUV = startUV + deltaUV * rayTime;
                if (!all(rayUV >= 0.0) || !all(rayUV <= 1.0))
                {
                    return true;
                }

                float3 rayPositionWS = BurtSSRInterpolateProjectedRayPosition(rayTime, weightedStartWS, weightedEndWS, inverseWStart, inverseWEnd);
                float rayLinearDepth = BurtSSRLinearEyeDepthWS(rayPositionWS);
                float frontTolerance = max(max(rayLinearDepth * 0.002, _BurtSSRParams0.y * 0.25), 0.04);
                float2 rayPixels = deltaUV * _BurtSSRSourceTexelSize.zw;
                float rayPixelLength = length(rayPixels);
                float2 alongUV = rayPixelLength > 0.0001 ? deltaUV / rayPixelLength : float2(_BurtSSRSourceTexelSize.x, 0.0);
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

                    float sceneRawDepth;
                    if (!BurtSSRTrySampleSceneRawDepth(sampleUV, 0.0, sceneRawDepth))
                    {
                        continue;
                    }

                    float sceneLinearDepth = LinearEyeDepth(sceneRawDepth);
                    if (rayLinearDepth >= sceneLinearDepth - frontTolerance)
                    {
                        return false;
                    }
                }

                return true;
            }

            bool BurtSSRIsMip0SegmentFrontMiss(
                float2 startUV,
                float2 deltaUV,
                float3 weightedStartWS,
                float3 weightedEndWS,
                float inverseWStart,
                float inverseWEnd,
                float currentTime,
                float nextTime)
            {
                [unroll]
                for (int sampleIndex = 0; sampleIndex < 5; sampleIndex++)
                {
                    float sampleT = (float)sampleIndex * 0.25;
                    float rayTime = lerp(currentTime, nextTime, sampleT);
                    if (!BurtSSRIsMip0FrontMiss(startUV, deltaUV, weightedStartWS, weightedEndWS, inverseWStart, inverseWEnd, rayTime))
                    {
                        return false;
                    }
                }

                return true;
            }

            bool BurtSSRCanSkipHiZCell(
                float2 startUV,
                float2 deltaUV,
                float3 weightedStartWS,
                float3 weightedEndWS,
                float inverseWStart,
                float inverseWEnd,
                float currentTime,
                float nextTime,
                float mipLevel)
            {
                float sampleTime = saturate((currentTime + nextTime) * 0.5);
                float2 sampleUV = BurtSSRComputeCellCenterUV(startUV, deltaUV, sampleTime, mipLevel);
                if (!all(sampleUV >= 0.0) || !all(sampleUV <= 1.0))
                {
                    return true;
                }

                float sceneRawDepth;
                if (!BurtSSRTrySampleHiZNeighborhoodClosestRawDepth(sampleUV, mipLevel, sceneRawDepth))
                {
                    return true;
                }

                float3 rayStartWS = BurtSSRInterpolateProjectedRayPosition(currentTime, weightedStartWS, weightedEndWS, inverseWStart, inverseWEnd);
                float3 rayMidWS = BurtSSRInterpolateProjectedRayPosition(sampleTime, weightedStartWS, weightedEndWS, inverseWStart, inverseWEnd);
                float3 rayEndWS = BurtSSRInterpolateProjectedRayPosition(nextTime, weightedStartWS, weightedEndWS, inverseWStart, inverseWEnd);
                float rayFarthestLinearDepth = max(max(BurtSSRLinearEyeDepthWS(rayStartWS), BurtSSRLinearEyeDepthWS(rayMidWS)), BurtSSRLinearEyeDepthWS(rayEndWS));
                float sceneClosestLinearDepth = LinearEyeDepth(sceneRawDepth);
                float frontTolerance = max(max(rayFarthestLinearDepth * 0.003, _BurtSSRParams0.y * 0.35), 0.05);
                if (rayFarthestLinearDepth >= sceneClosestLinearDepth - frontTolerance)
                {
                    return false;
                }

                // The caller performs the mip0 proof pass and reuses any hit it finds.
                return true;
            }

            void BurtSSREvaluateHiZDebugSegment(
                float2 startUV,
                float2 deltaUV,
                float3 weightedStartWS,
                float3 weightedEndWS,
                float inverseWStart,
                float inverseWEnd,
                float currentTime,
                float nextTime,
                float mipLevel,
                out float rawSkipCandidate,
                out float mip0Divergence)
            {
                rawSkipCandidate = 0.0;
                mip0Divergence = 0.0;

                float sampleTime = saturate((currentTime + nextTime) * 0.5);
                float2 sampleUV = BurtSSRComputeCellCenterUV(startUV, deltaUV, sampleTime, mipLevel);
                float sceneRawDepth;
                bool hiZHasDepth = BurtSSRTrySampleHiZNeighborhoodClosestRawDepth(sampleUV, mipLevel, sceneRawDepth);
                if (!hiZHasDepth)
                {
                    rawSkipCandidate = 1.0;
                    return;
                }

                float3 rayStartWS = BurtSSRInterpolateProjectedRayPosition(currentTime, weightedStartWS, weightedEndWS, inverseWStart, inverseWEnd);
                float3 rayMidWS = BurtSSRInterpolateProjectedRayPosition(sampleTime, weightedStartWS, weightedEndWS, inverseWStart, inverseWEnd);
                float3 rayEndWS = BurtSSRInterpolateProjectedRayPosition(nextTime, weightedStartWS, weightedEndWS, inverseWStart, inverseWEnd);
                float rayFarthestLinearDepth = max(max(BurtSSRLinearEyeDepthWS(rayStartWS), BurtSSRLinearEyeDepthWS(rayMidWS)), BurtSSRLinearEyeDepthWS(rayEndWS));
                float sceneClosestLinearDepth = LinearEyeDepth(sceneRawDepth);
                float frontTolerance = max(max(rayFarthestLinearDepth * 0.003, _BurtSSRParams0.y * 0.35), 0.05);
                rawSkipCandidate = rayFarthestLinearDepth < sceneClosestLinearDepth - frontTolerance ? 1.0 : 0.0;

                bool mip0Safe = BurtSSRIsMip0SegmentFrontMiss(startUV, deltaUV, weightedStartWS, weightedEndWS, inverseWStart, inverseWEnd, currentTime, nextTime);
                mip0Divergence = rawSkipCandidate * (mip0Safe ? 0.0 : 1.0);
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

            bool BurtSSRSegmentContainsMip0RawHit(
                float2 startUV,
                float2 deltaUV,
                float3 weightedStartWS,
                float3 weightedEndWS,
                float inverseWStart,
                float inverseWEnd,
                float3 originWS,
                float currentTime,
                float nextTime,
                float minTimeStep,
                float previousTime,
                float previousDepthDelta,
                bool hasPreviousDepthDelta,
                float minTraceDistance,
                out float probeWorkCost,
                out float descendTime,
                out float descendPreviousTime,
                out float descendPreviousDepthDelta,
                out float descendHasPreviousDepthDelta,
                out float proofRayTime,
                out float2 proofRayUV,
                out float proofDepthDelta,
                out float proofNearSurface,
                out float proofCrossedSurface)
            {
                probeWorkCost = 0.0;
                descendTime = currentTime;
                descendPreviousTime = previousTime;
                descendPreviousDepthDelta = previousDepthDelta;
                descendHasPreviousDepthDelta = hasPreviousDepthDelta ? 1.0 : 0.0;
                proofRayTime = currentTime;
                proofRayUV = startUV + deltaUV * currentTime;
                proofDepthDelta = 0.0;
                proofNearSurface = 0.0;
                proofCrossedSurface = 0.0;
                float localTime = currentTime;
                float localPreviousTime = previousTime;
                float localPreviousDepthDelta = previousDepthDelta;
                bool localHasPreviousDepthDelta = hasPreviousDepthDelta;

                [loop]
                for (int segmentStep = 0; segmentStep < 16; segmentStep++)
                {
                    if (localTime >= nextTime - 0.000001)
                    {
                        break;
                    }

                    float childNextTime = BurtSSRComputeCellExitTime(startUV, deltaUV, localTime, 0.0);
                    childNextTime = min(nextTime, BurtSSRAdvanceTime(localTime, childNextTime, minTimeStep));
                    if (childNextTime <= localTime + 0.000001)
                    {
                        break;
                    }

                    float rayTime = saturate((localTime + childNextTime) * 0.5);
                    float2 rayUV = startUV + deltaUV * rayTime;
                    float3 rayPositionWS = BurtSSRInterpolateProjectedRayPosition(rayTime, weightedStartWS, weightedEndWS, inverseWStart, inverseWEnd);
                    float rayLinearDepth = BurtSSRLinearEyeDepthWS(rayPositionWS);
                    if (!all(rayUV >= 0.0) || !all(rayUV <= 1.0) || rayLinearDepth <= 0.0)
                    {
                        return false;
                    }

                    float travelDistance = length(rayPositionWS - originWS);
                    float thickness = BurtSSRAdaptiveThickness(rayLinearDepth, travelDistance);
                    float frontTolerance = max(rayLinearDepth * 0.001, 0.02);
                    float sceneRawDepth;
                    probeWorkCost += 9.0;
                    if (!BurtSSRTrySampleRayMarchDepth(rayUV, deltaUV, rayLinearDepth, thickness, frontTolerance, sceneRawDepth))
                    {
                        localPreviousTime = rayTime;
                        localHasPreviousDepthDelta = false;
                        localTime = childNextTime;
                        continue;
                    }

                    float depthDelta = rayLinearDepth - LinearEyeDepth(sceneRawDepth);
                    bool farEnoughFromOrigin = travelDistance >= minTraceDistance;
                    if (!farEnoughFromOrigin)
                    {
                        localPreviousTime = rayTime;
                        localPreviousDepthDelta = -max(thickness, 0.0001);
                        localHasPreviousDepthDelta = true;
                        localTime = childNextTime;
                        continue;
                    }

                    bool nearSurface = depthDelta >= -frontTolerance && depthDelta <= thickness + frontTolerance;
                    bool crossedFromKnownMiss = localHasPreviousDepthDelta && localPreviousDepthDelta < -frontTolerance && depthDelta >= -frontTolerance;
                    bool crossedFromSkyOrOffscreen = !localHasPreviousDepthDelta && depthDelta >= -frontTolerance;
                    if (nearSurface || crossedFromKnownMiss || crossedFromSkyOrOffscreen)
                    {
                        descendTime = localTime;
                        descendPreviousTime = localPreviousTime;
                        descendPreviousDepthDelta = localPreviousDepthDelta;
                        descendHasPreviousDepthDelta = localHasPreviousDepthDelta ? 1.0 : 0.0;
                        proofRayTime = rayTime;
                        proofRayUV = rayUV;
                        proofDepthDelta = depthDelta;
                        proofNearSurface = nearSurface ? 1.0 : 0.0;
                        proofCrossedSurface = (crossedFromKnownMiss || crossedFromSkyOrOffscreen) ? 1.0 : 0.0;
                        return true;
                    }

                    localPreviousTime = rayTime;
                    localPreviousDepthDelta = depthDelta;
                    localHasPreviousDepthDelta = true;
                    localTime = childNextTime;
                }

                return false;
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

            BurtSSRHit BurtSSRMarchInternal(float3 originWS, float3 reflectionDirectionWS, int forcedMaxTraceMip, bool collectHiZDiagnostics)
            {
                BurtSSRHit result = BurtSSRCreateEmptyHit();

                const int traceStepLimit = 512;
                int requestedSteps = min(max((int)_BurtSSRParams1.x, 1), traceStepLimit);
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
                float2 screenDelta = deltaUV * _BurtSSRSourceTexelSize.zw;
                float screenMajorSpan = max(abs(screenDelta.x), abs(screenDelta.y));
                int iterationLimit = min(traceStepLimit, max(requestedSteps * 4, (int)ceil(max(screenMajorSpan, 1.0))));
                float minTimeStep = 0.25 / max(screenMajorSpan, 1.0);
                bool useHiZTrace = _BurtSSRParams2.z > 0.5 && _BurtSSRParams2.w > 0.5 && forcedMaxTraceMip > 0;
                int maxTraceMip = useHiZTrace ? min(max(forcedMaxTraceMip, 1), (int)min(_BurtSSRParams1.y, 3.0)) : 0;
                int currentMip = 0;
                int debugMode = (int)_BurtSSRParams1.z;
                bool collectHiZDebug = false;
                float debugHiZMip = min(_BurtSSRParams1.y, 1.0);

                float currentTime = 0.0;
                float previousTime = 0.0;
                float previousDepthDelta = -max(_BurtSSRParams0.y, 0.0001);
                bool hasPreviousDepthDelta = true;

                [loop]
                for (int iterationIndex = 1; iterationIndex <= traceStepLimit; iterationIndex++)
                {
                    if (iterationIndex > iterationLimit || currentTime >= 1.0)
                    {
                        break;
                    }

                    result.workCost += 1.0;
                    float mipLevel = (float)currentMip;
                    float nextTime = BurtSSRComputeCellExitTime(startUV, deltaUV, currentTime, mipLevel);
                    nextTime = BurtSSRAdvanceTime(currentTime, nextTime, minTimeStep);

                    if (collectHiZDebug && debugHiZMip >= 1.0)
                    {
                        float debugSkipMissThreshold = max(_BurtSSRParams0.y * 0.35, 0.05);
                        bool debugCanAttemptHiZSkip = previousTime > 0.0 && hasPreviousDepthDelta && previousDepthDelta < -debugSkipMissThreshold;
                        if (debugCanAttemptHiZSkip)
                        {
                            float rawSkipCandidate;
                            float mip0Divergence;
                            float debugNextTime = BurtSSRComputeCellExitTime(startUV, deltaUV, currentTime, debugHiZMip);
                            debugNextTime = BurtSSRAdvanceTime(currentTime, debugNextTime, minTimeStep);
                            BurtSSREvaluateHiZDebugSegment(
                                startUV,
                                deltaUV,
                                weightedStartWS,
                                weightedEndWS,
                                inverseWStart,
                                inverseWEnd,
                                currentTime,
                                debugNextTime,
                                debugHiZMip,
                                rawSkipCandidate,
                                mip0Divergence);
                            result.hiZSkipCandidate = max(result.hiZSkipCandidate, rawSkipCandidate);
                            result.hiZDivergence = max(result.hiZDivergence, mip0Divergence);
                            result.hiZMipLevel = max(result.hiZMipLevel, rawSkipCandidate * debugHiZMip / max(min(_BurtSSRParams1.y, 3.0), 1.0));
                        }
                    }

                    if (useHiZTrace && currentMip > 0)
                    {
                        float skipMissThreshold = max(_BurtSSRParams0.y * 0.35, 0.05);
                        bool canAttemptHiZSkip = previousTime > 0.0 && hasPreviousDepthDelta && previousDepthDelta < -skipMissThreshold;
                        if (canAttemptHiZSkip)
                        {
                            result.workCost += 9.0;
                        }

                        bool hiZCellLooksSkippable = canAttemptHiZSkip &&
                            BurtSSRCanSkipHiZCell(startUV, deltaUV, weightedStartWS, weightedEndWS, inverseWStart, inverseWEnd, currentTime, nextTime, mipLevel);
                        float segmentProbeWork = 0.0;
                        float descendTime = currentTime;
                        float descendPreviousTime = previousTime;
                        float descendPreviousDepthDelta = previousDepthDelta;
                        float descendHasPreviousDepthDelta = hasPreviousDepthDelta ? 1.0 : 0.0;
                        float proofRayTime = currentTime;
                        float2 proofRayUV = startUV + deltaUV * currentTime;
                        float proofDepthDelta = 0.0;
                        float proofNearSurface = 0.0;
                        float proofCrossedSurface = 0.0;
                        bool segmentHasMip0RawHit = false;
                        if (hiZCellLooksSkippable)
                        {
                            segmentHasMip0RawHit = BurtSSRSegmentContainsMip0RawHit(
                                startUV,
                                deltaUV,
                                weightedStartWS,
                                weightedEndWS,
                                inverseWStart,
                                inverseWEnd,
                                originWS,
                                currentTime,
                                nextTime,
                                minTimeStep,
                                previousTime,
                                previousDepthDelta,
                                hasPreviousDepthDelta,
                                minTraceDistance,
                                segmentProbeWork,
                                descendTime,
                                descendPreviousTime,
                                descendPreviousDepthDelta,
                                descendHasPreviousDepthDelta,
                                proofRayTime,
                                proofRayUV,
                                proofDepthDelta,
                                proofNearSurface,
                                proofCrossedSurface);
                        }

                        result.workCost += segmentProbeWork;
                        result.hiZProbeBlocked = max(result.hiZProbeBlocked, segmentHasMip0RawHit ? 1.0 : 0.0);
                        bool canSkipHiZCell = hiZCellLooksSkippable && !segmentHasMip0RawHit;
                        if (canSkipHiZCell)
                        {
                            result.hiZSkipUsed = 1.0;
                            previousTime = nextTime;
                            previousDepthDelta = -max(_BurtSSRParams0.y, 0.0001);
                            hasPreviousDepthDelta = true;
                            currentTime = nextTime;
                            currentMip = min(maxTraceMip, currentMip + 1);
                            continue;
                        }

                        if (segmentHasMip0RawHit)
                        {
                            float2 resolvedRawUV = proofRayUV;
                            float resolvedDepthDelta = proofDepthDelta;
                            bool shouldResolveProofHit = true;
                            bool proofIsNearSurface = proofNearSurface > 0.5;
                            bool proofIsCrossing = proofCrossedSurface > 0.5;

                            if (proofIsCrossing)
                            {
                                float refinedTime;
                                float2 refinedUV;
                                float refinedDepthDelta;
                                bool refined = BurtSSRRefineScreenHit(
                                    startUV,
                                    deltaUV,
                                    weightedStartWS,
                                    weightedEndWS,
                                    inverseWStart,
                                    inverseWEnd,
                                    descendPreviousTime,
                                    proofRayTime,
                                    refinedTime,
                                    refinedUV,
                                    refinedDepthDelta);
                                bool proofCrossedFromSkyOrOffscreen = descendHasPreviousDepthDelta <= 0.5;
                                shouldResolveProofHit = !proofCrossedFromSkyOrOffscreen || refined || proofIsNearSurface;
                                resolvedRawUV = refined ? refinedUV : proofRayUV;
                                resolvedDepthDelta = refined ? refinedDepthDelta : proofDepthDelta;
                            }

                            if (shouldResolveProofHit)
                            {
                                if (result.rawHit <= 0.0)
                                {
                                    result.rawHit = 1.0;
                                    result.uv = resolvedRawUV;
                                    result.steps = (float)iterationIndex / max((float)iterationLimit, 1.0);
                                    result.depthDelta = resolvedDepthDelta;
                                }

                                float2 baseCandidateUV = BurtSSRPushHitUVInsideSilhouette(resolvedRawUV, deltaUV);
                                float2 candidateUV;
                                float candidateDistance;
                                float candidateWorldError;
                                float candidateSurfaceSupport;
                                if (BurtSSRFindBestResolvedHitCandidate(baseCandidateUV, deltaUV, originWS, reflectionDirectionWS, minTraceDistance, clippedDistance, candidateUV, candidateDistance, candidateWorldError, candidateSurfaceSupport))
                                {
                                    result.hit = 1.0;
                                    result.rawHit = 1.0;
                                    result.uv = candidateUV;
                                    result.steps = (float)iterationIndex / max((float)iterationLimit, 1.0);
                                    result.depthDelta = resolvedDepthDelta;
                                    result.distance = candidateDistance;
                                    result.worldError = candidateWorldError;
                                    result.surfaceSupport = candidateSurfaceSupport;
                                    break;
                                }
                            }

                            currentTime = descendTime;
                            previousTime = descendPreviousTime;
                            previousDepthDelta = descendPreviousDepthDelta;
                            hasPreviousDepthDelta = descendHasPreviousDepthDelta > 0.5;
                        }

                        currentMip = 0;
                        mipLevel = 0.0;
                        nextTime = BurtSSRComputeCellExitTime(startUV, deltaUV, currentTime, mipLevel);
                        nextTime = BurtSSRAdvanceTime(currentTime, nextTime, minTimeStep);
                    }

                    float rayTime = saturate((currentTime + nextTime) * 0.5);
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
                    result.workCost += 9.0;
                    if (!BurtSSRTrySampleRayMarchDepth(rayUV, deltaUV, rayLinearDepth, thickness, frontTolerance, sceneRawDepth))
                    {
                        previousTime = rayTime;
                        hasPreviousDepthDelta = false;
                        currentTime = nextTime;
                        currentMip = min(maxTraceMip, 1);
                        continue;
                    }

                    float depthDelta;
                    depthDelta = rayLinearDepth - LinearEyeDepth(sceneRawDepth);
                    bool farEnoughFromOrigin = travelDistance >= minTraceDistance;
                    if (!farEnoughFromOrigin)
                    {
                        previousTime = rayTime;
                        previousDepthDelta = -max(thickness, 0.0001);
                        hasPreviousDepthDelta = true;
                        currentTime = nextTime;
                        currentMip = min(maxTraceMip, 1);
                        continue;
                    }

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
                            currentTime = nextTime;
                            currentMip = min(maxTraceMip, 1);
                            continue;
                        }

                        if (result.rawHit <= 0.0)
                        {
                            result.rawHit = 1.0;
                            result.uv = refined ? refinedUV : rayUV;
                            result.steps = (float)iterationIndex / max((float)iterationLimit, 1.0);
                            result.depthDelta = refined ? refinedDepthDelta : depthDelta;
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
                            currentTime = nextTime;
                            currentMip = min(maxTraceMip, 1);
                            continue;
                        }

                        result.hit = 1.0;
                        result.rawHit = 1.0;
                        result.uv = candidateUV;
                        result.steps = (float)iterationIndex / max((float)iterationLimit, 1.0);
                        result.depthDelta = refined ? refinedDepthDelta : depthDelta;
                        result.distance = candidateDistance;
                        result.worldError = candidateWorldError;
                        result.surfaceSupport = candidateSurfaceSupport;
                        break;
                    }

                    if (nearSurface)
                    {
                        if (result.rawHit <= 0.0)
                        {
                            result.rawHit = 1.0;
                            result.uv = rayUV;
                            result.steps = (float)iterationIndex / max((float)iterationLimit, 1.0);
                            result.depthDelta = depthDelta;
                        }

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
                            currentTime = nextTime;
                            currentMip = min(maxTraceMip, 1);
                            continue;
                        }

                        result.hit = 1.0;
                        result.rawHit = 1.0;
                        result.uv = candidateUV;
                        result.steps = (float)iterationIndex / max((float)iterationLimit, 1.0);
                        result.depthDelta = depthDelta;
                        result.distance = candidateDistance;
                        result.worldError = candidateWorldError;
                        result.surfaceSupport = candidateSurfaceSupport;
                        break;
                    }

                    previousTime = rayTime;
                    previousDepthDelta = depthDelta;
                    hasPreviousDepthDelta = true;
                    currentTime = nextTime;
                    currentMip = min(maxTraceMip, currentMip + 1);
                }

                return result;
            }

            BurtSSRHit BurtSSRMarch(float3 originWS, float3 reflectionDirectionWS)
            {
                bool useHiZTrace = _BurtSSRParams2.z > 0.5 && _BurtSSRParams2.w > 0.5;
                return BurtSSRMarchInternal(originWS, reflectionDirectionWS, useHiZTrace ? 3 : 0, true);
            }

            BurtSSRHit BurtSSRMarchHiZCandidate(float3 originWS, float3 reflectionDirectionWS)
            {
                return BurtSSRMarchInternal(originWS, reflectionDirectionWS, 1, false);
            }

            float BurtSSREdgeFade(float2 uv)
            {
                float2 edgeDistance = min(uv, 1.0 - uv);
                float edgeFadeWidth = max(_BurtSSRParams1.w, 0.0001);
                return saturate(min(edgeDistance.x, edgeDistance.y) / edgeFadeWidth);
            }

            float BurtSSRComputeTraceVisibility(BurtSSRHit hit, float3 reflectionDirectionWS, float nDotV, float thickness)
            {
                if (hit.hit <= 0.0)
                {
                    return 0.0;
                }

                float edgeFade = BurtSSREdgeFade(hit.uv);
                float hitNormalWeight = BurtSSRHitNormalWeight(hit.uv, reflectionDirectionWS);
                float3 reflectionDirectionVS = BurtSafeNormalize(mul(_BurtSSRViewMatrix, float4(reflectionDirectionWS, 0.0)).xyz);
                float screenParallelWeight = lerp(0.35, 1.0, smoothstep(0.005, 0.08, abs(reflectionDirectionVS.z)));
                float grazingWeight = smoothstep(0.01, 0.06, nDotV);
                float distanceFade = saturate(1.0 - hit.distance / max(_BurtSSRParams0.x, 0.01));
                distanceFade *= distanceFade;
                float depthError = abs(hit.depthDelta) / max(thickness * 1.25, 0.0001);
                float depthQuality = 1.0 - smoothstep(0.85, 1.35, depthError);
                float worldQuality = 1.0 - smoothstep(0.8, 1.6, hit.worldError);
                float surfaceSupportWeight = lerp(0.45, 1.0, smoothstep(0.15, 0.85, hit.surfaceSupport));
                float validHit = saturate(hit.hit * hitNormalWeight * screenParallelWeight * grazingWeight * distanceFade * depthQuality * worldQuality * surfaceSupportWeight);
                return saturate(validHit * edgeFade);
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
                    float rawHitMask = saturate(max(hit.rawHit, hit.hit));
                    return float4(rawHitMask, rawHitMask, rawHitMask, 1.0);
                }

                if (debugMode == 2)
                {
                    return float4(hit.uv * validHit, 0.0, 1.0);
                }

                if (debugMode == 3)
                {
                    float visibleSteps = hit.steps * saturate(max(hit.rawHit, hit.hit));
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

                if (debugMode >= 20 && debugMode <= 30)
                {
                    // HiZ diagnostics stay isolated in the debug shader; this production pass only exposes its final visibility.
                    return float4(0.0, visibilityWeight, 0.0, 1.0);
                }

                if (debugMode == 31)
                {
                    if (roughnessFade * _BurtSSRParams0.z <= 0.0001)
                    {
                        return float4(0.0, 0.0, 0.0, 1.0);
                    }

                    BurtSSRHit stableHit = BurtSSRMarchInternal(originWS, reflectionDirectionWS, 0, false);
                    BurtSSRHit hiZHit = hit;
                    float stableVisible = saturate(stableHit.hit);
                    float hiZVisible = saturate(hiZHit.hit);
                    float commonVisible = stableVisible * hiZVisible;
                    float minStep = 1.0 / max(_BurtSSRParams1.x, 1.0);
                    float stableSteps = max(stableHit.steps, minStep);
                    float hiZSteps = max(hiZHit.steps, minStep);
                    float stepDenominator = max(stableSteps, minStep);
                    float savedSteps = saturate((stableSteps - hiZSteps) / stepDenominator) * commonVisible;
                    float extraSteps = saturate((hiZSteps - stableSteps) / stepDenominator) * commonVisible;
                    float lostHit = stableVisible * (1.0 - hiZVisible);
                    float gainedHit = hiZVisible * (1.0 - stableVisible);
                    return float4(max(extraSteps, lostHit), savedSteps * (1.0 - lostHit), max(gainedHit, commonVisible * 0.25), 1.0);
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
                if ((debugMode > 0 && debugMode <= 7) || (debugMode >= 16 && debugMode <= 31))
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
