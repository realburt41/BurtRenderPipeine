// BurtRP hidden SSAO shader. It is intentionally simple: depth + GBuffer normal in, AO scalar out.
Shader "Hidden/BurtRP/ScreenSpaceAmbientOcclusion"
{
    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        HLSLINCLUDE
            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtDeferred.hlsl"

            sampler2D _BurtScreenSpaceAmbientOcclusionRawTexture;
            sampler2D _BurtScreenSpaceAmbientOcclusionTexture;
            sampler2D _BurtSSAOBlurSourceTexture;
            sampler2D _BurtSSAODebugCameraColorTexture;
            sampler2D _BurtSSAOHalfDepthNormalTexture;
            sampler2D _BurtSSAOHalfAmbientOcclusionTexture;
            sampler2D _BurtSSAOSpatialFinalTexture;
            sampler2D _BurtSSAOTemporalFinalTexture;
            sampler2D _BurtSSAOHistoryTexture;
            sampler2D _BurtSSAOHistoryDepthTexture;
            sampler2D _BurtSSAOPreviousHistoryTexture;
            sampler2D _BurtSSAOPreviousHistoryDepthTexture;
            float4x4 _BurtSSAOViewProjectionMatrix;
            float4x4 _BurtSSAOPreviousViewProjectionMatrix;
            float4 _BurtSSAOFullScreenSize;
            float4 _BurtSSAOHalfScreenSize;
            float4 _BurtSSAOParams0; // x radius, y intensity, z sample count, w bias.
            float4 _BurtSSAOParams1; // x power, y blur enabled, z frame salt, w unused.
            float4 _BurtSSAOParams2; // x fade distance, y fade radius, z thickness, w projected radius scale.
            float4 _BurtSSAOParams3; // x horizon search enabled, y direction count, z blur sharpness, w spatial denoise enabled.
            float4 _BurtSSAOBlurDirection; // xy blur direction, z resolve final curve.
            float4 _BurtSSAOTemporalParams; // x feedback, y history valid, z depth rejection, w clamp scale.
            float _BurtSSAODebugMode;

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

            bool BurtSSAOIsSkyDepth(float rawDepth)
            {
                #if defined(UNITY_REVERSED_Z)
                    return rawDepth <= 0.00001f;
                #else
                    return rawDepth >= 0.99999f;
                #endif
            }

            float BurtSSAORand(float2 pixelPosition)
            {
                return frac(sin(dot(pixelPosition + _BurtSSAOParams1.zz, float2(12.9898f, 78.233f))) * 43758.5453f);
            }

            float3 BurtSSAOSampleNormalWS(float2 screenUV)
            {
                return BurtDecodeNormalWSFromGBuffer(tex2D(_BurtGBuffer1, screenUV).rg);
            }

            float BurtSSAORawDepthFromClip(float clipZ)
            {
                #if defined(UNITY_REVERSED_Z)
                    return saturate(clipZ);
                #else
                    return saturate((clipZ - UNITY_NEAR_CLIP_VALUE) / max(1.0f - UNITY_NEAR_CLIP_VALUE, 0.00001f));
                #endif
            }

            float2 BurtSSAOClipToScreenUV(float2 clipXY)
            {
                float2 uv = clipXY * 0.5f + 0.5f;
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0f - uv.y;
                #endif

                return uv;
            }

            bool BurtSSAOProjectPositionWS(float3 positionWS, out float2 screenUV, out float linearDepth)
            {
                float4 clipPosition = mul(_BurtSSAOViewProjectionMatrix, float4(positionWS, 1.0f));
                if (clipPosition.w <= 0.00001f)
                {
                    screenUV = 0.0f;
                    linearDepth = 0.0f;
                    return false;
                }

                float3 ndc = clipPosition.xyz / clipPosition.w;
                screenUV = BurtSSAOClipToScreenUV(ndc.xy);
                float rawDepth = BurtSSAORawDepthFromClip(ndc.z);
                linearDepth = LinearEyeDepth(rawDepth);
                return !any(screenUV < 0.0f) && !any(screenUV > 1.0f) && rawDepth > 0.0f && rawDepth < 1.0f;
            }

            void BurtSSAOBuildTangentBasis(float3 normalWS, out float3 tangentWS, out float3 bitangentWS)
            {
                float3 upWS = abs(normalWS.y) < 0.99f ? float3(0.0f, 1.0f, 0.0f) : float3(1.0f, 0.0f, 0.0f);
                tangentWS = BurtSafeNormalize(cross(upWS, normalWS));
                bitangentWS = BurtSafeNormalize(cross(normalWS, tangentWS));
            }

            float3 BurtSSAOBuildSampleDirection(float3 normalWS, float3 tangentWS, float3 bitangentWS, float angle, float sampleFraction)
            {
                float radial = sqrt(saturate(sampleFraction));
                float normalAmount = sqrt(saturate(1.0f - radial * radial));
                float2 diskDirection = float2(cos(angle), sin(angle)) * radial;
                return BurtSafeNormalize(tangentWS * diskDirection.x + bitangentWS * diskDirection.y + normalWS * normalAmount);
            }

            float BurtSSAOApplyDistanceFade(float ao, float centerLinearDepth)
            {
                float fadeRadius = max(_BurtSSAOParams2.y, 0.0001f);
                float fadeDistance = max(_BurtSSAOParams2.x, fadeRadius);
                float fadeStart = max(fadeDistance - fadeRadius, 0.0f);
                float distanceFade = saturate((centerLinearDepth - fadeStart) / fadeRadius);
                return lerp(ao, 1.0f, distanceFade);
            }

            float BurtSSAOApplyFinalCurve(float rawVisibility, float centerLinearDepth)
            {
                float intensity = max(_BurtSSAOParams0.y, 0.0f);
                float power = max(_BurtSSAOParams1.x, 0.0001f);
                float curvedAO = 1.0f - (1.0f - pow(abs(saturate(rawVisibility)), power)) * intensity;
                return BurtSSAOApplyDistanceFade(saturate(curvedAO), centerLinearDepth);
            }

            bool BurtSSAOProjectHistoryUV(float3 positionWS, out float2 historyUV, out float historyRawDepth)
            {
                float4 previousClip = mul(_BurtSSAOPreviousViewProjectionMatrix, float4(positionWS, 1.0f));
                if (previousClip.w <= 0.00001f)
                {
                    historyUV = 0.0f;
                    historyRawDepth = 0.0f;
                    return false;
                }

                float3 previousNDC = previousClip.xyz / previousClip.w;
                historyUV = BurtSSAOClipToScreenUV(previousNDC.xy);
                historyRawDepth = BurtSSAORawDepthFromClip(previousNDC.z);
                return !any(historyUV < 0.0f) && !any(historyUV > 1.0f) && !BurtSSAOIsSkyDepth(historyRawDepth);
            }

            float BurtSSAOEvaluateHemisphereWithDepthNormal(float2 screenUV, float rawDepth, float3 normalWS, float2 randomSize)
            {
                if (BurtSSAOIsSkyDepth(rawDepth))
                {
                    return 1.0f;
                }

                float3 positionWS = BurtReconstructDeferredPositionWS(screenUV, rawDepth);
                normalWS = BurtSafeNormalize(normalWS);
                float centerLinearDepth = LinearEyeDepth(rawDepth);
                float radius = max(_BurtSSAOParams0.x, 0.0001f);
                int sampleCount = clamp((int)round(_BurtSSAOParams0.z), 1, 32);
                float bias = saturate(_BurtSSAOParams0.w);
                float thickness = saturate(_BurtSSAOParams2.z);
                float projectedRadiusPixels = radius * max(_BurtSSAOParams2.w, 1.0f) / max(centerLinearDepth, 0.0001f);
                float projectedRadiusFade = saturate((projectedRadiusPixels - 0.5f) / 2.0f);

                float3 tangentWS;
                float3 bitangentWS;
                BurtSSAOBuildTangentBasis(normalWS, tangentWS, bitangentWS);

                float rotation = BurtSSAORand(screenUV * randomSize) * 6.2831853f;
                float occlusion = 0.0f;
                float validSamples = 0.0f;

                [loop]
                for (int i = 0; i < 32; ++i)
                {
                    if (i >= sampleCount)
                    {
                        break;
                    }

                    float sampleFraction = ((float)i + 0.5f) / (float)sampleCount;
                    float angle = rotation + (float)i * 2.3999632f;
                    float3 sampleDirectionWS = BurtSSAOBuildSampleDirection(normalWS, tangentWS, bitangentWS, angle, sampleFraction);
                    float sampleDistance = radius * lerp(0.2f, 1.0f, sampleFraction * sampleFraction);
                    float3 probePositionWS = positionWS + sampleDirectionWS * sampleDistance;
                    float2 sampleUV;
                    float probeLinearDepth;
                    if (!BurtSSAOProjectPositionWS(probePositionWS, sampleUV, probeLinearDepth))
                    {
                        continue;
                    }

                    float sampleRawDepth = BurtSampleDeferredRawDepth(sampleUV);
                    if (BurtSSAOIsSkyDepth(sampleRawDepth))
                    {
                        continue;
                    }

                    float3 samplePositionWS = BurtReconstructDeferredPositionWS(sampleUV, sampleRawDepth);
                    float3 deltaWS = samplePositionWS - positionWS;
                    float distanceWS = length(deltaWS);
                    float sampleLinearDepth = LinearEyeDepth(sampleRawDepth);
                    float depthDelta = probeLinearDepth - sampleLinearDepth - bias;
                    float frontDepthWeight = smoothstep(0.0f, max(radius * 0.05f, 0.005f), depthDelta);
                    float thicknessStart = radius * lerp(0.15f, 0.75f, thickness);
                    float thicknessEnd = thicknessStart + max(radius * 0.5f, 0.005f);
                    float finiteThicknessWeight = 1.0f - smoothstep(thicknessStart, thicknessEnd, depthDelta);
                    float sampleDepthWeight = frontDepthWeight * finiteThicknessWeight;
                    float normalWeight = saturate(dot(normalWS, deltaWS / max(distanceWS, 0.0001f)));
                    float rangeWeight = saturate(1.0f - distanceWS / radius);
                    occlusion += sampleDepthWeight * normalWeight * rangeWeight;
                    validSamples += 1.0f;
                }

                float normalizedOcclusion = occlusion / max(validSamples, 1.0f);
                return saturate(1.0f - normalizedOcclusion * projectedRadiusFade);
            }

            float BurtSSAOEvaluateHorizonSample(
                float2 sampleUV,
                float3 positionWS,
                float3 normalWS,
                float radius,
                float bias,
                float thickness)
            {
                if (any(sampleUV < 0.0f) || any(sampleUV > 1.0f))
                {
                    return 0.0f;
                }

                float sampleRawDepth = BurtSampleDeferredRawDepth(sampleUV);
                if (BurtSSAOIsSkyDepth(sampleRawDepth))
                {
                    return 0.0f;
                }

                float3 samplePositionWS = BurtReconstructDeferredPositionWS(sampleUV, sampleRawDepth);
                float3 deltaWS = samplePositionWS - positionWS;
                float distanceWS = length(deltaWS);
                if (distanceWS <= 0.0001f)
                {
                    return 0.0f;
                }

                float heightWS = dot(normalWS, deltaWS);
                float horizon = saturate((heightWS - bias) / distanceWS);
                float frontWeight = smoothstep(0.0f, max(radius * 0.03f, 0.005f), heightWS - bias);
                float thicknessStart = radius * lerp(0.15f, 0.75f, thickness);
                float thicknessEnd = thicknessStart + max(radius * 0.5f, 0.005f);
                float finiteThicknessWeight = 1.0f - smoothstep(thicknessStart, thicknessEnd, heightWS);
                float rangeWeight = saturate(1.0f - distanceWS / radius);
                return horizon * frontWeight * finiteThicknessWeight * rangeWeight;
            }

            float BurtSSAOEvaluateHorizonWithDepthNormal(float2 screenUV, float rawDepth, float3 normalWS, float2 randomSize)
            {
                if (BurtSSAOIsSkyDepth(rawDepth))
                {
                    return 1.0f;
                }

                float3 positionWS = BurtReconstructDeferredPositionWS(screenUV, rawDepth);
                normalWS = BurtSafeNormalize(normalWS);
                float centerLinearDepth = LinearEyeDepth(rawDepth);
                float radius = max(_BurtSSAOParams0.x, 0.0001f);
                int stepCount = clamp((int)round(_BurtSSAOParams0.z), 1, 32);
                int directionCount = clamp((int)round(_BurtSSAOParams3.y), 1, 8);
                float bias = saturate(_BurtSSAOParams0.w);
                float thickness = saturate(_BurtSSAOParams2.z);
                float projectedRadiusPixels = radius * max(_BurtSSAOParams2.w, 1.0f) / max(centerLinearDepth, 0.0001f);
                float projectedRadiusFade = saturate((projectedRadiusPixels - 0.5f) / 2.0f);
                if (projectedRadiusFade <= 0.0001f)
                {
                    return 1.0f;
                }

                float rotation = BurtSSAORand(screenUV * randomSize) * 6.2831853f;
                float stepJitter = BurtSSAORand(screenUV * randomSize + 17.0f);
                float occlusion = 0.0f;
                float validDirections = 0.0f;

                [loop]
                for (int directionIndex = 0; directionIndex < 8; ++directionIndex)
                {
                    if (directionIndex >= directionCount)
                    {
                        break;
                    }

                    float angle = rotation + (float)directionIndex * 3.14159265f / (float)directionCount;
                    float2 screenDirection = float2(cos(angle), sin(angle));
                    float positiveHorizon = 0.0f;
                    float negativeHorizon = 0.0f;

                    [loop]
                    for (int stepIndex = 1; stepIndex <= 32; ++stepIndex)
                    {
                        if (stepIndex > stepCount)
                        {
                            break;
                        }

                        float stepFraction = ((float)stepIndex - 0.5f + stepJitter) / (float)stepCount;
                        float samplePixelRadius = max(projectedRadiusPixels * saturate(stepFraction), (float)stepIndex);
                        float2 sampleOffsetUV = screenDirection * samplePixelRadius * _BurtSSAOFullScreenSize.zw;
                        positiveHorizon = max(positiveHorizon, BurtSSAOEvaluateHorizonSample(screenUV + sampleOffsetUV, positionWS, normalWS, radius, bias, thickness));
                        negativeHorizon = max(negativeHorizon, BurtSSAOEvaluateHorizonSample(screenUV - sampleOffsetUV, positionWS, normalWS, radius, bias, thickness));
                    }

                    occlusion += positiveHorizon + negativeHorizon;
                    validDirections += 2.0f;
                }

                float normalizedOcclusion = occlusion / max(validDirections, 1.0f);
                return saturate(1.0f - normalizedOcclusion * projectedRadiusFade);
            }

            float BurtSSAOEvaluateWithDepthNormal(float2 screenUV, float rawDepth, float3 normalWS, float2 randomSize)
            {
                if (_BurtSSAOParams3.x > 0.5f)
                {
                    return BurtSSAOEvaluateHorizonWithDepthNormal(screenUV, rawDepth, normalWS, randomSize);
                }

                return BurtSSAOEvaluateHemisphereWithDepthNormal(screenUV, rawDepth, normalWS, randomSize);
            }

            float BurtSSAOEvaluate(float2 screenUV)
            {
                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                float3 normalWS = BurtSSAOSampleNormalWS(screenUV);
                return BurtSSAOEvaluateWithDepthNormal(screenUV, rawDepth, normalWS, _BurtDeferredScreenSize.xy);
            }

            float4 FragTrace(Varyings input) : SV_Target
            {
                float ao = BurtSSAOEvaluate(input.screenUV);
                return float4(ao, ao, ao, 1.0f);
            }

            float BurtSSAODownsampleDepthWeight(float sampleLinearDepth, float targetLinearDepth)
            {
                float tolerance = max(targetLinearDepth * 0.02f, 0.05f);
                return saturate(1.0f - abs(sampleLinearDepth - targetLinearDepth) / tolerance);
            }

            void BurtSSAOSampleDepthNormalForDownsample(float2 screenUV, out float rawDepth, out float linearDepth, out float3 normalWS, out float valid)
            {
                screenUV = saturate(screenUV);
                rawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtSSAOIsSkyDepth(rawDepth))
                {
                    linearDepth = -1.0f;
                    normalWS = float3(0.0f, 0.0f, 1.0f);
                    valid = 0.0f;
                    return;
                }

                linearDepth = LinearEyeDepth(rawDepth);
                normalWS = BurtSSAOSampleNormalWS(screenUV);
                valid = 1.0f;
            }

            float4 FragDownsampleDepthNormal(Varyings input) : SV_Target
            {
                float2 fullTexel = _BurtSSAOFullScreenSize.zw;
                float rawDepth0;
                float rawDepth1;
                float rawDepth2;
                float rawDepth3;
                float linearDepth0;
                float linearDepth1;
                float linearDepth2;
                float linearDepth3;
                float3 normal0;
                float3 normal1;
                float3 normal2;
                float3 normal3;
                float valid0;
                float valid1;
                float valid2;
                float valid3;

                BurtSSAOSampleDepthNormalForDownsample(input.screenUV + fullTexel * float2(-0.5f, -0.5f), rawDepth0, linearDepth0, normal0, valid0);
                BurtSSAOSampleDepthNormalForDownsample(input.screenUV + fullTexel * float2(0.5f, -0.5f), rawDepth1, linearDepth1, normal1, valid1);
                BurtSSAOSampleDepthNormalForDownsample(input.screenUV + fullTexel * float2(-0.5f, 0.5f), rawDepth2, linearDepth2, normal2, valid2);
                BurtSSAOSampleDepthNormalForDownsample(input.screenUV + fullTexel * float2(0.5f, 0.5f), rawDepth3, linearDepth3, normal3, valid3);

                float validCount = valid0 + valid1 + valid2 + valid3;
                if (validCount < 0.5f)
                {
                    return float4(0.5f, 0.5f, 1.0f, rawDepth0);
                }

                float targetLinearDepth = max(max(linearDepth0, linearDepth1), max(linearDepth2, linearDepth3));
                float targetRawDepth = rawDepth0;
                targetRawDepth = linearDepth1 >= targetLinearDepth ? rawDepth1 : targetRawDepth;
                targetRawDepth = linearDepth2 >= targetLinearDepth ? rawDepth2 : targetRawDepth;
                targetRawDepth = linearDepth3 >= targetLinearDepth ? rawDepth3 : targetRawDepth;

                float weight0 = valid0 * BurtSSAODownsampleDepthWeight(linearDepth0, targetLinearDepth);
                float weight1 = valid1 * BurtSSAODownsampleDepthWeight(linearDepth1, targetLinearDepth);
                float weight2 = valid2 * BurtSSAODownsampleDepthWeight(linearDepth2, targetLinearDepth);
                float weight3 = valid3 * BurtSSAODownsampleDepthWeight(linearDepth3, targetLinearDepth);
                float totalWeight = max(weight0 + weight1 + weight2 + weight3, 0.0001f);
                float3 normalWS = BurtSafeNormalize((normal0 * weight0 + normal1 * weight1 + normal2 * weight2 + normal3 * weight3) / totalWeight);
                return float4(normalWS * 0.5f + 0.5f, targetRawDepth);
            }

            float3 BurtSSAOUnpackHalfNormalWS(float3 packedNormal)
            {
                return BurtSafeNormalize(packedNormal * 2.0f - 1.0f);
            }

            float4 FragTraceHalf(Varyings input) : SV_Target
            {
                float4 halfDepthNormal = tex2D(_BurtSSAOHalfDepthNormalTexture, input.screenUV);
                float ao = BurtSSAOEvaluateWithDepthNormal(
                    input.screenUV,
                    halfDepthNormal.a,
                    BurtSSAOUnpackHalfNormalWS(halfDepthNormal.rgb),
                    _BurtSSAOHalfScreenSize.xy);
                return float4(ao, ao, ao, 1.0f);
            }

            void BurtSSAOAccumulateUpsampleSample(
                float2 sampleUV,
                float baseWeight,
                float centerLinearDepth,
                float3 centerNormalWS,
                inout float totalAO,
                inout float totalWeight)
            {
                sampleUV = saturate(sampleUV);
                float sampleAO = tex2D(_BurtSSAOHalfAmbientOcclusionTexture, sampleUV).r;
                float4 halfDepthNormal = tex2D(_BurtSSAOHalfDepthNormalTexture, sampleUV);
                if (BurtSSAOIsSkyDepth(halfDepthNormal.a))
                {
                    return;
                }

                float sampleLinearDepth = LinearEyeDepth(halfDepthNormal.a);
                float3 sampleNormalWS = BurtSSAOUnpackHalfNormalWS(halfDepthNormal.rgb);
                float depthDelta = abs(sampleLinearDepth - centerLinearDepth);
                float depthTolerance = max(centerLinearDepth * 0.025f, 0.025f);
                float depthWeight = exp2(-depthDelta / depthTolerance);
                float normalSimilarity = saturate(dot(centerNormalWS, sampleNormalWS));
                float normalWeight = pow(normalSimilarity, 16.0f);
                float edgeMismatch = saturate(depthDelta / max(depthTolerance * 2.0f, 0.0001f));
                float conservativeAO = lerp(sampleAO, 1.0f, edgeMismatch);
                float weight = baseWeight * depthWeight * normalWeight;
                totalAO += conservativeAO * weight;
                totalWeight += weight;
            }

            void BurtSSAOAccumulateUpsampleFootprint(
                float2 screenUV,
                float centerLinearDepth,
                float3 centerNormalWS,
                inout float totalAO,
                inout float totalWeight)
            {
                float2 halfTexel = _BurtSSAOHalfScreenSize.zw;
                float2 halfPixel = screenUV * _BurtSSAOHalfScreenSize.xy - 0.5f;
                float2 basePixel = floor(halfPixel);
                float2 bilinearFraction = saturate(halfPixel - basePixel);

                [unroll]
                for (int sampleIndex = 0; sampleIndex < 4; sampleIndex++)
                {
                    float2 offset = sampleIndex == 0 ? float2(0.0f, 0.0f) :
                        sampleIndex == 1 ? float2(1.0f, 0.0f) :
                        sampleIndex == 2 ? float2(0.0f, 1.0f) :
                        float2(1.0f, 1.0f);
                    float2 samplePixel = clamp(basePixel + offset, 0.0f, _BurtSSAOHalfScreenSize.xy - 1.0f);
                    float2 sampleUV = (samplePixel + 0.5f) * halfTexel;

                    float bilinearWeight = (offset.x > 0.5f ? bilinearFraction.x : 1.0f - bilinearFraction.x) *
                        (offset.y > 0.5f ? bilinearFraction.y : 1.0f - bilinearFraction.y);
                    BurtSSAOAccumulateUpsampleSample(sampleUV, bilinearWeight, centerLinearDepth, centerNormalWS, totalAO, totalWeight);
                }
            }

            float4 FragUpsampleRaw(Varyings input) : SV_Target
            {
                float rawDepth = BurtSampleDeferredRawDepth(input.screenUV);
                if (BurtSSAOIsSkyDepth(rawDepth))
                {
                    return float4(1.0f, 1.0f, 1.0f, 1.0f);
                }

                float centerLinearDepth = LinearEyeDepth(rawDepth);
                float3 centerNormalWS = BurtSSAOSampleNormalWS(input.screenUV);
                float2 halfTexel = _BurtSSAOHalfScreenSize.zw;
                float totalAO = 0.0f;
                float totalWeight = 0.0f;

                BurtSSAOAccumulateUpsampleFootprint(input.screenUV, centerLinearDepth, centerNormalWS, totalAO, totalWeight);
                BurtSSAOAccumulateUpsampleSample(input.screenUV + float2(halfTexel.x, 0.0f), 0.35f, centerLinearDepth, centerNormalWS, totalAO, totalWeight);
                BurtSSAOAccumulateUpsampleSample(input.screenUV + float2(-halfTexel.x, 0.0f), 0.35f, centerLinearDepth, centerNormalWS, totalAO, totalWeight);
                BurtSSAOAccumulateUpsampleSample(input.screenUV + float2(0.0f, halfTexel.y), 0.35f, centerLinearDepth, centerNormalWS, totalAO, totalWeight);
                BurtSSAOAccumulateUpsampleSample(input.screenUV + float2(0.0f, -halfTexel.y), 0.35f, centerLinearDepth, centerNormalWS, totalAO, totalWeight);

                float fallbackAO = tex2D(_BurtSSAOHalfAmbientOcclusionTexture, saturate(input.screenUV)).r;
                float ao = totalWeight > 0.0001f ? totalAO / totalWeight : fallbackAO;
                return float4(ao, ao, ao, 1.0f);
            }

            float BurtSSAOSampleRaw(float2 screenUV)
            {
                return tex2D(_BurtScreenSpaceAmbientOcclusionRawTexture, screenUV).r;
            }

            float BurtSSAOSampleBlurSource(float2 screenUV)
            {
                return tex2D(_BurtSSAOBlurSourceTexture, screenUV).r;
            }

            float BurtSSAOBlurKernelWeight(int offset)
            {
                int absOffset = offset < 0 ? -offset : offset;
                return absOffset == 0 ? 0.2162f : (absOffset == 1 ? 0.1907f : (absOffset == 2 ? 0.1174f : 0.0540f));
            }

            float BurtSSAOSampleLinearDepthForBlur(float2 screenUV, out float validDepth)
            {
                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtSSAOIsSkyDepth(rawDepth))
                {
                    validDepth = 0.0f;
                    return 0.0f;
                }

                validDepth = 1.0f;
                return LinearEyeDepth(rawDepth);
            }

            float BurtSSAOSmartEdgeWeight(float2 screenUV, float2 texelOffset, int offset, float centerLinearDepth, float depthTolerance)
            {
                int absOffset = offset < 0 ? -offset : offset;
                if (absOffset == 0)
                {
                    return 1.0f;
                }

                float stepDirection = offset < 0 ? -1.0f : 1.0f;
                float previousDepth = centerLinearDepth;
                float edgeWeight = 1.0f;

                [unroll]
                for (int stepIndex = 1; stepIndex <= 3; ++stepIndex)
                {
                    if (stepIndex > absOffset)
                    {
                        continue;
                    }

                    float2 sampleUV = saturate(screenUV + texelOffset * (stepDirection * (float)stepIndex));
                    float validDepth;
                    float sampleLinearDepth = BurtSSAOSampleLinearDepthForBlur(sampleUV, validDepth);
                    if (validDepth < 0.5f)
                    {
                        return 0.0f;
                    }

                    float depthStep = abs(sampleLinearDepth - previousDepth);
                    edgeWeight *= saturate(1.0f - depthStep / max(depthTolerance * 1.5f, 0.0001f));
                    previousDepth = sampleLinearDepth;
                }

                return edgeWeight;
            }

            float BurtSSAOBilateralBlur(float2 screenUV)
            {
                float centerAO = BurtSSAOSampleBlurSource(screenUV);
                if (_BurtSSAOParams1.y < 0.5f || abs(_BurtSSAOBlurDirection.x) + abs(_BurtSSAOBlurDirection.y) < 0.5f)
                {
                    return centerAO;
                }

                float centerDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtSSAOIsSkyDepth(centerDepth))
                {
                    return centerAO;
                }

                float centerLinearDepth = LinearEyeDepth(centerDepth);
                float3 centerNormalWS = BurtSSAOSampleNormalWS(screenUV);
                float2 texelOffset = _BurtSSAOBlurDirection.xy * _BurtDeferredScreenSize.zw;
                float smartDenoise = _BurtSSAOParams3.w;
                float blurSharpness = saturate(_BurtSSAOParams3.z);
                float depthTolerance = max(centerLinearDepth * lerp(0.08f, 0.012f, blurSharpness), lerp(0.08f, 0.02f, blurSharpness));
                float normalPower = lerp(4.0f, 24.0f, blurSharpness);
                float aoTolerance = lerp(0.35f, 0.08f, blurSharpness);
                float totalWeight = 0.0f;
                float totalAO = 0.0f;

                [unroll]
                for (int offset = -3; offset <= 3; ++offset)
                {
                    if (smartDenoise < 0.5f && (offset < -2 || offset > 2))
                    {
                        continue;
                    }

                    float2 sampleUV = saturate(screenUV + texelOffset * offset);
                    float sampleDepth = BurtSampleDeferredRawDepth(sampleUV);
                    if (BurtSSAOIsSkyDepth(sampleDepth))
                    {
                        continue;
                    }

                    float sampleAO = BurtSSAOSampleBlurSource(sampleUV);
                    float sampleLinearDepth = LinearEyeDepth(sampleDepth);
                    float3 sampleNormalWS = BurtSSAOSampleNormalWS(sampleUV);
                    float depthWeight = exp2(-abs(sampleLinearDepth - centerLinearDepth) / depthTolerance);
                    float normalWeight = pow(saturate(dot(centerNormalWS, sampleNormalWS)), normalPower);
                    float edgeWeight = smartDenoise > 0.5f ? BurtSSAOSmartEdgeWeight(screenUV, texelOffset, offset, centerLinearDepth, depthTolerance) : 1.0f;
                    float aoWeight = smartDenoise > 0.5f ? exp2(-abs(sampleAO - centerAO) / max(aoTolerance, 0.0001f)) : 1.0f;
                    float weight = BurtSSAOBlurKernelWeight(offset) * depthWeight * normalWeight * edgeWeight * aoWeight;
                    totalAO += sampleAO * weight;
                    totalWeight += weight;
                }

                return totalWeight > 0.0001f ? totalAO / totalWeight : centerAO;
            }

            float4 FragBlur(Varyings input) : SV_Target
            {
                float ao = saturate(BurtSSAOBilateralBlur(input.screenUV));
                if (_BurtSSAOBlurDirection.z > 0.5f)
                {
                    float rawDepth = BurtSampleDeferredRawDepth(input.screenUV);
                    if (BurtSSAOIsSkyDepth(rawDepth))
                    {
                        return float4(1.0f, 1.0f, 1.0f, 1.0f);
                    }

                    ao = BurtSSAOApplyFinalCurve(ao, LinearEyeDepth(rawDepth));
                }

                return float4(ao, ao, ao, 1.0f);
            }

            float4 FragDebug(Varyings input) : SV_Target
            {
                float rawAO = tex2D(_BurtScreenSpaceAmbientOcclusionRawTexture, input.screenUV).r;
                float finalAO = tex2D(_BurtScreenSpaceAmbientOcclusionTexture, input.screenUV).r;
                float debugMode = round(_BurtSSAODebugMode);
                if (debugMode == 4.0f)
                {
                    float historyAO = tex2D(_BurtSSAOPreviousHistoryTexture, input.screenUV).r;
                    return float4(saturate(historyAO).xxx, 1.0f);
                }

                if (debugMode == 5.0f)
                {
                    float historyAO = tex2D(_BurtSSAOPreviousHistoryTexture, input.screenUV).r;
                    return float4(saturate(abs(finalAO - historyAO) * 4.0f).xxx, 1.0f);
                }

                float ao = debugMode < 1.5f ? rawAO : finalAO;
                return float4(saturate(ao).xxx, 1.0f);
            }

            float4 FragOverlay(Varyings input) : SV_Target
            {
                float4 cameraColor = tex2D(_BurtSSAODebugCameraColorTexture, input.screenUV);
                float ao = saturate(tex2D(_BurtScreenSpaceAmbientOcclusionTexture, input.screenUV).r);
                return float4(cameraColor.rgb * ao, cameraColor.a);
            }

            void BurtSSAOAccumulateTemporalNeighbor(
                float2 sampleUV,
                float centerLinearDepth,
                float3 centerNormalWS,
                inout float minAO,
                inout float maxAO,
                inout float weightedAO,
                inout float totalWeight)
            {
                sampleUV = saturate(sampleUV);
                float sampleRawDepth = BurtSampleDeferredRawDepth(sampleUV);
                if (BurtSSAOIsSkyDepth(sampleRawDepth))
                {
                    return;
                }

                float sampleAO = saturate(tex2D(_BurtSSAOSpatialFinalTexture, sampleUV).r);
                float sampleLinearDepth = LinearEyeDepth(sampleRawDepth);
                float3 sampleNormalWS = BurtSSAOSampleNormalWS(sampleUV);
                float depthTolerance = max(centerLinearDepth * 0.012f, 0.02f);
                float depthWeight = exp2(-abs(sampleLinearDepth - centerLinearDepth) / depthTolerance);
                float normalWeight = pow(saturate(dot(centerNormalWS, sampleNormalWS)), 16.0f);
                float weight = depthWeight * normalWeight;
                if (weight <= 0.001f)
                {
                    return;
                }

                minAO = min(minAO, sampleAO);
                maxAO = max(maxAO, sampleAO);
                weightedAO += sampleAO * weight;
                totalWeight += weight;
            }

            void BurtSSAOSampleTemporalNeighborhood(float2 screenUV, float rawDepth, out float centerAO, out float minAO, out float maxAO, out float averageAO)
            {
                float2 texel = _BurtSSAOFullScreenSize.zw;
                float centerLinearDepth = LinearEyeDepth(rawDepth);
                float3 centerNormalWS = BurtSSAOSampleNormalWS(screenUV);
                centerAO = saturate(tex2D(_BurtSSAOSpatialFinalTexture, screenUV).r);
                minAO = centerAO;
                maxAO = centerAO;
                float weightedAO = centerAO;
                float totalWeight = 1.0f;

                BurtSSAOAccumulateTemporalNeighbor(screenUV + float2(texel.x, 0.0f), centerLinearDepth, centerNormalWS, minAO, maxAO, weightedAO, totalWeight);
                BurtSSAOAccumulateTemporalNeighbor(screenUV - float2(texel.x, 0.0f), centerLinearDepth, centerNormalWS, minAO, maxAO, weightedAO, totalWeight);
                BurtSSAOAccumulateTemporalNeighbor(screenUV + float2(0.0f, texel.y), centerLinearDepth, centerNormalWS, minAO, maxAO, weightedAO, totalWeight);
                BurtSSAOAccumulateTemporalNeighbor(screenUV - float2(0.0f, texel.y), centerLinearDepth, centerNormalWS, minAO, maxAO, weightedAO, totalWeight);
                averageAO = totalWeight > 0.0001f ? weightedAO / totalWeight : centerAO;
            }

            float BurtSSAOEvaluateCurrentSurfaceStability(
                float2 historyUV,
                float projectedHistoryLinearDepth,
                float3 centerNormalWS,
                float centerLinearDepth)
            {
                float currentRawDepthAtHistoryUV = BurtSampleDeferredRawDepth(historyUV);
                if (BurtSSAOIsSkyDepth(currentRawDepthAtHistoryUV))
                {
                    return 0.0f;
                }

                float currentLinearDepthAtHistoryUV = LinearEyeDepth(currentRawDepthAtHistoryUV);
                float currentDepthTolerance = max(centerLinearDepth * 0.018f, 0.03f);
                float currentDepthSimilarity = saturate(1.0f - abs(currentLinearDepthAtHistoryUV - centerLinearDepth) / currentDepthTolerance);
                float projectedDepthTolerance = max(projectedHistoryLinearDepth * 0.018f, 0.03f);
                float projectedDepthSimilarity = saturate(1.0f - abs(currentLinearDepthAtHistoryUV - projectedHistoryLinearDepth) / projectedDepthTolerance);
                float3 currentNormalAtHistoryUV = BurtSSAOSampleNormalWS(historyUV);
                float normalSimilarity = smoothstep(0.55f, 0.95f, saturate(dot(centerNormalWS, currentNormalAtHistoryUV)));
                return currentDepthSimilarity * projectedDepthSimilarity * normalSimilarity;
            }

            float BurtSSAOTemporalResolveAO(float2 screenUV)
            {
                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtSSAOIsSkyDepth(rawDepth))
                {
                    return 1.0f;
                }

                float centerAO;
                float minAO;
                float maxAO;
                float averageAO;
                BurtSSAOSampleTemporalNeighborhood(screenUV, rawDepth, centerAO, minAO, maxAO, averageAO);
                float centerLinearDepth = LinearEyeDepth(rawDepth);
                float3 centerNormalWS = BurtSSAOSampleNormalWS(screenUV);

                if (_BurtSSAOTemporalParams.y < 0.5f)
                {
                    return centerAO;
                }

                float3 positionWS = BurtReconstructDeferredPositionWS(screenUV, rawDepth);
                float2 historyUV;
                float projectedHistoryRawDepth;
                if (!BurtSSAOProjectHistoryUV(positionWS, historyUV, projectedHistoryRawDepth))
                {
                    return centerAO;
                }

                float historyDepth = tex2D(_BurtSSAOPreviousHistoryDepthTexture, historyUV).r;
                float projectedHistoryLinearDepth = LinearEyeDepth(projectedHistoryRawDepth);
                float historyLinearDepth = LinearEyeDepth(historyDepth);
                float depthTolerance = max(projectedHistoryLinearDepth * max(_BurtSSAOTemporalParams.z, 0.0001f), 0.025f);
                float depthValidity = saturate(1.0f - abs(historyLinearDepth - projectedHistoryLinearDepth) / depthTolerance);
                if (BurtSSAOIsSkyDepth(historyDepth) || depthValidity <= 0.0001f)
                {
                    return centerAO;
                }

                float surfaceStability = BurtSSAOEvaluateCurrentSurfaceStability(historyUV, projectedHistoryLinearDepth, centerNormalWS, centerLinearDepth);
                if (surfaceStability <= 0.0001f)
                {
                    return centerAO;
                }

                float clampScale = max(_BurtSSAOTemporalParams.w, 0.0f);
                float localRange = maxAO - minAO;
                float clampPad = max(localRange, 0.002f) * clampScale;
                float historyAO = saturate(tex2D(_BurtSSAOPreviousHistoryTexture, historyUV).r);
                float clampedHistoryAO = clamp(historyAO, minAO - clampPad, maxAO + clampPad);
                float historyDelta = abs(historyAO - centerAO);
                float rangeTolerance = max(localRange + clampPad, 0.01f);
                float historyConsistency = saturate(1.0f - historyDelta / rangeTolerance);
                float finalHistoryAO = lerp(clampedHistoryAO, historyAO, historyConsistency * 0.25f);
                float feedback = saturate(_BurtSSAOTemporalParams.x) * depthValidity * surfaceStability * lerp(0.35f, 1.0f, historyConsistency);
                float currentAO = lerp(centerAO, averageAO, 0.35f);
                return saturate(lerp(currentAO, finalHistoryAO, feedback));
            }

            float4 FragTemporal(Varyings input) : SV_Target
            {
                float ao = BurtSSAOTemporalResolveAO(input.screenUV);
                return float4(ao, ao, ao, 1.0f);
            }

            float4 FragCopyTemporalFinal(Varyings input) : SV_Target
            {
                float ao = saturate(tex2D(_BurtSSAOTemporalFinalTexture, input.screenUV).r);
                return float4(ao, ao, ao, 1.0f);
            }

            float4 FragCopyCurrentDepth(Varyings input) : SV_Target
            {
                float rawDepth = BurtSampleDeferredRawDepth(input.screenUV);
                return float4(rawDepth, rawDepth, rawDepth, 1.0f);
            }

            float4 FragTemporalDepthValidity(Varyings input) : SV_Target
            {
                float rawDepth = BurtSampleDeferredRawDepth(input.screenUV);
                if (_BurtSSAOTemporalParams.y < 0.5f || BurtSSAOIsSkyDepth(rawDepth))
                {
                    return float4(0.0f, 0.0f, 0.0f, 1.0f);
                }

                float3 positionWS = BurtReconstructDeferredPositionWS(input.screenUV, rawDepth);
                float2 historyUV;
                float projectedHistoryRawDepth;
                if (!BurtSSAOProjectHistoryUV(positionWS, historyUV, projectedHistoryRawDepth))
                {
                    return float4(1.0f, 0.0f, 0.0f, 1.0f);
                }

                float historyDepth = tex2D(_BurtSSAOHistoryDepthTexture, historyUV).r;
                if (BurtSSAOIsSkyDepth(historyDepth))
                {
                    return float4(1.0f, 0.25f, 0.0f, 1.0f);
                }

                float projectedHistoryLinearDepth = LinearEyeDepth(projectedHistoryRawDepth);
                float historyLinearDepth = LinearEyeDepth(historyDepth);
                float depthTolerance = max(projectedHistoryLinearDepth * max(_BurtSSAOTemporalParams.z, 0.0001f), 0.025f);
                float depthValidity = saturate(1.0f - abs(historyLinearDepth - projectedHistoryLinearDepth) / depthTolerance);
                if (round(_BurtSSAODebugMode) == 7.0f)
                {
                    float3 centerNormalWS = BurtSSAOSampleNormalWS(input.screenUV);
                    float centerLinearDepth = LinearEyeDepth(rawDepth);
                    float surfaceStability = BurtSSAOEvaluateCurrentSurfaceStability(historyUV, projectedHistoryLinearDepth, centerNormalWS, centerLinearDepth);
                    return float4(surfaceStability.xxx, 1.0f);
                }

                return float4(1.0f - depthValidity, depthValidity, 0.0f, 1.0f);
            }
        ENDHLSL

        Pass
        {
            Name "Burt SSAO Trace"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragTrace
            ENDHLSL
        }

        Pass
        {
            Name "Burt SSAO Blur"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragBlur
            ENDHLSL
        }

        Pass
        {
            Name "Burt SSAO Debug"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragDebug
            ENDHLSL
        }

        Pass
        {
            Name "Burt SSAO Overlay"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragOverlay
            ENDHLSL
        }

        Pass
        {
            Name "Burt SSAO Downsample Depth Normal"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragDownsampleDepthNormal
            ENDHLSL
        }

        Pass
        {
            Name "Burt SSAO Half Trace"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragTraceHalf
            ENDHLSL
        }

        Pass
        {
            Name "Burt SSAO Upsample Raw"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragUpsampleRaw
            ENDHLSL
        }

        Pass
        {
            Name "Burt SSAO Temporal"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragTemporal
            ENDHLSL
        }

        Pass
        {
            Name "Burt SSAO Copy Current Depth"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragCopyCurrentDepth
            ENDHLSL
        }

        Pass
        {
            Name "Burt SSAO Temporal Depth Validity"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragTemporalDepthValidity
            ENDHLSL
        }

        Pass
        {
            Name "Burt SSAO Copy Temporal Final"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragCopyTemporalFinal
            ENDHLSL
        }
    }

    Fallback Off
}
