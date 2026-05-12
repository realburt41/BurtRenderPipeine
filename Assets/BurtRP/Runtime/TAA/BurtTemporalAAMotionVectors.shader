Shader "Hidden/BurtRP/TemporalAAMotionVectors"
{
    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        Pass
        {
            Name "Burt Temporal AA Object Motion Vectors"
            Tags { "LightMode" = "BurtMotionVectors" }
            Cull Back
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            float4x4 _BurtTAACurrentViewProjection;
            float4x4 _BurtTAACurrentNonJitteredViewProjection;
            float4x4 _BurtTAAPreviousNonJitteredViewProjection;
            float4x4 unity_MatrixPreviousM;
            float4 unity_MotionVectorsParams;
            float4 _BurtTAATexelSize;

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 currentClipNoJitter : TEXCOORD0;
                float4 previousClipNoJitter : TEXCOORD1;
                float sourceConfidence : TEXCOORD2;
            };

            float2 BurtTaaClipToUv(float4 clipPosition)
            {
                float2 ndc = clipPosition.xy / max(abs(clipPosition.w), 1e-6);
                float2 uv = ndc * 0.5 + 0.5;
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0 - uv.y;
                #endif
                return uv;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float4 currentWorld = mul(unity_ObjectToWorld, float4(input.positionOS, 1.0));
                float4 previousObjectWorld = mul(unity_MatrixPreviousM, float4(input.positionOS, 1.0));
                float allowObjectMotion = step(0.5, unity_MotionVectorsParams.y);
                float3 objectDelta = previousObjectWorld.xyz - currentWorld.xyz;
                float objectMoved = step(1e-8, dot(objectDelta, objectDelta)) * allowObjectMotion;
                float4 previousWorld = lerp(currentWorld, previousObjectWorld, objectMoved);
                output.positionCS = mul(_BurtTAACurrentViewProjection, currentWorld);
                #if defined(UNITY_REVERSED_Z)
                    output.positionCS.z -= unity_MotionVectorsParams.z * output.positionCS.w;
                #else
                    output.positionCS.z += unity_MotionVectorsParams.z * output.positionCS.w;
                #endif
                output.currentClipNoJitter = mul(_BurtTAACurrentNonJitteredViewProjection, currentWorld);
                output.previousClipNoJitter = mul(_BurtTAAPreviousNonJitteredViewProjection, previousWorld);
                output.sourceConfidence = objectMoved;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float valid = step(1e-5, input.currentClipNoJitter.w) * step(1e-5, input.previousClipNoJitter.w);
                float2 currentUv = BurtTaaClipToUv(input.currentClipNoJitter);
                float2 previousUv = BurtTaaClipToUv(input.previousClipNoJitter);
                float2 velocity = previousUv - currentUv;
                return float4(velocity, valid, saturate(input.sourceConfidence));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
