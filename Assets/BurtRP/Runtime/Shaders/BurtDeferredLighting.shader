// 定义 BurtRP Deferred Lighting 在 Unity 内部查找时使用的隐藏 shader 名称，主 Agent 通过 Shader.Find("Hidden/BurtRP/DeferredLighting") 创建材质。
Shader "Hidden/BurtRP/DeferredLighting"
{
    // 当前 shader 只服务主 Agent 的 Deferred Lighting 全屏合成 Pass，不参与材质 Inspector 显示。
    SubShader
    {
        // 标记为 BurtRP 专用 shader，避免被其他管线误用。
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        // 定义 DefaultLit 第一版 Deferred Lighting pass：从 GBuffer 还原材质，再复用 Forward PBR shading core。
        Pass
        {
            // 给 Frame Debugger 显示明确名称，方便和 GBuffer 写入、Forward fallback 区分。
            Name "Burt Deferred Lighting"

            // 主 Agent 当前按材质 pass index 绘制；LightMode 仍保留，方便后续改成 ShaderTagId 或调试过滤。
            Tags { "LightMode" = "BurtDeferredLighting" }

            // 全屏三角形不需要背面剔除。
            Cull Off

            // Deferred Lighting 只写 CameraColor，不改 CameraDepth。
            ZWrite Off

            // 全屏合成始终通过深度测试，实际可见性来自 GBuffer 和 CameraDepth。
            ZTest Always

            // 第一版直接覆盖 CameraColor；后续如果改成逐灯累加，可以再参考 XRender 的 additive light pass。
            Blend Off

            // 开始 HLSL 程序。
            HLSLPROGRAM

            // 使用 shader model 3.5，保证 SV_VertexID 可以生成全屏三角形。
            #pragma target 3.5

            // 声明顶点 shader 入口。
            #pragma vertex Vert

            // 声明片元 shader 入口。
            #pragma fragment Frag

            // 引入 Unity 基础宏和内置环境光工具，PBR 间接光需要 SH / Reflection Probe 支持。
            #include "UnityCG.cginc"

            // 引入 BurtRP Deferred 公共工具；出处参考 XRender SlabDeferredLightingPass + SlabGbufferUnpack。
            #include "ShaderLibrary/BurtDeferred.hlsl"

            // 引入 BurtRP 主光阴影采样；Deferred 中会用重建 positionWS 生成 shadowCoord。
            #include "ShaderLibrary/BurtShadows.hlsl"

            // 引入 BurtRP PBR lighting；FromGBuffer 入口复用 Forward 的 BRDF、SH 和 Reflection Probe 逻辑。
            #include "ShaderLibrary/BurtLighting.hlsl"

            // 引入 BurtRP Shading Debug；Deferred Lighting 要复用 Forward 的材质、BRDF 和光照拆项显示逻辑。
            #include "ShaderLibrary/BurtShadingDebug.hlsl"

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
            float4 Frag(Varyings input) : SV_Target
            {
                // 使用全屏三角形插值得到的屏幕 UV；GBuffer 和 CameraDepth 都由同一相机 RT 链路生成。
                float2 screenUV = input.screenUV;

                // 采样三张 GBuffer，字段布局由 BurtGBuffer.hlsl 统一定义。
                BurtEncodedGBuffer encodedGBuffer = BurtSampleEncodedGBuffer(screenUV);

                // 把 RT 中的编码值还原成 Deferred shading 使用的语义化材质数据。
                BurtGBufferData gbufferData = BurtDecodeGBuffer(encodedGBuffer);

                // 从 CameraDepth 和逆 ViewProjection 重建世界坐标，同时得到和 Forward 一致的 viewDirectionWS。
                float rawDepth;
                float3 positionWS;
                float3 viewDirectionWS;
                BurtPrepareDeferredViewData(screenUV, rawDepth, positionWS, viewDirectionWS);

                // 使用重建出的世界坐标生成主光 shadowCoord，和 Forward 阴影接收路径保持一致。
                float4 shadowCoord = BurtTransformWorldToMainLightShadow(float4(positionWS, 1.0f));

                // 采样主光阴影；当 C# 没有启用阴影时，BurtShadows.hlsl 会安全返回 1。
                float shadowAttenuation = BurtSampleMainLightShadow(shadowCoord);

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

                // 从 GBuffer 进入 PBR shading core，评估主光直射、SH 间接漫反射和 Reflection Probe 间接高光。
                BurtPBRShadingComponents pbrComponents = BurtEvaluatePBRShadingComponentsFromGBuffer(shadingGBufferData, mainLight, viewDirectionWS);

                // 先合成最终材质颜色，后续 Shading Debug 可以直接观察写入 CameraColor 前的最终结果。
                float3 finalColor = pbrComponents.lighting + gbufferData.emission;

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

                // 写入 GBuffer 保存的 metallic，保证 Metallic Debug 显示 Deferred 解码后的金属度。
                debugSurfaceData.metallic = gbufferData.metallic;

                // 写入 GBuffer 保存的 occlusion，保证 Occlusion Debug 和间接光使用同一 AO。
                debugSurfaceData.occlusion = gbufferData.occlusion;

                // 把真实 GBuffer 解码数据转成 PBRMaterialData，方便填充 GBuffer DiffuseColor / Roughness 调试项。
                BurtPBRMaterialData debugGBufferMaterialData = BurtPreparePBRMaterialData(gbufferData);

                // 创建 Shading Debug 数据结构；Deferred Lighting 会把 GBuffer 还原后的所有 PBR 拆项写进去。
                BurtShadingDebugData debugData;

                // 写入 GBuffer 解码出的世界空间法线，NormalWS Debug View 会把它编码成颜色。
                debugData.normalWS = gbufferData.normalWS;

                // 写入 Detail Lighting 结果；当模式开启时 pbrComponents 已经基于 0.18 中灰 BaseColor 计算。
                debugData.detailLightingColor = pbrComponents.lighting;

                // 写入直接漫反射结果，DirectDiffuse Debug View 会显示它。
                debugData.directDiffuseColor = pbrComponents.directDiffuse;

                // 写入直接高光结果，DirectSpecular Debug View 会显示它。
                debugData.directSpecularColor = pbrComponents.directSpecular;

                // 写入间接漫反射结果，IndirectDiffuse Debug View 会显示 Unity SH / Light Probe 贡献。
                debugData.indirectDiffuseColor = pbrComponents.indirectDiffuse;

                // 写入间接高光结果，IndirectSpecular Debug View 会显示 Reflection Probe / Sky Reflection 贡献。
                debugData.indirectSpecularColor = pbrComponents.indirectSpecular;

                // 写入主光阴影衰减，ShadowAttenuation Debug View 用它确认 Deferred 接收阴影是否和 Forward 一致。
                debugData.shadowAttenuation = shadowAttenuation;

                // 写入参与间接光计算的 AO，AmbientOcclusion Debug View 用它确认 GBuffer0.a 是否进入 lighting。
                debugData.ambientOcclusion = gbufferData.occlusion;

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

                // 写入真实 GBuffer 解码后的 BaseColor，用来检查 GBuffer0.rgb 的材质颜色还原。
                debugData.gbufferBaseColor = gbufferData.baseColor;

                // 写入真实 GBuffer 解码后的世界空间法线，用来检查 octahedron normal 编码精度和方向。
                debugData.gbufferNormalWS = gbufferData.normalWS;

                // 写入真实 GBuffer 解码后的 Metallic，用来检查 GBuffer1.b 的材质还原。
                debugData.gbufferMetallic = gbufferData.metallic;

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

            // 结束 HLSL 程序。
            ENDHLSL
        }
    }

    // 禁用 fallback，避免 Deferred Lighting 缺失时悄悄回退到其他管线 shader。
    Fallback Off
}
