Shader "BurtRP/Easy Fog"
{
    Properties
    {
        [NoScaleOffset] [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalScale ("Normal Scale", Range(0, 5)) = 1

        _OpacityMap ("Opacity Map", 2D) = "white" {}
        [HDR] _BaseColorTink ("Base Color Tint", Color) = (1, 1, 1, 1)
        _FogIntensity ("Fog Density", Range(0, 15)) = 0.5

        [HideInInspector] _EmissiveColor ("Emissive Color", Color) = (1, 1, 1, 1)
        _EmissiveIntensity ("Emissive Intensity", Range(0, 30)) = 1

        _CameraFadingDistance ("Camera Fading Distance", Float) = 50
        _DepthFadeDistance ("Depth Fade Distance", Range(0, 20)) = 5
        _DepthFadePower ("Depth Fade Power", Range(0.5, 10)) = 1

        [Toggle] _EnableBillboard ("Billboard", Float) = 0

        _Flowmap ("Flowmap", 2D) = "white" {}
        _FlowmapSpeed ("Flowmap Speed", Float) = 0.2
        _FlowmapIntensity ("Flowmap Intensity", Float) = 0.2

        [HideInInspector] _Surface ("Surface Type", Float) = 1
        [HideInInspector] _SrcBlend ("Source Blend", Float) = 1
        [HideInInspector] _DstBlend ("Destination Blend", Float) = 10
        [HideInInspector] _ZWrite ("ZWrite", Float) = 0
        [HideInInspector] _ZTest ("ZTest", Float) = 4
        [HideInInspector] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "BurtRenderPipeline" }

        Pass
        {
            Name "Burt Easy Fog Forward"
            Tags { "LightMode" = "BurtForward" }

            Blend [_SrcBlend] [_DstBlend], [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex BurtEasyFogVert
            #pragma fragment BurtEasyFogFrag
            #pragma multi_compile_instancing
            #pragma target 3.5

            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtEasyFogPass.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "Burt.RenderPipeline.Editor.BurtEasyFogShaderGUI"
    Fallback Off
}
