// UnityPerMaterial layout for BurtRP/InteriorMapping only.
#ifndef BURT_INTERIOR_MAPPING_PROPERTIES_INCLUDED
#define BURT_INTERIOR_MAPPING_PROPERTIES_INCLUDED

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
    float _AtlasMode;
    float4 _RoomCount;
    float4 _FakeRoom_ST;
    float4 _AtlasMap_ST;
    float4 _FrostMap_ST;
    float _CubemapLightMultiplier;
    float _ColorTemp;
    float _Exposure;
    float _InteriorIntensity;
    float _Depth;
    float _ScaleXAxis;
    float _MarchSteps;
    float _DitherSteps;
    float4 _EmissionColor;
    float4 _EmissionMap_ST;
    float _ResponsiveAA;
CBUFFER_END

#endif // BURT_INTERIOR_MAPPING_PROPERTIES_INCLUDED
