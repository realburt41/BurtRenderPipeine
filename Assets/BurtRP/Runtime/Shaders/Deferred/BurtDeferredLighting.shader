// BurtRP hidden deferred lighting shader; C# creates it with Shader.Find("Hidden/BurtRP/DeferredLighting").
Shader "Hidden/BurtRP/DeferredLighting"
{
    // This shader serves deferred fullscreen lighting only and is not exposed in material inspectors.
    SubShader
    {
        // Mark as BurtRP-only so other render pipelines do not pick it accidentally.
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        HLSLINCLUDE
            #include "UnityCG.cginc"

            // 顶点输入只需要系统生成的顶点 ID。
            struct Attributes
            {
                // 读取程序化全屏三角形的顶点编号，范围是 0、1、2。
                uint vertexID : SV_VertexID;
            };

            // 顶点 shader 输出给片元 shader 的全屏数据。
            struct Varyings
            {
                // 保存裁剪空间位置，GPU 光栅化必须写入 SV_POSITION。
                float4 positionCS : SV_POSITION;

                // 保存屏幕 UV，用来采样 GBuffer 和 CameraDepth。
                float2 screenUV : TEXCOORD0;
            };

        ENDHLSL

        // Default Lit pass: writes stencil/model id 0 and clears the lighting target for the later additive model passes.
        Pass
        {
            Name "Burt Deferred Lit Lighting"
            Tags { "LightMode" = "BurtDeferredLitLighting" }
            Cull Off
            ZWrite Off
            ZTest Always
            Stencil
            {
                Ref 0
                ReadMask 3
                Comp Equal
                Pass Keep
            }
            Blend Off

            HLSLPROGRAM
            #define BURT_DEFERRED_SHADING_MODEL_DEFAULT_LIT 1
            #pragma target 4.5
            #pragma multi_compile_fragment _ BURT_SHADING_DEBUG
            #include "Assets/BurtRP/Runtime/Shaders/Deferred/BurtDeferredLightingPass.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        // Hair pass: only shades stencil/model id 1 pixels and adds them after the Lit pass.
        Pass
        {
            Name "Burt Deferred Hair Lighting"
            Tags { "LightMode" = "BurtDeferredHairLighting" }
            Cull Off
            ZWrite Off
            ZTest Always
            Stencil
            {
                Ref 1
                ReadMask 3
                Comp Equal
                Pass Keep
            }
            Blend One One

            HLSLPROGRAM
            #define BURT_DEFERRED_SHADING_MODEL_HAIR 1
            #pragma target 4.5
            #pragma multi_compile_fragment _ BURT_SHADING_DEBUG
            #include "Assets/BurtRP/Runtime/Shaders/Deferred/BurtDeferredLightingPass.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        // Clear Coat pass: only shades stencil/model id 2 pixels and adds them after the Lit pass.
        Pass
        {
            Name "Burt Deferred Clear Coat Lighting"
            Tags { "LightMode" = "BurtDeferredClearCoatLighting" }
            Cull Off
            ZWrite Off
            ZTest Always
            Stencil
            {
                Ref 2
                ReadMask 3
                Comp Equal
                Pass Keep
            }
            Blend One One

            HLSLPROGRAM
            #define BURT_DEFERRED_SHADING_MODEL_CLEAR_COAT 1
            #pragma target 4.5
            #pragma multi_compile_fragment _ BURT_SHADING_DEBUG
            #include "Assets/BurtRP/Runtime/Shaders/Deferred/BurtDeferredLightingPass.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        // Subsurface pass: only shades stencil/model id 3 pixels and adds them after the Lit pass.
        Pass
        {
            Name "Burt Deferred Subsurface Lighting"
            Tags { "LightMode" = "BurtDeferredSubsurfaceLighting" }
            Cull Off
            ZWrite Off
            ZTest Always
            Stencil
            {
                Ref 3
                ReadMask 3
                Comp Equal
                Pass Keep
            }
            Blend One One

            HLSLPROGRAM
            #define BURT_DEFERRED_SHADING_MODEL_SUBSURFACE 1
            #pragma target 4.5
            #pragma multi_compile_fragment _ BURT_SHADING_DEBUG
            #include "Assets/BurtRP/Runtime/Shaders/Deferred/BurtDeferredLightingPass.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }

    // Disable fallback so missing deferred lighting fails visibly instead of using an unrelated shader.
    Fallback Off
}
