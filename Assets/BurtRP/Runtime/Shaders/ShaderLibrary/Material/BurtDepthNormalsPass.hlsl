// Shared deferred depth-normal prepass. It reuses GBuffer material evaluation so
// alpha clip, vertex animation, foliage tint inputs, and normal selection stay in sync.
#ifndef BURT_DEPTH_NORMALS_PASS_INCLUDED
#define BURT_DEPTH_NORMALS_PASS_INCLUDED

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtGBufferPass.hlsl"

struct DepthNormalsFragmentOutput
{
    float4 GBuffer0 : SV_Target0;
};

DepthNormalsFragmentOutput FragDepthNormals(GBufferVaryings input, fixed facing : VFACE)
{
    BurtGBufferData gbufferData = BurtCreateMaterialPassGBufferDataFromInput(input, facing);
    BurtEncodedGBuffer encodedGBuffer = BurtEncodeGBuffer(gbufferData);

    DepthNormalsFragmentOutput output;
    output.GBuffer0 = encodedGBuffer.GBuffer0;
    return output;
}

#endif // BURT_DEPTH_NORMALS_PASS_INCLUDED
