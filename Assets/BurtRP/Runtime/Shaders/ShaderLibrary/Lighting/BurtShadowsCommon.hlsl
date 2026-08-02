// Shared shadow declarations used by the physically split main/additional modules.
#ifndef BURT_SHADOWS_COMMON_INCLUDED
#define BURT_SHADOWS_COMMON_INCLUDED

#include "UnityCG.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"

#if !defined(BURT_MAIN_LIGHT_DIRECTION_DECLARED)
#define BURT_MAIN_LIGHT_DIRECTION_DECLARED
float4 _BurtMainLightDirection;
#endif

float BurtApplyShadowStrength(float rawShadow, float strength)
{
    return lerp(1.0f, rawShadow, saturate(strength));
}

#endif // BURT_SHADOWS_COMMON_INCLUDED
