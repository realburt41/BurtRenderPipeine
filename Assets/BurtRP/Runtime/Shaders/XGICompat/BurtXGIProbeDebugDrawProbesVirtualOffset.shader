Shader "Hidden/XRender/XGI/XGIProbe/Debug/DrawProbesVirtualOffset"
{
    Properties
    {
        _BurtXGICompatTint ("Tint", Color) = (1.0, 0.52, 0.18, 0.9)
        _BurtXGICompatZTest ("ZTest", Float) = 4
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "BurtRenderPipeline" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "Forward" }
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest [_BurtXGICompatZTest]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex BurtXGICompatVert
            #pragma fragment BurtXGICompatFrag
            #pragma multi_compile_instancing
            #include "Assets/BurtRP/Runtime/Shaders/XGICompat/BurtXGICompatDebug.hlsl"
            ENDHLSL
        }
    }
    Fallback Off
}
