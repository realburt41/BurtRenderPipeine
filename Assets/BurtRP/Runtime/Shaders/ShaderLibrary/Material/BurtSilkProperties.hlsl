// UnityPerMaterial layout for BurtRP/Silk only.
#ifndef BURT_SILK_PROPERTIES_INCLUDED
#define BURT_SILK_PROPERTIES_INCLUDED

#include "UnityCG.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"

#if !defined(BURT_MATERIAL_SHADING_MODEL_SILK)
#define BURT_MATERIAL_SHADING_MODEL_SILK 1
#endif

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
    float _Roughness;
    float _OcclusionStrength;
    float4 _FacingColor;
    float4 _EmissionColor;
    float4 _EmissionMap_ST;
CBUFFER_END

#endif // BURT_SILK_PROPERTIES_INCLUDED
