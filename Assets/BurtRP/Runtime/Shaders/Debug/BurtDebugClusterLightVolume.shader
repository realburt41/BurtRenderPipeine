Shader "Hidden/BurtRP/DebugClusterLightVolume"
{
    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "Burt Debug Cluster Light Volume Solid"
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"

            struct Attributes
            {
                float3 PositionOS : POSITION;
                float4 Color : COLOR;
            };

            struct Varyings
            {
                float4 PositionCS : SV_POSITION;
                float4 Color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.PositionCS = UnityWorldToClipPos(input.PositionOS);
                output.Color = input.Color;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                return input.Color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Burt Debug Cluster Light Volume Lines"
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"

            struct Attributes
            {
                float3 PositionOS : POSITION;
                float4 Color : COLOR;
            };

            struct Varyings
            {
                float4 PositionCS : SV_POSITION;
                float4 Color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.PositionCS = UnityWorldToClipPos(input.PositionOS);
                output.Color = input.Color;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                return input.Color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
