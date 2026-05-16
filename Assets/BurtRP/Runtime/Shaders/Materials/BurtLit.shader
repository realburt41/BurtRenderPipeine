// Defines the Shader menu path for the first BurtRP lit material model.
Shader "BurtRP/Lit"
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

        // 定义材质光滑度，数值越高高光越小越锐利。
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5

        // 定义环境遮蔽强度，0 表示忽略 Mask Map 的 G 通道，1 表示完全使用 G 通道。
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1

        // 定义自发光贴图，Forward 光照会把它作为不受灯光影响的颜色叠加到最终结果。
        _EmissionMap ("Emission Map", 2D) = "white" {}

        // 定义自发光颜色，RGB 表示自发光颜色和强度。
        [HDR]_EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)

        // Enables cutout rendering when set to 1 so every Lit pass discards pixels below the same alpha threshold.
        [Toggle] _AlphaClip ("Alpha Clip", Float) = 0

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

            // Includes Unity helper functions such as UnityObjectToClipPos.
            #include "UnityCG.cginc"

            // Includes shared Base Map sampling and alpha-clip helpers so DepthOnly uses the same material logic as Forward.
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInput.hlsl"

            // 引入 BurtRP Lit 统一材质 CBUFFER，让 DepthOnly、ShadowCaster、Forward 的 SRP Batcher 布局完全一致。
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtLitProperties.hlsl"

            // Defines the mesh input needed for depth rendering.
            struct DepthAttributes
            {
                // Reads object-space vertex position from the mesh.
                float4 positionOS : POSITION;

                // Reads mesh UV0 so DepthOnly can sample the same Base Map alpha that the Forward pass uses for cutout decisions.
                float2 uv0 : TEXCOORD0;
            };

            // Defines the vertex-to-fragment data for depth rendering.
            struct DepthVaryings
            {
                // Stores clip-space position for rasterization.
                float4 positionCS : SV_POSITION;

                // Stores Base Map UVs after material tiling and offset so the fragment can alpha-clip before writing depth.
                float2 baseMapUV : TEXCOORD0;
            };

            // Converts object-space depth vertices into clip space.
            DepthVaryings VertDepth(DepthAttributes input)
            {
                // Creates the output structure that will be returned to the GPU pipeline.
                DepthVaryings output;

                // Transforms the object-space vertex position into clip-space position.
                output.positionCS = UnityObjectToClipPos(input.positionOS);

                // Applies the same Base Map tiling and offset as Forward so cutout depth follows the visible texture exactly.
                output.baseMapUV = BurtTransformBaseMapUV(input.uv0, _BaseMap_ST);

                // Returns the transformed vertex data.
                return output;
            }

            // Runs for each depth fragment and discards cutout pixels before the depth value is committed.
            float4 FragDepth(DepthVaryings input) : SV_Target
            {
                // Samples Base Map and material tint exactly like Forward so invisible cutout texels do not write CameraDepth.
                float4 baseColor = BurtSampleBaseMap(input.baseMapUV) * _BaseColor;

                // Applies the shared alpha-clip rule before returning so rejected fragments leave no depth behind.
                BurtApplyAlphaClip(baseColor.a, _AlphaClip, _Cutoff);

                // Returns a dummy color because this pass writes only depth after the alpha-clip decision succeeds.
                return 0;
            }

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

            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            // Includes Unity helper functions, including UnityObjectToClipPos which uses the current light view-projection matrix.
            #include "UnityCG.cginc"

            // Includes shared Base Map sampling and alpha-clip helpers so ShadowCaster matches the Forward visible silhouette.
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInput.hlsl"

            // 引入 BurtRP Lit 统一材质 CBUFFER，让 ShadowCaster 和其它 Lit pass 使用同一份材质字段顺序。
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtLitProperties.hlsl"

            // 保存当前 request 的主光方向，ShadowCaster 顶点偏移需要用它计算法线和光向夹角。
            float4 _BurtMainLightDirection;
            float4 _BurtShadowCasterLightPosition;
            float _BurtCastingPunctualLightShadow;
            float3 _LightDirection;
            float3 _LightPosition;
            float4 _ShadowBias;

            // 保存 C# 已折算到世界单位的 normal bias，ShadowCaster 只在顶点阶段使用它。
            float _BurtMainLightShadowDepthBias;
            float _BurtMainLightShadowNormalBias;

            // Defines the mesh input needed for shadow rendering.
            struct ShadowAttributes
            {
                // Reads object-space vertex position from the mesh.
                float4 positionOS : POSITION;

                // 读取模型空间法线，ShadowCaster normal bias 需要沿世界法线推开顶点。
                float3 normalOS : NORMAL;

                // Reads mesh UV0 so ShadowCaster can sample the same Base Map alpha that the Forward pass uses for cutout decisions.
                float2 uv0 : TEXCOORD0;
            };

            // Defines the vertex-to-fragment data for shadow rendering.
            struct ShadowVaryings
            {
                // Stores clip-space position for shadow-map rasterization.
                float4 positionCS : SV_POSITION;

                // Stores Base Map UVs after material tiling and offset so the fragment can alpha-clip before writing shadow depth.
                float2 baseMapUV : TEXCOORD0;
            };

            // 根据法线和主光方向计算 ShadowCaster 顶点的世界空间 normal bias。
            float3 ApplyBurtShadowCasterNormalBias(float4 positionOS, float3 normalOS)
            {
                // 先把顶点转到世界空间再偏移，避免非等比缩放时模型空间距离不一致。
                float3 positionWS = mul(unity_ObjectToWorld, positionOS).xyz;

                // 法线和光向必须处在同一世界空间，才能正确判断表面是否处于掠射角。
                float3 normalWS = UnityObjectToWorldNormal(normalOS);
                normalWS *= rsqrt(max(dot(normalWS, normalWS), 0.000001f));

                float3 lightDirectionWS = _LightDirection;
                if (dot(lightDirectionWS, lightDirectionWS) <= 0.000001f)
                {
                    lightDirectionWS = _BurtMainLightDirection.xyz;
                }

#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                lightDirectionWS = _BurtShadowCasterLightPosition.xyz - positionWS;
                if (dot(lightDirectionWS, lightDirectionWS) <= 0.000001f)
                {
                    lightDirectionWS = _LightPosition - positionWS;
                }
#else
                if (_BurtCastingPunctualLightShadow > 0.5f)
                {
                    lightDirectionWS = _BurtShadowCasterLightPosition.xyz - positionWS;
                }
#endif

                lightDirectionWS *= rsqrt(max(dot(lightDirectionWS, lightDirectionWS), 0.000001f));

                float depthBias = _ShadowBias.x;
                float normalBias = _ShadowBias.y;
                if (abs(depthBias) <= 0.0000001f && abs(normalBias) <= 0.0000001f)
                {
                    depthBias = _BurtMainLightShadowDepthBias;
                    normalBias = _BurtMainLightShadowNormalBias;
                }

                // 表面越接近掠射角越容易出现 self-shadow，所以用 1 - NdotL 放大法线偏移。
                float normalBiasScale = (1.0f - saturate(dot(normalWS, lightDirectionWS))) * normalBias;

                // 沿世界法线推出 caster 顶点，让 shadow map 深度和接收面错开一小段距离。
                return positionWS + lightDirectionWS * depthBias + normalWS * normalBiasScale;
            }

            // Converts object-space vertices into the current light clip space.
            ShadowVaryings VertShadow(ShadowAttributes input)
            {
                // Creates the output structure that will be returned to the GPU pipeline.
                ShadowVaryings output;

                // 在进入主光裁剪空间前先应用 normal bias，这样偏移会真实写进 shadow map 深度。
                float3 biasedPositionWS = ApplyBurtShadowCasterNormalBias(input.positionOS, input.normalOS);

                // 使用 BurtDrawMainLightShadowCasterPass 设置的主光 VP 矩阵，把偏移后的世界坐标写入 shadow map。
                output.positionCS = mul(UNITY_MATRIX_VP, float4(biasedPositionWS, 1.0f));

                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                // Applies the same Base Map tiling and offset as Forward so cast shadows match the visible cutout silhouette.
                output.baseMapUV = BurtTransformBaseMapUV(input.uv0, _BaseMap_ST);

                // Returns the transformed vertex data.
                return output;
            }

            // Runs for each shadow fragment and discards cutout pixels before the shadow-map depth value is committed.
            float4 FragShadow(ShadowVaryings input) : SV_Target
            {
                // Samples Base Map and material tint exactly like Forward so transparent cutout texels do not cast shadows.
                float4 baseColor = BurtSampleBaseMap(input.baseMapUV) * _BaseColor;

                // Applies the shared alpha-clip rule before returning so rejected fragments leave no shadow-map depth behind.
                BurtApplyAlphaClip(baseColor.a, _AlphaClip, _Cutoff);

                // Returns a dummy color because this pass writes only shadow depth after the alpha-clip decision succeeds.
                return 0;
            }

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

            // Deferred stencil layout: 0 = Default Lit. Keeping Lit at 0 matches the cleared stencil background and leaves Hair to mark 1.
            Stencil
            {
                Ref 0
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

            // MRT 输出 SV_Target0/1/2，显式要求 shader target 3.0，避免低目标平台不支持多渲染目标。
            #pragma target 3.0

            // 引入 Unity 基础变换和 normal map 解包函数。
            #include "UnityCG.cginc"

            // 引入 BurtRP 通用数学工具，例如 BurtSafeNormalize。
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"

            // 引入材质贴图采样、Mask Map 合成和 alpha clip 规则，保证 GBuffer 与 Forward 的材质输入一致。
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInput.hlsl"

            // 引入法线贴图工具，GBuffer 保存的 normalWS 必须和 Forward shading 使用同一条转换路径。
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtNormal.hlsl"

            // 引入自发光工具，GBuffer2.rgb 会保存 Forward 最终叠加前的 emission 输入。
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtEmission.hlsl"

            // 引入 BurtRP 三张 GBuffer 的 encode/decode 约定；出处参考 XRender SlabGBufferPass.hlsl -> GBufferPack(SlabParams) 的分层做法。
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBuffer.hlsl"

            // 引入 Lit 材质 CBUFFER，让 GBuffer、DepthOnly、ShadowCaster、Forward 使用同一套材质属性布局。
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtLitProperties.hlsl"

            // GBuffer pass 的 mesh 输入，字段和 Forward 保持一致，避免法线贴图或 alpha clip 取不到必要数据。
            struct GBufferAttributes
            {
                // 读取模型空间位置，用来输出裁剪空间位置并写入共享深度。
                float4 positionOS : POSITION;

                // 读取模型空间法线，作为法线贴图 TBN 的 N 轴基础。
                float3 normalOS : NORMAL;

                // 读取模型空间切线，法线贴图需要切线和 handedness 重建 TBN。
                float4 tangentOS : TANGENT;

                // 读取 UV0，Base/Mask/Emission/Normal 当前都从这套 mesh UV 派生。
                float2 uv0 : TEXCOORD0;
            };

            // 顶点到片元的数据，尽量只传 GBuffer 编码真正需要的字段。
            struct GBufferVaryings
            {
                // 保存裁剪空间位置，供光栅化和深度写入使用。
                float4 positionCS : SV_POSITION;

                // 保存插值后的世界空间几何法线，片元阶段会和 normal map 合成最终 normalWS。
                float3 normalWS : TEXCOORD0;

                // 保存 Base Map UV，片元阶段用于采样 baseColor 和 alpha。
                float2 baseMapUV : TEXCOORD1;

                // 保存世界空间切线和副切线符号，片元阶段用于 normal map 转世界空间。
                float4 tangentWS : TEXCOORD2;

                // 保存 Mask Map UV，片元阶段用于采样 metallic、occlusion、smoothness。
                float2 maskMapUV : TEXCOORD3;

                // 保存 Emission Map UV，片元阶段用于采样自发光颜色。
                float2 emissionMapUV : TEXCOORD4;
            };

            // 三张 MRT 的片元输出；出处参考 XRender SlabGBufferDefine.hlsl::FGBufferOutput 使用 SV_TargetN 显式绑定。
            struct GBufferFragmentOutput
            {
                // SV_Target0 对应 _BurtGBuffer0，保存 baseColor.rgb 和 occlusion.a。
                float4 gbuffer0 : SV_Target0;

                // SV_Target1 对应 _BurtGBuffer1，保存 oct normal.rg、packed shadingModel/material.b 和 smoothness.a。
                float4 gbuffer1 : SV_Target1;

                // SV_Target2 对应 _BurtGBuffer2，保存 emission.rgb 和 reflectance.a。
                float4 gbuffer2 : SV_Target2;
            };

            // 把 mesh 顶点转换成 GBuffer 片元阶段需要的数据。
            GBufferVaryings VertGBuffer(GBufferAttributes input)
            {
                // 创建输出结构体，后面逐项填充，避免未初始化字段进入片元阶段。
                GBufferVaryings output;

                // 把模型空间位置转换到裁剪空间，GBuffer pass 会用它进行光栅化和深度测试。
                output.positionCS = UnityObjectToClipPos(input.positionOS);

                // 把模型空间法线转换到世界空间，并安全归一化，作为 normal map 扰动前的基础法线。
                output.normalWS = BurtSafeNormalize(UnityObjectToWorldNormal(input.normalOS));

                // 把模型空间切线转换到世界空间，并保留 handedness，保证法线贴图方向和 Forward 一致。
                output.tangentWS = BurtObjectToWorldTangent(input.tangentOS);

                // 按 Base Map 自己的 Tiling/Offset 转换 UV，保证 alpha clip 和 Forward 可见轮廓一致。
                output.baseMapUV = BurtTransformBaseMapUV(input.uv0, _BaseMap_ST);

                // 按 Mask Map 自己的 Tiling/Offset 转换 UV，保证 GBuffer 中的 PBR 参数来自同一套材质规则。
                output.maskMapUV = BurtTransformMaskMapUV(input.uv0, _MaskMap_ST);

                // 按 Emission Map 自己的 Tiling/Offset 转换 UV，保证 GBuffer2.rgb 和 Forward emission 输入一致。
                output.emissionMapUV = BurtTransformEmissionMapUV(input.uv0, _EmissionMap_ST);

                // 返回准备好的插值数据。
                return output;
            }

            // 片元阶段采样材质输入并打包到三张 GBuffer。
            GBufferFragmentOutput FragGBuffer(GBufferVaryings input, fixed facing : VFACE)
            {
                // 采样 Base Map 并乘材质颜色，GBuffer0.rgb 保存的就是这份未光照 baseColor。
                float4 baseColor = BurtSampleBaseMap(input.baseMapUV) * _BaseColor;

                // 应用和 DepthOnly/ShadowCaster/Forward 完全相同的 alpha clip，避免 GBuffer 写入不可见镂空区域。
                BurtApplyAlphaClip(baseColor.a, _AlphaClip, _Cutoff);

                // 采样 normal map 并转换成世界空间，GBuffer1.rg 会保存这条最终 shading 法线的压缩结果。
                float3 normalWS = BurtSampleNormalWS(input.baseMapUV, input.normalWS, input.tangentWS, _NormalScale, facing, _DoubleSidedNormalModeConstants);

                // 采样 Mask Map，R/G/A 分别参与 metallic、occlusion 和 smoothness 的最终计算。
                float4 maskMap = BurtSampleMaskMap(input.maskMapUV);

                // Build Default Lit surface data; Hair now lives in the standalone BurtRP/Hair shader.
                BurtSurfaceData surfaceData = BurtCreateSurfaceData(baseColor, _Reflectance, _Smoothness, _Metallic, maskMap, _OcclusionStrength);

                // 采样自发光输入，GBuffer2.rgb 直接保存 emission，让 Deferred Lighting 最后再叠加。
                float3 emissionColor = BurtEvaluateEmission(input.emissionMapUV, _EmissionColor.rgb);

                // 把 surfaceData、最终 normalWS 和 emission 整理成语义化 GBuffer 数据。
                BurtGBufferData gbufferData = BurtCreateGBufferData(surfaceData, normalWS, emissionColor);

                // 出处参考 XRender SM_DefaultLit.GBuffer.hlsl::Pack_GBuffer_PC_High_DefaultLit，把 SlabParams 拆分写入多个 MRT；这里用 BurtEncodeGBuffer 固化 BurtRP 自己的三张 RT 布局。
                BurtEncodedGBuffer encodedGBuffer = BurtEncodeGBuffer(gbufferData);

                // 创建 MRT 输出结构，按 SV_Target0/1/2 顺序写入。
                GBufferFragmentOutput output;

                // 写入 _BurtGBuffer0：baseColor.rgb + occlusion.a。
                output.gbuffer0 = encodedGBuffer.gbuffer0;

                // 写入 _BurtGBuffer1：oct normal.rg + packed shadingModel/material.b + smoothness.a。
                output.gbuffer1 = encodedGBuffer.gbuffer1;

                // 写入 _BurtGBuffer2：emission.rgb + reflectance.a。
                output.gbuffer2 = encodedGBuffer.gbuffer2;

                // 返回三张 GBuffer 颜色，RenderGraph 侧绑定的 MRT 顺序必须和这里保持一致。
                return output;
            }

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

            // 使用 Unity reflection probe 的 cubemap mip 采样，需要 shader target 3.0 支持显式 LOD。
            #pragma target 3.0

            // Includes Unity helper functions for transforms and normal conversion.
            #include "UnityCG.cginc"

            // Includes BurtRP common helper functions such as safe normalization.
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"

            // Includes BurtRP surface/input data structures used by the Lit forward pass.
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInput.hlsl"

            // 引入 BurtRP 法线贴图工具，Forward pass 会用它把切线空间法线转换到世界空间。
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtNormal.hlsl"

            // 引入 BurtRP 自发光工具，Forward pass 会用它采样自发光贴图并叠加最终颜色。
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtEmission.hlsl"

            // Includes BurtRP simple main-light diffuse and ambient lighting helpers.
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLighting.hlsl"

            // 引入 BurtRP GBuffer 编解码约定；这里只做 shader 侧 roundtrip debug，不绑定任何 RenderTarget 生命周期。
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBuffer.hlsl"

            // Includes BurtRP main-light shadow receiver helpers.
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtShadows.hlsl"

            // 引入 BurtRP shading debug 工具，Forward pass 会根据 Overlay 选择输出 Albedo、Normal、Smoothness、Metallic 或 Detail Lighting。
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtShadingDebug.hlsl"

            // 引入 BurtRP Lit 统一材质 CBUFFER，Forward pass 直接使用同一份 SRP Batcher 字段布局。
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtLitProperties.hlsl"

            // BurtLighting.hlsl and BurtShadows.hlsl declare BurtRP global lighting and shadow variables for this pass.

            // Defines the mesh input needed by the lit forward pass.
            struct Attributes
            {
                // Reads object-space vertex position from the mesh.
                float4 positionOS : POSITION;

                // Reads object-space vertex normal from the mesh.
                float3 normalOS : NORMAL;

                // 读取模型空间切线，法线贴图需要用切线、法线和副切线组成 TBN 矩阵。
                float4 tangentOS : TANGENT;

                // Reads the first mesh UV channel so the forward pass can sample the Base Map.
                float2 uv0 : TEXCOORD0;
            };

            // Defines the data passed from the vertex shader to the fragment shader.
            struct Varyings
            {
                // Stores clip-space position for rasterization.
                float4 positionCS : SV_POSITION;

                // Stores world-space normal for diffuse lighting.
                float3 normalWS : TEXCOORD0;

                // Stores Base Map UVs after applying material tiling and offset from _BaseMap_ST.
                float2 baseMapUV : TEXCOORD2;

                // 保存世界空间切线和 handedness，片元阶段会用它重建 TBN 矩阵。
                float4 tangentWS : TEXCOORD3;

                // 保存世界空间位置，片元阶段会用它计算从表面指向相机的视线方向。
                float3 positionWS : TEXCOORD4;

                // 保存自发光贴图 UV，片元阶段会用它采样 Emission Map。
                float2 emissionMapUV : TEXCOORD5;

                // 保存 Mask Map UV，片元阶段会用它采样 metallic、occlusion 和 smoothness。
                float2 maskMapUV : TEXCOORD6;
            };

            // Transforms mesh vertices and normals for the lit forward pass.
            Varyings Vert(Attributes input)
            {
                // Creates the output structure that will be returned to the GPU pipeline.
                Varyings output;

                // Transforms the object-space vertex position into clip-space position.
                output.positionCS = UnityObjectToClipPos(input.positionOS);

                // Transforms the object-space vertex position into world space for shadow projection.
                float4 positionWS = mul(unity_ObjectToWorld, input.positionOS);

                // 保存世界空间位置，后续 fragment 会用它计算 view direction。
                output.positionWS = positionWS.xyz;

                // Transforms the object-space normal into world space and normalizes it.
                output.normalWS = normalize(UnityObjectToWorldNormal(input.normalOS));

                // 把模型空间切线转换到世界空间，并保留 tangent.w 里的副切线方向信息。
                output.tangentWS = BurtObjectToWorldTangent(input.tangentOS);

                // Applies the material Base Map tiling and offset to mesh UV0 for fragment texture sampling.
                output.baseMapUV = BurtTransformBaseMapUV(input.uv0, _BaseMap_ST);

                // 按自发光贴图自己的 Tiling/Offset 转换 UV，允许 Emission 和 Base Map 使用不同缩放。
                output.emissionMapUV = BurtTransformEmissionMapUV(input.uv0, _EmissionMap_ST);

                // 按 Mask Map 自己的 Tiling/Offset 转换 UV，允许 PBR 打包贴图独立缩放。
                output.maskMapUV = BurtTransformMaskMapUV(input.uv0, _MaskMap_ST);

                // Returns the transformed vertex data.
                return output;
            }

            // Main-light shadow receiver sampling now lives in BurtShadows.hlsl so future PCF/cascade work has one owner.

            // Computes BurtRP's current minimal Lit model through ShaderLibrary helpers.
            float4 Frag(Varyings input, fixed facing : VFACE) : SV_Target
            {
                // Samples the Base Map with transformed mesh UV0 and multiplies it by the material tint before evaluating visibility.
                float4 baseColor = BurtSampleBaseMap(input.baseMapUV) * _BaseColor;

                // Applies the shared alpha-clip rule so Forward visibility matches DepthOnly and ShadowCaster silhouettes.
                BurtApplyAlphaClip(baseColor.a, _AlphaClip, _Cutoff);

                // 采样法线贴图并把切线空间法线转换成世界空间法线，后续光照会使用这个最终法线。
                float3 normalWS = BurtSampleNormalWS(input.baseMapUV, input.normalWS, input.tangentWS, _NormalScale, facing, _DoubleSidedNormalModeConstants);

                // 计算从当前片元指向相机的世界空间方向，Specular 高光需要知道观察方向。
                float3 viewDirectionWS = BurtSafeNormalize(_WorldSpaceCameraPos.xyz - input.positionWS);

                // 采样 Mask Map，R/G/A 分别参与 metallic、occlusion 和 smoothness 的最终计算。
                float4 maskMap = BurtSampleMaskMap(input.maskMapUV);

                // Build Default Lit surface data from scalar properties and Mask Map; Hair lives in BurtRP/Hair.
                BurtSurfaceData surfaceData = BurtCreateSurfaceData(baseColor, _Reflectance, _Smoothness, _Metallic, maskMap, _OcclusionStrength);

                // Samples the main-light shadow attenuation using the shared shadow receiver helper.
                float shadowAttenuation = BurtSampleMainLightShadow(input.positionWS);

                // Builds the current main light from BurtRP global lighting variables and this pixel's shadow value.
                BurtLight mainLight = BurtCreateMainLight(shadowAttenuation);

                // 为当前 shading 准备一份可按 Debug View 覆盖的 SurfaceData，正常渲染时它和原始 surfaceData 完全一致。
                BurtSurfaceData shadingSurfaceData = surfaceData;

                // 出处：XRender/Shaders/SlabDebug/SlabDebugEvaluator/SlabDebug.Evaluate.Prev.hlsl::DEBUGID_LIGHTING_DETAIL_LIGHTING；Detail Lighting 用 0.18 中灰替换 Base.Color 来观察光照细节。
                if (BurtIsShadingDebugEnabled() && BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DETAIL_LIGHTING))
                {
                    // 只替换参与 shading 的 BaseColor，不改原始 surfaceData，避免 Albedo / Reflectance 等材质 Debug 读到被覆盖后的值。
                    shadingSurfaceData.baseColor.rgb = float3(0.18f, 0.18f, 0.18f);
                }

                // Lit shader always uses Default Lit PBR; Hair uses the standalone BurtRP/Hair shader.
                BurtPBRShadingComponents pbrComponents = BurtEvaluatePBRShadingComponents(shadingSurfaceData, mainLight, normalWS, viewDirectionWS, input.positionWS);

                // 取出不含自发光的 PBR 总光照，后续 finalColor 会在它基础上叠加 Emission。
                float3 lightingColor = pbrComponents.lighting;

                // 采样自发光颜色，它不受灯光和阴影影响，会直接叠加到最终颜色。
                float3 emissionColor = BurtEvaluateEmission(input.emissionMapUV, _EmissionColor.rgb);

                // 先合成最终材质颜色，后续 FinalLighting Debug 可以直接观察后处理前的材质输出。
                float3 finalColor = lightingColor + emissionColor;

                // 用 Forward 当前片元数据做一次 GBuffer 编码再解码，提前验证 Deferred 后续会消费的材质/法线还原路径。
                BurtGBufferData debugGBufferSourceData = BurtCreateGBufferData(surfaceData, normalWS, float3(0.0f, 0.0f, 0.0f));

                // 按 BurtGBuffer.hlsl 顶部约定生成三张逻辑 GBuffer；这里只在 shader 内部 roundtrip，不写入真实 RT。
                BurtEncodedGBuffer debugEncodedGBuffer = BurtEncodeGBuffer(debugGBufferSourceData);

                // 从逻辑 GBuffer 解码回语义数据，Debug View 读取的是这份解码结果。
                BurtGBufferData debugDecodedGBufferData = BurtDecodeGBuffer(debugEncodedGBuffer);

                // 把解码后的 GBuffer 数据转成 PBRMaterialData，提前验证 Deferred shading 前的材质重建口径。
                BurtPBRMaterialData debugGBufferMaterialData = BurtPreparePBRMaterialData(debugDecodedGBufferData);

                // 创建 Shading Debug 数据结构，确保 Debug View 读取的就是当前片元真实渲染使用的数据。
                BurtShadingDebugData debugData;

                // 写入世界空间法线，NormalWS Debug View 会把它编码成颜色。
                debugData.normalWS = normalWS;

                // 写入 Detail Lighting 结果，DetailLighting Debug View 会显示中灰 BaseColor 下的光照细节。
                debugData.detailLightingColor = pbrComponents.lighting;

                // 写入直接漫反射结果，DirectDiffuse Debug View 会显示它。
                debugData.directDiffuseColor = pbrComponents.directDiffuse;

                // 写入直接高光结果，DirectSpecular Debug View 会显示它。
                debugData.directSpecularColor = pbrComponents.directSpecular;

                // 写入追加光直接光拆分，Additional Light Debug View 会显示它。
                debugData.additionalDiffuseColor = pbrComponents.additionalDiffuse;
                debugData.additionalSpecularColor = pbrComponents.additionalSpecular;
                debugData.additionalUnshadowedColor = BurtEvaluateAdditionalLightingUnshadowedDebug(shadingSurfaceData, normalWS, viewDirectionWS, input.positionWS);

                // 写入间接漫反射结果，IndirectDiffuse Debug View 会显示它。
                debugData.indirectDiffuseColor = pbrComponents.indirectDiffuse;

                // 写入间接高光结果，IndirectSpecular Debug View 会显示它。
                debugData.indirectSpecularColor = pbrComponents.indirectSpecular;

                // 写入主光阴影衰减，ShadowAttenuation Debug View 用它确认当前像素的阴影接收结果。
                debugData.shadowAttenuation = shadowAttenuation;
                debugData.additionalShadowAttenuation = BurtEvaluateAdditionalShadowAttenuationDebug(input.positionWS, normalWS);
                BurtFillAdditionalLightShadowProjectionDebugData(
                    input.positionWS,
                    normalWS,
                    debugData.additionalShadowFaceColor,
                    debugData.additionalShadowUVColor,
                    debugData.additionalShadowDepthColor,
                    debugData.additionalShadowDepthDeltaColor);

                BurtFillMainLightShadowShadingDebugData(
                    input.positionWS,
                    debugData.normalWS,
                    debugData.shadowCascadeColor,
                    debugData.shadowCascadeBlend,
                    debugData.shadowDistanceFade,
                    debugData.shadowPCSSRadius,
                    debugData.shadowReceiverDepthDelta,
                    debugData.shadowPCSSBlockerFraction);

                // 写入参与间接光遮蔽的 AO，AmbientOcclusion Debug View 用它确认 Mask Map G 和强度混合结果。
                debugData.ambientOcclusion = surfaceData.occlusion;

                // 写入自发光贡献，Emission Debug View 用它确认 Emission Map 和 HDR Emission Color 是否生效。
                debugData.emissionColor = emissionColor;

                // 写入最终材质光照，FinalLighting Debug View 用它对比 Forward 输出和后处理前画面。
                debugData.finalLightingColor = finalColor;

                // 写入材质 reflectance，Reflectance Debug View 会用它检查非金属反射率输入。
                debugData.reflectance = surfaceData.reflectance;

                // 写入材质感知粗糙度，Roughness Debug View 会显示 1 - smoothness 后的结果。
                debugData.perceptualRoughness = pbrComponents.perceptualRoughness;

                // 写入直接高光实际粗糙度，SpecularAARoughness Debug View 会显示 AA 后的结果。
                debugData.specularAARoughness = pbrComponents.specularAARoughness;

                // 写入直接高光能量补偿，便于检查 LUT.z 是否过亮或过暗。
                debugData.specularEnergyCompensation = pbrComponents.specularEnergyCompensation;

                // 写入间接高光能量补偿，便于检查 Reflection Probe 高光是否也对齐 XRender 补能。
                debugData.indirectSpecularEnergyCompensation = pbrComponents.indirectSpecularEnergyCompensation;

                // 写入 XRender EnergyPreservation，EnergyPreservation Debug View 会显示 diffuse 底层保能比例。
                debugData.energyPreservation = pbrComponents.energyPreservation;

                // 写入间接高光遮蔽项，保持和间接镜面反射一样的 AO、NdotV 和粗糙度输入。
                debugData.specularOcclusion = pbrComponents.specularOcclusion;

                // 写入 XRender DiffuseColor，DiffuseColor Debug View 会显示 metallic 扣除后的漫反射颜色。
                debugData.diffuseColor = pbrComponents.diffuseColor;

                // 写入直接 GGX D 项，DirectBRDFD Debug View 会缩放显示高 smoothness 下的 NDF 峰值。
                debugData.directBRDFD = pbrComponents.directBRDFD;

                // 写入直接 Smith Joint Visibility 项，用来检查几何遮蔽是否压暗高光。
                debugData.directBRDFVisibility = pbrComponents.directBRDFVisibility;

                // 写入直接 Schlick Fresnel 项，用来检查 F0 和视角输入。
                debugData.directBRDFFresnel = pbrComponents.directBRDFFresnel;

                // 写入直接 diffuse lobe，当前默认 Lambert，后续启用 Burley 时这里会同步变化。
                debugData.directDiffuseLobe = pbrComponents.directDiffuseLobe;

                // 写入未乘灯光颜色、NdotL 和阴影的直接 diffuse BRDF。
                debugData.directDiffuseBRDF = pbrComponents.directDiffuseBRDF;

                // 写入未乘灯光颜色、NdotL 和阴影的直接 specular BRDF。
                debugData.directSpecularBRDF = pbrComponents.directSpecularBRDF;

                // 写入 Specular AA 法线方差，SpecularAANormalVariance Debug View 会放大显示。
                debugData.specularAANormalVariance = pbrComponents.specularAANormalVariance;

                // 写入 Specular AA 增加的感知粗糙度，SpecularAARoughnessDelta Debug View 会放大显示。
                debugData.specularAARoughnessDelta = pbrComponents.specularAARoughnessDelta;

                // 写入间接高光 DFG.xy，IndirectSpecularDFG Debug View 会显示为 R/G 通道。
                debugData.indirectSpecularDFG = pbrComponents.indirectSpecularDFG;

                // 写入 F0/F90 应用 DFG 后的环境 BRDF，用来检查 Reflection Probe 前的 BRDF 权重。
                debugData.indirectSpecularEnvBRDF = pbrComponents.indirectSpecularEnvBRDF;

                // ?? Hair ?? lobe ???? Hair ??? shading core ? 0????? Debug View ???
                debugData.hairPrimaryLobe = pbrComponents.hairPrimaryLobe;
                debugData.hairSecondaryLobe = pbrComponents.hairSecondaryLobe;
                debugData.hairTransmissionLobe = pbrComponents.hairTransmissionLobe;
                debugData.hairScatter = pbrComponents.hairScatter;

                // 写入 GBuffer 解码后的 BaseColor，用来检查 GBuffer0.rgb 的材质颜色还原。
                debugData.gbufferBaseColor = debugDecodedGBufferData.baseColor;

                // 写入 GBuffer 解码后的世界空间法线，用来检查 octahedron normal 编码精度和方向。
                debugData.gbufferNormalWS = BurtGetDefaultLitNormalWS(debugDecodedGBufferData);

                // 写入 GBuffer 解码后的 Metallic，用来检查 GBuffer1.b 的材质还原。
                debugData.gbufferMetallic = BurtGetDefaultLitMetallic(debugDecodedGBufferData);

                // 写入 GBuffer 解码后的 Smoothness，用来检查 GBuffer1.a 的面板语义还原。
                debugData.gbufferSmoothness = debugDecodedGBufferData.smoothness;

                // 写入 GBuffer 解码后的 AO，用来检查 GBuffer0.a 的间接光遮蔽输入。
                debugData.gbufferOcclusion = debugDecodedGBufferData.occlusion;

                // 写入 GBuffer 解码后的 Reflectance，用来检查 GBuffer2.a 的 XRender reflectance 输入。
                debugData.gbufferReflectance = debugDecodedGBufferData.reflectance;

                // 写入从 GBuffer Smoothness 还原出的感知粗糙度，用来和 Forward Roughness 对照。
                debugData.gbufferRoughness = debugGBufferMaterialData.perceptualRoughness;

                // 写入从 GBuffer 重建出的 DiffuseColor，用来检查 metallic 对 diffuse 的扣除是否和 Forward 一致。
                debugData.gbufferDiffuseColor = debugGBufferMaterialData.diffuseColor;

                // 创建一个临时调试颜色变量，只有命中材质 debug 模式时才会被真正输出。
                float3 debugColor;

                // 如果 Overlay 选择了材质类 debug 模式，就直接输出调试颜色，避免自发光或后处理干扰观察。
                if (BurtTryEvaluateMaterialShadingDebug(surfaceData, debugData, debugColor))
                {
                    // 返回材质 debug 颜色，同时保留材质 alpha，方便后续透明调试继续沿用同一逻辑。
                    return float4(debugColor, surfaceData.alpha);
                }

                // Returns the lit color and preserves the material alpha value for future transparent/alpha-clip work.
                return float4(finalColor, surfaceData.alpha);
            }

            // Ends the HLSL program for this pass.
            ENDHLSL
        }
    }

    CustomEditor "Burt.RenderPipeline.Editor.BurtLitShaderGUI"

    // Disables fallback so BurtRP shader errors do not silently use another pipeline shader.
    Fallback Off
}


