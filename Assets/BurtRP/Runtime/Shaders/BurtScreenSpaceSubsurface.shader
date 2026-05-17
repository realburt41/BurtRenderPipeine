Shader "Hidden/BurtRP/ScreenSpaceSubsurface"
{
    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        HLSLINCLUDE
        #pragma target 3.5
        #include "UnityCG.cginc"
        #include "ShaderLibrary/BurtDeferred.hlsl"

        sampler2D _BurtCameraColorTexture;
        sampler2D _BurtSSSSourceTexture;
        sampler2D _BurtSSSOriginalTexture;
        float4 _BurtSSSScreenSize;
        float4 _BurtSSSParams; // x=radiusPx, y=depthSigma, z=normalSigma, w=minStrength
        float4 _BurtSSSParams2; // x=blend, y=distanceScale, z=boundaryBleed, w=tintStrength
        float4 _BurtSSSSurfaceAlbedo; // rgb=profile surface albedo
        float4 _BurtSSSMeanFreePath; // rgb=profile mean free path color, w=screen radius scale
        float4 _BurtSSSProfileTint; // rgb=profile tint
        float4 _BurtSSSBoundaryColorBleed; // rgb=profile boundary bleed color

        static const float3 BURT_5S_KERNEL_CENTER = float3(0.204f, 0.236f, 0.290f);

        struct Attributes
        {
            uint vertexID : SV_VertexID;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 screenUV : TEXCOORD0;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionCS = BurtGetFullScreenTriangleVertexPosition(input.vertexID);
            output.screenUV = BurtGetFullScreenTriangleTexCoord(input.vertexID);
            return output;
        }

        bool BurtSSSIsSkyDepth(float rawDepth)
        {
            #if defined(UNITY_REVERSED_Z)
                return rawDepth <= 0.00001f;
            #else
                return rawDepth >= 0.99999f;
            #endif
        }

        struct BurtSSSSurface
        {
            float valid;
            float strength;
            float thickness;
            float3 normalWS;
            float3 tint;
            float rawDepth;
            float linearEyeDepth;
        };

        BurtSSSSurface BurtSSSLoadSurface(float2 uv)
        {
            BurtEncodedGBuffer encoded = BurtSampleEncodedGBuffer(uv);
            BurtGBufferData data = BurtDecodeGBuffer(encoded);
            BurtSSSSurface surface;
            surface.rawDepth = BurtSampleDeferredRawDepth(uv);
            surface.linearEyeDepth = BurtSSSIsSkyDepth(surface.rawDepth) ? 0.0f : LinearEyeDepth(surface.rawDepth);
            surface.strength = BurtGetSubsurfaceStrength(data);
            surface.thickness = BurtGetSubsurfaceThickness(data);
            surface.normalWS = BurtGetDefaultLitNormalWS(data);
            surface.tint = BurtGetSubsurfaceTint(data);
            surface.valid = BurtIsSubsurfaceShadingModel(data.shadingModelID) && !BurtSSSIsSkyDepth(surface.rawDepth) && surface.strength > _BurtSSSParams.w ? 1.0f : 0.0f;
            return surface;
        }

        float BurtSSSInBounds(float2 uv)
        {
            float2 insideMin = step(float2(0.0f, 0.0f), uv);
            float2 insideMax = step(uv, float2(1.0f, 1.0f));
            return insideMin.x * insideMin.y * insideMax.x * insideMax.y;
        }

        float BurtSSSSampleWeight(BurtSSSSurface center, BurtSSSSurface sampleSurface, float inBounds)
        {
            float modelWeight = center.valid * sampleSurface.valid;
            float depthWindow = max(_BurtSSSParams.y * max(center.linearEyeDepth, 0.05f), 0.0001f);
            float depthWeight = exp2(-abs(center.linearEyeDepth - sampleSurface.linearEyeDepth) / depthWindow);
            float normalWeight = saturate(dot(center.normalWS, sampleSurface.normalWS));
            normalWeight = pow(normalWeight, max(_BurtSSSParams.z * 24.0f, 1.0f));
            float thicknessWeight = saturate(1.0f - abs(center.thickness - sampleSurface.thickness) * 2.5f);
            return inBounds * modelWeight * depthWeight * normalWeight * thicknessWeight;
        }

        float3 BurtSSSProfileKernelScale(BurtSSSSurface center, float offset)
        {
            float3 meanFreePath = max(_BurtSSSMeanFreePath.rgb, float3(0.0001f, 0.0001f, 0.0001f));
            float maxMeanFreePath = max(max(meanFreePath.r, meanFreePath.g), meanFreePath.b);
            float3 normalizedMeanFreePath = meanFreePath / max(maxMeanFreePath, 0.0001f);
            float3 spread = lerp(float3(1.0f, 1.0f, 1.0f), normalizedMeanFreePath, saturate(center.strength));
            float3 channelFalloff = max(1.0f / max(spread, float3(0.0001f, 0.0001f, 0.0001f)) - 1.0f, float3(0.0f, 0.0f, 0.0f));
            return max(exp2(-abs(offset) * channelFalloff * 0.28f), float3(0.0001f, 0.0001f, 0.0001f));
        }

        void BurtSSSAccumulateSample(
            BurtSSSSurface center,
            float3 original,
            float2 uv,
            float2 texelStep,
            float offset,
            float3 kernelWeight,
            inout float3 sumColor,
            inout float3 sumWeight)
        {
            float2 sampleUVUnclamped = uv + texelStep * offset;
            float inBounds = BurtSSSInBounds(sampleUVUnclamped);
            float2 sampleUV = saturate(sampleUVUnclamped);
            BurtSSSSurface sampleSurface = BurtSSSLoadSurface(sampleUV);
            float scalarWeight = BurtSSSSampleWeight(center, sampleSurface, inBounds);
            float3 weight = kernelWeight * scalarWeight;
            float3 sampleColor = tex2D(_BurtSSSSourceTexture, sampleUV).rgb;
            float boundaryBlend = saturate((1.0f - scalarWeight) * _BurtSSSParams2.z);
            float3 boundaryColor = original * max(_BurtSSSBoundaryColorBleed.rgb, float3(0.0f, 0.0f, 0.0f));
            sampleColor = lerp(sampleColor, boundaryColor, boundaryBlend);
            float3 weightedKernel = weight * BurtSSSProfileKernelScale(center, offset);
            sumColor += sampleColor * weightedKernel;
            sumWeight += weightedKernel;
        }

        void BurtSSSAccumulatePair(
            BurtSSSSurface center,
            float3 original,
            float2 uv,
            float2 texelStep,
            float offset,
            float3 kernelWeight,
            inout float3 sumColor,
            inout float3 sumWeight)
        {
            BurtSSSAccumulateSample(center, original, uv, texelStep, offset, kernelWeight, sumColor, sumWeight);
            BurtSSSAccumulateSample(center, original, uv, texelStep, -offset, kernelWeight, sumColor, sumWeight);
        }

        void BurtSSSAccumulate5SKernel(
            BurtSSSSurface center,
            float3 original,
            float2 uv,
            float2 texelStep,
            inout float3 sumColor,
            inout float3 sumWeight)
        {
            BurtSSSAccumulatePair(center, original, uv, texelStep, 0.22f, float3(0.150f, 0.165f, 0.168f), sumColor, sumWeight);
            BurtSSSAccumulatePair(center, original, uv, texelStep, 0.46f, float3(0.118f, 0.123f, 0.114f), sumColor, sumWeight);
            BurtSSSAccumulatePair(center, original, uv, texelStep, 0.78f, float3(0.090f, 0.088f, 0.074f), sumColor, sumWeight);
            BurtSSSAccumulatePair(center, original, uv, texelStep, 1.16f, float3(0.066f, 0.058f, 0.043f), sumColor, sumWeight);
            BurtSSSAccumulatePair(center, original, uv, texelStep, 1.60f, float3(0.047f, 0.036f, 0.023f), sumColor, sumWeight);
            BurtSSSAccumulatePair(center, original, uv, texelStep, 2.12f, float3(0.032f, 0.021f, 0.012f), sumColor, sumWeight);
            BurtSSSAccumulatePair(center, original, uv, texelStep, 2.74f, float3(0.021f, 0.012f, 0.006f), sumColor, sumWeight);
            BurtSSSAccumulatePair(center, original, uv, texelStep, 3.48f, float3(0.013f, 0.006f, 0.003f), sumColor, sumWeight);
            BurtSSSAccumulatePair(center, original, uv, texelStep, 4.36f, float3(0.008f, 0.003f, 0.0015f), sumColor, sumWeight);
            BurtSSSAccumulatePair(center, original, uv, texelStep, 5.42f, float3(0.0045f, 0.0015f, 0.0007f), sumColor, sumWeight);
            BurtSSSAccumulatePair(center, original, uv, texelStep, 6.68f, float3(0.0025f, 0.0007f, 0.0003f), sumColor, sumWeight);
            BurtSSSAccumulatePair(center, original, uv, texelStep, 8.16f, float3(0.0015f, 0.0003f, 0.0001f), sumColor, sumWeight);
        }

        float3 BurtSSSBlur(float2 uv, float2 direction, float applyTint)
        {
            BurtSSSSurface center = BurtSSSLoadSurface(uv);
            float3 original = tex2D(_BurtSSSSourceTexture, uv).rgb;
            if (center.valid <= 0.0f)
            {
                return original;
            }

            float distanceFade = rsqrt(1.0f + center.linearEyeDepth * max(_BurtSSSParams2.y, 0.01f));
            float radius = _BurtSSSParams.x * _BurtSSSMeanFreePath.w * lerp(0.35f, 1.85f, center.thickness) * lerp(0.25f, 1.0f, center.strength) * distanceFade;
            float2 texelStep = direction * _BurtSSSScreenSize.zw * radius;
            float3 centerWeight = BURT_5S_KERNEL_CENTER;
            float3 sumColor = original * centerWeight;
            float3 sumWeight = centerWeight;
            BurtSSSAccumulate5SKernel(center, original, uv, texelStep, sumColor, sumWeight);

            float3 blurred = sumColor / max(sumWeight, float3(0.0001f, 0.0001f, 0.0001f));
            float3 materialTint = max(center.tint, float3(0.0f, 0.0f, 0.0f));
            float3 profileTint = max(_BurtSSSProfileTint.rgb, float3(0.0f, 0.0f, 0.0f));
            float3 profileAlbedo = max(_BurtSSSSurfaceAlbedo.rgb, float3(0.0f, 0.0f, 0.0f));
            float3 tint = lerp(float3(1.0f, 1.0f, 1.0f), materialTint * profileTint * lerp(float3(1.0f, 1.0f, 1.0f), profileAlbedo, 0.35f), _BurtSSSParams2.w);
            return blurred * lerp(float3(1.0f, 1.0f, 1.0f), tint, saturate(applyTint));
        }

        float4 FragCopy(Varyings input) : SV_Target
        {
            return tex2D(_BurtCameraColorTexture, input.screenUV);
        }

        float4 FragHorizontal(Varyings input) : SV_Target
        {
            return float4(BurtSSSBlur(input.screenUV, float2(1.0f, 0.0f), 0.0f), 1.0f);
        }

        float4 FragVertical(Varyings input) : SV_Target
        {
            float3 blurred = BurtSSSBlur(input.screenUV, float2(0.0f, 1.0f), 1.0f);
            float3 original = tex2D(_BurtSSSOriginalTexture, input.screenUV).rgb;
            BurtSSSSurface center = BurtSSSLoadSurface(input.screenUV);
            float blend = saturate(center.strength * _BurtSSSParams2.x);
            return float4(center.valid > 0.0f ? lerp(original, blurred, blend) : original, 1.0f);
        }
        ENDHLSL

        Pass
        {
            Name "Burt Screen Space Subsurface Copy"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragCopy
            ENDHLSL
        }

        Pass
        {
            Name "Burt Screen Space Subsurface Horizontal"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragHorizontal
            ENDHLSL
        }

        Pass
        {
            Name "Burt Screen Space Subsurface Vertical"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragVertical
            ENDHLSL
        }
    }
}
