Shader "BurtRP/Silk"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        [HDR] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _FacingColor ("Facing Color", Color) = (1, 1, 1, 1)
        _MaskMap ("Mask Map (R Metallic, G Occlusion, A Roughness)", 2D) = "white" {}
        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalScale ("Normal Scale", Range(0, 2)) = 1
        _Reflectance ("Reflectance", Range(0, 1)) = 0.5
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Anisotropy ("Anisotropy", Range(-0.999, 0.999)) = 0
        _Roughness ("Roughness", Range(0, 1)) = 1
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1
        _EmissionMap ("Emission Map", 2D) = "white" {}
        [HDR]_EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        [Toggle(BURT_ALPHA_CLIP)] _AlphaClip ("Alpha Clip", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5

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
        [HideInInspector] _BurtGBufferStencilRef ("GBuffer Stencil Ref", Float) = 160
        [HideInInspector] _BurtGBufferStencilWriteMask ("GBuffer Stencil Write Mask", Float) = 224
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "BurtRenderPipeline" }

        Pass
        {
            Name "Burt Silk Depth Only"
            Tags { "LightMode" = "BurtDepthOnly" }
            ColorMask 0
            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex VertDepth
            #pragma fragment FragDepth
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP
            #pragma multi_compile_instancing
            #pragma target 3.5
            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtLitProperties.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtDepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Silk Shadow Caster"
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
            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtLitProperties.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Silk Motion Vectors"
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

            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtLitProperties.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMotionVectorPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Silk GBuffer"
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
            #pragma multi_compile_instancing
            #pragma target 4.5
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtLitProperties.hlsl"
            #define BURT_MATERIAL_SHADING_MODEL_SILK 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtGBufferPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Silk Forward"
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
            #pragma multi_compile_instancing
            #pragma target 3.5
            #define BURT_MATERIAL_SHADING_MODEL_SILK 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtLitProperties.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtForwardPass.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "Burt.RenderPipeline.Editor.BurtLitShaderGUI"
    Fallback Off
}
