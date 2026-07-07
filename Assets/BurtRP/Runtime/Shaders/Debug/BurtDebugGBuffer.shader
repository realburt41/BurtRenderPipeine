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
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtDeferred.hlsl"

            float _BurtGBufferDebugMode;
            float _BurtGBufferDebugYFlip;

            struct Attributes
            {
                uint VertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 PositionCS : SV_POSITION;
                float2 ScreenUV : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.PositionCS = BurtGetFullScreenTriangleVertexPosition(input.VertexID);
                output.ScreenUV = BurtGetFullScreenTriangleTexCoord(input.VertexID);
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
                float2 screenUV = input.ScreenUV;
                if (_BurtGBufferDebugYFlip > 0.5f)
                {
                    screenUV.y = 1.0f - screenUV.y;
                }

                BurtEncodedGBuffer encodedGBuffer = BurtSampleEncodedGBuffer(screenUV);
                BurtGBufferData gbufferData = BurtDecodeDeferredGBuffer(encodedGBuffer, screenUV);
                int debugMode = BurtResolveGBufferDebugMode();

                if (debugMode == 1)
                {
                    return float4(saturate(encodedGBuffer.GBuffer0.rgb), 1.0f);
                }

                if (debugMode == 2)
                {
                    return float4(saturate(encodedGBuffer.GBuffer1.rgb), 1.0f);
                }

                if (debugMode == 3)
                {
                    return float4(saturate(encodedGBuffer.GBuffer2.rgb), 1.0f);
                }

                if (debugMode == 19)
                {
                    return float4(saturate(encodedGBuffer.GBuffer3.rgb), 1.0f);
                }

                if (debugMode == 23)
                {
                    return float4(saturate(encodedGBuffer.GBuffer4.rgb), 1.0f);
                }

                if (debugMode == 36)
                {
                    return float4(saturate(encodedGBuffer.GBuffer5.rgb), 1.0f);
                }

                if (debugMode == 37)
                {
                    float isGrass = BurtIsFoliageShadingModel(gbufferData.ShadingModelID) ? BurtGetFoliageIsGrass(gbufferData) : 0.0f;
                    return BurtDebugScalar(isGrass);
                }

                if (debugMode == 38)
                {
                    float isGrass = BurtIsFoliageShadingModel(gbufferData.ShadingModelID) ? BurtGetFoliageIsGrass(gbufferData) : 0.0f;
                    return BurtDebugScalar(BurtGetFoliageTransmissionWeight(gbufferData) * 0.1f * isGrass);
                }

                if (debugMode == 39)
                {
                    float isGrass = BurtIsFoliageShadingModel(gbufferData.ShadingModelID) ? BurtGetFoliageIsGrass(gbufferData) : 0.0f;
                    return BurtDebugScalar(BurtGetFoliageSpecularScale(gbufferData) * isGrass);
                }

                if (debugMode == 40)
                {
                    float isGrass = BurtIsFoliageShadingModel(gbufferData.ShadingModelID) ? BurtGetFoliageIsGrass(gbufferData) : 0.0f;
                    return BurtDebugScalar(BurtGetFoliageScreenSpaceShadowIntensity(gbufferData) * (1.0f / 3.0f) * isGrass);
                }

                if (debugMode == 4)
                {
                    return float4(max(gbufferData.BaseColor, float3(0.0f, 0.0f, 0.0f)), 1.0f);
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
                    return BurtDebugScalar(gbufferData.Smoothness);
                }

                if (debugMode == 8)
                {
                    return BurtDebugScalar(gbufferData.Occlusion);
                }

                if (debugMode == 9)
                {
                    return float4(saturate(gbufferData.Emission), 1.0f);
                }

                if (debugMode == 10)
                {
                    return BurtDebugScalar(gbufferData.Reflectance);
                }

                if (debugMode == 11)
                {
                    return BurtDebugScalar(BurtSampleDeferredRawDepth(screenUV));
                }

                if (debugMode == 12)
                {
                    return BurtDebugScalar(gbufferData.PerceptualRoughness);
                }

                if (debugMode == 13)
                {
                    BurtPBRMaterialData materialData = BurtPreparePBRMaterialData(gbufferData);
                    return float4(saturate(materialData.DiffuseColor), 1.0f);
                }

                if (debugMode == 14)
                {
                    return BurtDeferredDebugStencilShadingModelColor(gbufferData.ShadingModelID);
                }

                if (debugMode == 15)
                {
                    if (!BurtIsHairShadingModel(gbufferData.ShadingModelID))
                    {
                        return float4(0.0f, 0.0f, 0.0f, 1.0f);
                    }

                    return BurtDebugNormal(BurtGetHairStrandDirectionWS(gbufferData));
                }

                if (debugMode == 16)
                {
                    float isHair = BurtIsHairShadingModel(gbufferData.ShadingModelID) ? 1.0f : 0.0f;
                    return BurtDebugScalar(BurtGetHairScatter(gbufferData) * isHair);
                }

                if (debugMode == 17)
                {
                    float isHair = BurtIsHairShadingModel(gbufferData.ShadingModelID) ? 1.0f : 0.0f;
                    return BurtDebugScalar(BurtGetHairLongitudinalShiftScale(gbufferData) * isHair);
                }

                if (debugMode == 18)
                {
                    return BurtDebugScalar(BurtGetSubsurfaceStrength(gbufferData));
                }

                if (debugMode == 20)
                {
                    if (!BurtIsClearCoatShadingModel(gbufferData.ShadingModelID))
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
                    return BurtDebugScalar(gbufferData.Anisotropy * 0.5f + 0.5f);
                }

                if (debugMode == 25)
                {
                    return BurtDebugNormal(gbufferData.TangentWS);
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
                    if (!BurtIsFoliageShadingModel(gbufferData.ShadingModelID))
                    {
                        return float4(0.0f, 0.0f, 0.0f, 1.0f);
                    }

                    return float4(saturate(BurtGetFoliageTransmissionColor(gbufferData)), 1.0f);
                }

                if (debugMode == 29)
                {
                    float isFoliage = BurtIsFoliageShadingModel(gbufferData.ShadingModelID) ? 1.0f : 0.0f;
                    float foliageWeight = BurtGetFoliageTransmissionWeight(gbufferData);
                    float visibleFoliageWeight = gbufferData.FoliageIsGrass > 0.5f ? foliageWeight * 0.1f : foliageWeight;
                    return BurtDebugScalar(visibleFoliageWeight * isFoliage);
                }

                if (debugMode == 30)
                {
                    float isFoliage = BurtIsFoliageShadingModel(gbufferData.ShadingModelID) ? 1.0f : 0.0f;
                    return BurtDebugScalar(BurtGetFoliageThickness(gbufferData) * isFoliage);
                }

                if (debugMode == 31)
                {
                    float isFoliage = BurtIsFoliageShadingModel(gbufferData.ShadingModelID) ? 1.0f : 0.0f;
                    return BurtDebugScalar(BurtGetFoliageTransmissionNdotL(gbufferData) * isFoliage);
                }

                if (debugMode == 32)
                {
                    float isFoliage = BurtIsFoliageShadingModel(gbufferData.ShadingModelID) ? 1.0f : 0.0f;
                    return BurtDebugScalar(BurtGetFoliageSpecularScale(gbufferData) * isFoliage);
                }

                if (debugMode == 33)
                {
                    float isFoliage = BurtIsFoliageShadingModel(gbufferData.ShadingModelID) ? 1.0f : 0.0f;
                    return BurtDebugScalar(BurtGetFoliageScreenSpaceShadowIntensity(gbufferData) * (1.0f / 3.0f) * isFoliage);
                }

                if (debugMode == 34)
                {
                    return BurtDebugScalar((float)BurtLoadDeferredStencil(screenUV) / 255.0f);
                }

                if (debugMode == 35)
                {
                    return BurtDeferredDebugStencilShadingModelColor(BurtSampleDeferredShadingModelID(screenUV));
                }

                return float4(max(gbufferData.BaseColor, float3(0.0f, 0.0f, 0.0f)), 1.0f);
            }

            ENDHLSL
        }
    }

    Fallback Off
}
