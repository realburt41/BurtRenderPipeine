Shader "BurtRP/InteriorMapping"
{
    Properties
    {
        [HideInInspector] _BaseMap ("Base Map", 2D) = "white" {}
        [HideInInspector] [HDR] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [NoScaleOffset] [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalScale ("Normal Scale", Range(0, 2)) = 1

        [HideInInspector] [NoScaleOffset] _MaskMap ("Mask Map (MOHR)", 2D) = "white" {}
        [HideInInspector] _Metallic ("Metallic", Range(0, 1)) = 0
        [HideInInspector] _Occlusion ("Occlusion", Range(0, 1)) = 1
        [HideInInspector] _Roughness ("Roughness", Range(0, 1)) = 1
        [HideInInspector] _Reflectance ("Reflectance", Range(0, 1)) = 0

        [HideInInspector] [NoScaleOffset] _EmissiveMap ("Emissive Map", 2D) = "white" {}
        [HideInInspector] [HDR] _EmissiveColor ("Emissive Color", Color) = (0, 0, 0, 1)

        [HideInInspector] [ToggleUI] _PreserveSpecular ("Preserve Specular", Float) = 0
        [HideInInspector] _IOR ("IOR", Range(-3, 3)) = 1.5

        [Toggle(BURT_INTERIOR_ATLAS_MODE)] _AtlasMode ("Atlas Mode", Float) = 0
        _AtlasMap ("Atlas2D Room", 2D) = "white" {}
        _RoomCount ("Room Count", Vector) = (1, 1, 0, 0)

        _FakeRoom ("Fake Inner", Cube) = "black" {}
        [HideInInspector] _FakeRoom_ST ("Fake Room ST", Vector) = (1, 1, 0, 0)
        _Depth ("Depth", Range(0, 1)) = 0.5
        _ScaleXAxis ("Scale X Axis", Range(0, 1)) = 0.5
        [HideInInspector] _FrostMap ("Frost Map", 2D) = "white" {}
        [HideInInspector] _FrostMap_ST ("Frost Map ST", Vector) = (1, 1, 0, 0)

        _CubemapLightMultiplier ("Cube Light Multiplier", Range(1, 25)) = 1
        _ColorTemp ("Interior Temper", Range(0, 8)) = 2
        _Exposure ("Interior Exposure", Range(-16, 2)) = -2
        _InteriorIntensity ("Interior Intensity", Float) = 1

        [NoScaleOffset] _InteriorFrontDepth ("Front Depth", 2D) = "white" {}
        [NoScaleOffset] _InteriorBackDepth ("Back Depth", 2D) = "white" {}
        [NoScaleOffset] _InteriorColor ("Object Color", 2D) = "white" {}
        _MarchSteps ("Steps", Range(0, 400)) = 100
        _DitherSteps ("Dither Steps", Range(0, 10)) = 1

        [HideInInspector] _AlphaClip ("Alpha Clip", Float) = 0
        [HideInInspector] _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [HideInInspector] _Surface ("Surface Type", Float) = 0
        [HideInInspector] _DoubleSidedEnable ("Double Sided", Float) = 0
        [HideInInspector] _DoubleSidedNormalMode ("Double Sided Normal Mode", Float) = 0
        [HideInInspector] _DoubleSidedNormalModeConstants ("Double Sided Normal Mode Constants", Vector) = (1, 1, 1, 0)
        [HideInInspector] _Cull ("Cull", Float) = 2
        [HideInInspector] _SrcBlend ("Source Blend", Float) = 1
        [HideInInspector] _DstBlend ("Destination Blend", Float) = 0
        [HideInInspector] _ZWrite ("ZWrite", Float) = 1
        [HideInInspector] _ZTest ("ZTest", Float) = 4
        [ToggleUI] _ResponsiveAA ("Responsive AA", Float) = 0
        [HideInInspector] _BurtGBufferStencilRef ("GBuffer Stencil Ref", Float) = 40
        [HideInInspector] _BurtGBufferStencilReadMask ("GBuffer Stencil Read Mask", Float) = 224
        [HideInInspector] _BurtGBufferStencilWriteMask ("GBuffer Stencil Write Mask", Float) = 232
        [HideInInspector] _MotionVectorsStencilRef ("Motion Vectors Stencil Ref", Float) = 8
        [HideInInspector] _MotionVectorsStencilMask ("Motion Vectors Stencil Mask", Float) = 8
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "BurtRenderPipeline" }
        UsePass "Hidden/Burt Render Pipeline/GI Voxelize/BurtGIVoxelize"

        Pass
        {
            Name "Burt InteriorMapping Motion Vectors"
            Tags { "LightMode" = "BurtMotionVectors" }
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
            #pragma vertex VertMotionVector
            #pragma fragment FragMotionVector
            #pragma multi_compile_instancing
            #pragma target 3.5
            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInteriorMappingProperties.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMotionVectorPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt InteriorMapping Responsive AA Mask"
            Tags { "LightMode" = "BurtResponsiveAAMask" }
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex VertMotionVector
            #pragma fragment FragResponsiveAAMask
            #pragma multi_compile_instancing
            #pragma target 3.5
            #define BURT_MOTION_VECTOR_RESPONSIVE_AA_MASK 1
            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInteriorMappingProperties.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMotionVectorPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt InteriorMapping Shadow Caster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0
            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex VertShadow
            #pragma fragment FragShadow
            #pragma multi_compile_instancing
            #pragma target 3.5
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInteriorMappingProperties.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt InteriorMapping Depth Normals"
            Tags { "LightMode" = "BurtDepthNormals" }
            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex VertGBuffer
            #pragma fragment FragDepthNormals
            #pragma multi_compile_instancing
            #pragma target 4.5
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInteriorMappingProperties.hlsl"
            #define BURT_MATERIAL_SHADING_MODEL_DEFAULT_LIT 1
            #define BURT_MATERIAL_SELECTED_INTERIOR_MAPPING 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtDepthNormalsPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt InteriorMapping GBuffer"
            Tags { "LightMode" = "BurtGBuffer" }
            ZWrite Off
            ZTest Equal
            // GBuffer0 normal/roughness comes from BurtDepthNormals; keep MRT0 untouched here.
            ColorMask 0 0
            Stencil
            {
                Ref [_BurtGBufferStencilRef]
                ReadMask [_BurtGBufferStencilReadMask]
                WriteMask [_BurtGBufferStencilWriteMask]
                Comp Always
                Pass Replace
            }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex VertGBuffer
            #pragma fragment FragGBuffer
            #pragma shader_feature_local_fragment _ BURT_INTERIOR_ATLAS_MODE
            #pragma multi_compile_instancing
            #pragma target 4.5
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInteriorMappingProperties.hlsl"
            #define BURT_MATERIAL_SHADING_MODEL_DEFAULT_LIT 1
            #define BURT_MATERIAL_SELECTED_INTERIOR_MAPPING 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtGBufferPass.hlsl"
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
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInteriorMappingProperties.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtGIRayTracingLit.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "Burt.RenderPipeline.Editor.BurtLitShaderGUI"
    Fallback Off
}
