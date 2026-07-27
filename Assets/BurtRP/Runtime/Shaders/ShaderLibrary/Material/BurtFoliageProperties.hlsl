// UnityPerMaterial layout for BurtRP/Foliage only.
#ifndef BURT_FOLIAGE_PROPERTIES_INCLUDED
#define BURT_FOLIAGE_PROPERTIES_INCLUDED

#include "UnityCG.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"

#define BURT_FORWARD_ENABLE_REFRACTION 0

CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float4 _BaseMap_ST;
    float4 _MaskMap_ST;
    float _AlphaClip;
    float _Cutoff;
    float _AlphaIncrease;
    float _NormalScale;
    float4 _DoubleSidedNormalModeConstants;
    float _Reflectance;
    float _Smoothness;
    float _OcclusionStrength;
    float4 _SubsurfaceColor;
    float _SubsurfaceColorSaturate;
    float _ThicknessScale;
    float _RoughnessScale;
    float _ReflectanceScale;
    float _TransmissionNdotL;
    float _FoliageBackLight;
    float4 _VertexAORemap;
    float _TintValue;
    float _TintScale;
    float4 _LocalTintColor;
    float _TintAOHeightRatio;
    float4 _TintAORemap;
    float _TintHeightContrast;
    float _TreeHeight;
    float _MaxBendAngle;
    float _SwayIntensity;
    float _FlutterTipFrequency;
    float _FlutterTipIntensity;
    float _BendMaskPow;
    float _ToTrunkMaskPow;
    float _CustomEnum;
    float _FoliageTintMode;
    float4 _EmissionColor;
    float4 _EmissionMap_ST;
    float _ResponsiveAA;
CBUFFER_END

#endif // BURT_FOLIAGE_PROPERTIES_INCLUDED
