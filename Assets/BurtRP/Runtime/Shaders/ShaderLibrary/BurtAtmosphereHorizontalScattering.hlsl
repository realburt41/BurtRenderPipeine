#ifndef BURT_ATMOSPHERE_HORIZONTAL_SCATTERING_INCLUDED
#define BURT_ATMOSPHERE_HORIZONTAL_SCATTERING_INCLUDED

// XRender writes this triplet from a one-thread compute kernel into a
// StructuredBuffer, copies the 48 bytes into a constant-buffer mirror, then
// publishes that mirror to every fog/sky consumer.
cbuffer _BurtAtmosphereHorizontalScatteringCB
{
    float4 _BurtAtmosphereHorizontalRayleigh;
    float4 _BurtAtmosphereHorizontalMie;
    float4 _BurtAtmosphereHorizontalMultipleScattering;
};

float3 BurtAtmosphereLoadHorizontalScattering(float component)
{
    if (component < 0.5)
    {
        return _BurtAtmosphereHorizontalRayleigh.rgb;
    }

    if (component < 1.5)
    {
        return _BurtAtmosphereHorizontalMie.rgb;
    }

    return _BurtAtmosphereHorizontalMultipleScattering.rgb;
}

#endif
