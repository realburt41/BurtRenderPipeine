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

        // 定义深度预写 Pass，只写深度，不写颜色。
        Pass
        {
            // 给深度 Pass 起一个名字，方便 Frame Debugger 里识别。
            Name "Burt Depth Only"

            // 设置 LightMode 为 BurtDepthOnly，让 BurtDepthPrepass 能匹配到这个 Pass。
            Tags { "LightMode" = "BurtDepthOnly" }

            // 关闭颜色写入，让这个 Pass 只影响深度缓冲。
            ColorMask 0

            // 开启深度写入，让这个 Pass 能把不透明物体深度写进 CameraDepth。
            ZWrite On

            // 使用小于等于深度测试，和常见不透明绘制保持一致。
            ZTest LEqual

            // 开始 HLSL shader 程序。
            HLSLPROGRAM

            // 声明顶点 shader 函数名是 VertDepth。
            #pragma vertex VertDepth

            // 声明片元 shader 函数名是 FragDepth。
            #pragma fragment FragDepth

            // 引入 Unity 的基础 shader 工具函数，例如 UnityObjectToClipPos。
            #include "UnityCG.cginc"

            // 定义深度 Pass 的顶点输入结构。
            struct DepthAttributes
            {
                // 读取模型空间顶点位置，POSITION 是 Unity 传入顶点位置的语义。
                float4 positionOS : POSITION;
            };

            // 定义深度 Pass 的顶点输出结构。
            struct DepthVaryings
            {
                // 输出裁剪空间位置，SV_POSITION 是 GPU 光栅化必须使用的语义。
                float4 positionCS : SV_POSITION;
            };

            // 定义深度 Pass 的顶点 shader 函数。
            DepthVaryings VertDepth(DepthAttributes input)
            {
                // 创建一个输出结构变量，用来保存顶点 shader 的输出结果。
                DepthVaryings output;

                // 把模型空间顶点位置转换到裁剪空间，让 GPU 能进行深度光栅化。
                output.positionCS = UnityObjectToClipPos(input.positionOS);

                // 返回顶点 shader 输出结果。
                return output;
            }

            // 定义深度 Pass 的片元 shader 函数。
            float4 FragDepth(DepthVaryings input) : SV_Target
            {
                // 返回任意颜色值，因为 ColorMask 0 会禁止实际颜色写入。
                return 0;
            }

            // 结束 HLSL shader 程序。
            ENDHLSL
        }

        // 定义阴影投射 Pass，Burt Draw Main Light Shadow Caster 会用它把物体写进主光阴影图。
        Pass
        {
            // 给阴影 Pass 起一个名字，方便 Frame Debugger 里识别。
            Name "Burt Unlit Shadow Caster"

            // 使用 Unity 标准 ShadowCaster LightMode，因为 ScriptableRenderContext.DrawShadows 会查找这个标签。
            Tags { "LightMode" = "ShadowCaster" }

            // 关闭颜色写入，因为 shadow map 只需要深度信息。
            ColorMask 0

            // 开启深度写入，让这个 Pass 能把投影物体深度写进 MainLightShadowMap。
            ZWrite On

            // 使用小于等于深度测试，和其他不透明深度 Pass 保持一致。
            ZTest LEqual

            // 开始 HLSL shader 程序。
            HLSLPROGRAM

            // 声明顶点 shader 函数名是 VertShadow。
            #pragma vertex VertShadow

            // 声明片元 shader 函数名是 FragShadow。
            #pragma fragment FragShadow

            // 引入 Unity 的基础 shader 工具函数，UnityObjectToClipPos 会使用当前主光视图投影矩阵。
            #include "UnityCG.cginc"

            // 保存当前 request 的主光方向，ShadowCaster 顶点偏移需要用它计算法线和光向夹角。
            float4 _BurtMainLightDirection;

            // 保存 C# 已折算到世界单位的 normal bias，ShadowCaster 只在顶点阶段使用它。
            float _BurtMainLightShadowDepthBias;
            float _BurtMainLightShadowNormalBias;

            // 定义阴影 Pass 的顶点输入结构。
            struct ShadowAttributes
            {
                // 读取模型空间顶点位置，POSITION 是 Unity 传入顶点位置的语义。
                float4 positionOS : POSITION;

                // 读取模型空间法线，ShadowCaster normal bias 需要沿世界法线推开顶点。
                float3 normalOS : NORMAL;
            };

            // 定义阴影 Pass 的顶点输出结构。
            struct ShadowVaryings
            {
                // 输出主光裁剪空间位置，SV_POSITION 是 GPU 光栅化必须使用的语义。
                float4 positionCS : SV_POSITION;
            };

            // 根据法线和主光方向计算 ShadowCaster 顶点的世界空间 normal bias。
            float3 ApplyBurtShadowCasterNormalBias(float4 positionOS, float3 normalOS)
            {
                // 先把顶点转到世界空间再偏移，避免非等比缩放时模型空间距离不一致。
                float3 positionWS = mul(unity_ObjectToWorld, positionOS).xyz;

                // 法线和光向必须处在同一世界空间，才能正确判断表面是否处于掠射角。
                float3 normalWS = UnityObjectToWorldNormal(normalOS);
                normalWS *= rsqrt(max(dot(normalWS, normalWS), 0.000001f));

                // C# 每次 ShadowCaster 绘制前都会上传当前主光方向，这里做安全归一化避免长度影响偏移。
                float3 lightDirectionWS = _BurtMainLightDirection.xyz;
                lightDirectionWS *= rsqrt(max(dot(lightDirectionWS, lightDirectionWS), 0.000001f));

                // C# 已按 shadow texel 把 bias 转成世界单位，这里只做非负保护避免反向拉回表面。
                float normalBias = _BurtMainLightShadowNormalBias;

                // 表面越接近掠射角越容易出现 self-shadow，所以用 1 - NdotL 放大法线偏移。
                float normalBiasScale = (1.0f - saturate(dot(normalWS, lightDirectionWS))) * normalBias;

                // 沿世界法线推出 caster 顶点，让 shadow map 深度和接收面错开一小段距离。
                return positionWS + lightDirectionWS * _BurtMainLightShadowDepthBias + normalWS * normalBiasScale;
            }

            // 定义阴影 Pass 的顶点 shader 函数。
            ShadowVaryings VertShadow(ShadowAttributes input)
            {
                // 创建一个输出结构变量，用来保存顶点 shader 的输出结果。
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

                // 返回顶点 shader 输出结果。
                return output;
            }

            // 定义阴影 Pass 的片元 shader 函数。
            float4 FragShadow(ShadowVaryings input) : SV_Target
            {
                // 返回任意颜色值，因为 ColorMask 0 会禁止实际颜色写入。
                return 0;
            }

            // 结束 HLSL shader 程序。
            ENDHLSL
        }

        // 定义一个真正执行颜色绘制的 Pass。
        Pass
        {
            // 给 Pass 起一个名字，方便 Frame Debugger 里识别。
            Name "Burt Unlit Forward"

            // 设置 LightMode 为 BurtForward，让 BurtRenderPipeline.cs 里的 DrawingSettings 能匹配到这个 Pass。
            Tags { "LightMode" = "BurtForward" }

            // 开启深度写入，当前阶段 Forward Pass 仍保持传统不透明写深度行为。
            ZWrite On

            // 使用小于等于深度测试，让已经通过 DepthPrepass 的像素可以通过。
            ZTest LEqual

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

        // 定义 Deferred 路径专用的前向兜底颜色 Pass。
        Pass
        {
            // 给 Pass 起一个名字，方便 Frame Debugger 里区分它和普通 BurtForward Pass。
            Name "Burt Unlit Forward Only"

            // 设置 LightMode 为 BurtForwardOnly，让 Deferred 后的 ForwardOnly 兜底 Pass 精确匹配它。
            Tags { "LightMode" = "BurtForwardOnly" }

            // 开启深度写入，让这个不写 GBuffer 的不透明物体仍能更新后续透明物体看到的深度。
            ZWrite On

            // 使用小于等于深度测试，让已经通过 DepthPrepass 的像素可以在 Deferred Lighting 后重新写回颜色。
            ZTest LEqual

            // 开始 HLSL shader 程序。
            HLSLPROGRAM

            // 声明顶点 shader 函数名是 VertForwardOnly。
            #pragma vertex VertForwardOnly

            // 声明片元 shader 函数名是 FragForwardOnly。
            #pragma fragment FragForwardOnly

            // 引入 Unity 的基础 shader 工具函数，例如 UnityObjectToClipPos。
            #include "UnityCG.cginc"

            // 定义材质常量缓冲区，SRP Batcher 要求每材质属性放在 UnityPerMaterial 里。
            CBUFFER_START(UnityPerMaterial)

                // 声明材质颜色属性，对应 Properties 里的 _BaseColor。
                float4 _BaseColor;

            // 结束材质常量缓冲区定义。
            CBUFFER_END

            // 定义 Deferred ForwardOnly 顶点输入结构，描述从 Mesh 顶点数据里读取什么。
            struct ForwardOnlyAttributes
            {
                // 读取模型空间顶点位置，POSITION 是 Unity 传入顶点位置的语义。
                float4 positionOS : POSITION;
            };

            // 定义 Deferred ForwardOnly 顶点输出结构，也就是顶点 shader 传给片元 shader 的数据。
            struct ForwardOnlyVaryings
            {
                // 输出裁剪空间位置，SV_POSITION 是 GPU 光栅化必须使用的语义。
                float4 positionCS : SV_POSITION;
            };

            // 定义 Deferred ForwardOnly 顶点 shader 函数，输入 Mesh 顶点数据，输出裁剪空间位置。
            ForwardOnlyVaryings VertForwardOnly(ForwardOnlyAttributes input)
            {
                // 创建一个输出结构变量，用来保存顶点 shader 的输出结果。
                ForwardOnlyVaryings output;

                // 把模型空间顶点位置转换到裁剪空间，GPU 后续会用它进行屏幕投影。
                output.positionCS = UnityObjectToClipPos(input.positionOS);

                // 返回顶点 shader 输出结果。
                return output;
            }

            // 定义 Deferred ForwardOnly 片元 shader 函数，输入插值后的顶点输出，返回屏幕像素颜色。
            float4 FragForwardOnly(ForwardOnlyVaryings input) : SV_Target
            {
                // 返回材质颜色，不做光照计算，所以它适合作为不能写 GBuffer 的 Unlit 兜底路径。
                return _BaseColor;
            }

            // 结束 HLSL shader 程序。
            ENDHLSL
        }
    }

    CustomEditor "Burt.RenderPipeline.Editor.BurtUnlitShaderGUI"

    // 禁用 fallback，避免 BurtRP shader 出错时悄悄回退到其他管线 shader。
    Fallback Off
}


