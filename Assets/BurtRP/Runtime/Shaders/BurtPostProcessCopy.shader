// 定义 Shader 在 Unity 内部查找时使用的隐藏路径。
Shader "Hidden/BurtRP/PostProcessCopy"
{
    // 定义 SubShader，当前后处理框架只需要一个无效果全屏拷贝 SubShader。
    SubShader
    {
        // 给 SubShader 打标签，标记这是 BurtRP 专用 shader。
        Tags { "RenderPipeline" = "BurtRenderPipeline" }

        // 定义一个全屏后处理 Pass，用来执行 No-op Copy 或 Tonemapping。
        Pass
        {
            // 给 Pass 起名，方便 Frame Debugger 中识别。
            Name "Burt Post Process Copy"

            // 关闭剔除，因为全屏三角形不依赖正反面。
            Cull Off

            // 关闭深度写入，避免后处理污染 CameraDepth。
            ZWrite Off

            // 始终通过深度测试，让拷贝覆盖整个颜色目标。
            ZTest Always

            // 开始 HLSL 程序。
            HLSLPROGRAM

            // 使用 shader model 3.5，保证可以用 SV_VertexID 生成全屏三角形。
            #pragma target 3.5

            // 声明顶点 shader 函数名。
            #pragma vertex Vert

            // 声明片元 shader 函数名。
            #pragma fragment Frag

            // 引入 Unity 基础 shader 宏，保持和现有 FinalBlit shader 风格一致。
            #include "UnityCG.cginc"

            // 声明当前后处理使用的源纹理，C# Pass 会在每次绘制前设置它。
            sampler2D _BurtPostProcessSourceTexture;

            // 声明 Tonemapping 模式，0 表示 None，1 表示 Neutral，2 表示 ACES。
            float _BurtTonemappingMode;

            // 声明 Tonemapping 前使用的线性曝光倍率，1 表示不改变亮度。
            float _BurtPostExposure;

            // 定义顶点输入结构，全屏三角形只需要系统顶点 ID。
            struct Attributes
            {
                // 读取当前程序化顶点编号，取值为 0、1、2。
                uint vertexID : SV_VertexID;
            };

            // 定义顶点输出结构，传递裁剪空间位置和屏幕 UV。
            struct Varyings
            {
                // 输出裁剪空间位置，供 GPU 光栅化使用。
                float4 positionCS : SV_POSITION;

                // 输出屏幕 UV，供片元 shader 采样源纹理。
                float2 uv : TEXCOORD0;
            };

            // 定义中性 Tonemapping 曲线，用简单压缩把 HDR 颜色映射到 0..1 附近。
            float3 BurtTonemapNeutral(float3 color)
            {
                // 保证进入曲线的颜色不会是负值，避免负 HDR 值造成奇怪的压缩结果。
                color = max(color, 0.0);

                // 使用 Reinhard 风格的简单压缩，亮度越高越接近 1，但不会突然截断。
                return color / (color + 1.0);
            }

            // 定义 ACES 近似 Tonemapping 曲线，作为第一版更接近电影感的压缩选项。
            float3 BurtTonemapACES(float3 color)
            {
                // 保证进入曲线的颜色不会是负值，避免负 HDR 值在有理函数里产生异常颜色。
                color = max(color, 0.0);

                // 定义 ACES 拟合曲线参数 a，用来控制高光肩部形状。
                const float a = 2.51;

                // 定义 ACES 拟合曲线参数 b，用来控制暗部进入曲线的偏移。
                const float b = 0.03;

                // 定义 ACES 拟合曲线参数 c，用来控制高光压缩强度。
                const float c = 2.43;

                // 定义 ACES 拟合曲线参数 d，用来控制中间调斜率。
                const float d = 0.59;

                // 定义 ACES 拟合曲线参数 e，用来控制整体黑位和白位稳定性。
                const float e = 0.14;

                // 执行常见的 ACES fitted 近似曲线，并用 saturate 把结果限制到显示范围。
                return saturate((color * (a * color + b)) / (color * (c * color + d) + e));
            }

            // 根据 C# 上传的模式选择具体 Tonemapping 曲线。
            float3 BurtApplyTonemapping(float3 color)
            {
                // 如果模式小于 0.5，就认为是 None，直接返回原始颜色。
                if (_BurtTonemappingMode < 0.5)
                {
                    // 返回未修改的颜色，保证 No-op Copy 不改变画面。
                    return color;
                }

                // 在 Tonemapping 前应用线性曝光倍率，让用户可以用 EV 控制整体亮度。
                color *= _BurtPostExposure;

                // 如果模式小于 1.5，就认为是 Neutral，走中性压缩曲线。
                if (_BurtTonemappingMode < 1.5)
                {
                    // 返回 Neutral 曲线处理后的颜色。
                    return BurtTonemapNeutral(color);
                }

                // 其他当前已知模式走 ACES 近似曲线，后续新增模式时可以继续扩展分支。
                return BurtTonemapACES(color);
            }

            // 定义顶点 shader，用三个程序化顶点生成覆盖全屏的大三角形。
            Varyings Vert(Attributes input)
            {
                // 创建输出结构变量，用来保存顶点 shader 的结果。
                Varyings output;

                // 根据 vertexID 生成 0..2 范围的全屏三角形 UV。
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);

                // 把 UV 转换成裁剪空间坐标，形成覆盖屏幕的三角形。
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);

                // 中间 RT 到中间 RT 的拷贝不做 Y 翻转，最终方向仍交给 FinalBlit 统一处理。
                output.uv = uv;

                // 返回顶点 shader 输出。
                return output;
            }

            // 定义片元 shader，根据当前模式原样输出或执行 Tonemapping。
            float4 Frag(Varyings input) : SV_Target
            {
                // 从当前源纹理采样颜色。
                float4 color = tex2D(_BurtPostProcessSourceTexture, input.uv);

                // 对 RGB 执行 Tonemapping，Alpha 保持原样，避免破坏后续可能依赖透明度的目标。
                color.rgb = BurtApplyTonemapping(color.rgb);

                // 返回处理后的颜色；None 模式下这里就是原样返回。
                return color;
            }

            // 结束 HLSL 程序。
            ENDHLSL
        }
    }

    // 禁用 fallback，避免后处理拷贝失败时悄悄走其他管线 shader。
    Fallback Off
}
