Shader "Hidden/BurtRP/AvatarDecal/GeneratePositionMap"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #pragma shader_feature_local _ BURT_PRESKIN_POSITION_PACKED

            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtPreSkinPosition.hlsl"

            struct Attributes
            {
                float2 UV0 : TEXCOORD0;
            #if BURT_PRESKIN_POSITION_UV3_PACKED
                uint2 PreSkinPositionUV3 : TEXCOORD3;
            #else
                float3 PositionOS : POSITION;
            #endif
            };

            struct Varyings
            {
                float4 PositionCS : SV_POSITION;
                float3 PreSkinPositionOS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 uv = input.UV0;
                uv.y = 1.0f - uv.y;
                output.PositionCS = float4(uv * 2.0f - 1.0f, 0.0f, 1.0f);
            #if BURT_PRESKIN_POSITION_UV3_PACKED
                output.PreSkinPositionOS = BurtDecodePreSkinPositionOS(input.PreSkinPositionUV3);
            #else
                // XRender's non-compressed AvatarDecal path writes the mesh POSITION directly.
                // UV3 is only the pre-skin position source for the compressed mesh path above.
                output.PreSkinPositionOS = input.PositionOS;
            #endif
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                return float4(input.PreSkinPositionOS, 1.0f);
            }
            ENDHLSL
        }
    }
}
