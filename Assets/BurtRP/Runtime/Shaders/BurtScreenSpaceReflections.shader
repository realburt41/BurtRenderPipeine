Shader "Hidden/BurtRP/ScreenSpaceReflections"
{
    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        Pass
        {
            Name "Burt Screen Space Reflections"
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
            };

            BurtSSRHit BurtSSRCreateEmptyHit()
            {
                BurtSSRHit result;
                result.hit = 0.0;
                result.uv = 0.0;
                result.steps = 0.0;
                result.depthDelta = 0.0;
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

            float BurtSSRSelectHiZMip(float2 previousUV, float2 currentUV)
            {
                float2 deltaPixels = abs(currentUV - previousUV) * _BurtSSRSourceTexelSize.zw;
                float footprint = max(deltaPixels.x, deltaPixels.y);
                return clamp(floor(log2(max(footprint, 1.0))), 0.0, _BurtSSRParams1.y);
            }

            bool BurtSSRIsBehindSurface(float3 rayPositionWS, float2 rayUV, float hiZMip, out float depthDelta)
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
                return depthDelta >= 0.0 && depthDelta <= _BurtSSRParams0.y;
            }

            BurtSSRHit BurtSSRMarch(float3 originWS, float3 reflectionDirectionWS)
            {
                BurtSSRHit result = BurtSSRCreateEmptyHit();

                int maxSteps = min(max((int)_BurtSSRParams1.x, 1), 128);
                float maxDistance = max(_BurtSSRParams0.x, 0.01);
                float stepLength = maxDistance / max((float)maxSteps, 1.0);
                float2 previousUV = 0.0;
                bool hasPreviousUV = false;

                [loop]
                for (int stepIndex = 1; stepIndex <= 128; stepIndex++)
                {
                    if (stepIndex > maxSteps)
                    {
                        break;
                    }

                    float travel = stepLength * (float)stepIndex;
                    float3 rayPositionWS = originWS + reflectionDirectionWS * travel;
                    float2 rayUV;
                    float rayRawDepth;
                    float rayLinearDepth;

                    if (!BurtSSRProjectPosition(rayPositionWS, rayUV, rayRawDepth, rayLinearDepth))
                    {
                        break;
                    }

                    float hiZMip = hasPreviousUV ? BurtSSRSelectHiZMip(previousUV, rayUV) : 0.0;
                    hasPreviousUV = true;
                    previousUV = rayUV;

                    float depthDelta;
                    if (BurtSSRIsBehindSurface(rayPositionWS, rayUV, hiZMip, depthDelta))
                    {
                        result.hit = 1.0;
                        result.uv = rayUV;
                        result.steps = (float)stepIndex / max((float)maxSteps, 1.0);
                        result.depthDelta = depthDelta;
                        break;
                    }
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
                float3 sourceColor = tex2D(_BurtSSRSourceColorTexture, screenUV).rgb;
                float rawDepth = BurtSampleDeferredRawDepth(screenUV);

                if (BurtSSRIsSkyDepth(rawDepth))
                {
                    return float4(sourceColor, 1.0);
                }

                BurtGBufferData gbufferData = BurtDecodeGBuffer(BurtSampleEncodedGBuffer(screenUV));
                float3 positionWS = BurtReconstructDeferredPositionWS(screenUV, rawDepth);
                float3 viewDirectionWS = BurtSafeNormalize(_BurtDeferredCameraWorldPosition.xyz - positionWS);
                float3 normalWS = BurtSafeNormalize(gbufferData.normalWS);
                float3 reflectionDirectionWS = BurtSafeNormalize(reflect(-viewDirectionWS, normalWS));
                float nDotV = saturate(dot(normalWS, viewDirectionWS));

                BurtPBRMaterialData materialData = BurtPreparePBRMaterialData(gbufferData);
                float3 fresnel = F_Schlick(materialData.f0, materialData.f90, nDotV);
                float roughnessFade = saturate((_BurtSSRParams0.w - gbufferData.perceptualRoughness) / max(_BurtSSRParams0.w, 0.0001));
                float materialWeight = saturate(max(max(fresnel.r, fresnel.g), fresnel.b) * roughnessFade * _BurtSSRParams0.z);

                float thickness = max(_BurtSSRParams0.y, 0.0001);
                float3 originWS = positionWS + normalWS * min(thickness * 0.25, 0.05) + reflectionDirectionWS * thickness;
                BurtSSRHit hit = BurtSSRCreateEmptyHit();
                if (materialWeight > 0.0001)
                {
                    hit = BurtSSRMarch(originWS, reflectionDirectionWS);
                }

                float edgeFade = BurtSSREdgeFade(hit.uv);
                float hitWeight = saturate(hit.hit * materialWeight * edgeFade);
                float3 reflectionColor = tex2D(_BurtSSRSourceColorTexture, hit.uv).rgb;
                float3 compositeColor = lerp(sourceColor, reflectionColor, hitWeight);
                int debugMode = (int)_BurtSSRParams1.z;

                if (debugMode == 1)
                {
                    return float4(hit.hit, hit.hit, hit.hit, 1.0);
                }

                if (debugMode == 2)
                {
                    return float4(hit.uv, 0.0, 1.0);
                }

                if (debugMode == 3)
                {
                    return float4(hit.steps, hit.steps, hit.steps, 1.0);
                }

                if (debugMode == 4)
                {
                    return float4(reflectionColor * hit.hit, 1.0);
                }

                return float4(compositeColor, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Burt Copy Screen Space Reflections"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragCopy

            #include "UnityCG.cginc"
            #include "ShaderLibrary/BurtDeferred.hlsl"

            sampler2D _BurtScreenSpaceReflectionColorTexture;

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

            float4 FragCopy(Varyings input) : SV_Target
            {
                return tex2D(_BurtScreenSpaceReflectionColorTexture, input.screenUV);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
