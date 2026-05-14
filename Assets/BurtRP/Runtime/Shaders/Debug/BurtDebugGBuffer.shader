// 定义 BurtDebugGBufferPass 使用的隐藏 shader，用来把 Deferred GBuffer 可视化到屏幕。
Shader "Hidden/BurtRP/DebugGBuffer"
{
    // 这个 shader 只由运行时调试 Pass 创建材质使用，因此不需要暴露任何材质面板属性。
    SubShader
    {
        // 标记为 BurtRP 专用辅助 shader，避免被其他 SRP 或 Built-in 管线误用。
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        // 定义唯一的全屏 GBuffer 调试 Pass。
        Pass
        {
            // 在 Frame Debugger 中显示这个 Pass 的用途。
            Name "Burt Debug GBuffer"

            // 关闭背面剔除，因为全屏三角形没有传统网格正反面需求。
            Cull Off

            // 关闭深度写入，避免调试画面污染 CameraDepth。
            ZWrite Off

            // 始终通过深度测试，确保调试图覆盖整个 CameraColor。
            ZTest Always

            // 开始 HLSL 程序。
            HLSLPROGRAM

            // 使用 shader model 3.5，保证 SV_VertexID 可以生成全屏三角形。
            #pragma target 3.5

            // 声明顶点 shader 函数名。
            #pragma vertex Vert

            // 声明片元 shader 函数名。
            #pragma fragment Frag

            // 引入 Unity 基础函数，提供 Linear01Depth 和深度纹理采样宏。
            #include "UnityCG.cginc"

            // 引入 BurtRP Deferred 工具，提供 GBuffer 采样、解码和全屏三角形工具函数。
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtDeferred.hlsl"

            // 声明 C# 上传的调试模式，数值和 BurtGBufferDebugViewMode 枚举保持一致。
            float _BurtGBufferDebugMode;

            // 声明 C# 上传的 Y 预翻转开关，用来抵消后续 FinalBlit 的方向修正。
            float _BurtGBufferDebugYFlip;

            // 定义顶点输入结构，全屏三角形只需要系统提供的顶点编号。
            struct Attributes
            {
                // 读取当前程序化顶点的编号，范围是 0、1、2。
                uint vertexID : SV_VertexID;
            };

            // 定义顶点输出结构，也就是顶点 shader 传给片元 shader 的数据。
            struct Varyings
            {
                // 输出裁剪空间位置，SV_POSITION 是 GPU 光栅化必须使用的语义。
                float4 positionCS : SV_POSITION;

                // 输出屏幕 UV，用来采样 GBuffer 和 CameraDepth。
                float2 screenUV : TEXCOORD0;
            };

            // 定义顶点 shader，使用三个顶点生成覆盖屏幕的超大三角形。
            Varyings Vert(Attributes input)
            {
                // 创建输出结构体变量，用来保存顶点 shader 的输出结果。
                Varyings output;

                // 使用 Deferred 工具函数生成全屏三角形裁剪空间位置。
                output.positionCS = BurtGetFullScreenTriangleVertexPosition(input.vertexID);

                // 使用 Deferred 工具函数生成与全屏三角形匹配的屏幕 UV。
                output.screenUV = BurtGetFullScreenTriangleTexCoord(input.vertexID);

                // 返回生成好的顶点数据。
                return output;
            }

            // 把调试模式浮点值转换成稳定的整数分支索引。
            int BurtResolveGBufferDebugMode()
            {
                // 对 C# 传入的浮点值四舍五入，避免平台精度导致 4.999 进入错误分支。
                return (int)round(_BurtGBufferDebugMode);
            }

            // 把单通道数值显示成灰度颜色。
            float4 BurtDebugScalar(float value)
            {
                // 把输入限制到 0..1 并复制到 RGB，alpha 固定为 1。
                return float4(saturate(value).xxx, 1.0f);
            }

            // 把世界空间法线显示成 0..1 的彩色方向图。
            float4 BurtDebugNormal(float3 normalWS)
            {
                // 将 [-1,1] 法线映射到 [0,1]，方便肉眼检查方向是否翻转。
                return float4(saturate(normalWS * 0.5f + 0.5f), 1.0f);
            }

            // 定义片元 shader，根据调试模式输出对应的 GBuffer 内容。
            float4 Frag(Varyings input) : SV_Target
            {
                // 复制屏幕 UV，后面会根据 FinalBlit 方向决定是否翻转 y。
                float2 screenUV = input.screenUV;

                // 判断当前输出链路是否需要提前翻转采样方向。
                if (_BurtGBufferDebugYFlip > 0.5f)
                {
                    // 翻转采样 y，让调试图经过 FinalBlit 后在 SceneView / Preview 中仍然正向。
                    screenUV.y = 1.0f - screenUV.y;
                }

                // 读取三张 GBuffer 的原始编码值。
                BurtEncodedGBuffer encodedGBuffer = BurtSampleEncodedGBuffer(screenUV);

                // 把原始 GBuffer 解码成语义化材质数据。
                BurtGBufferData gbufferData = BurtDecodeGBuffer(encodedGBuffer);

                // 读取整数调试模式，后续分支和 BurtGBufferDebugViewMode 枚举一一对应。
                int debugMode = BurtResolveGBufferDebugMode();

                // 模式 1：直接显示 GBuffer0 原始 RT 内容。
                if (debugMode == 1)
                {
                    // 返回 baseColor.rgb + occlusion.a 的原始编码结果。
                    return float4(saturate(encodedGBuffer.gbuffer0.rgb), 1.0f);
                }

                // 模式 2：直接显示 GBuffer1 原始 RT 内容。
                if (debugMode == 2)
                {
                    // 返回 oct normal.rg、packed shadingModel/material.b、smoothness.a 的原始编码结果。
                    return float4(saturate(encodedGBuffer.gbuffer1.rgb), 1.0f);
                }

                // 模式 3：直接显示 GBuffer2 原始 RT 内容。
                if (debugMode == 3)
                {
                    // 返回 emission.rgb 的原始颜色，alpha 不显示。
                    return float4(saturate(encodedGBuffer.gbuffer2.rgb), 1.0f);
                }

                // 模式 4：显示解码后的 baseColor。
                if (debugMode == 4)
                {
                    // 返回材质基础色，排除光照和后处理影响。
                    return float4(saturate(gbufferData.baseColor), 1.0f);
                }

                // 模式 5：显示解码后的 GBuffer 向量槽；Default Lit=normalWS，Hair=strandDirectionWS。
                if (debugMode == 5)
                {
                    // 返回向量方向彩色图，用来检查 oct 解码、法线贴图方向和 Hair 发丝方向。
                    return BurtDebugNormal(BurtGetGBufferDirectionWS(gbufferData));
                }

                // 模式 6：显示解码后的 GBuffer 材质通道；Default Lit=metallic，Hair=scatter。
                if (debugMode == 6)
                {
                    // 返回材质通道灰度图，避免 Hair 像素把 scatter 误读成 metallic。
                    return BurtDebugScalar(BurtGetGBufferMaterialChannel(gbufferData));
                }

                // 模式 7：显示光滑度。
                if (debugMode == 7)
                {
                    // 返回 smoothness 灰度图。
                    return BurtDebugScalar(gbufferData.smoothness);
                }

                // 模式 8：显示环境遮蔽。
                if (debugMode == 8)
                {
                    // 返回 occlusion 灰度图。
                    return BurtDebugScalar(gbufferData.occlusion);
                }

                // 模式 9：显示自发光颜色。
                if (debugMode == 9)
                {
                    // 返回 emission 颜色，并做 saturate 以避免 HDR 调试图整屏过曝。
                    return float4(saturate(gbufferData.emission), 1.0f);
                }

                // 模式 10：显示 reflectance。
                if (debugMode == 10)
                {
                    // 返回 reflectance 灰度图，用来检查 XRender 风格非金属反射率输入。
                    return BurtDebugScalar(gbufferData.reflectance);
                }

                // 模式 11：显示 CameraDepth 原始深度。
                if (debugMode == 11)
                {
                    // 从当前相机深度纹理读取硬件深度。
                    float rawDepth = BurtSampleDeferredRawDepth(screenUV);

                    // 返回原始深度灰度，方便和专用 Depth Debug 视图对照。
                    return BurtDebugScalar(rawDepth);
                }

                // 模式 12：显示从 GBuffer smoothness 还原出的感知粗糙度。
                if (debugMode == 12)
                {
                    // 返回 perceptual roughness 灰度图，用来检查 Deferred 进入 BRDF 前的 roughness 输入。
                    return BurtDebugScalar(gbufferData.perceptualRoughness);
                }

                // 模式 13：显示从 GBuffer 重建出的 diffuseColor。
                if (debugMode == 13)
                {
                    // 用 GBuffer 数据准备 PBRMaterialData，复用 Deferred Lighting 的材质重建口径。
                    BurtPBRMaterialData materialData = BurtPreparePBRMaterialData(gbufferData);

                    // 返回 metallic 扣除后的 diffuseColor，用来检查金属材质是否正确去除漫反射。
                    return float4(saturate(materialData.diffuseColor), 1.0f);
                }

                // 模式 14：显示解码后的 shading model，黑色=Default Lit，洋红=Hair。
                if (debugMode == 14)
                {
                    float isHair = BurtIsHairShadingModel(gbufferData.shadingModelID) ? 1.0f : 0.0f;
                    return float4(0.6f * isHair, 0.1f * isHair, 0.5f * isHair, 1.0f);
                }

                // 模式 15：Hair 专用 strand direction；当前 Hair 复用 GBuffer1.rg 向量槽，非 Hair 像素显示黑色。
                if (debugMode == 15)
                {
                    if (!BurtIsHairShadingModel(gbufferData.shadingModelID))
                    {
                        return float4(0.0f, 0.0f, 0.0f, 1.0f);
                    }

                    return BurtDebugNormal(BurtGetHairStrandDirectionWS(gbufferData));
                }

                // 模式 16：Hair 专用 scatter；当前 Hair 复用 GBuffer1.b material channel，非 Hair 像素显示黑色。
                if (debugMode == 16)
                {
                    float isHair = BurtIsHairShadingModel(gbufferData.shadingModelID) ? 1.0f : 0.0f;
                    float scatter = BurtGetHairScatter(gbufferData) * isHair;
                    return float4(scatter, scatter, scatter, 1.0f);
                }

                // 模式 17：Hair 专用 longitudinal shift scale；和 scatter 一起打包在 GBuffer1.b material channel。
                if (debugMode == 17)
                {
                    float isHair = BurtIsHairShadingModel(gbufferData.shadingModelID) ? 1.0f : 0.0f;
                    float shiftScale = BurtGetHairLongitudinalShiftScale(gbufferData) * isHair;
                    return float4(shiftScale, shiftScale, shiftScale, 1.0f);
                }

                // 默认分支显示解码后的 baseColor，避免异常模式导致黑屏。
                return float4(saturate(gbufferData.baseColor), 1.0f);
            }

            // 结束 HLSL 程序。
            ENDHLSL
        }
    }

    // 禁用 fallback，避免调试 shader 编译失败时静默落到其他管线 shader。
    Fallback Off
}
