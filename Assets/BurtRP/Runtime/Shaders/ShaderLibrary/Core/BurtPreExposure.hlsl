#ifndef BURT_PRE_EXPOSURE_INCLUDED
#define BURT_PRE_EXPOSURE_INCLUDED

float _BurtPreExposure;
float _BurtInvPreExposure;
float4 _BurtPreExposureParams;

float3 BurtApplyPreExposure(float3 color)
{
    return color * max(_BurtPreExposure, 0.0f);
}

float3 BurtRemovePreExposure(float3 color)
{
    return color * max(_BurtInvPreExposure, 0.0f);
}

#endif
