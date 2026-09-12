#ifndef BURT_GI_VOXELIZE_FINE_INCLUDED
#define BURT_GI_VOXELIZE_FINE_INCLUDED

#include "UnityCG.cginc"
#include "../../Resources/BurtGISceneVoxelFineData.hlsl"

sampler2D _BaseMap;
sampler2D _MaskMap;
sampler2D _EmissionMap;
float4 _BaseMap_ST, _EmissionMap_ST, _BaseColor, _EmissionColor;
float _Metallic, _AlphaClip, _Cutoff, _BurtGIVoxelizeEmissionMode;
float4 _BurtGISceneVoxelCenterExtent, _BurtGISceneVoxelLightingParams;
uint _BurtGISceneVoxelFineResolution; // Coarse nodes per axis; raster is 4N.
uint _BurtGISceneVoxelFineCapacity;
uint _BurtGIVoxelizePrimitiveBase;
int _BurtGIVoxelizeResolvePhase;

#if defined(BURT_GI_FINE_OCCUPANCY_PASS)
RWTexture3D<uint> _BurtGISceneVoxelFineLow : register(u1);
RWTexture3D<uint> _BurtGISceneVoxelFineHigh : register(u2);
#else
Texture3D<uint> _BurtGISceneVoxelFineLow;
Texture3D<uint> _BurtGISceneVoxelFineHigh;
Texture3D<uint> _BurtGISceneVoxelFineOffsets;
RWStructuredBuffer<uint> _BurtGISceneVoxelFineOwners : register(u1);
RWStructuredBuffer<BurtGISceneVoxelFineMaterialData> _BurtGISceneVoxelFineMaterials : register(u2);
#endif

struct BurtGIFineAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
};
struct BurtGIFineVertex
{
    float3 positionWS : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float2 uv : TEXCOORD2;
};
struct BurtGIFineFragment
{
    float4 positionCS : SV_POSITION;
    nointerpolation uint ownerKey : TEXCOORD3;
    nointerpolation float4 rasterBounds : TEXCOORD4;
    nointerpolation uint projectionAxis : TEXCOORD5;
    nointerpolation float3 triangleA : TEXCOORD6;
    nointerpolation float3 triangleB : TEXCOORD7;
    nointerpolation float3 triangleC : TEXCOORD8;
    nointerpolation float2 triangleUV0 : TEXCOORD9;
    nointerpolation float2 triangleUV1 : TEXCOORD10;
    nointerpolation float2 triangleUV2 : TEXCOORD11;
    nointerpolation float3 triangleNormal0 : TEXCOORD12;
    nointerpolation float3 triangleNormal1 : TEXCOORD13;
    nointerpolation float3 triangleNormal2 : TEXCOORD14;
};

BurtGIFineVertex BurtGIVoxelizeFineVertex(BurtGIFineAttributes input)
{
    BurtGIFineVertex output;
    output.positionWS = mul(unity_ObjectToWorld, input.positionOS).xyz;
    output.normalWS = UnityObjectToWorldNormal(input.normalOS);
    output.uv = input.uv;
    return output;
}

float2 BurtGIFineSwizzle(float3 p,uint axis)
{
    return axis==0u?p.zy:(axis==1u?p.xz:p.xy);
}
float2 BurtGIFineProject(float3 p, uint axis)
{
    p=(p-_BurtGISceneVoxelCenterExtent.xyz)/max(_BurtGISceneVoxelCenterExtent.w,0.001);
    return axis==0u?p.zy:(axis==1u?p.xz:p.xy);
}
void BurtGIVoxelizeFineEmit(BurtGIFineVertex input[3], uint axis, uint primitiveID,
    inout TriangleStream<BurtGIFineFragment> stream)
{
    if(_BurtGISceneVoxelFineResolution==0u)return;
    BurtGIFineVertex vertices[3];
    float2 p[3];
    [unroll] for (uint i=0u;i<3u;i++)
    {
        vertices[i]=input[i];
        p[i]=BurtGIFineProject(vertices[i].positionWS,axis);
    }
    float determinant=(p[1].x-p[0].x)*(p[2].y-p[0].y)-(p[1].y-p[0].y)*(p[2].x-p[0].x);
    if (abs(determinant)<1e-10) return;
    if (determinant<0)
    {
        float2 temp=p[1];p[1]=p[2];p[2]=temp;
        BurtGIFineVertex vt=vertices[1];vertices[1]=vertices[2];vertices[2]=vt;
    }
    float2 hp=rcp((float)(_BurtGISceneVoxelFineResolution * 4u));
    float4 bounds=float4(min(p[0],min(p[1],p[2]))-hp,max(p[0],max(p[1],p[2]))+hp);
    float3 edges[3];
    edges[0]=cross(float3(p[0]-p[2],0),float3(p[2],1));
    edges[1]=cross(float3(p[1]-p[0],0),float3(p[0],1));
    edges[2]=cross(float3(p[2]-p[1],0),float3(p[1],1));
    [unroll] for(uint e=0u;e<3u;e++)edges[e].z-=dot(hp,abs(edges[e].xy));
    float3 expanded[3];
    expanded[0]=cross(edges[0],edges[1]);
    expanded[1]=cross(edges[1],edges[2]);
    expanded[2]=cross(edges[2],edges[0]);
    [unroll] for(uint vertexIndex=0u;vertexIndex<3u;vertexIndex++)
    {
        float2 xy=expanded[vertexIndex].xy/expanded[vertexIndex].z;
        BurtGIFineFragment output;
        output.positionCS=float4(xy,0.5,1);
        output.ownerKey=(_BurtGIVoxelizePrimitiveBase+primitiveID)*3u+axis;
        output.rasterBounds=bounds;output.projectionAxis=axis;
        output.triangleA=vertices[0].positionWS;output.triangleB=vertices[1].positionWS;output.triangleC=vertices[2].positionWS;
        output.triangleUV0=vertices[0].uv;output.triangleUV1=vertices[1].uv;output.triangleUV2=vertices[2].uv;
        output.triangleNormal0=vertices[0].normalWS;output.triangleNormal1=vertices[1].normalWS;output.triangleNormal2=vertices[2].normalWS;
        stream.Append(output);
    }
    stream.RestartStrip();
}
[maxvertexcount(3)]
void BurtGIVoxelizeFineGeometry(triangle BurtGIFineVertex input[3],
    uint primitiveID : SV_PrimitiveID, inout TriangleStream<BurtGIFineFragment> stream)
{
    float3 n=abs(cross(input[1].positionWS-input[0].positionWS,input[2].positionWS-input[0].positionWS));
    uint axis=n.x>=n.y&&n.x>=n.z?0u:(n.y>=n.z?1u:2u);
    BurtGIVoxelizeFineEmit(input, axis, primitiveID, stream);
}

bool BurtGIFineAxisOverlap(float3 a,float3 b,float3 c,float3 axis,float halfSize)
{
    float p0=dot(a,axis),p1=dot(b,axis),p2=dot(c,axis);
    float radius=halfSize*dot(abs(axis),1.0.xxx);
    return min(p0,min(p1,p2))<=radius && max(p0,max(p1,p2))>=-radius;
}
bool BurtGIFineTriangleCell(float3 a,float3 b,float3 c,float3 center,float halfSize)
{
    a-=center;b-=center;c-=center;
    // Match floor() ownership for a triangle exactly on an axis-aligned face.
    if(any(min(a,min(b,c))>=halfSize)||any(max(a,max(b,c))< -halfSize))return false;
    float3 edges[3];edges[0]=b-a;edges[1]=c-b;edges[2]=a-c;
    if(!BurtGIFineAxisOverlap(a,b,c,cross(edges[0],edges[1]),halfSize))return false;
    [unroll] for(uint e=0u;e<3u;e++)
    {
        float3 v=edges[e];
        if(!BurtGIFineAxisOverlap(a,b,c,float3(0,-v.z,v.y),halfSize)||
           !BurtGIFineAxisOverlap(a,b,c,float3(v.z,0,-v.x),halfSize)||
           !BurtGIFineAxisOverlap(a,b,c,float3(-v.y,v.x,0),halfSize))return false;
    }
    return true;
}

float4 BurtGIVoxelizeFineFragment(BurtGIFineFragment input) : SV_Target
{
    // Derivatives and alpha must agree in mask, owner election and resolve.
    // A complete sequence uses one stable draw list and a 4N single-sample RT.
    if(_BurtGISceneVoxelFineResolution==0u)discard;
    float rasterResolution=(float)(_BurtGISceneVoxelFineResolution*4u);
    float volumeExtent=max(_BurtGISceneVoxelCenterExtent.w,0.001);
    float2 pixelCenter=floor(input.positionCS.xy)+.5;
#if UNITY_UV_STARTS_AT_TOP
    pixelCenter.y=rasterResolution-pixelCenter.y;
#endif
    float2 projected=pixelCenter*(2.0/rasterResolution)-1;
    float2 worldPixel=BurtGIFineSwizzle(_BurtGISceneVoxelCenterExtent.xyz,input.projectionAxis)-volumeExtent+
        pixelCenter*(volumeExtent*2/rasterResolution);
    float2 a=BurtGIFineSwizzle(input.triangleA,input.projectionAxis);
    float2 ab=BurtGIFineSwizzle(input.triangleB-input.triangleA,input.projectionAxis);
    float2 ac=BurtGIFineSwizzle(input.triangleC-input.triangleA,input.projectionAxis);
    float2 ap=worldPixel-a;
    float invDet=rcp(ab.x*ac.y-ab.y*ac.x);
    float b=(ap.x*ac.y-ap.y*ac.x)*invDet;
    float c=(ab.x*ap.y-ab.y*ap.x)*invDet;
    float3 rawBary=float3(1-b-c,b,c);
    float3 planePositionWS=input.triangleA+(input.triangleB-input.triangleA)*b+(input.triangleC-input.triangleA)*c;
    float3 bary=max(rawBary,0);
    bary/=max(dot(bary,1.0.xxx),1e-8);
    // Interior samples keep the original affine UV mapping. Conservative
    // edge samples use a valid point on the original triangle, not a stretched
    // expanded triangle's UV. Gradients retain the original affine mapping.
    float2 sourceUV=input.triangleUV0*bary.x+input.triangleUV1*bary.y+input.triangleUV2*bary.z;
    float2 gradientUV=input.triangleUV0*rawBary.x+input.triangleUV1*rawBary.y+input.triangleUV2*rawBary.z;
    float2 baseUV=sourceUV*_BaseMap_ST.xy+_BaseMap_ST.zw;
    float2 baseGradientUV=gradientUV*_BaseMap_ST.xy+_BaseMap_ST.zw;
    float2 baseDx = ddx(baseGradientUV), baseDy = ddy(baseGradientUV);
    float2 emissionUV=sourceUV*_EmissionMap_ST.xy+_EmissionMap_ST.zw;
    float2 emissionGradientUV=gradientUV*_EmissionMap_ST.xy+_EmissionMap_ST.zw;
    float2 emissionDx = ddx(emissionGradientUV), emissionDy = ddy(emissionGradientUV);
    float4 baseSample = tex2Dgrad(_BaseMap, baseUV, baseDx, baseDy) * _BaseColor;
    if (_AlphaClip > 0.5) clip(baseSample.a - _Cutoff);
    if(any(projected<input.rasterBounds.xy)||any(projected>input.rasterBounds.zw))discard;

    if(_BurtGISceneVoxelFineResolution==0u)discard;
    uint fineResolution=_BurtGISceneVoxelFineResolution*4u;
    float extent=max(_BurtGISceneVoxelCenterExtent.w,0.001);
    float3 uvw=(planePositionWS-_BurtGISceneVoxelCenterExtent.xyz)/(extent*2)+.5;
    int3 baseCoord=(int3)floor(uvw*fineResolution);
    float cellSize=extent*2/fineResolution;
    // Dominant-axis projection bounds the plane's depth variation over one
    // raster pixel to at most one voxel. Cover the three possible depth cells;
    // exact triangle/box overlap rejects the conservative projected excess.
    [unroll] for(int depthOffset=-1;depthOffset<=1;depthOffset++)
    {
    int3 depthAxis=input.projectionAxis==0u?int3(1,0,0):(input.projectionAxis==1u?int3(0,1,0):int3(0,0,1));
    int3 signedCoord=baseCoord+depthAxis*depthOffset;
    if(any(signedCoord<0)||any(signedCoord>=(int)fineResolution))continue;
    uint3 fineCoord=(uint3)signedCoord;
    float3 cellCenter=_BurtGISceneVoxelCenterExtent.xyz-extent+((float3)fineCoord+.5)*cellSize;
    if(!BurtGIFineTriangleCell(input.triangleA,input.triangleB,input.triangleC,cellCenter,cellSize*.5))continue;
    uint3 coarseCoord=fineCoord>>2u;
    uint bit=BurtGIFineCellBit(fineCoord&3u);

#if defined(BURT_GI_FINE_OCCUPANCY_PASS)
    // Geometry is independent of allocation, material validity and brightness.
    if (bit < 32u) InterlockedOr(_BurtGISceneVoxelFineLow[coarseCoord], 1u << bit);
    else InterlockedOr(_BurtGISceneVoxelFineHigh[coarseCoord], 1u << (bit - 32u));
#else
    uint2 mask = uint2(_BurtGISceneVoxelFineLow[coarseCoord], _BurtGISceneVoxelFineHigh[coarseCoord]);
    if ((((bit < 32u ? mask.x : mask.y) >> (bit & 31u)) & 1u) == 0u) continue;
    uint offset = _BurtGISceneVoxelFineOffsets[coarseCoord];
    uint rank = BurtGIFineMaskRank(mask, bit);
    if (offset == BURT_GI_FINE_INVALID_OFFSET || offset >= _BurtGISceneVoxelFineCapacity ||
        rank >= _BurtGISceneVoxelFineCapacity - offset) continue;
    uint index = offset + rank;
    if (_BurtGIVoxelizeResolvePhase == 0)
    {
        InterlockedMin(_BurtGISceneVoxelFineOwners[index], input.ownerKey);
        continue;
    }
    if (_BurtGISceneVoxelFineOwners[index] != input.ownerKey) continue;
    float metallic = saturate(_Metallic * tex2Dgrad(_MaskMap, baseUV, baseDx, baseDy).r);
    float3 albedo = max(baseSample.rgb * (1.0 - metallic), 0.0);
    float3 emission = max(tex2Dgrad(_EmissionMap, emissionUV, emissionDx, emissionDy).rgb * _EmissionColor.rgb, 0.0);
    emission = max(emission, albedo * max(_BurtGIVoxelizeEmissionMode, 0.0));
    float3 boostedAlbedo = min(saturate(pow(albedo, max(_BurtGISceneVoxelLightingParams.x, 0.001))), 0.99);
    float3 normalWS=normalize(input.triangleNormal0*bary.x+input.triangleNormal1*bary.y+input.triangleNormal2*bary.z);
    // Store material, not the coarse raster's albedo*.12+emission surrogate.
    // Lighting is evaluated in a subsequent compute dispatch, never in-place.
    _BurtGISceneVoxelFineMaterials[index] = BurtGIFinePackMaterial(boostedAlbedo, normalWS * 0.5 + 0.5, emission);
#endif
    }
    return 0.0;
}
#endif
