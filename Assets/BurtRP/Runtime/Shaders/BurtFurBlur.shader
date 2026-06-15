Shader "Hidden/BurtRP/FurBlur"
{
    HLSLINCLUDE
    #include "UnityCG.cginc"
    #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"

    Texture2D _BurtCameraColorTexture;
    Texture2D _BurtFurBlurPropertyTexture;
    Texture2D _BurtFurBlurColorTexture;
    Texture2D _BurtFurBlurTemporalTexture;
    Texture2D _BurtFurBlurHistoryTexture;
    float4 _BurtFurBlurScreenSize;
    float4 _BurtFurBlurHistoryParams;
    int _BurtFurBlurDebugMode;

    static const float BURT_TWO_PI = 6.28318530717958647692;
    static const int BURT_FUR_BLUR_SAMPLE_COUNT = 3;
    static const float BURT_FUR_BLUR_RADIUS_CM = 2.0;
    static const float BURT_METER_TO_CENTIMETER = 100.0;
    static const float BURT_FUR_VALID_THETA_EPSILON = 1e-5;
    static const float BURT_FUR_DEPTH_THRESHOLD_EYE = 0.02;

    struct Attributes
    {
        uint vertexID : SV_VertexID;
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    Varyings Vert(Attributes input)
    {
        Varyings output;
        float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
        output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
        #if UNITY_UV_STARTS_AT_TOP
            uv.y = 1.0 - uv.y;
        #endif
        output.uv = uv;
        return output;
    }

    float2 BurtDecodeFurDir(float angle)
    {
        angle *= BURT_TWO_PI;
        float s;
        float c;
        sincos(angle, s, c);
        return float2(c, s);
    }

    bool BurtIsValidFurProperty(float4 property)
    {
        return property.r > BURT_FUR_VALID_THETA_EPSILON;
    }

    bool BurtFurDepthIsNearer(float sampleDepth, float referenceDepth)
    {
        #if UNITY_REVERSED_Z
            return sampleDepth > referenceDepth;
        #else
            return sampleDepth < referenceDepth;
        #endif
    }

    float BurtFurDepth01(float deviceDepth)
    {
        #if UNITY_REVERSED_Z
            return saturate(1.0 - deviceDepth);
        #else
            return saturate(deviceDepth);
        #endif
    }

    bool BurtFurDepthAllowsBlur(float sampleDepth, float centerDepth)
    {
        float sampleEyeDepth = LinearEyeDepth(sampleDepth);
        float centerEyeDepth = LinearEyeDepth(centerDepth);
        return abs(sampleEyeDepth - centerEyeDepth) <= BURT_FUR_DEPTH_THRESHOLD_EYE;
    }

    float4 BurtSampleFurProperty(float2 uv)
    {
        return BURT_SAMPLE_TEXTURE2D_LOD_POINT_CLAMP(_BurtFurBlurPropertyTexture, uv, 0.0);
    }

    float4 FragBlur(Varyings input) : SV_Target
    {
        float2 uv = input.uv;
        float4 property = BurtSampleFurProperty(uv);
        float4 centerColor = BURT_SAMPLE_TEXTURE2D_LOD_CLAMP(_BurtCameraColorTexture, uv, 0.0);
        if (!BurtIsValidFurProperty(property))
        {
            return float4(centerColor.rgb, 0.0);
        }

        float centerDepth = property.g;
        float centerLinearDepth = max(LinearEyeDepth(centerDepth), 1e-4);
        float pixelPerCm = _BurtFurBlurScreenSize.x / max(UNITY_MATRIX_P._m00 * 2.0 * centerLinearDepth * BURT_METER_TO_CENTIMETER, 1e-4);
        float stepCm = BURT_FUR_BLUR_RADIUS_CM / BURT_FUR_BLUR_SAMPLE_COUNT;
        float scale = min(2.0, stepCm * pixelPerCm);
        float2 furStep = BurtDecodeFurDir(property.r) * _BurtFurBlurScreenSize.zw * scale;
        float4 blur = float4(centerColor.rgb, 1.0);
        bool occludedPos = false;
        bool occludedNeg = false;

        for (int i = 1; i <= BURT_FUR_BLUR_SAMPLE_COUNT; i++)
        {
            float2 positiveUv = saturate(uv + furStep * i);
            float2 negativeUv = saturate(uv - furStep * i);
            if (!occludedPos)
            {
                float4 sampleProperty = BurtSampleFurProperty(positiveUv);
                occludedPos = !BurtIsValidFurProperty(sampleProperty) || !BurtFurDepthAllowsBlur(sampleProperty.g, centerDepth);
                if (!occludedPos)
                {
                    blur += float4(BURT_SAMPLE_TEXTURE2D_LOD_CLAMP(_BurtCameraColorTexture, positiveUv, 0.0).rgb, 1.0);
                }
            }

            if (!occludedNeg)
            {
                float4 sampleProperty = BurtSampleFurProperty(negativeUv);
                occludedNeg = !BurtIsValidFurProperty(sampleProperty) || !BurtFurDepthAllowsBlur(sampleProperty.g, centerDepth);
                if (!occludedNeg)
                {
                    blur += float4(BURT_SAMPLE_TEXTURE2D_LOD_CLAMP(_BurtCameraColorTexture, negativeUv, 0.0).rgb, 1.0);
                }
            }
        }

        return float4(blur.rgb / max(blur.a, 1e-4), 1.0);
    }

    float4 FragComposite(Varyings input) : SV_Target
    {
        return BURT_SAMPLE_TEXTURE2D_LOD_CLAMP(_BurtFurBlurTemporalTexture, input.uv, 0.0);
    }

    float4 FragDilate(Varyings input) : SV_Target
    {
        float2 uv = input.uv;
        float4 center = BurtSampleFurProperty(uv);
        if (BurtIsValidFurProperty(center))
        {
            return center;
        }

        float4 best = center;
        bool hasBest = false;
        float2 texel = _BurtFurBlurScreenSize.zw;
        [unroll]
        for (int y = -1; y <= 1; y++)
        {
            [unroll]
            for (int x = -1; x <= 1; x++)
            {
                if (x == 0 && y == 0)
                {
                    continue;
                }

                float4 sampleProperty = BurtSampleFurProperty(uv + float2(x, y) * texel);
                if (!BurtIsValidFurProperty(sampleProperty))
                {
                    continue;
                }

                if (!hasBest || BurtFurDepthIsNearer(sampleProperty.g, best.g))
                {
                    best = sampleProperty;
                    hasBest = true;
                }
            }
        }

        return hasBest ? best : center;
    }

    void BurtFurCurrentNeighborhood(float2 uv, out float3 minimumColor, out float3 maximumColor)
    {
        float2 texel = _BurtFurBlurScreenSize.zw;
        minimumColor = float3(1e20, 1e20, 1e20);
        maximumColor = float3(-1e20, -1e20, -1e20);
        [unroll]
        for (int y = -1; y <= 1; y++)
        {
            [unroll]
            for (int x = -1; x <= 1; x++)
            {
                float3 color = BURT_SAMPLE_TEXTURE2D_LOD_CLAMP(_BurtFurBlurColorTexture, uv + float2(x, y) * texel, 0.0).rgb;
                minimumColor = min(minimumColor, color);
                maximumColor = max(maximumColor, color);
            }
        }
    }

    float4 FragTemporal(Varyings input) : SV_Target
    {
        float2 uv = input.uv;
        float4 current = BURT_SAMPLE_TEXTURE2D_LOD_CLAMP(_BurtFurBlurColorTexture, uv, 0.0);
        if (current.a <= 0.0)
        {
            return current;
        }

        float historyValid = _BurtFurBlurHistoryParams.x;
        float feedback = saturate(_BurtFurBlurHistoryParams.y);
        if (historyValid <= 0.5)
        {
            return current;
        }

        float4 property = BurtSampleFurProperty(uv);
        if (!BurtIsValidFurProperty(property))
        {
            return current;
        }

        float4 history = BURT_SAMPLE_TEXTURE2D_LOD_CLAMP(_BurtFurBlurHistoryTexture, uv, 0.0);
        if (history.a <= 0.0)
        {
            return current;
        }

        float3 minimumColor;
        float3 maximumColor;
        BurtFurCurrentNeighborhood(uv, minimumColor, maximumColor);
        float3 clampedHistory = clamp(history.rgb, minimumColor, maximumColor);
        return float4(lerp(current.rgb, clampedHistory, feedback), current.a);
    }

    float3 BurtFurDebugDirection(float theta)
    {
        if (theta <= BURT_FUR_VALID_THETA_EPSILON)
        {
            return float3(0.0, 0.0, 0.0);
        }

        float2 direction = BurtDecodeFurDir(theta);
        return float3(direction * 0.5 + 0.5, 1.0);
    }

    float4 FragDebug(Varyings input) : SV_Target
    {
        float2 uv = input.uv;
        float4 property = BurtSampleFurProperty(uv);
        if (_BurtFurBlurDebugMode == 1)
        {
            return float4(BurtFurDebugDirection(property.r), 1.0);
        }

        if (_BurtFurBlurDebugMode == 2)
        {
            float valid = BurtIsValidFurProperty(property) ? 1.0 : 0.0;
            return float4(BurtFurDepth01(property.g).xxx * valid, 1.0);
        }

        if (_BurtFurBlurDebugMode == 3)
        {
            return BURT_SAMPLE_TEXTURE2D_LOD_CLAMP(_BurtFurBlurColorTexture, uv, 0.0);
        }

        if (_BurtFurBlurDebugMode == 4)
        {
            return BURT_SAMPLE_TEXTURE2D_LOD_CLAMP(_BurtFurBlurTemporalTexture, uv, 0.0);
        }

        if (_BurtFurBlurDebugMode == 5)
        {
            return _BurtFurBlurHistoryParams.x > 0.5 ? BURT_SAMPLE_TEXTURE2D_LOD_CLAMP(_BurtFurBlurHistoryTexture, uv, 0.0) : float4(0.0, 0.0, 0.0, 1.0);
        }

        if (_BurtFurBlurDebugMode == 6)
        {
            float valid = BurtIsValidFurProperty(property) ? 1.0 : 0.0;
            float temporal = BURT_SAMPLE_TEXTURE2D_LOD_CLAMP(_BurtFurBlurTemporalTexture, uv, 0.0).a;
            return float4(valid, temporal, saturate(_BurtFurBlurHistoryParams.z / 16.0), 1.0);
        }

        return BURT_SAMPLE_TEXTURE2D_LOD_CLAMP(_BurtFurBlurTemporalTexture, uv, 0.0);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        Pass
        {
            Name "Burt Fur Blur"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragBlur
            ENDHLSL
        }

        Pass
        {
            Name "Burt Fur Blur Composite"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragComposite
            ENDHLSL
        }

        Pass
        {
            Name "Burt Fur Blur Property Dilate"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragDilate
            ENDHLSL
        }

        Pass
        {
            Name "Burt Fur Blur Temporal"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragTemporal
            ENDHLSL
        }

        Pass
        {
            Name "Burt Fur Blur Debug"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragDebug
            ENDHLSL
        }
    }

    Fallback Off
}
