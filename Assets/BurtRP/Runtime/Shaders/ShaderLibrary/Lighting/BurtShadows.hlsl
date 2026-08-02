// Shadow facade. Keep this stable so existing material and pass includes do not change.
#ifndef BURT_SHADOWS_INCLUDED
#define BURT_SHADOWS_INCLUDED

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtShadowsCommon.hlsl"

#if !defined(BURT_SHADOWS_ADDITIONAL_ONLY)
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtMainLightShadows.hlsl"
#endif

#if !defined(BURT_SHADOWS_MAIN_ONLY)
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtAdditionalLightShadows.hlsl"
#endif

#if defined(BURT_INCLUDE_SHADOW_DEBUG)
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtShadowsDebugSupport.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtShadowsDebug.hlsl"
#endif

#endif // BURT_SHADOWS_INCLUDED
