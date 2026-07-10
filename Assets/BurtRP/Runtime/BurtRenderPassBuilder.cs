namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让 Builder 和其他 BurtRP 代码处在同一个模块里。
{
    public sealed class BurtRenderPassBuilder // 定义 RenderPass 配置阶段使用的 Builder，用来让 Pass 声明资源读写关系。
    {
        public BurtRenderRequest Request { get; } // 保存当前渲染请求，供 Pass.Configure 根据相机任务或 request 类型调整资源声明。

        public BurtRenderPipelineAsset Asset { get; } // 保存当前管线资产，供 Pass.Configure 根据 Inspector 配置开关调整资源声明。

        public BurtRequestRenderOptions RenderOptions { get; }

        public BurtRenderGraphResourceRegistry ResourceRegistry { get; } // 保存当前 RenderGraph 的资源注册表，Builder 通过它查找资源句柄。

        public BurtRenderPassResourceUsage Usage { get; } // 保存当前 Pass 的资源使用记录，RenderGraph 会在配置阶段收集它。

        public BurtRenderPassBuilder( // 保留旧构造函数，避免已有外部测试或工具直接创建 Builder 时失效。
            BurtRenderPass pass, // 接收正在配置的 RenderPass，用它的名称创建资源使用记录。
            BurtRenderRequest request, // 接收当前 RenderGraph 正在处理的渲染请求，让 Pass 可以根据相机任务声明资源。
            BurtRenderPipelineAsset asset, // 接收当前 BurtRP 管线资产，让 Pass 可以根据 Inspector 开关声明资源。
            BurtRenderGraphResourceRegistry resourceRegistry, // 接收当前 RenderGraph 的资源注册表。
            BurtRequestRenderOptions renderOptions = null)
        {
            Request = request; // 把当前渲染请求保存到 Request 属性里，供 Pass.Configure 使用。

            Asset = asset; // 把当前管线资产保存到 Asset 属性里，供 Pass.Configure 使用。

            RenderOptions = renderOptions;

            ResourceRegistry = resourceRegistry; // 把资源注册表保存到 ResourceRegistry 属性里。

            Usage = CreateUsage(-1, pass); // 为旧入口创建不带图内索引的资源使用记录。
        }

        public BurtRenderPassBuilder( // 定义构造函数，用来为某个 Pass 创建资源声明 Builder。
            int passIndex, // 接收 Pass 在 RenderGraph 中的顺序索引，用于 Debug 输出。
            BurtRenderPass pass, // 接收正在配置的 RenderPass，用它的名称创建资源使用记录。
            BurtRenderRequest request, // 接收当前 RenderGraph 正在处理的渲染请求，让 Pass 可以根据相机任务声明资源。
            BurtRenderPipelineAsset asset, // 接收当前 BurtRP 管线资产，让 Pass 可以根据 Inspector 开关声明资源。
            BurtRenderGraphResourceRegistry resourceRegistry, // 接收当前 RenderGraph 的资源注册表。
            BurtRequestRenderOptions renderOptions = null) // Receives current request render options.
        {
            Request = request; // 把当前渲染请求保存到 Request 属性里，供 Pass.Configure 使用。

            Asset = asset; // 把当前管线资产保存到 Asset 属性里，供 Pass.Configure 使用。

            RenderOptions = renderOptions;

            ResourceRegistry = resourceRegistry; // 把资源注册表保存到 ResourceRegistry 属性里。

            Usage = CreateUsage(passIndex, pass); // 为当前 Pass 创建一份带顺序索引的资源使用记录。
        }

        private static BurtRenderPassResourceUsage CreateUsage(int passIndex, BurtRenderPass pass)
        {
            var passName = pass != null ? pass.Name : "NullPass";
            var passKind = pass != null ? pass.Kind : BurtRenderPassKindUtility.InferKind(passName);
            var hasSideEffects = pass == null || pass.HasSideEffects;
            var allowCulling = pass != null && pass.AllowCulling;
            return new BurtRenderPassResourceUsage(passIndex, passName, passKind, hasSideEffects, allowCulling);
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

        public BurtRenderBufferHandle ReadClusterLightCountBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.ClusterLightCountBufferName);
        }

        public BurtRenderBufferHandle WriteClusterLightCountBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.ClusterLightCountBufferName);
        }

        public BurtRenderBufferHandle ReadClusterLightOffsetBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.ClusterLightOffsetBufferName);
        }

        public BurtRenderBufferHandle WriteClusterLightOffsetBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.ClusterLightOffsetBufferName);
        }

        public BurtRenderBufferHandle ReadBurtGIScreenProbeIndirectArgsBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeIndirectArgsBufferName);
        }

        public BurtRenderBufferHandle WriteBurtGIScreenProbeIndirectArgsBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeIndirectArgsBufferName);
        }

        public BurtRenderBufferHandle ReadBurtGIScreenProbeTraceCompactTexelCountBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceCompactTexelCountBufferName);
        }

        public BurtRenderBufferHandle WriteBurtGIScreenProbeTraceCompactTexelCountBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceCompactTexelCountBufferName);
        }

        public BurtRenderBufferHandle ReadBurtGIScreenProbeTraceCompactTexelDataBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceCompactTexelDataBufferName);
        }

        public BurtRenderBufferHandle WriteBurtGIScreenProbeTraceCompactTexelDataBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceCompactTexelDataBufferName);
        }

        public BurtRenderBufferHandle ReadBurtGIScreenProbeTraceCompactIndirectArgsBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceCompactIndirectArgsBufferName);
        }

        public BurtRenderBufferHandle WriteBurtGIScreenProbeTraceCompactIndirectArgsBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceCompactIndirectArgsBufferName);
        }

        public BurtRenderBufferHandle ReadBurtGIScreenProbeTraceCompactThreadCountXBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceCompactThreadCountXBufferName);
        }

        public BurtRenderBufferHandle WriteBurtGIScreenProbeTraceCompactThreadCountXBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceCompactThreadCountXBufferName);
        }

        public BurtRenderBufferHandle ReadBurtGIScreenProbeAdaptiveProbeNumBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeAdaptiveProbeNumBufferName);
        }

        public BurtRenderBufferHandle WriteBurtGIScreenProbeAdaptiveProbeNumBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeAdaptiveProbeNumBufferName);
        }

        public BurtRenderBufferHandle ReadBurtGIScreenProbeAdaptiveProbeDataBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeAdaptiveProbeDataBufferName);
        }

        public BurtRenderBufferHandle WriteBurtGIScreenProbeAdaptiveProbeDataBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeAdaptiveProbeDataBufferName);
        }

        public BurtRenderBufferHandle ReadBurtGIRadianceCacheClipMapProbeAllocatorBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeAllocatorBufferName);
        }

        public BurtRenderBufferHandle WriteBurtGIRadianceCacheClipMapProbeAllocatorBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeAllocatorBufferName);
        }

        public BurtRenderBufferHandle ReadBurtGIRadianceCacheClipMapProbeFreeListAllocatorBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeFreeListAllocatorBufferName);
        }

        public BurtRenderBufferHandle WriteBurtGIRadianceCacheClipMapProbeFreeListAllocatorBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeFreeListAllocatorBufferName);
        }

        public BurtRenderBufferHandle ReadBurtGIRadianceCacheClipMapProbeFreeListBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeFreeListBufferName);
        }

        public BurtRenderBufferHandle WriteBurtGIRadianceCacheClipMapProbeFreeListBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeFreeListBufferName);
        }

        public BurtRenderBufferHandle ReadBurtGIRadianceCacheClipMapProbeLastUsedFrameBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeLastUsedFrameBufferName);
        }

        public BurtRenderBufferHandle WriteBurtGIRadianceCacheClipMapProbeLastUsedFrameBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeLastUsedFrameBufferName);
        }

        public BurtRenderBufferHandle ReadBurtGIRadianceCacheClipMapProbeLastTracedFrameBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeLastTracedFrameBufferName);
        }

        public BurtRenderBufferHandle WriteBurtGIRadianceCacheClipMapProbeLastTracedFrameBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeLastTracedFrameBufferName);
        }

        public BurtRenderBufferHandle ReadBurtGIRadianceCacheClipMapProbeWorldOffsetBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeWorldOffsetBufferName);
        }

        public BurtRenderBufferHandle WriteBurtGIRadianceCacheClipMapProbeWorldOffsetBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeWorldOffsetBufferName);
        }

        public BurtRenderBufferHandle ReadBurtGIRadianceCacheClipMapProbeTraceDataBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeTraceDataBufferName);
        }

        public BurtRenderBufferHandle WriteBurtGIRadianceCacheClipMapProbeTraceDataBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeTraceDataBufferName);
        }

        public BurtRenderBufferHandle ReadBurtGIRadianceCacheClipMapProbeTraceAllocatorBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeTraceAllocatorBufferName);
        }

        public BurtRenderBufferHandle WriteBurtGIRadianceCacheClipMapProbeTraceAllocatorBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeTraceAllocatorBufferName);
        }

        public BurtRenderBufferHandle ReadBurtGIRadianceCacheClipMapPriorityHistogramBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapPriorityHistogramBufferName);
        }

        public BurtRenderBufferHandle WriteBurtGIRadianceCacheClipMapPriorityHistogramBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapPriorityHistogramBufferName);
        }

        public BurtRenderBufferHandle ReadBurtGIRadianceCacheClipMapMaxUpdateBucketBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapMaxUpdateBucketBufferName);
        }

        public BurtRenderBufferHandle WriteBurtGIRadianceCacheClipMapMaxUpdateBucketBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapMaxUpdateBucketBufferName);
        }

        public BurtRenderBufferHandle ReadBurtGIRadianceCacheClipMapMaxTracesFromMaxUpdateBucketBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapMaxTracesFromMaxUpdateBucketBufferName);
        }

        public BurtRenderBufferHandle WriteBurtGIRadianceCacheClipMapMaxTracesFromMaxUpdateBucketBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapMaxTracesFromMaxUpdateBucketBufferName);
        }

        public BurtRenderBufferHandle ReadBurtGIRadianceCacheClipMapProbesToUpdateTraceCostBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbesToUpdateTraceCostBufferName);
        }

        public BurtRenderBufferHandle WriteBurtGIRadianceCacheClipMapProbesToUpdateTraceCostBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbesToUpdateTraceCostBufferName);
        }

        public BurtRenderBufferHandle ReadBurtGIRadianceCacheClipMapRadianceProbePDFBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapRadianceProbePDFBufferName);
        }

        public BurtRenderBufferHandle WriteBurtGIRadianceCacheClipMapRadianceProbePDFBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapRadianceProbePDFBufferName);
        }

        public BurtRenderBufferHandle ReadBurtGIRadianceCacheClipMapClearProbePDFsIndirectArgsBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapClearProbePDFsIndirectArgsBufferName);
        }

        public BurtRenderBufferHandle WriteBurtGIRadianceCacheClipMapClearProbePDFsIndirectArgsBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapClearProbePDFsIndirectArgsBufferName);
        }

        public BurtRenderBufferHandle ReadBurtGIRadianceCacheClipMapGenerateProbeTraceTilesIndirectArgsBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapGenerateProbeTraceTilesIndirectArgsBufferName);
        }

        public BurtRenderBufferHandle WriteBurtGIRadianceCacheClipMapGenerateProbeTraceTilesIndirectArgsBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapGenerateProbeTraceTilesIndirectArgsBufferName);
        }

        public BurtRenderBufferHandle ReadBurtGIRadianceCacheClipMapProbeTraceTileAllocatorBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeTraceTileAllocatorBufferName);
        }

        public BurtRenderBufferHandle WriteBurtGIRadianceCacheClipMapProbeTraceTileAllocatorBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeTraceTileAllocatorBufferName);
        }

        public BurtRenderBufferHandle ReadBurtGIRadianceCacheClipMapFilterProbesIndirectArgsBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapFilterProbesIndirectArgsBufferName);
        }

        public BurtRenderBufferHandle WriteBurtGIRadianceCacheClipMapFilterProbesIndirectArgsBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapFilterProbesIndirectArgsBufferName);
        }

        public BurtRenderBufferHandle ReadBurtGIRadianceCacheClipMapPrepareProbeOcclusionIndirectArgsBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapPrepareProbeOcclusionIndirectArgsBufferName);
        }

        public BurtRenderBufferHandle WriteBurtGIRadianceCacheClipMapPrepareProbeOcclusionIndirectArgsBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapPrepareProbeOcclusionIndirectArgsBufferName);
        }

        public BurtRenderBufferHandle ReadBurtGIRadianceCacheClipMapFixupProbeBordersIndirectArgsBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapFixupProbeBordersIndirectArgsBufferName);
        }

        public BurtRenderBufferHandle WriteBurtGIRadianceCacheClipMapFixupProbeBordersIndirectArgsBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapFixupProbeBordersIndirectArgsBufferName);
        }

        public BurtRenderBufferHandle ReadBurtGIRadianceCacheClipMapTraceProbesIndirectArgsBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapTraceProbesIndirectArgsBufferName);
        }

        public BurtRenderBufferHandle WriteBurtGIRadianceCacheClipMapTraceProbesIndirectArgsBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapTraceProbesIndirectArgsBufferName);
        }

        public BurtRenderBufferHandle ReadBurtGIRadianceCacheClipMapSortProbeTraceTilesIndirectArgsBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapSortProbeTraceTilesIndirectArgsBufferName);
        }

        public BurtRenderBufferHandle WriteBurtGIRadianceCacheClipMapSortProbeTraceTilesIndirectArgsBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapSortProbeTraceTilesIndirectArgsBufferName);
        }

        public BurtRenderBufferHandle ReadBurtGIRadianceCacheClipMapRadianceCacheHardwareRayTracingIndirectArgsBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapRadianceCacheHardwareRayTracingIndirectArgsBufferName);
        }

        public BurtRenderBufferHandle WriteBurtGIRadianceCacheClipMapRadianceCacheHardwareRayTracingIndirectArgsBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapRadianceCacheHardwareRayTracingIndirectArgsBufferName);
        }

        public BurtRenderBufferHandle ReadBurtGIRadianceCacheClipMapHardwareRayTracingRayAllocatorBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapHardwareRayTracingRayAllocatorBufferName);
        }

        public BurtRenderBufferHandle WriteBurtGIRadianceCacheClipMapHardwareRayTracingRayAllocatorBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapHardwareRayTracingRayAllocatorBufferName);
        }

        public BurtRenderBufferHandle ReadBurtGIRadianceCacheClipMapProbeTraceTileDataBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeTraceTileDataBufferName);
        }

        public BurtRenderBufferHandle WriteBurtGIRadianceCacheClipMapProbeTraceTileDataBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeTraceTileDataBufferName);
        }

        public BurtRenderBufferHandle ReadBurtGIRadianceCacheClipMapSortedProbeTraceTileDataBuffer()
        {
            return ReadBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapSortedProbeTraceTileDataBufferName);
        }

        public BurtRenderBufferHandle WriteBurtGIRadianceCacheClipMapSortedProbeTraceTileDataBuffer()
        {
            return WriteBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapSortedProbeTraceTileDataBufferName);
        }

        public BurtRenderTargetHandle ReadCameraColor() // 定义声明读取 CameraColor 的快捷函数。
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.CameraColorName); // 使用统一资源名声明读取 CameraColor。
        }

        public BurtRenderTargetHandle WriteCameraColor() // 定义声明写入 CameraColor 的快捷函数。
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.CameraColorName); // 使用统一资源名声明写入 CameraColor。
        }

        public BurtRenderTargetHandle ReadOpaqueCameraColor()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.OpaqueCameraColorName);
        }

        public BurtRenderTargetHandle WriteOpaqueCameraColor()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.OpaqueCameraColorName);
        }

        public BurtRenderTargetHandle ReadRefractionDistortion()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.RefractionDistortionName);
        }

        public BurtRenderTargetHandle WriteRefractionDistortion()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.RefractionDistortionName);
        }

        public BurtRenderTargetHandle ReadRefractionSceneColorMipChain()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.RefractionSceneColorMipChainName);
        }

        public BurtRenderTargetHandle WriteRefractionSceneColorMipChain()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.RefractionSceneColorMipChainName);
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

        public BurtRenderTargetHandle ReadDeferredLightingDepth()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.DeferredLightingDepthName);
        }

        public BurtRenderTargetHandle WriteDeferredLightingDepth()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.DeferredLightingDepthName);
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

        public BurtRenderTargetHandle ReadGBuffer4()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.GBuffer4Name);
        }

        public BurtRenderTargetHandle WriteGBuffer4()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.GBuffer4Name);
        }

        public BurtRenderTargetHandle ReadGBuffer5()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.GBuffer5Name);
        }

        public BurtRenderTargetHandle WriteGBuffer5()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.GBuffer5Name);
        }

        public BurtRenderTargetHandle ReadGBufferObjectIndex()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.GBufferObjectIndexName);
        }

        public BurtRenderTargetHandle WriteGBufferObjectIndex()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.GBufferObjectIndexName);
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

        public BurtRenderTargetHandle ReadScreenSpaceShadow()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceShadowName);
        }

        public BurtRenderTargetHandle WriteScreenSpaceShadow()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceShadowName);
        }

        public BurtRenderTargetHandle ReadScreenSpaceGlobalIlluminationRaw()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationRawName);
        }

        public BurtRenderTargetHandle WriteScreenSpaceGlobalIlluminationRaw()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationRawName);
        }

        public BurtRenderTargetHandle ReadScreenSpaceGlobalIllumination()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationName);
        }

        public BurtRenderTargetHandle WriteScreenSpaceGlobalIllumination()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationName);
        }

        public BurtRenderTargetHandle ReadBurtGIBackfaceDiffuseIndirect()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIBackfaceDiffuseIndirectName);
        }

        public BurtRenderTargetHandle WriteBurtGIBackfaceDiffuseIndirect()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIBackfaceDiffuseIndirectName);
        }

        public BurtRenderTargetHandle ReadBurtGIRoughSpecularIndirect()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIRoughSpecularIndirectName);
        }

        public BurtRenderTargetHandle WriteBurtGIRoughSpecularIndirect()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIRoughSpecularIndirectName);
        }

        public BurtRenderTargetHandle ReadBurtGITranslucencyVolume0()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGITranslucencyVolume0Name);
        }

        public BurtRenderTargetHandle WriteBurtGITranslucencyVolume0()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGITranslucencyVolume0Name);
        }

        public BurtRenderTargetHandle ReadBurtGITranslucencyVolume1()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGITranslucencyVolume1Name);
        }

        public BurtRenderTargetHandle WriteBurtGITranslucencyVolume1()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGITranslucencyVolume1Name);
        }

        public BurtRenderTargetHandle ReadBurtGITranslucencyVolumeFilter0()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGITranslucencyVolumeFilter0Name);
        }

        public BurtRenderTargetHandle WriteBurtGITranslucencyVolumeFilter0()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGITranslucencyVolumeFilter0Name);
        }

        public BurtRenderTargetHandle ReadBurtGITranslucencyVolumeFilter1()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGITranslucencyVolumeFilter1Name);
        }

        public BurtRenderTargetHandle WriteBurtGITranslucencyVolumeFilter1()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGITranslucencyVolumeFilter1Name);
        }

        public BurtRenderTargetHandle ReadBurtGISceneVoxelRadiance()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGISceneVoxelRadianceName);
        }

        public BurtRenderTargetHandle WriteBurtGISceneVoxelRadiance()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGISceneVoxelRadianceName);
        }

        public BurtRenderTargetHandle ReadBurtGISceneVoxelGeometry()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGISceneVoxelGeometryName);
        }

        public BurtRenderTargetHandle WriteBurtGISceneVoxelGeometry()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGISceneVoxelGeometryName);
        }

        public BurtRenderTargetHandle ReadBurtGISceneVoxelOccupancyMip()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGISceneVoxelOccupancyMipName);
        }

        public BurtRenderTargetHandle WriteBurtGISceneVoxelOccupancyMip()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGISceneVoxelOccupancyMipName);
        }

        public BurtRenderTargetHandle ReadBurtGISceneVoxelLighting()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGISceneVoxelLightingName);
        }

        public BurtRenderTargetHandle WriteBurtGISceneVoxelLighting()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGISceneVoxelLightingName);
        }

        public BurtRenderTargetHandle ReadBurtGITemporalDiagnostics()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGITemporalDiagnosticsName);
        }

        public BurtRenderTargetHandle WriteBurtGITemporalDiagnostics()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGITemporalDiagnosticsName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeScreenDepth()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeScreenDepthName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeScreenDepth()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeScreenDepthName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeWorldNormal()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeWorldNormalName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeWorldNormal()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeWorldNormalName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeWorldPosition()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeWorldPositionName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeWorldPosition()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeWorldPositionName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeRadiance()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeRadianceName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeRadiance()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeRadianceName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeIrradiance()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeIrradianceName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeIrradiance()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeIrradianceName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeConfidence()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeConfidenceName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeConfidence()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeConfidenceName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeHitDistance()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeHitDistanceName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeHitDistance()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeHitDistanceName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeBentNormal()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeBentNormalName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeBentNormal()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeBentNormalName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeAdaptiveProbeHeader()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeAdaptiveProbeHeaderName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeAdaptiveProbeHeader()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeAdaptiveProbeHeaderName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeAdaptiveProbeIndices()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeAdaptiveProbeIndicesName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeAdaptiveProbeIndices()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeAdaptiveProbeIndicesName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeTraceRadiance()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceRadianceName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeTraceRadiance()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceRadianceName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeTraceHit()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceHitName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeTraceHit()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceHitName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeTemporalRadiance()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeTemporalRadianceName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeTemporalRadiance()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeTemporalRadianceName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeTemporalIrradiance()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeTemporalIrradianceName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeTemporalIrradiance()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeTemporalIrradianceName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeTemporalConfidence()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeTemporalConfidenceName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeTemporalConfidence()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeTemporalConfidenceName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeFilteredRadiance()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeFilteredRadianceName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeFilteredRadiance()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeFilteredRadianceName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeFilteredIrradiance()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeFilteredIrradianceName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeFilteredIrradiance()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeFilteredIrradianceName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeFilteredConfidence()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeFilteredConfidenceName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeFilteredConfidence()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeFilteredConfidenceName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeFixupRadiance()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeFixupRadianceName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeFixupRadiance()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeFixupRadianceName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeFixupIrradiance()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeFixupIrradianceName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeFixupIrradiance()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeFixupIrradianceName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeFixupConfidence()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeFixupConfidenceName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeFixupConfidence()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeFixupConfidenceName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeMipRadiance()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeMipRadianceName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeMipRadiance()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeMipRadianceName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeMipIrradiance()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeMipIrradianceName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeMipIrradiance()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeMipIrradianceName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeMipConfidence()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeMipConfidenceName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeMipConfidence()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeMipConfidenceName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeMip2Radiance()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeMip2RadianceName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeMip2Radiance()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeMip2RadianceName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeMip2Irradiance()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeMip2IrradianceName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeMip2Irradiance()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeMip2IrradianceName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeMip2Confidence()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeMip2ConfidenceName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeMip2Confidence()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeMip2ConfidenceName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeMip3Radiance()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeMip3RadianceName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeMip3Radiance()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeMip3RadianceName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeMip3Irradiance()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeMip3IrradianceName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeMip3Irradiance()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeMip3IrradianceName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeMip3Confidence()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeMip3ConfidenceName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeMip3Confidence()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeMip3ConfidenceName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeRadianceSHAmbient()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeRadianceSHAmbientName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeRadianceSHAmbient()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeRadianceSHAmbientName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeRadianceSHDirectional()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeRadianceSHDirectionalName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeRadianceSHDirectional()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeRadianceSHDirectionalName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeIrradianceOct()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeIrradianceOctName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeIrradianceOct()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeIrradianceOctName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeRadianceOct()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeRadianceOctName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeRadianceOct()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeRadianceOctName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeImportancePDF()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeImportancePDFName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeImportancePDF()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeImportancePDFName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeImportanceLightPDF()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeImportanceLightPDFName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeImportanceLightPDF()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeImportanceLightPDFName);
        }

        public BurtRenderTargetHandle ReadBurtGIScreenProbeImportanceRayInfo()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeImportanceRayInfoName);
        }

        public BurtRenderTargetHandle WriteBurtGIScreenProbeImportanceRayInfo()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIScreenProbeImportanceRayInfoName);
        }

        public BurtRenderTargetHandle ReadBurtGIRadianceCacheClipMapIndirection()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapIndirectionName);
        }

        public BurtRenderTargetHandle WriteBurtGIRadianceCacheClipMapIndirection()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapIndirectionName);
        }

        public BurtRenderTargetHandle ReadBurtGIRadianceCacheClipMapDepthProbeAtlas()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapDepthProbeAtlasName);
        }

        public BurtRenderTargetHandle WriteBurtGIRadianceCacheClipMapDepthProbeAtlas()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapDepthProbeAtlasName);
        }

        public BurtRenderTargetHandle ReadBurtGIRadianceCacheClipMapRadianceProbeAtlas()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapRadianceProbeAtlasName);
        }

        public BurtRenderTargetHandle WriteBurtGIRadianceCacheClipMapRadianceProbeAtlas()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapRadianceProbeAtlasName);
        }

        public BurtRenderTargetHandle ReadBurtGIRadianceCacheClipMapFinalRadianceAtlas()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapFinalRadianceAtlasName);
        }

        public BurtRenderTargetHandle WriteBurtGIRadianceCacheClipMapFinalRadianceAtlas()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapFinalRadianceAtlasName);
        }

        public BurtRenderTargetHandle ReadBurtGIRadianceCacheClipMapProbeOcclusionAtlas()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeOcclusionAtlasName);
        }

        public BurtRenderTargetHandle WriteBurtGIRadianceCacheClipMapProbeOcclusionAtlas()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeOcclusionAtlasName);
        }

        public BurtRenderTargetHandle ReadScreenSpaceSubsurfaceSource()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSourceName);
        }

        public BurtRenderTargetHandle WriteScreenSpaceSubsurfaceSource()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSourceName);
        }

        public BurtRenderTargetHandle ReadScreenSpaceSubsurfaceBaseColor()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBaseColorName);
        }

        public BurtRenderTargetHandle WriteScreenSpaceSubsurfaceBaseColor()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBaseColorName);
        }

        public BurtRenderTargetHandle ReadScreenSpaceSubsurfaceEmission()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceEmissionName);
        }

        public BurtRenderTargetHandle WriteScreenSpaceSubsurfaceEmission()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceEmissionName);
        }

        public BurtRenderTargetHandle ReadScreenSpaceSubsurfaceSetup()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSetupName);
        }

        public BurtRenderTargetHandle WriteScreenSpaceSubsurfaceSetup()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSetupName);
        }

        public BurtRenderTargetHandle ReadScreenSpaceSubsurfaceTemp()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceTempName);
        }

        public BurtRenderTargetHandle WriteScreenSpaceSubsurfaceTemp()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceTempName);
        }

        public BurtRenderTargetHandle ReadScreenSpaceSubsurfaceBlur()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBlurName);
        }

        public BurtRenderTargetHandle WriteScreenSpaceSubsurfaceBlur()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBlurName);
        }

        public BurtRenderTargetHandle ReadScreenSpaceSubsurfaceCombine()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceCombineName);
        }

        public BurtRenderTargetHandle WriteScreenSpaceSubsurfaceCombine()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceCombineName);
        }

        public BurtRenderTargetHandle ReadScreenSpaceSubsurfaceHistory()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceHistoryName);
        }

        public BurtRenderTargetHandle WriteScreenSpaceSubsurfaceHistory()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceHistoryName);
        }

        public BurtRenderTargetHandle ReadScreenSpaceSubsurfaceProfileIDAndType()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceProfileIDAndTypeName);
        }

        public BurtRenderTargetHandle WriteScreenSpaceSubsurfaceProfileIDAndType()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceProfileIDAndTypeName);
        }

        public BurtRenderTargetHandle ReadScreenSpaceSubsurfaceMask()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceMaskName);
        }

        public BurtRenderTargetHandle WriteScreenSpaceSubsurfaceMask()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceMaskName);
        }

        public BurtRenderTargetHandle ReadScreenSpaceSubsurfaceVelocity()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceVelocityName);
        }

        public BurtRenderTargetHandle WriteScreenSpaceSubsurfaceVelocity()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceVelocityName);
        }

        public BurtRenderTargetHandle ReadFurBlurProperty()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.FurBlurPropertyName);
        }

        public BurtRenderTargetHandle WriteFurBlurProperty()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.FurBlurPropertyName);
        }

        public BurtRenderTargetHandle ReadFurBlurPropertyTemp()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.FurBlurPropertyTempName);
        }

        public BurtRenderTargetHandle WriteFurBlurPropertyTemp()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.FurBlurPropertyTempName);
        }

        public BurtRenderTargetHandle ReadFurBlurColor()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.FurBlurColorName);
        }

        public BurtRenderTargetHandle WriteFurBlurColor()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.FurBlurColorName);
        }

        public BurtRenderTargetHandle ReadFurBlurTemporal()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.FurBlurTemporalName);
        }

        public BurtRenderTargetHandle WriteFurBlurTemporal()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.FurBlurTemporalName);
        }

        public BurtRenderTargetHandle ReadFurBlurVelocity()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.FurBlurVelocityName);
        }

        public BurtRenderTargetHandle WriteFurBlurVelocity()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.FurBlurVelocityName);
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

            if (BurtPerObjectShadowUtility.ShouldUsePerObjectShadow(Request, Asset))
            {
                ReadPerObjectShadowAtlas();
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

        public BurtRenderTargetHandle ReadPerObjectShadowAtlas()
        {
            return ReadRenderTarget(BurtRenderGraphResourceRegistry.PerObjectShadowAtlasName);
        }

        public BurtRenderTargetHandle WritePerObjectShadowAtlas()
        {
            return WriteRenderTarget(BurtRenderGraphResourceRegistry.PerObjectShadowAtlasName);
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
