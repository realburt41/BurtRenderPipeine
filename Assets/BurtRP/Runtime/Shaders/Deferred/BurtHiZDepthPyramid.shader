Shader "Hidden/BurtRP/HiZDepthPyramid"
{
    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        Pass
        {
            Name "Burt HiZ Copy Depth"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragCopyDepth

            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtDeferred.hlsl"

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

            float FragCopyDepth(Varyings input) : SV_Target
            {
                return SAMPLE_DEPTH_TEXTURE(_BurtCameraDepthTexture, input.screenUV);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Burt HiZ Reduce Furthest"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragReduceFurthest

            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtDeferred.hlsl"

            sampler2D _BurtHiZSourceTexture;
            float4 _BurtHiZSourceTexelSize;

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

            float SampleSourcePixel(float2 sourcePixel)
            {
                float2 maxPixel = max(_BurtHiZSourceTexelSize.zw - 0.5, 0.5);
                float2 clampedPixel = min(sourcePixel, maxPixel);
                return tex2D(_BurtHiZSourceTexture, clampedPixel * _BurtHiZSourceTexelSize.xy).r;
            }

            float ReduceFurthestRawDepth(float4 depth)
            {
                #if defined(UNITY_REVERSED_Z)
                    return min(min(depth.x, depth.y), min(depth.z, depth.w));
                #else
                    return max(max(depth.x, depth.y), max(depth.z, depth.w));
                #endif
            }

            float FragReduceFurthest(Varyings input) : SV_Target
            {
                float2 sourceBasePixel = input.positionCS.xy * 2.0 - 0.5;
                float4 sourceDepth;
                sourceDepth.x = SampleSourcePixel(sourceBasePixel);
                sourceDepth.y = SampleSourcePixel(sourceBasePixel + float2(1.0, 0.0));
                sourceDepth.z = SampleSourcePixel(sourceBasePixel + float2(0.0, 1.0));
                sourceDepth.w = SampleSourcePixel(sourceBasePixel + float2(1.0, 1.0));
                return ReduceFurthestRawDepth(sourceDepth);
            }
            ENDHLSL
        }
    }
}
