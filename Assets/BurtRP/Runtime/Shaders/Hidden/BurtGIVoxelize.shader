Shader "Hidden/Burt Render Pipeline/GI Voxelize"
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

            RWTexture3D<float4> _BurtGISceneVoxelRadianceTexture : register(u1);
            RWTexture3D<float4> _BurtGISceneVoxelGeometryTexture : register(u2);
            RWTexture3D<float4> _BurtGISceneVoxelLightingTexture : register(u3);

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
                    stream.Append(output);
                }
                stream.RestartStrip();
            }

            [maxvertexcount(9)]
            void BurtGIVoxelizeGeometry(triangle VoxelVertex input[3], inout TriangleStream<VoxelFragment> stream)
            {
                BurtGIVoxelizeEmitTriangle(input, 0u, stream);
                BurtGIVoxelizeEmitTriangle(input, 1u, stream);
                BurtGIVoxelizeEmitTriangle(input, 2u, stream);
            }

            float4 BurtGIVoxelizeFragment(VoxelFragment input) : SV_Target
            {
                float2 baseUV = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
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

                uint width;
                uint height;
                uint depth;
                _BurtGISceneVoxelRadianceTexture.GetDimensions(width, height, depth);
                uint3 volumeSize = max(uint3(width, height, depth), 1u);
                uint3 voxelCoord = min((uint3)(uvw * (float3)volumeSize), volumeSize - 1u);
                float metallic = saturate(_Metallic * tex2D(_MaskMap, baseUV).r);
                float3 albedo = max(baseSample.rgb * (1.0 - metallic), 0.0);
                float2 emissionUV = input.uv * _EmissionMap_ST.xy + _EmissionMap_ST.zw;
                float3 emission = max(tex2D(_EmissionMap, emissionUV).rgb * _EmissionColor.rgb, 0.0);
                emission = max(emission, albedo * max(_BurtGIVoxelizeEmissionMode, 0.0));
                float3 boostedAlbedo = min(saturate(pow(albedo, max(_BurtGISceneVoxelLightingParams.x, 0.001))), 0.99);
                float3 normalWS = normalize(input.normalWS);
                float3 radiance = boostedAlbedo * 0.12 + emission;

                _BurtGISceneVoxelRadianceTexture[voxelCoord] = float4(radiance, 1.0);
                _BurtGISceneVoxelGeometryTexture[voxelCoord] = float4(normalWS * 0.5 + 0.5, 1.0);
                _BurtGISceneVoxelLightingTexture[voxelCoord] = float4(emission, 1.0);
                return 0.0;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
