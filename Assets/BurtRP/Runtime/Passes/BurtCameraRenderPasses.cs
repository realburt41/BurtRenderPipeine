using UnityEngine; // 引入 UnityEngine 命名空间，用来使用 Color、ShaderTagId 等 Unity 类型。
using UnityEngine.Rendering; // 引入 UnityEngine.Rendering 命名空间，用来使用 CommandBufferPool、DrawingSettings、FilteringSettings 等 SRP 类型。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让这些 Pass 能直接访问 BurtRenderRequest、BurtCameraData 等类型。
{
    internal static class BurtDrawingSettingsUtility // Centralizes all ShaderTagId lists used by BurtRP drawing passes.
    {
        private static readonly ShaderTagId BurtForward = new ShaderTagId("BurtForward"); // Names the BurtRP forward color pass that supported shaders must provide.

        private static readonly ShaderTagId BurtDepthOnly = new ShaderTagId("BurtDepthOnly"); // Names the BurtRP depth-only pass used by the depth prepass.

        private static readonly ShaderTagId SRPDefaultUnlit = new ShaderTagId("SRPDefaultUnlit"); // Names Unity's generic SRP unlit pass so the unsupported-shader pass can catch it.

        private static readonly ShaderTagId ForwardBase = new ShaderTagId("ForwardBase"); // Names the Built-in pipeline forward pass so the unsupported-shader pass can catch it.

        private static readonly ShaderTagId Always = new ShaderTagId("Always"); // Names a common Built-in fallback pass so the unsupported-shader pass can catch it.

        private static readonly ShaderTagId PrepassBase = new ShaderTagId("PrepassBase"); // Names an old Built-in deferred prepass so the unsupported-shader pass can catch it.

        private static readonly ShaderTagId Vertex = new ShaderTagId("Vertex"); // Names an old Built-in vertex-lit pass so the unsupported-shader pass can catch it.

        private static readonly ShaderTagId VertexLMRGBM = new ShaderTagId("VertexLMRGBM"); // Names an old Built-in lightmap pass so the unsupported-shader pass can catch it.

        private static readonly ShaderTagId VertexLM = new ShaderTagId("VertexLM"); // Names another old Built-in lightmap pass so the unsupported-shader pass can catch it.

        private static readonly ShaderTagId UniversalForward = new ShaderTagId("UniversalForward"); // Names a URP forward pass so URP shaders do not silently render as BurtRP shaders.

        private static readonly ShaderTagId UniversalForwardOnly = new ShaderTagId("UniversalForwardOnly"); // Names a URP forward-only pass so URP shaders can be reported as unsupported.

        private static readonly ShaderTagId LightweightForward = new ShaderTagId("LightweightForward"); // Names an old LWRP forward pass so legacy SRP shaders can be reported as unsupported.

        private static readonly ShaderTagId[] UnsupportedShaderTagIds = new ShaderTagId[] // Lists LightMode names that BurtRP does not own and should render with an error material.
        {
            SRPDefaultUnlit, // Treats generic SRP unlit shaders as unsupported unless they are migrated to BurtForward.
            ForwardBase, // Treats Built-in ForwardBase shaders as unsupported so old materials are not accepted silently.
            Always, // Treats Built-in Always passes as unsupported so fallback-style shaders are visible as errors.
            PrepassBase, // Treats old Built-in prepass shaders as unsupported because BurtRP does not implement that path.
            Vertex, // Treats old Built-in vertex-lit shaders as unsupported because BurtRP does not implement vertex lighting.
            VertexLMRGBM, // Treats old Built-in lightmap shaders as unsupported because BurtRP does not implement that path yet.
            VertexLM, // Treats another old Built-in lightmap pass as unsupported for the same reason.
            UniversalForward, // Treats URP forward shaders as unsupported because BurtRP should use its own LightMode names.
            UniversalForwardOnly, // Treats URP forward-only shaders as unsupported because BurtRP does not execute URP passes.
            LightweightForward // Treats old LWRP forward shaders as unsupported because BurtRP does not execute LWRP passes.
        }; // Ends the unsupported LightMode list.

        public static DrawingSettings CreateForwardDrawingSettings(SortingSettings sortingSettings) // Creates drawing settings for normal BurtRP forward color rendering.
        {
            var drawingSettings = new DrawingSettings(BurtForward, sortingSettings); // Matches only BurtForward, making the main render path strict and BurtRP-owned.

            return drawingSettings; // Returns the configured forward drawing settings to the caller pass.
        }

        public static DrawingSettings CreateUnsupportedDrawingSettings( // Creates drawing settings for the unsupported-shader debug pass.
            SortingSettings sortingSettings, // Receives camera sorting rules so error-material rendering is stable.
            Material errorMaterial) // Receives the material that should override unsupported source materials.
        {
            var drawingSettings = new DrawingSettings(UnsupportedShaderTagIds[0], sortingSettings); // Uses the first unsupported LightMode as the primary shader pass name.

            for (var shaderTagIndex = 1; shaderTagIndex < UnsupportedShaderTagIds.Length; shaderTagIndex++) // Visits every remaining unsupported LightMode.
            {
                drawingSettings.SetShaderPassName(shaderTagIndex, UnsupportedShaderTagIds[shaderTagIndex]); // Registers the unsupported LightMode at the matching drawing-settings slot.
            }

            drawingSettings.overrideMaterial = errorMaterial; // Forces matched unsupported shaders to render with Unity's error material.

            return drawingSettings; // Returns the configured unsupported-shader drawing settings to the caller pass.
        }

        public static DrawingSettings CreateDepthDrawingSettings(SortingSettings sortingSettings) // Creates drawing settings for BurtRP depth-only rendering.
        {
            var drawingSettings = new DrawingSettings(BurtDepthOnly, sortingSettings); // Matches only BurtDepthOnly so the depth prepass cannot accidentally run a color pass.

            return drawingSettings; // Returns the configured depth drawing settings to the caller pass.
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

            cmd.GetTemporaryRT(BurtRenderGraphResourceRegistry.MainLightShadowMapId, descriptor, FilterMode.Bilinear); // Uses bilinear filtering so the hardware shadow sampler can smooth compare edges instead of amplifying point-sample bands.

            cmd.SetRenderTarget(shadowMapTarget.Identifier); // 把主光阴影图绑定为当前渲染目标，为后续 ShadowCaster 绘制做准备。

            cmd.ClearRenderTarget(true, false, Color.clear); // 清理阴影图深度，保证还没写入阴影 caster 时默认不会出现脏深度。

            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.MainLightShadowMapId, shadowMapTarget.Identifier); // 把主光阴影图暴露成全局纹理，后续 Lit shader 会通过它采样阴影。

            renderContext.ExecuteCommandBuffer(cmd); // 把申请、绑定和清理 shadow map 的命令提交给 ScriptableRenderContext。

            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。
        }
    }

    internal sealed class BurtDrawMainLightShadowCasterPass : BurtRenderPass // 定义主光阴影投射 Pass，负责把可投影物体写入 MainLightShadowMap。
    {
        private const int MainLightShadowCascadeIndex = 0; // 当前阶段只实现一张主光阴影图，所以固定使用第 0 个 cascade。

        private const int MainLightShadowCascadeCount = 1; // 当前阶段不做级联阴影，所以 cascade 总数固定为 1。

        private static readonly Vector3 MainLightShadowCascadeSplit = Vector3.zero; // 单 cascade 不需要分割比例，所以传入零向量作为 Unity 计算矩阵的占位参数。

        private static readonly int MainLightDirectionId = Shader.PropertyToID("_BurtMainLightDirection"); // 缓存主光方向属性 ID，ShadowCaster 顶点偏移需要用它计算法线和光向夹角。

        private static readonly int MainLightWorldToShadowId = Shader.PropertyToID("_BurtMainLightWorldToShadow"); // 缓存世界空间到主光阴影纹理空间矩阵的 shader 属性 ID。

        private static readonly int MainLightShadowStrengthId = Shader.PropertyToID("_BurtMainLightShadowStrength"); // 缓存主光阴影强度属性 ID，阴影绘制失败时会把它清零避免采样旧图。

        private static readonly int MainLightShadowTexelSizeId = Shader.PropertyToID("_BurtMainLightShadowTexelSize"); // 缓存 shadow map texel size 属性 ID，receiver 端软阴影采样会用它偏移 UV。

        private static readonly int MainLightShadowSampleBiasId = Shader.PropertyToID("_BurtMainLightShadowSampleBias"); // 缓存接收端采样 bias 属性 ID，替代 shader 内部硬编码偏移。

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

            if (!TryGetMainLightShadowMatrices(request, shadowData, out var viewMatrix, out var projectionMatrix, out var splitData)) // 计算主光视角的阴影视图矩阵、投影矩阵和裁剪数据。
            {
                DisableMainLightShadowReceiverGlobals(renderContext); // 计算失败时主动关闭接收端阴影，避免 Lit shader 继续使用上一帧的矩阵和 shadow map。

                return; // 如果 Unity 无法计算阴影矩阵，说明当前主光或剔除数据不足，直接跳过阴影绘制。
            }

            var worldToShadowMatrix = CreateWorldToShadowMatrix(viewMatrix, projectionMatrix); // 预先构造世界到 shadow map UV/depth 空间的矩阵，便于后续一次性上传。

            var shadowTexelSize = BurtShadowUtility.CreateMainLightShadowTexelSize(shadowData); // 根据最终阴影分辨率计算 texel size，软阴影和调试视图都会使用。

            var mainLightDirection = ResolveMainLightDirection(request); // 读取当前 request 的主光方向，ShadowCaster 顶点偏移不能依赖上一帧残留的全局方向。

            var shadowNormalBias = ResolveMainLightShadowNormalBias(shadowData, projectionMatrix); // 将 Inspector 上的 normal bias 折算成世界空间距离，避免直接把 0.4 当作 0.4 米偏移。

            var shadowSoftness = shadowData.IsMainLightShadowSoft ? 1f : 0f; // 把 Light 的 Soft/Hard 阴影设置转换成 shader 侧的 0/1 开关。

            var cmd = CommandBufferPool.Get(Name); // 从 Unity 命令缓冲池获取一个 CommandBuffer，并用 Pass 名称命名它。

            cmd.SetRenderTarget(shadowMapTarget.Identifier); // 把 MainLightShadowMap 绑定为当前渲染目标，后续 ShadowCaster 深度会写到这里。

            cmd.ClearRenderTarget(true, false, Color.clear); // 清理阴影图深度，避免上一帧或上一个 request 的深度残留影响当前阴影。

            cmd.SetViewProjectionMatrices(viewMatrix, projectionMatrix); // 把 GPU 当前矩阵切到主光视角，让 ShadowCaster 从灯光方向渲染。

            cmd.SetGlobalVector(MainLightDirectionId, new Vector4(mainLightDirection.x, mainLightDirection.y, mainLightDirection.z, 0f)); // 上传当前主光方向，ShadowCaster shader 会用它判断法线偏移强度。

            cmd.SetGlobalMatrix(MainLightWorldToShadowId, worldToShadowMatrix); // 上传世界空间到阴影纹理空间的矩阵，后续 Lit shader 会用它采样 shadow map。

            cmd.SetGlobalVector(MainLightShadowTexelSizeId, shadowTexelSize); // 上传 shadow map texel size，让 receiver 端可以做可控的邻域采样。

            cmd.SetGlobalFloat(MainLightShadowNormalBiasId, shadowNormalBias); // 上传世界空间 normal bias，ShadowCaster 顶点 shader 会沿法线推开 caster。

            cmd.SetGlobalFloat(MainLightShadowSampleBiasId, shadowData.MainLightShadowSampleBias); // 上传接收端深度比较偏移，替代 shader 中固定 0.001 的写法。

            cmd.SetGlobalFloat(MainLightShadowSoftnessId, shadowSoftness); // 上传软阴影开关，让 Lit shader 按 Light 的 Hard/Soft 阴影设置选择采样数量。

            cmd.SetGlobalFloat(MainLightShadowStrengthId, shadowData.MainLightShadowStrength); // 上传阴影强度，确保阴影成功绘制后 receiver 端才会使用它。

            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.MainLightShadowMapId, shadowMapTarget.Identifier); // 再次绑定当前 request 的 shadow map，避免多相机时全局纹理残留。

            cmd.SetGlobalDepthBias(shadowData.MainLightShadowDepthBias, 0f); // normal bias 已交给 ShadowCaster 顶点偏移处理，这里只保留硬件常量 depth bias，避免同一参数被当作 slope bias 重复使用。

            renderContext.ExecuteCommandBuffer(cmd); // 把绑定目标、清理深度和设置矩阵的命令提交给 ScriptableRenderContext。

            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。

            var shadowDrawingSettings = new ShadowDrawingSettings(request.CullingResults, shadowData.MainLightIndex); // 创建 Unity 阴影绘制设置，让 DrawShadows 找到主光对应的 ShadowCaster。

            shadowDrawingSettings.splitData = splitData; // 把 Unity 计算出的阴影裁剪数据交给 DrawShadows，避免绘制不在阴影范围内的物体。

            try // 使用 try/finally 保护渲染状态恢复，避免 DrawShadows 抛错后把深度 bias 留给后续颜色 Pass。
            {
                renderContext.DrawShadows(ref shadowDrawingSettings); // 绘制所有对主光投影的可见物体，shader 需要提供 LightMode=ShadowCaster 的 Pass。
            }
            finally // 无论阴影绘制是否成功，都要恢复状态。
            {
                ResetMainLightShadowCasterState(renderContext, camera); // 清掉 ShadowCaster 专用深度 bias，并把视图投影恢复到当前相机。
            }
        }

        private static void ResetMainLightShadowCasterState(ScriptableRenderContext renderContext, Camera camera) // 恢复 ShadowCaster Pass 修改过的全局渲染状态。
        {
            var cmd = CommandBufferPool.Get("Burt Reset Main Light Shadow Caster State"); // 获取一个独立 CommandBuffer，避免复用已经提交的 shadow draw 命令。
            cmd.SetGlobalDepthBias(0f, 0f); // 把 ShadowCaster 阶段设置的深度偏移清零，避免影响后续相机颜色绘制。
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
            cmd.SetGlobalMatrix(MainLightWorldToShadowId, Matrix4x4.identity); // 把世界到阴影矩阵重置为单位矩阵，避免调试时看到上一帧矩阵。
            cmd.SetGlobalVector(MainLightShadowTexelSizeId, Vector4.zero); // 清空 texel size，避免软阴影采样使用上一张 shadow map 的尺寸。
            cmd.SetGlobalFloat(MainLightShadowSampleBiasId, 0f); // 清空采样 bias，保证无阴影状态完全可控。
            cmd.SetGlobalFloat(MainLightShadowSoftnessId, 0f); // 清空软阴影开关，避免无阴影时执行额外采样逻辑。
            renderContext.ExecuteCommandBuffer(cmd); // 提交全局阴影状态清理命令。
            CommandBufferPool.Release(cmd); // 释放清理用 CommandBuffer，避免每帧分配。
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

            return normalBias * worldTexelSize; // 上传世界单位偏移，让 shader 不需要知道投影矩阵和分辨率。
        }

        private static bool TryGetMainLightShadowMatrices( // 定义计算主光阴影矩阵的辅助函数。
            BurtRenderRequest request, // 接收当前渲染请求，用来访问 CullingResults。
            BurtShadowData shadowData, // 接收主光阴影数据，用来访问主光索引、分辨率和 near plane。
            out Matrix4x4 viewMatrix, // 输出主光视角的 view matrix。
            out Matrix4x4 projectionMatrix, // 输出主光视角的 projection matrix。
            out ShadowSplitData splitData) // 输出 Unity 用于裁剪阴影投射物的 split data。
        {
            viewMatrix = Matrix4x4.identity; // 先给 viewMatrix 一个安全默认值，避免失败返回时输出未初始化。

            projectionMatrix = Matrix4x4.identity; // 先给 projectionMatrix 一个安全默认值，避免失败返回时输出未初始化。

            splitData = default; // 先给 splitData 一个默认值，避免失败返回时输出未初始化。

            if (request == null) // 如果 request 为空，说明没有剔除结果可以用于计算阴影矩阵。
            {
                return false; // 返回 false，让调用方跳过阴影绘制。
            }

            if (shadowData == null) // 如果 shadowData 为空，说明没有主光阴影配置。
            {
                return false; // 返回 false，让调用方跳过阴影绘制。
            }

            if (shadowData.MainLightIndex < 0) // 如果主光索引小于 0，说明没有可用的 visible light 条目。
            {
                return false; // 返回 false，避免把无效 light index 传给 Unity 阴影 API。
            }

            return request.CullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives( // 调用 Unity 的方向光阴影矩阵计算 API。
                shadowData.MainLightIndex, // 传入主方向光在 visibleLights 里的索引。
                MainLightShadowCascadeIndex, // 传入当前 cascade 索引，单张阴影图固定为 0。
                MainLightShadowCascadeCount, // 传入 cascade 总数，当前阶段固定为 1。
                MainLightShadowCascadeSplit, // 传入 cascade 分割比例，单 cascade 下使用零向量占位。
                shadowData.MainLightShadowResolution, // 传入阴影图分辨率，让 Unity 根据贴图大小计算稳定投影。
                shadowData.MainLightShadowNearPlane, // 传入主光 shadow near plane，控制阴影相机近裁剪面。
                out viewMatrix, // 接收 Unity 计算出的主光 view matrix。
                out projectionMatrix, // 接收 Unity 计算出的主光 projection matrix。
                out splitData); // 接收 Unity 计算出的阴影裁剪数据。
        }

        private static Matrix4x4 CreateWorldToShadowMatrix( // 定义把世界空间转换到阴影纹理空间的矩阵构造函数。
            Matrix4x4 viewMatrix, // 接收主光 view matrix。
            Matrix4x4 projectionMatrix) // 接收主光 projection matrix。
        {
            if (SystemInfo.usesReversedZBuffer) // 如果当前图形 API 使用反向 Z，Unity 的投影矩阵需要在手动采样阴影前修正 Z 分量。
            {
                projectionMatrix.m20 = -projectionMatrix.m20; // 翻转投影矩阵第三行第一列，修正反向 Z 下的深度方向。

                projectionMatrix.m21 = -projectionMatrix.m21; // 翻转投影矩阵第三行第二列，保持投影矩阵 Z 计算一致。

                projectionMatrix.m22 = -projectionMatrix.m22; // 翻转投影矩阵第三行第三列，保持投影矩阵 Z 计算一致。

                projectionMatrix.m23 = -projectionMatrix.m23; // 翻转投影矩阵第三行第四列，保持投影矩阵 Z 计算一致。
            }

            var textureScaleAndBias = Matrix4x4.identity; // 创建一个单位矩阵，后面把裁剪空间坐标转换到 0 到 1 的纹理坐标空间。

            textureScaleAndBias.m00 = 0.5f; // 把 x 从 -1..1 缩放到 -0.5..0.5。

            textureScaleAndBias.m11 = 0.5f; // 把 y 从 -1..1 缩放到 -0.5..0.5。

            textureScaleAndBias.m22 = 0.5f; // 把 z 从 -1..1 缩放到 -0.5..0.5，方便后续深度比较。

            textureScaleAndBias.m03 = 0.5f; // 把 x 从 -0.5..0.5 平移到 0..1。

            textureScaleAndBias.m13 = 0.5f; // 把 y 从 -0.5..0.5 平移到 0..1。

            textureScaleAndBias.m23 = 0.5f; // 把 z 从 -0.5..0.5 平移到 0..1。

            return textureScaleAndBias * projectionMatrix * viewMatrix; // 返回 world -> light clip -> shadow texture 的完整变换矩阵。
        }
    }

    internal sealed class BurtSetupLightingPass : BurtRenderPass // 上传当前 request 的主光、环境光和阴影接收端全局参数。
    {
        private static readonly int MainLightDirectionId = Shader.PropertyToID("_BurtMainLightDirection"); // 缓存主光方向属性 ID，避免每帧字符串查找。
        private static readonly int MainLightColorId = Shader.PropertyToID("_BurtMainLightColor"); // 缓存主光颜色属性 ID，避免每帧字符串查找。
        private static readonly int AmbientLightColorId = Shader.PropertyToID("_BurtAmbientLightColor"); // 缓存环境光颜色属性 ID，避免每帧字符串查找。
        private static readonly int MainLightWorldToShadowId = Shader.PropertyToID("_BurtMainLightWorldToShadow"); // 缓存世界到主光阴影空间矩阵属性 ID，用来在无阴影时主动清理旧矩阵。
        private static readonly int MainLightShadowStrengthId = Shader.PropertyToID("_BurtMainLightShadowStrength"); // 缓存主光阴影强度属性 ID，后续 Lit shader 用它决定是否采样 shadow map。
        private static readonly int MainLightShadowTexelSizeId = Shader.PropertyToID("_BurtMainLightShadowTexelSize"); // 缓存 shadow map texel size 属性 ID，软阴影采样会使用。
        private static readonly int MainLightShadowSampleBiasId = Shader.PropertyToID("_BurtMainLightShadowSampleBias"); // 缓存接收端采样 bias 属性 ID，避免 shader 内部硬编码。
        private static readonly int MainLightShadowSoftnessId = Shader.PropertyToID("_BurtMainLightShadowSoftness"); // 缓存软阴影开关属性 ID，让 Light 的 Hard/Soft 设置进入 shader。

        public override string Name => "Burt Setup Lighting"; // 返回这个 Pass 的名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
        } // 这个 Pass 只上传全局 shader 常量，不读取或写入 RenderGraph 渲染目标。

        public override void Execute(BurtRenderGraphContext context) // 执行灯光和阴影接收端参数上传。
        {
            var renderContext = context.ScriptableContext; // 读取 Unity SRP 上下文，后面用它提交 CommandBuffer。
            var request = context.Request; // 读取当前渲染请求，后面用它取得预先解析好的灯光数据。
            var lightingData = ResolveLightingData(request); // 取得 request 级灯光数据；如果 request 异常则返回安全默认值。
            var shadowData = BurtShadowUtility.ResolveMainLightShadowData(request, context.Asset); // 读取合并 PipelineAsset 后的主光阴影数据，让资产开关和 bias 生效。
            var mainLightDirection = lightingData.MainLightDirection; // 读取主光世界空间方向。
            var mainLightColor = lightingData.MainLightColor; // 读取主光颜色。
            var ambientLightColor = lightingData.AmbientLightColor; // 读取环境光颜色。
            var mainLightShadowStrength = ResolveMainLightShadowStrength(shadowData); // 读取最终阴影强度，没有阴影或资产关闭时返回 0。
            var mainLightShadowTexelSize = BurtShadowUtility.CreateMainLightShadowTexelSize(shadowData); // 计算 shadow map texel size，给 receiver 端软阴影采样使用。
            var mainLightShadowSampleBias = shadowData != null && shadowData.HasMainLightShadow ? shadowData.MainLightShadowSampleBias : 0f; // 只有有阴影时才上传采样 bias。
            var mainLightShadowSoftness = shadowData != null && shadowData.HasMainLightShadow && shadowData.IsMainLightShadowSoft ? 1f : 0f; // 把主光 Hard/Soft 阴影类型转换为 shader 可读的 0/1。
            var cmd = CommandBufferPool.Get(Name); // 从 Unity 命令缓冲池获取一个 CommandBuffer，并用 Pass 名称命名它。
            cmd.SetGlobalVector(MainLightDirectionId, new Vector4(mainLightDirection.x, mainLightDirection.y, mainLightDirection.z, 0f)); // 上传归一化的主光方向，Lit shader 用它计算 Lambert 漫反射。
            cmd.SetGlobalColor(MainLightColorId, mainLightColor); // 上传主光颜色，Lit shader 会把它乘到直接光上。
            cmd.SetGlobalColor(AmbientLightColorId, ambientLightColor); // 上传环境光颜色，Lit shader 会用它保留阴影区域的基础亮度。
            cmd.SetGlobalMatrix(MainLightWorldToShadowId, Matrix4x4.identity); // 每个 request 开始先清理阴影矩阵，真正绘制阴影成功后 ShadowCaster Pass 会覆盖它。
            cmd.SetGlobalFloat(MainLightShadowStrengthId, mainLightShadowStrength); // 上传最终阴影强度，0 表示 receiver 完全跳过 shadow map 采样。
            cmd.SetGlobalVector(MainLightShadowTexelSizeId, mainLightShadowTexelSize); // 上传 shadow map texel size，软阴影采样会根据它偏移邻域 UV。
            cmd.SetGlobalFloat(MainLightShadowSampleBiasId, mainLightShadowSampleBias); // 上传接收端采样 bias，替代 shader 内的固定 0.001 偏移。
            cmd.SetGlobalFloat(MainLightShadowSoftnessId, mainLightShadowSoftness); // 上传软阴影开关，Hard 阴影只做中心点比较，Soft 阴影做简单邻域采样。
            renderContext.ExecuteCommandBuffer(cmd); // 把灯光和阴影全局参数提交给 Unity 渲染上下文。
            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。
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

            builder.WriteCameraColor(); // 声明这个 Pass 会把不透明物体颜色写入 CameraColor。

            builder.WriteCameraDepth(); // 声明这个 Pass 当前仍可能通过 ZWrite 更新 CameraDepth。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现 BurtRenderPass 的执行函数。
        {
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
            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。

            var request = context.Request; // 从 GraphContext 中取出当前渲染请求。

            var camera = request.Camera; // 从 request 中取出当前相机，用来绘制天空盒。

            var cameraData = request.CameraData; // 从 request 中取出 BurtCameraData，用来判断是否需要绘制天空盒。

            if (camera == null) // 如果当前相机为空，就没有办法绘制天空盒。
            {
                return; // 直接结束这个 Pass。
            }

            if (cameraData == null) // 如果当前相机没有 BurtCameraData，就按当前规则不绘制天空盒。
            {
                return; // 直接结束这个 Pass。
            }

            if (cameraData.ClearMode != BurtCameraClearMode.Skybox) // 如果当前清屏模式不是 Skybox，就不绘制天空盒。
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

            builder.WriteCameraColor(); // 声明透明混合结果会写回 CameraColor。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现 BurtRenderPass 的执行函数。
        {
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

    internal sealed class BurtDrawUnsupportedShadersPass : BurtRenderPass // Renders non-BurtRP shaders with an error material so unsupported materials are obvious.
    {
        private const string ErrorShaderName = "Hidden/InternalErrorShader"; // Stores Unity's built-in error shader name.

        private Material errorMaterial; // Caches the runtime error material to avoid creating one every frame.

        private bool hasLoggedMissingErrorShader; // Tracks whether the missing-error-shader warning has already been printed.

        public override string Name => "Burt Draw Unsupported Shaders"; // Names this pass for RenderGraph debug output and Frame Debugger markers.

        public override void Configure(BurtRenderPassBuilder builder) // Declares the resources used by this pass.
        {
            builder.ReadCameraDepth(); // Declares that error-material rendering uses the current CameraDepth for depth testing.

            builder.WriteCameraColor(); // Declares that error-material rendering writes visible pixels into CameraColor.
        }

        public override void Execute(BurtRenderGraphContext context) // Executes unsupported-shader debug rendering.
        {
            var renderContext = context.ScriptableContext; // Reads Unity's SRP context so this pass can submit commands and draw renderers.

            var request = context.Request; // Reads the current render request so this pass can access culling results and the camera.

            var camera = request.Camera; // Reads the current camera for sorting settings.

            var cameraColorTarget = context.CameraColorTarget; // Reads the CameraColor target that receives the error-material output.

            var cameraDepthTarget = context.CameraDepthTarget; // Reads the CameraDepth target that controls depth testing for error-material output.

            if (!cameraColorTarget.IsValid) // Checks whether the graph registered a valid color target.
            {
                return; // Stops the pass because there is nowhere safe to draw the error material.
            }

            if (!cameraDepthTarget.IsValid) // Checks whether the graph registered a valid depth target.
            {
                return; // Stops the pass because error-material rendering should not run without the current depth target.
            }

            var material = GetErrorMaterial(); // Gets the cached Unity error material or creates it on first use.

            if (material == null) // Checks whether error material creation failed.
            {
                return; // Stops the pass because DrawRenderers requires a valid override material here.
            }

            var cmd = CommandBufferPool.Get(Name); // Gets a pooled CommandBuffer named after this pass.

            cmd.SetRenderTarget(cameraColorTarget.Identifier, cameraDepthTarget.Identifier); // Rebinds the current request color and depth targets before drawing unsupported shaders.

            renderContext.ExecuteCommandBuffer(cmd); // Submits the render-target binding command to Unity's render context.

            CommandBufferPool.Release(cmd); // Releases the CommandBuffer back to Unity's pool to avoid per-frame allocations.

            var sortingSettings = new SortingSettings(camera); // Creates sorting settings based on the current camera.

            sortingSettings.criteria = SortingCriteria.CommonOpaque; // Uses stable opaque-style sorting for the debug overlay pass.

            var drawingSettings = BurtDrawingSettingsUtility.CreateUnsupportedDrawingSettings(sortingSettings, material); // Builds DrawingSettings that match known non-BurtRP LightMode names.

            var filteringSettings = FilteringSettings.defaultValue; // Uses default filtering so unsupported shaders in any queue can be reported.

            renderContext.DrawRenderers(request.CullingResults, ref drawingSettings, ref filteringSettings); // Draws visible renderers whose shader passes are not supported by BurtRP.
        }

        private Material GetErrorMaterial() // Gets or creates the material used for unsupported shader rendering.
        {
            if (errorMaterial != null) // Checks whether the material has already been created.
            {
                return errorMaterial; // Returns the cached material instance.
            }

            var shader = Shader.Find(ErrorShaderName); // Looks up Unity's built-in error shader by name.

            if (shader == null) // Checks whether the error shader lookup failed.
            {
                if (!hasLoggedMissingErrorShader) // Checks whether this warning has already been logged.
                {
                    Debug.LogWarning("BurtRP could not find shader: " + ErrorShaderName); // Logs a single warning so the missing built-in shader can be diagnosed.

                    hasLoggedMissingErrorShader = true; // Marks the warning as printed to avoid Console spam.
                }

                return null; // Returns null so the caller can skip unsupported-shader rendering safely.
            }

            errorMaterial = new Material(shader); // Creates the runtime material from Unity's error shader.

            errorMaterial.hideFlags = HideFlags.HideAndDontSave; // Hides the runtime material and prevents it from being saved into assets or scenes.

            return errorMaterial; // Returns the cached runtime error material.
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
        private static readonly int ShadowDebugYFlipId = Shader.PropertyToID("_BurtMainLightShadowDebugYFlip"); // Caches the debug Y pre-flip property so the pass does not search this shader name every frame.
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

}
