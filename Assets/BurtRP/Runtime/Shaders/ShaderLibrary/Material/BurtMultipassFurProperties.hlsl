// BurtRP port of WorldX MM_CH_MultiPassFur material properties.
#ifndef BURT_MULTIPASS_FUR_PROPERTIES_INCLUDED
#define BURT_MULTIPASS_FUR_PROPERTIES_INCLUDED

#include "UnityCG.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float4 _DarkColor;
    float4 _BaseMap_ST;
    float4 _BaseMapPanner;
    float4 _MaskMap_ST;
    float _Occlusion;
    float _Roughness;
    float _Reflectance;
    float _Anisotropy;
    float4 _EmissiveColor;
    float4 _EmissiveMap_ST;
    float4 _EmissiveTillingPanner;
    float _EmissiveUseViewSpaceUV;
    float _ViewSpaceUVNormalIntensity;
    float _FurRimIntensity;
    float _FurRimPower;
    float _FurAttenuation;
    float _FurTickness;
    float _FurTicknessCurve;
    float _FurExpand;
    float _FurSpacing;
    float _FurSpacingMax;
    float4 _FlowTex_ST;
    float _FlowTexUV2;
    float _FlowTilling;
    float4 _FlowPanner;
    float4 _FlowDirectionMap_ST;
    float _UseDirectionMap;
    float _FlowDirectionUV2;
    float _FlowDirectionIntensity;
    float _FurGravityDirection;
    float _FurGravityIntensity;
    float _AlphaClip;
    float _Cutoff;
    float4 _DoubleSidedNormalModeConstants;
CBUFFER_END

#endif // BURT_MULTIPASS_FUR_PROPERTIES_INCLUDED
