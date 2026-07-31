using System; // 引入基础命名空间，用来捕获 Configure 阶段异常并写入诊断信息。
using System.Collections.Generic; // Uses List for passes, resource declarations, and validation messages.
using UnityEngine.Rendering;

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让这个类和其他 BurtRP 代码处在同一个模块里。
{
    public sealed class BurtRenderGraph // 定义 BurtRP 的最小渲染图类，当前阶段负责保存 Pass、资源表和资源读写声明。
    {
        private readonly List<BurtRenderPass> passes = new List<BurtRenderPass>(); // 创建一个可复用的 Pass 列表，避免每帧重复分配 List。

        private readonly List<BurtRenderPassResourceUsage> resourceUsages = new List<BurtRenderPassResourceUsage>(); // 创建一个可复用的资源使用记录列表，用来保存每个 Pass 的读写声明。

        private readonly List<string> validationMessages = new List<string>(); // 保存当前图级别的轻量校验消息，只用于 Debug dump，不改变实际渲染顺序。

        private readonly BurtRenderGraphResourceRegistry resources = new BurtRenderGraphResourceRegistry(); // 创建一个可复用的资源注册表，用来保存当前图里的渲染目标资源。

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly Dictionary<string, ProfilingSampler> profilingSamplers = new Dictionary<string, ProfilingSampler>(StringComparer.Ordinal);
        private readonly Dictionary<string, BurtRenderGraphProfilingMarkerPass> beginProfilingPasses = new Dictionary<string, BurtRenderGraphProfilingMarkerPass>(StringComparer.Ordinal);
        private readonly Dictionary<string, BurtRenderGraphProfilingMarkerPass> endProfilingPasses = new Dictionary<string, BurtRenderGraphProfilingMarkerPass>(StringComparer.Ordinal);
        private readonly List<string> profilingAssemblyScopeStack = new List<string>();
        private readonly List<BurtRenderGraphProfilingMarkerPass> activeProfilingScopes = new List<BurtRenderGraphProfilingMarkerPass>();
#endif

        public int PassCount => passes.Count; // 暴露当前图里有多少个 Pass，方便后面调试或判断图是否为空。

        public BurtRenderGraphResourceRegistry Resources => resources; // 暴露当前 RenderGraph 的资源注册表，让 Context 和 Assembler 可以读取资源。

        public IReadOnlyList<BurtRenderPassResourceUsage> ResourceUsages => resourceUsages; // 暴露只读资源使用记录，方便后面调试或做依赖分析。

        public IReadOnlyList<string> ValidationMessages => validationMessages; // 暴露只读校验消息，供调试工具集中输出 RenderGraph 问题。

        public void Clear() // 定义清空函数，每次组装新 request 前都要调用。
        {
            passes.Clear(); // 清空上一轮 request 组装出来的 Pass，避免 Pass 残留到下一次渲染。

            resourceUsages.Clear(); // 清空上一轮 Pass 的资源读写声明，避免调试数据残留。

            validationMessages.Clear(); // 清空上一轮图校验消息，避免不同相机或 request 互相污染。

            resources.Clear(); // 清空上一轮 request 注册的资源，避免 CameraColor 和 CameraDepth 等资源残留到下一次渲染。

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            profilingAssemblyScopeStack.Clear();
            activeProfilingScopes.Clear();
#endif
        }

        public void BeginProfilingScope(string name)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var safeName = NormalizeProfilingName(name, "BRP.Stage/Unnamed");
            profilingAssemblyScopeStack.Add(safeName);
            AddPass(GetOrCreateProfilingMarkerPass(safeName, true));
#endif
        }

        public void EndProfilingScope(string name)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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

            AddPass(GetOrCreateProfilingMarkerPass(safeName, false));
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
            }

            resources.RegisterBuffer(BurtRenderGraphResourceRegistry.AdditionalLightBufferName, BurtLightingData.CreateAdditionalLightBufferDescriptor()); // Register the graph-owned additional light buffer used by future tiled/cluster lighting.

            if (ShouldRegisterPostProcessColor(request, asset)) // 如果当前 request 启用了后处理框架，就把后处理中间颜色纳入资源表。
            {
                resources.RegisterPostProcessColorTexture(); // 注册 PostProcessColor 临时 RT，让分配、No-op Copy 和释放 Pass 使用同一个资源句柄。
                resources.RegisterTemporalAAOutputTexture();
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
                    resources.RegisterScreenSpaceGlobalIlluminationUpsampledTexture();
                    resources.RegisterBurtGIBackfaceDiffuseIndirectTexture();
                    resources.RegisterBurtGIBackfaceDiffuseIndirectUpsampledTexture();
                    resources.RegisterBurtGIRoughSpecularIndirectTexture();
                    resources.RegisterBurtGIRoughSpecularIndirectUpsampledTexture();
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
                        resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeIndirectArgsBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationScreenProbeIndirectArgsBufferDescriptor());
                        resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeIntegrateTileIndirectArgsBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationScreenProbeIntegrateTileIndirectArgsBufferDescriptor());
                        resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeIntegrateTileDataDiffuseBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationScreenProbeIntegrateTileDataDiffuseBufferDescriptor(request.Camera, screenProbeSettings));
                        resources.RegisterBuffer(BurtRenderGraphResourceRegistry.BurtGIScreenProbeIntegrateTileDataAllBufferName, BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationScreenProbeIntegrateTileDataAllBufferDescriptor(request.Camera, screenProbeSettings));
                        resources.RegisterBurtGIScreenProbeIntegrateTileClassificationTexture();
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
                    resources.RegisterScreenSpaceSubsurfaceSetupTexture();
                    resources.RegisterScreenSpaceSubsurfaceProfileIDAndTypeTexture();
                    if (BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceMaskTexture(request, asset))
                    {
                        resources.RegisterScreenSpaceSubsurfaceMaskTexture();
                    }

                    resources.RegisterScreenSpaceSubsurfaceTempTexture();
                    resources.RegisterScreenSpaceSubsurfaceBlurTexture();
                    resources.RegisterScreenSpaceSubsurfaceCombineTexture();
                    if (BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceBurley(request, asset))
                    {
                        resources.RegisterBuffer(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBurleyArgsBufferName, BurtScreenSpaceSubsurfacePassUtility.CreateBurleyArgsBufferDescriptor());
                        resources.RegisterBuffer(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBurleyGroupBufferName, BurtScreenSpaceSubsurfacePassUtility.CreateBurleyGroupBufferDescriptor(request.Camera));
                        resources.RegisterScreenSpaceSubsurfaceHistoryTexture();
                        resources.RegisterScreenSpaceSubsurfaceVelocityTexture();
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

            ConfigurePasses(context); // 在真正执行前收集所有 Pass 的资源读写声明，并把当前上下文传给配置阶段。

            ValidateConfiguredGraph(); // 对配置结果做轻量校验，只记录问题，不重排 Pass，也不改变 RenderTarget 绑定逻辑。

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (profilingAssemblyScopeStack.Count > 0)
            {
                AddValidationMessage("RenderGraph 存在未闭合 Profiling scope: " + profilingAssemblyScopeStack[profilingAssemblyScopeStack.Count - 1]);
            }

            var profilingCommandBuffer = CommandBufferPool.Get("BRP.RenderGraph/Profiling");
            var profileIndividualPasses = context.Asset != null && context.Asset.EnableRenderGraphDebug;
            activeProfilingScopes.Clear();
            try
            {
                for (var passIndex = 0; passIndex < passes.Count; passIndex++)
                {
                    var pass = passes[passIndex];
                    if (pass == null)
                    {
                        continue;
                    }

                    if (pass is BurtRenderGraphProfilingMarkerPass markerPass)
                    {
                        ExecuteProfilingMarker(context, profilingCommandBuffer, markerPass);
                        continue;
                    }

                    if (!profileIndividualPasses)
                    {
                        pass.Execute(context);
                        continue;
                    }

                    var passSampler = GetOrCreateProfilingSampler("BRP.Pass/" + GetPassName(pass));
                    passSampler.Begin(profilingCommandBuffer);
                    ExecuteAndClearProfilingCommands(context, profilingCommandBuffer);
                    try
                    {
                        pass.Execute(context);
                    }
                    finally
                    {
                        passSampler.End(profilingCommandBuffer);
                        ExecuteAndClearProfilingCommands(context, profilingCommandBuffer);
                    }
                }
            }
            finally
            {
                for (var scopeIndex = activeProfilingScopes.Count - 1; scopeIndex >= 0; scopeIndex--)
                {
                    activeProfilingScopes[scopeIndex].Sampler.End(profilingCommandBuffer);
                    ExecuteAndClearProfilingCommands(context, profilingCommandBuffer);
                }

                activeProfilingScopes.Clear();
                profilingCommandBuffer.Clear();
                CommandBufferPool.Release(profilingCommandBuffer);
            }
#else
            for (var passIndex = 0; passIndex < passes.Count; passIndex++) // 从前到后遍历当前图里的所有 Pass。
            {
                var pass = passes[passIndex]; // 取出当前索引对应的 Pass。

                if (pass == null) // 如果当前 Pass 是空，说明列表里存在异常数据。
                {
                    continue; // 跳过这个空 Pass，继续执行后面的 Pass。
                }

                pass.Execute(context); // Execute the current pass without changing its internal command buffer behavior.
            }
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void ExecuteProfilingMarker(
            BurtRenderGraphContext context,
            CommandBuffer commandBuffer,
            BurtRenderGraphProfilingMarkerPass markerPass)
        {
            if (markerPass.IsBegin)
            {
                markerPass.Sampler.Begin(commandBuffer);
                activeProfilingScopes.Add(markerPass);
            }
            else
            {
                if (activeProfilingScopes.Count == 0)
                {
                    AddValidationMessage("执行 Profiling End 时没有活动 Scope: " + markerPass.ScopeName);
                    return;
                }

                var lastIndex = activeProfilingScopes.Count - 1;
                var openedPass = activeProfilingScopes[lastIndex];
                activeProfilingScopes.RemoveAt(lastIndex);
                if (!string.Equals(openedPass.ScopeName, markerPass.ScopeName, StringComparison.Ordinal))
                {
                    AddValidationMessage("执行 Profiling Scope 顺序不匹配: expected " + openedPass.ScopeName + ", actual " + markerPass.ScopeName);
                }

                openedPass.Sampler.End(commandBuffer);
            }

            ExecuteAndClearProfilingCommands(context, commandBuffer);
        }

        private static void ExecuteAndClearProfilingCommands(BurtRenderGraphContext context, CommandBuffer commandBuffer)
        {
            context.ScriptableContext.ExecuteCommandBuffer(commandBuffer);
            commandBuffer.Clear();
        }

        private BurtRenderGraphProfilingMarkerPass GetOrCreateProfilingMarkerPass(string name, bool isBegin)
        {
            var cache = isBegin ? beginProfilingPasses : endProfilingPasses;
            if (cache.TryGetValue(name, out var markerPass))
            {
                return markerPass;
            }

            markerPass = new BurtRenderGraphProfilingMarkerPass(name, GetOrCreateProfilingSampler(name), isBegin);
            cache.Add(name, markerPass);
            return markerPass;
        }

        private ProfilingSampler GetOrCreateProfilingSampler(string name)
        {
            var safeName = NormalizeProfilingName(name, "BRP.Unknown");
            if (profilingSamplers.TryGetValue(safeName, out var sampler))
            {
                return sampler;
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
            return BurtRenderGraphDebugUtility.BuildDump(request, passes.Count, resourceUsages, validationMessages, resources, renderOptions); // 把 request、Pass、资源声明、校验和 RT 生命周期选项交给统一工具格式化。
        }

        public void FlushDeferredResourceReleases()
        {
            resources.FlushDeferredBufferReleases();
        }

        public string DumpDebugInfo( // 定义带管线资产状态的 RenderGraph 调试文本入口。
            BurtRenderRequest request, // 接收当前渲染请求，用来输出 Request 和 Camera 信息。
            BurtRenderPipelineAsset asset, // 接收当前 BurtRP 管线资产，用来输出 Renderer Mode 和 Debug View 状态。
            BurtRequestRenderOptions renderOptions) // 接收当前 request 的栈级 RT 生命周期选项。
        {
            return BurtRenderGraphDebugUtility.BuildDump(request, passes.Count, resourceUsages, validationMessages, resources, renderOptions, asset); // 把 request、资源声明、RT 生命周期和资产调试状态交给统一工具格式化。
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

                resourceUsages.Add(builder.Usage); // 把当前 Pass 的资源使用记录保存到 RenderGraph。
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
    internal sealed class BurtRenderGraphProfilingMarkerPass : BurtRenderPass
    {
        public BurtRenderGraphProfilingMarkerPass(string scopeName, ProfilingSampler sampler, bool isBegin)
        {
            ScopeName = scopeName;
            Sampler = sampler;
            IsBegin = isBegin;
        }

        public string ScopeName { get; }
        public ProfilingSampler Sampler { get; }
        public bool IsBegin { get; }
        public override string Name => IsBegin ? "Begin " + ScopeName : "End " + ScopeName;
        public override BurtRenderPassKind Kind => BurtRenderPassKind.Debug;

        public override void Execute(BurtRenderGraphContext context)
        {
            // BurtRenderGraph executes marker passes with its shared profiling command buffer.
        }
    }
#endif
}
