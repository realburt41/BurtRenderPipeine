Shader "BurtRP/Multipass Fur"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _BaseMap ("Base Map", 2D) = "white" {}
        _DarkColor ("Dark Color", Color) = (0, 0, 0, 0)
        _BaseMapPanner ("Base Map Panner", Vector) = (1, 1, 0, 0)

        [NoScaleOffset] [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalScale ("Normal Scale", Range(0, 2)) = 1

        [NoScaleOffset] _MaskMap ("Mask Map (R Metallic, G Occlusion, B Height, A Roughness)", 2D) = "white" {}
        _Occlusion ("AO", Range(0, 1)) = 1
        _Roughness ("Roughness", Range(0, 1)) = 0.75
        _Reflectance ("Reflectance", Range(0, 1)) = 0.5
        _Anisotropy ("Anisotropy", Range(-0.999, 0.999)) = 0

        [NoScaleOffset] _EmissiveMap ("Emissive Map", 2D) = "white" {}
        [HDR] _EmissiveColor ("Emissive Color", Color) = (0, 0, 0, 1)
        _EmissiveTillingPanner ("Emissive Tiling Panner", Vector) = (1, 1, 0, 0)
        [Toggle] _EmissiveUseViewSpaceUV ("Effect Use View Space UV", Float) = 0
        _ViewSpaceUVNormalIntensity ("View Space UV Normal Intensity", Range(0, 1)) = 0
        _FurRimIntensity ("Fur Rim Intensity", Range(0, 5)) = 1
        _FurRimPower ("Fur Rim Power", Range(1, 20)) = 8

        _FurAttenuation ("Fur Attenuation", Range(0, 2)) = 1
        _FurTickness ("Fur Thickness", Range(0, 1)) = 1
        _FurTicknessCurve ("Fur Thickness Curve", Range(0, 1)) = 1
        _FurExpand ("Fur Base Expand", Range(0, 5)) = 0
        _FurSpacing ("Fur Spacing", Float) = 3
        _FurSpacingMax ("Fur Spacing Max", Range(0, 1)) = 1

        [NoScaleOffset] _FlowTex ("Flow Noise", 2D) = "white" {}
        [Toggle] _FlowTexUV2 ("Flow Uses UV1", Range(0, 1)) = 0
        _FlowTilling ("Flow Tiling", Float) = 50
        _FlowPanner ("Flow Panner", Vector) = (0, 0, 0, 0)

        [NoScaleOffset] _FlowDirectionMap ("Flow Direction Map", 2D) = "gray" {}
        [Toggle(BURT_MULTIPASS_FUR_USE_DIRECTION_MAP)] _UseDirectionMap ("Use Direction Map", Float) = 0
        [NoScaleOffset] _FlowDirectionMapSegmentArray ("Direction Segment Array", 2DArray) = "" {}
        [Toggle] _UseDirectionMapSegment ("Use Direction Map Segment", Range(0, 1)) = 0
        [Toggle] _FlowDirectionUV2 ("Flow Direction Uses UV1", Range(0, 1)) = 0
        _FlowDirectionIntensity ("Flow Direction Intensity", Range(0, 2)) = 0
        _FlowDirectionIntensitySegment1 ("Direction Segment 1 Intensity", Range(0, 2)) = 0.6
        _FlowDirectionIntensitySegment2 ("Direction Segment 2 Intensity", Range(0, 2)) = 0.8
        _FlowDirectionIntensitySegment3 ("Direction Segment 3 Intensity", Range(0, 2)) = 1.2
        [Enum(X,0,Y,1,Z,2)] _FurGravityDirection ("Fur Gravity Direction", Float) = 0
        _FurGravityIntensity ("Fur Gravity Intensity", Range(-1, 1)) = 0
        [Toggle] _FurBlurEnabled ("Fur Blur Enabled", Float) = 1
        _FurBlurDistance ("Fur Blur Distance", Float) = 8

        [Toggle(BURT_ALPHA_CLIP)] _AlphaClip ("Alpha Clip", Float) = 1
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.01
        [ToggleUI] _ResponsiveAA ("Responsive AA", Float) = 0

        [HideInInspector] _FurScale ("Fur Scale", Vector) = (1, 1, 1, 1)
        [HideInInspector] _FurMaxCount ("Fur Max Count", Integer) = 16
        [HideInInspector] _DoubleSidedNormalModeConstants ("Double Sided Normal Mode Constants", Vector) = (1, 1, -1, 0)
        [HideInInspector] _Cull ("Cull", Float) = 2
        [HideInInspector] _SrcBlend ("Source Blend", Float) = 1
        [HideInInspector] _DstBlend ("Destination Blend", Float) = 0
        [HideInInspector] _ZWrite ("ZWrite", Float) = 1
        [HideInInspector] _ZTest ("ZTest", Float) = 4
        [HideInInspector] _BurtGBufferStencilRef ("GBuffer Stencil Ref", Float) = 224
        [HideInInspector] _BurtGBufferStencilReadMask ("GBuffer Stencil Read Mask", Float) = 224
        [HideInInspector] _BurtGBufferStencilWriteMask ("GBuffer Stencil Write Mask", Float) = 224
        [HideInInspector] _MotionVectorsStencilRef ("Motion Vectors Stencil Ref", Float) = 8
        [HideInInspector] _MotionVectorsStencilMask ("Motion Vectors Stencil Mask", Float) = 8
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "BurtRenderPipeline" }
        UsePass "Hidden/Burt Render Pipeline/GI Voxelize/BurtGIVoxelize"

        Pass
        {
            Name "Burt Multipass Fur Depth Only"
            Tags { "LightMode" = "BurtDepthOnly" }
            ColorMask 0
            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex VertMultipassFur
            #pragma fragment FragMultipassFurDepth
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP
            #pragma shader_feature_local_vertex _ BURT_MULTIPASS_FUR_USE_DIRECTION_MAP
            #pragma multi_compile_instancing
            #pragma target 4.5
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMultipassFurProperties.hlsl"
            #define BURT_MATERIAL_SHADING_MODEL_FUR 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMultipassFurPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Multipass Fur Responsive AA Mask"
            // Multipass fur is submitted explicitly by BurtMultipassRenderer. A dedicated
            // LightMode prevents the regular responsive RendererList from drawing the base
            // renderer once before the shell instances are submitted.
            Tags { "LightMode" = "BurtMultipassResponsiveAAMask" }
            ZWrite Off
            ZTest Equal
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex VertMultipassFur
            #pragma fragment FragMultipassFurResponsiveAAMask
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP
            #pragma shader_feature_local_vertex _ BURT_MULTIPASS_FUR_USE_DIRECTION_MAP
            #pragma multi_compile_instancing
            #pragma target 4.5
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMultipassFurProperties.hlsl"
            #define BURT_MATERIAL_SHADING_MODEL_FUR 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMultipassFurPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Multipass Fur Shadow Caster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0
            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex VertMultipassFur
            #pragma fragment FragMultipassFurDepth
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP
            #pragma shader_feature_local_vertex _ BURT_MULTIPASS_FUR_USE_DIRECTION_MAP
            #pragma multi_compile_instancing
            #pragma target 4.5
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMultipassFurProperties.hlsl"
            #define BURT_MATERIAL_SHADING_MODEL_FUR 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMultipassFurPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Multipass Fur GBuffer"
            Tags { "LightMode" = "BurtGBuffer" }
            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            Stencil
            {
                Ref [_BurtGBufferStencilRef]
                ReadMask [_BurtGBufferStencilReadMask]
                WriteMask [_BurtGBufferStencilWriteMask]
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex VertMultipassFur
            #pragma fragment FragMultipassFurGBuffer
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP
            #pragma shader_feature_local_vertex _ BURT_MULTIPASS_FUR_USE_DIRECTION_MAP
            #pragma multi_compile_instancing
            #pragma target 4.5
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMultipassFurProperties.hlsl"
            #define BURT_MATERIAL_SHADING_MODEL_FUR 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMultipassFurPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Multipass Fur Forward"
            Tags { "LightMode" = "BurtForward" }
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Cull [_Cull]
            Blend [_SrcBlend] [_DstBlend]

            HLSLPROGRAM
            #pragma vertex VertMultipassFur
            #pragma fragment FragMultipassFurForward
            #pragma multi_compile_fragment _ BURT_MAIN_LIGHT_PCF_3 BURT_MAIN_LIGHT_PCF_7
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP
            #pragma shader_feature_local_vertex _ BURT_MULTIPASS_FUR_USE_DIRECTION_MAP
            #pragma multi_compile_instancing
            #pragma target 4.5
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMultipassFurProperties.hlsl"
            #define BURT_MATERIAL_SHADING_MODEL_FUR 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMultipassFurPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Multipass Fur Motion Vectors"
            // The TAA and screen-probe paths submit every fur shell explicitly. Keep this pass
            // out of the regular object-motion RendererList to avoid an invalid, duplicate
            // base-renderer velocity draw.
            Tags { "LightMode" = "BurtMultipassMotionVectors" }
            ZWrite Off
            ZTest Equal
            Cull [_Cull]

            Stencil
            {
                Ref [_MotionVectorsStencilRef]
                ReadMask 8
                WriteMask [_MotionVectorsStencilMask]
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex VertMultipassFurVelocity
            #pragma fragment FragMultipassFurTemporalAAMotionVectors
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP
            #pragma shader_feature_local_vertex _ BURT_MULTIPASS_FUR_USE_DIRECTION_MAP
            #pragma multi_compile_instancing
            #pragma target 4.5
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMultipassFurProperties.hlsl"
            #define BURT_MATERIAL_SHADING_MODEL_FUR 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMultipassFurPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Multipass Fur Blur Property"
            Tags { "LightMode" = "BurtFurBlurProperty" }
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex VertMultipassFur
            #pragma fragment FragMultipassFurBlurProperty
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP
            #pragma shader_feature_local_vertex _ BURT_MULTIPASS_FUR_USE_DIRECTION_MAP
            #pragma multi_compile_instancing
            #pragma target 4.5
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMultipassFurProperties.hlsl"
            #define BURT_MATERIAL_SHADING_MODEL_FUR 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMultipassFurPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Multipass Fur Blur Velocity"
            Tags { "LightMode" = "BurtFurBlurVelocity" }
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex VertMultipassFurVelocity
            #pragma fragment FragMultipassFurBlurVelocity
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP
            #pragma shader_feature_local_vertex _ BURT_MULTIPASS_FUR_USE_DIRECTION_MAP
            #pragma multi_compile_instancing
            #pragma target 4.5
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMultipassFurProperties.hlsl"
            #define BURT_MATERIAL_SHADING_MODEL_FUR 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMultipassFurPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "BurtGI"
            Tags { "LightMode" = "RayTracing" }

            HLSLPROGRAM
            #pragma only_renderers d3d11 d3d12
            #pragma raytracing BurtGI
            #pragma shader_feature_local _ BURT_ALPHA_CLIP
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMultipassFurProperties.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtGIRayTracingFur.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "Burt.RenderPipeline.Editor.BurtMultipassFurShaderGUI"
    Fallback Off
}
