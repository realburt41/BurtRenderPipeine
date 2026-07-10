#ifndef BURT_GBUFFER_ENCODE_SUBSURFACE_INCLUDED
#define BURT_GBUFFER_ENCODE_SUBSURFACE_INCLUDED

float4 BurtEncodeSubsurfaceGBuffer3(BurtGBufferData Data)
{
    float2 EncodedGeometryNormalWS = BurtEncodeNormalWSForGBuffer(Data.SubsurfaceGeometryNormalWS);
    float SubsurfaceControl = BurtIsSubsurface3SPreIntegratedMode(Data.SubsurfaceScatteringMode)
        ? saturate(Data.Subsurface3SCurvature)
        : BurtEncodeSubsurfacePowerAmbientForGBuffer(Data.SubsurfacePower, Data.SubsurfaceAmbient);
    return float4(EncodedGeometryNormalWS, 1.0f, SubsurfaceControl);
}

float4 BurtEncodeGBuffer3_Subsurface(BurtGBufferData Data)
{
    return BurtEncodeSubsurfaceGBuffer3(Data);
}

float4 BurtEncodeGBuffer4_Subsurface(BurtGBufferData Data)
{
    float2 EncodedTangentWS = BurtEncodeNormalWSForGBuffer(Data.TangentWS);
    return float4(
        EncodedTangentWS,
        BurtEncodeSubsurfaceDistortionModeForGBuffer(Data.SubsurfaceDistortion, Data.SubsurfaceScatteringMode),
        BurtEncodeSubsurfaceThicknessProfileForGBuffer(Data.SubsurfaceThickness, Data.SubsurfaceProfileIndex));
}

#endif
