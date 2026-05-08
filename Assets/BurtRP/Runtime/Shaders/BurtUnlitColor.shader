// 定义 Shader 在 Unity Shader 菜单里的路径和名称。
Shader "BurtRP/UnlitColor"
{
    // 定义材质面板可编辑的属性区域。
    Properties
    {
        // 定义一个颜色属性，材质 Inspector 中显示为 Base Color，默认值为白色。
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
    }

    // 定义一个 SubShader，Unity 会从上到下选择当前平台可用的 SubShader。
    SubShader
    {
        // 给 SubShader 打标签，RenderType 表示这是不透明物体，RenderPipeline 标记这是 BurtRP shader。
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "BurtRenderPipeline" }

        // 定义一个真正执行绘制的 Pass。
        Pass
        {
            // 给 Pass 起一个名字，方便 Frame Debugger 里识别。
            Name "Burt Unlit Forward"

            // 设置 LightMode 为 BurtForward，让 BurtRenderPipeline.cs 里的 DrawingSettings 能匹配到这个 Pass。
            Tags { "LightMode" = "BurtForward" }

            // 开始 HLSL shader 程序。
            HLSLPROGRAM

            // 声明顶点 shader 函数名是 Vert。
            #pragma vertex Vert

            // 声明片元 shader 函数名是 Frag。
            #pragma fragment Frag

            // 引入 Unity 的基础 shader 工具函数，例如 UnityObjectToClipPos。
            #include "UnityCG.cginc"

            // 定义材质常量缓冲区，SRP Batcher 要求每材质属性放在 UnityPerMaterial 里。
            CBUFFER_START(UnityPerMaterial)

                // 声明材质颜色属性，对应 Properties 里的 _BaseColor。
                float4 _BaseColor;

            // 结束材质常量缓冲区定义。
            CBUFFER_END

            // 定义顶点输入结构，描述从 Mesh 顶点数据里读取什么。
            struct Attributes
            {
                // 读取模型空间顶点位置，POSITION 是 Unity 传入顶点位置的语义。
                float4 positionOS : POSITION;
            };

            // 定义顶点输出结构，也就是顶点 shader 传给片元 shader 的数据。
            struct Varyings
            {
                // 输出裁剪空间位置，SV_POSITION 是 GPU 光栅化必须使用的语义。
                float4 positionCS : SV_POSITION;
            };

            // 定义顶点 shader 函数，输入 Mesh 顶点数据，输出裁剪空间位置。
            Varyings Vert(Attributes input)
            {
                // 创建一个输出结构变量，用来保存顶点 shader 的输出结果。
                Varyings output;

                // 把模型空间顶点位置转换到裁剪空间，GPU 后续会用它进行屏幕投影。
                output.positionCS = UnityObjectToClipPos(input.positionOS);

                // 返回顶点 shader 输出结果。
                return output;
            }

            // 定义片元 shader 函数，输入插值后的顶点输出，返回屏幕像素颜色。
            float4 Frag(Varyings input) : SV_Target
            {
                // 返回材质颜色，不做光照计算，所以这是一个最简单的 Unlit shader。
                return _BaseColor;
            }

            // 结束 HLSL shader 程序。
            ENDHLSL
        }
    }

    // 禁用 fallback，避免 BurtRP shader 出错时悄悄回退到其他管线 shader。
    Fallback Off
}
