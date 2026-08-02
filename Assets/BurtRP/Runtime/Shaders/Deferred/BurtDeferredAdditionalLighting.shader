Shader "Hidden/BurtRP/DeferredAdditionalLighting"
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
        Cull Off
        ZWrite Off
        ZTest Always

        HLSLINCLUDE
            // Fullscreen punctual fallback. Tile/bin variants live in the
            // dedicated DeferredPunctualTileLighting shader so vertex and
            // fragment specialization do not form a Cartesian product.
            #pragma shader_feature_local_fragment _ BURT_USE_DEBUG_MODE_DEFERRED
            #if defined(BURT_USE_DEBUG_MODE_DEFERRED)
                #define BURT_DEFERRED_ADDITIONAL_LIGHTING_DEBUG 1
                // Do not put skip_optimizations in this keyword branch. Unity
                // applies that ShaderLab pragma to the non-debug variant too.
            #endif
        ENDHLSL

        Pass
        {
            Name "Burt Deferred Lit Additional Lighting"
            Tags { "LightMode" = "BurtDeferredLitAdditionalLighting" }
            Stencil
            {
                Ref [_BurtDeferredStencilDefaultLitRef]
                ReadMask [_BurtDeferredStencilShadingModelMask]
                Comp Equal
                Pass Keep
            }
            Blend One One, Zero One
            HLSLPROGRAM
            #define BURT_DEFERRED_SHADING_MODEL_DEFAULT_LIT 1
            #pragma target 4.5
            #include "Assets/BurtRP/Runtime/Shaders/Deferred/BurtDeferredAdditionalLightingPass.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        Pass
        {
            Name "Burt Deferred Hair Additional Lighting"
            Tags { "LightMode" = "BurtDeferredHairAdditionalLighting" }
            Stencil
            {
                Ref [_BurtDeferredStencilHairRef]
                ReadMask [_BurtDeferredStencilShadingModelMask]
                Comp Equal
                Pass Keep
            }
            Blend One One, Zero One
            HLSLPROGRAM
            #define BURT_DEFERRED_SHADING_MODEL_HAIR 1
            #pragma target 4.5
            #include "Assets/BurtRP/Runtime/Shaders/Deferred/BurtDeferredAdditionalLightingPass.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        Pass
        {
            Name "Burt Deferred Clear Coat Additional Lighting"
            Tags { "LightMode" = "BurtDeferredClearCoatAdditionalLighting" }
            Stencil
            {
                Ref [_BurtDeferredStencilClearCoatRef]
                ReadMask [_BurtDeferredStencilShadingModelMask]
                Comp Equal
                Pass Keep
            }
            Blend One One, Zero One
            HLSLPROGRAM
            #define BURT_DEFERRED_SHADING_MODEL_CLEAR_COAT 1
            #pragma target 4.5
            #include "Assets/BurtRP/Runtime/Shaders/Deferred/BurtDeferredAdditionalLightingPass.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        Pass
        {
            Name "Burt Deferred Subsurface Additional Lighting"
            Tags { "LightMode" = "BurtDeferredSubsurfaceAdditionalLighting" }
            Stencil
            {
                Ref [_BurtDeferredStencilSubsurfaceRef]
                ReadMask [_BurtDeferredStencilShadingModelMask]
                Comp Equal
                Pass Keep
            }
            Blend One One, One One
            HLSLPROGRAM
            #define BURT_DEFERRED_SHADING_MODEL_SUBSURFACE 1
            #define BURT_SUBSURFACE_DEFERRED_POSTPROCESS_INPUT 1
            #pragma target 4.5
            #include "Assets/BurtRP/Runtime/Shaders/Deferred/BurtDeferredAdditionalLightingPass.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        Pass
        {
            Name "Burt Deferred Fabric Additional Lighting"
            Tags { "LightMode" = "BurtDeferredFabricAdditionalLighting" }
            Stencil
            {
                Ref [_BurtDeferredStencilFabricRef]
                ReadMask [_BurtDeferredStencilShadingModelMask]
                Comp Equal
                Pass Keep
            }
            Blend One One, Zero One
            HLSLPROGRAM
            #define BURT_DEFERRED_SHADING_MODEL_FABRIC 1
            #pragma target 4.5
            #include "Assets/BurtRP/Runtime/Shaders/Deferred/BurtDeferredAdditionalLightingPass.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        Pass
        {
            Name "Burt Deferred Foliage Additional Lighting"
            Tags { "LightMode" = "BurtDeferredFoliageAdditionalLighting" }
            Stencil
            {
                Ref [_BurtDeferredStencilFoliageRef]
                ReadMask [_BurtDeferredStencilShadingModelMask]
                Comp Equal
                Pass Keep
            }
            Blend One One, Zero One
            HLSLPROGRAM
            #define BURT_DEFERRED_SHADING_MODEL_FOLIAGE 1
            #pragma target 4.5
            #include "Assets/BurtRP/Runtime/Shaders/Deferred/BurtDeferredAdditionalLightingPass.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        Pass
        {
            Name "Burt Deferred Fur Additional Lighting"
            Tags { "LightMode" = "BurtDeferredFurAdditionalLighting" }
            Stencil
            {
                Ref [_BurtDeferredStencilFurRef]
                ReadMask [_BurtDeferredStencilShadingModelMask]
                Comp Equal
                Pass Keep
            }
            Blend One One, Zero One
            HLSLPROGRAM
            #define BURT_DEFERRED_SHADING_MODEL_FUR 1
            #pragma target 4.5
            #include "Assets/BurtRP/Runtime/Shaders/Deferred/BurtDeferredAdditionalLightingPass.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }

    Fallback Off
}
