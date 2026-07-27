// UnityPerMaterial layout for BurtRP/Fabric only.
#ifndef BURT_FABRIC_PROPERTIES_INCLUDED
#define BURT_FABRIC_PROPERTIES_INCLUDED

#include "UnityCG.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"

#define BURT_FORWARD_ENABLE_REFRACTION 0
#define BURT_MATERIAL_SUPPORTS_TRANSPARENT_FOG 1

CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float4 _BaseMap_ST;
    float4 _MaskMap_ST;
    float _Surface;
    float _AlphaClip;
    float _Cutoff;
    float _NormalScale;
    float4 _DoubleSidedNormalModeConstants;
    float _Reflectance;
    float _Metallic;
    float _Anisotropy;
    float _Roughness;
    float _OcclusionStrength;
    float4 _FuzzMap_ST;
    float4 _FuzzColor;
    float _FuzzAmount;
    float _FuzzRoughness;
    float4 _EmissionColor;
    float4 _EmissionMap_ST;
    float _ResponsiveAA;
CBUFFER_END

#endif // BURT_FABRIC_PROPERTIES_INCLUDED
