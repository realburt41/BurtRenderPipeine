Shader "Hidden/BurtRP/BurtGI"
{
    // BurtRP v2.2 screen-space diffuse GI. It is a small bridge toward BurtGI/Lumen-style lighting,
    // not the final voxel/radiance-cache implementation.
    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        HLSLINCLUDE
            #define BURT_SUBSURFACE_DEFERRED_POSTPROCESS_INPUT 1
            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtDeferred.hlsl"

            Texture2D _BurtGISourceColorTexture;
            Texture2D _BurtScreenSpaceGlobalIlluminationRawTexture;
            Texture2D _BurtScreenSpaceGlobalIlluminationTexture;
            Texture2D _BurtGISpatialFinalTexture;
            Texture2D _BurtGITemporalFinalTexture;
            Texture2D _BurtGIHistoryTexture;
            Texture2D _BurtGIHistoryDepthNormalTexture;
            Texture2D _BurtGIBackfaceDiffuseIndirectHistoryTexture;
            Texture2D _BurtGIRoughSpecularIndirectHistoryTexture;
            Texture2D _BurtGIBackfaceDiffuseIndirectTexture;
            Texture2D _BurtGIRoughSpecularIndirectTexture;
            Texture2D _BurtGIPreviousHistoryTexture;
            Texture2D _BurtGIPreviousHistoryDepthNormalTexture;
            Texture2D _BurtGICameraColorCopyTexture;
            Texture2D _BurtGIDebugCameraColorTexture;
            Texture2D _BurtGITemporalDiagnosticsTexture;
            Texture2D _BurtGIRadianceCacheStatsTexture;
            Texture2D _BurtGIScreenProbeScreenDepthTexture;
            Texture2D _BurtGIScreenProbeWorldNormalTexture;
            Texture2D _BurtGIScreenProbeWorldPositionTexture;
            Texture2D _BurtGIScreenProbeRadianceTexture;
            Texture2D _BurtGIScreenProbeIrradianceTexture;
            Texture2D _BurtGIScreenProbeConfidenceTexture;
            Texture2D _BurtGIScreenProbeHitDistanceTexture;
            Texture2D _BurtGIScreenProbeBentNormalTexture;
            Texture2D _BurtGIScreenProbeTraceRadianceTexture;
            Texture2D _BurtGIScreenProbeTraceHitTexture;
            Texture2D _BurtGIScreenProbeTemporalRadianceTexture;
            Texture2D _BurtGIScreenProbeTemporalIrradianceTexture;
            Texture2D _BurtGIScreenProbeTemporalConfidenceTexture;
            Texture2D _BurtGIScreenProbeFilteredRadianceTexture;
            Texture2D _BurtGIScreenProbeFilteredIrradianceTexture;
            Texture2D _BurtGIScreenProbeFilteredConfidenceTexture;
            Texture2D _BurtGIScreenProbeFixupRadianceTexture;
            Texture2D _BurtGIScreenProbeFixupIrradianceTexture;
            Texture2D _BurtGIScreenProbeFixupConfidenceTexture;
            Texture2D _BurtGIScreenProbeMipRadianceTexture;
            Texture2D _BurtGIScreenProbeMipIrradianceTexture;
            Texture2D _BurtGIScreenProbeMipConfidenceTexture;
            Texture2D _BurtGIScreenProbeMip2RadianceTexture;
            Texture2D _BurtGIScreenProbeMip2IrradianceTexture;
            Texture2D _BurtGIScreenProbeMip2ConfidenceTexture;
            Texture2D _BurtGIScreenProbeMip3RadianceTexture;
            Texture2D _BurtGIScreenProbeMip3IrradianceTexture;
            Texture2D _BurtGIScreenProbeMip3ConfidenceTexture;
            Texture2D _BurtGIScreenProbeRadianceSHAmbientTexture;
            Texture2D _BurtGIScreenProbeRadianceSHDirectionalTexture;
            Texture2D _BurtGIScreenProbeIrradianceOctTexture;
            Texture2D _BurtGIScreenProbeRadianceOctTexture;
            Texture2D _BurtGIScreenProbeHistoryRadianceTexture;
            Texture2D _BurtGIScreenProbeHistoryIrradianceTexture;
            Texture2D _BurtGIScreenProbeHistoryConfidenceTexture;
            Texture2D _BurtGIScreenProbeHistoryNumFramesAccumulatedTexture;
            Texture2D _BurtGIScreenProbeHistoryScreenDepthTexture;
            Texture2D _BurtGIScreenProbeHistoryWorldPositionTexture;
            Texture2D _BurtGIScreenProbeHistoryWorldNormalTexture;
            Texture2D _BurtGIScreenProbeHistoryBentNormalTexture;
            Texture2D _BurtGIScreenProbeHistoryTraceHitTexture;
            Texture2D _BurtGIScreenProbeSceneVelocityTexture;
            Texture2D<float> _BurtHiZDepthTexture;
            #define tex2D(textureName, textureCoord) textureName.Sample(sampler_LinearClamp, textureCoord)
            Texture2D<uint> _BurtGIScreenProbeImportanceRayInfoTexture;
            Texture2D<uint> _BurtGIScreenProbeUseCountTexture;
            Texture2D<uint> _BurtGIScreenProbeAdaptiveProbeHeaderTexture;
            Texture2D<uint> _BurtGIScreenProbeAdaptiveProbeIndicesTexture;
            Texture3D<float4> _BurtGISceneVoxelOccupancyMipReadTexture;
            StructuredBuffer<uint> _BurtGIScreenProbeAdaptiveProbeNumBuffer;
            StructuredBuffer<uint> _BurtGIScreenProbeAdaptiveProbeDataBuffer;
            Texture2D<uint> _BurtGIRadianceCacheClipMapIndirectionTexture;
            Texture2D<float4> _BurtGIRadianceCacheClipMapFinalRadianceAtlasTexture;
            Texture2D<float4> _BurtGIRadianceCacheClipMapFinalIrradianceAtlasTexture;
            Texture2D<float2> _BurtGIRadianceCacheClipMapProbeOcclusionAtlasTexture;
            Texture2D<float> _BurtGIRadianceCacheClipMapProbeSkyAOAtlasTexture;
            StructuredBuffer<uint> _BurtGIRadianceCacheClipMapProbeAllocatorBuffer;
            StructuredBuffer<uint> _BurtGIRadianceCacheClipMapProbeFreeListAllocatorBuffer;
            StructuredBuffer<uint> _BurtGIRadianceCacheClipMapProbeFreeListBuffer;
            StructuredBuffer<uint4> _BurtGIRadianceCacheHashGridValueBuffer;
            StructuredBuffer<uint4> _BurtGIRadianceCacheHashGridTileBuffer;
            StructuredBuffer<float4> _BurtGIRadianceCacheHashGridDebugCellBuffer;
            StructuredBuffer<float4> _XGIScreenProbeTraceVisualize_TracesData;
            StructuredBuffer<uint> _BurtGIRadianceCacheClipMapProbeLastUsedFrameBuffer;
            StructuredBuffer<uint> _BurtGIRadianceCacheClipMapProbeLastTracedFrameBuffer;
            StructuredBuffer<float4> _BurtGIRadianceCacheClipMapProbeWorldOffsetBuffer;
            StructuredBuffer<float4> _BurtGIRadianceCacheClipMapProbeTraceDataBuffer;
            StructuredBuffer<uint> _BurtGIRadianceCacheClipMapProbeTraceAllocatorBuffer;
            StructuredBuffer<uint> _BurtGIRadianceCacheClipMapPriorityHistogramBuffer;
            StructuredBuffer<uint> _BurtGIRadianceCacheClipMapMaxUpdateBucketBuffer;
            StructuredBuffer<uint> _BurtGIRadianceCacheClipMapMaxTracesFromMaxUpdateBucketBuffer;
            StructuredBuffer<uint> _BurtGIRadianceCacheClipMapProbesToUpdateTraceCostBuffer;
            StructuredBuffer<uint3> _BurtGIRadianceCacheClipMapClearProbePDFsIndirectArgsBuffer;
            StructuredBuffer<uint3> _BurtGIRadianceCacheClipMapGenerateProbeTraceTilesIndirectArgsBuffer;
            StructuredBuffer<uint> _BurtGIRadianceCacheClipMapProbeTraceTileAllocatorBuffer;
            StructuredBuffer<uint3> _BurtGIRadianceCacheClipMapFilterProbesIndirectArgsBuffer;
            StructuredBuffer<uint3> _BurtGIRadianceCacheClipMapPrepareProbeOcclusionIndirectArgsBuffer;
            StructuredBuffer<uint3> _BurtGIRadianceCacheClipMapFixupProbeBordersIndirectArgsBuffer;
            StructuredBuffer<uint3> _BurtGIRadianceCacheClipMapTraceProbesIndirectArgsBuffer;
            StructuredBuffer<uint3> _BurtGIRadianceCacheClipMapSortProbeTraceTilesIndirectArgsBuffer;
            StructuredBuffer<uint3> _BurtGIRadianceCacheClipMapRadianceCacheHardwareRayTracingIndirectArgsBuffer;
            StructuredBuffer<uint> _BurtGIRadianceCacheClipMapHardwareRayTracingRayAllocatorBuffer;
            StructuredBuffer<uint2> _BurtGIRadianceCacheClipMapProbeTraceTileDataBuffer;
            StructuredBuffer<uint2> _BurtGIRadianceCacheClipMapSortedProbeTraceTileDataBuffer;
            float4x4 _BurtGIViewMatrix;
            float4x4 _BurtGIViewProjectionMatrix;
            float4x4 _BurtGIPreviousViewProjectionMatrix;
            float4 _BurtGISourceTexelSize; // xy=1/width,1/height, zw=width,height of the BurtGI target.
            float4 _BurtGIParams0; // x=radius, y=sampleCount, z=maxSteps, w=thickness.
            float4 _BurtGIParams1; // x=intensity, y=skyFallback, z=blur, w=blurSharpness.
            float4 _BurtGIParams2; // x=frame salt, y=normalWeight, z=distanceFade, w=radianceClamp.
            float4 _BurtGIParams3; // x=spatial radius, y=spatial strength, z=temporal variance clamp, w=hit rejection.
            float4 _BurtGIParams4; // x=leak guard, y=edge fade, z=normal cone tightness, w=sky edge suppression.
            float4 _BurtGIIrradianceFieldParams; // x=strength.
            float4 _BurtGIHistoryParams; // x=history exposure correction.
            float4 _BurtGITemporalParams; // x=feedback, y=history valid, z=depth rejection, w=normal rejection.
            float4 _BurtGITemporalParams1; // x=history clamp scale, y=variance clamp, z=hit rejection, w=spatial strength.
            float4 _BurtGIScreenProbeParams; // x=spacing pixels, y=trace distance, z=sample count, w=apply strength.
            float4 _BurtGIShortRangeAOParams; // x=enabled, y=weight, z=slope tolerance scale, w=reserved.
            float4 _BurtGIIndirectChannelParams; // x=backface diffuse enabled, y=rough specular enabled, z=history exposure correction, w=max roughness to evaluate rough specular.
            float4 _BurtGIScreenProbeGridParams; // xy=probe grid size, zw=probe grid texel size.
            float4 _BurtGIScreenProbeFilterParams; // x=spatial passes, y=half kernel, z=fixup borders, w=placement jitter index (negative encodes fixed).
            float4 _BurtGIScreenProbeSpatialFilterParams; // x=max radiance hit angle radians, y=position weight scale.
            float4 _BurtGIScreenProbePlacementParams; // x=HiZ valid.
            float4 _BurtGIScreenProbeTraceParams; // x=octahedral resolution, yz=trace atlas size, w=trace distance.
            float4 _BurtGIScreenProbeAdaptiveParams; // xy=probe grid size, z=tile capacity, w=max adaptive probes.
            float4 _BurtGIScreenProbeSHParams; // x=0 ambient/1 directional, y=base width, z=base height, w=directional tile count.
            float4 _BurtGIScreenProbeTemporalParams; // x=feedback, y=history valid, z=history distance threshold, w=temporal filter enabled.
            float4 _BurtGIScreenProbeReprojectionParams; // xy=XRender depth reject A/B, z=max frames accumulated, w=history normal cos threshold.
            float4 _BurtGIScreenProbeHistoryParams; // x=history exposure correction.
            float4 _BurtGIScreenProbeUseCountParams; // xy=use count texture size, z=inv width, w=valid.
            float4 _BurtGIRadianceCacheClipMapIndirectionSize; // xy=size, zw=texel size.
            float4 _BurtGIRadianceCacheClipMapParams; // x=atlas probes, y=probe resolution, z=clipmap count, w=clipmap resolution.
            float4 _BurtGIRadianceCacheClipMapApplyParams; // x=enabled, y=reprojection radius scale, z=debug visualize clipmap index (-1=auto), w=visualize radius scale.
            float4 _BurtGIRadianceCacheClipMapWorldParams; // x=base extent, y=distribution base, z=inv fade size, w=final probe resolution.
            uint _BurtGIRadianceCacheClipMapFinalRadianceAtlasMaxMip;
            uint _BurtGIRadianceCacheClipMapFinalIrradianceProbeResolution;
            uint _BurtGIRadianceCacheClipMapFinalOcclusionProbeResolution;
            float4 _BurtGIRadianceCacheClipMapStageParams; // x=stage, y=clear counters, z=frame index, w=reserved.
            float4 _BurtGIRadianceCacheClipMapWorldPositionToProbeCoord[6]; // xyz=bias, w=scale.
            float4 _BurtGIRadianceCacheClipMapProbeCoordToWorldCenter[6]; // xyz=bias, w=scale.
            float4 _BurtGIRadianceCacheClipMapClipmapCenterExtent[6]; // xyz=center, w=extent.
            float4 _BurtGIRadianceCacheClipMapClipmapParams[6]; // x=cell size, y=probe t min, z=volume uv offset x, w=index.
            float4 _BurtGIRadianceCacheClipMapVisualizeDrawParams; // x=clipmap index, y=radius scale, z=status mode, w=reserved.
            float4 _BurtGIRadianceCacheHashGridParams0; // x=cell size, y=tile cell ratio, z=bucket count, w=tiles per bucket.
            float4 _BurtGIRadianceCacheHashGridParams1; // x=tile count, y=cell count, z=cells per tile, w=reserved.
            float4 _BurtGISceneVoxelCenterExtent; // xyz=center, w=half extent of the active SceneVoxel volume.
            float4 _BurtGISceneVoxelDebugParams; // x=voxel resolution, y=clipmap count, z=debug occupancy mip, w=resource valid.
            int _BurtGISceneVoxelDebugLayer;
            float4 _BurtGISceneVoxelDebugProbeParams; // x=draw probe overlay, y=probe size in world units.
            float _BurtGIDebugMode;

            float3 BurtSampleIndirectDiffuseIrradiance(float3 normalWS)
            {
                return max(ShadeSH9(float4(BurtSafeNormalize(normalWS), 1.0f)), 0.0f);
            }

            float3 SampleIndirectSpecularRadiance(float3 reflectionDirectionWS, float roughness)
            {
                float3 safeReflectionDirectionWS = BurtSafeNormalize(reflectionDirectionWS);
                float mipLevel = saturate(roughness) * saturate(roughness) * 6.0f;
                float4 encodedSpecular = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, safeReflectionDirectionWS, mipLevel);
                return max(DecodeHDR(encodedSpecular, unity_SpecCube0_HDR), 0.0f);
            }

            float BurtGIRec709Luma(float3 color)
            {
                return dot(color, float3(0.2126f, 0.7152f, 0.0722f));
            }

            float3 BurtGITonemapLighting(float3 lighting)
            {
                return lighting / (1.0f + BurtGIRec709Luma(lighting));
            }

            float3 BurtGIInverseTonemapLighting(float3 tonemappedLighting)
            {
                return tonemappedLighting / max(1.0f - BurtGIRec709Luma(tonemappedLighting), 0.0001f);
            }

            struct Attributes
            {
                uint VertexID : SV_VertexID;
                uint InstanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 PositionCS : SV_POSITION;
                float2 ScreenUV : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.PositionCS = BurtGetFullScreenTriangleVertexPosition(input.VertexID);
                output.ScreenUV = BurtGetFullScreenTriangleTexCoord(input.VertexID);
                return output;
            }

            bool BurtGIIsSkyDepth(float rawDepth)
            {
                #if defined(UNITY_REVERSED_Z)
                    return rawDepth <= 0.00001f;
                #else
                    return rawDepth >= 0.99999f;
                #endif
            }

            float BurtGIRawDepthFromClip(float clipZ)
            {
                #if defined(UNITY_REVERSED_Z)
                    return saturate(clipZ);
                #else
                    return saturate((clipZ - UNITY_NEAR_CLIP_VALUE) / max(1.0f - UNITY_NEAR_CLIP_VALUE, 0.00001f));
                #endif
            }

            float2 BurtGIClipToScreenUV(float2 clipXY)
            {
                float2 uv = clipXY * 0.5f + 0.5f;
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0f - uv.y;
                #endif
                return uv;
            }

            bool BurtGIProjectPositionWS(float3 positionWS, out float2 screenUV, out float rawDepth, out float linearDepth)
            {
                float4 clipPosition = mul(_BurtGIViewProjectionMatrix, float4(positionWS, 1.0f));
                if (clipPosition.w <= 0.00001f)
                {
                    screenUV = 0.0f;
                    rawDepth = 0.0f;
                    linearDepth = 0.0f;
                    return false;
                }

                float3 ndc = clipPosition.xyz / clipPosition.w;
                screenUV = BurtGIClipToScreenUV(ndc.xy);
                rawDepth = BurtGIRawDepthFromClip(ndc.z);
                linearDepth = LinearEyeDepth(rawDepth);
                return !any(screenUV < 0.0f) && !any(screenUV > 1.0f) && !BurtGIIsSkyDepth(rawDepth);
            }

            float BurtGIRand(float2 pixelPosition)
            {
                return frac(52.9829189f * frac(dot(pixelPosition + _BurtGIParams2.xx, float2(0.06711056f, 0.00583715f))));
            }

            float BurtGIHash12(float2 pixelPosition, float salt)
            {
                float3 p3 = frac(float3(pixelPosition.x, pixelPosition.y, pixelPosition.x) * 0.1031f + salt * 0.0131f);
                p3 += dot(p3, p3.yzx + 33.33f);
                return frac((p3.x + p3.y) * p3.z);
            }

            float3 BurtGISampleNormalWS(float2 screenUV)
            {
                return BurtSampleDeferredSurfaceNormalWS(screenUV);
            }

            void BurtGIBuildTangentBasis(float3 normalWS, out float3 tangentWS, out float3 bitangentWS)
            {
                float3 upWS = abs(normalWS.y) < 0.99f ? float3(0.0f, 1.0f, 0.0f) : float3(1.0f, 0.0f, 0.0f);
                tangentWS = BurtSafeNormalize(cross(upWS, normalWS));
                bitangentWS = BurtSafeNormalize(cross(normalWS, tangentWS));
            }

            float3 BurtGIWorldToTangent(float3 directionWS, float3 tangentWS, float3 bitangentWS, float3 normalWS)
            {
                return float3(dot(directionWS, tangentWS), dot(directionWS, bitangentWS), dot(directionWS, normalWS));
            }

            float3 BurtGITangentToWorld(float3 directionTS, float3 tangentWS, float3 bitangentWS, float3 normalWS)
            {
                return BurtSafeNormalize(tangentWS * directionTS.x + bitangentWS * directionTS.y + normalWS * directionTS.z);
            }

            float3 BurtGIImportanceSampleVisibleGGXHalfVectorTS(float2 diskSample, float roughnessSq, float3 viewDirectionTS)
            {
                float a = sqrt(max(roughnessSq, 0.000001f));
                float3 stretchedView = BurtSafeNormalize(float3(a * viewDirectionTS.xy, max(viewDirectionTS.z, 0.0001f)));

                float tangentLengthSq = dot(stretchedView.xy, stretchedView.xy);
                float3 tangent0 = tangentLengthSq > 0.0f
                    ? float3(-stretchedView.y, stretchedView.x, 0.0f) * rsqrt(tangentLengthSq)
                    : float3(1.0f, 0.0f, 0.0f);
                float3 tangent1 = cross(stretchedView, tangent0);

                float2 projectedDisk = diskSample;
                float viewBlend = 0.5f + 0.5f * stretchedView.z;
                projectedDisk.y = (1.0f - viewBlend) * sqrt(saturate(1.0f - projectedDisk.x * projectedDisk.x)) + viewBlend * projectedDisk.y;

                float3 halfVectorTS = projectedDisk.x * tangent0;
                halfVectorTS += projectedDisk.y * tangent1;
                halfVectorTS += sqrt(saturate(1.0f - dot(projectedDisk, projectedDisk))) * stretchedView;
                return BurtSafeNormalize(float3(a * halfVectorTS.xy, max(0.0f, halfVectorTS.z)));
            }

            float3 BurtGIAnchoredVisibleGGXSpecularDirection(float3 normalWS, float3 viewDirectionWS, float roughness)
            {
                float3 tangentWS;
                float3 bitangentWS;
                float3 safeNormalWS = BurtSafeNormalize(normalWS);
                float3 safeViewDirectionWS = BurtSafeNormalize(viewDirectionWS);
                BurtGIBuildTangentBasis(safeNormalWS, tangentWS, bitangentWS);

                float3 viewDirectionTS = BurtSafeNormalize(BurtGIWorldToTangent(safeViewDirectionWS, tangentWS, bitangentWS, safeNormalWS));
                float2 anchoredSample = float2(0.0f, 0.0f);
                anchoredSample.y = (anchoredSample.y - 0.5f) * 0.9f + 0.5f;

                float3 halfVectorTS = BurtGIImportanceSampleVisibleGGXHalfVectorTS(anchoredSample, roughness * roughness, viewDirectionTS);
                float3 halfVectorWS = BurtGITangentToWorld(halfVectorTS, tangentWS, bitangentWS, safeNormalWS);
                float3 sampleDirectionWS = BurtSafeNormalize(2.0f * dot(safeViewDirectionWS, halfVectorWS) * halfVectorWS - safeViewDirectionWS);
                float3 fallbackReflectionWS = BurtSafeNormalize(reflect(-safeViewDirectionWS, safeNormalWS));
                return dot(sampleDirectionWS, safeNormalWS) > 0.0f ? sampleDirectionWS : fallbackReflectionWS;
            }

            float3 BurtGIBuildSampleDirection(float3 normalWS, float3 tangentWS, float3 bitangentWS, float angle, float sampleFraction)
            {
                float radial = sqrt(saturate(sampleFraction));
                float normalAmount = sqrt(saturate(1.0f - radial * radial));
                float2 diskDirection = float2(cos(angle), sin(angle)) * radial;
                return BurtSafeNormalize(tangentWS * diskDirection.x + bitangentWS * diskDirection.y + normalWS * normalAmount);
            }

            float3 BurtGIClampRadiance(float3 radiance)
            {
                return min(max(radiance, 0.0f), _BurtGIParams2.www);
            }

            float BurtGIEdgeFactor(float2 screenUV, float centerRawDepth, float3 centerNormalWS)
            {
                float2 texel = _BurtDeferredScreenSize.zw;
                float centerLinearDepth = LinearEyeDepth(centerRawDepth);
                float minNormalDot = 1.0f;
                float maxDepthEdge = 0.0f;
                float skyNeighbor = 0.0f;

                [unroll]
                for (int axis = 0; axis < 2; ++axis)
                {
                    float2 direction = axis == 0 ? float2(1.0f, 0.0f) : float2(0.0f, 1.0f);
                    float2 positiveUV = saturate(screenUV + direction * texel);
                    float2 negativeUV = saturate(screenUV - direction * texel);
                    float positiveRawDepth = BurtSampleDeferredRawDepth(positiveUV);
                    float negativeRawDepth = BurtSampleDeferredRawDepth(negativeUV);
                    bool positiveIsSky = BurtGIIsSkyDepth(positiveRawDepth);
                    bool negativeIsSky = BurtGIIsSkyDepth(negativeRawDepth);

                    if (positiveIsSky || negativeIsSky)
                    {
                        float2 positiveFarUV = saturate(screenUV + direction * texel * 2.0f);
                        float2 negativeFarUV = saturate(screenUV - direction * texel * 2.0f);
                        bool positiveFarIsSky = positiveIsSky && BurtGIIsSkyDepth(BurtSampleDeferredRawDepth(positiveFarUV));
                        bool negativeFarIsSky = negativeIsSky && BurtGIIsSkyDepth(BurtSampleDeferredRawDepth(negativeFarUV));
                        skyNeighbor = max(skyNeighbor, positiveFarIsSky || negativeFarIsSky ? 1.0f : 0.0f);
                        continue;
                    }

                    float positiveLinearDepth = LinearEyeDepth(positiveRawDepth);
                    float negativeLinearDepth = LinearEyeDepth(negativeRawDepth);
                    float pairSlope = abs(positiveLinearDepth - negativeLinearDepth) * 0.5f;
                    float pairCurvature = abs(positiveLinearDepth + negativeLinearDepth - centerLinearDepth * 2.0f);
                    float positiveCenterDelta = abs(positiveLinearDepth - centerLinearDepth);
                    float negativeCenterDelta = abs(negativeLinearDepth - centerLinearDepth);
                    float oneSidedStep = max(positiveCenterDelta, negativeCenterDelta);
                    float depthScale = max(centerLinearDepth * lerp(0.08f, 0.035f, saturate(_BurtGIParams4.x)), 0.12f);
                    float quantizationFloor = max(centerLinearDepth * 0.006f, 0.015f);
                    float curvatureSignal = max(pairCurvature - pairSlope * 0.75f - quantizationFloor, 0.0f);
                    float stepSignal = max(oneSidedStep - pairSlope * 0.5f - quantizationFloor, 0.0f);
                    float axisDepthEdge = saturate(max(curvatureSignal, stepSignal) / depthScale);
                    maxDepthEdge = max(maxDepthEdge, axisDepthEdge);

                    float positiveNormalDot = saturate(dot(centerNormalWS, BurtGISampleNormalWS(positiveUV)));
                    float negativeNormalDot = saturate(dot(centerNormalWS, BurtGISampleNormalWS(negativeUV)));
                    minNormalDot = min(minNormalDot, min(positiveNormalDot, negativeNormalDot));
                }

                float normalEdge = saturate((1.0f - minNormalDot) * lerp(1.5f, 5.0f, saturate(_BurtGIParams4.z)));
                float depthEdge = maxDepthEdge * lerp(0.35f, 1.0f, maxDepthEdge);
                return saturate(max(max(depthEdge, normalEdge), skyNeighbor * saturate(_BurtGIParams4.w)));
            }

            float BurtGIComputeDiffuseSourceWeight(BurtPBRMaterialData sampleMaterialData)
            {
                float diffuseLuma = dot(max(sampleMaterialData.DiffuseColor, 0.0f), float3(0.2126f, 0.7152f, 0.0722f));
                float baseLuma = dot(max(sampleMaterialData.BaseColor, 0.0f), float3(0.2126f, 0.7152f, 0.0722f));
                float diffuseFraction = saturate(diffuseLuma / max(baseLuma, 0.001f));
                float roughDiffuseWeight = lerp(0.35f, 1.0f, saturate(sampleMaterialData.PerceptualRoughness));
                float sourceOcclusion = lerp(0.25f, 1.0f, saturate(sampleMaterialData.Occlusion));
                return diffuseFraction * roughDiffuseWeight * sourceOcclusion;
            }

            float3 BurtGITintRadianceByDiffuseAlbedo(float3 sourceRadiance, BurtPBRMaterialData sampleMaterialData)
            {
                float3 lumaWeights = float3(0.2126f, 0.7152f, 0.0722f);
                float3 diffuseColor = max(sampleMaterialData.DiffuseColor, 0.0f);
                float diffuseLuma = dot(diffuseColor, lumaWeights);
                float sourceLuma = dot(max(sourceRadiance, 0.0f), lumaWeights);
                float maxChannel = max(diffuseColor.r, max(diffuseColor.g, diffuseColor.b));
                float minChannel = min(diffuseColor.r, min(diffuseColor.g, diffuseColor.b));
                float chroma = saturate((maxChannel - minChannel) / max(maxChannel, 0.001f));
                float tintWeight = smoothstep(0.025f, 0.18f, diffuseLuma) * saturate(chroma * 1.35f);
                float3 normalizedDiffuse = min(diffuseColor / max(diffuseLuma, 0.035f), 4.0f);
                float colorBleedBoost = lerp(1.0f, 1.65f, tintWeight);
                float3 diffuseTintedRadiance = normalizedDiffuse * sourceLuma * colorBleedBoost;
                return BurtGIClampRadiance(lerp(sourceRadiance, diffuseTintedRadiance, tintWeight));
            }

            float3 BurtGIEstimateSampleDiffuseRadiance(BurtPBRMaterialData sampleMaterialData, float3 sampleNormalWS)
            {
                float3 diffuseIrradiance = BurtSampleIndirectDiffuseIrradiance(sampleNormalWS);
                float3 diffuseRadiance = max(sampleMaterialData.DiffuseColor, 0.0f) * max(diffuseIrradiance, 0.0f);
                return BurtGIClampRadiance(diffuseRadiance);
            }

            float BurtGIComputeDiffuseChroma(float3 diffuseColor)
            {
                float maxChannel = max(diffuseColor.r, max(diffuseColor.g, diffuseColor.b));
                float minChannel = min(diffuseColor.r, min(diffuseColor.g, diffuseColor.b));
                return saturate((maxChannel - minChannel) / max(maxChannel, 0.001f));
            }

            float2 BurtGINearFieldColorBleedOffset(int index)
            {
                float2 direction = 0.0f;
                if (index == 0) direction = float2(1.0f, 0.0f);
                if (index == 1) direction = float2(-1.0f, 0.0f);
                if (index == 2) direction = float2(0.0f, 1.0f);
                if (index == 3) direction = float2(0.0f, -1.0f);
                if (index == 4) direction = float2(1.0f, 1.0f);
                if (index == 5) direction = float2(-1.0f, 1.0f);
                if (index == 6) direction = float2(1.0f, -1.0f);
                if (index == 7) direction = float2(-1.0f, -1.0f);
                if (index == 8) direction = float2(2.0f, 1.0f);
                if (index == 9) direction = float2(-2.0f, 1.0f);
                if (index == 10) direction = float2(2.0f, -1.0f);
                if (index == 11) direction = float2(-2.0f, -1.0f);
                if (index == 12) direction = float2(1.0f, 2.0f);
                if (index == 13) direction = float2(-1.0f, 2.0f);
                if (index == 14) direction = float2(1.0f, -2.0f);
                if (index == 15) direction = float2(-1.0f, -2.0f);
                if (index == 16) direction = float2(3.0f, 1.0f);
                if (index == 17) direction = float2(-3.0f, 1.0f);
                if (index == 18) direction = float2(3.0f, -1.0f);
                if (index == 19) direction = float2(-3.0f, -1.0f);
                if (index == 20) direction = float2(1.0f, 3.0f);
                if (index == 21) direction = float2(-1.0f, 3.0f);
                if (index == 22) direction = float2(1.0f, -3.0f);
                if (index == 23) direction = float2(-1.0f, -3.0f);
                return direction * rsqrt(max(dot(direction, direction), 0.0001f));
            }

            void BurtGIGatherNearFieldColorBleed(
                float2 screenUV,
                float3 positionWS,
                float3 normalWS,
                float radius,
                float centerEdgeFactor,
                out float3 bleedRadiance,
                out float bleedStrength)
            {
                float3 lumaWeights = float3(0.2126f, 0.7152f, 0.0722f);
                float centerEdgeGate = 1.0f - smoothstep(0.18f, 0.65f, centerEdgeFactor);
                if (centerEdgeGate <= 0.001f)
                {
                    bleedRadiance = 0.0f;
                    bleedStrength = 0.0f;
                    return;
                }

                float screenRadiusPixels = clamp(10.0f + radius * 18.0f, 16.0f, 52.0f) * lerp(0.55f, 1.0f, centerEdgeGate);
                float3 radianceSum = 0.0f;
                float weightSum = 0.0f;

                [loop]
                for (int i = 0; i < 24; ++i)
                {
                    float ring = i < 8 ? 0.38f : (i < 16 ? 0.76f : 1.16f);
                    float ringWeight = i < 8 ? 1.0f : (i < 16 ? 0.72f : 0.48f);
                    float2 sampleUV = saturate(screenUV + BurtGINearFieldColorBleedOffset(i) * _BurtGISourceTexelSize.xy * screenRadiusPixels * ring);
                    float sampleRawDepth = BurtSampleDeferredRawDepth(sampleUV);
                    if (BurtGIIsSkyDepth(sampleRawDepth))
                    {
                        continue;
                    }

                    float3 samplePositionWS = BurtReconstructDeferredPositionWS(sampleUV, sampleRawDepth);
                    float3 deltaWS = samplePositionWS - positionWS;
                    float distanceWS = length(deltaWS);
                    if (distanceWS <= 0.0001f || distanceWS > radius * 2.75f)
                    {
                        continue;
                    }

                    BurtEncodedGBuffer sampleEncodedGBuffer = BurtSampleEncodedGBuffer(sampleUV);
                    BurtPBRMaterialData sampleMaterialData = BurtPreparePBRMaterialData(BurtDecodeDeferredGBuffer(sampleEncodedGBuffer, sampleUV));
                    float3 sampleDiffuse = max(sampleMaterialData.DiffuseColor, 0.0f);
                    float sampleDiffuseLuma = dot(sampleDiffuse, lumaWeights);
                    float sampleChroma = BurtGIComputeDiffuseChroma(sampleDiffuse);
                    float3 sampleEmission = max(sampleEncodedGBuffer.GBuffer4.rgb, 0.0f);
                    float sampleEmissionLuma = dot(sampleEmission, lumaWeights);
                    float sampleEmissionChroma = BurtGIComputeDiffuseChroma(sampleEmission);
                    float diffuseColorSourceWeight = smoothstep(0.05f, 0.26f, sampleChroma) * smoothstep(0.025f, 0.14f, sampleDiffuseLuma);
                    float emissionSourceWeight = smoothstep(0.01f, 0.12f, sampleEmissionLuma) *
                        lerp(0.65f, 1.0f, smoothstep(0.025f, 0.2f, sampleEmissionChroma));
                    float colorSourceWeight = max(diffuseColorSourceWeight, emissionSourceWeight);
                    if (colorSourceWeight <= 0.0001f)
                    {
                        continue;
                    }

                    float3 toSampleWS = deltaWS / max(distanceWS, 0.0001f);
                    float3 sampleNormalWS = BurtGISampleNormalWS(sampleUV);
                    float sampleEdgeFactor = BurtGIEdgeFactor(sampleUV, sampleRawDepth, sampleNormalWS);
                    float sampleEdgeGate = 1.0f - smoothstep(0.22f, 0.72f, sampleEdgeFactor);
                    float receiverFacing = smoothstep(-0.08f, 0.32f, dot(normalWS, toSampleWS));
                    float sourceFacing = smoothstep(-0.18f, 0.42f, dot(sampleNormalWS, -toSampleWS));
                    float distanceWeight = 1.0f - smoothstep(radius * 0.08f, radius * 2.45f, distanceWS);
                    float sourceWeight = max(
                        BurtGIComputeDiffuseSourceWeight(sampleMaterialData),
                        max(colorSourceWeight * 0.72f, saturate(sampleEmissionLuma)));
                    float sampleWeight = ringWeight * colorSourceWeight * receiverFacing * sourceFacing * distanceWeight * sourceWeight;
                    sampleWeight *= centerEdgeGate * sampleEdgeGate;
                    if (sampleWeight <= 0.0001f)
                    {
                        continue;
                    }

                    float3 sampleDiffuseRadiance = BurtGIEstimateSampleDiffuseRadiance(sampleMaterialData, sampleNormalWS);
                    sampleDiffuseRadiance = BurtGITintRadianceByDiffuseAlbedo(sampleDiffuseRadiance, sampleMaterialData);
                    float3 sampleRadiance = sampleDiffuseRadiance * BurtGIComputeDiffuseSourceWeight(sampleMaterialData) + sampleEmission;
                    radianceSum += sampleRadiance * sampleWeight;
                    weightSum += sampleWeight;
                }

                bleedRadiance = weightSum > 0.0001f ? radianceSum / weightSum : 0.0f;
                bleedStrength = saturate(weightSum * 0.32f * centerEdgeGate);
            }

            bool BurtGIProjectHistoryUV(float3 positionWS, out float2 historyUV, out float projectedRawDepth)
            {
                float4 previousClip = mul(_BurtGIPreviousViewProjectionMatrix, float4(positionWS, 1.0f));
                if (previousClip.w <= 0.00001f)
                {
                    historyUV = 0.0f;
                    projectedRawDepth = 0.0f;
                    return false;
                }

                float3 previousNDC = previousClip.xyz / previousClip.w;
                historyUV = BurtGIClipToScreenUV(previousNDC.xy);
                projectedRawDepth = BurtGIRawDepthFromClip(previousNDC.z);
                return !any(historyUV < 0.0f) && !any(historyUV > 1.0f) && !BurtGIIsSkyDepth(projectedRawDepth);
            }

            float4 BurtGISampleHistoryDepthNormalClosest(float2 historyUV, float projectedRawDepth)
            {
                float4 bestDepthNormal = tex2D(_BurtGIHistoryDepthNormalTexture, historyUV);
                float projectedLinearDepth = LinearEyeDepth(projectedRawDepth);
                float bestDepthError = BurtGIIsSkyDepth(bestDepthNormal.r) ? 1.0e20f : abs(LinearEyeDepth(bestDepthNormal.r) - projectedLinearDepth);
                float2 texel = _BurtGISourceTexelSize.xy;

                [unroll]
                for (int i = 0; i < 4; ++i)
                {
                    float2 direction = 0.0f;
                    if (i == 0) direction = float2(1.0f, 0.0f);
                    if (i == 1) direction = float2(-1.0f, 0.0f);
                    if (i == 2) direction = float2(0.0f, 1.0f);
                    if (i == 3) direction = float2(0.0f, -1.0f);

                    float4 sampleDepthNormal = tex2D(_BurtGIHistoryDepthNormalTexture, saturate(historyUV + direction * texel));
                    if (BurtGIIsSkyDepth(sampleDepthNormal.r))
                    {
                        continue;
                    }

                    float sampleDepthError = abs(LinearEyeDepth(sampleDepthNormal.r) - projectedLinearDepth);
                    if (sampleDepthError < bestDepthError)
                    {
                        bestDepthError = sampleDepthError;
                        bestDepthNormal = sampleDepthNormal;
                    }
                }

                return bestDepthNormal;
            }

            void BurtGISampleTemporalNeighborhoodStats(float2 screenUV, out float4 centerBurtGI, out float3 minBurtGI, out float3 maxBurtGI, out float3 averageBurtGI, out float3 sigmaBurtGI)
            {
                float2 texel = _BurtGISourceTexelSize.xy;
                centerBurtGI = tex2D(_BurtGISpatialFinalTexture, screenUV);
                float3 sumBurtGI = centerBurtGI.rgb;
                float3 sumSqBurtGI = centerBurtGI.rgb * centerBurtGI.rgb;
                minBurtGI = centerBurtGI.rgb;
                maxBurtGI = centerBurtGI.rgb;
                float weight = 1.0f;

                [unroll]
                for (int i = 0; i < 8; ++i)
                {
                    float2 direction = 0.0f;
                    if (i == 0) direction = float2(1.0f, 0.0f);
                    if (i == 1) direction = float2(-1.0f, 0.0f);
                    if (i == 2) direction = float2(0.0f, 1.0f);
                    if (i == 3) direction = float2(0.0f, -1.0f);
                    if (i == 4) direction = float2(1.0f, 1.0f);
                    if (i == 5) direction = float2(-1.0f, 1.0f);
                    if (i == 6) direction = float2(1.0f, -1.0f);
                    if (i == 7) direction = float2(-1.0f, -1.0f);

                    float3 sampleBurtGI = tex2D(_BurtGISpatialFinalTexture, saturate(screenUV + direction * texel)).rgb;
                    minBurtGI = min(minBurtGI, sampleBurtGI);
                    maxBurtGI = max(maxBurtGI, sampleBurtGI);
                    sumBurtGI += sampleBurtGI;
                    sumSqBurtGI += sampleBurtGI * sampleBurtGI;
                    weight += 1.0f;
                }

                averageBurtGI = sumBurtGI / max(weight, 0.0001f);
                float3 variance = max(sumSqBurtGI / max(weight, 0.0001f) - averageBurtGI * averageBurtGI, 0.0f);
                sigmaBurtGI = sqrt(variance);
            }

            float4 FragTrace(Varyings input) : SV_Target
            {
                float2 screenUV = input.ScreenUV;
                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtGIIsSkyDepth(rawDepth))
                {
                    return 0.0f;
                }

                BurtEncodedGBuffer encodedGBuffer = BurtSampleEncodedGBuffer(screenUV);
                BurtGBufferData gbufferData = BurtDecodeDeferredGBuffer(encodedGBuffer, screenUV);
                BurtPBRMaterialData materialData = BurtPreparePBRMaterialData(gbufferData);
                float3 normalWS = BurtGetDeferredSurfaceNormalWS(gbufferData);
                float3 positionWS = BurtReconstructDeferredPositionWS(screenUV, rawDepth);
                float centerLinearDepth = LinearEyeDepth(rawDepth);
                float distanceFade = 1.0f - saturate(centerLinearDepth / max(_BurtGIParams2.z, 1.0f));
                float edgeFactor = BurtGIEdgeFactor(screenUV, rawDepth, normalWS);
                float leakGuard = saturate(_BurtGIParams4.x);

                float3 tangentWS;
                float3 bitangentWS;
                BurtGIBuildTangentBasis(normalWS, tangentWS, bitangentWS);

                int sampleCount = clamp((int)round(_BurtGIParams0.y), 1, 32);
                int maxSteps = clamp((int)round(_BurtGIParams0.z), 1, 64);
                int stepCount = min(maxSteps, sampleCount);
                float radius = max(_BurtGIParams0.x, 0.05f);
                float thickness = max(_BurtGIParams0.w, 0.01f);
                float normalWeightAmount = saturate(_BurtGIParams2.y);
                float2 pixelPosition = screenUV * _BurtGISourceTexelSize.zw;
                float rotation = BurtGIHash12(pixelPosition, _BurtGIParams2.x) * 6.2831853f;
                float sequenceJitter = BurtGIHash12(pixelPosition + 17.17f, _BurtGIParams2.x + 5.0f);
                float3 tracedRadiance = 0.0f;
                float totalWeight = 0.0f;

                [loop]
                for (int i = 0; i < 32; ++i)
                {
                    if (i >= stepCount)
                    {
                        break;
                    }

                    float sampleJitter = BurtGIHash12(pixelPosition + (float)i * float2(19.19f, 47.13f), _BurtGIParams2.x + (float)i * 3.0f);
                    float sampleFraction = (float)i + sampleJitter;
                    sampleFraction = saturate((sampleFraction + 0.35f) / (float)sampleCount);
                    float angle = rotation + ((float)i + sequenceJitter) * 2.3999632f;
                    float3 sampleDirectionWS = BurtGIBuildSampleDirection(normalWS, tangentWS, bitangentWS, angle, sampleFraction);
                    float sampleDistance = radius * lerp(0.12f, 1.0f, sampleFraction * sampleFraction);
                    float3 probePositionWS = positionWS + sampleDirectionWS * sampleDistance;
                    float2 sampleUV;
                    float probeRawDepth;
                    float probeLinearDepth;
                    if (!BurtGIProjectPositionWS(probePositionWS, sampleUV, probeRawDepth, probeLinearDepth))
                    {
                        continue;
                    }

                    float sampleRawDepth = BurtSampleDeferredRawDepth(sampleUV);
                    if (BurtGIIsSkyDepth(sampleRawDepth))
                    {
                        continue;
                    }

                    float3 samplePositionWS = BurtReconstructDeferredPositionWS(sampleUV, sampleRawDepth);
                    float3 deltaWS = samplePositionWS - positionWS;
                    float distanceWS = length(deltaWS);
                    if (distanceWS <= 0.0001f)
                    {
                        continue;
                    }

                    float3 sampleDirectionFromCenterWS = deltaWS / distanceWS;
                    float3 sampleToCenterWS = -deltaWS / max(distanceWS, 0.0001f);
                    float3 sampleNormalWS = BurtGISampleNormalWS(sampleUV);
                    float sampleLinearDepth = LinearEyeDepth(sampleRawDepth);
                    float depthError = abs(probeLinearDepth - sampleLinearDepth);
                    float guardedThickness = thickness * lerp(1.0f, 0.55f, leakGuard * edgeFactor);
                    float depthWeight = 1.0f - smoothstep(guardedThickness, guardedThickness + max(radius * lerp(0.3f, 0.14f, leakGuard), 0.01f), depthError);
                    float distanceWeight = 1.0f - smoothstep(radius * 0.15f, radius * 1.35f, distanceWS);
                    float normalFacingWeight = smoothstep(0.06f, 0.28f, saturate(dot(normalWS, sampleDirectionFromCenterWS)));
                    float edgeNormalWeightAmount = saturate(normalWeightAmount + leakGuard * edgeFactor * 0.35f);
                    float sampleNormalWeight = lerp(1.0f, saturate(dot(sampleNormalWS, sampleToCenterWS)), edgeNormalWeightAmount);
                    float weight = depthWeight * distanceWeight * normalFacingWeight * sampleNormalWeight;
                    if (weight <= 0.0001f)
                    {
                        continue;
                    }

                    BurtEncodedGBuffer sampleEncodedGBuffer = BurtSampleEncodedGBuffer(sampleUV);
                    BurtPBRMaterialData sampleMaterialData = BurtPreparePBRMaterialData(BurtDecodeDeferredGBuffer(sampleEncodedGBuffer, sampleUV));
                    float3 sampleDiffuseRadiance = BurtGIEstimateSampleDiffuseRadiance(sampleMaterialData, sampleNormalWS);
                    sampleDiffuseRadiance = BurtGITintRadianceByDiffuseAlbedo(sampleDiffuseRadiance, sampleMaterialData);
                    float3 sampleRadiance =
                        sampleDiffuseRadiance * BurtGIComputeDiffuseSourceWeight(sampleMaterialData) +
                        max(sampleEncodedGBuffer.GBuffer4.rgb, 0.0f);
                    tracedRadiance += sampleRadiance * weight;
                    totalWeight += weight;
                }

                float hitRatio = saturate(totalWeight / max((float)stepCount * 0.35f, 1.0f));
                float3 screenIrradiance = totalWeight > 0.0001f ? tracedRadiance / totalWeight : 0.0f;
                float screenHitEnergy = totalWeight > 0.0001f ? saturate(lerp(sqrt(hitRatio), pow(hitRatio, 0.35f), 0.55f)) : 0.0f;
                float3 nearFieldColorBleed = 0.0f;
                float nearFieldColorBleedStrength = 0.0f;
                BurtGIGatherNearFieldColorBleed(screenUV, positionWS, normalWS, radius, edgeFactor, nearFieldColorBleed, nearFieldColorBleedStrength);
                float nearFieldEnergy = nearFieldColorBleedStrength * (1.0f - screenHitEnergy * 0.35f);
                screenIrradiance += nearFieldColorBleed * nearFieldEnergy;
                screenHitEnergy = saturate(screenHitEnergy + nearFieldEnergy);
                float skyFallbackWeight = 1.0f - screenHitEnergy;
                float3 skyDiffuse = BurtSampleIndirectDiffuseIrradiance(normalWS) * _BurtGIParams1.y * skyFallbackWeight;
                // XRender parity: DiffuseIndirectTexture stores incident diffuse lighting (Fc).
                // Receiver DiffuseColor and material AO are applied exactly once by deferred lighting.
                float3 indirectDiffuseLighting = (screenIrradiance * screenHitEnergy + skyDiffuse) * BURT_INV_PI * distanceFade;
                return float4(max(indirectDiffuseLighting, 0.0f), hitRatio);
            }

            struct BurtGIScreenProbeOutput
            {
                float4 Radiance : SV_Target0;
                float4 Irradiance : SV_Target1;
                float4 Confidence : SV_Target2;
                float4 HitDistance : SV_Target3;
            };

            struct BurtGIScreenProbeFilterOutput
            {
                float4 Radiance : SV_Target0;
                float4 Irradiance : SV_Target1;
                float4 Confidence : SV_Target2;
            };

            struct BurtGIScreenProbeTemporalFilterOutput
            {
                float4 Radiance : SV_Target0;
                float4 Irradiance : SV_Target1;
                float4 Confidence : SV_Target2;
                float NumFramesAccumulated : SV_Target3;
            };

            struct BurtGIScreenProbeTraceAtlasOutput
            {
                float4 TraceRadiance : SV_Target0;
                float4 TraceHit : SV_Target1;
            };

            struct BurtGIScreenProbePlacementOutput
            {
                float4 ScreenDepth : SV_Target0;
                float4 WorldNormal : SV_Target1;
                float4 WorldPosition : SV_Target2;
            };

            #define BURT_GI_SCREEN_PROBE_IN_TILE_OFFSET_BITS 6u
            #define BURT_GI_SCREEN_PROBE_IN_TILE_OFFSET_MASK 0x3fu
            #define BURT_GI_SCREEN_PROBE_VALID_FLAG 0x2000u
            #define BURT_GI_SCREEN_PROBE_SKIP_SCREEN_TRACE_FLAG 0x4000u
            #define BURT_GI_SCREEN_PROBE_SKIP_ADAPTIVE_FLAG 0x8000u
            #define BURT_GI_SCREEN_PROBE_SPHERICAL_VISIBILITY_FLAG 0x10000u

            float2 BurtGIScreenProbeDirection(int index)
            {
                float2 direction = 0.0f;
                if (index == 0) direction = float2(1.0f, 0.0f);
                if (index == 1) direction = float2(-1.0f, 0.0f);
                if (index == 2) direction = float2(0.0f, 1.0f);
                if (index == 3) direction = float2(0.0f, -1.0f);
                if (index == 4) direction = float2(1.0f, 1.0f);
                if (index == 5) direction = float2(-1.0f, 1.0f);
                if (index == 6) direction = float2(1.0f, -1.0f);
                if (index == 7) direction = float2(-1.0f, -1.0f);
                return direction * rsqrt(max(dot(direction, direction), 0.0001f));
            }

            float BurtGIScreenProbeHammersley8Y(uint index)
            {
                if (index == 1u) return 0.5f;
                if (index == 2u) return 0.25f;
                if (index == 3u) return 0.75f;
                if (index == 4u) return 0.125f;
                if (index == 5u) return 0.625f;
                if (index == 6u) return 0.375f;
                if (index == 7u) return 0.875f;
                return 0.0f;
            }

            uint BurtGIScreenProbeCurrentJitterIndex()
            {
                float encodedJitterIndex = _BurtGIScreenProbeFilterParams.w;
                if (encodedJitterIndex < 0.0f)
                {
                    return (uint)floor(max(-encodedJitterIndex - 1.0f, 0.0f)) & 7u;
                }

                return (uint)floor(encodedJitterIndex) & 7u;
            }

            uint BurtGIScreenProbePreviousJitterIndex()
            {
                float encodedJitterIndex = _BurtGIScreenProbeFilterParams.w;
                uint currentJitterIndex = BurtGIScreenProbeCurrentJitterIndex();
                return encodedJitterIndex < 0.0f ? currentJitterIndex : ((currentJitterIndex + 7u) & 7u);
            }

            float2 BurtGIScreenProbeTileJitterPixelsForIndex(uint jitterIndex, float spacingPixels)
            {
                jitterIndex &= 7u;
                float2 hammersley = float2((float)jitterIndex * 0.125f, BurtGIScreenProbeHammersley8Y(jitterIndex));
                return min(floor(hammersley * spacingPixels), spacingPixels - 1.0f);
            }

            float2 BurtGIScreenProbeTileJitterPixels(float spacingPixels)
            {
                return BurtGIScreenProbeTileJitterPixelsForIndex(BurtGIScreenProbeCurrentJitterIndex(), spacingPixels);
            }

            float2 BurtGIScreenProbeTileCoordFromScreenUV(float2 screenUV, uint jitterIndex)
            {
                float spacingPixels = max(_BurtGIScreenProbeParams.x, 1.0f);
                float2 jitterPixels = BurtGIScreenProbeTileJitterPixelsForIndex(jitterIndex, spacingPixels);
                float2 screenPixel = screenUV * max(_BurtGISourceTexelSize.zw, 1.0f);
                return (screenPixel - (jitterPixels + 0.5f)) / spacingPixels;
            }

            float2 BurtGIScreenProbeUpsampleFullResCoord(float2 screenUV, float spacingPixels)
            {
                float2 viewSize = max(_BurtGISourceTexelSize.zw, 1.0f);
                float2 jitterPixels = BurtGIScreenProbeTileJitterPixels(spacingPixels);
                return clamp(screenUV * viewSize - jitterPixels, 0.0f, viewSize - 1.0f);
            }

            float2 BurtGIScreenProbeFallbackCoord(float2 screenUV, float2 gridSize)
            {
                float2 probeCoord = floor(BurtGIScreenProbeTileCoordFromScreenUV(screenUV, BurtGIScreenProbeCurrentJitterIndex()));
                return clamp(probeCoord, 0.0f, gridSize - 1.0f);
            }

            uint BurtGIHashScreenProbeTile(uint2 screenProbeCoord)
            {
                uint hash = screenProbeCoord.x * 1664525u + screenProbeCoord.y * 1013904223u + 374761393u;
                hash ^= hash >> 13;
                hash *= 1274126177u;
                return hash ^ (hash >> 16);
            }

            uint2 BurtGIHiZMipSize(uint2 fullSize, uint mipLevel)
            {
                uint divisor = 1u << min(mipLevel, 15u);
                return max((fullSize + divisor - 1u) / divisor, uint2(1u, 1u));
            }

            bool BurtGIHiZHasSurface(uint2 coord, uint mipLevel, uint2 fullSize)
            {
                uint2 mipSize = BurtGIHiZMipSize(fullSize, mipLevel);
                float rawDepth = _BurtHiZDepthTexture.Load(int3(min(coord, mipSize - 1u), mipLevel));
                return !BurtGIIsSkyDepth(rawDepth);
            }

            bool BurtGIScreenProbeLocateValidPixelViaHiZ(uint2 screenProbeCoord, uint2 tileStart, uint spacingPixels, out uint2 validPixel)
            {
                validPixel = tileStart;
                if (_BurtGIScreenProbePlacementParams.x <= 0.5f)
                {
                    return false;
                }

                uint2 viewSize = max((uint2)_BurtGISourceTexelSize.zw, uint2(1u, 1u));
                uint startMip = min(12u, (uint)floor(log2((float)max(spacingPixels, 1u))));
                uint2 current = min(tileStart >> startMip, BurtGIHiZMipSize(viewSize, startMip) - 1u);
                if (!BurtGIHiZHasSurface(current, startMip, viewSize))
                {
                    return false;
                }

                uint hash = BurtGIHashScreenProbeTile(screenProbeCoord);
                for (int childMip = (int)startMip - 1; childMip >= 0; --childMip)
                {
                    uint2 childBase = current << 1u;
                    uint2 mipSize = BurtGIHiZMipSize(viewSize, (uint)childMip);
                    uint childSelector = (hash + (uint)childMip) & 3u;
                    bool foundChild = false;

                    [unroll]
                    for (uint childIndex = 0u; childIndex < 4u; ++childIndex)
                    {
                        uint selectedChild = (childIndex + childSelector) & 3u;
                        uint2 childCoord = min(childBase + uint2(selectedChild & 1u, (selectedChild >> 1u) & 1u), mipSize - 1u);
                        if (BurtGIHiZHasSurface(childCoord, (uint)childMip, viewSize))
                        {
                            current = childCoord;
                            foundChild = true;
                            break;
                        }
                    }

                    if (!foundChild)
                    {
                        break;
                    }
                }

                uint2 tileEnd = min(tileStart + spacingPixels, viewSize);
                validPixel = clamp(current, tileStart, max(tileEnd, tileStart + 1u) - 1u);
                return !BurtGIIsSkyDepth(BurtSampleDeferredRawDepth(((float2)validPixel + 0.5f) * _BurtGISourceTexelSize.xy));
            }

            float BurtGIEncodeScreenProbePlacementPayload(
                float2 inTileOffset,
                bool screenTraceSkipMaterial,
                bool adaptiveSkipMaterial,
                bool sphericalVisibilityMaterial)
            {
                uint2 clampedInTileOffset = min((uint2)floor(max(inTileOffset, 0.0f)), uint2(BURT_GI_SCREEN_PROBE_IN_TILE_OFFSET_MASK, BURT_GI_SCREEN_PROBE_IN_TILE_OFFSET_MASK));
                uint placementPayload = clampedInTileOffset.x |
                    (clampedInTileOffset.y << BURT_GI_SCREEN_PROBE_IN_TILE_OFFSET_BITS) |
                    BURT_GI_SCREEN_PROBE_VALID_FLAG;
                placementPayload |= screenTraceSkipMaterial ? BURT_GI_SCREEN_PROBE_SKIP_SCREEN_TRACE_FLAG : 0u;
                placementPayload |= adaptiveSkipMaterial ? BURT_GI_SCREEN_PROBE_SKIP_ADAPTIVE_FLAG : 0u;
                placementPayload |= sphericalVisibilityMaterial ? BURT_GI_SCREEN_PROBE_SPHERICAL_VISIBILITY_FLAG : 0u;
                return (float)placementPayload;
            }

            float2 BurtGIDecodeScreenProbePlacementOffset(float encodedPlacementPayload, float spacingPixels)
            {
                uint placementPayload = (uint)round(max(encodedPlacementPayload, 0.0f));
                float2 decodedOffset = BurtGIScreenProbeTileJitterPixels(spacingPixels);
                if ((placementPayload & BURT_GI_SCREEN_PROBE_VALID_FLAG) != 0u)
                {
                    uint inTileOffsetX = placementPayload & BURT_GI_SCREEN_PROBE_IN_TILE_OFFSET_MASK;
                    uint inTileOffsetY = (placementPayload >> BURT_GI_SCREEN_PROBE_IN_TILE_OFFSET_BITS) & BURT_GI_SCREEN_PROBE_IN_TILE_OFFSET_MASK;
                    decodedOffset = min(float2(inTileOffsetX, inTileOffsetY), spacingPixels - 1.0f);
                }

                return decodedOffset;
            }

            float2 BurtGIScreenProbeSourceUVFromOffset(float2 probeUV, float2 inTileOffset)
            {
                float2 gridSize = max(_BurtGIScreenProbeGridParams.xy, 1.0f);
                float2 probePixel = min(floor(saturate(probeUV) * gridSize), gridSize - 1.0f);
                float spacingPixels = max(_BurtGIScreenProbeParams.x, 1.0f);
                float2 sourcePixel = probePixel * spacingPixels + min(max(inTileOffset, 0.0f), spacingPixels - 1.0f);
                return saturate((sourcePixel + 0.5f) * _BurtGISourceTexelSize.xy);
            }

            float2 BurtGIScreenProbeJitteredSourceUV(float2 probeUV)
            {
                float spacingPixels = max(_BurtGIScreenProbeParams.x, 1.0f);
                return BurtGIScreenProbeSourceUVFromOffset(probeUV, BurtGIScreenProbeTileJitterPixels(spacingPixels));
            }

            float2 BurtGIScreenProbeSourceUV(float2 probeUV)
            {
                float2 gridSize = max(_BurtGIScreenProbeGridParams.xy, 1.0f);
                float2 probePixel = min(floor(saturate(probeUV) * gridSize), gridSize - 1.0f);
                float2 probeCenterUV = (probePixel + 0.5f) * _BurtGIScreenProbeGridParams.zw;
                float4 encodedPosition = tex2D(_BurtGIScreenProbeWorldPositionTexture, probeCenterUV);
                float spacingPixels = max(_BurtGIScreenProbeParams.x, 1.0f);
                return BurtGIScreenProbeSourceUVFromOffset(probeUV, BurtGIDecodeScreenProbePlacementOffset(encodedPosition.a, spacingPixels));
            }

            float BurtGIEncodeScreenProbeHitDistanceForFiltering(float hitDistance)
            {
                float maxHitDistance = max(_BurtGIScreenProbeParams.y, 0.001f);
                return sqrt(saturate(hitDistance / maxHitDistance)) * (254.0f / 255.0f) + (1.0f / 255.0f);
            }

            float BurtGIDecodeScreenProbeHitDistanceForFiltering(float encodedHitDistance, float hitValid)
            {
                if (hitValid <= 0.0001f || encodedHitDistance <= (1.0f / 255.0f))
                {
                    return -1.0f;
                }

                float normalizedDistance = saturate((encodedHitDistance - (1.0f / 255.0f)) * (255.0f / 254.0f));
                return normalizedDistance * normalizedDistance * max(_BurtGIScreenProbeParams.y, 0.001f);
            }

            BurtGIScreenProbePlacementOutput FragScreenProbeLitePlacementUniform(Varyings input)
            {
                float2 sourceUV = BurtGIScreenProbeJitteredSourceUV(input.ScreenUV);
                float rawDepth = BurtSampleDeferredRawDepth(sourceUV);
                float2 gridSize = max(_BurtGIScreenProbeGridParams.xy, 1.0f);
                uint2 screenProbeCoord = (uint2)min(floor(saturate(input.ScreenUV) * gridSize), gridSize - 1.0f);
                float spacingPixelsFloat = max(_BurtGIScreenProbeParams.x, 1.0f);
                uint spacingPixels = max(1u, (uint)ceil(spacingPixelsFloat));
                uint2 viewSize = max((uint2)_BurtGISourceTexelSize.zw, uint2(1u, 1u));
                uint2 tileStart = min((uint2)floor((float2)screenProbeCoord * spacingPixelsFloat), viewSize - 1u);
                float2 inTileOffset = BurtGIScreenProbeTileJitterPixels(spacingPixelsFloat);

                BurtGIScreenProbePlacementOutput output;
                output.ScreenDepth = float4(rawDepth, 0.0f, 0.0f, 0.0f);
                output.WorldNormal = float4(0.5f, 0.5f, 1.0f, 0.0f);
                output.WorldPosition = 0.0f;

                if (BurtGIIsSkyDepth(rawDepth))
                {
                    uint2 fallbackPixel;
                    if (!BurtGIScreenProbeLocateValidPixelViaHiZ(screenProbeCoord, tileStart, spacingPixels, fallbackPixel))
                    {
                        return output;
                    }

                    inTileOffset = (float2)(fallbackPixel - tileStart);
                    sourceUV = ((float2)fallbackPixel + 0.5f) * _BurtGISourceTexelSize.xy;
                    rawDepth = BurtSampleDeferredRawDepth(sourceUV);
                    output.ScreenDepth = float4(rawDepth, 0.0f, 0.0f, 0.0f);
                }

                float3 normalWS = normalize(BurtGISampleNormalWS(sourceUV));
                float3 positionWS = BurtReconstructDeferredPositionWS(sourceUV, rawDepth);
                BurtGBufferData gbufferData = BurtDecodeDeferredGBuffer(BurtSampleEncodedGBuffer(sourceUV), sourceUV);
                bool isFoliageMaterial = BurtIsActiveFoliageShadingModel(gbufferData.ShadingModelID);
                bool screenTraceSkipMaterial = BurtIsActiveSubsurfaceShadingModel(gbufferData.ShadingModelID) ||
                    isFoliageMaterial ||
                    BurtIsActiveFurShadingModel(gbufferData.ShadingModelID);
                bool adaptiveSkipMaterial = isFoliageMaterial ||
                    BurtIsActiveHairShadingModel(gbufferData.ShadingModelID);
                bool sphericalVisibilityMaterial = BurtIsActiveSubsurfaceShadingModel(gbufferData.ShadingModelID) ||
                    isFoliageMaterial ||
                    BurtIsActiveHairShadingModel(gbufferData.ShadingModelID);
                float placementPayload = BurtGIEncodeScreenProbePlacementPayload(
                    inTileOffset,
                    screenTraceSkipMaterial,
                    adaptiveSkipMaterial,
                    sphericalVisibilityMaterial);
                output.WorldNormal = float4(normalWS * 0.5f + 0.5f, 1.0f);
                output.WorldPosition = float4(positionWS, placementPayload);
                return output;
            }

            bool BurtGISampleScreenProbePlacement(float2 probeUV, out float rawDepth, out float3 normalWS, out float3 positionWS)
            {
                rawDepth = tex2D(_BurtGIScreenProbeScreenDepthTexture, probeUV).r;
                float4 encodedNormal = tex2D(_BurtGIScreenProbeWorldNormalTexture, probeUV);
                float4 encodedPosition = tex2D(_BurtGIScreenProbeWorldPositionTexture, probeUV);
                normalWS = normalize(encodedNormal.rgb * 2.0f - 1.0f);
                positionWS = encodedPosition.rgb;
                return encodedNormal.a > 0.5f && encodedPosition.a > 0.5f && !BurtGIIsSkyDepth(rawDepth);
            }

            float3 BurtGIOctahedralDirectionFromUV(float2 octUV)
            {
                float2 f = octUV * 2.0f - 1.0f;
                float3 n = float3(f.x, f.y, 1.0f - abs(f.x) - abs(f.y));
                if (n.z < 0.0f)
                {
                    float2 signNotZero = float2(n.x >= 0.0f ? 1.0f : -1.0f, n.y >= 0.0f ? 1.0f : -1.0f);
                    float2 wrapped = (1.0f - abs(n.yx)) * signNotZero;
                    n.xy = wrapped;
                }

                return normalize(n);
            }

            float3 BurtGIEquiAreaSphericalMapping(float2 uv)
            {
                uv = 2.0f * uv - 1.0f;
                float d = 1.0f - (abs(uv.x) + abs(uv.y));
                float r = 1.0f - abs(d);
                float phi = r == 0.0f ? 0.0f : 0.7853981633974483f * ((abs(uv.y) - abs(uv.x)) / r + 1.0f);
                float f = r * sqrt(max(2.0f - r * r, 0.0f));
                return normalize(float3(
                    f * sign(uv.y) * abs(sin(phi)),
                    sign(d) * (1.0f - r * r),
                    f * sign(uv.x) * abs(cos(phi))));
            }

            float2 BurtGIInverseEquiAreaSphericalMapping(float3 direction)
            {
                direction = normalize(float3(direction.z, direction.x, direction.y));
                float3 absDirection = abs(direction);
                float r = sqrt(max(1.0f - absDirection.z, 0.0f));
                float x = min(absDirection.x, absDirection.y) / (max(absDirection.x, absDirection.y) + 1e-8f);
                const float t1 = 0.406758566246788489601959989e-5f;
                const float t2 = 0.636226545274016134946890922156f;
                const float t3 = 0.61572017898280213493197203466e-2f;
                const float t4 = -0.247333733281268944196501420480f;
                const float t5 = 0.881770664775316294736387951347e-1f;
                const float t6 = 0.419038818029165735901852432784e-1f;
                const float t7 = -0.251390972343483509333252996350e-1f;
                float phi = t6 + t7 * x;
                phi = t5 + phi * x;
                phi = t4 + phi * x;
                phi = t3 + phi * x;
                phi = t2 + phi * x;
                phi = t1 + phi * x;
                phi = absDirection.x < absDirection.y ? 1.0f - phi : phi;
                float2 uv = float2(r - phi * r, phi * r);
                uv = direction.z < 0.0f ? 1.0f - uv.yx : uv;
                uv.x = direction.x < 0.0f ? -uv.x : uv.x;
                uv.y = direction.y < 0.0f ? -uv.y : uv.y;
                return saturate(uv * 0.5f + 0.5f);
            }

            float2 BurtGITraceAtlasUV(float2 probeCoord, float2 octCoord, float octResolution)
            {
                float2 atlasSize = max(_BurtGIScreenProbeTraceParams.yz, 1.0f);
                return (probeCoord * octResolution + octCoord + 0.5f) / atlasSize;
            }

            void BurtGIUnpackScreenProbeImportanceRayInfo(uint rayInfo, out uint2 texelCoord, out uint level)
            {
                texelCoord.x = rayInfo & 0x3fu;
                texelCoord.y = (rayInfo >> 6) & 0x3fu;
                level = (rayInfo >> 12) & 0xfu;
            }

            void BurtGIResolveScreenProbeImportanceTraceSample(
                float2 probeCoord,
                float2 traceTexelCoord,
                float octResolution,
                out float3 sampleDirectionWS,
                out float sampleWeight)
            {
                uint traceResolution = max(1u, (uint)octResolution);
                uint maxImportanceTraceResolution = min(64u, max(traceResolution, traceResolution << 1u));
                uint2 traceTexelCoordUInt = (uint2)traceTexelCoord;
                uint2 atlasTexelCoord = (uint2)probeCoord * traceResolution + traceTexelCoordUInt;
                uint2 sourceTraceTexelCoord;
                uint sourceTraceLevel;
                BurtGIUnpackScreenProbeImportanceRayInfo(
                    _BurtGIScreenProbeImportanceRayInfoTexture.Load(int3(atlasTexelCoord, 0)),
                    sourceTraceTexelCoord,
                    sourceTraceLevel);

                uint sourceMipSize = max(1u, maxImportanceTraceResolution >> min(sourceTraceLevel, 5u));
                if (sourceTraceTexelCoord.x >= sourceMipSize || sourceTraceTexelCoord.y >= sourceMipSize)
                {
                    sourceTraceTexelCoord = traceTexelCoordUInt;
                    sourceTraceLevel = 0u;
                    sourceMipSize = traceResolution;
                }

                float2 sourceOctUV = (float2(sourceTraceTexelCoord) + 0.5f) / (float)sourceMipSize;
                sampleDirectionWS = BurtGIEquiAreaSphericalMapping(sourceOctUV);
                sampleWeight = 6.0f / (float)sourceMipSize;
                sampleWeight *= sampleWeight;
            }

            float BurtGISampleScreenProbeTraceHitAverage(float2 probeUV)
            {
                float2 gridSize = max(_BurtGIScreenProbeGridParams.xy, 1.0f);
                float2 probeCoord = min(floor(saturate(probeUV) * gridSize), gridSize - 1.0f);
                float octResolution = max(_BurtGIScreenProbeTraceParams.x, 1.0f);
                float traceHitSum = 0.0f;
                float weightSum = 0.0f;

                [unroll(8)]
                for (int y = 0; y < 8; ++y)
                {
                    [unroll(8)]
                    for (int x = 0; x < 8; ++x)
                    {
                        float2 octCoord = float2((float)x, (float)y);
                        float3 sampleDirectionWS;
                        float traceSampleWeight;
                        BurtGIResolveScreenProbeImportanceTraceSample(
                            probeCoord,
                            octCoord,
                            octResolution,
                            sampleDirectionWS,
                            traceSampleWeight);
                        traceHitSum += saturate(tex2D(_BurtGIScreenProbeTraceHitTexture, BurtGITraceAtlasUV(probeCoord, octCoord, octResolution)).r) * traceSampleWeight;
                        weightSum += traceSampleWeight;
                    }
                }

                return saturate(traceHitSum / max(weightSum, 0.0001f));
            }

            float BurtGISampleHistoryScreenProbeTraceHitAverage(float2 probeUV)
            {
                float2 gridSize = max(_BurtGIScreenProbeGridParams.xy, 1.0f);
                float2 probeCoord = min(floor(saturate(probeUV) * gridSize), gridSize - 1.0f);
                float octResolution = max(_BurtGIScreenProbeTraceParams.x, 1.0f);
                float2 centerOctCoord = (octResolution - 1.0f) * 0.5f;
                float traceHitSum = 0.0f;

                [unroll]
                for (int y = 0; y < 2; ++y)
                {
                    [unroll]
                    for (int x = 0; x < 2; ++x)
                    {
                        float2 octCoord = clamp(centerOctCoord + float2((float)x - 0.5f, (float)y - 0.5f), 0.0f, octResolution - 1.0f);
                        traceHitSum += saturate(tex2D(_BurtGIScreenProbeHistoryTraceHitTexture, BurtGITraceAtlasUV(probeCoord, octCoord, octResolution)).r);
                    }
                }

                return traceHitSum * 0.25f;
            }

            void BurtGIUnpackAdaptiveProbeData(uint packedProbeData, out uint2 adaptiveFineProbeCoord, out uint adaptiveLevel)
            {
                adaptiveFineProbeCoord.x = packedProbeData & 0x3fffu;
                adaptiveFineProbeCoord.y = (packedProbeData >> 14) & 0x3fffu;
                adaptiveLevel = (packedProbeData >> 28) & 0xfu;
            }

            uint2 BurtGIAdaptiveFineProbeCoordToBaseCoord(uint2 adaptiveFineProbeCoord, uint adaptiveLevel, uint2 gridSize)
            {
                uint adaptiveSubGridSize = 1u << min(adaptiveLevel, 13u);
                return min(adaptiveFineProbeCoord / max(1u, adaptiveSubGridSize), gridSize - 1u);
            }

            float2 BurtGIAdaptiveFineProbeCoordToScreenUV(uint2 adaptiveFineProbeCoord, uint adaptiveLevel, float2 gridSize)
            {
                float adaptiveSubGridSize = exp2((float)min(adaptiveLevel, 13u));
                return saturate((float2(adaptiveFineProbeCoord) + 0.5f) / max(gridSize * adaptiveSubGridSize, 1.0f));
            }

            BurtGIScreenProbeTraceAtlasOutput FragScreenProbeLiteTraceAtlas(Varyings input)
            {
                float octResolution = max(_BurtGIScreenProbeTraceParams.x, 1.0f);
                float2 atlasSize = max(_BurtGIScreenProbeTraceParams.yz, 1.0f);
                float2 atlasPixel = min(floor(saturate(input.ScreenUV) * atlasSize), atlasSize - 1.0f);
                float2 probeCoord = floor(atlasPixel / octResolution);
                float2 localCoord = atlasPixel - probeCoord * octResolution;
                float2 probeUV = (probeCoord + 0.5f) * _BurtGIScreenProbeGridParams.zw;
                float2 octUV = (localCoord + 0.5f) / octResolution;
                float3 rayDirection = BurtGIEquiAreaSphericalMapping(octUV);

                float4 probeRadiance = tex2D(_BurtGIScreenProbeRadianceTexture, probeUV);
                float4 probeIrradiance = tex2D(_BurtGIScreenProbeIrradianceTexture, probeUV);
                float4 probeConfidence = tex2D(_BurtGIScreenProbeConfidenceTexture, probeUV);
                float4 probeHitDistance = tex2D(_BurtGIScreenProbeHitDistanceTexture, probeUV);
                float confidence = saturate(min(probeIrradiance.a, probeConfidence.r));
                float hitRatio = saturate(max(probeRadiance.a, probeConfidence.g));
                float directionalEnergy = lerp(0.72f, 1.18f, saturate(rayDirection.z * 0.5f + 0.5f));
                float edgeDamping = lerp(1.0f, 0.55f, saturate(probeConfidence.b));
                float3 traceRadiance = max(probeRadiance.rgb, probeIrradiance.rgb * 0.65f) * directionalEnergy * edgeDamping;

                BurtGIScreenProbeTraceAtlasOutput output;
                output.TraceRadiance = float4(max(traceRadiance, 0.0f), hitRatio);
                output.TraceHit = float4(saturate(confidence * hitRatio), probeHitDistance.r, probeHitDistance.g, confidence);
                return output;
            }

            void BurtGISampleFilteredScreenProbeLite(float2 screenUV, out float4 probeRadiance, out float4 probeIrradiance, out float4 probeConfidence)
            {
                probeRadiance = tex2D(_BurtGIScreenProbeTemporalRadianceTexture, screenUV);
                probeIrradiance = tex2D(_BurtGIScreenProbeTemporalIrradianceTexture, screenUV);
                probeConfidence = tex2D(_BurtGIScreenProbeTemporalConfidenceTexture, screenUV);
                float4 probeHitDistance = tex2D(_BurtGIScreenProbeHitDistanceTexture, screenUV);
                float spatialPasses = _BurtGIScreenProbeFilterParams.x;
                if (spatialPasses <= 0.0001f)
                {
                    return;
                }

                float centerConfidence = saturate(min(probeIrradiance.a, probeConfidence.r));
                float centerTraceHit = BurtGISampleScreenProbeTraceHitAverage(screenUV);
                float centerRawDepth = tex2D(_BurtGIScreenProbeScreenDepthTexture, screenUV).r;
                float centerSceneDepth = LinearEyeDepth(centerRawDepth);
                float4 centerWorldPositionData = tex2D(_BurtGIScreenProbeWorldPositionTexture, screenUV);
                float3 centerWorldPosition = centerWorldPositionData.xyz;
                float3 centerNormalWS = normalize(tex2D(_BurtGIScreenProbeWorldNormalTexture, screenUV).xyz * 2.0f - 1.0f);
                float centerHitDistance = BurtGIDecodeScreenProbeHitDistanceForFiltering(probeHitDistance.r, probeHitDistance.z);
                float halfKernel = max(_BurtGIScreenProbeFilterParams.y, 1.0f);
                float2 probeTexel = _BurtGIScreenProbeGridParams.zw * halfKernel;
                float spatialStrength = 1.0f;
                float centerWeight = 1.0f;
                float4 radianceSum = probeRadiance * centerWeight;
                float4 irradianceSum = probeIrradiance * centerWeight;
                float4 confidenceSum = probeConfidence * centerWeight;
                float weightSum = centerWeight;

                [unroll(4)]
                for (int i = 0; i < 4; ++i)
                {
                    float2 neighborUV = saturate(screenUV + BurtGIScreenProbeDirection(i) * probeTexel);
                    float4 neighborRadiance = tex2D(_BurtGIScreenProbeTemporalRadianceTexture, neighborUV);
                    float4 neighborIrradiance = tex2D(_BurtGIScreenProbeTemporalIrradianceTexture, neighborUV);
                    float4 neighborConfidence = tex2D(_BurtGIScreenProbeTemporalConfidenceTexture, neighborUV);
                    float4 neighborHitDistance = tex2D(_BurtGIScreenProbeHitDistanceTexture, neighborUV);
                    float neighborRawDepth = tex2D(_BurtGIScreenProbeScreenDepthTexture, neighborUV).r;
                    float neighborSceneDepth = LinearEyeDepth(neighborRawDepth);
                    float relativeDepthDifference = abs(neighborSceneDepth - centerSceneDepth) / max(centerSceneDepth, 0.0001f);
                    float positionWeight = (!BurtGIIsSkyDepth(neighborRawDepth) && centerWorldPositionData.a > 0.5f)
                        ? exp2(-max(_BurtGIScreenProbeSpatialFilterParams.y, 0.001f) * relativeDepthDifference * relativeDepthDifference)
                        : 0.0f;
                    float neighborHitDistanceForFiltering = BurtGIDecodeScreenProbeHitDistanceForFiltering(neighborHitDistance.r, neighborHitDistance.z);
                    float angleWeight = 1.0f;
                    if (positionWeight > 0.0f && neighborHitDistanceForFiltering >= 0.0f)
                    {
                        if (centerHitDistance >= 0.0f)
                        {
                            neighborHitDistanceForFiltering = min(neighborHitDistanceForFiltering, centerHitDistance);
                        }

                        float3 neighborWorldPosition = tex2D(_BurtGIScreenProbeWorldPositionTexture, neighborUV).xyz;
                        float3 neighborHitPosition = neighborWorldPosition + centerNormalWS * neighborHitDistanceForFiltering;
                        float3 toNeighborHit = neighborHitPosition - centerWorldPosition;
                        float toNeighborHitLength = length(toNeighborHit);
                        float neighborAngle = toNeighborHitLength > 0.0001f
                            ? acos(saturate(dot(toNeighborHit / toNeighborHitLength, centerNormalWS)))
                            : 0.0f;
                        angleWeight = 1.0f - saturate(neighborAngle / max(_BurtGIScreenProbeSpatialFilterParams.x, 0.001f));
                    }

                    float neighborTraceHit = BurtGISampleScreenProbeTraceHitAverage(neighborUV);
                    float traceHitWeight = lerp(
                        1.0f,
                        1.0f - smoothstep(0.18f, 0.65f, abs(neighborTraceHit - centerTraceHit)),
                        saturate(max(centerTraceHit, neighborTraceHit)));
                    float filterWeight = positionWeight * angleWeight * traceHitWeight;
                    radianceSum += neighborRadiance * filterWeight;
                    irradianceSum += neighborIrradiance * filterWeight;
                    confidenceSum += neighborConfidence * filterWeight;
                    weightSum += filterWeight;
                }

                float4 filteredRadiance = radianceSum / max(weightSum, 0.0001f);
                float4 filteredIrradiance = irradianceSum / max(weightSum, 0.0001f);
                float4 filteredConfidence = confidenceSum / max(weightSum, 0.0001f);
                float filterApply = spatialStrength;
                probeRadiance = lerp(probeRadiance, filteredRadiance, filterApply);
                probeIrradiance = lerp(probeIrradiance, filteredIrradiance, filterApply);
                probeConfidence = lerp(probeConfidence, filteredConfidence, filterApply);
                probeRadiance.rgb = max(probeRadiance.rgb, 0.0f);
                probeRadiance.a = saturate(probeRadiance.a);
                probeIrradiance.rgb = max(probeIrradiance.rgb, 0.0f);
                probeIrradiance.a = saturate(probeIrradiance.a);
                probeConfidence.rgb = saturate(probeConfidence.rgb);
            }

            BurtGIScreenProbeFilterOutput FragScreenProbeLiteSpatialFilter(Varyings input)
            {
                BurtGIScreenProbeFilterOutput output;
                float4 radiance;
                float4 irradiance;
                float4 confidence;
                BurtGISampleFilteredScreenProbeLite(input.ScreenUV, radiance, irradiance, confidence);
                output.Radiance = radiance;
                output.Irradiance = irradiance;
                output.Confidence = confidence;
                return output;
            }

            void BurtGISampleFixupScreenProbeLite(float2 screenUV, out float4 probeRadiance, out float4 probeIrradiance, out float4 probeConfidence)
            {
                probeRadiance = tex2D(_BurtGIScreenProbeFilteredRadianceTexture, screenUV);
                probeIrradiance = tex2D(_BurtGIScreenProbeFilteredIrradianceTexture, screenUV);
                probeConfidence = tex2D(_BurtGIScreenProbeFilteredConfidenceTexture, screenUV);
                if (_BurtGIScreenProbeFilterParams.z <= 0.5f)
                {
                    return;
                }

                float centerConfidence = saturate(min(probeIrradiance.a, probeConfidence.r));
                float centerEdge = saturate(probeConfidence.b);
                float4 centerHitDistance = tex2D(_BurtGIScreenProbeHitDistanceTexture, screenUV);
                float centerTraceHit = BurtGISampleScreenProbeTraceHitAverage(screenUV);
                float fixupNeed = saturate((0.62f - centerConfidence) * 2.0f + centerEdge * 0.22f);
                if (fixupNeed <= 0.0001f)
                {
                    return;
                }

                float2 probeTexel = _BurtGIScreenProbeGridParams.zw;
                float centerWeight = lerp(2.0f, 4.0f, centerConfidence);
                float4 radianceSum = probeRadiance * centerWeight;
                float4 irradianceSum = probeIrradiance * centerWeight;
                float4 confidenceSum = probeConfidence * centerWeight;
                float weightSum = centerWeight;

                [unroll(8)]
                for (int i = 0; i < 8; ++i)
                {
                    float2 neighborUV = saturate(screenUV + BurtGIScreenProbeDirection(i) * probeTexel);
                    float4 neighborRadiance = tex2D(_BurtGIScreenProbeFilteredRadianceTexture, neighborUV);
                    float4 neighborIrradiance = tex2D(_BurtGIScreenProbeFilteredIrradianceTexture, neighborUV);
                    float4 neighborConfidence = tex2D(_BurtGIScreenProbeFilteredConfidenceTexture, neighborUV);
                    float4 neighborHitDistance = tex2D(_BurtGIScreenProbeHitDistanceTexture, neighborUV);
                    float neighborConfidenceScalar = saturate(min(neighborIrradiance.a, neighborConfidence.r));
                    float confidenceGain = saturate((neighborConfidenceScalar - centerConfidence) * 2.5f + neighborConfidenceScalar);
                    float edgeDelta = abs(neighborConfidence.b - centerEdge);
                    float edgeWeight = 1.0f - smoothstep(0.35f, 0.95f, edgeDelta);
                    float hitDistanceDelta = abs(neighborHitDistance.r - centerHitDistance.r);
                    float hitDistanceWeight = 1.0f - smoothstep(0.08f, 0.48f, hitDistanceDelta);
                    float neighborTraceHit = BurtGISampleScreenProbeTraceHitAverage(neighborUV);
                    float traceHitWeight = lerp(
                        1.0f,
                        1.0f - smoothstep(0.18f, 0.68f, abs(neighborTraceHit - centerTraceHit)),
                        saturate(max(centerTraceHit, neighborTraceHit)));
                    float traceConfidenceGain = lerp(0.76f, 1.12f, saturate(neighborTraceHit));
                    float ringWeight = i < 4 ? 1.0f : 0.7f;
                    float filterWeight = ringWeight * confidenceGain * traceConfidenceGain * traceHitWeight * lerp(edgeWeight * hitDistanceWeight, hitDistanceWeight, fixupNeed * 0.35f);
                    radianceSum += neighborRadiance * filterWeight;
                    irradianceSum += neighborIrradiance * filterWeight;
                    confidenceSum += neighborConfidence * filterWeight;
                    weightSum += filterWeight;
                }

                float4 fixedRadiance = radianceSum / max(weightSum, 0.0001f);
                float4 fixedIrradiance = irradianceSum / max(weightSum, 0.0001f);
                float4 fixedConfidence = confidenceSum / max(weightSum, 0.0001f);
                float fixupApply = fixupNeed * saturate((weightSum - centerWeight) * 0.35f);
                probeRadiance = lerp(probeRadiance, fixedRadiance, fixupApply);
                probeIrradiance = lerp(probeIrradiance, fixedIrradiance, fixupApply);
                probeConfidence.rgb = saturate(lerp(probeConfidence.rgb, fixedConfidence.rgb, fixupApply));
                probeConfidence.a = fixedConfidence.a;
                probeRadiance.rgb = max(probeRadiance.rgb, 0.0f);
                probeRadiance.a = saturate(probeRadiance.a);
                probeIrradiance.rgb = max(probeIrradiance.rgb, 0.0f);
                probeIrradiance.a = saturate(probeIrradiance.a);
            }

            BurtGIScreenProbeFilterOutput FragScreenProbeLiteFixupBorders(Varyings input)
            {
                BurtGIScreenProbeFilterOutput output;
                BurtGISampleFixupScreenProbeLite(input.ScreenUV, output.Radiance, output.Irradiance, output.Confidence);
                return output;
            }

            float BurtGIScreenProbeMipLuminance(float3 value)
            {
                return dot(max(value, 0.0f), float3(0.2126f, 0.7152f, 0.0722f));
            }

            float BurtGIScreenProbeMipSampleWeight(float4 radiance, float4 irradiance, float4 confidence)
            {
                float signalConfidence = saturate(max(confidence.r, max(radiance.a, irradiance.a)));
                float hitConfidence = saturate(max(confidence.g, signalConfidence));
                float edgePenalty = saturate(confidence.b);
                return max(0.035f, signalConfidence * lerp(1.0f, 0.55f, edgePenalty) * lerp(0.72f, 1.0f, hitConfidence));
            }

            BurtGIScreenProbeFilterOutput FragScreenProbeLiteGenerateMip(Varyings input)
            {
                float2 parentSize = max(_BurtGIScreenProbeGridParams.xy, 1.0f);
                float2 childPixel = floor(input.PositionCS.xy);
                float2 parentBasePixel = childPixel * 2.0f;
                float2 parentPixel00 = min(parentBasePixel + float2(0.0f, 0.0f), parentSize - 1.0f);
                float2 parentPixel10 = min(parentBasePixel + float2(1.0f, 0.0f), parentSize - 1.0f);
                float2 parentPixel01 = min(parentBasePixel + float2(0.0f, 1.0f), parentSize - 1.0f);
                float2 parentPixel11 = min(parentBasePixel + float2(1.0f, 1.0f), parentSize - 1.0f);
                float2 parentUV00 = (parentPixel00 + 0.5f) / parentSize;
                float2 parentUV10 = (parentPixel10 + 0.5f) / parentSize;
                float2 parentUV01 = (parentPixel01 + 0.5f) / parentSize;
                float2 parentUV11 = (parentPixel11 + 0.5f) / parentSize;

                float4 radiance00 = tex2D(_BurtGIScreenProbeFixupRadianceTexture, parentUV00);
                float4 radiance10 = tex2D(_BurtGIScreenProbeFixupRadianceTexture, parentUV10);
                float4 radiance01 = tex2D(_BurtGIScreenProbeFixupRadianceTexture, parentUV01);
                float4 radiance11 = tex2D(_BurtGIScreenProbeFixupRadianceTexture, parentUV11);
                float4 irradiance00 = tex2D(_BurtGIScreenProbeFixupIrradianceTexture, parentUV00);
                float4 irradiance10 = tex2D(_BurtGIScreenProbeFixupIrradianceTexture, parentUV10);
                float4 irradiance01 = tex2D(_BurtGIScreenProbeFixupIrradianceTexture, parentUV01);
                float4 irradiance11 = tex2D(_BurtGIScreenProbeFixupIrradianceTexture, parentUV11);
                float4 confidence00 = tex2D(_BurtGIScreenProbeFixupConfidenceTexture, parentUV00);
                float4 confidence10 = tex2D(_BurtGIScreenProbeFixupConfidenceTexture, parentUV10);
                float4 confidence01 = tex2D(_BurtGIScreenProbeFixupConfidenceTexture, parentUV01);
                float4 confidence11 = tex2D(_BurtGIScreenProbeFixupConfidenceTexture, parentUV11);

                float4 radianceBox = (radiance00 + radiance10 + radiance01 + radiance11) * 0.25f;
                float4 irradianceBox = (irradiance00 + irradiance10 + irradiance01 + irradiance11) * 0.25f;
                float4 confidenceBox = (confidence00 + confidence10 + confidence01 + confidence11) * 0.25f;

                float weight00 = BurtGIScreenProbeMipSampleWeight(radiance00, irradiance00, confidence00);
                float weight10 = BurtGIScreenProbeMipSampleWeight(radiance10, irradiance10, confidence10);
                float weight01 = BurtGIScreenProbeMipSampleWeight(radiance01, irradiance01, confidence01);
                float weight11 = BurtGIScreenProbeMipSampleWeight(radiance11, irradiance11, confidence11);
                float weightSum = weight00 + weight10 + weight01 + weight11;

                BurtGIScreenProbeFilterOutput output;
                output.Radiance = radianceBox;
                output.Irradiance = irradianceBox;
                output.Confidence = confidenceBox;
                if (weightSum > 0.0001f)
                {
                    float invWeightSum = rcp(weightSum);
                    float3 weightedRadiance =
                        (radiance00.rgb * weight00 +
                            radiance10.rgb * weight10 +
                            radiance01.rgb * weight01 +
                            radiance11.rgb * weight11) * invWeightSum;
                    float3 weightedIrradiance =
                        (irradiance00.rgb * weight00 +
                            irradiance10.rgb * weight10 +
                            irradiance01.rgb * weight01 +
                            irradiance11.rgb * weight11) * invWeightSum;
                    float weightedConfidence =
                        (saturate(min(irradiance00.a, confidence00.r)) * weight00 +
                            saturate(min(irradiance10.a, confidence10.r)) * weight10 +
                            saturate(min(irradiance01.a, confidence01.r)) * weight01 +
                            saturate(min(irradiance11.a, confidence11.r)) * weight11) * invWeightSum;
                    float weightedHit =
                        (saturate(confidence00.g) * weight00 +
                            saturate(confidence10.g) * weight10 +
                            saturate(confidence01.g) * weight01 +
                            saturate(confidence11.g) * weight11) * invWeightSum;
                    float weightedEdge =
                        (saturate(confidence00.b) * weight00 +
                            saturate(confidence10.b) * weight10 +
                            saturate(confidence01.b) * weight01 +
                            saturate(confidence11.b) * weight11) * invWeightSum;

                    float radianceMean = BurtGIScreenProbeMipLuminance(weightedRadiance);
                    float irradianceMean = BurtGIScreenProbeMipLuminance(weightedIrradiance);
                    float radianceVariance =
                        abs(BurtGIScreenProbeMipLuminance(radiance00.rgb) - radianceMean) * weight00 +
                        abs(BurtGIScreenProbeMipLuminance(radiance10.rgb) - radianceMean) * weight10 +
                        abs(BurtGIScreenProbeMipLuminance(radiance01.rgb) - radianceMean) * weight01 +
                        abs(BurtGIScreenProbeMipLuminance(radiance11.rgb) - radianceMean) * weight11;
                    float irradianceVariance =
                        abs(BurtGIScreenProbeMipLuminance(irradiance00.rgb) - irradianceMean) * weight00 +
                        abs(BurtGIScreenProbeMipLuminance(irradiance10.rgb) - irradianceMean) * weight10 +
                        abs(BurtGIScreenProbeMipLuminance(irradiance01.rgb) - irradianceMean) * weight01 +
                        abs(BurtGIScreenProbeMipLuminance(irradiance11.rgb) - irradianceMean) * weight11;
                    float varianceScale = max(max(radianceMean, irradianceMean), 0.035f) * weightSum;
                    float lowFrequencyStability = saturate(1.0f - (radianceVariance + irradianceVariance) / max(varianceScale * 3.0f, 0.0001f));
                    float boxBlend = saturate((0.42f - weightedConfidence) * 1.5f + weightedEdge * 0.18f);

                    output.Radiance.rgb = lerp(weightedRadiance, max(radianceBox.rgb, 0.0f), boxBlend * 0.35f);
                    output.Irradiance.rgb = lerp(weightedIrradiance, max(irradianceBox.rgb, 0.0f), boxBlend * 0.35f);
                    output.Radiance.a = saturate(max(radianceBox.a, weightedConfidence * lowFrequencyStability));
                    output.Irradiance.a = saturate(max(irradianceBox.a, weightedConfidence * lowFrequencyStability));
                    output.Confidence.r = saturate(max(confidenceBox.r, weightedConfidence * lerp(0.72f, 1.0f, lowFrequencyStability)));
                    output.Confidence.g = saturate(max(confidenceBox.g, weightedHit));
                    output.Confidence.b = saturate(lerp(weightedEdge, confidenceBox.b, 0.35f));
                }
                output.Radiance.rgb = max(output.Radiance.rgb, 0.0f);
                output.Radiance.a = saturate(output.Radiance.a);
                output.Irradiance.rgb = max(output.Irradiance.rgb, 0.0f);
                output.Irradiance.a = saturate(output.Irradiance.a);
                output.Confidence.rgb = saturate(output.Confidence.rgb);
                return output;
            }

            void BurtGISampleScreenProbeTraceAtlasMomentsAtProbeCoord(
                float2 probeCoord,
                out float3 averageRadiance,
                out float3 momentX,
                out float3 momentY,
                out float hitConfidence)
            {
                float octResolution = max(_BurtGIScreenProbeTraceParams.x, 1.0f);
                uint traceResolution = max(1u, (uint)octResolution);
                uint maxImportanceTraceResolution = min(64u, max(traceResolution, traceResolution << 1u));
                uint2 probeCoordUInt = (uint2)probeCoord;
                float3 radianceSum = 0.0f;
                float3 momentXSum = 0.0f;
                float3 momentYSum = 0.0f;
                float hitSum = 0.0f;
                float gatherSampleCount = 36.0f;

                [unroll(8)]
                for (int y = 0; y < 8; ++y)
                {
                    [unroll(8)]
                    for (int x = 0; x < 8; ++x)
                    {
                        float2 octCoord = float2((float)x, (float)y);
                        float2 octUV = (octCoord + 0.5f) / octResolution;
                        float2 atlasUV = BurtGITraceAtlasUV(probeCoord, octCoord, octResolution);
                        uint2 atlasTexelCoord = probeCoordUInt * traceResolution + uint2((uint)x, (uint)y);
                        uint2 sourceTraceTexelCoord;
                        uint sourceTraceLevel;
                        BurtGIUnpackScreenProbeImportanceRayInfo(
                            _BurtGIScreenProbeImportanceRayInfoTexture.Load(int3(atlasTexelCoord, 0)),
                            sourceTraceTexelCoord,
                            sourceTraceLevel);
                        uint sourceMipSize = max(1u, maxImportanceTraceResolution >> min(sourceTraceLevel, 5u));
                        if (sourceTraceTexelCoord.x >= sourceMipSize || sourceTraceTexelCoord.y >= sourceMipSize)
                        {
                            sourceTraceTexelCoord = uint2((uint)x, (uint)y);
                            sourceTraceLevel = 0u;
                            sourceMipSize = traceResolution;
                        }

                        float2 sourceOctUV = (float2(sourceTraceTexelCoord) + 0.5f) / (float)sourceMipSize;
                        float3 rayDirection = BurtGIEquiAreaSphericalMapping(sourceOctUV);
                        float4 traceRadiance = _BurtGIScreenProbeTraceRadianceTexture.SampleLevel(sampler_LinearClamp, atlasUV, 0.0f);
                        float traceHit = saturate(_BurtGIScreenProbeTraceHitTexture.SampleLevel(sampler_LinearClamp, atlasUV, 0.0f).r);
                        float sampleWeight = 6.0f / (float)sourceMipSize;
                        sampleWeight *= sampleWeight;
                        float3 radiance = max(traceRadiance.rgb, 0.0f);
                        radianceSum += radiance * sampleWeight;
                        momentXSum += radiance * rayDirection.x * sampleWeight;
                        momentYSum += radiance * rayDirection.y * sampleWeight;
                        hitSum += traceHit * sampleWeight;
                    }
                }

                averageRadiance = radianceSum / gatherSampleCount;
                momentX = momentXSum / gatherSampleCount;
                momentY = momentYSum / gatherSampleCount;
                hitConfidence = saturate(hitSum / gatherSampleCount);
            }

            void BurtGISampleScreenProbeTraceAtlasMoments(
                float2 probeUV,
                out float3 averageRadiance,
                out float3 momentX,
                out float3 momentY,
                out float hitConfidence)
            {
                float2 gridSize = max(_BurtGIScreenProbeGridParams.xy, 1.0f);
                float2 probeCoord = min(floor(saturate(probeUV) * gridSize), gridSize - 1.0f);
                BurtGISampleScreenProbeTraceAtlasMomentsAtProbeCoord(probeCoord, averageRadiance, momentX, momentY, hitConfidence);
            }

            void BurtGICompositeScreenProbeTraceAtlasIntoCurrent(float2 probeUV, inout BurtGIScreenProbeOutput current)
            {
                float3 traceAverageRadiance;
                float3 traceMomentX;
                float3 traceMomentY;
                float traceHitConfidence;
                BurtGISampleScreenProbeTraceAtlasMoments(
                    probeUV,
                    traceAverageRadiance,
                    traceMomentX,
                    traceMomentY,
                    traceHitConfidence);

                traceAverageRadiance = max(traceAverageRadiance, 0.0f);
                float traceSignal = saturate(traceHitConfidence);
                if (traceSignal <= 0.0001f)
                {
                    return;
                }

                float currentConfidence = saturate(min(current.Irradiance.a, current.Confidence.r));
                float traceBlend = saturate(traceSignal * lerp(0.72f, 0.38f, currentConfidence));
                float traceIrradianceBlend = traceBlend * 0.68f;
                current.Radiance.rgb = lerp(max(current.Radiance.rgb, 0.0f), traceAverageRadiance, traceBlend);
                current.Irradiance.rgb = lerp(max(current.Irradiance.rgb, 0.0f), traceAverageRadiance, traceIrradianceBlend);
                current.Radiance.a = saturate(max(current.Radiance.a, traceSignal));
                current.Irradiance.a = saturate(max(current.Irradiance.a, traceSignal * 0.86f));
                current.Confidence.r = saturate(max(current.Confidence.r, traceSignal * 0.86f));
                current.Confidence.g = saturate(max(current.Confidence.g, traceSignal));
            }

            bool BurtGISampleAdaptiveScreenProbeTraceAtlas(
                float2 screenUV,
                float3 positionWS,
                float sceneDepth,
                float3 normalWS,
                float currentConfidence,
                float edgeConfidence,
                float receiverFoliageWeight,
                out float3 adaptiveIrradiance,
                out float adaptiveConfidence,
                out float2 adaptiveAtlasProbeCoord)
            {
                adaptiveIrradiance = 0.0f;
                adaptiveConfidence = 0.0f;
                adaptiveAtlasProbeCoord = 0.0f;
                float2 gridSizeFloat = max(_BurtGIScreenProbeGridParams.xy, 1.0f);
                uint2 gridSize = max((uint2)gridSizeFloat, uint2(1u, 1u));
                uint tileCapacity = max(1u, (uint)max(_BurtGIScreenProbeAdaptiveParams.z, 1.0f));
                uint maxAdaptiveProbes = max(1u, (uint)max(_BurtGIScreenProbeAdaptiveParams.w, 1.0f));
                uint uniformProbeCount = gridSize.x * gridSize.y;
                float octResolution = max(_BurtGIScreenProbeTraceParams.x, 1.0f);
                float atlasProbeHeight = floor(max(_BurtGIScreenProbeTraceParams.z, octResolution) / octResolution);
                float2 probeCoordFloat = saturate(screenUV) * gridSizeFloat;
                uint2 baseTile = min((uint2)floor(probeCoordFloat), gridSize - 1u);
                float4 scenePlane = float4(normalize(normalWS), dot(positionWS, normalize(normalWS)));
                float bestScore = 0.0f;

                [unroll]
                for (uint cornerIndex = 0u; cornerIndex < 4u; ++cornerIndex)
                {
                    uint2 screenTileCoord = min(baseTile + uint2(cornerIndex & 1u, cornerIndex >> 1u), gridSize - 1u);
                    uint adaptiveProbeCount = _BurtGIScreenProbeAdaptiveProbeHeaderTexture.Load(int3(screenTileCoord, 0));
                    adaptiveProbeCount = min(adaptiveProbeCount, tileCapacity);

                    [loop]
                    for (uint adaptiveListIndex = 0u; adaptiveListIndex < adaptiveProbeCount; ++adaptiveListIndex)
                    {
                        uint2 adaptiveProbeCoord = uint2(screenTileCoord.x * tileCapacity + adaptiveListIndex, screenTileCoord.y);
                        uint adaptiveProbeIndex = _BurtGIScreenProbeAdaptiveProbeIndicesTexture.Load(int3(adaptiveProbeCoord, 0));
                        if (adaptiveProbeIndex >= maxAdaptiveProbes)
                        {
                            continue;
                        }

                        uint2 adaptiveFineProbeCoord;
                        uint adaptiveLevel;
                        BurtGIUnpackAdaptiveProbeData(_BurtGIScreenProbeAdaptiveProbeDataBuffer[adaptiveProbeIndex], adaptiveFineProbeCoord, adaptiveLevel);
                        uint2 sourceProbeCoord = BurtGIAdaptiveFineProbeCoordToBaseCoord(adaptiveFineProbeCoord, adaptiveLevel, gridSize);
                        if (sourceProbeCoord.x >= gridSize.x || sourceProbeCoord.y >= gridSize.y)
                        {
                            continue;
                        }

                        uint atlasProbeIndex = uniformProbeCount + adaptiveProbeIndex;
                        float2 atlasProbeCoord = float2((float)(atlasProbeIndex % gridSize.x), (float)(atlasProbeIndex / gridSize.x));
                        if (atlasProbeCoord.y >= atlasProbeHeight)
                        {
                            continue;
                        }

                        float2 sourceProbeUV = (float2(sourceProbeCoord) + 0.5f) * _BurtGIScreenProbeGridParams.zw;
                        float2 adaptiveProbeUV = BurtGIAdaptiveFineProbeCoordToScreenUV(adaptiveFineProbeCoord, adaptiveLevel, gridSizeFloat);
                        float4 sourcePosition = _BurtGIScreenProbeWorldPositionTexture.SampleLevel(sampler_LinearClamp, sourceProbeUV, 0.0f);
                        float4 sourceNormal = _BurtGIScreenProbeWorldNormalTexture.SampleLevel(sampler_LinearClamp, sourceProbeUV, 0.0f);
                        if (sourcePosition.a <= 0.5f || sourceNormal.a <= 0.5f)
                        {
                            continue;
                        }

                        float planeDistance = abs(dot(float4(sourcePosition.xyz, -1.0f), scenePlane));
                        float receiverPlaneWeightScale = lerp(1.0f, 0.25f, saturate(receiverFoliageWeight));
                        float planeWeight = exp2(-120.0f * receiverPlaneWeightScale * planeDistance / max(sceneDepth, 0.001f));
                        float3 sourceNormalWS = normalize(sourceNormal.xyz * 2.0f - 1.0f);
                        float normalWeight = lerp(pow(saturate(dot(normalWS, sourceNormalWS)), 6.0f), 1.0f, saturate(receiverFoliageWeight));
                        float2 pixelDistance = abs(adaptiveProbeUV - screenUV) * max(_BurtGISourceTexelSize.zw, 1.0f);
                        float distanceWeight = 1.0f - saturate(min(pixelDistance.x, pixelDistance.y) / max(_BurtGIScreenProbeParams.x, 1.0f));
                        float levelWeight = rcp(1.0f + (float)adaptiveLevel * 0.12f);
                        float3 traceAverage;
                        float3 traceMomentX;
                        float3 traceMomentY;
                        float traceConfidence;
                        BurtGISampleScreenProbeTraceAtlasMomentsAtProbeCoord(atlasProbeCoord, traceAverage, traceMomentX, traceMomentY, traceConfidence);
                        float adaptiveNeed = saturate((0.75f - currentConfidence) * 1.3333333f + edgeConfidence * 0.35f);
                        float candidateScore = planeWeight * normalWeight * distanceWeight * levelWeight * traceConfidence * adaptiveNeed;
                        if (candidateScore > bestScore)
                        {
                            float2 adaptiveOctUV = BurtGIInverseEquiAreaSphericalMapping(normalWS);
                            float2 adaptiveOctLocalCoord = adaptiveOctUV * 6.0f + 1.0f;
                            float2 adaptiveOctAtlasSize = max(float2(gridSizeFloat.x, atlasProbeHeight) * 8.0f, 1.0f);
                            float2 adaptiveOctAtlasTexelCoord = atlasProbeCoord * 8.0f + clamp(adaptiveOctLocalCoord, 0.5f, 7.5f);
                            float4 adaptiveOctSample = _BurtGIScreenProbeIrradianceOctTexture.SampleLevel(sampler_LinearClamp, adaptiveOctAtlasTexelCoord / adaptiveOctAtlasSize, 0.0f);
                            float adaptiveOctBlend = saturate(adaptiveOctSample.a) * 0.45f;
                            bestScore = candidateScore;
                            adaptiveIrradiance = lerp(max(traceAverage, 0.0f), max(adaptiveOctSample.rgb, 0.0f), adaptiveOctBlend);
                            adaptiveConfidence = saturate(max(candidateScore, adaptiveOctSample.a * candidateScore));
                            adaptiveAtlasProbeCoord = atlasProbeCoord;
                        }
                    }
                }

                return bestScore > 0.0001f;
            }

            float4 BurtGISHBasis3V0(float3 direction)
            {
                return float4(
                    0.28209479f,
                    -0.48860251f * direction.y,
                    0.48860251f * direction.z,
                    -0.48860251f * direction.x);
            }

            float4 BurtGISHBasis3V1(float3 direction)
            {
                return float4(
                    1.09254843f * direction.x * direction.y,
                    -1.09254843f * direction.y * direction.z,
                    0.31539157f * (3.0f * direction.z * direction.z - 1.0f),
                    -1.09254843f * direction.x * direction.z);
            }

            float BurtGISHBasis3V2(float3 direction)
            {
                return 0.54627422f * (direction.x * direction.x - direction.y * direction.y);
            }

            static const float BURT_GI_SH0_COEFFICIENT_TO_IRRADIANCE = 3.5449077f;

            void BurtGISampleScreenProbeTraceAtlasSH3(
                float2 probeUV,
                out float3 ambient,
                out float4 red0,
                out float4 red1,
                out float4 green0,
                out float4 green1,
                out float4 blue0,
                out float4 blue1,
                out float hitConfidence)
            {
                float octResolution = max(_BurtGIScreenProbeTraceParams.x, 1.0f);
                uint traceResolution = max(1u, (uint)octResolution);
                uint maxImportanceTraceResolution = min(64u, max(traceResolution, traceResolution << 1u));
                float2 gridSize = max(_BurtGIScreenProbeGridParams.xy, 1.0f);
                float2 probeCoord = min(floor(saturate(probeUV) * gridSize), gridSize - 1.0f);
                uint2 probeCoordUInt = (uint2)probeCoord;
                float3 sh0 = 0.0f;
                float3 sh1 = 0.0f;
                float3 sh2 = 0.0f;
                float3 sh3 = 0.0f;
                float3 sh4 = 0.0f;
                float3 sh5 = 0.0f;
                float3 sh6 = 0.0f;
                float3 sh7 = 0.0f;
                float3 sh8 = 0.0f;
                float hitSum = 0.0f;
                float gatherSampleCount = 36.0f;

                [unroll(8)]
                for (int y = 0; y < 8; ++y)
                {
                    [unroll(8)]
                    for (int x = 0; x < 8; ++x)
                    {
                        float2 octCoord = float2((float)x, (float)y);
                        float2 octUV = (octCoord + 0.5f) / octResolution;
                        float2 atlasUV = BurtGITraceAtlasUV(probeCoord, octCoord, octResolution);
                        uint2 atlasTexelCoord = probeCoordUInt * traceResolution + uint2((uint)x, (uint)y);
                        uint2 sourceTraceTexelCoord;
                        uint sourceTraceLevel;
                        BurtGIUnpackScreenProbeImportanceRayInfo(
                            _BurtGIScreenProbeImportanceRayInfoTexture.Load(int3(atlasTexelCoord, 0)),
                            sourceTraceTexelCoord,
                            sourceTraceLevel);
                        uint sourceMipSize = max(1u, maxImportanceTraceResolution >> min(sourceTraceLevel, 5u));
                        if (sourceTraceTexelCoord.x >= sourceMipSize || sourceTraceTexelCoord.y >= sourceMipSize)
                        {
                            sourceTraceTexelCoord = uint2((uint)x, (uint)y);
                            sourceTraceLevel = 0u;
                            sourceMipSize = traceResolution;
                        }

                        float2 sourceOctUV = (float2(sourceTraceTexelCoord) + 0.5f) / (float)sourceMipSize;
                        float3 traceDirection = BurtGIEquiAreaSphericalMapping(sourceOctUV).zxy;
                        float3 radiance = max(tex2D(_BurtGIScreenProbeTraceRadianceTexture, atlasUV).rgb, 0.0f);
                        float hit = saturate(tex2D(_BurtGIScreenProbeTraceHitTexture, atlasUV).r);
                        float sampleWeight = 6.0f / (float)sourceMipSize;
                        sampleWeight *= sampleWeight;
                        float4 basis0 = BurtGISHBasis3V0(traceDirection);
                        float4 basis1 = BurtGISHBasis3V1(traceDirection);
                        float basis2 = BurtGISHBasis3V2(traceDirection);

                        sh0 += radiance * basis0.x * sampleWeight;
                        sh1 += radiance * basis0.y * sampleWeight;
                        sh2 += radiance * basis0.z * sampleWeight;
                        sh3 += radiance * basis0.w * sampleWeight;
                        sh4 += radiance * basis1.x * sampleWeight;
                        sh5 += radiance * basis1.y * sampleWeight;
                        sh6 += radiance * basis1.z * sampleWeight;
                        sh7 += radiance * basis1.w * sampleWeight;
                        sh8 += radiance * basis2 * sampleWeight;
                        hitSum += hit * sampleWeight;
                    }
                }

                float normalizeWeight = rcp(gatherSampleCount);
                ambient = sh0 * normalizeWeight;
                red0 = float4(sh1.r, sh2.r, sh3.r, sh4.r) * normalizeWeight;
                red1 = float4(sh5.r, sh6.r, sh7.r, sh8.r) * normalizeWeight;
                green0 = float4(sh1.g, sh2.g, sh3.g, sh4.g) * normalizeWeight;
                green1 = float4(sh5.g, sh6.g, sh7.g, sh8.g) * normalizeWeight;
                blue0 = float4(sh1.b, sh2.b, sh3.b, sh4.b) * normalizeWeight;
                blue1 = float4(sh5.b, sh6.b, sh7.b, sh8.b) * normalizeWeight;
                hitConfidence = saturate(hitSum * normalizeWeight);
            }

            float4 FragScreenProbeLiteIrradianceSH(Varyings input) : SV_Target
            {
                if (_BurtGIScreenProbeSHParams.x < 0.5f)
                {
                    float3 traceAverage;
                    float3 traceMomentX;
                    float3 traceMomentY;
                    float traceConfidence;
                    BurtGISampleScreenProbeTraceAtlasMoments(input.ScreenUV, traceAverage, traceMomentX, traceMomentY, traceConfidence);
                    float4 fixupIrradiance = tex2D(_BurtGIScreenProbeFixupIrradianceTexture, input.ScreenUV);
                    float4 fixupConfidence = tex2D(_BurtGIScreenProbeFixupConfidenceTexture, input.ScreenUV);
                    float4 mipIrradiance = tex2D(_BurtGIScreenProbeMipIrradianceTexture, input.ScreenUV);
                    float4 mipConfidence = tex2D(_BurtGIScreenProbeMipConfidenceTexture, input.ScreenUV);
                    float4 mip2Irradiance = tex2D(_BurtGIScreenProbeMip2IrradianceTexture, input.ScreenUV);
                    float4 mip2Confidence = tex2D(_BurtGIScreenProbeMip2ConfidenceTexture, input.ScreenUV);
                    float4 mip3Irradiance = tex2D(_BurtGIScreenProbeMip3IrradianceTexture, input.ScreenUV);
                    float4 mip3Confidence = tex2D(_BurtGIScreenProbeMip3ConfidenceTexture, input.ScreenUV);
                    float confidence = saturate(min(fixupIrradiance.a, fixupConfidence.r));
                    float lowFrequencyConfidence = saturate(min(mipIrradiance.a, mipConfidence.r));
                    float veryLowFrequencyConfidence = saturate(min(mip2Irradiance.a, mip2Confidence.r));
                    float ultraLowFrequencyConfidence = saturate(min(mip3Irradiance.a, mip3Confidence.r));
                    float lowFrequencyFill = saturate((0.55f - confidence) * 2.0f) * lowFrequencyConfidence;
                    float veryLowFrequencyFill = saturate((0.38f - confidence) * 2.6315789f) * veryLowFrequencyConfidence;
                    float ultraLowFrequencyFill = saturate((0.24f - confidence) * 4.1666665f) * ultraLowFrequencyConfidence;
                    float3 veryLowFrequency = lerp(max(mip2Irradiance.rgb, 0.0f), max(mip3Irradiance.rgb, 0.0f), ultraLowFrequencyFill * 0.65f);
                    float3 lowFrequency = lerp(max(mipIrradiance.rgb, 0.0f), veryLowFrequency, veryLowFrequencyFill * 0.75f);
                    float3 ambient = lerp(max(fixupIrradiance.rgb, 0.0f), lowFrequency, lowFrequencyFill * 0.5f + veryLowFrequencyFill * 0.25f + ultraLowFrequencyFill * 0.15f);
                    ambient = lerp(ambient, traceAverage, saturate(traceConfidence * 0.65f));
                    return float4(ambient, saturate(max(max(max(max(confidence, traceConfidence), lowFrequencyConfidence * lowFrequencyFill), veryLowFrequencyConfidence * veryLowFrequencyFill), ultraLowFrequencyConfidence * ultraLowFrequencyFill)));
                }

                float tileCount = max(_BurtGIScreenProbeSHParams.w, 1.0f);
                float scaledX = min(saturate(input.ScreenUV.x) * tileCount, tileCount - 0.0001f);
                float tileIndex = floor(scaledX);
                float2 probeUV = float2(frac(scaledX), saturate(input.ScreenUV.y));
                float3 traceSHAmbient;
                float4 redSH0;
                float4 redSH1;
                float4 greenSH0;
                float4 greenSH1;
                float4 blueSH0;
                float4 blueSH1;
                float traceConfidence;
                BurtGISampleScreenProbeTraceAtlasSH3(probeUV, traceSHAmbient, redSH0, redSH1, greenSH0, greenSH1, blueSH0, blueSH1, traceConfidence);
                float4 centerIrradiance = tex2D(_BurtGIScreenProbeFixupIrradianceTexture, probeUV);
                float4 centerConfidence = tex2D(_BurtGIScreenProbeFixupConfidenceTexture, probeUV);
                float4 hitDistance = tex2D(_BurtGIScreenProbeHitDistanceTexture, probeUV);
                float confidence = saturate(max(min(centerIrradiance.a, centerConfidence.r), traceConfidence));
                float edge = saturate(centerConfidence.b);
                float fallbackOcclusion = saturate(edge + hitDistance.r) * confidence;
                float4 fallback = float4(traceSHAmbient * (0.125f * confidence), fallbackOcclusion);

                if (tileIndex < 0.5f)
                {
                    return lerp(fallback, redSH0, confidence);
                }
                if (tileIndex < 1.5f)
                {
                    return lerp(fallback, redSH1, confidence);
                }
                if (tileIndex < 2.5f)
                {
                    return lerp(fallback, greenSH0, confidence);
                }
                if (tileIndex < 3.5f)
                {
                    return lerp(fallback, greenSH1, confidence);
                }
                if (tileIndex < 4.5f)
                {
                    return lerp(fallback, blueSH0, confidence);
                }

                return lerp(fallback, blueSH1, confidence);
            }

            float4 BurtGISampleScreenProbeSHDirectionalTile(float2 screenUV, float tileIndex)
            {
                float tileCount = 6.0f;
                float x = (tileIndex + saturate(screenUV.x)) / tileCount;
                return tex2D(_BurtGIScreenProbeRadianceSHDirectionalTexture, float2(x, saturate(screenUV.y)));
            }

            float3 BurtGISampleScreenProbeSHDirectionalMagnitude(float2 screenUV)
            {
                float4 red0 = BurtGISampleScreenProbeSHDirectionalTile(screenUV, 0.0f);
                float4 red1 = BurtGISampleScreenProbeSHDirectionalTile(screenUV, 1.0f);
                float4 green0 = BurtGISampleScreenProbeSHDirectionalTile(screenUV, 2.0f);
                float4 green1 = BurtGISampleScreenProbeSHDirectionalTile(screenUV, 3.0f);
                float4 blue0 = BurtGISampleScreenProbeSHDirectionalTile(screenUV, 4.0f);
                float4 blue1 = BurtGISampleScreenProbeSHDirectionalTile(screenUV, 5.0f);
                return float3(
                    length(red0) + length(red1),
                    length(green0) + length(green1),
                    length(blue0) + length(blue1));
            }

            float3 BurtGIEvaluateScreenProbeSH3Irradiance(float2 screenUV, float3 normalWS, float cosThetaAO)
            {
                float4 ambient = tex2D(_BurtGIScreenProbeRadianceSHAmbientTexture, screenUV);
                float4 red0 = BurtGISampleScreenProbeSHDirectionalTile(screenUV, 0.0f);
                float4 red1 = BurtGISampleScreenProbeSHDirectionalTile(screenUV, 1.0f);
                float4 green0 = BurtGISampleScreenProbeSHDirectionalTile(screenUV, 2.0f);
                float4 green1 = BurtGISampleScreenProbeSHDirectionalTile(screenUV, 3.0f);
                float4 blue0 = BurtGISampleScreenProbeSHDirectionalTile(screenUV, 4.0f);
                float4 blue1 = BurtGISampleScreenProbeSHDirectionalTile(screenUV, 5.0f);
                float3 shDirection = normalize(normalWS).zxy;
                float4 basis0 = BurtGISHBasis3V0(shDirection);
                float4 basis1 = BurtGISHBasis3V1(shDirection);
                float basis2 = BurtGISHBasis3V2(shDirection);
                float coneCos = saturate(cosThetaAO);
                float coneCos2 = coneCos * coneCos;
                float coneCos3 = coneCos2 * coneCos;
                float coneCos4 = coneCos2 * coneCos2;
                float l0Scale = saturate(1.0f - coneCos2);
                float l1Scale = saturate(1.0f - coneCos3);
                float l2Scale = max(3.0f * (1.0f - coneCos4) - 2.0f * (1.0f - coneCos2), 0.0f);
                basis0 *= float4(l0Scale, l1Scale, l1Scale, l1Scale);
                basis1 *= l2Scale;
                basis2 *= l2Scale;
                float3 radiance = 0.0f;
                radiance.r = dot(float4(ambient.r, red0.xyz), basis0) + dot(float4(red0.w, red1.xyz), basis1) + red1.w * basis2;
                radiance.g = dot(float4(ambient.g, green0.xyz), basis0) + dot(float4(green0.w, green1.xyz), basis1) + green1.w * basis2;
                radiance.b = dot(float4(ambient.b, blue0.xyz), basis0) + dot(float4(blue0.w, blue1.xyz), basis1) + blue1.w * basis2;
                return max(radiance * 12.5663706f, 0.0f);
            }

            float3 BurtGIEvaluateScreenProbeSH3Irradiance(float2 screenUV, float3 normalWS)
            {
                return BurtGIEvaluateScreenProbeSH3Irradiance(screenUV, normalWS, 0.0f);
            }

            #define BURT_GI_SCREEN_PROBE_IRRADIANCE_OCT_RESOLUTION 6.0f
            #define BURT_GI_SCREEN_PROBE_IRRADIANCE_OCT_RESOLUTION_WITH_BORDER 8.0f
            #define BURT_GI_SCREEN_PROBE_RADIANCE_OCT_RESOLUTION 8.0f
            #define BURT_GI_SCREEN_PROBE_RADIANCE_OCT_RESOLUTION_WITH_BORDER 10.0f

            float2 BurtGIScreenProbeOctWrapBorderCoord(float2 localCoord, float resolutionWithBorder)
            {
                float2 wrappedCoord = localCoord;

                if (wrappedCoord.x < 0.5f)
                {
                    wrappedCoord.x = 1.0f - wrappedCoord.x;
                    wrappedCoord.y = resolutionWithBorder - wrappedCoord.y;
                }

                if (wrappedCoord.x > resolutionWithBorder - 0.5f)
                {
                    wrappedCoord.x = (2.0f * resolutionWithBorder - 1.0f) - wrappedCoord.x;
                    wrappedCoord.y = resolutionWithBorder - wrappedCoord.y;
                }

                if (wrappedCoord.y < 0.5f)
                {
                    wrappedCoord.y = 1.0f - wrappedCoord.y;
                    wrappedCoord.x = resolutionWithBorder - wrappedCoord.x;
                }

                if (wrappedCoord.y > resolutionWithBorder - 0.5f)
                {
                    wrappedCoord.y = (2.0f * resolutionWithBorder - 1.0f) - wrappedCoord.y;
                    wrappedCoord.x = resolutionWithBorder - wrappedCoord.x;
                }

                return clamp(wrappedCoord, 0.5f, resolutionWithBorder - 0.5f);
            }

            float2 BurtGIScreenProbeIrradianceOctWrapBorderCoord(float2 localCoord)
            {
                return BurtGIScreenProbeOctWrapBorderCoord(localCoord, BURT_GI_SCREEN_PROBE_IRRADIANCE_OCT_RESOLUTION_WITH_BORDER);
            }

            float2 BurtGIScreenProbeRadianceOctWrapBorderCoord(float2 localCoord)
            {
                return BurtGIScreenProbeOctWrapBorderCoord(localCoord, BURT_GI_SCREEN_PROBE_RADIANCE_OCT_RESOLUTION_WITH_BORDER);
            }

            float4 BurtGISampleScreenProbeIrradianceOctAtlasMip(float2 probeCoord, float2 localCoord, float2 atlasSize, float mipLevel);

            float4 BurtGISampleScreenProbeIrradianceOctAtlas(float2 probeCoord, float2 localCoord, float2 atlasSize)
            {
                return BurtGISampleScreenProbeIrradianceOctAtlasMip(probeCoord, localCoord, atlasSize, 0.0f);
            }

            float4 BurtGISampleScreenProbeIrradianceOctAtlasMip(float2 probeCoord, float2 localCoord, float2 atlasSize, float mipLevel)
            {
                float2 tileBase = probeCoord * BURT_GI_SCREEN_PROBE_IRRADIANCE_OCT_RESOLUTION_WITH_BORDER;
                float2 atlasTexelCoord = tileBase + BurtGIScreenProbeIrradianceOctWrapBorderCoord(localCoord);
                return _BurtGIScreenProbeIrradianceOctTexture.SampleLevel(
                    sampler_LinearClamp,
                    atlasTexelCoord / max(atlasSize, 1.0f),
                    clamp(mipLevel, 0.0f, 3.0f));
            }

            float4 BurtGIScreenProbeIrradianceOctWeightedAverage4(float4 sample00, float4 sample10, float4 sample01, float4 sample11)
            {
                float weight00 = saturate(sample00.a);
                float weight10 = saturate(sample10.a);
                float weight01 = saturate(sample01.a);
                float weight11 = saturate(sample11.a);
                float weightSum = weight00 + weight10 + weight01 + weight11;
                float4 average = (sample00 + sample10 + sample01 + sample11) * 0.25f;
                if (weightSum > 0.0001f)
                {
                    average.rgb =
                        (sample00.rgb * weight00 +
                            sample10.rgb * weight10 +
                            sample01.rgb * weight01 +
                            sample11.rgb * weight11) / weightSum;
                    average.a = saturate(weightSum * 0.25f);
                }

                average.rgb = max(average.rgb, 0.0f);
                average.a = saturate(average.a);
                return average;
            }

            float4 BurtGISampleScreenProbeIrradianceOctAtlasParent2x2(float2 probeCoord, float2 localCoord, float2 atlasSize)
            {
                float2 parentBase = floor(max(localCoord - 0.5f, 0.0f) * 0.5f) * 2.0f + 0.5f;
                float4 sample00 = BurtGISampleScreenProbeIrradianceOctAtlas(probeCoord, parentBase + float2(0.0f, 0.0f), atlasSize);
                float4 sample10 = BurtGISampleScreenProbeIrradianceOctAtlas(probeCoord, parentBase + float2(1.0f, 0.0f), atlasSize);
                float4 sample01 = BurtGISampleScreenProbeIrradianceOctAtlas(probeCoord, parentBase + float2(0.0f, 1.0f), atlasSize);
                float4 sample11 = BurtGISampleScreenProbeIrradianceOctAtlas(probeCoord, parentBase + float2(1.0f, 1.0f), atlasSize);
                return BurtGIScreenProbeIrradianceOctWeightedAverage4(sample00, sample10, sample01, sample11);
            }

            float4 BurtGISampleScreenProbeIrradianceOctAtlasParent4x4(float2 probeCoord, float2 localCoord, float2 atlasSize)
            {
                float2 parentBase = floor(max(localCoord - 0.5f, 0.0f) * 0.25f) * 4.0f + 0.5f;
                float4 sample00 = BurtGISampleScreenProbeIrradianceOctAtlasParent2x2(probeCoord, parentBase + float2(0.0f, 0.0f), atlasSize);
                float4 sample10 = BurtGISampleScreenProbeIrradianceOctAtlasParent2x2(probeCoord, parentBase + float2(2.0f, 0.0f), atlasSize);
                float4 sample01 = BurtGISampleScreenProbeIrradianceOctAtlasParent2x2(probeCoord, parentBase + float2(0.0f, 2.0f), atlasSize);
                float4 sample11 = BurtGISampleScreenProbeIrradianceOctAtlasParent2x2(probeCoord, parentBase + float2(2.0f, 2.0f), atlasSize);
                return BurtGIScreenProbeIrradianceOctWeightedAverage4(sample00, sample10, sample01, sample11);
            }

            float4 BurtGISampleScreenProbeIrradianceOctAtlasParent8x8(float2 probeCoord, float2 atlasSize)
            {
                float4 sample00 = BurtGISampleScreenProbeIrradianceOctAtlasParent4x4(probeCoord, float2(0.5f, 0.5f), atlasSize);
                float4 sample10 = BurtGISampleScreenProbeIrradianceOctAtlasParent4x4(probeCoord, float2(4.5f, 0.5f), atlasSize);
                float4 sample01 = BurtGISampleScreenProbeIrradianceOctAtlasParent4x4(probeCoord, float2(0.5f, 4.5f), atlasSize);
                float4 sample11 = BurtGISampleScreenProbeIrradianceOctAtlasParent4x4(probeCoord, float2(4.5f, 4.5f), atlasSize);
                return BurtGIScreenProbeIrradianceOctWeightedAverage4(sample00, sample10, sample01, sample11);
            }

            float4 BurtGISampleScreenProbeIrradianceOctAtlasDirectionalNeighborhood(float2 probeCoord, float2 localCoord, float2 atlasSize)
            {
                float4 center = BurtGISampleScreenProbeIrradianceOctAtlas(probeCoord, localCoord, atlasSize);
                float4 sampleRight = BurtGISampleScreenProbeIrradianceOctAtlas(probeCoord, localCoord + float2(1.0f, 0.0f), atlasSize);
                float4 sampleLeft = BurtGISampleScreenProbeIrradianceOctAtlas(probeCoord, localCoord + float2(-1.0f, 0.0f), atlasSize);
                float4 sampleUp = BurtGISampleScreenProbeIrradianceOctAtlas(probeCoord, localCoord + float2(0.0f, 1.0f), atlasSize);
                float4 sampleDown = BurtGISampleScreenProbeIrradianceOctAtlas(probeCoord, localCoord + float2(0.0f, -1.0f), atlasSize);
                float weightCenter = lerp(0.35f, 1.0f, saturate(center.a));
                float weightRight = saturate(sampleRight.a) * 0.55f;
                float weightLeft = saturate(sampleLeft.a) * 0.55f;
                float weightUp = saturate(sampleUp.a) * 0.55f;
                float weightDown = saturate(sampleDown.a) * 0.55f;
                float weightSum = weightCenter + weightRight + weightLeft + weightUp + weightDown;
                float4 neighborhood = (center + sampleRight + sampleLeft + sampleUp + sampleDown) * 0.2f;
                if (weightSum > 0.0001f)
                {
                    neighborhood.rgb =
                        (center.rgb * weightCenter +
                            sampleRight.rgb * weightRight +
                            sampleLeft.rgb * weightLeft +
                            sampleUp.rgb * weightUp +
                            sampleDown.rgb * weightDown) / weightSum;
                    neighborhood.a =
                        (center.a * weightCenter +
                            sampleRight.a * weightRight +
                            sampleLeft.a * weightLeft +
                            sampleUp.a * weightUp +
                            sampleDown.a * weightDown) / weightSum;
                }

                neighborhood.rgb = max(neighborhood.rgb, 0.0f);
                neighborhood.a = saturate(neighborhood.a);
                return neighborhood;
            }

            float4 BurtGISampleScreenProbeIrradianceOctAtlasMipChain(float2 probeCoord, float2 localCoord, float2 atlasSize, float requestedMipLevel)
            {
                float4 mainSample = 0.0f;
                mainSample = BurtGISampleScreenProbeIrradianceOctAtlas(probeCoord, localCoord, atlasSize);
                float4 directionalNeighborhood = BurtGISampleScreenProbeIrradianceOctAtlasDirectionalNeighborhood(probeCoord, localCoord, atlasSize);
                float directionalFill = saturate((0.42f - mainSample.a) * 2.3809524f) * saturate(directionalNeighborhood.a);
                mainSample.rgb = lerp(max(mainSample.rgb, 0.0f), max(directionalNeighborhood.rgb, 0.0f), directionalFill * 0.35f);
                mainSample.a = saturate(max(mainSample.a, directionalNeighborhood.a * directionalFill));

                float clampedMipLevel = clamp(requestedMipLevel, 0.0f, 3.0f);
                float4 mip1 = BurtGISampleScreenProbeIrradianceOctAtlasMip(probeCoord, localCoord, atlasSize, 1.0f);
                float4 mip2 = BurtGISampleScreenProbeIrradianceOctAtlasMip(probeCoord, localCoord, atlasSize, 2.0f);
                float4 mip3 = BurtGISampleScreenProbeIrradianceOctAtlasMip(probeCoord, localCoord, atlasSize, 3.0f);
                float4 requestedMipSample = mip1;
                if (clampedMipLevel < 1.0f)
                {
                    requestedMipSample = lerp(mainSample, mip1, clampedMipLevel);
                }
                else if (clampedMipLevel < 2.0f)
                {
                    requestedMipSample = lerp(mip1, mip2, clampedMipLevel - 1.0f);
                }
                else
                {
                    requestedMipSample = lerp(mip2, mip3, clampedMipLevel - 2.0f);
                }

                float requestedMipBlend = saturate(clampedMipLevel / 3.0f) * saturate(requestedMipSample.a);
                mainSample.rgb = lerp(mainSample.rgb, max(requestedMipSample.rgb, 0.0f), requestedMipBlend * 0.8f);
                mainSample.a = saturate(max(mainSample.a, requestedMipSample.a * requestedMipBlend));
                float4 fallbackMipSample = clampedMipLevel < 1.5f ? mip1 : (clampedMipLevel < 2.5f ? mip2 : mip3);
                float fallbackMipFill = saturate((0.62f - mainSample.a) * 1.6129032f) * saturate(fallbackMipSample.a);
                mainSample.rgb = lerp(mainSample.rgb, max(fallbackMipSample.rgb, 0.0f), fallbackMipFill * 0.42f);
                mainSample.a = saturate(max(mainSample.a, fallbackMipSample.a * fallbackMipFill));

                return mainSample;
            }

            float4 BurtGISampleScreenProbeIrradianceOctAtlasMipChain(float2 probeCoord, float2 localCoord, float2 atlasSize)
            {
                return BurtGISampleScreenProbeIrradianceOctAtlasMipChain(probeCoord, localCoord, atlasSize, 0.0f);
            }

            float4 BurtGISampleScreenProbeIrradianceOct(
                float2 screenUV,
                float3 positionWS,
                float sceneDepth,
                float3 normalWS,
                float requestedMipLevel,
                float receiverFoliageWeight)
            {
                float2 gridSize = max(_BurtGIScreenProbeGridParams.xy, 1.0f);
                float2 atlasSize = float2(
                    gridSize.x * BURT_GI_SCREEN_PROBE_IRRADIANCE_OCT_RESOLUTION_WITH_BORDER,
                    max(gridSize.y * BURT_GI_SCREEN_PROBE_IRRADIANCE_OCT_RESOLUTION_WITH_BORDER, _BurtGIScreenProbeTraceParams.z));
                float2 octUV = BurtGIInverseEquiAreaSphericalMapping(normalWS);
                float2 localCoord = octUV * BURT_GI_SCREEN_PROBE_IRRADIANCE_OCT_RESOLUTION + 1.0f;
                float2 fallbackProbeCoord = BurtGIScreenProbeFallbackCoord(screenUV, gridSize);
                float4 result = BurtGISampleScreenProbeIrradianceOctAtlasMipChain(fallbackProbeCoord, localCoord, atlasSize, requestedMipLevel);

                if (gridSize.x >= 2.0f && gridSize.y >= 2.0f)
                {
                    float spacingPixels = max(_BurtGIScreenProbeParams.x, 1.0f);
                    float2 screenCoord = BurtGIScreenProbeUpsampleFullResCoord(screenUV, spacingPixels);
                    float2 tile00 = min(floor(screenCoord / spacingPixels), gridSize - 2.0f);
                    float2 bilinearWeights = saturate((screenCoord - tile00 * spacingPixels + 1.0f) / (spacingPixels + 2.0f));
                    float4 interpolationWeights = float4(
                        (1.0f - bilinearWeights.y) * (1.0f - bilinearWeights.x),
                        (1.0f - bilinearWeights.y) * bilinearWeights.x,
                        bilinearWeights.y * (1.0f - bilinearWeights.x),
                        bilinearWeights.y * bilinearWeights.x);
                    float3 normalizedNormalWS = normalize(normalWS);
                    float4 scenePlane = float4(normalizedNormalWS, dot(positionWS, normalizedNormalWS));
                    float4 irradianceSum = 0.0f;
                    float weightSum = 0.0f;

                    [unroll]
                    for (int cornerIndex = 0; cornerIndex < 4; ++cornerIndex)
                    {
                        float2 cornerOffset = float2((float)(cornerIndex & 1), (float)(cornerIndex >> 1));
                        float2 probeCoord = min(tile00 + cornerOffset, gridSize - 1.0f);
                        float2 probeUV = (probeCoord + 0.5f) * _BurtGIScreenProbeGridParams.zw;
                        float4 probePosition = tex2D(_BurtGIScreenProbeWorldPositionTexture, probeUV);
                        float4 probeNormal = tex2D(_BurtGIScreenProbeWorldNormalTexture, probeUV);
                        float validProbe = (probePosition.a > 0.5f && probeNormal.a > 0.5f) ? 1.0f : 0.0f;
                        float planeDistance = abs(dot(float4(probePosition.xyz, -1.0f), scenePlane));
                        float receiverPlaneWeightScale = lerp(1.0f, 0.25f, saturate(receiverFoliageWeight));
                        float planeWeight = exp2(-200.0f * receiverPlaneWeightScale * planeDistance / max(sceneDepth, 0.001f));
                        float3 probeNormalWS = normalize(probeNormal.xyz * 2.0f - 1.0f);
                        float normalWeight = lerp(
                            lerp(0.35f, 1.0f, pow(saturate(dot(normalizedNormalWS, probeNormalWS)), 4.0f)),
                            1.0f,
                            saturate(receiverFoliageWeight));
                        float4 octSample = BurtGISampleScreenProbeIrradianceOctAtlasMipChain(probeCoord, localCoord, atlasSize, requestedMipLevel);
                        float4 probeHitDistance = tex2D(_BurtGIScreenProbeHitDistanceTexture, probeUV);
                        float probeTraceConfidence = saturate(max(probeHitDistance.b, probeHitDistance.a));
                        octSample.a = saturate(max(octSample.a, probeTraceConfidence * 0.65f));
                        float octSampleConfidence = saturate(octSample.a);
                        float weight = interpolationWeights[cornerIndex] * planeWeight * normalWeight * validProbe * lerp(0.18f, 1.0f, octSampleConfidence);
                        irradianceSum += octSample * weight;
                        weightSum += weight;
                    }

                    if (weightSum > 0.0001f)
                    {
                        result = irradianceSum / weightSum;
                    }
                }

                result.rgb = max(result.rgb, 0.0f);
                result.a = saturate(result.a);
                return result;
            }

            float4 BurtGISampleScreenProbeIrradianceOct(
                float2 screenUV,
                float3 positionWS,
                float sceneDepth,
                float3 normalWS,
                float requestedMipLevel)
            {
                return BurtGISampleScreenProbeIrradianceOct(screenUV, positionWS, sceneDepth, normalWS, requestedMipLevel, 0.0f);
            }

            float4 BurtGISampleScreenProbeIrradianceOct(
                float2 screenUV,
                float3 positionWS,
                float sceneDepth,
                float3 normalWS)
            {
                return BurtGISampleScreenProbeIrradianceOct(screenUV, positionWS, sceneDepth, normalWS, 0.0f, 0.0f);
            }

            float4 BurtGISampleScreenProbeRadianceOctAtlasMip(float2 probeCoord, float2 localCoord, float2 atlasSize, float mipLevel)
            {
                float2 tileBase = probeCoord * BURT_GI_SCREEN_PROBE_RADIANCE_OCT_RESOLUTION_WITH_BORDER;
                float2 atlasTexelCoord = tileBase + BurtGIScreenProbeRadianceOctWrapBorderCoord(localCoord);
                return _BurtGIScreenProbeRadianceOctTexture.SampleLevel(
                    sampler_LinearClamp,
                    atlasTexelCoord / max(atlasSize, 1.0f),
                    clamp(mipLevel, 0.0f, 3.0f));
            }

            float4 BurtGISampleScreenProbeRadianceOct(
                float2 screenUV,
                float3 positionWS,
                float sceneDepth,
                float3 planeNormalWS,
                float3 directionWS,
                float requestedMipLevel,
                float receiverFoliageWeight)
            {
                float2 gridSize = max(_BurtGIScreenProbeGridParams.xy, 1.0f);
                float traceResolution = max(_BurtGIScreenProbeTraceParams.x, 1.0f);
                float atlasProbeHeight = max(
                    gridSize.y,
                    ceil(max(_BurtGIScreenProbeTraceParams.z, traceResolution) / traceResolution));
                float2 atlasSize = float2(
                    gridSize.x * BURT_GI_SCREEN_PROBE_RADIANCE_OCT_RESOLUTION_WITH_BORDER,
                    atlasProbeHeight * BURT_GI_SCREEN_PROBE_RADIANCE_OCT_RESOLUTION_WITH_BORDER);
                float2 localCoord = BurtGIInverseEquiAreaSphericalMapping(directionWS) * BURT_GI_SCREEN_PROBE_RADIANCE_OCT_RESOLUTION + 1.0f;
                float clampedMipLevel = clamp(requestedMipLevel, 0.0f, 3.0f);

                if (gridSize.x < 2.0f || gridSize.y < 2.0f)
                {
                    float2 fallbackProbeCoord = BurtGIScreenProbeFallbackCoord(screenUV, gridSize);
                    return BurtGISampleScreenProbeRadianceOctAtlasMip(fallbackProbeCoord, localCoord, atlasSize, clampedMipLevel);
                }

                float spacingPixels = max(_BurtGIScreenProbeParams.x, 1.0f);
                float2 screenCoord = BurtGIScreenProbeUpsampleFullResCoord(screenUV, spacingPixels);
                float2 tile00 = min(floor(screenCoord / spacingPixels), gridSize - 2.0f);
                float2 bilinearWeights = saturate((screenCoord - tile00 * spacingPixels + 1.0f) / (spacingPixels + 2.0f));
                float4 interpolationWeights = float4(
                    (1.0f - bilinearWeights.y) * (1.0f - bilinearWeights.x),
                    (1.0f - bilinearWeights.y) * bilinearWeights.x,
                    bilinearWeights.y * (1.0f - bilinearWeights.x),
                    bilinearWeights.y * bilinearWeights.x);
                float3 normalizedPlaneNormalWS = normalize(planeNormalWS);
                float4 scenePlane = float4(normalizedPlaneNormalWS, dot(positionWS, normalizedPlaneNormalWS));
                float4 radianceSum = 0.0f;
                float weightSum = 0.0f;

                [unroll]
                for (int cornerIndex = 0; cornerIndex < 4; ++cornerIndex)
                {
                    float2 cornerOffset = float2((float)(cornerIndex & 1), (float)(cornerIndex >> 1));
                    float2 probeCoord = min(tile00 + cornerOffset, gridSize - 1.0f);
                    float2 probeUV = (probeCoord + 0.5f) * _BurtGIScreenProbeGridParams.zw;
                    float4 probePosition = tex2D(_BurtGIScreenProbeWorldPositionTexture, probeUV);
                    float4 probeNormal = tex2D(_BurtGIScreenProbeWorldNormalTexture, probeUV);
                    float validProbe = (probePosition.a > 0.5f && probeNormal.a > 0.5f) ? 1.0f : 0.0f;
                    float planeDistance = abs(dot(float4(probePosition.xyz, -1.0f), scenePlane));
                    float receiverPlaneWeightScale = lerp(1.0f, 0.25f, saturate(receiverFoliageWeight));
                    float planeWeight = exp2(-160.0f * receiverPlaneWeightScale * planeDistance / max(sceneDepth, 0.001f));
                    float4 radianceSample = BurtGISampleScreenProbeRadianceOctAtlasMip(probeCoord, localCoord, atlasSize, clampedMipLevel);
                    float sampleConfidence = saturate(radianceSample.a);
                    float weight = interpolationWeights[cornerIndex] * planeWeight * validProbe * lerp(0.12f, 1.0f, sampleConfidence);
                    radianceSum += float4(BurtGITonemapLighting(max(radianceSample.rgb, 0.0f)), sampleConfidence) * weight;
                    weightSum += weight;
                }

                if (weightSum <= 0.0001f)
                {
                    float2 fallbackProbeCoord = BurtGIScreenProbeFallbackCoord(screenUV, gridSize);
                    return BurtGISampleScreenProbeRadianceOctAtlasMip(fallbackProbeCoord, localCoord, atlasSize, clampedMipLevel);
                }

                float4 radiance = radianceSum / weightSum;
                radiance.rgb = max(BurtGIInverseTonemapLighting(radiance.rgb), 0.0f);
                radiance.a = saturate(radiance.a);
                return radiance;
            }

            float4 BurtGISampleScreenProbeRadianceOct(
                float2 screenUV,
                float3 positionWS,
                float sceneDepth,
                float3 planeNormalWS,
                float3 directionWS,
                float requestedMipLevel)
            {
                return BurtGISampleScreenProbeRadianceOct(screenUV, positionWS, sceneDepth, planeNormalWS, directionWS, requestedMipLevel, 0.0f);
            }

            bool BurtGISampleAdaptiveScreenProbeRadianceOct(
                float2 screenUV,
                float3 positionWS,
                float sceneDepth,
                float3 planeNormalWS,
                float3 directionWS,
                float requestedMipLevel,
                float currentConfidence,
                float edgeConfidence,
                float receiverFoliageWeight,
                out float3 adaptiveRadiance,
                out float adaptiveConfidence)
            {
                adaptiveRadiance = 0.0f;
                adaptiveConfidence = 0.0f;
                float3 ignoredIrradiance;
                float selectedAdaptiveConfidence;
                float2 atlasProbeCoord;
                if (!BurtGISampleAdaptiveScreenProbeTraceAtlas(
                    screenUV,
                    positionWS,
                    sceneDepth,
                    planeNormalWS,
                    currentConfidence,
                    edgeConfidence,
                    receiverFoliageWeight,
                    ignoredIrradiance,
                    selectedAdaptiveConfidence,
                    atlasProbeCoord))
                {
                    return false;
                }

                float2 gridSize = max(_BurtGIScreenProbeGridParams.xy, 1.0f);
                float traceResolution = max(_BurtGIScreenProbeTraceParams.x, 1.0f);
                float atlasProbeHeight = max(
                    gridSize.y,
                    ceil(max(_BurtGIScreenProbeTraceParams.z, traceResolution) / traceResolution));
                float2 atlasSize = float2(gridSize.x, atlasProbeHeight) * BURT_GI_SCREEN_PROBE_RADIANCE_OCT_RESOLUTION_WITH_BORDER;
                float2 localCoord = BurtGIInverseEquiAreaSphericalMapping(directionWS) * BURT_GI_SCREEN_PROBE_RADIANCE_OCT_RESOLUTION + 1.0f;
                float4 radianceSample = BurtGISampleScreenProbeRadianceOctAtlasMip(
                    atlasProbeCoord,
                    localCoord,
                    atlasSize,
                    requestedMipLevel);
                adaptiveRadiance = max(radianceSample.rgb, 0.0f);
                adaptiveConfidence = saturate(max(selectedAdaptiveConfidence * 0.75f, radianceSample.a * selectedAdaptiveConfidence));
                return adaptiveConfidence > 0.0001f;
            }

            void BurtGISampleUniformScreenProbeCorner(
                float2 tile00,
                int cornerIndex,
                float4 interpolationWeights,
                float4 scenePlane,
                float sceneDepth,
                float3 normalWS,
                float receiverFoliageWeight,
                float2 gridSize,
                out float4 cornerIrradiance,
                out float4 cornerConfidence,
                out float cornerWeight)
            {
                float2 cornerOffset = float2((float)(cornerIndex & 1), (float)(cornerIndex >> 1));
                float2 probeCoord = min(tile00 + cornerOffset, gridSize - 1.0f);
                float2 probeUV = (probeCoord + 0.5f) * _BurtGIScreenProbeGridParams.zw;
                float4 probePosition = tex2D(_BurtGIScreenProbeWorldPositionTexture, probeUV);
                float4 probeNormal = tex2D(_BurtGIScreenProbeWorldNormalTexture, probeUV);
                float validProbe = probePosition.a > 0.5f && probeNormal.a > 0.5f ? 1.0f : 0.0f;
                float planeDistance = abs(dot(float4(probePosition.xyz, -1.0f), scenePlane));
                float receiverPlaneWeightScale = lerp(1.0f, 0.25f, saturate(receiverFoliageWeight));
                float planeWeight = exp2(-200.0f * receiverPlaneWeightScale * planeDistance / max(sceneDepth, 0.001f));
                float3 probeNormalWS = normalize(probeNormal.xyz * 2.0f - 1.0f);
                float normalWeight = lerp(
                    lerp(0.35f, 1.0f, pow(saturate(dot(normalWS, probeNormalWS)), 4.0f)),
                    1.0f,
                    saturate(receiverFoliageWeight));
                cornerWeight = interpolationWeights[cornerIndex] * planeWeight * normalWeight * validProbe;
                cornerIrradiance = tex2D(_BurtGIScreenProbeFixupIrradianceTexture, probeUV);
                cornerConfidence = tex2D(_BurtGIScreenProbeFixupConfidenceTexture, probeUV);
            }

            void BurtGISampleUniformScreenProbePlaneWeighted(
                float2 screenUV,
                float3 positionWS,
                float sceneDepth,
                float3 normalWS,
                float receiverFoliageWeight,
                out float4 probeIrradiance,
                out float4 probeConfidence,
                out float sampleValidity)
            {
                probeIrradiance = 0.0f;
                probeConfidence = 0.0f;
                sampleValidity = 0.0f;
                float2 gridSize = max(_BurtGIScreenProbeGridParams.xy, 1.0f);
                if (gridSize.x < 2.0f || gridSize.y < 2.0f)
                {
                    return;
                }

                float spacingPixels = max(_BurtGIScreenProbeParams.x, 1.0f);
                float2 screenCoord = BurtGIScreenProbeUpsampleFullResCoord(screenUV, spacingPixels);
                float2 tile00 = min(floor(screenCoord / spacingPixels), gridSize - 2.0f);
                float2 bilinearWeights = saturate((screenCoord - tile00 * spacingPixels + 1.0f) / (spacingPixels + 2.0f));
                float4 interpolationWeights = float4(
                    (1.0f - bilinearWeights.y) * (1.0f - bilinearWeights.x),
                    (1.0f - bilinearWeights.y) * bilinearWeights.x,
                    bilinearWeights.y * (1.0f - bilinearWeights.x),
                    bilinearWeights.y * bilinearWeights.x);
                float3 unitNormalWS = normalize(normalWS);
                float4 scenePlane = float4(unitNormalWS, dot(positionWS, unitNormalWS));
                float4 cornerIrradiance0;
                float4 cornerIrradiance1;
                float4 cornerIrradiance2;
                float4 cornerIrradiance3;
                float4 cornerConfidence0;
                float4 cornerConfidence1;
                float4 cornerConfidence2;
                float4 cornerConfidence3;
                float cornerWeight0;
                float cornerWeight1;
                float cornerWeight2;
                float cornerWeight3;

                BurtGISampleUniformScreenProbeCorner(tile00, 0, interpolationWeights, scenePlane, sceneDepth, unitNormalWS, receiverFoliageWeight, gridSize, cornerIrradiance0, cornerConfidence0, cornerWeight0);
                BurtGISampleUniformScreenProbeCorner(tile00, 1, interpolationWeights, scenePlane, sceneDepth, unitNormalWS, receiverFoliageWeight, gridSize, cornerIrradiance1, cornerConfidence1, cornerWeight1);
                BurtGISampleUniformScreenProbeCorner(tile00, 2, interpolationWeights, scenePlane, sceneDepth, unitNormalWS, receiverFoliageWeight, gridSize, cornerIrradiance2, cornerConfidence2, cornerWeight2);
                BurtGISampleUniformScreenProbeCorner(tile00, 3, interpolationWeights, scenePlane, sceneDepth, unitNormalWS, receiverFoliageWeight, gridSize, cornerIrradiance3, cornerConfidence3, cornerWeight3);

                float weightSum = cornerWeight0 + cornerWeight1 + cornerWeight2 + cornerWeight3;

                if (weightSum <= 0.0001f)
                {
                    return;
                }

                float4 weightedIrradiance =
                    (cornerIrradiance0 * cornerWeight0 +
                     cornerIrradiance1 * cornerWeight1 +
                     cornerIrradiance2 * cornerWeight2 +
                     cornerIrradiance3 * cornerWeight3) / weightSum;
                float4 weightedConfidence =
                    (cornerConfidence0 * cornerWeight0 +
                     cornerConfidence1 * cornerWeight1 +
                     cornerConfidence2 * cornerWeight2 +
                     cornerConfidence3 * cornerWeight3) / weightSum;

                float randomValue = min(BurtGIHash12(screenCoord + 31.7f, _BurtGIParams2.x + 19.0f), 0.99f) * weightSum;
                float4 stochasticIrradiance = cornerIrradiance0;
                float4 stochasticConfidence = cornerConfidence0;
                if (randomValue >= cornerWeight0 + cornerWeight1 + cornerWeight2)
                {
                    stochasticIrradiance = cornerIrradiance3;
                    stochasticConfidence = cornerConfidence3;
                }
                else if (randomValue >= cornerWeight0 + cornerWeight1)
                {
                    stochasticIrradiance = cornerIrradiance2;
                    stochasticConfidence = cornerConfidence2;
                }
                else if (randomValue >= cornerWeight0)
                {
                    stochasticIrradiance = cornerIrradiance1;
                    stochasticConfidence = cornerConfidence1;
                }

                float stochasticProbeInterpolation = _BurtGIScreenProbeTemporalParams.w > 0.5f ? 1.0f : 0.0f;
                probeIrradiance = lerp(weightedIrradiance, stochasticIrradiance, stochasticProbeInterpolation);
                probeConfidence = lerp(weightedConfidence, stochasticConfidence, stochasticProbeInterpolation);
                probeIrradiance.rgb = max(probeIrradiance.rgb, 0.0f);
                probeIrradiance.a = saturate(probeIrradiance.a);
                probeConfidence.rgb = saturate(probeConfidence.rgb);
                sampleValidity = 1.0f;
            }

            void BurtGISampleUniformScreenProbePlaneWeighted(
                float2 screenUV,
                float3 positionWS,
                float sceneDepth,
                float3 normalWS,
                out float4 probeIrradiance,
                out float4 probeConfidence,
                out float sampleValidity)
            {
                BurtGISampleUniformScreenProbePlaneWeighted(
                    screenUV,
                    positionWS,
                    sceneDepth,
                    normalWS,
                    0.0f,
                    probeIrradiance,
                    probeConfidence,
                    sampleValidity);
            }

            #define BURT_GI_RADIANCE_CACHE_INVALID_PROBE_INDEX 0xffffu
            #define BURT_GI_RADIANCE_CACHE_USED_PROBE_INDEX 0xfffeu
            #define BURT_GI_RADIANCE_CACHE_MAX_CLIPMAPS 6u

            uint BurtGIRadianceCacheClipmapCount()
            {
                return min((uint)max(_BurtGIRadianceCacheClipMapParams.z, 0.0f), BURT_GI_RADIANCE_CACHE_MAX_CLIPMAPS);
            }

            uint BurtGIRadianceCacheClipmapResolution()
            {
                return max(1u, (uint)max(_BurtGIRadianceCacheClipMapParams.w, 1.0f));
            }

            uint BurtGIRadianceCacheAtlasResolutionInProbes()
            {
                return max(1u, (uint)max(_BurtGIRadianceCacheClipMapParams.x, 1.0f));
            }

            uint BurtGIRadianceCacheProbeResolution()
            {
                return max(1u, (uint)max(_BurtGIRadianceCacheClipMapParams.y, 1.0f));
            }

            uint BurtGIRadianceCacheFinalProbeResolution()
            {
                return max(BurtGIRadianceCacheProbeResolution(), (uint)max(_BurtGIRadianceCacheClipMapWorldParams.w, 1.0f));
            }

            uint BurtGIRadianceCacheFinalIrradianceProbeResolution()
            {
                return max(1u, _BurtGIRadianceCacheClipMapFinalIrradianceProbeResolution);
            }

            uint BurtGIRadianceCacheFinalAtlasBorderSize()
            {
                return 1u << min(_BurtGIRadianceCacheClipMapFinalRadianceAtlasMaxMip, 8u);
            }

            bool BurtGIRadianceCacheIsAvailable()
            {
                return _BurtGIRadianceCacheClipMapApplyParams.x > 0.5f &&
                    _BurtGIRadianceCacheClipMapParams.x > 0.5f &&
                    _BurtGIRadianceCacheClipMapParams.y > 0.5f &&
                    _BurtGIRadianceCacheClipMapParams.z > 0.5f &&
                    _BurtGIRadianceCacheClipMapParams.w > 0.5f &&
                    _BurtGIRadianceCacheClipMapIndirectionSize.x > 0.5f &&
                    _BurtGIRadianceCacheClipMapIndirectionSize.y > 0.5f;
            }

            bool BurtGIRadianceCacheWorldPositionToProbeCoordDithered(float3 worldPosition, float clipmapDitherRandom, out uint clipmapIndex, out float3 probeCoordFloat)
            {
                clipmapIndex = 0u;
                probeCoordFloat = 0.0f;
                uint clipmapCount = BurtGIRadianceCacheClipmapCount();
                float clipmapResolution = max(_BurtGIRadianceCacheClipMapParams.w, 1.0f);
                float invFadeSize = max(_BurtGIRadianceCacheClipMapWorldParams.z, 0.0001f);

                [unroll]
                for (uint currentClipmapIndex = 0u; currentClipmapIndex < BURT_GI_RADIANCE_CACHE_MAX_CLIPMAPS; ++currentClipmapIndex)
                {
                    if (currentClipmapIndex >= clipmapCount)
                    {
                        continue;
                    }

                    float4 worldPositionToProbeCoord = _BurtGIRadianceCacheClipMapWorldPositionToProbeCoord[currentClipmapIndex];
                    float3 currentProbeCoordFloat = worldPosition * worldPositionToProbeCoord.w + worldPositionToProbeCoord.xyz;
                    float3 bottomEdgeFade = saturate((currentProbeCoordFloat - 0.5f) * invFadeSize);
                    float3 topEdgeFade = saturate((clipmapResolution - 0.5f - currentProbeCoordFloat) * invFadeSize);
                    float edgeFade = min(
                        min(bottomEdgeFade.x, min(bottomEdgeFade.y, bottomEdgeFade.z)),
                        min(topEdgeFade.x, min(topEdgeFade.y, topEdgeFade.z)));

                    if (edgeFade > clipmapDitherRandom)
                    {
                        clipmapIndex = currentClipmapIndex;
                        probeCoordFloat = currentProbeCoordFloat;
                        return true;
                    }
                }

                return false;
            }

            bool BurtGIRadianceCacheWorldPositionToProbeCoord(float3 worldPosition, out uint clipmapIndex, out float3 probeCoordFloat)
            {
                return BurtGIRadianceCacheWorldPositionToProbeCoordDithered(worldPosition, 0.0001f, clipmapIndex, probeCoordFloat);
            }

            uint BurtGIRadianceCacheProbeIndexFromIndirection(uint clipmapIndex, uint3 probeCoord)
            {
                uint clipmapResolution = BurtGIRadianceCacheClipmapResolution();
                uint2 indirectionSize = max((uint2)_BurtGIRadianceCacheClipMapIndirectionSize.xy, uint2(1u, 1u));
                uint2 indirectionCoord = uint2(
                    clipmapIndex * clipmapResolution + probeCoord.x,
                    probeCoord.y + probeCoord.z * clipmapResolution);

                if (indirectionCoord.x >= indirectionSize.x || indirectionCoord.y >= indirectionSize.y)
                {
                    return BURT_GI_RADIANCE_CACHE_INVALID_PROBE_INDEX;
                }

                return _BurtGIRadianceCacheClipMapIndirectionTexture.Load(int3((int2)indirectionCoord, 0));
            }

            float2 BurtGIRayIntersectSphere(float3 rayOrigin, float3 rayDirection, float4 sphere)
            {
                float3 localPosition = rayOrigin - sphere.xyz;
                float a = max(dot(rayDirection, rayDirection), 0.000001f);
                float b = 2.0f * dot(rayDirection, localPosition);
                float c = dot(localPosition, localPosition) - sphere.w * sphere.w;
                float discriminant = b * b - 4.0f * a * c;
                if (discriminant < 0.0f)
                {
                    return -1.0f;
                }

                float sqrtDiscriminant = sqrt(discriminant);
                return (-b + float2(-sqrtDiscriminant, sqrtDiscriminant)) / (2.0f * a);
            }

            float3 BurtGIRadianceCacheProbeWorldPosition(uint clipmapIndex, uint3 probeCoord, uint probeIndex)
            {
                float4 probeCoordToWorld = _BurtGIRadianceCacheClipMapProbeCoordToWorldCenter[clipmapIndex];
                float3 probeWorldPosition = probeCoordToWorld.xyz + (float3)probeCoord * probeCoordToWorld.w;
                uint atlasResolutionInProbes = BurtGIRadianceCacheAtlasResolutionInProbes();
                uint maxProbeCount = atlasResolutionInProbes * atlasResolutionInProbes;
                if (probeIndex < maxProbeCount)
                {
                    probeWorldPosition += _BurtGIRadianceCacheClipMapProbeWorldOffsetBuffer[probeIndex].xyz;
                }

                return probeWorldPosition;
            }

            float BurtGIRadianceCacheFinalAtlasMaxMip()
            {
                return (float)_BurtGIRadianceCacheClipMapFinalRadianceAtlasMaxMip;
            }

            float BurtGISampleRadianceCacheProbeOcclusion(uint probeIndex, float3 probeToSampleWS, float sampleDistance)
            {
                uint atlasResolutionInProbes = BurtGIRadianceCacheAtlasResolutionInProbes();
                uint maxProbeCount = atlasResolutionInProbes * atlasResolutionInProbes;
                if (probeIndex >= maxProbeCount)
                {
                    return 0.0f;
                }

                uint finalOcclusionProbeResolution = max(1u, _BurtGIRadianceCacheClipMapFinalOcclusionProbeResolution);
                uint borderSize = BurtGIRadianceCacheFinalAtlasBorderSize();
                uint occlusionProbeResolution = max(1u, finalOcclusionProbeResolution > 2u * borderSize ? finalOcclusionProbeResolution - 2u * borderSize : 1u);
                float2 probeUV = BurtGIInverseEquiAreaSphericalMapping(BurtSafeNormalize(probeToSampleWS));
                float2 probeTexelCoord = probeUV * (float)occlusionProbeResolution + (float)borderSize;
                float2 probeAtlasBaseCoord = (float2)(uint2(probeIndex % atlasResolutionInProbes, probeIndex / atlasResolutionInProbes) * finalOcclusionProbeResolution);
                float2 occlusionAtlasResolution = (float)(atlasResolutionInProbes * finalOcclusionProbeResolution);
                float2 moments = _BurtGIRadianceCacheClipMapProbeOcclusionAtlasTexture.SampleLevel(
                    sampler_LinearClamp,
                    (probeAtlasBaseCoord + probeTexelCoord) / max(occlusionAtlasResolution, float2(1.0f, 1.0f)),
                    0.0f);

                float meanDepth = max(moments.x, 0.0f);
                if (meanDepth <= 0.0001f || sampleDistance <= meanDepth)
                {
                    return 1.0f;
                }

                float variance = max(abs(moments.y - meanDepth * meanDepth), 0.0001f);
                float distanceDelta = sampleDistance - meanDepth;
                float visibility = variance / (variance + distanceDelta * distanceDelta);
                return saturate(visibility * visibility * visibility);
            }

            float BurtGIRadianceCacheApplyProbeOcclusionWeightThreshold(float weight)
            {
                const float weightThreshold = 0.2f;
                weight = max(weight, 0.0f);
                if (weight < weightThreshold)
                {
                    return weight * weight * weight / (weightThreshold * weightThreshold);
                }

                return weight;
            }

            float3 BurtGISampleRadianceCacheProbe(uint probeIndex, float3 sampleDirectionWS, float mipLevel)
            {
                uint atlasResolutionInProbes = BurtGIRadianceCacheAtlasResolutionInProbes();
                uint maxProbeCount = atlasResolutionInProbes * atlasResolutionInProbes;
                if (probeIndex >= maxProbeCount)
                {
                    return 0.0f;
                }

                uint radianceProbeResolution = BurtGIRadianceCacheProbeResolution();
                uint finalProbeResolution = BurtGIRadianceCacheFinalProbeResolution();
                uint borderSize = (finalProbeResolution - radianceProbeResolution) / 2u;
                float2 probeUV = BurtGIInverseEquiAreaSphericalMapping(sampleDirectionWS);
                float2 probeTexelCoord = probeUV * (float)radianceProbeResolution + (float)borderSize;
                float2 probeAtlasBaseCoord = (float2)(uint2(probeIndex % atlasResolutionInProbes, probeIndex / atlasResolutionInProbes) * finalProbeResolution);
                float2 finalAtlasResolution = (float)(atlasResolutionInProbes * finalProbeResolution);
                float2 probeAtlasUV = (probeAtlasBaseCoord + probeTexelCoord) / max(finalAtlasResolution, float2(1.0f, 1.0f));
                return max(_BurtGIRadianceCacheClipMapFinalRadianceAtlasTexture.SampleLevel(sampler_LinearClamp, probeAtlasUV, mipLevel).rgb, 0.0f);
            }

            float3 BurtGISampleRadianceCacheProbeIrradiance(uint probeIndex, float3 sampleDirectionWS)
            {
                uint atlasResolutionInProbes = BurtGIRadianceCacheAtlasResolutionInProbes();
                uint maxProbeCount = atlasResolutionInProbes * atlasResolutionInProbes;
                if (probeIndex >= maxProbeCount)
                {
                    return 0.0f;
                }

                uint finalIrradianceProbeResolution = BurtGIRadianceCacheFinalIrradianceProbeResolution();
                uint borderSize = BurtGIRadianceCacheFinalAtlasBorderSize();
                uint irradianceProbeResolution = max(1u, finalIrradianceProbeResolution > 2u * borderSize ? finalIrradianceProbeResolution - 2u * borderSize : 1u);
                float2 probeUV = BurtGIInverseEquiAreaSphericalMapping(sampleDirectionWS);
                float2 probeTexelCoord = probeUV * (float)irradianceProbeResolution + (float)borderSize;
                float2 probeAtlasBaseCoord = (float2)(uint2(probeIndex % atlasResolutionInProbes, probeIndex / atlasResolutionInProbes) * finalIrradianceProbeResolution);
                float2 finalAtlasResolution = (float)(atlasResolutionInProbes * finalIrradianceProbeResolution);
                float2 probeAtlasUV = (probeAtlasBaseCoord + probeTexelCoord) / max(finalAtlasResolution, float2(1.0f, 1.0f));
                return max(_BurtGIRadianceCacheClipMapFinalIrradianceAtlasTexture.SampleLevel(sampler_LinearClamp, probeAtlasUV, 0.0f).rgb, 0.0f);
            }

            float BurtGISampleRadianceCacheProbeSkyAO(uint probeIndex, float3 sampleDirectionWS)
            {
                uint atlasResolutionInProbes = BurtGIRadianceCacheAtlasResolutionInProbes();
                uint maxProbeCount = atlasResolutionInProbes * atlasResolutionInProbes;
                if (probeIndex >= maxProbeCount)
                {
                    return 0.0f;
                }

                uint finalIrradianceProbeResolution = BurtGIRadianceCacheFinalIrradianceProbeResolution();
                uint borderSize = BurtGIRadianceCacheFinalAtlasBorderSize();
                uint irradianceProbeResolution = max(1u, finalIrradianceProbeResolution > 2u * borderSize ? finalIrradianceProbeResolution - 2u * borderSize : 1u);
                float2 probeUV = BurtGIInverseEquiAreaSphericalMapping(sampleDirectionWS);
                float2 probeTexelCoord = probeUV * (float)irradianceProbeResolution + (float)borderSize;
                float2 probeAtlasBaseCoord = (float2)(uint2(probeIndex % atlasResolutionInProbes, probeIndex / atlasResolutionInProbes) * finalIrradianceProbeResolution);
                float2 finalAtlasResolution = (float)(atlasResolutionInProbes * finalIrradianceProbeResolution);
                float2 probeAtlasUV = (probeAtlasBaseCoord + probeTexelCoord) / max(finalAtlasResolution, float2(1.0f, 1.0f));
                return saturate(_BurtGIRadianceCacheClipMapProbeSkyAOAtlasTexture.SampleLevel(sampler_LinearClamp, probeAtlasUV, 0.0f));
            }

            float3 BurtGISampleRadianceCacheProbeWithParallaxCorrection(
                uint3 probeCoord,
                uint clipmapIndex,
                uint probeIndex,
                float3 worldPosition,
                float3 sampleDirectionWS,
                float mipLevel)
            {
                float3 unitSampleDirectionWS = BurtSafeNormalize(sampleDirectionWS);
                float probeTMin = max(_BurtGIRadianceCacheClipMapClipmapParams[clipmapIndex].y, 0.001f);
                float reprojectionRadiusScale = _BurtGIRadianceCacheClipMapApplyParams.y > 0.0f ? _BurtGIRadianceCacheClipMapApplyParams.y : 1.5f;
                float reprojectionRadius = reprojectionRadiusScale * probeTMin;
                float3 probeWorldPosition = BurtGIRadianceCacheProbeWorldPosition(clipmapIndex, probeCoord, probeIndex);
                float2 intersections = BurtGIRayIntersectSphere(worldPosition, unitSampleDirectionWS, float4(probeWorldPosition, reprojectionRadius));

                if (intersections.y > 0.0001f)
                {
                    float3 intersectionPosition = worldPosition + unitSampleDirectionWS * intersections.y;
                    float3 reprojectedDirection = intersectionPosition - probeWorldPosition;
                    float correctionDenominator = reprojectionRadius * dot(reprojectedDirection, unitSampleDirectionWS);
                    if (abs(correctionDenominator) > 0.0001f)
                    {
                        float correctionFactor = max(intersections.y * intersections.y / correctionDenominator, 0.0f);
                        return BurtGISampleRadianceCacheProbe(probeIndex, reprojectedDirection, mipLevel) * correctionFactor;
                    }
                }

                return BurtGISampleRadianceCacheProbe(probeIndex, unitSampleDirectionWS, mipLevel);
            }

            bool BurtGISampleRadianceCacheClipMapInterpolated(
                float3 worldPosition,
                float3 sampleDirectionWS,
                float coneHalfAngle,
                float clipmapDitherRandom,
                out float3 radiance,
                out float confidence)
            {
                radiance = 0.0f;
                confidence = 0.0f;

                if (!BurtGIRadianceCacheIsAvailable())
                {
                    return false;
                }

                uint clipmapIndex = 0u;
                float3 probeCoordFloat = 0.0f;
                if (!BurtGIRadianceCacheWorldPositionToProbeCoordDithered(worldPosition, clipmapDitherRandom, clipmapIndex, probeCoordFloat))
                {
                    return false;
                }

                uint clipmapResolution = BurtGIRadianceCacheClipmapResolution();
                float3 cornerProbeCoordFloat = probeCoordFloat - 0.5f + 0.0001f;
                int3 cornerProbeCoord = (int3)floor(cornerProbeCoordFloat);
                float3 lerpAlphas = saturate(frac(cornerProbeCoordFloat));
                float numTexels = sqrt(saturate(1.0f - cos(coneHalfAngle))) * (float)BurtGIRadianceCacheProbeResolution();
                float mipLevel = clamp(log2(max(numTexels, 1.0f)), 0.0f, BurtGIRadianceCacheFinalAtlasMaxMip());

                [unroll]
                for (int z = 0; z < 2; ++z)
                {
                    [unroll]
                    for (int y = 0; y < 2; ++y)
                    {
                        [unroll]
                        for (int x = 0; x < 2; ++x)
                        {
                            int3 sampleProbeCoordSigned = clamp(
                                cornerProbeCoord + int3(x, y, z),
                                int3(0, 0, 0),
                                int3((int)clipmapResolution - 1, (int)clipmapResolution - 1, (int)clipmapResolution - 1));
                            uint probeIndex = BurtGIRadianceCacheProbeIndexFromIndirection(clipmapIndex, (uint3)sampleProbeCoordSigned);
                            if (probeIndex >= BURT_GI_RADIANCE_CACHE_USED_PROBE_INDEX)
                            {
                                continue;
                            }

                            float3 cornerWeight = lerp(1.0f - lerpAlphas, lerpAlphas, float3((float)x, (float)y, (float)z));
                            float weight = cornerWeight.x * cornerWeight.y * cornerWeight.z;
                            radiance += BurtGISampleRadianceCacheProbeWithParallaxCorrection((uint3)sampleProbeCoordSigned, clipmapIndex, probeIndex, worldPosition, sampleDirectionWS, mipLevel) * weight;
                            confidence += weight;
                        }
                    }
                }

                if (confidence <= 0.0001f)
                {
                    return false;
                }

                confidence = saturate(confidence);
                return true;
            }

            void BurtGIAccumulateRadianceCacheIrradianceSample(
                uint probeIndex,
                float weight,
                uint clipmapIndex,
                uint3 probeCoord,
                float3 occlusionPositionWS,
                float3 normalWS,
                inout float3 irradianceSum,
                inout float weightSum)
            {
                if (probeIndex >= BURT_GI_RADIANCE_CACHE_USED_PROBE_INDEX || weight <= 0.0001f)
                {
                    return;
                }

                float3 probeWorldPosition = BurtGIRadianceCacheProbeWorldPosition(clipmapIndex, probeCoord, probeIndex);
                float3 probeToSampleWS = occlusionPositionWS - probeWorldPosition;
                float sampleDistance = length(probeToSampleWS);
                float visibility = BurtGISampleRadianceCacheProbeOcclusion(probeIndex, probeToSampleWS, sampleDistance);
                float finalWeight = BurtGIRadianceCacheApplyProbeOcclusionWeightThreshold(weight * visibility);
                irradianceSum += BurtGISampleRadianceCacheProbeIrradiance(probeIndex, normalWS) * finalWeight;
                weightSum += finalWeight;
            }

            void BurtGIAccumulateRadianceCacheSkyAOSample(
                uint probeIndex,
                float weight,
                uint clipmapIndex,
                uint3 probeCoord,
                float3 occlusionPositionWS,
                float3 normalWS,
                inout float skyAOSum,
                inout float weightSum)
            {
                if (probeIndex >= BURT_GI_RADIANCE_CACHE_USED_PROBE_INDEX || weight <= 0.0001f)
                {
                    return;
                }

                float3 probeWorldPosition = BurtGIRadianceCacheProbeWorldPosition(clipmapIndex, probeCoord, probeIndex);
                float3 probeToSampleWS = occlusionPositionWS - probeWorldPosition;
                float sampleDistance = length(probeToSampleWS);
                float visibility = BurtGISampleRadianceCacheProbeOcclusion(probeIndex, probeToSampleWS, sampleDistance);
                float finalWeight = BurtGIRadianceCacheApplyProbeOcclusionWeightThreshold(weight * visibility);
                skyAOSum += BurtGISampleRadianceCacheProbeSkyAO(probeIndex, normalWS) * finalWeight;
                weightSum += finalWeight;
            }

            bool BurtGISampleRadianceCacheClipMapIrradianceInterpolated(
                float3 worldPosition,
                float3 occlusionPositionWS,
                float3 normalWS,
                float clipmapDitherRandom,
                out float3 irradiance,
                out float confidence)
            {
                irradiance = 0.0f;
                confidence = 0.0f;

                if (!BurtGIRadianceCacheIsAvailable())
                {
                    return false;
                }

                uint clipmapIndex = 0u;
                float3 probeCoordFloat = 0.0f;
                if (!BurtGIRadianceCacheWorldPositionToProbeCoordDithered(worldPosition, clipmapDitherRandom, clipmapIndex, probeCoordFloat))
                {
                    return false;
                }

                uint clipmapResolution = BurtGIRadianceCacheClipmapResolution();
                float3 cornerProbeCoordFloat = probeCoordFloat - 0.5f + 0.0001f;
                int3 cornerProbeCoord = (int3)floor(cornerProbeCoordFloat);
                float3 lerpAlphas = saturate(frac(cornerProbeCoordFloat));
                int3 maxProbeCoord = int3((int)clipmapResolution - 1, (int)clipmapResolution - 1, (int)clipmapResolution - 1);

                uint3 probeCoord000 = (uint3)clamp(cornerProbeCoord + int3(0, 0, 0), int3(0, 0, 0), maxProbeCoord);
                uint3 probeCoord001 = (uint3)clamp(cornerProbeCoord + int3(0, 0, 1), int3(0, 0, 0), maxProbeCoord);
                uint3 probeCoord010 = (uint3)clamp(cornerProbeCoord + int3(0, 1, 0), int3(0, 0, 0), maxProbeCoord);
                uint3 probeCoord011 = (uint3)clamp(cornerProbeCoord + int3(0, 1, 1), int3(0, 0, 0), maxProbeCoord);
                uint3 probeCoord100 = (uint3)clamp(cornerProbeCoord + int3(1, 0, 0), int3(0, 0, 0), maxProbeCoord);
                uint3 probeCoord101 = (uint3)clamp(cornerProbeCoord + int3(1, 0, 1), int3(0, 0, 0), maxProbeCoord);
                uint3 probeCoord110 = (uint3)clamp(cornerProbeCoord + int3(1, 1, 0), int3(0, 0, 0), maxProbeCoord);
                uint3 probeCoord111 = (uint3)clamp(cornerProbeCoord + int3(1, 1, 1), int3(0, 0, 0), maxProbeCoord);

                uint probeIndex000 = BurtGIRadianceCacheProbeIndexFromIndirection(clipmapIndex, probeCoord000);
                uint probeIndex001 = BurtGIRadianceCacheProbeIndexFromIndirection(clipmapIndex, probeCoord001);
                uint probeIndex010 = BurtGIRadianceCacheProbeIndexFromIndirection(clipmapIndex, probeCoord010);
                uint probeIndex011 = BurtGIRadianceCacheProbeIndexFromIndirection(clipmapIndex, probeCoord011);
                uint probeIndex100 = BurtGIRadianceCacheProbeIndexFromIndirection(clipmapIndex, probeCoord100);
                uint probeIndex101 = BurtGIRadianceCacheProbeIndexFromIndirection(clipmapIndex, probeCoord101);
                uint probeIndex110 = BurtGIRadianceCacheProbeIndexFromIndirection(clipmapIndex, probeCoord110);
                uint probeIndex111 = BurtGIRadianceCacheProbeIndexFromIndirection(clipmapIndex, probeCoord111);

                float3 invAlphas = 1.0f - lerpAlphas;
                float weight000 = invAlphas.x * invAlphas.y * invAlphas.z;
                float weight001 = invAlphas.x * invAlphas.y * lerpAlphas.z;
                float weight010 = invAlphas.x * lerpAlphas.y * invAlphas.z;
                float weight011 = invAlphas.x * lerpAlphas.y * lerpAlphas.z;
                float weight100 = lerpAlphas.x * invAlphas.y * invAlphas.z;
                float weight101 = lerpAlphas.x * invAlphas.y * lerpAlphas.z;
                float weight110 = lerpAlphas.x * lerpAlphas.y * invAlphas.z;
                float weight111 = lerpAlphas.x * lerpAlphas.y * lerpAlphas.z;

                float3 irradianceSum = 0.0f;
                float weightSum = 0.0f;
                BurtGIAccumulateRadianceCacheIrradianceSample(probeIndex000, weight000, clipmapIndex, probeCoord000, occlusionPositionWS, normalWS, irradianceSum, weightSum);
                BurtGIAccumulateRadianceCacheIrradianceSample(probeIndex001, weight001, clipmapIndex, probeCoord001, occlusionPositionWS, normalWS, irradianceSum, weightSum);
                BurtGIAccumulateRadianceCacheIrradianceSample(probeIndex010, weight010, clipmapIndex, probeCoord010, occlusionPositionWS, normalWS, irradianceSum, weightSum);
                BurtGIAccumulateRadianceCacheIrradianceSample(probeIndex011, weight011, clipmapIndex, probeCoord011, occlusionPositionWS, normalWS, irradianceSum, weightSum);
                BurtGIAccumulateRadianceCacheIrradianceSample(probeIndex100, weight100, clipmapIndex, probeCoord100, occlusionPositionWS, normalWS, irradianceSum, weightSum);
                BurtGIAccumulateRadianceCacheIrradianceSample(probeIndex101, weight101, clipmapIndex, probeCoord101, occlusionPositionWS, normalWS, irradianceSum, weightSum);
                BurtGIAccumulateRadianceCacheIrradianceSample(probeIndex110, weight110, clipmapIndex, probeCoord110, occlusionPositionWS, normalWS, irradianceSum, weightSum);
                BurtGIAccumulateRadianceCacheIrradianceSample(probeIndex111, weight111, clipmapIndex, probeCoord111, occlusionPositionWS, normalWS, irradianceSum, weightSum);

                if (weightSum <= 0.0001f)
                {
                    return false;
                }

                irradiance = irradianceSum;
                confidence = saturate(weightSum);
                return confidence > 0.0001f;
            }

            bool BurtGISampleRadianceCacheClipMapSkyAOInterpolated(
                float3 worldPosition,
                float3 occlusionPositionWS,
                float3 normalWS,
                float clipmapDitherRandom,
                out float skyAO)
            {
                skyAO = 0.0f;

                if (!BurtGIRadianceCacheIsAvailable())
                {
                    return false;
                }

                uint clipmapIndex = 0u;
                float3 probeCoordFloat = 0.0f;
                if (!BurtGIRadianceCacheWorldPositionToProbeCoordDithered(worldPosition, clipmapDitherRandom, clipmapIndex, probeCoordFloat))
                {
                    return false;
                }

                uint clipmapResolution = BurtGIRadianceCacheClipmapResolution();
                float3 cornerProbeCoordFloat = probeCoordFloat - 0.5f + 0.0001f;
                int3 cornerProbeCoord = (int3)floor(cornerProbeCoordFloat);
                float3 lerpAlphas = saturate(frac(cornerProbeCoordFloat));
                int3 maxProbeCoord = int3((int)clipmapResolution - 1, (int)clipmapResolution - 1, (int)clipmapResolution - 1);

                uint3 probeCoord000 = (uint3)clamp(cornerProbeCoord + int3(0, 0, 0), int3(0, 0, 0), maxProbeCoord);
                uint3 probeCoord001 = (uint3)clamp(cornerProbeCoord + int3(0, 0, 1), int3(0, 0, 0), maxProbeCoord);
                uint3 probeCoord010 = (uint3)clamp(cornerProbeCoord + int3(0, 1, 0), int3(0, 0, 0), maxProbeCoord);
                uint3 probeCoord011 = (uint3)clamp(cornerProbeCoord + int3(0, 1, 1), int3(0, 0, 0), maxProbeCoord);
                uint3 probeCoord100 = (uint3)clamp(cornerProbeCoord + int3(1, 0, 0), int3(0, 0, 0), maxProbeCoord);
                uint3 probeCoord101 = (uint3)clamp(cornerProbeCoord + int3(1, 0, 1), int3(0, 0, 0), maxProbeCoord);
                uint3 probeCoord110 = (uint3)clamp(cornerProbeCoord + int3(1, 1, 0), int3(0, 0, 0), maxProbeCoord);
                uint3 probeCoord111 = (uint3)clamp(cornerProbeCoord + int3(1, 1, 1), int3(0, 0, 0), maxProbeCoord);

                uint probeIndex000 = BurtGIRadianceCacheProbeIndexFromIndirection(clipmapIndex, probeCoord000);
                uint probeIndex001 = BurtGIRadianceCacheProbeIndexFromIndirection(clipmapIndex, probeCoord001);
                uint probeIndex010 = BurtGIRadianceCacheProbeIndexFromIndirection(clipmapIndex, probeCoord010);
                uint probeIndex011 = BurtGIRadianceCacheProbeIndexFromIndirection(clipmapIndex, probeCoord011);
                uint probeIndex100 = BurtGIRadianceCacheProbeIndexFromIndirection(clipmapIndex, probeCoord100);
                uint probeIndex101 = BurtGIRadianceCacheProbeIndexFromIndirection(clipmapIndex, probeCoord101);
                uint probeIndex110 = BurtGIRadianceCacheProbeIndexFromIndirection(clipmapIndex, probeCoord110);
                uint probeIndex111 = BurtGIRadianceCacheProbeIndexFromIndirection(clipmapIndex, probeCoord111);

                float3 invAlphas = 1.0f - lerpAlphas;
                float skyAOSum = 0.0f;
                float weightSum = 0.0f;
                BurtGIAccumulateRadianceCacheSkyAOSample(probeIndex000, invAlphas.x * invAlphas.y * invAlphas.z, clipmapIndex, probeCoord000, occlusionPositionWS, normalWS, skyAOSum, weightSum);
                BurtGIAccumulateRadianceCacheSkyAOSample(probeIndex001, invAlphas.x * invAlphas.y * lerpAlphas.z, clipmapIndex, probeCoord001, occlusionPositionWS, normalWS, skyAOSum, weightSum);
                BurtGIAccumulateRadianceCacheSkyAOSample(probeIndex010, invAlphas.x * lerpAlphas.y * invAlphas.z, clipmapIndex, probeCoord010, occlusionPositionWS, normalWS, skyAOSum, weightSum);
                BurtGIAccumulateRadianceCacheSkyAOSample(probeIndex011, invAlphas.x * lerpAlphas.y * lerpAlphas.z, clipmapIndex, probeCoord011, occlusionPositionWS, normalWS, skyAOSum, weightSum);
                BurtGIAccumulateRadianceCacheSkyAOSample(probeIndex100, lerpAlphas.x * invAlphas.y * invAlphas.z, clipmapIndex, probeCoord100, occlusionPositionWS, normalWS, skyAOSum, weightSum);
                BurtGIAccumulateRadianceCacheSkyAOSample(probeIndex101, lerpAlphas.x * invAlphas.y * lerpAlphas.z, clipmapIndex, probeCoord101, occlusionPositionWS, normalWS, skyAOSum, weightSum);
                BurtGIAccumulateRadianceCacheSkyAOSample(probeIndex110, lerpAlphas.x * lerpAlphas.y * invAlphas.z, clipmapIndex, probeCoord110, occlusionPositionWS, normalWS, skyAOSum, weightSum);
                BurtGIAccumulateRadianceCacheSkyAOSample(probeIndex111, lerpAlphas.x * lerpAlphas.y * lerpAlphas.z, clipmapIndex, probeCoord111, occlusionPositionWS, normalWS, skyAOSum, weightSum);

                if (weightSum <= 0.0001f)
                {
                    return false;
                }

                skyAO = saturate(skyAOSum / max(weightSum, 0.0001f));
                return true;
            }

            void BurtGISampleScreenProbeBentNormalAO(
                float2 screenUV,
                float3 fallbackNormalWS,
                out float3 unitBentNormalWS,
                out float bentAO,
                out float valid)
            {
                float4 encodedBentNormal = tex2D(_BurtGIScreenProbeBentNormalTexture, screenUV);
                float3 bentNormal = encodedBentNormal.xyz * 2.0f - 1.0f;
                float bentNormalLength = length(bentNormal);
                valid = encodedBentNormal.a > 0.0001f ? 1.0f : 0.0f;
                bentAO = saturate(encodedBentNormal.a);
                unitBentNormalWS = bentNormalLength > 0.0001f ? bentNormal / bentNormalLength : fallbackNormalWS;
                unitBentNormalWS = valid > 0.5f ? unitBentNormalWS : fallbackNormalWS;
            }

            float BurtGIResolveShortRangeAOWeight()
            {
                return _BurtGIShortRangeAOParams.x > 0.5f ? saturate(_BurtGIShortRangeAOParams.y) : 0.0f;
            }

            float BurtGIApplyShortRangeAOWeight(float occlusion)
            {
                return lerp(1.0f, saturate(occlusion), BurtGIResolveShortRangeAOWeight());
            }

            float3 BurtGIApplyShortRangeAOBentNormal(float3 surfaceNormalWS, float3 unitBentNormalWS, float bentAO, float valid)
            {
                float shortRangeAOWeight = BurtGIResolveShortRangeAOWeight();
                if (valid <= 0.5f || shortRangeAOWeight <= 0.0001f)
                {
                    return surfaceNormalWS;
                }

                float3 bentLightingNormalWS = BurtSafeNormalize(lerp(unitBentNormalWS, surfaceNormalWS, bentAO));
                return BurtSafeNormalize(lerp(surfaceNormalWS, bentLightingNormalWS, shortRangeAOWeight));
            }

            float4 BurtGIApplyScreenProbeLite(float2 screenUV, float4 filtered)
            {
                float applyStrength = saturate(_BurtGIScreenProbeParams.w);
                if (applyStrength <= 0.0001f)
                {
                    return filtered;
                }

                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtGIIsSkyDepth(rawDepth))
                {
                    return filtered;
                }

                float3 centerNormalWS = BurtGISampleNormalWS(screenUV);
                float3 unitBentNormalWS;
                float bentAO;
                float bentNormalValid;
                BurtGISampleScreenProbeBentNormalAO(screenUV, centerNormalWS, unitBentNormalWS, bentAO, bentNormalValid);
                float3 probeLightingNormalWS = BurtGIApplyShortRangeAOBentNormal(centerNormalWS, unitBentNormalWS, bentAO, bentNormalValid);
                float3 positionWS = BurtReconstructDeferredPositionWS(screenUV, rawDepth);
                BurtEncodedGBuffer encodedGBuffer = BurtSampleEncodedGBuffer(screenUV);
                BurtGBufferData gbufferData = BurtDecodeDeferredGBuffer(encodedGBuffer, screenUV);
                float receiverFoliageWeight = BurtIsActiveFoliageShadingModel(gbufferData.ShadingModelID) ? 1.0f : 0.0f;
                float4 probeIrradiance;
                float4 probeConfidence;
                float screenProbeSampleValidity;
                BurtGISampleUniformScreenProbePlaneWeighted(
                    screenUV,
                    positionWS,
                    LinearEyeDepth(rawDepth),
                    probeLightingNormalWS,
                    receiverFoliageWeight,
                    probeIrradiance,
                    probeConfidence,
                    screenProbeSampleValidity);
                float4 probeMipIrradiance = tex2D(_BurtGIScreenProbeMipIrradianceTexture, screenUV);
                float4 probeMipConfidence = tex2D(_BurtGIScreenProbeMipConfidenceTexture, screenUV);
                float4 probeMip2Irradiance = tex2D(_BurtGIScreenProbeMip2IrradianceTexture, screenUV);
                float4 probeMip2Confidence = tex2D(_BurtGIScreenProbeMip2ConfidenceTexture, screenUV);
                float4 probeMip3Irradiance = tex2D(_BurtGIScreenProbeMip3IrradianceTexture, screenUV);
                float4 probeMip3Confidence = tex2D(_BurtGIScreenProbeMip3ConfidenceTexture, screenUV);
                float confidence = saturate(min(probeIrradiance.a, probeConfidence.r) * screenProbeSampleValidity);
                float3 adaptiveIrradiance;
                float adaptiveConfidence;
                float2 ignoredAdaptiveAtlasProbeCoord;
                if (BurtGISampleAdaptiveScreenProbeTraceAtlas(screenUV, positionWS, LinearEyeDepth(rawDepth), probeLightingNormalWS, confidence, probeConfidence.b, receiverFoliageWeight, adaptiveIrradiance, adaptiveConfidence, ignoredAdaptiveAtlasProbeCoord))
                {
                    float adaptiveFill = saturate((0.74f - confidence) * 1.3513514f + probeConfidence.b * 0.24f) * adaptiveConfidence;
                    probeIrradiance.rgb = lerp(probeIrradiance.rgb, adaptiveIrradiance, adaptiveFill * 0.55f);
                    confidence = saturate(max(confidence, adaptiveConfidence * adaptiveFill * 0.65f));
                    probeConfidence.r = max(probeConfidence.r, confidence);
                }

                float mipConfidence = saturate(min(probeMipIrradiance.a, probeMipConfidence.r));
                float mip2Confidence = saturate(min(probeMip2Irradiance.a, probeMip2Confidence.r));
                float mip3Confidence = saturate(min(probeMip3Irradiance.a, probeMip3Confidence.r));
                float mipFill = saturate((0.42f - confidence) * 2.5f) * mipConfidence;
                float mip2Fill = saturate((0.28f - confidence) * 3.5714285f) * mip2Confidence;
                float mip3Fill = saturate((0.18f - confidence) * 5.5555553f) * mip3Confidence;
                float3 mip2Irradiance = lerp(max(probeMip2Irradiance.rgb, 0.0f), max(probeMip3Irradiance.rgb, 0.0f), mip3Fill * 0.55f);
                float3 mipIrradiance = lerp(max(probeMipIrradiance.rgb, 0.0f), mip2Irradiance, mip2Fill * 0.65f);
                probeIrradiance.rgb = lerp(probeIrradiance.rgb, mipIrradiance, mipFill * 0.55f + mip2Fill * 0.25f + mip3Fill * 0.15f);
                confidence = saturate(max(confidence, mipConfidence * mipFill * 0.55f));
                confidence = saturate(max(confidence, mip2Confidence * mip2Fill * 0.45f));
                confidence = saturate(max(confidence, mip3Confidence * mip3Fill * 0.35f));
                float4 probeSHAmbient = tex2D(_BurtGIScreenProbeRadianceSHAmbientTexture, screenUV);
                float shConfidence = saturate(probeSHAmbient.a);
                float shFill = saturate((0.65f - confidence) * 1.55f) * shConfidence;
                float shCosThetaAO = bentNormalValid > 0.5f ? saturate((1.0f - bentAO) * BurtGIResolveShortRangeAOWeight()) : 0.0f;
                float3 evaluatedSHIrradiance = BurtGIEvaluateScreenProbeSH3Irradiance(screenUV, probeLightingNormalWS, shCosThetaAO);
                float3 ambientOnlySHIrradiance = max(probeSHAmbient.rgb, 0.0f) * BURT_GI_SH0_COEFFICIENT_TO_IRRADIANCE;
                float3 shIrradiance = lerp(ambientOnlySHIrradiance, evaluatedSHIrradiance, 0.35f);
                probeIrradiance.rgb = lerp(probeIrradiance.rgb, shIrradiance, shFill * 0.45f);
                confidence = saturate(max(confidence, shConfidence * shFill * 0.65f));
                float3 shDirectionalMagnitude = BurtGISampleScreenProbeSHDirectionalMagnitude(screenUV);
                probeIrradiance.rgb += shDirectionalMagnitude * (applyStrength * confidence * 0.012f);
                float4 probeOctIrradiance = BurtGISampleScreenProbeIrradianceOct(screenUV, positionWS, LinearEyeDepth(rawDepth), probeLightingNormalWS, 0.0f, receiverFoliageWeight);
                float octConfidence = saturate(probeOctIrradiance.a);
                float octFill = saturate((0.72f - confidence) * 1.3888888f) * octConfidence;
                float octDirectionalRefine = saturate(octConfidence * confidence) * 0.18f;
                probeIrradiance.rgb = lerp(probeIrradiance.rgb, max(probeOctIrradiance.rgb, 0.0f), saturate(octFill * 0.35f + octDirectionalRefine));
                confidence = saturate(max(confidence, octConfidence * octFill * 0.55f));
                float hitRatio = saturate(filtered.a);
                float3 radianceCacheIrradiance;
                float radianceCacheConfidence;
                float radianceCacheClipmapDither = BurtGIHash12(screenUV * _BurtGISourceTexelSize.zw, _BurtGIParams2.x);
                if (BurtGISampleRadianceCacheClipMapIrradianceInterpolated(positionWS, positionWS, centerNormalWS, radianceCacheClipmapDither, radianceCacheIrradiance, radianceCacheConfidence))
                {
                    float radianceCacheFill = applyStrength * radianceCacheConfidence * saturate(0.25f + (1.0f - confidence) * 0.75f) * lerp(1.0f, 0.45f, hitRatio);
                    probeIrradiance.rgb = lerp(probeIrradiance.rgb, radianceCacheIrradiance, saturate(radianceCacheFill * 0.65f));
                    confidence = saturate(max(confidence, radianceCacheConfidence * radianceCacheFill * 0.75f));
                }
                float fillWeight = applyStrength * confidence * lerp(1.0f, 0.45f, hitRatio);
                // XRender writes DiffuseLighting * Fd_Lambert to DiffuseIndirect.
                // Keep screen trace and screen-probe fill in the same pre-material-lighting space.
                float3 probeDiffuseLighting = max(probeIrradiance.rgb, 0.0f) * BURT_INV_PI;
                float3 integratedGI = lerp(filtered.rgb, probeDiffuseLighting, fillWeight);
                return float4(max(integratedGI, 0.0f), saturate(max(hitRatio, probeConfidence.g * fillWeight)));
            }

            bool BurtGISampleScreenProbeTraceAtlasDirectionalRoughMipLite(
                float2 screenUV,
                float3 lightingDirectionWS,
                float roughness,
                out float3 radiance,
                out float confidence)
            {
                radiance = 0.0f;
                confidence = 0.0f;

                float octResolution = max(_BurtGIScreenProbeTraceParams.x, 1.0f);
                float2 gridSize = max(_BurtGIScreenProbeGridParams.xy, 1.0f);
                if (octResolution < 2.0f || gridSize.x < 1.0f || gridSize.y < 1.0f)
                {
                    return false;
                }

                float3 unitLightingDirectionWS = BurtSafeNormalize(lightingDirectionWS);
                float2 probeCoord = BurtGIScreenProbeFallbackCoord(screenUV, gridSize);
                float2 centerOctUV = BurtGIInverseEquiAreaSphericalMapping(unitLightingDirectionWS);
                float2 centerOctCoord = centerOctUV * octResolution - 0.5f;
                float radiusFloat = lerp(1.0f, 3.0f, saturate((roughness - 0.28f) * 1.3888888f));
                int radius = (int)round(radiusFloat);
                float3 radianceSum = 0.0f;
                float confidenceSum = 0.0f;
                float weightSum = 0.0f;

                [unroll]
                for (int y = -3; y <= 3; ++y)
                {
                    [unroll]
                    for (int x = -3; x <= 3; ++x)
                    {
                        if (abs(x) > radius || abs(y) > radius)
                        {
                            continue;
                        }

                        float2 octCoord = clamp(floor(centerOctCoord) + float2((float)x, (float)y), 0.0f, octResolution - 1.0f);
                        float3 sampleDirectionWS;
                        float traceSampleWeight;
                        BurtGIResolveScreenProbeImportanceTraceSample(
                            probeCoord,
                            octCoord,
                            octResolution,
                            sampleDirectionWS,
                            traceSampleWeight);
                        float angularWeight = pow(saturate(dot(unitLightingDirectionWS, sampleDirectionWS)), lerp(48.0f, 5.0f, saturate(roughness)));
                        float2 octDelta = abs(octCoord - centerOctCoord) / max(radiusFloat, 1.0f);
                        float distanceWeight = saturate(1.0f - dot(octDelta, octDelta) * 0.35f);
                        float2 atlasUV = BurtGITraceAtlasUV(probeCoord, octCoord, octResolution);
                        float4 traceRadiance = _BurtGIScreenProbeTraceRadianceTexture.SampleLevel(sampler_LinearClamp, atlasUV, 0.0f);
                        float traceHit = saturate(_BurtGIScreenProbeTraceHitTexture.SampleLevel(sampler_LinearClamp, atlasUV, 0.0f).r);
                        float traceConfidence = saturate(max(traceHit, traceRadiance.a));
                        float sampleWeight = traceSampleWeight * angularWeight * distanceWeight * max(traceConfidence, 0.035f);
                        radianceSum += max(traceRadiance.rgb, 0.0f) * sampleWeight;
                        confidenceSum += traceConfidence * sampleWeight;
                        weightSum += sampleWeight;
                    }
                }

                if (weightSum <= 0.0001f)
                {
                    return false;
                }

                radiance = radianceSum / weightSum;
                confidence = saturate(confidenceSum / weightSum);
                return confidence > 0.0001f;
            }

            float3 BurtGIResolveScreenProbeDirectionalLightingLite(
                float2 screenUV,
                float3 positionWS,
                float sceneDepth,
                float3 planeNormalWS,
                float3 lightingDirectionWS,
                float roughness,
                float receiverFoliageWeight,
                out float confidence)
            {
                confidence = 0.0f;
                float applyStrength = saturate(_BurtGIScreenProbeParams.w);
                if (applyStrength <= 0.0001f)
                {
                    return 0.0f;
                }

                float3 unitPlaneNormalWS = BurtSafeNormalize(planeNormalWS);
                float3 unitLightingDirectionWS = BurtSafeNormalize(lightingDirectionWS);
                float4 probeIrradiance;
                float4 probeConfidence;
                float screenProbeSampleValidity;
                BurtGISampleUniformScreenProbePlaneWeighted(
                    screenUV,
                    positionWS,
                    sceneDepth,
                    unitPlaneNormalWS,
                    receiverFoliageWeight,
                    probeIrradiance,
                    probeConfidence,
                    screenProbeSampleValidity);

                float3 directionalLighting = max(probeIrradiance.rgb, 0.0f);
                confidence = saturate(min(probeIrradiance.a, probeConfidence.r) * screenProbeSampleValidity);
                float roughMipBias = saturate((roughness - 0.35f) * 1.5384616f);

                float3 traceAtlasDirectionalRadiance;
                float traceAtlasDirectionalConfidence;
                if (BurtGISampleScreenProbeTraceAtlasDirectionalRoughMipLite(screenUV, unitLightingDirectionWS, roughness, traceAtlasDirectionalRadiance, traceAtlasDirectionalConfidence))
                {
                    float traceAtlasFill = traceAtlasDirectionalConfidence * lerp(0.35f, 0.78f, roughMipBias);
                    directionalLighting = lerp(directionalLighting, traceAtlasDirectionalRadiance, saturate(traceAtlasFill));
                    confidence = saturate(max(confidence, traceAtlasDirectionalConfidence * traceAtlasFill));
                }

                float4 probeMipIrradiance = tex2D(_BurtGIScreenProbeMipIrradianceTexture, screenUV);
                float4 probeMipConfidence = tex2D(_BurtGIScreenProbeMipConfidenceTexture, screenUV);
                float4 probeMip2Irradiance = tex2D(_BurtGIScreenProbeMip2IrradianceTexture, screenUV);
                float4 probeMip2Confidence = tex2D(_BurtGIScreenProbeMip2ConfidenceTexture, screenUV);
                float4 probeMip3Irradiance = tex2D(_BurtGIScreenProbeMip3IrradianceTexture, screenUV);
                float4 probeMip3Confidence = tex2D(_BurtGIScreenProbeMip3ConfidenceTexture, screenUV);
                float mipConfidence = saturate(min(probeMipIrradiance.a, probeMipConfidence.r));
                float mip2Confidence = saturate(min(probeMip2Irradiance.a, probeMip2Confidence.r));
                float mip3Confidence = saturate(min(probeMip3Irradiance.a, probeMip3Confidence.r));
                float mipFill = saturate((0.48f - confidence) * 2.0833333f + roughMipBias * 0.25f) * mipConfidence;
                float mip2Fill = saturate((0.32f - confidence) * 3.125f + roughMipBias * 0.35f) * mip2Confidence;
                float mip3Fill = saturate((0.20f - confidence) * 5.0f + roughMipBias * 0.45f) * mip3Confidence;
                float3 lowFrequencyLighting = lerp(max(probeMipIrradiance.rgb, 0.0f), max(probeMip2Irradiance.rgb, 0.0f), mip2Fill * 0.65f);
                lowFrequencyLighting = lerp(lowFrequencyLighting, max(probeMip3Irradiance.rgb, 0.0f), mip3Fill * 0.55f);
                directionalLighting = lerp(directionalLighting, lowFrequencyLighting, saturate(mipFill * 0.5f + mip2Fill * 0.3f + mip3Fill * 0.2f));
                confidence = saturate(max(confidence, mipConfidence * mipFill * 0.5f));
                confidence = saturate(max(confidence, mip2Confidence * mip2Fill * 0.45f));
                confidence = saturate(max(confidence, mip3Confidence * mip3Fill * 0.35f));

                float4 probeSHAmbient = tex2D(_BurtGIScreenProbeRadianceSHAmbientTexture, screenUV);
                float shConfidence = saturate(probeSHAmbient.a);
                float3 shLighting = BurtGIEvaluateScreenProbeSH3Irradiance(screenUV, unitLightingDirectionWS);
                float shFill = saturate((0.70f - confidence) * 1.4285715f) * shConfidence;
                directionalLighting = lerp(directionalLighting, shLighting, saturate(shFill * lerp(0.35f, 0.55f, roughMipBias)));
                confidence = saturate(max(confidence, shConfidence * shFill * 0.65f));

                float roughSpecularSampleCount = 1.0f;
                float roughSpecularSolidAngle = rcp(max(roughSpecularSampleCount, 1.0f));
                float roughSpecularCosConeHalfAngle = 1.0f - roughSpecularSolidAngle / (2.0f * UNITY_PI);
                float roughSpecularFootprintTexels = sqrt(max(0.0f, 1.0f - roughSpecularCosConeHalfAngle)) * 6.0f;
                float roughSpecularOctMipLevel = clamp(log2(max(roughSpecularFootprintTexels, 1.0f)) + roughMipBias * 1.25f, 0.0f, 3.0f);
                float4 octLighting = BurtGISampleScreenProbeIrradianceOct(
                    screenUV,
                    positionWS,
                    sceneDepth,
                    unitLightingDirectionWS,
                    roughSpecularOctMipLevel,
                    receiverFoliageWeight);
                float octConfidence = saturate(octLighting.a);
                float octFill = saturate((0.76f - confidence) * 1.3157895f) * octConfidence;
                float octDirectionalRefine = saturate(octConfidence * confidence) * lerp(0.14f, 0.28f, roughMipBias);
                directionalLighting = lerp(directionalLighting, max(octLighting.rgb, 0.0f), saturate(octFill * 0.55f + octDirectionalRefine));
                confidence = saturate(max(confidence, octConfidence * octFill * 0.6f));

                float4 octRadiance = BurtGISampleScreenProbeRadianceOct(
                    screenUV,
                    positionWS,
                    sceneDepth,
                    unitPlaneNormalWS,
                    unitLightingDirectionWS,
                    roughSpecularOctMipLevel,
                    receiverFoliageWeight);
                float octRadianceConfidence = saturate(octRadiance.a);
                float octRadianceFill = saturate((0.82f - confidence) * 1.2195122f) * octRadianceConfidence;
                directionalLighting = lerp(
                    directionalLighting,
                    max(octRadiance.rgb, 0.0f),
                    saturate(octRadianceFill * lerp(0.3f, 0.72f, roughMipBias)));
                confidence = saturate(max(confidence, octRadianceConfidence * octRadianceFill * 0.65f));

                float3 adaptiveRadiance;
                float adaptiveRadianceConfidence;
                if (BurtGISampleAdaptiveScreenProbeRadianceOct(
                    screenUV,
                    positionWS,
                    sceneDepth,
                    unitPlaneNormalWS,
                    unitLightingDirectionWS,
                    roughSpecularOctMipLevel,
                    confidence,
                    probeConfidence.b,
                    receiverFoliageWeight,
                    adaptiveRadiance,
                    adaptiveRadianceConfidence))
                {
                    float adaptiveRadianceFill = saturate((0.78f - confidence) * 1.2820513f + probeConfidence.b * 0.2f) * adaptiveRadianceConfidence;
                    directionalLighting = lerp(
                        directionalLighting,
                        adaptiveRadiance,
                        saturate(adaptiveRadianceFill * lerp(0.24f, 0.66f, roughMipBias)));
                    confidence = saturate(max(confidence, adaptiveRadianceConfidence * adaptiveRadianceFill * 0.65f));
                }

                float3 radianceCacheLighting;
                float radianceCacheConfidence;
                float radianceCacheConeHalfAngle = lerp(0.06f, 0.65f, roughMipBias);
                float radianceCacheClipmapDither = BurtGIHash12(screenUV * _BurtGISourceTexelSize.zw + 23.17f, _BurtGIParams2.x + 11.0f);
                if (BurtGISampleRadianceCacheClipMapInterpolated(positionWS, unitLightingDirectionWS, radianceCacheConeHalfAngle, radianceCacheClipmapDither, radianceCacheLighting, radianceCacheConfidence))
                {
                    float radianceCacheFill = applyStrength * radianceCacheConfidence * saturate(0.2f + (1.0f - confidence) * 0.8f);
                    directionalLighting = lerp(directionalLighting, max(radianceCacheLighting, 0.0f), saturate(radianceCacheFill * 0.45f));
                    confidence = saturate(max(confidence, radianceCacheConfidence * radianceCacheFill * 0.65f));
                }

                confidence *= applyStrength;
                return max(directionalLighting, 0.0f);
            }

            float BurtGISampleScreenProbeUseCountHistoryWeight(float2 historyUV)
            {
                if (_BurtGIScreenProbeUseCountParams.w < 0.5f)
                {
                    return 1.0f;
                }

                float2 useCountSize = max(_BurtGIScreenProbeUseCountParams.xy, 1.0f);
                float2 historyPixel = saturate(historyUV) * useCountSize - 0.5f;
                float2 topLeft = floor(historyPixel);
                float2 bilinear = historyPixel - topLeft;
                float4 weights = float4(
                    (1.0f - bilinear.x) * (1.0f - bilinear.y),
                    bilinear.x * (1.0f - bilinear.y),
                    (1.0f - bilinear.x) * bilinear.y,
                    bilinear.x * bilinear.y);
                float useCount = 0.0f;

                [unroll]
                for (int tapIndex = 0; tapIndex < 4; ++tapIndex)
                {
                    int2 tapOffset = int2(tapIndex & 1, tapIndex >> 1);
                    int2 tapCoord = clamp((int2)topLeft + tapOffset, int2(0, 0), (int2)useCountSize - 1);
                    useCount += weights[tapIndex] * (float)_BurtGIScreenProbeUseCountTexture.Load(int3(tapCoord, 0));
                }

                useCount *= rcp(255.0f);
                return saturate(1.0f - abs(useCount - 1.0f));
            }

            bool BurtGIResolveScreenProbeHistoryUV(float2 sourceUV, float3 positionWS, out float2 historyUV, out float projectedRawDepth)
            {
                if (!BurtGIProjectHistoryUV(positionWS, historyUV, projectedRawDepth))
                {
                    return false;
                }

                float2 velocitySize = max(_BurtGIScreenProbeUseCountParams.xy, 1.0f);
                int2 velocityCoord = clamp((int2)(saturate(sourceUV) * velocitySize), int2(0, 0), (int2)velocitySize - 1);
                float4 sceneVelocity = _BurtGIScreenProbeSceneVelocityTexture.Load(int3(velocityCoord, 0));
                if (sceneVelocity.w > 0.5f)
                {
                    float2 velocityHistoryUV = sourceUV - sceneVelocity.xy;
                    if (any(velocityHistoryUV < 0.0f) || any(velocityHistoryUV > 1.0f))
                    {
                        return false;
                    }

                    historyUV = velocityHistoryUV;
                }

                return true;
            }

            BurtGIScreenProbeOutput BurtGIApplyScreenProbeTemporal(float2 sourceUV, float rawDepth, BurtGIScreenProbeOutput current, out float numFramesAccumulatedNormalized)
            {
                numFramesAccumulatedNormalized = 0.0f;
                current.Confidence.a = rawDepth;
                if (_BurtGIScreenProbeTemporalParams.y < 0.5f)
                {
                    return current;
                }

                float3 positionWS = BurtReconstructDeferredPositionWS(sourceUV, rawDepth);
                float2 historyUV;
                float projectedRawDepth;
                if (!BurtGIResolveScreenProbeHistoryUV(sourceUV, positionWS, historyUV, projectedRawDepth))
                {
                    return current;
                }

                float4 historyConfidence = tex2D(_BurtGIScreenProbeHistoryConfidenceTexture, historyUV);
                float historyRawDepth = tex2D(_BurtGIScreenProbeHistoryScreenDepthTexture, historyUV).r;
                historyRawDepth = historyRawDepth > 0.0f ? historyRawDepth : historyConfidence.a;
                float currentTraceHit = BurtGISampleScreenProbeTraceHitAverage(sourceUV);
                float historyTraceHit = BurtGISampleHistoryScreenProbeTraceHitAverage(historyUV);
                float traceHitValidity = lerp(0.55f, 1.0f, saturate(1.0f - abs(currentTraceHit - historyTraceHit) * 1.35f));

                float projectedLinearDepth = max(LinearEyeDepth(projectedRawDepth), 0.0001f);
                float depthRejectScale = max(_BurtGIScreenProbeReprojectionParams.x * _BurtGIScreenProbeReprojectionParams.y, 0.0001f);
                float centerDepthValidity = 1.0f;
                if (!BurtGIIsSkyDepth(historyRawDepth) && historyConfidence.r > 0.0001f)
                {
                    float historyLinearDepth = LinearEyeDepth(historyRawDepth);
                    centerDepthValidity = saturate(1.0f - depthRejectScale * abs(historyLinearDepth - projectedLinearDepth) / projectedLinearDepth);
                }

                float3 normalWS = BurtGISampleNormalWS(sourceUV);
                float historyNormalCosThreshold = clamp(_BurtGIScreenProbeReprojectionParams.w, -1.0f, 1.0f);
                float4 scenePlane = float4(normalWS, dot(positionWS, normalWS));
                float2 gridSize = max(_BurtGIScreenProbeGridParams.xy, 1.0f);
                float2 historyProbeCoord = BurtGIScreenProbeTileCoordFromScreenUV(historyUV, BurtGIScreenProbePreviousJitterIndex());
                float2 historyBaseCoord = floor(historyProbeCoord);
                float4 totalHistoryRadiance = 0.0f;
                float4 totalHistoryIrradiance = 0.0f;
                float3 totalHistoryConfidence = 0.0f;
                float totalHistoryWeight = 0.0f;
                float historyExposureCorrection = max(_BurtGIScreenProbeHistoryParams.x, 0.0f);

                [unroll]
                for (int y = 0; y < 2; ++y)
                {
                    [unroll]
                    for (int x = 0; x < 2; ++x)
                    {
                        float2 neighborCoord = clamp(historyBaseCoord + float2((float)x, (float)y), 0.0f, gridSize - 1.0f);
                        float2 neighborUV = (neighborCoord + 0.5f) * _BurtGIScreenProbeGridParams.zw;
                        float4 neighborConfidence = tex2D(_BurtGIScreenProbeHistoryConfidenceTexture, neighborUV);
                        if (neighborConfidence.r <= 0.0001f)
                        {
                            continue;
                        }

                        float4 neighborWorldPosition = tex2D(_BurtGIScreenProbeHistoryWorldPositionTexture, neighborUV);
                        float neighborRawDepth = tex2D(_BurtGIScreenProbeHistoryScreenDepthTexture, neighborUV).r;
                        if (neighborWorldPosition.w <= 0.0001f || BurtGIIsSkyDepth(neighborRawDepth))
                        {
                            continue;
                        }

                        float planeDistance = abs(dot(float4(neighborWorldPosition.xyz, -1.0f), scenePlane));
                        float relativeDepthDifference = planeDistance / max(LinearEyeDepth(rawDepth), 0.0001f);
                        float planeWeight = exp2(-10000.0f * relativeDepthDifference * relativeDepthDifference) > 0.1f ? 1.0f : 0.0f;
                        float neighborLinearDepth = LinearEyeDepth(neighborRawDepth);
                        float neighborDepthValidity = saturate(1.0f - depthRejectScale * abs(neighborLinearDepth - projectedLinearDepth) / projectedLinearDepth);
                        float4 neighborNormalData = tex2D(_BurtGIScreenProbeHistoryWorldNormalTexture, neighborUV);
                        float3 neighborNormalWS = normalize(neighborNormalData.xyz * 2.0f - 1.0f);
                        float normalWeight = neighborNormalData.a > 0.5f && dot(normalWS, neighborNormalWS) >= historyNormalCosThreshold ? 1.0f : 0.0f;
                        float neighborWeight = planeWeight * neighborDepthValidity * normalWeight * saturate(neighborConfidence.r * 2.0f);
                        float4 neighborHistoryRadiance = tex2D(_BurtGIScreenProbeHistoryRadianceTexture, neighborUV);
                        float4 neighborHistoryIrradiance = tex2D(_BurtGIScreenProbeHistoryIrradianceTexture, neighborUV);
                        neighborHistoryRadiance.rgb = max(neighborHistoryRadiance.rgb * historyExposureCorrection, 0.0f);
                        neighborHistoryIrradiance.rgb = max(neighborHistoryIrradiance.rgb * historyExposureCorrection, 0.0f);
                        totalHistoryRadiance += neighborHistoryRadiance * neighborWeight;
                        totalHistoryIrradiance += neighborHistoryIrradiance * neighborWeight;
                        totalHistoryConfidence += neighborConfidence.rgb * neighborWeight;
                        totalHistoryWeight += neighborWeight;
                    }
                }

                if (totalHistoryWeight <= 0.0001f)
                {
                    return current;
                }

                float invHistoryWeight = rcp(totalHistoryWeight);
                float4 historyRadiance = totalHistoryRadiance * invHistoryWeight;
                float4 historyIrradiance = totalHistoryIrradiance * invHistoryWeight;
                float3 historyConfidenceRGB = totalHistoryConfidence * invHistoryWeight;
                float confidenceValidity = saturate(min(historyConfidenceRGB.r, current.Confidence.r) * 2.0f);
                float historyVisibility = centerDepthValidity * confidenceValidity * traceHitValidity * saturate(totalHistoryWeight);
                historyVisibility = min(historyVisibility, BurtGISampleScreenProbeUseCountHistoryWeight(historyUV));
                float maxFramesAccumulated = max(_BurtGIScreenProbeReprojectionParams.z, 1.0f);
                float previousNumFramesAccumulated = tex2D(_BurtGIScreenProbeHistoryNumFramesAccumulatedTexture, historyUV).r * maxFramesAccumulated;
                float maxFramesFromVisibility = min(maxFramesAccumulated, historyVisibility / max(1.0f - historyVisibility, 0.0001f));
                float newNumFramesAccumulated = min(min(previousNumFramesAccumulated + 1.0f, maxFramesAccumulated), maxFramesFromVisibility);
                float historyWeight = min(newNumFramesAccumulated / (1.0f + newNumFramesAccumulated), saturate(_BurtGIScreenProbeTemporalParams.x));
                numFramesAccumulatedNormalized = saturate(newNumFramesAccumulated / maxFramesAccumulated);
                current.Radiance = lerp(current.Radiance, historyRadiance, historyWeight);
                current.Irradiance = lerp(current.Irradiance, historyIrradiance, historyWeight);
                current.Confidence.rgb = lerp(current.Confidence.rgb, historyConfidenceRGB, historyWeight);
                current.Confidence.a = rawDepth;
                return current;
            }

            BurtGIScreenProbeTemporalFilterOutput FragScreenProbeLiteTemporalFilter(Varyings input)
            {
                BurtGIScreenProbeOutput current;
                current.Radiance = tex2D(_BurtGIScreenProbeRadianceTexture, input.ScreenUV);
                current.Irradiance = tex2D(_BurtGIScreenProbeIrradianceTexture, input.ScreenUV);
                current.Confidence = tex2D(_BurtGIScreenProbeConfidenceTexture, input.ScreenUV);
                current.HitDistance = 0.0f;
                float numFramesAccumulatedNormalized = 0.0f;

                float rawDepth = current.Confidence.a;
                if (!BurtGIIsSkyDepth(rawDepth) && current.Confidence.r > 0.0001f)
                {
                    float2 sourceUV = BurtGIScreenProbeSourceUV(input.ScreenUV);
                    current = BurtGIApplyScreenProbeTemporal(sourceUV, rawDepth, current, numFramesAccumulatedNormalized);
                }

                BurtGIScreenProbeTemporalFilterOutput output;
                output.Radiance = current.Radiance;
                output.Irradiance = current.Irradiance;
                output.Confidence = current.Confidence;
                output.NumFramesAccumulated = numFramesAccumulatedNormalized;
                return output;
            }

            BurtGIScreenProbeOutput FragScreenProbeLite(Varyings input)
            {
                BurtGIScreenProbeOutput output;
                output.Radiance = 0.0f;
                output.Irradiance = 0.0f;
                output.Confidence = 0.0f;
                output.HitDistance = 0.0f;

                float2 sourceUV = BurtGIScreenProbeSourceUV(input.ScreenUV);
                float rawDepth;
                float3 placementNormalWS;
                float3 placementPositionWS;
                bool hasPlacement = BurtGISampleScreenProbePlacement(input.ScreenUV, rawDepth, placementNormalWS, placementPositionWS);
                rawDepth = hasPlacement ? rawDepth : BurtSampleDeferredRawDepth(sourceUV);
                if (BurtGIIsSkyDepth(rawDepth))
                {
                    return output;
                }

                float3 centerNormalWS = hasPlacement ? placementNormalWS : BurtGISampleNormalWS(sourceUV);
                float centerEdgeFactor = BurtGIEdgeFactor(sourceUV, rawDepth, centerNormalWS);
                float4 centerGI = tex2D(_BurtScreenSpaceGlobalIlluminationRawTexture, sourceUV);
                float3 radianceSum = max(centerGI.rgb, 0.0f) * 4.0f;
                float hitSum = saturate(centerGI.a) * 4.0f;
                float hitDistanceSum = 0.0f;
                float weightSum = 4.0f;
                float2 probeFootprint = _BurtGISourceTexelSize.xy * max(_BurtGIScreenProbeParams.x, 1.0f);
                int sampleBudget = min((int)_BurtGIScreenProbeParams.z, 8);

                [unroll(8)]
                for (int i = 0; i < 8; ++i)
                {
                    if (i >= sampleBudget)
                    {
                        continue;
                    }

                    float2 sampleUV = saturate(sourceUV + BurtGIScreenProbeDirection(i) * probeFootprint);
                    float sampleRawDepth = BurtSampleDeferredRawDepth(sampleUV);
                    if (BurtGIIsSkyDepth(sampleRawDepth))
                    {
                        continue;
                    }

                    float depthDelta = abs(LinearEyeDepth(sampleRawDepth) - LinearEyeDepth(rawDepth));
                    float depthWeight = exp(-depthDelta * lerp(0.08f, 0.32f, saturate(_BurtGIParams4.x)));
                    float normalWeight = pow(saturate(dot(centerNormalWS, BurtGISampleNormalWS(sampleUV))), lerp(8.0f, 18.0f, saturate(_BurtGIParams4.z)));
                    float weight = depthWeight * normalWeight;
                    float4 sampleGI = tex2D(_BurtScreenSpaceGlobalIlluminationRawTexture, sampleUV);
                    radianceSum += max(sampleGI.rgb, 0.0f) * weight;
                    hitSum += saturate(sampleGI.a) * weight;
                    hitDistanceSum += depthDelta * weight;
                    weightSum += weight;
                }

                float3 radiance = radianceSum / max(weightSum, 0.0001f);
                float hitRatio = saturate(hitSum / max(weightSum, 0.0001f));
                float averageHitDistance = hitDistanceSum / max(weightSum, 0.0001f);
                float encodedHitDistance = BurtGIEncodeScreenProbeHitDistanceForFiltering(averageHitDistance);
                float edgeGate = 1.0f - smoothstep(0.42f, 0.88f, centerEdgeFactor);
                float confidence = saturate(hitRatio * edgeGate);
                float3 irradiance = BurtGIClampRadiance(radiance * lerp(0.82f, 1.18f, confidence));

                output.Radiance = float4(radiance, hitRatio);
                output.Irradiance = float4(irradiance, confidence);
                output.Confidence = float4(confidence, hitRatio, centerEdgeFactor, rawDepth);
                output.HitDistance = float4(encodedHitDistance, averageHitDistance, hitRatio, confidence);
                return output;
            }

            float4 FragBlur(Varyings input) : SV_Target
            {
                float2 screenUV = input.ScreenUV;
                float4 center = tex2D(_BurtScreenSpaceGlobalIlluminationRawTexture, screenUV);
                if (_BurtGIParams1.z < 0.5f)
                {
                    return BurtGIApplyScreenProbeLite(screenUV, center);
                }

                float centerRawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtGIIsSkyDepth(centerRawDepth))
                {
                    return 0.0f;
                }

                float centerLinearDepth = LinearEyeDepth(centerRawDepth);
                float3 centerNormalWS = BurtGISampleNormalWS(screenUV);
                float centerEdgeFactor = BurtGIEdgeFactor(screenUV, centerRawDepth, centerNormalWS);
                float2 texel = _BurtGISourceTexelSize.xy;
                float sharpness = lerp(0.04f, 0.45f, saturate(_BurtGIParams1.w));
                float spatialRadius = max(_BurtGIParams3.x, 0.5f);
                float spatialStrength = saturate(_BurtGIParams3.y);
                float leakGuard = saturate(_BurtGIParams4.x);
                float centerWeight = lerp(4.0f, 6.0f, spatialStrength);
                float3 colorSum = center.rgb * centerWeight;
                float colorWeightSum = centerWeight;
                float hitSum = saturate(center.a) * centerWeight;
                float hitWeightSum = centerWeight;

                [unroll]
                for (int i = 0; i < 16; ++i)
                {
                    float2 direction = 0.0f;
                    if (i == 0) direction = float2(1.0f, 0.0f);
                    if (i == 1) direction = float2(-1.0f, 0.0f);
                    if (i == 2) direction = float2(0.0f, 1.0f);
                    if (i == 3) direction = float2(0.0f, -1.0f);
                    if (i == 4) direction = float2(1.0f, 1.0f);
                    if (i == 5) direction = float2(-1.0f, 1.0f);
                    if (i == 6) direction = float2(1.0f, -1.0f);
                    if (i == 7) direction = float2(-1.0f, -1.0f);
                    if (i == 8) direction = float2(2.0f, 0.0f);
                    if (i == 9) direction = float2(-2.0f, 0.0f);
                    if (i == 10) direction = float2(0.0f, 2.0f);
                    if (i == 11) direction = float2(0.0f, -2.0f);
                    if (i == 12) direction = float2(2.0f, 1.0f);
                    if (i == 13) direction = float2(-2.0f, 1.0f);
                    if (i == 14) direction = float2(1.0f, 2.0f);
                    if (i == 15) direction = float2(1.0f, -2.0f);

                    float2 sampleUV = saturate(screenUV + direction * texel * spatialRadius);
                    float sampleRawDepth = BurtSampleDeferredRawDepth(sampleUV);
                    if (BurtGIIsSkyDepth(sampleRawDepth))
                    {
                        continue;
                    }

                    float depthDelta = abs(LinearEyeDepth(sampleRawDepth) - centerLinearDepth);
                    float edgeSharpness = lerp(sharpness, sharpness * 3.5f, leakGuard * centerEdgeFactor);
                    float depthWeight = exp(-depthDelta * edgeSharpness / max(spatialRadius, 0.5f));
                    float3 sampleNormalWS = BurtGISampleNormalWS(sampleUV);
                    float normalDot = saturate(dot(centerNormalWS, sampleNormalWS));
                    float normalPower = lerp(10.0f, 18.0f, spatialStrength) * lerp(1.0f, 1.8f, saturate(_BurtGIParams4.z) * centerEdgeFactor);
                    float normalWeight = pow(normalDot, normalPower);
                    float edgeCrossWeight = lerp(1.0f, smoothstep(0.35f, 1.0f, normalDot), leakGuard * centerEdgeFactor);
                    float ringWeight = i < 4 ? 1.0f : (i < 8 ? 0.7f : 0.42f);
                    float4 sampleBurtGI = tex2D(_BurtScreenSpaceGlobalIlluminationRawTexture, sampleUV);
                    float sampleHitRatio = saturate(sampleBurtGI.a);
                    float hitWeight = lerp(0.55f, 1.0f, sampleHitRatio);
                    float hitDeltaWeight = lerp(1.0f, 1.0f - smoothstep(0.25f, 0.85f, abs(sampleHitRatio - saturate(center.a))), leakGuard * centerEdgeFactor);
                    float baseWeight = depthWeight * normalWeight * edgeCrossWeight * ringWeight * spatialStrength;
                    float colorWeight = baseWeight * hitWeight * hitDeltaWeight;
                    float hitFilterWeight = baseWeight * lerp(1.0f, hitDeltaWeight, leakGuard * centerEdgeFactor * 0.65f);
                    colorSum += sampleBurtGI.rgb * colorWeight;
                    colorWeightSum += colorWeight;
                    hitSum += sampleHitRatio * hitFilterWeight;
                    hitWeightSum += hitFilterWeight;
                }

                float4 filtered = float4(colorSum / max(colorWeightSum, 0.0001f), hitSum / max(hitWeightSum, 0.0001f));
                filtered = lerp(center, filtered, spatialStrength);
                return BurtGIApplyScreenProbeLite(screenUV, filtered);
            }

            float3 BurtGIRoughSpecularMaterialScale(BurtPBRMaterialData materialData, BurtPBRGeometryData geometryData, float roughness);
            float BurtGIRoughSpecularDiffuseLerp(float roughness, bool hasBackfaceDiffuse);
            float BurtGICombineRoughSpecularReflectionsAlpha(float roughness, bool hasBackfaceDiffuse);

            struct BurtGIIrradianceFieldOutput
            {
                float4 Diffuse : SV_Target0;
                float4 BackfaceDiffuse : SV_Target1;
                float4 RoughSpecular : SV_Target2;
            };

            BurtGIIrradianceFieldOutput BurtGIEmptyIrradianceFieldOutput()
            {
                BurtGIIrradianceFieldOutput output;
                output.Diffuse = 0.0f;
                output.BackfaceDiffuse = 0.0f;
                output.RoughSpecular = 0.0f;
                return output;
            }

            BurtGIIrradianceFieldOutput FragIrradianceFieldIntegrate(Varyings input)
            {
                BurtGIIrradianceFieldOutput output = BurtGIEmptyIrradianceFieldOutput();
                float2 screenUV = input.ScreenUV;
                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtGIIsSkyDepth(rawDepth))
                {
                    return output;
                }

                BurtEncodedGBuffer encodedGBuffer = BurtSampleEncodedGBuffer(screenUV);
                BurtGBufferData gbufferData = BurtDecodeDeferredGBuffer(encodedGBuffer, screenUV);
                float3 normalWS = BurtGetDeferredSurfaceNormalWS(gbufferData);
                float3 positionWS = BurtReconstructDeferredPositionWS(screenUV, rawDepth);
                float3 viewDirectionWS = BurtSafeNormalize(_WorldSpaceCameraPos.xyz - positionWS);
                float3 occlusionPositionWS = positionWS +
                    normalWS * max(_BurtGIIrradianceFieldParams.y, 0.0f) +
                    viewDirectionWS * max(_BurtGIIrradianceFieldParams.z, 0.0f);
                float radianceCacheDither = BurtGIHash12(screenUV * _BurtGISourceTexelSize.zw + 41.0f, _BurtGIParams2.x + 29.0f);
                float3 irradiance;
                float confidence;
                if (!BurtGISampleRadianceCacheClipMapIrradianceInterpolated(positionWS, occlusionPositionWS, normalWS, radianceCacheDither, irradiance, confidence))
                {
                    return output;
                }

                float outputStrength = max(_BurtGIIrradianceFieldParams.x, 0.0f);
                float3 indirectDiffuse = max(irradiance, 0.0f) * BURT_INV_PI * outputStrength;
                output.Diffuse = float4(max(indirectDiffuse, 0.0f), 0.0f);

                return output;
            }

            float4 FragTemporal(Varyings input) : SV_Target
            {
                float2 screenUV = input.ScreenUV;
                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                float4 centerBurtGI;
                float3 minBurtGI;
                float3 maxBurtGI;
                float3 averageBurtGI;
                float3 sigmaBurtGI;
                BurtGISampleTemporalNeighborhoodStats(screenUV, centerBurtGI, minBurtGI, maxBurtGI, averageBurtGI, sigmaBurtGI);

                if (_BurtGITemporalParams.y < 0.5f || BurtGIIsSkyDepth(rawDepth))
                {
                    return centerBurtGI;
                }

                float3 positionWS = BurtReconstructDeferredPositionWS(screenUV, rawDepth);
                float2 historyUV;
                float projectedRawDepth;
                if (!BurtGIProjectHistoryUV(positionWS, historyUV, projectedRawDepth))
                {
                    return centerBurtGI;
                }

                float4 historyDepthNormal = BurtGISampleHistoryDepthNormalClosest(historyUV, projectedRawDepth);
                float historyRawDepth = historyDepthNormal.r;
                if (BurtGIIsSkyDepth(historyRawDepth))
                {
                    return centerBurtGI;
                }

                float projectedLinearDepth = LinearEyeDepth(projectedRawDepth);
                float historyLinearDepth = LinearEyeDepth(historyRawDepth);
                float depthTolerance = max(projectedLinearDepth * max(_BurtGITemporalParams.z, 0.0001f), 0.025f);
                float depthValidity = saturate(1.0f - abs(historyLinearDepth - projectedLinearDepth) / depthTolerance);
                if (depthValidity <= 0.0001f)
                {
                    return centerBurtGI;
                }

                float3 currentNormalWS = BurtGISampleNormalWS(screenUV);
                float edgeFactor = BurtGIEdgeFactor(screenUV, rawDepth, currentNormalWS);
                float3 historyNormalWS = BurtDecodeNormalWSFromGBuffer(historyDepthNormal.gb);
                float normalThreshold = saturate(_BurtGITemporalParams.w);
                float normalValidity = saturate((saturate(dot(currentNormalWS, historyNormalWS)) - normalThreshold) / max(1.0f - normalThreshold, 0.0001f));
                if (normalValidity <= 0.0001f)
                {
                    return centerBurtGI;
                }

                float3 historyBurtGI = max(tex2D(_BurtGIHistoryTexture, historyUV).rgb * max(_BurtGIHistoryParams.x, 0.0f), 0.0f);
                float3 localRange = max(maxBurtGI - minBurtGI, 0.001f);
                float clampPadScale = max(_BurtGITemporalParams1.x, 0.0f);
                float varianceClampScale = max(_BurtGITemporalParams1.y, 0.0f);
                float3 varianceMin = averageBurtGI - sigmaBurtGI * varianceClampScale;
                float3 varianceMax = averageBurtGI + sigmaBurtGI * varianceClampScale;
                float3 historyClampMin = max(minBurtGI - localRange * clampPadScale, varianceMin);
                float3 historyClampMax = min(maxBurtGI + localRange * clampPadScale, varianceMax);
                float3 historyClampCenter = 0.5f * (historyClampMin + historyClampMax);
                float3 historyClampHalfRange = max(0.5f * abs(historyClampMax - historyClampMin), 0.0005f);
                historyClampMin = historyClampCenter - historyClampHalfRange;
                historyClampMax = historyClampCenter + historyClampHalfRange;
                float3 clampedHistory = clamp(historyBurtGI, historyClampMin, historyClampMax);
                float3 historyDelta = abs(historyBurtGI - centerBurtGI.rgb);
                float historyConsistency = saturate(1.0f - dot(historyDelta, float3(0.2126f, 0.7152f, 0.0722f)) / max(dot(localRange, float3(0.2126f, 0.7152f, 0.0722f)), 0.02f));
                historyConsistency = lerp(0.65f, 1.0f, historyConsistency);
                float historyHitRatio = saturate(tex2D(_BurtGIHistoryTexture, historyUV).a);
                float hitDelta = abs(historyHitRatio - saturate(centerBurtGI.a));
                float hitValidity = lerp(0.7f, 1.0f, saturate(1.0f - hitDelta / max(_BurtGITemporalParams1.z, 0.001f)));
                float edgeValidity = lerp(1.0f, 1.0f - edgeFactor, saturate(_BurtGIParams4.y) * saturate(_BurtGIParams4.x));
                float spatialCurrentBlend = lerp(0.18f, 0.35f, saturate(_BurtGITemporalParams1.w));
                float feedback = saturate(_BurtGITemporalParams.x) * depthValidity * normalValidity * hitValidity * edgeValidity * historyConsistency;
                float3 currentBurtGI = lerp(centerBurtGI.rgb, averageBurtGI, spatialCurrentBlend);
                float3 resolvedBurtGI = lerp(currentBurtGI, clampedHistory, feedback);
                float resolvedHitRatio = lerp(centerBurtGI.a, historyHitRatio, feedback);
                return float4(max(resolvedBurtGI, 0.0f), saturate(resolvedHitRatio));
            }

            float4 FragCopyDepthNormal(Varyings input) : SV_Target
            {
                float2 screenUV = input.ScreenUV;
                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                float2 encodedNormal = BurtEncodeNormalWSForGBuffer(BurtGISampleNormalWS(screenUV));
                return float4(rawDepth, encodedNormal, 1.0f);
            }

            float4 FragCopyTemporalFinal(Varyings input) : SV_Target
            {
                return tex2D(_BurtGITemporalFinalTexture, input.ScreenUV);
            }

            float4 FragTemporalDiagnostics(Varyings input) : SV_Target
            {
                float2 screenUV = input.ScreenUV;
                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                float4 centerBurtGI;
                float3 minBurtGI;
                float3 maxBurtGI;
                float3 averageBurtGI;
                float3 sigmaBurtGI;
                BurtGISampleTemporalNeighborhoodStats(screenUV, centerBurtGI, minBurtGI, maxBurtGI, averageBurtGI, sigmaBurtGI);

                if (_BurtGITemporalParams.y < 0.5f || BurtGIIsSkyDepth(rawDepth))
                {
                    return float4(0.0f, 1.0f, 0.0f, 1.0f);
                }

                float3 positionWS = BurtReconstructDeferredPositionWS(screenUV, rawDepth);
                float2 historyUV;
                float projectedRawDepth;
                if (!BurtGIProjectHistoryUV(positionWS, historyUV, projectedRawDepth))
                {
                    return float4(0.0f, 1.0f, 1.0f, 1.0f);
                }

                float4 historyDepthNormal = BurtGISampleHistoryDepthNormalClosest(historyUV, projectedRawDepth);
                float historyRawDepth = historyDepthNormal.r;
                if (BurtGIIsSkyDepth(historyRawDepth))
                {
                    return float4(0.0f, 1.0f, 0.75f, 1.0f);
                }

                float projectedLinearDepth = LinearEyeDepth(projectedRawDepth);
                float historyLinearDepth = LinearEyeDepth(historyRawDepth);
                float depthTolerance = max(projectedLinearDepth * max(_BurtGITemporalParams.z, 0.0001f), 0.025f);
                float depthValidity = saturate(1.0f - abs(historyLinearDepth - projectedLinearDepth) / depthTolerance);
                float3 currentNormalWS = BurtGISampleNormalWS(screenUV);
                float edgeFactor = BurtGIEdgeFactor(screenUV, rawDepth, currentNormalWS);
                float3 historyNormalWS = BurtDecodeNormalWSFromGBuffer(historyDepthNormal.gb);
                float normalThreshold = saturate(_BurtGITemporalParams.w);
                float normalValidity = saturate((saturate(dot(currentNormalWS, historyNormalWS)) - normalThreshold) / max(1.0f - normalThreshold, 0.0001f));

                float3 historyBurtGI = max(tex2D(_BurtGIHistoryTexture, historyUV).rgb * max(_BurtGIHistoryParams.x, 0.0f), 0.0f);
                float historyHitRatio = saturate(tex2D(_BurtGIHistoryTexture, historyUV).a);
                float3 localRange = max(maxBurtGI - minBurtGI, 0.001f);
                float3 historyDelta = abs(historyBurtGI - centerBurtGI.rgb);
                float historyConsistency = saturate(1.0f - dot(historyDelta, float3(0.2126f, 0.7152f, 0.0722f)) / max(dot(localRange, float3(0.2126f, 0.7152f, 0.0722f)), 0.02f));
                historyConsistency = lerp(0.65f, 1.0f, historyConsistency);
                float varianceClampScale = max(_BurtGITemporalParams1.y, 0.0f);
                float3 varianceMin = averageBurtGI - sigmaBurtGI * varianceClampScale;
                float3 varianceMax = averageBurtGI + sigmaBurtGI * varianceClampScale;
                float3 varianceOverflow = max(max(historyBurtGI - varianceMax, varianceMin - historyBurtGI), 0.0f);
                float varianceValidity = saturate(1.0f - dot(varianceOverflow, float3(0.2126f, 0.7152f, 0.0722f)) / max(dot(localRange, float3(0.2126f, 0.7152f, 0.0722f)), 0.02f));
                varianceValidity = lerp(0.75f, 1.0f, varianceValidity);
                float hitDelta = abs(historyHitRatio - saturate(centerBurtGI.a));
                float hitValidity = lerp(0.7f, 1.0f, saturate(1.0f - hitDelta / max(_BurtGITemporalParams1.z, 0.001f)));
                float edgeValidity = lerp(1.0f, 1.0f - edgeFactor, saturate(_BurtGIParams4.y) * saturate(_BurtGIParams4.x));
                float confidence = saturate(depthValidity * normalValidity * historyConsistency * varianceValidity * hitValidity * edgeValidity * saturate(_BurtGITemporalParams.x));
                float rejection = 1.0f - confidence;
                return float4(confidence, rejection, saturate(centerBurtGI.a), 1.0f);
            }

            float4 FragComposite(Varyings input) : SV_Target
            {
                float2 screenUV = input.ScreenUV;
                float4 cameraColor = tex2D(_BurtGICameraColorCopyTexture, screenUV);
                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtGIIsSkyDepth(rawDepth))
                {
                    return cameraColor;
                }

                float3 burtGI = tex2D(_BurtScreenSpaceGlobalIlluminationTexture, screenUV).rgb;
                float3 appliedGI = burtGI * _BurtGIParams1.x;
                cameraColor.rgb += appliedGI;
                if (BurtIsSubsurfaceShadingModel(BurtSampleDeferredShadingModelID(screenUV)))
                {
                    cameraColor.a += dot(max(appliedGI, 0.0f), float3(0.3f, 0.59f, 0.11f));
                }

                return cameraColor;
            }

            struct BurtGIIndirectChannelsOutput
            {
                float4 BackfaceDiffuse : SV_Target0;
                float4 RoughSpecular : SV_Target1;
            };

            float BurtGIBackfaceMaterialWeight(BurtGBufferData gbufferData)
            {
                float subsurfaceWeight = BurtIsActiveSubsurfaceShadingModel(gbufferData.ShadingModelID)
                    ? saturate(max(BurtGetSubsurfaceStrength(gbufferData), max(BurtGetSubsurfaceThickness(gbufferData), BurtGetSubsurfaceAmbient(gbufferData))))
                    : 0.0f;
                bool isFoliageMaterial = BurtIsActiveFoliageShadingModel(gbufferData.ShadingModelID);
                bool isGrassMaterial = isFoliageMaterial && BurtGetFoliageIsGrass(gbufferData) > 0.5f;
                float foliageWeight = isFoliageMaterial && !isGrassMaterial
                    ? saturate(max(BurtGetFoliageTransmissionWeight(gbufferData), max(BurtGetFoliageThickness(gbufferData), BurtGetFoliageBackLight(gbufferData))))
                    : 0.0f;
                return saturate(max(subsurfaceWeight, foliageWeight));
            }

            float3 BurtGIRoughSpecularMaterialScale(BurtPBRMaterialData materialData, BurtPBRGeometryData geometryData, float roughness)
            {
                float2 dfg = GetSpecularDFGTerms(roughness, geometryData.NDotV);
                float3 envBRDF = EvalSpecularDFG(materialData.F0, materialData.F90, dfg);
                float3 energyCompensation = GetSpecularEnergyCompensation(materialData.F0, roughness, geometryData.NDotV);
                float specularOcclusion = GetIndirectSpecularOcclusion(geometryData.NDotV, materialData.Occlusion, roughness);
                return max(envBRDF * energyCompensation * specularOcclusion, float3(0.0f, 0.0f, 0.0f));
            }

            float BurtGIRoughSpecularDiffuseLerp(float roughness, bool hasBackfaceDiffuse)
            {
                float maxRoughnessToEvaluate = saturate(_BurtGIIndirectChannelParams.w);
                const float maxRoughnessToEvaluateFoliage = 0.4f;
                const float fadeLength = 0.2f;
                float maxRoughness = hasBackfaceDiffuse ? maxRoughnessToEvaluateFoliage : maxRoughnessToEvaluate;
                return saturate((roughness - maxRoughness + fadeLength) / fadeLength);
            }

            float BurtGICombineRoughSpecularReflectionsAlpha(float roughness, bool hasBackfaceDiffuse)
            {
                const float maxRoughnessToTrace = -0.1f;
                const float maxRoughnessToTraceFoliage = 0.4f;
                const float invRoughnessFadeLength = 10.0f;
                float maxRoughness = hasBackfaceDiffuse ? maxRoughnessToTraceFoliage : maxRoughnessToTrace;
                return saturate((maxRoughness - roughness) * invRoughnessFadeLength);
            }

            float BurtGISampleLocalTraceHitConfidence(float2 screenUV, float3 lightingDirectionWS, float roughness)
            {
                float octResolution = max(_BurtGIScreenProbeTraceParams.x, 1.0f);
                float2 gridSize = max(_BurtGIScreenProbeGridParams.xy, 1.0f);
                if (octResolution < 2.0f || gridSize.x < 1.0f || gridSize.y < 1.0f)
                {
                    return 0.0f;
                }

                float2 probeCoord = BurtGIScreenProbeFallbackCoord(screenUV, gridSize);
                float2 centerOctUV = BurtGIInverseEquiAreaSphericalMapping(BurtSafeNormalize(lightingDirectionWS));
                float2 centerOctCoord = centerOctUV * octResolution - 0.5f;
                float radiusFloat = lerp(1.0f, 2.0f, saturate((roughness - 0.25f) * 2.0f));
                int radius = (int)round(radiusFloat);
                float hitSum = 0.0f;
                float weightSum = 0.0f;

                [unroll]
                for (int y = -2; y <= 2; ++y)
                {
                    [unroll]
                    for (int x = -2; x <= 2; ++x)
                    {
                        if (abs(x) > radius || abs(y) > radius)
                        {
                            continue;
                        }

                        float2 octCoord = clamp(floor(centerOctCoord) + float2((float)x, (float)y), 0.0f, octResolution - 1.0f);
                        float3 sampleDirectionWS;
                        float traceSampleWeight;
                        BurtGIResolveScreenProbeImportanceTraceSample(
                            probeCoord,
                            octCoord,
                            octResolution,
                            sampleDirectionWS,
                            traceSampleWeight);
                        float angularWeight = pow(saturate(dot(BurtSafeNormalize(lightingDirectionWS), sampleDirectionWS)), lerp(48.0f, 8.0f, saturate(roughness)));
                        float2 octDelta = abs(octCoord - centerOctCoord) / max(radiusFloat, 1.0f);
                        float weight = traceSampleWeight * angularWeight * saturate(1.0f - dot(octDelta, octDelta) * 0.45f);
                        float2 atlasUV = BurtGITraceAtlasUV(probeCoord, octCoord, octResolution);
                        hitSum += saturate(tex2D(_BurtGIScreenProbeTraceHitTexture, atlasUV).r) * weight;
                        weightSum += weight;
                    }
                }

                return saturate(hitSum / max(weightSum, 0.0001f));
            }

            float4 BurtGISampleScreenProbeHitDistanceConfidence(float2 screenUV)
            {
                float4 hitDistance = tex2D(_BurtGIScreenProbeHitDistanceTexture, screenUV);
                float encodedDistanceConfidence = 1.0f - smoothstep(0.55f, 0.98f, saturate(hitDistance.r));
                float linearDistanceConfidence = 1.0f - saturate(hitDistance.g / max(_BurtGIScreenProbeParams.y, 0.001f));
                float hitConfidence = saturate(max(hitDistance.b, hitDistance.a) * lerp(encodedDistanceConfidence, linearDistanceConfidence, 0.35f));
                return float4(saturate(hitDistance.r), max(hitDistance.g, 0.0f), saturate(hitDistance.b), hitConfidence);
            }

            float BurtGIIndirectChannelHistoryFeedback(float2 screenUV, float rawDepth, float3 normalWS, out float2 historyUV)
            {
                historyUV = screenUV;
                if (_BurtGITemporalParams.y < 0.5f || BurtGIIsSkyDepth(rawDepth))
                {
                    return 0.0f;
                }

                float3 positionWS = BurtReconstructDeferredPositionWS(screenUV, rawDepth);
                float projectedRawDepth;
                if (!BurtGIResolveScreenProbeHistoryUV(screenUV, positionWS, historyUV, projectedRawDepth))
                {
                    return 0.0f;
                }

                float4 historyDepthNormal = BurtGISampleHistoryDepthNormalClosest(historyUV, projectedRawDepth);
                float historyRawDepth = historyDepthNormal.r;
                if (BurtGIIsSkyDepth(historyRawDepth))
                {
                    return 0.0f;
                }

                float projectedLinearDepth = LinearEyeDepth(projectedRawDepth);
                float historyLinearDepth = LinearEyeDepth(historyRawDepth);
                float depthTolerance = max(projectedLinearDepth * max(_BurtGITemporalParams.z, 0.0001f), 0.025f);
                float depthValidity = saturate(1.0f - abs(historyLinearDepth - projectedLinearDepth) / depthTolerance);
                float3 historyNormalWS = BurtDecodeNormalWSFromGBuffer(historyDepthNormal.gb);
                float normalThreshold = saturate(_BurtGITemporalParams.w);
                float normalValidity = saturate((saturate(dot(normalWS, historyNormalWS)) - normalThreshold) / max(1.0f - normalThreshold, 0.0001f));
                float edgeFactor = BurtGIEdgeFactor(screenUV, rawDepth, normalWS);
                float edgeValidity = lerp(1.0f, 1.0f - edgeFactor, saturate(_BurtGIParams4.y) * saturate(_BurtGIParams4.x));
                return saturate(_BurtGITemporalParams.x) * depthValidity * normalValidity * edgeValidity;
            }

            float4 BurtGIApplyIndirectChannelHistory(float4 historyValue, float4 currentValue, float baseFeedback)
            {
                if (baseFeedback <= 0.0001f)
                {
                    return currentValue;
                }

                float hitDelta = abs(saturate(historyValue.a) - saturate(currentValue.a));
                float hitValidity = lerp(0.65f, 1.0f, saturate(1.0f - hitDelta / max(_BurtGITemporalParams1.z, 0.001f)));
                float3 currentColor = max(currentValue.rgb, 0.0f);
                float3 historyColor = max(historyValue.rgb * max(_BurtGIIndirectChannelParams.z, 0.0f), 0.0f);
                float3 currentTonemapped = BurtGITonemapLighting(currentColor);
                float3 historyTonemapped = BurtGITonemapLighting(historyColor);
                float currentLightingValid = (saturate(currentValue.a) > 0.0001f || any(currentColor > 0.0001f)) ? 1.0f : 0.0f;
                float historyLightingValid = (saturate(historyValue.a) > 0.0001f || any(historyColor > 0.0001f)) ? 1.0f : 0.0f;
                float3 clampRange = max(currentTonemapped * 0.85f, float3(0.035f, 0.035f, 0.035f));
                float3 clampedHistory = currentLightingValid > 0.5f
                    ? clamp(historyTonemapped, max(currentTonemapped - clampRange, 0.0f), currentTonemapped + clampRange)
                    : historyTonemapped;
                float feedback = baseFeedback * hitValidity;
                feedback = currentLightingValid > 0.5f ? feedback : max(feedback, historyLightingValid * saturate(_BurtGITemporalParams.x));
                float3 blendedColor = BurtGIInverseTonemapLighting(lerp(currentTonemapped, clampedHistory, feedback));
                return float4(max(blendedColor, 0.0f), lerp(saturate(currentValue.a), saturate(historyValue.a), feedback));
            }

            BurtGIIndirectChannelsOutput FragResolveIndirectChannels(Varyings input)
            {
                BurtGIIndirectChannelsOutput output;
                output.BackfaceDiffuse = 0.0f;
                output.RoughSpecular = 0.0f;

                float2 screenUV = input.ScreenUV;
                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtGIIsSkyDepth(rawDepth))
                {
                    return output;
                }

                bool backfaceChannelEnabled = _BurtGIIndirectChannelParams.x > 0.5f;
                bool roughSpecularChannelEnabled = _BurtGIIndirectChannelParams.y > 0.5f;
                if (!backfaceChannelEnabled && !roughSpecularChannelEnabled)
                {
                    return output;
                }

                BurtEncodedGBuffer encodedGBuffer = BurtSampleEncodedGBuffer(screenUV);
                BurtGBufferData gbufferData = BurtDecodeDeferredGBuffer(encodedGBuffer, screenUV);
                BurtPBRMaterialData materialData = BurtPreparePBRMaterialData(gbufferData);
                float3 normalWS = BurtGetDeferredSurfaceNormalWS(gbufferData);
                float3 unitBentNormalWS;
                float bentAO;
                float bentNormalValid;
                BurtGISampleScreenProbeBentNormalAO(screenUV, normalWS, unitBentNormalWS, bentAO, bentNormalValid);
                float4 finalBurtGI = tex2D(_BurtScreenSpaceGlobalIlluminationTexture, screenUV);
                float3 diffuseGI = max(finalBurtGI.rgb, 0.0f);
                float hitRatio = saturate(finalBurtGI.a);
                float3 positionWS = BurtReconstructDeferredPositionWS(screenUV, rawDepth);
                float sceneDepth = LinearEyeDepth(rawDepth);
                float3 viewDirectionWS = BurtSafeNormalize(_BurtDeferredCameraWorldPosition.xyz - positionWS);
                float4 screenProbeHitDistanceConfidence = BurtGISampleScreenProbeHitDistanceConfidence(screenUV);
                float receiverFoliageWeight = BurtIsActiveFoliageShadingModel(gbufferData.ShadingModelID) ? 1.0f : 0.0f;

                float backfaceWeight = BurtGIBackfaceMaterialWeight(gbufferData);
                if (backfaceChannelEnabled && backfaceWeight > 0.0001f)
                {
                    float screenProbeBackfaceConfidence;
                    float3 backfaceLightingNormalWS = -normalWS;
                    float backfaceTraceHit = BurtGISampleLocalTraceHitConfidence(screenUV, backfaceLightingNormalWS, 1.0f);
                    float3 screenProbeBackfaceIrradiance = BurtGIResolveScreenProbeDirectionalLightingLite(
                        screenUV,
                        positionWS,
                        sceneDepth,
                        normalWS,
                        backfaceLightingNormalWS,
                        0.0f,
                        receiverFoliageWeight,
                        screenProbeBackfaceConfidence);
                    float3 backfaceIrradiance = max(screenProbeBackfaceIrradiance, 0.0f);
                    float3 environmentBackfaceIrradiance = BurtSampleIndirectDiffuseIrradiance(backfaceLightingNormalWS);
                    float xgiBackfaceTraceConfidence = saturate(max(screenProbeBackfaceConfidence, max(backfaceTraceHit, screenProbeHitDistanceConfidence.w)));
                    float environmentFill = saturate((0.45f - xgiBackfaceTraceConfidence) * 2.2222223f);
                    backfaceIrradiance = lerp(backfaceIrradiance, max(environmentBackfaceIrradiance, 0.0f), environmentFill * 0.35f);
                    // XRender stores pre-material two-sided diffuse lighting in the backface channel.
                    // Thin-walled transmission color and material AO are applied by deferred lighting.
                    float3 twoSidedDiffuseLighting = backfaceIrradiance * BURT_INV_PI;
                    float backfaceBentVisibility = saturate(dot(-normalWS, unitBentNormalWS) * 0.5f + 0.5f);
                    float unweightedBackfaceBentOcclusion = bentNormalValid > 0.5f
                        ? lerp(1.0f, saturate(0.5f + backfaceBentVisibility * 0.5f) * saturate(0.4f + bentAO * 0.6f), saturate(1.0f - bentAO))
                        : 1.0f;
                    float backfaceBentOcclusion = BurtGIApplyShortRangeAOWeight(unweightedBackfaceBentOcclusion);
                    output.BackfaceDiffuse = float4(max(twoSidedDiffuseLighting, 0.0f) * backfaceBentOcclusion * saturate(0.35f + xgiBackfaceTraceConfidence * 0.65f), max(hitRatio, xgiBackfaceTraceConfidence));
                }

                float roughness = saturate(gbufferData.PerceptualRoughness);
                BurtPBRGeometryData geometryData = BurtPreparePBRGeometryData(gbufferData, viewDirectionWS);
                float3 roughSpecularMaterialScale = BurtGIRoughSpecularMaterialScale(materialData, geometryData, roughness);
                float roughSpecularMaterialWeight = max(max(roughSpecularMaterialScale.r, roughSpecularMaterialScale.g), roughSpecularMaterialScale.b);
                bool foliageRoughSpecularMaterial = BurtIsActiveSubsurfaceShadingModel(gbufferData.ShadingModelID) ||
                    BurtIsActiveFoliageShadingModel(gbufferData.ShadingModelID) ||
                    BurtIsActiveHairShadingModel(gbufferData.ShadingModelID);
                float roughSpecularDiffuseLerp = BurtGIRoughSpecularDiffuseLerp(roughness, foliageRoughSpecularMaterial);
                float roughSpecularTraceAlpha = BurtGICombineRoughSpecularReflectionsAlpha(roughness, foliageRoughSpecularMaterial);
                float clearCoatMask = saturate(BurtGetClearCoatMask(gbufferData));
                if (roughSpecularChannelEnabled && roughSpecularMaterialWeight > 0.0001f)
                {
                    // DiffuseIndirect already contains XRender's Fd_Lambert factor.
                    float3 roughSpecular = diffuseGI;
                    float roughBentOcclusion = 1.0f;
                    bool shouldSampleScreenProbeSpecular = (roughSpecularDiffuseLerp < 1.0f && roughSpecularTraceAlpha < 1.0f) || clearCoatMask > 0.0001f;
                    if (shouldSampleScreenProbeSpecular)
                    {
                        float3 roughSpecularNormalWS = normalWS;
                        float roughSpecularRoughness = max(roughness, 0.2f);
                        if (clearCoatMask > 0.0001f)
                        {
                            roughSpecularNormalWS = BurtGetDeferredSurfaceNormalWS(gbufferData);
                        }

                        float3 reflectionDirectionWS = reflect(-viewDirectionWS, roughSpecularNormalWS);
                        float screenProbeRoughSpecularConfidence;
                        float3 screenProbeSpecularDirectionWS = BurtGIAnchoredVisibleGGXSpecularDirection(roughSpecularNormalWS, viewDirectionWS, roughSpecularRoughness);
                        float roughSpecularTraceHit = BurtGISampleLocalTraceHitConfidence(screenUV, screenProbeSpecularDirectionWS, roughSpecularRoughness);
                        float3 screenProbeRoughSpecularLighting = BurtGIResolveScreenProbeDirectionalLightingLite(
                            screenUV,
                            positionWS,
                            sceneDepth,
                            roughSpecularNormalWS,
                            screenProbeSpecularDirectionWS,
                            roughSpecularRoughness,
                            receiverFoliageWeight,
                            screenProbeRoughSpecularConfidence) * BURT_INV_PI;
                        screenProbeRoughSpecularLighting = BurtGIInverseTonemapLighting(BurtGITonemapLighting(max(screenProbeRoughSpecularLighting, 0.0f)));
                        float3 environmentSpecularRadiance = SampleIndirectSpecularRadiance(reflectionDirectionWS, roughSpecularRoughness);
                        float xgiRoughSpecularTraceConfidence = saturate(max(screenProbeRoughSpecularConfidence, max(roughSpecularTraceHit, screenProbeHitDistanceConfidence.w)));
                        float3 specularRadiance = lerp(
                            environmentSpecularRadiance,
                            screenProbeRoughSpecularLighting,
                            saturate(xgiRoughSpecularTraceConfidence * lerp(0.45f, 0.9f, roughSpecularRoughness)));
                        float reflectionBentVisibility = saturate(dot(reflectionDirectionWS, unitBentNormalWS) * 0.5f + 0.5f);
                        float unweightedRoughBentOcclusion = bentNormalValid > 0.5f
                            ? lerp(1.0f, reflectionBentVisibility * saturate(0.25f + bentAO * 0.75f), saturate((1.0f - bentAO) * (1.0f - roughSpecularRoughness * 0.35f)))
                            : 1.0f;
                        roughBentOcclusion = BurtGIApplyShortRangeAOWeight(unweightedRoughBentOcclusion);
                        roughSpecular = lerp(specularRadiance, roughSpecular, roughSpecularDiffuseLerp);
                        roughSpecularTraceAlpha = max(roughSpecularTraceAlpha, xgiRoughSpecularTraceConfidence * saturate(1.0f - roughSpecularDiffuseLerp * 0.55f));
                    }

                    float roughSpecularHitConfidence = saturate(max(hitRatio, roughSpecularTraceAlpha));
                    output.RoughSpecular = float4(max(roughSpecular, 0.0f) * roughSpecularMaterialScale * roughBentOcclusion * saturate(0.45f + roughSpecularHitConfidence * 0.55f), roughSpecularHitConfidence);
                }

                float2 indirectHistoryUV;
                float indirectHistoryFeedback = BurtGIIndirectChannelHistoryFeedback(screenUV, rawDepth, normalWS, indirectHistoryUV);
                float4 historyBackfaceDiffuse = tex2D(_BurtGIBackfaceDiffuseIndirectHistoryTexture, indirectHistoryUV);
                float4 historyRoughSpecular = tex2D(_BurtGIRoughSpecularIndirectHistoryTexture, indirectHistoryUV);
                output.BackfaceDiffuse = backfaceChannelEnabled && backfaceWeight > 0.0001f ? BurtGIApplyIndirectChannelHistory(historyBackfaceDiffuse, output.BackfaceDiffuse, indirectHistoryFeedback) : 0.0f;
                output.RoughSpecular = roughSpecularChannelEnabled && roughSpecularMaterialWeight > 0.0001f ? BurtGIApplyIndirectChannelHistory(historyRoughSpecular, output.RoughSpecular, indirectHistoryFeedback) : 0.0f;

                return output;
            }

            float4 BurtGIDebugHitRatio(float4 finalBurtGI)
            {
                float hitRatio = saturate(finalBurtGI.a);
                return float4(1.0f - hitRatio, hitRatio, 0.0f, 1.0f);
            }

            void BurtGIComputeDebugValidity(
                float2 screenUV,
                float4 finalBurtGI,
                out float surfaceMask,
                out float hitRatio,
                out float geometryMask,
                out float surfaceValidity,
                out float edgeLeakRisk,
                out float skyFallbackRisk)
            {
                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtGIIsSkyDepth(rawDepth))
                {
                    surfaceMask = 0.0f;
                    hitRatio = 0.0f;
                    geometryMask = 0.0f;
                    surfaceValidity = 0.0f;
                    edgeLeakRisk = 0.0f;
                    skyFallbackRisk = saturate(_BurtGIParams1.y) * 0.08f;
                    return;
                }

                float3 normalWS = BurtGISampleNormalWS(screenUV);
                float edgeFactor = BurtGIEdgeFactor(screenUV, rawDepth, normalWS);
                surfaceMask = 1.0f;
                hitRatio = saturate(finalBurtGI.a);
                geometryMask = surfaceMask;
                surfaceValidity = surfaceMask * lerp(1.0f, 1.0f - edgeFactor, saturate(_BurtGIParams4.y) * saturate(_BurtGIParams4.x));
                skyFallbackRisk = saturate((1.0f - hitRatio) * _BurtGIParams1.y) * surfaceMask;
                edgeLeakRisk = saturate(edgeFactor * lerp(0.75f, 1.3f, saturate(_BurtGIParams4.x))) * surfaceMask;
            }

            float4 BurtGIDebugLeakGuard(float2 screenUV, float4 finalBurtGI)
            {
                float surfaceMask;
                float hitRatio;
                float geometryMask;
                float surfaceValidity;
                float edgeLeakRisk;
                float skyFallbackRisk;
                BurtGIComputeDebugValidity(screenUV, finalBurtGI, surfaceMask, hitRatio, geometryMask, surfaceValidity, edgeLeakRisk, skyFallbackRisk);
                float geometryContext = geometryMask * 0.025f;
                return float4(edgeLeakRisk, geometryContext, skyFallbackRisk, 1.0f);
            }

            float4 BurtGIDebugConfidence(float2 screenUV)
            {
                float surfaceMask;
                float hitRatio;
                float geometryMask;
                float surfaceValidity;
                float edgeLeakRisk;
                float skyFallbackRisk;

                float2 quadrantUV = frac(screenUV * 2.0f);
                float4 quadrantFinalBurtGI = tex2D(_BurtScreenSpaceGlobalIlluminationTexture, quadrantUV);
                BurtGIComputeDebugValidity(quadrantUV, quadrantFinalBurtGI, surfaceMask, hitRatio, geometryMask, surfaceValidity, edgeLeakRisk, skyFallbackRisk);

                if (screenUV.y >= 0.5f)
                {
                    return screenUV.x < 0.5f
                        ? BurtGIDebugHitRatio(quadrantFinalBurtGI)
                        : float4((1.0f - surfaceValidity) * surfaceMask, surfaceValidity, geometryMask * 0.25f, 1.0f);
                }

                return screenUV.x < 0.5f
                    ? float4(edgeLeakRisk, 0.0f, 0.0f, 1.0f)
                    : float4(0.0f, 0.0f, skyFallbackRisk, 1.0f);
            }

            float4 BurtGIDebugRadianceCacheSkyAO(float2 screenUV)
            {
                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtGIIsSkyDepth(rawDepth))
                {
                    return float4(0.0f, 0.0f, 0.0f, 1.0f);
                }

                float3 normalWS = BurtGISampleNormalWS(screenUV);
                float3 positionWS = BurtReconstructDeferredPositionWS(screenUV, rawDepth);
                float dither = BurtGIHash12(screenUV * _BurtGISourceTexelSize.zw + 67.0f, _BurtGIParams2.x + 13.0f);
                float skyAO;
                if (!BurtGISampleRadianceCacheClipMapSkyAOInterpolated(positionWS, positionWS, normalWS, dither, skyAO))
                {
                    return float4(1.0f, 0.0f, 1.0f, 1.0f);
                }

                return float4(skyAO.xxx, 1.0f);
            }

            bool BurtGIDebugResolveRadianceCacheProbe(
                float2 screenUV,
                out uint clipmapIndex,
                out uint3 probeCoord,
                out uint probeIndex,
                out float3 positionWS,
                out float3 normalWS)
            {
                clipmapIndex = 0u;
                probeCoord = uint3(0u, 0u, 0u);
                probeIndex = BURT_GI_RADIANCE_CACHE_INVALID_PROBE_INDEX;
                positionWS = float3(0.0f, 0.0f, 0.0f);
                normalWS = float3(0.0f, 1.0f, 0.0f);

                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtGIIsSkyDepth(rawDepth))
                {
                    return false;
                }

                normalWS = BurtGISampleNormalWS(screenUV);
                positionWS = BurtReconstructDeferredPositionWS(screenUV, rawDepth);
                float dither = BurtGIHash12(screenUV * _BurtGISourceTexelSize.zw + 113.0f, _BurtGIParams2.x + 31.0f);
                float3 probeCoordFloat = 0.0f;
                int debugClipmapIndex = (int)floor(_BurtGIRadianceCacheClipMapApplyParams.z + 0.5f);
                uint clipmapResolution = BurtGIRadianceCacheClipmapResolution();
                if (debugClipmapIndex >= 0)
                {
                    if (debugClipmapIndex >= (int)BurtGIRadianceCacheClipmapCount())
                    {
                        return false;
                    }

                    clipmapIndex = (uint)debugClipmapIndex;
                    float4 worldPositionToProbeCoord = _BurtGIRadianceCacheClipMapWorldPositionToProbeCoord[clipmapIndex];
                    probeCoordFloat = positionWS * worldPositionToProbeCoord.w + worldPositionToProbeCoord.xyz;
                    if (any(probeCoordFloat < 0.5f) || any(probeCoordFloat > ((float)clipmapResolution - 0.5f)))
                    {
                        return false;
                    }
                }
                else if (!BurtGIRadianceCacheWorldPositionToProbeCoordDithered(positionWS, dither, clipmapIndex, probeCoordFloat))
                {
                    return false;
                }

                int maxProbeCoord = (int)max(clipmapResolution, 1u) - 1;
                probeCoord = (uint3)clamp((int3)floor(probeCoordFloat + 0.5f), int3(0, 0, 0), int3(maxProbeCoord, maxProbeCoord, maxProbeCoord));
                probeIndex = BurtGIRadianceCacheProbeIndexFromIndirection(clipmapIndex, probeCoord);
                return probeIndex < BURT_GI_RADIANCE_CACHE_USED_PROBE_INDEX;
            }

            float4 BurtGIDebugRadianceCacheVisualize(float2 screenUV)
            {
                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtGIIsSkyDepth(rawDepth))
                {
                    return float4(0.0f, 0.0f, 0.0f, 1.0f);
                }

                uint clipmapIndex;
                uint3 probeCoord;
                uint probeIndex;
                float3 positionWS;
                float3 normalWS;
                if (!BurtGIDebugResolveRadianceCacheProbe(screenUV, clipmapIndex, probeCoord, probeIndex, positionWS, normalWS))
                {
                    return float4(1.0f, 0.0f, 1.0f, 1.0f);
                }

                float dither = BurtGIHash12(screenUV * _BurtGISourceTexelSize.zw + 149.0f, _BurtGIParams2.x + 47.0f);
                float3 irradiance = 0.0f;
                float confidence = 0.0f;
                float skyAO = 0.0f;
                int debugClipmapIndex = (int)floor(_BurtGIRadianceCacheClipMapApplyParams.z + 0.5f);
                if (debugClipmapIndex >= 0)
                {
                    irradiance = BurtGISampleRadianceCacheProbeIrradiance(probeIndex, normalWS);
                    skyAO = BurtGISampleRadianceCacheProbeSkyAO(probeIndex, normalWS);
                    confidence = probeIndex < BURT_GI_RADIANCE_CACHE_USED_PROBE_INDEX ? 1.0f : 0.0f;
                }
                else
                {
                    if (!BurtGISampleRadianceCacheClipMapIrradianceInterpolated(positionWS, positionWS, normalWS, dither, irradiance, confidence))
                    {
                        return float4(1.0f, 0.0f, 1.0f, 1.0f);
                    }

                    bool hasSkyAO = BurtGISampleRadianceCacheClipMapSkyAOInterpolated(positionWS, positionWS, normalWS, dither, skyAO);
                    skyAO = hasSkyAO ? skyAO : 0.0f;
                }

                float clipmapCount = max((float)BurtGIRadianceCacheClipmapCount() - 1.0f, 1.0f);
                float clipmapT = saturate((float)clipmapIndex / clipmapCount);
                float3 clipmapTint = lerp(float3(0.02f, 0.22f, 0.85f), float3(1.0f, 0.42f, 0.04f), clipmapT);
                irradiance = max(irradiance, 0.0f);
                float3 displayIrradiance = irradiance / (irradiance + 1.0f);
                float3 color = displayIrradiance * lerp(0.45f, 1.0f, confidence) + clipmapTint * 0.18f + skyAO.xxx * 0.12f;
                float4 probeCoordToWorld = _BurtGIRadianceCacheClipMapProbeCoordToWorldCenter[clipmapIndex];
                float4 worldPositionToProbeCoord = _BurtGIRadianceCacheClipMapWorldPositionToProbeCoord[clipmapIndex];
                float3 probeCenterWS = (float3)probeCoord * probeCoordToWorld.w + probeCoordToWorld.xyz;
                float visualizeRadiusScale = max(_BurtGIRadianceCacheClipMapApplyParams.w, 0.001f);
                float visualizeRadius = visualizeRadiusScale / max(abs(worldPositionToProbeCoord.w), 0.00001f);
                float probeSphere = 1.0f - smoothstep(0.75f, 1.0f, distance(positionWS, probeCenterWS) / max(visualizeRadius, 0.0001f));
                color = lerp(color, float3(1.0f, 0.95f, 0.18f), probeSphere * 0.55f);
                return float4(saturate(color), 1.0f);
            }

            uint BurtGIRadianceCacheDebugFrameDelta(uint frameIndex, uint lastFrame)
            {
                return frameIndex >= lastFrame
                    ? frameIndex - lastFrame
                    : (0xffffffffu - lastFrame) + frameIndex + 1u;
            }

            float4 BurtGIDebugRadianceCacheStatus(float2 screenUV)
            {
                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtGIIsSkyDepth(rawDepth))
                {
                    return float4(0.0f, 0.0f, 0.0f, 1.0f);
                }

                uint clipmapIndex;
                uint3 probeCoord;
                uint probeIndex;
                float3 positionWS;
                float3 normalWS;
                if (!BurtGIDebugResolveRadianceCacheProbe(screenUV, clipmapIndex, probeCoord, probeIndex, positionWS, normalWS))
                {
                    return float4(1.0f, 0.0f, 1.0f, 1.0f);
                }

                uint frameIndex = (uint)max(0.0f, _BurtGIRadianceCacheClipMapStageParams.z);
                uint lastUsedFrame = _BurtGIRadianceCacheClipMapProbeLastUsedFrameBuffer[probeIndex];
                uint lastTracedFrame = _BurtGIRadianceCacheClipMapProbeLastTracedFrameBuffer[probeIndex];
                bool markedThisFrame = lastUsedFrame == frameIndex && frameIndex > 0u;
                bool neverUsed = lastUsedFrame == 0u;
                bool neverTraced = lastTracedFrame == 0u;

                if (neverTraced)
                {
                    return float4(1.0f, 0.0f, 1.0f, 1.0f);
                }

                if (markedThisFrame)
                {
                    return float4(1.0f, 0.82f, 0.0f, 1.0f);
                }

                uint usedAgeFrames = BurtGIRadianceCacheDebugFrameDelta(frameIndex, lastUsedFrame);
                uint tracedAgeFrames = BurtGIRadianceCacheDebugFrameDelta(frameIndex, lastTracedFrame);
                float usedFreshness = neverUsed ? 0.0f : 1.0f - saturate((float)usedAgeFrames / 120.0f);
                float tracedFreshness = 1.0f - saturate((float)tracedAgeFrames / 120.0f);
                float3 staleColor = float3(0.82f, 0.06f, 0.02f);
                float3 usedColor = float3(0.0f, 0.35f, 1.0f);
                float3 color = lerp(staleColor, usedColor, usedFreshness);
                color.g = max(color.g, tracedFreshness * 0.45f);
                color += frac(float3(probeCoord) * float3(0.071f, 0.113f, 0.173f)) * 0.12f;
                return float4(saturate(color), 1.0f);
            }

            float BurtGIDebugGridLine(float2 uv, float2 gridSize, float width)
            {
                float2 cell = frac(saturate(uv) * max(gridSize, 1.0f));
                float edge = min(min(cell.x, cell.y), min(1.0f - cell.x, 1.0f - cell.y));
                return 1.0f - smoothstep(width, width * 2.0f, edge);
            }

            float4 BurtGIDebugScreenProbePlacement(float2 screenUV)
            {
                float2 probeUV = saturate(screenUV);
                float rawDepth = _BurtGIScreenProbeScreenDepthTexture.SampleLevel(sampler_PointClamp, probeUV, 0.0f).r;
                float4 encodedNormal = _BurtGIScreenProbeWorldNormalTexture.SampleLevel(sampler_PointClamp, probeUV, 0.0f);
                float4 encodedPosition = _BurtGIScreenProbeWorldPositionTexture.SampleLevel(sampler_PointClamp, probeUV, 0.0f);
                float valid = saturate(min(encodedNormal.a, encodedPosition.a));
                if (valid <= 0.0001f || BurtGIIsSkyDepth(rawDepth))
                {
                    float gridLine = BurtGIDebugGridLine(probeUV, _BurtGIScreenProbeGridParams.xy, 0.025f);
                    return float4(0.32f + gridLine * 0.35f, 0.0f, 0.18f + gridLine * 0.25f, 1.0f);
                }

                float3 normalColor = encodedNormal.rgb;
                float depthFade = saturate(1.0f - LinearEyeDepth(rawDepth) / max(_BurtGIScreenProbeParams.y, 0.001f));
                float placementOffset = saturate(abs(encodedPosition.a - 1.0f));
                float grid = BurtGIDebugGridLine(probeUV, _BurtGIScreenProbeGridParams.xy, 0.02f);
                float3 color = lerp(normalColor, float3(depthFade, placementOffset, 1.0f - depthFade), 0.35f);
                color = lerp(color, float3(1.0f, 1.0f, 1.0f), grid * 0.45f);
                return float4(saturate(color), 1.0f);
            }

            float4 BurtGIDebugScreenProbeTraceVisualize(float2 screenUV)
            {
                float2 atlasUV = saturate(screenUV);
                float4 traceRadiance = _BurtGIScreenProbeTraceRadianceTexture.SampleLevel(sampler_LinearClamp, atlasUV, 0.0f);
                float traceHit = saturate(_BurtGIScreenProbeTraceHitTexture.SampleLevel(sampler_LinearClamp, atlasUV, 0.0f).r);
                float3 radiance = max(traceRadiance.rgb, 0.0f);
                float3 color = radiance / (radiance + 1.0f);
                color = lerp(color * 0.25f, color, max(traceHit, saturate(traceRadiance.a)));
                color.g = max(color.g, traceHit);

                float2 atlasSize = max(_BurtGIScreenProbeTraceParams.yz, 1.0f);
                float traceResolution = max(_BurtGIScreenProbeTraceParams.x, 1.0f);
                float fineGrid = BurtGIDebugGridLine(atlasUV, atlasSize, 0.015f);
                float probeTileGrid = BurtGIDebugGridLine(atlasUV, atlasSize / traceResolution, 0.025f);
                color = lerp(color, float3(0.0f, 0.0f, 0.0f), fineGrid * 0.25f);
                color = lerp(color, float3(1.0f, 1.0f, 1.0f), probeTileGrid * 0.35f);
                return float4(saturate(color), 1.0f);
            }

            float3 BurtGISceneVoxelDebugHeat(float value)
            {
                value = saturate(value);
                return value < 0.5f
                    ? lerp(float3(0.02f, 0.08f, 0.22f), float3(0.0f, 0.72f, 0.52f), value * 2.0f)
                    : lerp(float3(0.0f, 0.72f, 0.52f), float3(1.0f, 0.42f, 0.08f), value * 2.0f - 1.0f);
            }

            float4 BurtGIDebugSceneVoxelOccupancy(float2 screenUV)
            {
                if (_BurtGISceneVoxelDebugParams.w <= 0.5f)
                {
                    return float4(0.4f, 0.0f, 0.0f, 1.0f);
                }

                float rawDepth = BurtSampleDeferredRawDepth(screenUV);
                if (BurtGIIsSkyDepth(rawDepth))
                {
                    return float4(0.0f, 0.0f, 0.0f, 1.0f);
                }

                float3 positionWS = BurtReconstructDeferredPositionWS(screenUV, rawDepth);
                float extent = max(_BurtGISceneVoxelCenterExtent.w, 0.001f);
                float3 uvw = (positionWS - _BurtGISceneVoxelCenterExtent.xyz) / (extent * 2.0f) + 0.5f;
                if (any(uvw < 0.0f) || any(uvw > 1.0f))
                {
                    return float4(1.0f, 0.0f, 1.0f, 1.0f);
                }

                float resolution = max(_BurtGISceneVoxelDebugParams.x, 1.0f);
                float maxMip = clamp(floor(log2(resolution)), 0.0f, 8.0f);
                float mipLevel = clamp(round(_BurtGISceneVoxelDebugParams.z), 0.0f, maxMip);
                float4 occupancy = _BurtGISceneVoxelOccupancyMipReadTexture.SampleLevel(sampler_LinearClamp, uvw, mipLevel);
                float maxOccupancy = saturate(occupancy.x);
                float meanOccupancy = saturate(occupancy.y);
                float mipTint = maxMip > 0.0f ? mipLevel / maxMip : 0.0f;
                float3 color = float3(maxOccupancy, meanOccupancy, mipTint);
                if (_BurtGISceneVoxelDebugLayer == 1)
                {
                    color = float3(uvw.xy, mipTint);
                }
                else if (_BurtGISceneVoxelDebugLayer == 2)
                {
                    color = BurtGISceneVoxelDebugHeat(maxOccupancy);
                }
                else if (_BurtGISceneVoxelDebugLayer == 3)
                {
                    color = float3(maxOccupancy, maxOccupancy * meanOccupancy, meanOccupancy);
                }
                else if (_BurtGISceneVoxelDebugLayer == 4)
                {
                    color = normalize(uvw - 0.5f) * 0.5f + 0.5f;
                }
                else if (_BurtGISceneVoxelDebugLayer == 5)
                {
                    color = float3(meanOccupancy, meanOccupancy * 0.35f, 0.0f);
                }
                else if (_BurtGISceneVoxelDebugLayer >= 6)
                {
                    color = lerp(float3(0.02f, 0.05f, 0.12f), float3(1.0f, 0.72f, 0.18f), saturate(max(maxOccupancy, meanOccupancy)));
                }
                float gridResolution = max(resolution / exp2(mipLevel), 1.0f);
                float edgeGrid = max(
                    BurtGIDebugGridLine(uvw.xy, float2(gridResolution, gridResolution), 0.018f),
                    max(
                        BurtGIDebugGridLine(uvw.xz, float2(gridResolution, gridResolution), 0.018f),
                        BurtGIDebugGridLine(uvw.yz, float2(gridResolution, gridResolution), 0.018f)));
                color = lerp(color, float3(1.0f, 1.0f, 1.0f), edgeGrid * 0.18f);
                if (_BurtGISceneVoxelDebugProbeParams.x > 0.5f)
                {
                    float extentWS = max(_BurtGISceneVoxelCenterExtent.w * 2.0f, 0.001f);
                    float probeSizeWS = clamp(_BurtGISceneVoxelDebugProbeParams.y, 0.01f, 2.0f);
                    float probeGridResolution = clamp(extentWS / probeSizeWS, 4.0f, 128.0f);
                    float3 probeCell = abs(frac(uvw * probeGridResolution) - 0.5f);
                    float marker = 1.0f - smoothstep(0.11f, 0.22f, length(probeCell));
                    marker *= step(0.001f, max(maxOccupancy, meanOccupancy));
                    color = lerp(color, float3(1.0f, 0.18f, 0.48f), marker * 0.85f);
                }
                return float4(saturate(color), 1.0f);
            }

            float3 BurtGIHashGridDebugHeat(float value)
            {
                value = saturate(value);
                float3 cold = float3(0.02f, 0.08f, 0.22f);
                float3 mid = float3(0.0f, 0.72f, 0.52f);
                float3 hot = float3(1.0f, 0.42f, 0.08f);
                return value < 0.5f
                    ? lerp(cold, mid, value * 2.0f)
                    : lerp(mid, hot, value * 2.0f - 1.0f);
            }

            bool BurtGIHashGridDebugIsMipStatsCell(float4 debugCell)
            {
                return debugCell.y >= 0.5f && debugCell.y <= 3.5f && abs(debugCell.x) >= 4.0f;
            }

            bool BurtGIHashGridDebugCellHasPayload(float4 debugCell)
            {
                uint4 rawCell = asuint(debugCell);
                return any(rawCell != uint4(0u, 0u, 0u, 0u));
            }

            uint BurtGIHashGridDebugFrameDelta(uint frameIndex, uint lastUpdateFrame)
            {
                if (lastUpdateFrame == 0xffffffffu)
                {
                    return 500u;
                }

                return frameIndex >= lastUpdateFrame
                    ? frameIndex - lastUpdateFrame
                    : (0xffffffffu - lastUpdateFrame) + frameIndex + 1u;
            }

            float BurtGIHashGridDebugCellAgeScale(uint cellIndex)
            {
                uint configuredCellCount = (uint)max(0.0f, _BurtGIRadianceCacheHashGridParams1.y);
                if (cellIndex >= configuredCellCount)
                {
                    return 0.0f;
                }

                uint frameIndex = (uint)max(0.0f, _BurtGIRadianceCacheClipMapStageParams.z);
                uint frameDelta = BurtGIHashGridDebugFrameDelta(frameIndex, _BurtGIRadianceCacheHashGridValueBuffer[cellIndex].z);
                float maxCellDecay = max(0.0f, _BurtGIRadianceCacheHashGridParams1.w);
                if (maxCellDecay <= 0.0f)
                {
                    return frameDelta == 0u ? 1.0f : 0.0f;
                }

                return 1.0f - saturate((float)frameDelta / maxCellDecay);
            }

            float3 BurtGIHashGridDebugApplyAgeTint(float3 color, float ageScale)
            {
                float3 staleColor = float3(0.42f, 0.08f, 0.02f);
                return lerp(staleColor, color, saturate(ageScale));
            }

            void BurtGIHashGridDebugUnpackCell(
                float4 packedCell,
                out float3 centerWS,
                out float3 directionWS,
                out float cellSize,
                out float confidence)
            {
                uint4 rawCell = asuint(packedCell);
                float4 centerAndSize = f16tof32(rawCell >> 16);
                float4 directionAndConfidence = f16tof32(rawCell & 0xffffu);
                centerWS = centerAndSize.xyz;
                cellSize = centerAndSize.w;
                directionWS = BurtSafeNormalize(directionAndConfidence.xyz);
                confidence = saturate(directionAndConfidence.w);
            }

            float4 BurtGIDebugHashGrid(float2 screenUV)
            {
                float4 header = _BurtGIRadianceCacheHashGridDebugCellBuffer[0];
                uint cellCount = (uint)max(0.0f, header.x);
                uint tileCount = (uint)max(0.0f, header.y);
                uint traceTexelCount = (uint)max(0.0f, header.z);
                if (cellCount == 0u)
                {
                    return float4(0.08f, 0.0f, 0.0f, 1.0f);
                }

                if (screenUV.y > 0.965f)
                {
                    float cellFill = saturate((float)cellCount / 4096.0f);
                    float tileFill = saturate((float)tileCount / 512.0f);
                    float traceFill = saturate((float)traceTexelCount / 65536.0f);
                    return float4(cellFill, tileFill, traceFill, 1.0f);
                }

                uint gridDim = (uint)ceil(sqrt((float)cellCount));
                uint2 cellCoord = (uint2)floor(saturate(float2(screenUV.x, 1.0f - screenUV.y)) * (float)gridDim);
                cellCoord = min(cellCoord, uint2(gridDim - 1u, gridDim - 1u));
                uint debugIndex = min(cellCoord.y * gridDim + cellCoord.x, cellCount - 1u) + 1u;
                float4 debugCell = _BurtGIRadianceCacheHashGridDebugCellBuffer[debugIndex];
                if (!BurtGIHashGridDebugCellHasPayload(debugCell))
                {
                    return float4(0.0f, 0.0f, 0.0f, 1.0f);
                }

                if (!BurtGIHashGridDebugIsMipStatsCell(debugCell))
                {
                    uint cellIndex = debugIndex - 1u;
                    float ageScale = BurtGIHashGridDebugCellAgeScale(cellIndex);
                    float3 centerWS;
                    float3 directionWS;
                    float cellSize;
                    float confidence;
                    BurtGIHashGridDebugUnpackCell(debugCell, centerWS, directionWS, cellSize, confidence);
                    float3 positionColor = frac(abs(centerWS) * float3(0.071f, 0.113f, 0.173f));
                    float3 directionColor = directionWS * 0.5f + 0.5f;
                    float3 confidenceColor = BurtGIHashGridDebugHeat(confidence);
                    float3 debugColor = lerp(positionColor, lerp(confidenceColor, directionColor, 0.35f), 0.72f);
                    return float4(BurtGIHashGridDebugApplyAgeTint(debugColor, ageScale), 1.0f);
                }

                float ageScale = BurtGIHashGridDebugCellAgeScale(debugIndex - 1u);
                float mipMarker = saturate((debugCell.y - 1.0f) / 2.0f);
                float mipEnergy = saturate(max(debugCell.z, debugCell.w));
                float3 mipColor = BurtGIHashGridDebugApplyAgeTint(BurtGIHashGridDebugHeat(mipMarker), ageScale) * lerp(0.35f, 1.0f, mipEnergy);
                return float4(mipColor, 1.0f);
            }

            struct HashGridDebugVaryings
            {
                float4 PositionCS : SV_POSITION;
                float4 Color : COLOR0;
            };

            float2 BurtGIHashGridDebugQuadCorner(uint localVertexIndex)
            {
                if (localVertexIndex == 0u || localVertexIndex == 3u)
                {
                    return float2(-1.0f, -1.0f);
                }

                if (localVertexIndex == 1u)
                {
                    return float2(-1.0f, 1.0f);
                }

                if (localVertexIndex == 2u || localVertexIndex == 4u)
                {
                    return float2(1.0f, 1.0f);
                }

                return float2(1.0f, -1.0f);
            }

            HashGridDebugVaryings VertHashGridDebugGeometry(Attributes input)
            {
                HashGridDebugVaryings output;
                output.PositionCS = float4(2.0f, 2.0f, 0.0f, 1.0f);
                output.Color = 0.0f;

                uint cellIndex = input.VertexID / 6u;
                uint configuredCellCount = (uint)max(0.0f, _BurtGIRadianceCacheHashGridParams1.y);
                uint cellsPerTile = max(1u, (uint)max(1.0f, _BurtGIRadianceCacheHashGridParams1.z));
                uint tileCount = max(1u, (uint)max(1.0f, _BurtGIRadianceCacheHashGridParams1.x));
                uint packedTileIndex = cellIndex / cellsPerTile;
                uint localCellIndex = cellIndex - packedTileIndex * cellsPerTile;
                uint4 packedTile = _BurtGIRadianceCacheHashGridTileBuffer[packedTileIndex];
                uint hashGridPingPong = ((uint)max(0.0f, _BurtGIRadianceCacheClipMapStageParams.z)) & 1u;
                uint tileIndex = hashGridPingPong == 0u ? packedTile.z : packedTile.w;
                cellIndex = tileIndex * cellsPerTile + localCellIndex;
                if (cellIndex >= configuredCellCount)
                {
                    return output;
                }

                float4 debugCell = _BurtGIRadianceCacheHashGridDebugCellBuffer[cellIndex + 1u];
                if (!BurtGIHashGridDebugCellHasPayload(debugCell) || BurtGIHashGridDebugIsMipStatsCell(debugCell))
                {
                    return output;
                }

                float3 centerWS;
                float3 directionWS;
                float cellSize;
                float confidence;
                BurtGIHashGridDebugUnpackCell(debugCell, centerWS, directionWS, cellSize, confidence);
                float3 upSeed = abs(directionWS.y) > 0.96f ? float3(1.0f, 0.0f, 0.0f) : float3(0.0f, 1.0f, 0.0f);
                float3 rightWS = BurtSafeNormalize(cross(upSeed, directionWS));
                float3 upWS = BurtSafeNormalize(cross(directionWS, rightWS));
                float2 quadCorner = BurtGIHashGridDebugQuadCorner(input.VertexID % 6u);
                float debugCellSize = max(cellSize, 0.001f);
                float3 positionWS = centerWS + 0.5f * debugCellSize * (directionWS + rightWS * quadCorner.x + upWS * quadCorner.y);

                float3 positionColor = frac(abs(centerWS) * float3(0.071f, 0.113f, 0.173f));
                float3 directionColor = directionWS * 0.5f + 0.5f;
                float3 heatColor = lerp(BurtGIHashGridDebugHeat(confidence), directionColor, 0.35f);
                float ageScale = BurtGIHashGridDebugCellAgeScale(cellIndex);
                output.PositionCS = mul(_BurtGIViewProjectionMatrix, float4(positionWS, 1.0f));
                output.Color = float4(
                    BurtGIHashGridDebugApplyAgeTint(lerp(positionColor, heatColor, 0.7f), ageScale),
                    lerp(0.18f, lerp(0.32f, 0.82f, confidence), ageScale));
                return output;
            }

            float4 FragHashGridDebugGeometry(HashGridDebugVaryings input) : SV_Target
            {
                return input.Color;
            }

            struct RadianceCacheVisualizeGeometryVaryings
            {
                float4 PositionCS : SV_POSITION;
                float3 NormalWS : TEXCOORD0;
                nointerpolation uint ProbeIndex : TEXCOORD1;
                nointerpolation uint ClipmapIndex : TEXCOORD2;
            };

            uint BurtGIRadianceCacheVisualizeCubeCornerIndex(uint vertexIndex)
            {
                static const uint indices[36] =
                {
                    0u, 2u, 3u, 0u, 3u, 1u,
                    4u, 5u, 7u, 4u, 7u, 6u,
                    0u, 1u, 5u, 0u, 5u, 4u,
                    2u, 6u, 7u, 2u, 7u, 3u,
                    0u, 4u, 6u, 0u, 6u, 2u,
                    1u, 3u, 7u, 1u, 7u, 5u
                };
                return indices[vertexIndex % 36u];
            }

            float3 BurtGIRadianceCacheVisualizeCubeCorner(uint vertexIndex)
            {
                uint cornerIndex = BurtGIRadianceCacheVisualizeCubeCornerIndex(vertexIndex);
                return float3(
                    (cornerIndex & 1u) != 0u ? 1.0f : -1.0f,
                    (cornerIndex & 2u) != 0u ? 1.0f : -1.0f,
                    (cornerIndex & 4u) != 0u ? 1.0f : -1.0f);
            }

            RadianceCacheVisualizeGeometryVaryings VertRadianceCacheVisualizeGeometry(Attributes input)
            {
                RadianceCacheVisualizeGeometryVaryings output;
                output.PositionCS = float4(2.0f, 2.0f, 0.0f, 1.0f);
                output.NormalWS = float3(0.0f, 1.0f, 0.0f);
                output.ProbeIndex = BURT_GI_RADIANCE_CACHE_INVALID_PROBE_INDEX;
                output.ClipmapIndex = 0u;

                uint clipmapCount = max(BurtGIRadianceCacheClipmapCount(), 1u);
                uint clipmapIndex = min((uint)max(0.0f, _BurtGIRadianceCacheClipMapVisualizeDrawParams.x), clipmapCount - 1u);
                uint clipmapResolution = BurtGIRadianceCacheClipmapResolution();
                uint instanceId = input.InstanceID;
                uint3 probeCoord;
                probeCoord.x = instanceId % clipmapResolution;
                probeCoord.y = (instanceId / clipmapResolution) % clipmapResolution;
                probeCoord.z = instanceId / max(clipmapResolution * clipmapResolution, 1u);
                if (probeCoord.z >= clipmapResolution)
                {
                    return output;
                }

                uint probeIndex = BurtGIRadianceCacheProbeIndexFromIndirection(clipmapIndex, probeCoord);
                if (probeIndex >= BURT_GI_RADIANCE_CACHE_USED_PROBE_INDEX)
                {
                    return output;
                }

                float4 probeCoordToWorld = _BurtGIRadianceCacheClipMapProbeCoordToWorldCenter[clipmapIndex];
                float4 worldPositionToProbeCoord = _BurtGIRadianceCacheClipMapWorldPositionToProbeCoord[clipmapIndex];
                float3 probeCenterWS = (float3)probeCoord * probeCoordToWorld.w + probeCoordToWorld.xyz;
                probeCenterWS += _BurtGIRadianceCacheClipMapProbeWorldOffsetBuffer[probeIndex].xyz;

                float visualizeRadiusScale = max(_BurtGIRadianceCacheClipMapVisualizeDrawParams.y, 0.001f);
                float visualizeRadius = visualizeRadiusScale / max(abs(worldPositionToProbeCoord.w), 0.00001f);
                float3 localCorner = BurtGIRadianceCacheVisualizeCubeCorner(input.VertexID);
                float3 positionWS = probeCenterWS + localCorner * visualizeRadius;
                output.PositionCS = mul(_BurtGIViewProjectionMatrix, float4(positionWS, 1.0f));
                output.NormalWS = BurtSafeNormalize(localCorner);
                output.ProbeIndex = probeIndex;
                output.ClipmapIndex = clipmapIndex;
                return output;
            }

            float4 FragRadianceCacheVisualizeGeometry(RadianceCacheVisualizeGeometryVaryings input) : SV_Target
            {
                if (input.ProbeIndex >= BURT_GI_RADIANCE_CACHE_USED_PROBE_INDEX)
                {
                    discard;
                    return 0.0f;
                }

                float statusMode = _BurtGIRadianceCacheClipMapVisualizeDrawParams.z;
                if (statusMode > 0.5f)
                {
                    uint frameIndex = (uint)max(0.0f, _BurtGIRadianceCacheClipMapStageParams.z);
                    uint lastUsedFrame = _BurtGIRadianceCacheClipMapProbeLastUsedFrameBuffer[input.ProbeIndex];
                    uint lastTracedFrame = _BurtGIRadianceCacheClipMapProbeLastTracedFrameBuffer[input.ProbeIndex];
                    bool markedThisFrame = lastUsedFrame == frameIndex && frameIndex > 0u;
                    bool neverTraced = lastTracedFrame == 0u;
                    float3 stateColor = markedThisFrame ? float3(1.0f, 0.82f, 0.0f) : float3(0.0f, 0.35f, 1.0f);
                    stateColor = neverTraced ? float3(1.0f, 0.0f, 1.0f) : stateColor;
                    return float4(stateColor, 0.82f);
                }

                float3 normalWS = BurtSafeNormalize(input.NormalWS);
                float3 irradiance = max(BurtGISampleRadianceCacheProbeIrradiance(input.ProbeIndex, normalWS), 0.0f);
                float3 displayIrradiance = irradiance / (irradiance + 1.0f);
                float clipmapCount = max((float)BurtGIRadianceCacheClipmapCount() - 1.0f, 1.0f);
                float clipmapT = saturate((float)input.ClipmapIndex / clipmapCount);
                float3 clipmapTint = lerp(float3(0.02f, 0.22f, 0.85f), float3(1.0f, 0.42f, 0.04f), clipmapT);
                return float4(saturate(displayIrradiance + clipmapTint * 0.18f), 0.82f);
            }

            struct ScreenProbeTraceVisualizeGeometryVaryings
            {
                float4 PositionCS : SV_POSITION;
                float4 Color : COLOR0;
            };

            ScreenProbeTraceVisualizeGeometryVaryings VertScreenProbeTraceVisualizeGeometry(Attributes input)
            {
                ScreenProbeTraceVisualizeGeometryVaryings output;
                output.PositionCS = float4(2.0f, 2.0f, 0.0f, 1.0f);
                output.Color = 0.0f;

                uint traceIndex = input.VertexID / 2u;
                uint endpointIndex = input.VertexID - traceIndex * 2u;
                uint bufferIndex = traceIndex * 3u;
                float3 originWS = _XGIScreenProbeTraceVisualize_TracesData[bufferIndex + 0u].xyz;
                float3 traceVectorWS = _XGIScreenProbeTraceVisualize_TracesData[bufferIndex + 1u].xyz;
                float3 radiance = max(_XGIScreenProbeTraceVisualize_TracesData[bufferIndex + 2u].xyz, 0.0f);
                float3 positionWS = originWS + traceVectorWS * (float)endpointIndex;
                float3 displayColor = saturate(radiance);
                float lineAlpha = endpointIndex == 0u ? 0.35f : 0.85f;
                output.PositionCS = mul(_BurtGIViewProjectionMatrix, float4(positionWS, 1.0f));
                output.Color = float4(displayColor, lineAlpha);
                return output;
            }

            float4 FragScreenProbeTraceVisualizeGeometry(ScreenProbeTraceVisualizeGeometryVaryings input) : SV_Target
            {
                return input.Color;
            }

            float4 FragDebug(Varyings input) : SV_Target
            {
                float2 screenUV = input.ScreenUV;
                float4 rawBurtGI = tex2D(_BurtScreenSpaceGlobalIlluminationRawTexture, screenUV);
                float4 finalBurtGI = tex2D(_BurtScreenSpaceGlobalIlluminationTexture, screenUV);
                float debugMode = round(_BurtGIDebugMode);

                if (debugMode < 1.5f)
                {
                    return float4(max(rawBurtGI.rgb, 0.0f), 1.0f);
                }

                if (debugMode < 2.5f)
                {
                    return float4(max(finalBurtGI.rgb, 0.0f), 1.0f);
                }

                if (debugMode < 3.5f)
                {
                    return BurtGIDebugHitRatio(finalBurtGI);
                }

                float3 compositeContribution = max(finalBurtGI.rgb, 0.0f) * _BurtGIParams1.xxx;
                if (debugMode < 4.5f)
                {
                    float4 cameraColor = tex2D(_BurtGIDebugCameraColorTexture, screenUV);
                    cameraColor.rgb += compositeContribution;
                    return cameraColor;
                }

                if (debugMode < 5.5f)
                {
                    return float4(compositeContribution, 1.0f);
                }

                float4 temporalDiagnostics = tex2D(_BurtGITemporalDiagnosticsTexture, screenUV);
                if (debugMode < 6.5f)
                {
                    float confidence = saturate(temporalDiagnostics.r);
                    float rejection = saturate(temporalDiagnostics.g);
                    float hitRatio = saturate(temporalDiagnostics.b);
                    return float4(rejection, confidence, hitRatio * 0.45f, 1.0f);
                }

                if (debugMode < 7.5f)
                {
                    float rejection = saturate(temporalDiagnostics.g);
                    return float4(rejection, 1.0f - rejection, saturate(temporalDiagnostics.b), 1.0f);
                }

                float historyValid = _BurtGITemporalParams.y;
                float3 historyBurtGI = max(tex2D(_BurtGIPreviousHistoryTexture, screenUV).rgb, 0.0f);
                if (debugMode < 8.5f)
                {
                    return float4(historyBurtGI * historyValid, 1.0f);
                }

                float3 difference = abs(max(finalBurtGI.rgb, 0.0f) - historyBurtGI) * historyValid;
                if (debugMode < 9.5f)
                {
                    return float4(saturate(difference * 4.0f), 1.0f);
                }

                if (debugMode < 10.5f)
                {
                    return BurtGIDebugLeakGuard(screenUV, finalBurtGI);
                }

                if (debugMode > 11.5f && debugMode < 12.5f)
                {
                    return BurtGIDebugConfidence(screenUV);
                }

                if (debugMode > 12.5f && debugMode < 13.5f)
                {
                    return BurtGIDebugHashGrid(screenUV);
                }

                if (debugMode > 13.5f && debugMode < 14.5f)
                {
                    return BurtGIDebugRadianceCacheSkyAO(screenUV);
                }

                if (debugMode > 14.5f && debugMode < 15.5f)
                {
                    return tex2D(_BurtGIRadianceCacheStatsTexture, screenUV);
                }

                if (debugMode > 15.5f && debugMode < 16.5f)
                {
                    return BurtGIDebugRadianceCacheVisualize(screenUV);
                }

                if (debugMode > 16.5f && debugMode < 17.5f)
                {
                    return BurtGIDebugRadianceCacheStatus(screenUV);
                }

                if (debugMode > 17.5f && debugMode < 18.5f)
                {
                    return BurtGIDebugScreenProbePlacement(screenUV);
                }

                if (debugMode > 18.5f && debugMode < 19.5f)
                {
                    return BurtGIDebugScreenProbeTraceVisualize(screenUV);
                }

                if (debugMode > 19.5f && debugMode < 20.5f)
                {
                    return BurtGIDebugSceneVoxelOccupancy(screenUV);
                }

                float3 diffuseIndirect = max(finalBurtGI.rgb, 0.0f) * _BurtGIParams1.x;
                float3 backfaceDiffuseIndirect = _BurtGIIndirectChannelParams.x > 0.5f
                    ? max(tex2D(_BurtGIBackfaceDiffuseIndirectTexture, screenUV).rgb, 0.0f) * _BurtGIParams1.x
                    : 0.0f;
                float3 roughSpecularIndirect = _BurtGIIndirectChannelParams.y > 0.5f
                    ? max(tex2D(_BurtGIRoughSpecularIndirectTexture, screenUV).rgb, 0.0f)
                    : 0.0f;
                if (debugMode > 20.5f && debugMode < 21.5f)
                {
                    return float4(diffuseIndirect + backfaceDiffuseIndirect + roughSpecularIndirect, 1.0f);
                }

                if (debugMode > 21.5f && debugMode < 22.5f)
                {
                    return float4(diffuseIndirect, 1.0f);
                }

                if (debugMode > 22.5f && debugMode < 23.5f)
                {
                    return float4(backfaceDiffuseIndirect, 1.0f);
                }

                if (debugMode > 23.5f && debugMode < 24.5f)
                {
                    return float4(roughSpecularIndirect, 1.0f);
                }

                float2 quadrantUV = frac(screenUV * 2.0f);
                if (screenUV.y >= 0.5f)
                {
                    return screenUV.x < 0.5f
                        ? float4(max(tex2D(_BurtScreenSpaceGlobalIlluminationRawTexture, quadrantUV).rgb, 0.0f), 1.0f)
                        : float4(max(tex2D(_BurtScreenSpaceGlobalIlluminationTexture, quadrantUV).rgb, 0.0f), 1.0f);
                }

                float4 quadrantFinalBurtGI = tex2D(_BurtScreenSpaceGlobalIlluminationTexture, quadrantUV);
                return screenUV.x < 0.5f
                    ? BurtGIDebugHitRatio(quadrantFinalBurtGI)
                    : BurtGIDebugLeakGuard(quadrantUV, quadrantFinalBurtGI);
            }
        ENDHLSL

        Pass
        {
            Name "Burt Screen Space Global Illumination Trace"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragTrace
            ENDHLSL
        }

        Pass
        {
            Name "Burt Screen Space Global Illumination Blur"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragBlur
            ENDHLSL
        }

        Pass
        {
            Name "Burt Screen Space Global Illumination Composite"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragComposite
            ENDHLSL
        }

        Pass
        {
            Name "Burt Screen Space Global Illumination Debug"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragDebug
            ENDHLSL
        }

        Pass
        {
            Name "Burt Screen Space Global Illumination Temporal"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragTemporal
            ENDHLSL
        }

        Pass
        {
            Name "Burt Screen Space Global Illumination Copy Depth Normal"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragCopyDepthNormal
            ENDHLSL
        }

        Pass
        {
            Name "Burt Screen Space Global Illumination Copy Temporal Final"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragCopyTemporalFinal
            ENDHLSL
        }

        Pass
        {
            Name "Burt Screen Space Global Illumination Temporal Diagnostics"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragTemporalDiagnostics
            ENDHLSL
        }

        Pass
        {
            Name "Burt Screen Space Global Illumination Resolve Indirect Channels"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragResolveIndirectChannels
            ENDHLSL
        }

        Pass
        {
            Name "Burt ScreenProbe Lite Prepare"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragScreenProbeLite
            ENDHLSL
        }

        Pass
        {
            Name "Burt ScreenProbe Lite Temporal Filter"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragScreenProbeLiteTemporalFilter
            ENDHLSL
        }

        Pass
        {
            Name "Burt ScreenProbe Lite Spatial Filter"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragScreenProbeLiteSpatialFilter
            ENDHLSL
        }

        Pass
        {
            Name "Burt ScreenProbe Lite Fixup Borders"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragScreenProbeLiteFixupBorders
            ENDHLSL
        }

        Pass
        {
            Name "Burt ScreenProbe Lite Generate Mip"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragScreenProbeLiteGenerateMip
            ENDHLSL
        }

        Pass
        {
            Name "Burt ScreenProbe Lite Irradiance SH"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragScreenProbeLiteIrradianceSH
            ENDHLSL
        }

        Pass
        {
            Name "Burt ScreenProbe Lite Trace Atlas"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragScreenProbeLiteTraceAtlas
            ENDHLSL
        }

        Pass
        {
            Name "Burt ScreenProbe Placement Uniform"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragScreenProbeLitePlacementUniform
            ENDHLSL
        }

        Pass
        {
            Name "Burt Radiance Cache HashGrid Debug Geometry"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex VertHashGridDebugGeometry
            #pragma fragment FragHashGridDebugGeometry
            ENDHLSL
        }

        Pass
        {
            Name "Burt Irradiance Field Integrate"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragIrradianceFieldIntegrate
            ENDHLSL
        }

        Pass
        {
            Name "Burt Radiance Cache Visualize Geometry"
            Cull Back
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex VertRadianceCacheVisualizeGeometry
            #pragma fragment FragRadianceCacheVisualizeGeometry
            ENDHLSL
        }

        Pass
        {
            Name "Burt ScreenProbe Trace Visualize Geometry"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex VertScreenProbeTraceVisualizeGeometry
            #pragma fragment FragScreenProbeTraceVisualizeGeometry
            ENDHLSL
        }
    }
}
