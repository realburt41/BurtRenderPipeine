Shader "Hidden/BurtRP/AvatarDecal/PositionMapPreview"
{
    SubShader
    {
        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _BurtAvatarDecalPreviewRange;

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float4 positionMapValue = tex2D(_MainTex, input.uv);
                if (positionMapValue.a <= 0.5f)
                {
                    return float4(0.0f, 0.0f, 0.0f, 1.0f);
                }

                float previewRange = max(_BurtAvatarDecalPreviewRange, 0.01f);
                float3 previewColor = saturate((positionMapValue.rgb / previewRange + 1.0f) * 0.5f);
                return float4(previewColor, 1.0f);
            }
            ENDCG
        }
    }
}
