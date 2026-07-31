Shader "Hidden/BurtRP/DebugTexture"
{
    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" }
        Pass
        {
            Name "Burt Debug Texture"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            UNITY_DECLARE_DEPTH_TEXTURE(_BurtDebugTexture);
            float4 _BurtDebugTextureParams; // x: scale, y: flipY, z: linearize camera depth

            struct Varyings
            {
                float4 PositionCS : SV_POSITION;
                float2 UV : TEXCOORD0;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings output;
                float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
                output.PositionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                output.UV = uv;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.UV;
                if (_BurtDebugTextureParams.y > 0.5)
                {
                    uv.y = 1.0 - uv.y;
                }

                float depth = SAMPLE_DEPTH_TEXTURE(_BurtDebugTexture, uv);
                if (_BurtDebugTextureParams.z > 0.5)
                {
                    depth = Linear01Depth(depth);
                }
                else
                {
                    #if defined(UNITY_REVERSED_Z)
                        depth = 1.0 - depth;
                    #endif
                }

                depth = saturate(depth * max(_BurtDebugTextureParams.x, 0.0001));
                return float4(depth.xxx, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Burt Debug Texture Mip"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragMip
            #include "UnityCG.cginc"

            sampler2D _BurtDebugTexture;
            float4 _BurtDebugTextureParams;

            struct Varyings
            {
                float4 PositionCS : SV_POSITION;
                float2 UV : TEXCOORD0;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings output;
                float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
                output.PositionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                output.UV = uv;
                return output;
            }

            float4 FragMip(Varyings input) : SV_Target
            {
                float2 uv = input.UV;
                if (_BurtDebugTextureParams.y > 0.5)
                {
                    uv.y = 1.0 - uv.y;
                }

                float depth = tex2Dlod(
                    _BurtDebugTexture,
                    float4(uv, 0.0, max(_BurtDebugTextureParams.w, 0.0))).r;
                depth = Linear01Depth(depth);
                depth = saturate(depth * max(_BurtDebugTextureParams.x, 0.0001));
                return float4(depth.xxx, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
