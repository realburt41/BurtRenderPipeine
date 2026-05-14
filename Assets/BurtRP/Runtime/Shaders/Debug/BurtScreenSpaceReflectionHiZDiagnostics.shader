Shader "Hidden/BurtRP/ScreenSpaceReflectionHiZDiagnostics"
{
    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        Pass
        {
            Name "Burt Screen Space Reflection HiZ Diagnostics"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtDeferred.hlsl"

            sampler2D _BurtHiZDepthTexture;
            float4 _BurtSSRHiZDiagnosticsParams; // x=debugMode, y=maxMip, z=diagnosticMip, w=depthDiffScale
            float4 _BurtSSRHiZTraceParams0; // x=maxDistance, y=thickness, z=maxSteps, w=roughnessFade
            float4x4 _BurtSSRHiZViewProjectionMatrix;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 screenUV : TEXCOORD0;
            };

            struct BurtSSRHiZTraceResult
            {
                float hit;
                float rawHit;
                float steps;
                float workCost;
                float skipCandidate;
                float skipUsed;
                float skippedStableHit;
                float2 uv;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = BurtGetFullScreenTriangleVertexPosition(input.vertexID);
                output.screenUV = BurtGetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            bool BurtSSRHiZIsSkyRawDepth(float rawDepth)
            {
                #if defined(UNITY_REVERSED_Z)
                    return rawDepth <= 0.00001f;
                #else
                    return rawDepth >= 0.99999f;
                #endif
            }

            float BurtSSRHiZDepthDiff(float rawA, float rawB)
            {
                float a = Linear01Depth(rawA);
                float b = Linear01Depth(rawB);
                return saturate(abs(a - b) * max(_BurtSSRHiZDiagnosticsParams.w, 1.0f));
            }

            float3 BurtSSRHiZHeatColor(float heat)
            {
                heat = saturate(heat);
                float3 cold = float3(0.0f, 0.08f, 0.28f);
                float3 mid = float3(0.0f, 0.78f, 0.35f);
                float3 hot = float3(1.0f, 0.08f, 0.02f);
                return heat < 0.5f ? lerp(cold, mid, heat * 2.0f) : lerp(mid, hot, (heat - 0.5f) * 2.0f);
            }

            float BurtSSRHiZRawDepthFromClip(float clipZ)
            {
                #if defined(UNITY_REVERSED_Z)
                    return clipZ;
                #else
                    return (clipZ - UNITY_NEAR_CLIP_VALUE) / max(1.0f - UNITY_NEAR_CLIP_VALUE, 0.00001f);
                #endif
            }

            float2 BurtSSRHiZClipToScreenUV(float2 clipXY)
            {
                float2 uv = clipXY * 0.5f + 0.5f;
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0f - uv.y;
                #endif

                return uv;
            }

            bool BurtSSRHiZProjectPosition(
                float3 positionWS,
                out float2 screenUV,
                out float rawDepth,
                out float inverseW,
                out float3 weightedPositionWS)
            {
                float4 clip = mul(_BurtSSRHiZViewProjectionMatrix, float4(positionWS, 1.0f));
                if (clip.w <= 0.00001f)
                {
                    screenUV = 0.0f;
                    rawDepth = 0.0f;
                    inverseW = 0.0f;
                    weightedPositionWS = 0.0f;
                    return false;
                }

                inverseW = rcp(clip.w);
                float3 ndc = clip.xyz * inverseW;
                screenUV = BurtSSRHiZClipToScreenUV(ndc.xy);
                rawDepth = BurtSSRHiZRawDepthFromClip(ndc.z);
                weightedPositionWS = positionWS * inverseW;
                return true;
            }

            float3 BurtSSRHiZInterpolateRayPosition(
                float rayTime,
                float3 weightedStartWS,
                float3 weightedEndWS,
                float inverseWStart,
                float inverseWEnd)
            {
                float inverseW = lerp(inverseWStart, inverseWEnd, rayTime);
                float safeInverseW = abs(inverseW) > 0.000001f ? inverseW : (inverseW < 0.0f ? -0.000001f : 0.000001f);
                return lerp(weightedStartWS, weightedEndWS, rayTime) / safeInverseW;
            }

            float BurtSSRHiZClipScreenRay(float2 startUV, float2 deltaUV, float startRawDepth, float deltaRawDepth)
            {
                float rayScale = 1.0f;

                if (deltaUV.x > 0.000001f)
                {
                    rayScale = min(rayScale, (1.0f - startUV.x) / deltaUV.x);
                }
                else if (deltaUV.x < -0.000001f)
                {
                    rayScale = min(rayScale, -startUV.x / deltaUV.x);
                }

                if (deltaUV.y > 0.000001f)
                {
                    rayScale = min(rayScale, (1.0f - startUV.y) / deltaUV.y);
                }
                else if (deltaUV.y < -0.000001f)
                {
                    rayScale = min(rayScale, -startUV.y / deltaUV.y);
                }

                if (deltaRawDepth > 0.000001f)
                {
                    rayScale = min(rayScale, (1.0f - startRawDepth) / deltaRawDepth);
                }
                else if (deltaRawDepth < -0.000001f)
                {
                    rayScale = min(rayScale, -startRawDepth / deltaRawDepth);
                }

                return saturate(rayScale);
            }

            float BurtSSRHiZComputeCellExitTime(float2 startUV, float2 deltaUV, float currentTime, float mipLevel)
            {
                float2 screenSize = max(_BurtDeferredScreenSize.xy, float2(1.0f, 1.0f));
                float2 startPixel = startUV * screenSize;
                float2 deltaPixel = deltaUV * screenSize;
                float2 currentPixel = startPixel + deltaPixel * currentTime;
                float cellSize = exp2(mipLevel);
                float2 directionSign = float2(deltaPixel.x >= 0.0f ? 1.0f : -1.0f, deltaPixel.y >= 0.0f ? 1.0f : -1.0f);
                float2 biasedPixel = currentPixel + directionSign * 0.001f;
                float2 cellCoord = floor(biasedPixel / cellSize);
                float2 nextBoundary = float2(
                    deltaPixel.x >= 0.0f ? (cellCoord.x + 1.0f) * cellSize : cellCoord.x * cellSize,
                    deltaPixel.y >= 0.0f ? (cellCoord.y + 1.0f) * cellSize : cellCoord.y * cellSize);
                float2 boundaryTime = 999999.0f;

                if (abs(deltaPixel.x) > 0.00001f)
                {
                    boundaryTime.x = (nextBoundary.x - startPixel.x) / deltaPixel.x;
                }

                if (abs(deltaPixel.y) > 0.00001f)
                {
                    boundaryTime.y = (nextBoundary.y - startPixel.y) / deltaPixel.y;
                }

                return clamp(min(boundaryTime.x, boundaryTime.y), currentTime, 1.0f);
            }

            float BurtSSRHiZAdvanceTime(float currentTime, float nextTime, float minTimeStep)
            {
                return min(1.0f, max(nextTime, currentTime + minTimeStep));
            }

            bool BurtSSRHiZTrySampleRawDepth(float2 screenUV, float mipLevel, out float rawDepth)
            {
                if (!all(screenUV >= 0.0f) || !all(screenUV <= 1.0f))
                {
                    rawDepth = 0.0f;
                    return false;
                }

                rawDepth = mipLevel <= 0.5f ?
                    BurtSampleDeferredRawDepth(screenUV) :
                    tex2Dlod(_BurtHiZDepthTexture, float4(screenUV, 0.0f, mipLevel)).r;

                return !BurtSSRHiZIsSkyRawDepth(rawDepth);
            }

            float BurtSSRHiZDepthDelta(float rayRawDepth, float sceneRawDepth)
            {
                return LinearEyeDepth(rayRawDepth) - LinearEyeDepth(sceneRawDepth);
            }

            BurtSSRHiZTraceResult BurtSSRHiZCreateEmptyTraceResult()
            {
                BurtSSRHiZTraceResult result;
                result.hit = 0.0f;
                result.rawHit = 0.0f;
                result.steps = 0.0f;
                result.workCost = 0.0f;
                result.skipCandidate = 0.0f;
                result.skipUsed = 0.0f;
                result.skippedStableHit = 0.0f;
                result.uv = 0.0f;
                return result;
            }

            bool BurtSSRHiZSegmentHasMip0Risk(
                float2 startUV,
                float2 deltaUV,
                float3 weightedStartWS,
                float3 weightedEndWS,
                float inverseWStart,
                float inverseWEnd,
                float segmentStartTime,
                float segmentEndTime,
                float thickness,
                float minTimeStep,
                out float guardCost,
                out float guardTime)
            {
                guardCost = 0.0f;
                guardTime = segmentEndTime;
                float probeTime = segmentStartTime;
                float nearSurfaceThreshold = max(thickness * 0.35f, 0.05f);

                [loop]
                for (int guardIndex = 0; guardIndex < 12; guardIndex++)
                {
                    if (probeTime >= segmentEndTime - 0.000001f)
                    {
                        break;
                    }

                    float nextProbeTime = BurtSSRHiZComputeCellExitTime(startUV, deltaUV, probeTime, 0.0f);
                    nextProbeTime = BurtSSRHiZAdvanceTime(probeTime, nextProbeTime, minTimeStep);
                    nextProbeTime = min(nextProbeTime, segmentEndTime);

                    float2 probeUV = startUV + deltaUV * nextProbeTime;
                    float sceneRawDepth;
                    guardCost += 1.0f;
                    if (!BurtSSRHiZTrySampleRawDepth(probeUV, 0.0f, sceneRawDepth))
                    {
                        probeTime = nextProbeTime;
                        continue;
                    }

                    float3 probePositionWS = BurtSSRHiZInterpolateRayPosition(nextProbeTime, weightedStartWS, weightedEndWS, inverseWStart, inverseWEnd);
                    float rayRawDepth;
                    float2 projectedUV;
                    float inverseW;
                    float3 weightedPositionWS;
                    BurtSSRHiZProjectPosition(probePositionWS, projectedUV, rayRawDepth, inverseW, weightedPositionWS);

                    float depthDelta = BurtSSRHiZDepthDelta(rayRawDepth, sceneRawDepth);
                    bool rawHit = depthDelta >= 0.0f && depthDelta <= thickness * 1.5f;
                    if (rawHit || depthDelta >= -nearSurfaceThreshold)
                    {
                        guardTime = nextProbeTime;
                        return true;
                    }

                    probeTime = nextProbeTime;
                }

                return false;
            }

            bool BurtSSRHiZBuildRay(
                float3 originWS,
                float3 reflectionDirectionWS,
                out float2 startUV,
                out float2 deltaUV,
                out float3 weightedStartWS,
                out float3 weightedEndWS,
                out float inverseWStart,
                out float inverseWEnd)
            {
                float maxDistance = max(_BurtSSRHiZTraceParams0.x, 0.01f);
                float startRawDepth;
                if (!BurtSSRHiZProjectPosition(originWS, startUV, startRawDepth, inverseWStart, weightedStartWS))
                {
                    deltaUV = 0.0f;
                    weightedEndWS = 0.0f;
                    inverseWEnd = 0.0f;
                    return false;
                }

                if (!all(startUV >= 0.0f) || !all(startUV <= 1.0f) || startRawDepth < 0.0f || startRawDepth > 1.0f)
                {
                    deltaUV = 0.0f;
                    weightedEndWS = 0.0f;
                    inverseWEnd = 0.0f;
                    return false;
                }

                float2 endUV;
                float endRawDepth;
                if (!BurtSSRHiZProjectPosition(originWS + reflectionDirectionWS * maxDistance, endUV, endRawDepth, inverseWEnd, weightedEndWS))
                {
                    deltaUV = 0.0f;
                    return false;
                }

                float2 fullDeltaUV = endUV - startUV;
                float fullDeltaRawDepth = endRawDepth - startRawDepth;
                float rayScale = BurtSSRHiZClipScreenRay(startUV, fullDeltaUV, startRawDepth, fullDeltaRawDepth);
                if (rayScale <= 0.0001f)
                {
                    deltaUV = 0.0f;
                    return false;
                }

                deltaUV = fullDeltaUV * rayScale;
                weightedEndWS = lerp(weightedStartWS, weightedEndWS, rayScale);
                inverseWEnd = lerp(inverseWStart, inverseWEnd, rayScale);
                return true;
            }

            BurtSSRHiZTraceResult BurtSSRHiZTraceRay(float3 originWS, float3 reflectionDirectionWS, bool useCandidate, float diagnosticMip)
            {
                BurtSSRHiZTraceResult result = BurtSSRHiZCreateEmptyTraceResult();

                float2 startUV;
                float2 deltaUV;
                float3 weightedStartWS;
                float3 weightedEndWS;
                float inverseWStart;
                float inverseWEnd;
                if (!BurtSSRHiZBuildRay(originWS, reflectionDirectionWS, startUV, deltaUV, weightedStartWS, weightedEndWS, inverseWStart, inverseWEnd))
                {
                    return result;
                }

                int maxSteps = min(96, max(1, (int)round(_BurtSSRHiZTraceParams0.z)));
                float thickness = max(_BurtSSRHiZTraceParams0.y, 0.0001f);
                float2 screenDelta = deltaUV * max(_BurtDeferredScreenSize.xy, float2(1.0f, 1.0f));
                float screenMajorSpan = max(abs(screenDelta.x), abs(screenDelta.y));
                float minTimeStep = 0.25f / max(screenMajorSpan, 1.0f);
                float currentTime = 0.0f;
                float previousTime = 0.0f;
                float previousDepthDelta = -thickness;
                float skipDepthThreshold = max(thickness * 0.35f, 0.05f);

                [loop]
                for (int iterationIndex = 1; iterationIndex <= 96; iterationIndex++)
                {
                    if (iterationIndex > maxSteps || currentTime >= 1.0f)
                    {
                        break;
                    }

                    result.workCost += 1.0f;
                    bool candidateCanSkip = useCandidate && diagnosticMip >= 1.0f && previousTime > 0.0f && previousDepthDelta < -skipDepthThreshold;
                    float nextTime = BurtSSRHiZComputeCellExitTime(startUV, deltaUV, currentTime, candidateCanSkip ? diagnosticMip : 0.0f);
                    nextTime = BurtSSRHiZAdvanceTime(currentTime, nextTime, minTimeStep);
                    float rayTime = nextTime;
                    float2 rayUV = startUV + deltaUV * rayTime;
                    float3 rayPositionWS = BurtSSRHiZInterpolateRayPosition(rayTime, weightedStartWS, weightedEndWS, inverseWStart, inverseWEnd);

                    if (candidateCanSkip)
                    {
                        float coarseRawDepth;
                        if (BurtSSRHiZTrySampleRawDepth(rayUV, diagnosticMip, coarseRawDepth))
                        {
                            float rayRawDepth;
                            float2 projectedUV;
                            float inverseW;
                            float3 weightedPositionWS;
                            BurtSSRHiZProjectPosition(rayPositionWS, projectedUV, rayRawDepth, inverseW, weightedPositionWS);
                            float coarseDepthDelta = BurtSSRHiZDepthDelta(rayRawDepth, coarseRawDepth);
                            result.skipCandidate = max(result.skipCandidate, coarseDepthDelta < -skipDepthThreshold ? 1.0f : 0.0f);
                            if (coarseDepthDelta < -skipDepthThreshold)
                            {
                                float guardCost;
                                float guardTime;
                                bool mip0Risk = BurtSSRHiZSegmentHasMip0Risk(
                                    startUV,
                                    deltaUV,
                                    weightedStartWS,
                                    weightedEndWS,
                                    inverseWStart,
                                    inverseWEnd,
                                    currentTime,
                                    nextTime,
                                    thickness,
                                    minTimeStep,
                                    guardCost,
                                    guardTime);
                                result.workCost += guardCost;
                                result.skippedStableHit = max(result.skippedStableHit, mip0Risk ? 1.0f : 0.0f);
                                if (!mip0Risk)
                                {
                                    result.skipUsed = 1.0f;
                                    currentTime = nextTime;
                                    previousTime = nextTime;
                                    previousDepthDelta = -thickness;
                                    continue;
                                }

                                nextTime = clamp(guardTime, currentTime, nextTime);
                                rayTime = nextTime;
                                rayUV = startUV + deltaUV * rayTime;
                                rayPositionWS = BurtSSRHiZInterpolateRayPosition(rayTime, weightedStartWS, weightedEndWS, inverseWStart, inverseWEnd);
                            }
                        }
                    }

                    float sceneRawDepth;
                    if (!BurtSSRHiZTrySampleRawDepth(rayUV, 0.0f, sceneRawDepth))
                    {
                        currentTime = nextTime;
                        previousTime = nextTime;
                        previousDepthDelta = -thickness;
                        continue;
                    }

                    float rayRawDepth;
                    float2 projectedUV;
                    float inverseW;
                    float3 weightedPositionWS;
                    BurtSSRHiZProjectPosition(rayPositionWS, projectedUV, rayRawDepth, inverseW, weightedPositionWS);
                    float depthDelta = BurtSSRHiZDepthDelta(rayRawDepth, sceneRawDepth);
                    bool rawHit = depthDelta >= 0.0f && depthDelta <= thickness * 1.5f;
                    if (rawHit)
                    {
                        result.hit = 1.0f;
                        result.rawHit = 1.0f;
                        result.uv = rayUV;
                        result.steps = (float)iterationIndex / max((float)maxSteps, 1.0f);
                        return result;
                    }

                    previousTime = rayTime;
                    previousDepthDelta = depthDelta;
                    currentTime = nextTime;
                }

                result.steps = 1.0f;
                return result;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 screenUV = saturate(input.screenUV);
                int debugMode = (int)round(_BurtSSRHiZDiagnosticsParams.x);
                float maxMip = max(_BurtSSRHiZDiagnosticsParams.y, 1.0f);
                float diagnosticMip = clamp(round(_BurtSSRHiZDiagnosticsParams.z), 0.0f, maxMip);

                float cameraRawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtSSRHiZIsSkyRawDepth(cameraRawDepth))
                {
                    return float4(0.0f, 0.0f, 0.0f, 1.0f);
                }

                float hiZMip0RawDepth = tex2Dlod(_BurtHiZDepthTexture, float4(screenUV, 0.0f, 0.0f)).r;
                float hiZCoarseRawDepth = tex2Dlod(_BurtHiZDepthTexture, float4(screenUV, 0.0f, diagnosticMip)).r;
                float mip0Divergence = BurtSSRHiZDepthDiff(cameraRawDepth, hiZMip0RawDepth);
                float coarseDivergence = BurtSSRHiZDepthDiff(cameraRawDepth, hiZCoarseRawDepth);
                float stable = 1.0f - mip0Divergence;
                float mipWeight = saturate(diagnosticMip / max(maxMip, 1.0f));

                BurtGBufferData gbufferData = BurtDecodeGBuffer(BurtSampleEncodedGBuffer(screenUV));
                float roughnessFade = saturate((_BurtSSRHiZTraceParams0.w - gbufferData.perceptualRoughness) / max(_BurtSSRHiZTraceParams0.w, 0.0001f));
                float3 positionWS = BurtReconstructDeferredPositionWS(screenUV, cameraRawDepth);
                float3 viewDirectionWS = BurtSafeNormalize(_BurtDeferredCameraWorldPosition.xyz - positionWS);
                float3 normalWS = BurtSafeNormalize(gbufferData.normalWS);
                float3 reflectionDirectionWS = BurtSafeNormalize(reflect(-viewDirectionWS, normalWS));
                float thickness = max(_BurtSSRHiZTraceParams0.y, 0.0001f);
                float originBias = min(thickness * 0.08f, 0.025f);
                float3 originWS = positionWS + normalWS * originBias + reflectionDirectionWS * originBias;
                BurtSSRHiZTraceResult stableTrace = BurtSSRHiZCreateEmptyTraceResult();
                BurtSSRHiZTraceResult candidateTrace = BurtSSRHiZCreateEmptyTraceResult();
                if (roughnessFade > 0.0001f)
                {
                    stableTrace = BurtSSRHiZTraceRay(originWS, reflectionDirectionWS, false, 0.0f);
                    candidateTrace = BurtSSRHiZTraceRay(originWS, reflectionDirectionWS, true, diagnosticMip);
                }

                float stableHit = saturate(stableTrace.hit);
                float candidateHit = saturate(candidateTrace.hit);
                float missedHit = saturate(stableHit * (1.0f - candidateHit));
                float extraCandidateHit = saturate(candidateHit * (1.0f - stableHit));
                float candidateWork = max(candidateTrace.workCost, 0.0001f);
                float stableWork = max(stableTrace.workCost, 0.0001f);
                float candidateCheaper = saturate((stableWork - candidateWork) / max(stableWork, 1.0f));
                float candidateCostlier = saturate((candidateWork - stableWork) / max(stableWork, 1.0f));

                if (debugMode == 20)
                {
                    return float4(BurtSSRHiZHeatColor(candidateTrace.skipCandidate), 1.0f);
                }

                if (debugMode == 21)
                {
                    float depthView = 1.0f - Linear01Depth(hiZCoarseRawDepth);
                    return float4(depthView, depthView, depthView, 1.0f);
                }

                if (debugMode == 22)
                {
                    return float4(missedHit, stableHit * (1.0f - missedHit), extraCandidateHit, 1.0f);
                }

                if (debugMode >= 23 && debugMode <= 26)
                {
                    return float4(missedHit, candidateHit * (1.0f - missedHit), stableHit * 0.35f, 1.0f);
                }

                if (debugMode == 27)
                {
                    return float4(0.0f, candidateTrace.skipUsed, stableHit * 0.25f + mipWeight * 0.25f, 1.0f);
                }

                if (debugMode == 28)
                {
                    return float4(missedHit * candidateTrace.skipUsed, stableHit * (1.0f - missedHit), candidateTrace.skipCandidate * 0.5f, 1.0f);
                }

                if (debugMode == 29 || debugMode == 30)
                {
                    return float4(candidateCostlier, candidateCheaper, stableHit * candidateHit * 0.25f, 1.0f);
                }

                return float4(0.0f, stable, 0.0f, 1.0f);
            }
            ENDHLSL
        }
    }
}
