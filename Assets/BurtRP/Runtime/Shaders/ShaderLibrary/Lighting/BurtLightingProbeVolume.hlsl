// Burt GI probe-volume sampling and probe-volume diffuse helpers.
#ifndef BURT_LIGHTING_PROBE_VOLUME_INCLUDED
#define BURT_LIGHTING_PROBE_VOLUME_INCLUDED

Texture3D<float4> _BurtGIProbeVolumeIrradianceTexture;
float4 _BurtGIProbeVolumeCenterExtent;
float4 _BurtGIProbeVolumeParams;
StructuredBuffer<uint> _BurtGIProbeVolumeVirtualPageTable;
StructuredBuffer<uint3> _BurtGIProbeVolumeVirtualIndirection;
Texture3D<float4> _BurtGIProbeVolumeVirtualL0L1Rx;
Texture3D<float4> _BurtGIProbeVolumeVirtualL1GL1Ry;
Texture3D<float4> _BurtGIProbeVolumeVirtualL1BL1Rz;
Texture3D<float4> _BurtGIProbeVolumeVirtualL20;
Texture3D<float4> _BurtGIProbeVolumeVirtualL21;
Texture3D<float4> _BurtGIProbeVolumeVirtualL22;
Texture3D<float4> _BurtGIProbeVolumeVirtualL23;
Texture3D<float4> _BurtGIProbeVolumeVirtualSkyVisibilityL0L1;
Texture3D<float4> _BurtGIProbeVolumeVirtualSkyShadingDirectionIndices;
StructuredBuffer<float3> _BurtGIProbeVolumeVirtualSkyShadingDirections;
float4 _BurtGIProbeVolumeVirtualPosOffsetMinBrickSize;
float4 _BurtGIProbeVolumeVirtualIndirectionDimensions;
float4 _BurtGIProbeVolumeVirtualMinLoadedEntry;
float4 _BurtGIProbeVolumeVirtualMaxLoadedEntry;
float4 _BurtGIProbeVolumeVirtualMinEntryIndexEntrySize;
float4 _BurtGIProbeVolumeVirtualPhysicalPoolDimensions;
float4 _BurtGIProbeVolumeVirtualPhysicalPoolDimensionsRcp;
float4 _BurtGIProbeVolumeVirtualBiasL2;
float4 _BurtGIProbeVolumeVirtualSkyVisibilityParams;
float _BurtGIProbeVolumeVirtualSkyShadingDirectionEnabled;
float4 _BurtGIProbeVolumeVirtualBufferCounts;

bool BurtTrySampleGIProbeVolumeVirtualIrradiance(float3 PositionWS, float3 NormalWS, float3 ViewDirectionWS, out float3 Irradiance)
{
    Irradiance = 0.0f;
    if (_BurtGIProbeVolumeParams.w < 1.5f)
    {
        return false;
    }

    float3 BiasedPositionWS = PositionWS - _BurtGIProbeVolumeVirtualPosOffsetMinBrickSize.xyz;
    BiasedPositionWS += NormalWS * _BurtGIProbeVolumeVirtualBiasL2.x;
    BiasedPositionWS += ViewDirectionWS * _BurtGIProbeVolumeVirtualBiasL2.y;
    float EntrySizeWS = max(_BurtGIProbeVolumeVirtualMinEntryIndexEntrySize.w, 0.0001f);
    int3 EntryIndexWS = (int3)floor(BiasedPositionWS / EntrySizeWS);
    int3 MinLoadedEntry = (int3)_BurtGIProbeVolumeVirtualMinLoadedEntry.xyz;
    int3 MaxLoadedEntry = (int3)_BurtGIProbeVolumeVirtualMaxLoadedEntry.xyz;
    if (any(EntryIndexWS < MinLoadedEntry) || any(EntryIndexWS > MaxLoadedEntry))
    {
        return false;
    }

    int3 EntryIndexES = EntryIndexWS - (int3)_BurtGIProbeVolumeVirtualMinEntryIndexEntrySize.xyz;
    int3 IndirectionDimensions = max((int3)_BurtGIProbeVolumeVirtualIndirectionDimensions.xyz, int3(1, 1, 1));
    if (any(EntryIndexES < 0) || any(EntryIndexES >= IndirectionDimensions))
    {
        return false;
    }

    uint EntryFlatIndex = (uint)(EntryIndexES.x + EntryIndexES.y * IndirectionDimensions.x + EntryIndexES.z * IndirectionDimensions.x * IndirectionDimensions.y);
    if (EntryFlatIndex >= (uint)max(_BurtGIProbeVolumeVirtualBufferCounts.x, 0.0f))
    {
        return false;
    }

    uint3 MetaData = _BurtGIProbeVolumeVirtualIndirection[EntryFlatIndex];
    if (MetaData.x == 0xffffffffu)
    {
        return false;
    }

    uint ChunkIndex = MetaData.x & 0x1fffffffu;
    int BrickSize = (int)round(pow(3.0f, (float)((MetaData.x >> 29) & 0x7u)));
    int3 MinRelativeIndex = int3(MetaData.y & 0x3ffu, (MetaData.y >> 10) & 0x3ffu, (MetaData.y >> 20) & 0x3ffu);
    int3 MaxRelativeIndexPlusOne = int3(MetaData.z & 0x3ffu, (MetaData.z >> 10) & 0x3ffu, (MetaData.z >> 20) & 0x3ffu);
    float3 EntryCornerPositionWS = (float3)EntryIndexWS * EntrySizeWS;
    int3 BrickIndexES = (int3)floor((BiasedPositionWS - EntryCornerPositionWS) / max(_BurtGIProbeVolumeVirtualPosOffsetMinBrickSize.w * BrickSize, 0.0001f));
    BrickIndexES = min(BrickIndexES, int3(26, 26, 26));
    if (any(BrickIndexES < MinRelativeIndex) || any(BrickIndexES >= MaxRelativeIndexPlusOne))
    {
        return false;
    }

    int3 ValidBrickDimensions = MaxRelativeIndexPlusOne - MinRelativeIndex;
    int3 BrickIndexVS = BrickIndexES - MinRelativeIndex;
    uint BrickFlatIndex = (uint)(BrickIndexVS.x * ValidBrickDimensions.y + BrickIndexVS.y + BrickIndexVS.z * ValidBrickDimensions.x * ValidBrickDimensions.y);
    uint PageTableIndex = ChunkIndex * 243u + BrickFlatIndex;
    if (PageTableIndex >= (uint)max(_BurtGIProbeVolumeVirtualBufferCounts.y, 0.0f))
    {
        return false;
    }

    uint PhysicalLocationPacked = _BurtGIProbeVolumeVirtualPageTable[PageTableIndex];
    if (PhysicalLocationPacked == 0xffffffffu)
    {
        return false;
    }

    uint Subdivision = (PhysicalLocationPacked >> 28) & 0xfu;
    uint PhysicalLocation = PhysicalLocationPacked & 0x0fffffffu;
    uint3 PoolDimensions = (uint3)max((int3)_BurtGIProbeVolumeVirtualPhysicalPoolDimensions.xyz, int3(1, 1, 1));
    uint PhysicalPoolElementCount = PoolDimensions.x * PoolDimensions.y * PoolDimensions.z;
    if (PhysicalLocation >= PhysicalPoolElementCount)
    {
        return false;
    }

    uint3 PoolIndex;
    PoolIndex.z = PhysicalLocation / (PoolDimensions.x * PoolDimensions.y);
    PhysicalLocation -= PoolIndex.z * PoolDimensions.x * PoolDimensions.y;
    PoolIndex.y = PhysicalLocation / PoolDimensions.x;
    PoolIndex.x = PhysicalLocation - PoolIndex.y * PoolDimensions.x;

    float BrickSizeWS = pow(3.0f, (float)Subdivision) * _BurtGIProbeVolumeVirtualPosOffsetMinBrickSize.w;
    float3 BrickOffset = frac(BiasedPositionWS / max(BrickSizeWS, 0.0001f)) * 3.0f + 0.5f;
    float3 PoolUV = saturate(((float3)PoolIndex + BrickOffset) * _BurtGIProbeVolumeVirtualPhysicalPoolDimensionsRcp.xyz);

    float4 L0L1Rx = _BurtGIProbeVolumeVirtualL0L1Rx.SampleLevel(sampler_LinearClamp, PoolUV, 0.0f);
    float4 L1GL1Ry = _BurtGIProbeVolumeVirtualL1GL1Ry.SampleLevel(sampler_LinearClamp, PoolUV, 0.0f);
    float4 L1BL1Rz = _BurtGIProbeVolumeVirtualL1BL1Rz.SampleLevel(sampler_LinearClamp, PoolUV, 0.0f);
    float3 L0 = L0L1Rx.rgb;
    float3 L1R = (float3(L0L1Rx.a, L1GL1Ry.a, L1BL1Rz.a) - 0.5f) * (4.0f * L0.r);
    float3 L1G = (L1GL1Ry.rgb - 0.5f) * (4.0f * L0.g);
    float3 L1B = (L1BL1Rz.rgb - 0.5f) * (4.0f * L0.b);
    float3 SafeNormalWS = normalize(NormalWS);

    // XRender evaluates encoded XGI SH in its zxy coordinate convention.
    const float BurtXGIShBasis0 = 0.28209479177387814f;
    const float BurtXGIShBasis1 = 0.4886025119029199f;
    const float BurtXGIShBasis2 = 1.092548430592079f;
    const float BurtXGIShBasis3 = 0.31539156525252f;
    const float BurtXGIShBasis4 = 0.5462742152960395f;
    float3 XGISHAxis = SafeNormalWS.zxy;
    float3 XGISHAxisSquared = XGISHAxis * XGISHAxis;
    float4 SHBasisL0L1 = float4(
        BurtXGIShBasis0,
        -BurtXGIShBasis1 * XGISHAxis.y,
        BurtXGIShBasis1 * XGISHAxis.z,
        -BurtXGIShBasis1 * XGISHAxis.x);
    Irradiance = float3(dot(float4(L0.r, L1R.x, L1R.y, L1R.z), SHBasisL0L1),
                       dot(float4(L0.g, L1G.x, L1G.y, L1G.z), SHBasisL0L1),
                       dot(float4(L0.b, L1B.x, L1B.y, L1B.z), SHBasisL0L1));

    if (_BurtGIProbeVolumeVirtualBiasL2.w > 0.5f)
    {
        float4 SkyVisibilityL0L1 = _BurtGIProbeVolumeVirtualSkyVisibilityL0L1.SampleLevel(sampler_LinearClamp, PoolUV, 0.0f);
        float4 SkyVisibilityBasis = float4(
            BurtXGIShBasis0,
            BurtXGIShBasis1 * SafeNormalWS.x,
            BurtXGIShBasis1 * SafeNormalWS.y,
            BurtXGIShBasis1 * SafeNormalWS.z);
        float SkyVisibility = lerp(1.0f, dot(SkyVisibilityBasis, SkyVisibilityL0L1), _BurtGIProbeVolumeVirtualSkyVisibilityParams.w);
        float3 SkyShadingNormalWS = SafeNormalWS;
        if (_BurtGIProbeVolumeVirtualSkyShadingDirectionEnabled > 0.5f)
        {
            int3 PhysicalPoolDimensions = max((int3)_BurtGIProbeVolumeVirtualPhysicalPoolDimensions.xyz, 1);
            int3 PoolTexel = clamp((int3)floor(PoolUV * PhysicalPoolDimensions), 0, PhysicalPoolDimensions - 1);
            uint SkyDirectionIndex = (uint)round(saturate(_BurtGIProbeVolumeVirtualSkyShadingDirectionIndices.Load(int4(PoolTexel, 0)).r) * 255.0f);
            if (SkyDirectionIndex != 255u)
            {
                float3 SkyDirection = _BurtGIProbeVolumeVirtualSkyShadingDirections[SkyDirectionIndex];
                if (dot(SkyDirection, SkyDirection) > 0.0001f)
                {
                    SkyShadingNormalWS = normalize(SkyDirection);
                }
            }
        }

        float3 OccludedSkyIrradiance = BurtSampleIndirectDiffuseIrradiance(SkyShadingNormalWS) * SkyVisibility * max(_BurtGIProbeVolumeVirtualSkyVisibilityParams.rgb, 0.0f);
        Irradiance += max(OccludedSkyIrradiance, 0.0f);
    }

    if (_BurtGIProbeVolumeVirtualBiasL2.z > 0.5f)
    {
        float4 L2R = (_BurtGIProbeVolumeVirtualL20.SampleLevel(sampler_LinearClamp, PoolUV, 0.0f) - 0.5f) * (7.1554176f * L0.r);
        float4 L2G = (_BurtGIProbeVolumeVirtualL21.SampleLevel(sampler_LinearClamp, PoolUV, 0.0f) - 0.5f) * (7.1554176f * L0.g);
        float4 L2B = (_BurtGIProbeVolumeVirtualL22.SampleLevel(sampler_LinearClamp, PoolUV, 0.0f) - 0.5f) * (7.1554176f * L0.b);
        float3 L2C = (_BurtGIProbeVolumeVirtualL23.SampleLevel(sampler_LinearClamp, PoolUV, 0.0f).rgb - 0.5f) * (7.1554176f * L0);
        float4 SHBasisL2 = float4(
            BurtXGIShBasis2 * XGISHAxis.x * XGISHAxis.y,
            -BurtXGIShBasis2 * XGISHAxis.y * XGISHAxis.z,
            BurtXGIShBasis3 * (3.0f * XGISHAxisSquared.z - 1.0f),
            -BurtXGIShBasis2 * XGISHAxis.x * XGISHAxis.z);
        float SHBasisL2C = BurtXGIShBasis4 * (XGISHAxisSquared.x - XGISHAxisSquared.y);
        Irradiance += float3(dot(L2R, SHBasisL2) + L2C.x * SHBasisL2C,
                             dot(L2G, SHBasisL2) + L2C.y * SHBasisL2C,
                             dot(L2B, SHBasisL2) + L2C.z * SHBasisL2C);
    }

    Irradiance = max(Irradiance, 0.0f);
    return true;
}

bool BurtTrySampleGIProbeVolumeIrradiance(float3 PositionWS, float3 NormalWS, float3 ViewDirectionWS, out float3 Irradiance)
{
    Irradiance = 0.0f;
    if (_BurtGIProbeVolumeParams.x < 0.5f)
    {
        return false;
    }

    if (BurtTrySampleGIProbeVolumeVirtualIrradiance(PositionWS, NormalWS, ViewDirectionWS, Irradiance))
    {
        Irradiance *= _BurtGIProbeVolumeParams.y;
        return true;
    }

    if (_BurtGIProbeVolumeParams.w >= 1.5f)
    {
        return false;
    }

    if (_BurtGIProbeVolumeCenterExtent.w <= 0.0001f)
    {
        return false;
    }

    float3 LocalPosition = (PositionWS - _BurtGIProbeVolumeCenterExtent.xyz) / _BurtGIProbeVolumeCenterExtent.www;
    float3 AbsoluteLocalPosition = abs(LocalPosition);
    if (any(AbsoluteLocalPosition >= 1.0f))
    {
        return false;
    }

    float EdgeDistance = min(min(1.0f - AbsoluteLocalPosition.x, 1.0f - AbsoluteLocalPosition.y), 1.0f - AbsoluteLocalPosition.z);
    float EdgeWeight = smoothstep(0.0f, 1.0f, EdgeDistance * _BurtGIProbeVolumeParams.z);
    float3 VolumeUV = LocalPosition * 0.5f + 0.5f;
    Irradiance = max(_BurtGIProbeVolumeIrradianceTexture.SampleLevel(sampler_LinearClamp, VolumeUV, 0.0f).rgb, 0.0f);
    Irradiance *= _BurtGIProbeVolumeParams.y * EdgeWeight;
    return true;
}

float3 BurtEvaluateGIProbeVolumeIrradiance(float3 PositionWS, float3 NormalWS, float3 ViewDirectionWS)
{
    float3 Irradiance;
    return BurtTrySampleGIProbeVolumeIrradiance(PositionWS, NormalWS, ViewDirectionWS, Irradiance) ? Irradiance : 0.0f;
}

bool BurtTryEvaluateGIProbeVolumeIndirectDiffuse(
    BurtPBRMaterialData MaterialData,
    float3 PositionWS,
    float3 NormalWS,
    float3 ViewDirectionWS,
    float EnergyPreservation,
    out float3 Diffuse)
{
    float3 Irradiance;
    if (!BurtTrySampleGIProbeVolumeIrradiance(PositionWS, NormalWS, ViewDirectionWS, Irradiance))
    {
        Diffuse = 0.0f;
        return false;
    }

    Diffuse = MaterialData.DiffuseColor * Irradiance * BurtGTAOMultiBounce(MaterialData.Occlusion, MaterialData.BaseColor) * saturate(EnergyPreservation);
    return true;
}

#endif
