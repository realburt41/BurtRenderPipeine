Shader "BurtRP/Clear Crystal"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        [HDR] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _BaseColorFlowSpeed ("Base Color Flow Speed (UV/s)", Vector) = (0, 0, 0, 0)
        _ShadowIntensity ("Shadow Intensity", Range(0, 1)) = 1

        [NoScaleOffset][Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalScale ("Normal Scale", Range(0, 2)) = 1
        [NoScaleOffset][Normal] _DetailNormalMap ("Detail Normal Map", 2D) = "bump" {}
        _DetailNormalScale ("Detail Normal Scale", Range(0, 2)) = 1
        _DetailNormalTiling ("Detail Normal Tiling Offset", Vector) = (1, 1, 0, 0)
        _DetailNormalRotate ("Detail Normal Rotate", Range(0, 5)) = 0

        [NoScaleOffset] _MaskMap ("Mask Map (R Metallic, G Occlusion, B Height, A Roughness)", 2D) = "white" {}
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Occlusion ("Occlusion", Range(0, 1)) = 1
        _Roughness ("Roughness", Range(0, 1)) = 1
        _IOR ("IOR", Range(1, 2)) = 1.01
        _RoughnessRefractionWeight ("Roughness Refraction Weight", Range(0, 1)) = 1
        _Reflectance ("Reflectance", Range(0, 1)) = 1

        _EmissiveMap ("Emissive Map", 2D) = "white" {}
        [HDR] _EmissiveColor ("Emissive Color", Color) = (0, 0, 0, 1)
        _EmissiveTillingPanner ("Emissive Tiling Panner", Vector) = (1, 1, 0, 0)
        [Toggle] _EmissiveUseViewSpaceUV ("Use View Space UV", Float) = 0
        _ViewSpaceUVNormalIntensity ("View Space UV Normal Intensity", Range(0, 1)) = 0

        [NoScaleOffset] _ParallaxMap ("Parallax Map (R Layer 1, G Layer 2, B Layer 3)", 2D) = "black" {}
        _ParallaxStrength ("Parallax Strength (Layer 1, Layer 2, Layer 3)", Vector) = (-0.05, -0.1, -0.2, 0)
        [HDR] _ParallaxColor1 ("Layer 1 Color", Color) = (1, 1, 1, 1)
        _ParallaxBrightness1 ("Layer 1 Brightness", Range(0, 20)) = 7
        _ParallaxTilingOffset1 ("Layer 1 Tiling Offset", Vector) = (1, 1, 0, 0)
        _ParallaxFlowSpeed1 ("Layer 1 Flow Speed (UV/s)", Vector) = (0, 0, 0, 0)
        _ParallaxBaseColorBlend1 ("Layer 1 Base Color Blend", Range(0, 1)) = 0.5
        [Toggle] _UseObjectSpaceParallax1 ("Layer 1 Use Object Space", Float) = 0
        [HDR] _ParallaxColor2 ("Layer 2 Color", Color) = (1, 1, 1, 1)
        _ParallaxBrightness2 ("Layer 2 Brightness", Range(0, 20)) = 7
        _ParallaxTilingOffset2 ("Layer 2 Tiling Offset", Vector) = (1, 1, 0, 0)
        _ParallaxFlowSpeed2 ("Layer 2 Flow Speed (UV/s)", Vector) = (0, 0, 0, 0)
        _ParallaxBaseColorBlend2 ("Layer 2 Base Color Blend", Range(0, 1)) = 0.5
        [Toggle] _UseObjectSpaceParallax2 ("Layer 2 Use Object Space", Float) = 0
        [HDR] _ParallaxColor3 ("Layer 3 Color", Color) = (1, 1, 1, 1)
        _ParallaxBrightness3 ("Layer 3 Brightness", Range(0, 20)) = 1
        _ParallaxTilingOffset3 ("Layer 3 Tiling Offset", Vector) = (1, 1, 0, 0)
        _ParallaxFlowSpeed3 ("Layer 3 Flow Speed (UV/s)", Vector) = (0, 0, 0, 0)
        _ParallaxBaseColorBlend3 ("Layer 3 Base Color Blend", Range(0, 1)) = 0.5
        [Toggle] _UseObjectSpaceParallax3 ("Layer 3 Use Object Space", Float) = 0

        _Weight ("Transmission Weight", Range(0, 1)) = 1
        [NoScaleOffset] _TransmissionColorMap ("Transmission Mask (R Thickness, G Weight)", 2D) = "white" {}
        [HDR] _TransmissionColor ("Transmission Color", Color) = (1, 1, 1, 1)
        _MFPScale ("MFP Scale", Range(0.001, 100)) = 1
        _Thickness ("Thickness", Range(0, 100)) = 1
        _PhaseAniso ("Phase Aniso", Range(-1, 1)) = 0.9

        [Toggle(BURT_ALPHA_CLIP)] _AlphaClip ("Alpha Clip", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [Toggle] _Refraction ("Refraction", Float) = 1
        [Toggle] _ZWrite ("Transparent ZWrite", Float) = 1
        [ToggleUI] _ResponsiveAA ("Responsive AA", Float) = 1
        [ToggleUI] _IgnoreFog ("Ignore Global Fog", Float) = 0
        [Enum(Off,0,Front,1,Back,2)] _Cull ("Cull", Float) = 0
        [Enum(None,0,Flip,1,Mirror,2)] _DoubleSidedNormalMode ("Double Sided Normal Mode", Float) = 1
        [HideInInspector] _DoubleSidedNormalModeConstants ("Double Sided Normal Mode Constants", Vector) = (-1, -1, -1, 0)
        _TransparentSortPriority ("Transparent Sort Priority", Range(-2, 2)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "BurtRenderPipeline" }

        Pass
        {
            Name "Burt Clear Crystal Shadow Caster"
            Tags { "LightMode" = "ShadowCaster" }

            ColorMask 0
            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex VertClearCrystalShadow
            #pragma fragment FragClearCrystalShadow
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma target 3.5
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtClearCrystalPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Clear Crystal Refraction Distortion"
            Tags { "LightMode" = "BurtRefractionDistortion" }

            ZWrite Off
            ZTest LEqual
            Cull [_Cull]
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex VertClearCrystal
            #pragma fragment FragClearCrystalDistortion
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP
            #pragma multi_compile_instancing
            #pragma target 3.5
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtClearCrystalPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Clear Crystal Forward"
            Tags { "LightMode" = "BurtForward" }

            ZWrite [_ZWrite]
            ZTest LEqual
            Cull [_Cull]
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex VertClearCrystal
            #pragma fragment FragClearCrystal
            #pragma multi_compile_fragment _ BURT_MAIN_LIGHT_PCF_3 BURT_MAIN_LIGHT_PCF_7
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP
            #pragma shader_feature_local _ BURT_IGNORE_FOG
            #pragma multi_compile_instancing
            #pragma target 3.5
            #define BURT_TRANSPARENT_VERTEX_FOG 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtClearCrystalPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Clear Crystal Transparent Motion Vectors"
            Tags { "LightMode" = "BurtTransparentMotionVectors" }
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex VertClearCrystalMotionVector
            #pragma fragment FragClearCrystalMotionVector
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP
            #pragma multi_compile_instancing
            #pragma target 3.5
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtClearCrystalPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Clear Crystal Responsive AA Mask"
            Tags { "LightMode" = "BurtResponsiveAAMask" }
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex VertClearCrystalMotionVector
            #pragma fragment FragClearCrystalResponsiveAAMask
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP
            #pragma multi_compile_instancing
            #pragma target 3.5
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtClearCrystalPass.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "Burt.RenderPipeline.Editor.BurtClearCrystalShaderGUI"
    Fallback Off
}
