#ifndef BURT_EASY_FOG_PROPERTIES_INCLUDED
#define BURT_EASY_FOG_PROPERTIES_INCLUDED

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"

Texture2D _NormalMap;
Texture2D _OpacityMap;
Texture2D _Flowmap;

CBUFFER_START(UnityPerMaterial)
    float4 _BaseColorTink;
    float4 _EmissiveColor;
    float4 _OpacityMap_ST;
    float4 _Flowmap_ST;
    float _NormalScale;
    float _FogIntensity;
    float _EmissiveIntensity;
    float _CameraFadingDistance;
    float _DepthFadeDistance;
    float _DepthFadePower;
    float _EnableBillboard;
    float _FlowmapSpeed;
    float _FlowmapIntensity;
    float _Surface;
    float _SrcBlend;
    float _DstBlend;
    float _ZWrite;
    float _ZTest;
    float _Cull;
    float _ResponsiveAA;
CBUFFER_END

#endif // BURT_EASY_FOG_PROPERTIES_INCLUDED
