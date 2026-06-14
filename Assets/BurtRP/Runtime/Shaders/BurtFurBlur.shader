Shader "Hidden/BurtRP/FurBlur"
{
    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        Pass
        {
            Name "Burt Fur Blur"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragBlur
            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"

            Texture2D _BurtCameraColorTexture;
            Texture2D _BurtFurBlurPropertyTexture;
            float4 _BurtFurBlurScreenSize;

            static const float BURT_TWO_PI = 6.28318530717958647692;
            static const float BURT_FUR_DEPTH_THRESHOLD = 1e-3;
            static const int BURT_FUR_BLUR_SAMPLE_COUNT = 3;
            static const float BURT_FUR_BLUR_RADIUS_CM = 2.0;
            static const float BURT_METER_TO_CENTIMETER = 100.0;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0 - uv.y;
                #endif
                output.uv = uv;
                return output;
            }

            float2 BurtDecodeFurDir(float angle)
            {
                angle *= BURT_TWO_PI;
                float s;
                float c;
                sincos(angle, s, c);
                return float2(c, s);
            }

            float4 FragBlur(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float4 property = BURT_SAMPLE_TEXTURE2D_LOD_POINT_CLAMP(_BurtFurBlurPropertyTexture, uv, 0.0);
                float4 centerColor = BURT_SAMPLE_TEXTURE2D_LOD_CLAMP(_BurtCameraColorTexture, uv, 0.0);
                float theta = property.r;
                if (theta <= 1e-5)
                {
                    return float4(centerColor.rgb, 0.0);
                }

                float centerDepth = property.g;
                float centerLinearDepth = LinearEyeDepth(centerDepth);
                float pixelPerCm = _BurtFurBlurScreenSize.x / max(UNITY_MATRIX_P._m00 * 2.0 * centerLinearDepth * BURT_METER_TO_CENTIMETER, 1e-4);
                float stepCm = BURT_FUR_BLUR_RADIUS_CM / BURT_FUR_BLUR_SAMPLE_COUNT;
                float scale = min(2.0, stepCm * pixelPerCm);
                float2 furStep = BurtDecodeFurDir(theta) * _BurtFurBlurScreenSize.zw * scale;
                float4 blur = float4(centerColor.rgb, 1.0);
                bool occludedPos = false;
                bool occludedNeg = false;

                for (int i = 1; i <= BURT_FUR_BLUR_SAMPLE_COUNT; i++)
                {
                    float2 positiveUv = saturate(uv + furStep * i);
                    float2 negativeUv = saturate(uv - furStep * i);
                    if (!occludedPos)
                    {
                        float4 sampleProperty = BURT_SAMPLE_TEXTURE2D_LOD_POINT_CLAMP(_BurtFurBlurPropertyTexture, positiveUv, 0.0);
                        occludedPos = sampleProperty.g > centerDepth - BURT_FUR_DEPTH_THRESHOLD;
                        blur += float4(BURT_SAMPLE_TEXTURE2D_LOD_CLAMP(_BurtCameraColorTexture, positiveUv, 0.0).rgb, 1.0);
                    }

                    if (!occludedNeg)
                    {
                        float4 sampleProperty = BURT_SAMPLE_TEXTURE2D_LOD_POINT_CLAMP(_BurtFurBlurPropertyTexture, negativeUv, 0.0);
                        occludedNeg = sampleProperty.g > centerDepth - BURT_FUR_DEPTH_THRESHOLD;
                        blur += float4(BURT_SAMPLE_TEXTURE2D_LOD_CLAMP(_BurtCameraColorTexture, negativeUv, 0.0).rgb, 1.0);
                    }
                }

                return float4(blur.rgb / max(blur.a, 1e-4), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Burt Fur Blur Composite"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragComposite
            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"

            Texture2D _BurtFurBlurColorTexture;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0 - uv.y;
                #endif
                output.uv = uv;
                return output;
            }

            float4 FragComposite(Varyings input) : SV_Target
            {
                return BURT_SAMPLE_TEXTURE2D_LOD_CLAMP(_BurtFurBlurColorTexture, input.uv, 0.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
