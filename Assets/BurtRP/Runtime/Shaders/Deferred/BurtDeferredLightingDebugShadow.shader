// Lightweight deferred shadow diagnostics. One dynamic fullscreen pass covers
// every deferred shading model; shadow views do not need seven PBR permutations.
Shader "Hidden/BurtRP/DeferredLightingDebugShadow"
{
    Properties
    {
        [HideInInspector] _BurtDeferredStencilShadingModelMask ("Deferred Stencil Shading Model Mask", Float) = 224
    }

    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        Pass
        {
            Name "Burt Deferred Shadow Debug"
            Tags { "LightMode" = "BurtDeferredShadowDebug" }
            Cull Off
            ZWrite Off
            ZTest Always
            Stencil
            {
                // Deferred materials always write a non-zero shading-model value
                // in the high stencil bits; keep sky/background untouched.
                Ref 0
                ReadMask [_BurtDeferredStencilShadingModelMask]
                Comp NotEqual
                Pass Keep
            }
            Blend Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma skip_optimizations d3d11
            #pragma vertex Vert
            #pragma fragment Frag

            struct Attributes
            {
                uint VertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 PositionCS : SV_POSITION;
                float2 ScreenUV : TEXCOORD0;
            };

            #include "Assets/BurtRP/Runtime/Shaders/Deferred/BurtDeferredShadowDebugPass.hlsl"
            ENDHLSL
        }
    }

    Fallback Off
}
