// XRender-compatible pre-skin object-space position helpers.
#ifndef BURT_PRESKIN_POSITION_INCLUDED
#define BURT_PRESKIN_POSITION_INCLUDED

#define BURT_PRESKIN_POSITION_MIN_OS (-16.0f)
#define BURT_PRESKIN_POSITION_MAX_OS (16.0f)
#define BURT_PRESKIN_POSITION_PACKED_MAP (2097152)

#if defined(BURT_PRESKIN_POSITION_PACKED) || defined(XSKIN_MESH_COMPRESSED)
    #define BURT_PRESKIN_POSITION_UV3_PACKED 1
#else
    #define BURT_PRESKIN_POSITION_UV3_PACKED 0
#endif

float3 BurtDecodePreSkinPositionOS(float3 PreSkinPositionOS)
{
    return PreSkinPositionOS;
}

#if BURT_PRESKIN_POSITION_UV3_PACKED
float3 BurtUnpackPreSkinPositionOS(uint2 PackedPosition)
{
    uint3 PositionFP = uint3(
        PackedPosition.x >> 11,
        ((PackedPosition.x & 0x07FF) << 10) | (PackedPosition.y >> 22),
        PackedPosition.y & 0x001FFFFF);

    return BURT_PRESKIN_POSITION_MIN_OS
        + (float3)PositionFP / (float)(BURT_PRESKIN_POSITION_PACKED_MAP - 1)
        * (BURT_PRESKIN_POSITION_MAX_OS - BURT_PRESKIN_POSITION_MIN_OS);
}

float3 BurtDecodePreSkinPositionOS(uint2 PackedPreSkinPosition)
{
    return BurtUnpackPreSkinPositionOS(PackedPreSkinPosition);
}
#endif

float3 BurtEncodePreSkinPositionForDebug(float3 PreSkinPositionOS)
{
    return saturate(
        (PreSkinPositionOS - BURT_PRESKIN_POSITION_MIN_OS)
        / (BURT_PRESKIN_POSITION_MAX_OS - BURT_PRESKIN_POSITION_MIN_OS));
}

#endif // BURT_PRESKIN_POSITION_INCLUDED
