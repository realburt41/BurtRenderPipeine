Shader "Hidden/BurtRP/ScreenSpaceShadow"
{
    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        HLSLINCLUDE
            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtDeferred.hlsl"

            float4x4 _BurtSSShadowViewProjectionMatrix;
            float4 _BurtSSShadowParams0; // x depth offset, y max distance, z thickness, w intensity.
            float4 _BurtSSShadowParams1; // x sample count, y fade distance, z fade radius, w frame salt.
            float4 _BurtSSShadowParams2; // x bilinear threshold ratio, y bilinear sampling offset enabled, z downsample factor, w unused.
            float4 _BurtSSShadowContrastParams; // x grass, y detail, z foliage, w character.
            float4 _BurtSSShadowTraceScreenSize;
            float4 _BurtMainLightDirection;

            #define BURT_SS_SHADOW_PIXEL_GRASS 2u
            #define BURT_SS_SHADOW_PIXEL_DETAIL 3u
            #define BURT_SS_SHADOW_PIXEL_FOLIAGE 4u
            #define BURT_SS_SHADOW_PIXEL_CHARACTER 5u

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

            bool BurtSSShadowIsSkyDepth(float rawDepth)
            {
                #if defined(UNITY_REVERSED_Z)
                    return rawDepth <= 0.00001f;
                #else
                    return rawDepth >= 0.99999f;
                #endif
            }

            float BurtSSShadowRand(float2 pixelPosition)
            {
                return frac(sin(dot(pixelPosition + _BurtSSShadowParams1.ww, float2(12.9898f, 78.233f))) * 43758.5453f);
            }

            BurtGBufferData BurtSSShadowSampleGBufferData(float2 screenUV)
            {
                return BurtDecodeGBuffer(BurtSampleEncodedGBuffer(screenUV));
            }

            uint BurtSSShadowClassifyMaterial(BurtGBufferData data)
            {
            #if BURT_ENABLE_FOLIAGE_SHADING
                if (BurtIsActiveFoliageShadingModel(data.shadingModelID))
                {
                    return BurtGetFoliageIsGrass(data) > 0.5f ? BURT_SS_SHADOW_PIXEL_GRASS : BURT_SS_SHADOW_PIXEL_FOLIAGE;
                }
            #endif

            #if BURT_ENABLE_FABRIC_SHADING
                if (BurtIsActiveFabricShadingModel(data.shadingModelID))
                {
                    return BURT_SS_SHADOW_PIXEL_DETAIL;
                }
            #endif

                return BURT_SS_SHADOW_PIXEL_CHARACTER;
            }

            float BurtSSShadowSelectContrast(uint materialClass)
            {
                if (materialClass == BURT_SS_SHADOW_PIXEL_GRASS)
                {
                    return max(_BurtSSShadowContrastParams.x, 0.0f);
                }

                if (materialClass == BURT_SS_SHADOW_PIXEL_DETAIL)
                {
                    return max(_BurtSSShadowContrastParams.y, 0.0f);
                }

                if (materialClass == BURT_SS_SHADOW_PIXEL_FOLIAGE)
                {
                    return max(_BurtSSShadowContrastParams.z, 0.0f);
                }

                return max(_BurtSSShadowContrastParams.w, 0.0f);
            }

            float BurtSSShadowResolveContrast(uint receiverMaterialClass, uint casterMaterialClass)
            {
                float contrast = BurtSSShadowSelectContrast(casterMaterialClass);
                if (casterMaterialClass == BURT_SS_SHADOW_PIXEL_CHARACTER && receiverMaterialClass != BURT_SS_SHADOW_PIXEL_CHARACTER)
                {
                    contrast = 0.0f;
                }

                return contrast;
            }

            float BurtSSShadowRawDepthFromClip(float clipZ)
            {
                #if defined(UNITY_REVERSED_Z)
                    return saturate(clipZ);
                #else
                    return saturate((clipZ - UNITY_NEAR_CLIP_VALUE) / max(1.0f - UNITY_NEAR_CLIP_VALUE, 0.00001f));
                #endif
            }

            float2 BurtSSShadowClipToScreenUV(float2 clipXY)
            {
                float2 uv = clipXY * 0.5f + 0.5f;
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0f - uv.y;
                #endif

                return uv;
            }

            bool BurtSSShadowProjectPositionWS(float3 positionWS, out float2 screenUV, out float rawDepth, out float linearDepth)
            {
                float4 clipPosition = mul(_BurtSSShadowViewProjectionMatrix, float4(positionWS, 1.0f));
                if (clipPosition.w <= 0.00001f)
                {
                    screenUV = 0.0f;
                    rawDepth = 0.0f;
                    linearDepth = 0.0f;
                    return false;
                }

                float3 ndc = clipPosition.xyz / clipPosition.w;
                screenUV = BurtSSShadowClipToScreenUV(ndc.xy);
                rawDepth = BurtSSShadowRawDepthFromClip(ndc.z);
                linearDepth = LinearEyeDepth(rawDepth);
                return !any(screenUV < 0.0f) && !any(screenUV > 1.0f) && rawDepth >= 0.0f && rawDepth <= 1.0f;
            }

            float BurtSSShadowApplyDistanceFade(float shadow, float centerLinearDepth)
            {
                float fadeRadius = max(_BurtSSShadowParams1.z, 0.0001f);
                float fadeDistance = max(_BurtSSShadowParams1.y, fadeRadius);
                float fadeStart = max(fadeDistance - fadeRadius, 0.0f);
                float distanceFade = saturate((centerLinearDepth - fadeStart) / fadeRadius);
                return lerp(shadow, 1.0f, distanceFade);
            }

            float BurtSSShadowSampleRawDepthWithEdgeFallback(float2 sampleUV, float2 rayDirectionUV)
            {
                float rawDepth = BurtSampleDeferredRawDepth(sampleUV);
                if (_BurtSSShadowParams2.y < 0.5f || BurtSSShadowIsSkyDepth(rawDepth))
                {
                    return rawDepth;
                }

                float2 sourceSize = max(_BurtDeferredScreenSize.xy, float2(1.0f, 1.0f));
                float2 sourceTexel = _BurtDeferredScreenSize.zw;
                float2 rayDirectionPixels = rayDirectionUV * sourceSize;
                bool xAxisMajor = abs(rayDirectionPixels.x) > abs(rayDirectionPixels.y);
                float2 minorAxisTexel = xAxisMajor ? float2(0.0f, sourceTexel.y) : float2(sourceTexel.x, 0.0f);
                float minorAxisPosition = xAxisMajor ? sampleUV.y * sourceSize.y : sampleUV.x * sourceSize.x;
                float bilinear = frac(minorAxisPosition) - 0.5f;
                float offsetSign = bilinear >= 0.0f ? 1.0f : -1.0f;
                float2 neighborUV = saturate(sampleUV + minorAxisTexel * offsetSign);
                float neighborRawDepth = BurtSampleDeferredRawDepth(neighborUV);
                float threshold = max(rawDepth * max(_BurtSSShadowParams2.x, 0.0f), 0.000001f);
                if (BurtSSShadowIsSkyDepth(neighborRawDepth) || abs(rawDepth - neighborRawDepth) > threshold)
                {
                    return rawDepth;
                }

                return lerp(rawDepth, neighborRawDepth, abs(bilinear));
            }

            float BurtSSShadowTrace(float2 screenUV)
            {
                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtSSShadowIsSkyDepth(rawDepth))
                {
                    return 1.0f;
                }

                float3 positionWS = BurtReconstructDeferredPositionWS(screenUV, rawDepth);
                float centerLinearDepth = LinearEyeDepth(rawDepth);
                BurtGBufferData receiverData = BurtSSShadowSampleGBufferData(screenUV);
                uint receiverMaterialClass = BurtSSShadowClassifyMaterial(receiverData);
                float3 normalWS = receiverData.normalWS;
                float3 lightDirectionWS = BurtSafeNormalize(_BurtMainLightDirection.xyz);
                float receiverWeight = saturate(dot(normalWS, lightDirectionWS) * 4.0f);
                if (receiverWeight <= 0.0001f)
                {
                    return 1.0f;
                }

                int sampleCount = clamp((int)round(_BurtSSShadowParams1.x), 1, 64);
                float maxDistance = max(_BurtSSShadowParams0.y, 0.001f);
                float depthBias = max(_BurtSSShadowParams0.x, 0.0f) * max(centerLinearDepth, 1.0f) * 0.002f + 0.001f;
                float thicknessScale = max(_BurtSSShadowParams0.z, 0.0f);
                float jitter = BurtSSShadowRand(screenUV * _BurtSSShadowTraceScreenSize.xy);
                float occlusion = 0.0f;

                [loop]
                for (int sampleIndex = 1; sampleIndex <= 64; ++sampleIndex)
                {
                    if (sampleIndex > sampleCount)
                    {
                        break;
                    }

                    float stepFraction = ((float)sampleIndex - 0.5f + jitter) / (float)sampleCount;
                    float3 rayPositionWS = positionWS + lightDirectionWS * (stepFraction * maxDistance);
                    float2 sampleUV;
                    float rayRawDepth;
                    float rayLinearDepth;
                    if (!BurtSSShadowProjectPositionWS(rayPositionWS, sampleUV, rayRawDepth, rayLinearDepth))
                    {
                        continue;
                    }

                    float2 rayDirectionUV = sampleUV - screenUV;
                    float sceneRawDepth = BurtSSShadowSampleRawDepthWithEdgeFallback(sampleUV, rayDirectionUV);
                    if (BurtSSShadowIsSkyDepth(sceneRawDepth))
                    {
                        continue;
                    }

                    float sceneLinearDepth = LinearEyeDepth(sceneRawDepth);
                    float depthDelta = rayLinearDepth - sceneLinearDepth - depthBias;
                    float finiteThickness = max(0.01f, max(rayLinearDepth, 1.0f) * lerp(0.012f, 0.08f, saturate(thicknessScale * 0.1f)));
                    float frontHit = smoothstep(0.0f, finiteThickness * 0.25f, depthDelta);
                    float thicknessHit = 1.0f - smoothstep(finiteThickness, finiteThickness * 2.0f, depthDelta);
                    float rangeWeight = saturate(1.0f - stepFraction);
                    BurtGBufferData casterData = BurtSSShadowSampleGBufferData(sampleUV);
                    uint casterMaterialClass = BurtSSShadowClassifyMaterial(casterData);
                    float materialContrast = BurtSSShadowResolveContrast(receiverMaterialClass, casterMaterialClass);
                    occlusion = max(occlusion, frontHit * thicknessHit * rangeWeight * materialContrast);
                }

                float shadow = saturate(1.0f - occlusion * receiverWeight);
                shadow = lerp(1.0f, shadow, saturate(_BurtSSShadowParams0.w));
                return BurtSSShadowApplyDistanceFade(shadow, centerLinearDepth);
            }

            float4 FragTrace(Varyings input) : SV_Target
            {
                float shadow = BurtSSShadowTrace(input.screenUV);
                return float4(shadow, shadow, shadow, 1.0f);
            }
        ENDHLSL

        Pass
        {
            Name "Burt Screen Space Shadow Trace"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragTrace
            ENDHLSL
        }
    }

    Fallback Off
}
