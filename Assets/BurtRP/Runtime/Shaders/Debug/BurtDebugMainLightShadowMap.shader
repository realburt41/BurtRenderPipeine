// 定义 BurtDebugMainLightShadowMapPass 使用的隐藏 shader，用来把主光 shadow map 可视化到屏幕。
Shader "Hidden/BurtRP/DebugMainLightShadowMap"
{
    // 这个 shader 只由运行时调试 Pass 创建材质使用，因此不需要暴露任何材质属性。
    SubShader
    {
        // 标记为 BurtRP 专用辅助 shader，避免和用户可选材质 shader 混淆。
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        // 定义唯一的全屏调试 Pass。
        Pass
        {
            // 在 Frame Debugger 里显示这个 Pass 的用途。
            Name "Burt Debug Main Light Shadow Map"

            // 关闭裁剪，因为全屏三角形不依赖正反面。
            Cull Off

            // 关闭深度写入，因为调试视图只覆盖颜色目标。
            ZWrite Off

            // 深度测试始终通过，确保 shadow map 调试图覆盖整个 CameraColor。
            ZTest Always

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            // 以深度纹理方式声明主光 shadow map，方便读取原始深度用于调试显示。
            UNITY_DECLARE_DEPTH_TEXTURE(_BurtMainLightShadowMap);

            // 保存管线资产上传的调试亮度倍率。
            float _BurtMainLightShadowDebugExposure;

            // Stores the resolved ShadowMap sampling Y-flip flag; C# already folds in the later FinalBlit direction.
            float _BurtMainLightShadowDebugYFlip;

            // 定义全屏三角形从顶点阶段传给片元阶段的数据。
            struct Varyings
            {
                float4 PositionCS : SV_POSITION; // 保存裁剪空间位置，用于光栅化全屏三角形。
                float2 UV : TEXCOORD0; // 保存归一化 shadow map UV，用于片元阶段采样。
            };

            // 直接用 SV_VertexID 生成全屏三角形，避免额外创建临时 Mesh。
            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings output; // 创建要返回给 GPU 管线的输出结构。
                float2 uv = float2((vertexID << 1) & 2, vertexID & 2); // 生成 (0,0)、(2,0)、(0,2) 三个 UV，插值后覆盖屏幕 0..1。
                output.PositionCS = float4(uv * 2.0f - 1.0f, 0.0f, 1.0f); // 把生成的 UV 转换成全屏三角形的裁剪空间坐标。
                output.UV = uv; // 把 UV 传给片元 shader，屏幕范围内会插值到 0..1。
                return output; // 返回生成好的全屏顶点数据。
            }

            // 把 shadow map 深度值转换成灰度调试颜色。
            float4 Frag(Varyings input) : SV_Target
            {
                float2 shadowUv = input.UV; // Copies the interpolated fullscreen UV so the input structure stays read-only.
                if (_BurtMainLightShadowDebugYFlip > 0.5f) // Checks whether this output path needs to flip the ShadowMap source UV.
                {
                    shadowUv.y = 1.0f - shadowUv.y; // Mirrors the sampled ShadowMap vertically to compensate for source texture origin differences.
                }
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_BurtMainLightShadowMap, shadowUv); // 从 shadow map 当前 UV 读取原始深度。
                #if defined(UNITY_REVERSED_Z)
                    rawDepth = 1.0f - rawDepth; // 反向 Z 平台转换成更直观的近处 0、远处 1 显示范围。
                #endif
                float displayDepth = saturate(rawDepth * max(0.0001f, _BurtMainLightShadowDebugExposure)); // 应用曝光倍率并钳制到可见灰度范围。
                return float4(displayDepth.xxx, 1.0f); // 把深度写成灰度 RGB，并保持不透明 alpha。
            }
            ENDHLSL
        }
    }

    // 禁用 fallback，避免这个调试辅助 shader 编译失败时静默落到其他管线 shader。
    Fallback Off
}
