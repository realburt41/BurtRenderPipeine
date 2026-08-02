Shader "BurtRP/Eye"
{
    Properties
    {
        [HideInInspector] _BaseMap ("Base Map", 2D) = "white" {}
        [HideInInspector] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _MaskMap ("Mask Map", 2D) = "white" {}

        _ScalebyCenter ("Eye Scale By Center", Float) = 1
        _PupilScale ("Pupil Scale", Range(0, 2)) = 1
        _LimbusScale ("Limbus Scale", Float) = 1
        _LimbusPow ("Limbus Power", Float) = 1
        [Toggle] _InverseUV ("Inverse UV", Float) = 0

        [HDR] _IrisColor ("Iris Color", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _IrisColorMap ("Iris Color Map", 2D) = "white" {}
        _IrisColorRotate ("Iris Rotate", Float) = 0
        _IrisColorRotateSpeed ("Iris Rotate Speed", Float) = 0
        _IOR ("IOR", Float) = 1.33
        _IrisRadius ("Iris Radius", Range(0, 0.5)) = 0
        _IrisMaskBlurIntensity ("Iris Mask Blur", Vector) = (0, 1, 0.045, 0)
        _IrisFrontDirectionOS ("Iris Front Direction (Object Space)", Vector) = (0, 0, 1, 0)
        _IrisFrontHemisphereFade ("Iris Front Hemisphere Fade", Range(0, 0.5)) = 0.05
        _IrisConcavityScale ("Iris Caustic Scale", Range(0, 4)) = 0
        _IrisConcavityPow ("Iris Caustic Power", Range(0.1, 0.5)) = 0.1

        [NoScaleOffset] [Normal] _NormalMap ("Cornea Normal Map", 2D) = "bump" {}
        _NormalMapScale ("Normal Scale", Float) = 1
        [HideInInspector] _NormalScale ("Shared Normal Scale", Float) = 1
        _CorneaSpecular ("Cornea Reflectance", Range(0, 1)) = 1
        _CorneaRoughness ("Cornea Roughness", Range(0, 1)) = 0.1

        [HDR] _ScleraColor ("Sclera Color", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _ScleraMap ("Sclera Map", 2D) = "white" {}
        _ScleraSpecular ("Sclera Reflectance", Range(0, 1)) = 1
        _ScleraRoughness ("Sclera Roughness", Range(0, 1)) = 0.1

        [NoScaleOffset] _EmissiveMap ("Emissive Map", 2D) = "white" {}
        [HDR] _EyeEmissiveColor ("Emissive Color", Color) = (0, 0, 0, 1)

        [NoScaleOffset] [Normal] _EyeDirectionMap ("Eye Direction Map", 2D) = "bump" {}
        [NoScaleOffset] _MidPlaneHeightMap ("Mid Plane Height Map", 2D) = "white" {}
        _IrisDepthScale ("Iris Depth Scale", Float) = 1

        [NoScaleOffset] _Matcap ("Matcap", 2D) = "black" {}
        [HDR] _MatcapColor ("Matcap Color", Color) = (1, 1, 1, 1)
        _MatcapSizeOffset ("Matcap Size Offset", Vector) = (1, 0, 0, 0)

        [HideInInspector] _EmissionColor ("Shared Emission Color", Color) = (0, 0, 0, 1)
        [HideInInspector] _EmissionMap ("Shared Emission Map", 2D) = "white" {}
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
        [HideInInspector] _BurtGBufferStencilRef ("GBuffer Stencil Ref", Float) = 32
        [HideInInspector] _BurtGBufferStencilReadMask ("GBuffer Stencil Read Mask", Float) = 224
        [HideInInspector] _BurtGBufferStencilWriteMask ("GBuffer Stencil Write Mask", Float) = 224
        [HideInInspector] _MotionVectorsStencilRef ("Motion Vectors Stencil Ref", Float) = 8
        [HideInInspector] _MotionVectorsStencilMask ("Motion Vectors Stencil Mask", Float) = 8
        [ToggleUI] _ResponsiveAA ("Responsive AA", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "BurtRenderPipeline" }
        UsePass "Hidden/Burt Render Pipeline/GI Voxelize/BurtGIVoxelize"

        Pass
        {
            Name "Burt Eye Depth Only"
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
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtEyeProperties.hlsl"

            #define BURT_MATERIAL_SHADING_MODEL_EYE 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtDepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Eye Motion Vectors"
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
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtEyeProperties.hlsl"

            #define BURT_MATERIAL_SHADING_MODEL_EYE 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMotionVectorPass.hlsl"
            ENDHLSL
        }

        // The cornea contains thin refractive and specular detail. Keep this history
        // control signal independent from native depth-stencil availability.
        Pass
        {
            Name "Burt Eye Responsive AA Mask"
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

            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtEyeProperties.hlsl"

            #define BURT_MATERIAL_SHADING_MODEL_EYE 1
            #define BURT_MOTION_VECTOR_RESPONSIVE_AA_MASK 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMotionVectorPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Eye Shadow Caster"
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
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtEyeProperties.hlsl"

            #define BURT_MATERIAL_SHADING_MODEL_EYE 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Eye Depth Normals"
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

            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtEyeProperties.hlsl"

            #define BURT_MATERIAL_SHADING_MODEL_EYE 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtDepthNormalsPass.hlsl"
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
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtEyeProperties.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtGIRayTracingLit.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Eye Forward"
            Tags { "LightMode" = "BurtForwardOnly" }
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Cull [_Cull]
            Blend [_SrcBlend] [_DstBlend]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP
            #pragma shader_feature_local_fragment _ _EMISSION
            #pragma shader_feature_fragment _ BURT_USE_DEBUG_MODE_FORWARD
            #pragma multi_compile_instancing
            #pragma target 3.5

            #define BURT_MATERIAL_SHADING_MODEL_EYE 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtEyeProperties.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtForwardPass.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "Burt.RenderPipeline.Editor.BurtEyeShaderGUI"
    Fallback Off
}
