Shader "Hidden/BurtRP/ScreenSpaceSubsurface"
{
    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        HLSLINCLUDE
        #pragma target 3.5
        #define BURT_SUBSURFACE_DEFERRED_POSTPROCESS_INPUT 1
        #include "UnityCG.cginc"
        #include "ShaderLibrary/Core/BurtPreExposure.hlsl"
        #include "ShaderLibrary/BurtDeferred.hlsl"

        sampler2D _BurtCameraColorTexture;
        sampler2D _BurtSSSSourceTexture;
        sampler2D _BurtSSSSeparableInputTexture;
        sampler2D _BurtSSSOriginalTexture;
        sampler2D _BurtSSSSetupTexture;
        Texture2D<float> _BurtSSSProfileIDAndTypeTexture;
        sampler2D _BurtSSSMaskTexture;
        sampler2D _BurtSSSTempTexture;
        sampler2D _BurtSSSBlurTexture;
        sampler2D _BurtSSSCombineTexture;
        sampler2D _BurtSSSHistoryDebugTexture;
        sampler2D _BurtScreenSpaceSubsurfaceBaseColorTexture;
        sampler2D _BurtScreenSpaceSubsurfaceEmissionTexture;
        float4 _BurtSSSScreenSize;
        float4 _BurtSSSProjectionParams; // x=XRender SSS scale (m00 / kernel size * 0.5), y=projection m00, z=kernel size, w=unused
        float4 _BurtSSSFrameParams; // x=frame index, y=wrapped frame index, z=stable sampling, w=debug sampling.
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
        static const float3 BURT_SSS_FALLBACK_KERNEL_CENTER = float3(0.204f, 0.236f, 0.290f);
        static const float3 BURT_LUMINANCE_WEIGHTS = float3(0.3f, 0.59f, 0.11f);
        static const float BURT_SSS_EXTINCTION_DECODE_SCALE = 100.0f;
        static const float BURT_SSS_DEFAULT_RADIUS_PIXELS = 3.25f;
        static const float BURT_SSS_SUBSURFACE_RADIUS_SCALE = 1024.0f;
        static const float BURT_SSS_PROFILE_PARAM_SURFACE_ALBEDO_OFFSET = 0.0f;
        static const float BURT_SSS_PROFILE_PARAM_MEAN_FREE_PATH_OFFSET = 1.0f;
        static const float BURT_SSS_PROFILE_PARAM_TINT_OFFSET = 2.0f;
        static const float BURT_SSS_PROFILE_PARAM_BOUNDARY_COLOR_BLEED_OFFSET = 3.0f;
        static const float BURT_SSS_PROFILE_PARAM_TRANSMISSION_OFFSET = 5.0f;
        static const float BURT_SSS_PROFILE_PARAM_KERNEL0_OFFSET = 38.0f;
        static const float BURT_SSS_PROFILE_PARAM_KERNEL0_SIZE = 13.0f;
        static const int BURT_SSS_PROFILE_PARAM_KERNEL0_COUNT = 13;
        static const float BURT_SSS_PROFILE_PARAM_KERNEL1_OFFSET = 51.0f;
        static const float BURT_SSS_PROFILE_PARAM_KERNEL1_SIZE = 9.0f;
        static const int BURT_SSS_PROFILE_PARAM_KERNEL1_COUNT = 9;
        static const float BURT_SSS_PROFILE_PARAM_KERNEL2_OFFSET = 60.0f;
        static const float BURT_SSS_PROFILE_PARAM_KERNEL2_SIZE = 6.0f;
        static const int BURT_SSS_PROFILE_PARAM_KERNEL2_COUNT = 6;
        static const uint BURT_SSS_PROFILE_TYPE_BURLEY = 0x40u;
        static const uint BURT_SSS_PROFILE_TYPE_SEPARABLE = 0x80u;
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

        int2 BurtSSSClampPixel(float2 uv)
        {
            float2 pixel = floor(saturate(uv) * _BurtSSSScreenSize.xy - 0.5f);
            return (int2)clamp(pixel, float2(0.0f, 0.0f), _BurtSSSScreenSize.xy - 1.0f);
        }

        float BurtSSSLoadProfileIDAndType(float2 uv)
        {
            int2 pixel = BurtSSSClampPixel(uv);
            return _BurtSSSProfileIDAndTypeTexture.Load(int3(pixel.x, pixel.y, 0));
        }

        float BurtSSSLoadProfileIDAndTypePoint(float2 uv)
        {
            int2 pixel = (int2)floor(uv * _BurtSSSScreenSize.xy - 0.5f);
            int2 screenSize = max((int2)_BurtSSSScreenSize.xy, int2(1, 1));
            if (pixel.x < 0 || pixel.y < 0 || pixel.x >= screenSize.x || pixel.y >= screenSize.y)
            {
                return 0.0f;
            }

            return _BurtSSSProfileIDAndTypeTexture.Load(int3(pixel.x, pixel.y, 0));
        }

        float BurtSSSLoadMaterialEncodedProfileIDAndType(float2 uv)
        {
            return tex2D(_BurtScreenSpaceSubsurfaceBaseColorTexture, uv).a;
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

        float BurtSSSSourceDiffuseFactor(float4 litColor)
        {
            float combinedLuminance = dot(max(litColor.rgb, float3(0.0f, 0.0f, 0.0f)), BURT_LUMINANCE_WEIGHTS);
            return combinedLuminance > 0.0001f ? saturate(max(litColor.a, 0.0f) / combinedLuminance) : 0.0f;
        }

        float3 BurtSSSSplitDiffuseLighting(float4 litColor)
        {
            return max(litColor.rgb, float3(0.0f, 0.0f, 0.0f)) * BurtSSSSourceDiffuseFactor(litColor);
        }

        float3 BurtSSSLoadBaseColor(float2 uv)
        {
            return max(tex2D(_BurtScreenSpaceSubsurfaceBaseColorTexture, uv).rgb, float3(0.0f, 0.0f, 0.0f));
        }

        float3 BurtSSSLoadPreExposedEmission(float2 uv)
        {
            return max(tex2D(_BurtScreenSpaceSubsurfaceEmissionTexture, uv).rgb, float3(0.0f, 0.0f, 0.0f)) * max(_BurtPreExposure, 0.0f);
        }

        float3 BurtSSSDecodeDiffuseLighting(float3 diffuseWithBaseColor, float3 baseColor)
        {
            return max(diffuseWithBaseColor, float3(0.0f, 0.0f, 0.0f));
        }

        float3 BurtSSSSeparableFailureColor(float2 uv, float4 setup, BurtSSSSurface center, float sourceDepth)
        {
            BurtEncodedGBuffer encoded = BurtSampleEncodedGBuffer(uv);
            BurtGBufferData data = BurtDecodeGBuffer(encoded);
            uint materialProfileIndex;
            uint materialProfileType;
            BurtSSSDecodeProfileIDAndType(BurtSSSLoadMaterialEncodedProfileIDAndType(uv), materialProfileIndex, materialProfileType);
            float gbufferSubsurface = BurtIsSubsurfaceShadingModel(data.shadingModelID) && !BurtSSSIsSkyDepth(center.rawDepth) ? 1.0f : 0.0f;
            float materialSeparable = (materialProfileType & BURT_SSS_PROFILE_TYPE_SEPARABLE) != 0u ? 1.0f : 0.0f;
            float depthValid = sourceDepth > 0.0f ? 1.0f : 0.0f;
            return float3(max(setup.r, gbufferSubsurface * 0.35f), materialSeparable, depthValid);
        }

        float4 BurtSSSSeparableChainColor(float2 uv, float4 setup, BurtSSSSurface center)
        {
            BurtEncodedGBuffer encoded = BurtSampleEncodedGBuffer(uv);
            BurtGBufferData data = BurtDecodeGBuffer(encoded);

            uint materialProfileIndex;
            uint materialProfileType;
            BurtSSSDecodeProfileIDAndType(BurtSSSLoadMaterialEncodedProfileIDAndType(uv), materialProfileIndex, materialProfileType);

            uint setupProfileIndex;
            uint setupProfileType;
            BurtSSSDecodeProfileIDAndType(BurtSSSLoadProfileIDAndType(uv), setupProfileIndex, setupProfileType);

            float materialHasAny = (materialProfileType & (BURT_SSS_PROFILE_TYPE_BURLEY | BURT_SSS_PROFILE_TYPE_SEPARABLE)) != 0u ? 0.5f : 0.0f;
            float materialIs4S = (materialProfileType & BURT_SSS_PROFILE_TYPE_SEPARABLE) != 0u ? 1.0f : materialHasAny;
            float gbufferSubsurface = BurtIsSubsurfaceShadingModel(data.shadingModelID) && !BurtSSSIsSkyDepth(center.rawDepth) ? 1.0f : 0.0f;
            float setupHasAny = (setupProfileType & (BURT_SSS_PROFILE_TYPE_BURLEY | BURT_SSS_PROFILE_TYPE_SEPARABLE)) != 0u ? 0.35f : 0.0f;
            float setupIs4S = (setupProfileType & BURT_SSS_PROFILE_TYPE_SEPARABLE) != 0u ? 0.65f : 0.0f;
            float setupMask = saturate(setup.r);
            float4 horizontal = tex2D(_BurtSSSTempTexture, uv);
            float4 vertical = tex2D(_BurtSSSBlurTexture, uv);
            float passWrites = max(horizontal.a > 0.0f ? 0.55f : 0.0f, vertical.a > 0.00001f ? 1.0f : 0.0f);
            float blue = max(max(setupMask, setupHasAny), max(setupIs4S, passWrites));
            return float4(materialIs4S, gbufferSubsurface, blue, 1.0f);
        }

        void BurtSSSDecodeLightingComponents(
            float2 uv,
            float4 source,
            out float3 baseColor,
            out float3 emission,
            out float3 diffuseWithBaseColor,
            out float3 diffuseLighting,
            out float3 specularLight)
        {
            baseColor = BurtSSSLoadBaseColor(uv);
            emission = BurtSSSLoadPreExposedEmission(uv);
            float4 sourceWithoutEmission = float4(max(source.rgb - emission, float3(0.0f, 0.0f, 0.0f)), source.a);
            diffuseWithBaseColor = BurtSSSSplitDiffuseLighting(sourceWithoutEmission);
            diffuseLighting = BurtSSSDecodeDiffuseLighting(diffuseWithBaseColor, baseColor);
            specularLight = max(sourceWithoutEmission.rgb - diffuseWithBaseColor, float3(0.0f, 0.0f, 0.0f));
        }

        float3 BurtSSSDecodeSourceDiffuse(float4 sourceColor, float sourceIsLit)
        {
            return sourceIsLit > 0.5f ? BurtSSSSplitDiffuseLighting(sourceColor) : sourceColor.rgb;
        }

        float3 BurtSSSResolveSubsurfaceDiffuseColor(float4 subsurfaceColor, float3 fallbackDiffuseLighting)
        {
            return subsurfaceColor.a > 0.00001f
                ? max(subsurfaceColor.rgb / subsurfaceColor.a, float3(0.0f, 0.0f, 0.0f))
                : max(fallbackDiffuseLighting, float3(0.0f, 0.0f, 0.0f));
        }

        float3 BurtSSSResolveProfileDiffuseColor(uint profileType, float4 subsurfaceColor, float3 fallbackDiffuseLighting)
        {
            if ((profileType & BURT_SSS_PROFILE_TYPE_SEPARABLE) != 0u)
            {
                return subsurfaceColor.a > 0.00001f
                    ? max(subsurfaceColor.rgb, float3(0.0f, 0.0f, 0.0f))
                    : max(fallbackDiffuseLighting, float3(0.0f, 0.0f, 0.0f));
            }

            return BurtSSSResolveSubsurfaceDiffuseColor(subsurfaceColor, fallbackDiffuseLighting);
        }

        float BurtSSSResolveProfileBlend(BurtSSSProfile profile, uint profileType)
        {
            if ((profileType & BURT_SSS_PROFILE_TYPE_SEPARABLE) != 0u)
            {
                return saturate(profile.params2.x);
            }

            return 1.0f;
        }

        float BurtSSSResolveWhiteProfileTintWeight(float3 profileTint)
        {
            float minTint = min(profileTint.r, min(profileTint.g, profileTint.b));
            return saturate((minTint - 0.95f) * 20.0f);
        }

        float3 BurtSSSResolveSeparableAuthoredTint(BurtSSSProfile profile)
        {
            float3 profileTint = saturate(profile.tint.rgb);
            float3 surfaceAlbedoTint = saturate(profile.surfaceAlbedo.rgb);
            return lerp(profileTint, surfaceAlbedoTint, BurtSSSResolveWhiteProfileTintWeight(profileTint));
        }

        float3 BurtSSSResolveProfileTint(BurtSSSProfile profile, uint profileType)
        {
            float3 profileTint = saturate(profile.tint.rgb);
            if ((profileType & BURT_SSS_PROFILE_TYPE_SEPARABLE) == 0u)
            {
                return profileTint;
            }

            float tintStrength = saturate(profile.params2.w);
            return lerp(float3(1.0f, 1.0f, 1.0f), BurtSSSResolveSeparableAuthoredTint(profile), tintStrength);
        }

        float3 BurtSSSResolveSeparableProfileColorScale(BurtSSSProfile profile, uint profileType)
        {
            if ((profileType & BURT_SSS_PROFILE_TYPE_SEPARABLE) == 0u)
            {
                return float3(1.0f, 1.0f, 1.0f);
            }

            float tintStrength = saturate(profile.params2.w);
            if (tintStrength <= 0.0001f)
            {
                return float3(1.0f, 1.0f, 1.0f);
            }

            float3 authoredTint = BurtSSSResolveSeparableAuthoredTint(profile);
            float authoredLuminance = max(dot(authoredTint, BURT_LUMINANCE_WEIGHTS), 0.05f);
            float3 profileColorScale = clamp(authoredTint / authoredLuminance, float3(0.35f, 0.35f, 0.35f), float3(2.35f, 2.35f, 2.35f));
            return lerp(float3(1.0f, 1.0f, 1.0f), profileColorScale, tintStrength);
        }

        float3 BurtSSSVisualizeSignedDelta(float3 current, float3 reference, float scale)
        {
            float3 delta = (current - reference) * scale;
            return saturate(float3(
                max(-delta.r, 0.0f) + max(delta.g, 0.0f),
                max(delta.r, 0.0f) + max(-delta.g, 0.0f),
                abs(delta.b)));
        }

        float3 BurtSSSVisualizeRelativeDelta(float3 current, float3 reference, float absoluteScale, float relativeScale)
        {
            float3 delta = abs(max(current, float3(0.0f, 0.0f, 0.0f)) - max(reference, float3(0.0f, 0.0f, 0.0f)));
            float referenceLuma = max(dot(max(reference, float3(0.0f, 0.0f, 0.0f)), BURT_LUMINANCE_WEIGHTS), 0.02f);
            float deltaLuma = dot(delta, BURT_LUMINANCE_WEIGHTS);
            float relativeDelta = deltaLuma / referenceLuma;
            float relativeSignal = saturate(relativeDelta * relativeScale);
            float3 channelDelta = saturate(delta * absoluteScale);
            float3 low = lerp(float3(0.01f, 0.02f, 0.05f), float3(0.05f, 0.18f, 0.55f), saturate(relativeSignal * 2.0f));
            float3 high = lerp(float3(0.95f, 0.72f, 0.08f), float3(1.0f, 0.08f, 0.03f), saturate((relativeSignal - 0.65f) * 2.857f));
            float3 heat = lerp(low, high, saturate((relativeSignal - 0.35f) * 2.5f)) * relativeSignal;
            return max(channelDelta, heat);
        }

        struct BurtSSSXRenderCombineData
        {
            float3 baseColor;
            float3 emission;
            float3 combinedColor;
            float3 diffuseLight;
            float3 specularLight;
            float3 sssColor;
            float3 profileTint;
            float3 subsurfaceLighting;
            float3 finalColor;
            float diffuseFactor;
            float setupMask;
        };

        BurtSSSXRenderCombineData BurtSSSEvaluateXRenderCombineData(float2 uv, float4 originalLit, float4 subsurfaceColor, BurtSSSProfile profile, uint profileType, float setupMask)
        {
            BurtSSSXRenderCombineData data;
            data.baseColor = BurtSSSLoadBaseColor(uv);
            data.emission = BurtSSSLoadPreExposedEmission(uv);
            data.combinedColor = max(originalLit.rgb - data.emission, float3(0.0f, 0.0f, 0.0f));
            float combinedLuminance = dot(data.combinedColor, BURT_LUMINANCE_WEIGHTS);
            float diffuseLuminance = max(originalLit.a, 0.0f);
            data.diffuseFactor = combinedLuminance > 0.0001f ? saturate(diffuseLuminance / combinedLuminance) : 0.0f;
            data.diffuseLight = data.combinedColor * data.diffuseFactor;
            data.specularLight = data.combinedColor * (1.0f - data.diffuseFactor);
            data.sssColor = BurtSSSResolveProfileDiffuseColor(profileType, subsurfaceColor, data.diffuseLight);
            data.profileTint = BurtSSSResolveProfileTint(profile, profileType);
            float3 blendedSSSColor = lerp(data.diffuseLight, data.sssColor, BurtSSSResolveProfileBlend(profile, profileType));
            blendedSSSColor *= BurtSSSResolveSeparableProfileColorScale(profile, profileType);
            data.subsurfaceLighting = lerp(data.diffuseLight, blendedSSSColor, data.profileTint);
            data.finalColor = max(data.subsurfaceLighting * data.baseColor + data.specularLight + data.emission, float3(0.0f, 0.0f, 0.0f));
            data.setupMask = setupMask;
            return data;
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
            bool gbufferValid = BurtIsSubsurfaceShadingModel(data.shadingModelID) && !BurtSSSIsSkyDepth(surface.rawDepth);
            uint profileIDFromTexture;
            uint profileTypeFromTexture;
            BurtSSSDecodeProfileIDAndType(BurtSSSLoadProfileIDAndType(uv), profileIDFromTexture, profileTypeFromTexture);
            if ((profileTypeFromTexture & (BURT_SSS_PROFILE_TYPE_BURLEY | BURT_SSS_PROFILE_TYPE_SEPARABLE)) != 0u)
            {
                surface.profileIndex = (float)clamp((int)profileIDFromTexture, 0, 7);
                surface.profileType = profileTypeFromTexture;
                surface.valid = gbufferValid ? 1.0f : 0.0f;
            }
            else
            {
                surface.profileType = 0u;
                surface.valid = 0.0f;
            }
            return surface;
        }

        float BurtSSSInBounds(float2 uv)
        {
            float2 insideMin = step(float2(0.0f, 0.0f), uv);
            float2 insideMax = step(uv, float2(1.0f, 1.0f));
            return insideMin.x * insideMin.y * insideMax.x * insideMax.y;
        }

        float BurtSSSInSeparableSampleBounds(float2 uv)
        {
            return (uv.x > 0.0f && uv.y > 0.0f && uv.x < 1.0f && uv.y < 1.0f) ? 1.0f : 0.0f;
        }

        uint3 BurtSSSRand3DPCG16(int3 p)
        {
            uint3 v = uint3(p);
            v = v * 1664525u + 1013904223u;
            v.x += v.y * v.z;
            v.y += v.z * v.x;
            v.z += v.x * v.y;
            v.x += v.y * v.z;
            v.y += v.z * v.x;
            v.z += v.x * v.y;
            return v >> 16u;
        }

        float2 BurtSSSR2Sequence(uint index)
        {
            const float phiInv = 1.0f / 1.324717957244746f;
            const float phi2Inv = 1.0f / (1.324717957244746f * 1.324717957244746f);
            return frac(float2(phiInv, phi2Inv) * (float)index);
        }

        bool BurtSSSUseStableSampling()
        {
            return _BurtSSSFrameParams.z > 0.5f;
        }

        bool BurtSSSUseDebugSampling()
        {
            return _BurtSSSFrameParams.w > 0.5f;
        }

        bool BurtSSSUseSeparableJitter()
        {
            return !BurtSSSUseStableSampling() && !BurtSSSUseDebugSampling();
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

        float BurtSSSLoadCoarseMask(float2 uv)
        {
            float2 fullTexel = _BurtSSSScreenSize.zw;
            float2 groupCenter = (floor(saturate(uv) * _BurtSSSScreenSize.xy / 8.0f) * 8.0f + 4.0f) * fullTexel;
            float mask = 0.0f;
            [unroll]
            for (int y = -1; y <= 1; y++)
            {
                [unroll]
                for (int x = -1; x <= 1; x++)
                {
                    float2 sampleUV = saturate(groupCenter + float2((float)x, (float)y) * fullTexel * 8.0f);
                    mask = max(mask, BurtSSSLoadSetup(sampleUV).r);
                }
            }

            return mask;
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

        float BurtSSSResolveSeparableRadiusFromDepth(float centerSceneDepth)
        {
            float projectionScale = max(_BurtSSSProjectionParams.x, 0.0001f);
            float depthInCentimeters = max(centerSceneDepth * 100.0f, 0.0001f);
            return clamp(BURT_SSS_SUBSURFACE_RADIUS_SCALE * projectionScale / depthInCentimeters, 0.0f, 256.0f);
        }

        float BurtSSSResolveSeparableProfileRadiusScale(BurtSSSSurface center, BurtSSSProfile profile)
        {
            float radiusScale = max(profile.params.x, 0.01f) / BURT_SSS_DEFAULT_RADIUS_PIXELS;
            float distanceScale = rsqrt(max(profile.params2.y, 0.01f) / 2.0f);
            float strengthScale = lerp(saturate(profile.params.w / 0.012f), 1.0f, saturate(center.strength));
            return clamp(radiusScale * distanceScale * strengthScale, 0.05f, 8.0f);
        }

        float BurtSSSResolveSeparableRadius(BurtSSSSurface center, BurtSSSProfile profile)
        {
            return BurtSSSResolveSeparableRadiusFromDepth(center.linearEyeDepth) * BurtSSSResolveSeparableProfileRadiusScale(center, profile);
        }

        float BurtSSSResolveSeparableMaxKernelOffset(BurtSSSSurface center)
        {
            if (!BurtSSSUseProfileParamLut())
            {
                return 8.16f;
            }

            float maxOffset = 0.0f;
            [unroll]
            for (int i = 1; i < BURT_SSS_PROFILE_PARAM_KERNEL0_COUNT; i++)
            {
                float4 kernel = BurtSSSFetchProfileParam(BURT_SSS_PROFILE_PARAM_KERNEL0_OFFSET + (float)i, center.profileIndex);
                maxOffset = max(maxOffset, abs(kernel.a));
            }

            return maxOffset;
        }

        float BurtSSSResolveSeparableMaxOffsetPixels(BurtSSSSurface center, BurtSSSProfile profile)
        {
            bool useProfileLut = BurtSSSUseProfileParamLut();
            float radiusUVX = useProfileLut ? BurtSSSResolveSeparableRadius(center, profile) : BurtSSSResolveRadius(center, profile) * _BurtSSSScreenSize.z;
            return max(radiusUVX * BurtSSSResolveSeparableMaxKernelOffset(center) * _BurtSSSScreenSize.x, 0.0f);
        }

        float BurtSSSResolveSeparableMaxOffsetPixelsFromDepth(BurtSSSSurface center, BurtSSSProfile profile, float centerSceneDepth)
        {
            bool useProfileLut = BurtSSSUseProfileParamLut();
            float radiusUVX = useProfileLut ? BurtSSSResolveSeparableRadiusFromDepth(centerSceneDepth) * BurtSSSResolveSeparableProfileRadiusScale(center, profile) : BurtSSSResolveRadius(center, profile) * _BurtSSSScreenSize.z;
            return max(radiusUVX * BurtSSSResolveSeparableMaxKernelOffset(center) * _BurtSSSScreenSize.x, 0.0f);
        }

        float BurtSSSSurfaceStability(BurtSSSSurface center, BurtSSSProfile profile, float2 uv)
        {
            if (center.valid <= 0.0f)
            {
                return 0.0f;
            }

            float coarseMask = BurtSSSLoadCoarseMask(uv);
            float2 texel = _BurtSSSScreenSize.zw;
            BurtSSSSurface right = BurtSSSLoadSurface(saturate(uv + float2(texel.x, 0.0f)));
            BurtSSSSurface up = BurtSSSLoadSurface(saturate(uv + float2(0.0f, texel.y)));
            float horizontal = BurtSSSSampleWeight(center, right, profile, 1.0f);
            float vertical = BurtSSSSampleWeight(center, up, profile, 1.0f);
            return saturate(coarseMask * min(horizontal, vertical));
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

        float BurtSSSSeparableSampleAlpha(float centerSceneDepth, float sampleSceneDepth)
        {
            float hasSampleDepth = sampleSceneDepth > 0.0f ? 1.0f : 0.0f;
            float depthReject = saturate(12000.0f / 400000.0f * abs(centerSceneDepth - sampleSceneDepth) * 6.0f * 100.0f);
            return hasSampleDepth * (1.0f - depthReject);
        }

        float3 BurtSSSResolveSeparableSampleKernelWeight(int index, BurtSSSSurface center)
        {
            if (BurtSSSUseProfileParamLut())
            {
                return max(BurtSSSFetchProfileParam(BURT_SSS_PROFILE_PARAM_KERNEL0_OFFSET + (float)index, center.profileIndex).rgb, float3(0.0f, 0.0f, 0.0f));
            }

            if (index == 0)
            {
                return BURT_SSS_FALLBACK_KERNEL_CENTER;
            }

            if (index == 1) return float3(0.150f, 0.165f, 0.168f);
            if (index == 2) return float3(0.118f, 0.123f, 0.114f);
            if (index == 3) return float3(0.090f, 0.088f, 0.074f);
            if (index == 4) return float3(0.066f, 0.058f, 0.043f);
            if (index == 5) return float3(0.047f, 0.036f, 0.023f);
            if (index == 6) return float3(0.032f, 0.021f, 0.012f);
            if (index == 7) return float3(0.021f, 0.012f, 0.006f);
            if (index == 8) return float3(0.013f, 0.006f, 0.003f);
            if (index == 9) return float3(0.008f, 0.003f, 0.0015f);
            if (index == 10) return float3(0.0045f, 0.0015f, 0.0007f);
            if (index == 11) return float3(0.0025f, 0.0007f, 0.0003f);
            if (index == 12) return float3(0.0015f, 0.0003f, 0.0001f);
            return float3(0.0f, 0.0f, 0.0f);
        }

        float BurtSSSResolveSeparableSampleKernelOffset(int index, BurtSSSSurface center)
        {
            if (BurtSSSUseProfileParamLut())
            {
                return BurtSSSFetchProfileParam(BURT_SSS_PROFILE_PARAM_KERNEL0_OFFSET + (float)index, center.profileIndex).a;
            }

            if (index == 1) return 0.22f;
            if (index == 2) return 0.46f;
            if (index == 3) return 0.78f;
            if (index == 4) return 1.16f;
            if (index == 5) return 1.60f;
            if (index == 6) return 2.12f;
            if (index == 7) return 2.74f;
            if (index == 8) return 3.48f;
            if (index == 9) return 4.36f;
            if (index == 10) return 5.42f;
            if (index == 11) return 6.68f;
            if (index == 12) return 8.16f;
            return 0.0f;
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
            float sameType = ((center.profileType & sampleSurface.profileType) & BURT_SSS_PROFILE_TYPE_MASK) != 0u ? 1.0f : 0.0f;
            float3 weight = kernelWeight * scalarWeight;
            float boundaryBleed = 0.0f;
            float3 boundaryColor = original;
            float sameProfile = BurtSSSResolveProfileIndex(center.profileIndex) == BurtSSSResolveProfileIndex(sampleSurface.profileIndex) ? 1.0f : 0.0f;
            float sampleHasSource = inBounds * center.valid * sampleSurface.valid * sameProfile * sameType;
            float3 sampleColor = sampleHasSource > 0.5f ? BurtSSSDecodeSourceDiffuse(tex2D(_BurtSSSSourceTexture, sampleUV), sourceIsLit) : boundaryColor;
            float boundaryBlend = sampleHasSource > 0.5f ? saturate((1.0f - scalarWeight) * boundaryBleed) : 1.0f;
            sampleColor = lerp(sampleColor, boundaryColor, boundaryBlend);
            float3 weightedKernel = weight * BurtSSSProfileKernelScale(center, profile, offset);
            sumColor += sampleColor * weightedKernel;
            sumWeight += weightedKernel;
        }

        void BurtSSSAccumulateSeparableSample(
            BurtSSSSurface center,
            BurtSSSProfile profile,
            float4 centerSource,
            float centerSceneDepth,
            float2 uv,
            float2 texelStep,
            float offset,
            float3 kernelWeight,
            inout float3 sumColor,
            inout float3 sumWeight)
        {
            float2 sampleUVUnclamped = uv + texelStep * offset;
            float inBounds = BurtSSSInSeparableSampleBounds(sampleUVUnclamped);
            float2 sampleUV = saturate(sampleUVUnclamped);
            float4 sampleSource = tex2D(_BurtSSSSeparableInputTexture, sampleUV);
            uint sampleProfileIndex;
            uint sampleProfileType;
            BurtSSSDecodeProfileIDAndType(BurtSSSLoadProfileIDAndTypePoint(sampleUVUnclamped), sampleProfileIndex, sampleProfileType);
            float sampleDepth = sampleSource.a;
            float sameProfile = BurtSSSResolveProfileIndex(center.profileIndex) == BurtSSSResolveProfileIndex((float)sampleProfileIndex) ? 1.0f : 0.0f;
            float hasSeparableProfile = (sampleProfileType & BURT_SSS_PROFILE_TYPE_SEPARABLE) != 0u ? 1.0f : 0.0f;
            float sameSample = inBounds * sameProfile * hasSeparableProfile;
            float3 sampleColor = sameSample > 0.5f ? sampleSource.rgb : centerSource.rgb;
            float sampleAlpha = BurtSSSSeparableSampleAlpha(centerSceneDepth, sampleDepth);
            float3 colorTint = sameSample > 0.5f ? float3(1.0f, 1.0f, 1.0f) : max(profile.boundaryColorBleed.rgb, float3(0.0f, 0.0f, 0.0f));
            float3 weightedKernel = sampleAlpha * kernelWeight;
            sumColor += sampleColor * colorTint * weightedKernel;
            sumWeight += weightedKernel;
        }

        void BurtSSSAccumulateSeparableDebugSample(
            BurtSSSSurface center,
            float centerDepth,
            float2 uv,
            float2 texelStep,
            float offset,
            inout float sameSampleSum,
            inout float depthWeightSum,
            inout float sampleCount)
        {
            float2 sampleUVUnclamped = uv + texelStep * offset;
            float inBounds = BurtSSSInSeparableSampleBounds(sampleUVUnclamped);
            float2 sampleUV = saturate(sampleUVUnclamped);
            float sampleDepth = tex2D(_BurtSSSSeparableInputTexture, sampleUV).a;
            uint sampleProfileIndex;
            uint sampleProfileType;
            BurtSSSDecodeProfileIDAndType(BurtSSSLoadProfileIDAndTypePoint(sampleUVUnclamped), sampleProfileIndex, sampleProfileType);
            float sameProfile = BurtSSSResolveProfileIndex(center.profileIndex) == BurtSSSResolveProfileIndex((float)sampleProfileIndex) ? 1.0f : 0.0f;
            float hasSeparableProfile = (sampleProfileType & BURT_SSS_PROFILE_TYPE_SEPARABLE) != 0u ? 1.0f : 0.0f;
            float sameSample = inBounds * sameProfile * hasSeparableProfile;
            sameSampleSum += sameSample;
            depthWeightSum += inBounds * BurtSSSSeparableSampleAlpha(centerDepth, sampleDepth);
            sampleCount += 1.0f;
        }

        float3 BurtSSSSeparableSampleGateDebug(float2 uv)
        {
            float4 setup = BurtSSSLoadSetup(uv);
            BurtSSSSurface center = BurtSSSLoadSurface(uv);
            float centerDepth = tex2D(_BurtSSSSeparableInputTexture, uv).a;
            if (setup.r <= 0.0f || center.valid <= 0.0f || centerDepth <= 0.0f || (center.profileType & BURT_SSS_PROFILE_TYPE_SEPARABLE) == 0u)
            {
                return BurtSSSSeparableFailureColor(uv, setup, center, centerDepth) * 0.35f;
            }

            BurtSSSProfile profile = BurtSSSLoadProfile(center.profileIndex);
            bool useProfileLut = BurtSSSUseProfileParamLut();
            float radius = useProfileLut ? BurtSSSResolveSeparableRadiusFromDepth(centerDepth) * BurtSSSResolveSeparableProfileRadiusScale(center, profile) : BurtSSSResolveRadius(center, profile);
            float2 texelStep = useProfileLut ? float2(radius, 0.0f) : float2(_BurtSSSScreenSize.z * radius, 0.0f);
            float sameSampleSum = 0.0f;
            float depthWeightSum = 0.0f;
            float sampleCount = 0.0f;

            [unroll]
            for (int i = 1; i < BURT_SSS_PROFILE_PARAM_KERNEL0_COUNT; i++)
            {
                float kernelOffset = useProfileLut
                    ? BurtSSSResolveSeparableSampleKernelOffset(i, center)
                    : (float)i / (BURT_SSS_PROFILE_PARAM_KERNEL0_SIZE - 1.0f) * 8.16f;
                BurtSSSAccumulateSeparableDebugSample(center, centerDepth, uv, texelStep, kernelOffset, sameSampleSum, depthWeightSum, sampleCount);
                BurtSSSAccumulateSeparableDebugSample(center, centerDepth, uv, texelStep, -kernelOffset, sameSampleSum, depthWeightSum, sampleCount);
            }

            float valid = setup.r;
            float sameSampleRatio = sameSampleSum / max(sampleCount, 1.0f);
            float depthWeight = depthWeightSum / max(sampleCount, 1.0f);
            float radiusPixels = BurtSSSResolveSeparableMaxOffsetPixelsFromDepth(center, profile, centerDepth);
            return float3(saturate(sameSampleRatio), saturate(depthWeight), saturate(radiusPixels / 32.0f)) * valid;
        }

        void BurtSSSAccumulateSeparablePair(
            BurtSSSSurface center,
            BurtSSSProfile profile,
            float4 centerSource,
            float centerSceneDepth,
            float2 uv,
            float2 texelStep,
            float offset,
            float3 kernelWeight,
            inout float3 sumColor,
            inout float3 sumWeight)
        {
            BurtSSSAccumulateSeparableSample(center, profile, centerSource, centerSceneDepth, uv, texelStep, offset, kernelWeight, sumColor, sumWeight);
            BurtSSSAccumulateSeparableSample(center, profile, centerSource, centerSceneDepth, uv, texelStep, -offset, kernelWeight, sumColor, sumWeight);
        }

        float3 BurtSSSResolveProfileKernelSpreadColor(BurtSSSSurface center)
        {
            float3 spread = float3(0.0f, 0.0f, 0.0f);
            if (BurtSSSUseProfileParamLut())
            {
                [unroll]
                for (int i = 1; i < BURT_SSS_PROFILE_PARAM_KERNEL0_COUNT; i++)
                {
                    float4 kernel = BurtSSSFetchProfileParam(BURT_SSS_PROFILE_PARAM_KERNEL0_OFFSET + (float)i, center.profileIndex);
                    spread += max(kernel.rgb, float3(0.0f, 0.0f, 0.0f)) * abs(kernel.a) * 2.0f;
                }
            }
            else
            {
                spread += float3(0.150f, 0.165f, 0.168f) * 0.22f * 2.0f;
                spread += float3(0.118f, 0.123f, 0.114f) * 0.46f * 2.0f;
                spread += float3(0.090f, 0.088f, 0.074f) * 0.78f * 2.0f;
                spread += float3(0.066f, 0.058f, 0.043f) * 1.16f * 2.0f;
                spread += float3(0.047f, 0.036f, 0.023f) * 1.60f * 2.0f;
                spread += float3(0.032f, 0.021f, 0.012f) * 2.12f * 2.0f;
                spread += float3(0.021f, 0.012f, 0.006f) * 2.74f * 2.0f;
                spread += float3(0.013f, 0.006f, 0.003f) * 3.48f * 2.0f;
                spread += float3(0.008f, 0.003f, 0.0015f) * 4.36f * 2.0f;
                spread += float3(0.0045f, 0.0015f, 0.0007f) * 5.42f * 2.0f;
                spread += float3(0.0025f, 0.0007f, 0.0003f) * 6.68f * 2.0f;
                spread += float3(0.0015f, 0.0003f, 0.0001f) * 8.16f * 2.0f;
            }

            float maxChannel = max(max(spread.r, spread.g), spread.b);
            return maxChannel > 0.00001f ? saturate(spread / maxChannel) : float3(0.0f, 0.0f, 0.0f);
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

        void BurtSSSAccumulateFallbackKernel(
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
                for (int i = 1; i < BURT_SSS_PROFILE_PARAM_KERNEL0_COUNT; i++)
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
                BURT_SSS_PROFILE_PARAM_KERNEL0_COUNT,
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
                BURT_SSS_PROFILE_PARAM_KERNEL1_COUNT,
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
                BURT_SSS_PROFILE_PARAM_KERNEL2_COUNT,
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
            float luminanceLift = saturate((blurredLum - originalLum) / max(blurredLum, 0.0001f));
            float luminanceDrop = saturate((originalLum - blurredLum) / max(originalLum, 0.0001f));
            float preserveWeight = saturate(strength * lerp(0.68f, 0.18f, luminanceLift));
            preserveWeight = lerp(preserveWeight, saturate(strength * 0.86f), luminanceDrop);
            float lowerClamp = lerp(0.72f, 0.38f, luminanceLift);
            float upperClamp = lerp(1.28f, 1.38f, luminanceDrop);
            float energyRatio = lerp(1.0f, clamp(safeRatio, lowerClamp, upperClamp), preserveWeight);
            return max(blurred * energyRatio, float3(0.0f, 0.0f, 0.0f));
        }

        float3 BurtSSSStabilizeDiffuseLighting(float3 original, float3 blurred, float strength)
        {
            return BurtSSSPreserveDiffuseLuminance(original, blurred, strength);
        }

        float4 BurtSSSBlurSeparableXRender(float2 uv, float2 direction, float writeValidAlpha)
        {
            BurtSSSSurface center = BurtSSSLoadSurface(uv);
            float4 centerSource = tex2D(_BurtSSSSeparableInputTexture, uv);
            float centerSceneDepth = max(centerSource.a, 0.0f);
            float3 centerColor = centerSource.rgb;
            if (center.valid <= 0.0f || centerSceneDepth <= 0.0f || (center.profileType & BURT_SSS_PROFILE_TYPE_SEPARABLE) == 0u)
            {
                float fallbackAlpha = writeValidAlpha > 0.5f ? (centerSource.a > 0.00001f ? 1.0f : 0.0f) : centerSource.a;
                return float4(centerSource.rgb, fallbackAlpha);
            }

            BurtSSSProfile profile = BurtSSSLoadProfile(center.profileIndex);
            bool useProfileLut = BurtSSSUseProfileParamLut();
            float radius = useProfileLut ? BurtSSSResolveSeparableRadiusFromDepth(centerSceneDepth) * BurtSSSResolveSeparableProfileRadiusScale(center, profile) : BurtSSSResolveRadius(center, profile);
            float2 texelStep = useProfileLut ? direction * radius : direction * _BurtSSSScreenSize.zw * radius;
            texelStep.y *= useProfileLut ? _BurtSSSScreenSize.x * _BurtSSSScreenSize.w : 1.0f;
            float3 centerWeight = useProfileLut ? BurtSSSResolveSeparableSampleKernelWeight(0, center) : BURT_SSS_FALLBACK_KERNEL_CENTER;
            float3 sumColor = centerColor * centerWeight;
            float3 sumWeight = float3(0.00001f, 0.00001f, 0.00001f) + centerWeight;
            float2 sampleCenterUV = uv;

            if (useProfileLut)
            {
                if (BurtSSSUseSeparableJitter())
                {
                    int2 pixel = BurtSSSClampPixel(uv);
                    uint frameIndex = (uint)_BurtSSSFrameParams.x;
                    uint3 random = BurtSSSRand3DPCG16(int3(pixel.x, pixel.y, (int)frameIndex));
                    float2 r2 = BurtSSSR2Sequence(random.z);
                    sampleCenterUV += texelStep * BurtSSSResolveSeparableSampleKernelOffset(1, center) * (r2.x * 2.0f - 1.0f);

                    float2 crossAxis = abs(direction.x) > 0.5f
                        ? float2(0.0f, _BurtSSSScreenSize.w)
                        : float2(_BurtSSSScreenSize.z, 0.0f);
                    sampleCenterUV += crossAxis * ((r2.y * 2.0f - 1.0f) * 4.0f);
                }

                [unroll]
                for (int i = 1; i < BURT_SSS_PROFILE_PARAM_KERNEL0_COUNT; i++)
                {
                    BurtSSSAccumulateSeparablePair(
                        center,
                        profile,
                        centerSource,
                        centerSceneDepth,
                        sampleCenterUV,
                        texelStep,
                        BurtSSSResolveSeparableSampleKernelOffset(i, center),
                        BurtSSSResolveSeparableSampleKernelWeight(i, center),
                        sumColor,
                        sumWeight);
                }
            }
            else
            {
                BurtSSSAccumulateSeparablePair(center, profile, centerSource, centerSceneDepth, uv, texelStep, 0.22f, float3(0.150f, 0.165f, 0.168f), sumColor, sumWeight);
                BurtSSSAccumulateSeparablePair(center, profile, centerSource, centerSceneDepth, uv, texelStep, 0.46f, float3(0.118f, 0.123f, 0.114f), sumColor, sumWeight);
                BurtSSSAccumulateSeparablePair(center, profile, centerSource, centerSceneDepth, uv, texelStep, 0.78f, float3(0.090f, 0.088f, 0.074f), sumColor, sumWeight);
                BurtSSSAccumulateSeparablePair(center, profile, centerSource, centerSceneDepth, uv, texelStep, 1.16f, float3(0.066f, 0.058f, 0.043f), sumColor, sumWeight);
                BurtSSSAccumulateSeparablePair(center, profile, centerSource, centerSceneDepth, uv, texelStep, 1.60f, float3(0.047f, 0.036f, 0.023f), sumColor, sumWeight);
                BurtSSSAccumulateSeparablePair(center, profile, centerSource, centerSceneDepth, uv, texelStep, 2.12f, float3(0.032f, 0.021f, 0.012f), sumColor, sumWeight);
                BurtSSSAccumulateSeparablePair(center, profile, centerSource, centerSceneDepth, uv, texelStep, 2.74f, float3(0.021f, 0.012f, 0.006f), sumColor, sumWeight);
                BurtSSSAccumulateSeparablePair(center, profile, centerSource, centerSceneDepth, uv, texelStep, 3.48f, float3(0.013f, 0.006f, 0.003f), sumColor, sumWeight);
                BurtSSSAccumulateSeparablePair(center, profile, centerSource, centerSceneDepth, uv, texelStep, 4.36f, float3(0.008f, 0.003f, 0.0015f), sumColor, sumWeight);
                BurtSSSAccumulateSeparablePair(center, profile, centerSource, centerSceneDepth, uv, texelStep, 5.42f, float3(0.0045f, 0.0015f, 0.0007f), sumColor, sumWeight);
                BurtSSSAccumulateSeparablePair(center, profile, centerSource, centerSceneDepth, uv, texelStep, 6.68f, float3(0.0025f, 0.0007f, 0.0003f), sumColor, sumWeight);
                BurtSSSAccumulateSeparablePair(center, profile, centerSource, centerSceneDepth, uv, texelStep, 8.16f, float3(0.0015f, 0.0003f, 0.0001f), sumColor, sumWeight);
            }

            float3 blurred = max(sumColor / max(sumWeight, float3(0.00001f, 0.00001f, 0.00001f)), float3(0.0f, 0.0f, 0.0f));
            float alpha = writeValidAlpha > 0.5f ? 1.0f : centerSceneDepth;
            return float4(blurred, alpha);
        }

        float3 BurtSSSBlur(float2 uv, float2 direction, float sourceIsLit, float useSeparableRadius)
        {
            BurtSSSSurface center = BurtSSSLoadSurface(uv);
            float3 original = BurtSSSDecodeSourceDiffuse(tex2D(_BurtSSSSourceTexture, uv), sourceIsLit);
            if (center.valid <= 0.0f)
            {
                return original;
            }

            BurtSSSProfile profile = BurtSSSLoadProfile(center.profileIndex);
            bool useProfileLut = BurtSSSUseProfileParamLut();
            bool useSeparableProfileStep = useSeparableRadius > 0.5f && useProfileLut;
            float radius = useSeparableProfileStep ? BurtSSSResolveSeparableRadius(center, profile) : BurtSSSResolveRadius(center, profile);
            float2 texelStep = useSeparableProfileStep ? direction * radius : direction * _BurtSSSScreenSize.zw * radius;
            texelStep.y *= useSeparableProfileStep ? _BurtSSSScreenSize.x * _BurtSSSScreenSize.w : 1.0f;
            float3 centerWeight = useProfileLut
                ? max(BurtSSSFetchProfileParam(BURT_SSS_PROFILE_PARAM_KERNEL0_OFFSET, center.profileIndex).rgb, float3(0.0001f, 0.0001f, 0.0001f))
                : BURT_SSS_FALLBACK_KERNEL_CENTER;
            float3 sumColor = original * centerWeight;
            float3 sumWeight = centerWeight;
            if (useProfileLut)
            {
                BurtSSSAccumulateLayeredProfileKernels(center, profile, original, uv, texelStep, sourceIsLit, sumColor, sumWeight);
            }
            else
            {
                BurtSSSAccumulateFallbackKernel(center, profile, original, uv, texelStep, sourceIsLit, sumColor, sumWeight);
            }

            float3 blurred = sumColor / max(sumWeight, float3(0.0001f, 0.0001f, 0.0001f));
            return BurtSSSStabilizeDiffuseLighting(original, blurred, center.strength);
        }

        float4 FragCopy(Varyings input) : SV_Target
        {
            return tex2D(_BurtCameraColorTexture, input.screenUV);
        }

        float4 FragHorizontal(Varyings input) : SV_Target
        {
            float setupMask = BurtSSSLoadSetup(input.screenUV).r;
            BurtSSSSurface center = BurtSSSLoadSurface(input.screenUV);
            if (setupMask <= 0.0f || (center.profileType & BURT_SSS_PROFILE_TYPE_SEPARABLE) == 0u)
            {
                return tex2D(_BurtSSSSeparableInputTexture, input.screenUV);
            }

            return BurtSSSBlurSeparableXRender(input.screenUV, float2(1.0f, 0.0f), 0.0f);
        }

        float4 FragVertical(Varyings input) : SV_Target
        {
            float setupMask = BurtSSSLoadSetup(input.screenUV).r;
            BurtSSSSurface center = BurtSSSLoadSurface(input.screenUV);
            if (setupMask <= 0.0f || (center.profileType & BURT_SSS_PROFILE_TYPE_SEPARABLE) == 0u)
            {
                return tex2D(_BurtSSSSeparableInputTexture, input.screenUV);
            }

            return BurtSSSBlurSeparableXRender(input.screenUV, float2(0.0f, 1.0f), 1.0f);
        }

        float4 FragSetup(Varyings input) : SV_Target
        {
            BurtSSSSurface surface = BurtSSSLoadSurface(input.screenUV);
            return BurtSSSEncodeSetup(surface);
        }

        float4 FragCoarseMask(Varyings input) : SV_Target
        {
            float mask = BurtSSSLoadCoarseMask(input.screenUV);
            return float4(mask, mask, mask, 1.0f);
        }

        float4 FragMask(Varyings input) : SV_Target
        {
            float4 gbuffer1 = tex2D(_BurtGBuffer1, input.screenUV);
            float shadingModelID;
            float strength = BurtDecodeMetallicAndShadingModelFromGBuffer(gbuffer1.b, shadingModelID);
            uint profileIDFromMaterial;
            uint profileTypeFromMaterial;
            BurtSSSDecodeProfileIDAndType(BurtSSSLoadMaterialEncodedProfileIDAndType(input.screenUV), profileIDFromMaterial, profileTypeFromMaterial);
            float profileIndex = (float)clamp((int)profileIDFromMaterial, 0, 7);
            BurtSSSProfile profile = BurtSSSLoadProfile(profileIndex);
            uint effectiveProfileType = 0u;
            effectiveProfileType |= (profileTypeFromMaterial & BURT_SSS_PROFILE_TYPE_SEPARABLE);
            effectiveProfileType |= strength > profile.params.w ? (profileTypeFromMaterial & BURT_SSS_PROFILE_TYPE_BURLEY) : 0u;
            float valid = BurtIsSubsurfaceShadingModel(shadingModelID) &&
                (effectiveProfileType & (BURT_SSS_PROFILE_TYPE_BURLEY | BURT_SSS_PROFILE_TYPE_SEPARABLE)) != 0u
                    ? 1.0f
                    : 0.0f;
            return float4(valid, valid, valid, 1.0f);
        }

        float4 BurtSSSEvaluateCombineColor(float2 uv)
        {
            float4 originalLit = tex2D(_BurtSSSSourceTexture, uv);
            float4 setup = BurtSSSLoadSetup(uv);
            float setupMask = setup.r;
            uint profileIndex;
            uint profileType;
            BurtSSSDecodeProfileIDAndType(BurtSSSLoadProfileIDAndType(uv), profileIndex, profileType);
            if (setupMask <= 0.0f || (profileType & (BURT_SSS_PROFILE_TYPE_BURLEY | BURT_SSS_PROFILE_TYPE_SEPARABLE)) == 0u)
            {
                return float4(originalLit.rgb, 1.0f);
            }

            float4 subsurfaceColor = tex2D(_BurtSSSBlurTexture, uv);
            BurtSSSProfile profile = BurtSSSLoadProfile((float)profileIndex);
            BurtSSSXRenderCombineData combine = BurtSSSEvaluateXRenderCombineData(uv, originalLit, subsurfaceColor, profile, profileType, setupMask);
            return float4(combine.finalColor, 1.0f);
        }

        float4 FragCombine(Varyings input) : SV_Target
        {
            return BurtSSSEvaluateCombineColor(input.screenUV);
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
                float coarseMask = BurtSSSLoadCoarseMask(input.screenUV);
                return float4(coarseMask, coarseMask, coarseMask, 1.0f);
            }

            if (_BurtSSSDebugMode < 4.5f)
            {
                return float4(max(tex2D(_BurtSSSBlurTexture, input.screenUV).rgb, float3(0.0f, 0.0f, 0.0f)), 1.0f);
            }

            if (_BurtSSSDebugMode > 32.5f && _BurtSSSDebugMode < 33.5f)
            {
                float4 subsurfaceColor = tex2D(_BurtSSSBlurTexture, input.screenUV);
                uint normalizedProfileIndex;
                uint normalizedProfileType;
                BurtSSSDecodeProfileIDAndType(BurtSSSLoadProfileIDAndType(input.screenUV), normalizedProfileIndex, normalizedProfileType);
                float3 normalizedBlur = BurtSSSResolveProfileDiffuseColor(normalizedProfileType, subsurfaceColor, float3(0.0f, 0.0f, 0.0f));
                return float4(max(normalizedBlur, float3(0.0f, 0.0f, 0.0f)), 1.0f);
            }

            if (_BurtSSSDebugMode > 27.5f && _BurtSSSDebugMode < 28.5f)
            {
                float blurAlpha = tex2D(_BurtSSSBlurTexture, input.screenUV).a;
                return float4(blurAlpha, blurAlpha, blurAlpha, 1.0f);
            }

            if (_BurtSSSDebugMode < 5.5f)
            {
                return tex2D(_BurtSSSCombineTexture, input.screenUV);
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
            if (_BurtSSSDebugMode > 43.5f && _BurtSSSDebugMode < 44.5f)
            {
                uint rawProfileIndex;
                uint rawProfileType;
                BurtSSSDecodeProfileIDAndType(BurtSSSLoadProfileIDAndType(input.screenUV), rawProfileIndex, rawProfileType);
                float profileIndex = (rawProfileType & (BURT_SSS_PROFILE_TYPE_BURLEY | BURT_SSS_PROFILE_TYPE_SEPARABLE)) != 0u
                    ? (float)rawProfileIndex
                    : center.profileIndex;
                BurtSSSProfile rawProfile = BurtSSSLoadProfile(profileIndex);
                return float4(saturate(rawProfile.tint.rgb), 1.0f);
            }

            if (_BurtSSSDebugMode > 14.5f && _BurtSSSDebugMode < 15.5f)
            {
                float isBurley = (center.profileType & BURT_SSS_PROFILE_TYPE_BURLEY) != 0u ? 1.0f : 0.0f;
                float isSeparable = (center.profileType & BURT_SSS_PROFILE_TYPE_SEPARABLE) != 0u ? 1.0f : 0.0f;
                return float4(isBurley * setup.r, isSeparable * setup.r, 0.0f, 1.0f);
            }

            BurtSSSProfile profile = BurtSSSLoadProfile(center.profileIndex);
            if (_BurtSSSDebugMode > 28.5f && _BurtSSSDebugMode < 29.5f)
            {
                float valid = setup.r * ((center.profileType & BURT_SSS_PROFILE_TYPE_SEPARABLE) != 0u ? 1.0f : 0.0f);
                float centerDepth = tex2D(_BurtSSSSeparableInputTexture, input.screenUV).a;
                if (valid <= 0.0f || centerDepth <= 0.0f)
                {
                    return float4(BurtSSSSeparableFailureColor(input.screenUV, setup, center, centerDepth) * 0.35f, 1.0f);
                }

                float radiusPixels = centerDepth > 0.0f ? BurtSSSResolveSeparableMaxOffsetPixelsFromDepth(center, profile, centerDepth) : 0.0f;
                float visibleRadius = saturate(radiusPixels / 32.0f) * valid;
                return float4(visibleRadius, visibleRadius, visibleRadius, 1.0f);
            }

            if (_BurtSSSDebugMode > 22.5f && _BurtSSSDebugMode < 23.5f)
            {
                return float4(BurtSSSResolveProfileTint(profile, center.profileType) * setup.r, 1.0f);
            }

            if (_BurtSSSDebugMode > 23.5f && _BurtSSSDebugMode < 24.5f)
            {
                return float4(saturate(profile.surfaceAlbedo.rgb) * setup.r, 1.0f);
            }

            if (_BurtSSSDebugMode > 24.5f && _BurtSSSDebugMode < 25.5f)
            {
                float3 meanFreePathColor = max(profile.meanFreePath.rgb, float3(0.0f, 0.0f, 0.0f));
                float maxMeanFreePath = max(max(meanFreePathColor.r, meanFreePathColor.g), meanFreePathColor.b);
                meanFreePathColor /= max(maxMeanFreePath, 0.0001f);
                return float4(saturate(meanFreePathColor) * setup.r, 1.0f);
            }

            float4 originalLit = tex2D(_BurtSSSOriginalTexture, input.screenUV);
            float3 baseColor;
            float3 emission;
            float3 diffuseWithBaseColor;
            float3 diffuseLighting;
            float3 specularLight;
            BurtSSSDecodeLightingComponents(input.screenUV, originalLit, baseColor, emission, diffuseWithBaseColor, diffuseLighting, specularLight);

            if (_BurtSSSDebugMode > 33.5f && _BurtSSSDebugMode < 34.5f)
            {
                if (setup.r <= 0.0f || (center.profileType & (BURT_SSS_PROFILE_TYPE_BURLEY | BURT_SSS_PROFILE_TYPE_SEPARABLE)) == 0u)
                {
                    return BurtSSSSeparableChainColor(input.screenUV, setup, center);
                }

                float4 subsurfaceColor = tex2D(_BurtSSSBlurTexture, input.screenUV);
                float3 blurredDiffuse = BurtSSSResolveProfileDiffuseColor(center.profileType, subsurfaceColor, diffuseLighting);
                return float4(BurtSSSVisualizeSignedDelta(blurredDiffuse, diffuseLighting, 4.0f) * setup.r, 1.0f);
            }

            if (_BurtSSSDebugMode > 29.5f && _BurtSSSDebugMode < 30.5f)
            {
                if (setup.r <= 0.0f || (center.profileType & BURT_SSS_PROFILE_TYPE_SEPARABLE) == 0u)
                {
                    float sourceDepth = tex2D(_BurtSSSSeparableInputTexture, input.screenUV).a;
                    return float4(BurtSSSSeparableFailureColor(input.screenUV, setup, center, sourceDepth) * 0.35f, 1.0f);
                }

                float4 subsurfaceColor = tex2D(_BurtSSSBlurTexture, input.screenUV);
                float3 blurredDiffuse = BurtSSSResolveProfileDiffuseColor(center.profileType, subsurfaceColor, diffuseLighting);
                return float4(BurtSSSVisualizeRelativeDelta(blurredDiffuse, diffuseLighting, 128.0f, 96.0f) * setup.r, 1.0f);
            }

            if (_BurtSSSDebugMode > 30.5f && _BurtSSSDebugMode < 31.5f)
            {
                return float4(BurtSSSSeparableSampleGateDebug(input.screenUV), 1.0f);
            }

            if (_BurtSSSDebugMode > 31.5f && _BurtSSSDebugMode < 32.5f)
            {
                if (setup.r <= 0.0f || (center.profileType & BURT_SSS_PROFILE_TYPE_SEPARABLE) == 0u)
                {
                    float sourceDepth = tex2D(_BurtSSSSeparableInputTexture, input.screenUV).a;
                    return float4(BurtSSSSeparableFailureColor(input.screenUV, setup, center, sourceDepth) * 0.35f, 1.0f);
                }

                float3 horizontalDiffuse = max(tex2D(_BurtSSSTempTexture, input.screenUV).rgb, float3(0.0f, 0.0f, 0.0f));
                return float4(BurtSSSVisualizeRelativeDelta(horizontalDiffuse, diffuseLighting, 128.0f, 96.0f) * setup.r, 1.0f);
            }

            if (_BurtSSSDebugMode > 34.5f && _BurtSSSDebugMode < 35.5f)
            {
                if (setup.r <= 0.0f || (center.profileType & (BURT_SSS_PROFILE_TYPE_BURLEY | BURT_SSS_PROFILE_TYPE_SEPARABLE)) == 0u)
                {
                    return BurtSSSSeparableChainColor(input.screenUV, setup, center);
                }

                return float4(max(diffuseLighting, float3(0.0f, 0.0f, 0.0f)) * setup.r, 1.0f);
            }

            if (_BurtSSSDebugMode > 35.5f && _BurtSSSDebugMode < 36.5f)
            {
                if (setup.r <= 0.0f || (center.profileType & BURT_SSS_PROFILE_TYPE_SEPARABLE) == 0u)
                {
                    float sourceDepth = tex2D(_BurtSSSSeparableInputTexture, input.screenUV).a;
                    return float4(BurtSSSSeparableFailureColor(input.screenUV, setup, center, sourceDepth) * 0.35f, 1.0f);
                }

                return float4(max(tex2D(_BurtSSSTempTexture, input.screenUV).rgb, float3(0.0f, 0.0f, 0.0f)), 1.0f);
            }

            if (_BurtSSSDebugMode > 36.5f && _BurtSSSDebugMode < 37.5f)
            {
                if (setup.r <= 0.0f || (center.profileType & BURT_SSS_PROFILE_TYPE_SEPARABLE) == 0u)
                {
                    float sourceDepth = tex2D(_BurtSSSSeparableInputTexture, input.screenUV).a;
                    return float4(BurtSSSSeparableFailureColor(input.screenUV, setup, center, sourceDepth) * 0.35f, 1.0f);
                }

                float horizontalDepth = tex2D(_BurtSSSTempTexture, input.screenUV).a;
                float visibleDepth = saturate(horizontalDepth / max(center.linearEyeDepth * 2.0f, 0.0001f));
                return float4(visibleDepth, visibleDepth, visibleDepth, 1.0f);
            }

            if (_BurtSSSDebugMode > 25.5f && _BurtSSSDebugMode < 26.5f)
            {
                if (setup.r <= 0.0f || (center.profileType & (BURT_SSS_PROFILE_TYPE_BURLEY | BURT_SSS_PROFILE_TYPE_SEPARABLE)) == 0u)
                {
                    return BurtSSSSeparableChainColor(input.screenUV, setup, center);
                }

                float4 subsurfaceColor = tex2D(_BurtSSSBlurTexture, input.screenUV);
                BurtSSSXRenderCombineData combine = BurtSSSEvaluateXRenderCombineData(input.screenUV, originalLit, subsurfaceColor, profile, center.profileType, setup.r);
                return float4(max(combine.subsurfaceLighting, float3(0.0f, 0.0f, 0.0f)) * setup.r, 1.0f);
            }

            if (_BurtSSSDebugMode > 26.5f && _BurtSSSDebugMode < 27.5f)
            {
                if (setup.r <= 0.0f || (center.profileType & (BURT_SSS_PROFILE_TYPE_BURLEY | BURT_SSS_PROFILE_TYPE_SEPARABLE)) == 0u)
                {
                    return BurtSSSSeparableChainColor(input.screenUV, setup, center);
                }

                float4 subsurfaceColor = tex2D(_BurtSSSBlurTexture, input.screenUV);
                BurtSSSXRenderCombineData combine = BurtSSSEvaluateXRenderCombineData(input.screenUV, originalLit, subsurfaceColor, profile, center.profileType, setup.r);
                return float4(combine.finalColor, 1.0f);
            }

            if (_BurtSSSDebugMode > 37.5f && _BurtSSSDebugMode < 38.5f)
            {
                if (setup.r <= 0.0f || (center.profileType & (BURT_SSS_PROFILE_TYPE_BURLEY | BURT_SSS_PROFILE_TYPE_SEPARABLE)) == 0u)
                {
                    return BurtSSSSeparableChainColor(input.screenUV, setup, center);
                }

                float4 subsurfaceColor = tex2D(_BurtSSSBlurTexture, input.screenUV);
                BurtSSSXRenderCombineData combine = BurtSSSEvaluateXRenderCombineData(input.screenUV, originalLit, subsurfaceColor, profile, center.profileType, setup.r);
                return float4(combine.diffuseFactor * setup.r, max(max(combine.profileTint.r, combine.profileTint.g), combine.profileTint.b) * setup.r, saturate(dot(abs(combine.sssColor - combine.diffuseLight), BURT_LUMINANCE_WEIGHTS) * 4.0f) * setup.r, 1.0f);
            }

            if (_BurtSSSDebugMode > 38.5f && _BurtSSSDebugMode < 39.5f)
            {
                if (setup.r <= 0.0f || (center.profileType & (BURT_SSS_PROFILE_TYPE_BURLEY | BURT_SSS_PROFILE_TYPE_SEPARABLE)) == 0u)
                {
                    float sourceDepth = tex2D(_BurtSSSSeparableInputTexture, input.screenUV).a;
                    return float4(BurtSSSSeparableFailureColor(input.screenUV, setup, center, sourceDepth) * 0.35f, 1.0f);
                }

                float4 centerKernel = BurtSSSUseProfileParamLut()
                    ? BurtSSSFetchProfileParam(BURT_SSS_PROFILE_PARAM_KERNEL0_OFFSET, center.profileIndex)
                    : float4(BURT_SSS_FALLBACK_KERNEL_CENTER, 0.0f);
                float centerWeight = max(max(centerKernel.r, centerKernel.g), centerKernel.b);
                float maxOffset = BurtSSSResolveSeparableMaxKernelOffset(center);
                float centerDepth = tex2D(_BurtSSSSeparableInputTexture, input.screenUV).a;
                float radiusPixels = centerDepth > 0.0f ? BurtSSSResolveSeparableMaxOffsetPixelsFromDepth(center, profile, centerDepth) : 0.0f;
                return float4(saturate(centerWeight * 4.0f) * setup.r, saturate(maxOffset * 1024.0f) * setup.r, saturate(radiusPixels / 32.0f) * setup.r, 1.0f);
            }

            if (_BurtSSSDebugMode > 39.5f && _BurtSSSDebugMode < 40.5f)
            {
                if (setup.r <= 0.0f || (center.profileType & (BURT_SSS_PROFILE_TYPE_BURLEY | BURT_SSS_PROFILE_TYPE_SEPARABLE)) == 0u)
                {
                    return BurtSSSSeparableChainColor(input.screenUV, setup, center);
                }

                float4 subsurfaceColor = tex2D(_BurtSSSBlurTexture, input.screenUV);
                BurtSSSXRenderCombineData combine = BurtSSSEvaluateXRenderCombineData(input.screenUV, originalLit, subsurfaceColor, profile, center.profileType, setup.r);
                float3 reference = max(combine.diffuseLight, float3(0.0f, 0.0f, 0.0f));
                float3 delta = abs(combine.sssColor - reference);
                float referenceLuma = max(dot(reference, BURT_LUMINANCE_WEIGHTS), 0.025f);
                return float4(saturate(delta / referenceLuma * 8.0f) * setup.r, 1.0f);
            }

            if (_BurtSSSDebugMode > 40.5f && _BurtSSSDebugMode < 41.5f)
            {
                if (setup.r <= 0.0f || (center.profileType & BURT_SSS_PROFILE_TYPE_SEPARABLE) == 0u)
                {
                    float sourceDepth = tex2D(_BurtSSSSeparableInputTexture, input.screenUV).a;
                    return float4(BurtSSSSeparableFailureColor(input.screenUV, setup, center, sourceDepth) * 0.35f, 1.0f);
                }

                return float4(BurtSSSResolveProfileKernelSpreadColor(center) * setup.r, 1.0f);
            }

            if (_BurtSSSDebugMode > 41.5f && _BurtSSSDebugMode < 42.5f)
            {
                if (setup.r <= 0.0f || (center.profileType & (BURT_SSS_PROFILE_TYPE_BURLEY | BURT_SSS_PROFILE_TYPE_SEPARABLE)) == 0u)
                {
                    return BurtSSSSeparableChainColor(input.screenUV, setup, center);
                }

                float4 subsurfaceColor = tex2D(_BurtSSSBlurTexture, input.screenUV);
                BurtSSSXRenderCombineData combine = BurtSSSEvaluateXRenderCombineData(input.screenUV, originalLit, subsurfaceColor, profile, center.profileType, setup.r);
                return float4(BurtSSSVisualizeSignedDelta(combine.finalColor, max(originalLit.rgb, float3(0.0f, 0.0f, 0.0f)), 4.0f) * setup.r, 1.0f);
            }

            if (_BurtSSSDebugMode > 42.5f && _BurtSSSDebugMode < 43.5f)
            {
                if (setup.r <= 0.0f || (center.profileType & (BURT_SSS_PROFILE_TYPE_BURLEY | BURT_SSS_PROFILE_TYPE_SEPARABLE)) == 0u)
                {
                    return BurtSSSSeparableChainColor(input.screenUV, setup, center);
                }

                float4 subsurfaceColor = tex2D(_BurtSSSBlurTexture, input.screenUV);
                BurtSSSXRenderCombineData combine = BurtSSSEvaluateXRenderCombineData(input.screenUV, originalLit, subsurfaceColor, profile, center.profileType, setup.r);
                float3 originalDiffuseFinal = combine.diffuseLight * combine.baseColor;
                float3 subsurfaceDiffuseFinal = combine.subsurfaceLighting * combine.baseColor;
                return float4(BurtSSSVisualizeSignedDelta(subsurfaceDiffuseFinal, originalDiffuseFinal, 4.0f) * setup.r, 1.0f);
            }

            if (_BurtSSSDebugMode > 44.5f && _BurtSSSDebugMode < 45.5f)
            {
                if (setup.r <= 0.0f || (center.profileType & (BURT_SSS_PROFILE_TYPE_BURLEY | BURT_SSS_PROFILE_TYPE_SEPARABLE)) == 0u)
                {
                    return BurtSSSSeparableChainColor(input.screenUV, setup, center);
                }

                float4 subsurfaceColor = tex2D(_BurtSSSBlurTexture, input.screenUV);
                BurtSSSXRenderCombineData combine = BurtSSSEvaluateXRenderCombineData(input.screenUV, originalLit, subsurfaceColor, profile, center.profileType, setup.r);
                return float4(BurtSSSVisualizeSignedDelta(combine.sssColor, combine.diffuseLight, 4.0f) * setup.r, 1.0f);
            }

            if (_BurtSSSDebugMode > 45.5f && _BurtSSSDebugMode < 46.5f)
            {
                if (setup.r <= 0.0f || (center.profileType & (BURT_SSS_PROFILE_TYPE_BURLEY | BURT_SSS_PROFILE_TYPE_SEPARABLE)) == 0u)
                {
                    return BurtSSSSeparableChainColor(input.screenUV, setup, center);
                }

                float4 subsurfaceColor = tex2D(_BurtSSSBlurTexture, input.screenUV);
                BurtSSSXRenderCombineData combine = BurtSSSEvaluateXRenderCombineData(input.screenUV, originalLit, subsurfaceColor, profile, center.profileType, setup.r);
                return float4(BurtSSSVisualizeSignedDelta(combine.subsurfaceLighting, combine.diffuseLight, 4.0f) * setup.r, 1.0f);
            }

            if (_BurtSSSDebugMode > 46.5f && _BurtSSSDebugMode < 47.5f)
            {
                float setupValid = saturate(setup.r);
                float typeValid = (center.profileType & BURT_SSS_PROFILE_TYPE_SEPARABLE) != 0u ? 1.0f : 0.0f;
                float surfaceValid = center.valid > 0.0f ? 1.0f : 0.0f;
                float sourceDepth = tex2D(_BurtSSSSeparableInputTexture, input.screenUV).a;
                float depthValid = sourceDepth > 0.0f ? 1.0f : 0.0f;
                float4 chain = BurtSSSSeparableChainColor(input.screenUV, setup, center);
                float validAny = max(max(setupValid, typeValid * surfaceValid), depthValid);
                return validAny > 0.0f ? float4(setupValid, typeValid * surfaceValid, depthValid, 1.0f) : chain;
            }

            if (_BurtSSSDebugMode > 47.5f && _BurtSSSDebugMode < 48.5f)
            {
                if (setup.r <= 0.0f || (center.profileType & BURT_SSS_PROFILE_TYPE_SEPARABLE) == 0u)
                {
                    float sourceDepth = tex2D(_BurtSSSSeparableInputTexture, input.screenUV).a;
                    return float4(BurtSSSSeparableFailureColor(input.screenUV, setup, center, sourceDepth) * 0.35f, 1.0f);
                }

                float4 horizontal = tex2D(_BurtSSSTempTexture, input.screenUV);
                float4 vertical = tex2D(_BurtSSSBlurTexture, input.screenUV);
                float horizontalDepth = horizontal.a > 0.0f ? 1.0f : 0.0f;
                float verticalAlpha = vertical.a > 0.00001f ? 1.0f : 0.0f;
                float3 horizontalDiffuse = max(horizontal.rgb, float3(0.0f, 0.0f, 0.0f));
                float3 verticalDiffuse = verticalAlpha > 0.5f ? BurtSSSResolveProfileDiffuseColor(center.profileType, vertical, horizontalDiffuse) : horizontalDiffuse;
                float verticalDelta = saturate(dot(abs(verticalDiffuse - horizontalDiffuse), BURT_LUMINANCE_WEIGHTS) * 4.0f);
                return float4(horizontalDepth * setup.r, verticalAlpha * setup.r, verticalDelta * setup.r, 1.0f);
            }

            if (_BurtSSSDebugMode > 48.5f && _BurtSSSDebugMode < 49.5f)
            {
                float setupValid = saturate(setup.r);
                float typeValid = (center.profileType & BURT_SSS_PROFILE_TYPE_SEPARABLE) != 0u ? 1.0f : 0.0f;
                float surfaceValid = center.valid > 0.0f ? 1.0f : 0.0f;
                float4 horizontal = tex2D(_BurtSSSTempTexture, input.screenUV);
                float4 vertical = tex2D(_BurtSSSBlurTexture, input.screenUV);
                float horizontalDepthValid = horizontal.a > 0.0f ? 1.0f : 0.0f;
                float verticalAlphaValid = vertical.a > 0.00001f ? 1.0f : 0.0f;
                float stageValid = setupValid * typeValid * surfaceValid * horizontalDepthValid;
                float3 horizontalDiffuse = max(horizontal.rgb, float3(0.0f, 0.0f, 0.0f));
                float3 verticalDiffuse = BurtSSSResolveProfileDiffuseColor(center.profileType, vertical, diffuseLighting);
                float horizontalActivity = saturate(dot(abs(horizontalDiffuse - diffuseLighting), BURT_LUMINANCE_WEIGHTS) * 8.0f);
                float verticalActivity = saturate(dot(abs(verticalDiffuse - horizontalDiffuse), BURT_LUMINANCE_WEIGHTS) * 8.0f);
                if (setupValid <= 0.0f || typeValid <= 0.0f || surfaceValid <= 0.0f)
                {
                    return BurtSSSSeparableChainColor(input.screenUV, setup, center);
                }

                return float4(
                    stageValid,
                    setupValid * typeValid * horizontalDepthValid * lerp(0.25f, 1.0f, horizontalActivity),
                    setupValid * typeValid * verticalAlphaValid * lerp(0.25f, 1.0f, verticalActivity),
                    1.0f);
            }

            if (_BurtSSSDebugMode > 49.5f && _BurtSSSDebugMode < 50.5f)
            {
                return BurtSSSSeparableChainColor(input.screenUV, setup, center);
            }

            if (_BurtSSSDebugMode > 50.5f && _BurtSSSDebugMode < 51.5f)
            {
                if (setup.r <= 0.0f || (center.profileType & (BURT_SSS_PROFILE_TYPE_BURLEY | BURT_SSS_PROFILE_TYPE_SEPARABLE)) == 0u)
                {
                    return BurtSSSSeparableChainColor(input.screenUV, setup, center);
                }

                float4 subsurfaceColor = tex2D(_BurtSSSBlurTexture, input.screenUV);
                BurtSSSXRenderCombineData combine = BurtSSSEvaluateXRenderCombineData(input.screenUV, originalLit, subsurfaceColor, profile, center.profileType, setup.r);
                int2 pixel = BurtSSSClampPixel(input.screenUV);
                float localX = frac((pixel.x + pixel.y * 0.25f) / 54.0f);
                float band = floor(localX * 3.0f);
                float bandX = frac(localX * 3.0f);
                float edge = step(0.045f, bandX) * step(bandX, 0.955f);
                float3 leftProfileTint = combine.profileTint;
                float3 middleSSSColor = combine.sssColor / (1.0f + combine.sssColor);
                float3 rightSubsurfaceLighting = combine.subsurfaceLighting / (1.0f + combine.subsurfaceLighting);
                float3 triplet = band < 0.5f
                    ? leftProfileTint
                    : (band < 1.5f ? middleSSSColor : rightSubsurfaceLighting);
                return float4(triplet * edge, 1.0f);
            }

            if (_BurtSSSDebugMode < 8.5f)
            {
                float3 transmission = max(profile.transmissionTint.rgb, float3(0.0f, 0.0f, 0.0f)) * saturate(profile.transmission.y) * saturate(center.thickness);
                return float4(transmission * setup.r, 1.0f);
            }

            if (_BurtSSSDebugMode < 9.5f)
            {
                return float4(diffuseLighting, 1.0f);
            }

            if (_BurtSSSDebugMode < 10.5f)
            {
                float3 exposedSpecular = max(specularLight, float3(0.0f, 0.0f, 0.0f)) * 8.0f;
                return float4(exposedSpecular / (1.0f + exposedSpecular), 1.0f);
            }

            if (_BurtSSSDebugMode > 15.5f && _BurtSSSDebugMode < 16.5f)
            {
                return float4(max(originalLit.rgb, float3(0.0f, 0.0f, 0.0f)), 1.0f);
            }

            if (_BurtSSSDebugMode > 16.5f && _BurtSSSDebugMode < 17.5f)
            {
                float diffuseLuminance = max(originalLit.a, 0.0f);
                return float4(diffuseLuminance, diffuseLuminance, diffuseLuminance, 1.0f);
            }

            if (_BurtSSSDebugMode > 17.5f && _BurtSSSDebugMode < 18.5f)
            {
                return float4(baseColor, 1.0f);
            }

            if (_BurtSSSDebugMode > 18.5f && _BurtSSSDebugMode < 19.5f)
            {
                float3 exposedEmission = emission * 0.25f;
                return float4(exposedEmission / (1.0f + exposedEmission), 1.0f);
            }

            if (_BurtSSSDebugMode > 19.5f && _BurtSSSDebugMode < 20.5f)
            {
                return float4(diffuseWithBaseColor, 1.0f);
            }

            if (_BurtSSSDebugMode > 20.5f && _BurtSSSDebugMode < 21.5f)
            {
                return float4(max(specularLight, float3(0.0f, 0.0f, 0.0f)), 1.0f);
            }

            if (_BurtSSSDebugMode > 21.5f && _BurtSSSDebugMode < 22.5f)
            {
                if (setup.r <= 0.0f || (center.profileType & (BURT_SSS_PROFILE_TYPE_BURLEY | BURT_SSS_PROFILE_TYPE_SEPARABLE)) == 0u)
                {
                    return BurtSSSSeparableChainColor(input.screenUV, setup, center);
                }

                float deltaSum = 0.0f;
                float weightSum = 0.0f;
                float2 texel = _BurtSSSScreenSize.zw;
                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 sampleUV = saturate(input.screenUV + float2((float)x, (float)y) * texel);
                        float sampleMask = BurtSSSLoadSetup(sampleUV).r;
                        float3 combinedSample = max(tex2D(_BurtSSSCombineTexture, sampleUV).rgb, float3(0.0f, 0.0f, 0.0f));
                        float3 originalSample = max(tex2D(_BurtSSSOriginalTexture, sampleUV).rgb, float3(0.0f, 0.0f, 0.0f));
                        float sampleWeight = x == 0 && y == 0 ? 2.0f : (abs(x) + abs(y) == 1 ? 1.0f : 0.5f);
                        deltaSum += abs(dot(combinedSample - originalSample, BURT_LUMINANCE_WEIGHTS)) * sampleMask * sampleWeight;
                        weightSum += sampleMask * sampleWeight;
                    }
                }

                float3 combined = max(tex2D(_BurtSSSCombineTexture, input.screenUV).rgb, float3(0.0f, 0.0f, 0.0f));
                float originalLum = dot(max(originalLit.rgb, float3(0.0f, 0.0f, 0.0f)), BURT_LUMINANCE_WEIGHTS);
                float combinedLum = dot(combined, BURT_LUMINANCE_WEIGHTS);
                float normalizedDelta = saturate((deltaSum / max(weightSum, 0.0001f)) / max(max(originalLum, combinedLum), 0.025f) * 2.0f);
                float3 low = lerp(float3(0.015f, 0.02f, 0.028f), float3(0.06f, 0.22f, 0.72f), saturate(normalizedDelta * 2.0f));
                float3 high = lerp(float3(0.95f, 0.72f, 0.08f), float3(1.0f, 0.08f, 0.03f), saturate((normalizedDelta - 0.65f) * 2.857f));
                float3 heat = lerp(low, high, saturate((normalizedDelta - 0.35f) * 2.5f));
                return float4(heat * setup.r, 1.0f);
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
                return float4(max(history.rgb, float3(0.0f, 0.0f, 0.0f)) * historyValid, 1.0f);
            }

            if (_BurtSSSDebugMode < 13.5f)
            {
                float variance = saturate(history.a / max(_BurtSSSHistoryDebugParams.w * 16.0f, 0.000001f)) * historyValid;
                return float4(variance, variance, variance, 1.0f);
            }

            float historyAge = saturate(_BurtSSSHistoryDebugParams.y / 64.0f) * historyValid;
            return float4(historyValid, historyAge, saturate(history.a / max(_BurtSSSHistoryDebugParams.w * 16.0f, 0.000001f)), 1.0f);
        }

        float4 FragDebugImportant(Varyings input) : SV_Target
        {
            float2 uv = input.screenUV;
            float4 setup = BurtSSSLoadSetup(uv);

            if (_BurtSSSDebugMode < 1.5f)
            {
                return float4(setup.r, setup.g, setup.b, 1.0f);
            }

            if (_BurtSSSDebugMode < 2.5f)
            {
                float mask = tex2D(_BurtSSSMaskTexture, uv).r;
                return float4(mask, mask, mask, 1.0f);
            }

            if (_BurtSSSDebugMode > 3.5f && _BurtSSSDebugMode < 4.5f)
            {
                return float4(max(tex2D(_BurtSSSBlurTexture, uv).rgb, float3(0.0f, 0.0f, 0.0f)), 1.0f);
            }

            if (_BurtSSSDebugMode > 4.5f && _BurtSSSDebugMode < 5.5f)
            {
                return tex2D(_BurtSSSCombineTexture, uv);
            }

            BurtSSSSurface center = BurtSSSLoadSurface(uv);
            if (_BurtSSSDebugMode > 14.5f && _BurtSSSDebugMode < 15.5f)
            {
                float isBurley = (center.profileType & BURT_SSS_PROFILE_TYPE_BURLEY) != 0u ? 1.0f : 0.0f;
                float isSeparable = (center.profileType & BURT_SSS_PROFILE_TYPE_SEPARABLE) != 0u ? 1.0f : 0.0f;
                return float4(isBurley * setup.r, isSeparable * setup.r, 0.0f, 1.0f);
            }

            if (_BurtSSSDebugMode > 27.5f && _BurtSSSDebugMode < 28.5f)
            {
                float blurAlpha = tex2D(_BurtSSSBlurTexture, uv).a;
                return float4(blurAlpha, blurAlpha, blurAlpha, 1.0f);
            }

            BurtSSSProfile profile = BurtSSSLoadProfile(center.profileIndex);
            if (_BurtSSSDebugMode > 28.5f && _BurtSSSDebugMode < 29.5f)
            {
                float valid = setup.r * ((center.profileType & BURT_SSS_PROFILE_TYPE_SEPARABLE) != 0u ? 1.0f : 0.0f);
                float centerDepth = tex2D(_BurtSSSSeparableInputTexture, uv).a;
                if (valid <= 0.0f || centerDepth <= 0.0f)
                {
                    return float4(BurtSSSSeparableFailureColor(uv, setup, center, centerDepth) * 0.35f, 1.0f);
                }

                float radiusPixels = BurtSSSResolveSeparableMaxOffsetPixelsFromDepth(center, profile, centerDepth);
                float visibleRadius = saturate(radiusPixels / 32.0f) * valid;
                return float4(visibleRadius, visibleRadius, visibleRadius, 1.0f);
            }

            uint profileIndex;
            uint profileType;
            BurtSSSDecodeProfileIDAndType(BurtSSSLoadProfileIDAndType(uv), profileIndex, profileType);
            if (_BurtSSSDebugMode > 43.5f && _BurtSSSDebugMode < 44.5f)
            {
                float rawProfileIndex = (profileType & (BURT_SSS_PROFILE_TYPE_BURLEY | BURT_SSS_PROFILE_TYPE_SEPARABLE)) != 0u
                    ? (float)profileIndex
                    : center.profileIndex;
                BurtSSSProfile rawProfile = BurtSSSLoadProfile(rawProfileIndex);
                return float4(saturate(rawProfile.tint.rgb), 1.0f);
            }

            if (_BurtSSSDebugMode > 46.5f && _BurtSSSDebugMode < 47.5f)
            {
                float setupValid = saturate(setup.r);
                float typeValid = (center.profileType & BURT_SSS_PROFILE_TYPE_SEPARABLE) != 0u ? 1.0f : 0.0f;
                float surfaceValid = center.valid > 0.0f ? 1.0f : 0.0f;
                float sourceDepth = tex2D(_BurtSSSSeparableInputTexture, uv).a;
                float depthValid = sourceDepth > 0.0f ? 1.0f : 0.0f;
                float4 chain = BurtSSSSeparableChainColor(uv, setup, center);
                float validAny = max(max(setupValid, typeValid * surfaceValid), depthValid);
                return validAny > 0.0f ? float4(setupValid, typeValid * surfaceValid, depthValid, 1.0f) : chain;
            }

            if (_BurtSSSDebugMode > 47.5f && _BurtSSSDebugMode < 48.5f)
            {
                if (setup.r <= 0.0f || (center.profileType & BURT_SSS_PROFILE_TYPE_SEPARABLE) == 0u)
                {
                    float sourceDepth = tex2D(_BurtSSSSeparableInputTexture, uv).a;
                    return float4(BurtSSSSeparableFailureColor(uv, setup, center, sourceDepth) * 0.35f, 1.0f);
                }

                float4 horizontal = tex2D(_BurtSSSTempTexture, uv);
                float4 vertical = tex2D(_BurtSSSBlurTexture, uv);
                float horizontalDepth = horizontal.a > 0.0f ? 1.0f : 0.0f;
                float verticalAlpha = vertical.a > 0.00001f ? 1.0f : 0.0f;
                float3 horizontalDiffuse = max(horizontal.rgb, float3(0.0f, 0.0f, 0.0f));
                float3 verticalDiffuse = verticalAlpha > 0.5f ? BurtSSSResolveProfileDiffuseColor(center.profileType, vertical, horizontalDiffuse) : horizontalDiffuse;
                float verticalDelta = saturate(dot(abs(verticalDiffuse - horizontalDiffuse), BURT_LUMINANCE_WEIGHTS) * 4.0f);
                return float4(horizontalDepth * setup.r, verticalAlpha * setup.r, verticalDelta * setup.r, 1.0f);
            }

            if (_BurtSSSDebugMode > 49.5f && _BurtSSSDebugMode < 50.5f)
            {
                return BurtSSSSeparableChainColor(uv, setup, center);
            }

            float4 originalLit = tex2D(_BurtSSSOriginalTexture, uv);
            float3 baseColor;
            float3 emission;
            float3 diffuseWithBaseColor;
            float3 diffuseLighting;
            float3 specularLight;
            BurtSSSDecodeLightingComponents(uv, originalLit, baseColor, emission, diffuseWithBaseColor, diffuseLighting, specularLight);

            if (_BurtSSSDebugMode > 29.5f && _BurtSSSDebugMode < 30.5f)
            {
                if (setup.r <= 0.0f || (center.profileType & BURT_SSS_PROFILE_TYPE_SEPARABLE) == 0u)
                {
                    float sourceDepth = tex2D(_BurtSSSSeparableInputTexture, uv).a;
                    return float4(BurtSSSSeparableFailureColor(uv, setup, center, sourceDepth) * 0.35f, 1.0f);
                }

                float4 subsurfaceColor = tex2D(_BurtSSSBlurTexture, uv);
                float3 blurredDiffuse = BurtSSSResolveProfileDiffuseColor(center.profileType, subsurfaceColor, diffuseLighting);
                return float4(BurtSSSVisualizeRelativeDelta(blurredDiffuse, diffuseLighting, 128.0f, 96.0f) * setup.r, 1.0f);
            }

            if (_BurtSSSDebugMode > 26.5f && _BurtSSSDebugMode < 27.5f)
            {
                if (setup.r <= 0.0f || (center.profileType & (BURT_SSS_PROFILE_TYPE_BURLEY | BURT_SSS_PROFILE_TYPE_SEPARABLE)) == 0u)
                {
                    return BurtSSSSeparableChainColor(uv, setup, center);
                }

                float4 subsurfaceColor = tex2D(_BurtSSSBlurTexture, uv);
                BurtSSSXRenderCombineData combine = BurtSSSEvaluateXRenderCombineData(uv, originalLit, subsurfaceColor, profile, center.profileType, setup.r);
                return float4(combine.finalColor, 1.0f);
            }

            if (_BurtSSSDebugMode > 13.5f && _BurtSSSDebugMode < 14.5f)
            {
                float4 history = tex2D(_BurtSSSHistoryDebugTexture, uv);
                float historyValid = saturate(_BurtSSSHistoryDebugParams.x);
                float historyAge = saturate(_BurtSSSHistoryDebugParams.y / 64.0f) * historyValid;
                return float4(historyValid, historyAge, saturate(history.a / max(_BurtSSSHistoryDebugParams.w * 16.0f, 0.000001f)), 1.0f);
            }

            return float4(setup.r, setup.r, setup.r, 1.0f);
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
            Name "Burt Screen Space Subsurface Coarse Mask"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragCoarseMask
            ENDHLSL
        }

        Pass
        {
            Name "Burt Screen Space Subsurface Combine"
            Cull Off
            ZWrite Off
            ZTest Always

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
            #pragma fragment FragDebugImportant
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
