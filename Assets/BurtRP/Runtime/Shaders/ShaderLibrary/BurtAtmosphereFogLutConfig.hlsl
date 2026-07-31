#ifndef BURT_ATMOSPHERE_FOG_LUT_CONFIG_INCLUDED
#define BURT_ATMOSPHERE_FOG_LUT_CONFIG_INCLUDED

// XRender AtmosphereRenderConfig topology. Generation maps a texel-center W
// through W^2 * coverage; consumers use sqrt(distance / coverage).
static const int BURT_ATMOSPHERE_FOG_LUT_WIDTH = 32;
static const int BURT_ATMOSPHERE_FOG_LUT_HEIGHT = 32;
static const int BURT_ATMOSPHERE_FOG_LUT_DEPTH = 16;
static const int BURT_ATMOSPHERE_FOG_LUT_SAMPLES_PER_SLICE = 2;
static const float BURT_ATMOSPHERE_FOG_LUT_COVERAGE_KM = 96.0f;

#endif
