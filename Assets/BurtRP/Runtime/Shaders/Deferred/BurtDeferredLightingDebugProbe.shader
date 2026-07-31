Shader "Hidden/BurtRP/DeferredLightingDebugProbe"
{
    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        Pass
        {
            Name "Burt Deferred GI Probe Debug"
            Tags { "LightMode" = "BurtDeferredGIProbeDebug" }

            Cull Off
            ZWrite Off
            ZTest Always
            Blend Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Assets/BurtRP/Runtime/Shaders/Deferred/BurtDeferredGIProbeDebugPass.hlsl"
            ENDHLSL
        }
    }

    Fallback Off
}
