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
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtPreExposure.hlsl"

            #define BURT_DEPTH_ONLY_ALPHA_CLIP 0
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtDepthOnlyPass.hlsl"

            // 结束 HLSL shader 程序。
            ENDHLSL
        }

        // 定义阴影投射 Pass，Burt Draw Main Light Shadow Caster 会用它把物体写进主光阴影图。
        Pass
        {
            // 给阴影 Pass 起一个名字，方便 Frame Debugger 里识别。
            Name "Burt Unlit Shadow Caster"

            // 使用 Unity 标准 ShadowCaster LightMode，因为 ScriptableRenderContext.DrawShadows 会查找这个标签。
            Tags { "LightMode" = "BurtDisabledShadowCaster" }

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

            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            // 引入 Unity 的基础 shader 工具函数，UnityObjectToClipPos 会使用当前主光视图投影矩阵。
            #include "UnityCG.cginc"

            #define BURT_SHADOW_CASTER_ALPHA_CLIP 0
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtShadowCasterPass.hlsl"

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
            #pragma multi_compile_fragment _ BURT_SHADING_DEBUG
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtPreExposure.hlsl"

            // 引入 Unity 的基础 shader 工具函数，例如 UnityObjectToClipPos。
            #include "UnityCG.cginc"
#if defined(BURT_SHADING_DEBUG)
            #define BURT_FORWARD_SINGLE_SHADING_MODEL 1
            #define BURT_MATERIAL_SHADING_MODEL_DEFAULT_LIT 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInput.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLighting.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtShadows.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtShadingDebug.hlsl"
#endif

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
                float3 normalOS : NORMAL;
            };

            // 定义顶点输出结构，也就是顶点 shader 传给片元 shader 的数据。
            struct Varyings
            {
                // 输出裁剪空间位置，SV_POSITION 是 GPU 光栅化必须使用的语义。
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            // 定义顶点 shader 函数，输入 Mesh 顶点数据，输出裁剪空间位置。
            Varyings Vert(Attributes input)
            {
                // 创建一个输出结构变量，用来保存顶点 shader 的输出结果。
                Varyings output;

                // 把模型空间顶点位置转换到裁剪空间，GPU 后续会用它进行屏幕投影。
                output.positionCS = UnityObjectToClipPos(input.positionOS);
                output.positionWS = mul(unity_ObjectToWorld, input.positionOS).xyz;
                output.normalWS = UnityObjectToWorldNormal(input.normalOS);

                // 返回顶点 shader 输出结果。
                return output;
            }

            // 定义片元 shader 函数，输入插值后的顶点输出，返回屏幕像素颜色。
            float4 Frag(Varyings input) : SV_Target
            {
#if defined(BURT_ENABLE_SHADING_DEBUG)
                BurtSurfaceData surfaceData = BurtCreateSurfaceData(_BaseColor);
                float3 normalWS = BurtSafeNormalize(input.normalWS);
                float3 viewDirectionWS = BurtSafeNormalize(_WorldSpaceCameraPos.xyz - input.positionWS);
                BurtLight mainLight = BurtCreateMainLight(BurtSampleMainLightShadow(input.positionWS));
                BurtPBRShadingComponents pbrComponents = BurtEvaluatePBRShadingComponents(surfaceData, mainLight, normalWS, viewDirectionWS, input.positionWS);

                BurtShadingDebugData debugData = BurtCreateDefaultShadingDebugData(normalWS);
                debugData.shadowAttenuation = BurtSampleMainLightShadow(input.positionWS);
                debugData.additionalDiffuseColor = pbrComponents.additionalDiffuse;
                debugData.additionalSpecularColor = pbrComponents.additionalSpecular;
                debugData.additionalUnshadowedColor = BurtEvaluateAdditionalLightingUnshadowedDebug(surfaceData, normalWS, viewDirectionWS, input.positionWS);
                debugData.additionalShadowAttenuation = BurtEvaluateAdditionalShadowAttenuationDebug(input.positionWS, normalWS);
                BurtFillAdditionalLightShadowProjectionDebugData(
                    input.positionWS,
                    normalWS,
                    debugData.additionalShadowFaceColor,
                    debugData.additionalShadowUVColor,
                    debugData.additionalShadowDepthColor,
                    debugData.additionalShadowDepthDeltaColor);
                debugData.finalLightingColor = _BaseColor.rgb;

                BurtFillMainLightShadowShadingDebugData(
                    input.positionWS,
                    debugData.normalWS,
                    debugData.shadowCascadeColor,
                    debugData.shadowCascadeBlend,
                    debugData.shadowDistanceFade,
                    debugData.shadowPCSSRadius,
                    debugData.shadowReceiverDepthDelta,
                    debugData.shadowPCSSBlockerFraction);

                float3 debugColor;
                if (BurtTryEvaluateMaterialShadingDebug(surfaceData, debugData, debugColor))
                {
                    return float4(debugColor, surfaceData.alpha);
                }
#endif

                // 返回材质颜色，不做光照计算，所以这是一个最简单的 Unlit shader。
                return float4(BurtApplyPreExposure(_BaseColor.rgb), _BaseColor.a);
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
            #pragma multi_compile_fragment _ BURT_SHADING_DEBUG

            // 引入 Unity 的基础 shader 工具函数，例如 UnityObjectToClipPos。
            #include "UnityCG.cginc"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtPreExposure.hlsl"
#if defined(BURT_SHADING_DEBUG)
            #define BURT_FORWARD_SINGLE_SHADING_MODEL 1
            #define BURT_MATERIAL_SHADING_MODEL_DEFAULT_LIT 1
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInput.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLighting.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtShadows.hlsl"
            #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtShadingDebug.hlsl"
#endif

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
                float3 normalOS : NORMAL;
            };

            // 定义 Deferred ForwardOnly 顶点输出结构，也就是顶点 shader 传给片元 shader 的数据。
            struct ForwardOnlyVaryings
            {
                // 输出裁剪空间位置，SV_POSITION 是 GPU 光栅化必须使用的语义。
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            // 定义 Deferred ForwardOnly 顶点 shader 函数，输入 Mesh 顶点数据，输出裁剪空间位置。
            ForwardOnlyVaryings VertForwardOnly(ForwardOnlyAttributes input)
            {
                // 创建一个输出结构变量，用来保存顶点 shader 的输出结果。
                ForwardOnlyVaryings output;

                // 把模型空间顶点位置转换到裁剪空间，GPU 后续会用它进行屏幕投影。
                output.positionCS = UnityObjectToClipPos(input.positionOS);
                output.positionWS = mul(unity_ObjectToWorld, input.positionOS).xyz;
                output.normalWS = UnityObjectToWorldNormal(input.normalOS);

                // 返回顶点 shader 输出结果。
                return output;
            }

            // 定义 Deferred ForwardOnly 片元 shader 函数，输入插值后的顶点输出，返回屏幕像素颜色。
            float4 FragForwardOnly(ForwardOnlyVaryings input) : SV_Target
            {
#if defined(BURT_ENABLE_SHADING_DEBUG)
                BurtSurfaceData surfaceData = BurtCreateSurfaceData(_BaseColor);
                float3 normalWS = BurtSafeNormalize(input.normalWS);
                float3 viewDirectionWS = BurtSafeNormalize(_WorldSpaceCameraPos.xyz - input.positionWS);
                BurtLight mainLight = BurtCreateMainLight(BurtSampleMainLightShadow(input.positionWS));
                BurtPBRShadingComponents pbrComponents = BurtEvaluatePBRShadingComponents(surfaceData, mainLight, normalWS, viewDirectionWS, input.positionWS);

                BurtShadingDebugData debugData = BurtCreateDefaultShadingDebugData(normalWS);
                debugData.shadowAttenuation = BurtSampleMainLightShadow(input.positionWS);
                debugData.additionalDiffuseColor = pbrComponents.additionalDiffuse;
                debugData.additionalSpecularColor = pbrComponents.additionalSpecular;
                debugData.additionalUnshadowedColor = BurtEvaluateAdditionalLightingUnshadowedDebug(surfaceData, normalWS, viewDirectionWS, input.positionWS);
                debugData.additionalShadowAttenuation = BurtEvaluateAdditionalShadowAttenuationDebug(input.positionWS, normalWS);
                BurtFillAdditionalLightShadowProjectionDebugData(
                    input.positionWS,
                    normalWS,
                    debugData.additionalShadowFaceColor,
                    debugData.additionalShadowUVColor,
                    debugData.additionalShadowDepthColor,
                    debugData.additionalShadowDepthDeltaColor);
                debugData.finalLightingColor = _BaseColor.rgb;

                BurtFillMainLightShadowShadingDebugData(
                    input.positionWS,
                    debugData.normalWS,
                    debugData.shadowCascadeColor,
                    debugData.shadowCascadeBlend,
                    debugData.shadowDistanceFade,
                    debugData.shadowPCSSRadius,
                    debugData.shadowReceiverDepthDelta,
                    debugData.shadowPCSSBlockerFraction);

                float3 debugColor;
                if (BurtTryEvaluateMaterialShadingDebug(surfaceData, debugData, debugColor))
                {
                    return float4(debugColor, surfaceData.alpha);
                }
#endif

                // 返回材质颜色，不做光照计算，所以它适合作为不能写 GBuffer 的 Unlit 兜底路径。
                return float4(BurtApplyPreExposure(_BaseColor.rgb), _BaseColor.a);
            }

            // 结束 HLSL shader 程序。
            ENDHLSL
        }
    }

    CustomEditor "Burt.RenderPipeline.Editor.BurtUnlitShaderGUI"

    // 禁用 fallback，避免 BurtRP shader 出错时悄悄回退到其他管线 shader。
    Fallback Off
}


