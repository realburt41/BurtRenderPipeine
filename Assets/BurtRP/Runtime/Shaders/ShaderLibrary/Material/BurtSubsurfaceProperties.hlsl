// UnityPerMaterial layout for BurtRP/Subsurface only.
#ifndef BURT_SUBSURFACE_PROPERTIES_INCLUDED
#define BURT_SUBSURFACE_PROPERTIES_INCLUDED

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
    float _Smoothness;
    float _OcclusionStrength;
    float _SubsurfaceThickness;
    float _SubsurfacePower;
    float _SubsurfaceDistortion;
    float _SubsurfaceAmbient;
    float _SubsurfaceScatteringMode;
    float _SubsurfaceProfileIndex;
    float _Subsurface3SCurvatureScale;
    float _Subsurface3SCurvatureBias;
    float4 _EmissionColor;
    float4 _EmissionMap_ST;
CBUFFER_END

#endif // BURT_SUBSURFACE_PROPERTIES_INCLUDED
