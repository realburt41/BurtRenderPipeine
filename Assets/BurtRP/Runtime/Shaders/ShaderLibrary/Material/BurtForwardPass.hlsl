// Shared forward pass for lit-style BurtRP materials. Material shaders select one lighting path with BURT_MATERIAL_SHADING_MODEL_*.
#ifndef BURT_FORWARD_PASS_INCLUDED
#define BURT_FORWARD_PASS_INCLUDED

#define BURT_FORWARD_SINGLE_SHADING_MODEL 1
#define BURT_ENABLE_SHADING_DEBUG 1

#include "UnityCG.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtPreExposure.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInput.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtShadingModelMacros.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtNormal.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtEmission.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLighting.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtShadows.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtShadingDebug.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMaterialShadingModelPassCommon.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtTrunkVertexAnimation.hlsl"

struct Attributes
{
    float4 PositionOS : POSITION;
    float3 NormalOS : NORMAL;
    float4 TangentOS : TANGENT;
    float2 UV0 : TEXCOORD0;
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE) || defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_TRUNK)
    float4 Color : COLOR;
#endif
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    float2 UV1 : TEXCOORD1;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 PositionCS : SV_POSITION;
    float3 NormalWS : TEXCOORD0;
#if !defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    float2 BaseMapUV : TEXCOORD2;
#endif
    float4 TangentWS : TEXCOORD3;
    float3 PositionWS : TEXCOORD4;
    float2 EmissionMapUV : TEXCOORD5;
#if !defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    float2 MaskMapUV : TEXCOORD6;
#endif
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    float2 UV0 : TEXCOORD7;
    float2 UV1 : TEXCOORD8;
    float3 PositionOS : TEXCOORD9;
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE) || defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_TRUNK)
    float4 VertexColor : TEXCOORD7;
    float3 PositionOS : TEXCOORD8;
#endif
};

float BurtResolveForwardMaterialFoliageMicroShadow(BurtSurfaceData SurfaceData)
{
#if BURT_ENABLE_FOLIAGE_SHADING
    return BurtIsFoliageShadingModel(SurfaceData.ShadingModelID) ? saturate(SurfaceData.Occlusion) : 1.0f;
#else
    return 1.0f;
#endif
}

Varyings Vert(Attributes Input)
{
    UNITY_SETUP_INSTANCE_ID(Input);
    float4 PositionOS = BurtApplyMultipassObjectShellOffset(Input.PositionOS, Input.NormalOS);
    #if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_TRUNK)
        PositionOS = BurtApplyTrunkVertexAnimationObjectSpace(PositionOS, Input.Color, _Time.y);
    #elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
        #if defined(BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS)
        PositionOS = BurtApplyGrassVertexAnimationObjectSpace(PositionOS, Input.NormalOS, Input.Color, _Time.y);
        #else
        PositionOS = BurtApplyFoliageVertexAnimationObjectSpace(PositionOS, Input.Color, _Time.y);
        #endif
    #endif

    Varyings Output;
    Output.PositionCS = UnityObjectToClipPos(PositionOS);

    float4 PositionWS = mul(unity_ObjectToWorld, PositionOS);
    Output.PositionWS = PositionWS.xyz;

    Output.NormalWS = normalize(UnityObjectToWorldNormal(Input.NormalOS));
    Output.TangentWS = BurtObjectToWorldTangent(Input.TangentOS);
#if !defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    Output.BaseMapUV = BurtTransformBaseMapUV(Input.UV0, _BaseMap_ST);
#endif
    Output.EmissionMapUV = BurtTransformEmissionMapUV(Input.UV0, _EmissionMap_ST);
#if !defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    Output.MaskMapUV = BurtTransformMaskMapUV(Input.UV0, _MaskMap_ST);
#endif
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    Output.UV0 = Input.UV0;
    Output.UV1 = Input.UV1;
    Output.PositionOS = PositionOS.xyz;
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE) || defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_TRUNK)
    Output.VertexColor = Input.Color;
    Output.PositionOS = PositionOS.xyz;
#endif
    return Output;
}

float3 BurtGetForwardNormalWS(Varyings Input, float Facing)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    return BurtGetMaterialPassNormalWS(Input.UV0 * float2(_IDXTilling, 1.0f), Input.NormalWS, Input.TangentWS, Facing);
#else
    float3 NormalWS = BurtGetMaterialPassNormalWS(Input.BaseMapUV, Input.NormalWS, Input.TangentWS, Facing);
    #if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
        NormalWS = BurtApplyFoliageMaterialNormalWS(NormalWS, Input.PositionWS, Input.VertexColor);
    #endif
    return NormalWS;
#endif
}

float3 BurtGetForwardShadingDirectionWS(Varyings Input, float3 NormalWS, float Facing)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    return BurtGetMaterialPassShadingDirectionWS(Input.UV0, Input.NormalWS, Input.TangentWS, Facing);
#else
    return BurtGetMaterialPassShadingDirectionWS(NormalWS, Input.TangentWS);
#endif
}

float3 BurtGetForwardDebugNormalWS(float3 NormalWS, float3 ShadingDirectionWS)
{
    return BurtGetMaterialPassDebugNormalWS(NormalWS, ShadingDirectionWS);
}

BurtPBRShadingComponents BurtEvaluateForwardShadingComponents(BurtSurfaceData SurfaceData, BurtLight MainLight, Varyings Input, float3 NormalWS, float3 ShadingDirectionWS, float3 ViewDirectionWS, float Facing)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    float3 GeometryNormalWS = BurtGetMaterialPassGeometryNormalWS(Input.NormalWS, Facing);
    return BurtEvaluateHairShadingComponentsFromGBuffer(BurtCreateHairGBufferData(SurfaceData, ShadingDirectionWS, NormalWS, GeometryNormalWS, float3(0.0f, 0.0f, 0.0f)), MainLight, ViewDirectionWS, Input.PositionWS);
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_EYE)
    BurtGBufferData EyeGBufferData = BurtCreateEyeGBufferData(SurfaceData, NormalWS, Input.TangentWS, SurfaceData.EyeIrisNormalWS, SurfaceData.EyeCausticNormalWS, float3(0.0f, 0.0f, 0.0f));
    return BurtEvaluateEyeShadingComponentsFromGBuffer(EyeGBufferData, MainLight, ViewDirectionWS, Input.PositionWS);
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_CLEAR_COAT)
    float3 ClearCoatNormalWS = BurtSampleClearCoatNormalWS(Input.BaseMapUV, Input.NormalWS, Input.TangentWS, _ClearCoatNormalScale, Facing, _DoubleSidedNormalModeConstants);
    return BurtEvaluatePBRShadingComponents(SurfaceData, MainLight, NormalWS, Input.TangentWS, ClearCoatNormalWS, ViewDirectionWS, Input.PositionWS);
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FABRIC)
    return BurtEvaluatePBRShadingComponents(SurfaceData, MainLight, NormalWS, Input.TangentWS, ViewDirectionWS, Input.PositionWS);
#else
    return BurtEvaluatePBRShadingComponents(SurfaceData, MainLight, NormalWS, Input.TangentWS, ViewDirectionWS, Input.PositionWS);
#endif
}

#if defined(BURT_ENABLE_SHADING_DEBUG)
BurtGBufferData BurtCreateForwardDebugGBufferData(BurtSurfaceData SurfaceData, Varyings Input, float3 NormalWS, float3 ShadingDirectionWS, float Facing)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    float3 GeometryNormalWS = BurtGetMaterialPassGeometryNormalWS(Input.NormalWS, Facing);
    return BurtCreateMaterialPassGBufferData(SurfaceData, Input.UV0 * float2(_IDXTilling, 1.0f), GeometryNormalWS, NormalWS, Input.TangentWS, ShadingDirectionWS, Facing, float3(0.0f, 0.0f, 0.0f));
#else
    return BurtCreateMaterialPassGBufferData(SurfaceData, Input.BaseMapUV, Input.NormalWS, NormalWS, Input.TangentWS, ShadingDirectionWS, Facing, float3(0.0f, 0.0f, 0.0f));
#endif
}

float3 BurtEvaluateForwardAdditionalUnshadowedDebug(BurtSurfaceData SurfaceData, Varyings Input, float3 NormalWS, float3 ShadingDirectionWS, float3 ViewDirectionWS, float Facing)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    float3 GeometryNormalWS = BurtGetMaterialPassGeometryNormalWS(Input.NormalWS, Facing);
    BurtGBufferData HairGBufferData = BurtCreateHairGBufferData(SurfaceData, ShadingDirectionWS, NormalWS, GeometryNormalWS, float3(0.0f, 0.0f, 0.0f));
    BurtPBRGeometryData HairGeometryData = BurtPrepareHairGeometryData(HairGBufferData, ViewDirectionWS);
    BurtHairDirectComponents HairAdditional = BurtEvaluateHairAdditionalDirectLightingUnshadowedComponents(HairGBufferData, HairGeometryData, Input.PositionWS);
    return HairAdditional.Diffuse + HairAdditional.Specular;
#else
    BurtPBRShadingCoreData CoreData = BurtPreparePBRShadingCoreData(SurfaceData, NormalWS, ViewDirectionWS);
    BurtDirectPBRComponents Additional = BurtEvaluatePBRAdditionalDirectLightingUnshadowedFromCore(CoreData, Input.PositionWS);
    return Additional.Diffuse + Additional.Specular;
#endif
}

void BurtFillForwardShadingDebugData(
    inout BurtShadingDebugData DebugData,
    BurtSurfaceData SurfaceData,
    BurtSurfaceData ShadingSurfaceData,
    BurtPBRShadingComponents PBRComponents,
    BurtGBufferData DebugDecodedGBufferData,
    BurtPBRMaterialData DebugGBufferMaterialData,
    float3 NormalWS,
    float3 ShadingDirectionWS,
    float3 ViewDirectionWS,
    Varyings Input,
    float Facing,
    float3 PositionWS,
    float ShadowAttenuation,
    float3 EmissionColor,
    float3 FinalColor)
{
    DebugData.NormalWS = NormalWS;
    DebugData.DetailLightingColor = PBRComponents.Lighting;
    DebugData.DirectDiffuseColor = PBRComponents.DirectDiffuse;
    DebugData.DirectSpecularColor = PBRComponents.DirectSpecular;
    DebugData.AdditionalDiffuseColor = PBRComponents.AdditionalDiffuse;
    DebugData.AdditionalSpecularColor = PBRComponents.AdditionalSpecular;
    DebugData.AdditionalUnshadowedColor = BurtNeedsAdditionalLightingUnshadowedShadingDebug()
        ? BurtEvaluateForwardAdditionalUnshadowedDebug(ShadingSurfaceData, Input, NormalWS, ShadingDirectionWS, ViewDirectionWS, Facing)
        : float3(0.0f, 0.0f, 0.0f);
    DebugData.IndirectDiffuseColor = PBRComponents.IndirectDiffuse;
    DebugData.IndirectSpecularColor = PBRComponents.IndirectSpecular;
    DebugData.ShadowAttenuation = ShadowAttenuation;
    DebugData.AdditionalShadowAttenuation = BurtNeedsAdditionalShadowAttenuationShadingDebug()
        ? BurtEvaluateAdditionalShadowAttenuationDebug(PositionWS, NormalWS)
        : 1.0f;
    if (BurtNeedsAdditionalShadowProjectionShadingDebug())
    {
        BurtFillAdditionalLightShadowProjectionDebugData(
            PositionWS,
            NormalWS,
            DebugData.AdditionalShadowFaceColor,
            DebugData.AdditionalShadowUVColor,
            DebugData.AdditionalShadowDepthColor,
            DebugData.AdditionalShadowDepthDeltaColor);
    }

    BurtFillMainLightShadowShadingDebugData(
        PositionWS,
        DebugData.NormalWS,
        DebugData.ShadowCascadeColor,
        DebugData.ShadowCascadeBlend,
        DebugData.ShadowDistanceFade,
        DebugData.ShadowPCSSRadius,
        DebugData.ShadowReceiverDepthDelta,
        DebugData.ShadowPCSSBlockerFraction);

    BurtFillPerObjectShadowShadingDebugData(
        PositionWS,
        NormalWS,
        _BurtPerObjectShadowObjectIndex,
        DebugData.PerObjectShadowObjectIndexColor,
        DebugData.PerObjectShadowSliceColor,
        DebugData.PerObjectShadowUVColor,
        DebugData.PerObjectShadowDepthColor,
        DebugData.PerObjectShadowCompareColor,
        DebugData.PerObjectShadowTransmissionDepthColor,
        DebugData.PerObjectShadowTransmissionThicknessColor);

    DebugData.AmbientOcclusion = SurfaceData.Occlusion;
    DebugData.EmissionColor = EmissionColor;
    DebugData.FinalLightingColor = FinalColor;
    DebugData.Reflectance = SurfaceData.Reflectance;
    DebugData.PerceptualRoughness = PBRComponents.PerceptualRoughness;
    DebugData.SpecularAARoughness = PBRComponents.SpecularAARoughness;
    DebugData.SpecularEnergyCompensation = PBRComponents.SpecularEnergyCompensation;
    DebugData.IndirectSpecularEnergyCompensation = PBRComponents.IndirectSpecularEnergyCompensation;
    DebugData.EnergyPreservation = PBRComponents.EnergyPreservation;
    DebugData.SpecularOcclusion = PBRComponents.SpecularOcclusion;
    DebugData.DiffuseColor = PBRComponents.DiffuseColor;
    DebugData.DirectBRDFD = PBRComponents.DirectBRDFD;
    DebugData.DirectBRDFVisibility = PBRComponents.DirectBRDFVisibility;
    DebugData.DirectBRDFFresnel = PBRComponents.DirectBRDFFresnel;
    DebugData.DirectDiffuseLobe = PBRComponents.DirectDiffuseLobe;
    DebugData.DirectDiffuseBRDF = PBRComponents.DirectDiffuseBRDF;
    DebugData.DirectSpecularBRDF = PBRComponents.DirectSpecularBRDF;
    DebugData.SpecularAANormalVariance = PBRComponents.SpecularAANormalVariance;
    DebugData.SpecularAARoughnessDelta = PBRComponents.SpecularAARoughnessDelta;
    DebugData.IndirectSpecularDFG = PBRComponents.IndirectSpecularDFG;
    DebugData.IndirectSpecularEnvBRDF = PBRComponents.IndirectSpecularEnvBRDF;
    DebugData.SubsurfaceProfileIndex = PBRComponents.SubsurfaceProfileIndex;
    DebugData.SubsurfaceTransmission = PBRComponents.SubsurfaceTransmission;
    DebugData.SubsurfaceDirectTransmission = PBRComponents.SubsurfaceDirectTransmission;
    DebugData.SubsurfaceTransmissionBRDF = PBRComponents.SubsurfaceTransmissionBRDF;
    DebugData.SubsurfaceTransmissionShadow = PBRComponents.SubsurfaceTransmissionShadow;
    DebugData.SubsurfaceTransmissionPhase = PBRComponents.SubsurfaceTransmissionPhase;
    DebugData.SubsurfaceTransmissionThickness = PBRComponents.SubsurfaceTransmissionThickness;
    DebugData.SubsurfaceKernelWeight = PBRComponents.SubsurfaceKernelWeight;
    DebugData.SubsurfaceIndirect = PBRComponents.SubsurfaceIndirect;
    DebugData.FoliageMask = PBRComponents.FoliageMask;
    DebugData.FoliageTransmission = PBRComponents.FoliageTransmission;
    DebugData.FoliageDirectTransmission = PBRComponents.FoliageDirectTransmission;
    DebugData.FoliageTransmissionBRDF = PBRComponents.FoliageTransmissionBRDF;
    DebugData.FoliageTransmissionShadow = PBRComponents.FoliageTransmissionShadow;
    DebugData.FoliageSpecularBRDF = PBRComponents.FoliageSpecularBRDF;
    DebugData.HairPrimaryLobe = PBRComponents.HairPrimaryLobe;
    DebugData.HairSecondaryLobe = PBRComponents.HairSecondaryLobe;
    DebugData.HairTransmissionLobe = PBRComponents.HairTransmissionLobe;
    DebugData.HairScatter = PBRComponents.HairScatter;
    DebugData.GBufferBaseColor = DebugDecodedGBufferData.BaseColor;
    DebugData.GBufferNormalWS = BurtGetForwardDebugNormalWS(BurtGetDefaultLitNormalWS(DebugDecodedGBufferData), BurtGetHairStrandDirectionWS(DebugDecodedGBufferData));
    DebugData.GBufferMetallic = BurtGetGBufferMaterialChannel(DebugDecodedGBufferData);
    DebugData.GBufferClearCoatMask = BurtGetClearCoatMask(DebugDecodedGBufferData);
    DebugData.GBufferClearCoatNormalWS = BurtGetClearCoatNormalWS(DebugDecodedGBufferData);
    DebugData.GBufferClearCoatRoughness = BurtGetClearCoatRoughness(DebugDecodedGBufferData);
    DebugData.GBufferSubsurfaceStrength = BurtGetSubsurfaceStrength(DebugDecodedGBufferData);
    DebugData.GBufferSubsurfaceThickness = BurtGetSubsurfaceThickness(DebugDecodedGBufferData);
    DebugData.GBufferSubsurfaceProfileIndex = BurtGetSubsurfaceProfileIndex(DebugDecodedGBufferData);
    DebugData.GBufferAnisotropy = DebugDecodedGBufferData.Anisotropy;
    DebugData.GBufferTangentWS = DebugDecodedGBufferData.TangentWS;
    DebugData.GBufferSmoothness = DebugDecodedGBufferData.Smoothness;
    DebugData.GBufferOcclusion = DebugDecodedGBufferData.Occlusion;
    DebugData.GBufferReflectance = DebugDecodedGBufferData.Reflectance;
    DebugData.GBufferRoughness = DebugGBufferMaterialData.PerceptualRoughness;
    DebugData.GBufferDiffuseColor = DebugGBufferMaterialData.DiffuseColor;
}
#endif

float4 Frag(Varyings Input, fixed Facing : VFACE) : SV_Target
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_EYE)
    float3 ViewDirectionWS = BurtSafeNormalize(_WorldSpaceCameraPos.xyz - Input.PositionWS);
    BurtEyeMaterialData EyeData = BurtEvaluateEyeMaterialData(Input.BaseMapUV, Input.NormalWS, Input.TangentWS, ViewDirectionWS, Facing);
    float4 BaseColor = EyeData.BaseColor;
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    float4 MaskMap = BurtEvaluateMaterialPassMaskMap(Input.UV0, Input.UV1);
    float4 BaseColor = BurtEvaluateMaterialPassBaseColor(Input.UV0, Input.UV1, Input.PositionOS, MaskMap);
#else
    #if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
        float4 BaseColor = BurtEvaluateMaterialPassBaseColor(Input.BaseMapUV, Input.PositionWS, Input.PositionOS, Input.VertexColor);
    #else
        float4 BaseColor = BurtSampleBaseMap(Input.BaseMapUV) * _BaseColor;
    #endif
#endif
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_EYE)
    BurtApplyMaterialPassAlphaClip(BaseColor.a, _AlphaClip, _Cutoff, Input.PositionCS);
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    BurtApplyMaterialPassAlphaClip(BaseColor.a, _AlphaClip, _Cutoff, Input.PositionCS);
#else
    float Alpha = BurtEvaluateMaterialPassOpacity(BaseColor.a, Input.BaseMapUV, Input.PositionWS);
    BurtApplyMaterialPassAlphaClip(Alpha, _AlphaClip, _Cutoff, Input.PositionCS);
    BaseColor.a = Alpha;
#endif

#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_EYE)
    float3 NormalWS = EyeData.NormalWS;
    float3 ShadingDirectionWS = NormalWS;
#else
    float3 NormalWS = BurtGetForwardNormalWS(Input, Facing);
    #if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
        NormalWS = BurtApplyFoliageMaterialNormalWS(NormalWS, Input.PositionWS, Input.VertexColor);
    #endif
    float3 ShadingDirectionWS = BurtGetForwardShadingDirectionWS(Input, NormalWS, Facing);
    float3 ViewDirectionWS = BurtSafeNormalize(_WorldSpaceCameraPos.xyz - Input.PositionWS);
#endif
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_EYE)
    BurtSurfaceData SurfaceData = BurtCreateEyeSurfaceData(EyeData);
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    BurtSurfaceData SurfaceData = BurtCreateMaterialShadingModelSurfaceData(BaseColor, MaskMap, Input.UV0, Input.UV1, Input.PositionOS, Input.NormalWS, Input.TangentWS, ViewDirectionWS);
#else
    float4 MaskMap = BurtSampleMaskMap(Input.MaskMapUV);
    #if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
        BurtSurfaceData SurfaceData = BurtCreateMaterialShadingModelSurfaceData(BaseColor, MaskMap, Input.BaseMapUV, NormalWS, ViewDirectionWS, Input.PositionWS, Input.PositionOS, Input.VertexColor);
    #elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_TRUNK)
        BurtSurfaceData SurfaceData = BurtCreateMaterialShadingModelSurfaceData(BaseColor, MaskMap, Input.BaseMapUV, NormalWS, ViewDirectionWS, Input.PositionWS, Input.PositionOS, Input.VertexColor);
    #else
        BurtSurfaceData SurfaceData = BurtCreateMaterialShadingModelSurfaceData(BaseColor, MaskMap, Input.BaseMapUV, NormalWS, ViewDirectionWS, Input.PositionWS);
    #endif
#endif
    float ShadowAttenuation = BurtSampleMainLightShadow(Input.PositionWS, NormalWS, _BurtPerObjectShadowObjectIndex);
    ShadowAttenuation *= BurtResolveForwardMaterialFoliageMicroShadow(SurfaceData);
    float TransmissionThickness = BurtResolvePerObjectShadowTransmissionThickness(Input.PositionWS, -1.0f);
    float TransmissionShadowAttenuation = BurtSampleMainLightTransmissionShadow(Input.PositionWS, NormalWS, _BurtPerObjectShadowObjectIndex, TransmissionThickness);
    BurtLight MainLight = BurtCreateMainLight(ShadowAttenuation, TransmissionShadowAttenuation, TransmissionThickness);
    BurtSurfaceData ShadingSurfaceData = SurfaceData;

#if defined(BURT_ENABLE_SHADING_DEBUG)
    if (BurtIsShadingDebugEnabled() && BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DETAIL_LIGHTING))
    {
        ShadingSurfaceData.BaseColor.rgb = float3(0.18f, 0.18f, 0.18f);
    }
#endif

    BurtPBRShadingComponents PBRComponents = BurtEvaluateForwardShadingComponents(ShadingSurfaceData, MainLight, Input, NormalWS, ShadingDirectionWS, ViewDirectionWS, Facing);
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_EYE)
    float3 EmissionColor = EyeData.EmissionColor;
#else
    float3 EmissionColor = BurtEvaluateEmission(Input.EmissionMapUV, _EmissionColor.rgb);
#endif
    float3 FinalColor = PBRComponents.Lighting + EmissionColor;

#if defined(BURT_ENABLE_SHADING_DEBUG)
    if (!BurtIsShadingDebugEnabled())
    {
        return float4(BurtApplyPreExposure(FinalColor), SurfaceData.Alpha);
    }

    BurtGBufferData DebugGBufferSourceData = BurtCreateForwardDebugGBufferData(SurfaceData, Input, NormalWS, ShadingDirectionWS, Facing);
    BurtEncodedGBuffer DebugEncodedGBuffer = BurtEncodeGBuffer(DebugGBufferSourceData);
    BurtGBufferData DebugDecodedGBufferData = BurtDecodeGBuffer(DebugEncodedGBuffer);
    BurtPBRMaterialData DebugGBufferMaterialData = BurtPreparePBRMaterialData(DebugDecodedGBufferData);
    BurtShadingDebugData DebugData = BurtCreateDefaultShadingDebugData(NormalWS);
    BurtFillForwardShadingDebugData(
        DebugData,
        SurfaceData,
        ShadingSurfaceData,
        PBRComponents,
        DebugDecodedGBufferData,
        DebugGBufferMaterialData,
        NormalWS,
        ShadingDirectionWS,
        ViewDirectionWS,
        Input,
        Facing,
        Input.PositionWS,
        ShadowAttenuation,
        EmissionColor,
        FinalColor);

#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_SUBSURFACE)
    BurtSurfaceData DebugLightingSurfaceData = ShadingSurfaceData;
    DebugLightingSurfaceData.ShadingModelID = BURT_SHADING_MODEL_DEFAULT_LIT;
    BurtPBRShadingComponents DebugLightingComponents = BurtEvaluateForwardShadingComponents(
        DebugLightingSurfaceData,
        MainLight,
        Input,
        NormalWS,
        ShadingDirectionWS,
        ViewDirectionWS,
        Facing);
    DebugData.DetailLightingColor = DebugLightingComponents.Lighting;
    DebugData.DirectDiffuseColor = DebugLightingComponents.DirectDiffuse;
    DebugData.DirectSpecularColor = DebugLightingComponents.DirectSpecular;
    DebugData.AdditionalDiffuseColor = DebugLightingComponents.AdditionalDiffuse;
    DebugData.AdditionalSpecularColor = DebugLightingComponents.AdditionalSpecular;
    DebugData.AdditionalUnshadowedColor = BurtNeedsAdditionalLightingUnshadowedShadingDebug()
        ? BurtEvaluateForwardAdditionalUnshadowedDebug(DebugLightingSurfaceData, Input, NormalWS, ShadingDirectionWS, ViewDirectionWS, Facing)
        : float3(0.0f, 0.0f, 0.0f);
    DebugData.IndirectDiffuseColor = DebugLightingComponents.IndirectDiffuse;
    DebugData.IndirectSpecularColor = DebugLightingComponents.IndirectSpecular;
    DebugData.PerceptualRoughness = DebugLightingComponents.PerceptualRoughness;
    DebugData.SpecularAARoughness = DebugLightingComponents.SpecularAARoughness;
    DebugData.SpecularEnergyCompensation = DebugLightingComponents.SpecularEnergyCompensation;
    DebugData.IndirectSpecularEnergyCompensation = DebugLightingComponents.IndirectSpecularEnergyCompensation;
    DebugData.EnergyPreservation = DebugLightingComponents.EnergyPreservation;
    DebugData.SpecularOcclusion = DebugLightingComponents.SpecularOcclusion;
    DebugData.DiffuseColor = DebugLightingComponents.DiffuseColor;
    DebugData.DirectBRDFD = DebugLightingComponents.DirectBRDFD;
    DebugData.DirectBRDFVisibility = DebugLightingComponents.DirectBRDFVisibility;
    DebugData.DirectBRDFFresnel = DebugLightingComponents.DirectBRDFFresnel;
    DebugData.DirectDiffuseLobe = DebugLightingComponents.DirectDiffuseLobe;
    DebugData.DirectDiffuseBRDF = DebugLightingComponents.DirectDiffuseBRDF;
    DebugData.DirectSpecularBRDF = DebugLightingComponents.DirectSpecularBRDF;
    DebugData.SpecularAANormalVariance = DebugLightingComponents.SpecularAANormalVariance;
    DebugData.SpecularAARoughnessDelta = DebugLightingComponents.SpecularAARoughnessDelta;
    DebugData.IndirectSpecularDFG = DebugLightingComponents.IndirectSpecularDFG;
    DebugData.IndirectSpecularEnvBRDF = DebugLightingComponents.IndirectSpecularEnvBRDF;
#endif

    float3 DebugColor;
    if (BurtTryEvaluateMaterialShadingDebug(SurfaceData, DebugData, DebugColor))
    {
        return float4(DebugColor, SurfaceData.Alpha);
    }
#endif

    return float4(BurtApplyPreExposure(FinalColor), SurfaceData.Alpha);
}

#endif // BURT_FORWARD_PASS_INCLUDED
