// BurtRP port of WorldX MM_CH_AvatarEye material properties.
#ifndef BURT_EYE_PROPERTIES_INCLUDED
#define BURT_EYE_PROPERTIES_INCLUDED

#include "UnityCG.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float4 _BaseMap_ST;
    float4 _MaskMap_ST;
    float _AlphaClip;
    float _Cutoff;
    float _ResponsiveAA;
    float _NormalScale;
    float _NormalMapScale;
    float4 _DoubleSidedNormalModeConstants;
    float _ScalebyCenter;
    float _PupilScale;
    float _LimbusScale;
    float _LimbusPow;
    float _InverseUV;
    float4 _IrisColor;
    float _IrisColorRotate;
    float _IrisColorRotateSpeed;
    float _IOR;
    float _IrisRadius;
    float4 _IrisMaskBlurIntensity;
    float _IrisConcavityScale;
    float _IrisConcavityPow;
    float _CorneaSpecular;
    float _CorneaRoughness;
    float4 _ScleraColor;
    float _ScleraSpecular;
    float _ScleraRoughness;
    float4 _EyeEmissiveColor;
    float _IrisDepthScale;
    float4 _MatcapColor;
    float4 _MatcapSizeOffset;
    float4 _EmissionColor;
    float4 _EmissionMap_ST;
CBUFFER_END

#endif // BURT_EYE_PROPERTIES_INCLUDED
