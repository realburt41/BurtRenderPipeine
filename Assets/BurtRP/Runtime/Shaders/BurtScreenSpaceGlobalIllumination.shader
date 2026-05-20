// BurtRP v0 screen-space diffuse GI. It is a small bridge toward XRender XGI/Lumen style lighting,
// not the final voxel/radiance-cache implementation.
Shader "Hidden/BurtRP/ScreenSpaceGlobalIllumination"
{
    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        HLSLINCLUDE
            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtDeferred.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLighting.hlsl"

            sampler2D _BurtXGISourceColorTexture;
            sampler2D _BurtScreenSpaceGlobalIlluminationRawTexture;
            sampler2D _BurtScreenSpaceGlobalIlluminationTexture;
            sampler2D _BurtXGISpatialFinalTexture;
            sampler2D _BurtXGITemporalFinalTexture;
            sampler2D _BurtXGIHistoryTexture;
            sampler2D _BurtXGIHistoryDepthNormalTexture;
            sampler2D _BurtXGICameraColorCopyTexture;
            sampler2D _BurtXGIDebugCameraColorTexture;
            float4x4 _BurtXGIViewMatrix;
            float4x4 _BurtXGIViewProjectionMatrix;
            float4x4 _BurtXGIPreviousViewProjectionMatrix;
            float4 _BurtXGISourceTexelSize; // xy=1/width,1/height, zw=width,height of the XGI target.
            float4 _BurtXGIParams0; // x=radius, y=sampleCount, z=maxSteps, w=thickness.
            float4 _BurtXGIParams1; // x=intensity, y=skyFallback, z=blur, w=blurSharpness.
            float4 _BurtXGIParams2; // x=frame salt, y=normalWeight, z=distanceFade, w=radianceClamp.
            float4 _BurtXGITemporalParams; // x=feedback, y=history valid, z=depth rejection, w=normal rejection.
            float4 _BurtXGITemporalParams1; // x=history clamp scale.
            float _BurtXGIDebugMode;

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

            bool BurtXGIIsSkyDepth(float rawDepth)
            {
                #if defined(UNITY_REVERSED_Z)
                    return rawDepth <= 0.00001f;
                #else
                    return rawDepth >= 0.99999f;
                #endif
            }

            float BurtXGIRawDepthFromClip(float clipZ)
            {
                #if defined(UNITY_REVERSED_Z)
                    return saturate(clipZ);
                #else
                    return saturate((clipZ - UNITY_NEAR_CLIP_VALUE) / max(1.0f - UNITY_NEAR_CLIP_VALUE, 0.00001f));
                #endif
            }

            float2 BurtXGIClipToScreenUV(float2 clipXY)
            {
                float2 uv = clipXY * 0.5f + 0.5f;
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0f - uv.y;
                #endif
                return uv;
            }

            bool BurtXGIProjectPositionWS(float3 positionWS, out float2 screenUV, out float rawDepth, out float linearDepth)
            {
                float4 clipPosition = mul(_BurtXGIViewProjectionMatrix, float4(positionWS, 1.0f));
                if (clipPosition.w <= 0.00001f)
                {
                    screenUV = 0.0f;
                    rawDepth = 0.0f;
                    linearDepth = 0.0f;
                    return false;
                }

                float3 ndc = clipPosition.xyz / clipPosition.w;
                screenUV = BurtXGIClipToScreenUV(ndc.xy);
                rawDepth = BurtXGIRawDepthFromClip(ndc.z);
                linearDepth = LinearEyeDepth(rawDepth);
                return !any(screenUV < 0.0f) && !any(screenUV > 1.0f) && !BurtXGIIsSkyDepth(rawDepth);
            }

            float BurtXGIRand(float2 pixelPosition)
            {
                return frac(52.9829189f * frac(dot(pixelPosition + _BurtXGIParams2.xx, float2(0.06711056f, 0.00583715f))));
            }

            float3 BurtXGISampleNormalWS(float2 screenUV)
            {
                return BurtDecodeNormalWSFromGBuffer(tex2D(_BurtGBuffer1, screenUV).rg);
            }

            void BurtXGIBuildTangentBasis(float3 normalWS, out float3 tangentWS, out float3 bitangentWS)
            {
                float3 upWS = abs(normalWS.y) < 0.99f ? float3(0.0f, 1.0f, 0.0f) : float3(1.0f, 0.0f, 0.0f);
                tangentWS = BurtSafeNormalize(cross(upWS, normalWS));
                bitangentWS = BurtSafeNormalize(cross(normalWS, tangentWS));
            }

            float3 BurtXGIBuildSampleDirection(float3 normalWS, float3 tangentWS, float3 bitangentWS, float angle, float sampleFraction)
            {
                float radial = sqrt(saturate(sampleFraction));
                float normalAmount = sqrt(saturate(1.0f - radial * radial));
                float2 diskDirection = float2(cos(angle), sin(angle)) * radial;
                return BurtSafeNormalize(tangentWS * diskDirection.x + bitangentWS * diskDirection.y + normalWS * normalAmount);
            }

            float3 BurtXGIClampRadiance(float3 radiance)
            {
                return min(max(radiance, 0.0f), _BurtXGIParams2.www);
            }

            float BurtXGIComputeDiffuseSourceWeight(BurtPBRMaterialData sampleMaterialData)
            {
                float diffuseLuma = dot(max(sampleMaterialData.diffuseColor, 0.0f), float3(0.2126f, 0.7152f, 0.0722f));
                float baseLuma = dot(max(sampleMaterialData.baseColor, 0.0f), float3(0.2126f, 0.7152f, 0.0722f));
                float diffuseFraction = saturate(diffuseLuma / max(baseLuma, 0.001f));
                float roughDiffuseWeight = lerp(0.35f, 1.0f, saturate(sampleMaterialData.perceptualRoughness));
                return diffuseFraction * roughDiffuseWeight * saturate(sampleMaterialData.occlusion);
            }

            bool BurtXGIProjectHistoryUV(float3 positionWS, out float2 historyUV, out float projectedRawDepth)
            {
                float4 previousClip = mul(_BurtXGIPreviousViewProjectionMatrix, float4(positionWS, 1.0f));
                if (previousClip.w <= 0.00001f)
                {
                    historyUV = 0.0f;
                    projectedRawDepth = 0.0f;
                    return false;
                }

                float3 previousNDC = previousClip.xyz / previousClip.w;
                historyUV = BurtXGIClipToScreenUV(previousNDC.xy);
                projectedRawDepth = BurtXGIRawDepthFromClip(previousNDC.z);
                return !any(historyUV < 0.0f) && !any(historyUV > 1.0f) && !BurtXGIIsSkyDepth(projectedRawDepth);
            }

            void BurtXGISampleTemporalNeighborhood(float2 screenUV, out float4 centerXGI, out float3 minXGI, out float3 maxXGI, out float3 averageXGI)
            {
                float2 texel = _BurtXGISourceTexelSize.xy;
                centerXGI = tex2D(_BurtXGISpatialFinalTexture, screenUV);
                float3 sumXGI = centerXGI.rgb;
                minXGI = centerXGI.rgb;
                maxXGI = centerXGI.rgb;
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

                    float3 sampleXGI = tex2D(_BurtXGISpatialFinalTexture, saturate(screenUV + direction * texel)).rgb;
                    minXGI = min(minXGI, sampleXGI);
                    maxXGI = max(maxXGI, sampleXGI);
                    sumXGI += sampleXGI;
                    weight += 1.0f;
                }

                averageXGI = sumXGI / max(weight, 0.0001f);
            }

            float4 FragTrace(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenUV;
                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtXGIIsSkyDepth(rawDepth))
                {
                    return 0.0f;
                }

                BurtEncodedGBuffer encodedGBuffer = BurtSampleEncodedGBuffer(screenUV);
                BurtGBufferData gbufferData = BurtDecodeGBuffer(encodedGBuffer);
                BurtPBRMaterialData materialData = BurtPreparePBRMaterialData(gbufferData);
                float3 normalWS = BurtSafeNormalize(gbufferData.normalWS);
                float3 positionWS = BurtReconstructDeferredPositionWS(screenUV, rawDepth);
                float centerLinearDepth = LinearEyeDepth(rawDepth);
                float distanceFade = 1.0f - saturate(centerLinearDepth / max(_BurtXGIParams2.z, 1.0f));

                float3 tangentWS;
                float3 bitangentWS;
                BurtXGIBuildTangentBasis(normalWS, tangentWS, bitangentWS);

                int sampleCount = clamp((int)round(_BurtXGIParams0.y), 1, 32);
                int maxSteps = clamp((int)round(_BurtXGIParams0.z), 1, 64);
                int stepCount = min(maxSteps, sampleCount);
                float radius = max(_BurtXGIParams0.x, 0.05f);
                float thickness = max(_BurtXGIParams0.w, 0.01f);
                float normalWeightAmount = saturate(_BurtXGIParams2.y);
                float rotation = BurtXGIRand(screenUV * _BurtXGISourceTexelSize.zw) * 6.2831853f;
                float3 tracedRadiance = 0.0f;
                float totalWeight = 0.0f;

                [loop]
                for (int i = 0; i < 32; ++i)
                {
                    if (i >= stepCount)
                    {
                        break;
                    }

                    float sampleFraction = ((float)i + 0.5f) / (float)sampleCount;
                    float angle = rotation + (float)i * 2.3999632f;
                    float3 sampleDirectionWS = BurtXGIBuildSampleDirection(normalWS, tangentWS, bitangentWS, angle, sampleFraction);
                    float sampleDistance = radius * lerp(0.18f, 1.0f, sampleFraction * sampleFraction);
                    float3 probePositionWS = positionWS + sampleDirectionWS * sampleDistance;
                    float2 sampleUV;
                    float probeRawDepth;
                    float probeLinearDepth;
                    if (!BurtXGIProjectPositionWS(probePositionWS, sampleUV, probeRawDepth, probeLinearDepth))
                    {
                        continue;
                    }

                    float sampleRawDepth = BurtSampleDeferredRawDepth(sampleUV);
                    if (BurtXGIIsSkyDepth(sampleRawDepth))
                    {
                        continue;
                    }

                    float3 samplePositionWS = BurtReconstructDeferredPositionWS(sampleUV, sampleRawDepth);
                    float3 deltaWS = samplePositionWS - positionWS;
                    float distanceWS = length(deltaWS);
                    float3 sampleToCenterWS = -deltaWS / max(distanceWS, 0.0001f);
                    float sampleLinearDepth = LinearEyeDepth(sampleRawDepth);
                    float depthError = abs(probeLinearDepth - sampleLinearDepth);
                    float depthWeight = 1.0f - smoothstep(thickness, thickness + max(radius * 0.3f, 0.01f), depthError);
                    float distanceWeight = 1.0f - smoothstep(radius * 0.15f, radius * 1.35f, distanceWS);
                    float normalFacingWeight = saturate(dot(normalWS, deltaWS / max(distanceWS, 0.0001f)));
                    float3 sampleNormalWS = BurtXGISampleNormalWS(sampleUV);
                    float sampleNormalWeight = lerp(1.0f, saturate(dot(sampleNormalWS, sampleToCenterWS)), normalWeightAmount);
                    float weight = depthWeight * distanceWeight * normalFacingWeight * sampleNormalWeight;
                    if (weight <= 0.0001f)
                    {
                        continue;
                    }

                    BurtEncodedGBuffer sampleEncodedGBuffer = BurtSampleEncodedGBuffer(sampleUV);
                    BurtPBRMaterialData sampleMaterialData = BurtPreparePBRMaterialData(BurtDecodeGBuffer(sampleEncodedGBuffer));
                    float3 sampleRadiance = BurtXGIClampRadiance(tex2D(_BurtXGISourceColorTexture, sampleUV).rgb);
                    sampleRadiance *= BurtXGIComputeDiffuseSourceWeight(sampleMaterialData);
                    tracedRadiance += sampleRadiance * weight;
                    totalWeight += weight;
                }

                float hitRatio = saturate(totalWeight / max((float)stepCount * 0.35f, 1.0f));
                float3 screenIrradiance = totalWeight > 0.0001f ? tracedRadiance / totalWeight : 0.0f;
                float3 skyDiffuse = BurtSampleIndirectDiffuseIrradiance(normalWS) * _BurtXGIParams1.y * (1.0f - hitRatio);
                float3 diffuseOcclusion = BurtGTAOMultiBounce(materialData.occlusion, materialData.baseColor);
                float3 indirectDiffuse = materialData.diffuseColor * (screenIrradiance * hitRatio + skyDiffuse) * diffuseOcclusion * distanceFade;
                return float4(max(indirectDiffuse, 0.0f), hitRatio);
            }

            float4 FragBlur(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenUV;
                float4 center = tex2D(_BurtScreenSpaceGlobalIlluminationRawTexture, screenUV);
                if (_BurtXGIParams1.z < 0.5f)
                {
                    return center;
                }

                float centerRawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtXGIIsSkyDepth(centerRawDepth))
                {
                    return 0.0f;
                }

                float centerLinearDepth = LinearEyeDepth(centerRawDepth);
                float3 centerNormalWS = BurtXGISampleNormalWS(screenUV);
                float2 texel = _BurtXGISourceTexelSize.xy;
                float sharpness = lerp(0.04f, 0.35f, saturate(_BurtXGIParams1.w));
                float4 sum = center * 4.0f;
                float totalWeight = 4.0f;

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

                    float2 sampleUV = saturate(screenUV + direction * texel);
                    float sampleRawDepth = BurtSampleDeferredRawDepth(sampleUV);
                    if (BurtXGIIsSkyDepth(sampleRawDepth))
                    {
                        continue;
                    }

                    float depthWeight = exp(-abs(LinearEyeDepth(sampleRawDepth) - centerLinearDepth) * sharpness);
                    float normalWeight = pow(saturate(dot(centerNormalWS, BurtXGISampleNormalWS(sampleUV))), 8.0f);
                    float diagonalWeight = i < 4 ? 1.0f : 0.7f;
                    float weight = depthWeight * normalWeight * diagonalWeight;
                    sum += tex2D(_BurtScreenSpaceGlobalIlluminationRawTexture, sampleUV) * weight;
                    totalWeight += weight;
                }

                return sum / max(totalWeight, 0.0001f);
            }

            float4 FragTemporal(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenUV;
                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                float4 centerXGI;
                float3 minXGI;
                float3 maxXGI;
                float3 averageXGI;
                BurtXGISampleTemporalNeighborhood(screenUV, centerXGI, minXGI, maxXGI, averageXGI);

                if (_BurtXGITemporalParams.y < 0.5f || BurtXGIIsSkyDepth(rawDepth))
                {
                    return centerXGI;
                }

                float3 positionWS = BurtReconstructDeferredPositionWS(screenUV, rawDepth);
                float2 historyUV;
                float projectedRawDepth;
                if (!BurtXGIProjectHistoryUV(positionWS, historyUV, projectedRawDepth))
                {
                    return centerXGI;
                }

                float4 historyDepthNormal = tex2D(_BurtXGIHistoryDepthNormalTexture, historyUV);
                float historyRawDepth = historyDepthNormal.r;
                if (BurtXGIIsSkyDepth(historyRawDepth))
                {
                    return centerXGI;
                }

                float projectedLinearDepth = LinearEyeDepth(projectedRawDepth);
                float historyLinearDepth = LinearEyeDepth(historyRawDepth);
                float depthTolerance = max(projectedLinearDepth * max(_BurtXGITemporalParams.z, 0.0001f), 0.025f);
                float depthValidity = saturate(1.0f - abs(historyLinearDepth - projectedLinearDepth) / depthTolerance);
                if (depthValidity <= 0.0001f)
                {
                    return centerXGI;
                }

                float3 currentNormalWS = BurtXGISampleNormalWS(screenUV);
                float3 historyNormalWS = BurtDecodeNormalWSFromGBuffer(historyDepthNormal.gb);
                float normalThreshold = saturate(_BurtXGITemporalParams.w);
                float normalValidity = saturate((saturate(dot(currentNormalWS, historyNormalWS)) - normalThreshold) / max(1.0f - normalThreshold, 0.0001f));
                if (normalValidity <= 0.0001f)
                {
                    return centerXGI;
                }

                float3 historyXGI = max(tex2D(_BurtXGIHistoryTexture, historyUV).rgb, 0.0f);
                float3 localRange = max(maxXGI - minXGI, 0.001f);
                float clampPadScale = max(_BurtXGITemporalParams1.x, 0.0f);
                float3 clampedHistory = clamp(historyXGI, minXGI - localRange * clampPadScale, maxXGI + localRange * clampPadScale);
                float3 historyDelta = abs(historyXGI - centerXGI.rgb);
                float historyConsistency = saturate(1.0f - dot(historyDelta, float3(0.2126f, 0.7152f, 0.0722f)) / max(dot(localRange, float3(0.2126f, 0.7152f, 0.0722f)), 0.02f));
                float feedback = saturate(_BurtXGITemporalParams.x) * depthValidity * normalValidity * lerp(0.35f, 1.0f, historyConsistency);
                float3 currentXGI = lerp(centerXGI.rgb, averageXGI, 0.25f);
                float3 resolvedXGI = lerp(currentXGI, clampedHistory, feedback);
                float resolvedHitRatio = lerp(centerXGI.a, tex2D(_BurtXGIHistoryTexture, historyUV).a, feedback);
                return float4(max(resolvedXGI, 0.0f), saturate(resolvedHitRatio));
            }

            float4 FragCopyDepthNormal(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenUV;
                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                float2 encodedNormal = BurtEncodeNormalWSForGBuffer(BurtXGISampleNormalWS(screenUV));
                return float4(rawDepth, encodedNormal, 1.0f);
            }

            float4 FragCopyTemporalFinal(Varyings input) : SV_Target
            {
                return tex2D(_BurtXGITemporalFinalTexture, input.screenUV);
            }

            float4 FragComposite(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenUV;
                float4 cameraColor = tex2D(_BurtXGICameraColorCopyTexture, screenUV);
                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtXGIIsSkyDepth(rawDepth))
                {
                    return cameraColor;
                }

                float3 xgi = tex2D(_BurtScreenSpaceGlobalIlluminationTexture, screenUV).rgb;
                cameraColor.rgb += xgi * _BurtXGIParams1.x;
                return cameraColor;
            }

            float4 FragDebug(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenUV;
                float4 rawXGI = tex2D(_BurtScreenSpaceGlobalIlluminationRawTexture, screenUV);
                float4 finalXGI = tex2D(_BurtScreenSpaceGlobalIlluminationTexture, screenUV);
                float debugMode = round(_BurtXGIDebugMode);

                if (debugMode < 1.5f)
                {
                    return float4(max(rawXGI.rgb, 0.0f), 1.0f);
                }

                if (debugMode < 2.5f)
                {
                    return float4(max(finalXGI.rgb, 0.0f), 1.0f);
                }

                if (debugMode < 3.5f)
                {
                    float hitRatio = saturate(finalXGI.a);
                    return float4(1.0f - hitRatio, hitRatio, 0.0f, 1.0f);
                }

                float3 compositeContribution = max(finalXGI.rgb, 0.0f) * _BurtXGIParams1.xxx;
                if (debugMode < 4.5f)
                {
                    float4 cameraColor = tex2D(_BurtXGIDebugCameraColorTexture, screenUV);
                    cameraColor.rgb += compositeContribution;
                    return cameraColor;
                }

                return float4(compositeContribution, 1.0f);
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
    }
}
