// Shared GBuffer pass wiring. Material shaders select one packing path with BURT_MATERIAL_SHADING_MODEL_*.
#ifndef BURT_GBUFFER_PASS_INCLUDED
#define BURT_GBUFFER_PASS_INCLUDED

#define BURT_MATERIAL_GBUFFER_SINGLE_SHADING_MODEL 1

#include "UnityCG.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInput.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtShadingModelMacros.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtNormal.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtEmission.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBuffer.hlsl"
#if defined(BURT_MATERIAL_SELECTED_INTERIOR_MAPPING)
    #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInteriorMappingPass.hlsl"
#endif
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMaterialShadingModelPassCommon.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtTrunkVertexAnimation.hlsl"

int _BurtPerObjectShadowObjectIndex;

#if defined(BURT_USE_PRESKIN_POSITION) && BURT_USE_PRESKIN_POSITION && defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_SUBSURFACE)
    #define BURT_MATERIAL_ENABLE_PRESKIN_POSITION 1
#else
    #define BURT_MATERIAL_ENABLE_PRESKIN_POSITION 0
#endif

#if BURT_MATERIAL_ENABLE_PRESKIN_POSITION
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtPreSkinPosition.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtShadingDebugModes.hlsl"
#endif

struct GBufferAttributes
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
#if BURT_MATERIAL_ENABLE_PRESKIN_POSITION
    #if BURT_PRESKIN_POSITION_UV3_PACKED
        uint2 PreSkinPositionUV3 : TEXCOORD3;
    #else
        float3 PreSkinPositionUV3 : TEXCOORD3;
    #endif
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct GBufferVaryings
{
    float4 PositionCS : SV_POSITION;
    float3 NormalWS : TEXCOORD0;
#if !defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    float2 BaseMapUV : TEXCOORD1;
#endif
    float4 TangentWS : TEXCOORD2;
#if !defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    float2 MaskMapUV : TEXCOORD3;
#endif
    float2 EmissionMapUV : TEXCOORD4;
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    float2 UV0 : TEXCOORD5;
    float2 UV1 : TEXCOORD6;
    float3 PositionOS : TEXCOORD7;
    float3 PositionWS : TEXCOORD8;
#else
    float3 PositionWS : TEXCOORD5;
    #if BURT_MATERIAL_ENABLE_PRESKIN_POSITION
        float3 PreSkinPositionOS : TEXCOORD6;
    #else
        #if defined(BURT_MATERIAL_SELECTED_INTERIOR_MAPPING)
            float2 UV0 : TEXCOORD6;
        #endif
        #if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE) || defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_TRUNK)
            float4 VertexColor : TEXCOORD6;
            float3 PositionOS : TEXCOORD7;
        #endif
    #endif
#endif
};

struct GBufferFragmentOutput
{
    float4 GBuffer0 : SV_Target0;
    float4 GBuffer1 : SV_Target1;
    float4 GBuffer2 : SV_Target2;
    float4 GBuffer3 : SV_Target3;
    float4 GBuffer4 : SV_Target4;
    float4 GBuffer5 : SV_Target5;
    float4 ObjectIndex : SV_Target6;
};

float4 BurtEncodePerObjectShadowObjectIndexTarget()
{
    return float4(saturate((float)max(_BurtPerObjectShadowObjectIndex, 0) / 255.0f), 0.0f, 0.0f, 1.0f);
}

#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_SUBSURFACE)
struct SubsurfaceForwardFragmentOutput
{
    float4 GBuffer0 : SV_Target0;
    float4 GBuffer2 : SV_Target1;
};

float BurtEncodeSubsurfaceProfileIDAndTypeForScreenSpacePass(BurtSurfaceData SurfaceData)
{
    if (BurtIsSubsurface3SPreIntegratedMode(SurfaceData.SubsurfaceScatteringMode))
    {
        return 0.0f;
    }

    float ProfileID = BurtClampSubsurfaceProfileIndex(SurfaceData.SubsurfaceProfileIndex);
    float ProfileType = BurtIsSubsurface4SSeparableMode(SurfaceData.SubsurfaceScatteringMode)
        ? BURT_SSS_PROFILE_TYPE_SEPARABLE_FLOAT
        : BURT_SSS_PROFILE_TYPE_BURLEY_FLOAT;
    return (ProfileType + ProfileID) / 255.0f;
}
#endif

GBufferVaryings VertGBuffer(GBufferAttributes Input)
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

    GBufferVaryings Output;
    Output.PositionCS = UnityObjectToClipPos(PositionOS);
    Output.NormalWS = BurtSafeNormalize(UnityObjectToWorldNormal(Input.NormalOS));
    Output.TangentWS = BurtObjectToWorldTangent(Input.TangentOS);
#if !defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    Output.BaseMapUV = BurtTransformBaseMapUV(Input.UV0, _BaseMap_ST);
    Output.MaskMapUV = BurtTransformMaskMapUV(Input.UV0, _MaskMap_ST);
    Output.PositionWS = mul(unity_ObjectToWorld, PositionOS).xyz;
    #if BURT_MATERIAL_ENABLE_PRESKIN_POSITION
        Output.PreSkinPositionOS = _BurtSkinnedDecalUseMeshPosition > 0.5f
            ? PositionOS.xyz
            : BurtDecodePreSkinPositionOS(Input.PreSkinPositionUV3);
    #else
        #if defined(BURT_MATERIAL_SELECTED_INTERIOR_MAPPING)
            Output.UV0 = Input.UV0;
        #endif
        #if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE) || defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_TRUNK)
            Output.VertexColor = Input.Color;
            Output.PositionOS = PositionOS.xyz;
        #endif
    #endif
#endif
    Output.EmissionMapUV = BurtTransformEmissionMapUV(Input.UV0, _EmissionMap_ST);
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    Output.UV0 = Input.UV0;
    Output.UV1 = Input.UV1;
    Output.PositionOS = PositionOS.xyz;
    Output.PositionWS = mul(unity_ObjectToWorld, PositionOS).xyz;
#endif
    return Output;
}

#if BURT_MATERIAL_ENABLE_PRESKIN_POSITION
bool BurtShouldWriteSubsurfacePreSkinPositionDebug()
{
    return _BurtShadingDebugEnabled > 0.5f
        && abs(_BurtShadingDebugMode - BURT_SHADING_DEBUG_MODE_PRESKIN_POSITION) < 0.5f;
}

void BurtApplySubsurfacePreSkinPositionDebug(inout float4 BaseColor, float3 PreSkinPositionOS)
{
    if (BurtShouldWriteSubsurfacePreSkinPositionDebug())
    {
        BaseColor.rgb = BurtEncodePreSkinPositionForDebug(PreSkinPositionOS);
    }
}
#endif

GBufferFragmentOutput BurtPackGBufferOutput(BurtEncodedGBuffer EncodedGBuffer)
{
    GBufferFragmentOutput Output;
    Output.GBuffer0 = EncodedGBuffer.GBuffer0;
    Output.GBuffer1 = EncodedGBuffer.GBuffer1;
    Output.GBuffer2 = EncodedGBuffer.GBuffer2;
    Output.GBuffer3 = EncodedGBuffer.GBuffer3;
    Output.GBuffer4 = EncodedGBuffer.GBuffer4;
    Output.GBuffer5 = EncodedGBuffer.GBuffer5;
    Output.ObjectIndex = BurtEncodePerObjectShadowObjectIndexTarget();
    return Output;
}

BurtGBufferData BurtCreateMaterialGBufferData(GBufferVaryings Input, float Facing, BurtSurfaceData SurfaceData, float3 EmissionColor)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    float2 HairNormalMapUV = Input.UV0 * float2(_IDXTilling, 1.0f);
    float3 BaseNormalWS = BurtGetMaterialPassNormalWS(HairNormalMapUV, Input.NormalWS, Input.TangentWS, Facing);
    float3 GeometryNormalWS = BurtGetMaterialPassGeometryNormalWS(Input.NormalWS, Facing);
    float3 ShadingDirectionWS = BurtGetMaterialPassShadingDirectionWS(Input.UV0, Input.NormalWS, Input.TangentWS, Facing);
    return BurtCreateMaterialPassGBufferData(SurfaceData, HairNormalMapUV, GeometryNormalWS, BaseNormalWS, Input.TangentWS, ShadingDirectionWS, Facing, EmissionColor);
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_EYE)
    float3 BaseNormalWS = BurtEyeSampleNormalWS(Input.BaseMapUV, Input.NormalWS, Input.TangentWS, Facing, SurfaceData.EyeIrisMask);
    return BurtCreateEyeGBufferData(SurfaceData, BaseNormalWS, Input.TangentWS, SurfaceData.EyeIrisNormalWS, SurfaceData.EyeCausticNormalWS, EmissionColor);
#else
    float3 BaseNormalWS = BurtGetMaterialPassNormalWS(Input.BaseMapUV, Input.NormalWS, Input.TangentWS, Facing);
    #if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
        BaseNormalWS = BurtApplyFoliageMaterialNormalWS(BaseNormalWS, Input.PositionWS, Input.VertexColor);
    #endif
    float3 ShadingDirectionWS = BurtGetMaterialPassShadingDirectionWS(BaseNormalWS, Input.TangentWS);
    return BurtCreateMaterialPassGBufferData(SurfaceData, Input.BaseMapUV, Input.NormalWS, BaseNormalWS, Input.TangentWS, ShadingDirectionWS, Facing, EmissionColor);
#endif
}

BurtGBufferData BurtCreateMaterialPassGBufferDataFromInput(GBufferVaryings Input, float Facing)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_EYE)
    float3 ViewDirectionWS = BurtSafeNormalize(_WorldSpaceCameraPos.xyz - Input.PositionWS);
    BurtEyeMaterialData EyeData = BurtEvaluateEyeMaterialData(Input.BaseMapUV, Input.NormalWS, Input.TangentWS, ViewDirectionWS, Facing);
    BurtApplyMaterialPassAlphaClip(EyeData.BaseColor.a, _AlphaClip, _Cutoff, Input.PositionCS);
    BurtSurfaceData SurfaceData = BurtCreateEyeSurfaceData(EyeData);
    return BurtCreateEyeGBufferData(SurfaceData, EyeData.NormalWS, Input.TangentWS, EyeData.IrisNormalWS, EyeData.CausticNormalWS, EyeData.EmissionColor);
#else
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    float4 MaskMap = BurtEvaluateMaterialPassMaskMap(Input.UV0, Input.UV1);
    float4 BaseColor = BurtEvaluateMaterialPassBaseColor(Input.UV0, Input.UV1, Input.PositionOS, MaskMap);
#else
    #if defined(BURT_MATERIAL_SELECTED_INTERIOR_MAPPING)
        float4 BaseColor = float4(0.0f, 0.0f, 0.0f, 1.0f);
    #elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
        float4 BaseColor = BurtEvaluateMaterialPassBaseColor(Input.BaseMapUV, Input.PositionWS, Input.PositionOS, Input.VertexColor);
    #else
        float4 BaseColor = BurtSampleBaseMap(Input.BaseMapUV) * _BaseColor;
    #endif
#endif
#if BURT_MATERIAL_ENABLE_PRESKIN_POSITION
    BurtApplySubsurfacePreSkinPositionDebug(BaseColor, Input.PreSkinPositionOS);
#endif
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    BurtApplyMaterialPassAlphaClip(BaseColor.a, _AlphaClip, _Cutoff, Input.PositionCS);
#else
    float Alpha = BurtEvaluateMaterialPassOpacity(BaseColor.a, Input.BaseMapUV, Input.PositionWS);
    BurtApplyMaterialPassAlphaClip(Alpha, _AlphaClip, _Cutoff, Input.PositionCS);
    BaseColor.a = Alpha;
#endif

#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    float3 ViewDirectionWS = BurtSafeNormalize(_WorldSpaceCameraPos.xyz - Input.PositionWS);
    BurtSurfaceData SurfaceData = BurtCreateMaterialShadingModelSurfaceData(BaseColor, MaskMap, Input.UV0, Input.UV1, Input.PositionOS, Input.NormalWS, Input.TangentWS, ViewDirectionWS);
#else
    #if defined(BURT_MATERIAL_SELECTED_INTERIOR_MAPPING)
        float4 MaskMap = float4(0.0f, 1.0f, 0.5f, 1.0f);
    #else
        float4 MaskMap = BurtSampleMaskMap(Input.MaskMapUV);
    #endif
    float3 ViewDirectionWS = BurtSafeNormalize(_WorldSpaceCameraPos.xyz - Input.PositionWS);
    float3 BaseNormalWS = BurtGetMaterialPassNormalWS(Input.BaseMapUV, Input.NormalWS, Input.TangentWS, Facing);
    #if BURT_MATERIAL_ENABLE_PRESKIN_POSITION && defined(BURT_SKINNED_DECAL)
        BurtApplySkinnedDecals(BaseColor, MaskMap, BaseNormalWS, Input.NormalWS, Input.TangentWS, Input.PreSkinPositionOS);
    #endif
    #if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
        BurtSurfaceData SurfaceData = BurtCreateMaterialShadingModelSurfaceData(BaseColor, MaskMap, Input.BaseMapUV, BaseNormalWS, ViewDirectionWS, Input.PositionWS, Input.PositionOS, Input.VertexColor);
    #elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_TRUNK)
        BurtSurfaceData SurfaceData = BurtCreateMaterialShadingModelSurfaceData(BaseColor, MaskMap, Input.BaseMapUV, BaseNormalWS, ViewDirectionWS, Input.PositionWS, Input.PositionOS, Input.VertexColor);
    #else
        BurtSurfaceData SurfaceData = BurtCreateMaterialShadingModelSurfaceData(BaseColor, MaskMap, Input.BaseMapUV, BaseNormalWS, ViewDirectionWS, Input.PositionWS);
    #endif
#endif
    #if defined(BURT_MATERIAL_DEPTH_NORMALS_PASS)
    float3 EmissionColor = float3(0.0f, 0.0f, 0.0f);
    #elif defined(BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS)
    float3 EmissionColor = float3(0.0f, 0.0f, 0.0f);
    #elif defined(BURT_MATERIAL_SELECTED_INTERIOR_MAPPING)
    float3 EmissionColor = BurtEvaluateInteriorMappingEmission(Input.UV0, Input.BaseMapUV, Input.PositionWS, Input.NormalWS, Input.TangentWS, Input.PositionCS);
    #else
    float3 EmissionColor = BurtEvaluateEmission(Input.EmissionMapUV, _EmissionColor.rgb);
    #endif

    return BurtCreateMaterialGBufferData(Input, Facing, SurfaceData, EmissionColor);
#endif
}

GBufferFragmentOutput FragGBuffer(GBufferVaryings Input, fixed Facing : VFACE)
{
    BurtGBufferData GBufferData = BurtCreateMaterialPassGBufferDataFromInput(Input, Facing);
    return BurtPackGBufferOutput(BurtEncodeGBuffer(GBufferData));
}

#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_SUBSURFACE)
SubsurfaceForwardFragmentOutput FragSubsurfaceForward(GBufferVaryings Input, fixed Facing : VFACE)
{
    float4 BaseColor = BurtSampleBaseMap(Input.BaseMapUV) * _BaseColor;
    float4 MaskMap = BurtSampleMaskMap(Input.MaskMapUV);
#if BURT_MATERIAL_ENABLE_PRESKIN_POSITION
    #if defined(BURT_SKINNED_DECAL)
    float3 DecalNormalWS = BurtGetMaterialPassNormalWS(Input.BaseMapUV, Input.NormalWS, Input.TangentWS, Facing);
    BurtApplySkinnedDecals(BaseColor, MaskMap, DecalNormalWS, Input.NormalWS, Input.TangentWS, Input.PreSkinPositionOS);
    #endif
    BurtApplySubsurfacePreSkinPositionDebug(BaseColor, Input.PreSkinPositionOS);
#endif
    BurtApplyMaterialPassAlphaClip(BaseColor.a, _AlphaClip, _Cutoff, Input.PositionCS);

    BurtSurfaceData SurfaceData = BurtCreateMaterialShadingModelSurfaceData(BaseColor, MaskMap, Input.BaseMapUV);
    float3 EmissionColor = BurtEvaluateEmission(Input.EmissionMapUV, _EmissionColor.rgb);

    SubsurfaceForwardFragmentOutput Output;
    Output.GBuffer0 = float4(saturate(SurfaceData.BaseColor.rgb), BurtEncodeSubsurfaceProfileIDAndTypeForScreenSpacePass(SurfaceData));
    Output.GBuffer2 = float4(max(EmissionColor, float3(0.0f, 0.0f, 0.0f)), saturate(SurfaceData.Reflectance));
    return Output;
}
#endif

#endif // BURT_GBUFFER_PASS_INCLUDED
