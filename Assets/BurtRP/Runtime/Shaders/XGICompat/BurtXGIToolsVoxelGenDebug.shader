Shader "Hidden/XRender/XGI/XGIToolsVoxelGenDebug"
{
    Properties
    {
        _BurtXGICompatTint ("Tint", Color) = (0.26, 0.95, 0.38, 0.9)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "BurtRenderPipeline" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "Forward" }
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex BurtXGICompatVert
            #pragma fragment BurtXGICompatFrag
            #include "Assets/BurtRP/Runtime/Shaders/XGICompat/BurtXGICompatDebug.hlsl"
            ENDHLSL
        }
    }
    Fallback Off
}
