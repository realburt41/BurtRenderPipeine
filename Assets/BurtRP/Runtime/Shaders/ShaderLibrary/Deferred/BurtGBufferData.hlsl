// Shared GBuffer data layouts used by encoding and decoding paths.
#ifndef BURT_GBUFFER_DATA_INCLUDED
#define BURT_GBUFFER_DATA_INCLUDED

struct BurtGBufferData
{
    float3 BaseColor;

float3 NormalWS;

    float3 ClearCoatNormalWS;

    float3 TangentWS;

    float Anisotropy;

    float Metallic;

    float MaterialChannel;

float Smoothness;

    float PerceptualRoughness;

float Reflectance;

    float Occlusion;

    float3 Emission;

    float ShadingModelID;

    float ClearCoatMask;

    float ClearCoatRoughness;

    float SubsurfaceThickness;

    float SubsurfacePower;

    float SubsurfaceDistortion;

    float SubsurfaceAmbient;

    float SubsurfaceScatteringMode;

    float Subsurface3SCurvature;

    float SubsurfaceProfileIndex;

    float3 SubsurfaceGeometryNormalWS;

    float HairSecondaryRoughness;

    float HairBackLight;

    float HairShadowFillStrength;

    float3 HairGeometryNormalWS;

    float HairSpecularShift;

    float HairSecondarySpecularShift;

    float3 HairSpecularColor;

    float3 HairSecondarySpecularColor;

    float FabricIsSilk;

    float FabricFuzzWeight;

    float FabricFuzzRoughness;

    float3 FabricFuzzColor;

    float3 FoliageTransmissionColor;

    float FoliageTransmissionWeight;

    float FoliageThickness;

    float FoliageBackLight;

    float FoliageTransmissionNdotL;

    float FoliageSpecularScale;

    float FoliageUseSpecularColor;

    float FoliageScreenSpaceShadowIntensity;

    float FoliageIsGrass;

    float EyeIrisMask;

    float3 EyeIrisNormalWS;

    float3 EyeCausticNormalWS;
};

// Stores the six GBuffer color payloads; RT creation/lifetime is handled by the C# render graph.
struct BurtEncodedGBuffer
{
    // GBuffer0: normal.rgb + perceptual roughness
float4 GBuffer0;

    // GBuffer1: baseColor.rgb + occlusion
float4 GBuffer1;

    // GBuffer2: packed(shadingModelID, material channel), metallic, smoothness, reflectance
float4 GBuffer2;

    float4 GBuffer3;

    float4 GBuffer4;

    float4 GBuffer5;
};

float2 BurtWrapOctahedronNormal(float2 Value)
{
    float2 SignNotZero = float2(Value.x >= 0.0f ? 1.0f : -1.0f, Value.y >= 0.0f ? 1.0f : -1.0f);

    return (1.0f - abs(Value.yx)) * SignNotZero;
}

#endif
