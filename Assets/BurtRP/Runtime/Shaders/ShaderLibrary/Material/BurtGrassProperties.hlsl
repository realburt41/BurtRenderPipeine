// UnityPerMaterial layout for BurtRP/Grass only.
#ifndef BURT_GRASS_PROPERTIES_INCLUDED
#define BURT_GRASS_PROPERTIES_INCLUDED

#include "UnityCG.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"

#define BURT_FORWARD_ENABLE_REFRACTION 0

CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float4 _BaseColorTip;
    float4 _BaseMap_ST;
    float4 _MaskMap_ST;
    float _AlphaClip;
    float _Cutoff;
    float _AlphaIncrease;
    float _NormalScale;
    float4 _DoubleSidedNormalModeConstants;
    float _Reflectance;
    float _Roughness;
    float _OcclusionStrength;
    float _SSSIntensity;
    float _FresnelIntensity;
    float _FresnelExp;
    float _Specular;
    float _TipMaskPow;
    float _HeightAO;
    float _HeightAOFallOff;
    float _TLNormalWeight;
    float _SSShadowIntensity;
    float _SSShadowDistance;
    float _TiltingStrength;
    float _GroundFadeIntensity;
    float4 _NoiseMap_ST;
    float _VariationIntensity01;
    float _VariationIntensity02;
    float _Variation01Height;
    float _Variation02Height;
    float4 _Variation01;
    float4 _Variation02;
    float _WindHeightMask;
    float _WindStrength;
    float _WindNormalStrength;
    float4 _EmissionColor;
    float4 _EmissionMap_ST;
    float _ResponsiveAA;
CBUFFER_END

#endif // BURT_GRASS_PROPERTIES_INCLUDED
