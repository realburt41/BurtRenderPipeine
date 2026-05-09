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

    internal static class BurtRenderTargetDescriptorUtility // 定义渲染目标描述工具类，用来集中创建 BurtRP 自己管理的 RT 描述。
    {
        public static RenderTextureDescriptor CreateCameraDepthDescriptor(Camera camera) // 定义创建相机深度 RT 描述的函数。
        {
            var width = 1; // 定义默认宽度为 1，避免相机尺寸异常时创建 0 宽 RT。

            var height = 1; // 定义默认高度为 1，避免相机尺寸异常时创建 0 高 RT。

            if (camera != null) // 如果当前 request 有有效相机，就优先从相机读取目标尺寸。
            {
                width = Mathf.Max(1, camera.pixelWidth); // 使用相机像素宽度，并强制最小为 1。

                height = Mathf.Max(1, camera.pixelHeight); // 使用相机像素高度，并强制最小为 1。

                if (camera.targetTexture != null) // 如果相机输出到 RenderTexture，就以 targetTexture 的尺寸为准。
                {
                    width = Mathf.Max(1, camera.targetTexture.width); // 使用 targetTexture 宽度，并强制最小为 1。

                    height = Mathf.Max(1, camera.targetTexture.height); // 使用 targetTexture 高度，并强制最小为 1。
                }
            }

            var descriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.Depth, 32); // 创建深度专用 RT 描述，32 位深度让深度测试更稳定。

            descriptor.msaaSamples = 1; // 当前阶段先关闭 MSAA，避免深度 RT 和相机颜色目标采样数不匹配。

            descriptor.useMipMap = false; // 深度缓冲不需要 mipmap，关闭后可以减少无意义资源开销。

            descriptor.autoGenerateMips = false; // 深度缓冲不生成 mipmap，避免 Unity 做额外工作。

            return descriptor; // 返回创建好的深度 RT 描述，供分配 Pass 使用。
        }

        public static RenderTextureDescriptor CreateMainLightShadowMapDescriptor(BurtShadowData shadowData) // 定义创建主光阴影图 RT 描述的函数。
        {
            var resolution = 1024; // 定义默认阴影图分辨率，避免 shadowData 缺失时创建非法尺寸。

            if (shadowData != null) // 如果当前 request 提供了阴影数据，就优先使用灯光解析出来的分辨率。
            {
                resolution = Mathf.Max(1, shadowData.MainLightShadowResolution); // 读取主光阴影分辨率，并强制最小为 1。
            }

            var descriptor = new RenderTextureDescriptor(resolution, resolution, RenderTextureFormat.Shadowmap, 32); // 创建 Shadowmap 格式的深度纹理描述，供主光阴影 Pass 写入深度。

            descriptor.msaaSamples = 1; // 阴影图不使用 MSAA，保证后续深度采样和比较逻辑简单稳定。

            descriptor.useMipMap = false; // 阴影图当前阶段不生成 mipmap，避免多余显存和生成开销。

            descriptor.autoGenerateMips = false; // 关闭自动 mipmap 生成，防止 Unity 对深度纹理做无意义的额外处理。

            return descriptor; // 返回创建好的主光阴影图描述，供分配 Pass 使用。
        }
    }

    internal sealed class BurtAllocateCameraDepthPass : BurtRenderPass // 定义 CameraDepth 分配 Pass，负责为当前 request 创建真实深度 RT。
    {
        public override string Name => "Burt Allocate Camera Depth"; // 返回这个 Pass 的名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            builder.WriteCameraDepth(); // 声明这个 Pass 会创建并写入 CameraDepth 资源的生命周期状态。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现 CameraDepth 分配 Pass 的执行函数。
        {
            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。

            var request = context.Request; // 从 GraphContext 中取出当前渲染请求。

            var camera = request.Camera; // 从 request 中取出当前相机，用来决定深度 RT 尺寸。

            var cameraDepthTarget = context.CameraDepthTarget; // 从 GraphContext 中取出 CameraDepth 资源句柄。

            if (!cameraDepthTarget.IsValid) // 如果 CameraDepth 句柄无效，说明资源表没有注册深度目标。
            {
                return; // 直接结束这个 Pass，避免申请一个无法被后续 Pass 找到的 RT。
            }

            var descriptor = BurtRenderTargetDescriptorUtility.CreateCameraDepthDescriptor(camera); // 根据当前相机创建深度 RT 描述。

            var cmd = CommandBufferPool.Get(Name); // 从 Unity 命令缓冲池获取一个 CommandBuffer，并用 Pass 名称命名它。

            cmd.GetTemporaryRT(BurtRenderGraphResourceRegistry.CameraDepthTextureId, descriptor, FilterMode.Point); // 申请一个临时深度 RT，并绑定到 CameraDepth 的全局 ID。

            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.CameraDepthTextureId, cameraDepthTarget.Identifier); // 把 CameraDepth 暴露成全局纹理，方便后续 shader 或 pass 采样。

            renderContext.ExecuteCommandBuffer(cmd); // 把申请 RT 的命令提交给 ScriptableRenderContext。

            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。
        }
    }

    internal sealed class BurtAllocateMainLightShadowMapPass : BurtRenderPass // 定义主光阴影图分配 Pass，负责为当前 request 创建主光 shadow map。
    {
        public override string Name => "Burt Allocate Main Light Shadow Map"; // 返回这个 Pass 的名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            if (!BurtShadowUtility.ShouldUseMainLightShadow(builder.Request)) // 如果当前 request 没有主光阴影，就不声明阴影图写入。
            {
                return; // 直接结束资源声明，避免 Debug 输出无效的 MainLightShadowMap 依赖。
            }

            builder.WriteMainLightShadowMap(); // 声明这个 Pass 会申请并初始化 MainLightShadowMap 资源。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现主光阴影图分配 Pass 的执行函数。
        {
            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。

            var request = context.Request; // 从 GraphContext 中取出当前渲染请求。

            if (!BurtShadowUtility.ShouldUseMainLightShadow(request)) // 如果当前 request 不需要主光阴影，就不申请阴影图。
            {
                return; // 直接结束这个 Pass，避免无意义的临时 RT 分配。
            }

            var shadowData = BurtShadowUtility.ResolveMainLightShadowData(request); // 从 request 中安全读取主光阴影参数。

            var shadowMapTarget = context.MainLightShadowMapTarget; // 从 GraphContext 中取出 MainLightShadowMap 资源句柄。

            if (!shadowMapTarget.IsValid) // 如果 MainLightShadowMap 句柄无效，说明资源表没有注册阴影图。
            {
                return; // 直接结束这个 Pass，避免申请一个后续 Pass 无法找到的 RT。
            }

            var descriptor = BurtRenderTargetDescriptorUtility.CreateMainLightShadowMapDescriptor(shadowData); // 根据主光阴影数据创建 shadow map RT 描述。

            var cmd = CommandBufferPool.Get(Name); // 从 Unity 命令缓冲池获取一个 CommandBuffer，并用 Pass 名称命名它。

            cmd.GetTemporaryRT(BurtRenderGraphResourceRegistry.MainLightShadowMapId, descriptor, FilterMode.Bilinear); // 申请主光阴影图临时 RT，并绑定到统一的全局 ID。

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

        private static readonly int MainLightWorldToShadowId = Shader.PropertyToID("_BurtMainLightWorldToShadow"); // 缓存世界空间到主光阴影纹理空间矩阵的 shader 属性 ID。

        public override string Name => "Burt Draw Main Light Shadow Caster"; // 返回这个 Pass 的名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            if (!BurtShadowUtility.ShouldUseMainLightShadow(builder.Request)) // 如果当前 request 没有可用主光阴影，就不声明阴影图写入。
            {
                return; // 直接结束资源声明，避免 Debug 输出无效的 MainLightShadowMap 依赖。
            }

            builder.WriteMainLightShadowMap(); // 声明这个 Pass 会把 ShadowCaster 深度写入 MainLightShadowMap。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现主光阴影投射 Pass 的执行函数。
        {
            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文，用来提交命令和绘制阴影。

            var request = context.Request; // 从 GraphContext 中取出当前渲染请求，用来访问相机、剔除结果和灯光数据。

            if (!BurtShadowUtility.ShouldUseMainLightShadow(request)) // 如果当前 request 不需要主光阴影，就不执行阴影绘制。
            {
                return; // 直接结束这个 Pass，避免无意义的矩阵计算和 DrawShadows 调用。
            }

            var camera = request.Camera; // 从 request 中取出当前相机，阴影绘制后需要用它恢复相机矩阵状态。

            if (camera == null) // 如果相机为空，说明当前 request 状态异常。
            {
                return; // 直接结束这个 Pass，避免后面恢复相机状态时空引用。
            }

            var shadowData = BurtShadowUtility.ResolveMainLightShadowData(request); // 从 request 中安全读取主光阴影参数。

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
                return; // 如果 Unity 无法计算阴影矩阵，说明当前主光或剔除数据不足，直接跳过阴影绘制。
            }

            var cmd = CommandBufferPool.Get(Name); // 从 Unity 命令缓冲池获取一个 CommandBuffer，并用 Pass 名称命名它。

            cmd.SetRenderTarget(shadowMapTarget.Identifier); // 把 MainLightShadowMap 绑定为当前渲染目标，后续 ShadowCaster 深度会写到这里。

            cmd.ClearRenderTarget(true, false, Color.clear); // 清理阴影图深度，避免上一帧或上一个 request 的深度残留影响当前阴影。

            cmd.SetViewProjectionMatrices(viewMatrix, projectionMatrix); // 把 GPU 当前矩阵切到主光视角，让 ShadowCaster 从灯光方向渲染。

            cmd.SetGlobalMatrix(MainLightWorldToShadowId, CreateWorldToShadowMatrix(viewMatrix, projectionMatrix)); // 上传世界空间到阴影纹理空间的矩阵，后续 Lit shader 会用它采样 shadow map。

            renderContext.ExecuteCommandBuffer(cmd); // 把绑定目标、清理深度和设置矩阵的命令提交给 ScriptableRenderContext。

            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。

            var shadowDrawingSettings = new ShadowDrawingSettings(request.CullingResults, shadowData.MainLightIndex); // 创建 Unity 阴影绘制设置，让 DrawShadows 找到主光对应的 ShadowCaster。

            shadowDrawingSettings.splitData = splitData; // 把 Unity 计算出的阴影裁剪数据交给 DrawShadows，避免绘制不在阴影范围内的物体。

            renderContext.DrawShadows(ref shadowDrawingSettings); // 绘制所有对主光投影的可见物体，shader 需要提供 LightMode=ShadowCaster 的 Pass。

            renderContext.SetupCameraProperties(camera); // 阴影绘制修改了视图投影矩阵，这里恢复相机矩阵，避免后续相机颜色 Pass 继续用灯光矩阵。
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

    internal sealed class BurtSetRenderTargetPass : BurtRenderPass // 定义设置渲染目标的 Pass，负责把 CameraColorTarget 和 CameraDepthTarget 绑定到 GPU。
    {
        public override string Name => "Burt Set Render Target"; // 返回这个 Pass 的名称，方便 CommandBuffer 和 Frame Debugger 显示。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            builder.WriteCameraColor(); // 声明这个 Pass 会把 CameraColor 设置为后续颜色绘制目标。

            builder.WriteCameraDepth(); // 声明这个 Pass 会让后续绘制使用当前相机深度缓冲。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现 BurtRenderPass 的执行函数。
        {
            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。

            var cameraColorTarget = context.CameraColorTarget; // 从 GraphContext 中取出 RenderGraph 的相机颜色目标句柄。

            var cameraDepthTarget = context.CameraDepthTarget; // 从 GraphContext 中取出 RenderGraph 的相机深度目标句柄。

            if (!cameraColorTarget.IsValid) // 如果 CameraColorTarget 无效，说明当前图没有可绑定的颜色输出目标。
            {
                return; // 直接结束这个 Pass，避免绑定 default RenderTargetIdentifier。
            }

            if (!cameraDepthTarget.IsValid) // 如果 CameraDepthTarget 无效，说明当前图没有可用的深度目标。
            {
                return; // 直接结束这个 Pass，避免后续绘制缺失深度缓冲语义。
            }

            var cmd = CommandBufferPool.Get(Name); // 从 Unity 命令缓冲池获取一个 CommandBuffer，并用 Pass 名称命名它。

            cmd.SetRenderTarget(cameraColorTarget.Identifier, cameraDepthTarget.Identifier); // 同时绑定颜色目标和 BurtRP 自己的深度目标，让后续绘制真正写入独立 CameraDepth。

            renderContext.ExecuteCommandBuffer(cmd); // 把 CommandBuffer 里的设置渲染目标命令提交给 ScriptableRenderContext。

            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。
        }
    }

    internal sealed class BurtClearRenderTargetPass : BurtRenderPass // 定义清屏 Pass，负责根据 BurtCameraData 清理颜色和深度缓冲。
    {
        public override string Name => "Burt Clear Render Target"; // 返回这个 Pass 的名称，方便 CommandBuffer 和 Frame Debugger 显示。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            builder.WriteCameraColor(); // 声明这个 Pass 会清理并写入 CameraColor。

            builder.WriteCameraDepth(); // 声明这个 Pass 会清理并写入 CameraDepth。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现 BurtRenderPass 的执行函数。
        {
            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。

            var request = context.Request; // 从 GraphContext 中取出当前渲染请求。

            var asset = context.Asset; // 从 GraphContext 中取出当前管线资产配置。

            var camera = request.Camera; // 从 request 中取出当前相机。

            var cameraData = request.CameraData; // 从 request 中取出当前相机的 BurtCameraData。

            var clearMode = BurtCameraClearMode.SolidColor; // 定义默认清屏模式，如果没有 BurtCameraData 就使用纯色清屏。

            if (cameraData != null) // 如果当前相机挂了 BurtCameraData，就使用相机自己的清屏配置。
            {
                clearMode = cameraData.ClearMode; // 从 BurtCameraData 中读取清屏模式。
            }

            if (clearMode == BurtCameraClearMode.DontClear) // 如果清屏模式是不清屏，就不需要执行 ClearRenderTarget。
            {
                return; // 直接结束这个 Pass，让后续绘制保留当前目标里的旧内容。
            }

            var clearDepth = true; // 当前只要不是 DontClear，就清理深度，避免上一相机的深度影响当前相机。

            var clearColorBuffer = false; // 默认不清理颜色缓冲，后面根据清屏模式决定是否改为 true。

            if (clearMode == BurtCameraClearMode.SolidColor) // 如果是纯色清屏模式，就需要清理颜色缓冲。
            {
                clearColorBuffer = true; // 标记需要清理颜色缓冲。
            }

            if (clearMode == BurtCameraClearMode.Skybox) // 如果是天空盒模式，也先清理颜色缓冲作为天空盒绘制前的底色。
            {
                clearColorBuffer = true; // 标记需要清理颜色缓冲。
            }

            var clearColor = Color.black; // 定义默认清屏颜色，避免 asset 或 cameraData 为空时没有兜底颜色。

            if (cameraData != null) // 如果相机有 BurtCameraData，就优先使用相机自己的清屏颜色。
            {
                clearColor = cameraData.ClearColor; // 从 BurtCameraData 中读取清屏颜色。
            }
            else if (asset != null) // 如果没有 BurtCameraData，但是管线资产存在，就使用管线资产的默认清屏颜色。
            {
                clearColor = asset.ClearColor; // 从 BurtRenderPipelineAsset 中读取默认清屏颜色。
            }

            if (clearMode == BurtCameraClearMode.Skybox && camera != null) // 如果是天空盒模式，并且当前相机有效，就使用相机背景色作为天空盒前的底色。
            {
                clearColor = camera.backgroundColor; // 使用 Unity Camera 的 backgroundColor 作为清屏颜色。
            }

            var cmd = CommandBufferPool.Get(Name); // 从 Unity 命令缓冲池获取一个 CommandBuffer，并用 Pass 名称命名它。

            cmd.ClearRenderTarget(clearDepth, clearColorBuffer, clearColor); // 向 CommandBuffer 写入清理深度和颜色缓冲的命令。

            renderContext.ExecuteCommandBuffer(cmd); // 把清屏命令提交给 ScriptableRenderContext。

            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。
        }
    }

    internal sealed class BurtSetupLightingPass : BurtRenderPass // Uploads request-level lighting data for BurtRP/Lit shaders.
    {
        private static readonly int MainLightDirectionId = Shader.PropertyToID("_BurtMainLightDirection"); // Caches the shader property ID for the main light direction.

        private static readonly int MainLightColorId = Shader.PropertyToID("_BurtMainLightColor"); // Caches the shader property ID for the main light color.

        private static readonly int AmbientLightColorId = Shader.PropertyToID("_BurtAmbientLightColor"); // Caches the shader property ID for the ambient light color.

        private static readonly int MainLightShadowStrengthId = Shader.PropertyToID("_BurtMainLightShadowStrength"); // 缓存主光阴影强度属性 ID，后续 Lit shader 用它决定是否采样 shadow map。

        public override string Name => "Burt Setup Lighting"; // Names this pass for RenderGraph debug output and Frame Debugger markers.

        public override void Configure(BurtRenderPassBuilder builder) // Declares the resources used by this pass.
        {
        } // This pass only uploads global shader constants, so it does not read or write RenderGraph render targets.

        public override void Execute(BurtRenderGraphContext context) // Executes the lighting setup pass.
        {
            var renderContext = context.ScriptableContext; // Reads Unity's SRP context so this pass can submit the CommandBuffer.

            var request = context.Request; // Reads the current render request so this pass can access precomputed lighting data.

            var lightingData = ResolveLightingData(request); // Gets request-level lighting data or a safe fallback when the request is missing it.

            var mainLightDirection = lightingData.MainLightDirection; // Reads the selected world-space direction toward the main light.

            var mainLightColor = lightingData.MainLightColor; // Reads the selected main light color.

            var ambientLightColor = lightingData.AmbientLightColor; // Reads the ambient light color stored during request creation.

            var mainLightShadowStrength = ResolveMainLightShadowStrength(lightingData); // 读取当前主光阴影强度，没有阴影时返回 0，避免 shader 使用上一帧阴影状态。

            var cmd = CommandBufferPool.Get(Name); // Gets a pooled CommandBuffer named after this pass.

            cmd.SetGlobalVector(MainLightDirectionId, new Vector4(mainLightDirection.x, mainLightDirection.y, mainLightDirection.z, 0f)); // Uploads the normalized world-space direction from the shaded point toward the main light.

            cmd.SetGlobalColor(MainLightColorId, mainLightColor); // Uploads the main light color that BurtRP/Lit multiplies with diffuse lighting.

            cmd.SetGlobalColor(AmbientLightColorId, ambientLightColor); // Uploads the ambient color that BurtRP/Lit adds as a small baseline light.

            cmd.SetGlobalFloat(MainLightShadowStrengthId, mainLightShadowStrength); // 上传主光阴影强度，让后续 Lit shader 可以在 0 时完全跳过阴影影响。

            renderContext.ExecuteCommandBuffer(cmd); // Submits the lighting globals to Unity's render context.

            CommandBufferPool.Release(cmd); // Releases the CommandBuffer back to Unity's pool to avoid per-frame allocations.
        }

        private static BurtLightingData ResolveLightingData(BurtRenderRequest request) // Returns lighting data for the request or creates a fallback object when it is unavailable.
        {
            if (request == null) // Checks whether the caller provided a render request.
            {
                return BurtLightingData.Default(); // Returns fallback lighting data because there is no request to read from.
            }

            if (request.LightingData == null) // Checks whether request creation failed to attach lighting data.
            {
                return BurtLightingData.Default(); // Returns fallback lighting data so shaders still receive valid globals.
            }

            return request.LightingData; // Returns the lighting data computed during request creation.
        }

        private static float ResolveMainLightShadowStrength(BurtLightingData lightingData) // 定义从灯光数据里安全读取主光阴影强度的辅助函数。
        {
            if (lightingData == null) // 如果灯光数据为空，说明当前 request 没有可用灯光上下文。
            {
                return 0f; // 返回 0 表示关闭阴影，避免 shader 采样无效 shadow map。
            }

            var shadowData = lightingData.ShadowData; // 从灯光数据中读取阴影数据。

            if (shadowData == null) // 如果阴影数据为空，说明当前 request 没有主光阴影信息。
            {
                return 0f; // 返回 0 表示关闭阴影。
            }

            if (!shadowData.HasMainLightShadow) // 如果主光没有开启有效阴影，shader 不应该产生阴影衰减。
            {
                return 0f; // 返回 0 表示关闭阴影。
            }

            return shadowData.MainLightShadowStrength; // 返回主光阴影强度，让 shader 按灯光 Inspector 的 Shadow Strength 混合阴影。
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

    internal sealed class BurtReleaseMainLightShadowMapPass : BurtRenderPass // 定义主光阴影图释放 Pass，负责在当前 request 结束时释放 shadow map 临时 RT。
    {
        public override string Name => "Burt Release Main Light Shadow Map"; // 返回这个 Pass 的名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            if (!BurtShadowUtility.ShouldUseMainLightShadow(builder.Request)) // 如果当前 request 没有主光阴影，就不声明阴影图读取。
            {
                return; // 直接结束资源声明，避免 Debug 输出无效的 MainLightShadowMap 依赖。
            }

            builder.ReadMainLightShadowMap(); // 声明这个 Pass 依赖 MainLightShadowMap，表示它要结束这个阴影资源的生命周期。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现主光阴影图释放 Pass 的执行函数。
        {
            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。

            var request = context.Request; // 从 GraphContext 中取出当前渲染请求。

            if (!BurtShadowUtility.ShouldUseMainLightShadow(request)) // 如果当前 request 不需要主光阴影，就不释放阴影图。
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

    internal sealed class BurtReleaseCameraDepthPass : BurtRenderPass // 定义 CameraDepth 释放 Pass，负责在当前 request 渲染结束后释放临时深度 RT。
    {
        public override string Name => "Burt Release Camera Depth"; // 返回这个 Pass 的名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            builder.ReadCameraDepth(); // 声明这个 Pass 依赖 CameraDepth，表示它要结束这个深度资源的生命周期。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现 CameraDepth 释放 Pass 的执行函数。
        {
            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。

            var cameraDepthTarget = context.CameraDepthTarget; // 从 GraphContext 中取出 CameraDepth 资源句柄。

            if (!cameraDepthTarget.IsValid) // 如果 CameraDepth 句柄无效，说明当前图没有申请过这个资源。
            {
                return; // 直接结束这个 Pass，避免释放不存在的临时 RT。
            }

            var cmd = CommandBufferPool.Get(Name); // 从 Unity 命令缓冲池获取一个 CommandBuffer，并用 Pass 名称命名它。

            cmd.ReleaseTemporaryRT(BurtRenderGraphResourceRegistry.CameraDepthTextureId); // 释放前面申请的 CameraDepth 临时 RT，避免资源泄漏。

            renderContext.ExecuteCommandBuffer(cmd); // 把释放 RT 的命令提交给 ScriptableRenderContext。

            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。
        }
    }
}
