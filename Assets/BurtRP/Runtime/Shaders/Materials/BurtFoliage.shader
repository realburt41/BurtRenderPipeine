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
        [HideInInspector] _BurtGBufferStencilWriteMask ("GBuffer Stencil Write Mask", Float) = 224
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "BurtRenderPipeline" }

        Pass
        {
            Name "Burt Foliage Depth Only"
            Tags { "LightMode" = "BurtDepthOnly" }
            ColorMask 0
            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex VertDepth
            #pragma fragment FragDepth
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP
            #pragma shader_feature_local _ BURT_FOLIAGE_USE_BAKED_NORMALS
            #pragma multi_compile_instancing
            #pragma target 3.5
            #define BURT_MATERIAL_SHADING_MODEL_FOLIAGE 1
            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtLitProperties.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtDepthOnlyPass.hlsl"
            ENDHLSL
        }

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
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtLitProperties.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Foliage Motion Vectors"
            Tags { "LightMode" = "BurtMotionVectors" }
            ZWrite Off
            ZTest Always
            Cull [_Cull]

            Stencil
            {
                Ref 8
                ReadMask 8
                WriteMask 8
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
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtLitProperties.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMotionVectorPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Foliage GBuffer"
            Tags { "LightMode" = "BurtGBuffer" }
            ZWrite On
            ZTest LEqual
            Stencil
            {
                Ref [_BurtGBufferStencilRef]
                ReadMask 224
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
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtLitProperties.hlsl"
            #define BURT_MATERIAL_SHADING_MODEL_FOLIAGE 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtGBufferPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Foliage Forward"
            Tags { "LightMode" = "BurtForward" }
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Cull [_Cull]
            Blend [_SrcBlend] [_DstBlend]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP
            #pragma shader_feature_local_fragment _ _EMISSION
            #pragma shader_feature_local _ BURT_FOLIAGE_USE_BAKED_NORMALS
            #pragma multi_compile_instancing
            #pragma target 3.5
            #define BURT_MATERIAL_SHADING_MODEL_FOLIAGE 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtLitProperties.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtForwardPass.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "Burt.RenderPipeline.Editor.BurtLitShaderGUI"
    Fallback Off
}
