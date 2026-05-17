namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让 Builder 和其他 BurtRP 代码处在同一个模块里。
{
    public sealed class BurtRenderPassBuilder // 定义 RenderPass 配置阶段使用的 Builder，用来让 Pass 声明资源读写关系。
    {
        public BurtRenderRequest Request { get; } // 保存当前渲染请求，供 Pass.Configure 根据相机任务或 request 类型调整资源声明。

        public BurtRenderPipelineAsset Asset { get; } // 保存当前管线资产，供 Pass.Configure 根据 Inspector 配置开关调整资源声明。

        public BurtRenderGraphResourceRegistry ResourceRegistry { get; } // 保存当前 RenderGraph 的资源注册表，Builder 通过它查找资源句柄。

        public BurtRenderPassResourceUsage Usage { get; } // 保存当前 Pass 的资源使用记录，RenderGraph 会在配置阶段收集它。

        public BurtRenderPassBuilder( // 保留旧构造函数，避免已有外部测试或工具直接创建 Builder 时失效。
            BurtRenderPass pass, // 接收正在配置的 RenderPass，用它的名称创建资源使用记录。
            BurtRenderRequest request, // 接收当前 RenderGraph 正在处理的渲染请求，让 Pass 可以根据相机任务声明资源。
            BurtRenderPipelineAsset asset, // 接收当前 BurtRP 管线资产，让 Pass 可以根据 Inspector 开关声明资源。
            BurtRenderGraphResourceRegistry resourceRegistry) // 接收当前 RenderGraph 的资源注册表。
            : this(-1, pass, request, asset, resourceRegistry)
        {
        }

        public BurtRenderPassBuilder( // 定义构造函数，用来为某个 Pass 创建资源声明 Builder。
            int passIndex, // 接收 Pass 在 RenderGraph 中的顺序索引，用于 Debug 输出。
            BurtRenderPass pass, // 接收正在配置的 RenderPass，用它的名称创建资源使用记录。
            BurtRenderRequest request, // 接收当前 RenderGraph 正在处理的渲染请求，让 Pass 可以根据相机任务声明资源。
            BurtRenderPipelineAsset asset, // 接收当前 BurtRP 管线资产，让 Pass 可以根据 Inspector 开关声明资源。
            BurtRenderGraphResourceRegistry resourceRegistry) // 接收当前 RenderGraph 的资源注册表。
        {
            Request = request; // 把当前渲染请求保存到 Request 属性里，供 Pass.Configure 使用。

            Asset = asset; // 把当前管线资产保存到 Asset 属性里，供 Pass.Configure 使用。

            ResourceRegistry = resourceRegistry; // 把资源注册表保存到 ResourceRegistry 属性里。

            var passName = pass != null ? pass.Name : "NullPass"; // 如果 Pass 存在就读取它的名称，否则使用空 Pass 兜底名称。

            var passKind = pass != null ? pass.Kind : BurtRenderPassKindUtility.InferKind(passName); // Read pass metadata once so debug records match execution.

            var hasSideEffects = pass == null || pass.HasSideEffects; // Null pass records stay conservative.

            var allowCulling = pass != null && pass.AllowCulling; // Culling remains disabled unless a pass explicitly opts in.

            Usage = new BurtRenderPassResourceUsage(passIndex, passName, passKind, hasSideEffects, allowCulling); // 为当前 Pass 创建一份带顺序索引的资源使用记录。
        }

        public BurtRenderTargetHandle ReadRenderTarget(string name) // 定义声明读取某个渲染目标资源的函数。
        {
            if (string.IsNullOrEmpty(name)) // 空资源名会让依赖图无法准确定位生产者和消费者。
            {
                Usage.AddValidationMessage("ReadRenderTarget 收到空资源名。"); // 记录空名问题，但仍返回无效句柄保持旧流程安全。
            }

            var handle = GetRenderTarget(name); // 从资源注册表里读取指定名称的渲染目标句柄。

            Usage.AddReadRenderTarget(handle); // 把这个句柄记录为当前 Pass 的读取资源。

            return handle; // 返回这个句柄，方便 Pass 后续需要时继续使用。
        }

        public BurtRenderTargetHandle WriteRenderTarget(string name) // 定义声明写入某个渲染目标资源的函数。
        {
            if (string.IsNullOrEmpty(name)) // 空资源名会让依赖图无法准确定位生产者和消费者。
            {
                Usage.AddValidationMessage("WriteRenderTarget 收到空资源名。"); // 记录空名问题，但仍返回无效句柄保持旧流程安全。
            }

            var handle = GetRenderTarget(name); // 从资源注册表里读取指定名称的渲染目标句柄。

            Usage.AddWriteRenderTarget(handle); // 把这个句柄记录为当前 Pass 的写入资源。

            return handle; // 返回这个句柄，方便 Pass 后续需要时继续使用。
        }

        public BurtRenderBufferHandle ReadBuffer(string name) // Declares that the pass reads a logical buffer resource.
        {
            if (string.IsNullOrEmpty(name))
            {
                Usage.AddValidationMessage("ReadBuffer 收到空资源名。");
            }

            var handle = GetBuffer(name);

            Usage.AddReadBuffer(handle);

            return handle;
        }

        public BurtRenderBufferHandle WriteBuffer(string name) // Declares that the pass writes a logical buffer resource.
        {
            if (string.IsNullOrEmpty(name))
            {
                Usage.AddValidationMessage("WriteBuffer 收到空资源名。");
            }

            var handle = GetBuffer(name);

            Usage.AddWriteBuffer(handle);

            return handle;
        }

        public void AllowUnconsumedWrite(string resourceName) // Marks a written resource as an intentional terminal side effect.
        {
            Usage.AllowUnconsumedWriteResource(resourceName);
        }

        public void AllowUnconsumedRenderTargetWrite(string name) // Marks a render target write that intentionally has no later consumer.
        {
            if (string.IsNullOrEmpty(name))
            {
                Usage.AddValidationMessage("AllowUnconsumedRenderTargetWrite 收到空资源名。");
            }

            var handle = GetRenderTarget(name);
            if (!handle.IsValid)
            {
                Usage.AddValidationMessage("AllowUnconsumedRenderTargetWrite 引用缺失资源: " + (string.IsNullOrEmpty(handle.Name) ? "<empty>" : handle.Name));
            }

            Usage.AllowUnconsumedWriteResource(handle.Name);
        }

        public void AllowUnconsumedBufferWrite(string name) // Marks a logical buffer write that intentionally has no later consumer.
        {
            if (string.IsNullOrEmpty(name))
            {
                Usage.AddValidationMessage("AllowUnconsumedBufferWrite 收到空资源名。");
            }

            var handle = GetBuffer(name);
            if (!handle.IsValid)
            {
                Usage.AddValidationMessage("AllowUnconsumedBufferWrite 引用缺失 Buffer: " + (string.IsNullOrEmpty(handle.Name) ? "<empty>" : handle.Name));
            }

            Usage.AllowUnconsumedWriteResource(handle.Name);
        }

        public void AllowUnconsumedGlobalWrite(string resourceName) // Marks a logical global write that intentionally has no later consumer.
        {
            Usage.AllowUnconsumedWriteResource(resourceName);
        }

        public BurtRenderBufferHandle ReadAdditionalLightBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.AdditionalLightBufferName);
        }

        public BurtRenderBufferHandle WriteAdditionalLightBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.AdditionalLightBufferName);
        }

        public BurtRenderBufferHandle ReadTileLightCountBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.TileLightCountBufferName);
        }

        public BurtRenderBufferHandle WriteTileLightCountBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.TileLightCountBufferName);
        }

        public BurtRenderBufferHandle ReadTileLightListBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.TileLightListBufferName);
        }

        public BurtRenderBufferHandle WriteTileLightListBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.TileLightListBufferName);
        }

        public BurtRenderBufferHandle ReadTileLightOffsetBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.TileLightOffsetBufferName);
        }

        public BurtRenderBufferHandle WriteTileLightOffsetBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.TileLightOffsetBufferName);
        }

        public BurtRenderBufferHandle ReadClusterLightListBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.ClusterLightListBufferName);
        }

        public BurtRenderBufferHandle WriteClusterLightListBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.ClusterLightListBufferName);
        }

        public BurtRenderTargetHandle ReadCameraColor() // 定义声明读取 CameraColor 的快捷函数。
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.CameraColorName); // 使用统一资源名声明读取 CameraColor。
        }

        public BurtRenderTargetHandle WriteCameraColor() // 定义声明写入 CameraColor 的快捷函数。
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.CameraColorName); // 使用统一资源名声明写入 CameraColor。
        }

        public BurtRenderTargetHandle ReadFinalCameraTarget() // 定义声明读取最终相机输出目标的快捷函数。
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.FinalCameraTargetName); // 使用统一资源名声明读取 FinalCameraTarget。
        }

        public BurtRenderTargetHandle WriteFinalCameraTarget() // 定义声明写入最终相机输出目标的快捷函数。
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.FinalCameraTargetName); // 使用统一资源名声明写入 FinalCameraTarget。
        }

        public BurtRenderTargetHandle ReadCameraDepth() // 定义声明读取 CameraDepth 的快捷函数。
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.CameraDepthName); // 使用统一资源名声明读取 CameraDepth。
        }

        public BurtRenderTargetHandle WriteCameraDepth() // 定义声明写入 CameraDepth 的快捷函数。
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.CameraDepthName); // 使用统一资源名声明写入 CameraDepth。
        }

        public BurtRenderTargetHandle ReadPostProcessColor() // 定义声明读取 PostProcessColor 的快捷函数。
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.PostProcessColorName); // 使用统一资源名声明读取后处理中间颜色。
        }

        public BurtRenderTargetHandle WritePostProcessColor() // 定义声明写入 PostProcessColor 的快捷函数。
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.PostProcessColorName); // 使用统一资源名声明写入后处理中间颜色。
        }

        public BurtRenderTargetHandle ReadGBuffer0() // 定义声明读取 GBuffer0 的快捷函数。
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.GBuffer0Name); // 使用统一资源名声明读取 Deferred GBuffer0。
        }

        public BurtRenderTargetHandle WriteGBuffer0() // 定义声明写入 GBuffer0 的快捷函数。
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.GBuffer0Name); // 使用统一资源名声明写入 Deferred GBuffer0。
        }

        public BurtRenderTargetHandle ReadGBuffer1() // 定义声明读取 GBuffer1 的快捷函数。
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.GBuffer1Name); // 使用统一资源名声明读取 Deferred GBuffer1。
        }

        public BurtRenderTargetHandle WriteGBuffer1() // 定义声明写入 GBuffer1 的快捷函数。
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.GBuffer1Name); // 使用统一资源名声明写入 Deferred GBuffer1。
        }

        public BurtRenderTargetHandle ReadGBuffer2() // 定义声明读取 GBuffer2 的快捷函数。
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.GBuffer2Name); // 使用统一资源名声明读取 Deferred GBuffer2。
        }

        public BurtRenderTargetHandle WriteGBuffer2() // 定义声明写入 GBuffer2 的快捷函数。
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.GBuffer2Name); // 使用统一资源名声明写入 Deferred GBuffer2。
        }

        public BurtRenderTargetHandle ReadGBuffer3() // 定义声明读取 GBuffer3 的快捷函数。
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.GBuffer3Name); // 使用统一资源名声明读取 Deferred GBuffer3。
        }

        public BurtRenderTargetHandle WriteGBuffer3() // 定义声明写入 GBuffer3 的快捷函数。
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.GBuffer3Name); // 使用统一资源名声明写入 Deferred GBuffer3。
        }

        public BurtRenderTargetHandle ReadHiZDepth()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.HiZDepthName);
        }

        public BurtRenderTargetHandle WriteHiZDepth()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.HiZDepthName);
        }

        public BurtRenderTargetHandle ReadScreenSpaceReflectionColor()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceReflectionColorName);
        }

        public BurtRenderTargetHandle WriteScreenSpaceReflectionColor()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceReflectionColorName);
        }

        public BurtRenderTargetHandle ReadScreenSpaceReflectionDenoisedColor()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceReflectionDenoisedColorName);
        }

        public BurtRenderTargetHandle WriteScreenSpaceReflectionDenoisedColor()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceReflectionDenoisedColorName);
        }

        public BurtRenderTargetHandle ReadScreenSpaceReflectionTemporalColor()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceReflectionTemporalColorName);
        }

        public BurtRenderTargetHandle WriteScreenSpaceReflectionTemporalColor()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceReflectionTemporalColorName);
        }

        public BurtRenderTargetHandle ReadScreenSpaceAmbientOcclusionRaw()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionRawName);
        }

        public BurtRenderTargetHandle WriteScreenSpaceAmbientOcclusionRaw()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionRawName);
        }

        public BurtRenderTargetHandle ReadScreenSpaceAmbientOcclusion()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionName);
        }

        public BurtRenderTargetHandle WriteScreenSpaceAmbientOcclusion()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionName);
        }

        public BurtRenderTargetHandle ReadMainLightShadowMap() // 定义声明读取 MainLightShadowMap 的快捷函数。
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.MainLightShadowMapName); // 使用统一资源名声明读取主光阴影图。
        }

        public BurtRenderTargetHandle WriteMainLightShadowMap() // 定义声明写入 MainLightShadowMap 的快捷函数。
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.MainLightShadowMapName); // 使用统一资源名声明写入主光阴影图。
        }

        public void ReadGlobalResource(string resourceName) // 定义声明读取逻辑全局资源的通用函数。
        {
            Usage.AddReadGlobalResource(resourceName); // 把逻辑全局资源读取关系记录到当前 Pass 的资源使用信息里。
        }

        public void WriteGlobalResource(string resourceName) // 定义声明写入逻辑全局资源的通用函数。
        {
            Usage.AddWriteGlobalResource(resourceName); // 把逻辑全局资源写入关系记录到当前 Pass 的资源使用信息里。
        }

        public void ReadLightingGlobals() // 定义声明读取灯光全局状态的快捷函数。
        {
            ReadGlobalResource(BurtRenderGraphResourceRegistry.LightingGlobalsName); // 使用统一逻辑资源名声明读取 LightingGlobals。
            ReadAdditionalLightBuffer();
        }

        public void WriteLightingGlobals() // 定义声明写入灯光全局状态的快捷函数。
        {
            WriteGlobalResource(BurtRenderGraphResourceRegistry.LightingGlobalsName); // 使用统一逻辑资源名声明写入 LightingGlobals。
            WriteAdditionalLightBuffer();
        }

        public void ReadShadowGlobals() // 定义声明读取阴影全局状态的快捷函数。
        {
            ReadGlobalResource(BurtRenderGraphResourceRegistry.ShadowGlobalsName); // 使用统一逻辑资源名声明读取 ShadowGlobals。
            if (BurtAdditionalLightShadowUtility.ShouldUseAdditionalLightShadows(Request))
            {
                ReadAdditionalLightShadowAtlas();
            }
        }

        public void WriteShadowGlobals() // 定义声明写入阴影全局状态的快捷函数。
        {
            WriteGlobalResource(BurtRenderGraphResourceRegistry.ShadowGlobalsName); // 使用统一逻辑资源名声明写入 ShadowGlobals。
        }

        private BurtRenderTargetHandle GetRenderTarget(string name) // 定义从资源表读取渲染目标的内部辅助函数。
        {
            if (ResourceRegistry == null) // 如果资源注册表为空，说明当前 Builder 没有可查询的资源来源。
            {
                Usage.AddValidationMessage("资源注册表为空，无法解析资源: " + (string.IsNullOrEmpty(name) ? "<empty>" : name)); // 记录资源表缺失，避免 Debug 只看到 Invalid。

                return BurtRenderTargetHandle.Invalid(name); // 返回无效句柄，让使用记录保留资源名但不绑定真实目标。
            }

            return ResourceRegistry.GetRenderTarget(name); // 从资源注册表读取指定名称的渲染目标句柄。
        }

        public BurtRenderTargetHandle ReadAdditionalLightShadowAtlas()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.AdditionalLightShadowAtlasName);
        }

        public BurtRenderTargetHandle WriteAdditionalLightShadowAtlas()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.AdditionalLightShadowAtlasName);
        }

        private BurtRenderBufferHandle GetBuffer(string name) // Reads a logical buffer from the registry while preserving invalid handles for diagnostics.
        {
            if (ResourceRegistry == null)
            {
                Usage.AddValidationMessage("资源注册表为空，无法解析 Buffer: " + (string.IsNullOrEmpty(name) ? "<empty>" : name));

                return BurtRenderBufferHandle.Invalid(name);
            }

            return ResourceRegistry.GetBuffer(name);
        }
    }
}
