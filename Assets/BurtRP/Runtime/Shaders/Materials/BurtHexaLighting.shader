Shader "BurtRP/Hexa Lighting"
{
    Properties
    {
        [NoScaleOffset] _PositiveAxesLightmap ("Lightmap-P", 2D) = "black" {}
        [NoScaleOffset] _NegativeAxesLightmap ("Lightmap-N", 2D) = "black" {}
        _Rows ("Columns", Float) = 8.0
        _Columns ("Rows", Float) = 8.0
        _PlaySpeed ("Play Speed", Float) = 1.0

        [NoScaleOffset] _MotionVectorMap ("Motion Vector Map", 2D) = "black" {}
        _MotionVectorScale ("Motion Vector Scale", Float) = 0.002

        [HDR] _BasicColor ("Basic Color", Color) = (1, 1, 1, 1)
        _Density ("Density", Range(0.05, 2.0)) = 0.8
        _OverallAlpha ("Overall Alpha", Range(0, 1.0)) = 0.4

        [Enum(Off,0,Front,1,Back,2)] _Cull ("Cull", Float) = 2
        [Toggle] _ZWrite ("ZWrite", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }

        Pass
        {
            Name "Burt Hexa Lighting Forward"
            Tags { "LightMode" = "BurtForward" }

            Blend One OneMinusSrcAlpha
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtHexaLightingPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Hexa Lighting Transparent Motion Vectors"
            Tags { "LightMode" = "BurtTransparentMotionVectors" }

            ZWrite Off
            ZTest LEqual
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
            #pragma target 3.5
            #pragma vertex VertMotionVector
            #pragma fragment FragMotionVector
            #pragma multi_compile_instancing
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtHexaLightingPass.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "Burt.RenderPipeline.Editor.BurtHexaLightingShaderGUI"
    Fallback Off
}
