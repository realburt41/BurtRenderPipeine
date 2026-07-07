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

            sampler2D _BurtCameraColorTexture;
            UNITY_DECLARE_DEPTH_TEXTURE(_BurtCameraDepthTexture);

            float4 _BurtVolumetricFogParams; // x=visible distance, y=start distance, z=step count, w=max opacity
            float4 _BurtVolumetricFogDensityParams; // x=height, y=density, z=height falloff, w=extinction scale
            float4 _BurtVolumetricFogScatteringParams; // x=anisotropy, y=direct, z=ambient, w=jitter
            float4 _BurtVolumetricFogAlbedo;
            float4x4 _BurtVolumetricFogInverseViewProjection;
            float3 _BurtVolumetricFogCameraPositionWS;
            float _BurtVolumetricFogDebugMode;
            float4 _BurtVolumetricFogFrameParams;
            float4 _BurtMainLightDirection;
            float4 _BurtMainLightColor;
            float _BurtAdditionalLightCount;

            #define BURT_MAX_ADDITIONAL_LIGHTS 32
            #define BURT_ADDITIONAL_LIGHT_BUFFER_ROWS 4

            float4 _BurtAdditionalLightPositionAndRange[BURT_MAX_ADDITIONAL_LIGHTS];
            float4 _BurtAdditionalLightColorAndType[BURT_MAX_ADDITIONAL_LIGHTS];
            float4 _BurtAdditionalLightDirectionAndSpot[BURT_MAX_ADDITIONAL_LIGHTS];
            float4 _BurtAdditionalLightSpotParams[BURT_MAX_ADDITIONAL_LIGHTS];
            StructuredBuffer<float4> _BurtAdditionalLightBuffer;
            float _BurtAdditionalLightBufferEnabled;

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

            float HenyeyGreensteinPhase(float cosTheta, float g)
            {
                float g2 = g * g;
                float denom = max(0.05f, pow(abs(1.0f + g2 - 2.0f * g * cosTheta), 1.5f));
                return (1.0f - g2) / (4.0f * PI * denom);
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
                return density * extinctionScale * exp2(-max(-127.0f, falloff * (positionWS.y - fogHeight)));
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

            float EvaluateAdditionalLightDistanceAttenuation(float distanceSquared, float range)
            {
                float safeRange = max(range, 0.0001f);
                float rangeFade = saturate(1.0f - distanceSquared / max(safeRange * safeRange, 1.0e-6f));
                return rangeFade * rangeFade * rcp(max(distanceSquared, 0.25f));
            }

            float3 EvaluateAdditionalLightScattering(float3 positionWS, float3 viewDirWS, float phaseG, float directScale)
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
                    float4 directionAndSpot = ReadAdditionalLightDirectionAndSpot(lightIndex);
                    float3 lightDirWS = SafeNormalize(directionAndSpot.xyz, float3(0.0f, 1.0f, 0.0f));
                    float attenuation = 1.0f;
                    float nearCutoffMask = 1.0f;

                    if (lightType > 0.5f)
                    {
                        float4 positionAndRange = ReadAdditionalLightPositionAndRange(lightIndex);
                        float3 toLight = positionAndRange.xyz - positionWS;
                        float distanceSquared = dot(toLight, toLight);
                        lightDirWS = SafeNormalize(toLight, lightDirWS);
                        attenuation = EvaluateAdditionalLightDistanceAttenuation(distanceSquared, positionAndRange.w);
                        float nearCutoff = max(ReadAdditionalLightSpotParams(lightIndex).w, 0.0f);
                        float softEdge = max(nearCutoff * 0.1f, 0.0001f);
                        nearCutoffMask = smoothstep(nearCutoff, nearCutoff + softEdge, sqrt(max(distanceSquared, 0.0f)));

                        if (lightType > 1.5f)
                        {
                            float3 spotDirectionWS = SafeNormalize(directionAndSpot.xyz, float3(0.0f, 0.0f, 1.0f));
                            float3 fromLightDirectionWS = -lightDirWS;
                            float3 spotParams = ReadAdditionalLightSpotParams(lightIndex).xyz;
                            float spotCos = dot(fromLightDirectionWS, spotDirectionWS);
                            float spotFade = saturate((spotCos - spotParams.y) * spotParams.z);
                            attenuation *= spotFade * spotFade;
                        }
                    }

                    float phase = HenyeyGreensteinPhase(dot(lightDirWS, viewDirWS), phaseG) * 4.0f;
                    scattering += lightColor * attenuation * nearCutoffMask * volumetricScale * phase * directScale;
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
                float3 lightDirWS = SafeNormalize(_BurtMainLightDirection.xyz, float3(0.0f, 1.0f, 0.0f));
                float mainLightVolumetricScale = max(_BurtMainLightColor.a, 0.0f);
                float3 lightColor = NormalizeLightColor(max(_BurtMainLightColor.rgb, 0.0f)) * mainLightVolumetricScale;
                float phaseG = _BurtVolumetricFogScatteringParams.x;
                float phase = HenyeyGreensteinPhase(dot(lightDirWS, viewDirWS), phaseG);
                float direct = max(_BurtVolumetricFogScatteringParams.y, 0.0f);
                float ambient = max(_BurtVolumetricFogScatteringParams.z, 0.0f);
                float3 albedo = max(_BurtVolumetricFogAlbedo.rgb, 0.0f);
                float3 baseLighting = albedo * (ambient + direct * phase * 4.0f * lightColor);

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
                    float3 localLighting = EvaluateAdditionalLightScattering(samplePositionWS, viewDirWS, phaseG, direct);
                    float3 lighting = baseLighting + albedo * localLighting;

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
