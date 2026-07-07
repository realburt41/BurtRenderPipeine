Shader "Hidden/BurtRP/DebugHiZDepth"
{
    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        Pass
        {
            Name "Burt Debug HiZ Depth"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtDeferred.hlsl"

            sampler2D _BurtHiZDepthTexture;
            float _BurtHiZDebugMip;
            float _BurtHiZDebugScale;
            float _BurtHiZDebugMaxMip;

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

            float4 Frag(Varyings input) : SV_Target
            {
                float selectedMip = clamp(_BurtHiZDebugMip, 0.0, max(_BurtHiZDebugMaxMip, 0.0));
                float rawDepth = tex2Dlod(_BurtHiZDepthTexture, float4(input.ScreenUV, 0.0, selectedMip)).r;
                float linearDepth = Linear01Depth(rawDepth);
                float visualDepth = saturate(linearDepth * max(_BurtHiZDebugScale, 0.0001));
                return float4(visualDepth.xxx, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
