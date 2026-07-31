// Shared forward pass for lit-style BurtRP materials. Material shaders select one lighting path with BURT_MATERIAL_SHADING_MODEL_*.
#ifndef BURT_FORWARD_PASS_INCLUDED
#define BURT_FORWARD_PASS_INCLUDED

#define BURT_FORWARD_SINGLE_SHADING_MODEL 1
#if (defined(BURT_USE_DEBUG_MODE_FORWARD) || (defined(BURT_COMPILE_SHADING_DEBUG) && BURT_COMPILE_SHADING_DEBUG)) && !defined(BURT_ENABLE_SHADING_DEBUG)
#define BURT_ENABLE_SHADING_DEBUG 1
#endif

#if defined(BURT_ENABLE_SHADING_DEBUG) && BURT_ENABLE_SHADING_DEBUG
#define BURT_PBR_SHADING_COMPONENTS_INCLUDE_BRDF_DEBUG 1
#define BURT_PBR_SHADING_COMPONENTS_INCLUDE_TRANSMISSION_DEBUG 1
#else
#define BURT_PBR_SHADING_COMPONENTS_INCLUDE_BRDF_DEBUG 0
#define BURT_PBR_SHADING_COMPONENTS_INCLUDE_TRANSMISSION_DEBUG 0
#endif

#include "UnityCG.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtPreExposure.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInput.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtShadingModelMacros.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtNormal.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtEmission.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLighting.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtShadows.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtTransparentAtmosphereFog.hlsl"
#if defined(BURT_ENABLE_SHADING_DEBUG) && BURT_ENABLE_SHADING_DEBUG
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtShadingDebugCommon.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLightingAdditionalLightsDebug.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtShadingDebugShadow.hlsl"
#endif
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMaterialShadingModelPassCommon.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtTrunkVertexAnimation.hlsl"

sampler2D _BurtOpaqueCameraColorTexture;
UNITY_DECLARE_DEPTH_TEXTURE(_BurtCameraDepthTexture);
float _BurtOpaqueCameraColorAvailable;

#ifndef BURT_FORWARD_ENABLE_REFRACTION
#define BURT_FORWARD_ENABLE_REFRACTION 0
#endif

#if defined(BURT_USE_PRESKIN_POSITION) && BURT_USE_PRESKIN_POSITION && defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_SUBSURFACE)
    #define BURT_FORWARD_ENABLE_PRESKIN_POSITION 1
#else
    #define BURT_FORWARD_ENABLE_PRESKIN_POSITION 0
#endif

#if BURT_FORWARD_ENABLE_PRESKIN_POSITION
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtPreSkinPosition.hlsl"
#endif

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
#if BURT_FORWARD_ENABLE_PRESKIN_POSITION
    #if BURT_PRESKIN_POSITION_UV3_PACKED
        uint2 PreSkinPositionUV3 : TEXCOORD3;
    #else
        float3 PreSkinPositionUV3 : TEXCOORD3;
    #endif
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 PositionCS : SV_POSITION;
    float3 NormalWS : TEXCOORD0;
    float4 ScreenPos : TEXCOORD1;
#if !defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    float2 BaseMapUV : TEXCOORD2;
#endif
    float4 TangentWS : TEXCOORD3;
    float3 PositionWS : TEXCOORD4;
    float2 EmissionMapUV : TEXCOORD5;
#if !defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    float2 MaskMapUV : TEXCOORD6;
#endif
#if BURT_FORWARD_ENABLE_PRESKIN_POSITION
    float3 PreSkinPositionOS : TEXCOORD7;
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    float2 UV0 : TEXCOORD7;
    float2 UV1 : TEXCOORD8;
    float3 PositionOS : TEXCOORD9;
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE) || defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_TRUNK)
    float4 VertexColor : TEXCOORD7;
    float3 PositionOS : TEXCOORD8;
#endif
#if defined(BURT_TRANSPARENT_VERTEX_FOG) && !defined(BURT_IGNORE_FOG)
    float4 TransparentFog : TEXCOORD10;
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
    Output.ScreenPos = ComputeScreenPos(Output.PositionCS);

    float4 PositionWS = mul(unity_ObjectToWorld, PositionOS);
    Output.PositionWS = PositionWS.xyz;
#if defined(BURT_TRANSPARENT_VERTEX_FOG) && !defined(BURT_IGNORE_FOG)
    float2 transparentFogScreenUV = saturate(
        Output.ScreenPos.xy / max(Output.ScreenPos.w, BURT_EPSILON));
    Output.TransparentFog = BurtEvaluateTransparentFog(
        transparentFogScreenUV,
        Output.PositionWS);
#endif

    Output.NormalWS = normalize(UnityObjectToWorldNormal(Input.NormalOS));
    Output.TangentWS = BurtObjectToWorldTangent(Input.TangentOS);
#if !defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    Output.BaseMapUV = BurtTransformBaseMapUV(Input.UV0, _BaseMap_ST);
#endif
    Output.EmissionMapUV = BurtTransformEmissionMapUV(Input.UV0, _EmissionMap_ST);
#if !defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    Output.MaskMapUV = BurtTransformMaskMapUV(Input.UV0, _MaskMap_ST);
#endif
#if BURT_FORWARD_ENABLE_PRESKIN_POSITION
    Output.PreSkinPositionOS = _BurtSkinnedDecalUseMeshPosition > 0.5f
        ? PositionOS.xyz
        : BurtDecodePreSkinPositionOS(Input.PreSkinPositionUV3);
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
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

float2 BurtGetForwardScreenUV(Varyings Input)
{
    return saturate(Input.ScreenPos.xy / max(Input.ScreenPos.w, BURT_EPSILON));
}

float BurtGetForwardLinearEyeDepth(float3 PositionWS)
{
    return max(-mul(UNITY_MATRIX_V, float4(PositionWS, 1.0f)).z, 1.0e-4f);
}

float3 BurtSampleForwardOpaqueCameraColor(float2 ScreenUV)
{
    return BurtRemovePreExposure(tex2D(_BurtOpaqueCameraColorTexture, ScreenUV).rgb);
}

#if BURT_FORWARD_ENABLE_REFRACTION
float BurtGetForwardRefractionSqrtVarianceFromRoughness(float Roughness)
{
    return saturate(Roughness * Roughness * 0.00173056f);
}

float BurtGetForwardRoughRefractionRadiusPixels(float RoughRefraction, float Thickness, float SceneDepth)
{
    float roughness = saturate(RoughRefraction);
    if (roughness <= 1.0e-4f || Thickness <= 1.0e-4f)
    {
        return 0.0f;
    }

    float standardDeviationCM = BurtGetForwardRefractionSqrtVarianceFromRoughness(roughness) * Thickness * 100.0f * 350.0f;
    float tanHalfHFOV = rcp(max(abs(UNITY_MATRIX_P._m00), 1.0e-4f));
    float pixelRadiusCM = max(100.0f * max(SceneDepth, 1.0e-4f) * tanHalfHFOV * (2.0f / max(_ScreenParams.x, 1.0f)), 1.0e-4f);
    return clamp(standardDeviationCM / pixelRadiusCM, 0.0f, 32.0f);
}

float3 BurtSampleForwardRoughRefraction(float2 ScreenUV, float RoughRefraction, float Thickness, float SceneDepth)
{
    float radiusPixels = BurtGetForwardRoughRefractionRadiusPixels(RoughRefraction, Thickness, SceneDepth);
    if (radiusPixels <= 1.0e-3f)
    {
        return BurtSampleForwardOpaqueCameraColor(ScreenUV);
    }

    float2 texelSize = rcp(max(_ScreenParams.xy, float2(1.0f, 1.0f)));
    float2 radiusUV = texelSize * radiusPixels;

    float3 color = BurtSampleForwardOpaqueCameraColor(ScreenUV) * 0.25f;
    color += BurtSampleForwardOpaqueCameraColor(saturate(ScreenUV + float2(radiusUV.x, 0.0f))) * 0.125f;
    color += BurtSampleForwardOpaqueCameraColor(saturate(ScreenUV - float2(radiusUV.x, 0.0f))) * 0.125f;
    color += BurtSampleForwardOpaqueCameraColor(saturate(ScreenUV + float2(0.0f, radiusUV.y))) * 0.125f;
    color += BurtSampleForwardOpaqueCameraColor(saturate(ScreenUV - float2(0.0f, radiusUV.y))) * 0.125f;
    color += BurtSampleForwardOpaqueCameraColor(saturate(ScreenUV + radiusUV)) * 0.0625f;
    color += BurtSampleForwardOpaqueCameraColor(saturate(ScreenUV - radiusUV)) * 0.0625f;
    color += BurtSampleForwardOpaqueCameraColor(saturate(ScreenUV + float2(radiusUV.x, -radiusUV.y))) * 0.0625f;
    color += BurtSampleForwardOpaqueCameraColor(saturate(ScreenUV + float2(-radiusUV.x, radiusUV.y))) * 0.0625f;
    return color;
}

float2 BurtComputeForwardRefractionOffset(float3 NormalWS)
{
    float3 normalVS = normalize(mul((float3x3)UNITY_MATRIX_V, NormalWS));
    float aspect = max(_ScreenParams.x, 1.0f) / max(_ScreenParams.y, 1.0f);
    float2 fovFix = float2(UNITY_MATRIX_P._m00, aspect * UNITY_MATRIX_P._m00);
    return normalVS.xy * (_IOR - 1.0f) * fovFix * 0.00023f * max(_ScreenParams.x, 1.0f) * saturate(_Refraction);
}

void BurtApplyForwardRefraction(Varyings Input, float3 NormalWS, BurtSurfaceData SurfaceData, inout float3 FinalColor, inout float OutputAlpha)
{
    if (_Surface < 0.5f || _Refraction <= 1.0e-4f || _BurtOpaqueCameraColorAvailable < 0.5f || SurfaceData.Alpha <= 1.0e-4f)
    {
        return;
    }

    float2 screenUV = BurtGetForwardScreenUV(Input);
    float2 uvBorder = max(_ScreenParams.zw - 1.0f, float2(0.0f, 0.0f));
    float2 clampedUV = clamp(screenUV, uvBorder, 1.0f - uvBorder);
    float2 refractionOffset = BurtComputeForwardRefractionOffset(NormalWS);
    float2 depthProbeUV = clamp(clampedUV + refractionOffset, uvBorder, 1.0f - uvBorder);

    float surfaceDepth = BurtGetForwardLinearEyeDepth(Input.PositionWS);
    float sceneRawDepth = SAMPLE_DEPTH_TEXTURE(_BurtCameraDepthTexture, depthProbeUV);
    float sceneDepth = LinearEyeDepth(sceneRawDepth);
    float thickness = max(sceneDepth - surfaceDepth, 0.0f);
    float depthFade = saturate(thickness * 100.0f);
    if (depthFade <= 1.0e-4f)
    {
        return;
    }

    float2 refractionUV = clamp(clampedUV + refractionOffset * depthFade, uvBorder, 1.0f - uvBorder);
    float refractionSceneDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_BurtCameraDepthTexture, refractionUV));
    float refractionThickness = max(refractionSceneDepth - surfaceDepth, 0.0f);
    float roughRefraction = saturate((1.0f - SurfaceData.Smoothness) - saturate(_RefractionStage));
    float3 refractionColor = BurtSampleForwardRoughRefraction(refractionUV, roughRefraction, refractionThickness, refractionSceneDepth);
    float3 refractionComposite = lerp(refractionColor, FinalColor, saturate(SurfaceData.Alpha));
    float refractionBlend = saturate(_Refraction) * depthFade;
    FinalColor = lerp(FinalColor, refractionComposite, refractionBlend);
    OutputAlpha = lerp(OutputAlpha, 1.0f, refractionBlend);
}
#else
void BurtApplyForwardRefraction(Varyings Input, float3 NormalWS, BurtSurfaceData SurfaceData, inout float3 FinalColor, inout float OutputAlpha)
{
}
#endif

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

#if defined(BURT_ENABLE_SHADING_DEBUG) && BURT_ENABLE_SHADING_DEBUG
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtForwardShadingDebug.hlsl"
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
    #if BURT_FORWARD_ENABLE_PRESKIN_POSITION && defined(BURT_SKINNED_DECAL)
        BurtApplySkinnedDecals(BaseColor, MaskMap, NormalWS, Input.NormalWS, Input.TangentWS, Input.PreSkinPositionOS);
        ShadingDirectionWS = BurtGetForwardShadingDirectionWS(Input, NormalWS, Facing);
    #endif
    #if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
        BurtSurfaceData SurfaceData = BurtCreateMaterialShadingModelSurfaceData(BaseColor, MaskMap, Input.BaseMapUV, NormalWS, ViewDirectionWS, Input.PositionWS, Input.PositionOS, Input.VertexColor);
    #elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_TRUNK)
        BurtSurfaceData SurfaceData = BurtCreateMaterialShadingModelSurfaceData(BaseColor, MaskMap, Input.BaseMapUV, NormalWS, ViewDirectionWS, Input.PositionWS, Input.PositionOS, Input.VertexColor);
    #else
        BurtSurfaceData SurfaceData = BurtCreateMaterialShadingModelSurfaceData(BaseColor, MaskMap, Input.BaseMapUV, NormalWS, ViewDirectionWS, Input.PositionWS);
    #endif
#endif

#if defined(BURT_ENABLE_SHADING_DEBUG) && BURT_ENABLE_SHADING_DEBUG
    if (BurtIsShadingDebugEnabled())
    {
        float3 EarlyDebugColor;
        if (BurtTryEvaluateForwardSurfaceShadingDebug(SurfaceData, NormalWS, Input, EarlyDebugColor) ||
            BurtTryEvaluateForwardGBufferShadingDebug(SurfaceData, Input, NormalWS, ShadingDirectionWS, Facing, EarlyDebugColor) ||
            BurtTryEvaluateForwardShadowShadingDebug(Input.PositionWS, NormalWS, EarlyDebugColor))
        {
            return float4(EarlyDebugColor, SurfaceData.Alpha);
        }
    }
#endif

    float ShadowAttenuation = BurtSampleMainLightShadow(Input.PositionWS, NormalWS, _BurtPerObjectShadowObjectIndex);
    ShadowAttenuation *= BurtResolveForwardMaterialFoliageMicroShadow(SurfaceData);
    float TransmissionThickness = BurtResolvePerObjectShadowTransmissionThickness(Input.PositionWS, -1.0f);
    float TransmissionShadowAttenuation = BurtSampleMainLightTransmissionShadow(Input.PositionWS, NormalWS, _BurtPerObjectShadowObjectIndex, TransmissionThickness);
    BurtLight MainLight = BurtCreateMainLight(ShadowAttenuation, TransmissionShadowAttenuation, TransmissionThickness);
    BurtSurfaceData ShadingSurfaceData = SurfaceData;

#if defined(BURT_ENABLE_SHADING_DEBUG) && BURT_ENABLE_SHADING_DEBUG
    if (BurtIsShadingDebugEnabled() && BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DETAIL_LIGHTING))
    {
        ShadingSurfaceData.BaseColor.rgb = float3(0.18f, 0.18f, 0.18f);
    }
#endif

    BurtPBRShadingComponents PBRComponents = (BurtPBRShadingComponents)0;
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_EYE)
    float3 EmissionColor = EyeData.EmissionColor;
#else
    float3 EmissionColor = BurtEvaluateEmission(Input.EmissionMapUV, _EmissionColor.rgb);
#endif
    float3 FinalColor = EmissionColor;
    float OutputAlpha = SurfaceData.Alpha;

    PBRComponents = BurtEvaluateForwardShadingComponents(ShadingSurfaceData, MainLight, Input, NormalWS, ShadingDirectionWS, ViewDirectionWS, Facing);
    FinalColor = PBRComponents.Lighting + EmissionColor;
    BurtApplyForwardRefraction(Input, NormalWS, SurfaceData, FinalColor, OutputAlpha);

#if defined(BURT_MATERIAL_SUPPORTS_TRANSPARENT_FOG) && !defined(BURT_IGNORE_FOG)
    if (_Surface > 0.5f)
    {
        float2 transparentFogScreenUV = BurtGetForwardScreenUV(Input);
#if defined(BURT_TRANSPARENT_VERTEX_FOG)
        float4 transparentFog = Input.TransparentFog;
#else
        float4 transparentFog = BurtEvaluateTransparentFog(
            transparentFogScreenUV,
            Input.PositionWS);
#endif
        if (_BlendMode > 1.5f)
        {
            FinalColor = BurtBlendPremultipliedTransparentFog(
                FinalColor * OutputAlpha,
                OutputAlpha,
                transparentFog);
        }
        else if (_BlendMode > 0.5f)
        {
            FinalColor = BurtBlendAdditiveTransparentFog(
                FinalColor,
                transparentFog);
        }
        else
        {
            FinalColor = BurtBlendTransparentFog(
                FinalColor,
                transparentFog);
        }
    }
#endif

#if defined(BURT_ENABLE_SHADING_DEBUG) && BURT_ENABLE_SHADING_DEBUG
    if (!BurtIsShadingDebugEnabled())
    {
        return float4(BurtApplyPreExposure(FinalColor), OutputAlpha);
    }

    float3 DebugColor;
    if (BurtTryEvaluateForwardShadedShadingDebug(
        SurfaceData,
        ShadingSurfaceData,
        PBRComponents,
        Input,
        NormalWS,
        ShadingDirectionWS,
        ViewDirectionWS,
        Facing,
        EmissionColor,
        FinalColor,
        DebugColor))
    {
        return float4(DebugColor, SurfaceData.Alpha);
    }
#endif

    return float4(BurtApplyPreExposure(FinalColor), OutputAlpha);
}

#endif // BURT_FORWARD_PASS_INCLUDED
