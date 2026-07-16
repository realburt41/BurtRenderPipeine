// Runtime skinned decal projection for BurtRP/Subsurface.
#ifndef BURT_SKINNED_DECAL_INCLUDED
#define BURT_SKINNED_DECAL_INCLUDED

#if defined(BURT_SKINNED_DECAL)

#define BURT_SKINNED_DECAL_MAX_LAYERS (5)

Texture2D _SkinnedDecalPluginModel_DecalAlbedo;
Texture2D _SkinnedDecalPluginModel_DecalNormal;
Texture2D _SkinnedDecalPluginModel_DecalMOHR;

float BurtSkinnedDecalBoundsMask(float2 uv)
{
    return step(0.0f, uv.x) * step(uv.x, 1.0f) * step(0.0f, uv.y) * step(uv.y, 1.0f);
}

void BurtApplySkinnedDecals(
    inout float4 baseColor,
    inout float4 maskMap,
    inout float3 normalWS,
    float3 geometryNormalWS,
    float4 tangentWS,
    float3 preSkinPositionOS)
{
    float4 arrayIndexSize[BURT_SKINNED_DECAL_MAX_LAYERS] =
    {
        _SkinnedDecalPluginModel_DecalArrayIndexSize1,
        _SkinnedDecalPluginModel_DecalArraySizeIndex2,
        _SkinnedDecalPluginModel_DecalArraySizeIndex3,
        _SkinnedDecalPluginModel_DecalArraySizeIndex4,
        _SkinnedDecalPluginModel_DecalArraySizeIndex5,
    };
    float3 position[BURT_SKINNED_DECAL_MAX_LAYERS] =
    {
        _SkinnedDecalPluginModel_DecalPosition1.xyz,
        _SkinnedDecalPluginModel_DecalPosition2.xyz,
        _SkinnedDecalPluginModel_DecalPosition3.xyz,
        _SkinnedDecalPluginModel_DecalPosition4.xyz,
        _SkinnedDecalPluginModel_DecalPosition5.xyz,
    };
    float3 basisX[BURT_SKINNED_DECAL_MAX_LAYERS] =
    {
        _SkinnedDecalPluginModel_DecalBasisX1.xyz,
        _SkinnedDecalPluginModel_DecalBasisX2.xyz,
        _SkinnedDecalPluginModel_DecalBasisX3.xyz,
        _SkinnedDecalPluginModel_DecalBasisX4.xyz,
        _SkinnedDecalPluginModel_DecalBasisX5.xyz,
    };
    float3 basisY[BURT_SKINNED_DECAL_MAX_LAYERS] =
    {
        _SkinnedDecalPluginModel_DecalBasisY1.xyz,
        _SkinnedDecalPluginModel_DecalBasisY2.xyz,
        _SkinnedDecalPluginModel_DecalBasisY3.xyz,
        _SkinnedDecalPluginModel_DecalBasisY4.xyz,
        _SkinnedDecalPluginModel_DecalBasisY5.xyz,
    };
    float4 tint[BURT_SKINNED_DECAL_MAX_LAYERS] =
    {
        _SkinnedDecalPluginModel_DecalTint1,
        _SkinnedDecalPluginModel_DecalTint2,
        _SkinnedDecalPluginModel_DecalTint3,
        _SkinnedDecalPluginModel_DecalTint4,
        _SkinnedDecalPluginModel_DecalTint5,
    };

    if (_BurtSkinnedDecalEntryDebug > 0.5f)
    {
        // Entry marker: validate that this function is reached by the current material/pass variant.
        // It intentionally ignores all decal parameters, texture data, and projection coordinates.
        baseColor.rgb = float3(1.0f, 0.0f, 1.0f);
        return;
    }

    int decalCount = clamp((int)round(_SkinnedDecalPluginModel_DecalCount), 0, BURT_SKINNED_DECAL_MAX_LAYERS);
    [unroll]
    for (int layer = 0; layer < BURT_SKINNED_DECAL_MAX_LAYERS; layer++)
    {
        if (layer >= decalCount)
        {
            break;
        }

        float4 layerParams = arrayIndexSize[layer];
        float3 deltaPosition = preSkinPositionOS - position[layer];
        float2 decalUV = float2(-dot(basisX[layer], deltaPosition), dot(basisY[layer], deltaPosition));
        decalUV = decalUV / max(layerParams.y * 0.1f, 0.0001f) + 0.5f;

        float projectionMask = BurtSkinnedDecalBoundsMask(decalUV);
        if (_BurtSkinnedDecalProjectionDebug > 0.5f)
        {
            // Projection marker: cyan proves the decal layer loop runs; magenta marks an actual rectangle hit.
            baseColor.rgb = lerp(float3(0.0f, 1.0f, 1.0f), float3(1.0f, 0.0f, 1.0f), projectionMask);
            continue;
        }

        float4 decalColor = SAMPLE_TEXTURE2D_LOD(_SkinnedDecalPluginModel_DecalAlbedo, sampler_LinearRepeat, decalUV, 0.0f) * tint[layer];
        float decalMask = projectionMask * saturate(decalColor.a);
        float3 decalBaseColor = lerp(decalColor.rgb, decalColor.rgb * baseColor.rgb, saturate(layerParams.z));
        baseColor.rgb = lerp(baseColor.rgb, decalBaseColor, decalMask);

        float4 packedDecalNormal = SAMPLE_TEXTURE2D_LOD(_SkinnedDecalPluginModel_DecalNormal, sampler_LinearRepeat, decalUV, 0.0f);
        float normalScale = layerParams.w > 0.0f ? layerParams.w : 1.0f;
        float3 decalNormalTS = BurtUnpackNormalScale(packedDecalNormal, normalScale);
        float3 decalNormalWS = BurtTransformTangentToWorld(decalNormalTS, geometryNormalWS, tangentWS);
        normalWS = BurtSafeNormalize(lerp(normalWS, decalNormalWS, decalMask));

        float4 decalMOHR = SAMPLE_TEXTURE2D_LOD(_SkinnedDecalPluginModel_DecalMOHR, sampler_LinearRepeat, decalUV, 0.0f);
        maskMap.r = lerp(maskMap.r, decalMOHR.r, decalMask);
        // XRender's MOHR A channel stores perceptual roughness; BurtRP stores smoothness in MaskMap A.
        maskMap.a = lerp(maskMap.a, 1.0f - decalMOHR.a, decalMask);
    }
}

#endif // BURT_SKINNED_DECAL
#endif // BURT_SKINNED_DECAL_INCLUDED
