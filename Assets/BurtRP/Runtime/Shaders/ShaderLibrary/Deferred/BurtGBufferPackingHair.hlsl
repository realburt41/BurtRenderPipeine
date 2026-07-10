#ifndef BURT_GBUFFER_PACKING_HAIR_INCLUDED
#define BURT_GBUFFER_PACKING_HAIR_INCLUDED

#define BURT_HAIR_SCATTER_PACK_DIMENSION (32.0f)
#define BURT_HAIR_SHIFT_PACK_DIMENSION (16.0f)
#define BURT_HAIR_SCATTER_PACK_MAX_BUCKET (BURT_HAIR_SCATTER_PACK_DIMENSION - 1.0f)
#define BURT_HAIR_SHIFT_PACK_MAX_BUCKET (BURT_HAIR_SHIFT_PACK_DIMENSION - 1.0f)
#define BURT_HAIR_MATERIAL_PACK_MAX_VALUE (BURT_HAIR_SCATTER_PACK_DIMENSION * BURT_HAIR_SHIFT_PACK_DIMENSION - 1.0f)
#define BURT_HAIR_CONTROL_PACK_DIMENSION (64.0f)
#define BURT_HAIR_CONTROL_PACK_MAX_BUCKET (BURT_HAIR_CONTROL_PACK_DIMENSION - 1.0f)
#define BURT_HAIR_CONTROL_PACK_MAX_VALUE (BURT_HAIR_CONTROL_PACK_DIMENSION * BURT_HAIR_CONTROL_PACK_DIMENSION - 1.0f)
#define BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION (16.0f)
#define BURT_HAIR_SHIFT_CONTROL_PACK_MAX_BUCKET (BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION - 1.0f)
#define BURT_HAIR_SHIFT_CONTROL_PACK_MAX_VALUE (BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION * BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION * BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION - 1.0f)
#define BURT_HAIR_PRIMARY_SPECULAR_SHIFT_MIN (-2.60f)
#define BURT_HAIR_PRIMARY_SPECULAR_SHIFT_MAX (5.32f)
#define BURT_HAIR_SECONDARY_SPECULAR_SHIFT_MIN (-5.10f)
#define BURT_HAIR_SECONDARY_SPECULAR_SHIFT_MAX (8.22f)
float BurtQuantizeHairMaterialValue(float Value, float MaxBucket)
{
    return floor(saturate(Value) * MaxBucket + 0.5f);
}

float BurtEncodeHairMaterialChannel(float HairScatter, float HairShiftScale)
{
    // Hair only has one Material scalar inside GBuffer2.r; pack scatter and the longitudinal lobe shift scale together.
    float ScatterBucket = BurtQuantizeHairMaterialValue(HairScatter, BURT_HAIR_SCATTER_PACK_MAX_BUCKET);
    float ShiftBucket = BurtQuantizeHairMaterialValue(HairShiftScale, BURT_HAIR_SHIFT_PACK_MAX_BUCKET);
    return (ShiftBucket * BURT_HAIR_SCATTER_PACK_DIMENSION + ScatterBucket) / BURT_HAIR_MATERIAL_PACK_MAX_VALUE;
}

void BurtDecodeHairMaterialChannel(float PackedHairMaterial, out float HairScatter, out float HairShiftScale)
{
    float PackedBucket = floor(saturate(PackedHairMaterial) * BURT_HAIR_MATERIAL_PACK_MAX_VALUE + 0.5f);
    float ShiftBucket = floor(PackedBucket / BURT_HAIR_SCATTER_PACK_DIMENSION);
    float ScatterBucket = PackedBucket - ShiftBucket * BURT_HAIR_SCATTER_PACK_DIMENSION;

    HairScatter = saturate(ScatterBucket / BURT_HAIR_SCATTER_PACK_MAX_BUCKET);
    HairShiftScale = saturate(ShiftBucket / BURT_HAIR_SHIFT_PACK_MAX_BUCKET);
}

float BurtEncodeHairRoughnessFillForGBuffer(float SecondaryRoughness, float ShadowFillStrength)
{
    float RoughnessBucket = BurtQuantizeHairMaterialValue(SecondaryRoughness, BURT_HAIR_CONTROL_PACK_MAX_BUCKET);
    float FillBucket = BurtQuantizeHairMaterialValue(ShadowFillStrength, BURT_HAIR_CONTROL_PACK_MAX_BUCKET);
    return (FillBucket * BURT_HAIR_CONTROL_PACK_DIMENSION + RoughnessBucket) / BURT_HAIR_CONTROL_PACK_MAX_VALUE;
}

void BurtDecodeHairRoughnessFillFromGBuffer(float PackedValue, out float SecondaryRoughness, out float ShadowFillStrength)
{
    float PackedBucket = floor(saturate(PackedValue) * BURT_HAIR_CONTROL_PACK_MAX_VALUE + 0.5f);
    float FillBucket = floor(PackedBucket / BURT_HAIR_CONTROL_PACK_DIMENSION);
    float RoughnessBucket = PackedBucket - FillBucket * BURT_HAIR_CONTROL_PACK_DIMENSION;
    SecondaryRoughness = saturate(RoughnessBucket / BURT_HAIR_CONTROL_PACK_MAX_BUCKET);
    ShadowFillStrength = saturate(FillBucket / BURT_HAIR_CONTROL_PACK_MAX_BUCKET);
}

float BurtEncodeHairShiftBackLightForGBuffer(float SpecularShift, float SecondarySpecularShift, float BackLight)
{
    float PrimaryBucket = floor(saturate((SpecularShift - BURT_HAIR_PRIMARY_SPECULAR_SHIFT_MIN) / max(BURT_HAIR_PRIMARY_SPECULAR_SHIFT_MAX - BURT_HAIR_PRIMARY_SPECULAR_SHIFT_MIN, BURT_EPSILON)) * BURT_HAIR_SHIFT_CONTROL_PACK_MAX_BUCKET + 0.5f);
    float SecondaryBucket = floor(saturate((SecondarySpecularShift - BURT_HAIR_SECONDARY_SPECULAR_SHIFT_MIN) / max(BURT_HAIR_SECONDARY_SPECULAR_SHIFT_MAX - BURT_HAIR_SECONDARY_SPECULAR_SHIFT_MIN, BURT_EPSILON)) * BURT_HAIR_SHIFT_CONTROL_PACK_MAX_BUCKET + 0.5f);
    float BackLightBucket = BurtQuantizeHairMaterialValue(BackLight, BURT_HAIR_SHIFT_CONTROL_PACK_MAX_BUCKET);
    return (BackLightBucket * BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION * BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION + SecondaryBucket * BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION + PrimaryBucket) / BURT_HAIR_SHIFT_CONTROL_PACK_MAX_VALUE;
}

void BurtDecodeHairShiftBackLightFromGBuffer(float PackedValue, out float SpecularShift, out float SecondarySpecularShift, out float BackLight)
{
    float PackedBucket = floor(saturate(PackedValue) * BURT_HAIR_SHIFT_CONTROL_PACK_MAX_VALUE + 0.5f);
    float BackLightBucket = floor(PackedBucket / (BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION * BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION));
    float RemainingBucket = PackedBucket - BackLightBucket * BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION * BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION;
    float SecondaryBucket = floor(RemainingBucket / BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION);
    float PrimaryBucket = RemainingBucket - SecondaryBucket * BURT_HAIR_SHIFT_CONTROL_PACK_DIMENSION;
    SpecularShift = lerp(BURT_HAIR_PRIMARY_SPECULAR_SHIFT_MIN, BURT_HAIR_PRIMARY_SPECULAR_SHIFT_MAX, PrimaryBucket / BURT_HAIR_SHIFT_CONTROL_PACK_MAX_BUCKET);
    SecondarySpecularShift = lerp(BURT_HAIR_SECONDARY_SPECULAR_SHIFT_MIN, BURT_HAIR_SECONDARY_SPECULAR_SHIFT_MAX, SecondaryBucket / BURT_HAIR_SHIFT_CONTROL_PACK_MAX_BUCKET);
    BackLight = saturate(BackLightBucket / BURT_HAIR_SHIFT_CONTROL_PACK_MAX_BUCKET);
}

#endif
