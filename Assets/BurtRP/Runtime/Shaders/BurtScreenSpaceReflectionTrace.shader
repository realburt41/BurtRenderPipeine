Shader "Hidden/BurtRP/ScreenSpaceReflections/Trace"
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

            struct BurtSSRTraceQuality
            {
                float edgeFade;
                float hitNormalWeight;
                float screenParallelWeight;
                float grazingWeight;
                float distanceFade;
                float depthError;
                float depthQuality;
                float worldQuality;
                float surfaceSupportWeight;
                float resolveQuality;
                float validHit;
                float visibilityWeight;
            };

            struct BurtSSRLayerSample
            {
                BurtSSRHit hit;
                BurtSSRTraceQuality quality;
                float3 color;
                float visibility;
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

                [loop]
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
                float3 hitNormalWS = BurtGetReflectionNormalWS(hitGBufferData);
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

                [loop]
                for (int sampleIndex = 0; sampleIndex < 5; sampleIndex++)
                {
                    float2 offset = sampleIndex == 0 ? float2(0.0, 0.0) :
                        sampleIndex == 1 ? float2(1.0, 0.0) :
                        sampleIndex == 2 ? float2(-1.0, 0.0) :
                        sampleIndex == 3 ? float2(0.0, 1.0) :
                        float2(0.0, -1.0);
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

                [loop]
                for (int sampleIndex = 0; sampleIndex < 5; sampleIndex++)
                {
                    float2 offset = sampleIndex == 0 ? float2(0.0, 0.0) :
                        sampleIndex == 1 ? sideUV :
                        sampleIndex == 2 ? -sideUV :
                        sampleIndex == 3 ? alongUV :
                        -alongUV;
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
                [loop]
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

                [loop]
                for (int sampleIndex = 0; sampleIndex < 5; sampleIndex++)
                {
                    float2 offset = sampleIndex == 0 ? float2(0.0, 0.0) :
                        sampleIndex == 1 ? sideUV :
                        sampleIndex == 2 ? -sideUV :
                        sampleIndex == 3 ? alongUV :
                        -alongUV;
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

                    float distancePenalty = sampleIndex == 0 ? 0.0 : 0.04;
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
                    probeWorkCost += 5.0;
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
                [loop]
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
                float normalSupport = saturate(dot(centerNormal, BurtGetReflectionNormalWS(sampleGBuffer)));
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
                float3 centerNormal = BurtGetReflectionNormalWS(centerGBuffer);
                float depthTolerance = max(centerLinearDepth * 0.025, 0.035);
                float2 rayPixels = rayDeltaUV * _BurtSSRSourceTexelSize.zw;
                float rayPixelLength = length(rayPixels);
                float2 alongUV = rayPixelLength > 0.0001 ? rayDeltaUV / rayPixelLength : float2(_BurtSSRSourceTexelSize.x, 0.0);
                float2 sideUV = float2(-alongUV.y, alongUV.x);

                float sideA = BurtSSRSameSurfaceSupport(hitUV + sideUV, centerLinearDepth, centerNormal, depthTolerance);
                float sideB = BurtSSRSameSurfaceSupport(hitUV - sideUV, centerLinearDepth, centerNormal, depthTolerance);
                float alongF = BurtSSRSameSurfaceSupport(hitUV + alongUV, centerLinearDepth, centerNormal, depthTolerance);
                float alongB = BurtSSRSameSurfaceSupport(hitUV - alongUV, centerLinearDepth, centerNormal, depthTolerance);
                float pairedSupport = max(min(sideA, sideB), min(alongF, alongB));
                float totalSupport = sideA + sideB + alongF + alongB;
                return saturate(max(totalSupport * 0.5, pairedSupport));
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

                [loop]
                for (int sampleIndex = 0; sampleIndex < 9; sampleIndex++)
                {
                    float2 offset = sampleIndex == 0 ? float2(0.0, 0.0) :
                        sampleIndex == 1 ? sideUV :
                        sampleIndex == 2 ? -sideUV :
                        sampleIndex == 3 ? alongUV :
                        sampleIndex == 4 ? -alongUV :
                        sampleIndex == 5 ? alongUV + sideUV :
                        sampleIndex == 6 ? alongUV - sideUV :
                        sampleIndex == 7 ? -alongUV + sideUV :
                        -alongUV - sideUV;
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

                [loop]
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

                const int traceStepLimit = 128;
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
                int screenStepEstimate = (int)ceil(max(screenMajorSpan, 1.0) * 0.5);
                int iterationLimit = min(traceStepLimit, max(requestedSteps, min(screenStepEstimate, requestedSteps * 2)));
                float minTimeStep = rcp(max((float)iterationLimit, 1.0));
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
                            result.workCost += 5.0;
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
                    result.workCost += 5.0;
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
                return BurtSSRMarchInternal(originWS, reflectionDirectionWS, useHiZTrace ? 3 : 0, useHiZTrace);
            }

            BurtSSRHit BurtSSRMarchHiZCandidate(float3 originWS, float3 reflectionDirectionWS)
            {
                return BurtSSRMarchInternal(originWS, reflectionDirectionWS, 1, false);
            }

            float BurtSSREdgeFade(float2 uv)
            {
                float2 edgeDistance = min(uv, 1.0 - uv);
                float edgeFadeWidth = max(_BurtSSRParams1.w, 0.0001);
                float fade = saturate(min(edgeDistance.x, edgeDistance.y) / edgeFadeWidth);
                return fade * fade * (3.0 - 2.0 * fade);
            }

            float BurtSSRHitContinuityWeight(float surfaceSupport, float depthError, float worldError)
            {
                float supportGate = smoothstep(0.12, 0.85, surfaceSupport);
                float errorGate = smoothstep(0.45, 1.35, max(depthError, worldError));
                float lowSupportFloor = lerp(0.55, 0.28, errorGate);
                return lerp(lowSupportFloor, 1.0, supportGate);
            }

            BurtSSRTraceQuality BurtSSREvaluateTraceQuality(BurtSSRHit hit, float3 reflectionDirectionWS, float nDotV, float thickness)
            {
                BurtSSRTraceQuality quality;
                quality.edgeFade = hit.hit > 0.0 ? BurtSSREdgeFade(hit.uv) : 0.0;
                quality.hitNormalWeight = hit.hit > 0.0 ? BurtSSRHitNormalWeight(hit.uv, reflectionDirectionWS) : 0.0;
                float3 reflectionDirectionVS = BurtSafeNormalize(mul(_BurtSSRViewMatrix, float4(reflectionDirectionWS, 0.0)).xyz);
                quality.screenParallelWeight = lerp(0.35, 1.0, smoothstep(0.005, 0.08, abs(reflectionDirectionVS.z)));
                quality.grazingWeight = smoothstep(0.01, 0.06, nDotV);
                quality.distanceFade = hit.hit > 0.0 ? saturate(1.0 - hit.distance / max(_BurtSSRParams0.x, 0.01)) : 0.0;
                quality.distanceFade *= quality.distanceFade;
                quality.depthError = hit.hit > 0.0 ? abs(hit.depthDelta) / max(thickness * 1.25, 0.0001) : 999.0;
                quality.depthQuality = hit.hit > 0.0 ? 1.0 - smoothstep(0.85, 1.35, quality.depthError) : 0.0;
                quality.worldQuality = hit.hit > 0.0 ? 1.0 - smoothstep(0.8, 1.6, hit.worldError) : 0.0;
                quality.surfaceSupportWeight = hit.hit > 0.0 ? BurtSSRHitContinuityWeight(hit.surfaceSupport, quality.depthError, hit.worldError) : 0.0;
                quality.resolveQuality = quality.depthQuality * quality.worldQuality;
                quality.validHit = saturate(hit.hit * quality.hitNormalWeight * quality.screenParallelWeight * quality.grazingWeight * quality.distanceFade * quality.resolveQuality * quality.surfaceSupportWeight);
                quality.visibilityWeight = saturate(quality.validHit * quality.edgeFade);
                return quality;
            }

            float BurtSSRComputeTraceVisibility(BurtSSRHit hit, float3 reflectionDirectionWS, float nDotV, float thickness)
            {
                BurtSSRTraceQuality quality = BurtSSREvaluateTraceQuality(hit, reflectionDirectionWS, nDotV, thickness);
                return quality.visibilityWeight;
            }

            float BurtSSRComputeHiZValidationWeight(BurtSSRHit hit, BurtSSRTraceQuality quality)
            {
                if (hit.hit <= 0.0 || hit.hiZSkipUsed <= 0.0)
                {
                    return 0.0;
                }

                float lowSupportRisk = 1.0 - smoothstep(0.2, 0.75, hit.surfaceSupport);
                float depthRisk = smoothstep(0.55, 1.15, quality.depthError);
                float worldRisk = smoothstep(0.55, 1.25, hit.worldError);
                float lowVisibilityRisk = 1.0 - smoothstep(0.05, 0.22, quality.visibilityWeight);
                float continuityRisk = max(max(depthRisk, worldRisk), lowSupportRisk * 0.8);
                return saturate(hit.hiZSkipUsed * max(continuityRisk, lowVisibilityRisk * 0.6));
            }

            BurtSSRHit BurtSSRValidateHiZHit(
                float3 originWS,
                float3 reflectionDirectionWS,
                float nDotV,
                float thickness,
                BurtSSRHit hit,
                BurtSSRTraceQuality quality,
                out BurtSSRTraceQuality outputQuality)
            {
                outputQuality = quality;
                if (_BurtSSRParams2.z <= 0.5 || _BurtSSRParams2.w <= 0.5)
                {
                    return hit;
                }

                float validationWeight = BurtSSRComputeHiZValidationWeight(hit, quality);
                if (validationWeight <= 0.0)
                {
                    return hit;
                }

                // Avoid a second full ray march inside the trace fragment; suspicious HiZ hits are damped instead.
                float suppression = validationWeight;
                outputQuality.validHit *= lerp(1.0, 0.45, suppression);
                outputQuality.visibilityWeight = saturate(outputQuality.validHit * outputQuality.edgeFade);
                return hit;
            }

            BurtSSRLayerSample BurtSSRTraceLayer(
                float3 positionWS,
                float3 viewDirectionWS,
                float3 normalWS,
                float perceptualRoughness,
                float thickness,
                float nDotV,
                float roughnessIntensity)
            {
                BurtSSRLayerSample layer;
                BurtPBRGeometryData geometryData = BurtPreparePBRGeometryData(normalWS, viewDirectionWS);
                float3 reflectionDirectionWS = BurtGetIndirectSpecularReflectionDirectionWS(geometryData, 0.0, perceptualRoughness);
                float originBias = min(thickness * 0.08, 0.025);
                float3 originWS = positionWS + normalWS * originBias + reflectionDirectionWS * originBias;
                layer.hit = BurtSSRCreateEmptyHit();
                if (roughnessIntensity > 0.0001)
                {
                    layer.hit = BurtSSRMarch(originWS, reflectionDirectionWS);
                }
                layer.quality = BurtSSREvaluateTraceQuality(layer.hit, reflectionDirectionWS, nDotV, thickness);
                BurtSSRTraceQuality validatedQuality;
                layer.hit = BurtSSRValidateHiZHit(originWS, reflectionDirectionWS, nDotV, thickness, layer.hit, layer.quality, validatedQuality);
                layer.quality = validatedQuality;
                layer.color = tex2D(_BurtSSRSourceColorTexture, layer.hit.uv).rgb * layer.hit.hit;
                layer.visibility = layer.quality.visibilityWeight;
                return layer;
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
                if (BurtIsActiveHairShadingModel(gbufferData.shadingModelID) || BurtIsActiveFurShadingModel(gbufferData.shadingModelID))
                {
                    bool traceDebugMode = (debugMode > 0 && debugMode <= 8) || (debugMode >= 16 && debugMode <= 31);
                    return traceDebugMode ? float4(0.0, 0.0, 0.0, 1.0) : float4(0.0, 0.0, 0.0, 0.0);
                }

                float3 positionWS = BurtReconstructDeferredPositionWS(screenUV, rawDepth);
                float3 viewDirectionWS = BurtSafeNormalize(_BurtDeferredCameraWorldPosition.xyz - positionWS);
                float3 normalWS = BurtGetReflectionNormalWS(gbufferData);
                float nDotV = saturate(dot(normalWS, viewDirectionWS));
                float reflectionRoughness = BurtGetReflectionRoughness(gbufferData);
                float roughnessFade = saturate((_BurtSSRParams0.w - reflectionRoughness) / max(_BurtSSRParams0.w, 0.0001));
                float roughnessIntensity = roughnessFade * _BurtSSRParams0.z;
                if (debugMode == 0)
                {
                    if (roughnessIntensity <= 0.0001 || nDotV <= 0.01)
                    {
                        return float4(0.0, 0.0, 0.0, 0.0);
                    }

                    BurtPBRMaterialData materialData = BurtPreparePBRMaterialData(gbufferData);
                    float3 fresnel = F_Schlick(materialData.f0, materialData.f90, nDotV);
                    float materialWeight = saturate(max(max(fresnel.r, fresnel.g), fresnel.b) * roughnessIntensity);
                    if (materialWeight <= 0.002)
                    {
                        return float4(0.0, 0.0, 0.0, 0.0);
                    }
                }

                float thickness = max(_BurtSSRParams0.y, 0.0001);
                BurtSSRLayerSample baseLayer = BurtSSRTraceLayer(positionWS, viewDirectionWS, normalWS, reflectionRoughness, thickness, nDotV, roughnessIntensity);
                BurtSSRHit hit = baseLayer.hit;
                BurtSSRTraceQuality traceQuality = baseLayer.quality;
                float edgeFade = traceQuality.edgeFade;
                float hitNormalWeight = traceQuality.hitNormalWeight;
                float screenParallelWeight = traceQuality.screenParallelWeight;
                float grazingWeight = traceQuality.grazingWeight;
                float distanceFade = traceQuality.distanceFade;
                float depthError = traceQuality.depthError;
                float depthQuality = traceQuality.depthQuality;
                float worldQuality = traceQuality.worldQuality;
                float resolveQuality = traceQuality.resolveQuality;
                float validHit = traceQuality.validHit;
                float visibilityWeight = traceQuality.visibilityWeight;
                float3 reflectionColor = baseLayer.color;

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
                    if (roughnessIntensity <= 0.0001)
                    {
                        return float4(0.0, 0.0, 0.0, 1.0);
                    }

                    float skipUsed = saturate(hit.hiZSkipUsed);
                    float skipCandidate = saturate(hit.hiZSkipCandidate);
                    float divergence = saturate(hit.hiZDivergence);
                    float probeBlocked = saturate(hit.hiZProbeBlocked);
                    return float4(max(divergence, probeBlocked), skipUsed * (1.0 - divergence), skipCandidate, 1.0);
                }

                #if BURT_ENABLE_CLEAR_COAT_SHADING
                    BurtPBRMaterialData clearCoatMaterialData = BurtPreparePBRMaterialData(gbufferData);
                    float clearCoatMask = saturate(clearCoatMaterialData.clearCoatMask);
                    if (clearCoatMask > 0.0001)
                    {
                        float3 clearCoatNormalWS = BurtGetClearCoatNormalWS(gbufferData);
                        float clearCoatRoughness = ClampPerceptualRoughness(clearCoatMaterialData.clearCoatRoughness);
                        float clearCoatNoV = saturate(dot(clearCoatNormalWS, viewDirectionWS));
                        float clearCoatRoughnessIntensity = saturate((_BurtSSRParams0.w - clearCoatRoughness) / max(_BurtSSRParams0.w, 0.0001)) * saturate(_BurtSSRParams0.z);
                        BurtSSRLayerSample topLayer = BurtSSRTraceLayer(positionWS, viewDirectionWS, clearCoatNormalWS, clearCoatRoughness, thickness, clearCoatNoV, clearCoatRoughnessIntensity);

                        BurtPBRMaterialData topMaterialData = clearCoatMaterialData;
                        topMaterialData.baseColor = float3(1.0, 1.0, 1.0);
                        topMaterialData.metallic = 0.0;
                        topMaterialData.anisotropy = 0.0;
                        topMaterialData.reflectance = BURT_INPUT_DEFAULT_REFLECTANCE;
                        topMaterialData.diffuseColor = float3(0.0, 0.0, 0.0);
                        topMaterialData.f0 = float3(BURT_CLEAR_COAT_F0, BURT_CLEAR_COAT_F0, BURT_CLEAR_COAT_F0);
                        topMaterialData.f90 = float3(1.0, 1.0, 1.0);
                        topMaterialData.perceptualRoughness = clearCoatRoughness;
                        topMaterialData.linearRoughness = PerceptualRoughnessToLinearRoughness(clearCoatRoughness);
                        topMaterialData.a2 = LinearRoughnessToA2(topMaterialData.linearRoughness);
                        topMaterialData.clearCoatMask = 0.0;

                        float2 topDFG = GetSpecularDFGTerms(clearCoatRoughness, clearCoatNoV);
                        float3 topEnvBRDF = EvalSpecularDFG(topMaterialData.f0, topMaterialData.f90, topDFG);
                        float3 layerTransmission = BurtClearCoatFresnelTransmission(topEnvBRDF) * BurtSimpleClearCoatTransmittanceFromView(clearCoatNoV, clearCoatMaterialData.metallic, clearCoatMaterialData.baseColor);
                        float transmissionWeight = saturate(max(max(layerTransmission.r, layerTransmission.g), layerTransmission.b));
                        float baseLayerConfidence = smoothstep(0.01, 0.12, baseLayer.visibility);
                        float topLayerConfidence = smoothstep(0.01, 0.12, topLayer.visibility);
                        float3 safeBaseLayerColor = lerp(topLayer.color, baseLayer.color, baseLayerConfidence);
                        float3 safeTopLayerColor = lerp(safeBaseLayerColor, topLayer.color, topLayerConfidence);
                        float3 dualLayerColor = safeBaseLayerColor * layerTransmission + safeTopLayerColor * clearCoatMask;
                        float dualLayerVisibility = saturate(max(baseLayer.visibility * transmissionWeight, topLayer.visibility * clearCoatMask));
                        reflectionColor = lerp(baseLayer.color, dualLayerColor, clearCoatMask);
                        visibilityWeight = lerp(baseLayer.visibility, dualLayerVisibility, clearCoatMask);
                    }
                #endif

                return float4(reflectionColor, visibilityWeight);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
