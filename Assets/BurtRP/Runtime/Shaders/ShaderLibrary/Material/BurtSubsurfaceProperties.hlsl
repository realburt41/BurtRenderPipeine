// UnityPerMaterial layout for BurtRP/Subsurface only.
#ifndef BURT_SUBSURFACE_PROPERTIES_INCLUDED
#define BURT_SUBSURFACE_PROPERTIES_INCLUDED

#include "UnityCG.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"

#define BURT_FORWARD_ENABLE_REFRACTION 0
#define BURT_MATERIAL_SUPPORTS_TRANSPARENT_FOG 1

CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float4 _BaseMap_ST;
    float4 _MaskMap_ST;
    float _Surface;
    float _BlendMode;
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
    float _BurtSkinnedDecalProjectionDebug;
    float _BurtSkinnedDecalEntryDebug;
    float _BurtSkinnedDecalUseMeshPosition;
    float _SkinnedDecalPluginModel_DecalCount;
    float4 _SkinnedDecalPluginModel_DecalArrayIndexSize1;
    float4 _SkinnedDecalPluginModel_DecalTint1;
    float4 _SkinnedDecalPluginModel_DecalPosition1;
    float4 _SkinnedDecalPluginModel_DecalBasisX1;
    float4 _SkinnedDecalPluginModel_DecalBasisY1;
    float4 _SkinnedDecalPluginModel_DecalArraySizeIndex2;
    float4 _SkinnedDecalPluginModel_DecalTint2;
    float4 _SkinnedDecalPluginModel_DecalPosition2;
    float4 _SkinnedDecalPluginModel_DecalBasisX2;
    float4 _SkinnedDecalPluginModel_DecalBasisY2;
    float4 _SkinnedDecalPluginModel_DecalArraySizeIndex3;
    float4 _SkinnedDecalPluginModel_DecalTint3;
    float4 _SkinnedDecalPluginModel_DecalPosition3;
    float4 _SkinnedDecalPluginModel_DecalBasisX3;
    float4 _SkinnedDecalPluginModel_DecalBasisY3;
    float4 _SkinnedDecalPluginModel_DecalArraySizeIndex4;
    float4 _SkinnedDecalPluginModel_DecalTint4;
    float4 _SkinnedDecalPluginModel_DecalPosition4;
    float4 _SkinnedDecalPluginModel_DecalBasisX4;
    float4 _SkinnedDecalPluginModel_DecalBasisY4;
    float4 _SkinnedDecalPluginModel_DecalArraySizeIndex5;
    float4 _SkinnedDecalPluginModel_DecalTint5;
    float4 _SkinnedDecalPluginModel_DecalPosition5;
    float4 _SkinnedDecalPluginModel_DecalBasisX5;
    float4 _SkinnedDecalPluginModel_DecalBasisY5;
    float _ResponsiveAA;
CBUFFER_END

#endif // BURT_SUBSURFACE_PROPERTIES_INCLUDED
