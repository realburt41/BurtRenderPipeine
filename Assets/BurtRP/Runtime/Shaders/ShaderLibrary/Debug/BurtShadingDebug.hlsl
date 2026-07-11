// Shading-debug facade kept for existing includes.
// Normal/runtime variants should include this only when BURT_ENABLE_SHADING_DEBUG is defined.
#ifndef BURT_SHADING_DEBUG_FACADE_INCLUDED
#define BURT_SHADING_DEBUG_FACADE_INCLUDED

#if defined(BURT_SHADING_DEBUG) && !defined(BURT_ENABLE_SHADING_DEBUG)
#define BURT_ENABLE_SHADING_DEBUG 1
#endif

#if defined(BURT_ENABLE_SHADING_DEBUG) && BURT_ENABLE_SHADING_DEBUG
#ifndef BURT_SHADING_DEBUG_INCLUDE_SHADOW
#define BURT_SHADING_DEBUG_INCLUDE_SHADOW 1
#endif
#ifndef BURT_SHADING_DEBUG_INCLUDE_ADDITIONAL_LIGHTS
#define BURT_SHADING_DEBUG_INCLUDE_ADDITIONAL_LIGHTS 1
#endif

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInput.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtShadingModelMacros.hlsl"
#if BURT_SHADING_DEBUG_INCLUDE_SHADOW
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtShadowsDebug.hlsl"
#endif
#if BURT_SHADING_DEBUG_INCLUDE_ADDITIONAL_LIGHTS
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLightingAdditionalLightsDebug.hlsl"
#endif
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtShadingDebugModes.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtShadingDebugData.hlsl"
#if BURT_SHADING_DEBUG_INCLUDE_SHADOW
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtShadingDebugShadow.hlsl"
#endif
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtShadingDebugMaterial.hlsl"
#endif

#endif // BURT_SHADING_DEBUG_FACADE_INCLUDED
