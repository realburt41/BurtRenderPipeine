// XRender EV_InteriorMapping material model support for Burt deferred GBuffer passes.
#ifndef BURT_INTERIOR_MAPPING_PASS_INCLUDED
#define BURT_INTERIOR_MAPPING_PASS_INCLUDED

#include "UnityCG.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInput.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtNormal.hlsl"

Texture2D _EmissiveMap;
Texture2D _AtlasMap;
TextureCube _FakeRoom;
Texture2D _FrostMap;
Texture2D _InteriorFrontDepth;
Texture2D _InteriorBackDepth;
Texture2D _InteriorColor;

float3 BurtInteriorMappingBlackBody(float Temp)
{
    float Temp2 = Temp * Temp;
    float u = (0.860117757f + 1.54118254e-4f * Temp + 1.28641212e-7f * Temp2) /
        (1.0f + 8.42420235e-4f * Temp + 7.08145163e-7f * Temp2);
    float v = (0.317398726f + 4.22806245e-5f * Temp + 4.20481691e-8f * Temp2) /
        (1.0f - 2.89741816e-5f * Temp + 1.61456053e-7f * Temp2);

    float Denominator = max(2.0f * u - 8.0f * v + 4.0f, BURT_EPSILON);
    float x = 3.0f * u / Denominator;
    float y = max(2.0f * v / Denominator, BURT_EPSILON);
    float z = 1.0f - x - y;

    float Y = 1.0f;
    float X = Y / y * x;
    float Z = Y / y * z;

    float3x3 XYZToRGB = float3x3(
        3.2404542f, -1.5371385f, -0.4985314f,
        -0.9692660f, 1.8760108f, 0.0415560f,
        0.0556434f, -0.2040259f, 1.0572252f);

    return mul(XYZToRGB, float3(X, Y, Z)) * pow(0.0004f * Temp, 4.0f);
}

float BurtInteriorMappingSafeSignedDenominator(float Value)
{
    return Value >= 0.0f ? max(Value, BURT_EPSILON) : min(Value, -BURT_EPSILON);
}

float3 BurtInteriorMappingSafeSignedVector(float3 Value)
{
    return float3(
        BurtInteriorMappingSafeSignedDenominator(Value.x),
        BurtInteriorMappingSafeSignedDenominator(Value.y),
        BurtInteriorMappingSafeSignedDenominator(Value.z));
}

float2 BurtInteriorMappingHash2(float2 Seed)
{
    float3 p3 = frac(float3(Seed.xyx) * float3(0.1031f, 0.1030f, 0.0973f));
    p3 += dot(p3, p3.yzx + 33.33f);
    return frac((p3.xx + p3.yz) * p3.zy);
}

float BurtInteriorMappingCalculateDitherSteps(float3 ViewDirWS, float DitherSteps)
{
    float DistanceToWindow = length(ViewDirWS);
    return (1.0f - saturate(DistanceToWindow - 3600.0f)) * DitherSteps;
}

float3 BurtInteriorMappingTransformWorldToTangent(float3 DirectionWS, float3 NormalWS, float4 TangentWS)
{
    float3 SafeNormalWS = BurtSafeNormalize(NormalWS);
    float3 SafeTangentWS = BurtSafeNormalize(TangentWS.xyz);
    SafeTangentWS = BurtSafeNormalize(SafeTangentWS - SafeNormalWS * dot(SafeNormalWS, SafeTangentWS));
    float3 BitangentWS = BurtSafeNormalize(cross(SafeNormalWS, SafeTangentWS) * TangentWS.w);
    return float3(dot(DirectionWS, SafeTangentWS), dot(DirectionWS, BitangentWS), dot(DirectionWS, SafeNormalWS));
}

float2 BurtInteriorMappingConvertUVToAtlasUV(float2 OriginalRawUV, float3 ViewDirTangentSpace, float RoomMaxDepth01)
{
    float DepthScale = rcp(max(RoomMaxDepth01, BURT_EPSILON)) - 1.0f;
    float3 ViewRayStartPosBoxSpace = float3(OriginalRawUV * 2.0f - 1.0f, -1.0f);
    float3 ViewRayDirBoxSpace = ViewDirTangentSpace * float3(1.0f, 1.0f, -DepthScale);
    ViewRayDirBoxSpace = BurtInteriorMappingSafeSignedVector(ViewRayDirBoxSpace);

    float3 ViewRayDirBoxSpaceRcp = rcp(ViewRayDirBoxSpace);
    float3 HitRayLengthForSeparatedAxis = abs(ViewRayDirBoxSpaceRcp) - ViewRayStartPosBoxSpace * ViewRayDirBoxSpaceRcp;
    float ShortestHitRayLength = min(min(HitRayLengthForSeparatedAxis.x, HitRayLengthForSeparatedAxis.y), HitRayLengthForSeparatedAxis.z);
    float3 HitPosBoxSpace = ViewRayStartPosBoxSpace + ShortestHitRayLength * ViewRayDirBoxSpace;

    float Interp = HitPosBoxSpace.z * 0.5f + 0.5f;
    float RealZ = saturate(Interp) / max(DepthScale, BURT_EPSILON) + 1.0f;
    Interp = 1.0f - rcp(max(RealZ, BURT_EPSILON));
    Interp *= DepthScale + 1.0f;

    float2 InteriorUV = HitPosBoxSpace.xy * lerp(1.0f, 1.0f - RoomMaxDepth01, Interp);
    return InteriorUV * 0.5f + 0.5f;
}

void BurtInteriorMappingConvertUVToCubeMapUV(float3 ViewVectorTS, inout float3 Pos)
{
    float DepthScale = rcp(max(1.0f - saturate(_Depth), BURT_EPSILON)) - 1.0f;
    float XAxisScale = rcp(max(1.0f - saturate(_ScaleXAxis), BURT_EPSILON)) - 1.0f;

    ViewVectorTS.z *= DepthScale;
    ViewVectorTS.x *= XAxisScale;
    ViewVectorTS = BurtInteriorMappingSafeSignedVector(ViewVectorTS);

    float3 InvDir = rcp(ViewVectorTS);
    float3 HitLengths = abs(InvDir) - Pos * InvDir;
    float ShortestHit = min(min(HitLengths.x, HitLengths.y), HitLengths.z);
    Pos += ShortestHit * ViewVectorTS;
    Pos *= float3(1.0f, 1.0f, -1.0f);
}

float3 BurtInteriorMappingRayMarchInteriorObjects(float2 UV, float3 ViewDirTS, float DitherSteps, float2 PixelPos)
{
    float Denominator = BurtInteriorMappingSafeSignedDenominator(dot(ViewDirTS, float3(0.0f, 0.0f, 1.0f)));
    float2 UVOut = UV + ViewDirTS.xy * -1.0f / Denominator;
    float Alpha = 0.0f;
    int StepCount = min(max((int)round(_MarchSteps), 0), 400);
    float2 Random = BurtInteriorMappingHash2(PixelPos + _Time.yy);

    [loop]
    for (int i = 0; i < StepCount; ++i)
    {
        float H = ((float)i + lerp(0.0f, Random.x - 0.5f, DitherSteps)) / max((float)StepCount, 1.0f);
        float2 UVCurrent = UV + ViewDirTS.xy * (-1.0f + H) / Denominator;

        float Front = SAMPLE_TEXTURE2D(_InteriorFrontDepth, sampler_LinearClamp, UVCurrent).r;
        float Back = SAMPLE_TEXTURE2D(_InteriorBackDepth, sampler_LinearClamp, UVCurrent).g;
        float InBounds = step(0.0f, UVCurrent.x) * step(UVCurrent.x, 1.0f) *
            step(0.0f, UVCurrent.y) * step(UVCurrent.y, 1.0f);
        float Hit = step(1.0f - Back, H) * step(H, Front) * InBounds;

        UVOut = lerp(UVOut, UVCurrent, Hit);
        Alpha = max(Alpha, Hit);
    }

    return float3(UVOut, Alpha);
}

BurtSurfaceData BurtCreateInteriorMappingSurfaceData(float4 BaseColor, float4 MaskMap)
{
    BurtSurfaceData SurfaceData = BurtCreateSurfaceData(BaseColor, _Reflectance, 1.0f, 1.0f, MaskMap, 1.0f);
    float Roughness = saturate(MaskMap.a * _Roughness);
    SurfaceData.Metallic = 1.0f;
    SurfaceData.Smoothness = saturate(1.0f - Roughness);
    SurfaceData.Occlusion = saturate(lerp(1.0f, MaskMap.g, saturate(_Occlusion)));
    SurfaceData.Reflectance = saturate(_Reflectance);
    SurfaceData.Height = saturate(MaskMap.b);
    SurfaceData.ShadingModelID = BURT_SHADING_MODEL_DEFAULT_LIT;
    return SurfaceData;
}

float3 BurtEvaluateInteriorMappingEmission(
    float2 UV0,
    float2 BaseMapUV,
    float3 PositionWS,
    float3 NormalWS,
    float4 TangentWS,
    float4 PositionCS)
{
    float3 ViewVectorWS = PositionWS - _WorldSpaceCameraPos.xyz;
    float3 ViewVectorTS = BurtSafeNormalize(BurtInteriorMappingTransformWorldToTangent(ViewVectorWS, NormalWS, TangentWS));
    float3 RoomColor = float3(0.0f, 0.0f, 0.0f);

#if defined(BURT_INTERIOR_ATLAS_MODE)
    float2 RoomCount = max(_RoomCount.xy, float2(1.0f, 1.0f));
    float2 AtlasUV = UV0 * RoomCount;
    float2 InteriorUV = BurtInteriorMappingConvertUVToAtlasUV(frac(AtlasUV), ViewVectorTS, 0.5f);
    InteriorUV /= RoomCount;
    InteriorUV = InteriorUV * _AtlasMap_ST.xy + _AtlasMap_ST.zw;
    InteriorUV += floor(AtlasUV) / RoomCount;
    RoomColor = SAMPLE_TEXTURE2D(_AtlasMap, sampler_LinearRepeat, InteriorUV).rgb;
#else
    float ExposureAdjust = exp2(_Exposure);
    float3 ColorTemperature = BurtInteriorMappingBlackBody(1000.0f * _ColorTemp);

    float2 FakeRoomUV = UV0 * _FakeRoom_ST.xy + _FakeRoom_ST.zw;
    float2 CubeMapUV = frac(FakeRoomUV);
    float3 SamplePos = float3(CubeMapUV * 2.0f - 1.0f, 1.0f);
    BurtInteriorMappingConvertUVToCubeMapUV(ViewVectorTS, SamplePos);
    float3 CubeMapColor = BURT_SAMPLE_TEXTURECUBE_LOD_REPEAT(_FakeRoom, SamplePos, 0.0f).rgb * _CubemapLightMultiplier;

    float DitheredSteps = BurtInteriorMappingCalculateDitherSteps(ViewVectorWS, _DitherSteps);
    float3 InteriorDepth = BurtInteriorMappingRayMarchInteriorObjects(CubeMapUV, ViewVectorTS, DitheredSteps, PositionCS.xy);
    float3 InteriorColor = SAMPLE_TEXTURE2D(_InteriorColor, sampler_LinearClamp, InteriorDepth.xy).rgb;

    float3 ManualRoom = lerp(CubeMapColor, InteriorColor * _InteriorIntensity, saturate(InteriorDepth.z));
    RoomColor = ManualRoom * ExposureAdjust * ColorTemperature;
#endif

    return max(RoomColor, float3(0.0f, 0.0f, 0.0f));
}

#endif // BURT_INTERIOR_MAPPING_PASS_INCLUDED
