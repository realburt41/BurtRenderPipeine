// Shared deferred depth-normal prepass. It reuses GBuffer material evaluation so
// alpha clip, vertex animation, foliage tint inputs, and normal selection stay in sync.
#ifndef BURT_DEPTH_NORMALS_PASS_INCLUDED
#define BURT_DEPTH_NORMALS_PASS_INCLUDED

#define BURT_MATERIAL_DEPTH_NORMALS_PASS 1
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtGBufferPass.hlsl"

struct DepthNormalsFragmentOutput
{
    float4 GBuffer0 : SV_Target0;
    float4 GISurfaceNormal : SV_Target1;
};

DepthNormalsFragmentOutput FragDepthNormals(GBufferVaryings input, fixed facing : VFACE)
{
    BurtGBufferData gbufferData = BurtCreateMaterialPassGBufferDataFromInput(input, facing);
    BurtEncodedGBuffer encodedGBuffer = BurtEncodeGBuffer(gbufferData);

    DepthNormalsFragmentOutput output;
    output.GBuffer0 = encodedGBuffer.GBuffer0;
    // MRT1 is bound only when screen GI is active. Preserve strand in MRT0.
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    output.GISurfaceNormal = float4(BurtEncodeNormalWS888ForGBuffer(gbufferData.ClearCoatNormalWS), encodedGBuffer.GBuffer0.a);
#else
    output.GISurfaceNormal = encodedGBuffer.GBuffer0;
#endif
    return output;
}

#endif // BURT_DEPTH_NORMALS_PASS_INCLUDED
