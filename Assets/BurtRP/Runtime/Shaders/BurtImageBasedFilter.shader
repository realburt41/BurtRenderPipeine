Shader "Hidden/BurtRP/ImageBasedFilter"
{
    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        HLSLINCLUDE
            #pragma target 4.5
            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"

            UNITY_DECLARE_TEXCUBE(_BurtIBLFilterSource);
            float4 _BurtIBLFilterSourceHDR;
            float4 _BurtIBLFilterParams; // x roughness, y source max mip, z sample count, w source width.
            float4 _BurtIBLFilterFaceMip; // x face index, y mip, z mip size, w output max mip.
            float4 _BurtIBLFilterBakeRotation; // xy = cos/sin around world up.
            float4 _BurtIBLFilterBakeTintIntensity; // rgb = tint, a = intensity.
            float4 _BurtIBLFilterBakeLowerHemisphere; // rgb = solid color, a = blend.

            static const float BURT_IBL_PI = 3.14159265358979323846f;
            static const float BURT_IBL_TWO_PI = 6.28318530717958647692f;
            static const float BURT_IBL_FOUR_PI = 12.56637061435917295384f;
            static const float BURT_IBL_GOLDEN_RATIO = 1.618033988749895f;
            static const float BURT_IBL_MAX_HALF_FLOAT = 65504.0f;
            static const float BURT_IBL_SH_C0 = 0.07957747154594767f;
            static const float BURT_IBL_SH_C1 = 0.15915494309189535f;
            static const float BURT_IBL_SH_C2 = 0.2984155182973038f;
            static const float BURT_IBL_SH_C3 = 0.024867959858882128f;
            static const float BURT_IBL_SH_C4 = 0.07460387957664638f;

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
                output.positionCS = float4((input.vertexID == 2 ? 3.0f : -1.0f), (input.vertexID == 1 ? 3.0f : -1.0f), 0.0f, 1.0f);
                output.uv = float2((input.vertexID == 2 ? 2.0f : 0.0f), (input.vertexID == 1 ? 2.0f : 0.0f));
                return output;
            }

            float3 BurtIBLFaceUVToDirection(float face, float2 uv)
            {
                float2 st = uv * 2.0f - 1.0f;
                if (face < 0.5f) return BurtSafeNormalize(float3(1.0f, -st.y, -st.x));
                if (face < 1.5f) return BurtSafeNormalize(float3(-1.0f, -st.y, st.x));
                if (face < 2.5f) return BurtSafeNormalize(float3(st.x, 1.0f, st.y));
                if (face < 3.5f) return BurtSafeNormalize(float3(st.x, -1.0f, -st.y));
                if (face < 4.5f) return BurtSafeNormalize(float3(st.x, -st.y, 1.0f));
                return BurtSafeNormalize(float3(-st.x, -st.y, -1.0f));
            }

            float2 BurtIBLClampFaceUV(float2 uv, float faceSize)
            {
                float texelInset = 0.5f / max(faceSize, 1.0f);
                return clamp(uv, texelInset.xx, (1.0f - texelInset).xx);
            }

            void BurtIBLDirectionToFaceUV(float3 directionWS, out float face, out float2 uv)
            {
                float3 dir = BurtSafeNormalize(directionWS);
                float3 absDir = abs(dir);

                if (absDir.x >= absDir.y && absDir.x >= absDir.z)
                {
                    float invAxis = rcp(max(absDir.x, 0.000001f));
                    if (dir.x >= 0.0f)
                    {
                        face = 0.0f;
                        uv = float2(-dir.z, -dir.y) * invAxis;
                    }
                    else
                    {
                        face = 1.0f;
                        uv = float2(dir.z, -dir.y) * invAxis;
                    }
                }
                else if (absDir.y >= absDir.z)
                {
                    float invAxis = rcp(max(absDir.y, 0.000001f));
                    if (dir.y >= 0.0f)
                    {
                        face = 2.0f;
                        uv = float2(dir.x, dir.z) * invAxis;
                    }
                    else
                    {
                        face = 3.0f;
                        uv = float2(dir.x, -dir.z) * invAxis;
                    }
                }
                else
                {
                    float invAxis = rcp(max(absDir.z, 0.000001f));
                    if (dir.z >= 0.0f)
                    {
                        face = 4.0f;
                        uv = float2(dir.x, -dir.y) * invAxis;
                    }
                    else
                    {
                        face = 5.0f;
                        uv = float2(-dir.x, -dir.y) * invAxis;
                    }
                }

                uv = uv * 0.5f + 0.5f;
            }

            float3 BurtIBLApplyMipSeamScale(float3 directionWS, float mipLevel, float maxMipIndex)
            {
                float safeMaxMip = max(maxMipIndex, 0.0f);
                if (safeMaxMip <= 0.5f)
                {
                    return BurtSafeNormalize(directionWS);
                }

                float mipSize = exp2(max(safeMaxMip - floor(max(mipLevel, 0.0f)), 0.0f));
                float mipScale = saturate((mipSize - 2.0f) / max(mipSize, 1.0f));
                float face;
                float2 uv;
                BurtIBLDirectionToFaceUV(directionWS, face, uv);
                uv = (uv - 0.5f) * mipScale + 0.5f;
                return BurtIBLFaceUVToDirection(face, uv);
            }

            float2 BurtIBLHammersley(uint index, uint count)
            {
                uint bits = index;
                bits = (bits << 16u) | (bits >> 16u);
                bits = ((bits & 0x55555555u) << 1u) | ((bits & 0xAAAAAAAAu) >> 1u);
                bits = ((bits & 0x33333333u) << 2u) | ((bits & 0xCCCCCCCCu) >> 2u);
                bits = ((bits & 0x0F0F0F0Fu) << 4u) | ((bits & 0xF0F0F0F0u) >> 4u);
                bits = ((bits & 0x00FF00FFu) << 8u) | ((bits & 0xFF00FF00u) >> 8u);
                return float2((float)index / max((float)count, 1.0f), (float)bits * 2.3283064365386963e-10f);
            }

            float2 BurtIBLGolden2dSeq(uint index, uint count)
            {
                float sampleCount = max((float)count, 1.0f);
                return float2(((float)index + 0.5f) / sampleCount, frac((float)index / BURT_IBL_GOLDEN_RATIO));
            }

            void BurtIBLBuildBasis(float3 normalWS, out float3 tangentWS, out float3 bitangentWS)
            {
                float3 localZ = BurtSafeNormalize(normalWS);
                float sz = localZ.z >= 0.0f ? 1.0f : -1.0f;
                float a = rcp(sz + localZ.z);
                float ya = localZ.y * a;
                float b = localZ.x * ya;
                float c = localZ.x * sz;
                tangentWS = BurtSafeNormalize(float3(c * localZ.x * a - 1.0f, sz * b, c));
                bitangentWS = BurtSafeNormalize(float3(b, localZ.y * ya - sz, localZ.y));
            }

            float3 BurtIBLTangentToWorld(float3 sampleDirectionTS, float3 tangentWS, float3 bitangentWS, float3 normalWS)
            {
                return BurtSafeNormalize(tangentWS * sampleDirectionTS.x + bitangentWS * sampleDirectionTS.y + normalWS * sampleDirectionTS.z);
            }

            float3 BurtIBLImportanceSampleGGXLinear(float2 xi, float linearRoughness, float3 tangentWS, float3 bitangentWS, float3 normalWS)
            {
                float a2 = max(linearRoughness * linearRoughness, 0.000001f);
                float phi = BURT_IBL_TWO_PI * xi.y;
                float cosTheta = sqrt((1.0f - xi.x) / max(1.0f + (a2 - 1.0f) * xi.x, 0.000001f));
                float sinTheta = sqrt(saturate(1.0f - cosTheta * cosTheta));
                float3 halfVectorTS = float3(cos(phi) * sinTheta, sin(phi) * sinTheta, cosTheta);
                return BurtIBLTangentToWorld(halfVectorTS, tangentWS, bitangentWS, normalWS);
            }

            float BurtIBLD_GGX(float a2, float nDotH)
            {
                float d = (nDotH * a2 - nDotH) * nDotH + 1.0f;
                return a2 / max(BURT_IBL_PI * d * d, 0.0000001f);
            }

            float BurtIBLGetSmithJointGGXPartLambdaV(float nDotV, float a2)
            {
                return sqrt(max((-nDotV * a2 + nDotV) * nDotV + a2, 0.0f));
            }

            float BurtIBLV_SmithGGXCorrelated_PreLambdaV(float a2, float nDotV, float nDotL, float preLambdaV)
            {
                float lambdaV = nDotL * preLambdaV;
                float lambdaL = nDotV * sqrt(max((nDotL - a2 * nDotL) * nDotL + a2, 0.0f));
                return 0.5f / max(lambdaV + lambdaL, 0.0000001f);
            }

            float3 BurtIBLSampleSource(float3 directionWS, float mip)
            {
                float4 encoded = UNITY_SAMPLE_TEXCUBE_LOD(_BurtIBLFilterSource, BurtSafeNormalize(directionWS), mip);
                return max(DecodeHDR(encoded, _BurtIBLFilterSourceHDR), float3(0.0f, 0.0f, 0.0f));
            }

            float3 BurtIBLSampleSourceRaw(float3 directionWS, float mip)
            {
                return BurtIBLSampleSource(directionWS, mip);
            }

            float BurtIBLComputeFaceEdgeWeight(float edgeDistance, float texelSize)
            {
                return 1.0f - smoothstep(texelSize * 0.75f, texelSize * 2.75f, edgeDistance);
            }

            float3 BurtIBLSampleSourceFaceUV(float face, float2 uv, float mip)
            {
                return BurtIBLSampleSource(BurtIBLFaceUVToDirection(face, uv), mip);
            }

            float3 BurtIBLSampleSourceWithFaceEdgeFixup(float face, float2 uv, float faceSize, float mip)
            {
                float texelSize = rcp(max(faceSize, 1.0f));
                float leftWeight = BurtIBLComputeFaceEdgeWeight(uv.x, texelSize);
                float rightWeight = BurtIBLComputeFaceEdgeWeight(1.0f - uv.x, texelSize);
                float bottomWeight = BurtIBLComputeFaceEdgeWeight(uv.y, texelSize);
                float topWeight = BurtIBLComputeFaceEdgeWeight(1.0f - uv.y, texelSize);
                float3 color = BurtIBLSampleSourceFaceUV(face, uv, mip);
                float3 accum = color;
                float weight = 1.0f;
                accum += BurtIBLSampleSourceFaceUV(face, uv + float2(-texelSize, 0.0f), mip) * leftWeight;
                accum += BurtIBLSampleSourceFaceUV(face, uv + float2(texelSize, 0.0f), mip) * rightWeight;
                accum += BurtIBLSampleSourceFaceUV(face, uv + float2(0.0f, -texelSize), mip) * bottomWeight;
                accum += BurtIBLSampleSourceFaceUV(face, uv + float2(0.0f, texelSize), mip) * topWeight;
                weight += leftWeight + rightWeight + bottomWeight + topWeight;
                return accum / max(weight, 0.0001f);
            }

            float3 BurtIBLUniformSampleSphere(float2 xi)
            {
                float phi = BURT_IBL_TWO_PI * xi.x;
                float cosTheta = 1.0f - 2.0f * xi.y;
                float sinTheta = sqrt(saturate(1.0f - cosTheta * cosTheta));
                return float3(cos(phi) * sinTheta, sin(phi) * sinTheta, cosTheta);
            }

            float3 BurtIBLRotateBakeDirection(float3 directionWS)
            {
                float3 safeDirectionWS = BurtSafeNormalize(directionWS);
                float cosPhi = _BurtIBLFilterBakeRotation.x;
                float sinPhi = _BurtIBLFilterBakeRotation.y;
                float3 rotDirX = float3(cosPhi, 0.0f, -sinPhi);
                float3 rotDirZ = float3(sinPhi, 0.0f, cosPhi);
                return BurtSafeNormalize(float3(dot(rotDirX, safeDirectionWS), safeDirectionWS.y, dot(rotDirZ, safeDirectionWS)));
            }

            float4 FragSpecular(Varyings input) : SV_Target
            {
                float2 faceUV = BurtIBLClampFaceUV(input.uv, _BurtIBLFilterFaceMip.z);
                float3 normalWS = BurtIBLFaceUVToDirection(_BurtIBLFilterFaceMip.x, faceUV);
                float3 viewWS = normalWS;
                float perceptualRoughness = max(_BurtIBLFilterParams.x, 0.0f);
                float linearRoughness = max(perceptualRoughness * perceptualRoughness, 0.0001f);
                float a2 = max(linearRoughness * linearRoughness, 0.000001f);
                uint sampleCount = (uint)clamp(round(_BurtIBLFilterParams.z), 1.0f, 512.0f);
                float sourceMaxMip = max(_BurtIBLFilterParams.y, 0.0f);
                float sourceResolution = max(_BurtIBLFilterParams.w, exp2(sourceMaxMip));
                float invSourceTexelSolidAngle = 6.0f * sourceResolution * sourceResolution / BURT_IBL_FOUR_PI;
                float partLambdaV = BurtIBLGetSmithJointGGXPartLambdaV(1.0f, a2);
                float3 tangentWS;
                float3 bitangentWS;
                BurtIBLBuildBasis(normalWS, tangentWS, bitangentWS);

                float3 radiance = float3(0.0f, 0.0f, 0.0f);
                float totalWeight = 0.0f;
                for (uint sampleIndex = 0u; sampleIndex < 512u; sampleIndex++)
                {
                    if (sampleIndex >= sampleCount)
                    {
                        break;
                    }

                    float2 xi = BurtIBLGolden2dSeq(sampleIndex, sampleCount);
                    float3 halfVectorWS = perceptualRoughness <= 0.0001f ? normalWS : BurtIBLImportanceSampleGGXLinear(xi, linearRoughness, tangentWS, bitangentWS, normalWS);
                    float3 lightWS = BurtSafeNormalize(2.0f * dot(viewWS, halfVectorWS) * halfVectorWS - viewWS);
                    float nDotL = saturate(dot(normalWS, lightWS));
                    if (nDotL <= 0.0f)
                    {
                        continue;
                    }

                    float mip = 0.0f;
                    if (perceptualRoughness > 0.0001f)
                    {
                        float nDotH = saturate(dot(normalWS, halfVectorWS));
                        float pdf = max(0.25f * BurtIBLD_GGX(a2, nDotH), 0.0000001f);
                        float sampleSolidAngle = rcp(max((float)sampleCount * pdf, 0.0000001f));
                        mip = clamp(0.5f * log2(sampleSolidAngle * invSourceTexelSolidAngle) + linearRoughness, 0.0f, sourceMaxMip);
                    }

                    float weight = BurtIBLV_SmithGGXCorrelated_PreLambdaV(a2, 1.0f, nDotL, partLambdaV) * nDotL;
                    radiance += BurtIBLSampleSource(lightWS, mip) * weight;
                    totalWeight += weight;
                }

                if (totalWeight <= 0.0001f)
                {
                    radiance = BurtIBLSampleSource(normalWS, min(_BurtIBLFilterFaceMip.y, sourceMaxMip));
                }
                else
                {
                    radiance /= totalWeight;
                }

                return float4(radiance, 1.0f);
            }

            float4 FragDiffuse(Varyings input) : SV_Target
            {
                float2 faceUV = BurtIBLClampFaceUV(input.uv, _BurtIBLFilterFaceMip.z);
                float3 normalWS = BurtIBLFaceUVToDirection(_BurtIBLFilterFaceMip.x, faceUV);
                uint sampleCount = (uint)clamp(round(_BurtIBLFilterParams.z), 1.0f, 512.0f);
                float sourceMaxMip = max(_BurtIBLFilterParams.y, 0.0f);
                float3 tangentWS;
                float3 bitangentWS;
                BurtIBLBuildBasis(normalWS, tangentWS, bitangentWS);

                float3 irradiance = float3(0.0f, 0.0f, 0.0f);
                float sourceResolution = max(_BurtIBLFilterParams.w, exp2(sourceMaxMip));
                float sourceTexelSolidAngle = 4.0f * BURT_IBL_PI / max(6.0f * sourceResolution * sourceResolution, 1.0f);
                float sampleSolidAngle = BURT_IBL_PI / max((float)sampleCount, 1.0f);
                float diffuseMip = clamp(0.5f * log2(sampleSolidAngle / sourceTexelSolidAngle), 0.0f, sourceMaxMip);
                for (uint sampleIndex = 0u; sampleIndex < 512u; sampleIndex++)
                {
                    if (sampleIndex >= sampleCount)
                    {
                        break;
                    }

                    float2 xi = BurtIBLHammersley(sampleIndex, sampleCount);
                    float phi = BURT_IBL_TWO_PI * xi.x;
                    float cosTheta = sqrt(1.0f - xi.y);
                    float sinTheta = sqrt(saturate(xi.y));
                    float3 sampleDirectionTS = float3(cos(phi) * sinTheta, sin(phi) * sinTheta, cosTheta);
                    float3 sampleDirectionWS = BurtIBLTangentToWorld(sampleDirectionTS, tangentWS, bitangentWS, normalWS);
                    irradiance += BurtIBLSampleSource(sampleDirectionWS, diffuseMip);
                }

                irradiance = irradiance / max((float)sampleCount, 1.0f);
                return float4(max(irradiance, float3(0.0f, 0.0f, 0.0f)), 1.0f);
            }

            float4 FragBakeSource(Varyings input) : SV_Target
            {
                float2 faceUV = BurtIBLClampFaceUV(input.uv, _BurtIBLFilterFaceMip.z);
                float3 directionWS = BurtIBLFaceUVToDirection(_BurtIBLFilterFaceMip.x, faceUV);
                float3 sampleDirectionWS = BurtIBLRotateBakeDirection(directionWS);
                float bakeIntensity = max(_BurtIBLFilterBakeTintIntensity.a, 0.0f);
                float3 color = BurtIBLSampleSource(sampleDirectionWS, 0.0f) *
                    max(_BurtIBLFilterBakeTintIntensity.rgb, float3(0.0f, 0.0f, 0.0f)) *
                    bakeIntensity;

                color = clamp(color, float3(0.0f, 0.0f, 0.0f), BURT_IBL_MAX_HALF_FLOAT.xxx);
                if (_BurtIBLFilterBakeLowerHemisphere.a > 0.0f && sampleDirectionWS.y < 0.0f)
                {
                    float3 lowerColor = max(_BurtIBLFilterBakeLowerHemisphere.rgb, float3(0.0f, 0.0f, 0.0f));
                    color = lerp(color, lowerColor, saturate(_BurtIBLFilterBakeLowerHemisphere.a)) * bakeIntensity;
                }

                return float4(clamp(color, float3(0.0f, 0.0f, 0.0f), BURT_IBL_MAX_HALF_FLOAT.xxx), 1.0f);
            }

            float4 FragDiffuseSH(Varyings input) : SV_Target
            {
                uint coefficientIndex = (uint)clamp(floor(input.uv.x * _BurtIBLFilterFaceMip.z), 0.0f, 6.0f);
                uint sampleCount = (uint)clamp(round(_BurtIBLFilterParams.z), 1.0f, 512.0f);
                float sourceMaxMip = max(_BurtIBLFilterParams.y, 0.0f);
                float sourceResolution = max(_BurtIBLFilterParams.w, exp2(sourceMaxMip));
                float invSourceTexelSolidAngle = 6.0f * sourceResolution * sourceResolution / BURT_IBL_FOUR_PI;
                float sampleSolidAngle = BURT_IBL_FOUR_PI / max((float)sampleCount, 1.0f);
                float mipLevel = clamp(0.5f * log2(sampleSolidAngle * invSourceTexelSolidAngle), 0.0f, sourceMaxMip);

                float3 coeff0 = float3(0.0f, 0.0f, 0.0f);
                float3 coeff1 = float3(0.0f, 0.0f, 0.0f);
                float3 coeff2 = float3(0.0f, 0.0f, 0.0f);
                float3 coeff3 = float3(0.0f, 0.0f, 0.0f);
                float3 coeff4 = float3(0.0f, 0.0f, 0.0f);
                float3 coeff5 = float3(0.0f, 0.0f, 0.0f);
                float3 coeff6 = float3(0.0f, 0.0f, 0.0f);
                float3 coeff7 = float3(0.0f, 0.0f, 0.0f);
                float3 coeff8 = float3(0.0f, 0.0f, 0.0f);

                for (uint sampleIndex = 0u; sampleIndex < 512u; sampleIndex++)
                {
                    if (sampleIndex >= sampleCount)
                    {
                        break;
                    }

                    float2 xi = BurtIBLHammersley(sampleIndex, sampleCount);
                    float3 directionWS = BurtIBLUniformSampleSphere(xi);
                    float3 value = BurtIBLSampleSource(directionWS, mipLevel);
                    coeff0 += value;
                    coeff1 += -directionWS.y * value;
                    coeff2 += directionWS.z * value;
                    coeff3 += -directionWS.x * value;
                    coeff4 += directionWS.x * directionWS.y * value;
                    coeff5 += -directionWS.y * directionWS.z * value;
                    coeff6 += (3.0f * directionWS.z * directionWS.z - 1.0f) * value;
                    coeff7 += -directionWS.x * directionWS.z * value;
                    coeff8 += (directionWS.x * directionWS.x - directionWS.y * directionWS.y) * value;
                }

                float weight = BURT_IBL_FOUR_PI / max((float)sampleCount, 1.0f);
                coeff0 *= BURT_IBL_SH_C0 * weight;
                coeff1 *= -BURT_IBL_SH_C1 * weight;
                coeff2 *= BURT_IBL_SH_C1 * weight;
                coeff3 *= -BURT_IBL_SH_C1 * weight;
                coeff4 *= BURT_IBL_SH_C2 * weight;
                coeff5 *= -BURT_IBL_SH_C2 * weight;
                coeff6 *= BURT_IBL_SH_C3 * weight;
                coeff7 *= -BURT_IBL_SH_C2 * weight;
                coeff8 *= BURT_IBL_SH_C4 * weight;

                if (coefficientIndex == 0u) return float4(coeff3.r, coeff1.r, coeff2.r, coeff0.r - coeff6.r);
                if (coefficientIndex == 1u) return float4(coeff3.g, coeff1.g, coeff2.g, coeff0.g - coeff6.g);
                if (coefficientIndex == 2u) return float4(coeff3.b, coeff1.b, coeff2.b, coeff0.b - coeff6.b);
                if (coefficientIndex == 3u) return float4(coeff4.r, coeff5.r, coeff6.r * 3.0f, coeff7.r);
                if (coefficientIndex == 4u) return float4(coeff4.g, coeff5.g, coeff6.g * 3.0f, coeff7.g);
                if (coefficientIndex == 5u) return float4(coeff4.b, coeff5.b, coeff6.b * 3.0f, coeff7.b);
                return float4(coeff8.rgb, 1.0f);
            }
        ENDHLSL

        Pass
        {
            Name "Burt IBL Specular LD"
            Cull Off
            ZWrite Off
            ZTest Always
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragSpecular
            ENDHLSL
        }

        Pass
        {
            Name "Burt IBL Diffuse Irradiance"
            Cull Off
            ZWrite Off
            ZTest Always
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDiffuse
            ENDHLSL
        }

        Pass
        {
            Name "Burt IBL Runtime Source"
            Cull Off
            ZWrite Off
            ZTest Always
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBakeSource
            ENDHLSL
        }

        Pass
        {
            Name "Burt IBL Diffuse SH9"
            Cull Off
            ZWrite Off
            ZTest Always
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDiffuseSH
            ENDHLSL
        }
    }
    Fallback Off
}
