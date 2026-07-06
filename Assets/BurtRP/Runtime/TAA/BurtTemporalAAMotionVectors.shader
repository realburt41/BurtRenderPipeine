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
            Stencil
            {
                Ref 8
                ReadMask 8
                WriteMask 8
                Comp Always
                Pass Replace
            }

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
                float forceNoMotion = step(unity_MotionVectorsParams.y, 0.5);
                float cameraMotion = step(unity_MotionVectorsParams.w, 0.5);
                float allowObjectMotion = (1.0 - forceNoMotion) * (1.0 - cameraMotion);
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
                clip(input.sourceConfidence - 0.5);

                float valid = step(1e-5, input.currentClipNoJitter.w);
                float2 currentUv = BurtTaaClipToUv(input.currentClipNoJitter);
                float2 previousUv = BurtTaaClipToUv(input.previousClipNoJitter);
                valid *= step(0.0, currentUv.x) * step(currentUv.x, 1.0) * step(0.0, currentUv.y) * step(currentUv.y, 1.0);
                float previousAvailable = step(1e-5, input.previousClipNoJitter.w);
                previousAvailable *= step(0.0, previousUv.x) * step(previousUv.x, 1.0) * step(0.0, previousUv.y) * step(previousUv.y, 1.0);
                if (valid < 0.5)
                {
                    discard;
                }

                clip(previousAvailable - 0.5);

                float2 velocity = currentUv - previousUv;
                float2 velocityPixels = abs(velocity * _BurtTAATexelSize.zw);
                velocity *= step(float2(0.02, 0.02), velocityPixels);
                clip(max(abs(velocity.x), abs(velocity.y)) - 1e-8);
                return float4(velocity, 1.0, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
