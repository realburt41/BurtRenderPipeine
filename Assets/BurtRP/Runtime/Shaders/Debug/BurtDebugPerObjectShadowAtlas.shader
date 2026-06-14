Shader "Hidden/BurtRP/DebugPerObjectShadowAtlas"
{
    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        Pass
        {
            Name "Burt Debug Per Object Shadow Atlas"

            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            UNITY_DECLARE_DEPTH_TEXTURE(_BurtPerObjectShadowAtlas);

            float _BurtPerObjectShadowDebugExposure;
            float _BurtPerObjectShadowDebugYFlip;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings output;
                float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
                output.positionCS = float4(uv * 2.0f - 1.0f, 0.0f, 1.0f);
                output.uv = uv;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 shadowUv = input.uv;
                if (_BurtPerObjectShadowDebugYFlip > 0.5f)
                {
                    shadowUv.y = 1.0f - shadowUv.y;
                }

                float rawDepth = SAMPLE_DEPTH_TEXTURE(_BurtPerObjectShadowAtlas, shadowUv);
                #if defined(UNITY_REVERSED_Z)
                    rawDepth = 1.0f - rawDepth;
                #endif

                float displayDepth = saturate(rawDepth * max(0.0001f, _BurtPerObjectShadowDebugExposure));
                return float4(displayDepth.xxx, 1.0f);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
