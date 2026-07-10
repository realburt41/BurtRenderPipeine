// Shared GBuffer packing and unpacking helpers.
#ifndef BURT_GBUFFER_PACKING_INCLUDED
#define BURT_GBUFFER_PACKING_INCLUDED

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBufferPackingCommon.hlsl"
#if (BURT_STATIC_SHADING_MODEL == 0) || BURT_ENABLE_HAIR_SHADING
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBufferPackingHair.hlsl"
#endif

#if (BURT_STATIC_SHADING_MODEL == 0) || BURT_ENABLE_SUBSURFACE_SHADING
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBufferPackingSubsurface.hlsl"
#endif
#if (BURT_STATIC_SHADING_MODEL == 0) || BURT_ENABLE_FABRIC_SHADING
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBufferPackingFabric.hlsl"
#endif
#if (BURT_STATIC_SHADING_MODEL == 0) || BURT_ENABLE_FOLIAGE_SHADING
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBufferPackingFoliage.hlsl"
#endif

#endif
