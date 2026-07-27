#ifndef BURT_HEXA_LIGHTING_PROPERTIES_INCLUDED
#define BURT_HEXA_LIGHTING_PROPERTIES_INCLUDED

Texture2D _PositiveAxesLightmap;
Texture2D _NegativeAxesLightmap;
Texture2D _MotionVectorMap;

CBUFFER_START(UnityPerMaterial)
float _Rows;
float _Columns;
float _PlaySpeed;
float _MotionVectorScale;
float _ResponsiveAA;
float4 _BasicColor;
float _Density;
float _OverallAlpha;
CBUFFER_END

#endif // BURT_HEXA_LIGHTING_PROPERTIES_INCLUDED
