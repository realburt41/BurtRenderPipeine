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
        _MaskMap ("Mask Map (R Metallic, G Occlusion, B 3S Curvature, A Smoothness)", 2D) = "white" {}

        // 定义切线空间法线贴图，Forward 光照会用它改变每个片元的世界空间法线。
        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}

        // 定义法线贴图强度，0 表示退回几何法线，1 表示使用贴图原始强度。
        _NormalScale ("Normal Scale", Range(0, 2)) = 1

        // 定义 XRender / Frostbite 风格的介质反射率，0.5 会映射到常见非金属 F0=0.04。
        // 定义金属度，0 表示非金属介质，1 表示金属材质。
        // 定义材质光滑度，数值越高高光越小越锐利。
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5

        // 定义环境遮蔽强度，0 表示忽略 Mask Map 的 G 通道，1 表示完全使用 G 通道。
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1
        _SubsurfaceThickness ("Subsurface Thickness", Range(0, 1)) = 0.5
        [NoScaleOffset] _SubsurfaceThicknessMap ("Subsurface Thickness Map", 2D) = "white" {}
        _SubsurfacePower ("Subsurface Power", Range(0.5, 8)) = 3
        _SubsurfaceDistortion ("Subsurface Distortion", Range(0, 1)) = 0.35
        _SubsurfaceAmbient ("Subsurface Ambient", Range(0, 1)) = 0.35
        [Enum(5S Burley, 0, 4S Separable, 1, 3S Preintegrated, 2)] _SubsurfaceScatteringMode ("SSS Algorithm", Float) = 0
        _SubsurfaceProfileIndex ("Subsurface Profile Index", Range(0, 7)) = 0
        _Subsurface3SCurvatureScale ("3S Curvature Scale", Range(0, 2)) = 0.5
        _Subsurface3SCurvatureBias ("3S Curvature Bias", Range(0, 1)) = 0
        [Toggle(BURT_SKINNED_DECAL)] _SkinnedDecalEnabled ("Enable Skinned Decal", Float) = 0
        [HideInInspector] _BurtSkinnedDecalProjectionDebug ("Skinned Decal Projection Debug", Float) = 0
        [HideInInspector] _BurtSkinnedDecalEntryDebug ("Skinned Decal Entry Debug", Float) = 0
        [HideInInspector] _BurtSkinnedDecalUseMeshPosition ("Skinned Decal Use Mesh Position", Float) = 0
        [IntRange] _SkinnedDecalPluginModel_DecalCount ("Skinned Decal Count", Range(0, 5)) = 1
        [NoScaleOffset] _SkinnedDecalPluginModel_DecalAlbedo ("Skinned Decal Albedo", 2D) = "black" {}
        [NoScaleOffset] _SkinnedDecalPluginModel_DecalNormal ("Skinned Decal Normal", 2D) = "bump" {}
        [NoScaleOffset] _SkinnedDecalPluginModel_DecalMOHR ("Skinned Decal MOHR", 2D) = "black" {}
        _SkinnedDecalPluginModel_DecalArrayIndexSize1 ("Skinned Decal 1 (Unused, Size dm, Albedo Multiply, Normal Scale)", Vector) = (0, 1, 0, 1)
        _SkinnedDecalPluginModel_DecalTint1 ("Skinned Decal Tint 1", Color) = (1, 1, 1, 1)
        _SkinnedDecalPluginModel_DecalPosition1 ("Skinned Decal Position 1", Vector) = (0, 0, 0, 0)
        _SkinnedDecalPluginModel_DecalBasisX1 ("Skinned Decal Basis X 1", Vector) = (0, 0, 0, 0)
        _SkinnedDecalPluginModel_DecalBasisY1 ("Skinned Decal Basis Y 1", Vector) = (0, 0, 0, 0)
        _SkinnedDecalPluginModel_DecalArraySizeIndex2 ("Skinned Decal 2 (Unused, Size dm, Albedo Multiply, Normal Scale)", Vector) = (0, 1, 0, 1)
        _SkinnedDecalPluginModel_DecalTint2 ("Skinned Decal Tint 2", Color) = (1, 1, 1, 1)
        _SkinnedDecalPluginModel_DecalPosition2 ("Skinned Decal Position 2", Vector) = (0, 0, 0, 0)
        _SkinnedDecalPluginModel_DecalBasisX2 ("Skinned Decal Basis X 2", Vector) = (0, 0, 0, 0)
        _SkinnedDecalPluginModel_DecalBasisY2 ("Skinned Decal Basis Y 2", Vector) = (0, 0, 0, 0)
        _SkinnedDecalPluginModel_DecalArraySizeIndex3 ("Skinned Decal 3 (Unused, Size dm, Albedo Multiply, Normal Scale)", Vector) = (0, 1, 0, 1)
        _SkinnedDecalPluginModel_DecalTint3 ("Skinned Decal Tint 3", Color) = (1, 1, 1, 1)
        _SkinnedDecalPluginModel_DecalPosition3 ("Skinned Decal Position 3", Vector) = (0, 0, 0, 0)
        _SkinnedDecalPluginModel_DecalBasisX3 ("Skinned Decal Basis X 3", Vector) = (0, 0, 0, 0)
        _SkinnedDecalPluginModel_DecalBasisY3 ("Skinned Decal Basis Y 3", Vector) = (0, 0, 0, 0)
        _SkinnedDecalPluginModel_DecalArraySizeIndex4 ("Skinned Decal 4 (Unused, Size dm, Albedo Multiply, Normal Scale)", Vector) = (0, 1, 0, 1)
        _SkinnedDecalPluginModel_DecalTint4 ("Skinned Decal Tint 4", Color) = (1, 1, 1, 1)
        _SkinnedDecalPluginModel_DecalPosition4 ("Skinned Decal Position 4", Vector) = (0, 0, 0, 0)
        _SkinnedDecalPluginModel_DecalBasisX4 ("Skinned Decal Basis X 4", Vector) = (0, 0, 0, 0)
        _SkinnedDecalPluginModel_DecalBasisY4 ("Skinned Decal Basis Y 4", Vector) = (0, 0, 0, 0)
        _SkinnedDecalPluginModel_DecalArraySizeIndex5 ("Skinned Decal 5 (Unused, Size dm, Albedo Multiply, Normal Scale)", Vector) = (0, 1, 0, 1)
        _SkinnedDecalPluginModel_DecalTint5 ("Skinned Decal Tint 5", Color) = (1, 1, 1, 1)
        _SkinnedDecalPluginModel_DecalPosition5 ("Skinned Decal Position 5", Vector) = (0, 0, 0, 0)
        _SkinnedDecalPluginModel_DecalBasisX5 ("Skinned Decal Basis X 5", Vector) = (0, 0, 0, 0)
        _SkinnedDecalPluginModel_DecalBasisY5 ("Skinned Decal Basis Y 5", Vector) = (0, 0, 0, 0)
        // 定义自发光贴图，Forward 光照会把它作为不受灯光影响的颜色叠加到最终结果。
        _EmissionMap ("Emission Map", 2D) = "white" {}

        // 定义自发光颜色，RGB 表示自发光颜色和强度。
        [HDR]_EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)

        // Enables cutout rendering when set to 1 so every Lit pass discards pixels below the same alpha threshold.
        [Toggle(BURT_ALPHA_CLIP)] _AlphaClip ("Alpha Clip", Float) = 0

        // Stores the alpha cutoff threshold used by DepthNormals, GBuffer, and ShadowCaster to keep color, depth, and shadows consistent.
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5

        [HideInInspector] _Surface ("Surface Type", Float) = 0
        [HideInInspector] _BlendMode ("Transparent Blend Mode", Float) = 0
        [HideInInspector] _DoubleSidedEnable ("Double Sided", Float) = 0
        [HideInInspector] _DoubleSidedNormalMode ("Double Sided Normal Mode", Float) = 0
        [HideInInspector] _DoubleSidedNormalModeConstants ("Double Sided Normal Mode Constants", Vector) = (1, 1, 1, 0)
        [HideInInspector] _Cull ("Cull", Float) = 2
        [HideInInspector] _SrcBlend ("Source Blend", Float) = 1
        [HideInInspector] _DstBlend ("Destination Blend", Float) = 0
        [HideInInspector] _ZWrite ("ZWrite", Float) = 1
        [HideInInspector] _ZTest ("ZTest", Float) = 4
        [ToggleUI] _ResponsiveAA ("Responsive AA", Float) = 0
        [HideInInspector] _BurtGBufferStencilRef ("GBuffer Stencil Ref", Float) = 64
        [HideInInspector] _BurtGBufferStencilReadMask ("GBuffer Stencil Read Mask", Float) = 224
        [HideInInspector] _BurtGBufferStencilWriteMask ("GBuffer Stencil Write Mask", Float) = 224
        [HideInInspector] _MotionVectorsStencilRef ("Motion Vectors Stencil Ref", Float) = 8
        [HideInInspector] _MotionVectorsStencilMask ("Motion Vectors Stencil Mask", Float) = 8
    }

    // Defines the runtime SubShader used by BurtRP.
    SubShader
    {
        // Marks this shader as a BurtRP opaque shader so materials are easy to identify.
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "BurtRenderPipeline" }
        UsePass "Hidden/Burt Render Pipeline/GI Voxelize/BurtGIVoxelize"

        Pass
        {
            Name "Burt Subsurface Motion Vectors"
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
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtSubsurfaceProperties.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMotionVectorPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Subsurface Responsive AA Mask"
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
            #define BURT_MOTION_VECTOR_RESPONSIVE_AA_MASK 1
            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtSubsurfaceProperties.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMotionVectorPass.hlsl"
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
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtSubsurfaceProperties.hlsl"

            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtShadowCasterPass.hlsl"

            // Ends the HLSL program for this pass.
            ENDHLSL
        }

        Pass
        {
            Name "Burt Subsurface Depth Normals"
            Tags { "LightMode" = "BurtDepthNormals" }
            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex VertGBuffer
            #pragma fragment FragDepthNormals
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP
            #pragma shader_feature_local_fragment _ _EMISSION
            #pragma shader_feature_local _ BURT_PRESKIN_POSITION_PACKED
            #pragma shader_feature_local _ BURT_SKINNED_DECAL
            #pragma multi_compile _ XSKIN_MESH_COMPRESSED
            #pragma target 4.5

            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtSubsurfaceProperties.hlsl"

            #define BURT_MATERIAL_SHADING_MODEL_SUBSURFACE 1
            #define BURT_USE_PRESKIN_POSITION 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtDepthNormalsPass.hlsl"
            ENDHLSL
        }

        // 定义 Deferred 第一版使用的 GBuffer 写入 pass，只负责输出材质数据，不在这里做光照。
        Pass
        {
            // 给 Frame Debugger 显示一个明确的名字，方便和 Forward/DepthOnly 区分。
            Name "Burt Lit GBuffer"

            // 主 Agent 的 Draw GBuffer Opaque 会用 ShaderTagId("BurtGBuffer") 精确匹配这个 pass。
            Tags { "LightMode" = "BurtGBuffer" }

            // DepthNormals prepass owns CameraDepth and GBuffer0 normal/roughness; GBuffer only fills the remaining MRTs.
            ZWrite Off

            // Require exact prepass depth so retained normals/depth and GBuffer material payload describe the same visible surface.
            ZTest Equal
            // Keep MRT0 from the prepass instead of recomputing normal/roughness in the GBuffer pass.
            ColorMask 0 0

            // Deferred stencil layout matches XRender high bits: 64 = Subsurface.
            Stencil
            {
                Ref [_BurtGBufferStencilRef]
                ReadMask [_BurtGBufferStencilReadMask]
                WriteMask [_BurtGBufferStencilWriteMask]
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
            #pragma shader_feature_local_fragment _ _EMISSION
            #pragma shader_feature_local _ BURT_PRESKIN_POSITION_PACKED
            #pragma shader_feature_local _ BURT_SKINNED_DECAL
            #pragma multi_compile _ XSKIN_MESH_COMPRESSED

            // MRT 输出 SV_Target0..4，显式要求 shader target 3.0，避免低目标平台不支持多渲染目标。
            #pragma target 4.5

            // 引入 Lit 材质 CBUFFER，让 GBuffer、DepthOnly、ShadowCaster、Forward 使用同一套材质属性布局。
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtSubsurfaceProperties.hlsl"

            #define BURT_MATERIAL_SHADING_MODEL_SUBSURFACE 1
            #define BURT_USE_PRESKIN_POSITION 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtGBufferPass.hlsl"

            // 结束 GBuffer pass 的 HLSL 程序。
            ENDHLSL
        }

        Pass
        {
            Name "Burt Subsurface Forward"
            Tags { "LightMode" = "BurtSubsurfaceForward" }

            ZWrite Off
            ZTest Equal
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex VertGBuffer
            #pragma fragment FragSubsurfaceForward
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP
            #pragma shader_feature_local_fragment _ _EMISSION
            #pragma shader_feature_local _ BURT_PRESKIN_POSITION_PACKED
            #pragma shader_feature_local _ BURT_SKINNED_DECAL
            #pragma multi_compile _ XSKIN_MESH_COMPRESSED
            #pragma target 4.5

            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtSubsurfaceProperties.hlsl"

            #define BURT_MATERIAL_SHADING_MODEL_SUBSURFACE 1
            #define BURT_USE_PRESKIN_POSITION 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtGBufferPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Burt Subsurface Forward Preview"
            Tags { "LightMode" = "BurtForward" }

            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Blend [_SrcBlend] [_DstBlend]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ BURT_MAIN_LIGHT_PCF_3 BURT_MAIN_LIGHT_PCF_7
            #pragma shader_feature_local _ BURT_MATERIAL_TRANSPARENT
            #pragma shader_feature_local_fragment _ BURT_ALPHA_CLIP
            #pragma shader_feature_local_fragment _ _EMISSION
            #pragma shader_feature_local _ BURT_PRESKIN_POSITION_PACKED
            #pragma shader_feature_local _ BURT_SKINNED_DECAL
            #pragma multi_compile _ XSKIN_MESH_COMPRESSED
            #pragma shader_feature_fragment _ BURT_USE_DEBUG_MODE_FORWARD
            #pragma multi_compile_instancing
            #pragma target 4.5

            #define BURT_MATERIAL_SHADING_MODEL_SUBSURFACE 1
            #define BURT_USE_PRESKIN_POSITION 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtSubsurfaceProperties.hlsl"
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
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtSubsurfaceProperties.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtGIRayTracingLit.hlsl"
            ENDHLSL
        }

    }

    CustomEditor "Burt.RenderPipeline.Editor.BurtLitShaderGUI"

    // Disables fallback so BurtRP shader errors do not silently use another pipeline shader.
    Fallback Off
}
