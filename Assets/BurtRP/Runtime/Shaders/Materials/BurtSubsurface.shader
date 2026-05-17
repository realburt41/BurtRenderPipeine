// Defines the Shader menu path for the first BurtRP lit material model.
Shader "BurtRP/Subsurface"
{
    // Defines material properties shown in Unity's Inspector.
    Properties
    {
        // Defines the main albedo texture sampled by the forward Lit pass from mesh UV0.
        _BaseMap ("Base Map", 2D) = "white" {}

        // Defines the surface tint multiplied by the sampled Base Map before lighting.
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        // 定义 PBR Mask Map：R=金属度，G=环境遮蔽，B=预留，A=光滑度。
        _MaskMap ("Mask Map (R Metallic, G Occlusion, A Smoothness)", 2D) = "white" {}

        // 定义切线空间法线贴图，Forward 光照会用它改变每个片元的世界空间法线。
        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}

        // 定义法线贴图强度，0 表示退回几何法线，1 表示使用贴图原始强度。
        _NormalScale ("Normal Scale", Range(0, 2)) = 1

        // 定义 XRender / Frostbite 风格的介质反射率，0.5 会映射到常见非金属 F0=0.04。
        _Reflectance ("Reflectance", Range(0, 1)) = 0.5

        // 定义金属度，0 表示非金属介质，1 表示金属材质。
        _Metallic ("Metallic", Range(0, 1)) = 0

        _Anisotropy ("Anisotropy", Range(-1, 1)) = 0

        // 定义材质光滑度，数值越高高光越小越锐利。
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5

        // 定义环境遮蔽强度，0 表示忽略 Mask Map 的 G 通道，1 表示完全使用 G 通道。
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1
        _SubsurfaceStrength ("Subsurface Strength", Range(0, 1)) = 0
        _SubsurfaceThickness ("Subsurface Thickness", Range(0, 1)) = 0.5
        _SubsurfacePower ("Subsurface Power", Range(0.5, 8)) = 3
        _SubsurfaceDistortion ("Subsurface Distortion", Range(0, 1)) = 0.35
        _SubsurfaceAmbient ("Subsurface Ambient", Range(0, 1)) = 0.35
        [HDR]_SubsurfaceTint ("Subsurface Tint", Color) = (1, 0.45, 0.32, 1)

        // 定义自发光贴图，Forward 光照会把它作为不受灯光影响的颜色叠加到最终结果。
        _EmissionMap ("Emission Map", 2D) = "white" {}

        // 定义自发光颜色，RGB 表示自发光颜色和强度。
        [HDR]_EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)

        // Enables cutout rendering when set to 1 so every Lit pass discards pixels below the same alpha threshold.
        [Toggle(BURT_ALPHA_CLIP)] _AlphaClip ("Alpha Clip", Float) = 0

        // Stores the alpha cutoff threshold used by Forward, DepthOnly, and ShadowCaster to keep color, depth, and shadows consistent.
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
    }

    // Defines the runtime SubShader used by BurtRP.
    SubShader
    {
        // Marks this shader as a BurtRP opaque shader so materials are easy to identify.
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "BurtRenderPipeline" }

        // Defines the depth-only pass used by Burt Depth Prepass.
        Pass
        {
            // Names this pass for Frame Debugger readability.
            Name "Burt Lit Depth Only"

            // Matches BurtDepthPrepass because BurtRP looks for this LightMode.
            Tags { "LightMode" = "BurtDepthOnly" }

            // Disables color writes so this pass only affects CameraDepth.
            ColorMask 0

            // Enables depth writes so opaque lit objects can populate CameraDepth.
            ZWrite On

            // Uses less-equal depth testing, matching the forward color pass.
            ZTest LEqual

            // Applies the ShaderGUI resolved culling mode so depth follows double-sided Lit materials.
            Cull [_Cull]

            // Starts the HLSL program for this pass.
            HLSLPROGRAM

            // Declares the depth vertex shader entry point.
            #pragma vertex VertDepth

            // Declares the depth fragment shader entry point.
            #pragma fragment FragDepth
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP

            // Includes Unity helper functions such as UnityObjectToClipPos.
            #include "UnityCG.cginc"

            // 引入 BurtRP Lit 统一材质 CBUFFER，让 DepthOnly、ShadowCaster、Forward 的 SRP Batcher 布局完全一致。
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtLitProperties.hlsl"

            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtDepthOnlyPass.hlsl"

            // Ends the HLSL program for this pass.
            ENDHLSL
        }

        // Defines the shadow-caster pass used by Burt Draw Main Light Shadow Caster.
        Pass
        {
            // Names this pass for Frame Debugger readability.
            Name "Burt Lit Shadow Caster"

            // Uses Unity's standard ShadowCaster LightMode because ScriptableRenderContext.DrawShadows searches for this tag.
            Tags { "LightMode" = "ShadowCaster" }

            // Disables color writes because shadow maps only need depth.
            ColorMask 0

            // Enables depth writes so this pass can populate the main-light shadow map.
            ZWrite On

            // Uses less-equal depth testing, matching the other opaque depth passes.
            ZTest LEqual

            // Applies the ShaderGUI resolved culling mode so shadows follow double-sided Lit materials.
            Cull [_Cull]

            // Starts the HLSL program for this pass.
            HLSLPROGRAM

            // Declares the shadow vertex shader entry point.
            #pragma vertex VertShadow

            // Declares the shadow fragment shader entry point.
            #pragma fragment FragShadow
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP

            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            // Includes Unity helper functions, including UnityObjectToClipPos which uses the current light view-projection matrix.
            #include "UnityCG.cginc"

            // 引入 BurtRP Lit 统一材质 CBUFFER，让 ShadowCaster 和其它 Lit pass 使用同一份材质字段顺序。
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtLitProperties.hlsl"

            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtShadowCasterPass.hlsl"

            // Ends the HLSL program for this pass.
            ENDHLSL
        }

        // 定义 Deferred 第一版使用的 GBuffer 写入 pass，只负责输出材质数据，不在这里做光照。
        Pass
        {
            // 给 Frame Debugger 显示一个明确的名字，方便和 Forward/DepthOnly 区分。
            Name "Burt Lit GBuffer"

            // 主 Agent 的 Draw GBuffer Opaque 会用 ShaderTagId("BurtGBuffer") 精确匹配这个 pass。
            Tags { "LightMode" = "BurtGBuffer" }

            // 当前 Deferred 计划允许没有 Depth Prepass 时由 GBuffer pass 写深度，和已有 DepthOnly 的 LEqual 行为保持一致。
            ZWrite On

            // 如果前面已经跑过 Depth Prepass，LEqual 会让等深度片元通过；如果没跑过，也能正常建立 CameraDepth。
            ZTest LEqual

            // Deferred stencil layout: 3 = Subsurface. Deferred Lighting has a matching Ref 3 pass.
            Stencil
            {
                Ref 3
                ReadMask 3
                WriteMask 3
                Comp Always
                Pass Replace
            }

            // Applies the ShaderGUI resolved culling mode so deferred Lit supports double-sided materials.
            Cull [_Cull]

            // 开始 GBuffer pass 的 HLSL 程序。
            HLSLPROGRAM

            // 声明 GBuffer 顶点 shader 入口。
            #pragma vertex VertGBuffer

            // 声明 GBuffer 片元 shader 入口。
            #pragma fragment FragGBuffer
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP

            // MRT 输出 SV_Target0..4，显式要求 shader target 3.0，避免低目标平台不支持多渲染目标。
            #pragma target 3.0

            // 引入 Lit 材质 CBUFFER，让 GBuffer、DepthOnly、ShadowCaster、Forward 使用同一套材质属性布局。
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtLitProperties.hlsl"

            #define BURT_MATERIAL_SHADING_MODEL_SUBSURFACE 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtGBufferPass.hlsl"

            // 结束 GBuffer pass 的 HLSL 程序。
            ENDHLSL
        }

        // Defines the forward color pass used by Burt Draw Opaque and Burt Draw Transparent.
        Pass
        {
            // Names this pass for Frame Debugger readability.
            Name "Burt Lit Forward"

            // Matches BurtForward because BurtRP's main draw passes now only render this LightMode.
            Tags { "LightMode" = "BurtForward" }

            // Enables depth writes for opaque forward rendering.
            ZWrite [_ZWrite]

            // Uses less-equal depth testing so pixels that match the prepass depth still draw.
            ZTest [_ZTest]

            // Applies the ShaderGUI resolved culling mode for Lit forward rendering.
            Cull [_Cull]

            // Lets the ShaderGUI switch the same Lit pass between opaque and alpha blended rendering.
            Blend [_SrcBlend] [_DstBlend]

            // Starts the HLSL program for this pass.
            HLSLPROGRAM

            // Declares the forward vertex shader entry point.
            #pragma vertex Vert

            // Declares the forward fragment shader entry point.
            #pragma fragment Frag
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP

            // Uses explicit LOD cubemap sampling through UnityCG in BurtLighting.hlsl.
            #pragma target 3.0
            #pragma multi_compile_fragment _ BURT_SHADING_DEBUG

            // Selects the shared Forward shading model before BurtLighting.hlsl is included.
            #define BURT_MATERIAL_SHADING_MODEL_SUBSURFACE 1

            // Includes the material CBUFFER first so the shared pass can read the same SRP Batcher layout.
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtLitProperties.hlsl"

            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtForwardPass.hlsl"

            // Ends the HLSL program for this pass.
            ENDHLSL
        }
    }

    CustomEditor "Burt.RenderPipeline.Editor.BurtLitShaderGUI"

    // Disables fallback so BurtRP shader errors do not silently use another pipeline shader.
    Fallback Off
}


