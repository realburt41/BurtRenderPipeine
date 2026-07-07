// 定义 Shader 在 Unity 内部查找时使用的隐藏路径。
Shader "Hidden/BurtRP/FinalBlit"
{
    // 定义 SubShader，当前 FinalBlit 只需要一个可用的全屏拷贝 SubShader。
    SubShader
    {
        // 给 SubShader 打标签，RenderPipeline 标记这是 BurtRP 专用的最终输出 shader。
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        // 定义一个全屏拷贝 Pass，用来把中间 CameraColor 写入最终相机目标。
        Pass
        {
            // 给 Pass 起名，方便 Frame Debugger 里识别。
            Name "Burt Final Blit"

            // 关闭剔除，因为全屏三角形不需要背面剔除。
            Cull Off

            // 关闭深度写入，避免最终拷贝污染任何深度缓冲。
            ZWrite Off

            // 始终通过深度测试，让拷贝结果覆盖整个最终颜色目标。
            ZTest Always

            // 开始 HLSL shader 程序。
            HLSLPROGRAM

            // 使用 shader model 3.5，保证 SV_VertexID 可以用于生成全屏三角形。
            #pragma target 3.5

            // 声明顶点 shader 函数名是 Vert。
            #pragma vertex Vert

            // 声明片元 shader 函数名是 Frag。
            #pragma fragment Frag

            // 引入 Unity 的基础 shader 工具宏，例如 UNITY_UV_STARTS_AT_TOP。
            #include "UnityCG.cginc"

            // 声明 BurtRP 注册的中间相机颜色纹理。
            sampler2D _BurtCameraColorTexture;

            // 声明 FinalBlit 是否需要翻转采样 UV 的 Y 轴，1 表示翻转，0 表示保持原样。
            float _BurtFinalBlitYFlip;

            // 定义顶点输入结构，全屏三角形只需要系统提供的顶点 ID。
            struct Attributes
            {
                // 读取当前程序化顶点的编号，范围是 0、1、2。
                uint VertexID : SV_VertexID;
            };

            // 定义顶点输出结构，也就是顶点 shader 传给片元 shader 的数据。
            struct Varyings
            {
                // 输出裁剪空间位置，SV_POSITION 是 GPU 光栅化必须使用的语义。
                float4 PositionCS : SV_POSITION;

                // 输出屏幕 UV，用来在片元 shader 中采样中间 CameraColor。
                float2 UV : TEXCOORD0;
            };

            // 定义顶点 shader，使用三个顶点生成覆盖屏幕的超大三角形。
            Varyings Vert(Attributes input)
            {
                // 创建一个输出结构变量，用来保存顶点 shader 的输出结果。
                Varyings output;

                // 根据 vertexID 生成全屏三角形的 UV，三个点分别覆盖屏幕左下、右下外侧、左上外侧。
                float2 uv = float2((input.VertexID << 1) & 2, input.VertexID & 2);

                // 把 UV 从 0..2 区间转换成裁剪空间坐标，形成一个覆盖全屏的三角形。
                output.PositionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);

                // 先把程序化生成的 UV 写入输出结构，后面再根据 C# 上传的开关决定是否翻转 Y。
                output.UV = uv;

                // 如果当前最终目标需要适配 D3D 类平台的 RenderTexture 到 backbuffer 方向差异，就翻转采样 UV 的 Y 轴。
                if (_BurtFinalBlitYFlip > 0.5)
                {
                    // 用 1 - y 把屏幕底部采样到源纹理底部，修正 Scene/Game 视图上下颠倒。
                    output.UV.y = 1.0 - output.UV.y;
                }

                // 返回顶点 shader 输出结果。
                return output;
            }

            // 定义片元 shader，把中间颜色纹理原样输出到最终相机目标。
            float4 Frag(Varyings input) : SV_Target
            {
                // 从 BurtRP 的中间 CameraColor 中采样颜色。
                float4 color = tex2D(_BurtCameraColorTexture, input.UV);

                // 原样返回采样颜色，后续如果要加后处理可以从这里继续扩展。
                return color;
            }

            // 结束 HLSL shader 程序。
            ENDHLSL
        }
    }

    // 禁用 fallback，避免最终拷贝出错时悄悄回退到其他管线 shader。
    Fallback Off
}

