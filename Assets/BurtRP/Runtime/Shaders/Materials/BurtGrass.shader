Shader "BurtRP/Grass"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        [HDR] _BaseColor ("Base Color", Color) = (0.72, 0.9, 0.48, 1)
        [HDR] _BaseColorTip ("Tip Overlay Color", Color) = (0.5, 0.5, 0.5, 1)
        _TipMaskPow ("Tip Mask Power", Range(0.1, 10)) = 3
        _MaskMap ("Mask Map (R Unused, G Occlusion, B Thickness, A Roughness)", 2D) = "white" {}
        _AlphaMap ("Alpha Map", 2D) = "white" {}
        _AlphaIncrease ("Alpha Distance Increase", Range(0, 8)) = 4
        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalScale ("Normal Scale", Range(0, 2)) = 1
        _Reflectance ("Reflectance", Range(0, 1)) = 0.32
        _Smoothness ("Smoothness", Range(0, 1)) = 0.35
        _Roughness ("Grass Roughness", Range(0, 1)) = 0.81
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1
        [HDR] _FoliageTransmissionColor ("Transmission Color", Color) = (0.45, 0.9, 0.28, 1)
        _FoliageTransmissionWeight ("SSS Intensity", Range(0, 1)) = 0.28
        _FoliageThickness ("Thickness", Range(0, 1)) = 0.35
        _FoliageBackLight ("Back Light Wrap", Range(0, 1)) = 0.7
        _FoliageSubsurfaceColorSaturate ("Transmission Saturation", Range(0, 2)) = 0.35
        [HDR] _SubsurfaceColor ("SSS Tint", Color) = (1, 1, 1, 1)
        _SubsurfaceColorSaturate ("SSS Saturation", Range(0, 2)) = 0.35
        _RoughnessScale ("Roughness Scale", Range(0, 2)) = 1
        _ReflectanceScale ("Foliage Specular Scale", Range(0, 1)) = 0.5
        _ThicknessScale ("Thickness Scale", Range(0, 1)) = 1
        _TransmissionNdotL ("Transmission NdotL", Range(0, 1)) = 0.5
        _SSSIntensity ("Base SSS Intensity", Range(0, 1)) = 0.18
        _FresnelIntensity ("Fresnel SSS Intensity", Range(0, 5)) = 1
        _FresnelExp ("Fresnel Exp", Range(0, 0.5)) = 0.35
        _Specular ("Grass Specular", Range(0, 1)) = 0.5
        _HeightAO ("Height AO", Range(0, 1)) = 0
        _HeightAOFallOff ("Height AO Falloff", Range(0, 100)) = 30
        _TLNormalWeight ("Terrain Light Normal Weight", Range(1, 3)) = 1
        _SSShadowIntensity ("Screen Space Shadow Intensity", Range(0, 3)) = 1
        _SSShadowDistance ("Screen Space Shadow Distance", Range(10, 200)) = 30
        _GroundFadeIntensity ("Ground Fade Intensity", Range(0, 1)) = 0
        _NoiseMap ("Noise Map", 2D) = "white" {}
        _VariationIntensity01 ("Variation R Intensity", Range(0, 5)) = 0
        _VariationIntensity02 ("Variation G Intensity", Range(0, 5)) = 0
        _Variation01Height ("Variation R Height", Range(0.1, 0.9)) = 0.5
        _Variation02Height ("Variation G Height", Range(0.1, 0.9)) = 0.5
        [HDR] _Variation01 ("Variation R Overlay", Color) = (1, 1, 1, 1)
        [HDR] _Variation02 ("Variation G Overlay", Color) = (1, 1, 1, 1)
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
            Name "Burt Grass Depth Only"
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
            #define BURT_MATERIAL_SHADING_MODEL_FOLIAGE 1
            #define BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS 1
            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtLitProperties.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtDepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Grass Shadow Caster"
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
            #define BURT_MATERIAL_SHADING_MODEL_FOLIAGE 1
            #define BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS 1
            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtLitProperties.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Grass Motion Vectors"
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
            #define BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS 1
            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtLitProperties.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMotionVectorPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Grass GBuffer"
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
            #define BURT_MATERIAL_SHADING_MODEL_FOLIAGE 1
            #define BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtGBufferPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Grass Forward"
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
            #define BURT_MATERIAL_SHADING_MODEL_FOLIAGE 1
            #define BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtLitProperties.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtForwardPass.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "Burt.RenderPipeline.Editor.BurtLitShaderGUI"
    Fallback Off
}
