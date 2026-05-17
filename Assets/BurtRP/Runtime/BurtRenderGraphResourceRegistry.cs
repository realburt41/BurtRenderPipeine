using System.Collections.Generic; // 引入泛型集合命名空间，用来使用 Dictionary 和 HashSet 保存资源表与外部导入标记。
using UnityEngine; // 引入 UnityEngine 命名空间，用来使用 Shader.PropertyToID 生成临时 RT 的整数 ID。
using UnityEngine.Rendering; // 引入 Unity 渲染命名空间，用来使用 RenderTargetIdentifier。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让资源注册表和其他 BurtRP 代码处在同一个模块里。
{
    public sealed class BurtRenderGraphResourceRegistry // 定义 RenderGraph 资源注册表，用来集中保存当前图可访问的渲染资源。
    {
        public const string CameraColorName = "CameraColor"; // 定义 BurtRP 中间相机颜色目标的统一资源名，后续所有场景绘制都先写到这个临时颜色 RT。

        public const string IntermediateCameraColorName = CameraColorName; // 给中间颜色 RT 提供更直观的别名，方便后续代码表达“先渲染到中间目标”的语义。

        public const string FinalCameraTargetName = "FinalCameraTarget"; // 定义最终相机输出目标的统一资源名，用来保存 request.TargetIdentifier 指向的 backbuffer 或 targetTexture。

        public const string CameraColorTextureShaderName = "_BurtCameraColorTexture"; // 定义中间颜色 RT 暴露给 shader 的全局纹理名称，FinalBlit 会通过它采样相机颜色。

        public static readonly int CameraColorTextureId = Shader.PropertyToID(CameraColorTextureShaderName); // 把中间颜色 RT 的 shader 名称转换成整数 ID，保证申请、绑定、释放都使用同一个临时 RT。

        public const string CameraDepthName = "CameraDepth"; // 定义相机深度目标的统一资源名，后续 DepthPrepass、透明排序和后处理会依赖它。

        public const string CameraDepthTextureShaderName = "_BurtCameraDepthTexture"; // 定义真实相机深度 RT 的 shader 名称，后续 shader 采样深度时会使用它。

        public static readonly int CameraDepthTextureId = Shader.PropertyToID(CameraDepthTextureShaderName); // 把 shader 名称转换成整数 ID，CommandBuffer 使用整数 ID 会更稳定也更高效。

        public const string PostProcessColorName = "PostProcessColor"; // 定义后处理中间颜色目标的统一资源名，No-op Copy 和后续效果链会通过它做 ping-pong。

        public const string PostProcessColorTextureShaderName = "_BurtPostProcessColorTexture"; // 定义后处理中间颜色 RT 暴露给 shader 的全局纹理名称。

        public static readonly int PostProcessColorTextureId = Shader.PropertyToID(PostProcessColorTextureShaderName); // 把后处理中间颜色名称转换成整数 ID，申请、绑定和释放都会复用它。

        public const string GBuffer0Name = "GBuffer0"; // 定义 Deferred 第一张 GBuffer 的统一资源名，第一版用于保存 baseColor 和 occlusion。

        public const string GBuffer0ShaderName = "_BurtGBuffer0"; // 定义 GBuffer0 暴露给 shader 的全局纹理名称，Deferred Lighting 会采样它。

        public static readonly int GBuffer0Id = Shader.PropertyToID(GBuffer0ShaderName); // 把 GBuffer0 shader 名称转换成整数 ID，后续申请、绑定和释放都会复用它。

        public const string GBuffer1Name = "GBuffer1"; // 定义 Deferred 第二张 GBuffer 的统一资源名，第一版用于保存法线、金属度和光滑度。

        public const string GBuffer1ShaderName = "_BurtGBuffer1"; // 定义 GBuffer1 暴露给 shader 的全局纹理名称，Deferred Lighting 会采样它。

        public static readonly int GBuffer1Id = Shader.PropertyToID(GBuffer1ShaderName); // 把 GBuffer1 shader 名称转换成整数 ID，后续申请、绑定和释放都会复用它。

        public const string GBuffer2Name = "GBuffer2"; // 定义 Deferred 第三张 GBuffer 的统一资源名，第一版用于保存 emission 和 reflectance。

        public const string GBuffer2ShaderName = "_BurtGBuffer2"; // 定义 GBuffer2 暴露给 shader 的全局纹理名称，Deferred Lighting 会采样它。

        public static readonly int GBuffer2Id = Shader.PropertyToID(GBuffer2ShaderName); // 把 GBuffer2 shader 名称转换成整数 ID，后续申请、绑定和释放都会复用它。

        public const string GBuffer3Name = "GBuffer3"; // 定义 Deferred 第四张 GBuffer 的统一资源名，第一版用于保存 Clear Coat 独立法线。

        public const string GBuffer3ShaderName = "_BurtGBuffer3"; // 定义 GBuffer3 暴露给 shader 的全局纹理名称，Deferred Lighting 会采样它。

        public static readonly int GBuffer3Id = Shader.PropertyToID(GBuffer3ShaderName); // 把 GBuffer3 shader 名称转换成整数 ID，后续申请、绑定和释放都会复用它。

        public const string HiZDepthName = "HiZDepth";

        public const string HiZDepthTextureShaderName = "_BurtHiZDepthTexture";

        public static readonly int HiZDepthTextureId = Shader.PropertyToID(HiZDepthTextureShaderName);

        public const string ScreenSpaceReflectionColorName = "ScreenSpaceReflectionColor";

        public const string ScreenSpaceReflectionColorTextureShaderName = "_BurtScreenSpaceReflectionColorTexture";

        public static readonly int ScreenSpaceReflectionColorTextureId = Shader.PropertyToID(ScreenSpaceReflectionColorTextureShaderName);

        public const string ScreenSpaceReflectionDenoisedColorName = "ScreenSpaceReflectionDenoisedColor";

        public const string ScreenSpaceReflectionDenoisedColorTextureShaderName = "_BurtScreenSpaceReflectionDenoisedColorTexture";

        public static readonly int ScreenSpaceReflectionDenoisedColorTextureId = Shader.PropertyToID(ScreenSpaceReflectionDenoisedColorTextureShaderName);

        public const string ScreenSpaceReflectionTemporalColorName = "ScreenSpaceReflectionTemporalColor";

        public const string ScreenSpaceReflectionTemporalColorTextureShaderName = "_BurtScreenSpaceReflectionTemporalColorTexture";

        public static readonly int ScreenSpaceReflectionTemporalColorTextureId = Shader.PropertyToID(ScreenSpaceReflectionTemporalColorTextureShaderName);

        public const string ScreenSpaceAmbientOcclusionRawName = "ScreenSpaceAmbientOcclusionRaw";

        public const string ScreenSpaceAmbientOcclusionRawTextureShaderName = "_BurtScreenSpaceAmbientOcclusionRawTexture";

        public static readonly int ScreenSpaceAmbientOcclusionRawTextureId = Shader.PropertyToID(ScreenSpaceAmbientOcclusionRawTextureShaderName);

        public const string ScreenSpaceAmbientOcclusionName = "ScreenSpaceAmbientOcclusion";

        public const string ScreenSpaceAmbientOcclusionTextureShaderName = "_BurtScreenSpaceAmbientOcclusionTexture";

        public static readonly int ScreenSpaceAmbientOcclusionTextureId = Shader.PropertyToID(ScreenSpaceAmbientOcclusionTextureShaderName);

        public const string MainLightShadowMapName = "MainLightShadowMap"; // 定义主光阴影图在 RenderGraph 里的统一资源名，后续阴影绘制和光照采样都通过它建立依赖。

        public const string MainLightShadowMapShaderName = "_BurtMainLightShadowMap"; // 定义主光阴影图暴露给 shader 的全局纹理名称，后续 Lit shader 会用这个名字采样阴影。

        public static readonly int MainLightShadowMapId = Shader.PropertyToID(MainLightShadowMapShaderName); // 把主光阴影图 shader 名称转换成整数 ID，让 CommandBuffer 申请、释放和绑定同一个临时 RT。

        public const string AdditionalLightShadowAtlasName = "AdditionalLightShadowAtlas";

        public const string AdditionalLightShadowAtlasShaderName = "_BurtAdditionalLightShadowAtlas";

        public static readonly int AdditionalLightShadowAtlasId = Shader.PropertyToID(AdditionalLightShadowAtlasShaderName);

        public const string LightingGlobalsName = "LightingGlobals"; // 定义灯光全局状态的逻辑资源名，用来让 Setup Lighting 和 Shading Pass 建立依赖。

        public const string ShadowGlobalsName = "ShadowGlobals"; // 定义阴影全局状态的逻辑资源名，用来描述 shadow matrix、shadow strength 等 shader 全局变量。

        public const string AdditionalLightBufferName = "AdditionalLightBuffer"; // Future structured buffer for multi-light data when tiled/cluster lighting replaces global arrays.

        public const string TileLightCountBufferName = "TileLightCountBuffer"; // Per-tile debug light count buffer used by the tiled-lighting skeleton.

        public const string TileLightListBufferName = "TileLightListBuffer"; // Future per-tile light index list buffer used by tiled lighting.

        public const string TileLightOffsetBufferName = "TileLightOffsetBuffer"; // Future per-tile offset/count buffer used by tiled lighting.

        public const string ClusterLightListBufferName = "ClusterLightListBuffer"; // Future per-cluster light index list buffer used by clustered lighting.

        private const string UnnamedRenderTargetName = "UnnamedRenderTarget"; // 定义空资源名的兜底名称，避免 Dictionary 接收 null 或空字符串。

        private const string UnnamedBufferName = "UnnamedBuffer"; // Fallback logical buffer name used when a declaration passes null or empty.

        private readonly Dictionary<string, BurtRenderTargetHandle> renderTargets = new Dictionary<string, BurtRenderTargetHandle>(); // 创建渲染目标字典，用资源名映射到渲染目标句柄。

        private readonly HashSet<string> externalRenderTargets = new HashSet<string>(); // 记录由相机或外部系统提供的资源，Read-before-Write 校验会把它们视为已有生产者。

        private readonly Dictionary<string, BurtRenderBufferHandle> buffers = new Dictionary<string, BurtRenderBufferHandle>(); // Logical buffer registry for future tiled/cluster resources.

        private readonly Dictionary<string, BurtRenderBufferDescriptor> bufferDescriptors = new Dictionary<string, BurtRenderBufferDescriptor>(); // Allocation descriptors for graph-owned GPU buffers.

        private readonly HashSet<string> externalBuffers = new HashSet<string>(); // Buffers imported from outside the graph are valid read sources.

        private readonly List<GraphicsBuffer> deferredBufferReleases = new List<GraphicsBuffer>(); // Buffers queued by release passes until ScriptableRenderContext.Submit has consumed draw commands.

        public IEnumerable<string> RenderTargetNames => renderTargets.Keys; // Exposes registered RT names for debug dumps without exposing the dictionary.

        public IEnumerable<string> ExternalRenderTargetNames => externalRenderTargets; // Exposes external RT names for debug dumps.

        public IEnumerable<string> BufferNames => buffers.Keys; // Exposes registered logical buffer names for debug dumps.

        public IEnumerable<string> ExternalBufferNames => externalBuffers; // Exposes external buffer names for debug dumps.

        public void Clear() // Clears graph resources before assembling the next RenderGraph request.
        {
            FlushDeferredBufferReleases(); // Complete releases queued by the previous submitted request before reusing the registry.

            ReleaseAllInternalBuffers(); // Dispose any graph-owned buffers that survived because execution ended early.

            renderTargets.Clear(); // Clear render targets registered by the previous request.

            externalRenderTargets.Clear(); // Clear external render target markers for the next request.

            buffers.Clear(); // Clear logical buffer registrations alongside render targets.

            bufferDescriptors.Clear(); // Clear GPU buffer allocation descriptors.

            externalBuffers.Clear(); // Clear imported buffer markers for the next request.
        }

        public BurtRenderTargetHandle RegisterRenderTarget( // 定义注册渲染目标的函数，外部通过它把 RenderTargetIdentifier 放进资源表。
            string name, // 接收资源逻辑名称，例如 CameraColor 或 CameraDepth。
            RenderTargetIdentifier identifier) // 接收 Unity 实际渲染目标标识。
        {
            return RegisterRenderTarget(name, identifier, false); // 默认注册为图内资源，需要有 Pass 写入后才算生产完成。
        }

        public BurtRenderTargetHandle RegisterRenderTarget( // 定义带外部导入标记的注册函数，供 FinalCameraTarget 等外部资源使用。
            string name, // 接收资源逻辑名称，例如 FinalCameraTarget。
            RenderTargetIdentifier identifier, // 接收 Unity 实际渲染目标标识。
            bool isExternal) // 标记这个资源是否由 RenderGraph 外部已经提供。
        {
            var safeName = NormalizeResourceName(name); // 统一处理空资源名，保证资源表 key 可用且 Debug 输出稳定。

            var handle = new BurtRenderTargetHandle(safeName, identifier); // 把资源名和 Unity 渲染目标标识包装成 BurtRenderTargetHandle。

            renderTargets[safeName] = handle; // 把句柄写入资源表，如果同名资源已存在就覆盖旧值。

            if (isExternal) // 外部资源不需要图内生产者，例如相机最终输出目标。
            {
                externalRenderTargets.Add(safeName); // 记录外部导入资源名，供 Read-before-Write 校验使用。
            }
            else
            {
                externalRenderTargets.Remove(safeName); // 图内资源被重新注册时清理外部标记，避免校验误判。
            }

            return handle; // 返回刚注册好的资源句柄，方便调用方立刻使用。
        }

        public BurtRenderTargetHandle GetRenderTarget(string name) // 定义根据名称读取渲染目标句柄的函数。
        {
            var safeName = NormalizeResourceName(name); // 统一处理空名称，避免后续字典查询不稳定。

            if (renderTargets.TryGetValue(safeName, out var handle)) // 尝试从资源表里找到指定名称的渲染目标。
            {
                return handle; // 找到时返回资源表里保存的有效句柄。
            }

            return BurtRenderTargetHandle.Invalid(safeName); // 找不到时返回带资源名的无效句柄，方便调试缺失资源。
        }

        public bool ContainsRenderTarget(string name) // 判断某个资源名是否已经注册到当前资源表。
        {
            return renderTargets.ContainsKey(NormalizeResourceName(name)); // 使用同一套名称归一化逻辑，避免空名判断和 GetRenderTarget 分叉。
        }

        public bool IsExternalRenderTarget(string name) // 判断某个资源是否来自 RenderGraph 外部。
        {
            return externalRenderTargets.Contains(NormalizeResourceName(name)); // 外部资源可被读取而不需要图内写入生产者。
        }

        public BurtRenderBufferHandle RegisterBuffer(string name) // Registers a logical buffer resource without allocating a GPU buffer yet.
        {
            return RegisterBuffer(name, default, false, null);
        }

        public BurtRenderBufferHandle RegisterBuffer(string name, bool isExternal) // Registers a logical buffer and optional external import marker.
        {
            return RegisterBuffer(name, default, isExternal, null);
        }

        public BurtRenderBufferHandle RegisterBuffer(string name, BurtRenderBufferDescriptor descriptor) // Registers a graph-owned GPU buffer descriptor.
        {
            return RegisterBuffer(name, descriptor, false, null);
        }

        public BurtRenderBufferHandle RegisterExternalBuffer(string name, GraphicsBuffer buffer) // Imports a GPU buffer owned by code outside this graph.
        {
            return RegisterBuffer(name, default, true, buffer);
        }

        private BurtRenderBufferHandle RegisterBuffer(
            string name,
            BurtRenderBufferDescriptor descriptor,
            bool isExternal,
            GraphicsBuffer externalBuffer)
        {
            var safeName = NormalizeBufferName(name);
            ReleaseInternalBufferIfNeeded(safeName);

            var handle = new BurtRenderBufferHandle(safeName, externalBuffer);
            buffers[safeName] = handle;

            if (descriptor.IsValid)
            {
                bufferDescriptors[safeName] = descriptor;
            }
            else
            {
                bufferDescriptors.Remove(safeName);
            }

            if (isExternal)
            {
                externalBuffers.Add(safeName);
            }
            else
            {
                externalBuffers.Remove(safeName);
            }

            return handle;
        }

        public BurtRenderBufferHandle GetBuffer(string name) // Reads a logical buffer handle from the registry.
        {
            var safeName = NormalizeBufferName(name);

            if (buffers.TryGetValue(safeName, out var handle))
            {
                return handle;
            }

            return BurtRenderBufferHandle.Invalid(safeName);
        }

        public bool TryGetBufferDescriptor(string name, out BurtRenderBufferDescriptor descriptor) // Reads a registered GPU buffer descriptor.
        {
            return bufferDescriptors.TryGetValue(NormalizeBufferName(name), out descriptor);
        }

        public bool HasValidBufferDescriptor(string name) // Checks whether a graph-owned buffer can be allocated.
        {
            return bufferDescriptors.TryGetValue(NormalizeBufferName(name), out var descriptor) && descriptor.IsValid;
        }

        public bool IsBufferAllocated(string name) // Checks whether the registry currently holds a live GPU buffer object.
        {
            return buffers.TryGetValue(NormalizeBufferName(name), out var handle) && handle.HasBuffer;
        }

        public BurtRenderBufferHandle AllocateBuffer(string name) // Allocates or reuses a graph-owned GPU buffer from its descriptor.
        {
            var safeName = NormalizeBufferName(name);

            if (!bufferDescriptors.TryGetValue(safeName, out var descriptor) || !descriptor.IsValid)
            {
                return GetBuffer(safeName);
            }

            if (buffers.TryGetValue(safeName, out var currentHandle) && currentHandle.HasBuffer && IsBufferCompatible(currentHandle.Buffer, descriptor))
            {
                return currentHandle;
            }

            ReleaseInternalBufferIfNeeded(safeName);

            var buffer = new GraphicsBuffer(descriptor.Target, descriptor.Count, descriptor.Stride)
            {
                name = string.IsNullOrEmpty(descriptor.DebugName) ? safeName : descriptor.DebugName
            };

            var handle = new BurtRenderBufferHandle(safeName, buffer);
            buffers[safeName] = handle;
            externalBuffers.Remove(safeName);

            return handle;
        }

        public void ReleaseBuffer(string name) // Releases a graph-owned GPU buffer while keeping the logical registration visible for debug output.
        {
            var safeName = NormalizeBufferName(name);
            if (externalBuffers.Contains(safeName))
            {
                return;
            }

            QueueInternalBufferReleaseIfNeeded(safeName);

            if (buffers.ContainsKey(safeName))
            {
                buffers[safeName] = new BurtRenderBufferHandle(safeName);
            }
        }

        public void FlushDeferredBufferReleases()
        {
            for (var bufferIndex = 0; bufferIndex < deferredBufferReleases.Count; bufferIndex++)
            {
                ReleaseBufferObject(deferredBufferReleases[bufferIndex]);
            }

            deferredBufferReleases.Clear();
        }

        public bool ContainsBuffer(string name) // Checks whether a logical buffer is registered in the current graph.
        {
            return buffers.ContainsKey(NormalizeBufferName(name));
        }

        public bool IsExternalBuffer(string name) // Checks whether a logical buffer is imported from outside the graph.
        {
            return externalBuffers.Contains(NormalizeBufferName(name));
        }

        public BurtRenderTargetHandle RegisterCameraColor(RenderTargetIdentifier identifier) // 定义注册 CameraColor 的快捷函数。
        {
            return RegisterRenderTarget(CameraColorName, identifier); // 使用统一名称把相机颜色目标注册进资源表。
        }

        public BurtRenderTargetHandle GetCameraColor() // 定义读取 CameraColor 的快捷函数。
        {
            return GetRenderTarget(CameraColorName); // 使用统一名称从资源表读取相机颜色目标。
        }

        public BurtRenderTargetHandle RegisterCameraColorTexture() // 定义注册 BurtRP 自己创建的 CameraColor 临时颜色 RT 的快捷函数。
        {
            return RegisterCameraColor(new RenderTargetIdentifier(CameraColorTextureId)); // 使用统一 ID 创建 RenderTargetIdentifier，并把它注册为 CameraColor 中间目标。
        }

        public BurtRenderTargetHandle RegisterFinalCameraTarget(RenderTargetIdentifier identifier) // 定义注册最终相机输出目标的快捷函数。
        {
            return RegisterRenderTarget(FinalCameraTargetName, identifier, true); // 最终输出来自相机/backbuffer，校验时视为外部已存在资源。
        }

        public BurtRenderTargetHandle GetFinalCameraTarget() // 定义读取最终相机输出目标的快捷函数。
        {
            return GetRenderTarget(FinalCameraTargetName); // 使用统一名称从资源表读取 backbuffer 或相机 targetTexture。
        }

        public BurtRenderTargetHandle RegisterCameraDepthTexture() // 定义注册 BurtRP 自己创建的 CameraDepth 临时 RT 的快捷函数。
        {
            return RegisterCameraDepth(new RenderTargetIdentifier(CameraDepthTextureId)); // 使用统一 ID 创建 RenderTargetIdentifier，并把它注册为 CameraDepth。
        }

        public BurtRenderTargetHandle RegisterCameraDepth(RenderTargetIdentifier identifier) // 定义注册 CameraDepth 的快捷函数。
        {
            return RegisterRenderTarget(CameraDepthName, identifier); // 使用统一名称把相机深度目标注册进资源表。
        }

        public BurtRenderTargetHandle GetCameraDepth() // 定义读取 CameraDepth 的快捷函数。
        {
            return GetRenderTarget(CameraDepthName); // 使用统一名称从资源表读取相机深度目标。
        }

        public BurtRenderTargetHandle RegisterPostProcessColorTexture() // 定义注册 BurtRP 后处理中间颜色临时 RT 的快捷函数。
        {
            return RegisterPostProcessColor(new RenderTargetIdentifier(PostProcessColorTextureId)); // 使用统一 ID 创建 RenderTargetIdentifier，并把它注册为 PostProcessColor。
        }

        public BurtRenderTargetHandle RegisterPostProcessColor(RenderTargetIdentifier identifier) // 定义注册 PostProcessColor 的快捷函数。
        {
            return RegisterRenderTarget(PostProcessColorName, identifier); // 使用统一名称把后处理中间颜色目标注册进资源表。
        }

        public BurtRenderTargetHandle GetPostProcessColor() // 定义读取 PostProcessColor 的快捷函数。
        {
            return GetRenderTarget(PostProcessColorName); // 使用统一名称从资源表读取后处理中间颜色目标。
        }

        public BurtRenderTargetHandle RegisterGBuffer0Texture() // 定义注册 Deferred GBuffer0 临时 RT 的快捷函数。
        {
            return RegisterGBuffer0(new RenderTargetIdentifier(GBuffer0Id)); // 使用统一 ID 创建 RenderTargetIdentifier，并把它注册为 GBuffer0。
        }

        public BurtRenderTargetHandle RegisterGBuffer0(RenderTargetIdentifier identifier) // 定义注册 GBuffer0 的快捷函数。
        {
            return RegisterRenderTarget(GBuffer0Name, identifier); // 使用统一名称把 GBuffer0 注册进资源表。
        }

        public BurtRenderTargetHandle GetGBuffer0() // 定义读取 GBuffer0 的快捷函数。
        {
            return GetRenderTarget(GBuffer0Name); // 使用统一名称从资源表读取 GBuffer0 目标。
        }

        public BurtRenderTargetHandle RegisterGBuffer1Texture() // 定义注册 Deferred GBuffer1 临时 RT 的快捷函数。
        {
            return RegisterGBuffer1(new RenderTargetIdentifier(GBuffer1Id)); // 使用统一 ID 创建 RenderTargetIdentifier，并把它注册为 GBuffer1。
        }

        public BurtRenderTargetHandle RegisterGBuffer1(RenderTargetIdentifier identifier) // 定义注册 GBuffer1 的快捷函数。
        {
            return RegisterRenderTarget(GBuffer1Name, identifier); // 使用统一名称把 GBuffer1 注册进资源表。
        }

        public BurtRenderTargetHandle GetGBuffer1() // 定义读取 GBuffer1 的快捷函数。
        {
            return GetRenderTarget(GBuffer1Name); // 使用统一名称从资源表读取 GBuffer1 目标。
        }

        public BurtRenderTargetHandle RegisterGBuffer2Texture() // 定义注册 Deferred GBuffer2 临时 RT 的快捷函数。
        {
            return RegisterGBuffer2(new RenderTargetIdentifier(GBuffer2Id)); // 使用统一 ID 创建 RenderTargetIdentifier，并把它注册为 GBuffer2。
        }

        public BurtRenderTargetHandle RegisterGBuffer2(RenderTargetIdentifier identifier) // 定义注册 GBuffer2 的快捷函数。
        {
            return RegisterRenderTarget(GBuffer2Name, identifier); // 使用统一名称把 GBuffer2 注册进资源表。
        }

        public BurtRenderTargetHandle GetGBuffer2() // 定义读取 GBuffer2 的快捷函数。
        {
            return GetRenderTarget(GBuffer2Name); // 使用统一名称从资源表读取 GBuffer2 目标。
        }

        public BurtRenderTargetHandle RegisterGBuffer3Texture() // 定义注册 Deferred GBuffer3 临时 RT 的快捷函数。
        {
            return RegisterGBuffer3(new RenderTargetIdentifier(GBuffer3Id)); // 使用统一 ID 创建 RenderTargetIdentifier，并把它注册为 GBuffer3。
        }

        public BurtRenderTargetHandle RegisterGBuffer3(RenderTargetIdentifier identifier) // 定义注册 GBuffer3 的快捷函数。
        {
            return RegisterRenderTarget(GBuffer3Name, identifier); // 使用统一名称把 GBuffer3 注册进资源表。
        }

        public BurtRenderTargetHandle GetGBuffer3() // 定义读取 GBuffer3 的快捷函数。
        {
            return GetRenderTarget(GBuffer3Name); // 使用统一名称从资源表读取 GBuffer3 目标。
        }

        public BurtRenderTargetHandle RegisterHiZDepthTexture()
        {
            return RegisterHiZDepth(new RenderTargetIdentifier(HiZDepthTextureId));
        }

        public BurtRenderTargetHandle RegisterHiZDepth(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(HiZDepthName, identifier);
        }

        public BurtRenderTargetHandle GetHiZDepth()
        {
            return GetRenderTarget(HiZDepthName);
        }

        public BurtRenderTargetHandle RegisterScreenSpaceReflectionColorTexture()
        {
            return RegisterScreenSpaceReflectionColor(new RenderTargetIdentifier(ScreenSpaceReflectionColorTextureId));
        }

        public BurtRenderTargetHandle RegisterScreenSpaceReflectionColor(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(ScreenSpaceReflectionColorName, identifier);
        }

        public BurtRenderTargetHandle GetScreenSpaceReflectionColor()
        {
            return GetRenderTarget(ScreenSpaceReflectionColorName);
        }

        public BurtRenderTargetHandle RegisterScreenSpaceReflectionDenoisedColorTexture()
        {
            return RegisterScreenSpaceReflectionDenoisedColor(new RenderTargetIdentifier(ScreenSpaceReflectionDenoisedColorTextureId));
        }

        public BurtRenderTargetHandle RegisterScreenSpaceReflectionDenoisedColor(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(ScreenSpaceReflectionDenoisedColorName, identifier);
        }

        public BurtRenderTargetHandle GetScreenSpaceReflectionDenoisedColor()
        {
            return GetRenderTarget(ScreenSpaceReflectionDenoisedColorName);
        }

        public BurtRenderTargetHandle RegisterScreenSpaceReflectionTemporalColorTexture()
        {
            return RegisterScreenSpaceReflectionTemporalColor(new RenderTargetIdentifier(ScreenSpaceReflectionTemporalColorTextureId));
        }

        public BurtRenderTargetHandle RegisterScreenSpaceReflectionTemporalColor(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(ScreenSpaceReflectionTemporalColorName, identifier);
        }

        public BurtRenderTargetHandle GetScreenSpaceReflectionTemporalColor()
        {
            return GetRenderTarget(ScreenSpaceReflectionTemporalColorName);
        }

        public BurtRenderTargetHandle RegisterScreenSpaceAmbientOcclusionRawTexture()
        {
            return RegisterScreenSpaceAmbientOcclusionRaw(new RenderTargetIdentifier(ScreenSpaceAmbientOcclusionRawTextureId));
        }

        public BurtRenderTargetHandle RegisterScreenSpaceAmbientOcclusionRaw(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(ScreenSpaceAmbientOcclusionRawName, identifier);
        }

        public BurtRenderTargetHandle GetScreenSpaceAmbientOcclusionRaw()
        {
            return GetRenderTarget(ScreenSpaceAmbientOcclusionRawName);
        }

        public BurtRenderTargetHandle RegisterScreenSpaceAmbientOcclusionTexture()
        {
            return RegisterScreenSpaceAmbientOcclusion(new RenderTargetIdentifier(ScreenSpaceAmbientOcclusionTextureId));
        }

        public BurtRenderTargetHandle RegisterScreenSpaceAmbientOcclusion(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(ScreenSpaceAmbientOcclusionName, identifier);
        }

        public BurtRenderTargetHandle GetScreenSpaceAmbientOcclusion()
        {
            return GetRenderTarget(ScreenSpaceAmbientOcclusionName);
        }

        public BurtRenderTargetHandle RegisterMainLightShadowMapTexture() // 定义注册 BurtRP 主光阴影图临时 RT 的快捷函数。
        {
            return RegisterMainLightShadowMap(new RenderTargetIdentifier(MainLightShadowMapId)); // 使用统一 ID 创建 RenderTargetIdentifier，并把它注册为 MainLightShadowMap。
        }

        public BurtRenderTargetHandle RegisterMainLightShadowMap(RenderTargetIdentifier identifier) // 定义注册 MainLightShadowMap 的快捷函数。
        {
            return RegisterRenderTarget(MainLightShadowMapName, identifier); // 使用统一名称把主光阴影图目标注册进资源表。
        }

        public BurtRenderTargetHandle GetMainLightShadowMap() // 定义读取 MainLightShadowMap 的快捷函数。
        {
            return GetRenderTarget(MainLightShadowMapName); // 使用统一名称从资源表读取主光阴影图目标。
        }

        public BurtRenderTargetHandle RegisterAdditionalLightShadowAtlasTexture()
        {
            return RegisterAdditionalLightShadowAtlas(new RenderTargetIdentifier(AdditionalLightShadowAtlasId));
        }

        public BurtRenderTargetHandle RegisterAdditionalLightShadowAtlas(RenderTargetIdentifier identifier)
        {
            return RegisterRenderTarget(AdditionalLightShadowAtlasName, identifier);
        }

        public BurtRenderTargetHandle GetAdditionalLightShadowAtlas()
        {
            return GetRenderTarget(AdditionalLightShadowAtlasName);
        }

        private void ReleaseAllInternalBuffers() // Releases graph-owned buffers before the registry is reset.
        {
            foreach (var pair in buffers)
            {
                if (externalBuffers.Contains(pair.Key))
                {
                    continue;
                }

                ReleaseBufferObject(pair.Value.Buffer);
            }
        }

        private void ReleaseInternalBufferIfNeeded(string safeName) // Releases one graph-owned buffer if it is currently allocated.
        {
            if (externalBuffers.Contains(safeName))
            {
                return;
            }

            if (buffers.TryGetValue(safeName, out var handle))
            {
                ReleaseBufferObject(handle.Buffer);
            }
        }

        private void QueueInternalBufferReleaseIfNeeded(string safeName) // Defers release-pass buffers until after the context submit.
        {
            if (externalBuffers.Contains(safeName))
            {
                return;
            }

            if (buffers.TryGetValue(safeName, out var handle))
            {
                QueueBufferReleaseObject(handle.Buffer);
            }
        }

        private void QueueBufferReleaseObject(GraphicsBuffer buffer)
        {
            if (buffer == null)
            {
                return;
            }

            deferredBufferReleases.Add(buffer);
        }

        private static void ReleaseBufferObject(GraphicsBuffer buffer) // Keeps GraphicsBuffer disposal guarded and centralized.
        {
            if (buffer == null)
            {
                return;
            }

            buffer.Release();
        }

        private static bool IsBufferCompatible(GraphicsBuffer buffer, BurtRenderBufferDescriptor descriptor) // Avoids reallocating when a reused buffer still matches the descriptor.
        {
            return buffer != null && buffer.count == descriptor.Count && buffer.stride == descriptor.Stride;
        }

        private static string NormalizeResourceName(string name) // 归一化资源名，避免 null 或空字符串破坏资源表和依赖校验。
        {
            return string.IsNullOrEmpty(name) ? UnnamedRenderTargetName : name; // 空名统一映射到兜底名称，Debug 中仍会看到异常资源名。
        }

        private static string NormalizeBufferName(string name) // Normalizes logical buffer names independently from render target names.
        {
            return string.IsNullOrEmpty(name) ? UnnamedBufferName : name;
        }
    }
}
