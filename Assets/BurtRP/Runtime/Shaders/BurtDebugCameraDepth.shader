// 定义 Shader 在 Unity 内部查找时使用的隐藏路径。
Shader "Hidden/BurtRP/DebugCameraDepth"
{
    // 定义 SubShader，当前调试 shader 只需要一个可用的全屏绘制 SubShader。
    SubShader
    {
        // 给 SubShader 打标签，RenderPipeline 标记这是 BurtRP 专用的调试 shader。
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        // 定义一个全屏调试 Pass，用来把深度纹理转换成灰度颜色。
        Pass
        {
            // 给 Pass 起名，方便 Frame Debugger 里识别。
            Name "Burt Debug Camera Depth"

            // 关闭剔除，因为全屏三角形不需要背面剔除。
            Cull Off

            // 关闭深度写入，避免调试画面污染 CameraDepth。
            ZWrite Off

            // 始终通过深度测试，让调试画面覆盖整个 CameraColor。
            ZTest Always

            // 开始 HLSL shader 程序。
            HLSLPROGRAM

            // 使用 shader model 3.5，保证 SV_VertexID 可以用于生成全屏三角形。
            #pragma target 3.5

            // 声明顶点 shader 函数名是 Vert。
            #pragma vertex Vert

            // 声明片元 shader 函数名是 Frag。
            #pragma fragment Frag

            // 引入 Unity 的基础 shader 工具函数，例如 Linear01Depth 和深度纹理采样宏。
            #include "UnityCG.cginc"

            // 声明 BurtRP 注册的 CameraDepth 深度纹理。
            UNITY_DECLARE_DEPTH_TEXTURE(_BurtCameraDepthTexture);

            // 声明深度显示缩放，C# Pass 会从 BurtRenderPipelineAsset 传入这个值。
            float _BurtDepthDebugScale;

            // 声明深度调试图的 Y 预翻转开关；这个开关用来抵消后续 FinalBlit 对 CameraColor 的翻转。
            float _BurtDepthDebugYFlip;

            // 定义顶点输入结构，全屏三角形只需要系统提供的顶点 ID。
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

                // 输出屏幕 UV，用来在片元 shader 中采样深度纹理。
                float2 uv : TEXCOORD0;
            };

            // 定义顶点 shader，使用三个顶点生成覆盖屏幕的超大三角形。
            Varyings Vert(Attributes input)
            {
                // 创建一个输出结构变量，用来保存顶点 shader 的输出结果。
                Varyings output;

                // 根据 vertexID 生成全屏三角形的 UV，三个点分别覆盖屏幕左下、右下外侧、左上外侧。
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);

                // 把 UV 从 0..2 区间转换成裁剪空间坐标，形成一个覆盖全屏的三角形。
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);

                // 保存 UV，片元 shader 会用它采样 _BurtCameraDepthTexture。
                output.uv = uv;

                // 返回顶点 shader 输出结果。
                return output;
            }

            // 定义片元 shader，把深度纹理转换成灰度颜色。
            float4 Frag(Varyings input) : SV_Target
            {
                // 从 BurtRP 的 CameraDepth 深度纹理中采样原始硬件深度。
                float2 depthUv = input.uv; // 复制全屏三角形插值出来的 UV，后面会按需要修改 y 值。

                if (_BurtDepthDebugYFlip > 0.5f) // 如果当前相机的最终输出会在 FinalBlit 中翻转，这里提前把深度采样反向一次。
                {
                    depthUv.y = 1.0f - depthUv.y; // 把深度纹理采样坐标上下翻转，让调试图经过 FinalBlit 之后仍然保持正向。
                }

                float rawDepth = SAMPLE_DEPTH_TEXTURE(_BurtCameraDepthTexture, depthUv);

                // 把非线性的硬件深度转换成 0 到 1 的线性相机深度。
                float linear01Depth = Linear01Depth(rawDepth);

                // 用缩放系数增强近处深度差异，并限制到 0 到 1。
                float visualDepth = saturate(linear01Depth * max(_BurtDepthDebugScale, 0.0001));

                // 输出灰度深度，越远越亮，越近越暗。
                return float4(visualDepth, visualDepth, visualDepth, 1.0);
            }

            // 结束 HLSL shader 程序。
            ENDHLSL
        }
    }

    // 禁用 fallback，避免调试 shader 出错时悄悄回退到其他管线 shader。
    Fallback Off
}