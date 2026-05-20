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
        sampler2D _BurtSSSSetupTexture;
        sampler2D _BurtSSSProfileIDAndTypeTexture;
        sampler2D _BurtSSSMaskTexture;
        sampler2D _BurtSSSTileTexture;
        sampler2D _BurtSSSBlurTexture;
        sampler2D _BurtSSSCombineTexture;
        sampler2D _BurtSSSHistoryDebugTexture;
        float4 _BurtSSSScreenSize;
        float4 _BurtSSSTileSize;
        float _BurtSSSDebugMode;
        float4 _BurtSSSHistoryDebugParams; // x=valid, y=age, z=max samples, w=variance target.
        float4 _BurtSSSParams; // x=radiusPx, y=depthSigma, z=normalSigma, w=minStrength
        float4 _BurtSSSParams2; // x=blend, y=distanceScale, z=boundaryBleed, w=tintStrength
        float4 _BurtSSSSurfaceAlbedo; // rgb=profile surface albedo
        float4 _BurtSSSMeanFreePath; // rgb=profile mean free path color, w=screen radius scale
        float4 _BurtSSSProfileTint; // rgb=profile tint
        float4 _BurtSSSBoundaryColorBleed; // rgb=profile boundary bleed color
        float _BurtSSSProfileCount;
        float4 _BurtSSSProfileParams[8];
        float4 _BurtSSSProfileParams2[8];
        float4 _BurtSSSProfileSurfaceAlbedos[8];
        float4 _BurtSSSProfileMeanFreePaths[8];
        float4 _BurtSSSProfileTints[8];
        float4 _BurtSSSProfileBoundaryColorBleeds[8];
        float4 _BurtSSSProfileTransmissions[8];
        float4 _BurtSSSProfileTransmissionTints[8];
        static const float3 BURT_5S_KERNEL_CENTER = float3(0.204f, 0.236f, 0.290f);
        static const float3 BURT_LUMINANCE_WEIGHTS = float3(0.3f, 0.59f, 0.11f);
        static const float BURT_SSS_EXTINCTION_DECODE_SCALE = 100.0f;
        static const float BURT_SSS_PROFILE_PARAM_SURFACE_ALBEDO_OFFSET = 0.0f;
        static const float BURT_SSS_PROFILE_PARAM_MEAN_FREE_PATH_OFFSET = 1.0f;
        static const float BURT_SSS_PROFILE_PARAM_TINT_OFFSET = 2.0f;
        static const float BURT_SSS_PROFILE_PARAM_BOUNDARY_COLOR_BLEED_OFFSET = 3.0f;
        static const float BURT_SSS_PROFILE_PARAM_TRANSMISSION_OFFSET = 5.0f;
        static const float BURT_SSS_PROFILE_PARAM_KERNEL0_OFFSET = 38.0f;
        static const float BURT_SSS_PROFILE_PARAM_KERNEL0_SIZE = 13.0f;
        static const float BURT_SSS_PROFILE_PARAM_KERNEL1_OFFSET = 51.0f;
        static const float BURT_SSS_PROFILE_PARAM_KERNEL1_SIZE = 9.0f;
        static const float BURT_SSS_PROFILE_PARAM_KERNEL2_OFFSET = 60.0f;
        static const float BURT_SSS_PROFILE_PARAM_KERNEL2_SIZE = 6.0f;
        static const uint BURT_SSS_PROFILE_TYPE_BURLEY = 0x40u;
        static const uint BURT_SSS_PROFILE_TYPE_MASK = 0xC0u;
        static const uint BURT_SSS_PROFILE_ID_MASK = 0x3Fu;

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
            float profileIndex;
            uint profileType;
            float rawDepth;
            float linearEyeDepth;
        };

        struct BurtSSSProfile
        {
            float4 params;
            float4 params2;
            float4 surfaceAlbedo;
            float4 meanFreePath;
            float4 tint;
            float4 boundaryColorBleed;
            float4 transmission;
            float4 transmissionTint;
        };

        int BurtSSSResolveProfileIndex(float profileIndex)
        {
            return _BurtSSSProfileCount > 0.5f ? clamp((int)floor(profileIndex + 0.5f), 0, 7) : 0;
        }

        void BurtSSSDecodeProfileIDAndType(float encodedData, out uint profileIndex, out uint type)
        {
            uint encoded = (uint)floor(saturate(encodedData) * 255.0f + 0.5f);
            profileIndex = encoded & BURT_SSS_PROFILE_ID_MASK;
            type = encoded & BURT_SSS_PROFILE_TYPE_MASK;
        }

        float BurtSSSLoadProfileIDAndType(float2 uv)
        {
            return tex2D(_BurtSSSProfileIDAndTypeTexture, uv).r;
        }

        bool BurtSSSUseProfileParamLut()
        {
            return _BurtSubsurfaceProfileParamLutEnabled > 0.5f && _BurtSubsurfaceProfileParamLutSize.x > 1.0f && _BurtSubsurfaceProfileParamLutSize.y > 0.5f;
        }

        float4 BurtSSSFetchProfileParam(float sampleIndex, float profileIndex)
        {
            int width = max((int)_BurtSubsurfaceProfileParamLutSize.x, 1);
            int height = max((int)_BurtSubsurfaceProfileParamLutSize.y, 1);
            int sample = clamp((int)floor(sampleIndex + 0.5f), 0, width - 1);
            int profile = clamp(BurtSSSResolveProfileIndex(profileIndex), 0, height - 1);
            return _BurtSubsurfaceProfileParamLut.Load(int3(sample, profile, 0));
        }

        BurtSSSProfile BurtSSSLoadProfile(float profileIndex)
        {
            int index = BurtSSSResolveProfileIndex(profileIndex);
            BurtSSSProfile profile;
            profile.params = _BurtSSSProfileParams[index];
            profile.params2 = _BurtSSSProfileParams2[index];
            profile.surfaceAlbedo = _BurtSSSProfileSurfaceAlbedos[index];
            profile.meanFreePath = _BurtSSSProfileMeanFreePaths[index];
            profile.tint = _BurtSSSProfileTints[index];
            profile.boundaryColorBleed = _BurtSSSProfileBoundaryColorBleeds[index];
            profile.transmission = _BurtSSSProfileTransmissions[index];
            profile.transmissionTint = _BurtSSSProfileTransmissionTints[index];
            if (BurtSSSUseProfileParamLut())
            {
                profile.surfaceAlbedo = BurtSSSFetchProfileParam(BURT_SSS_PROFILE_PARAM_SURFACE_ALBEDO_OFFSET, profileIndex);
                profile.meanFreePath = BurtSSSFetchProfileParam(BURT_SSS_PROFILE_PARAM_MEAN_FREE_PATH_OFFSET, profileIndex);
                profile.tint = BurtSSSFetchProfileParam(BURT_SSS_PROFILE_PARAM_TINT_OFFSET, profileIndex);
                profile.boundaryColorBleed = BurtSSSFetchProfileParam(BURT_SSS_PROFILE_PARAM_BOUNDARY_COLOR_BLEED_OFFSET, profileIndex);
                profile.transmission = BurtSSSFetchProfileParam(BURT_SSS_PROFILE_PARAM_TRANSMISSION_OFFSET, profileIndex);
                profile.transmissionTint = profile.tint;
            }
            return profile;
        }

        float BurtSSSDecodeExtinctionScale(float encodedExtinctionScale)
        {
            return max(encodedExtinctionScale * BURT_SSS_EXTINCTION_DECODE_SCALE, 0.01f);
        }

        float BurtSSSDecodeScatteringDistribution(float encodedScatteringDistribution)
        {
            return clamp(encodedScatteringDistribution * 2.0f - 1.0f, -0.99f, 0.99f);
        }

        float3 BurtSSSSplitDiffuseLighting(float4 litColor)
        {
            float combinedLuminance = dot(max(litColor.rgb, float3(0.0f, 0.0f, 0.0f)), BURT_LUMINANCE_WEIGHTS);
            float diffuseFactor = combinedLuminance > 0.0001f ? saturate(max(litColor.a, 0.0f) / combinedLuminance) : 0.0f;
            return max(litColor.rgb, float3(0.0f, 0.0f, 0.0f)) * diffuseFactor;
        }

        float3 BurtSSSDecodeSourceDiffuse(float4 sourceColor, float sourceIsLit)
        {
            return sourceIsLit > 0.5f ? BurtSSSSplitDiffuseLighting(sourceColor) : sourceColor.rgb;
        }

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
            surface.profileIndex = BurtGetSubsurfaceProfileIndex(data);
            BurtSSSProfile profile = BurtSSSLoadProfile(surface.profileIndex);
            bool gbufferValid = BurtIsSubsurfaceShadingModel(data.shadingModelID) && !BurtSSSIsSkyDepth(surface.rawDepth) && surface.strength > profile.params.w;
            uint profileIDFromTexture;
            uint profileTypeFromTexture;
            BurtSSSDecodeProfileIDAndType(BurtSSSLoadProfileIDAndType(uv), profileIDFromTexture, profileTypeFromTexture);
            if ((profileTypeFromTexture & BURT_SSS_PROFILE_TYPE_BURLEY) != 0u)
            {
                surface.profileIndex = (float)clamp((int)profileIDFromTexture, 0, 7);
                surface.profileType = profileTypeFromTexture;
                surface.valid = gbufferValid ? 1.0f : 0.0f;
            }
            else
            {
                surface.profileType = gbufferValid ? BURT_SSS_PROFILE_TYPE_BURLEY : 0u;
                surface.valid = gbufferValid ? 1.0f : 0.0f;
            }
            return surface;
        }

        float BurtSSSInBounds(float2 uv)
        {
            float2 insideMin = step(float2(0.0f, 0.0f), uv);
            float2 insideMax = step(uv, float2(1.0f, 1.0f));
            return insideMin.x * insideMin.y * insideMax.x * insideMax.y;
        }

        float4 BurtSSSEncodeSetup(BurtSSSSurface surface)
        {
            float visibleProfile = saturate(surface.profileIndex / 7.0f);
            return float4(surface.valid, saturate(surface.strength), visibleProfile, saturate(surface.thickness));
        }

        float4 BurtSSSLoadSetup(float2 uv)
        {
            return tex2D(_BurtSSSSetupTexture, uv);
        }

        float2 BurtSSSGetTileUV(float2 uv)
        {
            float2 tileSize = max(_BurtSSSTileSize.xy, float2(1.0f, 1.0f));
            float2 tileCoord = floor(saturate(uv) * _BurtSSSScreenSize.xy / 8.0f);
            return (clamp(tileCoord, float2(0.0f, 0.0f), tileSize - 1.0f) + 0.5f) * _BurtSSSTileSize.zw;
        }

        float BurtSSSLoadTileMask(float2 uv)
        {
            return tex2D(_BurtSSSTileTexture, BurtSSSGetTileUV(uv)).r;
        }

        float BurtSSSSampleWeight(BurtSSSSurface center, BurtSSSSurface sampleSurface, BurtSSSProfile profile, float inBounds)
        {
            float modelWeight = center.valid * sampleSurface.valid;
            float depthWindow = max(profile.params.y * max(center.linearEyeDepth, 0.05f), 0.0001f);
            float depthWeight = exp2(-abs(center.linearEyeDepth - sampleSurface.linearEyeDepth) / depthWindow);
            float normalWeight = saturate(dot(center.normalWS, sampleSurface.normalWS));
            normalWeight = pow(normalWeight, max(profile.params.z * 24.0f, 1.0f));
            float thicknessWeight = saturate(1.0f - abs(center.thickness - sampleSurface.thickness) * 2.5f);
            float strengthWeight = saturate(1.0f - abs(center.strength - sampleSurface.strength) * 2.0f);
            float profileWeight = BurtSSSResolveProfileIndex(center.profileIndex) == BurtSSSResolveProfileIndex(sampleSurface.profileIndex) ? 1.0f : 0.0f;
            return inBounds * modelWeight * profileWeight * depthWeight * normalWeight * thicknessWeight * strengthWeight;
        }

        float BurtSSSResolveRadius(BurtSSSSurface center, BurtSSSProfile profile)
        {
            float distanceFade = rsqrt(1.0f + center.linearEyeDepth * max(profile.params2.y, 0.01f));
            float extinctionRadiusScale = clamp(rsqrt(BurtSSSDecodeExtinctionScale(profile.transmission.x)), 0.35f, 2.0f);
            float radius = profile.params.x * profile.meanFreePath.w * extinctionRadiusScale * lerp(0.35f, 1.85f, center.thickness) * lerp(0.25f, 1.0f, center.strength) * distanceFade;
            radius = clamp(radius, 0.0f, 16.0f);
            return radius > 0.25f ? floor(radius * 4.0f + 0.5f) * 0.25f : radius;
        }

        float BurtSSSSurfaceStability(BurtSSSSurface center, BurtSSSProfile profile, float2 uv)
        {
            if (center.valid <= 0.0f)
            {
                return 0.0f;
            }

            float tileMask = BurtSSSLoadTileMask(uv);
            float2 texel = _BurtSSSScreenSize.zw;
            BurtSSSSurface right = BurtSSSLoadSurface(saturate(uv + float2(texel.x, 0.0f)));
            BurtSSSSurface up = BurtSSSLoadSurface(saturate(uv + float2(0.0f, texel.y)));
            float horizontal = BurtSSSSampleWeight(center, right, profile, 1.0f);
            float vertical = BurtSSSSampleWeight(center, up, profile, 1.0f);
            return saturate(tileMask * min(horizontal, vertical));
        }

        float3 BurtSSSProfileKernelScale(BurtSSSSurface center, BurtSSSProfile profile, float offset)
        {
            float3 meanFreePath = max(profile.meanFreePath.rgb, float3(0.0001f, 0.0001f, 0.0001f));
            float maxMeanFreePath = max(max(meanFreePath.r, meanFreePath.g), meanFreePath.b);
            float3 normalizedMeanFreePath = meanFreePath / max(maxMeanFreePath, 0.0001f);
            float3 spread = lerp(float3(1.0f, 1.0f, 1.0f), normalizedMeanFreePath, saturate(center.strength));
            float3 channelFalloff = max(1.0f / max(spread, float3(0.0001f, 0.0001f, 0.0001f)) - 1.0f, float3(0.0f, 0.0f, 0.0f));
            float3 separableScale = exp2(-abs(offset) * channelFalloff * 0.28f);
            float extinctionScale = BurtSSSDecodeExtinctionScale(profile.transmission.x);
            float scatteringDistribution = saturate(BurtSSSDecodeScatteringDistribution(profile.transmission.z));
            float3 surfaceAlbedo = saturate(profile.surfaceAlbedo.rgb);
            float3 searchLightScale = 3.5f + 100.0f * pow(abs(surfaceAlbedo - 0.33f), 4.0f);
            float3 profileDistance = max(meanFreePath * max(profile.meanFreePath.w, 0.05f), float3(0.0001f, 0.0001f, 0.0001f));
            float3 burleyA = exp2(-1.442695f * abs(offset) * extinctionScale / max(searchLightScale * profileDistance, float3(0.001f, 0.001f, 0.001f)));
            float3 burleyB = exp2(-1.442695f * abs(offset) * extinctionScale / max(searchLightScale * profileDistance * 3.0f, float3(0.001f, 0.001f, 0.001f)));
            float3 burleyScale = max(surfaceAlbedo * (burleyA + burleyB) * 0.5f, float3(0.0001f, 0.0001f, 0.0001f));
            return max(lerp(separableScale, burleyScale, saturate(center.strength * scatteringDistribution * 0.55f)), float3(0.0001f, 0.0001f, 0.0001f));
        }

        void BurtSSSAccumulateSample(
            BurtSSSSurface center,
            BurtSSSProfile profile,
            float3 original,
            float2 uv,
            float2 texelStep,
            float offset,
            float3 kernelWeight,
            float sourceIsLit,
            inout float3 sumColor,
            inout float3 sumWeight)
        {
            float2 sampleUVUnclamped = uv + texelStep * offset;
            float inBounds = BurtSSSInBounds(sampleUVUnclamped);
            float2 sampleUV = saturate(sampleUVUnclamped);
            BurtSSSSurface sampleSurface = BurtSSSLoadSurface(sampleUV);
            float scalarWeight = BurtSSSSampleWeight(center, sampleSurface, profile, inBounds);
            float3 weight = kernelWeight * scalarWeight;
            float3 boundaryColor = original * max(profile.boundaryColorBleed.rgb, float3(0.0f, 0.0f, 0.0f));
            float sameProfile = BurtSSSResolveProfileIndex(center.profileIndex) == BurtSSSResolveProfileIndex(sampleSurface.profileIndex) ? 1.0f : 0.0f;
            float sampleHasSource = inBounds * center.valid * sampleSurface.valid * sameProfile;
            float3 sampleColor = sampleHasSource > 0.5f ? BurtSSSDecodeSourceDiffuse(tex2D(_BurtSSSSourceTexture, sampleUV), sourceIsLit) : boundaryColor;
            float boundaryBlend = sampleHasSource > 0.5f ? saturate((1.0f - scalarWeight) * profile.params2.z) : 1.0f;
            sampleColor = lerp(sampleColor, boundaryColor, boundaryBlend);
            float3 weightedKernel = weight * BurtSSSProfileKernelScale(center, profile, offset);
            sumColor += sampleColor * weightedKernel;
            sumWeight += weightedKernel;
        }

        void BurtSSSAccumulatePair(
            BurtSSSSurface center,
            BurtSSSProfile profile,
            float3 original,
            float2 uv,
            float2 texelStep,
            float offset,
            float3 kernelWeight,
            float sourceIsLit,
            inout float3 sumColor,
            inout float3 sumWeight)
        {
            BurtSSSAccumulateSample(center, profile, original, uv, texelStep, offset, kernelWeight, sourceIsLit, sumColor, sumWeight);
            BurtSSSAccumulateSample(center, profile, original, uv, texelStep, -offset, kernelWeight, sourceIsLit, sumColor, sumWeight);
        }

        void BurtSSSAccumulate5SKernel(
            BurtSSSSurface center,
            BurtSSSProfile profile,
            float3 original,
            float2 uv,
            float2 texelStep,
            float sourceIsLit,
            inout float3 sumColor,
            inout float3 sumWeight)
        {
            if (BurtSSSUseProfileParamLut())
            {
                [unroll]
                for (int i = 1; i < 13; i++)
                {
                    float4 kernel = BurtSSSFetchProfileParam(BURT_SSS_PROFILE_PARAM_KERNEL0_OFFSET + (float)i, center.profileIndex);
                    BurtSSSAccumulatePair(center, profile, original, uv, texelStep, kernel.a, max(kernel.rgb, float3(0.0001f, 0.0001f, 0.0001f)), sourceIsLit, sumColor, sumWeight);
                }
                return;
            }

            BurtSSSAccumulatePair(center, profile, original, uv, texelStep, 0.22f, float3(0.150f, 0.165f, 0.168f), sourceIsLit, sumColor, sumWeight);
            BurtSSSAccumulatePair(center, profile, original, uv, texelStep, 0.46f, float3(0.118f, 0.123f, 0.114f), sourceIsLit, sumColor, sumWeight);
            BurtSSSAccumulatePair(center, profile, original, uv, texelStep, 0.78f, float3(0.090f, 0.088f, 0.074f), sourceIsLit, sumColor, sumWeight);
            BurtSSSAccumulatePair(center, profile, original, uv, texelStep, 1.16f, float3(0.066f, 0.058f, 0.043f), sourceIsLit, sumColor, sumWeight);
            BurtSSSAccumulatePair(center, profile, original, uv, texelStep, 1.60f, float3(0.047f, 0.036f, 0.023f), sourceIsLit, sumColor, sumWeight);
            BurtSSSAccumulatePair(center, profile, original, uv, texelStep, 2.12f, float3(0.032f, 0.021f, 0.012f), sourceIsLit, sumColor, sumWeight);
            BurtSSSAccumulatePair(center, profile, original, uv, texelStep, 2.74f, float3(0.021f, 0.012f, 0.006f), sourceIsLit, sumColor, sumWeight);
            BurtSSSAccumulatePair(center, profile, original, uv, texelStep, 3.48f, float3(0.013f, 0.006f, 0.003f), sourceIsLit, sumColor, sumWeight);
            BurtSSSAccumulatePair(center, profile, original, uv, texelStep, 4.36f, float3(0.008f, 0.003f, 0.0015f), sourceIsLit, sumColor, sumWeight);
            BurtSSSAccumulatePair(center, profile, original, uv, texelStep, 5.42f, float3(0.0045f, 0.0015f, 0.0007f), sourceIsLit, sumColor, sumWeight);
            BurtSSSAccumulatePair(center, profile, original, uv, texelStep, 6.68f, float3(0.0025f, 0.0007f, 0.0003f), sourceIsLit, sumColor, sumWeight);
            BurtSSSAccumulatePair(center, profile, original, uv, texelStep, 8.16f, float3(0.0015f, 0.0003f, 0.0001f), sourceIsLit, sumColor, sumWeight);
        }

        void BurtSSSAccumulateProfileKernelRange(
            BurtSSSSurface center,
            BurtSSSProfile profile,
            float3 original,
            float2 uv,
            float2 texelStep,
            float sourceIsLit,
            float kernelOffset,
            int kernelSize,
            float layerRadiusScale,
            float layerWeightScale,
            int sampleStride,
            inout float3 sumColor,
            inout float3 sumWeight)
        {
            if (layerWeightScale <= 0.0001f)
            {
                return;
            }

            [loop]
            for (int i = 1; i < kernelSize; i += sampleStride)
            {
                float4 kernel = BurtSSSFetchProfileParam(kernelOffset + (float)i, center.profileIndex);
                float3 kernelWeight = max(kernel.rgb * layerWeightScale, float3(0.0001f, 0.0001f, 0.0001f));
                BurtSSSAccumulatePair(center, profile, original, uv, texelStep, kernel.a * layerRadiusScale, kernelWeight, sourceIsLit, sumColor, sumWeight);
            }
        }

        void BurtSSSAccumulateLayeredProfileKernels(
            BurtSSSSurface center,
            BurtSSSProfile profile,
            float3 original,
            float2 uv,
            float2 texelStep,
            float sourceIsLit,
            inout float3 sumColor,
            inout float3 sumWeight)
        {
            float thickness = saturate(center.thickness);
            float strength = saturate(center.strength);
            float mediumLayer = saturate((strength - 0.18f) * 1.65f) * saturate(thickness * 1.45f);
            float farLayer = saturate((strength - 0.38f) * 1.45f) * saturate((thickness - 0.15f) * 1.35f);

            BurtSSSAccumulateProfileKernelRange(
                center,
                profile,
                original,
                uv,
                texelStep,
                sourceIsLit,
                BURT_SSS_PROFILE_PARAM_KERNEL0_OFFSET,
                13,
                1.0f,
                1.0f,
                1,
                sumColor,
                sumWeight);

            BurtSSSAccumulateProfileKernelRange(
                center,
                profile,
                original,
                uv,
                texelStep,
                sourceIsLit,
                BURT_SSS_PROFILE_PARAM_KERNEL1_OFFSET,
                9,
                1.55f,
                mediumLayer * 0.42f,
                2,
                sumColor,
                sumWeight);

            BurtSSSAccumulateProfileKernelRange(
                center,
                profile,
                original,
                uv,
                texelStep,
                sourceIsLit,
                BURT_SSS_PROFILE_PARAM_KERNEL2_OFFSET,
                6,
                2.35f,
                farLayer * 0.24f,
                2,
                sumColor,
                sumWeight);
        }

        float3 BurtSSSPreserveDiffuseLuminance(float3 original, float3 blurred, float strength)
        {
            float originalLum = dot(max(original, float3(0.0f, 0.0f, 0.0f)), BURT_LUMINANCE_WEIGHTS);
            float blurredLum = dot(max(blurred, float3(0.0f, 0.0f, 0.0f)), BURT_LUMINANCE_WEIGHTS);
            float safeRatio = originalLum > 0.0001f ? originalLum / max(blurredLum, 0.0001f) : 1.0f;
            float energyRatio = lerp(1.0f, clamp(safeRatio, 0.58f, 1.28f), saturate(strength * 0.82f));
            return max(blurred * energyRatio, float3(0.0f, 0.0f, 0.0f));
        }

        float3 BurtSSSBlur(float2 uv, float2 direction, float applyTint, float sourceIsLit)
        {
            BurtSSSSurface center = BurtSSSLoadSurface(uv);
            float3 original = BurtSSSDecodeSourceDiffuse(tex2D(_BurtSSSSourceTexture, uv), sourceIsLit);
            if (center.valid <= 0.0f)
            {
                return original;
            }

            BurtSSSProfile profile = BurtSSSLoadProfile(center.profileIndex);
            float radius = BurtSSSResolveRadius(center, profile);
            float2 texelStep = direction * _BurtSSSScreenSize.zw * radius;
            float3 centerWeight = BurtSSSUseProfileParamLut()
                ? max(BurtSSSFetchProfileParam(BURT_SSS_PROFILE_PARAM_KERNEL0_OFFSET, center.profileIndex).rgb, float3(0.0001f, 0.0001f, 0.0001f))
                : BURT_5S_KERNEL_CENTER;
            float3 sumColor = original * centerWeight;
            float3 sumWeight = centerWeight;
            if (BurtSSSUseProfileParamLut())
            {
                BurtSSSAccumulateLayeredProfileKernels(center, profile, original, uv, texelStep, sourceIsLit, sumColor, sumWeight);
            }
            else
            {
                BurtSSSAccumulate5SKernel(center, profile, original, uv, texelStep, sourceIsLit, sumColor, sumWeight);
            }

            float3 blurred = sumColor / max(sumWeight, float3(0.0001f, 0.0001f, 0.0001f));
            float3 materialTint = max(center.tint, float3(0.0f, 0.0f, 0.0f));
            float3 profileTint = max(profile.tint.rgb, float3(0.0f, 0.0f, 0.0f));
            float3 transmissionTint = max(profile.transmissionTint.rgb, float3(0.0f, 0.0f, 0.0f));
            float3 profileAlbedo = max(profile.surfaceAlbedo.rgb, float3(0.0f, 0.0f, 0.0f));
            float3 tint = lerp(float3(1.0f, 1.0f, 1.0f), materialTint * profileTint * transmissionTint * lerp(float3(1.0f, 1.0f, 1.0f), profileAlbedo, 0.35f), profile.params2.w);
            float3 tintedBlurred = blurred * lerp(float3(1.0f, 1.0f, 1.0f), tint, saturate(applyTint));
            return BurtSSSPreserveDiffuseLuminance(original, tintedBlurred, center.strength);
        }

        float4 FragCopy(Varyings input) : SV_Target
        {
            return tex2D(_BurtCameraColorTexture, input.screenUV);
        }

        float4 FragHorizontal(Varyings input) : SV_Target
        {
            float setupMask = BurtSSSLoadSetup(input.screenUV).r;
            float tileMask = BurtSSSLoadTileMask(input.screenUV);
            if (setupMask <= 0.0f || tileMask <= 0.0f)
            {
                return tex2D(_BurtSSSSourceTexture, input.screenUV);
            }

            return float4(BurtSSSBlur(input.screenUV, float2(1.0f, 0.0f), 0.0f, 1.0f), 1.0f);
        }

        float4 FragVertical(Varyings input) : SV_Target
        {
            float setupMask = BurtSSSLoadSetup(input.screenUV).r;
            float tileMask = BurtSSSLoadTileMask(input.screenUV);
            if (setupMask <= 0.0f || tileMask <= 0.0f)
            {
                return tex2D(_BurtSSSOriginalTexture, input.screenUV);
            }

            float3 blurred = BurtSSSBlur(input.screenUV, float2(0.0f, 1.0f), 1.0f, 0.0f);
            float4 originalLit = tex2D(_BurtSSSOriginalTexture, input.screenUV);
            float3 original = BurtSSSSplitDiffuseLighting(originalLit);
            float3 specularAndEmission = max(originalLit.rgb - original, float3(0.0f, 0.0f, 0.0f));
            BurtSSSSurface center = BurtSSSLoadSurface(input.screenUV);
            BurtSSSProfile profile = BurtSSSLoadProfile(center.profileIndex);
            float stability = BurtSSSSurfaceStability(center, profile, input.screenUV);
            float blend = saturate(center.strength * profile.params2.x * tileMask * lerp(0.55f, 1.0f, stability));
            float3 diffuse = center.valid > 0.0f ? lerp(original, blurred, blend) : original;
            return float4(diffuse + specularAndEmission, 1.0f);
        }

        float4 FragSetup(Varyings input) : SV_Target
        {
            BurtSSSSurface surface = BurtSSSLoadSurface(input.screenUV);
            return BurtSSSEncodeSetup(surface);
        }

        float4 FragTile(Varyings input) : SV_Target
        {
            float2 fullTexel = _BurtSSSScreenSize.zw;
            float2 tileCenter = (floor(input.screenUV * _BurtSSSTileSize.xy) + 0.5f) / _BurtSSSTileSize.xy;
            float mask = 0.0f;
            [unroll]
            for (int y = -1; y <= 1; y++)
            {
                [unroll]
                for (int x = -1; x <= 1; x++)
                {
                    float2 sampleUV = saturate(tileCenter + float2((float)x, (float)y) * fullTexel * 8.0f);
                    mask = max(mask, BurtSSSLoadSetup(sampleUV).r);
                }
            }

            return float4(mask, mask, mask, 1.0f);
        }

        float4 FragMask(Varyings input) : SV_Target
        {
            return float4(1.0f, 1.0f, 1.0f, 1.0f);
        }

        float4 FragCombine(Varyings input) : SV_Target
        {
            float4 originalLit = tex2D(_BurtSSSOriginalTexture, input.screenUV);
            float3 subsurfaceDiffuse = tex2D(_BurtCameraColorTexture, input.screenUV).rgb;
            float setupMask = BurtSSSLoadSetup(input.screenUV).r;
            float tileMask = BurtSSSLoadTileMask(input.screenUV);
            float mixWeight = saturate(setupMask * tileMask);
            float3 originalDiffuse = BurtSSSSplitDiffuseLighting(originalLit);
            float3 specularAndEmission = max(originalLit.rgb - originalDiffuse, float3(0.0f, 0.0f, 0.0f));
            float profileIndex = BurtSSSLoadSetup(input.screenUV).b * 7.0f;
            BurtSSSProfile profile = BurtSSSLoadProfile(profileIndex);
            float3 profileTint = saturate(profile.tint.rgb);
            float3 subsurfaceLighting = lerp(originalDiffuse, subsurfaceDiffuse, profileTint);
            float3 finalDiffuse = lerp(originalDiffuse, subsurfaceLighting, mixWeight);
            return float4(finalDiffuse + specularAndEmission, 1.0f);
        }

        float4 FragDebug(Varyings input) : SV_Target
        {
            float4 setup = BurtSSSLoadSetup(input.screenUV);
            if (_BurtSSSDebugMode < 1.5f)
            {
                return float4(setup.r, setup.g, setup.b, 1.0f);
            }

            if (_BurtSSSDebugMode < 2.5f)
            {
                float mask = tex2D(_BurtSSSMaskTexture, input.screenUV).r;
                return float4(mask, mask, mask, 1.0f);
            }

            if (_BurtSSSDebugMode < 3.5f)
            {
                float tileMask = BurtSSSLoadTileMask(input.screenUV);
                return float4(tileMask, tileMask, tileMask, 1.0f);
            }

            if (_BurtSSSDebugMode < 4.5f)
            {
                return float4(max(tex2D(_BurtSSSBlurTexture, input.screenUV).rgb, float3(0.0f, 0.0f, 0.0f)), 1.0f);
            }

            if (_BurtSSSDebugMode < 5.5f)
            {
                return float4(max(tex2D(_BurtSSSCombineTexture, input.screenUV).rgb, float3(0.0f, 0.0f, 0.0f)), 1.0f);
            }

            if (_BurtSSSDebugMode < 6.5f)
            {
                return float4(setup.a, setup.a, setup.a, 1.0f);
            }

            if (_BurtSSSDebugMode < 7.5f)
            {
                float profileIndex = saturate(setup.b);
                return float4(profileIndex, profileIndex, profileIndex, 1.0f);
            }

            BurtSSSSurface center = BurtSSSLoadSurface(input.screenUV);
            BurtSSSProfile profile = BurtSSSLoadProfile(center.profileIndex);
            float4 originalLit = tex2D(_BurtSSSOriginalTexture, input.screenUV);
            float3 diffuse = BurtSSSSplitDiffuseLighting(originalLit);

            if (_BurtSSSDebugMode < 8.5f)
            {
                float3 transmission = max(profile.transmissionTint.rgb, float3(0.0f, 0.0f, 0.0f)) * saturate(profile.transmission.y) * saturate(center.thickness);
                return float4(transmission * setup.r, 1.0f);
            }

            if (_BurtSSSDebugMode < 9.5f)
            {
                return float4(diffuse, 1.0f);
            }

            if (_BurtSSSDebugMode < 10.5f)
            {
                return float4(max(originalLit.rgb - diffuse, float3(0.0f, 0.0f, 0.0f)), 1.0f);
            }

            float stability = BurtSSSSurfaceStability(center, profile, input.screenUV);
            if (_BurtSSSDebugMode < 11.5f)
            {
                return float4(stability, stability, stability, 1.0f);
            }

            float4 history = tex2D(_BurtSSSHistoryDebugTexture, input.screenUV);
            float historyValid = saturate(_BurtSSSHistoryDebugParams.x);
            if (_BurtSSSDebugMode < 12.5f)
            {
                float sampleRatio = saturate(history.r) * historyValid;
                return float4(sampleRatio, sampleRatio, sampleRatio, 1.0f);
            }

            if (_BurtSSSDebugMode < 13.5f)
            {
                float variance = saturate(history.b / max(_BurtSSSHistoryDebugParams.w * 32.0f, 0.000001f)) * historyValid;
                return float4(variance, variance, variance, 1.0f);
            }

            float historyAge = saturate(_BurtSSSHistoryDebugParams.y / 64.0f) * historyValid;
            return float4(historyValid, historyAge, saturate(history.a * 16.0f), 1.0f);
        }
        ENDHLSL

        Pass
        {
            Name "Burt Screen Space Subsurface Copy"
            Cull Off
            ZWrite Off
            ZTest Always
            Stencil
            {
                Ref 3
                ReadMask 3
                Comp Equal
                Pass Keep
            }

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
            Stencil
            {
                Ref 3
                ReadMask 3
                Comp Equal
                Pass Keep
            }

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
            Stencil
            {
                Ref 3
                ReadMask 3
                Comp Equal
                Pass Keep
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragVertical
            ENDHLSL
        }

        Pass
        {
            Name "Burt Screen Space Subsurface Setup"
            Cull Off
            ZWrite Off
            ZTest Always
            Stencil
            {
                Ref 3
                ReadMask 3
                Comp Equal
                Pass Keep
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragSetup
            ENDHLSL
        }

        Pass
        {
            Name "Burt Screen Space Subsurface Tile"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragTile
            ENDHLSL
        }

        Pass
        {
            Name "Burt Screen Space Subsurface Combine"
            Cull Off
            ZWrite Off
            ZTest Always
            Stencil
            {
                Ref 3
                ReadMask 3
                Comp Equal
                Pass Keep
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragCombine
            ENDHLSL
        }

        Pass
        {
            Name "Burt Screen Space Subsurface Debug"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDebug
            ENDHLSL
        }

        Pass
        {
            Name "Burt Screen Space Subsurface Mask"
            Cull Off
            ZWrite Off
            ZTest Always
            Stencil
            {
                Ref 3
                ReadMask 3
                Comp Equal
                Pass Keep
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragMask
            ENDHLSL
        }
    }
}
