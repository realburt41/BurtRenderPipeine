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
    Texture2D _BurtFurBlurPropertyHistoryTexture;
    Texture2D _BurtFurBlurVelocityTexture;
    UNITY_DECLARE_DEPTH_TEXTURE(_BurtCameraDepthTexture);
    #if SHADER_TARGET >= 45
    StructuredBuffer<uint> _BurtFurBlurTileDataBuffer;
    #endif
    float4 _BurtFurBlurScreenSize;
    float4 _BurtFurBlurHistoryParams;
    float4 _BurtFurBlurParams;
    float4 _BurtFurBlurTemporalParams;
    float4x4 _BurtFurBlurPreviousNonJitteredViewProjection;
    float4x4 _BurtFurBlurInverseCurrentNonJitteredViewProjection;
    float4 _BurtFurBlurJitter;
    int _BurtFurBlurDebugMode;

    static const float BURT_TWO_PI = 6.28318530717958647692;
    static const int BURT_FUR_BLUR_SAMPLE_COUNT = 3;
    static const float BURT_METER_TO_CENTIMETER = 100.0;
    static const float BURT_FUR_VALID_THETA_EPSILON = 1e-8;
    static const float BURT_FUR_TEMPORAL_DIRECTION_MIN_DOT = 0.4;

    struct Attributes
    {
        uint vertexID : SV_VertexID;
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    #if SHADER_TARGET >= 45
    struct TiledAttributes
    {
        uint vertexID : SV_VertexID;
        uint instanceID : SV_InstanceID;
    };
    #endif

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

    #if SHADER_TARGET >= 45
    Varyings VertTiled(TiledAttributes input)
    {
        Varyings output;
        float2 corner = float2(input.vertexID == 1 || input.vertexID == 2 || input.vertexID == 4 ? 1.0 : 0.0, input.vertexID == 2 || input.vertexID == 4 || input.vertexID == 5 ? 1.0 : 0.0);
        uint tileIndex = input.instanceID * 2;
        float2 tile = float2(_BurtFurBlurTileDataBuffer[tileIndex + 0], _BurtFurBlurTileDataBuffer[tileIndex + 1]);
        float2 pixel = min(tile * 8.0 + corner * 8.0, _BurtFurBlurScreenSize.xy);
        float2 uv = pixel * _BurtFurBlurScreenSize.zw;
        float2 positionUv = uv;
        #if UNITY_UV_STARTS_AT_TOP
            positionUv.y = 1.0 - positionUv.y;
        #endif
        output.positionCS = float4(positionUv * 2.0 - 1.0, 0.0, 1.0);
        output.uv = uv;
        return output;
    }
    #endif

    float2 BurtDecodeFurDir(float angle)
    {
        angle *= BURT_TWO_PI;
        float s;
        float c;
        sincos(angle, s, c);
        return float2(c, s);
    }

    float BurtEncodeFurDir(float2 direction)
    {
        float angle = atan2(direction.y, direction.x);
        angle += angle < 0.0 ? BURT_TWO_PI : 0.0;
        return angle / BURT_TWO_PI;
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
        return abs(sampleDepth - centerDepth) <= max(_BurtFurBlurParams.y, 1e-6);
    }

    bool BurtFurDepthOccludesBlur(float sampleDepth, float centerDepth)
    {
        float threshold = max(_BurtFurBlurParams.y, 1e-6);
        #if UNITY_REVERSED_Z
            return sampleDepth > centerDepth - threshold;
        #else
            return sampleDepth < centerDepth + threshold;
        #endif
    }

    float4 BurtSampleFurProperty(float2 uv)
    {
        return BURT_SAMPLE_TEXTURE2D_LOD_POINT_CLAMP(_BurtFurBlurPropertyTexture, uv, 0.0);
    }

    float4 BurtSampleFurPropertyHistory(float2 uv)
    {
        return BURT_SAMPLE_TEXTURE2D_LOD_POINT_CLAMP(_BurtFurBlurPropertyHistoryTexture, uv, 0.0);
    }

    bool BurtFurUvInBounds(float2 uv)
    {
        return uv.x >= 0.0 && uv.x <= 1.0 && uv.y >= 0.0 && uv.y <= 1.0;
    }

    float2 BurtFurClipToUv(float4 clipPosition)
    {
        float2 uv = clipPosition.xy / max(abs(clipPosition.w), 1e-6);
        uv = uv * 0.5 + 0.5;
        #if UNITY_UV_STARTS_AT_TOP
            uv.y = 1.0 - uv.y;
        #endif
        return uv;
    }

    float4 BurtFurScreenUvToClip(float2 uv, float rawDepth)
    {
        float2 clipXY = uv * 2.0 - 1.0;
        #if UNITY_UV_STARTS_AT_TOP
            clipXY.y = -clipXY.y;
        #endif
        return float4(clipXY, rawDepth, 1.0);
    }

    bool BurtFurTryReprojectHistoryUv(float2 uv, float rawDepth, out float2 historyUv)
    {
        float4 velocity = BURT_SAMPLE_TEXTURE2D_LOD_CLAMP(_BurtFurBlurVelocityTexture, uv, 0.0);
        if (velocity.z > 0.5)
        {
            historyUv = uv + velocity.xy;
            return BurtFurUvInBounds(historyUv);
        }

        float4 clip = BurtFurScreenUvToClip(uv, rawDepth);
        float2 currentFrameJitter = _BurtFurBlurJitter.xy;
        #if UNITY_UV_STARTS_AT_TOP
            currentFrameJitter.y = -currentFrameJitter.y;
        #endif
        clip.xy -= currentFrameJitter;

        float4 world = mul(_BurtFurBlurInverseCurrentNonJitteredViewProjection, clip);
        world.xyz /= max(abs(world.w), 1e-6);

        float4 previousClip = mul(_BurtFurBlurPreviousNonJitteredViewProjection, float4(world.xyz, 1.0));
        historyUv = BurtFurClipToUv(previousClip);
        return previousClip.w > 1e-5 && BurtFurUvInBounds(historyUv);
    }

    bool BurtFurPropertiesTemporallyCompatible(float4 current, float4 history)
    {
        if (!BurtIsValidFurProperty(current) || !BurtIsValidFurProperty(history))
        {
            return false;
        }

        if (!BurtFurDepthAllowsBlur(history.g, current.g))
        {
            return false;
        }

        float2 currentDir = BurtDecodeFurDir(current.r);
        float2 historyDir = BurtDecodeFurDir(history.r);
        return dot(currentDir, historyDir) >= BURT_FUR_TEMPORAL_DIRECTION_MIN_DOT;
    }

    float4 FragSetupDepth(Varyings input) : SV_Target
    {
        float rawDepth = SAMPLE_DEPTH_TEXTURE(_BurtCameraDepthTexture, input.uv);
        return float4(0.0, rawDepth, 0.0, 1.0);
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
        float stepCm = max(_BurtFurBlurParams.x, 0.0) / BURT_FUR_BLUR_SAMPLE_COUNT;
        float scale = min(2.0, stepCm * pixelPerCm);
        float2 furStep = BurtDecodeFurDir(property.r) * _BurtFurBlurScreenSize.zw * scale;
        float4 blur = float4(centerColor.rgb, 1.0);
        bool occludedPos = false;
        bool occludedNeg = false;

        for (int i = 1; i <= BURT_FUR_BLUR_SAMPLE_COUNT; i++)
        {
            float2 positiveUv = uv + furStep * i;
            float2 negativeUv = uv - furStep * i;
            if (!occludedPos)
            {
                float4 sampleColor = BURT_SAMPLE_TEXTURE2D_LOD_CLAMP(_BurtCameraColorTexture, positiveUv, 0.0);
                float sampleDepth = BurtSampleFurProperty(positiveUv).g;
                occludedPos = BurtFurDepthOccludesBlur(sampleDepth, centerDepth);
                blur += float4(sampleColor.rgb, 1.0);
            }

            if (!occludedNeg)
            {
                float4 sampleColor = BURT_SAMPLE_TEXTURE2D_LOD_CLAMP(_BurtCameraColorTexture, negativeUv, 0.0);
                float sampleDepth = BurtSampleFurProperty(negativeUv).g;
                occludedNeg = BurtFurDepthOccludesBlur(sampleDepth, centerDepth);
                blur += float4(sampleColor.rgb, 1.0);
            }
        }

        return float4(blur.rgb / max(blur.a, 1e-4), 1.0);
    }

    float4 FragComposite(Varyings input) : SV_Target
    {
        float4 property = BurtSampleFurProperty(input.uv);
        if (!BurtIsValidFurProperty(property))
        {
            return 0.0;
        }

        float4 blurResult = BURT_SAMPLE_TEXTURE2D_LOD_CLAMP(_BurtFurBlurTemporalTexture, input.uv, 0.0);
        return float4(blurResult.rgb, 1.0);
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

                float2 offset = float2(x, y);
                float4 sampleProperty = BurtSampleFurProperty(uv + offset * texel);
                if (!BurtIsValidFurProperty(sampleProperty))
                {
                    continue;
                }

                float2 offsetDirection = normalize(offset);
                float directionAlignment = abs(dot(BurtDecodeFurDir(sampleProperty.r), offsetDirection));
                if (directionAlignment < saturate(_BurtFurBlurParams.z))
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

    float2 BurtFurFilterCurrentDirection(float2 uv);
    void BurtFurCurrentDirectionNeighborhood(float2 uv, out float2 minimumDir, out float2 maximumDir);

    float4 FragThetaTemporal(Varyings input) : SV_Target
    {
        float2 uv = input.uv;
        float4 current = BurtSampleFurProperty(uv);
        if (!BurtIsValidFurProperty(current))
        {
            return current;
        }

        float historyValid = _BurtFurBlurTemporalParams.y;
        if (historyValid <= 0.5)
        {
            return current;
        }

        float2 historyUv;
        if (!BurtFurTryReprojectHistoryUv(uv, current.g, historyUv))
        {
            return current;
        }

        float4 history = BurtSampleFurPropertyHistory(historyUv);
        if (!BurtFurPropertiesTemporallyCompatible(current, history))
        {
            return current;
        }

        float feedback = saturate(_BurtFurBlurParams.w);
        float2 filteredDir = BurtFurFilterCurrentDirection(uv);
        float2 historyDir = BurtDecodeFurDir(history.r);
        float2 minimumDir;
        float2 maximumDir;
        BurtFurCurrentDirectionNeighborhood(uv, minimumDir, maximumDir);
        historyDir = clamp(historyDir, minimumDir, maximumDir);
        float2 blendedDir = lerp(historyDir, filteredDir, saturate(1.0 - feedback));
        float blendedLengthSquared = dot(blendedDir, blendedDir);
        float2 stableDir = blendedLengthSquared > BURT_FUR_VALID_THETA_EPSILON ? blendedDir * rsqrt(blendedLengthSquared) : BurtDecodeFurDir(current.r);

        float stableDepth = lerp(current.g, history.g, feedback * 0.25);
        return float4(BurtEncodeFurDir(stableDir), stableDepth, current.b, current.a);
    }

    float3 BurtFurRgbToYCoCg(float3 rgb)
    {
        float y = dot(rgb, float3(1.0, 2.0, 1.0));
        float co = dot(rgb, float3(2.0, 0.0, -2.0));
        float cg = dot(rgb, float3(-1.0, 2.0, -1.0));
        return float3(y, co, cg);
    }

    float3 BurtFurYCoCgToRgb(float3 ycocg)
    {
        float y = ycocg.x * 0.25;
        float co = ycocg.y * 0.25;
        float cg = ycocg.z * 0.25;
        return float3(y + co - cg, y + cg, y - co - cg);
    }

    float4 BurtFurTransformSceneColor(float4 color)
    {
        return float4(BurtFurRgbToYCoCg(color.rgb), color.a);
    }

    float4 BurtFurTransformBackToSceneColor(float4 color)
    {
        return float4(BurtFurYCoCgToRgb(color.rgb), color.a);
    }

    float BurtFurSceneColorLuma4(float4 color)
    {
        return color.x;
    }

    float BurtFurHdrWeight(float4 transformedColor)
    {
        return rcp(max(transformedColor.x, 0.0) + 4.0);
    }

    float2 BurtFurWeightedLerpFactors(float historyWeight, float currentWeight, float currentBlend)
    {
        float historyBlend = saturate(1.0 - currentBlend) * historyWeight;
        float filteredBlend = saturate(currentBlend) * currentWeight;
        float rcpBlend = rcp(max(historyBlend + filteredBlend, 1e-6));
        return float2(historyBlend, filteredBlend) * rcpBlend;
    }

    float BurtFurPlusWeight(float2 pixelOffset)
    {
        float2 jitterPixels = _BurtFurBlurJitter.zw;
        float2 delta = pixelOffset - jitterPixels;
        return exp(-2.29 * dot(delta, delta));
    }

    void BurtFurComputePlusWeights(out float upWeight, out float leftWeight, out float centerWeight, out float rightWeight, out float downWeight)
    {
        upWeight = BurtFurPlusWeight(float2(0.0, -1.0));
        leftWeight = BurtFurPlusWeight(float2(-1.0, 0.0));
        centerWeight = BurtFurPlusWeight(float2(0.0, 0.0));
        rightWeight = BurtFurPlusWeight(float2(1.0, 0.0));
        downWeight = BurtFurPlusWeight(float2(0.0, 1.0));
        float total = max(upWeight + leftWeight + centerWeight + rightWeight + downWeight, 1e-6);
        upWeight /= total;
        leftWeight /= total;
        centerWeight /= total;
        rightWeight /= total;
        downWeight /= total;
    }

    float4 BurtSampleFurTemporalColor(float2 uv, float2 pixelOffset)
    {
        return BURT_SAMPLE_TEXTURE2D_LOD_CLAMP(_BurtFurBlurColorTexture, uv + pixelOffset * _BurtFurBlurScreenSize.zw, 0.0);
    }

    float4 BurtFurFilterCurrentColor(float2 uv)
    {
        float upWeight;
        float leftWeight;
        float centerWeight;
        float rightWeight;
        float downWeight;
        BurtFurComputePlusWeights(upWeight, leftWeight, centerWeight, rightWeight, downWeight);

        float4 filtered = 0.0;
        filtered += BurtFurTransformSceneColor(BurtSampleFurTemporalColor(uv, float2(0.0, -1.0))) * upWeight;
        filtered += BurtFurTransformSceneColor(BurtSampleFurTemporalColor(uv, float2(-1.0, 0.0))) * leftWeight;
        filtered += BurtFurTransformSceneColor(BurtSampleFurTemporalColor(uv, float2(0.0, 0.0))) * centerWeight;
        filtered += BurtFurTransformSceneColor(BurtSampleFurTemporalColor(uv, float2(1.0, 0.0))) * rightWeight;
        filtered += BurtFurTransformSceneColor(BurtSampleFurTemporalColor(uv, float2(0.0, 1.0))) * downWeight;
        return filtered;
    }

    float2 BurtSampleFurTemporalDirection(float2 uv, float2 pixelOffset)
    {
        float theta = BurtSampleFurProperty(uv + pixelOffset * _BurtFurBlurScreenSize.zw).r;
        return theta > BURT_FUR_VALID_THETA_EPSILON ? BurtDecodeFurDir(theta) : 0.0;
    }

    float2 BurtFurFilterCurrentDirection(float2 uv)
    {
        float upWeight;
        float leftWeight;
        float centerWeight;
        float rightWeight;
        float downWeight;
        BurtFurComputePlusWeights(upWeight, leftWeight, centerWeight, rightWeight, downWeight);

        float2 upDir = BurtSampleFurTemporalDirection(uv, float2(0.0, -1.0));
        float2 leftDir = BurtSampleFurTemporalDirection(uv, float2(-1.0, 0.0));
        float2 centerDir = BurtSampleFurTemporalDirection(uv, float2(0.0, 0.0));
        float2 rightDir = BurtSampleFurTemporalDirection(uv, float2(1.0, 0.0));
        float2 downDir = BurtSampleFurTemporalDirection(uv, float2(0.0, 1.0));

        float upValid = step(BURT_FUR_VALID_THETA_EPSILON, dot(upDir, upDir));
        float leftValid = step(BURT_FUR_VALID_THETA_EPSILON, dot(leftDir, leftDir));
        float centerValid = step(BURT_FUR_VALID_THETA_EPSILON, dot(centerDir, centerDir));
        float rightValid = step(BURT_FUR_VALID_THETA_EPSILON, dot(rightDir, rightDir));
        float downValid = step(BURT_FUR_VALID_THETA_EPSILON, dot(downDir, downDir));
        float weightSum = upWeight * upValid + leftWeight * leftValid + centerWeight * centerValid + rightWeight * rightValid + downWeight * downValid;
        float2 filtered = upDir * upWeight * upValid +
            leftDir * leftWeight * leftValid +
            centerDir * centerWeight * centerValid +
            rightDir * rightWeight * rightValid +
            downDir * downWeight * downValid;
        return weightSum > 1e-6 ? filtered / weightSum : centerDir;
    }

    void BurtFurCurrentDirectionNeighborhood(float2 uv, out float2 minimumDir, out float2 maximumDir)
    {
        float2 upDir = BurtSampleFurTemporalDirection(uv, float2(0.0, -1.0));
        float2 leftDir = BurtSampleFurTemporalDirection(uv, float2(-1.0, 0.0));
        float2 centerDir = BurtSampleFurTemporalDirection(uv, float2(0.0, 0.0));
        float2 rightDir = BurtSampleFurTemporalDirection(uv, float2(1.0, 0.0));
        float2 downDir = BurtSampleFurTemporalDirection(uv, float2(0.0, 1.0));
        minimumDir = min(min(min(upDir, leftDir), centerDir), min(rightDir, downDir));
        maximumDir = max(max(max(upDir, leftDir), centerDir), max(rightDir, downDir));
    }

    void BurtFurCurrentNeighborhood(float2 uv, out float4 minimumColor, out float4 maximumColor)
    {
        float4 upColor = BurtFurTransformSceneColor(BurtSampleFurTemporalColor(uv, float2(0.0, -1.0)));
        float4 leftColor = BurtFurTransformSceneColor(BurtSampleFurTemporalColor(uv, float2(-1.0, 0.0)));
        float4 centerColor = BurtFurTransformSceneColor(BurtSampleFurTemporalColor(uv, float2(0.0, 0.0)));
        float4 rightColor = BurtFurTransformSceneColor(BurtSampleFurTemporalColor(uv, float2(1.0, 0.0)));
        float4 downColor = BurtFurTransformSceneColor(BurtSampleFurTemporalColor(uv, float2(0.0, 1.0)));
        minimumColor = min(min(min(upColor, leftColor), centerColor), min(rightColor, downColor));
        maximumColor = max(max(max(upColor, leftColor), centerColor), max(rightColor, downColor));
    }

    void BurtFurCurrentNeighborhoodRgb(float2 uv, out float3 minimumColor, out float3 maximumColor)
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

        float2 historyUv;
        if (!BurtFurTryReprojectHistoryUv(uv, property.g, historyUv))
        {
            return current;
        }

        float4 historyProperty = BurtSampleFurPropertyHistory(historyUv);
        if (!BurtFurPropertiesTemporallyCompatible(property, historyProperty))
        {
            return current;
        }

        float4 rawHistory = BURT_SAMPLE_TEXTURE2D_LOD_CLAMP(_BurtFurBlurHistoryTexture, historyUv, 0.0);
        if (rawHistory.a <= 0.0)
        {
            return current;
        }

        float historyExposureCorrection = _BurtFurBlurHistoryParams.w > 0.0 ? _BurtFurBlurHistoryParams.w : 1.0;
        rawHistory.rgb *= historyExposureCorrection;
        float4 filteredCurrent = BurtFurFilterCurrentColor(uv);
        float4 transformedCurrent = BurtFurTransformSceneColor(current);
        float4 history = BurtFurTransformSceneColor(rawHistory);
        float4 minimumColor;
        float4 maximumColor;
        BurtFurCurrentNeighborhood(uv, minimumColor, maximumColor);
        float4 clampedHistory = clamp(history, minimumColor, maximumColor);

        float lumaContrast = BurtFurSceneColorLuma4(maximumColor) - BurtFurSceneColorLuma4(minimumColor);
        float addAliasing = saturate(rcp(1.0 + lumaContrast * 128.0));
        filteredCurrent = lerp(filteredCurrent, transformedCurrent, addAliasing);

        float currentBlend = saturate(1.0 - feedback);
        float2 blendWeights = BurtFurWeightedLerpFactors(BurtFurHdrWeight(clampedHistory), BurtFurHdrWeight(filteredCurrent), currentBlend);
        float4 result = BurtFurTransformBackToSceneColor(clampedHistory * blendWeights.x + filteredCurrent * blendWeights.y);
        return float4(result.rgb, current.a);
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

        if (_BurtFurBlurDebugMode == 7)
        {
            float2 historyUv;
            float velocityValid = BURT_SAMPLE_TEXTURE2D_LOD_CLAMP(_BurtFurBlurVelocityTexture, uv, 0.0).z;
            float reprojected = BurtIsValidFurProperty(property) && BurtFurTryReprojectHistoryUv(uv, property.g, historyUv) ? 1.0 : 0.0;
            float4 historyProperty = reprojected > 0.5 ? BurtSampleFurPropertyHistory(historyUv) : 0.0;
            float compatible = BurtFurPropertiesTemporallyCompatible(property, historyProperty) ? 1.0 : 0.0;
            return float4(reprojected, compatible, saturate(velocityValid), _BurtFurBlurTemporalParams.y);
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

        Pass
        {
            Name "Burt Fur Blur Theta Temporal"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragThetaTemporal
            ENDHLSL
        }

        Pass
        {
            Name "Burt Fur Blur Tiled"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex VertTiled
            #pragma fragment FragBlur
            ENDHLSL
        }

        Pass
        {
            Name "Burt Fur Blur Temporal Tiled"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex VertTiled
            #pragma fragment FragTemporal
            ENDHLSL
        }

        Pass
        {
            Name "Burt Fur Blur Setup Depth"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragSetupDepth
            ENDHLSL
        }
    }

    Fallback Off
}
