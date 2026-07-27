// UnityPerMaterial layout for BurtRP/Clear Crystal.
#ifndef BURT_CLEAR_CRYSTAL_PROPERTIES_INCLUDED
#define BURT_CLEAR_CRYSTAL_PROPERTIES_INCLUDED

#include "UnityCG.cginc"

Texture2D _DetailNormalMap;
Texture2D _EmissiveMap;
Texture2D _ParallaxMap;
Texture2D _TransmissionColorMap;

CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float4 _BaseMap_ST;
    float4 _BaseColorFlowSpeed;
    float4 _DetailNormalTiling;
    float4 _ParallaxStrength;
    float4 _ParallaxColor1;
    float4 _ParallaxColor2;
    float4 _ParallaxColor3;
    float4 _ParallaxTilingOffset1;
    float4 _ParallaxTilingOffset2;
    float4 _ParallaxTilingOffset3;
    float4 _ParallaxFlowSpeed1;
    float4 _ParallaxFlowSpeed2;
    float4 _ParallaxFlowSpeed3;
    float4 _EmissiveColor;
    float4 _EmissiveTillingPanner;
    float4 _TransmissionColor;
    float4 _DoubleSidedNormalModeConstants;
    float _NormalScale;
    float _DetailNormalScale;
    float _DetailNormalRotate;
    float _Metallic;
    float _Occlusion;
    float _Roughness;
    float _IOR;
    float _RoughnessRefractionWeight;
    float _Reflectance;
    float _ShadowIntensity;
    float _EmissiveUseViewSpaceUV;
    float _ViewSpaceUVNormalIntensity;
    float _Weight;
    float _MFPScale;
    float _Thickness;
    float _PhaseAniso;
    float _ParallaxBrightness1;
    float _ParallaxBrightness2;
    float _ParallaxBrightness3;
    float _ParallaxBaseColorBlend1;
    float _ParallaxBaseColorBlend2;
    float _ParallaxBaseColorBlend3;
    float _UseObjectSpaceParallax1;
    float _UseObjectSpaceParallax2;
    float _UseObjectSpaceParallax3;
    float _Refraction;
    float _TransparentSortPriority;
    float _AlphaClip;
    float _Cutoff;
    float _ResponsiveAA;
CBUFFER_END

#endif // BURT_CLEAR_CRYSTAL_PROPERTIES_INCLUDED
