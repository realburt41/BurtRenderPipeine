Shader "BurtRP/Hair"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _RootColor ("Root Color", Color) = (1, 1, 1, 1)
        _RootGradient ("Root Gradient", Vector) = (0, 1, 0, 0)
        [Toggle] _RootGradientEnable ("Root Gradient Enable", Float) = 0
        [Toggle] _RootGradientReverse ("Root Gradient Reverse", Float) = 0
        [Toggle] _RootGradientPosEnable ("Use Position Gradient", Float) = 0
        _GradientDirection ("Position Gradient Direction", Vector) = (1, 0, 0, 0)
        _GradientPosOffset ("Position Gradient Offset", Vector) = (0, 0, 0, 0)

        _MaskMap ("Mask Map (R Reflectance, G Occlusion, B Height)", 2D) = "white" {}
        _IDMap ("ID Map", 2D) = "white" {}
        _IDXTilling ("ID X Tiling", Range(0, 10)) = 1
        _IDIntensity ("ID Strand Intensity", Float) = 0
        _TangentA ("Tangent A", Vector) = (0, 0, 1, 0)
        _TangentB ("Tangent B", Vector) = (0, 0, -1, 0)

        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalScale ("Normal Scale", Range(0, 4)) = 1

        _HairShadowFillStrength ("Shadow Fill Strength", Range(0, 1)) = 0
        _ShadowCutOff ("Shadow Cutoff", Range(0, 1)) = 0
        _OpacityMaskValue ("Opacity Mask Value", Range(0, 1)) = 0.33
        _OpacityMaskOffset ("Opacity Mask Offset", Range(0, 1)) = 0

        _ScatterUseFullRange ("Scatter Use Full Range", Float) = 0
        _Scatter ("Avatar Scatter", Range(0, 0.33)) = 0
        _ScatterFull ("Avatar Scatter Full", Range(0, 1)) = 0
        _Occlusion ("Avatar AO", Range(0, 1)) = 1
        _AlbedoOcclusion ("Albedo AO", Range(0, 1)) = 0
        _AlbedoOcclusionColor ("Albedo AO Color", Color) = (0, 0, 0, 0)
        _BackLightIntensity ("Back Light Intensity", Range(0, 1)) = 1
        _BackLightMask ("Back Light Mask", Range(0, 1)) = 0.5
        _BackLightMaskRange ("Back Light Mask Range", Range(0, 2)) = 1
        _Reflectance ("Reflectance", Range(0, 1)) = 0.5

        _HairBrightColor ("Structure Bright Color", Color) = (1, 1, 1, 1)
        _HairBrightIntensity ("Structure Bright Intensity", Range(1, 3)) = 2
        _HairShadowColor ("Structure Shadow Color", Color) = (1, 1, 1, 1)
        _HairShadowIntensity ("Structure Shadow Intensity", Range(0, 1)) = 0
        _HairShadowPower ("Structure Shadow Power", Range(0, 2)) = 0

        _HairRotate ("Hair Rotate", Range(-1, 1)) = 0
        _SpecularShift ("Specular Shift", Range(-2, 2)) = 0.5
        _SecondarySpecularShift ("Secondary Specular Shift", Range(-2, 2)) = 0.9
        _RoughParameter ("Primary Secondary Edge Roughness", Vector) = (0.5, 0.5, 1, 0)
        _EdgeRoughRimPower ("Edge Rough Rim Power", Float) = 4
        [HDR]_SpecularColor ("Specular Color", Color) = (1, 1, 1, 1)
        [HDR]_SpecularSecondColor ("Secondary Specular Color", Color) = (1, 1, 1, 1)

        [Toggle] _GradientColorEnable ("Gradient Color Enable", Float) = 0
        _GradientMap ("Gradient Map", 2D) = "gray" {}
        _GradientRowIndex ("Gradient Row Index", Float) = 0
        _GradientSoftLight ("Gradient Soft Light", Range(0, 1)) = 0
        _GradientOverlay ("Gradient Overlay", Range(0, 1)) = 0
        _GradientReplace ("Gradient Replace", Range(0, 1)) = 0

        _HairScatter ("Legacy Hair Scatter", Range(0, 1)) = 0.25
        _HairScatterBoost ("Legacy Hair Scatter Boost", Range(0, 1)) = 0
        _HairSpecularScale ("Hair Specular Scale", Range(0, 2)) = 0.85
        _HairShiftScale ("Legacy Hair Shift Scale", Range(0, 1)) = 1
        _HairRoughnessOffset ("Legacy Hair Roughness Offset", Range(0, 0.35)) = 0.05
        [Toggle] _HairTangentFlip ("Flip Strand Direction", Float) = 0

        _Smoothness ("Legacy Smoothness", Range(0, 1)) = 0.85
        _OcclusionStrength ("Legacy Occlusion Strength", Range(0, 1)) = 1
        _EmissionMap ("Emission Map", 2D) = "white" {}
        [HDR]_EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)

        [Toggle(BURT_ALPHA_CLIP)] _AlphaClip ("Alpha Clip", Float) = 1
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.33
        [ToggleUI] _ResponsiveAA ("Responsive AA", Float) = 0

        [HideInInspector] _DoubleSidedEnable ("Double Sided", Float) = 1
        [HideInInspector] _DoubleSidedNormalMode ("Double Sided Normal Mode", Float) = 2
        [HideInInspector] _DoubleSidedNormalModeConstants ("Double Sided Normal Mode Constants", Vector) = (1, 1, -1, 0)
        [HideInInspector] _Cull ("Cull", Float) = 0
        [HideInInspector] _BurtGBufferStencilRef ("GBuffer Stencil Ref", Float) = 96
        [HideInInspector] _BurtGBufferStencilReadMask ("GBuffer Stencil Read Mask", Float) = 224
        [HideInInspector] _BurtGBufferStencilWriteMask ("GBuffer Stencil Write Mask", Float) = 224
        [HideInInspector] _MotionVectorsStencilRef ("Motion Vectors Stencil Ref", Float) = 8
        [HideInInspector] _MotionVectorsStencilMask ("Motion Vectors Stencil Mask", Float) = 8
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "BurtRenderPipeline" }
        Cull [_Cull]

        Pass
        {
            Name "Burt Hair Depth Only"
            Tags { "LightMode" = "BurtDepthOnly" }
            ColorMask 0
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex VertDepth
            #pragma fragment FragDepth
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP
            #pragma multi_compile_instancing
            #pragma target 3.5

            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtHairProperties.hlsl"

            #define BURT_MATERIAL_SHADING_MODEL_HAIR 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtDepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Hair Shadow Caster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex VertShadow
            #pragma fragment FragShadow
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP
            #pragma multi_compile_instancing
            #pragma target 3.5
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtHairProperties.hlsl"

            #define BURT_MATERIAL_SHADING_MODEL_HAIR 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Hair Motion Vectors"
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

            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtHairProperties.hlsl"
            #define BURT_MATERIAL_SHADING_MODEL_HAIR 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMotionVectorPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Hair Responsive AA Mask"
            Tags { "LightMode" = "BurtResponsiveAAMask" }
            ZWrite Off
            ZTest Equal
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex VertMotionVector
            #pragma fragment FragResponsiveAAMask
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP
            #pragma multi_compile_instancing
            #pragma target 3.5
            #define BURT_MATERIAL_SHADING_MODEL_HAIR 1
            #define BURT_MOTION_VECTOR_RESPONSIVE_AA_MASK 1
            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtHairProperties.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMotionVectorPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Hair Depth Normals"
            Tags { "LightMode" = "BurtDepthNormals" }
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex VertGBuffer
            #pragma fragment FragDepthNormals
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP
            #pragma shader_feature_local_fragment _ _EMISSION
            #pragma multi_compile_instancing
            #pragma target 4.5

            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtHairProperties.hlsl"

            #define BURT_MATERIAL_SHADING_MODEL_HAIR 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtDepthNormalsPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Hair GBuffer"
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

            HLSLPROGRAM
            #pragma vertex VertGBuffer
            #pragma fragment FragGBuffer
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP
            #pragma shader_feature_local_fragment _ _EMISSION
            #pragma multi_compile_instancing
            #pragma target 4.5

            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtHairProperties.hlsl"

            #define BURT_MATERIAL_SHADING_MODEL_HAIR 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtGBufferPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Hair Forward"
            Tags { "LightMode" = "BurtForward" }
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP
            #pragma shader_feature_local_fragment _ _EMISSION
            #pragma shader_feature_fragment _ BURT_USE_DEBUG_MODE_FORWARD
            #pragma multi_compile_instancing
            #pragma target 3.5

            #define BURT_MATERIAL_SHADING_MODEL_HAIR 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtHairProperties.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtForwardPass.hlsl"
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
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtHairProperties.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtGIRayTracingLit.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "Burt.RenderPipeline.Editor.BurtHairShaderGUI"
    Fallback Off
}
