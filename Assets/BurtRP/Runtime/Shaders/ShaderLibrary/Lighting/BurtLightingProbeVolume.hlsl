// Burt GI probe-volume sampling and probe-volume diffuse helpers.
#ifndef BURT_LIGHTING_PROBE_VOLUME_INCLUDED
#define BURT_LIGHTING_PROBE_VOLUME_INCLUDED

Texture3D<float4> _BurtGIProbeVolumeIrradianceTexture;
float4 _BurtGIProbeVolumeCenterExtent;
float4 _BurtGIProbeVolumeParams;
float4x4 _BurtGIProbeVolumeDirectWorldToLocal;
float4 _BurtGIProbeVolumeDirectHalfExtent;
StructuredBuffer<uint> _BurtGIProbeVolumeVirtualPageTable;
StructuredBuffer<uint3> _BurtGIProbeVolumeVirtualIndirection;
Texture3D<float4> _BurtGIProbeVolumeVirtualL0L1Rx;
Texture3D<float4> _BurtGIProbeVolumeVirtualL1GL1Ry;
Texture3D<float4> _BurtGIProbeVolumeVirtualL1BL1Rz;
Texture3D<float4> _BurtGIProbeVolumeVirtualL20;
Texture3D<float4> _BurtGIProbeVolumeVirtualL21;
Texture3D<float4> _BurtGIProbeVolumeVirtualL22;
Texture3D<float4> _BurtGIProbeVolumeVirtualL23;
Texture3D<float> _BurtGIProbeVolumeVirtualValidity;
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
float _BurtGIProbeVolumeVirtualSkyVisibilityOffset;
float4 _BurtGIProbeVolumeVirtualMainLightSHParams;
float _BurtGIProbeVolumeVirtualSkyShadingDirectionEnabled;
float4 _BurtGIProbeVolumeVirtualBufferCounts;
Texture3D<uint4> _BurtGISceneVoxelProbePageTable;
Texture3D<float4> _BurtGISceneVoxelProbeIrradianceSHAmbient;
Texture3D<float4> _BurtGISceneVoxelProbeIrradianceSHDirectional;
Texture3D<uint4> _BurtGISceneVoxelProbeLevel1PageTable;
Texture3D<uint4> _BurtGISceneVoxelProbeLevel2PageTable;
Texture3D<uint4> _BurtGISceneVoxelProbeLevel3PageTable;
Texture3D<uint4> _BurtGISceneVoxelProbeLevel4PageTable;
Texture3D<uint4> _BurtGISceneVoxelProbeLevel5PageTable;
Texture3D<float4> _BurtGISceneVoxelProbeLevel1IrradianceSHAmbient;
Texture3D<float4> _BurtGISceneVoxelProbeLevel2IrradianceSHAmbient;
Texture3D<float4> _BurtGISceneVoxelProbeLevel3IrradianceSHAmbient;
Texture3D<float4> _BurtGISceneVoxelProbeLevel4IrradianceSHAmbient;
Texture3D<float4> _BurtGISceneVoxelProbeLevel5IrradianceSHAmbient;
Texture3D<float4> _BurtGISceneVoxelProbeLevel1IrradianceSHDirectional;
Texture3D<float4> _BurtGISceneVoxelProbeLevel2IrradianceSHDirectional;
Texture3D<float4> _BurtGISceneVoxelProbeLevel3IrradianceSHDirectional;
Texture3D<float4> _BurtGISceneVoxelProbeLevel4IrradianceSHDirectional;
Texture3D<float4> _BurtGISceneVoxelProbeLevel5IrradianceSHDirectional;
StructuredBuffer<uint> _BurtGISceneVoxelProbeIndexBuffer;
float4 _BurtGISceneVoxelProbeParams; // x=enabled, y=intensity, z=probe node size, w=index offset per clipmap.
float4 _BurtGISceneVoxelProbeCenterExtent;
float4 _BurtGISceneVoxelProbeClipmapParams[6]; // x=enabled, y=probe node size, z=index offset, w=reserved.
float4 _BurtGISceneVoxelProbeClipmapCenterExtent[6];

bool BurtTryResolveGIProbeVolumeVirtualPoolUV(float3 PositionWS, float3 NormalWS, float3 ViewDirectionWS, out float3 PoolUV)
{
    PoolUV = 0.0f;
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
    PoolUV = saturate(((float3)PoolIndex + BrickOffset) * _BurtGIProbeVolumeVirtualPhysicalPoolDimensionsRcp.xyz);
    return true;
}

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
    float3 L0 = L0L1Rx.rgb;
    float3 L1R = 0.0f;
    float3 L1G = 0.0f;
    float3 L1B = 0.0f;
    float3 SafeNormalWS = normalize(NormalWS);
    bool HasL1 = _BurtGIProbeVolumeVirtualBufferCounts.w > 0.5f;
    if (HasL1)
    {
        float4 L1GL1Ry = _BurtGIProbeVolumeVirtualL1GL1Ry.SampleLevel(sampler_LinearClamp, PoolUV, 0.0f);
        float4 L1BL1Rz = _BurtGIProbeVolumeVirtualL1BL1Rz.SampleLevel(sampler_LinearClamp, PoolUV, 0.0f);
        L1R = (float3(L0L1Rx.a, L1GL1Ry.a, L1BL1Rz.a) - 0.5f) * (4.0f * L0.r);
        L1G = (L1GL1Ry.rgb - 0.5f) * (4.0f * L0.g);
        L1B = (L1BL1Rz.rgb - 0.5f) * (4.0f * L0.b);
    }

    bool HasL2 = HasL1 && _BurtGIProbeVolumeVirtualBiasL2.z > 0.5f;
    float4 L2R = 0.0f;
    float4 L2G = 0.0f;
    float4 L2B = 0.0f;
    float3 L2C = 0.0f;
    if (HasL2)
    {
        L2R = (_BurtGIProbeVolumeVirtualL20.SampleLevel(sampler_LinearClamp, PoolUV, 0.0f) - 0.5f) * (7.1554176f * L0.r);
        L2G = (_BurtGIProbeVolumeVirtualL21.SampleLevel(sampler_LinearClamp, PoolUV, 0.0f) - 0.5f) * (7.1554176f * L0.g);
        L2B = (_BurtGIProbeVolumeVirtualL22.SampleLevel(sampler_LinearClamp, PoolUV, 0.0f) - 0.5f) * (7.1554176f * L0.b);
        L2C = (_BurtGIProbeVolumeVirtualL23.SampleLevel(sampler_LinearClamp, PoolUV, 0.0f).rgb - 0.5f) * (7.1554176f * L0);
    }

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
    float3 SkyIrradianceContribution = 0.0f;

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
            float3 PoolTexelFloat = PoolUV * (float3)PhysicalPoolDimensions - 0.5f + 0.0001f;
            int3 PoolTexel = clamp((int3)PoolTexelFloat, 0, PhysicalPoolDimensions - 1);
            uint SkyDirectionIndex = (uint)round(saturate(_BurtGIProbeVolumeVirtualSkyShadingDirectionIndices.Load(int4(PoolTexel, 0)).r) * 255.0f);
            if (SkyDirectionIndex != 255u)
            {
                float3 SkyDirection = _BurtGIProbeVolumeVirtualSkyShadingDirections[SkyDirectionIndex];
                if (dot(SkyDirection, SkyDirection) >= 0.2f)
                {
                    SkyShadingNormalWS = normalize(SkyDirection);
                }
            }
        }

        float3 OccludedSkyIrradiance = BurtSampleIndirectDiffuseIrradiance(SkyShadingNormalWS) * SkyVisibility * max(_BurtGIProbeVolumeVirtualSkyVisibilityParams.rgb, 0.0f);
        SkyIrradianceContribution += max(OccludedSkyIrradiance, 0.0f);
    }
    else
    {
        float3 UnoccludedSkyIrradiance = BurtSampleIndirectDiffuseIrradiance(SafeNormalWS) * max(_BurtGIProbeVolumeVirtualSkyVisibilityParams.rgb, 0.0f);
        SkyIrradianceContribution += max(UnoccludedSkyIrradiance, 0.0f);
    }

    if (HasL2)
    {
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

    Irradiance *= max(_BurtGIProbeVolumeVirtualMainLightSHParams.rgb, 0.0f);
    Irradiance += SkyIrradianceContribution;
    Irradiance = max(Irradiance, 0.0f);
    return true;
}

uint4 BurtLoadGISceneVoxelProbeInfo(uint ProbeLevel, int3 ProbeIndex)
{
    if (ProbeLevel == 1u)
    {
        return _BurtGISceneVoxelProbeLevel1PageTable.Load(int4(ProbeIndex, 0));
    }

    if (ProbeLevel == 2u)
    {
        return _BurtGISceneVoxelProbeLevel2PageTable.Load(int4(ProbeIndex, 0));
    }

    if (ProbeLevel == 3u)
    {
        return _BurtGISceneVoxelProbeLevel3PageTable.Load(int4(ProbeIndex, 0));
    }

    if (ProbeLevel == 4u)
    {
        return _BurtGISceneVoxelProbeLevel4PageTable.Load(int4(ProbeIndex, 0));
    }

    if (ProbeLevel == 5u)
    {
        return _BurtGISceneVoxelProbeLevel5PageTable.Load(int4(ProbeIndex, 0));
    }

    return _BurtGISceneVoxelProbePageTable.Load(int4(ProbeIndex, 0));
}

float3 BurtEvaluateGISceneVoxelProbeDiffuseFromSH(
    float3 AmbientVector,
    float4 SHCoefficients0Red,
    float4 SHCoefficients1Red,
    float4 SHCoefficients0Green,
    float4 SHCoefficients1Green,
    float4 SHCoefficients0Blue,
    float4 SHCoefficients1Blue,
    float3 DirectionWS)
{
    const float BurtXGIShBasis0 = 0.28209479177387814f;
    const float BurtXGIShBasis1 = 0.4886025119029199f;
    const float BurtXGIShBasis2 = 1.092548430592079f;
    const float BurtXGIShBasis3 = 0.31539156525252f;
    const float BurtXGIShBasis4 = 0.5462742152960395f;
    const float L0Scale = BURT_PI;
    const float L1Scale = 2.0f * BURT_PI / 3.0f;
    const float L2Scale = BURT_PI * 0.25f;

    float3 Axis = BurtSafeNormalize(DirectionWS).zxy;
    float3 AxisSquared = Axis * Axis;
    float4 Transfer0 = float4(
        BurtXGIShBasis0 * L0Scale,
        -BurtXGIShBasis1 * Axis.y * L1Scale,
        BurtXGIShBasis1 * Axis.z * L1Scale,
        -BurtXGIShBasis1 * Axis.x * L1Scale);
    float4 Transfer1 = float4(
        BurtXGIShBasis2 * Axis.x * Axis.y * L2Scale,
        -BurtXGIShBasis2 * Axis.y * Axis.z * L2Scale,
        BurtXGIShBasis3 * (3.0f * AxisSquared.z - 1.0f) * L2Scale,
        -BurtXGIShBasis2 * Axis.x * Axis.z * L2Scale);
    float Transfer2 = BurtXGIShBasis4 * (AxisSquared.x - AxisSquared.y) * L2Scale;

    float3 Diffuse;
    Diffuse.r = dot(float4(AmbientVector.r, SHCoefficients0Red.xyz), Transfer0) +
        dot(float4(SHCoefficients0Red.w, SHCoefficients1Red.xyz), Transfer1) +
        SHCoefficients1Red.w * Transfer2;
    Diffuse.g = dot(float4(AmbientVector.g, SHCoefficients0Green.xyz), Transfer0) +
        dot(float4(SHCoefficients0Green.w, SHCoefficients1Green.xyz), Transfer1) +
        SHCoefficients1Green.w * Transfer2;
    Diffuse.b = dot(float4(AmbientVector.b, SHCoefficients0Blue.xyz), Transfer0) +
        dot(float4(SHCoefficients0Blue.w, SHCoefficients1Blue.xyz), Transfer1) +
        SHCoefficients1Blue.w * Transfer2;

    return max(Diffuse * BURT_INV_PI, 0.0f);
}

float3 BurtEvaluateGISceneVoxelProbeDiffuseForLevel(uint ProbeLevel, uint3 ProbeIndex, uint ProbeNodeSize, float3 DirectionWS)
{
    float3 AmbientVector = 0.0f;
    float4 SHCoefficients0Red = 0.0f;
    float4 SHCoefficients1Red = 0.0f;
    float4 SHCoefficients0Green = 0.0f;
    float4 SHCoefficients1Green = 0.0f;
    float4 SHCoefficients0Blue = 0.0f;
    float4 SHCoefficients1Blue = 0.0f;

    if (ProbeLevel == 1u)
    {
        AmbientVector = _BurtGISceneVoxelProbeLevel1IrradianceSHAmbient.Load(int4(ProbeIndex, 0)).rgb;
        SHCoefficients0Red = _BurtGISceneVoxelProbeLevel1IrradianceSHDirectional.Load(int4(ProbeIndex.x + 0u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
        SHCoefficients1Red = _BurtGISceneVoxelProbeLevel1IrradianceSHDirectional.Load(int4(ProbeIndex.x + 1u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
        SHCoefficients0Green = _BurtGISceneVoxelProbeLevel1IrradianceSHDirectional.Load(int4(ProbeIndex.x + 2u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
        SHCoefficients1Green = _BurtGISceneVoxelProbeLevel1IrradianceSHDirectional.Load(int4(ProbeIndex.x + 3u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
        SHCoefficients0Blue = _BurtGISceneVoxelProbeLevel1IrradianceSHDirectional.Load(int4(ProbeIndex.x + 4u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
        SHCoefficients1Blue = _BurtGISceneVoxelProbeLevel1IrradianceSHDirectional.Load(int4(ProbeIndex.x + 5u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
    }
    else if (ProbeLevel == 2u)
    {
        AmbientVector = _BurtGISceneVoxelProbeLevel2IrradianceSHAmbient.Load(int4(ProbeIndex, 0)).rgb;
        SHCoefficients0Red = _BurtGISceneVoxelProbeLevel2IrradianceSHDirectional.Load(int4(ProbeIndex.x + 0u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
        SHCoefficients1Red = _BurtGISceneVoxelProbeLevel2IrradianceSHDirectional.Load(int4(ProbeIndex.x + 1u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
        SHCoefficients0Green = _BurtGISceneVoxelProbeLevel2IrradianceSHDirectional.Load(int4(ProbeIndex.x + 2u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
        SHCoefficients1Green = _BurtGISceneVoxelProbeLevel2IrradianceSHDirectional.Load(int4(ProbeIndex.x + 3u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
        SHCoefficients0Blue = _BurtGISceneVoxelProbeLevel2IrradianceSHDirectional.Load(int4(ProbeIndex.x + 4u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
        SHCoefficients1Blue = _BurtGISceneVoxelProbeLevel2IrradianceSHDirectional.Load(int4(ProbeIndex.x + 5u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
    }
    else if (ProbeLevel == 3u)
    {
        AmbientVector = _BurtGISceneVoxelProbeLevel3IrradianceSHAmbient.Load(int4(ProbeIndex, 0)).rgb;
        SHCoefficients0Red = _BurtGISceneVoxelProbeLevel3IrradianceSHDirectional.Load(int4(ProbeIndex.x + 0u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
        SHCoefficients1Red = _BurtGISceneVoxelProbeLevel3IrradianceSHDirectional.Load(int4(ProbeIndex.x + 1u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
        SHCoefficients0Green = _BurtGISceneVoxelProbeLevel3IrradianceSHDirectional.Load(int4(ProbeIndex.x + 2u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
        SHCoefficients1Green = _BurtGISceneVoxelProbeLevel3IrradianceSHDirectional.Load(int4(ProbeIndex.x + 3u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
        SHCoefficients0Blue = _BurtGISceneVoxelProbeLevel3IrradianceSHDirectional.Load(int4(ProbeIndex.x + 4u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
        SHCoefficients1Blue = _BurtGISceneVoxelProbeLevel3IrradianceSHDirectional.Load(int4(ProbeIndex.x + 5u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
    }
    else if (ProbeLevel == 4u)
    {
        AmbientVector = _BurtGISceneVoxelProbeLevel4IrradianceSHAmbient.Load(int4(ProbeIndex, 0)).rgb;
        SHCoefficients0Red = _BurtGISceneVoxelProbeLevel4IrradianceSHDirectional.Load(int4(ProbeIndex.x + 0u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
        SHCoefficients1Red = _BurtGISceneVoxelProbeLevel4IrradianceSHDirectional.Load(int4(ProbeIndex.x + 1u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
        SHCoefficients0Green = _BurtGISceneVoxelProbeLevel4IrradianceSHDirectional.Load(int4(ProbeIndex.x + 2u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
        SHCoefficients1Green = _BurtGISceneVoxelProbeLevel4IrradianceSHDirectional.Load(int4(ProbeIndex.x + 3u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
        SHCoefficients0Blue = _BurtGISceneVoxelProbeLevel4IrradianceSHDirectional.Load(int4(ProbeIndex.x + 4u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
        SHCoefficients1Blue = _BurtGISceneVoxelProbeLevel4IrradianceSHDirectional.Load(int4(ProbeIndex.x + 5u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
    }
    else if (ProbeLevel == 5u)
    {
        AmbientVector = _BurtGISceneVoxelProbeLevel5IrradianceSHAmbient.Load(int4(ProbeIndex, 0)).rgb;
        SHCoefficients0Red = _BurtGISceneVoxelProbeLevel5IrradianceSHDirectional.Load(int4(ProbeIndex.x + 0u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
        SHCoefficients1Red = _BurtGISceneVoxelProbeLevel5IrradianceSHDirectional.Load(int4(ProbeIndex.x + 1u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
        SHCoefficients0Green = _BurtGISceneVoxelProbeLevel5IrradianceSHDirectional.Load(int4(ProbeIndex.x + 2u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
        SHCoefficients1Green = _BurtGISceneVoxelProbeLevel5IrradianceSHDirectional.Load(int4(ProbeIndex.x + 3u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
        SHCoefficients0Blue = _BurtGISceneVoxelProbeLevel5IrradianceSHDirectional.Load(int4(ProbeIndex.x + 4u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
        SHCoefficients1Blue = _BurtGISceneVoxelProbeLevel5IrradianceSHDirectional.Load(int4(ProbeIndex.x + 5u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
    }
    else
    {
        AmbientVector = _BurtGISceneVoxelProbeIrradianceSHAmbient.Load(int4(ProbeIndex, 0)).rgb;
        SHCoefficients0Red = _BurtGISceneVoxelProbeIrradianceSHDirectional.Load(int4(ProbeIndex.x + 0u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
        SHCoefficients1Red = _BurtGISceneVoxelProbeIrradianceSHDirectional.Load(int4(ProbeIndex.x + 1u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
        SHCoefficients0Green = _BurtGISceneVoxelProbeIrradianceSHDirectional.Load(int4(ProbeIndex.x + 2u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
        SHCoefficients1Green = _BurtGISceneVoxelProbeIrradianceSHDirectional.Load(int4(ProbeIndex.x + 3u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
        SHCoefficients0Blue = _BurtGISceneVoxelProbeIrradianceSHDirectional.Load(int4(ProbeIndex.x + 4u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
        SHCoefficients1Blue = _BurtGISceneVoxelProbeIrradianceSHDirectional.Load(int4(ProbeIndex.x + 5u * ProbeNodeSize, ProbeIndex.y, ProbeIndex.z, 0));
    }

    return BurtEvaluateGISceneVoxelProbeDiffuseFromSH(
        AmbientVector,
        SHCoefficients0Red,
        SHCoefficients1Red,
        SHCoefficients0Green,
        SHCoefficients1Green,
        SHCoefficients0Blue,
        SHCoefficients1Blue,
        DirectionWS);
}

float3 BurtEvaluateGISceneVoxelProbeDiffuse(uint3 ProbeIndex, uint ProbeNodeSize, float3 DirectionWS)
{
    return BurtEvaluateGISceneVoxelProbeDiffuseForLevel(0u, ProbeIndex, ProbeNodeSize, DirectionWS);
}

bool BurtTrySampleGISceneVoxelProbeIrradianceFromLevel(
    uint ProbeLevel,
    float4 ProbeCenterExtent,
    float4 ProbeParams,
    float3 PositionWS,
    float3 NormalWS,
    float3 ViewDirectionWS,
    out float3 Irradiance)
{
    Irradiance = 0.0f;
    if (ProbeParams.x < 0.5f)
    {
        return false;
    }

    uint ProbeNodeSize = (uint)max(round(ProbeParams.y), 1.0f);
    float Extent = max(ProbeCenterExtent.w, 0.0001f);
    float3 Position01 = (PositionWS - ProbeCenterExtent.xyz) / (Extent * 2.0f) + 0.5f;
    if (any(Position01 < 0.0f) || any(Position01 > 1.0f))
    {
        return false;
    }

    float3 PositionIndexFloat = Position01 * (float)ProbeNodeSize - 0.5f;
    int3 CornerProbeIndex = (int3)floor(PositionIndexFloat);
    float3 LerpAlphas = saturate(frac(PositionIndexFloat));
    int3 MaxProbeIndex = int3((int)ProbeNodeSize - 1, (int)ProbeNodeSize - 1, (int)ProbeNodeSize - 1);
    float3 SafeNormalWS = BurtSafeNormalize(NormalWS);

    float WeightSum = 0.0f;
    [unroll]
    for (int z = 0; z < 2; ++z)
    {
        [unroll]
        for (int y = 0; y < 2; ++y)
        {
            [unroll]
            for (int x = 0; x < 2; ++x)
            {
                int3 ProbeIndex = clamp(CornerProbeIndex + int3(x, y, z), int3(0, 0, 0), MaxProbeIndex);
                uint4 ProbeInfo = BurtLoadGISceneVoxelProbeInfo(ProbeLevel, ProbeIndex);
                if (ProbeInfo.w == 0u)
                {
                    continue;
                }

                float3 ProbeOffset01 = ((float3)min(ProbeInfo.xyz, uint3(3u, 3u, 3u)) + 0.5f) * 0.25f;
                float3 ProbePosition01 = ((float3)ProbeIndex + ProbeOffset01) / max((float)ProbeNodeSize, 1.0f);
                float3 ProbePositionWS = ProbeCenterExtent.xyz + (ProbePosition01 - 0.5f) * (Extent * 2.0f);
                float3 SampleDirectionWS = BurtSafeNormalize(ProbePositionWS - PositionWS);
                if (dot(SampleDirectionWS, SafeNormalWS) < 0.0f)
                {
                    continue;
                }

                float3 CornerWeight = lerp(1.0f - LerpAlphas, LerpAlphas, float3((float)x, (float)y, (float)z));
                float Weight = CornerWeight.x * CornerWeight.y * CornerWeight.z;
                Irradiance += BurtEvaluateGISceneVoxelProbeDiffuseForLevel(
                    ProbeLevel,
                    (uint3)ProbeIndex,
                    ProbeNodeSize,
                    SafeNormalWS) * Weight;
                WeightSum += Weight;
            }
        }
    }

    if (WeightSum <= 0.0001f)
    {
        return false;
    }

    Irradiance = max(Irradiance / WeightSum, 0.0f) * max(_BurtGISceneVoxelProbeParams.y, 0.0f);
    return any(Irradiance > 0.00001f);
}

bool BurtTrySampleGISceneVoxelProbeIrradiance(float3 PositionWS, float3 NormalWS, float3 ViewDirectionWS, out float3 Irradiance)
{
    Irradiance = 0.0f;
    if (_BurtGISceneVoxelProbeParams.x < 0.5f)
    {
        return false;
    }

    if (BurtTrySampleGISceneVoxelProbeIrradianceFromLevel(
            1u,
            _BurtGISceneVoxelProbeClipmapCenterExtent[1],
            _BurtGISceneVoxelProbeClipmapParams[1],
            PositionWS,
            NormalWS,
            ViewDirectionWS,
            Irradiance))
    {
        return true;
    }

    if (BurtTrySampleGISceneVoxelProbeIrradianceFromLevel(
            2u,
            _BurtGISceneVoxelProbeClipmapCenterExtent[2],
            _BurtGISceneVoxelProbeClipmapParams[2],
            PositionWS,
            NormalWS,
            ViewDirectionWS,
            Irradiance))
    {
        return true;
    }

    if (BurtTrySampleGISceneVoxelProbeIrradianceFromLevel(
            3u,
            _BurtGISceneVoxelProbeClipmapCenterExtent[3],
            _BurtGISceneVoxelProbeClipmapParams[3],
            PositionWS,
            NormalWS,
            ViewDirectionWS,
            Irradiance))
    {
        return true;
    }

    if (BurtTrySampleGISceneVoxelProbeIrradianceFromLevel(
            4u,
            _BurtGISceneVoxelProbeClipmapCenterExtent[4],
            _BurtGISceneVoxelProbeClipmapParams[4],
            PositionWS,
            NormalWS,
            ViewDirectionWS,
            Irradiance))
    {
        return true;
    }

    if (BurtTrySampleGISceneVoxelProbeIrradianceFromLevel(
            5u,
            _BurtGISceneVoxelProbeClipmapCenterExtent[5],
            _BurtGISceneVoxelProbeClipmapParams[5],
            PositionWS,
            NormalWS,
            ViewDirectionWS,
            Irradiance))
    {
        return true;
    }

    return BurtTrySampleGISceneVoxelProbeIrradianceFromLevel(
        0u,
        _BurtGISceneVoxelProbeCenterExtent,
        float4(_BurtGISceneVoxelProbeParams.x, _BurtGISceneVoxelProbeParams.z, _BurtGISceneVoxelProbeParams.w, 0.0f),
        PositionWS,
        NormalWS,
        ViewDirectionWS,
        Irradiance);
}

bool BurtTrySampleGIProbeVolumeIrradiance(float3 PositionWS, float3 NormalWS, float3 ViewDirectionWS, out float3 Irradiance)
{
    Irradiance = 0.0f;
    if (_BurtGIProbeVolumeParams.x < 0.5f)
    {
        return BurtTrySampleGISceneVoxelProbeIrradiance(PositionWS, NormalWS, ViewDirectionWS, Irradiance);
    }

    if (BurtTrySampleGIProbeVolumeVirtualIrradiance(PositionWS, NormalWS, ViewDirectionWS, Irradiance))
    {
        Irradiance *= _BurtGIProbeVolumeParams.y;
        return true;
    }

    if (_BurtGIProbeVolumeParams.w >= 1.5f)
    {
        return BurtTrySampleGISceneVoxelProbeIrradiance(PositionWS, NormalWS, ViewDirectionWS, Irradiance);
    }

    float3 HalfExtent = max(_BurtGIProbeVolumeDirectHalfExtent.xyz, 0.0001f);
    if (any(_BurtGIProbeVolumeDirectHalfExtent.xyz <= 0.0001f))
    {
        return BurtTrySampleGISceneVoxelProbeIrradiance(PositionWS, NormalWS, ViewDirectionWS, Irradiance);
    }

    float3 LocalPosition = mul(_BurtGIProbeVolumeDirectWorldToLocal, float4(PositionWS, 1.0f)).xyz / HalfExtent;
    float3 AbsoluteLocalPosition = abs(LocalPosition);
    if (any(AbsoluteLocalPosition >= 1.0f))
    {
        return BurtTrySampleGISceneVoxelProbeIrradiance(PositionWS, NormalWS, ViewDirectionWS, Irradiance);
    }

    float EdgeDistance = min(min(1.0f - AbsoluteLocalPosition.x, 1.0f - AbsoluteLocalPosition.y), 1.0f - AbsoluteLocalPosition.z);
    float EdgeWeight = smoothstep(0.0f, 1.0f, EdgeDistance * _BurtGIProbeVolumeParams.z);
    float3 VolumeUV = LocalPosition * 0.5f + 0.5f;
    Irradiance = max(_BurtGIProbeVolumeIrradianceTexture.SampleLevel(sampler_LinearClamp, VolumeUV, 0.0f).rgb, 0.0f);
    Irradiance *= _BurtGIProbeVolumeParams.y * EdgeWeight;
    return true;
}

bool BurtTrySampleGIProbeVolumeVirtualDebugChannels(float3 PositionWS, float3 NormalWS, float3 ViewDirectionWS, out float Validity, out float SkyVisibility)
{
    Validity = 0.0f;
    SkyVisibility = 0.0f;
    if (_BurtGIProbeVolumeParams.w < 1.5f)
    {
        return false;
    }

    float3 PoolUV;
    if (!BurtTryResolveGIProbeVolumeVirtualPoolUV(PositionWS, NormalWS, ViewDirectionWS, PoolUV))
    {
        return false;
    }

    Validity = _BurtGIProbeVolumeVirtualBufferCounts.z > 0.5f
        ? saturate(_BurtGIProbeVolumeVirtualValidity.SampleLevel(sampler_LinearClamp, PoolUV, 0.0f))
        : 1.0f;
    if (_BurtGIProbeVolumeVirtualBiasL2.w > 0.5f)
    {
        const float BurtXGIShBasis0 = 0.28209479177387814f;
        const float BurtXGIShBasis1 = 0.4886025119029199f;
        float3 SafeNormalWS = BurtSafeNormalize(NormalWS);
        float4 SkyVisibilityL0L1 = _BurtGIProbeVolumeVirtualSkyVisibilityL0L1.SampleLevel(sampler_LinearClamp, PoolUV, 0.0f);
        float4 SkyVisibilityBasis = float4(
            BurtXGIShBasis0,
            BurtXGIShBasis1 * SafeNormalWS.x,
            BurtXGIShBasis1 * SafeNormalWS.y,
            BurtXGIShBasis1 * SafeNormalWS.z);
        SkyVisibility = saturate(lerp(
            1.0f,
            dot(SkyVisibilityBasis, SkyVisibilityL0L1),
            _BurtGIProbeVolumeVirtualSkyVisibilityParams.w));
    }
    else
    {
        SkyVisibility = 1.0f;
    }

    return true;
}

bool BurtTrySampleGIProbeVolumeDebugChannels(float3 PositionWS, float3 NormalWS, float3 ViewDirectionWS, out float Validity, out float SkyVisibility)
{
    Validity = 0.0f;
    SkyVisibility = 0.0f;
    if (_BurtGIProbeVolumeParams.x < 0.5f)
    {
        return false;
    }

    if (_BurtGIProbeVolumeParams.w >= 1.5f)
    {
        return BurtTrySampleGIProbeVolumeVirtualDebugChannels(PositionWS, NormalWS, ViewDirectionWS, Validity, SkyVisibility);
    }

    float3 Irradiance;
    if (!BurtTrySampleGIProbeVolumeIrradiance(PositionWS, NormalWS, ViewDirectionWS, Irradiance))
    {
        return false;
    }

    Validity = 1.0f;
    SkyVisibility = 1.0f;
    return true;
}

bool BurtTrySampleGIProbeVolumeDebugData(
    float3 PositionWS,
    float3 NormalWS,
    float3 ViewDirectionWS,
    out float3 Irradiance,
    out float Validity,
    out float SkyVisibility)
{
    bool Sampled = BurtTrySampleGIProbeVolumeIrradiance(PositionWS, NormalWS, ViewDirectionWS, Irradiance);
    Validity = Sampled ? 1.0f : 0.0f;
    SkyVisibility = Sampled ? 1.0f : 0.0f;
    if (!Sampled || _BurtGIProbeVolumeParams.x < 0.5f || _BurtGIProbeVolumeParams.w < 1.5f)
    {
        return Sampled;
    }

    float VirtualValidity;
    float VirtualSkyVisibility;
    if (!BurtTrySampleGIProbeVolumeVirtualDebugChannels(PositionWS, NormalWS, ViewDirectionWS, VirtualValidity, VirtualSkyVisibility))
    {
        return true;
    }

    Validity = VirtualValidity;
    SkyVisibility = VirtualSkyVisibility;

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
