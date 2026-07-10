#ifndef BURT_GBUFFER_PACKING_SUBSURFACE_INCLUDED
#define BURT_GBUFFER_PACKING_SUBSURFACE_INCLUDED

float BurtEncodeSubsurfacePowerForGBuffer(float Power)
{
    return saturate((BurtClampSubsurfacePower(Power) - BURT_SUBSURFACE_POWER_MIN) / max(BURT_SUBSURFACE_POWER_MAX - BURT_SUBSURFACE_POWER_MIN, BURT_EPSILON));
}

float BurtDecodeSubsurfacePowerFromGBuffer(float EncodedPower)
{
    return BurtClampSubsurfacePower(lerp(BURT_SUBSURFACE_POWER_MIN, BURT_SUBSURFACE_POWER_MAX, saturate(EncodedPower)));
}

#define BURT_SUBSURFACE_CONTROL_PACK_DIMENSION (32.0f)
#define BURT_SUBSURFACE_CONTROL_PACK_MAX_BUCKET (BURT_SUBSURFACE_CONTROL_PACK_DIMENSION - 1.0f)
#define BURT_SUBSURFACE_CONTROL_PACK_MAX_VALUE (BURT_SUBSURFACE_CONTROL_PACK_DIMENSION * BURT_SUBSURFACE_CONTROL_PACK_DIMENSION - 1.0f)
#define BURT_SUBSURFACE_THICKNESS_PACK_DIMENSION (64.0f)
#define BURT_SUBSURFACE_THICKNESS_PACK_MAX_BUCKET (BURT_SUBSURFACE_THICKNESS_PACK_DIMENSION - 1.0f)
#define BURT_SUBSURFACE_PROFILE_PACK_DIMENSION (BURT_SUBSURFACE_PROFILE_COUNT)
#define BURT_SUBSURFACE_PROFILE_PACK_MAX_BUCKET (BURT_SUBSURFACE_PROFILE_PACK_DIMENSION - 1.0f)
#define BURT_SUBSURFACE_THICKNESS_PROFILE_PACK_MAX_VALUE (BURT_SUBSURFACE_THICKNESS_PACK_DIMENSION * BURT_SUBSURFACE_PROFILE_PACK_DIMENSION - 1.0f)
#define BURT_SUBSURFACE_DISTORTION_MODE_PACK_COUNT (3.0f)
#define BURT_SUBSURFACE_DISTORTION_MODE_PACK_SCALE (0.999f)
float BurtQuantizeSubsurfaceControlValue(float Value)
{
    return floor(saturate(Value) * BURT_SUBSURFACE_CONTROL_PACK_MAX_BUCKET + 0.5f);
}

float BurtEncodeSubsurfacePowerAmbientForGBuffer(float Power, float Ambient)
{
    float PowerBucket = BurtQuantizeSubsurfaceControlValue(BurtEncodeSubsurfacePowerForGBuffer(Power));
    float AmbientBucket = BurtQuantizeSubsurfaceControlValue(Ambient);
    return (AmbientBucket * BURT_SUBSURFACE_CONTROL_PACK_DIMENSION + PowerBucket) / BURT_SUBSURFACE_CONTROL_PACK_MAX_VALUE;
}

void BurtDecodeSubsurfacePowerAmbientFromGBuffer(float PackedControl, out float Power, out float Ambient)
{
    float PackedBucket = floor(saturate(PackedControl) * BURT_SUBSURFACE_CONTROL_PACK_MAX_VALUE + 0.5f);
    float AmbientBucket = floor(PackedBucket / BURT_SUBSURFACE_CONTROL_PACK_DIMENSION);
    float PowerBucket = PackedBucket - AmbientBucket * BURT_SUBSURFACE_CONTROL_PACK_DIMENSION;
    Power = BurtDecodeSubsurfacePowerFromGBuffer(PowerBucket / BURT_SUBSURFACE_CONTROL_PACK_MAX_BUCKET);
    Ambient = saturate(AmbientBucket / BURT_SUBSURFACE_CONTROL_PACK_MAX_BUCKET);
}

float BurtEncodeSubsurfaceThicknessProfileForGBuffer(float Thickness, float ProfileIndex)
{
    float ThicknessBucket = floor(saturate(Thickness) * BURT_SUBSURFACE_THICKNESS_PACK_MAX_BUCKET + 0.5f);
    float ProfileBucket = BurtClampSubsurfaceProfileIndex(ProfileIndex);
    return (ProfileBucket * BURT_SUBSURFACE_THICKNESS_PACK_DIMENSION + ThicknessBucket) / BURT_SUBSURFACE_THICKNESS_PROFILE_PACK_MAX_VALUE;
}

void BurtDecodeSubsurfaceThicknessProfileFromGBuffer(float PackedValue, out float Thickness, out float ProfileIndex)
{
    float PackedBucket = floor(saturate(PackedValue) * BURT_SUBSURFACE_THICKNESS_PROFILE_PACK_MAX_VALUE + 0.5f);
    float ProfileBucket = floor(PackedBucket / BURT_SUBSURFACE_THICKNESS_PACK_DIMENSION);
    float ThicknessBucket = PackedBucket - ProfileBucket * BURT_SUBSURFACE_THICKNESS_PACK_DIMENSION;
    Thickness = saturate(ThicknessBucket / BURT_SUBSURFACE_THICKNESS_PACK_MAX_BUCKET);
    ProfileIndex = BurtClampSubsurfaceProfileIndex(ProfileBucket);
}

float BurtEncodeSubsurfaceDistortionModeForGBuffer(float Distortion, float ScatteringMode)
{
    float Mode = BurtClampSubsurfaceScatteringMode(ScatteringMode);
    return (Mode + saturate(Distortion) * BURT_SUBSURFACE_DISTORTION_MODE_PACK_SCALE) / BURT_SUBSURFACE_DISTORTION_MODE_PACK_COUNT;
}

void BurtDecodeSubsurfaceDistortionModeFromGBuffer(float PackedValue, out float Distortion, out float ScatteringMode)
{
    float Scaled = saturate(PackedValue) * BURT_SUBSURFACE_DISTORTION_MODE_PACK_COUNT;
    ScatteringMode = floor(min(Scaled, BURT_SUBSURFACE_DISTORTION_MODE_PACK_COUNT - BURT_EPSILON));
    Distortion = saturate((Scaled - ScatteringMode) / BURT_SUBSURFACE_DISTORTION_MODE_PACK_SCALE);
    ScatteringMode = BurtClampSubsurfaceScatteringMode(ScatteringMode);
}

#endif
