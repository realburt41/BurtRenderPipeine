#ifndef BURT_GI_HASH_GRID_ADDRESS_INCLUDED
#define BURT_GI_HASH_GRID_ADDRESS_INCLUDED
// x angular footprint, y orthographic world footprint, z orthographic,
// w surface-key mode. Legacy producers/readers remain a separate contract.
float4 _BurtGIRadianceCacheHashGridAddressParams;
uint BurtGIHashGridProbeCount(uint tileCount, uint tilesPerBucket)
{
    // Keep insertion and every reader identical. Native surface workloads can
    // exhaust 16 adjacent slots even while half the table is empty. A bounded
    // 32-slot window avoids those observed clusters without enlarging cells.
    return min(tileCount, tilesPerBucket *
        (_BurtGIRadianceCacheHashGridAddressParams.w > 0.5 ? 8u : 4u));
}
struct BurtGIHashSurfaceAddress
{
    uint hash;
    uint2 cell;
    float cellSize;
    float3 cellCenter;
};
uint BurtGIHashAddressXX(uint p)
{
    uint h=p+374761393u;
    h=668265263u*((h<<17)|(h>>15));
    h=2246822519u*(h^(h>>15));
    h=3266489917u*(h^(h>>13));
    return h^(h>>16);
}
float BurtGIHashSurfaceCellSize(float3 eye, float3 hit)
{
    float footprint=_BurtGIRadianceCacheHashGridAddressParams.z>0.5
        ? _BurtGIRadianceCacheHashGridAddressParams.y
        : distance(eye,hit)*_BurtGIRadianceCacheHashGridAddressParams.x;
    // XRender's logarithmic camera footprint; finite minimum at the eye.
    return 0.001*exp2(floor(log2(max(1.0,1000.0*footprint))));
}
BurtGIHashSurfaceAddress BurtGIGetHashSurfaceAddress(float3 eye, float3 hit,
    float3 direction, float hitDistance, uint ratio)
{
    BurtGIHashSurfaceAddress a;
    a.cellSize=BurtGIHashSurfaceCellSize(eye,hit);
    float tileSize=a.cellSize*ratio;
    int3 tile=(int3)floor(hit/tileSize);
    uint3 c=asuint(tile);
    uint3 d=asuint((int3)floor(0.5+(0.5*direction+0.5)*4.0));
    uint level=(uint)(int)round(log2(a.cellSize*1000.0));
    uint proximity=hitDistance<tileSize?1u:0u;
    uint h=BurtGIHashAddressXX(level+BurtGIHashAddressXX(c.x+BurtGIHashAddressXX(c.y+
        BurtGIHashAddressXX(c.z+BurtGIHashAddressXX(d.x+BurtGIHashAddressXX(d.y+
        BurtGIHashAddressXX(d.z+BurtGIHashAddressXX(proximity))))))));
    a.hash=h==0xffffffffu?0xfffffffeu:max(1u,h);
    uint3 cell=(uint3)clamp(floor(hit/a.cellSize)-(float3)tile*ratio,0.0,(float)(ratio-1u));
    float3 ad=abs(direction);float dominant=max(ad.x,max(ad.y,ad.z));
    uint axis=ad.x==dominant?0u:(ad.y==dominant?1u:2u);
    uint layer=axis==0u?cell.x:(axis==1u?cell.y:cell.z);
    // A projected 2D tile is one cell thick, not a whole 8-cell-deep slab.
    // Quantized direction alone does not identify its projection axis either.
    // Keep both in the shared producer/resolve/fallback address: otherwise a
    // moved emissive surface can leave its cached radiance on the exposed floor.
    h=BurtGIHashAddressXX(a.hash+BurtGIHashAddressXX(axis*ratio+layer));
    a.hash=h==0xffffffffu?0xfffffffeu:max(1u,h);
    a.cell=axis==0u?cell.yz:(axis==1u?cell.xz:cell.xy);
    a.cellCenter=((float3)tile*ratio+(float3)cell+0.5)*a.cellSize;
    return a;
}
#endif
