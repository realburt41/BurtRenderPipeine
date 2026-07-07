Shader "BurtRP/Trunk"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        [HDR] _BaseColor ("Base Tint", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _MaskMap ("MOHR Map", 2D) = "white" {}
        _VertexAORemap ("Vertex AO Remap", Vector) = (0, 1, 0, 0)
        _Specular ("Specular", Range(0, 1)) = 0.5
        [NoScaleOffset][Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalScale ("Normal Scale", Range(0, 2)) = 1

        _TreeHeight ("Tree Height", Float) = 25
        _MaxBendAngle ("Max Bend Angle", Range(0, 2)) = 0
        _SwayIntensity ("Sway Intensity", Range(0, 1.2)) = 0
        _BendMaskPow ("Bend Mask Power", Range(0, 3)) = 0
        _ToTrunkMaskPow ("Distance To Trunk Power", Range(0.1, 5)) = 1

        _TerrainBlend_TerrainTog ("Use Terrain Blend", Int) = 0
        _TerrainBlend_BlendHeight ("Blend Height", Range(0.001, 3)) = 0.2
        [HideInInspector] _TerrainBlendEnable ("Terrain Blend", Float) = 0
        [HideInInspector] _TerrainBlendHeight ("Terrain Blend Height", Range(0, 10)) = 0.5
        [HideInInspector] _TerrainBlendDetailHeight ("Terrain Blend Detail Height", Vector) = (1, 1, 1, 1)

        _EmissionMap ("Emission Map", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        [Toggle(BURT_ALPHA_CLIP)] _AlphaClip ("Alpha Clip", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [HideInInspector] [ToggleUI] _AlphaCutoffEnable ("XRender Alpha Clip", Float) = 0
        [HideInInspector] _AlphaCutoff ("XRender Alpha Cutoff", Range(0, 1)) = 0.5

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
        [HideInInspector] _BurtGBufferStencilRef ("GBuffer Stencil Ref", Float) = 32
        [HideInInspector] _BurtGBufferStencilReadMask ("GBuffer Stencil Read Mask", Float) = 224
        [HideInInspector] _BurtGBufferStencilWriteMask ("GBuffer Stencil Write Mask", Float) = 224
        [HideInInspector] _MotionVectorsStencilRef ("Motion Vectors Stencil Ref", Float) = 8
        [HideInInspector] _MotionVectorsStencilMask ("Motion Vectors Stencil Mask", Float) = 8
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "BurtRenderPipeline" }

        Pass
        {
            Name "Burt Trunk Motion Vectors"
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
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP
            #pragma multi_compile_instancing
            #pragma target 3.5
            #define BURT_MATERIAL_SHADING_MODEL_TRUNK 1
            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtLitProperties.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMotionVectorPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Trunk Shadow Caster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0
            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex VertShadow
            #pragma fragment FragShadow
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP
            #pragma multi_compile_instancing
            #pragma target 3.5
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #define BURT_MATERIAL_SHADING_MODEL_TRUNK 1
            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtLitProperties.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Trunk Depth Normals"
            Tags { "LightMode" = "BurtDepthNormals" }
            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex VertGBuffer
            #pragma fragment FragDepthNormals
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP
            #pragma shader_feature_local_fragment _ _EMISSION
            #pragma multi_compile_instancing
            #pragma target 4.5
            #define BURT_MATERIAL_SHADING_MODEL_TRUNK 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtLitProperties.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtDepthNormalsPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Trunk GBuffer"
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
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP
            #pragma shader_feature_local_fragment _ _EMISSION
            #pragma multi_compile_instancing
            #pragma target 4.5
            #define BURT_MATERIAL_SHADING_MODEL_TRUNK 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtLitProperties.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtGBufferPass.hlsl"
            ENDHLSL
        }

    }

    CustomEditor "Burt.RenderPipeline.Editor.BurtLitShaderGUI"
    Fallback Off
}
