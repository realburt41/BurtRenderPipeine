#ifndef BURT_LIGHT_SHAFT_OCCLUSION_INCLUDED
#define BURT_LIGHT_SHAFT_OCCLUSION_INCLUDED

sampler2D _BurtLightShaftOcclusionTexture;
float _BurtLightShaftOcclusionEnabled;

float BurtSampleLightShaftOcclusion(float2 screenUv)
{
    return _BurtLightShaftOcclusionEnabled > 0.5f
        ? tex2D(_BurtLightShaftOcclusionTexture, screenUv).r
        : 1.0f;
}

#endif
