#ifndef BURT_FORWARD_TRANSLUCENCY_VOLUME_INCLUDED
#define BURT_FORWARD_TRANSLUCENCY_VOLUME_INCLUDED

sampler3D _BurtGITranslucencyVolume0;
sampler3D _BurtGITranslucencyVolume1;
float4 _BurtGIApplyIndirectParams;
float4 _BurtGITranslucencyVolumeParams;
float4 _BurtGITranslucencyVolumeGridSize;
float4 _BurtGITranslucencyVolumeGridZParams;
float4 _BurtGITranslucencyVolumeParams0;
float4x4 _BurtDeferredCurrentNonJitteredViewProjectionMatrix;

float3 BurtForwardTranslucencyVolumeUV(float3 positionWS)
{
    float4 clipPosition = mul(_BurtDeferredCurrentNonJitteredViewProjectionMatrix, float4(positionWS, 1.0f));
    float safeW = abs(clipPosition.w) > 1.0e-5f ? clipPosition.w : (clipPosition.w < 0.0f ? -1.0e-5f : 1.0e-5f);
    float2 volumeXY = clipPosition.xy / safeW * 0.5f + 0.5f;
#if UNITY_UV_STARTS_AT_TOP
    volumeXY.y = 1.0f - volumeXY.y;
#endif
    float slice = log2(max(abs(safeW) * _BurtGITranslucencyVolumeGridZParams.x + _BurtGITranslucencyVolumeGridZParams.y, 1.0e-5f)) *
        _BurtGITranslucencyVolumeGridZParams.z / max(_BurtGITranslucencyVolumeGridSize.z, 1.0f);
    return saturate(float3(volumeXY, slice));
}

float4 BurtForwardTranslucencyVolumeDiffuseTransferSH2(float3 normalWS)
{
    const float sh0 = 0.28209479177387814f;
    const float sh1 = 0.4886025119029199f;
    const float pi = 3.14159265358979323846f;
    normalWS *= rsqrt(max(dot(normalWS, normalWS), 1.0e-6f));
    return float4(
        sh0 * pi,
        -sh1 * normalWS.y * (2.0f * pi / 3.0f),
        sh1 * normalWS.z * (2.0f * pi / 3.0f),
        -sh1 * normalWS.x * (2.0f * pi / 3.0f));
}

float3 BurtSampleForwardTranslucencyVolume(float3 positionWS, float3 normalWS, float3 diffuseColor)
{
    if (_BurtGITranslucencyVolumeParams.x < 0.5f || _BurtGIApplyIndirectParams.x < 0.5f)
    {
        return 0.0f;
    }

    float3 uvw = BurtForwardTranslucencyVolumeUV(positionWS);
    float4 volume0 = tex3D(_BurtGITranslucencyVolume0, uvw);
    float4 volume1 = tex3D(_BurtGITranslucencyVolume1, uvw);
    float confidence = saturate(max(volume0.a, volume1.a));
    float ambientLuminance = dot(volume0.rgb, float3(0.2126f, 0.7152f, 0.0722f));
    float3 ambientChromaticity = volume0.rgb / max(ambientLuminance, 1.0e-5f);
    float4 diffuseTransfer = BurtForwardTranslucencyVolumeDiffuseTransferSH2(normalWS);
    float3 irradiance = volume0.rgb * diffuseTransfer.x + ambientChromaticity * dot(volume1.rgb, diffuseTransfer.yzw);
    irradiance = max(irradiance * 0.31830988618f, 0.0f);
    float intensity = max(_BurtGIApplyIndirectParams.y, 0.0f) * max(_BurtGITranslucencyVolumeParams.y, 0.0f);
    return irradiance * max(diffuseColor, 0.0f) * confidence * intensity;
}

#endif
