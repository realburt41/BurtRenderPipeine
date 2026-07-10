Shader "Hidden/BurtRP/Refraction"
{
    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        Pass
        {
            Name "Burt Refraction Apply Merge"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend One Zero

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"

            Texture2D _BurtRefractionDistortionTexture;
            Texture2D _BurtRefractionSceneColorMipChain;
            UNITY_DECLARE_DEPTH_TEXTURE(_BurtCameraDepthTexture);

            float _BurtRefractionSceneColorMaxMip;

            #define BURT_REFRACTION_XRENDER_MAX_MIP (4.92955f)
            #define BURT_REFRACTION_STANDARD_DEV_IN_PIXEL_FOR_MIP0 (2.0f)
            #define BURT_REFRACTION_METER_TO_CENTIMETER (100.0f)
            #define BURT_REFRACTION_BLUR_SCALE (350.0f)

            struct Attributes
            {
                uint VertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 PositionCS : SV_POSITION;
                float2 UV : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 uv = float2((input.VertexID << 1) & 2, input.VertexID & 2);
                output.PositionCS = float4(uv * 2.0f - 1.0f, 0.0f, 1.0f);
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0f - uv.y;
                #endif
                output.UV = uv;
                return output;
            }

            float BurtRefractionApproxSqrtVarianceFromRoughness(float roughness)
            {
                return saturate(roughness * roughness * 0.00173056f);
            }

            float BurtResolveRefractionMipLevel(float roughness, float surfaceDepth, float sceneDepth)
            {
                float thicknessM = max(sceneDepth - surfaceDepth, 0.0f);
                float standardDeviationCM =
                    BurtRefractionApproxSqrtVarianceFromRoughness(roughness) *
                    thicknessM *
                    BURT_REFRACTION_METER_TO_CENTIMETER *
                    BURT_REFRACTION_BLUR_SCALE;

                float tanHalfHFOV = rcp(max(abs(UNITY_MATRIX_P._m00), 1.0e-4f));
                float pixelRadiusCM = max(
                    BURT_REFRACTION_METER_TO_CENTIMETER *
                    max(sceneDepth, 1.0e-4f) *
                    tanHalfHFOV *
                    (2.0f / max(_ScreenParams.x, 1.0f)),
                    1.0e-4f);

                float pixelStandardDeviation = standardDeviationCM / pixelRadiusCM;
                float mipLevel = log2(max(pixelStandardDeviation / BURT_REFRACTION_STANDARD_DEV_IN_PIXEL_FOR_MIP0, 1.0e-4f));
                float maxMip = min(max(_BurtRefractionSceneColorMaxMip, 0.0f), BURT_REFRACTION_XRENDER_MAX_MIP);
                float depthFade = saturate(log2(max(surfaceDepth, 1.0e-4f)) * 0.3f);
                float minBoundary = lerp(maxMip * saturate(roughness), 0.0f, depthFade);
                return clamp(mipLevel, minBoundary, maxMip);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float4 distortion = SAMPLE_TEXTURE2D_LOD(_BurtRefractionDistortionTexture, sampler_PointClamp, input.UV, 0.0f);
                clip(distortion.a >= 65504.0f ? -1.0f : 1.0f);

                float2 uv = saturate(input.UV + distortion.xy * 0.25f);
                float roughness = saturate(distortion.z);
                float surfaceDepth = max(distortion.a, 1.0e-4f);
                float sceneDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_BurtCameraDepthTexture, uv));
                float mipLevel = BurtResolveRefractionMipLevel(roughness, surfaceDepth, sceneDepth);
                return SAMPLE_TEXTURE2D_LOD(_BurtRefractionSceneColorMipChain, sampler_TriLinearClamp, uv, mipLevel);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
