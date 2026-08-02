Shader "Hidden/BurtRP/LightShaftOcclusion"
{
    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        HLSLINCLUDE
        #include "UnityCG.cginc"

        UNITY_DECLARE_DEPTH_TEXTURE(_BurtCameraDepthTexture);
        sampler2D _BurtLightShaftInputTexture;
        sampler2D _BurtLightShaftSceneColorTexture;

        float4 _BurtLightShaftParameters;
        float4 _BurtLightShaftTextureSpaceOrigin;
        float4 _BurtLightShaftAspectRatioAndInvAspectRatio;
        float4 _BurtLightShaftBlurParameters;
        float4 _BurtLightShaftBlurUVMinMax;
        float4 _BurtLightShaftBloomTintAndThreshold;

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
            float2 uv = float2((input.VertexID << 1) & 2, input.VertexID & 2);
            output.PositionCS = float4(uv * 2.0f - 1.0f, 0.0f, 1.0f);
            #if UNITY_UV_STARTS_AT_TOP
                uv.y = 1.0f - uv.y;
            #endif
            output.ScreenUV = uv;
            return output;
        }

        float CalcEdgeMask(float2 uv)
        {
            float edgeMask = 1.0f
                - uv.x * (1.0f - uv.x)
                * uv.y * (1.0f - uv.y)
                * 8.0f;
            return edgeMask * edgeMask * edgeMask * edgeMask;
        }

        float OcclusionSetupFrag(Varyings input) : SV_Target
        {
            float deviceDepth = SAMPLE_DEPTH_TEXTURE(
                _BurtCameraDepthTexture,
                input.ScreenUV);
            float sceneDepth = LinearEyeDepth(deviceDepth);
            float occlusionMask = saturate(
                sceneDepth * _BurtLightShaftParameters.x);
            return max(occlusionMask, CalcEdgeMask(input.ScreenUV));
        }

        float4 RadiusBlurFrag(Varyings input) : SV_Target
        {
            static const int sampleCount = 6;
            float passScale = pow(
                0.4f * sampleCount,
                _BurtLightShaftBlurParameters.z);
            float2 blurVector =
                (_BurtLightShaftTextureSpaceOrigin.xy - input.ScreenUV)
                * min(
                    _BurtLightShaftBlurParameters.y * passScale,
                    1.0f);
            float4 blurredValue = 0.0f;

            [unroll]
            for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
            {
                float2 sampleUv = input.ScreenUV
                    + blurVector * sampleIndex / (float)sampleCount;
                sampleUv = clamp(
                    sampleUv,
                    _BurtLightShaftBlurUVMinMax.xy,
                    _BurtLightShaftBlurUVMinMax.zw);
                blurredValue += tex2Dlod(
                    _BurtLightShaftInputTexture,
                    float4(sampleUv, 0.0f, 0.0f));
            }

            return blurredValue / sampleCount;
        }

        float OcclusionFinalFrag(Varyings input) : SV_Target
        {
            float lightShaftOcclusion = tex2Dlod(
                _BurtLightShaftInputTexture,
                float4(input.ScreenUV, 0.0f, 0.0f)).r;
            float finalOcclusion = lerp(
                _BurtLightShaftParameters.y,
                1.0f,
                lightShaftOcclusion * lightShaftOcclusion);
            float blurOriginDistanceMask = saturate(
                length(
                    _BurtLightShaftTextureSpaceOrigin.xy
                    - input.ScreenUV
                    * _BurtLightShaftAspectRatioAndInvAspectRatio.zw)
                * 0.2f);
            return lerp(finalOcclusion, 1.0f, blurOriginDistanceMask);
        }

        float4 BloomSetupFrag(Varyings input) : SV_Target
        {
            float3 sceneColor = max(tex2Dlod(
                _BurtLightShaftSceneColorTexture,
                float4(input.ScreenUV, 0.0f, 0.0f)).rgb, 0.0f);
            float deviceDepth = SAMPLE_DEPTH_TEXTURE(
                _BurtCameraDepthTexture,
                input.ScreenUV);
            float sceneDepth = LinearEyeDepth(deviceDepth);
            float luminance = max(
                dot(sceneColor, float3(0.3f, 0.59f, 0.11f)),
                6.10352e-5f);
            float adjustedLuminance = clamp(
                luminance - _BurtLightShaftBloomTintAndThreshold.a,
                0.0f,
                _BurtLightShaftParameters.w);
            float3 bloomColor = _BurtLightShaftParameters.z
                * sceneColor / luminance
                * adjustedLuminance
                * 2.0f;
            float bloomDistanceMask = saturate(
                (sceneDepth - 0.5f / max(_BurtLightShaftParameters.x, 1.0e-6f))
                * _BurtLightShaftParameters.x);
            float blurOriginDistanceMask = 1.0f - saturate(
                length(_BurtLightShaftTextureSpaceOrigin.xy - input.ScreenUV)
                * 2.0f);
            float edgeMask = CalcEdgeMask(input.ScreenUV);
            float3 outputColor = bloomColor
                * max(_BurtLightShaftBloomTintAndThreshold.rgb, 0.0f)
                * bloomDistanceMask
                * (1.0f - edgeMask)
                * blurOriginDistanceMask
                * blurOriginDistanceMask;
            return float4(outputColor, 1.0f);
        }

        float4 BloomFinalFrag(Varyings input) : SV_Target
        {
            return tex2Dlod(
                _BurtLightShaftInputTexture,
                float4(input.ScreenUV, 0.0f, 0.0f));
        }
        ENDHLSL

        Pass
        {
            Name "Burt Light Shaft Occlusion Setup"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment OcclusionSetupFrag
            ENDHLSL
        }

        Pass
        {
            Name "Burt Light Shaft Radius Blur"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment RadiusBlurFrag
            ENDHLSL
        }

        Pass
        {
            Name "Burt Light Shaft Occlusion Final"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment OcclusionFinalFrag
            ENDHLSL
        }

        Pass
        {
            Name "Burt Light Shaft Bloom Setup"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment BloomSetupFrag
            ENDHLSL
        }

        Pass
        {
            Name "Burt Light Shaft Bloom Final"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend One One, Zero One

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment BloomFinalFrag
            ENDHLSL
        }
    }

    Fallback Off
}
