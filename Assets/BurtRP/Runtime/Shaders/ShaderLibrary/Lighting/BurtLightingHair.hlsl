// Hair-specific deferred/forward lighting helpers. Included after the common PBR core.
#ifndef BURT_LIGHTING_HAIR_INCLUDED
#define BURT_LIGHTING_HAIR_INCLUDED

float BurtHairGaussian(float Width, float Theta)
{
    float SafeWidth = max(Width, 0.02f);
    return exp(-0.5f * Theta * Theta / max(SafeWidth * SafeWidth, BURT_EPSILON)) / max(sqrt(2.0f * BURT_PI) * SafeWidth, BURT_EPSILON);
}

float BurtHairFresnel(float CosTheta)
{
    const float Eta = 1.55f;
    const float F0 = ((1.0f - Eta) * (1.0f - Eta)) / ((1.0f + Eta) * (1.0f + Eta));
    return F0 + (1.0f - F0) * Pow5(1.0f - saturate(CosTheta));
}

float3 BurtHairAbsorptionTint(float3 BaseColor)
{
    // Hair has no dedicated Absorption/deep-opacity GBuffer data yet; sqrt(BaseColor) is a stable first-order tint proxy.
    return sqrt(saturate(BaseColor));
}

float3 BurtHairColorToAbsorption(float3 Color)
{
    float3 SafeColor = clamp(Color, float3(0.0001f, 0.0001f, 0.0001f), float3(1.0f, 1.0f, 1.0f));
    const float B = 0.3f;
    const float B2 = B * B;
    const float B3 = B * B2;
    const float B4 = B2 * B2;
    const float B5 = B * B4;
    const float D = 5.969f - 0.215f * B + 2.532f * B2 - 10.73f * B3 + 5.574f * B4 + 0.245f * B5;
    float3 Absorption = log(SafeColor) / D;
    return Absorption * Absorption;
}

float3 BurtHairSpecularF0(BurtGBufferData GBufferData)
{
    // Reflectance already includes _HairSpecularScale before GBuffer packing; keep F0 dielectric and avoid reading Lit metallic.
    float SpecularScale = saturate(GBufferData.Reflectance) * 2.0f;
    float3 SpecularTint = BurtGetHairSpecularColor(GBufferData);
    float TintScale = PerceivedLuminance(SpecularTint);
    float3 TintColor = SpecularTint / max(TintScale, 0.0001f);
    return float3(0.04f, 0.04f, 0.04f) * SpecularScale * TintScale * TintColor;
}

float BurtHairSpecularScale(BurtGBufferData GBufferData)
{
    // UE multiplies the R lobe by Specular * 2; Burt stores that control in Reflectance after _HairSpecularScale.
    return saturate(GBufferData.Reflectance) * 2.0f;
}

float3 BurtHairSafePow(float3 Value, float Power)
{
    return pow(max(abs(Value), float3(0.001f, 0.001f, 0.001f)), max(Power, 0.0f));
}

float3 BurtLimitHairSpecularEnergy(float3 SpecularBRDF, float Roughness, float SpecularScale, float Scatter)
{
    // Keep the compact UE-style lobes from producing fireflies at very low Roughness without changing normal cases.
    float3 SafeSpecularBRDF = max(SpecularBRDF, float3(0.0f, 0.0f, 0.0f));
    float SpecularLuminance = dot(SafeSpecularBRDF, float3(0.2126f, 0.7152f, 0.0722f));
    float Smoothness = 1.0f - saturate(Roughness);
    float EnergyLimit = lerp(4.0f, 18.0f, Smoothness) * lerp(0.8f, 1.25f, saturate(SpecularScale * 0.5f)) * lerp(0.9f, 1.15f, Scatter);
    return SafeSpecularBRDF * min(1.0f, EnergyLimit / max(SpecularLuminance, BURT_EPSILON));
}

float BurtHairRoughnessToBlinnPhongSpecularExponent(float Roughness)
{
    return clamp(2.0f * rcp(max(Roughness * Roughness, BURT_EPSILON)) - 2.0f, BURT_EPSILON, rcp(BURT_EPSILON));
}

float BurtHairKajiyaKayPeakFromLinearRoughness(float LinearRoughness)
{
    float SpecularExponent = BurtHairRoughnessToBlinnPhongSpecularExponent(max(LinearRoughness, 1.0f / 255.0f));
    return (SpecularExponent + 2.0f) * (0.5f * BURT_INV_PI);
}

float BurtHairMarschnerAutoSpecularGain(float LegacyPeak, float MarschnerPeak, float Response, float MaxGain)
{
    float Gain = LegacyPeak / max(MarschnerPeak, 0.0001f);
    return clamp(pow(max(Gain, 1.0f), Response), 1.0f, MaxGain);
}

float3 BurtHairKajiyaKayDiffuseAttenuation(float3 BaseColor, float Scatter, float3 LightDirectionWS, float3 ViewDirectionWS, float3 StrandDirectionWS, float Shadow)
{
    float KajiyaDiffuse = 1.0f - abs(dot(StrandDirectionWS, LightDirectionWS));
    float3 FakeNormal = BurtSafeNormalize(ViewDirectionWS - StrandDirectionWS * dot(ViewDirectionWS, StrandDirectionWS));
    float WrappedNoL = saturate((dot(FakeNormal, LightDirectionWS) + 1.0f) * 0.25f);
    float DiffuseScatter = BURT_INV_PI * lerp(WrappedNoL, KajiyaDiffuse, 0.33f) * Scatter;
    float Luma = max(PerceivedLuminance(BaseColor), BURT_EPSILON);
    float3 ScatterTint = pow(max(BaseColor / Luma, float3(0.001f, 0.001f, 0.001f)), saturate(1.0f - Shadow));
    return sqrt(saturate(BaseColor)) * DiffuseScatter * ScatterTint;
}

float3 BurtHairCreateFallbackNormalWS(float3 StrandDirectionWS)
{
    float3 FallbackAxis = abs(StrandDirectionWS.y) < 0.95f ? float3(0.0f, 1.0f, 0.0f) : float3(1.0f, 0.0f, 0.0f);
    return BurtSafeNormalize(cross(FallbackAxis, StrandDirectionWS));
}

float3 BurtHairCreateViewFacingNormalWS(float3 StrandDirectionWS, float3 ViewDirectionWS)
{
    // Hair GBuffer only stores Strand direction today; derive a stable view-facing normal for SH and reflection-probe lookups.
    float3 Strand = BurtSafeNormalize(StrandDirectionWS);
    float3 ViewDirection = BurtSafeNormalize(ViewDirectionWS);
    float3 ViewNormal = ViewDirection - Strand * dot(ViewDirection, Strand);
    float NormalLengthSquared = dot(ViewNormal, ViewNormal);
    return NormalLengthSquared > 0.0001f ? ViewNormal * rsqrt(NormalLengthSquared) : BurtHairCreateFallbackNormalWS(Strand);
}

float3 BurtHairReconstructGeometryNormalWS(float3 PositionWS, float3 ViewDirectionWS)
{
    float3 GeometricNormalWS = cross(ddx(PositionWS), ddy(PositionWS));
    float NormalLengthSquared = dot(GeometricNormalWS, GeometricNormalWS);
    if (NormalLengthSquared <= 0.0001f)
    {
        return BurtSafeNormalize(ViewDirectionWS);
    }

    GeometricNormalWS *= rsqrt(NormalLengthSquared);
    return dot(GeometricNormalWS, ViewDirectionWS) < 0.0f ? -GeometricNormalWS : GeometricNormalWS;
}

BurtGBufferData BurtResolveHairDeferredGeometryData(BurtGBufferData GBufferData, float3 ViewDirectionWS, float3 PositionWS)
{
    float3 StrandDirectionWS = BurtSafeNormalize(BurtGetHairStrandDirectionWS(GBufferData));
    float3 HairNormalWS = BurtGetHairShadingNormalWS(GBufferData);
    float3 HairGeometryNormalWS = BurtGetHairGeometryNormalWS(GBufferData);
    float3 ReconstructedGeometryNormalWS = BurtHairReconstructGeometryNormalWS(PositionWS, ViewDirectionWS);

    bool DecodedHairMissingNormals =
        abs(dot(HairNormalWS, StrandDirectionWS)) > 0.98f &&
        abs(dot(HairGeometryNormalWS, StrandDirectionWS)) > 0.98f;

    if (DecodedHairMissingNormals)
    {
        GBufferData.ClearCoatNormalWS = ReconstructedGeometryNormalWS;
        GBufferData.HairGeometryNormalWS = ReconstructedGeometryNormalWS;
    }

    return GBufferData;
}

BurtPBRGeometryData BurtPrepareHairGeometryData(BurtGBufferData GBufferData, float3 ViewDirectionWS)
{
    float3 HairNormalWS = BurtGetHairShadingNormalWS(GBufferData);
    float3 StrandDirectionWS = BurtGetHairStrandDirectionWS(GBufferData);
    return BurtPreparePBRGeometryData(HairNormalWS, float4(StrandDirectionWS, 1.0f), ViewDirectionWS);
}

BurtPBRGeometryData BurtPrepareHairGeometryData(BurtGBufferData GBufferData, float3 ViewDirectionWS, float3 PositionWS)
{
    GBufferData = BurtResolveHairDeferredGeometryData(GBufferData, ViewDirectionWS, PositionWS);
    return BurtPrepareHairGeometryData(GBufferData, ViewDirectionWS);
}

struct BurtHairDirectComponents
{
    float3 Diffuse;
    float3 Specular;
    float3 DiffuseBRDF;
    float3 SpecularBRDF;
    float PrimaryLobe;
    float SecondaryLobe;
    float TransmissionLobe;
    float Scatter;
    float DiffuseLobe;
    float3 Fresnel;
};

BurtHairDirectComponents BurtEvaluateHairDirectComponents(BurtGBufferData GBufferData, BurtPBRGeometryData GeometryData, BurtLight Light)
{
    float3 BaseColor = saturate(GBufferData.BaseColor);
    float3 T = BurtSafeNormalize(BurtGetHairStrandDirectionWS(GBufferData));
    float3 N = GeometryData.NormalWS;
    float3 GeometricN = BurtGetHairGeometryNormalWS(GBufferData);
    float3 V = GeometryData.ViewDirectionWS;
    float3 L = BurtSafeNormalize(Light.DirectionWS);
    float LightFalloff = saturate(Light.ShadowAttenuation + BurtGetHairShadowFillStrength(GBufferData));

    float Roughness = clamp(GBufferData.PerceptualRoughness, 1.0f / 255.0f, 1.0f);
    float SecondaryRoughness = clamp(BurtGetHairSecondaryRoughness(GBufferData), 1.0f / 255.0f, 1.0f);
    float Backlit = min(BurtGetHairBackLight(GBufferData), 1.0f);
    float Scatter = BurtGetHairScatter(GBufferData);
    float SpecularScale = BurtHairSpecularScale(GBufferData);

    float3 NormalSlope = N - GeometricN * dot(N, GeometricN);
    float3 MarschnerT = BurtSafeNormalize(T + NormalSlope * 1.5f);
    float SinThetaL = clamp(dot(MarschnerT, L), -1.0f, 1.0f);
    float SinThetaV = clamp(dot(MarschnerT, V), -1.0f, 1.0f);
    float CosThetaD = max(cos(0.5f * abs(asin(SinThetaV) - asin(SinThetaL))), 0.01f);
    float3 Lp = L - SinThetaL * MarschnerT;
    float3 Vp = V - SinThetaV * MarschnerT;
    float CosPhi = clamp(dot(Lp, Vp) * rsqrt(max(dot(Lp, Lp) * dot(Vp, Vp), 0.0001f)), -1.0f, 1.0f);
    float CosHalfPhi = sqrt(saturate(0.5f + 0.5f * CosPhi));
    float VoL = dot(V, L);
    float NPrime = 1.19f / CosThetaD + 0.36f * CosThetaD;

    float PrimaryB = Roughness * Roughness * 0.75f;
    float TtB = Roughness * Roughness * 0.5f;
    float SecondaryB = max(SecondaryRoughness * SecondaryRoughness, 0.0001f) * 0.85f;
    float AlphaR = -0.07f;
    float AlphaTT = 0.035f;
    float AlphaTRT = 0.14f;

    float SinAlphaR = sin(AlphaR);
    float CosAlphaR = cos(AlphaR);
    float ShiftR = 2.0f * SinAlphaR * (CosAlphaR * CosHalfPhi * sqrt(saturate(1.0f - SinThetaV * SinThetaV)) + SinAlphaR * SinThetaV);
    ShiftR += BurtGetHairSpecularShift(GBufferData);
    float PrimaryBScale = sqrt(2.0f) * CosHalfPhi;
    float PrimaryM = BurtHairGaussian(PrimaryB * PrimaryBScale, SinThetaL + SinThetaV - ShiftR);
    float PrimaryN = 0.25f * CosHalfPhi;
    float PrimaryFScalar = BurtHairFresnel(sqrt(saturate(0.5f + 0.5f * VoL)));

    float NdotL = saturate(dot(N, L));
    float LegacyVisibility = NdotL * saturate(dot(GeometricN, V) * 1000000.0f);
    float LegacyPrimaryPeak = 0.25f * max(PrimaryFScalar, 0.0001f) * BurtHairKajiyaKayPeakFromLinearRoughness(PerceptualRoughnessToLinearRoughness(Roughness)) * LegacyVisibility;
    float MarschnerPrimaryPeak = BurtHairGaussian(max(PrimaryB * sqrt(2.0f), 1.0f / 255.0f), 0.0f) * PrimaryN * max(PrimaryFScalar, 0.0001f);
    float PrimaryAutoGain = BurtHairMarschnerAutoSpecularGain(LegacyPrimaryPeak, MarschnerPrimaryPeak, 1.0f, 2.0f);
    float3 SpecularTint = BurtGetHairSpecularColor(GBufferData);
    float TintScale = PerceivedLuminance(SpecularTint);
    float3 TintColor = SpecularTint / max(TintScale, 0.0001f);
    float3 RBaseColorTint = lerp(float3(1.0f, 1.0f, 1.0f), sqrt(abs(BaseColor)), 0.6f);
    float3 PrimarySpecular = PrimaryM * PrimaryN * PrimaryFScalar * SpecularScale * PrimaryAutoGain * 2.0f * TintScale * TintColor * RBaseColorTint * lerp(1.0f, Backlit, saturate(-VoL));

    float TtM = BurtHairGaussian(TtB, SinThetaL + SinThetaV - AlphaTT);
    float A = rcp(max(NPrime, BURT_EPSILON));
    float H = CosHalfPhi * (1.0f + A * (0.6f - 0.8f * CosPhi));
    float TtF = BurtHairFresnel(CosThetaD * sqrt(saturate(1.0f - H * H)));
    float TtFp = (1.0f - TtF) * (1.0f - TtF);
    float3 TtAbsorption = BurtHairColorToAbsorption(BaseColor);
    float3 TtTint = exp(-TtAbsorption * 2.0f * abs(1.0f - (H * A) * (H * A) / CosThetaD));
    float TtN = exp(-3.65f * CosPhi - 3.98f);
    float3 TransmissionSpecular = TtM * TtN * TtFp * TtTint * Backlit;

    float SecondaryShiftRaw = clamp((BurtGetHairSecondarySpecularShift(GBufferData) - 1.56f) / 3.33f, -2.0f, 2.0f);
    float TrtM = BurtHairGaussian(SecondaryB, SinThetaL + SinThetaV - AlphaTRT - SecondaryShiftRaw);
    float TrtF = BurtHairFresnel(CosThetaD * 0.5f);
    float TrtFp = (1.0f - TrtF) * (1.0f - TrtF) * TrtF;
    float3 TrtTint = BurtHairSafePow(BaseColor, 0.8f / CosThetaD);
    const float TrtAzimuthSharpness = 20.0f;
    const float TrtAzimuthPeakLog = 0.22f;
    float TrtN = exp(TrtAzimuthSharpness * CosPhi - (TrtAzimuthSharpness - TrtAzimuthPeakLog));

    float SecondaryLinearRoughness = PerceptualRoughnessToLinearRoughness(SecondaryRoughness);
    float LegacySecondaryPeak = 0.25f * max(TrtFp, 0.0001f) * BurtHairKajiyaKayPeakFromLinearRoughness(SecondaryLinearRoughness) * LegacyVisibility;
    float TrtReferenceN = exp(TrtAzimuthPeakLog);
    float3 TrtReferenceTint = BurtHairSafePow(BaseColor, 0.8f);
    float MarschnerSecondaryPeak = BurtHairGaussian(max(SecondaryB, 1.0f / 255.0f), 0.0f) * TrtReferenceN * max(TrtFp, 0.0001f) * sqrt(max(PerceivedLuminance(TrtReferenceTint), 0.0001f));
    float SecondaryAutoGain = BurtHairMarschnerAutoSpecularGain(LegacySecondaryPeak, MarschnerSecondaryPeak, 0.75f, 2.1f);
    float3 SecondarySpecular = TrtM * TrtN * TrtFp * TrtTint * SecondaryAutoGain * 0.75f * BurtGetHairSecondarySpecularColor(GBufferData) * SpecularScale;

    float DiffuseLobe = BURT_INV_PI * lerp(saturate((dot(BurtHairCreateViewFacingNormalWS(T, V), L) + 1.0f) * 0.25f), 1.0f - abs(SinThetaL), 0.33f);
    float TransmissionLobe = TtM * TtN * TtFp * Backlit;
    float3 ScatterDiffuseBRDF = max(BurtHairKajiyaKayDiffuseAttenuation(BaseColor, Scatter, L, V, N, LightFalloff), float3(0.0f, 0.0f, 0.0f));

    BurtHairDirectComponents Components;
    Components.DiffuseBRDF = ScatterDiffuseBRDF;
    Components.SpecularBRDF = BurtLimitHairSpecularEnergy(PrimarySpecular + SecondarySpecular, Roughness, SpecularScale, Scatter);
    Components.PrimaryLobe = PrimaryM * PrimaryN * SpecularScale;
    Components.SecondaryLobe = TrtM * TrtN;
    Components.TransmissionLobe = TransmissionLobe;
    Components.Scatter = Scatter;
    Components.DiffuseLobe = DiffuseLobe;
    Components.Fresnel = float3(PrimaryFScalar, PrimaryFScalar, PrimaryFScalar);

    Components.Diffuse = Components.DiffuseBRDF * Light.Color;
    Components.Specular = Components.SpecularBRDF * Light.Color * LightFalloff * NdotL + TransmissionSpecular * Light.Color * LightFalloff;
    return Components;
}

BurtHairDirectComponents BurtCreateZeroHairDirectComponents()
{
    BurtHairDirectComponents Components;
    Components.Diffuse = float3(0.0f, 0.0f, 0.0f);
    Components.Specular = float3(0.0f, 0.0f, 0.0f);
    Components.DiffuseBRDF = float3(0.0f, 0.0f, 0.0f);
    Components.SpecularBRDF = float3(0.0f, 0.0f, 0.0f);
    Components.PrimaryLobe = 0.0f;
    Components.SecondaryLobe = 0.0f;
    Components.TransmissionLobe = 0.0f;
    Components.Scatter = 0.0f;
    Components.DiffuseLobe = 0.0f;
    Components.Fresnel = float3(0.0f, 0.0f, 0.0f);
    return Components;
}

BurtHairDirectComponents BurtAddHairDirectComponents(BurtHairDirectComponents BaseComponents, BurtHairDirectComponents AddedComponents)
{
    BaseComponents.Diffuse += AddedComponents.Diffuse;
    BaseComponents.Specular += AddedComponents.Specular;
    BaseComponents.DiffuseBRDF += AddedComponents.DiffuseBRDF;
    BaseComponents.SpecularBRDF += AddedComponents.SpecularBRDF;
    BaseComponents.PrimaryLobe += AddedComponents.PrimaryLobe;
    BaseComponents.SecondaryLobe += AddedComponents.SecondaryLobe;
    BaseComponents.TransmissionLobe += AddedComponents.TransmissionLobe;
    BaseComponents.DiffuseLobe += AddedComponents.DiffuseLobe;
    BaseComponents.Scatter = max(BaseComponents.Scatter, AddedComponents.Scatter);
    BaseComponents.Fresnel = max(BaseComponents.Fresnel, AddedComponents.Fresnel);
    return BaseComponents;
}

BurtHairDirectComponents BurtEvaluateHairAdditionalDirectLightingComponents(BurtGBufferData GBufferData, BurtPBRGeometryData GeometryData, float3 PositionWS)
{
    BurtHairDirectComponents AdditionalDirectComponents = BurtCreateZeroHairDirectComponents();
    int AdditionalLightCount = BurtGetAdditionalLightCount();
    if (AdditionalLightCount <= 0)
    {
        return AdditionalDirectComponents;
    }

    [loop]
    for (int LightIndex = 0; LightIndex < BURT_MAX_ADDITIONAL_LIGHTS; LightIndex++)
    {
        if (LightIndex >= AdditionalLightCount)
        {
            break;
        }

        BurtLight AdditionalLight = BurtCreateAdditionalLight(LightIndex, PositionWS, GeometryData.NormalWS);
        AdditionalDirectComponents = BurtAddHairDirectComponents(AdditionalDirectComponents, BurtEvaluateHairDirectComponents(GBufferData, GeometryData, AdditionalLight));
    }

    return AdditionalDirectComponents;
}

BurtHairDirectComponents BurtEvaluateHairAdditionalDirectLightingComponents(BurtGBufferData GBufferData, BurtPBRGeometryData GeometryData, float3 PositionWS, float3 ShadowPositionWS)
{
    BurtHairDirectComponents AdditionalDirectComponents = BurtCreateZeroHairDirectComponents();
    int AdditionalLightCount = BurtGetAdditionalLightCount();
    if (AdditionalLightCount <= 0)
    {
        return AdditionalDirectComponents;
    }

    [loop]
    for (int LightIndex = 0; LightIndex < BURT_MAX_ADDITIONAL_LIGHTS; LightIndex++)
    {
        if (LightIndex >= AdditionalLightCount)
        {
            break;
        }

        BurtLight AdditionalLight = BurtCreateAdditionalLight(LightIndex, PositionWS, GeometryData.NormalWS, ShadowPositionWS);
        AdditionalDirectComponents = BurtAddHairDirectComponents(AdditionalDirectComponents, BurtEvaluateHairDirectComponents(GBufferData, GeometryData, AdditionalLight));
    }

    return AdditionalDirectComponents;
}

BurtHairDirectComponents BurtEvaluateHairAdditionalDirectLightingComponents(BurtGBufferData GBufferData, BurtPBRGeometryData GeometryData, float3 PositionWS, float2 ScreenUV)
{
    if (!BurtHasAdditionalLights())
    {
        return BurtCreateZeroHairDirectComponents();
    }

#if defined(BURT_USE_TILED_LIGHTING)
    uint2 Range = uint2(0u, 0u);
    uint UseClusterLightList = 0u;
    if (!BurtTryGetAdditionalLightListRange(ScreenUV, PositionWS, Range, UseClusterLightList))
    {
        return BurtEvaluateHairAdditionalDirectLightingComponents(GBufferData, GeometryData, PositionWS);
    }

    BurtHairDirectComponents AdditionalDirectComponents = BurtCreateZeroHairDirectComponents();
    int AdditionalLightCount = BurtGetAdditionalLightCount();

    [loop]
    for (uint LocalLightIndex = 0u; LocalLightIndex < BURT_MAX_ADDITIONAL_LIGHTS; LocalLightIndex++)
    {
        if (LocalLightIndex >= Range.y)
        {
            break;
        }

        uint StoredLightIndex = BurtReadAdditionalLightListIndex(Range.x + LocalLightIndex, UseClusterLightList);
        if (StoredLightIndex >= (uint)AdditionalLightCount)
        {
            continue;
        }

        BurtLight AdditionalLight = BurtCreateAdditionalLight((int)StoredLightIndex, PositionWS, GeometryData.NormalWS);
        AdditionalDirectComponents = BurtAddHairDirectComponents(AdditionalDirectComponents, BurtEvaluateHairDirectComponents(GBufferData, GeometryData, AdditionalLight));
    }

    return AdditionalDirectComponents;
#else
    return BurtEvaluateHairAdditionalDirectLightingComponents(GBufferData, GeometryData, PositionWS);
#endif
}

BurtHairDirectComponents BurtEvaluateHairAdditionalDirectLightingComponents(BurtGBufferData GBufferData, BurtPBRGeometryData GeometryData, float3 PositionWS, float3 ShadowPositionWS, float2 ScreenUV)
{
    if (!BurtHasAdditionalLights())
    {
        return BurtCreateZeroHairDirectComponents();
    }

#if defined(BURT_USE_TILED_LIGHTING)
    uint2 Range = uint2(0u, 0u);
    uint UseClusterLightList = 0u;
    if (!BurtTryGetAdditionalLightListRange(ScreenUV, PositionWS, Range, UseClusterLightList))
    {
        return BurtEvaluateHairAdditionalDirectLightingComponents(GBufferData, GeometryData, PositionWS, ShadowPositionWS);
    }

    BurtHairDirectComponents AdditionalDirectComponents = BurtCreateZeroHairDirectComponents();
    int AdditionalLightCount = BurtGetAdditionalLightCount();

    [loop]
    for (uint LocalLightIndex = 0u; LocalLightIndex < BURT_MAX_ADDITIONAL_LIGHTS; LocalLightIndex++)
    {
        if (LocalLightIndex >= Range.y)
        {
            break;
        }

        uint StoredLightIndex = BurtReadAdditionalLightListIndex(Range.x + LocalLightIndex, UseClusterLightList);
        if (StoredLightIndex >= (uint)AdditionalLightCount)
        {
            continue;
        }

        BurtLight AdditionalLight = BurtCreateAdditionalLight((int)StoredLightIndex, PositionWS, GeometryData.NormalWS, ShadowPositionWS);
        AdditionalDirectComponents = BurtAddHairDirectComponents(AdditionalDirectComponents, BurtEvaluateHairDirectComponents(GBufferData, GeometryData, AdditionalLight));
    }

    return AdditionalDirectComponents;
#else
    return BurtEvaluateHairAdditionalDirectLightingComponents(GBufferData, GeometryData, PositionWS, ShadowPositionWS);
#endif
}

BurtHairDirectComponents BurtEvaluateHairAdditionalDirectLightingUnshadowedComponents(BurtGBufferData GBufferData, BurtPBRGeometryData GeometryData, float3 PositionWS)
{
    BurtHairDirectComponents AdditionalDirectComponents = BurtCreateZeroHairDirectComponents();
    int AdditionalLightCount = BurtGetAdditionalLightCount();
    if (AdditionalLightCount <= 0)
    {
        return AdditionalDirectComponents;
    }

    [loop]
    for (int LightIndex = 0; LightIndex < BURT_MAX_ADDITIONAL_LIGHTS; LightIndex++)
    {
        if (LightIndex >= AdditionalLightCount)
        {
            break;
        }

        BurtLight AdditionalLight = BurtCreateAdditionalLightUnshadowed(LightIndex, PositionWS);
        AdditionalDirectComponents = BurtAddHairDirectComponents(AdditionalDirectComponents, BurtEvaluateHairDirectComponents(GBufferData, GeometryData, AdditionalLight));
    }

    return AdditionalDirectComponents;
}

BurtHairDirectComponents BurtEvaluateHairAdditionalDirectLightingUnshadowedComponents(BurtGBufferData GBufferData, BurtPBRGeometryData GeometryData, float3 PositionWS, float2 ScreenUV)
{
    if (!BurtHasAdditionalLights())
    {
        return BurtCreateZeroHairDirectComponents();
    }

#if defined(BURT_USE_TILED_LIGHTING)
    uint2 Range = uint2(0u, 0u);
    uint UseClusterLightList = 0u;
    if (!BurtTryGetAdditionalLightListRange(ScreenUV, PositionWS, Range, UseClusterLightList))
    {
        return BurtEvaluateHairAdditionalDirectLightingUnshadowedComponents(GBufferData, GeometryData, PositionWS);
    }

    BurtHairDirectComponents AdditionalDirectComponents = BurtCreateZeroHairDirectComponents();
    int AdditionalLightCount = BurtGetAdditionalLightCount();

    [loop]
    for (uint LocalLightIndex = 0u; LocalLightIndex < BURT_MAX_ADDITIONAL_LIGHTS; LocalLightIndex++)
    {
        if (LocalLightIndex >= Range.y)
        {
            break;
        }

        uint StoredLightIndex = BurtReadAdditionalLightListIndex(Range.x + LocalLightIndex, UseClusterLightList);
        if (StoredLightIndex >= (uint)AdditionalLightCount)
        {
            continue;
        }

        BurtLight AdditionalLight = BurtCreateAdditionalLightUnshadowed((int)StoredLightIndex, PositionWS);
        AdditionalDirectComponents = BurtAddHairDirectComponents(AdditionalDirectComponents, BurtEvaluateHairDirectComponents(GBufferData, GeometryData, AdditionalLight));
    }

    return AdditionalDirectComponents;
#else
    return BurtEvaluateHairAdditionalDirectLightingUnshadowedComponents(GBufferData, GeometryData, PositionWS);
#endif
}

BurtHairDirectComponents BurtEvaluateHairDirectLightingComponents(BurtGBufferData GBufferData, BurtPBRGeometryData GeometryData, BurtLight MainLight, float3 PositionWS)
{
    BurtHairDirectComponents DirectComponents = BurtEvaluateHairDirectComponents(GBufferData, GeometryData, MainLight);
    return BurtAddHairDirectComponents(DirectComponents, BurtEvaluateHairAdditionalDirectLightingComponents(GBufferData, GeometryData, PositionWS));
}

BurtHairDirectComponents BurtEvaluateHairDirectLightingComponents(BurtGBufferData GBufferData, BurtPBRGeometryData GeometryData, BurtLight MainLight, float3 PositionWS, float2 ScreenUV)
{
    BurtHairDirectComponents DirectComponents = BurtEvaluateHairDirectComponents(GBufferData, GeometryData, MainLight);
    return BurtAddHairDirectComponents(DirectComponents, BurtEvaluateHairAdditionalDirectLightingComponents(GBufferData, GeometryData, PositionWS, ScreenUV));
}

float3 BurtEvaluateHairIndirectDiffuse(BurtGBufferData GBufferData, float3 HairNormalWS)
{
    // First-step Hair has no deep opacity/dual scattering data; use view-facing strand normal as A soft colored fill direction.
    float Scatter = BurtGetHairScatter(GBufferData);
    float3 FrontIrradiance = BurtSampleIndirectDiffuseIrradiance(HairNormalWS);
    float3 BackIrradiance = BurtSampleIndirectDiffuseIrradiance(-HairNormalWS);
    float3 Irradiance = lerp(FrontIrradiance, 0.5f * (FrontIrradiance + BackIrradiance), Scatter * 0.65f);
    float3 AbsorptionTint = BurtHairAbsorptionTint(GBufferData.BaseColor);
    return AbsorptionTint * Irradiance * saturate(GBufferData.Occlusion) * lerp(0.35f, 1.0f, Scatter);
}

float3 BurtEvaluateHairIndirectSpecular(BurtGBufferData GBufferData, BurtPBRGeometryData GeometryData, out float3 EnvBRDF)
{
    // Keep A small environment rim so Hair is not lit by the DefaultLit GGX path but still responds to reflection probes.
    float3 Radiance = SampleIndirectSpecularRadiance(GeometryData.ReflectionDirectionWS, GBufferData.PerceptualRoughness);
    float3 F0 = BurtHairSpecularF0(GBufferData);
    EnvBRDF = F_Schlick(F0, GeometryData.NDotV);
    float Scatter = BurtGetHairScatter(GBufferData);
    float Grazing = Pow5(1.0f - saturate(GeometryData.NDotV));
    float SpecularOcclusion = GetIndirectSpecularOcclusion(GeometryData.NDotV, GBufferData.Occlusion, GBufferData.PerceptualRoughness);
    float EnvironmentScale = lerp(0.18f, 0.45f, Scatter) * lerp(1.0f, 1.35f, Grazing);
    return Radiance * EnvBRDF * SpecularOcclusion * EnvironmentScale;
}

BurtPBRShadingComponents BurtEvaluateHairShadingComponentsFromGBuffer(BurtGBufferData GBufferData, BurtLight MainLight, float3 ViewDirectionWS)
{
    // Initialize with the existing PBR component layout so all debug fields stay valid, then overwrite the lighting lobes with Hair results.
    BurtPBRMaterialData MaterialData = BurtPreparePBRMaterialData(GBufferData);
    BurtPBRGeometryData GeometryData = BurtPrepareHairGeometryData(GBufferData, ViewDirectionWS);
    float3 HairNormalWS = GeometryData.NormalWS;
    BurtPBRShadingComponents Components = BurtEvaluatePBRShadingComponents(MaterialData, GeometryData, MainLight);

    BurtHairDirectComponents HairDirect = BurtEvaluateHairDirectComponents(GBufferData, GeometryData, MainLight);
    float3 HairEnvBRDF;
    float3 HairIndirectDiffuse = BurtEvaluateHairIndirectDiffuse(GBufferData, HairNormalWS);
    float3 HairIndirectSpecular = BurtEvaluateHairIndirectSpecular(GBufferData, GeometryData, HairEnvBRDF);

    Components.DiffuseColor = BurtHairAbsorptionTint(GBufferData.BaseColor);
    Components.F0 = BurtHairSpecularF0(GBufferData);
    Components.F90 = float3(1.0f, 1.0f, 1.0f);
    Components.DirectDiffuse = HairDirect.Diffuse;
    Components.DirectSpecular = HairDirect.Specular;
    Components.DirectLighting = Components.DirectDiffuse + Components.DirectSpecular;
    Components.IndirectDiffuse = HairIndirectDiffuse;
    Components.IndirectSpecular = HairIndirectSpecular;
    Components.IndirectLighting = Components.IndirectDiffuse + Components.IndirectSpecular;
    Components.Lighting = Components.DirectLighting + Components.IndirectLighting;

    Components.PerceptualRoughness = GBufferData.PerceptualRoughness;
    Components.SpecularAARoughness = GBufferData.PerceptualRoughness;
    Components.SpecularAANormalVariance = 0.0f;
    Components.SpecularAARoughnessDelta = 0.0f;
    Components.SpecularEnergyCompensation = float3(1.0f, 1.0f, 1.0f);
    Components.IndirectSpecularEnergyCompensation = float3(1.0f, 1.0f, 1.0f);
    Components.EnergyPreservation = 1.0f;
    Components.SpecularOcclusion = saturate(GBufferData.Occlusion);
    Components.DirectBRDFD = HairDirect.PrimaryLobe;
    Components.DirectBRDFVisibility = 1.0f;
    Components.DirectBRDFFresnel = HairDirect.Fresnel;
    Components.DirectDiffuseLobe = HairDirect.DiffuseLobe;
    Components.DirectDiffuseBRDF = HairDirect.DiffuseBRDF;
    Components.DirectSpecularBRDF = HairDirect.SpecularBRDF;
    Components.IndirectSpecularDFG = float2(0.0f, 0.0f);
    Components.IndirectSpecularEnvBRDF = HairEnvBRDF;
    Components.HairPrimaryLobe = HairDirect.PrimaryLobe;
    Components.HairSecondaryLobe = HairDirect.SecondaryLobe;
    Components.HairTransmissionLobe = HairDirect.TransmissionLobe;
    Components.HairScatter = HairDirect.Scatter;
    Components.ClearCoatMask = 0.0f;
    Components.DirectTransmission = float3(0.0f, 0.0f, 0.0f);
    Components.SubsurfaceProfileIndex = 0.0f;
    Components.SubsurfaceTransmission = float3(0.0f, 0.0f, 0.0f);
    Components.SubsurfaceDirectTransmission = float3(0.0f, 0.0f, 0.0f);
    Components.SubsurfaceTransmissionBRDF = float3(0.0f, 0.0f, 0.0f);
    Components.SubsurfaceTransmissionLobe = 0.0f;
    Components.SubsurfaceTransmissionPhase = 0.0f;
    Components.SubsurfaceTransmissionShadow = 1.0f;
    Components.SubsurfaceTransmissionThickness = 0.0f;
    Components.SubsurfaceKernelWeight = float3(0.0f, 0.0f, 0.0f);
    Components.SubsurfaceIndirect = float3(0.0f, 0.0f, 0.0f);
    Components.SubsurfaceIndirectTransmission = float3(0.0f, 0.0f, 0.0f);
    Components.FoliageMask = 0.0f;
    Components.FoliageTransmission = float3(0.0f, 0.0f, 0.0f);
    Components.FoliageDirectTransmission = float3(0.0f, 0.0f, 0.0f);
    Components.FoliageTransmissionBRDF = float3(0.0f, 0.0f, 0.0f);
    Components.FoliageTransmissionShadow = 1.0f;
    Components.FoliageSpecularBRDF = float3(0.0f, 0.0f, 0.0f);
    return Components;
}

BurtPBRShadingComponents BurtEvaluateHairShadingComponentsFromGBuffer(BurtGBufferData GBufferData, BurtLight MainLight, float3 ViewDirectionWS, float3 PositionWS)
{
    GBufferData = BurtResolveHairDeferredGeometryData(GBufferData, ViewDirectionWS, PositionWS);
    BurtPBRMaterialData MaterialData = BurtPreparePBRMaterialData(GBufferData);
    BurtPBRGeometryData GeometryData = BurtPrepareHairGeometryData(GBufferData, ViewDirectionWS);
    float3 HairNormalWS = GeometryData.NormalWS;
    BurtPBRShadingComponents Components = BurtEvaluatePBRShadingComponents(MaterialData, GeometryData, MainLight);

    BurtHairDirectComponents HairAdditionalDirect = BurtEvaluateHairAdditionalDirectLightingComponents(GBufferData, GeometryData, PositionWS);
    BurtHairDirectComponents HairDirect = BurtAddHairDirectComponents(BurtEvaluateHairDirectComponents(GBufferData, GeometryData, MainLight), HairAdditionalDirect);
    float3 HairEnvBRDF;
    float3 HairIndirectDiffuse = BurtEvaluateHairIndirectDiffuse(GBufferData, HairNormalWS);
    float3 HairIndirectSpecular = BurtEvaluateHairIndirectSpecular(GBufferData, GeometryData, HairEnvBRDF);

    Components.DiffuseColor = BurtHairAbsorptionTint(GBufferData.BaseColor);
    Components.F0 = BurtHairSpecularF0(GBufferData);
    Components.F90 = float3(1.0f, 1.0f, 1.0f);
    Components.DirectDiffuse = HairDirect.Diffuse;
    Components.DirectSpecular = HairDirect.Specular;
    Components.DirectLighting = Components.DirectDiffuse + Components.DirectSpecular;
    Components.AdditionalDiffuse = HairAdditionalDirect.Diffuse;
    Components.AdditionalSpecular = HairAdditionalDirect.Specular;
    Components.AdditionalLighting = Components.AdditionalDiffuse + Components.AdditionalSpecular;
    Components.IndirectDiffuse = HairIndirectDiffuse;
    Components.IndirectSpecular = HairIndirectSpecular;
    Components.IndirectLighting = Components.IndirectDiffuse + Components.IndirectSpecular;
    Components.Lighting = Components.DirectLighting + Components.IndirectLighting;

    Components.PerceptualRoughness = GBufferData.PerceptualRoughness;
    Components.SpecularAARoughness = GBufferData.PerceptualRoughness;
    Components.SpecularAANormalVariance = 0.0f;
    Components.SpecularAARoughnessDelta = 0.0f;
    Components.SpecularEnergyCompensation = float3(1.0f, 1.0f, 1.0f);
    Components.IndirectSpecularEnergyCompensation = float3(1.0f, 1.0f, 1.0f);
    Components.EnergyPreservation = 1.0f;
    Components.SpecularOcclusion = saturate(GBufferData.Occlusion);
    Components.DirectBRDFD = HairDirect.PrimaryLobe;
    Components.DirectBRDFVisibility = 1.0f;
    Components.DirectBRDFFresnel = HairDirect.Fresnel;
    Components.DirectDiffuseLobe = HairDirect.DiffuseLobe;
    Components.DirectDiffuseBRDF = HairDirect.DiffuseBRDF;
    Components.DirectSpecularBRDF = HairDirect.SpecularBRDF;
    Components.IndirectSpecularDFG = float2(0.0f, 0.0f);
    Components.IndirectSpecularEnvBRDF = HairEnvBRDF;
    Components.HairPrimaryLobe = HairDirect.PrimaryLobe;
    Components.HairSecondaryLobe = HairDirect.SecondaryLobe;
    Components.HairTransmissionLobe = HairDirect.TransmissionLobe;
    Components.HairScatter = HairDirect.Scatter;
    Components.ClearCoatMask = 0.0f;
    Components.DirectTransmission = float3(0.0f, 0.0f, 0.0f);
    Components.SubsurfaceProfileIndex = 0.0f;
    Components.SubsurfaceTransmission = float3(0.0f, 0.0f, 0.0f);
    Components.SubsurfaceDirectTransmission = float3(0.0f, 0.0f, 0.0f);
    Components.SubsurfaceTransmissionBRDF = float3(0.0f, 0.0f, 0.0f);
    Components.SubsurfaceTransmissionLobe = 0.0f;
    Components.SubsurfaceTransmissionPhase = 0.0f;
    Components.SubsurfaceTransmissionShadow = 1.0f;
    Components.SubsurfaceTransmissionThickness = 0.0f;
    Components.SubsurfaceKernelWeight = float3(0.0f, 0.0f, 0.0f);
    Components.SubsurfaceIndirect = float3(0.0f, 0.0f, 0.0f);
    Components.SubsurfaceIndirectTransmission = float3(0.0f, 0.0f, 0.0f);
    Components.FoliageMask = 0.0f;
    Components.FoliageTransmission = float3(0.0f, 0.0f, 0.0f);
    Components.FoliageDirectTransmission = float3(0.0f, 0.0f, 0.0f);
    Components.FoliageTransmissionBRDF = float3(0.0f, 0.0f, 0.0f);
    Components.FoliageTransmissionShadow = 1.0f;
    Components.FoliageSpecularBRDF = float3(0.0f, 0.0f, 0.0f);
    return Components;
}

BurtPBRShadingComponents BurtEvaluateHairShadingComponentsFromGBuffer(BurtGBufferData GBufferData, BurtLight MainLight, float3 ViewDirectionWS, float3 PositionWS, float2 ScreenUV)
{
    GBufferData = BurtResolveHairDeferredGeometryData(GBufferData, ViewDirectionWS, PositionWS);
    BurtPBRMaterialData MaterialData = BurtPreparePBRMaterialData(GBufferData);
    BurtPBRGeometryData GeometryData = BurtPrepareHairGeometryData(GBufferData, ViewDirectionWS);
    float3 HairNormalWS = GeometryData.NormalWS;
    BurtPBRShadingComponents Components = BurtEvaluatePBRShadingComponents(MaterialData, GeometryData, MainLight);

    BurtHairDirectComponents HairAdditionalDirect = BurtEvaluateHairAdditionalDirectLightingComponents(GBufferData, GeometryData, PositionWS, ScreenUV);
    BurtHairDirectComponents HairDirect = BurtAddHairDirectComponents(BurtEvaluateHairDirectComponents(GBufferData, GeometryData, MainLight), HairAdditionalDirect);
    float3 HairEnvBRDF;
    float3 HairIndirectDiffuse = BurtEvaluateHairIndirectDiffuse(GBufferData, HairNormalWS);
    float3 HairIndirectSpecular = BurtEvaluateHairIndirectSpecular(GBufferData, GeometryData, HairEnvBRDF);

    Components.DiffuseColor = BurtHairAbsorptionTint(GBufferData.BaseColor);
    Components.F0 = BurtHairSpecularF0(GBufferData);
    Components.F90 = float3(1.0f, 1.0f, 1.0f);
    Components.DirectDiffuse = HairDirect.Diffuse;
    Components.DirectSpecular = HairDirect.Specular;
    Components.AdditionalDiffuse = HairAdditionalDirect.Diffuse;
    Components.AdditionalSpecular = HairAdditionalDirect.Specular;
    Components.AdditionalLighting = Components.AdditionalDiffuse + Components.AdditionalSpecular;
    Components.DirectLighting = Components.DirectDiffuse + Components.DirectSpecular;
    Components.IndirectDiffuse = HairIndirectDiffuse;
    Components.IndirectSpecular = HairIndirectSpecular;
    Components.IndirectLighting = Components.IndirectDiffuse + Components.IndirectSpecular;
    Components.Lighting = Components.DirectLighting + Components.IndirectLighting;
    Components.PerceptualRoughness = GBufferData.PerceptualRoughness;
    Components.SpecularAARoughness = GBufferData.PerceptualRoughness;
    Components.SpecularAANormalVariance = 0.0f;
    Components.SpecularAARoughnessDelta = 0.0f;
    Components.SpecularEnergyCompensation = float3(1.0f, 1.0f, 1.0f);
    Components.IndirectSpecularEnergyCompensation = float3(1.0f, 1.0f, 1.0f);
    Components.EnergyPreservation = 1.0f;
    Components.SpecularOcclusion = saturate(GBufferData.Occlusion);
    Components.DirectBRDFD = HairDirect.PrimaryLobe;
    Components.DirectBRDFVisibility = 1.0f;
    Components.DirectBRDFFresnel = HairDirect.Fresnel;
    Components.DirectDiffuseLobe = HairDirect.DiffuseLobe;
    Components.DirectDiffuseBRDF = HairDirect.DiffuseBRDF;
    Components.DirectSpecularBRDF = HairDirect.SpecularBRDF;
    Components.IndirectSpecularDFG = float2(0.0f, 0.0f);
    Components.IndirectSpecularEnvBRDF = HairEnvBRDF;
    Components.HairPrimaryLobe = HairDirect.PrimaryLobe;
    Components.HairSecondaryLobe = HairDirect.SecondaryLobe;
    Components.HairTransmissionLobe = HairDirect.TransmissionLobe;
    Components.HairScatter = HairDirect.Scatter;
    Components.ClearCoatMask = 0.0f;
    Components.DirectTransmission = float3(0.0f, 0.0f, 0.0f);
    Components.SubsurfaceProfileIndex = 0.0f;
    Components.SubsurfaceTransmission = float3(0.0f, 0.0f, 0.0f);
    Components.SubsurfaceDirectTransmission = float3(0.0f, 0.0f, 0.0f);
    Components.SubsurfaceTransmissionBRDF = float3(0.0f, 0.0f, 0.0f);
    Components.SubsurfaceTransmissionLobe = 0.0f;
    Components.SubsurfaceTransmissionPhase = 0.0f;
    Components.SubsurfaceTransmissionShadow = 1.0f;
    Components.SubsurfaceTransmissionThickness = 0.0f;
    Components.SubsurfaceKernelWeight = float3(0.0f, 0.0f, 0.0f);
    Components.SubsurfaceIndirect = float3(0.0f, 0.0f, 0.0f);
    Components.SubsurfaceIndirectTransmission = float3(0.0f, 0.0f, 0.0f);
    Components.FoliageMask = 0.0f;
    Components.FoliageTransmission = float3(0.0f, 0.0f, 0.0f);
    Components.FoliageDirectTransmission = float3(0.0f, 0.0f, 0.0f);
    Components.FoliageTransmissionBRDF = float3(0.0f, 0.0f, 0.0f);
    Components.FoliageTransmissionShadow = 1.0f;
    Components.FoliageSpecularBRDF = float3(0.0f, 0.0f, 0.0f);
    return Components;
}

BurtPBRShadingComponents BurtEvaluateHairShadingComponentsFromGBuffer(BurtGBufferData GBufferData, BurtLight MainLight, float3 ViewDirectionWS, float3 PositionWS, float3 ShadowPositionWS, float2 ScreenUV)
{
    GBufferData = BurtResolveHairDeferredGeometryData(GBufferData, ViewDirectionWS, PositionWS);
    BurtPBRMaterialData MaterialData = BurtPreparePBRMaterialData(GBufferData);
    BurtPBRGeometryData GeometryData = BurtPrepareHairGeometryData(GBufferData, ViewDirectionWS);
    float3 HairNormalWS = GeometryData.NormalWS;
    BurtPBRShadingComponents Components = BurtEvaluatePBRShadingComponents(MaterialData, GeometryData, MainLight);

    BurtHairDirectComponents HairAdditionalDirect = BurtEvaluateHairAdditionalDirectLightingComponents(GBufferData, GeometryData, PositionWS, ShadowPositionWS, ScreenUV);
    BurtHairDirectComponents HairDirect = BurtAddHairDirectComponents(BurtEvaluateHairDirectComponents(GBufferData, GeometryData, MainLight), HairAdditionalDirect);
    float3 HairEnvBRDF;
    float3 HairIndirectDiffuse = BurtEvaluateHairIndirectDiffuse(GBufferData, HairNormalWS);
    float3 HairIndirectSpecular = BurtEvaluateHairIndirectSpecular(GBufferData, GeometryData, HairEnvBRDF);

    Components.DiffuseColor = BurtHairAbsorptionTint(GBufferData.BaseColor);
    Components.F0 = BurtHairSpecularF0(GBufferData);
    Components.F90 = float3(1.0f, 1.0f, 1.0f);
    Components.DirectDiffuse = HairDirect.Diffuse;
    Components.DirectSpecular = HairDirect.Specular;
    Components.AdditionalDiffuse = HairAdditionalDirect.Diffuse;
    Components.AdditionalSpecular = HairAdditionalDirect.Specular;
    Components.AdditionalLighting = Components.AdditionalDiffuse + Components.AdditionalSpecular;
    Components.DirectLighting = Components.DirectDiffuse + Components.DirectSpecular;
    Components.IndirectDiffuse = HairIndirectDiffuse;
    Components.IndirectSpecular = HairIndirectSpecular;
    Components.IndirectLighting = Components.IndirectDiffuse + Components.IndirectSpecular;
    Components.Lighting = Components.DirectLighting + Components.IndirectLighting;
    Components.PerceptualRoughness = GBufferData.PerceptualRoughness;
    Components.SpecularAARoughness = GBufferData.PerceptualRoughness;
    Components.SpecularAANormalVariance = 0.0f;
    Components.SpecularAARoughnessDelta = 0.0f;
    Components.SpecularEnergyCompensation = float3(1.0f, 1.0f, 1.0f);
    Components.IndirectSpecularEnergyCompensation = float3(1.0f, 1.0f, 1.0f);
    Components.EnergyPreservation = 1.0f;
    Components.SpecularOcclusion = saturate(GBufferData.Occlusion);
    Components.DirectBRDFD = HairDirect.PrimaryLobe;
    Components.DirectBRDFVisibility = 1.0f;
    Components.DirectBRDFFresnel = HairDirect.Fresnel;
    Components.DirectDiffuseLobe = HairDirect.DiffuseLobe;
    Components.DirectDiffuseBRDF = HairDirect.DiffuseBRDF;
    Components.DirectSpecularBRDF = HairDirect.SpecularBRDF;
    Components.IndirectSpecularDFG = float2(0.0f, 0.0f);
    Components.IndirectSpecularEnvBRDF = HairEnvBRDF;
    Components.HairPrimaryLobe = HairDirect.PrimaryLobe;
    Components.HairSecondaryLobe = HairDirect.SecondaryLobe;
    Components.HairTransmissionLobe = HairDirect.TransmissionLobe;
    Components.HairScatter = HairDirect.Scatter;
    Components.ClearCoatMask = 0.0f;
    Components.DirectTransmission = float3(0.0f, 0.0f, 0.0f);
    Components.SubsurfaceProfileIndex = 0.0f;
    Components.SubsurfaceTransmission = float3(0.0f, 0.0f, 0.0f);
    Components.SubsurfaceDirectTransmission = float3(0.0f, 0.0f, 0.0f);
    Components.SubsurfaceTransmissionBRDF = float3(0.0f, 0.0f, 0.0f);
    Components.SubsurfaceTransmissionLobe = 0.0f;
    Components.SubsurfaceTransmissionPhase = 0.0f;
    Components.SubsurfaceTransmissionShadow = 1.0f;
    Components.SubsurfaceTransmissionThickness = 0.0f;
    Components.SubsurfaceKernelWeight = float3(0.0f, 0.0f, 0.0f);
    Components.SubsurfaceIndirect = float3(0.0f, 0.0f, 0.0f);
    Components.SubsurfaceIndirectTransmission = float3(0.0f, 0.0f, 0.0f);
    Components.FoliageMask = 0.0f;
    Components.FoliageTransmission = float3(0.0f, 0.0f, 0.0f);
    Components.FoliageDirectTransmission = float3(0.0f, 0.0f, 0.0f);
    Components.FoliageTransmissionBRDF = float3(0.0f, 0.0f, 0.0f);
    Components.FoliageTransmissionShadow = 1.0f;
    Components.FoliageSpecularBRDF = float3(0.0f, 0.0f, 0.0f);
    return Components;
}

#endif
