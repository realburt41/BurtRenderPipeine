Shader "Hidden/XRender/XGI/XGIToolsVoxelGenDebugProbe"
{
    Properties
    {
        _BurtXGICompatTint ("Tint", Color) = (0.95, 0.16, 0.38, 0.9)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "BurtRenderPipeline" "Queue" = "Transparent" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "Forward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"

            Texture3D<float4> _BurtGISceneVoxelOccupancyMipReadTexture;
            SamplerState sampler_BurtGISceneVoxelOccupancyMipReadTexture;
            StructuredBuffer<uint> DebugProbeArgsBufferParams;
            StructuredBuffer<uint> DebugProbePlaceProbeIndex;

            float4 _BurtXGICompatTint;
            float4 _BurtGISceneVoxelDebugProbeClipmapCenterExtent[6];
            float4 _BurtGISceneVoxelDebugProbeDrawParams; // x=probe node size, y=clipmap count, z=draw instance count, w=occupancy valid.
            float4 _BurtGISceneVoxelDebugParams; // x=occupancy mip resolution, y=clipmap count, z=debug mip, w=valid.
            float DebugProbeSizeWS;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 centerWS : TEXCOORD1;
                float occupancy : TEXCOORD2;
                nointerpolation uint clipmapIndex : TEXCOORD3;
            };

            static const float3 kCubeCorners[8] =
            {
                float3(-0.5, -0.5, -0.5),
                float3( 0.5, -0.5, -0.5),
                float3( 0.5,  0.5, -0.5),
                float3(-0.5,  0.5, -0.5),
                float3(-0.5, -0.5,  0.5),
                float3( 0.5, -0.5,  0.5),
                float3( 0.5,  0.5,  0.5),
                float3(-0.5,  0.5,  0.5)
            };

            static const uint kCubeIndices[36] =
            {
                0, 2, 1, 0, 3, 2,
                4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4,
                3, 7, 6, 3, 6, 2,
                1, 2, 6, 1, 6, 5,
                0, 4, 7, 0, 7, 3
            };

            static const float3 kClipmapColors[6] =
            {
                float3(1.00, 0.16, 0.38),
                float3(0.20, 0.72, 1.00),
                float3(0.18, 1.00, 0.48),
                float3(1.00, 0.80, 0.18),
                float3(0.90, 0.32, 1.00),
                float3(0.12, 0.92, 0.90)
            };
            static const uint kDebugProbeAllocatorOffset = 8u;

            uint ResolveClipmapIndex(uint instanceId)
            {
                uint clipmapIndex = 0;
                if (DebugProbeArgsBufferParams[4] <= instanceId) clipmapIndex = 4;
                if (DebugProbeArgsBufferParams[clipmapIndex + 2] <= instanceId) clipmapIndex += 2;
                if (DebugProbeArgsBufferParams[clipmapIndex + 1] <= instanceId) clipmapIndex += 1;
                return min(clipmapIndex, 5u);
            }

            uint3 FlatToProbeCoord(uint flatIndex, uint probeNodeSize)
            {
                uint safeSize = max(probeNodeSize, 1u);
                uint x = flatIndex % safeSize;
                uint yz = flatIndex / safeSize;
                return uint3(x, yz / safeSize, yz % safeSize);
            }

            float SampleProbeOccupancy(uint3 probeCoord, uint probeNodeSize, uint clipmapIndex)
            {
                if (_BurtGISceneVoxelDebugParams.w < 0.5 || _BurtGISceneVoxelDebugProbeDrawParams.w < 0.5)
                {
                    return 1.0;
                }

                float occupancyMipResolution = max(_BurtGISceneVoxelDebugParams.x, 1.0);
                float3 uvw = ((float3)probeCoord + 0.5) / max((float)probeNodeSize, 1.0);
                float zSlice = (uvw.z + (float)clipmapIndex) / max(_BurtGISceneVoxelDebugParams.y, 1.0);
                float4 occupancyValue = _BurtGISceneVoxelOccupancyMipReadTexture.SampleLevel(
                    sampler_BurtGISceneVoxelOccupancyMipReadTexture,
                    float3(uvw.xy, zSlice),
                    0.0);
                return max(max(occupancyValue.r, occupancyValue.g), max(occupancyValue.b, occupancyValue.a));
            }

            Varyings Vert(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
            {
                Varyings output;
                output.positionCS = 0;
                output.normalWS = float3(0.0, 1.0, 0.0);
                output.centerWS = 0;
                output.occupancy = 0.0;
                output.clipmapIndex = 0u;

                uint drawInstanceCount = (uint)max(_BurtGISceneVoxelDebugProbeDrawParams.z, 0.0);
                if (instanceId >= drawInstanceCount)
                {
                    return output;
                }

                uint probeNodeSize = max((uint)round(_BurtGISceneVoxelDebugProbeDrawParams.x), 1u);
                uint clipmapIndex = ResolveClipmapIndex(instanceId);
                uint probeIndexInClipmap = instanceId - DebugProbeArgsBufferParams[clipmapIndex];
                uint probeNodeFlatIndex = DebugProbePlaceProbeIndex[kDebugProbeAllocatorOffset + probeIndexInClipmap];
                uint3 probeCoord = FlatToProbeCoord(probeNodeFlatIndex, probeNodeSize);
                float occupancy = SampleProbeOccupancy(probeCoord, probeNodeSize, clipmapIndex);
                if (occupancy <= 0.0001)
                {
                    return output;
                }

                float4 centerExtent = _BurtGISceneVoxelDebugProbeClipmapCenterExtent[clipmapIndex];
                float extent = max(centerExtent.w, 0.001);
                float3 centerWS = centerExtent.xyz + (((float3)probeCoord + 0.5) / (float)probeNodeSize - 0.5) * extent * 2.0;
                float probeSizeWS = max(DebugProbeSizeWS, 0.01) * exp2((float)clipmapIndex);
                float3 cornerOS = kCubeCorners[kCubeIndices[vertexId % 36u]];
                float3 cornerWS = centerWS + cornerOS * probeSizeWS;

                output.positionCS = UnityWorldToClipPos(cornerWS);
                output.normalWS = normalize(cornerOS);
                output.centerWS = centerWS;
                output.occupancy = saturate(occupancy);
                output.clipmapIndex = clipmapIndex;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float fresnel = pow(1.0 - saturate(dot(normalWS, normalize(_WorldSpaceCameraPos.xyz - input.centerWS))), 2.0);
                float3 color = lerp(_BurtXGICompatTint.rgb, kClipmapColors[min(input.clipmapIndex, 5u)], 0.65);
                color *= 0.35 + input.occupancy * 0.65 + fresnel * 0.35;
                return float4(saturate(color), saturate(_BurtXGICompatTint.a));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
