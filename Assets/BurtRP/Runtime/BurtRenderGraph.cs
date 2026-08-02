using System; // 引入基础命名空间，用来捕获 Configure 阶段异常并写入诊断信息。
using System.Collections.Generic; // Uses List for passes, resource declarations, and validation messages.
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让这个类和其他 BurtRP 代码处在同一个模块里。
{
    public sealed class BurtRenderGraph // 定义 BurtRP 的最小渲染图类，当前阶段负责保存 Pass、资源表和资源读写声明。
    {
        private static readonly ProfilerMarker ConfigureGraphMarker = new ProfilerMarker("BRP.RenderGraph.Configure");
        private static readonly ProfilerMarker ExecuteGraphMarker = new ProfilerMarker("BRP.RenderGraph.Execute");
        private readonly List<BurtRenderPass> passes = new List<BurtRenderPass>(); // 创建一个可复用的 Pass 列表，避免每帧重复分配 List。

        private readonly List<BurtRenderPassResourceUsage> resourceUsages = new List<BurtRenderPassResourceUsage>(); // 创建一个可复用的资源使用记录列表，用来保存每个 Pass 的读写声明。

        private readonly List<string> validationMessages = new List<string>(); // 保存当前图级别的轻量校验消息，只用于 Debug dump，不改变实际渲染顺序。

        private readonly BurtRenderGraphResourceRegistry resources = new BurtRenderGraphResourceRegistry(); // 创建一个可复用的资源注册表，用来保存当前图里的渲染目标资源。

        private BurtRenderGraphProfilingMode profilingMode = BurtRenderGraphProfilingMode.CameraAndStage;
        private BurtRenderGraphCompilationMode compilationMode = BurtRenderGraphCompilationMode.Lightweight;
        private readonly BurtRenderGraphCompiler compiler = new BurtRenderGraphCompiler();
        private BurtRenderGraphCompileResult compileResult;
        private bool isExecutingEmergencyCleanup;

    internal enum BurtRenderGraphCompilationMode
    {
        Lightweight = 0,
        Culling = 1,
        Full = 2,
    }

    public sealed class BurtRenderGraphCompileResult
    {
        public BurtRenderGraphCompileResult(
            int passCount,
            bool[] executePasses,
            List<int>[] dependencies,
            List<BurtRenderGraphResourceLifetime> resourceLifetimes,
            int dependencyCount,
            int culledPassCount)
        {
            PassCount = passCount;
            ExecutePasses = executePasses;
            Dependencies = dependencies;
            ResourceLifetimes = resourceLifetimes;
            DependencyCount = dependencyCount;
            CulledPassCount = culledPassCount;
        }

        public int PassCount { get; }
        public bool[] ExecutePasses { get; }
        public List<int>[] Dependencies { get; }
        public List<BurtRenderGraphResourceLifetime> ResourceLifetimes { get; }
        public int DependencyCount { get; }
        public int CulledPassCount { get; }

        public bool ShouldExecute(int passIndex)
        {
            return passIndex < 0 || passIndex >= PassCount || ExecutePasses[passIndex];
        }
    }

    public sealed class BurtRenderGraphResourceLifetime
    {
        public BurtRenderGraphResourceLifetime(string resourceKey, int firstPass, int lastPass, int aliasSlot)
        {
            ResourceKey = resourceKey;
            FirstPass = firstPass;
            LastPass = lastPass;
            AliasSlot = aliasSlot;
        }

        public string ResourceKey { get; }
        public int FirstPass { get; }
        public int LastPass { get; }
        public int AliasSlot { get; }
    }

    public sealed class BurtRenderGraphCompiler
    {
        private bool[] executePasses = Array.Empty<bool>();
        private List<int>[] dependencies = Array.Empty<List<int>>();
        private readonly Dictionary<string, int> lastWriters = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<int>> outstandingReaders = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Vector2Int> lifetimeRanges = new Dictionary<string, Vector2Int>(StringComparer.Ordinal);
        private readonly HashSet<string> requiredResources = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> reads = new List<string>();
        private readonly List<string> writes = new List<string>();

        public BurtRenderGraphCompileResult Compile(
            IReadOnlyList<BurtRenderPass> passes,
            IReadOnlyList<BurtRenderPassResourceUsage> usages,
            BurtRenderGraphResourceRegistry resources)
        {
            var passCount = passes != null ? passes.Count : 0;
            EnsurePassCapacity(passCount);
            lastWriters.Clear();
            lifetimeRanges.Clear();
            foreach (var readerList in outstandingReaders.Values)
            {
                readerList.Clear();
            }

            var dependencyCount = 0;

            for (var passIndex = 0; passIndex < passCount; passIndex++)
            {
                dependencies[passIndex].Clear();
                var usage = passIndex < usages.Count ? usages[passIndex] : null;
                if (usage == null)
                {
                    continue;
                }

                CollectReadKeys(usage, reads);
                CollectWriteKeys(usage, writes);
                for (var readIndex = 0; readIndex < reads.Count; readIndex++)
                {
                    var key = reads[readIndex];
                    TouchLifetime(lifetimeRanges, key, passIndex);
                    if (lastWriters.TryGetValue(key, out var writer))
                    {
                        dependencyCount += AddDependency(dependencies[passIndex], writer);
                    }

                    if (!outstandingReaders.TryGetValue(key, out var readers))
                    {
                        readers = new List<int>();
                        outstandingReaders.Add(key, readers);
                    }

                    if (!readers.Contains(passIndex))
                    {
                        readers.Add(passIndex);
                    }
                }

                for (var writeIndex = 0; writeIndex < writes.Count; writeIndex++)
                {
                    var key = writes[writeIndex];
                    TouchLifetime(lifetimeRanges, key, passIndex);
                    if (lastWriters.TryGetValue(key, out var writer))
                    {
                        dependencyCount += AddDependency(dependencies[passIndex], writer);
                    }

                    if (outstandingReaders.TryGetValue(key, out var readers))
                    {
                        for (var readerIndex = 0; readerIndex < readers.Count; readerIndex++)
                        {
                            if (readers[readerIndex] != passIndex)
                            {
                                dependencyCount += AddDependency(dependencies[passIndex], readers[readerIndex]);
                            }
                        }

                        readers.Clear();
                    }

                    lastWriters[key] = passIndex;
                }
            }

            CompileCullingMask(passCount, usages, resources);
            var culledPassCount = 0;
            for (var passIndex = 0; passIndex < passCount; passIndex++)
            {
                if (!executePasses[passIndex])
                {
                    culledPassCount++;
                }
            }

            var lifetimes = BuildLifetimes(lifetimeRanges);
            return new BurtRenderGraphCompileResult(passCount, executePasses, dependencies, lifetimes, dependencyCount, culledPassCount);
        }

        private void CompileCullingMask(
            int passCount,
            IReadOnlyList<BurtRenderPassResourceUsage> usages,
            BurtRenderGraphResourceRegistry resources)
        {
            requiredResources.Clear();
            for (var passIndex = passCount - 1; passIndex >= 0; passIndex--)
            {
                var usage = passIndex < usages.Count ? usages[passIndex] : null;
                if (usage == null)
                {
                    executePasses[passIndex] = true;
                    continue;
                }

                CollectWriteKeys(usage, writes);
                var isRequired = usage.HasSideEffects || !usage.AllowCulling || HasTerminalWrite(usage, resources);
                for (var writeIndex = 0; !isRequired && writeIndex < writes.Count; writeIndex++)
                {
                    isRequired = requiredResources.Contains(writes[writeIndex]);
                }

                executePasses[passIndex] = isRequired;
                if (!isRequired)
                {
                    continue;
                }

                for (var writeIndex = 0; writeIndex < writes.Count; writeIndex++)
                {
                    requiredResources.Remove(writes[writeIndex]);
                }

                if (usage.PassKind == BurtRenderPassKind.Release)
                {
                    continue;
                }

                CollectReadKeys(usage, reads);
                for (var readIndex = 0; readIndex < reads.Count; readIndex++)
                {
                    requiredResources.Add(reads[readIndex]);
                }
            }

        }

        private void EnsurePassCapacity(int passCount)
        {
            if (executePasses.Length < passCount)
            {
                var capacity = Math.Max(passCount, Math.Max(16, executePasses.Length * 2));
                Array.Resize(ref executePasses, capacity);
                var previousLength = dependencies.Length;
                Array.Resize(ref dependencies, capacity);
                for (var passIndex = previousLength; passIndex < capacity; passIndex++)
                {
                    dependencies[passIndex] = new List<int>(4);
                }
            }
        }

        private static bool HasTerminalWrite(BurtRenderPassResourceUsage usage, BurtRenderGraphResourceRegistry resources)
        {
            if (usage.AllowUnconsumedWriteResources.Count > 0)
            {
                return true;
            }

            for (var index = 0; index < usage.WriteRenderTargets.Count; index++)
            {
                if (resources != null && resources.IsExternalRenderTarget(usage.WriteRenderTargets[index].Name))
                {
                    return true;
                }
            }

            for (var index = 0; index < usage.WriteBuffers.Count; index++)
            {
                if (resources != null && resources.IsExternalBuffer(usage.WriteBuffers[index].Name))
                {
                    return true;
                }
            }

            return false;
        }

        private static void CollectReadKeys(BurtRenderPassResourceUsage usage, List<string> keys)
        {
            keys.Clear();
            for (var index = 0; index < usage.ReadRenderTargets.Count; index++)
            {
                AddUnique(keys, CreateKey("RT", usage.ReadRenderTargets[index].Name));
            }

            for (var index = 0; index < usage.ReadBuffers.Count; index++)
            {
                AddUnique(keys, CreateKey("BUF", usage.ReadBuffers[index].Name));
            }

            for (var index = 0; index < usage.ReadGlobalResources.Count; index++)
            {
                AddUnique(keys, CreateKey("GLOBAL", usage.ReadGlobalResources[index]));
            }

        }

        private static void CollectWriteKeys(BurtRenderPassResourceUsage usage, List<string> keys)
        {
            keys.Clear();
            for (var index = 0; index < usage.WriteRenderTargets.Count; index++)
            {
                AddUnique(keys, CreateKey("RT", usage.WriteRenderTargets[index].Name));
            }

            for (var index = 0; index < usage.WriteBuffers.Count; index++)
            {
                AddUnique(keys, CreateKey("BUF", usage.WriteBuffers[index].Name));
            }

            for (var index = 0; index < usage.WriteGlobalResources.Count; index++)
            {
                AddUnique(keys, CreateKey("GLOBAL", usage.WriteGlobalResources[index]));
            }

        }

        private static string CreateKey(string type, string name)
        {
            return type + ":" + (string.IsNullOrEmpty(name) ? "<unnamed>" : name);
        }

        private static void AddUnique(List<string> keys, string key)
        {
            if (!keys.Contains(key))
            {
                keys.Add(key);
            }
        }

        private static int AddDependency(List<int> dependencies, int dependency)
        {
            if (dependency < 0 || dependencies.Contains(dependency))
            {
                return 0;
            }

            dependencies.Add(dependency);
            return 1;
        }

        private static void TouchLifetime(Dictionary<string, Vector2Int> ranges, string key, int passIndex)
        {
            if (ranges.TryGetValue(key, out var range))
            {
                ranges[key] = new Vector2Int(Math.Min(range.x, passIndex), Math.Max(range.y, passIndex));
            }
            else
            {
                ranges.Add(key, new Vector2Int(passIndex, passIndex));
            }
        }

        private static List<BurtRenderGraphResourceLifetime> BuildLifetimes(Dictionary<string, Vector2Int> ranges)
        {
            var sorted = new List<KeyValuePair<string, Vector2Int>>(ranges);
            sorted.Sort((left, right) =>
            {
                var firstCompare = left.Value.x.CompareTo(right.Value.x);
                return firstCompare != 0 ? firstCompare : string.CompareOrdinal(left.Key, right.Key);
            });

            var slotEndPasses = new List<int>();
            var lifetimes = new List<BurtRenderGraphResourceLifetime>(sorted.Count);
            for (var index = 0; index < sorted.Count; index++)
            {
                var item = sorted[index];
                var aliasSlot = -1;
                if (item.Key.StartsWith("BUF:", StringComparison.Ordinal))
                {
                    for (var slot = 0; slot < slotEndPasses.Count; slot++)
                    {
                        if (slotEndPasses[slot] < item.Value.x)
                        {
                            aliasSlot = slot;
                            slotEndPasses[slot] = item.Value.y;
                            break;
                        }
                    }

                    if (aliasSlot < 0)
                    {
                        aliasSlot = slotEndPasses.Count;
                        slotEndPasses.Add(item.Value.y);
                    }
                }

                lifetimes.Add(new BurtRenderGraphResourceLifetime(item.Key, item.Value.x, item.Value.y, aliasSlot));
            }

            return lifetimes;
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const int MaxCachedProfilingSamplerCount = 1024;
        private readonly Dictionary<string, ProfilingSampler> profilingSamplers = new Dictionary<string, ProfilingSampler>(StringComparer.Ordinal);
        private readonly List<string> profilingAssemblyScopeStack = new List<string>();
        private readonly List<BurtRenderGraphProfilingEvent> profilingEvents = new List<BurtRenderGraphProfilingEvent>();
        private readonly List<BurtRenderGraphProfilingEvent> activeProfilingScopes = new List<BurtRenderGraphProfilingEvent>();
        private readonly ProfilingSampler overflowProfilingSampler = new ProfilingSampler("BRP.Profiling/Overflow");
#endif

        public int PassCount => passes.Count; // 暴露当前图里有多少个 Pass，方便后面调试或判断图是否为空。

        public BurtRenderGraphResourceRegistry Resources => resources; // 暴露当前 RenderGraph 的资源注册表，让 Context 和 Assembler 可以读取资源。

        public IReadOnlyList<BurtRenderPassResourceUsage> ResourceUsages => resourceUsages; // 暴露只读资源使用记录，方便后面调试或做依赖分析。

        public IReadOnlyList<string> ValidationMessages => validationMessages; // 暴露只读校验消息，供调试工具集中输出 RenderGraph 问题。
        public bool RequiresImmediateSubmit => resources.HasPendingBufferReleases;

        public void Clear() // 定义清空函数，每次组装新 request 前都要调用。
        {
            passes.Clear(); // 清空上一轮 request 组装出来的 Pass，避免 Pass 残留到下一次渲染。

            resourceUsages.Clear(); // 清空上一轮 Pass 的资源读写声明，避免调试数据残留。

            validationMessages.Clear(); // 清空上一轮图校验消息，避免不同相机或 request 互相污染。

            resources.Clear(); // 清空上一轮 request 注册的资源，避免 CameraColor 和 CameraDepth 等资源残留到下一次渲染。
            compileResult = null;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            profilingAssemblyScopeStack.Clear();
            profilingEvents.Clear();
            activeProfilingScopes.Clear();
#endif
        }

        public void SetProfilingMode(BurtRenderGraphProfilingMode mode)
        {
            profilingMode = mode;
        }

        internal void SetCompilationMode(BurtRenderGraphCompilationMode mode)
        {
            compilationMode = mode;
        }

        public void BeginProfilingScope(string name)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (profilingMode == BurtRenderGraphProfilingMode.Off)
            {
                return;
            }

            var safeName = NormalizeProfilingName(name, "BRP.Stage/Unnamed");
            profilingAssemblyScopeStack.Add(safeName);
            profilingEvents.Add(new BurtRenderGraphProfilingEvent(
                passes.Count,
                safeName,
                GetOrCreateProfilingSampler(safeName),
                true));
#endif
        }

        public void EndProfilingScope(string name)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (profilingMode == BurtRenderGraphProfilingMode.Off)
            {
                return;
            }

            var safeName = NormalizeProfilingName(name, "BRP.Stage/Unnamed");
            if (profilingAssemblyScopeStack.Count == 0)
            {
                AddValidationMessage("Profiling scope 结束时没有对应 Begin: " + safeName);
                return;
            }

            var lastIndex = profilingAssemblyScopeStack.Count - 1;
            var openedName = profilingAssemblyScopeStack[lastIndex];
            profilingAssemblyScopeStack.RemoveAt(lastIndex);
            if (!string.Equals(openedName, safeName, StringComparison.Ordinal))
            {
                AddValidationMessage("Profiling scope 顺序不匹配: expected " + openedName + ", actual " + safeName);
                safeName = openedName;
            }

            profilingEvents.Add(new BurtRenderGraphProfilingEvent(
                passes.Count,
                safeName,
                GetOrCreateProfilingSampler(safeName),
                false));
#endif
        }

        public void ImportRequestResources(BurtRenderRequest request, BurtRenderPipelineAsset asset) // 定义从 request 导入基础资源的函数，并允许资源注册使用管线资产配置。
        {
            if (request == null) // 如果 request 为空，说明没有合法渲染任务可以导入资源。
            {
                AddValidationMessage("ImportRequestResources 收到空 request。"); // 记录异常输入，便于 Debug 时定位调用链。

                return; // 直接结束导入，资源表保持为空。
            }

            if (!request.IsValid) // 如果 request 无效，说明它不应该提供可执行的 RenderGraph 资源。
            {
                AddValidationMessage("ImportRequestResources 收到无效 request。"); // 记录无效请求，避免 dump 里看不出资源为空的原因。

                return; // 直接结束导入，避免把无效 request 的目标注册进资源表。
            }

            resources.RegisterFinalCameraTarget(request.TargetIdentifier); // 把 request 的原始输出目标注册为 FinalCameraTarget，FinalBlit 最后会把中间颜色拷贝到这里。

            resources.RegisterCameraColorTexture(); // 把 BurtRP 自己的临时颜色 RT 注册成 CameraColor，让场景绘制不再直接写 backbuffer。
            resources.RegisterOpaqueCameraColorTexture();
            if (ShouldRegisterRefraction(request, asset))
            {
                resources.RegisterRefractionDistortionTexture();
                resources.RegisterRefractionSceneColorMipChainTexture();
            }

            resources.RegisterCameraDepthTexture(); // 把 BurtRP 自己的临时深度 RT 注册成 CameraDepth，让颜色目标和深度目标真正分离。

            if (BurtLightShaftOcclusionUtility.ShouldUseLightShaftOcclusion(request))
            {
                resources.RegisterRenderTarget(
                    BurtRenderGraphResourceRegistry.LightShaftOcclusionName,
                    new UnityEngine.Rendering.RenderTargetIdentifier(
                        BurtRenderGraphResourceRegistry.LightShaftOcclusionTextureId));
                resources.RegisterRenderTarget(
                    BurtRenderGraphResourceRegistry.LightShaftOcclusionTempName,
                    new UnityEngine.Rendering.RenderTargetIdentifier(
                        BurtRenderGraphResourceRegistry.LightShaftOcclusionTempTextureId));
            }

            if (BurtLightShaftOcclusionUtility.ShouldUseLightShaftBloom(request))
            {
                resources.RegisterRenderTarget(
                    BurtRenderGraphResourceRegistry.LightShaftBloomName,
                    new UnityEngine.Rendering.RenderTargetIdentifier(
                        BurtRenderGraphResourceRegistry.LightShaftBloomTextureId));
                resources.RegisterRenderTarget(
                    BurtRenderGraphResourceRegistry.LightShaftBloomTempName,
                    new UnityEngine.Rendering.RenderTargetIdentifier(
                        BurtRenderGraphResourceRegistry.LightShaftBloomTempTextureId));
            }

            if (BurtFogUtility.ShouldUseFog(request))
            {
                resources.RegisterRenderTarget(
                    BurtRenderGraphResourceRegistry.FogSourceColorName,
                    new UnityEngine.Rendering.RenderTargetIdentifier(
                        BurtRenderGraphResourceRegistry.FogSourceColorTextureId));
            }

            if (BurtVolumetricFogUtility.ShouldUseVolumetricFog(request))
            {
                resources.RegisterRenderTarget(
                    BurtRenderGraphResourceRegistry.VolumetricFogSourceColorName,
                    new UnityEngine.Rendering.RenderTargetIdentifier(
                        BurtRenderGraphResourceRegistry.VolumetricFogSourceColorTextureId));
            }

            if (BurtAtmosphereUtility.ShouldUseAerialPerspective(request))
            {
                resources.RegisterRenderTarget(
                    BurtRenderGraphResourceRegistry.AtmosphereAerialSourceColorName,
                    new UnityEngine.Rendering.RenderTargetIdentifier(
                        BurtRenderGraphResourceRegistry.AtmosphereAerialSourceColorTextureId));
            }

            if (request.LightingData != null && request.LightingData.AdditionalLightCount > 0)
            {
                resources.RegisterBuffer(BurtRenderGraphResourceRegistry.AdditionalLightBufferName, BurtLightingData.CreateAdditionalLightBufferDescriptor()); // Register only when the request has packed additional-light rows.
            }

            if (ShouldRegisterPostProcessColor(request, asset)) // 如果当前 request 启用了后处理框架，就把后处理中间颜色纳入资源表。
            {
                resources.RegisterPostProcessColorTexture(); // 注册 PostProcessColor 临时 RT，让分配、No-op Copy 和释放 Pass 使用同一个资源句柄。
                resources.RegisterTemporalAAOutputTexture();

                if (PostProcessPass.ShouldUseBloomPass(request, asset))
                {
                    resources.RegisterRenderTarget(
                        BurtRenderGraphResourceRegistry.BloomInputName,
                        new UnityEngine.Rendering.RenderTargetIdentifier(BurtRenderGraphResourceRegistry.BloomInputTextureId));
                    resources.RegisterRenderTarget(
                        BurtRenderGraphResourceRegistry.BloomSetupName,
                        new UnityEngine.Rendering.RenderTargetIdentifier(BurtRenderGraphResourceRegistry.BloomSetupTextureId));
                    for (var mipIndex = 0; mipIndex < BurtRenderGraphResourceRegistry.BloomPyramidCount; mipIndex++)
                    {
                        resources.RegisterRenderTarget(
                            BurtRenderGraphResourceRegistry.GetBloomDownsampleName(mipIndex),
                            new UnityEngine.Rendering.RenderTargetIdentifier(BurtRenderGraphResourceRegistry.GetBloomDownsampleTextureId(mipIndex)));
                        resources.RegisterRenderTarget(
                            BurtRenderGraphResourceRegistry.GetBloomGaussianHorizontalName(mipIndex),
                            new UnityEngine.Rendering.RenderTargetIdentifier(BurtRenderGraphResourceRegistry.GetBloomGaussianHorizontalTextureId(mipIndex)));
                        resources.RegisterRenderTarget(
                            BurtRenderGraphResourceRegistry.GetBloomGaussianVerticalName(mipIndex),
                            new UnityEngine.Rendering.RenderTargetIdentifier(BurtRenderGraphResourceRegistry.GetBloomGaussianVerticalTextureId(mipIndex)));
                    }
                }
            }

            if (ShouldRegisterGBufferTargets(request, asset)) // 如果当前 request 走 Deferred 实验路径，就把全部 GBuffer 目标纳入资源表。
            {
                resources.RegisterGBuffer0Texture(); // 注册 GBuffer0 临时 RT，让 Allocate、后续 GBuffer Pass 和 Release 使用同一个句柄。
                resources.RegisterGBuffer1Texture(); // 注册 GBuffer1 临时 RT，让 Allocate、后续 GBuffer Pass 和 Release 使用同一个句柄。
                resources.RegisterGBuffer2Texture(); // 注册 GBuffer2 临时 RT，让 Allocate、后续 GBuffer Pass 和 Release 使用同一个句柄。
                resources.RegisterGBuffer3Texture(); // 注册 GBuffer3 临时 RT，用于保存 Clear Coat 独立法线等专用扩展通道。
                resources.RegisterGBuffer4Texture(); // 注册 GBuffer4 临时 RT，用于保存底层 tangent 和 anisotropy。
                resources.RegisterGBuffer5Texture();
                resources.RegisterGBufferObjectIndexTexture();
                resources.RegisterDeferredLightingDepthTexture();
                if (ShouldRegisterTileLightBuffers(request, asset))
                {
                    resources.RegisterBuffer(BurtRenderGraphResourceRegistry.TileLightCountBufferName, BurtTiledLightData.CreateTileLightCountBufferDescriptor(request.Camera));
                    if (ShouldRegisterTileLightListBuffers(request, asset))
                    {
                        resources.RegisterBuffer(BurtRenderGraphResourceRegistry.TileLightListBufferName, BurtTiledLightData.CreateTileLightListBufferDescriptor(request.Camera));
                        resources.RegisterBuffer(BurtRenderGraphResourceRegistry.TileLightOffsetBufferName, BurtTiledLightData.CreateTileLightOffsetBufferDescriptor(request.Camera));
                    }
                }
                if (ShouldRegisterClusterLightBuffers(request, asset))
                {
                    resources.RegisterBuffer(BurtRenderGraphResourceRegistry.ClusterLightCountBufferName, BurtTiledLightData.CreateClusterLightCountBufferDescriptor(request.Camera));
                    resources.RegisterBuffer(BurtRenderGraphResourceRegistry.ClusterLightListBufferName, BurtTiledLightData.CreateClusterLightListBufferDescriptor(request.Camera, request.LightingData));
                    resources.RegisterBuffer(BurtRenderGraphResourceRegistry.ClusterLightOffsetBufferName, BurtTiledLightData.CreateClusterLightOffsetBufferDescriptor(request.Camera));
                }
                if (ShouldRegisterPunctualTileIdBuffer(request, asset))
                {
                    resources.RegisterBuffer(BurtRenderGraphResourceRegistry.PunctualTileIdBufferName, BurtTiledLightData.CreatePunctualTileIdBufferDescriptor(request.Camera));
                }
                if (ShouldRegisterHiZDepth(request, asset))
                {
                    resources.RegisterHiZDepthTexture();
                }

                if (ShouldRegisterScreenSpaceReflectionColor(request, asset))
                {
                    resources.RegisterScreenSpaceReflectionColorTexture();
                    resources.RegisterScreenSpaceReflectionDenoisedColorTexture();
                    resources.RegisterScreenSpaceReflectionTemporalColorTexture();
                }

                if (ShouldRegisterScreenSpaceAmbientOcclusion(request, asset))
                {
                    resources.RegisterScreenSpaceAmbientOcclusionRawTexture();
                    resources.RegisterScreenSpaceAmbientOcclusionTexture();
                }

                if (ShouldRegisterScreenSpaceShadow(request, asset))
                {
                    resources.RegisterScreenSpaceShadowTexture();
                }

                if (ShouldRegisterScreenSpaceGlobalIllumination(request, asset))
                {
                    resources.RegisterScreenSpaceGlobalIlluminationRawTexture();
                    resources.RegisterScreenSpaceGlobalIlluminationTexture();
                    resources.RegisterBurtGIBackfaceDiffuseIndirectTexture();
                    resources.RegisterBurtGIRoughSpecularIndirectTexture();
                    if (BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIlluminationBilateralUpsample(request, asset))
                    {
                        resources.RegisterScreenSpaceGlobalIlluminationUpsampledTexture();
                        resources.RegisterBurtGIBackfaceDiffuseIndirectUpsampledTexture();
                        resources.RegisterBurtGIRoughSpecularIndirectUpsampledTexture();
                    }
                    if (BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIlluminationTemporalDiagnostics(request, asset))
                    {
                        resources.RegisterBurtGITemporalDiagnosticsTexture();
                    }

                    if (BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIlluminationScreenProbeLite(request, asset))
                    {
                        var screenProbeSettings = BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationScreenProbeSettings(request, asset);
                        var useScreenProbeTraceCompact = screenProbeSettings.TraceCompact &&
                            BurtScreenSpaceGlobalIlluminationPassUtility.SupportsScreenProbeTraceCompactCompute();
                        var useRadianceCacheClipMap = BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIlluminationRadianceCacheClipMapContract(request, asset);
                        var useRadianceCacheHashGrid = BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIlluminationRadianceCacheHashGrid(request, asset);
                        var useTranslucencyVolume = BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIlluminationTranslucencyVolume(request, asset);
                        var useSceneVoxel = BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIlluminationSceneVoxel(request, asset);
                        var useIntegrateTileData = BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIlluminationScreenProbeIntegrateTileData(request, asset);
                        var useIntegrateTileClassification = BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIlluminationScreenProbeIntegrateTileClassification(request, asset);
                        resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeIndirectArgsBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationScreenProbeIndirectArgsBufferDescriptor());
                        if (useIntegrateTileData)
                        {
                            resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeIntegrateTileIndirectArgsBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationScreenProbeIntegrateTileIndirectArgsBufferDescriptor());
                            resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeIntegrateTileDataDiffuseBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationScreenProbeIntegrateTileDataDiffuseBufferDescriptor(request.Camera, screenProbeSettings));
                            resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeIntegrateTileDataAllBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationScreenProbeIntegrateTileDataAllBufferDescriptor(request.Camera, screenProbeSettings));
                        }
                        if (useIntegrateTileClassification)
                        {
                            resources.RegisterBurtGIScreenProbeIntegrateTileClassificationTexture();
                        }
                        if (useScreenProbeTraceCompact)
                        {
                            resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceCompactTexelCountBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationScreenProbeTraceCompactTexelCountBufferDescriptor());
                            resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceCompactTexelDataBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationScreenProbeTraceCompactTexelDataBufferDescriptor(request.Camera, screenProbeSettings));
                            resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceCompactIndirectArgsBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationScreenProbeTraceCompactIndirectArgsBufferDescriptor());
                            resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceCompactThreadCountXBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationScreenProbeTraceCompactThreadCountXBufferDescriptor());
                        }
                        resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeAdaptiveProbeNumBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationScreenProbeAdaptiveProbeNumBufferDescriptor());
                        resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeAdaptiveProbeDataBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationScreenProbeAdaptiveProbeDataBufferDescriptor(request.Camera, screenProbeSettings));
                        resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeImportancePDFSHBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationScreenProbeImportancePDFSHBufferDescriptor(request.Camera, screenProbeSettings));
                        if (useRadianceCacheClipMap)
                        {
                            var radianceCacheClipMapPersistentBuffers = BurtRadianceCacheClipMapPersistentBufferUtility.EnsureBuffers(request, screenProbeSettings);
                            if (radianceCacheClipMapPersistentBuffers.IsValid)
                            {
                                resources.RegisterExternalBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeAllocatorBufferName, radianceCacheClipMapPersistentBuffers.ProbeAllocator);
                                resources.RegisterExternalBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeFreeListAllocatorBufferName, radianceCacheClipMapPersistentBuffers.ProbeFreeListAllocator);
                                resources.RegisterExternalBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeFreeListBufferName, radianceCacheClipMapPersistentBuffers.ProbeFreeList);
                                resources.RegisterExternalBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeLastUsedFrameBufferName, radianceCacheClipMapPersistentBuffers.ProbeLastUsedFrame);
                                resources.RegisterExternalBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeLastTracedFrameBufferName, radianceCacheClipMapPersistentBuffers.ProbeLastTracedFrame);
                                resources.RegisterExternalBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeWorldOffsetBufferName, radianceCacheClipMapPersistentBuffers.ProbeWorldOffset);
                            }
                            else
                            {
                                resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeAllocatorBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheClipMapProbeAllocatorBufferDescriptor());
                                resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeFreeListAllocatorBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheClipMapProbeFreeListAllocatorBufferDescriptor());
                                resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeFreeListBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheClipMapProbeFreeListBufferDescriptor(request.Camera, screenProbeSettings));
                                resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeLastUsedFrameBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheClipMapProbeLastUsedFrameBufferDescriptor(request.Camera, screenProbeSettings));
                                resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeLastTracedFrameBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheClipMapProbeLastTracedFrameBufferDescriptor(request.Camera, screenProbeSettings));
                                resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeWorldOffsetBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheClipMapProbeWorldOffsetBufferDescriptor(request.Camera, screenProbeSettings));
                            }
                            resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeTraceDataBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheClipMapProbeTraceDataBufferDescriptor(request.Camera, screenProbeSettings));
                            resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeTraceAllocatorBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheClipMapProbeTraceAllocatorBufferDescriptor());
                            resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapPriorityHistogramBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheClipMapPriorityHistogramBufferDescriptor());
                            resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapMaxUpdateBucketBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheClipMapMaxUpdateBucketBufferDescriptor());
                            resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapMaxTracesFromMaxUpdateBucketBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheClipMapMaxTracesFromMaxUpdateBucketBufferDescriptor());
                            resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbesToUpdateTraceCostBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheClipMapProbesToUpdateTraceCostBufferDescriptor());
                            resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapRadianceProbePDFBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheClipMapRadianceProbePDFBufferDescriptor(request.Camera, screenProbeSettings));
                            resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapClearProbePDFsIndirectArgsBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheClipMapClearProbePDFsIndirectArgsBufferDescriptor());
                            resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapGenerateProbeTraceTilesIndirectArgsBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheClipMapGenerateProbeTraceTilesIndirectArgsBufferDescriptor());
                            resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeTraceTileAllocatorBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheClipMapProbeTraceTileAllocatorBufferDescriptor());
                            resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapFilterProbesIndirectArgsBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheClipMapFilterProbesIndirectArgsBufferDescriptor());
                            resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapPrepareProbeOcclusionIndirectArgsBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheClipMapPrepareProbeOcclusionIndirectArgsBufferDescriptor());
                            resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapFixupProbeBordersIndirectArgsBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheClipMapFixupProbeBordersIndirectArgsBufferDescriptor());
                            resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapTraceProbesIndirectArgsBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheClipMapTraceProbesIndirectArgsBufferDescriptor());
                            resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapSortProbeTraceTilesIndirectArgsBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheClipMapSortProbeTraceTilesIndirectArgsBufferDescriptor());
                            resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapRadianceCacheHardwareRayTracingIndirectArgsBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheClipMapRadianceCacheHardwareRayTracingIndirectArgsBufferDescriptor());
                            resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapHardwareRayTracingRayAllocatorBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheClipMapHardwareRayTracingRayAllocatorBufferDescriptor());
                            resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeTraceTileDataBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheClipMapProbeTraceTileDataBufferDescriptor(request.Camera, screenProbeSettings));
                            resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapSortedProbeTraceTileDataBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheClipMapSortedProbeTraceTileDataBufferDescriptor(request.Camera, screenProbeSettings));
                        }
                        if (useRadianceCacheHashGrid)
                        {
                            var radianceCacheHashGridHistoryBuffers = BurtRadianceCacheHashGridHistoryUtility.EnsureHistoryBuffers(request, screenProbeSettings, out _);
                            if (radianceCacheHashGridHistoryBuffers.IsValid)
                            {
                                resources.RegisterExternalBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheHashGridValueBufferName, radianceCacheHashGridHistoryBuffers.Value);
                                resources.RegisterExternalBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheHashGridTileBufferName, radianceCacheHashGridHistoryBuffers.Tile);
                                resources.RegisterExternalBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheHashGridCountBufferName, radianceCacheHashGridHistoryBuffers.Count);
                                resources.RegisterExternalBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheHashGridUpdateCellValueBufferName, radianceCacheHashGridHistoryBuffers.UpdateCellValue);
                            }
                            else
                            {
                                resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheHashGridValueBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheHashGridValueBufferDescriptor());
                                resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheHashGridTileBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheHashGridTileBufferDescriptor());
                                resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheHashGridCountBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheHashGridCountBufferDescriptor());
                                resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheHashGridUpdateCellValueBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheHashGridUpdateCellValueBufferDescriptor());
                            }
                            resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheHashGridVisibilityCellQueryBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheHashGridVisibilityCellQueryBufferDescriptor(request.Camera, screenProbeSettings));
                            resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheHashGridUpdateTileBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheHashGridUpdateTileBufferDescriptor(request.Camera, screenProbeSettings));
                            resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheHashGridUpdateTilesIndirectArgsBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheHashGridUpdateTilesIndirectArgsBufferDescriptor());
                            resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheHashGridUpdateTilesGroupCountXBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheHashGridUpdateTilesGroupCountXBufferDescriptor());
                            resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheHashGridDebugCellBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheHashGridDebugCellBufferDescriptor());
                            resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIRadianceCacheHashGridDebugDrawArgsBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheHashGridDebugDrawArgsBufferDescriptor());
                        }
                        resources.RegisterBurtGIScreenProbeScreenDepthTexture();
                        resources.RegisterBurtGIScreenProbeWorldNormalTexture();
                        resources.RegisterBurtGIScreenProbeWorldPositionTexture();
                        resources.RegisterBurtGIScreenProbeAdaptiveProbeHeaderTexture();
                        resources.RegisterBurtGIScreenProbeAdaptiveProbeIndicesTexture();
                        if (useRadianceCacheClipMap)
                        {
                            resources.RegisterBurtGIRadianceCacheClipMapIndirectionTexture();
                            resources.RegisterBurtGIRadianceCacheClipMapDepthProbeAtlasTexture();
                            resources.RegisterBurtGIRadianceCacheClipMapRadianceProbeAtlasTexture();
                            resources.RegisterBurtGIRadianceCacheClipMapFinalRadianceAtlasTexture();
                            resources.RegisterBurtGIRadianceCacheClipMapFinalIrradianceAtlasTexture();
                            resources.RegisterBurtGIRadianceCacheClipMapProbeOcclusionAtlasTexture();
                            resources.RegisterBurtGIRadianceCacheClipMapProbeSkyAOAtlasTexture();
                            resources.RegisterBurtGIRadianceCacheStatsTexture();
                        }
                        if (useTranslucencyVolume)
                        {
                            resources.RegisterBurtGITranslucencyVolume0Texture();
                            resources.RegisterBurtGITranslucencyVolume1Texture();
                            resources.RegisterBurtGITranslucencyVolumeFilter0Texture();
                            resources.RegisterBurtGITranslucencyVolumeFilter1Texture();
                            resources.RegisterBurtGITranslucencyVolumeTraceRadianceTexture();
                            resources.RegisterBurtGITranslucencyVolumeTraceFilteredRadianceTexture();
                            resources.RegisterBurtGITranslucencyVolumeTraceHitDistanceTexture();
                        }
                        if (useSceneVoxel)
                        {
                            resources.RegisterBurtGISceneVoxelRadianceTexture();
                            resources.RegisterBurtGISceneVoxelGeometryTexture();
                            resources.RegisterBurtGISceneVoxelOccupancyMipTexture();
                            resources.RegisterBurtGISceneVoxelLightingTexture();
                        }
                        resources.RegisterBurtGIScreenProbeRadianceTexture();
                        resources.RegisterBurtGIScreenProbeIrradianceTexture();
                        resources.RegisterBurtGIScreenProbeConfidenceTexture();
                        resources.RegisterBurtGIScreenProbeHitDistanceTexture();
                        resources.RegisterBurtGIScreenProbeBentNormalTexture();
                        resources.RegisterBurtGIScreenProbeTraceRadianceTexture();
                        resources.RegisterBurtGIScreenProbeTraceHitTexture();
                        resources.RegisterBurtGIScreenProbeTemporalRadianceTexture();
                        resources.RegisterBurtGIScreenProbeTemporalIrradianceTexture();
                        resources.RegisterBurtGIScreenProbeTemporalConfidenceTexture();
                        resources.RegisterBurtGIScreenProbeFilteredRadianceTexture();
                        resources.RegisterBurtGIScreenProbeFilteredIrradianceTexture();
                        resources.RegisterBurtGIScreenProbeFilteredConfidenceTexture();
                        resources.RegisterBurtGIScreenProbeFixupRadianceTexture();
                        resources.RegisterBurtGIScreenProbeFixupIrradianceTexture();
                        resources.RegisterBurtGIScreenProbeFixupConfidenceTexture();
                        resources.RegisterBurtGIScreenProbeMipRadianceTexture();
                        resources.RegisterBurtGIScreenProbeMipIrradianceTexture();
                        resources.RegisterBurtGIScreenProbeMipConfidenceTexture();
                        resources.RegisterBurtGIScreenProbeMip2RadianceTexture();
                        resources.RegisterBurtGIScreenProbeMip2IrradianceTexture();
                        resources.RegisterBurtGIScreenProbeMip2ConfidenceTexture();
                        resources.RegisterBurtGIScreenProbeMip3RadianceTexture();
                        resources.RegisterBurtGIScreenProbeMip3IrradianceTexture();
                        resources.RegisterBurtGIScreenProbeMip3ConfidenceTexture();
                        resources.RegisterBurtGIScreenProbeRadianceSHAmbientTexture();
                        resources.RegisterBurtGIScreenProbeRadianceSHDirectionalTexture();
                        resources.RegisterBurtGIScreenProbeIrradianceOctTexture();
                        resources.RegisterBurtGIScreenProbeRadianceOctTexture();
                        resources.RegisterBurtGIScreenProbeImportancePDFTexture();
                        resources.RegisterBurtGIScreenProbeImportanceLightPDFTexture();
                        resources.RegisterBurtGIScreenProbeImportanceRayInfoTexture();
                    }
                }

                if (ShouldRegisterScreenSpaceSubsurface(request, asset))
                {
                    resources.RegisterScreenSpaceSubsurfaceSourceTexture();
                    resources.RegisterScreenSpaceSubsurfaceBaseColorTexture();
                    resources.RegisterScreenSpaceSubsurfaceEmissionTexture();
                    resources.RegisterScreenSpaceSubsurfaceProfileIDAndTypeTexture();
                    if (BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceMaskTexture(request, asset))
                    {
                        resources.RegisterScreenSpaceSubsurfaceMaskTexture();
                    }

                    resources.RegisterScreenSpaceSubsurfaceTempTexture();
                    resources.RegisterScreenSpaceSubsurfaceBlurTexture();
                    if (BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceDebugView(request, asset))
                    {
                        resources.RegisterScreenSpaceSubsurfaceCombineTexture();
                    }
                    if (BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceBurley(request, asset))
                    {
                        resources.RegisterBuffer(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBurleyArgsBufferName, BurtScreenSpaceSubsurfacePassUtility.CreateBurleyArgsBufferDescriptor());
                        resources.RegisterBuffer(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBurleyGroupBufferName, BurtScreenSpaceSubsurfacePassUtility.CreateBurleyGroupBufferDescriptor(request.Camera));
                        resources.RegisterScreenSpaceSubsurfaceVelocityTexture();
                    }
                    if (BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceSeparable(request, asset))
                    {
                        resources.RegisterBuffer(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSeparableArgsBufferName, BurtScreenSpaceSubsurfacePassUtility.CreateSeparableArgsBufferDescriptor());
                        resources.RegisterBuffer(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSeparableGroupBufferName, BurtScreenSpaceSubsurfacePassUtility.CreateSeparableGroupBufferDescriptor(request.Camera));
                    }
                }

                if (ShouldRegisterFurBlur(request, asset))
                {
                    resources.RegisterFurBlurPropertyTexture();
                    resources.RegisterFurBlurPropertyTempTexture();
                    resources.RegisterFurBlurColorTexture();
                    resources.RegisterFurBlurTemporalTexture();
                    resources.RegisterFurBlurVelocityTexture();
                    if (BurtFurBlurPassUtility.ShouldUseTiledFurBlur(request, asset))
                    {
                        resources.RegisterBuffer(BurtRenderGraphResourceRegistry.FurBlurArgsBufferName, BurtFurBlurPassUtility.CreateTileArgsBufferDescriptor());
                        resources.RegisterBuffer(BurtRenderGraphResourceRegistry.FurBlurTileDataBufferName, BurtFurBlurPassUtility.CreateTileDataBufferDescriptor(request.Camera));
                    }
                }
            }

            if (ShouldRegisterMainLightShadowMap(request, asset)) // 如果当前 request 的主光需要阴影，就把主光阴影图纳入资源表。
            {
                resources.RegisterMainLightShadowMapTexture(); // 注册主光阴影图临时 RT，让后续分配、绘制和释放 Pass 使用同一个资源句柄。
            }

            if (ShouldRegisterAdditionalLightShadowAtlas(request))
            {
                resources.RegisterAdditionalLightShadowAtlasTexture();
            }

            if (ShouldRegisterPerObjectShadowAtlas(request, asset))
            {
                resources.RegisterPerObjectShadowAtlasTexture();
            }
        }

        private static bool ShouldRegisterPostProcessColor( // 定义判断当前 request 是否需要注册后处理中间颜色图的辅助函数。
            BurtRenderRequest request, // 接收当前渲染请求，用来确认后处理任务是否有效。
            BurtRenderPipelineAsset asset) // 接收当前管线资产，用来读取后处理设置。
        {
            return PostProcessUtility.ShouldUsePostProcessFramework(request, asset); // 复用后处理工具的判定逻辑，保证资源注册和 Pass 组装条件完全一致。
        }

        private static bool ShouldRegisterMainLightShadowMap( // 定义判断当前 request 是否需要注册主光阴影图的辅助函数。
            BurtRenderRequest request, // 接收当前渲染请求，用来读取 Light 解析出的阴影数据。
            BurtRenderPipelineAsset asset) // 接收当前管线资产，用来让资源注册尊重主光阴影总开关和默认配置。
        {
            return BurtShadowUtility.ShouldUseMainLightShadow(request, asset); // 复用阴影工具的判定逻辑，保证资源注册和 Pass 组装使用同一套条件。
        }

        private static bool ShouldRegisterAdditionalLightShadowAtlas(BurtRenderRequest request)
        {
            return BurtAdditionalLightShadowUtility.ShouldUseAdditionalLightShadows(request);
        }

        private static bool ShouldRegisterPerObjectShadowAtlas(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return BurtPerObjectShadowUtility.ShouldUsePerObjectShadow(request, asset);
        }

        private static bool ShouldRegisterGBufferTargets( // 定义判断当前 request 是否需要注册 Deferred GBuffer 资源的辅助函数。
            BurtRenderRequest request, // 接收当前渲染请求，用来确认 request 是否有效。
            BurtRenderPipelineAsset asset) // 接收当前管线资产，用来读取 Renderer Mode。
        {
            if (request == null) // 如果 request 为空，说明没有合法渲染任务。
            {
                return false; // 返回 false，避免为异常任务注册 GBuffer 资源。
            }

            if (!request.IsValid) // 如果 request 无效，就不应该提供可执行的 GBuffer 资源。
            {
                return false; // 返回 false，保持资源注册和渲染执行条件一致。
            }

            if (request.Type == BurtRenderRequestType.Preview || request.Type == BurtRenderRequestType.Reflection) // Preview/Reflection request 会强制走 Forward，不注册 GBuffer。
            {
                return false; // 返回 false，让资源表和实际 Forward 组装器保持一致。
            }

            if (asset == null) // 如果资产为空，就没有 Renderer Mode 配置来源。
            {
                return false; // 返回 false，默认保持 Forward 行为。
            }

            return asset.RendererMode == BurtRendererMode.Deferred; // 只有显式选择 Deferred 时才注册 GBuffer，默认 Forward 不受影响。
        }

        private static bool ShouldRegisterHiZDepth(
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset)
        {
            return ShouldRegisterGBufferTargets(request, asset) &&
                BurtHiZDepthPassUtility.ShouldUseHiZDepth(request, asset);
        }

        private static bool ShouldRegisterTileLightBuffers(
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset)
        {
            return ShouldRegisterGBufferTargets(request, asset) &&
                BurtTiledLightData.ShouldUseTiledLightResources(request, asset, true);
        }

        private static bool ShouldRegisterTileLightListBuffers(
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset)
        {
            return ShouldRegisterGBufferTargets(request, asset) &&
                BurtTiledLightData.ShouldUseTileLightListResources(request, asset, true);
        }

        private static bool ShouldRegisterClusterLightBuffers(
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset)
        {
            return ShouldRegisterGBufferTargets(request, asset) &&
                BurtTiledLightData.ShouldUseClusterLightResources(request, asset, true);
        }

        private static bool ShouldRegisterPunctualTileIdBuffer(
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset)
        {
            return ShouldRegisterGBufferTargets(request, asset) &&
                BurtTiledLightData.ShouldUsePunctualTileDrawResources(request, asset, true);
        }

        private static bool ShouldRegisterScreenSpaceReflectionColor(
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset)
        {
            return ShouldRegisterGBufferTargets(request, asset) && BurtScreenSpaceReflectionPassUtility.ShouldUseScreenSpaceReflections(request, asset);
        }

        private static bool ShouldRegisterScreenSpaceAmbientOcclusion(
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset)
        {
            return ShouldRegisterGBufferTargets(request, asset) && BurtScreenSpaceAmbientOcclusionPassUtility.ShouldUseScreenSpaceAmbientOcclusion(request, asset);
        }

        private static bool ShouldRegisterScreenSpaceShadow(
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset)
        {
            return ShouldRegisterGBufferTargets(request, asset) && BurtScreenSpaceShadowPassUtility.ShouldUseScreenSpaceShadow(request, asset);
        }

        private static bool ShouldRegisterScreenSpaceGlobalIllumination(
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset)
        {
            return ShouldRegisterGBufferTargets(request, asset) && BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIllumination(request, asset);
        }

        private static bool ShouldRegisterScreenSpaceSubsurface(
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset)
        {
            return ShouldRegisterGBufferTargets(request, asset) && BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(request, asset);
        }

        private static bool ShouldRegisterFurBlur(
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset)
        {
            return ShouldRegisterGBufferTargets(request, asset) && BurtFurBlurPassUtility.ShouldUseFurBlur(request, asset);
        }

        private static bool ShouldRegisterRefraction(
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset)
        {
            return BurtRefractionPassUtility.ShouldUseRefraction(request, asset);
        }

        public void AddPass(BurtRenderPass pass) // 定义添加 Pass 的函数，Assembler 会通过它把 Pass 放进图里。
        {
            if (pass == null) // 如果传入的 Pass 是空，说明组装器传入了异常数据。
            {
                AddValidationMessage("AddPass 收到空 Pass，已跳过。"); // 记录空 Pass，便于定位组装器问题。

                return; // 直接跳过，不把空 Pass 加进图里。
            }

            passes.Add(pass); // 把有效 Pass 加入当前 RenderGraph 的执行列表。
        }

        public void Execute(BurtRenderGraphContext context) // 定义执行函数，用来先收集资源声明再顺序执行所有 Pass。
        {
            if (context == null) // 如果执行上下文为空，说明调用方传入了异常数据。
            {
                AddValidationMessage("Execute 收到空 RenderGraphContext。"); // 记录异常上下文，便于 Debug dump 解释为什么没有执行。

                return; // 直接结束执行，避免后面访问空对象。
            }

            if (RequiresFullCompilation())
            {
                using (ConfigureGraphMarker.Auto())
                {
                    ConfigurePasses(context); // 在真正执行前收集所有 Pass 的资源读写声明，并把当前上下文传给配置阶段。
                    if (compilationMode == BurtRenderGraphCompilationMode.Full)
                    {
                        ValidateConfiguredGraph(); // Debug capture performs diagnostics; runtime culling only compiles resource dependencies.
                    }
                    compileResult = compiler.Compile(passes, resourceUsages, resources);
                    if (compilationMode == BurtRenderGraphCompilationMode.Full)
                    {
                        AddValidationMessage("RenderGraph Compiler: dependencies=" + compileResult.DependencyCount +
                            ", culled=" + compileResult.CulledPassCount +
                            ", lifetimes=" + compileResult.ResourceLifetimes.Count + ".");
                    }
                }
            }
            else
            {
                resourceUsages.Clear();
                compileResult = null;
            }

            using var executeGraphScope = ExecuteGraphMarker.Auto();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (profilingAssemblyScopeStack.Count > 0)
            {
                AddValidationMessage("RenderGraph 存在未闭合 Profiling scope: " + profilingAssemblyScopeStack[profilingAssemblyScopeStack.Count - 1]);
            }

            if (profilingMode == BurtRenderGraphProfilingMode.Off)
            {
                ExecutePassesWithoutProfiling(context);
                context.FlushCommandBuffer(false);
                return;
            }

            var ownsProfilingCommandBuffer = !context.HasSharedCommandBuffer;
            var profilingCommandBuffer = context.CommandBuffer ?? CommandBufferPool.Get("BRP.RenderGraph/Profiling");
            var profileIndividualPasses = profilingMode == BurtRenderGraphProfilingMode.CameraStageAndPass;
            ProfilingSampler activeResourceLifetimeSampler = null;
            string activeResourceLifetimeScopeName = null;
            activeProfilingScopes.Clear();
            try
            {
                var profilingEventIndex = 0;
                for (var passIndex = 0; passIndex <= passes.Count; passIndex++)
                {
                    // A graph-assembly profiling event can close the current Stage. Resource
                    // scopes must be closed first so the GPU marker stack remains well nested.
                    if (activeResourceLifetimeSampler != null &&
                        profilingEventIndex < profilingEvents.Count &&
                        profilingEvents[profilingEventIndex].PassIndex == passIndex)
                    {
                        EndProfilingScope(context, profilingCommandBuffer, activeResourceLifetimeSampler);
                        activeResourceLifetimeSampler = null;
                        activeResourceLifetimeScopeName = null;
                    }

                    while (profilingEventIndex < profilingEvents.Count &&
                        profilingEvents[profilingEventIndex].PassIndex == passIndex)
                    {
                        ExecuteProfilingEvent(context, profilingCommandBuffer, profilingEvents[profilingEventIndex]);
                        profilingEventIndex++;
                    }

                    if (passIndex == passes.Count)
                    {
                        break;
                    }

                    var pass = passes[passIndex];
                    if (pass == null || !ShouldExecutePass(passIndex))
                    {
                        continue;
                    }

                    var resourceLifetimeScopeName = profileIndividualPasses
                        ? GetResourceLifetimeScopeName(pass)
                        : null;
                    if (!string.IsNullOrEmpty(resourceLifetimeScopeName))
                    {
                        if (!string.Equals(activeResourceLifetimeScopeName, resourceLifetimeScopeName, StringComparison.Ordinal))
                        {
                            if (activeResourceLifetimeSampler != null)
                            {
                                EndProfilingScope(context, profilingCommandBuffer, activeResourceLifetimeSampler);
                            }

                            activeResourceLifetimeScopeName = resourceLifetimeScopeName;
                            activeResourceLifetimeSampler = GetOrCreateProfilingSampler(resourceLifetimeScopeName);
                            BeginProfilingScope(context, profilingCommandBuffer, activeResourceLifetimeSampler);
                        }

                        ExecutePass(context, pass);
                        continue;
                    }

                    if (activeResourceLifetimeSampler != null)
                    {
                        EndProfilingScope(context, profilingCommandBuffer, activeResourceLifetimeSampler);
                        activeResourceLifetimeSampler = null;
                        activeResourceLifetimeScopeName = null;
                    }

                    if (!profileIndividualPasses)
                    {
                        ExecutePass(context, pass);
                        continue;
                    }

                    var passSampler = GetOrCreateProfilingSampler("BRP.Pass/" + GetPassName(pass));
                    BeginProfilingScope(context, profilingCommandBuffer, passSampler);
                    try
                    {
                        ExecutePass(context, pass);
                    }
                    finally
                    {
                        EndProfilingScope(context, profilingCommandBuffer, passSampler);
                    }
                }
            }
            finally
            {
                if (activeResourceLifetimeSampler != null)
                {
                    EndProfilingScope(context, profilingCommandBuffer, activeResourceLifetimeSampler);
                    activeResourceLifetimeSampler = null;
                    activeResourceLifetimeScopeName = null;
                }

                for (var scopeIndex = activeProfilingScopes.Count - 1; scopeIndex >= 0; scopeIndex--)
                {
                    EndProfilingScope(context, profilingCommandBuffer, activeProfilingScopes[scopeIndex].Sampler);
                }

                activeProfilingScopes.Clear();
                if (ownsProfilingCommandBuffer)
                {
                    profilingCommandBuffer.Clear();
                    CommandBufferPool.Release(profilingCommandBuffer);
                }
            }

            context.FlushCommandBuffer(false);
#else
            ExecutePassesWithoutProfiling(context);
            context.FlushCommandBuffer(false);
#endif
        }

        private void ExecutePassesWithoutProfiling(BurtRenderGraphContext context)
        {
            for (var passIndex = 0; passIndex < passes.Count; passIndex++)
            {
                var pass = passes[passIndex];
                if (pass != null && ShouldExecutePass(passIndex))
                {
                    ExecutePass(context, pass);
                }
            }
        }

        private bool ShouldExecutePass(int passIndex)
        {
            return compileResult == null || compileResult.ShouldExecute(passIndex);
        }

        private static string GetResourceLifetimeScopeName(BurtRenderPass pass)
        {
            if (pass == null)
            {
                return null;
            }

            var passName = GetPassName(pass);
            if (pass.Kind == BurtRenderPassKind.Allocate ||
                passName.StartsWith("Burt Allocate ", StringComparison.OrdinalIgnoreCase))
            {
                return "BRP.Resources/Allocate/" + TrimResourceLifetimePassPrefix(passName, "Burt Allocate ");
            }

            if (pass.Kind == BurtRenderPassKind.Release ||
                passName.StartsWith("Burt Release ", StringComparison.OrdinalIgnoreCase))
            {
                return "BRP.Resources/Release/" + TrimResourceLifetimePassPrefix(passName, "Burt Release ");
            }

            return null;
        }

        private static string TrimResourceLifetimePassPrefix(string passName, string prefix)
        {
            if (!string.IsNullOrEmpty(passName) && passName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return passName.Substring(prefix.Length);
            }

            return string.IsNullOrEmpty(passName) ? "Unnamed" : passName;
        }

        private bool RequiresFullCompilation()
        {
            return compilationMode != BurtRenderGraphCompilationMode.Lightweight;
        }

        private void ExecutePass(BurtRenderGraphContext context, BurtRenderPass pass)
        {
            try
            {
                if (CanExecuteAsync(context, pass))
                {
                    ExecuteAsyncPass(context, pass);
                }
                else
                {
                    pass.Execute(context);
                }
            }
            catch
            {
                context.DiscardCommandBuffer();
                if (!isExecutingEmergencyCleanup)
                {
                    ExecuteEmergencyCleanup(context, passes.IndexOf(pass) + 1);
                }
                throw;
            }
        }

        private static bool CanExecuteAsync(BurtRenderGraphContext context, BurtRenderPass pass)
        {
            return pass != null &&
                pass.EnableAsyncCompute &&
                context != null &&
                context.Asset != null &&
                context.Asset.EnableAsyncCompute &&
                (context.Request == null ||
                    context.Request.CameraData == null ||
                    context.Request.CameraData.EnableAsyncCompute) &&
                SystemInfo.supportsAsyncCompute &&
                SystemInfo.supportsGraphicsFence &&
                context.HasSharedCommandBuffer;
        }

        private static void ExecuteAsyncPass(BurtRenderGraphContext context, BurtRenderPass pass)
        {
            var graphicsToAsyncFence = context.CommandBuffer.CreateGraphicsFence(
                GraphicsFenceType.AsyncQueueSynchronisation,
                SynchronisationStageFlags.PixelProcessing);
            context.MarkCommandBufferHasCommands();
            context.FlushCommandBuffer(false);
            var asyncCommandBuffer = CommandBufferPool.Get("BRP.Async/" + GetPassName(pass));
            asyncCommandBuffer.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
            asyncCommandBuffer.WaitOnAsyncGraphicsFence(graphicsToAsyncFence);
            context.BeginAsyncPass(asyncCommandBuffer);
            try
            {
                pass.Execute(context);
                var fence = asyncCommandBuffer.CreateGraphicsFence(
                    GraphicsFenceType.AsyncQueueSynchronisation,
                    SynchronisationStageFlags.ComputeProcessing);
                context.MarkCommandBufferHasCommands();
                context.ExecuteCurrentCommandBufferAsync(ComputeQueueType.Default);
                context.EndAsyncPass();
                context.CommandBuffer.WaitOnAsyncGraphicsFence(fence);
                context.MarkCommandBufferHasCommands();
            }
            finally
            {
                context.EndAsyncPass();
                asyncCommandBuffer.Clear();
                CommandBufferPool.Release(asyncCommandBuffer);
            }
        }

        private void ExecuteEmergencyCleanup(BurtRenderGraphContext context, int firstPassIndex)
        {
            isExecutingEmergencyCleanup = true;
            try
            {
                for (var passIndex = Math.Max(0, firstPassIndex); passIndex < passes.Count; passIndex++)
                {
                    var cleanupPass = passes[passIndex];
                    if (cleanupPass == null || cleanupPass.Kind != BurtRenderPassKind.Release)
                    {
                        continue;
                    }

                    try
                    {
                        cleanupPass.Execute(context);
                        context.FlushCommandBuffer();
                    }
                    catch (Exception cleanupException)
                    {
                        context.DiscardCommandBuffer();
                        AddValidationMessage("Emergency cleanup failed for " + GetPassName(cleanupPass) +
                            ": " + cleanupException.GetType().Name + " - " + cleanupException.Message);
                    }
                }
            }
            finally
            {
                isExecutingEmergencyCleanup = false;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void ExecuteProfilingEvent(
            BurtRenderGraphContext context,
            CommandBuffer commandBuffer,
            BurtRenderGraphProfilingEvent profilingEvent)
        {
            if (profilingEvent.IsBegin)
            {
                BeginProfilingScope(context, commandBuffer, profilingEvent.Sampler);
                activeProfilingScopes.Add(profilingEvent);
            }
            else
            {
                if (activeProfilingScopes.Count == 0)
                {
                    AddValidationMessage("执行 Profiling End 时没有活动 Scope: " + profilingEvent.ScopeName);
                    return;
                }

                var lastIndex = activeProfilingScopes.Count - 1;
                var openedEvent = activeProfilingScopes[lastIndex];
                activeProfilingScopes.RemoveAt(lastIndex);
                if (!string.Equals(openedEvent.ScopeName, profilingEvent.ScopeName, StringComparison.Ordinal))
                {
                    AddValidationMessage("执行 Profiling Scope 顺序不匹配: expected " + openedEvent.ScopeName + ", actual " + profilingEvent.ScopeName);
                }

                EndProfilingScope(context, commandBuffer, openedEvent.Sampler);
            }
        }

        private static void BeginProfilingScope(
            BurtRenderGraphContext context,
            CommandBuffer commandBuffer,
            ProfilingSampler sampler)
        {
            if (commandBuffer == context.CommandBuffer)
            {
                context.BeginProfilingScope(sampler);
                return;
            }

            sampler.Begin(commandBuffer);
            context.ScriptableContext.ExecuteCommandBuffer(commandBuffer);
            commandBuffer.Clear();
        }

        private static void EndProfilingScope(
            BurtRenderGraphContext context,
            CommandBuffer commandBuffer,
            ProfilingSampler sampler)
        {
            if (commandBuffer == context.CommandBuffer)
            {
                context.EndProfilingScope(sampler);
                return;
            }

            sampler.End(commandBuffer);
            context.ScriptableContext.ExecuteCommandBuffer(commandBuffer);
            commandBuffer.Clear();
        }

        private ProfilingSampler GetOrCreateProfilingSampler(string name)
        {
            var safeName = NormalizeProfilingName(name, "BRP.Unknown");
            if (profilingSamplers.TryGetValue(safeName, out var sampler))
            {
                return sampler;
            }

            if (profilingSamplers.Count >= MaxCachedProfilingSamplerCount)
            {
                return overflowProfilingSampler;
            }

            sampler = new ProfilingSampler(safeName);
            profilingSamplers.Add(safeName, sampler);
            return sampler;
        }

        private static string NormalizeProfilingName(string name, string fallback)
        {
            return string.IsNullOrWhiteSpace(name) ? fallback : name;
        }
#endif

        public string DumpDebugInfo(BurtRenderRequest request) // 保留旧 Dump 入口，未传入 RT 执行选项时仍然输出基础 RenderGraph 信息。
        {
            return DumpDebugInfo(request, null); // 转发到新入口，并用 null 表示没有额外 RT 生命周期选项。
        }

        public string DumpDebugInfo( // 定义生成带 RT 生命周期选项的 RenderGraph 调试文本的函数，具体格式交给 Debugging 工具维护。
            BurtRenderRequest request, // 接收当前渲染请求，用来输出 Request 和 Camera 信息。
            BurtRequestRenderOptions renderOptions) // 接收当前 request 的栈级 RT 生命周期选项。
        {
            return AppendCompilerDebugInfo(BurtRenderGraphDebugUtility.BuildDump(request, passes.Count, resourceUsages, validationMessages, resources, renderOptions)); // 把 request、Pass、资源声明、校验和 RT 生命周期选项交给统一工具格式化。
        }

        public void FlushDeferredResourceReleases()
        {
            resources.FlushDeferredBufferReleases();
        }

        public void DisposeResources()
        {
            resources.DisposeResources();
        }

        public string DumpDebugInfo( // 定义带管线资产状态的 RenderGraph 调试文本入口。
            BurtRenderRequest request, // 接收当前渲染请求，用来输出 Request 和 Camera 信息。
            BurtRenderPipelineAsset asset, // 接收当前 BurtRP 管线资产，用来输出 Renderer Mode 和 Debug View 状态。
            BurtRequestRenderOptions renderOptions) // 接收当前 request 的栈级 RT 生命周期选项。
        {
            return AppendCompilerDebugInfo(BurtRenderGraphDebugUtility.BuildDump(request, passes.Count, resourceUsages, validationMessages, resources, renderOptions, asset)); // 把 request、资源声明、RT 生命周期和资产调试状态交给统一工具格式化。
        }

        private string AppendCompilerDebugInfo(string debugInfo)
        {
            if (compileResult == null)
            {
                return debugInfo;
            }

            var builder = new StringBuilder(debugInfo ?? string.Empty);
            builder.AppendLine();
            builder.AppendLine("RenderGraph Compiler");
            builder.Append("Dependencies: ").Append(compileResult.DependencyCount)
                .Append(", Culled Passes: ").Append(compileResult.CulledPassCount)
                .Append(", Resource Lifetimes: ").Append(compileResult.ResourceLifetimes.Count)
                .AppendLine();
            for (var passIndex = 0; passIndex < compileResult.PassCount; passIndex++)
            {
                var dependencies = compileResult.Dependencies[passIndex];
                if (dependencies == null || dependencies.Count == 0)
                {
                    continue;
                }

                builder.Append('#').Append(passIndex).Append(" <- ");
                for (var dependencyIndex = 0; dependencyIndex < dependencies.Count; dependencyIndex++)
                {
                    if (dependencyIndex > 0)
                    {
                        builder.Append(", ");
                    }

                    builder.Append('#').Append(dependencies[dependencyIndex]);
                }

                builder.AppendLine();
            }

            builder.AppendLine("Resource Lifetimes");
            for (var lifetimeIndex = 0; lifetimeIndex < compileResult.ResourceLifetimes.Count; lifetimeIndex++)
            {
                var lifetime = compileResult.ResourceLifetimes[lifetimeIndex];
                builder.Append(lifetime.ResourceKey)
                    .Append(" [").Append(lifetime.FirstPass).Append("..").Append(lifetime.LastPass).Append(']');
                if (lifetime.AliasSlot >= 0)
                {
                    builder.Append(" buffer-alias-slot=").Append(lifetime.AliasSlot);
                }

                builder.AppendLine();
            }

            return builder.ToString();
        }

        private void ConfigurePasses(BurtRenderGraphContext context) // 定义资源声明收集函数，用来调用每个 Pass 的 Configure，并给 Builder 提供当前上下文。
        {
            resourceUsages.Clear(); // 每次执行前清空旧声明，保证 ResourceUsages 只描述当前图。
            // 不在这里清空 validationMessages，保留 Import/AddPass 阶段已经记录的图级问题。

            for (var passIndex = 0; passIndex < passes.Count; passIndex++) // 遍历当前图里的所有 Pass。
            {
                var pass = passes[passIndex]; // 取出当前索引对应的 Pass。

                if (pass == null) // 如果 Pass 为空，说明当前图里存在异常条目。
                {
                    AddValidationMessage("Pass #" + passIndex + " 为空，配置阶段已跳过。"); // 记录空 Pass 的索引，便于修复组装器。

                    continue; // 跳过空 Pass，避免创建无意义资源声明。
                }

                var builder = new BurtRenderPassBuilder(passIndex, pass, context.Request, context.Asset, resources, context.RenderOptions); // 为当前 Pass 创建资源声明 Builder，并注入当前 request、asset 与 RT 生命周期选项。

                try // Configure 只负责声明依赖，异常不应该直接阻断后续 Debug 信息收集。
                {
                    pass.Configure(builder); // 让当前 Pass 声明自己读取和写入哪些资源。
                }
                catch (Exception exception) // 捕获配置阶段异常，保留渲染执行顺序但让 dump 能指出具体 Pass。
                {
                    builder.Usage.AddValidationMessage("Configure 异常: " + exception.GetType().Name + " - " + exception.Message); // 把异常摘要写入当前 Pass 的校验消息。

                    AddValidationMessage("Pass #" + passIndex + " (" + GetPassName(pass) + ") Configure 抛出异常，已继续收集后续 Pass。"); // 写入图级别摘要。
                }

                ValidateResourceHandleVersions(builder.Usage);
                resourceUsages.Add(builder.Usage); // 把当前 Pass 的资源使用记录保存到 RenderGraph。
            }
        }

        private void ValidateResourceHandleVersions(BurtRenderPassResourceUsage usage)
        {
            if (usage == null)
            {
                return;
            }

            for (var index = 0; index < usage.ReadRenderTargets.Count; index++)
            {
                ValidateRenderTargetVersion(usage, usage.ReadRenderTargets[index]);
            }

            for (var index = 0; index < usage.WriteRenderTargets.Count; index++)
            {
                ValidateRenderTargetVersion(usage, usage.WriteRenderTargets[index]);
            }

            for (var index = 0; index < usage.ReadBuffers.Count; index++)
            {
                ValidateBufferVersion(usage, usage.ReadBuffers[index]);
            }

            for (var index = 0; index < usage.WriteBuffers.Count; index++)
            {
                ValidateBufferVersion(usage, usage.WriteBuffers[index]);
            }
        }

        private void ValidateRenderTargetVersion(BurtRenderPassResourceUsage usage, BurtRenderTargetHandle handle)
        {
            if (handle.ResourceId.IsValid && !resources.IsCurrent(handle))
            {
                usage.AddValidationMessage("Stale RenderTarget handle: " + handle.Name + " v" + handle.Version);
            }
        }

        private void ValidateBufferVersion(BurtRenderPassResourceUsage usage, BurtRenderBufferHandle handle)
        {
            if (handle.ResourceId.IsValid && !resources.IsCurrent(handle))
            {
                usage.AddValidationMessage("Stale Buffer handle: " + handle.Name + " v" + handle.Version);
            }
        }

        private void ValidateConfiguredGraph() // 对已收集的资源声明做轻量校验，当前阶段只产生日志，不改变实际渲染行为。
        {
            BurtRenderGraphValidationUtility.ValidateConfiguredGraph(passes, resourceUsages, resources, AddValidationMessage); // 交给诊断工具集中检查读写声明，保持执行类只负责调度。
        }

        private void AddValidationMessage(string message) // 定义图级别校验消息追加函数，带简单去重避免 Debug 开关打开时噪音过大。
        {
            if (string.IsNullOrEmpty(message)) // 空消息没有诊断价值。
            {
                return; // 直接忽略。
            }

            if (validationMessages.Contains(message)) // 相同图级消息只保留一次。
            {
                return; // 已存在时不重复添加。
            }

            validationMessages.Add(message); // 追加到当前图的校验消息列表。
        }

        private static string GetPassName(BurtRenderPass pass) // 安全读取 Pass 名称，避免异常路径再次触发空引用。
        {
            return pass != null && !string.IsNullOrEmpty(pass.Name) ? pass.Name : "UnnamedPass"; // 名称缺失时使用兜底文本。
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal readonly struct BurtRenderGraphProfilingEvent
    {
        public BurtRenderGraphProfilingEvent(int passIndex, string scopeName, ProfilingSampler sampler, bool isBegin)
        {
            PassIndex = passIndex;
            ScopeName = scopeName;
            Sampler = sampler;
            IsBegin = isBegin;
        }

        public int PassIndex { get; }
        public string ScopeName { get; }
        public ProfilingSampler Sampler { get; }
        public bool IsBegin { get; }
    }
#endif
}
