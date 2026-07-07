// AvatarEye material evaluation shared by BurtRP/Eye GBuffer and Forward passes.
#ifndef BURT_EYE_PASS_INCLUDED
#define BURT_EYE_PASS_INCLUDED

Texture2D _IrisColorMap;
Texture2D _ScleraMap;
Texture2D _EyeDirectionMap;
Texture2D _MidPlaneHeightMap;
Texture2D _EmissiveMap;
Texture2D _Matcap;

struct BurtEyeMaterialData
{
    float4 BaseColor;
    float Smoothness;
    float Reflectance;
    float IrisMask;
    float3 NormalWS;
    float3 IrisNormalWS;
    float3 CausticNormalWS;
    float3 EmissionColor;
};

float2 BurtEyeScaleUVsByCenter(float2 uv, float scale)
{
    return (uv - float2(0.5f, 0.5f)) / max(abs(scale), BURT_EPSILON) + float2(0.5f, 0.5f);
}

float2 BurtEyeScaleUVFromCircle(float2 uv, float scale)
{
    return (uv - float2(0.5f, 0.5f)) / max(abs(scale), BURT_EPSILON) + float2(0.5f, 0.5f);
}

float2 BurtEyeRotateUV(float2 uv, float2 center, float angle)
{
    float sine;
    float cosine;
    sincos(angle, sine, cosine);
    float2 delta = uv - center;
    return float2(delta.x * cosine - delta.y * sine, delta.x * sine + delta.y * cosine) + center;
}

float3 BurtEyeRefractDirection(float internalIOR, float3 normalWS, float3 incidentVector)
{
    float airIOR = 1.00029f;
    float n = airIOR / max(internalIOR, BURT_EPSILON);
    float facing = dot(normalWS, incidentVector);
    float w = n * facing;
    float k = sqrt(max(1.0f + (w - n) * (w + n), BURT_EPSILON));
    return -BurtSafeNormalize((w - k) * normalWS - n * incidentVector);
}

void BurtEyeRefraction(
    float2 baseUV,
    float3 normalWS,
    float3 viewDirectionWS,
    float ior,
    float irisRadius,
    float irisDepth,
    float3 eyeDirectionWS,
    float3 tangentWS,
    out float2 irisUV,
    out float irisConcavity)
{
    float safeIrisRadius = max(abs(irisRadius), 1e-4f);
    float3 refractedViewDir = BurtEyeRefractDirection(ior, normalWS, viewDirectionWS);
    float cosAlpha = dot(viewDirectionWS, eyeDirectionWS);
    cosAlpha = lerp(0.325f, 1.0f, cosAlpha * cosAlpha);
    refractedViewDir *= irisDepth / max(cosAlpha, BURT_EPSILON);

    float3 tangentDerived = BurtSafeNormalize(tangentWS - dot(tangentWS, eyeDirectionWS) * eyeDirectionWS);
    float3 bitangentDerived = BurtSafeNormalize(cross(eyeDirectionWS, tangentDerived));
    float2 refractOffset = float2(-dot(refractedViewDir, tangentDerived), dot(refractedViewDir, bitangentDerived));
    float2 refractedUV = baseUV + safeIrisRadius * refractOffset;

    irisUV = (refractedUV - float2(0.5f, 0.5f)) / safeIrisRadius * 0.5f + float2(0.5f, 0.5f);
    irisConcavity = length(refractedUV - float2(0.5f, 0.5f)) * safeIrisRadius;
}

float3 BurtEyeSampleNormalWS(float2 uv, float3 geometryNormalWS, float4 tangentWS, float facing, float irisMask)
{
    float4 normalMap = SAMPLE_TEXTURE2D(_NormalMap, sampler_LinearRepeat, uv);
    float3 normalTS = BurtUnpackNormalScale(normalMap, _NormalMapScale);
    normalTS = lerp(normalTS, float3(0.0f, 0.0f, 1.0f), saturate(irisMask));
    normalTS = BurtApplyDoubleSidedNormalMode(normalTS, facing, _DoubleSidedNormalModeConstants);
    return BurtTransformTangentToWorld(normalTS, geometryNormalWS, tangentWS);
}

float3 BurtEyeSampleDirectionWS(float2 uv, float3 geometryNormalWS, float4 tangentWS, float facing)
{
    float4 packedDirection = SAMPLE_TEXTURE2D(_EyeDirectionMap, sampler_LinearRepeat, uv);
    float3 directionTS = BurtUnpackNormalScale(packedDirection, 1.0f);
    directionTS = BurtApplyDoubleSidedNormalMode(directionTS, facing, _DoubleSidedNormalModeConstants);
    return BurtTransformTangentToWorld(directionTS, geometryNormalWS, tangentWS);
}

BurtEyeMaterialData BurtEvaluateEyeMaterialData(float2 baseUV, float3 geometryNormalWS, float4 tangentWS, float3 viewDirectionWS, float facing)
{
    BurtEyeMaterialData data;

    float2 eyeBallUV = BurtEyeScaleUVsByCenter(baseUV, _ScalebyCenter);
    float irisEdgeWidth = max(abs(_IrisMaskBlurIntensity.z), 1e-4f);
    float irisMaskValue = 1.0f - (distance(eyeBallUV, float2(0.5f, 0.5f)) - _IrisRadius + irisEdgeWidth) / irisEdgeWidth;
    float irisMask = smoothstep(_IrisMaskBlurIntensity.x, _IrisMaskBlurIntensity.y, irisMaskValue);

    float2 heightUV = float2(_ScalebyCenter * _IrisRadius + 0.5f, 0.5f);
    float height1 = SAMPLE_TEXTURE2D(_MidPlaneHeightMap, sampler_LinearRepeat, baseUV).r;
    float height2 = SAMPLE_TEXTURE2D(_MidPlaneHeightMap, sampler_LinearRepeat, heightUV).r;
    float irisDepth = max(height1 - height2, 0.0f) * _IrisDepthScale;

    float3 safeGeometryNormalWS = BurtSafeNormalize(geometryNormalWS);
    float3 eyeDirectionWS = BurtEyeSampleDirectionWS(baseUV, safeGeometryNormalWS, tangentWS, facing);

    float2 irisUV;
    float irisConcavity;
    BurtEyeRefraction(
        eyeBallUV,
        safeGeometryNormalWS,
        BurtSafeNormalize(viewDirectionWS),
        _IOR,
        _IrisRadius,
        irisDepth,
        eyeDirectionWS,
        tangentWS.xyz,
        irisUV,
        irisConcavity);

    float rotateAngle = frac((_IrisColorRotate + (_Time.y * 20.0f) * _IrisColorRotateSpeed) / 360.0f) * 2.0f * BURT_PI;
    irisUV = BurtEyeRotateUV(irisUV, float2(0.5f, 0.5f), rotateAngle);

    float2 irisMapUV = BurtEyeScaleUVFromCircle(irisUV, _PupilScale);
    irisMapUV.x = _InverseUV > 0.5f ? 1.0f - irisMapUV.x : irisMapUV.x;

    float limbusMask = saturate(length((irisMapUV - float2(0.5f, 0.5f)) * _LimbusScale));
    limbusMask = saturate(1.0f - pow(limbusMask, max(_LimbusPow, BURT_EPSILON)));

    float4 scleraColor = SAMPLE_TEXTURE2D(_ScleraMap, sampler_LinearRepeat, eyeBallUV) * _ScleraColor;
    float4 irisColor = SAMPLE_TEXTURE2D(_IrisColorMap, sampler_LinearRepeat, irisMapUV) * _IrisColor * limbusMask;

    data.BaseColor = float4(lerp(scleraColor.rgb, irisColor.rgb, irisMask), 1.0f);
    float roughness = lerp(_ScleraRoughness, _CorneaRoughness, irisMask);
    data.Smoothness = saturate(1.0f - roughness);
    data.Reflectance = saturate(lerp(_ScleraSpecular, _CorneaSpecular, irisMask));
    data.IrisMask = saturate(irisMask);
    data.NormalWS = BurtEyeSampleNormalWS(baseUV, safeGeometryNormalWS, tangentWS, facing, data.IrisMask);
    data.IrisNormalWS = safeGeometryNormalWS;

    float causticBlend = pow(abs(irisConcavity * _IrisConcavityScale), max(_IrisConcavityPow, BURT_EPSILON)) * data.IrisMask;
    data.CausticNormalWS = BurtSafeNormalize(lerp(eyeDirectionWS, -data.IrisNormalWS, saturate(causticBlend)));

    float3 emissiveColor = SAMPLE_TEXTURE2D(_EmissiveMap, sampler_LinearRepeat, irisMapUV).rgb * _EyeEmissiveColor.rgb;
    float2 eyeCookieUV = BurtEyeScaleUVFromCircle(irisUV + _MatcapSizeOffset.yz, max(_MatcapSizeOffset.x, BURT_EPSILON));
    emissiveColor += SAMPLE_TEXTURE2D(_Matcap, sampler_LinearClamp, eyeCookieUV).rgb * _MatcapColor.rgb * data.IrisMask;
    data.EmissionColor = max(emissiveColor, float3(0.0f, 0.0f, 0.0f));

    return data;
}

BurtSurfaceData BurtCreateEyeSurfaceData(BurtEyeMaterialData data)
{
    float4 maskMap = float4(0.0f, 1.0f, 0.5f, data.Smoothness);
    BurtSurfaceData surfaceData = BurtCreateSurfaceData(data.BaseColor, data.Reflectance, 1.0f, 0.0f, maskMap, 1.0f);
    return BurtApplyEyeSurfaceSemantics(surfaceData, data.IrisMask, data.IrisNormalWS, data.CausticNormalWS);
}

#endif // BURT_EYE_PASS_INCLUDED
