// BurtRP hidden deferred lighting debug shader for shadow-oriented material views.
Shader "Hidden/BurtRP/DeferredLightingDebugShadow"
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

    SubShader
    {
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        HLSLINCLUDE
            #define BURT_COMPILE_SHADING_DEBUG 1
            #define BURT_DEFERRED_LIGHTING_DEBUG_CATEGORY_SHADOW 1

            struct Attributes
            {
                uint VertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 PositionCS : SV_POSITION;
                float2 ScreenUV : TEXCOORD0;
            };
            // #pragma enable_d3d11_debug_symbols
        ENDHLSL

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

    Fallback Off
}
