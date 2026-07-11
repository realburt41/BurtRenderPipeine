// UnityPerMaterial layout for BurtRP/Trunk only.
#ifndef BURT_TRUNK_PROPERTIES_INCLUDED
#define BURT_TRUNK_PROPERTIES_INCLUDED

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
    float4 _VertexAORemap;
    float _Specular;
    float _TreeHeight;
    float _MaxBendAngle;
    float _SwayIntensity;
    float _BendMaskPow;
    float _ToTrunkMaskPow;
    float _TerrainBlend_TerrainTog;
    float _TerrainBlend_BlendHeight;
    float _TerrainBlendEnable;
    float _TerrainBlendHeight;
    float4 _TerrainBlendDetailHeight;
    float _AlphaCutoffEnable;
    float _AlphaCutoff;
    float4 _EmissionColor;
    float4 _EmissionMap_ST;
CBUFFER_END

#endif // BURT_TRUNK_PROPERTIES_INCLUDED
