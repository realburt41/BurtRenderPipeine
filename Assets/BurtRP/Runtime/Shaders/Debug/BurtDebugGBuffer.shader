Shader "Hidden/BurtRP/DebugGBuffer"
{
    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        Pass
        {
            Name "Burt Debug GBuffer"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtDeferred.hlsl"

            float _BurtGBufferDebugMode;
            float _BurtGBufferDebugYFlip;

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

            int BurtResolveGBufferDebugMode()
            {
                return (int)round(_BurtGBufferDebugMode);
            }

            float4 BurtDebugScalar(float value)
            {
                return float4(saturate(value).xxx, 1.0f);
            }

            float4 BurtDebugNormal(float3 normalWS)
            {
                return float4(saturate(normalWS * 0.5f + 0.5f), 1.0f);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenUV;
                if (_BurtGBufferDebugYFlip > 0.5f)
                {
                    screenUV.y = 1.0f - screenUV.y;
                }

                BurtEncodedGBuffer encodedGBuffer = BurtSampleEncodedGBuffer(screenUV);
                BurtGBufferData gbufferData = BurtDecodeGBuffer(encodedGBuffer);
                int debugMode = BurtResolveGBufferDebugMode();

                if (debugMode == 1)
                {
                    return float4(saturate(encodedGBuffer.gbuffer0.rgb), 1.0f);
                }

                if (debugMode == 2)
                {
                    return float4(saturate(encodedGBuffer.gbuffer1.rgb), 1.0f);
                }

                if (debugMode == 3)
                {
                    return float4(saturate(encodedGBuffer.gbuffer2.rgb), 1.0f);
                }

                if (debugMode == 19)
                {
                    return float4(saturate(encodedGBuffer.gbuffer3.rgb), 1.0f);
                }

                if (debugMode == 23)
                {
                    return float4(saturate(encodedGBuffer.gbuffer4.rgb), 1.0f);
                }

                if (debugMode == 4)
                {
                    return float4(max(gbufferData.baseColor, float3(0.0f, 0.0f, 0.0f)), 1.0f);
                }

                if (debugMode == 5)
                {
                    return BurtDebugNormal(BurtGetGBufferDirectionWS(gbufferData));
                }

                if (debugMode == 6)
                {
                    return BurtDebugScalar(BurtGetGBufferMaterialChannel(gbufferData));
                }

                if (debugMode == 7)
                {
                    return BurtDebugScalar(gbufferData.smoothness);
                }

                if (debugMode == 8)
                {
                    return BurtDebugScalar(gbufferData.occlusion);
                }

                if (debugMode == 9)
                {
                    return float4(saturate(gbufferData.emission), 1.0f);
                }

                if (debugMode == 10)
                {
                    return BurtDebugScalar(gbufferData.reflectance);
                }

                if (debugMode == 11)
                {
                    return BurtDebugScalar(BurtSampleDeferredRawDepth(screenUV));
                }

                if (debugMode == 12)
                {
                    return BurtDebugScalar(gbufferData.perceptualRoughness);
                }

                if (debugMode == 13)
                {
                    BurtPBRMaterialData materialData = BurtPreparePBRMaterialData(gbufferData);
                    return float4(saturate(materialData.diffuseColor), 1.0f);
                }

                if (debugMode == 14)
                {
                    float isHair = BurtIsHairShadingModel(gbufferData.shadingModelID) ? 1.0f : 0.0f;
                    float isClearCoat = BurtIsClearCoatShadingModel(gbufferData.shadingModelID) ? 1.0f : 0.0f;
                    float isSubsurface = BurtIsSubsurfaceShadingModel(gbufferData.shadingModelID) ? 1.0f : 0.0f;
                    float isFoliage = BurtIsFoliageShadingModel(gbufferData.shadingModelID) ? 1.0f : 0.0f;
                    return float4(
                        0.6f * isHair + 0.1f * isSubsurface + 0.18f * isFoliage,
                        0.1f * isHair + 0.45f * isClearCoat + 0.55f * isSubsurface + 0.85f * isFoliage,
                        0.5f * isHair + 0.7f * isClearCoat + 0.15f * isSubsurface + 0.18f * isFoliage,
                        1.0f);
                }

                if (debugMode == 15)
                {
                    if (!BurtIsHairShadingModel(gbufferData.shadingModelID))
                    {
                        return float4(0.0f, 0.0f, 0.0f, 1.0f);
                    }

                    return BurtDebugNormal(BurtGetHairStrandDirectionWS(gbufferData));
                }

                if (debugMode == 16)
                {
                    float isHair = BurtIsHairShadingModel(gbufferData.shadingModelID) ? 1.0f : 0.0f;
                    return BurtDebugScalar(BurtGetHairScatter(gbufferData) * isHair);
                }

                if (debugMode == 17)
                {
                    float isHair = BurtIsHairShadingModel(gbufferData.shadingModelID) ? 1.0f : 0.0f;
                    return BurtDebugScalar(BurtGetHairLongitudinalShiftScale(gbufferData) * isHair);
                }

                if (debugMode == 18)
                {
                    return BurtDebugScalar(BurtGetSubsurfaceStrength(gbufferData));
                }

                if (debugMode == 20)
                {
                    if (!BurtIsClearCoatShadingModel(gbufferData.shadingModelID))
                    {
                        return float4(0.0f, 0.0f, 0.0f, 1.0f);
                    }

                    return BurtDebugNormal(BurtGetClearCoatNormalWS(gbufferData));
                }

                if (debugMode == 21)
                {
                    return BurtDebugScalar(BurtGetClearCoatMask(gbufferData));
                }

                if (debugMode == 22)
                {
                    return BurtDebugScalar(BurtGetClearCoatRoughness(gbufferData));
                }

                if (debugMode == 24)
                {
                    return BurtDebugScalar(gbufferData.anisotropy * 0.5f + 0.5f);
                }

                if (debugMode == 25)
                {
                    return BurtDebugNormal(gbufferData.tangentWS);
                }

                if (debugMode == 26)
                {
                    return BurtDebugScalar(BurtGetSubsurfaceThickness(gbufferData));
                }

                if (debugMode == 27)
                {
                    float profileIndex = BurtGetSubsurfaceProfileIndex(gbufferData);
                    float visibleProfileIndex = saturate(profileIndex / max(BURT_SUBSURFACE_PROFILE_COUNT - 1.0f, 1.0f));
                    return BurtDebugScalar(visibleProfileIndex);
                }

                if (debugMode == 28)
                {
                    if (!BurtIsFoliageShadingModel(gbufferData.shadingModelID))
                    {
                        return float4(0.0f, 0.0f, 0.0f, 1.0f);
                    }

                    return float4(saturate(BurtGetFoliageTransmissionColor(gbufferData)), 1.0f);
                }

                if (debugMode == 29)
                {
                    float isFoliage = BurtIsFoliageShadingModel(gbufferData.shadingModelID) ? 1.0f : 0.0f;
                    return BurtDebugScalar(BurtGetFoliageTransmissionWeight(gbufferData) * isFoliage * 0.1f);
                }

                if (debugMode == 30)
                {
                    float isFoliage = BurtIsFoliageShadingModel(gbufferData.shadingModelID) ? 1.0f : 0.0f;
                    return BurtDebugScalar(BurtGetFoliageThickness(gbufferData) * isFoliage);
                }

                if (debugMode == 31)
                {
                    float isFoliage = BurtIsFoliageShadingModel(gbufferData.shadingModelID) ? 1.0f : 0.0f;
                    return BurtDebugScalar(BurtGetFoliageTransmissionNdotL(gbufferData) * isFoliage);
                }

                if (debugMode == 32)
                {
                    float isFoliage = BurtIsFoliageShadingModel(gbufferData.shadingModelID) ? 1.0f : 0.0f;
                    return BurtDebugScalar(BurtGetFoliageSpecularScale(gbufferData) * isFoliage);
                }

                if (debugMode == 33)
                {
                    float isFoliage = BurtIsFoliageShadingModel(gbufferData.shadingModelID) ? 1.0f : 0.0f;
                    return BurtDebugScalar(BurtGetFoliageScreenSpaceShadowIntensity(gbufferData) * (1.0f / 3.0f) * isFoliage);
                }

                return float4(max(gbufferData.baseColor, float3(0.0f, 0.0f, 0.0f)), 1.0f);
            }

            ENDHLSL
        }
    }

    Fallback Off
}
