// Isolated compact fine payload experiment, not enabled by the BRP runtime.
Shader "Hidden/Codex/Sparse Fine Voxelize 443"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _MaskMap ("Mask Map", 2D) = "black" {}
        _Metallic ("Metallic", Range(0, 1)) = 0
        _EmissionMap ("Emission Map", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _AlphaClip ("Alpha Clip", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _BurtGIVoxelizeEmissionMode ("GI Voxelize Emission Mode", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        Pass
        {
            Name "BurtGIVoxelize"
            Tags { "LightMode" = "BurtGIVoxelize" }
            Cull Off
            ZWrite Off
            ZTest Always
            ColorMask 0

            HLSLPROGRAM
            #pragma target 5.0
            #pragma require geometry
            #pragma vertex BurtGIVoxelizeVertex
            #pragma geometry BurtGIVoxelizeGeometry
            #pragma fragment BurtGIVoxelizeFragment

            #include "UnityCG.cginc"
            #include "../../Runtime/Resources/BurtGISceneVoxelFineOccupancy.hlsl"

            sampler2D _BaseMap;
            sampler2D _MaskMap;
            sampler2D _EmissionMap;
            float4 _BaseMap_ST;
            float4 _EmissionMap_ST;
            float4 _BaseColor;
            float4 _EmissionColor;
            float _Metallic;
            float _AlphaClip;
            float _Cutoff;
            float _BurtGIVoxelizeEmissionMode;
            float4 _BurtGISceneVoxelCenterExtent;
            float4 _BurtGISceneVoxelMaterialParams;
            float4 _BurtGISceneVoxelLightingParams;

            struct FinePayload { float4 radiance; float4 geometry; float4 lighting; };
            Texture3D<uint> _FineLowRead;
            Texture3D<uint> _FineHighRead;
            Texture3D<uint> _FineOffsetRead;
            RWStructuredBuffer<uint> _FineOwners : register(u1);
            RWStructuredBuffer<FinePayload> _FinePayload : register(u2);
            uint _Capacity;
            uint _BurtGIVoxelizePrimitiveBase;
            int _BurtGIVoxelizeResolvePhase;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct VoxelVertex
            {
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            struct VoxelFragment
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                nointerpolation uint ownerKey : TEXCOORD3;
            };

            VoxelVertex BurtGIVoxelizeVertex(Attributes input)
            {
                VoxelVertex output;
                output.positionWS = mul(unity_ObjectToWorld, input.positionOS).xyz;
                output.normalWS = UnityObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            float2 BurtGIVoxelizeProject(float3 positionWS, uint axis)
            {
                float3 normalizedPosition = (positionWS - _BurtGISceneVoxelCenterExtent.xyz) / max(_BurtGISceneVoxelCenterExtent.w, 0.001);
                if (axis == 0u)
                {
                    return normalizedPosition.zy;
                }
                if (axis == 1u)
                {
                    return normalizedPosition.xz;
                }
                return normalizedPosition.xy;
            }

            void BurtGIVoxelizeEmitTriangle(
                VoxelVertex input[3],
                uint axis,
                uint primitiveID,
                inout TriangleStream<VoxelFragment> stream)
            {
                [unroll]
                for (uint vertexIndex = 0u; vertexIndex < 3u; ++vertexIndex)
                {
                    VoxelFragment output;
                    output.positionCS = float4(BurtGIVoxelizeProject(input[vertexIndex].positionWS, axis), 0.5, 1.0);
                    output.positionWS = input[vertexIndex].positionWS;
                    output.normalWS = input[vertexIndex].normalWS;
                    output.uv = input[vertexIndex].uv;
                    output.ownerKey = (_BurtGIVoxelizePrimitiveBase + primitiveID) * 3u + axis;
                    stream.Append(output);
                }
                stream.RestartStrip();
            }

            [maxvertexcount(9)]
            void BurtGIVoxelizeGeometry(triangle VoxelVertex input[3], uint primitiveID : SV_PrimitiveID, inout TriangleStream<VoxelFragment> stream)
            {
                BurtGIVoxelizeEmitTriangle(input, 0u, primitiveID, stream);
                BurtGIVoxelizeEmitTriangle(input, 1u, primitiveID, stream);
                BurtGIVoxelizeEmitTriangle(input, 2u, primitiveID, stream);
            }

            float4 BurtGIVoxelizeFragment(VoxelFragment input) : SV_Target
            {
                float2 baseUV = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                // Derivatives must be evaluated before UAV-dependent control
                // flow on D3D11. Explicit gradients also keep texture LOD equal
                // between the selection and resolve draw sequences.
                float2 baseDx = ddx(baseUV), baseDy = ddy(baseUV);
                float2 emissionUV = input.uv * _EmissionMap_ST.xy + _EmissionMap_ST.zw;
                float2 emissionDx = ddx(emissionUV), emissionDy = ddy(emissionUV);
                float4 baseSample = tex2D(_BaseMap, baseUV) * _BaseColor;
                if (_AlphaClip > 0.5)
                {
                    clip(baseSample.a - _Cutoff);
                }

                float3 uvw = (input.positionWS - _BurtGISceneVoxelCenterExtent.xyz) /
                    max(_BurtGISceneVoxelCenterExtent.w * 2.0, 0.001) + 0.5;
                if (any(uvw < 0.0) || any(uvw > 1.0))
                {
                    discard;
                }

                uint width, height, depth;
                _FineLowRead.GetDimensions(width, height, depth);
                uint3 nodeSize = max(uint3(width, height, depth), 1u);
                uint3 fineCoord = min((uint3)(uvw * (float3)(nodeSize*4u)), nodeSize*4u-1u);
                uint3 coarseCoord = fineCoord >> 2u;
                uint3 localFine = fineCoord & 3u;
                uint fineBit = localFine.x + localFine.y*4u + localFine.z*16u;
                uint2 mask = uint2(_FineLowRead[coarseCoord], _FineHighRead[coarseCoord]);
                if (!BurtGIFineOccupancyContains(mask,localFine)) discard;
                uint offset = _FineOffsetRead[coarseCoord];
                uint rank = BurtGIFineOccupancyRank(mask,fineBit);
                if (offset == 0xffffffffu || offset >= _Capacity || rank >= _Capacity-offset) discard;
                uint index = offset+rank;
                // One payload per occupied fine cell restores the original
                // per-triangle/projection unique-writer premise at 4N viewport.
                if (_BurtGIVoxelizeResolvePhase == 0)
                {
                    InterlockedMin(_FineOwners[index], input.ownerKey);
                    return 0.0;
                }
                if (_FineOwners[index] != input.ownerKey) discard;
                float metallic = saturate(_Metallic * tex2Dgrad(_MaskMap, baseUV, baseDx, baseDy).r);
                float3 albedo = max(baseSample.rgb * (1.0 - metallic), 0.0);
                float3 emission = max(tex2Dgrad(_EmissionMap, emissionUV, emissionDx, emissionDy).rgb * _EmissionColor.rgb, 0.0);
                emission = max(emission, albedo * max(_BurtGIVoxelizeEmissionMode, 0.0));
                float3 boostedAlbedo = min(saturate(pow(albedo, max(_BurtGISceneVoxelLightingParams.x, 0.001))), 0.99);
                float3 normalWS = normalize(input.normalWS);
                float3 radiance = boostedAlbedo * 0.12 + emission;

                FinePayload payload;
                payload.radiance = float4(radiance, 1.0);
                payload.geometry = float4(normalWS * 0.5 + 0.5, 1.0);
                payload.lighting = float4(emission, 1.0);
                _FinePayload[index] = payload;
                return 0.0;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
