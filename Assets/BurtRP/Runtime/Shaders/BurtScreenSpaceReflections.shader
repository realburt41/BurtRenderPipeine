Shader "Hidden/BurtRP/ScreenSpaceReflections"
{
    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        Pass
        {
            Name "Burt Screen Space Reflections Compatibility Stub"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = float4(input.vertexID == 1 ? 3.0 : -1.0, input.vertexID == 2 ? 3.0 : -1.0, 0.0, 1.0);
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                return 0.0;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
