Shader "Hidden/BurtRP/VolumetricFog"
{
    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        Pass
        {
            Name "Burt Volumetric Fog"

            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtPreExposure.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/BurtAtmosphereLut.hlsl"
            // BurtShadows owns the guarded _BurtMainLightDirection declaration; do not predefine its guard here.
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtShadows.hlsl"

            sampler2D _BurtCameraColorTexture;
            UNITY_DECLARE_DEPTH_TEXTURE(_BurtCameraDepthTexture);
            sampler3D _BurtVolumetricFogIntegratedLut;
            sampler2D _BurtVolumetricFogMapTexture;
            sampler3D _BurtVolumetricFogTranslucencyVolume0;
            sampler3D _BurtVolumetricFogTranslucencyVolume1;

            float4 _BurtVolumetricFogParams; // x=visible distance, y=start distance, z=step count, w=max opacity
            float4 _BurtVolumetricFogDensityParams; // x=height, y=density, z=height falloff, w=extinction scale
            float4 _BurtVolumetricFogSecondDensityParams; // x=absolute height, y=density, z=height falloff
            float4 _BurtVolumetricFogMapWorldParams; // xy=world center XZ, zw=inverse coverage XZ
            float4 _BurtVolumetricFogMapAltitudeParams; // xy=min/max altitude, z=enabled
            float4 _BurtVolumetricFogScatteringParams; // x=anisotropy, y=direct, z=ambient, w=jitter
            float _BurtVolumetricFogAtmosphereScatteringEnabled;
            float4 _BurtVolumetricFogAtmosphereRayleighTintScale;
            float4 _BurtVolumetricFogAtmosphereMieTintScale;
            float4 _BurtVolumetricFogAtmosphereMultipleScatteringTintScale;
            float4 _BurtVolumetricFogAlbedo;
            float4x4 _BurtVolumetricFogInverseViewProjection;
            float3 _BurtVolumetricFogCameraPositionWS;
            float _BurtVolumetricFogDebugMode;
            float4 _BurtVolumetricFogFrameParams;
            float4 _BurtVolumetricFogTranslucencyGIParams; // x=use current filtered translucency GI
            float4 _BurtGITranslucencyVolumeGridSize; // xyz=grid size, w=material intensity scale
            float4 _BurtGITranslucencyVolumeGridZParams; // x=log scale, y=log bias, z=slice scale
            float4 _BurtMainLightColor;
            float4 _BurtMainLightColorOuterSpace;
            float4 _BurtMainLightAtmosphereTransmittance;
            float4 _BurtAtmosphereHorizontalFogSunDirection;
            float4 _BurtAtmosphereHorizontalFogLightColor;
            float _BurtMainLightOcclusionFactor;
            float _BurtAtmosphereHorizontalFogUsesMainLight;
            float4 _BurtAmbientSHAr;
            float4 _BurtAmbientSHAg;
            float4 _BurtAmbientSHAb;
            float4 _BurtAmbientSHBr;
            float4 _BurtAmbientSHBg;
            float4 _BurtAmbientSHBb;
            float _BurtAmbientSHEnabled;
            float _BurtAdditionalLightCount;

            #define BURT_MAX_ADDITIONAL_LIGHTS 8
            #define BURT_ADDITIONAL_LIGHT_BUFFER_ROWS 4

            float4 _BurtAdditionalLightPositionAndRange[BURT_MAX_ADDITIONAL_LIGHTS];
            float4 _BurtAdditionalLightColorAndType[BURT_MAX_ADDITIONAL_LIGHTS];
            float4 _BurtAdditionalLightDirectionAndSpot[BURT_MAX_ADDITIONAL_LIGHTS];
            float4 _BurtAdditionalLightSpotParams[BURT_MAX_ADDITIONAL_LIGHTS];
            StructuredBuffer<float4> _BurtAdditionalLightBuffer;
            float _BurtAdditionalLightBufferEnabled;
            float _BurtVolumetricFogIntegratedEnabled;
            float4 _BurtVolumetricFogIntegratedGridZParams;
            float4 _BurtVolumetricFogIntegratedSamplingParams;

            static const float PI = 3.14159265359f;

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

            bool IsSkyPixel(float rawDepth)
            {
                #if defined(UNITY_REVERSED_Z)
                    return rawDepth <= 0.00001f;
                #else
                    return rawDepth >= 0.99999f;
                #endif
            }

            float3 SafeNormalize(float3 value, float3 fallback)
            {
                float lengthSq = dot(value, value);
                return lengthSq > 1.0e-8f ? value * rsqrt(lengthSq) : fallback;
            }

            float4 BuildClipPosition(float2 screenUV, float rawDepth)
            {
                float2 clipXY = screenUV * 2.0f - 1.0f;
                #if UNITY_UV_STARTS_AT_TOP
                    clipXY.y = -clipXY.y;
                #endif

                #if defined(UNITY_REVERSED_Z)
                    float clipZ = rawDepth;
                #else
                    float clipZ = lerp(UNITY_NEAR_CLIP_VALUE, 1.0f, rawDepth);
                #endif

                return float4(clipXY, clipZ, 1.0f);
            }

            float3 ReconstructPositionWS(float2 screenUV, float rawDepth)
            {
                float4 positionWS = mul(_BurtVolumetricFogInverseViewProjection, BuildClipPosition(screenUV, rawDepth));
                positionWS.xyz /= max(abs(positionWS.w), 1.0e-6f);
                return positionWS.xyz;
            }

            float4 SampleIntegratedVolumetricFog(float2 screenUV, float viewDepth)
            {
                float b = _BurtVolumetricFogIntegratedGridZParams.x;
                float o = _BurtVolumetricFogIntegratedGridZParams.y;
                float s = max(_BurtVolumetricFogIntegratedGridZParams.z, 1.0e-4f);
                float totalSliceCount = max(_BurtVolumetricFogIntegratedGridZParams.w, 1.0f);
                float zSlice = log2(max(viewDepth * b + o, 1.0e-6f)) * s;
                float lastVisibleSliceCenter = max(_BurtVolumetricFogIntegratedSamplingParams.x - 0.5f, 0.5f);
                zSlice = clamp(zSlice, 0.5f, lastVisibleSliceCenter);
                return tex3D(
                    _BurtVolumetricFogIntegratedLut,
                    float3(saturate(screenUV), saturate(zSlice / totalSliceCount)));
            }

            float HenyeyGreensteinPhase(float cosTheta, float g)
            {
                float g2 = g * g;
                float denom = max(0.05f, pow(abs(1.0f + g2 - 2.0f * g * cosTheta), 1.5f));
                return (1.0f - g2) / (4.0f * PI * denom);
            }

            float3 EvaluateSkyAmbientScattering(float3 directionToCamera, float phaseG)
            {
                float3 direction = SafeNormalize(directionToCamera, float3(0.0f, 0.0f, -1.0f));
                float3 c6 = float3(_BurtAmbientSHBr.z, _BurtAmbientSHBg.z, _BurtAmbientSHBb.z) / 3.0f;
                float3 l0 = float3(_BurtAmbientSHAr.w, _BurtAmbientSHAg.w, _BurtAmbientSHAb.w) + c6;
                float3 l1 = float3(
                    dot(_BurtAmbientSHAr.xyz, direction),
                    dot(_BurtAmbientSHAg.xyz, direction),
                    dot(_BurtAmbientSHAb.xyz, direction));
                float3 skyScattering = max(
                    0.0f,
                    l0 / PI + l1 * (phaseG * (3.0f / (2.0f * PI))));
                return lerp(1.0f, skyScattering, saturate(_BurtAmbientSHEnabled));
            }

            float3 EvaluateTranslucencyGIAmbientScattering(
                float2 screenUV,
                float3 positionWS,
                float3 cameraToSampleDirection,
                float phaseG)
            {
                float viewDepth = max(-mul(UNITY_MATRIX_V, float4(positionWS, 1.0f)).z, 0.00001f);
                float normalizedSlice = log2(
                    viewDepth * _BurtGITranslucencyVolumeGridZParams.x
                    + _BurtGITranslucencyVolumeGridZParams.y)
                    * _BurtGITranslucencyVolumeGridZParams.z
                    / max(_BurtGITranslucencyVolumeGridSize.z, 1.0f);
                float3 volumeUVW = saturate(float3(screenUV, normalizedSlice));
                float3 ambientLighting = tex3Dlod(
                    _BurtVolumetricFogTranslucencyVolume0,
                    float4(volumeUVW, 0.0f)).rgb;
                float3 directionalLighting = tex3Dlod(
                    _BurtVolumetricFogTranslucencyVolume1,
                    float4(volumeUVW, 0.0f)).rgb;
                float inverseMaterialScale = rcp(max(_BurtGITranslucencyVolumeGridSize.w, 0.00001f));
                ambientLighting *= inverseMaterialScale;
                directionalLighting *= inverseMaterialScale;
                float ambientLuminance = dot(ambientLighting, float3(0.2126729f, 0.7151522f, 0.0721750f));
                float3 normalizedAmbientColor = ambientLighting / (ambientLuminance + 0.00001f);
                float3 rotatedHG = float3(
                    cameraToSampleDirection.y,
                    cameraToSampleDirection.z,
                    cameraToSampleDirection.x) * phaseG;
                return max(
                    0.0f,
                    ambientLighting + normalizedAmbientColor * dot(directionalLighting, rotatedHG));
            }

            float InterleavedGradientNoise(float2 pixelPosition, float frameIndex)
            {
                float3 magic = float3(0.06711056f, 0.00583715f, 52.9829189f);
                return frac(magic.z * frac(dot(pixelPosition + frameIndex * 0.37f, magic.xy)));
            }

            float3 NormalizeLightColor(float3 lightColor)
            {
                float peak = max(max(lightColor.r, lightColor.g), lightColor.b);
                return peak > 0.001f ? lightColor / peak : 1.0f;
            }

            float SampleHeightDensity(float3 positionWS)
            {
                float fogHeight = _BurtVolumetricFogDensityParams.x;
                float density = max(_BurtVolumetricFogDensityParams.y, 0.0f);
                float falloff = max(_BurtVolumetricFogDensityParams.z, 0.001f);
                float extinctionScale = max(_BurtVolumetricFogDensityParams.w, 0.0f);
                float extinction = density * exp2(-max(-127.0f, falloff * (positionWS.y - fogHeight)));
                float secondFogHeight = _BurtVolumetricFogSecondDensityParams.x;
                float secondDensity = max(_BurtVolumetricFogSecondDensityParams.y, 0.0f);
                float secondFalloff = max(_BurtVolumetricFogSecondDensityParams.z, 0.0f);
                extinction += secondDensity * exp2(
                    -max(-127.0f, secondFalloff * (positionWS.y - secondFogHeight)));
                if (_BurtVolumetricFogMapAltitudeParams.z > 0.5f)
                {
                    float2 fogMapUV = saturate(
                        (positionWS.xz - _BurtVolumetricFogMapWorldParams.xy)
                        * _BurtVolumetricFogMapWorldParams.zw
                        + 0.5f);
                    float3 fogMapTexel = tex2Dlod(
                        _BurtVolumetricFogMapTexture,
                        float4(fogMapUV, 0.0f, 0.0f)).rgb;
                    float minimumAltitude = _BurtVolumetricFogMapAltitudeParams.x;
                    float maximumAltitude = max(_BurtVolumetricFogMapAltitudeParams.y, minimumAltitude);
                    float mapFogHeight = lerp(minimumAltitude, maximumAltitude, fogMapTexel.r);
                    float mapFalloffRate = rcp(max(0.5f, fogMapTexel.g * 300.0f));
                    float mapDensity = max(fogMapTexel.b * 0.1f, 0.0f);
                    extinction += mapDensity * exp2(
                        -mapFalloffRate * max(positionWS.y - mapFogHeight, 0.0f));
                }

                return max(extinction * extinctionScale, 0.0f);
            }

            float3 DistanceDebugColor(float value)
            {
                value = saturate(value);
                return lerp(float3(0.02f, 0.05f, 0.12f), float3(0.25f, 0.72f, 1.0f), value);
            }

            int GetAdditionalLightCount()
            {
                return min((int)round(max(_BurtAdditionalLightCount, 0.0f)), BURT_MAX_ADDITIONAL_LIGHTS);
            }

            float4 ReadAdditionalLightPositionAndRange(int lightIndex)
            {
                return _BurtAdditionalLightBufferEnabled > 0.5f
                    ? _BurtAdditionalLightBuffer[lightIndex * BURT_ADDITIONAL_LIGHT_BUFFER_ROWS]
                    : _BurtAdditionalLightPositionAndRange[lightIndex];
            }

            float4 ReadAdditionalLightColorAndType(int lightIndex)
            {
                return _BurtAdditionalLightBufferEnabled > 0.5f
                    ? _BurtAdditionalLightBuffer[lightIndex * BURT_ADDITIONAL_LIGHT_BUFFER_ROWS + 1]
                    : _BurtAdditionalLightColorAndType[lightIndex];
            }

            float4 ReadAdditionalLightDirectionAndSpot(int lightIndex)
            {
                return _BurtAdditionalLightBufferEnabled > 0.5f
                    ? _BurtAdditionalLightBuffer[lightIndex * BURT_ADDITIONAL_LIGHT_BUFFER_ROWS + 2]
                    : _BurtAdditionalLightDirectionAndSpot[lightIndex];
            }

            float4 ReadAdditionalLightSpotParams(int lightIndex)
            {
                return _BurtAdditionalLightBufferEnabled > 0.5f
                    ? _BurtAdditionalLightBuffer[lightIndex * BURT_ADDITIONAL_LIGHT_BUFFER_ROWS + 3]
                    : _BurtAdditionalLightSpotParams[lightIndex];
            }

            bool AdditionalLightUsesInverseSquaredFalloff(float packedFalloffAndNearCutoff)
            {
                return packedFalloffAndNearCutoff >= 0.0f;
            }

            float DecodeAdditionalLightVolumetricNearCutoff(float packedFalloffAndNearCutoff)
            {
                return packedFalloffAndNearCutoff >= 0.0f
                    ? packedFalloffAndNearCutoff
                    : max(-packedFalloffAndNearCutoff - 1.0f, 0.0f);
            }

            float EvaluateAdditionalLightDistanceAttenuation(
                float distanceSquared,
                float distanceBiasSquared,
                float range,
                bool useInverseSquaredFalloff)
            {
                float safeRange = max(range, 0.0001f);
                float biasedDistanceSquared = distanceSquared + max(distanceBiasSquared, 1.0f);
                float normalizedDistanceSquared = biasedDistanceSquared / max(safeRange * safeRange, 1.0e-6f);
                if (!useInverseSquaredFalloff)
                {
                    return saturate(1.0f - sqrt(saturate(normalizedDistanceSquared)));
                }

                float smoothFactor = saturate(1.0f - normalizedDistanceSquared * normalizedDistanceSquared);
                return smoothFactor * smoothFactor * rcp(max(biasedDistanceSquared, 0.0001f));
            }

            float3 EvaluateAdditionalLightScattering(
                float3 positionWS,
                float3 viewDirWS,
                float phaseG,
                float distanceBiasSquared)
            {
                float3 scattering = 0.0f;
                int additionalLightCount = GetAdditionalLightCount();

                [loop]
                for (int lightIndex = 0; lightIndex < BURT_MAX_ADDITIONAL_LIGHTS; lightIndex++)
                {
                    if (lightIndex >= additionalLightCount)
                    {
                        break;
                    }

                    float4 colorAndType = ReadAdditionalLightColorAndType(lightIndex);
                    float3 lightColor = max(colorAndType.rgb, 0.0f);
                    float volumetricScale = max(ReadAdditionalLightDirectionAndSpot(lightIndex).w, 0.0f);
                    if (volumetricScale <= 0.0001f || dot(lightColor, float3(0.2126f, 0.7152f, 0.0722f)) <= 0.0001f)
                    {
                        continue;
                    }

                    float lightType = colorAndType.w;
                    if (lightType <= 0.5f)
                    {
                        continue;
                    }

                    float4 directionAndSpot = ReadAdditionalLightDirectionAndSpot(lightIndex);
                    float3 lightDirWS = SafeNormalize(directionAndSpot.xyz, float3(0.0f, 1.0f, 0.0f));
                    float attenuation = 1.0f;
                    float nearCutoffMask = 1.0f;

                    {
                        float4 positionAndRange = ReadAdditionalLightPositionAndRange(lightIndex);
                        float3 toLight = positionAndRange.xyz - positionWS;
                        float distanceSquared = dot(toLight, toLight);
                        lightDirWS = SafeNormalize(toLight, lightDirWS);
                        float4 spotParams = ReadAdditionalLightSpotParams(lightIndex);
                        attenuation = EvaluateAdditionalLightDistanceAttenuation(
                            distanceSquared,
                            distanceBiasSquared,
                            positionAndRange.w,
                            AdditionalLightUsesInverseSquaredFalloff(spotParams.w));
                        float nearCutoff = DecodeAdditionalLightVolumetricNearCutoff(spotParams.w);
                        float softEdge = max(nearCutoff * 0.1f, 0.0001f);
                        nearCutoffMask = smoothstep(nearCutoff, nearCutoff + softEdge, sqrt(max(distanceSquared, 0.0f)));

                        if (lightType > 1.5f)
                        {
                            float3 spotDirectionWS = SafeNormalize(directionAndSpot.xyz, float3(0.0f, 0.0f, 1.0f));
                            float3 fromLightDirectionWS = -lightDirWS;
                            float spotCos = dot(fromLightDirectionWS, spotDirectionWS);
                            float spotFade = saturate((spotCos - spotParams.y) * spotParams.z);
                            attenuation *= spotFade * spotFade;
                        }
                    }

                    float phase = HenyeyGreensteinPhase(dot(lightDirWS, viewDirWS), phaseG) * 4.0f;
                    scattering += lightColor * attenuation * nearCutoffMask * volumetricScale * phase;
                }

                return scattering;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 sourceColor = tex2D(_BurtCameraColorTexture, input.ScreenUV).rgb;
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_BurtCameraDepthTexture, input.ScreenUV);

                float visibleDistance = max(_BurtVolumetricFogParams.x, 1.0f);
                float startDistance = max(_BurtVolumetricFogParams.y, 0.0f);
                int stepCount = clamp((int)round(_BurtVolumetricFogParams.z), 4, 96);
                float maxOpacity = saturate(_BurtVolumetricFogParams.w);
                bool isDebug = _BurtVolumetricFogDebugMode > 0.5f;

                float farDepth;
                #if defined(UNITY_REVERSED_Z)
                    farDepth = 0.0f;
                #else
                    farDepth = 1.0f;
                #endif

                float sceneRawDepth = IsSkyPixel(rawDepth) ? farDepth : rawDepth;
                float3 endPositionWS = ReconstructPositionWS(input.ScreenUV, sceneRawDepth);
                float3 cameraToEnd = endPositionWS - _BurtVolumetricFogCameraPositionWS;
                float sceneDistance = length(cameraToEnd);
                if (sceneDistance <= 1.0e-4f)
                {
                    return isDebug ? float4(0.0f, 0.0f, 0.0f, 1.0f) : float4(sourceColor, 1.0f);
                }

                float3 viewDirWS = cameraToEnd / sceneDistance;
                if (_BurtVolumetricFogIntegratedEnabled > 0.5f && _BurtVolumetricFogDebugMode < 2.5f)
                {
                    float viewDepth = max(-mul(UNITY_MATRIX_V, float4(endPositionWS, 1.0f)).z, 0.0f);
                    float4 integratedFog = SampleIntegratedVolumetricFog(input.ScreenUV, viewDepth);
                    if (isDebug)
                    {
                        return _BurtVolumetricFogDebugMode < 1.5f
                            ? float4(integratedFog.rgb, 1.0f)
                            : float4(integratedFog.aaa, 1.0f);
                    }

                    return float4(
                        sourceColor * integratedFog.a + BurtApplyPreExposure(integratedFog.rgb),
                        1.0f);
                }

                float rayEndDistance = min(sceneDistance, visibleDistance);
                float rayLength = max(rayEndDistance - startDistance, 0.0f);
                if (rayLength <= 1.0e-4f)
                {
                    return isDebug ? float4(0.0f, 0.0f, 0.0f, 1.0f) : float4(sourceColor, 1.0f);
                }

                float jitter = _BurtVolumetricFogScatteringParams.w > 0.5f
                    ? InterleavedGradientNoise(input.PositionCS.xy, _BurtVolumetricFogFrameParams.x)
                    : 0.5f;
                float stepLength = rayLength / stepCount;
                float fallbackFootprintRadius = max(stepLength, 1.0f);
                float fallbackDistanceBiasSquared = fallbackFootprintRadius * fallbackFootprintRadius;
                float3 lightDirWS = SafeNormalize(_BurtMainLightDirection.xyz, float3(0.0f, 1.0f, 0.0f));
                float mainLightVolumetricScale = max(_BurtMainLightColor.a, 0.0f);
                // Keep XRender's atmosphere-transmitted direct light separate from
                // the environment occlusion factor applied per froxel below.
                float3 lightColor = NormalizeLightColor(max(_BurtMainLightColorOuterSpace.rgb * _BurtMainLightAtmosphereTransmittance.rgb, 0.0f)) * mainLightVolumetricScale;
                float phaseG = _BurtVolumetricFogScatteringParams.x;
                float phase = HenyeyGreensteinPhase(dot(lightDirWS, viewDirWS), phaseG);
                float direct = max(_BurtVolumetricFogScatteringParams.y, 0.0f);
                float ambient = max(_BurtVolumetricFogScatteringParams.z, 0.0f);
                float3 albedo = max(_BurtVolumetricFogAlbedo.rgb, 0.0f);
                float mainLightOcclusion = saturate(_BurtMainLightOcclusionFactor);
                float3 skyAmbientLighting = albedo
                    * EvaluateSkyAmbientScattering(-viewDirWS, phaseG)
                    * ambient;
                float3 legacyDirectLighting = albedo * direct * phase * 4.0f * lightColor;
                float useAtmosphereHorizontalScattering = saturate(_BurtVolumetricFogAtmosphereScatteringEnabled * _BurtAtmosphereUseLuts);
                float3 atmosphereSingleScatteringLighting = 0.0f;
                float3 atmosphereMultipleScatteringLighting = 0.0f;
                [branch]
                if (useAtmosphereHorizontalScattering > 0.5f)
                {
                    float3 horizontalSunDirection = SafeNormalize(_BurtAtmosphereHorizontalFogSunDirection.xyz, lightDirWS);
                    float horizontalLDotV = dot(horizontalSunDirection, viewDirWS);
                    BurtAtmosphereEvaluateHorizontalFogLightingTerms(
                        horizontalLDotV,
                        phaseG,
                        _BurtAtmosphereHorizontalFogLightColor.rgb * max(_BurtAtmosphereHorizontalFogLightColor.a, 0.0f),
                        _BurtVolumetricFogAtmosphereRayleighTintScale.rgb,
                        _BurtVolumetricFogAtmosphereMieTintScale.rgb,
                        _BurtVolumetricFogAtmosphereMultipleScatteringTintScale.rgb,
                        atmosphereSingleScatteringLighting,
                        atmosphereMultipleScatteringLighting);
                }

                float transmittance = 1.0f;
                float3 scattering = 0.0f;
                float densitySum = 0.0f;
                float maxSampleDensity = 0.0f;

                [loop]
                for (int stepIndex = 0; stepIndex < stepCount; stepIndex++)
                {
                    float sampleDistance = startDistance + (stepIndex + jitter) * stepLength;
                    float3 samplePositionWS = _BurtVolumetricFogCameraPositionWS + viewDirWS * sampleDistance;
                    float density = SampleHeightDensity(samplePositionWS);
                    float opticalDepth = density * stepLength;
                    float stepTransmittance = exp2(-max(opticalDepth, 0.0f));
                    float stepAlpha = saturate(1.0f - stepTransmittance);
                    float mainLightVisibility = BurtSampleMainLightVolumetricShadow(samplePositionWS);
                    float atmosphereSingleScatteringVisibility = lerp(1.0f, mainLightVisibility, saturate(_BurtAtmosphereHorizontalFogUsesMainLight));
                    float3 legacyAmbientLighting = _BurtVolumetricFogTranslucencyGIParams.x > 0.5f
                        && sampleDistance < 128.0f
                        ? albedo * EvaluateTranslucencyGIAmbientScattering(
                            input.ScreenUV,
                            samplePositionWS,
                            viewDirWS,
                            phaseG) * ambient
                        : skyAmbientLighting;
                    float3 legacyLighting = legacyAmbientLighting + legacyDirectLighting * (mainLightOcclusion * mainLightVisibility);
                    float3 atmosphereHorizontalLighting = BurtAtmosphereCombineHorizontalFogLighting(
                        atmosphereSingleScatteringLighting,
                        atmosphereMultipleScatteringLighting,
                        atmosphereSingleScatteringVisibility,
                        mainLightOcclusion);
                    float3 localLighting = sampleDistance < 128.0f
                        ? EvaluateAdditionalLightScattering(
                            samplePositionWS,
                            viewDirWS,
                            phaseG,
                            fallbackDistanceBiasSquared)
                        : 0.0f;
                    // XRender keeps the legacy/direct model in the near field and
                    // transitions its secondary lighting to horizontal scattering
                    // over 130-150 m. Additional local lights remain independent.
                    float horizontalBlend = useAtmosphereHorizontalScattering * smoothstep(130.0f, 150.0f, sampleDistance);
                    float3 lighting = lerp(legacyLighting, atmosphereHorizontalLighting, horizontalBlend) + albedo * localLighting;

                    scattering += transmittance * stepAlpha * lighting;
                    transmittance *= stepTransmittance;
                    densitySum += density;
                    maxSampleDensity = max(maxSampleDensity, density);
                }

                float fogAmount = saturate(1.0f - transmittance);
                if (maxOpacity < 0.999f)
                {
                    float opacityScale = fogAmount > 1.0e-5f ? min(maxOpacity / fogAmount, 1.0f) : 1.0f;
                    scattering *= opacityScale;
                    fogAmount *= opacityScale;
                    transmittance = 1.0f - fogAmount;
                }

                if (isDebug)
                {
                    if (_BurtVolumetricFogDebugMode < 1.5f)
                    {
                        return float4(scattering, 1.0f);
                    }

                    if (_BurtVolumetricFogDebugMode < 2.5f)
                    {
                        return float4(transmittance.xxx, 1.0f);
                    }

                    if (_BurtVolumetricFogDebugMode < 3.5f)
                    {
                        float averageDensity = densitySum / max(stepCount, 1);
                        float densityDebug = saturate(max(averageDensity, maxSampleDensity * 0.35f) * 20.0f);
                        return float4(densityDebug.xxx, 1.0f);
                    }

                    if (_BurtVolumetricFogDebugMode < 4.5f)
                    {
                        return float4(DistanceDebugColor(rayEndDistance / visibleDistance), 1.0f);
                    }

                    return float4(saturate((float)stepCount / 96.0f).xxx, 1.0f);
                }

                return float4(sourceColor * transmittance + BurtApplyPreExposure(scattering), 1.0f);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
