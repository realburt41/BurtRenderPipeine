using UnityEngine; // 引入 UnityEngine 命名空间，用来使用 Color、ShaderTagId 等 Unity 类型。
using UnityEngine.Rendering; // 引入 UnityEngine.Rendering 命名空间，用来使用 CommandBufferPool、DrawingSettings、FilteringSettings 等 SRP 类型。

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让这些 Pass 能直接访问 BurtRenderRequest、BurtCameraData 等类型。
{
    internal static class BurtDrawingSettingsUtility // 集中管理 BurtRP 所有 DrawRenderers Pass 会用到的 ShaderTagId 和 DrawingSettings 创建逻辑。
    {
        private static readonly ShaderTagId BurtForward = new ShaderTagId("BurtForward"); // 定义 BurtRP 前向颜色 Pass 的 LightMode 名称，受支持的 shader 必须提供它。

        private static readonly ShaderTagId BurtForwardOnly = new ShaderTagId("BurtForwardOnly"); // 定义 Deferred 后专用的前向兜底 LightMode，只给不能写 GBuffer 的不透明 shader 使用。

        private static readonly ShaderTagId BurtDepthOnly = new ShaderTagId("BurtDepthOnly"); // 定义 BurtRP Depth Prepass 使用的深度专用 LightMode 名称。

        private static readonly ShaderTagId BurtGBuffer = new ShaderTagId("BurtGBuffer"); // 定义 Deferred GBuffer 绘制使用的 LightMode 名称，shader 侧需要提供同名 Pass。

        private static readonly PerObjectData ForwardPerObjectData = // 定义前向颜色绘制需要 Unity 为每个 Renderer 绑定的内置间接光数据。
            PerObjectData.ReflectionProbes | // 请求 Unity 绑定 unity_SpecCube0 / unity_SpecCube0_HDR，让 Reflection Probe 间接高光能生效。
            PerObjectData.Lightmaps | // 请求 Unity 绑定光照贴图相关数据，给后续接入 baked GI 预留正确 per-object 数据。
            PerObjectData.LightProbe | // 请求 Unity 绑定 unity_SHAr 等 SH 数据，让 ShadeSH9 能读到 Light Probe / Ambient Probe。
            PerObjectData.LightProbeProxyVolume | // 请求 Unity 绑定 LPPV 数据，避免使用 Light Probe Proxy Volume 的物体丢失间接光。
            PerObjectData.LightData | // 请求 Unity 绑定基础 per-object 光照数据，保持和 URP 常见配置一致。
            PerObjectData.OcclusionProbe | // 请求 Unity 绑定 probe occlusion 数据，后续如果接入 shadow mask/AO 可直接读取。
            PerObjectData.OcclusionProbeProxyVolume | // 请求 Unity 绑定 LPPV 版本的 occlusion 数据，和 LightProbeProxyVolume 配套。
            PerObjectData.ShadowMask; // 请求 Unity 绑定 shadow mask 数据，给后续 baked shadow / mixed lighting 预留。

        private static readonly ShaderTagId SRPDefaultUnlit = new ShaderTagId("SRPDefaultUnlit"); // 定义 Unity 通用 SRP Unlit LightMode，方便 Unsupported Pass 把它抓出来显示错误材质。

        private static readonly ShaderTagId ForwardBase = new ShaderTagId("ForwardBase"); // 定义 Built-in 管线 ForwardBase LightMode，方便 Unsupported Pass 抓到旧管线材质。

        private static readonly ShaderTagId Always = new ShaderTagId("Always"); // 定义 Built-in 常见 Always LightMode，避免 fallback pass 被 BurtRP 静默接受。

        private static readonly ShaderTagId PrepassBase = new ShaderTagId("PrepassBase"); // 定义旧 Built-in deferred prepass LightMode，方便 Unsupported Pass 暴露不兼容材质。

        private static readonly ShaderTagId Vertex = new ShaderTagId("Vertex"); // 定义旧 Built-in 顶点光照 LightMode，方便 Unsupported Pass 暴露不兼容材质。

        private static readonly ShaderTagId VertexLMRGBM = new ShaderTagId("VertexLMRGBM"); // 定义旧 Built-in lightmap RGBM LightMode，方便 Unsupported Pass 暴露不兼容材质。

        private static readonly ShaderTagId VertexLM = new ShaderTagId("VertexLM"); // 定义另一个旧 Built-in lightmap LightMode，方便 Unsupported Pass 暴露不兼容材质。

        private static readonly ShaderTagId UniversalForward = new ShaderTagId("UniversalForward"); // 定义 URP Forward LightMode，避免 URP shader 被 BurtRP 静默当作可支持材质。

        private static readonly ShaderTagId UniversalForwardOnly = new ShaderTagId("UniversalForwardOnly"); // 定义 URP ForwardOnly LightMode，方便 Unsupported Pass 报告 URP 专用材质。

        private static readonly ShaderTagId LightweightForward = new ShaderTagId("LightweightForward"); // 定义旧 LWRP Forward LightMode，方便 Unsupported Pass 报告旧 SRP 材质。





        private static readonly ShaderTagId[] EditorPreviewShaderTagIds = new ShaderTagId[] // Preview 需要兼容 Unity 内部预览 shader，不能只匹配 BurtForward。
        {
            BurtForward, // BurtRP 自己的材质预览仍优先走正式前向 Pass。
            BurtForwardOnly, // 允许只实现 ForwardOnly 的 BurtRP 特殊材质在 Preview 中可见。
            SRPDefaultUnlit, // Unity/SRP 默认未标记 Pass 会落到这里，Inspector 预览大量使用它。
            ForwardBase, // Built-in 预览 shader 常见的前向 Pass 名称。
            Always, // Unity 内部预览 shader 常用 Always Pass。
            UniversalForward, // 兼容 URP 风格预览 shader。
            UniversalForwardOnly, // 兼容 URP ForwardOnly 风格预览 shader。
            LightweightForward, // 兼容旧 LWRP 风格预览 shader。
            Vertex, // 兼容 Built-in 顶点光照 fallback。
            VertexLMRGBM, // 兼容 Built-in lightmap RGBM fallback。
            VertexLM, // 兼容 Built-in lightmap fallback。
            PrepassBase, // 兜底旧 Built-in deferred prepass 名称，避免预览物体静默消失。
        };

        private static readonly ShaderTagId[] UnsupportedShaderTagIds = new ShaderTagId[] // 列出 BurtRP 不接管的 LightMode，这些材质会被错误材质明确显示出来。
        {
            SRPDefaultUnlit, // 把通用 SRP Unlit shader 视为不支持，除非它迁移到 BurtForward。
            ForwardBase, // 把 Built-in ForwardBase shader 视为不支持，避免旧材质静默进入 BurtRP。
            Always, // 把 Built-in Always pass 视为不支持，让 fallback 风格 shader 明确变成错误材质。
            PrepassBase, // 把旧 Built-in PrepassBase 视为不支持，因为 BurtRP 不实现这条路径。
            Vertex, // 把旧 Built-in 顶点光照 pass 视为不支持，因为 BurtRP 暂不实现 vertex lighting。
            VertexLMRGBM, // 把旧 Built-in RGBM lightmap pass 视为不支持，因为 BurtRP 暂未接入这条 lightmap 路径。
            VertexLM, // 把另一个旧 Built-in lightmap pass 视为不支持，理由同上。
            UniversalForward, // 把 URP Forward shader 视为不支持，因为 BurtRP 应该使用自己的 LightMode 名称。
            UniversalForwardOnly, // 把 URP ForwardOnly shader 视为不支持，因为 BurtRP 不执行 URP 专用 pass。
            LightweightForward // 把旧 LWRP shader 视为不支持，因为 BurtRP 不执行 LWRP 专用 pass。
        }; // 结束不支持 LightMode 列表。

        public static DrawingSettings CreateForwardDrawingSettings(SortingSettings sortingSettings) // 创建 BurtRP 常规前向颜色绘制使用的 DrawingSettings。
        {
            var drawingSettings = new DrawingSettings(BurtForward, sortingSettings); // 只匹配 BurtForward，让主渲染路径严格由 BurtRP 自己的 shader pass 驱动。

            drawingSettings.perObjectData = ForwardPerObjectData; // 让 Unity 在 DrawRenderers 时真正上传 SH、Reflection Probe 等 per-object 间接光数据。

            return drawingSettings; // 返回配置好的前向绘制设置，供调用方 Pass 使用。
        }

        public static DrawingSettings CreateForwardOnlyDrawingSettings(SortingSettings sortingSettings) // 创建 Deferred 后前向兜底绘制设置，只匹配显式声明 BurtForwardOnly 的 shader。
        {
            var drawingSettings = new DrawingSettings(BurtForwardOnly, sortingSettings); // 只匹配 BurtForwardOnly，避免 Deferred 模式把已经写入 GBuffer 的 BurtForward 物体再画一遍。

            drawingSettings.perObjectData = ForwardPerObjectData; // 兜底前向 shader 仍需要 SH、Reflection Probe 等 per-object 数据，保证和正常 Forward 光照能力一致。

            return drawingSettings; // 返回配置好的绘制设置，供 Deferred ForwardOnly Opaque Pass 使用。
        }

        public static DrawingSettings CreateUnsupportedDrawingSettings( // 创建 Unsupported Shader Debug Pass 使用的 DrawingSettings。
            SortingSettings sortingSettings, // 接收当前相机的排序规则，保证错误材质绘制顺序稳定。
            Material errorMaterial) // 接收用于覆盖不支持材质的错误材质。
        {
            var drawingSettings = new DrawingSettings(UnsupportedShaderTagIds[0], sortingSettings); // 使用第一个不支持 LightMode 作为 DrawingSettings 的主 shader pass 名称。

            for (var shaderTagIndex = 1; shaderTagIndex < UnsupportedShaderTagIds.Length; shaderTagIndex++) // 遍历剩余所有不支持的 LightMode。
            {
                drawingSettings.SetShaderPassName(shaderTagIndex, UnsupportedShaderTagIds[shaderTagIndex]); // 把当前不支持 LightMode 注册到对应 DrawingSettings 槽位。
            }

            drawingSettings.overrideMaterial = errorMaterial; // 强制匹配到的不支持 shader 使用 Unity 错误材质绘制。

            return drawingSettings; // 返回配置好的不支持 shader 绘制设置，供调用方 Pass 使用。
        }

        public static DrawingSettings CreateEditorPreviewDrawingSettings(SortingSettings sortingSettings) // 创建 Unity Editor Preview 专用绘制设置。
        {
            var drawingSettings = new DrawingSettings(EditorPreviewShaderTagIds[0], sortingSettings); // Preview 首选 BurtForward，同时继续注册 Unity 内部预览 Pass。

            for (var shaderTagIndex = 1; shaderTagIndex < EditorPreviewShaderTagIds.Length; shaderTagIndex++) // 把兼容 LightMode 全部注册进 DrawingSettings。
            {
                drawingSettings.SetShaderPassName(shaderTagIndex, EditorPreviewShaderTagIds[shaderTagIndex]); // 允许 Cubemap/ReflectionProbe 预览使用内置 shader 绘制。
            }

            drawingSettings.perObjectData = ForwardPerObjectData; // 如果预览的是 BurtRP 材质，仍然给它绑定基础 per-object 间接光数据。

            return drawingSettings; // 返回配置好的 Preview 绘制设置。
        }

        public static bool BindCameraColorAndDepth(BurtRenderGraphContext context, string commandBufferName) // DrawRenderers 前显式恢复颜色和深度附件，避免前一个全屏 Pass 只绑定 CameraColor 后丢失深度测试。
        {
            if (context == null) // 没有执行上下文时无法读取 RenderGraph 注册的目标。
            {
                return false;
            }

            var cameraColorTarget = context.CameraColorTarget; // 读取当前 request 的中间颜色 RT。
            var cameraDepthTarget = context.CameraDepthTarget; // 读取当前 request 的深度 RT。
            if (!cameraColorTarget.IsValid || !cameraDepthTarget.IsValid) // 任一目标无效时跳过绘制，避免 DrawRenderers 继承错误 RT 状态。
            {
                return false;
            }

            var cmd = CommandBufferPool.Get(commandBufferName); // 用当前 Pass 名创建临时命令缓冲，Frame Debugger 里能看到这次目标恢复。
            cmd.SetRenderTarget(cameraColorTarget.Identifier, cameraDepthTarget.Identifier); // 同时绑定颜色和深度，让 ZTest/ZWrite 对后续 DrawRenderers 生效。
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, context.Request != null ? context.Request.Camera : null); // 恢复 viewport，避免前序 RT 改过尺寸后影响绘制。
            BindMainLightShadowMapIfValid(context, cmd); // DrawRenderers 可能在 Deferred Lighting 之后执行，重新绑定当前 request 的 shadow map 避免读到旧全局纹理。
            context.ScriptableContext.ExecuteCommandBuffer(cmd); // 立即提交目标绑定，后面的 DrawRenderers 会使用这个状态。
            CommandBufferPool.Release(cmd); // 释放临时命令缓冲，避免每帧 GC。
            return true;
        }

        public static void BindMainLightShadowMapIfValid(BurtRenderGraphContext context, CommandBuffer cmd) // 把当前 request 的主光 shadow map 绑定到全局纹理槽。
        {
            if (context == null || cmd == null) // 没有执行上下文或命令缓冲时无法安全绑定。
            {
                return; // 直接跳过，调用方会继续使用已有的无阴影默认状态。
            }

            var shadowMapTarget = context.MainLightShadowMapTarget; // 从 RenderGraph 资源表读取当前 request 的主光 shadow map。
            if (!shadowMapTarget.IsValid) // 当前 request 没有生成主光阴影图时不能绑定无效句柄。
            {
                return; // 保持 Setup Lighting 上传的无阴影默认参数。
            }

            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.MainLightShadowMapId, shadowMapTarget.Identifier); // 确保 Forward/ForwardOnly/Transparent 采样的是当前 request 的阴影图。
        }

        public static DrawingSettings CreateDepthDrawingSettings(SortingSettings sortingSettings) // 创建 BurtRP 深度预写使用的 DrawingSettings。
        {
            var drawingSettings = new DrawingSettings(BurtDepthOnly, sortingSettings); // 只匹配 BurtDepthOnly，避免 Depth Prepass 意外执行颜色 pass。

            return drawingSettings; // 返回配置好的深度绘制设置，供调用方 Pass 使用。
        }

        public static DrawingSettings CreateGBufferDrawingSettings(SortingSettings sortingSettings) // 创建 Deferred GBuffer 绘制设置，只匹配 BurtRP 自己的 GBuffer Pass。
        {
            var drawingSettings = new DrawingSettings(BurtGBuffer, sortingSettings); // 只绘制 LightMode 为 BurtGBuffer 的 shader pass，避免 Forward pass 误写入 GBuffer。

            drawingSettings.perObjectData = PerObjectData.None; // GBuffer 只负责写材质属性，不再请求 SH/ReflectionProbe，避免 Deferred 间接光继续依赖 DrawRenderers 的 per-object 副作用。

            return drawingSettings; // 返回配置好的 GBuffer 绘制设置，供 Draw GBuffer Opaque Pass 使用。
        }
    }

    internal static class BurtShadowRenderTargetUtility
    {
        public static float ResolveMainLightShadowClearDepth()
        {
            return 1f;
        }
    }

    internal static class BurtMainLightShadowMatrixUtility
    {
        public const int CascadeIndex = 0;
        public const int CascadeCount = 1;

        public static readonly Vector3 CascadeSplit = new Vector3(1f, 0f, 0f); // URP single-cascade split: x=1 covers the full camera range.

        public static bool TryGetMainLightShadowMatrices(
            BurtRenderRequest request,
            BurtShadowData shadowData,
            out Matrix4x4 viewMatrix,
            out Matrix4x4 projectionMatrix,
            out ShadowSplitData splitData)
        {
            viewMatrix = Matrix4x4.identity;
            projectionMatrix = Matrix4x4.identity;
            splitData = default;

            if (request == null || shadowData == null || shadowData.MainLightIndex < 0)
            {
                return false;
            }

            return request.CullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                shadowData.MainLightIndex,
                CascadeIndex,
                CascadeCount,
                CascadeSplit,
                shadowData.MainLightShadowResolution,
                shadowData.MainLightShadowNearPlane,
                out viewMatrix,
                out projectionMatrix,
                out splitData);
        }

        public static Matrix4x4 CreateWorldToShadowMatrix(Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix)
        {
            if (SystemInfo.usesReversedZBuffer)
            {
                projectionMatrix.m20 = -projectionMatrix.m20;
                projectionMatrix.m21 = -projectionMatrix.m21;
                projectionMatrix.m22 = -projectionMatrix.m22;
                projectionMatrix.m23 = -projectionMatrix.m23;
            }

            var textureScaleAndBias = Matrix4x4.identity;
            textureScaleAndBias.m00 = 0.5f;
            textureScaleAndBias.m11 = 0.5f;
            textureScaleAndBias.m22 = 0.5f;
            textureScaleAndBias.m03 = 0.5f;
            textureScaleAndBias.m13 = 0.5f;
            textureScaleAndBias.m23 = 0.5f;
            return textureScaleAndBias * projectionMatrix * viewMatrix;
        }
    }

    internal sealed class BurtAllocateMainLightShadowMapPass : BurtRenderPass // 定义主光阴影图分配 Pass，负责为当前 request 创建主光 shadow map。
    {
        public override string Name => "Burt Allocate Main Light Shadow Map"; // 返回这个 Pass 的名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            if (!BurtShadowUtility.ShouldUseMainLightShadow(builder.Request, builder.Asset)) // 如果当前 request 没有主光阴影，就不声明阴影图写入。
            {
                return; // 直接结束资源声明，避免 Debug 输出无效的 MainLightShadowMap 依赖。
            }

            builder.WriteMainLightShadowMap(); // 声明这个 Pass 会申请并初始化 MainLightShadowMap 资源。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现主光阴影图分配 Pass 的执行函数。
        {
            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。

            var request = context.Request; // 从 GraphContext 中取出当前渲染请求。

            if (!BurtShadowUtility.ShouldUseMainLightShadow(request, context.Asset)) // 如果当前 request 不需要主光阴影，就不申请阴影图。
            {
                return; // 直接结束这个 Pass，避免无意义的临时 RT 分配。
            }

            var shadowData = BurtShadowUtility.ResolveMainLightShadowData(request, context.Asset); // 从 request 中安全读取主光阴影参数。

            var shadowMapTarget = context.MainLightShadowMapTarget; // 从 GraphContext 中取出 MainLightShadowMap 资源句柄。

            if (!shadowMapTarget.IsValid) // 如果 MainLightShadowMap 句柄无效，说明资源表没有注册阴影图。
            {
                return; // 直接结束这个 Pass，避免申请一个后续 Pass 无法找到的 RT。
            }

            var descriptor = BurtRenderTargetDescriptorUtility.CreateMainLightShadowMapDescriptor(shadowData); // 根据主光阴影数据创建 shadow map RT 描述。

            var cmd = CommandBufferPool.Get(Name); // 从 Unity 命令缓冲池获取一个 CommandBuffer，并用 Pass 名称命名它。

            cmd.GetTemporaryRT(BurtRenderGraphResourceRegistry.MainLightShadowMapId, descriptor, FilterMode.Bilinear); // 使用双线性过滤，让硬件阴影采样器能平滑比较边缘，避免点采样放大阴影条带。

            cmd.SetRenderTarget(shadowMapTarget.Identifier); // 把主光阴影图绑定为当前渲染目标，为后续 ShadowCaster 绘制做准备。
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, descriptor.width, descriptor.height);

            cmd.ClearRenderTarget(true, false, Color.clear, BurtShadowRenderTargetUtility.ResolveMainLightShadowClearDepth()); // 清理到当前 Z 方向的 far plane，避免 reversed-Z 下空 shadow map 被清成“全遮挡/全通过”的错误深度。

            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.MainLightShadowMapId, shadowMapTarget.Identifier); // 把主光阴影图暴露成全局纹理，后续 Lit shader 会通过它采样阴影。

            renderContext.ExecuteCommandBuffer(cmd); // 把申请、绑定和清理 shadow map 的命令提交给 ScriptableRenderContext。

            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。
        }
    }

    internal sealed class BurtDrawMainLightShadowCasterPass : BurtRenderPass // 定义主光阴影投射 Pass，负责把可投影物体写入 MainLightShadowMap。
    {
        private static readonly int MainLightDirectionId = Shader.PropertyToID("_BurtMainLightDirection"); // 缓存主光方向属性 ID，ShadowCaster 顶点偏移需要用它计算法线和光向夹角。

        private static readonly int MainLightWorldToShadowId = Shader.PropertyToID("_BurtMainLightWorldToShadow"); // 缓存世界空间到主光阴影纹理空间矩阵的 shader 属性 ID。

        private static readonly int MainLightWorldToShadowRow0Id = Shader.PropertyToID("_BurtMainLightWorldToShadowRow0");
        private static readonly int MainLightWorldToShadowRow1Id = Shader.PropertyToID("_BurtMainLightWorldToShadowRow1");
        private static readonly int MainLightWorldToShadowRow2Id = Shader.PropertyToID("_BurtMainLightWorldToShadowRow2");
        private static readonly int MainLightWorldToShadowRow3Id = Shader.PropertyToID("_BurtMainLightWorldToShadowRow3");

        private static readonly int MainLightShadowStrengthId = Shader.PropertyToID("_BurtMainLightShadowStrength"); // 缓存主光阴影强度属性 ID，阴影绘制失败时会把它清零避免采样旧图。

        private static readonly int MainLightShadowTexelSizeId = Shader.PropertyToID("_BurtMainLightShadowTexelSize"); // 缓存 shadow map texel size 属性 ID，receiver 端软阴影采样会用它偏移 UV。

        private static readonly int MainLightShadowSampleBiasId = Shader.PropertyToID("_BurtMainLightShadowSampleBias"); // 缓存接收端采样 bias 属性 ID，替代 shader 内部硬编码偏移。

        private static readonly int MainLightShadowDepthBiasId = Shader.PropertyToID("_BurtMainLightShadowDepthBias"); // 缓存 ShadowCaster 顶点 depth bias 属性 ID，让 shader 沿光向偏移 caster。

        private static readonly int MainLightShadowNormalBiasId = Shader.PropertyToID("_BurtMainLightShadowNormalBias"); // 缓存 ShadowCaster 顶点 normal bias 属性 ID，让 shader 使用资产参数做几何偏移。

        private static readonly int MainLightShadowSoftnessId = Shader.PropertyToID("_BurtMainLightShadowSoftness"); // 缓存软阴影开关属性 ID，让 Light 的 Hard/Soft 设置能影响 receiver 采样。

        public override string Name => "Burt Draw Main Light Shadow Caster"; // 返回这个 Pass 的名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            if (!BurtShadowUtility.ShouldUseMainLightShadow(builder.Request, builder.Asset)) // 如果当前 request 没有可用主光阴影，就不声明阴影图写入。
            {
                return; // 直接结束资源声明，避免 Debug 输出无效的 MainLightShadowMap 依赖。
            }

            builder.WriteMainLightShadowMap(); // 声明这个 Pass 会把 ShadowCaster 深度写入 MainLightShadowMap。

            builder.WriteShadowGlobals(); // 声明这个 Pass 会覆盖 shadow matrix、shadow strength、texel size 等阴影全局状态。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现主光阴影投射 Pass 的执行函数。
        {
            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文，用来提交命令和绘制阴影。

            var request = context.Request; // 从 GraphContext 中取出当前渲染请求，用来访问相机、剔除结果和灯光数据。

            if (!BurtShadowUtility.ShouldUseMainLightShadow(request, context.Asset)) // 如果当前 request 不需要主光阴影，就不执行阴影绘制。
            {
                return; // 直接结束这个 Pass，避免无意义的矩阵计算和 DrawShadows 调用。
            }

            var camera = request.Camera; // 从 request 中取出当前相机，阴影绘制后需要用它恢复相机矩阵状态。

            if (camera == null) // 如果相机为空，说明当前 request 状态异常。
            {
                return; // 直接结束这个 Pass，避免后面恢复相机状态时空引用。
            }

            var shadowData = BurtShadowUtility.ResolveMainLightShadowData(request, context.Asset); // 从 request 中安全读取主光阴影参数。

            if (shadowData == null) // 如果阴影数据为空，说明 request 没有有效灯光阴影信息。
            {
                return; // 直接结束这个 Pass，避免访问无效的主光索引或分辨率。
            }

            var shadowMapTarget = context.MainLightShadowMapTarget; // 从 GraphContext 中取出 MainLightShadowMap 资源句柄。

            if (!shadowMapTarget.IsValid) // 如果阴影图句柄无效，说明 RenderGraph 没有注册这个资源。
            {
                return; // 直接结束这个 Pass，避免把 ShadowCaster 画到错误目标。
            }

            if (!BurtMainLightShadowMatrixUtility.TryGetMainLightShadowMatrices(request, shadowData, out var viewMatrix, out var projectionMatrix, out var splitData)) // 计算主光视角的阴影视图矩阵、投影矩阵和裁剪数据。
            {
                DisableMainLightShadowReceiverGlobals(renderContext); // 计算失败时主动关闭接收端阴影，避免 Lit shader 继续使用上一帧的矩阵和 shadow map。

                return; // 如果 Unity 无法计算阴影矩阵，说明当前主光或剔除数据不足，直接跳过阴影绘制。
            }

            var worldToShadowMatrix = BurtMainLightShadowMatrixUtility.CreateWorldToShadowMatrix(viewMatrix, projectionMatrix); // 预先构造世界到 shadow map UV/depth 空间的矩阵，便于后续一次性上传。

            var shadowTexelSize = BurtShadowUtility.CreateMainLightShadowTexelSize(shadowData); // 根据最终阴影分辨率计算 texel size，软阴影和调试视图都会使用。

            var mainLightDirection = ResolveMainLightDirection(request); // 读取当前 request 的主光方向，ShadowCaster 顶点偏移不能依赖上一帧残留的全局方向。

            var shadowDepthBias = ResolveMainLightShadowDepthBias(shadowData, projectionMatrix); // 将 Inspector 上的 depth bias 折算成世界空间距离，避免硬件 depth bias 的平台方向差异。

            var shadowNormalBias = ResolveMainLightShadowNormalBias(shadowData, projectionMatrix); // 将 Inspector 上的 normal bias 折算成世界空间距离，避免直接把 0.4 当作 0.4 米偏移。

            var shadowSoftness = shadowData.IsMainLightShadowSoft ? 1f : 0f; // 把 Light 的 Soft/Hard 阴影设置转换成 shader 侧的 0/1 开关。

            var cmd = CommandBufferPool.Get(Name); // 从 Unity 命令缓冲池获取一个 CommandBuffer，并用 Pass 名称命名它。

            cmd.SetRenderTarget(shadowMapTarget.Identifier); // 把 MainLightShadowMap 绑定为当前渲染目标，后续 ShadowCaster 深度会写到这里。
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, shadowData.MainLightShadowResolution, shadowData.MainLightShadowResolution);

            cmd.ClearRenderTarget(true, false, Color.clear, BurtShadowRenderTargetUtility.ResolveMainLightShadowClearDepth()); // 清理到当前 Z 方向的 far plane，保证 caster 深度测试能真正写入 shadow map。

            cmd.SetViewProjectionMatrices(viewMatrix, projectionMatrix); // 把 GPU 当前矩阵切到主光视角，让 ShadowCaster 从灯光方向渲染。

            cmd.SetGlobalVector(MainLightDirectionId, new Vector4(mainLightDirection.x, mainLightDirection.y, mainLightDirection.z, 0f)); // 上传当前主光方向，ShadowCaster shader 会用它判断法线偏移强度。

            SetMainLightWorldToShadow(cmd, worldToShadowMatrix); // 上传世界空间到阴影纹理空间的矩阵，后续 Lit shader 会用它采样 shadow map。

            cmd.SetGlobalVector(MainLightShadowTexelSizeId, shadowTexelSize); // 上传 shadow map texel size，让 receiver 端可以做可控的邻域采样。

            cmd.SetGlobalFloat(MainLightShadowDepthBiasId, shadowDepthBias); // 上传世界空间 depth bias，ShadowCaster 顶点 shader 会沿光线方向推开 caster。

            cmd.SetGlobalFloat(MainLightShadowNormalBiasId, shadowNormalBias); // 上传世界空间 normal bias，ShadowCaster 顶点 shader 会沿法线推开 caster。

            cmd.SetGlobalFloat(MainLightShadowSampleBiasId, shadowData.MainLightShadowSampleBias); // 上传接收端深度比较偏移，替代 shader 中固定 0.001 的写法。

            cmd.SetGlobalFloat(MainLightShadowSoftnessId, shadowSoftness); // 上传软阴影开关，让 Lit shader 按 Light 的 Hard/Soft 阴影设置选择采样数量。

            cmd.SetGlobalFloat(MainLightShadowStrengthId, shadowData.MainLightShadowStrength); // 上传阴影强度，确保阴影成功绘制后 receiver 端才会使用它。

            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.MainLightShadowMapId, shadowMapTarget.Identifier); // 再次绑定当前 request 的 shadow map，避免多相机时全局纹理残留。

            cmd.SetGlobalDepthBias(1f, 2.5f); // Match URP/HDRP shadow slices: hardware slope bias is still required to keep large coplanar receivers from self-shadowing.

            renderContext.ExecuteCommandBuffer(cmd); // 把绑定目标、清理深度和设置矩阵的命令提交给 ScriptableRenderContext。

            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。

            var shadowDrawingSettings = new ShadowDrawingSettings(request.CullingResults, shadowData.MainLightIndex, BatchCullingProjectionType.Orthographic); // 创建 Unity 阴影绘制设置，并显式声明主方向光阴影使用正交投影，避免调用已废弃的旧构造函数。

            shadowDrawingSettings.splitData = splitData; // 把 Unity 计算出的阴影裁剪数据交给 DrawShadows，避免绘制不在阴影范围内的物体。

            try // 使用 try/finally 保护渲染状态恢复，避免 DrawShadows 抛错后把深度 bias 留给后续颜色 Pass。
            {
                renderContext.DrawShadows(ref shadowDrawingSettings); // 绘制所有对主光投影的可见物体，shader 需要提供 LightMode=ShadowCaster 的 Pass。
            }
            finally // 无论阴影绘制是否成功，都要恢复状态。
            {
                ResetMainLightShadowCasterState(renderContext, camera); // 清掉 ShadowCaster 专用深度 bias，并把视图投影恢复到当前相机。
            }

            UploadMainLightShadowReceiverGlobals(renderContext, shadowMapTarget, worldToShadowMatrix, shadowTexelSize, shadowData, shadowSoftness); // SetupCameraProperties 之后再上传一次接收端状态，保证 Scene/Game 后续 shading 读到当前 shadow map 和矩阵。
        }

        private static void UploadMainLightShadowReceiverGlobals( // 在 shadow caster 绘制完成后恢复给 Lit/Deferred 采样的阴影全局状态。
            ScriptableRenderContext renderContext, // 接收 Unity SRP 渲染上下文，用来提交全局变量命令。
            BurtRenderTargetHandle shadowMapTarget, // 接收当前 request 的主光 shadow map 句柄。
            Matrix4x4 worldToShadowMatrix, // 接收世界空间到 shadow map 空间的矩阵。
            Vector4 shadowTexelSize, // 接收 shadow map texel size。
            BurtShadowData shadowData, // 接收当前主光阴影参数。
            float shadowSoftness) // 接收软阴影开关。
        {
            if (shadowData == null || !shadowMapTarget.IsValid) // 数据或纹理无效时不能打开接收端采样。
            {
                DisableMainLightShadowReceiverGlobals(renderContext); // 显式关闭，避免沿用上一相机状态。
                return;
            }

            var cmd = CommandBufferPool.Get("Burt Upload Main Light Shadow Receiver"); // 独立命令缓冲，Frame Debugger 中能看到接收端全局状态恢复。
            SetMainLightWorldToShadow(cmd, worldToShadowMatrix); // 重新上传 shadow matrix，避免相机属性恢复后接收端读到单位矩阵或旧矩阵。
            cmd.SetGlobalVector(MainLightShadowTexelSizeId, shadowTexelSize); // 重新上传 texel size，供硬/软阴影采样使用。
            cmd.SetGlobalFloat(MainLightShadowSampleBiasId, shadowData.MainLightShadowSampleBias); // 重新上传 receiver bias，保证 Deferred/Forward 使用当前资产配置。
            cmd.SetGlobalFloat(MainLightShadowSoftnessId, shadowSoftness); // 重新上传硬/软阴影选择。
            cmd.SetGlobalFloat(MainLightShadowStrengthId, shadowData.MainLightShadowStrength); // 在 shadow map 已经绘制完成后打开接收端阴影强度。
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.MainLightShadowMapId, shadowMapTarget.Identifier); // 重新绑定当前 request 的 shadow map，避免多相机全局纹理串用。
            renderContext.ExecuteCommandBuffer(cmd); // 提交接收端阴影状态。
            CommandBufferPool.Release(cmd); // 释放命令缓冲。
        }

        private static void ResetMainLightShadowCasterState(ScriptableRenderContext renderContext, Camera camera) // 恢复 ShadowCaster Pass 修改过的全局渲染状态。
        {
            var cmd = CommandBufferPool.Get("Burt Reset Main Light Shadow Caster State"); // 获取一个独立 CommandBuffer，避免复用已经提交的 shadow draw 命令。
            cmd.SetGlobalDepthBias(0f, 0f); // 把 ShadowCaster 阶段设置的深度偏移清零，避免影响后续相机颜色绘制。
            cmd.SetGlobalFloat(MainLightShadowDepthBiasId, 0f); // 清空 ShadowCaster 顶点 depth bias，避免下一个 request 没有阴影时沿用旧偏移。
            cmd.SetGlobalFloat(MainLightShadowNormalBiasId, 0f); // 清空 ShadowCaster 顶点 normal bias，避免下一个 request 没有阴影时沿用旧偏移。
            renderContext.ExecuteCommandBuffer(cmd); // 提交深度偏移恢复命令。
            CommandBufferPool.Release(cmd); // 释放恢复用 CommandBuffer，避免每帧分配。

            if (camera != null) // 如果当前 request 仍然有有效相机，就恢复相机矩阵。
            {
                renderContext.SetupCameraProperties(camera); // 阴影绘制修改了视图投影矩阵，这里恢复相机矩阵和 Unity 内置相机参数。
            }
        }

        private static void DisableMainLightShadowReceiverGlobals(ScriptableRenderContext renderContext) // 关闭 receiver 端主光阴影全局变量。
        {
            var cmd = CommandBufferPool.Get("Burt Disable Main Light Shadow Receiver"); // 获取一个 CommandBuffer，用来清理 shader 侧阴影状态。
            cmd.SetGlobalFloat(MainLightShadowStrengthId, 0f); // 把阴影强度清零，让 Lit shader 直接跳过 shadow map 采样。
            SetMainLightWorldToShadow(cmd, Matrix4x4.identity); // 把世界到阴影矩阵重置为单位矩阵，避免调试时看到上一帧矩阵。
            cmd.SetGlobalVector(MainLightShadowTexelSizeId, Vector4.zero); // 清空 texel size，避免软阴影采样使用上一张 shadow map 的尺寸。
            cmd.SetGlobalFloat(MainLightShadowSampleBiasId, 0f); // 清空采样 bias，保证无阴影状态完全可控。
            cmd.SetGlobalFloat(MainLightShadowSoftnessId, 0f); // 清空软阴影开关，避免无阴影时执行额外采样逻辑。
            renderContext.ExecuteCommandBuffer(cmd); // 提交全局阴影状态清理命令。
            CommandBufferPool.Release(cmd); // 释放清理用 CommandBuffer，避免每帧分配。
        }

        private static void SetMainLightWorldToShadow(CommandBuffer cmd, Matrix4x4 matrix)
        {
            cmd.SetGlobalMatrix(MainLightWorldToShadowId, matrix);
            cmd.SetGlobalVector(MainLightWorldToShadowRow0Id, matrix.GetRow(0));
            cmd.SetGlobalVector(MainLightWorldToShadowRow1Id, matrix.GetRow(1));
            cmd.SetGlobalVector(MainLightWorldToShadowRow2Id, matrix.GetRow(2));
            cmd.SetGlobalVector(MainLightWorldToShadowRow3Id, matrix.GetRow(3));
        }

        private static Vector3 ResolveMainLightDirection(BurtRenderRequest request) // 从当前 request 解析 ShadowCaster 顶点偏移使用的主光方向。
        {
            var lightingData = request != null ? request.LightingData : null; // request 缺失时不能访问灯光数据，需要走安全兜底。
            var direction = lightingData != null ? lightingData.MainLightDirection : Vector3.forward; // 正常情况下使用 BurtLightingData 已归一化的“点到光源”方向。

            if (direction.sqrMagnitude <= 0.0001f) // 防止异常零向量传进 shader 后让法线偏移计算失去方向参考。
            {
                return Vector3.forward; // 只作为极端兜底；有效主光阴影流程中通常不会走到这里。
            }

            return direction.normalized; // 再归一化一次，保证 shader 收到稳定方向而不是受外部数据长度影响。
        }

        private static float ResolveMainLightShadowNormalBias(BurtShadowData shadowData, Matrix4x4 projectionMatrix) // 把 Inspector normal bias 转成 ShadowCaster shader 可直接使用的世界空间偏移。
        {
            if (shadowData == null || shadowData.MainLightShadowResolution <= 0) // 没有有效阴影数据或分辨率时不能计算 texel 尺寸。
            {
                return 0f; // 返回 0 表示完全关闭顶点 normal bias。
            }

            var normalBias = Mathf.Max(0f, shadowData.MainLightShadowNormalBias); // 保护用户输入，避免负数把 caster 拉回表面造成 acne。

            if (normalBias <= 0f) // 用户显式把 normal bias 调成 0 时应完全禁用几何偏移。
            {
                return 0f; // 直接返回 0，shader 分支外的乘法也会得到无偏移结果。
            }

            var projectionWidth = Mathf.Abs(projectionMatrix.m00) > 0.00001f ? 2f / Mathf.Abs(projectionMatrix.m00) : 0f; // 从方向光正交投影矩阵反推出 shadow 覆盖的世界宽度。
            var projectionHeight = Mathf.Abs(projectionMatrix.m11) > 0.00001f ? 2f / Mathf.Abs(projectionMatrix.m11) : 0f; // 从方向光正交投影矩阵反推出 shadow 覆盖的世界高度。
            var worldTexelSize = Mathf.Max(projectionWidth, projectionHeight) / Mathf.Max(1, shadowData.MainLightShadowResolution); // normal bias 使用 texel 倍率语义，分辨率越高单 texel 世界距离越小。

            return -normalBias * worldTexelSize; // Match Unity/URP: normal bias is an inset caster offset, not an outward push that self-shadows receivers.
        }

        private static float ResolveMainLightShadowDepthBias(BurtShadowData shadowData, Matrix4x4 projectionMatrix) // 把 Inspector depth bias 转成沿光向的世界空间偏移。
        {
            if (shadowData == null || shadowData.MainLightShadowResolution <= 0)
            {
                return 0f;
            }

            var depthBias = Mathf.Max(0f, shadowData.MainLightShadowDepthBias);
            if (depthBias <= 0f)
            {
                return 0f;
            }

            var projectionWidth = Mathf.Abs(projectionMatrix.m00) > 0.00001f ? 2f / Mathf.Abs(projectionMatrix.m00) : 0f;
            var projectionHeight = Mathf.Abs(projectionMatrix.m11) > 0.00001f ? 2f / Mathf.Abs(projectionMatrix.m11) : 0f;
            var worldTexelSize = Mathf.Max(projectionWidth, projectionHeight) / Mathf.Max(1, shadowData.MainLightShadowResolution);
            return -depthBias * worldTexelSize;
        }
    }

    internal sealed class BurtSetupLightingPass : BurtRenderPass // 上传当前 request 的主光、环境光和阴影接收端全局参数。
    {
        private static readonly int MainLightDirectionId = Shader.PropertyToID("_BurtMainLightDirection"); // 缓存主光方向属性 ID，避免每帧字符串查找。
        private static readonly int MainLightColorId = Shader.PropertyToID("_BurtMainLightColor"); // 缓存主光颜色属性 ID，避免每帧字符串查找。
        private static readonly int AmbientLightColorId = Shader.PropertyToID("_BurtAmbientLightColor"); // 缓存环境光颜色属性 ID，避免每帧字符串查找。
        private static readonly int MainLightWorldToShadowId = Shader.PropertyToID("_BurtMainLightWorldToShadow"); // 缓存世界到主光阴影空间矩阵属性 ID，用来在无阴影时主动清理旧矩阵。
        private static readonly int MainLightWorldToShadowRow0Id = Shader.PropertyToID("_BurtMainLightWorldToShadowRow0");
        private static readonly int MainLightWorldToShadowRow1Id = Shader.PropertyToID("_BurtMainLightWorldToShadowRow1");
        private static readonly int MainLightWorldToShadowRow2Id = Shader.PropertyToID("_BurtMainLightWorldToShadowRow2");
        private static readonly int MainLightWorldToShadowRow3Id = Shader.PropertyToID("_BurtMainLightWorldToShadowRow3");
        private static readonly int MainLightShadowStrengthId = Shader.PropertyToID("_BurtMainLightShadowStrength"); // 缓存主光阴影强度属性 ID，后续 Lit shader 用它决定是否采样 shadow map。
        private static readonly int MainLightShadowTexelSizeId = Shader.PropertyToID("_BurtMainLightShadowTexelSize"); // 缓存 shadow map texel size 属性 ID，软阴影采样会使用。
        private static readonly int MainLightShadowSampleBiasId = Shader.PropertyToID("_BurtMainLightShadowSampleBias"); // 缓存接收端采样 bias 属性 ID，避免 shader 内部硬编码。
        private static readonly int MainLightShadowSoftnessId = Shader.PropertyToID("_BurtMainLightShadowSoftness"); // 缓存软阴影开关属性 ID，让 Light 的 Hard/Soft 设置进入 shader。

        public override string Name => "Burt Setup Lighting"; // 返回这个 Pass 的名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            builder.WriteLightingGlobals(); // 声明这个 Pass 会上传主光方向、主光颜色和环境光等灯光全局状态。

            builder.WriteShadowGlobals(); // 声明这个 Pass 会上传默认阴影接收端全局状态，避免上一帧或上一相机残留。
        }

        public override void Execute(BurtRenderGraphContext context) // 执行灯光和阴影接收端参数上传。
        {
            var renderContext = context.ScriptableContext; // 读取 Unity SRP 上下文，后面用它提交 CommandBuffer。
            var request = context.Request; // 读取当前渲染请求，后面用它取得预先解析好的灯光数据。
            var lightingData = ResolveLightingData(request); // 取得 request 级灯光数据；如果 request 异常则返回安全默认值。
            var asset = context.Asset; // 读取当前 BurtRenderPipelineAsset，后面用它合并主光阴影资产配置。
            var shadowData = BurtShadowUtility.ResolveMainLightShadowData(request, asset); // 读取合并 PipelineAsset 后的主光阴影数据，让资产开关和 bias 生效。
            var mainLightDirection = lightingData.MainLightDirection; // 读取主光世界空间方向。
            var mainLightColor = lightingData.MainLightColor; // 读取主光颜色。
            var ambientLightColor = lightingData.AmbientLightColor; // 读取 Unity Lighting 设置里的环境光颜色，SimpleLit 路径继续使用它。
            var mainLightShadowTexelSize = BurtShadowUtility.CreateMainLightShadowTexelSize(shadowData); // 计算 shadow map texel size，给 receiver 端软阴影采样使用。
            var mainLightShadowSampleBias = shadowData != null && shadowData.HasMainLightShadow ? shadowData.MainLightShadowSampleBias : 0f; // 只有有阴影时才上传采样 bias。
            var mainLightShadowSoftness = shadowData != null && shadowData.HasMainLightShadow && shadowData.IsMainLightShadowSoft ? 1f : 0f; // 把主光 Hard/Soft 阴影类型转换为 shader 可读的 0/1。
            var hasMainLightWorldToShadow = BurtMainLightShadowMatrixUtility.TryGetMainLightShadowMatrices(request, shadowData, out var setupShadowViewMatrix, out var setupShadowProjectionMatrix, out _);
            var mainLightWorldToShadow = hasMainLightWorldToShadow ? BurtMainLightShadowMatrixUtility.CreateWorldToShadowMatrix(setupShadowViewMatrix, setupShadowProjectionMatrix) : Matrix4x4.identity;
            var mainLightShadowStrength = hasMainLightWorldToShadow ? ResolveMainLightShadowStrength(shadowData) : 0f;
            var cmd = CommandBufferPool.Get(Name); // 从 Unity 命令缓冲池获取一个 CommandBuffer，并用 Pass 名称命名它。
            cmd.SetGlobalVector(MainLightDirectionId, new Vector4(mainLightDirection.x, mainLightDirection.y, mainLightDirection.z, 0f)); // 上传归一化的主光方向，Lit shader 用它计算 Lambert 漫反射。
            cmd.SetGlobalColor(MainLightColorId, mainLightColor); // 上传主光颜色，Lit shader 会把它乘到直接光上。
            cmd.SetGlobalColor(AmbientLightColorId, ambientLightColor); // 上传环境光颜色，Lit shader 会用它保留阴影区域的基础亮度。
            BurtIndirectLightingUtility.UploadGlobalIndirectLighting(cmd, request != null ? request.Camera : null); // 上传 BurtRP 自己的全局间接光数据源，让 Deferred fullscreen pass 不依赖 Forward DrawRenderers 副作用。
            SetMainLightWorldToShadow(cmd, mainLightWorldToShadow); // 上传当前 request 的 shadow matrix，避免 Deferred 读到默认 identity。
            cmd.SetGlobalFloat(MainLightShadowStrengthId, mainLightShadowStrength); // 上传最终阴影强度，0 表示 receiver 完全跳过 shadow map 采样。
            cmd.SetGlobalVector(MainLightShadowTexelSizeId, mainLightShadowTexelSize); // 上传 shadow map texel size，软阴影采样会根据它偏移邻域 UV。
            cmd.SetGlobalFloat(MainLightShadowSampleBiasId, mainLightShadowSampleBias); // 上传接收端采样 bias，替代 shader 内的固定 0.001 偏移。
            cmd.SetGlobalFloat(MainLightShadowSoftnessId, mainLightShadowSoftness); // 上传软阴影开关，Hard 阴影只做中心点比较，Soft 阴影做简单邻域采样。
            renderContext.ExecuteCommandBuffer(cmd); // 把灯光和阴影全局参数提交给 Unity 渲染上下文。
            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。
        }

        private static void SetMainLightWorldToShadow(CommandBuffer cmd, Matrix4x4 matrix)
        {
            cmd.SetGlobalMatrix(MainLightWorldToShadowId, matrix);
            cmd.SetGlobalVector(MainLightWorldToShadowRow0Id, matrix.GetRow(0));
            cmd.SetGlobalVector(MainLightWorldToShadowRow1Id, matrix.GetRow(1));
            cmd.SetGlobalVector(MainLightWorldToShadowRow2Id, matrix.GetRow(2));
            cmd.SetGlobalVector(MainLightWorldToShadowRow3Id, matrix.GetRow(3));
        }

        private static BurtLightingData ResolveLightingData(BurtRenderRequest request) // 安全读取 request 里的灯光数据。
        {
            if (request == null) // 如果调用方没有提供 request，就没有可读取的灯光上下文。
            {
                return BurtLightingData.Default(); // 返回默认灯光数据，保证 shader 仍能收到有效全局变量。
            }

            if (request.LightingData == null) // 如果 request 创建阶段没有附加 LightingData，就使用兜底数据。
            {
                return BurtLightingData.Default(); // 返回默认灯光数据，避免空引用。
            }

            return request.LightingData; // 返回 request 创建阶段收集好的灯光数据。
        }

        private static float ResolveMainLightShadowStrength(BurtShadowData shadowData) // 从合并后的阴影数据中读取最终阴影强度。
        {
            if (shadowData == null) // 如果没有阴影数据，说明当前 request 无法产生主光阴影。
            {
                return 0f; // 返回 0 表示关闭阴影，避免 shader 采样无效 shadow map。
            }

            if (!shadowData.HasMainLightShadow) // 如果 Light 或 PipelineAsset 关闭了阴影，就不应该产生阴影衰减。
            {
                return 0f; // 返回 0 表示关闭阴影。
            }

            return shadowData.MainLightShadowStrength; // 返回最终阴影强度，让 shader 按 Light 的 Shadow Strength 混合阴影。
        }
    }

    internal sealed class BurtDepthPrepass : BurtRenderPass // 定义深度预写 Pass，负责在颜色绘制前先写入 CameraDepth。
    {
        public override string Name => "Burt Depth Prepass"; // 返回这个 Pass 的名称，方便 Frame Debugger 和 RenderGraph Debug 输出识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            builder.WriteCameraDepth(); // 声明这个 Pass 只写 CameraDepth，不写 CameraColor。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现深度预写 Pass 的执行函数。
        {
            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。

            var request = context.Request; // 从 GraphContext 中取出当前渲染请求。

            var camera = request.Camera; // 从 request 中取出当前相机，用来创建排序设置。

            var cameraDepthTarget = context.CameraDepthTarget; // 从 GraphContext 中取出 CameraDepth 资源句柄。

            if (!cameraDepthTarget.IsValid) // 如果 CameraDepth 无效，说明当前图没有可写入的深度资源。
            {
                return; // 直接结束这个 Pass，避免无效深度预写。
            }

            var sortingSettings = new SortingSettings(camera); // 创建排序设置，Unity 会根据相机信息计算排序参数。

            sortingSettings.criteria = SortingCriteria.CommonOpaque; // 深度预写只处理不透明物体，使用 CommonOpaque 有利于前到后减少 overdraw。

            var drawingSettings = BurtDrawingSettingsUtility.CreateDepthDrawingSettings(sortingSettings); // 创建只匹配 BurtDepthOnly shader pass 的绘制设置。

            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque); // 创建过滤设置，只允许渲染队列属于 opaque 范围的物体通过。

            renderContext.DrawRenderers(request.CullingResults, ref drawingSettings, ref filteringSettings); // 使用 request 的剔除结果绘制可见不透明物体的深度。
        }
    }

    internal sealed class BurtDrawOpaquePass : BurtRenderPass // 定义不透明物体颜色绘制 Pass。
    {
        public override string Name => "Burt Draw Opaque"; // 返回这个 Pass 的名称，后面可以用于调试和性能分析。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            var depthPrepassEnabled = builder.Asset == null || builder.Asset.EnableDepthPrepass; // 判断当前图是否包含 Depth Prepass，asset 为空时沿用 Assembler 的默认开启规则。

            if (depthPrepassEnabled) // 如果前面已经有 Depth Prepass，这个颜色 Pass 就会读取已经写好的 CameraDepth 做深度测试。
            {
                builder.ReadCameraDepth(); // 声明这个 Pass 会读取 Depth Prepass 写好的 CameraDepth。
            }

            builder.ReadLightingGlobals(); // 声明不透明前向着色会读取 Setup Lighting 上传的灯光全局状态。

            builder.ReadShadowGlobals(); // 声明不透明前向着色会读取阴影矩阵、强度、texel size 等阴影全局状态。

            if (BurtShadowUtility.ShouldUseMainLightShadow(builder.Request, builder.Asset)) // 如果当前 request 真的生成主光阴影图，就把 shadow map 声明为着色输入。
            {
                builder.ReadMainLightShadowMap(); // 声明不透明前向着色会采样 MainLightShadowMap。
            }

            builder.WriteCameraColor(); // 声明这个 Pass 会把不透明物体颜色写入 CameraColor。

            builder.WriteCameraDepth(); // 声明这个 Pass 当前仍可能通过 ZWrite 更新 CameraDepth。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现 BurtRenderPass 的执行函数。
        {
            if (!BurtDrawingSettingsUtility.BindCameraColorAndDepth(context, Name))
            {
                return;
            }

            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。

            var request = context.Request; // 从 GraphContext 中取出当前渲染请求。

            var camera = request.Camera; // 从 request 中取出当前相机，用来创建排序设置。

            var sortingSettings = new SortingSettings(camera); // 创建排序设置，Unity 会根据相机信息计算排序参数。

            sortingSettings.criteria = SortingCriteria.CommonOpaque; // 设置不透明物体排序规则，通常有利于 early-z 和减少 overdraw。

            var drawingSettings = BurtDrawingSettingsUtility.CreateForwardDrawingSettings(sortingSettings); // 创建前向颜色绘制设置，匹配 BurtForward 等颜色 Pass。

            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque); // 创建过滤设置，只允许渲染队列属于 opaque 范围的物体通过。

            renderContext.DrawRenderers(request.CullingResults, ref drawingSettings, ref filteringSettings); // 使用 request 的剔除结果绘制所有可见不透明物体。
        }
    }

    internal sealed class BurtDrawDeferredForwardOnlyOpaquePass : BurtRenderPass // 定义 Deferred 后的前向兜底不透明 Pass，只绘制显式标记为 BurtForwardOnly 的物体。
    {
        public override string Name => "Burt Draw Deferred Forward Only Opaque"; // 返回这个 Pass 的名称，方便在 RenderGraph Debug 和 Frame Debugger 里确认它不是普通 Forward Opaque。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            builder.ReadCameraDepth(); // 声明前向兜底物体会读取 Deferred/GBuffer 阶段已经建立好的 CameraDepth 做深度测试。

            builder.ReadLightingGlobals(); // 声明前向兜底 shader 会读取主光颜色、方向等灯光全局状态。

            builder.ReadShadowGlobals(); // 声明前向兜底 shader 会读取阴影矩阵、强度和 texel size 等阴影全局状态。

            if (BurtShadowUtility.ShouldUseMainLightShadow(builder.Request, builder.Asset)) // 如果当前 request 真的生成主光阴影图，就声明 shadow map 输入。
            {
                builder.ReadMainLightShadowMap(); // 声明前向兜底 shader 会采样 MainLightShadowMap。
            }

            builder.WriteCameraColor(); // 声明前向兜底结果会写回 Deferred Lighting 已经生成的 CameraColor。

            builder.WriteCameraDepth(); // 声明兜底不透明物体仍可能写入深度，保证后续 Skybox 和 Transparent 能按最终深度测试。
        }

        public override void Execute(BurtRenderGraphContext context) // 执行 Deferred 后的前向兜底不透明绘制。
        {
            if (!BurtDrawingSettingsUtility.BindCameraColorAndDepth(context, Name))
            {
                return;
            }

            var renderContext = context.ScriptableContext; // 从 GraphContext 取出 Unity SRP 渲染上下文。

            var request = context.Request; // 从 GraphContext 取出当前渲染请求。

            var camera = request.Camera; // 从 request 取出当前相机，用来创建排序设置。

            var sortingSettings = new SortingSettings(camera); // 创建排序设置，Unity 会根据相机矩阵和位置计算排序参数。

            sortingSettings.criteria = SortingCriteria.CommonOpaque; // 使用不透明排序，尽量保持 early-z 和稳定绘制顺序。

            var drawingSettings = BurtDrawingSettingsUtility.CreateForwardOnlyDrawingSettings(sortingSettings); // 只匹配 LightMode=BurtForwardOnly 的 shader，避免重复绘制 BurtGBuffer/BurtForward 材质。

            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque); // 只允许不透明队列通过，透明仍交给后面的 Draw Transparent Pass。

            renderContext.DrawRenderers(request.CullingResults, ref drawingSettings, ref filteringSettings); // 绘制所有显式声明 BurtForwardOnly 的可见不透明物体。
        }
    }

    internal sealed class BurtDrawEditorPreviewPass : BurtRenderPass // 定义编辑器 Preview 专用绘制 Pass，兼容 Unity 内部资产预览 shader。
    {
        public override string Name => "Burt Draw Editor Preview"; // 返回这个 Pass 的名称，方便 RenderGraph Debug 识别 Preview 是否走专用路径。

        public override void Configure(BurtRenderPassBuilder builder) // 声明 Preview 绘制会使用的资源。
        {
            builder.ReadLightingGlobals(); // BurtRP 材质预览仍可能需要基础灯光全局状态。

            builder.ReadShadowGlobals(); // 读取 Setup Lighting 写入的默认阴影状态，避免 shader 读到上一相机残留。

            if (BurtShadowUtility.ShouldUseMainLightShadow(builder.Request, builder.Asset)) // 如果 Preview 场景真的生成了主光阴影，就声明 shadow map 依赖。
            {
                builder.ReadMainLightShadowMap(); // 让 Preview 中的 BurtRP lit 材质可以保持和 Forward 路径一致。
            }

            builder.WriteCameraColor(); // Preview 结果写入中间颜色，后续仍统一交给 FinalBlit 输出。

            builder.WriteCameraDepth(); // Preview 内部物体需要正常深度测试和写入。
        }

        public override void Execute(BurtRenderGraphContext context) // 执行编辑器 Preview 绘制。
        {
            var renderContext = context.ScriptableContext; // 读取 Unity SRP 上下文，用来提交渲染目标绑定和 DrawRenderers。

            var request = context.Request; // 读取当前 request，用来访问相机和剔除结果。

            var camera = request.Camera; // Preview 相机来自 Unity 编辑器内部。

            var cameraColorTarget = context.CameraColorTarget; // 读取 Preview 中间颜色目标。

            var cameraDepthTarget = context.CameraDepthTarget; // 读取 Preview 中间深度目标。

            if (!cameraColorTarget.IsValid || !cameraDepthTarget.IsValid) // 任一目标无效时不能安全绘制。
            {
                return; // 直接跳过，避免把 Unity 内部预览画到错误目标。
            }

            var cmd = CommandBufferPool.Get(Name); // 重新绑定目标，防止前序内部预览状态影响 DrawRenderers。

            cmd.SetRenderTarget(cameraColorTarget.Identifier, cameraDepthTarget.Identifier); // 使用 BurtRP 当前 request 的颜色和深度目标。
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);

            renderContext.ExecuteCommandBuffer(cmd); // 提交绑定命令。

            CommandBufferPool.Release(cmd); // 释放 CommandBuffer，避免每帧分配。

            DrawPreviewRenderers(renderContext, request, camera, RenderQueueRange.opaque, SortingCriteria.CommonOpaque); // 先绘制不透明预览物体。

            DrawPreviewRenderers(renderContext, request, camera, RenderQueueRange.transparent, SortingCriteria.CommonTransparent); // 再绘制透明预览物体。
        }

        private static void DrawPreviewRenderers( // 绘制一段指定队列范围的 Preview renderer。
            ScriptableRenderContext renderContext, // 接收 Unity 渲染上下文。
            BurtRenderRequest request, // 接收当前 request，用来读取剔除结果。
            Camera camera, // 接收当前 Preview 相机。
            RenderQueueRange renderQueueRange, // 接收要绘制的不透明或透明队列范围。
            SortingCriteria sortingCriteria) // 接收对应队列的排序规则。
        {
            var sortingSettings = new SortingSettings(camera); // 基于 Preview 相机创建排序设置。

            sortingSettings.criteria = sortingCriteria; // 使用调用方指定的不透明或透明排序。

            var drawingSettings = BurtDrawingSettingsUtility.CreateEditorPreviewDrawingSettings(sortingSettings); // 使用 Preview 专用的宽松 LightMode 列表。

            var filteringSettings = new FilteringSettings(renderQueueRange); // 只绘制当前队列范围，保证透明物体顺序在不透明之后。

            renderContext.DrawRenderers(request.CullingResults, ref drawingSettings, ref filteringSettings); // 绘制 Unity 内部资产预览和 BurtRP 材质预览物体。
        }
    }

    internal sealed class BurtDrawSkyboxPass : BurtRenderPass // 定义天空盒绘制 Pass。
    {
        public override string Name => "Burt Draw Skybox"; // 返回这个 Pass 的名称，后面可以用于调试和性能分析。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            builder.ReadCameraDepth(); // 声明这个 Pass 会参考当前深度状态来把天空盒放在场景背景位置。

            builder.WriteCameraColor(); // 声明这个 Pass 在 Skybox 模式下会把天空盒颜色写入 CameraColor。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现 BurtRenderPass 的执行函数。
        {
            if (!BurtDrawingSettingsUtility.BindCameraColorAndDepth(context, Name))
            {
                return;
            }

            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。

            var request = context.Request; // 从 GraphContext 中取出当前渲染请求。

            var camera = request.Camera; // 从 request 中取出当前相机，用来绘制天空盒。

            if (camera == null) // 如果当前相机为空，就没有办法绘制天空盒。
            {
                return; // 直接结束这个 Pass。
            }

            var clearMode = BurtCameraClearUtility.ResolveClearMode(request); // 统一解析清屏模式，让 SceneView 没有 BurtCameraData 时也能识别 Unity 的 Skybox clearFlags。

            if (clearMode != BurtCameraClearMode.Skybox) // 如果当前清屏模式不是 Skybox，就不绘制天空盒。
            {
                return; // 直接结束这个 Pass。
            }

            renderContext.DrawSkybox(camera); // 使用 Unity SRP 上下文绘制当前相机的天空盒。
        }
    }

    internal sealed class BurtDrawTransparentPass : BurtRenderPass // 定义透明物体绘制 Pass。
    {
        public override string Name => "Burt Draw Transparent"; // 返回这个 Pass 的名称，后面可以用于调试和性能分析。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            builder.ReadCameraColor(); // 声明透明物体需要读取已有 CameraColor 作为混合背景。

            builder.ReadCameraDepth(); // 声明透明物体需要读取当前 CameraDepth 参与深度测试。

            builder.ReadLightingGlobals(); // 声明透明前向着色会读取 Setup Lighting 上传的灯光全局状态。

            builder.ReadShadowGlobals(); // 声明透明前向着色会读取阴影矩阵、强度、texel size 等阴影全局状态。

            if (BurtShadowUtility.ShouldUseMainLightShadow(builder.Request, builder.Asset)) // 如果当前 request 真的生成主光阴影图，就把 shadow map 声明为透明着色输入。
            {
                builder.ReadMainLightShadowMap(); // 声明透明前向着色会采样 MainLightShadowMap。
            }

            builder.WriteCameraColor(); // 声明透明混合结果会写回 CameraColor。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现 BurtRenderPass 的执行函数。
        {
            if (!BurtDrawingSettingsUtility.BindCameraColorAndDepth(context, Name))
            {
                return;
            }

            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。

            var request = context.Request; // 从 GraphContext 中取出当前渲染请求。

            var camera = request.Camera; // 从 request 中取出当前相机，用来创建排序设置。

            var sortingSettings = new SortingSettings(camera); // 创建排序设置，Unity 会根据相机信息计算透明排序参数。

            sortingSettings.criteria = SortingCriteria.CommonTransparent; // 设置透明物体排序规则，通常从后往前绘制以保证混合正确。

            var drawingSettings = BurtDrawingSettingsUtility.CreateForwardDrawingSettings(sortingSettings); // 创建前向颜色绘制设置，匹配 BurtForward 等颜色 Pass。

            var filteringSettings = new FilteringSettings(RenderQueueRange.transparent); // 创建过滤设置，只允许渲染队列属于 transparent 范围的物体通过。

            renderContext.DrawRenderers(request.CullingResults, ref drawingSettings, ref filteringSettings); // 使用 request 的剔除结果绘制所有可见透明物体。
        }
    }

    internal sealed class BurtDrawUnsupportedShadersPass : BurtRenderPass // 用错误材质绘制非 BurtRP shader，让不支持的材质在画面中明显暴露。
    {
        private const string ErrorShaderName = "Hidden/InternalErrorShader"; // 保存 Unity 内置错误 shader 名称。

        private Material errorMaterial; // 缓存运行时错误材质，避免每帧重复创建。

        private bool hasLoggedMissingErrorShader; // 记录缺失错误 shader 的警告是否已经打印，避免 Console 刷屏。

        public override string Name => "Burt Draw Unsupported Shaders"; // 返回这个 Pass 的名称，方便 RenderGraph Debug 和 Frame Debugger 标记。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 使用的资源。
        {
            builder.ReadCameraDepth(); // 声明错误材质绘制会读取当前 CameraDepth 做深度测试。

            builder.WriteCameraColor(); // 声明错误材质绘制会把可见像素写入 CameraColor。
        }

        public override void Execute(BurtRenderGraphContext context) // 执行不支持 shader 的调试绘制。
        {
            var renderContext = context.ScriptableContext; // 读取 Unity SRP 上下文，用来提交命令并绘制 renderer。

            var request = context.Request; // 读取当前渲染请求，用来访问剔除结果和相机。

            var camera = request.Camera; // 读取当前相机，用来创建排序设置。

            var cameraColorTarget = context.CameraColorTarget; // 读取接收错误材质输出的 CameraColor 目标。

            var cameraDepthTarget = context.CameraDepthTarget; // 读取控制错误材质深度测试的 CameraDepth 目标。

            if (!cameraColorTarget.IsValid) // 检查 RenderGraph 是否注册了有效颜色目标。
            {
                return; // 没有安全的颜色目标时直接结束，避免错误材质画到无效 RT。
            }

            if (!cameraDepthTarget.IsValid) // 检查 RenderGraph 是否注册了有效深度目标。
            {
                return; // 没有当前深度目标时直接结束，避免错误材质使用错误深度状态。
            }

            var material = GetErrorMaterial(); // 获取缓存的 Unity 错误材质，第一次使用时会创建。

            if (material == null) // 检查错误材质是否创建失败。
            {
                return; // 没有有效 override material 时直接结束，避免 DrawRenderers 报错。
            }

            var cmd = CommandBufferPool.Get(Name); // 从命令缓冲池获取一个以当前 Pass 命名的 CommandBuffer。

            cmd.SetRenderTarget(cameraColorTarget.Identifier, cameraDepthTarget.Identifier); // 在绘制不支持 shader 前重新绑定当前 request 的颜色和深度目标。
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);

            renderContext.ExecuteCommandBuffer(cmd); // 把渲染目标绑定命令提交给 Unity 渲染上下文。

            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回 Unity 池，避免每帧分配。

            var sortingSettings = new SortingSettings(camera); // 基于当前相机创建排序设置。

            sortingSettings.criteria = SortingCriteria.CommonOpaque; // 使用稳定的不透明排序，保证调试覆盖绘制顺序可预测。

            var drawingSettings = BurtDrawingSettingsUtility.CreateUnsupportedDrawingSettings(sortingSettings, material); // 创建会匹配已知非 BurtRP LightMode 的 DrawingSettings。

            var filteringSettings = FilteringSettings.defaultValue; // 使用默认过滤，让任意队列中的不支持 shader 都有机会被报告。

            renderContext.DrawRenderers(request.CullingResults, ref drawingSettings, ref filteringSettings); // 绘制所有 shader pass 不被 BurtRP 支持的可见 renderer。
        }

        private Material GetErrorMaterial() // 获取或创建用于绘制不支持 shader 的错误材质。
        {
            if (errorMaterial != null) // 检查错误材质是否已经创建。
            {
                return errorMaterial; // 返回缓存的材质实例。
            }

            var shader = Shader.Find(ErrorShaderName); // 通过名称查找 Unity 内置错误 shader。

            if (shader == null) // 检查错误 shader 查找是否失败。
            {
                if (!hasLoggedMissingErrorShader) // 检查这个警告是否已经输出过。
                {
                    Debug.LogWarning("BurtRP could not find shader: " + ErrorShaderName); // 输出一次警告，方便诊断内置错误 shader 缺失。

                    hasLoggedMissingErrorShader = true; // 标记警告已经打印，避免 Console 刷屏。
                }

                return null; // 返回 null，让调用方安全跳过不支持 shader 绘制。
            }

            errorMaterial = new Material(shader); // 使用 Unity 错误 shader 创建运行时材质。

            errorMaterial.hideFlags = HideFlags.HideAndDontSave; // 隐藏运行时材质，并防止它被保存进资源或场景。

            return errorMaterial; // 返回缓存后的运行时错误材质。
        }
    }

    internal sealed class BurtDebugCameraDepthPass : BurtRenderPass // 定义 CameraDepth 调试 Pass，负责把深度 RT 可视化到 CameraColor。
    {
        private const string DebugDepthShaderName = "Hidden/BurtRP/DebugCameraDepth"; // 定义调试深度 shader 的查找名称，必须和 shader 文件里的 Shader 名称一致。

        private static readonly int DepthDebugScaleId = Shader.PropertyToID("_BurtDepthDebugScale"); // 缓存深度调试缩放属性 ID，避免每帧通过字符串查找。

        private static readonly int DepthDebugYFlipId = Shader.PropertyToID("_BurtDepthDebugYFlip"); // 缓存深度调试 Y 预翻转属性 ID，避免每帧通过字符串查找。

        private Material debugDepthMaterial; // 缓存调试深度材质，避免每帧重复创建 Material。

        private bool hasLoggedMissingShader; // 记录是否已经输出过缺失 shader 警告，避免 Console 每帧刷屏。

        public override string Name => "Burt Debug Camera Depth"; // 返回这个 Pass 的名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            builder.ReadCameraDepth(); // 声明这个 Pass 会读取前面绘制阶段写好的 CameraDepth。

            builder.WriteCameraColor(); // 声明这个 Pass 会把深度可视化结果写回 CameraColor。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现深度可视化 Pass 的执行函数。
        {
            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。

            var asset = context.Asset; // 从 GraphContext 中取出当前管线资产，用来读取调试参数。

            var cameraColorTarget = context.CameraColorTarget; // 从 GraphContext 中取出 CameraColor 资源句柄，调试结果会写到这里。

            var cameraDepthTarget = context.CameraDepthTarget; // 从 GraphContext 中取出 CameraDepth 资源句柄，调试 shader 会采样它。

            if (!cameraColorTarget.IsValid) // 如果 CameraColor 无效，说明当前图没有可写入的颜色目标。
            {
                return; // 直接结束这个 Pass，避免绑定无效颜色目标。
            }

            if (!cameraDepthTarget.IsValid) // 如果 CameraDepth 无效，说明当前图没有可读取的深度目标。
            {
                return; // 直接结束这个 Pass，避免 shader 采样无效深度纹理。
            }

            var material = GetDebugDepthMaterial(); // 获取或创建深度调试材质。

            if (material == null) // 如果材质为空，说明 shader 没有找到或者创建失败。
            {
                return; // 直接结束这个 Pass，避免向 CommandBuffer 提交无效绘制。
            }

            var depthDebugScale = asset != null ? asset.DepthDebugScale : 50f; // 从资产读取深度显示缩放，资产为空时使用默认值。

            material.SetFloat(DepthDebugScaleId, depthDebugScale); // 把深度显示缩放传给 shader，用来增强深度灰度对比。

            var depthDebugYFlip = BurtFinalBlitUtility.ResolveFinalBlitYFlip(context.Request); // 复用最终输出的翻转规则，因为深度调试图写进 CameraColor 后还会经过 FinalBlit。

            material.SetFloat(DepthDebugYFlipId, depthDebugYFlip); // 把预翻转开关传给深度调试 shader，防止最终屏幕里的调试图上下颠倒。

            var cmd = CommandBufferPool.Get(Name); // 从 Unity 命令缓冲池获取一个 CommandBuffer，并用 Pass 名称命名它。

            cmd.SetRenderTarget(cameraColorTarget.Identifier); // 只绑定 CameraColor，因为这个全屏调试 Pass 不需要写入深度。
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, context.Request != null ? context.Request.Camera : null);

            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.CameraDepthTextureId, cameraDepthTarget.Identifier); // 确保 _BurtCameraDepthTexture 指向当前 request 的 CameraDepth。

            cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1); // 绘制一个全屏三角形，让 shader 把深度纹理转成灰度图。

            renderContext.ExecuteCommandBuffer(cmd); // 把调试绘制命令提交给 ScriptableRenderContext。

            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。
        }

        private Material GetDebugDepthMaterial() // 定义获取深度调试材质的内部辅助函数。
        {
            if (debugDepthMaterial != null) // 如果之前已经创建过材质，就直接复用它。
            {
                return debugDepthMaterial; // 返回缓存的调试材质。
            }

            var shader = Shader.Find(DebugDepthShaderName); // 通过 shader 名称查找深度调试 shader。

            if (shader == null) // 如果 shader 查找失败，说明 shader 文件没有被 Unity 导入或名称不一致。
            {
                if (!hasLoggedMissingShader) // 如果还没有输出过警告，就输出一次。
                {
                    Debug.LogWarning("BurtRP could not find shader: " + DebugDepthShaderName); // 输出缺失 shader 警告，方便定位资源问题。

                    hasLoggedMissingShader = true; // 标记警告已经输出过，避免每帧重复打印。
                }

                return null; // 返回空材质，调用方会跳过这个 Pass。
            }

            debugDepthMaterial = new Material(shader); // 使用找到的 shader 创建一个运行时材质。

            debugDepthMaterial.hideFlags = HideFlags.HideAndDontSave; // 隐藏运行时材质并避免它被保存到场景或资产中。

            return debugDepthMaterial; // 返回创建好的调试材质。
        }
    }

    internal sealed class BurtDebugMainLightShadowMapPass : BurtRenderPass // 定义主光 shadow map 调试 Pass，负责把阴影图画到 CameraColor。
    {
        private const string DebugShadowShaderName = "Hidden/BurtRP/DebugMainLightShadowMap"; // 定义调试 shadow map shader 的查找名称，必须和 shader 文件里的名称一致。
        private static readonly int ShadowDebugExposureId = Shader.PropertyToID("_BurtMainLightShadowDebugExposure"); // 缓存调试曝光属性 ID，避免每帧字符串查找。
        private static readonly int ShadowDebugYFlipId = Shader.PropertyToID("_BurtMainLightShadowDebugYFlip"); // 缓存 shadow debug 的 Y 预翻转属性 ID，避免每帧字符串查找。
        private Material debugShadowMaterial; // 缓存运行时调试材质，避免每帧重复创建 Material。
        private bool hasLoggedMissingShader; // 记录是否已经输出过缺失 shader 警告，避免 Console 每帧刷屏。
        public override string Name => "Burt Debug Main Light Shadow Map"; // 返回这个 Pass 的名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            if (!BurtShadowUtility.ShouldUseMainLightShadow(builder.Request, builder.Asset)) // 如果当前 request 没有有效主光阴影，就不声明 shadow map 依赖。
            {
                return; // 直接结束资源声明，避免 Debug 输出无效资源依赖。
            }

            if (builder.Asset == null || !builder.Asset.EnableMainLightShadowDebugView) // 如果资产没有开启 shadow map 调试视图，就不声明覆盖 CameraColor。
            {
                return; // 直接结束资源声明，保持正常渲染图干净。
            }

            builder.ReadMainLightShadowMap(); // 声明这个 Pass 会读取主光 shadow map。
            builder.WriteCameraColor(); // 声明这个 Pass 会把调试结果写回最终 CameraColor。
        }

        public override void Execute(BurtRenderGraphContext context) // 执行主光 shadow map 可视化。
        {
            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。
            var request = context.Request; // 从 GraphContext 中取出当前渲染请求，用来再次判断阴影是否有效。
            if (!BurtShadowUtility.ShouldUseMainLightShadow(request, context.Asset)) // 如果执行阶段发现阴影已无效，就跳过调试绘制。
            {
                return; // 直接结束，避免读取未申请的 shadow map。
            }

            var cameraColorTarget = context.CameraColorTarget; // 读取 CameraColor 目标，调试图会覆盖到这里。
            var shadowMapTarget = context.MainLightShadowMapTarget; // 读取 MainLightShadowMap 目标，调试 shader 会采样它。
            if (!cameraColorTarget.IsValid || !shadowMapTarget.IsValid) // 如果颜色目标或 shadow map 无效，就无法显示调试图。
            {
                return; // 直接结束，避免绑定或采样无效资源。
            }

            var material = GetDebugShadowMaterial(); // 获取或创建 shadow map 调试材质。
            if (material == null) // 如果材质创建失败，说明 shader 没找到或导入失败。
            {
                return; // 直接结束，避免提交无效绘制命令。
            }

            var exposure = context.Asset != null ? context.Asset.MainLightShadowDebugExposure : 1f; // 从资产读取调试曝光，资产缺失时使用默认 1。
            material.SetFloat(ShadowDebugExposureId, exposure); // 把曝光倍率传给 shader，便于放大或压暗 shadow map 深度显示。
            var debugYFlip = ResolveMainLightShadowDebugYFlip(context.Request, context.Asset); // 解析主光 shadow map 调试图最终要传给 shader 的 Y 翻转开关。
            material.SetFloat(ShadowDebugYFlipId, debugYFlip); // 把解析后的 Y 翻转开关传给调试 shader，让 shader 只负责执行一次采样方向修正。
            var cmd = CommandBufferPool.Get(Name); // 从 Unity 命令缓冲池获取一个 CommandBuffer，并用 Pass 名称命名它。
            cmd.SetRenderTarget(cameraColorTarget.Identifier); // 绑定 CameraColor 作为绘制目标，因为调试视图只覆盖颜色不写深度。
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, context.Request != null ? context.Request.Camera : null);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.MainLightShadowMapId, shadowMapTarget.Identifier); // 确保 shader 采样的是当前 request 的主光 shadow map。
            cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1); // 绘制全屏三角形，让 shader 把 shadow map 转成灰度图。
            renderContext.ExecuteCommandBuffer(cmd); // 提交调试绘制命令给 ScriptableRenderContext。
            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。
        }

        private static float ResolveMainLightShadowDebugYFlip(BurtRenderRequest request, BurtRenderPipelineAsset asset) // 根据资产配置和最终输出规则解析 shadow map 调试图的 Y 翻转值。
        {
            var finalBlitYFlip = BurtFinalBlitUtility.ResolveFinalBlitYFlip(request); // 先取得 CameraColor 输出到最终目标时是否会翻转，这是所有调试图都必须考虑的最后一步。

            var yFlipMode = asset != null ? asset.MainLightShadowDebugYFlipMode : BurtShadowDebugYFlipMode.MatchFinalBlit; // 如果资产存在就读取 Inspector 模式，资产缺失时使用默认模式。

            if (yFlipMode == BurtShadowDebugYFlipMode.InvertFinalBlit) // 如果选择反向 FinalBlit 模式，就把默认结果取反。
            {
                return finalBlitYFlip > 0.5f ? 0f : 1f; // FinalBlit 会翻时这里不翻，FinalBlit 不翻时这里翻。
            }

            if (yFlipMode == BurtShadowDebugYFlipMode.ForceNoFlip) // 如果选择强制不翻转，就忽略相机窗口和平台差异。
            {
                return 0f; // 返回 0 表示 shader 直接使用原始 shadow map UV。
            }

            if (yFlipMode == BurtShadowDebugYFlipMode.ForceFlip) // 如果选择强制翻转，就忽略相机窗口和平台差异。
            {
                return 1f; // 返回 1 表示 shader 使用 1 - uv.y 采样 shadow map。
            }

            return finalBlitYFlip; // 默认和 Depth Debug 一样复用 FinalBlit 预翻转规则，保持调试图链路一致。
        }

        private Material GetDebugShadowMaterial() // 获取或创建 shadow map 调试材质。
        {
            if (debugShadowMaterial != null) // 如果之前已经创建过材质，就直接复用。
            {
                return debugShadowMaterial; // 返回缓存材质。
            }

            var shader = Shader.Find(DebugShadowShaderName); // 按名称查找 shadow map 调试 shader。
            if (shader == null) // 如果 shader 查找失败，说明 shader 文件未导入或名称不一致。
            {
                if (!hasLoggedMissingShader) // 如果还没有输出过警告，就输出一次。
                {
                    Debug.LogWarning("BurtRP could not find shader: " + DebugShadowShaderName); // 输出缺失 shader 警告，方便定位资源问题。
                    hasLoggedMissingShader = true; // 标记已经输出过警告，避免每帧刷屏。
                }
                return null; // 返回空材质，调用方会跳过这个 Pass。
            }

            debugShadowMaterial = new Material(shader); // 使用查找到的 shader 创建运行时材质。
            debugShadowMaterial.hideFlags = HideFlags.HideAndDontSave; // 隐藏运行时材质并避免它被保存到场景或资产中。
            return debugShadowMaterial; // 返回缓存好的调试材质。
        }
    }

    internal sealed class BurtReleaseMainLightShadowMapPass : BurtRenderPass // 定义主光阴影图释放 Pass，负责在当前 request 结束时释放 shadow map 临时 RT。
    {
        public override string Name => "Burt Release Main Light Shadow Map"; // 返回这个 Pass 的名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            if (!BurtShadowUtility.ShouldUseMainLightShadow(builder.Request, builder.Asset)) // 如果当前 request 没有主光阴影，就不声明阴影图读取。
            {
                return; // 直接结束资源声明，避免 Debug 输出无效的 MainLightShadowMap 依赖。
            }

            builder.ReadMainLightShadowMap(); // 声明这个 Pass 依赖 MainLightShadowMap，表示它要结束这个阴影资源的生命周期。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现主光阴影图释放 Pass 的执行函数。
        {
            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。

            var request = context.Request; // 从 GraphContext 中取出当前渲染请求。

            if (!BurtShadowUtility.ShouldUseMainLightShadow(request, context.Asset)) // 如果当前 request 不需要主光阴影，就不释放阴影图。
            {
                return; // 直接结束这个 Pass，避免释放一个没有申请过的临时 RT。
            }

            var shadowMapTarget = context.MainLightShadowMapTarget; // 从 GraphContext 中取出 MainLightShadowMap 资源句柄。

            if (!shadowMapTarget.IsValid) // 如果 MainLightShadowMap 句柄无效，说明当前图没有注册这个资源。
            {
                return; // 直接结束这个 Pass，避免释放不存在的临时 RT。
            }

            var cmd = CommandBufferPool.Get(Name); // 从 Unity 命令缓冲池获取一个 CommandBuffer，并用 Pass 名称命名它。

            cmd.ReleaseTemporaryRT(BurtRenderGraphResourceRegistry.MainLightShadowMapId); // 释放前面申请的主光阴影图临时 RT，避免资源泄漏到下一个 request。

            renderContext.ExecuteCommandBuffer(cmd); // 把释放 RT 的命令提交给 ScriptableRenderContext。

            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。
        }
    }

    internal sealed class BurtDrawPreImageEffectsGizmosPass : BurtRenderPass
    {
        public override string Name => "Burt Draw Gizmos Pre Image Effects";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.Debug;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtEditorGizmoUtility.ShouldRenderGizmos(builder.Request))
            {
                return;
            }

            builder.ReadCameraColor();
            builder.ReadCameraDepth();
            builder.WriteCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
#if UNITY_EDITOR
            if (context == null || !BurtEditorGizmoUtility.ShouldRenderGizmos(context.Request))
            {
                return;
            }

            if (!BurtDrawingSettingsUtility.BindCameraColorAndDepth(context, Name))
            {
                return;
            }

            context.ScriptableContext.DrawGizmos(context.Request.Camera, GizmoSubset.PreImageEffects);
#endif
        }
    }

    internal sealed class BurtDrawPostImageEffectsGizmosPass : BurtRenderPass
    {
        public override string Name => "Burt Draw Gizmos Post Image Effects";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.Debug;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtEditorGizmoUtility.ShouldRenderGizmos(builder.Request))
            {
                return;
            }

            builder.ReadCameraColor();
            builder.ReadCameraDepth();
            builder.WriteCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
#if UNITY_EDITOR
            if (context == null || !BurtEditorGizmoUtility.ShouldRenderGizmos(context.Request))
            {
                return;
            }

            if (!BurtDrawingSettingsUtility.BindCameraColorAndDepth(context, Name))
            {
                return;
            }

            context.ScriptableContext.DrawGizmos(context.Request.Camera, GizmoSubset.PostImageEffects);
#endif
        }
    }

    internal static class BurtEditorGizmoUtility
    {
        public static void EmitWorldGeometryForSceneView(Camera camera)
        {
#if UNITY_EDITOR
            if (camera == null || camera.cameraType != CameraType.SceneView)
            {
                return;
            }

            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
#endif
        }

        public static bool ShouldRenderGizmos(BurtRenderRequest request)
        {
#if UNITY_EDITOR
            if (request == null || !request.IsValid || request.Camera == null)
            {
                return false;
            }

            if (request.Type == BurtRenderRequestType.Preview || request.Type == BurtRenderRequestType.Reflection)
            {
                return false;
            }

            return Handles.ShouldRenderGizmos();
#else
            return false;
#endif
        }

    }

}
