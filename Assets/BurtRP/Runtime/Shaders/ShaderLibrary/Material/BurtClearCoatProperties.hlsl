// UnityPerMaterial layout for BurtRP/Clear Coat only.
#ifndef BURT_CLEAR_COAT_PROPERTIES_INCLUDED
#define BURT_CLEAR_COAT_PROPERTIES_INCLUDED

#include "UnityCG.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"

#define BURT_FORWARD_ENABLE_REFRACTION 0

CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float4 _BaseMap_ST;
    float4 _MaskMap_ST;
    float _AlphaClip;
    float _Cutoff;
    float _NormalScale;
    float4 _DoubleSidedNormalModeConstants;
    float _Reflectance;
    float _Metallic;
    float _Anisotropy;
    float _Smoothness;
    float _OcclusionStrength;
    float _ClearCoatMask;
    float _ClearCoatRoughness;
    float _ClearCoatNormalScale;
    float4 _EmissionColor;
    float4 _EmissionMap_ST;
CBUFFER_END

#endif // BURT_CLEAR_COAT_PROPERTIES_INCLUDED
