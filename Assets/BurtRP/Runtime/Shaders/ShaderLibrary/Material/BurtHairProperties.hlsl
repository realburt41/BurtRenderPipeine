// BurtRP Hair material property layout. Keep all passes on one UnityPerMaterial CBUFFER for SRP Batcher compatibility.
#ifndef BURT_HAIR_PROPERTIES_INCLUDED
#define BURT_HAIR_PROPERTIES_INCLUDED

CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float4 _BaseMap_ST;
    float4 _MaskMap_ST;
    float _AlphaClip;
    float _Cutoff;
    float _NormalScale;
    float4 _DoubleSidedNormalModeConstants;
    float _Reflectance;
    float _HairScatter;
    float _HairScatterBoost;
    float _HairSpecularScale;
    float _HairRoughnessOffset;
    float _HairTangentFlip;
    float _Smoothness;
    float _OcclusionStrength;
    float4 _EmissionColor;
    float4 _EmissionMap_ST;
CBUFFER_END

#endif // BURT_HAIR_PROPERTIES_INCLUDED
