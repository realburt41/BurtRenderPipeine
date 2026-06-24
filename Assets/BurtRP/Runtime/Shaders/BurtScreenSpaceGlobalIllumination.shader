Shader "Hidden/BurtRP/BurtGI"
{
    // BurtRP v2.2 screen-space diffuse GI. It is a small bridge toward BurtGI/Lumen-style lighting,
    // not the final voxel/radiance-cache implementation.
    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        HLSLINCLUDE
            #define BURT_SUBSURFACE_DEFERRED_POSTPROCESS_INPUT 1
            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtDeferred.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLighting.hlsl"

            sampler2D _BurtGISourceColorTexture;
            sampler2D _BurtScreenSpaceGlobalIlluminationRawTexture;
            sampler2D _BurtScreenSpaceGlobalIlluminationTexture;
            sampler2D _BurtGISpatialFinalTexture;
            sampler2D _BurtGITemporalFinalTexture;
            sampler2D _BurtGIHistoryTexture;
            sampler2D _BurtGIHistoryDepthNormalTexture;
            sampler2D _BurtGIPreviousHistoryTexture;
            sampler2D _BurtGIPreviousHistoryDepthNormalTexture;
            sampler2D _BurtGICameraColorCopyTexture;
            sampler2D _BurtGIDebugCameraColorTexture;
            sampler2D _BurtGITemporalDiagnosticsTexture;
            float4x4 _BurtGIViewMatrix;
            float4x4 _BurtGIViewProjectionMatrix;
            float4x4 _BurtGIPreviousViewProjectionMatrix;
            float4 _BurtGISourceTexelSize; // xy=1/width,1/height, zw=width,height of the BurtGI target.
            float4 _BurtGIParams0; // x=radius, y=sampleCount, z=maxSteps, w=thickness.
            float4 _BurtGIParams1; // x=intensity, y=skyFallback, z=blur, w=blurSharpness.
            float4 _BurtGIParams2; // x=frame salt, y=normalWeight, z=distanceFade, w=radianceClamp.
            float4 _BurtGIParams3; // x=spatial radius, y=spatial strength, z=temporal variance clamp, w=hit rejection.
            float4 _BurtGIParams4; // x=leak guard, y=edge fade, z=normal cone tightness, w=sky edge suppression.
            float4 _BurtGITemporalParams; // x=feedback, y=history valid, z=depth rejection, w=normal rejection.
            float4 _BurtGITemporalParams1; // x=history clamp scale, y=variance clamp, z=hit rejection, w=spatial strength.
            float _BurtGIDebugMode;

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

            bool BurtGIIsSkyDepth(float rawDepth)
            {
                #if defined(UNITY_REVERSED_Z)
                    return rawDepth <= 0.00001f;
                #else
                    return rawDepth >= 0.99999f;
                #endif
            }

            float BurtGIRawDepthFromClip(float clipZ)
            {
                #if defined(UNITY_REVERSED_Z)
                    return saturate(clipZ);
                #else
                    return saturate((clipZ - UNITY_NEAR_CLIP_VALUE) / max(1.0f - UNITY_NEAR_CLIP_VALUE, 0.00001f));
                #endif
            }

            float2 BurtGIClipToScreenUV(float2 clipXY)
            {
                float2 uv = clipXY * 0.5f + 0.5f;
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0f - uv.y;
                #endif
                return uv;
            }

            bool BurtGIProjectPositionWS(float3 positionWS, out float2 screenUV, out float rawDepth, out float linearDepth)
            {
                float4 clipPosition = mul(_BurtGIViewProjectionMatrix, float4(positionWS, 1.0f));
                if (clipPosition.w <= 0.00001f)
                {
                    screenUV = 0.0f;
                    rawDepth = 0.0f;
                    linearDepth = 0.0f;
                    return false;
                }

                float3 ndc = clipPosition.xyz / clipPosition.w;
                screenUV = BurtGIClipToScreenUV(ndc.xy);
                rawDepth = BurtGIRawDepthFromClip(ndc.z);
                linearDepth = LinearEyeDepth(rawDepth);
                return !any(screenUV < 0.0f) && !any(screenUV > 1.0f) && !BurtGIIsSkyDepth(rawDepth);
            }

            float BurtGIRand(float2 pixelPosition)
            {
                return frac(52.9829189f * frac(dot(pixelPosition + _BurtGIParams2.xx, float2(0.06711056f, 0.00583715f))));
            }

            float BurtGIHash12(float2 pixelPosition, float salt)
            {
                float3 p3 = frac(float3(pixelPosition.x, pixelPosition.y, pixelPosition.x) * 0.1031f + salt * 0.0131f);
                p3 += dot(p3, p3.yzx + 33.33f);
                return frac((p3.x + p3.y) * p3.z);
            }

            float3 BurtGISampleNormalWS(float2 screenUV)
            {
                return BurtDecodeNormalWSFromGBuffer(BURT_SAMPLE_TEXTURE2D_POINT_CLAMP(_BurtGBuffer1, screenUV).rg);
            }

            void BurtGIBuildTangentBasis(float3 normalWS, out float3 tangentWS, out float3 bitangentWS)
            {
                float3 upWS = abs(normalWS.y) < 0.99f ? float3(0.0f, 1.0f, 0.0f) : float3(1.0f, 0.0f, 0.0f);
                tangentWS = BurtSafeNormalize(cross(upWS, normalWS));
                bitangentWS = BurtSafeNormalize(cross(normalWS, tangentWS));
            }

            float3 BurtGIBuildSampleDirection(float3 normalWS, float3 tangentWS, float3 bitangentWS, float angle, float sampleFraction)
            {
                float radial = sqrt(saturate(sampleFraction));
                float normalAmount = sqrt(saturate(1.0f - radial * radial));
                float2 diskDirection = float2(cos(angle), sin(angle)) * radial;
                return BurtSafeNormalize(tangentWS * diskDirection.x + bitangentWS * diskDirection.y + normalWS * normalAmount);
            }

            float3 BurtGIClampRadiance(float3 radiance)
            {
                return min(max(radiance, 0.0f), _BurtGIParams2.www);
            }

            float BurtGIEdgeFactor(float2 screenUV, float centerRawDepth, float3 centerNormalWS)
            {
                float2 texel = _BurtDeferredScreenSize.zw;
                float centerLinearDepth = LinearEyeDepth(centerRawDepth);
                float minNormalDot = 1.0f;
                float maxDepthEdge = 0.0f;
                float skyNeighbor = 0.0f;

                [unroll]
                for (int axis = 0; axis < 2; ++axis)
                {
                    float2 direction = axis == 0 ? float2(1.0f, 0.0f) : float2(0.0f, 1.0f);
                    float2 positiveUV = saturate(screenUV + direction * texel);
                    float2 negativeUV = saturate(screenUV - direction * texel);
                    float positiveRawDepth = BurtSampleDeferredRawDepth(positiveUV);
                    float negativeRawDepth = BurtSampleDeferredRawDepth(negativeUV);
                    bool positiveIsSky = BurtGIIsSkyDepth(positiveRawDepth);
                    bool negativeIsSky = BurtGIIsSkyDepth(negativeRawDepth);

                    if (positiveIsSky || negativeIsSky)
                    {
                        float2 positiveFarUV = saturate(screenUV + direction * texel * 2.0f);
                        float2 negativeFarUV = saturate(screenUV - direction * texel * 2.0f);
                        bool positiveFarIsSky = positiveIsSky && BurtGIIsSkyDepth(BurtSampleDeferredRawDepth(positiveFarUV));
                        bool negativeFarIsSky = negativeIsSky && BurtGIIsSkyDepth(BurtSampleDeferredRawDepth(negativeFarUV));
                        skyNeighbor = max(skyNeighbor, positiveFarIsSky || negativeFarIsSky ? 1.0f : 0.0f);
                        continue;
                    }

                    float positiveLinearDepth = LinearEyeDepth(positiveRawDepth);
                    float negativeLinearDepth = LinearEyeDepth(negativeRawDepth);
                    float pairSlope = abs(positiveLinearDepth - negativeLinearDepth) * 0.5f;
                    float pairCurvature = abs(positiveLinearDepth + negativeLinearDepth - centerLinearDepth * 2.0f);
                    float positiveCenterDelta = abs(positiveLinearDepth - centerLinearDepth);
                    float negativeCenterDelta = abs(negativeLinearDepth - centerLinearDepth);
                    float oneSidedStep = max(positiveCenterDelta, negativeCenterDelta);
                    float depthScale = max(centerLinearDepth * lerp(0.08f, 0.035f, saturate(_BurtGIParams4.x)), 0.12f);
                    float quantizationFloor = max(centerLinearDepth * 0.006f, 0.015f);
                    float curvatureSignal = max(pairCurvature - pairSlope * 0.75f - quantizationFloor, 0.0f);
                    float stepSignal = max(oneSidedStep - pairSlope * 0.5f - quantizationFloor, 0.0f);
                    float axisDepthEdge = saturate(max(curvatureSignal, stepSignal) / depthScale);
                    maxDepthEdge = max(maxDepthEdge, axisDepthEdge);

                    float positiveNormalDot = saturate(dot(centerNormalWS, BurtGISampleNormalWS(positiveUV)));
                    float negativeNormalDot = saturate(dot(centerNormalWS, BurtGISampleNormalWS(negativeUV)));
                    minNormalDot = min(minNormalDot, min(positiveNormalDot, negativeNormalDot));
                }

                float normalEdge = saturate((1.0f - minNormalDot) * lerp(1.5f, 5.0f, saturate(_BurtGIParams4.z)));
                float depthEdge = maxDepthEdge * lerp(0.35f, 1.0f, maxDepthEdge);
                return saturate(max(max(depthEdge, normalEdge), skyNeighbor * saturate(_BurtGIParams4.w)));
            }

            float BurtGIComputeDiffuseSourceWeight(BurtPBRMaterialData sampleMaterialData)
            {
                float diffuseLuma = dot(max(sampleMaterialData.diffuseColor, 0.0f), float3(0.2126f, 0.7152f, 0.0722f));
                float baseLuma = dot(max(sampleMaterialData.baseColor, 0.0f), float3(0.2126f, 0.7152f, 0.0722f));
                float diffuseFraction = saturate(diffuseLuma / max(baseLuma, 0.001f));
                float roughDiffuseWeight = lerp(0.35f, 1.0f, saturate(sampleMaterialData.perceptualRoughness));
                float sourceOcclusion = lerp(0.25f, 1.0f, saturate(sampleMaterialData.occlusion));
                return diffuseFraction * roughDiffuseWeight * sourceOcclusion;
            }

            float3 BurtGITintRadianceByDiffuseAlbedo(float3 sourceRadiance, BurtPBRMaterialData sampleMaterialData)
            {
                float3 lumaWeights = float3(0.2126f, 0.7152f, 0.0722f);
                float3 diffuseColor = max(sampleMaterialData.diffuseColor, 0.0f);
                float diffuseLuma = dot(diffuseColor, lumaWeights);
                float sourceLuma = dot(max(sourceRadiance, 0.0f), lumaWeights);
                float maxChannel = max(diffuseColor.r, max(diffuseColor.g, diffuseColor.b));
                float minChannel = min(diffuseColor.r, min(diffuseColor.g, diffuseColor.b));
                float chroma = saturate((maxChannel - minChannel) / max(maxChannel, 0.001f));
                float tintWeight = smoothstep(0.025f, 0.18f, diffuseLuma) * saturate(chroma * 1.35f);
                float3 normalizedDiffuse = min(diffuseColor / max(diffuseLuma, 0.035f), 4.0f);
                float colorBleedBoost = lerp(1.0f, 1.65f, tintWeight);
                float3 diffuseTintedRadiance = normalizedDiffuse * sourceLuma * colorBleedBoost;
                return BurtGIClampRadiance(lerp(sourceRadiance, diffuseTintedRadiance, tintWeight));
            }

            float3 BurtGIEstimateSampleDiffuseRadiance(BurtPBRMaterialData sampleMaterialData, float3 sampleNormalWS, float3 sampleEmission)
            {
                float3 diffuseIrradiance = BurtSampleIndirectDiffuseIrradiance(sampleNormalWS);
                float3 diffuseRadiance = max(sampleMaterialData.diffuseColor, 0.0f) * max(diffuseIrradiance, 0.0f);
                diffuseRadiance += max(sampleEmission, 0.0f);
                return BurtGIClampRadiance(diffuseRadiance);
            }

            float BurtGIComputeDiffuseChroma(float3 diffuseColor)
            {
                float maxChannel = max(diffuseColor.r, max(diffuseColor.g, diffuseColor.b));
                float minChannel = min(diffuseColor.r, min(diffuseColor.g, diffuseColor.b));
                return saturate((maxChannel - minChannel) / max(maxChannel, 0.001f));
            }

            float2 BurtGINearFieldColorBleedOffset(int index)
            {
                float2 direction = 0.0f;
                if (index == 0) direction = float2(1.0f, 0.0f);
                if (index == 1) direction = float2(-1.0f, 0.0f);
                if (index == 2) direction = float2(0.0f, 1.0f);
                if (index == 3) direction = float2(0.0f, -1.0f);
                if (index == 4) direction = float2(1.0f, 1.0f);
                if (index == 5) direction = float2(-1.0f, 1.0f);
                if (index == 6) direction = float2(1.0f, -1.0f);
                if (index == 7) direction = float2(-1.0f, -1.0f);
                if (index == 8) direction = float2(2.0f, 1.0f);
                if (index == 9) direction = float2(-2.0f, 1.0f);
                if (index == 10) direction = float2(2.0f, -1.0f);
                if (index == 11) direction = float2(-2.0f, -1.0f);
                if (index == 12) direction = float2(1.0f, 2.0f);
                if (index == 13) direction = float2(-1.0f, 2.0f);
                if (index == 14) direction = float2(1.0f, -2.0f);
                if (index == 15) direction = float2(-1.0f, -2.0f);
                if (index == 16) direction = float2(3.0f, 1.0f);
                if (index == 17) direction = float2(-3.0f, 1.0f);
                if (index == 18) direction = float2(3.0f, -1.0f);
                if (index == 19) direction = float2(-3.0f, -1.0f);
                if (index == 20) direction = float2(1.0f, 3.0f);
                if (index == 21) direction = float2(-1.0f, 3.0f);
                if (index == 22) direction = float2(1.0f, -3.0f);
                if (index == 23) direction = float2(-1.0f, -3.0f);
                return direction * rsqrt(max(dot(direction, direction), 0.0001f));
            }

            void BurtGIGatherNearFieldColorBleed(
                float2 screenUV,
                float3 positionWS,
                float3 normalWS,
                float radius,
                float centerEdgeFactor,
                out float3 bleedRadiance,
                out float bleedStrength)
            {
                float3 lumaWeights = float3(0.2126f, 0.7152f, 0.0722f);
                float centerEdgeGate = 1.0f - smoothstep(0.18f, 0.65f, centerEdgeFactor);
                if (centerEdgeGate <= 0.001f)
                {
                    bleedRadiance = 0.0f;
                    bleedStrength = 0.0f;
                    return;
                }

                float screenRadiusPixels = clamp(10.0f + radius * 18.0f, 16.0f, 52.0f) * lerp(0.55f, 1.0f, centerEdgeGate);
                float3 radianceSum = 0.0f;
                float weightSum = 0.0f;

                [loop]
                for (int i = 0; i < 24; ++i)
                {
                    float ring = i < 8 ? 0.38f : (i < 16 ? 0.76f : 1.16f);
                    float ringWeight = i < 8 ? 1.0f : (i < 16 ? 0.72f : 0.48f);
                    float2 sampleUV = saturate(screenUV + BurtGINearFieldColorBleedOffset(i) * _BurtGISourceTexelSize.xy * screenRadiusPixels * ring);
                    float sampleRawDepth = BurtSampleDeferredRawDepth(sampleUV);
                    if (BurtGIIsSkyDepth(sampleRawDepth))
                    {
                        continue;
                    }

                    float3 samplePositionWS = BurtReconstructDeferredPositionWS(sampleUV, sampleRawDepth);
                    float3 deltaWS = samplePositionWS - positionWS;
                    float distanceWS = length(deltaWS);
                    if (distanceWS <= 0.0001f || distanceWS > radius * 2.75f)
                    {
                        continue;
                    }

                    BurtEncodedGBuffer sampleEncodedGBuffer = BurtSampleEncodedGBuffer(sampleUV);
                    BurtPBRMaterialData sampleMaterialData = BurtPreparePBRMaterialData(BurtDecodeGBuffer(sampleEncodedGBuffer));
                    float3 sampleDiffuse = max(sampleMaterialData.diffuseColor, 0.0f);
                    float sampleDiffuseLuma = dot(sampleDiffuse, lumaWeights);
                    float sampleChroma = BurtGIComputeDiffuseChroma(sampleDiffuse);
                    float colorSourceWeight = smoothstep(0.05f, 0.26f, sampleChroma) * smoothstep(0.025f, 0.14f, sampleDiffuseLuma);
                    if (colorSourceWeight <= 0.0001f)
                    {
                        continue;
                    }

                    float3 toSampleWS = deltaWS / max(distanceWS, 0.0001f);
                    float3 sampleNormalWS = BurtGISampleNormalWS(sampleUV);
                    float sampleEdgeFactor = BurtGIEdgeFactor(sampleUV, sampleRawDepth, sampleNormalWS);
                    float sampleEdgeGate = 1.0f - smoothstep(0.22f, 0.72f, sampleEdgeFactor);
                    float planeSeparationRatio = abs(dot(deltaWS, normalWS)) / max(distanceWS, 0.0001f);
                    float planeGate = 1.0f - smoothstep(0.10f, 0.32f, planeSeparationRatio);
                    float normalSimilarity = saturate(dot(normalWS, sampleNormalWS));
                    float normalSimilarityGate = smoothstep(0.18f, 0.72f, normalSimilarity);
                    float receiverFacing = smoothstep(-0.08f, 0.32f, dot(normalWS, toSampleWS));
                    float sourceFacing = smoothstep(-0.18f, 0.42f, dot(sampleNormalWS, -toSampleWS));
                    float distanceWeight = 1.0f - smoothstep(radius * 0.08f, radius * 2.45f, distanceWS);
                    float sourceWeight = max(BurtGIComputeDiffuseSourceWeight(sampleMaterialData), colorSourceWeight * 0.72f);
                    float sampleWeight = ringWeight * colorSourceWeight * receiverFacing * sourceFacing * distanceWeight * sourceWeight;
                    sampleWeight *= centerEdgeGate * sampleEdgeGate * planeGate * normalSimilarityGate;
                    if (sampleWeight <= 0.0001f)
                    {
                        continue;
                    }

                    float3 sampleRadiance = BurtGIEstimateSampleDiffuseRadiance(sampleMaterialData, sampleNormalWS, sampleEncodedGBuffer.gbuffer2.rgb);
                    sampleRadiance = BurtGITintRadianceByDiffuseAlbedo(sampleRadiance, sampleMaterialData);
                    radianceSum += sampleRadiance * sampleWeight;
                    weightSum += sampleWeight;
                }

                bleedRadiance = weightSum > 0.0001f ? radianceSum / weightSum : 0.0f;
                bleedStrength = saturate(weightSum * 0.32f * centerEdgeGate);
            }

            bool BurtGIProjectHistoryUV(float3 positionWS, out float2 historyUV, out float projectedRawDepth)
            {
                float4 previousClip = mul(_BurtGIPreviousViewProjectionMatrix, float4(positionWS, 1.0f));
                if (previousClip.w <= 0.00001f)
                {
                    historyUV = 0.0f;
                    projectedRawDepth = 0.0f;
                    return false;
                }

                float3 previousNDC = previousClip.xyz / previousClip.w;
                historyUV = BurtGIClipToScreenUV(previousNDC.xy);
                projectedRawDepth = BurtGIRawDepthFromClip(previousNDC.z);
                return !any(historyUV < 0.0f) && !any(historyUV > 1.0f) && !BurtGIIsSkyDepth(projectedRawDepth);
            }

            float4 BurtGISampleHistoryDepthNormalClosest(float2 historyUV, float projectedRawDepth)
            {
                float4 bestDepthNormal = tex2D(_BurtGIHistoryDepthNormalTexture, historyUV);
                float projectedLinearDepth = LinearEyeDepth(projectedRawDepth);
                float bestDepthError = BurtGIIsSkyDepth(bestDepthNormal.r) ? 1.0e20f : abs(LinearEyeDepth(bestDepthNormal.r) - projectedLinearDepth);
                float2 texel = _BurtGISourceTexelSize.xy;

                [unroll]
                for (int i = 0; i < 4; ++i)
                {
                    float2 direction = 0.0f;
                    if (i == 0) direction = float2(1.0f, 0.0f);
                    if (i == 1) direction = float2(-1.0f, 0.0f);
                    if (i == 2) direction = float2(0.0f, 1.0f);
                    if (i == 3) direction = float2(0.0f, -1.0f);

                    float4 sampleDepthNormal = tex2D(_BurtGIHistoryDepthNormalTexture, saturate(historyUV + direction * texel));
                    if (BurtGIIsSkyDepth(sampleDepthNormal.r))
                    {
                        continue;
                    }

                    float sampleDepthError = abs(LinearEyeDepth(sampleDepthNormal.r) - projectedLinearDepth);
                    if (sampleDepthError < bestDepthError)
                    {
                        bestDepthError = sampleDepthError;
                        bestDepthNormal = sampleDepthNormal;
                    }
                }

                return bestDepthNormal;
            }

            void BurtGISampleTemporalNeighborhoodStats(float2 screenUV, out float4 centerBurtGI, out float3 minBurtGI, out float3 maxBurtGI, out float3 averageBurtGI, out float3 sigmaBurtGI)
            {
                float2 texel = _BurtGISourceTexelSize.xy;
                centerBurtGI = tex2D(_BurtGISpatialFinalTexture, screenUV);
                float3 sumBurtGI = centerBurtGI.rgb;
                float3 sumSqBurtGI = centerBurtGI.rgb * centerBurtGI.rgb;
                minBurtGI = centerBurtGI.rgb;
                maxBurtGI = centerBurtGI.rgb;
                float weight = 1.0f;

                [unroll]
                for (int i = 0; i < 8; ++i)
                {
                    float2 direction = 0.0f;
                    if (i == 0) direction = float2(1.0f, 0.0f);
                    if (i == 1) direction = float2(-1.0f, 0.0f);
                    if (i == 2) direction = float2(0.0f, 1.0f);
                    if (i == 3) direction = float2(0.0f, -1.0f);
                    if (i == 4) direction = float2(1.0f, 1.0f);
                    if (i == 5) direction = float2(-1.0f, 1.0f);
                    if (i == 6) direction = float2(1.0f, -1.0f);
                    if (i == 7) direction = float2(-1.0f, -1.0f);

                    float3 sampleBurtGI = tex2D(_BurtGISpatialFinalTexture, saturate(screenUV + direction * texel)).rgb;
                    minBurtGI = min(minBurtGI, sampleBurtGI);
                    maxBurtGI = max(maxBurtGI, sampleBurtGI);
                    sumBurtGI += sampleBurtGI;
                    sumSqBurtGI += sampleBurtGI * sampleBurtGI;
                    weight += 1.0f;
                }

                averageBurtGI = sumBurtGI / max(weight, 0.0001f);
                float3 variance = max(sumSqBurtGI / max(weight, 0.0001f) - averageBurtGI * averageBurtGI, 0.0f);
                sigmaBurtGI = sqrt(variance);
            }

            float4 FragTrace(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenUV;
                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtGIIsSkyDepth(rawDepth))
                {
                    return 0.0f;
                }

                BurtEncodedGBuffer encodedGBuffer = BurtSampleEncodedGBuffer(screenUV);
                BurtGBufferData gbufferData = BurtDecodeGBuffer(encodedGBuffer);
                BurtPBRMaterialData materialData = BurtPreparePBRMaterialData(gbufferData);
                float3 normalWS = BurtSafeNormalize(gbufferData.normalWS);
                float3 positionWS = BurtReconstructDeferredPositionWS(screenUV, rawDepth);
                float centerLinearDepth = LinearEyeDepth(rawDepth);
                float distanceFade = 1.0f - saturate(centerLinearDepth / max(_BurtGIParams2.z, 1.0f));
                float edgeFactor = BurtGIEdgeFactor(screenUV, rawDepth, normalWS);
                float leakGuard = saturate(_BurtGIParams4.x);

                float3 tangentWS;
                float3 bitangentWS;
                BurtGIBuildTangentBasis(normalWS, tangentWS, bitangentWS);

                int sampleCount = clamp((int)round(_BurtGIParams0.y), 1, 32);
                int maxSteps = clamp((int)round(_BurtGIParams0.z), 1, 64);
                int stepCount = min(maxSteps, sampleCount);
                float radius = max(_BurtGIParams0.x, 0.05f);
                float thickness = max(_BurtGIParams0.w, 0.01f);
                float normalWeightAmount = saturate(_BurtGIParams2.y);
                float2 pixelPosition = screenUV * _BurtGISourceTexelSize.zw;
                float rotation = BurtGIHash12(pixelPosition, _BurtGIParams2.x) * 6.2831853f;
                float sequenceJitter = BurtGIHash12(pixelPosition + 17.17f, _BurtGIParams2.x + 5.0f);
                float3 tracedRadiance = 0.0f;
                float totalWeight = 0.0f;

                [loop]
                for (int i = 0; i < 32; ++i)
                {
                    if (i >= stepCount)
                    {
                        break;
                    }

                    float sampleJitter = BurtGIHash12(pixelPosition + (float)i * float2(19.19f, 47.13f), _BurtGIParams2.x + (float)i * 3.0f);
                    float sampleFraction = (float)i + sampleJitter;
                    sampleFraction = saturate((sampleFraction + 0.35f) / (float)sampleCount);
                    float angle = rotation + ((float)i + sequenceJitter) * 2.3999632f;
                    float3 sampleDirectionWS = BurtGIBuildSampleDirection(normalWS, tangentWS, bitangentWS, angle, sampleFraction);
                    float sampleDistance = radius * lerp(0.12f, 1.0f, sampleFraction * sampleFraction);
                    float3 probePositionWS = positionWS + sampleDirectionWS * sampleDistance;
                    float2 sampleUV;
                    float probeRawDepth;
                    float probeLinearDepth;
                    if (!BurtGIProjectPositionWS(probePositionWS, sampleUV, probeRawDepth, probeLinearDepth))
                    {
                        continue;
                    }

                    float sampleRawDepth = BurtSampleDeferredRawDepth(sampleUV);
                    if (BurtGIIsSkyDepth(sampleRawDepth))
                    {
                        continue;
                    }

                    float3 samplePositionWS = BurtReconstructDeferredPositionWS(sampleUV, sampleRawDepth);
                    float3 deltaWS = samplePositionWS - positionWS;
                    float distanceWS = length(deltaWS);
                    if (distanceWS <= 0.0001f)
                    {
                        continue;
                    }

                    float3 sampleDirectionFromCenterWS = deltaWS / distanceWS;
                    float3 sampleToCenterWS = -deltaWS / max(distanceWS, 0.0001f);
                    float3 sampleNormalWS = BurtGISampleNormalWS(sampleUV);
                    float normalSimilarity = saturate(dot(normalWS, sampleNormalWS));
                    float normalDifferent = 1.0f - smoothstep(0.9f, 0.985f, normalSimilarity);
                    float planeSeparationRatio = abs(dot(deltaWS, normalWS)) / max(distanceWS, 0.0001f);
                    float planeSeparated = smoothstep(lerp(0.04f, 0.08f, leakGuard), lerp(0.14f, 0.22f, leakGuard), planeSeparationRatio);
                    float coplanarGate = 1.0f - max(normalDifferent, planeSeparated);
                    float sampleLinearDepth = LinearEyeDepth(sampleRawDepth);
                    float depthError = abs(probeLinearDepth - sampleLinearDepth);
                    float guardedThickness = thickness * lerp(1.0f, 0.55f, leakGuard * edgeFactor);
                    float depthWeight = 1.0f - smoothstep(guardedThickness, guardedThickness + max(radius * lerp(0.3f, 0.14f, leakGuard), 0.01f), depthError);
                    float distanceWeight = 1.0f - smoothstep(radius * 0.15f, radius * 1.35f, distanceWS);
                    float normalFacingWeight = smoothstep(0.06f, 0.28f, saturate(dot(normalWS, sampleDirectionFromCenterWS)));
                    float edgeNormalWeightAmount = saturate(normalWeightAmount + leakGuard * edgeFactor * 0.35f);
                    float sampleNormalWeight = lerp(1.0f, saturate(dot(sampleNormalWS, sampleToCenterWS)), edgeNormalWeightAmount);
                    float surfaceCone = smoothstep(lerp(0.12f, 0.45f, saturate(_BurtGIParams4.z)), 1.0f, saturate(dot(normalWS, sampleNormalWS)));
                    float weight = depthWeight * distanceWeight * normalFacingWeight * sampleNormalWeight * lerp(1.0f, surfaceCone, leakGuard) * coplanarGate;
                    if (weight <= 0.0001f)
                    {
                        continue;
                    }

                    BurtEncodedGBuffer sampleEncodedGBuffer = BurtSampleEncodedGBuffer(sampleUV);
                    BurtPBRMaterialData sampleMaterialData = BurtPreparePBRMaterialData(BurtDecodeGBuffer(sampleEncodedGBuffer));
                    float3 sampleRadiance = BurtGIEstimateSampleDiffuseRadiance(sampleMaterialData, sampleNormalWS, sampleEncodedGBuffer.gbuffer2.rgb);
                    sampleRadiance = BurtGITintRadianceByDiffuseAlbedo(sampleRadiance, sampleMaterialData);
                    sampleRadiance *= BurtGIComputeDiffuseSourceWeight(sampleMaterialData);
                    tracedRadiance += sampleRadiance * weight;
                    totalWeight += weight;
                }

                float hitRatio = saturate(totalWeight / max((float)stepCount * 0.35f, 1.0f));
                float3 screenIrradiance = totalWeight > 0.0001f ? tracedRadiance / totalWeight : 0.0f;
                float screenHitEnergy = totalWeight > 0.0001f ? saturate(lerp(sqrt(hitRatio), pow(hitRatio, 0.35f), 0.55f)) : 0.0f;
                float3 nearFieldColorBleed = 0.0f;
                float nearFieldColorBleedStrength = 0.0f;
                BurtGIGatherNearFieldColorBleed(screenUV, positionWS, normalWS, radius, edgeFactor, nearFieldColorBleed, nearFieldColorBleedStrength);
                float nearFieldEnergy = nearFieldColorBleedStrength * (1.0f - screenHitEnergy * 0.35f);
                screenIrradiance += nearFieldColorBleed * nearFieldEnergy;
                screenHitEnergy = saturate(screenHitEnergy + nearFieldEnergy);
                float skyFallbackWeight = 1.0f - screenHitEnergy;
                float3 skyDiffuse = BurtSampleIndirectDiffuseIrradiance(normalWS) * _BurtGIParams1.y * skyFallbackWeight;
                float3 diffuseOcclusion = BurtGTAOMultiBounce(materialData.occlusion, materialData.baseColor);
                float3 indirectDiffuse = materialData.diffuseColor * (screenIrradiance * screenHitEnergy + skyDiffuse) * diffuseOcclusion * distanceFade;
                return float4(max(indirectDiffuse, 0.0f), hitRatio);
            }

            float4 FragBlur(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenUV;
                float4 center = tex2D(_BurtScreenSpaceGlobalIlluminationRawTexture, screenUV);
                if (_BurtGIParams1.z < 0.5f)
                {
                    return center;
                }

                float centerRawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtGIIsSkyDepth(centerRawDepth))
                {
                    return 0.0f;
                }

                float centerLinearDepth = LinearEyeDepth(centerRawDepth);
                float3 centerNormalWS = BurtGISampleNormalWS(screenUV);
                float centerEdgeFactor = BurtGIEdgeFactor(screenUV, centerRawDepth, centerNormalWS);
                float2 texel = _BurtGISourceTexelSize.xy;
                float sharpness = lerp(0.04f, 0.45f, saturate(_BurtGIParams1.w));
                float spatialRadius = max(_BurtGIParams3.x, 0.5f);
                float spatialStrength = saturate(_BurtGIParams3.y);
                float leakGuard = saturate(_BurtGIParams4.x);
                float centerWeight = lerp(4.0f, 6.0f, spatialStrength);
                float3 colorSum = center.rgb * centerWeight;
                float colorWeightSum = centerWeight;
                float hitSum = saturate(center.a) * centerWeight;
                float hitWeightSum = centerWeight;

                [unroll]
                for (int i = 0; i < 16; ++i)
                {
                    float2 direction = 0.0f;
                    if (i == 0) direction = float2(1.0f, 0.0f);
                    if (i == 1) direction = float2(-1.0f, 0.0f);
                    if (i == 2) direction = float2(0.0f, 1.0f);
                    if (i == 3) direction = float2(0.0f, -1.0f);
                    if (i == 4) direction = float2(1.0f, 1.0f);
                    if (i == 5) direction = float2(-1.0f, 1.0f);
                    if (i == 6) direction = float2(1.0f, -1.0f);
                    if (i == 7) direction = float2(-1.0f, -1.0f);
                    if (i == 8) direction = float2(2.0f, 0.0f);
                    if (i == 9) direction = float2(-2.0f, 0.0f);
                    if (i == 10) direction = float2(0.0f, 2.0f);
                    if (i == 11) direction = float2(0.0f, -2.0f);
                    if (i == 12) direction = float2(2.0f, 1.0f);
                    if (i == 13) direction = float2(-2.0f, 1.0f);
                    if (i == 14) direction = float2(1.0f, 2.0f);
                    if (i == 15) direction = float2(1.0f, -2.0f);

                    float2 sampleUV = saturate(screenUV + direction * texel * spatialRadius);
                    float sampleRawDepth = BurtSampleDeferredRawDepth(sampleUV);
                    if (BurtGIIsSkyDepth(sampleRawDepth))
                    {
                        continue;
                    }

                    float depthDelta = abs(LinearEyeDepth(sampleRawDepth) - centerLinearDepth);
                    float edgeSharpness = lerp(sharpness, sharpness * 3.5f, leakGuard * centerEdgeFactor);
                    float depthWeight = exp(-depthDelta * edgeSharpness / max(spatialRadius, 0.5f));
                    float3 sampleNormalWS = BurtGISampleNormalWS(sampleUV);
                    float normalDot = saturate(dot(centerNormalWS, sampleNormalWS));
                    float normalPower = lerp(10.0f, 18.0f, spatialStrength) * lerp(1.0f, 1.8f, saturate(_BurtGIParams4.z) * centerEdgeFactor);
                    float normalWeight = pow(normalDot, normalPower);
                    float edgeCrossWeight = lerp(1.0f, smoothstep(0.35f, 1.0f, normalDot), leakGuard * centerEdgeFactor);
                    float ringWeight = i < 4 ? 1.0f : (i < 8 ? 0.7f : 0.42f);
                    float4 sampleBurtGI = tex2D(_BurtScreenSpaceGlobalIlluminationRawTexture, sampleUV);
                    float sampleHitRatio = saturate(sampleBurtGI.a);
                    float hitWeight = lerp(0.55f, 1.0f, sampleHitRatio);
                    float hitDeltaWeight = lerp(1.0f, 1.0f - smoothstep(0.25f, 0.85f, abs(sampleHitRatio - saturate(center.a))), leakGuard * centerEdgeFactor);
                    float baseWeight = depthWeight * normalWeight * edgeCrossWeight * ringWeight * spatialStrength;
                    float colorWeight = baseWeight * hitWeight * hitDeltaWeight;
                    float hitFilterWeight = baseWeight * lerp(1.0f, hitDeltaWeight, leakGuard * centerEdgeFactor * 0.65f);
                    colorSum += sampleBurtGI.rgb * colorWeight;
                    colorWeightSum += colorWeight;
                    hitSum += sampleHitRatio * hitFilterWeight;
                    hitWeightSum += hitFilterWeight;
                }

                float4 filtered = float4(colorSum / max(colorWeightSum, 0.0001f), hitSum / max(hitWeightSum, 0.0001f));
                return lerp(center, filtered, spatialStrength);
            }

            float4 FragTemporal(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenUV;
                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                float4 centerBurtGI;
                float3 minBurtGI;
                float3 maxBurtGI;
                float3 averageBurtGI;
                float3 sigmaBurtGI;
                BurtGISampleTemporalNeighborhoodStats(screenUV, centerBurtGI, minBurtGI, maxBurtGI, averageBurtGI, sigmaBurtGI);

                if (_BurtGITemporalParams.y < 0.5f || BurtGIIsSkyDepth(rawDepth))
                {
                    return centerBurtGI;
                }

                float3 positionWS = BurtReconstructDeferredPositionWS(screenUV, rawDepth);
                float2 historyUV;
                float projectedRawDepth;
                if (!BurtGIProjectHistoryUV(positionWS, historyUV, projectedRawDepth))
                {
                    return centerBurtGI;
                }

                float4 historyDepthNormal = BurtGISampleHistoryDepthNormalClosest(historyUV, projectedRawDepth);
                float historyRawDepth = historyDepthNormal.r;
                if (BurtGIIsSkyDepth(historyRawDepth))
                {
                    return centerBurtGI;
                }

                float projectedLinearDepth = LinearEyeDepth(projectedRawDepth);
                float historyLinearDepth = LinearEyeDepth(historyRawDepth);
                float depthTolerance = max(projectedLinearDepth * max(_BurtGITemporalParams.z, 0.0001f), 0.025f);
                float depthValidity = saturate(1.0f - abs(historyLinearDepth - projectedLinearDepth) / depthTolerance);
                if (depthValidity <= 0.0001f)
                {
                    return centerBurtGI;
                }

                float3 currentNormalWS = BurtGISampleNormalWS(screenUV);
                float edgeFactor = BurtGIEdgeFactor(screenUV, rawDepth, currentNormalWS);
                float3 historyNormalWS = BurtDecodeNormalWSFromGBuffer(historyDepthNormal.gb);
                float normalThreshold = saturate(_BurtGITemporalParams.w);
                float normalValidity = saturate((saturate(dot(currentNormalWS, historyNormalWS)) - normalThreshold) / max(1.0f - normalThreshold, 0.0001f));
                if (normalValidity <= 0.0001f)
                {
                    return centerBurtGI;
                }

                float3 historyBurtGI = max(tex2D(_BurtGIHistoryTexture, historyUV).rgb, 0.0f);
                float3 localRange = max(maxBurtGI - minBurtGI, 0.001f);
                float clampPadScale = max(_BurtGITemporalParams1.x, 0.0f);
                float varianceClampScale = max(_BurtGITemporalParams1.y, 0.0f);
                float3 varianceMin = averageBurtGI - sigmaBurtGI * varianceClampScale;
                float3 varianceMax = averageBurtGI + sigmaBurtGI * varianceClampScale;
                float3 historyClampMin = max(minBurtGI - localRange * clampPadScale, varianceMin);
                float3 historyClampMax = min(maxBurtGI + localRange * clampPadScale, varianceMax);
                float3 historyClampCenter = 0.5f * (historyClampMin + historyClampMax);
                float3 historyClampHalfRange = max(0.5f * abs(historyClampMax - historyClampMin), 0.0005f);
                historyClampMin = historyClampCenter - historyClampHalfRange;
                historyClampMax = historyClampCenter + historyClampHalfRange;
                float3 clampedHistory = clamp(historyBurtGI, historyClampMin, historyClampMax);
                float3 historyDelta = abs(historyBurtGI - centerBurtGI.rgb);
                float historyConsistency = saturate(1.0f - dot(historyDelta, float3(0.2126f, 0.7152f, 0.0722f)) / max(dot(localRange, float3(0.2126f, 0.7152f, 0.0722f)), 0.02f));
                historyConsistency = lerp(0.65f, 1.0f, historyConsistency);
                float historyHitRatio = saturate(tex2D(_BurtGIHistoryTexture, historyUV).a);
                float hitDelta = abs(historyHitRatio - saturate(centerBurtGI.a));
                float hitValidity = lerp(0.7f, 1.0f, saturate(1.0f - hitDelta / max(_BurtGITemporalParams1.z, 0.001f)));
                float edgeValidity = lerp(1.0f, 1.0f - edgeFactor, saturate(_BurtGIParams4.y) * saturate(_BurtGIParams4.x));
                float spatialCurrentBlend = lerp(0.18f, 0.35f, saturate(_BurtGITemporalParams1.w));
                float feedback = saturate(_BurtGITemporalParams.x) * depthValidity * normalValidity * hitValidity * edgeValidity * historyConsistency;
                float3 currentBurtGI = lerp(centerBurtGI.rgb, averageBurtGI, spatialCurrentBlend);
                float3 resolvedBurtGI = lerp(currentBurtGI, clampedHistory, feedback);
                float resolvedHitRatio = lerp(centerBurtGI.a, historyHitRatio, feedback);
                return float4(max(resolvedBurtGI, 0.0f), saturate(resolvedHitRatio));
            }

            float4 FragCopyDepthNormal(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenUV;
                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                float2 encodedNormal = BurtEncodeNormalWSForGBuffer(BurtGISampleNormalWS(screenUV));
                return float4(rawDepth, encodedNormal, 1.0f);
            }

            float4 FragCopyTemporalFinal(Varyings input) : SV_Target
            {
                return tex2D(_BurtGITemporalFinalTexture, input.screenUV);
            }

            float4 FragTemporalDiagnostics(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenUV;
                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                float4 centerBurtGI;
                float3 minBurtGI;
                float3 maxBurtGI;
                float3 averageBurtGI;
                float3 sigmaBurtGI;
                BurtGISampleTemporalNeighborhoodStats(screenUV, centerBurtGI, minBurtGI, maxBurtGI, averageBurtGI, sigmaBurtGI);

                if (_BurtGITemporalParams.y < 0.5f || BurtGIIsSkyDepth(rawDepth))
                {
                    return float4(0.0f, 1.0f, 0.0f, 1.0f);
                }

                float3 positionWS = BurtReconstructDeferredPositionWS(screenUV, rawDepth);
                float2 historyUV;
                float projectedRawDepth;
                if (!BurtGIProjectHistoryUV(positionWS, historyUV, projectedRawDepth))
                {
                    return float4(0.0f, 1.0f, 1.0f, 1.0f);
                }

                float4 historyDepthNormal = BurtGISampleHistoryDepthNormalClosest(historyUV, projectedRawDepth);
                float historyRawDepth = historyDepthNormal.r;
                if (BurtGIIsSkyDepth(historyRawDepth))
                {
                    return float4(0.0f, 1.0f, 0.75f, 1.0f);
                }

                float projectedLinearDepth = LinearEyeDepth(projectedRawDepth);
                float historyLinearDepth = LinearEyeDepth(historyRawDepth);
                float depthTolerance = max(projectedLinearDepth * max(_BurtGITemporalParams.z, 0.0001f), 0.025f);
                float depthValidity = saturate(1.0f - abs(historyLinearDepth - projectedLinearDepth) / depthTolerance);
                float3 currentNormalWS = BurtGISampleNormalWS(screenUV);
                float edgeFactor = BurtGIEdgeFactor(screenUV, rawDepth, currentNormalWS);
                float3 historyNormalWS = BurtDecodeNormalWSFromGBuffer(historyDepthNormal.gb);
                float normalThreshold = saturate(_BurtGITemporalParams.w);
                float normalValidity = saturate((saturate(dot(currentNormalWS, historyNormalWS)) - normalThreshold) / max(1.0f - normalThreshold, 0.0001f));

                float3 historyBurtGI = max(tex2D(_BurtGIHistoryTexture, historyUV).rgb, 0.0f);
                float historyHitRatio = saturate(tex2D(_BurtGIHistoryTexture, historyUV).a);
                float3 localRange = max(maxBurtGI - minBurtGI, 0.001f);
                float3 historyDelta = abs(historyBurtGI - centerBurtGI.rgb);
                float historyConsistency = saturate(1.0f - dot(historyDelta, float3(0.2126f, 0.7152f, 0.0722f)) / max(dot(localRange, float3(0.2126f, 0.7152f, 0.0722f)), 0.02f));
                historyConsistency = lerp(0.65f, 1.0f, historyConsistency);
                float varianceClampScale = max(_BurtGITemporalParams1.y, 0.0f);
                float3 varianceMin = averageBurtGI - sigmaBurtGI * varianceClampScale;
                float3 varianceMax = averageBurtGI + sigmaBurtGI * varianceClampScale;
                float3 varianceOverflow = max(max(historyBurtGI - varianceMax, varianceMin - historyBurtGI), 0.0f);
                float varianceValidity = saturate(1.0f - dot(varianceOverflow, float3(0.2126f, 0.7152f, 0.0722f)) / max(dot(localRange, float3(0.2126f, 0.7152f, 0.0722f)), 0.02f));
                varianceValidity = lerp(0.75f, 1.0f, varianceValidity);
                float hitDelta = abs(historyHitRatio - saturate(centerBurtGI.a));
                float hitValidity = lerp(0.7f, 1.0f, saturate(1.0f - hitDelta / max(_BurtGITemporalParams1.z, 0.001f)));
                float edgeValidity = lerp(1.0f, 1.0f - edgeFactor, saturate(_BurtGIParams4.y) * saturate(_BurtGIParams4.x));
                float confidence = saturate(depthValidity * normalValidity * historyConsistency * varianceValidity * hitValidity * edgeValidity * saturate(_BurtGITemporalParams.x));
                float rejection = 1.0f - confidence;
                return float4(confidence, rejection, saturate(centerBurtGI.a), 1.0f);
            }

            float4 FragComposite(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenUV;
                float4 cameraColor = tex2D(_BurtGICameraColorCopyTexture, screenUV);
                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtGIIsSkyDepth(rawDepth))
                {
                    return cameraColor;
                }

                float3 burtGI = tex2D(_BurtScreenSpaceGlobalIlluminationTexture, screenUV).rgb;
                float3 appliedGI = burtGI * _BurtGIParams1.x;
                cameraColor.rgb += appliedGI;
                if (BurtIsSubsurfaceShadingModel(BurtSampleDeferredShadingModelID(screenUV)))
                {
                    cameraColor.a += dot(max(appliedGI, 0.0f), float3(0.3f, 0.59f, 0.11f));
                }

                return cameraColor;
            }

            struct BurtGIIndirectChannelsOutput
            {
                float4 backfaceDiffuse : SV_Target0;
                float4 roughSpecular : SV_Target1;
            };

            float BurtGIBackfaceMaterialWeight(BurtGBufferData gbufferData)
            {
                float subsurfaceWeight = BurtIsActiveSubsurfaceShadingModel(gbufferData.shadingModelID)
                    ? saturate(BurtGetSubsurfaceThickness(gbufferData) * 0.65f + BurtGetSubsurfaceAmbient(gbufferData) * 0.35f)
                    : 0.0f;
                float hairWeight = BurtIsActiveHairShadingModel(gbufferData.shadingModelID) ? 0.65f : 0.0f;
                return saturate(max(subsurfaceWeight, hairWeight));
            }

            BurtGIIndirectChannelsOutput FragResolveIndirectChannels(Varyings input)
            {
                BurtGIIndirectChannelsOutput output;
                output.backfaceDiffuse = 0.0f;
                output.roughSpecular = 0.0f;

                float2 screenUV = input.screenUV;
                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtGIIsSkyDepth(rawDepth))
                {
                    return output;
                }

                BurtEncodedGBuffer encodedGBuffer = BurtSampleEncodedGBuffer(screenUV);
                BurtGBufferData gbufferData = BurtDecodeGBuffer(encodedGBuffer);
                BurtPBRMaterialData materialData = BurtPreparePBRMaterialData(gbufferData);
                float3 normalWS = BurtGetGBufferDirectionWS(gbufferData);
                float4 finalBurtGI = tex2D(_BurtScreenSpaceGlobalIlluminationTexture, screenUV);
                float3 diffuseGI = max(finalBurtGI.rgb, 0.0f);
                float hitRatio = saturate(finalBurtGI.a);

                float backfaceWeight = BurtGIBackfaceMaterialWeight(gbufferData);
                if (backfaceWeight > 0.0001f)
                {
                    float3 backIrradiance = BurtSampleIndirectDiffuseIrradiance(-normalWS) * materialData.diffuseColor;
                    float3 backfaceDiffuse = lerp(diffuseGI, backIrradiance, saturate(0.35f + backfaceWeight * 0.45f));
                    output.backfaceDiffuse = float4(max(backfaceDiffuse, 0.0f) * backfaceWeight, hitRatio);
                }

                float roughness = saturate(gbufferData.perceptualRoughness);
                float roughSpecularWeight = smoothstep(0.35f, 0.92f, roughness) * saturate(1.0f - materialData.metallic * 0.35f);
                if (roughSpecularWeight > 0.0001f)
                {
                    float3 positionWS = BurtReconstructDeferredPositionWS(screenUV, rawDepth);
                    float3 viewDirectionWS = BurtSafeNormalize(_BurtDeferredCameraWorldPosition.xyz - positionWS);
                    float3 reflectionDirectionWS = reflect(-viewDirectionWS, normalWS);
                    float3 specularRadiance = SampleIndirectSpecularRadiance(reflectionDirectionWS, roughness);
                    float3 roughSpecular = lerp(diffuseGI, specularRadiance, saturate(0.45f + roughness * 0.35f));
                    output.roughSpecular = float4(max(roughSpecular, 0.0f) * roughSpecularWeight * saturate(0.55f + hitRatio * 0.45f), hitRatio);
                }

                return output;
            }

            float4 BurtGIDebugHitRatio(float4 finalBurtGI)
            {
                float hitRatio = saturate(finalBurtGI.a);
                return float4(1.0f - hitRatio, hitRatio, 0.0f, 1.0f);
            }

            void BurtGIComputeDebugValidity(
                float2 screenUV,
                float4 finalBurtGI,
                out float surfaceMask,
                out float hitRatio,
                out float geometryMask,
                out float surfaceValidity,
                out float edgeLeakRisk,
                out float skyFallbackRisk)
            {
                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtGIIsSkyDepth(rawDepth))
                {
                    surfaceMask = 0.0f;
                    hitRatio = 0.0f;
                    geometryMask = 0.0f;
                    surfaceValidity = 0.0f;
                    edgeLeakRisk = 0.0f;
                    skyFallbackRisk = saturate(_BurtGIParams1.y) * 0.08f;
                    return;
                }

                float3 normalWS = BurtGISampleNormalWS(screenUV);
                float edgeFactor = BurtGIEdgeFactor(screenUV, rawDepth, normalWS);
                surfaceMask = 1.0f;
                hitRatio = saturate(finalBurtGI.a);
                geometryMask = surfaceMask;
                surfaceValidity = surfaceMask * lerp(1.0f, 1.0f - edgeFactor, saturate(_BurtGIParams4.y) * saturate(_BurtGIParams4.x));
                skyFallbackRisk = saturate((1.0f - hitRatio) * _BurtGIParams1.y) * surfaceMask;
                edgeLeakRisk = saturate(edgeFactor * lerp(0.75f, 1.3f, saturate(_BurtGIParams4.x))) * surfaceMask;
            }

            float4 BurtGIDebugLeakGuard(float2 screenUV, float4 finalBurtGI)
            {
                float surfaceMask;
                float hitRatio;
                float geometryMask;
                float surfaceValidity;
                float edgeLeakRisk;
                float skyFallbackRisk;
                BurtGIComputeDebugValidity(screenUV, finalBurtGI, surfaceMask, hitRatio, geometryMask, surfaceValidity, edgeLeakRisk, skyFallbackRisk);
                float geometryContext = geometryMask * 0.025f;
                return float4(edgeLeakRisk, geometryContext, skyFallbackRisk, 1.0f);
            }

            float4 BurtGIDebugConfidence(float2 screenUV)
            {
                float surfaceMask;
                float hitRatio;
                float geometryMask;
                float surfaceValidity;
                float edgeLeakRisk;
                float skyFallbackRisk;

                float2 quadrantUV = frac(screenUV * 2.0f);
                float4 quadrantFinalBurtGI = tex2D(_BurtScreenSpaceGlobalIlluminationTexture, quadrantUV);
                BurtGIComputeDebugValidity(quadrantUV, quadrantFinalBurtGI, surfaceMask, hitRatio, geometryMask, surfaceValidity, edgeLeakRisk, skyFallbackRisk);

                if (screenUV.y >= 0.5f)
                {
                    return screenUV.x < 0.5f
                        ? BurtGIDebugHitRatio(quadrantFinalBurtGI)
                        : float4((1.0f - surfaceValidity) * surfaceMask, surfaceValidity, geometryMask * 0.25f, 1.0f);
                }

                return screenUV.x < 0.5f
                    ? float4(edgeLeakRisk, 0.0f, 0.0f, 1.0f)
                    : float4(0.0f, 0.0f, skyFallbackRisk, 1.0f);
            }

            float4 FragDebug(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenUV;
                float4 rawBurtGI = tex2D(_BurtScreenSpaceGlobalIlluminationRawTexture, screenUV);
                float4 finalBurtGI = tex2D(_BurtScreenSpaceGlobalIlluminationTexture, screenUV);
                float debugMode = round(_BurtGIDebugMode);

                if (debugMode < 1.5f)
                {
                    return float4(max(rawBurtGI.rgb, 0.0f), 1.0f);
                }

                if (debugMode < 2.5f)
                {
                    return float4(max(finalBurtGI.rgb, 0.0f), 1.0f);
                }

                if (debugMode < 3.5f)
                {
                    return BurtGIDebugHitRatio(finalBurtGI);
                }

                float3 compositeContribution = max(finalBurtGI.rgb, 0.0f) * _BurtGIParams1.xxx;
                if (debugMode < 4.5f)
                {
                    float4 cameraColor = tex2D(_BurtGIDebugCameraColorTexture, screenUV);
                    cameraColor.rgb += compositeContribution;
                    return cameraColor;
                }

                if (debugMode < 5.5f)
                {
                    return float4(compositeContribution, 1.0f);
                }

                float4 temporalDiagnostics = tex2D(_BurtGITemporalDiagnosticsTexture, screenUV);
                if (debugMode < 6.5f)
                {
                    float confidence = saturate(temporalDiagnostics.r);
                    float rejection = saturate(temporalDiagnostics.g);
                    float hitRatio = saturate(temporalDiagnostics.b);
                    return float4(rejection, confidence, hitRatio * 0.45f, 1.0f);
                }

                if (debugMode < 7.5f)
                {
                    float rejection = saturate(temporalDiagnostics.g);
                    return float4(rejection, 1.0f - rejection, saturate(temporalDiagnostics.b), 1.0f);
                }

                float historyValid = _BurtGITemporalParams.y;
                float3 historyBurtGI = max(tex2D(_BurtGIPreviousHistoryTexture, screenUV).rgb, 0.0f);
                if (debugMode < 8.5f)
                {
                    return float4(historyBurtGI * historyValid, 1.0f);
                }

                float3 difference = abs(max(finalBurtGI.rgb, 0.0f) - historyBurtGI) * historyValid;
                if (debugMode < 9.5f)
                {
                    return float4(saturate(difference * 4.0f), 1.0f);
                }

                if (debugMode < 10.5f)
                {
                    return BurtGIDebugLeakGuard(screenUV, finalBurtGI);
                }

                if (debugMode > 11.5f && debugMode < 12.5f)
                {
                    return BurtGIDebugConfidence(screenUV);
                }

                float2 quadrantUV = frac(screenUV * 2.0f);
                if (screenUV.y >= 0.5f)
                {
                    return screenUV.x < 0.5f
                        ? float4(max(tex2D(_BurtScreenSpaceGlobalIlluminationRawTexture, quadrantUV).rgb, 0.0f), 1.0f)
                        : float4(max(tex2D(_BurtScreenSpaceGlobalIlluminationTexture, quadrantUV).rgb, 0.0f), 1.0f);
                }

                float4 quadrantFinalBurtGI = tex2D(_BurtScreenSpaceGlobalIlluminationTexture, quadrantUV);
                return screenUV.x < 0.5f
                    ? BurtGIDebugHitRatio(quadrantFinalBurtGI)
                    : BurtGIDebugLeakGuard(quadrantUV, quadrantFinalBurtGI);
            }
        ENDHLSL

        Pass
        {
            Name "Burt Screen Space Global Illumination Trace"
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
            Name "Burt Screen Space Global Illumination Blur"
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
            Name "Burt Screen Space Global Illumination Composite"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragComposite
            ENDHLSL
        }

        Pass
        {
            Name "Burt Screen Space Global Illumination Debug"
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
            Name "Burt Screen Space Global Illumination Temporal"
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
            Name "Burt Screen Space Global Illumination Copy Depth Normal"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragCopyDepthNormal
            ENDHLSL
        }

        Pass
        {
            Name "Burt Screen Space Global Illumination Copy Temporal Final"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragCopyTemporalFinal
            ENDHLSL
        }

        Pass
        {
            Name "Burt Screen Space Global Illumination Temporal Diagnostics"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragTemporalDiagnostics
            ENDHLSL
        }

        Pass
        {
            Name "Burt Screen Space Global Illumination Resolve Indirect Channels"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragResolveIndirectChannels
            ENDHLSL
        }
    }
}
