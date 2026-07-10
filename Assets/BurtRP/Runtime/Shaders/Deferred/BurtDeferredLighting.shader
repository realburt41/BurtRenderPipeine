// BurtRP hidden deferred lighting shader; C# creates it with Shader.Find("Hidden/BurtRP/DeferredLighting").
Shader "Hidden/BurtRP/DeferredLighting"
{
    Properties
    {
        [HideInInspector] _BurtDeferredStencilDefaultLitRef ("Deferred Stencil Default Lit Ref", Float) = 32
        [HideInInspector] _BurtDeferredStencilSubsurfaceRef ("Deferred Stencil Subsurface Ref", Float) = 64
        [HideInInspector] _BurtDeferredStencilHairRef ("Deferred Stencil Hair Ref", Float) = 96
        [HideInInspector] _BurtDeferredStencilClearCoatRef ("Deferred Stencil Clear Coat Ref", Float) = 128
        [HideInInspector] _BurtDeferredStencilFabricRef ("Deferred Stencil Fabric Ref", Float) = 160
        [HideInInspector] _BurtDeferredStencilFoliageRef ("Deferred Stencil Foliage Ref", Float) = 192
        [HideInInspector] _BurtDeferredStencilFurRef ("Deferred Stencil Fur Ref", Float) = 224
        [HideInInspector] _BurtDeferredStencilShadingModelMask ("Deferred Stencil Shading Model Mask", Float) = 224
    }

    // This shader serves deferred fullscreen lighting only and is not exposed in material inspectors.
    SubShader
    {
        // Mark as BurtRP-only so other render pipelines do not pick it accidentally.
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        HLSLINCLUDE
            // 顶点输入只需要系统生成的顶点 ID。
            struct Attributes
            {
                // 读取程序化全屏三角形的顶点编号，范围是 0、1、2。
                uint VertexID : SV_VertexID;
            };

            // 顶点 shader 输出给片元 shader 的全屏数据。
            struct Varyings
            {
                // 保存裁剪空间位置，GPU 光栅化必须写入 SV_POSITION。
                float4 PositionCS : SV_POSITION;

                // 保存屏幕 UV，用来采样 GBuffer 和 CameraDepth。
                float2 ScreenUV : TEXCOORD0;
            };
            // #pragma enable_d3d11_debug_symbols
        ENDHLSL

        // Default Lit pass: XRender-style high stencil bits, 32 = DefaultLit.
        Pass
        {
            Name "Burt Deferred Lit Lighting"
            Tags { "LightMode" = "BurtDeferredLitLighting" }
            Cull Off
            ZWrite Off
            ZTest Always
            Stencil
            {
                Ref [_BurtDeferredStencilDefaultLitRef]
                ReadMask [_BurtDeferredStencilShadingModelMask]
                Comp Equal
                Pass Keep
            }
            Blend Off

            HLSLPROGRAM
            #define BURT_DEFERRED_SHADING_MODEL_DEFAULT_LIT 1
            #pragma target 4.5
            #include "Assets/BurtRP/Runtime/Shaders/Deferred/BurtDeferredLightingPass.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        // Hair pass: Burt uses XRender's unused Transmission slot, 96 = Hair.
        Pass
        {
            Name "Burt Deferred Hair Lighting"
            Tags { "LightMode" = "BurtDeferredHairLighting" }
            Cull Off
            ZWrite Off
            ZTest Always
            Stencil
            {
                Ref [_BurtDeferredStencilHairRef]
                ReadMask [_BurtDeferredStencilShadingModelMask]
                Comp Equal
                Pass Keep
            }
            Blend One One

            HLSLPROGRAM
            #define BURT_DEFERRED_SHADING_MODEL_HAIR 1
            #pragma target 4.5
            #include "Assets/BurtRP/Runtime/Shaders/Deferred/BurtDeferredLightingPass.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        // Clear Coat pass: XRender-style high stencil bits, 128 = Coat.
        Pass
        {
            Name "Burt Deferred Clear Coat Lighting"
            Tags { "LightMode" = "BurtDeferredClearCoatLighting" }
            Cull Off
            ZWrite Off
            ZTest Always
            Stencil
            {
                Ref [_BurtDeferredStencilClearCoatRef]
                ReadMask [_BurtDeferredStencilShadingModelMask]
                Comp Equal
                Pass Keep
            }
            Blend One One

            HLSLPROGRAM
            #define BURT_DEFERRED_SHADING_MODEL_CLEAR_COAT 1
            #pragma target 4.5
            #include "Assets/BurtRP/Runtime/Shaders/Deferred/BurtDeferredLightingPass.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        // Subsurface pass: XRender-style high stencil bits, 64 = Subsurface.
        Pass
        {
            Name "Burt Deferred Subsurface Lighting"
            Tags { "LightMode" = "BurtDeferredSubsurfaceLighting" }
            Cull Off
            ZWrite Off
            ZTest Always
            Stencil
            {
                Ref [_BurtDeferredStencilSubsurfaceRef]
                ReadMask [_BurtDeferredStencilShadingModelMask]
                Comp Equal
                Pass Keep
            }
            Blend One One, One Zero

            HLSLPROGRAM
            #define BURT_DEFERRED_SHADING_MODEL_SUBSURFACE 1
            #define BURT_SUBSURFACE_DEFERRED_POSTPROCESS_INPUT 1
            #pragma target 4.5
            #include "Assets/BurtRP/Runtime/Shaders/Deferred/BurtDeferredLightingPass.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        // Fabric pass: XRender-style high stencil bits, 160 = Fabric.
        Pass
        {
            Name "Burt Deferred Fabric Lighting"
            Tags { "LightMode" = "BurtDeferredFabricLighting" }
            Cull Off
            ZWrite Off
            ZTest Always
            Stencil
            {
                Ref [_BurtDeferredStencilFabricRef]
                ReadMask [_BurtDeferredStencilShadingModelMask]
                Comp Equal
                Pass Keep
            }
            Blend One One

            HLSLPROGRAM
            #define BURT_DEFERRED_SHADING_MODEL_FABRIC 1
            #pragma target 4.5
            #include "Assets/BurtRP/Runtime/Shaders/Deferred/BurtDeferredLightingPass.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        // Foliage pass: XRender-style high stencil bits, 192 = Foliage.
        Pass
        {
            Name "Burt Deferred Foliage Lighting"
            Tags { "LightMode" = "BurtDeferredFoliageLighting" }
            Cull Off
            ZWrite Off
            ZTest Always
            Stencil
            {
                Ref [_BurtDeferredStencilFoliageRef]
                ReadMask [_BurtDeferredStencilShadingModelMask]
                Comp Equal
                Pass Keep
            }
            Blend One One

            HLSLPROGRAM
            #define BURT_DEFERRED_SHADING_MODEL_FOLIAGE 1
            #pragma target 4.5
            #include "Assets/BurtRP/Runtime/Shaders/Deferred/BurtDeferredLightingPass.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        // Fur pass: XRender-style high stencil bits, 224 = Fur.
        Pass
        {
            Name "Burt Deferred Fur Lighting"
            Tags { "LightMode" = "BurtDeferredFurLighting" }
            Cull Off
            ZWrite Off
            ZTest Always
            Stencil
            {
                Ref [_BurtDeferredStencilFurRef]
                ReadMask [_BurtDeferredStencilShadingModelMask]
                Comp Equal
                Pass Keep
            }
            Blend One One

            HLSLPROGRAM
            #define BURT_DEFERRED_SHADING_MODEL_FUR 1
            #pragma target 4.5
            #include "Assets/BurtRP/Runtime/Shaders/Deferred/BurtDeferredLightingPass.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }

    // Disable fallback so missing deferred lighting fails visibly instead of using an unrelated shader.
    Fallback Off
}
