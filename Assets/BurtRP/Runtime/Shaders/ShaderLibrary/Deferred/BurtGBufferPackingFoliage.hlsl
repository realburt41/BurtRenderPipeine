#ifndef BURT_GBUFFER_PACKING_FOLIAGE_INCLUDED
#define BURT_GBUFFER_PACKING_FOLIAGE_INCLUDED

#define BURT_FOLIAGE_SPECULAR_PACK_SCALE (0.499f)
float BurtEncodeFoliageSpecularTypeForGBuffer(float SpecularScale, float UseSpecularColor)
{
    float PackedSpecular = saturate(SpecularScale) * BURT_FOLIAGE_SPECULAR_PACK_SCALE;
    return UseSpecularColor > 0.5f ? 0.5f + PackedSpecular : PackedSpecular;
}

void BurtDecodeFoliageSpecularTypeFromGBuffer(float PackedValue, out float SpecularScale, out float UseSpecularColor)
{
    float Packed = saturate(PackedValue);
    UseSpecularColor = Packed >= 0.5f ? 1.0f : 0.0f;
    float LocalSpecular = UseSpecularColor > 0.5f ? Packed - 0.5f : Packed;
    SpecularScale = saturate(LocalSpecular / BURT_FOLIAGE_SPECULAR_PACK_SCALE);
}

#define BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_DIMENSION (32.0f)
#define BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_MAX_BUCKET (BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_DIMENSION - 1.0f)
#define BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_MAX_VALUE (BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_DIMENSION * BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_DIMENSION - 1.0f)
float BurtEncodeFoliageBackLightNdotLForGBuffer(float BackLight, float TransmissionNdotL)
{
    float BackLightBucket = floor(saturate(BackLight) * BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_MAX_BUCKET + 0.5f);
    float NdotLBucket = floor(saturate(TransmissionNdotL) * BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_MAX_BUCKET + 0.5f);
    return (NdotLBucket * BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_DIMENSION + BackLightBucket) / BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_MAX_VALUE;
}

void BurtDecodeFoliageBackLightNdotLFromGBuffer(float PackedValue, out float BackLight, out float TransmissionNdotL)
{
    float PackedBucket = floor(saturate(PackedValue) * BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_MAX_VALUE + 0.5f);
    float NdotLBucket = floor(PackedBucket / BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_DIMENSION);
    float BackLightBucket = PackedBucket - NdotLBucket * BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_DIMENSION;
    BackLight = saturate(BackLightBucket / BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_MAX_BUCKET);
    TransmissionNdotL = saturate(NdotLBucket / BURT_FOLIAGE_BACKLIGHT_NDOTL_PACK_MAX_BUCKET);
}

#endif
