// BurtRP hidden deferred lighting shader; C# creates it with Shader.Find("Hidden/BurtRP/DeferredLighting").
Shader "Hidden/BurtRP/DeferredLighting"
{
    // This shader serves deferred fullscreen lighting only and is not exposed in material inspectors.
    SubShader
    {
        // Mark as BurtRP-only so other render pipelines do not pick it accidentally.
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        HLSLINCLUDE
            #include "UnityCG.cginc"

            // 引入 BurtRP Deferred 公共工具；出处参考 XRender SlabDeferredLightingPass + SlabGbufferUnpack。
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtDeferred.hlsl"

            // 引入 BurtRP PBR lighting；FromGBuffer 入口复用 Forward 的 BRDF、SH 和 Reflection Probe 逻辑。

            #define BURT_USE_ADDITIONAL_LIGHT_BUFFER 1
            #define BURT_USE_TILED_LIGHTING 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLighting.hlsl"

            // 引入 BurtRP 主光阴影采样；Deferred 中会用重建 positionWS 生成 shadowCoord。
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtShadows.hlsl"

            // 引入 BurtRP Shading Debug；Deferred Lighting 要复用 Forward 的材质、BRDF 和光照拆项显示逻辑。
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtShadingDebug.hlsl"

            // 顶点输入只需要系统生成的顶点 ID。
            struct Attributes
            {
                // 读取程序化全屏三角形的顶点编号，范围是 0、1、2。
                uint vertexID : SV_VertexID;
            };

            // 顶点 shader 输出给片元 shader 的全屏数据。
            struct Varyings
            {
                // 保存裁剪空间位置，GPU 光栅化必须写入 SV_POSITION。
                float4 positionCS : SV_POSITION;

                // 保存屏幕 UV，用来采样 GBuffer 和 CameraDepth。
                float2 screenUV : TEXCOORD0;
            };

            // Deferred Lighting is split into two fullscreen passes; filtering is still driven by the packed GBuffer shading model.
            static const float BURT_DEFERRED_LIGHTING_FILTER_DEFAULT_LIT = 0.0f;
            static const float BURT_DEFERRED_LIGHTING_FILTER_HAIR = 1.0f;
            sampler2D _BurtScreenSpaceAmbientOcclusionTexture;
            float _BurtScreenSpaceAmbientOcclusionEnabled;

            bool BurtDeferredLightingPassAcceptsShadingModel(float shadingModelID, float shadingModelFilter)
            {
                bool isHair = BurtIsHairShadingModel(shadingModelID);
                return shadingModelFilter == BURT_DEFERRED_LIGHTING_FILTER_HAIR ? isHair : !isHair;
            }

            float BurtSampleDeferredScreenSpaceAmbientOcclusion(float2 screenUV)
            {
                if (_BurtScreenSpaceAmbientOcclusionEnabled < 0.5f)
                {
                    return 1.0f;
                }

                float ao = tex2D(_BurtScreenSpaceAmbientOcclusionTexture, screenUV).r;
                return saturate(ao);
            }

            float BurtResolveDeferredScreenSpaceSpecularOcclusionScale(
                BurtPBRShadingComponents components,
                float noV,
                float screenSpaceAO)
            {
                return GetIndirectSpecularOcclusion(noV, saturate(screenSpaceAO), components.perceptualRoughness);
            }

            BurtPBRShadingComponents BurtApplyDeferredScreenSpaceAmbientOcclusion(
                BurtPBRShadingComponents components,
                float noV,
                float screenSpaceAO)
            {
                float ao = saturate(screenSpaceAO);
                float specularAO = BurtResolveDeferredScreenSpaceSpecularOcclusionScale(components, noV, ao);
                components.indirectDiffuse *= ao;
                components.indirectSpecular *= specularAO;
                components.specularOcclusion *= specularAO;
                components.indirectLighting = components.indirectDiffuse + components.indirectSpecular;
                components.lighting = components.directLighting + components.indirectLighting;
                return components;
            }

            // 出处：XRender/Shaders/SlabShaderPass/SlabDeferredLightingPass.hlsl::Vert，通过 vertexID 生成全屏三角形。
            Varyings Vert(Attributes input)
            {
                // 创建输出结构体，下面填入全屏三角形的位置和 UV。
                Varyings output;

                // 生成覆盖整个屏幕的裁剪空间三角形顶点。
                output.positionCS = BurtGetFullScreenTriangleVertexPosition(input.vertexID);

                // 生成与全屏三角形匹配的屏幕 UV。
                output.screenUV = BurtGetFullScreenTriangleTexCoord(input.vertexID);

                // 返回顶点 shader 输出。
                return output;
            }

            // 出处：XRender DeferredLighting 的 GBufferUnpackAllParams -> SlabLightingLoop -> FinalColor；BurtRP 当前对应 DecodeGBuffer -> PBR FromGBuffer -> lighting + emission。
            float4 BurtDeferredLightingFragment(Varyings input, float shadingModelFilter)
            {
                // 使用全屏三角形插值得到的屏幕 UV；GBuffer 和 CameraDepth 都由同一相机 RT 链路生成。
                float2 screenUV = input.screenUV;

                // This is the first low-risk shading-model mask: read only the packed model id before the expensive GBuffer/depth/shadow path.
                float shadingModelID = BurtSampleDeferredShadingModelID(screenUV);
                if (!BurtDeferredLightingPassAcceptsShadingModel(shadingModelID, shadingModelFilter))
                {
                    return float4(0.0f, 0.0f, 0.0f, 0.0f);
                }

                // 采样四张 GBuffer，字段布局由 BurtGBuffer.hlsl 统一定义。
                BurtEncodedGBuffer encodedGBuffer = BurtSampleEncodedGBuffer(screenUV);

                // 把 RT 中的编码值还原成 Deferred shading 使用的语义化材质数据。
                BurtGBufferData gbufferData = BurtDecodeGBuffer(encodedGBuffer);

                // 从 CameraDepth 和逆 ViewProjection 重建世界坐标，同时得到和 Forward 一致的 viewDirectionWS。
                float rawDepth;
                float3 positionWS;
                float3 shadowPositionWS;
                float3 viewDirectionWS;
                BurtPrepareDeferredViewData(screenUV, rawDepth, positionWS, shadowPositionWS, viewDirectionWS);

                // 采样主光阴影；当 C# 没有启用阴影时，BurtShadows.hlsl 会安全返回 1。
                float shadowAttenuation = BurtSampleMainLightShadow(shadowPositionWS);


                // 从 BurtRP 全局主光参数创建一盏方向光，阴影衰减已经写入 light 数据。
                BurtLight mainLight = BurtCreateMainLight(shadowAttenuation);

                // 创建一份用于 shading 的 GBuffer 数据副本；正常模式下它和真实 GBuffer 解码结果完全一致。
                BurtGBufferData shadingGBufferData = gbufferData;

                // Detail Lighting 参考 XRender：只把参与光照计算的 BaseColor 替换成 0.18 中灰。
                if (BurtIsShadingDebugEnabled() && BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DETAIL_LIGHTING))
                {
                    // 只修改 shading 副本，不修改真实 gbufferData，避免 Albedo / GBuffer Debug 读到被中灰覆盖的数据。
                    shadingGBufferData.baseColor = float3(0.18f, 0.18f, 0.18f);
                }

                // 从 GBuffer 进入 shading-model dispatch；Default Lit 走 PBR，Hair 走独立的最小 Hair lighting 分支。
                BurtPBRShadingComponents pbrComponents = BurtEvaluateShadingModelComponentsFromGBuffer(shadingGBufferData, mainLight, viewDirectionWS, positionWS, shadowPositionWS, screenUV);
                float screenSpaceAmbientOcclusion = BurtSampleDeferredScreenSpaceAmbientOcclusion(screenUV);
                float3 deferredAONormalWS = BurtSafeNormalize(BurtGetGBufferDirectionWS(shadingGBufferData));
                float3 deferredAOViewDirectionWS = BurtSafeNormalize(viewDirectionWS);
                if (BurtIsHairShadingModel(shadingGBufferData.shadingModelID))
                {
                    deferredAONormalWS = BurtHairCreateViewFacingNormalWS(deferredAONormalWS, deferredAOViewDirectionWS);
                }

                float deferredNoV = saturate(dot(deferredAONormalWS, deferredAOViewDirectionWS));
                pbrComponents = BurtApplyDeferredScreenSpaceAmbientOcclusion(pbrComponents, deferredNoV, screenSpaceAmbientOcclusion);

                // 先合成最终材质颜色，后续 Shading Debug 可以直接观察写入 CameraColor 前的最终结果。
                float3 finalColor = pbrComponents.lighting + gbufferData.emission;

                if (!BurtIsShadingDebugEnabled())
                {
                    return float4(finalColor, 1.0f);
                }

                // 创建一份 SurfaceData；Shading Debug 的公共函数使用 SurfaceData 读取材质输入类调试项。
                BurtSurfaceData debugSurfaceData;

                // 写入原始 BaseColor；这里使用真实 gbufferData，而不是 Detail Lighting 中灰副本。
                debugSurfaceData.baseColor = float4(gbufferData.baseColor, 1.0f);

                // Deferred opaque GBuffer 当前不保存 alpha，所以调试输出固定为完全不透明。
                debugSurfaceData.alpha = 1.0f;

                // 写入 GBuffer 保存的 XRender reflectance，保证 Reflectance Debug 和 Forward 使用同一语义。
                debugSurfaceData.reflectance = gbufferData.reflectance;

                // 写入 GBuffer 保存的 smoothness，保证 Smoothness Debug 显示材质面板语义。
                debugSurfaceData.smoothness = gbufferData.smoothness;

                // 写入 GBuffer 保存的 material channel；Default Lit 是 metallic，Hair 第一版显示 scatter。
                debugSurfaceData.metallic = BurtGetGBufferMaterialChannel(gbufferData);
                debugSurfaceData.clearCoatMask = BurtGetClearCoatMask(gbufferData);
                debugSurfaceData.subsurfaceStrength = BurtGetSubsurfaceStrength(gbufferData);

                // 写入 GBuffer 保存的 occlusion，保证 Occlusion Debug 和间接光使用同一 AO。
                debugSurfaceData.occlusion = gbufferData.occlusion;

                // 写入 shading model，虽然当前材质 Debug 不直接显示它，但保持 SurfaceData 字段完整。
                debugSurfaceData.shadingModelID = gbufferData.shadingModelID;

                // 把真实 GBuffer 解码数据转成 PBRMaterialData，方便填充 GBuffer DiffuseColor / Roughness 调试项。
                BurtPBRMaterialData debugGBufferMaterialData = BurtPreparePBRMaterialData(gbufferData);

                // 创建 Shading Debug 数据结构；Deferred Lighting 会把 GBuffer 还原后的所有 PBR 拆项写进去。
                BurtShadingDebugData debugData;

                // 写入 GBuffer 解码出的向量槽；Default Lit 是 normalWS，Hair 是 strandDirectionWS。
                debugData.normalWS = BurtGetGBufferDirectionWS(gbufferData);

                // 写入 Detail Lighting 结果；当模式开启时 pbrComponents 已经基于 0.18 中灰 BaseColor 计算。
                debugData.detailLightingColor = pbrComponents.lighting;

                // 写入直接漫反射结果，DirectDiffuse Debug View 会显示它。
                debugData.directDiffuseColor = pbrComponents.directDiffuse;

                // 写入直接高光结果，DirectSpecular Debug View 会显示它。
                debugData.directSpecularColor = pbrComponents.directSpecular;

                // 写入追加光直接光拆分，Additional Light Debug View 会显示它。
                debugData.additionalDiffuseColor = pbrComponents.additionalDiffuse;
                debugData.additionalSpecularColor = pbrComponents.additionalSpecular;
                debugData.additionalUnshadowedColor = BurtEvaluateAdditionalLightingUnshadowedDebugFromGBuffer(shadingGBufferData, viewDirectionWS, positionWS, screenUV);

                // 写入间接漫反射结果，IndirectDiffuse Debug View 会显示 Unity SH / Light Probe 贡献。
                debugData.indirectDiffuseColor = pbrComponents.indirectDiffuse;

                // 写入间接高光结果，IndirectSpecular Debug View 会显示 Reflection Probe / Sky Reflection 贡献。
                debugData.indirectSpecularColor = pbrComponents.indirectSpecular;

                // 写入主光阴影衰减，ShadowAttenuation Debug View 用它确认 Deferred 接收阴影是否和 Forward 一致。
                debugData.shadowAttenuation = shadowAttenuation;
                debugData.additionalShadowAttenuation = BurtEvaluateAdditionalShadowAttenuationDebug(shadowPositionWS, deferredAONormalWS, screenUV);
                BurtFillAdditionalLightShadowProjectionDebugData(
                    shadowPositionWS,
                    deferredAONormalWS,
                    screenUV,
                    debugData.additionalShadowFaceColor,
                    debugData.additionalShadowUVColor,
                    debugData.additionalShadowDepthColor,
                    debugData.additionalShadowDepthDeltaColor);

                BurtFillMainLightShadowShadingDebugData(
                    shadowPositionWS,
                    debugData.normalWS,
                    debugData.shadowCascadeColor,
                    debugData.shadowCascadeBlend,
                    debugData.shadowDistanceFade,
                    debugData.shadowPCSSRadius,
                    debugData.shadowReceiverDepthDelta,
                    debugData.shadowPCSSBlockerFraction);

                // 写入参与间接光计算的 AO，AmbientOcclusion Debug View 用它确认 GBuffer0.a 是否进入 lighting。
                debugData.ambientOcclusion = saturate(gbufferData.occlusion * screenSpaceAmbientOcclusion);

                // 写入 GBuffer 保存的自发光贡献，Emission Debug View 用它确认 GBuffer2.rgb 是否被最终叠加。
                debugData.emissionColor = gbufferData.emission;

                // 写入最终材质光照，FinalLighting Debug View 用它对比 Deferred Lighting 输出和后处理前画面。
                debugData.finalLightingColor = finalColor;

                // 写入材质 reflectance，Reflectance Debug View 会用它检查 GBuffer2.a 是否按 XRender 语义还原。
                debugData.reflectance = gbufferData.reflectance;

                // 写入材质感知粗糙度，Roughness Debug View 会显示 1 - smoothness 后的结果。
                debugData.perceptualRoughness = pbrComponents.perceptualRoughness;

                // 写入直接高光实际粗糙度，SpecularAARoughness Debug View 会显示 AA 后的结果。
                debugData.specularAARoughness = pbrComponents.specularAARoughness;

                // 写入直接高光能量补偿，便于对比 Forward / Deferred 的 LUT.z 是否一致。
                debugData.specularEnergyCompensation = pbrComponents.specularEnergyCompensation;

                // 写入间接高光能量补偿，便于检查 Reflection Probe 高光补能是否一致。
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

                // 写入直接 Schlick Fresnel 项，用来检查 reflectance / metallic 到 F0 的映射。
                debugData.directBRDFFresnel = pbrComponents.directBRDFFresnel;

                // 写入直接 diffuse lobe，当前默认 Lambert，后续切 Burley 时这里会同步变化。
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

                // Hair debug lobes are non-zero only when the Hair lighting branch evaluates this pixel.
                debugData.hairPrimaryLobe = pbrComponents.hairPrimaryLobe;
                debugData.hairSecondaryLobe = pbrComponents.hairSecondaryLobe;
                debugData.hairTransmissionLobe = pbrComponents.hairTransmissionLobe;
                debugData.hairScatter = pbrComponents.hairScatter;

                // 写入真实 GBuffer 解码后的 BaseColor，用来检查 GBuffer0.rgb 的材质颜色还原。
                debugData.gbufferBaseColor = gbufferData.baseColor;

                // 写入真实 GBuffer 解码后的向量槽，用来检查 octahedron 编码精度和方向语义。
                debugData.gbufferNormalWS = BurtGetGBufferDirectionWS(gbufferData);

                // 写入真实 GBuffer 解码后的材质通道；Default Lit=metallic，Hair=scatter。
                debugData.gbufferMetallic = BurtGetGBufferMaterialChannel(gbufferData);
                debugData.gbufferClearCoatMask = BurtGetClearCoatMask(gbufferData);
                debugData.gbufferClearCoatNormalWS = BurtGetClearCoatNormalWS(gbufferData);
                debugData.gbufferSubsurfaceStrength = BurtGetSubsurfaceStrength(gbufferData);

                // 写入真实 GBuffer 解码后的 Smoothness，用来检查 GBuffer1.a 的面板语义还原。
                debugData.gbufferSmoothness = gbufferData.smoothness;

                // 写入真实 GBuffer 解码后的 AO，用来检查 GBuffer0.a 的间接光遮蔽输入。
                debugData.gbufferOcclusion = gbufferData.occlusion;

                // 写入真实 GBuffer 解码后的 Reflectance，用来检查 GBuffer2.a 的 XRender reflectance 输入。
                debugData.gbufferReflectance = gbufferData.reflectance;

                // 写入从真实 GBuffer Smoothness 还原出的感知粗糙度，用来和 Forward Roughness 对照。
                debugData.gbufferRoughness = debugGBufferMaterialData.perceptualRoughness;

                // 写入从真实 GBuffer 重建出的 DiffuseColor，用来检查 metallic 对 diffuse 的扣除是否和 Forward 一致。
                debugData.gbufferDiffuseColor = debugGBufferMaterialData.diffuseColor;

                // 创建一个临时调试颜色变量，只有命中材质、BRDF 或光照 debug 模式时才会被真正输出。
                float3 debugColor;

                // 如果 Overlay 选择了 Shading Debug 模式，就直接输出调试颜色，避免后续 skybox、透明或后处理干扰观察。
                if (BurtTryEvaluateMaterialShadingDebug(debugSurfaceData, debugData, debugColor))
                {
                    // Deferred Lighting Pass 只处理 opaque GBuffer，所以 debug 输出 alpha 固定为 1。
                    return float4(debugColor, 1.0f);
                }

                // 返回给 CameraColor，后续 Skybox、Transparent、PostProcess 和 FinalBlit 继续处理。
                return float4(finalColor, 1.0f);
            }

            // Pass-specific fragment wrappers call the shared filtered lighting path.

            float4 FragDeferredLit(Varyings input) : SV_Target
            {
                return BurtDeferredLightingFragment(input, BURT_DEFERRED_LIGHTING_FILTER_DEFAULT_LIT);
            }

            float4 FragDeferredHair(Varyings input) : SV_Target
            {
                return BurtDeferredLightingFragment(input, BURT_DEFERRED_LIGHTING_FILTER_HAIR);
            }
        ENDHLSL

        // Default Lit pass: writes all non-Hair deferred pixels and clears Hair pixels to black before the additive Hair pass.
        Pass
        {
            Name "Burt Deferred Lit Lighting"
            Tags { "LightMode" = "BurtDeferredLitLighting" }
            Cull Off
            ZWrite Off
            ZTest Always
            Blend Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragDeferredLit
            ENDHLSL
        }

        // Hair pass: only shades Hair GBuffer pixels and adds them after the Lit pass.
        Pass
        {
            Name "Burt Deferred Hair Lighting"
            Tags { "LightMode" = "BurtDeferredHairLighting" }
            Cull Off
            ZWrite Off
            ZTest Always
            Stencil
            {
                Ref 1
                ReadMask 3
                Comp Equal
                Pass Keep
            }
            Blend One One

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragDeferredHair
            ENDHLSL
        }
    }

    // Disable fallback so missing deferred lighting fails visibly instead of using an unrelated shader.
    Fallback Off
}
