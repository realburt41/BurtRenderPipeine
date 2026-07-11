// Ambient, sky/SH, probe-volume GI, and image-based indirect lighting facade.
#ifndef BURT_LIGHTING_INDIRECT_INCLUDED
#define BURT_LIGHTING_INDIRECT_INCLUDED

#ifndef BURT_SUBSURFACE_3S_SH_IRRADIANCE_WEIGHT
#define BURT_SUBSURFACE_3S_SH_IRRADIANCE_WEIGHT (0.0f)
#endif

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLightingSkySH.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLightingIBL.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLightingProbeVolume.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLightingIndirectPBR.hlsl"

#endif
