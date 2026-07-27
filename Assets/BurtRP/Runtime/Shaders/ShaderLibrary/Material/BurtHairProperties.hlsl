// BurtRP Hair material property layout. Keep all passes on one UnityPerMaterial CBUFFER for SRP Batcher compatibility.
#ifndef BURT_HAIR_PROPERTIES_INCLUDED
#define BURT_HAIR_PROPERTIES_INCLUDED

#include "UnityCG.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float4 _RootColor;
    float4 _RootGradient;
    float4 _GradientDirection;
    float4 _GradientPosOffset;
    float4 _TangentA;
    float4 _TangentB;
    float4 _HairBrightColor;
    float4 _HairShadowColor;
    float4 _AlbedoOcclusionColor;
    float4 _RoughParameter;
    float4 _SpecularColor;
    float4 _SpecularSecondColor;
    float4 _BaseMap_ST;
    float4 _MaskMap_ST;
    float4 _IDMap_ST;
    float4 _GradientMap_ST;
    float _AlphaClip;
    float _Cutoff;
    float _ShadowCutOff;
    float _OpacityMaskValue;
    float _OpacityMaskOffset;
    float _NormalScale;
    float4 _DoubleSidedNormalModeConstants;
    float _Reflectance;
    float _HairScatter;
    float _HairScatterBoost;
    float _HairSpecularScale;
    float _HairShiftScale;
    float _HairRoughnessOffset;
    float _HairTangentFlip;
    float _HairShadowFillStrength;
    float _RootGradientEnable;
    float _RootGradientReverse;
    float _RootGradientPosEnable;
    float _IDXTilling;
    float _IDIntensity;
    float _HairBrightIntensity;
    float _HairShadowIntensity;
    float _HairShadowPower;
    float _ScatterUseFullRange;
    float _Scatter;
    float _ScatterFull;
    float _Occlusion;
    float _AlbedoOcclusion;
    float _BackLightIntensity;
    float _BackLightMask;
    float _BackLightMaskRange;
    float _HairRotate;
    float _SpecularShift;
    float _SecondarySpecularShift;
    float _EdgeRoughRimPower;
    float _GradientColorEnable;
    float _GradientRowIndex;
    float _GradientSoftLight;
    float _GradientOverlay;
    float _GradientReplace;
    float _Smoothness;
    float _OcclusionStrength;
    float4 _EmissionColor;
    float4 _EmissionMap_ST;
    float _ResponsiveAA;
CBUFFER_END

float4 _GradientMap_TexelSize;

#endif // BURT_HAIR_PROPERTIES_INCLUDED
