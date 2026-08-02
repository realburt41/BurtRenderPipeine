Shader "BurtRP/Foliage"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        [HDR] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _MaskMap ("Legacy Mask Map", 2D) = "white" {}
        _AlphaMap ("Alpha Map", 2D) = "white" {}
        _AlphaIncrease ("Alpha Distance Increase", Range(0, 1)) = 0.4
        _NormalMap ("NSR Map (RG Normal, B Thickness, A Roughness)", 2D) = "bump" {}
        _NormalScale ("Normal Scale", Range(0, 2)) = 1
        _Reflectance ("Reflectance", Range(0, 1)) = 0.35
        _Smoothness ("Smoothness", Range(0, 1)) = 0.45
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1
        [HDR] _FoliageTransmissionColor ("Transmission Color", Color) = (0.55, 0.85, 0.35, 1)
        _FoliageTransmissionWeight ("Transmission Weight", Range(0, 1)) = 0.55
        _FoliageThickness ("Thickness", Range(0, 1)) = 0.5
        _FoliageBackLight ("Back Light Wrap", Range(0, 1)) = 0.55
        _FoliageSubsurfaceColorSaturate ("Transmission Saturation", Range(0, 2)) = 0.6
        [HDR] _SubsurfaceColor ("SSS Tint", Color) = (1, 1, 1, 1)
        _SubsurfaceColorSaturate ("SSS Saturation", Range(0, 2)) = 0.6
        _RoughnessScale ("Roughness Scale", Range(0, 2)) = 1
        _ReflectanceScale ("Specular Color Scale", Range(0, 1)) = 0.5
        _ThicknessScale ("Thickness Scale", Range(0, 1)) = 1
        _TransmissionNdotL ("Transmission NdotL", Range(0, 1)) = 0.5
        _VertexAORemap ("Vertex AO Remap", Vector) = (0.6, 1, 0, 0)
        _TintPalette ("Global Tint Palette", 2D) = "white" {}
        _LocalTintPalette ("Local Tint Palette", 2D) = "white" {}
        _TintValue ("Tint Value", Range(0, 1)) = 0
        _TintScale ("Local Tint Scale", Range(0, 2)) = 0.26
        _LocalTintColor ("Local Tint Color", Color) = (1, 1, 1, 1)
        _TintAOHeightRatio ("Tint AO Height Ratio", Range(0, 1)) = 0.5
        _TintAORemap ("Tint AO Remap", Vector) = (0.754, 1, 0, 0)
        _TintHeightContrast ("Tint Height Contrast", Range(0.1, 10)) = 1
        _TreeHeight ("Tree Height", Float) = 25
        _MaxBendAngle ("Max Bend Angle", Range(0, 3)) = 0
        _SwayIntensity ("Sway Intensity", Range(0, 1)) = 0.05
        _FlutterTipFrequency ("Flutter Tip Frequency", Range(0, 0.7)) = 0.08
        _FlutterTipIntensity ("Flutter Tip Intensity", Range(0, 15)) = 0.2
        _BendMaskPow ("Bend Mask Power", Range(0.1, 3)) = 1
        _ToTrunkMaskPow ("Distance To Trunk Power", Range(0.1, 5)) = 1
        _CustomEnum ("Tint Type", Float) = 0
        [HideInInspector] _FoliageTintMode ("Legacy Tint Mode", Float) = 0
        [Toggle(BURT_FOLIAGE_USE_BAKED_NORMALS)] _FoliageUseBakedNormals ("Use Baked Normals", Float) = 0
        _EmissionMap ("Emission Map", 2D) = "white" {}
        [HDR]_EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        [Toggle(BURT_ALPHA_CLIP)] _AlphaClip ("Alpha Clip", Float) = 1
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5

        [HideInInspector] _Surface ("Surface Type", Float) = 0
        [HideInInspector] _DoubleSidedEnable ("Double Sided", Float) = 1
        [HideInInspector] _DoubleSidedNormalMode ("Double Sided Normal Mode", Float) = 2
        [HideInInspector] _DoubleSidedNormalModeConstants ("Double Sided Normal Mode Constants", Vector) = (1, 1, -1, 0)
        [HideInInspector] _Cull ("Cull", Float) = 0
        [HideInInspector] _SrcBlend ("Source Blend", Float) = 1
        [HideInInspector] _DstBlend ("Destination Blend", Float) = 0
        [HideInInspector] _ZWrite ("ZWrite", Float) = 1
        [HideInInspector] _ZTest ("ZTest", Float) = 4
        [ToggleUI] _ResponsiveAA ("Responsive AA", Float) = 0
        [HideInInspector] _BurtGBufferStencilRef ("GBuffer Stencil Ref", Float) = 192
        [HideInInspector] _BurtGBufferStencilReadMask ("GBuffer Stencil Read Mask", Float) = 224
        [HideInInspector] _BurtGBufferStencilWriteMask ("GBuffer Stencil Write Mask", Float) = 224
        [HideInInspector] _MotionVectorsStencilRef ("Motion Vectors Stencil Ref", Float) = 8
        [HideInInspector] _MotionVectorsStencilMask ("Motion Vectors Stencil Mask", Float) = 8
    }

    HLSLINCLUDE
    // #pragma enable_d3d11_debug_symbols
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "BurtRenderPipeline" }
        UsePass "Hidden/Burt Render Pipeline/GI Voxelize/BurtGIVoxelize"

        Pass
        {
            Name "Burt Foliage Shadow Caster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0
            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex VertShadow
            #pragma fragment FragShadow
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP
            #pragma shader_feature_local _ BURT_FOLIAGE_USE_BAKED_NORMALS
            #pragma multi_compile_instancing
            #pragma target 3.5
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #define BURT_MATERIAL_SHADING_MODEL_FOLIAGE 1
            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtFoliageProperties.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Foliage Depth Normals"
            Tags { "LightMode" = "BurtDepthNormals" }
            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex VertGBuffer
            #pragma fragment FragDepthNormals
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP
            #pragma shader_feature_local_fragment _ _EMISSION
            #pragma shader_feature_local _ BURT_FOLIAGE_USE_BAKED_NORMALS
            #pragma multi_compile_instancing
            #pragma target 4.5
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtFoliageProperties.hlsl"
            #define BURT_MATERIAL_SHADING_MODEL_FOLIAGE 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtDepthNormalsPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Foliage Motion Vectors"
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
            #define BURT_MATERIAL_SHADING_MODEL_FOLIAGE 1
            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtFoliageProperties.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMotionVectorPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Foliage Responsive AA Mask"
            Tags { "LightMode" = "BurtResponsiveAAMask" }
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex VertMotionVector
            #pragma fragment FragResponsiveAAMask
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP
            #pragma multi_compile_instancing
            #pragma target 3.5
            #define BURT_MATERIAL_SHADING_MODEL_FOLIAGE 1
            #define BURT_MOTION_VECTOR_RESPONSIVE_AA_MASK 1
            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtFoliageProperties.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMotionVectorPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Foliage GBuffer"
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
            #pragma shader_feature_local _ BURT_FOLIAGE_USE_BAKED_NORMALS
            #pragma multi_compile_instancing
            #pragma target 4.5
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtFoliageProperties.hlsl"
            #define BURT_MATERIAL_SHADING_MODEL_FOLIAGE 1
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
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtFoliageProperties.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtGIRayTracingLit.hlsl"
            ENDHLSL
        }

    }

    CustomEditor "Burt.RenderPipeline.Editor.BurtLitShaderGUI"
    Fallback Off
}
