#ifndef BURT_GBUFFER_DECODE_SUBSURFACE_INCLUDED
#define BURT_GBUFFER_DECODE_SUBSURFACE_INCLUDED

BurtGBufferData BurtDecodeGBufferCustom_Subsurface(BurtEncodedGBuffer Encoded, BurtGBufferData Data)
{
    BurtDecodeSubsurfacePowerAmbientFromGBuffer(Encoded.GBuffer3.a, Data.SubsurfacePower, Data.SubsurfaceAmbient);
    BurtDecodeSubsurfaceDistortionModeFromGBuffer(Encoded.GBuffer5.b, Data.SubsurfaceDistortion, Data.SubsurfaceScatteringMode);
    BurtDecodeSubsurfaceThicknessProfileFromGBuffer(Encoded.GBuffer5.a, Data.SubsurfaceThickness, Data.SubsurfaceProfileIndex);
    Data.SubsurfaceGeometryNormalWS = BurtDecodeNormalWSFromGBuffer(Encoded.GBuffer3.rg);
    Data.Subsurface3SCurvature = BurtIsSubsurface3SPreIntegratedMode(Data.SubsurfaceScatteringMode)
        ? saturate(Encoded.GBuffer3.a)
        : saturate(1.0f - Data.SubsurfaceThickness);
    return Data;
}

#endif
