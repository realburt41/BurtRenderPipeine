// Shared material shading-model helpers used by GBuffer, Forward and refraction passes.
#ifndef BURT_MATERIAL_SHADING_MODEL_PASS_COMMON_INCLUDED
#define BURT_MATERIAL_SHADING_MODEL_PASS_COMMON_INCLUDED

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtHairDither.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtTrunkVertexAnimation.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtShadingModelMacros.hlsl"

#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_EYE)
    #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtEyePass.hlsl"
#endif

#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMaterialPassHair.hlsl"
#endif

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMaterialPassCore.hlsl"

#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_SUBSURFACE)
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMaterialPassSubsurface.hlsl"
#endif

#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FABRIC)
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMaterialPassFabric.hlsl"
#endif

#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMaterialPassFoliage.hlsl"
#endif

#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_TRUNK)
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMaterialPassTrunk.hlsl"
#endif

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMaterialPassSurface.hlsl"

#if defined(BURT_GBUFFER_INCLUDED)
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMaterialPassGBufferBridge.hlsl"
#endif

#endif // BURT_MATERIAL_SHADING_MODEL_PASS_COMMON_INCLUDED
